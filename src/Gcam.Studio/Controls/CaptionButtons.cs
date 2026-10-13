using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;

namespace Gcam.Studio.Controls;

/// <summary>Accessible system commands in the themed caption. Window code-behind stays declarative.</summary>
public sealed class CaptionButtons : StackPanel
{
    public static readonly DependencyProperty IsMaximizedProperty = DependencyProperty.Register(nameof(IsMaximized),
        typeof(bool), typeof(CaptionButtons), new PropertyMetadata(false, (d, _) => ((CaptionButtons)d).UpdateGlyph()));
    /// <summary>The caption state, also usable by a detached renderer without creating a native window.</summary>
    public bool IsMaximized { get => (bool)GetValue(IsMaximizedProperty); set => SetValue(IsMaximizedProperty, value); }
    private Window? _window;
    private readonly Button _maximize;
    public CaptionButtons()
    {
        Orientation = Orientation.Horizontal;
        Add("Minimize", "Window.Minimize", "Icon.Minimize", () => { if (_window is { } w) SystemCommands.MinimizeWindow(w); });
        _maximize = Add("Maximize", "Window.Maximize", "Icon.Maximize", () =>
        {
            if (_window is not { } w) return;
            if (w.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(w);
            else SystemCommands.MaximizeWindow(w);
        });
        Add("Close", "Window.Close", "Icon.Close", () => { if (_window is { } w) SystemCommands.CloseWindow(w); });
        Loaded += (_, _) =>
        {
            _window = Window.GetWindow(this);
            if (_window is not null) _window.StateChanged += StateChanged;
            StateChanged(null, EventArgs.Empty);
        };
        Unloaded += (_, _) => { if (_window is not null) _window.StateChanged -= StateChanged; _window = null; };
    }
    private Button Add(string name, string id, string icon, Action action)
    {
        var button = new Button { ToolTip = name, Command = new RelayCommand(action) };
        button.SetResourceReference(StyleProperty, id == "Window.Close" ? "Button.Caption.Close" : "Button.Caption");
        button.SetResourceReference(ContentControl.ContentProperty, icon);
        AutomationProperties.SetName(button, name); AutomationProperties.SetAutomationId(button, id);
        Children.Add(button);
        return button;
    }
    private void StateChanged(object? sender, EventArgs e)
    {
        IsMaximized = _window?.WindowState == WindowState.Maximized;
    }
    private void UpdateGlyph()
    {
        bool maximized = IsMaximized;
        string name = maximized ? "Restore" : "Maximize";
        _maximize.SetResourceReference(ContentControl.ContentProperty, maximized ? "Icon.Restore" : "Icon.Maximize");
        _maximize.ToolTip = name; AutomationProperties.SetName(_maximize, name);
    }
}
