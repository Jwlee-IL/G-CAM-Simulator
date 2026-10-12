using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Gcam.Studio.Controls;

/// <summary>Reusable in-window scrim and focus boundary. No HWND or separate window is created.</summary>
public sealed class ModalHost : ContentControl
{
    public static readonly DependencyProperty DialogContentProperty = DependencyProperty.Register(nameof(DialogContent),
        typeof(object), typeof(ModalHost), new PropertyMetadata(null, Changed));
    public object? DialogContent { get => GetValue(DialogContentProperty); set => SetValue(DialogContentProperty, value); }
    public static readonly DependencyProperty DialogNameProperty = DependencyProperty.Register(nameof(DialogName),
        typeof(string), typeof(ModalHost), new PropertyMetadata(""));
    public string DialogName { get => (string)GetValue(DialogNameProperty); set => SetValue(DialogNameProperty, value); }
    public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register(nameof(CancelCommand),
        typeof(ICommand), typeof(ModalHost));
    public ICommand? CancelCommand { get => (ICommand?)GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
    private IInputElement? _previousFocus;
    private FrameworkElement? _dialog;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _dialog = GetTemplateChild("PART_Dialog") as FrameworkElement;
    }

    public ModalHost()
    {
        PreviewKeyDown += (_, e) =>
        {
            if (DialogContent is null || e.Key != Key.Escape) return;
            CancelCommand?.Execute(null);
            e.Handled = true;
        };
        PreviewGotKeyboardFocus += (_, e) =>
        {
            if (DialogContent is null || _dialog is null || e.NewFocus is not DependencyObject target || IsInside(target)) return;
            e.Handled = true;
            FocusDefault();
        };
    }

    private bool IsInside(DependencyObject target)
    {
        for (DependencyObject? node = target; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node == _dialog) return true;
        return false;
    }

    private void FocusDefault()
    {
        if (_dialog is null) return;
        _dialog.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (ModalHost)d;
        if (e.NewValue is not null)
        {
            host._previousFocus = Keyboard.FocusedElement;
            host.Dispatcher.BeginInvoke(host.FocusDefault, DispatcherPriority.Input);
        }
        else if (host._previousFocus is { } previous)
        {
            host.Dispatcher.BeginInvoke(() => Keyboard.Focus(previous), DispatcherPriority.Input);
            host._previousFocus = null;
        }
    }
}
