using Gcam.Studio.Core.Reporting;

namespace Gcam.Studio.Core.Services;

/// <summary>The view layer chooses a destination, writes the two prepared files, and shows failures.</summary>
public interface IReportExportService
{
    string Version { get; }
    string? ChoosePath(string defaultName);
    Task WriteAsync(string path, ReportFiles files);
    void ShowError(string message);
}
