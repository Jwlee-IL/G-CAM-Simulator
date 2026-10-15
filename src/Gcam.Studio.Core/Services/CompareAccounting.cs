namespace Gcam.Studio.Core.Services;

/// <summary>Held-conversion populations. NeverDominant is not a threshold/dead-time loss estimate.</summary>
public sealed record CompareAccounting(int Histories, int Held, int Assigned, int Unknown, int MultiContributor,
    int NoDominant, int DistinctDominant, int NeverDominant, long Eligible, long Same,
    long DirectWindow, long AngerWindow)
{
    public ReadoutParticipation? Participation { get; init; }
    public double? SameShare => Eligible > 0 ? (double)Same / Eligible : null;
}
