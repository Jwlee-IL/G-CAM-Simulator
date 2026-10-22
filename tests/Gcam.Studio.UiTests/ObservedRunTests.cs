using System.Diagnostics;
using System.Text.Json;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;
using Gcam.Studio.UiTests.Harness.Observation;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>
/// Observed launch-idle-exit run (MR8): snapshot A, a null window, snapshot B, marker, launch, idle, graceful close,
/// snapshot C. No elevation, no trace session, no UIA discovery, pointer input, normalisation or tree dump.
/// </summary>
public sealed class ObservedRunTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(5);
    // At least the Studio window: the idle time, launch and close (a few seconds) and the window margins (-1 s / +2 s).
    private static readonly TimeSpan NullWindow = TimeSpan.FromSeconds(12);

    [DesktopFact(probe: true)]
    public void LaunchIdleExit_RecordsPendingChannelEvidence_WithoutAutomation()
    {
        string runId = Environment.GetEnvironmentVariable("GCAM_UI_PROBE_RUN") ?? throw new InvalidOperationException("Missing probe run ID");
        string runDirectory = Environment.GetEnvironmentVariable("GCAM_UI_PROBE_DIRECTORY") ?? throw new InvalidOperationException("Missing probe directory");
        string scratch = Path.Combine(Path.GetTempPath(), "gcam-todo43");
        if (!Path.GetFullPath(runDirectory).StartsWith(Path.GetFullPath(scratch) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Probe output must be under the declared scratch root");
        var package = ProbePackage.Load(IsolationPaths.Inside(runDirectory, "approvals.json"));
        string isolation = Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation");
        var source = SourceIdentity.Read(RepoPaths.Root());
        package.ValidateSource(source.Commit, source.CommittedAt);
        package.Validate(runId, IsolationPaths.Fingerprint(isolation), ProbePackage.DeploymentFingerprint(RepoPaths.StudioExe()));
        var manifest = new UiRunManifest(runId, nameof(LaunchIdleExit_RecordsPendingChannelEvidence_WithoutAutomation));
        manifest.RecordApprovedProbe(package);
        using var host = Process.GetCurrentProcess();
        string sandbox = IsolationPaths.RunRoot(Path.GetTempPath(), runId);
        string runFolder = Path.GetFullPath(runDirectory);
        string markerKey = "HKCU\\" + RegistryMarker.RelativeRoot + "\\" + runId;
        string[] ownedRoots = [sandbox, runFolder, OwnedStudioProcess.LeasePath(host.SessionId)];
        var roots = StateSnapshot.ObservedRoots(Path.GetDirectoryName(RepoPaths.StudioExe())!, sandbox, runFolder);
        var registry = new WindowsRegistrySource();
        var snapshots = new List<StateSnapshot.Counts>();
        // The run ID is the harness marker: a registry value whose data carries it is flagged at snapshot time (TODO-52).
        StateSnapshot.Counts Snapshot(string label) =>
            StateSnapshot.Take(label, IsolationPaths.Inside(runDirectory, $"snapshot-{label}.jsonl.gz"), roots, registry, [runId]);
        var issues = new List<UiRunManifest.Issue>();
        OwnedStudioProcess? app = null;
        RegistryMarker.Control? registryControl = null;
        Exception? error = null;
        DateTimeOffset nullStarted = default, nullFinished = default, launchAt = default, finished = DateTimeOffset.UtcNow;
        try
        {
            snapshots.Add(Snapshot("A"));
            nullStarted = DateTimeOffset.UtcNow;
            Thread.Sleep(NullWindow); // nothing launched: what changes here is other actors' background activity
            nullFinished = DateTimeOffset.UtcNow;
            snapshots.Add(Snapshot("B"));
            // The actor is this test host, before any product launch.
            registryControl = new RegistryMarker(new WindowsRegistryMarkerStore()).Write(runId, Environment.ProcessId);
            launchAt = DateTimeOffset.UtcNow;
            app = OwnedStudioProcess.Start(runId, scenario: nameof(LaunchIdleExit_RecordsPendingChannelEvidence_WithoutAutomation), package: package);
            if (app.Observation.ControlDirectory is { } control) ownedRoots = [.. ownedRoots, control];
            manifest.RecordPreflight(app.Preflight);
            File.WriteAllText(IsolationPaths.Inside(app.Sandbox, "probe-diagnostic.txt"), runId + "\n");
            // Idle observation is time-bounded and deliberately does not ask UI Automation for a tree/window.
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < Idle)
            {
                Assert.False(app.Process.HasExited, "Studio exited during the idle observation");
                Thread.Sleep(100);
            }
        }
        catch (Exception e) { error = e; issues.Add(new("blocked", e.GetType().Name)); }
        finally
        {
            try { app?.CloseWithoutAutomation(); }
            catch (Exception e) { error ??= e; issues.Add(new("cleanup_failed", e.GetType().Name)); }
            finished = DateTimeOffset.UtcNow;
            if (snapshots.Count == 2) error = CleanupBoundary.Attempt(error, () => snapshots.Add(Snapshot("C")), issues, "blocked");
            // A launch that failed after its observation existed: its channels and the Documents control still count.
            if (app is null && error is not null && RunObservation.From(error) is { } unstarted) unstarted.Record(manifest, issues);
            if (app is not null)
            {
                app.Observation.Record(manifest, issues);
                if (manifest.ProductIssues.Count != 0) error ??= new InvalidOperationException("Observed product failure");
                if (app.Ending.StartsWith("still running", StringComparison.Ordinal)) issues.Add(new("cleanup_failed", app.Ending));
                if (app.SandboxWrites is null) issues.Add(new("blocked", "sandbox-writes-unread"));
                else if (app.SandboxWrites.Count != 0) issues.Add(new("unsafe_effect", "undeclared-sandbox-write"));
                foreach (string issue in app.Ledger.Issues) issues.Add(new("unsafe_effect", issue));
                if (app.MonitorErrors.Count != 0) issues.Add(new("blocked", "process-monitor-not-proven"));
                manifest.Evidence["ownedProcesses"] = app.Ledger.Owned;
                manifest.Evidence["processIssues"] = app.Ledger.Issues;
                manifest.Evidence["sharedResources"] = new[] { app.LeaseState };
                manifest.Evidence["processMonitor"] = new { methods = new[] { "polling-100ms" }, errors = app.MonitorErrors };
                manifest.Evidence["ownedRoot"] = app.Sandbox;
                // Kept for the optional diagnostic trace classifier (stage0.py classify); kernel events name files by NT
                // device path, so owned roots are recorded in both forms (RC-08). Not used by diff-classify.
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var traceContext = new
                {
                    contract = "v4", runId, runDirectory = runFolder,
                    targetCommit = package.Commit, committedAt = package.CommittedAt, deploymentSha256 = package.DeploymentSha256,
                    realWorldEffects = app.Preflight.RealWorldEffects,
                    scenarios = new[] { new { runId, scenarioId = nameof(LaunchIdleExit_RecordsPendingChannelEvidence_WithoutAutomation) } },
                    uiManifests = new[] { new { runId, path = "ui-manifest.json" } },
                    ownedProcesses = app.Ledger.Owned.Select(p => new { pid = p.Pid, parentPid = p.ParentPid, startedAt = p.StartedAt.ToUniversalTime().ToString("O"), finishedAt = finished.ToString("O") }),
                    harnessPids = new[] { Environment.ProcessId }, osPids = Array.Empty<int>(), ownedRoots,
                    ownedRootDevicePaths = ownedRoots.Select(root => IsolationPaths.DeviceForm(root)).ToArray(),
                    knownFolders = new Dictionary<string, object>
                    {
                        ["ProgramData"] = new { path = programData, devicePath = IsolationPaths.DeviceForm(programData) },
                        ["APPDATA"] = new { path = roots.Single(r => r.Category == "APPDATA").Path,
                            devicePath = IsolationPaths.DeviceForm(roots.Single(r => r.Category == "APPDATA").Path) },
                    },
                    requiredChannels = new[] { "file", "process", "registry" },
                    liveChannels = manifest.Evidence.GetValueOrDefault("liveChannels"),
                    traceHealth = new { complete = false, eventsLost = (int?)null, buffersLost = (int?)null },
                    positiveControls = new object?[] {
                        new { channel = "file", pid = Environment.ProcessId, token = "probe-diagnostic.txt" },
                        new { channel = "process", pid = app.Ledger.Owned[0].Pid, token = "Gcam.Studio" }, registryControl },
                };
                error = CleanupBoundary.Attempt(error, () => File.WriteAllText(IsolationPaths.Inside(runDirectory, "trace-context.json"), JsonSerializer.Serialize(traceContext,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + "\n"), issues, "blocked");
            }
            // Input of stage0.py diff-classify; it names machine paths and stays local with the run folder (A4).
            var observation = new
            {
                schemaVersion = 1, contract = "v4", runId, method = "MR8 before/after snapshots",
                targetCommit = package.Commit, committedAt = package.CommittedAt, deploymentSha256 = package.DeploymentSha256,
                realWorldEffects = app?.Preflight.RealWorldEffects ?? [],
                scenarios = new[] { new { runId, scenarioId = nameof(LaunchIdleExit_RecordsPendingChannelEvidence_WithoutAutomation) } },
                uiManifests = new[] { new { runId, path = "ui-manifest.json" } },
                nullWindow = new { startedAt = nullStarted.ToString("O"), finishedAt = nullFinished.ToString("O"), seconds = (nullFinished - nullStarted).TotalSeconds },
                studioWindow = new { launched = app is not null, launchAt = launchAt.ToString("O"), exitAt = finished.ToString("O"),
                    seconds = (finished - launchAt).TotalSeconds, marginBeforeSeconds = 1, marginAfterSeconds = 2 },
                snapshots,
                ownedProcesses = app?.Ledger.Owned.Select(p => new { pid = p.Pid, parentPid = p.ParentPid, image = Path.GetFileName(p.ImagePath),
                    startedAt = p.StartedAt.ToUniversalTime().ToString("O"), finishedAt = finished.ToString("O"), ending = app.Ending }).ToArray() ?? [],
                processIssues = app?.Ledger.Issues.ToArray() ?? [],
                processMonitorErrors = app?.MonitorErrors.ToArray() ?? [],
                harnessOwned = new { files = ownedRoots, registry = new[] { markerKey } },
                positiveControls = new
                {
                    file = Path.Combine(sandbox, "probe-diagnostic.txt"),
                    registry = new { key = markerKey, valueName = RegistryMarker.ValueName, written = registryControl is not null },
                    process = "Gcam.Studio.exe",
                },
                liveChannels = manifest.Evidence.GetValueOrDefault("liveChannels"),
            };
            error = CleanupBoundary.Attempt(error, () => File.WriteAllText(IsolationPaths.Inside(runDirectory, "observation-context.json"),
                JsonSerializer.Serialize(observation, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + "\n"), issues, "blocked");
            manifest.WriteAudit = new { mode = "launch-idle-exit", method = "MR8 before/after snapshots", reused = false, scenarioIds = Array.Empty<string>(),
                positiveControls = new object[] { new { channel = "file", seen = false }, new { channel = "process", seen = false },
                    new { channel = "registry", seen = false, written = registryControl is not null, declaration = registryControl, actor = "harness" } },
                channelApplicability = new[] { ProbeChannels.Network },
                reason = "stage0.py diff-classify of the snapshots required before any G1 promotion",
                // v3.3 (TODO-52): candidates are changes in this scope, marker items and owned processes; the rest is unattributed.
                observationScope = ObservationScope.Describe(isolation, ownedRoots, [markerKey], ["Gcam.Studio.exe"]),
                unattributed = ObservationScope.PendingUnattributed(),
                controlWindow = ObservationScope.ControlWindow(nullFinished - nullStarted, finished - launchAt),
                blindSpots = new[] { "locations outside the observation scope: an app effect there without a harness marker is unattributed (no per-process write record, D2)",
                    "locations outside the snapshot roots", "a harness marker inside file content (files are matched by name only)",
                    "a content change that restores size and last-write time", "a write undone before snapshot C",
                    "100 ms process polling misses short-lived processes" }.Concat(ObservationScope.BlindSpots(isolation)).ToArray(),
                validFor = new { productBuild = package.DeploymentSha256, isolationContent = package.IsolationSha256,
                    pathRules = IsolationPaths.Hash(File.ReadAllBytes(Path.Combine(isolation, "surface-list.json"))),
                    registeredImages = new[] { "Gcam.Studio.exe" } } };
            manifest.CleanupCompleted(issues);
            error = CleanupBoundary.Attempt(error, () => manifest.Save(runDirectory, manifest.Finalise(error is null ? "passed" : "failed", observed: true)), issues, "blocked");
            error = CleanupBoundary.Attempt(error, () => File.WriteAllText(IsolationPaths.Inside(runDirectory, "harness.complete.json"), JsonSerializer.Serialize(new { runId, finishedAt = finished, result = error is null ? "passed" : "failed" }) + "\n"), issues, "blocked");
        }
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        output.WriteLine("Probe actions completed; G1 remains blocked until stage0.py diff-classify has classified the snapshots.");
        Assert.Empty(issues);
    }
}
