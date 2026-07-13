using System.Globalization;
using System.Windows;
using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Detector;
using Genoray.MonteCarlo.Simulation;
using SimBackground = Genoray.MonteCarlo.Simulation.Background;

namespace Genoray.MonteCarlo.Wpf;

/// <summary>A small viewer over the GCAM simulation core: shape a realistic ADC waveform (native C# port of
/// the RTL reference), image a source through the coded mask, and see the deposited-energy spectrum. Heavy MC
/// work runs off the UI thread; ScottPlot renders the results.</summary>
public partial class MainWindow : Window
{
    private const double AdcSampleRateHz = 125e6;   // AD9648 125 MSPS

    public MainWindow()
    {
        InitializeComponent();
    }

    // ---- shared config -----------------------------------------------------------------------------------

    private static SimulationConfig BuildConfig(double srcX, double srcY, double energyKeV, long photons,
                                                double bsr = 0.0)
    {
        var cfg = new SimulationConfig
        {
            PhotonCount = photons,
            Seed = 12345,
            Source = new SourceConfig
            {
                Isotope = "Cs-137",
                EnergyKeV = energyKeV,
                Position = [srcX, srcY, 0.0],
                DirectionalBiasing = true,
            },
        };
        if (bsr > 0.0)
            cfg.Background = new BackgroundConfig { BackgroundToSignalRatio = bsr, EnergyKeV = 200.0 };
        return cfg;
    }

    private static double ParseD(string s, double fallback) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static int ParseI(string s, int fallback) =>
        int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    // ============================ WAVEFORM ============================

    private async void RunWaveform_Click(object sender, RoutedEventArgs e)
    {
        double energy = ParseD(WfEnergy.Text, 661.7);
        double rateKcps = ParseD(WfRate.Text, 50);
        int nEvents = Math.Clamp(ParseI(WfEvents.Text, 40), 1, 4000);
        bool realistic = WfRealistic.IsChecked == true;
        bool crrc = WfShaper.SelectedIndex == 1;

        WfRun.IsEnabled = false;
        WfStatus.Text = "running…";
        try
        {
            var (adc, shaped, recovered) = await Task.Run(() =>
            {
                var cfg = BuildConfig(0, 0, energy, photons: 200_000);
                var events = new EventStreamStudy().Generate(cfg, rateKcps * 1000.0, AdcSampleRateHz, nEvents);
                var stream = events.Select(ev => (ev.ArrivalSample, ev.EnergyKeV)).ToList();

                var preset = Waveform.DefaultAdc;
                int[] wave = realistic
                    ? Waveform.Rasterize(stream, preset)
                    : Waveform.Rasterize(stream, preset, tauRise: 0.0, noiseKev: 0.0, intrinsicFwhm: 0.0);

                long[] sh = crrc ? Waveform.CrrcInt(wave) : Waveform.TrapShape(wave);

                // recover the FIRST event's energy from the trapezoid flat top (CR-RC uses its peak).
                double rec = double.NaN;
                if (!crrc && stream.Count > 0)
                {
                    long ft = Waveform.FlatTop(sh, (int)stream[0].ArrivalSample);
                    rec = ft / Waveform.CalibrateFlatPerKev(preset);
                }
                return (wave, sh, rec);
            });

            DrawSignal(WfAdcPlot, ToD(adc), "ADC waveform (bi-exponential pulses, pile-up, noise, clip)",
                "sample  (8 ns @ 125 MSPS)", "ADC code");
            DrawSignal(WfShapedPlot, ToD(shaped),
                (crrc ? "CR-RC^4 shaped" : "Trapezoidal shaped") + "  (flat top ∝ deposited energy)",
                "sample", "shaper output");
            WfStatus.Text = $"{nEvents} events @ {rateKcps:F0} kcps" +
                (double.IsNaN(recovered) ? "" : $"   ·   1st event recovered ≈ {recovered:F0} keV");
        }
        catch (Exception ex)
        {
            WfStatus.Text = "error: " + ex.Message;
        }
        finally
        {
            WfRun.IsEnabled = true;
        }
    }

    // ============================ IMAGING ============================

    private async void RunImaging_Click(object sender, RoutedEventArgs e)
    {
        double sx = ParseD(ImgX.Text, 2), sy = ParseD(ImgY.Text, 0);
        long photons = (long)ParseD(ImgPhotons.Text, 500_000);
        double bsr = ParseD(ImgBsr.Text, 0);

        ImgRun.IsEnabled = false;
        ImgStatus.Text = "running…";
        try
        {
            var r = await Task.Run(() =>
            {
                var cfg = BuildConfig(sx, sy, 661.7, photons);
                var factory = new DefaultSimulationFactory();
                var res = new SimulationRunner(factory).Run(cfg);
                var flood = res.DetectorImage;

                if (bsr > 0.0)
                {
                    double ped = SimBackground.PedestalPerPixel(bsr, SumImage(flood), flood.Width * flood.Height);
                    SimBackground.AddUniform(flood, ped);
                }

                var dec = factory.CreateDecoder(cfg)!.Decode(flood);
                return (flood, dec);
            });

            var (flood, dec) = r;
            DrawHeatmap(ImgFloodPlot, ToGrid(flood), 0, flood.Width, 0, flood.Height,
                "Detector flood map (counts)", "pixel x", "pixel y", null, null);

            double estX = dec.Estimate.Position.X, estY = dec.Estimate.Position.Y;
            double err = Math.Sqrt((estX - sx) * (estX - sx) + (estY - sy) * (estY - sy));
            var recon = dec.Reconstruction;
            double left = dec.ReconOriginMm, right = dec.ReconOriginMm + (recon.Width - 1) * dec.ReconStepMm;
            double bottom = dec.ReconOriginMm, top = dec.ReconOriginMm + (recon.Height - 1) * dec.ReconStepMm;
            DrawHeatmap(ImgReconPlot, ToGrid(recon), left, right, bottom, top,
                "Decoded reconstruction (× true, ○ estimate)", "x (mm)", "y (mm)",
                (sx, sy), (estX, estY));

            ImgStatus.Text = $"estimate ({estX:F2}, {estY:F2}) mm   ·   error {err:F2} mm" +
                (bsr > 0 ? $"   ·   BSR {bsr:F1}" : "");
        }
        catch (Exception ex)
        {
            ImgStatus.Text = "error: " + ex.Message;
        }
        finally
        {
            ImgRun.IsEnabled = true;
        }
    }

    // ============================ SPECTRUM ============================

    private async void RunSpectrum_Click(object sender, RoutedEventArgs e)
    {
        double energy = ParseD(SpEnergy.Text, 661.7);
        int nEvents = Math.Clamp(ParseI(SpEvents.Text, 4000), 100, 50_000);
        double windowFrac = ParseD(SpWindow.Text, 10) / 100.0;

        SpRun.IsEnabled = false;
        SpStatus.Text = "running…";
        try
        {
            var (centers, counts, inWindow) = await Task.Run(() =>
            {
                var cfg = BuildConfig(0, 0, energy, photons: 400_000);
                var events = new EventStreamStudy().Generate(cfg, 100_000.0, AdcSampleRateHz, nEvents);
                var deps = events.Select(ev => ev.EnergyKeV).ToArray();

                double lo = energy * (1.0 - windowFrac), hi = energy * (1.0 + windowFrac);
                int win = deps.Count(d => d >= lo && d <= hi);

                const int bins = 128;
                double max = energy * 1.15;
                var cnt = new double[bins];
                var ctr = new double[bins];
                double bw = max / bins;
                for (int i = 0; i < bins; i++) ctr[i] = (i + 0.5) * bw;
                foreach (var d in deps)
                {
                    int b = (int)(d / bw);
                    if (b >= 0 && b < bins) cnt[b] += 1;
                }
                return (ctr, cnt, win / (double)deps.Length);
            });

            double lo = energy * (1.0 - windowFrac), hi = energy * (1.0 + windowFrac);
            DrawSpectrum(SpPlot, centers, counts, lo, hi,
                $"Deposited-energy spectrum ({energy:F0} keV source)", "deposited energy (keV)", "counts");
            SpStatus.Text = $"±{windowFrac:P0} photopeak window holds {inWindow:P0} of events";
        }
        catch (Exception ex)
        {
            SpStatus.Text = "error: " + ex.Message;
        }
        finally
        {
            SpRun.IsEnabled = true;
        }
    }

    // ---- plotting helpers --------------------------------------------------------------------------------

    private static double[] ToD(int[] xs)
    {
        var d = new double[xs.Length];
        for (int i = 0; i < xs.Length; i++) d[i] = xs[i];
        return d;
    }

    private static double[] ToD(long[] xs)
    {
        var d = new double[xs.Length];
        for (int i = 0; i < xs.Length; i++) d[i] = xs[i];
        return d;
    }

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
        double left, double right, double bottom, double top,
        string title, string xlabel, string ylabel,
        (double x, double y)? truePos, (double x, double y)? estPos)
    {
        var p = view.Plot;
        p.Clear();
        var hm = p.Add.Heatmap(grid);
        hm.Extent = new ScottPlot.CoordinateRect(left, right, bottom, top);
        hm.Colormap = new ScottPlot.Colormaps.Viridis();
        p.Add.ColorBar(hm);

        if (truePos is { } tp)
        {
            var m = p.Add.Marker(tp.x, tp.y);
            m.Color = ScottPlot.Colors.White;
            m.Size = 18;
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
}
