namespace Gcam.Studio.Core.Services;

/// <summary>One primary-line window and its decoded flood; All retains every event.</summary>
public sealed record ImagingChannel(string Isotope, double LoKeV, double HiKeV,
    ImagingResult Image, IReadOnlyList<ImagingPeak> Peaks);
