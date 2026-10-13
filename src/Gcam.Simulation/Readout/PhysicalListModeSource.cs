using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using System.Diagnostics;

namespace Gcam.Simulation;

/// <summary>Explicit physical readout of source and ambient inputs. Ordinary direct factories remain guarded.</summary>
public sealed class PhysicalListModeSource : IDisposable
{
    private readonly ListModeSource _source;
    private readonly ReadoutDevice _device;
    private readonly ReadoutCalibration[] _calibrations;
    private readonly ReadoutPulseProcessor.Stream _stream;
    private readonly IRandom _response;
    private RecordedTransportEvent? _pending;
    private readonly bool _hasAmbient;
    private readonly Action<ReadoutAdvanceTiming>? _timing;
    public TransportOrigin? LastInputOrigin { get; private set; }
    private readonly List<PhysicalTruthSample> _truth = [];
    private readonly IReadOnlyList<PhysicalTruthSample> _readOnlyTruth;
    /// <summary>Only this advance's realised input. The consumer retains compact scalars for historical scope.</summary>
    public IReadOnlyList<ReadoutHit> LastHits { get; private set; } = [];
    public int HitsObserved { get; private set; }
    public IReadOnlyList<PhysicalTruthSample> TruthSamples => _readOnlyTruth;
    public const int MaximumTruthSamples = 256;
    public double ObservedTimeS { get; private set; }
    public double InputRateCps => _source.RateCps;
    public double SourceRateCps => _source.SourceRateCps;
    public double AmbientRateCps => _source.AmbientRateCps;
    public bool HasPendingHold => _stream.HasPendingHold;

    public PhysicalListModeSource(SimulationConfig configuration, ReadoutDevice device,
        ReadoutCalibration calibration, int seed)
        : this(configuration, device, [calibration], seed) { }

    /// <summary>TODO-41 (SD-A1…A3): one transport / readout stream projected through several line calibrations of the same
    /// prepared device. The held ADC facts are produced once; each calibration derives its own crystal and energy from
    /// them — no event is ever dispatched to a calibration by its generating line or true energy. The first calibration
    /// is the reference whose assignment is <see cref="PhysicalMeasuredEvent.Crystal"/> / <see cref="PhysicalMeasuredEvent.EnergyKeV"/>.</summary>
    public PhysicalListModeSource(SimulationConfig configuration, ReadoutDevice device,
        IReadOnlyList<ReadoutCalibration> calibrations, int seed, Action<ReadoutAdvanceTiming>? timing = null)
    {
        ArgumentNullException.ThrowIfNull(calibrations);
        _timing = timing;
        if (configuration.Detector.Readout is not { Mode: not ReadoutMode.DirectCrystal } readout || calibrations.Count == 0
            || calibrations.Any(c => !c.Succeeded || c.Lut.CountX != device.Crystals.CountX || c.Lut.CountY != device.Crystals.CountY))
            throw new ArgumentException("Physical acquisition requires a physical configuration and successful calibrations of this module.");
        if ((configuration.Background?.BackgroundToSignalRatio ?? 0) > 0)
            throw new NotSupportedException("Physical interaction recording requires BSR = 0 in this stage.");
        if ((configuration.Background?.DarkCountRateKcps ?? 0) > 0)
            throw new NotSupportedException("Physical readout does not support electronic dark arrivals: gamma interaction sites are unavailable.");
        if (configuration.Detector.OpticalCrosstalkFraction != 0)
            throw new NotSupportedException("Physical readout requires legacy optical crosstalk = 0: light sharing is provided by the readout optics.");
        var transport = configuration.Clone();
        _readOnlyTruth = _truth.AsReadOnly();
        transport.Detector.Readout = null;
        _hasAmbient = transport.Ambient is not null;
        _source = new ListModeSource(transport, recordInteractions: true, physicalModule: true);
        // Each session owns response scratch buffers; cached optics/calibration can safely be shared.
        _device = new ReadoutDevice(device, readout); _calibrations = calibrations.ToArray();
        _response = DefaultRandom.FromKey(DefaultRandom.Key(seed, 40001));
        _stream = new ReadoutPulseProcessor(_device, readout.Pulse, readout.Trigger)
            .CreateStream(DefaultRandom.FromKey(DefaultRandom.Key(seed, 40002)));
    }

    /// <summary>One bounded transport history towards a horizon; keep the already-drawn sites with look-ahead.</summary>
    public IReadOnlyList<PhysicalMeasuredEvent> AdvanceUntil(double horizonS, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        LastHits = [];
        LastInputOrigin = null;
        if (!double.IsFinite(horizonS) || horizonS < ObservedTimeS) throw new ArgumentOutOfRangeException(nameof(horizonS));
        long started = _timing is null ? 0 : Stopwatch.GetTimestamp();
        double observed;
        if (_hasAmbient)
        {
            _source.AdvanceUntil(horizonS, token);
            _pending = _source.LastRecordedEvent;
            observed = _source.ArrivalTimeS;
        }
        else
        {
            if (_pending is null)
            {
                _source.Advance(token);
                _pending = _source.LastRecordedEvent;
                if (_pending is null)
                {
                    _timing?.Invoke(new(Stopwatch.GetElapsedTime(started), TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero));
                    return [];
                }
            }
            observed = Math.Min(horizonS, _pending.Event.ArrivalTimeS);
        }
        var transportTime = _timing is null ? TimeSpan.Zero : Stopwatch.GetElapsedTime(started);
        long responseStart = _timing is null ? 0 : Stopwatch.GetTimestamp();
        if (_pending is { } input && input.Event.ArrivalTimeS <= horizonS)
        {
            var channels = new double[_device.Channels];
            double sum = _device.Respond(input.Sites, _response, channels);
            var hit = new ReadoutHit(observed * 1e9, channels, sum);
            if (_truth.Count < MaximumTruthSamples) _truth.Add(new(HitsObserved, input.Sites));
            HitsObserved++; LastHits = Array.AsReadOnly(new[] { hit }); _stream.Append(hit);
            LastInputOrigin = input.Origin;
            _pending = null;
        }
        // Stop cancellation occurs between bounded calls, never between a consumed hit and publishing its conversion.
        var responseTime = _timing is null ? TimeSpan.Zero : Stopwatch.GetElapsedTime(responseStart);
        long pulseStart = _timing is null ? 0 : Stopwatch.GetTimestamp();
        var events = _stream.AdvanceTo(observed * 1e9);
        var pulseTime = _timing is null ? TimeSpan.Zero : Stopwatch.GetElapsedTime(pulseStart);
        long projectionStart = _timing is null ? 0 : Stopwatch.GetTimestamp();
        ObservedTimeS = observed;
        var projected = events.Select(e =>
        {
            var codes = e.Codes.ToArray();
            bool positioned = _device.Position(codes, out double x, out double y);
            double sum = _device.Sum(codes);
            var assignments = new PhysicalAssignment[_calibrations.Length];
            for (int i = 0; i < assignments.Length; i++)
            {
                int crystal = positioned ? _calibrations[i].Lut.Lookup(x, y) : -1;
                double energy = _calibrations[i].Energy(crystal, sum);
                if (!double.IsFinite(energy) || energy < 0) crystal = -1;
                assignments[i] = new(crystal, energy);
            }
            return new PhysicalMeasuredEvent(e, x, y, assignments[0].Crystal, assignments[0].EnergyKeV)
            { Assignments = Array.AsReadOnly(assignments) };
        }).ToArray();
        _timing?.Invoke(new(transportTime, responseTime, pulseTime, Stopwatch.GetElapsedTime(projectionStart)));
        return projected;
    }

    public void Dispose() => _source.Dispose();
}
