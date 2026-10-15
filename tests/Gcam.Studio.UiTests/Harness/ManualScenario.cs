using System.Runtime.ExceptionServices;
using Gcam.Studio.UiTests.Harness.Isolation;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Marks a manual scenario with the registry scenario ID it generates (docs/manual/chapters.json).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ManualChapterAttribute(string scenario) : Attribute
{
    public string Scenario { get; } = scenario;
}

/// <summary>
/// A manual scenario: the ordinary owned, sandboxed <see cref="Scenario"/> run plus a manual run record written after
/// cleanup. The record lives in the run folder under <c>manual/&lt;runId&gt;/</c>; the generator reads a folder of such
/// run folders. A failure is re-thrown after the record is written, so the test fails as before. Under an optional
/// observed batch (<see cref="ManualObservation"/>) each launch reports its window and owned process.
/// </summary>
public static class ManualScenario
{
    public static void Run(string scenario, ITestOutputHelper output, Action<StudioWindow, ManualRun> body)
        => RunCore(scenario, output, body, []);

    public static void RunExport(ITestOutputHelper output, Action<StudioWindow, ManualRun> body)
        => RunCore("manual.export", output, body, new[] { "html", "pdf", "docx", "csv" }.SelectMany(ext => ext == "csv"
            ? new[] { $"{ext}/report.{ext}", $"{ext}/report.scene.json", $"{ext}/report.meta.json" }
            : new[] { $"{ext}/report.{ext}", $"{ext}/report.scene.json" }).Select(p => p.Replace('/', Path.DirectorySeparatorChar)).ToArray());

    private static void RunCore(string scenario, ITestOutputHelper output, Action<StudioWindow, ManualRun> body, IReadOnlyList<string> writes)
    {
        var registry = ManualRegistry.Load();
        _ = registry.ForScenario(scenario);
        var before = ManualRun.ReadSubject();
        var started = DateTimeOffset.Now;
        ManualRun? run = null;
        string? manifest = null;
        var observation = ManualObservation.Active;
        var launchAt = DateTimeOffset.UtcNow;
        int? pid = null;
        DateTimeOffset? processStarted = null;
        Exception? failure = null;
        VideoRecorder? video = null;
        try
        {
            Scenario.RunManual(scenario, output, (ui, record, sandbox) =>
            {
                manifest = Path.Combine(record.Directory, "ui-manifest.json");
                run = new ManualRun(scenario, registry, IsolationPaths.Inside(record.Directory, "manual/" + record.RunId), record.RunId,
                    started, (id, source) => ManualRun.ReadLabel(ui, id, source));
                run.Record = record; run.Sandbox = sandbox;
                System.IO.Directory.CreateDirectory(run.Directory);
                if (observation is not null)
                {
                    pid = ui.Element.Current.ProcessId;
                    using (var process = System.Diagnostics.Process.GetProcessById(pid.Value)) processStarted = process.StartTime.ToUniversalTime();
                    observation.SandboxReady(IsolationPaths.RunRoot(Path.GetTempPath(), record.RunId));
                }
                run.Attach(ui);
                try
                {
                    body(ui, run);
                    run.ResolveLabels(requireAll: true);
                }
                catch (Exception e)
                {
                    run.Fail(e);
                    throw;
                }
            }, writes, recorder => video = recorder);
        }
        catch (Exception e)
        {
            failure = e;
        }
        if (run is null)
        {
            // Not reached the body (launch or normalisation failed): still a record, so this attempt supersedes older passes.
            string runId = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
            string folder = IsolationPaths.Inside(OwnedFolder.Create(runId), "manual/" + runId);
            System.IO.Directory.CreateDirectory(folder);
            run = new ManualRun(scenario, registry, folder, runId, started, (_, _) => "");
        }
        try
        {
            run.Video = video?.Stop();
            if (failure is not null) run.Fail(failure);
            output.WriteLine("manual record: " + run.Finalise(manifest, before, ManualRun.ReadSubject()));
            observation?.Record(new(run.RunId, launchAt, DateTimeOffset.UtcNow, pid, processStarted));
        }
        catch (Exception e) { failure ??= e; }
        finally { video?.Complete(failure is null); }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
