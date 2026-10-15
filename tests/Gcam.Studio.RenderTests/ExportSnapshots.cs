using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Gcam.Configuration;
using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Services;

namespace Gcam.Studio.RenderTests;

public sealed partial class PlotViewRenderTests
{
    [RenderSnapshotFact]
    public void Export_TitleBarAndPopup_BothThemes_RenderWithoutWindow() => host.Run(() =>
    {
        foreach (string theme in new[] { "Dark", "Light" })
        {
            UseTheme(theme);
            var modal = new ModalService();
            var model = new MainViewModel(new FixtureAcquisition(), new FixtureTheme(Enum.Parse<AppTheme>(theme)),
                new FixtureSpectrum(), confirmation: modal, reportExport: new ReportExportService());
            model.Snapshot = new(1, 10, 10, 1, false,
                new(new Gcam.Core.DetectorImage(2, 2), 0, 1, null, 0, 0, null, 10, TimeSpan.Zero), [], TimeSpan.Zero, true)
            { ConfigurationJson = ConfigLoader.Serialize(new SimulationConfig()), Optics = new(), Detector = new() };
            var size = new Size(1280, 800);
            CaptureStage2(model, theme, size, "export-titlebar");
            model.ExportReportCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.IsType<ExportReportViewModel>(modal.ActiveContent);
            CaptureStage2(model, theme, size, "export-popup");
            var (root, content) = DetachMainWindow(model, size);
            root.UpdateLayout();
            var view = Descendants(root).OfType<Gcam.Studio.Views.ExportReportView>().Single();
            var json = Descendants(view).OfType<RadioButton>().Single(b => AutomationProperties.GetAutomationId(b) == "Export.Json");
            Assert.False(json.IsEnabled);
            var icon = Descendants(root).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "ExportReport");
            Assert.IsAssignableFrom<Geometry>(icon.Content);
            Assert.Equal("Alt+E", AutomationProperties.GetAccessKey(icon));
            Assert.Equal("Export report…", AutomationProperties.GetName(icon));
            model.ExportPopup!.CancelCommand.Execute(null);
            Assert.Null(modal.ActiveContent);
            content.DataContext = null;
        }
    });
}
