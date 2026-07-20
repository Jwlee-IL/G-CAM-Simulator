using System.Linq;
using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>
/// Depth-of-interaction (DOI) parallax as reconstruction sees it. A gamma enters the crystal front face but interacts
/// at a random depth; for an OBLIQUE (off-axis) ray the scintillation centroid is displaced by depth·tan(angle) from
/// the front-face crossing the decoder back-projects. So DOI adds a systematic off-axis localization shift that grows
/// with crystal thickness — zero on-axis — which the centre-back-projecting decoder cannot correct.
/// </summary>
public class DoiParallaxTests
{
    [Fact]
    public void DoiAddsAnOffAxisShiftThatGrowsWithThickness_ZeroOnAxis()
    {
        var cfg = new SimulationConfig { Seed = 55 };
        var rows = new DoiParallaxStudy().Run(cfg, [0.0, 9.0], [5.0, 30.0], photonCount: 2_000_000, seed: 55);
        double Shift(double x, double t) => rows.Single(r => r.SourceXMm == x && r.CrystalThicknessMm == t).DoiShiftMm;

        // On-axis (normal incidence) there is no DOI displacement, at any thickness.
        Assert.True(System.Math.Abs(Shift(0.0, 5.0)) < 0.05, $"on-axis thin shift {Shift(0.0, 5.0):F3}");
        Assert.True(System.Math.Abs(Shift(0.0, 30.0)) < 0.05, $"on-axis thick shift {Shift(0.0, 30.0):F3}");

        // Off-axis the DOI shift is real and GROWS with crystal thickness (deeper interactions → bigger parallax).
        double thin = Shift(9.0, 5.0), thick = Shift(9.0, 30.0);
        Assert.True(thick > 1.5 * thin, $"DOI shift should grow with thickness: 5mm {thin:F3} vs 30mm {thick:F3}");
        // Sub-mm but non-negligible against the sub-cell precision floor.
        Assert.InRange(thick, 0.1, 1.0);
    }
}
