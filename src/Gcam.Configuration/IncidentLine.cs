namespace Gcam.Configuration;

/// <summary>One incident photon line with relative scalar fluence weight, not dose fraction.</summary>
public sealed class IncidentLine
{
    public double EnergyKeV { get; set; }
    public double FluenceWeight { get; set; }
}
