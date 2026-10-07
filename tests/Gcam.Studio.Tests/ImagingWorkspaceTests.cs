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
            double liveTimeS, double speed, DetectorSettings? detector = null, double backgroundToSignalRatio = 0, int? seed = null)
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
            var channels = new List<ImagingChannel>();
            foreach (var isotope in scene.Select(s => s.Isotope).Distinct())
            {
                var flood = new DetectorImage(4, 4); flood[1, 1] = settings.Strip ? 3 : 5;
                channels.Add(new(isotope, 600, 720, snapshot.Imaging with { Flood = flood, EffectiveCounts = flood[1, 1],
                    StripCount = settings.Strip && isotope == "Cs-137" ? new(3, 4, true) : null,
                    Estimate = new SourceEstimate(new Vector3(2, 3, 1000), 1) }, [new(isotope, 2, 3, 10)]));
            }
            // As the service: All carries the union of the isotope channels' found peaks.
            channels.Insert(0, new("All", double.NaN, double.NaN, snapshot.Imaging, channels.SelectMany(c => c.Peaks).ToArray()));
            // As the service: the view records the method it was decoded with.
            Latest = new(channels, [new("Cs-137", "Co-60", 20000, 1000, 5000)], TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero) { Method = settings.Method };
            return Pending?.Task ?? Task.FromResult(Latest!);
        }
    }

    private sealed class DeferredFocus : IFocusSweepService
    {
        public CancellationToken Token { get; private set; }
        public FocusSweepRequest? Request { get; private set; }
        public TaskCompletionSource<FocusSweepResult> Pending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<FocusSweepResult> SweepAsync(FocusSweepRequest request, CancellationToken cancellationToken = default)
        { Request = request; Token = cancellationToken; return Pending.Task; }
    }

    [Fact]
    public async Task FocusSweep_ChannelChangeCancelsAndRejectsLateResult()
    {
        var focus = new DeferredFocus();
        var vm = new MainViewModel(new Acquisition(), new Theme(), new FakeSpectrumService(), new Imaging(), focusSweep: focus) { AmbientDoseRateMicroSvPerHour = 0 };
        await vm.StartCommand.ExecuteAsync(null);
        vm.Imaging.SelectedIsotope = "Cs-137";
        var task = vm.Imaging.SweepCommand.ExecuteAsync(null);
        Assert.Equal("Cs-137", focus.Request!.Identity.Channel);
        vm.Imaging.SelectedIsotope = "All";
        Assert.True(focus.Token.IsCancellationRequested);
        focus.Pending.SetResult(new(focus.Request.Identity, [], TimeSpan.Zero));
        await task;
        Assert.Null(vm.Imaging.SweepResult);
    }

    [Fact]
    public async Task SharedWindow_SelectorStripAndRoi_ReuseFrozenAcquisition()
    {
        var acquisition = new Acquisition(); var imaging = new Imaging(); var spectrum = new FakeSpectrumService();
        var vm = new MainViewModel(acquisition, new Theme(), spectrum, imaging) { AmbientDoseRateMicroSvPerHour = 0 };
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
        vm.Sources[1].Isotope = "Co-57"; vm.Optics = new() { CellPitchMm = 1 }; // locked with data: refused
        vm.WindowFwhm = 1; await Task.WhenAll(vm.Imaging.WhenUpdated, vm.Spectrum.WhenUpdated);
        Assert.Equal("Co-60", vm.Sources[1].Isotope);
        Assert.Equal(0.7, vm.Optics.CellPitchMm);
        Assert.Equal("Co-60", imaging.Requests[^1].Scene[1].Isotope);
        Assert.Equal(request.Optics, imaging.Requests[^1].Optics);
        Assert.Equal(request.Id, imaging.Requests[^1].Id);
        Assert.Same(request.Snapshot, imaging.Requests[^1].Snapshot);
        Assert.Equal(1, acquisition.Starts);
    }

    [Fact]
    public async Task PeakChip_CountsSeveralFoundPeaks_AndNamesASingleOne()
    {
        var vm = new MainViewModel(new Acquisition(), new Theme(), new FakeSpectrumService(), new Imaging()) { AmbientDoseRateMicroSvPerHour = 0 };
        vm.AddSourceCommand.Execute(null); vm.Sources[1].Isotope = "Co-60";
        await vm.StartCommand.ExecuteAsync(null);
        await vm.Imaging.WhenUpdated;
        Assert.Equal(2, vm.Imaging.Peaks.Count);
        Assert.Equal("2 peaks found", vm.Imaging.PeakText);
        vm.Imaging.SelectedIsotope = "Cs-137";
        Assert.Equal("peak (2.0, 3.0) mm", vm.Imaging.PeakText);
    }

    [Fact]
    public async Task LateResponse_CannotReplaceNewerWindowResult()
    {
        var imaging = new Imaging();
        var vm = new MainViewModel(new Acquisition(), new Theme(), new FakeSpectrumService(), imaging) { AmbientDoseRateMicroSvPerHour = 0 };
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

    [Fact]
    public async Task Reconstruction_IsAReprojectionSetting_KeepsMeasurementsAndSweep()
    {
        // TODO-36 (MD-3, MD-7): cross-correlation by default; MLEM is sent to the worker in ImagingSettings with every other
        // setting unchanged, one request per change, without new transport, measurement clearing or sweep invalidation.
        var acquisition = new Acquisition(); var imaging = new Imaging(); var focus = new DeferredFocus();
        var vm = new MainViewModel(acquisition, new Theme(), new FakeSpectrumService(), imaging, focusSweep: focus) { AmbientDoseRateMicroSvPerHour = 0 };
        Assert.Equal(DecoderMethod.CrossCorrelation, vm.Imaging.Reconstruction);
        Assert.Equal(new[] { DecoderMethod.CrossCorrelation, DecoderMethod.Mlem }, vm.Imaging.Reconstructions.Select(r => r.Method));
        Assert.Equal("(decoded)", vm.Imaging.ReconstructionUnit);
        Assert.Null(vm.Imaging.ReconstructionNote);
        await vm.StartCommand.ExecuteAsync(null);
        await vm.Imaging.WhenUpdated;
        Assert.Equal(DecoderMethod.CrossCorrelation, imaging.Requests[^1].Settings.Method);
        vm.Imaging.Measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Reconstruction, MeasurementKind.Roi, [new(-2, -2), new(2, 2)]));
        vm.Imaging.SelectedIsotope = "Cs-137";
        var sweep = vm.Imaging.SweepCommand.ExecuteAsync(null);
        focus.Pending.SetResult(new(focus.Request!.Identity, [], TimeSpan.Zero));
        await sweep;
        Assert.NotNull(vm.Imaging.SweepResult);
        var before = imaging.Requests[^1];
        int calls = imaging.Requests.Count;

        vm.Imaging.Reconstruction = DecoderMethod.Mlem;
        await vm.Imaging.WhenUpdated;
        Assert.Equal(calls + 1, imaging.Requests.Count);
        var after = imaging.Requests[^1];
        Assert.Equal(before.Settings with { Method = DecoderMethod.Mlem }, after.Settings);
        Assert.Equal(before.Id, after.Id);
        Assert.Same(before.Snapshot, after.Snapshot);
        Assert.Equal(1, acquisition.Starts);
        Assert.Single(vm.Imaging.Measurements.Items);           // same grid, same mm: kept
        Assert.NotNull(vm.Imaging.SweepResult);                  // the sweep cross-correlates whatever the method
        Assert.Equal("(MLEM λ)", vm.Imaging.ReconstructionUnit);
        Assert.Contains($"{StudioMlem.Iterations} iterations", vm.Imaging.ReconstructionNote);
        Assert.DoesNotContain("not measured", vm.Imaging.ReconstructionNote);
        Assert.EndsWith("· MLEM", vm.Imaging.ChannelSummary);
    }

    [Fact]
    public async Task ReconstructionNote_FlagsUnmeasuredOptics_AndTheAcquisitionImageIsNotShownAsMlem()
    {
        var vm = new MainViewModel(new Acquisition(), new Theme(), new FakeSpectrumService(), new Imaging()) { AmbientDoseRateMicroSvPerHour = 0 };
        vm.Imaging.Reconstruction = DecoderMethod.Mlem;
        vm.Imaging.FocalPlane = "1500";   // another decoder plane than the one the count was selected at
        Assert.Contains("not measured", vm.Imaging.ReconstructionNote);
        vm.Imaging.FocalPlane = "1000";
        Assert.DoesNotContain("not measured", vm.Imaging.ReconstructionNote);
        vm.Optics = new() { CellPitchMm = 1 };
        Assert.Contains("not measured", vm.Imaging.ReconstructionNote);
        // Before a worker view exists, All falls back to the acquisition's own image — a cross-correlation decode, so not under MLEM.
        await vm.StartCommand.ExecuteAsync(null);
        vm.Imaging.View = null;
        Assert.Null(vm.Imaging.Result);
        vm.Imaging.Reconstruction = DecoderMethod.CrossCorrelation;
        vm.Imaging.View = null;
        Assert.Same(vm.Result, vm.Imaging.Result);
    }

    [Fact]
    public async Task StripCount_IsLabelledNetWithUncertainty_ForBothMethods()
    {
        // TODO-39: the same signed estimator for either decoder, distinct from the displayed clipped flood.
        var vm = new MainViewModel(new Acquisition(), new Theme(), new FakeSpectrumService(), new Imaging()) { AmbientDoseRateMicroSvPerHour = 0 };
        await vm.StartCommand.ExecuteAsync(null);
        vm.Imaging.SelectedIsotope = "Cs-137";
        Assert.Contains(" counts · ", vm.Imaging.Summary);
        vm.Imaging.Strip = true; await vm.Imaging.WhenUpdated;
        Assert.Contains("3 ± 2 net counts (1σ, counting + calibration)", vm.Imaging.Summary);
        Assert.Equal("(clipped strip values)", vm.Imaging.FloodUnit);
        Assert.Equal("clipped values", vm.Imaging.FloodCaption);
        Assert.Contains("net count is reported separately", vm.Imaging.FloodHelp);
        Assert.Contains("Cross-correlation: signed difference", vm.Imaging.StripNote);
        vm.Imaging.Reconstruction = DecoderMethod.Mlem; await vm.Imaging.WhenUpdated;
        Assert.Contains("3 ± 2 net counts (1σ, counting + calibration)", vm.Imaging.Summary);
        Assert.Contains("MLEM: raw low", vm.Imaging.StripNote);
        vm.Imaging.SelectedIsotope = "All";   // All is never stripped
        Assert.DoesNotContain("net", vm.Imaging.Summary);
        Assert.Equal("counts", vm.Imaging.FloodUnit);
        Assert.Equal("counts", vm.Imaging.FloodCaption);
        Assert.Null(vm.Imaging.StripNote);
    }

    [Fact]
    public async Task ReconstructionUnit_DescribesTheDisplayedImage_UntilTheReDecodedViewArrives()
    {
        // TODO-38: an MLEM refresh takes longer than a tick, so after a switch the pane still shows the previous
        // (cross-correlation) image for a while. Its unit and count label must describe that image, not the selector.
        var imaging = new Imaging();
        var vm = new MainViewModel(new Acquisition(), new Theme(), new FakeSpectrumService(), imaging) { AmbientDoseRateMicroSvPerHour = 0 };
        await vm.StartCommand.ExecuteAsync(null);
        await vm.Imaging.WhenUpdated;
        vm.Imaging.SelectedIsotope = "Cs-137";
        vm.Imaging.Strip = true; await vm.Imaging.WhenUpdated;
        var ccView = vm.Imaging.View;
        imaging.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Imaging.Reconstruction = DecoderMethod.Mlem;            // request in flight
        Assert.Same(ccView, vm.Imaging.View);
        Assert.Equal(DecoderMethod.CrossCorrelation, vm.Imaging.DisplayedMethod);
        Assert.Equal("(decoded)", vm.Imaging.ReconstructionUnit);
        // (The summary line reads "Building channels…" while a request is in flight; its count label is checked after.)
        Assert.NotNull(vm.Imaging.ReconstructionNote);               // the setting's caveats show at once
        imaging.Pending.SetResult(imaging.Latest!);
        await vm.Imaging.WhenUpdated;
        Assert.Equal(DecoderMethod.Mlem, vm.Imaging.DisplayedMethod);
        Assert.Equal("(MLEM λ)", vm.Imaging.ReconstructionUnit);
        Assert.Contains("net counts (1σ, counting + calibration)", vm.Imaging.Summary);
    }

    [Fact]
    public async Task StripMetadata_AndRoiUnits_FollowPublishedViewWhileStripIsPending()
    {
        var imaging = new Imaging();
        var vm = new MainViewModel(new Acquisition(), new Theme(), new FakeSpectrumService(), imaging) { AmbientDoseRateMicroSvPerHour = 0 };
        await vm.StartCommand.ExecuteAsync(null);
        vm.Imaging.SelectedIsotope = "Cs-137";
        vm.Imaging.Measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Roi, [new(-2,-2),new(2,2)]));
        imaging.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Imaging.Strip = true;
        Assert.Equal("counts", vm.Imaging.FloodUnit);
        Assert.Null(vm.Imaging.StripNote);
        Assert.DoesNotContain("clipped", vm.Imaging.Measurements.Items[0].Value);
        imaging.Pending.SetResult(imaging.Latest!);
        await vm.Imaging.WhenUpdated;
        Assert.Equal("Σ 3 clipped strip values", vm.Imaging.Measurements.Items[0].Value);
        Assert.Contains("mean 0.188 clipped strip values", vm.Imaging.Measurements.Items[0].Detail);
        imaging.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Imaging.Strip = false;
        Assert.Equal("(clipped strip values)", vm.Imaging.FloodUnit);
        Assert.Contains("signed difference", vm.Imaging.StripNote);
        Assert.Contains("clipped", vm.Imaging.Measurements.Items[0].Description);
        imaging.Pending.SetResult(imaging.Latest!);
        await vm.Imaging.WhenUpdated;
        Assert.DoesNotContain("clipped", vm.Imaging.Measurements.Items[0].Value);
    }

    [Fact]
    public async Task NegativeNetAndUnavailableUncertainty_KeepSweepEnabledAndDisplayedRoiValues()
    {
        var imaging = new Imaging(); var focus = new DeferredFocus();
        var vm = new MainViewModel(new Acquisition(), new Theme(), new FakeSpectrumService(), imaging, focusSweep: focus) { AmbientDoseRateMicroSvPerHour = 0 };
        await vm.StartCommand.ExecuteAsync(null);
        vm.Imaging.SelectedIsotope = "Cs-137";
        imaging.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.Imaging.Strip = true;
        var view = imaging.Latest!;
        var cs = view.Channels.Single(c => c.Isotope == "Cs-137");
        var negative = cs with { Image = cs.Image with { EffectiveCounts = -7, StripCount = new(-7, null, true) } };
        imaging.Pending.SetResult(view with { Channels = view.Channels.Select(c => c.Isotope == "Cs-137" ? negative : c).ToArray() });
        await vm.Imaging.WhenUpdated;
        Assert.Contains("-7 net counts (uncertainty unavailable)", vm.Imaging.Summary);
        Assert.True(vm.Imaging.SweepCommand.CanExecute(null));
        vm.Imaging.Measurements.AddCommand.Execute(new MeasurementDraft(ImagePane.Flood, MeasurementKind.Roi, [new(-2,-2),new(2,2)]));
        Assert.Equal("Σ 3 clipped strip values", vm.Imaging.Measurements.Items[0].Value);
    }
}
