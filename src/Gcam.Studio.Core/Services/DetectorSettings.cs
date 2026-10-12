namespace Gcam.Studio.Core.Services;

/// <summary>Acquisition detector inputs, explicitly supplied by Studio without changing engine defaults.</summary>
public sealed record DetectorSettings
{
    public Gcam.Configuration.ReadoutMode ReadoutMode { get; init; }
    /// <summary>A single-line bank (TODO-40 callers); ignored when <see cref="PreparedReadoutLines"/> is set.</summary>
    public Guid PreparedReadoutId { get; init; }
    /// <summary>TODO-41: the captured line preparations, reference line first (SD-A1…A3).</summary>
    public PreparedReadoutSet? PreparedReadoutLines { get; init; }
    public Gcam.Configuration.FrontEndChain Chain { get; init; } = Gcam.Configuration.FrontEndParts.Default;
    public double EntranceAbsorberMm { get; init; } = 0.15;
    public double BackingScatterMm { get; init; } = 2;
    public double GainSigma { get; init; } = 0.03;
    public int GainSeed { get; init; } = 1;
    public double ReflectorGapMm { get; init; } = 0.1;

    /// <summary>The line preparation ids an acquisition captures, reference first.</summary>
    public IReadOnlyList<Guid> PreparedReadoutIds => PreparedReadoutLines?.Ids
        ?? (PreparedReadoutId == Guid.Empty ? [] : [PreparedReadoutId]);
}
