namespace Gcam.Studio.Core.Services;

/// <summary>One primary-line window and its decoded flood; All retains every event.</summary>
public sealed record ImagingChannel(string Isotope, double LoKeV, double HiKeV,
    ImagingResult Image, IReadOnlyList<ImagingPeak> Peaks)
{
    /// <summary>Physical readout (TODO-41): why this channel has no image (a window without one captured calibration
    /// line, or an empty correction bin). An unavailable channel is never a zero-count image.</summary>
    public string? Unavailable { get; init; }

    /// <summary>Physical readout: the calibration line whose LUT and energy assigned this channel's events.</summary>
    public double? CalibrationLineKeV { get; init; }

    /// <summary>Physical readout: raw (integer) window counts, kept separate from a flood-corrected display.</summary>
    public long? RawCounts { get; init; }

    /// <summary>Physical readout: the opt-in flood correction was applied to the displayed flood and decode.</summary>
    public bool FloodCorrected { get; init; }
}
