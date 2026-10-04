using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services.Tests;

/// <summary>Studio's ambient preset (AB-15): the validated terrestrial spectrum is deployed beside the assemblies,
/// pinned in code to the repository's sidecar hash, and resolved by the engine loader on the frozen config.</summary>
public sealed class AmbientPresetTests
{
    private static readonly AmbientPreset Preset = AmbientPreset.TerrestrialUnscear2000V1;

    private static string RepoSample(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Gcam.sln"))) return Path.Combine(dir.FullName, "samples", relative);
        throw new InvalidOperationException("Gcam.sln not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Pin_EqualsRepositorySidecar_AndDeployedBytesAreTheRepositoryFile()
    {
        string sidecar = File.ReadAllText(RepoSample("ambient/terrestrial-unscear2000-v1.json.sha256")).Trim();
        Assert.Equal(sidecar, Preset.Sha256);
        string deployed = Path.Combine(SimulationService.AmbientSpectrumDirectory, Preset.FileName);
        Assert.True(File.Exists(deployed), $"{Preset.FileName} is not deployed to the ambient output folder.");
        byte[] bytes = File.ReadAllBytes(deployed);
        Assert.Equal(File.ReadAllBytes(RepoSample("ambient/" + Preset.FileName)), bytes);
        Assert.Equal(Preset.Sha256, IncidentSpectrumFile.Sha256Hex(bytes));
    }

    [Fact]
    public void BuildConfig_ResolvesTheValidatedSpectrum_OnTheFrozenCopyOnly()
    {
        var field = Preset.Field(0.1, AmbientGeometry.FrontOnlyThroughMask);
        var config = SimulationService.BuildConfig([new SceneSource { ActivityUCi = 500 }], new(), new(), ambient: field);
        var resolved = config.Ambient!;
        Assert.Null(resolved.SpectrumFile);
        Assert.Equal("terrestrial-unscear2000-v1", resolved.Spectrum.Id);
        Assert.True(resolved.Spectrum.IsValidated); Assert.NotNull(resolved.Spectrum.Validation);
        Assert.Equal(resolved.Spectrum.ContentHash, resolved.Spectrum.ComputeContentHash());
        Assert.True(resolved.RequireValidatedSpectrum);
        Assert.Equal(0.1, resolved.DoseRateMicroSvPerHour); Assert.Equal(AmbientGeometry.FrontOnlyThroughMask, resolved.Geometry);
        Assert.Equal(Preset.FileName, field.SpectrumFile); // the caller's reference is not consumed
        Assert.Equal("development-mono662-v1-NOT-VALIDATED", field.Spectrum.Id);
    }

    [Fact]
    public void WrongPinOrMissingFile_IsRefusedAtStart()
    {
        var scene = new[] { new SceneSource { ActivityUCi = 500 } };
        var wrongPin = Preset.Field(0.1, AmbientGeometry.FrontOnlyThroughMask);
        wrongPin.SpectrumFileSha256 = new string('0', 64);
        Assert.Throws<InvalidDataException>(() => new SimulationService().StartAmbient(scene, new(), 1, 1, wrongPin));
        var missing = Preset.Field(0.1, AmbientGeometry.FrontOnlyThroughMask);
        missing.SpectrumFile = "no-such-spectrum.json";
        Assert.Throws<FileNotFoundException>(() => new SimulationService().StartAmbient(scene, new(), 1, 1, missing));
    }

    [Fact]
    public async Task DefaultField_AcquiresWithTheValidatedSpectrum()
    {
        var clock = new ManualTimeProvider();
        await using var session = new SimulationService(clock).StartAmbient([new SceneSource { ActivityUCi = 500 }], new(), .25, 1,
            Preset.Field(0.1, AmbientGeometry.FrontOnlyThroughMask), seed: 12345);
        AcquisitionSnapshot? last = null;
        await foreach (var snapshot in session.ReadSnapshotsAsync())
        {
            last = snapshot;
            if (!snapshot.IsCompleted) { await clock.WaitForTimerAsync(); clock.Advance(TimeSpan.FromMilliseconds(250)); }
        }
        Assert.True(last!.IsCompleted);
        Assert.True(last.SourceRateCps > 0); // the default front-only field is ~0.14 cps: too few histories here for its rate
        Assert.True(last.AmbientMaximumEnergyKeV > 2614); // terrestrial lines (Tl-208 2614 keV and above), not the 661.7 keV placeholder
    }
}
