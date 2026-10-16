using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Gcam.Configuration;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

using static Gcam.Studio.UiTests.Harness.ReportExportDialog;

namespace Gcam.Studio.UiTests;

/// <summary>Compiled desktop scenario for Studio's first explicit file-write surface. Run only on author go.</summary>
/// <remarks>While the native save dialog is open the run record holds step text only: no screenshot, no tree dump and
/// no folder contents. The steps name the invoked element (control type, AutomationId, name), the file name read back
/// from the dialog, and the titles of any further owned modal windows.</remarks>
public sealed class ReportExportScenarioTests(ITestOutputHelper output)
{
    /// <summary>The common item dialog's OK (Save) and Cancel buttons; "No" of a Yes/No prompt.</summary>
    private const int IdOk = 1, IdCancel = 2, IdNo = 7;

    [DesktopTheory]
    [InlineData("Html", "html")]
    [InlineData("Pdf", "pdf")]
    [InlineData("Word", "docx")]
    [InlineData("Csv", "csv")]
    public void ExportReport_SaveDialogWritesIntoOwnedSandbox_AndMatchesDisplayedEstimate(string format, string extension)
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
                // The typed name carries the run ID (D4): the save dialog's records of it carry a harness marker.
                string path = Path.Combine(sandbox, ReportExportDialog.FileName(record.RunId, extension));
                string scenePath = Path.ChangeExtension(path, ".scene.json");
                string metaPath = Path.ChangeExtension(path, ".meta.json");
                string[] declared = ReportExportDialog.DeclaredFiles(record.RunId, extension);
                bool FilesExist() => declared.All(name => File.Exists(Path.Combine(sandbox, name)));
                // Step text names sandbox paths relative to the sandbox (its full path is already in the record).
                string Shown(string value) => value.StartsWith(sandbox, StringComparison.OrdinalIgnoreCase)
                    ? "<sandbox>" + value[sandbox.Length..] : value;
                var before = Directory.GetFiles(sandbox, "*", SearchOption.AllDirectories).ToHashSet();
                int pid = ui.Element.Current.ProcessId;
                // Arm before Invoke: even a lookup/cleanup failure must not capture the real Documents listing.
                // Keep this suppression for the remainder of this scenario, including an unsuccessful cancellation.
                record.OmitWindowDiagnosticsReason = "Native save dialog may display private folder contents.";
                // The native dialog runs a nested message loop; keep the UIA caller free to drive it.
                ui.Invoke("ExportReport");
                StudioWindow.WaitUntil(() => ui.Exists("Export.Confirm"), TimeSpan.FromSeconds(5), "export popup opened");
                ui.Select("Export." + format);
                var invoke = Task.Run(() => ui.Invoke("Export.Confirm"));
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
                        StudioWindow.WaitUntil(FilesExist, TimeSpan.FromSeconds(10), $"{declared.Length} export files in owned sandbox");
                    }
                    catch (TimeoutException)
                    {
                        RecordState(ui.Element, pid, dialog, dialogHandle, FilesExist, record.Step, "report files timed out");
                        throw;
                    }
                    StudioWindow.WaitUntil(() => ui.IsEnabled("ExportReport") && invoke.IsCompleted,
                        TimeSpan.FromSeconds(10), "export completed and files closed");
                    invoke.GetAwaiter().GetResult();
                    if (extension == "html")
                    {
                        string html = File.ReadAllText(path);
                        Assert.Contains(WebUtility.HtmlEncode(estimate), html);
                        foreach (string peak in found) Assert.Contains(WebUtility.HtmlEncode(peak), html);
                    }
                    else if (extension == "pdf")
                    {
                        using var document = PdfSharp.Pdf.IO.PdfReader.Open(path, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
                        Assert.Contains(estimate, ReportDocumentTests.PdfText(document));
                    }
                    else if (extension == "csv")
                    {
                        // Event data, not a report: the displayed estimate is not in it. The direct assignment's
                        // header, one row per retained event (the displayed count), and the meta file naming it.
                        var lines = File.ReadAllText(path).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                        Assert.Equal("index,arrival_time_s,pixel_x,pixel_y,deposit_keV,measured_keV", lines[0]);
                        Assert.True(lines.Length > 1, "The CSV holds no event rows.");
                        Assert.All(lines.Skip(1), line => Assert.Equal(6, line.Split(',').Length));
                        using var meta = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(metaPath));
                        Assert.Equal("gcam.studio.events", meta.RootElement.GetProperty("schema").GetString());
                        Assert.Equal("DirectCrystal", meta.RootElement.GetProperty("readout").GetProperty("mode").GetString());
                        Assert.Equal(Path.GetFileName(path), meta.RootElement.GetProperty("files").GetProperty("events").GetString());
                        Assert.Equal(lines.Length - 1, meta.RootElement.GetProperty("eventCount").GetInt32());
                    }
                    else
                    {
                        using var zip = System.IO.Compression.ZipFile.OpenRead(path);
                        using var document = zip.GetEntry("word/document.xml")!.Open();
                        Assert.Contains(estimate, System.Xml.Linq.XDocument.Load(document).Root!.Value);
                    }
                    var config = ConfigLoader.Load(scenePath);
                    Assert.NotNull(config.Seed);
                    Assert.Equal(new OpticsSettings().DetectorPixels, config.Detector.PixelsX);
                    var created = Directory.GetFiles(sandbox, "*", SearchOption.AllDirectories).Where(f => !before.Contains(f))
                        .Select(f => Path.GetRelativePath(sandbox, f)).Order().ToArray();
                    Assert.Equal(declared.Order(), created);
                    record.Step(extension == "csv"
                        ? $"event data, meta and scene written; scene loads; only {declared.Length} declared sandbox writes"
                        : $"report contains displayed estimate: {estimate}; scene loads; only {declared.Length} declared sandbox writes");
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
            }, extension);

}
