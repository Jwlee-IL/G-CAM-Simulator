namespace Gcam.Simulation;

/// <summary>A catalog transition without an evaluated photon intensity (or otherwise outside the source term), with its reason.</summary>
public sealed class TerrestrialNotIncludedEntry
{
    public string Nuclide { get; set; } = "";
    public double? EnergyKeV { get; set; }
    public string Kind { get; set; } = "";
    public string Decay { get; set; } = "";
    public string Reason { get; set; } = "";
}
