using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gcam.Core;

namespace Gcam.Studio.Rendering;

/// <summary>Viridis colormap and a min–max normalised <see cref="DetectorImage"/> → bitmap conversion.</summary>
public static class Colormap
{
    private static readonly (double R, double G, double B)[] ViridisStops =
    [
        (68, 1, 84), (59, 82, 139), (33, 145, 140), (94, 201, 98), (253, 231, 37),
    ];

    public static (byte R, byte G, byte B) Viridis(double t)
    {
        t = Math.Clamp(double.IsNaN(t) ? 0 : t, 0, 1) * (ViridisStops.Length - 1);
        int i = Math.Min((int)t, ViridisStops.Length - 2);
        double f = t - i;
        var (a, b) = (ViridisStops[i], ViridisStops[i + 1]);
        return ((byte)(a.R + (b.R - a.R) * f), (byte)(a.G + (b.G - a.G) * f), (byte)(a.B + (b.B - a.B) * f));
    }

    /// <summary>Renders the image with row 0 at the bottom (detector +y up). Pixels are BGRA32.</summary>
    public static BitmapSource ToBitmap(DetectorImage image)
    {
        double min = double.MaxValue, max = double.MinValue;
        foreach (var v in image.Raw) { if (v < min) min = v; if (v > max) max = v; }
        double span = max > min ? max - min : 1;

        int w = image.Width, h = image.Height;
        var pixels = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            int row = (h - 1 - y) * w * 4;
            for (int x = 0; x < w; x++)
            {
                var (r, g, b) = Viridis((image[x, y] - min) / span);
                int o = row + x * 4;
                pixels[o] = b; pixels[o + 1] = g; pixels[o + 2] = r; pixels[o + 3] = 255;
            }
        }

        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        bmp.Freeze();
        return bmp;
    }
}
