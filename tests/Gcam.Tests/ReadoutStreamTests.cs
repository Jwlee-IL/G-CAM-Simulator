using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Tests;

public sealed class ReadoutStreamTests
{
    private static ReadoutPulseProcessor Processor()
    {
        var ro = new ReadoutConfig { Mode = ReadoutMode.FourOutputAnger };
        ro.Optics.PhotonsPerBin = 10; ro.Optics.DepthBins = 1;
        ro.Network.Topology = NetworkTopology.IdealBilinear;
        return new(new ReadoutDevice(new DetectorConfig { PixelsX = 2, PixelsY = 2 }, ro, 1), ro.Pulse, ro.Trigger);
    }

    [Theory]
    [InlineData(8)] [InlineData(73)] [InlineData(500)]
    public void PartitionedObservedStream_MatchesBatchExactly(int partitionNs)
    {
        var p = Processor();
        ReadoutHit[] hits = [new(100, [800, 10, 20, 30], 860), new(180, [30, 20, 10, 800], 860),
            new(5000, [200, 300, 400, 500], 1400), new(10000, [0, 0, 0, 0], 0)];
        var expected = p.Process(hits, new DefaultRandom(33));
        var stream = p.CreateStream(new DefaultRandom(33));
        var actual = new List<ObservedReadoutEvent>();
        int next = 0;
        for (int horizon = partitionNs; horizon < 20000; horizon += partitionNs)
        {
            while (next < hits.Length && hits[next].TimeNs <= horizon) stream.Append(hits[next++]);
            actual.AddRange(stream.AdvanceTo(horizon));
        }
        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(0, stream.RetainedHitCount);
        for (int i = 0; i < expected.Count; i++)
        {
            // Exact arithmetic/RNG oracle: no Monte Carlo tolerance applies to a realised hit sequence.
            Assert.Equal(expected[i].Codes, actual[i].Codes);
            Assert.Equal(expected[i].HoldTimeNs, actual[i].HoldTimeNs);
            Assert.Equal(expected[i].DominantHit, actual[i].DominantHit);
            Assert.Equal(expected[i].ContributingHits, actual[i].ContributingHits);
            Assert.Equal(expected[i].DominantShare, actual[i].DominantShare);
        }
    }

    [Fact]
    public void HoldWaitsForObservedFuture_AndCancellationConsumesNoNoise()
    {
        var p = Processor(); var stream = p.CreateStream(new DefaultRandom(9));
        stream.Append(new(0, [1000, 1000, 1000, 1000], 4000));
        Assert.Empty(stream.AdvanceTo(32)); Assert.True(stream.HasPendingHold);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => stream.AdvanceTo(10000, cancelled.Token));
        Assert.True(stream.HasPendingHold);
        var actual = Assert.Single(stream.AdvanceTo(10000));
        var reference = Assert.Single(p.Process([new(0, [1000, 1000, 1000, 1000], 4000)], new DefaultRandom(9)));
        Assert.Equal(reference.Codes, actual.Codes);
    }
}
