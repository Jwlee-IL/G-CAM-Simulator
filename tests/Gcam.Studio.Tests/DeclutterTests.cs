using Gcam.Configuration;
using Gcam.Studio.Core.Detector;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class DeclutterTests
{
    [Fact]
    public async Task Reset_WithoutConfirmation_IsDisabledAndKeepsData()
    {
        var acquired = DetectorWorkspaceTests.Model();
        await acquired.StartCommand.ExecuteAsync(null);
        var vm = new MainViewModel(new UnusedAcquisition(), new Theme(), new FakeSpectrumService());
        vm.Snapshot = acquired.Snapshot;
        vm.State = RunState.Completed;
        Assert.NotNull(vm.Snapshot);
        Assert.False(vm.ResetCommand.CanExecute(null));
        await vm.ResetCommand.ExecuteAsync(null);
        Assert.Same(acquired.Snapshot, vm.Snapshot);
    }

    private sealed class Theme : IThemeService
    {
        public AppTheme Current => AppTheme.Dark;
        public void Apply(AppTheme theme) { }
    }
    private sealed class UnusedAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS,
            double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
            => throw new InvalidOperationException("No acquisition is expected.");
    }

    [Theory]
    [InlineData(12)]
    [InlineData(44)]
    public void SensorsAndChainEnds_HaveEqualFrameMargins(int sensors)
    {
        double half = ReadoutSensorLayout.CellSize(sensors) / 2;
        double left = ReadoutSensorLayout.Centre(0, sensors) - half;
        double right = 1 - (ReadoutSensorLayout.Centre(sensors - 1, sensors) + half);
        Assert.Equal(left, right, 12);
        Assert.Equal(1.5 * ReadoutSensorLayout.CellSize(sensors), left, 12);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reset_ConfirmsBeforeDiscard_OrKeepsData(bool confirmed)
    {
        var fake = new FakeConfirmation(confirmed);
        var vm = DetectorWorkspaceTests.Model(confirmation: fake);
        await vm.StartCommand.ExecuteAsync(null);
        var snapshot = vm.Snapshot;
        Assert.NotNull(snapshot);
        await vm.ResetCommand.ExecuteAsync(null);
        Assert.Equal(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"Discard the acquired data ({snapshot.LiveTimeS:0.0} live s, {snapshot.Counts:N0} counts)? Inputs unlock for the next acquisition."), fake.Message);
        if (confirmed) { Assert.Null(vm.Snapshot); Assert.True(vm.CanEditInputs); }
        else { Assert.Same(snapshot, vm.Snapshot); Assert.False(vm.CanEditInputs); }
    }

    [Fact]
    public void RemovedCostPresentation_HasNoViewModelMembersOrProfilePolicy()
    {
        foreach (string name in new[] { "ReadoutCostCell", "ReadoutCostInherited", "ReadoutCostStatus", "ReadoutCostStatusDetails", "ReadoutCostRows", "ReadoutCostCard", "ReadoutCostScope", "ReadoutCostHelp" })
            Assert.Null(typeof(ImagingWorkspaceViewModel).GetProperty(name));
        Assert.Null(typeof(ImagingWorkspaceViewModel).Assembly.GetType("Gcam.Studio.Core.ViewModels.ReadoutCostRowView"));
        Assert.Null(typeof(ReadoutPolicy).GetMethod("MatchesImagingProfile"));
    }
}
