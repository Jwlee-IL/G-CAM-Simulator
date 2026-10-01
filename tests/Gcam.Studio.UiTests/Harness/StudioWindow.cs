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

    /// <summary>
    /// Types into a text field and commits it. Fields bind on LostFocus, so focus is moved to
    /// <paramref name="commitBy"/> afterwards; the value is read back before returning.
    /// </summary>
    public void SetText(string id, string text, string commitBy)
    {
        var value = Pattern<ValuePattern>(ById(id), ValuePattern.Pattern);
        value.SetValue(text);
        ById(id).SetFocus();
        ById(commitBy).SetFocus();
        Thread.Sleep(150);
    }

    public string Value(string id) => Pattern<ValuePattern>(ById(id), ValuePattern.Pattern).Current.Value;

    public bool IsEnabled(string id) => ById(id).Current.IsEnabled;

    /// <summary>Keys to the element (focused first), as <c>SendKeys</c> syntax: "{ESC}", "{DELETE}".</summary>
    public void Keys(string id, string keys)
    {
        ById(id).SetFocus();
        Thread.Sleep(100);
        System.Windows.Forms.SendKeys.SendWait(keys);
        Thread.Sleep(150);
    }

    /// <summary>Starts an acquisition and waits for a terminal state (Completed / Stopped / Failed).</summary>
    public string Acquire(TimeSpan timeout)
    {
        Invoke("StartAcquisition");
        WaitUntil(() => RunState == "Acquiring" || IsTerminal(RunState), TimeSpan.FromSeconds(5), "acquisition started");
        WaitUntil(() => IsTerminal(RunState), timeout, "acquisition finished");
        return RunState;
    }

    private static bool IsTerminal(string state) => state is "Completed" or "Stopped" or "Failed";

    /// <summary>Acquisition state published by the status line (Idle / Acquiring / Stopped / Completed / Failed).</summary>
    public string RunState => ById("StatusText").Current.ItemStatus;

    /// <summary>Counts in the status sentence ("t = 1.2 s of 60 s · 1,834 counts · 153 cps").</summary>
    public long Counts
    {
        get
        {
            var m = System.Text.RegularExpressions.Regex.Match(Text("StatusText"), @"([\d,]+) counts");
            return m.Success ? long.Parse(m.Groups[1].Value.Replace(",", ""), System.Globalization.CultureInfo.InvariantCulture) : -1;
        }
    }

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
