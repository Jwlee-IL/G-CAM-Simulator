using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Detector;
using Genoray.MonteCarlo.Masks;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>The mask-secondary background produced by a mono-energetic primary hitting the coded mask.</summary>
public sealed record MaskSecondaryResult(
    double PrimaryEnergyKeV,
    double OpenFraction,
    double SecondaryPerPrimaryPct,   // escaped mask secondaries as % of the open-cell (coded) primary flux
    double FluorPct,                 // K-fluorescence share of the escaped secondaries (rest = Compton scatter)
    double[] BinCenters,
    double[] SecondaryHist);         // arriving-energy spectrum of the escaped secondaries (counts per bin)

/// <summary>
/// Models the SECONDARY photons a real tungsten mask emits — the physics a pure-attenuation mask omits. A primary
/// that hits a closed cell interacts in the tungsten (<see cref="MaskSecondary"/>) and can send a Compton-scattered
/// gamma or a W K-fluorescence X-ray (59/67 keV) toward the detector. This is a focused slab MC (it does not touch
/// the main coded pipeline's Transmit): sample an interaction depth in the slab, decide the secondary, then require
/// it to head to the detector and survive the escape self-absorption. The output is the arriving-energy spectrum of
/// the mask secondaries, normalized to the open-cell primary flux — a low-energy scatter background plus a
/// (heavily self-absorbed, hence small) W X-ray line pair. "Heads to the detector" is a back-face HEMISPHERE tally
/// (dir.Z &lt; 0), not a finite-detector solid-angle cut, so the reported fraction is an upper bound.
/// </summary>
public sealed class MaskSecondaryStudy
{
    public MaskSecondaryResult Run(SimulationConfig config, double primaryEnergyKeV,
                                   int samples, int bins, double maxEnergyKeV, int seed = 20260717)
    {
        var m = config.Mask;
        var pattern = MuraGenerator.Mosaic(m.Rank, m.MosaicX, m.MosaicY);
        int open = 0, cells = pattern.Width * pattern.Height;
        for (int y = 0; y < pattern.Height; y++)
            for (int x = 0; x < pattern.Width; x++)
                if (pattern[x, y]) open++;
        double openFrac = cells > 0 ? (double)open / cells : 0.5;

        double t = m.ThicknessMm;
        double mu662 = m.LinearAttenuationPerMm;
        double mu0 = mu662 * MaskSecondary.MuRel(primaryEnergyKeV);   // primary attenuation in W
        double interactProb = 1.0 - Math.Exp(-mu0 * t);

        var sec = new MaskSecondary();
        var rng = new DefaultRandom(seed);
        var down = new Vector3(0.0, 0.0, -1.0);
        var hist = new double[bins];
        double bw = maxEnergyKeV / bins;

        long primary = 0, escaped = 0, fluor = 0;
        for (int i = 0; i < samples; i++)
        {
            if (rng.NextDouble() < openFrac) { primary++; continue; }   // open cell → coded primary to the detector

            // Closed cell: does the primary interact in the tungsten (vs leak straight through)?
            if (rng.NextDouble() >= interactProb) continue;             // leaked through — not a secondary

            // Interaction depth from the front face, ∝ exp(-μ0·s) truncated to [0, t].
            double u = rng.NextDouble();
            double depth = -Math.Log(1.0 - u * interactProb) / mu0;

            var (produced, eSec, dir, isFluor) = sec.Interact(primaryEnergyKeV, down, rng);
            if (!produced || dir.Z >= 0.0) continue;                    // absorbed, or not heading to the detector

            double pathOut = (t - depth) / Math.Abs(dir.Z);            // distance to exit the back face
            double escape = Math.Exp(-mu662 * MaskSecondary.MuRel(eSec) * pathOut);
            if (rng.NextDouble() >= escape) continue;                   // self-absorbed in the mask

            escaped++;
            if (isFluor) fluor++;
            int b = (int)(eSec / bw);
            if (b >= 0 && b < bins) hist[b] += 1.0;
        }

        var ctr = new double[bins];
        for (int i = 0; i < bins; i++) ctr[i] = (i + 0.5) * bw;
        double secPerPrimary = primary > 0 ? 100.0 * escaped / primary : 0.0;
        double fluorPct = escaped > 0 ? 100.0 * fluor / escaped : 0.0;
        return new MaskSecondaryResult(primaryEnergyKeV, openFrac, secPerPrimary, fluorPct, ctr, hist);
    }

    public static string ToCsv(MaskSecondaryResult r)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# mask secondary  primary={r.PrimaryEnergyKeV:F1}keV  open_frac={r.OpenFraction:F3}  " +
                      $"secondary_per_primary_pct={r.SecondaryPerPrimaryPct:F3}  fluor_pct={r.FluorPct:F1}");
        sb.AppendLine("energy_keV,secondary_counts");
        for (int i = 0; i < r.BinCenters.Length; i++)
            sb.AppendLine($"{r.BinCenters[i]:F1},{r.SecondaryHist[i]:F0}");
        return sb.ToString();
    }
}
