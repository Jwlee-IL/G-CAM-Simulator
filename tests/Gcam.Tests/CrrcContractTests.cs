using System.Text.Json;
using Gcam.Configuration;
using Gcam.Detector;
using Xunit;

namespace Gcam.Tests;

public sealed class CrrcContractTests
{
    [Theory]
    [InlineData(0, 20972)]
    [InlineData(1, 10486)]
    [InlineData(2, 4194)]
    public void Preset_ShapingTimeIsSeparateFromNoiseWindow(int index, int expectedK)
    {
        var preset = FrontEndParts.Preamps[index];
        Assert.Equal(4, preset.CrrcOrder);
        Assert.Equal(expectedK, preset.CrrcKQ16());
        Assert.Equal(expectedK, (preset with { IntegrationNs = 12345 }).CrrcKQ16());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void PreciseState_RetainsLowEnergyPulseWithinDerivedArithmeticError(int index)
    {
        var chain = new FrontEndChain(FrontEndParts.Scintillators[0], FrontEndParts.Sensors[0], FrontEndParts.Preamps[index]);
        var pulse = chain.PulseSamples;
        int a = (int)Math.Round(Math.Exp(-1 / pulse.TailSamples) * 65536);
        int k = chain.CrrcKQ16;
        var samples = Waveform.BiexpPulse(1024, 100, 32, pulse.TailSamples, pulse.RiseSamples,
            Waveform.DefaultAdc.AdcPerKev).Select(x => (int)Math.Round(x)).ToArray();
        var precise = Waveform.CrrcInt(samples, a, k, 4, Waveform.CrrcFractionalBits);
        Assert.True(precise.Max() > 0);
        // Deconvolution floor contributes <1 raw unit; each RC update contributes <1 with
        // steady-state accumulation <=1/K. Unit-gain cascades sum these bounds, for all samples.
        double errorBound = (1 + 4 * 65536.0 / k) / Waveform.CrrcOutputScale;
        var acc = new double[4]; double prev = 0;
        for (int n = 0; n < samples.Length; n++)
        {
            double u = samples[n] - a / 65536.0 * prev; prev = samples[n];
            for (int i = 0; i < 4; i++) { acc[i] += (u - acc[i]) * k / 65536.0; u = acc[i]; }
            Assert.InRange(Math.Abs(precise[n] / (double)Waveform.CrrcOutputScale - u), 0, errorBound);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(12)]
    public void SignedFullScale_StaysInsideTheConvexStateBound(int fractionalBits)
    {
        int[] samples = Enumerable.Range(0, 10000).Select(n => n % 2 == 0 ? -32768 : 32767).ToArray();
        long bound = 2 * (1L << (15 + fractionalBits)) + 1;
        foreach (int k in new[] { 1, 4194, 26214, 65536 })
        {
            var shaped = Waveform.CrrcInt(samples, 65536, k, 16, fractionalBits);
            Assert.All(shaped, value => Assert.InRange(value, -bound, bound));
        }
        // Unity stages expose exact signed deconvolution; this detects sign extension before scaling.
        var exact = Waveform.CrrcInt([-32768, 32767, 0], 65536, 65536, 4, fractionalBits);
        Assert.Equal(new[] { -32768L << fractionalBits, 65535L << fractionalBits, -32767L << fractionalBits }, exact);
    }

    [Fact]
    public void ParametersOutsideTheBound_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Waveform.CrrcInt([32768], 0, 1, 4, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => Waveform.CrrcInt([0], 65537, 1, 4, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => Waveform.CrrcInt([0], 0, 0, 4, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => Waveform.CrrcInt([0], 0, 1, 4, 13));
    }

    [Fact]
    public void CsharpContractVectors_UseConfiguredCoefficientsAndOptionalExport()
    {
        string? directory = Environment.GetEnvironmentVariable("GCAM_CRRC_VECTORS");
        if (directory is not null) Directory.CreateDirectory(directory);
        Export("legacy", 53656, 26214, 4, 1, 5, directory);
        foreach (var (name, scintillator) in new[] { ("gagg", 0), ("nai", 1), ("lyso", 2), ("bgo", 4) })
        foreach (int index in Enumerable.Range(0, 3))
        {
            var chain = new FrontEndChain(FrontEndParts.Scintillators[scintillator], FrontEndParts.Sensors[0], FrontEndParts.Preamps[index]);
            var pulse = chain.PulseSamples;
            int a = (int)Math.Round(Math.Exp(-1 / pulse.TailSamples) * 65536);
            string preamp = new[] { "fast", "original", "slow" }[index];
            Export($"{preamp}-{name}", a, chain.CrrcKQ16, chain.Preamp.CrrcOrder, pulse.RiseSamples, pulse.TailSamples, directory);
        }
    }

    private static void Export(string name, int a, int k, int order, double rise, double tail, string? directory)
    {
        var samples = new List<int>();
        foreach (double energy in new[] { 32, 32.1, 122, 662 })
            samples.AddRange(Waveform.BiexpPulse(1024, 100, energy, tail, rise,
                Waveform.DefaultAdc.AdcPerKev).Select(x => (int)Math.Round(x)));
        foreach (int index in Enumerable.Range(0, 41))
            samples.AddRange(Waveform.BiexpPulse(256, 20, 27 + index * .25, tail, rise,
                Waveform.DefaultAdc.AdcPerKev).Select(x => (int)Math.Round(x)));
        samples.AddRange(Enumerable.Range(0, 256).Select(n => n % 2 == 0 ? -32768 : 32767));
        samples.AddRange(Enumerable.Repeat(32767, 256)); samples.AddRange(Enumerable.Repeat(-32768, 256));
        var rng = new Random(20261002);
        samples.AddRange(Enumerable.Range(0, 1024).Select(_ => rng.Next(-32768, 32768)));
        samples.AddRange(new int[1024]);
        foreach (int fractional in new[] { 0, 12 })
        {
            var output = Waveform.CrrcInt(samples.ToArray(), a, k, order, fractional);
            long bound = 2 * (1L << (15 + fractional)) + 1;
            Assert.All(output, value => Assert.InRange(value, -bound, bound));
            if (directory is not null)
                File.WriteAllText(Path.Combine(directory, $"{name}-f{fractional}.json"),
                    JsonSerializer.Serialize(new { A = a, K = k, Order = order, F = fractional, Samples = samples, Expected = output }));
        }
    }
}
