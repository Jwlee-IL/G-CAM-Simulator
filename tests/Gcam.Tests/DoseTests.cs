using Gcam.Configuration;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>Dose rate from the detector spectrum (theme 54, TODO-05): ICRP 74 truth, a G(E) weighting fitted on
/// frontal responses, the collimating mask's angular response, and paralyzable over-range.</summary>
public class DoseTests
{
    private static SimulationConfig Head() => new()
    {
        Seed = 12345,
        Source = new SourceConfig { DirectionalBiasing = true },
        Mask = new MaskConfig { Rank = 7, MosaicX = 2, MosaicY = 2, ThicknessMm = 10.0, LinearAttenuationPerMm = 0.178, CellPitchMm = 1.0 },
        Detector = new DetectorConfig { PixelsX = 16, PixelsY = 16, PixelPitchMm = 1.0, Material = "GAGG_Mg",
                                        CrystalThicknessMm = 15.0, CrystalAttenuationPerMm = 0.0513, EnergyResolutionFwhm = 0.07 },
        Geometry = new GeometryConfig { MaskDetectorDistanceMm = 55.0, SourceMaskDistanceMm = 100.0 },
    };

    private static DoseResponse At(double keV, double angleDeg = 0.0)
        => new DoseStudy().Response(Head(), keV, angleDeg, distanceMm: 1000.0, photons: 100_000, seed: 7 + (int)keV);

    [Theory]
    [InlineData(60.0, 0.51)]
    [InlineData(600.0, 3.44)]
    [InlineData(1000.0, 5.20)]
    public void AmbientDose_MatchesIcrp74AtTablePoints(double keV, double pSvCm2)
        => Assert.Equal(pSvCm2, AmbientDose.PerFluence(keV), 6);

    [Fact]
    public void AmbientDose_InterpolatesLogLog_AndRejectsOutOfRange()
    {
        double h662 = AmbientDose.PerFluence(661.7);
        Assert.InRange(h662, 3.44, 4.38);
        Assert.Throws<ArgumentOutOfRangeException>(() => AmbientDose.PerFluence(5.0));
    }

    [Fact]
    public void FittedWeighting_TracksDose_FrontallyButNotOffAxis()
    {
        var fit = new[] { 80.0, 200.0, 400.0, 800.0, 1500.0 }.Select(e => At(e)).ToList();
        var g = DoseStudy.FitG(fit, order: 3);

        // A held-out energy reads within ±25 % frontally (the requirement is ±50 %).
        var cs = At(661.7);
        double ratio = DoseStudy.Estimate(g, cs.CountsPerFluence) / cs.DosePerFluence;
        Assert.InRange(ratio, 0.75, 1.25);

        // 20° off axis the 10 mm mask collimates a 122 keV field almost completely: the head reads far low.
        var oblique = At(122.0, 20.0);
        Assert.True(DoseStudy.Estimate(g, oblique.CountsPerFluence) / oblique.DosePerFluence < 0.1);
    }

    [Fact]
    public void OverRange_FollowsParalyzableDeadTime_AndLiveTimeCorrectsUntilItsResolution()
    {
        // 1e6 counts/µSv at 3600 µSv/h → n = 1 Mcps = 1/τ: recorded n/e, live 1/e.
        var rows = DoseStudy.OverRange(countsPerMicroSv: 1e6, tauUs: 1.0, microSvPerH: [3600.0, 3.6e5], calibrationRatio: 0.9);
        Assert.Equal(0.9 * Math.Exp(-1.0), rows[0].RawRatio, 6);
        Assert.Equal(0.9, rows[0].LiveCorrectedRatio, 6);
        // n·τ = 100: live fraction e^-100 is below a live clock's resolution — the correction fails, the flag remains.
        Assert.True(rows[1].LiveFraction < 1e-3);
        Assert.True(rows[1].LiveCorrectedRatio < 0.01);
        Assert.Throws<ArgumentException>(() => DoseStudy.OverRange(1e6, 1.0, [0.0]));
    }
}
