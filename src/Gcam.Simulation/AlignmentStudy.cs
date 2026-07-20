using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>One misalignment point: the localization bias/scatter it induces against an ideal decoder.</summary>
public sealed record AlignRow(
    string Dof,        // "offset_x" | "offset_z" | "roll_deg"
    double Magnitude,  // mm (offsets) or deg (roll)
    double BiasXMm,    // systematic decoded-minus-true offset
    double BiasYMm,
    double BiasMm,     // |mean estimate − true|
    double RmsMm);     // total localization error (bias + scatter)

/// <summary>
/// Studies how MASK–DETECTOR ALIGNMENT / POSE error limits a coded-aperture camera. The decoder back-projects
/// the IDEAL geometry (mask centred at its nominal plane, unrotated); a rigid-body misregistration of the real
/// mask therefore produces a SYSTEMATIC localization bias, not just scatter. Three dominant degrees of freedom
/// are swept independently, each with its own signature:
/// <list type="bullet">
/// <item><b>In-plane offset (x)</b> — the coded shadow shifts, biasing the decoded position almost uniformly.</item>
/// <item><b>Spacing error (z)</b> — the assumed magnification M=(D+S)/S is wrong → a radial scale/range bias that
/// grows with off-axis distance.</item>
/// <item><b>Roll (about z)</b> — the shadow is rotated → a bias that grows with off-axis distance (≈0 on-axis).</item>
/// </list>
/// An off-axis source is used so the z and roll leverage shows. Sweeping each DOF gives the alignment TOLERANCE
/// the mount must hold. (Pitch/yaw tilt is a distinct tilted-slab geometry, deferred.)
/// </summary>
public sealed class AlignmentStudy
{
    private readonly ISimulationFactory _factory;

    public AlignmentStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public AlignRow[] Run(SimulationConfig baseConfig, (double x, double y, double z) src,
                          double[] offsetsMm, double[] zOffsetsMm, double[] rollsDeg,
                          double photonBudget, int repeats)
    {
        var rows = new List<AlignRow>();
        foreach (double dx in offsetsMm)
            rows.Add(Measure(baseConfig, src, "offset_x", dx, m => m.MaskOffsetXMm = dx, photonBudget, repeats));
        foreach (double dz in zOffsetsMm)
            rows.Add(Measure(baseConfig, src, "offset_z", dz, m => m.MaskOffsetZMm = dz, photonBudget, repeats));
        foreach (double roll in rollsDeg)
            rows.Add(Measure(baseConfig, src, "roll_deg", roll, m => m.MaskRollDeg = roll, photonBudget, repeats));
        return rows.ToArray();
    }

    private AlignRow Measure(SimulationConfig baseConfig, (double x, double y, double z) src,
                             string dof, double magnitude, Action<MaskConfig> applyPose,
                             double photonBudget, int repeats)
    {
        var cfg = baseConfig.Clone();
        cfg.Source.Position = [src.x, src.y, src.z];
        applyPose(cfg.Mask);

        var mean = new SimulationRunner(_factory).Run(cfg);
        var img = mean.DetectorImage;
        double w = mean.DetectedWeight;
        if (w <= 0) return new AlignRow(dof, magnitude, double.NaN, double.NaN, double.NaN, double.NaN);
        double eff = mean.PhotonsEmitted > 0 ? w / mean.PhotonsEmitted : 0.0;
        double nDet = photonBudget * eff;
        double scale = nDet / w;

        var decoder = _factory.CreateDecoder(cfg)!;   // IDEAL geometry — unaware of the pose error
        var rng = _factory.CreateRandom(cfg);
        var work = new DetectorImage(img.Width, img.Height);

        double sumX = 0.0, sumY = 0.0, sumSq = 0.0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                    work[x, y] = Sampling.Poisson(rng, img[x, y] * scale);

            var est = decoder.Decode(work).Estimate.Position;
            sumX += est.X; sumY += est.Y;
            double ex = est.X - src.x, ey = est.Y - src.y;
            sumSq += ex * ex + ey * ey;
        }
        double biasX = sumX / repeats - src.x;
        double biasY = sumY / repeats - src.y;
        double bias = Math.Sqrt(biasX * biasX + biasY * biasY);
        double rms = Math.Sqrt(sumSq / repeats);
        return new AlignRow(dof, magnitude, biasX, biasY, bias, rms);
    }

    public static string ToCsv(AlignRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("dof,magnitude,bias_x_mm,bias_y_mm,bias_mm,rms_mm");
        foreach (var r in rows)
            sb.AppendLine($"{r.Dof},{r.Magnitude:F3},{r.BiasXMm:F3},{r.BiasYMm:F3},{r.BiasMm:F3},{r.RmsMm:F3}");
        return sb.ToString();
    }
}
