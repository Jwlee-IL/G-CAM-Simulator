using Gcam.Simulation;

namespace Gcam.Tests;

public sealed class AmbientGateStudyTests
{
    [Fact]
    public void Thresholds_GreaterThan_IsTheTurn7RuleAndTheDefault()
    {
        // A threshold file without a comparison is turn 7's (Z > T*): a tie is not trusted.
        var t = new AmbientGateStudy.Thresholds();
        Assert.Equal("GreaterThan", t.Comparison);
        Assert.False(t.Trusts(1.2345, 1.2345));
        Assert.True(t.Trusts(1.23451, 1.2345));
        Assert.False(t.Trusts(double.NegativeInfinity, -1e300));
    }

    [Fact]
    public void Thresholds_RoundedAtLeast_CountsATieAsTrusted()
    {
        // AB-11: Z is compared at the precision it is recorded and selected at (4 decimals), and a tie is trusted, so the
        // validation counts exactly what the selection counted as an exceedance.
        var t = new AmbientGateStudy.Thresholds { Comparison = "RoundedAtLeast" };
        Assert.True(t.Trusts(1.2345, 1.2345));
        Assert.True(t.Trusts(1.23449, 1.2345));     // rounds to the threshold: a tie
        Assert.False(t.Trusts(1.23444, 1.2345));    // rounds below it
        Assert.True(t.Trusts(-3.0, -1e300));        // the no-candidate floor trusts any candidate
        Assert.False(t.Trusts(double.NegativeInfinity, -1e300));   // no candidate is never trusted
    }

    [Fact]
    public void Thresholds_UnknownComparison_IsRefused()
        => Assert.Throws<InvalidDataException>(() => new AmbientGateStudy.Thresholds { Comparison = "AtLeast" }.Trusts(1, 0));
}
