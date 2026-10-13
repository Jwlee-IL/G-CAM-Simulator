using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Gcam.Studio.Controls;

/// <summary>Keeps section identifiers in the UIA tree after replacing expandable sections with tabs.</summary>
public sealed class SettingsGroup : StackPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new SettingsGroupPeer(this);

    private sealed class SettingsGroupPeer(SettingsGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(SettingsGroup);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override bool IsControlElementCore() => true;
    }
}
