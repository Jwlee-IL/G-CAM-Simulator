using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;
using Gcam.Studio.UiTests.Harness.Observation;

namespace Gcam.Studio.UiTests;

public sealed class RunIntegrationTests
{
    private static readonly OwnedProcessIdentity Identity = new(123, DateTimeOffset.Parse("2026-10-22T00:00:00Z"));
    private static LivenessResult Live(bool breach = false, bool early = false, int code = 0) =>
        new(Identity, 1, Identity.StartedAtUtc, 10, .1, .1, 10, breach ? 10 : 0, breach, breach, early, code,
            code != 0, early ? null : Identity.StartedAtUtc, [], []);

    [Theory]
    [InlineData(true, false, 0, "not_responding")]
    [InlineData(false, true, 0, "app_exited")]
    [InlineData(false, false, 9, "abnormal_exit")]
    public void ProductFailure_FailsTheScenarioWithoutInventingAnIsolationIssue(bool hang, bool early, int code, string issue)
    {
        var manifest = new UiRunManifest("fixture", "case");
        manifest.RecordLiveness(Live(hang, early, code)); manifest.CleanupCompleted([]);
        var result = JsonSerializer.SerializeToElement(manifest.Finalise("passed"), VideoEntry.Json);
        Assert.Equal("completed", result.GetProperty("runStatus").GetString());
        Assert.Equal("failed", result.GetProperty("productStatus").GetString());
        Assert.Equal("failed", result.GetProperty("scenarios")[0].GetProperty("status").GetString());
        Assert.Equal(issue, result.GetProperty("productIssues")[0].GetProperty("type").GetString());
        Assert.Empty(manifest.RunIssues);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void LauncherHandle_SeesAnExitBeforeObserverAttachment(int code)
    {
        using var helper = ObservationFixtures.Helper(code);
        var identity = OwnedProcessIdentity.From(helper);
        ObservationFixtures.Exit(helper);
        using var observer = new LivenessObserver(helper, identity, () => IntPtr.Zero);
        var result = observer.Stop();
        Assert.True(result.EarlyExit); Assert.Equal(code, result.ExitCode);
        Assert.Equal(code != 0, result.AbnormalExit);
        observer.MarkIntentionalClose();
        Assert.True(observer.Snapshot().EarlyExit);
    }

    [Theory]
    [InlineData("missing", "blocked")]
    [InlineData("file", "unsafe_effect")]
    [InlineData("network", "unsafe_effect")]
    [InlineData("control", "completed")]
    public void LiveChannels_NeverClearAHiddenTransientEffectOrAnInstrumentGap(string defect, string expected)
    {
        var manifest = new UiRunManifest("fixture", "case");
        var issues = new List<UiRunManifest.Issue>();
        var docs = new DocumentsWatchResult("synthetic", "pattern", true, true,
            defect is "file" or "control" ? [new(Identity.StartedAtUtc, "delete", "synthetic", null, defect == "control")] : [], []);
        var network = new ConnectionResult(Identity, 1, true,
            [new(Identity.StartedAtUtc, defect == "network" ? [new("udp4", "127.0.0.1", 1, null, null, 0, false)] : [])], []);
        RunObservation.RecordResults(manifest, issues, defect == "missing" ? null : docs, network, Live(), []);
        manifest.CleanupCompleted(issues); manifest.Finalise("passed");
        Assert.Equal(expected, manifest.RunStatus);
    }

    [Fact]
    public void V4Qualification_UsesOnlyItsActualScopeAndGateValues()
    {
        string commit = new('a', 40); var at = Identity.StartedAtUtc;
        var package = new ProbePackage("fixture", commit, at, "i", "d",
            [new("charter", "charter", commit, "Synthetic charter", at), new("go", "desktop-go", commit, "Synthetic go", at)])
        {
            Contract = "v4", Charter = new("charter", "synthetic-ordinary-scope", ["case"],
                [new("G0", "passed", "actual-charter"), new("G1", "blocked", "pending-observed-run")], "approved"),
        };
        Assert.Equal("synthetic-ordinary-scope", package.RequireScenario("case").Scope);
        Assert.Throws<InvalidDataException>(() => package.RequireScenario("other"));
        Assert.Throws<InvalidDataException>(() => (package with { Charter = package.Charter with { ApprovalId = "other" } }).RequireScenario("case"));
        Assert.Throws<InvalidDataException>(() => (package with { Charter = package.Charter with { Status = "draft" } }).RequireScenario("case"));
        // A launch may run while G1 is blocked (the observed run that establishes it); production may not (D7).
        Assert.Throws<InvalidDataException>(package.RequireProductionGates);
        var bothPassed = package with { Charter = package.Charter with { Gates = [new("G0", "passed", "actual-charter"), new("G1", "passed", "observed-run")] } };
        bothPassed.RequireProductionGates();
        Assert.Throws<InvalidDataException>((bothPassed with { Charter = bothPassed.Charter! with { Gates = [new("G0", "passed", "actual-charter")] } }).RequireProductionGates);
        var data = new ContentFingerprint.DataCheck("v4", "i", "d", [], [], false, new("generator", "synthetic", "case"), "case", "assembly");
        var manifest = new UiRunManifest("fixture", "case");
        manifest.RecordPreflight(new("v4", [], data, new("charter-exception", "charter", "reason", "risk", []), "policy", [], new(new(1, "draft", [])), package));
        manifest.CleanupCompleted([]);
        var result = JsonSerializer.SerializeToElement(manifest.Finalise("passed"), VideoEntry.Json);
        Assert.Equal("synthetic-ordinary-scope", result.GetProperty("charter").GetProperty("scope").GetString());
        Assert.Equal("passed", result.GetProperty("executionGates")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task ProductFailure_ReturnsControlWhileTheUiaWorkerIsStillBlocked()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using var left = new ManualResetEventSlim();
        var failure = new TaskCompletionSource<LivenessResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = Task.Run(() => Assert.Throws<ProductLivenessException>(() => RunObservation.RunUntilFailure(() =>
        {
            entered.Set();
            try { release.Wait(); throw new IOException("synthetic late worker failure"); }
            finally { left.Set(); }
        }, failure.Task)));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            failure.SetResult(Live(breach: true));
            var error = await controller.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains("UIA worker did not need to return", error.Message);
            Assert.False(left.IsSet);
        }
        finally { release.Set(); Assert.True(left.Wait(TimeSpan.FromSeconds(5))); }
    }

    [Fact]
    public void UiaWorker_PreservesTheOriginalAssertionFailure()
    {
        var expected = new InvalidDataException("synthetic product assertion");
        var pending = new TaskCompletionSource<LivenessResult>();
        Assert.Same(expected, Assert.Throws<InvalidDataException>(() => RunObservation.RunUntilFailure(() => throw expected, pending.Task)));
    }

    [Fact]
    public void NativeInputGuard_RefusesBeforeDereferencingTheTarget()
    {
        using var scope = PointerBoundary.Enter(null!, new(new(1, "draft", [])), () => throw new InvalidOperationException("fixture guard"));
        var failure = Assert.Throws<InvalidOperationException>(() => ReportExportDialog.PostEnter(null!, IntPtr.Zero, 0, _ => { }));
        Assert.Equal("fixture guard", failure.Message);
    }

    // ---------------------------------------------------------------- turn C2: teardown, startup, Documents control

    private static string SurfaceList => Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation/surface-list.json");
    private static string FreshRunId() => "20261022-000000-" + Guid.NewGuid().ToString("N")[..6];

    /// <summary>An observation attached to a message-only window of this test process (never Studio) with a short limit.</summary>
    private static RunObservation Observe(string documents, LivenessObserverTests.MessageWindow window, Process current, double limitSeconds)
    {
        var observation = new RunObservation(FreshRunId(), documents, SurfaceList);
        observation.Attach(current, OwnedProcessIdentity.From(current), null, () => window.Hwnd, TimeSpan.FromSeconds(limitSeconds));
        Assert.True(SpinWait.SpinUntil(() => observation.WindowSeen, 3000), "the helper window was not found");
        return observation;
    }

    /// <summary>Asks the test's own helper process (standing in for Studio, never Studio) to exit, without waiting.</summary>
    private static void RequestExit(Process helper) { helper.StandardInput.WriteLine(); helper.StandardInput.Flush(); }

    [PwshFact]
    public void CloseSequence_HangWhileClosingTheModalLayer_IsAProductFailureOnly_WithItsPhase_EndingAndNoRunIssue()
    {
        using var documents = ObservationFixtures.Scratch();
        using var window = new LivenessObserverTests.MessageWindow();
        using var current = Process.GetCurrentProcess();
        using var studio = ObservationFixtures.Helper(0);
        var observation = Observe(documents, window, current, 1);
        bool requested = false, markedBeforeRequest = false;
        string? recorded = null, ending;
        try
        {
            var watch = Stopwatch.StartNew();
            ending = OwnedStudioProcess.CloseOwned(observation, studio, true,
                () => { window.Block(); window.Release.Wait(); }, // a UIA call into a UI thread that stopped answering
                () => { requested = true; markedBeforeRequest = observation.HasProductFailure; RequestExit(studio); },
                e => recorded = e);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(8), $"close returned after {watch.Elapsed}");
            Assert.True(requested && markedBeforeRequest, "the close request follows the recorded failure, never precedes it");
        }
        finally { window.Release.Set(); observation.Dispose(); ObservationFixtures.Exit(studio); }
        Assert.Equal("exit 0", ending); Assert.Equal(ending, recorded); // never left at "running"
        var live = observation.Liveness!;
        Assert.True(live.Breached); Assert.Equal("close-modal-layer", live.Phase); Assert.NotNull(live.IntentionalCloseAtUtc);
        var issue = Assert.Single(UiRunManifest.LivenessIssues(live));
        Assert.Equal("not_responding", issue.Type); Assert.Contains("phase=close-modal-layer", issue.Reason);
        // Through the scenario's final checks: a completed run with a failed product, no cleanup or blocked issue.
        var manifest = new UiRunManifest("fixture", "case"); var issues = new List<UiRunManifest.Issue>();
        manifest.RecordLiveness(live);
        Assert.NotNull(Scenario.Conclude(null, issues, manifest, ending, [], []));
        Assert.Empty(issues);
        manifest.CleanupCompleted(issues); manifest.Finalise("failed");
        Assert.Equal("completed", manifest.RunStatus);
    }

    [PwshFact]
    public void CloseSequence_WithoutAHang_MarksTheCloseOnlyAfterTheModalLayer_AndReadsTheEnding()
    {
        using var documents = ObservationFixtures.Scratch();
        using var window = new LivenessObserverTests.MessageWindow();
        using var current = Process.GetCurrentProcess();
        using var studio = ObservationFixtures.Helper(0);
        var observation = Observe(documents, window, current, 1);
        var order = new List<string>();
        try
        {
            string ending = OwnedStudioProcess.CloseOwned(observation, studio, true, () => order.Add("modal"),
                () => { order.Add("close"); RequestExit(studio); });
            Assert.Equal(new[] { "modal", "close" }, order);
            Assert.Equal("exit 0", ending);
        }
        finally { observation.Dispose(); ObservationFixtures.Exit(studio); }
        Assert.True(observation.Liveness!.Passed, string.Join("; ", observation.Liveness.Errors));
        Assert.NotNull(observation.Liveness.IntentionalCloseAtUtc);
    }

    [PwshFact]
    public void CloseSequence_AHarnessErrorStillPropagates_AfterTheCloseRequestAndTheEnding()
    {
        using var documents = ObservationFixtures.Scratch();
        using var window = new LivenessObserverTests.MessageWindow();
        using var current = Process.GetCurrentProcess();
        using var studio = ObservationFixtures.Helper(0);
        var observation = Observe(documents, window, current, 1);
        string? recorded = null;
        try
        {
            Assert.Throws<IOException>(() => OwnedStudioProcess.CloseOwned(observation, studio, true,
                () => throw new IOException("synthetic selector error"), () => RequestExit(studio), e => recorded = e));
        }
        finally { observation.Dispose(); ObservationFixtures.Exit(studio); }
        Assert.Equal("exit 0", recorded);
        Assert.False(observation.HasProductFailure);
    }

    [Fact]
    public void FailureCapture_HangIsNotResponding_WithItsPhase_AndTheOriginalErrorIsKept()
    {
        using var documents = ObservationFixtures.Scratch();
        using var window = new LivenessObserverTests.MessageWindow();
        using var current = Process.GetCurrentProcess();
        var observation = Observe(documents, window, current, 1);
        var record = new RunRecord("failure-capture-fixture");
        try
        {
            var watch = Stopwatch.StartNew();
            Assert.Throws<ProductLivenessException>(() => Scenario.CaptureFailureBounded(observation, record,
                new IOException("synthetic original failure"), () => { window.Block(); window.Release.Wait(); }));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"capture returned after {watch.Elapsed}");
            window.Release.Set(); observation.Dispose();
            Assert.Equal("failure-capture", observation.Liveness!.Phase);
            Assert.Equal("not_responding", Assert.Single(UiRunManifest.LivenessIssues(observation.Liveness)).Type);
            Assert.Contains("synthetic original failure", File.ReadAllText(record.Save("failed")));
        }
        finally { window.Release.Set(); observation.Dispose(); Directory.Delete(record.Directory, recursive: true); }
    }

    [Fact]
    public void Startup_BeforeTheMainWindowIsNotNonResponse_AndNoWindowIsALaunchFailureWithItsOwnCode()
    {
        using var window = new LivenessObserverTests.MessageWindow();
        using var current = Process.GetCurrentProcess();
        IntPtr hwnd = IntPtr.Zero;
        using (var observer = new LivenessObserver(current, OwnedProcessIdentity.From(current), () => hwnd, TimeSpan.FromMilliseconds(300)))
        {
            Thread.Sleep(900); // three limits without a window: the launch wait governs, not the response limit
            var startup = observer.Snapshot();
            Assert.True(startup.Passed); Assert.False(startup.WindowSeen); Assert.Equal(0, startup.LongestNonResponseSeconds);
            Assert.False(observer.Failure.IsCompleted);
            hwnd = window.Hwnd;
            Assert.True(SpinWait.SpinUntil(() => observer.WindowSeen, 3000));
            var running = observer.Snapshot();
            Assert.True(running.Passed); Assert.True(running.StartupSeconds >= 0.8, running.StartupSeconds.ToString());
        }
        using var absent = new LivenessObserver(current, OwnedProcessIdentity.From(current), () => IntPtr.Zero, TimeSpan.FromMilliseconds(300));
        Thread.Sleep(400);
        absent.LaunchFailed(); // the launch wait expired with no window
        Assert.True(SpinWait.SpinUntil(() => absent.Failure.IsCompleted, 3000));
        var result = absent.Stop();
        Assert.True(result.LaunchFailed); Assert.False(result.Breached); Assert.Equal("launch", result.Phase);
        var issue = Assert.Single(UiRunManifest.LivenessIssues(result));
        Assert.Equal("launch_window_timeout", issue.Type); Assert.Contains("main window absent; startup seconds=", issue.Reason);
    }

    [Fact]
    public void DocumentsControl_IsCreatedForTheRun_RemovedAfterIt_AndBothAreRecorded()
    {
        using var documents = ObservationFixtures.Scratch();
        var observation = new RunObservation(FreshRunId(), documents, SurfaceList);
        string folder = observation.ControlDirectory!;
        Assert.True(observation.ControlCreated); Assert.True(Directory.Exists(folder));
        observation.Dispose();
        Assert.True(observation.ControlRemoved); Assert.Null(observation.ControlRemovalError); Assert.False(Directory.Exists(folder));
        Assert.Empty(Directory.EnumerateFileSystemEntries(documents)); // nothing of the run stays in the watched root
        Assert.True(observation.Documents!.Passed, string.Join("; ", observation.Documents.Errors));
        Assert.All(observation.Documents.Events, e => Assert.True(e.HarnessControl, e.Operation + " " + e.Path));
        var manifest = new UiRunManifest("fixture", "case"); var issues = new List<UiRunManifest.Issue>();
        observation.Record(manifest, issues);
        var control = JsonSerializer.SerializeToElement(manifest.Evidence["documentsControl"]);
        Assert.True(control.GetProperty("created").GetBoolean()); Assert.True(control.GetProperty("removed").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, control.GetProperty("createdAtUtc").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, control.GetProperty("removedAtUtc").ValueKind);
        Assert.DoesNotContain(issues, i => i.Type == "cleanup_failed");
    }

    [Fact]
    public void DocumentsControl_NeverRemovesWhatTheRunDidNotCreate_AndAFailedRemovalIsARunIssue()
    {
        using var documents = ObservationFixtures.Scratch();
        var observation = new RunObservation(FreshRunId(), documents, SurfaceList);
        string stranger = Path.Combine(observation.ControlDirectory!, "not-created-by-this-run.txt");
        File.WriteAllText(stranger, "x");
        observation.Dispose();
        Assert.False(observation.ControlRemoved); Assert.NotNull(observation.ControlRemovalError);
        Assert.True(File.Exists(stranger)); // non-recursive: the unexpected file and its folder stay
        Assert.Empty(Directory.EnumerateFiles(observation.ControlDirectory!, "gcam-report-*")); // the run's own control went
        var issues = new List<UiRunManifest.Issue>();
        observation.Record(new UiRunManifest("fixture", "case"), issues);
        Assert.Contains(issues, i => i.Type == "cleanup_failed" && i.Reason.StartsWith("documents-control-removal-failed", StringComparison.Ordinal));

        string runId = FreshRunId(), existing = Path.Combine(documents, ".gcam-uia-" + runId);
        Directory.CreateDirectory(existing); File.WriteAllText(Path.Combine(existing, "gcam-report-20000101-000000.html"), "kept");
        var refused = new RunObservation(runId, documents, SurfaceList);
        Assert.Null(refused.ControlDirectory); Assert.False(refused.ControlCreated); // an existing folder is never adopted
        refused.Dispose();
        Assert.True(File.Exists(Path.Combine(existing, "gcam-report-20000101-000000.html")));
        Assert.Null(refused.ControlRemovalError);
    }

    [Fact]
    public void InputGuard_NoScopeRefuses_AndAfterAProductFailureNoFurtherInputIsSent()
    {
        Assert.Contains("No owned input scope", Assert.Throws<InvalidOperationException>(
            () => ReportExportDialog.SetFileName(null!, "x", _ => { }, s => s)).Message);
        Assert.Contains("No owned input scope", Assert.Throws<InvalidOperationException>(
            () => ReportExportDialog.PostEnter(null!, IntPtr.Zero, 0, _ => { })).Message);
        using var documents = ObservationFixtures.Scratch();
        using var window = new LivenessObserverTests.MessageWindow();
        using var current = Process.GetCurrentProcess();
        var observation = Observe(documents, window, current, 0.3);
        try
        {
            window.Block();
            Assert.True(observation.HasFailure || SpinWait.SpinUntil(() => observation.HasFailure, 3000));
            window.Release.Set(); // the window answers again; the refusal stays
            Thread.Sleep(300);
            using var scope = PointerBoundary.Enter(null!, new(new(1, "draft", [])), observation.RequireResponsive);
            bool sent = false;
            Assert.Contains("product-liveness-failure", Assert.Throws<ProductLivenessException>(() => PointerBoundary.Input(() => sent = true)).Message);
            Assert.False(sent);
            Assert.Throws<ProductLivenessException>(() => ReportExportDialog.SetFileName(null!, "x", _ => { }, s => s));
        }
        finally { window.Release.Set(); observation.Dispose(); }
    }

    // ---------------------------------------------------------------- turn C3: product axis, input after failure

    [Theory]
    [InlineData("hang", "exit 0")]
    [InlineData("abnormal", "exit 9")]
    [InlineData("early", "exit 0")]
    public void ProductFailure_ThroughTheScenarioFinalChecks_IsNeverABlockedOrCleanupRunIssue(string kind, string ending)
    {
        var manifest = new UiRunManifest("fixture", "case"); var issues = new List<UiRunManifest.Issue>();
        manifest.RecordLiveness(kind switch { "hang" => Live(breach: true), "abnormal" => Live(code: 9), _ => Live(early: true) });
        var failure = Scenario.Conclude(null, issues, manifest, ending, ["report.html"], ["report.html"]);
        Assert.Contains("product failure", failure!.Message);
        Assert.Empty(issues);
        manifest.CleanupCompleted(issues);
        var result = JsonSerializer.SerializeToElement(manifest.Finalise("failed"), VideoEntry.Json);
        Assert.Equal("completed", result.GetProperty("runStatus").GetString());
        Assert.Equal("failed", result.GetProperty("productStatus").GetString());
    }

    [Fact]
    public void ScenarioFinalChecks_StillCountRealRunIssues()
    {
        var manifest = new UiRunManifest("fixture", "case"); var issues = new List<UiRunManifest.Issue>();
        Assert.NotNull(Scenario.Conclude(null, issues, manifest, OwnedStudioProcess.StillRunningAfterClose, [], []));
        Assert.Contains(issues, i => i.Type == "cleanup_failed");
        issues.Clear();
        Assert.NotNull(Scenario.Conclude(null, issues, manifest, "exit 9", [], [])); // no product record of it: not proven
        Assert.Contains(issues, i => i.Type == "blocked");
        issues.Clear();
        Assert.NotNull(Scenario.Conclude(null, issues, manifest, "exit 0", ["undeclared.txt"], []));
        Assert.Contains(issues, i => i.Type == "blocked");
        issues.Clear();
        Assert.Null(Scenario.Conclude(null, issues, manifest, "exit 0", [], []));
        Assert.Empty(issues);
    }

    [Fact]
    public void CleanupStep_FailingAfterAProductFailure_KeepsTheOriginalFailure_AndAddsNoRunIssue()
    {
        using var documents = ObservationFixtures.Scratch();
        using var window = new LivenessObserverTests.MessageWindow();
        using var current = Process.GetCurrentProcess();
        var observation = Observe(documents, window, current, 0.3);
        try
        {
            var issues = new List<UiRunManifest.Issue>();
            var original = new IOException("synthetic original failure");
            Assert.Same(original, Scenario.Attempt(original, () => throw new InvalidOperationException("before"), issues, "blocked", observation));
            Assert.Single(issues); issues.Clear(); // no product failure yet: an ordinary run issue
            window.Block();
            Assert.True(SpinWait.SpinUntil(() => observation.HasProductFailure, 3000));
            Assert.Same(original, Scenario.Attempt(original, () => throw new ProductLivenessException("product-liveness-failure"), issues, "blocked", observation));
            Assert.Empty(issues);
        }
        finally { window.Release.Set(); observation.Dispose(); }
    }

    [Fact]
    public void AfterAProductFailure_SandboxEnumerationLeaseAndMonitorFailures_StayRunIssues()
    {
        using var documents = ObservationFixtures.Scratch();
        using var window = new LivenessObserverTests.MessageWindow();
        using var current = Process.GetCurrentProcess();
        var observation = Observe(documents, window, current, 0.3);
        try
        {
            window.Block();
            Assert.True(SpinWait.SpinUntil(() => observation.HasProductFailure, 3000));
            window.Release.Set(); observation.Dispose(); // frozen: the product failure stays recorded
            Assert.True(observation.HasProductFailure);
            var original = new ProductLivenessException("product-liveness-failure");
            var issues = new List<UiRunManifest.Issue>();
            // Closing (the scenario's Dispose step): every exception but the product refusal stays cleanup_failed.
            foreach (var cleanup in new Exception[] { new UnauthorizedAccessException("synthetic: sandbox folder cannot be listed"),
                new IOException("synthetic: lease release failed"), new ObjectDisposedException("synthetic process monitor"),
                new InvalidOperationException("Process instance ownership changed before close") })
                Assert.Same(original, Scenario.Attempt(original, () => throw cleanup, issues, "cleanup_failed", observation));
            Assert.Equal(4, issues.Count(i => i.Type == "cleanup_failed"));
            // The unread sandbox never compares equal to "no writes": blocked, and the run is not completed.
            var manifest = new UiRunManifest("fixture", "case");
            manifest.RecordLiveness(observation.Liveness);
            Assert.NotNull(Scenario.Conclude(original, issues, manifest, "exit 0", null, []));
            Assert.Contains(issues, i => i.Type == "blocked" && i.Reason == "sandbox-writes-unread");
            manifest.CleanupCompleted(issues);
            var result = JsonSerializer.SerializeToElement(manifest.Finalise("failed"), VideoEntry.Json);
            Assert.NotEqual("completed", result.GetProperty("runStatus").GetString());
            Assert.Equal("cleanup_failed", result.GetProperty("runStatus").GetString());
        }
        finally { window.Release.Set(); observation.Dispose(); }
    }

    [Fact]
    public void UnreadSandbox_WithoutAnyProductFailure_IsBlocked_NeverNoWrites()
    {
        var manifest = new UiRunManifest("fixture", "case"); var issues = new List<UiRunManifest.Issue>();
        Assert.NotNull(Scenario.Conclude(null, issues, manifest, "exit 0", null, []));
        Assert.Contains(issues, i => i.Type == "blocked" && i.Reason == "sandbox-writes-unread");
        manifest.CleanupCompleted(issues); manifest.Finalise("passed");
        Assert.Equal("blocked", manifest.RunStatus);
    }

    [Fact]
    public void InstrumentErrorAlone_RefusesInput_ButIsNeitherAProductFailureNorExcused()
    {
        using var documents = ObservationFixtures.Scratch();
        using var current = Process.GetCurrentProcess();
        var observation = new RunObservation(FreshRunId(), documents, SurfaceList);
        try
        {
            observation.Attach(current, OwnedProcessIdentity.From(current), null,
                () => throw new IOException("synthetic window lookup error"), TimeSpan.FromSeconds(1));
            Assert.True(SpinWait.SpinUntil(() => observation.HasFailure, 3000));
            Assert.False(observation.HasProductFailure);
            var refusal = Assert.Throws<InvalidOperationException>(observation.RequireResponsive); // not the product type
            Assert.Contains("liveness-instrument-failure", refusal.Message);
            var issues = new List<UiRunManifest.Issue>();
            Scenario.Attempt(null, () => throw refusal, issues, "blocked", observation);
            Assert.Single(issues);
        }
        finally { observation.Dispose(); }
        Assert.Empty(UiRunManifest.LivenessIssues(observation.Liveness));
        Assert.NotEmpty(observation.Liveness!.Errors);
    }

    [Fact]
    public void LaunchFailure_AfterTheObservationExisted_StillRecordsTheDocumentsControlAndChannels()
    {
        using var documents = ObservationFixtures.Scratch();
        var observation = new RunObservation(FreshRunId(), documents, SurfaceList);
        var failure = new InvalidOperationException("synthetic launch failure");
        observation.Dispose(); observation.AttachTo(failure); // what OwnedStudioProcess.Start does on its failure path
        var manifest = new UiRunManifest("fixture", "case"); var issues = new List<UiRunManifest.Issue>();
        Assert.Same(observation, RunObservation.From(failure));
        RunObservation.From(failure)!.Record(manifest, issues);
        var control = JsonSerializer.SerializeToElement(manifest.Evidence["documentsControl"]);
        Assert.True(control.GetProperty("created").GetBoolean()); Assert.True(control.GetProperty("removed").GetBoolean());
        Assert.True(manifest.Evidence.ContainsKey("liveChannels"));
        Assert.Contains(issues, i => i.Reason == "live-channel-gap"); // no liveness instrument was attached
        Assert.Null(RunObservation.From(new InvalidOperationException("no observation")));
    }

    [Fact]
    public void RawInput_GuardTrippingDuringTheUiaCall_SendsNothing()
    {
        bool failed = false, sent = false;
        Action guard = () => { if (failed) throw new InvalidOperationException("product-liveness-failure; further input refused"); };
        Assert.Throws<InvalidOperationException>(() => StudioWindow.Act(guard, () => { failed = true; return "target"; }, _ => sent = true));
        Assert.False(sent);
        failed = false;
        StudioWindow.Act(guard, () => "target", _ => sent = true);
        Assert.True(sent);
    }

    private sealed class RecordingInput(Action<string> onCall) : Pointer.IRawInput
    {
        public readonly List<string> Sent = [];
        public void Check(Point p) => onCall("check");
        public void SetCursor(int x, int y) { onCall("cursor"); Sent.Add("cursor"); }
        public void Button(uint flags) { onCall("button"); Sent.Add("button-" + flags); }
    }

    [Theory]
    [InlineData("check", 0)]   // the boundary check's UIA reads outlast the limit: no move, no button
    [InlineData("cursor", 1)]  // failure while the cursor moves: no button press
    public void PointerClick_GuardTrippingBetweenSteps_SendsNoFurtherInput(string tripAt, int sentBefore)
    {
        bool failed = false;
        var raw = new RecordingInput(step => { if (step == tripAt) failed = true; });
        using var scope = PointerBoundary.Enter(null!, new(new(1, "draft", [])),
            () => { if (failed) throw new InvalidOperationException("product-liveness-failure; further input refused"); });
        Assert.Throws<InvalidOperationException>(() => Pointer.Click(new Point(10, 10), raw));
        Assert.Equal(sentBefore, raw.Sent.Count);
        Assert.DoesNotContain(raw.Sent, s => s.StartsWith("button", StringComparison.Ordinal));
    }

    [Fact]
    public void PointerDrag_GuardTrippingMidDrag_SendsNoFurtherMoveOrRelease()
    {
        bool failed = false; int moves = 0;
        var raw = new RecordingInput(step => { if (step == "cursor" && ++moves == 3) failed = true; });
        using var scope = PointerBoundary.Enter(null!, new(new(1, "draft", [])),
            () => { if (failed) throw new InvalidOperationException("product-liveness-failure; further input refused"); });
        Assert.Throws<InvalidOperationException>(() => Pointer.Drag(new Point(0, 0), new Point(100, 0), 12, raw));
        Assert.Equal(new[] { "cursor", "button-2", "cursor", "cursor" }, raw.Sent); // press, two moves, then nothing
    }

    [PwshFact]
    public void WindowLookupFailingBecauseTheProcessExited_IsTheExit_NotAChannelGap()
    {
        using var helper = ObservationFixtures.Helper(7);
        var identity = OwnedProcessIdentity.From(helper);
        using var observer = new LivenessObserver(helper, identity, () =>
        {
            ObservationFixtures.Exit(helper); // the product exits between the exit check and the lookup
            throw new InvalidOperationException("No process is associated with this object.");
        });
        Assert.True(SpinWait.SpinUntil(() => observer.Failure.IsCompleted, 10000));
        var result = observer.Stop();
        Assert.True(result.EarlyExit); Assert.True(result.AbnormalExit); Assert.Equal(7, result.ExitCode);
        Assert.Empty(result.Errors);
    }

    // ---------------------------------------------------------------- turn C2: C# records read by the Python gates

    private static readonly Lazy<string> Python = new(() =>
        PythonInterpreter.Resolve(PythonInterpreter.Execute, Environment.GetEnvironmentVariable("GCAM_TEST_PYTHON")));

    /// <summary>Feeds a run record path to the desktop collector's real per-case reader (a synthetic TRX line naming it).</summary>
    private const string CollectorGate = """
        import sys, xml.etree.ElementTree as ET
        sys.path.insert(0, sys.argv[1])
        import test_records as tr
        selector = sorted(s for s in tr.desktop_selectors() if '.ScenarioTests.' in s)[0]
        unit = selector.rsplit('.', 1)[0]
        ns = '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
        root = ET.Element(ns + 'TestRun')
        result = ET.SubElement(ET.SubElement(root, ns + 'Results'), ns + 'UnitTestResult', testId='1')
        ET.SubElement(ET.SubElement(result, ns + 'Output'), ns + 'StdOut').text = 'manifest: ' + sys.argv[2]
        case = {'selector': selector, 'unit': unit, 'id': selector, 'adapterId': '1', 'verdict': 'passed'}
        evidence = tr.collect_desktop_evidence(root, [case], {unit: 'source'})
        print('accepted', evidence[selector]['runId'])
        """;

    private sealed class EncoderSink(string path) : VideoRecorder.ISink
    {
        private readonly MediaFoundationWriter _writer = new(path, 64, 48);
        public void Write(byte[] pixels, long time100ns, long duration100ns) => _writer.Write(pixels, time100ns, duration100ns);
        public void Finish() => _writer.Finish();
        public string KeyFrameForcing => _writer.KeyFrameForcing;
        public void Dispose() => _writer.Dispose();
    }

    /// <summary>A real run record (RunRecord.Save) with a real recording and its real VideoCheckResult. A step after the
    /// recording stopped makes the check fail for real (step coverage).</summary>
    private static RunRecord RecordWithVideo(bool lateStep, out VideoRecorder video)
    {
        var record = new RunRecord("cross-language-fixture");
        int frames = 0;
        video = new VideoRecorder(record.Directory, "cross-language", record.RunId, 64, 48, privacy =>
        {
            var bytes = new byte[64 * 48 * 4];
            for (int y = 10; y < 35; y++) for (int x = 10; x < 35; x++) bytes[(y * 64 + x) * 4 + 1] = 220;
            Interlocked.Increment(ref frames);
            return new(bytes, [], [], privacy);
        }, () => new EncoderSink(Path.Combine(record.Directory, "attempt.mp4")), new('a', 40), false, new('b', 64), new('c', 64));
        record.Video = video;
        record.Step("window ready, state Empty");
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref frames) >= 4, 5000));
        record.Step("synthetic scenario action");
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref frames) >= 8, 5000));
        Assert.NotNull(video.Stop());
        if (lateStep) record.Step("a step after the recording stopped");
        return record;
    }

    [MediaFoundationFact]
    public void RunRecordSave_WithARealPassingVideoCheck_IsAcceptedByThePythonGate_AndARealFailingOneIsRefused()
    {
        var passing = RecordWithVideo(false, out var passVideo);
        var failing = RecordWithVideo(true, out var failVideo);
        try
        {
            Assert.True(passVideo.Check!.Passed, string.Join(", ", passVideo.Check.FailedCriteria));
            Assert.False(failVideo.Check!.Passed); Assert.Contains("step-coverage", failVideo.Check.FailedCriteria);
            string passPath = passing.Save("passed"), failPath = failing.Save("passed");
            // The embedded status has the video-status.json shape; the record's own historical values stay as they were.
            using (var doc = JsonDocument.Parse(File.ReadAllBytes(passPath)))
            {
                var status = doc.RootElement.GetProperty("videoStatus");
                Assert.True(status.GetProperty("check").GetProperty("passed").GetBoolean());
                Assert.Equal(passing.ExecutedStepIds, status.GetProperty("evidence").GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("id").GetString()));
                Assert.True(doc.RootElement.GetProperty("machine").TryGetProperty("perMonitorDpiAware", out _));
            }
            string testing = Path.Combine(RepoPaths.Root(), "samples", "testing");
            var accepted = PythonInterpreter.Execute(Python.Value, ["-B", "-c", CollectorGate, testing, passPath]);
            Assert.True(accepted.ExitCode == 0, accepted.Error);
            Assert.Equal("accepted " + passing.RunId, accepted.Output.Trim());
            var refused = PythonInterpreter.Execute(Python.Value, ["-B", "-c", CollectorGate, testing, failPath]);
            Assert.NotEqual(0, refused.ExitCode);
            Assert.Contains("video-gate: video check did not pass", refused.Error);
        }
        finally
        {
            passVideo.Complete(false); failVideo.Complete(false); // never publishes into the repository's video folder
            Directory.Delete(passing.Directory, recursive: true); Directory.Delete(failing.Directory, recursive: true);
        }
    }

    [Fact]
    public void DesktopSelectors_OfTheCollector_AreExactlyTheMethodsCarryingTheDesktopAttributes()
    {
        var expected = VideoCoverage.DesktopMethods.Select(m => "Gcam.Studio.UiTests::" + m.DeclaringType!.FullName + "." + m.Name)
            .Distinct().Order(StringComparer.Ordinal).ToArray();
        const string code = "import sys\nsys.path.insert(0, sys.argv[1])\nimport test_records as tr\nprint('\\n'.join(sorted(tr.desktop_selectors())))";
        var result = PythonInterpreter.Execute(Python.Value, ["-B", "-c", code, Path.Combine(RepoPaths.Root(), "samples", "testing")]);
        Assert.True(result.ExitCode == 0, result.Error);
        var actual = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
        Assert.DoesNotContain(actual, s => s.Contains(".ResultCacheTests.", StringComparison.Ordinal));
    }
}
