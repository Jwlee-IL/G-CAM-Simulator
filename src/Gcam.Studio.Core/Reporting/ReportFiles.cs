namespace Gcam.Studio.Core.Reporting;

/// <summary>Fully prepared UTF-8 file contents, with no file-system dependencies.</summary>
public sealed record ReportFiles(byte[] Html, byte[] SceneJson);
