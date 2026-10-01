using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.Plotting;
using Xunit.Abstractions;

namespace Gcam.Studio.RenderTests;

public sealed partial class PlotViewRenderTests(ITestOutputHelper output)
{
    [RenderSnapshotFact]
    public void Spectrum_BothThemesFullAndZoom_RenderWithoutWindow()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            // Base WPF resource infrastructure only: no Studio App, Run(), window or startup handler.
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            try
            {
                foreach (string theme in new[] { "Dark", "Light" })
                {
                    Render(theme);
                    RenderWindows(theme);
                    RenderWindows(theme, mixed: true);
                    RenderWindows(theme, mixed: true, opticsExpanded: true);
                    RenderWindows(theme, mixed: true, opticsExpanded: false);
                    RenderWaveforms(theme);
                }
            }
            catch (Exception ex) { failure = ex; }
            finally { application.Shutdown(); Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Offscreen rendering timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void Render(string theme)
    {
        const int width = 1000, height = 600;
        const double binWidth = 6;
        var resources = new ResourceDictionary();
        foreach (string file in new[] { $"Tokens.{theme}", "Metrics" })
            resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri($"/Gcam.Studio;component/Themes/{file}.xaml", UriKind.Relative) });
        Brush Brush(string key) => (Brush)resources[key];
        var counts = new double[256];
        // Analytic fixture for drawing only, not evidence about the detector's physics.
        for (int i = 0; i < counts.Length; i++)
        {
            double x = (i + 0.5) * binWidth;
            counts[i] = Math.Round(4000 * Gaussian(x, 34, 5) + 1200 * Gaussian(x, 661.7, 14)
                + 750 * Gaussian(x, 1173.2, 20) + 650 * Gaussian(x, 1332.5, 21)
                + (x < 478 ? 70 * Math.Exp(-x / 300) : 0));
        }
        var series = new PlotSeries("Fixture counts", counts, Step: binWidth, Kind: PlotKind.Histogram);
        var deferred = new PlotView { ViewRange = new(580, 740), Series = [series] };
        Assert.Equal(new PlotViewRange(580, 740), deferred.CurrentViewRange);
        var plot = new PlotView
        {
            Series = [series], LogY = true, XUnit = "keV", YUnit = "counts",
            XLabel = "measured energy (keV)", YLabel = "counts",
            Foreground = Brush("Brush.Text.Primary"), GridBrush = Brush("Brush.Plot.Grid"),
            BandBrush = Brush("Brush.Plot.Band"), BandEdgeBrush = Brush("Brush.Plot.BandEdge"), FocusBrush = Brush("Brush.Focus"),
            BandLabelBrush = Brush("Brush.Bg.Surface"), BandLabelPadding = (Thickness)resources["Pad.Plot.BandLabel"],
            Series1Brush = Brush("Brush.Plot.Series1"),
            Bands = [new(18, 50, "32.1 + 36.4 keV"), new(615, 708, "661.7 keV"),
                new(1110, 1236, "1173.2 keV"), new(1265, 1400, "1332.5 keV")]
        };
        TextElement.SetFontFamily(plot, (FontFamily)resources["Font.Mono"]);
        TextElement.SetFontSize(plot, (double)resources["FontSize.Label"]);
        var root = new Border { Background = Brush("Brush.Bg.Surface"), Child = plot };
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        Save(root, theme, "full", width, height);
        plot.ViewRange = new(580, 740);
        root.UpdateLayout();
        Save(root, theme, "zoom662", width, height);
        Assert.Equal(new PlotViewRange(580, 740), plot.CurrentViewRange);
        double top = plot.CurrentYMax;
        plot.Series = [series with { Y = counts.Select(c => c / 2).ToArray() }];
        root.UpdateLayout();
        Assert.Equal(new PlotViewRange(580, 740), plot.CurrentViewRange);
        Assert.Equal(top, plot.CurrentYMax);
        plot.ResetView(); root.UpdateLayout();
        Assert.Equal(new PlotViewRange(0, 1536), plot.CurrentViewRange);
        Assert.True(plot.RenderCount > 0);
    }

    private static double Gaussian(double x, double mean, double sigma) => Math.Exp(-0.5 * Math.Pow((x - mean) / sigma, 2));

    private void Save(Visual root, string theme, string view, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string directory = Path.Combine(RepositoryRoot(), "docs", "assets", "studio-plot");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"spectrum-{theme.ToLowerInvariant()}-{view}.png");
        using var stream = File.Create(path);
        encoder.Save(stream);
        output.WriteLine(path);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Gcam.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot find Gcam.sln for snapshot output.");
    }
}
