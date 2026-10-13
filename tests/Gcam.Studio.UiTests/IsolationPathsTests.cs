using Gcam.Studio.UiTests.Harness.Isolation;

namespace Gcam.Studio.UiTests;

public sealed class IsolationPathsTests
{
    internal static string Scratch() => IsolationPaths.RunRoot(Path.GetTempPath(), "fixture-" + Guid.NewGuid().ToString("N"));
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
