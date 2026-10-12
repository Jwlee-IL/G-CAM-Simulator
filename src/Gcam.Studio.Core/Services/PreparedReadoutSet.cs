namespace Gcam.Studio.Core.Services;

/// <summary>The line preparations a physical acquisition captures (the bank). Equality is by content, so detector
/// settings carrying it keep value semantics.</summary>
public sealed record PreparedReadoutSet(IReadOnlyList<Guid> Ids)
{
    public bool Equals(PreparedReadoutSet? other) => other is not null && Ids.SequenceEqual(other.Ids);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var id in Ids) hash.Add(id);
        return hash.ToHashCode();
    }
}
