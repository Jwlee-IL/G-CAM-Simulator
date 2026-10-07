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

    /// <summary>Centre of cell (<paramref name="col"/>, <paramref name="rowFromTop"/>) in mm — what the readout reports.</summary>
    public (double X, double Y) CellCentreMm(int col, int rowFromTop) =>
        (-Cells * PitchMm / 2 + (col + 0.5) * PitchMm, Cells * PitchMm / 2 - (rowFromTop + 0.5) * PitchMm);

    /// <summary>The screen point in the middle of a cell, for hovering.</summary>
    public Point CellCentre(int col, int rowFromTop) => ScreenAt((col + 0.5) / Cells, (rowFromTop + 0.5) / Cells);

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
    /// The screen point on the boundary before cell (<paramref name="col"/>, <paramref name="rowFromTop"/>). ROI corners go
    /// here, half a cell from every pixel centre, so a pixel of pointer or layout error can't change which centres
    /// are inside (a corner on a centre would flip with half a pixel).
    /// </summary>
    public Point CellCorner(int col, int rowFromTop) => ScreenAt((double)col / Cells, (double)rowFromTop / Cells);

    /// <summary>Pixels whose centres lie in the rectangle spanned by two screen points (the ROI rule, re-derived).</summary>
    public int PixelsInside(Point a, Point b)
    {
        var (ax, ay) = ScreenToMm(a);
        var (bx, by) = ScreenToMm(b);
        double half = Cells * PitchMm / 2;
        int Count(double lo, double hi) =>
            Enumerable.Range(0, Cells).Count(i => -half + (i + 0.5) * PitchMm is var c && c >= lo && c <= hi);
        return Count(Math.Min(ax, bx), Math.Max(ax, bx)) * Count(Math.Min(ay, by), Math.Max(ay, by));
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

    /// <summary>The number in "67.0°".</summary>
    public double AngleDeg()
    {
        if (!Value.EndsWith('°')) throw new FormatException($"not an angle: '{Value}'");
        return double.Parse(Value[..^1], NumberStyles.Float, CultureInfo.CurrentCulture);
    }
}

public static class Verdict
{
    public static bool Within(double actual, double expected, double tolerance) => Math.Abs(actual - expected) <= tolerance;

    /// <summary>Angle at <paramref name="vertex"/> in degrees, from screen points. A uniform scale and a y-flip (screen →
    /// mm) don't change it, so it needs no knowledge of the image's mm frame.</summary>
    public static double AngleDeg(Point a, Point vertex, Point b)
    {
        Vector u = a - vertex, v = b - vertex;
        return Math.Abs(Vector.AngleBetween(u, v));
    }

    /// <summary>Worst-case angle error if each of the three clicks lands one pixel (diagonal) off.</summary>
    public static double AngleToleranceDeg(Point a, Point vertex, Point b)
    {
        double arm1 = (a - vertex).Length, arm2 = (b - vertex).Length;
        double rad = 2 * Math.Atan(Math.Sqrt(2) / arm1) + 2 * Math.Atan(Math.Sqrt(2) / arm2);
        return rad * 180 / Math.PI + 0.05;
    }

    private static readonly Regex Peak = new(@"^peak \((?<x>[^,]+), (?<y>[^)]+)\) mm$");

    /// <summary>"peak (10.0, 0.2) mm" → (10.0, 0.2).</summary>
    public static (double X, double Y) ParsePeak(string text)
    {
        var m = Peak.Match(text);
        if (!m.Success) throw new FormatException($"unexpected peak text '{text}'");
        return (double.Parse(m.Groups["x"].Value, NumberStyles.Float, CultureInfo.CurrentCulture),
                double.Parse(m.Groups["y"].Value, NumberStyles.Float, CultureInfo.CurrentCulture));
    }

    // The value is grouped ("3,077", four significant digits) and may be followed by the heatmap's unit
    // ("counts", "(decoded)").
    private static readonly Regex Readout = new(@"^x (?<x>-?[\d.]+) mm, y (?<y>-?[\d.]+) mm · (?<v>-?[\d,]+(?:\.\d+)?)(?: .+)?$");

    /// <summary>The heatmap readout "x -6.3 mm, y 5.1 mm · 3,077 counts" (invariant culture) → (x, y, value);
    /// the unit suffix is optional.</summary>
    public static (double X, double Y, double Value) ParseReadout(string text)
    {
        var m = Readout.Match(text);
        if (!m.Success) throw new FormatException($"unexpected readout '{text}'");
        return (double.Parse(m.Groups["x"].Value, CultureInfo.InvariantCulture),
                double.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture),
                double.Parse(m.Groups["v"].Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture));
    }

    private static readonly Regex ReadoutUnitPattern = new(@"^x -?[\d.]+ mm, y -?[\d.]+ mm · -?[\d,]+(?:\.\d+)? (?<u>.+)$");

    /// <summary>The unit suffix of a heatmap readout: "counts" (flood), "(decoded)" (cross-correlation) or "(MLEM λ)"
    /// (TODO-36 MLEM reconstruction); throws when the readout has none.</summary>
    public static string ParseReadoutUnit(string text)
    {
        var m = ReadoutUnitPattern.Match(text);
        if (!m.Success) throw new FormatException($"readout without a unit: '{text}'");
        return m.Groups["u"].Value;
    }

    /// <summary>"Σ 0.0107" (the app's culture) → 0.0107.</summary>
    public static double ParseSum(string value)
    {
        if (!value.StartsWith("Σ ", StringComparison.Ordinal)) throw new FormatException($"not a sum: '{value}'");
        return double.Parse(value[2..], NumberStyles.Float, CultureInfo.CurrentCulture);
    }

    private static readonly Regex RoiDetail = new(@"^(?<w>[\d.,]+) × (?<h>[\d.,]+) mm · (?<n>\d+) px · ");

    /// <summary>ROI detail "1.8 × 1.8 mm · 9 px · mean … · max …" → (w, h, pixels).</summary>
    public static (double W, double H, int Pixels) ParseRoiDetail(string text)
    {
        var m = RoiDetail.Match(text);
        if (!m.Success) throw new FormatException($"unexpected ROI detail '{text}'");
        return (double.Parse(m.Groups["w"].Value, NumberStyles.Float, CultureInfo.CurrentCulture),
                double.Parse(m.Groups["h"].Value, NumberStyles.Float, CultureInfo.CurrentCulture),
                int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture));
    }
}
