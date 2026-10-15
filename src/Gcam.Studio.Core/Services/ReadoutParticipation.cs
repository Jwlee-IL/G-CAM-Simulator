namespace Gcam.Studio.Core.Services;

/// <summary>Unique input participation, finalized only on pulse-support expiry; unresolved tails are not losses.</summary>
public sealed record ReadoutParticipation(int FinalizedHistories, int NoRecordedContribution, int UnresolvedHistories);
