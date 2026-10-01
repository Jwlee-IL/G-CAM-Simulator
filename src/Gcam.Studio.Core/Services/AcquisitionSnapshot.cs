using Gcam.Core;

namespace Gcam.Studio.Core.Services;

/// <summary>Immutable cumulative acquisition data. Events carry unsmeared deposits for later workspaces.</summary>
public sealed record AcquisitionSnapshot(double LiveTimeS, long Counts, double RateCps,
    double ActualSpeed, bool IsMcLimited, ImagingResult Imaging, IReadOnlyList<DetectedEvent> Events,
    TimeSpan DecodeTime, bool IsCompleted);
