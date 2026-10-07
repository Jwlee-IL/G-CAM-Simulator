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
    public void CellCentreMm_IsAbsolute_WithYUp()
    {
        Assert.Equal((-8.7, 8.7), Round(Oracle.CellCentreMm(0, 0)));      // top-left cell
        Assert.Equal((8.7, -8.7), Round(Oracle.CellCentreMm(29, 29)));    // bottom-right cell
        Assert.Equal((-6.3, 5.1), Round(Oracle.CellCentreMm(4, 6)));
        static (double, double) Round((double X, double Y) p) => (Math.Round(p.X, 6), Math.Round(p.Y, 6));
    }

    [Fact]
    public void ReadoutAndSumParsers_ReadTheAppsFormats()
    {
        Assert.Equal((-6.3, 5.1, 0.01067), Verdict.ParseReadout("x -6.3 mm, y 5.1 mm · 0.01067"));
        // Current HeatmapView formats: unit suffix, grouped four-significant-digit values.
        Assert.Equal((0.3, 0.3, 10), Verdict.ParseReadout("x 0.3 mm, y 0.3 mm · 10 counts"));
        Assert.Equal((0.0, 0.0, 3077), Verdict.ParseReadout("x 0.0 mm, y 0.0 mm · 3,077 (decoded)"));
        Assert.Equal((-6.3, 5.1, 18.53), Verdict.ParseReadout("x -6.3 mm, y 5.1 mm · 18.53 (decoded)"));
        Assert.Equal((1.5, -2.1, -0.275), Verdict.ParseReadout("x 1.5 mm, y -2.1 mm · -0.275"));
        Assert.Throws<FormatException>(() => Verdict.ParseReadout("x 1.5 mm, y -2.1 mm · counts"));
        Assert.Equal(0.0107, Verdict.ParseSum($"Σ {0.0107:G3}"), 9);
        Assert.Throws<FormatException>(() => Verdict.ParseReadout(""));
        Assert.Throws<FormatException>(() => Verdict.ParseSum("—"));
    }

    [Fact]
    public void ReadoutUnit_NamesTheImageKind_IncludingMlem()
    {
        // TODO-38 / DC-3: the MLEM reconstruction's readout suffix (TODO-36, SR-IMG-07); the value still parses.
        Assert.Equal("(MLEM λ)", Verdict.ParseReadoutUnit("x 0.0 mm, y 0.0 mm · 9.266 (MLEM λ)"));
        Assert.Equal((0.0, 0.0, 9.266), Verdict.ParseReadout("x 0.0 mm, y 0.0 mm · 9.266 (MLEM λ)"));
        Assert.Equal("(decoded)", Verdict.ParseReadoutUnit("x -6.3 mm, y 5.1 mm · 3,077 (decoded)"));
        Assert.Equal("counts", Verdict.ParseReadoutUnit("x 0.3 mm, y 0.3 mm · 10 counts"));
        Assert.Throws<FormatException>(() => Verdict.ParseReadoutUnit("x -6.3 mm, y 5.1 mm · 0.01067"));
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
