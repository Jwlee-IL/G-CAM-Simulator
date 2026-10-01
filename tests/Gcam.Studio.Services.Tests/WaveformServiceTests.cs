using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Core.Plotting;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;
using Xunit;

namespace Gcam.Studio.Services.Tests;

public sealed class WaveformServiceTests(ITestOutputHelper output)
{
    private static FrontEndChain Chain(int preamp = 1, int scintillator = 0, int sensor = 0)
        => new(FrontEndMaterials.Scintillators[scintillator], FrontEndParts.Sensors[sensor], FrontEndParts.Preamps[preamp]);

    private static AcquisitionSnapshot Snapshot(DetectedEvent[]? events = null, FrontEndChain? chain = null,
        double live = 2, double gainSigma = 0)
    {
        events ??= [new(1, 2, 661.7, 1)];
        var flood = new DetectorImage(30, 30);
        foreach (var e in events) flood.Add(e.PixelX, e.PixelY, 1);
        var image = new ImagingResult(flood.ReadOnlyCopy(), -8.7, .6, null, 0, 0, null, events.Length, TimeSpan.Zero);
        return new(live, events.Length, events.Length / live, 1, false, image, Array.AsReadOnly(events), TimeSpan.Zero, true)
        { Detector = new DetectorSettings { Chain = chain ?? Chain(), GainSigma = gainSigma }, Optics = new OpticsSettings() };
    }

    [Theory]
    [InlineData(0, "GAGG")]
    [InlineData(1, "NaI")]
    [InlineData(2, "LYSO")]
    [InlineData(3, "BGO")]
    public void Chain_SetsTransportMaterialWithoutChangingEngineDefaults(int index, string material)
    {
        var config = SimulationService.BuildConfig([new SceneSource()], new(), new() { Chain = Chain(scintillator: index) });
        Assert.Equal(material, config.Detector.Material);
        Assert.Equal("ideal", new SimulationConfig().Detector.Material);
        Assert.DoesNotContain(FrontEndMaterials.Scintillators, s => s.Name == "CsI(Tl)");
    }

    [Fact]
    public void Chain_RejectsUnsupportedMaterial()
    {
        var chain = Chain() with { Scintillator = FrontEndParts.Scintillators.Single(s => s.Name == "CsI(Tl)") };
        Assert.Throws<ArgumentException>(() => SimulationService.BuildConfig([new SceneSource()], new(), new() { Chain = chain }));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task Response_AgreesAcrossWaveformSpectrumAndImaging(int preamp)
    {
        var snapshot = Snapshot([new(1, 2, 661.7, 1), new(3, 4, 32.1, 1.000001)], Chain(preamp), gainSigma: .03);
        var wave = await new WaveformService().ProcessAsync(snapshot, new(0));
        var measurement = new MeasurementStage(snapshot.Detector, 30, 30);
        Assert.Equal(2, wave.Events.Count);
        foreach (var e in wave.Events) Assert.Equal(measurement.Measure(snapshot.Events[e.Index], e.Index), e.AmplitudeKeV);
        var lines = Isotopes.Get("Cs-137").Lines.Select(l => new SpectrumLine("Cs-137", l.EnergyKeV)).ToArray();
        var spectrum = await new SpectrumService().ProcessAsync(Guid.NewGuid(), snapshot.Events, lines,
            new() { Detector = snapshot.Detector, PixelsX = 30, PixelsY = 30 });
        var expected = new double[SpectrumService.BinCount];
        double max = lines.Max(l => l.EnergyKeV) * 1.15;
        foreach (var e in wave.Events) expected[(int)(e.AmplitudeKeV / max * expected.Length)]++;
        Assert.Equal(expected, spectrum.Counts);
        Assert.Equal(snapshot.Chain.ToString(), spectrum.Chain);
        Assert.Equal(new FrontEndModel(snapshot.Chain.BuildConfig()).FwhmFraction(662), spectrum.Resolution662);
        var imaging = await new ImagingService().ProcessAsync(Guid.NewGuid(), snapshot, [new SceneSource()], new(), new());
        var channel = imaging.Channels.Single(c => c.Isotope == "Cs-137");
        Assert.Equal(1, channel.Image.EffectiveCounts);
        Assert.Equal(1, channel.Image.Flood[1, 2]);
        Assert.Equal(0, channel.Image.Flood[3, 4]);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Rasterizer_MatchesLegacyWithoutSecondIntrinsicSmear(int preamp)
    {
        var p = Chain(preamp).PulseSamples;
        var events = new[] { (100L, 662.0) };
        var legacy = Waveform.Rasterize(events, Waveform.DefaultAdc, p.TailSamples, p.RiseSamples, intrinsicFwhm: 0, tailPad: 1024);
        var bounded = WindowRasterizer.Rasterize(events, legacy.Length, Waveform.DefaultAdc, p.TailSamples, p.RiseSamples);
        Assert.Equal(legacy, bounded);
    }

    [Fact]
    public async Task Window_PreservesRequestedLengthAndPrecedingPulseAcrossPixels()
    {
        var snapshot = Snapshot([new(1, 2, 662, .9999975), new(3, 4, 662, 1), new(5, 6, 662, 1.00001)]);
        var service = new WaveformService();
        var withHistory = await service.ProcessAsync(snapshot, new(1, Ideal: true));
        var isolated = await service.ProcessAsync(Snapshot([snapshot.Events[1]]), new(0, Ideal: true));
        Assert.Equal(1250, withHistory.Adc.Y.Length);
        Assert.Equal(-2, withHistory.Adc.Origin);
        Assert.Equal(.008, withHistory.Adc.Step);
        Assert.Single(withHistory.Events);
        Assert.Equal(1, withHistory.Events[0].Index);
        // Preceding event starts before the display, but its real ADC tail reaches the pretrigger region.
        Assert.True(withHistory.Adc.Y.Take(250).Any(v => v > 0));
        Assert.All(isolated.Adc.Y.Take(250), v => Assert.Equal(0, v));
        Assert.Equal(withHistory.Adc.Origin, withHistory.Shaped.Origin);
        Assert.Equal(withHistory.Adc.Step, withHistory.Shaped.Step);
    }

    [Fact]
    public async Task Ideal_HasNoNoiseOrSmearAndKeepsTail()
    {
        var service = new WaveformService();
        var snapshot = Snapshot(gainSigma: .2);
        var view = await service.ProcessAsync(snapshot, new(Ideal: true));
        Assert.Equal(new MeasurementStage(snapshot.Detector, 30, 30).Amplitude(snapshot.Events[0]), Assert.Single(view.Events).AmplitudeKeV);
        Assert.All(view.Adc.Y.Take(250), v => Assert.Equal(0, v));
        Assert.True(view.Adc.Y[250] > view.Adc.Y[251]);
        Assert.Contains("Ideal shaper stimulus", view.Note);
        var empty = Snapshot([], live: 1);
        var quiet = await service.ProcessAsync(empty, new(Ideal: true));
        Assert.All(quiet.Adc.Y, v => Assert.Equal(0, v));
        var noise = await service.ProcessAsync(empty, new());
        Assert.Equal(1250, noise.Adc.Y.Length);
        Assert.Contains(noise.Adc.Y, v => v != 0);
    }

    [Fact]
    public async Task TimeBase_IsOriginRelativeAtLateAcquisitionTime()
    {
        var snapshot = Snapshot([new(1, 2, 662, 100000)], live: 100001);
        var view = await new WaveformService().ProcessAsync(snapshot, new());
        Assert.Equal(1250, view.Adc.Y.Length);
        Assert.Equal(100000, view.Events.Single().AcquisitionTimeS);
        Assert.Equal(0, view.Events.Single().RelativeTimeUs);
    }

    [Fact]
    public async Task RateStudy_IsDeterministicAndLeavesAcquisitionUntouched()
    {
        var events = Enumerable.Range(0, 40).Select(i => new DetectedEvent(i % 30, 2, 662, 1 + i * .01)).ToArray();
        var snapshot = Snapshot(events);
        var copy = events.ToArray();
        var service = new WaveformService();
        var settings = new WaveformSettings(20, RateStudy: true, RateKcps: 1000);
        var a = await service.ProcessAsync(snapshot, settings);
        var b = await service.ProcessAsync(snapshot, settings);
        Assert.Equal(a.Adc.Y, b.Adc.Y); Assert.Equal(a.Shaped.Y, b.Shaped.Y);
        Assert.True(a.Events.Count > 1);
        Assert.Equal(copy, snapshot.Events);
        Assert.Equal(40, snapshot.Counts); Assert.Equal(2, snapshot.LiveTimeS);
        Assert.Contains("not the measured rate", a.Note);
        Assert.All(a.Events, e => Assert.Equal(copy[e.Index].ArrivalTimeS, e.AcquisitionTimeS));
    }

    [Fact]
    public async Task AcquiredChain_IsUsedAfterPendingSelectionChanges()
    {
        var service = new SpectrumService();
        var snapshot = Snapshot(chain: Chain(3, 3, 2));
        var settings = new SpectrumSettings { Detector = snapshot.Detector, PixelsX = 30, PixelsY = 30 };
        var lines = new[] { new SpectrumLine("Cs-137", 661.7) };
        var id = Guid.NewGuid();
        var first = await service.ProcessAsync(id, snapshot.Events, lines, settings);
        var changed = settings with { Detector = snapshot.Detector! with { Chain = Chain() } };
        var second = await service.ProcessAsync(id, snapshot.Events, lines, changed);
        Assert.NotEqual(first.Resolution662, second.Resolution662);
        Assert.Equal(1, second.TotalCounts);
        var wave = await new WaveformService().ProcessAsync(snapshot, new());
        Assert.Contains("BGO", wave.ChainReadout);
        Assert.Contains("Trapezoid", wave.ChainReadout);
    }

    [Fact]
    public async Task Readout_SuppressesPartialOverlapAndSaturation()
    {
        var service = new WaveformService();
        var chain = Chain(3);
        var clean = await service.ProcessAsync(Snapshot(chain: chain), new(Ideal: true));
        Assert.Contains("Trapezoid local flat-top", clean.PulseReadout);
        Assert.DoesNotContain("NaN", clean.PulseReadout);
        Assert.DoesNotContain("Infinity", clean.PulseReadout);
        var pair = Snapshot([new(1, 2, 662, 1), new(3, 4, 662, 1.0000001)], chain);
        Assert.Contains("unavailable", (await service.ProcessAsync(pair, new())).PulseReadout);
        Assert.Contains("unavailable", (await service.ProcessAsync(Snapshot(chain: chain, live: 1), new())).PulseReadout);
        Assert.Contains("unavailable", (await service.ProcessAsync(Snapshot([new(1, 2, 100000, 1)], chain), new(Ideal: true))).PulseReadout);
        var cr = await service.ProcessAsync(Snapshot([new(1, 2, 32.1, 1)]), new());
        Assert.Contains("quantisation", cr.PulseReadout);
        Assert.DoesNotContain("flat-top", cr.PulseReadout);
    }

    [Fact]
    public async Task InvalidRequestsAndCancellation_FailWithoutPublishing()
    {
        var service = new WaveformService();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ProcessAsync(Snapshot(), new(-1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ProcessAsync(Snapshot(), new(WindowUs: double.NaN)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ProcessAsync(Snapshot(), new(RateKcps: 0)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessAsync(Snapshot([new(1, 2, 662, 3)]), new()));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ProcessAsync(Snapshot(), new(), cancel.Token));
    }

    [Fact]
    public async Task RetainedMonteCarloEvents_PreserveAssociationAndTime()
    {
        var config = SimulationService.BuildConfig([new SceneSource()], new(), new()); config.Seed = 12345;
        using var source = new ListModeSource(config);
        var events = new List<DetectedEvent>();
        while (events.Count < 128) if (source.Advance() is { } e) events.Add(e);
        var snapshot = Snapshot(events.ToArray(), live: events[^1].ArrivalTimeS + 1);
        var service = new WaveformService();
        var real = await service.ProcessAsync(snapshot, new(64));
        var selected = real.Events.Single(e => e.Index == 64);
        Assert.Equal(events[64].DepositKeV, selected.DepositKeV);
        Assert.Equal(events[64].ArrivalTimeS, selected.AcquisitionTimeS);
        Assert.Equal(events[64].PixelX, selected.PixelX);
        var study = await service.ProcessAsync(snapshot, new(64, RateStudy: true, RateKcps: 1000));
        Assert.True(study.Events.Count > 1);
        Assert.Equal(events[64].ArrivalTimeS, study.Events.Single(e => e.Index == 64).AcquisitionTimeS);
    }

    [EvidenceFact]
    [Trait("Category", "Evidence")]
    public async Task MaximumWindow_ReportsWorkerCostAndBoundedPlotSamples()
    {
        foreach (int pa in Enumerable.Range(0, 4))
        {
            var watch = Stopwatch.StartNew();
            var view = await new WaveformService().ProcessAsync(Snapshot(chain: Chain(pa)), new(WindowUs: 100000));
            Assert.True(view.Adc.Y.Length < PlotSeries.MaximumSamples);
            Assert.Contains("clipped", view.Note);
            Assert.True(view.Adc.PreparedPyramid!.IsFor(view.Adc.Y));
            output.WriteLine($"preamp={pa} samples={view.Adc.Y.Length} total_ms={watch.Elapsed.TotalMilliseconds:F3} worker_ms={view.ProcessingTime.TotalMilliseconds:F3}");
        }
    }

    [EvidenceFact]
    [Trait("Category", "Evidence")]
    public async Task RetainedMonteCarlo_IsolatedPulseReadoutsAcrossParts()
    {
        foreach (int scintillator in Enumerable.Range(0, 4))
        foreach (int sensor in Enumerable.Range(0, 3))
        {
            var detector = new DetectorSettings { Chain = Chain(3, scintillator, sensor) };
            var config = SimulationService.BuildConfig([new SceneSource()], new(), detector); config.Seed = 12345;
            using var source = new ListModeSource(config);
            var events = new List<DetectedEvent>();
            while (events.Count < 512) if (source.Advance() is { } e) events.Add(e);
            foreach (double reference in new[] { 32.1, 661.7 })
            {
                // Choose an actual retained deposit near each reference, without inventing a spectral line.
                var acquired = events.OrderBy(e => Math.Abs(e.DepositKeV - reference)).First();
                foreach (int preamp in Enumerable.Range(0, 4))
                foreach (bool ideal in new[] { false, true })
                {
                    var chain = Chain(preamp, scintillator, sensor);
                    var snapshot = Snapshot([acquired], chain, acquired.ArrivalTimeS + 1);
                    var view = await new WaveformService().ProcessAsync(snapshot, new(Ideal: ideal));
                    Assert.DoesNotContain("NaN", view.PulseReadout);
                    Assert.DoesNotContain("Infinity", view.PulseReadout);
                    Assert.Equal(!chain.Preamp.Crrc, view.PulseReadout.Contains("local flat-top"));
                    output.WriteLine($"scintillator={chain.Scintillator.Name} sensor={sensor} preamp={preamp} ideal={ideal} deposit={acquired.DepositKeV:F6} amplitude={view.Events.Single().AmplitudeKeV:F6}: {view.PulseReadout}");
                }
            }
        }
    }

    [EvidenceFact]
    [Trait("Category", "Evidence")]
    public async Task MixedFieldCalibration_UsesAcquiredChainAndInvalidatesRatios()
    {
        var scene = new[] { new SceneSource(), new SceneSource { Isotope = "Co-60", X = 10 } };
        var service = new ImagingService(); var id = Guid.NewGuid();
        foreach (var chain in new[] { Chain(), Chain(3, 3, 2) })
        {
            var cfg = SimulationService.BuildConfig(scene, new(), new() { Chain = chain }); cfg.Seed = 987;
            using var source = new ListModeSource(cfg); var events = new List<DetectedEvent>();
            while (events.Count < 512) if (source.Advance() is { } e) events.Add(e);
            var snapshot = Snapshot(events.ToArray(), chain, events[^1].ArrivalTimeS + 1);
            var view = await service.ProcessAsync(id, snapshot, scene, new(), new(1.5, true));
            Assert.Equal(events.Count, view.NewlyMeasuredEvents);
            Assert.NotEmpty(view.Ratios);
            Assert.True(view.CalibrationTime > TimeSpan.Zero);
            var independent = await new ImagingService().ProcessAsync(Guid.NewGuid(), snapshot, scene, new(), new(1.5, true));
            Assert.Equal(independent.Ratios, view.Ratios);
            foreach (var ch in view.Channels)
                Assert.Equal(independent.Channels.Single(c => c.Isotope == ch.Isotope).Image.Flood.Raw.ToArray(), ch.Image.Flood.Raw.ToArray());
        }
    }
}
