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
        ClipToBoundsProperty.OverrideMetadata(typeof(HeatmapView), new FrameworkPropertyMetadata(true));
    }

    public HeatmapView()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    // ---- dependency properties ---------------------------------------------------------------------

    public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(
        nameof(Image), typeof(DetectorImage), typeof(HeatmapView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((HeatmapView)d).OnImageChanged()));

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

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(HeatmapView), new FrameworkPropertyMetadata("No data", FrameworkPropertyMetadataOptions.AffectsRender));

    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(HeatmapView),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Colour of the overlay text (readout, zoom, empty state).</summary>
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private static readonly DependencyPropertyKey ReadoutPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Readout), typeof(string), typeof(HeatmapView), new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty ReadoutProperty = ReadoutPropertyKey.DependencyProperty;

    /// <summary>"x, y mm · value" for the hovered pixel — bindable, e.g. to a status line.</summary>
    public string Readout => (string)GetValue(ReadoutProperty);

    /// <summary>Current zoom relative to fit (1 = whole image).</summary>
    public double Zoom => _viewport.Zoom;

    // ---- data --------------------------------------------------------------------------------------

    private void OnImageChanged()
    {
        _hover = null;
        SetValue(ReadoutPropertyKey, string.Empty);
        var img = Image;
        if (img is null) { _bitmap = null; return; }

        _min = double.MaxValue; _max = double.MinValue;
        foreach (var v in img.Raw) { if (v < _min) _min = v; if (v > _max) _max = v; }
        double span = _max > _min ? _max - _min : 1;

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
        _viewport.Configure(img.Width, img.Height, ActualWidth, ActualHeight);
    }

    // ---- rendering ---------------------------------------------------------------------------------

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (Image is { } img) _viewport.Configure(img.Width, img.Height, ActualWidth, ActualHeight);
    }

    protected override void OnRender(DrawingContext dc)
    {
        // Transparent fill so the whole area is hit-testable (wheel/drag work on the letterbox too).
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

        if (_bitmap is null || Image is null)
        {
            var empty = Text(EmptyText, 13);
            dc.DrawText(empty, new Point((ActualWidth - empty.Width) / 2, (ActualHeight - empty.Height) / 2));
            return;
        }

        double s = _viewport.Scale;
        var origin = new Point(_viewport.Offset.X, _viewport.Offset.Y);
        dc.DrawImage(_bitmap, new Rect(origin, new Size(_bitmap.PixelWidth * s, _bitmap.PixelHeight * s)));

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
        if (Readout.Length > 0)
        {
            var r = Text(Readout, 11);
            DrawLabel(dc, r, new Point(8, ActualHeight - r.Height - 10));
        }
    }

    private void DrawLabel(DrawingContext dc, FormattedText text, Point at)
    {
        var bg = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
        bg.Freeze();
        dc.DrawRoundedRectangle(bg, null, new Rect(at.X - 6, at.Y - 3, text.Width + 12, text.Height + 6), 3, 3);
        text.SetForegroundBrush(Brushes.White);   // labels sit on a dark chip regardless of theme
        dc.DrawText(text, at);
    }

    private FormattedText Text(string s, double size) => new(
        s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, Foreground,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

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
        var p = e.GetPosition(this);
        _viewport.ZoomAt(new Vec2(p.X, p.Y), e.Delta > 0 ? WheelStep : 1 / WheelStep);
        UpdateHover(p);
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
        InvalidateVisual();
    }

    /// <summary>Back to fit-to-view.</summary>
    public void ResetView()
    {
        _viewport.Reset();
        InvalidateVisual();
    }

    private void UpdateHover(Point p)
    {
        var img = Image;
        _hover = img is null ? null : _viewport.PixelAt(new Vec2(p.X, p.Y));
        string readout = string.Empty;
        if (_hover is { } h && img is not null)
        {
            var mm = HeatmapViewport.ImageToMm(new Vec2(h.X + 0.5, h.Y + 0.5), OriginMm, StepMm);
            readout = string.Format(CultureInfo.InvariantCulture, "x {0:F1} mm, y {1:F1} mm · {2:G4}", mm.X, mm.Y, img[h.X, h.Y]);
        }
        SetValue(ReadoutPropertyKey, readout);
        Cursor = _viewport.Zoom > HeatmapViewport.MinZoom ? (IsMouseCaptured ? Cursors.SizeAll : Cursors.Hand) : null;
        InvalidateVisual();
    }
}
