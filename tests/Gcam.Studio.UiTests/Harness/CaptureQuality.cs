using System.Text.Json;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Pure capture preflight; fixture checks never read the desktop.</summary>
public static class CaptureQuality
{
    public sealed record Template(string Screen, string State, int WidthPx, int HeightPx, double Dpi,
        string Theme, string Locale, string[] Targets, string Framing, string QualityRevision)
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public string Sha256 => IsolationPaths.Hash(JsonSerializer.SerializeToUtf8Bytes(this));
    }
    public sealed record Frame(int WidthPx, int HeightPx, bool Owned, bool Foreground, bool Occluded,
        bool Stable, bool PrivacySafe, byte[] Rgb);
    public static string[] Check(Template template, Frame frame)
    {
        var reasons = new List<string>();
        if (!frame.Owned) reasons.Add("not-owned");
        if (!frame.Foreground) reasons.Add("not-foreground");
        if (frame.Occluded) reasons.Add("occluded");
        if (!frame.Stable) reasons.Add("unstable");
        if (!frame.PrivacySafe) reasons.Add("privacy-unknown");
        if (frame.WidthPx != template.WidthPx || frame.HeightPx != template.HeightPx) reasons.Add("wrong-size");
        if (frame.WidthPx <= 0 || frame.HeightPx <= 0 || frame.Rgb.Length != (long)frame.WidthPx * frame.HeightPx * 3) reasons.Add("invalid-pixels");
        // Exact flat colour is certainly blank; softer thresholds require the authorised capture pilot.
        else if (Enumerable.Range(0, frame.Rgb.Length / 3).All(i => frame.Rgb[i * 3] == frame.Rgb[0]
            && frame.Rgb[i * 3 + 1] == frame.Rgb[1] && frame.Rgb[i * 3 + 2] == frame.Rgb[2])) reasons.Add("blank");
        return reasons.ToArray();
    }
}
