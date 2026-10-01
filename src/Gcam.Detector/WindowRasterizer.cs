using Gcam.Core;

namespace Gcam.Detector;

/// <summary>Bounded, cancellable ADC stimulus with an explicit length and negative prehistory arrivals.
/// Amplitudes are supplied by the caller; no second intrinsic response is applied.</summary>
public static class WindowRasterizer
{
    public static int[] Rasterize(IReadOnlyList<(long Arrival, double EnergyKeV)> events, int length,
        AdcPreset adc, double tailSamples, double riseSamples, double noiseKeV = Waveform.NoiseKev,
        int seed = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(adc);
        if (length is < 1 or > 10_000_000 || !double.IsFinite(tailSamples) || tailSamples <= 0 ||
            !double.IsFinite(riseSamples) || riseSamples < 0 || !double.IsFinite(noiseKeV) || noiseKeV < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        var wave = new double[length];
        foreach (var (arrival, energy) in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!double.IsFinite(energy) || energy < 0) throw new ArgumentOutOfRangeException(nameof(events));
            double amplitude = energy * adc.AdcPerKev;
            double support = Math.Ceiling(tailSamples * Math.Log(2 * Math.Abs(amplitude) + 2)) + 4;
            if (!double.IsFinite(support)) throw new ArgumentOutOfRangeException(nameof(events));
            long end = (long)Math.Min(length, arrival + support);
            for (long t = Math.Max(0, arrival); t < end; t++)
            {
                if ((t & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                double dt = t - arrival;
                wave[t] += amplitude * (Math.Exp(-dt / tailSamples) -
                    (riseSamples > 0 ? Math.Exp(-dt / riseSamples) : 0));
            }
        }
        var output = new int[length];
        double sigma = Math.Sqrt(Math.Pow(noiseKeV * adc.AdcPerKev, 2) + Math.Pow(adc.AdcNoiseCodes, 2));
        var rng = new DefaultRandom(seed);
        for (int i = 0; i < length; i++)
        {
            if ((i & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
            double v = wave[i] + (sigma > 0 ? sigma * Sampling.Gaussian(rng) : 0);
            output[i] = (int)Math.Round(Math.Clamp(v, -adc.AdcMax, adc.AdcMax));
        }
        return output;
    }
}
