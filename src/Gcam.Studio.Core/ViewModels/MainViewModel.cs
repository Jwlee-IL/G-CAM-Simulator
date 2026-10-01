using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.Optics;

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

    public MainViewModel(IAcquisitionService acquisition, IThemeService theme, ISpectrumService spectrum, IImagingService? imaging = null)
    {
        _acquisition = acquisition;
        _theme = theme;
        Imaging = new ImagingWorkspaceViewModel(this, imaging);
        Spectrum = new SpectrumWorkspaceViewModel(this, spectrum);
        Workspaces = new ReadOnlyObservableCollection<WorkspaceViewModel>(new ObservableCollection<WorkspaceViewModel> { Imaging, Spectrum });
        foreach (var workspace in Workspaces) workspace.PropertyChanged += OnWorkspaceChanged;
        _selectedWorkspace = Imaging;
        Imaging.IsActive = true;
        Sources.CollectionChanged += OnSourcesChanged;
        OpticsEditor.Changed += (_, _) =>
        {
            if (OpticsEditor.Error is null && Optics != OpticsEditor.Effective) Optics = OpticsEditor.Effective;
            if (OpticsEditor.Error is not null) MarkStale();
            ValidationError = OpticsEditor.Error;
        };
        AddSource();
    }

    public ImagingWorkspaceViewModel Imaging { get; }
    public SpectrumWorkspaceViewModel Spectrum { get; }
    public ReadOnlyObservableCollection<WorkspaceViewModel> Workspaces { get; }
    public bool HasWorkspaceSwitch => Workspaces.Count >= 2;
    private void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        // UIA SelectionItem.Select sets IsChecked without invoking the button's command.
        if (e.PropertyName == nameof(WorkspaceViewModel.IsActive) && sender is WorkspaceViewModel { IsActive: true } workspace)
            SelectedWorkspace = workspace;
    }
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
    public double FcfovHalfMm => Imaging.Geometry?.NominalHalfFieldMm ?? 0;
    public OpticsEditorViewModel OpticsEditor { get; } = new();
    [ObservableProperty] private bool _isOpticsExpanded = true;
    [ObservableProperty] private bool _isDetectorExpanded = true;
    [ObservableProperty] private string? _validationError;
    public string DetectorSummary => $"gap {Detector.ReflectorGapMm:0.##} mm · gain σ {GainSigmaPercent:0.#}%";
    partial void OnOpticsChanged(OpticsSettings value)
    {
        if (OpticsEditor.Effective != value) OpticsEditor.Load(value);
        Imaging.NotifyGeometryChanged();
        MarkStale();
    }
    partial void OnIsRunningChanged(bool value) => OpticsEditor.IsEditable = !value;
    public DetectorSettings Detector => new() { GainSigma = GainSigmaPercent / 100, GainSeed = GainSeed };
    [ObservableProperty] private double _gainSigmaPercent = 3;
    [ObservableProperty] private int _gainSeed = 1;
    [ObservableProperty] private double _backgroundToSignalRatio;
    partial void OnGainSigmaPercentChanged(double value)
    {
        if (!double.IsFinite(value) || value < 0) { GainSigmaPercent = 3; return; }
        OnPropertyChanged(nameof(Detector));
        OnPropertyChanged(nameof(DetectorSummary));
        MarkStale();
    }
    partial void OnGainSeedChanged(int value) { OnPropertyChanged(nameof(Detector)); MarkStale(); }
    partial void OnBackgroundToSignalRatioChanged(double value)
    {
        if (!double.IsFinite(value) || value < 0) { BackgroundToSignalRatio = 0; return; }
        MarkStale();
    }
    [ObservableProperty] private double _liveTimeS = 60;
    [ObservableProperty] private double _speed = 10;
    [ObservableProperty] private AcquisitionSnapshot? _snapshot;
    partial void OnSnapshotChanged(AcquisitionSnapshot? value) { Spectrum.Refresh(); Imaging.RefreshChannels(); }
    [ObservableProperty] private double _windowFwhm = 1.5;
    partial void OnWindowFwhmChanged(double value)
    {
        if (!double.IsFinite(value) || value <= 0) { WindowFwhm = 1.5; return; }
        Spectrum.NotifyWindowChanged();
        Imaging.RefreshChannels();
    }

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
        var scene = Sources.Select(s => s.ToModel()).ToArray();
        ValidationError = OpticsEditor.Error ?? OpticsPolicy.Validate(Optics, Detector.ReflectorGapMm)
            ?? OpticsPolicy.ValidateScene(Optics, scene) ?? OpticsPolicy.ValidateFocus(Optics, Imaging.FocalDistanceMm)
            ?? Imaging.FocusError;
        if (ValidationError is not null)
        {
            IsOpticsExpanded = true;
            IsDetectorExpanded = true;
            return;
        }
        var acquisitionOptics = Optics with { FocalDistanceMm = Imaging.FocalDistanceMm };
        IsRunning = true;
        State = RunState.Acquiring;
        Result = null;
        Snapshot = null;
        IsResultStale = false;
        Progress = 0;
        Status = $"t = 0 s of {LiveTimeS:G} s · 0 counts · 0 cps";
        double preset = LiveTimeS;
        Spectrum.Begin(scene);
        Imaging.Begin(scene, acquisitionOptics);
        try
        {
            await using var session = _acquisition.Start(scene, acquisitionOptics, preset, Speed, Detector, BackgroundToSignalRatio);
            _session = session;
            await foreach (var snapshot in session.ReadSnapshotsAsync())
            {
                Result = snapshot.Imaging;
                Progress = snapshot.LiveTimeS / preset;
                string limited = snapshot.IsMcLimited ? $" · MC-limited ×{snapshot.ActualSpeed:F2}" : "";
                Status = $"t = {snapshot.LiveTimeS:F1} s of {preset:G} s · {snapshot.Counts:N0} counts · {snapshot.RateCps:F0} cps{limited}";
                Snapshot = snapshot;
                await Task.WhenAll(Spectrum.WhenUpdated, Imaging.WhenUpdated);
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

    internal void NotifyFocalGeometryChanged() => OnPropertyChanged(nameof(FcfovHalfMm));

    private void MarkStale()
    {
        if (Result is not null) IsResultStale = true;
    }

}
