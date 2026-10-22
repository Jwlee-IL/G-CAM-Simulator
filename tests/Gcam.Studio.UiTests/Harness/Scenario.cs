using System.Windows.Automation;
using System.Runtime.ExceptionServices;
using Gcam.Studio.UiTests.Harness.Isolation;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>
/// One scenario = one fresh, sandboxed, owned app instance: start → normalise the window → body → close and check the
/// app exited cleanly and wrote nothing. A failure leaves a diagnostics bundle; every run leaves a manifest.
/// </summary>
/// <remarks>A new process per scenario trades run time (~2 s) for isolation: no tool, selection, zoom or result leaks
/// from one scenario into the next.</remarks>
public static class Scenario
{
    public const int WindowWidth = 1440, WindowHeight = 900;

    public static void Run(string name, ITestOutputHelper output, Action<StudioWindow, RunRecord> body)
        => RunCore(name, output, (ui, record, _) => body(ui, record), _ => []);

    /// <summary>The report scenario alone declares writes at fixed names in its owned sandbox: the export and its scene
    /// record, plus the metadata file for event data (CSV); the names carry the run ID (D4).</summary>
    public static void RunReportExport(string name, ITestOutputHelper output, Action<StudioWindow, RunRecord, string> body, string extension = "html")
        => RunCore(name, output, body, runId => ReportExportDialog.DeclaredFiles(runId, extension), caseArguments: [extension]);

    /// <summary>The manual adapter owns the outer final verdict and exact export destinations (a function of the run ID,
    /// which the typed export names carry).</summary>
    public static void RunManual(string name, ITestOutputHelper output, Action<StudioWindow, RunRecord, string> body,
        Func<string, IReadOnlyList<string>> expectedWrites, Action<VideoRecorder?> recorder, Action<RunRecord>? recordCreated = null)
        => RunCore(name, output, body, expectedWrites, recorder, resultUse: "manual", recordCreated: recordCreated);

    /// <summary>Failure diagnostics read the owned window through UIA, so they run under the same bounded runner as the
    /// scenario (phase <c>failure-capture</c>): a hang there is recorded as <c>not_responding</c> and control returns
    /// within the limit. The error itself is recorded first, without any UIA call. Also a headless test seam.</summary>
    internal static void CaptureFailureBounded(Observation.RunObservation? observation, RunRecord record, Exception error, Action capture)
    {
        if (observation is null || observation.HasFailure) { record.CaptureFailure(error, null); return; }
        record.Set("error", error.ToString());
        observation.RunUi(capture, "failure-capture");
    }

    /// <summary>A cleanup step (as <see cref="CleanupBoundary.Attempt"/>) that excuses exactly one exception: the product
    /// refusal (<see cref="Observation.ProductLivenessException"/>) while a product failure is recorded, which belongs to the
    /// product axis (the original failure is kept, no run issue). Every other exception, also after a product failure
    /// (sandbox enumeration, lease release, monitor disposal, ownership, instrument errors), stays the run issue
    /// <paramref name="issueType"/>. Also a headless test seam.</summary>
    internal static Exception? Attempt(Exception? existing, Action action, List<UiRunManifest.Issue> issues, string issueType,
        Observation.RunObservation? observation)
    {
        try { action(); return existing; }
        catch (Observation.ProductLivenessException e) when (observation?.HasProductFailure == true) { return existing ?? e; }
        catch (Exception e) { issues.Add(new(issueType, e.GetType().Name + ": " + e.Message)); return existing ?? e; }
    }

    /// <summary>The final checks (a headless test seam with the real <see cref="CleanupBoundary"/>). Run issues: a process
    /// still running after close (<c>cleanup_failed</c>), any earlier issue or undeclared write (<c>blocked</c>). A product
    /// failure (hang, early or abnormal exit, launch failure: <see cref="UiRunManifest.ProductIssues"/>) fails the scenario
    /// on the product axis only, so the run can still be <c>completed</c> with a failed product; a non-zero exit is checked
    /// there, not as a run issue.</summary>
    /// <remarks>An unread sandbox (<paramref name="sandboxWrites"/> null) is the run issue <c>blocked</c>
    /// (<c>sandbox-writes-unread</c>): the containment check could not run, so isolation is unproven, like any other missing
    /// evidence; it never compares equal to "no writes". The read's own exception is already <c>cleanup_failed</c> from
    /// closing.</remarks>
    internal static Exception? Conclude(Exception? failure, List<UiRunManifest.Issue> issues, UiRunManifest manifest, string? ending,
        IReadOnlyList<string>? sandboxWrites, IReadOnlyList<string> expectedWrites)
    {
        if (ending?.StartsWith("still running", StringComparison.Ordinal) == true) issues.Add(new("cleanup_failed", ending));
        if (sandboxWrites is null) issues.Add(new("blocked", "sandbox-writes-unread"));
        failure = CleanupBoundary.Attempt(failure, () =>
        {
            Assert.Empty(issues);
            if (manifest.ProductIssues.Count == 0) Assert.Equal("exit 0", ending);
            Assert.Equal(expectedWrites.Order(), (sandboxWrites ?? []).Order());
        }, issues, "blocked");
        if (manifest.ProductIssues.Count != 0)
            failure ??= new InvalidOperationException("product failure: " + string.Join(", ", manifest.ProductIssues.Select(i => i.Type + " (" + i.Reason + ")")));
        return failure;
    }

    private static void RunCore(string name, ITestOutputHelper output, Action<StudioWindow, RunRecord, string> body,
        Func<string, IReadOnlyList<string>> declaredWrites, Action<VideoRecorder?>? outerRecorder = null, string[]? caseArguments = null,
        string resultUse = "exploratory", Action<RunRecord>? recordCreated = null)
    {
        var record = new RunRecord(name);
        string videoId = VideoCoverage.CurrentId(name, caseArguments ?? []);
        recordCreated?.Invoke(record);
        record.Set("purpose", resultUse);
        IReadOnlyList<string> expectedWrites = declaredWrites(record.RunId);
        using var pollingLog = PollingWait.Record(record.Diagnostic);
        StudioProcess? app = null;
        AutomationElement? window = null;
        string result = "failed";
        bool bodyCompleted = false;
        VideoRecorder? video = null;
        var manifest = new UiRunManifest(record.RunId, name);
        var issues = new List<UiRunManifest.Issue>();
        Exception? failure = null;
        try
        {
            app = StudioProcess.Start(record.RunId, name, expectedWrites);
            manifest.RecordPreflight(app.Preflight);
            record.Set("preflight", app.Preflight);
            record.Set("realWorldEffects", app.Preflight.RealWorldEffects);
            record.Set("build", new { exe = app.ExePath, productVersion = app.ProductVersion, exeWritten = File.GetLastWriteTime(app.ExePath) });
            record.Set("process", new { pid = app.Process.Id, started = app.StartedAt, sandbox = app.Sandbox });
            app.Observation.RunUi(() =>
            {
                window = app.MainWindow(TimeSpan.FromSeconds(15));
            }, "launch");
            // Capture setup is bounded and contains no scenario actions; the same recording spans normalisation and body.
            video = VideoRecorder.TryStart(record, app.Process.MainWindowHandle, app.Process.Id, videoId);
            record.Video = video;
            outerRecorder?.Invoke(video);
            app.Observation.RunUi(() =>
            {
                using var pointerScope = PointerBoundary.Enter(window!, app.Policy, app.RequireInputSafe,
                    Observation.OwnedProcessIdentity.From(app.Process), new IntPtr(window!.Current.NativeWindowHandle),
                    outcome => record.RecordPointerRelease(manifest, outcome));
                var ui = new StudioWindow(window!, app.Policy, app.RequireInputSafe) { Log = record.Step };
                ui.Normalise(WindowWidth, WindowHeight);
                record.Set("window", window!.Current.BoundingRectangle.ToString());
                Assert.Equal("Empty", ui.RunState);
                record.Step("window ready, state Empty");
                body(ui, record, app.Sandbox);
                bodyCompleted = true;
            });
        }
        catch (Exception e)
        {
            failure = e;
            if (app?.Observation.HasFailure == true) record.AbortSteps();
            failure = Attempt(failure,
                () => CaptureFailureBounded(app?.Observation, record, e, () => record.CaptureFailure(e, window)), issues, "blocked", app?.Observation);
        }
        finally
        {
            // Pixels stop while the handle still belongs to Studio; publication awaits every final assertion.
            video?.StopCapture();
            failure = Attempt(failure, () => app?.Dispose(), issues, "cleanup_failed", app?.Observation);
            // A launch that failed after its observation existed: its channels and the Documents control still count.
            if (app is null && failure is not null && Observation.RunObservation.From(failure) is { } unstarted)
                unstarted.Record(manifest, issues);
            if (app is not null)
            {
                app.Observation.Record(manifest, issues);
                record.Set("ownedProcessEnding", app.Ending);
                record.Set("sandboxWrites", app.SandboxWrites);
                manifest.Evidence["ownedProcesses"] = app.Ledger.Owned;
                manifest.Evidence["sharedResources"] = new[] { app.LeaseState };
                manifest.Evidence["processIssues"] = app.Ledger.Issues;
                manifest.Evidence["processMonitor"] = new { methods = new[] { "polling-100ms" }, errors = app.MonitorErrors,
                    blindSpots = new[] { "processes shorter than a polling interval; use ETW for qualification" } };
                if (app.SandboxWrites?.Except(expectedWrites).Any() == true) issues.Add(new("unsafe_effect", "Undeclared sandbox writes"));
                foreach (string issue in app.Ledger.Issues) issues.Add(new("unsafe_effect", issue));
                if (app.MonitorErrors.Count != 0) issues.Add(new("blocked", "process-monitor-not-proven"));
            }
            if (app is null) issues.Add(new("blocked", "app-not-started"));
            failure = Conclude(failure, issues, manifest, app?.Ending, app is null ? [] : app.SandboxWrites, expectedWrites);
            result = bodyCompleted && failure is null && issues.Count == 0 ? "passed" : "failed";
            video?.Stop(); // Decode only after exit and channel freeze, outside the product liveness interval.
            manifest.Evidence["videoStatus"] = video?.StatusRecord();
            record.Set("productIssues", manifest.ProductIssues.Select(i => new { type = i.Type, reason = i.Reason }).ToArray());
            manifest.CleanupCompleted(issues);
            failure = CleanupBoundary.Attempt(failure, () => manifest.Save(record.Directory, manifest.Finalise(result, resultUse)), issues, "blocked");
            failure = CleanupBoundary.Attempt(failure, () => output.WriteLine($"manifest: {record.Save(issues.Count == 0 ? result : "failed")}"), issues, "blocked");
            if (outerRecorder is null) video?.Complete(failure is null && result == "passed");
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
