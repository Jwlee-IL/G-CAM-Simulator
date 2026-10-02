namespace Gcam.Configuration;

/// <summary>A uniform-energy incident continuum bin with integrated scalar fluence weight.</summary>
public sealed class IncidentContinuumBin
{
    public double LowKeV { get; set; }
    public double HighKeV { get; set; }
    public double FluenceWeight { get; set; }
}
