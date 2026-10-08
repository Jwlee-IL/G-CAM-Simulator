using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Detector;

/// <summary>
/// Time-domain trigger, hold and digitisation of the readout channels (RD-3). Every channel's waveform is the sum over
/// hits of (that hit's channel charge) × u(t − t_hit), with u the engine's bi-exponential front-end pulse
/// e^(−t/τ_tail) − e^(−t/τ_rise) (Waveform.BiexpPulse), normalised so that an isolated pulse sampled on its own grid peaks
/// at exactly 1. Because all channels share u, overlapping pulses add in EVERY channel before the hold: a piled-up
/// event's held channels are Σ_h a_h·u(t* − t_h), and its Anger position is the centroid of the hits' light weighted by
/// their pulse heights at the hold instant t* — the charge-weighted light centroid when the pulses coincide.
/// <para>The waveform is evaluated on a grid of <see cref="StepNs"/> whose origin is reset to the arrival of a hit that
/// finds the chain idle (so an isolated pulse is always sampled at the same phase). States: Armed → (trigger condition
/// true on the noiseless waveform) → hold: search the window [t_trig, t_trig + window] for the sum peak (or each
/// channel's peak) → digitise (electronic noise + quantisation, channel order) → Busy for the dead time → WaitRearm until
/// the condition is false (edge trigger) → Armed. A pulse is dropped from the waveform once below 10⁻³ of its peak.</para>
/// Not modelled (RD-5): the network's own RC response, channel skew, hold droop, trigger-noise jitter.
/// </summary>
public sealed class ReadoutPulseProcessor
{
    private const double SupportFraction = 1e-3;
    private readonly ReadoutDevice _device;
    private readonly TriggerLogic _logic;
    private readonly HoldMode _hold;
    private readonly double _norm, _contribution;
    private readonly int _channels;

    public double RiseNs { get; }
    public double TailNs { get; }
    public double StepNs { get; }
    public double HoldWindowNs { get; }
    public double DeadTimeNs { get; }

    /// <summary>Time-to-peak of the continuous pulse, τ_r·τ_t/(τ_t − τ_r)·ln(τ_t/τ_r).</summary>
    public double PeakTimeNs { get; }

    /// <summary>Grid time of an isolated pulse's sampled peak.</summary>
    public double PeakGridNs { get; }

    /// <summary>Time after which a pulse is below 10⁻³ of its peak and leaves the waveform.</summary>
    public double SupportNs { get; }

    /// <summary>Trigger threshold in codes (the compared signal: sum for Sum, each channel for Or / And).</summary>
    public double ThresholdCodes { get; }

    public ReadoutPulseProcessor(ReadoutDevice device, ReadoutPulseConfig pulse, ReadoutTriggerConfig trigger)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(pulse);
        ArgumentNullException.ThrowIfNull(trigger);
        _device = device;
        _channels = device.Channels;
        var (riseSamples, tailSamples) = FrontEndParts.Default.PulseSamples;
        double nsPerSample = 1e9 / FrontEndParts.AdcSampleRateHz;
        RiseNs = pulse.RiseTimeNs ?? riseSamples * nsPerSample;
        TailNs = pulse.TailTimeNs ?? tailSamples * nsPerSample;
        StepNs = pulse.TimeStepNs;
        if (!(RiseNs > 0) || !(TailNs > RiseNs) || !(StepNs > 0) || !(pulse.DeadTimeNs >= 0)
            || pulse.PileUpContributionFraction is <= 0 or >= 1 || pulse.HoldWindowNs is <= 0)
            throw new ArgumentException("Pulse needs 0 < rise < tail, a positive step and window, dead time ≥ 0.");
        PeakTimeNs = RiseNs * TailNs / (TailNs - RiseNs) * Math.Log(TailNs / RiseNs);
        int k = (int)Math.Floor(PeakTimeNs / StepNs);
        double a = Raw(k * StepNs), b = Raw((k + 1) * StepNs);
        _norm = Math.Max(a, b);
        PeakGridNs = (a >= b ? k : k + 1) * StepNs;
        HoldWindowNs = pulse.HoldWindowNs ?? Math.Ceiling(PeakTimeNs / StepNs) * StepNs;
        DeadTimeNs = pulse.DeadTimeNs;
        SupportNs = TailNs * Math.Log(1.0 / (SupportFraction * _norm));
        _hold = pulse.Hold;
        _contribution = pulse.PileUpContributionFraction;
        _logic = trigger.Logic;
        if (_logic == TriggerLogic.And && device.Mode != ReadoutMode.FourOutputAnger)
            throw new ArgumentException("AND trigger is defined for the four-output readout only.");
        if (!double.IsFinite(trigger.Threshold)) throw new ArgumentOutOfRangeException(nameof(trigger));
        ThresholdCodes = trigger.Unit == ThresholdUnit.AdcCode ? trigger.Threshold : trigger.Threshold * device.CodesPerKeV;
    }

    private double Raw(double t) => t < 0 ? 0 : Math.Exp(-t / TailNs) - Math.Exp(-t / RiseNs);

    /// <summary>Normalised pulse u(t): 0 before arrival, 1 at an isolated pulse's sampled peak.</summary>
    public double Unit(double tNs) => Raw(tNs) / _norm;

    /// <summary>Trigger condition on given analogue channel amplitudes.</summary>
    public bool Triggers(ReadOnlySpan<double> channels, double sum)
    {
        switch (_logic)
        {
            case TriggerLogic.Sum: return sum >= ThresholdCodes;
            case TriggerLogic.Or:
                foreach (double c in channels) if (c >= ThresholdCodes) return true;
                return false;
            default:
                foreach (double c in channels) if (c < ThresholdCodes) return false;
                return true;
        }
    }

    /// <summary>An isolated hit (no other pulse within its support): identical to <see cref="Process"/> on a one-hit list —
    /// all channels share u, so the condition holds somewhere iff it holds at the peak, where u = 1.</summary>
    public ReadoutEvent? ProcessIsolated(ReadoutHit hit, IRandom noise)
    {
        ArgumentNullException.ThrowIfNull(hit);
        if (!Triggers(hit.Channels, hit.Sum)) return null;
        var codes = new double[_channels];
        for (int c = 0; c < _channels; c++) codes[c] = _device.Digitize(hit.Channels[c], noise);
        return new ReadoutEvent(hit.TimeNs + PeakGridNs, codes, 0, 1, 1.0);
    }

    private enum State { Armed, Busy, WaitRearm }

    /// <summary>Process a time-ordered hit stream; returns the recorded events (DominantHit indexes the input list).</summary>
    public List<ReadoutEvent> Process(IReadOnlyList<ReadoutHit> hits, IRandom noise)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(noise);
        for (int i = 1; i < hits.Count; i++)
            if (hits[i].TimeNs < hits[i - 1].TimeNs) throw new ArgumentException("Hits must be sorted by time.");
        var events = new List<ReadoutEvent>();
        var active = new List<int>();
        var channels = new double[_channels];
        int next = 0, n = hits.Count;
        double origin = 0;
        long step = 0;
        var state = State.Armed;
        double busyUntil = double.NegativeInfinity;
        while (true)
        {
            // Times relative to a hit are (origin − t_hit) + k·step, exact for the hit that set the origin.
            double t = origin + step * StepNs;
            active.RemoveAll(h => Since(hits[h], origin, step) > SupportNs);
            if (active.Count == 0)
            {
                if (next >= n) break;
                if (hits[next].TimeNs > t) { origin = hits[next].TimeNs; step = 0; t = origin; }
            }
            while (next < n && hits[next].TimeNs <= t) active.Add(next++);
            if (state == State.Busy && t >= busyUntil) state = State.WaitRearm;
            if (state != State.Busy)
            {
                bool condition = Condition(hits, active, origin, step, channels);
                if (state == State.WaitRearm)
                {
                    if (!condition) state = State.Armed;
                }
                else if (condition)
                {
                    step = Hold(hits, active, ref next, origin, step, noise, events);
                    busyUntil = origin + step * StepNs + DeadTimeNs;
                    state = State.Busy;
                }
            }
            step++;
        }
        return events;
    }

    private double Since(ReadoutHit hit, double origin, long step) => (origin - hit.TimeNs) + step * StepNs;

    private bool Condition(IReadOnlyList<ReadoutHit> hits, List<int> active, double origin, long step, double[] channels)
    {
        if (_logic == TriggerLogic.Sum) return SumAt(hits, active, origin, step) >= ThresholdCodes;
        Channels(hits, active, origin, step, channels);
        return Triggers(channels, 0);
    }

    private double SumAt(IReadOnlyList<ReadoutHit> hits, List<int> active, double origin, long step)
    {
        double s = 0;
        foreach (int h in active) s += hits[h].Sum * Unit(Since(hits[h], origin, step));
        return s;
    }

    private void Channels(IReadOnlyList<ReadoutHit> hits, List<int> active, double origin, long step, double[] channels)
    {
        Array.Clear(channels);
        foreach (int h in active)
        {
            double u = Unit(Since(hits[h], origin, step));
            if (u == 0) continue;
            var a = hits[h].Channels;
            for (int c = 0; c < channels.Length; c++) channels[c] += a[c] * u;
        }
    }

    // Search the hold window from the trigger step; returns the window's last grid step.
    private long Hold(IReadOnlyList<ReadoutHit> hits, List<int> active, ref int next, double origin, long triggerStep,
        IRandom noise, List<ReadoutEvent> events)
    {
        long steps = (long)Math.Floor(HoldWindowNs / StepNs + 1e-9);
        double best = double.NegativeInfinity;
        long starStep = triggerStep;
        double[]? peaks = _hold == HoldMode.IndependentPeaks ? Enumerable.Repeat(double.NegativeInfinity, _channels).ToArray() : null;
        var buffer = new double[_channels];
        for (long j = 0; j <= steps; j++)
        {
            long k = triggerStep + j;
            double t = origin + k * StepNs;
            while (next < hits.Count && hits[next].TimeNs <= t) active.Add(next++);
            double s = SumAt(hits, active, origin, k);
            if (s > best) { best = s; starStep = k; }
            if (peaks is not null)
            {
                Channels(hits, active, origin, k, buffer);
                for (int c = 0; c < _channels; c++) peaks[c] = Math.Max(peaks[c], buffer[c]);
            }
        }
        double tStar = origin + starStep * StepNs;
        var held = peaks ?? buffer;
        if (peaks is null) Channels(hits, active, origin, starStep, held);
        int dominant = -1, contributing = 0;
        double top = 0, total = 0;
        foreach (int h in active)
        {
            double w = hits[h].Sum * Unit(Since(hits[h], origin, starStep));
            total += w;
            if (w > top) { top = w; dominant = h; }
        }
        foreach (int h in active)
            if (hits[h].Sum * Unit(Since(hits[h], origin, starStep)) >= _contribution * total) contributing++;
        var codes = new double[_channels];
        for (int c = 0; c < _channels; c++) codes[c] = _device.Digitize(held[c], noise);
        events.Add(new ReadoutEvent(tStar, codes, dominant, contributing, total > 0 ? top / total : 0));
        return triggerStep + steps;
    }
}
