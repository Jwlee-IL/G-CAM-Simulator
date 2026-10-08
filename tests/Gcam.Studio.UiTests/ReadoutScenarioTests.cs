using System.Text.RegularExpressions;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>New desktop scenarios; opt-in only. No statistical localisation or calibration-accuracy threshold.</summary>
public sealed class ReadoutScenarioTests(ITestOutputHelper output)
{
    private static bool Broken => Environment.GetEnvironmentVariable(PilotTests.BreakVerdictVariable) == "1";

    private static void SelectHead(StudioWindow ui)
    {
        ui.Expand("Optics.Section");
        ui.Choose("Optics.Preset", "Baseline");
        ui.Expand("Detector.Section");
        ui.Choose("Readout.Mode", "FourOutputAnger");
        ui.SetText("AcquisitionSeed", "12345", "SourceY");
    }

    private static void Prepare(StudioWindow ui)
    {
        ui.Invoke("Readout.Prepare");
        StudioWindow.WaitUntil(() => ui.Text("Readout.State").StartsWith("Ready", StringComparison.Ordinal)
            || ui.Text("Readout.State").StartsWith("Failed", StringComparison.Ordinal), TimeSpan.FromSeconds(90), "preparation ends");
        Assert.StartsWith("Ready", ui.Text("Readout.State"));
    }

    internal static (long Assigned, long Unknown, long Triggered) Counts(string text)
    {
        var m = Regex.Match(text, @"Assigned ([\d,]+) · unknown ([\d,]+) · triggered ([\d,]+)");
        if (!m.Success) throw new ArgumentException("Physical count identity missing.");
        long N(int n) => long.Parse(m.Groups[n].Value.Replace(",", ""), System.Globalization.CultureInfo.InvariantCulture);
        var result = (N(1), N(2), N(3));
        if (result.Item1 + result.Item2 != result.Item3) throw new ArgumentException("Assigned + unknown must equal triggered.");
        return result;
    }

    [Fact]
    public void CountOracle_RefusesNonConservationAndMissingIdentity()
    {
        Assert.Equal((1234L, 2L, 1236L), Counts("Assigned 1,234 · unknown 2 · triggered 1,236 · realised hits 2,000"));
        Assert.Throws<ArgumentException>(() => Counts("Assigned 10 · unknown 2 · triggered 11"));
        Assert.Throws<ArgumentException>(() => Counts("No acquired counts"));
    }

    [DesktopFact]
    public void UnsupportedGeometryAndField_ExplainRefusalWithoutZeroing() => Scenario.Run(nameof(UnsupportedGeometryAndField_ExplainRefusalWithoutZeroing), output, (ui, record) =>
    {
        ui.Expand("Detector.Section");
        ui.Choose("Readout.Mode", "FourOutputAnger");
        Assert.Equal(Broken ? "FourOutputAnger" : "DirectCrystal", ui.SelectedItem("Readout.Mode"));
        Assert.Contains("12 × 12", ui.Text("Readout.Reason"));
        SelectHead(ui);
        string field = ui.Value("Acquisition.AmbientDose");
        ui.Invoke("StartAcquisition");
        Assert.Contains("zero", ui.Text("Readout.Reason"));
        Assert.Equal(field, ui.Value("Acquisition.AmbientDose"));
        Assert.Equal("Empty", ui.RunState);
        Assert.False(ui.IsEnabled("Detector.GainSigma"));
        Assert.False(ui.IsEnabled("Chain.Scintillator"));
        record.Step("Reference geometry and nonzero field refusals preserve inputs; legacy gain and chain disabled.");
    });

    [DesktopFact]
    public void PreparationCancelRetryAndTabs_AreExplicit() => Scenario.Run(nameof(PreparationCancelRetryAndTabs_AreExplicit), output, (ui, record) =>
    {
        SelectHead(ui);
        ui.Invoke("Readout.Prepare");
        ui.Invoke("Readout.Cancel");
        StudioWindow.WaitUntil(() => ui.Text("Readout.Reason").Contains("cancelled", StringComparison.Ordinal), TimeSpan.FromSeconds(10), "preparation cancellation");
        Prepare(ui);
        ui.Workspace("Detector", "Readout.Map");
        foreach (string tab in new[] { "Sipm", "Anger", "Calibration", "Diagnostics" }) ui.Select("Readout." + tab + "Tab");
        Assert.Equal(!Broken, ui.Rows("Readout.Diagnostics").Any());
        record.Step("Cancelled preparation is retryable; calibration and per-crystal diagnostics are visible.");
    });

    [DesktopFact]
    public void FixedSeedRepeatAndContinuation_ConserveCountsAndExposeFourLanes() => Scenario.Run(nameof(FixedSeedRepeatAndContinuation_ConserveCountsAndExposeFourLanes), output, (ui, record) =>
    {
        SelectHead(ui);
        ui.SetText("Acquisition.AmbientDose", "0", "SourceY");
        ui.SetText("AcquisitionLiveTime", "4", "SourceY");
        ui.SetText("AcquisitionSpeed", "1", "SourceY");
        Prepare(ui);
        Assert.Equal("Completed", ui.Acquire(TimeSpan.FromSeconds(30)));
        ui.Workspace("Detector", "Readout.Map");
        var expected = Counts(ui.Text("Readout.Counts"));
        Assert.Equal(expected.Assigned, ui.Counts);
        ui.Invoke("ResetAcquisition");
        ui.Invoke("StartAcquisition");
        StudioWindow.WaitUntil(() => ui.RunState == "Acquiring" && ui.Counts > 0, TimeSpan.FromSeconds(10), "live measured counts");
        ui.Invoke("StopAcquisition");
        StudioWindow.WaitUntil(() => ui.RunState == "Stopped", TimeSpan.FromSeconds(10), "stopped");
        long stopped = ui.Counts;
        Thread.Sleep(500);
        Assert.Equal(stopped, ui.Counts);
        Assert.Equal("Completed", ui.Acquire(TimeSpan.FromSeconds(30)));
        Assert.Equal(Broken ? (expected.Assigned + 1, expected.Unknown, expected.Triggered) : expected,
            Counts(ui.Text("Readout.Counts")));
        ui.Workspace("Waveform", "Waveform.A");
        foreach (string lane in new[] { "A", "B", "C", "D", "Sum" }) Assert.True(ui.Bounds("Waveform." + lane).Height > 0);
        Assert.False(ui.IsEnabled("Waveform.Ideal"));
        Assert.False(ui.IsEnabled("Waveform.RateStudy"));
        ui.Workspace("Spectrum", "Spectrum.Plot");
        Assert.False(ui.IsEnabled("Spectrum.PileUp"));
        record.Step("Fixed seed Stop / Continue equals uninterrupted assigned/unknown/triggered counts; four lanes and sum visible; replay disabled.");
    });
}
