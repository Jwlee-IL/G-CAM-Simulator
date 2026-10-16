using System.Text.Json;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class UiRunManifestTests
{
    [Fact]
    public void ProbeApproval_DoesNotApproveTheP4Charter()
    {
        var manifest = new UiRunManifest("fixture", "probe");
        manifest.RecordApprovedProbe(new("fixture", new string('a', 40), DateTimeOffset.UtcNow, "isolation", "deployment", []));
        manifest.CleanupCompleted([]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(manifest.Finalise("passed", observed: true)));
        var charter = json.RootElement.GetProperty("charter");
        Assert.Equal("launch-idle-exit-probe-only", charter.GetProperty("scope").GetString());
        Assert.Equal("draft-not-approved", charter.GetProperty("p4Status").GetString());
    }

    [Fact]
    public void CleanupAndRecordFailure_DoNotMaskTheBodyFailure()
    {
        var body = new Xunit.Sdk.XunitException("fixture body");
        var issues = new List<UiRunManifest.Issue>();
        Exception? actual = Gcam.Studio.UiTests.Harness.Isolation.CleanupBoundary.Attempt(body,
            () => throw new IOException("fixture cleanup"), issues, "cleanup_failed");
        actual = Gcam.Studio.UiTests.Harness.Isolation.CleanupBoundary.Attempt(actual,
            () => throw new IOException("fixture record"), issues, "blocked");
        Assert.Same(body, actual); Assert.Equal(2, issues.Count);
    }
    [Fact]
    public void Finalisation_RequiresCleanup_AndDoesNotConfuseProductFailureWithUnsafeRun()
    {
        var manifest = new UiRunManifest("fixture", "case");
        Assert.Throws<InvalidOperationException>(() => manifest.Finalise("passed"));
        manifest.CleanupCompleted([]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(manifest.Finalise("failed", "P4")));
        Assert.Equal("completed", manifest.RunStatus);
        Assert.Equal("final", manifest.ManifestPhase);
        Assert.Equal("failed", json.RootElement.GetProperty("scenarios")[0].GetProperty("Status").GetString());
        Assert.Equal("blocked", json.RootElement.GetProperty("resultBundles")[0].GetProperty("Status").GetString());
        Assert.Throws<InvalidOperationException>(() => manifest.Finalise("passed"));
        Assert.Throws<InvalidOperationException>(() => manifest.CleanupCompleted([]));
    }
    [Fact]
    public void StatusPrecedence_PreservesEveryIssue_AndNeverPromotesDraftGates()
    {
        var manifest = new UiRunManifest("fixture", "case");
        manifest.CleanupCompleted([new("blocked", "environment"), new("cleanup_failed", "lease"), new("unsafe_effect", "write")]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(manifest.Finalise("passed")));
        Assert.Equal("unsafe_effect", manifest.RunStatus);
        Assert.Equal(3, json.RootElement.GetProperty("runIssues").GetArrayLength());
        Assert.Equal(2, json.RootElement.GetProperty("executionGates").GetArrayLength());
        Assert.Equal(5, json.RootElement.GetProperty("resultBundles")[0].GetProperty("Gates").GetArrayLength());
    }

    [Fact]
    public void ObservedWriteAudit_SchemaThree_NamesTheScopeWithItsBasisAndTheBlindSpots()
    {
        // TODO-52: manifest schema 3 carries observationScope, unattributed and controlWindow in an observed run's writeAudit.
        var manifest = new UiRunManifest("fixture", "probe"); manifest.CleanupCompleted([]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(manifest.Finalise("passed", observed: true)));
        Assert.Equal(3, json.RootElement.GetProperty("schemaVersion").GetInt32());
        string isolation = Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation");
        var scope = Gcam.Studio.UiTests.Harness.Isolation.ObservationScope.Describe(isolation, [@"C:\scratch\gcam-uia-fixture-run"],
            [@"HKCU\Software\GcamUiaProbe\fixture-run"], ["Gcam.Studio.exe"]);
        string[] bases = scope.Select(l => l.Id + " | " + l.Basis.Split(" (")[0]).ToArray();
        foreach (string expected in new[] { "harness-owned | harness-owned", "app-report-export | declared default", "app-report-export | resolved value",
                     "app-logs-cache-temp | isolation location", "app-tsf-composition-window | declared default", "app-gpu-driver-timestamps | declared default",
                     "E6-os-save-dialog-history | shell record location", "recent-document-links | shell record location",
                     "owned-processes | registered image and its descendants" })
            Assert.Contains(expected, bases);
        Assert.Contains(scope, l => l.Channel == "registry" && l.Where == @"HKCU\Software\\Microsoft\\CTF\\CUAS\\DefaultCompositionWindow / Left|Top");   // key and names as patterns
        var blind = Gcam.Studio.UiTests.Harness.Isolation.ObservationScope.BlindSpots(isolation);
        foreach (string id in new[] { "framework-registry", "framework-cache-shell", "framework-crash", "shell record jump-lists", "shell record recent-documents-registry" })
            Assert.Contains(blind, b => b.StartsWith(id + ": ", StringComparison.Ordinal));
        using var window = JsonDocument.Parse(JsonSerializer.Serialize(
            Gcam.Studio.UiTests.Harness.Isolation.ObservationScope.ControlWindow(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(5))));
        Assert.Equal(("changed-in-null-window", false), (window.RootElement.GetProperty("rule").GetString(), window.RootElement.GetProperty("shorterThanAppWindow").GetBoolean()));
        using var pending = JsonDocument.Parse(JsonSerializer.Serialize(Gcam.Studio.UiTests.Harness.Isolation.ObservationScope.PendingUnattributed()));
        Assert.Equal(JsonValueKind.Null, pending.RootElement.GetProperty("count").ValueKind);
    }

    [Fact]
    public void SkippedScenario_RequiresAndRetainsTheExactConditionId()
    {
        var manifest = new UiRunManifest("fixture", "case"); manifest.CleanupCompleted([]);
        Assert.Throws<InvalidDataException>(() => manifest.Finalise("skipped"));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(manifest.Finalise("skipped", skipConditionId: "desktop-opt-in-off")));
        Assert.Equal("desktop-opt-in-off", json.RootElement.GetProperty("scenarios")[0].GetProperty("SkipConditionId").GetString());
    }
}
