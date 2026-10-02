using System.Windows;
using System.Windows.Controls;

namespace Gcam.Studio.Controls;

/// <summary>
/// Two rows: the first child (the image pair) takes the height it needs, up to the panel height less
/// <see cref="MinRestHeight"/>; the second child (measurements, focus curve) fills everything below it.
/// </summary>
/// <remarks>
/// The square images are width-limited at the supported window sizes, so a plain star row left their spare height
/// empty under them (P-04). A Grid cannot express "auto, but never squeeze the lower row below a minimum", which a
/// wide but short window needs; this panel does.
/// </remarks>
public sealed class ImageAreaPanel : Panel
{
    public static readonly DependencyProperty MinRestHeightProperty = DependencyProperty.Register(
        nameof(MinRestHeight), typeof(double), typeof(ImageAreaPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Height always left for the second child (set from <c>Size.Imaging.Lower</c>).</summary>
    public double MinRestHeight { get => (double)GetValue(MinRestHeightProperty); set => SetValue(MinRestHeightProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (InternalChildren.Count == 0) return default;
        double height = availableSize.Height;
        var top = InternalChildren[0];
        top.Measure(new Size(availableSize.Width, double.IsInfinity(height) ? height : Math.Max(0, height - MinRestHeight)));
        double rest = double.IsInfinity(height) ? double.PositiveInfinity : Math.Max(0, height - top.DesiredSize.Height);
        double width = top.DesiredSize.Width, below = 0;
        for (int i = 1; i < InternalChildren.Count; i++)
        {
            InternalChildren[i].Measure(new Size(availableSize.Width, rest));
            width = Math.Max(width, InternalChildren[i].DesiredSize.Width);
            below = Math.Max(below, InternalChildren[i].DesiredSize.Height);
        }
        return new Size(width, top.DesiredSize.Height + below);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count == 0) return finalSize;
        double topHeight = Math.Min(finalSize.Height, InternalChildren[0].DesiredSize.Height);
        InternalChildren[0].Arrange(new Rect(0, 0, finalSize.Width, topHeight));
        for (int i = 1; i < InternalChildren.Count; i++)
            InternalChildren[i].Arrange(new Rect(0, topHeight, finalSize.Width, Math.Max(0, finalSize.Height - topHeight)));
        return finalSize;
    }
}
