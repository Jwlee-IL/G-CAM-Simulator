using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>A source-locating camera must work at a realistic standoff, not just right in front. The default
/// Sharp optics (rank 13, cell 0.7, D 80, 30×30 @ 0.6) must localize a source ~1 m away accurately (laterally)
/// when focused there — this guards the app's realistic ~1 m default operating distance.</summary>
public class RangeLocalizationTests
{
    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2000)]
    public void LocalizesAtStandoff(double z)
    {
        const double d = 80, cell = 0.7;
        const int rank = 13;
        double srcX = 15;   // inside the FCFOV (= rank·cell·z/D/2) even at the closest tested standoff (500 mm)
        var s = new SourceConfig
        {
            Position = [srcX, 0, z], ActivityBq = 1.0, DirectionalBiasing = true,
            Lines = [new EmissionLine { EnergyKeV = 661.7, Intensity = 0.851 }],
        };
        var cfg = new SimulationConfig { PhotonCount = 3_000_000, Seed = 3, Source = s, Sources = [s] };
        cfg.Mask.Rank = rank; cfg.Mask.CellPitchMm = cell; cfg.Mask.MosaicX = cfg.Mask.MosaicY = 2;
        cfg.Geometry.MaskDetectorDistanceMm = d;
        cfg.Geometry.SourceMaskDistanceMm = z - d;      // focused at the source plane
        cfg.Detector.PixelsX = cfg.Detector.PixelsY = 30; cfg.Detector.PixelPitchMm = 0.6;
        cfg.Decoder.Cyclic = false;
        double frac = d / z;
        cfg.Decoder.ReconHalfExtentMm = 0.95 * rank * cell / frac / 2.0;
        cfg.Decoder.ReconStepMm = System.Math.Max(0.2, cell / frac / 4.0);

        var factory = new DefaultSimulationFactory();
        var flood = new SimulationRunner(factory).Run(cfg).DetectorImage;
        var dec = factory.CreateDecoder(cfg)!.Decode(flood);
        var pk = MixedFieldStudy.TopPeaks(dec.Reconstruction, dec.ReconOriginMm, dec.ReconStepMm, 1, 5)[0];
        double err = System.Math.Sqrt((pk.Xmm - srcX) * (pk.Xmm - srcX) + pk.Ymm * pk.Ymm);
        // Lateral resolution ~ cell·z/D (9 mm at 1 m); the peak still localizes to a fraction of that.
        Assert.True(err < 8.0, $"source at {z} mm should localize within 8 mm laterally, got {err:F1} mm");
    }
}
