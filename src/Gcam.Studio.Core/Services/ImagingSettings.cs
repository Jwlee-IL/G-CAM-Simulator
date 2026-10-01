namespace Gcam.Studio.Core.Services;

/// <summary>View settings over retained events; N is supplied by the shell.</summary>
public sealed record ImagingSettings(double WindowFwhm = 1.5, bool Strip = false, double? FocalDistanceMm = null);
