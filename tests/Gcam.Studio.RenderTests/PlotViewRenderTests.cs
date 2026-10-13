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

// The main-window offscreen renders, split by screen / capture group so no test comes near the 60 s deadlock deadline.
// They share one STA thread and Application (MainWindowRenderHost; WPF allows one Application per AppDomain), so they run
// one after the other in this collection; each test runs under its own deadline.
[Collection(MainWindowRenderCollection.Name)]
public sealed partial class PlotViewRenderTests(ITestOutputHelper output, MainWindowRenderHost host)
{
    /// <summary>The spectrum PlotView in both themes (full and 662 keV zoom), and the heatmap readout across refreshes.</summary>
    [RenderSnapshotFact]
    public void Spectrum_BothThemesFullAndZoom_RenderWithoutWindow() => host.Run(() =>
    {
        // As in the former single test: the readout check runs before any theme is loaded into the Application.
        Application.Current.Resources.MergedDictionaries.Clear();
        CheckHeatmapReadoutAcrossRefresh();
        foreach (string theme in new[] { "Dark", "Light" }) Render(theme);
    });

    /// <summary>Every workspace of the main window after a completed acquisition (single source), both sizes.</summary>
    [RenderSnapshotFact]
    public void MainWindow_Workspaces_Dark_RenderWithoutWindow() => MainWindow_Workspaces("Dark");

    [RenderSnapshotFact]
    public void MainWindow_Workspaces_Light_RenderWithoutWindow() => MainWindow_Workspaces("Light");

    private void MainWindow_Workspaces(string theme) => host.Run(() => RenderWindows(theme));

    /// <summary>The mixed two-isotope field: All / Cs-137 channels, and the crowded scene.</summary>
    [RenderSnapshotFact]
    public void MainWindow_MixedField_Dark_RenderWithoutWindow() => MainWindow_MixedField("Dark");

    [RenderSnapshotFact]
    public void MainWindow_MixedField_Light_RenderWithoutWindow() => MainWindow_MixedField("Light");

    private void MainWindow_MixedField(string theme) => host.Run(() =>
    {
        RenderWindows(theme, mixed: true);
        RenderWindows(theme, mixed: true, crowded: true);
    });

    /// <summary>The mixed field at focal plane 800 mm (the former optics-section variants) and with MLEM.</summary>
    [RenderSnapshotFact]
    public void MainWindow_FocalPlaneAndMlem_Dark_RenderWithoutWindow() => MainWindow_FocalPlaneAndMlem("Dark");

    [RenderSnapshotFact]
    public void MainWindow_FocalPlaneAndMlem_Light_RenderWithoutWindow() => MainWindow_FocalPlaneAndMlem("Light");

    private void MainWindow_FocalPlaneAndMlem(string theme) => host.Run(() =>
    {
        RenderWindows(theme, mixed: true, opticsExpanded: true);
        RenderWindows(theme, mixed: true, opticsExpanded: false);
        RenderWindows(theme, mixed: true, mlem: true);
    });

    /// <summary>The Waveform workspace modes, the Detector workspace and focus sweep, and the Measurements panel.</summary>
    [RenderSnapshotFact]
    public void Workspaces_WaveformDetectorMeasurements_Dark_RenderWithoutWindow() => Workspaces_WaveformDetectorMeasurements("Dark");

    [RenderSnapshotFact]
    public void Workspaces_WaveformDetectorMeasurements_Light_RenderWithoutWindow() => Workspaces_WaveformDetectorMeasurements("Light");

    private void Workspaces_WaveformDetectorMeasurements(string theme) => host.Run(() =>
    {
        RenderWaveforms(theme);
        RenderDetectorAndFocus(theme);
        RenderMeasurements(theme);
    });

    /// <summary>The shell: ambient inputs, the Reset confirmation, and the Options / top bar / status bar / left tabs /
    /// physical-readout captures.</summary>
    [RenderSnapshotFact]
    public void Shell_OptionsCaptionStatusLeftTabs_Dark_RenderWithoutWindow() => Shell_OptionsCaptionStatusLeftTabs("Dark");

    [RenderSnapshotFact]
    public void Shell_OptionsCaptionStatusLeftTabs_Light_RenderWithoutWindow() => Shell_OptionsCaptionStatusLeftTabs("Light");

    private void Shell_OptionsCaptionStatusLeftTabs(string theme) => host.Run(() =>
    {
        UseTheme(theme); // these captures read the Application's theme (the former test loaded it in an earlier step)
        RenderAmbientInputs(theme);
        RenderResetConfirmation(theme);
        RenderOptions(theme);
    });

    /// <summary>Loads one theme's dictionaries into the shared Application, as the workspace renders do themselves.</summary>
    private static void UseTheme(string theme)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();
        foreach (string file in new[] { $"Tokens.{theme}", "Metrics", "Typography", "Controls" })
            dictionaries.Add(new ResourceDictionary { Source = new Uri($"/Gcam.Studio;component/Themes/{file}.xaml", UriKind.Relative) });
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
        string directory = SnapshotDirectory("studio-plot");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"spectrum-{theme.ToLowerInvariant()}-{view}.png");
        using var stream = File.Create(path);
        encoder.Save(stream);
        output.WriteLine(path);
    }

    private static string SnapshotDirectory(string group)
        => Environment.GetEnvironmentVariable("GCAM_RENDER_OUTPUT") is { Length: > 0 } destination
            ? Path.Combine(destination, group) : Path.Combine(RepositoryRoot(), "docs", "assets", group);

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Gcam.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot find Gcam.sln for snapshot output.");
    }
}
