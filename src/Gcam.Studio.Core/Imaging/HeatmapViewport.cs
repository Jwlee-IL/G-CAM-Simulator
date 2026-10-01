namespace Gcam.Studio.Core.Imaging;

/// <summary>A 2D point in some coordinate space (screen px, image px, or mm). Kept UI-free on purpose.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double k) => new(a.X * k, a.Y * k);
}

/// <summary>
/// Zoom/pan state for showing a W×H image grid inside a viewport, plus the conversions the heatmap control
/// needs: screen ↔ image pixel ↔ physical mm. Image row 0 is the BOTTOM (detector +y up), screen y grows down.
/// </summary>
/// <remarks>
/// Zoom is relative to "fit" (1 = the whole image fits, aspect preserved). Panning is clamped so the image
/// can't be dragged out of view. All maths lives here so it can be unit-tested without a UI stack.
/// </remarks>
public sealed class HeatmapViewport
{
    public const double MinZoom = 1.0;
    public const double MaxZoom = 32.0;

    public int ImageWidth { get; private set; } = 1;
    public int ImageHeight { get; private set; } = 1;
    public double ViewWidth { get; private set; } = 1;
    public double ViewHeight { get; private set; } = 1;

    /// <summary>Zoom relative to fit-to-view.</summary>
    public double Zoom { get; private set; } = MinZoom;

    /// <summary>Screen position (px) of the image's top-left corner.</summary>
    public Vec2 Offset { get; private set; }

    /// <summary>Screen pixels per image pixel.</summary>
    public double Scale => FitScale * Zoom;

    /// <summary>Device pixels per layout unit (1 at 100 % scaling, 1.25 at 125 %, …).</summary>
    public double PixelsPerDip { get; private set; } = 1;

    /// <summary>When set, fit uses a whole number of device pixels per cell so nearest-neighbour cells stay even.</summary>
    public bool SnapToWholePixels { get; private set; }

    private double FitScale
    {
        get
        {
            double raw = Math.Min(ViewWidth / ImageWidth, ViewHeight / ImageHeight);
            double device = raw * PixelsPerDip;
            return SnapToWholePixels && device >= 1 ? Math.Floor(device) / PixelsPerDip : raw;
        }
    }

    /// <summary>Resize the image and/or viewport. Resets to fit when the image size changes.</summary>
    public void Configure(int imageWidth, int imageHeight, double viewWidth, double viewHeight,
        double pixelsPerDip = 1, bool snapToWholePixels = false)
    {
        PixelsPerDip = pixelsPerDip > 0 ? pixelsPerDip : 1;
        SnapToWholePixels = snapToWholePixels;
        bool imageChanged = imageWidth != ImageWidth || imageHeight != ImageHeight;
        ImageWidth = Math.Max(1, imageWidth);
        ImageHeight = Math.Max(1, imageHeight);
        ViewWidth = Math.Max(1, viewWidth);
        ViewHeight = Math.Max(1, viewHeight);
        if (imageChanged || Zoom <= MinZoom) Reset();
        else Offset = Clamp(Offset);
    }

    /// <summary>Fit the whole image, centred.</summary>
    public void Reset()
    {
        Zoom = MinZoom;
        Offset = new Vec2((ViewWidth - ImageWidth * Scale) / 2, (ViewHeight - ImageHeight * Scale) / 2);
    }

    /// <summary>Zoom by <paramref name="factor"/> keeping the image point under <paramref name="anchor"/> fixed.</summary>
    public void ZoomAt(Vec2 anchor, double factor)
    {
        if (factor <= 0 || double.IsNaN(factor)) return;
        double newZoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        if (newZoom == Zoom) return;

        var imagePoint = (anchor - Offset) * (1 / Scale);   // in image px (screen orientation)
        Zoom = newZoom;
        Offset = Clamp(anchor - imagePoint * Scale);
        if (Zoom == MinZoom) Reset();
    }

    public void PanBy(Vec2 delta) => Offset = Clamp(Offset + delta);

    /// <summary>Screen px → continuous image coordinates (x right, y UP; pixel centres at i + 0.5).</summary>
    public Vec2 ScreenToImage(Vec2 screen)
    {
        var p = (screen - Offset) * (1 / Scale);
        return new Vec2(p.X, ImageHeight - p.Y);
    }

    /// <summary>Continuous image coordinates (y up) → screen px.</summary>
    public Vec2 ImageToScreen(Vec2 image) =>
        Offset + new Vec2(image.X, ImageHeight - image.Y) * Scale;

    /// <summary>The image pixel under <paramref name="screen"/>, or null if outside the image.</summary>
    public (int X, int Y)? PixelAt(Vec2 screen)
    {
        var p = ScreenToImage(screen);
        int x = (int)Math.Floor(p.X), y = (int)Math.Floor(p.Y);
        return x >= 0 && y >= 0 && x < ImageWidth && y < ImageHeight ? (x, y) : null;
    }

    /// <summary>
    /// Image coordinates → mm, where pixel index i has its centre at <c>originMm + i·stepMm</c> on both axes
    /// (the convention the decoder uses for the reconstruction grid).
    /// </summary>
    public static Vec2 ImageToMm(Vec2 image, double originMm, double stepMm) =>
        new(originMm + (image.X - 0.5) * stepMm, originMm + (image.Y - 0.5) * stepMm);

    public static Vec2 MmToImage(Vec2 mm, double originMm, double stepMm) =>
        new((mm.X - originMm) / stepMm + 0.5, (mm.Y - originMm) / stepMm + 0.5);

    // Keep at least half the view covered by the image when zoomed in; centre it when it's smaller than the view.
    private Vec2 Clamp(Vec2 offset)
    {
        double w = ImageWidth * Scale, h = ImageHeight * Scale;
        return new Vec2(ClampAxis(offset.X, w, ViewWidth), ClampAxis(offset.Y, h, ViewHeight));
    }

    private static double ClampAxis(double offset, double size, double view)
    {
        if (size <= view) return (view - size) / 2;
        return Math.Clamp(offset, view - size, 0);
    }
}
