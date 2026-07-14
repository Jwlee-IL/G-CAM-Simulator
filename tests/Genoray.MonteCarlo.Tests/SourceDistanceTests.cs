using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>A source may set its own distance-to-detector via Position[2] (z, mm). The detector-biasing
/// weight carries A·cosθ/(4π r²), so a source twice as far detects ~1/4 as efficiently — the physical
/// inverse-square falloff the scene editor relies on. Position[2] = 0 keeps the old shared-plane behaviour.</summary>
public class SourceDistanceTests
{
    private static double Efficiency(double z)
    {
        var cfg = new SimulationConfig
        {
            PhotonCount = 300_000,
            Seed = 7,
            Source = new SourceConfig { Position = [0, 0, z], EnergyKeV = 661.7, DirectionalBiasing = true },
        };
        var res = new SimulationRunner(new DefaultSimulationFactory()).Run(cfg);
        return res.PhotonsEmitted > 0 ? res.DetectedWeight / res.PhotonsEmitted : 0.0;
    }

    [Fact]
    public void PerSourceDistance_FollowsInverseSquare()
    {
        double near = Efficiency(160.0);   // the default source plane (D 60 + S 100)
        double far = Efficiency(320.0);    // twice as far

        Assert.True(far < near, $"a farther source should detect less: near {near:E2} vs far {far:E2}");
        double ratio = far / near;
        // 1/r² → (160/320)² = 0.25; allow MC scatter + finite-detector geometry.
        Assert.InRange(ratio, 0.20, 0.30);
    }

    [Fact]
    public void ZeroZ_FallsBackToTheSharedPlane()
    {
        // Position[2] = 0 must match an explicit z at the default plane (160 mm) — backward compatible.
        double baseline = Efficiency(0.0);
        double explicitPlane = Efficiency(160.0);
        Assert.Equal(explicitPlane, baseline, 3);
    }
}
