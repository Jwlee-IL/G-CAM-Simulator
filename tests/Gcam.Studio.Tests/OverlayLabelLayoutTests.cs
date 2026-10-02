using Gcam.Studio.Core.Imaging;

namespace Gcam.Studio.Tests;

public sealed class OverlayLabelLayoutTests
{
    [Fact]
    public void Arrange_PreservesUncrowdedPreferredPositions()
    {
        var preferred = new ScreenRect(40, 30, 80, 17);
        var result = OverlayLabelLayout.Arrange([(preferred, new(30, 20))], new(0, 0, 250, 250), [], 4);
        Assert.Equal(preferred, Assert.Single(result));
    }

    [Theory]
    [InlineData(245)]
    [InlineData(327)]
    public void Arrange_SeparatesEqualYLanes_AndAvoidsTruthMarkersAndMeasurement(double width)
    {
        ScreenRect bounds = new(0, 0, width, width);
        ScreenRect[] obstacles = [new(65, 105, 22, 22), new(140, 105, 22, 22), new(5, 130, 90, 17)];
        (ScreenRect, Vec2)[] labels = [(new(80, 91, 49, 17), new(76, 116)),
            (new(155, 91, 42, 17), new(151, 116)), (new(82, 125, 87, 17), new(76, 116)),
            (new(157, 125, 81, 17), new(151, 116))];
        var result = OverlayLabelLayout.Arrange(labels, bounds, obstacles, 4);
        var placed = result.Select(r => Assert.IsType<ScreenRect>(r)).ToArray();
        Assert.All(placed, r => Assert.True(bounds.Contains(r)));
        Assert.All(placed, r => Assert.All(obstacles, o => Assert.True(r.IsSeparatedFrom(o, 4))));
        for (int i = 0; i < placed.Length; i++)
            for (int j = i + 1; j < placed.Length; j++) Assert.True(placed[i].IsSeparatedFrom(placed[j], 4));
        Assert.Equal(result, OverlayLabelLayout.Arrange(labels, bounds, obstacles, 4));
    }

    [Fact]
    public void Arrange_ClampsEdgeLabel_AndRecalculatesForChangedBounds()
    {
        (ScreenRect, Vec2)[] labels = [(new(220, 230, 87, 17), new(215, 219))];
        var small = Assert.IsType<ScreenRect>(Assert.Single(OverlayLabelLayout.Arrange(labels, new(0, 0, 245, 245), [], 4)));
        var large = Assert.IsType<ScreenRect>(Assert.Single(OverlayLabelLayout.Arrange(labels, new(0, 0, 400, 400), [], 4)));
        Assert.Equal(158, small.X);
        Assert.Equal(228, small.Y);
        Assert.Equal(labels[0].Item1, large);
    }

    [Fact]
    public void Arrange_OmitsChip_WhenBoundsAreTooSmallOrFullyOccupied()
    {
        (ScreenRect, Vec2)[] labels = [(new(0, 0, 87, 17), new(0, 0))];
        Assert.Null(Assert.Single(OverlayLabelLayout.Arrange(labels, new(0, 0, 50, 50), [], 4)));
        Assert.Null(Assert.Single(OverlayLabelLayout.Arrange(labels, new(0, 0, 100, 100), [new(0, 0, 100, 100)], 4)));
    }

    [Fact]
    public void Arrange_RejectsNonFiniteGeometry()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OverlayLabelLayout.Arrange([], new(0, 0, double.NaN, 100), [], 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => OverlayLabelLayout.Arrange([], new(0, 0, 100, 100), [], -1));
    }
}
