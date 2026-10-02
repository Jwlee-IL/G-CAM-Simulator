using CommunityToolkit.Mvvm.ComponentModel;
using Gcam.Configuration;
using Gcam.Studio.Core.Imaging;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Editable view of one <see cref="SceneSource"/>. Values are clamped to physically sensible ranges.</summary>
public sealed partial class SourceItemViewModel : ObservableObject, IPlaneMarker
{
    public static IReadOnlyList<string> IsotopeNames { get; } = Isotopes.All.Select(i => i.Name).ToArray();

    [ObservableProperty] private string _isotope = "Cs-137";
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _distanceMm = 1000;
    [ObservableProperty] private double _activityUCi = 500;
    /// <summary>False while the shell holds acquired data (physical inputs are locked); a change is then reverted.</summary>
    [ObservableProperty] private bool _isEditable = true;
    private bool _reverting;

    private bool Locked(Action revert)
    {
        if (_reverting) return true;
        if (IsEditable) return false;
        _reverting = true;
        try { revert(); }
        finally { _reverting = false; }
        return true;
    }

    partial void OnXChanged(double oldValue, double newValue) => Locked(() => X = oldValue);
    partial void OnYChanged(double oldValue, double newValue) => Locked(() => Y = oldValue);

    partial void OnDistanceMmChanged(double oldValue, double newValue)
    {
        if (Locked(() => DistanceMm = oldValue)) return;
        double clamped = Math.Clamp(newValue, 200, 3000);
        if (clamped != newValue) DistanceMm = clamped;
    }

    partial void OnActivityUCiChanged(double oldValue, double newValue)
    {
        if (Locked(() => ActivityUCi = oldValue)) return;
        if (newValue <= 0) ActivityUCi = 1;
    }

    partial void OnIsotopeChanged(string? oldValue, string newValue)
    {
        if (Locked(() => Isotope = oldValue!)) return;
        if (!IsotopeNames.Contains(newValue)) Isotope = IsotopeNames[0];
    }

    // Keep the list label in sync with whatever was edited.
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Isotope)) OnPropertyChanged(nameof(MarkerLabel));
        if (e.PropertyName is not (nameof(Label) or nameof(MarkerLabel) or nameof(IsEditable))) OnPropertyChanged(nameof(Label));
    }

    /// <summary>Text next to the source's marker on the reconstruction.</summary>
    public string MarkerLabel => Isotope;

    public string Label => $"{Isotope}  ({X:F0}, {Y:F0}) mm  ·  {DistanceMm:F0} mm  ·  {ActivityUCi:F0} µCi";

    public SceneSource ToModel() => new()
    {
        Isotope = Isotope,
        X = X,
        Y = Y,
        DistanceMm = DistanceMm,
        ActivityUCi = ActivityUCi,
    };
}
