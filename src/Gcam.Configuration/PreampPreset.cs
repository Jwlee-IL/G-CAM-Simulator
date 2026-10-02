namespace Gcam.Configuration;

/// <summary>A preamp + shaper: the effective integration time (noise window; also sets DCR-collected variance)
/// and the pulse decay tail the charge-sensitive preamp produces, plus which digital shaper the DAQ runs.</summary>
public sealed record PreampPreset(string Name, double IntegrationNs, double PulseTailNs, bool Crrc,
    double? CrrcShapingTimeNs = null, int CrrcOrder = 4)
{
    /// <summary>Euler coefficient for T_sum = order × nominal RC time. This is a simulation convention,
    /// not the peaking time, the DCR noise window, or evidence of the original rig's filter.</summary>
    public int CrrcKQ16(double sampleRateHz = FrontEndParts.AdcSampleRateHz)
    {
        if (!Crrc || CrrcShapingTimeNs is not { } time || !double.IsFinite(time) || time <= 0 ||
            CrrcOrder is < 1 or > 16 || !double.IsFinite(sampleRateHz) || sampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(CrrcShapingTimeNs));
        double coefficient = 65536 * CrrcOrder * 1e9 / sampleRateHz / time;
        if (coefficient < 1 || coefficient > 65536)
            throw new ArgumentOutOfRangeException(nameof(CrrcShapingTimeNs));
        return (int)Math.Round(coefficient);
    }
}
