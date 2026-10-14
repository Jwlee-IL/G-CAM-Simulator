using System.Windows.Automation;
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
        => RunCore(name, output, (ui, record, _) => body(ui, record), []);

    /// <summary>The report scenario alone declares two writes at fixed names in its owned sandbox.</summary>
    public static void RunReportExport(string name, ITestOutputHelper output, Action<StudioWindow, RunRecord, string> body)
        => RunCore(name, output, body, ["report.html", "report.scene.json"]);

    private static void RunCore(string name, ITestOutputHelper output, Action<StudioWindow, RunRecord, string> body,
        IReadOnlyList<string> expectedWrites)
    {
        var record = new RunRecord(name);
        using var pollingLog = PollingWait.Record(record.Step);
        StudioProcess? app = null;
        AutomationElement? window = null;
        string result = "failed";
        try
        {
            app = StudioProcess.Start(record.RunId);
            record.Set("build", new { exe = app.ExePath, productVersion = app.ProductVersion, exeWritten = File.GetLastWriteTime(app.ExePath) });
            record.Set("process", new { pid = app.Process.Id, started = app.StartedAt, sandbox = app.Sandbox });
            window = app.MainWindow(TimeSpan.FromSeconds(15));
            var ui = new StudioWindow(window) { Log = record.Step };
            ui.Normalise(WindowWidth, WindowHeight);
            record.Set("window", window.Current.BoundingRectangle.ToString());
            Assert.Equal("Empty", ui.RunState);   // declared start state
            record.Step("window ready, state Empty");

            body(ui, record, app.Sandbox);
            result = "passed";
        }
        catch (Exception e)
        {
            record.CaptureFailure(e, window);
            throw;
        }
        finally
        {
            app?.Dispose();
            if (app is not null)
            {
                record.Set("ownedProcessEnding", app.Ending);
                record.Set("sandboxWrites", app.SandboxWrites);
            }
            output.WriteLine($"manifest: {record.Save(result)}");
        }
        Assert.Equal("exit 0", app!.Ending);
        Assert.Equal(expectedWrites.Order(), app.SandboxWrites.Order());
    }
}
