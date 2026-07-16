using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Detector;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One bad-pixel level: localization with the raw defective flood vs after bad-pixel repair.</summary>
public sealed record DefectRow(
    double BadPixelPct,   // total dead + hot pixels, as a % of the array
    double DeadPct,
    double HotPct,
    double RmsRawMm,      // ideal decode on the raw defective flood
    double RmsCorrMm);    // ideal decode after interpolating over the known bad-pixel map

/// <summary>
/// Studies how BAD (dead / hot) PIXELS degrade a coded-aperture localization and how a known bad-pixel map
/// recovers it. Dead pixels punch holes in the flood; hot pixels add source-independent spikes — both imprint a
/// fixed structure the ideal decoder's correlation mistakes for signal. The repair is the discrete analogue of
/// flood-field correction: interpolate every flagged pixel from its live neighbours. Each level is averaged over
/// several defect maps (seeds) so the result is the typical degradation, not one placement.
///
/// The MC geometry flood is run ONCE; every level/seed reuses it and stamps the defects analytically — the same
/// "flood once, apply per-pixel" approach as <see cref="UniformityStudy"/>.
/// </summary>
public sealed class DetectorDefectStudy
{
    private readonly ISimulationFactory _factory;

    public DetectorDefectStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public DefectRow[] Run(SimulationConfig baseConfig, double[] badPixelPct, double deadShare,
                           double hotFactor, double photonBudget, int repeats, int seeds = 4)
    {
        var mean = new SimulationRunner(_factory).Run(baseConfig);
        var img = mean.DetectorImage;
        int w = img.Width, h = img.Height;
        double detW = mean.DetectedWeight;
        double eff = mean.PhotonsEmitted > 0 ? detW / mean.PhotonsEmitted : 0.0;
        double nDet = photonBudget * eff;
        if (!(detW > 0.0)) return System.Array.Empty<DefectRow>();
        double scale = nDet / detW;

        // Base (defect-free) counts and the mean live-pixel level for the hot-pixel spike.
        double[] baseCounts = new double[w * h];
        double sum = 0.0; int nz = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double c = img[x, y] * scale;
                baseCounts[y * w + x] = c;
                if (c > 0) { sum += c; nz++; }
            }
        double meanLevel = nz > 0 ? sum / nz : 0.0;

        var rows = new List<DefectRow>();
        foreach (double pct in badPixelPct)
        {
            double deadFrac = pct / 100.0 * deadShare;
            double hotFrac = pct / 100.0 * (1.0 - deadShare);
            int nSeeds = pct <= 0.0 ? 1 : seeds;
            double sumRaw = 0.0, sumCorr = 0.0;
            double gotDead = 0, gotHot = 0;
            for (int s = 0; s < nSeeds; s++)
            {
                var defects = new DetectorDefects(w, h, deadFrac, hotFrac, hotFactor, seed: 200 + s);
                gotDead += defects.DeadCount; gotHot += defects.HotCount;
                var (raw, corr) = Evaluate(baseConfig, baseCounts, meanLevel, defects, repeats);
                sumRaw += raw; sumCorr += corr;
            }
            rows.Add(new DefectRow(pct, 100.0 * gotDead / nSeeds / (w * h), 100.0 * gotHot / nSeeds / (w * h),
                                   sumRaw / nSeeds, sumCorr / nSeeds));
        }
        return rows.ToArray();
    }

    private (double rmsRaw, double rmsCorr) Evaluate(SimulationConfig cfg, double[] baseCounts, double meanLevel,
                                                     DetectorDefects defects, int repeats)
    {
        int w = defects.Width, h = defects.Height;
        double hotAdd = defects.HotFactor * meanLevel;
        var decoder = _factory.CreateDecoder(cfg)!;   // IDEAL geometry
        var rng = _factory.CreateRandom(cfg);
        var raw = new DetectorImage(w, h);
        var corr = new DetectorImage(w, h);
        double tx = cfg.Source.Position[0], ty = cfg.Source.Position[1];

        double sumRaw = 0.0, sumCorr = 0.0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    double baseC = defects.Dead[i] ? 0.0 : baseCounts[i];
                    if (defects.Hot[i]) baseC += hotAdd;
                    raw[x, y] = Sampling.Poisson(rng, baseC);
                }

            // Repair: replace every flagged pixel with the mean of its non-defective 4-neighbours.
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (defects.Dead[i] || defects.Hot[i])
                        corr[x, y] = NeighbourMean(raw, defects, x, y, meanLevel);
                    else
                        corr[x, y] = raw[x, y];
                }

            sumRaw += Err2(decoder, raw, tx, ty);
            sumCorr += Err2(decoder, corr, tx, ty);
        }
        return (Math.Sqrt(sumRaw / repeats), Math.Sqrt(sumCorr / repeats));
    }

    private static double NeighbourMean(DetectorImage img, DetectorDefects d, int x, int y, double fallback)
    {
        double s = 0.0; int n = 0;
        void Add(int nx, int ny)
        {
            if (nx < 0 || ny < 0 || nx >= d.Width || ny >= d.Height) return;
            int j = ny * d.Width + nx;
            if (d.Dead[j] || d.Hot[j]) return;   // don't interpolate from another defect
            s += img[nx, ny]; n++;
        }
        Add(x - 1, y); Add(x + 1, y); Add(x, y - 1); Add(x, y + 1);
        return n > 0 ? s / n : fallback;
    }

    private static double Err2(IDecoder decoder, DetectorImage work, double tx, double ty)
    {
        var p = decoder.Decode(work).Estimate.Position;
        double ex = p.X - tx, ey = p.Y - ty;
        return ex * ex + ey * ey;
    }

    public static string ToCsv(DefectRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("bad_pixel_pct,dead_pct,hot_pct,rms_raw_mm,rms_corrected_mm");
        foreach (var r in rows)
            sb.AppendLine($"{r.BadPixelPct:F2},{r.DeadPct:F2},{r.HotPct:F2},{r.RmsRawMm:F3},{r.RmsCorrMm:F3}");
        return sb.ToString();
    }
}
