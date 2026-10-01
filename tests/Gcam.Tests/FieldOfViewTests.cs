using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

/// <summary>Field of view at field distance (theme 53, TODO-04): non-cyclic decoding localizes beyond the fully coded
/// field, the flood centroid tells the side of an out-of-field source until the aperture's shadow leaves the array
/// (~15° here), and beyond that only the front plate's leak reaches the detector.</summary>
public class FieldOfViewTests
{
    private static SimulationConfig Head() => Rigs.Handheld();

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
