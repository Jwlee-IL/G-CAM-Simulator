using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;

namespace Gcam.Studio.RenderTests;

/// <summary>Checks complete glyph ink against an unconstrained reference, then exports enlarged button crops.</summary>
internal static class IconRenderChecks
{
    public static void CheckAndInspect(FrameworkElement root, RenderTargetBitmap window, string? snapshot = null)
    {
        foreach (var button in ReadoutRenderTests.Descendants(root).OfType<Button>())
        {
            var iconStyle = (Style)button.FindResource("Button.Icon");
            bool icon = false;
            for (var style = button.Style; style is not null; style = style.BasedOn) icon |= ReferenceEquals(style, iconStyle);
            string id = AutomationProperties.GetAutomationId(button);
            bool caption = id.StartsWith("Window.", StringComparison.Ordinal);
            if ((!icon && !caption) || !Visible(button, root)) continue;
            var path = ReadoutRenderTests.Descendants(button).OfType<Path>().Single();
            var box = path.TransformToAncestor(button).TransformBounds(new Rect(path.RenderSize));
            double size = (double)button.FindResource("Size.Icon");
            Assert.True(new Rect(button.RenderSize).Contains(box), $"{id}: glyph outside button");
            Assert.InRange(Math.Abs(box.Left - (button.ActualWidth - box.Right)), 0, 1);
            Assert.InRange(Math.Abs(box.Top - (button.ActualHeight - box.Bottom)), 0, 1);

            // A separate Path has the full icon slot: it does not inherit the button's content padding or clip.
            var reference = new Path { Data = path.Data, Fill = Brushes.White, Stretch = path.Stretch, Width = size, Height = size,
                UseLayoutRounding = path.UseLayoutRounding, SnapsToDevicePixels = path.SnapsToDevicePixels };
            reference.Measure(new Size(size, size)); reference.Arrange(new Rect(0, 0, size, size));
            Assert.True(box.Size == reference.RenderSize,
                $"{id}: glyph slot {box}; button {button.RenderSize}; full reference {reference.RenderSize}");
            var expected = new RenderTargetBitmap((int)size, (int)size, 96, 96, PixelFormats.Pbgra32);
            var referenceInk = new DrawingVisual();
            using (var context = referenceInk.RenderOpen()) context.DrawGeometry(Brushes.White, null, reference.RenderedGeometry);
            expected.Render(referenceInk);
            var actual = window;
            var origin = path.TransformToAncestor(root).Transform(new Point());
            if (ReadoutRenderTests.Descendants(root).OfType<Gcam.Studio.Controls.ModalLayer>().Any(l => l.Visibility == Visibility.Visible))
            {
                // The scrim/dialog intentionally covers underlying ink. Check that same production template
                // separately at its actual size, rather than mistaking modal occlusion for icon clipping.
                var clone = new Button { Style = button.Style, Content = button.Content, Foreground = button.Foreground,
                    IsEnabled = button.IsEnabled, DataContext = button.DataContext,
                    UseLayoutRounding = button.UseLayoutRounding, SnapsToDevicePixels = button.SnapsToDevicePixels };
                clone.Measure(button.RenderSize); clone.Arrange(new Rect(button.RenderSize)); clone.UpdateLayout();
                var clonePath = ReadoutRenderTests.Descendants(clone).OfType<Path>().Single();
                origin = clonePath.TransformToAncestor(clone).Transform(new Point());
                actual = new RenderTargetBitmap((int)button.ActualWidth, (int)button.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                actual.Render(clone);
            }
            byte[] mask = Pixels(expected), pixels = Pixels(actual);
            var foreground = ((SolidColorBrush)path.Fill).Color;
            int samples = 0;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (mask[(y * (int)size + x) * 4 + 3] < 250) continue;
                int i = (((int)Math.Round(origin.Y) + y) * actual.PixelWidth + (int)Math.Round(origin.X) + x) * 4;
                Assert.True(Math.Abs(pixels[i] - foreground.B) < 8 && Math.Abs(pixels[i + 1] - foreground.G) < 8
                    && Math.Abs(pixels[i + 2] - foreground.R) < 8, $"{id}: clipped glyph ink at {x},{y}");
                samples++;
            }
            Assert.True(samples > 0, $"{id}: no opaque reference ink");
            var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
            Assert.True(new Rect(root.RenderSize).Contains(bounds), $"{id}: button outside rendered window");
            if (snapshot is null) continue;
            string directory = InspectionDirectory(snapshot);
            Directory.CreateDirectory(directory);
            string file = System.IO.Path.GetFileNameWithoutExtension(snapshot) + "-" + id.Replace('.', '-')
                + (caption ? "-" + AutomationProperties.GetName(button) : "") + ".png";
            Crop(window, bounds, System.IO.Path.Combine(directory, file));
        }
        foreach (string id in new[] { "Window.TopBar", "Options.Summary" })
        {
            if (snapshot is null) break;
            var element = ReadoutRenderTests.Descendants(root).OfType<FrameworkElement>().SingleOrDefault(e => AutomationProperties.GetAutomationId(e) == id);
            if (element is null || !Visible(element, root) || element.ActualHeight == 0) continue;
            string directory = InspectionDirectory(snapshot);
            Crop(window, element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize)),
                System.IO.Path.Combine(directory, System.IO.Path.GetFileNameWithoutExtension(snapshot) + "-" + id.Replace('.', '-') + ".png"));
        }
    }

    private static bool Visible(FrameworkElement element, FrameworkElement root)
    {
        for (DependencyObject? node = element; node is not null && node != root; node = VisualTreeHelper.GetParent(node))
            if (node is UIElement visual && visual.Visibility != Visibility.Visible) return false;
        return element.ActualWidth > 0 && element.ActualHeight > 0;
    }

    private static byte[] Pixels(BitmapSource bitmap)
    {
        var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0);
        return bytes;
    }

    private static string InspectionDirectory(string snapshot)
        => System.IO.Path.Combine(Directory.GetParent(System.IO.Path.GetDirectoryName(snapshot)!)!.Parent!.FullName, "inspection3");

    private static void Crop(BitmapSource window, Rect bounds, string file)
    {
        int x = (int)Math.Round(bounds.Left), y = (int)Math.Round(bounds.Top);
        int width = Math.Min((int)Math.Round(bounds.Width), window.PixelWidth - x);
        int height = Math.Min((int)Math.Round(bounds.Height), window.PixelHeight - y);
        var cropped = new CroppedBitmap(window, new Int32Rect(x, y, width, height));
        byte[] source = Pixels(cropped), enlarged = new byte[width * height * 16 * 4];
        for (int row = 0; row < height * 4; row++)
        for (int col = 0; col < width * 4; col++)
            Array.Copy(source, ((row / 4) * width + col / 4) * 4, enlarged, (row * width * 4 + col) * 4, 4);
        var bitmap = BitmapSource.Create(width * 4, height * 4, 96, 96, PixelFormats.Pbgra32, null, enlarged, width * 16);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file); encoder.Save(stream);
    }
}
