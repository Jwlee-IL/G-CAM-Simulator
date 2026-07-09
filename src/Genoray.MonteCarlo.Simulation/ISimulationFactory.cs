using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>
/// Builds the pluggable pipeline components from a configuration. Swapping an
/// implementation here (e.g. a non-cyclic decoder) changes the experiment
/// without touching the runner.
/// </summary>
public interface ISimulationFactory
{
    IRandom CreateRandom(SimulationConfig config);
    ISource CreateSource(SimulationConfig config);
    IMask CreateMask(SimulationConfig config);
    IDetector CreateDetector(SimulationConfig config);

    /// <summary>The decoder, or null if decoding is not wired yet (geometry-only runs).</summary>
    IDecoder? CreateDecoder(SimulationConfig config);
}
