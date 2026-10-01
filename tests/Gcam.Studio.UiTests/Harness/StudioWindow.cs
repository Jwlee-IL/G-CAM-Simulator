using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>
/// Screen map of the main window: elements by AutomationId (exactly one match, or the selector is wrong), typed
/// pattern access, and bounded waits on product state.
/// </summary>
/// <remarks>
/// Names are not used as selectors: several are shared on purpose (a panel title and its heatmap are both
/// "Detector flood map"), and they are display text. AutomationIds are set in MainWindow.xaml for that reason.
/// </remarks>
public sealed class StudioWindow(AutomationElement window)
{
    public AutomationElement Element { get; } = window;

    public AutomationElement ById(string automationId, AutomationElement? scope = null)
    {
        var matches = (scope ?? Element).FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
        return matches.Count == 1
            ? matches[0]
            : throw new InvalidOperationException($"selector '{automationId}' matched {matches.Count} elements (expected exactly 1)");
    }

    public bool Exists(string automationId) =>
        Element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId)) is not null;

    public void Invoke(string id) => Pattern<InvokePattern>(ById(id), InvokePattern.Pattern).Invoke();

    /// <summary>Selects a radio button / list item and reads the selection back.</summary>
    public void Select(string id)
    {
        var item = Pattern<SelectionItemPattern>(ById(id), SelectionItemPattern.Pattern);
        item.Select();
        WaitUntil(() => item.Current.IsSelected, TimeSpan.FromSeconds(2), $"{id} selected");
    }

    /// <summary>Run state published by the status line (Idle / Running / Succeeded / Cancelled / Failed).</summary>
    public string RunState => ById("StatusText").Current.ItemStatus;

    public string Text(string id) => ById(id).Current.Name;

    public IReadOnlyList<AutomationElement> Rows(string listId) =>
        ById(listId).FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
            .Cast<AutomationElement>().ToArray();

    /// <summary>Bounding box in physical screen pixels.</summary>
    public Rect Bounds(string id) => ById(id).Current.BoundingRectangle;

    /// <summary>Puts the window in front at a fixed size, so pointer positions are reproducible.</summary>
    public void Normalise(int width, int height)
    {
        if (Element.TryGetCurrentPattern(WindowPattern.Pattern, out var w) && w is WindowPattern window
            && window.Current.WindowVisualState != WindowVisualState.Normal)
            window.SetWindowVisualState(WindowVisualState.Normal);
        if (Element.TryGetCurrentPattern(TransformPattern.Pattern, out var t) && t is TransformPattern transform && transform.Current.CanResize)
            transform.Resize(width, height);
        Element.SetFocus();
        Thread.Sleep(300);   // let layout settle after the resize; the verdict re-reads every bound it uses
    }

    public static T Pattern<T>(AutomationElement element, AutomationPattern pattern) where T : BasePattern =>
        element.TryGetCurrentPattern(pattern, out var p) && p is T typed
            ? typed
            : throw new InvalidOperationException($"'{element.Current.AutomationId}' ({element.Current.ControlType.ProgrammaticName}) does not support {pattern.ProgrammaticName}");

    public static void WaitUntil(Func<bool> condition, TimeSpan timeout, string what)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (condition()) return;
            Thread.Sleep(50);
        }
        throw new TimeoutException($"timed out after {timeout.TotalSeconds:0.#} s waiting for: {what}");
    }
}
