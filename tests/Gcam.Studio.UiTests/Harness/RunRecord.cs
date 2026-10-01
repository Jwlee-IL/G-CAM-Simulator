using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>
/// Run manifest + failure bundle for one scenario run, written under the test output folder
/// (<c>ui-runs/&lt;runId&gt;/</c>, ignored by git). Profile P1: exploratory, never promoted to evidence.
/// </summary>
public sealed class RunRecord
{
    private readonly Dictionary<string, object?> _fields = new();
    private readonly List<string> _steps = [];

    public RunRecord(string scenario)
    {
        RunId = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        Directory = Path.Combine(AppContext.BaseDirectory, "ui-runs", RunId);
        System.IO.Directory.CreateDirectory(Directory);
        _fields["schema"] = 1;
        _fields["runId"] = RunId;
        _fields["scenario"] = scenario;
        _fields["profile"] = "P1";
        _fields["purpose"] = "exploratory";
        _fields["machine"] = new
        {
            os = Environment.OSVersion.VersionString,
            culture = CultureInfo.CurrentCulture.Name,
            perMonitorDpiAware = Pointer.IsPerMonitorAware,
        };
        _fields["workingTreeDirtyFiles"] = RepoPaths.DirtyFileCount();
    }

    public string RunId { get; }
    public string Directory { get; }

    public void Set(string key, object? value) => _fields[key] = value;

    public void Step(string text) => _steps.Add($"{DateTime.Now:HH:mm:ss.fff} {text}");

    /// <summary>Tree dump + window capture + last steps, for a run that failed.</summary>
    public void CaptureFailure(Exception error, AutomationElement? window)
    {
        _fields["error"] = error.ToString();
        if (window is null) return;
        try
        {
            File.WriteAllText(Path.Combine(Directory, "tree.txt"), Dump(window));
            // Only the app's visible frame. The UIA rectangle includes the invisible resize borders, and copying it
            // captured strips of whatever window was behind (observed: another app's text along the bottom edge).
            var r = VisibleFrame(new IntPtr(window.Current.NativeWindowHandle));
            using var bmp = new System.Drawing.Bitmap(r.Width, r.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen(r.Left, r.Top, 0, 0, bmp.Size);
            bmp.Save(Path.Combine(Directory, "window.png"));
        }
        catch (Exception e)
        {
            _fields["diagnosticsError"] = e.Message;   // a broken bundle must not hide the original failure
        }
    }

    public string Save(string result)
    {
        _fields["result"] = result;
        _fields["steps"] = _steps;
        string path = Path.Combine(Directory, "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(_fields, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static (int Left, int Top, int Width, int Height) VisibleFrame(IntPtr hwnd)
    {
        const int ExtendedFrameBounds = 9;   // DWMWA_EXTENDED_FRAME_BOUNDS: the frame without the invisible borders
        int hr = DwmGetWindowAttribute(hwnd, ExtendedFrameBounds, out var r, System.Runtime.InteropServices.Marshal.SizeOf<NativeRect>());
        if (hr != 0) throw new InvalidOperationException($"DwmGetWindowAttribute failed: 0x{hr:X8}");
        return (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect value, int size);

    private static string Dump(AutomationElement root)
    {
        var sb = new StringBuilder();
        void Walk(AutomationElement e, int depth)
        {
            var c = e.Current;
            sb.Append(' ', depth * 2).Append(c.ControlType.ProgrammaticName.Replace("ControlType.", ""))
              .Append(" id=").Append(c.AutomationId).Append(" name=\"").Append(c.Name).Append("\" status=\"").Append(c.ItemStatus)
              .Append("\" enabled=").Append(c.IsEnabled).AppendLine();
            for (var ch = TreeWalker.ControlViewWalker.GetFirstChild(e); ch is not null; ch = TreeWalker.ControlViewWalker.GetNextSibling(ch))
                Walk(ch, depth + 1);
        }
        Walk(root, 0);
        return sb.ToString();
    }
}
