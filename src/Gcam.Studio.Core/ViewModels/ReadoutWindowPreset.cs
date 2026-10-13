namespace Gcam.Studio.Core.ViewModels;

/// <summary>One energy-window preset of the Readout panel: ±<see cref="Fraction"/> around the window's calibration line.
/// Only the benchmark width carries a measured localisation cost (EV-36).</summary>
public sealed record ReadoutWindowPreset(double Fraction, bool IsBenchmark, bool IsActive, string AutomationId)
{
    public string Label => $"±{Fraction * 100:0} %";
    public string AccessibleName => IsBenchmark ? $"Window {Label} (EV-36 benchmark)" : $"Window {Label}";
    public string ToolTip => IsBenchmark
        ? $"{Label} around the window line: the EV-36 benchmark window (measured localisation cost)"
        : $"{Label} around the window line (no measured localisation cost)";
}
