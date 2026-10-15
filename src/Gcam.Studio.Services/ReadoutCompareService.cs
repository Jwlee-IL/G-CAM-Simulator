using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>One serialized comparison worker. Only appended inputs are aggregated; view changes rebuild aggregates.
/// Decoder matrices survive resets. No transport, calibration or legacy measurement occurs here.</summary>
public sealed class ReadoutCompareService : IReadoutCompareService
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly MlemDecoderCache _mlem = new();
    private readonly ReadoutOfferRecord _offers = ReadoutOfferRecordLoader.LoadEmbedded();
    private Guid _id, _preparation;
    private CompareSettings? _settings;
    private int _hits, _held, _assigned, _unknown, _multi, _noDominant;
    private long _directWindow, _angerWindow, _directOverflow, _angerOverflow;
    private DetectorImage _direct = new(1, 1);
    private long[] _eligible = [], _different = [];
    private double[] _directSpectrum = [], _angerSpectrum = [];
    private readonly HashSet<int> _dominants = [];

    public async Task<CompareResult> ProcessAsync(CompareKey key, AcquisitionSnapshot snapshot,
        IReadOnlyList<SceneSource> scene, CompareSettings settings, CancellationToken cancellationToken = default)
    {
        var readout = snapshot.Readout ?? throw new ArgumentException("Compare requires a physical acquisition.", nameof(snapshot));
        if (key.HitCount != readout.Hits.Count || key.HeldCount != readout.Records.Count || key.LiveTimeS != snapshot.LiveTimeS)
            throw new ArgumentException("Comparison key must name the captured prefix.", nameof(key));
        var optics = snapshot.Optics ?? throw new ArgumentException("Acquired optics are missing.", nameof(snapshot));
        var detector = snapshot.Detector ?? throw new ArgumentException("Acquired detector is missing.", nameof(snapshot));
        var frozenScene = scene.Select(s => new SceneSource { Isotope = s.Isotope, X = s.X, Y = s.Y,
            DistanceMm = s.DistanceMm, ActivityUCi = s.ActivityUCi }).ToArray();
        // BuildConfig freezes mutable scene inputs before entering the worker; it transports nothing.
        var config = SimulationService.BuildConfig(frozenScene, optics, detector);
        var projection = ImagingProjection.AtFocus(config, optics, settings.Imaging.FocalDistanceMm ?? optics.FocalDistanceMm);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Process(key, snapshot, frozenScene, settings, projection, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private CompareResult Process(CompareKey key, AcquisitionSnapshot snapshot, IReadOnlyList<SceneSource> scene,
        CompareSettings settings, SimulationConfig projection, CancellationToken token)
    {
        var total = Stopwatch.StartNew();
        var readout = snapshot.Readout!;
        var imaging = settings.Imaging;
        PhysicalReadoutProjection.ValidateWindow(imaging.WindowLowKeV, imaging.WindowHighKeV);
        double? line = settings.Window ? ReadoutPolicy.LineForWindow(imaging.WindowLowKeV, imaging.WindowHighKeV, out _) : readout.Preparation.LineKeV;
        var view = line is { } energy ? PhysicalReadoutProjection.LineView(readout, energy) : null;
        if (view is not null && view.Assignments.Count != readout.Records.Count)
            throw new ArgumentException("Line assignments must cover the same held prefix.");
        int nx = snapshot.Imaging.Flood.Width, ny = snapshot.Imaging.Flood.Height;
        if (_id != key.AcquisitionId || _settings != settings || _preparation != view?.Preparation.Id ||
            _hits > readout.Hits.Count || _held > readout.Records.Count)
        {
            _id = key.AcquisitionId; _settings = settings; _preparation = view?.Preparation.Id ?? Guid.Empty;
            _hits = _held = _assigned = _unknown = _multi = _noDominant = 0;
            _directWindow = _angerWindow = _directOverflow = _angerOverflow = 0;
            _direct = new(nx, ny); _eligible = new long[nx * ny]; _different = new long[nx * ny];
            _directSpectrum = new double[SpectrumService.BinCount]; _angerSpectrum = new double[SpectrumService.BinCount];
            _dominants.Clear();
        }
        try
        {
            bool InWindow(double e) => e >= imaging.WindowLowKeV && e <= imaging.WindowHighKeV;
            static void Bin(double energy, double[] bins, ref long overflow)
            {
                if (!double.IsFinite(energy) || energy < 0) throw new ArgumentException("Energy must be finite and nonnegative.");
                if (energy >= SpectrumService.AxisMaximumKeV) overflow++;
                else bins[(int)(energy / SpectrumService.BinWidthKeV)]++;
            }
            for (; _hits < readout.Hits.Count; _hits++)
            {
                if ((_hits & 4095) == 0) token.ThrowIfCancellationRequested();
                var h = readout.Hits[_hits];
                if (h.PixelX < 0 || h.PixelX >= nx || h.PixelY < 0 || h.PixelY >= ny)
                    throw new ArgumentException("Retained direct pixel is outside the acquired detector.");
                Bin(h.DepositKeV, _directSpectrum, ref _directOverflow);
                if (InWindow(h.DepositKeV)) _directWindow++;
                if (!settings.Window || InWindow(h.DepositKeV)) _direct.Add(h.PixelX, h.PixelY, 1);
            }
            for (; _held < readout.Records.Count; _held++)
            {
                if ((_held & 4095) == 0) token.ThrowIfCancellationRequested();
                var e = readout.Records[_held];
                if (e.DominantHit < -1 || e.DominantHit >= readout.Hits.Count)
                    throw new ArgumentException("Dominant index is outside the captured physical prefix.");
                if (e.DominantHit >= 0) _dominants.Add(e.DominantHit); else _noDominant++;
                if (e.Contributors > 1) _multi++;
                if (view is null) continue;
                var a = view.Assignments[_held];
                if (a.Crystal < 0) { _unknown++; continue; }
                if (a.Crystal >= nx * ny) throw new ArgumentException("LUT crystal is outside the acquired detector.");
                _assigned++; Bin(a.EnergyKeV, _angerSpectrum, ref _angerOverflow);
                if (InWindow(a.EnergyKeV)) _angerWindow++;
                if (e.Contributors != 1 || e.DominantHit < 0 || settings.Window && !InWindow(a.EnergyKeV)) continue;
                var h = readout.Hits[e.DominantHit]; int pixel = h.PixelY * nx + h.PixelX;
                _eligible[pixel]++;
                if (pixel != a.Crystal) _different[pixel]++;
            }
            var aggregate = total.Elapsed;
            var decode = Stopwatch.StartNew();
            if (imaging.Method == DecoderMethod.Mlem) projection.Decoder.MlemIterations = StudioMlem.Iterations;
            var direct = ImagingProjection.Project(_direct.ReadOnlyCopy(), snapshot.Imaging, projection, "Direct crystal", 0,
                settings.Window ? imaging.WindowLowKeV : double.NaN, settings.Window ? imaging.WindowHighKeV : double.NaN,
                imaging.Method == DecoderMethod.Mlem ? new(_mlem, line ?? readout.Preparation.LineKeV) : null);
            token.ThrowIfCancellationRequested();
            var anger = PhysicalReadoutProjection.Selected(snapshot, projection, imaging, settings.Window, _mlem, token);
            var difference = new DetectorImage(nx, ny);
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                int p = y * nx + x;
                difference[x, y] = _eligible[p] == 0 ? double.NaN : (double)_different[p] / _eligible[p];
            }
            long eligible = _eligible.Sum(), different = _different.Sum();
            var preparation = view?.Preparation ?? readout.Preparation;
            var cost = ReadoutCostApplicability.Evaluate(_offers, preparation, scene, snapshot.Optics!, snapshot.Detector!, settings,
                snapshot.RateCps, snapshot.AmbientRateCps > 0, snapshot.AmbientBackgroundToSignalRatio > 0);
            if (view is null) cost = new(null, [], "No cost cell: the selected window has no captured calibration line.");
            return new(key, settings, direct, anger, new(difference.ReadOnlyCopy(), Array.AsReadOnly((long[])_eligible.Clone()), Array.AsReadOnly((long[])_different.Clone())),
                new(Enumerable.Range(0, SpectrumService.BinCount + 1).Select(i => i * SpectrumService.BinWidthKeV).ToArray(),
                    (double[])_directSpectrum.Clone(), (double[])_angerSpectrum.Clone(), _directOverflow, _angerOverflow, line,
                    view is null ? "Spectrum unavailable: the selected window has no captured calibration line." : null),
                new(_hits, _held, _assigned, _unknown, _multi, _noDominant, _dominants.Count, _hits - _dominants.Count,
                    eligible, eligible - different, _directWindow, _angerWindow) { Participation = readout.Participation },
                new(aggregate, decode.Elapsed, total.Elapsed), cost);
        }
        catch { _settings = null; throw; } // an interrupted aggregation is rebuilt, never partly counted twice
    }
}
