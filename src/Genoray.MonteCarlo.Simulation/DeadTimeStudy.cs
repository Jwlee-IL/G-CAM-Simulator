using Genoray.MonteCarlo.Configuration;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One true count rate: the recorded rate and live fraction under both dead-time models, MC vs analytic.</summary>
public sealed record DeadTimeRow(
    double TrueRateCps,          // nominal source rate dialed in (x-axis); with background off, ≈ the effective rate
    double Rtau,                 // R·τ, the dead-time load — R is the EFFECTIVE arrival rate (source + any background)
    double RecordedNonParaCps,   // MC recorded rate, non-paralyzable
    double RecordedParaCps,      // MC recorded rate, paralyzable
    double LiveNonPara,          // live fraction = recorded / true
    double LivePara,
    double AnalyticNonParaCps,   // R/(1+Rτ)
    double AnalyticParaCps);     // R·exp(−Rτ)

/// <summary>
/// Studies counting-system DEAD TIME: how the RECORDED count rate falls below the true rate as the DAQ's per-pulse
/// dead time τ starts to matter, for the non-paralyzable and paralyzable models. Reuses the timed MC event stream
/// (<see cref="EventStreamStudy"/>, the same stream pile-up uses) so the loss comes from the real Poisson arrival
/// statistics, and cross-checks it against the analytic m = R/(1+Rτ) and m = R·exp(−Rτ). At high rate the
/// non-paralyzable rate saturates at 1/τ while the paralyzable rate turns over and collapses — the classic curves.
/// </summary>
public sealed class DeadTimeStudy
{
    private readonly EventStreamStudy _stream;

    public DeadTimeStudy(ISimulationFactory? factory = null)
    {
        _stream = new EventStreamStudy(factory);
    }

    public DeadTimeRow[] Run(SimulationConfig config, double[] trueRatesCps, double deadTimeUs,
                             double adcSampleRateHz, int maxEvents)
    {
        double tauSec = deadTimeUs * 1e-6;
        double tauSamples = tauSec * adcSampleRateHz;
        var rows = new List<DeadTimeRow>();
        foreach (double r in trueRatesCps)
        {
            var events = _stream.Generate(config, r, adcSampleRateHz, maxEvents);
            int nTrue = events.Count;
            if (nTrue == 0) continue;

            // The dead-time filters see EVERY arrival — including any ambient background / dark-count pulses
            // EventStreamStudy merges into the train. Derive the effective true arrival rate from the generated
            // stream itself (nTrue over its time span), not the nominal source rate r, so m = R/(1+Rτ) and
            // R·exp(−Rτ) are compared against the rate the DAQ actually experiences. For a clean source-only
            // stream this equals r to within 1/nTrue; with background on it correctly counts the extra arrivals.
            double spanSec = events[^1].ArrivalSample / adcSampleRateHz;
            double rEff = spanSec > 0.0 ? nTrue / spanSec : r;

            double liveNon = (double)DeadTime.NonParalyzable(events, tauSamples) / nTrue;
            double livePara = (double)DeadTime.Paralyzable(events, tauSamples) / nTrue;
            double rtau = rEff * tauSec;

            rows.Add(new DeadTimeRow(
                r, rtau,
                rEff * liveNon, rEff * livePara,
                liveNon, livePara,
                rEff / (1.0 + rtau), rEff * Math.Exp(-rtau)));
        }
        return rows.ToArray();
    }

    public static string ToCsv(DeadTimeRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("true_cps,r_tau,rec_nonpara_cps,rec_para_cps,live_nonpara,live_para,analytic_nonpara_cps,analytic_para_cps");
        foreach (var r in rows)
            sb.AppendLine($"{r.TrueRateCps:F0},{r.Rtau:F4},{r.RecordedNonParaCps:F0},{r.RecordedParaCps:F0}," +
                          $"{r.LiveNonPara:F4},{r.LivePara:F4},{r.AnalyticNonParaCps:F0},{r.AnalyticParaCps:F0}");
        return sb.ToString();
    }
}
