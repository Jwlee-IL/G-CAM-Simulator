using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Studio.Core.Optics;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

public sealed class OpticsProjectionTests(ITestOutputHelper output)
{
    // At 100k histories/position over five fixed seeds, fine/coarse RMS was
    // 0.463150..0.591137. Max + 2 * observed range = 0.847111, rounded up.
    // This empirical margin protects a pinned deterministic regression, not arbitrary seeds.
    private const double SmallSamplingRmsRatioLimit = 0.85;

    private static AcquisitionSnapshot Acquire(SceneSource[] scene, OpticsSettings optics, int count = 2000)
    {
        var detector = new DetectorSettings();
        var config = SimulationService.BuildConfig(scene, optics, detector);
        using var source = new ListModeSource(config);
        var events = new List<DetectedEvent>();
        var flood = new DetectorImage(optics.DetectorPixels, optics.DetectorPixels);
        while (events.Count < count)
            if (source.Advance() is { } ev) { events.Add(ev); flood.Add(ev.PixelX, ev.PixelY, 1); }
        var original = new ImagingResult(flood.ReadOnlyCopy(), -(flood.Width - 1) * optics.PixelPitchMm / 2,
            optics.PixelPitchMm, null, 0, 0, null, count, TimeSpan.Zero);
        return new(events.Count > 0 ? events[^1].ArrivalTimeS : 0, count, source.RateCps, 1, false, original,
            events.AsReadOnly(), TimeSpan.Zero, true) { Detector = detector, Optics = optics };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refocus_ReprojectsAllChannels_WithoutMeasurementCalibrationOrNewEvents(bool strip)
    {
        SceneSource[] scene = [new() { X = 15, Y = 8 }, new() { Isotope = "Co-60", X = -15, Y = -8 }];
        var optics = new OpticsSettings();
        var snapshot = Acquire(scene, optics);
        var events = snapshot.Events.ToArray();
        var flood = snapshot.Imaging.Flood.Raw.ToArray();
        var service = new ImagingService();
        var id = Guid.NewGuid();
        var raw = await service.ProcessAsync(id, snapshot, scene, optics, new());
        var before = await service.ProcessAsync(id, snapshot, scene, optics, new(Strip: strip));
        var after = await service.ProcessAsync(id, snapshot, scene, optics with { PixelPitchMm = 0.3 }, new(1.5, strip, 800));
        Assert.Equal(0, after.NewlyMeasuredEvents);
        Assert.Equal(TimeSpan.Zero, after.CalibrationTime);
        Assert.Equal(before.Ratios, after.Ratios);
        Assert.Same(snapshot.Imaging.Flood, after.Channels[0].Image.Flood);
        Assert.Equal(events, snapshot.Events);
        Assert.Equal(flood, snapshot.Imaging.Flood.Raw.ToArray());
        var config = ImagingProjection.AtFocus(SimulationService.BuildConfig(scene, optics, snapshot.Detector!), optics, 800);
        var decoder = new DefaultSimulationFactory().CreateDecoder(config)!;
        for (int i = 0; i < after.Channels.Count; i++)
        {
            var channel = after.Channels[i];
            Assert.Equal(before.Channels[i].Image.Flood.Raw.ToArray(), channel.Image.Flood.Raw.ToArray());
            Assert.Equal(before.Channels[i].Image.StripCount, channel.Image.StripCount);
            var input = channel.Image.Flood;
            var ratios = strip ? before.Ratios.Where(r => r.LowIsotope == channel.Isotope).ToArray() : [];
            if (ratios.Length > 0)
            {
                var low = raw.Channels.Single(c => c.Isotope == channel.Isotope).Image.Flood;
                input = new DetectorImage(low.Width,low.Height);
                for (int y=0;y<low.Height;y++) for (int x=0;x<low.Width;x++)
                    input[x,y] = low[x,y]-ratios.Sum(r => r.R*raw.Channels.Single(c => c.Isotope == r.HighIsotope).Image.Flood[x,y]);
            }
            var independentlyDecoded = decoder.Decode(input);
            Assert.Equal(independentlyDecoded.Reconstruction.Raw.ToArray(), channel.Image.Reconstruction!.Raw.ToArray());
            Assert.Equal(independentlyDecoded.Estimate, channel.Image.Estimate);
            Assert.NotEqual(before.Channels[i].Image.ReconOriginMm, channel.Image.ReconOriginMm);
            if (i > 0)
            {
                var independent = ImagingProjection.Project(input, snapshot.Imaging, config, channel.Isotope, 1);
                Assert.Equal(independent.Peaks, channel.Peaks);
            }
        }
        Assert.Equal(after.Channels.Skip(1).SelectMany(c => c.Peaks), after.Channels[0].Peaks);
        var spectrum = new SpectrumService();
        var lines = scene.Select(s => new SpectrumLine(s.Isotope, Isotopes.Get(s.Isotope).Lines[0].EnergyKeV)).ToArray();
        var firstSpectrum = await spectrum.ProcessAsync(id, snapshot.Events, lines, new());
        var secondSpectrum = await spectrum.ProcessAsync(id, snapshot.Events, lines, new());
        Assert.Equal(firstSpectrum.Counts, secondSpectrum.Counts);
    }

    [Fact]
    public async Task EmptySnapshot_RefocusKeepsEmptyImagesAndDoesNotCalibrate()
    {
        SceneSource[] scene = [new(), new() { Isotope = "Co-60" }];
        var optics = new OpticsSettings();
        var snapshot = Acquire(scene, optics, 0);
        var view = await new ImagingService().ProcessAsync(Guid.NewGuid(), snapshot, scene, optics, new(1.5, true, 800));
        Assert.Equal(TimeSpan.Zero, view.CalibrationTime);
        Assert.Equal(0, view.NewlyMeasuredEvents);
        Assert.All(view.Channels, c => { Assert.Null(c.Image.Reconstruction); Assert.Empty(c.Peaks); Assert.Null(c.Image.Estimate); });
    }

    [Theory]
    [InlineData("Sharp", 160)]
    [InlineData("Sharp", 1000)]
    [InlineData("Baseline", 160)]
    [InlineData("Baseline", 1000)]
    [InlineData("Wide FOV", 160)]
    [InlineData("Wide FOV", 1000)]
    [InlineData("High-res", 160)]
    [InlineData("High-res", 1000)]
    public async Task Presets_NormalizedMixedScene_LocalizeWindowedChannelsWithinOneElement(string name, double focal)
    {
        var optics = OpticsPreset.All.Single(p => p.Name == name).Settings! with { FocalDistanceMm = focal };
        var geometry = OpticsGeometry.Calculate(optics, focal);
        double half = geometry.NominalHalfFieldMm;
        SceneSource[] scene = [new() { X = -0.5 * half, Y = -0.3 * half, DistanceMm = focal, ActivityUCi = 1 },
            new() { Isotope = "Co-60", X = 0.5 * half, Y = -0.3 * half, DistanceMm = focal, ActivityUCi = 1 },
            new() { Isotope = "Co-57", X = 0, Y = 0.5 * half, DistanceMm = focal, ActivityUCi = 1.5 }];
        var snapshot = Acquire(scene, optics, 20_000);
        var service = new ImagingService();
        var id = Guid.NewGuid();
        foreach (bool strip in new[] { false, true })
        {
            var view = await service.ProcessAsync(id, snapshot, scene, optics, new(1.5, strip));
            foreach (var source in scene)
            {
                var peak = Assert.Single(view.Channels.Single(c => c.Isotope == source.Isotope).Peaks);
                double error = Math.Sqrt(Math.Pow(peak.Xmm - source.X, 2) + Math.Pow(peak.Ymm - source.Y, 2));
                output.WriteLine($"{name}, F={focal}, strip={strip}, {source.Isotope}: error={error:F4} mm, element={geometry.ResolutionElementMm:F4} mm");
                // Conditional, K-known per-isotope gate; it makes no broadband or precision guarantee.
                Assert.True(error < geometry.ResolutionElementMm);
            }
        }
    }

    [EvidenceFact]
    [Trait("Category", "Evidence")]
    public void Sampling_PositionSweepAtConstantDetectorSize_FinerPixelsReduceLocalizationError()
    {
        var rms = new List<double>();
        var maxima = new List<double>();
        foreach (double pitch in new[] { 0.6, 0.3 })
        {
            var errors = new List<double>();
            for (int y = -10; y <= 10; y++)
            {
                var optics = new OpticsSettings { PixelPitchMm = pitch, DetectorPixels = (int)Math.Round(18 / pitch) };
                var config = SceneConfigBuilder.Build([new SceneSource { X = 0, Y = y }], optics, 1_000_000, 12345);
                config.Decoder.ReconStepMm = 0.25;
                var decoded = new SimulationRunner(new DefaultSimulationFactory()).Run(config).Estimate!.Position;
                errors.Add(Math.Sqrt(decoded.X * decoded.X + Math.Pow(decoded.Y - y, 2)));
            }
            rms.Add(Math.Sqrt(errors.Average(e => e * e)));
            maxima.Add(errors.Max());
            output.WriteLine($"pitch={pitch}, width=18 mm, y=-10..10, 1e6 biased histories/position, 0.25 mm grid: RMS={rms[^1]:F4}, max={maxima[^1]:F4} mm");
        }
        // Theme-55's 3e6 run measured RMS 0.95 -> 0.51 mm. This smaller deterministic
        // regression verifies the effect across positions, with margin for MC fluctuations.
        Assert.True(rms[1] < 0.75 * rms[0]);
        Assert.True(maxima[1] < 0.75 * maxima[0]);
    }

    [Fact]
    public void Sampling_SmallOffAxisSample_FinerPixelsReduceLocalizationError()
    {
        var (coarse, fine) = MeasureSmallSampling(12345);
        output.WriteLine($"seed=12345: coarse RMS={coarse:F6}, fine RMS={fine:F6}, ratio={fine / coarse:F6}");
        Assert.True(fine < SmallSamplingRmsRatioLimit * coarse,
            $"RMS: coarse={coarse:F4}, fine={fine:F4} mm; ratio must be < {SmallSamplingRmsRatioLimit}");
    }

    [EvidenceFact]
    [Trait("Category", "Evidence")]
    public void Sampling_SmallBudgetSeedSpread_ReportsRegressionMargin()
    {
        foreach (int seed in new[] { 12345, 23456, 34567, 45678, 56789 })
        {
            var (coarse, fine) = MeasureSmallSampling(seed);
            output.WriteLine($"seed={seed}: coarse RMS={coarse:F6}, fine RMS={fine:F6}, ratio={fine / coarse:F6}, improvement={coarse - fine:F6} mm");
            Assert.True(fine < SmallSamplingRmsRatioLimit * coarse);
        }
    }

    private static (double Coarse, double Fine) MeasureSmallSampling(int seed)
    {
        var rms = new List<double>();
        foreach (double pitch in new[] { 0.6, 0.3 })
        {
            var errorsSquared = new List<double>();
            foreach (int y in new[] { -8, -4, 4, 8 })
            {
                var optics = new OpticsSettings { PixelPitchMm = pitch, DetectorPixels = (int)Math.Round(18 / pitch) };
                var config = SceneConfigBuilder.Build([new SceneSource { X = 0, Y = y }], optics, 100_000, seed);
                // Keep the nominal full-field grid's quarter-mm phase, but reconstruct only
                // this small single-source test region. Transport and flood scoring are unchanged.
                config.Decoder.ReconHalfExtentMm = 12.125;
                config.Decoder.ReconStepMm = 0.25;
                var decoded = new SimulationRunner(new DefaultSimulationFactory()).Run(config).Estimate!.Position;
                errorsSquared.Add(decoded.X * decoded.X + Math.Pow(decoded.Y - y, 2));
            }
            rms.Add(Math.Sqrt(errorsSquared.Average()));
        }
        return (rms[0], rms[1]);
    }
}
