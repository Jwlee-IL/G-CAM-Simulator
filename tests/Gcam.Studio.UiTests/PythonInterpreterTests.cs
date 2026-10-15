using System.Text.Json;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class PythonInterpreterTests
{
    private static string Executable => Path.Combine(Path.GetTempPath(), "gcam-python-fixture", "python.exe");
    private static PythonInterpreter.Result Identity => new(0, JsonSerializer.Serialize(new { executable = Executable, major = 3 }), "");
    private static string Packaged => Path.Combine(Path.GetTempPath(), "WindowsApps", "PythonSoftwareFoundation.Python.3.11_x", "python.exe");
    private static PythonInterpreter.Result IdentityOf(string path) => new(0, JsonSerializer.Serialize(new { executable = path, major = 3 }), "");
    [Fact]
    public void Launcher_ComesFirst_AndItsExecutableIsValidated()
    {
        var calls = new List<string>();
        string result = PythonInterpreter.Resolve((cmd, args) =>
        {
            calls.Add(cmd);
            if (cmd == "py") { Assert.Equal("-3", args[0]); return Identity; }
            return new(0, "3\n", "");
        });
        Assert.Equal(Executable, result); Assert.Equal(new[] { "py", Executable }, calls);
    }
    [Fact]
    public void MissingLauncher_FallsBackToPython()
    {
        var calls = new List<string>();
        string result = PythonInterpreter.Resolve((cmd, _) =>
        {
            calls.Add(cmd);
            if (cmd == "py") throw new System.ComponentModel.Win32Exception("not found");
            return cmd == "python" ? Identity : new(0, "3", "");
        });
        Assert.Equal(Executable, result); Assert.Equal(new[] { "py", "python", Executable }, calls);
    }
    [Fact]
    public void StorePackage_IsTheLastResort()
    {
        // Seen 2026-10-16: the Store package executable passed the probe, then refused the next start ("access denied").
        string first = PythonInterpreter.Resolve((cmd, _) => cmd switch
        {
            "py" => IdentityOf(Packaged), "python" => Identity, _ => new(0, "3", "")
        });
        Assert.Equal(Executable, first);
        string only = PythonInterpreter.Resolve((cmd, _) => cmd switch
        {
            "py" => throw new System.ComponentModel.Win32Exception("not found"), "python" => IdentityOf(Packaged), _ => new(0, "3", "")
        });
        Assert.Equal(Packaged, only);
    }
    [Fact]
    public void ExplicitOverride_IsExclusive_AndFailsByName()
    {
        var calls = new List<string>();
        Assert.Equal(Executable, PythonInterpreter.Resolve((cmd, args) => { calls.Add(cmd); return args.Contains("-c") && args[^1].Contains("json") ? Identity : new(0, "3", ""); }, Executable));
        Assert.All(calls, c => Assert.Equal(Executable, c));
        calls.Clear();
        var failure = Assert.Throws<InvalidOperationException>(() => PythonInterpreter.Resolve((cmd, _) => { calls.Add(cmd); return new(9, "", "broken"); }, Executable));
        Assert.Equal(new[] { Executable }, calls); Assert.Contains("GCAM_TEST_PYTHON", failure.Message);
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
