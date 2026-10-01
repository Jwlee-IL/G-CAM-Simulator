using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public class MainViewModelTests
{
    /// <summary>Fake simulation: reports progress, then either returns a canned result or waits for cancellation.</summary>
    private sealed class FakeSimulation : ISimulationService
    {
        public bool BlockUntilCancelled { get; init; }
        public Exception? Throw { get; init; }
        public IReadOnlyList<SceneSource>? LastScene { get; private set; }
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ImagingResult> RunAsync(IReadOnlyList<SceneSource> scene, OpticsSettings optics, long photons,
            IProgress<double>? progress, CancellationToken cancellationToken)
        {
            LastScene = scene;
            Started.TrySetResult();
            progress?.Report(0.5);
            if (Throw is not null) throw Throw;
            if (BlockUntilCancelled) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new ImagingResult(new DetectorImage(4, 4), null, 0, 0,
                new SourceEstimate(new Vector3(1, 2, 0), 2.5), 1234, TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public void Starts_with_one_selected_source()
    {
        var vm = new MainViewModel(new FakeSimulation());
        Assert.Single(vm.Sources);
        Assert.Same(vm.Sources[0], vm.SelectedSource);
        Assert.True(vm.RunCommand.CanExecute(null));
    }

    [Fact]
    public void Add_selects_new_source_and_remove_selects_neighbour()
    {
        var vm = new MainViewModel(new FakeSimulation());
        vm.AddSourceCommand.Execute(null);
        vm.AddSourceCommand.Execute(null);
        Assert.Equal(3, vm.Sources.Count);
        Assert.Same(vm.Sources[2], vm.SelectedSource);
        Assert.NotEqual(vm.Sources[0].X, vm.Sources[1].X);   // new sources are spread, not stacked

        vm.SelectedSource = vm.Sources[1];
        vm.RemoveSourceCommand.Execute(null);
        Assert.Equal(2, vm.Sources.Count);
        Assert.Same(vm.Sources[1], vm.SelectedSource);
    }

    [Fact]
    public void Removing_every_source_disables_remove_and_run()
    {
        var vm = new MainViewModel(new FakeSimulation());
        vm.RemoveSourceCommand.Execute(null);
        Assert.Empty(vm.Sources);
        Assert.Null(vm.SelectedSource);
        Assert.False(vm.RemoveSourceCommand.CanExecute(null));
        Assert.False(vm.RunCommand.CanExecute(null));
    }

    [Fact]
    public async Task Run_passes_scene_and_publishes_result()
    {
        var sim = new FakeSimulation();
        var vm = new MainViewModel(sim);
        vm.SelectedSource!.X = 12;

        await vm.RunCommand.ExecuteAsync(null);

        Assert.NotNull(vm.Result);
        Assert.False(vm.IsRunning);
        Assert.Equal(1.0, vm.Progress);
        Assert.Contains("1,234.0 effective counts", vm.Status);
        Assert.Equal(12, Assert.Single(sim.LastScene!).X);
    }

    [Fact]
    public async Task Cancel_keeps_previous_result_and_reenables_editing()
    {
        var vm = new MainViewModel(new FakeSimulation());
        await vm.RunCommand.ExecuteAsync(null);
        var first = vm.Result;

        var blocking = new FakeSimulation { BlockUntilCancelled = true };
        var vm2 = new MainViewModel(blocking) { Result = first };
        var run = vm2.RunCommand.ExecuteAsync(null);
        await blocking.Started.Task;
        Assert.True(vm2.IsRunning);
        Assert.False(vm2.AddSourceCommand.CanExecute(null));   // scene is locked while running

        vm2.RunCancelCommand.Execute(null);
        await run;

        Assert.False(vm2.IsRunning);
        Assert.Same(first, vm2.Result);
        Assert.StartsWith("Cancelled", vm2.Status);
        Assert.True(vm2.AddSourceCommand.CanExecute(null));
    }

    [Fact]
    public async Task Failure_is_reported_not_thrown()
    {
        var vm = new MainViewModel(new FakeSimulation { Throw = new InvalidOperationException("boom") });
        await vm.RunCommand.ExecuteAsync(null);
        Assert.False(vm.IsRunning);
        Assert.Equal("Failed: boom", vm.Status);
    }

    [Fact]
    public void Source_values_are_clamped_and_label_follows_edits()
    {
        var s = new SourceItemViewModel();
        var changed = new List<string?>();
        s.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        s.DistanceMm = 50;
        s.ActivityUCi = -3;
        s.Isotope = "Unobtainium-1";

        Assert.Equal(200, s.DistanceMm);
        Assert.Equal(1, s.ActivityUCi);
        Assert.Equal("Cs-137", s.Isotope);
        Assert.Contains(nameof(SourceItemViewModel.Label), changed);
    }
}
