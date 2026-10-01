using CommunityToolkit.Mvvm.ComponentModel;
using Gcam.Configuration;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>Editable view of one <see cref="SceneSource"/>. Values are clamped to physically sensible ranges.</summary>
public sealed partial class SourceItemViewModel : ObservableObject
{
    public static IReadOnlyList<string> IsotopeNames { get; } = Isotopes.All.Select(i => i.Name).ToArray();

    [ObservableProperty] private string _isotope = "Cs-137";
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _distanceMm = 1000;
    [ObservableProperty] private double _activityUCi = 500;

    partial void OnDistanceMmChanged(double value)
    {
        double clamped = Math.Clamp(value, 200, 3000);
        if (clamped != value) DistanceMm = clamped;
    }

    partial void OnActivityUCiChanged(double value)
    {
        if (value <= 0) ActivityUCi = 1;
    }

    partial void OnIsotopeChanged(string value)
    {
        if (!IsotopeNames.Contains(value)) Isotope = IsotopeNames[0];
    }

    // Keep the list label in sync with whatever was edited.
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(Label)) OnPropertyChanged(nameof(Label));
    }

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
