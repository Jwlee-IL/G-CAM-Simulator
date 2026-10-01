using Gcam.Configuration;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>Field of view at field distance (theme 53, TODO-04): non-cyclic decoding localizes beyond the fully coded
/// field, the flood centroid tells the side of an out-of-field source until the aperture's shadow leaves the array
/// (~15° here), and beyond that only the front plate's leak reaches the detector.</summary>
public class FieldOfViewTests
{
    // The theme-22 hand-held head: rank-7 2×2 mosaic, 1 mm cells, 10 mm W, D = 55 mm, 16×16 @ 1 mm, 15 mm GAGG:Ce,Mg.
    private static SimulationConfig Head() => new()
    {
        PhotonCount = 300_000,
        Seed = 12345,
        Source = new SourceConfig { EnergyKeV = 661.7, DirectionalBiasing = true },
        Mask = new MaskConfig { Rank = 7, MosaicX = 2, MosaicY = 2, ThicknessMm = 10.0, LinearAttenuationPerMm = 0.178, CellPitchMm = 1.0 },
        Detector = new DetectorConfig { PixelsX = 16, PixelsY = 16, PixelPitchMm = 1.0, Material = "GAGG_Mg",
                                        CrystalThicknessMm = 15.0, CrystalAttenuationPerMm = 0.0513 },
        Geometry = new GeometryConfig { MaskDetectorDistanceMm = 55.0, SourceMaskDistanceMm = 100.0 },
    };

    private static FovRow[] Sweep(double[] angles) =>
        new FieldOfViewStudy().Run(Head(), distanceMm: 1000.0, directionDeg: 0.0, angles,
                                   onAxisCounts: [5000.0], bsrValues: [0.0], repeats: 40, gridStepDeg: 0.5);

    [Fact]
    public void FullyCodedHalfAngle_IsSquare()
    {
        Assert.Equal(3.64, FieldOfViewStudy.FullyCodedHalfAngleDeg(Head()), 2);
        Assert.Equal(5.14, FieldOfViewStudy.FullyCodedHalfAngleDeg(Head(), 45.0), 2);
    }

    [Fact]
    public void NonCyclic_LocalizesBeyondTheFullyCodedField_AndCentroidGivesTheSide()
    {
        var rows = Sweep([0.0, 2.0, 6.0, 10.0, 18.0]);
        FovRow At(double a) => rows.Single(r => r.AngleDeg == a);

        // 6° is outside the ±3.64° fully coded field: the cyclic decode aliases, the non-cyclic one still localizes.
        Assert.True(At(6.0).LocalizedNonCyclic >= 0.9, $"non-cyclic at 6°: {At(6.0).LocalizedNonCyclic:P0}");
        Assert.True(At(6.0).LocalizedCyclic <= 0.1, $"cyclic at 6°: {At(6.0).LocalizedCyclic:P0}");

        // 10°: not localized, but the centroid gives the side and flags "outside".
        Assert.True(At(10.0).SideByCentroid >= 0.9, $"side at 10°: {At(10.0).SideByCentroid:P0}");
        Assert.True(At(10.0).OutsideByCentroid >= 0.9, $"outside flag at 10°: {At(10.0).OutsideByCentroid:P0}");
        Assert.True(At(0.0).OutsideByCentroid <= 0.1);

        // 18°: the aperture's shadow has left the array — only the plate leak remains, the side cue is gone.
        Assert.True(At(18.0).RelEfficiency < 0.35, $"relative efficiency at 18°: {At(18.0).RelEfficiency:F2}");
        Assert.True(At(18.0).SideByCentroid < 0.8, $"side at 18°: {At(18.0).SideByCentroid:P0}");
    }

    [Fact]
    public void Run_IsReproducible()
    {
        var a = Sweep([0.0, 8.0]);
        var b = Sweep([0.0, 8.0]);
        Assert.Equal(a, b);
    }

    [Fact]
    public void NegativeAngles_AreRejected()
        => Assert.Throws<ArgumentException>(() => Sweep([-1.0]));
}
