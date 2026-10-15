namespace Gcam.Studio.Core.Services;

/// <summary>Worker elapsed observations, separate from dispatcher/render time.</summary>
public sealed record CompareTimings(TimeSpan Aggregate, TimeSpan Decode, TimeSpan Total);
