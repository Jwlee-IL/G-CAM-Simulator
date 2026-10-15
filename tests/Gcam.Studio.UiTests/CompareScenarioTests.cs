using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>Compiled desktop scenario, opt-in only. No desktop execution is part of TODO-47 implementation.</summary>
public sealed class CompareScenarioTests(ITestOutputHelper output)
{
    [DesktopFact]
    public void PhysicalPrefix_ComparisonDifferenceAndReset_KeepTheirPopulationLabels() => Scenario.Run(
        nameof(PhysicalPrefix_ComparisonDifferenceAndReset_KeepTheirPopulationLabels), output, (ui, record) =>
        {
            ui.Workspace("Compare", "Readout.EmptyState");
            Assert.Contains("experimental physical readout", ui.Text("Readout.EmptyState"));
            ui.Invoke("Readout.ChooseReadout");
            ui.SelectLeftTab("Optics"); ui.Choose("Optics.Preset", "Baseline");
            ui.SelectLeftTab("Detector"); ui.Choose("Readout.Mode", "FourOutputAnger");
            ui.SetOption("AcquisitionSeed", "47001"); ui.SetOption("Acquisition.AmbientDose", "0");
            ui.SetText("AcquisitionLiveTime", "10", "SourceY");
            ui.Invoke("Readout.Prepare");
            StudioWindow.WaitUntil(() => ui.Text("Readout.State").StartsWith("Ready", StringComparison.Ordinal), TimeSpan.FromSeconds(90), "Baseline preparation");
            Assert.Equal("Completed", ui.Acquire(TimeSpan.FromSeconds(60)));
            ui.Workspace("Compare", "Compare.DirectFlood");
            StudioWindow.WaitUntil(() => ui.Text("Compare.Prefix").Contains("10 s"), TimeSpan.FromSeconds(20), "comparison reaches acquired prefix");
            Assert.Contains("Same crystal", ui.Text("Compare.SameShare"));
            Assert.Contains("reference only", ui.Text("Compare.Reference"));
            ui.Toggle("Compare.AssignmentDifference");
            ui.Workspace("Compare", "Compare.Difference");
            record.Step("Same transported prefix shown as direct and Anger images, then assignment disagreement; benchmark stays reference only.");
            ui.Invoke("ResetAcquisition"); ui.ConfirmReset(true);
            Assert.Contains("Start an acquisition", ui.Text("Compare.StartState"));
        });
}
