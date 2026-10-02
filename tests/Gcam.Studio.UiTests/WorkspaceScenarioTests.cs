using System.Text.Json;
using System.Text.RegularExpressions;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

public sealed class WorkspaceScenarioTests(ITestOutputHelper output)
{
    private static bool Broken => Environment.GetEnvironmentVariable(PilotTests.BreakVerdictVariable) == "1";
    private static double[] Numbers(JsonElement e, string key) => e.GetProperty(key).EnumerateArray().Select(v => v.GetDouble()).ToArray();
    private static void AcquireFixed(StudioWindow ui)
    {
        ui.SetText("AcquisitionSeed", "12345", "SourceY");
        Assert.Equal("Completed", ui.Acquire(PilotTests.RunTimeout));
    }

    internal static void TwoIsotopes(StudioWindow ui)
    {
        ui.SetText("SourceX", "-20", "SourceY");
        ui.Invoke("AddSource");
        ui.Choose("SourceIsotope", "Co-60");
        ui.SetText("SourceX", "20", "SourceY");
        ui.SetText("SourceActivity", "20", "SourceY");
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

    private static JsonElement ReadyImage(StudioWindow ui)
    {
        StudioWindow.WaitUntil(() => ui.Evidence("ReconView").TryGetProperty("IsProcessing", out var p) && !p.GetBoolean()
            && ui.Evidence("ReconView").GetProperty("Peaks").GetArrayLength() > 0, TimeSpan.FromSeconds(30), "channels decoded");
        return ui.Evidence("ReconView");
    }

    private static void AssertPeakAtMaximum(StudioWindow ui, JsonElement data)
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
            TwoIsotopes(ui);
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
            // Source inputs independently put Cs on the negative side and Co on the positive side.
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
            AssertPeakAtMaximum(ui, stripped);
            Assert.NotEqual(low.GetProperty("Peaks")[0].GetProperty("Value").GetDouble(), stripped.GetProperty("Peaks")[0].GetProperty("Value").GetDouble());
        });

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
