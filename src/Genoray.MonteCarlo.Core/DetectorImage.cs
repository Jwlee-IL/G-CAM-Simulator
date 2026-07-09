namespace Genoray.MonteCarlo.Core;

/// <summary>
/// A 2D grid of accumulated detector response (counts or deposited energy) —
/// i.e. the flood map read out from the crystal array.
/// </summary>
public sealed class DetectorImage
{
    private readonly double[] _data;

    public int Width { get; }
    public int Height { get; }

    public DetectorImage(int width, int height)
    {
        Width = width;
        Height = height;
        _data = new double[width * height];
    }

    public double this[int x, int y]
    {
        get => _data[y * Width + x];
        set => _data[y * Width + x] = value;
    }

    public void Add(int x, int y, double value) => _data[y * Width + x] += value;

    /// <summary>Flat row-major view of the underlying buffer.</summary>
    public ReadOnlySpan<double> Raw => _data;
}
