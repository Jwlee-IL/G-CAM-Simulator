using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using Gcam.Studio.Controls;

namespace Gcam.Studio.RenderTests;

/// <summary>Declutter review F1: the help-mark tooltip text took its MaxWidth from a GridLength token, which throws when the
/// reference resolves. The tooltip is laid out detached under the real theme dictionaries on an STA — no window, no popup.</summary>
public sealed class HelpMarkTooltipTests
{
    [Fact]
    public void TooltipContent_ResolvesItsWidthTokenAsADouble_UnderTheRealThemes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (string theme in new[] { "Dark", "Light" })
                {
                    var mark = new HelpMark { Text = new string('x', 40) + " A long explanation that wraps inside the tooltip"
                        + " rather than spanning the screen, longer than the token width at the caption font size." };
                    var tip = Assert.IsType<ToolTip>(mark.ToolTip);
                    var content = Assert.IsType<TextBlock>(tip.Content);
                    // A ToolTip may have no parent but its popup, so it carries the theme dictionaries itself (in Studio they
                    // come from the application resources) and is laid out as a root: the resource reference on its
                    // content resolves exactly as when the popup opens, without an HWND.
                    tip.Resources = ThemeDictionaries(theme);
                    tip.Measure(new Size(800, 600));
                    tip.Arrange(new Rect(tip.DesiredSize));
                    Drain();

                    double token = Assert.IsType<double>(tip.FindResource("Size.HelpMark.TooltipWidth"));
                    Assert.Equal(token, content.MaxWidth);
                    Assert.True(content.ActualWidth <= token, $"{theme}: tooltip text {content.ActualWidth} wider than {token}");
                    Assert.True(content.ActualHeight > 0, $"{theme}: tooltip text was not laid out");
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static ResourceDictionary ThemeDictionaries(string theme)
    {
        string dictionaries = string.Concat(new[] { $"Tokens.{theme}", "Metrics", "Typography", "Controls" }
            .Select(f => $"<ResourceDictionary Source='/Gcam.Studio;component/Themes/{f}.xaml' />"));
        return (ResourceDictionary)XamlReader.Parse(
            "<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>"
            + $"<ResourceDictionary.MergedDictionaries>{dictionaries}</ResourceDictionary.MergedDictionaries></ResourceDictionary>");
    }

    private static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
