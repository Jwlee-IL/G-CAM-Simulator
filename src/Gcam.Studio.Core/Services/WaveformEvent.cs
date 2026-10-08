namespace Gcam.Studio.Core.Services;

/// <summary>An acquired event marker; absolute time retains its acquisition identity in rate study.</summary>
public sealed record WaveformEvent(int Index, int PixelX, int PixelY, double DepositKeV,
    double AcquisitionTimeS, double RelativeTimeUs, double AmplitudeKeV)
{
    public bool IsMeasured { get; init; }
    public string Label => IsMeasured ? PixelX < 0 ? $"#{Index} LUT unknown · energy unavailable" : $"#{Index} LUT ({PixelX},{PixelY}) {AmplitudeKeV:0.#} measured keV"
        : $"#{Index} ({PixelX},{PixelY}) {DepositKeV:0.#} keV";
    public string Description => $"{Label} · acquired {Gcam.Studio.Core.Imaging.NumberFormat.GroupedFraction(AcquisitionTimeS, 9)} s · scope {RelativeTimeUs:0.###} µs";
}
