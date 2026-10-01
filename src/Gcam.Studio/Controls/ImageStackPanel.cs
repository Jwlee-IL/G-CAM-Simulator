using System.Windows;
using System.Windows.Controls;

namespace Gcam.Studio.Controls;

/// <summary>
/// Lays out an image with its legend: the first child is kept <b>square</b> and as large as the panel allows, and the
/// other children (colour bar, readout) stack directly below it at their desired height, at the image's width. The
/// group is centred horizontally and sits at the top; leftover height collects below it.
/// </summary>
/// <remarks>
/// The images are square grids. Letting the heatmap fill a tall panel centred the image in empty space and left the
/// colour bar far below the data it explains.
/// </remarks>
public sealed class ImageStackPanel : Panel
{
    private double _side;

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        double below = 0;
        for (int i = 1; i < InternalChildren.Count; i++)
        {
            InternalChildren[i].Measure(new Size(width, double.PositiveInfinity));
            below += InternalChildren[i].DesiredSize.Height;
        }

        double heightLeft = double.IsInfinity(availableSize.Height) ? width : availableSize.Height - below;
        _side = Math.Max(0, Math.Min(width, heightLeft));
        if (InternalChildren.Count > 0) InternalChildren[0].Measure(new Size(_side, _side));
        return new Size(_side, _side + below);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = Math.Max(0, (finalSize.Width - _side) / 2);
        double y = 0;
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            double h = i == 0 ? _side : child.DesiredSize.Height;
            child.Arrange(new Rect(x, y, _side, h));
            y += h;
        }
        return finalSize;
    }
}
