using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.Optics;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Where the acquisition is — drives the status-bar indicator and the command table.</summary>
public enum RunState
{
    /// <summary>No acquired data: physical inputs are editable.</summary>
    Empty,
    Failed,
    Acquiring,
    Stopped,
    Completed,
}

/// <summary>Shell state. Acquisition follows the multichannel-analyser model: Start acquires, Stop pauses keeping
/// everything, Start again continues the same acquisition, Reset discards it. Physical inputs (sources, optics,
/// detector, chain, background, seed) are locked while data exist; view settings never are.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAcquisitionService _acquisition;
    private readonly IThemeService _theme;
    private IAcquisitionSession? _session;
    private bool _reverting;

    public MainViewModel(IAcquisitionService acquisition, IThemeService theme, ISpectrumService spectrum, IImagingService? imaging = null,
        IWaveformService? waveform = null, IDetectorFaceService? detectorFace = null, IFocusSweepService? focusSweep = null, IReadoutPreparationService? readoutPreparation = null, IConfirmationService? confirmation = null)
    {
        Confirmation = confirmation;
        _readoutPreparation = readoutPreparation;
        if (_readoutPreparation is not null) _readoutPreparation.CacheChanged += ReadoutCacheChanged;
        _acquisition = acquisition;
        _theme = theme;
        Imaging = new ImagingWorkspaceViewModel(this, imaging, focusSweep);
        Spectrum = new SpectrumWorkspaceViewModel(this, spectrum);
        Waveform = new WaveformWorkspaceViewModel(this, waveform);
        DetectorWorkspace = new DetectorWorkspaceViewModel(this, detectorFace);
        Workspaces = new ReadOnlyObservableCollection<WorkspaceViewModel>(new ObservableCollection<WorkspaceViewModel> { Imaging, Spectrum, Waveform, DetectorWorkspace });
        foreach (var workspace in Workspaces) workspace.PropertyChanged += OnWorkspaceChanged;
        _selectedWorkspace = Imaging;
        Imaging.IsActive = true;
        Sources.CollectionChanged += OnSourcesChanged;
        OpticsEditor.Changed += (_, _) =>
        {
            if (OpticsEditor.Error is null && Optics != OpticsEditor.Effective) Optics = OpticsEditor.Effective;
            ValidateGap();
            ValidationError = OpticsEditor.Error ?? GapError;
        };
        DetectorWorkspace.Refresh();
        AddSource();
    }

    public IConfirmationService? Confirmation { get; }
    public ImagingWorkspaceViewModel Imaging { get; }
    public SpectrumWorkspaceViewModel Spectrum { get; }
    public WaveformWorkspaceViewModel Waveform { get; }
    public DetectorWorkspaceViewModel DetectorWorkspace { get; }
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
        Waveform.Refresh();
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

    // ---------------------------------------------------------------- state and command table

    /// <summary>Acquired data exist (Stopped, Completed, or Failed after a snapshot).</summary>
    public bool HasData => Snapshot is not null;
    /// <summary>Physical inputs are editable only without data and while not acquiring (A-2).</summary>
    public bool CanEditInputs => !IsPreparingReadout && !IsRunning && !HasData;
    /// <summary>The preset may be raised after Stop / Completed, never while acquiring or after a failure with data.</summary>
    public bool CanEditLiveTime => !IsRunning && (!HasData || _session is not null);
    /// <summary>Speed only paces events; it may change in every state but Acquiring.</summary>
    public bool CanEditSpeed => !IsRunning;
    public bool IsIdle => !IsRunning;
    public string StartLabel => HasData ? "Continue" : "Start";

    /// <summary>Re-evaluates everything that follows (running, data, session, preset).</summary>
    private void NotifyRunState()
    {
        foreach (string name in new[] { nameof(HasData), nameof(CanEditInputs), nameof(CanEditLiveTime), nameof(CanEditSpeed), nameof(IsIdle), nameof(StartLabel), nameof(CanEditLegacy) })
            OnPropertyChanged(name);
        OpticsEditor.IsEditable = CanEditInputs;
        foreach (var source in Sources) source.IsEditable = CanEditInputs;
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
        AddSourceCommand.NotifyCanExecuteChanged();
        RemoveSourceCommand.NotifyCanExecuteChanged();
        PrepareReadoutCommand.NotifyCanExecuteChanged();
        DetectorWorkspace.Refresh();
    }

    /// <summary>Reverts a physical-input change made while inputs are locked (bindings are disabled too; this is the
    /// guard for every other writer). Returns true when the change must not take effect.</summary>
    private bool Locked(Action revert, bool allowed = false)
    {
        if (_reverting) return true; // the nested change is the revert itself
        if (allowed || CanEditInputs) return false;
        _reverting = true;
        try { revert(); }
        finally { _reverting = false; }
        return true;
    }

    private bool LockedLegacy(Action revert)
    {
        if (_reverting) return true;
        if (CanEditLegacy) return false;
        _reverting = true;
        try { revert(); }
        finally { _reverting = false; }
        return true;
    }

    // ---------------------------------------------------------------- physical inputs

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
    // The detection chain moved to Options; its (unused) section view keeps this flag.
    [ObservableProperty] private bool _isChainExpanded = true;
    [ObservableProperty] private string? _validationError;
    public string DetectorSummary => $"gap {Detector.ReflectorGapMm * 1000:0.###} µm · gain σ {GainSigmaPercent:0.#}%";
    partial void OnOpticsChanged(OpticsSettings? oldValue, OpticsSettings newValue)
    {
        if (Locked(() => { Optics = oldValue!; OpticsEditor.Load(oldValue!); })) return;
        if (OpticsEditor.Effective != newValue) OpticsEditor.Load(newValue);
        Imaging.NotifyGeometryChanged();
        ValidateGap();
        DetectorWorkspace.Refresh();
        Imaging.InvalidateSweep();
        NotifyReadout();
    }
    /// <summary>Pending detector inputs; on the physical path they carry the captured bank of required line preparations.</summary>
    public DetectorSettings Detector => IsPhysicalReadout && MatchingBank is { Count: > 0 } bank
        ? ModuleDetector with { PreparedReadoutLines = new(bank.Select(p => p.Id).ToArray()) }
        : ModuleDetector;
    public IReadOnlyList<ScintPreset> Scintillators => FrontEndMaterials.Scintillators;
    public IReadOnlyList<SensorPreset> Sensors => FrontEndParts.Sensors;
    public IReadOnlyList<PreampPreset> Preamps => FrontEndParts.Preamps;
    private ScintPreset _scintillator = StudioOptionDefaults.Current.Chain.Scintillator;
    private SensorPreset _sensor = StudioOptionDefaults.Current.Chain.Sensor;
    private PreampPreset _preamp = StudioOptionDefaults.Current.Chain.Preamp;
    public ScintPreset Scintillator
    {
        get => _scintillator;
        set { if (CanEditLegacy && Scintillators.Contains(value) && SetProperty(ref _scintillator, value)) NotifyChainChanged(); else OnPropertyChanged(); }
    }
    public SensorPreset Sensor
    {
        get => _sensor;
        set { if (CanEditLegacy && Sensors.Contains(value) && SetProperty(ref _sensor, value)) NotifyChainChanged(); else OnPropertyChanged(); }
    }
    public PreampPreset Preamp
    {
        get => _preamp;
        set { if (CanEditLegacy && Preamps.Contains(value) && SetProperty(ref _preamp, value)) NotifyChainChanged(); else OnPropertyChanged(); }
    }
    public FrontEndChain Chain => new(Scintillator, Sensor, Preamp);
    public string ChainSummary => Chain.ToString();
    private void NotifyChainChanged()
    {
        OnPropertyChanged(nameof(Chain)); OnPropertyChanged(nameof(Detector)); OnPropertyChanged(nameof(ChainSummary));
        DetectorWorkspace.Refresh();
    }
    [ObservableProperty] private string _reflectorGapUm = StudioOptionDefaults.Current.GapUm.ToString("G", CultureInfo.CurrentCulture);
    [ObservableProperty] private string? _gapError;
    private double PendingGapMm => double.TryParse(ReflectorGapUm, out double gap) ? gap / 1000 : double.NaN;
    partial void OnReflectorGapUmChanged(string? oldValue, string newValue)
    {
        if (Locked(() => ReflectorGapUm = oldValue!)) return;
        ValidateGap();
        OnPropertyChanged(nameof(Detector)); OnPropertyChanged(nameof(DetectorSummary));
        NotifyReadout();
    }
    private void ValidateGap()
    {
        GapError = StudioOptionValidation.GapError(ReflectorGapUm, OpticsEditor.Effective.PixelPitchMm);
        ValidationError = OpticsEditor.Error ?? GapError;
    }
    [ObservableProperty] private double _gainSigmaPercent = StudioOptionDefaults.Current.GainSigmaPercent;
    [ObservableProperty] private int _gainSeed = StudioOptionDefaults.Current.GainSeed;
    [ObservableProperty] private double _backgroundToSignalRatio = StudioOptionDefaults.Current.BackgroundToSignalRatio;
    /// <summary>Photon H*(10) of the absolute ambient field; default 0.10 µSv/h (AB-15). 0 is the ideal environment.</summary>
    [ObservableProperty] private double _ambientDoseRateMicroSvPerHour = DefaultAmbientDoseRateMicroSvPerHour;
    [ObservableProperty] private AmbientGeometry _ambientGeometry = StudioOptionDefaults.Current.AmbientGeometry;
    public static double DefaultAmbientDoseRateMicroSvPerHour => StudioOptionDefaults.Current.AmbientDoseMicroSvPerHour;
    public IReadOnlyList<AmbientGeometry> AmbientGeometries { get; } = Enum.GetValues<AmbientGeometry>();
    /// <summary>The validated spectrum every non-zero field uses (the only user-facing preset, AB-15).</summary>
    public AmbientPreset AmbientPreset => AmbientPreset.TerrestrialUnscear2000V1;
    public string AmbientPresetName => AmbientPreset.Name;
    public string AmbientEnvironmentLabel => AmbientDoseRateMicroSvPerHour == 0 ? "ideal environment"
        : $"{AmbientPreset.Id} · {Snapshot?.AmbientIncidence ?? (IsPhysicalReadout
            ? AmbientIncidenceDescription.For(AmbientGeometry, true)
            : AmbientGeometry == AmbientGeometry.FrontOnlyThroughMask ? "front-only bound" : "bare-crystal bound")}";
    public string AmbientBsrReadout => AmbientDoseRateMicroSvPerHour == 0 ? "off"
        : Snapshot?.AmbientBackgroundToSignalRatio is { } ratio ? $"{ratio:G4} × signal"
        : Sources.Count == 0 ? "undefined (source-free)" : "available during acquisition";

    partial void OnAmbientDoseRateMicroSvPerHourChanged(double oldValue, double newValue)
    {
        if (Locked(() => AmbientDoseRateMicroSvPerHour = oldValue)) return;
        if (!StudioOptionValidation.Nonnegative(newValue))
        {
            // Refused (AB-16): the previous value stays; there is no silent fall back to the ideal environment.
            _reverting = true;
            try { AmbientDoseRateMicroSvPerHour = oldValue; }
            finally { _reverting = false; }
            return;
        }
        AmbientDoseError = null;
        string text = FormatAmbientDose(newValue);
        if (ParseAmbientDose(AmbientDoseText) != newValue) SetAmbientDoseText(text);
        OnPropertyChanged(nameof(AmbientEnvironmentLabel)); OnPropertyChanged(nameof(AmbientBsrReadout));
        StartCommand.NotifyCanExecuteChanged();
    }

    /// <summary>The dose-rate field as typed. It must match <see cref="AmbientDosePatternText"/> (digits with at most one
    /// decimal point: no sign, exponent, NaN or infinity); a matching entry sets the dose rate, any other entry is refused
    /// with <see cref="AmbientDoseError"/>, keeps the previous dose rate and blocks Start until corrected.</summary>
    [ObservableProperty] private string _ambientDoseText = FormatAmbientDose(DefaultAmbientDoseRateMicroSvPerHour);
    [ObservableProperty] private string? _ambientDoseError;
    public const string AmbientDosePatternText = StudioOptionValidation.AmbientDosePatternText;
    private static double ParseAmbientDose(string text) => StudioOptionValidation.TryAmbientDose(text, out double value) ? value : double.NaN;
    private static string FormatAmbientDose(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private bool _syncingDoseText;
    private void SetAmbientDoseText(string text)
    {
        _syncingDoseText = true;
        try { AmbientDoseText = text; }
        finally { _syncingDoseText = false; }
    }
    partial void OnAmbientDoseTextChanged(string? oldValue, string newValue)
    {
        if (_syncingDoseText) return;
        if (Locked(() => AmbientDoseText = oldValue ?? "")) return;
        double value = ParseAmbientDose(newValue ?? "");
        if (!double.IsFinite(value)) { AmbientDoseError = StudioOptionValidation.AmbientDoseMessage; return; } // previous dose rate stays
        AmbientDoseError = null;
        AmbientDoseRateMicroSvPerHour = value;
    }

    partial void OnAmbientGeometryChanged(AmbientGeometry oldValue, AmbientGeometry newValue)
    {
        if (Locked(() => AmbientGeometry = oldValue)) return;
        if (!Enum.IsDefined(newValue)) { AmbientGeometry = oldValue; return; }
        OnPropertyChanged(nameof(AmbientEnvironmentLabel));
    }
    partial void OnGainSigmaPercentChanged(double oldValue, double newValue)
    {
        if (LockedLegacy(() => GainSigmaPercent = oldValue)) return;
        if (!StudioOptionValidation.Nonnegative(newValue)) { GainSigmaPercent = StudioOptionDefaults.Current.GainSigmaPercent; return; }
        OnPropertyChanged(nameof(Detector));
        OnPropertyChanged(nameof(DetectorSummary));
        DetectorWorkspace.Refresh();
    }
    partial void OnGainSeedChanged(int oldValue, int newValue)
    {
        if (LockedLegacy(() => GainSeed = oldValue)) return;
        OnPropertyChanged(nameof(Detector)); DetectorWorkspace.Refresh();
    }
    partial void OnBackgroundToSignalRatioChanged(double oldValue, double newValue)
    {
        if (Locked(() => BackgroundToSignalRatio = oldValue)) return;
        if (!StudioOptionValidation.Nonnegative(newValue)) BackgroundToSignalRatio = StudioOptionDefaults.Current.BackgroundToSignalRatio;
    }

    /// <summary>Monte Carlo seed input: blank draws a new seed for every acquisition (independent measurements);
    /// a number fixes it to reproduce an acquisition. Continue keeps the acquisition's seed.</summary>
    [ObservableProperty] private string _seedText = StudioOptionDefaults.Current.SeedText;
    [ObservableProperty] private string? _seedError;
    /// <summary>Seed of the retained (or running) acquisition; null in Empty.</summary>
    [ObservableProperty] private int? _acquisitionSeed;
    partial void OnSeedTextChanged(string? oldValue, string newValue)
    {
        if (Locked(() => SeedText = oldValue ?? "")) return;
        SeedError = StudioOptionValidation.SeedError(newValue);
    }

    // ---------------------------------------------------------------- acquisition inputs (not physics)

    [ObservableProperty] private double _liveTimeS = StudioOptionDefaults.Current.LiveTimeS;
    [ObservableProperty] private string? _liveTimeError;
    [ObservableProperty] private double _speed = StudioOptionDefaults.Current.Speed;
    [ObservableProperty] private AcquisitionSnapshot? _snapshot;
    partial void OnSnapshotChanged(AcquisitionSnapshot? oldValue, AcquisitionSnapshot? newValue)
    {
        OnPropertyChanged(nameof(AmbientBsrReadout));
        Spectrum.Refresh(); Imaging.RefreshChannels(); Waveform.NotifySnapshot(); DetectorWorkspace.Refresh();
        OnPropertyChanged(nameof(ScopeEventCount));
        if (oldValue is null != newValue is null) NotifyRunState();
    }
    [ObservableProperty] private double _windowFwhm = StudioOptionDefaults.Current.WindowFwhm;
    partial void OnWindowFwhmChanged(double value)
    {
        if (!double.IsFinite(value) || value <= 0) { WindowFwhm = StudioOptionDefaults.Current.WindowFwhm; return; }
        Imaging.InvalidateSweep();
        Spectrum.NotifyWindowChanged();
        Imaging.RefreshChannels();
    }

    partial void OnLiveTimeSChanged(double oldValue, double newValue)
    {
        if (Locked(() => LiveTimeS = oldValue, allowed: CanEditLiveTime)) return;
        if (!(newValue > 0) || !double.IsFinite(newValue)) { LiveTimeS = StudioOptionDefaults.Current.LiveTimeS; return; }
        if (Snapshot is { } s && newValue < s.LiveTimeS)
        {
            // The preset can be raised to continue, never lowered below what was already acquired.
            LiveTimeError = $"The preset cannot be below the acquired live time ({s.LiveTimeS:0.###} s).";
            _reverting = true;
            try { LiveTimeS = oldValue; }
            finally { _reverting = false; }
            return;
        }
        LiveTimeError = null;
        StartCommand.NotifyCanExecuteChanged();
    }

    partial void OnSpeedChanged(double oldValue, double newValue)
    {
        if (Locked(() => Speed = oldValue, allowed: CanEditSpeed)) return;
        if (!(newValue > 0) || !double.IsFinite(newValue)) Speed = StudioOptionDefaults.Current.Speed;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand))]
    private bool _isRunning;
    partial void OnIsRunningChanged(bool value) => NotifyRunState();

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _status = "Ready";
    [ObservableProperty] private RunState _state = RunState.Empty;

    [ObservableProperty]
    private ImagingResult? _result;

    partial void OnResultChanged(ImagingResult? value) => Imaging.Refresh(value);

    /// <summary>Label for the theme toggle: the theme you would switch TO.</summary>
    public string ThemeToggleLabel => _theme.Current == AppTheme.Dark ? "Light theme" : "Dark theme";

    [RelayCommand]
    private void ToggleTheme()
    {
        _theme.Apply(_theme.Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
        OnPropertyChanged(nameof(ThemeToggleLabel));
    }

    [RelayCommand(CanExecute = nameof(CanEditInputs))]
    private void AddSource()
    {
        if (!CanEditInputs) return;
        // Spread new sources so they don't stack on the axis.
        double offset = Sources.Count * 15.0;
        var source = new SourceItemViewModel { X = offset, Y = 0 };
        Sources.Add(source);
        SelectedSource = source;
    }

    private bool CanRemoveSource() => CanEditInputs && SelectedSource is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveSource))]
    private void RemoveSource()
    {
        if (!CanRemoveSource()) return;
        int index = Sources.IndexOf(SelectedSource!);
        Sources.Remove(SelectedSource!);
        SelectedSource = Sources.Count == 0 ? null : Sources[Math.Min(index, Sources.Count - 1)];
    }

    /// <summary>Start (no data) or Continue (Stopped / Completed with the preset above the acquired live time).</summary>
    private bool CanStart() => !IsPreparingReadout && !IsRunning && (Sources.Count > 0 || AmbientDoseRateMicroSvPerHour > 0)
        && (Snapshot is not { } s || _session is not null && LiveTimeS > s.LiveTimeS);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop() => _session?.Stop();

    private bool CanReset() => Confirmation is not null && !IsRunning && HasData;

    /// <summary>Discards after deliberate confirmation; disabled while acquiring. Measurement shapes are kept.</summary>
    [RelayCommand(CanExecute = nameof(CanReset))]
    private async Task ResetAsync()
    {
        if (!CanReset()) return;
        string message = string.Create(CultureInfo.InvariantCulture,
            $"Discard the acquired data ({Snapshot!.LiveTimeS:0.0} live s, {Snapshot.Counts:N0} counts)? Inputs unlock for the next acquisition.");
        if (Confirmation is null || !await Confirmation.ConfirmAsync(message) || !CanReset()) return;
        var session = _session;
        _session = null;
        if (session is not null) await session.DisposeAsync();
        Result = null;
        Snapshot = null;
        AcquisitionSeed = null;
        Spectrum.Reset();
        Imaging.Reset();
        Waveform.Begin();
        Progress = 0;
        State = RunState.Empty;
        Status = "Ready";
        ClearStartRefusal();
        NotifyRunState();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (!CanStart()) return; // ExecuteAsync does not check CanExecute
        if (Snapshot is null)
        {
            if (!BeginAcquisition()) return;
        }
        else
        {
            try { _session!.Continue(LiveTimeS, Speed); }
            catch (Exception ex) { await FailAsync(ex); return; }
        }
        await ReadSegmentAsync(LiveTimeS);
    }

    /// <summary>Validates the pending inputs, draws or takes the seed, freezes the scene in the workspaces and starts
    /// a new session. False when validation fails or the service refuses (then Failed without data).</summary>
    private bool BeginAcquisition()
    {
        var scene = Sources.Select(s => s.ToModel()).ToArray();
        // Every refusal is visible: its reason in the status bar (the field messages stay where they are), the left tab
        // holding the offending input selected, and for an Options input the cog's warning state (the dialog opens only
        // on the user's click).
        string? optics = OpticsEditor.Error ?? OpticsPolicy.Validate(Optics, Detector.ReflectorGapMm);
        string? sceneError = OpticsPolicy.ValidateScene(Optics, scene);
        string? focus = OpticsPolicy.ValidateFocus(Optics, Imaging.FocalDistanceMm) ?? Imaging.FocusError;
        ValidationError = GapError ?? optics ?? sceneError ?? focus;
        if (GapError is not null) return RefuseStart(GapError, OpticsTab, options: true); // the gap is checked against the pixel pitch
        if (optics is not null) return RefuseStart(optics, OpticsTab);
        if (sceneError is not null) return RefuseStart(sceneError, SceneTab);
        if (focus is not null) { Imaging.IsFocalExpanded = true; return RefuseStart("Decoder focal plane: " + focus); }
        if (SeedError is not null) return RefuseStart(SeedError, options: true);
        if (AmbientDoseError is not null) return RefuseStart(AmbientDoseError, options: true);
        if (IsPhysicalReadout)
        {
            var required = RequiredReadoutLines;
            bool bsr = Sources.Count > 0 && BackgroundToSignalRatio != 0;
            ReadoutMessage = ReadoutPolicy.AcquisitionError(Sources.Count, BackgroundToSignalRatio)
                ?? (required.Count == 0
                    ? "No preparable calibration line is present in the scene: " + string.Join(" ", ReadoutPlans.Select(p => p.Reason))
                    : !IsReadoutBankReady
                        ? "Prepare the matching experimental readout before Start (every calibration line present in the scene: "
                            + string.Join(", ", required.Select(p => p.LineKeV.ToString("0.##", CultureInfo.InvariantCulture) + " keV")) + ")."
                        : null);
            if (ReadoutMessage is not null) return RefuseStart(ReadoutMessage,
                Sources.Count == 0 ? SceneTab : DetectorTab, options: bsr);
        }
        ClearStartRefusal();
        int seed = string.IsNullOrWhiteSpace(SeedText)
            ? Random.Shared.Next(1, int.MaxValue)
            : int.Parse(SeedText, NumberStyles.Integer, CultureInfo.CurrentCulture);
        var acquisitionOptics = Optics with { FocalDistanceMm = Imaging.FocalDistanceMm };
        Result = null;
        Progress = 0;
        Spectrum.Begin(scene);
        Imaging.Begin(scene, acquisitionOptics);
        Waveform.Begin();
        if (IsPhysicalReadout) { Imaging.Strip = false; Spectrum.PileUp = false; Waveform.Ideal = false; Waveform.RateStudy = false; }
        AcquisitionSeed = seed;
        try
        {
            _session = AmbientDoseRateMicroSvPerHour > 0
                ? _acquisition.StartAmbient(scene, acquisitionOptics, LiveTimeS, Speed,
                    AmbientPreset.Field(AmbientDoseRateMicroSvPerHour, AmbientGeometry),
                    Detector, BackgroundToSignalRatio, seed)
                : _acquisition.Start(scene, acquisitionOptics, LiveTimeS, Speed, Detector, BackgroundToSignalRatio, seed);
        }
        catch (Exception ex)
        {
            _session = null;
            State = RunState.Failed;
            Status = $"Failed: {ex.Message}";
            NotifyRunState();
            return false;
        }
        return true;
    }

    private async Task ReadSegmentAsync(double preset)
    {
        IsRunning = true;
        State = RunState.Acquiring;
        Status = Snapshot is { } s ? StatusLine(s, preset) : $"t = 0 s of {preset:G} s · 0 counts · 0 cps{SeedSuffix}";
        try
        {
            await foreach (var snapshot in _session!.ReadSnapshotsAsync())
            {
                Result = snapshot.Imaging;
                Progress = snapshot.LiveTimeS / preset;
                Status = StatusLine(snapshot, preset);
                Snapshot = snapshot;
                await Task.WhenAll(Spectrum.WhenUpdated, Imaging.WhenUpdated);
            }
            State = Snapshot?.IsCompleted == true ? RunState.Completed : RunState.Stopped;
            Status = $"{State} · {Status}";
        }
        catch (Exception ex)
        {
            await FailAsync(ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>A failed acquisition keeps what it published (locked, Reset only); its session is gone.</summary>
    private async Task FailAsync(Exception ex)
    {
        var session = _session;
        _session = null;
        if (session is not null)
        {
            try { await session.DisposeAsync(); } catch { /* already failed; the first error is the one shown */ }
        }
        State = RunState.Failed;
        Status = $"Failed: {ex.Message}";
        NotifyRunState();
    }

    private string SeedSuffix => AcquisitionSeed is { } seed ? $" · seed {seed}" : "";

    /// <summary>One status line: live time of preset, counts, observed rate (counts / live time — the rate the
    /// Detector panel shows too), seed, and the MC-limited note.</summary>
    private string StatusLine(AcquisitionSnapshot snapshot, double preset)
    {
        string limited = snapshot.IsMcLimited ? $" · MC-limited ×{snapshot.ActualSpeed:F2}" : "";
        return $"t = {snapshot.LiveTimeS:F1} s of {preset:G} s · {snapshot.Counts:N0} counts · {snapshot.ObservedRateCps:F0} cps{SeedSuffix}{limited}";
    }

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AmbientBsrReadout));
        foreach (SourceItemViewModel s in e.NewItems ?? Array.Empty<SourceItemViewModel>())
        {
            s.IsEditable = CanEditInputs;
            s.PropertyChanged += OnSourceIsotopeChanged;
        }
        foreach (SourceItemViewModel s in e.OldItems ?? Array.Empty<SourceItemViewModel>()) s.PropertyChanged -= OnSourceIsotopeChanged;
        StartCommand.NotifyCanExecuteChanged();
        NotifyReadout();
    }

    // The calibration lines a physical acquisition needs follow the scene's isotopes (SD-A1).
    private void OnSourceIsotopeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SourceItemViewModel.Isotope)) NotifyReadout();
        else if (e.PropertyName is nameof(SourceItemViewModel.X) or nameof(SourceItemViewModel.Y) or nameof(SourceItemViewModel.DistanceMm) or nameof(SourceItemViewModel.ActivityUCi)) Imaging.NotifyPhysicalChannel();
    }

    internal void NotifyFocalGeometryChanged() => OnPropertyChanged(nameof(FcfovHalfMm));
}
