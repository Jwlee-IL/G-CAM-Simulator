namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>A retained harness effect, written once after tracing is ready and before Studio starts.</summary>
public sealed class RegistryMarker(IRegistryMarkerStore store)
{
    public const string RelativeRoot = @"Software\GcamUiaProbe";
    public const string ValueName = "RunId";
    public sealed record Control(string Channel, int Pid, string RunId, string KeyPath, string TraceKeyPath,
        string ValueName, string ValueData, DateTimeOffset StartedAt, DateTimeOffset FinishedAt);

    public Control Write(string runId, int harnessPid)
    {
        // Share the launch identifier validation; never permit a registry path supplied by a scenario.
        _ = IsolationPaths.RunRoot("scratch", runId);
        if (runId.Length > 71) throw new ArgumentException("Probe run ID exceeds the trace wrapper limit", nameof(runId));
        if (harnessPid <= 0) throw new ArgumentOutOfRangeException(nameof(harnessPid));
        string key = RelativeRoot + "\\" + runId;
        string hive = store.NativeUserHive;
        if (!System.Text.RegularExpressions.Regex.IsMatch(hive, @"^\\REGISTRY\\USER\\S-1-[0-9]+(?:-[0-9]+)+$"))
            throw new InvalidDataException("Invalid native user hive identity");
        var started = DateTimeOffset.UtcNow;
        if (!store.TryCreateNew(key, ValueName, runId))
            throw new IOException("Registry probe marker already exists; use a fresh run ID (no automatic deletion)");
        return new("registry", harnessPid, runId, "HKCU\\" + key, hive + "\\" + key, ValueName, runId, started, DateTimeOffset.UtcNow);
    }
}
