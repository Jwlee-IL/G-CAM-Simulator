using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Coordinate input must land in the owned foreground frame on a declared target.</summary>
public static class PointerBoundary
{
    private sealed record Context(AutomationElement Window, ActionPolicy Policy, Action? RequireInputSafe);
    private static readonly AsyncLocal<Context?> Current = new();
    private sealed class Scope(Context? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
    public static IDisposable Enter(AutomationElement window, ActionPolicy policy, Action? requireInputSafe = null)
    {
        var prior = Current.Value; Current.Value = new(window, policy, requireInputSafe); return new Scope(prior);
    }
    public static void Validate(bool foreground, bool owned, bool declared)
    {
        if (!foreground || !owned || !declared) throw new InvalidOperationException("coordinate-input-boundary-refused");
    }
    public static void RequireInputSafe() => Current.Value?.RequireInputSafe?.Invoke();
    public static void Require(Point point)
    {
        var context = Current.Value ?? throw new InvalidOperationException("No owned coordinate-input scope");
        context.RequireInputSafe?.Invoke();
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
