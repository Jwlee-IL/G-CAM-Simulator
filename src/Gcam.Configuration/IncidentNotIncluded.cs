namespace Gcam.Configuration;

/// <summary>A source-term item the spectrum deliberately leaves out, with its reason (AB-4d): a transition listed
/// without an evaluated photon intensity, an AB-4c omission bound, or a line outside the file's energy range.
/// Listing it is a record, never a substitute intensity.</summary>
public sealed class IncidentNotIncluded
{
    public string Chain { get; set; } = "";
    public string Nuclide { get; set; } = "";
    public double? EnergyKeV { get; set; }
    public string Reason { get; set; } = "";
}
