using System.Text;
using System.Xml.Linq;

namespace Gcam.Studio.Core.Reporting;

/// <summary>The report writer's own restricted markup, shared by PDF and Word. SVG is the common figure data.</summary>
public static class ReportMarkup
{
    public static XElement Body(SimulationReport report)
    {
        string html = Encoding.UTF8.GetString(ReportWriter.Write(report).Html);
        int start = html.IndexOf("<body>", StringComparison.Ordinal);
        int end = html.IndexOf("</body>", StringComparison.Ordinal) + "</body>".Length;
        return XElement.Parse(html[start..end].Replace("<br>", "<br />", StringComparison.Ordinal));
    }

    public static IEnumerable<XElement> Blocks(SimulationReport report)
    {
        XElement? configuration = null;
        foreach (var block in Body(report).Elements())
        {
            if (block.Name.LocalName == "details")
            {
                configuration = block.Element("pre");
                yield return new XElement("p", "Acquired configuration: see the appendix at the end of this report.");
            }
            else yield return block;
        }
        if (configuration is not null)
        {
            yield return new XElement("h2", new XAttribute("class", "appendix"), "Appendix — acquired configuration");
            yield return configuration;
        }
    }
}
