using System.IO;
using System.Runtime.ExceptionServices;
using Gcam.Studio.Core.Reporting;

namespace Gcam.Studio.Services;

/// <summary>Writes only the chosen file and its declared sidecars; rolls all of them back on a failed write.</summary>
public static class ReportFileWriter
{
    public static string ScenePath(string path) => ReportFiles.ScenePath(path);
    public static string MetaPath(string path) => ReportFiles.MetaPath(path);

    public static async Task WriteAsync(string path, ReportFiles files)
    {
        var targets = files.Destinations(path);
        // Keep existing bytes in memory, so a failed overwrite can restore them without extra write surfaces.
        var previous = new byte[]?[targets.Count];
        for (int i = 0; i < targets.Count; i++)
            previous[i] = File.Exists(targets[i].Path) ? await File.ReadAllBytesAsync(targets[i].Path) : null;
        var opened = new List<FileStream>(targets.Count);
        Exception? failure = null;
        try
        {
            foreach (var target in targets)
                opened.Add(new FileStream(target.Path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None));
            // Open every destination before truncating any.
            foreach (var stream in opened) stream.SetLength(0);
            for (int i = 0; i < opened.Count; i++) await opened[i].WriteAsync(targets[i].Bytes);
            foreach (var stream in opened) await stream.FlushAsync();
        }
        catch (Exception error) { failure = error; }
        // Close every opened destination before any rollback touches it.
        foreach (var stream in opened)
        {
            try { await stream.DisposeAsync(); }
            catch (Exception error) { failure ??= error; }
        }
        if (failure is null) return;
        var errors = new List<Exception> { failure };
        for (int i = 0; i < opened.Count; i++) await RestoreAsync(targets[i].Path, previous[i], errors);
        if (errors.Count > 1) throw new AggregateException("Export failed and rollback failed; check the export destinations.", errors);
        ExceptionDispatchInfo.Capture(failure).Throw();
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
