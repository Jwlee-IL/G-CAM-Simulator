using System.Diagnostics;
using System.Text.Json;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>
/// Opt-in MR8 observation around the whole manual scenario batch (mode observed-run: the first run of new flows under
/// the existing G1 basis, no permission change). With <c>GCAM_UI_OBSERVE_DIRECTORY</c> set, the collection fixture takes
/// snapshot A, waits a null window, takes snapshot B and writes the registry marker before the first scenario; after
/// the last scenario has cleaned up it takes snapshot C and writes <c>observation-context.json</c> in the shape
/// <c>stage0.py diff-classify</c> reads. Without the variable it does nothing, so ordinary runs are unchanged.
/// The Studio window is the first launch .. the last exit; harness-owned locations are the run folder, the desktop lease
/// and every run folder the batch created (proved by its owner-marker file).
/// </summary>
public sealed class ManualObservation : IDisposable
{
    public const string DirectoryVariable = "GCAM_UI_OBSERVE_DIRECTORY";
    public const string DiagnosticFile = "probe-diagnostic.txt";
    public static readonly TimeSpan NullWindow = TimeSpan.FromSeconds(12);

    /// <summary>One scenario's launch: its window bounds (before launch, after cleanup) and the owned Studio process.</summary>
    public sealed record Launch(string RunId, DateTimeOffset LaunchAt, DateTimeOffset ExitAt, int? Pid, DateTimeOffset? ProcessStartedAt,
        string ScenarioId = "", string? UiManifestPath = null, ProbePackage.EffectGrant[]? RealWorldEffects = null, string[]? LiveControlFiles = null);

    private static ManualObservation? _active;
    public static ManualObservation? Active => _active;

    private readonly List<Launch> _launches = [];
    private readonly List<StateSnapshot.Counts> _snapshots = [];
    private readonly IReadOnlyList<StateSnapshot.Root> _roots = [];
    private readonly WindowsRegistrySource _registry = new();
    private readonly DateTimeOffset _batchStarted = DateTimeOffset.UtcNow;
    private DateTimeOffset _nullStarted, _nullFinished;
    private RegistryMarker.Control? _marker;
    private string? _diagnostic;

    public string? RunDirectory { get; }
    public string? RunId { get; }
    public ProbePackage? Package { get; }

    public ManualObservation() : this(Environment.GetEnvironmentVariable) { }

    internal ManualObservation(Func<string, string?> environment)
    {
        string? directory = environment(DirectoryVariable);
        if (environment(DesktopFactAttribute.OptInVariable) != "1") return;
        if (environment("GCAM_UI_CACHE") == "1") throw new InvalidDataException("Manual production requires fresh attempts (GCAM_UI_CACHE=0)");
        if (string.IsNullOrEmpty(directory))
        {
            string? path = environment("GCAM_UI_APPROVALS");
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("Manual batch requires GCAM_UI_APPROVALS or an observed batch approvals.json");
            Package = ProbePackage.Load(path);
            if (Package.Contract != "v4") throw new InvalidDataException("Manual batch requires a v4 author approval package");
            var source = SourceIdentity.Read(RepoPaths.Root());
            Package.ValidateSource(source.Commit, source.CommittedAt);
            Package.Validate(Package.RunId, IsolationPaths.Fingerprint(Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation")),
                ProbePackage.DeploymentFingerprint(RepoPaths.StudioExe()));
            Package.RequireProductionGates(); // a manual batch produces an edition's inputs (D7)
            _active = this;
            return;
        }
        RunDirectory = Path.GetFullPath(directory);
        RunId = Path.GetFileName(RunDirectory);
        Package = Preflight(RunDirectory, RunId);
        if (Package.Contract != "v4") throw new InvalidDataException("Manual batch requires a v4 author approval package");
        Package.RequireProductionGates();
        _roots = StateSnapshot.ObservedRoots(Path.GetDirectoryName(RepoPaths.StudioExe())!, Path.GetTempPath(), RunDirectory);
        _snapshots.Add(Snapshot("A"));
        _nullStarted = DateTimeOffset.UtcNow;
        Thread.Sleep(NullWindow);   // nothing launched: what changes here is other actors' background activity
        _nullFinished = DateTimeOffset.UtcNow;
        _snapshots.Add(Snapshot("B"));
        _marker = new RegistryMarker(new WindowsRegistryMarkerStore()).Write(RunId, Environment.ProcessId);   // before any launch
        _active = this;
    }

    /// <summary>Read-only checks before any snapshot: a fresh confined run folder, a clean committed tree, pin.json and
    /// approvals.json naming this run, the live isolation and deployment fingerprints and the HEAD commit, and the
    /// author's desktop go for that commit. Retired permission-expansion approvals are not required.</summary>
    private static ProbePackage Preflight(string directory, string runId)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "gcam-todo43");
        if (!directory.StartsWith(Path.GetFullPath(scratch) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Observation output must be under the declared scratch root");
        _ = IsolationPaths.RunRoot("scratch", runId, v4: true);   // run ID rule shared with the launcher
        foreach (string name in new[] { "snapshot-A.jsonl.gz", "snapshot-B.jsonl.gz", "snapshot-C.jsonl.gz", "observation-context.json", "harness.complete.json" })
            if (File.Exists(Path.Combine(directory, name))) throw new InvalidOperationException("Run folder already holds " + name + "; use a fresh run ID");
        var source = SourceIdentity.Read(RepoPaths.Root());   // throws on a dirty tree
        string isolation = IsolationPaths.Fingerprint(Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation"));
        string deployment = ProbePackage.DeploymentFingerprint(RepoPaths.StudioExe());
        using (var pin = JsonDocument.Parse(File.ReadAllBytes(IsolationPaths.Inside(directory, "pin.json"))))
        {
            var p = pin.RootElement;
            if (p.GetProperty("sourceCommit").GetString() != source.Commit || p.GetProperty("workingTree").GetString() != "clean"
                || p.GetProperty("isolationSha256").GetString() != isolation || p.GetProperty("deploymentSha256").GetString() != deployment)
                throw new InvalidDataException("pin.json does not match the live commit, isolation or deployment");
        }
        var package = ProbePackage.Load(IsolationPaths.Inside(directory, "approvals.json"));
        package.ValidateSource(source.Commit, source.CommittedAt);
        if (package.RunId != runId || package.IsolationSha256 != isolation || package.DeploymentSha256 != deployment)
            throw new InvalidDataException("approvals.json does not identify this run, isolation or deployment");
        package.Validate(runId, isolation, deployment);
        return package;
    }

    /// <summary>The harness markers known when a snapshot is taken: the batch run ID and every scenario run ID so far (the
    /// sandboxes and the typed export names carry them); a registry value whose data carries one is flagged (TODO-52).</summary>
    private StateSnapshot.Counts Snapshot(string label) =>
        StateSnapshot.Take(label, IsolationPaths.Inside(RunDirectory!, $"snapshot-{label}.jsonl.gz"), _roots, _registry,
            [RunId!, .. _launches.Select(l => l.RunId)], _launches.SelectMany(l => l.RealWorldEffects ?? []).SelectMany(e => e.TypedNames).Distinct().ToArray());

    /// <summary>The file positive control: one harness write into the first scenario's sandbox while Studio runs.</summary>
    public void SandboxReady(string sandbox)
    {
        if (RunDirectory is null) return;
        if (_diagnostic is not null) return;
        _diagnostic = IsolationPaths.Inside(sandbox, DiagnosticFile);
        File.WriteAllText(_diagnostic, RunId + "\n");
    }

    public void Record(Launch launch) => _launches.Add(launch);

    /// <summary>Run folders of this batch in the temporary folder: created after the batch began and holding the
    /// harness owner marker (launch sandbox or run record).</summary>
    public static string[] OwnedFolders(string temporaryRoot, DateTimeOffset since) =>
        Directory.EnumerateDirectories(temporaryRoot, "gcam-uia-*")
            .Where(d => Directory.GetCreationTimeUtc(d) >= since.UtcDateTime.AddSeconds(-1)
                && (File.Exists(Path.Combine(d, ".gcam-uia-owner")) || File.Exists(Path.Combine(d, ".gcam-record-owner"))))
            .Select(Path.GetFullPath).Order(StringComparer.Ordinal).ToArray();

    /// <summary>The diff-classify input (local only: it names machine paths, A4).</summary>
    public static object Context(string runId, DateTimeOffset nullStarted, DateTimeOffset nullFinished, IReadOnlyList<StateSnapshot.Counts> snapshots,
        IReadOnlyList<Launch> launches, IReadOnlyList<string> ownedFiles, string? diagnosticFile, bool markerWritten, ProbePackage? package = null)
    {
        string markerKey = "HKCU\\" + RegistryMarker.RelativeRoot + "\\" + runId;
        var launchAt = launches.Count == 0 ? nullFinished : launches.Min(l => l.LaunchAt);
        var exitAt = launches.Count == 0 ? nullFinished : launches.Max(l => l.ExitAt);
        return new
        {
            schemaVersion = 1, contract = "v4", runId, method = "MR8 before/after snapshots", mode = "observed-run",
            targetCommit = package?.Commit, committedAt = package?.CommittedAt, deploymentSha256 = package?.DeploymentSha256,
            realWorldEffects = launches.SelectMany(l => l.RealWorldEffects ?? []).ToArray(),
            uiManifests = launches.Where(l => l.UiManifestPath is not null).Select(l => new { runId = l.RunId, path = l.UiManifestPath }).ToArray(),
            scope = "manual scenario batch: first runs of new flows under the existing G1 basis",
            nullWindow = new { startedAt = nullStarted.ToString("O"), finishedAt = nullFinished.ToString("O"), seconds = (nullFinished - nullStarted).TotalSeconds },
            studioWindow = new { launched = launches.Count != 0, launchAt = launchAt.ToString("O"), exitAt = exitAt.ToString("O"),
                seconds = (exitAt - launchAt).TotalSeconds, marginBeforeSeconds = 1, marginAfterSeconds = 2 },
            snapshots,
            scenarios = launches.Select(l => new { runId = l.RunId, scenarioId = l.ScenarioId, launchAt = l.LaunchAt.ToString("O"), exitAt = l.ExitAt.ToString("O") }).ToArray(),
            ownedProcesses = launches.Where(l => l.Pid is not null).Select(l => new { pid = l.Pid, parentPid = Environment.ProcessId, image = "Gcam.Studio.exe",
                startedAt = l.ProcessStartedAt?.ToString("O"), finishedAt = l.ExitAt.ToString("O"), ending = "see the scenario's UI manifest" }).ToArray(),
            harnessOwned = new { files = ownedFiles.Concat(launches.SelectMany(l => l.LiveControlFiles ?? [])).ToArray(), registry = new[] { markerKey } },
            positiveControls = new
            {
                file = diagnosticFile ?? "",
                registry = new { key = markerKey, valueName = RegistryMarker.ValueName, written = markerWritten },
                process = "Gcam.Studio.exe",
            },
            channelApplicability = new[] { new { channel = "network", applicable = true, reason = "see each scenario manifest liveChannels" } },
        };
    }

    public void Dispose()
    {
        if (ReferenceEquals(_active, this)) _active = null;
        if (RunDirectory is null) return;
        _active = null;
        var finished = DateTimeOffset.UtcNow;
        Exception? error = null;
        try { _snapshots.Add(Snapshot("C")); }
        catch (Exception e) { error = e; }
        using var host = Process.GetCurrentProcess();
        // The recorder's latest-video folder is a harness destination too (surface-list.json#harness-ui-videos).
        string[] owned = [RunDirectory, OwnedStudioProcess.LeasePath(host.SessionId), .. OwnedFolders(Path.GetTempPath(), _batchStarted),
            .. VideoRecorder.OwnedDestinations()];
        var context = Context(RunId!, _nullStarted, _nullFinished, _snapshots, _launches, owned, _diagnostic, _marker is not null, Package);
        File.WriteAllText(IsolationPaths.Inside(RunDirectory, "observation-context.json"), JsonSerializer.Serialize(context,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + "\n");
        File.WriteAllText(IsolationPaths.Inside(RunDirectory, "harness.complete.json"), JsonSerializer.Serialize(new
        {
            contract = "v4", runId = RunId, finishedAt = finished, scenarios = _launches.Count, snapshotC = error is null ? "taken" : "failed: " + error.GetType().Name,
        }) + "\n");
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}

/// <summary>The manual scenarios share one optional observation (one batch, A / null window / B … C).</summary>
[CollectionDefinition(Name)]
public sealed class ManualObservationCollection : ICollectionFixture<ManualObservation>
{
    public const string Name = "Manual scenarios (optional observed batch)";
}
