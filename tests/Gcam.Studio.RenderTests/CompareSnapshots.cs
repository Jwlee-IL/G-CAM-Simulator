using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Services;

namespace Gcam.Studio.RenderTests;

public sealed partial class PlotViewRenderTests
{
    /// <summary>One transported Baseline acquisition, reused across all eight real-data captures.</summary>
    [RenderSnapshotFact]
    public async Task Compare_RealBaseline_BothThemesAndSizes_RenderWithoutWindow()
    {
        var optics = new OpticsSettings { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
        var preparation = new ReadoutPreparationService();
        var prepared = await preparation.PrepareAsync(optics, new());
        Assert.True(prepared.Succeeded);
        var detector = new DetectorSettings { ReadoutMode = ReadoutMode.FourOutputAnger, PreparedReadoutId = prepared.Id };
        AcquisitionSnapshot? snapshot = null;
        await using (var session = new SimulationService(readoutPreparation: preparation).Start([new()], optics, 10, 1000, detector, seed: 47001))
            await foreach (var prefix in session.ReadSnapshotsAsync()) snapshot = prefix;
        Assert.NotNull(snapshot?.Readout);
        Assert.Equal(426, snapshot.Readout.Hits.Count);
        Assert.Equal(360, snapshot.Readout.Records.Count);
        output.WriteLine("Baseline: Cs-137, 661.7 keV calibration, on axis, 1 m, 500 uCi, 10 s, seed 47001; ambient / BSR off.");
        host.Run(() =>
        {
            foreach (string theme in new[] { "Dark", "Light" })
            {
                UseTheme(theme);
                var model = new MainViewModel(new FixtureAcquisition(), new FixtureTheme(Enum.Parse<AppTheme>(theme)), new FixtureSpectrum(),
                    readoutPreparation: preparation, compare: new ReadoutCompareService());
                model.Optics = optics; model.ReadoutMode = ReadoutMode.FourOutputAnger;
                model.Snapshot = snapshot; model.SelectedWorkspace = model.Compare;
                model.Compare.WhenUpdated.GetAwaiter().GetResult();
                Assert.Null(model.Compare.Error);
                Assert.Equal("Cs-137", Assert.Single(model.Compare.DirectPeaks).Isotope);
                Assert.Equal("Cs-137", Assert.Single(model.Compare.AngerPeaks).Isotope);
                foreach (bool difference in new[] { false, true })
                foreach (var size in new[] { new Size(1280, 800), new Size(1440, 900) })
                {
                    model.Compare.ShowDifference = difference;
                    var (root, content) = DetachMainWindow(model, size);
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); root.UpdateLayout();
                    AttachCompareOverlays(root);
                    VerifyCompareGeometry(root, difference);
                    VerifyOverlayChips(root, "DirectRecon", checkMarkers: true);
                    VerifyOverlayChips(root, "AngerRecon", checkMarkers: true);
                    SaveCompare(root, size, $"real-baseline-{(difference ? "difference" : "pair")}", theme);
                    content.DataContext = null; root.UpdateLayout();
                }
                model.SelectedWorkspace = model.Imaging;
            }
        });
    }

    [RenderSnapshotFact]
    public void Compare_AllStates_BothThemesAndSizes_RenderWithoutWindow() => host.Run(() =>
    {
        foreach (string theme in new[] { "Dark", "Light" })
        foreach (string state in new[] { "direct", "ready", "pair", "difference", "no-held", "unknown", "empty-window", "updating", "missing-line" })
        {
            UseTheme(theme);
            var preparation = new ReadoutPreparationService();
            var model = new MainViewModel(new FixtureAcquisition(), new FixtureTheme(Enum.Parse<AppTheme>(theme)), new FixtureSpectrum(),
                readoutPreparation: preparation, compare: new ReadoutCompareService());
            model.Optics = new() { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
            if (state != "direct") model.ReadoutMode = ReadoutMode.FourOutputAnger;
            if (state is not ("direct" or "ready"))
            {
                var snapshot = CompareSnapshot(model.Optics, state);
                if (state is "empty-window" or "missing-line")
                {
                    model.Imaging.View = new([new("All", 0, 0, snapshot.Imaging, []), new("Energy window", 600, 720, snapshot.Imaging, [])], [], TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
                    model.Imaging.SelectedIsotope = "Energy window";
                    if (state == "missing-line") { model.WindowLowKeV = 100; model.WindowHighKeV = 140; }
                }
                model.Snapshot = snapshot;
            }
            model.SelectedWorkspace = model.Compare;
            model.Compare.WhenUpdated.GetAwaiter().GetResult();
            Assert.Null(model.Compare.Error);
            model.Compare.ShowDifference = state == "difference";
            if (state == "updating") model.Compare.IsUpdating = true; // drawing fixture: keep the completed atomic pair
            foreach (var size in new[] { new Size(1280, 800), new Size(1440, 900) })
            {
                var (root, content) = DetachMainWindow(model, size);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); root.UpdateLayout();
                var picker = Descendants(root).OfType<RadioButton>().Single(e => AutomationProperties.GetAutomationId(e) == "Workspace.Compare");
                Assert.True(picker.IsChecked);
                var switcher = Descendants(root).OfType<Border>().Single(e => AutomationProperties.GetAutomationId(e) == "WorkspaceSwitch");
                var input = Descendants(root).OfType<TextBox>().Single(e => AutomationProperties.GetAutomationId(e) == "AcquisitionLiveTime");
                var actions = (FrameworkElement)VisualTreeHelper.GetParent(input);
                Assert.True(switcher.TranslatePoint(new Point(switcher.ActualWidth, 0), root).X <= actions.TranslatePoint(new Point(), root).X);
                Assert.Equal(28, Descendants(root).OfType<Button>().Single(e => AutomationProperties.GetAutomationId(e) == "ExportReport").ActualWidth);
                if (state == "direct")
                {
                    Assert.False(IsShown(Descendants(root).OfType<TextBlock>().Single(e => AutomationProperties.GetAutomationId(e) == "Compare.SameShare")));
                    Assert.False(IsShown(Descendants(root).OfType<HelpMark>().Single(e => AutomationProperties.GetAutomationId(e) == "Compare.CostHelp")));
                    Assert.True(IsShown(Descendants(root).OfType<TextBlock>().Single(e => AutomationProperties.GetAutomationId(e) == "Compare.Reference")));
                }
                if (model.Compare.Result is { } result)
                {
                    var direct = Descendants(root).OfType<HeatmapView>().Single(e => AutomationProperties.GetAutomationId(e) == "Compare.DirectFlood");
                    Assert.Same(result.Direct.Image.Flood, direct.Image);
                    AttachCompareOverlays(root);
                    VerifyCompareGeometry(root, state == "difference");
                    if (state is "no-held" or "unknown" or "empty-window") Assert.True(result.Direct.Image.EffectiveCounts > 0);
                    if (state == "difference")
                    {
                        var difference = Descendants(root).OfType<HeatmapView>().Single(e => AutomationProperties.GetAutomationId(e) == "Compare.Difference");
                        Assert.Equal(0, difference.DataMin); Assert.Equal(1, difference.DataMax);
                        Assert.Contains(result.Assignment.Difference.Raw.ToArray(), double.IsNaN);
                    }
                }
                SaveCompare(root, size, state, theme);
                content.DataContext = null; root.UpdateLayout();
            }
            model.SelectedWorkspace = model.Imaging;
        }
    });

    private static void AttachCompareOverlays(FrameworkElement root)
    {
        foreach (var heatmap in Descendants(root).OfType<HeatmapView>().Where(MeasurementOverlay.GetDisplayOnly))
        {
            Assert.Null(MeasurementOverlay.GetSession(heatmap)); Assert.False(MeasurementOverlay.GetCanMoveMarkers(heatmap));
            typeof(MeasurementOverlay).GetMethod("OnLoaded", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [heatmap, new RoutedEventArgs(FrameworkElement.LoadedEvent)]);
        }
        root.UpdateLayout();
    }

    private static void VerifyCompareGeometry(FrameworkElement root, bool difference)
    {
        var maps = Descendants(root).OfType<HeatmapView>().ToArray();
        var map = maps.Single(e => e.Name == "Difference");
        Assert.False(MeasurementOverlay.GetDisplayOnly(map));
        Assert.Null(MeasurementOverlay.GetMarkers(map)); Assert.Null(MeasurementOverlay.GetFoundPeaks(map));
        Assert.DoesNotContain(Descendants(root).OfType<MeasurementAdorner>(), a => ReferenceEquals(a.AdornedElement, map));
        foreach (string name in new[] { "DirectRecon", "AngerRecon" })
        {
            var recon = maps.Single(e => e.Name == name);
            Assert.True(IsShown(recon)); Assert.True(recon.ActualHeight > 0);
            if (difference) Assert.True(recon.TranslatePoint(new(), root).Y >= map.TranslatePoint(new(0, map.ActualHeight), root).Y);
        }
        Assert.Equal(difference, IsShown(map));
        Assert.Equal(!difference, IsShown(maps.Single(e => e.Name == "DirectFlood")));
    }

    // IsVisible is false for every element of a detached tree, which deliberately has no presentation source.
    private static bool IsShown(DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    private void SaveCompare(FrameworkElement root, Size size, string state, string theme)
    {
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string directory = SnapshotDirectory("compare"); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"compare-{state}-{theme.ToLowerInvariant()}-{size.Width:0}x{size.Height:0}.png");
        using (var stream = File.Create(path)) encoder.Save(stream);
        output.WriteLine(path);
    }

    private static AcquisitionSnapshot CompareSnapshot(OpticsSettings optics, string state)
    {
        var offers = ReadoutOfferRecordLoader.LoadEmbedded();
        var prep = new ReadoutPreparation(Guid.NewGuid(), ReadoutPolicy.Key(optics, new()), true, null, new(2, 2), [], [], [],
            0, 0, 0, 0, TimeSpan.Zero, 1, 2, 1, 10, 50)
        { Plan = ReadoutPolicy.Plan(optics, new(), 661.7, offers), CorrectionHistogram = Enumerable.Repeat(1, 144).ToArray() };
        var hits = Enumerable.Range(0, 144).Select(i => new RealisedReadoutHit(.01 * i, new(), i % 12, i / 12, 661.7)).ToArray();
        var records = state == "no-held" ? [] : Enumerable.Range(0, 144).Select(i => new MeasuredReadoutRecord(i, .01 * i, .01 * i,
            new(), new(), 0, 0, state == "unknown" ? -1 : i % 3 == 0 ? (i + 1) % 144 : i,
            state == "unknown" ? double.NaN : state == "empty-window" ? 800 : 640 + i % 40, i, i % 10 == 0 ? 2 : 1, .99)).ToArray();
        var flood = new DetectorImage(12, 12);
        foreach (var r in records.Where(e => e.Crystal >= 0)) flood.Add(r.Crystal % 12, r.Crystal / 12, 1);
        int assigned = records.Count(r => r.Crystal >= 0);
        return new(2, assigned, 72, 1, false, new(flood.ReadOnlyCopy(), -5.5, 1, null, 0, 0, null, assigned, TimeSpan.Zero), [], TimeSpan.Zero, true)
        { Detector = new() { ReadoutMode = ReadoutMode.FourOutputAnger }, Optics = optics,
            Readout = new(prep, records, hits, records.Length - assigned, false)
            { Lines = [new(prep, records.Select(r => new ReadoutLineAssignment(r.Crystal, r.EnergyKeV)).ToArray(), assigned, records.Length - assigned)] } };
    }
}
