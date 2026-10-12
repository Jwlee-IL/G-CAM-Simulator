namespace Gcam.Studio.Core.Services;

/// <summary>The held measured records of an acquisition projected through one captured line calibration. Views of
/// different lines are overlapping readings of the same events, never independent counts.</summary>
public sealed record ReadoutLineView(ReadoutPreparation Preparation, IReadOnlyList<ReadoutLineAssignment> Assignments,
    int Assigned, int Unknown)
{
    public double LineKeV => Preparation.LineKeV;
}
