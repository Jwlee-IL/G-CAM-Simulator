using System.Diagnostics;
using System.Text.Json;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>First safety probe. No UIA discovery, pointer input, normalisation or tree dump.</summary>
public sealed class ObservedRunTests(ITestOutputHelper output)
{
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
        var ready = IsolationPaths.Inside(runDirectory, "trace.ready.json");
        if (!File.Exists(ready)) throw new InvalidOperationException("Elevated trace wrapper is not ready");
        using (var doc = JsonDocument.Parse(File.ReadAllText(ready)))
            Assert.Equal(runId, doc.RootElement.GetProperty("runId").GetString());
        var manifest = new UiRunManifest(runId, nameof(LaunchIdleExit_RecordsPendingChannelEvidence_WithoutAutomation));
        manifest.RecordApprovedProbe(package);
        var issues = new List<UiRunManifest.Issue>();
        OwnedStudioProcess? app = null;
        Exception? error = null;
        DateTimeOffset finished = DateTimeOffset.UtcNow;
        try
        {
            app = OwnedStudioProcess.Start(runId);
            File.WriteAllText(IsolationPaths.Inside(app.Sandbox, "probe-diagnostic.txt"), runId + "\n");
            // Idle observation is time-bounded and deliberately does not ask UI Automation for a tree/window.
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(5))
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
            if (app is not null)
            {
                if (app.Ending != "exit 0") issues.Add(new("cleanup_failed", app.Ending));
                if (app.SandboxWrites.Count != 0) issues.Add(new("unsafe_effect", "undeclared-sandbox-write"));
                foreach (string issue in app.Ledger.Issues) issues.Add(new("unsafe_effect", issue));
                if (app.MonitorErrors.Count != 0) issues.Add(new("blocked", "process-monitor-not-proven"));
                manifest.Evidence["ownedProcesses"] = app.Ledger.Owned;
                manifest.Evidence["sharedResources"] = new[] { app.LeaseState };
                manifest.Evidence["processMonitor"] = new { methods = new[] { "polling-100ms" }, errors = app.MonitorErrors };
                manifest.Evidence["ownedRoot"] = app.Sandbox;
                var context = new
                {
                    ownedProcesses = app.Ledger.Owned.Select(p => new { pid = p.Pid, parentPid = p.ParentPid, startedAt = p.StartedAt.ToUniversalTime().ToString("O"), finishedAt = finished.ToString("O") }),
                    harnessPids = new[] { Environment.ProcessId }, osPids = Array.Empty<int>(), ownedRoots = new[] { app.Sandbox, runDirectory },
                    requiredChannels = new[] { "file", "process", "registry" },
                    traceHealth = new { complete = false, eventsLost = (int?)null, buffersLost = (int?)null },
                    positiveControls = new[] {
                        new { channel = "file", pid = Environment.ProcessId, token = "probe-diagnostic.txt" },
                        new { channel = "process", pid = app.Ledger.Owned[0].Pid, token = "Gcam.Studio" } },
                    blindSpots = new[] { "No guaranteed Studio registry effect; registry instrument proof requires approved fixture/channel evidence",
                        "No declared network traffic; network channel has no positive control and cannot establish absence",
                        "100 ms polling misses short-lived processes; ETW supplies event evidence", "Shell-mediated effects require causal metadata" },
                };
                error = CleanupBoundary.Attempt(error, () => File.WriteAllText(IsolationPaths.Inside(runDirectory, "trace-context.json"), JsonSerializer.Serialize(context) + "\n"), issues, "blocked");
            }
            manifest.WriteAudit = new { mode = "launch-idle-exit", reused = false, scenarioIds = Array.Empty<string>(),
                positiveControls = new[] { new { channel = "file", seen = false }, new { channel = "process", seen = false }, new { channel = "registry", seen = false } },
                reason = "ETL export and classifier required before any G1 promotion",
                validFor = new { productBuild = package.DeploymentSha256, isolationContent = package.IsolationSha256,
                    pathRules = IsolationPaths.Hash(File.ReadAllBytes(Path.Combine(isolation, "surface-list.json"))),
                    registeredImages = new[] { "Gcam.Studio.exe" } } };
            manifest.CleanupCompleted(issues);
            error = CleanupBoundary.Attempt(error, () => manifest.Save(runDirectory, manifest.Finalise(error is null ? "passed" : "failed", observed: true)), issues, "blocked");
            error = CleanupBoundary.Attempt(error, () => File.WriteAllText(IsolationPaths.Inside(runDirectory, "harness.complete.json"), JsonSerializer.Serialize(new { runId, finishedAt = finished, result = error is null ? "passed" : "failed" }) + "\n"), issues, "blocked");
        }
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        output.WriteLine("Probe actions completed; G1 remains blocked until channel evidence is classified.");
        Assert.Empty(issues);
    }
}
