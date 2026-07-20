using Gcam.Configuration;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>
/// Counting-system dead time: the recorded rate falls below the true rate as the DAQ's per-pulse dead time τ
/// matters. Non-paralyzable saturates at 1/τ; paralyzable peaks at R=1/τ then collapses. The MC event stream
/// reproduces the analytic m = R/(1+Rτ) and m = R·exp(−Rτ).
/// </summary>
public class DeadTimeTests
{
    // --- Filter logic ---

    [Fact]
    public void ZeroDeadTime_RecordsEverything()
    {
        StreamEvent[] s = [new(0, 1), new(3, 1), new(4, 1), new(50, 1)];
        Assert.Equal(4, DeadTime.NonParalyzable(s, 0.0));
        Assert.Equal(4, DeadTime.Paralyzable(s, 0.0));
    }

    [Fact]
    public void NonParalyzable_DeadOnlyAfterRecordedEvents()
    {
        // τ=10. Record 0 (dead→10), drop 5, record 12 (dead→22), record 100.
        StreamEvent[] s = [new(0, 1), new(5, 1), new(12, 1), new(100, 1)];
        Assert.Equal(3, DeadTime.NonParalyzable(s, 10.0));
    }

    [Fact]
    public void Paralyzable_EveryArrivalExtendsTheDeadPeriod()
    {
        // τ=10. Record 0, drop 5 (gap 5), drop 12 (gap 7 from 5 — extended), record 100 (gap 88).
        StreamEvent[] s = [new(0, 1), new(5, 1), new(12, 1), new(100, 1)];
        Assert.Equal(2, DeadTime.Paralyzable(s, 10.0));
    }

    [Fact]
    public void Paralyzable_LosesMoreThanNonParalyzable_UnderLoad()
    {
        // A tight burst: non-paralyzable recovers after each recorded event; paralyzable stays paralysed.
        StreamEvent[] s = [new(0, 1), new(4, 1), new(8, 1), new(12, 1), new(16, 1), new(100, 1)];
        long np = DeadTime.NonParalyzable(s, 10.0);
        long p = DeadTime.Paralyzable(s, 10.0);
        Assert.True(p <= np, $"paralyzable should lose at least as much: para {p} vs non-para {np}");
    }

    // --- Study: MC reproduces the analytic curves; the paralyzable rate turns over ---

    [Fact]
    public void MatchesAnalytic_AndParalyzableTurnsOver()
    {
        var cfg = new SimulationConfig { PhotonCount = 300_000, Seed = 12345 };
        const double fs = 125e6, tauUs = 1.0;
        double[] rates = [1e5, 1e6, 1e7];
        var rows = new DeadTimeStudy().Run(cfg, rates, tauUs, fs, maxEvents: 80_000);

        Assert.Equal(3, rows.Length);
        foreach (var r in rows)
        {
            // MC recorded rate tracks the analytic formula (Poisson-stream statistics). Skip the tight paralyzable
            // check under extreme load (Rτ ≫ 1) where only a handful of events survive → large relative variance.
            Assert.InRange(r.RecordedNonParaCps / r.AnalyticNonParaCps, 0.95, 1.05);
            if (r.Rtau <= 1.5)
                Assert.InRange(r.RecordedParaCps / System.Math.Max(r.AnalyticParaCps, 1.0), 0.9, 1.1);
        }

        var atPeak = System.Array.Find(rows, r => r.TrueRateCps == 1e6)!;   // R = 1/τ, paralyzable peak
        var atHigh = System.Array.Find(rows, r => r.TrueRateCps == 1e7)!;   // far past 1/τ → paralysis
        Assert.True(atHigh.RecordedParaCps < atPeak.RecordedParaCps * 0.2, "paralyzable rate should collapse past 1/τ");
        Assert.True(atHigh.RecordedNonParaCps > atPeak.RecordedNonParaCps, "non-paralyzable keeps rising toward 1/τ");
    }
}
