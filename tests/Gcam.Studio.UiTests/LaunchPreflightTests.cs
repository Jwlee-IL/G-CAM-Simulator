using System.Text.Json;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class LaunchPreflightTests
{
    private static OwnedStudioProcess.PeerBoundary Peers => new("charter-exception", "charter-fixture", "No equivalent ordinary-user block",
        "Native connections remain possible", []);
    private static ContentFingerprint.DataCheck Data => new("v4", "list-hash", "deployment-hash", [".dll", ".exe", ".json"], [], false,
        new("harness-typed-scenes", "synthetic", "fixture-generator"), "fixture-scenario", "assembly-hash");

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void FailedCheck_NeverCallsTheStartDelegate(int failed)
    {
        var called = new List<int>(); int starts = 0;
        void Check(int index) { called.Add(index); if (index == failed) throw new InvalidDataException("fixture check " + index); }
        var checks = new OwnedStudioProcess.PreflightChecks(() => { Check(0); return Data; }, () => Check(1), () => Check(2),
            () => { Check(3); return Peers; }, () => { Check(4); return new(new(1, "draft", [])); });
        Assert.Throws<InvalidDataException>(() => OwnedStudioProcess.StartAfterPreflight(checks, _ => ++starts));
        Assert.Equal(0, starts);
        Assert.Equal(Enumerable.Range(0, failed + 1), called);
    }

    [Fact]
    public void SuccessfulPreflight_PassesTheSamePolicyAndFiveChecksToStart()
    {
        var policy = new ActionPolicy(new(1, "draft", []), new string('a', 64));
        var checks = new OwnedStudioProcess.PreflightChecks(() => Data, () => { }, () => { }, () => Peers, () => policy);
        var result = OwnedStudioProcess.StartAfterPreflight(checks, r => r);
        Assert.Same(policy, result.Policy);
        Assert.Equal(new[] { "test-data", "resolved-write-surfaces", "launch-values", "peer-boundary", "action-policy" }, result.Checks.Select(c => c.Id));
        Assert.All(result.Checks, c => Assert.Equal("passed", c.Status));
        var json = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal("v4", json.GetProperty("contract").GetString());
        Assert.False(json.TryGetProperty("policy", out _));
        Assert.Equal(policy.DocumentSha256, json.GetProperty("actionPolicySha256").GetString());
        Assert.Throws<InvalidDataException>(() => OwnedStudioProcess.StartAfterPreflight(checks with
            { PeerBoundary = () => Peers with { Status = "blocked-without-proof" } }, _ => true));
    }

    [Fact]
    public void ResolvedSurfaces_RejectAnExportOrEnvironmentPathOutsideIsolation()
    {
        string root = IsolationPathsTests.Scratch();
        var values = IsolationPaths.LaunchValues(root, "fixture").ToDictionary(p => p.Key, p => p.Value);
        OwnedStudioProcess.ValidateResolvedSurfaces(root, values, ["report.html"]);
        Assert.Throws<ArgumentException>(() => OwnedStudioProcess.ValidateResolvedSurfaces(root, values, ["../outside.html"]));
        values["TEMP"] = Path.GetTempPath();
        Assert.Throws<ArgumentException>(() => OwnedStudioProcess.ValidateResolvedSurfaces(root, values, []));
    }

    [Fact]
    public void DeployedInputs_MatchThePinnedFileSetAndEmbeddedModel_BeforeLaunch()
    {
        string isolation = Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation");
        var (data, peers) = ContentFingerprint.CheckData(Path.GetDirectoryName(RepoPaths.StudioExe())!, Path.Combine(isolation, "test-data.json"), "headless-fixture");
        Assert.False(data.UserImportSlot);
        Assert.Equal(new[] { ".dll", ".exe", ".json" }, data.FingerprintCoverage);
        Assert.Equal("headless-fixture", data.ScenarioId);
        Assert.Equal(26, data.DeploymentFiles.Length);
        Assert.Contains("ambient/terrestrial-unscear2000-v1.json", data.DeploymentFiles);
        Assert.Equal("harness-typed-scenes", data.TypedScenes.Id);
        peers.Validate();
    }

    [Theory]
    [InlineData("unexpected-json")] [InlineData("unexpected-csv")] [InlineData("missing")]
    [InlineData("hash")] [InlineData("deployment-pin")] [InlineData("user-import")]
    public void DataDrift_RefusesBeforeTheStartDelegate(string defect)
    {
        string root = Path.Combine(Path.GetTempPath(), "gcam-todo54", "data-fixture-" + Guid.NewGuid().ToString("N"));
        string deployment = Directory.CreateDirectory(Path.Combine(root, "app")).FullName;
        byte[] bytes = "synthetic"u8.ToArray();
        File.WriteAllBytes(Path.Combine(deployment, "model.json"), bytes);
        var list = new ContentFingerprint.DataList("v4", defect == "user-import", ["model.json"],
            [new("model.json", defect == "hash" ? new string('0', 64) : IsolationPaths.Hash(bytes))], [], Data.TypedScenes, Peers);
        string listPath = Path.Combine(root, "test-data.json");
        File.WriteAllText(listPath, JsonSerializer.Serialize(list));
        if (defect == "unexpected-json") File.WriteAllText(Path.Combine(deployment, "other.json"), "unexpected");
        if (defect == "unexpected-csv") File.WriteAllText(Path.Combine(deployment, "other.csv"), "unexpected");
        if (defect == "missing") list = list with { DeploymentFiles = ["missing.json"] };
        File.WriteAllText(listPath, JsonSerializer.Serialize(list));
        int starts = 0;
        var checks = new OwnedStudioProcess.PreflightChecks(
            () => ContentFingerprint.CheckData(deployment, listPath, "fixture", defect == "deployment-pin" ? "wrong" : null).Data,
            () => { }, () => { }, () => Peers, () => new(new(1, "draft", [])));
        Assert.Throws<InvalidDataException>(() => OwnedStudioProcess.StartAfterPreflight(checks, _ => ++starts));
        Assert.Equal(0, starts);
    }
}
