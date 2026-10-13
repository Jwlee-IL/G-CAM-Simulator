using Gcam.Configuration;

namespace Gcam.Studio.Core.Services;

/// <summary>The Studio option defaults shared by initial values, fallbacks and the visible summary.</summary>
public sealed record StudioOptionDefaults(string SeedText, FrontEndChain Chain, double GapUm, double GainSigmaPercent,
    int GainSeed, double AmbientDoseMicroSvPerHour, AmbientGeometry AmbientGeometry, double BackgroundToSignalRatio,
    double EntranceAbsorberMm, double BackingScatterMm, double LiveTimeS, double Speed, double WindowFwhm)
{
    public static StudioOptionDefaults Current { get; } = new("", FrontEndParts.Default, 100, 3, 1, 0.10,
        AmbientGeometry.FrontOnlyThroughMask, 0, 0.15, 2, 60, 10, 1.5);
}
