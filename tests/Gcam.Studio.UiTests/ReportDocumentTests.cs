using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Core.Reporting;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Services;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;

namespace Gcam.Studio.UiTests;

/// <summary>Headless byte validation. Uses WPF drawing/font services without a window or UI Automation.</summary>
public sealed class ReportDocumentTests
{
    [Fact]
    public void Pdf_HasPagesTextAndVectorFigures_WithoutImageObjects()
    {
        var bytes = PdfReportWriter.Write(Report());
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes));
        using var document = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        Assert.Equal("GCAM Studio simulation report", document.Info.Title);
        Assert.True(document.PageCount >= 2);
        IEnumerable<PdfSharp.Pdf.PdfDictionary> Streams(PdfSharp.Pdf.PdfItem item)
        {
            if (item is PdfSharp.Pdf.Advanced.PdfReference reference) return Streams(reference.Value);
            if (item is PdfSharp.Pdf.PdfArray array) return array.Elements.SelectMany(Streams);
            return [(PdfSharp.Pdf.PdfDictionary)item];
        }
        // Read the original dictionaries: page.Contents normalizes/decompresses imported content.
        var streams = document.Pages.Cast<PdfSharp.Pdf.PdfPage>().SelectMany(p => Streams(p.Elements["/Contents"]!)).ToArray();
        Assert.Contains(streams, stream => stream.Elements["/Filter"]?.ToString()?.Contains("FlateDecode") == true);
        Assert.Contains(streams, stream => stream.Stream.Value.Length < stream.Stream.UnfilteredValue.Length);
        string content = string.Join("\n", streams.Select(stream => Encoding.Latin1.GetString(stream.Stream.UnfilteredValue)));
        Assert.Contains("BT", content); Assert.Contains(" re", content); Assert.Contains(" l", content);
        Assert.DoesNotContain("/Subtype /Image", Encoding.ASCII.GetString(bytes));
        string text = PdfText(document);
        foreach (string expected in new[] { "Scene", "Acquisition", "Imaging", "Spectrum", "Imaging measurements", "test+commit", "peak (1.0, 1.0) mm", "12.3 mm" })
            Assert.Contains(expected, text);
        string normalized = Regex.Replace(text, @"\s+", " ");
        Assert.True(normalized.IndexOf("Appendix — acquired configuration", StringComparison.Ordinal) > normalized.IndexOf("Imaging measurements", StringComparison.Ordinal), normalized);
        var lastFonts = document.Pages[document.PageCount - 1].Resources.Elements.GetDictionary("/Font")!;
        Assert.Contains(lastFonts.Elements.Keys, key => lastFonts.Elements.GetDictionary(key)!.Elements.GetName("/BaseFont").Contains("Consolas"));
        string html = Encoding.UTF8.GetString(ReportWriter.Write(Report()).Html);
        Assert.True(html.IndexOf("<details>", StringComparison.Ordinal) < html.IndexOf("<h2>Acquisition", StringComparison.Ordinal));
        Assert.DoesNotContain("Appendix —", html);
    }
    [Fact]
    public void Word_ContainsRealPngFigures_FromTheSharedSvgData()
    {
        var report = Report();
        var svg = ReportMarkup.Body(report).Descendants().First(e => e.Name.LocalName == "svg");
        byte[] png = ReportFigureRenderer.Png(svg);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8));
        byte[] bytes = WordReportWriter.Write(report, ReportFigureRenderer.Png);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var entry = zip.GetEntry("word/media/image1.png")!.Open();
        using var read = new MemoryStream(); entry.CopyTo(read);
        Assert.Equal(png, read.ToArray());
    }
    private static SimulationReport Report()
    {
        var image = new DetectorImage(2, 2); image[0, 0] = 10;
        var imaging = new ImagingResult(image, -1, 2, image, -1, 2, new SourceEstimate(new(1, 1, 0), 1), 10, TimeSpan.Zero);
        return new(DateTimeOffset.Parse("2026-10-15T12:00:00+09:00"), "test+commit", ConfigLoader.Serialize(new SimulationConfig()), false,
            [new("Source", "Cs-137")], [new("Counts", "10")], imaging, [], [new("Estimate", "peak (1.0, 1.0) mm")],
            new([250, 750], [1, 10], [new([new("Cs-137", 661.7)], 650, 675, 10, 1)], 11, 0, 1, .05, 1e-6, "chain", TimeSpan.Zero) { BinEdgesKeV = [0, 500, 1000] },
            true, [new("Summary", "11 pulses")], [new("M1", "Flood", "Distance", "12.3 mm", "points")]);
    }

    // Decode PDFsharp's emitted Unicode font maps and text operators, independently of the report markup.
    internal static string PdfText(PdfSharp.Pdf.PdfDocument document)
    {
        var result = new StringBuilder();
        foreach (PdfSharp.Pdf.PdfPage page in document.Pages)
        {
            var maps = new Dictionary<string, Dictionary<int, string>>();
            var fonts = page.Resources.Elements.GetDictionary("/Font")!;
            foreach (string key in fonts.Elements.Keys)
            {
                var map = new Dictionary<int, string>();
                var unicode = fonts.Elements.GetDictionary(key)!.Elements.GetDictionary("/ToUnicode");
                if (unicode is not null)
                {
                    string cmap = Encoding.ASCII.GetString(unicode.Stream.UnfilteredValue);
                    foreach (Match block in Regex.Matches(cmap, @"beginbfchar(.*?)endbfchar", RegexOptions.Singleline))
                        foreach (Match pair in Regex.Matches(block.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>"))
                            map[Convert.ToInt32(pair.Groups[1].Value, 16)] = Encoding.BigEndianUnicode.GetString(Convert.FromHexString(pair.Groups[2].Value));
                    foreach (Match block in Regex.Matches(cmap, @"beginbfrange(.*?)endbfrange", RegexOptions.Singleline))
                        foreach (Match range in Regex.Matches(block.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>"))
                        {
                            int first = Convert.ToInt32(range.Groups[1].Value, 16), last = Convert.ToInt32(range.Groups[2].Value, 16);
                            int start = Convert.ToInt32(range.Groups[3].Value, 16);
                            for (int code = first; code <= last; code++) map[code] = char.ConvertFromUtf32(start + code - first);
                        }
                }
                maps[key] = map;
            }
            string font = "";
            void Append(CObject operand)
            {
                if (operand is CArray array) { foreach (var item in array) Append(item); }
                if (operand is not CString value) return;
                // Fonts without a Unicode map use PDFsharp's WinAnsi (Windows-1252) encoding.
                if (!maps.TryGetValue(font, out var map) || map.Count == 0)
                    result.Append(CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetString(value.Value.Select(c => (byte)c).ToArray()));
                else for (int i = 0; i + 1 < value.Value.Length; i += 2)
                    result.Append(map.GetValueOrDefault((value.Value[i] << 8) | value.Value[i + 1], "?"));
                result.AppendLine();
            }
            foreach (var op in ContentReader.ReadContent(page).OfType<COperator>())
            {
                if (op.Name == "Tf") font = ((CName)op.Operands[0]).Name;
                if (op.Name is "Tj" or "TJ") Append(op.Operands[0]);
            }
        }
        return result.ToString();
    }
}
