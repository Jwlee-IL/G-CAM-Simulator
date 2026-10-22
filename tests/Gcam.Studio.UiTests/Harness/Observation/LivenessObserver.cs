using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Gcam.Studio.UiTests.Harness.Observation;

/// <summary>Background WM_NULL probes independent of any synchronous UIA call. Never terminates the target.</summary>
public sealed class LivenessObserver : IDisposable
{
    private readonly object _gate = new();
    private readonly Process _process;
    private readonly OwnedProcessIdentity _identity;
    private readonly IntPtr _hwnd;
    private readonly TimeSpan _limit, _period, _timeout;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DateTimeOffset _anchorUtc = DateTimeOffset.UtcNow;
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource<LivenessResult> _failure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _worker;
    private readonly List<LivenessResult.Interval> _intervals = [];
    private readonly HashSet<string> _errors = [];
    private double? _unresponsive;
    private double _longest;
    private int _samples;
    private int? _exit;
    private bool _early;
    private DateTimeOffset? _close;
    private LivenessResult? _final;
    /// <summary>Completes independently of the UIA caller, so a blocked UIA operation cannot hide the first failure.</summary>
    public Task<LivenessResult> Failure => _failure.Task;

    public LivenessObserver(IntPtr hwnd, OwnedProcessIdentity identity, TimeSpan? limit = null,
        TimeSpan? samplePeriod = null, TimeSpan? probeTimeout = null)
    {
        _hwnd = hwnd; _identity = identity;
        _limit = limit ?? TimeSpan.FromSeconds(10); _period = samplePeriod ?? TimeSpan.FromMilliseconds(100);
        _timeout = probeTimeout ?? TimeSpan.FromMilliseconds(100);
        if (_limit <= TimeSpan.Zero || _period <= TimeSpan.Zero || _timeout <= TimeSpan.Zero || _timeout > TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(limit));
        _process = Process.GetProcessById(identity.Pid);
        if (!identity.Matches(_process)) { _process.Dispose(); throw new InvalidOperationException("Owned process identity mismatch"); }
        _ = _process.SafeHandle; // Retain the instance through exit, even after its PID leaves the process table.
        _worker = new(Observe) { IsBackground = true, Name = "GCAM liveness observer" }; _worker.Start();
    }

    /// <summary>Call immediately BEFORE requesting close. An already exited process remains an early exit.</summary>
    public void MarkIntentionalClose()
    {
        lock (_gate)
        {
            CheckExit(); _close ??= DateTimeOffset.UtcNow;
            EndNonResponse(_clock.Elapsed.TotalSeconds);
        }
    }
    public LivenessResult Snapshot()
    {
        lock (_gate)
        {
            if (_final is not null) return _final;
            CheckExit();
            double end = _clock.Elapsed.TotalSeconds;
            var intervals = _intervals.ToList();
            if (_unresponsive is double start) intervals.Add(new(start, end));
            double longest = Math.Max(_longest, _unresponsive is double s ? end - s : 0);
            return new(_identity, _hwnd.ToInt64(), _anchorUtc, _limit.TotalSeconds, _period.TotalSeconds, _timeout.TotalSeconds,
                _samples, longest, longest >= _limit.TotalSeconds, _unresponsive is not null,
                _early, _exit, _exit is not null and not 0, _close, intervals.ToArray(), _errors.ToArray());
        }
    }
    private bool CheckExit()
    {
        try
        {
            if (!_process.HasExited) return false;
            _exit = _process.ExitCode;
            // Ordered by observation under the gate, never by comparing clocks: the kernel's exit time has timer-tick
            // granularity (up to ~16 ms) while the mark is a precise UTC reading, so a prompt exit after the mark could
            // compare as earlier. MarkIntentionalClose checks for exit before setting the mark, so an exit first seen
            // with no mark happened before it, and a process still running when the mark was set exited after it.
            _early |= _close is null;
            EndNonResponse(_clock.Elapsed.TotalSeconds);
            return true;
        }
        catch (Exception e) { _errors.Add("process: " + e.Message); return true; }
    }
    private void EndNonResponse(double end)
    {
        if (_unresponsive is not double start) return;
        _intervals.Add(new(start, end)); _longest = Math.Max(_longest, end - start); _unresponsive = null;
    }
    private void Observe()
    {
        while (!_stop.IsCancellationRequested)
        {
            bool exited, closing;
            lock (_gate) { exited = CheckExit(); closing = _close is not null; }
            if (exited) { PublishFailure(); return; }
            // Once close was requested the HWND may disappear before the process. Exit-code observation continues;
            // the caller's separate cleanup deadline bounds a process that does not finish closing.
            if (closing) { _stop.Token.WaitHandle.WaitOne(_period); continue; }
            double start = _clock.Elapsed.TotalSeconds;
            GetWindowThreadProcessId(_hwnd, out uint pid);
            if (pid != _identity.Pid)
            {
                lock (_gate) { _samples++; if (_close is null) _errors.Add("owned-window-missing-or-replaced"); }
            }
            else
            {
                // BLOCK + ERRORONEXIT, deliberately no ABORTIFHUNG (the OS heuristic is not our charter limit).
                bool answered = SendMessageTimeout(_hwnd, 0, UIntPtr.Zero, IntPtr.Zero, 0x21,
                    (uint)Math.Ceiling(_timeout.TotalMilliseconds), out _) != IntPtr.Zero;
                int error = Marshal.GetLastWin32Error();
                lock (_gate)
                {
                    _samples++;
                    if (answered) EndNonResponse(_clock.Elapsed.TotalSeconds);
                    else if (_close is null)
                    {
                        _unresponsive ??= start;
                        if (error is not 0 and not 1460) _errors.Add("WM_NULL: " + new Win32Exception(error).Message);
                    }
                }
            }
            PublishFailure();
            _stop.Token.WaitHandle.WaitOne(_period);
        }
    }
    private void PublishFailure()
    {
        var result = Snapshot();
        if (!result.Passed) _failure.TrySetResult(result);
    }
    public LivenessResult Stop()
    {
        if (_final is not null) return _final;
        _stop.Cancel(); _worker.Join();
        lock (_gate) _final = Snapshot();
        if (!_final.Passed) _failure.TrySetResult(_final);
        _process.Dispose(); _stop.Dispose();
        return _final;
    }
    public void Dispose() => Stop();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SendMessageTimeout(IntPtr hwnd,
        uint message, UIntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
}
