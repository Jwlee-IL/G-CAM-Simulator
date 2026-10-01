using System.Runtime.CompilerServices;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Imaging;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class ImagingWorkspaceTests
{
    private sealed class Theme : IThemeService
    {
        public AppTheme Current => AppTheme.Dark;
        public void Apply(AppTheme theme) { }
    }
    private sealed class Acquisition : IAcquisitionService
    {
        public int Starts { get; private set; }
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene, OpticsSettings optics,
            double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0)
        { Starts++; return new Session(); }
    }
    private sealed class Session : IAcquisitionSession
    {
        public async IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            var image = new ImagingResult(new DetectorImage(4, 4), -1.5, 1, null, 0, 1, null, 10, TimeSpan.Zero);
            yield return new(60, 10, 1, 10, false, image, [new(1, 1, 662, 1)], TimeSpan.Zero, true);
        }
        public void Stop() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class Imaging : IImagingService
    {
        public List<(Guid Id, AcquisitionSnapshot Snapshot, IReadOnlyList<SceneSource> Scene, OpticsSettings Optics, ImagingSettings Settings)> Requests { get; } = [];
        public TaskCompletionSource<ImagingView>? Pending { get; set; }
        public ImagingView? Latest { get; private set; }
        public Task<ImagingView> ProcessAsync(Guid acquisitionId, AcquisitionSnapshot snapshot,
            IReadOnlyList<SceneSource> scene, OpticsSettings optics, ImagingSettings settings, CancellationToken cancellationToken = default)
        {
            Requests.Add((acquisitionId, snapshot, scene, optics, settings));
            var channels = new List<ImagingChannel> { new("All", double.NaN, double.NaN, snapshot.Imaging, []) };
            foreach (var isotope in scene.Select(s => s.Isotope).Distinct())
            {
                var flood = new DetectorImage(4, 4); flood[1, 1] = settings.Strip ? 3 : 5;
                channels.Add(new(isotope, 600, 720, snapshot.Imaging with { Flood = flood, EffectiveCounts = flood[1, 1] },
                    [new(isotope, 2, 3, 10)]));
            }
            Latest = new(channels, [new("Cs-137", "Co-60", 20000, 1000, 5000)], TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            return Pending?.Task ?? Task.FromResult(Latest!);
        }
    }

    [Fact]
    public async Task SharedWindow_SelectorStripAndRoi_ReuseFrozenAcquisition()
    {
        var acquisition = new Acquisition(); var imaging = new Imaging(); var spectrum = new FakeSpectrumService();
        var vm = new MainViewModel(acquisition, new Theme(), spectrum, imaging);
        vm.AddSourceCommand.Execute(null); vm.Sources[1].Isotope = "Co-60";
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "All", "Cs-137", "Co-60" }, vm.Imaging.Isotopes);
        Assert.Same(vm.Result, vm.Imaging.Result);
        vm.Imaging.Measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Roi,
            [new(-2, -2), new(2, 2)]));
        int calls = imaging.Requests.Count;
        vm.Imaging.SelectedIsotope = "Cs-137";
        Assert.Single(vm.Imaging.Peaks);
        Assert.Contains("5", vm.Imaging.Measurements.Items[0].Value);
        Assert.Equal(calls, imaging.Requests.Count); // Selection does not decode on the UI or call the worker.
        vm.Spectrum.WindowFwhm = 2;
        await Task.WhenAll(vm.Imaging.WhenUpdated, vm.Spectrum.WhenUpdated);
        Assert.Equal(2, vm.WindowFwhm); Assert.Equal(2, imaging.Requests[^1].Settings.WindowFwhm);
        Assert.Equal(2, spectrum.Settings[^1].WindowFwhm);
        vm.Imaging.Strip = true; await vm.Imaging.WhenUpdated;
        Assert.Contains("3", vm.Imaging.Measurements.Items[0].Value);
        var request = imaging.Requests[^1];
        vm.Sources[1].Isotope = "Co-57"; vm.Optics = new() { CellPitchMm = 1 };
        vm.WindowFwhm = 1; await Task.WhenAll(vm.Imaging.WhenUpdated, vm.Spectrum.WhenUpdated);
        Assert.True(vm.IsResultStale);
        Assert.Equal("Co-60", imaging.Requests[^1].Scene[1].Isotope);
        Assert.Equal(request.Optics, imaging.Requests[^1].Optics);
        Assert.Equal(request.Id, imaging.Requests[^1].Id);
        Assert.Same(request.Snapshot, imaging.Requests[^1].Snapshot);
        Assert.Equal(1, acquisition.Starts);
    }

    [Fact]
    public async Task LateResponse_CannotReplaceNewerWindowResult()
    {
        var imaging = new Imaging();
        var vm = new MainViewModel(new Acquisition(), new Theme(), new FakeSpectrumService(), imaging);
        await vm.StartCommand.ExecuteAsync(null);
        imaging.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.WindowFwhm = 2;
        var oldTask = vm.Imaging.WhenUpdated;
        var oldView = imaging.Latest!;
        var pending = imaging.Pending;
        imaging.Pending = null;
        vm.WindowFwhm = 3;
        pending.SetResult(oldView); await oldTask;
        Assert.NotSame(oldView, vm.Imaging.View);
        Assert.Equal(3, imaging.Requests[^1].Settings.WindowFwhm);
        Assert.False(vm.Imaging.IsProcessing);
    }
}
