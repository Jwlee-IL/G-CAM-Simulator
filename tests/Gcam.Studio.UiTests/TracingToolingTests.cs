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
        foreach (string keyword in new[] { "ProcessThread", "Loader" }) Assert.Contains(keyword, keywords);
        foreach (string unused in new[] { "FileIO", "FileIOInit", "Registry", "NetworkTrace" }) Assert.DoesNotContain(unused, keywords);
        var fileIds = doc.Descendants("EventProvider").Single(e => e.Attribute("Id")!.Value == "FileEffects")
            .Descendants("EventId").Select(e => e.Attribute("Value")!.Value).ToHashSet();
        Assert.Contains("68", fileIds); Assert.Contains("69", fileIds); Assert.Contains("71", fileIds);
        Assert.DoesNotContain("67", fileIds); // read is unnecessary for an effect audit
        var registryIds = doc.Descendants("EventProvider").Single(e => e.Attribute("Id")!.Value == "RegistryEffects")
            .Descendants("EventId").Select(e => e.Attribute("Value")!.Value).ToHashSet();
        Assert.Contains("5", registryIds); Assert.Contains("6", registryIds); Assert.DoesNotContain("7", registryIds);
        Assert.Empty(doc.Descendants("Stacks"));
        string collector = doc.Descendants("SystemCollector").Single().Attribute("Id")!.Value;
        Assert.Equal(collector, doc.Descendants("SystemCollectorId").Single().Attribute("Value")!.Value);
        string wrapper = File.ReadAllText(Path.Combine(root, "Trace-Probe.ps1"));
        Assert.DoesNotContain("wpr -cancel", wrapper);
        Assert.Contains("'-instancename', $instance", wrapper);
        Assert.Contains("'-recordtempto', $destination", wrapper);
        Assert.Contains("WaitForExit", wrapper);
        Assert.Contains("finally", wrapper);
        Assert.Contains("Administrator", wrapper);
        Assert.StartsWith("#Requires -Version 7.0", wrapper);
    }
}
