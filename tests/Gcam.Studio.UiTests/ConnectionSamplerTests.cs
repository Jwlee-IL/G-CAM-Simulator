using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Gcam.Studio.UiTests.Harness.Observation;

namespace Gcam.Studio.UiTests;

public sealed class ConnectionSamplerTests
{
    [Fact]
    public void FourOwnerTables_SeeOwnLoopbackTcpAndUdp_AndPositiveControl()
    {
        using var current = Process.GetCurrentProcess();
        using var sampler = new ConnectionSampler(OwnedProcessIdentity.From(current));
        Assert.True(sampler.PositiveControl());
        using var tcp4 = new TcpListener(IPAddress.Loopback, 0); tcp4.Start();
        using var tcp6 = new TcpListener(IPAddress.IPv6Loopback, 0); tcp6.Start();
        using var client4 = new TcpClient(); client4.Connect((IPEndPoint)tcp4.LocalEndpoint);
        using var accepted4 = tcp4.AcceptTcpClient();
        using var client6 = new TcpClient(AddressFamily.InterNetworkV6); client6.Connect((IPEndPoint)tcp6.LocalEndpoint);
        using var accepted6 = tcp6.AcceptTcpClient();
        using var udp4 = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var udp6 = new UdpClient(new IPEndPoint(IPAddress.IPv6Loopback, 0));
        sampler.SampleNow(); var result = sampler.Snapshot();
        Assert.Empty(result.Errors); Assert.True(result.PositiveControlPassed); Assert.True(result.SampleCount > 0);
        var rows = result.Samples.SelectMany(s => s.Endpoints).ToArray();
        foreach (string protocol in new[] { "tcp4", "tcp6", "udp4", "udp6" }) Assert.Contains(rows, e => e.Protocol == protocol);
        Assert.Contains(rows, e => e.Protocol == "tcp4" && e.LocalPort == ((IPEndPoint)tcp4.LocalEndpoint).Port && e.LoopbackToSelf);
        Assert.Contains(rows, e => e.Protocol == "tcp6" && e.LocalPort == ((IPEndPoint)tcp6.LocalEndpoint).Port && e.LoopbackToSelf);
        // UDP owner tables contain no remote endpoint: a bound socket cannot prove loopback-to-self.
        Assert.Contains(result.Findings, e => e.Protocol == "udp4" && e.LocalPort == ((IPEndPoint)udp4.Client.LocalEndPoint!).Port);
    }
    [PwshFact]
    public void LoopbackToAnotherProcess_IsAFindingEvenThoughNoExternalNetworkIsUsed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var helper = ObservationFixtures.Helper(0, "$c = [System.Net.Sockets.TcpClient]::new(); $c.Connect('127.0.0.1', " + port + ")");
        try
        {
            using var accepted = listener.AcceptTcpClient();
            using var sampler = new ConnectionSampler(OwnedProcessIdentity.From(helper)); sampler.SampleNow();
            Assert.Contains(sampler.Snapshot().Findings, e => e.RemotePort == port && e.RemoteAddress == "127.0.0.1");
        }
        finally { ObservationFixtures.Exit(helper); }
    }
    [PwshFact]
    public void ReadOverlappingExit_IsDiscarded_AndLaterSamplesStop()
    {
        using var helper = ObservationFixtures.Helper(0);
        try
        {
            bool exitDuringRead = false;
            ConnectionResult.Endpoint[] Read(int pid)
            {
                Assert.Equal(helper.Id, pid);
                if (Volatile.Read(ref exitDuringRead)) ObservationFixtures.Exit(helper); // the owned instance ends mid-read
                return [new("tcp4", "192.0.2.1", 50000, "192.0.2.2", 443, 5, false)];
            }
            using var sampler = new ConnectionSampler(OwnedProcessIdentity.From(helper), TimeSpan.FromHours(1), Read);
            Assert.True(SpinWait.SpinUntil(() => sampler.Snapshot().SampleCount == 1, 5000));
            Volatile.Write(ref exitDuringRead, true);
            sampler.SampleNow(); sampler.SampleNow();
            var result = sampler.Snapshot();
            Assert.Equal(1, result.SampleCount); Assert.Empty(result.Errors);
        }
        finally { ObservationFixtures.Exit(helper); }
    }
    [Fact]
    public void ReusedPidIdentity_IsRejected()
    {
        using var current = Process.GetCurrentProcess();
        Assert.Throws<InvalidOperationException>(() => new ConnectionSampler(OwnedProcessIdentity.From(current)
            with { StartedAtUtc = DateTimeOffset.UtcNow.AddDays(-1) }));
    }
}
