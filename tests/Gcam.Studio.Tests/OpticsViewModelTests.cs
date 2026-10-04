using System.Threading.Channels;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Optics;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class OpticsViewModelTests
{
    private sealed class Theme : IThemeService
    {
        public AppTheme Current => AppTheme.Dark;
        public void Apply(AppTheme theme) { }
    }
    private sealed class Session : IAcquisitionSession
    {
        private Channel<AcquisitionSnapshot> _channel = Channel.CreateUnbounded<AcquisitionSnapshot>();
        public int Continues { get; private set; }
        public void Continue(double presetLiveTimeS, double speed) { Continues++; _channel = Channel.CreateUnbounded<AcquisitionSnapshot>(); }
        public void Publish(int count, bool completed)
        {
            var flood = new DetectorImage(30, 30); flood[15, 15] = count;
            var image = new ImagingResult(flood.ReadOnlyCopy(), -8.7, 0.6, null, 0, 0, null, count, TimeSpan.Zero);
            _channel.Writer.TryWrite(new(count, count, 1, 1, false, image,
                Enumerable.Range(0, count).Select(i => new DetectedEvent(15, 15, 662, i + 1)).ToArray(), TimeSpan.Zero, completed));
            if (completed) Stop();
        }
        public IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync(CancellationToken cancellationToken = default)
            => _channel.Reader.ReadAllAsync(cancellationToken);
        public void Stop() => _channel.Writer.TryComplete();
        public ValueTask DisposeAsync() { Stop(); return ValueTask.CompletedTask; }
    }
    private sealed class Acquisition : IAcquisitionService
    {
        public int Starts { get; private set; }
        public Session Session { get; private set; } = null!;
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics, double liveTimeS,
            double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
        { Starts++; return Session = new(); }
    }
    private sealed class Imaging : IImagingService
    {
        public List<(Guid Id, OpticsSettings Optics, double? Focus, long Count)> Requests { get; } = [];
        public TaskCompletionSource<ImagingView>? Pending { get; set; }
        public ImagingView? PendingView { get; private set; }
        public Task<ImagingView> ProcessAsync(Guid acquisitionId, AcquisitionSnapshot snapshot, IReadOnlyList<SceneSource> scene,
            OpticsSettings optics, ImagingSettings settings, CancellationToken cancellationToken = default)
        {
            Requests.Add((acquisitionId, optics, settings.FocalDistanceMm, snapshot.Counts));
            var result = snapshot.Imaging with { ReconOriginMm = -settings.FocalDistanceMm!.Value,
                ReconStepMm = 1, Reconstruction = new DetectorImage(4, 4).ReadOnlyCopy() };
            var view = new ImagingView([new("All", double.NaN, double.NaN, result, [])], [], TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            PendingView = view;
            return Pending?.Task ?? Task.FromResult(view);
        }
    }
    private static async Task Until(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(1, timeout.Token);
    }

    [Fact]
    public async Task Focus_AcquiringStoppedCompleted_KeepsEventsAndSpectrum_OpticsLockedWithData()
    {
        var acquisition = new Acquisition(); var imaging = new Imaging(); var spectrum = new FakeSpectrumService();
        var vm = new MainViewModel(acquisition, new Theme(), spectrum, imaging) { AmbientDoseRateMicroSvPerHour = 0 };
        var run = vm.StartCommand.ExecuteAsync(null);
        Assert.False(vm.OpticsEditor.IsEditable);
        acquisition.Session.Publish(1, false);
        await Until(() => vm.Imaging.View is not null);
        vm.Imaging.FocalPlane = "800"; await vm.Imaging.WhenUpdated;
        Assert.Equal(-800, vm.Imaging.Result!.ReconOriginMm);
        vm.StopCommand.Execute(null); await run;
        var snapshot = vm.Snapshot;
        int spectrumCalls = spectrum.Calls;
        vm.Imaging.Measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Roi, [new(-1, -1), new(1, 1)]));
        vm.Imaging.Measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Reconstruction, MeasurementKind.Distance, [new(0, 0), new(1, 1)]));
        vm.Imaging.FocalPlane = "600"; await vm.Imaging.WhenUpdated;
        Assert.Equal(RunState.Stopped, vm.State);
        Assert.Single(vm.Imaging.Measurements.Items);
        Assert.Equal(ImagePane.Flood, vm.Imaging.Measurements.Items[0].Pane);
        Assert.NotNull(vm.Imaging.FocusNote);
        Assert.False(vm.OpticsEditor.IsEditable);
        vm.OpticsEditor.CellPitch = "0.8"; // locked with data: reverted
        Assert.Equal("0.7", vm.OpticsEditor.CellPitch);
        vm.Imaging.FocalPlane = "500"; await vm.Imaging.WhenUpdated; // focus stays a view setting
        Assert.Same(snapshot, vm.Snapshot);
        Assert.Equal(spectrumCalls, spectrum.Calls);
        Assert.Equal(0.7, imaging.Requests[^1].Optics.CellPitchMm);
        Assert.Equal(1, acquisition.Starts);
        var id = imaging.Requests[^1].Id;
        run = vm.StartCommand.ExecuteAsync(null); // Continue
        Assert.Equal(1, acquisition.Session.Continues);
        acquisition.Session.Publish(2, true); await run;
        Assert.Equal(RunState.Completed, vm.State);
        Assert.Equal(1, acquisition.Starts);
        Assert.Equal(id, imaging.Requests[^1].Id); // the services keep appending to one acquisition
        Assert.Equal(2, imaging.Requests[^1].Count);
        vm.Imaging.FocalPlane = "400"; await vm.Imaging.WhenUpdated;
        Assert.Equal(-400, vm.Imaging.Result!.ReconOriginMm);
    }

    [Fact]
    public async Task LatestFocus_CoalescesChangesAndRejectsLateSnapshotProjection()
    {
        var acquisition = new Acquisition(); var imaging = new Imaging();
        var vm = new MainViewModel(acquisition, new Theme(), new FakeSpectrumService(), imaging) { AmbientDoseRateMicroSvPerHour = 0 };
        var run = vm.StartCommand.ExecuteAsync(null);
        acquisition.Session.Publish(1, false); await Until(() => vm.Imaging.View is not null);
        imaging.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Imaging.FocalPlane = "800";
        var pending = imaging.Pending; var oldView = imaging.PendingView!;
        imaging.Pending = null;
        vm.Imaging.FocalPlane = "700";
        vm.Imaging.FocalPlane = "600";
        acquisition.Session.Publish(2, false);
        await Until(() => vm.Snapshot?.Counts == 2);
        pending.SetResult(oldView);
        await vm.Imaging.WhenUpdated;
        Assert.Equal(-600, vm.Imaging.Result!.ReconOriginMm);
        Assert.Equal(2, vm.Imaging.Result.EffectiveCounts);
        Assert.NotSame(oldView, vm.Imaging.View);
        Assert.DoesNotContain(imaging.Requests, r => r.Focus == 700);
        vm.StopCommand.Execute(null); await run;
    }

    [Fact]
    public async Task OldAcquisitionWorker_CannotPublishIntoNewAcquisition()
    {
        var acquisition = new Acquisition(); var imaging = new Imaging();
        var vm = new MainViewModel(acquisition, new Theme(), new FakeSpectrumService(), imaging) { AmbientDoseRateMicroSvPerHour = 0 };
        var run = vm.StartCommand.ExecuteAsync(null);
        acquisition.Session.Publish(1, true); await run;
        imaging.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Imaging.FocalPlane = "800";
        var pending = imaging.Pending; var oldView = imaging.PendingView!; var oldTask = vm.Imaging.WhenUpdated;
        imaging.Pending = null;
        await vm.ResetCommand.ExecuteAsync(null); // a new acquisition (not Continue) starts after Reset
        run = vm.StartCommand.ExecuteAsync(null);
        acquisition.Session.Publish(2, true); await run;
        var current = vm.Imaging.View;
        pending.SetResult(oldView); await oldTask;
        Assert.Same(current, vm.Imaging.View);
        Assert.Equal(2, vm.Imaging.Result!.EffectiveCounts);
        Assert.False(vm.Imaging.IsProcessing);
    }

    [Fact]
    public async Task InvalidInput_StartExpandsSectionsAndDoesNotStartTransport()
    {
        var acquisition = new Acquisition();
        var vm = new MainViewModel(acquisition, new Theme(), new FakeSpectrumService());
        vm.IsOpticsExpanded = vm.IsDetectorExpanded = false;
        vm.OpticsEditor.PixelPitch = "0.1";
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(0, acquisition.Starts);
        Assert.True(vm.IsOpticsExpanded); Assert.True(vm.IsDetectorExpanded);
        Assert.Contains("reflector", vm.ValidationError);
        vm.OpticsEditor.SelectedPreset = OpticsPreset.All[1];
        vm.OpticsEditor.Distance = "1000";
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(0, acquisition.Starts);
        Assert.Contains("front face", vm.ValidationError);
        vm.OpticsEditor.SelectedPreset = OpticsPreset.All[1];
        vm.Imaging.FocalPlane = "NaN";
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(0, acquisition.Starts);
        Assert.NotNull(vm.Imaging.FocusError);
    }
}
