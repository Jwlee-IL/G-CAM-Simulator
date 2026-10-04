namespace Gcam.Simulation;

/// <summary>Partial mass interaction coefficients at one energy, cm²/g. Pair is nuclear plus electron field.</summary>
public readonly record struct PhotonCoefficients(double Coherent, double Incoherent, double Photoelectric, double Pair)
{
    /// <summary>The coefficient the soil/air transport attenuates with: coherent scattering is treated as no
    /// interaction (it keeps the energy; see <see cref="SoilAirTransport"/> for the stated effect).</summary>
    public double WithoutCoherent => Incoherent + Photoelectric + Pair;
    public double Total => Coherent + WithoutCoherent;
}
