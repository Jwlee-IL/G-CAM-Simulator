using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Core.Plotting;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Services;

/// <summary>Array-wide energy-channel ADC simulation. Analytic MCA energies are applied exactly once.</summary>
public sealed class WaveformService : IWaveformService
{
    private readonly SemaphoreSlim _gate = new(1);

    public async Task<WaveformView> ProcessAsync(AcquisitionSnapshot snapshot, WaveformSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(settings);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(() => Process(snapshot, settings, cancellationToken), cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private static WaveformView Process(AcquisitionSnapshot snapshot, WaveformSettings settings, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        if (!double.IsFinite(settings.RateKcps) || settings.RateKcps <= 0 ||
            !double.IsFinite(snapshot.LiveTimeS) || snapshot.LiveTimeS < 0 ||
            (snapshot.Events.Count > 0 && (settings.TriggerIndex < 0 || settings.TriggerIndex >= snapshot.Events.Count)))
            throw new ArgumentOutOfRangeException(nameof(settings));
        var chain = snapshot.Chain;
        _ = FrontEndMaterials.Material(chain.Scintillator);
        var pulse = chain.PulseSamples;
        var measurement = new MeasurementStage(snapshot.Detector, snapshot.Imaging.Flood.Width, snapshot.Imaging.Flood.Height);
        var times = new double[snapshot.Events.Count];
        var amplitudes = new double[snapshot.Events.Count];
        var rng = new DefaultRandom(settings.Seed);
        double maxAmplitude = 0, t = 0;
        for (int i = 0; i < times.Length; i++)
        {
            if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
            var ev = snapshot.Events[i];
            if (!double.IsFinite(ev.ArrivalTimeS) || ev.ArrivalTimeS < 0 || ev.ArrivalTimeS > snapshot.LiveTimeS ||
                (i > 0 && ev.ArrivalTimeS < snapshot.Events[i - 1].ArrivalTimeS) ||
                !double.IsFinite(ev.DepositKeV) || ev.DepositKeV < 0)
                throw new ArgumentException("Scope events must be an ordered acquired prefix.", nameof(snapshot));
            // Index-addressed response agrees with isolated Spectrum/Imaging, including fixed pixel gain.
            amplitudes[i] = settings.Ideal ? measurement.Amplitude(ev) : measurement.Measure(ev, i);
            maxAmplitude = Math.Max(maxAmplitude, amplitudes[i]);
            t = settings.RateStudy ? t - Math.Log(1 - rng.NextDouble()) / (settings.RateKcps * 1000) : ev.ArrivalTimeS;
            times[i] = t;
        }
        double support = Math.Ceiling(pulse.TailSamples * Math.Log(2 * maxAmplitude * Waveform.DefaultAdc.AdcPerKev + 2)) + 4;
        if (!double.IsFinite(support) || support > PlotSeries.MaximumSamples / 2)
            throw new ArgumentException("Pulse support exceeds the scope work budget.");
        // Include pulse support, tail history and eight times the configured sum of RC times.
        double filterHistory = chain.Preamp.Crrc
            ? 8 * chain.Preamp.CrrcOrder * 65536.0 / chain.CrrcKQ16 : 128;
        int warmup = checked((int)support + (int)Math.Ceiling(8 * pulse.TailSamples + filterHistory));
        var window = ScopeWindow.Create(settings.WindowUs, warmup);
        double triggerTime = times.Length > 0 ? times[settings.TriggerIndex] : snapshot.LiveTimeS;
        double displayOrigin = triggerTime - window.PretriggerUs * 1e-6;
        double workOrigin = displayOrigin - warmup / FrontEndParts.AdcSampleRateHz;
        double endTime = displayOrigin + window.Samples / FrontEndParts.AdcSampleRateHz;
        var stimulus = new List<(long Arrival, double EnergyKeV)>();
        var markers = new List<WaveformEvent>();
        int start = LowerBound(times, workOrigin);
        int end = LowerBound(times, endTime);
        for (int i = start; i < end; i++)
        {
            token.ThrowIfCancellationRequested();
            var ev = snapshot.Events[i];
            double amplitude = amplitudes[i];
            long sample = ScopeWindow.RelativeSample(times[i], workOrigin);
            stimulus.Add((sample, amplitude));
            if (times[i] >= displayOrigin)
                markers.Add(new(i, ev.PixelX, ev.PixelY, ev.DepositKeV, ev.ArrivalTimeS,
                    (times[i] - triggerTime) * 1e6, amplitude));
        }
        var adc = settings.Ideal ? Waveform.DefaultAdc with { AdcNoiseCodes = 0 } : Waveform.DefaultAdc;
        int[] raster = WindowRasterizer.Rasterize(stimulus, window.Samples + warmup, adc,
            pulse.TailSamples, settings.Ideal ? 0 : pulse.RiseSamples,
            settings.Ideal ? 0 : Waveform.NoiseKev, settings.Seed, token);
        token.ThrowIfCancellationRequested();
        int a = (int)Math.Round(Math.Exp(-1 / pulse.TailSamples) * 65536);
        int m = (int)Math.Round(256 / (Math.Exp(1 / pulse.TailSamples) - 1));
        // Preserve the established integer recurrences. Cancellation is checked around the bounded shaper call.
        long[] shaped = chain.Preamp.Crrc
            ? Waveform.CrrcInt(raster, a, chain.CrrcKQ16, chain.Preamp.CrrcOrder, Waveform.CrrcFractionalBits)
            : Waveform.TrapShape(raster, Waveform.Rise, Waveform.Flat, m);
        token.ThrowIfCancellationRequested();
        string pulseReadout = "CR-RC energy unavailable: trigger, phase and overlap estimator not validated.";
        if (!chain.Preamp.Crrc)
        {
            int n0 = checked((int)ScopeWindow.RelativeSample(triggerTime, workOrigin));
            int horizon = Waveform.Rise + Waveform.Flat + 4;
            double observedEnd = settings.RateStudy ? double.PositiveInfinity : snapshot.LiveTimeS;
            bool isolated = times.Length > 0 && n0 + horizon < raster.Length &&
                triggerTime + horizon / FrontEndParts.AdcSampleRateHz <= observedEnd &&
                (settings.TriggerIndex == 0 || triggerTime - times[settings.TriggerIndex - 1] > warmup / FrontEndParts.AdcSampleRateHz) &&
                (settings.TriggerIndex == times.Length - 1 || times[settings.TriggerIndex + 1] - triggerTime > warmup / FrontEndParts.AdcSampleRateHz);
            bool clipped = raster.Skip(Math.Max(0, n0)).Take(horizon + 1).Any(x => Math.Abs(x) == adc.AdcMax);
            if (isolated && !clipped)
            {
                // The legacy bi-exponential calibration divides by tauRise; zero-rise stimulus needs
                // its own noiseless reference, with the same integer filter and ADC gain.
                double gain;
                if (settings.Ideal)
                {
                    int length = 40 + Waveform.Rise + Waveform.Flat + 8 * (int)Math.Ceiling(pulse.TailSamples) + 8;
                    var reference = WindowRasterizer.Rasterize(new[] { (40L, 662.0) }, length,
                        adc with { AdcNoiseCodes = 0 }, pulse.TailSamples, 0, 0, cancellationToken: token);
                    gain = Waveform.TrapShape(reference, Waveform.Rise, Waveform.Flat, m).Max() / 662.0;
                }
                else gain = Waveform.CalibrateFlatPerKev(adc, mQ8: m, tau: pulse.TailSamples, tauRise: pulse.RiseSamples);
                pulseReadout = $"Trapezoid local flat-top {Waveform.FlatTop(shaped, n0) / gain:0.##} keV; deposit {snapshot.Events[settings.TriggerIndex].DepositKeV:0.##} keV. ADC estimate, not MCA.";
            }
            else pulseReadout = "Trapezoid energy unavailable: overlap, saturation, empty or partial window.";
        }
        var ys = new double[window.Samples];
        var zs = new double[window.Samples];
        for (int i = 0; i < ys.Length; i++)
        {
            if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
            ys[i] = raster[warmup + i];
            zs[i] = shaped[warmup + i] / (chain.Preamp.Crrc ? (double)Waveform.CrrcOutputScale : 1);
        }
        var adcSeries = new PlotSeries("ADC energy sum", ys, Origin: -window.PretriggerUs, Step: 1 / ScopeWindow.SamplesPerUs)
        { PreparedPyramid = new MinMaxPyramid(ys) };
        token.ThrowIfCancellationRequested();
        var shapedSeries = new PlotSeries(chain.Preamp.Crrc ? "CR-RC shaped codes (Q12 state)" : "Integer shaped output", zs, Origin: -window.PretriggerUs, Step: 1 / ScopeWindow.SamplesPerUs)
        { PreparedPyramid = new MinMaxPyramid(zs) };
        token.ThrowIfCancellationRequested();
        var model = new FrontEndModel(chain.BuildConfig());
        string filter = chain.Preamp.Crrc
            ? $"CR-RC order {chain.Preamp.CrrcOrder} · Q12 state / Q16 coefficients · A={a} · K={chain.CrrcKQ16}\nShaping T_sum {chain.Preamp.CrrcShapingTimeNs:0} ns (simulation convention; not peaking time or rig evidence) · DCR noise window {chain.Preamp.IntegrationNs:0} ns"
            : $"Trapezoid ramp 80 ns / flat 64 ns · M={m}";
        string readout = $"{chain}\nN_pe(662) {model.Photoelectrons(662):0} · FWHM {model.FwhmFraction(662):P2} (single channel; excludes pixel gain spread)\nRise / tail constants {pulse.RiseSamples * 8:0} / {pulse.TailSamples * 8:0} ns\n{filter}\nEffective resolving interval {EventStreamStudy.ResolvingSamples(pulse.RiseSamples, pulse.TailSamples) * 8:0} ns";
        string mode = settings.RateStudy ? $"Rate study: arrivals re-spaced at {settings.RateKcps:0.###} kcps — not the measured rate." : "Real acquired arrival times.";
        string note = $"{mode} Summed energy channel; four position channels are not modelled. " +
            (settings.Ideal ? "Ideal shaper stimulus: no response smear, noise or rise. " : "ADC simulation: shaped heights are not the analytic MCA spectrum. ") +
            "Finite filter warm-up; uncorrected shaped baseline. " +
            (window.Clipped ? "Window clipped to the 10 M-sample work cap. " : "") +
            (!settings.RateStudy && endTime > snapshot.LiveTimeS ? "Partial acquisition window: no future deposits; pulse response and baseline beyond acquired live time are simulated." : "");
        return new(adcSeries, shapedSeries, markers.AsReadOnly(), readout, pulseReadout, note,
            window.Samples / ScopeWindow.SamplesPerUs, watch.Elapsed);
    }

    private static int LowerBound(double[] times, double time)
    {
        int lo = 0, hi = times.Length;
        while (lo < hi) { int mid = lo + (hi - lo) / 2; if (times[mid] < time) lo = mid + 1; else hi = mid; }
        return lo;
    }
}
