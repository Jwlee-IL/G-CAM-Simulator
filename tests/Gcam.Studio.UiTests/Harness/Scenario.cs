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
    {
        var record = new RunRecord(name);
        using var pollingLog = PollingWait.Record(record.Step);
        StudioProcess? app = null;
        AutomationElement? window = null;
        string result = "failed";
        var manifest = new UiRunManifest(record.RunId, name);
        var issues = new List<UiRunManifest.Issue>();
        Exception? failure = null;
        IDisposable? pointerScope = null;
        try
        {
            app = StudioProcess.Start(record.RunId);
            record.Set("build", new { exe = app.ExePath, productVersion = app.ProductVersion, exeWritten = File.GetLastWriteTime(app.ExePath) });
            record.Set("process", new { pid = app.Process.Id, started = app.StartedAt, sandbox = app.Sandbox });
            window = app.MainWindow(TimeSpan.FromSeconds(15));
            pointerScope = PointerBoundary.Enter(window, ActionPolicy.Load(Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation/action-policy.json")));
            var ui = new StudioWindow(window) { Log = record.Step };
            ui.Normalise(WindowWidth, WindowHeight);
            record.Set("window", window.Current.BoundingRectangle.ToString());
            Assert.Equal("Empty", ui.RunState);   // declared start state
            record.Step("window ready, state Empty");

            body(ui, record);
            result = "passed";
        }
        catch (Exception e)
        {
            failure = e;
            failure = CleanupBoundary.Attempt(failure, () => record.CaptureFailure(e, window), issues, "blocked");
        }
        finally
        {
            failure = CleanupBoundary.Attempt(failure, () => app?.Dispose(), issues, "cleanup_failed");
            failure = CleanupBoundary.Attempt(failure, () => pointerScope?.Dispose(), issues, "cleanup_failed");
            if (app is not null)
            {
                record.Set("ownedProcessEnding", app.Ending);
                record.Set("sandboxWrites", app.SandboxWrites);
                manifest.Evidence["ownedProcesses"] = app.Ledger.Owned;
                manifest.Evidence["sharedResources"] = new[] { app.LeaseState };
                manifest.Evidence["processMonitor"] = new { methods = new[] { "polling-100ms" }, errors = app.MonitorErrors,
                    blindSpots = new[] { "processes shorter than a polling interval; use ETW for qualification" } };
                if (app.Ending != "exit 0") issues.Add(new("cleanup_failed", app.Ending));
                if (app.SandboxWrites.Count != 0) issues.Add(new("unsafe_effect", "Undeclared sandbox writes"));
                foreach (string issue in app.Ledger.Issues) issues.Add(new("unsafe_effect", issue));
                if (app.MonitorErrors.Count != 0) issues.Add(new("blocked", "process-monitor-not-proven"));
            }
            if (app is null) issues.Add(new("blocked", "app-not-started"));
            manifest.CleanupCompleted(issues);
            failure = CleanupBoundary.Attempt(failure, () => manifest.Save(record.Directory, manifest.Finalise(result)), issues, "blocked");
            failure = CleanupBoundary.Attempt(failure, () => output.WriteLine($"manifest: {record.Save(issues.Count == 0 ? result : "failed")}"), issues, "blocked");
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.Empty(issues);
        Assert.Equal("exit 0", app!.Ending);
        Assert.Empty(app.SandboxWrites);
    }
}
