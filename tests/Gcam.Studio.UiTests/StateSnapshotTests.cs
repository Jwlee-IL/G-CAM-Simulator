using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class StateSnapshotTests
{
    [Fact]
    public void ExportNameEvidence_IdentifiesTheExactTypedName_WithoutKeepingPrivateValueData()
    {
        const string run = "20261022-100000-abcdef";
        const string export = "report-" + run + ".html";
        var registry = new FakeRegistry(new()
        {
            [""] = new(DateTime.UtcNow, [new("13", 3, Encoding.Unicode.GetBytes(@"C:\private-folder\" + export)),
                new("14", 3, Encoding.Unicode.GetBytes("different-name-" + run))], []),
        });
        string root = IsolationPathsTests.Scratch(); Directory.CreateDirectory(root);
        string path = Path.Combine(root, "snapshot-C.jsonl.gz");
        StateSnapshot.Take("C", path, [], registry, [run], [export, "report-" + run + ".csv"]);
        var rows = Lines(path).Where(r => r.GetProperty("c").GetString() == "v").ToArray();
        Assert.Equal(new[] { export }, rows[0].GetProperty("names").EnumerateArray().Select(n => n.GetString()));
        Assert.Equal(1, rows[1].GetProperty("m").GetInt32());
        Assert.Equal(new[] { run }, rows[1].GetProperty("runMarkers").EnumerateArray().Select(m => m.GetString()));
        Assert.False(rows[1].TryGetProperty("names", out _));
        Assert.DoesNotContain("private-folder", string.Join("\n", rows.Select(r => r.GetRawText())));
    }

    private sealed class FakeRegistry(Dictionary<string, IRegistrySource.Key> keys) : IRegistrySource
    {
        public HashSet<string> Denied { get; } = [];
        public IRegistrySource.Key Read(string path) =>
            Denied.Contains(path) ? throw new UnauthorizedAccessException("fixture") : keys[path];
    }

    private static List<JsonElement> Lines(string path)
    {
        using var reader = new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress), Encoding.UTF8);
        var lines = new List<JsonElement>();
        for (string? line; (line = reader.ReadLine()) is not null; ) lines.Add(JsonDocument.Parse(line).RootElement.Clone());
        return lines;
    }

    private static FakeRegistry Registry()
    {
        var written = new DateTime(2026, 10, 14, 1, 2, 3, DateTimeKind.Utc);
        return new(new()
        {
            [""] = new(written, [], ["Software"]),
            ["Software"] = new(written, [new("", 1, Encoding.Unicode.GetBytes("default\0"))], ["Fixture", "Locked"]),
            [@"Software\Fixture"] = new(written.AddHours(1), [new("Left", 4, [1, 0, 0, 0]), new(@"a\b", 3, [])], []),
        });
    }

    [Fact]
    public void Take_TempFolder_RecordsFilesDirectoriesOnceAndKeysWithRawValueHashes()
    {
        string root = IsolationPathsTests.Scratch(), outputs = IsolationPathsTests.Scratch();
        Directory.CreateDirectory(Path.Combine(root, "inner", "deep")); Directory.CreateDirectory(outputs);
        File.WriteAllText(Path.Combine(root, "a.txt"), "12345");
        File.WriteAllText(Path.Combine(root, "inner", "deep", "b.bin"), "x");
        File.SetAttributes(Path.Combine(root, "a.txt"), FileAttributes.ReadOnly);
        var registry = Registry(); registry.Denied.Add(@"Software\Locked");
        string output = Path.Combine(outputs, "snapshot-A.jsonl.gz");
        try
        {
            var counts = StateSnapshot.Take("A", output, [new("Outer", root), new("Inner", Path.Combine(root, "inner")),
                new("Missing", Path.Combine(root, "absent"))], registry);
            var lines = Lines(output);
            var header = lines[0];
            Assert.Equal(StateSnapshot.Schema, header.GetProperty("schema").GetString());
            var roots = header.GetProperty("roots").EnumerateArray().ToDictionary(r => r.GetProperty("category").GetString()!);
            Assert.True(roots["Outer"].GetProperty("walked").GetBoolean());
            Assert.False(roots["Inner"].GetProperty("walked").GetBoolean()); // listed by the outer walk only
            Assert.False(roots["Missing"].GetProperty("exists").GetBoolean());
            var files = lines.Where(l => l.GetProperty("c").GetString() == "f").ToDictionary(l => Path.GetFileName(l.GetProperty("p").GetString()!));
            Assert.Equal(2, files.Count);
            Assert.Equal(5, files["a.txt"].GetProperty("s").GetInt64());
            Assert.True(((FileAttributes)files["a.txt"].GetProperty("a").GetInt32()).HasFlag(FileAttributes.ReadOnly));
            Assert.Equal(File.GetLastWriteTimeUtc(Path.Combine(root, "a.txt")).ToFileTimeUtc(), files["a.txt"].GetProperty("t").GetInt64());
            Assert.Equal(2, lines.Count(l => l.GetProperty("c").GetString() == "d"));
            var keys = lines.Where(l => l.GetProperty("c").GetString() == "k").Select(l => l.GetProperty("p").GetString()).ToArray();
            Assert.Equal(new[] { "HKCU", @"HKCU\Software", @"HKCU\Software\Fixture" }, keys.Order().ToArray());
            var left = lines.Single(l => l.GetProperty("c").GetString() == "v" && l.GetProperty("n").GetString() == "Left");
            Assert.Equal(@"HKCU\Software\Fixture", left.GetProperty("p").GetString());
            Assert.Equal(4, left.GetProperty("k").GetInt32());
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1, 0, 0, 0 })), left.GetProperty("h").GetString());
            Assert.Contains(lines, l => l.GetProperty("c").GetString() == "v" && l.GetProperty("n").GetString() == @"a\b");
            var denied = lines.Single(l => l.GetProperty("c").GetString() == "u");
            Assert.Equal(("registry", @"HKCU\Software\Locked", "UnauthorizedAccessException"),
                (denied.GetProperty("ch").GetString(), denied.GetProperty("p").GetString(), denied.GetProperty("e").GetString()));
            var end = lines[^1];
            Assert.Equal("end", end.GetProperty("c").GetString());
            Assert.Equal((2, 2, 3, 3, 1), (counts.Files, counts.Directories, counts.Keys, counts.Values, counts.Unreadable));
            Assert.Equal(counts.Values, end.GetProperty("values").GetInt32());
            using (var stream = File.OpenRead(output)) Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(stream)), counts.Sha256);
            // A snapshot never overwrites an existing file.
            Assert.Throws<IOException>(() => StateSnapshot.Take("A", output, [new("Outer", root)], registry));
        }
        finally
        {
            File.SetAttributes(Path.Combine(root, "a.txt"), FileAttributes.Normal);
        }
    }

    [Fact]
    public void Take_WithRunIdMarkers_FlagsValuesWhoseDataCarriesOne_AndRecordsTheMarkersInTheHeader()
    {
        // TODO-52 (v3.3 candidate 2): the hash hides value data afterwards, so the marker is looked for at snapshot time.
        const string run = "probe-20261016-07";
        var written = new DateTime(2026, 10, 16, 1, 2, 3, DateTimeKind.Utc);
        byte[] shellItem = [0x14, 0x00, .. Encoding.Unicode.GetBytes(@"C:\x\gcam-uia-" + run.ToUpperInvariant() + @"\report.html"), 0x00];
        var registry = new FakeRegistry(new()
        {
            [""] = new(written, [], ["Software"]),
            ["Software"] = new(written, [
                new("Unicode", 1, Encoding.Unicode.GetBytes("report-" + run + ".csv\0")),
                new("OddAligned", 3, [0x01, .. Encoding.Unicode.GetBytes(run)]),
                new("Ascii", 3, Encoding.ASCII.GetBytes("x" + run + "y")),
                new("ShellItem", 3, shellItem),
                new("Other", 3, Encoding.Unicode.GetBytes("probe-20261016-08")),
                new("Empty", 3, []),
            ], []),
        });
        string outputs = IsolationPathsTests.Scratch(); Directory.CreateDirectory(outputs);
        string output = Path.Combine(outputs, "snapshot-C.jsonl.gz");
        StateSnapshot.Take("C", output, [], registry, [run, "short", ""]);
        var lines = Lines(output);
        var header = lines[0];
        Assert.Equal(2, header.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(new[] { run }, header.GetProperty("valueMarkers").GetProperty("markers").EnumerateArray().Select(m => m.GetString()));
        var flagged = lines.Where(l => l.GetProperty("c").GetString() == "v")
            .ToDictionary(l => l.GetProperty("n").GetString()!, l => l.TryGetProperty("m", out var m) && m.GetInt32() == 1);
        Assert.Equal(new Dictionary<string, bool> { ["Unicode"] = true, ["OddAligned"] = true, ["Ascii"] = true, ["ShellItem"] = true,
            ["Other"] = false, ["Empty"] = false }, flagged);
        // Without markers nothing is flagged and the header says so.
        string plain = Path.Combine(outputs, "snapshot-A.jsonl.gz");
        StateSnapshot.Take("A", plain, [], registry);
        Assert.Empty(Lines(plain)[0].GetProperty("valueMarkers").GetProperty("markers").EnumerateArray());
        Assert.DoesNotContain(Lines(plain), l => l.TryGetProperty("m", out _));
    }

    [Fact]
    public void ObservedRoots_CoverTheCharterScope_WithSandboxAndRunFolderNamed()
    {
        var roots = StateSnapshot.ObservedRoots(@"C:\fixture\deploy", @"C:\fixture\sandbox", @"C:\fixture\run");
        var categories = roots.Select(r => r.Category).ToArray();
        foreach (string expected in new[] { "APPDATA", "LOCALAPPDATA", "VirtualStore", "TEMP", "LocalLow", "Documents", "Desktop",
                     "ProgramData", "Deployment", "Sandbox", "RunDirectory" })
            Assert.Contains(expected, categories);
        Assert.Equal(@"C:\fixture\sandbox", roots.Single(r => r.Category == "Sandbox").Path);
    }
}
