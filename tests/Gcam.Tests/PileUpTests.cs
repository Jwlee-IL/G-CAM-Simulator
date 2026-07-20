using System.Linq;
using Gcam.Configuration;
using Gcam.Detector;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>
/// Random-coincidence pile-up: overlapping pulses SUM into one recorded event, adding a self-convolution
/// continuum above the photopeak and a count-rate-dependent throughput loss. The energies are the real MC
/// spectrum; only their time coincidence (within the shaper's resolving time) is added.
/// </summary>
public class PileUpTests
{
    // --- ApplyPileUp merge logic ---

    [Fact]
    public void CloseEvents_MergeAndSumEnergy()
    {
        StreamEvent[] s = [new(0, 100), new(5, 200), new(100, 300)];   // 0&5 within τ=11; 100 far
        var piled = EventStreamStudy.ApplyPileUp(s, 11.0);
        Assert.Equal(2, piled.Count);
        Assert.Equal(300.0, piled[0].EnergyKeV, 6);   // 100 + 200 summed
        Assert.Equal(300.0, piled[1].EnergyKeV, 6);   // lone event unchanged
    }

    [Fact]
    public void FarEvents_DoNotMerge()
    {
        StreamEvent[] s = [new(0, 100), new(50, 200), new(100, 300)];
        var piled = EventStreamStudy.ApplyPileUp(s, 11.0);
        Assert.Equal(3, piled.Count);
    }

    [Fact]
    public void Pileup_IsExtending_ABurstPilesToThreefold()
    {
        // Each gap (8) is below τ=11, so the window keeps re-extending: all three sum into one.
        StreamEvent[] s = [new(0, 100), new(8, 100), new(16, 100)];
        var piled = EventStreamStudy.ApplyPileUp(s, 11.0);
        Assert.Single(piled);
        Assert.Equal(300.0, piled[0].EnergyKeV, 6);
    }

    [Fact]
    public void ResolvingTime_FromShaperPulse()
    {
        // rise 1 + 2·tail 5 = 11 samples; a faster (shorter-tail) shaper resolves better.
        Assert.Equal(11.0, EventStreamStudy.ResolvingSamples(1.0, 5.0), 6);
        Assert.True(EventStreamStudy.ResolvingSamples(1.0, 2.0) < EventStreamStudy.ResolvingSamples(1.0, 5.0));
    }

    // --- Physics: throughput drops and a sum continuum grows with count rate ---

    [Fact]
    public void HigherRate_LosesThroughput_AndBuildsSumContinuum()
    {
        var cfg = new SimulationConfig { PhotonCount = 400_000, Seed = 12345 };
        const double fs = 125e6;
        double tau = EventStreamStudy.ResolvingSamples(Waveform.TauRiseSamples, Waveform.TauSamples);
        var study = new EventStreamStudy();

        var low = study.Generate(cfg, 50e3, fs, 60_000);
        var high = study.Generate(cfg, 2e6, fs, 60_000);
        var lowP = EventStreamStudy.ApplyPileUp(low, tau);
        var highP = EventStreamStudy.ApplyPileUp(high, tau);

        double lowThru = (double)lowP.Count / low.Count;
        double highThru = (double)highP.Count / high.Count;
        Assert.True(highThru < lowThru, $"higher rate should lose more throughput ({highThru} vs {lowThru})");
        Assert.True(lowThru > 0.98, $"low rate barely piles up, got {lowThru}");

        // Sum continuum: counts ABOVE the 662 line (impossible for singles of a 662 source) appear only via pile-up
        // and grow with rate.
        double primary = 661.7;
        double lowAbove = lowP.Count(e => e.EnergyKeV > primary * 1.30) / (double)lowP.Count;
        double highAbove = highP.Count(e => e.EnergyKeV > primary * 1.30) / (double)highP.Count;
        Assert.True(highAbove > lowAbove * 3.0, $"sum continuum should grow with rate ({highAbove} vs {lowAbove})");

        // The sum peak reaches ~2× the line.
        Assert.Contains(highP, e => e.EnergyKeV > 1.9 * primary);
    }

    [Fact]
    public void EnergyIsConserved_NoCountsAppearFromNowhere()
    {
        var cfg = new SimulationConfig { PhotonCount = 200_000, Seed = 7 };
        var stream = new EventStreamStudy().Generate(cfg, 1e6, 125e6, 40_000);
        var piled = EventStreamStudy.ApplyPileUp(stream, 11.0);
        double before = stream.Sum(e => e.EnergyKeV);
        double after = piled.Sum(e => e.EnergyKeV);
        Assert.Equal(before, after, 3);   // merging sums energy — total deposited energy is conserved
        Assert.True(piled.Count <= stream.Count);
    }
}
