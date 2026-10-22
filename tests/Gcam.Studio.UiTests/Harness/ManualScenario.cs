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
        => RunCore(scenario, output, body, _ => []);

    /// <summary>The export chapter: one folder per format in the owned sandbox, each holding the export and its sidecars
    /// under the run-ID name the harness types (D4).</summary>
    public static void RunExport(ITestOutputHelper output, Action<StudioWindow, ManualRun> body)
        => RunCore("manual.export", output, body, runId => new[] { "html", "pdf", "docx", "csv" }
            .SelectMany(ext => ReportExportDialog.DeclaredFiles(runId, ext).Select(name => Path.Combine(ext, name))).ToArray());

    private static void RunCore(string scenario, ITestOutputHelper output, Action<StudioWindow, ManualRun> body, Func<string, IReadOnlyList<string>> writes)
    {
        var registry = ManualRegistry.Load();
        _ = registry.ForScenario(scenario);
        var before = ManualRun.ReadSubject();
        var started = DateTimeOffset.Now;
        ManualRun? run = null;
        string? manifest = null;
        RunRecord? originalRecord = null;
        var observation = ManualObservation.Active;
        var launchAt = DateTimeOffset.UtcNow;
        int? pid = null;
        DateTimeOffset? processStarted = null;
        Exception? failure = null;
        VideoRecorder? video = null;
        ProbePackage.EffectGrant[] effects = [];
        string[] liveControlFiles = [];
        try
        {
            Scenario.RunManual(scenario, output, (ui, record, sandbox) =>
            {
                manifest = Path.Combine(record.Directory, "ui-manifest.json");
                // The preflight record is set before any input; the exact grants also enter the scenario UI manifest.
                // Reading them after cleanup below avoids coupling this adapter to the video implementation.
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
            }, writes, recorder => video = recorder, record =>
            {
                originalRecord = record;
                manifest = Path.Combine(record.Directory, "ui-manifest.json");
            });
        }
        catch (Exception e)
        {
            failure = e;
        }
        if (run is null)
        {
            // Not reached the body (launch or normalisation failed): still a record, so this attempt supersedes older passes.
            string runId = originalRecord?.RunId ?? $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
            string folder = IsolationPaths.Inside(originalRecord?.Directory ?? OwnedFolder.Create(runId), "manual/" + runId);
            System.IO.Directory.CreateDirectory(folder);
            run = new ManualRun(scenario, registry, folder, runId, started, (_, _) => "");
            run.Record = originalRecord;
        }
        try
        {
            run.Video = video?.Stop();
            if (manifest is not null && File.Exists(manifest))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(manifest));
                effects = System.Text.Json.JsonSerializer.Deserialize<ProbePackage.EffectGrant[]>(doc.RootElement.GetProperty("realWorldEffects"),
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                if (doc.RootElement.TryGetProperty("liveChannels", out var channels) && channels.ValueKind == System.Text.Json.JsonValueKind.Object)
                    liveControlFiles = channels.GetProperty("harnessOwnedFiles").EnumerateArray().Select(v => v.GetString()!).ToArray();
                if (pid is null && doc.RootElement.GetProperty("ownedProcesses").EnumerateArray().FirstOrDefault() is { ValueKind: System.Text.Json.JsonValueKind.Object } owned)
                { pid = owned.GetProperty("pid").GetInt32(); processStarted = owned.GetProperty("startedAt").GetDateTimeOffset(); }
            }
            if (failure is not null) run.Fail(failure);
            output.WriteLine("manual record: " + run.Finalise(manifest, before, ManualRun.ReadSubject()));
            observation?.Record(new(run.RunId, launchAt, DateTimeOffset.UtcNow, pid, processStarted, scenario, manifest, effects, liveControlFiles));
        }
        catch (Exception e) { failure ??= e; }
        finally { video?.Complete(failure is null); }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
