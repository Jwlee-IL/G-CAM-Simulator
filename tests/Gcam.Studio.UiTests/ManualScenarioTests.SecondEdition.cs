using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Windows.Automation;
using System.Xml.Linq;
using Gcam.Configuration;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed partial class ManualScenarioTests
{
    [DesktopFact, ManualChapter("manual.export")]
    public void Export_ReportsAndEvents_WriteOnlyDeclaredFiles() => ManualScenario.RunExport(output, (ui, run) =>
    {
        run.BeginStep("Set a short live time and click {StartAcquisition}; wait until the acquisition completes.", "N-SIM", "N-NOT-SAVED");
        run.CheckEqual("export.initially-disabled", false, ui.IsEnabled("ExportReport"));
        ui.SetText("AcquisitionLiveTime", "1", "StartAcquisition");
        run.CheckEqual("export.acquired", "Completed", ui.Acquire(RunTimeout));
        StudioWindow.WaitUntil(() => ui.Exists("PeakText") && ui.Text("PeakText").StartsWith("peak ("), TimeSpan.FromSeconds(10), "estimate");
        string estimate = ui.Text("PeakText"); long count = ui.Counts;
        run.Check("export.has-data", count > (Broken ? long.MaxValue : 0), "positive retained event population");
        run.Capture("An acquired snapshot can be exported.", [new("ExportReport"), new("StatusText")]);

        run.BeginStep("Open {ExportReport}. Choose HTML, PDF, Word or Event data. Cancel closes the format popup without writing.", "N-EXPORT-SCOPE");
        ui.Invoke("ExportReport");
        StudioWindow.WaitUntil(() => ui.Exists("Export.Confirm"), TimeSpan.FromSeconds(5), "format popup");
        run.Check("export.formats", new[] { "Html", "Pdf", "Word", "Csv" }.All(f => ui.Exists("Export." + f)), "four visible format choices");
        // The four choices sit 54 px apart: marks on all of them leave no room for the labels, so the figure marks the
        // first report format, the event data and the confirm button; PDF and Word are visible between them.
        run.Capture("Report and event-data formats.", [new("Export.Html"), new("Export.Csv"), new("Export.Confirm")], full: true);
        ui.Invoke("Modal.Cancel");
        StudioWindow.WaitUntil(() => !ui.Exists("Export.Confirm"), TimeSpan.FromSeconds(5), "format popup closed");
        run.Check("export.cancel-writes-nothing", !Directory.EnumerateFiles(run.Sandbox, "report.*", SearchOption.AllDirectories).Any(), "no report after Cancel");

        foreach (var (format, extension) in new[] { ("Html", "html"), ("Pdf", "pdf"), ("Word", "docx"), ("Csv", "csv") })
        {
            run.BeginStep($"Open {{ExportReport}}, choose {format} and click {{Export.Confirm}}. In the save dialog enter a destination, check the filename and Save. Keep the same-name scene sidecar" + (extension == "csv" ? " and metadata sidecar." : "."));
            ui.Invoke("ExportReport");
            StudioWindow.WaitUntil(() => ui.Exists("Export.Confirm"), TimeSpan.FromSeconds(5), "format popup");
            run.ResolveLabels(); // Export.Confirm exists only while the format popup is open
            string path = Path.Combine(run.Sandbox, extension, "report." + extension);
            ReportExportDialog.Save(ui, run.Record!, run.Sandbox, path, format);
            CheckExport(ui, run, path, extension, estimate, count);
        }
        run.Capture("Studio after the exports; the acquisition is retained.", [new("ExportReport"), new("StatusText")]);
    });

    private static void CheckExport(StudioWindow ui, ManualRun run, string path, string extension, string estimate, long count)
    {
        string scene = Path.ChangeExtension(path, ".scene.json"); var config = ConfigLoader.Load(scene);
        run.Check("export.scene-" + extension, config.Seed is not null && config.Source.EnergyKeV > 0, "scene parses and records resolved inputs");
        if (extension == "html")
        {
            string html = File.ReadAllText(path);
            run.Check("export.html-content", html.Contains(WebUtility.HtmlEncode(estimate)) && html.Contains("Simulation") &&
                !html.Contains("<script", StringComparison.OrdinalIgnoreCase) && !System.Text.RegularExpressions.Regex.IsMatch(html, "(?:src|href)=[\"'](?:https?:|//)", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "self-contained simulated report with captured estimate");
        }
        else if (extension == "pdf")
        {
            using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(path, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            run.Check("export.pdf-content", pdf.PageCount > 0 && ReportDocumentTests.PdfText(pdf).Contains(estimate), "PDF reader finds captured estimate");
        }
        else if (extension == "docx")
        {
            using var zip = ZipFile.OpenRead(path); using var xml = zip.GetEntry("word/document.xml")!.Open();
            run.Check("export.word-content", XDocument.Load(xml).Root!.Value.Contains(estimate) && zip.Entries.Any(e => e.FullName.StartsWith("word/media/")), "Word XML and figure entries");
        }
        else
        {
            string text = File.ReadAllText(path); string[] rows = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            run.CheckEqual("export.csv-header", "index,arrival_time_s,pixel_x,pixel_y,deposit_keV,measured_keV", rows[0]);
            run.CheckEqual("export.csv-count", count, rows.LongLength - 1);
            foreach (string row in rows.Skip(1))
            {
                string[] cells = row.Split(',');
                run.Check("export.csv-row", cells.Length == 6 && cells.Take(5).All(c => double.TryParse(c, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && double.IsFinite(n)) &&
                    (cells[5] == "" || double.TryParse(cells[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double energy) && double.IsFinite(energy)), "six invariant numeric/optional fields");
            }
            using var meta = JsonDocument.Parse(File.ReadAllBytes(Path.ChangeExtension(path, ".meta.json")));
            run.CheckEqual("export.csv-meta-count", count, meta.RootElement.GetProperty("eventCount").GetInt64());
            run.CheckEqual("export.csv-meta-file", "report.csv", meta.RootElement.GetProperty("files").GetProperty("events").GetString());
        }
        var files = Directory.GetFiles(Path.GetDirectoryName(path)!).Select(Path.GetFileName).Order().ToArray();
        string[] expected = extension == "csv" ? ["report.csv", "report.meta.json", "report.scene.json"] : ["report." + extension, "report.scene.json"];
        run.CheckEqual("export.file-set-" + extension, string.Join('|', expected.Order()), string.Join('|', files));
        run.CheckEqual("export.keeps-count-" + extension, count, ui.Counts);
    }

    [DesktopFact, ManualChapter("manual.compare")]
    public void Compare_PhysicalPrefix_DisplaysConsistentPopulations() => ManualScenario.Run("manual.compare", output, (ui, run) =>
    {
        run.BeginStep("Choose {Workspace.Compare}. With direct crystal assignment the physical comparison is unavailable; follow {Readout.ChooseReadout}.", "N-SIM", "N-READOUT");
        ui.Workspace("Compare", "Readout.EmptyState");
        run.CheckEqual("compare.direct-empty", "Available with the experimental physical readout", ui.Text("Readout.EmptyState"));
        run.Capture("Compare requires the experimental physical readout.", [new("Workspace.Compare"), new("Readout.ChooseReadout")], full: true);
        ui.Invoke("Readout.ChooseReadout");
        run.BeginStep("Choose Baseline in {Optics.Preset:label}, then FourOutputAnger in {Readout.Mode:label}. Set seed 47001, ambient dose 0 and live time 10 s. Click {Readout.Prepare} and wait for Ready.");
        ui.SelectLeftTab("Optics"); ui.Choose("Optics.Preset", "Baseline");
        run.ResolveLabels(); // the preset's label is visible only on the Optics tab
        ui.SelectLeftTab("Detector"); ui.Choose("Readout.Mode", "FourOutputAnger");
        ui.SetOption("AcquisitionSeed", "47001"); ui.SetOption("Acquisition.AmbientDose", "0");
        ui.SetText("AcquisitionLiveTime", "10", "SourceY"); ui.Invoke("Readout.Prepare");
        StudioWindow.WaitUntil(() => ui.Text("Readout.State").StartsWith("Ready", StringComparison.Ordinal), TimeSpan.FromSeconds(90), "Baseline preparation");
        run.Check("compare.ready", ui.Text("Readout.State").StartsWith("Ready"), "Baseline prepared");
        run.Capture("Baseline physical readout prepared.", [new("Readout.Prepare"), new("Readout.State")]);
        run.BeginStep("Click {StartAcquisition}; after completion choose {Workspace.Compare}. Both paths show the same retained prefix, with direct ideal deposit energy beside the acquired Anger path.");
        run.CheckEqual("compare.acquired", "Completed", ui.Acquire(TimeSpan.FromSeconds(60)));
        ui.Workspace("Compare", "Compare.DirectFlood");
        StudioWindow.WaitUntil(() => ui.Text("Compare.Prefix").Contains("10 s"), TimeSpan.FromSeconds(20), "10 s pair");
        run.Check("compare.prefix", ui.Text("Compare.Prefix").Contains(Broken ? "11 s" : "10 s"), "both paths at completed prefix");
        run.Capture("Direct crystal and physical readout, same acquired prefix.", [new("Workspace.Compare"), new("Compare.Prefix")], full: true);
        run.BeginStep("Read the population rows and the same-crystal share beneath them. Histories and held conversions are different populations. "
            + "Further down the panel (scroll), Reference cost · 1 m is a benchmark, not a prediction for this scene.");
        string[] text = ui.Element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
            .Cast<AutomationElement>().Select(e => e.Current.Name).ToArray();
        var population = ComparePopulationOracle.Read(text);
        run.Check("compare.population-arithmetic", ComparePopulationOracle.Consistent(population, ui.Text("Compare.SameShare")), "assigned + unknown = held; same/eligible agrees with rounded share; different = eligible - same");
        run.Check("compare.reference", ui.Text("Compare.Reference").Contains("reference only"), "benchmark not a scene prediction");
        // The reference sits below the population rows, outside the panel's view at the normalised window size; the
        // harness may not scroll it (no policy entry), so the figure shows the share and the check reads the reference.
        run.Capture("Population share and the rows beneath it.", [new("Compare.SameShare")], full: true);
        run.BeginStep("Turn on {Compare.AssignmentDifference}: the flood row becomes different / eligible per direct crystal. Reconstructions remain below it.");
        ui.Toggle("Compare.AssignmentDifference");
        run.Check("compare.difference", Visible(ui, "Compare.Difference") && Visible(ui, "Compare.DirectRecon") && Visible(ui, "Compare.AngerRecon"), "difference and retained reconstructions");
        run.Capture("Assignment disagreement with the reconstructions retained.", [new("Compare.AssignmentDifference")], full: true);
        run.BeginStep("Click {ResetAcquisition} and confirm with {Reset.Discard}. Compare returns to its start state.");
        ui.Invoke("ResetAcquisition");
        StudioWindow.WaitUntil(() => ui.Exists("Reset.Message"), TimeSpan.FromSeconds(5), "reset confirmation opened");
        run.ResolveLabels(); // Discard exists only while the confirmation is open
        ui.ConfirmReset(true);
        run.CheckEqual("compare.reset", "Start an acquisition to compare", ui.Text("Compare.StartState"));
    });
}
