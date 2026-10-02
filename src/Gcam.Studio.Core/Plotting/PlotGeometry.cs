namespace Gcam.Studio.Core.Plotting;

/// <summary>Visible step geometry or exact device-column extrema, bounded by the output resolution.</summary>
public static class PlotGeometry
{
    private const double MinimumStepPixels = 2;
    public static PlotRange VisibleRange(PlotSeries series, MinMaxPyramid pyramid, double lo, double hi)
    {
        if (series.Kind != PlotKind.Histogram)
        {
            // Include crossing segments as well as centres inside the view.
            return pyramid.Range(Math.Max(0, series.LowerBound(lo) - 1), Math.Min(series.Y.Length, series.LowerBound(hi) + 1));
        }
        if (series.Y.Length == 0 || hi <= series.EdgeAt(0) || lo >= series.EdgeAt(series.Y.Length)) return pyramid.Range(0, 0);
        int start = series.BinAt(Math.Max(lo, series.EdgeAt(0)));
        int end = hi >= series.EdgeAt(series.Y.Length) ? series.Y.Length : series.BinAt(hi);
        if (end < series.Y.Length && hi > series.EdgeAt(end)) end++;
        return pyramid.Range(start, end);
    }

    /// <param name="logX">Device columns are equal in log10(x) (a log X axis); <paramref name="lo"/> must then be positive.</param>
    public static IReadOnlyList<PlotPoint> Build(PlotSeries series, MinMaxPyramid pyramid, double lo, double hi, int columns, bool logX = false)
    {
        if (columns < 1 || !double.IsFinite(lo) || !double.IsFinite(hi) || hi <= lo || (logX && lo <= 0)) throw new ArgumentOutOfRangeException(nameof(columns));
        double Edge(int c) => !logX ? lo + (hi - lo) * c / columns : c == columns ? hi : lo * Math.Pow(hi / lo, (double)c / columns);
        var points = new List<PlotPoint>();
        if (series.Kind == PlotKind.Histogram)
        {
            var visible = VisibleRange(series, pyramid, lo, hi);
            if (visible.IsEmpty) return points;
            bool steps = true;
            for (int i = visible.Start; i < visible.End; i++)
                if ((series.EdgeAt(i + 1) - series.EdgeAt(i)) / (hi - lo) * columns < MinimumStepPixels) { steps = false; break; }
            if (steps)
            {
                for (int i = visible.Start; i < visible.End; i++)
                {
                    points.Add(new(Math.Max(lo, series.EdgeAt(i)), series.Y[i]));
                    points.Add(new(Math.Min(hi, series.EdgeAt(i + 1)), series.Y[i]));
                }
                return points;
            }
        }
        for (int c = 0; c < columns; c++)
        {
            double a = Edge(c), b = Edge(c + 1);
            PlotRange range;
            if (series.Kind == PlotKind.Histogram) range = VisibleRange(series, pyramid, a, b);
            else
            {
                int begin = series.LowerBound(a), end = series.LowerBound(b);
                if (c == columns - 1 && end < series.Y.Length && series.XAt(end) <= b) end++;
                range = pyramid.Range(begin, end);
            }
            if (range.IsEmpty) continue;
            double x = series.Kind != PlotKind.Histogram && range.End - range.Start == 1 ? series.XAt(range.Start) : logX ? Math.Sqrt(a * b) : (a + b) / 2;
            points.Add(new(x, range.Min));
            points.Add(new(x, range.Max));
        }
        return points;
    }
}
