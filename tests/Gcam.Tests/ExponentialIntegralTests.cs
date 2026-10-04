using Gcam.Simulation;

namespace Gcam.Tests;

public sealed class ExponentialIntegralTests
{
    [Theory]
    [InlineData(1e-4)]
    [InlineData(.01)]
    [InlineData(.5)]
    [InlineData(1)]
    [InlineData(1.0000001)]
    [InlineData(3)]
    [InlineData(20)]
    public void E1_MatchesIndependentQuadrature(double x)
    {
        // E1(x) = ∫₀¹ e^(−x/μ)/μ dμ, substituted μ = e^(−s) → ∫₀^∞ exp(−x e^s) ds, which decays doubly
        // exponentially; composite Simpson with 200000 intervals on [0, 40] has error far below 1e-10 relative.
        const int intervals = 200000;
        double upper = 40, h = upper / intervals, sum = 0;
        for (int i = 0; i <= intervals; i++)
            sum += (i == 0 || i == intervals ? 1 : i % 2 == 1 ? 4 : 2) * Math.Exp(-x * Math.Exp(i * h));
        double quadrature = sum * h / 3;
        Assert.Equal(1, ExponentialIntegral.E1(x) / quadrature, 9);
    }

    [Fact]
    public void E1_RejectsNonPositiveArgument()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExponentialIntegral.E1(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExponentialIntegral.E1(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExponentialIntegral.E1(double.NaN));
    }
}
