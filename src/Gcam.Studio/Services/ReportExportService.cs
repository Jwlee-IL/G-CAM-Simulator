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
        var dialog = new SaveFileDialog
        {
            Title = "Export report", FileName = defaultName, DefaultExt = ".html", AddExtension = true,
            Filter = "HTML report (*.html)|*.html", OverwritePrompt = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true) return null;
        string scene = ReportFileWriter.ScenePath(dialog.FileName);
        if (File.Exists(scene) && MessageBox.Show(Application.Current.MainWindow,
            $"The report also writes {scene}. Replace the existing scene file?", "Export report",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return null;
        return dialog.FileName;
    }

    public Task WriteAsync(string path, ReportFiles files) => ReportFileWriter.WriteAsync(path, files);

    public void ShowError(string message) => MessageBox.Show(Application.Current.MainWindow,
        message, "Report export failed", MessageBoxButton.OK, MessageBoxImage.Error);
}
