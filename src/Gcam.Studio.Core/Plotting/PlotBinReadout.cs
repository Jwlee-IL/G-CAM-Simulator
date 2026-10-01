using System.Globalization;

namespace Gcam.Studio.Core.Plotting;

/// <summary>Counts and real bin bounds; energy precision follows bin width.</summary>
public static class PlotBinReadout
{
    public static string Format(PlotSeries series, int bin, string xUnit, string yUnit, string? xFormat = null, string yFormat = "N0")
    {
        double lo = series.EdgeAt(bin), hi = series.EdgeAt(bin + 1);
        int digits = Math.Clamp(1 - (int)Math.Floor(Math.Log10(hi - lo)), 0, 12);
        string format = string.IsNullOrEmpty(xFormat) ? $"F{digits}" : xFormat;
        string X(double value) => value.ToString(format, CultureInfo.InvariantCulture);
        return $"{X((lo + hi) / 2)} {xUnit} ({X(lo)} – {X(hi)}) · {series.Y[bin].ToString(yFormat, CultureInfo.InvariantCulture)} {yUnit}";
    }
}
