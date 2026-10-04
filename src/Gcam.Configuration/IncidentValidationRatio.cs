namespace Gcam.Configuration;

/// <summary>One compared quantity of an acceptance record: the model's value over the reference value, with the model's
/// own Monte Carlo standard error (the reference's uncertainty is not part of it).</summary>
public sealed class IncidentValidationRatio
{
    public string Chain { get; set; } = "";
    public string Quantity { get; set; } = "";
    public double ModelValue { get; set; }
    public double ModelStandardError { get; set; }
    public double ReferenceValue { get; set; }
    public string Reference { get; set; } = "";
    public double Ratio { get; set; }
    public double RatioStandardError { get; set; }
}
