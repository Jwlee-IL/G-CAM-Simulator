using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>
/// Orchestrates the Monte Carlo pipeline:
/// source emission → mask transmission → detector scoring → decoding.
/// The physics lives in the injected components; this class just drives the loop.
/// </summary>
public sealed class SimulationRunner : ISimulation
{
    private readonly ISimulationFactory _factory;

    public SimulationRunner(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public SimulationResult Run(SimulationConfig config)
    {
        var rng = _factory.CreateRandom(config);
        var source = _factory.CreateSource(config);
        var mask = _factory.CreateMask(config);
        var detector = _factory.CreateDetector(config);
        var decoder = _factory.CreateDecoder(config);

        long emitted = 0, detected = 0;
        foreach (var photon in source.Emit(rng, config.PhotonCount))
        {
            emitted++;
            if (!mask.Transmit(photon.Ray, photon.EnergyKeV, rng))
                continue;

            if (detector.Score(photon))
                detected++;
        }

        var image = detector.Readout();
        var decode = decoder?.Decode(image);

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
}
