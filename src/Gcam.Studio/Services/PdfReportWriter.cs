using System.IO;
using System.Xml.Linq;
using Gcam.Studio.Core.Reporting;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Gcam.Studio.Services;

/// <summary>PDFsharp text/tables and vector figures over the exact report markup used by HTML and Word.</summary>
public static class PdfReportWriter
{
    public static byte[] Write(SimulationReport report)
    {
        using var document = new PdfDocument();
        document.Info.Title = "GCAM Studio simulation report";
        document.Info.Creator = report.Version;
        document.Info.CreationDate = report.ExportedAt.UtcDateTime;
        XGraphics? graphics = null; double y = 0;
        const double left = 40, available = 515, bottom = 790;
        void Page() { graphics?.Dispose(); var page = document.AddPage(); page.Size = PdfSharp.PageSize.A4; graphics = XGraphics.FromPdfPage(page); y = 40; }
        IReadOnlyList<string> Wrap(string text, double width, XFont font)
            => ReportTextWrapping.Wrap(text, width, value => graphics!.MeasureString(value, font).Width);
        void Text(string text, bool heading = false, bool code = false)
        {
            var font = new XFont(code ? "Consolas" : "Arial", code ? 7.5 : heading ? 14 : 9, heading ? XFontStyleEx.Bold : XFontStyleEx.Regular);
            double lineHeight = code ? 10 : heading ? 22 : 14;
            if (heading)
            {
                if (y + 10 + lineHeight + 28 > bottom) Page();
                if (y > 40) y += 10;
            }
            foreach (string line in Wrap(text, available, font))
            {
                if (y + lineHeight > bottom) Page();
                graphics!.DrawString(line, font, XBrushes.Black, left, y + (code ? 8 : 12));
                y += lineHeight;
            }
        }
        Page();
        foreach (var block in ReportMarkup.Blocks(report))
        {
            if (block.Name.LocalName == "figure")
            {
                var svg = block.Elements().Single(e => e.Name.LocalName == "svg");
                var size = ReportFigureRenderer.Size(svg); double scale = available / size[2];
                string caption = block.Element("figcaption")!.Value;
                double captionHeight = Wrap(caption, available, new XFont("Arial", 9)).Count * 14;
                if (y + size[3] * scale + captionHeight + 15 > bottom) Page();
                Text(caption);
                var state = graphics!.Save(); graphics.TranslateTransform(left, y); graphics.ScaleTransform(scale);
                Figure(graphics, svg); graphics.Restore(state); y += size[3] * scale + 15;
            }
            else if (block.Name.LocalName == "table")
            {
                foreach (var row in block.Descendants("tr"))
                {
                    var cells = row.Elements().ToArray();
                    var font = new XFont("Arial", 9);
                    double[] widths = cells.Length == 2 ? [145, available - 145] : Enumerable.Repeat(available / cells.Length, cells.Length).ToArray();
                    var lines = cells.Select((cell, i) => Wrap(cell.Value, widths[i] - 8, font)).ToArray();
                    double height = lines.Max(c => c.Count) * 14 + 8;
                    if (y + height > bottom) Page();
                    double x = left;
                    for (int i = 0; i < cells.Length; i++)
                    {
                        graphics!.DrawRectangle(XPens.LightGray, x, y, widths[i], height);
                        for (int line = 0; line < lines[i].Count; line++) graphics.DrawString(lines[i][line], font, XBrushes.Black, x + 4, y + 14 + line * 14);
                        x += widths[i];
                    }
                    y += height;
                }
                y += 8;
            }
            else
            {
                if (block.Attribute("class")?.Value == "appendix" && y > 40) Page();
                Text(WordReportWriter.Text(block), block.Name.LocalName is "h1" or "h2", block.Name.LocalName == "pre");
            }
        }
        graphics!.Dispose();
        using var buffer = new MemoryStream(); document.Save(buffer, false); return buffer.ToArray();
    }

    private static void Figure(XGraphics g, XElement svg)
    {
        foreach (var e in svg.Elements())
        {
            double N(string name, double fallback = 0) => ReportFigureRenderer.Number(e, name, fallback);
            XColor Colour(string s)
            {
                var colour = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(s);
                return XColor.FromArgb(colour.R, colour.G, colour.B);
            }
            XBrush? fill = e.Attribute("fill")?.Value is { } f && f != "none" ? new XSolidBrush(Colour(f)) : null;
            XPen? pen = e.Attribute("stroke")?.Value is { } s ? new XPen(Colour(s), N("stroke-width", 1)) : null;
            if (pen is not null && e.Attribute("stroke-dasharray") is not null) pen.DashStyle = XDashStyle.Dash;
            switch (e.Name.LocalName)
            {
                case "rect": g.DrawRectangle(pen, fill, N("x"), N("y"), N("width"), N("height")); break;
                case "circle": g.DrawEllipse(pen, fill, N("cx") - N("r"), N("cy") - N("r"), N("r") * 2, N("r") * 2); break;
                case "line": g.DrawLine(pen!, N("x1"), N("y1"), N("x2"), N("y2")); break;
                case "text": g.DrawString(e.Value, new XFont("Arial", N("font-size", 12)), XBrushes.Black, N("x"), N("y")); break;
                case "path":
                    // Our writer emits M/L/H/V only. WPF's flattening preserves those exact vector coordinates.
                    var geometry = System.Windows.Media.Geometry.Parse(e.Attribute("d")!.Value).GetFlattenedPathGeometry();
                    foreach (var figure in geometry.Figures)
                    {
                        var previous = figure.StartPoint;
                        foreach (var segment in figure.Segments.Cast<System.Windows.Media.PolyLineSegment>())
                            foreach (var point in segment.Points)
                            { g.DrawLine(pen!, previous.X, previous.Y, point.X, point.Y); previous = point; }
                    }
                    break;
            }
        }
    }
}
