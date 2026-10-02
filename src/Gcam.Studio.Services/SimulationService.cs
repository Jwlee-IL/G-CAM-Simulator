using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.Optics;

namespace Gcam.Studio.Services;

public sealed class SimulationService(TimeProvider? timeProvider = null) : IAcquisitionService
{
    public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
        double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0,
        int? seed = null)
    {
        if (!(liveTimeS > 0) || !double.IsFinite(liveTimeS)) throw new ArgumentOutOfRangeException(nameof(liveTimeS));
        if (!(speed > 0) || !double.IsFinite(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
        if (scene.Count == 0) throw new ArgumentException("Acquisition needs at least one source.", nameof(scene));
        detector ??= new DetectorSettings();
        var config = BuildConfig(scene, optics, detector, backgroundToSignalRatio);
        if (seed is { } fixedSeed) config.Seed = fixedSeed;
        return new AcquisitionSession(config, detector, liveTimeS, speed,
            timeProvider ?? TimeProvider.System);
    }

    /// <summary>Studio applies its realism inputs to a clone; existing scene-builder callers stay unchanged.</summary>
    public static SimulationConfig BuildConfig(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
        DetectorSettings detector, double backgroundToSignalRatio = 0)
    {
        ArgumentNullException.ThrowIfNull(detector);
        string? error = OpticsPolicy.Validate(optics, detector.ReflectorGapMm)
            ?? OpticsPolicy.ValidateScene(optics, scene) ?? OpticsPolicy.ValidateFocus(optics, optics.FocalDistanceMm);
        if (error is not null) throw new ArgumentException(error, nameof(optics));
        var config = ImagingProjection.AtFocus(SceneConfigBuilder.Build(scene, optics, 1), optics, optics.FocalDistanceMm);
        if (!double.IsFinite(detector.GainSigma) || detector.GainSigma < 0 ||
            !double.IsFinite(detector.EntranceAbsorberMm) || detector.EntranceAbsorberMm < 0 ||
            !double.IsFinite(detector.BackingScatterMm) || detector.BackingScatterMm < 0 ||
            !double.IsFinite(detector.ReflectorGapMm) || detector.ReflectorGapMm < 0 ||
            detector.ReflectorGapMm >= config.Detector.PixelPitchMm)
            throw new ArgumentOutOfRangeException(nameof(detector));
        if (!double.IsFinite(backgroundToSignalRatio) || backgroundToSignalRatio < 0)
            throw new ArgumentOutOfRangeException(nameof(backgroundToSignalRatio));
        config.Detector.EntranceAbsorberMm = detector.EntranceAbsorberMm;
        config.Detector.Material = FrontEndMaterials.Material(detector.Chain.Scintillator);
        config.Detector.BackingScatterMm = detector.BackingScatterMm;
        config.Detector.ReflectorGapMm = detector.ReflectorGapMm;
        config.Detector.GainSigma = detector.GainSigma;
        config.Detector.UniformitySeed = detector.GainSeed;
        if (backgroundToSignalRatio > 0)
            config.Background = new BackgroundConfig { BackgroundToSignalRatio = backgroundToSignalRatio };
        return config;
    }

}
