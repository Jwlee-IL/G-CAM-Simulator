using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>Expected count-rate maps (counts per second per pixel) of the ambient field and of a point source, through the
/// same crystal transport and the same counting windows, so source and background counts are in one unit. Both use the
/// ambient bound's homogeneous crystal (<see cref="AmbientPhotonProcess"/>: no gaps, entrance or backing; the deposit's
/// largest site places the event). Poisson acquisitions of fixed live time are then exact draws from these means.</summary>
public static class GateResponse
{
    private const double FarHorizonS = 1e300;   // one incident history per AdvanceUntil call; time is irrelevant here

    /// <summary>Ambient rate per pixel per µSv/h of photon H*(10), one map per window, from
    /// <paramref name="histories"/> incident photons of the transported field (<see cref="AmbientPhotonProcess"/>, which
    /// insists on a validated spectrum here). The process is linear in the dose rate, so one map serves every field level.</summary>
    public static GateMaps Ambient(SimulationConfig scenario, AmbientGeometry bound, IncidentSpectrum spectrum, long histories,
        int seed, IReadOnlyList<CountingWindow> windows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(histories, 1);
        var config = scenario.Clone();
        config.Seed = seed;
        config.Ambient = new AmbientFieldConfig
        {
            DoseRateMicroSvPerHour = 1, Geometry = bound, Spectrum = spectrum, RequireValidatedSpectrum = true
        };
        var process = new AmbientPhotonProcess(config);
        var tally = new Tally(windows, config.Detector.PixelsX * config.Detector.PixelsY);
        for (long h = 0; h < histories; h++)
            if (process.AdvanceUntil(FarHorizonS) is { } e)
                tally.Add(e.PixelY * config.Detector.PixelsX + e.PixelX, e.DepositKeV, 1);
        return tally.ToMaps(process.IncidentRateCps / histories, histories);
    }

    /// <summary>Source rate per pixel per becquerel, one map per window: the scenario's single-line source with
    /// directional biasing (unbiased against 4π, EV-07), through the scenario's mask, into the same crystal as
    /// <see cref="Ambient"/> entering through the front face. Weight = photons per emitted photon; × branching ratio.</summary>
    public static GateMaps Source(SimulationConfig scenario, long photons, int seed, IReadOnlyList<CountingWindow> windows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(photons, 1);
        var config = scenario.Clone();
        config.Ambient = null; config.Background = null;
        if (config.Sources is { Length: > 0 } || config.Source.Lines is { Length: > 0 } || !config.Source.DirectionalBiasing)
            throw new NotSupportedException("Gate response maps take one single-line, directionally biased source.");
        var factory = new DefaultSimulationFactory();
        var source = factory.CreateSource(config);
        var mask = factory.CreateMask(config);
        var d = config.Detector;
        int pixels = d.PixelsX * d.PixelsY;
        var tally = new Tally(windows, pixels);
        (int Pixel, double Energy, double Weight)? hit = null;
        double incident = config.Source.EnergyKeV;
        var detector = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm, 1, double.MaxValue,
            ComptonStrategy.Argmax, DefaultRandom.FromKey(DefaultRandom.Key(seed, 52003)),
            d.CrystalAttenuationPerMm > 0 ? d.CrystalAttenuationPerMm : null, d.CrystalThicknessMm,
            material: CrystalMaterial.ForConfig(d.Material),
            pixelEventSink: (x, y, e, w) => hit = (y * d.PixelsX + x, Math.Min(e, incident), w));
        var emit = DefaultRandom.FromKey(DefaultRandom.Key(seed, 52001));
        var maskRandom = DefaultRandom.FromKey(DefaultRandom.Key(seed, 52002));
        foreach (var photon in source.Emit(emit, photons))
        {
            if (!mask.Transmit(photon.Ray, photon.EnergyKeV, maskRandom)) continue;
            hit = null;
            detector.Score(photon);
            if (hit is { } h) tally.Add(h.Pixel, h.Energy, h.Weight);
        }
        return tally.ToMaps(config.Source.BranchingRatio / photons, photons);
    }

    /// <summary>Derived independent int seed for one purpose of an outer seed (SplitMix-keyed, not seed + offset).</summary>
    public static int StreamSeed(int outerSeed, uint purpose)
        => (int)(DefaultRandom.FromKey(DefaultRandom.Key(outerSeed, purpose)).NextDouble() * int.MaxValue);

    private sealed class Tally(IReadOnlyList<CountingWindow> windows, int pixels)
    {
        private readonly double[][] _sum = windows.Select(_ => new double[pixels]).ToArray();
        private readonly double[] _sumSq = new double[windows.Count];
        private long _events;

        public void Add(int pixel, double energyKeV, double weight)
        {
            _events++;
            for (int w = 0; w < windows.Count; w++)
            {
                double a = weight * windows[w].Acceptance(energyKeV);
                _sum[w][pixel] += a; _sumSq[w] += a * a;
            }
        }

        public GateMaps ToMaps(double scale, long trials)
        {
            var rates = _sum.Select(m => m.Select(v => v * scale).ToArray()).ToArray();
            // Standard error of each window's total: per-trial scores are independent (zero for a miss).
            var se = _sum.Select((m, w) =>
            {
                double mean = m.Sum() / trials;
                return scale * Math.Sqrt(Math.Max(0, _sumSq[w] - trials * mean * mean) * trials / (trials - 1.0));
            }).ToArray();
            return new GateMaps(rates, se, trials, _events);
        }
    }
}
