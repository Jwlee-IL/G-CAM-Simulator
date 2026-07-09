using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Decoding;
using Genoray.MonteCarlo.Masks;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One assumed source distance and how sharply the coded image focuses there.</summary>
public sealed record DepthRow(double AssumedSmm, double Focus);

/// <summary>Depth-from-focus result for one true source distance.</summary>
public sealed record DepthResult(double TrueSmm, double EstimatedSmm, double ErrorMm, DepthRow[] Curve);

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

        var rows = new List<DepthRow>();
        foreach (double s in assumedSmm)
        {
            var decoder = new CrossCorrelationDecoder(MuraGenerator.DecodingArray(cfg.Mask.Rank),
                                                      BuildGeometry(cfg, s));
            rows.Add(new DepthRow(s, Focus(decoder.Decode(img).Reconstruction)));
        }
        double est = CentroidEstimate(rows);
        return new DepthResult(trueSmm, est, est - trueSmm, rows.ToArray());
    }

    // Fixed small reconstruction grid (± the near-field half-period, same step) for every assumed S:
    // small enough to stay inside the smallest FCFOV (no aliasing) yet wide enough to hold the peak's
    // sidelobes, and a FIXED step so the focus metric carries no assumed-S grid-scale artifact.
    private static CodedApertureGeometry BuildGeometry(SimulationConfig cfg, double assumedSmm)
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
            ReconHalfExtentMm: 0.0,     // single candidate at the known on-axis source position
            ReconStepMm: 1.0,
            Cyclic: cfg.Decoder.Cyclic);
    }

    /// <summary>Focus = the correlation value at the (known, on-axis) source position — evaluated on a
    /// single-point grid so there is no candidate-position aliasing and no grid-scale artifact. When
    /// the assumed geometry matches the true one, the decoding array aligns with the coded shadow and
    /// this correlation is maximal; a wrong assumed S misaligns it and the value drops.</summary>
    private static double Focus(DetectorImage img)
    {
        double peak = double.NegativeInfinity;
        foreach (var v in img.Raw) if (v > peak) peak = v;
        return peak;
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
        return den > 0.0 ? num / den : rows[0].AssumedSmm;
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
}
