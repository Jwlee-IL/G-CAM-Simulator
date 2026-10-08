using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Tests;

/// <summary>TODO-19, RD-3 / RD-6: the time-domain four-output model — trigger truth table with explicit units, the
/// isolated-pulse shortcut equal to the full time-domain path, and pile-up: pulses add in every channel, so a piled-up
/// event's Anger position is the light centroid of its hits weighted by their pulse heights at the hold instant. Noise is
/// switched off (NoiseKeV = 0, ENOB = 30 → ADC noise ≈ 1e-6 code); the remaining error is code rounding, |δc| ≤ ½ per
/// channel, which moves X = Σ s_c·c_c / Σ c_c by at most Σ_c ½·|s_c − X| / Σ ≤ 4/Σ (four channels, |s_c − X| ≤ 2).</summary>
public class ReadoutPulseProcessorTests
{
    private static readonly CrystalArrayGeometry Crystals = new(6, 6, 3.2, 0.2, 10.0);

    private static (ReadoutDevice Device, ReadoutPulseProcessor Processor) Chain(TriggerLogic logic = TriggerLogic.Sum,
        double threshold = 50, ThresholdUnit unit = ThresholdUnit.KeVEquivalent, double deadNs = 0)
    {
        var cfg = new ReadoutConfig
        {
            Mode = ReadoutMode.FourOutputAnger,
            Network = new ChargeNetworkConfig { Topology = NetworkTopology.IdealBilinear },
            Optics = new ReadoutOpticsConfig { PhotonsPerBin = 2000, DepthBins = 2 },
            Digitizer = new ReadoutDigitizerConfig { NoiseKeV = 0, Enob = 30 },
            Pulse = new ReadoutPulseConfig { DeadTimeNs = deadNs },
        };
        var device = new ReadoutDevice(Crystals, cfg, 3);
        var trigger = new ReadoutTriggerConfig { Logic = logic, Threshold = threshold, Unit = unit };
        return (device, new ReadoutPulseProcessor(device, cfg.Pulse, trigger));
    }

    private static ReadoutHit Hit(ReadoutDevice device, int ix, int iy, double energy, double timeNs)
    {
        var ch = new double[device.Channels];
        double sum = device.Expected([new InteractionSite(Crystals.CenterX(ix), Crystals.CenterY(iy), -5, 0, energy, ix, iy)], ch);
        return new ReadoutHit(timeNs, ch, sum);
    }

    private static double X(ReadOnlySpan<double> c) => (c[1] + c[3] - c[0] - c[2]) / (c[0] + c[1] + c[2] + c[3]);

    [Fact]
    public void TriggerTruthTable_WithExplicitUnits()
    {
        var (device, sum) = Chain(TriggerLogic.Sum, 100, ThresholdUnit.AdcCode);
        var (_, or) = Chain(TriggerLogic.Or, 100, ThresholdUnit.AdcCode);
        var (_, and) = Chain(TriggerLogic.And, 100, ThresholdUnit.AdcCode);
        double[] spread = [30, 30, 30, 30], one = [110, 0, 0, 0], all = [110, 110, 110, 110];
        Assert.True(sum.Triggers(spread, 120)); Assert.False(or.Triggers(spread, 120)); Assert.False(and.Triggers(spread, 120));
        Assert.True(sum.Triggers(one, 110)); Assert.True(or.Triggers(one, 110)); Assert.False(and.Triggers(one, 110));
        Assert.True(and.Triggers(all, 440));
        var (_, kev) = Chain(TriggerLogic.Or, 50, ThresholdUnit.KeVEquivalent);
        Assert.Equal(50 * device.CodesPerKeV, kev.ThresholdCodes, 12);
        // AND at T keV-equivalent per channel needs ≥ 4·T in total for a centre crystal of the ideal divider.
        var (_, and150) = Chain(TriggerLogic.And, 150);
        Assert.Null(and150.ProcessIsolated(Hit(device, 2, 2, 500, 0), new DefaultRandom(1)));
    }

    [Fact]
    public void IsolatedPulse_TimeDomainEqualsTheShortcut()
    {
        var (device, processor) = Chain();
        var hit = Hit(device, 1, 4, 661.7, 12_345.6);
        var direct = processor.ProcessIsolated(hit, new DefaultRandom(8))!;
        var events = processor.Process([hit], new DefaultRandom(8));
        Assert.Single(events);
        Assert.Equal(direct.Codes, events[0].Codes);
        Assert.Equal(direct.HoldTimeNs, events[0].HoldTimeNs);
        Assert.Equal(1, events[0].ContributingHits);
    }

    [Fact]
    public void CoincidentPileUp_IsTheChargeWeightedCentroid()
    {
        var (device, processor) = Chain();
        var a = Hit(device, 1, 2, 400, 1000);
        var b = Hit(device, 4, 2, 250, 1000);
        var ev = processor.Process([a, b], new DefaultRandom(2)).Single();
        double expected = (X(a.Channels) * a.Sum + X(b.Channels) * b.Sum) / (a.Sum + b.Sum);
        double sum = ev.Codes.Sum();
        Assert.Equal(a.Sum + b.Sum, sum, 2.0);                                  // ½ code per channel
        Assert.Equal(expected, X(ev.Codes), 4.0 / sum);
        Assert.Equal(2, ev.ContributingHits);
    }

    /// <summary>A hit 40 ns later: the trigger fires at the first grid point where the sum reaches the threshold; the hold
    /// samples the grid point t* of the window [trigger, trigger + window] where the SUM peaks, and each hit enters with
    /// its pulse height there, u(t* − t_h). The expectation is built independently by scanning the same grid.</summary>
    [Fact]
    public void DelayedPileUp_WeightsHitsByPulseHeightAtTheHold()
    {
        var (device, processor) = Chain();
        var a = Hit(device, 0, 0, 500, 1000);
        var b = Hit(device, 5, 5, 300, 1040);
        var ev = processor.Process([a, b], new DefaultRandom(2)).Single();
        double S(int k) => a.Sum * processor.Unit(k * processor.StepNs) + b.Sum * processor.Unit(k * processor.StepNs - 40);
        int trigger = 0;
        while (S(trigger) < processor.ThresholdCodes) trigger++;
        int window = (int)Math.Floor(processor.HoldWindowNs / processor.StepNs + 1e-9);
        double best = double.NegativeInfinity, tStar = 0;
        for (int k = trigger; k <= trigger + window; k++)
            if (S(k) > best) { best = S(k); tStar = 1000 + k * processor.StepNs; }
        double ua = processor.Unit(tStar - 1000), ub = processor.Unit(tStar - 1040);
        var held = new double[4];
        for (int c = 0; c < 4; c++) held[c] = a.Channels[c] * ua + b.Channels[c] * ub;
        Assert.Equal(tStar, ev.HoldTimeNs, 6);
        Assert.Equal(X(held), X(ev.Codes), 4.0 / held.Sum());
        Assert.True(Math.Abs(X(ev.Codes) - X(a.Channels)) > 0.1, "the piled position must move away from the first hit");
    }

    [Fact]
    public void HitsBeyondThePulseSupport_AreSeparateEvents()
    {
        var (device, processor) = Chain();
        double gap = processor.SupportNs + 1000;
        var events = processor.Process([Hit(device, 1, 1, 661.7, 0), Hit(device, 4, 4, 661.7, gap)], new DefaultRandom(3));
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(1, e.ContributingHits));
        Assert.Equal([0, 1], events.Select(e => e.DominantHit));
    }

    /// <summary>A second pulse arriving while the first still holds the sum above threshold cannot trigger (edge trigger,
    /// re-arm only below threshold): it is either piled into the first event or lost; never its own event.</summary>
    [Fact]
    public void PulseDuringTheBusyTail_IsNotItsOwnEvent()
    {
        var (device, processor) = Chain(deadNs: 200);
        var events = processor.Process([Hit(device, 1, 1, 661.7, 0), Hit(device, 4, 4, 661.7, 400)], new DefaultRandom(4));
        Assert.Single(events);
        Assert.Equal(0, events[0].DominantHit);
    }

    [Fact]
    public void UnsortedHits_AreRefused()
    {
        var (device, processor) = Chain();
        Assert.Throws<ArgumentException>(() => processor.Process([Hit(device, 1, 1, 100, 10), Hit(device, 1, 1, 100, 5)], new DefaultRandom(1)));
    }
}
