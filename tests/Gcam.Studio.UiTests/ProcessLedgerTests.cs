using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class ProcessLedgerTests
{
    [Theory]
    [InlineData(10, 0, false, false)]
    [InlineData(10, 0, false, true)]
    [InlineData(11, 0, true, false)]
    [InlineData(11, 0, true, true)]
    [InlineData(10, 1, true, false)]
    public void ModuleReadFailure_OnlyProvenPidAndBirthMayRetry(int pid, int birthOffset, bool blocks, bool exited)
    {
        var at = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var ledger = new ProcessLedger("fixture", "Gcam.Studio.exe");
        ledger.RegisterDirect(new(10, 1, at, "Gcam.Studio.exe", "fixture"));
        int reads = 0;
        IReadOnlyList<ProcessLedger.Instance> Sample() => [new(pid, 1, at.AddSeconds(birthOffset),
            ProcessMonitor.ReadStudioImage(ledger, pid, at.AddSeconds(birthOffset), () =>
            {
                reads++;
                // Loading / exiting: Win32Exception; already exited: InvalidOperationException (Process.MainModule).
                throw exited ? (Exception)new InvalidOperationException("Synthetic exit race") : new System.ComponentModel.Win32Exception("Synthetic loader race");
            }), "")];
        if (blocks)
        {
            Assert.Throws<InvalidOperationException>(() => new ProcessMonitor(ledger, Sample));
            Assert.Equal(1, reads);
        }
        else
        {
            using var monitor = new ProcessMonitor(ledger, Sample);
            monitor.RequireInputSafe();
            Assert.Empty(monitor.Errors);
            Assert.Empty(ledger.Issues);
            Assert.InRange(reads, 4, 100);
        }
    }

    [Fact]
    public void OwnedModuleRead_RecoversOnTheBoundedRetry()
    {
        var at = DateTimeOffset.UtcNow;
        var ledger = new ProcessLedger("fixture", "Gcam.Studio.exe");
        ledger.RegisterDirect(new(10, 1, at, "Gcam.Studio.exe", "fixture"));
        int reads = 0;
        Assert.Equal("Gcam.Studio.exe", ProcessMonitor.ReadStudioImage(ledger, 10, at,
            () => ++reads == 1 ? throw new System.ComponentModel.Win32Exception() : "Gcam.Studio.exe"));
        Assert.Equal(2, reads);
    }
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

    [Fact]
    public void UnprovenRegisteredName_StopsFurtherInput_EvenWithAnotherImagePath()
    {
        var at = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var ledger = new ProcessLedger("fixture", @"C:\app\Gcam.Studio.exe");
        ledger.RegisterDirect(new(10, 1, at, @"C:\app\Gcam.Studio.exe", "fixture"));
        using var monitor = new ProcessMonitor(ledger, () => []);
        monitor.RequireInputSafe();
        ledger.Observe([new(20, 1, at.AddSeconds(1), @"D:\other\Gcam.Studio.exe", "")]);
        Assert.Throws<InvalidOperationException>(monitor.RequireInputSafe);
        Assert.Single(ledger.Owned);
    }

    [Fact]
    public void InputGuard_SamplesImmediately_AndRefusesAnInstrumentFailure()
    {
        var at = DateTimeOffset.Parse("2026-10-13T00:00:00Z");
        var ledger = new ProcessLedger("fixture", "Studio.exe");
        ledger.RegisterDirect(new(10, 1, at, "Studio.exe", "fixture"));
        bool newProcess = false, broken = false;
        using var monitor = new ProcessMonitor(ledger, () => broken ? throw new IOException("fixture")
            : newProcess ? [new(20, 1, at.AddSeconds(1), "Studio.exe", "")] : []);
        monitor.RequireInputSafe();
        newProcess = true;
        Assert.Throws<InvalidOperationException>(monitor.RequireInputSafe);
        Assert.Single(ledger.Owned);
        broken = true;
        Assert.Throws<InvalidOperationException>(monitor.RequireInputSafe);
        Assert.NotEmpty(monitor.Errors);
    }

    [Fact]
    public void PatternAndPointerScope_GuardsRefuseInput_AndRestoreThePreviousScope()
    {
        // No scope is a refusal (the desktop path always enters one); tests use an explicit test scope.
        Assert.Contains("No owned input scope", Assert.Throws<InvalidOperationException>(PointerBoundary.RequireInputSafe).Message);
        var policy = new ActionPolicy(new(1, "draft", []));
        using (PointerBoundary.Enter(null!, policy, () => { }))
        {
            PointerBoundary.RequireInputSafe();
            using (PointerBoundary.Enter(null!, policy, () => throw new InvalidOperationException("unproven instance")))
                Assert.Throws<InvalidOperationException>(PointerBoundary.RequireInputSafe);
            PointerBoundary.RequireInputSafe(); // the previous (passing) scope is restored
        }
        using (PointerBoundary.Enter(null!, policy))
            Assert.Contains("No input guard", Assert.Throws<InvalidOperationException>(PointerBoundary.RequireInputSafe).Message);
        Assert.Throws<InvalidOperationException>(PointerBoundary.RequireInputSafe);
    }
}
