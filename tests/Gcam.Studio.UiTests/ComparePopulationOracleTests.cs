using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class ComparePopulationOracleTests
{
    [Fact]
    public void DisplayedRows_RecomputeHeldEligibleAndRoundedShare()
    {
        var p = ComparePopulationOracle.Read(["Histories / held", "1,000 / 900", "Assigned / unknown", "800 / 100", "Same / eligible", "650 / 700"]);
        Assert.Equal(50, p.Different); Assert.Equal(650.0 / 700, p.Share);
        string shown = "Same crystal " + (650.0 / 700).ToString("P2");
        Assert.True(ComparePopulationOracle.Consistent(p, shown));
        Assert.False(ComparePopulationOracle.Consistent(p with { Unknown = 99 }, shown));
        Assert.False(ComparePopulationOracle.Consistent(p with { Same = 701 }, shown));
        Assert.False(ComparePopulationOracle.Consistent(p, "Same crystal " + 0.5.ToString("P2")));
    }
    [Fact]
    public void EmptyDenominator_IsUnavailable_AndMalformedOrNegativeRowsAreRefused()
    {
        var p = new ComparePopulationOracle.Population(0, 0, 0, 0, 0, 0);
        Assert.Null(p.Share);
        Assert.True(ComparePopulationOracle.Consistent(p, "Same crystal unavailable: no eligible conversions"));
        Assert.False(ComparePopulationOracle.Consistent(p, "Same crystal 0.00%"));
        Assert.False(ComparePopulationOracle.Consistent(p with { Same = -1 }, "Same crystal unavailable: no eligible conversions"));
        Assert.Throws<InvalidDataException>(() => ComparePopulationOracle.Read(["Histories / held", "bad"]));
    }
}
