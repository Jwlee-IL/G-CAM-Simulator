namespace Gcam.Studio.Core.ViewModels;

/// <summary>One read-only label / value row of a panel section (geometry facts, detector settings). The same facts stay
/// available as one text through the section's accessible name.</summary>
public sealed record FactRow(string Label, string Value)
{
    public override string ToString() => $"{Label} {Value}";
}
