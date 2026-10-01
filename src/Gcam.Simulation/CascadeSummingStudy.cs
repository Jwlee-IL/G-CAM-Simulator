using System.Linq;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>One source distance: the single-line photopeak vs cascade-SUM yields (per decay) and their ratio.</summary>
public sealed record CascadeRow(
    double SourceDistanceMm,
    double SinglePhotopeakPerDecay,  // decays landing in a single-line photopeak window (∝ ε)
    double SumPeakPerDecay,          // decays landing in a coincidence-SUM window (∝ ε² for a true cascade)
    double SumToSingleRatio);        // SUM/single ∝ ε (geometric)

/// <summary>
/// Studies TRUE (cascade) COINCIDENCE SUMMING: two gammas from ONE decay both depositing in the crystal, so their
/// energies SUM into one recorded event — a SUM peak (Co-60 1173+1332 → 2505) plus a loss of counts from the single
/// photopeaks (summing-out). Unlike random coincidence / pile-up (theme 37, between DIFFERENT decays, ∝ rate), this
/// is RATE-INDEPENDENT and ∝ ε²: it is a geometric effect of the detector solid angle, so it grows as the source
/// nears the camera. The emission comes from <see cref="DecayScheme"/> (correlated per-decay gammas with angular
/// correlation), and each gamma is transported through the crystal with the SAME interaction physics the main
/// detector uses (<see cref="ComptonModel"/>: μ(E), photoelectric-vs-Compton, Klein-Nishina), so the sum peak and
/// its continuum come from real energy deposition, not a hand-added line.
///
/// Sweeping the source distance changes ε; the SUM-peak yield scales as the SQUARE of the single-photopeak yield
/// (slope 2 on log-log), confirming the ∝ ε² GEOMETRIC scaling. (Note: a distance sweep alone does NOT separate
/// cascade from random coincidence — random pile-up also scales ∝ ε² in geometry; the true discriminator is that
/// cascade summing is rate/activity-INDEPENDENT per decay while random coincidence grows with rate. Here the two
/// gammas are same-decay by construction, so this IS cascade summing.) Cs-137 (single line) gives ≈ 0 summing;
/// Na-22's back-to-back 511s can't both reach a one-sided detector, so its 511+511 sum is suppressed and 511+1275
/// dominates.
/// </summary>
public sealed class CascadeSummingStudy
{
    private readonly double _detHalfW, _detHalfH, _openFraction, _muAt662, _depthMm, _fwhmAt662;
    private readonly CrystalMaterial _material;

    public CascadeSummingStudy(double detHalfWidthMm, double detHalfHeightMm, double maskOpenFraction,
                               double? muAt662PerMm = null, double crystalDepthMm = 10.0, double fwhmAt662 = 0.06,
                               CrystalMaterial? material = null)
    {
        _detHalfW = detHalfWidthMm;
        _detHalfH = detHalfHeightMm;
        _openFraction = maskOpenFraction;
        _material = material ?? CrystalMaterial.Gagg;
        _muAt662 = muAt662PerMm ?? _material.MuPerMm(CrystalMaterial.ReferenceKeV);
        _depthMm = crystalDepthMm;
        _fwhmAt662 = fwhmAt662;
    }

    /// <summary>Run the distance sweep. Returns the per-distance rows plus one spectrum (per-decay total deposited
    /// energy, energy-smeared) taken at <paramref name="spectrumDistanceMm"/> for plotting the sum peak.</summary>
    public (CascadeRow[] rows, double[] spectrum, double specBinKeV, double specMaxKeV) Run(
        DecayScheme scheme, double[] sourceDistancesMm, long decays, int? seed,
        double spectrumDistanceMm, int specBins = 300, double specMaxKeV = 2800.0)
    {
        var rows = new List<CascadeRow>();
        double[] spectrum = new double[specBins];
        double specBinKeV = specMaxKeV / specBins;

        // A SUM peak only proves true coincidence if it lies ABOVE the highest single line — otherwise a single
        // photon's Compton continuum (up to its Compton edge) can leak into the window as a ∝ε single-photon event,
        // not a ∝ε² coincidence (e.g. Na-22's 1022 keV 511+511 window sits on the 1275 keV Compton edge). Restrict
        // the coincidence metric to sum peaks above max(single line).
        double maxSingle = scheme.SingleLinesKeV.Length > 0 ? scheme.SingleLinesKeV.Max() : 0.0;
        var cleanSums = scheme.SumPeaks.Where(s => s.energyKeV > maxSingle + 1.0).ToArray();

        foreach (double dist in sourceDistancesMm)
        {
            var rng = new DefaultRandom((seed ?? 0) + (int)Math.Round(dist));
            bool snapshot = Math.Abs(dist - spectrumDistanceMm) < 1e-6;
            long singleHits = 0, sumHits = 0;
            var photons = new List<(double energyKeV, Vector3 dir)>(4);

            for (long n = 0; n < decays; n++)
            {
                scheme.Sample(rng, photons);
                double totalDep = 0.0;
                foreach (var (energyKeV, dir) in photons)
                {
                    if (dir.Z >= 0.0) continue;                       // heading away from the detector plane
                    double t = dist / (-dir.Z);
                    double lx = t * dir.X, ly = t * dir.Y;
                    if (Math.Abs(lx) > _detHalfW || Math.Abs(ly) > _detHalfH) continue;  // misses the detector
                    if (rng.NextDouble() > _openFraction) continue;   // hit a closed mask cell → absorbed in the mask
                    totalDep += CrystalDeposit(energyKeV, rng);       // energy this gamma leaves in the crystal
                }
                if (totalDep <= 0.0) continue;

                double measured = Smear(totalDep, rng);
                if (snapshot)
                {
                    int b = (int)(measured / specBinKeV);
                    if (b >= 0 && b < specBins) spectrum[b] += 1.0;
                }
                if (InAnyWindow(measured, scheme.SingleLinesKeV)) singleHits++;
                if (InAnySumWindow(measured, cleanSums)) sumHits++;
            }

            double single = (double)singleHits / decays;
            double sum = (double)sumHits / decays;
            rows.Add(new CascadeRow(dist, single, sum, single > 0 ? sum / single : 0.0));
        }
        return (rows.ToArray(), spectrum, specBinKeV, specMaxKeV);
    }

    /// <summary>Total energy one gamma deposits in the crystal slab (front face z=0, back z=−depth). Mirrors the main
    /// detector's cascade physics (sample a path, photoelectric → full absorb, else Compton scatter and continue;
    /// 0 if it escapes a face) using the shared <see cref="ComptonModel"/>, but always enters at NORMAL incidence from
    /// the slab centre — so absolute efficiency is approximate (real incident angle, lateral entry point, side escape,
    /// backing and parallax are omitted). The sum-peak POSITION and the ∝ ε² scaling do not depend on that.</summary>
    private double CrystalDeposit(double eKeV, IRandom rng)
    {
        double deposited = 0.0, energy = eKeV;
        var pos = new Vector3(0.0, 0.0, 0.0);
        var dir = new Vector3(0.0, 0.0, -1.0);
        for (int bounce = 0; bounce < 24; bounce++)
        {
            double mu = _muAt662 * _material.MuRel(energy);
            double s = -Math.Log(1.0 - rng.NextDouble()) / mu;
            pos += dir * s;
            if (pos.Z > 0.0 || pos.Z < -_depthMm) return deposited;       // left the slab (front or back face)
            if (rng.NextDouble() < _material.PhotoFraction(energy))
            {
                deposited += energy;                                       // photoelectric: full remaining energy
                return deposited;
            }
            var (dep, newE, newDir) = ComptonModel.Scatter(energy, dir, rng);
            deposited += dep;
            energy = newE;
            dir = newDir;
            if (energy < 1.0) return deposited;
        }
        return deposited;
    }

    private double Fwhm(double eKeV) => _fwhmAt662 * eKeV * Math.Sqrt(661.7 / Math.Max(eKeV, 1.0));

    private double Smear(double eKeV, IRandom rng)
    {
        double sigma = Fwhm(eKeV) / 2.3548;
        // Box-Muller
        double u1 = Math.Max(1e-12, rng.NextDouble()), u2 = rng.NextDouble();
        double g = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return eKeV + sigma * g;
    }

    private bool InAnyWindow(double measured, double[] lines)
    {
        foreach (double e in lines)
            if (Math.Abs(measured - e) <= Fwhm(e)) return true;
        return false;
    }

    private bool InAnySumWindow(double measured, (double energyKeV, string label)[] sums)
    {
        foreach (var (e, _) in sums)
            if (Math.Abs(measured - e) <= Fwhm(e)) return true;
        return false;
    }

    public static string ToCsv(CascadeRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("distance_mm,single_photopeak_per_decay,sum_peak_per_decay,sum_to_single_ratio");
        foreach (var r in rows)
            sb.AppendLine($"{r.SourceDistanceMm:F1},{r.SinglePhotopeakPerDecay:E4}," +
                          $"{r.SumPeakPerDecay:E4},{r.SumToSingleRatio:E4}");
        return sb.ToString();
    }
}
