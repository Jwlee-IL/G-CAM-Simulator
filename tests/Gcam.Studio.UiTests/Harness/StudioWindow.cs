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
        if (scope is null) SelectLeftTabFor(automationId);
        var matches = (scope ?? Element).FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
        return matches.Count == 1
            ? matches[0]
            : throw new SelectorMatchException(automationId, matches.Count);
    }

    public void SelectLeftTab(string tab)
    {
        var element = Element.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "Left." + tab + "Tab"))
            ?? throw new SelectorMatchException("Left." + tab + "Tab", 0);
        var selection = Pattern<SelectionItemPattern>(element, SelectionItemPattern.Pattern);
        if (selection.Current.IsSelected) return;
        selection.Select();
        WaitUntil(() => selection.Current.IsSelected, TimeSpan.FromSeconds(2), "left " + tab + " tab selected");
    }

    private void SelectLeftTabFor(string id)
    {
        string? tab = id.StartsWith("Optics.", StringComparison.Ordinal) ? "Optics"
            : id.StartsWith("Readout.", StringComparison.Ordinal) || id == "Detector.Section" ? "Detector"
            : id.StartsWith("Source", StringComparison.Ordinal) || id is "AddSource" or "RemoveSource" ? "Scene" : null;
        if (tab is not null) SelectLeftTab(tab);
    }

    /// <summary>The in-window modal layer, in this window's single UIA tree. The dialog's own elements can be found a moment
    /// before the layer group itself (recovery run 20261013-170634: the Options tab existed, the layer matched 0), so the
    /// layer is waited for; more than one match stays a selector error.</summary>
    public AutomationElement ModalLayer()
    {
        AutomationElement? layer = null;
        WaitUntil(() => (layer = ById("Modal.Layer")) is not null, TimeSpan.FromSeconds(5), "modal layer in the tree");
        return layer!;
    }

    public void ConfirmReset(bool confirm)
    {
        WaitUntil(() => Exists("Reset.Message"), TimeSpan.FromSeconds(5), "reset confirmation opened");
        _ = ModalLayer();
        Invoke(confirm ? "Reset.Discard" : "Modal.Cancel");
        WaitUntil(() => !Exists("Reset.Message"), TimeSpan.FromSeconds(5), "reset confirmation closed");
    }

    public void CloseModalLayer()
    {
        if (Visible("Modal.Close")) Invoke("Modal.Close");
        else if (Exists("Modal.Cancel")) Invoke("Modal.Cancel");
    }

    public void OpenOptions(string tab = "Acquisition")
    {
        Invoke("OpenOptions");
        WaitUntil(() => Exists("Options." + tab + "Tab"), TimeSpan.FromSeconds(5), "Options opened");
        Assert.Equal("Options", ModalLayer().Current.Name);
        Select("Options." + tab + "Tab");
    }
    public void ApplyOptions()
    {
        Invoke("Options.Apply");
        WaitUntil(() => !Exists("Options.AcquisitionTab"), TimeSpan.FromSeconds(5), "Options applied");
    }
    public void CancelOptions()
    {
        Invoke(Visible("Modal.Close") ? "Modal.Close" : "Modal.Cancel");
        WaitUntil(() => !Exists("Options.AcquisitionTab"), TimeSpan.FromSeconds(5), "Options cancelled or closed");
    }
    private static string OptionTab(string id) => id == "AcquisitionSeed" ? "Acquisition"
        : id.StartsWith("Detector.", StringComparison.Ordinal) || id.StartsWith("Chain.", StringComparison.Ordinal) ? "Detector" : "Environment";
    public void SetOption(string id, string value)
    {
        OpenOptions(OptionTab(id));
        SetText(id, value, "Options.Apply");
        ApplyOptions();
    }
    public bool OptionEnabled(string id)
    {
        OpenOptions(OptionTab(id));
        bool enabled = IsEnabled(id); CancelOptions(); return enabled;
    }
    public string OptionValue(string id)
    {
        OpenOptions(OptionTab(id));
        string value = Value(id); CancelOptions(); return value;
    }

    public bool Exists(string automationId) =>
        Element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId)) is not null;

    private bool Visible(string id) => Exists(id) && !ById(id).Current.IsOffscreen;

    public void Invoke(string id) => Pattern<InvokePattern>(ById(id), InvokePattern.Pattern).Invoke();

    public void Click(string id)
    {
        // A real pointer click lands on whatever window is on top: never click unless the owned window is foreground.
        if (!EnsureForeground())
            throw new InvalidOperationException("The owned Studio window is not in the foreground; a pointer click would land on another window.");
        var r = Bounds(id);
        Pointer.Click(new Point(r.X + r.Width / 2, r.Y + r.Height / 2));
    }

    /// <summary>Run-record logger (set by <see cref="Scenario"/>).</summary>
    public Action<string>? Log { get; set; }

    /// <summary>Restores and activates the owned Studio window when another window has taken the foreground (e.g. during
    /// a long preparation wait). Acts on the owned window's own handle only — after checking that the handle belongs to
    /// the owned process — and never on any other window. True when the owned window is foreground afterwards.</summary>
    public bool EnsureForeground()
    {
        var handle = new IntPtr(Element.Current.NativeWindowHandle);
        int pid = Element.Current.ProcessId;
        GetWindowThreadProcessId(handle, out uint owner);
        if (handle == IntPtr.Zero || owner != (uint)pid)
            throw new InvalidOperationException("The automation element is not the owned Studio window.");
        if (GetForegroundWindow() == handle) return true;
        Log?.Invoke("foreground lost to another window; restoring and activating the owned Studio window (own handle only)");
        if (Element.TryGetCurrentPattern(WindowPattern.Pattern, out var w) && w is WindowPattern window
            && window.Current.WindowVisualState == WindowVisualState.Minimized)
            window.SetWindowVisualState(WindowVisualState.Normal);
        SetForegroundWindow(handle);
        var watch = Stopwatch.StartNew();
        while (GetForegroundWindow() != handle && watch.Elapsed < TimeSpan.FromSeconds(3)) Thread.Sleep(50);
        bool restored = GetForegroundWindow() == handle;
        Log?.Invoke(restored ? "owned Studio window is foreground again" : "could not restore the owned Studio window to the foreground");
        if (restored) Thread.Sleep(200); // let the window repaint before pointer input reads bounds
        return restored;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    public void Toggle(string id) => Pattern<TogglePattern>(ById(id), TogglePattern.Pattern).Toggle();

    public void Expand(string id)
    {
        if (id == "Optics.Section") SelectLeftTab("Optics");
        else if (id == "Detector.Section") SelectLeftTab("Detector");
        else Pattern<ExpandCollapsePattern>(ById(id), ExpandCollapsePattern.Pattern).Expand();
    }

    /// <summary>Selects a combo box item by name through its opened drop-down and reads the selection back. Throws
    /// <see cref="InvalidOperationException"/> when the drop-down does not open (or does not stay open until the item is
    /// selected), the item is absent, or the selection does not take; callers poll on that for late-published items. An item
    /// that is already selected returns without opening the drop-down.</summary>
    /// <remarks>A WPF combo exposes as items only the containers realised in its drop-down's items host, which lives in the
    /// drop-down Popup (its own HWND). Run 20261013-165943 found no item under the Options dialog's combo ("No
    /// FrontOnlyThroughMask in Acquisition.AmbientBound") and captured the drop-down closed, although Choose had not collapsed
    /// it: ModalHost's focus boundary walked the visual tree only, where the Popup root has no parent, and refused the focus
    /// the opening combo gives its selected item (fixed in ModalHost). The drop-down must therefore be open, and still open
    /// when the item is selected, so a drop-down that cannot open fails here. The item is looked up under the combo first
    /// and, while the drop-down is open, also through the combo's own item-container provider (which cannot match an element
    /// of another control); the selection is then read back.</remarks>
    public void Choose(string id, string name)
    {
        var combo = ById(id);
        if (SelectedName(combo) == name) return;
        var expand = Pattern<ExpandCollapsePattern>(combo, ExpandCollapsePattern.Pattern);
        expand.Expand();
        AutomationElement? item = null;
        try
        {
            WaitUntil(() => IsOpen(expand) && (item = DropDownItem(combo, name)) is not null, TimeSpan.FromSeconds(5), $"{name} in the open drop-down of {id}");
        }
        catch (TimeoutException e)
        {
            bool open = IsOpen(expand);
            Collapse(expand);
            throw new InvalidOperationException(open ? $"No {name} in {id}" : $"The drop-down of {id} did not open or stay open", e);
        }
        if (!IsOpen(expand))
            throw new InvalidOperationException($"The drop-down of {id} closed before {name} was selected");
        Pattern<SelectionItemPattern>(item!, SelectionItemPattern.Pattern).Select();
        Collapse(expand);
        try
        {
            WaitUntil(() => SelectedName(combo) == name, TimeSpan.FromSeconds(5), $"{id} selects {name}");
        }
        catch (TimeoutException e)
        {
            throw new InvalidOperationException($"{id} did not select {name} (selected: {SelectedName(combo) ?? "none"})", e);
        }
        Thread.Sleep(150);
    }

    private static bool IsOpen(ExpandCollapsePattern expand) => expand.Current.ExpandCollapseState == ExpandCollapseState.Expanded;

    private static AutomationElement? DropDownItem(AutomationElement combo, string name)
    {
        var realised = combo.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
            new PropertyCondition(AutomationElement.NameProperty, name)));
        if (realised is not null) return realised;
        if (!combo.TryGetCurrentPattern(ItemContainerPattern.Pattern, out var p) || p is not ItemContainerPattern items) return null;
        var item = items.FindItemByProperty(null, AutomationElement.NameProperty, name);
        return item is not null && item.Current.ControlType == ControlType.ListItem ? item : null;
    }

    private static void Collapse(ExpandCollapsePattern expand)
    {
        if (expand.Current.ExpandCollapseState != ExpandCollapseState.Collapsed) expand.Collapse();
        WaitUntil(() => expand.Current.ExpandCollapseState == ExpandCollapseState.Collapsed, TimeSpan.FromSeconds(2), "drop-down closed");
    }

    private static string? SelectedName(AutomationElement selector)
    {
        var selection = Pattern<SelectionPattern>(selector, SelectionPattern.Pattern).Current.GetSelection();
        return selection.Length == 1 ? selection[0].Current.Name : null;
    }

    /// <summary>The selected item's name of a selector (combo box) — read back, not set.</summary>
    public string SelectedItem(string id)
    {
        var selection = Pattern<SelectionPattern>(ById(id), SelectionPattern.Pattern).Current.GetSelection();
        return selection.Length == 1 ? selection[0].Current.Name : throw new InvalidOperationException($"{id}: {selection.Length} selected");
    }

    public System.Text.Json.JsonElement Evidence(string id) =>
        System.Text.Json.JsonDocument.Parse(ById(id).Current.HelpText).RootElement.Clone();

    public void Workspace(string name, string surface)
    {
        Click($"Workspace.{name}");
        WaitUntil(() => Exists(surface), TimeSpan.FromSeconds(5), $"{name} command activates its surface");
    }

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

    /// <summary>Starts (or continues) an acquisition and waits for a terminal state (Completed / Stopped / Failed). A new
    /// acquisition after one exists needs <c>ResetAcquisition</c> first.</summary>
    public string Acquire(TimeSpan timeout)
    {
        Invoke("StartAcquisition");
        WaitUntil(() => RunState == "Acquiring" || IsTerminal(RunState), TimeSpan.FromSeconds(5), "acquisition started");
        WaitUntil(() => IsTerminal(RunState), timeout, "acquisition finished");
        return RunState;
    }

    private static bool IsTerminal(string state) => state is "Completed" or "Stopped" or "Failed";

    /// <summary>Acquisition state published by the status line (Empty / Acquiring / Stopped / Completed / Failed).</summary>
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

    /// <summary>The names of every descendant text of a container (an ItemsControl's rows have no list items).</summary>
    public string AllText(string id) => string.Join('\n', ById(id)
        .FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
        .Cast<AutomationElement>().Select(e => e.Current.Name));

    /// <summary>Everything a container says, including what sits behind help marks: the names and HelpTexts of the element
    /// and of all its descendants. List rows of an ItemsControl appear as data items whose name is the row text, and their
    /// help marks carry the moved details as HelpText.</summary>
    public string Describe(string id)
    {
        var root = ById(id);
        var parts = new List<string> { root.Current.Name, root.Current.HelpText };
        foreach (AutomationElement e in root.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition))
        {
            parts.Add(e.Current.Name);
            parts.Add(e.Current.HelpText);
        }
        return string.Join('\n', parts.Where(p => !string.IsNullOrEmpty(p)));
    }

    /// <summary>The HelpText of one element (a help mark's explanation).</summary>
    public string Help(string id) => ById(id).Current.HelpText;

    /// <summary>Waits until <see cref="Describe"/> of a container contains every expected fragment (it refreshes after edits).</summary>
    public string WaitForDescription(string id, TimeSpan timeout, params string[] expected)
    {
        string text = "";
        WaitUntil(() => expected.All((text = Describe(id)).Contains), timeout, $"{id} describes: {string.Join(" | ", expected)}");
        return text;
    }

    /// <summary>A custom surface's automation ItemStatus (e.g. the readout map's line, module and zoom).</summary>
    public string Status(string id) => ById(id).Current.ItemStatus;

    public IReadOnlyList<AutomationElement> Rows(string listId) =>
        ById(listId).FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
            .Cast<AutomationElement>().ToArray();

    /// <summary>Bounding box in physical screen pixels.</summary>
    public Rect Bounds(string id) => ById(id).Current.BoundingRectangle;

    /// <summary>Puts the window in front at a fixed size, so pointer positions are reproducible.</summary>
    public void Normalise(int width, int height)
    {
        if (Visible("Modal.Layer"))
            throw new InvalidOperationException("Normalise requires a closed modal layer; the scenario left a dialog open.");
        if (Element.TryGetCurrentPattern(WindowPattern.Pattern, out var w) && w is WindowPattern window
            && window.Current.WindowVisualState != WindowVisualState.Normal)
            window.SetWindowVisualState(WindowVisualState.Normal);
        if (Element.TryGetCurrentPattern(TransformPattern.Pattern, out var t) && t is TransformPattern transform && transform.Current.CanResize)
            transform.Resize(width, height);
        AutomationElement? focusTarget = null;
        FocusPreparation.Wait(() =>
        {
            if (!EnsureForeground()) return false;
            focusTarget = ById("ThemeToggle");
            return focusTarget.Current.IsEnabled && !focusTarget.Current.IsOffscreen && focusTarget.Current.IsKeyboardFocusable;
        }, () =>
        {
            focusTarget!.SetFocus();
            return focusTarget.Current.HasKeyboardFocus;
        }, TimeSpan.FromSeconds(5), Log);
        Thread.Sleep(300);   // let layout settle after the resize; the verdict re-reads every bound it uses
    }

    public static T Pattern<T>(AutomationElement element, AutomationPattern pattern) where T : BasePattern =>
        element.TryGetCurrentPattern(pattern, out var p) && p is T typed
            ? typed
            : throw new InvalidOperationException($"'{element.Current.AutomationId}' ({element.Current.ControlType.ProgrammaticName}) does not support {pattern.ProgrammaticName}");

    public static void WaitUntil(Func<bool> condition, TimeSpan timeout, string what)
        => PollingWait.WaitUntil(condition, timeout, what);
}
