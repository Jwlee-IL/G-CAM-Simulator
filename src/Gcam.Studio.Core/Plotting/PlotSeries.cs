namespace Gcam.Studio.Core.Plotting;

/// <summary>Finite samples on an increasing X axis. Arrays are immutable after publication.</summary>
public sealed record PlotSeries(string Name, double[] Y, double[]? X = null,
    double Origin = 0, double Step = 1, PlotKind Kind = PlotKind.Line, PlotColourRole ColourRole = PlotColourRole.Series1,
    double[]? BinEdges = null)
{
    public MinMaxPyramid? PreparedPyramid { get; init; }
    public const int MaximumSamples = 10_000_000;
    public double XAt(int index) => X is null ? Origin + index * Step : X[index];
    public double EdgeAt(int index) => BinEdges is null ? Origin + index * Step : BinEdges[index];

    /// <summary>Half-open bins; the final edge is outside the histogram.</summary>
    public int BinAt(double x)
    {
        if (!double.IsFinite(x) || Y.Length == 0 || x < EdgeAt(0) || x >= EdgeAt(Y.Length)) return -1;
        if (BinEdges is null) return Math.Min(Y.Length - 1, (int)Math.Floor((x - Origin) / Step));
        int lo = 0, hi = Y.Length;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (EdgeAt(mid + 1) <= x) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Y);
        if (Y.Length > MaximumSamples) throw new ArgumentOutOfRangeException(nameof(Y), "Scope input limit is 10 million samples.");
        if (!double.IsFinite(Origin) || !double.IsFinite(Step) || Step <= 0)
            throw new ArgumentOutOfRangeException(nameof(Step));
        if (X is not null && X.Length != Y.Length) throw new ArgumentException("X and Y lengths differ.");
        if (Kind == PlotKind.Histogram)
        {
            if (BinEdges is not null && BinEdges.Length != Y.Length + 1) throw new ArgumentException("Bins need N + 1 edges.");
            for (int i = 0; i <= Y.Length; i++)
                if (!double.IsFinite(EdgeAt(i)) || (i > 0 && EdgeAt(i) <= EdgeAt(i - 1)))
                    throw new ArgumentException("Bin edges must be finite and strictly increasing.");
            if (Y.Any(y => y < 0)) throw new ArgumentException("Histogram counts must be nonnegative.");
        }
        if (PreparedPyramid is not null && !PreparedPyramid.IsFor(Y))
            throw new ArgumentException("Prepared pyramid belongs to another array.");
        // A prepared pyramid already checked every Y value on the worker. Uniform X needs only its endpoint.
        if (PreparedPyramid is not null && X is null)
        {
            if (Y.Length > 0 && !double.IsFinite(XAt(Y.Length - 1))) throw new ArgumentException("X must be finite.");
            return;
        }
        for (int i = 0; i < Y.Length; i++)
        {
            if (!double.IsFinite(Y[i]) || !double.IsFinite(XAt(i))) throw new ArgumentException("Samples must be finite.");
            if (X is not null && i > 0 && X[i] <= X[i - 1]) throw new ArgumentException("X must increase strictly.");
        }
    }

    /// <summary>First sample at or beyond x, including Count at the right end.</summary>
    public int LowerBound(double x)
    {
        if (X is null) return (int)Math.Clamp(Math.Ceiling((x - Origin) / Step), 0, Y.Length);
        int lo = 0, hi = X.Length;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (X[mid] < x) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}
