using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>Explicit, cancellable per-line readout preparation with session-only reuse (SD-A6…A8).</summary>
public interface IReadoutPreparationService
{
    /// <summary>The shipped, hash-pinned offer / cost record (unavailable when missing or malformed).</summary>
    ReadoutOfferRecord Offers { get; }

    /// <summary>Prepares (or returns the session-cached) calibration of one line for the given module.</summary>
    Task<ReadoutPreparation> PrepareAsync(OpticsSettings optics, DetectorSettings detector, ReadoutLinePlan line,
        IProgress<ReadoutPrepareProgress>? progress = null, CancellationToken cancellationToken = default);
}
