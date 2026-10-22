using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Monotonic media slots and text-only captions; no desktop access.</summary>
public static class VideoTimeline
{
    public const long Period100ns = 1_000_000;
    public sealed record Step(long Time100ns, string Text, string Id = "", bool Privacy = false);
    public sealed record Cue(string Id, long Start100ns, long End100ns, string[] StepIds, string Text);
    public static long Slot(long time100ns) => Math.Max(0, time100ns / Period100ns) * Period100ns;
    public static string SafeText(string text)
    {
        text = Regex.Replace(text, @"(?:[A-Za-z]:[\\/]|\\\\)\S+", "<path>");
        foreach (string identity in new[] { Environment.UserName, Environment.MachineName }.Where(s => s.Length >= 3))
            text = text.Replace(identity, "<identity>", StringComparison.OrdinalIgnoreCase);
        return Regex.Replace(text.Replace("-->", "→"), @"[\p{Cc}\p{Cf}]", " ").Trim();
    }
    public static Cue[] Cues(IEnumerable<Step> steps, long duration100ns)
    {
        if (duration100ns < 10_000) return [];
        // Reserve one millisecond at the end. Terminal steps coalesce into a positive cue instead of disappearing.
        var rows = steps.Select((s, i) => s with { Id = s.Id.Length == 0 ? $"step-{i + 1:0000}" : s.Id,
                Time100ns = Math.Clamp(s.Time100ns / 10_000, 0, duration100ns / 10_000 - 1) * 10_000, Text = SafeText(s.Text) })
            .GroupBy(s => s.Time100ns).OrderBy(g => g.Key)
            .Select(g => (Time: g.Key, Ids: g.Select(s => s.Id).ToArray(), Text: string.Join(" · ", g.Select(s => s.Text))))
            .ToArray();
        return rows.Select((r, i) => new Cue($"cue-{i + 1:0000}", r.Time,
            i + 1 < rows.Length ? rows[i + 1].Time : duration100ns / 10_000 * 10_000, r.Ids, r.Text)).ToArray();
    }
    public static string Captions(IEnumerable<Step> steps, long duration100ns) => Captions(Cues(steps, duration100ns));
    public static string Captions(IEnumerable<Cue> cues)
    {
        var result = new StringBuilder("WEBVTT\n\n");
        foreach (var cue in cues)
        {
            result.Append(cue.Id).Append('|').AppendJoin(',', cue.StepIds).Append('\n')
                .Append(Stamp(cue.Start100ns / 10_000)).Append(" --> ").Append(Stamp(cue.End100ns / 10_000)).Append('\n')
                .Append(cue.Text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")).Append("\n\n");
        }
        return result.ToString();
    }
    private static string Stamp(long ms) => string.Create(CultureInfo.InvariantCulture,
        $"{ms / 3_600_000:00}:{ms / 60_000 % 60:00}:{ms / 1000 % 60:00}.{ms % 1000:000}");
}
