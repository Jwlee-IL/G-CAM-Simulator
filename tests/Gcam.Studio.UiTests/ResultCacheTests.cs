using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Gcam.Studio.UiTests;

public sealed class ResultCacheTests(ITestOutputHelper output)
{
    private const string RunId = "20261016-120000-abcdef";
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly ResultCache.Mode On = new(CacheOn: true, BreakVerdict: false, Observed: false);
    private static readonly string Unit = "Gcam.Studio.UiTests::" + typeof(ResultCacheTests).FullName;

    private sealed record Fixture(string Root, ResultCache.Inputs Inputs, string ClassFile, string HelperFile);

    /// <summary>A repository-shaped scratch folder whose catalog lists two sources of this class.</summary>
    private static Fixture Build()
    {
        string root = IsolationPathsTests.Scratch();
        const string classFile = "tests/Gcam.Studio.UiTests/ResultCacheTests.cs", helperFile = "tests/Gcam.Studio.UiTests/Harness/Helper.cs";
        foreach (string file in new[] { classFile, helperFile, "tests/Gcam.Studio.UiTests/Unlisted.cs" })
        {
            string path = Path.Combine(root, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "class C\r\n{\r\n    // calls OtherTests.Helper\r\n}\r\n");
        }
        var listed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [classFile] = ResultCache.SourceHash(root, classFile), [helperFile] = ResultCache.SourceHash(root, helperFile),
        };
        var catalog = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal) { [Unit] = listed };
        // Other test classes of the assembly: the class file names OtherTests, which names ThirdTests; UnrelatedTests is not named.
        foreach (var (name, text) in new[] { ("OtherTests", "class OtherTests { int x = ThirdTests.Value; }\n"),
            ("ThirdTests", "class ThirdTests { }\n"), ("UnrelatedTests", "class UnrelatedTests { }\n") })
        {
            string file = $"tests/Gcam.Studio.UiTests/{name}.cs";
            File.WriteAllText(Path.Combine(root, file), text);
            catalog[$"Gcam.Studio.UiTests::Gcam.Studio.UiTests.{name}"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [file] = ResultCache.SourceHash(root, file), [helperFile] = listed[helperFile],
            };
        }
        var switches = ResultCache.Switches.ToDictionary(s => s, s => s == DesktopFactAttribute.OptInVariable ? "1" : "off", StringComparer.Ordinal);
        return new(root, new(new('d', 64), new('e', 64), "10.0.26200.1", "1440x900 per-monitor-v2 dpi 96", "en-US/en-US", switches, root, catalog),
            classFile, helperFile);
    }

    private static ResultCache.Store StoreWith(string caseId, ResultCache.Key key, DateTimeOffset passed, string? osBuild = null) =>
        new(ResultCache.Schema, 1, new() { [caseId] = new(RunId, passed, key.Digest, osBuild ?? key.OsBuild, new(key.Components, StringComparer.Ordinal)) });

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Fact]
    public void SourceHash_ReadsClassFilesAsTextAndHelpersAsBytes_LikeTheCatalog()
    {
        string root = IsolationPathsTests.Scratch();
        byte[] raw = [0xEF, 0xBB, 0xBF, (byte)'a', (byte)'\r', (byte)'\n', (byte)'b', (byte)'\r', (byte)'c'];
        foreach (string file in new[] { "tests/P/X.cs", "tests/P/Harness/H.cs", "tests/P/Harness/Isolation/r.json", "tests/P/DesktopFactAttribute.cs" })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, file))!);
            File.WriteAllBytes(Path.Combine(root, file), raw);
        }
        Assert.Equal(Sha("a\nb\nc"u8.ToArray()), ResultCache.SourceHash(root, "tests/P/X.cs"));
        byte[] helper = [0xEF, 0xBB, 0xBF, (byte)'a', (byte)'\n', (byte)'b', (byte)'\r', (byte)'c'];
        foreach (string file in new[] { "tests/P/Harness/H.cs", "tests/P/Harness/Isolation/r.json", "tests/P/DesktopFactAttribute.cs" })
            Assert.Equal(Sha(helper), ResultCache.SourceHash(root, file));
    }

    [Fact]
    public void SourceHash_AgreesWithTheCatalogGenerator_ForEveryUiTestSource()
    {
        string interpreter = PythonInterpreter.Resolve(PythonInterpreter.Execute, Environment.GetEnvironmentVariable("GCAM_TEST_PYTHON"));
        var result = PythonInterpreter.Execute(interpreter, ["-B", "-c",
            "import json, sys; sys.path.insert(0, sys.argv[1]); import test_records; " +
            "print(json.dumps({k: v['sources'] for k, v in test_records.source_units().items() if k.startswith('Gcam.Studio.UiTests::')}))",
            Path.Combine(RepoPaths.Root(), "samples", "testing")]);
        Assert.True(result.ExitCode == 0, result.Error);
        using var json = JsonDocument.Parse(result.Output);
        int compared = 0;
        foreach (var unit in json.RootElement.EnumerateObject())
            foreach (var source in unit.Value.EnumerateObject())
            {
                Assert.True(source.Value.GetString() == ResultCache.SourceHash(RepoPaths.Root(), source.Name), source.Name);
                compared++;
            }
        Assert.True(compared > 100, $"only {compared} sources compared");
        Assert.Contains(json.RootElement.EnumerateObject(), u => u.Name == Unit);
    }

    [Fact]
    public void Key_ChangesWithEveryComponent_AndNotWithUnlistedFiles()
    {
        var f = Build();
        var key = ResultCache.Compute(typeof(ResultCacheTests), f.Inputs)!;
        Assert.Null(key.CatalogStale);
        Assert.Equal(new[] { "culture", "deployment", "display", "isolation", "os", "sources", "switches" }, key.Components.Keys.Order(StringComparer.Ordinal));
        string caseId = ResultCache.CaseId(typeof(ResultCacheTests), "Case", null);
        var store = StoreWith(caseId, key, Now.AddHours(-1));
        Assert.True(ResultCache.Decide(typeof(ResultCacheTests), caseId, key, store, Now, On).Reuse);
        var switches = new Dictionary<string, string>(f.Inputs.SwitchValues, StringComparer.Ordinal) { [DesktopFactAttribute.LongOptInVariable] = "1" };
        foreach (var (component, changed) in new (string, ResultCache.Inputs)[]
        {
            ("deployment", f.Inputs with { Deployment = new('0', 64) }), ("isolation", f.Inputs with { Isolation = new('1', 64) }),
            ("display", f.Inputs with { Display = "1440x900 per-monitor-v2 dpi 144" }), ("culture", f.Inputs with { Culture = "ko-KR/en-US" }),
            ("switches", f.Inputs with { SwitchValues = switches }),
        })
        {
            var other = ResultCache.Compute(typeof(ResultCacheTests), changed)!;
            Assert.NotEqual(key.Digest, other.Digest);
            var decision = ResultCache.Decide(typeof(ResultCacheTests), caseId, other, store, Now, On);
            Assert.False(decision.Reuse);
            Assert.StartsWith("changed: " + component, decision.Reason);
        }
        // The OS build is part of the key and also expires the entry outright.
        var newOs = ResultCache.Compute(typeof(ResultCacheTests), f.Inputs with { OsBuild = "10.0.26200.2" })!;
        Assert.NotEqual(key.Digest, newOs.Digest);
        Assert.Equal("expired: OS build 10.0.26200.1 -> 10.0.26200.2", ResultCache.Decide(typeof(ResultCacheTests), caseId, newOs, store, Now, On).Reason);

        File.AppendAllText(Path.Combine(f.Root, "tests/Gcam.Studio.UiTests/Unlisted.cs"), "// not listed\n");
        Assert.Equal(key.Digest, ResultCache.Compute(typeof(ResultCacheTests), f.Inputs)!.Digest);

        File.AppendAllText(Path.Combine(f.Root, f.HelperFile), "// edited\n");
        var edited = ResultCache.Compute(typeof(ResultCacheTests), f.Inputs)!;
        Assert.NotEqual(key.Digest, edited.Digest);
        Assert.Equal(f.HelperFile, edited.CatalogStale);
        Assert.Equal("catalog source hash stale: " + f.HelperFile, ResultCache.Decide(typeof(ResultCacheTests), caseId, edited, store, Now, On).Reason);
        // The catalog refreshed to the edited bytes: the digest follows the bytes, so it is the same key as when stale.
        var refreshed = f.Inputs.Catalog[Unit].ToDictionary(p => p.Key, p => ResultCache.SourceHash(f.Root, p.Key), StringComparer.Ordinal);
        var catalog = new Dictionary<string, IReadOnlyDictionary<string, string>>(f.Inputs.Catalog, StringComparer.Ordinal) { [Unit] = refreshed };
        var fresh = ResultCache.Compute(typeof(ResultCacheTests), f.Inputs with { Catalog = catalog })!;
        Assert.Null(fresh.CatalogStale);
        Assert.Equal(edited.Digest, fresh.Digest);
        Assert.StartsWith("changed: sources", ResultCache.Decide(typeof(ResultCacheTests), caseId, fresh, store, Now, On).Reason);

        Assert.Null(ResultCache.Compute(typeof(VideoModelTests), f.Inputs));
        Assert.Equal("no catalog entry for the class in docs/VV.Tests.md", ResultCache.Decide(typeof(VideoModelTests), caseId, null, store, Now, On).Reason);
    }

    [Fact]
    public void Key_FollowsTheTestClassesItsOwnFilesName_Transitively()
    {
        var f = Build();
        var key = ResultCache.Compute(typeof(ResultCacheTests), f.Inputs)!;
        File.AppendAllText(Path.Combine(f.Root, "tests/Gcam.Studio.UiTests/UnrelatedTests.cs"), "// edited\n");
        Assert.Equal(key.Digest, ResultCache.Compute(typeof(ResultCacheTests), f.Inputs)!.Digest);
        foreach (string name in new[] { "ThirdTests", "OtherTests" })   // named through OtherTests, then directly
        {
            string file = $"tests/Gcam.Studio.UiTests/{name}.cs";
            File.AppendAllText(Path.Combine(f.Root, file), "// edited\n");
            var edited = ResultCache.Compute(typeof(ResultCacheTests), f.Inputs)!;
            Assert.NotEqual(key.Digest, edited.Digest);
            Assert.Equal(file, edited.CatalogStale);   // a referenced class's catalog hash is checked as well
            key = edited;
        }
    }

    [Fact]
    public void Decide_ReusesOnlyAMatchingPassWithinSevenDays_WithAParseableReason()
    {
        var f = Build();
        var key = ResultCache.Compute(typeof(ResultCacheTests), f.Inputs)!;
        string caseId = ResultCache.CaseId(typeof(ResultCacheTests), "Export", ["Html", "html"]);
        Assert.NotEqual(caseId, ResultCache.CaseId(typeof(ResultCacheTests), "Export", ["Pdf", "pdf"]));
        var store = StoreWith(caseId, key, Now.AddDays(-6.9));
        var reused = ResultCache.Decide(typeof(ResultCacheTests), caseId, key, store, Now, On);
        Assert.True(reused.Reuse);
        Assert.Matches(@"^\[cache-reused\] reused from run 20261016-120000-abcdef \(key [0-9a-f]{12}\)$", reused.Reason);
        Assert.Equal($"[cache-reused] reused from run {RunId} (key {key.Digest[..12]})", reused.Reason);

        Assert.Equal("cache off (GCAM_UI_CACHE is not 1)", ResultCache.Decide(typeof(ResultCacheTests), caseId, key, store, Now, On with { CacheOn = false }).Reason);
        Assert.StartsWith("expired: pass of", ResultCache.Decide(typeof(ResultCacheTests), caseId, key, StoreWith(caseId, key, Now - ResultCache.Expiry - TimeSpan.FromSeconds(1)), Now, On).Reason);
        Assert.True(ResultCache.Decide(typeof(ResultCacheTests), caseId, key, StoreWith(caseId, key, Now - ResultCache.Expiry), Now, On).Reuse);
        Assert.StartsWith("expired:", ResultCache.Decide(typeof(ResultCacheTests), caseId, key, StoreWith(caseId, key, Now.AddHours(1)), Now, On).Reason);
        Assert.StartsWith("expired: OS build", ResultCache.Decide(typeof(ResultCacheTests), caseId, key, StoreWith(caseId, key, Now, "10.0.22631.1"), Now, On).Reason);
        Assert.Equal("no recorded pass", ResultCache.Decide(typeof(ResultCacheTests), "other-case", key, store, Now, On).Reason);
    }

    [Fact]
    public void Decide_NeverReusesTheObservedRunThePolishSurveyABrokenVerdictOrAnObservedBatch()
    {
        var f = Build();
        var key = ResultCache.Compute(typeof(ResultCacheTests), f.Inputs)!;
        foreach (var type in new[] { typeof(ObservedRunTests), typeof(PolishSurveyTests), typeof(ManualScenarioTests) })
        {
            string caseId = ResultCache.CaseId(type, "Case", null);
            var store = StoreWith(caseId, key, Now);
            bool excluded = type != typeof(ManualScenarioTests);
            Assert.Equal(!excluded, ResultCache.Decide(type, caseId, key, store, Now, On).Reuse);   // manual chapters outside a batch may reuse
            Assert.StartsWith("never reused:", ResultCache.Decide(type, caseId, key, store, Now, On with { BreakVerdict = true }).Reason);
            Assert.StartsWith("never reused:", ResultCache.Decide(type, caseId, key, store, Now, On with { Observed = true }).Reason);
        }
        Assert.Contains("GCAM_UI_BREAK_VERDICT=1", ResultCache.Decide(typeof(ManualScenarioTests), "x", key, StoreWith("x", key, Now), Now, On with { BreakVerdict = true }).Reason);
        Assert.Contains("GCAM_UI_OBSERVE_DIRECTORY", ResultCache.Decide(typeof(ManualScenarioTests), "x", key, StoreWith("x", key, Now), Now, On with { Observed = true }).Reason);
    }

    [Fact]
    public void Finish_RecordsOnlyPasses_AndAFailedOrBrokenRunNeverLeavesAReusableEntry()
    {
        var f = Build();
        var key = ResultCache.Compute(typeof(ResultCacheTests), f.Inputs)!;
        string folder = Path.Combine(f.Root, "ui-videos"), path = Path.Combine(folder, "cache.json");   // no video beside it
        var normal = On with { CacheOn = false };   // recording does not need the cache switch
        string a = ResultCache.CaseId(typeof(ResultCacheTests), "A", null), b = ResultCache.CaseId(typeof(ResultCacheTests), "B", null);

        ResultCache.Finish(path, typeof(ResultCacheTests), a, key, RunId, passed: false, normal, Now);
        Assert.False(File.Exists(path));   // a failure creates nothing
        ResultCache.Finish(path, typeof(ResultCacheTests), a, key, RunId, passed: true, normal, Now);
        ResultCache.Finish(path, typeof(ResultCacheTests), b, key, RunId, passed: true, normal, Now);
        Assert.Equal(new[] { a, b }, ResultCache.Read(path).Cases.Keys.Order(StringComparer.Ordinal));
        Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp"));
        Assert.True(ResultCache.Decide(typeof(ResultCacheTests), a, key, ResultCache.Read(path), Now.AddDays(1), On).Reuse);

        ResultCache.Finish(path, typeof(ResultCacheTests), a, key, "20261017-090000-123456", passed: true, normal, Now.AddDays(1));
        Assert.Equal("20261017-090000-123456", ResultCache.Read(path).Cases[a].RunId);   // a later pass refreshes

        ResultCache.Finish(path, typeof(ResultCacheTests), a, key, "20261017-100000-654321", passed: false, normal, Now.AddDays(2));
        var after = ResultCache.Read(path);
        Assert.False(after.Cases.ContainsKey(a));   // the failure neither refreshed nor kept the older pass
        Assert.Equal(RunId, after.Cases[b].RunId);
        Assert.Equal("no recorded pass", ResultCache.Decide(typeof(ResultCacheTests), a, key, after, Now.AddDays(2), On).Reason);

        byte[] before = File.ReadAllBytes(path);
        ResultCache.Finish(path, typeof(ResultCacheTests), a, key, RunId, passed: true, normal with { BreakVerdict = true }, Now);
        ResultCache.Finish(path, typeof(ResultCacheTests), b, key, RunId, passed: false, normal with { BreakVerdict = true }, Now);
        ResultCache.Finish(path, typeof(ObservedRunTests), a, key, RunId, passed: true, normal, Now);
        ResultCache.Finish(path, typeof(PolishSurveyTests), b, key, RunId, passed: false, normal, Now);
        ResultCache.Finish(path, typeof(ResultCacheTests), a, key, runId: null, passed: true, normal, Now);
        ResultCache.Finish(path, typeof(ResultCacheTests), a, key: null, RunId, passed: true, normal, Now);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(ResultCache.RecordsPasses(normal with { BreakVerdict = true }));
    }

    [Fact]
    public void Store_RejectsUnknownSchemaDuplicatesAndInvalidEntries_AndAnUnreadableStoreReusesNothing()
    {
        string folder = IsolationPathsTests.Scratch(); Directory.CreateDirectory(folder);
        string entry = $"{{\"runId\":\"{RunId}\",\"passedAtUtc\":\"2026-10-16T12:00:00+00:00\",\"key\":\"{new string('a', 64)}\",\"osBuild\":\"10.0.1.2\",\"components\":{{}}}}";
        string Write(string name, string text) { string p = Path.Combine(folder, name); File.WriteAllText(p, text); return p; }
        Assert.Single(ResultCache.Read(Write("ok.json", $"{{\"schema\":\"gcam.ui-cache\",\"schemaVersion\":1,\"cases\":{{\"c\":{entry}}}}}")).Cases);
        foreach (string bad in new[]
        {
            $"{{\"schema\":\"gcam.ui-cache\",\"schemaVersion\":2,\"cases\":{{\"c\":{entry}}}}}",
            $"{{\"schema\":\"gcam.ui-videos\",\"schemaVersion\":1,\"cases\":{{}}}}",
            $"{{\"schema\":\"gcam.ui-cache\",\"schemaVersion\":1,\"cases\":{{\"c\":{entry},\"c\":{entry}}}}}",
            $"{{\"schema\":\"gcam.ui-cache\",\"schemaVersion\":1,\"cases\":{{\"c\":{entry.Replace(RunId, "run with spaces")}}}}}",
            $"{{\"schema\":\"gcam.ui-cache\",\"schemaVersion\":1,\"cases\":{{\"c\":{entry.Replace(new string('a', 64), "short")}}}}}",
            $"{{\"schema\":\"gcam.ui-cache\",\"schemaVersion\":1,\"cases\":{{\"../c\":{entry}}}}}",
        })
        {
            string path = Write("bad.json", bad);
            Assert.Throws<InvalidDataException>(() => ResultCache.Read(path));
            Assert.Empty(ResultCache.ReadOrEmpty(path).Cases);
        }
        Assert.Empty(ResultCache.ReadOrEmpty(Write("corrupt.json", "{\"schema\":")).Cases);
        Assert.Empty(ResultCache.ReadOrEmpty(Path.Combine(folder, "absent.json")).Cases);
    }

    [Fact]
    public void Discoverers_MakeDesktopCasesCacheAware_PerTheoryRow_WithTheOptInSkipFirst()
    {
        foreach (var (attribute, discoverer) in new[] { (typeof(DesktopFactAttribute), typeof(CachedFactDiscoverer)), (typeof(DesktopTheoryAttribute), typeof(CachedTheoryDiscoverer)) })
        {
            var data = attribute.CustomAttributes.Single(a => a.AttributeType == typeof(XunitTestCaseDiscovererAttribute));
            Assert.Equal(discoverer, Type.GetType($"{data.ConstructorArguments[0].Value}, {data.ConstructorArguments[1].Value}"));
        }
        var desktop = typeof(PilotTests).GetMethods().First(m => m.GetCustomAttribute<DesktopFactAttribute>() is not null);
        var facts = new CachedFactDiscoverer(new Sink()).Discover(new Options(), Method(desktop), Attribute(desktop)).ToArray();
        var fact = Assert.IsType<CachedDesktopTestCase>(Assert.Single(facts));
        string? optIn = desktop.GetCustomAttribute<DesktopFactAttribute>()!.Skip;
        Assert.True(fact.SkipReason == optIn || optIn is null && (fact.SkipReason is null || fact.SkipReason.StartsWith(ResultCache.ReusedTag, StringComparison.Ordinal)), fact.SkipReason);
        // An ordinary theory (no opt-in skip) shows the per-row path: one cache-aware case per data row, none reused.
        var theory = typeof(PythonInterpreterTests).GetMethods().Single(m => m.GetCustomAttribute<TheoryAttribute>() is not null);
        var rows = new CachedTheoryDiscoverer(new Sink()).Discover(new Options(), Method(theory), Attribute(theory)).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => { Assert.IsType<CachedDesktopTestCase>(row); Assert.Single(row.TestMethodArguments); Assert.Null(row.SkipReason); });
    }

    [Fact]
    public void ReuseReason_IsReadByTheTestRecordAdapter_AsItsOwnReasonCode()
    {
        var f = Build();
        var key = ResultCache.Compute(typeof(ResultCacheTests), f.Inputs)!;
        string reason = ResultCache.Decide(typeof(ResultCacheTests), "c", key, StoreWith("c", key, Now), Now, On).Reason;
        string interpreter = PythonInterpreter.Resolve(PythonInterpreter.Execute, Environment.GetEnvironmentVariable("GCAM_TEST_PYTHON"));
        var result = PythonInterpreter.Execute(interpreter, ["-B", "-c",
            "import json, sys; sys.path.insert(0, sys.argv[1]); import adapters; " +
            "print(json.dumps([adapters.skip_reason(sys.argv[2], {'GCAM_UI_TESTS': '1'}), adapters.cache_reuse(sys.argv[2])]))",
            Path.Combine(RepoPaths.Root(), "samples", "testing"), reason]);
        Assert.True(result.ExitCode == 0, result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal("cache_reused", json.RootElement[0][0].GetString());
        Assert.Equal(ResultCache.Variable, json.RootElement[0][1].GetString());
        Assert.Equal(RunId, json.RootElement[1].GetProperty("sourceRunId").GetString());
        Assert.Equal(key.Short, json.RootElement[1].GetProperty("cacheKey").GetString());
    }

    /// <summary>The listing of which desktop cases <c>GCAM_UI_CACHE=1</c> would run and why (read-only).</summary>
    [Fact]
    public void Plan_ListsEveryDesktopCase_AndWhyItWouldRun()
    {
        var mode = ResultCache.Mode.Current();
        var store = ResultCache.ReadOrEmpty(ResultCache.StorePath);
        var plan = ResultCache.Plan(mode with { CacheOn = true }, store, ResultCache.KeyFor, DateTimeOffset.UtcNow);
        Assert.Equal(plan.Count, plan.Select(c => c.CaseId).Distinct().Count());
        Assert.True(VideoCoverage.DesktopMethods.Select(m => m.DeclaringType!).ToHashSet().SetEquals(plan.Select(c => c.TestClass)));
        Assert.All(plan.Select(c => c.TestClass).Distinct(), type => Assert.True(ResultCache.KeyFor(type) is not null, type.Name + " has no catalog entry"));
        var text = new StringBuilder();
        text.AppendLine($"{ResultCache.Variable} is {(mode.CacheOn ? "on" : "off")}; {store.Cases.Count} recorded passes; with {ResultCache.Variable}=1:");
        foreach (var c in plan) text.AppendLine($"{(c.Decision.Reuse ? "reuse" : "run  ")}  {c.CaseId}  {c.Decision.Reason}");
        text.AppendLine($"{plan.Count(c => !c.Decision.Reuse)} of {plan.Count} desktop cases would run.");
        output.WriteLine(text.ToString());
    }

    private static TestMethod Method(MethodInfo method)
    {
        var assembly = new TestAssembly(Reflector.Wrap(method.DeclaringType!.Assembly));
        return new(new TestClass(new TestCollection(assembly, null, "fixture"), Reflector.Wrap(method.DeclaringType)), Reflector.Wrap(method));
    }

    private static IAttributeInfo Attribute(MethodInfo method) =>
        Reflector.Wrap(method.CustomAttributes.Single(a => typeof(FactAttribute).IsAssignableFrom(a.AttributeType)));

    private sealed class Sink : LongLivedMarshalByRefObject, IMessageSink
    {
        public bool OnMessage(IMessageSinkMessage message) => true;
    }

    private sealed class Options : ITestFrameworkDiscoveryOptions
    {
        public TValue GetValue<TValue>(string name) => default!;
        public void SetValue<TValue>(string name, TValue value) { }
    }
}
