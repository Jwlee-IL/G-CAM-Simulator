using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Bounded polling: transient UIA failures mean not yet; assertions and other errors remain strict.
/// Each completed or failed wait reports its transient count and last error to the scenario's run-record scope.</summary>
public static class PollingWait
{
    private static readonly AsyncLocal<Action<string>?> Logger = new();

    /// <summary>Installs the logger for this scenario only; nested scopes restore the previous logger.</summary>
    public static IDisposable Record(Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        var scope = new LogScope(Logger.Value);
        Logger.Value = log;
        return scope;
    }

    /// <summary>Same 50 ms polling interval and deadline as the original harness. The elapsed / pause delegates
    /// are a headless test seam: oracles advance a fake clock without sleeping or contacting UI Automation.</summary>
    public static void WaitUntil(Func<bool> condition, TimeSpan timeout, string what,
        Func<TimeSpan>? elapsed = null, Action? pause = null)
        => WaitUntil(condition, () => timeout, what, elapsed, pause);

    /// <summary>As above with a deadline read before every poll, so a caller can extend it once (the focus preparation's
    /// launch budget after a root re-acquisition); the timeout message names the deadline in force at the end.</summary>
    public static void WaitUntil(Func<bool> condition, Func<TimeSpan> deadline, string what,
        Func<TimeSpan>? elapsed = null, Action? pause = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(deadline);
        var watch = Stopwatch.StartNew();
        elapsed ??= () => watch.Elapsed;
        pause ??= () => Thread.Sleep(50);
        int transients = 0;
        Exception? last = null;
        try
        {
            while (elapsed() < deadline())
            {
                try
                {
                    if (condition()) return;
                }
                catch (Exception ex) when (ex is COMException or ElementNotAvailableException
                    or SelectorMatchException { Matches: 0 })
                {
                    last = ex;
                    transients++;
                }
                pause();
            }
            throw new TimeoutException(string.Create(CultureInfo.InvariantCulture,
                $"timed out after {deadline().TotalSeconds:0.#} s waiting for: {what}; transient UIA failures: {transients}"), last);
        }
        finally
        {
            Logger.Value?.Invoke(string.Create(CultureInfo.InvariantCulture,
                $"polling {what}: transient UIA failures: {transients}; last: {(last is null ? "none" : last.GetType().Name + ": " + last.Message)}"));
        }
    }

    private sealed class LogScope(Action<string>? previous) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            Logger.Value = previous;
            _disposed = true;
        }
    }
}
