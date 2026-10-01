using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Transport and decoding run on one worker. Backpressure bounds unpublished snapshots.</summary>
internal sealed class AcquisitionSession : IAcquisitionSession
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan TransportBudget = TimeSpan.FromMilliseconds(200);
    private readonly CancellationTokenSource _stop = new();
    private readonly Channel<AcquisitionSnapshot> _snapshots = Channel.CreateBounded<AcquisitionSnapshot>(
        new BoundedChannelOptions(2) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Task _worker;

    public AcquisitionSession(SimulationConfig config, double preset, double speed, TimeProvider clock)
        => _worker = Task.Run(() => ProduceAsync(config, preset, speed, clock));

    public void Stop() => _stop.Cancel();

    public async IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var registration = cancellationToken.Register(Stop);
        // Cancellation requests Stop; drain the terminal snapshot instead of throwing away acquired data.
        await foreach (var snapshot in _snapshots.Reader.ReadAllAsync()) yield return snapshot;
    }

    private async Task ProduceAsync(SimulationConfig config, double preset, double speed, TimeProvider clock)
    {
        try
        {
            using var source = new ListModeSource(config);
            var decoder = new DefaultSimulationFactory().CreateDecoder(config);
            var flood = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
            var events = new List<DetectedEvent>();
            DetectedEvent? pending = null;
            double live = 0;
            long start = clock.GetTimestamp(), previous = start;
            long previousPublished = start;
            bool firstRefresh = true;
            double previousLive = 0;
            while (true)
            {
                double wall = clock.GetElapsedTime(start).TotalSeconds;
                long tickStart = clock.GetTimestamp();
                // After a compute-limited tick, keep aiming at the requested speed from the current live time.
                double dt = clock.GetElapsedTime(previous).TotalSeconds;
                double target = Math.Min(preset, live + speed * dt);
                if (firstRefresh) target = 0;
                if (_stop.IsCancellationRequested) target = live;
                var compute = Stopwatch.StartNew();
                try
                {
                    while (!_stop.IsCancellationRequested && compute.Elapsed < TransportBudget)
                    {
                        if (pending is { } next)
                        {
                            if (next.ArrivalTimeS > target) break;
                            events.Add(next);
                            flood.Add(next.PixelX, next.PixelY, 1);
                            pending = null;
                        }
                        if (source.ArrivalTimeS >= target) break;
                        pending = source.Advance(_stop.Token);
                    }
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
                // A look-ahead event proves that no counts were skipped in the intervening empty live time.
                bool limited = source.ArrivalTimeS < target || pending is { } waiting && waiting.ArrivalTimeS <= target;
                // Only the consumed prefix belongs to the acquisition. A pending event is look-ahead, not a count.
                live = limited ? Math.Max(live, events.Count == 0 ? 0 : events[^1].ArrivalTimeS) : target;
                bool completed = live >= preset;
                var decodeWatch = Stopwatch.StartNew();
                var decoded = events.Count > 0 ? decoder?.Decode(flood) : null;
                decodeWatch.Stop();
                var imaging = new ImagingResult(flood.ReadOnlyCopy(), -(flood.Width - 1) * config.Detector.PixelPitchMm / 2,
                    config.Detector.PixelPitchMm, decoded?.Reconstruction.ReadOnlyCopy(), decoded?.ReconOriginMm ?? 0,
                    decoded?.ReconStepMm ?? 0, decoded?.Estimate, events.Count, TimeSpan.FromSeconds(wall));
                double reportInterval = clock.GetElapsedTime(previousPublished).TotalSeconds;
                double actual = reportInterval > 0 ? (live - previousLive) / reportInterval : 0;
                await _snapshots.Writer.WriteAsync(new AcquisitionSnapshot(live, events.Count, source.RateCps,
                    actual, limited, imaging, Array.AsReadOnly(events.ToArray()), decodeWatch.Elapsed, completed));
                if (completed || _stop.IsCancellationRequested) break;
                previous = tickStart;
                previousPublished = clock.GetTimestamp();
                firstRefresh = false;
                previousLive = live;
                // Include transport/decode in the 250 ms refresh interval.
                var wait = RefreshInterval - clock.GetElapsedTime(start) + TimeSpan.FromSeconds(wall);
                if (wait > TimeSpan.Zero)
                {
                    try { await Task.Delay(wait, clock, _stop.Token); }
                    catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
                }
            }
            _snapshots.Writer.TryComplete();
        }
        catch (Exception ex) { _snapshots.Writer.TryComplete(ex); }
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        await _worker;
        _stop.Dispose();
    }
}
