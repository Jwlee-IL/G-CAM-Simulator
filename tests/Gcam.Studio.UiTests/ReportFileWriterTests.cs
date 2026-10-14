using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Services;

namespace Gcam.Studio.UiTests;

/// <summary>Headless I/O checks of the view-layer writer; never creates a window or calls a dialog.</summary>
public sealed class ReportFileWriterTests
{
    [Fact]
    public async Task Write_CreatesOnlyReportAndScene_WithExactBytes()
    {
        string folder = Folder();
        string path = Path.Combine(folder, "report.html");
        var files = new ReportFiles([1, 2, 3], [4, 5]);
        await ReportFileWriter.WriteAsync(path, files);
        Assert.Equal(files.Html, await File.ReadAllBytesAsync(path));
        Assert.Equal(files.SceneJson, await File.ReadAllBytesAsync(ReportFileWriter.ScenePath(path)));
        Assert.Equal(new[] { "report.html", "report.scene.json" }, Directory.GetFiles(folder).Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task Write_FailedSidecarOpen_RemovesNewReport()
    {
        string path = Path.Combine(Folder(), "report.html");
        Directory.CreateDirectory(ReportFileWriter.ScenePath(path)); // a directory cannot be opened as a file
        var error = await Record.ExceptionAsync(() => ReportFileWriter.WriteAsync(path, new([1], [2])));
        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString() ?? "Expected a failed write");
        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public async Task Write_FailedSidecarOpen_KeepsExistingReportAndScene()
    {
        string path = Path.Combine(Folder(), "report.html");
        string scene = ReportFileWriter.ScenePath(path);
        await File.WriteAllTextAsync(path, "previous HTML");
        await File.WriteAllTextAsync(scene, "previous scene");
        // Readable for the backup, but not writable for export; no permission or machine-setting changes.
        using var held = new FileStream(scene, FileMode.Open, FileAccess.Read, FileShare.Read);
        await Assert.ThrowsAnyAsync<IOException>(() => ReportFileWriter.WriteAsync(path, new([1], [2])));
        Assert.Equal("previous HTML", await File.ReadAllTextAsync(path));
        Assert.Equal("previous scene", await File.ReadAllTextAsync(scene));
    }

    private static string Folder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "gcam-todo46", "impl1", "writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder; // retained for audit; no cleanup commands
    }
}
