using System.Runtime.InteropServices;
using Rect = System.Drawing.Rectangle;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Shared z-order survey for manual stills and video. Never reads foreign window text.</summary>
public static class WindowSurvey
{
    public sealed record Result(Rect Frame, Rect[] Foreign, bool OwnedPopupOutside);
    public static Rect Bounds(IntPtr hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, 9, out NativeRect r, 16) != 0 && !GetWindowRect(hwnd, out r))
            throw new InvalidOperationException("Window bounds unavailable");
        return Rect.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }
    public static Result Take(IntPtr hwnd, int pid)
    {
        if (!IsWindow(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd) || GetWindowThreadProcessId(hwnd, out uint owner) == 0 || owner != pid)
            throw new InvalidOperationException("Owned frame unavailable");
        Rect frame = Bounds(hwnd);
        if (frame.Width <= 0 || frame.Height <= 0) throw new InvalidOperationException("Empty frame");
        // Refuse partial-monitor copies rather than retaining pixels from outside a visible screen.
        var screen = new Rect(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79));
        if (!screen.Contains(frame)) throw new InvalidOperationException("Frame outside desktop bounds");
        var foreign = new List<Rect>(); bool popupOutside = false;
        var seen = new HashSet<IntPtr>();
        for (var w = GetWindow(hwnd, 3); w != IntPtr.Zero; w = GetWindow(w, 3))
        {
            if (!seen.Add(w)) throw new InvalidOperationException("Unstable window stack");
            if (!IsWindowVisible(w)) continue;
            if (DwmGetWindowAttribute(w, 14, out int cloaked, 4) != 0) throw new InvalidOperationException("Window cloak state unavailable");
            if (cloaked != 0) continue;
            Rect r = Bounds(w);
            if (!r.IntersectsWith(frame)) continue;
            if (GetWindowThreadProcessId(w, out uint process) == 0) throw new InvalidOperationException("Window owner unavailable");
            if (process != pid)
            {
                r.Inflate(8, 8); // conservatively cover compositor shadows around the reported frame
                foreign.Add(Rect.Intersect(frame, r));
            }
            else if (!frame.Contains(r)) popupOutside = true;
        }
        return new(frame, foreign.ToArray(), popupOutside);
    }
    public static Rect Letterbox(int sourceWidth, int sourceHeight, int width, int height)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        double scale = Math.Min((double)width / sourceWidth, (double)height / sourceHeight);
        int w = Math.Max(1, (int)Math.Floor(sourceWidth * scale)), h = Math.Max(1, (int)Math.Floor(sourceHeight * scale));
        return new((width - w) / 2, (height - h) / 2, w, h);
    }
    public static Rect Mask(Rect absolute, Rect source, Rect target)
    {
        Rect clipped = Rect.Intersect(absolute, source);
        if (clipped.IsEmpty) return Rect.Empty;
        int x = (int)Math.Floor((double)(clipped.Left - source.Left) * target.Width / source.Width) + target.Left;
        int y = (int)Math.Floor((double)(clipped.Top - source.Top) * target.Height / source.Height) + target.Top;
        int right = (int)Math.Ceiling((double)(clipped.Right - source.Left) * target.Width / source.Width) + target.Left;
        int bottom = (int)Math.Ceiling((double)(clipped.Bottom - source.Top) * target.Height / source.Height) + target.Top;
        return Rect.Intersect(target, Rect.FromLTRB(x, y, right, bottom));
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect rect, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
}
