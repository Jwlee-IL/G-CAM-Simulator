using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>One tungsten-thickness point of the optimization.</summary>
public sealed record ThicknessRow(
    double ThicknessMm,
    double UsableRadiusMm,   // largest radius localizable at the fixed budget
    double EffCenter,        // detection efficiency on-axis
    double EffEdge);         // detection efficiency at the largest tested radius

/// <summary>
/// Finds the optimal tungsten thickness. Thin masks leak (low contrast); thick masks
/// collimate the open channels (off-axis efficiency drops). Evaluated at a FIXED
/// source strength × time budget so both effects trade off honestly: for each
/// thickness we walk the source out radially, Poisson-realize the (biased) mean map at
/// the counts that budget yields, decode, and record how far out localization survives.
/// </summary>
public sealed class ThicknessStudy
{
    private readonly ISimulationFactory _factory;

    public ThicknessStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public ThicknessRow[] Run(
        SimulationConfig baseConfig,
        double[] thicknesses,
        double maxRadiusMm,
        int nRadial,
        double photonBudget,   // physical emitted photons = activity × branching × time
        int repeats,
        double failFraction)   // localizable if fraction(err > resolution) <= this
    {
        double d = baseConfig.Geometry.MaskDetectorDistanceMm;
        double s = baseConfig.Geometry.SourceMaskDistanceMm;
        double resolution = baseConfig.Mask.CellPitchMm * (d + s) / d;

        var rows = new List<ThicknessRow>();
        foreach (double thickness in thicknesses)
        {
            double usable = 0.0, effCenter = 0.0, effEdge = 0.0;
            bool stillGood = true;

            for (int i = 0; i < nRadial; i++)
            {
                double r = maxRadiusMm * i / (nRadial - 1);
                var cfg = Clone(baseConfig, thickness, r);

                var mean = new SimulationRunner(_factory).Run(cfg);
                double eff = mean.PhotonsEmitted > 0 ? mean.DetectedWeight / mean.PhotonsEmitted : 0.0;
                if (i == 0) effCenter = eff;
                effEdge = eff;

                double nDet = photonBudget * eff;
                double failRate = FailRate(cfg, mean, nDet, r, resolution, repeats);
                if (stillGood && failRate <= failFraction) usable = r;
                else stillGood = false;
            }

            rows.Add(new ThicknessRow(thickness, usable, effCenter, effEdge));
        }
        return rows.ToArray();
    }

    private double FailRate(SimulationConfig cfg, SimulationResult mean, double nDet,
                            double trueX, double resolution, int repeats)
    {
        var img = mean.DetectorImage;
        double w = mean.DetectedWeight;
        if (w <= 0) return 1.0;
        double scale = nDet / w;

        var decoder = _factory.CreateDecoder(cfg)!;
        var rng = RealizationRandom.For(cfg);   // own stream: not the mean map's transport stream
        var noisy = new DetectorImage(img.Width, img.Height);

        int fails = 0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                    noisy[x, y] = Sampling.Poisson(rng, img[x, y] * scale);

            var est = decoder.Decode(noisy).Estimate;
            double dx = est.Position.X - trueX;
            double dy = est.Position.Y;
            if (Math.Sqrt(dx * dx + dy * dy) > resolution) fails++;
        }
        return (double)fails / repeats;
    }

    private static SimulationConfig Clone(SimulationConfig b, double thickness, double sourceX)
    {
        var cfg = b.Clone();
        cfg.Mask.ThicknessMm = thickness;
        cfg.Source.Position = [sourceX, 0.0, 0.0];
        cfg.Source.DirectionalBiasing = true;
        return cfg;
    }

    public static string ToCsv(ThicknessRow[] rows, double resolution)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("thickness_mm,usable_radius_mm,eff_center,eff_edge,resolution_mm");
        foreach (var r in rows)
            sb.AppendLine($"{r.ThicknessMm:F1},{r.UsableRadiusMm:F2},{r.EffCenter:E3},{r.EffEdge:E3},{resolution:F2}");
        return sb.ToString();
    }
}
