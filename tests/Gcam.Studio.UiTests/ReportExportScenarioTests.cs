using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Gcam.Configuration;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>Compiled desktop scenario for Studio's first explicit file-write surface. Run only on author go.</summary>
/// <remarks>While the native save dialog is open the run record holds step text only: no screenshot, no tree dump and
/// no folder contents. The steps name the invoked element (control type, AutomationId, name), the file name read back
/// from the dialog, and the titles of any further owned modal windows.</remarks>
public sealed class ReportExportScenarioTests(ITestOutputHelper output)
{
    /// <summary>The common item dialog's OK (Save) and Cancel buttons; "No" of a Yes/No prompt.</summary>
    private const int IdOk = 1, IdCancel = 2, IdNo = 7;

    [DesktopFact]
    public void ExportReport_SaveDialogWritesIntoOwnedSandbox_AndMatchesDisplayedEstimate()
        => Scenario.RunReportExport(nameof(ExportReport_SaveDialogWritesIntoOwnedSandbox_AndMatchesDisplayedEstimate), output,
            (ui, record, sandbox) =>
            {
                Assert.False(ui.IsEnabled("ExportReport"));
                ui.SetText("AcquisitionLiveTime", "1", "StartAcquisition");
                Assert.Equal("Completed", ui.Acquire(TimeSpan.FromSeconds(30)));
                StudioWindow.WaitUntil(() => ui.Exists("PeakText") && ui.Text("PeakText").StartsWith("peak ("),
                    TimeSpan.FromSeconds(10), "displayed estimate");
                StudioWindow.WaitUntil(() => ui.AllText("Imaging.Peaks").Contains("Found"),
                    TimeSpan.FromSeconds(10), "published found-peak rows");
                string estimate = ui.Text("PeakText");
                var found = ui.AllText("Imaging.Peaks").Split('\n', StringSplitOptions.RemoveEmptyEntries);
                string path = Path.Combine(sandbox, "report.html");
                string scenePath = Path.ChangeExtension(path, ".scene.json");
                bool FilesExist() => File.Exists(path) && File.Exists(scenePath);
                // Step text names sandbox paths relative to the sandbox (its full path is already in the record).
                string Shown(string value) => value.StartsWith(sandbox, StringComparison.OrdinalIgnoreCase)
                    ? "<sandbox>" + value[sandbox.Length..] : value;
                var before = Directory.GetFiles(sandbox, "*", SearchOption.AllDirectories).ToHashSet();
                int pid = ui.Element.Current.ProcessId;
                // Arm before Invoke: even a lookup/cleanup failure must not capture the real Documents listing.
                // Keep this suppression for the remainder of this scenario, including an unsuccessful cancellation.
                record.OmitWindowDiagnosticsReason = "Native save dialog may display private folder contents.";
                // The native dialog runs a nested message loop; keep the UIA caller free to drive it.
                var invoke = Task.Run(() => ui.Invoke("ExportReport"));
                AutomationElement? dialog = null;
                try
                {
                    StudioWindow.WaitUntil(() =>
                    {
                        dialog = FindOwnedModalDialog(ui.Element, pid);
                        return dialog is not null;
                    }, TimeSpan.FromSeconds(10), "owned native save dialog");
                    var dialogHandle = new IntPtr(dialog!.Current.NativeWindowHandle);
                    if (!Native.BelongsTo(dialogHandle, pid))
                        throw new InvalidOperationException("The save dialog's window handle is not an owned Studio window.");
                    record.Step($"owned save dialog found: title \"{dialog.Current.Name}\"");

                    var fields = dialog.FindAll(TreeScope.Descendants, new AndCondition(
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "1001"),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));
                    var filename = Assert.Single(fields.Cast<AutomationElement>());
                    SetFileName(filename, path, record.Step, Shown);

                    var save = FindSaveButton(dialog, dialogHandle, pid, record.Step);
                    StudioWindow.Pattern<InvokePattern>(save, InvokePattern.Pattern).Invoke();
                    record.Step("Save invoked");
                    if (AwaitSaveOutcome(ui.Element, pid, dialog, dialogHandle, FilesExist, record.Step, "after Save"))
                    {
                        // Still at the dialog with no prompt and no file: commit the way a user does, Enter in the
                        // file-name edit. The key goes to the dialog's own windows only, never to the foreground.
                        SetFileName(filename, path, record.Step, Shown);
                        PostEnter(filename, dialogHandle, pid, record.Step);
                        AwaitSaveOutcome(ui.Element, pid, dialog, dialogHandle, FilesExist, record.Step, "after Enter fallback");
                    }
                    try
                    {
                        StudioWindow.WaitUntil(FilesExist, TimeSpan.FromSeconds(10), "two report files in owned sandbox");
                    }
                    catch (TimeoutException)
                    {
                        RecordState(ui.Element, pid, dialog, dialogHandle, FilesExist, record.Step, "report files timed out");
                        throw;
                    }
                    StudioWindow.WaitUntil(() => ui.IsEnabled("ExportReport") && invoke.IsCompleted,
                        TimeSpan.FromSeconds(10), "export completed and files closed");
                    invoke.GetAwaiter().GetResult();
                    string html = File.ReadAllText(path);
                    Assert.Contains(WebUtility.HtmlEncode(estimate), html);
                    foreach (string peak in found) Assert.Contains(WebUtility.HtmlEncode(peak), html);
                    var config = ConfigLoader.Load(scenePath);
                    Assert.NotNull(config.Seed);
                    Assert.Equal(new OpticsSettings().DetectorPixels, config.Detector.PixelsX);
                    var created = Directory.GetFiles(sandbox, "*", SearchOption.AllDirectories).Where(f => !before.Contains(f))
                        .Select(f => Path.GetRelativePath(sandbox, f)).Order().ToArray();
                    Assert.Equal(new[] { "report.html", "report.scene.json" }, created);
                    record.Step($"report contains displayed estimate: {estimate}; scene loads; only two declared sandbox writes");
                }
                finally
                {
                    // A selector/verdict failure must not leave the owned native dialog blocking graceful closure.
                    try
                    {
                        CancelOwnedModalDialogs(ui.Element, pid, dialog, record.Step);
                    }
                    catch (Exception error)
                    {
                        // Preserve the original verdict; the harness still checks graceful exit. No dialog contents.
                        record.Step($"Owned native dialog cleanup failed: {error.GetType().Name}");
                    }
                }
            });

    /// <summary>Sets the dialog's file-name edit through ValuePattern and reads it back; one retry with the edit
    /// focused. Already holding the path (the fallback's re-check) is logged and left alone.</summary>
    private static void SetFileName(AutomationElement edit, string path, Action<string> log, Func<string, string> shown)
    {
        var value = StudioWindow.Pattern<ValuePattern>(edit, ValuePattern.Pattern);
        if (SamePath(value.Current.Value, path))
        {
            log($"file-name edit (Edit, AutomationId \"1001\") still reads back \"{shown(value.Current.Value)}\"");
            return;
        }
        value.SetValue(path);
        string back = value.Current.Value;
        log($"file-name edit (Edit, AutomationId \"1001\") read back: \"{shown(back)}\"; equals the requested path: {SamePath(back, path)}");
        if (SamePath(back, path)) return;
        edit.SetFocus();
        value.SetValue(path);
        back = value.Current.Value;
        log($"file-name edit retry with focus, read back: \"{shown(back)}\"; equals the requested path: {SamePath(back, path)}");
        if (!SamePath(back, path))
            throw new InvalidOperationException("The save dialog's file-name edit did not take the sandbox path.");
    }

    private static bool SamePath(string? value, string path) => string.Equals(value?.Trim(), path, StringComparison.OrdinalIgnoreCase);

    /// <summary>The dialog's own OK (Save) push button: its direct dialog item first, then a Button — never any
    /// other control type — with AutomationId "1" among the dialog's children, then its descendants. Folder-view
    /// and navigation items can also carry AutomationId "1"; invoking one would navigate instead of saving.</summary>
    private static AutomationElement FindSaveButton(AutomationElement dialog, IntPtr dialogHandle, int pid, Action<string> log)
    {
        int defaultId = Native.DefaultButtonId(dialogHandle);
        AutomationElement? save = null;
        string foundAs = "dialog item";
        IntPtr item = Native.GetDlgItem(dialogHandle, IdOk);
        if (item != IntPtr.Zero && Native.BelongsTo(item, pid) && Native.GetAncestor(item, Native.RootAncestor) == dialogHandle)
        {
            var candidate = AutomationElement.FromHandle(item);
            if (candidate.Current.ControlType == ControlType.Button) save = candidate;
        }
        if (save is null)
        {
            var button = new AndCondition(
                new PropertyCondition(AutomationElement.ProcessIdProperty, pid),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.AutomationIdProperty, IdOk.ToString()));
            save = dialog.FindFirst(TreeScope.Children, button);
            foundAs = "dialog child Button";
            if (save is null)
            {
                save = dialog.FindFirst(TreeScope.Descendants, button);
                foundAs = "dialog descendant Button";
            }
        }
        string reportedDefault = defaultId > 0 ? defaultId.ToString() : "none";
        if (save is null)
        {
            log($"no Save button (Button, AutomationId \"1\") in the save dialog; dialog default button id: {reportedDefault}");
            throw new InvalidOperationException("The save dialog has no Save button (Button, AutomationId \"1\").");
        }
        var c = save.Current;
        log($"Save element to invoke: control type {c.ControlType.ProgrammaticName}, AutomationId \"{c.AutomationId}\", " +
            $"name \"{c.Name}\", enabled {c.IsEnabled}, found as {foundAs}; dialog default button id: {reportedDefault} " +
            $"(is default: {defaultId == IdOk})");
        return save;
    }

    /// <summary>Waits up to 5 s for the files, the dialog closing, or a further owned modal window, then records the
    /// state. True when the dialog is still open with no further window and no file.</summary>
    private static bool AwaitSaveOutcome(AutomationElement owner, int pid, AutomationElement dialog, IntPtr dialogHandle,
        Func<bool> filesExist, Action<string> log, string stage)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (filesExist() || !Native.IsOpen(dialogHandle) || OtherModalTitles(owner, pid, dialog).Count > 0) break;
            Thread.Sleep(100);
        }
        var (open, others, files) = RecordState(owner, pid, dialog, dialogHandle, filesExist, log, stage);
        return open && others == 0 && !files;
    }

    private static (bool Open, int Others, bool Files) RecordState(AutomationElement owner, int pid, AutomationElement dialog,
        IntPtr dialogHandle, Func<bool> filesExist, Action<string> log, string stage)
    {
        bool open = Native.IsOpen(dialogHandle);
        var titles = OtherModalTitles(owner, pid, dialog);
        bool files = filesExist();
        log($"{stage}: save dialog open: {open}; other owned modal windows: " +
            $"[{string.Join(", ", titles.Select(t => $"\"{t}\""))}]; report files present: {files}");
        return (open, titles.Count, files);
    }

    /// <summary>Titles only (never contents) of owned modal windows other than the save dialog: a prompt after Save,
    /// or Studio's own error message.</summary>
    private static IReadOnlyList<string> OtherModalTitles(AutomationElement owner, int pid, AutomationElement dialog)
    {
        var titles = new List<string>();
        try
        {
            foreach (var window in OwnedModalWindows(owner, pid))
            {
                try
                {
                    if (!Automation.Compare(window, dialog)) titles.Add(window.Current.Name);
                }
                catch (ElementNotAvailableException) { }
            }
        }
        catch (Exception error) when (error is ElementNotAvailableException or COMException) { }
        return titles;
    }

    /// <summary>Focuses the file-name edit and posts Enter to it (or to the dialog itself when the edit has no window
    /// of its own). Posted messages reach only that window; no input goes to whatever window is in the foreground.</summary>
    private static void PostEnter(AutomationElement edit, IntPtr dialogHandle, int pid, Action<string> log)
    {
        if (!Native.BelongsTo(dialogHandle, pid))
            throw new InvalidOperationException("The save dialog's window handle is no longer an owned Studio window.");
        edit.SetFocus();
        var target = new IntPtr(edit.Current.NativeWindowHandle);
        bool toEdit = target != IntPtr.Zero && Native.BelongsTo(target, pid)
            && Native.GetAncestor(target, Native.RootAncestor) == dialogHandle;
        if (!toEdit) target = dialogHandle;
        Native.PostEnter(target);
        log($"fallback: focused the file-name edit and posted Enter to {(toEdit ? "the file-name edit" : "the save dialog")}");
    }

    private static AutomationElement? FindOwnedModalDialog(AutomationElement owner, int pid)
        => OwnedModalWindows(owner, pid).FirstOrDefault();

    /// <summary>Owned modal windows of the Studio process, in tree order: the owner's subtree first, then desktop children.</summary>
    private static List<AutomationElement> OwnedModalWindows(AutomationElement owner, int pid)
    {
        var condition = new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty, pid),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
        var modal = new List<AutomationElement>();
        // Owned WPF dialogs appear beneath their owner in UIA, rather than as desktop children.
        Collect(owner.FindAll(TreeScope.Descendants, condition));
        Collect(AutomationElement.RootElement.FindAll(TreeScope.Children, condition));
        return modal;

        void Collect(AutomationElementCollection windows)
        {
            foreach (AutomationElement window in windows)
            {
                try
                {
                    if (window.TryGetCurrentPattern(WindowPattern.Pattern, out var pattern)
                        && ((WindowPattern)pattern).Current.IsModal && !modal.Any(m => Automation.Compare(m, window)))
                        modal.Add(window);
                }
                catch (ElementNotAvailableException) { } // a dialog can disappear between enumeration and inspection
            }
        }
    }

    /// <summary>Dismisses owned modal windows innermost first (a prompt above the save dialog before the dialog), each
    /// with a non-writing choice: Cancel, else No, else Close. Never Studio's main window or another process.</summary>
    private static void CancelOwnedModalDialogs(AutomationElement owner, int pid, AutomationElement? knownDialog, Action<string> log)
    {
        for (int round = 0; round < 4; round++)
        {
            // Re-find independently of the acquisition lookup, including when it timed out before assigning dialog.
            AutomationElement? window = null;
            StudioWindow.WaitUntil(() =>
            {
                var windows = OwnedModalWindows(owner, pid);
                window = windows.LastOrDefault(w => w.Current.IsEnabled) ?? windows.LastOrDefault()
                    ?? (round == 0 ? knownDialog : null);
                return true;
            }, TimeSpan.FromSeconds(5), "owned native dialog cleanup lookup");
            if (window is null) break;
            string title, how;
            try
            {
                if (window.Current.ProcessId != pid || !window.TryGetCurrentPattern(WindowPattern.Pattern, out var pattern)
                    || !((WindowPattern)pattern).Current.IsModal) break;
                title = window.Current.Name;
                how = Dismiss(window, pid);
            }
            catch (ElementNotAvailableException) { continue; }   // it closed on its own
            log($"cleanup: owned modal window \"{title}\" dismissed by {how}");
            try
            {
                StudioWindow.WaitUntil(() => !OwnedModalWindows(owner, pid).Any(w => Automation.Compare(w, window)),
                    TimeSpan.FromSeconds(3), "dismissed owned modal window gone");
            }
            catch (TimeoutException) { /* the next round re-finds what is left; the final wait below decides */ }
        }
        StudioWindow.WaitUntil(() => FindOwnedModalDialog(owner, pid) is null,
            TimeSpan.FromSeconds(5), "owned native dialog closed before scenario teardown");
    }

    private static string Dismiss(AutomationElement window, int pid)
    {
        foreach (var (id, name) in new[] { (IdCancel, "Cancel"), (IdNo, "No") })
        {
            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ProcessIdProperty, pid),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.AutomationIdProperty, id.ToString()));
            var button = window.FindFirst(TreeScope.Children, condition) ?? window.FindFirst(TreeScope.Descendants, condition);
            if (button is null || !button.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) continue;
            try
            {
                ((InvokePattern)invoke).Invoke();
                return name;
            }
            catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException or COMException) { }
        }
        // Missing/unusable Cancel and No: close only this re-identified owned modal window.
        if (window.TryGetCurrentPattern(WindowPattern.Pattern, out var close))
        {
            ((WindowPattern)close).Close();
            return "Close";
        }
        return "nothing (no Cancel, No or Close)";
    }

    /// <summary>Win32 checks and the targeted Enter. Every call acts on a handle verified to belong to the owned
    /// Studio process; nothing is sent to the foreground or to another process.</summary>
    private static class Native
    {
        public const uint RootAncestor = 2;   // GA_ROOT
        private const uint DmGetDefId = 0x0400, WmKeyDown = 0x0100, WmKeyUp = 0x0101, VkReturn = 0x0D;
        private const int DcHasDefId = 0x534B;
        private const uint SmtoAbortIfHung = 0x0002;

        public static bool BelongsTo(IntPtr window, int pid)
            => window != IntPtr.Zero && IsWindow(window) && GetWindowThreadProcessId(window, out uint owner) != 0 && owner == (uint)pid;

        public static bool IsOpen(IntPtr window) => IsWindow(window) && IsWindowVisible(window);

        /// <summary>The dialog's default push-button id (DM_GETDEFID), or 0 when it reports none or does not answer.</summary>
        public static int DefaultButtonId(IntPtr dialog)
        {
            if (SendMessageTimeout(dialog, DmGetDefId, IntPtr.Zero, IntPtr.Zero, SmtoAbortIfHung, 1000, out var result) == IntPtr.Zero)
                return 0;
            long value = result.ToInt64();
            return (int)((value >> 16) & 0xFFFF) == DcHasDefId ? (int)(value & 0xFFFF) : 0;
        }

        public static void PostEnter(IntPtr window)
        {
            const long down = 0x001C0001, up = 0xC01C0001;   // repeat 1, scan code 0x1C; key-up sets bits 30 and 31
            if (!PostMessage(window, WmKeyDown, (IntPtr)VkReturn, (IntPtr)down) || !PostMessage(window, WmKeyUp, (IntPtr)VkReturn, unchecked((IntPtr)up)))
                throw new InvalidOperationException($"PostMessage failed: {Marshal.GetLastWin32Error()}");
        }

        [DllImport("user32.dll")] public static extern IntPtr GetDlgItem(IntPtr dialog, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam,
            uint flags, uint timeoutMs, out IntPtr result);
    }
}
