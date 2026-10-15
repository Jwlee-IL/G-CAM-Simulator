using System.Text.Json;
using System.Text.RegularExpressions;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>The latest passing video's portable identity. All paths are basenames, never local identities.</summary>
public sealed record VideoEntry(string RunId, DateTimeOffset PassedAtUtc, string Commit, bool Dirty,
    string AppBinarySha256, string DeploymentSha256, VideoEntry.FileRecord Video, VideoEntry.FileRecord Captions,
    long Duration100ns, int WidthPx, int HeightPx, int Frames, int CapturedFrames, int DroppedSlots,
    int MaskedFrames, int PrivacySuppressedFrames, VideoEntry.MaskRange[] MaskRanges, int NominalFps = 10)
{
    public sealed record FileRecord(string Path, string Sha256, long Bytes);
    public sealed record MaskRange(int FirstFrame, int LastFrame, string[] Reasons, int[][] RectsPx)
    {
        public bool SameMask(MaskRange other) => Reasons.SequenceEqual(other.Reasons) &&
            RectsPx.Length == other.RectsPx.Length && RectsPx.Zip(other.RectsPx).All(p => p.First.SequenceEqual(p.Second));
    }
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static string CaseId(string declaringType, string method, params string[] arguments)
    {
        string raw = string.Join(".", new[] { declaringType, method }.Concat(arguments));
        string safe = Regex.Replace(raw, "[^A-Za-z0-9._-]", "_");
        return safe[..Math.Min(safe.Length, 140)] + "-" + IsolationPaths.Hash(System.Text.Encoding.UTF8.GetBytes(raw))[..12];
    }
    public void Validate()
    {
        static bool Hash(string value) => Regex.IsMatch(value, "^[a-f0-9]{64}$");
        foreach (var file in new[] { Video, Captions })
            if (file.Path != System.IO.Path.GetFileName(file.Path) || file.Path.Contains('\\') || file.Path.Contains('/') ||
                file.Path is "" or "." or ".." || !Hash(file.Sha256) || file.Bytes <= 0) throw new InvalidDataException("Invalid video file identity");
        if (!Hash(AppBinarySha256) || !Hash(DeploymentSha256) || !Regex.IsMatch(Commit, "^[a-f0-9]{40,64}$") ||
            string.IsNullOrWhiteSpace(RunId) || Duration100ns <= 0 || Frames <= 0 || WidthPx <= 0 || HeightPx <= 0 ||
            WidthPx % 2 != 0 || HeightPx % 2 != 0 || NominalFps != 10 || CapturedFrames < Frames || DroppedSlots < 0 ||
            MaskedFrames < 0 || MaskedFrames > Frames || PrivacySuppressedFrames < 0 || PrivacySuppressedFrames > Frames)
            throw new InvalidDataException("Invalid video metadata");
        foreach (var mask in MaskRanges)
        {
            if (mask.FirstFrame < 0 || mask.LastFrame < mask.FirstFrame || mask.LastFrame >= Frames || mask.Reasons.Length == 0)
                throw new InvalidDataException("Invalid video mask range");
            foreach (var r in mask.RectsPx)
                if (!CaptureQuality.ValidMask(r, WidthPx, HeightPx))
                    throw new InvalidDataException("Invalid video mask rectangle");
        }
    }
}
