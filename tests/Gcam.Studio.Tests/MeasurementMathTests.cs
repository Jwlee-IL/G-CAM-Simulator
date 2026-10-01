using Gcam.Core;
using Gcam.Studio.Core.Imaging;

namespace Gcam.Studio.Tests;

public class MeasurementMathTests
{
    [Fact]
    public void Distance_IsEuclidean()
    {
        Assert.Equal(5, MeasurementMath.Distance(new Vec2(1, 1), new Vec2(4, 5)), 12);
    }

    [Fact]
    public void AngleDeg_MeasuresAtTheVertex()
    {
        var vertex = new Vec2(2, 2);
        Assert.Equal(90, MeasurementMath.AngleDeg(new Vec2(5, 2), vertex, new Vec2(2, 7)), 9);
        Assert.Equal(45, MeasurementMath.AngleDeg(new Vec2(3, 2), vertex, new Vec2(4, 4)), 9);
        Assert.Equal(180, MeasurementMath.AngleDeg(new Vec2(0, 2), vertex, new Vec2(4, 2)), 9);
    }

    [Fact]
    public void AngleDeg_DegenerateArm_IsZero()
    {
        Assert.Equal(0, MeasurementMath.AngleDeg(new Vec2(1, 1), new Vec2(1, 1), new Vec2(3, 4)));
    }

    // 4×4 image, value = 10·y + x, pixel i centred at -1.5 + i mm (step 1).
    private static DetectorImage Ramp()
    {
        var img = new DetectorImage(4, 4);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                img[x, y] = 10 * y + x;
        return img;
    }

    [Fact]
    public void Roi_CountsPixelsByCentre_CornersInAnyOrder()
    {
        // x ∈ [-1, 1] covers centres -0.5, 0.5 → x = 1, 2; y ∈ [0, 2] covers centres 0.5, 1.5 → y = 2, 3.
        var s = MeasurementMath.Roi(Ramp(), new Vec2(1, 2), new Vec2(-1, 0), -1.5, 1);
        Assert.Equal(4, s.Pixels);
        Assert.Equal(21 + 22 + 31 + 32, s.Sum);
        Assert.Equal(26.5, s.Mean);
        Assert.Equal(32, s.Max);
    }

    [Fact]
    public void Roi_ClipsToTheImage_AndIsEmptyBetweenCentres()
    {
        var all = MeasurementMath.Roi(Ramp(), new Vec2(-100, -100), new Vec2(100, 100), -1.5, 1);
        Assert.Equal(16, all.Pixels);

        var none = MeasurementMath.Roi(Ramp(), new Vec2(0.1, 0.1), new Vec2(0.4, 0.4), -1.5, 1);
        Assert.Equal(0, none.Pixels);
        Assert.Equal(0, none.Sum);
    }
}
