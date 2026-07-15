using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>Auto-focus: MixedFieldStudy.BestFocalMm must find the focal plane that focuses a source at an
/// UNKNOWN distance — the fix for "a far source isn't caught" (the app used to decode at a fixed near plane).</summary>
public class AutoFocusTests
{
    private static SimulationConfig Cfg(double srcZ)
    {
        const double d = 80, cell = 0.7;
        const int rank = 13;
        var s = new SourceConfig
        {
            Position = [5, 0, srcZ], ActivityBq = 1.0, DirectionalBiasing = true,
            Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }],
        };
        var cfg = new SimulationConfig { PhotonCount = 2_000_000, Seed = 3, Source = s, Sources = [s] };
        cfg.Mask.Rank = rank; cfg.Mask.CellPitchMm = cell; cfg.Mask.MosaicX = cfg.Mask.MosaicY = 2;
        cfg.Geometry.MaskDetectorDistanceMm = d;
        cfg.Geometry.SourceMaskDistanceMm = srcZ - d;
        cfg.Detector.PixelsX = cfg.Detector.PixelsY = 30; cfg.Detector.PixelPitchMm = 0.6;
        cfg.Decoder.Cyclic = false;
        return cfg;
    }

    [Theory]
    [InlineData(160)]
    [InlineData(280)]
    public void BestFocal_FindsTheSourcePlane(double srcZ)
    {
        var factory = new DefaultSimulationFactory();
        var flood = new SimulationRunner(factory).Run(Cfg(srcZ)).DetectorImage;
        double best = MixedFieldStudy.BestFocalMm(flood, Cfg(srcZ), factory, 60, 400, 20);
        // Within one sweep step (~18 mm) of the true distance.
        Assert.InRange(best, srcZ - 25, srcZ + 25);
    }
}
