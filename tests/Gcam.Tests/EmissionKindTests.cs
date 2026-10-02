using Gcam.Configuration;
using Xunit;

namespace Gcam.Tests;

/// <summary>
/// The emission-line kind is descriptive data on the isotope table: Cs-137's 32.1 / 36.4 keV lines are Ba K X-rays
/// (K-shell fluorescence of the daughter Ba-137m after internal conversion of the 662 keV transition), every other
/// line is a gamma with no X-ray origin, and adding the kind and origin changed no energy or intensity.
/// </summary>
public class EmissionKindTests
{
    [Fact]
    public void Cs137_BaKLines_AreXRays_AndThePhotopeakIsGamma()
    {
        var cs = Isotopes.Get("Cs-137");
        Assert.Equal(new IsotopeLine(661.7, 0.851, EmissionKind.Gamma), cs.Lines[0]);
        Assert.Null(cs.Lines[0].XRayOrigin);
        // The emitter is barium (the daughter), not caesium.
        Assert.Equal(new IsotopeLine(32.1, 0.056, EmissionKind.XRay, "Ba K"), cs.Lines[1]);
        Assert.Equal(new IsotopeLine(36.4, 0.014, EmissionKind.XRay, "Ba K"), cs.Lines[2]);
        Assert.Equal(3, cs.Lines.Length);
    }

    [Fact]
    public void EveryOtherLine_DefaultsToGamma()
    {
        foreach (var isotope in Isotopes.All.Where(i => i.Name != "Cs-137"))
            Assert.All(isotope.Lines, l => { Assert.Equal(EmissionKind.Gamma, l.Kind); Assert.Null(l.XRayOrigin); });
    }

    [Fact]
    public void EnergyIntensityPair_ConvertsToAGammaLine()
    {
        IsotopeLine line = (122.1, 0.856);
        Assert.Equal(new IsotopeLine(122.1, 0.856), line);
        Assert.Equal(EmissionKind.Gamma, line.Kind);
        Assert.Null(line.XRayOrigin);
    }
}
