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
        var labels = TickFormatter.StepLabels(first, step, n);
        return Enumerable.Range(0, n).Select(i => new PlotTick(first + i * step, labels[i])).ToArray();
    }

    /// <summary>
    /// Ticks for a log10 X axis over a positive range: 1, 2 and 5 of each decade are labelled grid lines, the other
    /// mantissas unlabelled minor ticks. A range too narrow for two labelled ticks falls back to linear 1–2–5 steps,
    /// which are equally valid positions on a log axis.
    /// </summary>
    public static IReadOnlyList<PlotTick> LogarithmicAxis(double min, double max)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || min <= 0 || max <= min) return [];
        var ticks = new List<PlotTick>();
        for (int decade = (int)Math.Floor(Math.Log10(min)); decade <= (int)Math.Ceiling(Math.Log10(max)); decade++)
        {
            double basis = Math.Pow(10, decade);
            for (int m = 1; m <= 9; m++)
            {
                double v = basis * m;
                if (v < min * (1 - 1e-12) || v > max * (1 + 1e-12)) continue;
                bool major = m is 1 or 2 or 5;
                string label = !major ? ""
                    : decade is >= 0 and <= 5 ? v.ToString("N0", CultureInfo.InvariantCulture)
                    : decade < 0 ? v.ToString("G3", CultureInfo.InvariantCulture)
                    : m == 1 ? "10" + TickFormatter.Superscript(decade)
                    : m.ToString(CultureInfo.InvariantCulture) + "×10" + TickFormatter.Superscript(decade);
                ticks.Add(new(v, label, major));
            }
        }
        return ticks.Count(t => t.IsMajor) >= 2 ? ticks : Linear(min, max);
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
