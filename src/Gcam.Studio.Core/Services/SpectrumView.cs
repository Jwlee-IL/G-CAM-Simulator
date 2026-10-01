namespace Gcam.Studio.Core.Services;

/// <summary>Immutable after publication. Total includes pulses beyond the displayed energy range.</summary>
public sealed record SpectrumView(double[] CentresKeV, double[] Counts, IReadOnlyList<SpectrumBand> Bands,
    long TotalCounts, long OverflowCounts, double InWindowShare, double Resolution662,
    double ResolvingTimeS, string Chain, TimeSpan ProcessingTime)
{
    public double[] BinEdgesKeV { get; init; } = [];
}
