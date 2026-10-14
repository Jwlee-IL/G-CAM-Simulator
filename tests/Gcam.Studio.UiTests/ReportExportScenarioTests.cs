using System.Net;
using System.Windows.Automation;
using Gcam.Configuration;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>Compiled desktop scenario for Studio's first explicit file-write surface. Run only on author go.</summary>
public sealed class ReportExportScenarioTests(ITestOutputHelper output)
{
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
                    var fields = dialog!.FindAll(TreeScope.Descendants, new AndCondition(
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "1001"),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)));
                    var filename = Assert.Single(fields.Cast<AutomationElement>());
                    ((ValuePattern)filename.GetCurrentPattern(ValuePattern.Pattern)).SetValue(path);
                    var save = dialog.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "1"));
                    Assert.NotNull(save);
                    ((InvokePattern)save.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                    StudioWindow.WaitUntil(() => File.Exists(path) && File.Exists(Path.ChangeExtension(path, ".scene.json")),
                        TimeSpan.FromSeconds(10), "two report files in owned sandbox");
                    StudioWindow.WaitUntil(() => ui.IsEnabled("ExportReport") && invoke.IsCompleted,
                        TimeSpan.FromSeconds(10), "export completed and files closed");
                    invoke.GetAwaiter().GetResult();
                    string html = File.ReadAllText(path);
                    Assert.Contains(WebUtility.HtmlEncode(estimate), html);
                    foreach (string peak in found) Assert.Contains(WebUtility.HtmlEncode(peak), html);
                    var config = ConfigLoader.Load(Path.ChangeExtension(path, ".scene.json"));
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
                        CancelOwnedModalDialog(ui.Element, pid, dialog);
                    }
                    catch (Exception error)
                    {
                        // Preserve the original verdict; the harness still checks graceful exit. No dialog contents.
                        record.Step($"Owned native dialog cleanup failed: {error.GetType().Name}");
                    }
                }
            });

    private static AutomationElement? FindOwnedModalDialog(AutomationElement owner, int pid)
    {
        var condition = new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty, pid),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
        // Owned WPF dialogs appear beneath their owner in UIA, rather than as desktop children.
        return Modal(owner.FindAll(TreeScope.Descendants, condition))
            ?? Modal(AutomationElement.RootElement.FindAll(TreeScope.Children, condition));

        static AutomationElement? Modal(AutomationElementCollection windows)
        {
            foreach (AutomationElement window in windows)
            {
                try
                {
                    if (window.TryGetCurrentPattern(WindowPattern.Pattern, out var pattern)
                        && ((WindowPattern)pattern).Current.IsModal) return window;
                }
                catch (ElementNotAvailableException) { } // a dialog can disappear between enumeration and inspection
            }
            return null;
        }
    }

    private static void CancelOwnedModalDialog(AutomationElement owner, int pid, AutomationElement? knownDialog)
    {
        // Re-find independently of the acquisition lookup, including when it timed out before assigning dialog.
        AutomationElement? dialog = null;
        StudioWindow.WaitUntil(() =>
        {
            dialog = FindOwnedModalDialog(owner, pid) ?? knownDialog;
            return true;
        }, TimeSpan.FromSeconds(5), "owned native dialog cleanup lookup");
        if (dialog is null) return;
        try
        {
            if (dialog.Current.ProcessId != pid || !dialog.TryGetCurrentPattern(WindowPattern.Pattern, out var pattern)
                || !((WindowPattern)pattern).Current.IsModal) return;
            var cancel = dialog.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ProcessIdProperty, pid),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.AutomationIdProperty, "2")));
            if (cancel is not null && cancel.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
            {
                try { ((InvokePattern)invoke).Invoke(); }
                catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException
                    or System.Runtime.InteropServices.COMException) { /* Close fallback below re-finds the dialog. */ }
            }
        }
        catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException
            or System.Runtime.InteropServices.COMException) { }

        // Missing/unusable Cancel: close only a re-identified owned modal dialog, never Studio or another process.
        dialog = FindOwnedModalDialog(owner, pid);
        try
        {
            if (dialog is not null && dialog.TryGetCurrentPattern(WindowPattern.Pattern, out var close))
                ((WindowPattern)close).Close();
        }
        catch (ElementNotAvailableException) { }
        StudioWindow.WaitUntil(() => FindOwnedModalDialog(owner, pid) is null,
            TimeSpan.FromSeconds(5), "owned native dialog closed before scenario teardown");
    }
}
