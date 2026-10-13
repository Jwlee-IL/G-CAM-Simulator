namespace Gcam.Studio.Core.ViewModels;

/// <summary>One crystal of the local per-crystal check (Diagnostics view): assigned single-crystal histories and wrong
/// assignments; <see cref="Note"/> carries a line that is not a crystal row (shown across the table).</summary>
public sealed record ReadoutDiagnosticRow(string Crystal, string Assigned, string Wrong, string? Note = null)
{
    public bool IsNote => Note is not null;
    public override string ToString() => Note ?? $"Crystal {Crystal}: assigned {Assigned}; wrong {Wrong}";
}
