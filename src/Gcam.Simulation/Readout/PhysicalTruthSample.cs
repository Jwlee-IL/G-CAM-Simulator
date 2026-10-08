using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>Bounded diagnostic truth only; never used for measured LUT assignment.</summary>
public sealed record PhysicalTruthSample(int HitIndex, IReadOnlyList<InteractionSite> Sites);
