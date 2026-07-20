using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>Uncoded ambient-background helpers: an isotropic background carries no source direction, so on
/// the flood map it is (to first order) a UNIFORM pedestal. BSR here is a DETECTED background-to-source
/// ratio, so the mask's average transmission is already folded in — this is not an incident-flux predictor
/// (finite-mask partial coding, vignetting, and angular acceptance would add a slow spatial shape). Centralizes
/// what the antimask/shield studies used to inject ad hoc.</summary>
public static class Background
{
    /// <summary>Mean background counts per pixel for a given background-to-signal ratio at a fixed
    /// detected-source budget: BSR·sourceCounts spread uniformly over the array.</summary>
    public static double PedestalPerPixel(double bsr, double detectedSourceCounts, int pixels)
        => pixels > 0 && bsr > 0.0 ? bsr * detectedSourceCounts / pixels : 0.0;

    /// <summary>Add a uniform mean pedestal to every pixel of a (mean) flood map.</summary>
    public static void AddUniform(DetectorImage map, double meanPerPixel)
    {
        if (!(meanPerPixel > 0.0)) return;
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                map.Add(x, y, meanPerPixel);
    }

    /// <summary>Poisson-realize one pixel as (source mean + uncoded background pedestal) — the shared
    /// "signal + ambient" counting primitive behind every study that injects a background. Keeping it in
    /// one place means antimask (per-pixel, possibly graded), shield, and the background sweep all realize
    /// their noise identically.</summary>
    public static double RealizePixel(IRandom rng, double sourceMean, double pedestalPerPixel)
        => Sampling.Poisson(rng, sourceMean + pedestalPerPixel);

    /// <summary>Poisson-realize a whole noisy flood map from a source MEAN map scaled to a detected budget
    /// plus a uniform uncoded pedestal (row-major, so the RNG draw order matches a hand-rolled y,x loop).</summary>
    public static void Realize(DetectorImage dst, DetectorImage sourceMean, double sourceScale,
                               double pedestalPerPixel, IRandom rng)
    {
        for (int y = 0; y < dst.Height; y++)
            for (int x = 0; x < dst.Width; x++)
                dst[x, y] = RealizePixel(rng, sourceMean[x, y] * sourceScale, pedestalPerPixel);
    }

    /// <summary>A per-pixel multiplier (MEAN exactly 1) for background leaking through a 5-sided shield:
    /// <paramref name="sideFraction"/> of the leak enters through the 4 SIDE walls — edge-weighted, since a
    /// pixel near a wall sees more of that (finite) wall's diffuse leak (modelled ∝ Σ 1/(1+distanceToWall)) —
    /// and the rest through the REAR wall (uniform). The mean is normalized to 1 so the TOTAL leaked counts
    /// match the uniform model: this isolates the harm of the background's SPATIAL STRUCTURE from its level.
    /// <paramref name="sideFraction"/> 0 returns the flat uniform pedestal. Heuristic edge-weighting, not a
    /// solid-angle transport (that would be the full-MC option we deliberately skipped as not worth it).</summary>
    public static double[] SideLeakProfile(int width, int height, double sideFraction)
    {
        int n = width * height;
        var profile = new double[n];
        double f = Math.Clamp(sideFraction, 0.0, 1.0);
        if (f == 0.0) { Array.Fill(profile, 1.0); return profile; }

        var side = new double[n];
        double sum = 0.0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                double dl = x + 0.5, dr = (width - 0.5) - x;    // distance to left/right walls (pixels)
                double db = y + 0.5, dt = (height - 0.5) - y;   // distance to bottom/top walls
                double s = 1.0 / (1.0 + dl) + 1.0 / (1.0 + dr) + 1.0 / (1.0 + db) + 1.0 / (1.0 + dt);
                side[y * width + x] = s;
                sum += s;
            }
        double mean = sum / n;
        for (int i = 0; i < n; i++)
            profile[i] = f * (side[i] / mean) + (1.0 - f);      // mean = f·1 + (1-f)·1 = 1
        return profile;
    }

    /// <summary>A per-pixel multiplier (MEAN exactly 1) for a spatially GRADED diffuse background — a field
    /// that is stronger on one side of the array (a nearer contaminated wall, ground/sky asymmetry, scatter
    /// off an adjacent surface). Unlike a flat pedestal — which a cyclic MURA decode pushes to DC and rejects —
    /// a linear gradient carries a low-spatial-frequency component the decode does NOT fully reject, so it
    /// leaves a small localization BIAS (this is the one genuinely-diffuse case that survives DC rejection).
    /// The ramp runs along <paramref name="angleDeg"/> (0 = +x / rightward, 90 = +y / up) with a half-swing of
    /// <paramref name="contrast"/>: values run linearly to 1±contrast at the extreme corner pixel, mean
    /// normalized to exactly 1 so the TOTAL leaked counts match the uniform model — isolating the harm of the
    /// gradient's STRUCTURE from its level. <paramref name="contrast"/> 0 returns the flat uniform pedestal;
    /// clamped to [0, 0.999) so the pedestal stays non-negative. Like <see cref="SideLeakProfile"/> this is a
    /// heuristic spatial shape, not a transported flux.</summary>
    public static double[] GradientProfile(int width, int height, double angleDeg, double contrast)
    {
        int n = width * height;
        var profile = new double[n];
        double c = Math.Clamp(contrast, 0.0, 0.999);
        if (c == 0.0) { Array.Fill(profile, 1.0); return profile; }

        double rad = angleDeg * Math.PI / 180.0;
        double ux = Math.Cos(rad), uy = Math.Sin(rad);
        double cx = (width - 1) / 2.0, cy = (height - 1) / 2.0;   // grid centroid

        // Projection onto the ramp direction, centred on the centroid: its mean over the array is exactly 0,
        // so ANY scaling of it keeps the profile mean at 1. Normalize by the largest |projection| (a corner
        // pixel) so the swing is exactly ±contrast at the extremes.
        double maxAbs = 0.0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                double p = (x - cx) * ux + (y - cy) * uy;
                profile[y * width + x] = p;                       // stash raw projection
                double a = Math.Abs(p);
                if (a > maxAbs) maxAbs = a;
            }
        if (maxAbs == 0.0) { Array.Fill(profile, 1.0); return profile; }
        for (int i = 0; i < n; i++)
            profile[i] = 1.0 + c * (profile[i] / maxAbs);
        return profile;
    }
}

/// <summary>One background level: the uniform pedestal it puts on the flood map and what it does to
/// localization and decode contrast.</summary>
public sealed record BackgroundRow(
    double Bsr,             // background-to-signal ratio
    double BgPerPixel,      // uniform mean background counts/pixel
    double BiasMm,          // localization error of the high-statistics (mean-map) decode
    double RmsMm,           // localization RMS at a fixed acquisition (Poisson realizations)
    double FailRate,        // fraction of realizations beyond the fail threshold
    double PeakSnr);        // recon peak height / recon background std (decode contrast)

/// <summary>
/// How a diffuse ambient background degrades coded-aperture localization. The background is uncoded, so it
/// adds a uniform pedestal to the flood map; a CYCLIC MURA decode pushes the flat pedestal into the DC term
/// and rejects most of it, but its Poisson noise remains and eventually buries the coded peak. (Under
/// non-cyclic finite-mask decoding a uniform pedestal is not pure DC — it becomes a mild position-dependent
/// acceptance shape — so run this sweep with the cyclic decoder for the clean DC-rejection reading.) Sweeping
/// the background-to-signal ratio finds the collapse point — the SNR knee where localization fails.
/// </summary>
public sealed class BackgroundStudy
{
    /// <param name="gradientContrast">half-swing of a spatial gradient on the pedestal (0 = flat uniform,
    /// the unchanged model). A gradient is diffuse yet NOT flat, so the decode leaves a residual bias the
    /// uniform pedestal does not — compare the BiasMm column at contrast 0 vs &gt;0.</param>
    /// <param name="gradientAngleDeg">direction the gradient runs (0 = +x, 90 = +y).</param>
    public BackgroundRow[] RunSweep(SimulationConfig baseConfig, double[] bsrValues,
                                    double detectedBudget, int repeats, double failThrMm,
                                    double gradientContrast = 0.0, double gradientAngleDeg = 0.0)
    {
        var cfg = baseConfig.Clone();
        double tx = cfg.Source.Position[0], ty = cfg.Source.Position[1];

        // Source mean flood map (background OFF for the reference), scaled to the fixed detected budget.
        cfg.Background = null;
        var srcRes = new SimulationRunner(new DefaultSimulationFactory()).Run(cfg);
        var srcMap = srcRes.DetectorImage;
        double srcSum = Sum(srcMap);
        double scale = srcSum > 0 ? detectedBudget / srcSum : 0.0;

        var decoder = new DefaultSimulationFactory().CreateDecoder(cfg)!;
        int pixels = srcMap.Width * srcMap.Height;
        var rng = new DefaultRandom((cfg.Seed ?? 0) + 3210);

        // Spatial shape of the pedestal (mean 1): flat when gradientContrast==0 (identical to the old uniform
        // model), a linear ramp otherwise. Applied to BOTH the deterministic mean map (bias) and every Poisson
        // realization, so the gradient's low-frequency residual shows up in BiasMm while the total level is
        // unchanged (mean-1 profile).
        var profile = Background.GradientProfile(srcMap.Width, srcMap.Height, gradientAngleDeg, gradientContrast);

        var rows = new List<BackgroundRow>();
        foreach (double bsr in bsrValues)
        {
            double bgPerPixel = Background.PedestalPerPixel(bsr, detectedBudget, pixels);

            // Combined MEAN map = scaled source + pedestal·profile (deterministic, for the bias/contrast decode).
            var mean = new DetectorImage(srcMap.Width, srcMap.Height);
            for (int y = 0; y < srcMap.Height; y++)
                for (int x = 0; x < srcMap.Width; x++)
                    mean[x, y] = srcMap[x, y] * scale + bgPerPixel * profile[y * srcMap.Width + x];

            double bias = Dist(decoder.Decode(mean).Estimate, tx, ty);

            // Poisson realizations at this acquisition: background raises the mean floor, its shot noise
            // rides on top just like the source counts. Peak SNR is measured on the NOISY decodes and
            // averaged, so it is a genuine shot-noise contrast (not just the deterministic DC-leakage of
            // the mean map) — it and the RMS both reflect the background's counting noise.
            double sumSq = 0.0, snrSum = 0.0; int fails = 0;
            var noisy = new DetectorImage(srcMap.Width, srcMap.Height);
            for (int rep = 0; rep < repeats; rep++)
            {
                for (int y = 0; y < srcMap.Height; y++)
                    for (int x = 0; x < srcMap.Width; x++)
                        noisy[x, y] = Background.RealizePixel(rng, srcMap[x, y] * scale,
                                                             bgPerPixel * profile[y * srcMap.Width + x]);
                var dNoisy = decoder.Decode(noisy);
                double err = Dist(dNoisy.Estimate, tx, ty);
                sumSq += err * err;
                if (err > failThrMm) fails++;
                snrSum += PeakSnr(dNoisy.Reconstruction!);
            }
            rows.Add(new BackgroundRow(bsr, bgPerPixel, bias, Math.Sqrt(sumSq / repeats),
                                       (double)fails / repeats, snrSum / repeats));
        }
        return rows.ToArray();
    }

    // Decode contrast: the reconstruction peak relative to the spread of the (source-free) background of
    // the recon. A clean coded peak stands high above the recon floor; as ambient background grows the
    // floor's noise rises and the ratio collapses.
    private static double PeakSnr(DetectorImage recon)
    {
        double peak = double.MinValue, sum = 0.0, sumSq = 0.0;
        int n = recon.Width * recon.Height;
        foreach (double v in recon.Raw) { if (v > peak) peak = v; sum += v; sumSq += v * v; }
        double mean = sum / n;
        double var = Math.Max(0.0, sumSq / n - mean * mean);
        double std = Math.Sqrt(var);
        return std > 0 ? (peak - mean) / std : 0.0;
    }

    private static double Sum(DetectorImage img)
    {
        double s = 0.0;
        foreach (var v in img.Raw) s += v;
        return s;
    }

    private static double Dist(SourceEstimate e, double tx, double ty)
        => Math.Sqrt((e.Position.X - tx) * (e.Position.X - tx) + (e.Position.Y - ty) * (e.Position.Y - ty));

    public static string ToCsv(BackgroundRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("bsr,bg_per_pixel,bias_mm,rms_mm,fail_rate,peak_snr");
        foreach (var r in rows)
            sb.AppendLine($"{r.Bsr:F3},{r.BgPerPixel:F3},{r.BiasMm:F3},{r.RmsMm:F3},{r.FailRate:F3},{r.PeakSnr:F2}");
        return sb.ToString();
    }
}
