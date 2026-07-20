namespace Gcam.Simulation;

/// <summary>
/// Counting-system DEAD TIME applied to a timed event stream: while the DAQ processes one pulse it cannot register
/// another, so counts are lost at rate. Two classic models:
/// <list type="bullet">
/// <item><b>Non-paralyzable</b> — after a RECORDED event the system is dead for τ, then the next arrival is
/// recorded and starts a fresh dead period. Recorded rate m = R/(1+Rτ), saturating at 1/τ.</item>
/// <item><b>Paralyzable</b> — EVERY arrival (recorded or not) restarts the dead period, so an event is recorded
/// only if the preceding arrival was more than τ ago. m = R·exp(−Rτ) — it peaks at R = 1/τ then falls (the system
/// paralyses at high rate).</item>
/// </list>
/// The arrival samples must be sorted ascending. τ is given in the same (ADC-sample) units as the arrivals.
/// </summary>
public static class DeadTime
{
    /// <summary>Non-paralyzable (non-extending) dead time: count events, skipping any that arrive within τ of the
    /// last RECORDED event.</summary>
    public static long NonParalyzable(IReadOnlyList<StreamEvent> events, double tauSamples)
    {
        if (!(tauSamples > 0.0)) return events.Count;
        long recorded = 0;
        double deadUntil = double.NegativeInfinity;
        foreach (var e in events)
        {
            if (e.ArrivalSample >= deadUntil)
            {
                recorded++;
                deadUntil = e.ArrivalSample + tauSamples;
            }
        }
        return recorded;
    }

    /// <summary>Paralyzable (extending) dead time: an event is recorded only if the PREVIOUS arrival (recorded or
    /// not) was more than τ earlier; every arrival extends the dead period.</summary>
    public static long Paralyzable(IReadOnlyList<StreamEvent> events, double tauSamples)
    {
        if (!(tauSamples > 0.0)) return events.Count;
        long recorded = 0;
        double prev = double.NegativeInfinity;
        foreach (var e in events)
        {
            if (e.ArrivalSample - prev > tauSamples) recorded++;
            prev = e.ArrivalSample;   // every arrival extends the dead period
        }
        return recorded;
    }
}
