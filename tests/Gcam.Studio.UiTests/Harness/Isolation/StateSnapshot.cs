using System.ComponentModel;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>
/// MR8 before/after observation: a read-only listing of the declared file roots and all of HKCU, streamed as gzip JSON
/// lines into the run folder (local only; it names machine paths). Files carry size, last-write and attributes;
/// directories their existence and last-write; keys their last-write; values their type and the SHA-256 of the raw
/// bytes. An entry that cannot be read is recorded as unreadable, never as an error of the snapshot.
/// Version 2 (TODO-52): the run's harness markers (run IDs) are known at snapshot time, so a value whose raw data
/// carries one is flagged (<c>m</c>) — the hash alone cannot show it afterwards. Files are matched by name only, by the
/// classifier; their content is never read.
/// </summary>
public static class StateSnapshot
{
    public const string Schema = "gcam.state-snapshot";
    public const int SchemaVersion = 2;
    // Registry keys nest at most 512 levels; the guard stops a link loop from walking forever.
    private const int MaxKeyDepth = 512;
    public const int MarkerMinLength = 8;

    /// <summary>A file scope root and the category that stands for it in redacted output.</summary>
    public sealed record Root(string Category, string Path);

    public sealed record Counts(string Label, string File, DateTimeOffset StartedAt, DateTimeOffset FinishedAt,
        int Files, int Directories, int Keys, int Values, int Unreadable, string Sha256);

    /// <summary>The charter's file scope; Temp, VirtualStore, the sandbox and the run folder lie inside a walked root.</summary>
    public static IReadOnlyList<Root> ObservedRoots(string deployment, string sandbox, string runDirectory)
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return
        [
            new("APPDATA", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            new("LOCALAPPDATA", local),
            new("VirtualStore", Path.Combine(local, "VirtualStore")),
            new("TEMP", Path.GetTempPath()),
            new("LocalLow", Path.Combine(profile, "AppData", "LocalLow")),
            new("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            new("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
            new("ProgramData", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
            new("Deployment", deployment),
            new("Sandbox", sandbox),
            new("RunDirectory", runDirectory),
        ];
    }

    /// <summary>True when raw registry value data carries one of the markers, case-insensitively, as UTF-16LE text (at
    /// either byte alignment, as shell item lists embed it) or as single-byte text (ASCII / UTF-8 run IDs).</summary>
    public static bool CarriesMarker(byte[] data, IReadOnlyList<string> markers)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(markers);
        if (data.Length == 0 || markers.Count == 0) return false;
        string[] views =
        [
            Encoding.Unicode.GetString(data, 0, data.Length & ~1),
            data.Length > 2 ? Encoding.Unicode.GetString(data, 1, (data.Length - 1) & ~1) : "",
            Encoding.Latin1.GetString(data),
        ];
        return markers.Any(marker => views.Any(view => view.Contains(marker, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Writes one snapshot to a new file (never overwrites) and returns its counts and hash. <paramref name="markers"/>:
    /// the run's harness markers (run IDs); a value whose data carries one is flagged.</summary>
    public static Counts Take(string label, string output, IReadOnlyList<Root> roots, IRegistrySource registry, IReadOnlyList<string>? markers = null)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(registry);
        // A marker must be specific enough not to occur by chance (stage0.py uses the same minimum).
        string[] marks = (markers ?? []).Where(m => !string.IsNullOrWhiteSpace(m) && m.Length >= MarkerMinLength)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var started = DateTimeOffset.UtcNow;
        var full = roots.Where(r => !string.IsNullOrWhiteSpace(r.Path))
            .Select(r => r with { Path = Path.GetFullPath(r.Path).TrimEnd(Path.DirectorySeparatorChar) }).ToArray();
        // A root inside another root is listed once, by the outer walk; its category still names it in redaction.
        var walked = new List<Root>();
        foreach (var root in full.OrderBy(r => r.Path.Length))
            if (!walked.Any(outer => Inside(root.Path, outer.Path))) walked.Add(root);
        int files = 0, directories = 0, keys = 0, values = 0, unreadable = 0;
        using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var gzip = new GZipStream(stream, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)) { NewLine = "\n" })
        {
            void Line(object value) => writer.WriteLine(JsonSerializer.Serialize(value));
            Line(new { c = "header", schema = Schema, schemaVersion = SchemaVersion, label, startedAt = started.ToString("O"),
                roots = full.Select(r => new { category = r.Category, path = r.Path, exists = Directory.Exists(r.Path), walked = walked.Contains(r) }),
                registryRoot = "HKCU",
                valueMarkers = new { markers = marks, encodings = new[] { "utf-16le", "single-byte" }, caseInsensitive = true } });
            foreach (var root in walked.Where(r => Directory.Exists(r.Path)))
            {
                var pending = new Stack<string>([root.Path]);
                while (pending.Count != 0)
                {
                    string directory = pending.Pop();
                    try
                    {
                        var options = new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = false, AttributesToSkip = 0 };
                        foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", options))
                        {
                            long written = entry.LastWriteTimeUtc.ToFileTimeUtc();
                            if (entry is DirectoryInfo)
                            {
                                Line(new { c = "d", p = entry.FullName, t = written, a = (int)entry.Attributes });
                                directories++;
                                // A junction or link is recorded but not followed (legacy profile junctions point back into it).
                                if (!entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) pending.Push(entry.FullName);
                            }
                            else
                            {
                                Line(new { c = "f", p = entry.FullName, s = ((FileInfo)entry).Length, t = written, a = (int)entry.Attributes });
                                files++;
                            }
                        }
                    }
                    catch (Exception e) when (e is UnauthorizedAccessException or IOException or SecurityException)
                    {
                        Line(new { c = "u", ch = "file", p = directory, e = e.GetType().Name });
                        unreadable++;
                    }
                }
            }
            var keysPending = new Stack<string>([""]);
            while (keysPending.Count != 0)
            {
                string path = keysPending.Pop();
                string shown = path.Length == 0 ? "HKCU" : "HKCU\\" + path;
                if (path.Count(ch => ch == '\\') >= MaxKeyDepth)
                {
                    Line(new { c = "u", ch = "registry", p = shown, e = "depth" });
                    unreadable++;
                    continue;
                }
                IRegistrySource.Key key;
                try { key = registry.Read(path); }
                catch (Exception e) when (e is UnauthorizedAccessException or IOException or SecurityException or Win32Exception)
                {
                    Line(new { c = "u", ch = "registry", p = shown, e = e.GetType().Name });
                    unreadable++;
                    continue;
                }
                Line(new { c = "k", p = shown, t = key.LastWriteUtc.ToFileTimeUtc() });
                keys++;
                foreach (var value in key.Values)
                {
                    if (CarriesMarker(value.Data, marks))
                        Line(new { c = "v", p = shown, n = value.Name, k = value.Kind, h = IsolationPaths.Hash(value.Data), m = 1 });
                    else
                        Line(new { c = "v", p = shown, n = value.Name, k = value.Kind, h = IsolationPaths.Hash(value.Data) });
                    values++;
                }
                foreach (string sub in key.SubKeys) keysPending.Push(path.Length == 0 ? sub : path + "\\" + sub);
            }
            Line(new { c = "end", files, directories, keys, values, unreadable, finishedAt = DateTimeOffset.UtcNow.ToString("O") });
        }
        var finished = DateTimeOffset.UtcNow;
        using var snapshotFile = File.OpenRead(output);
        return new(label, Path.GetFileName(output), started, finished, files, directories, keys, values, unreadable,
            Convert.ToHexStringLower(SHA256.HashData(snapshotFile)));
    }

    private static bool Inside(string path, string root) =>
        path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
