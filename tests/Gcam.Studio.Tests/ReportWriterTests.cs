using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;

namespace Gcam.Studio.Tests;

public sealed class ReportWriterTests
{
    [Fact]
    public void Write_IdenticalInputsGiveIdenticalBytes_AndAllSections()
    {
        var report = Report();
        var first = ReportWriter.Write(report);
        var second = ReportWriter.Write(report);
        Assert.Equal(first.Html, second.Html);
        Assert.Equal(first.SceneJson, second.SceneJson);
        string html = Encoding.UTF8.GetString(first.Html);
        foreach (string section in new[] { "Scene", "Acquisition", "Imaging", "Spectrum", "Imaging measurements" })
            Assert.Contains($"<h2>{section}</h2>", html);
        Assert.Contains("Simulation — not a measurement", html);
        Assert.Contains("+09:00", html);
        Assert.Contains("2026-10-14T03:00:00", html);
        Assert.Contains("1.0.0+test-commit", html);
        Assert.Contains("M1", html);
        Assert.Contains("12.3 mm", html);
    }

    [Fact]
    public void Write_EscapesUserText_AndSvgHasNoExternalReferences()
    {
        string html = Encoding.UTF8.GetString(ReportWriter.Write(Report() with
        {
            Version = "<script>alert('x')</script>",
            Measurements = [new("M1", "Flood", "ROI", "<img src=x>", "a & b")]
        }).Html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("a &amp; b", html);
        var svgs = Regex.Matches(html, "<svg.*?</svg>", RegexOptions.Singleline);
        Assert.Equal(3, svgs.Count);
        foreach (Match match in svgs)
        {
            var svg = XElement.Parse(match.Value);
            Assert.Equal("svg", svg.Name.LocalName);
            Assert.DoesNotContain(svg.DescendantsAndSelf().Attributes(), a => a.Name.LocalName is "href" or "src");
            Assert.DoesNotContain(svg.Descendants(), e => e.Name.LocalName is "script" or "image" or "foreignObject");
        }
        Assert.Contains("661.7 keV", html);
        Assert.Contains("Estimate</title>", html);
    }

    [Fact]
    public void Write_UsesStudioColormapAndBottomUpRows()
    {
        var image = new DetectorImage(1, 2);
        image[0, 0] = 0; image[0, 1] = 10;
        var report = Report();
        string html = Encoding.UTF8.GetString(ReportWriter.Write(report with
        { Image = report.Image! with { Flood = image, Reconstruction = null } }).Html);
        var svg = XElement.Parse(Regex.Match(html, "<svg.*?</svg>", RegexOptions.Singleline).Value);
        var cells = svg.Descendants().Where(e => e.Name.LocalName == "rect").Take(2).ToArray();
        Assert.Equal("210", cells[0].Attribute("y")!.Value);
        Assert.Equal("30", cells[1].Attribute("y")!.Value);
        Assert.Equal("#440154", cells[0].Attribute("fill")!.Value);
        Assert.Equal("#FDE725", cells[1].Attribute("fill")!.Value);
    }

    [Fact]
    public void Write_SceneRoundTripsThroughConfigLoader_WithPhysicalReadoutNotice()
    {
        var config = new SimulationConfig { Seed = 73 };
        config.Detector.Readout = new() { Mode = ReadoutMode.FourOutputAnger };
        var files = ReportWriter.Write(Report() with { ConfigurationJson = ConfigLoader.Serialize(config), PhysicalReadout = true });
        string folder = Path.Combine(Path.GetTempPath(), "gcam-todo46", "impl1", "roundtrip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "report.scene.json");
        File.WriteAllBytes(path, files.SceneJson);
        var loaded = ConfigLoader.Load(path);
        Assert.Equal(73, loaded.Seed);
        Assert.Equal(ReadoutMode.FourOutputAnger, loaded.Detector.Readout!.Mode);
        Assert.Equal(files.SceneJson, Encoding.UTF8.GetBytes(ConfigLoader.Serialize(loaded)));
        string html = Encoding.UTF8.GetString(files.Html);
        Assert.Contains("captured calibration bank", html);
        Assert.Contains("ordinary CLI does not run this mode", html);
    }

    [Fact]
    public void Write_MissingPublishedViewsAreExplicit()
    {
        string html = Encoding.UTF8.GetString(ReportWriter.Write(Report() with { Image = null, Spectrum = null }).Html);
        Assert.Contains("Image unavailable", html);
        Assert.Contains("Spectrum unavailable", html);
    }

    internal static SimulationReport Report()
    {
        var flood = new DetectorImage(2, 2); flood[0, 0] = 10;
        var image = new ImagingResult(flood, -1, 2, flood, -1, 2,
            new SourceEstimate(new Vector3(1, -1, 0), 2.5), 10, TimeSpan.Zero);
        var band = new SpectrumBand([new("Cs-137", 661.7)], 650, 675, 10, 1);
        var spectrum = new SpectrumView([250, 750], [0, 10], [band], 10, 0, 1, .05, 1e-6, "chain", TimeSpan.Zero)
        { BinEdgesKeV = [0, 500, 1000] };
        return new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(9)), "1.0.0+test-commit",
            ConfigLoader.Serialize(new SimulationConfig()), false, [new("Source", "Cs-137")], [new("Counts", "10")],
            image, [], [new("Estimate", "peak (1.0, -1.0) mm")], spectrum, true, [new("Summary", "10 pulses")],
            [new("M1", "Flood", "Distance", "12.3 mm", "endpoints")]);
    }
}
