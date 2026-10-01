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
    public void PixelsInside_CountsCentres_RobustToAPixelAtTheCorners()
    {
        Assert.Equal(1, Oracle.PixelsInside(Oracle.CellCorner(3, 3), Oracle.CellCorner(4, 4)));
        Assert.Equal(6 * 7, Oracle.PixelsInside(Oracle.CellCorner(8, 10), Oracle.CellCorner(14, 17)));
        Assert.Equal(6 * 7, Oracle.PixelsInside(Oracle.CellCorner(14, 17), Oracle.CellCorner(8, 10)));   // any order
        // One pixel of error at either corner changes nothing
        Assert.Equal(6 * 7, Oracle.PixelsInside(Oracle.CellCorner(8, 10) + new Vector(1, -1), Oracle.CellCorner(14, 17) + new Vector(-1, 1)));
    }

    [Fact]
    public void AngleDeg_IsTheVertexAngle_ToleranceShrinksWithLongerArms()
    {
        Assert.Equal(90, Verdict.AngleDeg(new Point(10, 0), new Point(0, 0), new Point(0, 10)), 9);
        Assert.Equal(45, Verdict.AngleDeg(new Point(10, 0), new Point(0, 0), new Point(10, -10)), 9);
        double shortArms = Verdict.AngleToleranceDeg(new Point(20, 0), new Point(0, 0), new Point(0, 20));
        double longArms = Verdict.AngleToleranceDeg(new Point(200, 0), new Point(0, 0), new Point(0, 200));
        Assert.True(longArms < shortArms / 5);   // ~1.6° at 200 px arms vs ~16° at 20 px
    }

    [Fact]
    public void Parsers_ReadPeakAndRoiDetail_RejectOtherText()
    {
        Assert.Equal((10.0, -0.2), Verdict.ParsePeak($"peak ({10.0:F1}, {-0.2:F1}) mm"));
        Assert.Equal((1.8, 2.4, 12), Verdict.ParseRoiDetail($"{1.8:F1} × {2.4:F1} mm · 12 px · mean 0.01 · max 0.02"));
        Assert.Throws<FormatException>(() => Verdict.ParsePeak("Cancelled — previous result kept"));
        Assert.Throws<FormatException>(() => Verdict.ParseRoiDetail($"{1.8:F1} × {2.4:F1} mm · no image yet"));
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
