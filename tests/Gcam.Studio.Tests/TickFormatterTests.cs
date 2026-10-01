using Gcam.Studio.Core.Imaging;

namespace Gcam.Studio.Tests;

public class TickFormatterTests
{
    [Fact]
    public void SmallValues_ShareOneMultiplier_AndOneDecimalCount()
    {
        // The flood map's real range: weights of ~0.0016 … 0.016 per crystal.
        var labels = TickFormatter.Labels(0.0016, 0.016, 5);
        Assert.Equal(["1.6", "5.2", "8.8", "12.4", "16.0 ×10⁻³"], labels);
    }

    [Fact]
    public void OrdinaryRange_HasNoMultiplier_AndNoNegativeZero()
    {
        var labels = TickFormatter.Labels(-1.21, 3.63, 5);
        Assert.Equal(["-1.21", "0.00", "1.21", "2.42", "3.63"], labels);
    }

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(5000, 0)]
    [InlineData(0.016, -3)]
    [InlineData(0.00016, -6)]
    [InlineData(25_000, 3)]
    [InlineData(0, 0)]
    public void SharedExponent_IsZeroForReadableValues_OtherwiseAMultipleOfThree(double maxAbs, int expected)
    {
        Assert.Equal(expected, TickFormatter.SharedExponent(maxAbs));
    }

    [Fact]
    public void FlatImage_GivesZeroDecimals_WithoutThrowing()
    {
        Assert.Equal(["7", "7"], TickFormatter.Labels(7, 7, 2));
    }
}
