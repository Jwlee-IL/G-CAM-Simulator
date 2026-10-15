using CommunityToolkit.Mvvm.Input;
using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

public sealed partial class MainViewModel
{
    private readonly IReportExportService? _reportExport;
    private readonly TimeProvider _reportClock;
    public ExportReportViewModel? ExportPopup { get; private set; }

    private bool CanExportReport() => Snapshot is not null;

    [RelayCommand(CanExecute = nameof(CanExportReport))]
    private Task ExportReportAsync()
    {
        if (Snapshot is null || _reportExport is null || Modal?.ActiveContent is not null) return Task.CompletedTask;
        SimulationReport report;
        try { report = SimulationReport.Capture(this, _reportClock.GetLocalNow(), _reportExport.Version); }
        catch (Exception ex) { _reportExport.ShowError($"Report export failed: {ex.Message}"); return Task.CompletedTask; }
        if (Modal is null) return SaveReportAsync(report, ReportFormat.Html); // UI-free callers without a modal host
        ExportPopup = new(report.AdcRecords.Count > 0, format => SaveReportAsync(report, format), () =>
        {
            if (ExportPopup is { } current) Modal.Dismiss(current);
            ExportPopup = null;
        });
        Modal.Show(ExportPopup, () => ExportPopup?.CancelCommand.Execute(null));
        return Task.CompletedTask;
    }

    private async Task SaveReportAsync(SimulationReport report, ReportFormat format)
    {
        if (_reportExport is null) return;
        try
        {
            var files = _reportExport.Render(report, format);
            string extension = format switch { ReportFormat.Pdf => "pdf", ReportFormat.Word => "docx", ReportFormat.Json => "json", _ => "html" };
            string? path = _reportExport.ChoosePath($"gcam-report-{report.ExportedAt:yyyyMMdd-HHmmss}.{extension}");
            if (path is null) return;
            await _reportExport.WriteAsync(path, files);
        }
        catch (Exception ex) { _reportExport.ShowError($"Report export failed: {ex.Message}"); }
    }
}
