namespace Gcam.Masks;

/// <summary>
/// Generates Modified Uniformly Redundant Array (MURA) coded-aperture patterns.
/// Defined for a prime rank p (Gottesman &amp; Fenimore, 1989). The decoding array
/// G paired with the aperture yields a delta-function periodic autocorrelation,
/// which is what makes the cross-correlation reconstruction clean.
/// </summary>
public static class MuraGenerator
{
    /// <summary>Builds the p×p MURA basic pattern for a prime <paramref name="rank"/>.</summary>
    public static MaskPattern Basic(int rank)
    {
        if (!IsPrime(rank))
            throw new ArgumentException($"MURA rank must be prime; got {rank}.", nameof(rank));

        var qr = QuadraticResidues(rank);
        var pattern = new MaskPattern(rank, rank);
        for (int x = 0; x < rank; x++)
            for (int y = 0; y < rank; y++)
                pattern[x, y] = IsOpen(x, y, qr);
        return pattern;
    }

    /// <summary>Tiles the basic pattern into a mosaic (typically 2×2) for cyclic decoding.</summary>
    public static MaskPattern Mosaic(int rank, int tilesX, int tilesY)
    {
        var basic = Basic(rank);
        var mosaic = new MaskPattern(rank * tilesX, rank * tilesY);
        for (int x = 0; x < mosaic.Width; x++)
            for (int y = 0; y < mosaic.Height; y++)
                mosaic[x, y] = basic[x % rank, y % rank];
        return mosaic;
    }

    /// <summary>
    /// The decoding array G used in the reconstruction:
    /// +1 where the basic pattern is open, -1 where closed, except G[0,0] = +1.
    /// </summary>
    public static int[,] DecodingArray(int rank)
    {
        var basic = Basic(rank);
        var g = new int[rank, rank];
        for (int x = 0; x < rank; x++)
            for (int y = 0; y < rank; y++)
                g[x, y] = basic[x, y] ? 1 : -1;
        g[0, 0] = 1;
        return g;
    }

    // Gottesman–Fenimore MURA construction.
    private static bool IsOpen(int x, int y, HashSet<int> qr)
    {
        if (x == 0) return false;          // first column closed
        if (y == 0) return true;           // first row (except [0,0]) open
        return C(x, qr) * C(y, qr) == 1;
    }

    /// <summary>Quadratic-residue indicator: +1 if k is a QR mod p, -1 otherwise (k != 0).</summary>
    private static int C(int k, HashSet<int> qr) => qr.Contains(k) ? 1 : -1;

    private static HashSet<int> QuadraticResidues(int p)
    {
        var set = new HashSet<int>();
        for (int x = 1; x < p; x++)
            set.Add((x * x) % p);
        return set;
    }

    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        for (int d = 2; (long)d * d <= n; d++)
            if (n % d == 0) return false;
        return true;
    }
}
