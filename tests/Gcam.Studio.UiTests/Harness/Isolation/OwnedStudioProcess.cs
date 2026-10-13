using System.Diagnostics;
using System.Windows.Automation;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>One isolated direct launch and one graceful close, retaining the owned folders for audit.</summary>
public sealed class OwnedStudioProcess : IDisposable
{
    private readonly DesktopLease _lease;
    private readonly ProcessMonitor _monitor;
    private bool _disposed;
    public Process Process { get; }
    public string Sandbox { get; }
    public string ExePath { get; }
    public DateTime StartedAt { get; }
    public string ProductVersion { get; }
    public string Ending { get; private set; } = "running";
    public IReadOnlyList<string> SandboxWrites { get; private set; } = [];
    public ProcessLedger Ledger { get; }
    public IReadOnlyList<string> MonitorErrors => _monitor.Errors;
    public DesktopLease.State LeaseState => _lease.Current;

    private OwnedStudioProcess(Process process, string sandbox, string exe, string runId, DesktopLease lease)
    {
        Process = process; Sandbox = sandbox; ExePath = exe; _lease = lease;
        StartedAt = process.StartTime; ProductVersion = FileVersionInfo.GetVersionInfo(exe).ProductVersion ?? "unknown";
        Ledger = new(runId, exe);
        Ledger.RegisterDirect(new(process.Id, Environment.ProcessId, StartedAt, exe, runId));
        _monitor = new ProcessMonitor(Ledger);
    }

    public static OwnedStudioProcess Start(string runId, IReadOnlyDictionary<string, string>? suppliedValues = null)
    {
        using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            ValidatePrivilege(new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator));
        string sandbox = IsolationPaths.RunRoot(Path.GetTempPath(), runId);
        var values = suppliedValues ?? IsolationPaths.LaunchValues(sandbox, runId);
        IsolationPaths.ValidateLaunchValues(sandbox, runId, values);
        using var self = Process.GetCurrentProcess();
        string leasePath = Path.Combine(Path.GetTempPath(), "gcam-desktop-lease", $"session-{self.SessionId}.json");
        var lease = new DesktopLease(leasePath, runId, self.SessionId, DateTimeOffset.UtcNow, TimeSpan.FromHours(3));
        Process? app = null;
        try
        {
            var existing = Process.GetProcessesByName("Gcam.Studio");
            int count = existing.Length; foreach (var p in existing) p.Dispose();
            if (count != 0) throw new InvalidOperationException("Unowned Studio process exists; launch refused");
            if (Directory.Exists(sandbox)) throw new IOException("Run folder already exists");
            Directory.CreateDirectory(sandbox);
            File.WriteAllText(IsolationPaths.Inside(sandbox, ".gcam-uia-owner"), runId + "\n");
            foreach (string name in new[] { "Roaming", "Local", "Temp" }) Directory.CreateDirectory(IsolationPaths.Inside(sandbox, name));
            string exe = RepoPaths.StudioExe();
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
            foreach (var value in values) psi.Environment[value.Key] = value.Value;
            app = Process.Start(psi) ?? throw new InvalidOperationException("Studio launch failed");
            return new(app, sandbox, exe, runId, lease);
        }
        catch (Exception failure)
        {
            try
            {
                if (app is not null)
                {
                    try { if (!app.HasExited) { app.CloseMainWindow(); app.WaitForExit(5000); } }
                    finally { app.Dispose(); }
                }
            }
            catch (Exception cleanup) { failure.Data["launchCleanupFailure"] = cleanup.ToString(); }
            finally
            {
                try { lease.Dispose(); }
                catch (Exception cleanup) { failure.Data["leaseCleanupFailure"] = cleanup.ToString(); }
            }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
    }

    public static void ValidatePrivilege(bool elevated)
    {
        if (elevated) throw new InvalidOperationException("Studio and its harness must remain non-elevated");
    }

    public AutomationElement MainWindow(TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (Process.HasExited) throw new InvalidOperationException($"Studio exited: {Process.ExitCode}");
            Process.Refresh();
            if (Process.MainWindowHandle != IntPtr.Zero) return AutomationElement.FromHandle(Process.MainWindowHandle);
            Thread.Sleep(100);
        }
        throw new TimeoutException("Studio main window did not appear");
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
                if (closeModal) new StudioWindow(MainWindow(TimeSpan.FromSeconds(5))).CloseModalLayer();
                Process.CloseMainWindow();
                if (!Process.WaitForExit(5000)) Ending = "still running after graceful close (force-stop requires approval)";
            }
            if (Ending == "running") Ending = Process.HasExited ? $"exit {Process.ExitCode}" : "still running";
        }
        finally
        {
            try
            {
                try
                {
                    SandboxWrites = Directory.EnumerateFiles(Sandbox, "*", SearchOption.AllDirectories)
                        .Where(f => Path.GetFileName(f) is not (".gcam-uia-owner" or "probe-diagnostic.txt"))
                        .Select(f => Path.GetRelativePath(Sandbox, f)).ToArray();
                }
                finally { _monitor.Dispose(); }
            }
            finally { try { _lease.Dispose(); } finally { Process.Dispose(); _disposed = true; } }
        }
    }
}
