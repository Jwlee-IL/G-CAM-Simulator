using Gcam.Detector;
using Xunit;

namespace Gcam.Tests;

/// <summary>The crystal interaction tables (theme 52) against independent references.</summary>
public class CrystalMaterialTests
{
    // GAGG (Gd3Al2Ga3O12) total μ/ρ from the NIST elemental tables mixed by weight, minus coherent: the table is
    // photo + Compton, so it must sit a few % BELOW NIST's total-with-coherent and never above it.
    [Theory]
    [InlineData(300.0, 0.17624)]
    [InlineData(600.0, 0.08585)]
    [InlineData(1000.0, 0.06094)]
    [InlineData(1250.0, 0.05338)]
    public void Gagg_IsJustBelowNistTotalWithCoherent(double keV, double nistTotal)
    {
        double ratio = CrystalMaterial.Gagg.MassAttenuation(keV) / nistTotal;
        Assert.InRange(ratio, 0.92, 1.0);
    }

    [Fact]
    public void Gagg_At662_MatchesTheNistMixture()
    {
        // photo + Compton = 0.0773 cm²/g; adding coherent (xraylib) gives 0.0799 vs 0.0801 from the NIST elemental mixture
        Assert.Equal(0.0773, CrystalMaterial.Gagg.MassAttenuation(661.7), 4);
        Assert.Equal(1.0, CrystalMaterial.Gagg.MuRel(661.7), 9);
        Assert.Equal(0.0513, CrystalMaterial.Gagg.MuPerMm(661.7), 3);           // × 6.63 g/cm³
    }

    [Fact]
    public void PhotoFraction_RisesWithZ_AndFallsWithEnergy()
    {
        // At 662 keV: NaI (I, Z 53) and GAGG (Gd, 64) ≈ 0.12–0.13, LYSO (Lu, 71) ≈ 0.23, BGO (Bi, 83) ≈ 0.32.
        Assert.InRange(CrystalMaterial.Gagg.PhotoFraction(661.7), 0.12, 0.14);
        Assert.True(CrystalMaterial.Find("BGO")!.PhotoFraction(661.7) > CrystalMaterial.Find("LYSO")!.PhotoFraction(661.7));
        Assert.True(CrystalMaterial.Find("LYSO")!.PhotoFraction(661.7) > CrystalMaterial.Gagg.PhotoFraction(661.7));
        foreach (var m in CrystalMaterial.All)
            Assert.True(m.PhotoFraction(122) > m.PhotoFraction(662) && m.PhotoFraction(662) > m.PhotoFraction(1332), m.Name);
    }

    [Fact]
    public void KEdge_IsAStep_NotASlope()
    {
        // Gd K edge 50.24 keV: attenuation jumps up across it (the table straddles every edge in range).
        var g = CrystalMaterial.Gagg;
        Assert.True(g.MassAttenuation(50.25) > 2.0 * g.MassAttenuation(50.23));
    }

    [Fact]
    public void Lookup_IsCaseInsensitive_AndUnknownFallsBackToGagg()
    {
        Assert.Equal("CeBr3", CrystalMaterial.Find("cebr3")!.Name);
        Assert.Null(CrystalMaterial.Find("ideal"));
        Assert.Same(CrystalMaterial.Gagg, CrystalMaterial.ForConfig("ideal"));
        Assert.Same(CrystalMaterial.Gagg, CrystalMaterial.ForConfig(null));
        Assert.Equal(7, CrystalMaterial.All.Count);
    }
}
