using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class VerdictMutationTests
{
    [Fact]
    public void Mutation_ProvesApplicationAndExpectedFailure_WithoutWeakeningTolerance()
    {
        Assert.Equal(13.4, VerdictMutation.Apply(13.4, .2, false));
        double changed = VerdictMutation.Apply(13.4, .2, true);
        Assert.Equal(14.4, changed); VerdictMutation.Verify(13.4, changed, 13.4, .2);
        Assert.Throws<InvalidOperationException>(() => VerdictMutation.Apply(13.4, 1.1, true));
        Assert.Throws<InvalidOperationException>(() => VerdictMutation.Verify(13.4, 13.4, 13.4, .2));
        Assert.Throws<InvalidOperationException>(() => VerdictMutation.Verify(13.4, 14.4, 14.4, .2));
    }
    [Fact]
    public void SkipConditions_IdentifyTheIndependentClosedGate()
    {
        Assert.Equal("desktop-opt-in-off", DesktopFactAttribute.ConditionId(false, true, true));
        Assert.Equal("long-desktop-opt-in-off", DesktopFactAttribute.ConditionId(true, false, true));
        Assert.Null(DesktopFactAttribute.ConditionId(true, true, true));
        Assert.Null(DesktopFactAttribute.ConditionId(true, false, false));
    }
}
