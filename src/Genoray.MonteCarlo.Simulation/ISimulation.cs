using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>Runs one scenario end-to-end and returns its result.</summary>
public interface ISimulation
{
    SimulationResult Run(SimulationConfig config);
}
