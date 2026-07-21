using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using SimBackground = Gcam.Simulation.Background;

namespace Gcam.Wpf;

/// <summary>A viewer over the GCAM core. A 2D scene editor (top) holds the sources — each with an isotope,
/// activity (µCi), and distance to the detector — and a single Simulate button drives whichever analysis tab
/// is active: shape the ADC waveform, image the field, or count the deposited-energy spectrum over a real
/// acquisition time. Heavy MC work runs off the UI thread; ScottPlot renders the results.</summary>
public partial class MainWindow : Window
{
    private const double AdcSampleRateHz = 125e6;   // AD9648 125 MSPS
    // Canvas span auto-fits the FCFOV, which grows with the rangefinder range — so sources stay in view at any
    // range (a fixed span pushes them off-canvas when zoomed far out).
    private double MmSpanMm() => Math.Max(120.0, 2.6 * FcfovHalfMm());

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
    private DetectorImage? _floodAccum;      // imaging: COMBINED flood (all nuclide channels summed) — depth/autofocus decode this so they see EVERY source
    private IDecoder? _liveDecoder;          // geometry-only decoder, SHARED across nuclide channels (energy-independent)
    private bool _preparing;                 // a background acquisition-prepare is in flight — guards against a double-click launching a second run

    // One imaging channel per DISTINCT isotope in the scene. Each isolates its nuclide with that nuclide's
    // photopeak energy window, so decoding its own flood reconstructs (mostly) only that nuclide's sources. The
    // per-nuclide recons are normalized and composited (isotope-coloured) so co-measured isotopes separate in the
    // reconstructed image the way they already separate in the spectrum. (A single isotope → one channel.)
    private readonly List<NuclideChannel> _channels = [];

    private sealed class NuclideChannel
    {
        public string Isotope = "";
        public double WindowCenterKeV;
        public (byte r, byte g, byte b) Color;
        public double[] FloodShape = [];        // per-pixel mean, Σ = 1
        public DetectorImage FloodAccum = null!;
        public double RateCps;                  // detected counts/s landing in THIS nuclide's window
        // Compton stripping: (index of a CONTAMINATING channel — one with an emission line above this window,
        // calibrated downscatter ratio R). Subtracting R·(that channel's flood) per pixel removes its downscatter
        // leaking into THIS window.
        public readonly List<(int hiIdx, double r)> Strip = new();
    }
    private SimulationConfig? _liveCfg;      // the config in flight, so refocusing can rebuild the decoder
    private IReadOnlyList<(double x, double y)>? _liveTruePos;
    private double[]? _specPdf, _specCounts, _specCenters;   // spectrum accumulators
    private double[]? _noisePdf;                             // source-independent low-energy noise-wall shape
    private double _noiseCps;                                // device electronics/EMI noise trigger rate (cps)
    private List<(double lo, double hi, double energy)> _specWindows = [];   // one ROI per emission line
    private double[]? _wfPool;               // waveform: this acquisition's deposit energies (scope source)
    private IRandom? _wfRng;
    private int _liveTickCount;
    private string _liveDetail = "";

    // Selected detection chain (scintillator / photosensor / preamp) — the pulse shape and the energy resolution
    // are DERIVED from the parts, not hand-set. Populated by UpdateFrontEnd().
    private FrontEndConfig? _feConfig;
    private double _pulseTauSamples = Waveform.TauSamples, _pulseRiseSamples = Waveform.TauRiseSamples;
    private bool _feCrrc = true;
    private double _feResPct662 = 6.0;

    public MainWindow()
    {
        InitializeComponent();
        foreach (var iso in Isotopes.All) PropIsotope.Items.Add(iso.Name);
        foreach (var s in FrontEndParts.Scintillators) FeScint.Items.Add(s.Name);
        foreach (var s in FrontEndParts.Sensors) FeSensor.Items.Add(s.Name);
        foreach (var s in FrontEndParts.Preamps) FePreamp.Items.Add(s.Name);
        FeScint.SelectedIndex = 0;    // GAGG(Ce)
        FeSensor.SelectedIndex = 0;   // Hamamatsu MPPC S13360-3050
        FePreamp.SelectedIndex = 1;   // CSP + CR-RC (your rig)

        _scene.Add(new SceneSource { Isotope = "Cs-137", X = 0, Y = 0, DistanceMm = 160, ActivityUCi = 10 });
        RefreshSourceList(select: 0);

        Loaded += (_, _) => RedrawScene();
        SceneCanvas.SizeChanged += (_, _) => RedrawScene();
        SceneCanvas.MouseMove += SceneCanvas_MouseMove;
        SceneCanvas.MouseLeftButtonUp += SceneCanvas_MouseUp;
        // One acquisition drives every tab; switching tabs just re-renders the current state (no stop).
        MainTabs.SelectionChanged += (_, e) =>
        {
            if (e.Source is not TabControl) return;
            if (_liveRunning) RenderVisibleTab();
            else if (MainTabs.SelectedIndex == 4) RenderDetector();   // static detector view works with no run
        };

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
        return (Math.Min(cw, ch) / MmSpanMm(), cw, ch);
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
        Math.Clamp(16.0 * (1000.0 / Math.Max(distanceMm, 1.0)), 5.0, 22.0);   // nearer source = bigger dot (~1 m ref)

    private static Brush IsotopeBrush(string isotope) => isotope switch
    {
        "Cs-137" => Brushes.Crimson,
        "Co-60" => Brushes.DarkOrange,
        "Co-57" => Brushes.MediumSeaGreen,
        "Na-22" => Brushes.RoyalBlue,
        "Am-241" => Brushes.MediumPurple,
        _ => Brushes.Gray,
    };

    /// <summary>The isotope's marker colour as raw RGB — used to tint its channel in the per-nuclide
    /// reconstruction composite (so each nuclide reads as its own colour on the scene overlay).</summary>
    private static (byte r, byte g, byte b) IsotopeRgb(string isotope)
    {
        var c = ((SolidColorBrush)IsotopeBrush(isotope)).Color;
        return (c.R, c.G, c.B);
    }

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

    /// <summary>Per-nuclide reconstruction composite for the scene overlay: each channel's recon is normalized to
    /// its own peak and tinted its isotope colour, then the tinted layers are added. Co-measured nuclides read as
    /// their own colours at their own positions — the image-domain nuclide separation the single-window decode
    /// couldn't do. (One channel → a single-colour heatmap of that nuclide.)</summary>
    private void SetReconOverlayComposite(
        List<(NuclideChannel ch, DetectorImage recon, double max)> recons, double originMm, double stepMm)
    {
        if (recons.Count == 0) { _reconBmp = null; return; }
        int w = recons[0].recon.Width, h = recons[0].recon.Height;
        var bmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        var px = new byte[w * h * 4];
        for (int r = 0; r < h; r++)
            for (int x = 0; x < w; x++)
            {
                double accR = 0, accG = 0, accB = 0;
                foreach (var (ch, rec, mx) in recons)
                {
                    if (mx <= 0) continue;
                    double t = Math.Clamp(rec[x, h - 1 - r] / mx, 0.0, 1.0);   // flip: screen-top = max y
                    accR += t * ch.Color.r; accG += t * ch.Color.g; accB += t * ch.Color.b;
                }
                int i = (r * w + x) * 4;
                px[i + 0] = (byte)Math.Min(255.0, accB);
                px[i + 1] = (byte)Math.Min(255.0, accG);
                px[i + 2] = (byte)Math.Min(255.0, accR);
                px[i + 3] = 215;
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
        double half = MmSpanMm() / 2;
        _dragging.X = Math.Round(Math.Clamp(x, -half, half), 1);
        _dragging.Y = Math.Round(Math.Clamp(y, -half, half), 1);
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

        // Passive entrance material (source encapsulation + detector window/housing), stainless-steel-equivalent.
        // 0.15 mm leaves the Ba K X-ray (32 keV) as a modest ~8% bump instead of the blown-up peak a bare vacuum
        // geometry produces; it also Compton-scatters, filling the photopeak's low-energy tail (the edge-to-peak
        // valley). Sweep-picked; MC-verified.
        cfg.Detector.EntranceAbsorberMm = 0.15;
        // Backing behind the crystal (SiPM + PCB + housing) — a through-going 662 keV photon back-scatters off it
        // and re-enters as ~184 keV: the backscatter peak. Behind the crystal, so it doesn't attenuate the beam.
        cfg.Detector.BackingScatterMm = 2.0;

        // The detection chain drives the per-pixel photopeak window's energy smear (the ComptonCrystalDetector
        // measures each deposit through this before the LLD/ULD check), so a coarser detector's wider FWHM lets
        // more scatter into the imaging window.
        if (_feConfig != null) cfg.Detector.FrontEnd = _feConfig;

        // Per-crystal gain spread (fixed pattern, seeded): each pixel's gain differs, so its 662 keV deposit lands
        // slightly off-window and it loses part of the photopeak — the crystal-to-crystal non-uniformity the real
        // rig calibrated out per crystal. EnergyWindowFraction stays 0 so the sensitivity is pure gain (the LLD/ULD
        // itself lives in the ComptonCrystalDetector window).
        cfg.Detector.GainSigma = Math.Max(0.0, ParseD(ImgGainSigma.Text, 3) / 100.0);
        cfg.Detector.UniformitySeed = (int)ParseD(ImgUnifSeed.Text, 1);
        // Dead reflector / saw-kerf gap between crystals (µm → mm): a photon entering the gap is lost. Clamp below
        // the pitch (a gap ≥ pitch would kill the whole detector) — matches the Detector-tab preview's clamp.
        cfg.Detector.ReflectorGapMm = Math.Clamp(ParseD(DetGapUm.Text, 100) / 1000.0, 0.0, pitch * 0.9);
        // Optical crosstalk: fraction of scintillation light an imperfect reflector leaks to neighbouring crystals.
        cfg.Detector.OpticalCrosstalkFraction = Math.Clamp(ParseD(DetCrosstalk.Text, 0) / 100.0, 0.0, 0.9);

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

        // Rough 3D depth reach: aperture A = rank·mosaic·cell; range ~ √(A·D) anchored to the MC (A=18mm,
        // D=80mm → ±10% depth to ~0.15 m). Only a hint — run `montecarlo depthdesign` for the real curve.
        double apertureMm = rank * 2 * cell;
        double range10m = 0.15 * Math.Sqrt((apertureMm * d) / (18.2 * 80.0));

        OptReadout.Text =
            $"resolution element ≈ {res:F2} mm      FCFOV ± {fcfovHalf:F1} mm   (rank {rank})\n" +
            $"detector {n}×{n} @ {pitch:F2} mm = {detSize:F1} mm across\n" +
            $"Nyquist {samples:F1} samples/cell  {(samples >= 2.0 ? "✓" : "⚠ undersampled — finer pixel pitch")}\n" +
            $"detector spans {coverage:F2} mask periods  " +
            $"{(coverage is >= 0.9 and <= 1.4 ? "✓" : "⚠ set N so detector ≈ 1 period, else off-axis sources decode badly")}\n" +
            $"mask aperture {apertureMm:F0} mm → 3D depth reach ≈ {range10m:F2} m (±10%, rough — run depthdesign)\n" +
            $"(range set to {focalZ:F0} mm — aperture gives direction, this input scales it to position)";
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

    // Automatic per-source DEPTH (#1): refocus the accumulated flood across a sweep of planes and take each
    // source's sharpest plane as its distance — recovers (x, y, z) even for sources at different distances.
    private async void EstimateDepth_Click(object sender, RoutedEventArgs e)
    {
        if (!_liveRunning || _floodAccum == null || _liveCfg == null)
        {
            Log("  depth: start an acquisition first"); return;
        }
        if (_liveTotalCounts < 300)
        {
            Log("  depth: let more counts build first"); return;
        }
        var snap = CopyImage(_floodAccum);              // snapshot — the timer keeps mutating _floodAccum
        var cfg = _liveCfg.Clone();
        int k = Math.Max(1, _scene.Count);
        double d = cfg.Geometry.MaskDetectorDistanceMm;
        Log($"3D depth estimate — refocusing {k} source(s) across {d + 30:F0}–3000 mm…");

        var found = await Task.Run(() => MixedFieldStudy.LocalizeDepths(
            snap, cfg, new DefaultSimulationFactory(), k, zMin: d + 30, zMax: 3000, steps: 30));

        foreach (var p in found)
        {
            var near = _scene.Count > 0
                ? _scene.OrderBy(s => (s.X - p.Xmm) * (s.X - p.Xmm) + (s.Y - p.Ymm) * (s.Y - p.Ymm)).First()
                : null;
            string tru = near != null ? $"  (placed {near.Isotope} @ {near.DistanceMm:F0} mm)" : "";
            Log($"   ({p.Xmm:F1}, {p.Ymm:F1}) at {p.Zmm:F0} mm{tru}");
        }
        ImgStatus.Text = "3D: " + string.Join("  ·  ", found.Select(p => $"({p.Xmm:F0},{p.Ymm:F0}) @ {p.Zmm:F0}mm"));
    }

    // Average each SiPM block of crystals into a flat value — models a SiPM reading a block of crystals as one
    // channel (light-sharing), so position is lost below the SiPM pitch.
    private static void BlockifyFlood(DetectorImage img, int block)
    {
        int w = img.Width, h = img.Height;
        for (int by = 0; by < h; by += block)
            for (int bx = 0; bx < w; bx += block)
            {
                double sum = 0; int n = 0;
                for (int y = by; y < Math.Min(by + block, h); y++)
                    for (int x = bx; x < Math.Min(bx + block, w); x++) { sum += img[x, y]; n++; }
                double avg = n > 0 ? sum / n : 0.0;
                for (int y = by; y < Math.Min(by + block, h); y++)
                    for (int x = bx; x < Math.Min(bx + block, w); x++) img[x, y] = avg;
            }
    }

    private static DetectorImage CopyImage(DetectorImage src)
    {
        var dst = new DetectorImage(src.Width, src.Height);
        for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++)
                dst[x, y] = src[x, y];
        return dst;
    }

    private double EmissionRatePerSec() => _scene.Sum(s => s.EmissionRatePerSec());

    // ============================ SIMULATE — one acquisition drives every tab ============================

    private async void Simulate_Click(object sender, RoutedEventArgs e)
    {
        if (_liveRunning) { StopLive(); return; }            // a running acquisition: this click stops it
        if (_preparing) return;                              // a prepare is already in flight — ignore the double-click
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
        // Per-pixel photopeak window: ±(N × FWHM) around the primary line. FWHM comes from the detection chain.
        double peakWinFwhm = Math.Max(0.1, ParseD(SpWindow.Text, 1.5));
        double windowFrac = Math.Max(0.005, peakWinFwhm * _feResPct662 / 100.0);
        // One imaging channel per DISTINCT isotope in the scene (each imaged through its own photopeak window).
        string[] sceneIsotopes = _scene.Select(s => s.Isotope).Distinct().ToArray();
        double resPct = ParseD(SpResolution.Text, 6);
        double noiseCps = Math.Max(0.0, ParseD(SpNoiseCps.Text, 0));
        double sipmPitch = Math.Max(0.05, ParseD(DetSipmPitch.Text, 0.6));   // readout pitch (≥ crystal pitch → light-sharing)
        var cfg = ConfigFromScene(photons, bsr);
        _liveCfg = cfg;                     // kept so the focal slider can rebuild the decoder without a new MC
        double emissionRate = EmissionRatePerSec();
        _liveTruePos = _scene.Select(s => (s.X, s.Y)).ToList();
        // Every distinct emission line across ALL sources gets its own photopeak window (ROI), so a mixed
        // Cs/Co field marks 662, 1173 and 1332 — not just the first isotope.
        double[] lineEnergies = _scene.SelectMany(s => Isotopes.Get(s.Isotope).Lines.Select(l => l.EnergyKeV))
                                      .Distinct().OrderBy(x => x).ToArray();
        double topLine = lineEnergies.Length > 0 ? lineEnergies.Max() : 661.7;
        // With pile-up on, extend the axis past the 2× sum peak so the self-convolution continuum is visible.
        bool pileUp = SpPileUp.IsChecked == true;
        double maxE = topLine * (pileUp ? 2.15 : 1.15);

        SimulateButton.Content = "■  STOP";
        LiveStatus.Text = "preparing…";
        Log($"▶ Start acquisition  (speed ×{_liveSpeed:F0}) — {SceneSummary()}");
        _preparing = true;
        bool ok = await Task.Run(() => PrepareLive(cfg, bsr, emissionRate, windowFrac, sceneIsotopes, lineEnergies, maxE, resPct, noiseCps, sipmPitch, pileUp));
        _preparing = false;
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
                             double windowFrac, string[] isotopes, double[] lineEnergies, double maxE, double resPct,
                             double noiseCps, double sipmPitch, bool pileUp)
    {
        // ONE imaging channel per distinct isotope: each is imaged through ITS OWN per-pixel photopeak window
        // (LLD/ULD = the isotope's primary line ± windowFrac, gain-corrected per crystal), so its coded flood —
        // and thus its reconstruction — contains essentially only that nuclide's sources. Co-measured isotopes then
        // separate in the IMAGE, not just the spectrum. Out-of-window Compton/scatter/other-line events are rejected.
        // SiPM readout granularity: a coarser SiPM pitch reads a block of crystals (light-sharing) → the sub-block
        // position is lost, so each block is averaged before the coded decode.
        int sipmBlock = Math.Max(1, (int)Math.Round(sipmPitch / cfg.Detector.PixelPitchMm));
        IDecoder? decoder = null;                 // geometry-only (rank/pitch/distance) → SHARED across channels
        _channels.Clear();
        double totalRate = 0.0;
        foreach (var iso in isotopes)
        {
            double center = Isotopes.Get(iso).Lines[0].EnergyKeV;
            var factory = new ComptonFactory(ComptonStrategy.PerPixelWindow, center, windowFrac);
            var res = new SimulationRunner(factory).Run(cfg);
            double eff = res.PhotonsEmitted > 0 ? res.DetectedWeight / res.PhotonsEmitted : 0.0;
            double rate = emissionRate * eff;
            if (!(rate > 0.0)) continue;          // nothing lands in this nuclide's window (e.g. Am-241 vs a Cs scene)
            var mean = res.DetectorImage;
            if (sipmBlock > 1) BlockifyFlood(mean, sipmBlock);
            int w = mean.Width, h = mean.Height;
            double ped = bsr > 0.0 ? SimBackground.PedestalPerPixel(bsr, SumImage(mean), w * h) : 0.0;
            var shape = new double[w * h];
            double sum = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) { double v = mean[x, y] + ped; shape[y * w + x] = v; sum += v; }
            if (!(sum > 0.0)) continue;
            for (int i = 0; i < shape.Length; i++) shape[i] /= sum;
            decoder ??= factory.CreateDecoder(cfg);
            _channels.Add(new NuclideChannel
            {
                Isotope = iso, WindowCenterKeV = center, Color = IsotopeRgb(iso),
                FloodShape = shape, FloodAccum = new DetectorImage(w, h), RateCps = rate,
            });
            totalRate += rate;
        }
        if (_channels.Count == 0 || decoder == null) return false;
        _liveDecoder = decoder;
        _liveRateCps = totalRate;
        // COMBINED flood (every channel summed) — depth / autofocus / focus-fusion decode this so a multi-isotope
        // scene's ALL sources are present (not just the first nuclide's). Per-nuclide floods live on the channels.
        _floodAccum = new DetectorImage(_channels[0].FloodAccum.Width, _channels[0].FloodAccum.Height);

        // Compton-stripping calibration: for each channel, against every contaminating channel H, the ratio
        // R = (H-only counts landing in THIS window) / (H-only counts in H's own window) — a spectral/geometry
        // property calibrated from an H-only run of the scene. Per pixel, subtracting R·H.flood then removes H's
        // downscatter leaking into this window, recovering a co-located lower isotope (themes 16-17).
        // NOTE: this is the CLI's one-pass SCALAR model — exact for a clean pair; with 3+ overlapping contaminants
        // it approximates (subtracts the RAW high-window flood, not a purified one). A full fix is a per-pixel
        // isotope×window response-matrix solve, high→low.
        for (int i = 0; i < _channels.Count; i++)
        {
            var lo = _channels[i];
            for (int j = 0; j < _channels.Count; j++)
            {
                if (j == i) continue;
                var hi = _channels[j];
                // H contaminates lo's window only if H has an emission LINE above lo's window centre. H's channel
                // CENTRE (its primary line) may be lower yet a higher secondary line still leaks in — e.g. Na-22's
                // 511 channel contaminates a 662 window via its 1275 line. So gate on H's MAX line, not its centre.
                if (Isotopes.Get(hi.Isotope).Lines.Max(l => l.EnergyKeV) <= lo.WindowCenterKeV) continue;
                var hCfg = cfg.Clone();
                hCfg.Sources = (cfg.Sources ?? []).Where(s => s.Isotope == hi.Isotope).ToArray();
                if (hCfg.Sources.Length == 0) continue;
                double hiSum = SumImage(new SimulationRunner(new ComptonFactory(
                    ComptonStrategy.PerPixelWindow, hi.WindowCenterKeV, windowFrac)).Run(hCfg).DetectorImage);
                if (!(hiSum > 0)) continue;
                double loSum = SumImage(new SimulationRunner(new ComptonFactory(
                    ComptonStrategy.PerPixelWindow, lo.WindowCenterKeV, windowFrac)).Run(hCfg).DetectorImage);
                double r = loSum / hiSum;
                if (r > 0) lo.Strip.Add((j, r));
            }
        }

        // Deposit pool (shared): raw energies drive the Waveform scope; resolution-smeared energies build the
        // Spectrum PDF — same detected events, two views.
        var stream = new EventStreamStudy().Generate(cfg, Math.Max(_liveRateCps, 1.0), AdcSampleRateHz, 20000);
        var rawPool = stream.Select(ev => ev.EnergyKeV).ToArray();
        if (rawPool.Length == 0) return false;
        _wfPool = rawPool;   // the scope keeps SINGLES — the waveform renders its own time-domain pulse overlap

        // Spectrum energies: with pile-up on, events within the shaper's resolving time SUM into one recorded
        // event (peak pile-up) at the DETECTED rate — a self-convolution continuum above the line + throughput
        // loss. The resolving time is DERIVED from the selected chain's pulse (rise + 2·tail), not a free knob.
        double[] specPool = rawPool;
        if (pileUp)
        {
            double tauRes = EventStreamStudy.ResolvingSamples(_pulseRiseSamples, _pulseTauSamples);
            specPool = EventStreamStudy.ApplyPileUp(stream, tauRes).Select(ev => ev.EnergyKeV).ToArray();
            if (specPool.Length == 0) specPool = rawPool;
        }

        // Energy resolution: when a detection chain is selected, smear each deposit by the DERIVED, energy-
        // dependent FrontEndModel resolution (photostatistics from N_pe + non-proportionality + DCR) instead of
        // the hand-set 1/√E fraction — so choosing a scintillator/sensor actually changes the photopeak width.
        double[] smeared;
        if (_feConfig != null)
        {
            var fem = new FrontEndModel(_feConfig);
            var srng = new DefaultRandom(909);
            smeared = new double[specPool.Length];
            for (int i = 0; i < specPool.Length; i++) smeared[i] = fem.Measure(specPool[i], srng);
        }
        else smeared = ApplyResolution(specPool, resPct, seed: 909);
        const int bins = 256;             // ~1.5–3 keV/bin — fine enough to read as an MCA-style curve
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

        // Source-INDEPENDENT low-energy noise wall (device electronics / EMI): a falling exponential toward low
        // energy, accumulated at a fixed rate regardless of activity — so at a weak source it dominates the low-E
        // region (the "noise wall" real detectors show). The LLD (applied at render) cuts its bottom. The rate is
        // a device knob, not derivable from the scintillator/sensor datasheet.
        if (noiseCps > 0.0)
        {
            const double noiseScaleKev = 30.0;
            var npdf = new double[bins]; double nsum = 0.0;
            for (int i = 0; i < bins; i++) { npdf[i] = Math.Exp(-ctr[i] / noiseScaleKev); nsum += npdf[i]; }
            if (nsum > 0.0) for (int i = 0; i < bins; i++) npdf[i] /= nsum;
            _noisePdf = npdf; _noiseCps = noiseCps;
        }
        else { _noisePdf = null; _noiseCps = 0.0; }
        return true;
    }

    private void LiveTimer_Tick(object? sender, EventArgs e)
    {
        double dt = 0.25 * _liveSpeed;                 // simulated acquisition seconds this tick
        _liveElapsedSec += dt;
        double dN = _liveRateCps * dt;
        var rng = _liveRng!;

        // Accumulate each nuclide channel into its OWN flood at ITS OWN detected rate (channels differ by energy
        // and efficiency), so the per-nuclide floods — and the composited reconstruction — build up correctly for a
        // mixed field. The spectrum/scope stores below use the combined rate (dN). Render only the visible tab.
        foreach (var ch in _channels)
        {
            int w = ch.FloodAccum.Width, h = ch.FloodAccum.Height;
            double dNc = ch.RateCps * dt;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int add = Sampling.Poisson(rng, ch.FloodShape[y * w + x] * dNc);
                    if (add != 0) { ch.FloodAccum.Add(x, y, add); _floodAccum?.Add(x, y, add); _liveTotalCounts += add; }
                }
        }
        if (_specPdf != null && _specCounts != null)
            for (int i = 0; i < _specCounts.Length; i++)
                _specCounts[i] += Sampling.Poisson(rng, _specPdf[i] * dN);
        // Noise wall accumulates at its own (source-independent) rate.
        if (_noisePdf != null && _specCounts != null && _noiseCps > 0.0)
        {
            double noiseN = _noiseCps * dt;
            for (int i = 0; i < _specCounts.Length; i++)
                _specCounts[i] += Sampling.Poisson(rng, _noisePdf[i] * noiseN);
        }

        // Auto-focus: every ~2 s, refocus the decoder to the plane that best focuses the strongest source, so
        // a source at any distance is caught without knowing its depth (the fixed default plane misses far ones).
        // Focus fusion: the rangefinder (OptFocal) is the absolute anchor; depth-from-focus cross-checks it.
        // If the image is clearly sharper at a DIFFERENT plane, the source isn't on the laser-hit surface — near
        // (narrow depth of field) the focus is trustworthy so we refine the range; far (wide DoF) we only flag it.
        if (AutoFocus.IsChecked == true && _liveCfg != null && _floodAccum != null &&
            _liveTotalCounts > 500 && _liveTickCount % 8 == 0)
        {
            double laser = OptFocal.Value;
            var chk = MixedFieldStudy.CheckFocus(_floodAccum, _liveCfg, new DefaultSimulationFactory(),
                laser, Math.Max(OptFocal.Minimum, laser * 0.4), Math.Min(OptFocal.Maximum, laser * 2.5), 14);
            double delta = chk.BestFocalMm - laser;
            bool sharperElsewhere = chk.BestSnr > chk.LaserSnr * 1.2 && Math.Abs(delta) > 150.0;
            if (sharperElsewhere && chk.FwhmMm < 250.0)         // near: trustworthy → refine the range
            {
                double snapped = Math.Clamp(Math.Round(chk.BestFocalMm / 50.0) * 50.0, OptFocal.Minimum, OptFocal.Maximum);
                if (Math.Abs(snapped - laser) >= 50.0)
                {
                    OptFocal.Value = snapped;
                    Log($"fusion: source ~{delta:+0} mm off the laser surface → range corrected to {snapped:F0} mm");
                    ImgStatus.Text = $"fusion: laser {laser:F0}mm + focus Δ{delta:+0} → range {snapped:F0}mm";
                }
            }
            else if (sharperElsewhere)                          // far: can't refine, flag the mismatch
            {
                Log($"⚠ fusion: laser {laser:F0} mm but image sharpest ~{chk.BestFocalMm:F0} mm — target may be off the laser surface (Δ{delta:+0}, coarse)");
                ImgStatus.Text = $"⚠ laser {laser / 1000:F1}m vs image ~{chk.BestFocalMm / 1000:F1}m — target may be off the laser surface";
            }
        }

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
            case 4: RenderDetector(); break;   // Detector face — static, no acquisition needed
        }
    }

    // ---- Detector face: per-crystal gain × fill factor (reflector gaps) ----------------------------------

    private void DetField_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsLoaded && MainTabs.SelectedIndex == 4) RenderDetector();
    }

    private void DetRedraw_Click(object sender, RoutedEventArgs e) => RenderDetector();

    /// <summary>Render the detector face at sub-pixel resolution: each crystal is coloured by its seeded gain, and
    /// the reflector / saw-kerf gap between crystals (the dead region) is drawn as NaN over a dark background — so
    /// the fixed-pattern non-uniformity (gain spread) and the fill factor are both visible.</summary>
    private void RenderDetector()
    {
        if (DetPlot == null) return;
        int n = Math.Clamp((int)ParseD(OptDetN.Text, 30), 4, 64);
        double pitch = Math.Max(0.05, ParseD(OptDetPitch.Text, 0.6));
        double gapMm = Math.Max(0.0, ParseD(DetGapUm.Text, 100) / 1000.0);
        gapMm = Math.Min(gapMm, pitch * 0.9);
        double gainSigma = Math.Max(0.0, ParseD(ImgGainSigma.Text, 3) / 100.0);
        int seed = (int)ParseD(ImgUnifSeed.Text, 1);

        var dcfg = new DetectorConfig { PixelsX = n, PixelsY = n, PixelPitchMm = pitch, GainSigma = gainSigma, UniformitySeed = seed };
        var sens = new CrystalUniformity(dcfg).Sensitivity;   // per-pixel gain (row-major, mean ≈ 1)

        const int SUB = 12;                                   // sub-pixel cells per crystal to resolve the gap
        int g = n * SUB;
        var grid = new double[g, g];
        double half = gapMm * 0.5;
        for (int py = 0; py < n; py++)
            for (int px = 0; px < n; px++)
            {
                double gain = sens[py * n + px];
                for (int sy = 0; sy < SUB; sy++)
                    for (int sx = 0; sx < SUB; sx++)
                    {
                        double lx = (sx + 0.5) / SUB * pitch, ly = (sy + 0.5) / SUB * pitch;
                        bool gap = lx < half || lx > pitch - half || ly < half || ly > pitch - half;
                        grid[py * SUB + sy, px * SUB + sx] = gap ? double.NaN : gain;
                    }
            }

        double fill = Math.Pow((pitch - gapMm) / pitch, 2.0);
        double contactXtalk = Math.Clamp(ParseD(DetCrosstalk.Text, 0) / 100.0, 0.0, 0.9);
        double effXtalk = contactXtalk * Math.Exp(-gapMm / 0.04);   // coupled to the reflector thickness (λ=40µm)
        double sipmPitch = Math.Max(pitch, ParseD(DetSipmPitch.Text, pitch));
        int sipmBlock = Math.Max(1, (int)Math.Round(sipmPitch / pitch));
        DrawDetector(DetPlot, grid, n * pitch, sipmBlock > 1 ? sipmBlock * pitch : 0.0,
            "Detector face — per-crystal gain (colour), reflector gaps (dark), SiPM grid (white)");
        DetReadout.Text = $"fill factor {fill:P0}  ·  active {(pitch - gapMm) * 1000:F0} µm / {pitch * 1000:F0} µm pitch" +
                          $"  ·  crosstalk {contactXtalk:P0}→{effXtalk:P1} eff" +
                          $"  ·  SiPM {(sipmBlock > 1 ? $"{sipmBlock}×{sipmBlock} crystals/SiPM ({sipmBlock * pitch:F1}mm)" : "1:1")}" +
                          $"  ·  gain σ {gainSigma:P0}, seed {seed}";
    }

    private static void DrawDetector(ScottPlot.WPF.WpfPlot view, double[,] grid, double sizeMm, double sipmPitchMm,
        string title)
    {
        var p = view.Plot;
        p.Clear();
        var hm = p.Add.Heatmap(grid);
        hm.Extent = new ScottPlot.CoordinateRect(0, sizeMm, 0, sizeMm);
        hm.Colormap = new ScottPlot.Colormaps.Viridis();
        p.DataBackground.Color = ScottPlot.Color.FromHex("#202020");   // NaN reflector gaps show as dark

        // SiPM readout grid — one cell reads a block of crystals (light-sharing). White lines at the SiPM pitch.
        if (sipmPitchMm > 0.0)
            for (double c = sipmPitchMm; c < sizeMm - 1e-6; c += sipmPitchMm)
            {
                var vl = p.Add.VerticalLine(c); vl.Color = ScottPlot.Colors.White.WithAlpha(0.6); vl.LineWidth = 1;
                var hl = p.Add.HorizontalLine(c); hl.Color = ScottPlot.Colors.White.WithAlpha(0.6); hl.LineWidth = 1;
            }

        p.Title(title);
        p.XLabel("x (mm)");
        p.YLabel("y (mm)");
        p.Axes.AutoScale();
        view.Refresh();
    }

    // Compton-strip toggle: re-decode the accumulated floods with/without stripping (no new MC needed).
    private void ImgStrip_Changed(object sender, RoutedEventArgs e)
    {
        if (_liveRunning && MainTabs.SelectedIndex == 1) RenderImaging();
    }

    private void RenderImaging()
    {
        if (_channels.Count == 0 || _liveDecoder == null || _floodAccum == null) return;
        int w = _floodAccum.Width, h = _floodAccum.Height;

        // Decode EACH nuclide channel's own flood → a reconstruction with (mostly) only that nuclide's sources.
        // Normalize each and composite them, isotope-coloured, so co-measured nuclides separate in the image.
        // With Compton strip on, first subtract each higher-energy channel's calibrated downscatter per pixel —
        // so a lower isotope CO-LOCATED with a higher one (which the spatial decode alone can't split) is recovered.
        bool strip = ImgStrip?.IsChecked == true;
        var recons = new List<(NuclideChannel ch, DetectorImage recon, double max)>();
        double originMm = 0, stepMm = 1; int rw = 0, rh = 0;
        var floodSum = new DetectorImage(w, h);
        foreach (var ch in _channels)
        {
            var flood = ch.FloodAccum;
            if (strip && ch.Strip.Count > 0)
            {
                flood = new DetectorImage(w, h);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        double v = ch.FloodAccum[x, y];
                        foreach (var (hiIdx, r) in ch.Strip) v -= r * _channels[hiIdx].FloodAccum[x, y];
                        flood[x, y] = Math.Max(0.0, v);
                    }
            }
            var dr = _liveDecoder.Decode(flood);
            var rec = dr.Reconstruction;
            double mx = 0; foreach (var v in rec.Raw) if (v > mx) mx = v;
            recons.Add((ch, rec, mx));
            originMm = dr.ReconOriginMm; stepMm = dr.ReconStepMm; rw = rec.Width; rh = rec.Height;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) floodSum.Add(x, y, ch.FloodAccum[x, y]);
        }
        if (rw == 0) return;

        DrawHeatmap(ImgFloodPlot, ToGrid(floodSum), 0, w, 0, h,
            "Detector flood map (all windows, counts)", "pixel x", "pixel y", null, null);

        // Imaging-tab recon heatmap = per-pixel MAX over each channel's NORMALIZED recon (so every nuclide's
        // source shows at comparable strength regardless of its count level), plus per-nuclide peak marks.
        var comp = new DetectorImage(rw, rh);
        var estList = new List<(double x, double y, string label)>();       // each found peak tagged with its nuclide
        foreach (var (ch, rec, mx) in recons)
        {
            if (mx > 0)
                for (int y = 0; y < rh; y++)
                    for (int x = 0; x < rw; x++)
                    {
                        double t = rec[x, y] / mx;
                        if (t > comp[x, y]) comp[x, y] = t;
                    }
            int kIso = Math.Max(1, _scene.Count(s => s.Isotope == ch.Isotope));
            foreach (var pk in MixedFieldStudy.TopPeaks(rec, originMm, stepMm, kIso, minSeparationMm: 2.5))
                estList.Add((pk.Xmm, pk.Ymm, ch.Isotope));
        }
        double left = originMm, right = originMm + (rw - 1) * stepMm;
        double bottom = originMm, top = originMm + (rh - 1) * stepMm;
        // flipY: the heatmap draws grid row 0 at the TOP, so flip rows to put +y up — matching the scene canvas
        // overlay (which flips the same way) and the data-space markers, so image and markers line up.
        DrawHeatmap(ImgReconPlot, ToGrid(comp, flipY: true), left, right, bottom, top,
            "Decoded reconstruction — per-nuclide (× sources, ○ found)", "x (mm)", "y (mm)",
            _liveTruePos, estList);

        // Scene-canvas overlay: colour composite (each nuclide tinted its isotope colour, normalized).
        SetReconOverlayComposite(recons, originMm, stepMm);
        RedrawScene();

        var truth = _scene.Select(s => new[] { s.X, s.Y }).ToArray();
        var matches = MixedFieldStudy.MatchOneToOne(truth, estList.Select(e => new FoundSource(e.x, e.y, 1.0)).ToArray());
        double worst = matches.Length > 0 ? matches.Max(m => m.ErrorMm) : 0.0;
        string stripNote = strip && _channels.Any(c => c.Strip.Count > 0) ? " · Compton-stripped" : "";
        ImgStatus.Text = $"{_channels.Count} nuclide(s) · {estList.Count} source(s) found · worst {worst:F2} mm{stripNote} — sharpens as counts build";
        _liveDetail = $"{_channels.Count} nuclide, {estList.Count} found, worst {worst:F1}mm{stripNote}";
    }

    private void RenderSpectrum()
    {
        if (_specCounts == null || _specCenters == null) return;
        // The spectrum shows the FULL deposit spectrum; the red ROI band marks the per-pixel photopeak window
        // that the imaging actually uses (the "% in window" is the photopeak-selected fraction).
        double win = 0, tot = 0;
        for (int i = 0; i < _specCounts.Length; i++)
        {
            tot += _specCounts[i];
            double c = _specCenters[i];
            foreach (var wnd in _specWindows)
                if (c >= wnd.lo && c <= wnd.hi) { win += _specCounts[i]; break; }
        }
        DrawSpectrum(SpPlot, _specCenters, _specCounts, _specWindows,
            "Energy spectrum — red band = per-pixel imaging photopeak window", "measured energy (keV)", "counts",
            logY: SpLogY.IsChecked == true);
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
        bool crrc = _feCrrc;
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
        double tau = _pulseTauSamples, tauRise = _pulseRiseSamples;   // derived from the selected chain
        int[] wave = realistic
            ? Waveform.Rasterize(stream, preset, tau: tau, tauRise: tauRise, intrinsicFwhm: _feResPct662 / 100.0)
            : Waveform.Rasterize(stream, preset, tau: tau, tauRise: 0.0, noiseKev: 0.0, intrinsicFwhm: 0.0);

        // Shaper pole-zero matched to THIS pulse tail tau (keeps the flat top / low-pass correct for the tail the
        // preamp produces; the RTL golden constants are the tau=5 defaults and are untouched by this display path).
        long[] sh;
        if (crrc)
        {
            int aQ16 = (int)Math.Round(Math.Exp(-1.0 / tau) * 65536.0);
            sh = Waveform.CrrcInt(wave, aQ16, Waveform.CrrcKQ16, Waveform.CrrcOrder);
        }
        else
        {
            int mQ8 = (int)Math.Round(256.0 / (Math.Exp(1.0 / tau) - 1.0));
            sh = Waveform.TrapShape(wave, Waveform.Rise, Waveform.Flat, mQ8);
        }

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

    private static double[,] ToGrid(DetectorImage img, bool flipY = false)
    {
        var g = new double[img.Height, img.Width];
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
                g[y, x] = flipY ? img[x, img.Height - 1 - y] : img[x, y];   // flipY: row 0 = max-y so +y points up
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
        IReadOnlyList<(double x, double y)>? truePositions,
        IReadOnlyList<(double x, double y, string label)>? estPositions)
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
                if (!string.IsNullOrEmpty(ep.label))     // tag the found peak with its nuclide
                {
                    var txt = p.Add.Text(ep.label, ep.x, ep.y);
                    txt.LabelFontColor = ScottPlot.Colors.Red;
                    txt.LabelFontSize = 12;
                    txt.LabelBold = true;
                    txt.OffsetX = 8;                     // nudge clear of the circle (screen px)
                    txt.OffsetY = -8;
                }
            }

        p.Title(title);
        p.XLabel(xlabel);
        p.YLabel(ylabel);
        p.Axes.AutoScale();
        view.Refresh();
    }

    private static void DrawSpectrum(ScottPlot.WPF.WpfPlot view, double[] centers, double[] counts,
        IReadOnlyList<(double lo, double hi, double energy)> windows, string title, string xlabel, string ylabel,
        bool logY)
    {
        var p = view.Plot;
        p.Clear();

        // On a log axis, bars grow from a baseline of 0 in the TRANSFORMED coordinate = 10^0 = 1 count, so
        // we plot log10(count) with an empty bin (count <= 1) collapsing to zero height. This is the standard
        // spectroscopy view: it lifts the Compton continuum (bin ~10s of counts) out from under a photopeak
        // that towers ~20x above it on a linear scale.
        double Ty(double c) => logY ? Math.Log10(Math.Max(c, 1.0)) : c;

        double maxCount = 1.0;
        foreach (var c in counts) if (c > maxCount) maxCount = c;
        double top = Ty(maxCount);

        // Photopeak ROI bands — one per emission line (translucent), with a labelled edge at each line.
        foreach (var wnd in windows)
        {
            var band = p.Add.Rectangle(wnd.lo, wnd.hi, 0, top);
            band.FillColor = ScottPlot.Colors.Red.WithAlpha(0.10);
            band.LineColor = ScottPlot.Colors.Transparent;
            var line = p.Add.VerticalLine(wnd.energy);
            line.Color = ScottPlot.Colors.Red.WithAlpha(0.55);
            line.LineWidth = 1;
            line.LabelText = $"{wnd.energy:F0}";
        }

        // Draw the spectrum as a filled outline (MCA-style curve) rather than fat bars — with fine bins it
        // reads as a smooth trace. Fill to the baseline for a spectrum look.
        var ys = new double[counts.Length];
        for (int i = 0; i < counts.Length; i++) ys[i] = Ty(counts[i]);
        var trace = p.Add.Scatter(centers, ys);
        trace.MarkerSize = 0;
        trace.LineWidth = 1.4f;
        trace.LineColor = ScottPlot.Color.FromHex("#2a9d63");
        trace.FillY = true;
        trace.FillYColor = ScottPlot.Color.FromHex("#2a9d63").WithAlpha(0.22);
        trace.FillYValue = 0;

        if (logY)
        {
            // Label the log ticks back in real counts (10^tick) with decade minor ticks.
            p.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic
            {
                MinorTickGenerator = new ScottPlot.TickGenerators.LogMinorTickGenerator(),
                IntegerTicksOnly = true,
                LabelFormatter = y => $"{Math.Pow(10, y):N0}",
            };
        }
        else
        {
            p.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
        }

        p.Title(title);
        p.XLabel(xlabel);
        p.YLabel(logY ? ylabel + " (log)" : ylabel);
        p.Axes.AutoScale();
        view.Refresh();
    }

    private void SpLogY_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        // Re-render immediately if the spectrum is the visible tab (works whether or not a run is live).
        if (IsLoaded && MainTabs.SelectedIndex == 2) RenderSpectrum();
    }

    // ---- detection chain (scintillator / photosensor / preamp presets) -----------------------------------

    private void FrontEnd_Changed(object sender, SelectionChangedEventArgs e) => UpdateFrontEnd();

    /// <summary>Rebuild the front-end model from the three selected parts: N_pe = lightYield·collection·PDE·E
    /// sets the resolution (FrontEndModel), the scintillator decay sets the pulse's leading edge, and the preamp
    /// tail + shaper set the falling edge and the DAQ filter. The pulse re-renders live; the derived resolution
    /// drives the Spectrum's photopeak width on the next acquisition.</summary>
    private void UpdateFrontEnd()
    {
        if (FeScint == null || FeScint.SelectedIndex < 0 ||
            FeSensor.SelectedIndex < 0 || FePreamp.SelectedIndex < 0) return;   // still populating

        var sc = FrontEndParts.Scintillators[FeScint.SelectedIndex];
        var se = FrontEndParts.Sensors[FeSensor.SelectedIndex];
        var pa = FrontEndParts.Preamps[FePreamp.SelectedIndex];
        _feConfig = FrontEndParts.BuildConfig(sc, se, pa);
        _feCrrc = pa.Crrc;

        // The charge-sensitive preamp output is the convolution of the scintillation decay with the preamp
        // response — a bi-exponential of the two time constants, where the SLOWER one sets the tail and the
        // faster the leading edge. So a fast scintillator (GAGG) + slow preamp gives a preamp-limited tail, while
        // a slow scintillator (CsI) dominates its own tail regardless of the fast preamp. (Taking min/max also
        // keeps tau_fall > tau_rise, so the bi-exp never inverts.)
        double nsPerSample = 1e9 / AdcSampleRateHz;                 // 8 ns at 125 MSPS
        double riseNs = Math.Min(sc.DecayNs, pa.PulseTailNs);
        double fallNs = Math.Max(sc.DecayNs, pa.PulseTailNs);
        _pulseRiseSamples = Math.Max(0.5, riseNs / nsPerSample);
        _pulseTauSamples = Math.Max(_pulseRiseSamples + 0.5, fallNs / nsPerSample);

        var model = new FrontEndModel(_feConfig);
        _feResPct662 = model.FwhmFraction(662.0) * 100.0;
        SpResolution.Text = _feResPct662.ToString("F1", CultureInfo.InvariantCulture);   // spectrum uses this on next Simulate

        FeReadout.Text = $"→ N_pe(662) ≈ {model.Photoelectrons(662):N0},  R(662) ≈ {_feResPct662:F1}%,  " +
                         $"pulse rise {riseNs:F0} ns / tail {fallNs:F0} ns,  {(pa.Crrc ? "CR-RC^4" : "trapezoid")}";

        if (IsLoaded && _liveRunning && MainTabs.SelectedIndex == 0) RenderWaveform();
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
