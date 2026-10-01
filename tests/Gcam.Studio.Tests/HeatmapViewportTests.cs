using Gcam.Studio.Core.Imaging;

namespace Gcam.Studio.Tests;

public class HeatmapViewportTests
{
    private static HeatmapViewport Fit30In300x200()
    {
        var v = new HeatmapViewport();
        v.Configure(30, 30, 300, 200);
        return v;
    }

    [Fact]
    public void Fit_preserves_aspect_and_centres()
    {
        var v = Fit30In300x200();
        Assert.Equal(200.0 / 30, v.Scale, 9);          // limited by height
        Assert.Equal(50, v.Offset.X, 9);               // (300 - 200) / 2
        Assert.Equal(0, v.Offset.Y, 9);
    }

    [Fact]
    public void Screen_and_image_round_trip_with_y_up()
    {
        var v = Fit30In300x200();
        // Top-left screen corner of the image is image (0, H) because row 0 is at the bottom.
        var topLeft = v.ScreenToImage(new Vec2(50, 0));
        Assert.Equal(0, topLeft.X, 9);
        Assert.Equal(30, topLeft.Y, 9);

        var p = new Vec2(12.25, 7.5);
        var back = v.ScreenToImage(v.ImageToScreen(p));
        Assert.Equal(p.X, back.X, 9);
        Assert.Equal(p.Y, back.Y, 9);
    }

    [Fact]
    public void Pixel_lookup_respects_bounds_and_orientation()
    {
        var v = Fit30In300x200();
        Assert.Equal((0, 29), v.PixelAt(new Vec2(51, 1)));     // top-left screen = top row = y 29
        Assert.Equal((29, 0), v.PixelAt(new Vec2(249, 199)));  // bottom-right
        Assert.Null(v.PixelAt(new Vec2(10, 100)));             // left letterbox
    }

    [Fact]
    public void Zoom_keeps_the_anchor_point_fixed()
    {
        var v = Fit30In300x200();
        var anchor = new Vec2(180, 60);
        var before = v.ScreenToImage(anchor);

        v.ZoomAt(anchor, 4);

        Assert.Equal(4, v.Zoom, 9);
        var after = v.ScreenToImage(anchor);
        Assert.Equal(before.X, after.X, 6);
        Assert.Equal(before.Y, after.Y, 6);
    }

    [Fact]
    public void Zoom_is_clamped_and_zooming_out_fully_refits()
    {
        var v = Fit30In300x200();
        v.ZoomAt(new Vec2(150, 100), 1000);
        Assert.Equal(HeatmapViewport.MaxZoom, v.Zoom);

        v.ZoomAt(new Vec2(10, 10), 1e-6);
        Assert.Equal(HeatmapViewport.MinZoom, v.Zoom);
        Assert.Equal(50, v.Offset.X, 9);   // back to centred fit, not stuck where the anchor was
    }

    [Fact]
    public void Pan_cannot_drag_the_image_out_of_view()
    {
        var v = Fit30In300x200();
        v.PanBy(new Vec2(500, 500));           // at fit, the image stays centred
        Assert.Equal(50, v.Offset.X, 9);

        v.ZoomAt(new Vec2(150, 100), 4);       // image 800×800 in a 300×200 view
        v.PanBy(new Vec2(10_000, 10_000));
        Assert.Equal(0, v.Offset.X, 9);
        Assert.Equal(0, v.Offset.Y, 9);
        v.PanBy(new Vec2(-10_000, -10_000));
        Assert.Equal(300 - 800, v.Offset.X, 9);
        Assert.Equal(200 - 800, v.Offset.Y, 9);
    }

    [Fact]
    public void Mm_mapping_puts_pixel_centres_on_the_grid()
    {
        // Pixel i centred at origin + i·step, so the centre of pixel 0 (image 0.5) is the origin.
        var mm = HeatmapViewport.ImageToMm(new Vec2(0.5, 0.5), originMm: -8.7, stepMm: 0.6);
        Assert.Equal(-8.7, mm.X, 9);
        var centre29 = HeatmapViewport.ImageToMm(new Vec2(29.5, 29.5), -8.7, 0.6);
        Assert.Equal(8.7, centre29.X, 9);   // symmetric 30-pixel detector

        var back = HeatmapViewport.MmToImage(mm, -8.7, 0.6);
        Assert.Equal(0.5, back.X, 9);
    }

    [Theory]
    [InlineData(366, 1.0, 12.0)]      // 366/30 = 12.2 → 12 px per cell
    [InlineData(366, 1.25, 12.0)]     // 15.25 device px → 15 → 12 dip
    [InlineData(20, 1.0, 20.0 / 30)]  // smaller than one px per cell: no snapping
    public void Snapping_fits_a_whole_number_of_device_pixels_per_cell(double view, double ppd, double expectedScale)
    {
        var v = new HeatmapViewport();
        v.Configure(30, 30, view, view, ppd, snapToWholePixels: true);
        Assert.Equal(expectedScale, v.Scale, 9);
        Assert.Equal((view - 30 * v.Scale) / 2, v.Offset.X, 9);   // still centred
    }

    [Fact]
    public void Resizing_the_view_keeps_zoom_but_new_image_size_refits()
    {
        var v = Fit30In300x200();
        v.ZoomAt(new Vec2(150, 100), 2);
        v.Configure(30, 30, 600, 400);
        Assert.Equal(2, v.Zoom, 9);

        v.Configure(64, 64, 600, 400);
        Assert.Equal(1, v.Zoom, 9);
    }
}
