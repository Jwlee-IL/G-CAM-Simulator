using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>Automatic per-source depth by refocusing (MixedFieldStudy.LocalizeDepths): decode ONE flood at a
/// sweep of focal planes and take each source's sharpest plane (highest peak SNR) as its distance. Two sources
/// at DIFFERENT distances are each recovered in 3D from a single acquisition.</summary>
public class DepthLocalizationTests
{
    private static SourceConfig Src(double x, double y, double z) => new()
    {
        Position = [x, y, z], ActivityBq = 1.0, DirectionalBiasing = true,
        Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }],
    };

    [Fact]
    public void TwoSourcesAtDifferentDistances_RecoveredIn3D()
    {
        const double d = 80, cell = 0.7;
        const int rank = 13;
        var a = Src(5, 0, 160);        // near
        var b = Src(-5, 0, 260);       // far
        var cfg = new SimulationConfig { PhotonCount = 2_500_000, Seed = 9, Source = a, Sources = [a, b] };
        cfg.Mask.Rank = rank; cfg.Mask.CellPitchMm = cell; cfg.Mask.MosaicX = cfg.Mask.MosaicY = 2;
        cfg.Geometry.MaskDetectorDistanceMm = d;
        cfg.Geometry.SourceMaskDistanceMm = 160 - d;
        cfg.Detector.PixelsX = cfg.Detector.PixelsY = 30; cfg.Detector.PixelPitchMm = 0.6;
        cfg.Decoder.Cyclic = false;

        var factory = new DefaultSimulationFactory();
        var flood = new SimulationRunner(factory).Run(cfg).DetectorImage;

        var found = MixedFieldStudy.LocalizeDepths(flood, cfg, factory, k: 2, zMin: 100, zMax: 340, steps: 25);
        Assert.Equal(2, found.Length);

        // Match each source to its nearest found peak (lateral), then check the recovered distance.
        var forA = Nearest(found, 5, 0);
        var forB = Nearest(found, -5, 0);
        Assert.NotSame(forA, forB);

        Assert.InRange(forA.Xmm, 4.0, 6.0);
        Assert.InRange(forA.Zmm, 135, 185);      // true 160, sweep step 10 mm
        Assert.InRange(forB.Xmm, -6.0, -4.0);
        Assert.InRange(forB.Zmm, 235, 285);      // true 260
        // The near source must be recovered nearer than the far one.
        Assert.True(forA.Zmm < forB.Zmm, $"A ({forA.Zmm:F0}) should be nearer than B ({forB.Zmm:F0})");
    }

    private static MixedFieldStudy.DepthPeak Nearest(MixedFieldStudy.DepthPeak[] found, double x, double y)
    {
        MixedFieldStudy.DepthPeak best = found[0];
        double bd = double.MaxValue;
        foreach (var p in found)
        {
            double dd = (p.Xmm - x) * (p.Xmm - x) + (p.Ymm - y) * (p.Ymm - y);
            if (dd < bd) { bd = dd; best = p; }
        }
        return best;
    }
}
