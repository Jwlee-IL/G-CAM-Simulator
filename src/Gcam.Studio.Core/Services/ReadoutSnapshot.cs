namespace Gcam.Studio.Core.Services;

/// <summary>A cumulative immutable physical prefix. Unknown records remain available for diagnostics, not images.</summary>
public sealed record ReadoutSnapshot(ReadoutPreparation Preparation, IReadOnlyList<MeasuredReadoutRecord> Records,
    IReadOnlyList<RealisedReadoutHit> Hits, int Unknown, bool PendingHold)
{
    public Gcam.Core.DetectorImage? LiveDensity { get; init; }
    public IReadOnlyList<ReadoutTruthSample> TruthSamples { get; init; } = [];
}
