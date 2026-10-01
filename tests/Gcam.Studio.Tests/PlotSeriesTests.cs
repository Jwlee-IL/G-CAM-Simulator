using Gcam.Studio.Core.Plotting;

namespace Gcam.Studio.Tests;

public sealed class PlotSeriesTests
{
    [Fact]
    public void LowerBound_HandlesUniformAndIrregularX_AndEnds()
    {
        var regular = new PlotSeries("ADC", [1, 2, 3], Origin: 10, Step: 2);
        regular.Validate();
        Assert.Equal(2, regular.LowerBound(13));
        Assert.Equal(0, regular.LowerBound(-100));
        Assert.Equal(3, regular.LowerBound(100));
        var irregular = new PlotSeries("Spectrum", [1, 2, 3], [10, 11, 30], Kind: PlotKind.Area);
        irregular.Validate(); Assert.Equal(2, irregular.LowerBound(12));
        Assert.Throws<ArgumentException>(() => (irregular with { X = [10, 10, 30] }).Validate());
        Assert.Throws<ArgumentException>(() => (irregular with { X = [10] }).Validate());
    }
}
