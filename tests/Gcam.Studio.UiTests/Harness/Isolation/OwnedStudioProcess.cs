using System.Diagnostics;
using System.Windows.Automation;
using System.Text.Json.Serialization;
using Gcam.Studio.UiTests.Harness.Observation;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>One isolated direct launch and one graceful close, retaining the owned folders for audit.</summary>
public sealed class OwnedStudioProcess : IDisposable
{
    public sealed record PeerBoundary(string Status, string CharterReference, string Reason, string ResidualRisk, string[] AllowedPeers)
    {
        public void Validate()
        {
            if (Status != "charter-exception" || string.IsNullOrWhiteSpace(CharterReference) || string.IsNullOrWhiteSpace(Reason)
                || string.IsNullOrWhiteSpace(ResidualRisk) || AllowedPeers.Length != 0)
                throw new InvalidDataException("Peer boundary is neither blocked nor the declared no-peers charter exception");
        }
    }
    public sealed record Check(string Id, string Status);
    public sealed record PreflightChecks(Func<ContentFingerprint.DataCheck> TestData, Action ResolvedWriteSurfaces,
        Action LaunchValues, Func<PeerBoundary> PeerBoundary, Func<ActionPolicy> ActionPolicy);
    public sealed record PreflightResult(string Contract, Check[] Checks, ContentFingerprint.DataCheck TestData,
        PeerBoundary PeerBoundary, string? ActionPolicySha256, ProbePackage.EffectGrant[] RealWorldEffects,
        [property: JsonIgnore] ActionPolicy Policy, ProbePackage? ApprovalPackage = null,
        IReadOnlyDictionary<string, string>? LaunchValues = null, string[]? ResolvedExportPaths = null);
    public PreflightResult Preflight { get; }
    public ActionPolicy Policy => Preflight.Policy;

    /// <summary>The start delegate is unreachable until all five checks succeed; also a headless test seam.</summary>
    public static T StartAfterPreflight<T>(PreflightChecks checks, Func<PreflightResult, T> start,
        ProbePackage.EffectGrant[]? effects = null, ProbePackage? package = null,
        IReadOnlyDictionary<string, string>? launchValues = null, string[]? resolvedExportPaths = null)
    {
        var data = checks.TestData();
        checks.ResolvedWriteSurfaces();
        checks.LaunchValues();
        var peers = checks.PeerBoundary(); peers.Validate();
        var policy = checks.ActionPolicy() ?? throw new InvalidDataException("Action policy was not loaded");
        return start(new("v4", new[] { "test-data", "resolved-write-surfaces", "launch-values", "peer-boundary", "action-policy" }
            .Select(id => new Check(id, "passed")).ToArray(), data, peers, policy.DocumentSha256, effects ?? [], policy,
            package is null ? null : package with { Contract = "v4", Approvals = package.LiveApprovals }, launchValues, resolvedExportPaths));
    }
    private readonly DesktopLease _lease;
    private readonly ProcessMonitor _monitor;
    private bool _disposed;
    public Process Process { get; }
    public string Sandbox { get; }
    public string ExePath { get; }
    public DateTime StartedAt { get; }
    public string ProductVersion { get; }
    public string Ending { get; private set; } = "running";
    /// <summary>The files found in the sandbox after close; null while unread (an unread sandbox never equals "no writes").</summary>
    public IReadOnlyList<string>? SandboxWrites { get; private set; }
    public ProcessLedger Ledger { get; }
    public IReadOnlyList<string> MonitorErrors => _monitor.Errors;
    public DesktopLease.State LeaseState => _lease.Current;
    public RunObservation Observation { get; }

    private readonly int _pid;
    private OwnedStudioProcess(Process process, string sandbox, string exe, string runId, DesktopLease lease, PreflightResult preflight, RunObservation observation)
    {
        Process = process; Sandbox = sandbox; ExePath = exe; _lease = lease; _pid = process.Id;
        Preflight = preflight;
        Observation = observation;
        StartedAt = process.StartTime; ProductVersion = FileVersionInfo.GetVersionInfo(exe).ProductVersion ?? "unknown";
        Ledger = new(runId, exe);
        Ledger.RegisterDirect(new(process.Id, Environment.ProcessId, StartedAt, exe, runId));
        _monitor = new ProcessMonitor(Ledger);
        Observation.ProveConnectionChannel();
    }

    public static OwnedStudioProcess Start(string runId, IReadOnlyDictionary<string, string>? suppliedValues = null,
        string scenario = "launch-idle-exit", IReadOnlyList<string>? declaredWrites = null, ProbePackage? package = null)
    {
        using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            ValidatePrivilege(new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator));
        string sandbox = IsolationPaths.RunRoot(Path.GetTempPath(), runId);
        var values = (suppliedValues ?? IsolationPaths.LaunchValues(sandbox, runId)).ToDictionary(v => v.Key, v => v.Value);
        string isolation = Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation");
        string exe = RepoPaths.StudioExe();
        package ??= LoadLaunchPackage();
        var source = SourceIdentity.Read(RepoPaths.Root());
        package.ValidateSource(source.Commit, source.CommittedAt);
        package.Validate(package.RunId, IsolationPaths.Fingerprint(isolation), ProbePackage.DeploymentFingerprint(exe));
        if (package.Contract != "v4") throw new InvalidDataException("Desktop launch requires a v4 author approval package");
        package.RequireScenario(scenario);
        var effects = package.BindEffects(runId, scenario, declaredWrites ?? []);
        (ContentFingerprint.DataCheck Data, PeerBoundary Peers)? data = null;
        var checks = new PreflightChecks(
            () => (data = ContentFingerprint.CheckData(Path.GetDirectoryName(exe)!, Path.Combine(isolation, "test-data.json"), scenario, package.DeploymentSha256)).Value.Data,
            () => ValidateResolvedSurfaces(sandbox, values, declaredWrites ?? []),
            () => IsolationPaths.ValidateLaunchValues(sandbox, runId, values), () => data!.Value.Peers,
            () => ActionPolicy.Load(Path.Combine(isolation, "action-policy.json")));
        using var self = Process.GetCurrentProcess();
        string leasePath = LeasePath(self.SessionId);
        var lease = new DesktopLease(leasePath, runId, self.SessionId, DateTimeOffset.UtcNow, TimeSpan.FromHours(3));
        Process? app = null;
        RunObservation? observation = null;
        try
        {
            var existing = Process.GetProcessesByName("Gcam.Studio");
            int count = existing.Length; foreach (var p in existing) p.Dispose();
            if (count != 0) throw new InvalidOperationException("Unowned Studio process exists; launch refused");
            if (Directory.Exists(sandbox)) throw new IOException("Run folder already exists");
            Directory.CreateDirectory(sandbox);
            File.WriteAllText(IsolationPaths.Inside(sandbox, ".gcam-uia-owner"), runId + "\n");
            foreach (string name in new[] { "Roaming", "Local", "Temp" }) Directory.CreateDirectory(IsolationPaths.Inside(sandbox, name));
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
            foreach (var value in values) psi.Environment[value.Key] = value.Value;
            PreflightResult? preflight = null;
            app = StartAfterPreflight(checks, passed =>
            {
                preflight = passed;
                observation = new(runId, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Path.Combine(isolation, "surface-list.json"));
                var launched = app = Process.Start(psi) ?? throw new InvalidOperationException("Studio launch failed");
                observation.Attach(launched, OwnedProcessIdentity.From(launched), sandbox);
                return launched;
            }, effects, package, values, (declaredWrites ?? []).Select(p => IsolationPaths.Inside(sandbox, p)).ToArray());
            return new(app, sandbox, exe, runId, lease, preflight!, observation!);
        }
        catch (Exception failure)
        {
            try
            {
                if (app is not null)
                {
                    try { if (!app.HasExited) { observation?.MarkIntentionalClose(); app.CloseMainWindow(); app.WaitForExit(5000); } }
                    finally { app.Dispose(); }
                }
            }
            catch (Exception cleanup) { failure.Data["launchCleanupFailure"] = cleanup.ToString(); }
            finally
            {
                observation?.Dispose();
                observation?.AttachTo(failure);
                try { lease.Dispose(); }
                catch (Exception cleanup) { failure.Data["leaseCleanupFailure"] = cleanup.ToString(); }
            }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
    }

    private static ProbePackage LoadLaunchPackage()
    {
        if (ManualObservation.Active?.Package is { } batch) return batch;
        string? path = Environment.GetEnvironmentVariable("GCAM_UI_APPROVALS");
        if (string.IsNullOrWhiteSpace(path) && Environment.GetEnvironmentVariable("GCAM_UI_PROBE_DIRECTORY") is { } directory)
            path = IsolationPaths.Inside(directory, "approvals.json");
        return !string.IsNullOrWhiteSpace(path) ? ProbePackage.Load(path)
            : throw new InvalidDataException("A launch requires charter and desktop-go records (GCAM_UI_APPROVALS)");
    }

    public static void ValidateResolvedSurfaces(string sandbox, IReadOnlyDictionary<string, string> values, IReadOnlyList<string> exports)
    {
        foreach (string key in new[] { "APPDATA", "LOCALAPPDATA", "TEMP", "TMP" })
        {
            if (!values.TryGetValue(key, out var path)) throw new InvalidDataException("Missing resolved surface: " + key);
            _ = IsolationPaths.Inside(sandbox, Path.GetRelativePath(sandbox, path));
        }
        foreach (string file in exports) _ = IsolationPaths.Inside(sandbox, file);
    }

    public void RequireInputSafe() { Observation.RequireResponsive(); _monitor.RequireInputSafe(); }

    /// <summary>The declared harness lease file for an interactive session (surface harness-lease).</summary>
    public static string LeasePath(int sessionId) => Path.Combine(Path.GetTempPath(), "gcam-desktop-lease", $"session-{sessionId}.json");

    public static void ValidatePrivilege(bool elevated)
    {
        if (elevated) throw new InvalidOperationException("Studio and its harness must remain non-elevated");
    }

    /// <param name="launch">True for the launch wait: a timeout there is the run's launch failure. Later look-ups (the
    /// close sequence) only throw.</param>
    /// <remarks>Runs on a UIA worker that a product failure can abandon, so it uses its own process handle (opened here,
    /// identity-checked, disposed here), never the launcher's <see cref="Process"/>, which the controlling thread disposes
    /// at close.</remarks>
    public AutomationElement MainWindow(TimeSpan timeout, bool launch = true)
    {
        Process own;
        try { own = Process.GetProcessById(_pid); }
        catch (ArgumentException) { throw new InvalidOperationException("Studio exited before its main window was found"); }
        using (own)
        {
            if (!own.HasExited && !Ledger.Owns(own.Id, own.StartTime)) throw new InvalidOperationException("Process instance ownership changed");
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < timeout)
            {
                if (own.HasExited) throw new InvalidOperationException($"Studio exited: {own.ExitCode}");
                own.Refresh();
                if (own.MainWindowHandle != IntPtr.Zero)
                {
                    // An element taken before WPF attaches the window's content stays empty for good (measured 2026-10-15:
                    // 3 of 12 launches); take a fresh one until the window exposes its children.
                    var window = AutomationElement.FromHandle(own.MainWindowHandle);
                    if (window.FindFirst(TreeScope.Children, Condition.TrueCondition) is not null) return window;
                }
                Thread.Sleep(100);
            }
        }
        if (launch) Observation.LaunchFailed();
        throw new TimeoutException("Studio main window did not appear");
    }

    private bool HasMainWindow()
    {
        try { Process.Refresh(); return Process.MainWindowHandle != IntPtr.Zero; }
        catch (InvalidOperationException) { return false; } // exited meanwhile: the observer records the exit
    }

    public const string StillRunningAfterClose = "still running after graceful close (force-stop requires approval)";

    /// <summary>The close sequence (also a headless test seam); returns the owned process's ending. Modal-layer closing runs
    /// under the same bounded runner as every UI step, so a hang there is a recorded product failure (<c>not_responding</c>,
    /// phase <c>close-modal-layer</c>, the product axis only) and control returns within the limit; it does not end the close.
    /// The intentional-close mark is set only after it, immediately before the close request itself, so the hang is never
    /// excused as closing. The ending is always read from the process: <c>exit N</c>, or still running after the wait.
    /// A harness error (not a product failure) still propagates, after the close request and the ending.</summary>
    internal static string CloseOwned(RunObservation observation, Process process, bool closeModal, Action closeModalLayer, Action requestClose,
        Action<string>? onEnding = null)
    {
        string? ending = null;
        try
        {
            try
            {
                if (closeModal && !observation.HasFailure) observation.RunUi(closeModalLayer, "close-modal-layer");
            }
            catch (ProductLivenessException) when (observation.HasProductFailure) { } // recorded by the observer; the close goes on
            finally
            {
                observation.MarkIntentionalClose();
                requestClose();
                if (!process.WaitForExit(5000)) ending = StillRunningAfterClose;
            }
        }
        finally
        {
            try { ending ??= process.HasExited ? $"exit {process.ExitCode}" : "still running"; }
            catch (InvalidOperationException) { ending ??= "unknown (process handle unavailable)"; }
            onEnding?.Invoke(ending); // also when a harness error propagates
        }
        return ending;
    }

    /// <summary>The first observed run closes without UIA discovery or input.</summary>
    public void CloseWithoutAutomation() => Close(false);
    public void Dispose() => Close(true);
    private void Close(bool closeModal)
    {
        if (_disposed) return;
        try
        {
            if (!Process.HasExited)
            {
                if (!Ledger.Owns(Process.Id, Process.StartTime)) throw new InvalidOperationException("Process instance ownership changed before close");
                // A launch that never produced a main window (the observed probe has no launch wait of its own) is a launch
                // failure, not a later hang or exit. The controlling thread's own handle confirms the observer's view.
                if (!Observation.WindowSeen && !HasMainWindow()) Observation.LaunchFailed();
                CloseOwned(Observation, Process, closeModal && Ledger.Issues.Count == 0 && MonitorErrors.Count == 0, () =>
                {
                    var window = MainWindow(TimeSpan.FromSeconds(5), launch: false);
                    using var scope = PointerBoundary.Enter(window, Policy, RequireInputSafe);
                    new StudioWindow(window, Policy, RequireInputSafe).CloseModalLayer();
                }, () => Process.CloseMainWindow(), ending => Ending = ending);
            }
        }
        finally
        {
            try { if (Ending == "running") Ending = Process.HasExited ? $"exit {Process.ExitCode}" : "still running"; }
            catch (InvalidOperationException) { Ending = "unknown (process handle unavailable)"; }
            try
            {
                try
                {
                    SandboxWrites = Directory.EnumerateFiles(Sandbox, "*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(Sandbox, f))
                        .Where(f => f is not (".gcam-uia-owner" or "probe-diagnostic.txt" or ".gcam-liveness-failure.json")).ToArray();
                }
                finally { try { Observation.Dispose(); } finally { _monitor.Dispose(); } }
            }
            finally { try { _lease.Dispose(); } finally { Process.Dispose(); _disposed = true; } }
        }
    }
}
