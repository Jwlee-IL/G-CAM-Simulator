using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Gcam.Studio.Controls;

namespace Gcam.Studio.RenderTests;

/// <summary>Pixel coverage of the production caption glyphs, without an HWND or desktop input.</summary>
public sealed class CaptionRenderTests
{
    [Theory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    [InlineData(1.5d)]
    public void MaximizedWorkArea_UsesMonitorRelativePixels_AtEveryDpi(double dpi)
    {
        var monitor = new WindowFrame.NativeRect { Left = -1920, Top = -120, Right = 0, Bottom = 960 };
        var work = new WindowFrame.NativeRect { Left = -1880, Top = -120, Right = 0, Bottom = 920 };
        var bounds = WindowFrame.WorkAreaBounds(monitor, work);
        Assert.Equal((40, 0, 1880, 1040), bounds);
        Assert.Equal(work.Right, monitor.Left + bounds.X + bounds.Width);
        Assert.Equal(work.Bottom, monitor.Top + bounds.Y + bounds.Height);
        Assert.Equal(1880 / dpi, bounds.Width / dpi);
        Assert.Equal(1040 / dpi, bounds.Height / dpi);
        var window = new WindowFrame.NativeRect { Left = -1920, Top = -120, Right = 0, Bottom = 960 };
        int border = (int)Math.Ceiling(8 * dpi);
        foreach (var (x, y, hit) in new[] { (-1919, -119, 13), (-1, -119, 14), (-1919, 959, 16), (-1, 959, 17),
            (-960, -119, 12), (-960, 959, 15), (-1919, 400, 10), (-1, 400, 11), (-960, 400, 0), (-1921, 400, 0) })
            Assert.Equal(hit, WindowFrame.ResizeHit(window, x, y, border));
    }
    [Fact]
    public void CaptionGlyphs_AreUnclipped_AndCaptionStatesUseThemeTokens()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (string theme in new[] { "Dark", "Light" })
                {
                    var resources = ReadoutRenderTests.ProductionView("OptionsView", theme).Resources;
                    var bar = new CaptionBar();
                    AutomationProperties.SetAutomationId(bar, "Window.TopBar");
                    var peer = UIElementAutomationPeer.CreatePeerForElement(bar);
                    Assert.Equal("Window.TopBar", peer.GetAutomationId());
                    Assert.Equal(AutomationControlType.Pane, peer.GetAutomationControlType());
                    Assert.True(peer.IsControlElement());
                    CheckAppIcons(resources);
                    var caption = new CaptionButtons { Resources = resources };
                    var root = new Border { Background = (Brush)resources["Brush.Bg.Surface"], Child = caption };
                    root.Measure(new Size(200, 48)); root.Arrange(new Rect(0, 0, 200, 48)); root.UpdateLayout();
                    var maximize = caption.Children.OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "Window.Maximize");
                    CheckSquare(root, maximize);
                    Assert.Equal("Maximize", AutomationProperties.GetName(maximize));
                    caption.IsMaximized = true; root.UpdateLayout();
                    Assert.Equal("Restore", AutomationProperties.GetName(maximize));
                    Assert.Equal("Restore", maximize.ToolTip);
                    var restored = Ink(root, maximize);
                    Assert.True(Enumerable.Range(3, 9).Count(i => restored[0, i]) >= 8, "restore front left edge");
                    Assert.True(Enumerable.Range(0, 9).Count(i => restored[i, 11]) >= 8, "restore front bottom edge");
                    Assert.True(Enumerable.Range(3, 9).Count(i => restored[i, 0]) >= 8, "restore rear top edge");
                    Assert.True(Enumerable.Range(0, 9).Count(i => restored[11, i]) >= 8, "restore rear right edge");
                    Assert.False(restored[4, 8]); Assert.False(restored[7, 2]);
                    caption.IsMaximized = false; root.UpdateLayout(); CheckSquare(root, maximize);
                    foreach (Button button in caption.Children)
                    {
                        string id = AutomationProperties.GetAutomationId(button);
                        var glyph = Ink(root, button);
                        if (id == "Window.Minimize")
                            Assert.Equal(2, Enumerable.Range(0, 12).Count(y => Enumerable.Range(0, 12).All(x => glyph[x, y])));
                        if (id == "Window.Close")
                        {
                            Assert.True(Enumerable.Range(1, 10).Count(i => glyph[i, i]) >= 8, "close descending diagonal");
                            Assert.True(Enumerable.Range(1, 10).Count(i => glyph[i, 11 - i]) >= 8, "close ascending diagonal");
                        }
                        Assert.Equal(Colors.Transparent, ((SolidColorBrush)button.Background).Color);
                        Assert.Equal(46, button.ActualWidth); Assert.Equal(48, button.ActualHeight);
                        Assert.True(button.Focusable); Assert.NotNull(button.ToolTip);
                        bool close = AutomationProperties.GetAutomationId(button) == "Window.Close";
                        SetState(button, typeof(UIElement), "IsMouseOver", true); root.UpdateLayout();
                        CheckBrush(button.Background, resources, close ? "Brush.Caption.Close.Hover" : "Brush.Bg.Hover");
                        CheckPixel(root, button, new Point(1, 1), button.Background);
                        SetState(button, typeof(ButtonBase), "IsPressed", true); root.UpdateLayout();
                        CheckBrush(button.Background, resources, close ? "Brush.Caption.Close.Pressed" : "Brush.Line");
                        CheckPixel(root, button, new Point(1, 1), button.Background);
                        if (close) CheckBrush(button.Foreground, resources, "Brush.Caption.Close.Foreground");
                        SetState(button, typeof(ButtonBase), "IsPressed", false);
                        SetState(button, typeof(UIElement), "IsMouseOver", false);
                        SetState(button, typeof(UIElement), "IsKeyboardFocused", true); root.UpdateLayout();
                        var cue = (Border)button.Template.FindName("FocusCue", button);
                        CheckBrush(cue.BorderBrush, resources, "Brush.Focus");
                        CheckPixel(root, button, new Point(2, 24), cue.BorderBrush);
                        SetState(button, typeof(UIElement), "IsKeyboardFocused", false);
                    }
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void TopBarLine_RunsUnderTheCaptionButtons_WhichRestOnTheBarSurface()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (string theme in new[] { "Dark", "Light" })
                {
                    // The production caption bar from MainWindow.xaml, at the minimum window width.
                    var view = ReadoutRenderTests.ProductionView("MainWindow.TopBar", theme);
                    double barHeight = ((GridLength)view.FindResource("Size.Bar")).Value;
                    var size = new Size(1280, barHeight);
                    view.Measure(size); view.Arrange(new Rect(size)); view.UpdateLayout();
                    var image = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                    image.Render(view);
                    var pixels = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(pixels, image.PixelWidth * 4, 0);
                    Color Pixel(int x, int y)
                    {
                        int i = (y * image.PixelWidth + x) * 4;
                        return Color.FromArgb(pixels[i + 3], pixels[i + 2], pixels[i + 1], pixels[i]);
                    }
                    var line = ((SolidColorBrush)view.FindResource("Brush.Line")).Color;
                    var surface = ((SolidColorBrush)view.FindResource("Brush.Bg.Surface")).Color;
                    var caption = ReadoutRenderTests.Descendants(view).OfType<CaptionButtons>().Single();
                    var buttons = Bounds(caption, view);
                    // The bar's last pixel row is the line from the left edge through the caption-button area to the right edge.
                    int bottom = (int)barHeight - 1;
                    for (int x = 0; x < image.PixelWidth; x++)
                        Assert.True(Pixel(x, bottom) == line, $"{theme}: bottom line missing at x={x} (caption buttons from {buttons.Left})");
                    // Flush top-right, one hairline short of the bar: the chrome's caption height, never over the line.
                    Assert.Equal(0, buttons.Top); Assert.Equal(size.Width, buttons.Right);
                    Assert.Equal(barHeight - 1, buttons.Height);
                    Assert.Equal((double)view.FindResource("Size.Caption"), buttons.Height);
                    foreach (Button button in caption.Children)
                    {
                        // Resting caption buttons are transparent on the bar: corners and the row above the line show the surface.
                        var b = Bounds(button, view);
                        foreach (var (x, y) in new[] { (b.Left + 1, b.Top + 1), (b.Right - 2, b.Top + 1), (b.Left + 1, b.Bottom - 1), (b.Right - 2, b.Bottom - 1) })
                            Assert.True(Pixel((int)x, (int)y) == surface, $"{theme} {AutomationProperties.GetAutomationId(button)}: resting background at {x},{y}");
                    }
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void ClearGlyph_IsOneConnectedTrashCan_WithSlotCutOuts()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (string theme in new[] { "Dark", "Light" })
                {
                    var resources = ReadoutRenderTests.ProductionView("OptionsView", theme).Resources;
                    var clear = (Geometry)resources["Icon.Clear"];
                    var glyph = IconInk(resources, clear);
                    Assert.Equal(1, Components(glyph));
                    // Lid spans the box; the slots are open in the body's middle and closed by its bottom rim.
                    Assert.True(Enumerable.Range(0, 12).All(x => glyph[x, 2]), "full-width lid");
                    foreach (int slot in new[] { 4, 7 })
                    {
                        Assert.True(Enumerable.Range(5, 5).All(y => !glyph[slot, y]), $"slot at x={slot}");
                        Assert.True(glyph[slot, 11], $"bottom rim under slot x={slot}");
                    }
                    // Reproduce the old can with a detached side bar (and a floating lid): the same oracle must reject it.
                    var old = Geometry.Parse("M0,3 H8 V12 H0 Z M0,1 H3 V0 H5 V1 H8 V2 H0 Z M10,4 H12 V12 H10 Z");
                    Assert.True(Components(IconInk(resources, old)) > 1, "old Clear glyph reads as one shape");
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static Rect Bounds(FrameworkElement element, Visual root)
        => element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));

    // The glyph as the production Button.Icon draws it, sampled over its Size.Icon box.
    private static bool[,] IconInk(ResourceDictionary resources, Geometry icon)
    {
        var button = new Button { Style = (Style)resources["Button.Icon"], Content = icon };
        var root = new Border { Resources = resources, Child = button, Background = (Brush)resources["Brush.Bg.Surface"],
            UseLayoutRounding = true, SnapsToDevicePixels = true };
        root.Measure(new Size(28, 28)); root.Arrange(new Rect(0, 0, 28, 28)); root.UpdateLayout();
        var ink = Ink(root, button);
        root.Child = null;
        return ink;
    }

    // 4-connected ink regions: a detached bar or floating lid is a second region.
    private static int Components(bool[,] ink)
    {
        int width = ink.GetLength(0), height = ink.GetLength(1), count = 0;
        var seen = new bool[width, height];
        for (int x0 = 0; x0 < width; x0++)
        for (int y0 = 0; y0 < height; y0++)
        {
            if (!ink[x0, y0] || seen[x0, y0]) continue;
            count++;
            var stack = new Stack<(int X, int Y)>([(x0, y0)]); seen[x0, y0] = true;
            while (stack.TryPop(out var p))
                foreach (var (x, y) in new[] { (p.X - 1, p.Y), (p.X + 1, p.Y), (p.X, p.Y - 1), (p.X, p.Y + 1) })
                    if (x >= 0 && y >= 0 && x < width && y < height && ink[x, y] && !seen[x, y]) { seen[x, y] = true; stack.Push((x, y)); }
        }
        return count;
    }

    private static void CheckAppIcons(ResourceDictionary resources)
    {
        foreach (string icon in new[] { "Sun", "Moon", "Add", "Remove", "Clear", "Reset", "Settings", "Latest", "Next", "Cancel" })
        foreach (bool enabled in new[] { true, false })
        {
            var button = new Button { Style = (Style)resources["Button.Icon"], Content = resources["Icon." + icon], IsEnabled = enabled };
            AutomationProperties.SetAutomationId(button, icon);
            var root = new Border { Resources = resources, Child = button, Background = (Brush)resources["Brush.Bg.Surface"],
                UseLayoutRounding = true, SnapsToDevicePixels = true };
            root.Measure(new Size(28, 28)); root.Arrange(new Rect(0, 0, 28, 28)); root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(28, 28, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            IconRenderChecks.CheckAndInspect(root, bitmap);
            // Reproduce the old cramped content box: the same oracle must reject it.
            button.Padding = new Thickness(8);
            root.UpdateLayout(); bitmap.Render(root);
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => IconRenderChecks.CheckAndInspect(root, bitmap));
            root.Child = null;
        }
    }

    private static void CheckSquare(FrameworkElement root, Button button)
    {
        var ink = Ink(root, button);
        int size = ink.GetLength(0);
        foreach (string side in new[] { "left", "right", "top", "bottom" })
        {
            // The two corner samples may anti-alias; the straight interior of every edge must carry ink.
            int count = Enumerable.Range(0, size).Count(i => side switch
            {
                "left" => ink[0, i], "right" => ink[size - 1, i],
                "top" => ink[i, 0], _ => ink[i, size - 1]
            });
            Assert.True(count >= size - 2, $"Maximize {side} edge has {count}/{size} ink pixels");
        }
    }

    private static bool[,] Ink(FrameworkElement root, Button button)
    {
        var path = ReadoutRenderTests.Descendants(button).OfType<Path>().Single();
        var origin = path.TransformToAncestor(root).Transform(new Point());
        var image = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(root);
        var pixels = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(pixels, image.PixelWidth * 4, 0);
        var ink = ((SolidColorBrush)button.Foreground).Color;
        bool IsInk(int x, int y)
        {
            int i = (y * image.PixelWidth + x) * 4;
            return Math.Abs(pixels[i] - ink.B) < 8 && Math.Abs(pixels[i + 1] - ink.G) < 8 && Math.Abs(pixels[i + 2] - ink.R) < 8;
        }
        int left = (int)Math.Round(origin.X), top = (int)Math.Round(origin.Y);
        int size = (int)(double)button.FindResource("Size.Icon");
        var result = new bool[size, size];
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++) result[x, y] = IsInk(left + x, top + y);
        return result;
    }

    private static void CheckBrush(Brush brush, ResourceDictionary resources, string key)
        => Assert.Equal(((SolidColorBrush)resources[key]).Color, ((SolidColorBrush)brush).Color);

    private static void CheckPixel(FrameworkElement root, Button button, Point sample, Brush expected)
    {
        var point = button.TransformToAncestor(root).Transform(sample);
        var image = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(root);
        var pixel = new byte[4];
        image.CopyPixels(new Int32Rect((int)point.X, (int)point.Y, 1, 1), pixel, 4, 0);
        Assert.Equal(((SolidColorBrush)expected).Color, Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]));
    }

    // Set WPF control state directly on the detached visual; no HWND, pointer or global keyboard state is used.
    private static void SetState(Button button, Type owner, string property, bool value)
    {
        var key = (DependencyPropertyKey)owner.GetField(property + "PropertyKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        button.SetValue(key, value);
    }
}
