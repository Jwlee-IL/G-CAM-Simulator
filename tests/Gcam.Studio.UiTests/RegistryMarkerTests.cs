using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class RegistryMarkerTests
{
    private sealed class FakeStore : IRegistryMarkerStore
    {
        public string NativeUserHive => @"\REGISTRY\USER\S-1-5-21-123-456-789-1001";
        public Dictionary<string, (string Name, string Data)> Values { get; } = [];
        public int Calls { get; private set; }
        public bool TryCreateNew(string key, string name, string data)
        {
            Calls++;
            return Values.TryAdd(key, (name, data));
        }
    }

    [Fact]
    public void Write_UsesRunScopedKeyAndSingleRunIdValue_WithFakeOnly()
    {
        var store = new FakeStore();
        var control = new RegistryMarker(store).Write("probe-01", 20);
        Assert.Equal(@"HKCU\Software\GcamUiaProbe\probe-01", control.KeyPath);
        Assert.Equal(store.NativeUserHive + @"\Software\GcamUiaProbe\probe-01", control.TraceKeyPath);
        Assert.Equal(("RunId", "probe-01"), store.Values[@"Software\GcamUiaProbe\probe-01"]);
        Assert.Equal(20, control.Pid);
        Assert.Equal("registry", control.Channel);
        Assert.True(control.FinishedAt >= control.StartedAt);
        Assert.Equal(1, store.Calls);
    }

    [Fact]
    public void Write_RefusesExistingRunWithoutOverwriteOrDeletion()
    {
        var store = new FakeStore();
        var writer = new RegistryMarker(store);
        writer.Write("probe-01", 20);
        Assert.Throws<IOException>(() => writer.Write("probe-01", 20));
        Assert.Single(store.Values);
        Assert.Equal(("RunId", "probe-01"), store.Values.Values.Single());
    }

    [Fact]
    public void Write_RejectsInvalidRunBeforeStoreAccess()
    {
        var store = new FakeStore();
        Assert.ThrowsAny<ArgumentException>(() => new RegistryMarker(store).Write(@"bad\key", 20));
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public void Network_IsNotApplicable_WithDecisionAndSourceEvidence()
    {
        var network = ProbeChannels.Network;
        Assert.Equal("not_applicable", network.Status);
        Assert.Equal("MR6", network.Decision);
        Assert.NotEmpty(network.SourceEvidence);
        Assert.DoesNotContain("blind spot", network.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
