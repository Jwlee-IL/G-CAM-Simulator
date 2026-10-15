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
/// </summary>
public static class StateSnapshot
{
    public const string Schema = "gcam.state-snapshot";
    // Registry keys nest at most 512 levels; the guard stops a link loop from walking forever.
    private const int MaxKeyDepth = 512;

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

    /// <summary>Writes one snapshot to a new file (never overwrites) and returns its counts and hash.</summary>
    public static Counts Take(string label, string output, IReadOnlyList<Root> roots, IRegistrySource registry)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(registry);
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
            Line(new { c = "header", schema = Schema, schemaVersion = 1, label, startedAt = started.ToString("O"),
                roots = full.Select(r => new { category = r.Category, path = r.Path, exists = Directory.Exists(r.Path), walked = walked.Contains(r) }),
                registryRoot = "HKCU" });
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
