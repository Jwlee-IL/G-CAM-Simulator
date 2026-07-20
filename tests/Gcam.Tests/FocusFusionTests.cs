using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>Focus fusion: cross-check the laser rangefinder against depth-from-focus. When the laser range is
/// wrong (it hit a different surface than the source), the coded-aperture image is sharper at the SOURCE's true
/// plane — MixedFieldStudy.CheckFocus must surface that (best-focus points back to the source, higher SNR).</summary>
public class FocusFusionTests
{
    private static (DetectorImage flood, SimulationConfig cfg, DefaultSimulationFactory f) Scene(double z)
    {
        const double d = 80, cell = 0.7;
        const int rank = 13;
        var s = new SourceConfig
        {
            Position = [10, 0, z], ActivityBq = 1.0, DirectionalBiasing = true,
            Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }],
        };
        var cfg = new SimulationConfig { PhotonCount = 2_500_000, Seed = 3, Source = s, Sources = [s] };
        cfg.Mask.Rank = rank; cfg.Mask.CellPitchMm = cell; cfg.Mask.MosaicX = cfg.Mask.MosaicY = 2;
        cfg.Geometry.MaskDetectorDistanceMm = d;
        cfg.Geometry.SourceMaskDistanceMm = z - d;
        cfg.Detector.PixelsX = cfg.Detector.PixelsY = 30; cfg.Detector.PixelPitchMm = 0.6;
        cfg.Decoder.Cyclic = false;
        var f = new DefaultSimulationFactory();
        var flood = new SimulationRunner(f).Run(cfg).DetectorImage;
        return (flood, cfg, f);
    }

    [Fact]
    public void Consistent_WhenLaserMatchesSource()
    {
        var (flood, cfg, f) = Scene(300);
        var chk = MixedFieldStudy.CheckFocus(flood, cfg, f, laserMm: 300, 120, 750, 14);
        // The sharpest plane is at the source; the laser plane is essentially as sharp (no mismatch).
        Assert.InRange(chk.BestFocalMm, 240, 380);
        Assert.True(chk.BestSnr <= chk.LaserSnr * 1.2, "laser matches source -> no meaningful sharper plane");
    }

    [Fact]
    public void FlagsMismatch_WhenLaserHitsWrongSurface()
    {
        var (flood, cfg, f) = Scene(300);      // source truly at 300 mm
        var chk = MixedFieldStudy.CheckFocus(flood, cfg, f, laserMm: 550, 220, 1375, 14);   // laser says 550 (wrong)
        // The image is sharper back toward the true 300 mm, and clearly sharper than at the laser plane.
        Assert.True(chk.BestFocalMm < 450, $"best focus should point back toward the source (~300), got {chk.BestFocalMm:F0}");
        Assert.True(chk.BestSnr > chk.LaserSnr * 1.2, $"image should be clearly sharper off the laser plane ({chk.BestSnr:F1} vs {chk.LaserSnr:F1})");
    }
}
