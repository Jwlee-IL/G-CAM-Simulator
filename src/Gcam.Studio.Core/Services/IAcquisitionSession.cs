namespace Gcam.Studio.Core.Services;

/// <summary>One acquisition, run in segments. <see cref="ReadSnapshotsAsync"/> reads the current segment (the first
/// starts with the session); Stop ends a segment keeping everything; <see cref="Continue"/> starts the next segment from
/// the retained state — the same events, live-time clock and seed, as if the acquisition had never paused.</summary>
public interface IAcquisitionSession : IAsyncDisposable
{
    IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync(CancellationToken cancellationToken = default);
    void Stop();

    /// <summary>Starts the next segment up to <paramref name="presetLiveTimeS"/> (above the acquired live time) at
    /// <paramref name="speed"/>. Throws while a segment runs or after a failure.</summary>
    void Continue(double presetLiveTimeS, double speed) => throw new NotSupportedException("This session cannot continue.");
}
