using System.Text.Json;
using System.Text.RegularExpressions;
using Gcam.Configuration;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

public sealed class WorkspaceScenarioTests(ITestOutputHelper output)
{
    private static bool Broken => Environment.GetEnvironmentVariable(PilotTests.BreakVerdictVariable) == "1";
    internal static double[] Numbers(JsonElement e, string key) => e.GetProperty(key).EnumerateArray().Select(v => v.GetDouble()).ToArray();
    private static void AcquireFixed(StudioWindow ui)
    {
        ui.SetText("AcquisitionSeed", "12345", "SourceY");
        Assert.Equal("Completed", ui.Acquire(PilotTests.RunTimeout));
    }

    /// <summary>TODO-38: the Co-60 activity at which the pair scene's channel-side assertions are guaranteed. At 20 µCi the
    /// Co-60 window holds ~40 counts (1.98 per µCi in 60 s at the default optics and chain) and its peak falls on the
    /// wrong side in 27 % of acquisitions — the scenario passed only by its realisation, and TODO-30's ambient stream
    /// changed the realisation. Criterion: with Poisson moments of D(θ) = recon(true node) − recon(θ) from the measured
    /// window shapes, Σ over every grid point θ on the wrong side of P(D(θ) ≤ 0) (Gaussian tails, Bonferroni) ≤ α = 10⁻³
    /// per channel. 400 µCi gives 1.3 × 10⁻⁴ for the Co-60 channel (~792 counts) and 1.3 × 10⁻⁵ for the unstripped
    /// Cs-137 channel with Co downscatter (PLAN.Studio.DesktopChecks.Turn1).</summary>
    internal const string SeparatedCoActivityUCi = "400";

    /// <summary>The pair scene: Cs-137 500 µCi at (−20, 0) mm and Co-60 at (20, 0) mm, 1 m, seed 12345. The default 20 µCi
    /// keeps the survey's documented scene; channel-side assertions need <see cref="SeparatedCoActivityUCi"/>.</summary>
    internal static void TwoIsotopes(StudioWindow ui, string coActivityUCi = "20")
    {
        ui.SetText("SourceX", "-20", "SourceY");
        ui.Invoke("AddSource");
        ui.Choose("SourceIsotope", "Co-60");
        ui.SetText("SourceX", "20", "SourceY");
        ui.SetText("SourceActivity", coActivityUCi, "SourceY");
        ui.SetText("AcquisitionSeed", "12345", "SourceY");
    }

    [DesktopFact]
    public void Spectrum_BandCountAndWindowChange_MatchRetainedHistogram() =>
        Scenario.Run(nameof(Spectrum_BandCountAndWindowChange_MatchRetainedHistogram), output, (ui, record) =>
        {
            AcquireFixed(ui);
            ui.Workspace("Spectrum", "Spectrum.Plot");
            StudioWindow.WaitUntil(() => ui.Rows("Spectrum.Lines").Count > 0, TimeSpan.FromSeconds(10), "emission rows");
            var before = ui.Evidence("Spectrum.Plot");
            var band = before.GetProperty("Bands").EnumerateArray().Single(b => b.GetProperty("Energies").GetString() == "661.7");
            double width = band.GetProperty("HiKeV").GetDouble() - band.GetProperty("LoKeV").GetDouble();
            foreach (string n in new[] { "1", "2" })
            {
                ui.SetText("Spectrum.Window", n, "Spectrum.Plot");
                StudioWindow.WaitUntil(() => ui.Evidence("Spectrum.Plot").GetProperty("WindowFwhm").GetDouble() == double.Parse(n),
                    TimeSpan.FromSeconds(5), "window input committed");
                StudioWindow.WaitUntil(() =>
                {
                    var b = ui.Evidence("Spectrum.Plot").GetProperty("Bands").EnumerateArray().Single(b => b.GetProperty("Energies").GetString() == "661.7");
                    return Math.Abs(b.GetProperty("HiKeV").GetDouble() - b.GetProperty("LoKeV").GetDouble() - width * double.Parse(n) / before.GetProperty("WindowFwhm").GetDouble()) < 1e-8;
                }, TimeSpan.FromSeconds(10), "new window processed");
                var e = ui.Evidence("Spectrum.Plot");
                Assert.Equal(Numbers(e, "Counts"), Numbers(e, "PlotCounts"));
                var b = e.GetProperty("Bands").EnumerateArray().Single(b => b.GetProperty("Energies").GetString() == "661.7");
                long expected = WorkspaceOracle.BandCount(Numbers(e, "CentresKeV"), Numbers(e, "Counts"), b.GetProperty("LoKeV").GetDouble(), b.GetProperty("HiKeV").GetDouble());
                string row = ui.Rows("Spectrum.Lines").Single(r => r.Current.Name.Contains("661.7 keV")).Current.Name;
                long actual = long.Parse(Regex.Match(row, @"([\d,]+) counts").Groups[1].Value.Replace(",", ""));
                record.Set($"window-{n}", new { expected, actual, row, histogram = e });
                Assert.Equal(Broken ? expected + 1 : expected, actual);
                Assert.Equal(Numbers(before, "Counts"), Numbers(e, "Counts"));
            }
        });

    [DesktopFact]
    public void Waveform_SelectedEventListAndMarkers_MatchArrivalWindow() =>
        Scenario.Run(nameof(Waveform_SelectedEventListAndMarkers_MatchArrivalWindow), output, (ui, record) =>
        {
            AcquireFixed(ui);
            ui.Workspace("Waveform", "Waveform.Adc");
            ui.SetText("Waveform.Index", "10", "Waveform.Window");
            StudioWindow.WaitUntil(() => ui.Evidence("Waveform.Adc").TryGetProperty("TriggerIndex", out var v) && v.GetInt32() == 10,
                TimeSpan.FromSeconds(10), "event selected");
            StudioWindow.WaitUntil(() => ui.Evidence("Waveform.Adc").GetProperty("Events").EnumerateArray().Any(e => e.GetProperty("Index").GetInt32() == 10),
                TimeSpan.FromSeconds(10), "selected event processed");
            var data = ui.Evidence("Waveform.Adc");
            double[] times = Numbers(data, "Times");
            int[] expected = WorkspaceOracle.EventsInWindow(times, 10, data.GetProperty("WindowUs").GetDouble());
            int[] rows = ui.Rows("Waveform.Events").Select(r => int.Parse(Regex.Match(r.Current.Name, @"#(\d+)").Groups[1].Value)).ToArray();
            record.Set("oracle", new { expected, rows, data });
            Assert.Equal(Broken ? expected.Append(-1).ToArray() : expected, rows);
            foreach (string plot in new[] { "Waveform.Adc", "Waveform.Shaped" })
            {
                var markers = ui.Evidence(plot).GetProperty("PlotMarkers").EnumerateArray().ToArray();
                Assert.Equal(expected.Select(i => $"#{i}"), markers.Select(m => m.GetProperty("Label").GetString()));
                for (int j = 0; j < expected.Length; j++)
                    Assert.Equal((times[expected[j]] - times[10]) * 1e6, markers[j].GetProperty("X").GetDouble(), 8);
            }
            ui.Toggle("Waveform.RateStudy");
            StudioWindow.WaitUntil(() => ui.Text("Waveform.Note").StartsWith("Rate study: arrivals re-spaced at 50 kcps"), TimeSpan.FromSeconds(10), "rate-study label");
            Assert.Equal(times, Numbers(ui.Evidence("Waveform.Adc"), "Times"));
            record.Step(ui.Text("Waveform.Note"));
        });

    [DesktopFact]
    public void Detector_FaceBeforeStart_LockedUntilReset() =>
        Scenario.Run(nameof(Detector_FaceBeforeStart_LockedUntilReset), output, (ui, record) =>
        {
            ui.Workspace("Detector", "Detector.Face");
            Assert.False(ui.ById("Detector.Face").Current.IsOffscreen);
            Assert.Contains("900 crystals; 18 mm square", ui.ById("Detector.Face").Current.ItemStatus);
            Assert.Equal("Settings for the next acquisition", ui.Text("Detector.Identity"));
            ui.Expand("Detector.Section");
            Assert.True(ui.IsEnabled("Detector.Gap"));
            AcquireFixed(ui);
            record.Set("actual", new { caption = ui.Text("Detector.Identity"), face = ui.ById("Detector.Face").Current.ItemStatus });
            Assert.Equal(Broken ? "Settings for the next acquisition" : "Acquired settings · locked until Reset", ui.Text("Detector.Identity"));
            Assert.False(ui.IsEnabled("Detector.Gap"));
            Assert.False(ui.IsEnabled("Detector.GainSigma"));
            Assert.False(ui.IsEnabled("SourceX"));
            ui.Invoke("ResetAcquisition");
            Assert.True(ui.IsEnabled("Detector.Gap"));
            Assert.Equal("Settings for the next acquisition", ui.Text("Detector.Identity"));
        });

    internal static JsonElement ReadyImage(StudioWindow ui)
    {
        StudioWindow.WaitUntil(() => ui.Evidence("ReconView").TryGetProperty("IsProcessing", out var p) && !p.GetBoolean()
            && ui.Evidence("ReconView").GetProperty("Peaks").GetArrayLength() > 0, TimeSpan.FromSeconds(30), "channels decoded");
        return ui.Evidence("ReconView");
    }

    internal static void AssertPeakAtMaximum(StudioWindow ui, JsonElement data)
    {
        double[] grid = Numbers(data, "Reconstruction");
        Assert.Equal(grid, Numbers(data, "DisplayedImage"));
        Assert.Equal(data.GetProperty("Peaks").GetRawText(), data.GetProperty("FoundMarkers").GetRawText());
        int max = Array.IndexOf(grid, grid.Max()), width = data.GetProperty("Width").GetInt32();
        double origin = data.GetProperty("ReconOriginMm").GetDouble(), step = data.GetProperty("ReconStepMm").GetDouble();
        var peak = data.GetProperty("Peaks")[0];
        // Found peak uses tent interpolation: at most half a reconstruction cell from the global maximum.
        Assert.InRange(Math.Abs(peak.GetProperty("Xmm").GetDouble() - (origin + max % width * step)), 0, step / 2 + 1e-8);
        Assert.InRange(Math.Abs(peak.GetProperty("Ymm").GetDouble() - (origin + max / width * step)), 0, step / 2 + 1e-8);
        Assert.Equal(grid.Max(), peak.GetProperty("Value").GetDouble(), 8);
        var (x, y) = Verdict.ParsePeak(ui.Text("PeakText"));
        Assert.InRange(Math.Abs(x - (origin + max % width * step)), 0, step / 2 + 0.051);
        Assert.InRange(Math.Abs(y - (origin + max / width * step)), 0, step / 2 + 0.051);
    }

    [DesktopFact]
    public void Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks() =>
        Scenario.Run(nameof(Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks), output, (ui, record) =>
        {
            TwoIsotopes(ui, SeparatedCoActivityUCi);
            Assert.Equal("Completed", ui.Acquire(PilotTests.RunTimeout));
            var all = ReadyImage(ui);
            Assert.Equal(2, all.GetProperty("Peaks").GetArrayLength());
            Assert.Equal("2 peaks found", ui.Text("PeakText"));
            ui.Choose("Imaging.Channel", "Co-60");
            var high = ReadyImage(ui);
            AssertPeakAtMaximum(ui, high);
            ui.Choose("Imaging.Channel", "Cs-137");
            var low = ReadyImage(ui);
            AssertPeakAtMaximum(ui, low);
            // Source inputs independently put Cs on the negative side and Co on the positive side; at
            // SeparatedCoActivityUCi each side holds with failure probability ≤ 10⁻³ (bound above).
            // No depth or sub-mm accuracy claim at the undersampled default optics.
            Assert.True(high.GetProperty("Peaks")[0].GetProperty("Xmm").GetDouble() > 0);
            Assert.True(low.GetProperty("Peaks")[0].GetProperty("Xmm").GetDouble() < 0);
            Assert.NotEqual(high.GetProperty("Peaks")[0].GetProperty("Xmm").GetDouble(), low.GetProperty("Peaks")[0].GetProperty("Xmm").GetDouble());
            ui.Toggle("Imaging.Strip");
            var stripped = ReadyImage(ui);
            var ratio = stripped.GetProperty("Ratios").EnumerateArray().Single(r => r.GetProperty("LowIsotope").GetString() == "Cs-137");
            double r = ratio.GetProperty("LowCounts").GetDouble() / ratio.GetProperty("HighCounts").GetDouble();
            double[] rawLow = Numbers(low, "Flood"), rawHigh = Numbers(high, "Flood"), actual = Numbers(stripped, "Flood");
            double[] expected = rawLow.Select((v, i) => Math.Max(0, v - r * rawHigh[i])).ToArray();
            record.Set("oracle", new { r, expectedCounts = expected.Sum(), actualCounts = actual.Sum(), high, low, stripped });
            Assert.Equal(Broken ? expected.Sum() + 1 : expected.Sum(), actual.Sum(), 8);
            for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], actual[i], 10);
            Assert.True(actual.Sum() < rawLow.Sum(), "strip removes high-isotope contamination");
            double net = rawLow.Sum()-r*rawHigh.Sum();
            Assert.Equal(net, stripped.GetProperty("EffectiveCounts").GetDouble());
            double lc = ratio.GetProperty("LowCounts").GetDouble(), hc = ratio.GetProperty("HighCounts").GetDouble();
            Assert.Equal(0, ratio.GetProperty("OverlapCounts").GetInt64());
            double variance = rawLow.Sum()+r*r*rawHigh.Sum()+Math.Pow(rawHigh.Sum(),2)*r*r*(1/lc+1/hc);
            // Fewer than 32 binary64 operations in equivalent positive-term variance expansions.
            Assert.InRange(Math.Abs(variance-stripped.GetProperty("StripCount").GetProperty("Variance").GetDouble()),0,
                32*2.220446049250313e-16*variance);
            Assert.Equal((Math.Round(net),(double?)Math.Round(Math.Sqrt(variance))), Verdict.ParseNetCount(stripped.GetProperty("Summary").GetString()!));
            Assert.Contains("signed difference", ui.Text("Imaging.StripNote"));
            Assert.Equal("(clipped strip values)", stripped.GetProperty("FloodUnit").GetString());
            record.Set("stripRoi", AssertClippedRoi(ui,stripped));
            AssertPeakAtMaximum(ui, stripped);
            Assert.NotEqual(low.GetProperty("Peaks")[0].GetProperty("Value").GetDouble(), stripped.GetProperty("Peaks")[0].GetProperty("Value").GetDouble());
        });

    internal static string AssertClippedRoi(StudioWindow ui, JsonElement evidence, bool draw = true)
    {
        var optics = new OpticsSettings();
        double[] values = Numbers(evidence,"Flood");
        int cells = optics.DetectorPixels;
        // Pick an interior pixel by its displayed value. y is up in the array, down in screen rows.
        int index = Enumerable.Range(0,values.Length).Where(i => i%cells > 0 && i%cells < cells-1 && i/cells > 0 && i/cells < cells-1)
            .OrderByDescending(i => values[i]).First();
        if (draw)
        {
            ui.Select("ToolRoi");
            var geometry = new FloodOracle(ui.Bounds("FloodView"),cells,optics.PixelPitchMm);
            int col=index%cells, row=cells-1-index/cells;
            Pointer.Drag(geometry.CellCorner(col,row),geometry.CellCorner(col+1,row+1));
            StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 1,TimeSpan.FromSeconds(3),"one clipped flood ROI");
        }
        var rowValue = MeasurementRow.Parse(ui.Rows("MeasurementList")[0].Current.Name);
        Assert.Equal(("ROI","Flood"),(rowValue.Kind,rowValue.Pane));
        Assert.EndsWith(" clipped strip values",rowValue.Value);
        Assert.Equal(1,Verdict.ParseRoiDetail(ui.Text("MeasurementDetail")).Pixels);
        // Exact formatting oracle, not a physics tolerance: large values are integral, smaller ones have 3 sig figs.
        string number = values[index].ToString(Math.Abs(values[index]) >= 100 ? "N0" : "G3");
        Assert.Equal($"Σ {number} clipped strip values",rowValue.Value);
        return rowValue.Value;
    }

    [DesktopFact]
    public void Imaging_RefocusAndSweep_MatchProjectionAndHalfMaxInterval() =>
        Scenario.Run(nameof(Imaging_RefocusAndSweep_MatchProjectionAndHalfMaxInterval), output, (ui, record) =>
        {
            AcquireFixed(ui);
            ui.Choose("Imaging.Channel", "Cs-137");
            var before = ReadyImage(ui);
            long counts = ui.Counts;
            ui.SetText("Imaging.FocalPlane", "500", "Imaging.Window");
            var after = ReadyImage(ui);
            Assert.Equal(Numbers(before, "Flood"), Numbers(after, "Flood"));
            Assert.Equal(counts, ui.Counts);
            Assert.Equal(Broken ? 0.6 : 0.5, after.GetProperty("ReconStepMm").GetDouble() / before.GetProperty("ReconStepMm").GetDouble(), 8);
            AssertPeakAtMaximum(ui, after);
            ui.Expand("Imaging.SweepSection");
            StudioWindow.WaitUntil(() => ui.IsEnabled("Imaging.Sweep"), TimeSpan.FromSeconds(5), "sweep enabled");
            ui.Invoke("Imaging.Sweep");
            StudioWindow.WaitUntil(() => ui.Exists("Imaging.FocusCurve") && ui.Evidence("Imaging.FocusCurve").GetProperty("SweepResult").ValueKind == JsonValueKind.Object,
                TimeSpan.FromSeconds(30), "focus sweep published");
            var evidence = ui.Evidence("Imaging.FocusCurve");
            var tracks = evidence.GetProperty("SweepResult").GetProperty("Tracks");
            Assert.Equal(1, tracks.GetArrayLength());
            foreach (var track in tracks.EnumerateArray())
            {
                var curve = track.GetProperty("Curve").EnumerateArray().ToArray();
                var expected = WorkspaceOracle.HalfMax(curve.Select(c => c.GetProperty("PlaneMm").GetDouble()).ToArray(), curve.Select(c => c.GetProperty("Prominence").GetDouble()).ToArray());
                var interval = track.GetProperty("Interval");
                Assert.Equal(expected.Lo, interval.GetProperty("LoMm").GetDouble(), 7);
                Assert.Equal(expected.Hi, interval.GetProperty("HiMm").GetDouble(), 7);
                Assert.Equal(expected.Near, interval.GetProperty("NearCensored").GetBoolean());
                Assert.Equal(expected.Far, interval.GetProperty("FarCensored").GetBoolean());
                var band = evidence.GetProperty("PlotBands")[0];
                Assert.Equal(expected.Lo, band.GetProperty("Lo").GetDouble(), 7);
                Assert.Equal(expected.Hi, band.GetProperty("Hi").GetDouble(), 7);
            }
            Assert.Equal(counts, ui.Counts);
            record.Set("oracle", new { beforeStep = before.GetProperty("ReconStepMm"), afterStep = after.GetProperty("ReconStepMm"), counts, evidence });
        });
}
