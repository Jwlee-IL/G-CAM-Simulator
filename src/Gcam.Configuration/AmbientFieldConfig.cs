namespace Gcam.Configuration;

/// <summary>Absolute photon ambient dose equivalent in the free field, before inserting the detector.</summary>
public sealed class AmbientFieldConfig
{
    public double DoseRateMicroSvPerHour { get; set; }
    public AmbientGeometry Geometry { get; set; } = AmbientGeometry.BareCrystalAllFaces;
    public IncidentSpectrum Spectrum { get; set; } = IncidentSpectrum.Placeholder();
    /// <summary>Evidence recipes must require a validated spectrum; development acquisitions may use the labelled placeholder.</summary>
    public bool RequireValidatedSpectrum { get; set; }
}
