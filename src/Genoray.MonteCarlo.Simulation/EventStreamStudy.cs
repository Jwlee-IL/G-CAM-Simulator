using System.Text;
using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Detector;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One detected event in the MC→RTL stream: when it arrives (in ADC samples) and how much
/// energy it deposited in the crystal (keV). The deposit is the analog pulse height the trapezoidal
/// shaper works on; the arrival sample carries the pile-up statistics.</summary>
public readonly record struct StreamEvent(long ArrivalSample, double EnergyKeV);

/// <summary>
/// Bridges the validated C# Monte Carlo to the RTL front-end: runs the coded-aperture pipeline through
/// the crystal-Compton detector, taps the TOTAL deposited energy of every scored event (the real
/// spectrum — photopeak, Compton continuum, escape, and, for a mixed field, several isotope lines),
/// and overlays a Poisson arrival process at a chosen operating count rate. The resulting
/// (arrivalSample, energyKeV) list is rasterized into an ADC waveform by the cocotb testbench, which
/// then drives <c>trapezoidal_shaper.sv</c> directly — closing the loop from MC physics to gate-level RTL.
///
/// The ENERGIES are MC physics; the RATE is an independent operating-point knob (exactly how the RTL
/// rate studies treat count rate — the MC's PhotonCount is a variance-reduction budget, not real counts).
/// </summary>
public sealed class EventStreamStudy
{
    private readonly ISimulationFactory _base;

    public EventStreamStudy(ISimulationFactory? baseFactory = null)
        => _base = baseFactory ?? new DefaultSimulationFactory();

    /// <param name="countRateCps">operating count rate (detected events per second) for the Poisson overlay.</param>
    /// <param name="adcSampleRateHz">ADC sample rate; sets samples-per-second for the arrival times.</param>
    /// <param name="maxEvents">cap on collected events (bounds the cocotb waveform length / sim time).</param>
    public IReadOnlyList<StreamEvent> Generate(SimulationConfig config, double countRateCps,
        double adcSampleRateHz, int maxEvents)
    {
        if (!(countRateCps > 0.0)) throw new ArgumentException("countRateCps must be > 0", nameof(countRateCps));
        if (!(adcSampleRateHz > 0.0)) throw new ArgumentException("adcSampleRateHz must be > 0", nameof(adcSampleRateHz));
        if (maxEvents < 1) throw new ArgumentException("maxEvents must be >= 1", nameof(maxEvents));

        var rng = _base.CreateRandom(config);
        var source = _base.CreateSource(config);
        var mask = _base.CreateMask(config);

        // Build the Compton detector directly (not via ComptonFactory) so we can tap the per-event
        // deposit sink. Window params are irrelevant here — the sink fires for EVERY scored event,
        // independent of any energy window (we want the full analog spectrum for the shaper).
        var d = config.Detector;
        bool nonUniform = d.GainSigma != 0.0 || d.EnergyResolutionFwhmSigma != 0.0 || d.GainGradient != 0.0;
        double[]? sensitivity = nonUniform ? new CrystalUniformity(d).Sensitivity : null;
        var cascadeRng = new DefaultRandom(config.Seed + 777);

        var deposits = new List<double>(Math.Min(maxEvents, 4096));
        var weights = new List<double>(Math.Min(maxEvents, 4096));
        var detector = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm,
            windowCenterKeV: 661.7, windowFraction: 1.0, ComptonStrategy.Argmax, cascadeRng,
            muAt662PerMm: 0.09, d.CrystalThicknessMm, planeZ: 0.0, sensitivity,
            eventSink: (dep, w) => { deposits.Add(dep); weights.Add(w); });

        foreach (var photon in source.Emit(rng, config.PhotonCount))
        {
            if (!mask.Transmit(photon.Ray, photon.EnergyKeV, rng)) continue;
            detector.Score(photon);
            if (deposits.Count >= maxEvents) break;
        }

        var timeRng = new DefaultRandom(config.Seed + 4242);

        // Directional biasing makes each detected photon carry an importance weight, so the raw
        // deposit list is a sample of the BIASED proposal, not the physical detected-event spectrum.
        // Resample it (weighted) back to the physical distribution before rasterizing — otherwise the
        // pulse-height spectrum is skewed wherever deposit/escape probability varies with entry angle.
        // (Analog runs have all weights = 1, so this is the identity up to RNG.)
        double[] physical = ResampleByWeight(deposits, weights, timeRng);

        // Poisson arrival process: exponential inter-arrival gaps with mean (fs / rate) samples.
        double meanGapSamples = adcSampleRateHz / countRateCps;
        var events = new List<StreamEvent>(physical.Length);
        double t = 0.0;
        foreach (double e in physical)
        {
            // -ln(1-U) is a unit-mean exponential; scale by the mean gap.
            t += -Math.Log(1.0 - timeRng.NextDouble()) * meanGapSamples;
            events.Add(new StreamEvent((long)Math.Round(t), e));
        }
        return events;
    }

    /// <summary>Systematic (low-variance) resampling of the deposits with probability ∝ weight, then a
    /// shuffle so identical energies don't land in artificial consecutive clusters (arrival times are an
    /// independent overlay, so order carries no physics — but adjacency would fake same-energy pile-up).
    /// Returns a physical-spectrum sample the same size as the input.</summary>
    private static double[] ResampleByWeight(List<double> deposits, List<double> weights, IRandom rng)
    {
        int n = deposits.Count;
        var outp = new double[n];
        if (n == 0) return outp;

        double total = 0.0;
        foreach (double w in weights) total += double.IsFinite(w) && w > 0.0 ? w : 0.0;
        if (!(total > 0.0))   // no usable weights (shouldn't happen) — fall back to the raw deposits
        {
            deposits.CopyTo(outp);
            return outp;
        }

        // One random offset, N equally spaced pointers walking the cumulative-weight line.
        double step = total / n;
        double pointer = rng.NextDouble() * step;
        double cum = weights[0] > 0.0 && double.IsFinite(weights[0]) ? weights[0] : 0.0;
        int j = 0;
        for (int i = 0; i < n; i++)
        {
            double target = pointer + i * step;
            while (target > cum && j < n - 1)
            {
                j++;
                cum += double.IsFinite(weights[j]) && weights[j] > 0.0 ? weights[j] : 0.0;
            }
            outp[i] = deposits[j];
        }

        // Fisher–Yates shuffle.
        for (int i = n - 1; i > 0; i--)
        {
            int k = (int)(rng.NextDouble() * (i + 1));
            if (k > i) k = i;
            (outp[i], outp[k]) = (outp[k], outp[i]);
        }
        return outp;
    }

    /// <summary>Serialize the stream for the cocotb rasterizer: a header (so the Python side knows the
    /// operating point) then one "arrivalSample energyKeV" line per event.</summary>
    public static string ToText(IReadOnlyList<StreamEvent> events, double countRateCps,
        double adcSampleRateHz, string scenario)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# GCAM MC event stream  scenario={scenario}");
        sb.AppendLine($"# count_rate_cps={countRateCps:G6} adc_sample_rate_hz={adcSampleRateHz:G6} events={events.Count}");
        sb.AppendLine("# columns: arrival_sample energy_keV");
        foreach (var ev in events)
            sb.AppendLine($"{ev.ArrivalSample} {ev.EnergyKeV:F3}");
        return sb.ToString();
    }

    /// <summary>Summary stats for the CLI banner (event count, deposited-energy span, mean gap).</summary>
    public static (int Count, double MinKeV, double MaxKeV, double MeanGap) Summary(
        IReadOnlyList<StreamEvent> events)
    {
        if (events.Count == 0) return (0, 0, 0, 0);
        double min = double.MaxValue, max = double.MinValue;
        foreach (var ev in events)
        {
            if (ev.EnergyKeV < min) min = ev.EnergyKeV;
            if (ev.EnergyKeV > max) max = ev.EnergyKeV;
        }
        double meanGap = events.Count > 1
            ? (double)(events[^1].ArrivalSample - events[0].ArrivalSample) / (events.Count - 1)
            : 0.0;
        return (events.Count, min, max, meanGap);
    }
}
