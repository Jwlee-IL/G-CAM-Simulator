using Gcam.Configuration;

namespace Gcam.Studio.Core.Optics;

/// <summary>Historical engineering geometries; performance depends on scene and focus.</summary>
public sealed record OpticsPreset(string Name, OpticsSettings? Settings)
{
    public override string ToString() => Name;

    public static IReadOnlyList<OpticsPreset> All { get; } = Array.AsReadOnly(new[]
    {
        new OpticsPreset("Custom", null),
        new OpticsPreset("Sharp", new()),
        new OpticsPreset("Baseline", new() { MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60, DetectorPixels = 12, PixelPitchMm = 1 }),
        new OpticsPreset("Wide FOV", new() { MuraRank = 17, CellPitchMm = 1, MaskDetectorDistanceMm = 50, DetectorPixels = 34, PixelPitchMm = 0.75 }),
        new OpticsPreset("High-res", new() { MuraRank = 13, CellPitchMm = 0.5, MaskDetectorDistanceMm = 100, DetectorPixels = 44, PixelPitchMm = 0.4 })
    });

    public static OpticsPreset Match(OpticsSettings value) => All.FirstOrDefault(p => p.Settings is { } s &&
        s with { FocalDistanceMm = value.FocalDistanceMm } == value) ?? All[0];
}
