using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

public sealed class SimulationService(TimeProvider? timeProvider = null) : ISimulationService
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

    public Task<ImagingResult> RunAsync(
        IReadOnlyList<SceneSource> scene,
        OpticsSettings optics,
        long photons,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        // Build the config on the caller's thread so argument errors surface immediately;
        // the transport itself is CPU-bound and runs on the thread pool.
        var config = SceneConfigBuilder.Build(scene, optics, photons);
        return Task.Run(() =>
        {
            var watch = Stopwatch.StartNew();
            var result = new SimulationRunner(new DefaultSimulationFactory()).Run(config, progress, cancellationToken);
            // Detector pixels are centred on the optical axis: pixel i at (i - (N-1)/2)·pitch.
            double pitch = config.Detector.PixelPitchMm;
            double floodOrigin = -(result.DetectorImage.Width - 1) / 2.0 * pitch;
            return new ImagingResult(
                result.DetectorImage,
                floodOrigin,
                pitch,
                result.Reconstruction,
                result.ReconOriginMm,
                result.ReconStepMm,
                result.Estimate,
                result.DetectedWeight,   // physical count; PhotonsDetected over-counts non-ideal crystals
                watch.Elapsed);
        }, cancellationToken);
    }
}
