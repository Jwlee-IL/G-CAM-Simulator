using System.Globalization;
using Gcam.Studio.Core.Imaging;

namespace Gcam.Studio.Core.Plotting;

/// <summary>1–2–5 linear steps with engineering labels; log decades with grouped numbers or superscript powers.</summary>
public static class NiceTicks
{
    public static IReadOnlyList<PlotTick> Linear(double min, double max, int count = 6)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min || count < 2) return [];
        double raw = (max - min) / (count - 1), power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double fraction = raw / power;
        double step = (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * power;
        double first = Math.Ceiling(min / step) * step;
        int n = Math.Max(0, (int)Math.Floor((max - first) / step + 1e-10) + 1);
        if (n == 0) return [];
        var labels = TickFormatter.Labels(first, first + step * (n - 1), Math.Max(2, n));
        return Enumerable.Range(0, n).Select(i => new PlotTick(first + i * step, labels[n == 1 ? 1 : i])).ToArray();
    }

    public static IReadOnlyList<PlotTick> Logarithmic(double min, double max)
    {
        min = Math.Max(PlotViewport.LogFloor, min);
        if (!double.IsFinite(max) || max < min) return [];
        var ticks = new List<PlotTick>();
        for (int decade = (int)Math.Floor(Math.Log10(min)); decade <= (int)Math.Ceiling(Math.Log10(max)); decade++)
        {
            double basis = Math.Pow(10, decade);
            for (int m = 1; m <= 9; m++)
            {
                double v = basis * m;
                if (v < min || v > max) continue;
                string label = decade <= 5 ? v.ToString("N0", CultureInfo.InvariantCulture)
                    : "10" + TickFormatter.Superscript(decade);
                ticks.Add(new(v, m == 1 ? label : "", m == 1));
            }
        }
        return ticks;
    }
}
