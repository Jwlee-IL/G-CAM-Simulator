using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Decoding;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Serialized worker: measure each new event once, accumulate windows, calibrate and decode.
/// Subtraction uses the raw high floods simultaneously, never recursively stripped floods.</summary>
public sealed class ImagingService : IImagingService
{
    // Above the specified 20,000 minimum, to reduce calibration uncertainty without reusing deposits.
    public const int CalibrationEvents = 100_000;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly FrontEndModel _model = new(FrontEndParts.Default.BuildConfig());
    private Guid _id;
    private double _n = double.NaN;
    private readonly List<double> _energies = [];
    private DetectorImage[] _floods = [];
    private StripRatio[] _ratios = [];
    private int _consumed;

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
        if (scene.Count == 0) throw new ArgumentException("Imaging needs a source.", nameof(scene));
        // Freeze caller-owned mutable source objects before crossing the worker boundary.
        var physical = snapshot.Optics ?? optics;
        var config = SimulationService.BuildConfig(scene, physical, snapshot.Detector ?? new DetectorSettings());
        if (snapshot.Imaging.Flood.Width != config.Detector.PixelsX || snapshot.Imaging.Flood.Height != config.Detector.PixelsY)
            throw new ArgumentException("Snapshot dimensions do not match acquired optics.", nameof(snapshot));
        var projection = ImagingProjection.AtFocus(config, physical, settings.FocalDistanceMm ?? optics.FocalDistanceMm);
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
            if (id != _id || snapshot.Events.Count < _energies.Count)
            {
                _id = id;
                _n = double.NaN;
                _energies.Clear();
            }
            int previousMeasured = _energies.Count;
            var sources = config.Sources!;
            if (snapshot.Events.Count == 0)
            {
                var empty = new List<ImagingChannel> { ImagingProjection.Project(snapshot.Imaging.Flood, snapshot.Imaging, projection, "All", 0) };
                foreach (var group in sources.GroupBy(s => s.Isotope))
                    empty.Add(ImagingProjection.Project(new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY).ReadOnlyCopy(),
                        snapshot.Imaging, projection, group.Key, 0));
                return new(empty.AsReadOnly(), Array.Empty<StripRatio>(), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            }
            var groups = sources.GroupBy(s => s.Isotope!).ToArray();
            var lines = groups.SelectMany(g => Isotopes.Get(g.Key).Lines.Select(l => new SpectrumLine(g.Key, l.EnergyKeV))).ToArray();
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
            if (settings.Strip)
            {
                for (int j = 0; j < groups.Length; j++)
                {
                    var flood = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
                    var ratios = _ratios.Where(r => r.LowIsotope == groups[j].Key).ToArray();
                    for (int y = 0; y < flood.Height; y++)
                    for (int x = 0; x < flood.Width; x++)
                        flood[x, y] = Math.Max(0, _floods[j][x, y] - ratios.Sum(r =>
                            r.R * _floods[Array.FindIndex(groups, g => g.Key == r.HighIsotope)][x, y]));
                    corrected[j] = flood.ReadOnlyCopy();
                }
            }
            channels.Stop();
            var decode = Stopwatch.StartNew();
            var results = new List<ImagingChannel>
            {
                ImagingProjection.Project(snapshot.Imaging.Flood, snapshot.Imaging, projection, "All", 0)
            };
            for (int j = 0; j < groups.Length; j++)
            {
                token.ThrowIfCancellationRequested();
                results.Add(ImagingProjection.Project(corrected[j], snapshot.Imaging, projection, groups[j].Key,
                    groups[j].Count(), windows[j].Lo, windows[j].Hi));
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
