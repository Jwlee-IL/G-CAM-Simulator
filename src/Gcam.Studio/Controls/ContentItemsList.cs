using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Gcam.Studio.Controls;

/// <summary>An ItemsControl whose rows are exposed to UI Automation through their own content — e.g. a row's focusable
/// <see cref="HelpMark"/> named after the row, with the row's details as HelpText — instead of WPF's data-item peers.
/// Data-item peers reach the row content only while WPF can map the item back to its container; after the items are
/// replaced (the readout rows are rebuilt on every module edit) that mapping was seen to fail on the desktop, leaving
/// items with a name and no children or help text, which hides the details from screen readers.</summary>
public sealed class ContentItemsList : ItemsControl
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ContentItemsListPeer(this);

    private sealed class ContentItemsListPeer(ContentItemsList owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(ContentItemsList);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
        // Children: the peers found in the realised row visuals (FrameworkElementAutomationPeer's visual-tree walk).
    }
}
