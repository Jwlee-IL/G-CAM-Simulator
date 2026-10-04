namespace Gcam.Simulation;

/// <summary>One evaluated photon line of a decay chain: absolute photons per chain decay (secular equilibrium).</summary>
public sealed class TerrestrialLine
{
    public string Nuclide { get; set; } = "";
    public double EnergyKeV { get; set; }
    public string Kind { get; set; } = "";
    public string Decay { get; set; } = "";
    public double IntensityPercentPerParentDecay { get; set; }
    public double YieldPerChainDecay { get; set; }
}
