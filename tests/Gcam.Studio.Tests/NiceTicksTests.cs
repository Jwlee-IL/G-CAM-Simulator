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
}
