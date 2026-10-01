namespace Gcam.Studio.Core.Plotting;

/// <summary>Scope allocation policy at the fixed 125 MSPS ADC, including filter warm-up.</summary>
public sealed record ScopeWindow(int Samples, int WarmupSamples, double PretriggerUs, bool Clipped)
{
    public const double SamplesPerUs = 125;
    public const double PretriggerFraction = 0.2;

    public static ScopeWindow Create(double windowUs, int warmupSamples)
    {
        if (!double.IsFinite(windowUs) || windowUs <= 0 || warmupSamples < 0 || warmupSamples >= PlotSeries.MaximumSamples)
            throw new ArgumentOutOfRangeException(nameof(windowUs));
        double requested = Math.Ceiling(windowUs * SamplesPerUs);
        int count = (int)Math.Clamp(requested, 1, PlotSeries.MaximumSamples - warmupSamples);
        return new(count, warmupSamples, count / SamplesPerUs * PretriggerFraction, requested > count);
    }

    public static long RelativeSample(double timeS, double originS)
    {
        double samples = (timeS - originS) * SamplesPerUs * 1e6;
        if (!double.IsFinite(samples) || samples < long.MinValue || samples >= long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeS));
        return checked((long)Math.Round(samples));
    }
}
