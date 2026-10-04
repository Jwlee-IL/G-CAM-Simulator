using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class AmbientViewModelTests
{
    private sealed class Acquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS,
            double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
            => throw new NotSupportedException();
    }
    private sealed class Theme : IThemeService
    {
        public AppTheme Current => AppTheme.Dark;
        public void Apply(AppTheme theme) { }
    }
    private static MainViewModel Model() => new(new Acquisition(), new Theme(), new FakeSpectrumService());

    [Fact]
    public void DefaultIsValidatedFrontOnlyField_ZeroIsIdeal_SourceFreeStartRequiresAbsoluteField()
    {
        var model = Model(); // AB-15: on by default, 0.10 µSv/h, validated terrestrial spectrum, front-only bound
        Assert.Equal(0.10, model.AmbientDoseRateMicroSvPerHour);
        Assert.Equal(AmbientGeometry.FrontOnlyThroughMask, model.AmbientGeometry);
        Assert.Same(AmbientPreset.TerrestrialUnscear2000V1, model.AmbientPreset);
        Assert.Equal("Terrestrial UNSCEAR 2000 v1 — validated", model.AmbientPresetName);
        Assert.DoesNotContain("not validated", model.AmbientPresetName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("terrestrial-unscear2000-v1 · front-only bound", model.AmbientEnvironmentLabel);
        Assert.DoesNotContain("not validated", model.AmbientEnvironmentLabel, StringComparison.OrdinalIgnoreCase);
        model.AmbientGeometry = AmbientGeometry.BareCrystalAllFaces;
        Assert.Equal("terrestrial-unscear2000-v1 · bare-crystal bound", model.AmbientEnvironmentLabel);
        model.Sources.Clear(); Assert.True(model.StartCommand.CanExecute(null));
        Assert.Equal("undefined (source-free)", model.AmbientBsrReadout);
        model.AmbientDoseRateMicroSvPerHour = 0;
        Assert.Equal("ideal environment", model.AmbientEnvironmentLabel); Assert.Equal("off", model.AmbientBsrReadout);
        Assert.False(model.StartCommand.CanExecute(null));
    }

    [Fact]
    public void PresetListOffersOnlyTheValidatedSpectrum_ByPinnedReference()
    {
        var preset = Assert.Single(AmbientPreset.All);
        Assert.Same(AmbientPreset.TerrestrialUnscear2000V1, preset);
        Assert.DoesNotContain(AmbientPreset.All, p => p.Id.Contains("mono662") || p.FileName.Contains("NOT-VALIDATED"));
        var field = preset.Field(0.1, AmbientGeometry.FrontOnlyThroughMask);
        Assert.Equal("terrestrial-unscear2000-v1.json", field.SpectrumFile);
        Assert.Equal(preset.Sha256, field.SpectrumFileSha256); Assert.Matches("^[0-9a-f]{64}$", preset.Sha256);
        Assert.True(field.RequireValidatedSpectrum);
        Assert.Equal(0.1, field.DoseRateMicroSvPerHour); Assert.Equal(AmbientGeometry.FrontOnlyThroughMask, field.Geometry);
    }

    private sealed class Recording : IAcquisitionService
    {
        public AmbientFieldConfig? Field; public int Plain;
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS,
            double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
        { Plain++; throw new NotSupportedException("recorded"); }
        public IAcquisitionSession StartAmbient(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS,
            double speed, AmbientFieldConfig ambient, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
        { Field = ambient; throw new NotSupportedException("recorded"); }
    }

    [Fact]
    public async Task Start_DefaultPassesValidatedPresetReference_ZeroTakesTheLegacyPath()
    {
        var service = new Recording();
        var model = new MainViewModel(service, new Theme(), new FakeSpectrumService()) { SeedText = "7" };
        await model.StartCommand.ExecuteAsync(null);
        Assert.NotNull(service.Field); Assert.Equal(0, service.Plain);
        Assert.Equal(0.10, service.Field!.DoseRateMicroSvPerHour);
        Assert.Equal(AmbientGeometry.FrontOnlyThroughMask, service.Field.Geometry);
        Assert.Equal(AmbientPreset.TerrestrialUnscear2000V1.FileName, service.Field.SpectrumFile);
        Assert.Equal(AmbientPreset.TerrestrialUnscear2000V1.Sha256, service.Field.SpectrumFileSha256);
        Assert.True(service.Field.RequireValidatedSpectrum);

        var zero = new Recording();
        var ideal = new MainViewModel(zero, new Theme(), new FakeSpectrumService()) { SeedText = "7", AmbientDoseRateMicroSvPerHour = 0 };
        await ideal.StartCommand.ExecuteAsync(null);
        Assert.Null(zero.Field); Assert.Equal(1, zero.Plain);
    }

    [Fact]
    public void AmbientPhysicalInputsLockAndDerivedBsrHasNoSetter()
    {
        var model = Model(); model.AmbientDoseRateMicroSvPerHour = .1;
        model.AmbientGeometry = AmbientGeometry.FrontOnlyThroughMask;
        var image = new ImagingResult(new DetectorImage(30,30), 0, .6, null, 0, 0, null, 0, TimeSpan.Zero);
        model.Snapshot = new(1,0,0,1,false,image,[],TimeSpan.Zero,false)
            { AmbientRateCps=2, SourceRateCps=4, AmbientMaximumEnergyKeV=661.7 };
        model.AmbientDoseRateMicroSvPerHour = .2; model.AmbientGeometry = AmbientGeometry.BareCrystalAllFaces;
        Assert.Equal(.1, model.AmbientDoseRateMicroSvPerHour);
        Assert.Equal(AmbientGeometry.FrontOnlyThroughMask, model.AmbientGeometry);
        Assert.False(model.CanEditInputs); Assert.Contains("0.5", model.AmbientBsrReadout);
        Assert.False(typeof(MainViewModel).GetProperty(nameof(MainViewModel.AmbientBsrReadout))!.CanWrite);
    }

    [Theory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidAmbientDoseIsRefused_PreviousValueStays(double dose)
    {
        var model = Model(); model.AmbientDoseRateMicroSvPerHour = .2;
        model.AmbientDoseRateMicroSvPerHour = dose; // AB-16: refused, no fall back to 0
        Assert.Equal(.2, model.AmbientDoseRateMicroSvPerHour); Assert.Equal("0.2", model.AmbientDoseText);
    }

    [Theory]
    [InlineData("")] [InlineData(" ")] [InlineData("-1")] [InlineData("+0.1")] [InlineData("1e-1")] [InlineData("NaN")]
    [InlineData("Infinity")] [InlineData("abc")] [InlineData("0.1.2")] [InlineData("0,1")] [InlineData(".")]
    public async Task DoseTextOutsideThePattern_IsRefused_PreviousValueStays_StartBlocked(string text)
    {
        var service = new Recording();
        var model = new MainViewModel(service, new Theme(), new FakeSpectrumService()) { SeedText = "7" };
        model.AmbientDoseText = text;
        Assert.Equal(.1, model.AmbientDoseRateMicroSvPerHour);
        Assert.Equal(text, model.AmbientDoseText); // the entry stays visible with the warning until corrected
        Assert.NotNull(model.AmbientDoseError);
        await model.StartCommand.ExecuteAsync(null);
        Assert.Null(service.Field); Assert.Equal(0, service.Plain);
        model.AmbientDoseText = "0.3";
        Assert.Null(model.AmbientDoseError); Assert.Equal(.3, model.AmbientDoseRateMicroSvPerHour);
    }

    [Theory]
    [InlineData("0", 0)] [InlineData("0.1", .1)] [InlineData(" 2.5 ", 2.5)] [InlineData(".5", .5)] [InlineData("3.", 3)] [InlineData("10", 10)]
    public void DoseTextMatchingThePattern_SetsTheDoseRate(string text, double expected)
    {
        var model = Model(); model.AmbientDoseText = text;
        Assert.Null(model.AmbientDoseError); Assert.Equal(expected, model.AmbientDoseRateMicroSvPerHour);
        Assert.Equal(expected == 0 ? "ideal environment" : "terrestrial-unscear2000-v1 · front-only bound", model.AmbientEnvironmentLabel);
    }

    [Fact]
    public void DoseText_FollowsProgrammaticValue_AndLocksWithData()
    {
        var model = Model(); Assert.Equal("0.1", model.AmbientDoseText);
        model.AmbientDoseRateMicroSvPerHour = 0; Assert.Equal("0", model.AmbientDoseText);
        var image = new ImagingResult(new DetectorImage(30,30), 0, .6, null, 0, 0, null, 0, TimeSpan.Zero);
        model.Snapshot = new(1,0,0,1,false,image,[],TimeSpan.Zero,false);
        model.AmbientDoseText = "0.5";
        Assert.Equal("0", model.AmbientDoseText); Assert.Equal(0, model.AmbientDoseRateMicroSvPerHour);
    }
}
