using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class ProcessLedgerTests
{
    [Fact]
    public void Ownership_RejectsReusedPidWrongImageAndWrongMarker()
    {
        var at = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var ledger = new ProcessLedger("fixture", "Studio.exe");
        ledger.RegisterDirect(new(10, 1, at, "Studio.exe", "fixture"));
        Assert.True(ledger.Owns(10, at)); Assert.False(ledger.Owns(10, at.AddSeconds(1)));
        Assert.Throws<InvalidDataException>(() => ledger.RegisterDirect(new(11, 1, at, "other.exe", "fixture")));
        Assert.Throws<InvalidDataException>(() => ledger.RegisterDirect(new(11, 1, at, "Studio.exe", "other")));
        Assert.Throws<InvalidDataException>(() => ledger.RegisterDirect(new(10, 1, at, "Studio.exe", "fixture")));
    }
    [Fact]
    public void Snapshot_FlagsDescendantsGrandchildrenAndUnownedImage_WithoutAdopting()
    {
        var at = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var ledger = new ProcessLedger("fixture", "Studio.exe"); ledger.RegisterDirect(new(10, 1, at, "Studio.exe", "fixture"));
        ProcessLedger.Instance[] snapshot = [new(11, 10, at.AddSeconds(1), "child.exe", ""), new(12, 11, at.AddSeconds(2), "grandchild.exe", ""), new(13, 2, at.AddSeconds(2), "Studio.exe", "")];
        ledger.Observe(snapshot); ledger.Observe(snapshot);
        Assert.Equal(3, ledger.Issues.Count); Assert.Single(ledger.Owned);
    }
    [Fact]
    public void Monitor_ProvesInjectedSampler_AndRefusesBrokenSampler()
    {
        var ledger = new ProcessLedger("fixture", "Studio.exe");
        using (var monitor = new ProcessMonitor(ledger, () => [])) Assert.Empty(monitor.Errors);
        Assert.Throws<InvalidOperationException>(() => new ProcessMonitor(ledger, () => throw new IOException("fixture instrument")));
    }
}
