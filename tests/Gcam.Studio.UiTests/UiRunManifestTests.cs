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
    public void SkippedScenario_RequiresAndRetainsTheExactConditionId()
    {
        var manifest = new UiRunManifest("fixture", "case"); manifest.CleanupCompleted([]);
        Assert.Throws<InvalidDataException>(() => manifest.Finalise("skipped"));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(manifest.Finalise("skipped", skipConditionId: "desktop-opt-in-off")));
        Assert.Equal("desktop-opt-in-off", json.RootElement.GetProperty("scenarios")[0].GetProperty("SkipConditionId").GetString());
    }
}
