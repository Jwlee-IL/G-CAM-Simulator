using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Detector;

namespace Gcam.Tests;

public sealed class ReadoutBsrTests
{
    private static SimulationConfig Configuration(double bsr)
    {
        var c = new SimulationConfig { Seed = 4711, Background = new() { BackgroundToSignalRatio = bsr } };
        c.Source.ActivityBq = 1e7;
        c.Detector.ReflectorGapMm = .1; c.Detector.EntranceAbsorberMm = .15; c.Detector.BackingScatterMm = 2;
        return c;
    }

    [Theory]
    [InlineData(.5)] [InlineData(1)]
    public void PairedSchedule_EqualsLegacyPretriggerTimes_AndSitesOwnThePixel(double bsr)
    {
        var config = Configuration(bsr);
        using var legacy = new ListModeSource(config);
        using var physical = new ListModeSource(config, true, true);
        var times = new List<double>(); var records = new List<RecordedTransportEvent>();
        while (legacy.ArrivalTimeS < 30)
            if (legacy.AdvanceUntil(30) is { } e) times.Add(e.ArrivalTimeS);
        while (physical.ArrivalTimeS < 30)
        {
            var e = physical.AdvanceUntil(30);
            if (e is null) { Assert.Null(physical.LastRecordedEvent); continue; }
            var r = Assert.IsType<RecordedTransportEvent>(physical.LastRecordedEvent);
            Assert.Equal(e, r.Event); records.Add(r);
        }
        // Identical source transport, adaptive rate estimates and schedule RNG; only conditional background
        // histories differ. This oracle never substitutes measured assigned counts for source intensity.
        Assert.Equal(times, records.Select(r => r.Event.ArrivalTimeS));
        Assert.Equal(legacy.HistoriesEmitted, physical.HistoriesEmitted);
        Assert.Equal(legacy.DetectedWeight, physical.DetectedWeight);
        var backgrounds = records.Where(r => r.Origin == TransportOrigin.Background).ToArray();
        Assert.NotEmpty(backgrounds);
        Assert.Contains(backgrounds, r => r.Sites.Count > 1);
        foreach (var r in backgrounds)
        {
            Assert.Equal(r.Event.PixelY * config.Detector.PixelsX + r.Event.PixelX,
                ReadoutDevice.DirectCrystal(r.Sites, config.Detector.PixelsX));
            // At most 32 transport steps per site, two reductions and final rounding: gamma_(34n+1).
            double k = 34 * r.Sites.Count + 1, u = Math.ScaleB(1.0, -53);
            double bound = k * u / (1 - k * u) * config.Background!.EnergyKeV;
            Assert.InRange(Math.Abs(r.Sites.Sum(s => s.EnergyKeV) - r.Event.DepositKeV), 0, bound);
        }
        var frozen = backgrounds[0].Sites.ToArray();
        while (physical.ArrivalTimeS < 31) physical.AdvanceUntil(31);
        Assert.Equal(frozen, backgrounds[0].Sites);
    }

    [Fact]
    public void ConditionalRetries_CancelDuringLowAcceptance_WithoutConsumingNextAttempt()
    {
        var c = Configuration(1);
        c.Background!.EnergyKeV = 1; // front steel proxy absorbs these inputs; source gammas still enter
        using var source = new ListModeSource(c, true, true);
        while (source.BackgroundAttempts < 100) source.AdvanceUntil(100);
        long histories = source.HistoriesEmitted, attempts = source.BackgroundAttempts;
        double observed = source.ArrivalTimeS;
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => source.AdvanceUntil(100, cancelled.Token));
        Assert.Equal(attempts, source.BackgroundAttempts); Assert.Equal(histories, source.HistoriesEmitted);
        Assert.Equal(observed, source.ArrivalTimeS);
        using var during = new CancellationTokenSource();
        // Cancel from a dedicated thread: a timed source's timer runs on the thread pool, which a parallel CPU-bound run starves.
        var canceller = new Thread(() => { Thread.Sleep(20); during.Cancel(); }) { IsBackground = true };
        var watch = Stopwatch.StartNew();
        canceller.Start();
        try
        {
            Assert.Throws<OperationCanceledException>((Action)(() =>
            {
                while (true) source.AdvanceUntil(100, during.Token);
            }));
        }
        finally { canceller.Join(); }
        Assert.True(source.BackgroundAttempts > attempts);
        Assert.Equal(histories, source.HistoriesEmitted); // source envelope remains pending during retries
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5)); // operational responsiveness deadline
        Assert.Null(source.LastRecordedEvent);
    }

    [Fact]
    public void ZeroSourceRate_ProducesNoBsrInputs()
    {
        var c = Configuration(1); c.Source.ActivityBq = 0; c.Ambient = new() { DoseRateMicroSvPerHour = 0 };
        using var source = new ListModeSource(c, true, true);
        Assert.Null(source.AdvanceUntil(10)); Assert.Equal(10, source.ArrivalTimeS);
        Assert.Equal(0, source.SourceRateCps); Assert.Equal(0, source.RateCps);
        Assert.Equal(0, source.BackgroundAttempts); Assert.Null(source.LastRecordedEvent);
    }
}
