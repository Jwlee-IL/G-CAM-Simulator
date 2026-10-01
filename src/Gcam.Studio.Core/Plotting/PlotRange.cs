namespace Gcam.Studio.Core.Plotting;

/// <summary>Extrema in a half-open sample interval; empty columns contain no fabricated samples.</summary>
public readonly record struct PlotRange(int Start, int End, double Min, double Max)
{
    public bool IsEmpty => End <= Start;
}
