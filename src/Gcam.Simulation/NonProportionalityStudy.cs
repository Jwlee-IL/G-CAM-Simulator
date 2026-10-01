using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>One energy × crystal: the non-proportionality COMPONENT of the photopeak resolution, from the cascade.</summary>
public sealed record NonPropPoint(
    double EnergyKeV,
    string Crystal,
    double IntrinsicFwhmPct,   // FWHM of the non-proportional light over full-energy events ÷ its mean (the nP COMPONENT)
    double PeakShiftPct,       // RAW non-proportional centroid vs true energy (pre-calibration; a Cs-137 cal zeroes 662)
    double FullEnergyFrac);    // fraction of events that fully absorbed (the photopeak efficiency here)

/// <summary>
/// Derives the NON-PROPORTIONALITY COMPONENT of the crystal's intrinsic resolution from FIRST PRINCIPLES — the part
/// the front-end otherwise buries in one hand-set constant (<c>FrontEndConfig.IntrinsicResolutionFwhm</c>). A
/// full-energy gamma deposits through a Compton CASCADE of electrons; each electron's light is Eᵢ·nP(Eᵢ) with the
/// scintillator's non-proportional response (<see cref="NonProportionality"/>). Because the cascade composition varies
/// event-to-event, the total light Σ Eᵢ·nP(Eᵢ) fluctuates even though Σ Eᵢ is fixed at the photopeak — that fluctuation
/// is the non-proportional resolution, with ZERO photon-counting noise. The study transports the cascade (the same
/// <see cref="ComptonModel"/> physics the main detector uses), applies every candidate crystal's nP to the SAME
/// cascade, and reports the resulting FWHM per energy — showing it is ENERGY-DEPENDENT (a constant floor cannot be),
/// and exactly 0 for a perfectly proportional crystal (the mechanism check). It is only the nP COMPONENT: the full
/// intrinsic floor also has light-collection / Ce-uniformity / SiPM terms this does not model.
///
/// Simplification: photoelectric absorption deposits the WHOLE remaining photon energy as one electron — it omits the
/// shell binding + Auger/X-ray sub-cascade, which would put a little more energy through low-nP-energy electrons, so
/// the photoabsorption-event spread here is a slight UNDER-estimate. Self-contained: it does not change the detector.
/// </summary>
public sealed class NonProportionalityStudy
{
    private readonly double _muAt662, _depthMm;
    private readonly CrystalMaterial _material;

    public NonProportionalityStudy(double? muAt662PerMm = null, double crystalDepthMm = 10.0, CrystalMaterial? material = null)
    {
        _material = material ?? CrystalMaterial.Gagg;
        _muAt662 = muAt662PerMm ?? _material.MuPerMm(CrystalMaterial.ReferenceKeV);
        _depthMm = crystalDepthMm;
    }

    public (NonPropPoint[] points, double[][] spectra, double specBinKeV, double specMaxKeV) Run(
        double[] energiesKeV, NonProportionality[] crystals, long samples, int? seed,
        double spectrumEnergyKeV, int specBins = 260, double specMaxKeV = 1450.0)
    {
        var points = new List<NonPropPoint>();
        var spectra = new double[crystals.Length][];
        for (int c = 0; c < crystals.Length; c++) spectra[c] = new double[specBins];
        double specBinKeV = specMaxKeV / specBins;

        var sites = new List<double>(8);
        foreach (double e0 in energiesKeV)
        {
            var rng = new DefaultRandom((seed ?? 0) + (int)Math.Round(e0));
            bool snapshot = Math.Abs(e0 - spectrumEnergyKeV) < 1e-6;
            var n = new long[crystals.Length];
            var mean = new double[crystals.Length];
            var m2 = new double[crystals.Length];       // Welford accumulation for variance
            long full = 0;

            for (long s = 0; s < samples; s++)
            {
                bool fullyAbsorbed = Transport(e0, rng, sites);
                if (fullyAbsorbed) full++;
                for (int c = 0; c < crystals.Length; c++)
                {
                    double light = 0.0;
                    foreach (double dep in sites) light += dep * crystals[c].Relative(dep);
                    if (snapshot)
                    {
                        int b = (int)(light / specBinKeV);
                        if (b >= 0 && b < specBins) spectra[c][b] += 1.0;
                    }
                    if (fullyAbsorbed)
                    {
                        n[c]++;
                        double d = light - mean[c];
                        mean[c] += d / n[c];
                        m2[c] += d * (light - mean[c]);
                    }
                }
            }

            for (int c = 0; c < crystals.Length; c++)
            {
                double fwhm = n[c] > 1 && mean[c] > 0 ? 2.3548 * Math.Sqrt(m2[c] / n[c]) / mean[c] : 0.0;
                double shift = mean[c] > 0 ? (mean[c] - e0) / e0 : 0.0;   // nP is normed to 662, so ≠662 lines shift
                points.Add(new NonPropPoint(e0, crystals[c].Name, 100.0 * fwhm, 100.0 * shift,
                                            (double)full / samples));
            }
        }
        return (points.ToArray(), spectra, specBinKeV, specMaxKeV);
    }

    /// <summary>Transport one gamma of energy e0 into the crystal slab (front z=0, back z=−depth), normal incidence,
    /// collecting each interaction's ELECTRON energy into <paramref name="sites"/>. Returns true if the photon fully
    /// absorbed (photopeak event, Σ sites = e0); false if it escaped a face (a Compton-continuum event, partial
    /// deposits still in <paramref name="sites"/>). Same cascade physics as the main detector's <see cref="ComptonModel"/>.</summary>
    private bool Transport(double e0, IRandom rng, List<double> sites)
    {
        sites.Clear();
        double e = e0;
        var pos = new Vector3(0.0, 0.0, 0.0);
        var dir = new Vector3(0.0, 0.0, -1.0);
        for (int step = 0; step < 32 && e > 1.0; step++)
        {
            double mu = _muAt662 * _material.MuRel(e);
            double s = -Math.Log(1.0 - rng.NextDouble()) / mu;
            pos += dir * s;
            if (pos.Z > 0.0 || pos.Z < -_depthMm) return false;   // escaped the slab → not full-energy
            if (rng.NextDouble() < _material.PhotoFraction(e))
            {
                sites.Add(e);                                     // photoelectric: the remaining energy → one electron
                return true;
            }
            var (dep, newE, newDir) = ComptonModel.Scatter(e, dir, rng);
            sites.Add(dep);                                       // Compton recoil electron
            e = newE; dir = newDir;
        }
        return false;
    }

    public static string ToCsv(NonPropPoint[] points)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("energy_keV,crystal,intrinsic_fwhm_pct,peak_shift_pct,full_energy_frac");
        foreach (var p in points)
            sb.AppendLine($"{p.EnergyKeV:F0},{p.Crystal},{p.IntrinsicFwhmPct:F3},{p.PeakShiftPct:F3},{p.FullEnergyFrac:F4}");
        return sb.ToString();
    }
}
