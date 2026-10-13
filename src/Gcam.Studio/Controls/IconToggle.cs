using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Gcam.Studio.Controls;

/// <summary>An icon-only radio toggle (style <c>RadioButton.Icon</c>; the Measurements tools) whose automation peer reports
/// <see cref="AutomationProperties.AccessKeyProperty"/>. An icon has no text to underline, so the key is a window key
/// binding named in the tooltip and in <c>AutomationProperties.AccessKey</c>; WPF's <c>UIElementAutomationPeer</c>
/// returns an empty access key whenever that property is set (it reports only an underlined AccessText character), so
/// without this peer the key would never reach UI Automation. Everything else — group, check state, control type and
/// class name ("RadioButton") — is the plain RadioButton's.</summary>
public sealed class IconToggle : RadioButton
{
    protected override AutomationPeer OnCreateAutomationPeer() => new IconTogglePeer(this);

    private sealed class IconTogglePeer(IconToggle owner) : RadioButtonAutomationPeer(owner)
    {
        protected override string GetAccessKeyCore()
            => AutomationProperties.GetAccessKey(Owner) is { Length: > 0 } key ? key : base.GetAccessKeyCore();

        // Unchanged for UI Automation clients and the desktop harness: a radio button.
        protected override string GetClassNameCore() => nameof(RadioButton);
    }
}
