using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>Free-electron Klein–Nishina Compton scattering for the soil/air transport (no incoherent scattering
/// function, no Doppler broadening). The interaction probability itself uses XCOM's incoherent cross section, which
/// includes binding; only the angle/energy sampling is free-electron, which over-weights small-angle scattering below
/// ~100 keV where binding suppresses it.</summary>
public static class KleinNishina
{
    public const double ElectronRestEnergyKeV = 510.99895;

    /// <summary>Kahn's rejection method (H. Kahn, 1954) for the Klein–Nishina distribution; KleinNishinaTests checks the
    /// sampled cosines against <see cref="Density"/>. Returns the scattering cosine and the energy ratio
    /// E/E' = 1 + k(1 − cosθ), k = E/m_ec².</summary>
    public static (double CosTheta, double EnergyRatio) Sample(double energyKeV, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        double k = energyKeV / ElectronRestEnergyKeV;
        if (!(k > 0) || !double.IsFinite(k)) throw new ArgumentOutOfRangeException(nameof(energyKeV));
        double branch = (1 + 2 * k) / (9 + 2 * k);
        while (true)
        {
            double r1 = random.NextDouble(), r2 = random.NextDouble(), r3 = random.NextDouble();
            if (r1 <= branch)
            {
                double eta = 1 + 2 * k * r2;
                if (r3 <= 4 * (1 / eta - 1 / (eta * eta))) return (1 - (eta - 1) / k, eta);
            }
            else
            {
                double eta = (1 + 2 * k) / (1 + 2 * k * r2);
                double mu = 1 - (eta - 1) / k;
                if (r3 <= .5 * (mu * mu + 1 / eta)) return (mu, eta);
            }
        }
    }

    /// <summary>Unnormalised Klein–Nishina angular density per unit cosθ (for tests): P² (P + 1/P − sin²θ), P = E'/E.</summary>
    public static double Density(double cosTheta, double energyKeV)
    {
        double k = energyKeV / ElectronRestEnergyKeV;
        double p = 1 / (1 + k * (1 - cosTheta));
        return p * p * (p + 1 / p - (1 - cosTheta * cosTheta));
    }

    /// <summary>Rotate a unit direction by polar cosine <paramref name="cosTheta"/> and azimuth <paramref name="phi"/>.</summary>
    public static Vector3 Rotate(Vector3 d, double cosTheta, double phi)
    {
        double sinTheta = Math.Sqrt(Math.Max(0, 1 - cosTheta * cosTheta));
        double cp = Math.Cos(phi), sp = Math.Sin(phi);
        double w = d.Z;
        if (Math.Abs(w) > 0.99999)
        {
            double sign = w > 0 ? 1 : -1;
            return new(sinTheta * cp, sinTheta * sp, sign * cosTheta);
        }
        double t = Math.Sqrt(1 - w * w);
        return new(
            d.X * cosTheta + sinTheta * (d.X * w * cp - d.Y * sp) / t,
            d.Y * cosTheta + sinTheta * (d.Y * w * cp + d.X * sp) / t,
            w * cosTheta - sinTheta * cp * t);
    }
}
