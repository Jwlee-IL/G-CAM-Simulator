using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>Frozen analysis identity. Prefix counts/time identify the retained flood, not a new transport run.</summary>
public sealed record FocusSweepIdentity(Guid AcquisitionId, long Counts, double LiveTimeS, string Channel,
    double WindowFwhm, bool Strip, OpticsSettings Optics, DetectorSettings Detector, int PeakCount, int PlaneCount = 81);
