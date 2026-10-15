using Gcam.Studio.Core.Reporting;

namespace Gcam.Studio.Core.Services;

/// <summary>The view layer renders platform formats, chooses a destination, writes declared files and shows failures.</summary>
public interface IReportExportService
{
    string Version { get; }
    string? ChoosePath(string defaultName);
    ReportFiles Render(SimulationReport report, ReportFormat format) => format switch
    {
        ReportFormat.Html => ReportWriter.Write(report),
        ReportFormat.Json => AdcJsonWriter.Write(report),
        _ => throw new NotSupportedException("This renderer does not support the selected format.")
    };
    Task WriteAsync(string path, ReportFiles files);
    void ShowError(string message);
}
