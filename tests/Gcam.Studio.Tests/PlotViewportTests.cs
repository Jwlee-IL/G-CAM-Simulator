using Gcam.Studio.Core.Plotting;

namespace Gcam.Studio.Tests;

public sealed class PlotViewportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mapping_RoundTrips_AndClampsLogFloor(bool log)
    {
        var v = new PlotViewport();
        v.Configure(-10, 70, log ? 1 : -20, 1000, log);
        foreach (double x in new[] { -10.0, 0, 70 }) Assert.Equal(x, v.PixelToX(v.XToPixel(x, 800), 800), 10);
        foreach (double y in new[] { 1.0, 10, 1000 }) Assert.Equal(y, v.PixelToY(v.YToPixel(y, 600), 600), 9);
        if (log)
        {
            Assert.Equal(600, v.YToPixel(0, 600));
            Assert.Equal(600, v.YToPixel(-10, 600));
        }
    }

    [Fact]
    public void ZoomPanReset_PreservesAnchor_AndBounds()
    {
        var v = new PlotViewport(); v.Configure(0, 100, 0, 10, false);
        v.ZoomAt(25, 2);
        Assert.Equal(200, v.XToPixel(25, 800));
        Assert.Equal(2, v.Zoom);
        v.Pan(-10000); Assert.Equal(0, v.XMin);
        v.Pan(10000); Assert.Equal(100, v.XMax);
        v.Reset(); Assert.Equal(0, v.XMin); Assert.Equal(100, v.XMax);
        v.Configure(0, 0, 0, 0, true);
        Assert.True(double.IsFinite(v.YToPixel(0, 100)));
    }
}
