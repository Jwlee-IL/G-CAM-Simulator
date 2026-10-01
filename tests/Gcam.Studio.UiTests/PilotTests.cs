using Gcam.Configuration;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>
/// The pilot flow: simulate → pick the distance tool → drag on the flood map → judge the product's result. Judged
/// on what only the product's handlers produce (run state, a results-table row and its value), never on "the click
/// didn't throw".
/// </summary>
public sealed class PilotTests(ITestOutputHelper output)
{
    /// <summary>Set to 1 to shift the expected length by 1 mm: the run must then FAIL (verdict regression check).</summary>
    public const string BreakVerdictVariable = "GCAM_UI_BREAK_VERDICT";

    internal static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(60);

    [DesktopFact]
    public void Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength() =>
        Scenario.Run(nameof(Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength), output, (ui, record) =>
        {
            Assert.Empty(ui.Rows("MeasurementList"));

            // Progress end and result presence are separate conditions
            Assert.Equal("Completed", ui.Acquire(RunTimeout));
            StudioWindow.WaitUntil(() => ui.ById("FloodView").Current.ItemStatus.StartsWith("zoom", StringComparison.Ordinal),
                TimeSpan.FromSeconds(5), "flood map has an image");
            record.Step($"simulated: {ui.Text("StatusText")}");

            // Tool: selection read back, and the handler's effect (hint text) observed
            string hintBefore = ui.Text("ToolHint");
            ui.Select("ToolDistance");
            StudioWindow.WaitUntil(() => ui.Text("ToolHint") != hintBefore, TimeSpan.FromSeconds(2), "tool hint follows the tool");

            var optics = new OpticsSettings();
            var oracle = new FloodOracle(ui.Bounds("FloodView"), optics.DetectorPixels, optics.PixelPitchMm);
            var from = oracle.ScreenAt(0.2, 0.8);
            var to = oracle.ScreenAt(0.75, 0.3);
            double expected = oracle.DistanceMm(from, to);
            bool broken = Environment.GetEnvironmentVariable(BreakVerdictVariable) == "1";
            if (broken) expected += 1.0;
            record.Set("oracle", new { view = oracle.ViewBounds.ToString(), image = oracle.ImageBounds.ToString(), oracle.CellPx, from = from.ToString(), to = to.ToString(), expected, tolerance = oracle.ToleranceMm, verdictBroken = broken });
            Pointer.Drag(from, to);
            Assert.Equal(to, Pointer.Position());   // the input landed where the oracle assumed

            // Exactly one new row, of the right kind, on the right image, with the expected length
            StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 1, TimeSpan.FromSeconds(3), "one measurement row");
            var rowElement = ui.Rows("MeasurementList")[0];
            var row = MeasurementRow.Parse(rowElement.Current.Name);
            record.Set("actual", new { row = rowElement.Current.Name, id = rowElement.Current.AutomationId, detail = ui.Text("MeasurementDetail") });
            Assert.Equal(("M1", "M1", "Distance", "Flood"), (rowElement.Current.AutomationId, row.Name, row.Kind, row.Pane));
            double actual = row.LengthMm();
            output.WriteLine($"expected {expected:F3} mm ± {oracle.ToleranceMm:F3}, actual {actual:F1} mm");
            Assert.True(Verdict.Within(actual, expected, oracle.ToleranceMm),
                $"length {actual} mm is not within {oracle.ToleranceMm:F3} mm of the expected {expected:F3} mm");
        });
}
