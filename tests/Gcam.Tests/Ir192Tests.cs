using Gcam.Configuration;
using Gcam.Detector;
using Gcam.Masks;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>
/// Ir-192 (industrial radiography, URS reference source RS-1): the line table, the tungsten attenuation it relies on
/// between 200 and 620 keV, the explicit "cascade not modelled" guard, and imaging through the full pipeline.
/// </summary>
public class Ir192Tests
{
    private static IsotopeInfo Ir => Isotopes.Get("Ir-192");

    [Fact]
    public void LineTable_MatchesEnsdf_PrimaryLineFirst()
    {
        Assert.Equal("Ir-192", Ir.Name);
        Assert.Equal(9, Ir.Lines.Length);
        Assert.Equal(316.5, Ir.Lines[0].EnergyKeV);            // primary line / photopeak centre
        Assert.Equal(0.8286, Ir.Lines[0].Intensity, 4);
        // Sum of the nine ENSDF intensities (82.86 + 47.84 + 29.70 + 28.71 + 8.216 + 5.34 + 4.522 + 3.31 + 3.19) %
        Assert.Equal(2.13688, Ir.Lines.Sum(l => l.Intensity), 4);
        Assert.Equal(73.829, Ir.HalfLifeYears * 365.25, 3);
        Assert.Equal("Cs-137", Isotopes.All[0].Name);          // the pickers' "unknown → first entry" default
    }

    // NIST μ/ρ for W (Hubbell & Seltzer) at the grid points, ÷ μ/ρ(662 keV) = 0.09852 cm²/g.
    [Theory]
    [InlineData(200.0, 7.962)]
    [InlineData(300.0, 3.287)]
    [InlineData(400.0, 1.954)]
    [InlineData(500.0, 1.399)]
    [InlineData(600.0, 1.109)]
    public void TungstenMuRel_MatchesNist_Between200And600keV(double keV, double nistRatio)
    {
        Assert.Equal(nistRatio, CodedApertureMask.TungstenMuRel(keV), 3);
        Assert.Equal(nistRatio, MaskSecondary.MuRel(keV), 3);   // the two copies of the table must agree
    }

    [Theory]
    [InlineData(316.5, 2.984)]    // NIST log-log between 300 and 400 keV
    [InlineData(468.1, 1.544)]
    [InlineData(604.4, 1.101)]
    public void TungstenMuRel_AtIr192Lines_IsWithinOnePercentOfNist(double keV, double nistRatio)
    {
        double rel = CodedApertureMask.TungstenMuRel(keV) / nistRatio - 1;
        Assert.True(Math.Abs(rel) < 0.01, $"{keV} keV: table {CodedApertureMask.TungstenMuRel(keV):F3} vs NIST {nistRatio:F3}");
    }

    [Fact]
    public void CascadeStudy_RefusesIr192_InsteadOfSilentlyRunningCs137()
    {
        var e = Assert.Throws<NotSupportedException>(() => DecayScheme.From("Ir-192"));
        Assert.Contains("not modelled", e.Message);
        Assert.Equal(Isotope.Cs137, DecayScheme.From("unknown").Isotope);   // existing fallback unchanged
    }

    private static SimulationConfig SingleScene(string isotope, double x, double y)
    {
        var scene = new[] { new SceneSource { Isotope = isotope, X = x, Y = y } };
        return SceneConfigBuilder.Build(scene, new OpticsSettings(), photons: 1_500_000);
    }

    // Peak-picking returns a reconstruction grid cell, so the error floor is set by the grid: one cell diagonal
    // (step·√2) is the largest miss a correctly decoded source can have. In the Studio geometry (1 m standoff) the
    // step is ~3 mm, which is why a fixed 2 mm tolerance from the finer-grid tests does not apply here.
    private static (double ErrorMm, double CellDiagonalMm) Localise(ISimulationFactory factory, SimulationConfig cfg)
    {
        var r = new MixedFieldStudy(factory).LocalizeMultiple(cfg, k: 1, minSeparationMm: 3.0);
        var m = Assert.Single(MixedFieldStudy.MatchOneToOne(r.TruthXY, r.Found));
        return (m.ErrorMm, r.StepMm * Math.Sqrt(2));
    }

    [Fact]
    public void Ir192Source_LocalisesThroughTheFullPipeline_AsWellAsCs137()
    {
        // All nine lines through the energy-dependent mask; the decoded peak lands in the source's cell, and the
        // error is no worse than a Cs-137 source at the same place (same grid, same photon budget).
        var (ir, cell) = Localise(new DefaultSimulationFactory(), SingleScene("Ir-192", 12, -8));
        var (cs, _) = Localise(new DefaultSimulationFactory(), SingleScene("Cs-137", 12, -8));
        Assert.True(ir <= cell, $"Ir-192 at (12, -8) found {ir:F2} mm away (one cell = {cell:F2} mm)");
        Assert.True(ir <= cs + cell / 2, $"Ir-192 error {ir:F2} mm vs Cs-137 {cs:F2} mm");
    }

    [Fact]
    public void Ir192_ThroughA316keVWindow_StillLocalises()
    {
        // Crystal Compton + a ±10 % window on the primary line: the 308 / 296 keV lines and downscatter of the
        // 468–612 keV lines share the window, yet the image keeps the source position.
        var factory = new ComptonFactory(ComptonStrategy.PerPixelWindow, 316.5, 0.10);
        var (err, cell) = Localise(factory, SingleScene("Ir-192", -10, 6));
        Assert.True(err <= cell, $"Ir-192 at (-10, 6) through the 316 keV window found {err:F2} mm away (one cell = {cell:F2} mm)");
    }
}
