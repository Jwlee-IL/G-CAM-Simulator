using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Gcam.Studio.UiTests.Harness.Isolation;
using Gcam.Studio.UiTests.Harness.Video;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>MD6 in-process recorder: at most one pending and one encoder-active frame, bounded stop, faults isolated.</summary>
public sealed class VideoRecorder
{
    public sealed record Provenance(string RunId, string TestId, string VideoSha256, string CaptionsSha256);
    public interface ISink : IDisposable
    {
        void Write(byte[] pixels, long time100ns, long duration100ns); void Finish();
        /// <summary>See <see cref="MediaFoundationWriter.KeyFrameForcing"/>; a sink without an encoder reports none.</summary>
        string KeyFrameForcing => "not-reported";
    }
    private sealed class Sink(string path, int width, int height) : ISink
    {
        private readonly MediaFoundationWriter _writer = new(path, width, height);
        public void Write(byte[] pixels, long time100ns, long duration100ns) => _writer.Write(pixels, time100ns, duration100ns);
        public void Finish() => _writer.Finish();
        public string KeyFrameForcing => _writer.KeyFrameForcing;
        public void Dispose() => _writer.Dispose();
    }
    private sealed record Pending(VideoCapture.Frame Frame, long Time100ns, Transition? Owner);
    /// <summary>An open window transition: the samples captured while it is open are declared as its interval. It ends at
    /// the first painted capture that starts after the harness reported the window's new state (the launch is reported from
    /// the start); a close is never reported, so it stays open until the capture stops.</summary>
    internal sealed class Transition(string reason, string? stepId, bool reported)
    {
        public string Reason { get; } = reason;
        public string? StepId { get; set; } = stepId;
        public bool StateReported { get; set; } = reported;
    }
    /// <summary>A window transition the harness issued; <see cref="StateReported"/> (or disposing) says the window reports
    /// its new state, so the next painted capture ends the transition.</summary>
    public sealed class WindowTransition : IDisposable
    {
        private readonly VideoRecorder _owner;
        private readonly Transition _transition;
        internal WindowTransition(VideoRecorder owner, Transition transition) { _owner = owner; _transition = transition; }
        public string StepId => _transition.StepId!;
        public void StateReported() => _owner.Report(_transition);
        public void Dispose() => StateReported();
    }
    private readonly object _gate = new();
    private readonly Stopwatch _clock = new();
    private readonly DateTimeOffset _anchorUtc;
    private readonly long _monotonicStart;
    private long _monotonicEnd;
    private readonly Func<bool, VideoCapture.Frame> _capture;
    private readonly Func<ISink> _sink;
    private readonly string _folder, _id, _run, _commit, _binary, _deployment;
    private readonly bool _dirty;
    private readonly int _width, _height;
    private readonly Thread _captureThread, _encodeThread;
    private readonly ManualResetEventSlim _encoderReady = new();
    private readonly List<VideoTimeline.Step> _steps = [];
    private readonly List<VideoTimeline.Step> _late = [];
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private int _generated;
    private bool _entryFinal;
    private string _status = "recording";
    private string? _keyFrameForcing;
    private readonly Dictionary<int, Func<string>> _resolvedSteps = [];
    private readonly List<VideoEntry.MaskRange> _masks = [];
    private readonly List<VideoEvidence.MaskSample> _maskSamples = [];
    private readonly List<(int Frame, long Start100ns, long End100ns, Transition Owner)> _transitionSamples = [];
    private Transition? _transition;
    private int _transitionEpoch;
    private Pending? _pending;
    private volatile bool _stopping, _privacy, _abandoned;
    private int _privacyEpoch, _captured, _dropped, _frames, _masked, _private;
    private long _end, _duration;
    private string? _error;
    private VideoEntry? _entry;
    public VideoEntry? Entry => _entry;
    public VideoCheckResult? Check => _entry?.Check;
    private bool _stopped;
    private bool _completed;
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
        _encodeThread.Start();
        if (!_encoderReady.Wait(TimeSpan.FromSeconds(5)))
        {
            _abandoned = true; _stopping = true;
            lock (_gate) Monitor.PulseAll(_gate);
            throw new TimeoutException("Video encoder startup timed out");
        }
        if (_error is not null) throw new IOException("Video encoder startup failed: " + _error);
        // Constructor completion means recording is ready, before callers execute their first step.
        _anchorUtc = DateTimeOffset.UtcNow; _monotonicStart = Stopwatch.GetTimestamp(); _clock.Start();
        // Launch: from the recording's start until the owned window's first painted capture. Its step is recorded only when
        // a sample is actually marked, so a launch whose first capture is painted leaves no trace in the evidence.
        _transition = new(LaunchReason, null, true);
        _captureThread.Start();
    }
    private long Now => _clock.Elapsed.Ticks;
    public const string LaunchReason = "Launch: recording start until the owned window's first painted capture";
    /// <summary>Declares a window transition the harness is about to cause (maximise, restore, minimise, close): records its
    /// beginning step and marks every sample from now on until the harness reports the new state and a painted capture
    /// follows. A new transition ends an open one. The checker excuses single-colour samples only inside these intervals and
    /// fails any interval longer than <see cref="VideoChecker.MaximumTransition100ns"/>.</summary>
    public WindowTransition BeginTransition(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_gate)
        {
            string id = RecordStep("Window transition: " + reason);
            var transition = new Transition(VideoTimeline.SafeText(reason), id, false);
            _transition = transition; Interlocked.Increment(ref _transitionEpoch);
            return new(this, transition);
        }
    }
    private void Report(Transition transition)
    {
        lock (_gate)
        {
            if (transition.StateReported) return;
            transition.StateReported = true; Interlocked.Increment(ref _transitionEpoch);
        }
    }
    /// <summary>A capture shows the owned window: not a privacy frame, not a failed survey, and not wholly covered by masks.
    /// Decided from the capture's own record, never from its pixels.</summary>
    internal static bool Painted(VideoCapture.Frame frame, int width, int height) => !frame.Privacy
        && !frame.Reasons.Contains("survey-failed") && !CoversCanvas(frame.Masks, width, height);
    /// <summary>Whether the union of the rectangles (x, y, width, height) covers the whole canvas.</summary>
    internal static bool CoversCanvas(int[][] rects, int width, int height)
    {
        var clipped = rects.Where(r => r.Length == 4 && r[2] > 0 && r[3] > 0)
            .Select(r => (L: Math.Max(0, r[0]), T: Math.Max(0, r[1]), R: Math.Min(width, r[0] + r[2]), B: Math.Min(height, r[1] + r[3])))
            .Where(r => r.L < r.R && r.T < r.B).ToArray();
        if (clipped.Length == 0) return false;
        var xs = clipped.SelectMany(r => new[] { r.L, r.R }).Append(0).Append(width).Distinct().Order().ToArray();
        var ys = clipped.SelectMany(r => new[] { r.T, r.B }).Append(0).Append(height).Distinct().Order().ToArray();
        for (int i = 0; i + 1 < xs.Length; i++)
            for (int j = 0; j + 1 < ys.Length; j++)
                if (!clipped.Any(r => r.L <= xs[i] && xs[i + 1] <= r.R && r.T <= ys[j] && ys[j + 1] <= r.B)) return false;
        return true;
    }
    /// <summary>The transition a capture belongs to (caller holds the gate). <paramref name="before"/>, <paramref name="epoch"/>
    /// and <paramref name="reported"/> were read before the capture started, so a transition that began, was reported or was
    /// replaced during the capture marks the sample. The first painted capture that started after the report ends it unmarked.
    /// A privacy sample is never marked: privacy intervals have their own evidence, and the two kinds never overlap.</summary>
    private Transition? Mark(VideoCapture.Frame frame, Transition? before, int epoch, bool reported)
    {
        var current = _transition; bool changed = epoch != _transitionEpoch;
        if (current is null && !changed) return null;
        if (current is not null && !changed && reported && Painted(frame, _width, _height))
        {
            _transition = null; Interlocked.Increment(ref _transitionEpoch);
            return null;
        }
        var owner = current ?? before;
        if (owner is null || frame.Privacy) return null;
        owner.StepId ??= RecordStep("Launch transition: owned window not yet painted");
        return owner;
    }
    /// <summary>Suppressed steps retain their identity with generic text; native dialog details can name folders.</summary>
    public void Step(string text) => RecordStep(text);
    /// <summary>Records an executed step and returns its stable ID. Explicit IDs are safe identifiers outside the
    /// generated <c>step-N</c> form, so generated and explicit IDs never collide. A step after <see cref="Stop"/> never
    /// throws: it is kept as a late step, which cannot map to a cue, so the video check fails step coverage.</summary>
    public string RecordStep(string text, string? id = null)
    {
        lock (_gate)
        {
            if (_completed) throw new InvalidOperationException("Recording is complete; no further executed steps are accepted");
            if (id is null) do id = $"step-{++_generated:0000}"; while (_ids.Contains(id));
            else if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"\A[A-Za-z0-9_-]+\z") || GeneratedId().IsMatch(id) || _ids.Contains(id))
                throw new ArgumentException("Step IDs must be unique safe identifiers outside the generated step-N form", nameof(id));
            _ids.Add(id);
            var step = new VideoTimeline.Step(Now, _privacy ? "Private dialog step — picture suppressed" : VideoTimeline.SafeText(text), id, _privacy);
            if (!_stopped) { _steps.Add(step); return id; }
            _late.Add(step);
            if (_entryFinal) ApplyLate([step]);
            return id;
        }
    }
    private static System.Text.RegularExpressions.Regex GeneratedId() => new(@"\Astep-[0-9]+\z");
    /// <summary>Marks late steps unmapped in the recorded check at once (no re-decode); <see cref="Complete"/> re-runs the
    /// full check on the updated evidence. Caller holds the gate.</summary>
    private void ApplyLate(VideoTimeline.Step[] late)
    {
        if (_entry?.Evidence is not { } evidence || _entry.Check is not { } check) return;
        var ids = late.Select(s => s.Id).ToArray();
        _entry = _entry with
        {
            Evidence = evidence with { Steps = [.. evidence.Steps, .. late], LateStepIds = [.. evidence.LateStepIds, .. ids] },
            Check = check with { Passed = false, FailedCriteria = check.FailedCriteria.Append("step-coverage").Distinct().Order().ToArray(),
                UnmappedStepIds = [.. check.UnmappedStepIds, .. ids] },
        };
        try { SaveStatus(_status); } catch { }
    }
    public void Step(Func<string> resolvedText)
        => RecordStep(resolvedText);
    public string RecordStep(Func<string> resolvedText, string? id = null)
    {
        lock (_gate)
        {
            if (_stopped) return RecordStep(_privacy ? "" : resolvedText(), id);
            int i = _steps.Count;
            string actual = RecordStep("", id);
            if (!_privacy) _resolvedSteps[i] = resolvedText;
            return actual;
        }
    }
    public void Privacy(bool suppressed)
    {
        lock (_gate)
        {
            if (suppressed == _privacy) return; // a no-op change makes no transition frame, step or interval
            if (_privacy && !suppressed) RecordStep("Privacy interval ending");
            _privacy = suppressed; Interlocked.Increment(ref _privacyEpoch);
            if (suppressed) RecordStep("Privacy interval beginning");
        }
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
                Transition? before; int transitionEpoch; bool reported;
                lock (_gate) { before = _transition; transitionEpoch = _transitionEpoch; reported = before?.StateReported == true; }
                var frame = _capture(_privacy);
                if (epoch != Volatile.Read(ref _privacyEpoch)) frame = new(new byte[_width * _height * 4], [[0, 0, _width, _height]], ["privacy-transition"], true);
                lock (_gate)
                {
                    if (_stopping) break;
                    _captured++;
                    if (_pending is not null) _dropped++;
                    _pending = new(frame, time, Mark(frame, before, transitionEpoch, reported)); Monitor.PulseAll(_gate);
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
            _encoderReady.Set();
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
            _keyFrameForcing = sink.KeyFrameForcing;
            if (!_abandoned) sink.Finish();
        }
        catch (Exception e) { Fault(e); _encoderReady.Set(); }
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
        if (item.Frame.Masks.Length > 0 || item.Frame.Privacy)
            _maskSamples.Add(new(_frames, item.Time100ns, item.Time100ns + duration, item.Frame.Privacy,
                item.Frame.Reasons, item.Frame.Masks));
        if (item.Owner is not null)
            lock (_gate) _transitionSamples.Add((_frames, item.Time100ns, item.Time100ns + Math.Max(1, duration), item.Owner));
        _frames++; _duration = item.Time100ns + Math.Max(1, duration);
    }
    private void Fault(Exception e)
    {
        _error = e.GetType().Name; _stopping = true; _end = Now; _monotonicEnd = Stopwatch.GetTimestamp();
        lock (_gate) Monitor.PulseAll(_gate);
    }
    /// <summary>Stop pixels before closing the HWND; defer decoding until the owned process is closed.</summary>
    public void StopCapture(TimeSpan? budget = null)
    {
        if (_stopped) return;
        lock (_gate) { _stopped = true; _end = Now; _monotonicEnd = Stopwatch.GetTimestamp(); _stopping = true; Monitor.PulseAll(_gate); }
        var wait = Stopwatch.StartNew(); TimeSpan limit = budget ?? TimeSpan.FromSeconds(5);
        if (!_captureThread.Join(limit) || !_encodeThread.Join(TimeSpan.FromTicks(Math.Max(0, (limit - wait.Elapsed).Ticks))))
        { _abandoned = true; _error = "stop-timeout"; }
    }
    public Provenance? Stop(TimeSpan? budget = null)
    {
        if (_entryFinal) return ProvenanceOfEntry();
        StopCapture(budget);
        try
        {
            if (_error is null && _frames > 0)
            {
                VideoTimeline.Step[] steps;
                lock (_gate) steps = _steps.Select((s, i) => _resolvedSteps.TryGetValue(i, out var text) ? s with { Text = VideoTimeline.SafeText(text()) } : s).ToArray();
                var cues = VideoTimeline.Cues(steps, _duration);
                File.WriteAllText(Vtt, VideoTimeline.Captions(cues), new UTF8Encoding(false));
                var privacy = new List<VideoEvidence.PrivacyInterval>();
                foreach (var mask in _maskSamples.Where(m => m.Privacy))
                {
                    if (privacy.Count > 0 && privacy[^1].End100ns == mask.Start100ns)
                        privacy[^1] = privacy[^1] with { End100ns = mask.End100ns };
                    else privacy.Add(new(mask.Start100ns, mask.End100ns, "Native dialog privacy suppression", []));
                }
                privacy = privacy.Select(p => p with { StepIds = steps.Where(s => s.Privacy && s.Time100ns >= p.Start100ns - VideoChecker.IntervalTolerance100ns
                    && s.Time100ns <= p.End100ns + VideoChecker.IntervalTolerance100ns).Select(s => s.Id).ToArray() }).ToList();
                var evidence = new VideoEvidence(new(_anchorUtc, _monotonicStart, _monotonicEnd, Stopwatch.Frequency), steps, cues,
                    privacy.ToArray(), _maskSamples.ToArray()) { KeyFrameForcing = _keyFrameForcing, TransitionIntervals = TransitionIntervals() };
                _entry = new(_run, DateTimeOffset.UtcNow, _commit, _dirty, _binary, _deployment,
                    new(_id + ".mp4", IsolationPaths.Hash(File.ReadAllBytes(Mp4)), new FileInfo(Mp4).Length),
                    new(_id + ".vtt", IsolationPaths.Hash(File.ReadAllBytes(Vtt)), new FileInfo(Vtt).Length),
                    _duration, _width, _height, _frames, _captured, _dropped, _masked, _private, _masks.ToArray()) { Evidence = evidence };
                _entry.Validate();
                _entry = _entry with { Check = VideoChecker.Check(Mp4, Vtt, evidence, _entry) };
            }
            lock (_gate)
            {
                _entryFinal = true; _status = _entry is null ? "unavailable" : "encoded";
                if (_late.Count > 0) ApplyLate(_late.ToArray());
                SaveStatus(_status);
            }
        }
        catch (Exception e) { _entry = null; _error = e.GetType().Name; }
        return ProvenanceOfEntry();
    }
    /// <summary>Consecutive written samples of one transition form one interval (a dropped slot inside it does not split it);
    /// a transition still open when the capture stopped ends with its last sample.</summary>
    private VideoEvidence.TransitionInterval[] TransitionIntervals()
    {
        var intervals = new List<VideoEvidence.TransitionInterval>(); Transition? last = null; int lastFrame = -2;
        lock (_gate)
            foreach (var (frame, start, end, owner) in _transitionSamples)
            {
                if (ReferenceEquals(owner, last) && frame == lastFrame + 1) intervals[^1] = intervals[^1] with { End100ns = end };
                else intervals.Add(new(start, end, owner.Reason, owner.StepId is { } id ? [id] : []));
                last = owner; lastFrame = frame;
            }
        return intervals.ToArray();
    }
    private Provenance? ProvenanceOfEntry() => _entry is null ? null : new(_run, _id, _entry.Video.Sha256, _entry.Captions.Sha256);
    public void Complete(bool passed)
    {
        lock (_gate) { if (_completed) return; _completed = true; }
        try
        {
            Stop();
            VideoEntry? withLate;
            lock (_gate) withLate = _entry?.Evidence is { LateStepIds.Length: > 0 } ? _entry : null;
            if (withLate is not null) // the exact check on the evidence including late steps, decoded outside the gate
            {
                var exact = VideoChecker.Check(Mp4, Vtt, withLate.Evidence, withLate);
                lock (_gate) if (ReferenceEquals(_entry, withLate)) _entry = _entry with { Check = exact };
            }
            if (passed && _entry?.Check?.Passed == true) VideoPublisher.Publish(LatestRoot, _id, _entry, Mp4, Vtt);
            SaveStatus(_entry is null ? "unavailable" : !passed ? "failed-test-retained" : _entry.Check?.Passed == true ? "published" : "video-check-failed");
        }
        catch (Exception e) { _error = e.GetType().Name; try { SaveStatus("unavailable"); } catch { } }
        finally { lock (_gate) _completed = true; }
    }
    /// <summary>The run record's copy of this attempt's video status, serialised with the same camelCase property shape as
    /// <c>video-status.json</c> (contract, runId, testId, videoSha256, check, evidence), so a record written with other options
    /// (the run record keeps its historical PascalCase values, the UI manifest's evidence its defaults) still embeds exactly
    /// what the production gate reads. One field differs on purpose: <c>status</c> here is the check verdict at the time the
    /// record is saved (<c>passed</c> / <c>failed</c>, what the gate requires), while <c>video-status.json</c>'s
    /// <c>status</c> is the publication outcome written later by <see cref="Complete"/> (<c>published</c>,
    /// <c>failed-test-retained</c>, <c>video-check-failed</c>, <c>encoded</c>, <c>unavailable</c>).</summary>
    public JsonElement StatusRecord() => JsonSerializer.SerializeToElement(new { contract = "v4", runId = _run, testId = _id,
        status = _entry?.Check?.Passed == true ? "passed" : "failed", videoSha256 = _entry?.Video.Sha256,
        canvas = new { widthPx = _width, heightPx = _height },
        check = _entry?.Check, evidence = _entry?.Evidence }, VideoEntry.Json);
    private void SaveStatus(string status)
    {
        _status = status;
        File.WriteAllBytes(Path.Combine(_folder, "video-status.json"),
        JsonSerializer.SerializeToUtf8Bytes(new { contract = "v4", status, error = _error, testId = _id, runId = _run,
            videoSha256 = _entry?.Check?.VideoSha256, canvas = new { widthPx = _width, heightPx = _height }, check = _entry?.Check, evidence = _entry?.Evidence }, VideoEntry.Json));
    }
}
