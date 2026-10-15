namespace Gcam.Studio.Core.Services;

/// <summary>Immutable publication: images, spectrum and diagnostics always name the same prefix.</summary>
public sealed record CompareResult(CompareKey Key, CompareSettings Settings, ImagingChannel Direct, ImagingChannel Anger,
    CompareAssignment Assignment, CompareSpectrum Spectrum, CompareAccounting Accounting,
    CompareTimings Timings, ReadoutCostApplicability Cost);
