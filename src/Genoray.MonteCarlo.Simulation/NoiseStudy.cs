using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>Localization statistics at one detected-count level.</summary>
public sealed record NoisePoint(
    double DetectedCounts,
    double RmsErrorMm,
    double MeanErrorMm,
    double FailureRate);   // fraction of realizations with error > threshold (gross mislocalization)

public sealed class NoiseResult
{
    public required NoisePoint[] Points { get; init; }
    public required double Efficiency { get; init; }   // detected / emitted (geometric)
}

/// <summary>
/// Studies how localization degrades under counting (Poisson) noise. The biased MC
/// gives a low-noise estimate of the *expected* flood map; for each target detected-
/// count level we draw many Poisson realizations of that map, decode each, and measure
/// the spread of the position estimate. This isolates physical shot noise from MC
/// sampling noise.
/// </summary>
public sealed class NoiseStudy
{
    private readonly ISimulationFactory _factory;

    public NoiseStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public NoiseResult Run(SimulationConfig cfg, double[] countLevels, int repeats, double failThresholdMm)
    {
        // 1. Expected (mean) flood map from the clean biased MC.
        var mean = new SimulationRunner(_factory).Run(cfg);
        var img = mean.DetectorImage;
        double totalWeight = mean.DetectedWeight;
        double efficiency = mean.PhotonsEmitted > 0 ? totalWeight / mean.PhotonsEmitted : 0.0;

        double tx = cfg.Source.Position[0];
        double ty = cfg.Source.Position[1];
        var decoder = _factory.CreateDecoder(cfg)!;
        var rng = _factory.CreateRandom(cfg);
        var noisy = new DetectorImage(img.Width, img.Height);

        var points = new List<NoisePoint>();
        foreach (double nDet in countLevels)
        {
            double scale = totalWeight > 0 ? nDet / totalWeight : 0.0; // expected total counts = nDet
            double sumSq = 0.0, sumErr = 0.0;
            int fails = 0;

            for (int r = 0; r < repeats; r++)
            {
                for (int y = 0; y < img.Height; y++)
                    for (int x = 0; x < img.Width; x++)
                        noisy[x, y] = Sampling.Poisson(rng, img[x, y] * scale);

                var est = decoder.Decode(noisy).Estimate;
                double dx = est.Position.X - tx, dy = est.Position.Y - ty;
                double err = Math.Sqrt(dx * dx + dy * dy);
                sumSq += err * err;
                sumErr += err;
                if (err > failThresholdMm) fails++;
            }

            points.Add(new NoisePoint(nDet, Math.Sqrt(sumSq / repeats), sumErr / repeats, (double)fails / repeats));
        }

        return new NoiseResult { Points = points.ToArray(), Efficiency = efficiency };
    }

    /// <summary>Detected counts → equivalent acquisition time (s) at a given activity.</summary>
    public static double EquivalentTimeSeconds(double detectedCounts, double activityBq, double branching, double efficiency)
    {
        double rate = activityBq * branching * efficiency; // detected counts per second
        return rate > 0 ? detectedCounts / rate : double.NaN;
    }

    public static string ToCsv(string label, NoiseResult result, double activityBq, double branching)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("label,detected_counts,rms_error_mm,mean_error_mm,failure_rate,equiv_time_s");
        foreach (var p in result.Points)
        {
            double t = EquivalentTimeSeconds(p.DetectedCounts, activityBq, branching, result.Efficiency);
            sb.AppendLine($"{label},{p.DetectedCounts:F0},{p.RmsErrorMm:F3},{p.MeanErrorMm:F3},{p.FailureRate:F3},{t:F3}");
        }
        return sb.ToString();
    }
}
