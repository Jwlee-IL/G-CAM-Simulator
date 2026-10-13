using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gcam.Studio.Core.Optics;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Studio.Controls;
using Gcam.Studio.Core.Detector;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.RenderTests;

/// <summary>Production readout XAML rendered detached on an STA, with synthetic drawing-only records (12 × 12 and the
/// 44 × 44 High-res module at its real LUT size: 1408 × 1408 bins, 1936 peaks).</summary>
public sealed class ReadoutRenderTests(ITestOutputHelper output)
{
    private static readonly OpticsSettings Baseline = OpticsPreset.All.Single(p => p.Name == "Baseline").Settings!;
    private static readonly OpticsSettings High = OpticsPreset.All.Single(p => p.Name == "High-res").Settings!;

    [Fact]
    public void BoundReadoutControls_RejectedEditsShowAcceptedSourceValues()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            try
            {
                var model = new MainViewModel(new RefusingAcquisition(), new Theme(), new SpectrumService(),
                    readoutPreparation: new FixturePreparation(_ => throw new InvalidOperationException("not prepared in this test")));
                var panel = ProductionView("ReadoutPanel", "Dark");
                panel.DataContext = model;
                panel.Measure(new Size(400, 800));
                panel.Arrange(new Rect(0, 0, 400, 800));
                Drain();
                var mode = Descendants(panel).OfType<ComboBox>().Single();
                Assert.Equal(ReadoutMode.DirectCrystal, mode.SelectedItem);
                // TODO-41: the default Sharp head may select the physical readout (offers are decided per line).
                mode.SelectedIndex = 1;
                Drain();
                Assert.Equal(ReadoutMode.FourOutputAnger, model.ReadoutMode);
                Assert.Equal(ReadoutMode.FourOutputAnger, mode.SelectedItem);
                Assert.Contains("Refused", Assert.IsType<ReadoutLineRow>(model.ReadoutLines[0]).Offer);

                model.IsPreparingReadout = true;
                mode.SelectedIndex = 0;
                Drain();
                Assert.Equal(ReadoutMode.FourOutputAnger, mode.SelectedItem);
                model.IsPreparingReadout = false;

                var low = Descendants(panel).OfType<TextBox>().Single(t => AutomationProperties.GetAutomationId(t) == "Readout.WindowLow");
                low.SetCurrentValue(TextBox.TextProperty, "800");
                low.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Drain();
                Assert.Equal(600, model.WindowLowKeV);
                Assert.Equal("600", low.Text);
                var high = Descendants(panel).OfType<TextBox>().Single(t => AutomationProperties.GetAutomationId(t) == "Readout.WindowHigh");
                high.SetCurrentValue(TextBox.TextProperty, "500");
                high.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Drain();
                Assert.Equal(720, model.WindowHighKeV);
                Assert.Equal("720", high.Text);

                // MainWindow's optics editor bindings, reproduced without constructing a Window.
                var preset = new ComboBox { DataContext = model.OpticsEditor, DisplayMemberPath = "Name" };
                preset.SetBinding(ComboBox.ItemsSourceProperty, new Binding("Presets"));
                preset.SetBinding(ComboBox.SelectedIndexProperty, new Binding("SelectedPresetIndex") { Mode = BindingMode.TwoWay });
                preset.Measure(new Size(400, 40));
                preset.Arrange(new Rect(0, 0, 400, 40));
                Drain();
                Assert.Equal(ReadoutMode.FourOutputAnger, model.ReadoutMode);
                preset.SelectedIndex = 3; // Wide FOV: no geometry gate any more
                Drain();
                Assert.Equal("Wide FOV", ((OpticsPreset)preset.SelectedItem).Name);
                Assert.Equal(34, model.Optics.DetectorPixels);
                Assert.Equal(8000, model.ReadoutPlans.Single(p => p.LineKeV == 661.7).Budget);
                panel.DataContext = null;
                preset.DataContext = null;
            }
            catch (Exception ex) { failure = ex; }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>Desktop run 3 (bundle 111545): the line rows' details must reach UI Automation. Builds the production Readout
    /// panel detached on an STA and walks the provider-side automation tree of Readout.Lines — the same peers UIA serves —
    /// before and after the rows are rebuilt by module edits (the case that lost the data-item children on the desktop).</summary>
    [Fact]
    public void LineRows_ExposeTheirDetailsToAutomation_AcrossRowRebuilds()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            try
            {
                var model = new MainViewModel(new RefusingAcquisition(), new Theme(), new SpectrumService(),
                    readoutPreparation: new FixturePreparation(_ => throw new InvalidOperationException("not prepared in this test")))
                { Optics = Baseline, ReadoutMode = ReadoutMode.FourOutputAnger };
                var panel = ProductionView("ReadoutPanel", "Dark");
                panel.DataContext = model;
                void Layout() { panel.Measure(new Size(400, 900)); panel.Arrange(new Rect(0, 0, 400, 900)); panel.UpdateLayout(); Drain(); }
                Layout();
                string Rows()
                {
                    var list = Descendants(panel).OfType<ContentItemsList>().Single(l => AutomationProperties.GetAutomationId(l) == "Readout.Lines");
                    var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(list);
                    peer.ResetChildrenCache();
                    var children = peer.GetChildren() ?? [];
                    var marks = children.Where(c => c.GetClassName() == nameof(HelpMark)).ToArray();
                    Assert.Equal(3, marks.Length);
                    Assert.All(marks, m => { Assert.True(m.IsControlElement()); Assert.True(m.IsKeyboardFocusable()); Assert.False(string.IsNullOrEmpty(m.GetHelpText())); });
                    for (int i = 0; i < 3; i++)
                    {
                        Assert.Contains(model.ReadoutLines[i].Summary, marks[i].GetName()); // the name contains the visible row text
                        Assert.Equal(model.ReadoutLines[i].Details, marks[i].GetHelpText());
                        Assert.Equal(model.ReadoutLines[i].HelpId, marks[i].GetAutomationId());
                    }
                    return string.Join("\n", marks.Select(m => m.GetName() + "\n" + m.GetHelpText()));
                }
                string baseline = Rows();
                Assert.Contains("Offered (experimental): Baseline 661.7 keV", baseline);
                Assert.Contains("2,000 histories per crystal", baseline);
                // The desktop failure's sequence: High-res module, then Custom optics edits that rebuild the rows.
                model.Optics = High; Layout();
                model.Optics = High with { MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 }; Layout();
                string custom = Rows();
                Assert.Contains("Offered (experimental): High 661.7 keV", custom);
                Assert.Contains("64,000 histories per crystal", custom);
                output.WriteLine(custom);
                panel.DataContext = null;
            }
            catch (Exception ex) { failure = ex; }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    internal static FrameworkElement ProductionView(string view, string theme)
    {
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "Gcam.sln"))) repo = Directory.GetParent(repo)!.FullName;
        bool topBar = view == "MainWindow.TopBar";
        string xaml = File.ReadAllText(Path.Combine(repo, "src/Gcam.Studio/Views", (topBar ? "MainWindow" : view) + ".xaml"));
        xaml = Regex.Replace(xaml, "x:Class=\"[^\"]+\"", "");
        if (topBar)
        {
            // The production caption bar alone, with the window's resources; its bindings stay unresolved (no view model).
            string header = xaml[..(xaml.IndexOf('>') + 1)];
            string windowResources = Regex.Match(xaml, "<Window.Resources>[\\s\\S]*?</Window.Resources>").Value;
            string bar = Regex.Match(xaml, "<c:CaptionBar[\\s\\S]*?</c:CaptionBar>").Value;
            header = Regex.Replace(header.Replace("<Window", "<UserControl"), " (Title|Width|Height|MinWidth|MinHeight|WindowStartupLocation|WindowStyle|FontFamily)=\"[^\"]+\"", "");
            xaml = header + windowResources.Replace("Window.Resources", "UserControl.Resources") + bar + "</UserControl>";
        }
        else if (view == "MainWindow")
        {
            // Exercise the production modal resources and host bindings without constructing compiled child views,
            // whose InitializeComponent requires Application resources. Full shell snapshots cover those views.
            string header = xaml[..(xaml.IndexOf('>') + 1)];
            string modalResources = Regex.Match(xaml, "<Window.Resources>[\\s\\S]*?</Window.Resources>").Value;
            string host = Regex.Match(xaml, "<c:ModalHost[^>]*>").Value;
            header = Regex.Replace(header.Replace("<Window", "<UserControl"), " (Title|Width|Height|MinWidth|MinHeight|WindowStartupLocation|WindowStyle|FontFamily)=\"[^\"]+\"", "");
            xaml = header + modalResources.Replace("Window.Resources", "UserControl.Resources") + host
                + "<Button Content='Underlying input' AutomationProperties.AutomationId='UnderlyingInput' />"
                + "</c:ModalHost></UserControl>";
        }
        xaml = xaml.Replace("clr-namespace:Gcam.Studio.Controls", "clr-namespace:Gcam.Studio.Controls;assembly=Gcam.Studio")
            .Replace("clr-namespace:Gcam.Studio.Converters", "clr-namespace:Gcam.Studio.Converters;assembly=Gcam.Studio")
            .Replace("clr-namespace:Gcam.Studio.Views", "clr-namespace:Gcam.Studio.Views;assembly=Gcam.Studio")
            .Replace("clr-namespace:Gcam.Studio.Services", "clr-namespace:Gcam.Studio.Services;assembly=Gcam.Studio");
        string dictionaries = string.Concat(new[] { $"Tokens.{theme}", "Metrics", "Typography", "Controls" }
            .Select(f => $"<ResourceDictionary Source='/Gcam.Studio;component/Themes/{f}.xaml' />"));
        string resources = $"<ResourceDictionary.MergedDictionaries>{dictionaries}</ResourceDictionary.MergedDictionaries>";
        if (xaml.Contains("<UserControl.Resources>"))
            xaml = xaml.Replace("<UserControl.Resources>", "<UserControl.Resources><ResourceDictionary>" + resources)
                .Replace("</UserControl.Resources>", "</ResourceDictionary></UserControl.Resources>");
        else
            xaml = xaml.Insert(xaml.IndexOf('>') + 1, $"<UserControl.Resources><ResourceDictionary>{resources}</ResourceDictionary></UserControl.Resources>");
        return (FrameworkElement)XamlReader.Parse(xaml);
    }

    [RenderSnapshotFact]
    public void ExperimentalDetectorAndFourLanes_BothThemesAndSizes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { Render(); } catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>Synthetic Gaussian spots on the nominal lattice with nearest labels: layout fixtures, not calibration
    /// evidence. <paramref name="n"/> crystals per side at 32 bins per crystal (the Studio LUT scale).</summary>
    private static ReadoutPreparation Fixture(int n, double pitchMm, ProcessorTiming timing)
    {
        const int binsPerCrystal = 32;
        int bins = n * binsPerCrystal;
        var density = new DetectorImage(bins, bins); var labels = new int[bins * bins];
        for (int y = 0; y < bins; y++) for (int x = 0; x < bins; x++)
        {
            int c = y / binsPerCrystal * n + x / binsPerCrystal; labels[y * bins + x] = c;
            double u = x % binsPerCrystal - 15.5, v = y % binsPerCrystal - 15.5;
            density[x, y] = 100 * Math.Exp(-(u * u + v * v) / 48);
        }
        var peaks = Enumerable.Range(0, n * n).Select(c => ((c % n + .5) / n * 2 - 1, (c / n + .5) / n * 2 - 1)).ToArray();
        var circuit = ChargeDivisionNetwork.Dpc(n, n, 1000, 10).Circuit!;
        return new ReadoutPreparation(Guid.Parse($"40000000-0000-0000-0000-{n:D12}"), "", true, null,
            density.ReadOnlyCopy(), Array.AsReadOnly(labels), Array.AsReadOnly(peaks), ["Synthetic drawing fixture; no measured calibration result."],
            n * n * 2000, n * n * 1700, n * n * 1400, 0, TimeSpan.FromSeconds(11), timing.RiseNs, timing.TailNs, timing.Normalization, timing.SupportNs, timing.ThresholdCodes)
        {
            Circuit = new(Array.AsReadOnly(circuit.NodeNames), Array.AsReadOnly(circuit.Resistors), 0), SensorActiveWidthMm = pitchMm - .1,
            Module = new(n, n, pitchMm, .1, n, n, pitchMm, pitchMm - .1), CorrectionHistogram = Enumerable.Repeat(100, n * n).ToArray(),
            Seeds = "fixture",
        };
    }

    private sealed record ProcessorTiming(double RiseNs, double TailNs, double Normalization, double SupportNs, double ThresholdCodes);

    private void Render()
    {
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "Gcam.sln"))) repo = Directory.GetParent(repo)!.FullName;
        string target = Environment.GetEnvironmentVariable("GCAM_RENDER_OUTPUT")
            ?? throw new InvalidOperationException("Readout renders require explicit GCAM_RENDER_OUTPUT.");
        target = Path.Combine(target, "readout-render"); Directory.CreateDirectory(target);
        var detector = new DetectorSettings { ReadoutMode = ReadoutMode.FourOutputAnger };
        // Real engine timing and held values from one realised hit; drawing density remains synthetic.
        var ro = ReadoutPreparationService.Preset();
        ro.Optics.DepthBins = 1; ro.Optics.PhotonsPerBin = 10;
        ro.Network.Topology = NetworkTopology.IdealBilinear;
        ro.Trigger.Unit = ThresholdUnit.AdcCode; ro.Trigger.Threshold = 400;
        var processor = new ReadoutPulseProcessor(new ReadoutDevice(new DetectorConfig { PixelsX=2, PixelsY=2 },ro,1),ro.Pulse,ro.Trigger);
        double normalization = Math.Exp(-processor.PeakGridNs/processor.TailNs)-Math.Exp(-processor.PeakGridNs/processor.RiseNs);
        var timing = new ProcessorTiming(processor.RiseNs, processor.TailNs, normalization, processor.SupportNs, processor.ThresholdCodes);
        var hit = new ReadoutHit(1000,[1000,500,800,400],2700);
        var stream = processor.CreateStream(new DefaultRandom(2)); stream.Append(hit);
        var conversion = Assert.Single(stream.AdvanceTo(5000));
        var record = new MeasuredReadoutRecord(0, conversion.HoldTimeNs*1e-9, conversion.TriggerTimeNs*1e-9,
            new(conversion.Codes[0],conversion.Codes[1],conversion.Codes[2],conversion.Codes[3]),
            new(conversion.AnalogAtHold[0],conversion.AnalogAtHold[1],conversion.AnalogAtHold[2],conversion.AnalogAtHold[3]),-.25,-.1,65,661.7,0,1,1);
        foreach (var (name, optics) in new[] { ("12x12", Baseline), ("44x44", High) })
        {
            int n = optics.DetectorPixels;
            var template = Fixture(n, optics.PixelPitchMm, timing);
            var model = Model(optics, template);
            var p = model.MatchingPreparation!;
            Assert.Equal(n * n, p.Module!.Crystals);
            var flood = new DetectorImage(n, n); flood.Add(n / 2, n / 2, 1);
            var image = new ImagingResult(flood.ReadOnlyCopy(), -(n - 1) * optics.PixelPitchMm / 2, optics.PixelPitchMm, null, 0, 0, null, 1, TimeSpan.Zero);
            var snapshot = new AcquisitionSnapshot(5e-6,1,200000,1,false,image,[],TimeSpan.Zero,false)
            { Detector=model.Detector, Optics=optics, Readout=new(p,[record with { Crystal = n * n - 1 }],[new(hit.TimeNs*1e-9,new(1000,500,800,400))],0,true) { LiveDensity=p.Density } };
            model.Snapshot = snapshot;
            model.SelectedWorkspace = model.Waveform;
            var scope = PhysicalReadoutWaveform.Process(model.Snapshot, new(0,10,false,50,false),default);
            // The largest crystal index maps to the last column / row of the captured module, not of a fixed 12 × 12.
            Assert.Equal((n - 1, n - 1), (scope.Events[0].PixelX, scope.Events[0].PixelY));
            var sum = scope.PhysicalLanes[4];
            int crossing = Array.FindIndex(sum.Y,y=>y>=p.ThresholdCodes);
            int peakIndex = Array.IndexOf(sum.Y,sum.Y.Max());
            Assert.True(crossing>0); Assert.True(sum.Y[crossing-1]<p.ThresholdCodes);
            double sampleBoundUs = Math.Max(processor.StepNs*1e-3,sum.Step);
            Assert.InRange(Math.Abs(scope.PhysicalMarkers[0].X-sum.XAt(crossing)),0,sampleBoundUs);
            Assert.InRange(Math.Abs(scope.PhysicalMarkers[1].X-sum.XAt(peakIndex)),0,sampleBoundUs);
            model.Waveform.View = scope;
            var failed = Model(optics, template with { Succeeded = false, Failure = $"Fixture: {n * n - 1} of {n * n} peaks found" });
            var pending = Model(optics, null);
            foreach (string theme in new[] { "Dark", "Light" })
            foreach (var size in new[] { new Size(1280,800), new Size(1440,900) })
            {
                foreach (var tab in Enum.GetValues<ReadoutDetectorTab>())
                {
                    model.DetectorWorkspace.ReadoutTab = tab;
                    Capture("ReadoutDetectorView", model.DetectorWorkspace, $"{name}-detector-{tab}", theme, size, tab == ReadoutDetectorTab.Anger || tab == ReadoutDetectorTab.Sipm);
                }
                Capture("ReadoutWaveformView", model.Waveform, $"{name}-four-lanes", theme, size);
                Capture("ImagingOptionsPanel", model.Imaging, $"{name}-imaging-channel", theme, size);
                Capture("ReadoutPanel", model, $"{name}-ready", theme, size);
                Capture("ReadoutPanel", pending, $"{name}-pending", theme, size);
                pending.IsPreparingReadout = true; pending.ReadoutProgressText = $"661.7 keV · Transporting independent calibration flood… · 1,000 / {n * n * 2000:N0} histories (0 %)";
                Capture("ReadoutPanel", pending, $"{name}-preparing", theme, size);
                pending.IsPreparingReadout = false; pending.ReadoutProgressText = null;
                Capture("ReadoutPanel", failed, $"{name}-failed", theme, size);
            }
        }

        MainViewModel Model(OpticsSettings optics, ReadoutPreparation? template)
        {
            var preparation = new FixturePreparation(plan => template ?? throw new OperationCanceledException());
            var model = new MainViewModel(new RefusingAcquisition(), new Theme(), new SpectrumService(), waveform: new WaveformService(),
                detectorFace: new DetectorFaceService(), readoutPreparation: preparation)
            { Optics = optics, AmbientDoseRateMicroSvPerHour = 0, ReadoutMode = ReadoutMode.FourOutputAnger };
            if (template is not null) model.PrepareReadoutCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            return model;
        }

        void Capture(string view, object data, string name, string theme, Size size, bool timeMap = false)
        {
            // Read the production layout; inject only its usual App dictionaries before parsing. No Application,
            // Window, HWND, Show or desktop input. This avoids a second Application in the existing render host.
            string xaml = File.ReadAllText(Path.Combine(repo,"src/Gcam.Studio/Views",view+".xaml"));
            xaml = Regex.Replace(xaml, "x:Class=\"[^\"]+\"", "");
            xaml = xaml.Replace("clr-namespace:Gcam.Studio.Controls", "clr-namespace:Gcam.Studio.Controls;assembly=Gcam.Studio")
                .Replace("clr-namespace:Gcam.Studio.Converters", "clr-namespace:Gcam.Studio.Converters;assembly=Gcam.Studio");
            string dictionaries = string.Concat(new[] { $"Tokens.{theme}", "Metrics", "Typography", "Controls" }
                .Select(f => $"<ResourceDictionary Source='/Gcam.Studio;component/Themes/{f}.xaml' />"));
            string resources = $"<ResourceDictionary.MergedDictionaries>{dictionaries}</ResourceDictionary.MergedDictionaries>";
            if (xaml.Contains("<UserControl.Resources>")) xaml=xaml.Replace("<UserControl.Resources>","<UserControl.Resources><ResourceDictionary>"+resources)
                .Replace("</UserControl.Resources>","</ResourceDictionary></UserControl.Resources>");
            else { int start=xaml.IndexOf('>')+1; xaml=xaml.Insert(start,$"<UserControl.Resources><ResourceDictionary>{resources}</ResourceDictionary></UserControl.Resources>"); }
            var element = (FrameworkElement)XamlReader.Parse(xaml); element.DataContext=data;
            var root = new Border { Child=element, Width=size.Width, Height=size.Height };
            root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
            if (view=="ReadoutWaveformView")
            {
                var plots = Descendants(root).OfType<PlotView>().ToArray(); Assert.Equal(5,plots.Length);
                Assert.All(plots,pv=> { Assert.True(pv.ActualHeight>0); Assert.True(pv.ActualWidth>0); Assert.NotEmpty(pv.Series!); });
                Assert.Equal(2, plots[4].Series!.Count);
            }
            // Help marks (TODO-41): every visible mark is a focusable tab stop whose automation HelpText is its tooltip text.
            static bool Shown(DependencyObject node)
            {
                // Visible itself and through every ancestor (a collapsed tab or expander hides its marks).
                for (var n = node; n is not null; n = VisualTreeHelper.GetParent(n))
                    if (n is UIElement e && e.Visibility != Visibility.Visible) return false;
                return true;
            }
            var marks = Descendants(root).OfType<HelpMark>().Where(Shown).ToArray();
            foreach (var mark in marks)
            {
                Assert.True(mark.Focusable && mark.IsTabStop);
                Assert.False(string.IsNullOrEmpty(mark.Text));
                Assert.Equal(mark.Text, AutomationProperties.GetHelpText(mark));
                Assert.False(string.IsNullOrEmpty(AutomationProperties.GetAutomationId(mark)));
                Assert.True(mark.ActualWidth > 0 && mark.ActualHeight > 0);
            }
            if (view == "ReadoutPanel" || view == "ImagingOptionsPanel" || view == "ReadoutDetectorView")
                output.WriteLine($"HELP {name} {theme} {size.Width:0}x{size.Height:0}: {marks.Length} visible marks ({string.Join(", ", marks.Select(AutomationProperties.GetAutomationId))})");
            var bitmap = new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);
            if (data is DetectorWorkspaceViewModel detectorView && detectorView.ReadoutTab == ReadoutDetectorTab.Calibration
                && detectorView.SensitivitySummary is { } summary)
            {
                var summaryControl = Descendants(root).OfType<HeatmapView>()
                    .Single(h => AutomationProperties.GetAutomationId(h) == "Readout.SensitivityMap");
                Assert.Same(summary, summaryControl.Image);
                Assert.Equal(detectorView.Shared.Optics.DetectorPixels, summary.Width);
                Assert.Contains("never flood-correction weights", detectorView.SensitivityHelp);
            }
            var watch = Stopwatch.StartNew();
            bitmap.Render(root);
            var first = watch.Elapsed;
            if (timeMap)
            {
                var map = Descendants(root).OfType<ReadoutMapView>().Single();
                Assert.True(map.ActualWidth > 0 && map.ActualHeight > 0);
                // A second render reuses the cached raster / geometry (as a redraw after zoom or a live tick does).
                map.InvalidateVisual(); root.UpdateLayout();
                var again = new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);
                watch.Restart(); again.Render(root); var cached = watch.Elapsed;
                map.Zoom = 4; root.UpdateLayout();
                watch.Restart(); again.Render(root); var zoomed = watch.Elapsed;
                output.WriteLine($"MAP {name} {theme} {size.Width:0}x{size.Height:0}: map {map.ActualWidth:0}x{map.ActualHeight:0} DIP; first whole-view render {first.TotalMilliseconds:F1} ms (cache build {map.LastCacheBuild.TotalMilliseconds:F1} ms); cached re-render {cached.TotalMilliseconds:F1} ms; zoom 4x re-render {zoomed.TotalMilliseconds:F1} ms");
                Assert.Contains(view == "ReadoutDetectorView" ? "module" : "", AutomationPeerStatus(map));
            }
            if (view=="ReadoutWaveformView")
            {
                var plots = Descendants(root).OfType<PlotView>().ToArray();
                // Rendered geometry, not just the gutter property: exactly the same X mapping in every lane.
                var reference = plots[0];
                double zero = reference.TranslatePoint(new Point(reference.XToScreen(0),0),root).X;
                foreach (var plot in plots)
                {
                    Assert.Equal(80,plot.PlotAreaBounds.Left);
                    Assert.Equal(reference.PlotAreaBounds.Width,plot.PlotAreaBounds.Width);
                    Assert.Equal(reference.CurrentViewRange,plot.CurrentViewRange);
                    Assert.Equal(zero,plot.TranslatePoint(new Point(plot.XToScreen(0),0),root).X);
                }
                output.WriteLine($"{theme} {size}: five lanes share left=80 DIP and zero={zero:R} DIP exactly.");
            }
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string path=Path.Combine(target,$"{name}-{theme.ToLowerInvariant()}-{size.Width:0}x{size.Height:0}.png");
            using(var file=File.Create(path)) encoder.Save(file); output.WriteLine(path); element.DataContext=null;
        }
    }

    private static string AutomationPeerStatus(ReadoutMapView map)
        => System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(map).GetItemStatus();

    internal static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    { for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) { var c=VisualTreeHelper.GetChild(node,i); yield return c; foreach(var d in Descendants(c)) yield return d; } }
    private sealed class Theme : IThemeService { public AppTheme Current => AppTheme.Dark; public void Apply(AppTheme theme) { } }
    private sealed class RefusingAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene,OpticsSettings optics,double liveTimeS,double speed,DetectorSettings? detector=null,double backgroundToSignalRatio=0,int? seed=null) => throw new NotSupportedException();
    }

    /// <summary>The real embedded record; preparations come from a drawing fixture stamped with the request's keys.</summary>
    private sealed class FixturePreparation(Func<ReadoutLinePlan, ReadoutPreparation> template) : IReadoutPreparationService
    {
        private readonly Dictionary<string, ReadoutPreparation> _cache = [];
        public event EventHandler? CacheChanged;
        public ReadoutPreparation? FindCached(string key) => _cache.GetValueOrDefault(key);
        public ReadoutOfferRecord Offers { get; } = ReadoutOfferRecordLoader.LoadEmbedded();
        public Task<ReadoutPreparation> PrepareAsync(OpticsSettings optics, DetectorSettings detector, ReadoutLinePlan line,
            IProgress<ReadoutPrepareProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            string module = ReadoutPolicy.Key(optics, detector);
            var result = template(line) with
            {
                Id = Guid.NewGuid(), Key = module, LineKeV = line.LineKeV, Plan = line,
                CalibrationKey = ReadoutPolicy.CalibrationKey(module, line),
            };
            _cache[result.CalibrationKey] = result;
            CacheChanged?.Invoke(this, EventArgs.Empty);
            return Task.FromResult(result);
        }
    }
}
