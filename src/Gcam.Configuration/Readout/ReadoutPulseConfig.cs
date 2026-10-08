namespace Gcam.Configuration;

/// <summary>
/// Time-domain model of the outputs (RD-3). Every channel carries the same pulse shape — the engine's bi-exponential
/// front-end pulse a·(e^(−t/τ_tail) − e^(−t/τ_rise)) (Waveform.BiexpPulse) with the default chain's time constants
/// (FrontEndParts.PulseSamples: GAGG 90 ns ⊗ "CSP + CR-RC (200 ns)" 320 ns) — scaled by the channel's charge, so
/// pulses of overlapping events SUM in every channel before the hold. A trigger arms a hold that searches its window for
/// the peak; the chain then stays busy for <see cref="DeadTimeNs"/> and re-arms only after the trigger condition has
/// dropped (edge trigger). The network's own RC response, channel skew and hold droop are not modelled (RD-5).
/// </summary>
public sealed class ReadoutPulseConfig
{
    /// <summary>Rise time constant (ns); null = the default chain's (90 ns).</summary>
    public double? RiseTimeNs { get; set; }

    /// <summary>Tail time constant (ns); null = the default chain's (320 ns).</summary>
    public double? TailTimeNs { get; set; }

    /// <summary>Time step of the waveform evaluation (ns); 8 ns = one AD9648 sample at 125 MSPS.</summary>
    public double TimeStepNs { get; set; } = 8;

    /// <summary>Window after the trigger in which the peak is searched (ns); null = the pulse's own time-to-peak (the
    /// shortest window that always contains an isolated pulse's peak, whatever the threshold).</summary>
    public double? HoldWindowNs { get; set; }

    /// <summary>Conversion / reset time after the hold during which no trigger is accepted (ns).</summary>
    public double DeadTimeNs { get; set; }

    public HoldMode Hold { get; set; } = HoldMode.CommonAtSumPeak;

    /// <summary>A hit counts as piled into an event when its pulse contributes at least this fraction of the held sum
    /// (bookkeeping for the pile-up metrics only; the physics sums every pulse in full).</summary>
    public double PileUpContributionFraction { get; set; } = 0.01;
}
