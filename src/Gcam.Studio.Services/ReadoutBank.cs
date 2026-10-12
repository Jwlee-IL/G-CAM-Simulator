namespace Gcam.Studio.Services;

/// <summary>The immutable set of line preparations an acquisition captures at Start: one prepared device (module) and
/// one or more successful line calibrations of it, the reference line first.</summary>
internal sealed record ReadoutBank(IReadOnlyList<ReadoutArtifact> Entries)
{
    public ReadoutArtifact Reference => Entries[0];
}
