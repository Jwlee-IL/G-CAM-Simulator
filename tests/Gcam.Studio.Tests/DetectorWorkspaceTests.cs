using System.Runtime.CompilerServices;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Detector;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class DetectorWorkspaceTests
{
    internal static MainViewModel Model(IFocusSweepService? focus = null, IAcquisitionService? acquisition = null) => new(acquisition ?? new Acquisition(), new Theme(),
        new FakeSpectrumService(), detectorFace: new Face(), focusSweep: focus) { AmbientDoseRateMicroSvPerHour = 0 };

    [Theory]
    [InlineData("NaN")]
    [InlineData("-1")]
    [InlineData("600")]
    [InlineData("bad")]
    public async Task Gap_InvalidEditPreventsStart(string value)
    {
        var vm = Model(); vm.ReflectorGapUm = value;
        Assert.NotNull(vm.GapError); await vm.StartCommand.ExecuteAsync(null);
        Assert.Null(vm.Snapshot); Assert.NotNull(vm.ValidationError);
    }
    [Fact]
    public void Gap_RevalidatesAfterPitchChange_AndConvertsUnits()
    {
        var vm = Model(); vm.ReflectorGapUm = "200";
        Assert.Equal(.2, vm.Detector.ReflectorGapMm); Assert.Null(vm.GapError);
        vm.Optics = vm.Optics with { PixelPitchMm = .15 };
        Assert.NotNull(vm.GapError);
        vm.Optics = vm.Optics with { PixelPitchMm = .3 };
        Assert.Null(vm.GapError);
    }
    [Fact]
    public void Gap_ZeroAllowsPitchBelowOldDefaultGap()
    {
        var vm = Model(); vm.ReflectorGapUm = "0"; vm.OpticsEditor.PixelPitch = "0.05";
        Assert.Null(vm.OpticsEditor.Error); Assert.Null(vm.GapError);
        Assert.Equal(.05, vm.Optics.PixelPitchMm);
    }
    [Fact]
    public async Task Face_FollowsPendingBeforeStart_AcquiredAndLockedAfter_PendingAgainAfterReset()
    {
        var vm = Model(); vm.ReflectorGapUm = "20";
        Assert.Equal("Settings for the next acquisition", vm.DetectorWorkspace.Identity);
        double area = vm.DetectorWorkspace.Face!.ActiveAreaFraction;
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(.02, vm.Snapshot!.Detector!.ReflectorGapMm);
        Assert.Equal("Acquired settings · locked until Reset", vm.DetectorWorkspace.Identity);
        vm.ReflectorGapUm = "100"; vm.GainSeed = 2; // locked: reverted
        Assert.Equal("20", vm.ReflectorGapUm);
        Assert.Equal(1, vm.GainSeed);
        Assert.Equal(area, vm.DetectorWorkspace.Face!.ActiveAreaFraction);
        Assert.Contains("Gap 20 µm", vm.DetectorWorkspace.Readout);
        await vm.ResetCommand.ExecuteAsync(null);
        Assert.Equal("Settings for the next acquisition", vm.DetectorWorkspace.Identity);
        vm.ReflectorGapUm = "100";
        Assert.Contains("Gap 100 µm", vm.DetectorWorkspace.Readout);
        Assert.NotEqual(area, vm.DetectorWorkspace.Face!.ActiveAreaFraction);
    }
    private sealed class Face : IDetectorFaceService
    {
        public DetectorFace Build(OpticsSettings optics, DetectorSettings detector) => DetectorFace.Create(
            optics.DetectorPixels, optics.PixelPitchMm, detector.ReflectorGapMm,
            Enumerable.Repeat(1d, optics.DetectorPixels * optics.DetectorPixels).ToArray());
    }
    private sealed class Theme : IThemeService
    {
        public AppTheme Current => AppTheme.Dark;
        public void Apply(AppTheme theme) { }
    }
    private sealed class Acquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
            double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
            => new Session(optics, detector!);
    }
    private sealed class Session(OpticsSettings optics, DetectorSettings detector) : IAcquisitionSession
    {
        public async IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            var flood = new DetectorImage(optics.DetectorPixels, optics.DetectorPixels); flood[1, 1] = 10;
            var image = new ImagingResult(flood.ReadOnlyCopy(), 0, optics.PixelPitchMm, null, 0, 0, null, 10, TimeSpan.Zero);
            yield return new(60, 10, 1, 1, false, image, [], TimeSpan.Zero, true) { Detector = detector, Optics = optics };
        }
        public void Stop() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
