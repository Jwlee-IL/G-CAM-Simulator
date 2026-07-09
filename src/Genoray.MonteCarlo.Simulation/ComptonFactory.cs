using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Detector;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>
/// Standard pipeline but with a <see cref="ComptonCrystalDetector"/> (crystal-internal Compton
/// scattering + a chosen multi-pixel positioning strategy and analysis energy window) in place
/// of the ideal detector. The cascade RNG is seeded independently of the source/mask stream so
/// the same photons + cascades are replayed across strategies (fair comparison).
/// </summary>
public sealed class ComptonFactory : ISimulationFactory
{
    private readonly DefaultSimulationFactory _base = new();
    private readonly ComptonStrategy _strategy;
    private readonly double _windowCenterKeV, _windowFraction, _muAt662;

    public ComptonFactory(ComptonStrategy strategy, double windowCenterKeV, double windowFraction,
                          double muAt662PerMm = 0.09)
    {
        _strategy = strategy;
        _windowCenterKeV = windowCenterKeV;
        _windowFraction = windowFraction;
        _muAt662 = muAt662PerMm;
    }

    public IRandom CreateRandom(SimulationConfig c) => _base.CreateRandom(c);
    public ISource CreateSource(SimulationConfig c) => _base.CreateSource(c);
    public IMask CreateMask(SimulationConfig c) => _base.CreateMask(c);
    public IDecoder? CreateDecoder(SimulationConfig c) => _base.CreateDecoder(c);

    public IDetector CreateDetector(SimulationConfig config)
    {
        var d = config.Detector;
        bool nonUniform = d.GainSigma != 0.0 || d.EnergyResolutionFwhmSigma != 0.0 || d.GainGradient != 0.0;
        double[]? sensitivity = nonUniform ? new CrystalUniformity(d).Sensitivity : null;
        var cascadeRng = new DefaultRandom(config.Seed + 777);
        return new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm,
            _windowCenterKeV, _windowFraction, _strategy, cascadeRng,
            _muAt662, d.CrystalThicknessMm, planeZ: 0.0, sensitivity);
    }
}
