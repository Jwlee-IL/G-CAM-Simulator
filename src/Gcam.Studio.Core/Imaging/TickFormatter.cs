using System.Globalization;

namespace Gcam.Studio.Core.Imaging;

/// <summary>
/// Labels for a colour scale: one shared power of ten (×10⁻³) when values are very small or large, and the same
/// number of decimals on every tick, so a column of labels reads as one scale rather than "0.00157 … 0.0161".
/// </summary>
public static class TickFormatter
{
    /// <summary>Tick values evenly spaced from <paramref name="min"/> to <paramref name="max"/>, as display strings;
    /// the shared multiplier (if any) is appended to the last label.</summary>
    public static IReadOnlyList<string> Labels(double min, double max, int count)
    {
        count = Math.Max(2, count);
        double maxAbs = Math.Max(Math.Abs(min), Math.Abs(max));
        int exponent = SharedExponent(maxAbs);
        double scale = Math.Pow(10, -exponent);
        double range = (max - min) * scale;
        int decimals = range > 0 ? Math.Clamp(2 - (int)Math.Floor(Math.Log10(range)), 0, 3) : 0;

        var labels = new string[count];
        for (int i = 0; i < count; i++)
        {
            double v = (min + (max - min) * i / (count - 1)) * scale;
            if (Math.Round(v, decimals) == 0) v = 0;   // never "-0.0"
            labels[i] = v.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }
        if (exponent != 0) labels[^1] += " ×10" + Superscript(exponent);
        return labels;
    }

    /// <summary>0 for values that read well as they are (0.1 … 9999); otherwise a multiple of 3 (engineering style).</summary>
    public static int SharedExponent(double maxAbs)
    {
        if (maxAbs == 0 || double.IsNaN(maxAbs) || double.IsInfinity(maxAbs)) return 0;
        int k = (int)Math.Floor(Math.Log10(maxAbs));
        return k is >= -1 and <= 3 ? 0 : 3 * (int)Math.Floor(k / 3.0);
    }

    private static string Superscript(int n) => string.Concat(n.ToString(CultureInfo.InvariantCulture).Select(c => c switch
    {
        '-' => '⁻', '0' => '⁰', '1' => '¹', '2' => '²', '3' => '³', '4' => '⁴',
        '5' => '⁵', '6' => '⁶', '7' => '⁷', '8' => '⁸', _ => '⁹',
    }));
}
