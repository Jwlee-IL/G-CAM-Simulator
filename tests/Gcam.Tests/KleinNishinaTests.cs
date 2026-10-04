using Gcam.Core;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

public sealed class KleinNishinaTests
{
    [Theory]
    [InlineData(30.0)]
    [InlineData(661.7)]
    [InlineData(2614.5)]
    public void Sample_MatchesKleinNishinaAngularDistribution(double energyKeV)
    {
        // Expected bin probabilities from the analytic density by fine Simpson quadrature (2000 intervals per bin;
        // the density is smooth, so the quadrature error is many orders below the binomial σ). Tolerance: 4σ binomial
        // per bin for N = 200000 samples, seed 4101.
        const int n = 200000, bins = 20;
        var random = new DefaultRandom(4101);
        var counts = new long[bins];
        for (int i = 0; i < n; i++)
        {
            var (cosine, ratio) = KleinNishina.Sample(energyKeV, random);
            double k = energyKeV / KleinNishina.ElectronRestEnergyKeV;
            Assert.InRange(cosine, -1, 1);
            // Compton relation; cosθ is computed from the ratio, so only a few ulps of rounding separate them.
            Assert.Equal(1 + k * (1 - cosine), ratio, 1e-12 * ratio);
            counts[Math.Min(bins - 1, (int)((cosine + 1) / 2 * bins))]++;
        }
        double total = Integrate(-1, 1, energyKeV);
        for (int b = 0; b < bins; b++)
        {
            double p = Integrate(-1 + 2.0 * b / bins, -1 + 2.0 * (b + 1) / bins, energyKeV) / total;
            Stat.Within((double)counts[b] / n, p, Stat.BinomialSigma(p, n), what: $"KN bin {b} at {energyKeV} keV");
        }
    }

    [Fact]
    public void Rotate_KeepsUnitLengthAndScatteringAngle()
    {
        var random = new DefaultRandom(4102);
        for (int i = 0; i < 1000; i++)
        {
            var d = random.NextOnUnitSphere();
            double cosine = 2 * random.NextDouble() - 1, phi = 2 * Math.PI * random.NextDouble();
            var r = KleinNishina.Rotate(d, cosine, phi);
            Assert.Equal(1, r.Length, 12);
            Assert.Equal(cosine, r.Dot(d), 9);
        }
    }

    private static double Integrate(double a, double b, double energyKeV)
    {
        const int intervals = 2000;
        double h = (b - a) / intervals, sum = 0;
        for (int i = 0; i <= intervals; i++)
            sum += (i == 0 || i == intervals ? 1 : i % 2 == 1 ? 4 : 2) * KleinNishina.Density(a + i * h, energyKeV);
        return sum * h / 3;
    }
}
