namespace Gcam.Studio.Core.Services;

/// <summary>An acquired event marker; absolute time retains its acquisition identity in rate study.</summary>
public sealed record WaveformEvent(int Index, int PixelX, int PixelY, double DepositKeV,
    double AcquisitionTimeS, double RelativeTimeUs, double AmplitudeKeV)
{
    public string Label => $"#{Index} ({PixelX},{PixelY}) {DepositKeV:0.#} keV";
    public string Description => $"{Label} · acquired {AcquisitionTimeS:0.000000000} s · scope {RelativeTimeUs:0.###} µs";
}
