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
        var keywords = doc.Descendants("SystemProvider").Single().Descendants("Keyword").Select(e => e.Attribute("Value")!.Value).ToHashSet();
        Assert.True(keywords.SetEquals(new[] { "ProcessThread", "Loader", "FileIO", "FileIOInit", "Filename", "Registry" }));
        // E4 (RC-05): only Microsoft-Windows-Kernel-Registry with the SetInformationKey keyword; classic opcodes are not Event IDs.
        var provider = Assert.Single(doc.Descendants("EventProvider"));
        Assert.Equal("70eb4f03-c1de-4f73-a051-33d13d5413bd", provider.Attribute("Name")!.Value);
        Assert.Equal(new[] { "0x40" }, provider.Descendants("Keyword").Select(e => e.Attribute("Value")!.Value));
        Assert.Equal(provider.Attribute("Id")!.Value, doc.Descendants("EventProviderId").Single().Attribute("Value")!.Value);
        Assert.Equal(doc.Descendants("EventCollector").Single().Attribute("Id")!.Value,
            doc.Descendants("EventCollectorId").Single().Attribute("Value")!.Value);
        Assert.Empty(doc.Descendants("EventFilters"));
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
