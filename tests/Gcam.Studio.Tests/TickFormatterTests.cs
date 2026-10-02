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

    [Fact]
    public void StepLabels_TakeDecimalsFromTheStep_AndGroupThousands()
    {
        Assert.Equal(["0", "10", "20"], TickFormatter.StepLabels(0, 10, 3));          // counts: no ".0"
        Assert.Equal(["-2", "0", "2", "4"], TickFormatter.StepLabels(-2, 2, 4));      // µs axis
        Assert.Equal(["0.5", "1.0", "1.5"], TickFormatter.StepLabels(0.5, 0.5, 3));   // a 0.5 step needs one decimal
        Assert.Equal(["1,000", "2,000", "3,000"], TickFormatter.StepLabels(1000, 1000, 3));
        Assert.Equal(["10", "20 ×10³"], TickFormatter.StepLabels(10_000, 10_000, 2));
    }

    [Theory]
    [InlineData(18.5323, "18.53")]
    [InlineData(3077, "3,077")]
    [InlineData(0.2750, "0.275")]
    [InlineData(10, "10")]
    [InlineData(-0.00001, "-0.00001")]
    [InlineData(0, "0")]
    [InlineData(123456, "123,456")]
    public void Significant_FourDigitsGroupedWithoutTrailingZeros(double value, string expected)
    {
        Assert.Equal(expected, NumberFormat.Significant(value));
    }

    [Fact]
    public void GroupedFraction_KeepsResolution_InGroupsOfThree()
    {
        Assert.Equal("0.853 889 538", NumberFormat.GroupedFraction(0.853889538, 9));
        Assert.Equal("12.5", NumberFormat.GroupedFraction(12.5, 1));
    }
}
