namespace Gcam.Studio.Core.Services;

/// <summary>A cumulative immutable physical prefix. Unknown records remain available for diagnostics, not images.
/// <see cref="Preparation"/> is the reference line (its assignments are the records' Crystal / EnergyKeV);
/// <see cref="Lines"/> holds every captured line's projection of the same held records.</summary>
public sealed record ReadoutSnapshot(ReadoutPreparation Preparation, IReadOnlyList<MeasuredReadoutRecord> Records,
    IReadOnlyList<RealisedReadoutHit> Hits, int Unknown, bool PendingHold)
{
    /// <summary>Finalized at pulse-support expiry; unresolved tails are never implicitly flushed at Stop.</summary>
    public ReadoutParticipation? Participation { get; init; }
    public Gcam.Core.DetectorImage? LiveDensity { get; init; }
    public IReadOnlyList<ReadoutTruthSample> TruthSamples { get; init; } = [];
    public IReadOnlyList<ReadoutLineView> Lines { get; init; } = [];

    /// <summary>The captured line view whose calibration line is <paramref name="lineKeV"/>, or null.</summary>
    public ReadoutLineView? Line(double lineKeV) => Lines.FirstOrDefault(l => l.LineKeV == lineKeV);
}
