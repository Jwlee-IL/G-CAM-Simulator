using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Masks;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>
/// Mask–detector alignment / pose error: the physical mask is displaced/rolled from the ideal pose the decoder
/// back-projects with, so a mis-registered mask casts a systematically shifted/rotated coded shadow → a
/// SYSTEMATIC localization bias. The pose lives on the mask Transmit only (the decoder stays ideal).
/// </summary>
public class AlignmentTests
{
    private static readonly MaskPattern Pattern = MuraGenerator.Mosaic(7, 2, 2);
    private const double Pitch = 1.0, Thick = 10.0, Mu = 0.178;

    private static CodedApertureMask Mask(double dx = 0, double dy = 0, double dz = 0, double roll = 0)
        => new CodedApertureMask(Pattern, 0.0, Pitch, Thick, Mu, holeFraction: 0.71,
                                 offsetXMm: dx, offsetYMm: dy, offsetZMm: dz, rollDeg: roll);

    // --- Pose transform equivalence: an offset mask hit at p behaves like the ideal mask hit at p − offset ---

    [Fact]
    public void InPlaneOffset_EquivalentToShiftingTheRay()
    {
        var ideal = Mask();
        var offset = Mask(dx: 0.3, dy: -0.2);
        int checkedRays = 0;
        for (double x = -4; x <= 4; x += 0.37)
            for (double y = -4; y <= 4; y += 0.41)
            {
                // Straight-down ray. Offset mask at (x,y) must equal ideal mask at (x−dx, y−dy).
                bool a = ideal.Transmit(Straight(x - 0.3, y + 0.2), 661.7, new DefaultRandom(42));
                bool b = offset.Transmit(Straight(x, y), 661.7, new DefaultRandom(42));
                Assert.Equal(a, b);
                checkedRays++;
            }
        Assert.True(checkedRays > 100);
    }

    [Fact]
    public void Roll_EquivalentToRotatingTheRay()
    {
        double rollDeg = 3.0, r = rollDeg * System.Math.PI / 180.0;
        double c = System.Math.Cos(r), s = System.Math.Sin(r);
        var ideal = Mask();
        var rolled = Mask(roll: rollDeg);
        for (double x = -4; x <= 4; x += 0.53)
            for (double y = -4; y <= 4; y += 0.59)
            {
                // Rolled mask at (x,y) equals ideal at Rz(−roll)·(x,y).
                double nx = c * x + s * y, ny = -s * x + c * y;
                bool a = ideal.Transmit(Straight(nx, ny), 661.7, new DefaultRandom(7));
                bool b = rolled.Transmit(Straight(x, y), 661.7, new DefaultRandom(7));
                Assert.Equal(a, b);
            }
    }

    [Fact]
    public void PerfectAlignment_IsUnchangedFromNoPose()
    {
        var plain = new CodedApertureMask(Pattern, 0.0, Pitch, Thick, Mu, holeFraction: 0.71);
        var zeroPose = Mask();   // all offsets/roll 0
        for (double x = -3.5; x <= 3.5; x += 0.7)
            for (double y = -3.5; y <= 3.5; y += 0.7)
            {
                bool a = plain.Transmit(Straight(x, y), 661.7, new DefaultRandom(99));
                bool b = zeroPose.Transmit(Straight(x, y), 661.7, new DefaultRandom(99));
                Assert.Equal(a, b);
            }
    }

    private static Ray Straight(double x, double y) => new Ray(new Vector3(x, y, 20.0), new Vector3(0, 0, -1));

    // --- Study: in-plane offset biases localization, amplified by the magnification (D+S)/D ---

    [Fact]
    public void InPlaneOffset_BiasesLocalization_AmplifiedByMagnification()
    {
        var cfg = new SimulationConfig { PhotonCount = 400_000, Seed = 12345 };
        double D = cfg.Geometry.MaskDetectorDistanceMm, S = cfg.Geometry.SourceMaskDistanceMm;
        var rows = new AlignmentStudy(new DefaultSimulationFactory())
            .Run(cfg, (8.0, 0.0, 0.0), offsetsMm: [0.0, 1.0], zOffsetsMm: [0.0], rollsDeg: [0.0],
                 photonBudget: 400_000.0, repeats: 40);

        var off0 = System.Array.Find(rows, r => r.Dof == "offset_x" && r.Magnitude == 0.0)!;
        var off1 = System.Array.Find(rows, r => r.Dof == "offset_x" && r.Magnitude == 1.0)!;

        Assert.True(off0.BiasMm < 1.0, $"aligned mask should localize near the floor, got {off0.BiasMm}");
        // A 1 mm mask offset shifts the shadow, amplified to a source bias ≈ (D+S)/D ≈ 2.7 mm.
        double amp = System.Math.Abs(off1.BiasXMm) / 1.0;
        Assert.True(amp > 1.8 && amp < 3.4, $"1mm offset bias should be ~(D+S)/D={((D + S) / D):F2}×, got {amp:F2}×");
    }
}
