using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Where the acquisition ended up — drives the status-bar indicator.</summary>
public enum RunState
{
    Idle,
    Failed,
    Acquiring,
    Stopped,
    Completed,
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAcquisitionService _acquisition;
    private readonly IThemeService _theme;
    private IAcquisitionSession? _session;

    public MainViewModel(IAcquisitionService acquisition, IThemeService theme)
    {
        _acquisition = acquisition;
        _theme = theme;
        Imaging = new ImagingWorkspaceViewModel(this);
        Workspaces = new ReadOnlyObservableCollection<WorkspaceViewModel>(new ObservableCollection<WorkspaceViewModel> { Imaging });
        _selectedWorkspace = Imaging;
        Imaging.IsActive = true;
        Sources.CollectionChanged += OnSourcesChanged;
        AddSource();
    }

    public ImagingWorkspaceViewModel Imaging { get; }
    public ReadOnlyObservableCollection<WorkspaceViewModel> Workspaces { get; }
    public bool HasWorkspaceSwitch => Workspaces.Count >= 2;
    [ObservableProperty] private WorkspaceViewModel _selectedWorkspace;

    partial void OnSelectedWorkspaceChanged(WorkspaceViewModel value)
    {
        foreach (var workspace in Workspaces) workspace.IsActive = ReferenceEquals(workspace, value);
    }

    [RelayCommand]
    private void ActivateWorkspace(WorkspaceViewModel workspace)
    {
        if (Workspaces.Contains(workspace)) SelectedWorkspace = workspace;
    }

    [RelayCommand]
    private void SelectWorkspace(string index)
    {
        if (int.TryParse(index, out int i) && i >= 0 && i < Workspaces.Count)
            SelectedWorkspace = Workspaces[i];
    }

    public ObservableCollection<SourceItemViewModel> Sources { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSourceCommand))]
    private SourceItemViewModel? _selectedSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FcfovHalfMm))]
    private OpticsSettings _optics = new();

    /// <summary>Half-width of the fully-coded field of view at the focal plane (mm).</summary>
    public double FcfovHalfMm => SceneConfigBuilder.FcfovHalfMm(Optics);
    partial void OnOpticsChanged(OpticsSettings value) => MarkStale();
    [ObservableProperty] private double _liveTimeS = 60;
    [ObservableProperty] private double _speed = 10;
    [ObservableProperty] private AcquisitionSnapshot? _snapshot;

    partial void OnLiveTimeSChanged(double value)
    {
        if (!(value > 0) || !double.IsFinite(value)) LiveTimeS = 60;
        MarkStale();
    }

    partial void OnSpeedChanged(double value)
    {
        if (!(value > 0) || !double.IsFinite(value)) Speed = 10;
        MarkStale();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(AddSourceCommand), nameof(RemoveSourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand))]
    private bool _isRunning;

    public bool IsIdle => !IsRunning;

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _status = "Ready";
    [ObservableProperty] private RunState _state = RunState.Idle;

    [ObservableProperty]
    private ImagingResult? _result;

    /// <summary>The scene was edited after the shown result was simulated — the images no longer match it.</summary>
    [ObservableProperty] private bool _isResultStale;

    partial void OnResultChanged(ImagingResult? value) => Imaging.Refresh(value);

    /// <summary>Label for the theme toggle: the theme you would switch TO.</summary>
    public string ThemeToggleLabel => _theme.Current == AppTheme.Dark ? "Light theme" : "Dark theme";

    [RelayCommand]
    private void ToggleTheme()
    {
        _theme.Apply(_theme.Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
        OnPropertyChanged(nameof(ThemeToggleLabel));
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

    private bool CanStart() => IsIdle && Sources.Count > 0;

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop() => _session?.Stop();

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        IsRunning = true;
        State = RunState.Acquiring;
        Result = null;
        Snapshot = null;
        IsResultStale = false;
        Progress = 0;
        Status = $"t = 0 s of {LiveTimeS:G} s · 0 counts · 0 cps";
        double preset = LiveTimeS;
        try
        {
            await using var session = _acquisition.Start(Sources.Select(s => s.ToModel()).ToArray(), Optics, preset, Speed);
            _session = session;
            await foreach (var snapshot in session.ReadSnapshotsAsync())
            {
                Result = snapshot.Imaging;
                Progress = snapshot.LiveTimeS / preset;
                string limited = snapshot.IsMcLimited ? $" · MC-limited ×{snapshot.ActualSpeed:F2}" : "";
                Status = $"t = {snapshot.LiveTimeS:F1} s of {preset:G} s · {snapshot.Counts:N0} counts · {snapshot.RateCps:F0} cps{limited}";
                Snapshot = snapshot;
            }
            State = Snapshot?.IsCompleted == true ? RunState.Completed : RunState.Stopped;
            Status = $"{State} · {Status}";
        }
        catch (Exception ex)
        {
            State = RunState.Failed;
            Status = $"Failed: {ex.Message}";
        }
        finally
        {
            _session = null;
            IsRunning = false;
        }
    }

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (SourceItemViewModel s in e.OldItems ?? Array.Empty<SourceItemViewModel>()) s.PropertyChanged -= OnSourceEdited;
        foreach (SourceItemViewModel s in e.NewItems ?? Array.Empty<SourceItemViewModel>()) s.PropertyChanged += OnSourceEdited;
        StartCommand.NotifyCanExecuteChanged();
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

}
