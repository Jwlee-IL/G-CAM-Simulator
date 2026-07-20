using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>Runs one scenario end-to-end and returns its result.</summary>
public interface ISimulation
{
    SimulationResult Run(SimulationConfig config);
}
