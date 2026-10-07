using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Decoding;
using Gcam.Simulation;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Serialized worker: measure each new event once, accumulate windows, calibrate and decode.
/// Subtraction uses the raw high floods simultaneously, never recursively stripped floods.</summary>
public sealed class ImagingService : IImagingService
{
    // Above the specified 20,000 minimum, to reduce calibration uncertainty without reusing deposits.
    public const int CalibrationEvents = 100_000;
    private readonly SemaphoreSlim _gate = new(1);
    private FrontEndModel _model = new(new DetectorSettings().Chain.BuildConfig());
    private DetectorSettings? _detector;
    private Guid _id;
    private double _n = double.NaN;
    private readonly List<double> _energies = [];
    private DetectorImage[] _floods = [];
    private StripRatio[] _ratios = [];
    private int _consumed;
    // Pixel-area MLEM matrices survive refreshes, acquisitions and Reset (keyed by geometry and line, not by acquisition).
    private readonly MlemDecoderCache _mlem = new();

    /// <summary>MLEM matrices built so far (diagnostics and tests).</summary>
    public int MlemMatrixBuilds => _mlem.Builds;

    public async Task<ImagingView> ProcessAsync(Guid acquisitionId, AcquisitionSnapshot snapshot,
        IReadOnlyList<SceneSource> scene, OpticsSettings optics, ImagingSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(optics);
        ArgumentNullException.ThrowIfNull(settings);
        if (!double.IsFinite(settings.WindowFwhm) || settings.WindowFwhm <= 0)
            throw new ArgumentOutOfRangeException(nameof(settings));
        if (scene.Count == 0 && snapshot.AmbientMaximumEnergyKeV is null) throw new ArgumentException("Imaging needs a source or an absolute field.", nameof(scene));
        // Freeze caller-owned mutable source objects before crossing the worker boundary.
        var physical = snapshot.Optics ?? optics;
        var config = SimulationService.BuildConfig(scene, physical, snapshot.Detector ?? new DetectorSettings(),
            ambient: scene.Count == 0 ? new AmbientFieldConfig() : null);
        if (snapshot.Imaging.Flood.Width != config.Detector.PixelsX || snapshot.Imaging.Flood.Height != config.Detector.PixelsY)
            throw new ArgumentException("Snapshot dimensions do not match acquired optics.", nameof(snapshot));
        var projection = ImagingProjection.AtFocus(config, physical, settings.FocalDistanceMm ?? optics.FocalDistanceMm);
        if (settings.Method == DecoderMethod.Mlem) projection.Decoder.MlemIterations = StudioMlem.Iterations;
        if (scene.Count == 0)
        {
            // Source-free: the field's highest energy sets the (largest, conservative) closed-cell transmission.
            var channel = ImagingProjection.Project(snapshot.Imaging.Flood, snapshot.Imaging, projection, "All", 0, double.NaN, double.NaN,
                Mlem(settings, snapshot.AmbientMaximumEnergyKeV ?? 661.7));
            return new(Array.AsReadOnly(new[] { channel }), Array.Empty<StripRatio>(), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
        }
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Process(acquisitionId, snapshot, config, projection, settings, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private ImagingView Process(Guid id, AcquisitionSnapshot snapshot, SimulationConfig config, SimulationConfig projection,
        ImagingSettings settings, CancellationToken token)
    {
        try
        {
            if (id != _id || snapshot.Detector != _detector || snapshot.Events.Count < _energies.Count)
            {
                _id = id;
                _detector = snapshot.Detector;
                _model = new FrontEndModel(snapshot.Chain.BuildConfig());
                _n = double.NaN;
                _energies.Clear();
            }
            int previousMeasured = _energies.Count;
            var sources = config.Sources!;
            if (snapshot.Events.Count == 0)
            {
                var empty = new List<ImagingChannel> { ImagingProjection.Project(snapshot.Imaging.Flood, snapshot.Imaging, projection, "All", 0,
                    double.NaN, double.NaN, Mlem(settings, AllLineKeV(sources))) };
                foreach (var group in sources.GroupBy(s => s.Isotope))
                    empty.Add(ImagingProjection.Project(new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY).ReadOnlyCopy(),
                        snapshot.Imaging, projection, group.Key, 0, double.NaN, double.NaN, Mlem(settings, PrimaryKeV(group.Key))));
                return new(empty.AsReadOnly(), Array.Empty<StripRatio>(), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            }
            var groups = sources.GroupBy(s => s.Isotope!).ToArray();
            var lines = groups.SelectMany(g => Isotopes.Get(g.Key).Lines.Select(l => new SpectrumLine(g.Key, l.EnergyKeV, l.Kind, l.XRayOrigin))).ToArray();
            var bands = SpectrumService.BuildBands(lines, settings.WindowFwhm, _model);
            var windows = groups.Select(g =>
            {
                double energy = Isotopes.Get(g.Key).Lines[0].EnergyKeV;
                var band = bands.Single(b => b.Lines.Any(l => l.Isotope == g.Key && l.EnergyKeV == energy));
                return (Lo: band.LoKeV, Hi: band.HiKeV);
            }).ToArray();
            var calibration = Stopwatch.StartNew();
            bool changed = _n != settings.WindowFwhm;
            if (changed)
            {
                _ratios = Calibrate(config, groups, windows, token);
                _floods = groups.Select(_ => new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY)).ToArray();
                _consumed = 0;
                _n = settings.WindowFwhm;
            }
            calibration.Stop();
            var channels = Stopwatch.StartNew();
            var measurement = new MeasurementStage(snapshot.Detector, config.Detector.PixelsX, config.Detector.PixelsY);
            for (int i = _energies.Count; i < snapshot.Events.Count; i++)
            {
                if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
                _energies.Add(measurement.Measure(snapshot.Events[i], i));
            }
            for (; _consumed < snapshot.Events.Count; _consumed++)
            {
                if ((_consumed & 4095) == 0) token.ThrowIfCancellationRequested();
                var ev = snapshot.Events[_consumed];
                for (int j = 0; j < groups.Length; j++)
                    if (_energies[_consumed] >= windows[j].Lo && _energies[_consumed] <= windows[j].Hi)
                        _floods[j].Add(ev.PixelX, ev.PixelY, 1);
            }
            var corrected = _floods.Select(f => f.ReadOnlyCopy()).ToArray();
            var mlem = groups.Select(g => Mlem(settings, PrimaryKeV(g.Key))).ToArray();
            if (settings.Strip)
            {
                for (int j = 0; j < groups.Length; j++)
                {
                    var flood = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
                    var background = new double[flood.Width * flood.Height];
                    var ratios = _ratios.Where(r => r.LowIsotope == groups[j].Key).ToArray();
                    for (int y = 0; y < flood.Height; y++)
                    for (int x = 0; x < flood.Width; x++)
                    {
                        double higher = ratios.Sum(r => r.R * _floods[Array.FindIndex(groups, g => g.Key == r.HighIsotope)][x, y]);
                        background[y * flood.Width + x] = higher;
                        flood[x, y] = Math.Max(0, _floods[j][x, y] - higher);
                    }
                    corrected[j] = flood.ReadOnlyCopy();
                    // MLEM keeps the raw window counts (Poisson) and models the higher lines' downscatter as the known
                    // background b_i = Σ R·high_i (MD-6); the displayed flood stays the clipped strip view. Its effective
                    // count is the unclipped difference Σ low − Σ b, an unbiased estimate of the channel's own counts.
                    if (mlem[j] is { } m && ratios.Length > 0)
                        mlem[j] = m with { Flood = _floods[j].ReadOnlyCopy(), Background = background,
                            EffectiveCounts = _floods[j].Raw.ToArray().Sum() - background.Sum() };
                }
            }
            channels.Stop();
            var decode = Stopwatch.StartNew();
            var results = new List<ImagingChannel>
            {
                ImagingProjection.Project(snapshot.Imaging.Flood, snapshot.Imaging, projection, "All", 0, double.NaN, double.NaN,
                    Mlem(settings, AllLineKeV(sources)))
            };
            for (int j = 0; j < groups.Length; j++)
            {
                token.ThrowIfCancellationRequested();
                results.Add(ImagingProjection.Project(corrected[j], snapshot.Imaging, projection, groups[j].Key,
                    groups[j].Count(), windows[j].Lo, windows[j].Hi, mlem[j]));
            }
            results[0] = results[0] with { Peaks = Array.AsReadOnly(results.Skip(1).SelectMany(c => c.Peaks).ToArray()) };
            decode.Stop();
            return new(Array.AsReadOnly(results.ToArray()), Array.AsReadOnly(_ratios), channels.Elapsed,
                changed ? calibration.Elapsed : TimeSpan.Zero, decode.Elapsed)
                { NewlyMeasuredEvents = _energies.Count - previousMeasured };
        }
        catch
        {
            // No partially filtered prefix or partially calibrated response is reusable after cancellation.
            _id = Guid.Empty;
            _n = double.NaN;
            _energies.Clear();
            throw;
        }
    }

    private MlemProjection? Mlem(ImagingSettings settings, double lineEnergyKeV)
        => settings.Method == DecoderMethod.Mlem ? new MlemProjection(_mlem, lineEnergyKeV) : null;

    private static double PrimaryKeV(string isotope) => Isotopes.Get(isotope).Lines[0].EnergyKeV;

    /// <summary>All mixes every line; the highest primary line gives the largest closed-cell transmission, the
    /// conservative side (an over-estimated leak smooths, an under-estimated one adds false peaks — TODO-36 review).</summary>
    private static double AllLineKeV(IEnumerable<SourceConfig> sources) => sources.Select(s => PrimaryKeV(s.Isotope)).DefaultIfEmpty(661.7).Max();

    private StripRatio[] Calibrate(SimulationConfig config, IGrouping<string, SourceConfig>[] groups,
        (double Lo, double Hi)[] windows, CancellationToken token)
    {
        var ratios = new List<StripRatio>();
        for (int h = 0; h < groups.Length; h++)
        {
            var lows = Enumerable.Range(0, groups.Length).Where(l => l != h &&
                Isotopes.Get(groups[h].Key).Lines.Any(line => line.EnergyKeV > windows[l].Hi)).ToArray();
            if (lows.Length == 0) continue;
            var highOnly = config.Clone();
            highOnly.Sources = groups[h].ToArray();
            highOnly.Source = highOnly.Sources[0];
            highOnly.Background = null; // Calibration is H-only, including all its real emission lines.
            highOnly.Seed = unchecked((config.Seed ?? 0) + 6007 + h * 1009);
            using var source = new ListModeSource(highOnly);
            var detector = new DetectorSettings
            {
                Chain = (_detector ?? new DetectorSettings()).Chain,
                GainSigma = config.Detector.GainSigma, GainSeed = config.Detector.UniformitySeed
            };
            var measurement = new MeasurementStage(detector, config.Detector.PixelsX, config.Detector.PixelsY);
            var counts = new long[groups.Length];
            int accepted = 0;
            while (accepted < CalibrationEvents)
            {
                if (source.Advance(token) is not { } ev) continue;
                double energy = measurement.Measure(ev, accepted++);
                foreach (int j in lows.Append(h))
                    if (energy >= windows[j].Lo && energy <= windows[j].Hi) counts[j]++;
            }
            if (counts[h] == 0) throw new InvalidOperationException($"No {groups[h].Key} calibration counts in its primary window.");
            foreach (int l in lows)
                ratios.Add(new(groups[l].Key, groups[h].Key, accepted, counts[l], counts[h]));
        }
        return ratios.ToArray();
    }
}
