using Gcam.Studio.Core.Plotting;

namespace Gcam.Studio.Tests;

public sealed class NiceTicksTests
{
    [Fact]
    public void Linear_Uses125Steps_EngineeringLabels_NoNegativeZero()
    {
        Assert.Contains("×10", Assert.Single(NiceTicks.Linear(9800, 12000, 2)).Label);
        foreach (double scale in new[] { 0.001, 1, 10000 })
        {
            var ticks = NiceTicks.Linear(-3 * scale, 17 * scale);
            double step = ticks[1].Value - ticks[0].Value;
            double fraction = step / Math.Pow(10, Math.Floor(Math.Log10(step)));
            Assert.Contains(Math.Round(fraction, 6), new[] { 1.0, 2, 5 });
            Assert.All(ticks, t => Assert.DoesNotContain("-0", t.Label));
            if (scale != 1) Assert.Contains("×10", ticks[^1].Label);
        }
    }

    [Fact]
    public void Logarithmic_LabelsDecades_WithEightMinorTicks()
    {
        var ticks = NiceTicks.Logarithmic(1, 100);
        Assert.Equal(new[] { 1.0, 10, 100 }, ticks.Where(t => t.IsMajor).Select(t => t.Value));
        Assert.Equal(16, ticks.Count(t => !t.IsMajor));
        Assert.All(ticks.Where(t => !t.IsMajor), t => Assert.Empty(t.Label));
        Assert.Empty(NiceTicks.Linear(0, 0));
    }

    [Fact]
    public void LogarithmicAxis_Labels125_InsideRange_FallsBackToLinearWhenNarrow()
    {
        // The focus sweep's plane range (D+30 = 110 mm to 3000 mm).
        var ticks = NiceTicks.LogarithmicAxis(110, 3000);
        Assert.Equal(new[] { 200.0, 500, 1000, 2000 }, ticks.Where(t => t.IsMajor).Select(t => t.Value));
        Assert.Equal(new[] { "200", "500", "1,000", "2,000" }, ticks.Where(t => t.IsMajor).Select(t => t.Label));
        Assert.All(ticks, t => Assert.InRange(t.Value, 110, 3000));
        Assert.All(ticks.Where(t => !t.IsMajor), t => Assert.Empty(t.Label));
        Assert.Contains(ticks, t => t.Value == 3000 && !t.IsMajor);
        var narrow = NiceTicks.LogarithmicAxis(250, 400);   // only 300 / 400 mantissas: linear steps instead
        Assert.Equal(NiceTicks.Linear(250, 400).Select(t => t.Value), narrow.Select(t => t.Value));
        Assert.Empty(NiceTicks.LogarithmicAxis(0, 10));
        Assert.Empty(NiceTicks.LogarithmicAxis(10, 10));
    }

    [Fact]
    public void Logarithmic_UsesGroupedNumbersThenSuperscriptPowers()
    {
        var major = NiceTicks.Logarithmic(1, 10_000_000).Where(t => t.IsMajor).ToArray();
        Assert.Equal(new[] { "1", "10", "100", "1,000", "10,000", "100,000", "10⁶", "10⁷" },
            major.Select(t => t.Label));
    }

    [Fact]
    public void Linear_ColourScaleTicksStayInsideDataRange()
    {
        Assert.Equal(new[] { 0.0, 10, 20 }, NiceTicks.Linear(0, 25, 5).Select(t => t.Value));
        var decoded = NiceTicks.Linear(-1027, 3077, 5);
        Assert.Equal(new[] { 0.0, 2000 }, decoded.Select(t => t.Value));
        Assert.All(decoded, t => Assert.InRange(t.Value, -1027, 3077));
        Assert.Equal("0", decoded[0].Label);
    }
}
