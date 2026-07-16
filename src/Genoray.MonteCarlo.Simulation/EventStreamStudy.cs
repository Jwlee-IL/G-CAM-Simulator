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
        var entrance = d.EntranceAbsorberMm > 0.0 ? new EntranceAbsorber(d.EntranceAbsorberMm) : null;
        var backing = d.BackingScatterMm > 0.0 ? new EntranceAbsorber(d.BackingScatterMm) : null;
        var detector = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm,
            windowCenterKeV: 661.7, windowFraction: 1.0, ComptonStrategy.Argmax, cascadeRng,
            muAt662PerMm: 0.09, d.CrystalThicknessMm, planeZ: 0.0, sensitivity,
            eventSink: (dep, w) => { deposits.Add(dep); weights.Add(w); },
            entranceAbsorber: entrance, backingScatterer: backing, reflectorGapMm: d.ReflectorGapMm);

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

        // Ambient background: a SECOND, uncoded Poisson event process merged into the source train. Its
        // rate is BSR × the source rate (so the same background-to-signal knob drives imaging and the RTL),
        // its deposits come from the crystal response to the background energy, and optional DCR adds
        // sub-keV nuisance pulses. Off (null / zero) leaves the clean source-only stream unchanged.
        var bg = config.Background;
        bool wantBg = bg is not null &&
            (bg.BackgroundToSignalRatio > 0.0 || bg.DarkCountRateKcps is double dr && dr > 0.0);
        if (wantBg && events.Count > 0)
        {
            long span = events[^1].ArrivalSample;
            if (bg!.BackgroundToSignalRatio > 0.0)
            {
                double bgRateCps = bg.BackgroundToSignalRatio * countRateCps;
                double[] pool = GenerateBackgroundDeposits(config, bg.EnergyKeV, 4096);
                if (pool.Length > 0)
                {
                    var bgRng = new DefaultRandom((config.Seed ?? 0) + 909);
                    AddPoissonEvents(events, bgRateCps, adcSampleRateHz, span, bgRng,
                        r => pool[Math.Min(pool.Length - 1, (int)(r.NextDouble() * pool.Length))]);
                }
            }
            if (bg.DarkCountRateKcps is double dcr && dcr > 0.0)
            {
                var dcrRng = new DefaultRandom((config.Seed ?? 0) + 1717);
                AddPoissonEvents(events, dcr * 1e3, adcSampleRateHz, span, dcrRng, _ => SinglePeKeV);
            }
            events.Sort((a, b) => a.ArrivalSample.CompareTo(b.ArrivalSample));
        }
        return events;
    }

    // A dark-count nuisance pulse in keV-equivalent: a few photoelectrons, far below any photopeak window.
    // Kept a few keV (not literally sub-keV) so it clears the rasterizer's integer-ADC rounding floor and
    // actually appears as a low-amplitude pulse; its only effect on the shaper is occasional small pile-up.
    private const double SinglePeKeV = 3.0;

    /// <summary>Append events of a Poisson process at <paramref name="rateCps"/> over [0, span] samples, each
    /// with an energy from <paramref name="energyPicker"/>. Exponential inter-arrival gaps, mean fs/rate.</summary>
    private static void AddPoissonEvents(List<StreamEvent> into, double rateCps, double adcSampleRateHz,
        long span, IRandom rng, Func<IRandom, double> energyPicker)
    {
        if (!(rateCps > 0.0) || span <= 0) return;
        double meanGap = adcSampleRateHz / rateCps;
        double t = 0.0;
        while (true)
        {
            t += -Math.Log(1.0 - rng.NextDouble()) * meanGap;
            if (t > span) break;
            into.Add(new StreamEvent((long)Math.Round(t), energyPicker(rng)));
        }
    }

    /// <summary>A pool of crystal DEPOSITS for a background gamma of the given energy: transport background
    /// photons onto the (unmasked) detector and record each event's total Compton-cascade deposit.
    /// Background events are uncoded, so they illuminate the array uniformly; the deposit spectrum (photopeak
    /// + continuum around the background energy) is the real crystal response, sampled per event.
    /// <paramref name="isotropic"/> (the default) samples a COSINE-weighted downward hemisphere — the correct
    /// angular distribution for an isotropic flux crossing the top face. Oblique rays traverse a longer VERTICAL
    /// path but, in a finite-width array, also reach the SIDE walls sooner and escape — empirically the side
    /// escape wins slightly, so the isotropic photopeak fraction is a touch LOWER than the normal-incidence
    /// idealization (the 1/cosθ longer-path gain only dominates for an infinite lateral slab). Model: a diffuse
    /// field entering from the (least-shielded) mask side.</summary>
    private static double[] GenerateBackgroundDeposits(SimulationConfig config, double energyKeV, int count)
        => BackgroundDepositSpectrum(config, energyKeV, count, isotropic: true);

    /// <summary>The crystal deposit spectrum for a background gamma of the given energy — exposed so the
    /// isotropic (cosine-hemisphere) vs normal-incidence angular models can be compared. Returns up to
    /// <paramref name="count"/> per-event total deposits.</summary>
    public static double[] BackgroundDepositSpectrum(SimulationConfig config, double energyKeV, int count,
                                                     bool isotropic)
    {
        var d = config.Detector;
        var cascadeRng = new DefaultRandom((config.Seed ?? 0) + 555);
        var deposits = new List<double>(count);
        var det = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm,
            windowCenterKeV: 661.7, windowFraction: 1.0, ComptonStrategy.Argmax, cascadeRng,
            muAt662PerMm: 0.09, d.CrystalThicknessMm, planeZ: 0.0, sensitivity: null,
            eventSink: (dep, _) => deposits.Add(dep));

        double halfW = d.PixelsX * d.PixelPitchMm / 2.0, halfH = d.PixelsY * d.PixelPitchMm / 2.0;
        var entryRng = new DefaultRandom((config.Seed ?? 0) + 556);
        int tries = 0, maxTries = count * 50;
        while (deposits.Count < count && tries < maxTries)
        {
            tries++;
            double x = (entryRng.NextDouble() * 2.0 - 1.0) * halfW * 0.999;
            double y = (entryRng.NextDouble() * 2.0 - 1.0) * halfH * 0.999;

            Vector3 dir;
            if (isotropic)
            {
                // Cosine-weighted hemisphere about -z: pdf ∝ cosθ = |dz|, the flux-through-a-plane law.
                double u1 = entryRng.NextDouble(), u2 = entryRng.NextDouble();
                double r = Math.Sqrt(u1), phi = 2.0 * Math.PI * u2;
                dir = new Vector3(r * Math.Cos(phi), r * Math.Sin(phi), -Math.Sqrt(1.0 - u1));
            }
            else
            {
                dir = new Vector3(0.0, 0.0, -1.0);
            }

            // Place the origin above the top face so the ray crosses the sampled entry point (x, y, 0).
            double lead = 10.0 / -dir.Z;                 // travel distance to reach z = 0 from z = +10
            var origin = new Vector3(x - dir.X * lead, y - dir.Y * lead, 10.0);
            det.Score(new Photon { Ray = new Ray(origin, dir), EnergyKeV = energyKeV, Weight = 1.0 });
        }
        return deposits.ToArray();
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
