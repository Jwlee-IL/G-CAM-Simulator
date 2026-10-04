using System.Diagnostics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

public sealed class DetectorRealismTests(ITestOutputHelper output)
{
    private static readonly SpectrumLine[] CsLines = Isotopes.Get("Cs-137").Lines
        .Select(l => new SpectrumLine("Cs-137", l.EnergyKeV)).ToArray();

    private DetectedEvent[] Acquire(SimulationConfig config, int histories = 2_000_000)
    {
        using var source = new ListModeSource(config);
        var events = new List<DetectedEvent>();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < histories; i++)
            if (source.Advance() is { } ev) events.Add(ev);
        watch.Stop();
        output.WriteLine($"histories={histories}, events={events.Count}, throughput={events.Count / watch.Elapsed.TotalSeconds:F0} events/s, rate={source.RateCps:F4} cps");
        return events.ToArray();
    }

    private static SimulationConfig Config(DetectorSettings detector)
        => SimulationService.BuildConfig([new SceneSource()], new OpticsSettings(), detector);

    private static double Band(SpectrumView view, double lo, double hi)
        => view.Counts.Where((_, i) => view.CentresKeV[i] >= lo && view.CentresKeV[i] <= hi).Sum();

    private static double Peak(SpectrumView view, double lo, double hi)
        => view.Counts.Where((_, i) => view.CentresKeV[i] >= lo && view.CentresKeV[i] <= hi).Max();

    private static (double Weight, double Bound) WeightedBand(SimulationConfig original, DetectorSettings settings,
        double lo, double hi, double binWidth)
    {
        var config = original.Clone(); config.Seed = 987; config.PhotonCount = 1_000_000;
        var d = config.Detector;
        var factory = new DefaultSimulationFactory();
        var rng = factory.CreateRandom(config);
        var mask = factory.CreateMask(config);
        var measurement = new MeasurementStage(settings, d.PixelsX, d.PixelsY, seed: 444);
        double weight = 0; int index = 0;
        var detector = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm,
            1, double.MaxValue, ComptonStrategy.Argmax, new DefaultRandom(config.Seed + 777),
            d.CrystalAttenuationPerMm > 0 ? d.CrystalAttenuationPerMm : null, d.CrystalThicknessMm,
            entranceAbsorber: d.EntranceAbsorberMm > 0 ? new EntranceAbsorber(d.EntranceAbsorberMm) : null,
            backingScatterer: d.BackingScatterMm > 0 ? new EntranceAbsorber(d.BackingScatterMm) : null,
            reflectorGapMm: d.ReflectorGapMm, material: CrystalMaterial.ForConfig(d.Material),
            pixelEventSink: (x, y, energy, w) =>
            {
                double measured = measurement.Measure(new DetectedEvent(x, y, energy, 0), index++);
                double centre = (Math.Floor(measured / binWidth) + 0.5) * binWidth;
                if (centre >= lo && centre <= hi) weight += w;
            });
        foreach (var photon in factory.CreateSource(config).Emit(rng, config.PhotonCount))
            if (mask.Transmit(photon.Ray, photon.EnergyKeV, rng)) detector.Score(photon);
        double bound = d.PixelsX * d.PixelsY * d.PixelPitchMm * d.PixelPitchMm / (4 * Math.PI * 1000 * 1000);
        return (weight, bound);
    }

    [Fact]
    public void StudioDefaults_AreExplicit_AndSceneBuilderRemainsBare()
    {
        var bare = SceneConfigBuilder.Build([new SceneSource()], new OpticsSettings(), 1);
        var studio = Config(new());
        Assert.Equal(0, bare.Detector.EntranceAbsorberMm);
        Assert.Equal(0, bare.Detector.BackingScatterMm);
        Assert.Equal(0, bare.Detector.GainSigma);
        Assert.Equal(0, bare.Detector.ReflectorGapMm);
        Assert.Equal(0.15, studio.Detector.EntranceAbsorberMm);
        Assert.Equal(2, studio.Detector.BackingScatterMm);
        Assert.Equal(0.03, studio.Detector.GainSigma);
        Assert.Equal(1, studio.Detector.UniformitySeed);
        Assert.Equal(0.1, studio.Detector.ReflectorGapMm);
        Assert.Throws<ArgumentOutOfRangeException>(() => Config(new() { GainSigma = double.NaN }));
    }

    [Fact]
    public async Task BaKAbsorber_UncollidedBandMatchesIndependentNarrowBeam_ReportsMeasuredSpectrum()
    {
        var detector = new DetectorSettings();
        var with = Config(detector);
        var without = with.Clone();
        without.Detector.EntranceAbsorberMm = 0;
        var bare = Acquire(without);
        var absorbed = Acquire(with);
        // Independent Beer-Lambert calculation from the iron table's 30/40 keV anchors.
        // A narrow beam counts only unchanged line energies; a broad measured band also accepts
        // photons scattered by the entrance. It must not be compared to uncollided transmission.
        double Transmission(double energy)
        {
            double slope = Math.Log(49.2 / 110.9) / Math.Log(40.0 / 30);
            double mu = 0.0580 * 110.9 * Math.Pow(energy / 30, slope);
            return Math.Exp(-mu * 0.15);
        }
        double expectedCount = 0, referenceVariance = 0;
        foreach (double energy in new[] { 32.1, 36.4 })
        {
            int b = bare.Count(e => Math.Abs(e.DepositKeV - energy) < 1e-6);
            double transmission = Transmission(energy);
            expectedCount += b * transmission;
            referenceVariance += b * transmission * transmission;
            output.WriteLine($"narrow-beam T({energy})={transmission:F6}, bare line counts={b}");
        }
        int before = bare.Count(e => Math.Abs(e.DepositKeV - 32.1) < 1e-6 || Math.Abs(e.DepositKeV - 36.4) < 1e-6);
        int after = absorbed.Count(e => Math.Abs(e.DepositKeV - 32.1) < 1e-6 || Math.Abs(e.DepositKeV - 36.4) < 1e-6);
        // Independent Poisson-count variance including the measured bare reference; shared seed
        // coupling is ignored conservatively. Four standard errors, fixed before the MC run.
        double tolerance = 4 * Math.Sqrt(expectedCount + referenceVariance);
        output.WriteLine($"Ba K uncollided band ratio={after / (double)before:F6}, narrow-beam={expectedCount / before:F6}, 4σ={tolerance / before:F6}; counts {before} → {after}");
        Assert.InRange(Math.Abs(after - expectedCount), 0, tolerance);
        var settings = new SpectrumSettings { Detector = detector, PixelsX = with.Detector.PixelsX, PixelsY = with.Detector.PixelsY };
        var service = new SpectrumService();
        var v0 = await service.ProcessAsync(Guid.NewGuid(), bare, CsLines, settings);
        var v1 = await service.ProcessAsync(Guid.NewGuid(), absorbed, CsLines, settings);
        foreach (var (name, view) in new[] { ("without absorber", v0), ("with absorber", v1) })
            output.WriteLine($"{name}: Ba K/662 peak-bin={Peak(view, 20, 50) / Peak(view, 620, 710):F6}; global maximum at {view.CentresKeV[Array.IndexOf(view.Counts, view.Counts.Max())]:F4} keV; Ba band={Band(view, view.Bands[0].LoKeV, view.Bands[0].HiKeV)}, valley 480–620={Band(view, 480, 620)}, backscatter 170–210={Band(view, 170, 210)}");
        Assert.True(Band(v1, v1.Bands[0].LoKeV, v1.Bands[0].HiKeV) < Band(v0, v0.Bands[0].LoKeV, v0.Bands[0].HiKeV));
        double binWidth = v1.CentresKeV[1] - v1.CentresKeV[0];
        var ref0 = WeightedBand(without, detector, v1.Bands[0].LoKeV, v1.Bands[0].HiKeV, binWidth);
        var ref1 = WeightedBand(with, detector, v1.Bands[0].LoKeV, v1.Bands[0].HiKeV, binWidth);
        double b0 = Band(v0, v0.Bands[0].LoKeV, v0.Bands[0].HiKeV), b1 = Band(v1, v1.Bands[0].LoKeV, v1.Bands[0].HiKeV);
        double ratio = b1 / b0, reference = ref1.Weight / ref0.Weight;
        // w² ≤ w_max*w bounds independent weighted-reference variance. All four
        // Poisson-count variances contribute to the ratio's delta-method uncertainty.
        double bandTolerance = 4 * reference * Math.Sqrt(1 / b0 + 1 / b1 + ref0.Bound / ref0.Weight + ref1.Bound / ref1.Weight);
        output.WriteLine($"Measured Ba band ratio={ratio:F6}, independent weighted absorber MC={reference:F6}, 4σ={bandTolerance:F6}; narrow-beam {expectedCount / before:F6} is the uncollided component only");
        Assert.InRange(Math.Abs(ratio - reference), 0, bandTolerance);
    }

    [Fact]
    public async Task Backing_AddsBackscatterRegion_FromTransport()
    {
        var detector = new DetectorSettings();
        var with = Config(detector);
        var without = with.Clone(); without.Detector.BackingScatterMm = 0;
        var bare = Acquire(without);
        var backed = Acquire(with);
        var settings = new SpectrumSettings { Detector = detector, PixelsX = with.Detector.PixelsX, PixelsY = with.Detector.PixelsY };
        var service = new SpectrumService();
        var v0 = await service.ProcessAsync(Guid.NewGuid(), bare, CsLines, settings);
        var v1 = await service.ProcessAsync(Guid.NewGuid(), backed, CsLines, settings);
        double before = Band(v0, 170, 210), after = Band(v1, 170, 210);
        // Compton's 180-degree return energy is E/(1+2E/m_ec²). Finite-angle return
        // broadens the region toward higher energy; the crystal already has continuum here.
        double expectedEnergy = 661.7 / (1 + 2 * 661.7 / 510.999);
        double tolerance = 4 * Math.Sqrt(before + after);
        output.WriteLine($"Backscatter kinematic edge={expectedEnergy:F4} keV; 170–210 counts {before} → {after}; excess={after - before}, 4σ={tolerance:F4}");
        Assert.True(after - before > tolerance);
    }

    [Fact]
    public void Gain_WidensPhotopeakInQuadrature_AndReducesTightWindowAcceptance()
    {
        var config = Config(new());
        var full = Acquire(config, 1_000_000).Where(e => Math.Abs(e.DepositKeV - 661.7) < 1e-6).ToArray();
        var uniform = new MeasurementStage(new() { GainSigma = 0 }, config.Detector.PixelsX, config.Detector.PixelsY);
        var varying = new MeasurementStage(new(), config.Detector.PixelsX, config.Detector.PixelsY);
        var model = new FrontEndModel(FrontEndParts.Default.BuildConfig());
        var zero = full.Select((e, i) => uniform.Measure(e, i)).ToArray();
        var gain = full.Select((e, i) => varying.Measure(e, i)).ToArray();
        var amplitudes = full.Select(varying.Amplitude).ToArray();
        double mean = amplitudes.Average();
        double patternVariance = amplitudes.Select(a => Math.Pow(a - mean, 2)).Average();
        double expectedVariance = patternVariance + amplitudes.Select(a => Math.Pow(a * model.FwhmFraction(a) / 2.3548, 2)).Average();
        double observedMean = gain.Average();
        double variance = gain.Sum(e => Math.Pow(e - observedMean, 2)) / (gain.Length - 1);
        double fourth = gain.Select(e => Math.Pow(e - observedMean, 4)).Average();
        // Variance estimator SE from the fourth central moment of this finite gain mixture.
        double tolerance = 4 * Math.Sqrt((fourth - variance * variance) / gain.Length);
        output.WriteLine($"Gain N={gain.Length}: Gaussian-equivalent FWHM={2.3548 * Math.Sqrt(variance):F4}, quadrature={2.3548 * Math.Sqrt(expectedVariance):F4} keV; variance={variance:F4}, expected={expectedVariance:F4}, 4σ={tolerance:F4} keV²; pattern σ={Math.Sqrt(patternVariance) / 661.7:P4}");
        Assert.InRange(Math.Abs(variance - expectedVariance), 0, tolerance);
        const double width = SpectrumService.BinWidthKeV;
        var histogram = new double[SpectrumService.BinCount];
        // Studio's fixed 0–2000 keV axis (AB-16); a smear beyond it would be overflow and cannot affect the photopeak
        // FWHM — skip it instead of indexing past the array.
        foreach (double energy in gain)
        {
            int bin = (int)(energy / width);
            if (bin < histogram.Length) histogram[bin]++;
        }
        int peak = Array.IndexOf(histogram, histogram.Max());
        double half = histogram[peak] / 2;
        int left = peak, right = peak;
        while (left > 0 && histogram[left] >= half) left--;
        while (right + 1 < histogram.Length && histogram[right] >= half) right++;
        double lo = (left + 0.5) * width + width * (half - histogram[left]) / (histogram[left + 1] - histogram[left]);
        double hi = (right - 0.5) * width + width * (histogram[right - 1] - half) / (histogram[right - 1] - histogram[right]);
        double expectedFwhm = 2.3548 * Math.Sqrt(expectedVariance);
        double fwhmTolerance = 2 * width + 5 * expectedFwhm / Math.Sqrt(2 * (gain.Length - 1));
        output.WriteLine($"Histogram FWHM={hi - lo:F4}, quadrature={expectedFwhm:F4} keV, tolerance=2 bins+5σ={fwhmTolerance:F4} keV");
        Assert.InRange(Math.Abs(hi - lo - expectedFwhm), 0, fwhmTolerance);
        double window = 0.5 * 661.7 * model.FwhmFraction(661.7);
        int n0 = zero.Count(e => Math.Abs(e - 661.7) <= window), n1 = gain.Count(e => Math.Abs(e - 661.7) <= window);
        // Independent-binomial bound is conservative for these paired measurements.
        double p0 = n0 / (double)gain.Length, p1 = n1 / (double)gain.Length;
        double acceptanceTolerance = 4 * Math.Sqrt((p0 * (1 - p0) + p1 * (1 - p1)) / gain.Length);
        output.WriteLine($"±0.5 FWHM window: σ0={p0:F6}, σ3%={p1:F6}, decline={p0 - p1:F6}, 4σ={acceptanceTolerance:F6}");
        Assert.True(p0 - p1 > acceptanceTolerance);
        double zeroMean = zero.Average();
        Assert.True(variance > zero.Select(e => Math.Pow(e - zeroMean, 2)).Average());
    }

    [Fact]
    public void Throughput_RecordsBareAndRealisticAcquisition()
    {
        var bare = SceneConfigBuilder.Build([new SceneSource()], new OpticsSettings(), 1);
        var realistic = Config(new());
        foreach (var (name, config) in new[] { ("bare", bare), ("realism defaults", realistic) })
        {
            var times = new List<double>();
            for (int run = 0; run < 4; run++)
            {
                using var source = new ListModeSource(config);
                int count = 0;
                while (count < 2000) if (source.Advance() is not null) count++;
                var watch = Stopwatch.StartNew();
                while (count < 102000) if (source.Advance() is not null) count++;
                watch.Stop();
                times.Add(100000 / watch.Elapsed.TotalSeconds);
            }
            output.WriteLine($"{name}: warmed accepted events/s [{string.Join(", ", times.Select(t => t.ToString("F0")))}], mean={times.Average():F0}; transport+rejection, excludes measurement and decode");
        }
    }

    [Fact]
    public async Task Measurement_UsesFrozenInputs_AndReplaysAcrossSnapshotsAndPileUp()
    {
        var config = Config(new());
        var events = Acquire(config, 50_000);
        var settings = new SpectrumSettings { Detector = new(), PixelsX = config.Detector.PixelsX, PixelsY = config.Detector.PixelsY };
        var service = new SpectrumService(); var id = Guid.NewGuid();
        await service.ProcessAsync(id, events.Take(1000).ToArray(), CsLines, settings);
        var incremental = await service.ProcessAsync(id, events, CsLines, settings);
        var batch = await new SpectrumService().ProcessAsync(Guid.NewGuid(), events, CsLines, settings);
        Assert.Equal(batch.Counts, incremental.Counts);
        await service.ProcessAsync(id, events, CsLines, settings with { PileUp = true });
        Assert.Equal(batch.Counts, (await service.ProcessAsync(id, events, CsLines, settings)).Counts);
        var stage = new MeasurementStage(settings.Detector, settings.PixelsX, settings.PixelsY);
        var expected = new double[SpectrumService.BinCount];
        double width = batch.CentresKeV[1] - batch.CentresKeV[0];
        for (int i = 0; i < events.Length; i++)
        {
            int bin = (int)(stage.Measure(events[i], i) / width);
            if (bin < expected.Length) expected[bin]++;
        }
        Assert.Equal(expected, batch.Counts);
    }
}
