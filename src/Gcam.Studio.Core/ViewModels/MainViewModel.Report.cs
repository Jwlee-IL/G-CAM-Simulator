using CommunityToolkit.Mvvm.Input;
using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

public sealed partial class MainViewModel
{
    private readonly IReportExportService? _reportExport;
    private readonly TimeProvider _reportClock;

    private bool CanExportReport() => Snapshot is not null;

    [RelayCommand(CanExecute = nameof(CanExportReport))]
    private async Task ExportReportAsync()
    {
        if (Snapshot is null || _reportExport is null) return;
        try
        {
            // Capture before the native dialog pumps messages: live acquisition may publish while it is open.
            var report = SimulationReport.Capture(this, _reportClock.GetLocalNow(), _reportExport.Version);
            var files = ReportWriter.Write(report);
            string? path = _reportExport.ChoosePath($"gcam-report-{report.ExportedAt:yyyyMMdd-HHmmss}.html");
            if (path is null) return;
            await _reportExport.WriteAsync(path, files);
        }
        catch (Exception ex) { _reportExport.ShowError($"Report export failed: {ex.Message}"); }
    }
}
