using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>
/// Manual figure capture: quality checks first (owned, foreground, restored, unoccluded, expected size, stable, no local
/// identity in the window's text), then the visible frame's pixels and the marks measured in the same transaction. Mark
/// rectangles are UIA bounds (physical pixels, never re-scaled) minus the frame origin, floor / ceil to whole pixels.
/// A capture with any reason is rejected: no pixels are kept.
/// </summary>
public static class ManualCapture
{
    public sealed record Baseline(int WidthPx, int HeightPx, int Dpi, string Theme);
    public sealed record Outcome(string[] Reasons, string? PngPath = null, byte[]? Png = null, string? MarksPath = null, byte[]? Marks = null);
    public sealed record Measured(string Target, string Label, string LabelSource, double[] Screen, int[] Image);

    /// <summary>Frame tolerance against the requested window size (resize borders, caption rounding).</summary>
    public const int SizeSlackPx = 32;

    /// <summary>Physical screen rectangle → integer image rectangle relative to the frame origin (same rule as the generator).</summary>
    public static int[] ImageRect(double x, double y, double w, double h, int originX, int originY)
    {
        int left = (int)Math.Floor(x - originX), top = (int)Math.Floor(y - originY);
        return [left, top, (int)Math.Ceiling(x + w - originX) - left, (int)Math.Ceiling(y + h - originY) - top];
    }

    public static bool Inside(int[] rect, int width, int height) =>
        rect[2] > 0 && rect[3] > 0 && rect[0] >= 0 && rect[1] >= 0 && rect[0] + rect[2] <= width && rect[1] + rect[3] <= height;

    public static Baseline MeasureBaseline(StudioWindow ui)
    {
        var (_, _, w, h) = Frame(Handle(ui));
        return new(w, h, (int)GetDpiForWindow(Handle(ui)), ui.Text("ThemeToggle") == "Light theme" ? "dark" : "light");
    }

    public static Outcome Take(StudioWindow ui, Baseline baseline, string directory, string figureId, string scenario,
        IReadOnlyList<Mark> marks, string? region, bool full)
    {
        var reasons = new List<string>();
        IntPtr hwnd = Handle(ui);
        int pid = ui.Element.Current.ProcessId;
        GetWindowThreadProcessId(hwnd, out uint owner);
        bool owned = owner == (uint)pid;
        bool foreground = ui.EnsureForeground();
        if (IsIconic(hwnd)) reasons.Add("minimised");
        if (!Pointer.IsPerMonitorAware) reasons.Add("dpi-unaware");
        if (Math.Abs(baseline.WidthPx - Scenario.WindowWidth) > SizeSlackPx || Math.Abs(baseline.HeightPx - Scenario.WindowHeight) > SizeSlackPx)
            reasons.Add("frame-not-normalised");
        Thread.Sleep(250);   // render settle; stability is then measured, not assumed
        var frame = Frame(hwnd);
        var before = Measure(ui, marks, region, frame.Left, frame.Top, reasons);
        string? occluder = Occluder(hwnd, pid, frame);
        if (occluder is not null) reasons.Add("occluded:" + occluder);
        if (!PrivacySafe(ui)) reasons.Add("privacy-identity-in-window-text");
        byte[] rgb;
        byte[] png;
        using (var bmp = new System.Drawing.Bitmap(frame.Width, frame.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        {
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(frame.Left, frame.Top, 0, 0, bmp.Size);
                g.FillRectangles(System.Drawing.Brushes.Black, VideoCapture.EdgeRing(bmp.Size));   // what lies behind shows in the edge
            }
            rgb = Rgb(bmp);
            using var stream = new MemoryStream();
            bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            png = stream.ToArray();
        }
        var after = Measure(ui, marks, region, frame.Left, frame.Top, null);
        bool stable = Frame(hwnd) == frame && Same(before, after);
        var template = new CaptureQuality.Template(scenario, figureId, baseline.WidthPx, baseline.HeightPx, baseline.Dpi,
            baseline.Theme, "en", marks.Select(m => m.Target).ToArray(), full ? "owned-frame-full" : "owned-frame-crop", "manual-1");
        reasons.AddRange(CaptureQuality.Check(template, new(frame.Width, frame.Height, owned, foreground, occluder is not null, stable,
            !reasons.Contains("privacy-identity-in-window-text"), rgb)));
        if (reasons.Count != 0) return new(reasons.Distinct().ToArray());

        string pngPath = "captures/" + figureId + ".png", marksPath = "captures/" + figureId + ".marks.json";
        var regionMeasured = region is null ? null : before.Single(m => m.LabelSource == "region");
        var doc = new
        {
            schema = "gcam.figure-marks", schemaVersion = 1, capture = pngPath, captureSha256 = IsolationPaths.Hash(png),
            widthPx = frame.Width, heightPx = frame.Height, frameOriginPx = new[] { frame.Left, frame.Top }, dpi = (int)GetDpiForWindow(hwnd),
            perMonitorAware = Pointer.IsPerMonitorAware, templateSha256 = template.Sha256, crop = full ? "full" : "marks",
            region = regionMeasured is null ? null : new { target = regionMeasured.Target, screen = regionMeasured.Screen, image = regionMeasured.Image },
            marks = before.Where(m => m.LabelSource != "region").Select((m, i) => new
            {
                n = i + 1, target = m.Target, label = m.Label, labelSource = m.LabelSource, screen = m.Screen, image = m.Image,
            }),
        };
        byte[] marksBytes = JsonSerializer.SerializeToUtf8Bytes(doc, ManualRun.Json).Concat("\n"u8.ToArray()).ToArray();
        System.IO.Directory.CreateDirectory(IsolationPaths.Inside(directory, "captures"));
        File.WriteAllBytes(IsolationPaths.Inside(directory, pngPath), png);
        File.WriteAllBytes(IsolationPaths.Inside(directory, marksPath), marksBytes);
        return new([], pngPath, png, marksPath, marksBytes);
    }

    /// <summary>Marks and region in physical pixels, read in the window scope (no tab navigation). Adds a reason for a
    /// target that is off screen, unlabelled or not wholly inside the frame.</summary>
    private static List<Measured> Measure(StudioWindow ui, IReadOnlyList<Mark> marks, string? region, int originX, int originY, List<string>? reasons)
    {
        var result = new List<Measured>();
        var (_, _, width, height) = Frame(Handle(ui));
        void Add(string target, string label, string source)
        {
            var element = ui.ById(target, ui.Element);
            var r = element.Current.BoundingRectangle;
            if (element.Current.IsOffscreen || r.IsEmpty) { reasons?.Add("target-offscreen:" + target); return; }
            var image = ImageRect(r.X, r.Y, r.Width, r.Height, originX, originY);
            if (!Inside(image, width, height)) reasons?.Add("target-outside-frame:" + target);
            if (source != "region" && string.IsNullOrWhiteSpace(label)) reasons?.Add("label-empty:" + target);
            result.Add(new(target, label, source, [r.X, r.Y, r.Width, r.Height], image));
        }
        if (region is not null) Add(region, "", "region");
        foreach (var mark in marks)
            Add(mark.Target, ManualRun.ReadLabel(ui, mark.Target, mark.Label), mark.Label switch
            {
                LabelSource.PrecedingText => "preceding-text", LabelSource.ChildText => "child-text", _ => "name",
            });
        return result;
    }

    private static bool Same(List<Measured> a, List<Measured> b) => a.Count == b.Count && a.Zip(b).All(p =>
        p.First.Target == p.Second.Target && p.First.Label == p.Second.Label && p.First.Screen.SequenceEqual(p.Second.Screen));

    /// <summary>No account, machine or profile path in any text the window exposes (the visible text is a subset).</summary>
    private static bool PrivacySafe(StudioWindow ui)
    {
        string[] identities = new[] { Environment.UserName, Environment.MachineName,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) }.Where(s => s.Length >= 3).ToArray();
        foreach (AutomationElement e in ui.Element.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition))
        {
            string name = e.Current.Name;
            if (identities.Any(id => name.Contains(id, StringComparison.OrdinalIgnoreCase))) return false;
        }
        return true;
    }

    /// <summary>A visible, uncloaked top-level window above the owned frame that overlaps it: another process's window, or
    /// an owned popup reaching outside the frame. Returns its class name (never its title) or null.</summary>
    private static string? Occluder(IntPtr hwnd, int pid, (int Left, int Top, int Width, int Height) frame)
    {
        try
        {
            var survey = WindowSurvey.Take(hwnd, pid);
            if (survey.Frame != new System.Drawing.Rectangle(frame.Left, frame.Top, frame.Width, frame.Height)) return "unstable-frame";
            return survey.Foreign.Length > 0 ? "foreign-window" : survey.OwnedPopupOutside ? "owned-popup-outside-frame" : null;
        }
        catch { return "survey-failed"; }
    }

    private static byte[] Rgb(System.Drawing.Bitmap bmp)
    {
        var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var bgra = new byte[data.Stride * bmp.Height];
            Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);
            var rgb = new byte[bmp.Width * bmp.Height * 3];
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    int s = y * data.Stride + x * 4, d = (y * bmp.Width + x) * 3;
                    rgb[d] = bgra[s + 2]; rgb[d + 1] = bgra[s + 1]; rgb[d + 2] = bgra[s];
                }
            return rgb;
        }
        finally { bmp.UnlockBits(data); }
    }

    private static IntPtr Handle(StudioWindow ui) => new(ui.Element.Current.NativeWindowHandle);

    /// <summary>The visible frame (DWM extended frame bounds), without the invisible resize borders.</summary>
    private static (int Left, int Top, int Width, int Height) Frame(IntPtr hwnd)
    {
        int hr = DwmGetWindowAttribute(hwnd, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out NativeRect r, Marshal.SizeOf<NativeRect>());
        if (hr != 0) throw new InvalidOperationException($"DwmGetWindowAttribute failed: 0x{hr:X8}");
        return (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    private static (int Left, int Top, int Width, int Height) Bounds(IntPtr hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, 9, out NativeRect r, Marshal.SizeOf<NativeRect>()) != 0 && !GetWindowRect(hwnd, out r)) return (0, 0, 0, 0);
        return (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int capacity);
}
