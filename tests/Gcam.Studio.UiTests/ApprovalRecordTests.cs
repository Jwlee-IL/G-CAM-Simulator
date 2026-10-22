using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class ApprovalRecordTests
{
    [Fact]
    public void Approval_RequiresActualStatementActTargetAndNonOlderDate()
    {
        string commit = new('a', 40); var at = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var record = new ApprovalRecord("fixture", "HUM-07", commit, "Synthetic test statement", at);
        record.Validate("HUM-07", commit, at);
        Assert.Throws<InvalidDataException>(() => record.Validate("HUM-04", commit, at));
        Assert.Throws<InvalidDataException>(() => record.Validate("HUM-07", new string('b', 40), at));
        Assert.Throws<InvalidDataException>(() => record.Validate("HUM-07", commit, at.AddSeconds(1)));
        Assert.Throws<InvalidDataException>(() => (record with { Quote = "" }).Validate("HUM-07", commit, at));
    }
    [Fact]
    public void ProbePackage_RequiresCharterAndDesktopGoAndExactFingerprints_WithoutPermissionExpansion()
    {
        string commit = new('a', 40); var at = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var acts = new[] { "charter", "desktop-go" }.Select(a => new ApprovalRecord(a, a, commit, "Synthetic test statement", at)).ToArray();
        var package = new ProbePackage("fixture", commit, at, "isolation", "deployment", acts);
        package.Validate("fixture", "isolation", "deployment");
        package.ValidateSource(commit, at);
        Assert.Throws<InvalidDataException>(() => package.ValidateSource(commit, at.AddSeconds(1)));
        Assert.Throws<InvalidDataException>(() => package.Validate("fixture", "changed", "deployment"));
        Assert.Throws<InvalidDataException>(() => (package with { Approvals = [] }).Validate("fixture", "isolation", "deployment"));
    }

    [Fact]
    public void LegacyActs_AreReadButLiveRecordsUseNames_AndUnknownContractsRefuse()
    {
        string commit = new('a', 40); var at = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var package = new ProbePackage("fixture", commit, at, "i", "d",
            [new("go", "HUM-01", commit, "Synthetic go", at), new("charter", "HUM-04", commit, "Synthetic charter", at)]);
        package.Validate("fixture", "i", "d");
        Assert.Equal(new[] { "desktop-go", "charter" }, package.LiveApprovals.Select(a => a.HumanActId));
        var additionalLegacyCharter = package with { Approvals = [.. package.Approvals, new("extra-charter", "HUM-09", commit, "Synthetic historical charter", at)] };
        additionalLegacyCharter.Validate("fixture", "i", "d");
        Assert.Equal(new[] { "desktop-go", "charter" }, additionalLegacyCharter.LiveApprovals.Select(a => a.HumanActId));
        Assert.Throws<InvalidDataException>(() => (additionalLegacyCharter with
            { Approvals = additionalLegacyCharter.Approvals.Where(a => a.HumanActId != "HUM-04").ToArray() }).Validate("fixture", "i", "d"));
        Assert.Throws<InvalidDataException>(() => (package with { Contract = "v5" }).Validate("fixture", "i", "d"));
        Assert.Throws<InvalidDataException>(() => (package with { Contract = "v4" }).Validate("fixture", "i", "d"));
        Assert.Throws<InvalidDataException>(() => (package with { Approvals = [.. package.Approvals, package.Approvals[0]] }).Validate("fixture", "i", "d"));
    }

    [Fact]
    public void E6_BindsOnlyApprovedScenariosNamesAndFamilies_AndRejectsMissingOrStaleScope()
    {
        string commit = new('a', 40); var at = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var package = new ProbePackage("gcam-batch-fixture", commit, at, "i", new string('b', 64),
            [new("go", "desktop-go", commit, "Synthetic go", at), new("charter", "charter", commit, "Synthetic charter", at),
             new("effect", "real-world-effect", commit, "Synthetic permission naming this run's export shell records", at)])
        {
            Contract = "v4",
            RealWorldEffects = [new("E6", "effect", "gcam-batch-fixture", commit, new string('b', 64), ["export"],
                ["report-{runId}.html"], ProbePackage.ShellFamilies)],
        };
        const string run = "gcam-20261022-100000-abcdef";
        var grant = Assert.Single(package.BindEffects(run, "export", [$"report-{run}.html", $"report-{run}.scene.json"]));
        Assert.Equal(new[] { $"report-{run}.html" }, grant.TypedNames);
        Assert.Equal(("gcam-batch-fixture", run, "export", "effect"), (grant.RunId, grant.ScenarioRunId, grant.ScenarioId, grant.Approval.Id));
        Assert.Empty(package.BindEffects(run, "idle", []));
        var invented = package with { Contract = null, Approvals = package.Approvals.Select(a =>
            a.Id == "effect" ? a with { HumanActId = "HUM-02" } : a).ToArray() };
        Assert.Throws<InvalidDataException>(() => invented.BindEffects(run, "export", [$"report-{run}.html"]));
        Assert.Throws<InvalidDataException>(() => package.BindEffects(run, "other-scenario", [$"report-{run}.html"]));
        Assert.Throws<InvalidDataException>(() => package.BindEffects(run, "export", [$"report-{run}.csv"]));
        foreach (var effect in new[] { package.RealWorldEffects[0] with { RunId = "other" },
                     package.RealWorldEffects[0] with { TargetCommit = new string('c', 40) },
                     package.RealWorldEffects[0] with { RecordFamilies = ["all-shell-records"] },
                     package.RealWorldEffects[0] with { TypedNameTemplates = ["*"] },
                     package.RealWorldEffects[0] with { ApprovalId = "missing" } })
            Assert.Throws<InvalidDataException>(() => (package with { RealWorldEffects = [effect] }).BindEffects(run, "export", [$"report-{run}.html"]));
    }

    [Fact]
    public void V4Packages_RequireProductUniqueRunIds_AndLegacyPackagesKeepTheirOriginalIds()
    {
        string commit = new('a', 40); var at = DateTimeOffset.Parse("2026-10-22T00:00:00Z");
        var package = new ProbePackage("probe-20261022-01", commit, at, "i", "d",
            [new("go", "desktop-go", commit, "Synthetic go", at), new("charter", "charter", commit, "Synthetic charter", at)]);
        package.Validate(package.RunId, "i", "d");
        var v4 = package with { Contract = "v4" };
        Assert.Contains("must begin with gcam-", Assert.Throws<InvalidDataException>(() => v4.Validate(v4.RunId, "i", "d")).Message);
        v4 = v4 with { RunId = "gcam-probe-20261022-01" };
        v4.Validate(v4.RunId, "i", "d");
    }

    [Theory]
    [InlineData("HUM-02")]
    [InlineData("HUM-07")]
    [InlineData("HUM-09")]
    [InlineData("HUM-13")]
    [InlineData("HUM-16")]
    [InlineData("HUM-17")]
    public void OtherHistoricalIdentifiers_DeserializeWithoutGrantingAnyNamedAct(string id)
    {
        var record = System.Text.Json.JsonSerializer.Deserialize<ApprovalRecord>(
            $$"""{"Id":"fixture","HumanActId":"{{id}}","TargetCommit":"a","Quote":"historical","QuotedAt":"2026-10-14T00:00:00Z"}""")!;
        Assert.Equal(id, record.HumanActId);
        Assert.Null(ApprovalRecord.ActName(id));
        Assert.All(ApprovalRecord.Acts, act => Assert.False(record.Covers(act)));
    }
}
