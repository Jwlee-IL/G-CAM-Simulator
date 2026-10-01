using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using Gcam.Studio.Core.Plotting;

namespace Gcam.Studio.Controls;

/// <summary>First-party trace surface. Data preparation is cached; navigation draws exact column extrema.</summary>
public sealed class PlotView : FrameworkElement
{
    private const double ZoomStep = 1.25; // A wheel / key step changes the X span by 20 percent.
    private const double KeyPanFraction = 0.1; // Arrow keys pan by one tenth of the visible X span.
    private const double LabelGap = 4; // Separate text from axes and neighbouring labels in DIPs.
    private const double OuterPadding = 4; // Keep text clear of the clipped control edge.
    private const double MinorTickLength = 4; // Distinguish unlabelled log ticks from major grid lines.
    private const double TraceWidth = 1.4; // Keep the trace legible against the one-DIP grid.
    private const double AreaOpacity = 0.22; // Preserve grid visibility under the area fill.
    private const double FocusWidth = 2; // Make keyboard focus visible on both themes.
    private const double FocusInset = 1; // Keep the focus stroke inside the clipped bounds.
    private readonly PlotViewport _viewport = new();
    private readonly List<(PlotSeries Series, MinMaxPyramid Pyramid)> _prepared = [];
    private Point? _drag;
    private double _xMin, _xMax = 1, _yMin, _yMax = 1;
    private Rect _plotRect;
    private (PlotSeries Series, int Bin)? _hover;
    private double? _hoverX;
    private bool _pendingViewRange;
    private Rect PlotRect => _plotRect;

    private IReadOnlyList<PlotTick> YTicks() => LogY
        ? NiceTicks.Logarithmic(_viewport.YMin, _viewport.YMax)
        : NiceTicks.Linear(_viewport.YMin, _viewport.YMax);

    private Rect MeasurePlotRect()
    {
        double yWidth = 0, yHeight = 0, xHeight = 0, xHalfWidth = 0;
        foreach (var tick in YTicks())
        {
            var text = Text(tick.Label);
            yWidth = Math.Max(yWidth, text.Width);
            yHeight = Math.Max(yHeight, text.Height);
        }
        foreach (var tick in NiceTicks.Linear(_viewport.XMin, _viewport.XMax))
        {
            var text = Text(tick.Label);
            xHeight = Math.Max(xHeight, text.Height);
            xHalfWidth = Math.Max(xHalfWidth, text.Width / 2);
        }
        double left = OuterPadding + Math.Max(yWidth + LabelGap, xHalfWidth);
        double titleRow = string.IsNullOrEmpty(YLabel) ? 0 : Text(YLabel).Height + LabelGap;
        double top = OuterPadding + titleRow + yHeight / 2;
        double right = OuterPadding + xHalfWidth;
        double bottom = OuterPadding + xHeight + LabelGap + Text(XLabel).Height + LabelGap;
        return new(left, top, Math.Max(1, ActualWidth - left - right), Math.Max(1, ActualHeight - top - bottom));
    }

    static PlotView()
    {
        FocusableProperty.OverrideMetadata(typeof(PlotView), new FrameworkPropertyMetadata(true));
        FocusVisualStyleProperty.OverrideMetadata(typeof(PlotView), new FrameworkPropertyMetadata(null));
        ClipToBoundsProperty.OverrideMetadata(typeof(PlotView), new FrameworkPropertyMetadata(true));
    }

    private static DependencyProperty Register<T>(string name, T value, PropertyChangedCallback? changed = null) =>
        DependencyProperty.Register(name, typeof(T), typeof(PlotView),
            new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender, changed));

    public static readonly DependencyProperty SeriesProperty = Register<IReadOnlyList<PlotSeries>?>(nameof(Series), null, (d, _) => ((PlotView)d).Prepare());
    public IReadOnlyList<PlotSeries>? Series { get => (IReadOnlyList<PlotSeries>?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public static readonly DependencyProperty BandsProperty = Register<IReadOnlyList<PlotBand>?>(nameof(Bands), null);
    public IReadOnlyList<PlotBand>? Bands { get => (IReadOnlyList<PlotBand>?)GetValue(BandsProperty); set => SetValue(BandsProperty, value); }
    public static readonly DependencyProperty MarkersProperty = Register<IReadOnlyList<PlotMarker>?>(nameof(Markers), null);
    public IReadOnlyList<PlotMarker>? Markers { get => (IReadOnlyList<PlotMarker>?)GetValue(MarkersProperty); set => SetValue(MarkersProperty, value); }
    public static readonly DependencyProperty LogYProperty = Register(nameof(LogY), false, (d, _) => ((PlotView)d).Configure(false));
    public bool LogY { get => (bool)GetValue(LogYProperty); set => SetValue(LogYProperty, value); }
    public static readonly DependencyProperty XLabelProperty = Register(nameof(XLabel), "");
    public string XLabel { get => (string)GetValue(XLabelProperty); set => SetValue(XLabelProperty, value); }
    public static readonly DependencyProperty YLabelProperty = Register(nameof(YLabel), "");
    public string YLabel { get => (string)GetValue(YLabelProperty); set => SetValue(YLabelProperty, value); }
    public static readonly DependencyProperty ViewRangeProperty = Register<PlotViewRange?>(nameof(ViewRange), null,
        (d, _) => ((PlotView)d).ApplyViewRange());
    public PlotViewRange? ViewRange { get => (PlotViewRange?)GetValue(ViewRangeProperty); set => SetValue(ViewRangeProperty, value); }
    public static readonly DependencyProperty XUnitProperty = Register(nameof(XUnit), "");
    public string XUnit { get => (string)GetValue(XUnitProperty); set => SetValue(XUnitProperty, value); }
    public static readonly DependencyProperty YUnitProperty = Register(nameof(YUnit), "");
    public string YUnit { get => (string)GetValue(YUnitProperty); set => SetValue(YUnitProperty, value); }
    public static readonly DependencyProperty XFormatProperty = Register<string?>(nameof(XFormat), null);
    public string? XFormat { get => (string?)GetValue(XFormatProperty); set => SetValue(XFormatProperty, value); }
    public static readonly DependencyProperty YFormatProperty = Register(nameof(YFormat), "N0");
    public string YFormat { get => (string)GetValue(YFormatProperty); set => SetValue(YFormatProperty, value); }
    public static readonly DependencyProperty EmptyTextProperty = Register(nameof(EmptyText), "No data");
    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    public static readonly DependencyProperty ForegroundProperty = Register<Brush?>(nameof(Foreground), null);
    public Brush? Foreground { get => (Brush?)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public static readonly DependencyProperty GridBrushProperty = Register<Brush?>(nameof(GridBrush), null);
    public Brush? GridBrush { get => (Brush?)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public static readonly DependencyProperty BandBrushProperty = Register<Brush?>(nameof(BandBrush), null);
    public Brush? BandBrush { get => (Brush?)GetValue(BandBrushProperty); set => SetValue(BandBrushProperty, value); }
    public static readonly DependencyProperty BandEdgeBrushProperty = Register<Brush?>(nameof(BandEdgeBrush), null);
    public Brush? BandEdgeBrush { get => (Brush?)GetValue(BandEdgeBrushProperty); set => SetValue(BandEdgeBrushProperty, value); }
    public static readonly DependencyProperty FocusBrushProperty = Register<Brush?>(nameof(FocusBrush), null);
    public Brush? FocusBrush { get => (Brush?)GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }
    public static readonly DependencyProperty Series1BrushProperty = Register<Brush?>(nameof(Series1Brush), null);
    public Brush? Series1Brush { get => (Brush?)GetValue(Series1BrushProperty); set => SetValue(Series1BrushProperty, value); }
    public static readonly DependencyProperty Series2BrushProperty = Register<Brush?>(nameof(Series2Brush), null);
    public Brush? Series2Brush { get => (Brush?)GetValue(Series2BrushProperty); set => SetValue(Series2BrushProperty, value); }
    public static readonly DependencyProperty Series3BrushProperty = Register<Brush?>(nameof(Series3Brush), null);
    public Brush? Series3Brush { get => (Brush?)GetValue(Series3BrushProperty); set => SetValue(Series3BrushProperty, value); }

    private static readonly DependencyPropertyKey ReadoutPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Readout), typeof(string), typeof(PlotView), new FrameworkPropertyMetadata(""));
    public static readonly DependencyProperty ReadoutProperty = ReadoutPropertyKey.DependencyProperty;
    /// <summary>Line pointer coordinates or histogram bin counts / bounds for a host TextBlock.</summary>
    public string Readout => (string)GetValue(ReadoutProperty);
    public double Zoom => _viewport.Zoom;
    public PlotViewRange CurrentViewRange => new(_viewport.XMin, _viewport.XMax);
    public double CurrentYMax => _viewport.YMax;

    /// <summary>CPU redraw duration, including ticks, query, geometry and drawing commands; excludes composition.</summary>
    public double LastRedrawMilliseconds { get; private set; }
    public long RenderCount { get; private set; }
    public event EventHandler? Redrawn;

    private void Prepare()
    {
        _prepared.Clear();
        _xMin = double.PositiveInfinity; _xMax = double.NegativeInfinity;
        _yMin = double.PositiveInfinity; _yMax = double.NegativeInfinity;
        foreach (var series in Series ?? [])
        {
            series.Validate();
            if (series.Y.Length == 0) continue;
            var pyramid = new MinMaxPyramid(series.Y);
            var range = pyramid.Range(0, series.Y.Length);
            _prepared.Add((series, pyramid));
            _xMin = Math.Min(_xMin, series.Kind == PlotKind.Histogram ? series.EdgeAt(0) : series.XAt(0));
            _xMax = Math.Max(_xMax, series.Kind == PlotKind.Histogram ? series.EdgeAt(series.Y.Length) : series.XAt(series.Y.Length - 1));
            _yMin = Math.Min(_yMin, series.Kind != PlotKind.Line ? Math.Min(0, range.Min) : range.Min);
            _yMax = Math.Max(_yMax, range.Max);
        }
        Configure(true);
        if (_pendingViewRange && _prepared.Count > 0) ApplyViewRange();
        UpdateHover(_hoverX);
    }

    private void Configure(bool dataUpdate)
    {
        if (_prepared.Count == 0)
        {
            _viewport.Configure(0, 1, 0, 1, LogY);
            _viewport.Reset();
            _hover = null;
            _hoverX = null;
        }
        else
        {
            double oldMin = _viewport.XMin, oldMax = _viewport.XMax, oldTop = _viewport.YMax;
            bool sameRange = _xMin == _fullXMin && _xMax == _fullXMax;
            _viewport.Configure(_xMin, _xMax, _yMin, _yMax, LogY);
            AutoScale(dataUpdate && sameRange && oldMin == _viewport.XMin && oldMax == _viewport.XMax ? oldTop : null);
        }
        _fullXMin = _xMin; _fullXMax = _xMax;
        InvalidateVisual();
    }

    private double _fullXMin = double.NaN, _fullXMax = double.NaN;
    private void AutoScale(double? previousTop = null)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        foreach (var (series, pyramid) in _prepared)
        {
            var range = PlotGeometry.VisibleRange(series, pyramid, _viewport.XMin, _viewport.XMax);
            if (range.IsEmpty) continue;
            min = Math.Min(min, range.Min); max = Math.Max(max, range.Max);
        }
        if (!double.IsFinite(min)) { min = 0; max = 0; }
        var bounds = PlotAutoScale.Calculate(min, max, _prepared.Any(p => p.Series.Kind != PlotKind.Line), LogY, previousTop);
        _viewport.SetY(bounds.Min, bounds.Max);
    }

    private void ApplyViewRange()
    {
        _pendingViewRange = ViewRange is not null && _prepared.Count == 0;
        if (ViewRange is { } range && _prepared.Count > 0) _viewport.SetRange(range);
        NavigationChanged();
    }

    private void NavigationChanged()
    {
        AutoScale();
        UpdateHover(null);
        InvalidateVisual();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateVisual(); // resizing preserves the X viewport and the prepared dataset
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateVisual();
    }

    protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusedChanged(e);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        long start = Stopwatch.GetTimestamp();
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var r = _plotRect = MeasurePlotRect();
        if (_prepared.Count == 0) DrawText(dc, EmptyText, new Point(r.Left, r.Top));
        else
        {
            DrawAxes(dc, r);
            dc.PushClip(new RectangleGeometry(r));
            var visibleBands = (Bands ?? []).Where(b => b.Hi >= _viewport.XMin && b.Lo <= _viewport.XMax).ToArray();
            foreach (var band in visibleBands)
            {
                double a = ScreenX(band.Lo), b = ScreenX(band.Hi);
                dc.DrawRectangle(BandBrush, null, new Rect(Math.Min(a, b), r.Top, Math.Abs(b - a), r.Height));
            }
            foreach (var (series, pyramid) in _prepared) DrawSeries(dc, r, series, pyramid);
            // Edges stay visible even where a filled series covers the window tint.
            foreach (var band in visibleBands)
            {
                foreach (double x in new[] { ScreenX(band.Lo), ScreenX(band.Hi) })
                    dc.DrawLine(Pen(BandEdgeBrush), new Point(x, r.Top), new Point(x, r.Bottom));
            }
            if (_hover is { } hover)
            {
                double a = ScreenX(hover.Series.EdgeAt(hover.Bin)), b = ScreenX(hover.Series.EdgeAt(hover.Bin + 1));
                dc.DrawRectangle(BandBrush, Pen(Foreground), new Rect(a, r.Top, b - a, r.Height));
                double centre = (a + b) / 2;
                dc.DrawLine(Pen(Foreground), new Point(centre, r.Top), new Point(centre, r.Bottom));
            }
            var labels = visibleBands.Select(b => Text(b.Label)).ToArray();
            var layout = PlotBandLayout.Arrange(visibleBands.Select((b, i) =>
                (ScreenX((b.Lo + b.Hi) / 2) - r.Left, labels[i].Width)).ToArray(), r.Width, LabelGap);
            double rowHeight = labels.Length == 0 ? 0 : labels.Max(t => t.Height) + LabelGap;
            foreach (var label in layout)
            {
                var text = labels[label.Index];
                text.MaxTextWidth = Math.Max(1, label.Width);
                text.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(text, new Point(r.Left + label.Left, r.Top + LabelGap + label.Row * rowHeight));
            }
            foreach (var marker in Markers ?? [])
            {
                double x = ScreenX(marker.X);
                dc.DrawLine(Pen(Foreground), new Point(x, r.Top), new Point(x, r.Bottom));
                DrawText(dc, marker.Label, new Point(x + LabelGap, r.Top + Text(marker.Label).Height + LabelGap));
            }
            dc.Pop();
        }
        if (IsKeyboardFocused) dc.DrawRectangle(null, Pen(FocusBrush, FocusWidth), new Rect(FocusInset, FocusInset,
            Math.Max(0, ActualWidth - FocusInset * 2), Math.Max(0, ActualHeight - FocusInset * 2)));
        LastRedrawMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        RenderCount++;
        Redrawn?.Invoke(this, EventArgs.Empty);
    }

    private double ScreenX(double x) => PlotRect.Left + _viewport.XToPixel(x, PlotRect.Width);
    private double ScreenY(double y) => PlotRect.Top + _viewport.YToPixel(y, PlotRect.Height);

    private void DrawSeries(DrawingContext dc, Rect r, PlotSeries series, MinMaxPyramid pyramid)
    {
        int columns = Math.Max(1, (int)Math.Ceiling(r.Width * VisualTreeHelper.GetDpi(this).PixelsPerDip));
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            var points = PlotGeometry.Build(series, pyramid, _viewport.XMin, _viewport.XMax, columns);
            if (points.Count > 0)
            {
                bool filled = series.Kind != PlotKind.Line;
                double floor = ScreenY(LogY ? PlotViewport.LogFloor : 0);
                var first = points[0];
                g.BeginFigure(new Point(ScreenX(first.X), filled ? floor : ScreenY(first.Y)), filled, filled);
                foreach (var point in points) g.LineTo(new Point(ScreenX(point.X), ScreenY(point.Y)), true, false);
                if (filled) g.LineTo(new Point(ScreenX(points[^1].X), floor), true, false);
            }
        }
        geometry.Freeze();
        Brush? brush = series.ColourRole switch { PlotColourRole.Series2 => Series2Brush, PlotColourRole.Series3 => Series3Brush, _ => Series1Brush };
        if (series.Kind != PlotKind.Line)
        {
            dc.PushOpacity(AreaOpacity);
            dc.DrawGeometry(brush, null, geometry);
            dc.Pop();
        }
        dc.DrawGeometry(null, Pen(brush, TraceWidth), geometry);
    }

    private void DrawAxes(DrawingContext dc, Rect r)
    {
        double xTickHeight = 0;
        foreach (var tick in NiceTicks.Linear(_viewport.XMin, _viewport.XMax))
        {
            double x = ScreenX(tick.Value);
            dc.DrawLine(Pen(GridBrush), new Point(x, r.Top), new Point(x, r.Bottom));
            var text = Text(tick.Label);
            xTickHeight = Math.Max(xTickHeight, text.Height);
            dc.DrawText(text, new Point(x - text.Width / 2, r.Bottom + LabelGap));
        }
        foreach (var tick in YTicks())
        {
            double y = ScreenY(tick.Value);
            if (tick.IsMajor) dc.DrawLine(Pen(GridBrush), new Point(r.Left, y), new Point(r.Right, y));
            else dc.DrawLine(Pen(GridBrush), new Point(r.Left - MinorTickLength, y), new Point(r.Left, y));
            var text = Text(tick.Label);
            dc.DrawText(text, new Point(r.Left - LabelGap - text.Width, y - text.Height / 2));
        }
        dc.DrawRectangle(null, Pen(Foreground), r);
        var xLabel = Text(XLabel);
        dc.DrawText(xLabel, new Point(r.Left + (r.Width - xLabel.Width) / 2, r.Bottom + xTickHeight + LabelGap * 2));
        DrawText(dc, YLabel, new Point(OuterPadding, OuterPadding));
    }

    private static Pen Pen(Brush? brush, double width = 1)
    {
        var pen = new Pen(brush, width);
        if (pen.CanFreeze) pen.Freeze();
        return pen;
    }
    private void DrawText(DrawingContext dc, string text, Point at)
    {
        if (string.IsNullOrEmpty(text) || Foreground is null) return;
        dc.DrawText(Text(text), at);
    }

    private FormattedText Text(string text)
    {
        var family = System.Windows.Documents.TextElement.GetFontFamily(this);
        double size = System.Windows.Documents.TextElement.GetFontSize(this);
        return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, Foreground ?? Brushes.Transparent,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    public void ZoomAt(double fraction, bool zoomIn)
    {
        _viewport.ZoomAt(_viewport.PixelToX(Math.Clamp(fraction, 0, 1), 1), zoomIn ? ZoomStep : 1 / ZoomStep);
        NavigationChanged();
    }
    public void ResetView() { _viewport.Reset(); NavigationChanged(); }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ZoomAt((e.GetPosition(this).X - PlotRect.Left) / PlotRect.Width, e.Delta > 0);
        e.Handled = true;
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (e.ClickCount == 2) ResetView();
        else { _drag = e.GetPosition(this); CaptureMouse(); }
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        if (_drag is { } from && IsMouseCaptured && point.X != from.X)
        {
            double previousMin = _viewport.XMin, previousMax = _viewport.XMax;
            _viewport.Pan((from.X - point.X) / PlotRect.Width * (_viewport.XMax - _viewport.XMin));
            _drag = point;
            if (_viewport.XMin != previousMin || _viewport.XMax != previousMax) NavigationChanged();
        }
        if (PlotRect.Contains(point) && _prepared.Any(p => p.Series.Kind == PlotKind.Histogram))
            UpdateHover(_viewport.PixelToX(point.X - PlotRect.Left, PlotRect.Width));
        else
        {
            UpdateHover(null);
            SetValue(ReadoutPropertyKey, PlotRect.Contains(point) && _prepared.Count > 0
                ? string.Format(CultureInfo.InvariantCulture, "x {0:G5}, y {1:G5}", _viewport.PixelToX(point.X - PlotRect.Left, PlotRect.Width),
                    _viewport.PixelToY(point.Y - PlotRect.Top, PlotRect.Height)) : "");
        }
    }

    private void UpdateHover(double? x)
    {
        var previous = _hover;
        _hoverX = x;
        _hover = null;
        if (x is { } value)
            foreach (var (series, _) in _prepared)
                if (series.Kind == PlotKind.Histogram && series.BinAt(value) is var bin && bin >= 0)
                { _hover = (series, bin); break; }
        SetValue(ReadoutPropertyKey, _hover is { } h ? PlotBinReadout.Format(h.Series, h.Bin, XUnit, YUnit, XFormat, YFormat) : "");
        if (previous != _hover) InvalidateVisual();
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e); _drag = null; ReleaseMouseCapture();
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); _drag = null; }
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e); UpdateHover(null);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Add or Key.OemPlus: ZoomAt(0.5, true); break;
            case Key.Subtract or Key.OemMinus: ZoomAt(0.5, false); break;
            case Key.Left or Key.Up: _viewport.Pan(-(_viewport.XMax - _viewport.XMin) * KeyPanFraction); break;
            case Key.Right or Key.Down: _viewport.Pan((_viewport.XMax - _viewport.XMin) * KeyPanFraction); break;
            case Key.D0 or Key.NumPad0 or Key.Home: ResetView(); break;
            default: return;
        }
        e.Handled = true; NavigationChanged();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PlotViewAutomationPeer(this);
    private sealed class PlotViewAutomationPeer(PlotView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string GetClassNameCore() => nameof(PlotView);
        protected override bool IsKeyboardFocusableCore() => true;
        protected override string GetItemStatusCore() => owner._prepared.Count == 0 ? owner.EmptyText
            : $"zoom {owner.Zoom.ToString("0.#", CultureInfo.InvariantCulture)}x; {owner.Readout}";
    }
}
