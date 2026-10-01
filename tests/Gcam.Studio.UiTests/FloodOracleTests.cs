using System.Windows;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

/// <summary>The pilot's oracle and verdict are test code too — these run everywhere, no desktop needed.</summary>
public class FloodOracleTests
{
    // 30 cells in a 400×350 view → 11 px per cell, a 330 px image centred in the view.
    private static readonly FloodOracle Oracle = new(new Rect(100, 200, 400, 350), 30, 0.6);

    [Fact]
    public void ImageBounds_UseWholePixelsPerCell_Centred()
    {
        Assert.Equal(11, Oracle.CellPx);
        Assert.Equal(new Rect(135, 210, 330, 330), Oracle.ImageBounds);
    }

    [Fact]
    public void ScreenToMm_MapsImageCornersToDetectorEdges_YUp()
    {
        Assert.Equal((-9.0, 9.0), Oracle.ScreenToMm(new Point(135, 210)));
        Assert.Equal((9.0, -9.0), Oracle.ScreenToMm(new Point(465, 540)));
        var centre = Oracle.ScreenToMm(new Point(300, 375));
        Assert.Equal(0, centre.X, 9);
        Assert.Equal(0, centre.Y, 9);
    }

    [Fact]
    public void DistanceMm_FullDiagonal_IsSpanTimesRootTwo()
    {
        Assert.Equal(18 * Math.Sqrt(2), Oracle.DistanceMm(new Point(135, 210), new Point(465, 540)), 9);
    }

    [Fact]
    public void Verdict_RejectsAValueOutsideTheTolerance()
    {
        double expected = 10;
        Assert.True(Verdict.Within(10 + Oracle.ToleranceMm * 0.9, expected, Oracle.ToleranceMm));
        Assert.False(Verdict.Within(10 + Oracle.ToleranceMm * 1.1, expected, Oracle.ToleranceMm));
        Assert.False(Verdict.Within(11, expected, Oracle.ToleranceMm));   // the pilot's "break the verdict" offset
    }

    [Fact]
    public void MeasurementRow_ParsesTheAutomationName()
    {
        var row = MeasurementRow.Parse($"M1 Distance on Flood: {12.3:F1} mm");
        Assert.Equal(("M1", "Distance", "Flood"), (row.Name, row.Kind, row.Pane));
        Assert.Equal(12.3, row.LengthMm(), 9);
        Assert.Throws<FormatException>(() => MeasurementRow.Parse("Gcam.Studio.Core.ViewModels.MeasurementViewModel"));
        Assert.Throws<FormatException>(() => MeasurementRow.Parse("M2 Angle on Recon: 45.0°").LengthMm());
    }
}
