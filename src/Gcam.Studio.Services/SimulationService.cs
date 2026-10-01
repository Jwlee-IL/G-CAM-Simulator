using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

public sealed class SimulationService(TimeProvider? timeProvider = null) : IAcquisitionService
{
    public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
        double liveTimeS, double speed)
    {
        if (!(liveTimeS > 0) || !double.IsFinite(liveTimeS)) throw new ArgumentOutOfRangeException(nameof(liveTimeS));
        if (!(speed > 0) || !double.IsFinite(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
        if (scene.Count == 0) throw new ArgumentException("Acquisition needs at least one source.", nameof(scene));
        return new AcquisitionSession(SceneConfigBuilder.Build(scene, optics, 1), liveTimeS, speed,
            timeProvider ?? TimeProvider.System);
    }

}
