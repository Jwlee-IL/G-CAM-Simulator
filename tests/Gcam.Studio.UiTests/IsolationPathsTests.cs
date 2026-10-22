using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class IsolationPathsTests
{
    internal static string Scratch() => IsolationPaths.RunRoot(Path.GetTempPath(), "gcam-fixture-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void V4RunIds_RequireProductPrefix_WhileLegacyPathsKeepTheirIdsAndLengthLimits()
    {
        const string old = "probe-20261016-03", current = "gcam-probe-20261022-01";
        IsolationPaths.ValidateRunId(current);
        Assert.Throws<ArgumentException>(() => IsolationPaths.RunRoot("scratch", old, v4: true));
        Assert.EndsWith("gcam-uia-" + old, IsolationPaths.RunRoot("scratch", old, v4: false));
        IsolationPaths.ValidateRunId("gcam-" + new string('a', 91));
        Assert.Throws<ArgumentException>(() => IsolationPaths.ValidateRunId("gcam-" + new string('a', 92)));
    }

    [Fact]
    public void LaunchValues_RefuseExtraWrongOrEscapingValues()
    {
        string root = Scratch();
        var values = IsolationPaths.LaunchValues(root, "fixture").ToDictionary(v => v.Key, v => v.Value);
        IsolationPaths.ValidateLaunchValues(root, "fixture", values);
        values["HTTP_PROXY"] = "unapproved";
        Assert.Throws<ArgumentException>(() => IsolationPaths.ValidateLaunchValues(root, "fixture", values));
        values.Remove("HTTP_PROXY"); values["TMP"] = Path.GetTempPath();
        Assert.Throws<ArgumentException>(() => IsolationPaths.ValidateLaunchValues(root, "fixture", values));
        Assert.Throws<ArgumentException>(() => IsolationPaths.Inside(root, "../foreign"));
        Assert.Throws<ArgumentException>(() => IsolationPaths.RunRoot(root, "../foreign"));
        OwnedStudioProcess.ValidatePrivilege(false);
        Assert.Throws<InvalidOperationException>(() => OwnedStudioProcess.ValidatePrivilege(true));
    }
    [Fact]
    public void DeviceForm_MapsDriveLetterRootsToTraceDevicePaths_AndRefusesUnmappedDrives()
    {
        Func<string, string?> volumes = drive => drive == "C:" ? @"\Device\HarddiskVolume7" : drive == "S:" ? @"\??\C:\subst" : null;
        Assert.Equal(@"\Device\HarddiskVolume7\Users\fixture\AppData\Local\Temp\gcam-uia-run",
            IsolationPaths.DeviceForm(@"C:\Users\fixture\AppData\Local\Temp\gcam-uia-run", volumes));
        Assert.Equal(@"\Device\HarddiskVolume7\ProgramData", IsolationPaths.DeviceForm(@"c:\ProgramData", volumes));
        Assert.Null(IsolationPaths.DeviceForm(@"S:\fixture", volumes)); // SUBST target is not a device name
        Assert.Null(IsolationPaths.DeviceForm(@"D:\fixture", volumes)); // unmapped drive
        Assert.Null(IsolationPaths.DeviceForm(@"\\server\share\fixture", volumes)); // UNC is not a drive-letter root
    }
    [Fact]
    public void Fingerprint_ChangesWithContentsAndNames()
    {
        string root = Scratch(); Directory.CreateDirectory(root);
        string before = IsolationPaths.Fingerprint(root);
        File.WriteAllText(IsolationPaths.Inside(root, "a.txt"), "fixture");
        string first = IsolationPaths.Fingerprint(root); Assert.NotEqual(before, first);
        File.WriteAllText(IsolationPaths.Inside(root, "a.txt"), "changed");
        Assert.NotEqual(first, IsolationPaths.Fingerprint(root));
    }
}
