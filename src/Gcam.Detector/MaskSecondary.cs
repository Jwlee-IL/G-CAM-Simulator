using Gcam.Core;

namespace Gcam.Detector;

/// <summary>
/// Secondary-photon production when a gamma interacts in the TUNGSTEN of a coded-aperture mask — the physics a
/// pure-attenuation mask omits. A photon that interacts in a closed cell does not simply vanish:
/// <list type="bullet">
/// <item><b>Compton scatter</b> (dominant at 662 keV, where W attenuation is Compton-flat): a lower-energy,
/// deflected photon (Klein-Nishina, via <see cref="ComptonModel"/>) — some head to the detector as a scatter
/// background.</item>
/// <item><b>K-fluorescence</b> (from photoelectric absorption above the W K-edge, 69.5 keV): the atom emits a
/// characteristic W K X-ray — Kα 59.3 keV / Kβ 67.2 keV — isotropically. Both are BELOW the K-edge so they are
/// not re-absorbed by the K-shell and can escape, though heavily self-absorbed.</item>
/// </list>
/// This class only DECIDES the secondary (type, energy, direction) at an interaction; the study transports it out
/// of the slab and to the detector. No hand-tuned lines — the X-rays are the real W characteristic energies.
/// </summary>
public sealed class MaskSecondary
{
    public const double KEdgeKeV = 69.5;   // W K absorption edge
    public const double KaKeV = 59.3;      // W Kα (intensity-weighted Kα1/Kα2)
    public const double KbKeV = 67.2;      // W Kβ
    private const double FluorYield = 0.958;   // W K fluorescence yield ω_K
    private const double KShellFrac = 0.88;    // K-shell share of the photoelectric cross-section above the edge
    private const double KaShare = 0.87;       // Kα fraction of the emitted K X-rays (rest Kβ)

    // W photoelectric fraction of the total attenuation (log-log table; PE ∝ Z^~4.5/E^3, Compton ~ flat).
    private static readonly double[] _peE = { 60, 100, 200, 300, 511, 662, 1000, 1332 };
    private static readonly double[] _peF = { 0.98, 0.96, 0.63, 0.40, 0.16, 0.095, 0.05, 0.032 };

    // W total attenuation μ(E)/μ(662), extended below 122 keV so fluorescence escape is attenuated correctly.
    // Kα/Kβ (59/67 keV) sit just BELOW the 69.5 keV K-edge, so they use the (lower) below-edge μ.
    // Same tungsten μ(E)/μ(662) table as CodedApertureMask.TungstenMuRel (200–600 keV points from NIST, 2026-10-01).
    private static readonly double[] _muE = { 50, 60, 69.4, 122, 200, 300, 400, 500, 600, 662, 1000, 1332 };
    private static readonly double[] _muR = { 57.0, 47.0, 40.0, 28.6, 7.962, 3.287, 1.954, 1.399, 1.109, 1.0, 0.67, 0.55 };

    /// <summary>W photoelectric fraction of the total attenuation at <paramref name="eKeV"/>.</summary>
    public static double PhotoFraction(double eKeV) => LogLog(eKeV, _peE, _peF);

    /// <summary>W total linear attenuation relative to 662 keV.</summary>
    public static double MuRel(double eKeV) => LogLog(eKeV, _muE, _muR);

    /// <summary>Decide the secondary photon produced when a gamma of (energy, dir) interacts in tungsten. Returns
    /// <c>produced = false</c> when the interaction leaves no escaping secondary (photo-absorbed with no K X-ray).</summary>
    public (bool produced, double energyKeV, Vector3 dir, bool isFluor) Interact(
        double energyKeV, Vector3 dir, IRandom rng)
    {
        if (rng.NextDouble() < PhotoFraction(energyKeV))
        {
            // Photoelectric absorption: possibly a K-fluorescence X-ray (only above the K-edge).
            if (energyKeV > KEdgeKeV && rng.NextDouble() < KShellFrac * FluorYield)
            {
                double e = rng.NextDouble() < KaShare ? KaKeV : KbKeV;
                return (true, e, rng.NextOnUnitSphere(), true);   // isotropic
            }
            return (false, 0.0, dir, false);
        }
        // Compton scatter: the deflected, lower-energy photon.
        var (_, newE, newDir) = ComptonModel.Scatter(energyKeV, dir, rng);
        return (true, newE, newDir, false);
    }

    private static double LogLog(double e, double[] xs, double[] ys)
    {
        if (e <= xs[0]) return ys[0];
        if (e >= xs[^1]) return ys[^1];
        int i = 1; while (e > xs[i]) i++;
        double t = (Math.Log(e) - Math.Log(xs[i - 1])) / (Math.Log(xs[i]) - Math.Log(xs[i - 1]));
        return Math.Exp(Math.Log(ys[i - 1]) + t * (Math.Log(ys[i]) - Math.Log(ys[i - 1])));
    }
}
