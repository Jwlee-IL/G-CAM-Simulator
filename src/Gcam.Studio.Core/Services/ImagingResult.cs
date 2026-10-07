using Gcam.Core;

namespace Gcam.Studio.Core.Services;

/// <summary>Imaging from one acquisition snapshot, in the shape the views need.</summary>
/// <remarks>Both grids use the same convention: pixel i is centred at <c>origin + i·step</c> mm.</remarks>
public sealed record ImagingResult(
    DetectorImage Flood,
    double FloodOriginMm,
    double FloodStepMm,
    DetectorImage? Reconstruction,
    double ReconOriginMm,
    double ReconStepMm,
    SourceEstimate? Estimate,
    double EffectiveCounts,
    TimeSpan Elapsed)
{
    /// <summary>Published strip/count metadata; null for All, unstripped and uncontaminated channels.</summary>
    public StripCountEstimate? StripCount { get; init; }

    /// <summary>Acquisition support, independent of the sign of a stripped net estimate.</summary>
    public bool HasData => StripCount?.HasRawCounts ?? EffectiveCounts > 0;
}
