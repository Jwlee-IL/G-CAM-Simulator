using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

public sealed class AmbientAngularSamplerTests
{
    private static IncidentSpectrum IsotropicTable() => new()
    {
        Id = "angular-test-NOT-VALIDATED", AngularModel = "EnergyZenithTable",
        Lines = [new() { EnergyKeV = 300, FluenceWeight = 1 }],
        EnergyZenith = [new() { EnergyIndex = 0, LowCosine = -1, HighCosine = 1, FluenceWeight = 1 }]
    };

    [Theory]
    [InlineData(AmbientGeometry.BareCrystalAllFaces, 42)]
    [InlineData(AmbientGeometry.FrontOnlyThroughMask, 6)]
    public void IsotropicTable_CurrentMatchesAnalyticSurfaceArea(AmbientGeometry geometry, double expectedArea)
    {
        var sampler = new AmbientAngularSampler(IsotropicTable(), geometry, 4, 6, 6);
        // Exact analytic integration; tolerance only covers the finite floating-point operations (64 ulps).
        Assert.InRange(Math.Abs(sampler.EffectiveAreaMm2 - expectedArea), 0, 64 * 2.2204460492503131e-16 * expectedArea);
    }

    [Theory]
    [InlineData(AmbientGeometry.BareCrystalAllFaces)]
    [InlineData(AmbientGeometry.FrontOnlyThroughMask)]
    public void JointDistribution_PreservesEnergyAngleCorrelationAndProjectedCurrent(AmbientGeometry geometry)
    {
        var spectrum = IsotropicTable();
        spectrum.Lines = [new() { EnergyKeV = 300, FluenceWeight = 1 }, new() { EnergyKeV = 800, FluenceWeight = 1 }];
        spectrum.EnergyZenith =
        [
            new() { EnergyIndex = 0, LowCosine = -.9, HighCosine = -.7, FluenceWeight = 1 },
            new() { EnergyIndex = 1, LowCosine = .1, HighCosine = .3, FluenceWeight = 1 }
        ];
        var sampler = new AmbientAngularSampler(spectrum, geometry, 4, 6, 6);
        bool front = geometry == AmbientGeometry.FrontOnlyThroughMask;
        double first = AmbientAngularSampler.MeanProjectedArea(-.9, -.7, 24, 36, 24, front);
        double second = AmbientAngularSampler.MeanProjectedArea(.1, .3, 24, 36, 24, front);
        double p = first / (first + second);
        const int n = 20000;
        var angular = new DefaultRandom(2831); var energy = new DefaultRandom(2832);
        int selected = 0, lowerHalf = 0;
        for (int i = 0; i < n; i++)
        {
            var sample = sampler.Sample(angular, energy, 65);
            if (sample.EnergyKeV == 300)
            {
                selected++;
                if (sample.Ray.Direction.Y < -.8) lowerHalf++;
                Assert.InRange(sample.Ray.Direction.Y, -.9, -.7);
            }
            else
            {
                Assert.Equal(800, sample.EnergyKeV);
                Assert.InRange(sample.Ray.Direction.Y, .1, .3);
            }
            if (front) Assert.True(sample.Ray.Direction.Z < 0);
            Assert.True(IntersectsCrystal(sample.Ray));
        }
        // Binomial sampling of the current-weighted energy component: six SE, derived from N=20000 and this p.
        Stat.Within((double)selected / n, p, Math.Sqrt(p * (1 - p) / n), 6, "energy component current");
        double conditional = AmbientAngularSampler.MeanProjectedArea(-.9, -.8, 24, 36, 24, front) / (2 * first);
        // Conditional zenith CDF uses the number selected for this energy, not the original N.
        Stat.Within((double)lowerHalf / selected, conditional, Math.Sqrt(conditional * (1 - conditional) / selected), 6, "conditional zenith CDF");
    }

    private static bool IntersectsCrystal(Ray ray)
    {
        double near = 0, far = double.PositiveInfinity;
        foreach (var (origin, direction, low, high) in new[]
        {
            (ray.Origin.X, ray.Direction.X, -2.0, 2.0),
            (ray.Origin.Y, ray.Direction.Y, -3.0, 3.0),
            (ray.Origin.Z, ray.Direction.Z, -6.0, 0.0)
        })
        {
            if (direction == 0) { if (origin < low || origin > high) return false; continue; }
            double a = (low - origin) / direction, b = (high - origin) / direction;
            near = Math.Max(near, Math.Min(a, b)); far = Math.Min(far, Math.Max(a, b));
        }
        return near <= far;
    }

    [Fact]
    public void InvalidTable_RejectsOverlappingBinsMarginalMismatchAndStaleHash()
    {
        var spectrum = IsotropicTable();
        spectrum.EnergyZenith![0].FluenceWeight = .9;
        Assert.Throws<ArgumentException>(() => new AmbientAngularSampler(spectrum, AmbientGeometry.BareCrystalAllFaces, 4, 6, 6));
        spectrum = IsotropicTable();
        spectrum.EnergyZenith =
        [
            new() { EnergyIndex = 0, LowCosine = -1, HighCosine = .1, FluenceWeight = .5 },
            new() { EnergyIndex = 0, LowCosine = 0, HighCosine = 1, FluenceWeight = .5 }
        ];
        Assert.Throws<ArgumentException>(() => new AmbientAngularSampler(spectrum, AmbientGeometry.BareCrystalAllFaces, 4, 6, 6));
        spectrum = IsotropicTable(); spectrum.IsValidated = true; spectrum.Id = "hash-test";
        spectrum.ContentHash = spectrum.ComputeContentHash(); spectrum.EnergyZenith![0].LowCosine = -.9;
        var config = new SimulationConfig { Ambient = new() { Spectrum = spectrum, RequireValidatedSpectrum = true } };
        Assert.Throws<InvalidOperationException>(() => new AmbientPhotonProcess(config));
    }

    [Theory]
    [InlineData(AmbientGeometry.BareCrystalAllFaces)]
    [InlineData(AmbientGeometry.FrontOnlyThroughMask)]
    public void AngularField_StopContinuePartitionKeepsEveryRecord(AmbientGeometry geometry)
    {
        var config = new SimulationConfig { Seed = 2718, Source = new() { ActivityBq = 0 },
            Ambient = new() { DoseRateMicroSvPerHour = .1, Geometry = geometry, Spectrum = IsotropicTable() } };
        using var whole = new ListModeSource(config); using var partitioned = new ListModeSource(config);
        var expected = new List<DetectedEvent>(); var actual = new List<DetectedEvent>();
        while (whole.ArrivalTimeS < 100) if (whole.AdvanceUntil(100) is { } e) expected.Add(e);
        for (int t = 1; t <= 100; t++)
            while (partitioned.ArrivalTimeS < t) if (partitioned.AdvanceUntil(t) is { } e) actual.Add(e);
        Assert.Equal(expected, actual);
    }
}
