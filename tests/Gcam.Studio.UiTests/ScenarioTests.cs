using System.Text.RegularExpressions;
using System.Windows;
using Gcam.Configuration;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>
/// Stage-4 scenarios, one fresh app each, traced to the validation scenarios (VAL-xx) and requirements (SR-xx) in
/// docs/VV.Studio.md. Each is judged on product state; the expected values are derived independently of the app.
/// </summary>
public sealed class ScenarioTests(ITestOutputHelper output)
{
    private static readonly TimeSpan RunTimeout = PilotTests.RunTimeout;

    /// <summary>With GCAM_UI_BREAK_VERDICT=1 every scenario corrupts its own expectation and must then fail.</summary>
    private static bool Broken => Environment.GetEnvironmentVariable(PilotTests.BreakVerdictVariable) == "1";

    /// <summary>VAL-02 · SR-RUN-02, SR-RUN-05.</summary>
    [DesktopFact]
    public void Cancel_KeepsPreviousResult_LocksThenUnlocksScene() =>
        Scenario.Run(nameof(Cancel_KeepsPreviousResult_LocksThenUnlocksScene), output, (ui, record) =>
        {
            Assert.Equal("Succeeded", ui.Simulate(RunTimeout));
            string peakBefore = ui.Text("PeakText");

            // A budget large enough to still be running when we cancel (measured: ~2 % done after 0.3 s at 20 M photons)
            ui.SetText("PhotonBudget", "20000000", commitBy: "SourceY");
            Assert.Equal("20000000", Regex.Replace(ui.Value("PhotonBudget"), @"\D", ""));

            ui.Invoke("RunSimulation");
            StudioWindow.WaitUntil(() => ui.RunState == "Running", TimeSpan.FromSeconds(5), "run started");
            Assert.True(ui.Exists("CancelRun"), "Cancel is offered while running");
            Assert.False(ui.IsEnabled("RunSimulation"));
            Assert.False(ui.IsEnabled("AddSource"));      // scene locked
            Assert.False(ui.IsEnabled("PhotonBudget"));
            record.Step("running, scene locked");

            ui.Invoke("CancelRun");
            StudioWindow.WaitUntil(() => ui.RunState != "Running", TimeSpan.FromSeconds(5), "run stopped");
            Assert.Equal("Cancelled", ui.RunState);
            Assert.Equal("Cancelled — previous result kept", ui.Text("StatusText"));
            Assert.Equal(Broken ? peakBefore + " (broken)" : peakBefore, ui.Text("PeakText"));
            Assert.StartsWith("zoom", ui.ById("FloodView").Current.ItemStatus);   // image still there
            Assert.False(ui.Exists("CancelRun"));
            Assert.True(ui.IsEnabled("AddSource"));       // scene editable again
            Assert.True(ui.IsEnabled("RunSimulation"));
            record.Set("actual", new { peakBefore, peakAfter = ui.Text("PeakText"), status = ui.Text("StatusText") });
        });

    /// <summary>VAL-03 (ROI part) · SR-MEAS-03.</summary>
    [DesktopFact]
    public void Roi_OnFloodMap_CountsWholePixelsByCentre() =>
        Scenario.Run(nameof(Roi_OnFloodMap_CountsWholePixelsByCentre), output, (ui, record) =>
        {
            Assert.Equal("Succeeded", ui.Simulate(RunTimeout));
            ui.Select("ToolRoi");

            var optics = new OpticsSettings();
            var oracle = new FloodOracle(ui.Bounds("FloodView"), optics.DetectorPixels, optics.PixelPitchMm);
            // Corners on cell boundaries, half a cell from every centre (see FloodOracle.CellCorner).
            var a = oracle.CellCorner(8, 10);
            var b = oracle.CellCorner(14, 17);
            int expectedPixels = oracle.PixelsInside(a, b) + (Broken ? 1 : 0);
            var (ax, ay) = oracle.ScreenToMm(a);
            var (bx, by) = oracle.ScreenToMm(b);
            record.Set("oracle", new { a = a.ToString(), b = b.ToString(), expectedPixels, w = Math.Abs(bx - ax), h = Math.Abs(by - ay), tolerance = oracle.ToleranceMm });
            Assert.Equal(6 * 7 + (Broken ? 1 : 0), expectedPixels);   // the oracle itself: cells 8..13 × rows 10..16

            Pointer.Drag(a, b);
            StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 1, TimeSpan.FromSeconds(3), "one ROI row");
            var row = MeasurementRow.Parse(ui.Rows("MeasurementList")[0].Current.Name);
            string detail = ui.Text("MeasurementDetail");
            record.Set("actual", new { row = row.ToString(), detail });
            Assert.Equal(("ROI", "Flood"), (row.Kind, row.Pane));
            Assert.StartsWith("Σ ", row.Value);
            var (w, h, pixels) = Verdict.ParseRoiDetail(detail);
            Assert.Equal(expectedPixels, pixels);
            Assert.True(Verdict.Within(w, Math.Abs(bx - ax), oracle.ToleranceMm), $"width {w}");
            Assert.True(Verdict.Within(h, Math.Abs(by - ay), oracle.ToleranceMm), $"height {h}");
        });

    /// <summary>VAL-03 (angle, Esc, Delete) · SR-MEAS-02, SR-MEAS-07.</summary>
    [DesktopFact]
    public void Angle_EscAbandonsDraft_DeleteRemovesSelected() =>
        Scenario.Run(nameof(Angle_EscAbandonsDraft_DeleteRemovesSelected), output, (ui, record) =>
        {
            Assert.Equal("Succeeded", ui.Simulate(RunTimeout));
            ui.Select("ToolAngle");

            var view = ui.Bounds("ReconView");
            var c = new Point(Math.Round(view.X + view.Width / 2), Math.Round(view.Y + view.Height / 2));
            double r = Math.Round(0.3 * Math.Min(view.Width, view.Height));   // stays on the (square, centred) image

            // A first click, then Esc: if Esc did not abandon it, the next clicks would complete a different angle.
            Pointer.Click(c + new Vector(-r, -r));
            ui.Keys("ReconView", "{ESC}");

            var arm1 = c + new Vector(r, 0.5 * r);
            var vertex = c + new Vector(-0.6 * r, 0.5 * r);
            var arm2 = c + new Vector(-0.2 * r, -0.6 * r);
            double expected = Verdict.AngleDeg(arm1, vertex, arm2) + (Broken ? 5 : 0);
            double tolerance = Verdict.AngleToleranceDeg(arm1, vertex, arm2);
            record.Set("oracle", new { arm1 = arm1.ToString(), vertex = vertex.ToString(), arm2 = arm2.ToString(), expected, tolerance });
            Pointer.Click(arm1);
            Pointer.Click(vertex);
            Pointer.Click(arm2);

            StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 1, TimeSpan.FromSeconds(3), "one angle row");
            var row = MeasurementRow.Parse(ui.Rows("MeasurementList")[0].Current.Name);
            record.Set("actual", new { row = row.ToString() });
            Assert.Equal(("M1", "Angle", "Recon"), (row.Name, row.Kind, row.Pane));
            Assert.True(Verdict.Within(row.AngleDeg(), expected, tolerance), $"angle {row.AngleDeg()}° vs {expected:F2}° ± {tolerance:F2}");

            // Delete on the focused heatmap removes the selected (new) measurement
            ui.Keys("ReconView", "{DELETE}");
            StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 0, TimeSpan.FromSeconds(3), "row deleted");
        });

    /// <summary>VAL-04, VAL-05 · SR-MEAS-08, SR-RUN-07, SR-VIEW-08.</summary>
    [DesktopFact]
    public void SourceDrag_MarksOutdated_RerunPutsPeakOnTheSource() =>
        Scenario.Run(nameof(SourceDrag_MarksOutdated_RerunPutsPeakOnTheSource), output, (ui, record) =>
        {
            Assert.Equal("Succeeded", ui.Simulate(RunTimeout));
            Assert.False(ui.Exists("ResultStale"));
            Assert.Equal(("0", "0"), (ui.Value("SourceX"), ui.Value("SourceY")));
            Assert.True(StudioWindow.Pattern<System.Windows.Automation.SelectionItemPattern>(
                ui.ById("ToolPan"), System.Windows.Automation.SelectionItemPattern.Pattern).Current.IsSelected);

            // The single source starts at (0, 0) mm, the centre of the reconstruction; drag it right and up.
            var view = ui.Bounds("ReconView");
            var start = new Point(Math.Round(view.X + view.Width / 2), Math.Round(view.Y + view.Height / 2));
            var end = start + new Vector(60, -45);
            Pointer.Drag(start, end);

            double x = double.Parse(ui.Value("SourceX"), System.Globalization.CultureInfo.CurrentCulture);
            double y = double.Parse(ui.Value("SourceY"), System.Globalization.CultureInfo.CurrentCulture);
            record.Set("dragged", new { start = start.ToString(), end = end.ToString(), x, y });
            Assert.True(x > 5 && y > 5, $"source should have moved right and up, got ({x}, {y})");
            Assert.Equal(x, Math.Round(x, 1));         // snapped to 0.1 mm
            Assert.Equal(y, Math.Round(y, 1));
            Assert.True(ui.Exists("ResultStale"), "outdated chip after moving the source");

            Assert.Equal("Succeeded", ui.Simulate(RunTimeout));
            Assert.False(ui.Exists("ResultStale"));
            var (px, py) = Verdict.ParsePeak(ui.Text("PeakText"));
            // Localisation inside the fully-coded field is sub-mm for this camera (AGENTS.md findings); 1.5 mm
            // allows for the photon noise of a 500 k-photon run while still failing if the peak didn't follow.
            const double LocalisationToleranceMm = 1.5;
            if (Broken) x += 3;
            record.Set("actual", new { peak = new { px, py }, source = new { x, y }, tolerance = LocalisationToleranceMm });
            output.WriteLine($"source ({x}, {y}) mm, peak ({px}, {py}) mm");
            Assert.True(Math.Abs(px - x) <= LocalisationToleranceMm && Math.Abs(py - y) <= LocalisationToleranceMm,
                $"peak ({px}, {py}) is not within {LocalisationToleranceMm} mm of the source ({x}, {y})");
        });

    /// <summary>SR-THEME-01 (the visual result of the swap stays a manual check).</summary>
    [DesktopFact]
    public void ThemeToggle_RelabelsAndSwitchesBack() =>
        Scenario.Run(nameof(ThemeToggle_RelabelsAndSwitchesBack), output, (ui, record) =>
        {
            Assert.Equal(Broken ? "Dark theme" : "Light theme", ui.Text("ThemeToggle"));
            ui.Invoke("ThemeToggle");
            StudioWindow.WaitUntil(() => ui.Text("ThemeToggle") == "Dark theme", TimeSpan.FromSeconds(2), "label names the other theme");
            ui.Invoke("ThemeToggle");
            StudioWindow.WaitUntil(() => ui.Text("ThemeToggle") == "Light theme", TimeSpan.FromSeconds(2), "and back");
            Assert.Equal("Succeeded", ui.Simulate(RunTimeout));   // still fully working after two swaps
        });
}
