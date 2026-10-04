using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Single-worker incremental MCA. The last paralyzable pile-up group stays open across snapshots.</summary>
public sealed class SpectrumService : ISpectrumService
{
    /// <summary>Fixed axis (AB-16): 0–2000 keV for every scene, field and pile-up setting; a measured pulse at or above
    /// the end is not discarded but counted in <see cref="SpectrumView.OverflowCounts"/>.</summary>
    public const double AxisMaximumKeV = 2000;
    /// <summary>2 keV bins: finer than the 2.97 keV the old Cs-only axis gave at 662 keV (FWHM ≈ 15 bins for the default
    /// chain), and at least two bins per FWHM at the narrowest offered peak (Ba K 32 keV, 4.4 keV FWHM with GAGG / CsI).</summary>
    public const double BinWidthKeV = 2;
    public const int BinCount = (int)(AxisMaximumKeV / BinWidthKeV);
    private readonly SemaphoreSlim _gate = new(1);
    private FrontEndModel _model = new(new DetectorSettings().Chain.BuildConfig());
    private double _resolvingTimeS;
    private Guid _acquisitionId;
    private bool _pileUp;
    private int _seed, _consumed, _groupStart;
    private double _lastArrival, _groupEnergy;
    private long _total, _overflow;
    private double[] _counts = new double[BinCount];
    private SpectrumSettings? _measurementSettings;
    private MeasurementStage _measurement = new(null, 0, 0);
    public double Resolution662 => _model.FwhmFraction(662);
    public double ResolvingTimeS => _resolvingTimeS;

    public SpectrumService()
    {
        var pulse = new DetectorSettings().Chain.PulseSamples;
        _resolvingTimeS = EventStreamStudy.ResolvingSamples(pulse.RiseSamples, pulse.TailSamples)
            / FrontEndParts.AdcSampleRateHz;
    }

    public async Task<SpectrumView> ProcessAsync(Guid acquisitionId, IReadOnlyList<DetectedEvent> events,
        IReadOnlyList<SpectrumLine> lines, SpectrumSettings settings, int seed = 909,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(settings);
        if (!(settings.WindowFwhm > 0) || !double.IsFinite(settings.WindowFwhm))
            throw new ArgumentOutOfRangeException(nameof(settings));
        if (lines.Count == 0 && !(settings.IncidentMaximumEnergyKeV > 0)
            || settings.IncidentMaximumEnergyKeV is { } maximum && (!(maximum > 0) || !double.IsFinite(maximum))
            || lines.Any(l => !(l.EnergyKeV > 0) || !double.IsFinite(l.EnergyKeV)))
            throw new ArgumentException("At least one finite positive emission energy is required.", nameof(lines));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Process(acquisitionId, events, lines, settings, seed, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private SpectrumView Process(Guid id, IReadOnlyList<DetectedEvent> events, IReadOnlyList<SpectrumLine> lines,
        SpectrumSettings settings, int seed, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        if (id != _acquisitionId || settings.PileUp != _pileUp || seed != _seed ||
            events.Count < _consumed ||
            settings.Detector != _measurementSettings?.Detector ||
            settings.PixelsX != _measurementSettings?.PixelsX || settings.PixelsY != _measurementSettings?.PixelsY)
        {
            _measurementSettings = settings;
            var chain = (settings.Detector ?? new DetectorSettings()).Chain;
            _model = new FrontEndModel(chain.BuildConfig());
            var pulse = chain.PulseSamples;
            _resolvingTimeS = EventStreamStudy.ResolvingSamples(pulse.RiseSamples, pulse.TailSamples)
                / FrontEndParts.AdcSampleRateHz;
            _measurement = new MeasurementStage(settings.Detector, settings.PixelsX, settings.PixelsY, seed);
            _acquisitionId = id;
            _pileUp = settings.PileUp;
            _seed = seed;
            _consumed = 0;
            _groupEnergy = 0;
            _counts = new double[BinCount];
            _total = _overflow = 0;
        }
        // Cancellation between requests is cheap; within a request invalidate the cache on cancellation.
        try
        {
            for (; _consumed < events.Count; _consumed++)
            {
                if ((_consumed & 4095) == 0) token.ThrowIfCancellationRequested();
                var ev = events[_consumed];
                if (!double.IsFinite(ev.DepositKeV) || ev.DepositKeV < 0 ||
                    !double.IsFinite(ev.ArrivalTimeS) || ev.ArrivalTimeS < 0 ||
                    (_consumed > 0 && ev.ArrivalTimeS < events[_consumed - 1].ArrivalTimeS))
                    throw new ArgumentException("Events must have ordered finite arrival times and nonnegative deposits.", nameof(events));
                double amplitude = _measurement.Amplitude(ev);
                if (!_pileUp) AddMeasured(_counts, amplitude, _consumed, ref _total, ref _overflow);
                else
                {
                    // Each absorbed pulse re-extends the resolving interval, exactly as ApplyPileUp.
                    if (_groupEnergy > 0 && ev.ArrivalTimeS - _lastArrival >= _resolvingTimeS)
                    {
                        AddMeasured(_counts, _groupEnergy, _groupStart, ref _total, ref _overflow);
                        _groupEnergy = 0;
                    }
                    if (_groupEnergy == 0) _groupStart = _consumed;
                    _groupEnergy += amplitude;
                    _lastArrival = ev.ArrivalTimeS;
                }
            }
        }
        catch { _acquisitionId = Guid.Empty; _consumed = int.MaxValue; throw; }
        var counts = (double[])_counts.Clone();
        long total = _total, overflow = _overflow;
        if (_pileUp && _groupEnergy > 0) AddMeasured(counts, _groupEnergy, _groupStart, ref total, ref overflow);
        const double width = AxisMaximumKeV / BinCount;
        var centres = Enumerable.Range(0, BinCount).Select(i => (i + 0.5) * width).ToArray();
        var bands = BuildBands(lines, settings.WindowFwhm, _model);
        var rows = bands.Select(b =>
        {
            long count = (long)counts.Where((_, i) => centres[i] >= b.LoKeV && centres[i] <= b.HiKeV).Sum();
            return b with { Counts = count, Share = total > 0 ? (double)count / total : 0 };
        }).ToArray();
        double union = counts.Where((_, i) => rows.Any(b => centres[i] >= b.LoKeV && centres[i] <= b.HiKeV)).Sum();
        watch.Stop();
        return new(centres, counts, Array.AsReadOnly(rows), total, overflow, total > 0 ? union / total : 0,
            _model.FwhmFraction(662), _resolvingTimeS, (settings.Detector ?? new DetectorSettings()).Chain.ToString(), watch.Elapsed)
        { BinEdgesKeV = Enumerable.Range(0, BinCount + 1).Select(i => i * width).ToArray() };
    }

    private void AddMeasured(double[] counts, double energy, int index, ref long total, ref long overflow)
    {
        // Index-addressed randomness keeps a pulse stable when an open pile-up group is redrawn,
        // regardless of snapshot partitioning, window changes or a later full reprocess.
        double measured = _measurement.MeasureAmplitude(energy, index);
        int bin = (int)(measured / AxisMaximumKeV * BinCount);
        total++;
        if (bin >= 0 && bin < BinCount) counts[bin]++;
        else overflow++;
    }

    /// <summary>Merge unresolved adjacent emissions by FWHM at their mean, never by window overlap.</summary>
    public static IReadOnlyList<SpectrumBand> BuildBands(IReadOnlyList<SpectrumLine> lines, double n,
        FrontEndModel model)
    {
        var ordered = lines.Distinct().OrderBy(l => l.EnergyKeV).ToArray();
        var groups = new List<List<SpectrumLine>>();
        foreach (var line in ordered)
        {
            if (groups.Count > 0)
            {
                double previous = groups[^1][^1].EnergyKeV, mean = (previous + line.EnergyKeV) / 2;
                if (line.EnergyKeV - previous < mean * model.FwhmFraction(mean))
                { groups[^1].Add(line); continue; }
            }
            groups.Add([line]);
        }
        return groups.Select(g => new SpectrumBand(Array.AsReadOnly(g.ToArray()),
            g.Min(l => l.EnergyKeV - n * l.EnergyKeV * model.FwhmFraction(l.EnergyKeV)),
            g.Max(l => l.EnergyKeV + n * l.EnergyKeV * model.FwhmFraction(l.EnergyKeV)), 0, 0)).ToArray();
    }
}
