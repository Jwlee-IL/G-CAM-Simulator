using System.IO.Compression;
using System.Xml.Linq;
using Gcam.Studio.Core.Reporting;

namespace Gcam.Studio.Tests;

public sealed class WordReportWriterTests
{
    [Fact]
    public void Write_ValidOoxmlPartsRelationshipsAndContent_AreDeterministic()
    {
        var report = ReportWriterTests.Report() with { Version = "version <test> & commit" };
        byte[] Pixel(System.Xml.Linq.XElement _) => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9j0AAAAASUVORK5CYII=");
        byte[] bytes = WordReportWriter.Write(report, Pixel);
        Assert.Equal(bytes, WordReportWriter.Write(report, Pixel));
        using var zip = new ZipArchive(new MemoryStream(bytes));
        XDocument Xml(string path) { using var entry = zip.GetEntry(path)!.Open(); return XDocument.Load(entry); }
        var document = Xml("word/document.xml");
        foreach (string text in new[] { "Scene", "Acquisition", "Imaging", "Spectrum", "Imaging measurements", report.Version, "12.3 mm" })
            Assert.Contains(text, document.Root!.Value);
        Assert.Equal(3, zip.Entries.Count(e => e.FullName.EndsWith(".png")));
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = document.Descendants(w + "p").ToArray();
        int appendix = Array.FindIndex(paragraphs, p => p.Value == "Appendix — acquired configuration");
        Assert.True(appendix > Array.FindIndex(paragraphs, p => p.Value == "Imaging measurements"));
        Assert.NotNull(paragraphs[appendix].Element(w + "pPr")!.Element(w + "pageBreakBefore"));
        Assert.All(paragraphs.Skip(appendix + 1), p => Assert.Equal("Code", p.Element(w + "pPr")!.Element(w + "pStyle")!.Attribute(w + "val")!.Value));
        var styles = Xml("word/styles.xml");
        XElement Style(string id) => styles.Descendants(w + "style").Single(s => s.Attribute(w + "styleId")!.Value == id);
        Assert.Equal("0", Style("Heading1").Descendants(w + "outlineLvl").Single().Attribute(w + "val")!.Value);
        Assert.Equal("1", Style("Heading2").Descendants(w + "outlineLvl").Single().Attribute(w + "val")!.Value);
        Assert.Equal("200", Style("Heading2").Descendants(w + "spacing").Single().Attribute(w + "before")!.Value);
        Assert.Equal("Consolas", Style("Code").Descendants(w + "rFonts").Single().Attribute(w + "ascii")!.Value);
        Assert.Equal("15", Style("Code").Descendants(w + "sz").Single().Attribute(w + "val")!.Value);
        Assert.Equal(6, Style("ReportTable").Descendants(w + "tblBorders").Single().Elements().Count());
        Assert.All(paragraphs.Where(p => p.Descendants(w + "drawing").Any()), picture =>
            Assert.NotNull(picture.PreviousNode is XElement caption ? caption.Element(w + "pPr")?.Element(w + "keepNext") : null));
        Assert.NotEmpty(document.Descendants(w + "tbl"));
        foreach (var table in document.Descendants(w + "tbl"))
        {
            Assert.Equal("ReportTable", table.Element(w + "tblPr")!.Element(w + "tblStyle")!.Attribute(w + "val")!.Value);
            int columns = table.Element(w + "tblGrid")!.Elements(w + "gridCol").Count();
            Assert.All(table.Elements(w + "tr"), row => Assert.Equal(columns, row.Elements(w + "tc").Count()));
        }
        XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
        foreach (var r in Xml("word/_rels/document.xml.rels").Descendants(rel + "Relationship"))
            Assert.NotNull(zip.GetEntry("word/" + r.Attribute("Target")!.Value));
        Assert.Contains("word/document.xml", Xml("_rels/.rels").Root!.Value + Xml("_rels/.rels").ToString());
        Assert.Contains("wordprocessingml.document.main+xml", Xml("[Content_Types].xml").ToString());
        Assert.Contains("wordprocessingml.styles+xml", Xml("[Content_Types].xml").ToString());
        Assert.Contains(Xml("word/_rels/document.xml.rels").Descendants(rel + "Relationship"), r =>
            r.Attribute("Type")!.Value.EndsWith("/styles") && r.Attribute("Target")!.Value == "styles.xml");
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels")))
        { using var stream = entry.Open(); Assert.NotNull(XDocument.Load(stream).Root); }
    }
}
