using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gcam.Studio.Core.Detector;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Rendering;

namespace Gcam.Studio.Controls;

/// <summary>Resolved SiPM circuit or raw Anger density with the prepared LUT boundaries and peak marks, for any captured
/// module size (TODO-41, SD-A14). The density is drawn from a cached raster (one pixel per LUT bin, viridis, built once per
/// density image); boundaries, peak marks and the circuit are cached frozen geometries in normalised coordinates, so a
/// redraw only transforms them. Sensors use the captured module's actual counts, pitch and active width. Zoom (wheel,
/// + / −, 0 resets) and pan (drag, arrow keys) work on the cached layers; <see cref="Inspection"/> names the LUT crystal
/// and bin value under the pointer (also reported in the automation ItemStatus).</summary>
public sealed class ReadoutMapView : FrameworkElement
{
    public static readonly DependencyProperty PreparationProperty = DependencyProperty.Register(nameof(Preparation), typeof(ReadoutPreparation), typeof(ReadoutMapView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnLayersChanged));
    public ReadoutPreparation? Preparation { get => (ReadoutPreparation?)GetValue(PreparationProperty); set => SetValue(PreparationProperty, value); }
    public static readonly DependencyProperty SnapshotProperty = DependencyProperty.Register(nameof(Snapshot), typeof(ReadoutSnapshot), typeof(ReadoutMapView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnLayersChanged));
    public ReadoutSnapshot? Snapshot { get => (ReadoutSnapshot?)GetValue(SnapshotProperty); set => SetValue(SnapshotProperty, value); }
    public static readonly DependencyProperty TabProperty = DependencyProperty.Register(nameof(Tab), typeof(ReadoutDetectorTab), typeof(ReadoutMapView), new FrameworkPropertyMetadata(ReadoutDetectorTab.Sipm, FrameworkPropertyMetadataOptions.AffectsRender));
    public ReadoutDetectorTab Tab { get => (ReadoutDetectorTab)GetValue(TabProperty); set => SetValue(TabProperty, value); }
    public static readonly DependencyProperty ShowLiveProperty = DependencyProperty.Register(nameof(ShowLive), typeof(bool), typeof(ReadoutMapView), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnLayersChanged));
    public bool ShowLive { get => (bool)GetValue(ShowLiveProperty); set => SetValue(ShowLiveProperty, value); }
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(ReadoutMapView), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, null, (_, v) => Math.Clamp((double)v, 1, MaximumZoom)));
    /// <summary>Magnification of the square map (1 = whole module).</summary>
    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    private static readonly DependencyPropertyKey InspectionKey = DependencyProperty.RegisterReadOnly(nameof(Inspection), typeof(string), typeof(ReadoutMapView), new PropertyMetadata(""));
    public static readonly DependencyProperty InspectionProperty = InspectionKey.DependencyProperty;
    /// <summary>The LUT crystal and density under the pointer, or the zoom / pan hint.</summary>
    public string Inspection => (string)GetValue(InspectionProperty);

    public const double MaximumZoom = 32;
    private static readonly uint[] Palette = Colormap.Viridis.Select(v => 0xFF000000u | v).ToArray();
    private BitmapSource? _raster;
    private Geometry? _boundaries, _peaks, _circuitLines, _sensorCells, _sensorActive;
    private (double X, double Y) _center = (.5, .5);
    private Point? _drag;

    public ReadoutMapView()
    {
        Focusable = true;
        ClipToBounds = true;
        // One raster pixel per LUT bin: magnify without smoothing so bins stay inspectable when zoomed.
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    /// <summary>Time spent building the cached layers last time (rendering diagnostics).</summary>
    public TimeSpan LastCacheBuild { get; private set; }

    private static void OnLayersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ReadoutMapView)d;
        // A live snapshot tick only replaces the live raster; boundaries, peaks and the circuit belong to the preparation.
        if (e.Property == SnapshotProperty && !view.ShowLive) return;
        view._raster = null;
        if (e.Property != PreparationProperty) return;
        view._boundaries = null; view._peaks = null;
        view._circuitLines = null; view._sensorCells = null; view._sensorActive = null;
        view._center = (.5, .5); view.Zoom = 1;
    }

    private Rect Square()
    {
        double side = Math.Min(ActualWidth, ActualHeight);
        return new((ActualWidth - side) / 2, (ActualHeight - side) / 2, side, side);
    }

    // Normalised (0…1, y up) → screen, through zoom and pan.
    private Matrix Transform(Rect square)
    {
        double scale = square.Width * Zoom;
        var m = new Matrix(scale, 0, 0, -scale, 0, 0);
        m.Translate(square.Left + square.Width / 2 - _center.X * scale, square.Top + square.Height / 2 + _center.Y * scale);
        return m;
    }

    private void EnsureLayers(ReadoutPreparation p)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool built = false;
        if (Tab == ReadoutDetectorTab.Anger)
        {
            var density = ShowLive ? Snapshot?.LiveDensity : p.Density;
            if (_raster is null && density is not null) { _raster = Raster(density); built = true; }
            if (_boundaries is null) { _boundaries = Boundaries(p); built = true; }
            if (_peaks is null) { _peaks = Peaks(p); built = true; }
        }
        else if (Tab == ReadoutDetectorTab.Sipm && p.Circuit is { } circuit && _circuitLines is null)
        { BuildCircuit(p, circuit); built = true; }
        if (built) LastCacheBuild = watch.Elapsed;
    }

    private static BitmapSource Raster(Gcam.Core.DetectorImage density)
    {
        int w = density.Width, h = density.Height;
        double max = 0;
        foreach (double v in density.Raw) max = Math.Max(max, v);
        var pixels = new uint[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double v = density[x, y];
                // Raster rows run top-down; density row 0 is the bottom of the raw-position plane.
                pixels[(h - 1 - y) * w + x] = v > 0 && max > 0 ? Palette[(int)Math.Clamp(Math.Round(255 * v / max), 0, 255)] : 0u;
            }
        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        bitmap.Freeze();
        return bitmap;
    }

    // LUT boundaries as merged straight runs: consecutive bin edges on the same grid line become one segment, so a
    // 1408 × 1408 High-res LUT (≈ 120 000 bin edges) draws as a few thousand figures.
    private static Geometry Boundaries(ReadoutPreparation p)
    {
        int nx = p.Density.Width, ny = p.Density.Height;
        var geometry = new StreamGeometry();
        if (p.Labels.Count == nx * ny)
            using (var g = geometry.Open())
            {
                bool Vertical(int x, int y) => p.Labels[y * nx + x] is var c && c >= 0 && c != p.Labels[y * nx + x + 1];
                bool Horizontal(int x, int y) => p.Labels[y * nx + x] is var c && c >= 0 && c != p.Labels[(y + 1) * nx + x];
                for (int x = 0; x + 1 < nx; x++)
                    for (int y = 0; y < ny; y++)
                    {
                        if (!Vertical(x, y)) continue;
                        int start = y;
                        while (y + 1 < ny && Vertical(x, y + 1)) y++;
                        g.BeginFigure(new((double)(x + 1) / nx, (double)start / ny), false, false);
                        g.LineTo(new((double)(x + 1) / nx, (double)(y + 1) / ny), true, false);
                    }
                for (int y = 0; y + 1 < ny; y++)
                    for (int x = 0; x < nx; x++)
                    {
                        if (!Horizontal(x, y)) continue;
                        int start = x;
                        while (x + 1 < nx && Horizontal(x + 1, y)) x++;
                        g.BeginFigure(new((double)start / nx, (double)(y + 1) / ny), false, false);
                        g.LineTo(new((double)(x + 1) / nx, (double)(y + 1) / ny), true, false);
                    }
            }
        geometry.Freeze();
        return geometry;
    }

    private static Geometry Peaks(ReadoutPreparation p)
    {
        // Cross half-size: a quarter of the nominal spot spacing, so marks scale with the module and the zoom.
        int crystals = p.Module?.CrystalsX ?? Math.Max(1, (int)Math.Round(Math.Sqrt(p.Peaks.Count)));
        double half = .25 / crystals;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
            foreach (var peak in p.Peaks)
            {
                double x = (peak.X + 1) / 2, y = (peak.Y + 1) / 2;
                g.BeginFigure(new(x - half, y), false, false); g.LineTo(new(x + half, y), true, false);
                g.BeginFigure(new(x, y - half), false, false); g.LineTo(new(x, y + half), true, false);
            }
        geometry.Freeze();
        return geometry;
    }

    /// <summary>Sensor grid from the captured module (else the circuit's own sensor nodes), with the n + 3 layout that leaves
    /// room for the row-chain ends and the four outputs.</summary>
    private static (int X, int Y) SensorGrid(ReadoutPreparation p, ReadoutCircuit circuit)
    {
        if (p.Module is { } m) return (m.SensorsX, m.SensorsY);
        int sx = 0, sy = 0;
        foreach (string name in circuit.Nodes.Where(n => n.StartsWith('S')))
        {
            var xy = name[1..].Split('_');
            sx = Math.Max(sx, int.Parse(xy[0], CultureInfo.InvariantCulture) + 1);
            sy = Math.Max(sy, int.Parse(xy[1], CultureInfo.InvariantCulture) + 1);
        }
        return (sx, sy);
    }

    private static Point Node(ReadoutCircuit circuit, int n, int sx, int sy)
    {
        string name = circuit.Nodes[n];
        double cellX = 1.0 / (sx + 3), cellY = 1.0 / (sy + 3);
        if (name.StartsWith('S'))
        {
            var xy = name[1..].Split('_');
            return new((int.Parse(xy[0], CultureInfo.InvariantCulture) + 1.5) * cellX, (int.Parse(xy[1], CultureInfo.InvariantCulture) + 1.5) * cellY);
        }
        if (name.StartsWith('L') || name.StartsWith('R')) return new(name[0] == 'L' ? .025 : .975, (int.Parse(name[1..], CultureInfo.InvariantCulture) + 1.5) * cellY);
        int c = "ABCD".IndexOf(name[^1]);
        return new(c % 2 == 0 ? .025 : .975, c < 2 ? .025 : .975);
    }

    private void BuildCircuit(ReadoutPreparation p, ReadoutCircuit circuit)
    {
        var (sx, sy) = SensorGrid(p, circuit);
        double cellX = 1.0 / (sx + 3), cellY = 1.0 / (sy + 3);
        // Active width as a fraction of the actual sensor pitch (virtual matched sensors: pitch − gap).
        double pitch = p.Module?.SensorPitchMm ?? 0, fraction = pitch > 0 ? Math.Clamp(p.SensorActiveWidthMm / pitch, 0, 1) : 1;
        var cells = new StreamGeometry(); var active = new StreamGeometry(); var lines = new StreamGeometry();
        using (var g = cells.Open())
        using (var a = active.Open())
            for (int k = 0; k < circuit.Nodes.Count; k++)
            {
                if (!circuit.Nodes[k].StartsWith('S')) continue;
                var c = Node(circuit, k, sx, sy);
                Rectangle(g, c, cellX, cellY);
                Rectangle(a, c, cellX * fraction, cellY * fraction);
            }
        using (var g = lines.Open())
            foreach (var r in circuit.Resistors)
                if (r.A >= 0 && r.B >= 0) { g.BeginFigure(Node(circuit, r.A, sx, sy), false, false); g.LineTo(Node(circuit, r.B, sx, sy), true, false); }
        cells.Freeze(); active.Freeze(); lines.Freeze();
        _sensorCells = cells; _sensorActive = active; _circuitLines = lines;

        static void Rectangle(StreamGeometryContext g, Point c, double w, double h)
        {
            g.BeginFigure(new(c.X - w / 2, c.Y - h / 2), true, true);
            g.PolyLineTo([new(c.X + w / 2, c.Y - h / 2), new(c.X + w / 2, c.Y + h / 2), new(c.X - w / 2, c.Y + h / 2)], true, false);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Preparation is not { } p || ActualWidth <= 0 || ActualHeight <= 0) return;
        EnsureLayers(p);
        var square = Square();
        Brush line = (Brush)FindResource("Brush.Line.Control"), ink = (Brush)FindResource("Brush.Text.Primary"), accent = (Brush)FindResource("Brush.Accent");
        var pen = new Pen(line, 1); var peakPen = new Pen(accent, 1);
        dc.DrawRectangle((Brush)FindResource("Brush.Bg.Canvas"), pen, square);
        dc.PushClip(new RectangleGeometry(square));
        var transform = new MatrixTransform(Transform(square));
        transform.Freeze();
        if (Tab == ReadoutDetectorTab.Sipm && p.Circuit is { } circuit && _circuitLines is not null)
        {
            dc.PushTransform(transform);
            // Geometry pens are in normalised units under the transform; divide so strokes stay one device pixel.
            double unit = 1 / (square.Width * Zoom);
            dc.DrawGeometry(Palette160, null, _sensorActive);
            dc.DrawGeometry(null, new Pen(line, unit), _sensorCells);
            dc.DrawGeometry(null, new Pen(line, unit), _circuitLines);
            dc.Pop();
            var m = transform.Matrix;
            var (sx, sy) = SensorGrid(p, circuit);
            for (int n = 0; n < circuit.Nodes.Count; n++) if (circuit.Nodes[n].StartsWith("OUT_", StringComparison.Ordinal))
                dc.DrawText(new FormattedText(circuit.Nodes[n][^1].ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface((FontFamily)FindResource("Font.Mono"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    (double)FindResource("FontSize.Caption"), ink, VisualTreeHelper.GetDpi(this).PixelsPerDip), m.Transform(Node(circuit, n, sx, sy)));
        }
        else if (Tab == ReadoutDetectorTab.Anger)
        {
            dc.PushTransform(transform);
            double unit = 1 / (square.Width * Zoom);
            if (_raster is not null)
            {
                // The raster is top-down; place it on the unit square (y up) by flipping back.
                dc.PushTransform(new MatrixTransform(1, 0, 0, -1, 0, 1));
                dc.DrawImage(_raster, new Rect(0, 0, 1, 1));
                dc.Pop();
            }
            if (_boundaries is not null) dc.DrawGeometry(null, new Pen(line, unit), _boundaries);
            if (_peaks is not null) dc.DrawGeometry(null, new Pen(accent, unit), _peaks);
            dc.Pop();
        }
        dc.Pop();
    }

    private static readonly Brush Palette160 = Freeze(new SolidColorBrush(Color.FromRgb((byte)(Colormap.Viridis[160] >> 16), (byte)(Colormap.Viridis[160] >> 8), (byte)Colormap.Viridis[160])));
    private static Brush Freeze(Brush brush) { brush.Freeze(); return brush; }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ZoomAt(e.GetPosition(this), e.Delta > 0 ? 1.25 : 0.8);
        e.Handled = true;
    }

    /// <summary>Zooms by <paramref name="factor"/> keeping the normalised point under <paramref name="at"/> fixed.</summary>
    public void ZoomAt(Point at, double factor)
    {
        var square = Square();
        if (square.Width <= 0) return;
        var before = Normalised(at, square);
        Zoom = Math.Clamp(Zoom * factor, 1, MaximumZoom);
        var after = Normalised(at, square);
        Pan(before.X - after.X, before.Y - after.Y);
    }

    private (double X, double Y) Normalised(Point at, Rect square)
    {
        var inverse = Transform(square); inverse.Invert();
        var n = inverse.Transform(at);
        return (n.X, n.Y);
    }

    private void Pan(double dx, double dy)
    {
        double half = .5 / Zoom;
        _center = (Math.Clamp(_center.X + dx, half, 1 - half), Math.Clamp(_center.Y + dy, half, 1 - half));
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        _drag = e.GetPosition(this);
        CaptureMouse();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _drag = null;
        ReleaseMouseCapture();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var at = e.GetPosition(this);
        var square = Square();
        if (_drag is { } from && square.Width > 0)
        {
            double scale = square.Width * Zoom;
            Pan(-(at.X - from.X) / scale, (at.Y - from.Y) / scale);
            _drag = at;
        }
        SetValue(InspectionKey, Inspect(Normalised(at, square)));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        double step = .1 / Zoom;
        switch (e.Key)
        {
            case Key.Add or Key.OemPlus: ZoomAt(new(ActualWidth / 2, ActualHeight / 2), 1.25); break;
            case Key.Subtract or Key.OemMinus: ZoomAt(new(ActualWidth / 2, ActualHeight / 2), .8); break;
            case Key.D0 or Key.NumPad0: Zoom = 1; _center = (.5, .5); InvalidateVisual(); break;
            case Key.Left: Pan(-step, 0); break;
            case Key.Right: Pan(step, 0); break;
            case Key.Up: Pan(0, step); break;
            case Key.Down: Pan(0, -step); break;
            default: return;
        }
        e.Handled = true;
    }

    /// <summary>The LUT crystal (row, column) and calibration-density bin under a normalised point.</summary>
    public string Inspect((double X, double Y) at)
    {
        if (Preparation is not { } p || at.X < 0 || at.X >= 1 || at.Y < 0 || at.Y >= 1) return Hint;
        int bx = (int)(at.X * p.Density.Width), by = (int)(at.Y * p.Density.Height);
        int label = p.Labels.Count == p.Density.Width * p.Density.Height ? p.Labels[by * p.Density.Width + bx] : -1;
        int nx = p.Module?.CrystalsX ?? 0;
        string crystal = label < 0 ? "no LUT crystal" : nx > 0 ? $"LUT crystal {label} (column {label % nx}, row {label / nx})" : $"LUT crystal {label}";
        return string.Create(CultureInfo.InvariantCulture,
            $"raw X/Y {at.X * 2 - 1:0.###} / {at.Y * 2 - 1:0.###} · {crystal} · calibration density {p.Density[bx, by]:0.#} · zoom {Zoom:0.##}×");
    }

    private const string Hint = "Wheel or + / − zooms, drag or arrow keys pan, 0 resets; hover to inspect.";

    protected override AutomationPeer OnCreateAutomationPeer() => new MapPeer(this);
    private sealed class MapPeer(ReadoutMapView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(ReadoutMapView);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override bool IsKeyboardFocusableCore() => true;
        protected override string GetItemStatusCore() => owner.Preparation is { } p
            ? string.Create(CultureInfo.InvariantCulture,
                $"{owner.Tab}; line {p.LineKeV:0.##} keV; {p.Peaks.Count} calibration peaks; module {(p.Module is { } m ? $"{m.CrystalsX} x {m.CrystalsY}, sensor pitch {m.SensorPitchMm:0.###} mm" : "not captured")}; calibration {p.Id}; live {owner.ShowLive}; zoom {owner.Zoom:0.##}")
            : "Not prepared";
        protected override string GetHelpTextCore() => owner.Inspection is { Length: > 0 } inspection ? inspection : Hint;
    }
}
