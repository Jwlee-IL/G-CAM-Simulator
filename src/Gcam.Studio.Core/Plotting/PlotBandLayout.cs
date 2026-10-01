namespace Gcam.Studio.Core.Plotting;

/// <summary>Centre on each band, clamp to the plot, then choose the first free row.</summary>
public static class PlotBandLayout
{
    public static IReadOnlyList<PlotBandLabel> Arrange(IReadOnlyList<(double Centre, double Width)> labels, double width, double gap)
    {
        if (!double.IsFinite(width) || width <= 0 || !double.IsFinite(gap) || gap < 0) throw new ArgumentOutOfRangeException(nameof(width));
        var result = new List<PlotBandLabel>();
        foreach (var item in labels.Select((label, index) => (label.Centre, label.Width, Index: index)).OrderBy(l => l.Centre))
        {
            double w = Math.Clamp(item.Width, 0, width), left = Math.Clamp(item.Centre - w / 2, 0, width - w);
            int row = 0;
            while (result.Any(l => l.Row == row && left < l.Left + l.Width + gap && left + w + gap > l.Left)) row++;
            result.Add(new(item.Index, left, w, row));
        }
        return result;
    }
}
