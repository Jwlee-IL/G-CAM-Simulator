using System.IO;
using System.Text.Json;
using Gcam.Core;

namespace Gcam.Detector;

/// <summary>An ADC preset derived from datasheet specs (mirror of rtl/event_stream.load_adc): the signed
/// full-scale code from the bit depth, the codes-per-keV gain (full scale mapped to <see cref="FullScaleKev"/>),
/// and the ADC's own input-referred noise in codes from its ENOB (SNR = 6.02·ENOB + 1.76 dB).</summary>
public sealed record AdcPreset(
    string Name, int Bits, int AdcMax, double AdcPerKev, double AdcNoiseCodes, double FullScaleKev,
    double? SampleRateMsps);

/// <summary>C# port of the validated RTL waveform reference (rtl/trap_ref.py + rtl/event_stream.py): build the
/// ADC sample waveform from an MC event stream and shape it with the trapezoidal or CR-RC^n filter. The integer
/// shapers are bit-exact to the SystemVerilog — a signed arithmetic right shift in C# (<c>&gt;&gt;</c> on a
/// signed <see cref="long"/>) floors like the SV <c>&gt;&gt;&gt;</c> and like Python's <c>&gt;&gt;</c>, so these
/// curves ARE the RTL behaviour. This makes the waveform pipeline (previously Python-only) native to C# for the
/// WPF viewer; cross-checked against Python golden values in WaveformTests.</summary>
public static class Waveform
{
    // Operating point (mirror trap_ref.py). tau, tau_rise in SAMPLES.
    public const double TauSamples = 5.0;
    public const int Rise = 10, Flat = 8;
    /// <summary>pole-zero deconvolution constant M = 1/(e^(1/tau)-1) in Q8 → 1156.</summary>
    public static readonly int MQ8 = (int)Math.Round(256.0 / (Math.Exp(1.0 / TauSamples) - 1.0));
    public const double AdcPerKevIdeal = 4.0;   // the pure shaper unit-test gain (realistic path uses an AdcPreset)

    // Realistic front-end parameters.
    public const double TauRiseSamples = 1.0;   // finite rise, Nyquist-bounded (see theme 30)
    public const double NoiseKev = 3.0;         // analog/preamp white-noise floor (keV-equivalent RMS)
    public const int AdcMaxIdeal = 32767;       // 16-bit signed full scale for the ideal-path shaper input
    public const double IntrinsicFwhm = 0.06;   // scintillator+SiPM photostatistics FWHM at IntrinsicRefKev
    public const double IntrinsicRefKev = 662.0;

    // CR-RC^n shaper (Q16). A = e^(-1/tau) deconvolves the exp tail (pole-zero); K = 1/tau_s low-pass gain.
    public const int CrrcOrder = 4;
    public const int CrrcFractionalBits = 12;
    public const int CrrcOutputScale = 1 << CrrcFractionalBits;
    public static readonly int CrrcAQ16 = (int)Math.Round(Math.Exp(-1.0 / TauSamples) * 65536);   // 53656
    public static readonly int CrrcKQ16 = (int)Math.Round((1.0 / 2.5) * 65536);                    // 26214

    /// <summary>The module-default ADC (Analog Devices AD9648, 14-bit / 125 MSPS).</summary>
    public static readonly AdcPreset DefaultAdc = DeriveAdc("AD9648 (14-bit, 125 MSPS)", 14, 2000.0, 11.8, 125.0);

    // ---- ADC presets -------------------------------------------------------------------------------------

    /// <summary>Derive the model quantities from datasheet specs (pure math; the testable part of load_adc).</summary>
    public static AdcPreset DeriveAdc(string name, int bits, double fullScaleKev, double enob,
                                      double? sampleRateMsps = null)
    {
        int adcMax = (1 << (bits - 1)) - 1;                 // signed full scale, e.g. 14-bit → 8191
        double adcPerKev = adcMax / fullScaleKev;
        double snrDb = 6.02 * enob + 1.76;
        double adcNoiseCodes = (adcMax / Math.Sqrt(2.0)) / Math.Pow(10.0, snrDb / 20.0);
        return new AdcPreset(name, bits, adcMax, adcPerKev, adcNoiseCodes, fullScaleKev, sampleRateMsps);
    }

    /// <summary>Load an ADC preset from a samples/adc/&lt;name&gt;.json datasheet file.</summary>
    public static AdcPreset LoadAdc(string jsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var r = doc.RootElement;
        return DeriveAdc(
            r.GetProperty("name").GetString() ?? Path.GetFileNameWithoutExtension(jsonPath),
            r.GetProperty("bits").GetInt32(),
            r.GetProperty("fullScaleKeV").GetDouble(),
            r.GetProperty("enob").GetDouble(),
            r.TryGetProperty("sampleRateMsps", out var sr) ? sr.GetDouble() : null);
    }

    // ---- pulse stimulus ----------------------------------------------------------------------------------

    /// <summary>One scintillation pulse: instantaneous rise at n0, exponential decay tau (samples). The pure
    /// shaper-correctness stimulus (matches trap_ref.exp_pulse; the realistic front-end uses BiexpPulse).</summary>
    public static int[] ExpPulse(int n, int n0, double ampKev, double tau = TauSamples)
    {
        var w = new int[n];
        double a = ampKev * AdcPerKevIdeal;
        for (int t = n0; t < n; t++)
            w[t] = (int)Math.Round(a * Math.Exp(-(t - n0) / tau));
        return w;
    }

    /// <summary>One scintillation + front-end pulse with a FINITE rise: a·(e^(-t/tau) − e^(-t/tau_rise)). The
    /// tail is a·e^(-t/tau) so the shaper's pole-zero and energy calibration are unchanged; the rise introduces
    /// a small ballistic deficit in the flat top. Returns floats (the caller adds noise, clips, quantizes).</summary>
    public static double[] BiexpPulse(int n, int n0, double ampKev, double tau, double tauRise, double adcPerKev)
    {
        var w = new double[n];
        double a = ampKev * adcPerKev;
        for (int t = n0; t < n; t++)
        {
            double dt = t - n0;
            w[t] = a * (Math.Exp(-dt / tau) - Math.Exp(-dt / tauRise));
        }
        return w;
    }

    // ---- shapers (bit-exact to the SystemVerilog) --------------------------------------------------------

    public static long[] TrapShape(int[] samples) => TrapShape(samples, Rise, Flat, MQ8);

    /// <summary>s[n] for the recursive trapezoidal filter (matches trapezoidal_shaper.sv). The <c>&gt;&gt;8</c>
    /// is a signed arithmetic shift, exactly the SV <c>&gt;&gt;&gt;</c>.</summary>
    public static long[] TrapShape(int[] samples, int rise, int flat, int mQ8)
    {
        int L = rise + flat, KL = rise + (rise + flat);
        var dl = new long[KL];
        long p = 0, s = 0;
        var outp = new long[samples.Length];
        for (int idx = 0; idx < samples.Length; idx++)
        {
            long v = samples[idx];
            long dkl = v - dl[rise - 1] - dl[L - 1] + dl[KL - 1];
            p += dkl;
            long r = p + ((dkl * mQ8) >> 8);            // arithmetic shift, as SV >>>
            s += r;
            outp[idx] = s;
            for (int i = KL - 1; i > 0; i--) dl[i] = dl[i - 1];
            dl[0] = v;
        }
        return outp;
    }

    public static long[] CrrcInt(int[] samples) => CrrcInt(samples, CrrcAQ16, CrrcKQ16, CrrcOrder);

    /// <summary>Integer CR-RC^order, bit-exact to crrc_shaper.sv: deconvolve the exp tail (imp = x − A·x[-1])
    /// then <paramref name="order"/> single-pole RC low-passes (acc += (u−acc)·K, all Q16), each stage feeding
    /// the next this sample. Returns signed codes × 2^fractionalBits; retain that scale through plotting.
    /// F=0 preserves the legacy golden path. The bounded contract is signed-16 input, A in [0,65536],
    /// K in [1,65536], order in [1,16], F in [0,12]. For B=2^(15+F), |imp| ≤ 2B+1;
    /// floor of a convex update keeps every stage in that interval. Thus |u-acc| ≤ 4B+2 and
    /// |(u-acc)*K| ≤ (4B+2)*65536 &lt; 2^46. Signed 48-bit RTL products and C# long cannot overflow.</summary>
    public static long[] CrrcInt(int[] samples, int aQ16, int kQ16, int order, int fractionalBits = 0)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (aQ16 is < 0 or > 65536 || kQ16 is < 1 or > 65536 || order is < 1 or > 16 ||
            fractionalBits is < 0 or > CrrcFractionalBits)
            throw new ArgumentOutOfRangeException(nameof(fractionalBits));
        long prev = 0;
        var acc = new long[order];
        var outp = new long[samples.Length];
        for (int idx = 0; idx < samples.Length; idx++)
        {
            if (samples[idx] is < short.MinValue or > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(samples), "CR-RC input must fit signed 16 bits.");
            long x = (long)samples[idx] << fractionalBits;
            long imp = x - ((aQ16 * prev) >> 16);
            prev = x;
            long u = imp;
            for (int i = 0; i < order; i++)
            {
                acc[i] += ((u - acc[i]) * kQ16) >> 16;
                u = acc[i];
            }
            outp[idx] = u;
        }
        return outp;
    }

    /// <summary>Integer reference for baseline_restorer.sv (bit-exact): a gated leaky integrator tracks the DC
    /// baseline in quiet regions (|x−base| &lt; gate) and subtracts it, cancelling the shaper's pole-zero walk.
    /// The estimate is carried in Q(frac) so the small per-sample step does not round away.</summary>
    public static long[] Blr(long[] xs, long gate = 4096, int frac = 12)
    {
        long baseAcc = 0;
        var outp = new long[xs.Length];
        for (int i = 0; i < xs.Length; i++)
        {
            long baseline = baseAcc >> frac;
            long diff = xs[i] - baseline;
            outp[i] = xs[i] - baseline;                 // registered output uses the PRE-update baseline
            if (-gate < diff && diff < gate) baseAcc += diff;
        }
        return outp;
    }

    // ---- realistic rasterizer + energy recovery ----------------------------------------------------------

    /// <summary>Build the realistic ADC waveform for an event stream: per-event intrinsic amplitude smear
    /// (1/√E photostatistics), a finite-rise bi-exponential pulse (ballistic deficit), white analog noise in
    /// quadrature with the ADC's own input-referred noise, and clip+quantize to the ADC full scale (pile-up
    /// stacks saturate). Set intrinsicFwhm=0, noiseKev=0, tauRise=0 for the old ideal shape. <paramref
    /// name="seed"/> fixes the amplitude and sample-noise RNG streams (C# RNG, so noise differs from the Python
    /// reference run-to-run — the deterministic pulse/shaper path is what the golden test pins).</summary>
    public static int[] Rasterize(IReadOnlyList<(long Arrival, double EnergyKeV)> events, AdcPreset adc,
        double tau = TauSamples, double tauRise = TauRiseSamples, double noiseKev = NoiseKev,
        double intrinsicFwhm = IntrinsicFwhm, double intrinsicRefKev = IntrinsicRefKev,
        int seed = 1, int tailPad = 128)
    {
        if (events.Count == 0) return [];
        double adcPerKev = adc.AdcPerKev;
        int adcMax = adc.AdcMax;
        long maxArrival = 0;
        foreach (var e in events) if (e.Arrival > maxArrival) maxArrival = e.Arrival;
        int length = (int)maxArrival + tailPad;
        var wave = new double[length];

        var ampRng = new DefaultRandom(seed + 777);         // per-event amplitude (intrinsic) — its own stream
        const double inv2355 = 1.0 / 2.3548;
        foreach (var (n0, eKev) in events)
        {
            double eEff = eKev;
            if (intrinsicFwhm > 0.0 && eKev > 0.0)
            {
                double relSigma = intrinsicFwhm * inv2355 * Math.Sqrt(intrinsicRefKev / eKev);
                eEff = eKev * (1.0 + Sampling.Gaussian(ampRng) * relSigma);
                if (eEff < 0.0) eEff = 0.0;
            }
            double a = eEff * adcPerKev;
            int support = (int)Math.Ceiling(tau * Math.Log(2.0 * Math.Abs(a) + 2.0)) + 4;
            long end = Math.Min(length, n0 + support);
            for (long t = n0; t < end; t++)
            {
                double dt = t - n0;
                double fall = Math.Exp(-dt / tau);
                double rise = tauRise > 0.0 ? Math.Exp(-dt / tauRise) : 0.0;
                wave[t] += a * (fall - rise);
            }
        }

        double sigma = Math.Sqrt(noiseKev * adcPerKev * (noiseKev * adcPerKev) + adc.AdcNoiseCodes * adc.AdcNoiseCodes);
        var noiseRng = new DefaultRandom(seed);
        var outp = new int[length];
        for (int i = 0; i < length; i++)
        {
            double v = wave[i] + (sigma > 0.0 ? Sampling.Gaussian(noiseRng) * sigma : 0.0);
            if (v > adcMax) v = adcMax; else if (v < -adcMax) v = -adcMax;
            outp[i] = (int)Math.Round(v);
        }
        return outp;
    }

    /// <summary>Flat-top height the trapezoid produces per keV, from ONE isolated noiseless reference pulse
    /// (recovered energy = flat_top / this). Uses the SAME finite-rise pulse model and ADC gain as the
    /// rasterizer, so the gain already folds in the ballistic deficit; noise is excluded (deterministic gain).</summary>
    public static double CalibrateFlatPerKev(AdcPreset adc, int rise = Rise, int flat = Flat, int mQ8 = -1,
        double tau = TauSamples, double tauRise = TauRiseSamples, double refKev = 662.0)
    {
        if (mQ8 < 0) mQ8 = MQ8;
        int n0 = 4 * rise;
        int length = n0 + rise + flat + 8 * (int)Math.Ceiling(tau) + 8;
        var bi = BiexpPulse(length, n0, refKev, tau, tauRise, adc.AdcPerKev);
        var pulse = new int[length];
        for (int i = 0; i < length; i++) pulse[i] = (int)Math.Round(bi[i]);
        var shaped = TrapShape(pulse, rise, flat, mQ8);
        long mx = long.MinValue;
        foreach (var v in shaped) if (v > mx) mx = v;
        return mx / refKev;
    }

    /// <summary>Flat-top height of the pulse that arrived at n0, RELATIVE to the local pre-pulse baseline —
    /// peak over [n0+rise, n0+rise+flat] minus the level just before the ramp. The relative read is what real
    /// trapezoidal DAQs do (baseline restoration): the Q8 pole-zero residual walks the absolute baseline over a
    /// long train, so subtracting the local baseline isolates the genuine pile-up distortion from the slow walk.</summary>
    public static long FlatTop(long[] shaped, int n0, int rise = Rise, int flat = Flat, int latency = 0, int guard = 4)
    {
        int lo = Math.Max(0, n0 + rise + latency - guard);
        int hi = Math.Min(shaped.Length, n0 + rise + flat + latency + guard + 1);
        long peak = 0; bool any = false;
        for (int i = lo; i < hi; i++) { if (!any || shaped[i] > peak) { peak = shaped[i]; any = true; } }
        int bi = n0 + latency - 1;
        long baseline = bi >= 0 && bi < shaped.Length ? shaped[bi] : 0;
        return (any ? peak : 0) - baseline;
    }
}
