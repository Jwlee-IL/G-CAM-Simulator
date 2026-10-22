namespace Gcam.Studio.UiTests.Harness.Observation;

/// <summary>Socket snapshots are observation, not a firewall; connections between samples may be missed.</summary>
public sealed record ConnectionResult(OwnedProcessIdentity Process, int SampleCount, bool PositiveControlPassed,
    ConnectionResult.Sample[] Samples, string[] Errors)
{
    public Endpoint[] ControlEndpoints { get; init; } = [];
    public OwnedProcessIdentity? ControlProcess { get; init; }
    public sealed record Endpoint(string Protocol, string LocalAddress, int LocalPort, string? RemoteAddress,
        int? RemotePort, int State, bool LoopbackToSelf);
    public sealed record Sample(DateTimeOffset AtUtc, Endpoint[] Endpoints);
    public Endpoint[] Findings => Samples.SelectMany(s => s.Endpoints).Where(e => !e.LoopbackToSelf).Distinct().ToArray();
    public bool Passed => SampleCount > 0 && PositiveControlPassed && Findings.Length == 0 && Errors.Length == 0;
}
