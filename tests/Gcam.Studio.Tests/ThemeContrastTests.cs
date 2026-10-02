using System.Globalization;
using System.Xml.Linq;

namespace Gcam.Studio.Tests;

public sealed class ThemeContrastTests
{
    [Theory]
    [InlineData("Dark", "Surface")]
    [InlineData("Dark", "Canvas")]
    [InlineData("Dark", "Raised")]
    [InlineData("Light", "Surface")]
    [InlineData("Light", "Canvas")]
    [InlineData("Light", "Raised")]
    public void DisabledText_MeetsRetainedValueContrastTarget(string theme, string background)
    {
        var tokens = Tokens(theme);
        double ratio = Contrast(tokens["Color.Text.Disabled"], tokens[$"Color.Bg.{background}"]);
        // Product target for retained values, including locked inputs. Disabled action opacity is separate.
        Assert.True(ratio >= 4.5, $"{theme} Disabled / {background}: {ratio:F6}:1, required >= 4.5:1");
    }

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void DisabledText_RemainsDimmerThanEnabledText(string theme)
    {
        var tokens = Tokens(theme);
        double disabled = Contrast(tokens["Color.Text.Disabled"], tokens["Color.Bg.Surface"]);
        Assert.True(disabled < Contrast(tokens["Color.Text.Secondary"], tokens["Color.Bg.Surface"]));
        Assert.True(disabled < Contrast(tokens["Color.Text.Primary"], tokens["Color.Bg.Surface"]));
    }

    private static Dictionary<string, string> Tokens(string theme)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Themes", $"Tokens.{theme}.xaml"))
            .Root!.Elements().Where(e => e.Name.LocalName == "Color")
            .ToDictionary(e => e.Attribute(x + "Key")!.Value, e => e.Value);
    }

    private static double Contrast(string a, string b)
    {
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static double Luminance(string hex)
    {
        Assert.Equal(7, hex.Length); // These foreground/background tokens must remain opaque #RRGGBB.
        double Component(int offset)
        {
            double c = int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Component(1) + 0.7152 * Component(3) + 0.0722 * Component(5);
    }
}
