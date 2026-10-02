using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Masks;

namespace Gcam.Simulation;

/// <summary>One assumed source distance and how sharply the coded image focuses there.</summary>
public sealed record DepthRow(double AssumedSmm, double Focus);

/// <summary>Depth-from-focus result for one true source distance.</summary>
public sealed record DepthResult(double TrueSmm, double EstimatedSmm, double ErrorMm, DepthRow[] Curve);

/// <summary>Noisy joint (lateral + depth) estimation summary for one scenario/count budget.</summary>
public sealed record JointResult(
    string Name, double[] TruePos, double TrueSmm, double Counts,
    double DepthBiasMm, double DepthRmsMm, double LateralRmsMm);

/// <summary>
/// Source-distance (z) estimation by coded-aperture refocusing. The mask shadow's magnification
/// M = (D+S)/S depends on the source distance S, and the decoder back-projects with
/// frac = D/(D+S). Decoding one flood map at a range of ASSUMED S values, the correlation peak is
/// sharpest when the assumed S matches the true S (the coded shadow aligns) and defocuses otherwise
/// — so argmax(focus) over assumed S estimates the depth. Sensitivity ∝ dM/dS, so the near field
/// (small S) resolves depth well and the far field (M→1) poorly — the intrinsic coded-aperture limit.
/// </summary>
public sealed class DepthStudy
{
    private readonly ISimulationFactory _factory;

    public DepthStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public DepthResult Run(SimulationConfig baseConfig, double trueSmm, double[] assumedSmm)
    {
        var cfg = baseConfig.Clone();
        cfg.Geometry.SourceMaskDistanceMm = trueSmm;
        cfg.Source.Position = [0.0, 0.0, 0.0];        // on-axis: depth only, no FOV confusion
        var img = new SimulationRunner(_factory).Run(cfg).DetectorImage;

        var rows = DepthCurveAt(cfg, img, 0.0, 0.0, assumedSmm);
        double est = CentroidEstimate(rows);
        return new DepthResult(trueSmm, est, est - trueSmm, rows.ToArray());
    }

    /// <summary>
    /// Noisy JOINT (lateral + depth) estimation for an off-axis source: over <paramref name="repeats"/>
    /// Poisson realizations at <paramref name="counts"/> detected counts, estimate (x, y, S) each time
    /// and report the depth bias/RMS and lateral RMS. Estimation iterates: decode laterally at the
    /// current S → refine the depth focus at that lateral position → repeat (the magnification couples
    /// lateral and depth).
    /// </summary>
    public JointResult RunNoisyJoint(SimulationConfig baseConfig, string name, double sx, double sy,
                                     double trueSmm, double[] assumedSmm, double nominalSmm,
                                     double counts, int repeats)
    {
        var cfg = baseConfig.Clone();
        cfg.Geometry.SourceMaskDistanceMm = trueSmm;
        cfg.Source.Position = [sx, sy, 0.0];
        var mean = new SimulationRunner(_factory).Run(cfg).DetectorImage;
        double w = 0.0; foreach (var v in mean.Raw) w += v;
        double scale = w > 0 ? counts / w : 0.0;

        var rng = RealizationRandom.For(cfg);   // own stream: not the mean map's transport stream
        var noisy = new DetectorImage(mean.Width, mean.Height);
        double sumD = 0, sumDsq = 0, sumLsq = 0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < mean.Height; y++)
                for (int x = 0; x < mean.Width; x++)
                    noisy[x, y] = Sampling.Poisson(rng, mean[x, y] * scale);

            var (ex, ey, es) = EstimateJoint(cfg, noisy, assumedSmm, nominalSmm);
            double dErr = es - trueSmm;
            sumD += dErr; sumDsq += dErr * dErr;
            sumLsq += (ex - sx) * (ex - sx) + (ey - sy) * (ey - sy);
        }
        return new JointResult(name, [sx, sy], trueSmm, counts,
            sumD / repeats, Math.Sqrt(sumDsq / repeats), Math.Sqrt(sumLsq / repeats));
    }

    /// <summary>Depth-only estimation under Poisson noise for a KNOWN on-axis lateral position:
    /// the depth is scanned directly at (0,0) (no lateral coupling), so this isolates how counting
    /// noise — amplified by the broad far-field focus curve — limits the depth estimate.</summary>
    public JointResult RunNoisyDepth(SimulationConfig baseConfig, string name, double trueSmm,
                                     double[] assumedSmm, double counts, int repeats)
    {
        var cfg = baseConfig.Clone();
        cfg.Geometry.SourceMaskDistanceMm = trueSmm;
        cfg.Source.Position = [0.0, 0.0, 0.0];
        var mean = new SimulationRunner(_factory).Run(cfg).DetectorImage;
        double w = 0.0; foreach (var v in mean.Raw) w += v;
        double scale = w > 0 ? counts / w : 0.0;

        var rng = RealizationRandom.For(cfg);   // own stream: not the mean map's transport stream
        var noisy = new DetectorImage(mean.Width, mean.Height);
        double sumD = 0, sumDsq = 0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < mean.Height; y++)
                for (int x = 0; x < mean.Width; x++)
                    noisy[x, y] = Sampling.Poisson(rng, mean[x, y] * scale);
            double es = CentroidEstimate(DepthCurveAt(cfg, noisy, 0.0, 0.0, assumedSmm));
            double dErr = es - trueSmm;
            sumD += dErr; sumDsq += dErr * dErr;
        }
        return new JointResult(name, [0.0, 0.0], trueSmm, counts,
            sumD / repeats, Math.Sqrt(sumDsq / repeats), 0.0);
    }

    /// <summary>Noisy joint estimation using the FULL 3D (x, y, S) search (no alternating iteration).
    /// Same protocol as <see cref="RunNoisyJoint"/> so the two are directly comparable.</summary>
    public JointResult RunNoisyJoint3D(SimulationConfig baseConfig, string name, double sx, double sy,
                                       double trueSmm, double[] assumedSmm, double counts, int repeats)
    {
        var cfg = baseConfig.Clone();
        cfg.Geometry.SourceMaskDistanceMm = trueSmm;
        cfg.Source.Position = [sx, sy, 0.0];
        var mean = new SimulationRunner(_factory).Run(cfg).DetectorImage;
        double w = 0.0; foreach (var v in mean.Raw) w += v;
        double scale = w > 0 ? counts / w : 0.0;

        var rng = RealizationRandom.For(cfg);   // own stream: not the mean map's transport stream
        var noisy = new DetectorImage(mean.Width, mean.Height);
        double sumD = 0, sumDsq = 0, sumLsq = 0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < mean.Height; y++)
                for (int x = 0; x < mean.Width; x++)
                    noisy[x, y] = Sampling.Poisson(rng, mean[x, y] * scale);

            var (ex, ey, es) = EstimateJoint3D(cfg, noisy, assumedSmm);
            double dErr = es - trueSmm;
            sumD += dErr; sumDsq += dErr * dErr;
            sumLsq += (ex - sx) * (ex - sx) + (ey - sy) * (ey - sy);
        }
        return new JointResult(name, [sx, sy], trueSmm, counts,
            sumD / repeats, Math.Sqrt(sumDsq / repeats), Math.Sqrt(sumLsq / repeats));
    }

    /// <summary>
    /// Full 3D (x, y, S) joint estimate — replaces the alternating iteration and its start-point trap.
    /// For each assumed S, decode the flood over the lateral FCFOV grid and score that slice by its peak
    /// PROMINENCE z = (peak − grid mean)/grid std. NOTE: z is a HEURISTIC focus/detection score, not a
    /// calibrated GLRT/SNR — the grid mean/std include the peak and sidelobes, so it can carry a
    /// systematic bias across defocus (hence the far-field high-count bias). The depth is the
    /// plateau-robust centroid of the per-S z-profile; the lateral (x, y) is taken from the slice
    /// NEAREST that depth estimate (so (x, y, S) is self-consistent — same assumed-S plane). No
    /// nominal-S seed, no alternation.
    /// </summary>
    private (double x, double y, double s) EstimateJoint3D(SimulationConfig cfg, DetectorImage img,
                                                           double[] assumedSmm)
    {
        var g = MuraGenerator.DecodingArray(cfg.Mask.Rank);
        var profile = new List<DepthRow>(assumedSmm.Length);
        var lateral = new (double x, double y)[assumedSmm.Length];   // per-slice argmax position

        for (int si = 0; si < assumedSmm.Length; si++)
        {
            var dr = new CrossCorrelationDecoder(g, LateralGeometry(cfg, assumedSmm[si])).Decode(img);
            var recon = dr.Reconstruction;
            int n = recon.Width * recon.Height;
            double mean = 0.0; foreach (var v in recon.Raw) mean += v; mean /= n;
            double m2 = 0.0; foreach (var v in recon.Raw) m2 += (v - mean) * (v - mean);
            double std = Math.Sqrt(m2 / n) + 1e-9;

            double peak = double.NegativeInfinity; int px = 0, py = 0;
            for (int y = 0; y < recon.Height; y++)
                for (int x = 0; x < recon.Width; x++)
                    if (recon[x, y] > peak) { peak = recon[x, y]; px = x; py = y; }

            profile.Add(new DepthRow(assumedSmm[si], (peak - mean) / std));
            lateral[si] = (dr.ReconOriginMm + px * dr.ReconStepMm, dr.ReconOriginMm + py * dr.ReconStepMm);
        }

        double sEst = CentroidEstimate(profile);          // plateau-robust depth from the z-profile
        // Lateral from the slice closest to the depth estimate -> a self-consistent (x, y, S).
        int nearest = 0; double bestGap = double.PositiveInfinity;
        for (int si = 0; si < assumedSmm.Length; si++)
        {
            double gap = Math.Abs(assumedSmm[si] - sEst);
            if (gap < bestGap) { bestGap = gap; nearest = si; }
        }
        return (lateral[nearest].x, lateral[nearest].y, sEst);
    }

    /// <summary>Iterated joint estimate: alternate lateral decode (at the current depth) and depth
    /// focus (at the current lateral position) until they settle.</summary>
    private (double x, double y, double s) EstimateJoint(SimulationConfig cfg, DetectorImage img,
                                                         double[] assumedSmm, double nominalSmm)
    {
        double s = nominalSmm, x = 0.0, y = 0.0;
        for (int iter = 0; iter < 3; iter++)
        {
            var p = new CrossCorrelationDecoder(MuraGenerator.DecodingArray(cfg.Mask.Rank),
                                                LateralGeometry(cfg, s)).Decode(img).Estimate.Position;
            x = p.X; y = p.Y;
            s = CentroidEstimate(DepthCurveAt(cfg, img, x, y, assumedSmm));
        }
        return (x, y, s);
    }

    /// <summary>Focus curve at a fixed lateral position (sxp, syp): the single-point decoded
    /// correlation vs assumed distance — no grid, so no candidate-position aliasing.</summary>
    private static List<DepthRow> DepthCurveAt(SimulationConfig cfg, DetectorImage img,
                                               double sxp, double syp, double[] assumedSmm)
    {
        var g = MuraGenerator.DecodingArray(cfg.Mask.Rank);
        var rows = new List<DepthRow>(assumedSmm.Length);
        foreach (double s in assumedSmm)
        {
            var dec = new CrossCorrelationDecoder(g, PointGeometry(cfg, s));
            rows.Add(new DepthRow(s, dec.PointResponse(img, sxp, syp)));
        }
        return rows;
    }

    // Geometry for a single-point depth probe (the grid fields are unused by PointResponse).
    private static CodedApertureGeometry PointGeometry(SimulationConfig cfg, double assumedSmm)
        => Geometry(cfg, assumedSmm, halfExtent: 0.0, step: 1.0);

    // Geometry with an FCFOV-sized grid for the lateral decode at a given assumed distance.
    private static CodedApertureGeometry LateralGeometry(SimulationConfig cfg, double assumedSmm)
    {
        double d = cfg.Geometry.MaskDetectorDistanceMm;
        double period = cfg.Mask.Rank * cfg.Mask.CellPitchMm * (d + assumedSmm) / d;
        return Geometry(cfg, assumedSmm, halfExtent: period / 2.0, step: period / 48.0);
    }

    private static CodedApertureGeometry Geometry(SimulationConfig cfg, double assumedSmm,
                                                  double halfExtent, double step)
    {
        double d = cfg.Geometry.MaskDetectorDistanceMm;
        return new CodedApertureGeometry(
            Rank: cfg.Mask.Rank,
            MaskPlaneZ: d,
            MaskCellPitchMm: cfg.Mask.CellPitchMm,
            MaskCellsX: cfg.Mask.Rank * cfg.Mask.MosaicX,
            MaskCellsY: cfg.Mask.Rank * cfg.Mask.MosaicY,
            DetectorPlaneZ: 0.0,
            DetectorPitchMm: cfg.Detector.PixelPitchMm,
            SourcePlaneZ: d + assumedSmm,
            ReconHalfExtentMm: halfExtent,
            ReconStepMm: step,
            Cyclic: cfg.Decoder.Cyclic);
    }

    /// <summary>Depth estimate = the centroid of the high-focus region (weight = focus above a
    /// threshold). Robust to the flat top the focus curve develops when a band of distances is
    /// unresolvable — the plateau's width IS the depth resolution, and its centre is the best
    /// estimate (a bare argmax would snap to the plateau's leading edge).</summary>
    private static double CentroidEstimate(List<DepthRow> rows)
    {
        double max = double.NegativeInfinity, min = double.PositiveInfinity;
        foreach (var r in rows) { if (r.Focus > max) max = r.Focus; if (r.Focus < min) min = r.Focus; }
        double thr = min + 0.7 * (max - min);

        double num = 0.0, den = 0.0;
        foreach (var r in rows)
        {
            double w = r.Focus - thr;
            if (w > 0.0) { num += r.AssumedSmm * w; den += w; }
        }
        // Failure fallback (a flat/failed focus curve): return the scan midpoint (a neutral
        // "no depth information" answer), NOT rows[0], which would fabricate a confident near value.
        return den > 0.0 ? num / den : 0.5 * (rows[0].AssumedSmm + rows[^1].AssumedSmm);
    }

    public static string ToCsv(DepthResult[] results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("true_s_mm,assumed_s_mm,focus,estimated_s_mm,error_mm");
        foreach (var r in results)
            foreach (var row in r.Curve)
                sb.AppendLine($"{r.TrueSmm:F1},{row.AssumedSmm:F1},{row.Focus:F1},{r.EstimatedSmm:F2},{r.ErrorMm:F2}");
        return sb.ToString();
    }

    public static string JointToCsv(JointResult[] results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("scenario,true_x_mm,true_y_mm,true_s_mm,counts,depth_bias_mm,depth_rms_mm,lateral_rms_mm");
        foreach (var r in results)
            sb.AppendLine($"{r.Name},{r.TruePos[0]:F1},{r.TruePos[1]:F1},{r.TrueSmm:F1},{r.Counts:F0}," +
                          $"{r.DepthBiasMm:F2},{r.DepthRmsMm:F2},{r.LateralRmsMm:F3}");
        return sb.ToString();
    }
}
