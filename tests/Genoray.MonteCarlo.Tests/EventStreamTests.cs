using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>The MC→RTL event-stream bridge: the crystal-Compton deposit spectrum tapped per event and
/// overlaid with a Poisson arrival process, ready to drive the RTL trapezoidal shaper (cocotb). Checks
/// the physics (deposit spectrum bounded by the line energy, photopeak + continuum both present) and
/// the timing (arrivals monotonic, mean rate matches the request).</summary>
public class EventStreamTests
{
    private const double Fs = 100e6;

    private static SimulationConfig Base()
        => new() { PhotonCount = 600_000, Seed = 12345,
                   Source = new SourceConfig { Position = [0, 0, 0.0], EnergyKeV = 661.7,
                                               BranchingRatio = 1.0, DirectionalBiasing = true } };

    [Fact]
    public void Deposits_AreBoundedAndSpanPhotopeakAndContinuum()
    {
        var events = new EventStreamStudy().Generate(Base(), countRateCps: 500e3, adcSampleRateHz: Fs, maxEvents: 1500);
        Assert.NotEmpty(events);

        int photopeak = 0, continuum = 0;
        foreach (var ev in events)
        {
            // A deposited energy can never exceed the incident line energy (small epsilon for rounding).
            Assert.True(ev.EnergyKeV <= 661.7 + 0.5, $"deposit {ev.EnergyKeV} exceeds the 661.7 keV line");
            Assert.True(ev.EnergyKeV >= 0.0, $"negative deposit {ev.EnergyKeV}");
            if (ev.EnergyKeV > 620.0) photopeak++;
            else if (ev.EnergyKeV > 50.0) continuum++;
        }
        // A Cs-137 crystal spectrum must show BOTH the full-absorption photopeak and Compton continuum.
        Assert.True(photopeak > events.Count / 4, $"too few photopeak events ({photopeak}/{events.Count})");
        Assert.True(continuum > events.Count / 20, $"no Compton continuum ({continuum}/{events.Count})");
    }

    [Fact]
    public void Arrivals_AreMonotonic()
    {
        var events = new EventStreamStudy().Generate(Base(), countRateCps: 500e3, adcSampleRateHz: Fs, maxEvents: 1000);
        for (int i = 1; i < events.Count; i++)
            Assert.True(events[i].ArrivalSample >= events[i - 1].ArrivalSample,
                $"arrival {i} ({events[i].ArrivalSample}) precedes {i - 1} ({events[i - 1].ArrivalSample})");
    }

    [Theory]
    [InlineData(200e3)]
    [InlineData(1000e3)]
    public void MeanGap_MatchesRequestedRate(double rateCps)
    {
        var events = new EventStreamStudy().Generate(Base(), rateCps, adcSampleRateHz: Fs, maxEvents: 1500);
        var (_, _, _, meanGap) = EventStreamStudy.Summary(events);
        double expected = Fs / rateCps;   // samples between events
        Assert.True(System.Math.Abs(meanGap - expected) / expected < 0.10,
            $"mean gap {meanGap:F1} vs expected {expected:F1} samples ({rateCps / 1e3:F0} kcps)");
    }

    [Fact]
    public void Background_MergesExtraUncodedEvents()
    {
        var clean = new EventStreamStudy().Generate(Base(), countRateCps: 500e3, adcSampleRateHz: Fs, maxEvents: 1000);

        var cfg = Base();
        cfg.Background = new BackgroundConfig { BackgroundToSignalRatio = 0.5, EnergyKeV = 200.0 };
        var withBg = new EventStreamStudy().Generate(cfg, countRateCps: 500e3, adcSampleRateHz: Fs, maxEvents: 1000);

        // BSR 0.5 adds ~0.5x as many background events on top of the 1000 source events.
        Assert.True(withBg.Count > clean.Count + 200,
            $"background should add events: clean {clean.Count} vs with-bg {withBg.Count}");
        // Arrivals stay sorted after the merge.
        for (int i = 1; i < withBg.Count; i++)
            Assert.True(withBg[i].ArrivalSample >= withBg[i - 1].ArrivalSample);
    }

    [Fact]
    public void DarkCounts_AddSubKeVPulses()
    {
        var cfg = Base();
        cfg.Background = new BackgroundConfig { BackgroundToSignalRatio = 0.0, DarkCountRateKcps = 200.0 };
        var events = new EventStreamStudy().Generate(cfg, countRateCps: 500e3, adcSampleRateHz: Fs, maxEvents: 1000);

        int subKeV = 0;
        foreach (var ev in events) if (ev.EnergyKeV < 10.0) subKeV++;
        Assert.True(subKeV > 50, $"DCR should inject many sub-keV pulses, got {subKeV}");
    }

    [Fact]
    public void Text_RoundTripsHeaderAndColumns()
    {
        var events = new EventStreamStudy().Generate(Base(), countRateCps: 300e3, adcSampleRateHz: Fs, maxEvents: 200);
        string text = EventStreamStudy.ToText(events, 300e3, Fs, "unit-test");
        Assert.Contains("count_rate_cps=300000", text);
        Assert.Contains("arrival_sample energy_keV", text);
        // Every data line is "<long> <energy>" — parseable back to the same event count.
        int dataLines = 0;
        foreach (var line in text.Split('\n'))
        {
            var s = line.Trim();
            if (s.Length == 0 || s.StartsWith("#")) continue;
            var parts = s.Split(' ');
            Assert.Equal(2, parts.Length);
            Assert.True(long.TryParse(parts[0], out _));
            dataLines++;
        }
        Assert.Equal(events.Count, dataLines);
    }
}
