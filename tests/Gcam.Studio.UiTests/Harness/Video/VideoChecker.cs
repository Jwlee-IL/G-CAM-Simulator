using System.Security.Cryptography;

namespace Gcam.Studio.UiTests.Harness.Video;

/// <summary>Frozen L2/R6 criteria. Historical files may be measured but missing independent inputs cannot qualify.</summary>
/// <remarks>Window transitions (TODO-54 turn G): a single-colour sample inside a declared transition interval is excused
/// from the single-colour rules and counted in <see cref="VideoCheckResult.TransitionShare"/> instead. The intervals come from
/// the harness's own events (the recorder marks samples while a transition it was told about is open), never from frame
/// content; each must be well formed, name its beginning step and last at most <see cref="MaximumTransition100ns"/>.
/// Evidence without the field excuses nothing, so an old record keeps its verdict.</remarks>
public static class VideoChecker
{
    public const long IntervalTolerance100ns = 2_000_000, MaximumGap100ns = 3_000_000;
    /// <summary>The longest declared window transition (launch, state change or close), 0.5 s.</summary>
    public const long MaximumTransition100ns = 5_000_000;
    public static VideoCheckResult Check(string mp4, string vtt, VideoEvidence? evidence, VideoEntry? expected = null)
    {
        string hash = "", captionsHash = ""; var failures = new HashSet<string>();
        // Hold read-only leases through decoding: the digest names the bytes actually checked.
        using var videoFile = TryOpen(mp4); using var captionFile = TryOpen(vtt);
        try { hash = Hash(videoFile!); } catch { failures.Add("video-file"); }
        string captions = "";
        try
        {
            captionsHash = Hash(captionFile!); captionFile!.Position = 0;
            using var reader = new StreamReader(captionFile, leaveOpen: true); captions = reader.ReadToEnd().Replace("\r\n", "\n");
        }
        catch { failures.Add("captions-file"); }
        var decoded = MediaFoundationDecoder.Decode(mp4);
        if (!decoded.EndOfStream || decoded.Error is not null || decoded.Samples.Length == 0) failures.Add("decode-to-end");
        if (expected is not null)
        {
            if (hash != expected.Video.Sha256 || captionsHash != expected.Captions.Sha256) failures.Add("file-identity");
            if (decoded.Samples.Length != expected.Frames || decoded.Samples.Any(s => s.WidthPx != expected.WidthPx || s.HeightPx != expected.HeightPx))
                failures.Add("sample-metadata");
        }
        double? intervalError = null;
        if (evidence is null || evidence.Interval.Frequency <= 0 || evidence.Interval.Duration100ns <= 0) failures.Add("recording-interval-missing");
        else
        {
            intervalError = Math.Abs(decoded.End100ns - evidence.Interval.Duration100ns) / (double)TimeSpan.TicksPerSecond;
            if (decoded.Samples.Length == 0 || Math.Abs(decoded.Samples[0].Time100ns) > IntervalTolerance100ns || intervalError > 0.2)
                failures.Add("recording-interval");
        }
        long gap = 0, longest = 0, runStart = -1, runEnd = 0; int single = 0, privacy = 0, transition = 0;
        var declared = evidence?.PrivacyIntervals ?? [];
        var transitions = evidence?.TransitionIntervals ?? [];
        var masks = evidence?.MaskSamples ?? [];
        if (evidence is null) failures.Add("privacy-evidence-missing");
        bool Privacy(DecodedVideo.Sample sample) => declared.Count(p => p.Start100ns <= sample.Time100ns
            && p.End100ns >= sample.Time100ns + sample.Duration100ns) == 1;
        bool Transition(DecodedVideo.Sample sample) => transitions.Count(t => t.Start100ns <= sample.Time100ns
            && t.End100ns >= sample.Time100ns + sample.Duration100ns) == 1;
        for (int i = 0; i < decoded.Samples.Length; i++)
        {
            var sample = decoded.Samples[i];
            if (sample.Time100ns < 0 || sample.Duration100ns <= 0 || i > 0 && sample.Time100ns <= decoded.Samples[i - 1].Time100ns)
                failures.Add("sample-timestamps");
            if (i > 0) gap = Math.Max(gap, sample.Time100ns - decoded.Samples[i - 1].Time100ns);
            bool hidden = Privacy(sample), changing = !hidden && Transition(sample);
            if (hidden) privacy++;
            if (changing) transition++;
            if (sample.SingleColour && !hidden && !changing)
            {
                // On-screen time: a frame stays shown until the next sample starts (the last one until the media end),
                // so dropped slots inside a single-colour stretch cannot split it into short runs.
                single++; if (runStart < 0) runStart = sample.Time100ns;
                runEnd = ShownUntil(decoded, i); longest = Math.Max(longest, runEnd - runStart);
            }
            else runStart = -1;
            var mask = masks.FirstOrDefault(m => m.Frame == i);
            bool masked = mask?.Privacy == true;
            if (hidden != masked || masked && (mask!.Start100ns != sample.Time100ns || mask.End100ns != sample.Time100ns + sample.Duration100ns
                || !mask.RectsPx.Any(r => r.SequenceEqual(new[] { 0, 0, sample.WidthPx, sample.HeightPx })) || !sample.Black))
                failures.Add("privacy-mask-match");
        }
        if (gap > MaximumGap100ns) failures.Add("timestamp-gap");
        double singleShare = decoded.Samples.Length == 0 ? 0 : (double)single / decoded.Samples.Length;
        if (longest > IntervalTolerance100ns) failures.Add("single-colour-continuous");
        if (singleShare > 0.01) failures.Add("single-colour-share");
        foreach (var p in declared)
        {
            var covered = decoded.Samples.Where(s => s.Time100ns >= p.Start100ns && s.Time100ns < p.End100ns).ToArray();
            if (p.Start100ns < 0 || p.End100ns <= p.Start100ns || string.IsNullOrWhiteSpace(p.Reason) || p.StepIds.Length == 0
                || covered.Length == 0 || covered[0].Time100ns != p.Start100ns || covered[^1].Time100ns + covered[^1].Duration100ns != p.End100ns
                || declared.Any(other => !ReferenceEquals(other, p) && other.Start100ns < p.End100ns && other.End100ns > p.Start100ns))
                failures.Add("privacy-declaration");
            // Suppression must be bounded by visible material, rather than hiding an entire clip.
            bool VisibleCue(DecodedVideo.Sample sample) => !Privacy(sample) && (evidence?.Cues ?? []).Any(c =>
                c.Start100ns < sample.Time100ns + sample.Duration100ns && c.End100ns > sample.Time100ns && CaptionsContain(captions, c));
            if (!decoded.Samples.Any(s => s.Time100ns + s.Duration100ns <= p.Start100ns && VisibleCue(s))
                || !decoded.Samples.Any(s => s.Time100ns >= p.End100ns && VisibleCue(s))) failures.Add("privacy-visible-boundaries");
        }
        foreach (var t in transitions)
        {
            var covered = decoded.Samples.Where(s => s.Time100ns >= t.Start100ns && s.Time100ns < t.End100ns).ToArray();
            if (t.Start100ns < 0 || t.End100ns <= t.Start100ns || string.IsNullOrWhiteSpace(t.Reason) || t.StepIds.Length == 0
                || covered.Length == 0 || covered[0].Time100ns != t.Start100ns || covered[^1].Time100ns + covered[^1].Duration100ns != t.End100ns
                || transitions.Any(other => !ReferenceEquals(other, t) && other.Start100ns < t.End100ns && other.End100ns > t.Start100ns)
                || declared.Any(p => p.Start100ns < t.End100ns && p.End100ns > t.Start100ns))
                failures.Add("transition-declaration");
            if (t.End100ns - t.Start100ns > MaximumTransition100ns) failures.Add("transition-length");
        }
        if (masks.Any(m => m.Frame < 0 || m.Frame >= decoded.Samples.Length) || masks.Select(m => m.Frame).Distinct().Count() != masks.Length)
            failures.Add("privacy-mask-match");
        var unmapped = new List<string>();
        if (evidence is null || evidence.Steps.Length == 0) failures.Add("step-evidence-missing");
        else
        {
            var ids = evidence.Steps.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
            if (ids.Contains("") || ids.Count != evidence.Steps.Length) failures.Add("step-identities");
            foreach (var cue in evidence.Cues)
                if (cue.StepIds.Length == 0 || cue.StepIds.Any(id => !ids.Contains(id)) || cue.Start100ns < 0 || cue.End100ns <= cue.Start100ns
                    || cue.End100ns > decoded.End100ns || !decoded.Samples.Any(s => s.Time100ns < cue.End100ns && s.Time100ns + s.Duration100ns > cue.Start100ns)
                    || !CaptionsContain(captions, cue)) failures.Add("timed-cues");
            foreach (var p in declared)
                if (p.StepIds.Distinct(StringComparer.Ordinal).Count() != p.StepIds.Length
                    || p.StepIds.Any(id => !evidence.Steps.Any(s => s.Id == id && s.Privacy))
                    || evidence.Steps.Any(s => s.Privacy && s.Time100ns >= p.Start100ns - IntervalTolerance100ns
                        && s.Time100ns <= p.End100ns + IntervalTolerance100ns && !p.StepIds.Contains(s.Id)))
                    failures.Add("privacy-step-identities");
            // A transition names the visible step that began it, recorded when it began (within the interval's tolerance).
            foreach (var t in transitions)
                if (t.StepIds.Distinct(StringComparer.Ordinal).Count() != t.StepIds.Length
                    || t.StepIds.Any(id => !evidence.Steps.Any(s => s.Id == id && !s.Privacy
                        && s.Time100ns >= t.Start100ns - IntervalTolerance100ns && s.Time100ns <= t.End100ns + IntervalTolerance100ns)))
                    failures.Add("transition-step-identities");
            foreach (var step in evidence.Steps)
            {
                bool cue = evidence.Cues.Any(c => c.StepIds.Contains(step.Id) && c.Start100ns >= 0 && c.End100ns > c.Start100ns
                    && c.End100ns <= decoded.End100ns && Math.Abs(c.Start100ns - step.Time100ns) <= IntervalTolerance100ns && CaptionsContain(captions, c));
                bool hidden = step.Privacy && declared.Any(p => p.StepIds.Contains(step.Id) && step.Time100ns >= p.Start100ns - IntervalTolerance100ns
                    && step.Time100ns <= p.End100ns + IntervalTolerance100ns);
                if (!cue && !hidden) unmapped.Add(step.Id);
            }
            if (unmapped.Count > 0) failures.Add("step-coverage");
        }
        return new("v4", hash, captionsHash, failures.Count == 0, failures.Order().ToArray(), decoded.EndOfStream && decoded.Error is null && decoded.Samples.Length > 0,
            decoded.Samples.Length, decoded.End100ns, intervalError, longest / (double)TimeSpan.TicksPerSecond, singleShare,
            gap / (double)TimeSpan.TicksPerSecond, decoded.Samples.Length == 0 ? 0 : (double)privacy / decoded.Samples.Length, unmapped.ToArray(), decoded.Error)
        {
            TransitionShare = evidence?.TransitionIntervals is null ? null : decoded.Samples.Length == 0 ? 0 : (double)transition / decoded.Samples.Length,
        };
    }
    internal static long ShownUntil(DecodedVideo decoded, int i) => i + 1 < decoded.Samples.Length
        ? Math.Max(decoded.Samples[i + 1].Time100ns, decoded.Samples[i].Time100ns) : decoded.End100ns;
    private static bool CaptionsContain(string captions, VideoTimeline.Cue cue) => captions.Contains(VideoTimeline.Captions([cue])["WEBVTT\n\n".Length..], StringComparison.Ordinal);
    private static string Hash(Stream file) => Convert.ToHexStringLower(SHA256.HashData(file));
    private static FileStream? TryOpen(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            return path.StartsWith(@"\\", StringComparison.Ordinal) ? null : File.OpenRead(path);
        }
        catch { return null; }
    }
}
