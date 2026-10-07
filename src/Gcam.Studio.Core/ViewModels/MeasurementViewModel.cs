using CommunityToolkit.Mvvm.ComponentModel;
using Gcam.Core;
using Gcam.Studio.Core.Imaging;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>
/// One measurement drawn on an image. Geometry is stored in mm, so it survives zoom, pan and re-runs;
/// ROI statistics are recomputed whenever the image under it changes (<see cref="Refresh"/>).
/// </summary>
public sealed partial class MeasurementViewModel : ObservableObject
{
    public MeasurementViewModel(int number, ImagePane pane, MeasurementKind kind, IReadOnlyList<Vec2> pointsMm)
    {
        int expected = kind == MeasurementKind.Angle ? 3 : 2;
        if (pointsMm.Count != expected)
            throw new ArgumentException($"{kind} needs {expected} points, got {pointsMm.Count}.", nameof(pointsMm));
        Number = number;
        Pane = pane;
        Kind = kind;
        PointsMm = pointsMm.ToArray();
    }

    public int Number { get; }
    public string Name => $"M{Number}";
    public ImagePane Pane { get; }
    public string PaneLabel => Pane == ImagePane.Flood ? "Flood" : "Recon";
    public MeasurementKind Kind { get; }
    public string KindLabel => Kind switch
    {
        MeasurementKind.Distance => "Distance",
        MeasurementKind.Angle => "Angle",
        _ => "ROI",
    };

    /// <summary>Points in the pane's mm frame: 2 for distance and ROI corners, 3 for an angle (vertex second).</summary>
    public IReadOnlyList<Vec2> PointsMm { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    private string _value = "—";

    [ObservableProperty] private string _detail = string.Empty;

    /// <summary>One sentence for screen readers and the overlay label: "M2 ROI on Flood: Σ 1,234".</summary>
    public string Description => $"{Name} {KindLabel} on {PaneLabel}: {Value}";

    /// <summary>Recompute the value against the image this measurement sits on (null = not simulated yet).</summary>
    public void Refresh(DetectorImage? image, double originMm, double stepMm, string? valueUnit = null)
    {
        var p = PointsMm;
        switch (Kind)
        {
            case MeasurementKind.Distance:
                Value = $"{MeasurementMath.Distance(p[0], p[1]):F1} mm";
                Detail = $"{Point(p[0])} → {Point(p[1])}";
                break;
            case MeasurementKind.Angle:
                Value = $"{MeasurementMath.AngleDeg(p[0], p[1], p[2]):F1}°";
                Detail = $"vertex {Point(p[1])}";
                break;
            default:
                string size = $"{Math.Abs(p[1].X - p[0].X):F1} × {Math.Abs(p[1].Y - p[0].Y):F1} mm";
                if (image is null)
                {
                    Value = "—";
                    Detail = $"{size} · no image yet";
                    break;
                }
                var s = MeasurementMath.Roi(image, p[0], p[1], originMm, stepMm);
                // No pixel centre inside (e.g. a new result moved the grid away): no value, not a plausible "Σ 0".
                string unit = valueUnit is null ? "" : $" {valueUnit}";
                Value = s.Pixels == 0 ? "—" : $"Σ {Amount(s.Sum)}{unit}";
                Detail = s.Pixels == 0
                    ? $"{size} · no pixel centres inside"
                    : $"{size} · {s.Pixels} px · mean {Amount(s.Mean)}{unit} · max {Amount(s.Max)}{unit}";
                break;
        }
    }

    private static string Point(Vec2 v) => $"({Coord(v.X)}, {Coord(v.Y)}) mm";

    // A tiny negative coordinate shouldn't read as "-0.0".
    private static string Coord(double v) => (Math.Round(v, 1) == 0 ? 0.0 : v).ToString("F1");

    // Counts read as integers once they are large; small or fractional values (reconstruction) keep 3 digits.
    private static string Amount(double v) => Math.Abs(v) >= 100 ? v.ToString("N0") : v.ToString("G3");
}
