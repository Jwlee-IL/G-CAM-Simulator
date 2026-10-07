using System.Reflection;
using System.Windows;
using Gcam.Core;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.Imaging;

namespace Gcam.Studio.RenderTests;

public sealed partial class PlotViewRenderTests
{
    /// <summary>TODO-38: a live acquisition replaces the reconstruction four times a second. A refresh of the same size keeps
    /// the viewport, so the hovered readout must survive it and show the new value; a new size refits the viewport and drops
    /// the hover. Exact strings: the readout is "x … mm, y … mm · value unit" in the invariant culture.</summary>
    private static void CheckHeatmapReadoutAcrossRefresh()
    {
        var view = new HeatmapView { OriginMm = -4.5, StepMm = 1, ValueUnit = "(MLEM λ)" };
        DetectorImage Image(int n, double value)
        {
            var img = new DetectorImage(n, n);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) img[x, y] = value + x + 10 * y;
            return img;
        }
        view.Image = Image(10, 100);
        view.Measure(new Size(300, 300));
        view.Arrange(new Rect(0, 0, 300, 300));
        view.UpdateLayout();
        var hover = typeof(HeatmapView).GetMethod("HoverAt", BindingFlags.Instance | BindingFlags.NonPublic)!;
        hover.Invoke(view, [view.MmToScreen(new Vec2(0.5, 0.5))]);   // pixel (5, 5): centre 0.5 mm
        Assert.Equal("x 0.5 mm, y 0.5 mm · 155 (MLEM λ)", view.Readout);
        view.Image = Image(10, 200);                                   // same size: kept, re-read
        Assert.Equal("x 0.5 mm, y 0.5 mm · 255 (MLEM λ)", view.Readout);
        view.StepMm = 2;                                               // a re-gridded projection of the same size
        Assert.Equal("x 5.5 mm, y 5.5 mm · 255 (MLEM λ)", view.Readout);   // origin + 5 · step
        view.Image = Image(12, 300);                                   // new size: refit, hover dropped
        Assert.Equal("", view.Readout);
    }
}
