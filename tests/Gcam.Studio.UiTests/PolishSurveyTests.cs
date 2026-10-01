using System.Windows.Automation;
using Gcam.Configuration;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

/// <summary>Diagnostic captures, never pixel pass/fail baselines. The author selects the polish work.</summary>
public sealed class PolishSurveyTests(ITestOutputHelper output)
{
    [DesktopFact]
    public void Imaging_BothThemesAndWindowSizes_CapturesPolishSurvey()
    {
        Scenario.Run(nameof(Imaging_BothThemesAndWindowSizes_CapturesPolishSurvey), output, (ui, record) =>
        {
            Assert.True(ui.Exists("Workspace.Imaging") && ui.Exists("Workspace.Spectrum"), "workspace switch shown");
            Assert.Equal("Completed", ui.Acquire(TimeSpan.FromSeconds(30)));
            StudioWindow.WaitUntil(() => ui.ById("FloodView").Current.ItemStatus.StartsWith("zoom"),
                TimeSpan.FromSeconds(5), "flood rendered");
            ui.Select("ToolDistance");
            var optics = new OpticsSettings();
            var oracle = new FloodOracle(ui.Bounds("FloodView"), optics.DetectorPixels, optics.PixelPitchMm);
            Pointer.Drag(oracle.ScreenAt(0.2, 0.8), oracle.ScreenAt(0.75, 0.3));
            StudioWindow.WaitUntil(() => ui.Rows("MeasurementList").Count == 1, TimeSpan.FromSeconds(3), "survey measurement");
            ui.Select("ToolPan");
            string directory = Path.Combine(RepoPaths.Root(), "docs", "assets", "studio-polish-survey");
            Directory.CreateDirectory(directory);
            foreach (var size in new[] { (Width: 1280, Height: 800), (Width: 1440, Height: 900) })
            {
                ui.Normalise(size.Width, size.Height);
                // Real keyboard input reaches the heatmap and returns it to fit before every capture.
                ui.Keys("FloodView", "{+}");
                Assert.Contains("zoom 1.3x", ui.ById("FloodView").Current.ItemStatus);
                ui.Keys("FloodView", "0");
                Assert.Contains("zoom 1x", ui.ById("FloodView").Current.ItemStatus);
                foreach (string theme in new[] { "dark", "light" })
                {
                    if (ui.Text("ThemeToggle") != (theme == "dark" ? "Light theme" : "Dark theme")) ui.Invoke("ThemeToggle");
                    // Walk focus with Tab and record what is announced; screenshots include a focus ring.
                    ui.Keys("ToolPan", "{TAB}");
                    var focused = AutomationElement.FocusedElement;
                    record.Set($"focus-{theme}-{size.Width}", new { focused.Current.AutomationId, focused.Current.Name });
                    string path = Path.Combine(directory, $"imaging-{theme}-{size.Width}x{size.Height}.png");
                    RunRecord.CaptureWindow(ui.Element, path);
                    record.Step($"captured {path}");
                    output.WriteLine(path);
                }
            }

            // The Spectrum workspace, same sizes and themes, after the same acquisition.
            // UIA Select on the switch checks the button but does not change workspace (found 2026-10-01);
            // the keyboard shortcut drives the command path. Restore Select once the app is fixed.
            ui.Keys("ToolPan", "^2");
            StudioWindow.WaitUntil(() => ui.Exists("Spectrum.Plot"), TimeSpan.FromSeconds(3), "spectrum shown");
            foreach (var size in new[] { (Width: 1280, Height: 800), (Width: 1440, Height: 900) })
            {
                ui.Normalise(size.Width, size.Height);
                foreach (string theme in new[] { "dark", "light" })
                {
                    if (ui.Text("ThemeToggle") != (theme == "dark" ? "Light theme" : "Dark theme")) ui.Invoke("ThemeToggle");
                    record.Set($"spectrum-{theme}-{size.Width}", new { plot = ui.ById("Spectrum.Plot").Current.ItemStatus,
                        lines = ui.Rows("Spectrum.Lines").Select(r => r.Current.Name).ToArray() });
                    string path = Path.Combine(directory, $"spectrum-{theme}-{size.Width}x{size.Height}.png");
                    RunRecord.CaptureWindow(ui.Element, path);
                    record.Step($"captured {path}");
                    output.WriteLine(path);
                }
            }
        });
    }
}
