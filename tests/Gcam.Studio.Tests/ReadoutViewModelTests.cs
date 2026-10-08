using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class ReadoutViewModelTests
{
    private static readonly OpticsSettings Head = new() { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
    private static MainViewModel Model() => new(new RefusingAcquisition(), new Theme(), new FakeSpectrumService(), readoutPreparation: new Preparation());

    [Fact]
    public void GeometryRefusalAndLegacyLocks_HaveActionableReasons()
    {
        var vm = Model(); vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        Assert.Equal(ReadoutMode.DirectCrystal, vm.ReadoutMode);
        Assert.Contains("12 × 12", vm.ReadoutMessage);
        vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        Assert.True(vm.IsPhysicalReadout); Assert.False(vm.CanEditLegacy);
        var before = vm.Detector; vm.GainSigmaPercent = 9; vm.GainSeed = 999;
        Assert.Equal(before, vm.Detector);
        vm.Spectrum.PileUp = true; vm.Imaging.Strip = true; vm.Waveform.Ideal = true; vm.Waveform.RateStudy = true;
        Assert.False(vm.Spectrum.PileUp); Assert.False(vm.Imaging.Strip); Assert.False(vm.Waveform.Ideal); Assert.False(vm.Waveform.RateStudy);
        vm.Optics = new(); Assert.Equal(Head, vm.Optics);
    }

    [Fact]
    public async Task StartRefusesFieldAndMissingPreparationWithoutSilentZeroing()
    {
        var vm = Model(); vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        vm.AmbientDoseRateMicroSvPerHour = 1; vm.BackgroundToSignalRatio = .5;
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Contains("zero", vm.ReadoutMessage); Assert.Equal(1, vm.AmbientDoseRateMicroSvPerHour); Assert.Equal(.5, vm.BackgroundToSignalRatio);
        vm.AmbientDoseRateMicroSvPerHour = 0; vm.BackgroundToSignalRatio = 0;
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Contains("Prepare", vm.ReadoutMessage); Assert.Null(vm.Snapshot);
    }

    [Fact]
    public async Task PreparationKeyInvalidationAndWindowValidation_AreExplicit()
    {
        var vm = Model(); vm.Optics = Head; vm.ReadoutMode = ReadoutMode.FourOutputAnger;
        await vm.PrepareReadoutCommand.ExecuteAsync(null);
        Assert.NotNull(vm.MatchingPreparation); Assert.StartsWith("Ready", vm.ReadoutState);
        vm.ReflectorGapUm = "90";
        Assert.Null(vm.MatchingPreparation); Assert.Equal("Not prepared", vm.ReadoutState);
        vm.WindowLowKeV = 721; Assert.Equal(600, vm.WindowLowKeV); Assert.Contains("low < high", vm.ReadoutMessage);
        vm.WindowLowKeV = 500; Assert.Equal(500, vm.WindowLowKeV);
        Assert.False(vm.IsPreparingReadout);
    }

    private sealed class Preparation : IReadoutPreparationService
    {
        public Task<ReadoutPreparation> PrepareAsync(OpticsSettings optics, DetectorSettings detector, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReadoutPreparation(Guid.NewGuid(), ReadoutPolicy.Key(optics, detector), true, null,
                new DetectorImage(1,1).ReadOnlyCopy(), [-1], [], [], 0,0,0,0,TimeSpan.Zero,8,100,1,1000,100));
    }
    private sealed class Theme : IThemeService
    {
        public AppTheme Current { get; private set; }
        public void Apply(AppTheme theme) => Current = theme;
    }
    private sealed class RefusingAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
            => throw new InvalidOperationException("This service must not be called before validation.");
    }
}
