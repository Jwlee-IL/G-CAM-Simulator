using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Decoding;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

public sealed class ImagingServiceTests(ITestOutputHelper output)
{
    private static readonly DetectorSettings Detector = new();
    private static readonly OpticsSettings Optics = new();

    private static AcquisitionSnapshot Acquire(IReadOnlyList<SceneSource> scene, double liveTimeS, int seed)
    {
        var config = SimulationService.BuildConfig(scene, Optics, Detector);
        config.Seed = seed;
        using var source = new ListModeSource(config);
        var events = new List<DetectedEvent>();
        var flood = new DetectorImage(config.Detector.PixelsX, config.Detector.PixelsY);
        while (source.ArrivalTimeS <= liveTimeS)
        {
            if (source.Advance() is not { } ev || ev.ArrivalTimeS > liveTimeS) continue;
            events.Add(ev);
            flood.Add(ev.PixelX, ev.PixelY, 1);
        }
        var image = new ImagingResult(flood.ReadOnlyCopy(), -(flood.Width - 1) * config.Detector.PixelPitchMm / 2,
            config.Detector.PixelPitchMm, null, 0, 0, null, events.Count, TimeSpan.Zero);
        return new(liveTimeS, events.Count, source.RateCps, 1, false, image,
            Array.AsReadOnly(events.ToArray()), TimeSpan.Zero, true) { Detector = Detector };
    }

    private void Report(ImagingView view)
    {
        foreach (var r in view.Ratios) output.WriteLine($"{r.Description}; H accepted={r.Events}");
        output.WriteLine($"Worker: channels={view.ChannelTime.TotalMilliseconds:F3} ms, calibration={view.CalibrationTime.TotalMilliseconds:F3} ms, decode={view.DecodeTime.TotalMilliseconds:F3} ms");
    }

    [Fact]
    public async Task Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount()
    {
        const double live = 600;
        SceneSource[] cs = [new()];
        SceneSource[] mixed = [new(), new() { Isotope = "Co-60", ActivityUCi = 1000 }];
        var reference = Acquire(cs, live, 31415);
        var snapshot = Acquire(mixed, live, 27182);
        var truth = await new ImagingService().ProcessAsync(Guid.NewGuid(), reference, cs, Optics, new());
        var service = new ImagingService();
        var id = Guid.NewGuid();
        var raw = await service.ProcessAsync(id, snapshot, mixed, Optics, new());
        var stripped = await service.ProcessAsync(id, snapshot, mixed, Optics, new(Strip: true));
        Report(raw);
        Report(stripped);
        double expected = truth.Channels.Single(c => c.Isotope == "Cs-137").Image.EffectiveCounts;
        double low = raw.Channels.Single(c => c.Isotope == "Cs-137").Image.EffectiveCounts;
        double high = raw.Channels.Single(c => c.Isotope == "Co-60").Image.EffectiveCounts;
        double observed = stripped.Channels.Single(c => c.Isotope == "Cs-137").Image.EffectiveCounts;
        var r = Assert.Single(raw.Ratios);
        // Disjoint Poisson low/high windows and independent Cs-only acquisition.
        // Delta-method calibration variance for R=L/H: R²(1/L+1/H).
        // Include independent calibration uncertainty times the mixed high count squared.
        double varianceR = r.R * r.R * (1.0 / r.LowCounts + 1.0 / r.HighCounts);
        double tolerance = 4 * Math.Sqrt(low + r.R * r.R * high + expected + high * high * varianceR);
        output.WriteLine($"Co-located {live} s: raw Cs={low:F4}, high={high:F4}, stripped Cs={observed:F4}, Cs-only={expected:F4}; error={observed - expected:F4}, 4σ={tolerance:F4}, Var(R)={varianceR:G8}");
        Assert.True(low - expected > 4 * Math.Sqrt(low + expected), "Unstripped Co downscatter must bias the Cs count high.");
        Assert.InRange(Math.Abs(observed - expected), 0, tolerance);
        Assert.All(stripped.Channels.SelectMany(c => c.Image.Flood.Raw.ToArray()), x => Assert.True(x >= 0));
        Assert.Same(snapshot.Imaging.Flood, raw.Channels[0].Image.Flood);
    }

    [Fact]
    public async Task Channels_OffAxisCsAndCo_LocalizeAtTheirOwnSource()
    {
        const double live = 600;
        SceneSource[] scene = [new() { X = 15, Y = 8 }, new() { Isotope = "Co-60", X = -15, Y = -8, ActivityUCi = 1000 }];
        var snapshot = Acquire(scene, live, 19283);
        var view = await new ImagingService().ProcessAsync(Guid.NewGuid(), snapshot, scene, Optics, new(Strip: true));
        Report(view);
        foreach (var s in scene)
        {
            var channel = view.Channels.Single(c => c.Isotope == s.Isotope);
            var peak = Assert.Single(channel.Peaks);
            var matches = MixedFieldStudy.MatchOneToOne([[s.X, s.Y]], [new FoundSource(peak.Xmm, peak.Ymm, peak.Value)]);
            double error = Assert.Single(matches).ErrorMm;
            double diagonal = channel.Image.ReconStepMm * Math.Sqrt(2);
            output.WriteLine($"{s.Isotope} {live} s: found ({peak.Xmm:F4},{peak.Ymm:F4}), truth ({s.X},{s.Y}); error={error:F4} mm, association bound={diagonal:F4} mm (step × sqrt(2))");
            Assert.True(error < diagonal);
        }
    }

    [Fact]
    public async Task WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing()
    {
        SceneSource[] scene = [new(), new() { Isotope = "Co-60", ActivityUCi = 1000 }];
        var snapshot = Acquire(scene, 60, 13579);
        var service = new ImagingService();
        var id = Guid.NewGuid();
        var first = await service.ProcessAsync(id, snapshot, scene, Optics, new());
        var changed = await service.ProcessAsync(id, snapshot, scene, Optics, new(2));
        var fresh = await new ImagingService().ProcessAsync(id, snapshot, scene, Optics, new(2));
        Report(first); Report(changed);
        Assert.NotEqual(first.Ratios[0].R, changed.Ratios[0].R);
        Assert.True(changed.CalibrationTime > TimeSpan.Zero);
        Assert.Same(snapshot.Imaging.Flood, changed.Channels[0].Image.Flood);
        Assert.Equal(first.Channels[1].LoKeV > changed.Channels[1].LoKeV, true);
        for (int i = 0; i < changed.Channels.Count; i++)
            Assert.Equal(fresh.Channels[i].Image.Flood.Raw.ToArray(), changed.Channels[i].Image.Flood.Raw.ToArray());
        var cached = await service.ProcessAsync(id, snapshot, scene, Optics, new(2, true));
        Assert.Equal(TimeSpan.Zero, cached.CalibrationTime);
        Assert.Equal(changed.Ratios, cached.Ratios);
    }

    [EvidenceFact]
    [Trait("Category", "Evidence")]
    public async Task Precision_TwentySeeds_MeasuresMixedIsotopesAndCoOnlyControl()
    {
        const int seeds = 20;
        const double live = 600;
        SceneSource[] mixed = [new() { X = 15, Y = 8 }, new() { Isotope = "Co-60", X = -15, Y = -8, ActivityUCi = 1000 }];
        SceneSource[] control = [mixed[1]];
        foreach (var scene in new[] { mixed, control })
        {
            // Each seed has its own acquisition identity and independent retained event prefix.
            var service = new ImagingService();
            var errors = scene.Select(_ => new List<(double X, double Y, double RawX, double RawY)>()).ToArray();
            for (int seed = 0; seed < seeds; seed++)
            {
                var snapshot = Acquire(scene, live, 42000 + seed);
                // Unique id invalidates all cumulative event caches for each independent acquisition.
                var view = await service.ProcessAsync(Guid.NewGuid(), snapshot, scene, Optics, new(Strip: true));
                if (seed == 0) Report(view);
                for (int i = 0; i < scene.Length; i++)
                {
                    var channel = view.Channels.Single(c => c.Isotope == scene[i].Isotope);
                    var peak = Assert.Single(channel.Peaks);
                    var image = channel.Image;
                    var raw = MixedFieldStudy.TopPeaks(image.Reconstruction!, image.ReconOriginMm, image.ReconStepMm, 1, 0).Single();
                    errors[i].Add((peak.Xmm - scene[i].X, peak.Ymm - scene[i].Y, raw.Xmm - scene[i].X, raw.Ymm - scene[i].Y));
                }
            }
            for (int i = 0; i < scene.Length; i++)
            foreach (bool refined in new[] { false, true })
            {
                var e = errors[i].Select(e => refined ? (e.X, e.Y) : (X: e.RawX, Y: e.RawY)).ToArray();
                output.WriteLine($"Precision {(scene.Length == 1 ? "Co-only" : "mixed")} {scene[i].Isotope}, refined={refined}, seeds={seeds}, live={live}s: bias=({e.Average(p => p.X):F6},{e.Average(p => p.Y):F6}) mm, RMS={Math.Sqrt(e.Average(p => p.X*p.X+p.Y*p.Y)):F6} mm, max={e.Max(p => Math.Sqrt(p.X*p.X+p.Y*p.Y)):F6} mm; measured, no precision tolerance.");
            }
        }
    }

    [Fact]
    public async Task IncrementalChannels_MatchWholePrefixAndSharedMeasuredEnergyWindows()
    {
        SceneSource[] scene = [new()];
        var snapshot = Acquire(scene, 60, 24680);
        var service = new ImagingService();
        var id = Guid.NewGuid();
        var prefix = snapshot with { Events = snapshot.Events.Take(snapshot.Events.Count / 2).ToArray() };
        await service.ProcessAsync(id, prefix, scene, Optics, new());
        var incremental = await service.ProcessAsync(id, snapshot, scene, Optics, new());
        var fresh = await new ImagingService().ProcessAsync(Guid.NewGuid(), snapshot, scene, Optics, new());
        Assert.Equal(fresh.Channels[1].Image.Flood.Raw.ToArray(), incremental.Channels[1].Image.Flood.Raw.ToArray());
        var channel = incremental.Channels[1];
        var measurement = new MeasurementStage(Detector, snapshot.Imaging.Flood.Width, snapshot.Imaging.Flood.Height);
        int count = snapshot.Events.Select((ev, i) => measurement.Measure(ev, i))
            .Count(e => e >= channel.LoKeV && e <= channel.HiKeV);
        Assert.Equal(count, channel.Image.EffectiveCounts);
        Assert.Equal(TimeSpan.Zero, incremental.CalibrationTime);
        Report(incremental);
    }
}
