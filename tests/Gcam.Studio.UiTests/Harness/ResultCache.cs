using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gcam.Studio.UiTests.Harness.Isolation;
using Microsoft.Win32;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>
/// Opt-in reuse of a desktop case's latest pass (TODO-51 step 2). A case's key joins what can change its outcome: the
/// Studio deployment fingerprint, the source hashes its class's catalog entry lists (<c>docs/VV.Tests.md</c>, hashed
/// live), the isolation folder, the environment and the opt-in switches. Every pass records its key in
/// <c>artifacts/ui-videos/cache.json</c> (with or without a video); a failure removes the case's entry. Only with
/// <c>GCAM_UI_CACHE=1</c> does a case whose key matches a pass younger than <see cref="Expiry"/> on the same OS build
/// end as an xUnit skip with a fixed reason (<see cref="ReusedTag"/>) — reported as reused, never as a pass.
/// </summary>
public static class ResultCache
{
    public const string Variable = "GCAM_UI_CACHE";
    public const string ReusedTag = "[cache-reused]";
    public const string Schema = "gcam.ui-cache";
    public static readonly TimeSpan Expiry = TimeSpan.FromDays(7);
    /// <summary>A pass stamped this far in the future (clock change) is not trusted.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>The opt-in switches that select or change what a desktop case does, normalised to "1" or "off".
    /// <c>GCAM_UI_VIDEO</c> is not one: recording never changes a verdict.</summary>
    public static readonly string[] Switches = [DesktopFactAttribute.OptInVariable, DesktopFactAttribute.LongOptInVariable,
        "GCAM_UI_PROBE", "GCAM_README_CAPTURE_ONLY"];

    /// <summary>Classes never reused nor recorded, with the reason.</summary>
    public static readonly IReadOnlyDictionary<Type, string> Exclusions = new Dictionary<Type, string>
    {
        [typeof(ObservedRunTests)] = "the observed run is the observation itself",
        [typeof(PolishSurveyTests)] = "it exists to rewrite the committed screenshots",
    };

    /// <summary>Components shown by value in a "changed" reason; the others are content hashes.</summary>
    private static readonly HashSet<string> Readable = new(StringComparer.Ordinal) { "os", "display", "culture", "switches" };

    public sealed record Inputs(string Deployment, string Isolation, string OsBuild, string Display, string Culture,
        IReadOnlyDictionary<string, string> SwitchValues, string RepoRoot, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalog);

    /// <summary>A case's key. <see cref="CatalogStale"/> names a listed source whose live hash differs from the
    /// catalog: the digest still follows the live bytes, but a stale catalog never allows reuse.</summary>
    public sealed record Key(string Digest, IReadOnlyDictionary<string, string> Components, string OsBuild, string? CatalogStale)
    {
        public string Short => Digest[..12];
    }

    public sealed record Entry(string RunId, DateTimeOffset PassedAtUtc, string Key, string OsBuild, Dictionary<string, string> Components);

    public sealed record Store(string Schema, int SchemaVersion, Dictionary<string, Entry> Cases);

    public sealed record Mode(bool CacheOn, bool BreakVerdict, bool Observed)
    {
        public static Mode Current() => new(Environment.GetEnvironmentVariable(Variable) == "1",
            Environment.GetEnvironmentVariable(PilotTests.BreakVerdictVariable) == "1",
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ManualObservation.DirectoryVariable)));
    }

    public sealed record Decision(bool Reuse, string Reason);

    public sealed record PlannedCase(Type TestClass, string CaseId, Decision Decision);

    /// <summary>The case being run (desktop runs are serial), so its run record can name itself as the source run.</summary>
    public sealed class Attempt(Type testClass, string caseId, Key? key)
    {
        public Type TestClass { get; } = testClass;
        public string CaseId { get; } = caseId;
        public Key? Key { get; } = key;
        public string? RunId { get; set; }
    }

    private static readonly Lazy<Inputs> _live = new(ReadLive);
    private static readonly ConcurrentDictionary<Type, Key?> _keys = new();
    private static Attempt? _current;

    public static Inputs Live => _live.Value;
    public static string StorePath => Path.Combine(VideoRecorder.LatestRoot, "cache.json");

    /// <summary>False only when a run must leave the store untouched (a deliberately broken verdict).</summary>
    public static bool RecordsPasses(Mode mode) => !mode.BreakVerdict;

    public static string CaseId(Type testClass, string method, object?[]? arguments) => VideoEntry.CaseId(testClass.Name, method,
        (arguments ?? []).Select(a => Convert.ToString(a, CultureInfo.InvariantCulture) ?? "null").ToArray());

    public static Key? KeyFor(Type testClass) => _keys.GetOrAdd(testClass, t => Compute(t, Live));

    /// <summary>The xUnit skip reason for a case, or null to run it. Off by default; any doubt runs the case.</summary>
    public static string? SkipReason(Type testClass, string method, object?[]? arguments)
    {
        var mode = Mode.Current();
        if (!mode.CacheOn) return null;
        try
        {
            var decision = Decide(testClass, CaseId(testClass, method, arguments), KeyFor(testClass), ReadOrEmpty(StorePath), DateTimeOffset.UtcNow, mode);
            return decision.Reuse ? decision.Reason : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static Attempt Begin(Type testClass, string method, object?[]? arguments)
    {
        Key? key = null;
        try { if (RecordsPasses(Mode.Current()) && !Exclusions.ContainsKey(testClass)) key = KeyFor(testClass); }
        catch (Exception) { }   // no key: the pass is not recorded, the verdict is unchanged
        var attempt = new Attempt(testClass, CaseId(testClass, method, arguments), key);
        _current = attempt;
        return attempt;
    }

    /// <summary>Called by each scenario's run record; the last one is the case's source run.</summary>
    public static void Ran(string runId)
    {
        if (_current is { } attempt) attempt.RunId = runId;
    }

    public static string End(Attempt attempt, bool passed)
    {
        _current = null;
        try { return Finish(StorePath, attempt.TestClass, attempt.CaseId, attempt.Key, attempt.RunId, passed, Mode.Current(), DateTimeOffset.UtcNow); }
        catch (Exception e) { return "result cache not updated: " + e.GetType().Name; }   // never changes the verdict
    }

    /// <summary>A pass records (or refreshes) the case's entry; a failure removes it, so an older pass never stands for
    /// a newer failure. A broken-verdict run and the excluded classes leave the store untouched.</summary>
    public static string Finish(string path, Type testClass, string caseId, Key? key, string? runId, bool passed, Mode mode, DateTimeOffset nowUtc)
    {
        if (Exclusions.TryGetValue(testClass, out string? why)) return "result cache not used: " + why;
        if (!RecordsPasses(mode)) return "result cache untouched: " + PilotTests.BreakVerdictVariable + "=1";
        var cases = ReadOrEmpty(path).Cases;
        if (!passed)
        {
            if (cases.Remove(caseId)) Save(path, cases);
            return "result cache: no entry for a failed case";
        }
        if (key is null || runId is null) return "result cache not updated: " + (key is null ? "no key (catalog entry missing?)" : "no run record");
        cases[caseId] = new(runId, nowUtc, key.Digest, key.OsBuild, new(key.Components, StringComparer.Ordinal));
        Save(path, cases);
        return $"result cache: recorded run {runId} (key {key.Short})";
    }

    public static Decision Decide(Type testClass, string caseId, Key? current, Store store, DateTimeOffset nowUtc, Mode mode)
    {
        if (!mode.CacheOn) return new(false, $"cache off ({Variable} is not 1)");
        if (Exclusions.TryGetValue(testClass, out string? why)) return new(false, "never reused: " + why);
        if (mode.BreakVerdict) return new(false, $"never reused: {PilotTests.BreakVerdictVariable}=1");
        if (mode.Observed) return new(false, $"never reused: observed batch ({ManualObservation.DirectoryVariable} is set)");
        if (current is null) return new(false, "no catalog entry for the class in docs/VV.Tests.md");
        if (current.CatalogStale is { } stale) return new(false, "catalog source hash stale: " + stale);
        if (!store.Cases.TryGetValue(caseId, out var entry)) return new(false, "no recorded pass");
        if (entry.OsBuild != current.OsBuild) return new(false, $"expired: OS build {entry.OsBuild} -> {current.OsBuild}");
        var age = nowUtc - entry.PassedAtUtc;
        if (age > Expiry || age < -ClockSkew) return new(false, $"expired: pass of {entry.PassedAtUtc:yyyy-MM-dd HH:mm} UTC is outside {Expiry.TotalDays:0} days");
        if (entry.Key != current.Digest) return new(false, "changed: " + string.Join(", ", Changed(entry.Components, current.Components)));
        return new(true, $"{ReusedTag} reused from run {entry.RunId} (key {current.Short})");
    }

    private static IEnumerable<string> Changed(IReadOnlyDictionary<string, string> stored, IReadOnlyDictionary<string, string> current)
    {
        var names = stored.Keys.Union(current.Keys).Order(StringComparer.Ordinal).ToArray();
        var changed = names.Where(n => stored.GetValueOrDefault(n) != current.GetValueOrDefault(n))
            .Select(n => Readable.Contains(n) ? $"{n} {stored.GetValueOrDefault(n) ?? "-"} -> {current.GetValueOrDefault(n) ?? "-"}" : n).ToArray();
        return changed.Length == 0 ? ["key"] : changed;
    }

    public static Key? Compute(Type testClass, Inputs inputs)
    {
        string unit = testClass.Assembly.GetName().Name + "::" + testClass.FullName;
        if (!inputs.Catalog.TryGetValue(unit, out var own) || own.Count == 0) return null;
        var listed = WithReferencedClasses(unit, own, inputs);
        var live = listed.Keys.Order(StringComparer.Ordinal).Select(p =>
        {
            string file = Path.Combine(inputs.RepoRoot, p);
            return (Path: p, Hash: File.Exists(file) ? SourceHash(inputs.RepoRoot, p) : "missing");
        }).ToArray();
        string? stale = live.Where(s => s.Hash != listed[s.Path]).Select(s => s.Path).FirstOrDefault();
        var components = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["deployment"] = inputs.Deployment,
            ["sources"] = Digest(live.Select(s => s.Path + ":" + s.Hash)),
            ["isolation"] = inputs.Isolation,
            ["os"] = inputs.OsBuild,
            ["display"] = inputs.Display,
            ["culture"] = inputs.Culture,
            ["switches"] = string.Join(";", inputs.SwitchValues.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value)),
        };
        return new(Digest(components.Select(c => c.Key + ":" + c.Value)), components, inputs.OsBuild, stale);
    }

    /// <summary>A class's catalog sources plus the own files of every other test class of its assembly that its own
    /// files name, transitively (a scenario class calls another's helpers: <c>ManualScenarioTests</c> →
    /// <c>WorkspaceScenarioTests</c> → <c>PilotTests</c>). Harness files are already listed for every class; their
    /// references to test classes (exclusion lists, a shared constant) are not followed.</summary>
    private static Dictionary<string, string> WithReferencedClasses(string unit, IReadOnlyDictionary<string, string> own, Inputs inputs)
    {
        string prefix = unit[..(unit.IndexOf("::", StringComparison.Ordinal) + 2)];
        var classes = inputs.Catalog.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .GroupBy(k => k[(k.LastIndexOf('.') + 1)..], StringComparer.Ordinal).Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
        var names = new Regex(@"\b(" + string.Join("|", classes.Keys.OrderByDescending(n => n.Length).Select(Regex.Escape)) + @")\b");
        var listed = new Dictionary<string, string>(own, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal) { unit };
        var pending = new Queue<string>([unit]);
        while (pending.TryDequeue(out string? current))
            foreach (string file in inputs.Catalog[current].Keys.Where(p => !IsHelper(p)))
            {
                string path = Path.Combine(inputs.RepoRoot, file);
                if (!File.Exists(path)) continue;
                foreach (Match name in names.Matches(File.ReadAllText(path)))
                {
                    string other = classes[name.Value];
                    if (!seen.Add(other)) continue;
                    pending.Enqueue(other);
                    foreach (var (otherFile, hash) in inputs.Catalog[other].Where(p => !IsHelper(p.Key))) listed[otherFile] = hash;
                }
            }
        return listed;
    }

    private static bool IsHelper(string relative)
    {
        string[] parts = relative.Split('/');
        return parts.Length > 3 && parts[2] == "Harness" || parts.Length == 3
            && (parts[2].EndsWith("FactAttribute.cs", StringComparison.Ordinal) || parts[2].EndsWith("TheoryAttribute.cs", StringComparison.Ordinal));
    }

    /// <summary>The catalog's source hash (test_records.py): the class's own files as text — a UTF-8 BOM dropped and
    /// line ends read as LF — and the project's helpers (<c>Harness/</c>, root <c>*FactAttribute.cs</c> /
    /// <c>*TheoryAttribute.cs</c>) as bytes with CRLF as LF.</summary>
    public static string SourceHash(string repoRoot, string relative)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(repoRoot, relative));
        bool helper = IsHelper(relative);
        int start = !helper && bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
        using var normalised = new MemoryStream(bytes.Length);
        for (int i = start; i < bytes.Length; i++)
        {
            if (bytes[i] == '\r' && i + 1 < bytes.Length && bytes[i + 1] == '\n') continue;
            normalised.WriteByte(bytes[i] == '\r' && !helper ? (byte)'\n' : bytes[i]);
        }
        return Convert.ToHexStringLower(SHA256.HashData(normalised.ToArray()));
    }

    /// <summary>Unit id → listed sources, from every fenced JSON block of the catalog.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadCatalog(string path)
    {
        string text = File.ReadAllText(path).Replace("\r\n", "\n");
        var units = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (Match block in Regex.Matches(text, "```json\n(.*?)\n```", RegexOptions.Singleline))
        {
            using var json = JsonDocument.Parse(block.Groups[1].Value);
            if (json.RootElement.TryGetProperty("id", out var id) && json.RootElement.TryGetProperty("sources", out var sources))
                units[id.GetString()!] = sources.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.Ordinal);
        }
        return units;
    }

    public static Store Read(string path)
    {
        if (!File.Exists(path)) return new(Schema, 1, []);
        using var json = JsonDocument.Parse(File.ReadAllBytes(path));
        // Enumerate first: deserializing a dictionary alone would silently accept duplicate case ids.
        var ids = json.RootElement.GetProperty("cases").EnumerateObject().Select(p => p.Name).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new InvalidDataException("Duplicate result cache case id");
        var store = json.Deserialize<Store>(VideoEntry.Json) ?? throw new InvalidDataException("Empty result cache");
        if (store.Schema != Schema || store.SchemaVersion != 1 || store.Cases is null) throw new InvalidDataException("Unknown result cache");
        foreach (var (id, entry) in store.Cases)
            if (!Regex.IsMatch(id, "^[A-Za-z0-9._-]{1,160}$") || entry is null || !Regex.IsMatch(entry.RunId ?? "", "^[0-9]{8}-[0-9]{6}-[0-9a-f]{6}$")
                || !Regex.IsMatch(entry.Key ?? "", "^[0-9a-f]{64}$") || string.IsNullOrWhiteSpace(entry.OsBuild) || entry.Components is null)
                throw new InvalidDataException("Invalid result cache entry");
        return store;
    }

    /// <summary>An unreadable store reuses nothing; the next pass replaces it.</summary>
    public static Store ReadOrEmpty(string path)
    {
        try { return Read(path); }
        catch (Exception e) when (e is InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new(Schema, 1, []);
        }
    }

    /// <summary>Same-folder staging copy, then a replace (as the video index).</summary>
    private static void Save(string path, Dictionary<string, Entry> cases)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var store = new Store(Schema, 1, cases.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        string staged = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(staged, JsonSerializer.SerializeToUtf8Bytes(store, VideoEntry.Json));
        File.Move(staged, path, overwrite: true);
    }

    /// <summary>Every desktop case (theory rows from their inline data) with what <c>GCAM_UI_CACHE=1</c> would decide.</summary>
    public static IReadOnlyList<PlannedCase> Plan(Mode mode, Store store, Func<Type, Key?> keys, DateTimeOffset nowUtc) =>
        VideoCoverage.DesktopMethods.SelectMany(m => (m.GetCustomAttribute<DesktopTheoryAttribute>() is null
                ? new[] { Array.Empty<object?>() }
                : m.GetCustomAttributes<InlineDataAttribute>().SelectMany(d => d.GetData(m)).Select(row => (object?[])row))
                .Select(arguments => (Type: m.DeclaringType!, Id: CaseId(m.DeclaringType!, m.Name, arguments))))
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => new PlannedCase(c.Type, c.Id, Decide(c.Type, c.Id, keys(c.Type), store, nowUtc, mode)))
            .ToArray();

    private static string Digest(IEnumerable<string> lines) => IsolationPaths.Hash(Encoding.UTF8.GetBytes(string.Join('\n', lines)));

    private static Inputs ReadLive()
    {
        string root = RepoPaths.Root();
        var version = Environment.OSVersion.Version;
        // The update build revision moves with each cumulative update; Environment.OSVersion leaves it out.
        object? revision = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR", null);
        // The display scaling the harness meets; the harness itself is always per-monitor v2 aware (Pointer).
        object? dpi = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "AppliedDPI", null);
        return new(ProbePackage.DeploymentFingerprint(RepoPaths.StudioExe()),
            IsolationPaths.Fingerprint(Path.Combine(root, "tests/Gcam.Studio.UiTests/Harness/Isolation")),
            $"{version.Major}.{version.Minor}.{version.Build}.{(revision is int r ? r : 0)}",
            $"{Scenario.WindowWidth}x{Scenario.WindowHeight} per-monitor-v2 dpi {(dpi is int d ? d.ToString(CultureInfo.InvariantCulture) : "unknown")}",
            CultureInfo.CurrentCulture.Name + "/" + CultureInfo.CurrentUICulture.Name,
            Switches.ToDictionary(s => s, s => Environment.GetEnvironmentVariable(s) == "1" ? "1" : "off", StringComparer.Ordinal),
            root, ReadCatalog(Path.Combine(root, "docs/VV.Tests.md")));
    }
}
