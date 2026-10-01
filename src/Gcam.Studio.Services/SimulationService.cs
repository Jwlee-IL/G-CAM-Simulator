using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

public sealed class SimulationService : ISimulationService
{
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
