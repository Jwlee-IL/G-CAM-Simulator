using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>Diagnostic desktop frames, never automatic pixel baselines.</summary>
public sealed class PolishSurveyTests(ITestOutputHelper output)
{
    [DesktopFact]
    public void AllWorkspaces_BothThemesAndWindowSizes_CapturesPolishSurvey() =>
        Scenario.Run(Environment.GetEnvironmentVariable("GCAM_README_CAPTURE_ONLY") == "1"
            ? "README_Imaging_Co200_FixedSeed" : nameof(AllWorkspaces_BothThemesAndWindowSizes_CapturesPolishSurvey), output, (ui, record) =>
        {
            if (Environment.GetEnvironmentVariable("GCAM_README_CAPTURE_ONLY") == "1")
            {
                CaptureReadme(ui, record);
                return;
            }
            WorkspaceScenarioTests.TwoIsotopes(ui);
            Assert.Equal("Completed", ui.Acquire(PilotTests.RunTimeout));
            StudioWindow.WaitUntil(() => ui.ById("FloodView").Current.ItemStatus.StartsWith("zoom"), TimeSpan.FromSeconds(10), "flood rendered");
            string directory = Path.Combine(RepoPaths.Root(), "docs", "assets", "studio-polish-survey");
            Directory.CreateDirectory(directory);
            foreach (var workspace in new[] { (Name: "Imaging", Surface: "FloodView"), (Name: "Spectrum", Surface: "Spectrum.Plot"),
                (Name: "Waveform", Surface: "Waveform.Adc"), (Name: "Detector", Surface: "Detector.Face") })
            {
                ui.Workspace(workspace.Name, workspace.Surface);
                if (workspace.Name == "Waveform") ui.SetText("Waveform.Index", "10", "Waveform.Window");
                Thread.Sleep(500);
                foreach (var size in new[] { (Width: 1280, Height: 800), (Width: 1440, Height: 900) })
                {
                    ui.Normalise(size.Width, size.Height);
                    foreach (string theme in new[] { "dark", "light" })
                    {
                        if (ui.Text("ThemeToggle") != (theme == "dark" ? "Light theme" : "Dark theme")) ui.Invoke("ThemeToggle");
                        Thread.Sleep(300);
                        string path = Path.Combine(directory, $"{workspace.Name.ToLowerInvariant()}-{theme}-{size.Width}x{size.Height}.png");
                        RunRecord.CaptureWindow(ui.Element, path);
                        record.Step($"captured {path}");
                        record.Set($"{workspace.Name}-{theme}-{size.Width}", new { status = ui.Text("StatusText"), surface = ui.ById(workspace.Surface).Current.ItemStatus });
                        output.WriteLine(path);
                    }
                }
            }
        });

    private static void CaptureReadme(StudioWindow ui, RunRecord record)
    {
        WorkspaceScenarioTests.TwoIsotopes(ui);
        ui.SetText("SourceActivity", "200", "SourceY");
        ui.SetText("AcquisitionLiveTime", "60", "SourceY");
        ui.SetText("AcquisitionSpeed", "10", "SourceY");
        Assert.Equal("200", ui.Value("SourceActivity"));
        Assert.Equal("Completed", ui.Acquire(PilotTests.RunTimeout));
        ui.Workspace("Imaging", "ReconView");
        ui.Choose("Imaging.Channel", "All");
        StudioWindow.WaitUntil(() => ui.Evidence("ReconView").TryGetProperty("IsProcessing", out var p)
            && !p.GetBoolean() && ui.Evidence("ReconView").GetProperty("Peaks").GetArrayLength() == 2,
            TimeSpan.FromSeconds(30), "two isotope peaks decoded");
        var data = ui.Evidence("ReconView");
        Assert.Equal(1000, data.GetProperty("FocalDistanceMm").GetDouble());
        Assert.False(data.GetProperty("Strip").GetBoolean());
        // Mask-cell projection is one resolution element, derived from the unchanged default physical inputs.
        var optics = new Gcam.Configuration.OpticsSettings();
        double elementMm = optics.CellPitchMm * 1000 / optics.MaskDetectorDistanceMm;
        Assert.Contains($"Resolution element {elementMm:0.##} mm", ui.Text("Imaging.Geometry"));
        var results = data.GetProperty("Peaks").EnumerateArray().Select(p =>
        {
            string isotope = p.GetProperty("Isotope").GetString()!;
            double x = p.GetProperty("Xmm").GetDouble(), y = p.GetProperty("Ymm").GetDouble();
            double truthX = isotope == "Cs-137" ? -20 : 20;
            return new { isotope, truthXmm = truthX, truthYmm = 0, xMm = x, yMm = y,
                errorMm = Math.Sqrt((x - truthX) * (x - truthX) + y * y) };
        }).ToArray();
        record.Set("readmeCapture", new { seed = 12345, liveTimeS = 60, speed = 10,
            csActivityUCi = 500, coActivityUCi = 200, distanceMm = 1000, channel = "All", theme = "dark",
            resolutionElementMm = elementMm, peaks = results, counts = ui.Counts });
        Assert.Equal(new[] { "Co-60", "Cs-137" }, results.Select(p => p.isotope).Order().ToArray());
        Assert.All(results, p => Assert.True(p.errorMm <= elementMm,
            $"{p.isotope} found ({p.xMm}, {p.yMm}) mm: error {p.errorMm} exceeds element {elementMm} mm"));
        ui.Normalise(1440, 900);
        if (ui.Text("ThemeToggle") != "Light theme") ui.Invoke("ThemeToggle");
        Thread.Sleep(300);
        string path = Path.Combine(RepoPaths.Root(), "docs", "assets", "studio-desktop-imaging.png");
        RunRecord.CaptureWindow(ui.Element, path);
        record.Step($"README capture written after truth check: {path}");
    }
}
