using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>
/// Swaps the colour-token dictionary (Themes/Tokens.*.xaml) in the application resources. Styles reference
/// tokens through DynamicResource, so every open window repaints. Also asks DWM to draw a matching title bar.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const int DwmUseImmersiveDarkMode = 20;   // Windows 10 1809+ / 11

    public AppTheme Current { get; private set; } = AppTheme.Dark;

    public void Apply(AppTheme theme)
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        var tokens = new ResourceDictionary { Source = TokensUri(theme) };
        // Replace in place: inserting a second token dictionary would lose to the old one (later merged wins).
        int index = merged.ToList().FindIndex(d => d.Source is { } s && s.OriginalString.Contains("Themes/Tokens."));
        if (index < 0) throw new InvalidOperationException("No Themes/Tokens.*.xaml dictionary is merged into App.xaml.");
        merged[index] = tokens;

        Current = theme;
        foreach (Window w in Application.Current.Windows) ApplyTitleBar(w);
    }

    /// <summary>Call from a window's SourceInitialized so its title bar matches the current theme.</summary>
    public void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int dark = Current == AppTheme.Dark ? 1 : 0;
        // Best effort: older Windows simply ignores the attribute.
        _ = DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
        // The attribute alone doesn't repaint an existing frame; ask for a non-client refresh.
        _ = SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    private static Uri TokensUri(AppTheme theme) =>
        new($"pack://application:,,,/Gcam.Studio;component/Themes/Tokens.{theme}.xaml", UriKind.Absolute);

    private const uint SwpNoSize = 0x0001, SwpNoMove = 0x0002, SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010, SwpFrameChanged = 0x0020;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
