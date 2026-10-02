using System.Diagnostics;
using System.Windows.Automation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>
/// Starts GCAM Studio in an owned sandbox and requests graceful closure of its own window.
/// </summary>
/// <remarks>
/// <para><b>Isolation.</b> The app has no write surfaces of its own (no file, registry, network or process API is
/// reachable from Studio — see docs/AGENTS.UiAutomation.md, G1). As a technical boundary anyway, APPDATA,
/// LOCALAPPDATA, TEMP and TMP point into a fresh folder under %TEMP% that carries an owner marker; after the run the
/// folder is checked for files and retained for audit; deletion requires author approval.</para>
/// <para><b>Ownership.</b> An already-running Gcam.Studio that we did not start is a conflict: the run fails, and
/// that process is never attached to or killed. On dispose only the PID recorded at start receives a graceful
/// close request; a timeout is reported without killing it.</para>
/// </remarks>
public sealed class StudioProcess : IDisposable
{
    public const string ProcessName = "Gcam.Studio";
    private const string MarkerFile = ".gcam-uia-owner";

    private StudioProcess(Process process, string sandbox, string exePath)
    {
        Process = process;
        Sandbox = sandbox;
        ExePath = exePath;
        StartedAt = process.StartTime;
        ProductVersion = FileVersionInfo.GetVersionInfo(exePath).ProductVersion ?? "unknown";
    }

    public Process Process { get; }
    public string Sandbox { get; }
    public string ExePath { get; }
    public DateTime StartedAt { get; }

    /// <summary>"1.0.0+&lt;commit&gt;" — the SDK stamps the source revision into the binary.</summary>
    public string ProductVersion { get; }

    /// <summary>How the owned process ended (filled on dispose): "exit 0", "killed", …</summary>
    public string Ending { get; private set; } = "running";

    /// <summary>Files the app left in the sandbox (filled on dispose). Expected: none.</summary>
    public IReadOnlyList<string> SandboxWrites { get; private set; } = [];

    public static StudioProcess Start(string runId)
    {
        var running = Process.GetProcessesByName(ProcessName);
        string pids = string.Join(", ", running.Select(p => p.Id));
        foreach (var p in running) p.Dispose();
        if (running.Length > 0)
            throw new InvalidOperationException(
                $"{ProcessName} is already running (PID {pids}) and was not started by " +
                "this run. Close it yourself; the harness never attaches to or kills a process it does not own.");

        string exe = RepoPaths.StudioExe();
        string sandbox = Path.Combine(Path.GetTempPath(), $"gcam-uia-{runId}");
        Directory.CreateDirectory(sandbox);
        string marker = $"run={runId} host={Environment.ProcessId}";
        File.WriteAllText(Path.Combine(sandbox, MarkerFile), marker);
        foreach (var d in new[] { "Roaming", "Local", "Temp" }) Directory.CreateDirectory(Path.Combine(sandbox, d));

        var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
        psi.Environment["APPDATA"] = Path.Combine(sandbox, "Roaming");
        psi.Environment["LOCALAPPDATA"] = Path.Combine(sandbox, "Local");
        psi.Environment["TEMP"] = Path.Combine(sandbox, "Temp");
        psi.Environment["TMP"] = Path.Combine(sandbox, "Temp");
        psi.Environment["GCAM_UIA_RUN"] = runId;   // marker a later handoff check could look for
        var process = Process.Start(psi) ?? throw new InvalidOperationException($"could not start {exe}");
        return new StudioProcess(process, sandbox, exe);
    }

    /// <summary>The main window, once it exists (bounded wait).</summary>
    public AutomationElement MainWindow(TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (Process.HasExited) throw new InvalidOperationException($"app exited early with code {Process.ExitCode}");
            Process.Refresh();
            if (Process.MainWindowHandle != IntPtr.Zero) return AutomationElement.FromHandle(Process.MainWindowHandle);
            Thread.Sleep(100);
        }
        throw new TimeoutException($"no main window within {timeout.TotalSeconds:0} s");
    }

    public void Dispose()
    {
        try
        {
            if (!Process.HasExited)
            {
                Process.CloseMainWindow();
                if (!Process.WaitForExit(5000))
                {
                    Ending = "still running after graceful close (force-stop requires approval)";
                }
            }
            if (Ending == "running") Ending = Process.HasExited ? $"exit {Process.ExitCode}" : "still running";
        }
        finally
        {
            SandboxWrites = Directory.Exists(Sandbox)
                ? Directory.EnumerateFiles(Sandbox, "*", SearchOption.AllDirectories)
                    .Where(f => Path.GetFileName(f) != MarkerFile)
                    .Select(f => Path.GetRelativePath(Sandbox, f)).ToArray()
                : [];
            // Retain the owned sandbox for audit; deletion requires the author's approval.
            Process.Dispose();
        }
    }

}
