using System.Diagnostics;
using System.Text.Json;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness.Observation;

/// <summary>A recorded product failure (hang, early or abnormal exit, launch failure) stopped this UI step or input.</summary>
public sealed class ProductLivenessException(string message) : InvalidOperationException(message);

/// <summary>Owns the live channels from before launch until after exit. Missing instruments are explicit gaps.</summary>
public sealed class RunObservation : IDisposable
{
    private DocumentsWatcher? _documents;
    private ConnectionSampler? _connections;
    private LivenessObserver? _liveness;
    private readonly List<string> _errors = [];
    public string? ControlDirectory { get; private set; }
    public DocumentsWatchResult? Documents { get; private set; }
    public ConnectionResult? Connections { get; private set; }
    public LivenessResult? Liveness { get; private set; }
    public string[] Errors => _errors.ToArray();
    private bool _stopped;
    private Task? _failureRecord;
    private string? _controlFile;
    private bool _createdDirectory;
    public bool ControlCreated { get; private set; }
    public bool ControlRemoved { get; private set; }
    public DateTimeOffset? ControlCreatedAtUtc { get; private set; }
    public DateTimeOffset? ControlRemovedAtUtc { get; private set; }
    /// <summary>A cleanup failure (a run issue), kept apart from the channel errors, which are instrument gaps.</summary>
    public string? ControlRemovalError { get; private set; }

    public RunObservation(string runId, string documentsRoot, string surfaceList)
    {
        _ = IsolationPaths.RunRoot("scratch", runId);
        try
        {
            _documents = new(documentsRoot, surfaceList);
            if (_documents.Snapshot().CoversRoot)
            {
                // Created new for this run only: an existing folder of that name is never adopted (or later removed).
                string control = Path.Combine(_documents.Snapshot().ResolvedRoot, ".gcam-uia-" + runId);
                if (Directory.Exists(control) || File.Exists(control)) throw new IOException("Documents control folder already exists");
                ControlDirectory = control;
                Directory.CreateDirectory(ControlDirectory);
                _createdDirectory = true;
                _controlFile = Path.Combine(ControlDirectory, "gcam-report-20000101-000000.html");
                _documents.PositiveControl(Path.Combine(Path.GetFileName(ControlDirectory), Path.GetFileName(_controlFile)));
                ControlCreated = File.Exists(_controlFile); // the watcher creates it with CreateNew inside the fresh folder
                if (ControlCreated) ControlCreatedAtUtc = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception e) { _errors.Add("file: " + e.Message); }
    }

    /// <param name="window">Headless test seam for HWND discovery; Studio launches use the owned process's main window.</param>
    /// <param name="limit">Headless test seam; Studio launches use the charter limit (10 s).</param>
    public void Attach(Process launched, OwnedProcessIdentity identity, string? failureRecordDirectory = null,
        Func<IntPtr>? window = null, TimeSpan? limit = null)
    {
        try
        {
            _liveness = new(launched, identity, window, limit);
            if (failureRecordDirectory is not null)
                _failureRecord = _liveness.Failure.ContinueWith(t => File.WriteAllBytes(
                    Path.Combine(failureRecordDirectory, ".gcam-liveness-failure.json"),
                    JsonSerializer.SerializeToUtf8Bytes(new { contract = "v4", liveness = t.Result,
                        productIssues = UiRunManifest.LivenessIssues(t.Result), productStatus = "failed" }, VideoEntry.Json)),
                    CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
        catch (Exception e) { _errors.Add("liveness: " + e.Message); }
        try
        {
            _connections = new(identity);
            _connections.SampleNow();
        }
        catch (Exception e) { _errors.Add("network: " + e.Message); }
    }
    public void ProveConnectionChannel() => _connections?.PositiveControl();
    public bool HasFailure => _liveness is null || _liveness.Failure.IsCompleted;
    /// <summary>A recorded product failure: one of the product issues (<c>not_responding</c>, <c>app_exited</c>,
    /// <c>abnormal_exit</c>, <c>launch_window_timeout</c>). An instrument error alone (a failed probe or window lookup) also
    /// refuses input (<see cref="HasFailure"/>) but is a channel gap, never a product failure.</summary>
    public bool HasProductFailure => _liveness is not null && UiRunManifest.LivenessIssues(_liveness.Snapshot()).Length != 0;

    private const string FailureDataKey = "gcam.runObservation";
    /// <summary>A launch that fails after the observation exists carries it on the exception, so its channel results and the
    /// Documents control's creation and removal still reach the manifest.</summary>
    public void AttachTo(Exception failure) => failure.Data[FailureDataKey] = this;
    public static RunObservation? From(Exception failure) => failure.Data[FailureDataKey] as RunObservation;
    public bool WindowSeen => _liveness?.WindowSeen == true;
    public void RunUi(Action action, string phase = "running")
    {
        _liveness?.SetPhase(phase);
        RequireResponsive();
        RunUntilFailure(action, _liveness!.Failure);
    }
    /// <summary>A blocked UIA call cannot stop the controlling thread from recording product failure. Its worker is
    /// left alone; subsequent input is refused by the sticky liveness guard, never by terminating a process/thread.</summary>
    public static void RunUntilFailure(Action action, Task<LivenessResult> failure)
    {
        if (failure.IsCompleted) throw Refusal(failure.Result, "");
        var work = Task.Run(action);
        if (Task.WhenAny(work, failure).GetAwaiter().GetResult() == failure)
        {
            _ = work.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            throw Refusal(failure.Result, "; UIA worker did not need to return");
        }
        work.GetAwaiter().GetResult();
    }
    public void RequireResponsive()
    {
        if (_liveness is null) throw new InvalidOperationException("liveness-monitor-not-proven; further input refused");
        var snapshot = _liveness.Snapshot();
        if (_liveness.Failure.IsCompleted || !snapshot.Passed)
            throw Refusal(snapshot, "; further input refused"); // product issues are sticky in every later snapshot
    }
    /// <summary>A product failure gets its own exception type (the only one cleanup may excuse on the product axis); an
    /// instrument error stays an ordinary refusal.</summary>
    private static InvalidOperationException Refusal(LivenessResult result, string suffix) =>
        UiRunManifest.LivenessIssues(result).Length != 0
            ? new ProductLivenessException("product-liveness-failure" + suffix)
            : new InvalidOperationException("liveness-instrument-failure" + suffix);
    public void LaunchFailed() => _liveness?.LaunchFailed();
    public void MarkIntentionalClose() => _liveness?.MarkIntentionalClose();
    public void Dispose()
    {
        if (_stopped) return;
        _stopped = true;
        try { Liveness = _liveness?.Stop(); } catch (Exception e) { _errors.Add("liveness: " + e.Message); }
        if (_failureRecord?.IsCompleted == true || Liveness?.Passed == false)
            try { _failureRecord?.GetAwaiter().GetResult(); } catch (Exception e) { _errors.Add("liveness-record: " + e.Message); }
        try { _connections?.SampleNow(); _connections?.Dispose(); Connections = _connections?.Snapshot(); }
        catch (Exception e) { _errors.Add("network: " + e.Message); }
        RemoveControl();
        try { _documents?.Dispose(); Documents = _documents?.Snapshot(); }
        catch (Exception e) { _errors.Add("file: " + e.Message); }
    }

    /// <summary>Removes only what this run created: the control file (named, never a pattern) and then the folder it
    /// created, non-recursively, so anything else found in it stays and the failed removal is reported.</summary>
    private void RemoveControl()
    {
        if (!_createdDirectory) return;
        try
        {
            if (_controlFile is not null && File.Exists(_controlFile)) File.Delete(_controlFile);
            Directory.Delete(ControlDirectory!, recursive: false);
        }
        catch (Exception e) { ControlRemovalError = e.GetType().Name + ": " + e.Message; }
        ControlRemoved = !Directory.Exists(ControlDirectory) && (_controlFile is null || !File.Exists(_controlFile));
        if (ControlRemoved) ControlRemovedAtUtc = DateTimeOffset.UtcNow;
        else ControlRemovalError ??= "control still present";
    }

    public void Record(UiRunManifest manifest, ICollection<UiRunManifest.Issue> issues)
    {
        RecordResults(manifest, issues, Documents, Connections, Liveness, Errors, ControlDirectory);
        manifest.Evidence["documentsControl"] = new { file = _controlFile, directory = ControlDirectory,
            created = ControlCreated, createdAtUtc = ControlCreatedAtUtc, removed = ControlRemoved, removedAtUtc = ControlRemovedAtUtc,
            removalError = ControlRemovalError };
        if (ControlRemovalError is not null) issues.Add(new("cleanup_failed", "documents-control-removal-failed: " + ControlRemovalError));
    }

    public static void RecordResults(UiRunManifest manifest, ICollection<UiRunManifest.Issue> issues,
        DocumentsWatchResult? documents, ConnectionResult? connections, LivenessResult? liveness,
        string[] errors, string? controlDirectory = null)
    {
        manifest.RecordLiveness(liveness);
        manifest.Evidence["liveChannels"] = new { file = documents, network = connections,
            process = liveness, errors, harnessOwnedFiles = controlDirectory is null ? Array.Empty<string>() : [controlDirectory],
            blindSpots = new[] { "processes and connections entirely between polling samples", "Documents subtree blind spots are listed in file.blindSpots" } };
        if (documents?.Passed != true || connections?.SampleCount is not > 0 || connections.PositiveControlPassed != true
            || connections.Errors.Length != 0 || liveness is null || liveness.Errors.Length != 0 || errors.Length != 0)
            issues.Add(new("blocked", "live-channel-gap"));
        if (connections?.Findings.Length > 0) issues.Add(new("unsafe_effect", "unapproved-network-peer"));
        // A pattern alone is attribution, never an exemption. Keep transient Documents effects even if C is clean.
        if (documents?.Events.Any(e => !e.HarnessControl) == true)
            issues.Add(new("unsafe_effect", "default-export-outside-isolation"));
    }
}
