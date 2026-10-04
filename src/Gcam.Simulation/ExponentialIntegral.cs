namespace Gcam.Simulation;

/// <summary>E₁(x) = ∫₁^∞ e^(−xt)/t dt for x &gt; 0: power series for x ≤ 1, Lentz continued fraction above
/// (Abramowitz &amp; Stegun 5.1.11 and 5.1.22). Both are iterated to double precision.</summary>
public static class ExponentialIntegral
{
    private const double EulerGamma = 0.57721566490153286061;

    public static double E1(double x)
    {
        if (!(x > 0) || double.IsNaN(x)) throw new ArgumentOutOfRangeException(nameof(x));
        if (double.IsPositiveInfinity(x)) return 0;
        if (x <= 1)
        {
            double sum = 0, term = 1;
            for (int k = 1; k < 200; k++)
            {
                term *= -x / k;
                double add = -term / k;
                sum += add;
                if (Math.Abs(add) < 1e-17 * Math.Abs(sum)) break;
            }
            return -EulerGamma - Math.Log(x) + sum;
        }
        // Continued fraction e^{-x} / (x + 1/(1 + 1/(x + 2/(1 + 2/(x + ...))))) in Lentz form.
        const double tiny = 1e-300;
        double b = x + 1, c = 1 / tiny, d = 1 / b, h = d;
        for (int i = 1; i < 500; i++)
        {
            double a = -(double)i * i;
            b += 2;
            d = 1 / (a * d + b);
            c = b + a / c;
            double delta = c * d;
            h *= delta;
            if (Math.Abs(delta - 1) < 1e-16) break;
        }
        return h * Math.Exp(-x);
    }
}
