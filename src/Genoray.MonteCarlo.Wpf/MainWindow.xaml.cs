using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Detector;
using Genoray.MonteCarlo.Simulation;
using SimBackground = Genoray.MonteCarlo.Simulation.Background;

namespace Genoray.MonteCarlo.Wpf;

/// <summary>A viewer over the GCAM core. A 2D scene editor (top) holds the sources — each with an isotope,
/// activity (µCi), and distance to the detector — and a single Simulate button drives whichever analysis tab
/// is active: shape the ADC waveform, image the field, or count the deposited-energy spectrum over a real
/// acquisition time. Heavy MC work runs off the UI thread; ScottPlot renders the results.</summary>
public partial class MainWindow : Window
{
    private const double AdcSampleRateHz = 125e6;   // AD9648 125 MSPS
    private const double MmSpan = 60.0;             // ±30 mm visible on the canvas
    private const double DetectorSizeMm = 12.0;     // 12×12 mm detector footprint

    private readonly List<SceneSource> _scene = [];
    private SceneSource? _selected;
    private SceneSource? _dragging;
    private bool _syncing;                          // guard property write-back while the UI is being synced

    public MainWindow()
    {
        InitializeComponent();
        foreach (var iso in Isotopes.All) PropIsotope.Items.Add(iso.Name);

        _scene.Add(new SceneSource { Isotope = "Cs-137", X = 0, Y = 0, DistanceMm = 160, ActivityUCi = 10 });
        RefreshSourceList(select: 0);

        Loaded += (_, _) => RedrawScene();
        SceneCanvas.SizeChanged += (_, _) => RedrawScene();
        SceneCanvas.MouseMove += SceneCanvas_MouseMove;
        SceneCanvas.MouseLeftButtonUp += SceneCanvas_MouseUp;
    }

    // ---- scene state / property panel --------------------------------------------------------------------

    private void RefreshSourceList(int select)
    {
        _syncing = true;
        SourceList.Items.Clear();
        foreach (var s in _scene) SourceList.Items.Add(s.ToString());
        if (select >= 0 && select < _scene.Count) SourceList.SelectedIndex = select;
        _syncing = false;
        SelectSource(select >= 0 && select < _scene.Count ? _scene[select] : null);
    }

    private void SelectSource(SceneSource? s)
    {
        _selected = s;
        _syncing = true;
        if (s != null)
        {
            PropIsotope.SelectedItem = s.Isotope;
            PropActivity.Text = s.ActivityUCi.ToString("F0", CultureInfo.InvariantCulture);
            PropDistance.Value = s.DistanceMm;
            PropDistanceLabel.Text = $"{s.DistanceMm:F0} mm";
            PropXY.Text = $"x = {s.X:F1} mm,  y = {s.Y:F1} mm    (drag on the canvas to move)";
        }
        else
        {
            PropActivity.Text = "";
            PropDistanceLabel.Text = "";
            PropXY.Text = "no source selected";
        }
        _syncing = false;
        RedrawScene();
    }

    private void UpdateSelectedText()
    {
        if (_selected == null) return;
        int i = _scene.IndexOf(_selected);
        if (i >= 0)
        {
            _syncing = true;
            SourceList.Items[i] = _selected.ToString();
            SourceList.SelectedIndex = i;
            _syncing = false;
        }
        RedrawScene();
    }

    private void SourceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing) return;
        int i = SourceList.SelectedIndex;
        SelectSource(i >= 0 && i < _scene.Count ? _scene[i] : null);
    }

    private void AddSource_Click(object sender, RoutedEventArgs e)
    {
        _scene.Add(new SceneSource());
        RefreshSourceList(_scene.Count - 1);
    }

    private void RemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        int i = _scene.IndexOf(_selected);
        _scene.Remove(_selected);
        RefreshSourceList(Math.Min(i, _scene.Count - 1));
    }

    private void PropIsotope_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _selected == null) return;
        if (PropIsotope.SelectedItem is string n) { _selected.Isotope = n; UpdateSelectedText(); }
    }

    private void PropActivity_Changed(object sender, RoutedEventArgs e)
    {
        if (_syncing || _selected == null) return;
        _selected.ActivityUCi = Math.Max(0.0, ParseD(PropActivity.Text, _selected.ActivityUCi));
        UpdateSelectedText();
    }

    private void PropDistance_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing || _selected == null) return;
        _selected.DistanceMm = PropDistance.Value;
        PropDistanceLabel.Text = $"{_selected.DistanceMm:F0} mm";
        UpdateSelectedText();
    }

    // ---- canvas rendering + interaction ------------------------------------------------------------------

    private (double s, double cw, double ch) CanvasMetrics()
    {
        double cw = SceneCanvas.ActualWidth > 0 ? SceneCanvas.ActualWidth : SceneCanvas.Width;
        double ch = SceneCanvas.ActualHeight > 0 ? SceneCanvas.ActualHeight : SceneCanvas.Height;
        return (Math.Min(cw, ch) / MmSpan, cw, ch);
    }

    private Point MmToPx(double xmm, double ymm)
    {
        var (s, cw, ch) = CanvasMetrics();
        return new Point(cw / 2 + xmm * s, ch / 2 - ymm * s);   // y up
    }

    private (double x, double y) PxToMm(Point p)
    {
        var (s, cw, ch) = CanvasMetrics();
        return ((p.X - cw / 2) / s, -(p.Y - ch / 2) / s);
    }

    private static double DotRadius(double distanceMm) =>
        Math.Clamp(16.0 * (160.0 / Math.Max(distanceMm, 1.0)), 5.0, 22.0);   // nearer source = bigger dot

    private static Brush IsotopeBrush(string isotope) => isotope switch
    {
        "Cs-137" => Brushes.Crimson,
        "Co-60" => Brushes.DarkOrange,
        "Co-57" => Brushes.MediumSeaGreen,
        "Na-22" => Brushes.RoyalBlue,
        "Am-241" => Brushes.MediumPurple,
        _ => Brushes.Gray,
    };

    private void RedrawScene()
    {
        var (s, cw, ch) = CanvasMetrics();
        if (cw <= 0 || ch <= 0) return;
        SceneCanvas.Children.Clear();

        AddLine(cw / 2, 0, cw / 2, ch, Brushes.Gainsboro);
        AddLine(0, ch / 2, cw, ch / 2, Brushes.Gainsboro);

        double dpx = DetectorSizeMm * s;
        var det = new Rectangle
        {
            Width = dpx, Height = dpx, Stroke = Brushes.SteelBlue, StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Color.FromArgb(40, 70, 130, 180)),
        };
        Canvas.SetLeft(det, cw / 2 - dpx / 2);
        Canvas.SetTop(det, ch / 2 - dpx / 2);
        SceneCanvas.Children.Add(det);
        AddText("detector", cw / 2 - dpx / 2, ch / 2 - dpx / 2 - 14, Brushes.SteelBlue);

        foreach (var src in _scene)
        {
            var p = MmToPx(src.X, src.Y);
            double r = DotRadius(src.DistanceMm);
            var dot = new Ellipse
            {
                Width = 2 * r, Height = 2 * r, Fill = IsotopeBrush(src.Isotope),
                Stroke = src == _selected ? Brushes.Black : Brushes.White,
                StrokeThickness = src == _selected ? 2.5 : 1.0,
            };
            Canvas.SetLeft(dot, p.X - r);
            Canvas.SetTop(dot, p.Y - r);
            SceneCanvas.Children.Add(dot);
            AddText($"{src.Isotope} · {src.ActivityUCi:F0}µCi", p.X + r + 2, p.Y - 8, Brushes.Black);
        }
    }

    private void AddLine(double x1, double y1, double x2, double y2, Brush b) =>
        SceneCanvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = b, StrokeThickness = 1 });

    private void AddText(string text, double left, double top, Brush b)
    {
        var t = new TextBlock { Text = text, FontSize = 10, Foreground = b };
        Canvas.SetLeft(t, left);
        Canvas.SetTop(t, top);
        SceneCanvas.Children.Add(t);
    }

    private SceneSource? HitTest(Point p)
    {
        SceneSource? best = null;
        double bestD = double.MaxValue;
        foreach (var src in _scene)
        {
            var q = MmToPx(src.X, src.Y);
            double d = (q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y);
            double r = DotRadius(src.DistanceMm) + 3;
            if (d <= r * r && d < bestD) { bestD = d; best = src; }
        }
        return best;
    }

    private void SceneCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(SceneCanvas);
        var hit = HitTest(pos);
        if (hit == null)
        {
            var (x, y) = PxToMm(pos);
            hit = new SceneSource { X = Math.Round(x, 1), Y = Math.Round(y, 1) };
            _scene.Add(hit);
            RefreshSourceList(_scene.Count - 1);
        }
        else
        {
            RefreshSourceList(_scene.IndexOf(hit));
        }
        _dragging = hit;
        SceneCanvas.CaptureMouse();
    }

    private void SceneCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging == null || e.LeftButton != MouseButtonState.Pressed) return;
        var (x, y) = PxToMm(e.GetPosition(SceneCanvas));
        _dragging.X = Math.Round(Math.Clamp(x, -MmSpan / 2, MmSpan / 2), 1);
        _dragging.Y = Math.Round(Math.Clamp(y, -MmSpan / 2, MmSpan / 2), 1);
        if (_dragging == _selected)
            PropXY.Text = $"x = {_dragging.X:F1} mm,  y = {_dragging.Y:F1} mm";
        RedrawScene();
    }

    private void SceneCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging == null) return;
        int i = _scene.IndexOf(_dragging);
        _dragging = null;
        SceneCanvas.ReleaseMouseCapture();
        RefreshSourceList(i);
    }

    // ---- config from the scene ---------------------------------------------------------------------------

    private SimulationConfig ConfigFromScene(long photons, double bsr = 0.0)
    {
        var srcs = _scene.Select(s => s.ToConfig()).ToArray();
        var cfg = new SimulationConfig
        {
            PhotonCount = photons,
            Seed = 12345,
            Source = srcs.Length > 0 ? srcs[0] : new SourceConfig(),
            Sources = srcs.Length > 0 ? srcs : null,
        };
        if (bsr > 0.0)
            cfg.Background = new BackgroundConfig { BackgroundToSignalRatio = bsr, EnergyKeV = 200.0 };
        return cfg;
    }

    private double EmissionRatePerSec() => _scene.Sum(s => s.EmissionRatePerSec());

    // ============================ SIMULATE dispatch ============================

    private async void Simulate_Click(object sender, RoutedEventArgs e)
    {
        if (_scene.Count == 0) { MessageBox.Show("Add at least one source to the scene."); return; }
        switch (MainTabs.SelectedIndex)
        {
            case 0: await RunWaveform(); break;
            case 1: await RunImaging(); break;
            case 2: await RunSpectrum(); break;
        }
    }

    private async Task RunWaveform()
    {
        double rateKcps = ParseD(WfRate.Text, 50);
        int nEvents = Math.Clamp(ParseI(WfEvents.Text, 40), 1, 4000);
        bool realistic = WfRealistic.IsChecked == true;
        bool crrc = WfShaper.SelectedIndex == 1;
        var cfg = ConfigFromScene(200_000);

        WfStatus.Text = "running…";
        try
        {
            var (adc, shaped) = await Task.Run(() =>
            {
                var events = new EventStreamStudy().Generate(cfg, rateKcps * 1000.0, AdcSampleRateHz, nEvents);
                var stream = events.Select(ev => (ev.ArrivalSample, ev.EnergyKeV)).ToList();
                var preset = Waveform.DefaultAdc;
                int[] wave = realistic
                    ? Waveform.Rasterize(stream, preset)
                    : Waveform.Rasterize(stream, preset, tauRise: 0.0, noiseKev: 0.0, intrinsicFwhm: 0.0);
                long[] sh = crrc ? Waveform.CrrcInt(wave) : Waveform.TrapShape(wave);
                return (wave, sh);
            });

            DrawSignal(WfAdcPlot, ToD(adc),
                "ADC waveform — scene sources (bi-exp pulses, pile-up, noise, clip)",
                "sample  (8 ns @ 125 MSPS)", "ADC code");
            DrawSignal(WfShapedPlot, ToD(shaped),
                (crrc ? "CR-RC^4" : "Trapezoidal") + " shaped  (flat top ∝ deposited energy)",
                "sample", "shaper output");
            WfStatus.Text = $"{nEvents} events @ {rateKcps:F0} kcps display rate  ·  " +
                            $"isotopes: {string.Join(", ", _scene.Select(s => s.Isotope).Distinct())}";
        }
        catch (Exception ex) { WfStatus.Text = "error: " + ex.Message; }
    }

    private async Task RunImaging()
    {
        long photons = (long)ParseD(ImgPhotons.Text, 500_000);
        double bsr = ParseD(ImgBsr.Text, 0);
        var cfg = ConfigFromScene(photons, bsr);
        var truePos = _scene.Select(s => (s.X, s.Y)).ToList();

        ImgStatus.Text = "running…";
        try
        {
            var (flood, dec) = await Task.Run(() =>
            {
                var factory = new DefaultSimulationFactory();
                var res = new SimulationRunner(factory).Run(cfg);
                var fl = res.DetectorImage;
                if (bsr > 0.0)
                {
                    double ped = SimBackground.PedestalPerPixel(bsr, SumImage(fl), fl.Width * fl.Height);
                    SimBackground.AddUniform(fl, ped);
                }
                var d = factory.CreateDecoder(cfg)!.Decode(fl);
                return (fl, d);
            });

            DrawHeatmap(ImgFloodPlot, ToGrid(flood), 0, flood.Width, 0, flood.Height,
                "Detector flood map (counts)", "pixel x", "pixel y", null, null);

            var recon = dec.Reconstruction;
            double left = dec.ReconOriginMm, right = dec.ReconOriginMm + (recon.Width - 1) * dec.ReconStepMm;
            double bottom = dec.ReconOriginMm, top = dec.ReconOriginMm + (recon.Height - 1) * dec.ReconStepMm;
            DrawHeatmap(ImgReconPlot, ToGrid(recon), left, right, bottom, top,
                "Decoded reconstruction (× sources, ○ estimate)", "x (mm)", "y (mm)",
                truePos, (dec.Estimate.Position.X, dec.Estimate.Position.Y));

            ImgStatus.Text = $"peak estimate ({dec.Estimate.Position.X:F2}, {dec.Estimate.Position.Y:F2}) mm" +
                             (_scene.Count > 1 ? $"  ·  {_scene.Count} sources" : "") +
                             (bsr > 0 ? $"  ·  BSR {bsr:F1}" : "");
        }
        catch (Exception ex) { ImgStatus.Text = "error: " + ex.Message; }
    }

    private async Task RunSpectrum()
    {
        double time = ParseD(SpTime.Text, 60);
        double windowFrac = ParseD(SpWindow.Text, 10) / 100.0;
        double energyRef = _scene.Count > 0 ? Isotopes.Get(_scene[0].Isotope).Lines[0].EnergyKeV : 661.7;
        double emissionRate = EmissionRatePerSec();
        var cfg = ConfigFromScene(400_000);

        SpStatus.Text = "running…";
        try
        {
            var (centers, counts, rate, total) = await Task.Run(() =>
            {
                var factory = new DefaultSimulationFactory();
                var res = new SimulationRunner(factory).Run(cfg);
                double eff = res.PhotonsEmitted > 0 ? res.DetectedWeight / res.PhotonsEmitted : 0.0;
                double detRate = emissionRate * eff;            // detected counts / sec
                double n = detRate * time;                      // expected total detected counts

                // deposit-spectrum SHAPE from a representative pool, then scale to n and Poisson-realize.
                var pool = new EventStreamStudy()
                    .Generate(cfg, Math.Max(detRate, 1.0), AdcSampleRateHz, 20000)
                    .Select(ev => ev.EnergyKeV).ToArray();
                double maxE = _scene
                    .SelectMany(s => Isotopes.Get(s.Isotope).Lines.Select(l => l.EnergyKeV))
                    .DefaultIfEmpty(energyRef).Max() * 1.15;

                const int bins = 128;
                double bw = maxE / bins;
                var pdf = new double[bins];
                foreach (var d in pool) { int b = (int)(d / bw); if (b >= 0 && b < bins) pdf[b] += 1; }
                double poolN = Math.Max(pool.Length, 1);
                var rng = new DefaultRandom(4242);
                var cnt = new double[bins];
                var ctr = new double[bins];
                double tot = 0;
                for (int i = 0; i < bins; i++)
                {
                    ctr[i] = (i + 0.5) * bw;
                    cnt[i] = Sampling.Poisson(rng, pdf[i] / poolN * n);
                    tot += cnt[i];
                }
                return (ctr, cnt, detRate, tot);
            });

            double lo = energyRef * (1.0 - windowFrac), hi = energyRef * (1.0 + windowFrac);
            DrawSpectrum(SpPlot, centers, counts, lo, hi,
                $"Deposited-energy spectrum · {time:F0}s acquisition", "deposited energy (keV)", "counts");
            double uci = _scene.Sum(s => s.ActivityUCi);
            SpStatus.Text = $"{uci:F0} µCi total  ·  {rate:F0} cps detected  ·  {total:F0} counts in {time:F0}s";
        }
        catch (Exception ex) { SpStatus.Text = "error: " + ex.Message; }
    }

    // ---- plotting helpers --------------------------------------------------------------------------------

    private static double[] ToD(int[] xs) { var d = new double[xs.Length]; for (int i = 0; i < xs.Length; i++) d[i] = xs[i]; return d; }
    private static double[] ToD(long[] xs) { var d = new double[xs.Length]; for (int i = 0; i < xs.Length; i++) d[i] = xs[i]; return d; }

    private static double SumImage(DetectorImage img)
    {
        double s = 0;
        foreach (var v in img.Raw) s += v;
        return s;
    }

    private static double[,] ToGrid(DetectorImage img)
    {
        var g = new double[img.Height, img.Width];
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
                g[y, x] = img[x, y];
        return g;
    }

    private static void DrawSignal(ScottPlot.WPF.WpfPlot view, double[] ys, string title, string xlabel, string ylabel)
    {
        var p = view.Plot;
        p.Clear();
        p.Add.Signal(ys);
        p.Title(title);
        p.XLabel(xlabel);
        p.YLabel(ylabel);
        p.Axes.AutoScale();
        view.Refresh();
    }

    private static void DrawHeatmap(ScottPlot.WPF.WpfPlot view, double[,] grid,
        double left, double right, double bottom, double top, string title, string xlabel, string ylabel,
        IReadOnlyList<(double x, double y)>? truePositions, (double x, double y)? estPos)
    {
        var p = view.Plot;
        p.Clear();
        var hm = p.Add.Heatmap(grid);
        hm.Extent = new ScottPlot.CoordinateRect(left, right, bottom, top);
        hm.Colormap = new ScottPlot.Colormaps.Viridis();
        p.Add.ColorBar(hm);

        if (truePositions != null)
            foreach (var tp in truePositions)
            {
                var m = p.Add.Marker(tp.x, tp.y);
                m.Color = ScottPlot.Colors.White;
                m.Size = 16;
                m.Shape = ScottPlot.MarkerShape.Cross;
            }
        if (estPos is { } ep)
        {
            var m = p.Add.Marker(ep.x, ep.y);
            m.Color = ScottPlot.Colors.Red;
            m.Size = 18;
            m.Shape = ScottPlot.MarkerShape.OpenCircle;
        }

        p.Title(title);
        p.XLabel(xlabel);
        p.YLabel(ylabel);
        p.Axes.AutoScale();
        view.Refresh();
    }

    private static void DrawSpectrum(ScottPlot.WPF.WpfPlot view, double[] centers, double[] counts,
        double windowLo, double windowHi, string title, string xlabel, string ylabel)
    {
        var p = view.Plot;
        p.Clear();

        double bw = centers.Length > 1 ? centers[1] - centers[0] : 1.0;
        var bars = new List<ScottPlot.Bar>(centers.Length);
        for (int i = 0; i < centers.Length; i++)
            bars.Add(new ScottPlot.Bar { Position = centers[i], Value = counts[i], Size = bw });
        p.Add.Bars(bars);

        var vlo = p.Add.VerticalLine(windowLo);
        vlo.Color = ScottPlot.Colors.Red; vlo.LineWidth = 2;
        var vhi = p.Add.VerticalLine(windowHi);
        vhi.Color = ScottPlot.Colors.Red; vhi.LineWidth = 2;

        p.Title(title);
        p.XLabel(xlabel);
        p.YLabel(ylabel);
        p.Axes.AutoScale();
        view.Refresh();
    }

    private static double ParseD(string s, double fallback) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static int ParseI(string s, int fallback) =>
        int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
