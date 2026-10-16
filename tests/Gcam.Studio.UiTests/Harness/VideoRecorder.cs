using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>MD6 in-process recorder: at most one pending and one encoder-active frame, bounded stop, faults isolated.</summary>
public sealed class VideoRecorder
{
    public sealed record Provenance(string RunId, string TestId, string VideoSha256, string CaptionsSha256);
    public interface ISink : IDisposable { void Write(byte[] pixels, long time100ns, long duration100ns); void Finish(); }
    private sealed class Sink(string path, int width, int height) : ISink
    {
        private readonly MediaFoundationWriter _writer = new(path, width, height);
        public void Write(byte[] pixels, long time100ns, long duration100ns) => _writer.Write(pixels, time100ns, duration100ns);
        public void Finish() => _writer.Finish();
        public void Dispose() => _writer.Dispose();
    }
    private sealed record Pending(VideoCapture.Frame Frame, long Time100ns);
    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Func<bool, VideoCapture.Frame> _capture;
    private readonly Func<ISink> _sink;
    private readonly string _folder, _id, _run, _commit, _binary, _deployment;
    private readonly bool _dirty;
    private readonly int _width, _height;
    private readonly Thread _captureThread, _encodeThread;
    private readonly List<VideoTimeline.Step> _steps = [];
    private readonly Dictionary<int, Func<string>> _resolvedSteps = [];
    private readonly List<VideoEntry.MaskRange> _masks = [];
    private Pending? _pending;
    private volatile bool _stopping, _privacy, _abandoned;
    private int _privacyEpoch, _captured, _dropped, _frames, _masked, _private;
    private long _end, _duration;
    private string? _error;
    private VideoEntry? _entry;
    private bool _stopped;
    public string Mp4 => Path.Combine(_folder, "attempt.mp4");
    public string Vtt => Path.Combine(_folder, "attempt.vtt");
    public static bool Enabled(string? value) => value != "0";
    /// <summary>The latest passing videos: repository-local, git-ignored, removed by <c>git clean -fdx</c>.</summary>
    public static string LatestRoot => Path.Combine(RepoPaths.Root(), "artifacts", "ui-videos");
    /// <summary>Folders the recorder and the result cache (<c>cache.json</c>, also without video) may write outside the
    /// run record, for an observed batch's harness accounting.</summary>
    public static string[] OwnedDestinations() => Enabled(Environment.GetEnvironmentVariable("GCAM_UI_VIDEO"))
        || ResultCache.RecordsPasses(ResultCache.Mode.Current()) ? [LatestRoot] : [];
    public static VideoRecorder? TryStart(RunRecord record, IntPtr hwnd, int pid, string id)
    {
        if (!Enabled(Environment.GetEnvironmentVariable("GCAM_UI_VIDEO"))) { record.Set("video", "disabled"); return null; }
        try
        {
            var frame = WindowSurvey.Bounds(hwnd);
            int width = checked((frame.Width + 1) & ~1), height = checked((frame.Height + 1) & ~1);
            var capture = new VideoCapture(hwnd, pid, width, height);
            var subject = ManualRun.ReadSubject(); string commit = ManualRun.GitHead();
            return new(record.Directory, id, record.RunId, width, height, capture.Take,
                () => new Sink(Path.Combine(record.Directory, "attempt.mp4"), width, height), commit,
                RepoPaths.DirtyFileCount() != 0, IsolationPaths.Hash(File.ReadAllBytes(RepoPaths.StudioExe())), subject.DeploymentSha256);
        }
        catch (Exception e) { record.Set("video", "unavailable: " + e.GetType().Name); return null; }
    }
    public VideoRecorder(string folder, string id, string run, int width, int height, Func<bool, VideoCapture.Frame> capture,
        Func<ISink> sink, string commit, bool dirty, string binary, string deployment)
    {
        _folder = folder; _id = id; _run = run; _width = width; _height = height; _capture = capture; _sink = sink;
        _commit = commit; _dirty = dirty; _binary = binary; _deployment = deployment;
        _encodeThread = new(Encode) { IsBackground = true, Name = "GCAM video encoder" };
        _encodeThread.SetApartmentState(ApartmentState.MTA);
        _captureThread = new(Capture) { IsBackground = true, Name = "GCAM video capture" };
        _encodeThread.Start(); _captureThread.Start();
    }
    private long Now => _clock.Elapsed.Ticks;
    /// <summary>A caption. While the picture is suppressed only the generic pause caption is kept: dialog steps can
    /// name folders.</summary>
    public void Step(string text) { if (!_privacy) lock (_gate) _steps.Add(new(Now, text)); }
    public void Step(Func<string> resolvedText)
    {
        if (!_privacy) lock (_gate) { _resolvedSteps[_steps.Count] = resolvedText; _steps.Add(new(Now, "")); }
    }
    public void Privacy(bool suppressed)
    {
        if (suppressed) Step("Private dialog — picture suppressed");
        _privacy = suppressed; Interlocked.Increment(ref _privacyEpoch);
    }
    private void Capture()
    {
        try
        {
            long next = 0;
            while (!_stopping)
            {
                long now = Now;
                if (now < next) { Thread.Sleep((int)Math.Min(20, Math.Max(1, (next - now) / 10_000))); continue; }
                long time = VideoTimeline.Slot(now); int epoch = Volatile.Read(ref _privacyEpoch);
                var frame = _capture(_privacy);
                if (epoch != Volatile.Read(ref _privacyEpoch)) frame = new(new byte[_width * _height * 4], [[0, 0, _width, _height]], ["privacy-transition"], true);
                lock (_gate)
                {
                    if (_stopping) break;
                    _captured++;
                    if (_pending is not null) _dropped++;
                    _pending = new(frame, time); Monitor.PulseAll(_gate);
                }
                next = time + VideoTimeline.Period100ns;
            }
        }
        catch (Exception e) { Fault(e); }
    }
    private void Encode()
    {
        try
        {
            using var sink = _sink();
            while (true)
            {
                Pending? next;
                lock (_gate)
                {
                    while (_pending is null && !_stopping) Monitor.Wait(_gate, 100);
                    next = _pending; _pending = null;
                    if (next is null && _stopping) break;
                }
                if (next is null) continue;
                // Timestamp gaps preserve wall time if slots were dropped; there is no third retained frame.
                Write(sink, next, VideoTimeline.Period100ns);
            }
            if (!_abandoned) sink.Finish();
        }
        catch (Exception e) { Fault(e); }
    }
    private void Write(ISink sink, Pending item, long duration)
    {
        if (_abandoned) return;
        sink.Write(item.Frame.Rgb32, item.Time100ns, Math.Max(1, duration));
        if (item.Frame.Masks.Length > 0)
        {
            _masked++;
            var range = new VideoEntry.MaskRange(_frames, _frames, item.Frame.Reasons.Length == 0 ? ["masked"] : item.Frame.Reasons, item.Frame.Masks);
            // Consecutive frames with identical masks and reasons share one range (R14).
            if (_masks.Count > 0 && _masks[^1].LastFrame == _frames - 1 && _masks[^1].SameMask(range)) _masks[^1] = _masks[^1] with { LastFrame = _frames };
            else _masks.Add(range);
        }
        if (item.Frame.Privacy) _private++;
        _frames++; _duration = item.Time100ns + Math.Max(1, duration);
    }
    private void Fault(Exception e)
    {
        _error = e.GetType().Name; _stopping = true; _end = Now;
        lock (_gate) Monitor.PulseAll(_gate);
    }
    public Provenance? Stop(TimeSpan? budget = null)
    {
        if (_stopped) return ProvenanceOfEntry();
        _stopped = true; _end = Now; _stopping = true;
        lock (_gate) Monitor.PulseAll(_gate);
        var wait = Stopwatch.StartNew(); TimeSpan limit = budget ?? TimeSpan.FromSeconds(5);
        if (!_captureThread.Join(limit) || !_encodeThread.Join(TimeSpan.FromTicks(Math.Max(0, (limit - wait.Elapsed).Ticks))))
        { _abandoned = true; _error = "stop-timeout"; }
        try
        {
            if (_error is null && _frames > 0)
            {
                VideoTimeline.Step[] steps;
                lock (_gate) steps = _steps.Select((s, i) => _resolvedSteps.TryGetValue(i, out var text) ? s with { Text = text() } : s).ToArray();
                File.WriteAllText(Vtt, VideoTimeline.Captions(steps, _duration), new UTF8Encoding(false));
                _entry = new(_run, DateTimeOffset.UtcNow, _commit, _dirty, _binary, _deployment,
                    new(_id + ".mp4", IsolationPaths.Hash(File.ReadAllBytes(Mp4)), new FileInfo(Mp4).Length),
                    new(_id + ".vtt", IsolationPaths.Hash(File.ReadAllBytes(Vtt)), new FileInfo(Vtt).Length),
                    _duration, _width, _height, _frames, _captured, _dropped, _masked, _private, _masks.ToArray());
                _entry.Validate();
            }
            SaveStatus(_entry is null ? "unavailable" : "encoded");
        }
        catch (Exception e) { _entry = null; _error = e.GetType().Name; }
        return ProvenanceOfEntry();
    }
    private Provenance? ProvenanceOfEntry() => _entry is null ? null : new(_run, _id, _entry.Video.Sha256, _entry.Captions.Sha256);
    public void Complete(bool passed)
    {
        try
        {
            Stop();
            if (passed && _entry is not null) VideoPublisher.Publish(LatestRoot, _id, _entry, Mp4, Vtt);
            SaveStatus(_entry is null ? "unavailable" : passed ? "published" : "failed-test-retained");
        }
        catch (Exception e) { _error = e.GetType().Name; try { SaveStatus("unavailable"); } catch { } }
    }
    private void SaveStatus(string status) => File.WriteAllBytes(Path.Combine(_folder, "video-status.json"),
        JsonSerializer.SerializeToUtf8Bytes(new { status, error = _error, testId = _id, runId = _run }, VideoEntry.Json));
}
