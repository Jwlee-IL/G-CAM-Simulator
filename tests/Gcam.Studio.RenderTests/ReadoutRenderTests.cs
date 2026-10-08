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

/// <summary>Production readout XAML rendered detached on an STA, with synthetic drawing-only records.</summary>
public sealed class ReadoutRenderTests(ITestOutputHelper output)
{
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
                var model = new MainViewModel(new RefusingAcquisition(), new Theme(), new SpectrumService());
                var panel = ProductionView("ReadoutPanel", "Dark");
                panel.DataContext = model;
                panel.Measure(new Size(400, 800));
                panel.Arrange(new Rect(0, 0, 400, 800));
                Drain();
                var mode = Descendants(panel).OfType<ComboBox>().Single();
                Assert.Equal(ReadoutMode.DirectCrystal, mode.SelectedItem);
                mode.SelectedIndex = 1;
                Drain();
                Assert.Equal(ReadoutMode.DirectCrystal, model.ReadoutMode);
                Assert.Equal(ReadoutMode.DirectCrystal, mode.SelectedItem);
                Assert.Equal(0, mode.SelectedIndex);
                Assert.Equal("DirectCrystal", mode.Text);
                Assert.Contains("12 × 12", model.ReadoutMessage);

                model.Optics = OpticsPreset.All.Single(p => p.Name == "Baseline").Settings!;
                mode.SelectedIndex = 1;
                Drain();
                Assert.Equal(ReadoutMode.FourOutputAnger, mode.SelectedItem);
                Assert.True(model.IsPhysicalReadout);

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
                Assert.Equal(2, preset.SelectedIndex);
                preset.SelectedIndex = 3; // Wide FOV is unsupported by the physical reference head.
                Drain();
                Assert.Equal("Baseline", ((OpticsPreset)preset.SelectedItem).Name);
                Assert.Equal(2, preset.SelectedIndex);
                Assert.Equal(12, model.Optics.DetectorPixels);
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

        static void Drain()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }

    private static FrameworkElement ProductionView(string view, string theme)
    {
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "Gcam.sln"))) repo = Directory.GetParent(repo)!.FullName;
        string xaml = File.ReadAllText(Path.Combine(repo, "src/Gcam.Studio/Views", view + ".xaml"));
        xaml = Regex.Replace(xaml, "x:Class=\"[^\"]+\"", "");
        xaml = xaml.Replace("clr-namespace:Gcam.Studio.Controls", "clr-namespace:Gcam.Studio.Controls;assembly=Gcam.Studio")
            .Replace("clr-namespace:Gcam.Studio.Converters", "clr-namespace:Gcam.Studio.Converters;assembly=Gcam.Studio");
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

    private void Render()
    {
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "Gcam.sln"))) repo = Directory.GetParent(repo)!.FullName;
        string target = Environment.GetEnvironmentVariable("GCAM_RENDER_OUTPUT")
            ?? throw new InvalidOperationException("Readout renders require explicit GCAM_RENDER_OUTPUT.");
        target = Path.Combine(target, "readout-render"); Directory.CreateDirectory(target);
        var optics = new OpticsSettings { DetectorPixels = 12, PixelPitchMm = 1, MuraRank = 7, CellPitchMm = 1, MaskDetectorDistanceMm = 60 };
        var detector = new DetectorSettings { ReadoutMode = ReadoutMode.FourOutputAnger };
        // Synthetic 12×12 Gaussian spots and nearest labels: layout fixtures, not calibration evidence.
        var density = new DetectorImage(96, 96); var labels = new int[96 * 96];
        var peaks = Enumerable.Range(0,144).Select(c => ((c % 12 + .5) / 6 - 1, (c / 12 + .5) / 6 - 1)).ToArray();
        for (int y=0;y<96;y++) for (int x=0;x<96;x++)
        {
            int c = y / 8 * 12 + x / 8; labels[y*96+x] = c;
            density[x,y] = 100 * Math.Exp(-((x % 8 - 3.5)*(x % 8 - 3.5)+(y % 8 - 3.5)*(y % 8 - 3.5))/3);
        }
        var circuit = Gcam.Detector.ChargeDivisionNetwork.Dpc(12,12,1000,10).Circuit!;
        // Real engine timing and held values from one realised hit; drawing density remains synthetic.
        var ro = ReadoutPreparationService.Preset();
        ro.Optics.DepthBins = 1; ro.Optics.PhotonsPerBin = 10;
        ro.Network.Topology = NetworkTopology.IdealBilinear;
        ro.Trigger.Unit = ThresholdUnit.AdcCode; ro.Trigger.Threshold = 400;
        var processor = new ReadoutPulseProcessor(new ReadoutDevice(new DetectorConfig { PixelsX=2, PixelsY=2 },ro,1),ro.Pulse,ro.Trigger);
        double normalization = Math.Exp(-processor.PeakGridNs/processor.TailNs)-Math.Exp(-processor.PeakGridNs/processor.RiseNs);
        var hit = new ReadoutHit(1000,[1000,500,800,400],2700);
        var stream = processor.CreateStream(new DefaultRandom(2)); stream.Append(hit);
        var conversion = Assert.Single(stream.AdvanceTo(5000));
        var p = new ReadoutPreparation(Guid.Parse("40000000-0000-0000-0000-000000000001"), ReadoutPolicy.Key(optics, detector), true, null,
            density.ReadOnlyCopy(), Array.AsReadOnly(labels), Array.AsReadOnly(peaks), ["Synthetic drawing fixture; no measured calibration result."],
            288000,250000,200000,0,TimeSpan.FromSeconds(11),processor.RiseNs,processor.TailNs,normalization,processor.SupportNs,processor.ThresholdCodes)
        { Circuit = new(Array.AsReadOnly(circuit.NodeNames), Array.AsReadOnly(circuit.Resistors),0), SensorActiveWidthMm=.9 };
        var model = new MainViewModel(new RefusingAcquisition(), new Theme(), new SpectrumService(), waveform: new WaveformService(), detectorFace: new DetectorFaceService())
        { Optics = optics, AmbientDoseRateMicroSvPerHour = 0, ReadoutMode = ReadoutMode.FourOutputAnger, PreparedReadout = p };
        var record = new MeasuredReadoutRecord(0, conversion.HoldTimeNs*1e-9, conversion.TriggerTimeNs*1e-9,
            new(conversion.Codes[0],conversion.Codes[1],conversion.Codes[2],conversion.Codes[3]),
            new(conversion.AnalogAtHold[0],conversion.AnalogAtHold[1],conversion.AnalogAtHold[2],conversion.AnalogAtHold[3]),-.25,-.1,65,661.7,0,1,1);
        var flood = new DetectorImage(12,12); flood.Add(5,5,1);
        var image = new ImagingResult(flood.ReadOnlyCopy(),-5.5,1,null,0,0,null,1,TimeSpan.Zero);
        model.Snapshot = new(5e-6,1,200000,1,false,image,[],TimeSpan.Zero,false)
        { Detector=detector, Optics=optics, Readout=new(p,[record],[new(hit.TimeNs*1e-9,new(1000,500,800,400))],0,true) { LiveDensity=density.ReadOnlyCopy() } };
        model.SelectedWorkspace = model.Waveform;
        var measuredSnapshot = model.Snapshot;
        var scope = PhysicalReadoutWaveform.Process(model.Snapshot, new(0,10,false,50,false),default);
        var sum = scope.PhysicalLanes[4];
        int crossing = Array.FindIndex(sum.Y,y=>y>=p.ThresholdCodes);
        int peakIndex = Array.IndexOf(sum.Y,sum.Y.Max());
        Assert.True(crossing>0); Assert.True(sum.Y[crossing-1]<p.ThresholdCodes);
        double sampleBoundUs = Math.Max(processor.StepNs*1e-3,sum.Step);
        Assert.InRange(Math.Abs(scope.PhysicalMarkers[0].X-sum.XAt(crossing)),0,sampleBoundUs);
        Assert.InRange(Math.Abs(scope.PhysicalMarkers[1].X-sum.XAt(peakIndex)),0,sampleBoundUs);
        Assert.InRange(record.HoldTimeS-record.TriggerTimeS,0,processor.HoldWindowNs*1e-9);
        model.Waveform.View = scope;
        foreach (string theme in new[] { "Dark", "Light" })
        foreach (var size in new[] { new Size(1280,800), new Size(1440,900) })
        {
            model.Snapshot = measuredSnapshot;
            foreach (var tab in Enum.GetValues<ReadoutDetectorTab>())
            {
                model.DetectorWorkspace.ReadoutTab = tab;
                Capture("ReadoutDetectorView", model.DetectorWorkspace, $"detector-{tab}");
            }
            Capture("ReadoutWaveformView", model.Waveform, "four-lanes");
            Capture("ReadoutPanel", model, "ready");
            model.Snapshot = null; model.PreparedReadout = null;
            Capture("ReadoutPanel", model, "pending");
            model.IsPreparingReadout = true; model.ReadoutMessage = "Preparing optical table and DC network…";
            Capture("ReadoutPanel", model, "preparing");
            model.IsPreparingReadout = false; model.ReadoutMessage = null;
            model.PreparedReadout = p with { Succeeded=false, Failure="Fixture: 143 of 144 peaks found" };
            model.Snapshot = null;
            Capture("ReadoutPanel", model, "failed");
            model.PreparedReadout = p;
            void Capture(string view, object data, string name)
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
                var bitmap = new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32); bitmap.Render(root);
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
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    { for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) { var c=VisualTreeHelper.GetChild(node,i); yield return c; foreach(var d in Descendants(c)) yield return d; } }
    private sealed class Theme : IThemeService { public AppTheme Current => AppTheme.Dark; public void Apply(AppTheme theme) { } }
    private sealed class RefusingAcquisition : IAcquisitionService
    {
        public IAcquisitionSession Start(IReadOnlyList<SceneSource> scene,OpticsSettings optics,double liveTimeS,double speed,DetectorSettings? detector=null,double backgroundToSignalRatio=0,int? seed=null) => throw new NotSupportedException();
    }
}
