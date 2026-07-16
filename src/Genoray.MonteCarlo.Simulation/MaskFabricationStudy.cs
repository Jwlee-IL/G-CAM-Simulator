using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One fabrication-tolerance level (averaged over several manufactured masks): how a mis-machined
/// tungsten mask degrades imaging.</summary>
public sealed record MaskFabRow(
    double SigmaUm,        // machining tolerance scale (µm) that drives all the per-cell errors
    double PosJitterUm,    // in-plane hole placement σ
    double WanderUm,       // front→back drill wander σ
    double BlockedPct,     // fraction of open cells that failed to open
    double EfficiencyRel,  // detected counts relative to the σ=0 ideal mask (blocked + shrunken holes cut area)
    double RmsMm,          // localization error with the IDEAL decoder, averaged over masks × Poisson reps
    double Psr);           // decode peak-to-sidelobe ratio (peak−mean)/std of the reconstruction (coded fidelity)

/// <summary>
/// Studies how MASK FABRICATION TOLERANCES limit a coded-aperture camera. Tungsten is hard and brittle, so a
/// deployed thick fine-pitch mask is not the ideal MURA: holes are mis-placed and mis-sized, some fail to open,
/// and the high-aspect-ratio bore wanders through the slab. The decoder still assumes the ideal pattern, so the
/// mis-machined mask is a forward-model mismatch — the coded shadow no longer decodes cleanly.
///
/// A single conservative tolerance scale σ (µm) drives every error the way real machining quality does: hole
/// placement σ, size σ (0.7·σ), depth wander (2.5·σ — amplified by the aspect ratio through a thick slab), and a
/// blocked-cell rate that climbs as tolerance loosens. Each σ is AVERAGED over several manufactured masks (fab
/// seeds) so the result is the typical degradation, not one lucky/unlucky mask. Sweeping σ finds the USABILITY
/// THRESHOLD — the tolerance a real tungsten mask must actually hold, not the best-case a machine could hit once.
/// </summary>
public sealed class MaskFabricationStudy
{
    private readonly ISimulationFactory _factory;

    public MaskFabricationStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public MaskFabRow[] Run(SimulationConfig baseConfig, double[] sigmasUm,
                            double photonBudget, int repeats, int fabSeeds = 6)
    {
        var rows = new List<MaskFabRow>();
        double eff0 = double.NaN;
        foreach (double sigmaUm in sigmasUm)
        {
            double sumRms = 0.0, sumPsr = 0.0, sumEff = 0.0;
            int nSeeds = sigmaUm <= 0.0 ? 1 : fabSeeds;    // σ=0 is the deterministic ideal mask
            for (int s = 0; s < nSeeds; s++)
            {
                var cfg = Clone(baseConfig, sigmaUm, fabSeed: 100 + s);
                var mean = new SimulationRunner(_factory).Run(cfg);
                double eff = mean.PhotonsEmitted > 0 ? mean.DetectedWeight / mean.PhotonsEmitted : 0.0;
                double nDet = photonBudget * eff;
                var (rms, psr) = Evaluate(cfg, mean, nDet, repeats);
                sumRms += rms; sumPsr += psr; sumEff += eff;
            }
            double meanEff = sumEff / nSeeds;
            // Baseline for the relative efficiency is the IDEAL (σ=0) mask specifically, not merely the first row —
            // guard against a caller passing an unsorted sweep or omitting σ=0.
            if (sigmaUm <= 0.0 || double.IsNaN(eff0)) eff0 = meanEff;
            rows.Add(new MaskFabRow(sigmaUm, sigmaUm, 2.5 * sigmaUm, BlockedProb(sigmaUm) * 100.0,
                                    eff0 > 0 ? meanEff / eff0 : 0.0, sumRms / nSeeds, sumPsr / nSeeds));
        }
        return rows.ToArray();
    }

    // Conservative machining model: one tolerance scale drives placement, size, depth wander and blocked cells.
    private static double BlockedProb(double sigmaUm) => Math.Min(0.05, sigmaUm / 4000.0);

    private static SimulationConfig Clone(SimulationConfig b, double sigmaUm, int fabSeed)
    {
        var cfg = b.Clone();
        // A real drilled mask has a tungsten web (open fraction ρ≈0.5 ⇒ linear hole ≈0.71) — needed for the
        // placement/size errors to have web to move within.
        if (cfg.Mask.HoleFraction >= 1.0) cfg.Mask.HoleFraction = 0.71;
        double mm = sigmaUm / 1000.0;
        cfg.Mask.HolePositionJitterMm = mm;
        cfg.Mask.HoleSizeJitterMm = 0.7 * mm;
        cfg.Mask.HoleWanderMm = 2.5 * mm;                 // depth wander amplified by the aspect ratio
        cfg.Mask.BlockedCellProbability = BlockedProb(sigmaUm);
        cfg.Mask.FabricationSeed = fabSeed;
        return cfg;
    }

    private (double rms, double psr) Evaluate(SimulationConfig cfg, SimulationResult mean,
                                              double nDet, int repeats)
    {
        var img = mean.DetectorImage;
        double w = mean.DetectedWeight;
        if (w <= 0) return (double.NaN, double.NaN);
        double scale = nDet / w;

        var decoder = _factory.CreateDecoder(cfg)!;       // IDEAL decoder — assumes the perfect MURA
        var rng = _factory.CreateRandom(cfg);
        var work = new DetectorImage(img.Width, img.Height);
        double tx = cfg.Source.Position[0];
        double ty = cfg.Source.Position[1];

        double sumSq = 0.0, sumPsr = 0.0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                    work[x, y] = Sampling.Poisson(rng, img[x, y] * scale);

            var result = decoder.Decode(work);
            var est = result.Estimate;
            double err = Math.Sqrt((est.Position.X - tx) * (est.Position.X - tx) +
                                   (est.Position.Y - ty) * (est.Position.Y - ty));
            sumSq += err * err;
            sumPsr += PeakToSidelobe(result.Reconstruction);
        }
        return (Math.Sqrt(sumSq / repeats), sumPsr / repeats);
    }

    /// <summary>Peak-to-sidelobe ratio of the reconstruction: (peak − mean) / std over the background outside a
    /// small exclusion window around the peak. A clean coded shadow gives a high PSR; fabrication error blurs the
    /// shadow and raises the sidelobes, so PSR falls — a sensitive coded-fidelity metric.</summary>
    private static double PeakToSidelobe(DetectorImage recon)
    {
        int bx = 0, by = 0;
        double peak = double.NegativeInfinity;
        for (int y = 0; y < recon.Height; y++)
            for (int x = 0; x < recon.Width; x++)
                if (recon[x, y] > peak) { peak = recon[x, y]; bx = x; by = y; }

        const int excl = 3;
        double sum = 0.0, sum2 = 0.0; int n = 0;
        for (int y = 0; y < recon.Height; y++)
            for (int x = 0; x < recon.Width; x++)
                if (Math.Abs(x - bx) > excl || Math.Abs(y - by) > excl)
                {
                    double v = recon[x, y]; sum += v; sum2 += v * v; n++;
                }
        if (n == 0) return 0.0;
        double m = sum / n;
        double var = Math.Max(0.0, sum2 / n - m * m);
        double std = Math.Sqrt(var);
        return std > 1e-12 ? (peak - m) / std : 0.0;
    }

    public static string ToCsv(MaskFabRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("sigma_um,pos_jitter_um,wander_um,blocked_pct,efficiency_rel,rms_mm,psr");
        foreach (var r in rows)
            sb.AppendLine($"{r.SigmaUm:F0},{r.PosJitterUm:F0},{r.WanderUm:F0},{r.BlockedPct:F2}," +
                          $"{r.EfficiencyRel:F4},{r.RmsMm:F3},{r.Psr:F3}");
        return sb.ToString();
    }
}
