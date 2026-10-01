namespace Gcam.Studio.Core.Services;

/// <summary>A single acquisition. Stop retains its final snapshot; enumeration has one consumer.</summary>
public interface IAcquisitionSession : IAsyncDisposable
{
    IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync(CancellationToken cancellationToken = default);
    void Stop();
}
