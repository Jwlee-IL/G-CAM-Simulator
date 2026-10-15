using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Gcam.Studio.Core.Reporting;

/// <summary>Minimal OOXML report package; image rendering is supplied by the view layer.</summary>
public static class WordReportWriter
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static byte[] Write(SimulationReport report, Func<XElement, byte[]> png)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var body = new XElement(W + "body");
            var relationships = new XElement(Rel + "Relationships");
            relationships.Add(new XElement(Rel + "Relationship", new XAttribute("Id", "styles"),
                new XAttribute("Type", R.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml")));
            int imageId = 0;
            foreach (var block in ReportMarkup.Blocks(report))
            {
                if (block.Name.LocalName == "figure")
                {
                    var svg = block.Elements().Single(e => e.Name.LocalName == "svg");
                    string id = "image" + ++imageId;
                    Entry(zip, "word/media/" + id + ".png", png(svg));
                    relationships.Add(new XElement(Rel + "Relationship", new XAttribute("Id", id),
                        new XAttribute("Type", R.NamespaceName + "/image"), new XAttribute("Target", "media/" + id + ".png")));
                    body.Add(Paragraph(block.Element("figcaption")!.Value, keepNext: true));
                    body.Add(Picture(svg, id, imageId));
                }
                else if (block.Name.LocalName == "table")
                {
                    var rows = block.Descendants("tr").ToArray();
                    int columns = rows.Max(row => row.Elements().Count());
                    int columnWidth = 9906 / columns; // A4 text width in twips: page 11906 minus two 1000-twip margins
                    var table = new XElement(W + "tbl", new XElement(W + "tblPr",
                        new XElement(W + "tblStyle", new XAttribute(W + "val", "ReportTable"))),
                        new XElement(W + "tblGrid", Enumerable.Range(0, columns).Select(_ => new XElement(W + "gridCol", new XAttribute(W + "w", columnWidth)))));
                    foreach (var row in rows)
                        table.Add(new XElement(W + "tr", row.Elements().Select(cell => new XElement(W + "tc",
                            new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", columnWidth), new XAttribute(W + "type", "dxa"))),
                            Paragraph(cell.Value, bold: cell.Name.LocalName == "th")))));
                    body.Add(table);
                }
                else
                {
                    foreach (string line in Text(block).Split('\n'))
                        body.Add(Paragraph(line, block.Name.LocalName switch { "h1" => "Heading1", "h2" => "Heading2", "pre" => "Code", _ => "Normal" },
                            pageBreak: block.Attribute("class")?.Value == "appendix"));
                }
            }
            body.Add(new XElement(W + "sectPr", new XElement(W + "pgSz", new XAttribute(W + "w", 11906), new XAttribute(W + "h", 16838)),
                new XElement(W + "pgMar", new XAttribute(W + "top", 1000), new XAttribute(W + "bottom", 1000),
                    new XAttribute(W + "left", 1000), new XAttribute(W + "right", 1000))));
            Xml(zip, "word/document.xml", new XElement(W + "document", new XAttribute(XNamespace.Xmlns + "w", W), new XAttribute(XNamespace.Xmlns + "r", R), body));
            Xml(zip, "word/styles.xml", Styles());
            Xml(zip, "word/_rels/document.xml.rels", relationships);
            Xml(zip, "_rels/.rels", new XElement(Rel + "Relationships", new XElement(Rel + "Relationship",
                new XAttribute("Id", "document"), new XAttribute("Type", R.NamespaceName + "/officeDocument"), new XAttribute("Target", "word/document.xml"))));
            Xml(zip, "[Content_Types].xml", new XElement(Ct + "Types",
                new XElement(Ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(Ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(Ct + "Default", new XAttribute("Extension", "png"), new XAttribute("ContentType", "image/png")),
                new XElement(Ct + "Override", new XAttribute("PartName", "/word/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")),
                new XElement(Ct + "Override", new XAttribute("PartName", "/word/document.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"))));
        }
        return stream.ToArray();
    }

    public static string Text(XElement block) => string.Concat(block.Nodes().Select(n => n is XElement { Name.LocalName: "br" } ? "\n" : n is XElement e ? e.Value : ((XText)n).Value));

    private static XElement Paragraph(string text, string style = "Normal", bool bold = false, bool keepNext = false, bool pageBreak = false) => new(W + "p",
        new XElement(W + "pPr", new XElement(W + "pStyle", new XAttribute(W + "val", style)),
            keepNext ? new XElement(W + "keepNext") : null, pageBreak ? new XElement(W + "pageBreakBefore") : null),
        new XElement(W + "r", bold ? new XElement(W + "rPr", new XElement(W + "b")) : null,
            new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));

    private static XElement Styles()
    {
        XElement Value(string name, object value) => new(W + name, new XAttribute(W + "val", value));
        XElement Font(string name, int halfPoints) => new(W + "rPr",
            new XElement(W + "rFonts", new XAttribute(W + "ascii", name), new XAttribute(W + "hAnsi", name)),
            Value("sz", halfPoints), Value("szCs", halfPoints));
        XElement Heading(string id, string name, int level, int size) => new(W + "style",
            new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", id), Value("name", name),
            Value("basedOn", "Normal"), Value("next", "Normal"), new XElement(W + "qFormat"),
            new XElement(W + "pPr", new XElement(W + "keepNext"), new XElement(W + "keepLines"),
                new XElement(W + "spacing", new XAttribute(W + "before", 200), new XAttribute(W + "after", 100)), Value("outlineLvl", level)),
            new XElement(W + "rPr", new XElement(W + "b"), Value("sz", size)));
        return new XElement(W + "styles",
            new XElement(W + "style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "default", 1),
                new XAttribute(W + "styleId", "Normal"), Value("name", "Normal"), new XElement(W + "qFormat"),
                new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", 80))), Font("Arial", 18)),
            Heading("Heading1", "Heading 1", 0, 32), Heading("Heading2", "Heading 2", 1, 28),
            new XElement(W + "style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", "Code"),
                Value("name", "Code"), Value("basedOn", "Normal"),
                new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", 0),
                    new XAttribute(W + "line", 200), new XAttribute(W + "lineRule", "exact"))), Font("Consolas", 15)),
            new XElement(W + "style", new XAttribute(W + "type", "table"), new XAttribute(W + "styleId", "ReportTable"),
                Value("name", "Report table"), new XElement(W + "tblPr", new XElement(W + "tblBorders",
                    new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(side =>
                        new XElement(W + side, new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 4)))))));
    }

    private static XElement Picture(XElement svg, string id, int number)
    {
        XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
        var size = svg.Attribute("viewBox")!.Value.Split(' ').Select(s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        const long cx = 5400000; long cy = (long)(cx * size[3] / size[2]);
        var picture = new XElement(pic + "pic",
            new XElement(pic + "nvPicPr", new XElement(pic + "cNvPr", new XAttribute("id", number), new XAttribute("name", id)), new XElement(pic + "cNvPicPr")),
            new XElement(pic + "blipFill", new XElement(a + "blip", new XAttribute(R + "embed", id)), new XElement(a + "stretch", new XElement(a + "fillRect"))),
            new XElement(pic + "spPr", new XElement(a + "xfrm", new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(a + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))),
                new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst"))));
        return new XElement(W + "p", new XElement(W + "r", new XElement(W + "drawing", new XElement(wp + "inline",
            new XElement(wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)),
            new XElement(wp + "docPr", new XAttribute("id", number), new XAttribute("name", id)),
            new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", pic.NamespaceName), picture))))));
    }

    private static void Xml(ZipArchive zip, string path, XElement element) => Entry(zip, path, Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>" + element.ToString(SaveOptions.DisableFormatting)));
    private static void Entry(ZipArchive zip, string path, byte[] bytes)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var target = entry.Open(); target.Write(bytes);
    }
}
