using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Tests;

public sealed class ReadoutArtifactTests
{
    [Fact]
    public void ContentReuse_KeysSeedAndMutableConfig_SessionHasPrivateBuffers()
    {
        var detector = new DetectorConfig { PixelsX = 4, PixelsY = 4 };
        var config = new ReadoutConfig { Mode = ReadoutMode.FourOutputAnger };
        config.Optics.PhotonsPerBin = 200; config.Optics.DepthBins = 2;
        var first = new ReadoutDevice(detector, config, 41000001);
        var second = new ReadoutDevice(detector, config, 41000001);
        var session = new ReadoutDevice(first, config);
        Assert.Same(first.Optics, second.Optics); Assert.Same(first.Network, second.Network);
        Assert.Same(first.Network, session.Network);
        Assert.NotSame(first.Optics, new ReadoutDevice(detector, config, 41000002).Optics);
        var shares = first.Optics.Response(0, 0).ToArray();
        config.Optics.CouplantThicknessMm *= 2;
        Assert.NotSame(first.Optics, new ReadoutDevice(detector, config, 41000001).Optics);
        Assert.Equal(shares, first.Optics.Response(0, 0).ToArray());
        config.Network.ColumnResistanceOhm *= 2;
        Assert.Throws<ArgumentException>(() => new ReadoutDevice(first, config));
        var circuit = first.Network.Circuit!;
        string netlist = circuit.ToSpice("identity");
        circuit.NodeNames[0] = "mutated"; circuit.InputNodes[0] = 999; circuit.Resistors[0] = (0, 0, 0);
        Assert.Equal(netlist, circuit.ToSpice("identity"));
    }

    [Fact]
    public void CancelledBuild_RefusesEvenOnACacheHit()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => ChargeDivisionNetwork.Dpc(4, 4, 1000, 10,
            cancellationToken: cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => new ReadoutDevice(new DetectorConfig(),
            new() { Mode = ReadoutMode.FourOutputAnger }, 1, cancelled.Token));
    }
}
