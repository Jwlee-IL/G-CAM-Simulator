using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Configuration;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>A session-only draft. Validation is atomic; cancel never writes to the acquisition inputs.</summary>
public sealed partial class OptionsViewModel : ObservableObject
{
    private readonly MainViewModel _owner;
    private readonly Action _close;

    public OptionsViewModel(MainViewModel owner, Action close)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(close);
        _owner = owner;
        _close = close;
        SeedText = owner.SeedText;
        GapUm = owner.ReflectorGapUm;
        GainSigmaPercent = owner.GainSigmaPercent.ToString("G", CultureInfo.CurrentCulture);
        GainSeed = owner.GainSeed.ToString(CultureInfo.CurrentCulture);
        BackgroundToSignalRatio = owner.BackgroundToSignalRatio.ToString("G", CultureInfo.CurrentCulture);
        AmbientDoseText = owner.AmbientDoseText;
        AmbientGeometry = owner.AmbientGeometry;
        Scintillator = owner.Scintillator;
        Sensor = owner.Sensor;
        Preamp = owner.Preamp;
        owner.PropertyChanged += OwnerChanged;
    }
    public string Title => "Options";
    [ObservableProperty] private OptionsTab _selectedTab;
    [ObservableProperty] private bool _isOpen = true;
    [ObservableProperty] private string _seedText = "";
    [ObservableProperty] private string _gapUm = "";
    [ObservableProperty] private string _gainSigmaPercent = "";
    [ObservableProperty] private string _gainSeed = "";
    [ObservableProperty] private string _backgroundToSignalRatio = "";
    [ObservableProperty] private string _ambientDoseText = "";
    [ObservableProperty] private AmbientGeometry _ambientGeometry;
    [ObservableProperty] private ScintPreset _scintillator = null!;
    [ObservableProperty] private SensorPreset _sensor = null!;
    [ObservableProperty] private PreampPreset _preamp = null!;
    [ObservableProperty] private string? _seedError;
    [ObservableProperty] private string? _gapError;
    [ObservableProperty] private string? _gainSigmaError;
    [ObservableProperty] private string? _gainSeedError;
    [ObservableProperty] private string? _backgroundError;
    [ObservableProperty] private string? _ambientDoseError;
    public bool CanEditInputs => _owner.CanEditInputs;
    public bool CanEditLegacy => _owner.CanEditLegacy;
    public bool IsReadOnly => !CanEditInputs;
    public string? LockCaption => IsReadOnly ? "Acquired settings · locked until Reset" : null;
    public IReadOnlyList<ScintPreset> Scintillators => _owner.Scintillators;
    public IReadOnlyList<SensorPreset> Sensors => _owner.Sensors;
    public IReadOnlyList<PreampPreset> Preamps => _owner.Preamps;
    public IReadOnlyList<AmbientGeometry> AmbientGeometries => _owner.AmbientGeometries;
    public double EntranceAbsorberMm => _owner.Detector.EntranceAbsorberMm;
    public double BackingScatterMm => _owner.Detector.BackingScatterMm;
    public string AmbientPresetName => _owner.AmbientPresetName;
    public string AmbientEnvironmentLabel => StudioOptionValidation.TryAmbientDose(AmbientDoseText, out double dose) && dose == 0
        ? "ideal environment" : $"{_owner.AmbientPreset.Id} · {(AmbientGeometry == AmbientGeometry.FrontOnlyThroughMask ? "front-only bound" : "bare-crystal bound")}";
    public string AmbientBsrReadout => _owner.AmbientBsrReadout;
    partial void OnAmbientDoseTextChanged(string value) => OnPropertyChanged(nameof(AmbientEnvironmentLabel));
    partial void OnAmbientGeometryChanged(AmbientGeometry value) => OnPropertyChanged(nameof(AmbientEnvironmentLabel));
    private void OwnerChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (string name in new[] { nameof(CanEditInputs), nameof(CanEditLegacy), nameof(IsReadOnly), nameof(LockCaption), nameof(AmbientBsrReadout) })
            OnPropertyChanged(name);
    }
    [RelayCommand]
    private void Apply()
    {
        if (!IsOpen) return;
        if (IsReadOnly) { Cancel(); return; }
        SeedError = StudioOptionValidation.SeedError(SeedText);
        GapError = StudioOptionValidation.GapError(GapUm, _owner.OpticsEditor.Effective.PixelPitchMm);
        AmbientDoseError = StudioOptionValidation.TryAmbientDose(AmbientDoseText, out _) ? null : StudioOptionValidation.AmbientDoseMessage;
        BackgroundError = StudioOptionValidation.TryNonnegative(BackgroundToSignalRatio, out double background) ? null : "Background BSR must be finite and nonnegative.";
        GainSigmaError = CanEditLegacy && !StudioOptionValidation.TryNonnegative(GainSigmaPercent, out _) ? "Gain σ must be finite and nonnegative." : null;
        GainSeedError = CanEditLegacy && !int.TryParse(GainSeed, out _) ? "Gain seed must be an integer." : null;
        if (SeedError is not null || GapError is not null || GainSigmaError is not null || GainSeedError is not null || BackgroundError is not null || AmbientDoseError is not null)
        {
            SelectedTab = SeedError is not null ? OptionsTab.Acquisition : GapError is not null || GainSigmaError is not null || GainSeedError is not null ? OptionsTab.Detector : OptionsTab.Environment;
            return;
        }
        _owner.SeedText = SeedText;
        _owner.BackgroundToSignalRatio = background;
        _owner.AmbientDoseText = AmbientDoseText;
        _owner.AmbientGeometry = AmbientGeometry;
        if (CanEditLegacy)
        {
            _owner.Scintillator = Scintillator;
            _owner.Sensor = Sensor;
            _owner.Preamp = Preamp;
            _owner.GainSigmaPercent = double.Parse(GainSigmaPercent, CultureInfo.CurrentCulture);
            _owner.GainSeed = int.Parse(GainSeed, CultureInfo.CurrentCulture);
        }
        _owner.ReflectorGapUm = GapUm;
        Cancel();
    }
    [RelayCommand]
    private void Cancel()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _owner.PropertyChanged -= OwnerChanged;
        _close();
    }
}
