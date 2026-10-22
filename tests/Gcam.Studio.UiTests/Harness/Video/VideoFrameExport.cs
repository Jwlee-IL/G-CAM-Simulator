using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness.Video;

/// <summary>Offline still export from the exact selected v4 attempts. No screen capture or application launch.</summary>
public static class VideoFrameExport
{
    public const int MaximumWidth = 960, JpegQuality = 70, MaximumJpegBytes = 160 * 1024;
    public const string SelectionRule = "First visible step, up to three visible key steps nearest equally spaced media-time targets, terminal visible step; last non-private non-neutral decoded sample overlapping each cue. Sparse timelines retain real step IDs and use visible start/midpoint/end samples of their existing cues.";
    public sealed record Frame(string StepId, string CueText, long MediaTime100ns, long MediaEnd100ns,
        string File, string Sha256, int WidthPx, int HeightPx);
    public sealed record Case(string CaseId, string? RunId, string Verdict, string? VideoSha256, string? CheckContract,
        string VideoStatus, string? Reason, int CanvasWidthPx, int CanvasHeightPx, Frame[] Frames);

    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidDataException(reason); }

    public static void SafeText(string text)
    {
        Require(VideoTimeline.SafeText(text) == text && !Regex.IsMatch(text, @"(?i)(?:file:/|/(?:Users|home)/|~[\\/])"),
            "frames: private path or identity in text");
    }

    /// <summary>The same checked hash, verdict and complete executed-step inventory required by the Python L3 gate.</summary>
    public static void RequireVideoStatus(JsonElement record)
    {
        Require(record.TryGetProperty("contract", out var contract) && contract.GetString() == "v4", "video-gate: missing v4 run record");
        Require(record.TryGetProperty("videoStatus", out var status) && status.ValueKind == JsonValueKind.Object,
            "video-gate: missing v4 status");
        Require(status.GetProperty("contract").GetString() == "v4" && status.GetProperty("runId").GetString() == record.GetProperty("runId").GetString(),
            "video-gate: different attempt or contract");
        var check = status.GetProperty("check"); string? hash = status.GetProperty("videoSha256").GetString();
        Require(check.GetProperty("contract").GetString() == "v4" && hash is not null && Regex.IsMatch(hash, "^[0-9a-f]{64}$")
            && check.GetProperty("videoSha256").GetString() == hash, "video-gate: checked hash differs");
        Require(status.GetProperty("status").GetString() == "passed" && check.GetProperty("passed").GetBoolean()
            && check.GetProperty("failedCriteria").GetArrayLength() == 0, "video-gate: video check did not pass");
        Require(record.TryGetProperty("executedStepIds", out var executed) && executed.ValueKind == JsonValueKind.Array
            && executed.GetArrayLength() > 0, "video-gate: missing executedStepIds");
        var ids = status.GetProperty("evidence").GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("id").GetString()).ToHashSet();
        Require(executed.EnumerateArray().All(s => s.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(s.GetString()) && ids.Contains(s.GetString())), "video-gate: executed steps missing from recording");
        if (record.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
            foreach (var step in steps.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.Object))
                Require(step.TryGetProperty("videoStepId", out var videoStep) && ids.Contains(videoStep.GetString())
                    && executed.EnumerateArray().Any(s => s.GetString() == videoStep.GetString()), "video-gate: manual step missing from execution or recording");
        if (record.TryGetProperty("video", out var provenance) && provenance.ValueKind != JsonValueKind.String)
            Require(provenance.ValueKind == JsonValueKind.Object && provenance.GetProperty("runId").GetString() == record.GetProperty("runId").GetString()
                && provenance.GetProperty("videoSha256").GetString() == hash, "video-gate: provenance differs");
    }

    public static Case ExportCase(string caseId, string verdict, JsonElement record, string mp4, string vtt,
        int canvasWidth, int canvasHeight, string output, string stem)
    {
        RequireVideoStatus(record); SafeText(caseId);
        string run = record.GetProperty("runId").GetString()!; SafeText(run);
        var status = record.GetProperty("videoStatus"); string hash = status.GetProperty("videoSha256").GetString()!;
        Require(Regex.IsMatch(stem, "^[a-zA-Z0-9_-]+$"), "frames: invalid output stem");
        Require(!Path.GetFullPath(mp4).StartsWith(@"\\", StringComparison.Ordinal) && !Path.GetFullPath(vtt).StartsWith(@"\\", StringComparison.Ordinal), "frames: local media required");
        using var lease = System.IO.File.OpenRead(mp4);
        using var captionsLease = System.IO.File.OpenRead(vtt);
        Require(Hash(lease) == hash, "frames: video hash differs from checked run");
        var evidence = status.GetProperty("evidence").Deserialize<VideoEvidence>(VideoEntry.Json)!;
        var check = VideoChecker.Check(mp4, vtt, evidence);
        Require(check.Passed && check.VideoSha256 == hash && check.CaptionsSha256 == status.GetProperty("check").GetProperty("captionsSha256").GetString(),
            "frames: actual video/captions no longer qualify");
        var decoded = MediaFoundationDecoder.Decode(mp4);
        Require(decoded.EndOfStream && decoded.Error is null && decoded.Samples.Length > 0
            && decoded.Samples.All(s => s.WidthPx == canvasWidth && s.HeightPx == canvasHeight), "frames: recorded canvas differs");
        bool Visible(DecodedVideo.Sample s) => !s.SingleColour
            && !evidence.PrivacyIntervals.Any(p => p.Start100ns < s.Time100ns + s.Duration100ns && p.End100ns > s.Time100ns)
            && !evidence.MaskSamples.Any(m => m.Start100ns == s.Time100ns && m.RectsPx.Any(r => r.SequenceEqual(new[] { 0, 0, canvasWidth, canvasHeight })));
        var visible = evidence.Steps.Where(s => !s.Privacy).Select(step =>
        {
            var cue = evidence.Cues.FirstOrDefault(c => c.StepIds.Contains(step.Id));
            int sample = cue is null ? -1 : Array.FindLastIndex(decoded.Samples, s =>
                s.Time100ns < cue.End100ns && s.Time100ns + s.Duration100ns > cue.Start100ns && Visible(s));
            return (Step: step, Cue: cue, Sample: sample);
        }).Where(s => s.Sample >= 0).OrderBy(s => s.Step.Time100ns).ToArray();
        Require(visible.Length > 0, "frames: no visible recorded cue");
        var chosen = visible;
        if (visible.Select(p => p.Sample).Distinct().Count() >= 3)
        {
            int desired = Math.Min(5, visible.Length);
            var selected = new List<int> { 0, visible.Length - 1 };
            for (int i = 1; i < desired - 1; i++)
            {
                double target = visible[0].Step.Time100ns + (visible[^1].Step.Time100ns - visible[0].Step.Time100ns) * (double)i / (desired - 1);
                int index = Enumerable.Range(1, visible.Length - 2).Where(n => !selected.Contains(n))
                    .OrderBy(n => Math.Abs(visible[n].Step.Time100ns - target)).ThenBy(n => n).First();
                selected.Add(index);
            }
            chosen = selected.Order().Select(i => visible[i]).ToArray();
        }
        else
        {
            var first = visible[0];
            if (first.Sample == visible[^1].Sample)
                first.Sample = Array.FindIndex(decoded.Samples, s => Visible(s) && s.Time100ns < first.Cue!.End100ns
                    && s.Time100ns + s.Duration100ns > first.Cue.Start100ns);
            var sparse = new List<(VideoTimeline.Step Step, VideoTimeline.Cue? Cue, int Sample)> { first, visible[^1] };
            double target = (decoded.Samples[first.Sample].Time100ns + decoded.Samples[visible[^1].Sample].Time100ns) / 2.0;
            var candidates = Enumerable.Range(0, decoded.Samples.Length).Where(i => Visible(decoded.Samples[i])
                && sparse.All(p => p.Sample != i)
                && visible.Any(p => decoded.Samples[i].Time100ns < p.Cue!.End100ns
                    && decoded.Samples[i].Time100ns + decoded.Samples[i].Duration100ns > p.Cue.Start100ns))
                .OrderBy(i => Math.Abs(decoded.Samples[i].Time100ns - target)).ThenBy(i => i).ToArray();
            Require(candidates.Length > 0 && first.Sample != visible[^1].Sample, "frames: fewer-than-three-visible-frames");
            int middle = candidates[0]; var owner = visible.First(p => decoded.Samples[middle].Time100ns < p.Cue!.End100ns
                && decoded.Samples[middle].Time100ns + decoded.Samples[middle].Duration100ns > p.Cue.Start100ns);
            sparse.Add((owner.Step, owner.Cue, middle));
            chosen = sparse.OrderBy(p => p.Sample).ToArray();
        }
        int count = chosen.Length;
        var frames = new List<Frame>(); int n = 0;
        var second = MediaFoundationDecoder.Decode(mp4, (sample, pixels) =>
        {
            foreach (var item in chosen.Where(c => decoded.Samples[c.Sample].Time100ns == sample.Time100ns))
            {
                SafeText(item.Step.Id); SafeText(item.Cue!.Text);
                Require(sample.WidthPx == canvasWidth && sample.HeightPx == canvasHeight
                    && pixels.Length == checked(canvasWidth * canvasHeight * 4), "frames: canvas byte count differs");
                string file = stem + "-" + (++n).ToString("D2") + ".jpg";
                byte[] jpeg = Jpeg(pixels, canvasWidth, canvasHeight,
                    evidence.MaskSamples.FirstOrDefault(m => m.Frame == item.Sample)?.RectsPx ?? []);
                Require(jpeg.Length <= MaximumJpegBytes, "frames: JPEG exceeds annex size budget");
                using (var destination = new FileStream(Path.Combine(output, file), FileMode.CreateNew)) destination.Write(jpeg);
                using var image = Image.FromStream(new MemoryStream(jpeg));
                frames.Add(new(item.Step.Id, item.Cue.Text, sample.Time100ns, sample.Time100ns + sample.Duration100ns,
                    file, IsolationPaths.Hash(jpeg), image.Width, image.Height));
            }
        });
        Require(second.EndOfStream && second.Error is null && frames.Count == count, "frames: export decode incomplete");
        return new(caseId, run, verdict, hash, "v4", "passed", null, canvasWidth, canvasHeight, frames.ToArray());
    }

    internal static byte[] Jpeg(byte[] pixels, int width, int height, int[][] masks)
    {
        using var canvas = new Bitmap(width, height, PixelFormat.Format32bppRgb);
        var bits = canvas.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        try { for (int y = 0; y < height; y++) Marshal.Copy(pixels, y * width * 4, bits.Scan0 + y * bits.Stride, width * 4); }
        finally { canvas.UnlockBits(bits); }
        using (var g = Graphics.FromImage(canvas)) foreach (var rect in masks)
        {
            Require(CaptureQuality.ValidMask(rect, width, height), "frames: invalid recorder mask");
            g.FillRectangle(Brushes.Black, rect[0], rect[1], rect[2], rect[3]);
        }
        int w = Math.Min(MaximumWidth, width), h = Math.Max(1, (int)Math.Round(height * (double)w / width));
        using var small = new Bitmap(w, h);
        using (var g = Graphics.FromImage(small)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(canvas, 0, 0, w, h); }
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)JpegQuality);
        using var output = new MemoryStream();
        small.Save(output, ImageCodecInfo.GetImageEncoders().Single(c => c.FormatID == ImageFormat.Jpeg.Guid), parameters);
        return output.ToArray();
    }

    private static string Hash(Stream file) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(file));
    private static byte[] Physical(string path)
    {
        Require(!(System.IO.File.Exists(path) && System.IO.File.Exists(path + ".gz")), "frames: ambiguous compressed record");
        if (System.IO.File.Exists(path)) return System.IO.File.ReadAllBytes(path);
        using var zip = new GZipStream(System.IO.File.OpenRead(path + ".gz"), CompressionMode.Decompress);
        using var bytes = new MemoryStream(); zip.CopyTo(bytes); return bytes.ToArray();
    }

    // The archive pins expanded Python canonical bytes, not the manifest-pool envelope or gzip bytes.
    internal static byte[] Read(string path, string? archiveRoot = null)
    {
        byte[] raw = Physical(path);
        using var document = JsonDocument.Parse(raw);
        if (!document.RootElement.TryGetProperty("archiveEncoding", out var encoding)) return raw;
        Require(encoding.GetString() == "manifest-pool-v1" && archiveRoot is not null, "frames: unsupported/missing archive pool");
        var pool = JsonNode.Parse(Physical(Path.Combine(archiveRoot!, "manifests.json")))!.AsObject();
        foreach (var pair in pool)
            Require(IsolationPaths.Hash(Canonical(JsonSerializer.SerializeToElement(pair.Value))) == pair.Key, "frames: manifest pool hash differs");
        var value = JsonNode.Parse(document.RootElement.GetProperty("document").GetRawText())!.AsObject();
        foreach (string owner in new[] { "source", "generatorSource", "subject" })
        foreach (string field in new[] { "files", "postFiles" })
            if (value[owner] is JsonObject group && group[field] is JsonObject reference
                && reference.Count == 1 && reference["manifestRef"] is JsonValue id)
            {
                Require(pool.TryGetPropertyValue(id.GetValue<string>(), out var manifest), "frames: archive manifest reference missing");
                group[field] = manifest!.DeepClone();
            }
        return Canonical(JsonSerializer.SerializeToElement(value));
    }

    internal static byte[] Canonical(JsonElement value)
    {
        var text = new StringBuilder();
        void String(string value)
        {
            text.Append('"');
            foreach (char c in value)
                text.Append(c switch
                {
                    '"' => "\\\"", '\\' => "\\\\", '\b' => "\\b", '\f' => "\\f", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                    _ when c < 32 => "\\u" + ((int)c).ToString("x4"), _ => c.ToString(),
                });
            text.Append('"');
        }
        int Compare(string left, string right)
        {
            var a = left.EnumerateRunes().GetEnumerator(); var b = right.EnumerateRunes().GetEnumerator();
            while (true)
            {
                bool moreA = a.MoveNext(), moreB = b.MoveNext();
                if (!moreA || !moreB) return moreA.CompareTo(moreB);
                int result = a.Current.Value.CompareTo(b.Current.Value); if (result != 0) return result;
            }
        }
        void Write(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                text.Append('{'); bool first = true;
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, Comparer<string>.Create(Compare)))
                {
                    if (!first) text.Append(','); first = false;
                    String(property.Name); text.Append(':'); Write(property.Value);
                }
                text.Append('}');
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                text.Append('['); bool first = true;
                foreach (var item in element.EnumerateArray()) { if (!first) text.Append(','); first = false; Write(item); }
                text.Append(']');
            }
            else if (element.ValueKind == JsonValueKind.String) String(element.GetString()!);
            else text.Append(element.GetRawText());
        }
        Write(value); text.Append('\n');
        return new UTF8Encoding(false, true).GetBytes(text.ToString());
    }

    /// <summary>Fresh destination only; the manifest is the commit marker and is always written after every JPEG.</summary>
    public static void Export(string bundle, string runRoot, string videoRoot, string output)
    {
        Require(!Directory.Exists(output), "frames: output already exists");
        Directory.CreateDirectory(output);
        using var selection = JsonDocument.Parse(Read(Path.Combine(bundle, "selection.json")));
        var records = new Dictionary<string, JsonElement>();
        foreach (var entry in selection.RootElement.GetProperty("records").EnumerateArray())
        {
            byte[] bytes = Read(IsolationPaths.Inside(bundle, entry.GetProperty("path").GetString()!), bundle);
            Require(IsolationPaths.Hash(bytes) == entry.GetProperty("sha256").GetString(), "frames: bundle record hash differs");
            using var document = JsonDocument.Parse(bytes);
            var record = document.RootElement.Clone(); records.Add(record.GetProperty("runId").GetString()!, record);
        }
        var rows = new List<Case>(); int number = 0;
        foreach (var selected in selection.RootElement.GetProperty("caseSelections").EnumerateObject())
        {
            var record = records[selected.Value.GetString()!];
            if (record.GetProperty("profile").GetString() != "desktop") continue;
            record.TryGetProperty("desktopEvidence", out var desktop);
            bool hasEvidence = desktop.ValueKind == JsonValueKind.Object && desktop.TryGetProperty(selected.Name, out _);
            bool listed = record.TryGetProperty("desktopAllCaseIds", out var inventory)
                ? inventory.EnumerateArray().Any(i => i.GetString() == selected.Name) : hasEvidence;
            if (!listed) continue;
            var evidence = hasEvidence ? desktop.GetProperty(selected.Name) : JsonSerializer.SerializeToElement(new { });
            string verdict = record.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == selected.Name).GetProperty("verdict").GetString()!;
            string? run = evidence.TryGetProperty("runId", out var id) ? id.GetString() : null;
            try { RequireVideoStatus(evidence); }
            catch (Exception e) when (e is InvalidDataException or KeyNotFoundException or InvalidOperationException)
            {
                string? digest = evidence.TryGetProperty("videoStatus", out var bad) && bad.ValueKind == JsonValueKind.Object && bad.TryGetProperty("videoSha256", out var d) ? d.GetString() : null;
                rows.Add(new(selected.Name, run, verdict, digest, null, "unavailable", e is InvalidDataException ? e.Message : "video-gate: missing fields", 0, 0, []));
                continue;
            }
            IsolationPaths.ValidateRunId(run!);
            string[] candidates = [Path.Combine(runRoot, "gcam-uia-record-" + run!, "manifest.json"), Path.Combine(runRoot, run!, "manifest.json"), Path.Combine(runRoot, "manifest.json")];
            var matching = candidates.Where(System.IO.File.Exists).Select(p => (Path: p, Bytes: System.IO.File.ReadAllBytes(p)))
                .Where(p => IsolationPaths.Hash(p.Bytes) == evidence.GetProperty("runRecordSha256").GetString()).ToArray();
            Require(matching.Length == 1, "frames: exact run record missing or ambiguous");
            string local = matching[0].Path;
            using var actual = JsonDocument.Parse(matching[0].Bytes);
            Require(actual.RootElement.GetProperty("runId").GetString() == run, "frames: local run differs");
            var status = evidence.GetProperty("videoStatus"); string hash = status.GetProperty("videoSha256").GetString()!;
            Require(actual.RootElement.GetProperty("videoStatus").GetProperty("videoSha256").GetString() == hash, "frames: selected/run video hash differs");
            var index = VideoPublisher.Read(Path.Combine(videoRoot, "index.json"));
            var published = index.Tests.Values.SingleOrDefault(e => e.RunId == run && e.Video.Sha256 == hash);
            string attempt = Path.Combine(Path.GetDirectoryName(local)!, "attempt.mp4");
            string mp4 = System.IO.File.Exists(attempt) && IsolationPaths.Hash(System.IO.File.ReadAllBytes(attempt)) == hash
                ? attempt : published is not null ? IsolationPaths.Inside(videoRoot, published.Video.Path) : throw new InvalidDataException("frames: matching video not found");
            string vtt = mp4 == attempt ? Path.ChangeExtension(attempt, ".vtt") : IsolationPaths.Inside(videoRoot, published!.Captions.Path);
            int width = published?.WidthPx ?? (status.TryGetProperty("canvas", out var canvas) ? canvas.GetProperty("widthPx").GetInt32() : 0);
            int height = published?.HeightPx ?? (status.TryGetProperty("canvas", out var size) ? size.GetProperty("heightPx").GetInt32() : 0);
            rows.Add(ExportCase(selected.Name, verdict, actual.RootElement, mp4, vtt, width, height, output, "case-" + (++number).ToString("D3")));
        }
        foreach (var row in rows) { SafeText(row.CaseId); if (row.RunId is not null) SafeText(row.RunId); }
        var manifest = new { schema = "gcam.str-frames", schemaVersion = 1, contract = "v4", selectionRule = SelectionRule,
            maximumWidthPx = MaximumWidth, jpegQuality = JpegQuality, maximumJpegBytes = MaximumJpegBytes,
            captureScope = "recorded owned-window canvas; recorder masks reapplied; no screen access", cases = rows };
        using var last = new FileStream(Path.Combine(output, "manifest.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(last, manifest, VideoEntry.Json);
    }
}
