using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Detector;

/// <summary>
/// Photon interaction physics inside the scintillator: the photoelectric-vs-Compton branch,
/// the energy-dependent attenuation, and Klein-Nishina scattering (Kahn's exact rejection
/// sampler). Approximations (parametrized, not tabulated cross-sections) tuned to a mid-Z
/// scintillator (~GAGG) at gamma-camera energies; the point is the multi-pixel spreading of a
/// Compton event, not spectroscopic-grade cross-sections.
/// </summary>
public static class ComptonModel
{
    public const double MeC2 = 510.999;   // electron rest energy (keV)

    /// <summary>Probability an interaction is photoelectric (full absorption) vs Compton, at
    /// energy <paramref name="eKeV"/>. Tuned to ~0.9 @122, ~0.35 @662, ~0.15 @1332 keV.</summary>
    public static double PhotoFraction(double eKeV)
        => 1.0 / (1.0 + Math.Pow(eKeV / 460.0, 1.66));

    /// <summary>Total linear attenuation at <paramref name="eKeV"/> relative to its value at
    /// 662 keV (rises steeply at low energy as photoelectric takes over → short mean free path,
    /// so a downscattered photon reabsorbs nearby).</summary>
    public static double MuRel(double eKeV)
        => Math.Pow(661.7 / Math.Max(eKeV, 1.0), 1.56);

    /// <summary>
    /// Klein-Nishina Compton scatter via Kahn's rejection method (exact, allocation-free).
    /// Returns the energy deposited to the recoil electron and the scattered photon's new
    /// energy/direction.
    /// </summary>
    public static (double deposit, double newEnergy, Vector3 newDir) Scatter(
        double eKeV, Vector3 dir, IRandom rng)
    {
        double alpha = eKeV / MeC2;
        double eps0 = 1.0 / (1.0 + 2.0 * alpha);
        double eps0sq = eps0 * eps0;
        double a1 = Math.Log(1.0 / eps0);            // = ln(1+2α)
        double a2 = 0.5 * (1.0 - eps0sq);
        double eps, t, sin2;
        while (true)
        {
            double u1 = rng.NextDouble(), u2 = rng.NextDouble(), u3 = rng.NextDouble();
            if (a1 / (a1 + a2) > u1)
                eps = Math.Exp(-a1 * u2);             // ε log-uniform in [ε0, 1]
            else
                eps = Math.Sqrt(eps0sq + (1.0 - eps0sq) * u2);
            t = (1.0 - eps) / (alpha * eps);          // = 1 - cosθ
            sin2 = t * (2.0 - t);                     // sin²θ
            double g = 1.0 - eps * sin2 / (1.0 + eps * eps);
            if (g >= u3) break;
        }
        double cosTheta = 1.0 - t;
        double newE = eps * eKeV;
        double deposit = eKeV - newE;
        return (deposit, newE, ScatterDirection(dir, cosTheta, rng));
    }

    /// <summary>Rotate a unit direction by polar angle θ (cosθ given) and a uniform azimuth.</summary>
    private static Vector3 ScatterDirection(Vector3 d, double cosTheta, IRandom rng)
    {
        double sinTheta = Math.Sqrt(Math.Max(0.0, 1.0 - cosTheta * cosTheta));
        double phi = 2.0 * Math.PI * rng.NextDouble();

        // orthonormal basis (u, w) perpendicular to d
        var a = Math.Abs(d.X) < 0.9 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
        var u = (a - d * a.Dot(d)).Normalized();
        var w = Cross(d, u);
        return (d * cosTheta + (u * Math.Cos(phi) + w * Math.Sin(phi)) * sinTheta).Normalized();
    }

    private static Vector3 Cross(Vector3 a, Vector3 b)
        => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
