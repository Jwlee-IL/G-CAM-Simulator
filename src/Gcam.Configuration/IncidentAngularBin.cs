namespace Gcam.Configuration;

/// <summary>Joint scalar fluence in an energy component and propagation zenith-cosine interval.
/// EnergyIndex addresses Lines followed by Continuum. Azimuth is uniform. Zenith is world +y;
/// the camera optical axis remains +z. Line energies stay discrete; continuum energies are uniform within their bin.</summary>
public sealed class IncidentAngularBin
{
    public int EnergyIndex { get; set; }
    public double LowCosine { get; set; }
    public double HighCosine { get; set; }
    public double FluenceWeight { get; set; }
}
