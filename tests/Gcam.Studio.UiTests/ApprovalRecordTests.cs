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
    public void ProbePackage_RequiresAllThreeActsAndExactFingerprints()
    {
        string commit = new('a', 40); var at = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var acts = new[] { "HUM-01", "HUM-04", "HUM-07" }.Select(a => new ApprovalRecord(a, a, commit, "Synthetic test statement", at)).ToArray();
        var package = new ProbePackage("fixture", commit, at, "isolation", "deployment", acts);
        package.Validate("fixture", "isolation", "deployment");
        package.ValidateSource(commit, at);
        Assert.Throws<InvalidDataException>(() => package.ValidateSource(commit, at.AddSeconds(1)));
        Assert.Throws<InvalidDataException>(() => package.Validate("fixture", "changed", "deployment"));
        Assert.Throws<InvalidDataException>(() => (package with { Approvals = [] }).Validate("fixture", "isolation", "deployment"));
    }
}
