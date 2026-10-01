using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>Starts live MC acquisition; the implementation owns background work and its injectable time base.</summary>
public interface IAcquisitionService
{
    IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
        double liveTimeS, double speed);
}
