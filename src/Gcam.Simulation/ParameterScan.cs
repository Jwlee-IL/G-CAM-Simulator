using Gcam.Configuration;

namespace Gcam.Simulation;

/// <summary>One configuration evaluated by the scan.</summary>
public sealed record ScanRow(
    int Rank,
    double CellPitchMm,
    double DistanceMm,       // mask-detector distance D
    double SourceMm,         // source-mask distance S (fixed)
    double FcfovHalfMm,      // analytic fully-coded FOV half-width
    double ResolutionMm,     // source-plane resolution element = cellPitch * (D+S)/D
    double MedianErrorMm,
    double UsableFraction,   // fraction of swept points with error < resolution
    double UsableFovAreaMm2, // usableFraction * swept area
    double MedianEfficiency);// median geometric detection efficiency (N-independent)

/// <summary>
/// Sweeps a grid of (rank, cell pitch, mask-detector distance) configurations,
/// measuring the actually-usable field of view (where the decoder localizes to
/// within one resolution cell) rather than the unbounded analytic FCFOV.
/// </summary>
public sealed class ParameterScan
{
    private readonly ISimulationFactory _factory;

    public ParameterScan(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public ScanRow[] Run(
        SimulationConfig baseConfig,
        int[] ranks,
        double[] cellPitches,
        double[] distances,
        int sweepN,
        long photonsPerPoint)
    {
        double sourceMm = baseConfig.Geometry.SourceMaskDistanceMm;
        var rows = new List<ScanRow>();

        foreach (int rank in ranks)
            foreach (double pitch in cellPitches)
                foreach (double dist in distances)
                {
                    double fcfovHalf = rank * pitch * (dist + sourceMm) / dist / 2.0;
                    double resolution = pitch * (dist + sourceMm) / dist;

                    var cfg = baseConfig.Clone();
                    cfg.Name = $"r{rank}_p{pitch}_D{dist}";
                    cfg.Source.Position = [0.0, 0.0, 0.0];
                    cfg.Mask.Rank = rank;
                    cfg.Mask.CellPitchMm = pitch;
                    cfg.Geometry.MaskDetectorDistanceMm = dist;
                    cfg.Geometry.SourceMaskDistanceMm = sourceMm;
                    cfg.Decoder.Cyclic = true;
                    cfg.Decoder.ReconHalfExtentMm = null;   // auto one-FCFOV grid
                    cfg.Decoder.ReconStepMm = null;

                    // Sweep just inside the nominal FCFOV to gauge how much of it actually works.
                    double sweepHalf = 0.95 * fcfovHalf;
                    double step = 2.0 * sweepHalf / (sweepN - 1);
                    var sweep = new SourceSweep(_factory).Run(cfg, sweepHalf, step, photonsPerPoint);

                    var errs = sweep.Points.Select(p => p.ErrorMm).OrderBy(e => e).ToArray();
                    var effs = sweep.Points.Select(p => p.Efficiency).OrderBy(d => d).ToArray();
                    double medErr = Median(errs);
                    double medEff = Median(effs);
                    double usableFrac = sweep.Points.Count(p => p.ErrorMm < resolution) / (double)sweep.Points.Length;
                    double sweptArea = (2.0 * sweepHalf) * (2.0 * sweepHalf);

                    rows.Add(new ScanRow(
                        rank, pitch, dist, sourceMm, fcfovHalf, resolution,
                        medErr, usableFrac, usableFrac * sweptArea, medEff));
                }

        return rows.ToArray();
    }

    private static double Median(double[] sorted)
    {
        if (sorted.Length == 0) return 0;
        int m = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[m] : 0.5 * (sorted[m - 1] + sorted[m]);
    }

    public static string ToCsv(ScanRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("rank,cell_pitch_mm,dist_mm,source_mm,fcfov_half_mm,resolution_mm,median_error_mm,usable_fraction,usable_fov_area_mm2,median_efficiency");
        foreach (var r in rows)
            sb.AppendLine($"{r.Rank},{r.CellPitchMm:F2},{r.DistanceMm:F1},{r.SourceMm:F1},{r.FcfovHalfMm:F2},{r.ResolutionMm:F2},{r.MedianErrorMm:F3},{r.UsableFraction:F3},{r.UsableFovAreaMm2:F1},{r.MedianEfficiency:E3}");
        return sb.ToString();
    }
}
