using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.Optics;

namespace Gcam.Studio.Services;

public sealed class SimulationService(TimeProvider? timeProvider = null, IReadoutPreparationService? readoutPreparation = null) : IAcquisitionService
{
    private readonly ReadoutPreparationService _readoutPreparation = readoutPreparation as ReadoutPreparationService ?? new();
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
        var prepared = detector.ReadoutMode == ReadoutMode.DirectCrystal ? ((ReadoutPreparation View, Gcam.Detector.ReadoutDevice Device, Gcam.Simulation.ReadoutCalibration Calibration)?)null : _readoutPreparation.Get(detector.PreparedReadoutId);
        return new AcquisitionSession(config, detector, liveTimeS, speed,
            timeProvider ?? TimeProvider.System, prepared);
    }

    /// <summary>Studio applies its realism inputs to a clone; existing scene-builder callers stay unchanged.</summary>
    public IAcquisitionSession StartAmbient(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
        double liveTimeS, double speed, AmbientFieldConfig ambient, DetectorSettings? detector = null,
        double backgroundToSignalRatio = 0, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(ambient);
        if (!(liveTimeS > 0) || !double.IsFinite(liveTimeS)) throw new ArgumentOutOfRangeException(nameof(liveTimeS));
        if (!(speed > 0) || !double.IsFinite(speed)) throw new ArgumentOutOfRangeException(nameof(speed));
        detector ??= new DetectorSettings();
        if (detector.ReadoutMode != ReadoutMode.DirectCrystal) throw new NotSupportedException("Physical readout requires ambient and BSR at zero: ambient interaction records are unavailable.");
        var config = BuildConfig(scene, optics, detector, backgroundToSignalRatio, ambient);
        if (seed is { } fixedSeed) config.Seed = fixedSeed;
        return new AcquisitionSession(config, detector, liveTimeS, speed, timeProvider ?? TimeProvider.System);
    }

    public static SimulationConfig BuildConfig(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
        DetectorSettings detector, double backgroundToSignalRatio = 0, AmbientFieldConfig? ambient = null)
    {
        ArgumentNullException.ThrowIfNull(detector);
        if (detector.ReadoutMode != ReadoutMode.DirectCrystal)
        {
            if (ReadoutPreparationService.GeometryError(optics) is { } readoutError) throw new ArgumentException(readoutError);
            if (ambient is not null || backgroundToSignalRatio != 0) throw new NotSupportedException("Physical readout requires ambient and BSR at zero: background interaction records are unavailable.");
            if (detector.ReadoutMode != ReadoutMode.FourOutputAnger) throw new NotSupportedException("Studio supports the experimental four-output preset only.");
        }
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
        if (detector.ReadoutMode != ReadoutMode.DirectCrystal)
        {
            config.Detector.Material = "GAGG";
            config.Detector.Readout = ReadoutPreparationService.Preset();
        }
        if (backgroundToSignalRatio > 0)
            config.Background = new BackgroundConfig { BackgroundToSignalRatio = backgroundToSignalRatio };
        if (ambient is not null)
        {
            config.Ambient = ambient;
            if (scene.Count == 0) { config.Source.ActivityBq = 0; config.Sources = []; }
            config = config.Clone(); // freeze the field and its spectrum along with the detector and scene
            // A preset reference (file name + pinned SHA-256) is resolved by the engine loader on the frozen copy.
            ConfigLoader.ResolveAmbientSpectrum(config, AmbientSpectrumDirectory);
        }
        return config;
    }

    /// <summary>Where Studio's hash-pinned ambient spectra are deployed: copied from <c>samples/ambient</c> into the
    /// <c>ambient</c> folder beside the assemblies at build time (Gcam.Studio.Services.csproj), so no machine path
    /// is involved and the loader re-checks the bytes against the pin on every Start.</summary>
    public static string AmbientSpectrumDirectory => Path.Combine(AppContext.BaseDirectory, "ambient");

}
