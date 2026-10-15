using System.Diagnostics;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class VideoRecorderTests
{
    private sealed class Sink(Action? write = null) : VideoRecorder.ISink
    {
        public readonly List<long> Times = [];
        public void Write(byte[] pixels, long time100ns, long duration100ns) { write?.Invoke(); lock (Times) Times.Add(time100ns); }
        public void Finish() { }
        public void Dispose() { }
    }
    private static VideoRecorder Start(Func<bool, VideoCapture.Frame> capture, Func<VideoRecorder.ISink> sink) => Start(capture, sink, out _);
    private static VideoRecorder Start(Func<bool, VideoCapture.Frame> capture, Func<VideoRecorder.ISink> sink, out string root)
    {
        root = IsolationPathsTests.Scratch(); Directory.CreateDirectory(root);
        return new(root, "case", "run", 64, 48, capture, sink, new('a', 40), false, new('b', 64), new('c', 64));
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
        recorder.Step("dialog step naming a folder"); // dropped while the picture is suppressed
        recorder.Stop(); recorder.Complete(false);
        using var failed = new ManualResetEventSlim();
        var broken = Start(privacy => new(new byte[64 * 48 * 4], [], [], privacy), () => new Sink(() => { failed.Set(); throw new IOException("encoder"); }), out string root);
        Assert.True(failed.Wait(TimeSpan.FromSeconds(2))); Assert.Null(broken.Stop()); broken.Complete(true);
        // A passing test with a faulted recording: no promotion, the attempt says why.
        using var status = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "video-status.json")));
        Assert.Equal(("unavailable", "IOException"), (status.RootElement.GetProperty("status").GetString(), status.RootElement.GetProperty("error").GetString()));
    }
}
