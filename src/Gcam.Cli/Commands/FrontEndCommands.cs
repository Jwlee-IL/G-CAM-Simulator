using System.Text;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using static Gcam.Cli.ConsoleRender;

namespace Gcam.Cli;

/// <summary>Detector front-end and readout: shaping, RTL event stream, thermal, pile-up, dead time, bad pixels.</summary>
internal static class FrontEndCommands
{
    internal static int RunFrontend(string[] args)
    {
        // Energy resolution from the photoelectron budget — the C# FrontEndModel (port of rtl/frontend_model.py),
        // now folded into the pipeline (the crystal-Compton detector smears each deposit by this 1/√E FWHM).
        Console.WriteLine("SiPM front-end — energy resolution from the photoelectron budget (collection 0.60, PDE 0.45, ENF 1.20)");
        Console.WriteLine();
        Console.WriteLine("  crystal   LY    N_pe@662   R_stat   R_intr   R_tot@662   R_tot@1332   regime");
        Console.WriteLine("  -------   --   --------   ------   ------   ---------   ----------   ------");
        // name: (light yield ph/keV, intrinsic FWHM fraction)
        (string name, double ly, double intr)[] crystals =
        {
            ("GAGG", 50, 0.050), ("CeBr3", 45, 0.032), ("LaBr3", 63, 0.022),
            ("LYSO", 30, 0.070), ("BGO", 9, 0.080), ("NaI", 38, 0.055),
        };
        foreach (var (name, ly, intr) in crystals)
        {
            var m = new FrontEndModel(new FrontEndConfig
            {
                LightYieldPhPerKeV = ly, CollectionEfficiency = 0.60, SipmPde = 0.45,
                ExcessNoiseFactor = 1.20, IntrinsicResolutionFwhm = intr,
            });
            double npe = m.Photoelectrons(662.0);
            double rStat = System.Math.Sqrt(System.Math.Max(0.0, m.FwhmFraction(662.0) * m.FwhmFraction(662.0) - intr * intr));
            string regime = intr > rStat ? "crystal-limited" : "photon-limited";
            Console.WriteLine($"  {name,-6}   {ly,2:F0}   {npe,8:F0}   {rStat,5:P1}   {intr,5:P1}   {m.FwhmFraction(662.0),8:P1}    {m.FwhmFraction(1332.0),8:P1}    {regime}");
        }
        Console.WriteLine();
        Console.WriteLine("R_stat = 2.355·√(ENF/N_pe) scales 1/√E; past the knee the crystal's intrinsic floor dominates");
        Console.WriteLine("(more PDE wasted). Enable in a scenario via detector.frontEnd — the Compton detector then");
        Console.WriteLine("smears each deposit by R_tot(E), so energy windows / isotope separation reflect real resolution.");
        Console.WriteLine();

        // DCR as background: dark counts add a PARALLEL-noise term that scales 1/E (worse at low energy).
        const double dcrHz = 1.0e6, tauNs = 200.0;   // ~1 Mcps SiPM (S13360-3050CS class), 200 ns integration
        var clean = new FrontEndModel(new FrontEndConfig { LightYieldPhPerKeV = 45, IntrinsicResolutionFwhm = 0.032 });
        var withDcr = new FrontEndModel(new FrontEndConfig { LightYieldPhPerKeV = 45, IntrinsicResolutionFwhm = 0.032,
            DarkCountRateHz = dcrHz, IntegrationTimeNs = tauNs });
        Console.WriteLine($"SiPM dark counts as background (CeBr3, DCR {dcrHz / 1e6:F1} Mcps, {tauNs:F0} ns window):");
        Console.WriteLine("  energy    R_tot(no DCR)   R_tot(+DCR)   R_dcr alone   (DCR is a 1/E parallel-noise term)");
        foreach (double e in new[] { 122.0, 662.0, 1332.0 })
            Console.WriteLine($"  {e,5:F0} keV   {clean.FwhmFraction(e),10:P2}    {withDcr.FwhmFraction(e),9:P2}    {withDcr.DcrFwhmFraction(e),9:P3}");
        Console.WriteLine("  -> negligible at the photopeak, grows toward low energy: DCR matters for weak / low-line sources.");
        return 0;
    }

    internal static int RunEventStream(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo eventstream <base.json> [rateKcps] [maxEvents] [bgRatio]"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        double rateKcps = args.Length > 2 ? double.Parse(args[2]) : 500.0;
        int maxEvents = args.Length > 3 ? int.Parse(args[3]) : 1500;
        const double adcSampleRateHz = 100e6;   // 100 MSPS front-end (Ts = 10 ns; trap_ref tau = 5 samples = 50 ns)

        // A background ratio on the command line opts this run into an ambient field (overrides the config's
        // Background); without it the config's Background (if any) is used, else the stream is clean.
        if (args.Length > 4)
        {
            double bgRatio = double.Parse(args[4]);
            baseConfig.Background = bgRatio > 0.0
                ? new BackgroundConfig { BackgroundToSignalRatio = bgRatio, EnergyKeV = baseConfig.Background?.EnergyKeV ?? 200.0 }
                : null;
        }

        var study = new EventStreamStudy();
        var events = study.Generate(baseConfig, rateKcps * 1e3, adcSampleRateHz, maxEvents);
        var (count, minKeV, maxKeV, meanGap) = EventStreamStudy.Summary(events);

        Directory.CreateDirectory("rtl");
        string path = "rtl/event_stream.txt";
        File.WriteAllText(path, EventStreamStudy.ToText(events, rateKcps * 1e3, adcSampleRateHz, baseConfig.Name));

        Console.WriteLine("MC -> RTL event stream (crystal-Compton deposits + Poisson arrival overlay)");
        Console.WriteLine($"  Field    : {baseConfig.Source.Isotope} @ {baseConfig.Source.EnergyKeV} keV" +
            (baseConfig.Sources is { Length: > 0 } ss ? $"  (+{ss.Length - 1} more sources / mixed field)" : ""));
        Console.WriteLine($"  Rate     : {rateKcps:F0} kcps @ {adcSampleRateHz / 1e6:F0} MSPS  ->  mean gap {meanGap:F0} samples");
        if (baseConfig.Background is { BackgroundToSignalRatio: > 0.0 } bgc)
            Console.WriteLine($"  Backgrd  : BSR {bgc.BackgroundToSignalRatio:F2} @ {bgc.EnergyKeV:F0} keV" +
                (bgc.DarkCountRateKcps is > 0.0 ? $" + DCR {bgc.DarkCountRateKcps:F0} kcps" : "") + "  (uncoded ambient events merged in)");
        Console.WriteLine($"  Events   : {count}   deposited energy {minKeV:F0}..{maxKeV:F0} keV (photopeak + Compton continuum" +
            (baseConfig.Background is { BackgroundToSignalRatio: > 0.0 } ? " + background)" : ")"));
        Console.WriteLine($"  Written  : {path}");
        Console.WriteLine();
        Console.WriteLine("  Drive the RTL shaper with it:  cd rtl && python run_cocotb.py   (mc_event_stream_matches_reference)");
        Console.WriteLine("  Recovery / pile-up analysis :  cd rtl && python event_stream_study.py  -> event_stream.png");
        return 0;
    }

    internal static int RunThermal(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo thermal <base.json> [out_prefix]");
            Console.Error.WriteLine("  Models SiPM gain drift DURING an acquisition (ambient + self-heating) and");
            Console.Error.WriteLine("  its effect on a per-crystal photopeak-window system, bias-comp OFF vs ON.");
            return 1;
        }

        string prefix = args.Length >= 3 ? args[2] : "samples/thermal";
        const double photonBudget = 800_000.0;
        const int repeats = 60;

        var baseConfig = ConfigLoader.Load(args[1]);
        // Activate a per-crystal photopeak window + static non-uniformity so the drift has something to walk in.
        if (baseConfig.Detector.EnergyResolutionFwhm <= 0.0) baseConfig.Detector.EnergyResolutionFwhm = 0.08;
        if (baseConfig.Detector.EnergyWindowFraction <= 0.0) baseConfig.Detector.EnergyWindowFraction = 0.10;
        if (baseConfig.Detector.EnergyResolutionFwhmSigma <= 0.0) baseConfig.Detector.EnergyResolutionFwhmSigma = 0.30;
        if (baseConfig.Detector.GainSigma <= 0.0) baseConfig.Detector.GainSigma = 0.05;
        if (baseConfig.Detector.GainGradient <= 0.0) baseConfig.Detector.GainGradient = 0.10;

        int W = baseConfig.Detector.PixelsX, H = baseConfig.Detector.PixelsY;
        int n = 13;
        var times = new double[n];
        for (int i = 0; i < n; i++) times[i] = (double)i / (n - 1);

        // Physical drift: SiPM gain −0.7%/°C, ambient +3°C linear over the run, self-heating +8°C centre hotspot
        // (τ 0.3, corner 40% of centre). Compare an uncompensated array with a 90%-effective bias-comp loop.
        ThermalDrift Make(double comp) => new ThermalDrift(W, H,
            alphaPerC: -0.007, biasCompFraction: comp,
            ambientRatePerT: 3.0, ambientSwingC: 0.0, ambientPeriod: 0.0,
            selfHeatC: 8.0, selfHeatTau: 0.3, edgeFactor: 0.4);

        var study = new ThermalDriftStudy(new DefaultSimulationFactory());
        var off = study.Run(baseConfig, Make(0.0), times, photonBudget, repeats);
        var on = study.Run(baseConfig, Make(0.9), times, photonBudget, repeats);

        File.WriteAllText(prefix + "_off.csv", ThermalDriftStudy.ToCsv(off));
        File.WriteAllText(prefix + "_on.csv", ThermalDriftStudy.ToCsv(on));

        Console.WriteLine("Thermal-drift study: SiPM gain -0.7%/C, ambient +3C linear + self-heating +8C hotspot (tau 0.3).");
        Console.WriteLine("Per-crystal photopeak window; flood correction uses the t=0 calibration map.");
        Console.WriteLine();
        Console.WriteLine("                  bias-comp OFF                    bias-comp ON (90%)");
        Console.WriteLine("  time  ctrDeg   eff    resid%   bias    |   eff    resid%   bias");
        Console.WriteLine("  ----  ------   ----   ------   -----   |   ----   ------   -----");
        for (int i = 0; i < n; i++)
        {
            var f = off[i]; var o = on[i];
            Console.WriteLine($"  {f.Time,4:F2}  {f.CenterDeltaC,5:F1}   {f.Efficiency,5:P1} {f.ResidualCoV,7:P1} {f.RmsBiasMm,6:F2}mm  | " +
                              $" {o.Efficiency,5:P1} {o.ResidualCoV,7:P1} {o.RmsBiasMm,6:F2}mm");
        }
        Console.WriteLine();
        Console.WriteLine($"End of acquisition: OFF loses {1 - off[^1].Efficiency,4:P1} of photopeak counts, residual {off[^1].ResidualCoV,4:P1};");
        Console.WriteLine($"                    ON  holds {on[^1].Efficiency,4:P1} efficiency, residual {on[^1].ResidualCoV,4:P1}.");
        Console.WriteLine("Drift is an ENERGY-WINDOW (efficiency) problem, not a localization one: the bias tracks the decoder floor.");
        Console.WriteLine($"CSV written: {prefix}_off.csv, {prefix}_on.csv");
        return 0;
    }

    internal static int RunThermalReadout(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo thermalro <base.json> [out.csv]");
            Console.Error.WriteLine("  Thermal DCR/PDE readout effects (beyond the gain-centroid drift): dark-count rate,");
            Console.Error.WriteLine("  low-energy vs photopeak resolution, and PDE droop vs temperature deviation.");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/thermalro.csv";
        var cfg = ConfigLoader.Load(args[1]);
        var fe = cfg.Detector.FrontEnd ?? new FrontEndConfig();
        if (!(fe.DarkCountRateHz > 0)) fe = new FrontEndConfig
        {
            LightYieldPhPerKeV = fe.LightYieldPhPerKeV, CollectionEfficiency = fe.CollectionEfficiency,
            SipmPde = fe.SipmPde, ExcessNoiseFactor = fe.ExcessNoiseFactor,
            IntrinsicResolutionFwhm = fe.IntrinsicResolutionFwhm, DarkCountRateHz = 1.0e6, IntegrationTimeNs = 300,
        };
        var thermal = new ThermalDrift(cfg.Detector.PixelsX, cfg.Detector.PixelsY);
        double[] dts = [0, 4, 8, 12, 16, 20, 24, 30];
        var rows = new ThermalReadoutStudy().Run(fe, thermal, dts, lowLineKeV: 60.0, photopeakKeV: 661.7);
        File.WriteAllText(csvPath, ThermalReadoutStudy.ToCsv(rows));

        Console.WriteLine($"Thermal readout study: DCR₀ = {fe.DarkCountRateHz / 1e6:F1} Mcps, doubling every {thermal.DcrDoublingC:F0} °C.");
        Console.WriteLine("Bias compensation nulls the GAIN drift (theme 36) but NOT the thermal dark generation.");
        Console.WriteLine();
        Console.WriteLine("   ΔT(°C)   DCR(Mcps)   res@60keV   res@662keV   PDE");
        Console.WriteLine("   ------   ---------   ---------   ----------   ----");
        foreach (var r in rows)
            Console.WriteLine($"   {r.DeltaTC,5:F0}    {r.DcrHz / 1e6,8:F2}    {r.ResLowEPct,7:F2}%    {r.ResPhotopeakPct,7:F2}%   {r.PdeFactor:F3}");
        var c = rows[0]; var h = rows[^1];
        Console.WriteLine();
        Console.WriteLine($"Over {h.DeltaTC:F0} °C: DCR ×{h.DcrHz / c.DcrHz:F0}, PDE {(h.PdeFactor - 1) * 100:F0}%. The DCR term is ∝1/E, so the");
        Console.WriteLine($"low line degrades ~2× more (relatively) than the photopeak — but modestly for this high-light-yield");
        Console.WriteLine("crystal (statistics-dominated). The dominant effect is the raw DCR growth = a dark trigger / pile-up load.");
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }

    internal static int RunPileUp(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo pileup <base.json> [out.csv]");
            Console.Error.WriteLine("  Random-coincidence pile-up: overlapping pulses SUM into one recorded event,");
            Console.Error.WriteLine("  adding a self-convolution continuum above the photopeak + a throughput loss.");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/pileup.csv";
        const double adcSampleRateHz = 125e6;               // AD9648 125 MSPS (matches the WPF chain)
        const int maxEvents = 150_000;
        double[] rates = [50e3, 200e3, 500e3, 1e6, 2e6];    // detected count rates (cps)

        // Pulse-pair resolving time from the (default) shaper pulse: rise + 2·tail. Faster shaper → less pile-up.
        double tauRes = EventStreamStudy.ResolvingSamples(Waveform.TauRiseSamples, Waveform.TauSamples);
        double tauResNs = tauRes / adcSampleRateHz * 1e9;

        var baseConfig = ConfigLoader.Load(args[1]);
        double primary = baseConfig.Source.EnergyKeV > 0 ? baseConfig.Source.EnergyKeV : 661.7;
        const double maxE = 1500.0;
        const int bins = 750;                               // 2 keV/bin, spans up to the 2× sum region
        double bw = maxE / bins;

        static double[] Hist(IEnumerable<double> es, double bw, int bins)
        {
            var h = new double[bins];
            foreach (double e in es) { int b = (int)(e / bw); if (b >= 0 && b < bins) h[b] += 1; }
            return h;
        }

        var stream0 = new EventStreamStudy();
        // Singles reference: rate is irrelevant to the singles ENERGY spectrum (only its time spacing), so build once.
        var singlesStream = stream0.Generate(baseConfig, rates[0], adcSampleRateHz, maxEvents);
        double[] singlesE = singlesStream.Select(e => e.EnergyKeV).ToArray();
        double[] singlesHist = Hist(singlesE, bw, bins);
        double photopeakLo = primary * 0.90, sumLo = primary * 1.30;   // photopeak window / above-line sum region
        double SingPk = singlesE.Count(e => e >= photopeakLo && e <= primary * 1.10);

        Console.WriteLine($"Pile-up study: peak pile-up on the MC event stream, resolving time {tauRes:F0} samples " +
                          $"({tauResNs:F0} ns @ {adcSampleRateHz / 1e6:F0} MSPS, from shaper rise {Waveform.TauRiseSamples:F0}+2·tail {Waveform.TauSamples:F0}).");
        Console.WriteLine($"Primary line {primary:F0} keV; {singlesE.Length} detected events per rate.");
        Console.WriteLine();
        Console.WriteLine("   rate(cps)   R·τ     throughput   photopeak_kept   above-line(sum)%");
        Console.WriteLine("   ---------   -----   ----------   --------------   ----------------");

        double[]? piledShowcaseHist = null;
        double showcaseRate = rates[^2];   // 1 Mcps: clear continuum but not absurd
        foreach (double rate in rates)
        {
            var stream = stream0.Generate(baseConfig, rate, adcSampleRateHz, maxEvents);
            var piled = EventStreamStudy.ApplyPileUp(stream, tauRes);
            double[] piledE = piled.Select(e => e.EnergyKeV).ToArray();

            double throughput = (double)piled.Count / stream.Count;
            double pk = piledE.Count(e => e >= photopeakLo && e <= primary * 1.10);
            double aboveLine = piledE.Count(e => e > sumLo);
            double rtau = rate * tauRes / adcSampleRateHz;

            Console.WriteLine($"   {rate,9:N0}   {rtau,5:F3}   {throughput,10:P1}   {pk / SingPk,14:P1}   {aboveLine / piledE.Length,16:P2}");

            if (rate == showcaseRate) piledShowcaseHist = Hist(piledE, bw, bins);
        }

        // CSV: singles vs the showcase-rate piled spectrum (per 2-keV bin) for plotting / autoconvolution cross-check.
        var sb = new StringBuilder();
        sb.AppendLine($"# pileup spectrum  primary={primary:F1}keV  resolving_samples={tauRes:F1}  showcase_rate_cps={showcaseRate:G3}");
        sb.AppendLine("energy_keV,singles,piled");
        for (int i = 0; i < bins; i++)
            sb.AppendLine($"{(i + 0.5) * bw:F1},{singlesHist[i]:F0},{(piledShowcaseHist?[i] ?? 0):F0}");
        File.WriteAllText(csvPath, sb.ToString());

        Console.WriteLine();
        Console.WriteLine($"Above the {primary:F0} keV line, pile-up builds a self-convolution continuum up to the " +
                          $"{2 * primary:F0} keV sum peak; higher rate migrates more counts out of the photopeak.");
        Console.WriteLine($"CSV written: {csvPath} (singles vs {showcaseRate / 1e6:F1} Mcps piled, 2 keV bins).");
        return 0;
    }

    internal static int RunDeadTime(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo deadtime <base.json> [out.csv]");
            Console.Error.WriteLine("  Sweeps the true count rate vs the recorded rate under non-paralyzable and");
            Console.Error.WriteLine("  paralyzable dead time (MC event stream vs analytic), plus the live fraction.");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/deadtime.csv";
        const double adcSampleRateHz = 125e6;
        const int maxEvents = 200_000;
        const double deadTimeUs = 1.0;                    // DAQ per-pulse processing dead time
        double[] rates = [1e4, 5e4, 1e5, 3e5, 6e5, 1e6, 2e6, 5e6, 1e7];

        var baseConfig = ConfigLoader.Load(args[1]);
        var rows = new DeadTimeStudy().Run(baseConfig, rates, deadTimeUs, adcSampleRateHz, maxEvents);
        File.WriteAllText(csvPath, DeadTimeStudy.ToCsv(rows));

        Console.WriteLine($"Dead-time study: τ = {deadTimeUs:F1} µs (1/τ = {1e6 / deadTimeUs:N0} cps), {maxEvents:N0} events/rate.");
        Console.WriteLine("Recorded rate under non-paralyzable m=R/(1+Rτ) and paralyzable m=R·exp(−Rτ); MC vs analytic.");
        Console.WriteLine();
        Console.WriteLine("   true(cps)   R·τ    non-para(MC/an)      para(MC/an)        live np/p");
        Console.WriteLine("   ---------   ----   ----------------     ----------------   ---------");
        foreach (var r in rows)
            Console.WriteLine($"   {r.TrueRateCps,9:N0}   {r.Rtau,4:F2}   {r.RecordedNonParaCps,7:N0}/{r.AnalyticNonParaCps,-7:N0}  " +
                              $"{r.RecordedParaCps,7:N0}/{r.AnalyticParaCps,-7:N0}  {r.LiveNonPara,4:P0}/{r.LivePara,-4:P0}");

        Console.WriteLine();
        Console.WriteLine("Non-paralyzable saturates toward 1/τ; paralyzable PEAKS at R=1/τ then collapses (paralysis).");
        Console.WriteLine("Live fraction = recorded/true = the live-time vs real-time correction a real acquisition applies.");
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }

    internal static int RunDefects(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: montecarlo defects <base.json> [out.csv]");
            Console.Error.WriteLine("  Sweeps bad (dead/hot) pixel fraction vs an IDEAL decoder: raw flood vs a known");
            Console.Error.WriteLine("  bad-pixel-map repair (interpolate the flagged pixels).");
            return 1;
        }

        string csvPath = args.Length >= 3 ? args[2] : "samples/defects.csv";
        const double photonBudget = 800_000.0;
        const int repeats = 60;
        const double deadShare = 0.6;    // dead pixels are more common than hot
        const double hotFactor = 5.0;    // a hot pixel adds 5× the mean live-pixel counts
        double[] badPct = [0.0, 1.0, 2.0, 4.0, 8.0];

        var baseConfig = ConfigLoader.Load(args[1]);

        Console.WriteLine("Detector bad-pixel study: dead (holes) + hot (spikes) pixels vs the IDEAL decoder.");
        Console.WriteLine($"dead share {deadShare:P0}, hot pixel = {hotFactor:F0}× mean level; {repeats} Poisson reps, " +
                          $"averaged over defect maps. Repair = interpolate the known bad-pixel map.");
        Console.WriteLine();

        var rows = new DetectorDefectStudy(new DefaultSimulationFactory())
            .Run(baseConfig, badPct, deadShare, hotFactor, photonBudget, repeats, seeds: 8);
        File.WriteAllText(csvPath, DetectorDefectStudy.ToCsv(rows));

        Console.WriteLine("   bad%   dead%   hot%   RMS raw    RMS repaired");
        Console.WriteLine("   ----   -----   ----   --------   ------------");
        foreach (var r in rows)
            Console.WriteLine($"   {r.BadPixelPct,4:F1}   {r.DeadPct,5:F2}   {r.HotPct,4:F2}   {r.RmsRawMm,7:F2}mm   {r.RmsCorrMm,9:F2}mm");

        Console.WriteLine();
        Console.WriteLine("Bad pixels imprint fixed non-coded structure that pulls the correlation peak; a known bad-pixel");
        Console.WriteLine("map repairs most of it by interpolation (the discrete analogue of flood-field correction).");
        Console.WriteLine($"CSV written: {csvPath}");
        return 0;
    }
}
