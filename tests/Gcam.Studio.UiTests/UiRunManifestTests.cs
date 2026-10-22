using System.Text.Json;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class UiRunManifestTests
{
    [Fact]
    public void NewManifest_CarriesTheCheckedPolicyDataAndExactE6Grant_WithNamedLegacyApprovals()
    {
        string commit = new('a', 40), deployment = new('b', 64);
        var at = DateTimeOffset.Parse("2026-10-14T00:00:00Z");
        var package = new Gcam.Studio.UiTests.Harness.Isolation.ProbePackage("batch-fixture", commit, at, "isolation", deployment,
            [new("charter", "HUM-04", commit, "Synthetic charter", at), new("go", "HUM-01", commit, "Synthetic go", at),
             new("effect", "real-world-effect", commit, "Synthetic permission for export shell records", at)])
        {
            RealWorldEffects = [new("E6", "effect", "batch-fixture", commit, deployment, ["export"], ["report-{runId}.html"], ["save-dialog-history"])],
        };
        const string run = "20261022-100000-abcdef";
        var grants = package.BindEffects(run, "export", ["report-" + run + ".html"]);
        var data = new Gcam.Studio.UiTests.Harness.Isolation.ContentFingerprint.DataCheck("v4", "list", deployment, [".dll", ".exe", ".json"], [], false,
            new("harness-typed-scenes", "synthetic", "fixture"), "export", "assembly");
        var policy = new Gcam.Studio.UiTests.Harness.Isolation.ActionPolicy(new(1, "draft", []));
        var manifest = new UiRunManifest(run, "export");
        manifest.RecordPreflight(new("v4", [], data, new("charter-exception", "charter", "reason", "risk", []), "policy-hash", grants, policy, package));
        manifest.CleanupCompleted([]);
        var result = JsonSerializer.SerializeToElement(manifest.Finalise("passed"),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal("v4", result.GetProperty("contract").GetString());
        Assert.Equal("policy-hash", result.GetProperty("preflight").GetProperty("actionPolicySha256").GetString());
        Assert.Equal("report-" + run + ".html", result.GetProperty("realWorldEffects")[0].GetProperty("typedNames")[0].GetString());
        Assert.All(result.GetProperty("charter").GetProperty("approvals").EnumerateArray(), a =>
            Assert.DoesNotContain("HUM-", a.GetProperty("humanActId").GetString()));
        Assert.DoesNotContain("\"profile\"", result.GetRawText());
        Assert.DoesNotContain("HUM-", result.GetRawText());
    }

    [Fact]
    public void ProbeApproval_DoesNotApproveTheManualCharter()
    {
        var manifest = new UiRunManifest("fixture", "probe");
        manifest.RecordApprovedProbe(new("fixture", new string('a', 40), DateTimeOffset.UtcNow, "isolation", "deployment", []));
        manifest.CleanupCompleted([]);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(manifest.Finalise("passed", observed: true)));
        var charter = json.RootElement.GetProperty("charter");
        Assert.Equal("launch-idle-exit-probe-only", charter.GetProperty("scope").GetString());
        Assert.Equal("draft-not-approved", charter.GetProperty("manualStatus").GetString());
    }

    [Theory]
    [InlineData("export", false)]
    [InlineData("probe", false)]
    [InlineData("probe", true)]
    public void Preflight_RecordsOnlyTheActualApplicableQualification(string scenario, bool probeCharter)
    {
        var at = DateTimeOffset.UtcNow;
        string commit = new('a', 40);
        var package = new Gcam.Studio.UiTests.Harness.Isolation.ProbePackage("batch", commit, at, "i", "d",
            [new("charter", "charter", commit, "Synthetic charter", at), new("go", "desktop-go", commit, "Synthetic go", at)]);
        var data = new Gcam.Studio.UiTests.Harness.Isolation.ContentFingerprint.DataCheck("v4", "list", "d", [], [], false,
            new("generator", "synthetic", "fixture"), scenario, "assembly");
        var policy = new Gcam.Studio.UiTests.Harness.Isolation.ActionPolicy(new(1, "draft", []));
        var manifest = new UiRunManifest("scenario-run", scenario);
        if (probeCharter) manifest.RecordApprovedProbe(package);
        manifest.RecordPreflight(new("v4", [], data, new("charter-exception", "charter", "reason", "risk", []), "hash", [], policy, package));
        manifest.CleanupCompleted([]);
        var result = JsonSerializer.SerializeToElement(manifest.Finalise("passed"),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        // The launch package still identifies the revision; only the probe run's own package is a charter claim.
        Assert.Equal(commit, result.GetProperty("product").GetProperty("revision").GetString());
        var charter = result.GetProperty("charter");
        Assert.Equal(probeCharter ? "approved" : "draft", charter.GetProperty("status").GetString());
        Assert.Equal(probeCharter ? "launch-idle-exit-probe-only" : null, charter.GetProperty("scope").GetString());
        Assert.Equal(probeCharter ? 2 : 0, charter.GetProperty("approvals").GetArrayLength());
        var gate = result.GetProperty("executionGates")[0];
        Assert.Equal(("G0", probeCharter ? "passed-lite" : "blocked", probeCharter ? "approved-probe-charter" : "no-applicable-charter-package"),
            (gate.GetProperty("id").GetString(), gate.GetProperty("status").GetString(), gate.GetProperty("reason").GetString()));
        if (!probeCharter) Assert.DoesNotContain("launch-idle-exit-probe-only", result.GetRawText());
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
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(manifest.Finalise("failed", "manual")));
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
        Assert.Equal(3, json.RootElement.GetProperty("resultBundles")[0].GetProperty("Gates").GetArrayLength());
        Assert.Equal("blocked", json.RootElement.GetProperty("scenarios")[0].GetProperty("Status").GetString());
        Assert.Equal("v4", json.RootElement.GetProperty("contract").GetString());
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
                     "app-logs-cache-temp | isolation location",
                     "recent-document-links | shell record location",
                     "owned-processes | registered image and its descendants" })
            Assert.Contains(expected, bases);
        Assert.DoesNotContain(scope, l => l.Id is "app-tsf-composition-window" or "app-gpu-driver-timestamps" or "E6-os-save-dialog-history");
        var blind = Gcam.Studio.UiTests.Harness.Isolation.ObservationScope.BlindSpots(isolation);
        foreach (string id in new[] { "framework-registry", "framework-cache-shell", "framework-crash", "shell record jump-lists", "shell record recent-documents-registry" })
            Assert.Contains(blind, b => b.StartsWith(id + ": ", StringComparison.Ordinal));
        using var window = JsonDocument.Parse(JsonSerializer.Serialize(
            Gcam.Studio.UiTests.Harness.Isolation.ObservationScope.ControlWindow(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(5))));
        Assert.Equal(("reference-only", false), (window.RootElement.GetProperty("rule").GetString(), window.RootElement.GetProperty("shorterThanAppWindow").GetBoolean()));
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
