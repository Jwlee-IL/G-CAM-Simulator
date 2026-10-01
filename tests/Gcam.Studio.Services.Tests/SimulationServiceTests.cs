using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Studio.Services;

namespace Gcam.Studio.Services.Tests;

/// <summary>The only Studio layer that touches the engine: it must return exactly what the engine computes for the
/// scene, describe the flood in the decoder's own pixel convention, fail fast on bad arguments, and honour progress
/// and cancellation.</summary>
public class SimulationServiceTests
{
    private static readonly OpticsSettings Optics = new();
    private static IReadOnlyList<SceneSource> OneSource(double x = 20, double y = -10)
        => [new SceneSource { Isotope = "Cs-137", X = x, Y = y, DistanceMm = 1000 }];

    [Fact]
    public async Task Result_IsTheEngineRunForTheSameScene()
    {
        var scene = OneSource();
        var r = await new SimulationService().RunAsync(scene, Optics, 60_000, null, CancellationToken.None);
        var engine = new SimulationRunner(new DefaultSimulationFactory()).Run(SceneConfigBuilder.Build(scene, Optics, 60_000));

        Assert.Equal(engine.DetectorImage.Raw.ToArray(), r.Flood.Raw.ToArray());
        Assert.Equal(engine.Reconstruction!.Raw.ToArray(), r.Reconstruction!.Raw.ToArray());
        Assert.Equal(engine.Estimate!.Position, r.Estimate!.Position);
        Assert.Equal(engine.ReconOriginMm, r.ReconOriginMm);
        Assert.Equal(engine.ReconStepMm, r.ReconStepMm);
        // The count shown to the user is the physical (weighted) one, not the raw hit count.
        Assert.Equal(r.Flood.Raw.ToArray().Sum(), r.EffectiveCounts, 9);
    }

    [Fact]
    public async Task FloodAxis_MatchesTheDecodersPixelCentres()
    {
        var r = await new SimulationService().RunAsync(OneSource(), Optics, 5_000, null, CancellationToken.None);
        int n = r.Flood.Width;
        double pitch = SceneConfigBuilder.Build(OneSource(), Optics, 1).Detector.PixelPitchMm;

        Assert.Equal(pitch, r.FloodStepMm);
        for (int i = 0; i < n; i++)   // CrossCorrelationDecoder: pixel i centre at (i + 0.5)·pitch − N·pitch/2
            Assert.Equal((i + 0.5) * pitch - n * pitch / 2.0, r.FloodOriginMm + i * r.FloodStepMm, 9);
    }

    [Fact]
    public async Task Result_LocalizesAnInFieldSource()
    {
        var r = await new SimulationService().RunAsync(OneSource(15, 8), Optics, 400_000, null, CancellationToken.None);
        double err = Math.Sqrt(Math.Pow(r.Estimate!.Position.X - 15, 2) + Math.Pow(r.Estimate.Position.Y - 8, 2));
        double resolution = Optics.CellPitchMm * Optics.FocalDistanceMm / Optics.MaskDetectorDistanceMm;
        Assert.True(err < resolution, $"error {err:F1} mm, resolution element {resolution:F1} mm");
    }

    [Fact]
    public void BadArguments_ThrowBeforeAnyWorkIsScheduled()
    {
        var service = new SimulationService();
        // Thrown by the call itself, not by the returned task: the caller learns at once, on its own thread.
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = service.RunAsync(OneSource(), Optics, 0, null, CancellationToken.None); });
        Assert.Throws<ArgumentNullException>(() => { _ = service.RunAsync(null!, Optics, 1000, null, CancellationToken.None); });
        Assert.Throws<ArgumentNullException>(() => { _ = service.RunAsync(OneSource(), null!, 1000, null, CancellationToken.None); });
    }

    [Fact]
    public async Task Progress_RisesMonotonicallyToOne()
    {
        var seen = new List<double>();
        await new SimulationService().RunAsync(OneSource(), Optics, 50_000, new SyncProgress(seen), CancellationToken.None);
        Assert.NotEmpty(seen);
        for (int i = 1; i < seen.Count; i++) Assert.True(seen[i] >= seen[i - 1]);
        Assert.Equal(1.0, seen[^1]);
    }

    [Fact]
    public async Task AlreadyCancelled_NeverRuns()
    {
        var seen = new List<double>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SimulationService().RunAsync(OneSource(), Optics, 50_000, new SyncProgress(seen), cts.Token));
        Assert.Empty(seen);
    }

    [Fact]
    public async Task CancelledMidRun_Stops()
    {
        using var cts = new CancellationTokenSource();
        var progress = new SyncProgress(new List<double>(), onReport: _ => cts.Cancel());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SimulationService().RunAsync(OneSource(), Optics, 5_000_000, progress, cts.Token));
    }

    // Reports on the calling (worker) thread; Progress<T> would post to the thread pool and race the assertions.
    private sealed class SyncProgress(List<double> sink, Action<double>? onReport = null) : IProgress<double>
    {
        public void Report(double value) { lock (sink) sink.Add(value); onReport?.Invoke(value); }
    }
}
