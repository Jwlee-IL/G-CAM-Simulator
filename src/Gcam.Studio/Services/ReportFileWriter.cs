using System.IO;
using Gcam.Studio.Core.Reporting;

namespace Gcam.Studio.Services;

/// <summary>Writes only the chosen report and its scene sidecar; rolls back both on a failed write.</summary>
public static class ReportFileWriter
{
    public static string ScenePath(string htmlPath) => Path.ChangeExtension(htmlPath, ".scene.json");

    public static async Task WriteAsync(string path, ReportFiles files)
    {
        string scenePath = ScenePath(path);
        // Keep existing bytes in memory, so a failed overwrite can restore them without extra write surfaces.
        byte[]? oldHtml = File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
        byte[]? oldScene = File.Exists(scenePath) ? await File.ReadAllBytesAsync(scenePath) : null;
        bool openedHtml = false, openedScene = false;
        try
        {
            await using var html = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            openedHtml = true;
            await using var scene = new FileStream(scenePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            openedScene = true;
            // Open both destinations before truncating either. Scope disposes both before the rollback below.
            html.SetLength(0);
            scene.SetLength(0);
            await html.WriteAsync(files.Html);
            await scene.WriteAsync(files.SceneJson);
            await html.FlushAsync();
            await scene.FlushAsync();
        }
        catch (Exception writeError)
        {
            var errors = new List<Exception> { writeError };
            if (openedHtml) await RestoreAsync(path, oldHtml, errors);
            if (openedScene) await RestoreAsync(scenePath, oldScene, errors);
            if (errors.Count > 1) throw new AggregateException("Export failed and rollback failed; check the report and scene destinations.", errors);
            throw;
        }
    }

    private static async Task RestoreAsync(string path, byte[]? previous, List<Exception> errors)
    {
        try
        {
            if (previous is null) File.Delete(path);
            else await File.WriteAllBytesAsync(path, previous);
        }
        catch (Exception error) { errors.Add(error); }
    }
}
