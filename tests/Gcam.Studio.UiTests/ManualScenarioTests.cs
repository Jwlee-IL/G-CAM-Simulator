using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using Gcam.Configuration;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>
/// The manual's generated chapters (docs/manual/chapters.json), one desktop scenario each, written as reader steps.
/// Every step's checks are automatic and use the same independent oracles as the regression scenarios; captures are
/// document evidence, never verdicts. Chapter numbers and titles come from the registry; warnings only as notice IDs.
/// With GCAM_UI_BREAK_VERDICT=1 each scenario corrupts one expectation, so its chapter must drop out of the edition.
/// </summary>
public sealed class ManualScenarioTests(ITestOutputHelper output)
{
    private static readonly TimeSpan RunTimeout = PilotTests.RunTimeout;
    private static bool Broken => ManualRun.Broken;

    private static AutomationElement Find(StudioWindow ui, string id) => ui.ById(id, ui.Element);
    private static bool Selected(StudioWindow ui, string id) =>
        StudioWindow.Pattern<SelectionItemPattern>(Find(ui, id), SelectionItemPattern.Pattern).Current.IsSelected;
    private static bool Visible(StudioWindow ui, string id) => ui.Exists(id) && !Find(ui, id).Current.IsOffscreen;

    [DesktopFact, ManualChapter("manual.navigate")]
    public void Navigate_WindowWorkspacesAndViewTabs() => ManualScenario.Run("manual.navigate", output, (ui, run) =>
    {
        run.BeginStep("GCAM Studio opens with the top bar — the workspace switch {Workspace.Imaging}, {Workspace.Spectrum}, "
            + "{Workspace.Waveform}, {Workspace.Detector} and the {StartAcquisition} button — the left panel with the tabs "
            + "{Left.SceneTab}, {Left.OpticsTab} and {Left.DetectorTab}, the workspace in the centre and the status bar at the bottom.");
        run.CheckEqual("window.starts-empty", Broken ? "Acquiring" : "Empty", ui.RunState);
        run.Check("window.imaging-first", Visible(ui, "FloodView") && Visible(ui, "ReconView"), "Imaging shows the flood map and the reconstruction");
        run.Capture("The window after start, Imaging workspace.", [new("Workspace.Imaging"), new("Workspace.Spectrum"), new("Workspace.Waveform"),
            new("Workspace.Detector"), new("StartAcquisition"), new("Left.SceneTab"), new("Left.OpticsTab"), new("Left.DetectorTab")], full: true);

        run.BeginStep("Select the {Left.OpticsTab} tab: the engineering geometry replaces the source list in the left panel.");
        ui.SelectLeftTab("Optics");
        run.Check("navigation.left-tab", Selected(ui, "Left.OpticsTab") && Visible(ui, "Optics.Preset"), "Optics tab selected, its preset visible");
        run.Capture("The Optics tab.", [new("Left.OpticsTab"), new("Optics.Preset", LabelSource.PrecedingText)], region: "Optics.Section");

        run.BeginStep("Choose {Workspace.Spectrum} in the workspace switch: the centre shows the spectrum instead of the images.");
        ui.Workspace("Spectrum", "Spectrum.Plot");
        run.Check("navigation.workspace-surface", Visible(ui, "Spectrum.Plot") && !ui.Exists("FloodView"), "Spectrum plot shown, flood map gone");

        run.BeginStep("Choose {Workspace.Waveform}, then its view tab {Waveform.FourOutputsTab}. With the default direct crystal "
            + "assignment there are no four outputs: the view reads {Readout.EmptyState} and offers {Readout.ChooseReadout}.");
        ui.Workspace("Waveform", "Waveform.Views");
        ui.Select("Waveform.FourOutputsTab");
        StudioWindow.WaitUntil(() => ui.Exists("Readout.EmptyState"), TimeSpan.FromSeconds(5), "four-output empty state");
        run.CheckEqual("navigation.waveform-empty-state", "Available with the experimental physical readout", ui.Text("Readout.EmptyState"));
        run.Capture("Four outputs under the direct crystal assignment.", [new("Waveform.FourOutputsTab"), new("Readout.EmptyState"),
            new("Readout.ChooseReadout")], region: "Waveform.Views");

        run.BeginStep("Choose {Workspace.Detector}, then its view tab {Readout.SipmTab}: the physical readout views are empty in the same way.");
        ui.Workspace("Detector", "Detector.Views");
        ui.Select("Readout.SipmTab");
        StudioWindow.WaitUntil(() => ui.Exists("Readout.EmptyState"), TimeSpan.FromSeconds(5), "detector readout empty state");
        run.Check("navigation.detector-empty-state", Selected(ui, "Readout.SipmTab") && Visible(ui, "Readout.ChooseReadout"),
            "SiPM tab selected, readout choice offered");
        run.Capture("A Detector readout view under the direct crystal assignment.", [new("Readout.SipmTab"), new("Readout.EmptyState"),
            new("Readout.ChooseReadout")], region: "Detector.Views");

        run.BeginStep("Click {Readout.ChooseReadout}: the left panel switches to its {Left.DetectorTab} tab with the keyboard focus "
            + "on the readout choice.", "N-READOUT");
        ui.Invoke("Readout.ChooseReadout");
        StudioWindow.WaitUntil(() => Selected(ui, "Left.DetectorTab") && Find(ui, "Readout.Mode").Current.HasKeyboardFocus,
            TimeSpan.FromSeconds(5), "readout choice focused");
        run.Check("navigation.choose-readout", Selected(ui, "Left.DetectorTab") && Find(ui, "Readout.Mode").Current.HasKeyboardFocus,
            "left Detector tab selected and the readout mode focused");
        run.Capture("The readout choice in the left Detector tab.", [new("Left.DetectorTab")], region: "Readout.Mode");

        run.BeginStep("Choose {Workspace.Imaging} to return to the images.");
        ui.Workspace("Imaging", "FloodView");
        run.Check("navigation.back-to-imaging", Visible(ui, "FloodView"), "flood map shown again");
    });

    [DesktopFact, ManualChapter("manual.sources")]
    public void Sources_PlaceEditAddRemove_LockedUntilReset() => ManualScenario.Run("manual.sources", output, (ui, run) =>
    {
        run.BeginStep("Select the {Left.SceneTab} tab. The source list holds one source; the selected source is described by "
            + "{SourceIsotope:label}, {SourceX:label}, {SourceY:label}, {SourceDistance:label} and {SourceActivity:label}.");
        ui.SelectLeftTab("Scene");
        run.CheckEqual("sources.one-default-source", 1, ui.Rows("SourceList").Count);
        run.CheckEqual("sources.default-isotope", "Cs-137", ui.SelectedItem("SourceIsotope"));
        run.CheckEqual("sources.default-position", Broken ? ("1", "0") : ("0", "0"), (ui.Value("SourceX"), ui.Value("SourceY")));
        run.Capture("The Scene tab with the default source.", [new("SourceIsotope", LabelSource.PrecedingText), new("SourceX", LabelSource.PrecedingText),
            new("SourceY", LabelSource.PrecedingText), new("SourceDistance", LabelSource.PrecedingText), new("SourceActivity", LabelSource.PrecedingText)],
            region: "SourceList");

        run.BeginStep("Type -20 in {SourceX:label} and leave the field (Tab or a click elsewhere): the value is taken when the field loses focus.");
        ui.SetText("SourceX", "-20", "SourceY");
        run.CheckEqual("sources.edit-commits", "-20", ui.Value("SourceX"));

        run.BeginStep("Click the icon button whose tooltip reads {AddSource}: a second source is added to the list and selected.");
        ui.Invoke("AddSource");
        StudioWindow.WaitUntil(() => ui.Rows("SourceList").Count == 2, TimeSpan.FromSeconds(3), "second source row");
        run.CheckEqual("sources.add", 2, ui.Rows("SourceList").Count);

        run.BeginStep("For the new source choose Co-60 in {SourceIsotope:label}, type 20 in {SourceX:label} and 400 in {SourceActivity:label}.");
        ui.Choose("SourceIsotope", "Co-60");
        ui.SetText("SourceX", "20", "SourceY");
        ui.SetText("SourceActivity", "400", "SourceY");
        run.CheckEqual("sources.second-source", ("Co-60", "20", "400"), (ui.SelectedItem("SourceIsotope"), ui.Value("SourceX"), ui.Value("SourceActivity")));
        run.Capture("Two sources; the selected one is edited below the list.", [new("SourceIsotope", LabelSource.PrecedingText),
            new("SourceX", LabelSource.PrecedingText), new("SourceActivity", LabelSource.PrecedingText)], region: "SourceList");

        run.BeginStep("Click the icon button whose tooltip reads {RemoveSource}: the selected source is removed.");
        ui.Invoke("RemoveSource");
        StudioWindow.WaitUntil(() => ui.Rows("SourceList").Count == 1, TimeSpan.FromSeconds(3), "one source row");
        run.CheckEqual("sources.remove", ("Cs-137", "-20"), (ui.SelectedItem("SourceIsotope"), ui.Value("SourceX")));

        run.BeginStep("Click {StartAcquisition} and wait until the acquisition completes. While acquired data exist the source "
            + "fields and the add and remove buttons are locked, so the images always belong to the scene shown.");
        run.CheckEqual("sources.acquired", "Completed", ui.Acquire(RunTimeout));
        run.Check("sources.locked-with-data", !ui.IsEnabled("SourceX") && !ui.IsEnabled("AddSource") && !ui.IsEnabled("RemoveSource"),
            "source fields, add and remove disabled");
        run.Capture("Source fields locked while data exist.", [new("SourceX", LabelSource.PrecedingText), new("SourceActivity", LabelSource.PrecedingText)],
            region: "SourceList");

        run.BeginStep("Click the icon button whose tooltip reads {ResetAcquisition}: a confirmation asks before the data are discarded.");
        ui.Invoke("ResetAcquisition");
        StudioWindow.WaitUntil(() => ui.Exists("Reset.Message"), TimeSpan.FromSeconds(5), "reset confirmation opened");
        run.Check("sources.reset-asks", ui.Exists("Reset.Discard") && ui.Exists("Modal.Cancel"), "Discard and Cancel offered");

        run.BeginStep("Confirm with {Reset.Discard}: the acquisition is discarded and the source fields unlock for the next acquisition.");
        run.ResolveLabels();
        ui.Invoke("Reset.Discard");
        StudioWindow.WaitUntil(() => ui.RunState == "Empty", TimeSpan.FromSeconds(5), "acquisition reset");
        run.Check("sources.unlocked-after-reset", ui.IsEnabled("SourceX") && ui.IsEnabled("AddSource"), "source fields and add enabled");
    });

    [DesktopFact, ManualChapter("manual.geometry")]
    public void Geometry_PresetCustomAndRefusal_DetectorFaceFollows() => ManualScenario.Run("manual.geometry", output, (ui, run) =>
    {
        string[] fields = ["Optics.Rank", "Optics.CellPitch", "Optics.Distance", "Optics.Pixels", "Optics.PixelPitch"];
        run.BeginStep("Select the {Left.OpticsTab} tab. {Optics.Preset:label} shows Sharp, the default engineering geometry. A preset "
            + "writes its five values — {Optics.Rank:label}, {Optics.CellPitch:label}, {Optics.Distance:label}, {Optics.Pixels:label} and "
            + "{Optics.PixelPitch:label} — into the fields and keeps them read-only.");
        ui.SelectLeftTab("Optics");
        run.CheckEqual("optics.default-preset", "Sharp", ui.SelectedItem("Optics.Preset"));
        run.Check("optics.preset-locks-fields", fields.All(f => !ui.IsEnabled(f)), "all five fields disabled under a preset");
        run.Capture("The Optics tab with the default preset.", [new("Optics.Preset", LabelSource.PrecedingText), .. fields.Select(f => new Mark(f, LabelSource.PrecedingText))],
            region: "Optics.Section");

        run.BeginStep("Choose Baseline in {Optics.Preset:label}: the fields now hold the 12 × 12 pixel, rank-7 reference geometry, still read-only.");
        ui.Choose("Optics.Preset", "Baseline");
        // Oracle: the documented reference camera (rank-7 MURA over a 12 × 12 crystal array), not the preset table.
        run.CheckEqual("optics.baseline-geometry", ("7", "12"), (ui.SelectedItem("Optics.Rank"), ui.Value("Optics.Pixels")));
        run.Check("optics.preset-still-locked", fields.All(f => !ui.IsEnabled(f)), "fields disabled under Baseline");

        run.BeginStep("Choose {Workspace.Detector}: its {Detector.FaceTab} view draws the detector face for these settings, captioned {Detector.Identity}.");
        int pixels = int.Parse(ui.Value("Optics.Pixels"), CultureInfo.CurrentCulture);
        double pitch = double.Parse(ui.Value("Optics.PixelPitch"), NumberStyles.Float, CultureInfo.CurrentCulture);
        ui.Workspace("Detector", "Detector.Face");
        // Independent arithmetic from the read-back fields: N² crystals, side N × pitch.
        string expected = $"{pixels * pixels + (Broken ? 1 : 0)} crystals; {(pixels * pitch).ToString("0.###", CultureInfo.CurrentCulture)} mm square";
        run.Check("detector.face-geometry", Find(ui, "Detector.Face").Current.ItemStatus.Contains(expected, StringComparison.Ordinal),
            $"face status '{Find(ui, "Detector.Face").Current.ItemStatus}' names '{expected}'");
        run.CheckEqual("detector.pending-caption", "Settings for the next acquisition", ui.Text("Detector.Identity"));
        run.Capture("The detector face of the Baseline geometry before an acquisition.", [new("Detector.FaceTab"), new("Detector.Identity")],
            region: "Detector.Face");

        run.BeginStep("Back on the {Left.OpticsTab} tab, choose Custom in {Optics.Preset:label}: the fields keep the Baseline values and become editable.");
        ui.SelectLeftTab("Optics");
        ui.Choose("Optics.Preset", "Custom");
        run.Check("optics.custom-enables", fields.All(ui.IsEnabled) && ui.Value("Optics.Pixels") == "12", "fields enabled, values kept");

        run.BeginStep("Type 3 in {Optics.Pixels:label}. The geometry is refused — the tab reads {Optics.Error} — and {StartAcquisition} "
            + "does not start: the status bar gives the reason.");
        ui.SetText("Optics.Pixels", "3", "Optics.PixelPitch");
        // Oracle: the documented input rule, N an integer from 4 to 64.
        run.Check("optics.invalid-refused", ui.Text("Optics.Error").Contains("4 to 64", StringComparison.Ordinal), $"message '{ui.Text("Optics.Error")}'");
        ui.Invoke("StartAcquisition");
        StudioWindow.WaitUntil(() => ui.Text("StatusText").StartsWith("Start refused", StringComparison.Ordinal), TimeSpan.FromSeconds(5), "start refused");
        run.Check("optics.start-refused", ui.RunState != "Acquiring" && ui.Counts <= 0, $"status '{ui.Text("StatusText")}'");
        run.Capture("A refused geometry: the message under the fields and the refusal in the status bar.",
            [new("Optics.Pixels", LabelSource.PrecedingText), new("Optics.Error"), new("StatusText")]);

        run.BeginStep("Type 12 in {Optics.Pixels:label} again: the message clears.");
        ui.SetText("Optics.Pixels", "12", "Optics.PixelPitch");
        run.CheckEqual("optics.correction-clears", "", ui.Text("Optics.Error"));
    });

    [DesktopFact, ManualChapter("manual.acquisition")]
    public void Acquisition_StartStopContinueReset() => ManualScenario.Run("manual.acquisition", output, (ui, run) =>
    {
        run.BeginStep("Set {AcquisitionLiveTime:label} to 600 (seconds) and {AcquisitionSpeed:label} to 1 (live seconds per wall second) in the top bar.");
        ui.SetText("AcquisitionLiveTime", "600", commitBy: "SourceY");
        ui.SetText("AcquisitionSpeed", "1", commitBy: "SourceY");
        run.CheckEqual("acquisition.preset", ("600", "1"), (ui.Value("AcquisitionLiveTime"), ui.Value("AcquisitionSpeed")));

        run.BeginStep("Open the Options dialog with the cog button (accessible name {OpenOptions}); on its {Options.AcquisitionTab} tab type 12345 "
            + "in the seed field and apply with {Options.Apply}. A fixed seed makes the acquisition reproducible; the status bar lists it.");
        ui.OpenOptions("Acquisition");
        ui.SetText("AcquisitionSeed", "12345", "Options.Apply");
        run.ResolveLabels();
        ui.ApplyOptions();
        run.CheckEqual("acquisition.seed-applied", "Options: seed 12345", ui.Text("Options.Summary"));

        run.BeginStep("Click {StartAcquisition}. The acquisition runs: counts grow in the status bar, the scene and the live time are "
            + "locked, and the button becomes {StopAcquisition}.", "N-SIM");
        ui.Invoke("StartAcquisition");
        StudioWindow.WaitUntil(() => ui.RunState == "Acquiring", TimeSpan.FromSeconds(5), "acquisition started");
        StudioWindow.WaitUntil(() => ui.Counts > 0, TimeSpan.FromSeconds(20), "first counts");
        run.Check("acquisition.acquiring-locks", !ui.Exists("StartAcquisition") && !ui.IsEnabled("AddSource") && !ui.IsEnabled("AcquisitionLiveTime")
            && !ui.IsEnabled("AcquisitionSpeed") && !ui.IsEnabled("ResetAcquisition") && ui.IsEnabled("StopAcquisition"),
            "Stop offered; Start, scene, live time, speed and Reset locked");
        run.Capture("Acquiring.", [new("StopAcquisition")], region: "Window.TopBar");

        run.BeginStep("Click {StopAcquisition}: the acquisition stops and keeps its data; the counts no longer change, the "
            + "inputs stay locked and the button reads {StartAcquisition}.", "N-TIMING");
        ui.Invoke("StopAcquisition");
        StudioWindow.WaitUntil(() => ui.RunState != "Acquiring", TimeSpan.FromSeconds(5), "acquisition stopped");
        long kept = ui.Counts;
        Thread.Sleep(600);   // two refresh periods: nothing more arrives
        run.Check("acquisition.stop-freezes", ui.RunState == "Stopped" && kept > 0 && ui.Counts == (Broken ? kept + 1 : kept)
            && ui.Text("StatusText").StartsWith("Stopped", StringComparison.Ordinal) && ui.Text("StatusText").Contains("seed 12345", StringComparison.Ordinal),
            $"status '{ui.Text("StatusText")}'");
        run.Check("acquisition.stopped-locks", !ui.IsEnabled("SourceX") && ui.IsEnabled("ResetAcquisition") && ui.Text("StartAcquisition") == "Continue",
            "scene locked, Reset and Continue offered");
        run.Capture("Stopped: the data are kept and Continue is offered.", [new("StartAcquisition"), new("StatusText")]);

        run.BeginStep("Click {StartAcquisition} again: the same acquisition continues and its counts keep growing; stop it again with Stop.");
        ui.Invoke("StartAcquisition");
        StudioWindow.WaitUntil(() => ui.RunState == "Acquiring", TimeSpan.FromSeconds(5), "acquisition continued");
        StudioWindow.WaitUntil(() => ui.Counts > kept, TimeSpan.FromSeconds(20), "counts grow after Continue");
        ui.Invoke("StopAcquisition");
        StudioWindow.WaitUntil(() => ui.RunState == "Stopped", TimeSpan.FromSeconds(5), "continued acquisition stopped");
        long continued = ui.Counts;
        run.Check("acquisition.continue-accumulates", continued > kept, $"{continued} counts after {kept}");

        run.BeginStep("Click the icon button whose tooltip reads {ResetAcquisition}. A confirmation names the acquired live time and "
            + "counts; {Modal.Cancel} keeps the acquisition.");
        ui.Invoke("ResetAcquisition");
        StudioWindow.WaitUntil(() => ui.Exists("Reset.Message"), TimeSpan.FromSeconds(5), "reset confirmation opened");
        _ = ui.ModalLayer();
        run.Check("acquisition.reset-asks", ui.Text("Reset.Message").Contains(continued.ToString("N0", CultureInfo.InvariantCulture), StringComparison.Ordinal),
            $"message '{ui.Text("Reset.Message")}' names {continued} counts");
        run.Capture("The reset confirmation.", [new("Reset.Message"), new("Modal.Cancel"), new("Reset.Discard")]);
        ui.Invoke("Modal.Cancel");
        StudioWindow.WaitUntil(() => !ui.Exists("Reset.Message"), TimeSpan.FromSeconds(5), "reset confirmation closed");
        run.Check("acquisition.cancel-keeps", ui.Counts == continued && ui.RunState == "Stopped" && !ui.IsEnabled("SourceX"), "acquisition kept, scene locked");

        run.BeginStep("Click {ResetAcquisition} again and confirm with {Reset.Discard}: the data are discarded, the status reads Ready and "
            + "the inputs unlock; the button reads Start again.");
        ui.Invoke("ResetAcquisition");
        StudioWindow.WaitUntil(() => ui.Exists("Reset.Message"), TimeSpan.FromSeconds(5), "reset confirmation opened");
        run.ResolveLabels();
        ui.ConfirmReset(true);
        StudioWindow.WaitUntil(() => ui.RunState == "Empty", TimeSpan.FromSeconds(5), "acquisition reset");
        run.Check("acquisition.discard-clears", ui.Text("StatusText") == "Ready" && ui.IsEnabled("AddSource") && ui.IsEnabled("SourceX")
            && !ui.IsEnabled("ResetAcquisition") && ui.Text("StartAcquisition") == "Start", $"status '{ui.Text("StatusText")}'");
        run.Capture("Ready for the next acquisition.", [new("StartAcquisition"), new("StatusText")], region: "Window.TopBar");
    });

    [DesktopFact, ManualChapter("manual.measurements")]
    public void Measurements_ReadoutDistanceAngleRoi_DeleteAndClear() => ManualScenario.Run("manual.measurements", output, (ui, run) =>
    {
        var optics = new OpticsSettings();
        run.BeginStep("Click {StartAcquisition} and wait until the acquisition completes: the flood map and the reconstruction fill in.", "N-SIM");
        run.CheckEqual("measure.acquired", "Completed", ui.Acquire(RunTimeout));
        StudioWindow.WaitUntil(() => Find(ui, "FloodView").Current.ItemStatus.StartsWith("zoom", StringComparison.Ordinal), TimeSpan.FromSeconds(5), "flood rendered");

        run.BeginStep("Hold the pointer over a cell of the flood map: the line under the image reads {FloodReadout} — the cell's "
            + "centre in millimetres (x to the right, y up, 0 on the axis) and its counts.");
        var oracle = new FloodOracle(ui.Bounds("FloodView"), optics.DetectorPixels, optics.PixelPitchMm);
        Pointer.MoveTo(oracle.CellCentre(25, 22));
        StudioWindow.WaitUntil(() => ui.Text("FloodReadout").Length > 0, TimeSpan.FromSeconds(2), "readout shown");
        var (x, y, _) = Verdict.ParseReadout(ui.Text("FloodReadout"));
        var (ex, ey) = oracle.CellCentreMm(25, 22);
        run.Check("measure.readout-position", Verdict.Within(x, Broken ? ex + optics.PixelPitchMm : ex, 0.051) && Verdict.Within(y, ey, 0.051),
            $"read ({x}, {y}) mm, expected ({ex:F2}, {ey:F2}) mm");
        run.Capture("The readout under the flood map.", [new("FloodReadout")], region: "FloodView");

        run.BeginStep("Choose the distance tool (accessible name {ToolDistance}) above the measurement table and drag across the "
            + "flood map: the table gains a row with the length in millimetres.");
        ui.Select("ToolDistance");
        var from = oracle.ScreenAt(0.2, 0.8);
        var to = oracle.ScreenAt(0.75, 0.3);
        Pointer.Drag(from, to);
        StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 1, TimeSpan.FromSeconds(3), "distance row");
        var distance = MeasurementRow.Parse(ui.Rows("MeasurementList")[0].Current.Name);
        run.Check("measure.distance", (distance.Kind, distance.Pane) == ("Distance", "Flood")
            && Verdict.Within(distance.LengthMm(), oracle.DistanceMm(from, to), oracle.ToleranceMm),
            $"{distance} against {oracle.DistanceMm(from, to):F3} mm ± {oracle.ToleranceMm:F3}");

        run.BeginStep("Choose the angle tool ({ToolAngle}) and click three points on the reconstruction — the end of the first arm, "
            + "the vertex, the end of the second arm: the table gains the angle in degrees.");
        ui.Select("ToolAngle");
        var view = ui.Bounds("ReconView");
        var c = new Point(Math.Round(view.X + view.Width / 2), Math.Round(view.Y + view.Height / 2));
        double r = Math.Round(0.3 * Math.Min(view.Width, view.Height));
        Point arm1 = c + new Vector(r, 0.5 * r), vertex = c + new Vector(-0.6 * r, 0.5 * r), arm2 = c + new Vector(-0.2 * r, -0.6 * r);
        Pointer.Click(arm1);
        Pointer.Click(vertex);
        Pointer.Click(arm2);
        StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 2, TimeSpan.FromSeconds(3), "angle row");
        var angle = MeasurementRow.Parse(ui.Rows("MeasurementList")[1].Current.Name);
        run.Check("measure.angle", (angle.Kind, angle.Pane) == ("Angle", "Recon")
            && Verdict.Within(angle.AngleDeg(), Verdict.AngleDeg(arm1, vertex, arm2), Verdict.AngleToleranceDeg(arm1, vertex, arm2)),
            $"{angle} against {Verdict.AngleDeg(arm1, vertex, arm2):F2}°");

        run.BeginStep("Choose the ROI tool ({ToolRoi}) and drag a rectangle on the flood map: the table gains the sum over the whole "
            + "pixels whose centres lie inside, and the line under the table gives its size and pixel count.");
        ui.Select("ToolRoi");
        var a = oracle.CellCorner(8, 10);
        var b = oracle.CellCorner(14, 17);
        Pointer.Drag(a, b);
        StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 3, TimeSpan.FromSeconds(3), "ROI row");
        var roi = MeasurementRow.Parse(ui.Rows("MeasurementList")[2].Current.Name);
        run.Check("measure.roi-pixels", (roi.Kind, roi.Pane) == ("ROI", "Flood") && Verdict.ParseRoiDetail(ui.Text("MeasurementDetail")).Pixels == oracle.PixelsInside(a, b),
            $"{roi}; detail '{ui.Text("MeasurementDetail")}'; expected {oracle.PixelsInside(a, b)} px");
        run.Capture("Three measurements in the table; the selected one is described under it.", [new("M1", LabelSource.ChildText),
            new("M2", LabelSource.ChildText), new("M3", LabelSource.ChildText), new("MeasurementDetail")], region: "MeasurementList");

        run.BeginStep("Press Delete with the flood map focused: the selected measurement (the ROI) is removed.");
        ui.Keys("FloodView", "{DELETE}");
        StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 2, TimeSpan.FromSeconds(3), "row deleted");
        run.CheckEqual("measure.delete-selected", "M1 M2", string.Join(' ', ui.Rows("MeasurementList").Select(e => MeasurementRow.Parse(e.Current.Name).Name)));

        run.BeginStep("Click the icon button whose tooltip reads {ClearMeasurements}: the table is emptied.");
        ui.Invoke("ClearMeasurements");
        StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 0, TimeSpan.FromSeconds(3), "table cleared");
        run.CheckEqual("measure.clear-all", 0, ui.Rows("MeasurementList").Count);
    });

    [DesktopFact, ManualChapter("manual.spectrum")]
    public void Spectrum_LinesWindowAndLogY_MatchRetainedHistogram() => ManualScenario.Run("manual.spectrum", output, (ui, run) =>
    {
        run.BeginStep("Click {StartAcquisition} and wait until the acquisition completes.", "N-SIM");
        run.CheckEqual("spectrum.acquired", "Completed", ui.Acquire(RunTimeout));

        run.BeginStep("Choose {Workspace.Spectrum}: the histogram of measured energies with a band at each emission line, and the "
            + "line table with the counts inside each band.");
        ui.Workspace("Spectrum", "Spectrum.Plot");
        StudioWindow.WaitUntil(() => ui.Rows("Spectrum.Lines").Count > 0, TimeSpan.FromSeconds(10), "emission rows");
        var before = ui.Evidence("Spectrum.Plot");
        run.CheckEqual("spectrum.display-equals-retained", string.Join(',', WorkspaceScenarioTests.Numbers(before, "Counts")),
            string.Join(',', WorkspaceScenarioTests.Numbers(before, "PlotCounts")));
        run.CheckEqual("spectrum.line-band-count", BandCount(before) + (Broken ? 1 : 0), RowCount(ui));
        run.Capture("The spectrum after an acquisition of the default Cs-137 source.", [new("Spectrum.LogY"), new("Spectrum.Window", LabelSource.PrecedingText)],
            region: "Spectrum.Plot");

        run.BeginStep("Type 2 in {Spectrum.Window:label} and leave the field: each band widens to twice the FWHM at its line and the "
            + "table recounts; the histogram itself does not change.");
        double width = Width(before);
        ui.SetText("Spectrum.Window", "2", "Spectrum.Plot");
        StudioWindow.WaitUntil(() => ui.Evidence("Spectrum.Plot").GetProperty("WindowFwhm").GetDouble() == 2, TimeSpan.FromSeconds(5), "window committed");
        double ratio = 2 / before.GetProperty("WindowFwhm").GetDouble();
        StudioWindow.WaitUntil(() => Math.Abs(Width(ui.Evidence("Spectrum.Plot")) - width * ratio) < 1e-8, TimeSpan.FromSeconds(10), "new window processed");
        var after = ui.Evidence("Spectrum.Plot");
        run.Check("spectrum.window-widens-bands", Math.Abs(Width(after) - width * ratio) < 1e-8, $"band {Width(after)} keV from {width} keV × {ratio}");
        run.CheckEqual("spectrum.window-band-count", BandCount(after), RowCount(ui));
        run.CheckEqual("spectrum.histogram-unchanged", string.Join(',', WorkspaceScenarioTests.Numbers(before, "Counts")),
            string.Join(',', WorkspaceScenarioTests.Numbers(after, "Counts")));
        run.Capture("The wider window at 2 × FWHM.", [new("Spectrum.Window", LabelSource.PrecedingText)], region: "Spectrum.Plot");

        run.BeginStep("Tick {Spectrum.LogY}: the counts axis becomes logarithmic, so small and large bins can be read together; the data are unchanged.");
        ui.Toggle("Spectrum.LogY");
        StudioWindow.WaitUntil(() => StudioWindow.Pattern<TogglePattern>(Find(ui, "Spectrum.LogY"), TogglePattern.Pattern).Current.ToggleState == ToggleState.On,
            TimeSpan.FromSeconds(3), "Log Y on");
        var log = ui.Evidence("Spectrum.Plot");
        run.CheckEqual("spectrum.logy-display-only", string.Join(',', WorkspaceScenarioTests.Numbers(after, "Counts")),
            string.Join(',', WorkspaceScenarioTests.Numbers(log, "PlotCounts")));
        run.Capture("The spectrum with a logarithmic counts axis.", [new("Spectrum.LogY")], region: "Spectrum.Plot");
    });

    private static JsonElement Band(JsonElement e) =>
        e.GetProperty("Bands").EnumerateArray().Single(b => b.GetProperty("Energies").GetString() == "661.7");

    private static double Width(JsonElement e) => Band(e).GetProperty("HiKeV").GetDouble() - Band(e).GetProperty("LoKeV").GetDouble();

    private static long BandCount(JsonElement e) => WorkspaceOracle.BandCount(WorkspaceScenarioTests.Numbers(e, "CentresKeV"),
        WorkspaceScenarioTests.Numbers(e, "Counts"), Band(e).GetProperty("LoKeV").GetDouble(), Band(e).GetProperty("HiKeV").GetDouble());

    private static long RowCount(StudioWindow ui)
    {
        string row = ui.Rows("Spectrum.Lines").Single(r => r.Current.Name.Contains("661.7 keV", StringComparison.Ordinal)).Current.Name;
        return long.Parse(Regex.Match(row, @"([\d,]+) counts").Groups[1].Value.Replace(",", ""), CultureInfo.InvariantCulture);
    }
}
