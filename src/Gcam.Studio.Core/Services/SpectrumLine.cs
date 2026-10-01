namespace Gcam.Studio.Core.Services;

/// <summary>A source emission to annotate; never an added histogram count.</summary>
public sealed record SpectrumLine(string Isotope, double EnergyKeV);
