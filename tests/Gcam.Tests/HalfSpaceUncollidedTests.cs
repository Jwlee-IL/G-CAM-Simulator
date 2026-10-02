using Gcam.Core;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

public sealed class HalfSpaceUncollidedTests
{
    [Theory]
    [InlineData(.0001)]
    [InlineData(.003)]
    [InlineData(.03)]
    public void UniformHalfSpace_FluenceAndZenithMatchIndependentAnalogAirSurvival(double airMu)
    {
        // Synthetic coefficients, not a material preset or terrestrial field: yield .3, density 1.7,
        // soil mu .12/cm, air mu as supplied, height 100 cm. MC N=200000, 16 zenith bins.
        const int n = 200000, bins = 16;
        const double prefactor = .3 * 1.7 / (2000 * .12);
        var mc = HalfSpaceUncollided.Measure(.3, 1.7, .12, airMu, 100, n, bins, new DefaultRandom(7283));
        double analytic = HalfSpaceUncollided.FluencePerBqKg(.3, 1.7, .12, airMu, 100);
        double totalProbability = analytic / prefactor;
        Stat.Within(mc.FluencePerBqKg, analytic, prefactor * Math.Sqrt(totalProbability * (1 - totalProbability) / n), 6,
            "uncollided scalar fluence");
        for (int i = 0; i < bins; i++)
        {
            double expected = HalfSpaceUncollided.FluencePerBqKg(.3, 1.7, .12, airMu, 100, (double)i / bins, (double)(i + 1) / bins);
            double p = expected / prefactor;
            if (n * p < 1e-10) { Assert.Equal(0, mc.ZenithCounts[i]); continue; }
            // Each bin is a binomial indicator over ALL proposals; no renormalization by the random survivor count.
            Stat.Within(prefactor * mc.ZenithCounts[i] / n, expected, prefactor * Math.Sqrt(p * (1 - p) / n), 6,
                $"zenith bin {i}");
        }
    }

    [Fact]
    public void ZeroAirAttenuation_RetainsInfiniteHalfSpaceNormalizationAndMassUnits()
    {
        // Soil density cancels exactly when linear attenuation is density times a fixed mass coefficient.
        double first = HalfSpaceUncollided.FluencePerBqKg(1, 1.5, 1.5 * .08, 0, 100);
        double second = HalfSpaceUncollided.FluencePerBqKg(1, 2, 2 * .08, 0, 100);
        Assert.Equal(1 / (2000 * .08), first); Assert.Equal(first, second);
        var mc = HalfSpaceUncollided.Measure(1, 1.5, 1.5 * .08, 0, 100, 1000, 10, new DefaultRandom(3281));
        Assert.Equal(1000, mc.ZenithCounts.Sum()); Assert.Equal(first, mc.FluencePerBqKg); Assert.Equal(0, mc.StandardErrorPerBqKg);
    }
}
