using Gcam.Configuration;
using Gcam.Core;
using Gcam.Masks;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

/// <summary>The cross-correlation decoder against an ANALYTIC shadow (no Monte Carlo): each pixel is lit iff the ray
/// from the source to its centre crosses an open cell. The decode must put its maximum on the source, be linear in
/// the image, and (cyclic) shrug off a modest uniform pedestal.</summary>
public class DecoderInvariantTests
{
    private static SimulationConfig Rig(bool cyclic)
    {
        var cfg = Rigs.Lab();
        cfg.Decoder.Cyclic = cyclic;
        cfg.Decoder.SubCellInterpolation = SubCellMethod.None;   // raw argmax: the invariant is about the grid
        return cfg;
    }

    // Lit (1) or shadowed (0) pixels for a point source on the source plane, through the ideal mask.
    private static DetectorImage Shadow(SimulationConfig cfg, double sx, double sy)
    {
        var pattern = MuraGenerator.Mosaic(cfg.Mask.Rank, cfg.Mask.MosaicX, cfg.Mask.MosaicY);
        double d = cfg.Geometry.MaskDetectorDistanceMm, frac = d / (d + cfg.Geometry.SourceMaskDistanceMm);
        double pitch = cfg.Detector.PixelPitchMm, cell = cfg.Mask.CellPitchMm;
        double detHalf = cfg.Detector.PixelsX * pitch / 2.0, maskHalfX = pattern.Width * cell / 2.0, maskHalfY = pattern.Height * cell / 2.0;
        var img = new DetectorImage(cfg.Detector.PixelsX, cfg.Detector.PixelsY);
        for (int iy = 0; iy < img.Height; iy++)
            for (int ix = 0; ix < img.Width; ix++)
            {
                double px = (ix + 0.5) * pitch - detHalf, py = (iy + 0.5) * pitch - detHalf;
                int cx = (int)Math.Floor((px + (sx - px) * frac + maskHalfX) / cell);
                int cy = (int)Math.Floor((py + (sy - py) * frac + maskHalfY) / cell);
                bool open = cx >= 0 && cy >= 0 && cx < pattern.Width && cy < pattern.Height && pattern[cx, cy];
                img[ix, iy] = open ? 1.0 : 0.0;
            }
        return img;
    }

    private static (int gx, int gy) GridIndex(DecodeResult r, double x, double y)
        => ((int)Math.Round((x - r.ReconOriginMm) / r.ReconStepMm), (int)Math.Round((y - r.ReconOriginMm) / r.ReconStepMm));

    [Theory]
    [InlineData(true, 0, 0)]
    [InlineData(true, 12, -7)]
    [InlineData(true, -20, 15)]
    [InlineData(false, 0, 0)]
    [InlineData(false, 12, -7)]
    public void AnalyticShadow_PeaksAtItsSource(bool cyclic, int kx, int ky)
    {
        var cfg = Rig(cyclic);
        var decoder = new DefaultSimulationFactory().CreateDecoder(cfg)!;
        var probe = decoder.Decode(new DetectorImage(cfg.Detector.PixelsX, cfg.Detector.PixelsY));
        // Put the source exactly on a grid point, kx / ky steps from the centre.
        int c = probe.Reconstruction.Width / 2;
        double sx = probe.ReconOriginMm + (c + kx) * probe.ReconStepMm, sy = probe.ReconOriginMm + (c + ky) * probe.ReconStepMm;

        var r = decoder.Decode(Shadow(cfg, sx, sy));
        var (gx, gy) = GridIndex(r, sx, sy);
        double max = r.Reconstruction.Raw.ToArray().Max();
        Assert.Equal(max, r.Reconstruction[gx, gy], 9);
        // Ties (a plateau where no pixel changes cell) may move the argmax, but never by half a resolution element.
        double d = cfg.Geometry.MaskDetectorDistanceMm, frac = d / (d + cfg.Geometry.SourceMaskDistanceMm);
        double err = Math.Sqrt(Math.Pow(r.Estimate.Position.X - sx, 2) + Math.Pow(r.Estimate.Position.Y - sy, 2));
        Assert.True(err < 0.5 * cfg.Mask.CellPitchMm / frac, $"estimate {err:F2} mm from the source");
    }

    [Fact]
    public void Decode_IsLinearInTheImage()
    {
        var cfg = Rig(cyclic: true);
        var decoder = new DefaultSimulationFactory().CreateDecoder(cfg)!;
        var a = Shadow(cfg, 3.1, -2.4);
        var b = Shadow(cfg, -8.0, 5.5);
        var sum = new DetectorImage(a.Width, a.Height);
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++) sum[x, y] = 2.5 * a[x, y] + b[x, y];

        var ra = decoder.Decode(a).Reconstruction;
        var rb = decoder.Decode(b).Reconstruction;
        var rs = decoder.Decode(sum).Reconstruction;
        for (int y = 0; y < rs.Height; y++)
            for (int x = 0; x < rs.Width; x++)
                Assert.Equal(2.5 * ra[x, y] + rb[x, y], rs[x, y], 9);
    }

    [Fact]
    public void Cyclic_RejectsAModestPedestal_ButNotAnUnlimitedOne()
    {
        // The textbook "balanced MURA pushes a flat pedestal into DC" holds only partly here: the 12-pixel array spans
        // ~1.07 projected mask periods, so a uniform image still decodes to a ±27-per-unit swing against a 66-high
        // source peak (theme 5 caveat; theme 28's mean-map bias at BSR 4–8). Pin both ends of that behaviour.
        var cfg = Rig(cyclic: true);
        var decoder = new DefaultSimulationFactory().CreateDecoder(cfg)!;
        var img = Shadow(cfg, 6.0, 4.0);
        double mean = img.Raw.ToArray().Average();
        DetectorImage Lift(double times)
        {
            var l = new DetectorImage(img.Width, img.Height);
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++) l[x, y] = img[x, y] + times * mean;
            return l;
        }
        var clean = decoder.Decode(img).Estimate.Position;

        Assert.Equal(clean, decoder.Decode(Lift(1.0)).Estimate.Position);          // pedestal = mean signal: unmoved
        var swamped = decoder.Decode(Lift(6.0)).Estimate.Position;                  // 6× the mean signal: moved
        double d = cfg.Geometry.MaskDetectorDistanceMm, frac = d / (d + cfg.Geometry.SourceMaskDistanceMm);
        Assert.True(Math.Abs(swamped.X - clean.X) + Math.Abs(swamped.Y - clean.Y) > 0.5 * cfg.Mask.CellPitchMm / frac,
            "a pedestal 6× the signal no longer moves the cyclic peak — if DC rejection was improved, update theme 5 / 28");
    }
}
