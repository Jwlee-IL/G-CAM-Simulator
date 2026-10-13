using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Gcam.Studio.Controls;

/// <summary>Exposes the complete caption bounds to UIA; a plain Border has no automation peer.</summary>
public sealed class CaptionBar : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new CaptionBarPeer(this);

    private sealed class CaptionBarPeer(CaptionBar owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(CaptionBar);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => false;
    }
}
