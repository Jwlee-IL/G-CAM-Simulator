using System.Runtime.InteropServices;
using Gcam.Studio.UiTests.Harness.Observation;
using System.Windows;
using System.Windows.Automation;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Coordinate input must land in the owned foreground frame on a declared target.</summary>
public static class PointerBoundary
{
    private sealed record Context(AutomationElement Window, ActionPolicy Policy, Action? RequireInputSafe, OwnedProcessIdentity? Identity, IntPtr MainWindow, Action<Pointer.ReleaseOutcome>? RecordRelease);
    private static readonly AsyncLocal<Context?> Current = new();
    private sealed class Scope(Context? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
    public static IDisposable Enter(AutomationElement window, ActionPolicy policy, Action? requireInputSafe = null,
        OwnedProcessIdentity? identity = null, IntPtr mainWindow = default, Action<Pointer.ReleaseOutcome>? recordRelease = null)
    {
        var prior = Current.Value; Current.Value = new(window, policy, requireInputSafe, identity, mainWindow, recordRelease); return new Scope(prior);
    }
    internal static (OwnedProcessIdentity? Identity, IntPtr MainWindow, Action<Pointer.ReleaseOutcome>? Record) ReleaseContext
        => (Current.Value?.Identity, Current.Value?.MainWindow ?? IntPtr.Zero, Current.Value?.RecordRelease);

    public static void Validate(bool foreground, bool owned, bool declared)
    {
        if (!foreground || !owned || !declared) throw new InvalidOperationException("coordinate-input-boundary-refused");
    }
    /// <summary>Every input into Studio passes here. No scope (or a scope without a guard) refuses: the desktop path
    /// always enters one with the owned instance's guard; headless tests enter an explicit test scope.</summary>
    public static void RequireInputSafe()
    {
        var context = Current.Value ?? throw new InvalidOperationException("No owned input scope; input refused");
        (context.RequireInputSafe ?? throw new InvalidOperationException("No input guard; input refused"))();
    }
    public static void Input(Action input) { RequireInputSafe(); input(); }
    public static void Require(Point point)
    {
        var context = Current.Value ?? throw new InvalidOperationException("No owned coordinate-input scope");
        RequireInputSafe();
        var window = context.Window.Current;
        var native = new NativePoint { X = (int)Math.Round(point.X), Y = (int)Math.Round(point.Y) };
        IntPtr hit = WindowFromPoint(native);
        GetWindowThreadProcessId(hit, out uint pid);
        Validate(GetForegroundWindow() == new IntPtr(window.NativeWindowHandle),
            pid == (uint)window.ProcessId && window.BoundingRectangle.Contains(point), true);
        var element = AutomationElement.FromPoint(point);
        while (element is not null && element.Current.ProcessId == window.ProcessId)
        {
            string id = element.Current.AutomationId;
            if (!string.IsNullOrEmpty(id)) { context.Policy.Require(id, "pointer"); return; }
            element = TreeWalker.ControlViewWalker.GetParent(element);
        }
        Validate(true, true, false);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
}
