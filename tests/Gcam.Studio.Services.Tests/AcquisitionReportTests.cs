using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services.Tests;

/// <summary>The report records the service's finalized inputs, not a fresh scene-builder approximation.</summary>
public sealed class AcquisitionReportTests
{
    [Fact]
    public async Task Snapshot_RecordsFinalDetectorBackgroundAndSeed_OncePerAcquisition()
    {
        var scene = new[] { new SceneSource { X = 15, ActivityUCi = 500 } };
        var optics = new OpticsSettings();
        var detector = new DetectorSettings { GainSigma = .07, GainSeed = 19, ReflectorGapMm = .03 };
        var expected = SimulationService.BuildConfig(scene, optics, detector, .2);
        expected.Seed = 73;
        await using var session = new SimulationService().Start(scene, optics, .05, 10, detector, .2, 73);
        string? first = null;
        await foreach (var snapshot in session.ReadSnapshotsAsync())
        {
            Assert.Equal(ConfigLoader.Serialize(expected), snapshot.ConfigurationJson);
            first ??= snapshot.ConfigurationJson;
            Assert.Same(first, snapshot.ConfigurationJson);
        }
        Assert.NotNull(first);
    }

    [Fact]
    public async Task Snapshot_RecordsResolvedAmbientSpectrum_WithoutExternalFileReference()
    {
        var field = AmbientPreset.TerrestrialUnscear2000V1.Field(.1, AmbientGeometry.FrontOnlyThroughMask);
        var scene = new[] { new SceneSource { ActivityUCi = 500 } };
        var expected = SimulationService.BuildConfig(scene, new(), new(), ambient: field);
        expected.Seed = 73;
        Assert.Null(expected.Ambient!.SpectrumFile);
        Assert.NotNull(expected.Ambient.Spectrum);
        await using var session = new SimulationService().StartAmbient(scene, new(), .05, 10, field, seed: 73);
        await foreach (var snapshot in session.ReadSnapshotsAsync())
            Assert.Equal(ConfigLoader.Serialize(expected), snapshot.ConfigurationJson);
    }
}
