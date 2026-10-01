namespace Gcam.Core;

/// <summary>
/// A 2D grid of accumulated detector response (counts or deposited energy) —
/// i.e. the flood map read out from the crystal array.
/// </summary>
public sealed class DetectorImage
{
    private readonly double[] _data;
    private bool _isReadOnly;

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
        set { EnsureWritable(); _data[y * Width + x] = value; }
    }

    public void Add(int x, int y, double value) { EnsureWritable(); _data[y * Width + x] += value; }

    /// <summary>A detached immutable image for acquisition snapshots. Existing images stay writable.</summary>
    public DetectorImage ReadOnlyCopy()
    {
        var copy = new DetectorImage(Width, Height);
        _data.CopyTo(copy._data, 0);
        copy._isReadOnly = true;
        return copy;
    }

    private void EnsureWritable()
    {
        if (_isReadOnly) throw new InvalidOperationException("Snapshot images are immutable.");
    }

    /// <summary>Flat row-major view of the underlying buffer.</summary>
    public ReadOnlySpan<double> Raw => _data;
}
