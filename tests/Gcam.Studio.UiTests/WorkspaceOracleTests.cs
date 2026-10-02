using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class WorkspaceOracleTests
{
    [Fact]
    public void BandCount_IncludesBoundaryCentres() => Assert.Equal(7, WorkspaceOracle.BandCount([1, 2, 3, 4], [1, 3, 4, 8], 2, 3));

    [Fact]
    public void EventsInWindow_IncludesStartExcludesEnd() => Assert.Equal(new[] { 0, 1 }, WorkspaceOracle.EventsInWindow([0, 2e-6, 10e-6], 1, 10));

    [Fact]
    public void HalfMax_InterpolatesAndCensors()
    {
        Assert.Equal((15d, 25d, false, false), WorkspaceOracle.HalfMax([10, 20, 30], [0, 8, 0]));
        Assert.Equal((10d, 30d, true, true), WorkspaceOracle.HalfMax([10, 20, 30], [6, 8, 6]));
    }
}
