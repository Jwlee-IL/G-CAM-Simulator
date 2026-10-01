using Gcam.Configuration;
using Gcam.Simulation;

namespace Gcam.Tests;

public class SceneConfigBuilderTests
{
    [Theory]
    [InlineData(13, 13)]
    [InlineData(12, 11)]   // ties resolve downward
    [InlineData(1, 2)]
    [InlineData(20, 19)]
    public void Rank_snaps_to_nearest_prime(int requested, int expected) =>
        Assert.Equal(expected, SceneConfigBuilder.NearestPrime(requested));

    [Fact]
    public void Builds_multi_source_finite_mask_config()
    {
        var scene = new[]
        {
            new SceneSource { X = -10, Y = 5, DistanceMm = 900, ActivityUCi = 100 },
            new SceneSource { Isotope = "Co-57", X = 20, DistanceMm = 1100 },
        };
        var optics = new OpticsSettings { MuraRank = 12, MaskDetectorDistanceMm = 80, FocalDistanceMm = 1000 };

        var cfg = SceneConfigBuilder.Build(scene, optics, photons: 10_000);

        Assert.Equal(2, cfg.Sources!.Length);
        Assert.Equal(new[] { -10.0, 5.0, 900.0 }, cfg.Sources[0].Position);
        Assert.Equal("Co-57", cfg.Sources[1].Isotope);
        Assert.Equal(11, cfg.Mask.Rank);
        Assert.Equal(920, cfg.Geometry.SourceMaskDistanceMm);
        Assert.False(cfg.Decoder.Cyclic);
        // Recon grid stays just inside the fully-coded FOV at the focal plane.
        Assert.True(cfg.Decoder.ReconHalfExtentMm < SceneConfigBuilder.FcfovHalfMm(optics));
    }

    [Fact]
    public void Rejects_non_positive_photon_budget() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SceneConfigBuilder.Build([new SceneSource()], new OpticsSettings(), photons: 0));

    [Fact]
    public void Runner_reports_progress_and_honours_cancellation()
    {
        var cfg = SceneConfigBuilder.Build([new SceneSource()], new OpticsSettings(), photons: 50_000);
        var runner = new SimulationRunner(new DefaultSimulationFactory());

        var reports = new List<double>();
        runner.Run(cfg, new SyncProgress(reports.Add), CancellationToken.None);
        Assert.NotEmpty(reports);
        Assert.Equal(1.0, reports[^1]);
        Assert.True(reports.SequenceEqual(reports.Order()));   // monotonic

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => runner.Run(cfg, null, cts.Token));
    }

    /// <summary>Progress&lt;T&gt; posts to a sync context; tests want the callback inline.</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
