using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Services;

namespace Gcam.Studio.Services.Tests;

public sealed class FocusSweepServiceTests
{
    [Fact]
    public async Task Sweep_UsesRetainedFlood_WithoutEventsOrSceneTruth()
    {
        var request = Request(); var before = request.Image.Flood.Raw.ToArray();
        var result = await new FocusSweepService().SweepAsync(request);
        Assert.Equal(request.Identity, result.Identity);
        var track = Assert.Single(result.Tracks);
        Assert.Equal(FocusSweepMath.Planes(80, 5), track.Curve.Select(p => p.PlaneMm));
        Assert.All(track.Curve, p => Assert.True(double.IsFinite(p.Prominence)));
        Assert.Equal(before, request.Image.Flood.Raw.ToArray());
    }
    [Fact]
    public async Task Sweep_PreCancelledWorkerCannotPublish()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FocusSweepService().SweepAsync(Request(), cancellation.Token));
    }
    [Fact]
    public async Task Sweep_EmptyFloodIsUnresolved()
    {
        var request = Request();
        request = request with { Image = request.Image with { Flood = new DetectorImage(30, 30).ReadOnlyCopy(), EffectiveCounts = 0 } };
        Assert.Empty((await new FocusSweepService().SweepAsync(request)).Tracks);
    }
    [Fact]
    public void Face_GainsMatchMeasurementPattern()
    {
        var optics = new OpticsSettings(); var detector = new DetectorSettings();
        var face = new DetectorFaceService().Build(optics, detector);
        var pattern = new CrystalUniformity(new DetectorConfig { PixelsX = 30, PixelsY = 30,
            GainSigma = detector.GainSigma, UniformitySeed = detector.GainSeed });
        Assert.Equal(pattern.Gain, face.Crystals.Select(c => c.Gain));
    }
    [Theory]
    [InlineData(-.1)]
    [InlineData(.6)]
    [InlineData(double.NaN)]
    public void Config_RejectsGapAtServiceBoundary(double gap)
        => Assert.ThrowsAny<ArgumentException>(() => SimulationService.BuildConfig([new SceneSource()], new(),
            new DetectorSettings { ReflectorGapMm = gap }));
    private static FocusSweepRequest Request()
    {
        var flood = new DetectorImage(30, 30);
        for (int y = 0; y < 30; y++)
        for (int x = 0; x < 30; x++) flood[x, y] = (x + y * 7) % 11;
        var image = new ImagingResult(flood.ReadOnlyCopy(), -8.7, .6, null, 0, 0, null,
            flood.Raw.ToArray().Sum(), TimeSpan.Zero);
        return new(new(Guid.NewGuid(), 1000, 60, "All", 1.5, false, new(), new(), 1, 5), image);
    }
}
