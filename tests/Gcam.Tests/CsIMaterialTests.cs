using Gcam.Detector;

namespace Gcam.Tests;

/// <summary>CsI host transport against xraylib 4.3.0 / NIST; provenance in samples/materials/CsI_checks.txt.</summary>
public sealed class CsIMaterialTests
{
    private static CrystalMaterial CsI => CrystalMaterial.Find("CsI")!;

    [Theory]
    [InlineData(300, .1818, .0148079546524, 5e-5, 5e-6)]
    [InlineData(600, .08373, .00388028010493, 5e-6, 5e-7)]
    [InlineData(800, .06769, .00220608283248, 5e-6, 5e-7)]
    public void TotalWithCoherent_AgreesWithNist(double keV, double nistTotal, double coherent,
        double nistHalfUnit, double tableHalfUnit)
    {
        // NIST CsI static compound table: cm²/g. Coherent is independently generated with CS_Rayl_CP.
        // CsI's largest measured relative like-total difference is 0.000105808471 (800 keV).
        // Round upward to 0.00011; add each source's displayed half-unit. No GAGG bound is reused.
        double tolerance = nistTotal * .00011 + nistHalfUnit + tableHalfUnit;
        Assert.InRange(CsI.MassAttenuation(keV) + coherent, nistTotal - tolerance, nistTotal + tolerance);
    }

    [Theory]
    [InlineData(1000, .05848)]
    [InlineData(1250, .05110)]
    public void AnalyticExtension_StaysBelowNistTotal(double keV, double nistTotal)
        => Assert.True(CsI.MassAttenuation(keV) < nistTotal);

    [Theory]
    [InlineData(1000)]
    [InlineData(1250)]
    [InlineData(1500)]
    public void AnalyticExtension_MatchesDocumentedFormula(double keV)
    {
        // Full-precision xraylib CsI photo anchors and composition-derived electrons/g.
        const double photo600 = .013564839088399753, photo800 = .007033020708288476;
        const double electronsPerGram = 2.50323763405434e23;
        double k = keV / 510.99895;
        double logarithm = Math.Log(1 + 2 * k);
        double kn = 2 * Math.PI * Math.Pow(2.8179403262e-13, 2) *
            ((1 + k) / (k * k) * (2 * (1 + k) / (1 + 2 * k) - logarithm / k)
             + logarithm / (2 * k) - (1 + 3 * k) / Math.Pow(1 + 2 * k, 2));
        double photo = photo800 * Math.Pow(keV / 800, Math.Log(photo800 / photo600) / Math.Log(800.0 / 600));
        double mu = photo + kn * electronsPerGram;
        // Half the last retained decimal unit: mu .057096/.049805/.044624; pf .07401/.05097/.03752.
        Assert.InRange(CsI.MassAttenuation(keV), mu - 5e-7, mu + 5e-7);
        Assert.InRange(CsI.PhotoFraction(keV), photo / mu - 5e-6, photo / mu + 5e-6);
    }

    [Fact]
    public void ReferenceAnchor_UsesCsIHostDensityAndNormalizesAttenuation()
    {
        Assert.Same(CsI, CrystalMaterial.Find(" csi "));
        Assert.Same(CsI, CrystalMaterial.ForConfig("CsI"));
        Assert.Equal("CsI:Tl", CsI.Label);
        Assert.Equal("CsI", CsI.Formula);
        Assert.Equal(4.51, CsI.DensityGPerCm3);
        const double mu = .0743482266668961, pf = .144938418801308;
        // C# table mu .074348 and pf .1449; propagate mu's half-unit through rho/10 for 1/mm.
        Assert.InRange(CsI.MassAttenuation(661.7), mu - 5e-7, mu + 5e-7);
        Assert.InRange(CsI.PhotoFraction(661.7), pf - 5e-5, pf + 5e-5);
        Assert.InRange(CsI.MuPerMm(661.7), (mu - 5e-7) * 4.51 / 10, (mu + 5e-7) * 4.51 / 10);
        Assert.Equal(1, CsI.MuRel(661.7));
        Assert.Same(CrystalMaterial.Gagg, CrystalMaterial.ForConfig("unknown"));
    }

    [Theory]
    [InlineData(33.1661, 33.1727)] // I K edge 33.1694 keV
    [InlineData(35.9810, 35.9882)] // Cs K edge 35.9846 keV
    public void KEdges_JumpInAttenuationAndPhotoFraction(double below, double above)
    {
        // Measured host jumps 3.281027× and 1.786975×; a shared 2× bound would be wrong for Cs.
        Assert.True(CsI.MassAttenuation(above) > CsI.MassAttenuation(below));
        Assert.True(CsI.PhotoFraction(above) > CsI.PhotoFraction(below));
    }

    [Fact]
    public void Table_ClampsAtEndpointsAndInterpolatesLogLogBetweenNodes()
    {
        Assert.Equal(CsI.MassAttenuation(20), CsI.MassAttenuation(1));
        Assert.Equal(CsI.MassAttenuation(3000), CsI.MassAttenuation(4000));
        Assert.Equal(CsI.PhotoFraction(20), CsI.PhotoFraction(1));
        Assert.Equal(CsI.PhotoFraction(3000), CsI.PhotoFraction(4000));
        double middle = Math.Sqrt(600 * 661.7);
        // Geometric midpoint of adjacent nodes is their geometric response mean; allow double arithmetic only.
        Assert.Equal(Math.Sqrt(CsI.MassAttenuation(600) * CsI.MassAttenuation(661.7)), CsI.MassAttenuation(middle), 12);
        Assert.Equal(Math.Sqrt(CsI.PhotoFraction(600) * CsI.PhotoFraction(661.7)), CsI.PhotoFraction(middle), 12);
    }
}
