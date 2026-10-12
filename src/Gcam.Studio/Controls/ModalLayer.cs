using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Gcam.Studio.Controls;

/// <summary>A focus boundary exposed as a group in the window's existing automation tree.</summary>
public sealed class ModalLayer : Grid
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ModalLayerPeer(this);
    private sealed class ModalLayerPeer(ModalLayer owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(ModalLayer);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
