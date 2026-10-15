using System.Text.Json;

namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>An exclusive file handle serialises independent runners; expiry never permits stealing a live handle.</summary>
public sealed class DesktopLease : IDisposable
{
    public sealed record State(string RunId, int SessionId, DateTimeOffset AcquiredAt, DateTimeOffset ExpiresAt, string ReleaseStatus);
    private readonly FileStream _stream;
    public State Current { get; private set; }
    private bool _disposed;

    public DesktopLease(string path, string runId, int sessionId, DateTimeOffset now, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        path = IsolationPaths.Inside(Path.GetDirectoryName(Path.GetFullPath(path))!, Path.GetFileName(path));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (_stream.Length > 0)
            {
                var prior = JsonSerializer.Deserialize<State>(_stream) ?? throw new InvalidDataException("Invalid desktop lease");
                if (prior.ReleaseStatus != "released" && prior.ExpiresAt > now) throw new IOException("Another unexpired desktop lease exists");
            }
            Current = new(runId, sessionId, now, now + duration, "pending");
            Save();
        }
        catch { _stream.Dispose(); throw; }
    }

    private void Save()
    {
        _stream.Position = 0; _stream.SetLength(0);
        JsonSerializer.Serialize(_stream, Current); _stream.Flush(true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        try { Current = Current with { ReleaseStatus = "released" }; Save(); }
        finally { _disposed = true; _stream.Dispose(); }
    }
}
