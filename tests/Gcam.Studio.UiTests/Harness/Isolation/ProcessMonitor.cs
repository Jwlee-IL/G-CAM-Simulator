using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Bounded periodic process-tree observation. ETW covers the explicitly recorded polling blind spot.</summary>
public sealed class ProcessMonitor : IDisposable
{
    private readonly Timer _timer;
    private readonly ProcessLedger _ledger;
    private readonly object _sync = new();
    public List<string> Errors { get; } = [];
    public ProcessMonitor(ProcessLedger ledger, Func<IReadOnlyList<ProcessLedger.Instance>>? sample = null)
    {
        _ledger = ledger; sample ??= Snapshot;
        void Observe(object? unused)
        {
            lock (_sync)
            {
                try { _ledger.Observe(sample()); }
                catch (Exception e) { Errors.Add(e.GetType().Name + ":" + e.Message); }
            }
        }
        Observe(null); // proving the sampler precedes any application input
        if (Errors.Count != 0) throw new InvalidOperationException("process-monitor-not-proven");
        _timer = new Timer(Observe, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    public void Dispose()
    {
        using var done = new ManualResetEvent(false);
        if (_timer.Dispose(done) && !done.WaitOne(TimeSpan.FromSeconds(5))) throw new TimeoutException("Process monitor did not stop");
    }

    public static IReadOnlyList<ProcessLedger.Instance> Snapshot()
    {
        var results = new List<ProcessLedger.Instance>();
        IntPtr handle = CreateToolhelp32Snapshot(2, 0);
        if (handle == new IntPtr(-1)) throw new InvalidOperationException("Process snapshot unavailable");
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), Name = "" };
            if (!Process32First(handle, ref entry)) throw new InvalidOperationException("Process snapshot empty");
            do
            {
                // Protected unrelated processes do not need their private executable path read.
                if (entry.Pid == 0) continue;
                try
                {
                    using var p = Process.GetProcessById((int)entry.Pid);
                    results.Add(new(p.Id, (int)entry.ParentPid, p.StartTime,
                        entry.Name.Equals("Gcam.Studio.exe", StringComparison.OrdinalIgnoreCase) ? p.MainModule?.FileName ?? entry.Name : entry.Name, ""));
                }
                catch (ArgumentException) { } // exited between snapshot and read
                catch (System.ComponentModel.Win32Exception) { } // protected unrelated process; ETW is the complete image source
            } while (Process32Next(handle, ref entry));
        }
        finally { CloseHandle(handle); }
        return results;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, Pid; public UIntPtr Heap; public uint Module, Threads, ParentPid; public int Priority; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
