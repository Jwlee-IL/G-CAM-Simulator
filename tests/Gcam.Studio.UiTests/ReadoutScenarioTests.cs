using System.Text.RegularExpressions;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>Desktop scenarios of the physical readout; opt-in only (GCAM_UI_TESTS=1, author's go). No statistical
/// localisation or calibration-accuracy threshold. Preparation waits follow the measured cold cost (Turn 7 §7:
/// max(90 s, 2 × measured wall + 10 s), rounded up to whole minutes) — scheduling allowances, not tolerances.</summary>
public sealed class ReadoutScenarioTests(ITestOutputHelper output)
{
    private static bool Broken => Environment.GetEnvironmentVariable(PilotTests.BreakVerdictVariable) == "1";

    /// <summary>Measured cold Prepare (Turn 7, one seed, one worker) → wait per (preset, line).</summary>
    internal static readonly IReadOnlyDictionary<(string Preset, double Line), TimeSpan> PrepareWait = new Dictionary<(string, double), TimeSpan>
    {
        [("Baseline", 661.7)] = TimeSpan.FromSeconds(90), [("Baseline", 1332.5)] = TimeSpan.FromSeconds(90),
        [("Sharp", 661.7)] = TimeSpan.FromSeconds(180), [("Sharp", 1332.5)] = TimeSpan.FromSeconds(180),
        [("Wide FOV", 661.7)] = TimeSpan.FromSeconds(420), [("Wide FOV", 1332.5)] = TimeSpan.FromSeconds(240),
        [("High-res", 661.7)] = TimeSpan.FromSeconds(5400), [("High-res", 1332.5)] = TimeSpan.FromSeconds(420),
    };

    private static void SelectHead(StudioWindow ui, string preset = "Baseline")
    {
        ui.SelectLeftTab("Optics");
        ui.Choose("Optics.Preset", preset);
        ui.SelectLeftTab("Detector");
        ui.Choose("Readout.Mode", "FourOutputAnger");
        ui.SetOption("AcquisitionSeed", "12345");
    }

    private static void Prepare(StudioWindow ui, TimeSpan wait)
    {
        ui.Invoke("Readout.Prepare");
        StudioWindow.WaitUntil(() => ui.Text("Readout.State").StartsWith("Ready", StringComparison.Ordinal)
            || ui.Text("Readout.State").StartsWith("Failed", StringComparison.Ordinal), wait, "preparation ends");
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

    /// <summary>The module the map captured, from its automation ItemStatus ("module 44 x 44, sensor pitch 0.4 mm").</summary>
    internal static (int X, int Y, double PitchMm) Module(string status)
    {
        var m = Regex.Match(status, @"module (\d+) x (\d+), sensor pitch ([\d.]+) mm");
        if (!m.Success) throw new ArgumentException("Captured module missing from the map status.");
        return (int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CountOracle_RefusesNonConservationAndMissingIdentity()
    {
        Assert.Equal((1234L, 2L, 1236L), Counts("Assigned 1,234 · unknown 2 · triggered 1,236 · realised hits 2,000 · reference 661.7 keV calibration"));
        Assert.Throws<ArgumentException>(() => Counts("Assigned 10 · unknown 2 · triggered 11"));
        Assert.Throws<ArgumentException>(() => Counts("No acquired counts"));
    }

    [Fact]
    public void ModuleOracle_ReadsTheCapturedGeometry_AndWaitsFollowTheMeasuredCost()
    {
        Assert.Equal((44, 44, .4), Module("Anger; line 661.7 keV; 1936 calibration peaks; module 44 x 44, sensor pitch 0.4 mm; calibration x; live False; zoom 1"));
        Assert.Throws<ArgumentException>(() => Module("Not prepared"));
        // max(90 s, 2 × 2672.44 s + 10 s) = 5354.9 s → 5400 s; max(90, 2 × 177.45 + 10) = 364.9 → 420 s.
        Assert.Equal(TimeSpan.FromSeconds(5400), PrepareWait[("High-res", 661.7)]);
        Assert.Equal(TimeSpan.FromSeconds(420), PrepareWait[("Wide FOV", 661.7)]);
        Assert.All(PrepareWait.Values, w => Assert.True(w >= TimeSpan.FromSeconds(90)));
    }

    [Fact]
    public void DesktopGate_LongPreparationRequiresBothExplicitOptIns()
    {
        foreach (bool desktop in new[] { false, true })
        foreach (bool longOptIn in new[] { false, true })
        {
            string? reason = DesktopFactAttribute.SkipReason(desktop, longOptIn, longRunning: true);
            if (desktop && longOptIn) Assert.Null(reason);
            else
            {
                Assert.Contains("GCAM_UI_TESTS=1 AND GCAM_UI_LONG_TESTS=1", reason);
                Assert.Contains("64 k preparation, ≈ 45 min, ≈ 6 GiB", reason);
            }
            Assert.Equal(desktop, DesktopFactAttribute.SkipReason(desktop, longOptIn, longRunning: false) is null);
        }
    }

    [DesktopFact]
    public void RefusedLineAndField_ExplainWithoutZeroing() => Scenario.Run(nameof(RefusedLineAndField_ExplainWithoutZeroing), output, (ui, record) =>
    {
        // TODO-41: the default Sharp head selects the physical readout; 122.06 keV is refused with its published reason.
        ui.SelectLeftTab("Detector");
        ui.Choose("Readout.Mode", "FourOutputAnger");
        Assert.Equal("FourOutputAnger", ui.SelectedItem("Readout.Mode"));
        // The refusal fact stays inline; the record sentence (population, source, first failure) is the row's help text.
        string lines = ui.WaitForDescription("Readout.Lines", TimeSpan.FromSeconds(5), "122.06 keV · Refused");
        Assert.Contains("Refused: Sharp 122.06 keV calibration failed on 32 / 32 seeds", lines);
        Assert.Contains(Broken ? "Refused: Sharp 661.7 keV" : "Offered (experimental): Sharp 661.7 keV", lines);
        ui.SetText("Readout.WindowLow", "100", "SourceY");
        ui.SetText("Readout.WindowHigh", "140", "SourceY");
        Assert.Equal("Window line 122.06 keV", ui.Text("Readout.WindowLine"));
        string field = ui.OptionValue("Acquisition.AmbientDose");
        ui.Invoke("StartAcquisition");
        Assert.Contains("zero", ui.Text("Readout.Reason"));
        Assert.Equal(field, ui.OptionValue("Acquisition.AmbientDose"));
        Assert.Equal("Empty", ui.RunState);
        Assert.False(ui.OptionEnabled("Detector.GainSigma"));
        Assert.False(ui.OptionEnabled("Chain.Scintillator"));
        record.Step("Sharp physical selection; 122.06 keV refused with population and literal reason; nonzero field refusal preserves inputs.");
    });

    [DesktopFact]
    public void PreparationCancelRetryAndTabs_AreExplicit() => Scenario.Run(nameof(PreparationCancelRetryAndTabs_AreExplicit), output, (ui, record) =>
    {
        SelectHead(ui);
        ui.Invoke("Readout.Prepare");
        ui.Invoke("Readout.Cancel");
        StudioWindow.WaitUntil(() => ui.Text("Readout.Reason").Contains("cancelled", StringComparison.Ordinal), TimeSpan.FromSeconds(10), "preparation cancellation");
        Prepare(ui, PrepareWait[("Baseline", 661.7)]);
        // Same-session reuse: a second Prepare is a cache hit (measured < 0.1 ms in the service).
        var watch = System.Diagnostics.Stopwatch.StartNew();
        ui.Invoke("Readout.Prepare");
        StudioWindow.WaitUntil(() => ui.Text("Readout.State").StartsWith("Ready", StringComparison.Ordinal), TimeSpan.FromSeconds(10), "cache hit");
        record.Step($"Second Prepare returned in {watch.Elapsed.TotalSeconds:0.0} s (session cache).");
        ui.Workspace("Detector", "Readout.Map");
        foreach (string tab in new[] { "Sipm", "Anger", "Calibration", "Diagnostics" }) ui.Select("Readout." + tab + "Tab");
        Assert.Equal(!Broken, ui.Rows("Readout.Diagnostics").Any());
        ui.Select("Readout.AngerTab"); // the map is in the tree only on the SiPM / Anger tabs
        Assert.Equal((12, 12, 1.0), Module(ui.Status("Readout.Map")));
        ui.SelectLeftTab("Detector");
        ui.SetOption("Detector.Gap", "90");
        Assert.Equal("Not prepared", ui.Text("Readout.State")); // a stale bank after a module edit
        record.Step("Cancelled preparation is retryable; same-session hit; tabs and per-crystal diagnostics; module edit makes the bank stale.");
    });

    [DesktopFact]
    public void FixedSeedRepeatAndContinuation_ConserveCountsAndExposeFourLanes() => Scenario.Run(nameof(FixedSeedRepeatAndContinuation_ConserveCountsAndExposeFourLanes), output, (ui, record) =>
    {
        SelectHead(ui);
        ui.SetOption("Acquisition.AmbientDose", "0");
        ui.SetText("AcquisitionLiveTime", "4", "SourceY");
        ui.SetText("AcquisitionSpeed", "1", "SourceY");
        Prepare(ui, PrepareWait[("Baseline", 661.7)]);
        Assert.Equal("Completed", ui.Acquire(TimeSpan.FromSeconds(30)));
        ui.Workspace("Detector", "Readout.Map");
        var expected = Counts(ui.Text("Readout.Counts"));
        Assert.Equal(expected.Assigned, ui.Counts);
        ui.Invoke("ResetAcquisition");
            ui.ConfirmReset(true);
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

    [DesktopFact]
    public void SharpTwoLineBank_SwitchesWindowAndLineWithoutTruthDispatch() => Scenario.Run(nameof(SharpTwoLineBank_SwitchesWindowAndLineWithoutTruthDispatch), output, (ui, record) =>
    {
        SelectHead(ui, "Sharp");
        ui.SetOption("Acquisition.AmbientDose", "0");
        ui.Invoke("AddSource");
        ui.Choose("SourceIsotope", "Co-60");
        Prepare(ui, PrepareWait[("Sharp", 661.7)] + PrepareWait[("Sharp", 1332.5)]);
        Assert.Contains("1332.5 keV", ui.Text("Readout.State"));
        ui.SetText("AcquisitionLiveTime", "4", "SourceY");
        Assert.Equal("Completed", ui.Acquire(TimeSpan.FromSeconds(60)));
        ui.Workspace("Detector", "Readout.Map");
        string counts = ui.Text("Readout.Counts");
        Counts(counts);
        // Key verdict: the same held records are read through the second line's own calibration (corrupted when broken).
        Assert.Contains(Broken ? "1332.5 keV calibration: missing" : "1332.5 keV calibration: assigned", counts);
        Assert.Equal((30, 30, .6), Module(ui.Status("Readout.Map")));
        ui.Choose("Readout.Line", "1332.5");
        Assert.Contains("line 1332.5 keV", ui.Status("Readout.Map"));
        ui.Invoke("Readout.BenchmarkWindow"); // ±15 % of the window's line (661.7 keV first)
        // Scene inputs are locked once data exist, so commit each window edge by focusing the other (both stay enabled).
        // High edge first, so the window stays valid (low < high) after every commit.
        ui.SetText("Readout.WindowHigh", "1532.375", "Readout.WindowLow");
        ui.SetText("Readout.WindowLow", "1132.625", "Readout.WindowHigh");
        Assert.Equal("Window line 1332.5 keV", ui.Text("Readout.WindowLine"));
        ui.Workspace("Imaging", "FloodView");
        // "All" is a broadband reading through the labelled reference calibration (SD-A2); the window channel is per line.
        StudioWindow.WaitUntil(() => { try { ui.Choose("Imaging.Channel", "Energy window"); return true; } catch (InvalidOperationException) { return false; } },
            TimeSpan.FromSeconds(30), "Energy window channel published");
        StudioWindow.WaitUntil(() => ui.Text("Imaging.ReadoutCurrentCounts").Contains("1332.5 keV calibration", StringComparison.Ordinal),
            TimeSpan.FromSeconds(30), "1332.5 keV window channel");
        Assert.False(ui.Exists("Imaging.ChannelUnavailable") && ui.Bounds("Imaging.ChannelUnavailable").Height > 0);
        record.Step("Sharp 661.7 + 1332.5 keV bank: held facts once, per-line counts, map and Imaging follow the selected line.");
    });

    [DesktopFact]
    public void WideLocalisationHelp_ShowsRecordScopeAndDirectBias() => Scenario.Run(nameof(WideLocalisationHelp_ShowsRecordScopeAndDirectBias), output, (ui, record) =>
    {
        SelectHead(ui, "Wide FOV");
        string help = ui.Help("Readout.LocalisationHelp");
        Assert.Contains(Broken ? "wrong benchmark wording" : "F is an equal-fluence MSE ratio, not a time factor.", help);
        Assert.Contains("F below 1 is chiefly direct-reference bias", help);
        Assert.Contains("Benchmark: one mono-line point source", help);
        Assert.False(ui.Exists("Imaging.ReadoutCostSection"));
        record.Step("Experimental localisation help carries the record scope, module F range and direct-bias explanation.");
    });

    [DesktopFact(longRunning: true)]
    public void HighResCold_ProgressTabsCornersAndSessionHit_Bundled() => Scenario.Run(nameof(HighResCold_ProgressTabsCornersAndSessionHit_Bundled), output, (ui, record) =>
    {
        // One cold 64 k High-res preparation (≈ 45 min, ≈ 6 GiB) bundles every High check; later steps reuse the session.
        SelectHead(ui, "High-res");
        ui.SetOption("Acquisition.AmbientDose", "0");
        ui.WaitForDescription("Readout.Lines", TimeSpan.FromSeconds(5), "64,000 histories per crystal");
        ui.Invoke("Readout.Prepare");
        StudioWindow.WaitUntil(() => ui.Text("Readout.State").Contains("histories", StringComparison.Ordinal), TimeSpan.FromSeconds(600), "streamed flood progress");
        Assert.True(ui.Bounds("Readout.Progress").Height > 0);
        StudioWindow.WaitUntil(() => ui.Text("Readout.State").StartsWith("Ready", StringComparison.Ordinal)
            || ui.Text("Readout.State").StartsWith("Failed", StringComparison.Ordinal), PrepareWait[("High-res", 661.7)], "High-res 661.7 keV preparation");
        Assert.StartsWith("Ready", ui.Text("Readout.State"));
        // Another window may have taken the foreground during the long wait: restore the owned window before pointer input.
        Assert.True(ui.EnsureForeground(), "owned Studio window restored to the foreground");
        ui.Workspace("Detector", "Readout.Map");
        foreach (string tab in new[] { "Sipm", "Anger", "Calibration", "Diagnostics" }) ui.Select("Readout." + tab + "Tab");
        ui.Select("Readout.CalibrationTab");
        Assert.Contains("mean sensitivity map (32 seeds)", ui.Text("Readout.SensitivityCaption"));
        Assert.True(ui.Bounds("Readout.SensitivityMap").Height > 0);
        Assert.Contains("never flood-correction weights", ui.Help("Readout.SensitivityHelp"));
        ui.Select("Readout.AngerTab");
        // Key verdict (after the unavoidable preparation): the captured 44 × 44 module at 0.4 mm (corrupted when broken).
        Assert.Equal(Broken ? (44, 44, .5) : (44, 44, .4), Module(ui.Status("Readout.Map")));
        Assert.Contains("1936 calibration peaks", ui.Status("Readout.Map"));
        ui.SetText("AcquisitionLiveTime", "2", "SourceY");
        Assert.Equal("Completed", ui.Acquire(TimeSpan.FromSeconds(120)));
        Counts(ui.Text("Readout.Counts"));
        ui.Invoke("ResetAcquisition");
            ui.ConfirmReset(true);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        ui.Invoke("Readout.Prepare");
        StudioWindow.WaitUntil(() => ui.Text("Readout.State").StartsWith("Ready", StringComparison.Ordinal), TimeSpan.FromSeconds(30), "session hit");
        record.Step($"High-res 64 k cold preparation with streamed progress; 44 × 44 module captured; acquisition conserved; session hit in {watch.Elapsed.TotalSeconds:0.0} s.");
    });

    [DesktopFact]
    public void HighResCoLine_ProgressTabsSensitivityAndSessionHit() => Scenario.Run(nameof(HighResCoLine_ProgressTabsSensitivityAndSessionHit), output, (ui, record) =>
    {
        SelectHead(ui, "High-res");
        // Replace the default Cs source: do not add it to a two-line bank that would prepare the 64 k line.
        ui.Choose("SourceIsotope", "Co-60");
        Assert.Equal("Co-60", ui.SelectedItem("SourceIsotope"));
        ui.SetOption("Acquisition.AmbientDose", "0");
        ui.SetText("Readout.WindowHigh", "1532.375", "Readout.WindowLow");
        ui.SetText("Readout.WindowLow", "1132.625", "Readout.WindowHigh");
        Assert.Equal("Window line 1332.5 keV", ui.Text("Readout.WindowLine"));
        string lines = ui.WaitForDescription("Readout.Lines", TimeSpan.FromSeconds(5), "1332.5 keV · Offered (experimental) · in scene");
        Assert.DoesNotContain("661.7 keV · Offered (experimental) · in scene", lines);
        Assert.Contains("2,000 histories per crystal", ui.Help("Readout.LineHelp.1332.5"));
        var prepareWatch = System.Diagnostics.Stopwatch.StartNew();
        var wait = PrepareWait[("High-res", 1332.5)];
        ui.Invoke("Readout.Prepare");
        StudioWindow.WaitUntil(() => ui.Text("Readout.State").Contains("histories", StringComparison.Ordinal)
            || ui.Text("Readout.State").StartsWith("Failed", StringComparison.Ordinal), wait, "1332.5 keV streamed flood progress");
        Assert.Contains("histories", ui.Text("Readout.State"));
        Assert.True(ui.Bounds("Readout.Progress").Height > 0);
        var remaining = wait - prepareWatch.Elapsed;
        Assert.True(remaining > TimeSpan.Zero, "history progress arrived within the measured preparation wait");
        StudioWindow.WaitUntil(() => ui.Text("Readout.State").StartsWith("Ready", StringComparison.Ordinal)
            || ui.Text("Readout.State").StartsWith("Failed", StringComparison.Ordinal), remaining, "High-res 1332.5 keV preparation");
        Assert.StartsWith("Ready", ui.Text("Readout.State"));
        Assert.Contains("1332.5 keV", ui.Text("Readout.State"));
        Assert.DoesNotContain("661.7 keV", ui.Text("Readout.State"));
        Assert.True(ui.EnsureForeground(), "owned Studio window restored to the foreground");
        ui.Workspace("Detector", "Readout.Map");
        foreach (string tab in new[] { "Sipm", "Anger", "Calibration", "Diagnostics" }) ui.Select("Readout." + tab + "Tab");
        ui.Select("Readout.CalibrationTab");
        Assert.Contains("mean sensitivity map (32 seeds)", ui.Text("Readout.SensitivityCaption"));
        Assert.True(ui.Bounds("Readout.SensitivityMap").Height > 0);
        Assert.Contains("never flood-correction weights", ui.Help("Readout.SensitivityHelp"));
        ui.Select("Readout.AngerTab");
        // This scenario has its own deliberately corrupted key verdict.
        Assert.Equal(Broken ? (44, 44, .5) : (44, 44, .4), Module(ui.Status("Readout.Map")));
        Assert.Contains("line 1332.5 keV", ui.Status("Readout.Map"));
        Assert.Contains("1936 calibration peaks", ui.Status("Readout.Map"));
        ui.SetText("AcquisitionLiveTime", "2", "SourceY");
        Assert.Equal("Completed", ui.Acquire(TimeSpan.FromSeconds(120)));
        Counts(ui.Text("Readout.Counts"));
        ui.Invoke("ResetAcquisition");
            ui.ConfirmReset(true);
        var cacheWatch = System.Diagnostics.Stopwatch.StartNew();
        ui.Invoke("Readout.Prepare");
        StudioWindow.WaitUntil(() => ui.Text("Readout.State").StartsWith("Ready", StringComparison.Ordinal), TimeSpan.FromSeconds(30), "1332.5 keV session hit");
        record.Step($"High-res 1332.5 keV only, pinned 2 k budget: history progress, tabs, mean sensitivity map and help, captured 44 × 44 module and 1936 peaks, conserved acquisition; session hit in {cacheWatch.Elapsed.TotalSeconds:0.0} s.");
    });

    [DesktopFact]
    public void CustomModules_InheritByModuleNotCost_UnknownCalibratesLocally() => Scenario.Run(nameof(CustomModules_InheritByModuleNotCost_UnknownCalibratesLocally), output, (ui, record) =>
    {
        SelectHead(ui, "High-res");
        // Author review 2 (TODO-45): the fields are shown under a preset too, holding its values, but disabled.
        var high = Gcam.Studio.Core.Optics.OpticsPreset.All.Single(p => p.Name == "High-res").Settings!;
        Assert.False(ui.IsEnabled("Optics.CellPitch"));
        Assert.Equal(high.CellPitchMm, double.Parse(ui.Value("Optics.CellPitch")));
        ui.Choose("Optics.Preset", "Custom");
        Assert.True(ui.IsEnabled("Optics.CellPitch")); // Custom enables them, starting from the preset's values
        Assert.Equal(high.CellPitchMm, double.Parse(ui.Value("Optics.CellPitch")));
        ui.Choose("Optics.Rank", "7"); // a ComboBox of primes (the other optics fields are text boxes)
        ui.SetText("Optics.CellPitch", "1", "SourceY");
        ui.SetText("Optics.Distance", "60", "SourceY");
        Assert.Equal("Custom", ui.SelectedItem("Optics.Preset"));
        // Read rows and their help marks, waiting for the refresh after the optics edits (bundle 101054: the rows are data
        // items whose text is their name, so a Text-only read returned "").
        string inherited = ui.WaitForDescription("Readout.Lines", TimeSpan.FromSeconds(5), "Offered (experimental): High 661.7 keV");
        // Key verdict: the matching module inherits High's pinned 64 k budget (a broken verdict expects the superseded 32 k).
        Assert.Contains(Broken ? "32,000 histories per crystal" : "64,000 histories per crystal", inherited);
        ui.Workspace("Imaging", "FloodView");
        Assert.Contains("Benchmark reference only", ui.Help("Readout.LocalisationHelp"));
        ui.SetText("Optics.Pixels", "20", "SourceY");
        ui.SetText("Optics.PixelPitch", "0.5", "SourceY");
        ui.WaitForDescription("Readout.Lines", TimeSpan.FromSeconds(5), "experimental, unvalidated");
        ui.Invoke("Readout.Prepare");
        StudioWindow.WaitUntil(() => ui.Text("Readout.State").StartsWith("Ready", StringComparison.Ordinal)
            || ui.Text("Readout.State").StartsWith("Failed", StringComparison.Ordinal), TimeSpan.FromSeconds(300), "local Custom calibration");
        record.Step($"Custom: High module inherits the 64 k offer but not the cost; unknown 20 × 20 calibrates locally: {ui.Text("Readout.State")}");
    });
}
