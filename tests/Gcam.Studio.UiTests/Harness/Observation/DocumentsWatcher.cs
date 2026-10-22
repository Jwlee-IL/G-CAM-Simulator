using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace Gcam.Studio.UiTests.Harness.Observation;

/// <summary>Narrow during-run default-export observation, using the declaration rather than a duplicated pattern.</summary>
/// <remarks>Reparse-point subfolders (the hidden legacy junctions of a real Documents folder, links, cloud placeholders)
/// are never followed: each is reported as a named blind spot, and only a root that cannot be watched, a notification
/// overflow or another watcher error fails coverage. Notifications are queued by the watcher callback and classified on
/// a separate thread, so the callback returns at once and the 64 KiB notification buffer (the API maximum) drains.</remarks>
public sealed class DocumentsWatcher : IDisposable
{
    /// <summary>FileSystemWatcher refuses a larger buffer; the system limit for one ReadDirectoryChangesW buffer.</summary>
    public const int NotificationBufferBytes = 64 * 1024;
    private readonly object _gate = new();
    private readonly string _root, _pattern;
    private readonly Regex _match;
    private readonly List<DocumentsWatchResult.Change> _events = [];
    private readonly List<string> _errors = [];
    private readonly List<DocumentsWatchResult.BlindSpot> _blind = [];
    private readonly HashSet<string> _controls = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly BlockingCollection<(string Operation, string Path, string? Old)> _queue = new();
    private readonly Thread? _worker;
    private FileSystemWatcher? _watcher;
    private bool _covers, _control;

    public DocumentsWatcher(string root, string surfaceListPath)
    {
        _root = Path.GetFullPath(root);
        using var json = JsonDocument.Parse(File.ReadAllBytes(surfaceListPath));
        _pattern = json.RootElement.GetProperty("surfaces").EnumerateArray()
            .SelectMany(s => s.TryGetProperty("scope", out var scope) ? scope.EnumerateArray().ToArray() : [])
            .Single(s => s.TryGetProperty("root", out var r) && r.GetString() == "Documents")
            .GetProperty("relativePattern").GetString()!;
        _match = new("\\A(?:" + _pattern + ")\\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        try
        {
            _root = ResolveRoot(_root);
            foreach (string file in Inventory(_root, _blind)) _known.Add(file);
            _watcher = new(_root) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite | NotifyFilters.Size, InternalBufferSize = NotificationBufferBytes };
            _watcher.Created += (_, e) => Enqueue("create", e.FullPath, null);
            _watcher.Changed += (_, e) => Enqueue("change", e.FullPath, null);
            _watcher.Deleted += (_, e) => Enqueue("delete", e.FullPath, null);
            _watcher.Renamed += (_, e) => Enqueue("rename", e.FullPath, e.OldFullPath);
            _watcher.Error += (_, e) => ReportError(e.GetException());
            _worker = new(Classify) { IsBackground = true, Name = "GCAM Documents watcher" }; _worker.Start();
            _covers = true; _watcher.EnableRaisingEvents = true;
        }
        catch (Exception e) { _watcher?.Dispose(); _watcher = null; ReportError(e); }
    }
    private void Enqueue(string operation, string path, string? old)
    {
        try { _queue.Add((operation, path, old)); } catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException) { }
    }
    private bool Matches(string path) => _match.IsMatch(Path.GetRelativePath(_root, path).Replace('/', '\\'));
    /// <summary>The final path of a directory through its handle, without walking or surveying its ancestors. Also the
    /// normaliser callers use to compare their own paths with the recorded ones (8.3 and long forms).</summary>
    public static string ResolveRoot(string root)
    {
        using var handle = CreateFile(Path.GetFullPath(root), 0x80, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var name = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandle(handle, name, (uint)name.Capacity, 0);
        if (length == 0 || length >= name.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
        string result = name.ToString();
        return result.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ? @"\\" + result[8..]
            : result.StartsWith(@"\\?\", StringComparison.Ordinal) ? result[4..] : result;
    }
    /// <summary>Matching files below <paramref name="folder"/>; reparse-point and unreadable subfolders become blind spots.</summary>
    private List<string> Inventory(string folder, List<DocumentsWatchResult.BlindSpot> blind)
    {
        var files = new List<string>(); var pending = new Queue<string>(); pending.Enqueue(folder);
        while (pending.TryDequeue(out string? current))
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(current)) if (Matches(file)) files.Add(file);
                foreach (string child in Directory.EnumerateDirectories(current))
                    if (Blind(child) is { } spot) blind.Add(spot); else pending.Enqueue(child);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException)
            {
                if (current == _root) throw; // The root itself must be readable.
                blind.Add(new(current, "unreadable", e.GetType().Name + ": " + e.Message));
            }
        }
        return files;
    }
    private static DocumentsWatchResult.BlindSpot? Blind(string directory)
    {
        var info = new DirectoryInfo(directory);
        if ((info.Attributes & FileAttributes.ReparsePoint) == 0) return null;
        // A link's target is outside the notification subtree; other reparse folders (e.g. cloud placeholders) are inside
        // it, but listing them can make their provider fetch content, so they are not entered either.
        string? target = null;
        try { target = info.LinkTarget; } catch (IOException) { }
        return target is not null ? new(info.FullName, "link", "not followed; target " + target)
            : new(info.FullName, "reparse-point", "not entered; changes inside are still reported by the watcher");
    }
    private void Classify()
    {
        foreach (var (operation, path, old) in _queue.GetConsumingEnumerable())
        {
            // Directory facts are read outside the gate. A changed directory only reports that its contents changed:
            // those changes arrive as their own notifications, so nothing is rescanned for it.
            DocumentsWatchResult.BlindSpot? spot = null; List<string>? arrived = null; var blind = new List<DocumentsWatchResult.BlindSpot>();
            if (operation is "create" or "rename")
                try
                {
                    if (Directory.Exists(path))
                    {
                        spot = Blind(path);
                        // A directory created or moved in may bring files without one notification per child.
                        if (spot is null) arrived = Inventory(path, blind);
                    }
                }
                catch (Exception e) { blind.Add(new(path, "unreadable", e.GetType().Name + ": " + e.Message)); }
            lock (_gate)
            {
                if (spot is not null) _blind.Add(spot);
                _blind.AddRange(blind);
                void Append(string target, string? previous)
                {
                    _events.Add(new(DateTimeOffset.UtcNow, operation, target, previous,
                        _controls.Contains(target) || previous is not null && _controls.Contains(previous)));
                    if (previous is not null) _known.Remove(previous);
                    if (operation == "delete") _known.Remove(target); else if (Matches(target)) _known.Add(target);
                }
                if (Matches(path) || old is not null && Matches(old)) Append(path, old);
                // A directory rename/delete need not emit one notification per child. Preserve the affected names too.
                string prefix = (old ?? path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (operation is "rename" or "delete")
                    foreach (string file in _known.Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray())
                        Append(old is null ? file : Path.Combine(path, Path.GetRelativePath(old, file)), old is null ? null : file);
                foreach (string file in arrived ?? [])
                    if (!_known.Contains(file)) Append(file, old is null ? null : Path.Combine(old, Path.GetRelativePath(path, file)));
                Monitor.PulseAll(_gate);
            }
        }
    }
    /// <summary>Also a test seam for overflow; a failed notification channel cannot qualify.</summary>
    public void ReportError(Exception error)
    {
        lock (_gate) { _errors.Add(error.GetType().Name + ": " + error.Message); _covers = false; }
    }
    /// <summary>The harness supplies a fresh path matching the loaded declaration. Existing files are never replaced.</summary>
    public bool PositiveControl(string relativePath, TimeSpan? timeout = null)
    {
        string path = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!path.StartsWith(_root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !Matches(path)) throw new ArgumentException("Control must be inside the root and match its declaration");
        lock (_gate)
        {
            if (!_covers) { _errors.Add("positive-control-root-not-covered"); return false; }
            _controls.Add(path);
        }
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite)) file.WriteByte(1);
            var until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(3));
            lock (_gate)
            {
                while (!_events.Any(e => e.Path == path && e.Operation == "create") && DateTime.UtcNow < until)
                    Monitor.Wait(_gate, TimeSpan.FromMilliseconds(25));
                _control = _events.Any(e => e.Path == path && e.Operation == "create");
                if (!_control) _errors.Add("positive-control-not-observed");
                return _control;
            }
        }
        catch (Exception e) { ReportError(e); return false; }
    }
    public DocumentsWatchResult Snapshot()
    {
        lock (_gate) return new(_root, _pattern, _covers, _control, _events.ToArray(), _errors.ToArray()) { BlindSpots = _blind.ToArray() };
    }
    public void Dispose()
    {
        _watcher?.Dispose(); _queue.CompleteAdding(); _worker?.Join(TimeSpan.FromSeconds(5)); _queue.Dispose();
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string name,
        uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,
        StringBuilder path, uint length, uint flags);
}
