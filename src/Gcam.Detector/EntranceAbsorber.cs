using Gcam.Core;

namespace Gcam.Detector;

/// <summary>
/// A thin passive attenuator in front of the crystal — the source encapsulation, the detector entrance
/// window / reflector and the housing lumped into one stainless-steel-equivalent slab. Because photoelectric
/// absorption rises steeply toward low energy (μ/ρ ~ 1/E^3 well above the K-edge), it preferentially removes
/// soft photons: it tames the low-energy X-ray lines (e.g. Cs-137's 32 keV Ba K X-ray) that a bare vacuum
/// geometry lets through unattenuated, while barely touching the 662 keV photopeak. That reproduces why a real,
/// encapsulated source shows only a modest X-ray bump — its prominence is set by how much material sits between
/// the source and the crystal.
///
/// Applied as a survival WEIGHT factor Transmit(E) = exp(-μ(E)·t): low-variance and consistent with the
/// importance weights the biased source carries (an attenuated photon still deposits when it interacts, just
/// with reduced probability). Off (thickness 0) leaves the bare-geometry spectrum unchanged.
/// </summary>
public sealed class EntranceAbsorber
{
    private readonly double _muThickness662;   // μ(662 keV)·thickness, dimensionless

    /// <summary>Linear attenuation of stainless steel (≈ iron) at 662 keV: μ/ρ 0.0737 cm²/g × ρ 7.87 g/cm³
    /// = 0.580 /cm = 0.0580 /mm.</summary>
    public const double SteelMuPerMm662 = 0.0580;

    /// <param name="thicknessMm">effective stainless-steel-equivalent thickness in front of the crystal.</param>
    /// <param name="muPerMm662">absorber linear attenuation at 662 keV (defaults to steel).</param>
    public EntranceAbsorber(double thicknessMm, double muPerMm662 = SteelMuPerMm662)
        => _muThickness662 = Math.Max(0.0, thicknessMm) * muPerMm662;

    /// <summary>Fraction of photons of energy <paramref name="energyKeV"/> that pass the absorber.</summary>
    public double Transmit(double energyKeV)
        => _muThickness662 <= 0.0 ? 1.0 : Math.Exp(-_muThickness662 * IronMuRel(energyKeV));

    /// <summary>Transport a photon THROUGH the slab as real physics: it most likely passes unchanged, or it
    /// interacts — photo-absorbed (removed) or Compton-scattered to a lower energy and new direction. A 662 keV
    /// photon that interacts almost always Compton-scatters (iron is Compton-dominated there), so the FORWARD
    /// small-angle scatters continue into the crystal and deposit just below full energy — the physical origin of
    /// the photopeak's low-energy tail that fills the Compton-edge-to-photopeak valley. Soft X-rays that interact
    /// are instead mostly photo-absorbed, which is why the slab still attenuates the 32 keV line. Uses the shared
    /// <see cref="ComptonModel"/> (Klein-Nishina) — no hand-tuned tail.</summary>
    public (bool absorbed, double energy, Vector3 dir) Interact(double energyKeV, Vector3 dir, IRandom rng)
    {
        if (_muThickness662 <= 0.0) return (false, energyKeV, dir);
        double muT = _muThickness662 * IronMuRel(energyKeV);
        if (rng.NextDouble() >= 1.0 - Math.Exp(-muT)) return (false, energyKeV, dir);   // passes through
        if (rng.NextDouble() < IronPhotoFraction(energyKeV)) return (true, 0.0, dir);   // photo-absorbed
        var (_, eNew, dirNew) = ComptonModel.Scatter(energyKeV, dir, rng);              // Compton-scattered
        return (false, eNew, dirNew);
    }

    // Iron photoelectric FRACTION of interactions vs Compton: photoelectric dominates only at low energy (K-edge
    // 7 keV) and is negligible above ~100 keV where Compton takes over. So a 32 keV X-ray that interacts is mostly
    // absorbed, while a 662 keV photon that interacts almost always scatters. Rough parametrization (not tabulated).
    private static double IronPhotoFraction(double eKeV) => 1.0 / (1.0 + Math.Pow(eKeV / 45.0, 2.6));

    // Iron (stainless-steel proxy) linear-attenuation ratio μ(E)/μ(662 keV) — NIST-XCOM total mass-attenuation
    // (with coherent) points, divided by the 662 keV value (0.0737 cm²/g), log–log interpolated and end-clamped.
    // Anchored at 662 keV = 1.0 so the thickness keeps its physical meaning. Steep below ~150 keV (photoelectric),
    // nearly flat above (Compton) — the shape that makes a thin slab a low-energy-only filter.
    private static readonly double[] _muE = { 30.0, 40.0, 50.0, 60.0, 80.0, 100.0, 150.0, 200.0, 300.0, 400.0, 662.0, 1000.0, 1332.0 };
    private static readonly double[] _muR = { 110.9, 49.2, 26.6, 16.3, 8.07, 5.05, 2.66, 1.981, 1.492, 1.275, 1.000, 0.814, 0.701 };

    private static double IronMuRel(double energyKeV)
    {
        if (energyKeV <= _muE[0]) return _muR[0];
        if (energyKeV >= _muE[^1]) return _muR[^1];
        int i = 1; while (energyKeV > _muE[i]) i++;
        double t = (Math.Log(energyKeV) - Math.Log(_muE[i - 1])) / (Math.Log(_muE[i]) - Math.Log(_muE[i - 1]));
        return Math.Exp(Math.Log(_muR[i - 1]) + t * (Math.Log(_muR[i]) - Math.Log(_muR[i - 1])));
    }
}
