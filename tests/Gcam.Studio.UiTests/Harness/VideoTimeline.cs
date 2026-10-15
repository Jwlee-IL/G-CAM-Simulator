using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Monotonic media slots and text-only captions; no desktop access.</summary>
public static class VideoTimeline
{
    public const long Period100ns = 1_000_000;
    public sealed record Step(long Time100ns, string Text);
    public static long Slot(long time100ns) => Math.Max(0, time100ns / Period100ns) * Period100ns;
    public static string SafeText(string text)
    {
        text = Regex.Replace(text, @"(?:[A-Za-z]:[\\/]|\\\\)\S+", "<path>");
        foreach (string identity in new[] { Environment.UserName, Environment.MachineName }.Where(s => s.Length >= 3))
            text = text.Replace(identity, "<identity>", StringComparison.OrdinalIgnoreCase);
        return Regex.Replace(text.Replace("-->", "→"), @"[\p{Cc}\p{Cf}]", " ").Trim();
    }
    public static string Captions(IEnumerable<Step> steps, long duration100ns)
    {
        var rows = steps.Select(s => new Step(Math.Clamp(s.Time100ns / 10_000, 0, duration100ns / 10_000), SafeText(s.Text)))
            .GroupBy(s => s.Time100ns).Select(g => new Step(g.Key, string.Join(" · ", g.Select(s => s.Text))))
            .OrderBy(s => s.Time100ns).ToArray();
        var result = new StringBuilder("WEBVTT\n\n");
        for (int i = 0; i < rows.Length; i++)
        {
            long end = i + 1 < rows.Length ? rows[i + 1].Time100ns : duration100ns / 10_000;
            if (end <= rows[i].Time100ns) continue;
            result.Append(i + 1).Append('\n').Append(Stamp(rows[i].Time100ns)).Append(" --> ").Append(Stamp(end)).Append('\n')
                .Append(rows[i].Text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")).Append("\n\n");
        }
        return result.ToString();
    }
    private static string Stamp(long ms) => string.Create(CultureInfo.InvariantCulture,
        $"{ms / 3_600_000:00}:{ms / 60_000 % 60:00}:{ms / 1000 % 60:00}.{ms % 1000:000}");
}
