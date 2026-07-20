using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Masks;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One (off-axis position × crystal thickness): the reconstruction blur DOI parallax adds, isolated by
/// comparing the SAME rays with the DOI depth-displacement on vs off.</summary>
public sealed record DoiRow(
    double SourceXMm,
    double CrystalThicknessMm,
    double DoiShiftMm,             // systematic localization shift the DOI adds (est_doi − est_noDoi) — the clean metric
    double ReconPeakFwhmNoDoiMm,   // reference peak FWHM (deposit at the front-face crossing)
    double ReconPeakFwhmDoiMm);    // peak FWHM with the DOI depth displacement

/// <summary>
/// DEPTH-OF-INTERACTION (DOI) parallax as the reconstruction sees it — the decoder's pixel-area / pixel-CENTRE
/// forward model vs the real detector response (audit item F). A gamma enters the crystal front face but interacts at
/// a random DEPTH; for an OBLIQUE ray (an off-axis source) the interaction is laterally displaced by
/// depth·tan(incidence), so the scintillation centroid the detector reads is NOT where the ray crossed the front face
/// the decoder back-projects. On-axis (normal incidence) this vanishes; off-axis it smears the coded shadow by
/// ≈ thickness·tan(angle), broadening (and slightly shifting) the reconstruction peak — a resolution floor the
/// centre-back-projecting decoder (and the sub-cell interpolation of theme 43) cannot correct. This study builds the
/// flood with the real DOI-displaced deposit position and measures the peak FWHM vs off-axis position and crystal
/// thickness. Self-contained.
/// </summary>
public sealed class DoiParallaxStudy
{
    private readonly double _muAt662;

    public DoiParallaxStudy(double muAt662PerMm = 0.09) { _muAt662 = muAt662PerMm; }

    public DoiRow[] Run(SimulationConfig config, double[] sourceXsMm, double[] thicknessesMm, long photonCount, int? seed = null)
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
        var decoder = new DefaultSimulationFactory().CreateDecoder(config)!;

        var rows = new List<DoiRow>();
        foreach (double thick in thicknessesMm)
            foreach (double sx in sourceXsMm)
            {
                var rng = new DefaultRandom((seed ?? config.Seed ?? 0) + (int)Math.Round(sx * 10 + thick * 1000));
                var floodNo = new DetectorImage(W, H);   // deposit at the front-face crossing (no DOI)
                var floodDoi = new DetectorImage(W, H);   // deposit at the DOI-displaced interaction point
                var src = new Vector3(sx, 0.0, sourceZ);
                for (long n = 0; n < photonCount; n++)
                {
                    double tx = (rng.NextDouble() * 2.0 - 1.0) * detHalfW;
                    double ty = (rng.NextDouble() * 2.0 - 1.0) * detHalfH;
                    var aim = new Vector3(tx, ty, 0.0);
                    var delta = aim - src;
                    double r = delta.Length;
                    var d = delta * (1.0 / r);
                    if (d.Z >= 0.0) continue;
                    var maskHit = src + d * ((maskZ - src.Z) / d.Z);
                    double u = maskHit.X + maskHalfW, v = maskHit.Y + maskHalfH;
                    if (u < 0.0 || v < 0.0 || u >= 2.0 * maskHalfW || v >= 2.0 * maskHalfH) continue;
                    int cx = (int)(u / pitch), cy = (int)(v / pitch);
                    if (!(cx < pattern.Width && cy < pattern.Height && pattern[cx, cy])) continue;

                    // Interaction depth in the crystal (front face z=0, sampled ~exp(-μ·s) truncated to [0,thick]).
                    double interactProb = 1.0 - Math.Exp(-_muAt662 * thick / Math.Abs(d.Z));
                    if (interactProb <= 0.0 || rng.NextDouble() >= interactProb) continue;
                    double pathIn = -Math.Log(1.0 - rng.NextDouble() * interactProb) / _muAt662;
                    double depthZ = pathIn * Math.Abs(d.Z);
                    // No-DOI reference: the detector reads the front-face crossing (what the decoder assumes).
                    Deposit(floodNo, aim.X, aim.Y, detHalfW, detHalfH, detPitch, W, H);
                    // DOI: the scintillation centroid is the interaction point, displaced along the ray by depth·tan(θ).
                    Deposit(floodDoi, aim.X + d.X / Math.Abs(d.Z) * depthZ, aim.Y + d.Y / Math.Abs(d.Z) * depthZ,
                            detHalfW, detHalfH, detPitch, W, H);
                }

                var resNo = decoder.Decode(floodNo);
                var resDoi = decoder.Decode(floodDoi);
                double shift = resDoi.Estimate.Position.X - resNo.Estimate.Position.X;
                rows.Add(new DoiRow(sx, thick, shift, PeakFwhmMm(resNo), PeakFwhmMm(resDoi)));
            }
        return rows.ToArray();
    }

    private static void Deposit(DetectorImage img, double x, double y, double halfW, double halfH, double pitch, int W, int H)
    {
        int ix = (int)((x + halfW) / pitch), iy = (int)((y + halfH) / pitch);
        if (ix >= 0 && iy >= 0 && ix < W && iy < H) img.Add(ix, iy, 1.0);
    }

    private static double PeakFwhmMm(Genoray.MonteCarlo.Core.DecodeResult res)
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
        double half = baseline + 0.5 * (peak - baseline);
        double left = double.NaN, right = double.NaN;
        for (int x = bx; x > 0; x--)
            if (recon[x, by] >= half && recon[x - 1, by] < half)
            { left = x - 1 + (half - recon[x - 1, by]) / (recon[x, by] - recon[x - 1, by]); break; }
        for (int x = bx; x < recon.Width - 1; x++)
            if (recon[x, by] >= half && recon[x + 1, by] < half)
            { right = x + (recon[x, by] - half) / (recon[x, by] - recon[x + 1, by]); break; }
        return (right - left) * res.ReconStepMm;
    }

    public static string ToCsv(DoiRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("source_x_mm,thickness_mm,doi_shift_mm,fwhm_nodoi_mm,fwhm_doi_mm");
        foreach (var r in rows)
            sb.AppendLine($"{r.SourceXMm:F2},{r.CrystalThicknessMm:F1},{r.DoiShiftMm:F4},{r.ReconPeakFwhmNoDoiMm:F4},{r.ReconPeakFwhmDoiMm:F4}");
        return sb.ToString();
    }
}
