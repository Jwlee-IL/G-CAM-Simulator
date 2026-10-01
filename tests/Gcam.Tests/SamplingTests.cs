using Gcam.Core;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

/// <summary>The samplers every noise study rests on: Poisson counts (Knuth below λ = 30, a rounded Gaussian above)
/// and the Box–Muller normal. Checked against their analytic moments with k·σ tolerances.</summary>
public class SamplingTests
{
    private const int N = 200_000;

    [Theory]
    [InlineData(0.5)]
    [InlineData(5.0)]
    [InlineData(29.9)]    // last Knuth value
    [InlineData(30.0)]    // first Gaussian-approximation value
    [InlineData(400.0)]
    public void Poisson_HasMeanAndVarianceLambda(double lambda)
    {
        var rng = new DefaultRandom(1000 + (int)(lambda * 10));
        var xs = new double[N];
        for (int i = 0; i < N; i++) xs[i] = Sampling.Poisson(rng, lambda);
        var (mean, variance) = Stat.Moments(xs);

        Stat.Within(mean, lambda, Math.Sqrt(lambda / N), what: $"Poisson({lambda}) mean");
        // Standard error of the sample variance: sqrt((μ4 − σ⁴)/n), with μ4 = λ(1 + 3λ) for a Poisson variable.
        // Above λ = 30 the rounded Gaussian adds ~1/12 of rounding variance; it is within the tolerance at these n.
        Stat.Within(variance, lambda, Math.Sqrt((lambda + 2.0 * lambda * lambda) / N), what: $"Poisson({lambda}) variance");
        Assert.All(xs, x => Assert.True(x >= 0 && x == Math.Floor(x)));
    }

    [Fact]
    public void Poisson_IsContinuousAcrossTheMethodSwitch()
    {
        // The two methods meet at λ = 30: their means at 29.999 and 30 must agree within statistics.
        double Mean(double lambda, int seed)
        {
            var rng = new DefaultRandom(seed);
            double s = 0;
            for (int i = 0; i < N; i++) s += Sampling.Poisson(rng, lambda);
            return s / N;
        }
        double below = Mean(29.999, 7), above = Mean(30.0, 8);
        Stat.Within(above - below, 0.001, Math.Sqrt(2 * 30.0 / N), what: "mean step at λ = 30");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-3.0)]
    public void Poisson_OfNonPositiveMean_IsZero(double lambda)
        => Assert.Equal(0, Sampling.Poisson(new DefaultRandom(1), lambda));

    [Fact]
    public void Gaussian_IsStandardNormal()
    {
        var rng = new DefaultRandom(42);
        var xs = new double[N];
        for (int i = 0; i < N; i++) xs[i] = Sampling.Gaussian(rng);
        var (mean, variance) = Stat.Moments(xs);
        Stat.Within(mean, 0.0, Math.Sqrt(1.0 / N), what: "mean");
        Stat.Within(variance, 1.0, Math.Sqrt(2.0 / N), what: "variance");
        // Tail: P(|z| > 2) = 4.550 %.
        double tail = xs.Count(x => Math.Abs(x) > 2.0) / (double)N;
        Stat.Within(tail, 0.0455, Stat.BinomialSigma(0.0455, N), what: "P(|z| > 2)");
    }

    [Fact]
    public void UnitSphere_IsIsotropic()
    {
        var rng = new DefaultRandom(5);
        double sz = 0, sz2 = 0, up = 0;
        for (int i = 0; i < N; i++)
        {
            var v = rng.NextOnUnitSphere();
            Assert.Equal(1.0, v.Length, 9);
            sz += v.Z; sz2 += v.Z * v.Z;
            if (v.Z > 0.5) up++;
        }
        // Isotropic: z uniform on [−1, 1] → mean 0, E[z²] = 1/3, P(z > 0.5) = 1/4.
        Stat.Within(sz / N, 0.0, Math.Sqrt(1.0 / 3.0 / N), what: "E[z]");
        Stat.Within(sz2 / N, 1.0 / 3.0, Math.Sqrt((1.0 / 5.0 - 1.0 / 9.0) / N), what: "E[z²]");
        Stat.Within(up / N, 0.25, Stat.BinomialSigma(0.25, N), what: "P(z > 0.5)");
    }
}
