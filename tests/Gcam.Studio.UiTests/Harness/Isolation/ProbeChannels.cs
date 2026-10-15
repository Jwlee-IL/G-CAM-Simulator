namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>MR6 excludes a network observation requirement, without ignoring any observed traffic.</summary>
public static class ProbeChannels
{
    public sealed record Applicability(string Channel, string Status, string Decision, string Reason, string[] SourceEvidence);
    public static Applicability Network => new("network", "not_applicable", "MR6",
        "Current Studio source has no network code or authorised external connection",
        ["src/Gcam.Studio/App.xaml.cs:17", "src/Gcam.Studio.Services/SimulationService.cs:105",
            "tests/Gcam.Studio.UiTests/Harness/Isolation/surface-list.json#app-network-process"]);
}
