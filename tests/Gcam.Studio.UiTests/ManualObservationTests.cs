using System.Text.Json;
using Gcam.Studio.UiTests.Harness;
using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

/// <summary>The optional observed manual batch: the diff-classify context and the owned-folder rule, without a desktop.</summary>
public sealed class ManualObservationTests
{
    [Fact]
    public void WithoutTheVariable_TheFixtureDoesNothing()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ManualObservation.DirectoryVariable))) return;   // observed runs only
        using var fixture = new ManualObservation();
        Assert.Null(fixture.RunDirectory);
        Assert.Null(ManualObservation.Active);
    }

    [Fact]
    public void Context_HasTheFieldsDiffClassifyReads_WithTheWindowFromFirstLaunchToLastExit()
    {
        var t0 = new DateTimeOffset(2026, 10, 15, 1, 0, 0, TimeSpan.Zero);
        var launches = new[]
        {
            new ManualObservation.Launch("run-b", t0.AddSeconds(40), t0.AddSeconds(100), 22, t0.AddSeconds(41)),
            new ManualObservation.Launch("run-a", t0.AddSeconds(20), t0.AddSeconds(35), 11, t0.AddSeconds(21)),
            new ManualObservation.Launch("run-c", t0.AddSeconds(110), t0.AddSeconds(115), null, null),   // never reached its window
        };
        var context = JsonSerializer.SerializeToElement(ManualObservation.Context("probe-20261015-01", t0, t0.AddSeconds(12), [], launches,
            [@"C:\scratch\run", @"C:\scratch\gcam-uia-run-a"], @"C:\scratch\gcam-uia-run-a\probe-diagnostic.txt", true),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(12, context.GetProperty("nullWindow").GetProperty("seconds").GetDouble());
        var window = context.GetProperty("studioWindow");
        Assert.Equal(t0.AddSeconds(20).ToString("O"), window.GetProperty("launchAt").GetString());
        Assert.Equal(t0.AddSeconds(115).ToString("O"), window.GetProperty("exitAt").GetString());
        Assert.Equal(95, window.GetProperty("seconds").GetDouble());
        Assert.Equal((1, 2), (window.GetProperty("marginBeforeSeconds").GetInt32(), window.GetProperty("marginAfterSeconds").GetInt32()));
        Assert.Equal(new[] { 22, 11 }, context.GetProperty("ownedProcesses").EnumerateArray().Select(p => p.GetProperty("pid").GetInt32()));
        Assert.All(context.GetProperty("ownedProcesses").EnumerateArray(), p => Assert.Equal("Gcam.Studio.exe", p.GetProperty("image").GetString()));
        var owned = context.GetProperty("harnessOwned");
        Assert.Equal(2, owned.GetProperty("files").GetArrayLength());
        string key = owned.GetProperty("registry")[0].GetString()!;
        Assert.Equal(@"HKCU\" + RegistryMarker.RelativeRoot + @"\probe-20261015-01", key);
        var controls = context.GetProperty("positiveControls");
        Assert.EndsWith(ManualObservation.DiagnosticFile, controls.GetProperty("file").GetString());
        Assert.Equal((key, RegistryMarker.ValueName, true), (controls.GetProperty("registry").GetProperty("key").GetString(),
            controls.GetProperty("registry").GetProperty("valueName").GetString(), controls.GetProperty("registry").GetProperty("written").GetBoolean()));
        Assert.Equal("Gcam.Studio.exe", controls.GetProperty("process").GetString());
        Assert.Equal(("network", "not_applicable"), (context.GetProperty("channelApplicability")[0].GetProperty("channel").GetString(), context.GetProperty("channelApplicability")[0].GetProperty("status").GetString()));
    }

    [Fact]
    public void OwnedFolders_AreTheBatchFoldersWithAHarnessOwnerMarker()
    {
        string root = IsolationPathsTests.Scratch();
        Directory.CreateDirectory(root);
        var since = DateTimeOffset.UtcNow;
        string sandbox = Directory.CreateDirectory(Path.Combine(root, "gcam-uia-run-a")).FullName;
        File.WriteAllText(Path.Combine(sandbox, ".gcam-uia-owner"), "run-a\n");
        string record = Directory.CreateDirectory(Path.Combine(root, "gcam-uia-record-run-a")).FullName;
        File.WriteAllText(Path.Combine(record, ".gcam-record-owner"), "run-a\n");
        string unmarked = Directory.CreateDirectory(Path.Combine(root, "gcam-uia-foreign")).FullName;
        string older = Directory.CreateDirectory(Path.Combine(root, "gcam-uia-older")).FullName;
        File.WriteAllText(Path.Combine(older, ".gcam-uia-owner"), "older\n");
        Directory.SetCreationTimeUtc(older, since.UtcDateTime.AddHours(-1));
        Directory.CreateDirectory(Path.Combine(root, "other-tool"));
        Assert.Equal(new[] { record, sandbox }.Order(StringComparer.Ordinal), ManualObservation.OwnedFolders(root, since));
        Assert.DoesNotContain(unmarked, ManualObservation.OwnedFolders(root, since));
    }
}
