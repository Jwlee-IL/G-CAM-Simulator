using System.Globalization;
using System.Text.RegularExpressions;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>MD3: recompute from visible population rows only, without a Studio evidence hook or service calls.</summary>
public static class ComparePopulationOracle
{
    public sealed record Population(long Histories, long Held, long Assigned, long Unknown, long Same, long Eligible)
    {
        public long Different => Eligible - Same;
        public double? Share => Eligible == 0 ? null : (double)Same / Eligible;
    }
    public static Population Read(IReadOnlyList<string> text)
    {
        (long A, long B) Pair(string label)
        {
            int i = text.ToList().IndexOf(label);
            if (i < 0 || i + 1 >= text.Count) throw new InvalidDataException("Missing displayed population: " + label);
            var match = Regex.Match(text[i + 1], @"^([\d,]+) / ([\d,]+)$");
            if (!match.Success) throw new InvalidDataException("Malformed displayed population: " + label);
            long Parse(string s) => long.Parse(s.Replace(",", ""), CultureInfo.InvariantCulture);
            return (Parse(match.Groups[1].Value), Parse(match.Groups[2].Value));
        }
        var history = Pair("Histories / held"); var assigned = Pair("Assigned / unknown"); var same = Pair("Same / eligible");
        return new(history.A, history.B, assigned.A, assigned.B, same.A, same.B);
    }
    public static bool Consistent(Population p, string displayedShare)
    {
        if (new[] { p.Histories, p.Held, p.Assigned, p.Unknown, p.Same, p.Eligible }.Any(n => n < 0) ||
            p.Assigned > p.Held || p.Unknown != p.Held - p.Assigned || p.Same > p.Eligible || p.Eligible > p.Assigned || p.Different < 0)
            return false;
        if (p.Share is null) return displayedShare == "Same crystal unavailable: no eligible conversions";
        var match = Regex.Match(displayedShare, @"^Same crystal (.+?)\s*[%％]$");
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.CurrentCulture, out double percent)
            && Math.Abs(percent - p.Share.Value * 100) <= 0.0050000001; // UI P2: half of the last displayed percentage digit
    }
}
