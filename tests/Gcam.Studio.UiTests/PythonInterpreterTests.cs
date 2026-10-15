using System.Text.Json;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class PythonInterpreterTests
{
    private static string Executable => Path.Combine(Path.GetTempPath(), "gcam-python-fixture", "python.exe");
    private static PythonInterpreter.Result Identity => new(0, JsonSerializer.Serialize(new { executable = Executable, major = 3 }), "");
    [Fact]
    public void WorkingPath_UsesResolvedExecutable_AndValidatesIt()
    {
        var calls = new List<string>();
        string result = PythonInterpreter.Resolve((cmd, _) => { calls.Add(cmd); return cmd == "python" ? Identity : new(0, "3\n", ""); });
        Assert.Equal(Executable, result); Assert.Equal(new[] { "python", Executable }, calls);
    }
    [Fact]
    public void FailedAlias_FallsBackToLauncher_WithExplicitVersionPrefix()
    {
        var calls = new List<string>();
        string result = PythonInterpreter.Resolve((cmd, args) =>
        {
            calls.Add(cmd);
            if (cmd == "python") throw new UnauthorizedAccessException("alias refused");
            if (cmd == "py") { Assert.Equal("-3", args[0]); return Identity; }
            return new(0, "3", "");
        });
        Assert.Equal(Executable, result); Assert.Equal(new[] { "python", "py", Executable }, calls);
    }
    [Fact]
    public void ExplicitOverride_HasPriority_WithoutAnInstalledLauncher()
    {
        var calls = new List<string>();
        Assert.Equal(Executable, PythonInterpreter.Resolve((cmd, args) => { calls.Add(cmd); return args.Contains("-c") && args[^1].Contains("json") ? Identity : new(0, "3", ""); }, Executable));
        Assert.All(calls, c => Assert.Equal(Executable, c));
    }
    [Theory]
    [InlineData("bad-json")]
    [InlineData("exit")]
    [InlineData("timeout")]
    public void UnusableCandidates_FailActionably_WithoutSkippingFingerprint(string fault)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => PythonInterpreter.Resolve((_, _) => fault switch
        {
            "bad-json" => new(0, "not an identity", ""),
            "exit" => new(5, "", "refused"),
            _ => throw new TimeoutException("bounded probe")
        }));
        Assert.Contains("GCAM_TEST_PYTHON", failure.Message); Assert.Contains("python:", failure.Message); Assert.Contains("py:", failure.Message);
    }
}
