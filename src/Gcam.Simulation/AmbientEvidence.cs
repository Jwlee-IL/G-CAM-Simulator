using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Simulation;

/// <summary>Shared machinery of the AB-13 evidence families under the absolute ambient field (TODO-30 turn 8): the
/// validated spectrum, the counting windows, response maps (source per becquerel, ambient per µSv/h, both through the
/// ambient bound's homogeneous crystal — <see cref="GateResponse"/>), exact Poisson acquisitions and error tallies.
/// An acquisition of live time t is a flood map of exact Poisson counts per pixel with mean
/// activity × t × source map + H*(10) × t × ambient map (the AB-1 flood route, as the turn-7 gate study).</summary>
public static class AmbientEvidence
{
    public static string Repo(AmbientEvidenceRequest q, string relative) => Path.GetFullPath(Path.Combine(q.RepoRoot, relative));

    public static IncidentSpectrum LoadSpectrum(AmbientEvidenceRequest q)
        => IncidentSpectrumFile.Load(Repo(q, q.Spectrum.File), q.Spectrum.Sha256);

    public static CountingWindow[] Windows(AmbientEvidenceRequest q, SimulationConfig config)
        => q.Windows.Select(w => new CountingWindow(w.Name, w.LowKeV, w.HighKeV, config.Detector.EnergyResolutionFwhm)).ToArray();

    /// <summary>Source rate per pixel per becquerel of a multi-line emitter: one directionally biased map per line (its
    /// energy and per-decay intensity), summed; each line has its own derived stream. Standard errors add in quadrature.</summary>
    public static GateMaps SourceLines(SimulationConfig scenario, IReadOnlyList<AmbientEvidenceRequest.SourceLine> lines,
        long photons, int outerSeed, uint purpose, IReadOnlyList<CountingWindow> windows)
    {
        if (lines.Count == 0) throw new ArgumentException("A source needs at least one line.");
        GateMaps? sum = null;
        for (int l = 0; l < lines.Count; l++)
        {
            var c = scenario.Clone();
            c.Source.Lines = null;
            c.Source.EnergyKeV = lines[l].EnergyKeV;
            c.Source.BranchingRatio = lines[l].Intensity;
            var map = GateResponse.Source(c, photons, GateResponse.StreamSeed(outerSeed, purpose + (uint)l), windows);
            sum = sum is null ? map : new GateMaps(
                sum.RatePerPixel.Select((m, w) => m.Zip(map.RatePerPixel[w], (a, b) => a + b).ToArray()).ToArray(),
                sum.TotalRateStandardError.Zip(map.TotalRateStandardError, (a, b) => Math.Sqrt(a * a + b * b)).ToArray(),
                sum.Trials + map.Trials, sum.Events + map.Events);
        }
        return sum!;
    }

    /// <summary>Exact Poisson counts per pixel (<see cref="Sampling.PoissonExact"/>); returns the total.</summary>
    public static int Draw(IRandom rng, ReadOnlySpan<double> lambda, Span<int> counts)
    {
        int total = 0;
        for (int i = 0; i < lambda.Length; i++) total += counts[i] = Sampling.PoissonExact(rng, lambda[i]);
        return total;
    }

    /// <summary>Independent draw stream for one purpose of an outer seed (SplitMix-keyed).</summary>
    public static IRandom Stream(int outerSeed, uint purpose) => DefaultRandom.FromKey(DefaultRandom.Key(outerSeed, purpose));

    /// <summary>λ = a × source + b × background, pixel by pixel (either map may be null = 0).</summary>
    public static void Mean(Span<double> lambda, double a, double[]? source, double b, double[]? background)
    {
        for (int i = 0; i < lambda.Length; i++)
            lambda[i] = (source is null ? 0 : a * source[i]) + (background is null ? 0 : b * background[i]);
    }

    /// <summary>Decoder errors over repeated acquisitions of one condition: RMS radial error, gross failures, the signed
    /// mean error vector (systematic pull, AB-12) and the share within a tolerance.</summary>
    public sealed class ErrorTally(double failMm, double withinMm)
    {
        private int _n, _fail, _within;
        private double _sumSq, _sumDx, _sumDy;

        public void Add(double dxMm, double dyMm)
        {
            double r2 = dxMm * dxMm + dyMm * dyMm;
            _n++; _sumSq += r2; _sumDx += dxMm; _sumDy += dyMm;
            if (r2 > failMm * failMm) _fail++;
            if (r2 <= withinMm * withinMm) _within++;
        }

        public object Summary() => new
        {
            Repeats = _n, RmsErrorMm = _n > 0 ? Math.Sqrt(_sumSq / _n) : double.NaN,
            Failures = _fail, Within = _within, MeanDxMm = _n > 0 ? _sumDx / _n : double.NaN, MeanDyMm = _n > 0 ? _sumDy / _n : double.NaN
        };
    }
}
