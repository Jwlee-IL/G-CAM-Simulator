using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

/// <summary>Start / Stop / Continue on the real session: a paused and continued acquisition is the uninterrupted one,
/// event for event (pixel, true deposit, arrival time), because the session keeps the list-mode source, its RNG
/// streams and the one already-drawn look-ahead event; wall-clock pacing never enters the events.</summary>
public sealed class AcquisitionContinuationTests(ITestOutputHelper output)
{
    private const int Seed = 4711;
    private static readonly SceneSource[] Mixed =
    [
        new() { X = 0, Y = 0, DistanceMm = 1000, ActivityUCi = 500 },
        new() { Isotope = "Co-60", X = 20, Y = 0, DistanceMm = 1000, ActivityUCi = 300 },
    ];
    // Co-60 close to the camera: per-decay emission with true-coincidence summed events in the stream (TODO-14).
    private static readonly SceneSource[] Co60Near = [new() { Isotope = "Co-60", X = 3, Y = -2, DistanceMm = 100, ActivityUCi = 500 }];
    private static SceneSource[] Scene(string name) => name == "co60-near" ? Co60Near : Mixed;

    /// <summary>Reads one segment on the virtual clock: one 250 ms tick per snapshot; Stop after
    /// <paramref name="stopAfterTicks"/> ticks (null: run to the preset).</summary>
    private static async Task<List<AcquisitionSnapshot>> Segment(IAcquisitionSession session, ManualTimeProvider clock, int? stopAfterTicks)
    {
        var snapshots = new List<AcquisitionSnapshot>();
        int ticks = 0;
        await foreach (var snapshot in session.ReadSnapshotsAsync())
        {
            snapshots.Add(snapshot);
            if (snapshot.IsCompleted) continue;
            if (stopAfterTicks is { } n && ticks >= n) { session.Stop(); continue; }
            await clock.WaitForTimerAsync();
            clock.Advance(TimeSpan.FromMilliseconds(250));
            ticks++;
        }
        return snapshots;
    }

    private static (List<DetectedEvent> Events, long Coincident) Truth(SceneSource[] scene, double bsr, double liveTimeS)
    {
        var config = SimulationService.BuildConfig(scene, new OpticsSettings(), new DetectorSettings(), bsr);
        config.Seed = Seed;
        using var source = new ListModeSource(config);
        var events = new List<DetectedEvent>();
        while (true)
            if (source.Advance() is { } e)
            {
                if (e.ArrivalTimeS > liveTimeS) return (events, source.CoincidentHistories);
                events.Add(e);
            }
    }

    [Theory]
    [InlineData("mixed", 0.0)]
    [InlineData("mixed", 0.5)] // background: a second look-ahead lives inside the list-mode source
    [InlineData("co60-near", 0.0)] // one summed event per decay keeps the single look-ahead slot sufficient
    public async Task StopAndContinue_ReproducesTheUninterruptedEventStream(string sceneName, double bsr)
    {
        const double preset = 12;
        var scene = Scene(sceneName);
        var clock = new ManualTimeProvider();
        var service = new SimulationService(clock);
        await using var whole = service.Start(scene, new OpticsSettings(), preset, 4, new DetectorSettings(), bsr, Seed);
        var uninterrupted = (await Segment(whole, clock, null))[^1];
        Assert.True(uninterrupted.IsCompleted);

        await using var paused = service.Start(scene, new OpticsSettings(), preset, 4, new DetectorSettings(), bsr, Seed);
        var all = new List<AcquisitionSnapshot>();
        var stops = new List<(double Live, long Counts)>();
        var resumed = new List<AcquisitionSnapshot>();
        // Stops after 1, 3, 2 and 5 ticks at changing speeds; 100 s of wall time pass while stopped each time.
        var plan = new (double Speed, int? StopAfter)[] { (4, 1), (1.5, 3), (11, 2), (0.7, 5), (6, null) };
        for (int i = 0; i < plan.Length; i++)
        {
            if (i > 0) paused.Continue(preset, plan[i].Speed);
            var segment = await Segment(paused, clock, plan[i].StopAfter);
            if (i > 0) resumed.Add(segment[0]);
            all.AddRange(segment);
            if (plan[i].StopAfter is null) break;
            var stopped = segment[^1];
            Assert.False(stopped.IsCompleted);
            stops.Add((stopped.LiveTimeS, stopped.Counts));
            clock.Advance(TimeSpan.FromSeconds(100));
        }
        var final = all[^1];
        var (truth, coincident) = Truth(scene, bsr, preset);
        if (sceneName == "co60-near")
            Assert.Contains(truth, e => e.DepositKeV > 1332.5 + 1e-6); // a summed decay is in the compared stream

        Assert.True(final.IsCompleted);
        Assert.Equal(preset, final.LiveTimeS);
        Assert.Equal(Seed, final.Seed);
        Assert.Equal(truth, uninterrupted.Events);
        Assert.Equal(uninterrupted.Events, final.Events); // event by event: pixel, deposit, arrival time
        Assert.True(final.Events.Zip(final.Events.Skip(1)).All(p => p.Second.ArrivalTimeS > p.First.ArrivalTimeS));
        Assert.All(all.Zip(all.Skip(1)), p =>
        {
            Assert.True(p.Second.LiveTimeS >= p.First.LiveTimeS);
            Assert.True(p.Second.Counts >= p.First.Counts);
        });
        Assert.All(all, s => Assert.All(s.Events, e => Assert.True(e.ArrivalTimeS <= s.LiveTimeS)));
        // Each continued segment starts by republishing the stopped state: wall time while stopped added no live time.
        Assert.Equal(stops.Count, resumed.Count);
        foreach (var ((live, counts), first) in stops.Zip(resumed))
        {
            Assert.Equal(live, first.LiveTimeS);
            Assert.Equal(counts, first.Counts);
        }
        Assert.True(stops.Select(s => s.Live).Distinct().Count() == stops.Count, "every stop at a new live time");
        output.WriteLine($"{sceneName}, BSR {bsr}: {coincident} coincident decays; {final.Counts} events in {preset} s; {stops.Count} stops at live " +
            string.Join(", ", stops.Select(s => $"{s.Live:0.###} s/{s.Counts}")) + "; identical to the uninterrupted run and the direct source stream");
    }

    [Fact]
    public async Task Completed_RaisedPreset_ContinuesExactly()
    {
        var clock = new ManualTimeProvider();
        var service = new SimulationService(clock);
        await using var whole = service.Start(Mixed, new OpticsSettings(), 6, 3, seed: Seed);
        var reference = (await Segment(whole, clock, null))[^1];
        await using var session = service.Start(Mixed, new OpticsSettings(), 3, 3, seed: Seed);
        var first = (await Segment(session, clock, null))[^1];
        Assert.True(first.IsCompleted);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Continue(3, 3)); // nothing left to acquire
        session.Continue(6, 3);
        var last = (await Segment(session, clock, null))[^1];
        Assert.True(last.IsCompleted);
        Assert.Equal(reference.Events, last.Events);
        Assert.Equal(first.Events, last.Events.Take(first.Events.Count));
    }

    [Fact]
    public async Task Continue_IsRejectedWhileRunning()
    {
        var clock = new ManualTimeProvider();
        await using var session = new SimulationService(clock).Start(Mixed, new OpticsSettings(), 60, 1, seed: Seed);
        await using var reader = session.ReadSnapshotsAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        Assert.Throws<InvalidOperationException>(() => session.Continue(120, 1));
        session.Stop();
        while (await reader.MoveNextAsync()) { }
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Continue(double.NaN, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.Continue(120, 0));
    }

    [Fact]
    public async Task Seed_FixedReproduces_OtherSeedIsAnIndependentAcquisition()
    {
        async Task<AcquisitionSnapshot> Run(int seed)
        {
            var clock = new ManualTimeProvider();
            await using var session = new SimulationService(clock).Start(Mixed, new OpticsSettings(), 2, 4, seed: seed);
            return (await Segment(session, clock, null))[^1];
        }
        var a = await Run(Seed);
        var again = await Run(Seed);
        var other = await Run(Seed + 1);
        Assert.Equal(Seed, a.Seed);
        Assert.Equal(Seed + 1, other.Seed);
        Assert.Equal(a.Events, again.Events);
        Assert.NotEqual(a.Events.Take(20), other.Events.Take(20));
    }
}
