using System.Xml.Linq;
using Gcam.Studio.UiTests.Harness;

namespace Gcam.Studio.UiTests;

public sealed class TracingToolingTests
{
    [Fact]
    public void PreparedProfile_ReferencesDeclaredCollectorsAndRequiredChannels()
    {
        string root = Path.Combine(RepoPaths.Root(), "tests/Gcam.Studio.UiTests/Harness/Isolation");
        var doc = XDocument.Load(Path.Combine(root, "Probe.wprp"));
        var keywords = doc.Descendants("Keyword").Select(e => e.Attribute("Value")!.Value).ToHashSet();
        foreach (string keyword in new[] { "ProcessThread", "Loader", "FileIO", "FileIOInit", "Registry", "NetworkTrace" }) Assert.Contains(keyword, keywords);
        string collector = doc.Descendants("SystemCollector").Single().Attribute("Id")!.Value;
        Assert.Equal(collector, doc.Descendants("SystemCollectorId").Single().Attribute("Value")!.Value);
        string wrapper = File.ReadAllText(Path.Combine(root, "Trace-Probe.ps1"));
        Assert.DoesNotContain("wpr -cancel", wrapper);
        Assert.Contains("'-instancename', $instance", wrapper);
        Assert.Contains("'-recordtempto', $destination", wrapper);
        Assert.Contains("WaitForExit", wrapper);
        Assert.Contains("finally", wrapper);
        Assert.Contains("Administrator", wrapper);
    }
}
