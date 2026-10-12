using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Gcam.Studio.Controls;

/// <summary>A small, keyboard-focusable circled "?" that carries secondary explanation off the screen (TODO-41; reusable
/// Studio-wide). Its <see cref="Text"/> is the tooltip, which opens on hover and on keyboard focus, and the same text is
/// the automation HelpText, so screen readers and UI tests read exactly what the tooltip shows. The look (theme tokens,
/// both themes, focus visual) is the implicit style in Themes/Controls.xaml; give each mark an AutomationId and an
/// accessible name that says what it explains.</summary>
public sealed class HelpMark : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string),
        typeof(HelpMark), new FrameworkPropertyMetadata("", OnTextChanged));

    /// <summary>The explanation shown in the tooltip and exposed as the automation HelpText.</summary>
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    private readonly TextBlock _content = new() { TextWrapping = TextWrapping.Wrap };

    static HelpMark()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HelpMark), new FrameworkPropertyMetadata(typeof(HelpMark)));
        FocusableProperty.OverrideMetadata(typeof(HelpMark), new FrameworkPropertyMetadata(true));
        IsTabStopProperty.OverrideMetadata(typeof(HelpMark), new FrameworkPropertyMetadata(true));
    }

    public HelpMark()
    {
        Visibility = Visibility.Collapsed; // until it has something to say
        ToolTip = new ToolTip { Content = _content, PlacementTarget = this };
        // A long explanation wraps at the side-panel width rather than spanning the screen.
        _content.SetResourceReference(MaxWidthProperty, "Size.HelpMark.TooltipWidth");
        ToolTipService.SetShowDuration(this, int.MaxValue);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var mark = (HelpMark)d;
        string text = (string?)e.NewValue ?? "";
        mark._content.Text = text;
        AutomationProperties.SetHelpText(mark, text);
        mark.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // Keyboard users get the same tooltip as pointer users: open on focus, close when focus leaves.
    protected override void OnGotKeyboardFocus(System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (ToolTip is ToolTip tip && Text.Length > 0) tip.IsOpen = true;
    }

    protected override void OnLostKeyboardFocus(System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        if (ToolTip is ToolTip tip) tip.IsOpen = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HelpMarkPeer(this);

    private sealed class HelpMarkPeer(HelpMark owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(HelpMark);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
        protected override string GetLocalizedControlTypeCore() => "help";
        protected override bool IsKeyboardFocusableCore() => true;
        // Also inside an item template: the mark is how a row's details reach the control view.
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
        protected override string GetHelpTextCore() => owner.Text;
        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : "Help";
    }
}
