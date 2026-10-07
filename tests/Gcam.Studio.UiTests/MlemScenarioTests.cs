using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;
using static Gcam.Studio.UiTests.WorkspaceScenarioTests;

namespace Gcam.Studio.UiTests;

/// <summary>TODO-36's desktop checks as scenarios (TODO-38 / DC-3; SR-IMG-07). Every acquisition fixes seed 12345.
/// Expectations come from the specification (the selector's item and unit, MD-4 / MD-10's 400 iterations, the
/// strip formula) or from the product's own retained data recomputed here (floods, ratios, status counts) — never from
/// the app's reconstruction code.</summary>
public sealed class MlemScenarioTests(ITestOutputHelper output)
{
    private static bool Broken => Environment.GetEnvironmentVariable(PilotTests.BreakVerdictVariable) == "1";

    // The selector's items as specified (MD-3 default cross-correlation; MD-10: 400 iterations).
    private const string MlemItem = "MLEM (pixel-area, 400 iterations)";
    private const string CcItem = "Cross-correlation";
    private const string MlemUnit = "(MLEM λ)";
    private const string CcUnit = "(decoded)";

    /// <summary>The reconstruction view once the displayed image was decoded with <paramref name="method"/> (the published
    /// view's own method, not the selector's) and, when <paramref name="idle"/>, the worker is idle.</summary>
    /// <remarks>TODO-38 turn 2: <c>idle</c> is only meaningful once the acquisition has stopped. While acquiring under MLEM a
    /// refresh decodes for 0.39–0.56 s (measured headless at the default optics, 400 iterations, two channels) against the
    /// 0.25 s snapshot tick, so the worker runs back to back and IsProcessing is false only for the instant between one
    /// publication and the next request — polling cannot observe it. A live wait therefore keys on the displayed method;
    /// after Stop the worker drains and stays idle.</remarks>
    private static JsonElement Decoded(StudioWindow ui, string method, Func<JsonElement, bool>? also = null, bool idle = true)
    {
        StudioWindow.WaitUntil(() =>
        {
            var e = ui.Evidence("ReconView");
            return e.TryGetProperty("DisplayedMethod", out var m) && m.GetString() == method
                && (!idle || e.TryGetProperty("IsProcessing", out var p) && !p.GetBoolean())
                && e.GetProperty("Reconstruction").ValueKind == JsonValueKind.Array
                && (also?.Invoke(e) ?? true);
        }, TimeSpan.FromSeconds(30), $"{method} reconstruction displayed{(idle ? ", worker idle" : "")}");
        return ui.Evidence("ReconView");
    }

    /// <summary>MLEM's multiplicative update from a positive start keeps λ ≥ 0 exactly (no tolerance needed).</summary>
    private static void AssertNonNegative(JsonElement data) =>
        Assert.All(Numbers(data, "Reconstruction"), v => Assert.True(v >= 0, $"λ = {v} < 0"));

    private static string ReadoutUnit(StudioWindow ui)
    {
        var r = ui.Bounds("ReconView");
        Pointer.MoveTo(new Point(Math.Round(r.X + r.Width / 2), Math.Round(r.Y + r.Height / 2)));
        // One read: the string judged is the string that satisfied the wait.
        string readout = "";
        StudioWindow.WaitUntil(() => (readout = ui.Text("ReconReadout")).Length > 0, TimeSpan.FromSeconds(2), "reconstruction readout shown");
        return Verdict.ParseReadoutUnit(readout);
    }

    private static string Note(StudioWindow ui) => ui.Exists("Imaging.ReconstructionNote") ? ui.Text("Imaging.ReconstructionNote") : "";

    private static void StartLong(StudioWindow ui)
    {
        // 600 s at ×1 stays acquiring for the whole scenario (10 wall minutes).
        ui.SetText("AcquisitionLiveTime", "600", "SourceY");
        ui.SetText("AcquisitionSpeed", "1", "SourceY");
        ui.SetText("AcquisitionSeed", "12345", "SourceY");
        ui.Invoke("StartAcquisition");
        StudioWindow.WaitUntil(() => ui.RunState == "Acquiring", TimeSpan.FromSeconds(5), "acquisition started");
        StudioWindow.WaitUntil(() => ui.Counts > 0, TimeSpan.FromSeconds(20), "first counts");
    }

    /// <summary>The All image's flood is the acquisition's own flood: once the worker is idle on the latest snapshot its sum
    /// equals the status line's counts (each list-mode event adds one count) — a stale image would lag.</summary>
    private static void AssertNotStale(StudioWindow ui, JsonElement data) =>
        Assert.Equal(Broken ? ui.Counts + 1 : ui.Counts, (long)Math.Round(Numbers(data, "Flood").Sum()));

    [DesktopFact]
    public void Mlem_SelectedDuringAcquisition_NonNegativeWithUnitNoteAndAdvancingStatus() =>
        Scenario.Run(nameof(Mlem_SelectedDuringAcquisition_NonNegativeWithUnitNoteAndAdvancingStatus), output, (ui, record) =>
        {
            Assert.Equal(CcItem, ui.SelectedItem("Imaging.Reconstruction"));   // MD-3: cross-correlation by default
            Assert.Equal("", Note(ui));
            StartLong(ui);
            ui.Choose("Imaging.Channel", "Cs-137");   // a channel's found peak and estimate come from the same image
            var cc = Decoded(ui, "CrossCorrelation", idle: false);
            Assert.Equal(CcUnit, ReadoutUnit(ui));

            ui.Choose("Imaging.Reconstruction", MlemItem);
            Assert.Equal(MlemItem, ui.SelectedItem("Imaging.Reconstruction"));
            var ml = Decoded(ui, "Mlem", idle: false);
            AssertNonNegative(ml);
            Assert.Equal(Broken ? CcUnit : MlemUnit, ReadoutUnit(ui));
            Assert.Equal(MlemUnit, ml.GetProperty("ReconstructionUnit").GetString());
            string note = Note(ui);
            Assert.Contains("400 iterations", note);
            Assert.Contains("≈50%", note);                                // MD-10: 50.4 % of 250-count frames
            Assert.DoesNotContain("not measured", note);                   // default optics, focal plane 1000 mm

            // Transport is independent of the decode (MD-5): the status keeps advancing while MLEM is shown.
            long before = ui.Counts;
            StudioWindow.WaitUntil(() => ui.Counts > before, TimeSpan.FromSeconds(10), "status counts advance under MLEM");
            Assert.Equal("Acquiring", ui.RunState);
            ui.Invoke("StopAcquisition");
            StudioWindow.WaitUntil(() => ui.RunState == "Stopped", TimeSpan.FromSeconds(5), "stopped");
            // On the frozen data the found peak and the chip agree with the λ image's own maximum (half a cell + text
            // rounding, as for cross-correlation).
            var frozen = Decoded(ui, "Mlem");
            Thread.Sleep(600);   // two refresh periods: the stopped acquisition publishes nothing more
            frozen = Decoded(ui, "Mlem");
            AssertNonNegative(frozen);
            AssertPeakAtMaximum(ui, frozen);

            ui.Choose("Imaging.Reconstruction", CcItem);
            var back = Decoded(ui, "CrossCorrelation");
            Assert.Equal(CcUnit, ReadoutUnit(ui));
            Assert.Equal("", Note(ui));
            record.Set("actual", new { ccMin = Numbers(cc, "Reconstruction").Min(), mlemMin = Numbers(ml, "Reconstruction").Min(), note, before, after = ui.Counts });
        });

    [DesktopFact]
    public void Mlem_WithStrip_ReportsNetCountsAndFindsCsOnItsSide() =>
        Scenario.Run(nameof(Mlem_WithStrip_ReportsNetCountsAndFindsCsOnItsSide), output, (ui, record) =>
        {
            TwoIsotopes(ui, SeparatedCoActivityUCi);
            Assert.Equal("Completed", ui.Acquire(PilotTests.RunTimeout));
            ui.Choose("Imaging.Channel", "Co-60");
            var high = ReadyImage(ui);
            ui.Choose("Imaging.Channel", "Cs-137");
            var low = ReadyImage(ui);
            ui.Toggle("Imaging.Strip");
            var ccStrip = Decoded(ui, "CrossCorrelation", e => e.GetProperty("Strip").GetBoolean());
            var ratio = ccStrip.GetProperty("Ratios").EnumerateArray().Single(r => r.GetProperty("LowIsotope").GetString() == "Cs-137");
            double r = ratio.GetProperty("LowCounts").GetDouble() / ratio.GetProperty("HighCounts").GetDouble();
            double[] rawLow = Numbers(low, "Flood"), rawHigh = Numbers(high, "Flood");
            double clipped = rawLow.Select((v, i) => Math.Max(0, v - r * rawHigh[i])).Sum();
            double net = rawLow.Sum() - r * rawHigh.Sum();
            // Cross-correlation keeps its clipped strip count (TODO-39), labelled as such (MD-11).
            Assert.Equal(clipped, ccStrip.GetProperty("EffectiveCounts").GetDouble(), 6);
            Assert.Contains("counts (clipped strip sum)", ccStrip.GetProperty("Summary").GetString());

            ui.Choose("Imaging.Reconstruction", MlemItem);
            var ml = Decoded(ui, "Mlem", e => e.GetProperty("Strip").GetBoolean());
            AssertNonNegative(ml);
            // The displayed flood stays the clipped strip view; the count is the net Σ low − R·Σ high (MD-6). Six decimals:
            // the service sums the per-pixel background R·high_i, the oracle multiplies the total (rounding ~1e-12 relative).
            Assert.Equal(Numbers(ccStrip, "Flood"), Numbers(ml, "Flood"));
            Assert.Equal(Broken ? net + 1 : net, ml.GetProperty("EffectiveCounts").GetDouble(), 6);
            string summary = ml.GetProperty("Summary").GetString()!;
            Assert.Contains("net counts (Σ low − R·Σ high)", summary);
            long shown = long.Parse(Regex.Match(summary, @"Cs-137 · (-?[\d,]+) net counts").Groups[1].Value.Replace(",", ""));
            Assert.Equal(Math.Round(net), shown);
            Assert.True(net < rawLow.Sum(), "the Co-60 downscatter is removed from the Cs-137 count");
            AssertPeakAtMaximum(ui, ml);
            // Cs-137 at x = −20 mm. Measured, not derived (TODO-36 MD-6): MLEM with the background term put the Cs peak
            // within one element (8.75 mm) of the source in ≥ 99.9 % of acquisitions at 1000 Cs counts and Co:Cs up to
            // 4 : 1; here ~1590 Cs counts and Co:Cs 0.8 : 1.
            Assert.True(ml.GetProperty("Peaks")[0].GetProperty("Xmm").GetDouble() < 0);
            record.Set("oracle", new { r, clipped, net, summary, ccSummary = ccStrip.GetProperty("Summary").GetString() });
        });

    [DesktopFact]
    public void Mlem_FocalPlane1500_FlagsTheUnmeasuredIterationCount() =>
        Scenario.Run(nameof(Mlem_FocalPlane1500_FlagsTheUnmeasuredIterationCount), output, (ui, record) =>
        {
            ui.SetText("AcquisitionSeed", "12345", "SourceY");
            Assert.Equal("Completed", ui.Acquire(PilotTests.RunTimeout));
            ui.Choose("Imaging.Reconstruction", MlemItem);
            var at1000 = Decoded(ui, "Mlem");
            Assert.DoesNotContain("not measured", Note(ui));
            ui.SetText("Imaging.FocalPlane", "1500", "Imaging.Window");
            // Step = element / 4 with element = cell · F / D: 1500 / 1000 exactly (both well above the 0.2 mm floor).
            var at1500 = Decoded(ui, "Mlem", e => Math.Abs(e.GetProperty("ReconStepMm").GetDouble() / at1000.GetProperty("ReconStepMm").GetDouble() - 1.5) < 1e-12);
            AssertNonNegative(at1500);
            // MD-4: the iteration count was selected at the default optics and the 1000 mm plane only.
            Assert.Contains(Broken ? "measured for every plane" : "Iteration count not measured for these optics.", Note(ui));
            ui.SetText("Imaging.FocalPlane", "1000", "Imaging.Window");
            Decoded(ui, "Mlem", e => e.GetProperty("ReconStepMm").GetDouble() == at1000.GetProperty("ReconStepMm").GetDouble());
            Assert.DoesNotContain("not measured", Note(ui));
            record.Set("actual", new { note1500 = Note(ui) });
        });

    [DesktopFact]
    public void MethodSwitch_KeepsMeasurementsOnTheSameGrid() =>
        Scenario.Run(nameof(MethodSwitch_KeepsMeasurementsOnTheSameGrid), output, (ui, record) =>
        {
            ui.SetText("AcquisitionSeed", "12345", "SourceY");
            Assert.Equal("Completed", ui.Acquire(PilotTests.RunTimeout));
            Decoded(ui, "CrossCorrelation");
            var view = ui.Bounds("ReconView");
            var c = new Point(Math.Round(view.X + view.Width / 2), Math.Round(view.Y + view.Height / 2));
            double s = Math.Round(0.3 * Math.Min(view.Width, view.Height));   // stays on the (square, centred) image

            // An angle: its oracle is the three screen points (scale-, translation- and flip-invariant).
            ui.Select("ToolAngle");
            Point arm1 = c + new Vector(s, 0.5 * s), vertex = c + new Vector(-0.6 * s, 0.5 * s), arm2 = c + new Vector(-0.2 * s, -0.6 * s);
            Pointer.Click(arm1); Pointer.Click(vertex); Pointer.Click(arm2);
            StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 1, TimeSpan.FromSeconds(3), "angle row");
            double expected = Verdict.AngleDeg(arm1, vertex, arm2), tolerance = Verdict.AngleToleranceDeg(arm1, vertex, arm2);
            // An ROI around the centre (the source at (0, 0) mm).
            ui.Select("ToolRoi");
            Pointer.Drag(c + new Vector(-0.2 * s, -0.2 * s), c + new Vector(0.2 * s, 0.2 * s));
            StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 2, TimeSpan.FromSeconds(3), "ROI row");
            var angleBefore = MeasurementRow.Parse(ui.Rows("MeasurementList")[0].Current.Name);
            int pixelsBefore = Verdict.ParseRoiDetail(ui.Text("MeasurementDetail")).Pixels;

            ui.Choose("Imaging.Reconstruction", MlemItem);
            Decoded(ui, "Mlem");
            StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 2, TimeSpan.FromSeconds(3), "measurements kept");
            var rows = ui.Rows("MeasurementList").Select(e => MeasurementRow.Parse(e.Current.Name)).ToArray();
            Assert.Equal(("M1", "Angle", "Recon"), (rows[0].Name, rows[0].Kind, rows[0].Pane));
            Assert.Equal(("M2", "ROI", "Recon"), (rows[1].Name, rows[1].Kind, rows[1].Pane));
            // Same grid, same mm: geometry is unchanged exactly; the ROI's value follows the new image, and λ ≥ 0 makes
            // its sum non-negative.
            Assert.Equal(angleBefore.Value, rows[0].Value);
            Assert.True(Verdict.Within(rows[0].AngleDeg(), Broken ? expected + 5 : expected, tolerance), $"angle {rows[0].AngleDeg()}° vs {expected:F2}° ± {tolerance:F2}");
            Assert.Equal(pixelsBefore, Verdict.ParseRoiDetail(ui.Text("MeasurementDetail")).Pixels);
            Assert.True(Verdict.ParseSum(rows[1].Value) >= 0, $"MLEM ROI sum {rows[1].Value}");
            record.Set("actual", new { expected, tolerance, rows = rows.Select(r => r.ToString()).ToArray(), pixelsBefore });
        });

    [DesktopFact]
    public void FocusSweep_UnderMlem_IsTheCrossCorrelationSweep() =>
        Scenario.Run(nameof(FocusSweep_UnderMlem_IsTheCrossCorrelationSweep), output, (ui, record) =>
        {
            ui.SetText("AcquisitionSeed", "12345", "SourceY");
            Assert.Equal("Completed", ui.Acquire(PilotTests.RunTimeout));
            ui.Choose("Imaging.Channel", "Cs-137");
            Decoded(ui, "CrossCorrelation");
            ui.Expand("Imaging.SweepSection");
            JsonElement Sweep(double? notTime)
            {
                StudioWindow.WaitUntil(() => ui.IsEnabled("Imaging.Sweep"), TimeSpan.FromSeconds(10), "sweep enabled");
                ui.Invoke("Imaging.Sweep");
                StudioWindow.WaitUntil(() => ui.Exists("Imaging.FocusCurve")
                    && ui.Evidence("Imaging.FocusCurve").GetProperty("SweepResult") is { ValueKind: JsonValueKind.Object } r
                    && r.GetProperty("ProcessingTime").GetString() is { } t && (notTime is null || TimeSpan.Parse(t).TotalMilliseconds != notTime),
                    TimeSpan.FromSeconds(30), "focus sweep published");
                return ui.Evidence("Imaging.FocusCurve").GetProperty("SweepResult");
            }
            var cc = Sweep(null);
            ui.Choose("Imaging.Reconstruction", MlemItem);
            Decoded(ui, "Mlem");
            // A fresh sweep is recognised by its own worker time (a re-run measures a different wall time).
            var ml = Sweep(TimeSpan.Parse(cc.GetProperty("ProcessingTime").GetString()!).TotalMilliseconds);
            // The sweep cross-correlates the retained flood whatever the selector shows (SR-IMG-07): same tracks exactly.
            string Tracks(JsonElement e) => e.GetProperty("Tracks").GetRawText();
            Assert.Equal(Broken ? Tracks(cc) + " " : Tracks(cc), Tracks(ml));
            foreach (var track in ml.GetProperty("Tracks").EnumerateArray())
            {
                var curve = track.GetProperty("Curve").EnumerateArray().ToArray();
                var expected = WorkspaceOracle.HalfMax(curve.Select(c => c.GetProperty("PlaneMm").GetDouble()).ToArray(), curve.Select(c => c.GetProperty("Prominence").GetDouble()).ToArray());
                Assert.Equal(expected.Lo, track.GetProperty("Interval").GetProperty("LoMm").GetDouble(), 7);
                Assert.Equal(expected.Hi, track.GetProperty("Interval").GetProperty("HiMm").GetDouble(), 7);
            }
            record.Set("actual", new { ccTime = cc.GetProperty("ProcessingTime").GetString(), mlTime = ml.GetProperty("ProcessingTime").GetString() });
        });

    [DesktopFact]
    public void MlemSelection_SurvivesStopContinueAndReset_WithoutAStaleImage() =>
        Scenario.Run(nameof(MlemSelection_SurvivesStopContinueAndReset_WithoutAStaleImage), output, (ui, record) =>
        {
            StartLong(ui);
            ui.Choose("Imaging.Reconstruction", MlemItem);
            Decoded(ui, "Mlem", idle: false);
            ui.Invoke("StopAcquisition");
            StudioWindow.WaitUntil(() => ui.RunState == "Stopped", TimeSpan.FromSeconds(5), "stopped");
            long kept = ui.Counts;
            var stopped = Decoded(ui, "Mlem", e => Math.Round(Numbers(e, "Flood").Sum()) == kept);
            AssertNotStale(ui, stopped);
            AssertNonNegative(stopped);

            ui.Invoke("StartAcquisition");   // Continue
            StudioWindow.WaitUntil(() => ui.RunState == "Acquiring", TimeSpan.FromSeconds(5), "continued");
            StudioWindow.WaitUntil(() => ui.Counts > kept, TimeSpan.FromSeconds(20), "counts grow after Continue");
            ui.Invoke("StopAcquisition");
            StudioWindow.WaitUntil(() => ui.RunState == "Stopped", TimeSpan.FromSeconds(5), "stopped again");
            long continued = ui.Counts;
            Assert.Equal(MlemItem, ui.SelectedItem("Imaging.Reconstruction"));
            var again = Decoded(ui, "Mlem", e => Math.Round(Numbers(e, "Flood").Sum()) == continued);
            AssertNotStale(ui, again);
            AssertNonNegative(again);

            ui.Invoke("ResetAcquisition");
            StudioWindow.WaitUntil(() => ui.RunState == "Empty", TimeSpan.FromSeconds(5), "reset");
            Assert.Equal(MlemItem, ui.SelectedItem("Imaging.Reconstruction"));   // a view setting, not acquisition data
            Assert.False(ui.Evidence("ReconView").TryGetProperty("Reconstruction", out _), "no image after Reset");
            ui.SetText("AcquisitionLiveTime", "60", "SourceY");
            ui.SetText("AcquisitionSpeed", "10", "SourceY");
            Assert.Equal("Completed", ui.Acquire(PilotTests.RunTimeout));
            long fresh = ui.Counts;
            var next = Decoded(ui, "Mlem", e => Math.Round(Numbers(e, "Flood").Sum()) == fresh);
            AssertNotStale(ui, next);
            AssertNonNegative(next);
            record.Set("actual", new { kept, continued, fresh });
        });
}
