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
    public void DefaultIsIdealAndSourceFreeStartRequiresAbsoluteField()
    {
        var model = Model(); Assert.Equal(0, model.AmbientDoseRateMicroSvPerHour);
        Assert.Equal("ideal environment", model.AmbientEnvironmentLabel);
        Assert.Contains("not validated", model.AmbientPresetName);
        model.Sources.Clear(); Assert.False(model.StartCommand.CanExecute(null));
        model.AmbientDoseRateMicroSvPerHour = .1; Assert.True(model.StartCommand.CanExecute(null));
        Assert.Equal("undefined (source-free)", model.AmbientBsrReadout);
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
    public void InvalidAmbientDoseReturnsToOff(double dose)
    {
        var model = Model(); model.AmbientDoseRateMicroSvPerHour = dose;
        Assert.Equal(0, model.AmbientDoseRateMicroSvPerHour);
    }
}
