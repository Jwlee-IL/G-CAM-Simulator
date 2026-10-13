using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class ActionPolicyTests
{
    [Fact]
    public void CurrentPolicy_MatchesBothDirectionsIncludingDynamicAndCodeCreatedTargets()
    {
        string repo = RepoPaths.Root();
        var policy = ActionPolicy.Load(Path.Combine(repo, "tests/Gcam.Studio.UiTests/Harness/Isolation/action-policy.json"));
        var targets = AutomationTargets.Read(repo);
        var difference = policy.Compare(targets.Keys);
        Assert.Empty(difference.New); Assert.Empty(difference.Removed);
        Assert.Equal("in-memory-scene-delete", policy.Require("RemoveSource", "invoke").Effect);
        Assert.Equal("in-memory-acquisition-reset", policy.Require("Reset.Discard", "invoke").Effect);
        Assert.Equal("in-memory-measurement-delete", policy.Require("M2.Delete", "invoke").Effect);
        policy.VerifyControlType("SourceX", "Edit");
        Assert.Throws<InvalidOperationException>(() => policy.VerifyControlType("SourceX", "Button"));
    }
    [Fact]
    public void UnknownRemovedRenamedWrongTypeOrUnapprovedTargets_FailClosed()
    {
        var entry = new ActionPolicy.Entry("Known", "view", "in-memory", "allow", ["invoke"], ["Button"]);
        var policy = new ActionPolicy(new(1, "draft", [entry]));
        Assert.Throws<InvalidOperationException>(() => policy.Require("New", "invoke"));
        Assert.Throws<InvalidOperationException>(() => policy.Require("Known", "set"));
        Assert.Throws<InvalidOperationException>(() => policy.Require("Known", "invoke", "TextBox"));
        Assert.Throws<InvalidOperationException>(() => policy.Require("Known", "invoke", qualified: true));
        var difference = policy.Compare(["Renamed"]);
        Assert.Equal(new[] { "Renamed" }, difference.New); Assert.Equal(new[] { "Known" }, difference.Removed);
        Assert.Throws<InvalidDataException>(() => new ActionPolicy(new(1, "approved", [entry, entry])));
        PointerBoundary.Validate(true, true, true);
        Assert.Throws<InvalidOperationException>(() => PointerBoundary.Validate(false, true, true));
        Assert.Throws<InvalidOperationException>(() => PointerBoundary.Validate(true, false, true));
        Assert.Throws<InvalidOperationException>(() => PointerBoundary.Validate(true, true, false));
    }
}
