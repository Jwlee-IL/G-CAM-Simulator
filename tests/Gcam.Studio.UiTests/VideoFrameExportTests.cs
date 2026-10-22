using System.Text.Json;
using System.Text.Json.Nodes;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;
using Gcam.Studio.UiTests.Harness.Video;

namespace Gcam.Studio.UiTests;

public sealed class VideoFrameExportTests
{
    /// <summary>Opt-in offline entry: GCAM_STR_FRAME_EXPORT names a JSON file with bundle, runRoot, videoRoot, output.
    /// Filter to this method; it never starts Studio or uses desktop input.</summary>
    [Fact]
    public void ExportBundle_FromExplicitInput()
    {
        string? input = Environment.GetEnvironmentVariable("GCAM_STR_FRAME_EXPORT");
        if (string.IsNullOrEmpty(input)) return;
        using var config = JsonDocument.Parse(File.ReadAllBytes(input));
        string Get(string key) => config.RootElement.GetProperty(key).GetString()!;
        VideoFrameExport.Export(Get("bundle"), Get("runRoot"), Get("videoRoot"), Get("output"));
    }

    private sealed record Clip(ObservationFixtures.ScratchFolder Folder, JsonElement Record, string Mp4, string Vtt) : IDisposable
    { public void Dispose() => Folder.Dispose(); }

    /// <summary>The recorder's own step for a window transition it began (turn G): it has a cue but is not a scenario step,
    /// so it is not in the run's executedStepIds.</summary>
    private const string RecorderStep = "step-0001", RecorderCue = "Window transition: maximise";

    private static Clip Make(bool sparse = false, VideoEvidence.TransitionInterval[]? transitions = null, bool recorderStep = false)
    {
        var folder = ObservationFixtures.Scratch(); string root = folder;
        string mp4 = Path.Combine(root, "attempt.mp4"), vtt = Path.Combine(root, "attempt.vtt");
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var writer = new MediaFoundationWriter(mp4, 64, 48);
                for (int n = 0; n < 40; n++)
                {
                    var pixels = new byte[64 * 48 * 4];
                    if (n is < 16 or >= 24)
                        for (int y = 8; y < 38; y++) for (int x = 8; x < 46; x++)
                        { int at = (y * 64 + x) * 4; pixels[at] = (byte)(40 + n); pixels[at + 1] = 180; pixels[at + 2] = 220; }
                    writer.Write(pixels, n * VideoTimeline.Period100ns, VideoTimeline.Period100ns);
                }
                writer.Finish();
            }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.MTA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(15))); Assert.Null(error);
        VideoTimeline.Step[] steps = [new(0, "start", "start"), new(8_000_000, "key one", "one"),
            new(16_000_000, "private", "private", true), new(24_000_000, "key two", "two"),
            new(32_000_000, "key three", "three"), new(40_000_000, "terminal", "end")];
        if (sparse) steps = steps.Where(s => s.Id is "start" or "private").ToArray();
        if (recorderStep)
        {
            // A maximise at 2.8 s, its transition over sample 28: the step's cue (2.8-3.2 s) still overlaps samples 29-31.
            steps = steps.Append(new(28_000_000, RecorderCue, RecorderStep)).OrderBy(s => s.Time100ns).ToArray();
            transitions = [new(28_000_000, 29_000_000, "maximise", [RecorderStep])];
        }
        var cues = VideoTimeline.Cues(steps.Where(s => !s.Privacy), 40_000_000);
        File.WriteAllText(vtt, VideoTimeline.Captions(cues));
        var evidence = new VideoEvidence(new(DateTimeOffset.UtcNow, 0, 40_000_000, TimeSpan.TicksPerSecond), steps, cues,
            [new(16_000_000, 24_000_000, "synthetic privacy", ["private"])],
            Enumerable.Range(16, 8).Select(n => new VideoEvidence.MaskSample(n, n * VideoTimeline.Period100ns,
                (n + 1) * VideoTimeline.Period100ns, true, ["privacy-pause"], [[0, 0, 64, 48]])).ToArray()) { TransitionIntervals = transitions };
        var check = VideoChecker.Check(mp4, vtt, evidence);
        Assert.True(check.Passed, string.Join(",", check.FailedCriteria));
        var record = JsonSerializer.SerializeToElement(new { contract = "v4", runId = "gcam-frame-fixture", result = "passed",
            executedStepIds = steps.Where(s => s.Id != RecorderStep).Select(s => s.Id).ToArray(), videoStatus = new { contract = "v4", runId = "gcam-frame-fixture",
                testId = "fixture", status = "passed", videoSha256 = check.VideoSha256, check, evidence,
                canvas = new { widthPx = 64, heightPx = 48 } } }, VideoEntry.Json);
        return new(folder, record, mp4, vtt);
    }

    [MediaFoundationFact]
    public void Selection_UsesLastVisibleCueSamples_ExcludesPrivacy_AndBoundsJpegs()
    {
        using var clip = Make(); string root = clip.Folder;
        var result = VideoFrameExport.ExportCase("fixture", "passed", clip.Record, clip.Mp4, clip.Vtt, 64, 48, root, "case");
        Assert.Equal(new[] { "start", "one", "two", "three", "end" }, result.Frames.Select(f => f.StepId));
        Assert.Equal(new long[] { 7_000_000, 15_000_000, 31_000_000, 39_000_000, 39_000_000 }, result.Frames.Select(f => f.MediaTime100ns));
        Assert.All(result.Frames, frame =>
        {
            Assert.False(frame.MediaTime100ns < 24_000_000 && frame.MediaEnd100ns > 16_000_000);
            byte[] jpeg = File.ReadAllBytes(Path.Combine(root, frame.File));
            Assert.Equal(frame.Sha256, IsolationPaths.Hash(jpeg));
            Assert.InRange(jpeg.Length, 100, VideoFrameExport.MaximumJpegBytes);
            using var image = System.Drawing.Image.FromStream(new MemoryStream(jpeg));
            Assert.Equal((64, 48), (image.Width, image.Height));
        });
    }

    [MediaFoundationFact]
    public void Selection_NeverPicksASampleInsideAWindowTransition()
    {
        // Samples 28-31 (2.8-3.2 s) are a declared transition, begun by the step at 3.2 s (within the interval's tolerance).
        // Without it the "two" cue (2.4-3.2 s) is illustrated by its last sample, 31 (see the test above); with it, by 27.
        using var clip = Make(transitions: [new(28_000_000, 32_000_000, "maximise", ["three"])]); string root = clip.Folder;
        var result = VideoFrameExport.ExportCase("fixture", "passed", clip.Record, clip.Mp4, clip.Vtt, 64, 48, root, "case");
        Assert.Equal(new[] { "start", "one", "two", "three", "end" }, result.Frames.Select(f => f.StepId));
        Assert.Equal(new long[] { 7_000_000, 15_000_000, 27_000_000, 39_000_000, 39_000_000 }, result.Frames.Select(f => f.MediaTime100ns));
        Assert.All(result.Frames, frame => Assert.False(frame.MediaTime100ns < 32_000_000 && frame.MediaEnd100ns > 28_000_000));
    }

    [MediaFoundationFact]
    public void Selection_NeverPutsAFrameOnARecorderStep_TheRunDidNotExecute()
    {
        // Without the executed-step rule the 3.0 s target picks the recorder step (it ties with "three" and comes first),
        // and its last cue sample, 31, is outside the transition: the annex then refuses "unknown or private step".
        using var clip = Make(recorderStep: true); string root = clip.Folder;
        var result = VideoFrameExport.ExportCase("fixture", "passed", clip.Record, clip.Mp4, clip.Vtt, 64, 48, root, "case");
        Assert.Equal(new[] { "start", "one", "two", "three", "end" }, result.Frames.Select(f => f.StepId));
        Assert.Equal(new long[] { 7_000_000, 15_000_000, 27_000_000, 39_000_000, 39_000_000 }, result.Frames.Select(f => f.MediaTime100ns));
        Assert.DoesNotContain(result.Frames, f => f.StepId == RecorderStep || f.CueText == RecorderCue);
        // The annex mirror refuses such a row outright, even on a sample outside the transition.
        var onRecorder = result with { Frames = result.Frames.Select((f, i) => i != 2 ? f
            : f with { StepId = RecorderStep, CueText = RecorderCue, MediaTime100ns = 31_000_000, MediaEnd100ns = 32_000_000 }).ToArray() };
        Assert.Contains("unknown or private step", Assert.Throws<InvalidDataException>(() =>
            VideoFrameExport.RequireAnnexRow(onRecorder, clip.Record, root)).Message);
        var shortened = result with { Frames = result.Frames[..2] };
        Assert.Contains("frame count", Assert.Throws<InvalidDataException>(() => VideoFrameExport.RequireAnnexRow(shortened, clip.Record, root)).Message);
        // The annex reads the manifest's raw JSON: an escaped character after "a:" reads as a drive path there.
        Assert.Contains("private path", Assert.Throws<InvalidDataException>(() => VideoFrameExport.AnnexPrivacy(@"{""text"":""a:\u00b7""}")).Message);
    }

    [MediaFoundationFact]
    public void HashCanvasAndFailedOrMissingStatus_RefuseWithoutAnExportManifest()
    {
        using var clip = Make(); string root = clip.Folder;
        Assert.Contains("canvas", Assert.Throws<InvalidDataException>(() => VideoFrameExport.ExportCase("fixture", "passed", clip.Record,
            clip.Mp4, clip.Vtt, 66, 48, root, "wrong")).Message);
        var bad = JsonNode.Parse(clip.Record.GetRawText())!;
        bad["videoStatus"]!["check"]!["passed"] = false;
        Assert.Throws<InvalidDataException>(() => VideoFrameExport.RequireVideoStatus(JsonSerializer.SerializeToElement(bad)));
        bad.AsObject().Remove("videoStatus");
        Assert.Throws<InvalidDataException>(() => VideoFrameExport.RequireVideoStatus(JsonSerializer.SerializeToElement(bad)));
        File.AppendAllText(clip.Mp4, "changed bytes");
        Assert.Contains("hash differs", Assert.Throws<InvalidDataException>(() => VideoFrameExport.ExportCase("fixture", "passed", clip.Record,
            clip.Mp4, clip.Vtt, 64, 48, root, "bad")).Message);
        Assert.False(File.Exists(Path.Combine(root, "manifest.json")));
    }

    [MediaFoundationFact]
    public void Bundle_PinsTheExactRun_WritesItsManifestLast_AndNeverOverwrites()
    {
        using var clip = Make(recorderStep: true); string root = clip.Folder;
        string recordPath = Path.Combine(root, "manifest.json"); byte[] raw = JsonSerializer.SerializeToUtf8Bytes(clip.Record);
        File.WriteAllBytes(recordPath, raw);
        var node = JsonNode.Parse(raw)!.AsObject(); node["runRecordSha256"] = IsolationPaths.Hash(raw);
        string bundle = Path.Combine(root, "bundle"), output = Path.Combine(root, "frames"); Directory.CreateDirectory(bundle);
        var record = new { contract = "v4", runId = "gcam-batch", profile = "desktop", cases = new[] { new { id = "fixture", verdict = "passed" } },
            desktopEvidence = new Dictionary<string, JsonNode> { ["fixture"] = node }, artifacts = Array.Empty<object>(),
            source = new { files = new Dictionary<string, string> { ["fixture.cs"] = new('e', 64), ["β.cs"] = new('f', 64) } },
            subject = new { files = new Dictionary<string, string> { ["fixture.cs"] = new('e', 64), ["β.cs"] = new('f', 64) } } };
        byte[] bytes = VideoFrameExport.Canonical(JsonSerializer.SerializeToElement(record));
        File.WriteAllBytes(Path.Combine(bundle, "record.json"), bytes);
        File.WriteAllBytes(Path.Combine(bundle, "selection.json"), JsonSerializer.SerializeToUtf8Bytes(new
        { records = new[] { new { path = "record.json", sha256 = IsolationPaths.Hash(bytes) } }, caseSelections = new Dictionary<string, string> { ["fixture"] = "gcam-batch" } }));
        VideoFrameExport.Export(bundle, root, Path.Combine(root, "no-index"), output);
        Assert.Equal(5, Directory.GetFiles(output, "*.jpg").Length);
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "manifest.json")));
        Assert.Equal(5, manifest.RootElement.GetProperty("cases")[0].GetProperty("frames").GetArrayLength());
        Assert.DoesNotContain(manifest.RootElement.GetProperty("cases")[0].GetProperty("frames").EnumerateArray(),
            f => f.GetProperty("stepId").GetString() == RecorderStep);
        Assert.All(Directory.GetFiles(output, "*.jpg"), file => Assert.True(File.GetLastWriteTimeUtc(file) <= File.GetLastWriteTimeUtc(Path.Combine(output, "manifest.json"))));
        foreach (string name in new[] { "catalog.md", "report.md", "inventory.md", "trace.md" })
            File.WriteAllText(Path.Combine(bundle, name), "synthetic archive fixture");
        File.WriteAllText(Path.Combine(bundle, "generation.json"), "{}\n");
        const string gate = """
            import sys,json
            from pathlib import Path
            sys.path.insert(0,sys.argv[1])
            import ui_annex,software_model as m,software_formats as f
            root=Path(sys.argv[2]);ui_annex.ROOT=root
            record=json.loads((root/'bundle/record.json').read_bytes())
            selection=json.loads((root/'bundle/selection.json').read_bytes())
            blocks,inputs=ui_annex.annex(root,selection,[record],record['cases'])
            m.LinkMap({'STR':blocks}).rewrite({'STR':blocks})
            for i,b in enumerate(blocks):b['id']='fixture'+str(i)
            meta=dict(edition='2026-10-22-1',date='2026-10-22',sourceCommit='a'*40,draft=True,treeStatus='synthetic',
                      milestone='M4',testSubject='b'*40,manual=dict(deploymentSha256='c'*64),history={'STR':[]})
            f.check_html(f.render_html('STR',blocks,meta,{}),blocks)
            f.check_docx(f.render_docx('STR',blocks,meta,{}),blocks)
            import test_records as tr
            tr.export_bundle(root/'bundle',root/'archive')
            print('accepted',sum(b['kind']=='image' for b in blocks))
            """;
        string python = PythonInterpreter.Resolve(PythonInterpreter.Execute, Environment.GetEnvironmentVariable("GCAM_TEST_PYTHON"));
        var accepted = PythonInterpreter.Execute(python, ["-B", "-c", gate, Path.Combine(RepoPaths.Root(), "samples/docs"), root]);
        Assert.True(accepted.ExitCode == 0, accepted.Error);
        Assert.Contains("accepted 5", accepted.Output);
        // The same annex refuses a frame on the recorder's step, which the exporter wrote before it checked execution.
        const string refuse = """
            import sys,json
            from pathlib import Path
            sys.path.insert(0,sys.argv[1])
            import ui_annex
            root=Path(sys.argv[2]);ui_annex.ROOT=root
            record=json.loads((root/'bundle/record.json').read_bytes())
            selection=json.loads((root/'bundle/selection.json').read_bytes())
            ui_annex.annex(root,selection,[record],record['cases'])
            """;
        string manifestPath = Path.Combine(output, "manifest.json"); byte[] exported = File.ReadAllBytes(manifestPath);
        var onRecorder = JsonNode.Parse(exported)!; var moved = onRecorder["cases"]![0]!["frames"]![2]!;
        moved["stepId"] = RecorderStep; moved["cueText"] = RecorderCue;
        File.WriteAllBytes(manifestPath, JsonSerializer.SerializeToUtf8Bytes(onRecorder));
        var refused = PythonInterpreter.Execute(python, ["-B", "-c", refuse, Path.Combine(RepoPaths.Root(), "samples/docs"), root]);
        File.WriteAllBytes(manifestPath, exported);
        Assert.NotEqual(0, refused.ExitCode);
        Assert.Contains("frames: unknown or private step", refused.Error);
        string archive = Path.Combine(root, "archive"), archivedOutput = Path.Combine(root, "archive-frames");
        VideoFrameExport.Export(archive, root, root, archivedOutput);
        Assert.Equal(5, Directory.GetFiles(archivedOutput, "*.jpg").Length);
        var pool = JsonNode.Parse(VideoFrameExport.Read(Path.Combine(archive, "manifests.json")))!.AsObject();
        pool.First().Value!["fixture.cs"] = new string('0', 64);
        using (var file = File.Create(Path.Combine(archive, "manifests.json.gz")))
        using (var zip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Compress))
            zip.Write(VideoFrameExport.Canonical(JsonSerializer.SerializeToElement(pool)));
        string badPool = Path.Combine(root, "bad-pool-frames");
        Assert.Contains("pool hash differs", Assert.Throws<InvalidDataException>(() => VideoFrameExport.Export(archive, root, root, badPool)).Message);
        Assert.False(File.Exists(Path.Combine(badPool, "manifest.json")));
        Assert.Throws<InvalidDataException>(() => VideoFrameExport.Export(bundle, root, root, output));
        File.AppendAllText(clip.Mp4, "corruption");
        string failed = Path.Combine(root, "refused");
        Assert.Throws<InvalidDataException>(() => VideoFrameExport.Export(bundle, root, root, failed));
        Assert.False(File.Exists(Path.Combine(failed, "manifest.json")));
    }

    [Fact]
    public void PrivatePathsAndAccountNames_AreRefusedInManifestText()
    {
        Assert.Throws<InvalidDataException>(() => VideoFrameExport.SafeText(@"open C:\private\file"));
        if (Environment.UserName.Length >= 3)
            Assert.Throws<InvalidDataException>(() => VideoFrameExport.SafeText("hello " + Environment.UserName));
    }

    [Fact]
    public void Jpegs_AreDownscaledWithoutUpscaling_AndPreserveCanvasAspect()
    {
        byte[] bytes = VideoFrameExport.Jpeg(new byte[1200 * 600 * 4], 1200, 600, []);
        using var image = System.Drawing.Image.FromStream(new MemoryStream(bytes));
        Assert.Equal((960, 480), (image.Width, image.Height));
        Assert.True(bytes.Length < VideoFrameExport.MaximumJpegBytes);
    }

    [MediaFoundationFact]
    public void SparseTimeline_StillExportsThreeRealVisibleFrames_WithoutInventingSteps()
    {
        using var clip = Make(sparse: true); string root = clip.Folder;
        var result = VideoFrameExport.ExportCase("fixture", "passed", clip.Record, clip.Mp4, clip.Vtt, 64, 48, root, "sparse");
        Assert.Equal(3, result.Frames.Length);
        Assert.All(result.Frames, frame => { Assert.Equal("start", frame.StepId); Assert.Equal("start", frame.CueText); });
        Assert.Equal(new long[] { 0, 15_000_000, 39_000_000 }, result.Frames.Select(f => f.MediaTime100ns));
    }

    [Fact]
    public void Jpegs_ReapplyTheRecordedForeignWindowMask_WithinTheOwnedCanvas()
    {
        var pixels = new byte[64 * 48 * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = 80; pixels[i + 1] = 180; pixels[i + 2] = 240; }
        byte[] bytes = VideoFrameExport.Jpeg(pixels, 64, 48, [[0, 0, 32, 32]]);
        var redacted = (byte[])pixels.Clone();
        for (int y = 0; y < 32; y++) Array.Clear(redacted, y * 64 * 4, 32 * 4);
        // JPEG is lossy, so decoded black need not be RGB(0,0,0). Require exact encoded equality to an
        // independently redacted source instead of introducing a pixel tolerance after observing the result.
        Assert.Equal(VideoFrameExport.Jpeg(redacted, 64, 48, []), bytes);
        using var image = new System.Drawing.Bitmap(new MemoryStream(bytes));
        Assert.True(image.GetPixel(48, 16).R > 0);
        Assert.Equal((64, 48), (image.Width, image.Height));
    }
}
