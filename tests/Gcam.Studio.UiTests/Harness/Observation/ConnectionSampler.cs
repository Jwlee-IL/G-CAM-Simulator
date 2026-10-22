using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Gcam.Studio.UiTests.Harness.Observation;

/// <summary>Non-elevated IPv4/IPv6 TCP/UDP owner tables, filtered to a retained process instance.</summary>
public sealed class ConnectionSampler : IDisposable
{
    private readonly object _gate = new();
    private readonly Process _process;
    private readonly OwnedProcessIdentity _identity;
    private readonly List<ConnectionResult.Sample> _samples = [];
    private readonly HashSet<string> _errors = [];
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _worker;
    private bool _control;
    private ConnectionResult.Endpoint[] _controlEndpoints = [];
    private OwnedProcessIdentity? _controlProcess;

    private readonly Func<int, ConnectionResult.Endpoint[]> _read;

    public ConnectionSampler(OwnedProcessIdentity identity, TimeSpan? samplePeriod = null) : this(identity, samplePeriod, ReadOwned) { }
    /// <summary>Test seam: the owner-table reader.</summary>
    internal ConnectionSampler(OwnedProcessIdentity identity, TimeSpan? samplePeriod, Func<int, ConnectionResult.Endpoint[]> read)
    {
        _identity = identity; _read = read; _process = Process.GetProcessById(identity.Pid);
        try
        {
            // Open and keep the handle BEFORE the identity check: while it is held the PID cannot name another process,
            // so every later table row with this PID belongs to this instance or to none.
            _ = _process.SafeHandle;
            if (!identity.Matches(_process)) throw new InvalidOperationException("Owned process identity mismatch");
        }
        catch { _process.Dispose(); throw; }
        var period = samplePeriod ?? TimeSpan.FromMilliseconds(100);
        if (period <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(samplePeriod));
        _worker = new(() =>
        {
            while (!_stop.IsCancellationRequested)
            {
                SampleNow();
                _stop.Token.WaitHandle.WaitOne(period);
            }
        }) { IsBackground = true, Name = "GCAM connection observer" }; _worker.Start();
    }
    public void SampleNow()
    {
        lock (_gate)
        {
            try
            {
                if (_process.HasExited) return;
                var rows = _read(_identity.Pid);
                // Re-checked after every read through the retained handle: a read overlapping exit (or any identity
                // change) is discarded rather than attributed.
                if (_process.HasExited) return;
                if (!_identity.Matches(_process)) throw new InvalidOperationException("Owned process identity changed");
                _samples.Add(new(DateTimeOffset.UtcNow, rows));
            }
            catch (Exception e) { _errors.Add(e.GetType().Name + ": " + e.Message); }
        }
    }
    /// <summary>Exercises the same owner-table reader with harness sockets; never attributes these to the product.</summary>
    public bool PositiveControl()
    {
        try
        {
            using var harness = Process.GetCurrentProcess();
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using var client = new TcpClient(); client.Connect((IPEndPoint)listener.LocalEndpoint);
            using var accepted = listener.AcceptTcpClient();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            ConnectionResult.Endpoint[] observed = [];
            bool seen = SpinWait.SpinUntil(() =>
            {
                observed = ReadOwned(Environment.ProcessId).Where(e => e.Protocol == "tcp4" && e.LocalPort == port
                    && e.RemotePort != 0 && e.LoopbackToSelf).ToArray(); return observed.Length > 0;
            }, TimeSpan.FromSeconds(3));
            lock (_gate)
            {
                _control = seen; _controlEndpoints = observed; _controlProcess = OwnedProcessIdentity.From(harness);
                if (!seen) _errors.Add("network-positive-control-not-observed");
            }
            return seen;
        }
        catch (Exception e) { lock (_gate) _errors.Add("positive-control: " + e.Message); return false; }
    }
    public ConnectionResult Snapshot()
    {
        lock (_gate) return new(_identity, _samples.Count, _control, _samples.ToArray(), _errors.ToArray())
            { ControlEndpoints = _controlEndpoints, ControlProcess = _controlProcess };
    }
    public static ConnectionResult.Endpoint[] ReadOwned(int pid)
    {
        var endpoints = new List<ConnectionResult.Endpoint>();
        foreach (bool tcp in new[] { true, false })
            foreach (bool v6 in new[] { false, true })
            {
                byte[] bytes = ReadTable(tcp, v6);
                int count = BitConverter.ToInt32(bytes, 0), size = tcp ? (v6 ? 56 : 24) : (v6 ? 28 : 12);
                if (count < 0 || 4L + (long)count * size > bytes.Length) throw new InvalidDataException("Malformed owner table");
                for (int i = 0; i < count; i++)
                {
                    int row = 4 + i * size;
                    if (BitConverter.ToInt32(bytes, row + size - 4) != pid) continue;
                    int local = row + (tcp && !v6 ? 4 : 0), localPort = local + (v6 ? 20 : 4);
                    string address = v6 ? new IPAddress(bytes.AsSpan(local, 16), BitConverter.ToUInt32(bytes, local + 16)).ToString()
                        : new IPAddress(bytes.AsSpan(local, 4)).ToString();
                    int Port(int at) => (bytes[at] << 8) | bytes[at + 1];
                    string? remote = null; int? remotePort = null;
                    if (tcp)
                    {
                        int at = row + (v6 ? 24 : 12);
                        remote = v6 ? new IPAddress(bytes.AsSpan(at, 16), BitConverter.ToUInt32(bytes, at + 16)).ToString()
                            : new IPAddress(bytes.AsSpan(at, 4)).ToString();
                        remotePort = Port(at + (v6 ? 20 : 4));
                    }
                    int state = tcp ? BitConverter.ToInt32(bytes, row + (v6 ? 48 : 0)) : 0;
                    endpoints.Add(new((tcp ? "tcp" : "udp") + (v6 ? "6" : "4"), address, Port(localPort), remote, remotePort, state, false));
                }
            }
        return endpoints.Select(e => e with { LoopbackToSelf = e.Protocol.StartsWith("tcp", StringComparison.Ordinal)
            && IPAddress.IsLoopback(IPAddress.Parse(e.LocalAddress)) && (e.State == 2 || e.RemoteAddress is not null
            && IPAddress.IsLoopback(IPAddress.Parse(e.RemoteAddress)) && endpoints.Any(r => r.Protocol == e.Protocol
                && r.LocalAddress == e.RemoteAddress && r.RemoteAddress == e.LocalAddress
                && r.LocalPort == e.RemotePort && r.RemotePort == e.LocalPort)) }).ToArray();
    }
    private static byte[] ReadTable(bool tcp, bool v6)
    {
        int length = 0, family = v6 ? 23 : 2;
        uint Call(IntPtr buffer, ref int size) => tcp ? GetExtendedTcpTable(buffer, ref size, false, family, 5, 0)
            : GetExtendedUdpTable(buffer, ref size, false, family, 1, 0);
        uint status = Call(IntPtr.Zero, ref length);
        if (status != 122 && status != 0) throw new Win32Exception((int)status);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                int capacity = length; status = Call(buffer, ref length);
                if (status == 122) continue;
                if (status != 0) throw new Win32Exception((int)status);
                byte[] bytes = new byte[capacity]; Marshal.Copy(buffer, bytes, 0, capacity); return bytes;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new IOException("Owner table kept growing during bounded read");
    }
    public void Dispose() { _stop.Cancel(); _worker.Join(); _stop.Dispose(); _process.Dispose(); }
    [DllImport("iphlpapi.dll")] private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);
    [DllImport("iphlpapi.dll")] private static extern uint GetExtendedUdpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);
}
