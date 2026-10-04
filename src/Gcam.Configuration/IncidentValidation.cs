namespace Gcam.Configuration;

/// <summary>The acceptance record a validated spectrum carries (AB-10): which decision accepted it, against what, and how.
/// It is part of the hashed payload, so the acceptance cannot be separated from the weights it accepted.</summary>
public sealed class IncidentValidation
{
    /// <summary>Decision identifier in the plan's decision log (e.g. "AB-10").</summary>
    public string Decision { get; set; } = "";
    public string Date { get; set; } = "";
    /// <summary>What kind of comparison the acceptance is. AB-10: a model comparison, not a statistical test.</summary>
    public string Kind { get; set; } = "";
    /// <summary>Accepted |ratio − 1| for every compared quantity (AB-10: 0.03, chosen after seeing the ratios).</summary>
    public double AgreementBandFraction { get; set; }
    public IncidentValidationRatio[] Ratios { get; set; } = [];
    public string Note { get; set; } = "";
    /// <summary>The not-validated file this one re-issues with unchanged weights.</summary>
    public string SupersedesId { get; set; } = "";
    public string SupersedesContentHash { get; set; } = "";
    public string SupersedesFileSha256 { get; set; } = "";
}
