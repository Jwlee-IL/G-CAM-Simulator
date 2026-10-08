using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Detector;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Transport and decoding run on one worker per segment (Start, then each Continue). Everything a continuation
/// needs — the list-mode source with its RNG streams, the one already-drawn look-ahead event, the events, flood and
/// live-time clock — lives in fields, so a stopped and continued acquisition is event-for-event the uninterrupted one.
/// Backpressure bounds unpublished snapshots.</summary>
internal sealed class AcquisitionSession : IAcquisitionSession
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan TransportBudget = TimeSpan.FromMilliseconds(200);
    private readonly SimulationConfig _config;
    private readonly DetectorSettings _detector;
    private readonly TimeProvider _clock;
    private readonly OpticsSettings _optics;
    private readonly DetectorImage _flood;
    private readonly List<DetectedEvent> _events = [];
    private ListModeSource? _source;
    private PhysicalListModeSource? _physicalSource;
    private readonly (ReadoutPreparation View, ReadoutDevice Device, ReadoutCalibration Calibration)? _prepared;
    private readonly ImmutableRecordStore<MeasuredReadoutRecord> _measured = new();
    private readonly ImmutableRecordStore<RealisedReadoutHit> _realised = new();
    private readonly ImmutableRecordStore<ReadoutTruthSample> _truth = new();
    private int _assigned, _unknown;
    private readonly DetectorImage _rawDensity = new(256, 256);
    private IDecoder? _decoder;
    private bool _decoderBuilt;
    // Look-ahead: drawn, but later than the acquired live time. Dropping it at Stop would lose a real count.
    private DetectedEvent? _pending;
    private double _live;
    private volatile bool _failed;
    private CancellationTokenSource _stop = new();
    private Channel<AcquisitionSnapshot> _snapshots = NewChannel();
    private Task _worker;

    public AcquisitionSession(SimulationConfig config, DetectorSettings detector, double preset, double speed, TimeProvider clock,
        (ReadoutPreparation View, ReadoutDevice Device, ReadoutCalibration Calibration)? prepared = null)
    {
        _config = config;
        _prepared = prepared;
        if (prepared is { } artifact && artifact.View.Key != ReadoutPreparationService.Key(
            new OpticsSettings { DetectorPixels = config.Detector.PixelsX, PixelPitchMm = config.Detector.PixelPitchMm }, detector))
            throw new InvalidOperationException("Prepared readout does not match the acquired detector inputs.");
        _detector = detector;
        _clock = clock;
        _flood = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
        _optics = new OpticsSettings
        {
            MuraRank = config.Mask.Rank, CellPitchMm = config.Mask.CellPitchMm,
            MaskDetectorDistanceMm = config.Geometry.MaskDetectorDistanceMm,
            DetectorPixels = config.Detector.PixelsX, PixelPitchMm = config.Detector.PixelPitchMm,
            FocalDistanceMm = config.Geometry.MaskDetectorDistanceMm + config.Geometry.SourceMaskDistanceMm
        };
        _worker = StartSegment(preset, speed);
    }

    private static Channel<AcquisitionSnapshot> NewChannel() => Channel.CreateBounded<AcquisitionSnapshot>(
        new BoundedChannelOptions(2) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.DropOldest });

    private Task StartSegment(double preset, double speed)
    {
        var stop = _stop;
        var channel = _snapshots;
        return Task.Run(() => ProduceAsync(preset, speed, stop, channel));
    }

    public void Stop() => _stop.Cancel();

    public void Continue(double presetLiveTimeS, double speed)
    {
        if (!(presetLiveTimeS > 0) || !double.IsFinite(presetLiveTimeS)) throw new ArgumentOutOfRangeException(nameof(presetLiveTimeS));
        if (!(speed > 0) || !double.IsFinite(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
        if (!_worker.IsCompleted) throw new InvalidOperationException("A segment is still running; stop it first.");
        if (_failed) throw new InvalidOperationException("A failed acquisition cannot continue.");
        if (!(presetLiveTimeS > _live))
            throw new ArgumentOutOfRangeException(nameof(presetLiveTimeS), "The preset must exceed the acquired live time.");
        _stop.Dispose();
        _stop = new CancellationTokenSource();
        _snapshots = NewChannel();
        _worker = StartSegment(presetLiveTimeS, speed);
    }

    public async IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = _snapshots;
        var worker = _worker;
        using var registration = cancellationToken.Register(Stop);
        try
        {
            // Cancellation requests Stop; drain the terminal snapshot instead of throwing away acquired data.
            await foreach (var snapshot in channel.Reader.ReadAllAsync()) yield return snapshot;
        }
        finally
        {
            // The next segment may start only after this worker has left the shared state.
            await worker.ConfigureAwait(false);
        }
    }

    private async Task ProduceAsync(double preset, double speed, CancellationTokenSource stop, Channel<AcquisitionSnapshot> channel)
    {
        try
        {
            if (_prepared is { } artifact) _physicalSource ??= new PhysicalListModeSource(_config, artifact.Device, artifact.Calibration, _config.Seed ?? 0);
            else _source ??= new ListModeSource(_config);
            if (!_decoderBuilt)
            {
                _decoder = new DefaultSimulationFactory().CreateDecoder(_config);
                _decoderBuilt = true;
            }
            var source = _source;
            long start = _clock.GetTimestamp(), previous = start;
            long previousPublished = start;
            bool firstRefresh = true;
            double previousLive = _live;
            while (true)
            {
                double wall = _clock.GetElapsedTime(start).TotalSeconds;
                long tickStart = _clock.GetTimestamp();
                // After a compute-limited tick, keep aiming at the requested speed from the current live time.
                double dt = _clock.GetElapsedTime(previous).TotalSeconds;
                double target = Math.Min(preset, _live + speed * dt);
                // A segment's first refresh republishes the retained state: live time never advances while stopped.
                if (firstRefresh || stop.IsCancellationRequested) target = _live;
                var compute = Stopwatch.StartNew();
                try
                {
                    while (!stop.IsCancellationRequested && compute.Elapsed < TransportBudget)
                    {
                        if (_physicalSource is { } physical)
                        {
                            if (physical.ObservedTimeS >= target) break;
                            foreach (var measured in physical.AdvanceUntil(target, stop.Token))
                            {
                                if (double.IsFinite(measured.RawX) && double.IsFinite(measured.RawY) && measured.RawX >= -1 && measured.RawX < 1 && measured.RawY >= -1 && measured.RawY < 1)
                                    _rawDensity.Add((int)((measured.RawX + 1) * 128), (int)((measured.RawY + 1) * 128), 1);
                                var e = measured.Conversion;
                                static ReadoutCharges Charges(IReadOnlyList<double> c) => new(c[0], c[1], c[2], c[3]);
                                _measured.Add(new(_measured.Count, e.HoldTimeNs * 1e-9, e.TriggerTimeNs * 1e-9,
                                    Charges(e.Codes), Charges(e.AnalogAtHold), measured.RawX, measured.RawY, measured.Crystal,
                                    measured.EnergyKeV, e.DominantHit, e.ContributingHits, e.DominantShare));
                                if (measured.Crystal < 0) _unknown++;
                                else { _assigned++; _flood.Add(measured.Crystal % 12, measured.Crystal / 12, 1); }
                            }
                            foreach (var h in physical.LastHits)
                            {
                                var c = h.Channels;
                                _realised.Add(new(h.TimeNs * 1e-9, new(c[0], c[1], c[2], c[3])));
                            }
                            while (_truth.Count < physical.TruthSamples.Count)
                            {
                                var sample = physical.TruthSamples[_truth.Count];
                                _truth.Add(new(sample.HitIndex, sample.Sites));
                            }
                            continue;
                        }
                        if (_pending is { } next)
                        {
                            if (next.ArrivalTimeS > target) break;
                            _events.Add(next);
                            _flood.Add(next.PixelX, next.PixelY, 1);
                            _pending = null;
                        }
                        if (source!.ArrivalTimeS >= target) break;
                        // Advance checks the token before any random draw, so a cancelled call consumes nothing.
                        _pending = _config.Ambient is not null ? source.AdvanceUntil(target, stop.Token) : source.Advance(stop.Token);
                    }
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                // A look-ahead event proves that no counts were skipped in the intervening empty live time.
                bool limited = _physicalSource is { } physicalPrefix ? physicalPrefix.ObservedTimeS < target
                    : source!.ArrivalTimeS < target || _pending is { } waiting && waiting.ArrivalTimeS <= target;
                // Only the consumed prefix belongs to the acquisition. A pending event is look-ahead, not a count.
                _live = _physicalSource is { } prefix ? prefix.ObservedTimeS
                    : limited ? Math.Max(_live, _events.Count == 0 ? 0 : _events[^1].ArrivalTimeS) : target;
                bool completed = _live >= preset;
                var decodeWatch = Stopwatch.StartNew();
                int counts = _prepared is null ? _events.Count : _assigned;
                var decoded = counts > 0 ? _decoder?.Decode(_flood) : null;
                decodeWatch.Stop();
                var imaging = new ImagingResult(_flood.ReadOnlyCopy(), -(_flood.Width - 1) * _config.Detector.PixelPitchMm / 2,
                    _config.Detector.PixelPitchMm, decoded?.Reconstruction.ReadOnlyCopy(), decoded?.ReconOriginMm ?? 0,
                    decoded?.ReconStepMm ?? 0, decoded?.Estimate, counts, TimeSpan.FromSeconds(wall));
                double reportInterval = _clock.GetElapsedTime(previousPublished).TotalSeconds;
                double actual = reportInterval > 0 ? (_live - previousLive) / reportInterval : 0;
                await channel.Writer.WriteAsync(new AcquisitionSnapshot(_live, counts, _physicalSource?.InputRateCps ?? source!.RateCps,
                    actual, limited, imaging, Array.AsReadOnly(_events.ToArray()), decodeWatch.Elapsed, completed)
                    { Detector = _detector, Optics = _optics, Seed = _config.Seed,
                        Readout = _prepared is { } prepared ? new(prepared.View, _measured.Publish(), _realised.Publish(), _unknown, _physicalSource!.HasPendingHold) { LiveDensity = _rawDensity.ReadOnlyCopy(), TruthSamples = _truth.Publish() } : null,
                        AmbientRateCps = source?.AmbientRateCps ?? 0,
                        SourceRateCps = _config.Ambient is null ? 0 : source!.SourceRateCps,
                        AmbientMaximumEnergyKeV = _config.Ambient is { } ambient
                            ? ambient.Spectrum.Lines.Select(l => l.EnergyKeV).Concat(ambient.Spectrum.Continuum.Select(b => b.HighKeV)).Max()
                            : null });
                if (completed || stop.IsCancellationRequested) break;
                previous = tickStart;
                previousPublished = _clock.GetTimestamp();
                firstRefresh = false;
                previousLive = _live;
                // Include transport/decode in the 250 ms refresh interval.
                var wait = RefreshInterval - _clock.GetElapsedTime(start) + TimeSpan.FromSeconds(wall);
                if (wait > TimeSpan.Zero)
                {
                    try { await Task.Delay(wait, _clock, stop.Token); }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                }
            }
            channel.Writer.TryComplete();
        }
        catch (Exception ex)
        {
            // The source may be mid-history: published data stay valid, but the acquisition cannot continue.
            _failed = true;
            channel.Writer.TryComplete(ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        await _worker;
        _stop.Dispose();
        _source?.Dispose();
        _physicalSource?.Dispose();
    }
}
