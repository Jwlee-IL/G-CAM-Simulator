using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Controls;

/// <summary>
/// Draws measurements and draggable markers over a <see cref="HeatmapView"/> and turns pointer gestures into new
/// measurements. Attached from XAML through <see cref="MeasurementOverlay"/>.
/// </summary>
/// <remarks>
/// <para>Everything is stored in mm (the ViewModels) and mapped to the screen on every render through the heatmap,
/// so overlays follow zoom and pan without keeping any screen state of their own.</para>
/// <para>Input ownership: with the Pan tool the adorner is hit-test transparent except on a marker, so drags and
/// the wheel reach the heatmap underneath. With a measuring tool it takes the pointer and forwards the wheel.</para>
/// </remarks>
public sealed class MeasurementAdorner : Adorner
{
    private const double MarkerRadius = 7;
    private const double MarkerHitRadius = 11;   // a little larger than the ring, so it is easy to grab
    private const double MinDragPx = 4;          // shorter drags are clicks, not measurements
    private const int MarkerSnapDigits = 1;      // dragged markers land on 0.1 mm

    private static readonly Pen HaloPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), 4));
    private static readonly Pen LinePen = Frozen(new Pen(Brushes.White, 1.5));
    private static readonly Pen SelectedPen = Frozen(new Pen(Brushes.White, 2.5));
    private static readonly Pen DraftPen = Frozen(new Pen(Brushes.White, 1.5) { DashStyle = new DashStyle([4, 3], 0) });
    private static readonly Brush ChipBrush = Frozen(new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)));
    private static readonly Brush SelectedChipBrush = Brushes.White;

    private readonly HeatmapView _view;
    private readonly List<INotifyPropertyChanged> _watched = [];
    private MeasurementsViewModel? _session;
    private INotifyCollectionChanged? _markerCollection;

    private List<Vec2>? _draft;     // points placed so far, in mm
    private Vec2? _cursorMm;
    private IPlaneMarker? _dragMarker;

    public MeasurementAdorner(HeatmapView view) : base(view)
    {
        _view = view;
        IsClipEnabled = true;
        _view.ViewChanged += OnViewChanged;
        _view.PreviewKeyDown += OnViewKeyDown;
    }

    /// <summary>The layer this adorner was added to (removal must not depend on the element still being in the tree).</summary>
    internal AdornerLayer? Layer { get; init; }

    private ImagePane Pane => MeasurementOverlay.GetPane(_view);
    private MeasureTool Tool => _session?.ActiveTool ?? MeasureTool.Pan;

    // ---- binding to the ViewModels ----------------------------------------------------------------

    /// <summary>Re-reads the attached properties and re-subscribes; called whenever one of them changes.</summary>
    internal void Rebind()
    {
        var session = MeasurementOverlay.GetSession(_view);
        if (!ReferenceEquals(session, _session))
        {
            if (_session is not null)
            {
                _session.PropertyChanged -= OnSessionChanged;
                _session.Items.CollectionChanged -= OnCollectionChanged;
            }
            _session = session;
            if (_session is not null)
            {
                _session.PropertyChanged += OnSessionChanged;
                _session.Items.CollectionChanged += OnCollectionChanged;
            }
            CancelDraft();
        }

        var markers = MeasurementOverlay.GetMarkers(_view) as INotifyCollectionChanged;
        if (!ReferenceEquals(markers, _markerCollection))
        {
            if (_markerCollection is not null) _markerCollection.CollectionChanged -= OnCollectionChanged;
            _markerCollection = markers;
            if (_markerCollection is not null) _markerCollection.CollectionChanged += OnCollectionChanged;
        }
        WatchItems();
        InvalidateVisual();
    }

    internal void Unbind()
    {
        if (_session is not null)
        {
            _session.PropertyChanged -= OnSessionChanged;
            _session.Items.CollectionChanged -= OnCollectionChanged;
            _session = null;
        }
        if (_markerCollection is not null) _markerCollection.CollectionChanged -= OnCollectionChanged;
        _markerCollection = null;
        foreach (var w in _watched) w.PropertyChanged -= OnItemChanged;
        _watched.Clear();
        _view.ViewChanged -= OnViewChanged;
        _view.PreviewKeyDown -= OnViewKeyDown;
    }

    // Measurement values (ROI sums) and marker positions change in place, so watch every item too.
    private void WatchItems()
    {
        foreach (var w in _watched) w.PropertyChanged -= OnItemChanged;
        _watched.Clear();
        IEnumerable<object> items = (_session?.Items ?? Enumerable.Empty<object>()).Cast<object>()
            .Concat(Markers().Cast<object>());
        foreach (var item in items.OfType<INotifyPropertyChanged>())
        {
            item.PropertyChanged += OnItemChanged;
            _watched.Add(item);
        }
    }

    private IEnumerable<IPlaneMarker> Markers() =>
        (MeasurementOverlay.GetMarkers(_view) ?? Array.Empty<object>()).OfType<IPlaneMarker>();

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MeasurementsViewModel.ActiveTool) ||
            e.PropertyName == nameof(MeasurementsViewModel.ReconstructionRevision) && Pane == ImagePane.Reconstruction) CancelDraft();
        InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        WatchItems();
        InvalidateVisual();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();
    private void OnViewChanged(object? sender, EventArgs e) => InvalidateVisual();

    // ---- rendering --------------------------------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        if (!_view.HasImage) return;

        if (_session is { } s)
            foreach (var m in s.Items.Where(m => m.Pane == Pane))
                DrawMeasurement(dc, m, ReferenceEquals(m, s.Selected));
        DrawDraft(dc);

        // With one source "selected" carries no information and would compete with the selected measurement, so a
        // marker is highlighted only while dragged, or when there are several to tell apart.
        var markers = Markers().ToArray();
        var selectedMarker = MeasurementOverlay.GetSelectedMarker(_view);
        foreach (var marker in markers)
            DrawMarker(dc, marker, ReferenceEquals(marker, _dragMarker) || (markers.Length > 1 && ReferenceEquals(marker, selectedMarker)));
        foreach (var peak in (MeasurementOverlay.GetFoundPeaks(_view) ?? Array.Empty<object>()).OfType<ImagingPeak>())
            DrawFoundPeak(dc, peak);
    }

    private void DrawFoundPeak(DrawingContext dc, ImagingPeak peak)
    {
        double radius = (double)_view.FindResource("Size.Imaging.FoundMarker");
        double gap = (double)_view.FindResource("Space.Imaging.FoundLabel");
        var p = _view.MmToScreen(new Vec2(peak.Xmm, peak.Ymm));
        // A diamond, unlike the truth marker's ring/cross; shared neutral overlay pens and chip.
        Point[] points = [p + new Vector(0, -radius), p + new Vector(radius, 0),
            p + new Vector(0, radius), p + new Vector(-radius, 0)];
        for (int i = 0; i < points.Length; i++) Segment(dc, points[i], points[(i + 1) % points.Length], LinePen);
        Chip(dc, $"Found {peak.Isotope}", p + new Vector(radius + gap, radius + gap), false);
    }

    private void DrawMeasurement(DrawingContext dc, MeasurementViewModel m, bool selected)
    {
        var p = m.PointsMm.Select(_view.MmToScreen).ToArray();
        var pen = selected ? SelectedPen : LinePen;
        string text = $"{m.Name}  {m.Value}";
        switch (m.Kind)
        {
            case MeasurementKind.Distance:
                Segment(dc, p[0], p[1], pen);
                Dot(dc, p[0], pen.Brush);
                Dot(dc, p[1], pen.Brush);
                Chip(dc, text, Mid(p[0], p[1]) + new Vector(8, 4), selected);
                break;
            case MeasurementKind.Angle:
                Segment(dc, p[1], p[0], pen);
                Segment(dc, p[1], p[2], pen);
                Arc(dc, p[1], p[0], p[2], pen);
                Chip(dc, text, p[1] + new Vector(10, 6), selected);
                break;
            default:
                var rect = new Rect(p[0], p[1]);
                dc.DrawRectangle(null, HaloPen, rect);
                dc.DrawRectangle(null, pen, rect);
                Chip(dc, text, new Point(rect.Left, Math.Max(2, rect.Top - 22)), selected);
                break;
        }
    }

    private void DrawDraft(DrawingContext dc)
    {
        if (_draft is not { Count: > 0 } d || _cursorMm is not { } cursor) return;
        var a = _view.MmToScreen(d[0]);
        var c = _view.MmToScreen(cursor);
        switch (Tool)
        {
            case MeasureTool.Distance:
                Segment(dc, a, c, DraftPen);
                Chip(dc, $"{MeasurementMath.Distance(d[0], cursor):F1} mm", c + new Vector(12, 8), false);
                break;
            case MeasureTool.Roi:
                var rect = new Rect(a, c);
                dc.DrawRectangle(null, HaloPen, rect);
                dc.DrawRectangle(null, DraftPen, rect);
                Chip(dc, $"{Math.Abs(cursor.X - d[0].X):F1} × {Math.Abs(cursor.Y - d[0].Y):F1} mm", c + new Vector(12, 8), false);
                break;
            case MeasureTool.Angle when d.Count == 1:
                Segment(dc, a, c, DraftPen);
                break;
            case MeasureTool.Angle:
                var v = _view.MmToScreen(d[1]);
                Segment(dc, v, a, LinePen);
                Segment(dc, v, c, DraftPen);
                Arc(dc, v, a, c, DraftPen);
                Chip(dc, $"{MeasurementMath.AngleDeg(d[0], d[1], cursor):F1}°", c + new Vector(12, 8), false);
                break;
        }
    }

    private void DrawMarker(DrawingContext dc, IPlaneMarker marker, bool selected)
    {
        var p = _view.MmToScreen(new Vec2(marker.X, marker.Y));
        var pen = selected ? SelectedPen : LinePen;
        dc.DrawEllipse(null, HaloPen, p, MarkerRadius, MarkerRadius);
        dc.DrawEllipse(null, pen, p, MarkerRadius, MarkerRadius);
        Segment(dc, p + new Vector(-MarkerRadius - 4, 0), p + new Vector(-MarkerRadius + 3, 0), pen);
        Segment(dc, p + new Vector(MarkerRadius - 3, 0), p + new Vector(MarkerRadius + 4, 0), pen);
        Segment(dc, p + new Vector(0, -MarkerRadius - 4), p + new Vector(0, -MarkerRadius + 3), pen);
        Segment(dc, p + new Vector(0, MarkerRadius - 3), p + new Vector(0, MarkerRadius + 4), pen);
        Chip(dc, marker.MarkerLabel, p + new Vector(MarkerRadius + 8, -MarkerRadius - 14), selected);
    }

    private static void Segment(DrawingContext dc, Point a, Point b, Pen pen)
    {
        dc.DrawLine(HaloPen, a, b);
        dc.DrawLine(pen, a, b);
    }

    private static void Dot(DrawingContext dc, Point p, Brush fill)
    {
        dc.DrawEllipse(fill, HaloPen, p, 2.5, 2.5);
    }

    // The smaller arc between the two arms, at a radius that stays inside short arms.
    private static void Arc(DrawingContext dc, Point vertex, Point a, Point b, Pen pen)
    {
        Vector u = a - vertex, v = b - vertex;
        if (u.Length < 1 || v.Length < 1) return;
        double r = Math.Min(20, 0.5 * Math.Min(u.Length, v.Length));
        u.Normalize();
        v.Normalize();
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(vertex + u * r, false, false);
            var sweep = Vector.CrossProduct(u, v) > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
            g.ArcTo(vertex + v * r, new Size(r, r), 0, false, sweep, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, HaloPen, geometry);
        dc.DrawGeometry(null, pen, geometry);
    }

    // White on a dark chip, like the heatmap's own labels — readable on any colormap value and in both themes.
    // Selection inverts the chip and thickens the lines rather than adding a colour: the accent would collide
    // with viridis' teal band (DESIGN.Color, "Data colours vs UI colours").
    private void Chip(DrawingContext dc, string text, Point at, bool selected)
    {
        var typeface = new Typeface(TextElement.GetFontFamily(_view), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface,
            11, selected ? Brushes.Black : Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var box = new Rect(at.X - 5, at.Y - 2, ft.Width + 10, ft.Height + 4);
        dc.DrawRoundedRectangle(selected ? SelectedChipBrush : ChipBrush, selected ? HaloPen : null, box, 3, 3);
        dc.DrawText(ft, at);
    }

    private static Point Mid(Point a, Point b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    private static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    // ---- hit testing and input --------------------------------------------------------------------

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var p = hitTestParameters.HitPoint;
        if (!_view.HasImage || !new Rect(_view.RenderSize).Contains(p)) return null;
        bool mine = Tool != MeasureTool.Pan || _draft is not null || _dragMarker is not null
                    || (MeasurementOverlay.GetCanMoveMarkers(_view) && MarkerAt(p) is not null);
        return mine ? new PointHitTestResult(this, p) : null;
    }

    private IPlaneMarker? MarkerAt(Point p) =>
        Markers().Select(m => (Marker: m, D: (_view.MmToScreen(new Vec2(m.X, m.Y)) - p).Length))
            .Where(t => t.D <= MarkerHitRadius)
            .OrderBy(t => t.D)
            .Select(t => t.Marker)
            .FirstOrDefault();

    private bool Inside(Vec2 mm)
    {
        var (min, max) = _view.ExtentMm;
        return mm.X >= min.X && mm.X <= max.X && mm.Y >= min.Y && mm.Y <= max.Y;
    }

    private Vec2 Clamp(Vec2 mm)
    {
        var (min, max) = _view.ExtentMm;
        return new Vec2(Math.Clamp(mm.X, min.X, max.X), Math.Clamp(mm.Y, min.Y, max.Y));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _view.Focus();
        var p = e.GetPosition(this);
        var mm = _view.ScreenToMm(p);
        e.Handled = true;

        if (Tool == MeasureTool.Pan)
        {
            if (!MeasurementOverlay.GetCanMoveMarkers(_view) || MarkerAt(p) is not { } marker) return;
            _dragMarker = marker;
            _view.SetCurrentValue(MeasurementOverlay.SelectedMarkerProperty, marker);
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            return;
        }

        if (_draft is null && !Inside(mm)) return;   // measurements start on the image, not the letterbox
        mm = Clamp(mm);
        _cursorMm = mm;
        if (Tool == MeasureTool.Angle)
        {
            (_draft ??= []).Add(mm);
            if (_draft.Count == 3) Commit(MeasurementKind.Angle, _draft);
        }
        else
        {
            _draft = [mm];
            CaptureMouse();
        }
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        _view.HoverAt(p);
        var mm = Clamp(_view.ScreenToMm(p));

        if (_dragMarker is { } marker && IsMouseCaptured)
        {
            marker.X = Math.Round(mm.X, MarkerSnapDigits);
            marker.Y = Math.Round(mm.Y, MarkerSnapDigits);
            return;   // the marker's PropertyChanged repaints
        }

        Cursor = Tool != MeasureTool.Pan ? Cursors.Cross : MarkerAt(p) is not null ? Cursors.Hand : null;
        if (_draft is not null)
        {
            _cursorMm = mm;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragMarker is not null)
        {
            _dragMarker = null;
            ReleaseMouseCapture();
            Cursor = Cursors.Hand;
            return;
        }
        if (_draft is not { Count: 1 } d || Tool is not (MeasureTool.Distance or MeasureTool.Roi)) return;

        ReleaseMouseCapture();
        var end = Clamp(_view.ScreenToMm(e.GetPosition(this)));
        if ((_view.MmToScreen(end) - _view.MmToScreen(d[0])).Length < MinDragPx) { CancelDraft(); return; }
        Commit(Tool == MeasureTool.Distance ? MeasurementKind.Distance : MeasurementKind.Roi, [d[0], end]);
    }

    // Alt+Tab, the Windows key or a modal can take the capture mid-gesture. Without this the marker kept following a
    // released mouse, and the next click committed a measurement from a stale start point.
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_dragMarker is not null)
        {
            _dragMarker = null;
            Cursor = null;
            InvalidateVisual();
        }
        if (_draft is not null && Tool != MeasureTool.Angle) CancelDraft();   // angle drafts are click-by-click, no capture
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (_draft is null) return;
        CancelDraft();
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _view.ZoomStep(e.GetPosition(this), e.Delta > 0);
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!IsMouseCaptured) _view.HoverAt(null);
    }

    // Keys arrive at the focused heatmap: Esc abandons a half-drawn measurement, Delete removes the selected one.
    private void OnViewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _draft is not null)
        {
            CancelDraft();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && _session?.DeleteCommand.CanExecute(null) == true)
        {
            _session.DeleteCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Commit(MeasurementKind kind, IReadOnlyList<Vec2> points)
    {
        var draft = new MeasurementDraft(Pane, kind, points.ToArray());
        CancelDraft();
        if (_session?.AddCommand.CanExecute(draft) == true) _session.AddCommand.Execute(draft);
    }

    private void CancelDraft()
    {
        _draft = null;
        _cursorMm = null;
        if (IsMouseCaptured && _dragMarker is null) ReleaseMouseCapture();
        InvalidateVisual();
    }
}
