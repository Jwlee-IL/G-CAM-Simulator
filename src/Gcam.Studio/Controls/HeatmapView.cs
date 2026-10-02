using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gcam.Core;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Rendering;

namespace Gcam.Studio.Controls;

/// <summary>
/// Draws a <see cref="DetectorImage"/> as a heatmap with zoom (wheel, +/−), pan (drag, arrows) and a live
/// readout of the hovered pixel in mm. Built on <see cref="FrameworkElement"/> + <see cref="WriteableBitmap"/>:
/// the bitmap is rebuilt only when the data changes, and zoom/pan only change the destination rectangle.
/// </summary>
/// <remarks>Geometry (fit, zoom about a point, pan limits, screen↔mm) lives in <see cref="HeatmapViewport"/>.</remarks>
public sealed class HeatmapView : FrameworkElement
{
    private const double WheelStep = 1.25;
    private const double KeyPanPx = 40;

    private readonly HeatmapViewport _viewport = new();
    private WriteableBitmap? _bitmap;
    private double _min, _max;
    private Point? _dragFrom;
    private (int X, int Y)? _hover;

    static HeatmapView()
    {
        FocusableProperty.OverrideMetadata(typeof(HeatmapView), new FrameworkPropertyMetadata(true));
        FocusVisualStyleProperty.OverrideMetadata(typeof(HeatmapView), new FrameworkPropertyMetadata(null));
        ClipToBoundsProperty.OverrideMetadata(typeof(HeatmapView), new FrameworkPropertyMetadata(true));
    }

    public HeatmapView()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    // ---- dependency properties ---------------------------------------------------------------------

    public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(
        nameof(Image), typeof(DetectorImage), typeof(HeatmapView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure,
            (d, _) => ((HeatmapView)d).OnImageChanged()));

    /// <summary>The grid to draw (row 0 at the bottom).</summary>
    public DetectorImage? Image { get => (DetectorImage?)GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    public static readonly DependencyProperty OriginMmProperty = DependencyProperty.Register(
        nameof(OriginMm), typeof(double), typeof(HeatmapView), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Centre of pixel 0 in mm (both axes).</summary>
    public double OriginMm { get => (double)GetValue(OriginMmProperty); set => SetValue(OriginMmProperty, value); }

    public static readonly DependencyProperty StepMmProperty = DependencyProperty.Register(
        nameof(StepMm), typeof(double), typeof(HeatmapView), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Pixel spacing in mm.</summary>
    public double StepMm { get => (double)GetValue(StepMmProperty); set => SetValue(StepMmProperty, value); }

    public static readonly DependencyProperty ValueUnitProperty = DependencyProperty.Register(
        nameof(ValueUnit), typeof(string), typeof(HeatmapView),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((HeatmapView)d).RefreshReadout()));

    /// <summary>Value suffix for the hovered pixel, such as counts or (decoded).</summary>
    public string ValueUnit { get => (string)GetValue(ValueUnitProperty); set => SetValue(ValueUnitProperty, value); }

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(HeatmapView), new FrameworkPropertyMetadata("No data", FrameworkPropertyMetadataOptions.AffectsRender));

    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(HeatmapView),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Colour of the overlay text (readout, zoom, empty state).</summary>
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public static readonly DependencyProperty ShowReadoutOverlayProperty = DependencyProperty.Register(
        nameof(ShowReadoutOverlay), typeof(bool), typeof(HeatmapView), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Draw the hovered-pixel readout inside the image (turn off when the host shows <see cref="Readout"/> itself).</summary>
    public bool ShowReadoutOverlay { get => (bool)GetValue(ShowReadoutOverlayProperty); set => SetValue(ShowReadoutOverlayProperty, value); }

    public static readonly DependencyProperty FrameBrushProperty = DependencyProperty.Register(
        nameof(FrameBrush), typeof(Brush), typeof(HeatmapView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>1 px outline around the image, so dark colormap ends don't melt into a dark background.</summary>
    public Brush? FrameBrush { get => (Brush?)GetValue(FrameBrushProperty); set => SetValue(FrameBrushProperty, value); }

    public static readonly DependencyProperty FocusBrushProperty = DependencyProperty.Register(
        nameof(FocusBrush), typeof(Brush), typeof(HeatmapView), new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Keyboard focus ring colour.</summary>
    public Brush FocusBrush { get => (Brush)GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    private static readonly DependencyPropertyKey DataMinPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(DataMin), typeof(double), typeof(HeatmapView), new FrameworkPropertyMetadata(0.0));

    public static readonly DependencyProperty DataMinProperty = DataMinPropertyKey.DependencyProperty;

    /// <summary>Value mapped to the bottom of the colormap (bind a <see cref="ColorBar"/> to it).</summary>
    public double DataMin => (double)GetValue(DataMinProperty);

    private static readonly DependencyPropertyKey DataMaxPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(DataMax), typeof(double), typeof(HeatmapView), new FrameworkPropertyMetadata(1.0));

    public static readonly DependencyProperty DataMaxProperty = DataMaxPropertyKey.DependencyProperty;

    /// <summary>Value mapped to the top of the colormap.</summary>
    public double DataMax => (double)GetValue(DataMaxProperty);

    private static readonly DependencyPropertyKey ReadoutPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Readout), typeof(string), typeof(HeatmapView), new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty ReadoutProperty = ReadoutPropertyKey.DependencyProperty;

    /// <summary>"x, y mm · value" for the hovered pixel — bindable, e.g. to a status line.</summary>
    public string Readout => (string)GetValue(ReadoutProperty);

    /// <summary>Current zoom relative to fit (1 = whole image).</summary>
    public double Zoom => _viewport.Zoom;

    // ---- view mapping (for overlays such as MeasurementAdorner) -----------------------------------

    /// <summary>Raised after anything that moves the image on screen: new data, resize, DPI, zoom or pan.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>True once there is an image to map coordinates onto.</summary>
    public bool HasImage => _bitmap is not null && Image is not null;

    /// <summary>Screen point (element coordinates) → mm in the image's frame.</summary>
    public Vec2 ScreenToMm(Point screen) =>
        HeatmapViewport.ImageToMm(_viewport.ScreenToImage(new Vec2(screen.X, screen.Y)), OriginMm, StepMm);

    /// <summary>mm in the image's frame → screen point (element coordinates).</summary>
    public Point MmToScreen(Vec2 mm)
    {
        var p = _viewport.ImageToScreen(HeatmapViewport.MmToImage(mm, OriginMm, StepMm));
        return new Point(p.X, p.Y);
    }

    /// <summary>mm extent of the image's outer edges (not pixel centres): (min corner, max corner).</summary>
    public (Vec2 Min, Vec2 Max) ExtentMm
    {
        get
        {
            var img = Image;
            if (img is null) return default;
            return (HeatmapViewport.ImageToMm(new Vec2(0, 0), OriginMm, StepMm),
                    HeatmapViewport.ImageToMm(new Vec2(img.Width, img.Height), OriginMm, StepMm));
        }
    }

    /// <summary>One wheel notch of zoom about <paramref name="anchor"/> — lets an overlay forward the wheel.</summary>
    internal void ZoomStep(Point anchor, bool zoomIn)
    {
        if (Image is null) return;
        _viewport.ZoomAt(new Vec2(anchor.X, anchor.Y), zoomIn ? WheelStep : 1 / WheelStep);
        UpdateHover(anchor);
        OnViewChanged();
    }

    /// <summary>Show the readout for <paramref name="screen"/> (null clears it) while an overlay owns the mouse.</summary>
    internal void HoverAt(Point? screen)
    {
        if (screen is { } p) { UpdateHover(p); return; }
        _hover = null;
        SetValue(ReadoutPropertyKey, string.Empty);
        InvalidateVisual();
    }

    private void OnViewChanged()
    {
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- data --------------------------------------------------------------------------------------

    private void OnImageChanged()
    {
        _hover = null;
        SetValue(ReadoutPropertyKey, string.Empty);
        var img = Image;
        if (img is null) { _bitmap = null; OnViewChanged(); return; }

        _min = double.MaxValue; _max = double.MinValue;
        foreach (var v in img.Raw) { if (v < _min) _min = v; if (v > _max) _max = v; }
        double span = _max > _min ? _max - _min : 1;
        SetValue(DataMinPropertyKey, _min);
        SetValue(DataMaxPropertyKey, _max);

        if (_bitmap is null || _bitmap.PixelWidth != img.Width || _bitmap.PixelHeight != img.Height)
            _bitmap = new WriteableBitmap(img.Width, img.Height, 96, 96, PixelFormats.Bgra32, null);

        var lut = Colormap.Viridis;
        var pixels = new uint[img.Width * img.Height];
        for (int y = 0; y < img.Height; y++)
        {
            int row = (img.Height - 1 - y) * img.Width;   // flip: row 0 of the data is the bottom of the screen
            for (int x = 0; x < img.Width; x++)
                pixels[row + x] = lut[(int)Math.Round((img[x, y] - _min) / span * 255)];
        }
        _bitmap.WritePixels(new Int32Rect(0, 0, img.Width, img.Height), pixels, img.Width * 4, 0);
        ConfigureViewport();
    }

    // ---- rendering ---------------------------------------------------------------------------------

    /// <summary>
    /// Asks for the size the image draws at fit (whole device pixels per cell), so a host such as ImageStackPanel can
    /// size the colour bar to the image rather than to the space offered. Without an image the whole space is used
    /// for the empty-state text.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? w : availableSize.Height;
        if (Image is not { } img || w <= 0 || h <= 0) return new Size(w, h);
        double scale = HeatmapViewport.FitScaleFor(img.Width, img.Height, w, h, VisualTreeHelper.GetDpi(this).PixelsPerDip, snapToWholePixels: true);
        return new Size(Math.Min(w, img.Width * scale), Math.Min(h, img.Height * scale));
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ConfigureViewport();
    }

    // Whole device pixels per cell at fit, so nearest-neighbour cells stay equal width at any DPI.
    private void ConfigureViewport()
    {
        if (Image is { } img)
            _viewport.Configure(img.Width, img.Height, ActualWidth, ActualHeight, VisualTreeHelper.GetDpi(this).PixelsPerDip, snapToWholePixels: true);
        OnViewChanged();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ConfigureViewport();
    }

    protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusedChanged(e);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        // Transparent fill so the whole area is hit-testable (wheel/drag work on the letterbox too).
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

        if (_bitmap is null || Image is null)
        {
            var empty = Text(EmptyText, 13);
            // Wrap inside the pane: a narrow column must not clip the empty-state sentence at both edges.
            // Centre alignment places each line inside the MaxTextWidth box, so the box (not the text) is centred.
            double boxWidth = empty.Width;
            if (ActualWidth > 24)
            {
                empty.MaxTextWidth = ActualWidth - 24;
                empty.TextAlignment = TextAlignment.Center;
                boxWidth = empty.MaxTextWidth;
            }
            dc.DrawText(empty, new Point((ActualWidth - boxWidth) / 2, (ActualHeight - empty.Height) / 2));
            DrawFocusRing(dc);
            return;
        }

        double s = _viewport.Scale;
        var origin = new Point(_viewport.Offset.X, _viewport.Offset.Y);
        var imageRect = new Rect(origin, new Size(_bitmap.PixelWidth * s, _bitmap.PixelHeight * s));
        dc.DrawImage(_bitmap, imageRect);
        if (FrameBrush is { } frame)
        {
            var framePen = new Pen(frame, 1);
            framePen.Freeze();
            imageRect.Intersect(new Rect(RenderSize));
            if (!imageRect.IsEmpty) dc.DrawRectangle(null, framePen, Inset(imageRect, 0.5));
        }

        if (_hover is { } h)
        {
            var tl = _viewport.ImageToScreen(new Vec2(h.X, h.Y + 1));
            var pen = new Pen(Brushes.White, 1.5);
            pen.Freeze();
            dc.DrawRectangle(null, pen, new Rect(tl.X, tl.Y, s, s));
        }

        if (_viewport.Zoom > HeatmapViewport.MinZoom)
        {
            var z = Text($"×{_viewport.Zoom:0.#}", 11);
            DrawLabel(dc, z, new Point(ActualWidth - z.Width - 12, 8));
        }
        if (ShowReadoutOverlay && Readout.Length > 0)
        {
            var r = Text(Readout, 11);
            DrawLabel(dc, r, new Point(8, ActualHeight - r.Height - 10));
        }
        DrawFocusRing(dc);
    }

    private static Rect Inset(Rect r, double d) => new(r.X + d, r.Y + d, Math.Max(0, r.Width - 2 * d), Math.Max(0, r.Height - 2 * d));

    private void DrawFocusRing(DrawingContext dc)
    {
        if (!IsKeyboardFocused) return;
        var pen = new Pen(FocusBrush, 2);
        pen.Freeze();
        dc.DrawRectangle(null, pen, Inset(new Rect(RenderSize), 1));
    }

    private void DrawLabel(DrawingContext dc, FormattedText text, Point at)
    {
        var bg = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
        bg.Freeze();
        dc.DrawRoundedRectangle(bg, null, new Rect(at.X - 6, at.Y - 3, text.Width + 12, text.Height + 6), 3, 3);
        text.SetForegroundBrush(Brushes.White);   // labels sit on a dark chip regardless of theme
        dc.DrawText(text, at);
    }

    // Font from the inherited TextElement.FontFamily (the view sets Font.Mono), not a hard-coded face.
    private FormattedText Text(string s, double size) => new(
        s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(System.Windows.Documents.TextElement.GetFontFamily(this),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    // ---- accessibility -----------------------------------------------------------------------------

    // A bare FrameworkElement has no automation peer, so screen readers (and UI tests) can't see it at all.
    protected override AutomationPeer OnCreateAutomationPeer() => new HeatmapViewAutomationPeer(this);

    private sealed class HeatmapViewAutomationPeer(HeatmapView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string GetClassNameCore() => nameof(HeatmapView);
        protected override bool IsKeyboardFocusableCore() => true;

        // Item status carries what a sighted user reads off the overlay: zoom and the hovered value.
        protected override string GetItemStatusCore()
        {
            var v = (HeatmapView)Owner;
            if (v.Image is null) return v.EmptyText;
            string zoom = string.Format(CultureInfo.InvariantCulture, "zoom {0:0.#}x", v.Zoom);
            return v.Readout.Length > 0 ? $"{zoom}; {v.Readout}" : zoom;
        }
    }

    // ---- interaction -------------------------------------------------------------------------------

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Image is null) return;
        ZoomStep(e.GetPosition(this), e.Delta > 0);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (Image is null) return;
        if (e.ClickCount == 2) { ResetView(); e.Handled = true; return; }
        _dragFrom = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        if (_dragFrom is { } from && IsMouseCaptured)
        {
            _viewport.PanBy(new Vec2(p.X - from.X, p.Y - from.Y));
            _dragFrom = p;
            OnViewChanged();
        }
        UpdateHover(p);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _dragFrom = null;
        ReleaseMouseCapture();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        SetValue(ReadoutPropertyKey, string.Empty);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Image is null) return;
        var centre = new Vec2(ActualWidth / 2, ActualHeight / 2);
        switch (e.Key)
        {
            case Key.OemPlus or Key.Add: _viewport.ZoomAt(centre, WheelStep); break;
            case Key.OemMinus or Key.Subtract: _viewport.ZoomAt(centre, 1 / WheelStep); break;
            case Key.Left: _viewport.PanBy(new Vec2(KeyPanPx, 0)); break;
            case Key.Right: _viewport.PanBy(new Vec2(-KeyPanPx, 0)); break;
            case Key.Up: _viewport.PanBy(new Vec2(0, KeyPanPx)); break;
            case Key.Down: _viewport.PanBy(new Vec2(0, -KeyPanPx)); break;
            case Key.D0 or Key.NumPad0 or Key.Home: _viewport.Reset(); break;
            default: return;
        }
        e.Handled = true;
        OnViewChanged();
    }

    /// <summary>Back to fit-to-view.</summary>
    public void ResetView()
    {
        _viewport.Reset();
        OnViewChanged();
    }

    private void UpdateHover(Point p)
    {
        var img = Image;
        _hover = img is null ? null : _viewport.PixelAt(new Vec2(p.X, p.Y));
        RefreshReadout();
        Cursor = _viewport.Zoom > HeatmapViewport.MinZoom ? (IsMouseCaptured ? Cursors.SizeAll : Cursors.Hand) : null;
        InvalidateVisual();
    }

    private void RefreshReadout()
    {
        var img = Image;
        string readout = string.Empty;
        if (_hover is { } h && img is not null)
        {
            var mm = HeatmapViewport.ImageToMm(new Vec2(h.X + 0.5, h.Y + 0.5), OriginMm, StepMm);
            readout = string.Format(CultureInfo.InvariantCulture, "x {0:F1} mm, y {1:F1} mm · {2}", mm.X, mm.Y, NumberFormat.Significant(img[h.X, h.Y]))
                .Replace("-0.0 mm", "0.0 mm");   // a tiny negative coordinate shouldn't read as "-0.0"
            if (!string.IsNullOrWhiteSpace(ValueUnit)) readout += " " + ValueUnit;
        }
        SetValue(ReadoutPropertyKey, readout);
    }
}
