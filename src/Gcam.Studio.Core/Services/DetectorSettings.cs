namespace Gcam.Studio.Core.Services;

/// <summary>Acquisition detector inputs, explicitly supplied by Studio without changing engine defaults.</summary>
public sealed record DetectorSettings
{
    public Gcam.Configuration.ReadoutMode ReadoutMode { get; init; }
    /// <summary>A single-line bank (TODO-40 callers); ignored when <see cref="PreparedReadoutLines"/> is set.</summary>
    public Guid PreparedReadoutId { get; init; }
    /// <summary>TODO-41: the captured line preparations, reference line first (SD-A1…A3).</summary>
    public PreparedReadoutSet? PreparedReadoutLines { get; init; }
    public Gcam.Configuration.FrontEndChain Chain { get; init; } = StudioOptionDefaults.Current.Chain;
    public double EntranceAbsorberMm { get; init; } = StudioOptionDefaults.Current.EntranceAbsorberMm;
    public double BackingScatterMm { get; init; } = StudioOptionDefaults.Current.BackingScatterMm;
    public double GainSigma { get; init; } = StudioOptionDefaults.Current.GainSigmaPercent / 100;
    public int GainSeed { get; init; } = StudioOptionDefaults.Current.GainSeed;
    public double ReflectorGapMm { get; init; } = StudioOptionDefaults.Current.GapUm / 1000;

    /// <summary>The line preparation ids an acquisition captures, reference first.</summary>
    public IReadOnlyList<Guid> PreparedReadoutIds => PreparedReadoutLines?.Ids
        ?? (PreparedReadoutId == Guid.Empty ? [] : [PreparedReadoutId]);
}
