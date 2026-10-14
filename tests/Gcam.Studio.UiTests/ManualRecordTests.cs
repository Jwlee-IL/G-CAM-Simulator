using System.Reflection;
using System.Text;
using System.Text.Json;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

/// <summary>The manual run record, mark geometry and registry binding — headless, no desktop access.</summary>
public sealed class ManualRecordTests
{
    private static readonly byte[] Registry = Encoding.UTF8.GetBytes("""
        {"schema":"gcam.manual-registry","schemaVersion":1,"id":"fixture","locale":"en","title":"Fixture",
         "parts":[{"number":"2","title":"Two"}],
         "chapters":[{"number":"2.4","title":"Start and stop","part":"2","source":"generated","scenario":"manual.fixture"}]}
        """);
    private static readonly byte[] Notices = Encoding.UTF8.GetBytes("""
        {"schema":"gcam.manual-notices","schemaVersion":1,"status":"draft",
         "notices":[{"id":"N-SIM","level":"caution","text":"Simulated.","chapters":["2.4"],"sources":["fixture"]},
                    {"id":"N-OTHER","level":"note","text":"Elsewhere.","chapters":["1.1"],"sources":["fixture"]}]}
        """);

    private static ManualRun Fixture(Dictionary<string, string>? labels = null) =>
        new("manual.fixture", new ManualRegistry(Registry, Notices), "unused", "20261014-120000-abcdef",
            new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(9)),
            (id, _) => (labels ?? new() { ["StartAcquisition"] = "Start" }).GetValueOrDefault(id, ""));

    [Fact]
    public void ImageRect_FloorsTheNearEdgeAndCeilsTheFarEdge_AtFractionalAndNegativeOrigins()
    {
        Assert.Equal(new[] { 10, 10, 9, 6 }, ManualCapture.ImageRect(110.25, 60.0, 8.5, 6.0, 100, 50));   // same fixture as the generator's self-test
        Assert.Equal(new[] { 119, 10, 31, 21 }, ManualCapture.ImageRect(-1800.5, 10.4, 30, 20, -1920, 0)); // a monitor left of the primary
        // 150 % display: UIA bounds are already physical (DIP 100, 100, 20 × 10 → 150, 150, 30 × 15); no second scaling.
        Assert.Equal(new[] { 75, 105, 30, 15 }, ManualCapture.ImageRect(150, 150, 30, 15, 75, 45));
        Assert.True(ManualCapture.Inside([75, 105, 30, 15], 105, 120));
        Assert.False(ManualCapture.Inside([75, 105, 30, 15], 104, 120));   // partly outside: never clipped into a valid mark
        Assert.False(ManualCapture.Inside([-1, 0, 5, 5], 100, 100));
        Assert.False(ManualCapture.Inside([0, 0, 0, 5], 100, 100));
    }

    [Fact]
    public void Registry_EveryGeneratedChapter_HasExactlyOneDesktopScenario()
    {
        var methods = typeof(ManualScenarioTests).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => (Method: m, Chapter: m.GetCustomAttribute<ManualChapterAttribute>())).Where(m => m.Chapter is not null).ToArray();
        Assert.All(methods, m => Assert.NotNull(m.Method.GetCustomAttribute<DesktopFactAttribute>()));
        Assert.Equal(ManualRegistry.Load().Scenarios.Order(), methods.Select(m => m.Chapter!.Scenario).Order());
    }

    [Fact]
    public void Notices_OnlyListedIdsInTheirChapters_AreAccepted()
    {
        var registry = new ManualRegistry(Registry, Notices);
        registry.RequireNotice("N-SIM", "2.4");
        Assert.Throws<InvalidDataException>(() => registry.RequireNotice("N-OTHER", "2.4"));
        Assert.Throws<InvalidDataException>(() => registry.RequireNotice("N-NONE", "2.4"));
        var run = Fixture();
        Assert.Throws<InvalidDataException>(() => run.BeginStep("Click {StartAcquisition}.", "N-OTHER"));
        Assert.Throws<InvalidDataException>(() => ManualRegistry.Load().ForScenario("manual.none"));
    }

    [Fact]
    public void Record_ResolvesScreenLabels_AggregatesTheWorstStep_AndWritesTheGeneratorSchema()
    {
        var run = Fixture();
        run.BeginStep("Click {StartAcquisition}.", "N-SIM");
        run.Check("fixture.start", true, "state Acquiring");
        run.BeginStep("Click {StartAcquisition} again.");
        Assert.ThrowsAny<Exception>(() => run.CheckEqual("fixture.stop", "Stopped", "Acquiring"));
        Assert.Equal("failed", run.Status);
        var subject = new ManualRun.Subject(new string('a', 64), new Dictionary<string, string> { ["Gcam.Studio.dll"] = new string('b', 64) });
        byte[] bytes = run.BuildRecord(new DateTimeOffset(2026, 10, 14, 12, 1, 0, TimeSpan.FromHours(9)), "completed", new string('c', 64),
            subject, subject, new { commit = new string('d', 40), dirtyFiles = 0 }, new { uiLocale = "en" });
        Assert.DoesNotContain((byte)'\r', bytes);
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        Assert.Equal("gcam.manual-run", root.GetProperty("schema").GetString());
        Assert.Equal("2.4", root.GetProperty("chapter").GetProperty("number").GetString());
        Assert.Equal("2026-10-14T12:00:00.000+09:00", root.GetProperty("startedAt").GetString());
        Assert.Equal("failed", root.GetProperty("status").GetString());
        Assert.Equal("completed", root.GetProperty("execution").GetProperty("status").GetString());
        Assert.True(root.GetProperty("subject").GetProperty("unchanged").GetBoolean());
        var steps = root.GetProperty("steps").EnumerateArray().ToArray();
        Assert.Equal("Click **Start**.", steps[0].GetProperty("text").GetString());
        Assert.Equal("N-SIM", steps[0].GetProperty("notices")[0].GetString());
        Assert.Equal(("passed", "pass"), (steps[0].GetProperty("verdict").GetString(), steps[0].GetProperty("checks")[0].GetProperty("result").GetString()));
        Assert.Equal(("failed", "fail"), (steps[1].GetProperty("verdict").GetString(), steps[1].GetProperty("checks")[0].GetProperty("result").GetString()));
        Assert.Equal("Click {StartAcquisition} again.", steps[1].GetProperty("text").GetString());   // a failed step keeps its template
    }

    [Fact]
    public void Record_NonCompletedUiRun_IsAborted_AndAnEmptyRunIsNotExecuted()
    {
        var run = Fixture();
        var subject = new ManualRun.Subject(new string('a', 64), new Dictionary<string, string>());
        var changed = subject with { DeploymentSha256 = new string('e', 64) };
        using var doc = JsonDocument.Parse(run.BuildRecord(DateTimeOffset.UnixEpoch, "cleanup_failed", null, subject, changed, new { }, new { }));
        Assert.Equal("not_executed", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("aborted", doc.RootElement.GetProperty("execution").GetProperty("status").GetString());
        Assert.Equal("ui-run-status: cleanup_failed", doc.RootElement.GetProperty("execution").GetProperty("abortReason").GetString());
        Assert.False(doc.RootElement.GetProperty("subject").GetProperty("unchanged").GetBoolean());
    }

    [Fact]
    public void Labels_AreRequired_AndEscapedForMarkdown_AndErrorsLoseLocalPaths()
    {
        var run = Fixture(new() { ["StartAcquisition"] = "Start *now*", ["Empty"] = "" });
        run.BeginStep("Click {StartAcquisition}.");
        run.ResolveLabels();
        Assert.Equal(@"Click **Start \*now\***.", run.Steps[0].Text);
        run.BeginStep("Click {Empty}.");
        Assert.Throws<InvalidOperationException>(run.ResolveLabels);
        string clean = ManualRun.Sanitise(@"IOException: C:\Users\someone\x.png missing" + "\n   at Stack.Frame()");
        Assert.Equal("IOException: <path> missing", clean);
    }
}
