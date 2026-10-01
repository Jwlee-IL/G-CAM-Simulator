using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

public sealed class AcquisitionServiceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ShortAcquisition_LocalizesAfterCountThreshold_SnapshotsAreImmutable()
    {
        var scene = new[] { new SceneSource { X = 15, Y = 8, DistanceMm = 1000, ActivityUCi = 500 } };
        var optics = new OpticsSettings();
        await using var session = new SimulationService().Start(scene, optics, 5, 20);
        var snapshots = new List<AcquisitionSnapshot>();
        await foreach (var snapshot in session.ReadSnapshotsAsync()) snapshots.Add(snapshot);
        var last = snapshots[^1];
        Assert.True(last.IsCompleted);
        Assert.Equal(5, last.LiveTimeS);
        Assert.True(last.Counts >= 50, $"Requires ≥50 detected counts, got {last.Counts}");
        Assert.True(snapshots.Zip(snapshots.Skip(1)).All(pair => pair.First.Counts <= pair.Second.Counts));
        Assert.Equal(last.Counts, last.Events.Count);
        Assert.Equal(last.Counts, last.Imaging.Flood.Raw.ToArray().Sum());
        Assert.All(last.Events, e => Assert.True(e.ArrivalTimeS <= last.LiveTimeS));
        Assert.Equal(last.Events.Count, last.Events.Distinct().Count());
        double error = Math.Sqrt(Math.Pow(last.Imaging.Estimate!.Position.X - 15, 2) + Math.Pow(last.Imaging.Estimate.Position.Y - 8, 2));
        double tolerance = optics.CellPitchMm * optics.FocalDistanceMm / optics.MaskDetectorDistanceMm;
        Assert.True(error < tolerance, $"Localization error {error:F3} mm must be below one resolution element {tolerance:F3} mm");
        output.WriteLine($"5 s Cs-137 acquisition: {last.Counts} counts (threshold ≥50), localization error={error:F3} mm < {tolerance:F3} mm; refresh decode={last.DecodeTime.TotalMilliseconds:F3} ms");
        Assert.Throws<InvalidOperationException>(() => last.Imaging.Flood.Add(0, 0, 1));
        Assert.Throws<NotSupportedException>(() => ((IList<Gcam.Core.DetectedEvent>)last.Events).Add(default));
        Assert.Equal(snapshots[0].Counts, snapshots[0].Imaging.Flood.Raw.ToArray().Sum());
    }

    [Fact]
    public async Task Stop_KeepsConsumedPrefix_AtExtremeMcLimitedSpeed()
    {
        var clock = new ManualTimeProvider();
        await using var session = new SimulationService(clock).Start(
            [new SceneSource { DistanceMm = 1000, ActivityUCi = 1e9 }], new OpticsSettings(), 60, 1e12);
        var snapshots = new List<AcquisitionSnapshot>();
        await using var reader = session.ReadSnapshotsAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        snapshots.Add(reader.Current);
        await clock.WaitForTimerAsync();
        clock.Advance(TimeSpan.FromMilliseconds(250));
        Assert.True(await reader.MoveNextAsync());
        snapshots.Add(reader.Current);
        await clock.WaitForTimerAsync();
        session.Stop();
        while (await reader.MoveNextAsync()) snapshots.Add(reader.Current);
        Assert.Contains(snapshots, s => s.IsMcLimited);
        Assert.False(snapshots[^1].IsCompleted);
        Assert.Equal(snapshots[^2].LiveTimeS, snapshots[^1].LiveTimeS);
        Assert.Equal(snapshots[^2].Counts, snapshots[^1].Counts);
        Assert.All(snapshots, s => Assert.All(s.Events, e => Assert.True(e.ArrivalTimeS <= s.LiveTimeS)));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(60, 0)]
    [InlineData(double.NaN, 10)]
    [InlineData(60, double.PositiveInfinity)]
    public void InvalidInputs_FailBeforeStarting(double liveTime, double speed)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationService().Start(
            [new SceneSource()], new OpticsSettings(), liveTime, speed));

    [Fact]
    public async Task InjectedClock_AdvancesLiveTimeAndPresetWithoutWallDelay()
    {
        var clock = new ManualTimeProvider();
        await using var session = new SimulationService(clock).Start(
            [new SceneSource { ActivityUCi = 500 }], new OpticsSettings(), 5, 10);
        await using var reader = session.ReadSnapshotsAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(0, reader.Current.LiveTimeS);
        await clock.WaitForTimerAsync();
        clock.Advance(TimeSpan.FromMilliseconds(250));
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(2.5, reader.Current.LiveTimeS);
        await clock.WaitForTimerAsync();
        clock.Advance(TimeSpan.FromMilliseconds(250));
        Assert.True(await reader.MoveNextAsync());
        Assert.Equal(5, reader.Current.LiveTimeS);
        Assert.True(reader.Current.IsCompleted);
        Assert.False(await reader.MoveNextAsync());
    }
}
