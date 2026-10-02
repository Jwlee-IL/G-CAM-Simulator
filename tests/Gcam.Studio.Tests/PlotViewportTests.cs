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
    public void LogX_MapsDecadesEqually_RoundTrips_AndZoomsPansInLogSpace()
    {
        var v = new PlotViewport(); v.Configure(100, 10_000, 0, 1, false, logX: true);
        Assert.True(v.LogX);
        Assert.Equal(0, v.XToPixel(100, 800), 9);
        Assert.Equal(400, v.XToPixel(1000, 800), 9);   // one of two decades = half the width
        Assert.Equal(800, v.XToPixel(10_000, 800), 9);
        foreach (double x in new[] { 100.0, 317, 3000, 10_000 }) Assert.Equal(x, v.PixelToX(v.XToPixel(x, 800), 800), 8);
        v.ZoomAt(1000, 2);                               // anchor stays at its pixel, span halves in log space
        Assert.Equal(400, v.XToPixel(1000, 800), 9);
        Assert.Equal(2, v.Zoom, 9);
        Assert.Equal(Math.Pow(10, 2.5), v.XMin, 6); Assert.Equal(Math.Pow(10, 3.5), v.XMax, 6);
        v.PanFraction(-10); Assert.Equal(100, v.XMin); Assert.Equal(1000, v.XMax, 9);
        v.PanFraction(10); Assert.Equal(10_000, v.XMax); Assert.Equal(1000, v.XMin, 9);
        v.SetRange(new(200, 2000)); Assert.Equal(200, v.XMin, 9); Assert.Equal(2000, v.XMax, 9);
        v.Reset(); Assert.Equal(100, v.XMin); Assert.Equal(10_000, v.XMax);
        v.Configure(-5, 10, 0, 1, false, logX: true);    // a non-positive range cannot be log: stays linear
        Assert.False(v.LogX); Assert.Equal(400, v.XToPixel(2.5, 800), 9);
    }

    [Fact]
    public void PanFraction_OnLinearAxis_EqualsDataDeltaPan()
    {
        var a = new PlotViewport(); a.Configure(0, 100, 0, 1, false); a.ZoomAt(50, 4);
        var b = new PlotViewport(); b.Configure(0, 100, 0, 1, false); b.ZoomAt(50, 4);
        a.PanFraction(0.2); b.Pan(0.2 * (b.XMax - b.XMin));
        Assert.Equal(b.XMin, a.XMin, 12); Assert.Equal(b.XMax, a.XMax, 12);
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
