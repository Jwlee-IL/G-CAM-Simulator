using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>View settings over retained events; N is supplied by the shell. <paramref name="Method"/> is a re-projection
/// choice like the focal plane: it changes decoding of the retained floods, never transport or calibration.</summary>
public sealed record ImagingSettings(double WindowFwhm = 1.5, bool Strip = false, double? FocalDistanceMm = null,
    DecoderMethod Method = DecoderMethod.CrossCorrelation)
{
    public double WindowLowKeV { get; init; } = 600;
    public double WindowHighKeV { get; init; } = 720;
}
