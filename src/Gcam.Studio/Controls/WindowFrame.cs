using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Shell;

namespace Gcam.Studio.Controls;

/// <summary>Fits the borderless maximized window to the monitor work area in native pixels.</summary>
public sealed class WindowFrame : Border
{
    private Window? _window;
    private HwndSource? _source;
    private double _minWidth;
    private double _minHeight;

    public WindowFrame()
    {
        Loaded += (_, _) =>
        {
            _window = Window.GetWindow(this);
            if (_window is null) return;
            _minWidth = _window.MinWidth;
            _minHeight = _window.MinHeight;
            _source = HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);
            if (_source is not null)
            {
                const int styleIndex = -16, systemMenu = 0x00080000;
                // None removes the native caption; keep its system commands (Alt+Space/Alt+F4).
                SetWindowLong(_source.Handle, styleIndex, GetWindowLong(_source.Handle, styleIndex) | systemMenu);
            }
            _source?.AddHook(WindowMessage);
            _window.StateChanged += UpdateConstraints;
            UpdateConstraints(null, EventArgs.Empty);
        };
        Unloaded += (_, _) =>
        {
            if (_window is null) return;
            if (_source is { IsDisposed: false } source) source.RemoveHook(WindowMessage);
            _source = null;
            _window.StateChanged -= UpdateConstraints;
            _window = null;
        };
    }

    private void UpdateConstraints(object? sender, EventArgs e)
    {
        if (_window is null) return;
        // A high-DPI work area may be smaller than the normal minimum in DIPs.
        // Do not let that minimum make WPF arrange content beyond the maximized client area.
        bool maximized = _window.WindowState == WindowState.Maximized;
        _window.SetCurrentValue(Window.MinWidthProperty, maximized ? 0 : _minWidth);
        _window.SetCurrentValue(Window.MinHeightProperty, maximized ? 0 : _minHeight);
    }

    private IntPtr WindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int getMinMaxInfo = 0x0024, nonClientHitTest = 0x0084, monitorNearest = 2;
        if (message == nonClientHitTest && _window is { WindowState: WindowState.Normal,
            ResizeMode: ResizeMode.CanResize or ResizeMode.CanResizeWithGrip } && GetWindowRect(window, out var rect))
        {
            long position = lParam.ToInt64();
            int x = unchecked((short)position), y = unchecked((short)(position >> 16));
            double border = (WindowChrome.GetWindowChrome(_window)?.ResizeBorderThickness.Left ?? 0) * GetDpiForWindow(window) / 96d;
            int hit = ResizeHit(rect, x, y, (int)Math.Ceiling(border));
            // Caption controls stay flush visually, but must not consume the outer resize strip.
            if (hit != 0) { handled = true; return new IntPtr(hit); }
        }
        if (message != getMinMaxInfo) return IntPtr.Zero;
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(window, monitorNearest), ref monitor)) return IntPtr.Zero;
        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var bounds = WorkAreaBounds(monitor.Monitor, monitor.Work);
        info.MaxPosition = new NativePoint { X = bounds.X, Y = bounds.Y };
        info.MaxSize = new NativePoint { X = bounds.Width, Y = bounds.Height };
        info.MaxTrackSize = info.MaxSize;
        double dpi = GetDpiForWindow(window) / 96d;
        info.MinTrackSize = new NativePoint
        {
            X = Math.Min(bounds.Width, (int)Math.Ceiling(_minWidth * dpi)),
            Y = Math.Min(bounds.Height, (int)Math.Ceiling(_minHeight * dpi))
        };
        Marshal.StructureToPtr(info, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    /// <summary>WM_GETMINMAXINFO takes monitor-relative physical pixels at every DPI.</summary>
    public static (int X, int Y, int Width, int Height) WorkAreaBounds(NativeRect monitor, NativeRect work)
        => (work.Left - monitor.Left, work.Top - monitor.Top, work.Right - work.Left, work.Bottom - work.Top);

    /// <summary>Native HT codes for every edge/corner; zero leaves caption and client hit-testing to WindowChrome.</summary>
    public static int ResizeHit(NativeRect rect, int x, int y, int border)
    {
        if (border <= 0 || x < rect.Left || x >= rect.Right || y < rect.Top || y >= rect.Bottom) return 0;
        bool left = x < rect.Left + border, right = x >= rect.Right - border;
        bool top = y < rect.Top + border, bottom = y >= rect.Bottom - border;
        if (top) return left ? 13 : right ? 14 : 12; // HTTOPLEFT / HTTOPRIGHT / HTTOP
        if (bottom) return left ? 16 : right ? 17 : 15; // HTBOTTOMLEFT / HTBOTTOMRIGHT / HTBOTTOM
        return left ? 10 : right ? 11 : 0; // HTLEFT / HTRIGHT / delegate
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo { public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public int Flags; }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, int flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr window, int index, int value);
}
