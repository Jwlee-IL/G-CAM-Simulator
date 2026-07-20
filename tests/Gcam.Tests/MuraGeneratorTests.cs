using Gcam.Masks;
using Xunit;

namespace Gcam.Tests;

public class MuraGeneratorTests
{
    [Fact]
    public void Rank7_HasCharacteristicMuraSignature()
    {
        var p = MuraGenerator.Basic(7);

        // First column (x = 0) is fully closed in the Gottesman–Fenimore construction.
        for (int y = 0; y < 7; y++)
            Assert.False(p[0, y]);

        // First row (y = 0), excluding [0,0], is fully open.
        for (int x = 1; x < 7; x++)
            Assert.True(p[x, 0]);
    }

    [Fact]
    public void Rank7_OpenFractionIsNearHalf()
    {
        var p = MuraGenerator.Basic(7);
        Assert.InRange(p.OpenFraction(), 0.40, 0.55);
    }

    [Fact]
    public void NonPrimeRank_Throws()
    {
        Assert.Throws<ArgumentException>(() => MuraGenerator.Basic(12));
    }

    [Fact]
    public void Mosaic2x2_TilesTheBasicPattern()
    {
        var basic = MuraGenerator.Basic(7);
        var mosaic = MuraGenerator.Mosaic(7, 2, 2);

        Assert.Equal(14, mosaic.Width);
        Assert.Equal(14, mosaic.Height);

        // Every cell repeats with period = rank in both axes.
        for (int x = 0; x < 7; x++)
            for (int y = 0; y < 7; y++)
                Assert.Equal(basic[x, y], mosaic[x + 7, y + 7]);
    }

    [Fact]
    public void DecodingArray_IsPlusOneAtOrigin()
    {
        var g = MuraGenerator.DecodingArray(7);
        Assert.Equal(1, g[0, 0]);
    }
}
