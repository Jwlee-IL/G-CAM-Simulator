using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>Depth-of-field design study: the depth resolution (FWHM of the sharpness-vs-focal curve) must grow
/// with distance — a source far away is harder to place in depth. This guards the measurement the productization
/// trade (mask size vs safe standoff) is built on.</summary>
public class DepthDesignTests
{
    private static SimulationConfig Sharp()
    {
        var cfg = new SimulationConfig { PhotonCount = 1_500_000, Seed = 7 };
        cfg.Mask.Rank = 13; cfg.Mask.CellPitchMm = 0.7; cfg.Mask.MosaicX = cfg.Mask.MosaicY = 2;
        cfg.Geometry.MaskDetectorDistanceMm = 80;
        cfg.Detector.PixelsX = cfg.Detector.PixelsY = 30; cfg.Detector.PixelPitchMm = 0.6;
        return cfg;
    }

    [Fact]
    public void DepthResolution_WorsensWithDistance()
    {
        var factory = new DefaultSimulationFactory();
        var cfg = Sharp();
        double near = DepthDesignStudy.DepthResolutionMm(cfg, factory, 160, steps: 25);
        double far = DepthDesignStudy.DepthResolutionMm(cfg, factory, 400, steps: 25);

        Assert.True(near > 0 && far > 0);
        Assert.True(far > near * 1.5, $"depth FWHM should grow with distance: 160mm→{near:F0} vs 400mm→{far:F0}");
        // Aperture = rank·mosaic·cell = 13·2·0.7 = 18.2 mm.
        Assert.Equal(18.2, DepthDesignStudy.ApertureMm(cfg), 1);
    }

    [Fact]
    public void EffectiveRange_ReadsOffTheCurve()
    {
        var rows = new[]
        {
            new DepthDesignRow(150, 36, 0),   // 24%
            new DepthDesignRow(250, 148, 0),  // 59%
            new DepthDesignRow(400, 398, 0),  // 99%
        };
        double r = DepthDesignStudy.EffectiveRangeMm(rows, 0.40);   // crosses 40% between 150 and 250
        Assert.InRange(r, 150, 250);
    }
}
