using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>Starts live MC acquisition; the implementation owns background work and its injectable time base.</summary>
public interface IAcquisitionService
{
    /// <param name="seed">Monte Carlo seed of this acquisition (every random stream derives from it); null keeps the
    /// engine's fixed default. The shell draws a new one per acquisition unless the user fixes it.</param>
    IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
        double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0,
        int? seed = null);

    /// <summary>Absolute ambient acquisition, including an empty source scene. The field is frozen at Start.</summary>
    IAcquisitionSession StartAmbient(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
        double liveTimeS, double speed, AmbientFieldConfig ambient, DetectorSettings? detector = null,
        double backgroundToSignalRatio = 0, int? seed = null)
        => throw new NotSupportedException("This acquisition service does not support absolute ambient fields.");
}
