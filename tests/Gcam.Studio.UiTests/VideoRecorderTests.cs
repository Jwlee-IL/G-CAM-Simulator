using System.Diagnostics;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class VideoRecorderTests : IDisposable
{
    private readonly List<string> _roots = [];
    /// <summary>Removes the scratch folders this test's recorders used (each recorder is stopped by then).</summary>
    public void Dispose()
    {
        foreach (string root in _roots)
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // a hung-capture worker may still hold it
    }
    private sealed class Sink(Action? write = null) : VideoRecorder.ISink
    {
        public readonly List<long> Times = [];
        public void Write(byte[] pixels, long time100ns, long duration100ns) { write?.Invoke(); lock (Times) Times.Add(time100ns); }
        public void Finish() { }
        public void Dispose() { }
    }
    private VideoRecorder Start(Func<bool, VideoCapture.Frame> capture, Func<VideoRecorder.ISink> sink) => Start(capture, sink, out _);
    private VideoRecorder Start(Func<bool, VideoCapture.Frame> capture, Func<VideoRecorder.ISink> sink, out string root)
    {
        root = IsolationPathsTests.Scratch(); Directory.CreateDirectory(root); _roots.Add(root);
        return new(root, "case", "run", 64, 48, capture, sink, new('a', 40), false, new('b', 64), new('c', 64));
    }
    [Fact]
    public void CaptureStopsBeforeChecking_AndCompleteRefusesFurtherSteps()
    {
        var recorder = Start(privacy => new(new byte[64 * 48 * 4], [], [], privacy), () => new Sink());
        recorder.RecordStep("initial", "action-0001");
        recorder.StopCapture();
        Assert.Null(recorder.Entry);
        recorder.Stop();
        recorder.Complete(false);
        Assert.Throws<InvalidOperationException>(() => recorder.RecordStep("after completion", "action-0002"));
        Assert.Throws<InvalidOperationException>(() => recorder.RecordStep(() => "deferred after completion", "action-0003"));
    }
    [Fact]
    public void EdgeRing_CoversEveryBorderPixel_AndNothingInside()
    {
        var size = new System.Drawing.Size(40, 30); var ring = VideoCapture.EdgeRing(size); int e = VideoCapture.NeutralEdgePx;
        for (int y = 0; y < size.Height; y++)
            for (int x = 0; x < size.Width; x++)
            {
                bool edge = x < e || y < e || x >= size.Width - e || y >= size.Height - e;
                Assert.Equal(edge, ring.Any(r => r.Contains(x, y)));
            }
    }

    [Fact]
    public void SlowEncoder_DropsPendingFrames_PreservesMonotonicGaps_AndStopsWithinBudget()
    {
        var sink = new Sink(() => Thread.Sleep(240)); int captured = 0;
        var recorder = Start(privacy => { Interlocked.Increment(ref captured); return new(new byte[64 * 48 * 4], [], [], privacy); }, () => sink);
        SpinWait.SpinUntil(() => Volatile.Read(ref captured) >= 7, TimeSpan.FromSeconds(3));
        recorder.Stop(TimeSpan.FromSeconds(2)); recorder.Complete(false);
        lock (sink.Times)
        {
            Assert.True(sink.Times.Count < captured);
            Assert.True(sink.Times.Zip(sink.Times.Skip(1)).All(p => p.Second > p.First));
            Assert.Contains(sink.Times.Zip(sink.Times.Skip(1)), p => p.Second - p.First > VideoTimeline.Period100ns);
        }
    }
    [Fact]
    public void HungCapture_StopIsBounded_AndWorkerCanLaterExit()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); using var left = new ManualResetEventSlim();
        var recorder = Start(_ => { entered.Set(); release.Wait(); left.Set(); return new(new byte[64 * 48 * 4], [], [], false); }, () => new Sink());
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2))); var clock = Stopwatch.StartNew();
            Assert.Null(recorder.Stop(TimeSpan.FromMilliseconds(50))); Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1));
            recorder.Complete(false);
        }
        finally { release.Set(); Assert.True(left.Wait(TimeSpan.FromSeconds(2))); }
    }
    /// <summary>The two halves use separate recorders: an encoder fault stops capture at once, so a privacy pause
    /// requested after the first faulted frame would never be captured (the cause of the first version's failure).</summary>
    [Fact]
    public void EncoderExceptionAndPrivacyPause_NeverEscapeTheTestBoundary()
    {
        using var captured = new ManualResetEventSlim();
        var recorder = Start(privacy => { if (privacy) captured.Set(); return new(new byte[64 * 48 * 4], [], [], privacy); }, () => new Sink());
        recorder.Privacy(true); Assert.True(captured.Wait(TimeSpan.FromSeconds(2)));
        recorder.Step("dialog step naming a folder"); // identity retained with generic privacy text
        recorder.Stop(); recorder.Complete(false);
        using var failed = new ManualResetEventSlim();
        var broken = Start(privacy => new(new byte[64 * 48 * 4], [], [], privacy), () => new Sink(() => { failed.Set(); throw new IOException("encoder"); }), out string root);
        Assert.True(failed.Wait(TimeSpan.FromSeconds(2))); Assert.Null(broken.Stop()); broken.Complete(true);
        // A passing test with a faulted recording: no promotion, the attempt says why.
        using var status = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "video-status.json")));
        Assert.Equal(("unavailable", "IOException"), (status.RootElement.GetProperty("status").GetString(), status.RootElement.GetProperty("error").GetString()));
    }
    private sealed class RealSink(string path) : VideoRecorder.ISink
    {
        private readonly MediaFoundationWriter _writer = new(path, 64, 48);
        public void Write(byte[] pixels, long time100ns, long duration100ns) => _writer.Write(pixels, time100ns, duration100ns);
        public void Finish() => _writer.Finish();
        public string KeyFrameForcing => _writer.KeyFrameForcing;
        public void Dispose() => _writer.Dispose();
    }
    private sealed class Counts { public int Visible, Private; }
    private static VideoRecorder Real(string root, Counts counts) => new(root, "case", "run", 64, 48, privacy =>
    {
        var bytes = new byte[64 * 48 * 4];
        if (privacy) Interlocked.Increment(ref counts.Private);
        else
        {
            for (int y = 10; y < 35; y++) for (int x = 10; x < 35; x++) bytes[(y * 64 + x) * 4 + 1] = 220;
            Interlocked.Increment(ref counts.Visible);
        }
        return new(bytes, privacy ? [[0, 0, 64, 48]] : [], privacy ? ["privacy-pause"] : [], privacy);
    }, () => new RealSink(Path.Combine(root, "attempt.mp4")), new('a', 40), false, new('b', 64), new('c', 64));
    [MediaFoundationFact]
    public void RealRecording_PersistsIndependentIntervalStableStepsAndExactPrivacyEvidence()
    {
        using var scratch = ObservationFixtures.Scratch(); string root = scratch; var counts = new Counts();
        var recorder = Real(root, counts);
        try
        {
            recorder.RecordStep("before", "before");
            Assert.Throws<ArgumentException>(() => recorder.RecordStep("duplicate", "before"));
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref counts.Visible) >= 3, 5000));
            recorder.Privacy(true); recorder.RecordStep("private-value-must-not-survive", "private");
            bool evaluated = false; recorder.Step(() => { evaluated = true; return "private-deferred-value"; });
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref counts.Private) >= 5, 5000));
            recorder.Privacy(false); recorder.RecordStep("after", "after");
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref counts.Visible) >= 6, 5000));
            Assert.NotNull(recorder.Stop()); Assert.False(evaluated);
            var entry = Assert.IsType<VideoEntry>(recorder.Entry);
            Assert.NotNull(entry.Evidence); Assert.True(entry.Evidence.Interval.MonotonicEnd > entry.Evidence.Interval.MonotonicStart);
            Assert.Contains(entry.Evidence.Steps, s => s.Id == "private" && !s.Text.Contains("private-value", StringComparison.Ordinal));
            Assert.StartsWith("requested-", entry.Evidence.KeyFrameForcing); // reported, whatever the encoder mode
            Assert.True(entry.Check!.Passed, string.Join(", ", entry.Check.FailedCriteria) + ": " +
                (entry.Check.Passed ? "" : ObservationFixtures.ExplainVideo(recorder.Mp4)));
            string status = File.ReadAllText(Path.Combine(root, "video-status.json"));
            Assert.Contains("\"contract\": \"v4\"", status); Assert.DoesNotContain("private-value-must-not-survive", status);
            Assert.Contains(entry.Video.Sha256, status); Assert.Contains("keyFrameForcing", status);
        }
        finally { recorder.Stop(); }
    }
    [Fact]
    public void StepIds_RefuseTrailingNewlineAndTheGeneratedForm_GeneratedIdsStayUnique()
    {
        var recorder = Start(privacy => new(new byte[64 * 48 * 4], [], [], privacy), () => new Sink(), out _);
        try
        {
            Assert.Throws<ArgumentException>(() => recorder.RecordStep("x", "safe\n")); // '$' would have accepted it
            Assert.Throws<ArgumentException>(() => recorder.RecordStep("x", "step-0002")); // reserved for generated IDs
            Assert.Equal("step-0001", recorder.RecordStep("first"));
            Assert.Equal("custom", recorder.RecordStep("explicit", "custom"));
            Assert.Equal("step-0002", recorder.RecordStep("second"));
            recorder.Step(() => "deferred"); recorder.Step("plain");
            Assert.Throws<ArgumentException>(() => recorder.RecordStep("again", "custom"));
        }
        finally { recorder.Stop(); recorder.Complete(false); }
    }
    [MediaFoundationFact]
    public void NoOpPrivacyChange_MakesNoTransitionFrameStepOrInterval()
    {
        using var scratch = ObservationFixtures.Scratch(); var counts = new Counts();
        var recorder = Real(scratch, counts);
        try
        {
            recorder.RecordStep("start", "start");
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref counts.Visible) >= 2, 5000));
            for (int i = 0; i < 5; i++) { recorder.Privacy(false); Thread.Sleep(30); } // privacy is already off
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref counts.Visible) >= 6, 5000));
            recorder.RecordStep("end", "end");
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref counts.Visible) >= 8, 5000));
            Assert.NotNull(recorder.Stop());
            var evidence = recorder.Entry!.Evidence!;
            Assert.Empty(evidence.MaskSamples); Assert.Empty(evidence.PrivacyIntervals);
            Assert.Equal(new[] { "start", "end" }, evidence.Steps.Select(s => s.Id));
            Assert.Equal(0, recorder.Entry.PrivacySuppressedFrames); Assert.True(recorder.Check!.Passed, string.Join(", ", recorder.Check.FailedCriteria));
        }
        finally { recorder.Stop(); }
    }
    [MediaFoundationFact]
    public void StepAfterStop_DoesNotThrow_IsALateStep_AndFailsStepCoverage()
    {
        using var scratch = ObservationFixtures.Scratch(); var counts = new Counts();
        var recorder = Real(scratch, counts);
        recorder.RecordStep("start", "start");
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref counts.Visible) >= 4, 5000));
        Assert.NotNull(recorder.Stop()); Assert.True(recorder.Check!.Passed, string.Join(", ", recorder.Check.FailedCriteria));
        string late = recorder.RecordStep("background log after the end"); // a late background log never faults its caller
        recorder.Step(() => "late deferred text");
        Assert.Contains(late, recorder.Entry!.Evidence!.LateStepIds); Assert.Equal(2, recorder.Entry.Evidence.LateStepIds.Length);
        Assert.False(recorder.Check!.Passed); Assert.Contains("step-coverage", recorder.Check.FailedCriteria);
        Assert.Contains(late, recorder.Check.UnmappedStepIds);
        Assert.Contains(late, File.ReadAllText(Path.Combine(scratch, "video-status.json")));
        recorder.Complete(false); // the exact re-check on the evidence with the late steps agrees
        Assert.Contains("step-coverage", recorder.Check!.FailedCriteria); Assert.Contains(late, recorder.Check.UnmappedStepIds);
    }
}
