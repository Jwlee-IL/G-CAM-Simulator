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
            Filter = $"{extension[1..].ToUpperInvariant()} report (*{extension})|*{extension}", OverwritePrompt = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true) return null;
        string scene = ReportFileWriter.ScenePath(dialog.FileName);
        if (extension != ".json" && File.Exists(scene) && MessageBox.Show(Application.Current.MainWindow,
            $"The report also writes {scene}. Replace the existing scene file?", "Export report",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return null;
        return dialog.FileName;
    }

    public Task WriteAsync(string path, ReportFiles files) => ReportFileWriter.WriteAsync(path, files);

    public ReportFiles Render(SimulationReport report, ReportFormat format) => format switch
    {
        ReportFormat.Html => ReportWriter.Write(report),
        ReportFormat.Json => AdcJsonWriter.Write(report),
        ReportFormat.Pdf => new(PdfReportWriter.Write(report), System.Text.Encoding.UTF8.GetBytes(report.ConfigurationJson)),
        ReportFormat.Word => new(WordReportWriter.Write(report, ReportFigureRenderer.Png), System.Text.Encoding.UTF8.GetBytes(report.ConfigurationJson)),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public void ShowError(string message) => MessageBox.Show(Application.Current.MainWindow,
        message, "Report export failed", MessageBoxButton.OK, MessageBoxImage.Error);
}
