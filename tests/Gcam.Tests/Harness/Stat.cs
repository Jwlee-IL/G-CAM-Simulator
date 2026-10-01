namespace Gcam.Tests.Harness;

/// <summary>Statistical assertions for Monte Carlo results: a tolerance is k standard errors of the estimator,
/// not a hand-picked percentage, so it scales with the sample size and says how unlikely a false failure is
/// (k = 4: about 6 in 100 000 for a normal estimator).</summary>
public static class Stat
{
    public const double DefaultK = 4.0;

    /// <summary>Asserts |observed − expected| ≤ k·σ, where σ is the standard error of <paramref name="observed"/>.</summary>
    public static void Within(double observed, double expected, double sigma, double k = DefaultK, string what = "value")
    {
        Assert.True(sigma > 0.0 && double.IsFinite(sigma), $"{what}: standard error must be positive, got {sigma}");
        double z = (observed - expected) / sigma;
        Assert.True(Math.Abs(z) <= k,
            $"{what}: observed {observed:G6}, expected {expected:G6} — {z:+0.0;-0.0}σ (σ = {sigma:G3}, limit {k}σ)");
    }

    /// <summary>Standard error of a fraction p estimated from n trials.</summary>
    public static double BinomialSigma(double p, long n) => Math.Sqrt(Math.Max(p * (1.0 - p), 1e-12) / n);

    /// <summary>Sample mean and (unbiased) variance.</summary>
    public static (double Mean, double Variance) Moments(IReadOnlyList<double> xs)
    {
        double mean = xs.Average();
        double ss = 0.0;
        foreach (double x in xs) ss += (x - mean) * (x - mean);
        return (mean, ss / (xs.Count - 1));
    }

    /// <summary>Mean and standard error of the mean.</summary>
    public static (double Mean, double Sigma) MeanWithError(IReadOnlyList<double> xs)
    {
        var (mean, variance) = Moments(xs);
        return (mean, Math.Sqrt(variance / xs.Count));
    }
}
