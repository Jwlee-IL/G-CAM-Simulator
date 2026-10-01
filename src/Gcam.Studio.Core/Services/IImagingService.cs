using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>Builds measured-energy channels and H-only calibrations on a worker.</summary>
public interface IImagingService
{
    Task<ImagingView> ProcessAsync(Guid acquisitionId, AcquisitionSnapshot snapshot,
        IReadOnlyList<SceneSource> scene, OpticsSettings optics, ImagingSettings settings,
        CancellationToken cancellationToken = default);
}
