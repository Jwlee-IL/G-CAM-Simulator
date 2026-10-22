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
    public const string SelectionRule = "Only scenario steps own a frame: non-private steps listed in the run's executedStepIds (recorder-owned transition steps and privacy steps never do; a cue is illustrated through one of its qualifying step IDs or not at all). First such step, up to three such key steps nearest equally spaced media-time targets, terminal such step; last non-private non-transition non-neutral decoded sample overlapping each cue. Sparse timelines retain real step IDs and use visible start/midpoint/end samples of their existing cues.";
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
        // As the Python gate: step objects without an ID add none, rather than failing as a missing field.
        var ids = status.GetProperty("evidence").GetProperty("steps").EnumerateArray()
            .Where(s => s.ValueKind == JsonValueKind.Object && s.TryGetProperty("id", out _)).Select(s => s.GetProperty("id").GetString()).ToHashSet();
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
        if (status.TryGetProperty("canvas", out var recorded) && recorded.ValueKind != JsonValueKind.Null)
            Require(recorded.GetProperty("widthPx").GetInt32() == canvasWidth && recorded.GetProperty("heightPx").GetInt32() == canvasHeight,
                "frames: recorded canvas differs");
        var decoded = MediaFoundationDecoder.Decode(mp4);
        Require(decoded.EndOfStream && decoded.Error is null && decoded.Samples.Length > 0
            && decoded.Samples.All(s => s.WidthPx == canvasWidth && s.HeightPx == canvasHeight), "frames: recorded canvas differs");
        // The annex illustrates scenario steps only: non-private steps the run executed. Recorder-owned steps (the launch
        // and harness-issued window transitions) are not in executedStepIds, and privacy steps are never shown.
        var executed = record.GetProperty("executedStepIds").EnumerateArray().Select(s => s.GetString()!).ToHashSet(StringComparer.Ordinal);
        // A window-transition interval is treated like privacy: its samples are never illustrated (old evidence has none).
        // A frame's media interval must be non-empty and start at or after zero, as the annex requires.
        bool Visible(DecodedVideo.Sample s) => s.Time100ns >= 0 && s.Duration100ns > 0 && !s.SingleColour
            && !evidence.PrivacyIntervals.Any(p => p.Start100ns < s.Time100ns + s.Duration100ns && p.End100ns > s.Time100ns)
            && !(evidence.TransitionIntervals ?? []).Any(t => t.Start100ns < s.Time100ns + s.Duration100ns && t.End100ns > s.Time100ns)
            && !evidence.MaskSamples.Any(m => m.Start100ns == s.Time100ns && m.RectsPx.Any(r => r.SequenceEqual(new[] { 0, 0, canvasWidth, canvasHeight })));
        // Per step, so a cue that groups a recorder step with a scenario step is reached through the scenario step only,
        // and a cue whose step IDs none qualify is never illustrated.
        var visible = evidence.Steps.Where(s => !s.Privacy && executed.Contains(s.Id)).Select(step =>
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
        var result = new Case(caseId, run, verdict, hash, "v4", "passed", null, canvasWidth, canvasHeight, frames.ToArray());
        RequireAnnexRow(result, record, output);
        return result;
    }

    /// <summary>The STR annex's checks on one qualified row (samples/docs/ui_annex.py), against the record whose evidence the
    /// annex reads, so the exporter never writes a row the annex would refuse. Stricter where the two languages differ.</summary>
    internal static void RequireAnnexRow(Case row, JsonElement record, string output)
    {
        var status = record.GetProperty("videoStatus"); var evidence = status.GetProperty("evidence");
        Require(row.RunId == record.GetProperty("runId").GetString(), "frames: case/run/verdict differs");
        Require(row.VideoSha256 == status.GetProperty("videoSha256").GetString(), "frames: video hash differs from selected run");
        Require(row.Reason is null && row.CheckContract == "v4" && row.VideoStatus == "passed", "frames: unqualified export");
        if (status.TryGetProperty("canvas", out var canvas) && canvas.ValueKind != JsonValueKind.Null)
            Require(row.CanvasWidthPx == canvas.GetProperty("widthPx").GetInt32() && row.CanvasHeightPx == canvas.GetProperty("heightPx").GetInt32(),
                "frames: recorded canvas differs");
        Require(row.Frames.Length is >= 3 and <= 5 && row.Frames.Select(f => f.File).Distinct(StringComparer.Ordinal).Count() == row.Frames.Length,
            "frames: frame count or duplicate file");
        // Any privacy value other than false / null counts as private here (Python's truthiness is the looser of the two).
        var steps = evidence.GetProperty("steps").EnumerateArray().Where(s => !(s.TryGetProperty("privacy", out var p)
            && p.ValueKind is not (JsonValueKind.False or JsonValueKind.Null))).Select(s => s.GetProperty("id").GetString()).ToHashSet(StringComparer.Ordinal);
        var executed = record.GetProperty("executedStepIds").EnumerateArray().Select(s => s.GetString()).ToHashSet(StringComparer.Ordinal);
        var cues = evidence.GetProperty("cues").EnumerateArray().ToArray();
        (long Start, long End)[] Intervals(string name) => evidence.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(i => (i.GetProperty("start100ns").GetInt64(), i.GetProperty("end100ns").GetInt64())).ToArray() : [];
        var privacy = Intervals("privacyIntervals"); var transitions = Intervals("transitionIntervals");
        foreach (var frame in row.Frames)
        {
            Require(steps.Contains(frame.StepId) && executed.Contains(frame.StepId), "frames: unknown or private step");
            var cue = cues.Where(c => c.GetProperty("stepIds").EnumerateArray().Any(i => i.GetString() == frame.StepId)
                && c.GetProperty("text").GetString() == frame.CueText).Select(c => (Found: true, Start: c.GetProperty("start100ns").GetInt64(),
                End: c.GetProperty("end100ns").GetInt64())).FirstOrDefault();
            Require(cue.Found, "frames: cue differs");
            long start = frame.MediaTime100ns, end = frame.MediaEnd100ns;
            Require(0 <= start && start < end && start < cue.End && end > cue.Start, "frames: time outside cue");
            Require(!privacy.Any(p => p.Start < end && p.End > start), "frames: private frame");
            Require(!transitions.Any(t => t.Start < end && t.End > start), "frames: transition frame");
            Require(Regex.IsMatch(frame.File, @"^[A-Za-z0-9_-]+\.jpg$"), "frames: invalid relative path");
            byte[] data = System.IO.File.ReadAllBytes(Path.Combine(output, frame.File));
            Require(IsolationPaths.Hash(data) == frame.Sha256, "frames: JPEG hash differs");
            var (width, height) = JpegSize(data);
            Require((width, height) == (frame.WidthPx, frame.HeightPx) && width <= MaximumWidth
                && width <= row.CanvasWidthPx && height <= row.CanvasHeightPx, "frames: canvas differs");
            AnnexPrivacy(frame.StepId + " · " + frame.CueText + " · "
                + (start / 10_000_000.0).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " s");
        }
    }

    /// <summary>The annex's privacy rule (samples/docs/edge_output.py) on the text it reads: the manifest and each caption.</summary>
    internal static void AnnexPrivacy(string text)
    {
        Require(!Regex.IsMatch(text, @"(?:(?<![\w])[A-Za-z]:[\\/]|file:/|\\\\[^\s\\]+\\|/(?:Users|home)/|~[\\/])", RegexOptions.IgnoreCase),
            "frames: private path in annex text");
        string name = Environment.GetEnvironmentVariable("USERNAME") ?? Environment.GetEnvironmentVariable("USER") ?? "";
        if (name.Length > 0 && !new[] { "user", "runner", "root", "admin", "administrator" }.Contains(name.ToLowerInvariant()))
            Require(!Regex.IsMatch(text, @"(?<![\w])" + Regex.Escape(name) + @"(?![\w])", RegexOptions.IgnoreCase),
                "frames: account name in annex text");
    }

    /// <summary>The annex's SOF canvas reader (ui_annex.jpeg_size), so a frame's dimensions are the ones the annex will read.</summary>
    internal static (int Width, int Height) JpegSize(byte[] data)
    {
        Require(data.Length >= 4 && data[0] == 0xff && data[1] == 0xd8 && data[^2] == 0xff && data[^1] == 0xd9, "frames: invalid JPEG");
        int offset = 2;
        while (offset + 4 <= data.Length)
        {
            Require(data[offset] == 0xff, "frames: invalid JPEG marker");
            int marker = data[offset + 1]; offset += 2;
            int length = data[offset] << 8 | data[offset + 1];
            Require(length >= 2 && offset + length <= data.Length, "frames: invalid JPEG segment");
            if (marker is 0xc0 or 0xc1 or 0xc2)
            {
                Require(length >= 8, "frames: invalid JPEG dimensions");
                int height = data[offset + 3] << 8 | data[offset + 4], width = data[offset + 5] << 8 | data[offset + 6];
                Require(width > 0 && height > 0, "frames: invalid JPEG dimensions");
                return (width, height);
            }
            Require(marker != 0xda, "frames: JPEG lacks canvas");
            offset += length;
        }
        throw new InvalidDataException("frames: JPEG lacks canvas");
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
            // The annex's inventory: desktopAllCaseIds, else desktopCaseIds, else the evidence keys.
            bool listed = record.TryGetProperty("desktopAllCaseIds", out var inventory) || record.TryGetProperty("desktopCaseIds", out inventory)
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
            // The selected run's recorded canvas is the one the annex compares; the published entry is the fallback.
            bool recorded = status.TryGetProperty("canvas", out var canvas) && canvas.ValueKind != JsonValueKind.Null;
            int width = recorded ? canvas.GetProperty("widthPx").GetInt32() : published?.WidthPx ?? 0;
            int height = recorded ? canvas.GetProperty("heightPx").GetInt32() : published?.HeightPx ?? 0;
            var row = ExportCase(selected.Name, verdict, actual.RootElement, mp4, vtt, width, height, output, "case-" + (++number).ToString("D3"));
            // The annex reads the selected evidence, not the local run record: check the row against it as well.
            RequireAnnexRow(row, evidence, output);
            rows.Add(row);
        }
        foreach (var row in rows) { SafeText(row.CaseId); if (row.RunId is not null) SafeText(row.RunId); }
        var manifest = new { schema = "gcam.str-frames", schemaVersion = 1, contract = "v4", selectionRule = SelectionRule,
            maximumWidthPx = MaximumWidth, jpegQuality = JpegQuality, maximumJpegBytes = MaximumJpegBytes,
            captureScope = "recorded owned-window canvas; recorder masks reapplied; no screen access", cases = rows };
        byte[] text = JsonSerializer.SerializeToUtf8Bytes(manifest, VideoEntry.Json);
        AnnexPrivacy(Encoding.UTF8.GetString(text)); // the annex checks the manifest's raw text, escapes included
        using var last = new FileStream(Path.Combine(output, "manifest.json"), FileMode.CreateNew);
        last.Write(text);
    }
}
