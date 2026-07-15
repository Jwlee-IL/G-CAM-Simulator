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
    private double _liveRateCps, _liveElapsedSec, _liveTotalCounts, _liveSpeed;
    private IRandom? _liveRng;
    private DetectorImage? _floodAccum;      // imaging: accumulated flood map
    private double[]? _floodShape;           // imaging: per-pixel mean, Σ = 1
    private IDecoder? _liveDecoder;
    private SimulationConfig? _liveCfg;      // the config in flight, so refocusing can rebuild the decoder
    private IReadOnlyList<(double x, double y)>? _liveTruePos;
    private double[]? _specPdf, _specCounts, _specCenters;   // spectrum accumulators
    private List<(double lo, double hi, double energy)> _specWindows = [];   // one ROI per emission line
    private double[]? _wfPool;               // waveform: this acquisition's deposit energies (scope source)
    private IRandom? _wfRng;
    private int _liveTickCount;
    private string _liveDetail = "";

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
        // One acquisition drives every tab; switching tabs just re-renders the current state (no stop).
        MainTabs.SelectionChanged += (_, e) => { if (e.Source is TabControl && _liveRunning) RenderVisibleTab(); };

        foreach (var box in new[] { OptRank, OptCell, OptD, OptDetN, OptDetPitch })
            box.TextChanged += (_, _) => UpdateOpticsReadout();
        OptFocalLabel.Text = $"{OptFocal.Value:F0} mm";
        UpdateOpticsReadout();

        Log("Ready. Place sources on the scene, then press Simulate.");
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

    /// <summary>Fully-coded FOV half-extent at the first source's plane, from the current optics (the same
    /// geometry the decoder uses to size its reconstruction grid): period = rank·cellPitch·sourceZ/D.</summary>
    private double FcfovHalfMm()
    {
        int rank = NearestPrime((int)ParseD(OptRank.Text, 13));
        double cell = ParseD(OptCell.Text, 0.7);
        double d = ParseD(OptD.Text, 80);
        double focalZ = OptFocal?.Value ?? 160.0;
        if (focalZ <= d) focalZ = d + 1.0;
        double period = rank * cell / (d / focalZ);
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

        // Optics (from the Optics/Presets tab): mask rank/cell, mask–detector gap, detector array. The decoder
        // focuses on the first source's plane, so its source–mask distance S = (source z) − D.
        int rank = NearestPrime((int)ParseD(OptRank.Text, 13));
        double cell = Math.Max(0.05, ParseD(OptCell.Text, 0.7));
        double d = Math.Max(1.0, ParseD(OptD.Text, 80));
        int detN = Math.Clamp((int)ParseD(OptDetN.Text, 20), 4, 64);
        double pitch = Math.Max(0.05, ParseD(OptDetPitch.Text, 0.6));
        double focalZ = OptFocal?.Value ?? 160.0;      // the plane the decoder focuses on (sources keep their z)
        cfg.Mask.Rank = rank;
        cfg.Mask.CellPitchMm = cell;
        cfg.Geometry.MaskDetectorDistanceMm = d;
        cfg.Geometry.SourceMaskDistanceMm = Math.Max(1.0, focalZ - d);
        cfg.Detector.PixelsX = cfg.Detector.PixelsY = detN;
        cfg.Detector.PixelPitchMm = pitch;

        // Non-cyclic (finite-mask) decode + recon kept just inside the FCFOV — suppresses the off-axis ghosts
        // that a cyclic decode aliases in, so MULTIPLE off-axis sources each resolve to their own peak.
        cfg.Decoder.Cyclic = false;
        SetReconExtent(cfg, focalZ);

        if (bsr > 0.0)
            cfg.Background = new BackgroundConfig { BackgroundToSignalRatio = bsr, EnergyKeV = 200.0 };
        return cfg;
    }

    // ---- optics / presets tab ----------------------------------------------------------------------------

    private void OptPreset_Changed(object sender, SelectionChangedEventArgs e)
    {
        string name = (OptPreset.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        // Each preset keeps the detector spanning ~one mask period (det ≈ rank·cell·srcDist/S); otherwise
        // off-axis / multiple sources decode badly. Verified in the MC (mixedfield, 3 sources).
        (int rank, double cell, double d, int n, double pitch) p = name switch
        {
            "Baseline (coarse)" => (7, 1.0, 60, 12, 1.0),
            "Wide FOV" => (17, 1.0, 50, 34, 0.75),
            "High-res" => (13, 0.5, 100, 44, 0.4),
            _ => (13, 0.7, 80, 30, 0.6),   // Sharp (default)
        };
        OptRank.Text = p.rank.ToString(CultureInfo.InvariantCulture);
        OptCell.Text = p.cell.ToString(CultureInfo.InvariantCulture);
        OptD.Text = p.d.ToString(CultureInfo.InvariantCulture);
        OptDetN.Text = p.n.ToString(CultureInfo.InvariantCulture);
        OptDetPitch.Text = p.pitch.ToString(CultureInfo.InvariantCulture);
        UpdateOpticsReadout();
        Log($"Optics preset: {name} — rank {p.rank}, cell {p.cell} mm, D {p.d} mm, det {p.n}×{p.n} @ {p.pitch} mm");
    }

    // Live derived numbers from the current optics fields (no MC — just the geometry formulas).
    private void UpdateOpticsReadout()
    {
        if (OptReadout == null) return;
        int rank = NearestPrime((int)ParseD(OptRank.Text, 13));
        double cell = ParseD(OptCell.Text, 0.7);
        double d = ParseD(OptD.Text, 80);
        int n = (int)ParseD(OptDetN.Text, 20);
        double pitch = ParseD(OptDetPitch.Text, 0.6);
        double focalZ = OptFocal?.Value ?? 160.0;
        if (focalZ <= d) focalZ = d + 1.0;

        double res = cell * focalZ / d;                        // resolution element at the focal plane
        double fcfovHalf = rank * res / 2.0;
        double shadow = cell * focalZ / (focalZ - d);          // mask-cell shadow at the detector
        double samples = shadow / pitch;
        double detSize = n * pitch;
        double period = rank * shadow;                         // one mask period at the detector
        double coverage = detSize / period;                    // detector must span ~1 period to decode well

        OptReadout.Text =
            $"resolution element ≈ {res:F2} mm      FCFOV ± {fcfovHalf:F1} mm   (rank {rank})\n" +
            $"detector {n}×{n} @ {pitch:F2} mm = {detSize:F1} mm across\n" +
            $"Nyquist {samples:F1} samples/cell  {(samples >= 2.0 ? "✓" : "⚠ undersampled — finer pixel pitch")}\n" +
            $"detector spans {coverage:F2} mask periods  " +
            $"{(coverage is >= 0.9 and <= 1.4 ? "✓" : "⚠ set N so detector ≈ 1 period, else off-axis sources decode badly")}\n" +
            $"(focal plane at {focalZ:F0} mm — sources at other distances defocus)";
        RedrawScene();   // keep the scene's FCFOV box in sync with the optics
    }

    private static int NearestPrime(int n)
    {
        if (n < 2) return 2;
        for (int d = 0; d < n + 2; d++)
        {
            if (IsPrime(n - d) && n - d >= 2) return n - d;
            if (IsPrime(n + d)) return n + d;
        }
        return 2;
    }

    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        for (int i = 2; (long)i * i <= n; i++)
            if (n % i == 0) return false;
        return true;
    }

    private static void SetReconExtent(SimulationConfig cfg, double focalZ)
    {
        double d = cfg.Geometry.MaskDetectorDistanceMm;
        double frac = d / Math.Max(d + 1.0, focalZ);
        cfg.Decoder.ReconHalfExtentMm = 0.95 * cfg.Mask.Rank * cfg.Mask.CellPitchMm / frac / 2.0;
        cfg.Decoder.ReconStepMm = Math.Max(0.2, cfg.Mask.CellPitchMm / frac / 4.0);
    }

    // Moving the focal slider only changes the DECODE (back-projection plane), not the flood map — so a live
    // acquisition refocuses instantly (rebuild the decoder, re-decode the same accumulated counts). Sources at
    // the chosen plane sharpen; sources at other distances defocus.
    private void OptFocal_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OptReadout == null) return;                 // still initializing
        OptFocalLabel.Text = $"{OptFocal.Value:F0} mm";
        UpdateOpticsReadout();
        Refocus();
    }

    private void Refocus()
    {
        if (!_liveRunning || _liveCfg == null || _floodAccum == null) return;
        double focalZ = OptFocal.Value;
        var cfg = _liveCfg.Clone();
        cfg.Geometry.SourceMaskDistanceMm = Math.Max(1.0, focalZ - cfg.Geometry.MaskDetectorDistanceMm);
        SetReconExtent(cfg, focalZ);
        _liveDecoder = new DefaultSimulationFactory().CreateDecoder(cfg);
        _liveCfg = cfg;
        if (MainTabs.SelectedIndex == 1) RenderImaging();   // re-decode the accumulated flood at the new plane
    }

    private double EmissionRatePerSec() => _scene.Sum(s => s.EmissionRatePerSec());

    // ============================ SIMULATE — one acquisition drives every tab ============================

    private async void Simulate_Click(object sender, RoutedEventArgs e)
    {
        if (_liveRunning) { StopLive(); return; }            // a running acquisition: this click stops it
        if (_scene.Count == 0) { MessageBox.Show("Add at least one source to the scene."); return; }
        await StartLive();
    }

    // One detector acquisition. Each tick, ΔN detected events feed the flood map (Imaging), the energy
    // histogram (Spectrum) and the scope (Waveform) — the SAME event stream, three views. Only the visible
    // tab is rendered each tick; switching tabs shows the current accumulated state immediately.
    private async Task StartLive()
    {
        _liveSpeed = Math.Max(0.1, ParseD(LiveSpeed.Text, 10));
        long photons = (long)ParseD(ImgPhotons.Text, 500_000);
        double bsr = ParseD(ImgBsr.Text, 0);
        double windowFrac = ParseD(SpWindow.Text, 10) / 100.0;
        double resPct = ParseD(SpResolution.Text, 6);
        var cfg = ConfigFromScene(photons, bsr);
        _liveCfg = cfg;                     // kept so the focal slider can rebuild the decoder without a new MC
        double emissionRate = EmissionRatePerSec();
        _liveTruePos = _scene.Select(s => (s.X, s.Y)).ToList();
        // Every distinct emission line across ALL sources gets its own photopeak window (ROI), so a mixed
        // Cs/Co field marks 662, 1173 and 1332 — not just the first isotope.
        double[] lineEnergies = _scene.SelectMany(s => Isotopes.Get(s.Isotope).Lines.Select(l => l.EnergyKeV))
                                      .Distinct().OrderBy(x => x).ToArray();
        double maxE = (lineEnergies.Length > 0 ? lineEnergies.Max() : 661.7) * 1.15;

        SimulateButton.Content = "■  STOP";
        LiveStatus.Text = "preparing…";
        Log($"▶ Start acquisition  (speed ×{_liveSpeed:F0}) — {SceneSummary()}");
        bool ok = await Task.Run(() => PrepareLive(cfg, bsr, emissionRate, windowFrac, lineEnergies, maxE, resPct));
        if (!ok)
        {
            SimulateButton.Content = "▶  SIMULATE";
            LiveStatus.Text = "no counts detected — check activity / distance / geometry";
            Log("  ✗ no counts detected — check activity / distance / geometry");
            return;
        }
        Log($"  detected rate = {_liveRateCps:F0} cps  (emission {emissionRate:F0}/s × geometric efficiency)");

        _liveElapsedSec = 0;
        _liveTotalCounts = 0;
        _liveTickCount = 0;
        _liveRng = new DefaultRandom(20260715);
        _wfRng = new DefaultRandom(555);
        _liveRunning = true;
        RenderVisibleTab();
        _liveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _liveTimer.Tick += LiveTimer_Tick;
        _liveTimer.Start();
    }

    // Off the UI thread: one MC run fixes the flood SHAPE + count rate and the deposit pool (spectrum PDF +
    // scope energies) — everything every tab needs, from a single acquisition.
    private bool PrepareLive(SimulationConfig cfg, double bsr, double emissionRate,
                             double windowFrac, double[] lineEnergies, double maxE, double resPct)
    {
        var factory = new DefaultSimulationFactory();
        var res = new SimulationRunner(factory).Run(cfg);
        double eff = res.PhotonsEmitted > 0 ? res.DetectedWeight / res.PhotonsEmitted : 0.0;
        _liveRateCps = emissionRate * eff;
        if (!(_liveRateCps > 0.0)) return false;

        // Imaging: normalized flood shape + decoder.
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
        if (_liveDecoder == null) return false;

        // Deposit pool (shared): raw energies drive the Waveform scope; resolution-smeared energies build the
        // Spectrum PDF — same detected events, two views.
        var rawPool = new EventStreamStudy().Generate(cfg, Math.Max(_liveRateCps, 1.0), AdcSampleRateHz, 20000)
                          .Select(ev => ev.EnergyKeV).ToArray();
        if (rawPool.Length == 0) return false;
        _wfPool = rawPool;

        var smeared = ApplyResolution(rawPool, resPct, seed: 909);
        const int bins = 128;
        double bw = maxE / bins;
        var pdf = new double[bins];
        var ctr = new double[bins];
        foreach (var d in smeared) { int b = (int)(d / bw); if (b >= 0 && b < bins) pdf[b] += 1; }
        for (int i = 0; i < bins; i++) { ctr[i] = (i + 0.5) * bw; pdf[i] /= smeared.Length; }
        _specPdf = pdf;
        _specCounts = new double[bins];
        _specCenters = ctr;
        _specWindows = lineEnergies
            .Select(en => (en * (1.0 - windowFrac), en * (1.0 + windowFrac), en))
            .ToList();
        return true;
    }

    private void LiveTimer_Tick(object? sender, EventArgs e)
    {
        double dt = 0.25 * _liveSpeed;                 // simulated acquisition seconds this tick
        _liveElapsedSec += dt;
        double dN = _liveRateCps * dt;
        var rng = _liveRng!;

        // Accumulate the SAME ΔN detected events into every view's store (cheap); render only the visible tab.
        if (_floodShape != null && _floodAccum != null)
        {
            int w = _floodAccum.Width, h = _floodAccum.Height;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int add = Sampling.Poisson(rng, _floodShape[y * w + x] * dN);
                    if (add != 0) { _floodAccum.Add(x, y, add); _liveTotalCounts += add; }
                }
        }
        if (_specPdf != null && _specCounts != null)
            for (int i = 0; i < _specCounts.Length; i++)
                _specCounts[i] += Sampling.Poisson(rng, _specPdf[i] * dN);

        RenderVisibleTab();

        LiveStatus.Text = $"t = {_liveElapsedSec:F0} s   ·   {_liveTotalCounts:N0} counts   ·   {_liveRateCps:F0} cps detected";
        if (++_liveTickCount % 8 == 0)
            Log($"  t={_liveElapsedSec:F0}s   {_liveTotalCounts:N0} counts   {_liveDetail}");
    }

    // ---- per-tab rendering (all read the same live acquisition state) ----

    private void RenderVisibleTab()
    {
        switch (MainTabs.SelectedIndex)
        {
            case 0: RenderWaveform(); break;
            case 1: RenderImaging(); break;
            case 2: RenderSpectrum(); break;
        }
    }

    private void RenderImaging()
    {
        if (_floodAccum == null || _liveDecoder == null) return;
        int w = _floodAccum.Width, h = _floodAccum.Height;
        var dec = _liveDecoder.Decode(_floodAccum);
        DrawHeatmap(ImgFloodPlot, ToGrid(_floodAccum), 0, w, 0, h,
            "Detector flood map (counts)", "pixel x", "pixel y", null, null);
        var recon = dec.Reconstruction;
        double left = dec.ReconOriginMm, right = dec.ReconOriginMm + (recon.Width - 1) * dec.ReconStepMm;
        double bottom = dec.ReconOriginMm, top = dec.ReconOriginMm + (recon.Height - 1) * dec.ReconStepMm;

        // A coded aperture images every source at once — find the K strongest peaks (K = source count) so
        // each source is marked, not just the single global maximum.
        int k = Math.Max(1, _scene.Count);
        var peaks = MixedFieldStudy.TopPeaks(recon, dec.ReconOriginMm, dec.ReconStepMm, k, minSeparationMm: 2.5);
        var estList = peaks.Select(pk => (pk.Xmm, pk.Ymm)).ToList();

        DrawHeatmap(ImgReconPlot, ToGrid(recon), left, right, bottom, top,
            "Decoded reconstruction (× sources, ○ found)", "x (mm)", "y (mm)",
            _liveTruePos, estList);
        SetReconOverlay(recon, dec.ReconOriginMm, dec.ReconStepMm);
        RedrawScene();

        var truth = _scene.Select(s => new[] { s.X, s.Y }).ToArray();
        var matches = MixedFieldStudy.MatchOneToOne(truth, peaks);
        double worst = matches.Length > 0 ? matches.Max(m => m.ErrorMm) : 0.0;
        ImgStatus.Text = $"{peaks.Length} source(s) found · worst error {worst:F2} mm — sharpens as counts build";
        _liveDetail = $"{peaks.Length} found, worst {worst:F1}mm";
    }

    private void RenderSpectrum()
    {
        if (_specCounts == null || _specCenters == null) return;
        double win = 0, tot = 0;
        for (int i = 0; i < _specCounts.Length; i++)
        {
            tot += _specCounts[i];
            double c = _specCenters[i];
            foreach (var wnd in _specWindows)
                if (c >= wnd.lo && c <= wnd.hi) { win += _specCounts[i]; break; }
        }
        DrawSpectrum(SpPlot, _specCenters, _specCounts, _specWindows,
            "Energy spectrum — detector resolution applied (accumulating)", "measured energy (keV)", "counts");
        SpStatus.Text = tot > 0
            ? $"{_specWindows.Count} photopeak window(s) hold {win / tot:P0} of {tot:N0} counts"
            : "";
        if (tot > 0) _liveDetail = $"{win / tot:P0} in {_specWindows.Count} window(s)";
    }

    private void RenderWaveform()
    {
        if (_wfPool == null || _wfPool.Length == 0) return;
        double scopeCps = Math.Max(1.0, ParseD(WfRate.Text, 50)) * 1000.0;   // scope time-zoom (see the label)
        int nEvents = Math.Clamp(ParseI(WfEvents.Text, 40), 1, 4000);
        bool realistic = WfRealistic.IsChecked == true;
        bool crrc = WfShaper.SelectedIndex == 1;
        var rng = _wfRng ??= new DefaultRandom(555);

        // Sample nEvents from THIS acquisition's deposit pool, spaced by the scope rate (exponential arrivals).
        double meanGap = AdcSampleRateHz / scopeCps;
        var stream = new List<(long, double)>(nEvents);
        long t = (long)(meanGap * 0.5);
        for (int i = 0; i < nEvents; i++)
        {
            double energy = _wfPool[(int)(rng.NextDouble() * _wfPool.Length)];
            stream.Add((t, energy));
            double gap = -Math.Log(1.0 - rng.NextDouble()) * meanGap;
            t += (long)Math.Max(1.0, gap);
        }

        var preset = Waveform.DefaultAdc;
        int[] wave = realistic
            ? Waveform.Rasterize(stream, preset)
            : Waveform.Rasterize(stream, preset, tauRise: 0.0, noiseKev: 0.0, intrinsicFwhm: 0.0);
        long[] sh = crrc ? Waveform.CrrcInt(wave) : Waveform.TrapShape(wave);

        DrawSignal(WfAdcPlot, ToD(wave),
            "ADC waveform — this acquisition's events (bi-exp pulses, pile-up, noise, clip)",
            "sample  (8 ns @ 125 MSPS)", "ADC code");
        DrawSignal(WfShapedPlot, ToD(sh),
            (crrc ? "CR-RC^4" : "Trapezoidal") + " shaped  (flat top ∝ deposited energy)",
            "sample", "shaper output");
        WfStatus.Text = $"{nEvents} events @ scope rate {scopeCps / 1000:F0} kcps  ·  " +
                        $"isotopes: {string.Join(", ", _scene.Select(s => s.Isotope).Distinct())}";
        _liveDetail = $"waveform peak {ToD(sh).Max():F0}";
    }

    private void StopLive()
    {
        bool wasRunning = _liveRunning;
        _liveTimer?.Stop();
        _liveTimer = null;
        _liveRunning = false;
        SimulateButton.Content = "▶  SIMULATE";
        if (wasRunning) Log($"■ Stopped — t = {_liveElapsedSec:F0}s, {_liveTotalCounts:N0} counts total");
    }

    private string SceneSummary() =>
        _scene.Count == 0 ? "no sources"
            : string.Join(", ", _scene.Select(s =>
                $"{s.Isotope} {s.ActivityUCi:F0}µCi @({s.X:F0},{s.Y:F0}) d{s.DistanceMm:F0}mm"));

    /// <summary>Smear each true deposit by the detector energy resolution — photostatistics give a Gaussian
    /// whose FWHM fraction scales as √(662/E) (broader, relatively, at low energy), the same 1/√E model the
    /// waveform rasterizer uses. <paramref name="fwhmAt662Pct"/> is the % FWHM at 662 keV (≈6% for GAGG).</summary>
    private static double[] ApplyResolution(double[] deposits, double fwhmAt662Pct, int seed)
    {
        if (!(fwhmAt662Pct > 0.0)) return deposits;
        double relSigmaRef = fwhmAt662Pct / 100.0 / 2.3548;   // FWHM% → σ fraction at 662 keV
        var rng = new DefaultRandom(seed);
        var outp = new double[deposits.Length];
        for (int i = 0; i < deposits.Length; i++)
        {
            double e = deposits[i];
            if (e <= 0.0) { outp[i] = e; continue; }
            double relSigma = relSigmaRef * Math.Sqrt(662.0 / e);
            double m = e * (1.0 + Sampling.Gaussian(rng) * relSigma);
            outp[i] = m < 0.0 ? 0.0 : m;
        }
        return outp;
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
        IReadOnlyList<(double x, double y)>? truePositions, IReadOnlyList<(double x, double y)>? estPositions)
    {
        var p = view.Plot;
        p.Clear();
        var hm = p.Add.Heatmap(grid);
        hm.Extent = new ScottPlot.CoordinateRect(left, right, bottom, top);
        hm.Colormap = new ScottPlot.Colormaps.Viridis();
        // NOTE: no Add.ColorBar here — Plot.Clear() does not remove colorbars, so re-adding one every live
        // tick stacks them across the right edge. Brightness is relative; the axes carry the scale.

        if (truePositions != null)
            foreach (var tp in truePositions)
            {
                var m = p.Add.Marker(tp.x, tp.y);
                m.Color = ScottPlot.Colors.White;
                m.Size = 16;
                m.Shape = ScottPlot.MarkerShape.Cross;
            }
        if (estPositions != null)
            foreach (var ep in estPositions)
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
        IReadOnlyList<(double lo, double hi, double energy)> windows, string title, string xlabel, string ylabel)
    {
        var p = view.Plot;
        p.Clear();

        double maxCount = 1.0;
        foreach (var c in counts) if (c > maxCount) maxCount = c;

        // Photopeak ROI bands — one per emission line (translucent), with a labelled edge at each line.
        foreach (var wnd in windows)
        {
            var band = p.Add.Rectangle(wnd.lo, wnd.hi, 0, maxCount);
            band.FillColor = ScottPlot.Colors.Red.WithAlpha(0.10);
            band.LineColor = ScottPlot.Colors.Transparent;
            var line = p.Add.VerticalLine(wnd.energy);
            line.Color = ScottPlot.Colors.Red.WithAlpha(0.55);
            line.LineWidth = 1;
            line.LabelText = $"{wnd.energy:F0}";
        }

        double bw = centers.Length > 1 ? centers[1] - centers[0] : 1.0;
        var bars = new List<ScottPlot.Bar>(centers.Length);
        for (int i = 0; i < centers.Length; i++)
            bars.Add(new ScottPlot.Bar { Position = centers[i], Value = counts[i], Size = bw });
        p.Add.Bars(bars);

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

    // ---- log console -------------------------------------------------------------------------------------

    private void Log(string msg)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }
}
