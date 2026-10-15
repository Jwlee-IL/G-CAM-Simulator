using System.Text.Json;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>MD7: whole files replaced separately, index last; the next publication removes stale mismatching entries.</summary>
public static class VideoPublisher
{
    public sealed record Index(string Schema, int SchemaVersion, Dictionary<string, VideoEntry> Tests);
    public static Index Read(string path)
    {
        if (!File.Exists(path)) return new("gcam.ui-videos", 1, []);
        // Enumerate first: deserializing a dictionary alone would silently accept duplicate case ids.
        using var json = JsonDocument.Parse(File.ReadAllBytes(path));
        var keys = json.RootElement.GetProperty("tests").EnumerateObject().Select(p => p.Name).ToArray();
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length) throw new InvalidDataException("Duplicate video case id");
        var index = JsonSerializer.Deserialize<Index>(json, VideoEntry.Json)!;
        if (index.Schema != "gcam.ui-videos" || index.SchemaVersion != 1) throw new InvalidDataException("Unknown video index");
        foreach (var (key, entry) in index.Tests)
        {
            if (key != Path.GetFileName(key) || key.Contains('\\') || key.Contains('/') || key is "" or "." or "..")
                throw new InvalidDataException("Invalid video case id");
            entry.Validate();
        }
        return index;
    }
    public static bool Matches(string root, VideoEntry entry) => new[] { entry.Video, entry.Captions }.All(f =>
        File.Exists(Path.Combine(root, f.Path)) && new FileInfo(Path.Combine(root, f.Path)).Length == f.Bytes &&
        IsolationPaths.Hash(File.ReadAllBytes(Path.Combine(root, f.Path))) == f.Sha256);

    public static void Publish(string root, string id, VideoEntry entry, string mp4, string vtt, Action<string>? replaced = null)
    {
        entry.Validate();
        if (entry.Video.Path != id + ".mp4" || entry.Captions.Path != id + ".vtt") throw new InvalidDataException("Case/file mismatch");
        if (IsolationPaths.Hash(File.ReadAllBytes(mp4)) != entry.Video.Sha256 || IsolationPaths.Hash(File.ReadAllBytes(vtt)) != entry.Captions.Sha256)
            throw new InvalidDataException("Attempt hash mismatch");
        Directory.CreateDirectory(root);
        string indexPath = Path.Combine(root, "index.json");
        var index = Read(indexPath);
        foreach (string stale in index.Tests.Where(p => !Matches(root, p.Value)).Select(p => p.Key).ToArray()) index.Tests.Remove(stale);
        ReplaceCopy(mp4, Path.Combine(root, entry.Video.Path)); replaced?.Invoke("mp4");
        ReplaceCopy(vtt, Path.Combine(root, entry.Captions.Path)); replaced?.Invoke("vtt");
        index.Tests[id] = entry;
        string staged = indexPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(staged, JsonSerializer.SerializeToUtf8Bytes(index, VideoEntry.Json));
        File.Move(staged, indexPath, overwrite: true); replaced?.Invoke("index");
    }
    private static void ReplaceCopy(string source, string destination)
    {
        string staged = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.Copy(source, staged); // destination-volume staging: rename is never cross-volume
        File.Move(staged, destination, overwrite: true);
    }
}
