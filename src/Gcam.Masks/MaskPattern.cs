namespace Gcam.Masks;

/// <summary>
/// A 2D binary coded-aperture pattern.
/// <c>true</c> = open cell (hole), <c>false</c> = closed cell (absorber).
/// Indexed as [x, y] with x the column and y the row.
/// </summary>
public sealed class MaskPattern
{
    private readonly bool[] _cells;

    public int Width { get; }
    public int Height { get; }

    public MaskPattern(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new bool[width * height];
    }

    public bool this[int x, int y]
    {
        get => _cells[y * Width + x];
        set => _cells[y * Width + x] = value;
    }

    /// <summary>Fraction of open cells (ideal MURA ≈ 0.5).</summary>
    public double OpenFraction()
    {
        int open = 0;
        foreach (var c in _cells)
            if (c) open++;
        return (double)open / _cells.Length;
    }
}
