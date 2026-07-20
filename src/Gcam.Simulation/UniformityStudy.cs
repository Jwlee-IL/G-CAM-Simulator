using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>One non-uniformity level: localization error with and without flood correction.</summary>
public sealed record UniformityRow(
    double Level,             // 0..N; drives both random-gain σ and the structured gradient
    double RmsRawMm,          // uncorrected
    double RmsCorrectedMm,    // after dividing by the calibrated sensitivity map
    double FailRaw,
    double FailCorrected);

/// <summary>
/// Studies how per-crystal non-uniformity (gain + energy-resolution spread) distorts
/// the flood map and biases localization, and shows that dividing the measured image
/// by the calibrated per-pixel sensitivity map (flood correction) recovers it.
/// </summary>
public sealed class UniformityStudy
{
    private readonly ISimulationFactory _factory;

    public UniformityStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public UniformityRow[] Run(SimulationConfig baseConfig, double[] levels,
                               double photonBudget, int repeats, double failThresholdMm)
    {
        var rows = new List<UniformityRow>();
        foreach (double level in levels)
        {
            var cfg = Clone(baseConfig, level);
            var sensitivity = new CrystalUniformity(cfg.Detector).Sensitivity;

            var mean = new SimulationRunner(_factory).Run(cfg);
            double eff = mean.PhotonsEmitted > 0 ? mean.DetectedWeight / mean.PhotonsEmitted : 0.0;
            double nDet = photonBudget * eff;

            var (rmsRaw, failRaw) = Evaluate(cfg, mean, nDet, repeats, failThresholdMm, null);
            var (rmsCorr, failCorr) = Evaluate(cfg, mean, nDet, repeats, failThresholdMm, sensitivity);

            rows.Add(new UniformityRow(level, rmsRaw, rmsCorr, failRaw, failCorr));
        }
        return rows.ToArray();
    }

    private (double rms, double fail) Evaluate(SimulationConfig cfg, SimulationResult mean,
                                               double nDet, int repeats, double failThr, double[]? correction)
    {
        var img = mean.DetectorImage;
        double w = mean.DetectedWeight;
        if (w <= 0) return (double.NaN, 1.0);
        double scale = nDet / w;

        var decoder = _factory.CreateDecoder(cfg)!;
        var rng = _factory.CreateRandom(cfg);
        var work = new DetectorImage(img.Width, img.Height);
        double tx = cfg.Source.Position[0];
        double ty = cfg.Source.Position[1];

        double sumSq = 0.0;
        int fails = 0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    double v = Sampling.Poisson(rng, img[x, y] * scale);
                    if (correction is not null)
                    {
                        double s = correction[y * img.Width + x];
                        v = s > 1e-6 ? v / s : v;   // flood correction
                    }
                    work[x, y] = v;
                }

            var est = decoder.Decode(work).Estimate;
            double err = Math.Sqrt((est.Position.X - tx) * (est.Position.X - tx) +
                                   (est.Position.Y - ty) * (est.Position.Y - ty));
            sumSq += err * err;
            if (err > failThr) fails++;
        }
        return (Math.Sqrt(sumSq / repeats), (double)fails / repeats);
    }

    private static SimulationConfig Clone(SimulationConfig b, double level)
    {
        var cfg = b.Clone();
        cfg.Detector.GainSigma = 0.40 * level;     // random (uncorrelated) component
        cfg.Detector.GainGradient = 0.60 * level;  // structured (gradient) component
        return cfg;
    }

    public static string ToCsv(UniformityRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("level,gain_sigma,gain_gradient,rms_raw_mm,rms_corrected_mm,fail_raw,fail_corrected");
        foreach (var r in rows)
            sb.AppendLine($"{r.Level:F3},{0.40 * r.Level:F3},{0.60 * r.Level:F3},{r.RmsRawMm:F3},{r.RmsCorrectedMm:F3},{r.FailRaw:F3},{r.FailCorrected:F3}");
        return sb.ToString();
    }
}
