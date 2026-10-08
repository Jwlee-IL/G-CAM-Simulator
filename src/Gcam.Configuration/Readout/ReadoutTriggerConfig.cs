namespace Gcam.Configuration;

/// <summary>Trigger (RD-2): the sum trigger is the baseline; per-channel OR and AND are compared. The trigger compares
/// the noiseless analogue waveform (trigger-noise jitter is not modelled). A trigger decides whether an event is
/// recorded; every channel of a triggered event is kept for its position, below threshold or not.</summary>
public sealed class ReadoutTriggerConfig
{
    public TriggerLogic Logic { get; set; } = TriggerLogic.Sum;

    /// <summary>Threshold value in <see cref="Unit"/>. Default 50 keV (sum): below the lowest line studied (122 keV) and
    /// above the dark-count / single-photoelectron nuisance — a choice to be swept, not a measured optimum.</summary>
    public double Threshold { get; set; } = 50;

    public ThresholdUnit Unit { get; set; } = ThresholdUnit.KeVEquivalent;
}
