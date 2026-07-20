using Gcam.Configuration;

namespace Gcam.Simulation;

/// <summary>One grid point of a source-position sweep.</summary>
public sealed record SweepPoint(
    double Sx, double Sy,
    double EstX, double EstY,
    double ErrorMm,
    double Confidence,
    double Efficiency);   // detected weight / emitted photons (geometric detection efficiency)

/// <summary>The full result of a 2D source-position sweep (row-major grid).</summary>
public sealed class SweepResult
{
    public required int N { get; init; }
    public required double MinMm { get; init; }
    public required double StepMm { get; init; }
    public required SweepPoint[] Points { get; init; }

    public SweepPoint At(int ix, int iy) => Points[iy * N + ix];
}

/// <summary>
/// Sweeps the source across a lateral grid, localizing each position, so we can
/// map where the decoder is accurate (fully-coded FOV) versus where it aliases
/// into a ghost. This is the automated version of the old "move the isotope
/// around by hand" experiment.
/// </summary>
public sealed class SourceSweep
{
    private readonly ISimulationFactory _factory;

    public SourceSweep(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public SweepResult Run(SimulationConfig baseConfig, double halfExtentMm, double stepMm, long photonsPerPoint)
    {
        int n = (int)Math.Round(2.0 * halfExtentMm / stepMm) + 1;
        double min = -halfExtentMm;
        var points = new SweepPoint[n * n];

        Parallel.For(0, n * n, k =>
        {
            int ix = k % n;
            int iy = k / n;
            double sx = min + ix * stepMm;
            double sy = min + iy * stepMm;

            // Deep-cloned per point (thread-safe); vary only the source position + seed.
            var cfg = baseConfig.Clone();
            cfg.PhotonCount = photonsPerPoint;
            cfg.Seed = baseConfig.Seed.HasValue ? baseConfig.Seed + k : null;
            cfg.Source.Position = [sx, sy, 0.0];

            var result = new SimulationRunner(_factory).Run(cfg);
            var est = result.Estimate!;
            double err = Math.Sqrt((est.Position.X - sx) * (est.Position.X - sx) +
                                   (est.Position.Y - sy) * (est.Position.Y - sy));

            double eff = result.PhotonsEmitted > 0 ? result.DetectedWeight / result.PhotonsEmitted : 0.0;
            points[k] = new SweepPoint(sx, sy, est.Position.X, est.Position.Y, err, est.Confidence, eff);
        });

        return new SweepResult { N = n, MinMm = min, StepMm = stepMm, Points = points };
    }

    /// <summary>Serializes a sweep result to CSV.</summary>
    public static string ToCsv(SweepResult sweep)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("sx_mm,sy_mm,est_x_mm,est_y_mm,error_mm,confidence,efficiency");
        foreach (var p in sweep.Points)
            sb.AppendLine($"{p.Sx:F3},{p.Sy:F3},{p.EstX:F3},{p.EstY:F3},{p.ErrorMm:F3},{p.Confidence:F4},{p.Efficiency:E3}");
        return sb.ToString();
    }
}
