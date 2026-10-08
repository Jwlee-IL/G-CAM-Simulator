using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

public interface IReadoutPreparationService
{
    Task<ReadoutPreparation> PrepareAsync(OpticsSettings optics, DetectorSettings detector,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
