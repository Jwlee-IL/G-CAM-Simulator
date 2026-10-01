using System.Text;
using Gcam.Configuration;
using Gcam.Simulation;

namespace Gcam.Cli;

/// <summary>Dose rate from the detector spectrum: G(E) weighting vs the ICRP 74 truth, angle, over-range (theme 54).</summary>
internal static class DoseCommands
{
    internal static int RunDose(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: montecarlo dose <base.json> [out-prefix]"); return 1; }
        var baseConfig = ConfigLoader.Load(args[1]);
        string prefix = args.Length >= 3 ? args[2] : "samples/dose";
        const double distanceMm = 1000.0;
        const long photons = 400_000;
        const int order = 4;
        const double deadTimeUs = 1.0;                              // theme 42's DAQ dead time
        double[] fitKeV = [50, 60, 80, 100, 150, 200, 300, 400, 500, 600, 800, 1000, 1250, 1500];
        double[] checkKeV = [70, 122, 250, 662, 1173, 1332];
        double[] angleKeV = [60, 122, 316, 662, 1250];
        double[] angles = [0, 2.5, 5, 10, 20, 30, 45, 60];
        string[] isotopes = ["Am-241", "Co-57", "Ir-192", "Cs-137", "Co-60"];

        var study = new DoseStudy();
        var jobs = new List<(double e, double a)>();
        foreach (double e in fitKeV) jobs.Add((e, 0));
        foreach (double e in checkKeV) jobs.Add((e, 0));
        foreach (var iso in isotopes) foreach (var l in Isotopes.Get(iso).Lines) jobs.Add((l.EnergyKeV, 0));
        foreach (double e in angleKeV) foreach (double a in angles) jobs.Add((e, a));
        jobs = jobs.Distinct().ToList();
        var resp = new System.Collections.Concurrent.ConcurrentDictionary<(double, double), DoseResponse>();
        Parallel.ForEach(jobs, j =>
            resp[j] = study.Response(baseConfig, j.e, j.a, distanceMm, photons,
                                     (baseConfig.Seed ?? 0) + (int)Math.Round(j.e * 10) * 97 + (int)Math.Round(j.a * 10)));

        var g = DoseStudy.FitG(fitKeV.Select(e => resp[(e, 0.0)]).ToList(), order);
        Console.WriteLine($"Dose rate from the spectrum: G(E) fit (order {order}) on frontal mono-energetic responses, S = {distanceMm / 1000:F0} m");
        Console.WriteLine("Truth = free-in-air fluence at the detector × ICRP 74 H*(10)/Φ. Ratio = estimate / truth.");
        Console.WriteLine();

        var rows = new List<DoseRatioRow>();
        Console.WriteLine("  energy (keV)   ratio      [fit set]");
        foreach (double e in fitKeV)
        {
            double r = DoseStudy.Estimate(g, resp[(e, 0.0)].CountsPerFluence) / resp[(e, 0.0)].DosePerFluence;
            rows.Add(new DoseRatioRow("fit", e, 0, r));
            Console.WriteLine($"  {e,8:F0}       {r,5:F3}");
        }
        Console.WriteLine("  energy (keV)   ratio      [check, not fitted]");
        foreach (double e in checkKeV)
        {
            double r = DoseStudy.Estimate(g, resp[(e, 0.0)].CountsPerFluence) / resp[(e, 0.0)].DosePerFluence;
            rows.Add(new DoseRatioRow("check", e, 0, r));
            Console.WriteLine($"  {e,8:F0}       {r,5:F3}");
        }
        Console.WriteLine();
        Console.WriteLine("  source     ratio   counts per µSv (frontal)");
        var cps = new Dictionary<string, double>();
        foreach (var iso in isotopes)
        {
            var lines = Isotopes.Get(iso).Lines.Select(l => (resp[(l.EnergyKeV, 0.0)], l.Intensity)).ToList();
            double r = DoseStudy.MixtureRatio(g, lines);
            double counts = lines.Sum(x => x.Item2 * x.Item1.TotalCountsPerFluence);
            double dose = lines.Sum(x => x.Item2 * x.Item1.DosePerFluence);
            cps[iso] = counts / dose * 1e6;                             // counts per pSv → per µSv
            rows.Add(new DoseRatioRow(iso, 0, 0, r));
            Console.WriteLine($"  {iso,-8}   {r,5:F3}   {cps[iso],10:N0}");
        }
        Console.WriteLine();
        Console.WriteLine("  angle →        " + string.Join("  ", angles.Select(a => $"{a,5:F1}°")));
        foreach (double e in angleKeV)
        {
            var line = new List<string>();
            foreach (double a in angles)
            {
                double r = DoseStudy.Estimate(g, resp[(e, a)].CountsPerFluence) / resp[(e, a)].DosePerFluence;
                rows.Add(new DoseRatioRow("angle", e, a, r));
                line.Add($"{r,6:F3}");
            }
            Console.WriteLine($"  {e,6:F0} keV     " + string.Join(" ", line));
        }

        double[] doses = [1, 10, 100, 1e3, 1e4, 1e5, 1e6];               // µSv/h: 1 µSv/h … 1 Sv/h
        var over = DoseStudy.OverRange(cps["Cs-137"], deadTimeUs, doses, calibrationRatio: rows.Single(r => r.Label == "Cs-137").Ratio);
        Console.WriteLine();
        Console.WriteLine($"Over-range, Cs-137 frontal, paralyzable τ = {deadTimeUs:F0} µs (theme 42); ratios = estimated / true dose:");
        Console.WriteLine("  dose rate      true rate    recorded    live     raw ratio   live-corrected");
        foreach (var o in over)
            Console.WriteLine($"  {FormatDose(o.TrueMicroSvPerH),-12}  {o.TrueRateCps,10:N0}  {o.RecordedRateCps,10:N0}  {o.LiveFraction,7:P1}  {o.RawRatio,8:F3}   {o.LiveCorrectedRatio,8:F3}");

        var sb = new StringBuilder("label,energy_kev,angle_deg,ratio\n");
        foreach (var r in rows) sb.Append(FormattableString.Invariant($"{r.Label},{r.EnergyKeV:F1},{r.AngleDeg:F1},{r.Ratio:F4}\n"));
        File.WriteAllText(prefix + "_ratio.csv", sb.ToString());
        var gs = new StringBuilder("energy_kev,g_psv_per_count,h_psv_cm2\n");
        for (double e = 40; e <= 1600; e += 20)
            gs.Append(FormattableString.Invariant($"{e:F0},{DoseStudy.G(g, e):E4},{AmbientDose.PerFluence(e):F4}\n"));
        File.WriteAllText(prefix + "_g.csv", gs.ToString());
        var os = new StringBuilder("usv_per_h,true_cps,recorded_cps,live_fraction,raw_ratio,live_corrected_ratio\n");
        foreach (var o in over)
            os.Append(FormattableString.Invariant($"{o.TrueMicroSvPerH:G4},{o.TrueRateCps:F0},{o.RecordedRateCps:F1},{o.LiveFraction:E4},{o.RawRatio:E4},{o.LiveCorrectedRatio:E4}\n"));
        File.WriteAllText(prefix + "_overrange.csv", os.ToString());
        Console.WriteLine();
        Console.WriteLine($"CSV written: {prefix}_ratio.csv, {prefix}_g.csv, {prefix}_overrange.csv");
        return 0;

        static string FormatDose(double uSvPerH) => uSvPerH >= 1e6 ? $"{uSvPerH / 1e6:G3} Sv/h" : uSvPerH >= 1e3 ? $"{uSvPerH / 1e3:G3} mSv/h" : $"{uSvPerH:G3} µSv/h";
    }
}
