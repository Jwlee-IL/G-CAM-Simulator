using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    private readonly List<SceneSource> _scene = [];
    private SceneSource? _selected;
    private SceneSource? _dragging;
    private bool _syncing;                          // guard property write-back while the UI is being synced

    // Last decoded reconstruction, painted onto the scene canvas as a background heatmap (truth vs decode).
    private WriteableBitmap? _reconBmp;
    private double _reconLeftMm, _reconRightMm, _reconBottomMm, _reconTopMm;

    // Live (continuous) acquisition: one MC run fixes the shape, then a timer accumulates Poisson counts.
    private System.Windows.Threading.DispatcherTimer? _liveTimer;
    private bool _liveRunning;
    private int _liveTab;                    // 1 = imaging, 2 = spectrum
    private double _liveRateCps, _liveElapsedSec, _liveTotalCounts, _liveSpeed;
    private IRandom? _liveRng;
    private DetectorImage? _floodAccum;      // imaging: accumulated flood map
    private double[]? _floodShape;           // imaging: per-pixel mean, Σ = 1
    private IDecoder? _liveDecoder;
    private IReadOnlyList<(double x, double y)>? _liveTruePos;
    private double[]? _specPdf, _specCounts, _specCenters;   // spectrum accumulators
    private double _specWindowLo, _specWindowHi;

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
        MainTabs.SelectionChanged += (_, e) => { if (e.Source is TabControl && _liveRunning) StopLive(); };
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
        var (_, cw, ch) = CanvasMetrics();
        if (cw <= 0 || ch <= 0) return;
        SceneCanvas.Children.Clear();

        // 1. Reconstruction heatmap (behind everything): the decoder's view of the field, in the SAME x,y mm
        //    space as the placed sources — so the overlaid dots show truth vs decode directly.
        if (_reconBmp != null)
        {
            var rtl = MmToPx(_reconLeftMm, _reconTopMm);
            var rbr = MmToPx(_reconRightMm, _reconBottomMm);
            var im = new Image
            {
                Source = _reconBmp,
                Width = Math.Max(1, rbr.X - rtl.X),
                Height = Math.Max(1, rbr.Y - rtl.Y),
                Stretch = Stretch.Fill,
            };
            RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.Linear);
            Canvas.SetLeft(im, rtl.X);
            Canvas.SetTop(im, rtl.Y);
            SceneCanvas.Children.Add(im);
        }

        // 2. Axes through the origin (on-axis).
        AddLine(cw / 2, 0, cw / 2, ch, Brushes.Gainsboro);
        AddLine(0, ch / 2, cw, ch / 2, Brushes.Gainsboro);

        // 3. Fully-coded FOV boundary (NOT the detector — the detector sits at z=0, a different plane). A source
        //    inside this box decodes to a single clean peak; outside it aliases into a ghost.
        double half = FcfovHalfMm();
        var tl = MmToPx(-half, half);
        var br = MmToPx(half, -half);
        var fov = new Rectangle
        {
            Width = Math.Max(1, br.X - tl.X), Height = Math.Max(1, br.Y - tl.Y),
            Stroke = Brushes.SteelBlue, StrokeThickness = 1.5, StrokeDashArray = [4, 3],
        };
        Canvas.SetLeft(fov, tl.X);
        Canvas.SetTop(fov, tl.Y);
        SceneCanvas.Children.Add(fov);
        AddText($"FCFOV ±{half:F0}mm (clean-decode region)", tl.X, tl.Y - 14, Brushes.SteelBlue);

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

    /// <summary>Fully-coded FOV half-extent at the nominal source plane, from the same geometry the decoder
    /// uses to size its reconstruction grid: period = rank·cellPitch / (maskZ/sourceZ), half = period/2.</summary>
    private static double FcfovHalfMm()
    {
        var d = new SimulationConfig();   // scene runs use the default geometry/mask
        double maskZ = d.Geometry.MaskDetectorDistanceMm;
        double sourceZ = maskZ + d.Geometry.SourceMaskDistanceMm;
        double period = d.Mask.Rank * d.Mask.CellPitchMm / (maskZ / sourceZ);
        return period / 2.0;
    }

    /// <summary>Rasterize a reconstruction image to a canvas-overlay bitmap (viridis, semi-transparent). Row 0
    /// of the bitmap is the TOP of the screen, so the recon is flipped vertically (its gy=0 is the min-y row).</summary>
    private void SetReconOverlay(DetectorImage recon, double originMm, double stepMm)
    {
        int w = recon.Width, h = recon.Height;
        double min = double.MaxValue, max = double.MinValue;
        foreach (var v in recon.Raw) { if (v < min) min = v; if (v > max) max = v; }
        double range = max - min > 0 ? max - min : 1.0;

        var bmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        var px = new byte[w * h * 4];
        for (int r = 0; r < h; r++)
            for (int x = 0; x < w; x++)
            {
                double t = (recon[x, h - 1 - r] - min) / range;   // flip: screen-top = max y
                var (rr, gg, bb) = Viridis(t);
                int i = (r * w + x) * 4;
                px[i + 0] = bb; px[i + 1] = gg; px[i + 2] = rr; px[i + 3] = 215;
            }
        bmp.WritePixels(new Int32Rect(0, 0, w, h), px, w * 4, 0);

        _reconBmp = bmp;
        _reconLeftMm = originMm;
        _reconBottomMm = originMm;
        _reconRightMm = originMm + (w - 1) * stepMm;
        _reconTopMm = originMm + (h - 1) * stepMm;
    }

    private static readonly (double R, double G, double B)[] ViridisStops =
        [(68, 1, 84), (59, 82, 139), (33, 145, 140), (94, 201, 98), (253, 231, 37)];

    private static (byte r, byte g, byte b) Viridis(double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        double f = t * (ViridisStops.Length - 1);
        int i = Math.Min((int)f, ViridisStops.Length - 2);
        double u = f - i;
        var a = ViridisStops[i];
        var c = ViridisStops[i + 1];
        byte L(double lo, double hi) => (byte)Math.Round(lo + (hi - lo) * u);
        return (L(a.R, c.R), L(a.G, c.G), L(a.B, c.B));
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
        if (_liveRunning) { StopLive(); return; }            // a running acquisition: this click stops it
        if (_scene.Count == 0) { MessageBox.Show("Add at least one source to the scene."); return; }

        int tab = MainTabs.SelectedIndex;
        if (tab == 0) { await RunWaveform(); return; }       // waveform: a µs snapshot, one-shot
        await StartLive(tab);                                // imaging / spectrum: continuous accumulation
    }

    // ---- live acquisition ----

    private async Task StartLive(int tab)
    {
        _liveTab = tab;
        _liveSpeed = Math.Max(0.1, ParseD(LiveSpeed.Text, 10));
        long photons = tab == 1 ? (long)ParseD(ImgPhotons.Text, 500_000) : 400_000;
        double bsr = tab == 1 ? ParseD(ImgBsr.Text, 0) : 0.0;
        double windowFrac = ParseD(SpWindow.Text, 10) / 100.0;
        var cfg = ConfigFromScene(photons, bsr);
        double emissionRate = EmissionRatePerSec();
        _liveTruePos = _scene.Select(s => (s.X, s.Y)).ToList();
        double energyRef = Isotopes.Get(_scene[0].Isotope).Lines[0].EnergyKeV;
        double maxE = _scene.SelectMany(s => Isotopes.Get(s.Isotope).Lines.Select(l => l.EnergyKeV))
                            .DefaultIfEmpty(energyRef).Max() * 1.15;

        SimulateButton.Content = "■  STOP";
        LiveStatus.Text = "preparing…";
        bool ok = await Task.Run(() => PrepareLive(tab, cfg, bsr, emissionRate, windowFrac, energyRef, maxE));
        if (!ok)
        {
            SimulateButton.Content = "▶  SIMULATE";
            LiveStatus.Text = "no counts detected — check activity / distance / geometry";
            return;
        }

        _liveElapsedSec = 0;
        _liveTotalCounts = 0;
        _liveRng = new DefaultRandom(20260715);
        _liveRunning = true;
        _liveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _liveTimer.Tick += LiveTimer_Tick;
        _liveTimer.Start();
    }

    // Off the UI thread: one MC run fixes the accumulation SHAPE (normalized) and the detected count rate.
    private bool PrepareLive(int tab, SimulationConfig cfg, double bsr, double emissionRate,
                             double windowFrac, double energyRef, double maxE)
    {
        var factory = new DefaultSimulationFactory();
        var res = new SimulationRunner(factory).Run(cfg);
        double eff = res.PhotonsEmitted > 0 ? res.DetectedWeight / res.PhotonsEmitted : 0.0;
        _liveRateCps = emissionRate * eff;
        if (!(_liveRateCps > 0.0)) return false;

        if (tab == 1)
        {
            var mean = res.DetectorImage;
            int w = mean.Width, h = mean.Height;
            double ped = bsr > 0.0 ? SimBackground.PedestalPerPixel(bsr, SumImage(mean), w * h) : 0.0;
            var shape = new double[w * h];
            double sum = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) { double v = mean[x, y] + ped; shape[y * w + x] = v; sum += v; }
            if (!(sum > 0.0)) return false;
            for (int i = 0; i < shape.Length; i++) shape[i] /= sum;
            _floodShape = shape;
            _floodAccum = new DetectorImage(w, h);
            _liveDecoder = factory.CreateDecoder(cfg);
            return _liveDecoder != null;
        }

        var pool = new EventStreamStudy().Generate(cfg, Math.Max(_liveRateCps, 1.0), AdcSampleRateHz, 20000)
                       .Select(ev => ev.EnergyKeV).ToArray();
        if (pool.Length == 0) return false;
        const int bins = 128;
        double bw = maxE / bins;
        var pdf = new double[bins];
        var ctr = new double[bins];
        foreach (var d in pool) { int b = (int)(d / bw); if (b >= 0 && b < bins) pdf[b] += 1; }
        for (int i = 0; i < bins; i++) { ctr[i] = (i + 0.5) * bw; pdf[i] /= pool.Length; }
        _specPdf = pdf;
        _specCounts = new double[bins];
        _specCenters = ctr;
        _specWindowLo = energyRef * (1.0 - windowFrac);
        _specWindowHi = energyRef * (1.0 + windowFrac);
        return true;
    }

    private void LiveTimer_Tick(object? sender, EventArgs e)
    {
        double dt = 0.25 * _liveSpeed;                 // simulated acquisition seconds this tick
        _liveElapsedSec += dt;
        double dN = _liveRateCps * dt;
        var rng = _liveRng!;

        if (_liveTab == 1 && _floodShape != null && _floodAccum != null && _liveDecoder != null)
        {
            int w = _floodAccum.Width, h = _floodAccum.Height;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int add = Sampling.Poisson(rng, _floodShape[y * w + x] * dN);
                    if (add != 0) { _floodAccum.Add(x, y, add); _liveTotalCounts += add; }
                }

            var dec = _liveDecoder.Decode(_floodAccum);
            DrawHeatmap(ImgFloodPlot, ToGrid(_floodAccum), 0, w, 0, h,
                "Detector flood map (counts)", "pixel x", "pixel y", null, null);
            var recon = dec.Reconstruction;
            double left = dec.ReconOriginMm, right = dec.ReconOriginMm + (recon.Width - 1) * dec.ReconStepMm;
            double bottom = dec.ReconOriginMm, top = dec.ReconOriginMm + (recon.Height - 1) * dec.ReconStepMm;
            DrawHeatmap(ImgReconPlot, ToGrid(recon), left, right, bottom, top,
                "Decoded reconstruction (× sources, ○ estimate)", "x (mm)", "y (mm)",
                _liveTruePos, (dec.Estimate.Position.X, dec.Estimate.Position.Y));
            SetReconOverlay(recon, dec.ReconOriginMm, dec.ReconStepMm);
            RedrawScene();
            ImgStatus.Text = $"estimate ({dec.Estimate.Position.X:F2}, {dec.Estimate.Position.Y:F2}) mm — sharpens as counts build";
        }
        else if (_liveTab == 2 && _specPdf != null && _specCounts != null && _specCenters != null)
        {
            double win = 0;
            for (int i = 0; i < _specCounts.Length; i++)
            {
                int add = Sampling.Poisson(rng, _specPdf[i] * dN);
                if (add != 0) { _specCounts[i] += add; _liveTotalCounts += add; }
                if (_specCenters[i] >= _specWindowLo && _specCenters[i] <= _specWindowHi) win += _specCounts[i];
            }
            DrawSpectrum(SpPlot, _specCenters, _specCounts, _specWindowLo, _specWindowHi,
                "Deposited-energy spectrum (accumulating)", "deposited energy (keV)", "counts");
            SpStatus.Text = _liveTotalCounts > 0
                ? $"±window holds {win / _liveTotalCounts:P0} of {_liveTotalCounts:N0} counts"
                : "";
        }

        LiveStatus.Text = $"t = {_liveElapsedSec:F0} s   ·   {_liveTotalCounts:N0} counts   ·   {_liveRateCps:F0} cps detected";
    }

    private void StopLive()
    {
        _liveTimer?.Stop();
        _liveTimer = null;
        _liveRunning = false;
        SimulateButton.Content = "▶  SIMULATE";
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
