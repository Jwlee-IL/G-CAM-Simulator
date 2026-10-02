using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>One detector-array granularity point.</summary>
public sealed record ArrayRow(
    double PixelPitchMm,
    int Pixels,                  // per side
    double SamplesPerCellShadow, // detector pixels spanning one mask-cell shadow
    double RmsMm,
    double FailRate);

/// <summary>
/// Sweeps the detector pixel pitch at a fixed physical size, evaluating localization at
/// a fixed detected-count budget. Finer pixels sample the coded shadow better (the mask
/// cell shadow should span ≳1 pixel) but split the same counts over more pixels (more
/// per-pixel Poisson noise) — so there is an optimal granularity.
/// </summary>
public sealed class ArrayStudy
{
    private readonly ISimulationFactory _factory;

    public ArrayStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public ArrayRow[] Run(SimulationConfig baseConfig, double physicalSizeMm, double[] pitches,
                          double photonBudget, int repeats, double failThresholdMm)
    {
        double dz = baseConfig.Geometry.MaskDetectorDistanceMm;
        double s = baseConfig.Geometry.SourceMaskDistanceMm;
        double cellShadow = baseConfig.Mask.CellPitchMm * (dz + s) / s; // mask-to-detector magnification

        var rows = new List<ArrayRow>();
        foreach (double pitch in pitches)
        {
            int pixels = (int)Math.Round(physicalSizeMm / pitch);
            var cfg = Clone(baseConfig, pixels, pitch);

            var mean = new SimulationRunner(_factory).Run(cfg);
            double w = mean.DetectedWeight;
            double nDet = photonBudget * (mean.PhotonsEmitted > 0 ? w / mean.PhotonsEmitted : 0.0);

            var (rms, fail) = Evaluate(cfg, mean, nDet, repeats, failThresholdMm);
            rows.Add(new ArrayRow(pitch, pixels, cellShadow / pitch, rms, fail));
        }
        return rows.ToArray();
    }

    private (double rms, double fail) Evaluate(SimulationConfig cfg, SimulationResult mean,
                                               double nDet, int repeats, double failThr)
    {
        var img = mean.DetectorImage;
        double w = mean.DetectedWeight;
        if (w <= 0) return (double.NaN, 1.0);
        double scale = nDet / w;

        var decoder = _factory.CreateDecoder(cfg)!;
        var rng = RealizationRandom.For(cfg);   // own stream: not the mean map's transport stream
        var noisy = new DetectorImage(img.Width, img.Height);
        double tx = cfg.Source.Position[0];
        double ty = cfg.Source.Position[1];

        double sumSq = 0.0;
        int fails = 0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                    noisy[x, y] = Sampling.Poisson(rng, img[x, y] * scale);

            var est = decoder.Decode(noisy).Estimate;
            double err = Math.Sqrt((est.Position.X - tx) * (est.Position.X - tx) +
                                   (est.Position.Y - ty) * (est.Position.Y - ty));
            sumSq += err * err;
            if (err > failThr) fails++;
        }
        return (Math.Sqrt(sumSq / repeats), (double)fails / repeats);
    }

    private static SimulationConfig Clone(SimulationConfig b, int pixels, double pitch)
    {
        var cfg = b.Clone();
        cfg.Detector.PixelsX = pixels;
        cfg.Detector.PixelsY = pixels;
        cfg.Detector.PixelPitchMm = pitch;
        return cfg;
    }

    public static string ToCsv(ArrayRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("pixel_pitch_mm,pixels,samples_per_cell_shadow,rms_mm,fail_rate");
        foreach (var r in rows)
            sb.AppendLine($"{r.PixelPitchMm:F2},{r.Pixels},{r.SamplesPerCellShadow:F2},{r.RmsMm:F3},{r.FailRate:F3}");
        return sb.ToString();
    }
}
