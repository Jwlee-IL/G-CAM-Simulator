using System.Globalization;

namespace Gcam.Studio.Core.Imaging;

/// <summary>
/// One display rule per kind of number (DESIGN.Typography, "Numbers"): continuous readings to a fixed number of
/// significant digits, grouped; long fixed-resolution fractions grouped in threes. Axis and colour-bar ticks follow
/// <see cref="TickFormatter"/> (decimals from the tick step).
/// </summary>
public static class NumberFormat
{
    /// <summary>
    /// A continuous reading (decoded intensity, ratio, prominence) to <paramref name="digits"/> significant digits,
    /// thousands grouped, trailing fractional zeros dropped: 18.5323 → "18.53", 3077 → "3,077", 0.2750 → "0.275".
    /// </summary>
    public static string Significant(double value, int digits = 4)
    {
        if (!double.IsFinite(value)) return value.ToString(CultureInfo.InvariantCulture);
        if (value == 0) return "0";
        int magnitude = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        int decimals = Math.Clamp(digits - 1 - magnitude, 0, 9);
        double rounded = Math.Round(value, decimals);
        if (rounded == 0) return "0";   // never "-0"
        string text = rounded.ToString("N" + decimals, CultureInfo.InvariantCulture);
        return decimals > 0 ? text.TrimEnd('0').TrimEnd('.') : text;
    }

    /// <summary>
    /// A value at a fixed resolution with many decimals, the fraction grouped in threes by a narrow no-break space so
    /// it can be read: 0.853889538 at 9 decimals → "0.853 889 538" (ns resolution kept).
    /// </summary>
    public static string GroupedFraction(double value, int decimals)
    {
        string text = value.ToString("N" + decimals, CultureInfo.InvariantCulture);
        int point = text.IndexOf('.');
        if (point < 0) return text;
        var fraction = text[(point + 1)..];
        var groups = Enumerable.Range(0, (fraction.Length + 2) / 3)
            .Select(i => fraction.Substring(i * 3, Math.Min(3, fraction.Length - i * 3)));
        return text[..(point + 1)] + string.Join(' ', groups);
    }
}
