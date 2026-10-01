using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>
/// Independent expectation for a measurement drawn on the flood map: where the image sits inside the heatmap and
/// which mm a screen pixel corresponds to — re-derived from the documented layout rules, not by calling the app's code.
/// </summary>
/// <remarks>
/// The heatmap fits the N×N image with a whole number of device pixels per cell, centred (DESIGN.Controls). The
/// detector is centred on the axis, so its outer edges are at ±N·pitch/2 mm, with +y up on screen.
/// </remarks>
public sealed record FloodOracle(Rect ViewBounds, int Cells, double PitchMm)
{
    /// <summary>Device pixels per cell at fit.</summary>
    public int CellPx => (int)Math.Floor(Math.Min(ViewBounds.Width, ViewBounds.Height) / Cells);

    public Rect ImageBounds
    {
        get
        {
            double size = CellPx * Cells;
            return new Rect(ViewBounds.X + (ViewBounds.Width - size) / 2, ViewBounds.Y + (ViewBounds.Height - size) / 2, size, size);
        }
    }

    /// <summary>A point given as fractions of the image (0,0 = top-left), rounded to the pixel the pointer can actually hit.</summary>
    public Point ScreenAt(double fx, double fy)
    {
        var r = ImageBounds;
        return new Point(Math.Round(r.X + fx * r.Width), Math.Round(r.Y + fy * r.Height));
    }

    public (double X, double Y) ScreenToMm(Point p)
    {
        var r = ImageBounds;
        double span = Cells * PitchMm;
        return (-span / 2 + (p.X - r.X) / r.Width * span, span / 2 - (p.Y - r.Y) / r.Height * span);
    }

    public double DistanceMm(Point a, Point b)
    {
        var (ax, ay) = ScreenToMm(a);
        var (bx, by) = ScreenToMm(b);
        return Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
    }

    /// <summary>
    /// Allowed error: each endpoint may land one device pixel off (layout rounding of the view's position), so
    /// 2·√2 pixels of length, plus 0.05 mm for the one-decimal display.
    /// </summary>
    public double ToleranceMm => 2 * Math.Sqrt(2) * PitchMm / CellPx + 0.05;
}

/// <summary>A row of the results table, parsed from its automation name ("M1 Distance on Flood: 12.3 mm").</summary>
public sealed record MeasurementRow(string Name, string Kind, string Pane, string Value)
{
    private static readonly Regex Shape = new(@"^(?<name>M\d+) (?<kind>\w+) on (?<pane>\w+): (?<value>.+)$");

    public static MeasurementRow Parse(string automationName)
    {
        var m = Shape.Match(automationName);
        if (!m.Success) throw new FormatException($"unexpected row name '{automationName}'");
        return new MeasurementRow(m.Groups["name"].Value, m.Groups["kind"].Value, m.Groups["pane"].Value, m.Groups["value"].Value);
    }

    /// <summary>The number in "12.3 mm", in the culture the app formatted it with (same machine, same culture).</summary>
    public double LengthMm()
    {
        if (!Value.EndsWith(" mm", StringComparison.Ordinal)) throw new FormatException($"not a length: '{Value}'");
        return double.Parse(Value[..^3], NumberStyles.Float, CultureInfo.CurrentCulture);
    }
}

public static class Verdict
{
    public static bool Within(double actual, double expected, double tolerance) => Math.Abs(actual - expected) <= tolerance;
}
