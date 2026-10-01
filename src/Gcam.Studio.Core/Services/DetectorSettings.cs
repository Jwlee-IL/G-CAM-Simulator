namespace Gcam.Studio.Core.Services;

/// <summary>Acquisition detector inputs, explicitly supplied by Studio without changing engine defaults.</summary>
public sealed record DetectorSettings
{
    public Gcam.Configuration.FrontEndChain Chain { get; init; } = Gcam.Configuration.FrontEndParts.Default;
    public double EntranceAbsorberMm { get; init; } = 0.15;
    public double BackingScatterMm { get; init; } = 2;
    public double GainSigma { get; init; } = 0.03;
    public int GainSeed { get; init; } = 1;
    public double ReflectorGapMm { get; init; } = 0.1;
}
