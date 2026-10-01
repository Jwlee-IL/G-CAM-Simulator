using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Where the last run ended up — drives the status-bar indicator.</summary>
public enum RunState
{
    Idle,
    Running,
    Succeeded,
    Cancelled,
    Failed,
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISimulationService _simulation;
    private readonly IThemeService _theme;

    public MainViewModel(ISimulationService simulation, IThemeService theme)
    {
        _simulation = simulation;
        _theme = theme;
        Sources.CollectionChanged += OnSourcesChanged;
        AddSource();
    }

    /// <summary>Measurement tools, overlays and the results table.</summary>
    public MeasurementsViewModel Measurements { get; } = new();

    public ObservableCollection<SourceItemViewModel> Sources { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSourceCommand))]
    private SourceItemViewModel? _selectedSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FcfovHalfMm))]
    private OpticsSettings _optics = new();

    /// <summary>Half-width of the fully-coded field of view at the focal plane (mm).</summary>
    public double FcfovHalfMm => SceneConfigBuilder.FcfovHalfMm(Optics);
    [ObservableProperty] private long _photons = 500_000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(AddSourceCommand), nameof(RemoveSourceCommand))]
    private bool _isRunning;

    public bool IsIdle => !IsRunning;

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _status = "Ready";
    [ObservableProperty] private RunState _state = RunState.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeakText))]
    private ImagingResult? _result;

    /// <summary>The scene was edited after the shown result was simulated — the images no longer match it.</summary>
    [ObservableProperty] private bool _isResultStale;

    partial void OnResultChanged(ImagingResult? value) => Measurements.Refresh(value);

    /// <summary>Decoded peak position for the reconstruction header, or null before the first run.</summary>
    public string? PeakText => Result?.Estimate is { } e ? $"peak ({e.Position.X:F1}, {e.Position.Y:F1}) mm" : null;

    /// <summary>Label for the theme toggle: the theme you would switch TO.</summary>
    public string ThemeToggleLabel => _theme.Current == AppTheme.Dark ? "Light theme" : "Dark theme";

    [RelayCommand]
    private void ToggleTheme()
    {
        _theme.Apply(_theme.Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
        OnPropertyChanged(nameof(ThemeToggleLabel));
    }

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
        State = RunState.Running;
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
            IsResultStale = false;
            Progress = 1;
            Status = Describe(result);
            State = RunState.Succeeded;
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled — previous result kept";
            State = RunState.Cancelled;
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
            State = RunState.Failed;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (SourceItemViewModel s in e.OldItems ?? Array.Empty<SourceItemViewModel>()) s.PropertyChanged -= OnSourceEdited;
        foreach (SourceItemViewModel s in e.NewItems ?? Array.Empty<SourceItemViewModel>()) s.PropertyChanged += OnSourceEdited;
        RunCommand.NotifyCanExecuteChanged();
        MarkStale();
    }

    private void OnSourceEdited(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SourceItemViewModel.Label) or nameof(SourceItemViewModel.MarkerLabel))) MarkStale();
    }

    private void MarkStale()
    {
        if (Result is not null) IsResultStale = true;
    }

    private static string Describe(ImagingResult r)
    {
        string where = r.Estimate is { } e
            ? $"peak at ({e.Position.X:F1}, {e.Position.Y:F1}) mm, ghost margin {e.Confidence:F2}"
            : "no decode";
        return $"{r.EffectiveCounts:N1} effective counts in {r.Elapsed.TotalSeconds:F1} s · {where}";
    }
}
