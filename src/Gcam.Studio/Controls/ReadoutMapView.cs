using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using Gcam.Studio.Core.Detector;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Rendering;

namespace Gcam.Studio.Controls;

/// <summary>Resolved SiPM circuit or raw Anger density with the prepared LUT boundaries and peak crosses.</summary>
public sealed class ReadoutMapView : FrameworkElement
{
    public static readonly DependencyProperty PreparationProperty = DependencyProperty.Register(nameof(Preparation), typeof(ReadoutPreparation), typeof(ReadoutMapView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public ReadoutPreparation? Preparation { get => (ReadoutPreparation?)GetValue(PreparationProperty); set => SetValue(PreparationProperty, value); }
    public static readonly DependencyProperty SnapshotProperty = DependencyProperty.Register(nameof(Snapshot), typeof(ReadoutSnapshot), typeof(ReadoutMapView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public ReadoutSnapshot? Snapshot { get => (ReadoutSnapshot?)GetValue(SnapshotProperty); set => SetValue(SnapshotProperty, value); }
    public static readonly DependencyProperty TabProperty = DependencyProperty.Register(nameof(Tab), typeof(ReadoutDetectorTab), typeof(ReadoutMapView), new FrameworkPropertyMetadata(ReadoutDetectorTab.Sipm, FrameworkPropertyMetadataOptions.AffectsRender));
    public ReadoutDetectorTab Tab { get => (ReadoutDetectorTab)GetValue(TabProperty); set => SetValue(TabProperty, value); }
    public static readonly DependencyProperty ShowLiveProperty = DependencyProperty.Register(nameof(ShowLive), typeof(bool), typeof(ReadoutMapView), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool ShowLive { get => (bool)GetValue(ShowLiveProperty); set => SetValue(ShowLiveProperty, value); }
    private static readonly Brush[] Palette = Colormap.Viridis.Select(v =>
    { var b = new SolidColorBrush(Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v)); b.Freeze(); return (Brush)b; }).ToArray();
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Preparation is not { } p || ActualWidth <= 0 || ActualHeight <= 0) return;
        double side = Math.Min(ActualWidth, ActualHeight), left = (ActualWidth - side) / 2, top = (ActualHeight - side) / 2;
        Brush line = (Brush)FindResource("Brush.Line.Control"), ink = (Brush)FindResource("Brush.Text.Primary"), accent = (Brush)FindResource("Brush.Accent");
        var pen = new Pen(line, 1); var peakPen = new Pen(accent, 1);
        Point Map(double x, double y) => new(left + side * x, top + side * (1 - y));
        dc.DrawRectangle((Brush)FindResource("Brush.Bg.Canvas"), pen, new(left, top, side, side));
        if (Tab == ReadoutDetectorTab.Sipm && p.Circuit is { } circuit)
        {
            Point Node(int n)
            {
                string name = circuit.Nodes[n];
                if (name.StartsWith('S')) { var xy = name[1..].Split('_'); return Map((int.Parse(xy[0], CultureInfo.InvariantCulture) + 1.5) / 15, (int.Parse(xy[1], CultureInfo.InvariantCulture) + 1.5) / 15); }
                if (name.StartsWith('L') || name.StartsWith('R')) return Map(name[0] == 'L' ? .025 : .975, (int.Parse(name[1..], CultureInfo.InvariantCulture) + 1.5) / 15);
                int c = "ABCD".IndexOf(name[^1]); return Map(c % 2 == 0 ? .025 : .975, c < 2 ? .025 : .975);
            }
            for (int k = 0; k < 144; k++)
            {
                Point c = Node(k); double w = p.SensorActiveWidthMm * side / 15;
                double cell = side / 15;
                dc.DrawRectangle(null, pen, new(c.X - cell / 2, c.Y - cell / 2, cell, cell));
                dc.DrawRectangle(Palette[160], null, new(c.X - w / 2, c.Y - w / 2, w, w));
            }
            foreach (var r in circuit.Resistors) if (r.A >= 0 && r.B >= 0) dc.DrawLine(pen, Node(r.A), Node(r.B));
            for (int n = 0; n < circuit.Nodes.Count; n++) if (circuit.Nodes[n].StartsWith("OUT_", StringComparison.Ordinal))
            {
                Point at = Node(n);
                dc.DrawText(new FormattedText(circuit.Nodes[n][^1].ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface((FontFamily)FindResource("Font.Mono"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    (double)FindResource("FontSize.Caption"), ink, VisualTreeHelper.GetDpi(this).PixelsPerDip), at);
            }
        }
        else if (Tab == ReadoutDetectorTab.Anger)
        {
            var density = ShowLive ? Snapshot?.LiveDensity : p.Density;
            if (density is not null)
            {
                double max = density.Raw.ToArray().DefaultIfEmpty().Max();
                for (int y = 0; y < density.Height; y++) for (int x = 0; x < density.Width; x++)
                    if (density[x,y] > 0)
                    {
                        Point at = Map((double)x / density.Width, (double)(y + 1) / density.Height);
                        int colour = max > 0 ? (int)Math.Clamp(Math.Round(255 * density[x,y] / max), 0, 255) : 0;
                        dc.DrawRectangle(Palette[colour], null, new(at.X, at.Y, side / density.Width, side / density.Height));
                    }
            }
            int nx = p.Density.Width, ny = p.Density.Height;
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                int c = p.Labels[y * nx + x]; if (c < 0) continue;
                if (x + 1 < nx && c != p.Labels[y * nx + x + 1]) dc.DrawLine(pen, Map((double)(x + 1) / nx, (double)y / ny), Map((double)(x + 1) / nx, (double)(y + 1) / ny));
                if (y + 1 < ny && c != p.Labels[(y + 1) * nx + x]) dc.DrawLine(pen, Map((double)x / nx, (double)(y + 1) / ny), Map((double)(x + 1) / nx, (double)(y + 1) / ny));
            }
            foreach (var peak in p.Peaks)
            {
                Point at = Map((peak.X + 1) / 2, (peak.Y + 1) / 2);
                dc.DrawLine(peakPen, new(at.X - 3, at.Y), new(at.X + 3, at.Y));
                dc.DrawLine(peakPen, new(at.X, at.Y - 3), new(at.X, at.Y + 3));
            }
        }
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new MapPeer(this);
    private sealed class MapPeer(ReadoutMapView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(ReadoutMapView);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string GetItemStatusCore() => owner.Preparation is { } p ? $"{owner.Tab}; {p.Peaks.Count} calibration peaks; calibration {p.Id}; live {owner.ShowLive}" : "Not prepared";
    }
}
