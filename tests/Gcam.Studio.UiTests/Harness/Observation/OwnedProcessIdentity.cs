using System.Diagnostics;

namespace Gcam.Studio.UiTests.Harness.Observation;

/// <summary>A PID is never sufficient evidence after process reuse.</summary>
public sealed record OwnedProcessIdentity(int Pid, DateTimeOffset StartedAtUtc)
{
    public static OwnedProcessIdentity From(Process process) => new(process.Id, process.StartTime.ToUniversalTime());
    public bool Matches(Process process) => process.Id == Pid && process.StartTime.ToUniversalTime() == StartedAtUtc.UtcDateTime;
}
