using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services.Tests;

public sealed class AmbientAcquisitionTests
{
    private static async Task<AcquisitionSnapshot> Segment(IAcquisitionSession session, ManualTimeProvider clock, int? stopAtTick = null)
    {
        AcquisitionSnapshot? last = null; int tick = 0;
        await foreach (var snapshot in session.ReadSnapshotsAsync())
        {
            last = snapshot;
            if (snapshot.IsCompleted) continue;
            if (stopAtTick is { } limit && tick >= limit) { session.Stop(); continue; }
            await clock.WaitForTimerAsync(); clock.Advance(TimeSpan.FromMilliseconds(250)); tick++;
        }
        return last!;
    }

    [Theory]
    [InlineData(AmbientGeometry.BareCrystalAllFaces)]
    [InlineData(AmbientGeometry.FrontOnlyThroughMask)]
    public async Task BackgroundOnly_StopContinueRetainsExactlyTheFixedTimeStream(AmbientGeometry geometry)
    {
        var clock = new ManualTimeProvider(); var service = new SimulationService(clock);
        var field = new AmbientFieldConfig { DoseRateMicroSvPerHour = 1, Geometry = geometry };
        await using var whole = service.StartAmbient([], new(), 10, 4, field, seed: 4711);
        var truth = await Segment(whole, clock);
        await using var paused = service.StartAmbient([], new(), 10, 4, field, seed: 4711);
        var stopped = await Segment(paused, clock, 1);
        clock.Advance(TimeSpan.FromSeconds(100));
        paused.Continue(10, 4);
        var resumed = await Segment(paused, clock);
        Assert.True(resumed.IsCompleted); Assert.Equal(10, resumed.LiveTimeS);
        Assert.Equal(truth.Events, resumed.Events);
        Assert.Equal(stopped.Events, resumed.Events.Take(stopped.Events.Count));
        Assert.Equal(0, resumed.SourceRateCps); Assert.Null(resumed.AmbientBackgroundToSignalRatio);
        Assert.Equal(661.7, resumed.AmbientMaximumEnergyKeV);
    }

    [Fact]
    public async Task EmptyZeroField_CompletesAndBuildConfigFreezesInputs()
    {
        var field = new AmbientFieldConfig();
        var config = SimulationService.BuildConfig([], new(), new(), ambient: field);
        field.DoseRateMicroSvPerHour = 100; field.Spectrum.Lines[0].EnergyKeV = 100;
        Assert.Equal(0, config.Ambient!.DoseRateMicroSvPerHour);
        Assert.Equal(661.7, config.Ambient.Spectrum.Lines[0].EnergyKeV);
        Assert.Empty(config.Sources!);
        var clock = new ManualTimeProvider();
        await using var session = new SimulationService(clock).StartAmbient([], new(), 3, 4, config.Ambient, seed: 12345);
        var snapshot = await Segment(session, clock);
        Assert.True(snapshot.IsCompleted); Assert.Equal(3, snapshot.LiveTimeS); Assert.Empty(snapshot.Events);
    }

    [Fact]
    public async Task BackgroundOnly_WorkspacesShowCountsWithoutInventingIsotopeLines()
    {
        var clock = new ManualTimeProvider();
        await using var session = new SimulationService(clock).StartAmbient([], new(), 10, 4,
            new() { DoseRateMicroSvPerHour = 1 }, seed: 12345);
        var snapshot = await Segment(session, clock);
        var spectrum = await new SpectrumService().ProcessAsync(Guid.NewGuid(), snapshot.Events, [],
            new() { Detector = snapshot.Detector, PixelsX = snapshot.Imaging.Flood.Width, PixelsY = snapshot.Imaging.Flood.Height,
                IncidentMaximumEnergyKeV = snapshot.AmbientMaximumEnergyKeV });
        Assert.Equal(snapshot.Counts, spectrum.TotalCounts); Assert.Empty(spectrum.Bands);
        var image = await new ImagingService().ProcessAsync(Guid.NewGuid(), snapshot, [], new(), new());
        Assert.Single(image.Channels); Assert.Empty(image.Channels[0].Peaks);
        Assert.Equal(snapshot.Counts, image.Channels[0].Image.EffectiveCounts);
    }
}
