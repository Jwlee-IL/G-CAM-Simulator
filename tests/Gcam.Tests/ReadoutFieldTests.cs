using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;

namespace Gcam.Tests;

public sealed class ReadoutFieldTests
{
    [Theory]
    [InlineData(AmbientGeometry.FrontOnlyThroughMask)]
    [InlineData(AmbientGeometry.BareCrystalAllFaces)]
    public void RecordedMerge_PreservesOwnedSitesAndPartitions(AmbientGeometry geometry)
    {
        var config = new SimulationConfig { Seed = 4711, Ambient = new() { Geometry = geometry, DoseRateMicroSvPerHour = 1 } };
        using var whole = new ListModeSource(config, true);
        using var split = new ListModeSource(config, true);
        static List<RecordedTransportEvent> Collect(ListModeSource source, double horizon)
        {
            var result = new List<RecordedTransportEvent>();
            while (source.ArrivalTimeS < horizon)
            {
                var e = source.AdvanceUntil(horizon);
                if (e is null) { Assert.Null(source.LastRecordedEvent); continue; }
                var recorded = Assert.IsType<RecordedTransportEvent>(source.LastRecordedEvent);
                Assert.Equal(e, recorded.Event);
                result.Add(recorded);
            }
            return result;
        }
        var expected = Collect(whole, 10);
        var actual = new List<RecordedTransportEvent>();
        for (int i = 1; i <= 40; i++) actual.AddRange(Collect(split, i * .25));
        Assert.Contains(expected, e => e.Origin == TransportOrigin.Ambient);
        Assert.Contains(expected, e => e.Origin == TransportOrigin.Signal);
        Assert.Equal(expected.Select(e => e.Event), actual.Select(e => e.Event));
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Origin, actual[i].Origin);
            Assert.Equal(expected[i].Sites, actual[i].Sites);
            Assert.NotEmpty(actual[i].Sites);
            // Each site requires at most 32 subtract/scatter steps; two reductions and clamp add
            // at most 2n+1 operations. gamma_k bounds accumulated roundoff at the incident scale.
            double n = actual[i].Sites.Count, k = 32 * n + 2 * n + 1, u = Math.ScaleB(1.0, -53);
            double energyScale = Math.Max(config.Source.EnergyKeV, config.Ambient!.Spectrum.Lines.Max(l => l.EnergyKeV));
            double bound = k * u / (1 - k * u) * energyScale;
            Assert.InRange(Math.Abs(actual[i].Sites.Sum(s => s.EnergyKeV) - actual[i].Event.DepositKeV), 0, bound);
        }
        var frozen = actual[0].Sites.ToArray();
        Collect(split, 11);
        Assert.Equal(frozen, actual[0].Sites);
    }

    [Fact]
    public void EmptyAndFutureIntervals_ClearReturnedSites_WithoutDroppingPendingOwnership()
    {
        var c = new SimulationConfig { Seed = 4711, Ambient = new() { DoseRateMicroSvPerHour = 0 } };
        c.Source.ActivityBq = 0;
        using var empty = new ListModeSource(c, true);
        Assert.Null(empty.AdvanceUntil(20)); Assert.Equal(20, empty.ArrivalTimeS); Assert.Null(empty.LastRecordedEvent);
        c.Ambient = null; c.Source.ActivityBq = 1;
        using var source = new ListModeSource(c, true);
        while (source.ArrivalTimeS < .001) source.AdvanceUntil(.001);
        Assert.Null(source.LastRecordedEvent);
        RecordedTransportEvent? next = null;
        while (next is null) { source.AdvanceUntil(1e6); next = source.LastRecordedEvent; }
        Assert.NotEmpty(next.Sites);
        Assert.Equal(next.Event, next.Event with { ArrivalTimeS = source.ArrivalTimeS });
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void ModuleEntry_SelectsFrontRearOrNoSidePassive_AndKeepsPassingDirection(int face)
    {
        Ray[] rays = [new(new(2.5, 3.5, 20), new(0, 0, -1)), new(new(2.5, 3.5, -20), new(0, 0, 1)),
            new(new(-20, 2.5, -5), new(1, 0, 0)), new(new(20, 2.5, -5), new(-1, 0, 0)),
            new(new(2.5, -20, -5), new(0, 1, 0)), new(new(2.5, 20, -5), new(0, -1, 0))];
        // First passive draw is an unchanged pass; following draws force a short photoelectric flight.
        var rng = new SequenceRandom([.999999, .5, 0]);
        var detector = new ComptonCrystalDetector(12, 12, 1, 1, double.MaxValue, ComptonStrategy.Argmax, rng,
            muAt662PerMm: 1000, entryThroughAllFaces: true, boundaryAwareModule: true,
            entranceAbsorber: new(1), backingScatterer: new(1));
        Assert.True(detector.Score(new Photon { Ray = rays[face], EnergyKeV = 20, Weight = 1 }));
        Assert.Equal(face < 2 ? 3 : 2, rng.Draws);
        // Opaque incoming front/rear proxy absorbs; side entry never samples either slab.
        var opaque = new ComptonCrystalDetector(12, 12, 1, 1, double.MaxValue, ComptonStrategy.Argmax,
            new SequenceRandom([.5, 0, 0]), muAt662PerMm: 1000, entryThroughAllFaces: true,
            boundaryAwareModule: true, entranceAbsorber: new(1e6), backingScatterer: new(1e6));
        Assert.Equal(face >= 2, opaque.Score(new Photon { Ray = rays[face], EnergyKeV = 20, Weight = 1 }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void IncomingPassiveScatter_RejectsOutwardContinuation(bool rear)
    {
        // Force passive interaction, Compton rather than photoelectric, a near-180-degree
        // accepted Kahn draw, then azimuth. No crystal random draw may follow rejection.
        var rng = new SequenceRandom([0, .999, 0, .999999, 0, 0]);
        var detector = new ComptonCrystalDetector(12, 12, 1, 1, double.MaxValue, ComptonStrategy.Argmax, rng,
            entryThroughAllFaces: true, boundaryAwareModule: true, entranceAbsorber: new(100), backingScatterer: new(100));
        var ray = rear ? new Ray(new(2.5, 3.5, -20), new(0, 0, 1)) : new Ray(new(2.5, 3.5, 20), new(0, 0, -1));
        Assert.False(detector.Score(new Photon { Ray = ray, EnergyKeV = 661.7, Weight = 1 }));
        Assert.Equal(6, rng.Draws);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ModuleFrontOrZeroPassives_EqualsDefaultSeededTransport(bool allFaces)
    {
        var a = new List<InteractionSite>(); var b = new List<InteractionSite>();
        ComptonCrystalDetector Build(bool module, List<InteractionSite> sites) => new(12, 12, 1, 1, double.MaxValue,
            ComptonStrategy.Argmax, new DefaultRandom(73), reflectorGapMm: .1,
            entranceAbsorber: allFaces ? null : new(.15), backingScatterer: allFaces ? null : new(2),
            interactionSink: (s, _) => sites.AddRange(s), entryThroughAllFaces: allFaces, boundaryAwareModule: module);
        var legacy = Build(false, a); var physical = Build(true, b);
        var random = new DefaultRandom(44);
        for (int i = 0; i < 1000; i++)
        {
            var ray = allFaces && i % 2 == 0 ? new Ray(new(2.5, 3.5, -20), new(0, 0, 1))
                : new Ray(new((random.NextDouble() - .5) * 12, (random.NextDouble() - .5) * 12, 20), new(0, 0, -1));
            var photon = new Photon { Ray = ray, EnergyKeV = 661.7, Weight = 1 };
            a.Clear(); b.Clear();
            Assert.Equal(legacy.Score(photon), physical.Score(photon)); Assert.Equal(a, b);
        }
        Assert.Equal(legacy.Readout().Raw.ToArray(), physical.Readout().Raw.ToArray());
    }

    private sealed class SequenceRandom(double[] values) : IRandom
    {
        public int Draws { get; private set; }
        public double NextDouble() => values[Math.Min(Draws++, values.Length - 1)];
        public Vector3 NextOnUnitSphere() => throw new NotSupportedException();
    }
}
