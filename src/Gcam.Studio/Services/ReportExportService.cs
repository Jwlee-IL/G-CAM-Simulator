using System.IO;
using System.Reflection;
using System.Windows;
using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.Services;
using Microsoft.Win32;

namespace Gcam.Studio.Services;

/// <summary>Studio's explicit report write surface; no folder or setting is remembered.</summary>
public sealed class ReportExportService : IReportExportService
{
    public string Version => typeof(ReportExportService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    public string? ChoosePath(string defaultName)
    {
        string extension = Path.GetExtension(defaultName);
        var dialog = new SaveFileDialog
        {
            Title = "Export report", FileName = defaultName, DefaultExt = extension, AddExtension = true,
            Filter = Filter(extension), OverwritePrompt = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true) return null;
        // The dialog prompts for the chosen file; Studio asks once for the declared sidecars it would replace.
        var existing = ReportFiles.SidecarPaths(dialog.FileName, withMeta: extension == ".csv").Where(File.Exists).ToArray();
        if (existing.Length > 0 && MessageBox.Show(Application.Current.MainWindow,
            existing.Length == 1
                ? $"The export also writes {existing[0]}. Replace the existing file?"
                : $"The export also writes {string.Join(" and ", existing)}. Replace the existing files?", "Export report",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return null;
        return dialog.FileName;
    }

    /// <summary>The save dialog's filter for one format's extension.</summary>
    public static string Filter(string extension) => extension == ".csv"
        ? "CSV event data (*.csv)|*.csv"
        : $"{extension[1..].ToUpperInvariant()} report (*{extension})|*{extension}";

    public Task WriteAsync(string path, ReportFiles files) => ReportFileWriter.WriteAsync(path, files);

    public ReportFiles Render(SimulationReport report, ReportFormat format, string path) => format switch
    {
        ReportFormat.Html => ReportWriter.Write(report),
        ReportFormat.Csv => EventCsvWriter.Write(report, Path.GetFileName(path)),
        ReportFormat.Pdf => new(PdfReportWriter.Write(report), System.Text.Encoding.UTF8.GetBytes(report.ConfigurationJson)),
        ReportFormat.Word => new(WordReportWriter.Write(report, ReportFigureRenderer.Png), System.Text.Encoding.UTF8.GetBytes(report.ConfigurationJson)),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public void ShowError(string message) => MessageBox.Show(Application.Current.MainWindow,
        message, "Report export failed", MessageBoxButton.OK, MessageBoxImage.Error);
}
