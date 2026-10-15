using Gcam.Studio.Core.Reporting;

namespace Gcam.Studio.Core.Services;

/// <summary>The view layer renders platform formats, chooses a destination, writes declared files and shows failures.</summary>
public interface IReportExportService
{
    string Version { get; }
    string? ChoosePath(string defaultName);
    /// <summary>The export's bytes for the confirmed <paramref name="path"/>; the event data's metadata names its siblings.</summary>
    ReportFiles Render(SimulationReport report, ReportFormat format, string path) => format switch
    {
        ReportFormat.Html => ReportWriter.Write(report),
        ReportFormat.Csv => EventCsvWriter.Write(report, Path.GetFileName(path)),
        _ => throw new NotSupportedException("This renderer does not support the selected format.")
    };
    Task WriteAsync(string path, ReportFiles files);
    void ShowError(string message);
}
