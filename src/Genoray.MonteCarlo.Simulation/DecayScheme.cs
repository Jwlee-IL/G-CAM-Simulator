using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>The isotopes whose per-decay gamma CASCADE is modelled for true (cascade) coincidence summing.</summary>
public enum Isotope { Cs137, Co60, Na22 }

/// <summary>
/// A nuclear decay's CORRELATED gamma emission: the set of gammas from ONE decay, with the right per-decay
/// intensities AND angular correlation. This is what makes true (cascade) coincidence summing possible — two gammas
/// from the SAME decay share a decay time, so if both deposit in the crystal their energies SUM into one event.
/// (Contrast random coincidence / pile-up, which is between DIFFERENT decays and scales with rate; cascade summing
/// is rate-INDEPENDENT and ∝ ε².)
/// <list type="bullet">
/// <item><b>Cs-137</b> — a single 662 keV line (per-decay 0.851); no significant coincident partner, so summing ≈ 0.</item>
/// <item><b>Co-60</b> — the 1173 + 1332 keV CASCADE, both ~1.0/decay. Their angular correlation W(θ) is weak (few %),
///   so the two directions are taken independent-isotropic.</item>
/// <item><b>Na-22</b> — a 1275 keV gamma plus (β⁺ branch) a positron whose annihilation gives two 511 keV photons
///   emitted BACK-TO-BACK (180°). On a one-sided detector the back-to-back pair can almost never both be caught, so
///   511+511 summing is strongly suppressed and 511+1275 dominates — a correlation the model must capture.</item>
/// </list>
/// </summary>
public sealed class DecayScheme
{
    public Isotope Isotope { get; }

    // Single-line photopeak energies and the SUM-peak energies (coincident combinations) this decay can produce —
    // used by the summing study to window the spectrum.
    public double[] SingleLinesKeV { get; }
    public (double energyKeV, string label)[] SumPeaks { get; }

    private DecayScheme(Isotope iso, double[] singles, (double, string)[] sums)
    {
        Isotope = iso;
        SingleLinesKeV = singles;
        SumPeaks = sums;
    }

    public static DecayScheme For(Isotope iso) => iso switch
    {
        Isotope.Co60 => new(Isotope.Co60, [1173.2, 1332.5], [(2505.7, "1173+1332")]),
        Isotope.Na22 => new(Isotope.Na22, [511.0, 1274.5],
                            [(1022.0, "511+511"), (1785.5, "511+1275"), (2296.5, "511+511+1275")]),
        _ => new(Isotope.Cs137, [661.7], []),
    };

    public static DecayScheme From(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "CO-60" or "CO60" => For(Isotope.Co60),
        "NA-22" or "NA22" => For(Isotope.Na22),
        _ => For(Isotope.Cs137),
    };

    /// <summary>Sample ONE decay's emitted gammas (energy + direction) into <paramref name="outPhotons"/>, applying
    /// the per-decay intensity and angular correlation. May be empty (branching miss).</summary>
    public void Sample(IRandom rng, List<(double energyKeV, Vector3 dir)> outPhotons)
    {
        outPhotons.Clear();
        switch (Isotope)
        {
            case Isotope.Co60:
                // Cascade: 1173 then 1332, both ~1.0/decay, weak W(θ) → independent isotropic directions.
                if (rng.NextDouble() < 0.999) outPhotons.Add((1173.2, rng.NextOnUnitSphere()));
                if (rng.NextDouble() < 0.999) outPhotons.Add((1332.5, rng.NextOnUnitSphere()));
                break;

            case Isotope.Na22:
                // 1275 keV prompt gamma (~1.0/decay), independent direction.
                if (rng.NextDouble() < 0.999) outPhotons.Add((1274.5, rng.NextOnUnitSphere()));
                // β⁺ branch (~90.3%) → annihilation → two 511 keV BACK-TO-BACK; EC branch (~9.7%) → no annihilation.
                if (rng.NextDouble() < 0.903)
                {
                    var d = rng.NextOnUnitSphere();
                    outPhotons.Add((511.0, d));
                    outPhotons.Add((511.0, d * -1.0));   // exactly anti-parallel (~0.5° acollinearity ignored)
                }
                break;

            default: // Cs-137
                if (rng.NextDouble() < 0.851) outPhotons.Add((661.7, rng.NextOnUnitSphere()));
                break;
        }
    }
}
