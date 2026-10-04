using System.Text.Json;

namespace Gcam.Simulation;

/// <summary>One chain of the AB-4 source catalog (K-40, U-238 series or Th-232 series).</summary>
public sealed class TerrestrialChain
{
    public string Name { get; set; } = "";
    public TerrestrialLine[] Lines { get; set; } = [];
    public double PhotonsPerDecay { get; set; }
    public double PhotonEnergyKeVPerDecay { get; set; }
    public TerrestrialNotIncludedEntry[] NotIncluded { get; set; } = [];
    /// <summary>The turn-5 AB-4c omission records, carried unchanged into the spectrum file.</summary>
    public JsonElement[] Ab4cOmissionsOnRecord { get; set; } = [];
}
