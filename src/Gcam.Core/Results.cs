namespace Gcam.Core;

/// <summary>The decoder's estimate of where the source is.</summary>
public sealed record SourceEstimate(Vector3 Position, double Confidence);

/// <summary>
/// The decoder's output: the peak estimate plus the full reconstruction image
/// (so artifacts like the off-axis ghost can be visualized). The reconstruction
/// is sampled over candidate source positions; pixel (0,0) maps to physical
/// coordinate (<see cref="ReconOriginMm"/>, <see cref="ReconOriginMm"/>) and each
/// step advances by <see cref="ReconStepMm"/>.
/// </summary>
public sealed record DecodeResult(
    SourceEstimate Estimate,
    DetectorImage Reconstruction,
    double ReconOriginMm,
    double ReconStepMm);

/// <summary>The full output of one simulation run.</summary>
public sealed class SimulationResult
{
    /// <summary>The raw detector flood map.</summary>
    public required DetectorImage DetectorImage { get; init; }

    /// <summary>The decoded source estimate (null if decoding was skipped).</summary>
    public SourceEstimate? Estimate { get; init; }

    /// <summary>The full reconstruction image (null if decoding was skipped).</summary>
    public DetectorImage? Reconstruction { get; init; }

    /// <summary>Physical coordinate of reconstruction pixel 0 on both axes (mm).</summary>
    public double ReconOriginMm { get; init; }

    /// <summary>Physical step between reconstruction pixels (mm).</summary>
    public double ReconStepMm { get; init; }

    public long PhotonsEmitted { get; init; }

    /// <summary>Raw number of photons that reached the detector (weighting ignored).</summary>
    public long PhotonsDetected { get; init; }

    /// <summary>
    /// Sum of scored detector weights = effective detected counts. Invariant across
    /// 4π vs biased emission (in 4π, weights are 1 so this equals PhotonsDetected).
    /// Divide by PhotonsEmitted for the geometric detection efficiency.
    /// </summary>
    public double DetectedWeight { get; init; }
}
