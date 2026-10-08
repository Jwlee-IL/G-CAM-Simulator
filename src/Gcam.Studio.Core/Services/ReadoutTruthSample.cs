using Gcam.Core;

namespace Gcam.Studio.Core.Services;

/// <summary>First, at most 256 scored input hits' pre-optical sites, for diagnostics only.</summary>
public sealed record ReadoutTruthSample(int HitIndex, IReadOnlyList<InteractionSite> Sites);
