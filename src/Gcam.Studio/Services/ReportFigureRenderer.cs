using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace Gcam.Studio.Services;

/// <summary>Rasterizes only the report writer's own SVG primitives; no file, URI or external image loading.</summary>
public static class ReportFigureRenderer
{
    public static double Number(XElement element, string name, double fallback = 0)
        => element.Attribute(name) is { } a ? double.Parse(a.Value, CultureInfo.InvariantCulture) : fallback;
    public static double[] Size(XElement svg) => svg.Attribute("viewBox")!.Value.Split(' ').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();

    public static byte[] Png(XElement svg)
    {
        var size = Size(svg);
        var visual = new DrawingVisual();
        using (var draw = visual.RenderOpen())
        {
            draw.DrawRectangle(Brushes.White, null, new Rect(0, 0, size[2], size[3]));
            foreach (var e in svg.Elements())
            {
                Brush? fill = e.Attribute("fill")?.Value is { } f && f != "none" ? (Brush)new BrushConverter().ConvertFromInvariantString(f)! : null;
                Pen? pen = e.Attribute("stroke")?.Value is { } s ? new Pen((Brush)new BrushConverter().ConvertFromInvariantString(s)!, Number(e, "stroke-width", 1)) : null;
                if (pen is not null && e.Attribute("stroke-dasharray") is not null) pen.DashStyle = DashStyles.Dash;
                switch (e.Name.LocalName)
                {
                    case "rect": draw.DrawRectangle(fill, pen, new Rect(Number(e, "x"), Number(e, "y"), Number(e, "width"), Number(e, "height"))); break;
                    case "circle": draw.DrawEllipse(fill, pen, new Point(Number(e, "cx"), Number(e, "cy")), Number(e, "r"), Number(e, "r")); break;
                    case "line": draw.DrawLine(pen!, new(Number(e, "x1"), Number(e, "y1")), new(Number(e, "x2"), Number(e, "y2"))); break;
                    case "path": draw.DrawGeometry(fill, pen, Geometry.Parse(e.Attribute("d")!.Value)); break;
                    case "text":
                        var text = new FormattedText(e.Value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                            new Typeface("Arial"), Number(e, "font-size", 12), Brushes.Black, 1);
                        draw.DrawText(text, new Point(Number(e, "x"), Number(e, "y") - text.Baseline)); break;
                }
            }
        }
        var bitmap = new RenderTargetBitmap((int)size[2], (int)size[3], 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var buffer = new MemoryStream(); encoder.Save(buffer); return buffer.ToArray();
    }
}
