using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>Explicit signal-only physical readout entry point. The ordinary direct factories remain guarded.</summary>
public sealed class PhysicalListModeSource : IDisposable
{
    private readonly ListModeSource _source;
    private readonly ReadoutDevice _device;
    private readonly ReadoutCalibration _calibration;
    private readonly ReadoutPulseProcessor.Stream _stream;
    private readonly IRandom _response;
    private DetectedEvent? _pending;
    private InteractionSite[] _pendingSites = [];
    private readonly List<PhysicalTruthSample> _truth = [];
    private readonly IReadOnlyList<PhysicalTruthSample> _readOnlyTruth;
    /// <summary>Only this advance's realised input. The consumer retains compact scalars for historical scope.</summary>
    public IReadOnlyList<ReadoutHit> LastHits { get; private set; } = [];
    public int HitsObserved { get; private set; }
    public IReadOnlyList<PhysicalTruthSample> TruthSamples => _readOnlyTruth;
    public const int MaximumTruthSamples = 256;
    public double ObservedTimeS { get; private set; }
    public double InputRateCps => _source.RateCps;
    public bool HasPendingHold => _stream.HasPendingHold;

    public PhysicalListModeSource(SimulationConfig configuration, ReadoutDevice device,
        ReadoutCalibration calibration, int seed)
    {
        if (configuration.Detector.Readout is not { Mode: not ReadoutMode.DirectCrystal } readout || !calibration.Succeeded)
            throw new ArgumentException("Physical acquisition requires a physical configuration and successful calibration.");
        if (configuration.Ambient is not null || (configuration.Background?.BackgroundToSignalRatio ?? 0) > 0 ||
            (configuration.Background?.DarkCountRateKcps ?? 0) > 0 || configuration.Detector.OpticalCrosstalkFraction != 0)
            throw new NotSupportedException("Physical interaction recording supports signal only; legacy optical crosstalk is replaced by readout optics.");
        var transport = configuration.Clone();
        _readOnlyTruth = _truth.AsReadOnly();
        transport.Detector.Readout = null;
        _source = new ListModeSource(transport, recordInteractions: true);
        // Each session owns response scratch buffers; cached optics/calibration can safely be shared.
        _device = new ReadoutDevice(device.Optics, readout); _calibration = calibration;
        _response = DefaultRandom.FromKey(DefaultRandom.Key(seed, 40001));
        _stream = new ReadoutPulseProcessor(_device, readout.Pulse, readout.Trigger)
            .CreateStream(DefaultRandom.FromKey(DefaultRandom.Key(seed, 40002)));
    }

    /// <summary>One bounded transport history towards a horizon; keep the already-drawn sites with look-ahead.</summary>
    public IReadOnlyList<PhysicalMeasuredEvent> AdvanceUntil(double horizonS, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        LastHits = [];
        if (!double.IsFinite(horizonS) || horizonS < ObservedTimeS) throw new ArgumentOutOfRangeException(nameof(horizonS));
        if (_pending is null)
        {
            _pending = _source.Advance(token);
            if (_pending is null) return [];
            _pendingSites = _source.LastInteractions.ToArray();
        }
        double observed = Math.Min(horizonS, _pending.Value.ArrivalTimeS);
        if (_pending.Value.ArrivalTimeS <= horizonS)
        {
            var channels = new double[_device.Channels];
            double sum = _device.Respond(_pendingSites, _response, channels);
            var hit = new ReadoutHit(observed * 1e9, channels, sum);
            if (_truth.Count < MaximumTruthSamples) _truth.Add(new(HitsObserved, Array.AsReadOnly(_pendingSites)));
            HitsObserved++; LastHits = Array.AsReadOnly(new[] { hit }); _stream.Append(hit);
            _pending = null; _pendingSites = [];
        }
        // Stop cancellation occurs between bounded calls, never between a consumed hit and publishing its conversion.
        var events = _stream.AdvanceTo(observed * 1e9);
        ObservedTimeS = observed;
        return events.Select(e =>
        {
            var codes = e.Codes.ToArray();
            int crystal = _device.Position(codes, out double x, out double y) ? _calibration.Lut.Lookup(x, y) : -1;
            double energy = _calibration.Energy(crystal, _device.Sum(codes));
            if (!double.IsFinite(energy) || energy < 0) crystal = -1;
            return new PhysicalMeasuredEvent(e, x, y, crystal, energy);
        }).ToArray();
    }

    public void Dispose() => _source.Dispose();
}
