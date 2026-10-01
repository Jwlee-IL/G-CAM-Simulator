using System.Runtime.InteropServices;
using System.Windows;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>
/// Real pointer input (the overlay has no automation patterns, so measuring goes through the mouse). Coordinates
/// are physical screen pixels, the same space as UIA bounding rectangles once this process is per-monitor DPI aware.
/// </summary>
public static class Pointer
{
    private const int StepDelayMs = 15;
    private const uint LeftDown = 0x0002, LeftUp = 0x0004;

    static Pointer()
    {
        // The INPUT layout below (union at offset 8) is the x64 one; an x86 test host would send garbage.
        if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("Pointer requires a 64-bit test host");
        // Per-monitor v2: without it, UIA rectangles and SetCursorPos disagree at 125 / 150 % scaling.
        SetProcessDpiAwarenessContext(new IntPtr(-4));
    }

    /// <summary>True when cursor coordinates and UIA rectangles are both physical pixels.</summary>
    public static bool IsPerMonitorAware => AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), new IntPtr(-4));

    public static void MoveTo(Point p)
    {
        if (!SetCursorPos((int)Math.Round(p.X), (int)Math.Round(p.Y))) throw new InvalidOperationException("SetCursorPos failed");
        Thread.Sleep(StepDelayMs);
    }

    public static void Click(Point p)
    {
        MoveTo(p);
        Button(LeftDown);
        Button(LeftUp);
        Thread.Sleep(100);
    }

    /// <summary>Press at <paramref name="from"/>, move in steps (well past any drag threshold), release at <paramref name="to"/>.</summary>
    public static void Drag(Point from, Point to, int steps = 12)
    {
        MoveTo(from);
        Button(LeftDown);
        for (int i = 1; i <= steps; i++)
            MoveTo(new Point(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps));
        Button(LeftUp);
        Thread.Sleep(100);
    }

    /// <summary>Where the cursor actually is (to confirm the input landed where intended).</summary>
    public static Point Position() => GetCursorPos(out var p) ? new Point(p.X, p.Y) : throw new InvalidOperationException("GetCursorPos failed");

    private static void Button(uint flags)
    {
        var input = new Input { Type = 0, Mouse = new MouseInput { Flags = flags } };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1) throw new InvalidOperationException("SendInput was blocked (UIPI or a locked desktop?)");
        Thread.Sleep(StepDelayMs);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx, Dy;
        public uint MouseData, Flags, Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public MouseInput Mouse;   // union starts after the 4-byte type, 8-aligned on x64
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X, Y;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out NativePoint p);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);
}
