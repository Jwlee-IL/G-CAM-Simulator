namespace Gcam.Studio.Services.Tests;

/// <summary>Explicit virtual time for acquisition service tests; never waits for physical acquisition time.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (_gate) return _ticks; }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(GetTimestamp());
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_gate) _timers.Add(timer);
        return timer;
    }
    public void Advance(TimeSpan interval)
    {
        ManualTimer[] ready;
        lock (_gate)
        {
            _ticks += interval.Ticks;
            ready = _timers.Where(t => !t.Disposed && t.Due <= _ticks).ToArray();
            foreach (var timer in ready) timer.Due = long.MaxValue;
        }
        foreach (var timer in ready) timer.Fire();
    }
    public async Task WaitForTimerAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            lock (_gate)
                if (_timers.Any(t => !t.Disposed && t.Due != long.MaxValue)) return;
            await Task.Delay(1, timeout.Token);
        }
    }

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public long Due { get; set; } = long.MaxValue;
        public bool Disposed { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (period != Timeout.InfiniteTimeSpan) throw new NotSupportedException("Only one-shot test timers are needed.");
            lock (clock._gate)
            {
                if (Disposed) return false;
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._ticks + dueTime.Ticks;
                return true;
            }
        }
        public void Fire() => callback(state);
        public void Dispose() { lock (clock._gate) Disposed = true; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
