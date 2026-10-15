using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Studio.Core.Reporting;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>A transient format choice over one frozen report. No preferences are persisted.</summary>
public sealed partial class ExportReportViewModel : ObservableObject
{
    private readonly Func<ReportFormat, Task> _export;
    private readonly Action _close;
    public ExportReportViewModel(bool hasAdc, Func<ReportFormat, Task> export, Action close)
    { HasAdc = hasAdc; _export = export; _close = close; }
    public string Title => "Export report";
    public bool HasAdc { get; }
    public string JsonReason => HasAdc ? "Retained integer A–D codes, with the full configuration header."
        : "Available with the experimental physical readout, after four-channel ADC values have been acquired.";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private ReportFormat _format = ReportFormat.Html;
    private bool CanExport() => Format != ReportFormat.Json || HasAdc;
    partial void OnFormatChanged(ReportFormat value) { if (value == ReportFormat.Json && !HasAdc) Format = ReportFormat.Html; }
    [RelayCommand] private void Cancel() => _close();
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync() { if (!CanExport()) return; _close(); await _export(Format); }
}
