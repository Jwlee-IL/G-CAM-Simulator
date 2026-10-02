using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using Gcam.Studio.Core.Detector;
using Gcam.Studio.Rendering;

namespace Gcam.Studio.Controls;

/// <summary>Exact vector coverage of crystals and gaps in physical coordinates.</summary>
public sealed class DetectorFaceView : FrameworkElement
{
    public static readonly DependencyProperty FaceProperty = DependencyProperty.Register(nameof(Face), typeof(DetectorFace),
        typeof(DetectorFaceView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public DetectorFace? Face { get => (DetectorFace?)GetValue(FaceProperty); set => SetValue(FaceProperty, value); }
    private static readonly IReadOnlyList<Brush> GainBrushes = Colormap.Viridis.Select(value =>
    {
        var brush = new SolidColorBrush(Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value));
        brush.Freeze();
        return (Brush)brush;
    }).ToArray();
    public static readonly DependencyProperty GapBrushProperty = DependencyProperty.Register(nameof(GapBrush), typeof(Brush),
        typeof(DetectorFaceView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? GapBrush { get => (Brush?)GetValue(GapBrushProperty); set => SetValue(GapBrushProperty, value); }
    /// <summary>The face is square: ask for the largest square that fits, so a legend below can match its width.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? availableSize.Height : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? w : availableSize.Height;
        double side = double.IsInfinity(w) ? 0 : Math.Max(0, Math.Min(w, h));
        return new Size(side, side);
    }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (Face is not { } face || ActualWidth <= 0 || ActualHeight <= 0) return;
        double side = Math.Min(ActualWidth, ActualHeight), scale = side / face.SizeMm;
        double left = (ActualWidth - side) / 2, top = (ActualHeight - side) / 2;
        double lo = face.Crystals.Min(c => c.Gain), hi = face.Crystals.Max(c => c.Gain);
        drawing.DrawRectangle(GapBrush, null, new Rect(left, top, side, side));
        foreach (var crystal in face.Crystals)
        {
            // Use the same data palette as ColorBar; this visual encoding never modifies acquired counts.
            int index = hi > lo ? (int)Math.Clamp(Math.Round(255 * (crystal.Gain - lo) / (hi - lo)), 0, 255) : 0;
            drawing.DrawRectangle(GainBrushes[index], null, Map(crystal));
        }
        foreach (var gap in face.Gaps) drawing.DrawRectangle(GapBrush, null, Map(gap));
        Rect Map(FaceRectangle r) => new(left + r.X * scale, top + (face.SizeMm - r.Y - r.Height) * scale,
            r.Width * scale, r.Height * scale);
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new FacePeer(this);
    private sealed class FacePeer(DetectorFaceView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(DetectorFaceView);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
    }
}
