using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>The isotopes whose per-decay gamma CASCADE is modelled for true (cascade) coincidence summing.</summary>
public enum Isotope { Cs137, Co60, Na22 }

/// <summary>
/// A nuclear decay's CORRELATED gamma emission: the set of gammas from ONE decay, with the per-decay intensities of
/// <see cref="Isotopes"/> (one table, one value) and the directional correlation between them. Two gammas from the
/// SAME decay share a decay time, so if both deposit in the crystal their energies SUM into one event. (Contrast
/// random coincidence / pile-up, which is between DIFFERENT decays and scales with rate; cascade summing is
/// rate-INDEPENDENT and ∝ ε².)
/// <list type="bullet">
/// <item><b>Cs-137</b> — a single 662 keV line (per-decay 0.851); no coincident partner, so summing = 0.</item>
/// <item><b>Co-60</b> — the 1173 + 1332 keV cascade 4⁺ →E2→ 2⁺ →E2→ 0⁺, both ~1.0/decay, with the angular
///   correlation W(θ) = 1 + (1/8)cos²θ + (1/24)cos⁴θ (A₂ = 0.1020, A₄ = 0.0091 — D. R. Hamilton, Phys. Rev. 58,
///   122 (1940); ORTEC AN34 Experiment 19). Two gammas that both reach a small, distant detector are near-parallel,
///   so W raises their joint detection by 1 + A₂ + A₄ = 10/9 over independent emission.</item>
/// <item><b>Na-22</b> — a 1275 keV gamma plus (β⁺ branch) a positron whose annihilation gives two 511 keV photons
///   emitted BACK-TO-BACK (180°). On a one-sided detector the back-to-back pair can almost never both be caught, so
///   511+511 summing is strongly suppressed and 511+1275 dominates. The 1275 keV direction is independent of the
///   pair (no β⁺–γ correlation for an unoriented source).</item>
/// </list>
/// Directions are drawn with a FIXED number of uniforms per photon (the W(θ) cosine by inverse CDF, never by
/// rejection): a data-dependent draw count on the seeded generator biased analog hit fractions by 1–2 %
/// (PLAN.Physics.CascadeEmission.Review, R-6).
/// </summary>
public sealed class DecayScheme
{
    /// <summary>Co-60 W(θ) = 1 + a₂cos²θ + a₄cos⁴θ (pure E2–E2, 4→2→0).</summary>
    public const double Co60A2Cos = 1.0 / 8.0, Co60A4Cos = 1.0 / 24.0;

    public Isotope Isotope { get; }

    // Single-line photopeak energies and the SUM-peak energies (coincident combinations) this decay can produce —
    // used by the summing study to window the spectrum.
    public double[] SingleLinesKeV { get; }
    public (double energyKeV, string label)[] SumPeaks { get; }

    /// <summary>Mean photons per decay (Σ line intensities) — the history allocation weight per becquerel.</summary>
    public double MeanPhotonsPerDecay { get; }

    /// <summary>Largest number of photons one decay can emit.</summary>
    public int MaxPhotonsPerDecay { get; }

    private readonly double _p1, _p2;   // Co-60: P(1173), P(1332); Na-22: P(1275), P(β⁺); Cs-137: P(662)

    private DecayScheme(Isotope iso, double[] singles, (double, string)[] sums, double p1, double p2, double mean, int max)
    {
        Isotope = iso;
        SingleLinesKeV = singles;
        SumPeaks = sums;
        _p1 = p1;
        _p2 = p2;
        MeanPhotonsPerDecay = mean;
        MaxPhotonsPerDecay = max;
    }

    public static DecayScheme For(Isotope iso)
    {
        switch (iso)
        {
            case Isotope.Co60:
            {
                var l = Isotopes.Get("Co-60").Lines;
                return new(Isotope.Co60, [l[0].EnergyKeV, l[1].EnergyKeV], [(l[0].EnergyKeV + l[1].EnergyKeV, "1173+1332")],
                    l[0].Intensity, l[1].Intensity, l[0].Intensity + l[1].Intensity, 2);
            }
            case Isotope.Na22:
            {
                // Isotopes lists the 511 line as 2 × the β⁺ branch (two photons per annihilation).
                var l = Isotopes.Get("Na-22").Lines;
                double beta = l[0].Intensity / 2, gamma = l[1].Intensity;
                return new(Isotope.Na22, [l[0].EnergyKeV, l[1].EnergyKeV],
                    [(2 * l[0].EnergyKeV, "511+511"), (l[0].EnergyKeV + l[1].EnergyKeV, "511+1275"),
                     (2 * l[0].EnergyKeV + l[1].EnergyKeV, "511+511+1275")],
                    gamma, beta, gamma + 2 * beta, 3);
            }
            default:
                return new(Isotope.Cs137, [661.7], [], 0.851, 0, 0.851, 1);
        }
    }

    /// <summary>The scheme for an isotope name. Only Cs-137, Co-60 and Na-22 have a modelled cascade; any other name
    /// (Ir-192, Co-57, Am-241, a typo, blank) throws — a fallback would quietly run another isotope's study.</summary>
    public static DecayScheme From(string? name) => Cascade(name) ?? (IsCs137(name) ? For(Isotope.Cs137)
        : throw new NotSupportedException(
            $"Cascade (true-coincidence) summing is not modelled for '{name}'; use Cs-137, Co-60 or Na-22."));

    /// <summary>The scheme of an isotope that emits two or more correlated photons per decay (Co-60, Na-22), else
    /// null — single-photon isotopes (Cs-137, Co-57, Am-241) need no decay grouping, and Ir-192's cascades are not
    /// modelled.</summary>
    public static DecayScheme? Cascade(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "CO-60" or "CO60" => For(Isotope.Co60),
        "NA-22" or "NA22" => For(Isotope.Na22),
        _ => null,
    };

    private static bool IsCs137(string? name) => name?.Trim().ToUpperInvariant() is "CS-137" or "CS137";

    /// <summary>Sample ONE decay's emitted gammas (energy + direction) into <paramref name="outPhotons"/>, applying
    /// the per-decay intensity and angular correlation (analog 4π). May be empty (branching miss).</summary>
    public void Sample(IRandom rng, List<(double energyKeV, Vector3 dir)> outPhotons)
    {
        var energies = new List<double>(3);
        SampleEnergies(rng, energies);
        outPhotons.Clear();
        if (energies.Count == 0) return;
        var dirs = new Vector3[energies.Count];
        Directions(rng, energies, 0, rng.NextOnUnitSphere(), dirs);
        for (int i = 0; i < energies.Count; i++) outPhotons.Add((energies[i], dirs[i]));
    }

    /// <summary>The photon energies of one decay (branching only; directions come from <see cref="Directions"/>).
    /// Na-22 lists the 1275 keV gamma first, then the two 511 keV annihilation photons.</summary>
    public void SampleEnergies(IRandom rng, List<double> energies)
    {
        energies.Clear();
        switch (Isotope)
        {
            case Isotope.Co60:
                if (rng.NextDouble() < _p1) energies.Add(SingleLinesKeV[0]);
                if (rng.NextDouble() < _p2) energies.Add(SingleLinesKeV[1]);
                break;
            case Isotope.Na22:
                if (rng.NextDouble() < _p1) energies.Add(SingleLinesKeV[1]);
                if (rng.NextDouble() < _p2) { energies.Add(SingleLinesKeV[0]); energies.Add(SingleLinesKeV[0]); }
                break;
            default:
                if (rng.NextDouble() < _p1) energies.Add(SingleLinesKeV[0]);
                break;
        }
    }

    /// <summary>Directions of one decay's photons given photon <paramref name="known"/>'s direction: every other
    /// photon is drawn from its conditional density given that one. The joint density is symmetric in the photons it
    /// correlates, so any photon may be the given one — this is what lets a biased source aim one photon of the decay
    /// and still sample the decay exactly (PLAN.Physics.CascadeEmission, K-2).</summary>
    public void Directions(IRandom rng, IReadOnlyList<double> energies, int known, Vector3 knownDir, Vector3[] dirs)
    {
        dirs[known] = knownDir;
        switch (Isotope)
        {
            case Isotope.Co60:
                // Two photons: the partner follows W(θ); a lone photon has no partner.
                for (int i = 0; i < energies.Count; i++)
                    if (i != known) dirs[i] = Correlated(rng, knownDir);
                break;
            case Isotope.Na22:
            {
                bool knownIsPair = Math.Abs(energies[known] - SingleLinesKeV[0]) < 1e-9;
                Vector3 pair = knownIsPair ? knownDir : default;
                bool havePair = knownIsPair;
                for (int i = 0; i < energies.Count; i++)
                {
                    if (i == known) continue;
                    if (Math.Abs(energies[i] - SingleLinesKeV[0]) < 1e-9)
                    {
                        // The other annihilation photon: exactly anti-parallel to its partner (acollinearity ~0.5°
                        // is irrelevant for a one-sided detector).
                        if (!havePair) { pair = rng.NextOnUnitSphere(); havePair = true; dirs[i] = pair; }
                        else dirs[i] = pair * -1.0;
                    }
                    else dirs[i] = rng.NextOnUnitSphere();   // 1275 keV: independent of the pair
                }
                break;
            }
            default:
                for (int i = 0; i < energies.Count; i++)
                    if (i != known) dirs[i] = rng.NextOnUnitSphere();
                break;
        }
    }

    /// <summary>Inverse CDF of the Co-60 cos θ density ∝ W(c) on [−1, 1]: solves G(c) = (2u − 1)·G(1) with
    /// G(c) = c + a₂c³/3 + a₄c⁵/5 (odd, strictly increasing) by Newton — one uniform per partner.</summary>
    public static double Co60Cosine(double u)
    {
        const double a2 = Co60A2Cos, a4 = Co60A4Cos;
        double g1 = 1 + a2 / 3 + a4 / 5, target = (2 * u - 1) * g1, c = target / g1;
        for (int i = 0; i < 50; i++)
        {
            double c2 = c * c;
            double g = c * (1 + c2 * (a2 / 3 + c2 * a4 / 5)) - target, slope = 1 + c2 * (a2 + c2 * a4);
            double next = Math.Clamp(c - g / slope, -1.0, 1.0);
            if (Math.Abs(next - c) < 1e-15) return next;
            c = next;
        }
        return c;
    }

    private static Vector3 Correlated(IRandom rng, Vector3 u)
    {
        double c = Co60Cosine(rng.NextDouble());
        double phi = 2.0 * Math.PI * rng.NextDouble(), s = Math.Sqrt(Math.Max(0.0, 1.0 - c * c));
        var helper = Math.Abs(u.X) < 0.9 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
        var e1 = Cross(u, helper).Normalized();
        var e2 = Cross(u, e1);
        return u * c + e1 * (s * Math.Cos(phi)) + e2 * (s * Math.Sin(phi));
    }

    private static Vector3 Cross(Vector3 a, Vector3 b)
        => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
