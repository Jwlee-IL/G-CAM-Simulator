using Gcam.Core;
using Gcam.Detector;
using Gcam.Tests.Harness;
using Xunit.Abstractions;

namespace Gcam.Tests;

/// <summary>Properties of <see cref="DefaultRandom"/> the engine relies on (TODO-26). The old seeded System.Random was
/// affine in its seed: streams whose seeds differ by d were the same stream shifted mod 1 (draw-for-draw correlation
/// up to 0.99), which the engine's "seed + offset" streams and per-item reseeding silently assumed away. Tolerances
/// are k = 4 standard errors of each estimator.</summary>
public class RandomQualityTests(ITestOutputHelper output)
{
    // Pearson correlation of draw j of seed s with draw j of seed s + d, over M base seeds. Independent streams give
    // r ~ N(0, 1/M); the legacy generator fails this by +0.5 … +0.99 for every d tested in the review.
    [Theory]
    [InlineData(1)]
    [InlineData(777)]        // the cascade-stream offset
    [InlineData(104_729)]    // the old per-event MeasurementStage stride
    public void NearbySeeds_GiveUncorrelatedStreams(int d)
    {
        const int m = 4000;
        foreach (int j in new[] { 0, 1, 54 })
        {
            var x = new double[m];
            var y = new double[m];
            for (int b = 0; b < m; b++)
            {
                int s = 1000 + 7919 * b;
                x[b] = Draw(new DefaultRandom(s), j);
                y[b] = Draw(new DefaultRandom(s + d), j);
            }
            double r = Correlation(x, y);
            output.WriteLine($"d={d} j={j}: corr={r:+0.0000;-0.0000} (σ={1.0 / Math.Sqrt(m):F4})");
            Stat.Within(r, 0.0, 1.0 / Math.Sqrt(m), what: $"corr(u_{j}(s), u_{j}(s+{d}))");
        }
    }

    // Within one stream, successive draws are uncorrelated (lag 1 … 3).
    [Fact]
    public void SuccessiveDraws_AreUncorrelated()
    {
        const int n = 400_000;
        var rng = new DefaultRandom(26);
        var u = new double[n];
        for (int i = 0; i < n; i++) u[i] = rng.NextDouble();
        for (int lag = 1; lag <= 3; lag++)
        {
            output.WriteLine($"lag {lag}: corr={Correlation(u[..^lag], u[lag..]):+0.00000;-0.00000} (σ={1.0 / Math.Sqrt(n - lag):F5})");
            Stat.Within(Correlation(u[..^lag], u[lag..]), 0.0, 1.0 / Math.Sqrt(n - lag), what: $"lag-{lag} correlation");
        }
    }

    [Fact]
    public void UnseededStream_ExposesAReplayableSeed()
    {
        var a = new DefaultRandom();
        var b = DefaultRandom.FromKey(a.Seed);
        for (int i = 0; i < 100; i++) Assert.Equal(a.NextDouble(), b.NextDouble());
        Assert.Equal(unchecked((ulong)(long)-5), new DefaultRandom(-5).Seed);
        Assert.NotEqual(DefaultRandom.Key(1, 0), DefaultRandom.Key(0, 1));
    }

    [Fact]
    public void NextDouble_IsInTheUnitInterval_WithMeanAndVarianceOfAUniform()
    {
        const int n = 400_000;
        var rng = new DefaultRandom(7);
        var u = new double[n];
        for (int i = 0; i < n; i++)
        {
            u[i] = rng.NextDouble();
            Assert.True(u[i] >= 0.0 && u[i] < 1.0, $"draw {i} = {u[i]} outside [0, 1)");
        }
        var (mean, variance) = Stat.Moments(u);
        output.WriteLine($"mean {mean:F6}, variance {variance:F6}");
        Stat.Within(mean, 0.5, Math.Sqrt(1.0 / 12.0 / n), what: "mean");
        // Var of the sample variance of U(0,1): (μ4 − σ⁴)/n with μ4 = 1/80, σ⁴ = 1/144.
        Stat.Within(variance, 1.0 / 12.0, Math.Sqrt((1.0 / 80 - 1.0 / 144) / n), what: "variance");
    }

    // The Klein–Nishina rejection sampler (the engine's main data-dependent draw loop) against its quadrature: mean
    // scattered-energy fraction ε and backscatter fraction at 662 keV. A correctness check of the sampler on the new
    // generator; at this N it cannot resolve the legacy generator's −0.010 % bias (review P3 needed 4·10⁸).
    [Fact]
    public void KleinNishina_MatchesQuadrature()
    {
        const double e = 661.7;
        double a = e / ComptonModel.MeC2;
        double z = 0, me = 0, pb = 0;
        const int steps = 200_000;
        for (int i = 0; i <= steps; i++)
        {
            double c = -1 + 2.0 * i / steps, w = i == 0 || i == steps ? 1 : i % 2 == 1 ? 4 : 2;
            double eps = 1 / (1 + a * (1 - c));
            double f = eps * eps * (eps + 1 / eps - (1 - c * c)) * w;
            z += f; me += f * eps; if (c < 0) pb += f;
        }
        double refEps = me / z, refBack = pb / z;

        const int n = 2_000_000;
        var rng = new DefaultRandom(662);
        var dir = new Vector3(0, 0, -1);
        double sum = 0, sum2 = 0;
        long back = 0;
        for (int i = 0; i < n; i++)
        {
            var (_, newE, newDir) = ComptonModel.Scatter(e, dir, rng);
            double eps = newE / e;
            sum += eps; sum2 += eps * eps;
            if (newDir.Z > 0) back++;
        }
        double mean = sum / n, sd = Math.Sqrt((sum2 / n - mean * mean) / n);
        output.WriteLine($"KN mean ε {mean:F6} ± {sd:F6} vs quadrature {refEps:F6} ({(mean - refEps) / sd:+0.0;-0.0}σ); backscatter {back / (double)n:F6} vs {refBack:F6} ({(back / (double)n - refBack) / Stat.BinomialSigma(refBack, n):+0.0;-0.0}σ)");
        Stat.Within(mean, refEps, sd, what: "KN mean ε at 662 keV");
        Stat.Within(back / (double)n, refBack, Stat.BinomialSigma(refBack, n), what: "KN backscatter fraction at 662 keV");
    }

    private static double Draw(IRandom rng, int j)
    {
        double u = 0;
        for (int k = 0; k <= j; k++) u = rng.NextDouble();
        return u;
    }

    private static double Correlation(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < x.Count; i++)
        {
            double dx = x[i] - mx, dy = y[i] - my;
            sxy += dx * dy; sxx += dx * dx; syy += dy * dy;
        }
        return sxy / Math.Sqrt(sxx * syy);
    }
}
