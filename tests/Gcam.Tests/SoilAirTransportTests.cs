using Gcam.Core;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

public sealed class SoilAirTransportTests
{
    private static readonly SoilAirMaterials Materials = SoilAirMaterials.Load(RepoPaths.Sample("ambient/materials-v1.json"));

    private static SoilAirTransportOptions Options() => new()
    {
        ContinuumEdgesKeV = [10, 50, 100, 200, 400, 700, 1000, 1500, 2000, 3000]
    };

    private static TerrestrialChain Chain(params (double EnergyKeV, double Yield)[] lines) => new()
    {
        Name = "test",
        Lines = lines.Select(l => new TerrestrialLine { Nuclide = "test", EnergyKeV = l.EnergyKeV, YieldPerChainDecay = l.Yield }).ToArray()
    };

    [Fact]
    public void Run_IsReproduciblePerSeed()
    {
        var transport = new SoilAirTransport(Chain((661.7, .85), (1460.82, .1066)), Materials, Options());
        var a = transport.Run(20000, new DefaultRandom(5101));
        var b = transport.Run(20000, new DefaultRandom(5101));
        var c = transport.Run(20000, new DefaultRandom(5102));
        Assert.Equal(a.UncollidedSum, b.UncollidedSum);
        Assert.Equal(a.ContinuumSum, b.ContinuumSum);
        Assert.Equal(a.KermaTotal, b.KermaTotal);
        Assert.Equal(a.FluorescenceKermaBound, b.FluorescenceKermaBound);
        Assert.NotEqual(a.KermaTotal, c.KermaTotal);
    }

    [Fact]
    public void Run_UncollidedMatchesAnalyticHalfSpaceKernel()
    {
        // Real soil/air coefficients, two lines; N = 400000 histories, seed 5103. The estimator scores the slab
        // average, so it is compared with the slab-averaged analytic kernel. Tolerance 4σ with σ from the per-history
        // second moment (each history scores at most one uncollided crossing). Upward bins only: a source line can
        // never cross the slab downward uncollided, so those bins must be exactly zero.
        const long n = 400000;
        var chain = Chain((351.932, .3572), (1460.82, .1066));
        var options = Options();
        var tally = new SoilAirTransport(chain, Materials, options).Run(n, new DefaultRandom(5103));
        int zb = options.ZenithBins;
        for (int l = 0; l < chain.Lines.Length; l++)
        {
            for (int z = 0; z < zb / 2; z++) Assert.Equal(0, tally.UncollidedSum[l * zb + z]);
            double sum = 0, sq = 0;
            for (int z = zb / 2; z < zb; z++)
            {
                double cell = tally.UncollidedSum[l * zb + z], cellSq = tally.UncollidedSumSq[l * zb + z];
                sum += cell; sq += cellSq;
                double expected = SoilAirTransport.SlabAveragedUncollided(chain.Lines[l], Materials, options,
                    TerrestrialSpectrumBuilder.Cosine(z, zb), TerrestrialSpectrumBuilder.Cosine(z + 1, zb));
                Stat.Within(cell / n, expected, Se(cell, cellSq, n), what: $"line {l} bin {z}");
            }
            Stat.Within(sum / n, SoilAirTransport.SlabAveragedUncollided(chain.Lines[l], Materials, options, 0, 1), Se(sum, sq, n),
                what: $"line {l} total");
        }
    }

    [Fact]
    public void Run_TalliesAreConsistentAndPairProductionNeedsThreshold()
    {
        var options = Options();
        var below = new SoilAirTransport(Chain((661.7, 1)), Materials, options).Run(50000, new DefaultRandom(5104));
        Assert.Equal(0, below.PairEvents);                       // 661.7 keV < 2 m_e c²
        Assert.All(below.AnnihilationSum, v => Assert.Equal(0, v));
        Assert.Equal(0, below.ScatteredFluenceAboveLastEdge);    // scattered photons are below the source energy
        var above = new SoilAirTransport(Chain((2614.511, 1)), Materials, options).Run(50000, new DefaultRandom(5105));
        Assert.True(above.PairEvents > 0);
        Assert.True(above.FluenceAnnihilation > 0);
        foreach (var t in new[] { below, above })
        {
            // Bookkeeping identities (rounding only): binned + out-of-range scattered fluence = scattered fluence.
            double binned = t.ContinuumSum.Sum() + t.ScatteredFluenceBelowFirstEdge + t.ScatteredFluenceAboveLastEdge;
            Assert.Equal(t.FluenceScattered, binned, t.FluenceScattered * 1e-10);
            Assert.Equal(t.FluenceUncollided, t.UncollidedSum.Sum(), t.FluenceUncollided * 1e-10);
            Assert.True(t.FluorescenceKermaBound >= 0 && t.FluorescenceKermaBoundFromSoil <= t.FluorescenceKermaBound);
        }
    }

    [Fact]
    public void Constructor_RejectsInvalidRecipes()
    {
        Assert.Throws<ArgumentException>(() => new SoilAirTransport(Chain((.5, 1)), Materials, Options()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoilAirTransport(Chain((661.7, 1)), Materials,
            new SoilAirTransportOptions { ContinuumEdgesKeV = [10, 100], DepthSamplingFactor = 1 }));
        Assert.Throws<ArgumentException>(() => new SoilAirTransport(Chain((661.7, 1)), Materials,
            new SoilAirTransportOptions { ContinuumEdgesKeV = [100, 10] }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoilAirTransport(Chain((661.7, 1)), Materials,
            new SoilAirTransportOptions { ContinuumEdgesKeV = [10, 100], ZenithBins = 7 }));
    }

    private static double Se(double sum, double sumSq, long n)
    {
        double mean = sum / n;
        return Math.Sqrt(Math.Max(0, sumSq / n - mean * mean) / (n - 1));
    }
}
