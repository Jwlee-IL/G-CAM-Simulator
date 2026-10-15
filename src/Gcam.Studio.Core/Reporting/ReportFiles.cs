namespace Gcam.Studio.Core.Reporting;

/// <summary>Fully prepared report bytes and scene bytes, with no file-system dependencies.</summary>
public sealed record ReportFiles(byte[] Html, byte[] SceneJson)
{
    public byte[] Content => Html;
    public bool HasScene { get; init; } = true;
}
