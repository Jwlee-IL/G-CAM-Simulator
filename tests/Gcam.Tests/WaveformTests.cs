using Gcam.Detector;
using Xunit;

namespace Gcam.Tests;

/// <summary>The C# waveform port (Waveform.cs) must reproduce the validated Python RTL reference
/// (rtl/trap_ref.py + rtl/event_stream.py) bit-for-bit on the deterministic path. Golden values were
/// captured from the Python reference on a fixed pulse (exp_pulse(80, 20, 662 keV)); the integer shapers
/// use a signed arithmetic shift in both, so equality is exact, not approximate.</summary>
public class WaveformTests
{
    [Fact]
    public void Constants_MatchThePythonReference()
    {
        Assert.Equal(1156, Waveform.MQ8);           // round(256/(e^0.2-1))
        Assert.Equal(53656, Waveform.CrrcAQ16);     // round(e^-0.2 * 65536)
        Assert.Equal(26214, Waveform.CrrcKQ16);     // round(0.4 * 65536)
    }

    [Fact]
    public void ExpPulse_MatchesGolden()
    {
        var p = Waveform.ExpPulse(80, 20, 662.0);
        // amp = 662*4 = 2648 at n0, decaying by e^(-1/5) each sample.
        int[] gold = [2648, 2168, 1775, 1453, 1190, 974];
        for (int i = 0; i < gold.Length; i++)
            Assert.Equal(gold[i], p[20 + i]);
        for (int i = 0; i < 20; i++) Assert.Equal(0, p[i]);
    }

    [Fact]
    public void BiexpPulse_MatchesGolden()
    {
        var w = Waveform.BiexpPulse(80, 20, 662.0, Waveform.TauSamples, Waveform.TauRiseSamples, Waveform.AdcPerKevIdeal);
        int[] gold = [0, 1194, 1417, 1321, 1141, 956];     // finite rise: 0 at n0, peaks a couple samples later
        for (int i = 0; i < gold.Length; i++)
            Assert.Equal(gold[i], (int)System.Math.Round(w[20 + i]));
    }

    [Fact]
    public void TrapShape_MatchesGolden()
    {
        var shaped = Waveform.TrapShape(Waveform.ExpPulse(80, 20, 662.0));
        long max = long.MinValue;
        foreach (var v in shaped) if (v > max) max = v;
        Assert.Equal(146075L, max);                 // flat-top height ∝ energy
        Assert.Equal(131462L, shaped[38]);          // a golden checkpoint on the falling edge
    }

    [Fact]
    public void CrrcInt_MatchesGolden()
    {
        var shaped = Waveform.CrrcInt(Waveform.ExpPulse(80, 20, 662.0));
        long max = long.MinValue;
        foreach (var v in shaped) if (v > max) max = v;
        Assert.Equal(304L, max);                    // CR-RC^4 semi-Gaussian peak ∝ energy
        Assert.Equal(23L, shaped[35]);
    }

    [Fact]
    public void Blr_MatchesGolden()
    {
        // A trapezoid pulse on a 3000-count DC offset: the restorer tracks the offset while quiet and freezes
        // (|x − base| ≥ gate) under the pulse, so both branches of baseline_restorer.sv are exercised.
        var shaped = Waveform.TrapShape(Waveform.ExpPulse(400, 200, 662.0));
        var xs = new long[shaped.Length];
        for (int i = 0; i < xs.Length; i++) xs[i] = shaped[i] + 3000;
        var o = Waveform.Blr(xs);
        long max = long.MinValue, sum = 0;
        foreach (var v in o) { if (v > max) max = v; sum += v; }
        Assert.Equal(3000L, o[0]);                  // pre-update baseline: nothing subtracted yet
        Assert.Equal(2858L, o[199]);                // partly converged on the DC offset
        Assert.Equal(148932L, max);
        Assert.Equal(2861L, o[230]);                // baseline frozen under the pulse
        Assert.Equal(2708L, o[399]);
        Assert.Equal(3770843L, sum);                // checksum over every sample
    }

    [Fact]
    public void TrapFlatTop_IsProportionalToEnergy()
    {
        // Linear filter: doubling the deposited energy doubles the flat top (within integer rounding).
        long f400 = Max(Waveform.TrapShape(Waveform.ExpPulse(120, 20, 400.0)));
        long f800 = Max(Waveform.TrapShape(Waveform.ExpPulse(120, 20, 800.0)));
        Assert.True(System.Math.Abs((double)f800 / f400 - 2.0) < 0.02,
            $"trap flat top should be linear in energy: 400->{f400}, 800->{f800}");
    }

    [Fact]
    public void DeriveAdc_MatchesDatasheetMath()
    {
        var adc = Waveform.DefaultAdc;      // AD9648 14-bit, fullScale 2000 keV, ENOB 11.8
        Assert.Equal(8191, adc.AdcMax);                             // 2^13 - 1
        Assert.Equal(8191.0 / 2000.0, adc.AdcPerKev, 6);            // 4.0955
        Assert.Equal(1.327, adc.AdcNoiseCodes, 3);                 // from SNR = 6.02*11.8 + 1.76 dB
    }

    [Fact]
    public void Rasterize_RecoversEnergy_ViaFlatTopCalibration()
    {
        // One clean 662 keV event; recovered energy = flat_top / calibration should be ~662 keV.
        var adc = Waveform.DefaultAdc;
        int n0 = 60;
        var wave = Waveform.Rasterize([(n0, 662.0)], adc, intrinsicFwhm: 0.0, noiseKev: 0.0);
        var shaped = Waveform.TrapShape(wave);
        long flat = Waveform.FlatTop(shaped, n0);
        double perKev = Waveform.CalibrateFlatPerKev(adc);
        double recovered = flat / perKev;
        Assert.True(System.Math.Abs(recovered - 662.0) < 15.0,
            $"recovered energy {recovered:F1} keV should be near 662 (flat {flat}, perKev {perKev:F1})");
    }

    private static long Max(long[] xs)
    {
        long m = long.MinValue;
        foreach (var v in xs) if (v > m) m = v;
        return m;
    }
}
