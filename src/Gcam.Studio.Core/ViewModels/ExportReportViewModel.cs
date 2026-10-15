using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Studio.Core.Reporting;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>A transient format choice over one frozen report. Every format is available whenever a snapshot exists;
/// no preferences are persisted.</summary>
public sealed partial class ExportReportViewModel : ObservableObject
{
    private readonly Func<ReportFormat, Task> _export;
    private readonly Action _close;
    public ExportReportViewModel(Func<ReportFormat, Task> export, Action close)
    { _export = export; _close = close; }
    public string Title => "Export report";
    [ObservableProperty] private ReportFormat _format = ReportFormat.Html;
    [RelayCommand] private void Cancel() => _close();
    [RelayCommand]
    private async Task ExportAsync() { _close(); await _export(Format); }
}
