namespace Gcam.Core;

/// <summary>Statistical samplers built on an <see cref="IRandom"/>.</summary>
public static class Sampling
{
    /// <summary>Standard-normal sample via Box–Muller.</summary>
    public static double Gaussian(IRandom rng)
    {
        double u1 = 1.0 - rng.NextDouble(); // in (0, 1], avoids log(0)
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>
    /// Poisson sample with mean <paramref name="lambda"/>. Knuth's method for small
    /// means, Gaussian approximation for large means (where Knuth gets slow).
    /// </summary>
    public static int Poisson(IRandom rng, double lambda)
    {
        if (lambda <= 0.0) return 0;

        if (lambda < 30.0)
        {
            double target = Math.Exp(-lambda);
            int k = 0;
            double p = 1.0;
            do
            {
                k++;
                p *= rng.NextDouble();
            }
            while (p > target);
            return k - 1;
        }

        int v = (int)Math.Round(lambda + Math.Sqrt(lambda) * Gaussian(rng));
        return v < 0 ? 0 : v;
    }
}
