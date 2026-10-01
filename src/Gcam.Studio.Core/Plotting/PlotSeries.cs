namespace Gcam.Studio.Core.Plotting;

/// <summary>Finite samples on an increasing X axis. Arrays are immutable after publication.</summary>
public sealed record PlotSeries(string Name, double[] Y, double[]? X = null,
    double Origin = 0, double Step = 1, PlotKind Kind = PlotKind.Line, PlotColourRole ColourRole = PlotColourRole.Series1)
{
    public const int MaximumSamples = 10_000_000;
    public double XAt(int index) => X is null ? Origin + index * Step : X[index];

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Y);
        if (Y.Length > MaximumSamples) throw new ArgumentOutOfRangeException(nameof(Y), "Scope input limit is 10 million samples.");
        if (!double.IsFinite(Origin) || !double.IsFinite(Step) || Step <= 0)
            throw new ArgumentOutOfRangeException(nameof(Step));
        if (X is not null && X.Length != Y.Length) throw new ArgumentException("X and Y lengths differ.");
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
