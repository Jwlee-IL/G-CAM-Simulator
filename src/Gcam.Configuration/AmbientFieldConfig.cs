namespace Gcam.Configuration;

/// <summary>Absolute photon ambient dose equivalent in the free field, before inserting the detector.</summary>
public sealed class AmbientFieldConfig
{
    public double DoseRateMicroSvPerHour { get; set; }
    public AmbientGeometry Geometry { get; set; } = AmbientGeometry.BareCrystalAllFaces;
    public IncidentSpectrum Spectrum { get; set; } = IncidentSpectrum.Placeholder();
    /// <summary>Evidence recipes must require a validated spectrum; development acquisitions may use the labelled placeholder.</summary>
    public bool RequireValidatedSpectrum { get; set; }
    /// <summary>Spectrum by reference: a file path (absolute, or relative to the scenario file) whose bytes must hash to
    /// <see cref="SpectrumFileSha256"/>. <see cref="ConfigLoader.Load"/> resolves it into <see cref="Spectrum"/> and clears
    /// it; an unresolved reference is rejected by the engine, so the placeholder default can never stand in for it.</summary>
    public string? SpectrumFile { get; set; }
    public string? SpectrumFileSha256 { get; set; }
}
