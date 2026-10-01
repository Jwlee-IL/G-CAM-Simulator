using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Studio.Core.Services;

/// <summary>What one imaging run produced, in the shape the views need.</summary>
/// <remarks>Both grids use the same convention: pixel i is centred at <c>origin + i·step</c> mm.</remarks>
public sealed record ImagingResult(
    DetectorImage Flood,
    double FloodOriginMm,
    double FloodStepMm,
    DetectorImage? Reconstruction,
    double ReconOriginMm,
    double ReconStepMm,
    SourceEstimate? Estimate,
    double EffectiveCounts,
    TimeSpan Elapsed);

/// <summary>Runs the Monte Carlo imaging pipeline off the UI thread.</summary>
public interface ISimulationService : IAcquisitionService
{
    // Existing batch clients/fakes keep compiling. Production SimulationService supplies live acquisition.
    IAcquisitionSession IAcquisitionService.Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
        double liveTimeS, double speed) => throw new NotSupportedException("This service supports batch simulation only.");

    Task<ImagingResult> RunAsync(
        IReadOnlyList<SceneSource> scene,
        OpticsSettings optics,
        long photons,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}
