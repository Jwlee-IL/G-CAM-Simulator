using Gcam.Masks;
using Xunit;

namespace Gcam.Tests;

/// <summary>The coded mask's tungsten attenuation μ(E)/μ(662). Below 122 keV the curve is now DEFINED
/// (was clamped at the 122 keV value), so soft lines (Am-241 59.5 keV, Cs-137 Ba X-rays) see the correct,
/// more-opaque mask. Photoelectric absorption rises steeply toward low energy, so the ratio keeps growing
/// below the old 122 keV floor.</summary>
public class MaskAttenuationTests
{
    [Fact]
    public void TungstenMu_IsAnchoredAt662_AndFlatteningAtHighEnergy()
    {
        Assert.Equal(1.0, CodedApertureMask.TungstenMuRel(662.0), 3);                 // anchor
        Assert.True(CodedApertureMask.TungstenMuRel(1332.0) < 1.0, "μ falls above 662 (Compton-flat)");
        Assert.True(CodedApertureMask.TungstenMuRel(122.0) > 20.0, "μ is steep below ~200 keV (photoelectric)");
    }

    [Fact]
    public void TungstenMu_RisesBelow122keV_ForSoftLines()
    {
        double r122 = CodedApertureMask.TungstenMuRel(122.0);
        double r60 = CodedApertureMask.TungstenMuRel(60.0);
        double r50 = CodedApertureMask.TungstenMuRel(50.0);

        // The fix: below the old 122 keV clamp the ratio keeps RISING toward lower energy.
        Assert.True(r60 > r122, $"60 keV ({r60:F1}) should exceed 122 keV ({r122:F1}) — was clamped equal before");
        Assert.True(r50 > r60, $"50 keV ({r50:F1}) should exceed 60 keV ({r60:F1})");

        // Am-241's 59.5 keV line now sees ~47× (was clamped to the 122 keV value ~28.6×).
        double am = CodedApertureMask.TungstenMuRel(59.5);
        Assert.InRange(am, 40.0, 60.0);
        Assert.True(am > r122, $"Am-241 59.5 keV ({am:F1}) should exceed the old 122 keV clamp ({r122:F1})");

        // Below the extended 50 keV floor it clamps (soft X-rays are already fully blocked) rather than
        // extrapolating without bound.
        Assert.Equal(CodedApertureMask.TungstenMuRel(50.0), CodedApertureMask.TungstenMuRel(30.0), 6);
    }
}
