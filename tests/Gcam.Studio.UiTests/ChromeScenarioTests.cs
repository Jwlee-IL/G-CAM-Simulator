using System.Windows.Automation;
using System.Runtime.InteropServices;
using Gcam.Studio.UiTests.Harness;
using Xunit.Abstractions;

namespace Gcam.Studio.UiTests;

public sealed class ChromeScenarioTests(ITestOutputHelper output)
{
    [DesktopFact]
    public void BorderlessCaption_HasNoNativeTitleBar_AndStartsAtWindowTop()
    {
        Scenario.Run("borderless-caption", output, (ui, record) =>
        {
            Assert.Empty(ui.Element.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TitleBar)).Cast<AutomationElement>());
            var window = ui.Element.Current.BoundingRectangle;
            var top = ui.Bounds("Window.TopBar");
            Assert.InRange(Math.Abs(top.Top - window.Top), 0, 1); // physical-pixel rounding only
            Assert.InRange(Math.Abs(ui.Bounds("Window.Close").Right - window.Right), 0, 1);
            foreach (string tab in new[] { "Scene", "Optics", "Detector" }) ui.SelectLeftTab(tab);
            Assert.Equal("Options", ui.ById("OpenOptions").Current.Name);
            Assert.Equal("Options: defaults", ui.ById("OpenOptions").Current.HelpText);
            ui.SetOption("AcquisitionSeed", "42");
            Assert.Contains("seed 42", ui.ById("OpenOptions").Current.HelpText);
            Assert.Equal("Options: seed 42", ui.Text("Options.Summary"));
            ui.OpenOptions();
            ui.SetText("AcquisitionSeed", "43", "Modal.Cancel");
            ui.CancelOptions();
            Assert.Equal("Options: seed 42", ui.Text("Options.Summary"));
            ui.Invoke("Window.Maximize");
            StudioWindow.WaitUntil(() => StudioWindow.Pattern<WindowPattern>(ui.Element, WindowPattern.Pattern)
                .Current.WindowVisualState == WindowVisualState.Maximized, TimeSpan.FromSeconds(5), "caption maximizes window");
            var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            Assert.True(GetMonitorInfo(MonitorFromWindow(new IntPtr(ui.Element.Current.NativeWindowHandle), 2), ref monitor));
            var maximized = ui.Element.Current.BoundingRectangle;
            Assert.InRange(Math.Abs(maximized.Left - monitor.Work.Left), 0, 1);
            Assert.InRange(Math.Abs(maximized.Top - monitor.Work.Top), 0, 1);
            Assert.InRange(Math.Abs(maximized.Right - monitor.Work.Right), 0, 1);
            Assert.InRange(Math.Abs(maximized.Bottom - monitor.Work.Bottom), 0, 1);
            Assert.Equal("Restore", ui.ById("Window.Maximize").Current.Name);
            record.Set("maximizedWorkArea", new { actual = maximized.ToString(), monitor.Work });
            ui.Invoke("Window.Maximize");
            StudioWindow.WaitUntil(() => StudioWindow.Pattern<WindowPattern>(ui.Element, WindowPattern.Pattern)
                .Current.WindowVisualState == WindowVisualState.Normal, TimeSpan.FromSeconds(5), "caption restores window");
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public int Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, int flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
