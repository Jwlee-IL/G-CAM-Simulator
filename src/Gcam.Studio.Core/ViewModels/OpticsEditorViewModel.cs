using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Gcam.Configuration;
using Gcam.Studio.Core.Optics;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Retains invalid text so a failed edit cannot silently start the previous geometry.</summary>
public sealed partial class OpticsEditorViewModel : ObservableObject
{
    private bool _loading;
    public event EventHandler? Changed;
    public OpticsSettings Effective { get; private set; } = new();
    public IReadOnlyList<int> Ranks => OpticsPolicy.Ranks;
    public IReadOnlyList<OpticsPreset> Presets => OpticsPreset.All;
    [ObservableProperty] private bool _isEditable = true;
    [ObservableProperty] private int _rank = 13;
    [ObservableProperty] private string _cellPitch = "0.7";
    [ObservableProperty] private string _distance = "80";
    [ObservableProperty] private string _pixels = "30";
    [ObservableProperty] private string _pixelPitch = "0.6";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private OpticsPreset _selectedPreset = OpticsPreset.All[1];
    public string Summary => $"rank {Effective.MuraRank} · {Effective.CellPitchMm:0.##} mm · D {Effective.MaskDetectorDistanceMm:0.#} mm · {Effective.DetectorPixels}×{Effective.DetectorPixels} @ {Effective.PixelPitchMm:0.##} mm";
    partial void OnRankChanged(int value) => Validate();
    partial void OnCellPitchChanged(string value) => Validate();
    partial void OnDistanceChanged(string value) => Validate();
    partial void OnPixelsChanged(string value) => Validate();
    partial void OnPixelPitchChanged(string value) => Validate();
    partial void OnSelectedPresetChanged(OpticsPreset value)
    {
        if (_loading) return;
        if (!IsEditable) { Load(Effective); return; } // locked: the selector snaps back to the effective geometry
        if (value.Settings is { } settings) Load(settings with { FocalDistanceMm = Effective.FocalDistanceMm });
    }

    public void Load(OpticsSettings value)
    {
        _loading = true;
        Rank = value.MuraRank;
        CellPitch = value.CellPitchMm.ToString("G", CultureInfo.CurrentCulture);
        Distance = value.MaskDetectorDistanceMm.ToString("G", CultureInfo.CurrentCulture);
        Pixels = value.DetectorPixels.ToString(CultureInfo.CurrentCulture);
        PixelPitch = value.PixelPitchMm.ToString("G", CultureInfo.CurrentCulture);
        Effective = value;
        _loading = false;
        Validate(loading: true);
    }

    private void Validate(bool loading = false)
    {
        if (_loading) return;
        // Locked while the shell holds acquired data: any writer is reverted to the effective geometry.
        if (!IsEditable && !loading) { Load(Effective); return; }
        _loading = true;
        if (!double.TryParse(CellPitch, out double cell) || !double.TryParse(Distance, out double distance) ||
            !int.TryParse(Pixels, out int pixels) || !double.TryParse(PixelPitch, out double pitch))
            Error = "Enter numeric pitches/distance and an integer pixel count.";
        else
        {
            var candidate = Effective with { MuraRank = Rank, CellPitchMm = cell,
                MaskDetectorDistanceMm = distance, DetectorPixels = pixels, PixelPitchMm = pitch };
            // Pending gap is validated by the shell against this effective pitch. Do not embed a fixed gap
            // in the physical editor: a valid zero-gap detector can have a pitch below the old 100 µm default.
            Error = OpticsPolicy.Validate(candidate, reflectorGapMm: 0);
            if (Error is null) Effective = candidate;
        }
        SelectedPreset = Error is null ? OpticsPreset.Match(Effective) : OpticsPreset.All[0];
        _loading = false;
        OnPropertyChanged(nameof(Summary));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
