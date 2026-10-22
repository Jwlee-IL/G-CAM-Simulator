using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Bounded periodic process-tree observation. ETW covers the explicitly recorded polling blind spot.</summary>
public sealed class ProcessMonitor : IDisposable
{
    private readonly Timer _timer;
    private readonly ProcessLedger _ledger;
    private readonly Func<IReadOnlyList<ProcessLedger.Instance>> _sample;
    private readonly object _sync = new();
    public List<string> Errors { get; } = [];
    public ProcessMonitor(ProcessLedger ledger, Func<IReadOnlyList<ProcessLedger.Instance>>? sample = null)
    {
        _ledger = ledger; _sample = sample ?? (() => Snapshot(ledger));
        void Observe(object? unused)
        {
            lock (_sync)
            {
                try { _ledger.Observe(_sample()); }
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

    /// <summary>Every semantic/coordinate action checks the live process instrument before supplying input.</summary>
    public void RequireInputSafe()
    {
        lock (_sync)
        {
            try { _ledger.Observe(_sample()); }
            catch (Exception e) { Errors.Add(e.GetType().Name + ":" + e.Message); }
            if (Errors.Count != 0) throw new InvalidOperationException("process-monitor-not-proven");
            if (_ledger.Issues.Count != 0) throw new InvalidOperationException("unproven-Studio-instance; further input refused");
        }
    }

    /// <summary>A module read can race loader/exit (Win32Exception while loading or exiting, InvalidOperationException once
    /// exited). Only a previously proven PID and birth time may use its registered image; any other instance still fails.</summary>
    public static string ReadStudioImage(ProcessLedger ledger, int pid, DateTimeOffset startedAt, Func<string> readModule)
    {
        var owned = ledger.Owned.SingleOrDefault(p => p.Pid == pid && p.StartedAt == startedAt);
        try { return readModule(); }
        catch (Exception e) when (owned is not null && e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // One bounded retry in this sample; neither transient failure becomes a permanent monitor error.
            try { return readModule(); }
            catch (Exception again) when (again is System.ComponentModel.Win32Exception or InvalidOperationException) { return owned.ImagePath; }
        }
    }

    public static IReadOnlyList<ProcessLedger.Instance> Snapshot(ProcessLedger ledger)
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
                    DateTimeOffset startedAt = p.StartTime;
                    string image = entry.Name.Equals("Gcam.Studio.exe", StringComparison.OrdinalIgnoreCase)
                        ? ReadStudioImage(ledger, p.Id, startedAt, () => p.MainModule?.FileName
                            ?? throw new System.ComponentModel.Win32Exception("Module not available")) : entry.Name;
                    results.Add(new(p.Id, (int)entry.ParentPid, startedAt, image, ""));
                }
                catch (ArgumentException) { } // exited between snapshot and read
                catch (System.ComponentModel.Win32Exception)
                {
                    if (entry.Name.Equals("Gcam.Studio.exe", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Cannot prove the discovered Studio instance");
                    // Protected unrelated processes are outside the registered-image boundary.
                }
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
