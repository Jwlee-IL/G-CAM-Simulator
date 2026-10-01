using Gcam.Core;

namespace Gcam.Studio.Core.Services;

/// <summary>Immutable cumulative acquisition data. Events carry unsmeared deposits for later workspaces.</summary>
public sealed record AcquisitionSnapshot(double LiveTimeS, long Counts, double RateCps,
    double ActualSpeed, bool IsMcLimited, ImagingResult Imaging, IReadOnlyList<DetectedEvent> Events,
    TimeSpan DecodeTime, bool IsCompleted)
{
    public Gcam.Configuration.FrontEndChain Chain => (Detector ?? new DetectorSettings()).Chain;
    /// <summary>Frozen measurement inputs belonging to these events, never the current editable detector.</summary>
    public DetectorSettings? Detector { get; init; }
    /// <summary>Effective physical optics frozen at Start; decoder view focus may change later.</summary>
    public Gcam.Configuration.OpticsSettings? Optics { get; init; }
}
