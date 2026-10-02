using System.Windows;
using System.Windows.Controls;

namespace Gcam.Studio.Controls;

/// <summary>
/// Lays out an image with its legend: the first child is offered a <b>square</b> as large as the panel allows, and
/// the other children (colour bar, readout) stack directly below it at their desired height, at the width the image
/// actually takes. The group is centred horizontally and sits at the top; leftover height collects below it.
/// </summary>
/// <remarks>
/// The images are square grids. Letting the heatmap fill a tall panel centred the image in empty space and left the
/// colour bar far below the data it explains. A heatmap snaps its cells to whole device pixels, so it can draw a
/// little smaller than the square offered; the legend follows the drawn width, not the offered one (L-12).
/// </remarks>
public sealed class ImageStackPanel : Panel
{
    private double _width, _height;

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        double below = MeasureLegend(width);

        double heightLeft = double.IsInfinity(availableSize.Height) ? width : availableSize.Height - below;
        double side = Math.Max(0, Math.Min(width, heightLeft));
        _width = _height = side;
        if (InternalChildren.Count > 0)
        {
            var image = InternalChildren[0];
            image.Measure(new Size(side, side));
            // An element that reports no size of its own takes the whole square.
            if (image.DesiredSize.Width > 0) _width = Math.Min(side, image.DesiredSize.Width);
            if (image.DesiredSize.Height > 0) _height = Math.Min(side, image.DesiredSize.Height);
        }
        if (_width < width) below = MeasureLegend(_width);   // a narrower legend may wrap to more lines
        return new Size(_width, _height + below);
    }

    private double MeasureLegend(double width)
    {
        double below = 0;
        for (int i = 1; i < InternalChildren.Count; i++)
        {
            InternalChildren[i].Measure(new Size(width, double.PositiveInfinity));
            below += InternalChildren[i].DesiredSize.Height;
        }
        return below;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = Math.Max(0, (finalSize.Width - _width) / 2);
        double y = 0;
        for (int i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            double h = i == 0 ? _height : child.DesiredSize.Height;
            child.Arrange(new Rect(x, y, _width, h));
            y += h;
        }
        return finalSize;
    }
}
