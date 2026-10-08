using Gcam.Configuration;

namespace Gcam.Simulation;

/// <summary>One readout compared in the study: its mode and, optionally, network, segmentation and zero suppression
/// (else the request's base readout).</summary>
public sealed class ReadoutStudyVariant
{
    public string Name { get; set; } = "";
    public ReadoutMode Mode { get; set; } = ReadoutMode.FourOutputAnger;
    public ChargeNetworkConfig? Network { get; set; }
    public FloodSegmentation? Segmentation { get; set; }
    public double? ZeroSuppressionSigma { get; set; }
}
