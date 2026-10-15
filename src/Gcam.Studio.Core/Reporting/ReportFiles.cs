namespace Gcam.Studio.Core.Reporting;

/// <summary>Fully prepared export bytes and their declared sidecars, with no file-system dependencies. Every format writes
/// the scene record beside the chosen file; the event-data CSV also writes its metadata file.</summary>
public sealed record ReportFiles(byte[] Html, byte[] SceneJson)
{
    public byte[] Content => Html;

    /// <summary><c>&lt;name&gt;.meta.json</c> beside an event-data CSV; null for the report documents.</summary>
    public byte[]? MetaJson { get; init; }

    public static string ScenePath(string path) => Path.ChangeExtension(path, ".scene.json");
    public static string MetaPath(string path) => Path.ChangeExtension(path, ".meta.json");

    /// <summary>The sidecar paths an export to <paramref name="path"/> declares: the scene record, plus the metadata
    /// file for event data.</summary>
    public static IReadOnlyList<string> SidecarPaths(string path, bool withMeta)
        => withMeta ? [ScenePath(path), MetaPath(path)] : [ScenePath(path)];

    /// <summary>Every declared destination in write order: the chosen path, the scene record, then the metadata file.</summary>
    public IReadOnlyList<(string Path, byte[] Bytes)> Destinations(string path)
        => MetaJson is { } meta ? [(path, Html), (ScenePath(path), SceneJson), (MetaPath(path), meta)]
            : [(path, Html), (ScenePath(path), SceneJson)];
}
