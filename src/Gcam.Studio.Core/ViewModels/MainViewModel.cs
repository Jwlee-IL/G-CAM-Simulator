using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISimulationService _simulation;

    public MainViewModel(ISimulationService simulation)
    {
        _simulation = simulation;
        Sources.CollectionChanged += (_, _) => RunCommand.NotifyCanExecuteChanged();
        AddSource();
    }

    public ObservableCollection<SourceItemViewModel> Sources { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSourceCommand))]
    private SourceItemViewModel? _selectedSource;

    [ObservableProperty] private OpticsSettings _optics = new();
    [ObservableProperty] private long _photons = 500_000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(AddSourceCommand), nameof(RemoveSourceCommand))]
    private bool _isRunning;

    public bool IsIdle => !IsRunning;

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _status = "Ready";
    [ObservableProperty] private ImagingResult? _result;

    partial void OnPhotonsChanged(long value)
    {
        if (value < 1_000) Photons = 1_000;
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void AddSource()
    {
        // Spread new sources so they don't stack on the axis.
        double offset = Sources.Count * 15.0;
        var source = new SourceItemViewModel { X = offset, Y = 0 };
        Sources.Add(source);
        SelectedSource = source;
    }

    private bool CanRemoveSource() => IsIdle && SelectedSource is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveSource))]
    private void RemoveSource()
    {
        if (SelectedSource is null) return;
        int index = Sources.IndexOf(SelectedSource);
        Sources.Remove(SelectedSource);
        SelectedSource = Sources.Count == 0 ? null : Sources[Math.Min(index, Sources.Count - 1)];
    }

    private bool CanRun() => Sources.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRun), IncludeCancelCommand = true)]
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        IsRunning = true;
        Progress = 0;
        Status = $"Simulating {Photons:N0} photons…";
        try
        {
            var scene = Sources.Select(s => s.ToModel()).ToArray();
            // Progress<T> posts reports asynchronously, so a late report can land after completion:
            // accept them only while running, and never let the bar move backwards.
            var progress = new Progress<double>(p =>
            {
                if (IsRunning && p > Progress) Progress = p;
            });
            var result = await _simulation.RunAsync(scene, Optics, Photons, progress, cancellationToken);
            Result = result;
            Progress = 1;
            Status = Describe(result);
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled — previous result kept";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private static string Describe(ImagingResult r)
    {
        string where = r.Estimate is { } e
            ? $"peak at ({e.Position.X:F1}, {e.Position.Y:F1}) mm, ghost margin {e.Confidence:F2}"
            : "no decode";
        return $"{r.EffectiveCounts:N1} effective counts in {r.Elapsed.TotalSeconds:F1} s · {where}";
    }
}
