using System.Diagnostics;
using System.Windows.Automation;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Compatibility entry point; all launch, ownership and cleanup code lives in Isolation.</summary>
public sealed class StudioProcess : IDisposable
{
    public const string ProcessName = "Gcam.Studio";
    private readonly OwnedStudioProcess _owned;
    private StudioProcess(OwnedStudioProcess owned) => _owned = owned;
    public Process Process => _owned.Process;
    public string Sandbox => _owned.Sandbox;
    public string ExePath => _owned.ExePath;
    public DateTime StartedAt => _owned.StartedAt;
    public string ProductVersion => _owned.ProductVersion;
    public string Ending => _owned.Ending;
    public IReadOnlyList<string> SandboxWrites => _owned.SandboxWrites;
    public ProcessLedger Ledger => _owned.Ledger;
    public IReadOnlyList<string> MonitorErrors => _owned.MonitorErrors;
    public DesktopLease.State LeaseState => _owned.LeaseState;
    public OwnedStudioProcess.PreflightResult Preflight => _owned.Preflight;
    public ActionPolicy Policy => _owned.Policy;
    public void RequireInputSafe() => _owned.RequireInputSafe();
    public static StudioProcess Start(string runId, string scenario = "scenario", IReadOnlyList<string>? declaredWrites = null)
        => new(OwnedStudioProcess.Start(runId, scenario: scenario, declaredWrites: declaredWrites));
    public AutomationElement MainWindow(TimeSpan timeout) => _owned.MainWindow(timeout);
    public void Dispose() => _owned.Dispose();
}
