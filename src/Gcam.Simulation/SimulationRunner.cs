using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>
/// Orchestrates the Monte Carlo pipeline:
/// source emission → mask transmission → detector scoring → decoding.
/// The physics lives in the injected components; this class just drives the loop.
/// </summary>
public sealed class SimulationRunner : ISimulation
{
    // Photons between progress reports / cancellation checks: frequent enough for a responsive UI,
    // rare enough that the check costs nothing next to the transport.
    private const int CheckInterval = 4096;

    private readonly ISimulationFactory _factory;

    public SimulationRunner(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public SimulationResult Run(SimulationConfig config) => Run(config, progress: null, CancellationToken.None);

    /// <summary>
    /// Same as <see cref="Run(SimulationConfig)"/>, but reports the emitted fraction (0..1) and stops with
    /// <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    public SimulationResult Run(SimulationConfig config, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (config.Ambient is not null)
            throw new InvalidOperationException("Weighted photon-budget runs cannot mix absolute ambient counts. Use RunFixedTime with an explicit duration.");

        var rng = _factory.CreateRandom(config);
        var source = _factory.CreateSource(config);
        var mask = _factory.CreateMask(config);
        var detector = _factory.CreateDetector(config);
        var decoder = _factory.CreateDecoder(config);

        long emitted = 0, detected = 0;
        double budget = Math.Max(1, config.PhotonCount);
        foreach (var photon in source.Emit(rng, config.PhotonCount))
        {
            emitted++;
            if (emitted % CheckInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(Math.Min(1.0, emitted / budget));
            }

            if (!mask.Transmit(photon.Ray, photon.EnergyKeV, rng))
                continue;

            if (detector.Score(photon))
                detected++;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var image = detector.Readout();
        var decode = decoder?.Decode(image);
        progress?.Report(1.0);

        double detectedWeight = 0.0;
        foreach (var v in image.Raw) detectedWeight += v;

        return new SimulationResult
        {
            DetectorImage = image,
            Estimate = decode?.Estimate,
            Reconstruction = decode?.Reconstruction,
            ReconOriginMm = decode?.ReconOriginMm ?? 0.0,
            ReconStepMm = decode?.ReconStepMm ?? 0.0,
            PhotonsEmitted = emitted,
            PhotonsDetected = detected,
            DetectedWeight = detectedWeight,
        };
    }

    /// <summary>Observed Poisson counts at physical source activity and absolute field strength, in explicit live time.
    /// Separate from Run's weighted photon-budget units; no normalized study silently rescales ambient counts.</summary>
    public SimulationResult RunFixedTime(SimulationConfig config, double durationS, CancellationToken cancellationToken = default)
    {
        if (!(durationS >= 0) || !double.IsFinite(durationS)) throw new ArgumentOutOfRangeException(nameof(durationS));
        using var source = new ListModeSource(config);
        var image = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
        long count = 0;
        while (source.ArrivalTimeS < durationS)
            if (source.AdvanceUntil(durationS, cancellationToken) is { } e)
            { image.Add(e.PixelX, e.PixelY, 1); count++; }
        var decoded = count > 0 ? _factory.CreateDecoder(config)?.Decode(image) : null;
        return new SimulationResult
        {
            DetectorImage = image, PhotonsEmitted = source.HistoriesEmitted,
            PhotonsDetected = count, DetectedWeight = count,
            Estimate = decoded?.Estimate, Reconstruction = decoded?.Reconstruction,
            ReconOriginMm = decoded?.ReconOriginMm ?? 0, ReconStepMm = decoded?.ReconStepMm ?? 0
        };
    }
}
