using Gcam.Core;

namespace Gcam.Studio.Core.Services;

/// <summary>Immutable cumulative acquisition data. Events carry unsmeared deposits for later workspaces.</summary>
public sealed record AcquisitionSnapshot(double LiveTimeS, long Counts, double RateCps,
    double ActualSpeed, bool IsMcLimited, ImagingResult Imaging, IReadOnlyList<DetectedEvent> Events,
    TimeSpan DecodeTime, bool IsCompleted)
{
    /// <summary>Observed count rate, counts / live time — the one rate the UI shows. <see cref="RateCps"/> is the MC's
    /// running expected rate, which the observed rate scatters around.</summary>
    public double ObservedRateCps => LiveTimeS > 0 ? Counts / LiveTimeS : 0;
    public Gcam.Configuration.FrontEndChain Chain => (Detector ?? new DetectorSettings()).Chain;
    /// <summary>Frozen measurement inputs belonging to these events, never the current editable detector.</summary>
    public DetectorSettings? Detector { get; init; }
    /// <summary>Effective physical optics frozen at Start; decoder view focus may change later.</summary>
    public Gcam.Configuration.OpticsSettings? Optics { get; init; }
    /// <summary>Monte Carlo seed of the acquisition these events come from (provenance; reproduces them).</summary>
    public int? Seed { get; init; }
}
