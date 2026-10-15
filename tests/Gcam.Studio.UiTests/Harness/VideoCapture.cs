using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>One owned-region frame. Called only by the opted-in recorder, never by a headless fixture.</summary>
public sealed class VideoCapture(IntPtr hwnd, int pid, int width, int height)
{
    public sealed record Frame(byte[] Rgb32, int[][] Masks, string[] Reasons, bool Privacy);
    public Frame Take(bool privacy)
    {
        using var canvas = new Bitmap(width, height, PixelFormat.Format32bppRgb);
        var masks = new List<int[]>(); var reasons = new List<string>();
        using (var output = Graphics.FromImage(canvas))
        {
            output.Clear(Color.Black);
            try
            {
                if (privacy) { reasons.Add("privacy-pause"); }
                else
                {
                    var before = WindowSurvey.Take(hwnd, pid);
                    using var source = new Bitmap(before.Frame.Width, before.Frame.Height, PixelFormat.Format32bppRgb);
                    using (var g = Graphics.FromImage(source)) g.CopyFromScreen(before.Frame.Location, Point.Empty, source.Size);
                    var after = WindowSurvey.Take(hwnd, pid);
                    if (before.Frame != after.Frame) throw new InvalidOperationException("Frame moved during capture");
                    DrawCursor(source, before.Frame);
                    var target = WindowSurvey.Letterbox(source.Width, source.Height, width, height);
                    output.DrawImage(source, target);
                    foreach (var rect in before.Foreign.Concat(after.Foreign).Distinct())
                    {
                        var mask = WindowSurvey.Mask(rect, before.Frame, target);
                        if (mask.IsEmpty) continue;
                        output.FillRectangle(Brushes.Black, mask);
                        masks.Add([mask.X, mask.Y, mask.Width, mask.Height]);
                    }
                    if (masks.Count > 0) reasons.Add("foreign-window");
                    if (before.OwnedPopupOutside || after.OwnedPopupOutside) reasons.Add("owned-popup-clipped");
                }
            }
            catch { output.Clear(Color.Black); masks.Clear(); reasons.Add("survey-failed"); }
            if (privacy || reasons.Contains("survey-failed")) masks.Add([0, 0, width, height]);
        }
        var data = canvas.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var bytes = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, bytes, y * width * 4, width * 4);
            return new(bytes, masks.ToArray(), reasons.ToArray(), privacy);
        }
        finally { canvas.UnlockBits(data); }
    }
    private static void DrawCursor(Bitmap bitmap, Rectangle frame)
    {
        var cursor = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
        if (!GetCursorInfo(ref cursor)) throw new InvalidOperationException("Cursor unavailable");
        if ((cursor.Flags & 1) == 0) return;
        IntPtr icon = CopyIcon(cursor.Cursor);
        if (icon == IntPtr.Zero) throw new InvalidOperationException("Cursor copy unavailable");
        IconInfo info = default;
        try
        {
            if (!GetIconInfo(icon, out info)) throw new InvalidOperationException("Cursor hotspot unavailable");
            using var g = Graphics.FromImage(bitmap); IntPtr dc = g.GetHdc();
            try { if (!DrawIconEx(dc, cursor.X - frame.Left - (int)info.XHotspot, cursor.Y - frame.Top - (int)info.YHotspot, icon, 0, 0, 0, IntPtr.Zero, 3)) throw new InvalidOperationException("Cursor draw failed"); }
            finally { g.ReleaseHdc(dc); }
        }
        finally
        {
            if (info.Mask != IntPtr.Zero) DeleteObject(info.Mask);
            if (info.Colour != IntPtr.Zero) DeleteObject(info.Colour);
            DestroyIcon(icon);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct CursorInfo { public int Size, Flags; public IntPtr Cursor; public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct IconInfo { public int Icon; public uint XHotspot, YHotspot; public IntPtr Mask, Colour; }
    [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CursorInfo cursor);
    [DllImport("user32.dll")] private static extern IntPtr CopyIcon(IntPtr icon);
    [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int width, int height, int step, IntPtr brush, int flags);
}
