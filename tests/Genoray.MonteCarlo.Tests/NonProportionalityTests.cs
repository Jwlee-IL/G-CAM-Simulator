using System.Linq;
using Genoray.MonteCarlo.Detector;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>
/// Scintillator non-proportionality: the light yield per keV varies with the depositing electron's energy, so a
/// full-energy gamma's total light Σ Eᵢ·nP(Eᵢ) fluctuates with the (random) Compton-cascade composition even at fixed
/// total energy — the INTRINSIC resolution, derived from first principles instead of the hand-set constant floor. It is
/// exactly 0 for a proportional crystal and ENERGY-dependent (which a constant cannot be).
/// </summary>
public class NonProportionalityTests
{
    // --- the response curves ---

    [Fact]
    public void AllPresets_AreNormalizedTo662()
    {
        foreach (var np in new[] { NonProportionality.Proportional, NonProportionality.Gagg, NonProportionality.NaI, NonProportionality.CsI })
            Assert.Equal(1.0, np.Relative(661.7), 2);
    }

    [Fact]
    public void Proportional_IsFlat_NaI_HasALowEnergyDeficit()
    {
        Assert.Equal(1.0, NonProportionality.Proportional.Relative(15.0), 9);
        Assert.Equal(1.0, NonProportionality.Proportional.Relative(900.0), 9);
        // NaI(Tl) loses light at low electron energy (the classic non-proportional halide).
        Assert.True(NonProportionality.NaI.Relative(10.0) < 0.80);
        // GAGG stays within a few % across the whole range — comparatively proportional.
        foreach (double e in new[] { 10.0, 60.0, 200.0, 662.0, 1332.0 })
            Assert.True(System.Math.Abs(NonProportionality.Gagg.Relative(e) - 1.0) < 0.08);
    }

    // --- intrinsic resolution derived from the cascade ---

    private static NonPropPoint[] Points()
    {
        double[] energies = { 122, 662, 1332 };
        var crystals = new[] { NonProportionality.Proportional, NonProportionality.Gagg, NonProportionality.NaI, NonProportionality.CsI };
        return new NonProportionalityStudy().Run(energies, crystals, samples: 400_000, seed: 909, spectrumEnergyKeV: 662.0).points;
    }

    private static double Fwhm(NonPropPoint[] pts, string crystal, double e)
        => pts.Single(p => p.Crystal == crystal && p.EnergyKeV == e).IntrinsicFwhmPct;

    [Fact]
    public void ProportionalCrystal_HasExactlyZeroIntrinsicResolution()
    {
        var pts = Points();
        // The mechanism check: with nP≡1 the light is exactly the energy, so there is NO intrinsic spread at all —
        // the resolution the study reports comes ENTIRELY from non-proportionality, nothing else.
        Assert.All(pts.Where(p => p.Crystal == "proportional"), p => Assert.Equal(0.0, p.IntrinsicFwhmPct, 6));
    }

    [Fact]
    public void NonProportionality_GivesAnEnergyDependentIntrinsicResolution()
    {
        var pts = Points();
        // Real, nonzero, and DIFFERENT at different energies (a constant floor cannot reproduce this).
        Assert.True(Fwhm(pts, "NaI", 122) > 1.5, "NaI's low-energy deficit should give a large intrinsic FWHM at 122 keV");
        Assert.True(Fwhm(pts, "NaI", 122) > 2.0 * Fwhm(pts, "NaI", 1332), "NaI intrinsic resolution should fall with energy");

        // GAGG (this camera) is comparatively proportional — a modest floor, and smaller than CsI.
        Assert.InRange(Fwhm(pts, "GAGG", 662), 0.5, 3.0);
        Assert.True(Fwhm(pts, "CsI", 662) > Fwhm(pts, "GAGG", 662), "CsI is more non-proportional than GAGG");
    }
}
