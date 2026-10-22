using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Video;

namespace Gcam.Studio.UiTests;

public sealed class VideoCheckerTests
{
    private sealed record Clip(string Mp4, string Vtt, VideoEvidence Evidence, ObservationFixtures.ScratchFolder Folder) : IDisposable
    {
        public void Dispose() => Folder.Dispose();
    }
    /// <summary>Encodes the frames on an MTA thread with the production writer.</summary>
    private static string Encode(string mp4, int width, int height, IEnumerable<(long Time, byte[] Pixels)> frames)
    {
        Exception? error = null; string forcing = "";
        var thread = new Thread(() =>
        {
            try
            {
                using var writer = new MediaFoundationWriter(mp4, width, height);
                foreach (var (time, pixels) in frames) writer.Write(pixels, time, VideoTimeline.Period100ns);
                writer.Finish(); forcing = writer.KeyFrameForcing;
            }
            catch (Exception e) { error = e; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
        return forcing;
    }
    private static Clip Make(bool blank = false, bool gap = false, bool privacy = false)
    {
        var folder = ObservationFixtures.Scratch(); string root = folder, mp4 = Path.Combine(root, "clip.mp4"), vtt = Path.Combine(root, "clip.vtt");
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var writer = new MediaFoundationWriter(mp4, 64, 48);
                for (int n = 0; n < 40; n++)
                {
                    byte[] pixels = new byte[64 * 48 * 4];
                    if (!blank && !(privacy && n >= 10 && n < 30))
                        for (int y = 10; y < 35; y++) for (int x = 10; x < 35; x++)
                        { int at = (y * 64 + x) * 4; pixels[at] = 40; pixels[at + 1] = 200; pixels[at + 2] = 240; }
                    writer.Write(pixels, n * VideoTimeline.Period100ns + (gap && n >= 20 ? 4_000_000 : 0), VideoTimeline.Period100ns);
                }
                writer.Finish();
            }
            catch (Exception e) { error = e; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
        long duration = gap ? 44_000_000 : 40_000_000;
        VideoTimeline.Step[] steps = privacy ? [new(0, "before", "before"), new(15_000_000, "private", "private", true), new(35_000_000, "after", "after")]
            : [new(0, "start", "start"), new(35_000_000, "assertion", "assertion"), new(duration, "terminal", "terminal")];
        var cues = VideoTimeline.Cues(privacy ? steps.Where(s => s.Id != "private") : steps, duration);
        File.WriteAllText(vtt, VideoTimeline.Captions(cues));
        VideoEvidence.PrivacyInterval[] intervals = privacy ? [new(10_000_000, 30_000_000, "Synthetic privacy control", ["private"])] : [];
        VideoEvidence.MaskSample[] masks = privacy ? Enumerable.Range(10, 20).Select(n => new VideoEvidence.MaskSample(n,
            n * VideoTimeline.Period100ns, (n + 1) * VideoTimeline.Period100ns, true, ["privacy-pause"], [[0, 0, 64, 48]])).ToArray() : [];
        return new(mp4, vtt, new(new(DateTimeOffset.UtcNow, 0, duration, TimeSpan.TicksPerSecond), steps, cues, intervals, masks), folder);
    }
    [MediaFoundationFact]
    public void GoodClip_DecodesToEnd_QualifiesWithIndependentIntervalAndTerminalStep()
    {
        using var clip = Make(); var check = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence);
        Assert.True(check.Passed, string.Join(", ", check.FailedCriteria) + ": " + check.DecodeError);
        Assert.Equal("v4", check.Contract); Assert.Equal(64, check.VideoSha256.Length); Assert.Equal(40, check.DecodedFrames);
        Assert.Empty(check.UnmappedStepIds); Assert.Equal(0.1, check.MaximumTimestampGapSeconds, 6);
        int frames = 0;
        var decoded = MediaFoundationDecoder.Decode(clip.Mp4, (sample, pixels) =>
        { Assert.Equal(64 * 48 * 4, pixels.Length); Assert.Equal(64, sample.WidthPx); frames++; });
        Assert.True(decoded.EndOfStream); Assert.Equal(40, frames);
    }
    [MediaFoundationFact]
    public void CorruptedClip_FailsDecodeAndKeepsHashAndEveryFailure()
    {
        using var clip = Make(); File.WriteAllBytes(clip.Mp4, new byte[512]);
        var check = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence);
        Assert.False(check.Passed); Assert.Contains("decode-to-end", check.FailedCriteria); Assert.NotNull(check.DecodeError);
        Assert.Equal(64, check.VideoSha256.Length); Assert.Contains("recording-interval", check.FailedCriteria);
    }
    [MediaFoundationFact]
    public void TruncatedClip_CannotQualifyEvenIfSourceReaderAcceptsPartialMedia()
    {
        using var clip = Make(); byte[] bytes = File.ReadAllBytes(clip.Mp4); File.WriteAllBytes(clip.Mp4, bytes[..(bytes.Length / 2)]);
        var check = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence);
        Assert.False(check.Passed);
        Assert.True(check.FailedCriteria.Contains("decode-to-end") || check.FailedCriteria.Contains("recording-interval"));
    }
    [MediaFoundationFact]
    public void BlankClip_FailsContinuousAndFrameShareCriteria()
    {
        using var clip = Make(blank: true); var check = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence);
        Assert.Contains("single-colour-continuous", check.FailedCriteria); Assert.Contains("single-colour-share", check.FailedCriteria);
        Assert.Equal(1, check.UndeclaredSingleColourShare); Assert.Equal(4, check.LongestUndeclaredSingleColourSeconds, 6);
    }
    [MediaFoundationFact]
    public void TimestampGap_IsMeasuredFromDecodedSamples()
    {
        using var clip = Make(gap: true); var check = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence);
        Assert.True(check.FailedCriteria.Contains("timestamp-gap"), System.Text.Json.JsonSerializer.Serialize(MediaFoundationDecoder.Decode(clip.Mp4).Samples));
        Assert.True(check.MaximumTimestampGapSeconds > 0.3);
    }
    [MediaFoundationFact]
    public void MissingCaption_FailsExecutedStepCoverage()
    {
        using var clip = Make(); File.WriteAllText(clip.Vtt, "WEBVTT\n\n");
        var check = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence);
        Assert.Contains("step-coverage", check.FailedCriteria); Assert.Contains("terminal", check.UnmappedStepIds);
        Assert.Contains("timed-cues", check.FailedCriteria);
    }
    [MediaFoundationFact]
    public void DeclaredPrivacy_ExactMaskSamplesAndStepMapping_NoShareCap()
    {
        using var clip = Make(privacy: true); var check = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence);
        Assert.True(check.Passed, string.Join(", ", check.FailedCriteria) + ": " +
            (check.Passed ? "" : ObservationFixtures.ExplainVideo(clip.Mp4)));
        Assert.Equal(0.5, check.PrivacyShare, 6);
        Assert.Equal(0, check.UndeclaredSingleColourShare);
        var mismatch = clip.Evidence with { PrivacyIntervals = [clip.Evidence.PrivacyIntervals[0] with { End100ns = 29_000_000 }] };
        var failed = VideoChecker.Check(clip.Mp4, clip.Vtt, mismatch);
        Assert.Contains("privacy-mask-match", failed.FailedCriteria); Assert.False(failed.Passed);
    }
    [MediaFoundationFact]
    public void MissingIndependentEvidenceAndWrongInterval_AreNotMetadataPasses()
    {
        using var clip = Make();
        var missing = VideoChecker.Check(clip.Mp4, clip.Vtt, null);
        Assert.Contains("recording-interval-missing", missing.FailedCriteria); Assert.Contains("step-evidence-missing", missing.FailedCriteria);
        var wrong = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence with { Interval = clip.Evidence.Interval with { MonotonicEnd = 50_000_000 } });
        Assert.Contains("recording-interval", wrong.FailedCriteria);
    }
    [MediaFoundationFact]
    public void PrivacyEvidence_RejectsMissingReasonUnknownIdsDuplicateMasksAndMissingVisibleCues()
    {
        using var clip = Make(privacy: true); var p = clip.Evidence.PrivacyIntervals[0];
        var noReason = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence with { PrivacyIntervals = [p with { Reason = "" }] });
        Assert.Contains("privacy-declaration", noReason.FailedCriteria);
        var unknown = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence with { PrivacyIntervals = [p with { StepIds = ["unknown"] }] });
        Assert.Contains("privacy-step-identities", unknown.FailedCriteria); Assert.Contains("step-coverage", unknown.FailedCriteria);
        var duplicates = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence with { MaskSamples = [.. clip.Evidence.MaskSamples, clip.Evidence.MaskSamples[0]] });
        Assert.Contains("privacy-mask-match", duplicates.FailedCriteria);
        var invisible = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence with { Cues = [] });
        Assert.Contains("privacy-visible-boundaries", invisible.FailedCriteria);
    }
    [MediaFoundationFact]
    public void PaddedCodedHeight_DecodesTheDisplayApertureOnly()
    {
        // 1440 x 900 recordings decode into 16-row-aligned frames (912 rows) after a media-type change on the first
        // sample. A 40-row picture (48 coded) reproduces it: the decoder must follow the change and report and analyse
        // only the displayed 64 x 40, so padding rows can neither fail decoding nor hide or fake a single-colour frame.
        using var scratch = ObservationFixtures.Scratch();
        string mp4 = Path.Combine(scratch, "padded.mp4"); Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var writer = new MediaFoundationWriter(mp4, 64, 40);
                for (int n = 0; n < 20; n++)
                {
                    byte[] pixels = new byte[64 * 40 * 4];
                    if (n < 10) for (int y = 8; y < 30; y++) for (int x = 8; x < 40; x++) pixels[(y * 64 + x) * 4 + 1] = 220;
                    writer.Write(pixels, n * VideoTimeline.Period100ns, VideoTimeline.Period100ns);
                }
                writer.Finish();
            }
            catch (Exception e) { error = e; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
        int consumed = 0;
        var decoded = MediaFoundationDecoder.Decode(mp4, (sample, rgb) => { Assert.Equal(64 * 40 * 4, rgb.Length); consumed++; });
        Assert.True(decoded.EndOfStream, decoded.Error); Assert.Null(decoded.Error);
        Assert.True(decoded.MediaTypeChanges >= 1); // the fixture exercises the change the real recordings show
        Assert.Equal(20, decoded.Samples.Length); Assert.Equal(20, consumed);
        Assert.All(decoded.Samples, s => Assert.Equal((64, 40), (s.WidthPx, s.HeightPx)));
        Assert.All(decoded.Samples.Take(10), s => Assert.False(s.SingleColour));
        Assert.All(decoded.Samples.Skip(10), s => Assert.True(s.SingleColour && s.Black));
    }
    [MediaFoundationFact]
    public void BlackStretchWithDroppedSlots_IsOneContinuousRun()
    {
        // Every third slot of a 1.1 s black stretch is missing: the largest gap (0.2 s) passes the gap rule, but each black
        // frame stays on screen until the next sample, so the stretch is one run and must fail continuity.
        using var scratch = ObservationFixtures.Scratch(); string mp4 = Path.Combine(scratch, "dropped.mp4"), vtt = Path.Combine(scratch, "dropped.vtt");
        var frames = new List<(long, byte[])>();
        for (int n = 0; n < 40; n++)
        {
            bool black = n >= 10 && n <= 20;
            if (black && n % 3 == 0) continue;
            byte[] pixels = new byte[64 * 48 * 4];
            if (!black) for (int y = 10; y < 35; y++) for (int x = 10; x < 35; x++) pixels[(y * 64 + x) * 4 + 1] = 220;
            frames.Add((n * VideoTimeline.Period100ns, pixels));
        }
        Encode(mp4, 64, 48, frames);
        var decoded = MediaFoundationDecoder.Decode(mp4);
        Assert.Equal(frames.Count, decoded.Samples.Length);
        VideoTimeline.Step[] steps = [new(0, "start", "start"), new(30_000_000, "end", "end")];
        var cues = VideoTimeline.Cues(steps, 40_000_000); File.WriteAllText(vtt, VideoTimeline.Captions(cues));
        var check = VideoChecker.Check(mp4, vtt, new(new(DateTimeOffset.UtcNow, 0, 40_000_000, TimeSpan.TicksPerSecond), steps, cues, [], []));
        Assert.Equal(0.2, check.MaximumTimestampGapSeconds, 6); Assert.DoesNotContain("timestamp-gap", check.FailedCriteria);
        Assert.True(check.FailedCriteria.Contains("single-colour-continuous"), ObservationFixtures.ExplainVideo(mp4));
        Assert.Equal(1.1, check.LongestUndeclaredSingleColourSeconds, 6);
    }
    [MediaFoundationFact]
    public void Writer_ReportsGuaranteedKeyFramePlacement_WithTheDefaultSoftwareEncoder()
    {
        // The sink writer enables no hardware transform, so the encoder is the synchronous software one; anything else
        // must be reported as placement-unverified rather than claimed.
        using var scratch = ObservationFixtures.Scratch();
        string forcing = Encode(Path.Combine(scratch, "mode.mp4"), 64, 48, Enumerable.Range(0, 3).Select(n => (n * VideoTimeline.Period100ns, new byte[64 * 48 * 4])));
        Assert.Equal("requested-synchronous-encoder-after-drain-marker", forcing);
    }
    [Fact]
    public void MediaTypeChange_IsFollowedOnlyBeforeTheFirstDeliveredSample()
    {
        Assert.True(MediaFoundationDecoder.MayFollowTypeChange(0));
        Assert.False(MediaFoundationDecoder.MayFollowTypeChange(1));
        Assert.False(MediaFoundationDecoder.MayFollowTypeChange(500));
    }
    [MediaFoundationFact]
    public void HighDefinitionClip_KeepsItsColourThroughEncodeAndDecode()
    {
        // A matrix mismatch between the writer's RGB->YCbCr conversion and the decoder's YCbCr->RGB shifts this green by
        // ~19 levels in G (measured: BT.601 encode decoded as (R 51, G 161, B 59)), far above H.264 quantisation.
        using var scratch = ObservationFixtures.Scratch(); string mp4 = Path.Combine(scratch, "hd.mp4");
        const int width = 160, height = 720;
        byte[] pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = 60; pixels[i + 1] = 180; pixels[i + 2] = 60; }
        Encode(mp4, width, height, Enumerable.Range(0, 5).Select(n => (n * VideoTimeline.Period100ns, pixels)));
        var centres = new List<(int B, int G, int R)>();
        var decoded = MediaFoundationDecoder.Decode(mp4, (sample, rgb) =>
        {
            int at = (sample.HeightPx / 2 * sample.WidthPx + sample.WidthPx / 2) * 4; centres.Add((rgb[at], rgb[at + 1], rgb[at + 2]));
        });
        Assert.True(decoded.EndOfStream, decoded.Error);
        Assert.All(centres, c => Assert.True(Math.Abs(c.R - 60) <= 8 && Math.Abs(c.G - 180) <= 8 && Math.Abs(c.B - 60) <= 8, $"decoded {c}"));
    }
    /// <summary>A 4 s clip (40 samples at 10 fps) whose listed samples are black, with the given steps and transitions.</summary>
    private static Clip Transitions(int[] black, VideoTimeline.Step[] steps, VideoEvidence.TransitionInterval[]? transitions,
        VideoEvidence.PrivacyInterval[]? privacy = null)
    {
        var folder = ObservationFixtures.Scratch(); string mp4 = Path.Combine(folder, "clip.mp4"), vtt = Path.Combine(folder, "clip.vtt");
        Encode(mp4, 64, 48, Enumerable.Range(0, 40).Select(n =>
        {
            byte[] pixels = new byte[64 * 48 * 4];
            if (!black.Contains(n))
                for (int y = 10; y < 35; y++) for (int x = 10; x < 35; x++) { int at = (y * 64 + x) * 4; pixels[at] = 40; pixels[at + 1] = 200; pixels[at + 2] = 240; }
            return (n * VideoTimeline.Period100ns, pixels);
        }));
        var cues = VideoTimeline.Cues(steps, 40_000_000);
        File.WriteAllText(vtt, VideoTimeline.Captions(cues));
        var masks = (privacy ?? []).SelectMany(p => Enumerable.Range((int)(p.Start100ns / VideoTimeline.Period100ns),
            (int)((p.End100ns - p.Start100ns) / VideoTimeline.Period100ns))).Select(n => new VideoEvidence.MaskSample(n,
            n * VideoTimeline.Period100ns, (n + 1) * VideoTimeline.Period100ns, true, ["privacy-pause"], [[0, 0, 64, 48]])).ToArray();
        return new(mp4, vtt, new(new(DateTimeOffset.UtcNow, 0, 40_000_000, TimeSpan.TicksPerSecond), steps, cues, privacy ?? [], masks)
            { TransitionIntervals = transitions }, folder);
    }
    private static readonly VideoTimeline.Step[] StateChangeSteps =
        [new(0, "start", "start"), new(10_000_000, "Window transition: maximise", "maximise"), new(35_000_000, "assertion", "assertion"), new(40_000_000, "terminal", "terminal")];

    [MediaFoundationFact]
    public void BlackSamplesInsideADeclaredStateChange_AreExcused_AndCountedAsTransitionShare()
    {
        // Two black samples (1.0-1.2 s) are 5 % of the clip, far over the 1 % rule unless the harness declared them.
        using var declared = Transitions([10, 11], StateChangeSteps, [new(10_000_000, 12_000_000, "maximise", ["maximise"])]);
        var check = VideoChecker.Check(declared.Mp4, declared.Vtt, declared.Evidence);
        Assert.True(check.Passed, string.Join(", ", check.FailedCriteria) + ": " + ObservationFixtures.ExplainVideo(declared.Mp4));
        Assert.Equal(0, check.UndeclaredSingleColourShare); Assert.Equal(0, check.LongestUndeclaredSingleColourSeconds);
        Assert.Equal(0.05, check.TransitionShare!.Value, 6);
        // The same samples outside every declared interval fail exactly as before: none declared, or one elsewhere.
        var none = VideoChecker.Check(declared.Mp4, declared.Vtt, declared.Evidence with { TransitionIntervals = [] });
        Assert.Equal(new[] { "single-colour-share" }, none.FailedCriteria); Assert.Equal(0.05, none.UndeclaredSingleColourShare, 6);
        Assert.Equal(0, none.TransitionShare);
        VideoTimeline.Step[] later = [.. StateChangeSteps.Where(s => s.Id != "maximise"), new(20_000_000, "Window transition: restore", "restore")];
        using var elsewhere = Transitions([10, 11], later, [new(20_000_000, 22_000_000, "restore", ["restore"])]);
        var outside = VideoChecker.Check(elsewhere.Mp4, elsewhere.Vtt, elsewhere.Evidence);
        Assert.Equal(new[] { "single-colour-share" }, outside.FailedCriteria); Assert.Equal(0.05, outside.TransitionShare!.Value, 6);
    }

    [MediaFoundationFact]
    public void LaunchAndCloseTransitions_AtTheClipEdges_Pass()
    {
        // Launch: black until the first painted capture; close: black after the harness closed the window, until the stop.
        VideoTimeline.Step[] steps = [new(500_000, "Launch transition: owned window not yet painted", "launch"), new(5_000_000, "ready", "ready"),
            new(38_000_000, "Window transition: close", "close")];
        using var clip = Transitions([0, 1, 38, 39], steps,
            [new(0, 2_000_000, VideoRecorder.LaunchReason, ["launch"]), new(38_000_000, 40_000_000, "close", ["close"])]);
        var check = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence);
        Assert.True(check.Passed, string.Join(", ", check.FailedCriteria) + ": " + ObservationFixtures.ExplainVideo(clip.Mp4));
        Assert.Equal(0.1, check.TransitionShare!.Value, 6);
    }

    [MediaFoundationFact]
    public void TransitionLongerThanHalfASecond_FailsEvenThoughItExcusesItsSamples()
    {
        using var clip = Transitions([10, 11, 12, 13, 14, 15], StateChangeSteps, [new(10_000_000, 16_000_000, "maximise", ["maximise"])]);
        var check = VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence);
        Assert.False(check.Passed); Assert.Equal(new[] { "transition-length" }, check.FailedCriteria);
        Assert.Equal(0, check.UndeclaredSingleColourShare);
        // Exactly 0.5 s is the limit itself and passes.
        using var limit = Transitions([10, 11, 12, 13, 14], StateChangeSteps, [new(10_000_000, 15_000_000, "maximise", ["maximise"])]);
        Assert.True(VideoChecker.Check(limit.Mp4, limit.Vtt, limit.Evidence).Passed);
    }

    [MediaFoundationFact]
    public void MalformedTransitions_AreRefused()
    {
        using var clip = Transitions([10, 11], StateChangeSteps, [new(10_000_000, 12_000_000, "maximise", ["maximise"])]);
        var t = clip.Evidence.TransitionIntervals![0];
        string[] Failed(VideoEvidence.TransitionInterval[] transitions)
            => VideoChecker.Check(clip.Mp4, clip.Vtt, clip.Evidence with { TransitionIntervals = transitions }).FailedCriteria;
        Assert.Contains("transition-step-identities", Failed([t with { StepIds = ["unknown"] }]));
        Assert.Contains("transition-step-identities", Failed([t with { StepIds = ["assertion"] }])); // a step far from the interval cannot have begun it
        Assert.Contains("transition-declaration", Failed([t with { Reason = " " }]));
        Assert.Contains("transition-declaration", Failed([t with { Start100ns = 9_500_000 }])); // not sample-aligned
        Assert.Contains("transition-declaration", Failed([t, t with { Start100ns = 11_000_000 }])); // overlapping
        // A transition never overlaps a privacy interval: the recorder never marks a privacy sample as a transition.
        VideoTimeline.Step[] steps = [new(0, "start", "start"), new(10_000_000, "Window transition: maximise", "maximise"),
            new(15_000_000, "Private dialog step — picture suppressed", "private", true), new(35_000_000, "after", "after")];
        using var both = Transitions([.. Enumerable.Range(10, 10)], steps.Where(s => !s.Privacy).ToArray(),
            [new(10_000_000, 12_000_000, "maximise", ["maximise"])], [new(10_000_000, 20_000_000, "Synthetic privacy control", ["private"])]);
        var overlap = VideoChecker.Check(both.Mp4, both.Vtt, both.Evidence with { Steps = steps });
        Assert.Contains("transition-declaration", overlap.FailedCriteria);
    }

    [MediaFoundationFact]
    public void OldEvidenceWithoutTransitions_KeepsItsVerdictAndItsShape()
    {
        using var good = Make();
        Assert.Null(good.Evidence.TransitionIntervals);
        var passed = VideoChecker.Check(good.Mp4, good.Vtt, good.Evidence);
        Assert.True(passed.Passed); Assert.Null(passed.TransitionShare);
        string evidenceJson = System.Text.Json.JsonSerializer.Serialize(good.Evidence, VideoEntry.Json);
        string checkJson = System.Text.Json.JsonSerializer.Serialize(passed, VideoEntry.Json);
        Assert.DoesNotContain("transition", evidenceJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("transition", checkJson, StringComparison.OrdinalIgnoreCase);
        // An old record read back has no transitions and is judged as before: its black samples still fail.
        using var black = Transitions([10, 11], StateChangeSteps, null);
        var read = System.Text.Json.JsonSerializer.Deserialize<VideoEvidence>(
            System.Text.Json.JsonSerializer.Serialize(black.Evidence, VideoEntry.Json), VideoEntry.Json)!;
        Assert.Null(read.TransitionIntervals);
        var failed = VideoChecker.Check(black.Mp4, black.Vtt, read);
        Assert.Equal(new[] { "single-colour-share" }, failed.FailedCriteria); Assert.Null(failed.TransitionShare);
    }
    [Fact]
    public void TerminalSteps_KeepStableIdsInPositiveTimedCue_WithoutOverlappingPreviousCue()
    {
        VideoTimeline.Step[] steps = [new(0, "first", "a"), new(10_000_000, "terminal one", "b"), new(11_000_000, "terminal two", "c")];
        var cues = VideoTimeline.Cues(steps, 10_000_000);
        Assert.Equal(new[] { "b", "c" }, cues[^1].StepIds); Assert.True(cues[^1].End100ns > cues[^1].Start100ns);
        Assert.Equal(cues[0].End100ns, cues[1].Start100ns);
        Assert.Contains("terminal one · terminal two", VideoTimeline.Captions(cues));
    }
}
