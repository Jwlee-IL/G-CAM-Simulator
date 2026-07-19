using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Decoding;
using Genoray.MonteCarlo.Masks;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One two-source separation: how well each decoder resolves the pair (valley depth between the peaks).</summary>
public sealed record MlemRow(
    double SeparationMm,
    double CrossValleyDepth,   // 1 − dip/peak between the two sources for cross-correlation (higher = better split)
    double MlemValleyDepth,    // …for MLEM
    bool CrossResolved,
    bool MlemResolved);

/// <summary>Single-source comparison of the two reconstructions.</summary>
public sealed record MlemSingle(
    double CrossBiasMm, double MlemBiasMm,
    double CrossMinValue, double MlemMinValue,   // most-negative reconstruction value (cross-corr has sidelobes; MLEM ≥ 0)
    double CrossPeakFwhmMm, double MlemPeakFwhmMm);

/// <summary>
/// Compares MLEM (<see cref="MlemDecoder"/>) against cross-correlation (<see cref="CrossCorrelationDecoder"/>) on the
/// same coded floods — the audit's "likelihood vs peak-picking" reconstruction item. The headline is TWO-SOURCE
/// SEPARATION: cross-correlation convolves the two coded shadows into one blurred blob (with ± sidelobes) and its
/// argmath picks a single peak, while MLEM deconvolves the physical forward model into two non-negative peaks, so it
/// resolves pairs at a smaller separation. Also reports the single-source non-negativity (MLEM ≥ 0, cross-corr dips
/// negative) and peak sharpness. Self-contained: builds the floods and both decoders from the config geometry.
///
/// Caveats (this is an IDEAL high-count binary-aperture demonstration, not a general resolution limit): the floods
/// use the same binary pixel-centre forward model MLEM inverts, so there is no model mismatch; both decoders run in
/// the config's mode (default <c>Cyclic = true</c>), so the ghost-suppression benefit of the finite forward model is
/// a capability, not what this demo exercises. MLEM's sharper peak and closer resolving are iteration- and
/// count-dependent (early/late stopping trades resolution for noise). The <see cref="ValleyDepth"/> metric is
/// truth-centred, so the deepest-separation numbers (≈1.5 mm) are optimistic; the robust, test-backed claim is that
/// MLEM splits 2–3 mm pairs cross-correlation merges.
/// </summary>
public sealed class MlemStudy
{
    public (MlemRow[] rows, MlemSingle single, double[] crossProfile, double[] mlemProfile, double profileStepMm, double profileOriginMm)
        Run(SimulationConfig config, double[] separationsMm, double photonCount, int mlemIterations, int? seed = null,
            double profileSeparationMm = 3.0, double resolveThreshold = 0.25)
    {
        var m = config.Mask;
        var pattern = MuraGenerator.Mosaic(m.Rank, m.MosaicX, m.MosaicY);
        double pitch = m.CellPitchMm;
        double maskHalfW = pattern.Width * pitch / 2.0, maskHalfH = pattern.Height * pitch / 2.0;
        double maskZ = config.Geometry.MaskDetectorDistanceMm;
        double sourceZ = maskZ + config.Geometry.SourceMaskDistanceMm;
        int W = config.Detector.PixelsX, H = config.Detector.PixelsY;
        double detPitch = config.Detector.PixelPitchMm;
        double detHalfW = W * detPitch / 2.0, detHalfH = H * detPitch / 2.0;

        double frac = maskZ / sourceZ;
        double fcfovPeriod = m.Rank * pitch / frac;
        double half = config.Decoder.ReconHalfExtentMm ?? fcfovPeriod / 2.0;
        double step = config.Decoder.ReconStepMm ?? fcfovPeriod / 48.0;
        var geo = new CodedApertureGeometry(m.Rank, maskZ, pitch, pattern.Width, pattern.Height, 0.0, detPitch,
                                            sourceZ, half, step, config.Decoder.Cyclic);
        var cross = new CrossCorrelationDecoder(MuraGenerator.DecodingArray(m.Rank), geo);
        var mlem = new MlemDecoder(pattern, geo, mlemIterations);

        int seed0 = seed ?? config.Seed ?? 0;

        // --- single source at the origin: bias, negativity, sharpness ---
        var floodOne = TwoSourceFlood(pattern, pitch, maskHalfW, maskHalfH, maskZ, sourceZ,
                                      W, H, detPitch, detHalfW, detHalfH, 0.0, (long)photonCount, seed0 + 1);
        var rc = cross.Decode(floodOne);
        var rm = mlem.Decode(floodOne);
        var single = new MlemSingle(
            Dist(rc.Estimate.Position, 0, 0), Dist(rm.Estimate.Position, 0, 0),
            MinValue(rc.Reconstruction), MinValue(rm.Reconstruction),
            PeakFwhm(rc), PeakFwhm(rm));

        // --- two-source separation sweep ---
        var rows = new List<MlemRow>();
        double[] crossProfile = [], mlemProfile = [];
        double profStep = step, profOrigin = -half;
        foreach (double sep in separationsMm)
        {
            var flood = TwoSourceFlood(pattern, pitch, maskHalfW, maskHalfH, maskZ, sourceZ,
                                       W, H, detPitch, detHalfW, detHalfH, sep, (long)photonCount, seed0 + (int)Math.Round(sep * 100));
            var dc = cross.Decode(flood);
            var dm = mlem.Decode(flood);
            double cv = ValleyDepth(dc, sep);
            double mv = ValleyDepth(dm, sep);
            rows.Add(new MlemRow(sep, cv, mv, cv > resolveThreshold, mv > resolveThreshold));
            if (Math.Abs(sep - profileSeparationMm) < 1e-6)
            {
                crossProfile = CenterRow(dc);
                mlemProfile = CenterRow(dm);
            }
        }
        return (rows.ToArray(), single, crossProfile, mlemProfile, profStep, profOrigin);
    }

    // Build a coded flood from one or two point sources at (±sep/2, 0) on the source plane (sep=0 → single source).
    private static DetectorImage TwoSourceFlood(MaskPattern pattern, double pitch, double maskHalfW, double maskHalfH,
        double maskZ, double sourceZ, int W, int H, double detPitch, double detHalfW, double detHalfH,
        double sepMm, long photons, int seed)
    {
        var rng = new DefaultRandom(seed);
        var flood = new DetectorImage(W, H);
        double norm = (2.0 * detHalfW) * (2.0 * detHalfH) / (4.0 * Math.PI);
        for (long n = 0; n < photons; n++)
        {
            double sx = sepMm > 0 ? (rng.NextDouble() < 0.5 ? -sepMm / 2.0 : sepMm / 2.0) : 0.0;
            var src = new Vector3(sx, 0.0, sourceZ);
            double tx = (rng.NextDouble() * 2.0 - 1.0) * detHalfW;
            double ty = (rng.NextDouble() * 2.0 - 1.0) * detHalfH;
            var aim = new Vector3(tx, ty, 0.0);
            var delta = aim - src;
            double r = delta.Length;
            var d = delta * (1.0 / r);
            if (d.Z >= 0.0) continue;
            double weight = norm * Math.Abs(d.Z) / (r * r);
            var maskHit = src + d * ((maskZ - src.Z) / d.Z);
            double u = maskHit.X + maskHalfW, v = maskHit.Y + maskHalfH;
            if (u < 0.0 || v < 0.0 || u >= 2.0 * maskHalfW || v >= 2.0 * maskHalfH) continue;
            int cx = (int)(u / pitch), cy = (int)(v / pitch);
            if (cx < pattern.Width && cy < pattern.Height && pattern[cx, cy])
            {
                int ix = (int)((aim.X + detHalfW) / detPitch), iy = (int)((aim.Y + detHalfH) / detPitch);
                if (ix >= 0 && iy >= 0 && ix < W && iy < H) flood.Add(ix, iy, weight);
            }
        }
        return flood;
    }

    // Depth of the valley between the two source peaks along the central recon row: 1 − dip/mean(peaks). The peak
    // windows are TRUTH-centred (±2 cells), so below ~2 mm separation they can overlap and the metric is optimistic —
    // it is a comparative indicator, not a blind resolution measurement.
    private static double ValleyDepth(DecodeResult res, double sepMm)
    {
        var recon = res.Reconstruction;
        int gyc = (int)Math.Round((0.0 - res.ReconOriginMm) / res.ReconStepMm);
        gyc = Math.Clamp(gyc, 0, recon.Height - 1);
        int gL = (int)Math.Round((-sepMm / 2.0 - res.ReconOriginMm) / res.ReconStepMm);
        int gR = (int)Math.Round((sepMm / 2.0 - res.ReconOriginMm) / res.ReconStepMm);
        gL = Math.Clamp(gL, 0, recon.Width - 1); gR = Math.Clamp(gR, 0, recon.Width - 1);
        if (gR <= gL) return 0.0;
        // Peak on each side (search a small window around the expected positions), and the dip strictly between.
        double peakL = LocalMax(recon, gyc, gL, 2), peakR = LocalMax(recon, gyc, gR, 2);
        double dip = double.PositiveInfinity;
        for (int x = gL + 1; x < gR; x++) dip = Math.Min(dip, recon[x, gyc]);
        double meanPeak = 0.5 * (peakL + peakR);
        if (!(meanPeak > 0.0)) return 0.0;
        return 1.0 - dip / meanPeak;
    }

    private static double LocalMax(DetectorImage img, int y, int xc, int win)
    {
        double mx = double.NegativeInfinity;
        for (int x = Math.Max(0, xc - win); x <= Math.Min(img.Width - 1, xc + win); x++) mx = Math.Max(mx, img[x, y]);
        return mx;
    }

    private static double[] CenterRow(DecodeResult res)
    {
        var recon = res.Reconstruction;
        int gyc = Math.Clamp((int)Math.Round((0.0 - res.ReconOriginMm) / res.ReconStepMm), 0, recon.Height - 1);
        var row = new double[recon.Width];
        for (int x = 0; x < recon.Width; x++) row[x] = recon[x, gyc];
        return row;
    }

    private static double MinValue(DetectorImage img)
    {
        double mn = double.PositiveInfinity;
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++) mn = Math.Min(mn, img[x, y]);
        return mn;
    }

    private static double Dist(Vector3 p, double x, double y) => Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y));

    private static double PeakFwhm(DecodeResult res)
    {
        var recon = res.Reconstruction;
        int bx = 0, by = 0; double peak = double.NegativeInfinity;
        for (int y = 0; y < recon.Height; y++)
            for (int x = 0; x < recon.Width; x++)
                if (recon[x, y] > peak) { peak = recon[x, y]; bx = x; by = y; }
        var sorted = new List<double>(recon.Width);
        for (int x = 0; x < recon.Width; x++) sorted.Add(recon[x, by]);
        sorted.Sort();
        double baseline = sorted[sorted.Count / 2];
        double halfv = baseline + 0.5 * (peak - baseline);
        double left = double.NaN, right = double.NaN;
        for (int x = bx; x > 0; x--)
            if (recon[x, by] >= halfv && recon[x - 1, by] < halfv)
            { left = x - 1 + (halfv - recon[x - 1, by]) / (recon[x, by] - recon[x - 1, by]); break; }
        for (int x = bx; x < recon.Width - 1; x++)
            if (recon[x, by] >= halfv && recon[x + 1, by] < halfv)
            { right = x + (recon[x, by] - halfv) / (recon[x, by] - recon[x + 1, by]); break; }
        return (right - left) * res.ReconStepMm;
    }

    public static string ToCsv(MlemRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("separation_mm,cross_valley_depth,mlem_valley_depth,cross_resolved,mlem_resolved");
        foreach (var r in rows)
            sb.AppendLine($"{r.SeparationMm:F2},{r.CrossValleyDepth:F4},{r.MlemValleyDepth:F4},{(r.CrossResolved ? 1 : 0)},{(r.MlemResolved ? 1 : 0)}");
        return sb.ToString();
    }
}
