using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

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
    public BackgroundRow[] RunSweep(SimulationConfig baseConfig, double[] bsrValues,
                                    double detectedBudget, int repeats, double failThrMm)
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

        var rows = new List<BackgroundRow>();
        foreach (double bsr in bsrValues)
        {
            double bgPerPixel = Background.PedestalPerPixel(bsr, detectedBudget, pixels);

            // Combined MEAN map = scaled source + uniform pedestal.
            var mean = new DetectorImage(srcMap.Width, srcMap.Height);
            for (int y = 0; y < srcMap.Height; y++)
                for (int x = 0; x < srcMap.Width; x++)
                    mean[x, y] = srcMap[x, y] * scale + bgPerPixel;

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
                        noisy[x, y] = Sampling.Poisson(rng, mean[x, y]);
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
