using Gcam.Studio.Core.Services;
using Gcam.Configuration;
using Gcam.Core;
using System.Threading.Channels;

namespace Gcam.Studio.Tests;

public sealed class FocusSweepViewModelTests
{
    [Fact]
    public async Task Sweep_DoesNotHoldSnapshotConsumption_AndLabelsFrozenPrefix()
    {
        var service = new PendingSweep(); var acquisition = new StreamingAcquisition();
        var vm = DetectorWorkspaceTests.Model(service, acquisition);
        var start = vm.StartCommand.ExecuteAsync(null);
        Assert.True(vm.IsRunning); Assert.Equal(10, vm.Snapshot!.Counts);
        var sweep = vm.Imaging.SweepCommand.ExecuteAsync(null);
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Snapshot)) published.TrySetResult(); };
        acquisition.Snapshots.Writer.TryWrite(StreamingAcquisition.Snapshot(20));
        // A deadlock deadline, not a physics or publisher-frequency tolerance.
        await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(sweep.IsCompleted); Assert.Equal(20, vm.Snapshot.Counts);
        Assert.Equal(10, service.Request!.Identity.Counts);
        service.Complete(); await sweep;
        Assert.Equal(10, vm.Imaging.SweepResult!.Identity.Counts);
        acquisition.Snapshots.Writer.TryComplete(); await start;
    }

    private sealed class StreamingAcquisition : IAcquisitionService, IAcquisitionSession
    {
        public Channel<AcquisitionSnapshot> Snapshots { get; } = Channel.CreateUnbounded<AcquisitionSnapshot>();
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS,
            double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0)
        { Snapshots.Writer.TryWrite(Snapshot(10)); return this; }
        public IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync(CancellationToken cancellationToken = default)
            => Snapshots.Reader.ReadAllAsync(cancellationToken);
        public void Stop() => Snapshots.Writer.TryComplete();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public static AcquisitionSnapshot Snapshot(int counts)
        {
            var flood = new DetectorImage(30, 30); flood[1, 1] = counts;
            var image = new ImagingResult(flood.ReadOnlyCopy(), -8.7, .6, null, 0, 0, null, counts, TimeSpan.Zero);
            return new(counts, counts, 1, 1, false, image, [], TimeSpan.Zero, false) { Optics = new(), Detector = new() };
        }
    }
    [Fact]
    public async Task Sweep_FreezesIdentity_UserK_AndNeverDelaysAcquisition()
    {
        var service = new PendingSweep(); var vm = DetectorWorkspaceTests.Model(service);
        await vm.StartCommand.ExecuteAsync(null);
        vm.Imaging.PeakCount = 4;
        var task = vm.Imaging.SweepCommand.ExecuteAsync(null);
        Assert.True(vm.Imaging.IsSweeping); Assert.Equal(4, service.Request!.Identity.PeakCount);
        Assert.Same(vm.Result, service.Request.Image); Assert.True(vm.Snapshot!.IsCompleted);
        service.Complete(); await task;
        Assert.Equal(service.Request.Identity, vm.Imaging.SweepResult!.Identity);
        Assert.False(vm.Imaging.IsSweeping);
    }
    [Theory]
    [InlineData("window")]
    [InlineData("strip")]
    [InlineData("optics")]
    [InlineData("K")]
    [InlineData("cancel")]
    [InlineData("acquisition")]
    public async Task Sweep_RejectsLateResult_WhenIdentityChanges(string edit)
    {
        var service = new PendingSweep(); var vm = DetectorWorkspaceTests.Model(service);
        await vm.StartCommand.ExecuteAsync(null);
        var task = vm.Imaging.SweepCommand.ExecuteAsync(null);
        switch (edit)
        {
            case "window": vm.WindowFwhm = 2; break;
            case "strip": vm.Imaging.Strip = true; break;
            case "optics": vm.Optics = vm.Optics with { CellPitchMm = 1 }; break;
            case "K": vm.Imaging.PeakCount = 2; break;
            case "cancel": vm.Imaging.CancelSweepCommand.Execute(null); break;
            case "acquisition": await vm.StartCommand.ExecuteAsync(null); break;
        }
        Assert.True(service.Token.IsCancellationRequested);
        service.Complete(); await task;
        Assert.Null(vm.Imaging.SweepResult); Assert.False(vm.Imaging.IsSweeping);
    }
    [Fact]
    public void ExternalRange_OnlyExplicitActionChangesFocus()
    {
        var vm = DetectorWorkspaceTests.Model();
        vm.Imaging.ExternalRange = "500";
        Assert.Equal(1000, vm.Imaging.FocalDistanceMm);
        Assert.Single(vm.Imaging.FocusMarkers);
        vm.Imaging.UseAsFocusCommand.Execute(null);
        Assert.Equal(500, vm.Imaging.FocalDistanceMm); Assert.False(vm.IsResultStale);
        vm.Imaging.ExternalRange = "NaN";
        Assert.NotNull(vm.Imaging.ExternalRangeError); Assert.False(vm.Imaging.UseAsFocusCommand.CanExecute(null));
        vm.Imaging.ExternalRange = ""; Assert.Null(vm.Imaging.ExternalRangeError);
    }
    private sealed class PendingSweep : IFocusSweepService
    {
        private readonly TaskCompletionSource<FocusSweepResult> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public FocusSweepRequest? Request { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<FocusSweepResult> SweepAsync(FocusSweepRequest request, CancellationToken cancellationToken = default)
        { Request = request; Token = cancellationToken; return _pending.Task; }
        public void Complete() => _pending.SetResult(new(Request!.Identity, [], TimeSpan.Zero));
    }
}
