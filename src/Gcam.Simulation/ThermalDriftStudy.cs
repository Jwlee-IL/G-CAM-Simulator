using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>One acquisition time slice: temperature state and its measurable consequences.</summary>
public sealed record ThermalDriftRow(
    double Time,
    double AmbientDeltaC,   // ambient temperature deviation from calibration (uniform)
    double CenterDeltaC,    // total temperature deviation at the array centre (ambient + self-heating)
    double Efficiency,      // photopeak counts relative to t = 0 (global droop)
    double ResidualCoV,     // flood-corrected sensitivity spread (0 = perfectly flat after correction)
    double RmsBiasMm);      // localization error after flood correction

/// <summary>
/// Studies how the SiPM array's temperature drift DURING an acquisition degrades a per-crystal photopeak-window
/// system, and why flood correction alone cannot save it. A calibration flood map <c>S_cal</c> is snapshot at
/// t = 0; the acquisition-time sensitivity walks as the photopeak centroid drifts (<see cref="ThermalDrift"/>),
/// so dividing by the fixed <c>S_cal</c> leaves a residual that grows with time — unless a bias-compensation
/// loop keeps the gain stable. Ambient drift shows up as a uniform efficiency droop; self-heating's spatial
/// gradient shows up as a residual non-uniformity and a localization bias.
///
/// The MC geometry flood is run ONCE (uniform detector); every time slice reuses it and applies the analytic
/// per-crystal sensitivity S_i(t) = gain_i · acceptance(fwhm_i, window, ε_i(t)) — same "flood once, apply
/// per-pixel" approach as <see cref="UniformityStudy"/>.
/// </summary>
public sealed class ThermalDriftStudy
{
    private readonly ISimulationFactory _factory;

    public ThermalDriftStudy(ISimulationFactory factory)
    {
        _factory = factory;
    }

    public ThermalDriftRow[] Run(SimulationConfig baseConfig, ThermalDrift drift, double[] times,
                                 double photonBudget, int repeats)
    {
        if (drift.Width != baseConfig.Detector.PixelsX || drift.Height != baseConfig.Detector.PixelsY)
            throw new ArgumentException(
                $"ThermalDrift is {drift.Width}×{drift.Height} but the detector is " +
                $"{baseConfig.Detector.PixelsX}×{baseConfig.Detector.PixelsY}.", nameof(drift));

        // Per-crystal static non-uniformity (gain_i, fwhm_i) and the calibration map S_cal.
        var uni = new CrystalUniformity(baseConfig.Detector);
        double[] sCal = uni.Sensitivity;

        // Geometry flood: run the MC once with a UNIFORM detector so the per-crystal sensitivity is applied
        // analytically afterwards (identical geometry across every time slice — the temperature only moves S_i).
        var geoCfg = baseConfig.Clone();
        geoCfg.Detector.GainSigma = 0.0;
        geoCfg.Detector.GainGradient = 0.0;
        geoCfg.Detector.EnergyResolutionFwhm = 0.0;
        geoCfg.Detector.EnergyResolutionFwhmSigma = 0.0;
        geoCfg.Detector.EnergyWindowFraction = 0.0;
        var mean = new SimulationRunner(_factory).Run(geoCfg);

        var img = mean.DetectorImage;
        int w = img.Width, h = img.Height;
        double detW = mean.DetectedWeight;
        double eff0 = mean.PhotonsEmitted > 0 ? detW / mean.PhotonsEmitted : 0.0;
        double nDet = photonBudget * eff0;

        // G_i weight (geometry illumination) for the efficiency / residual metrics.
        double[] g = new double[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                g[y * w + x] = img[x, y];

        // Reference (t = 0) illuminated-photopeak counts, Σ G_i · S_cal_i.
        double ref0 = 0.0;
        for (int i = 0; i < g.Length; i++) ref0 += g[i] * sCal[i];

        var rows = new List<ThermalDriftRow>();
        foreach (double t in times)
        {
            double[] shift = drift.CentroidShiftMap(t);
            double[] sNow = uni.SensitivityWithShift(shift);

            // Efficiency droop: illuminated photopeak counts relative to t = 0.
            double now = 0.0;
            for (int i = 0; i < g.Length; i++) now += g[i] * sNow[i];
            double efficiency = ref0 > 0 ? now / ref0 : 0.0;

            // Flood-corrected residual: spread of S_i(t)/S_cal_i, weighted by illumination.
            double wsum = 0.0, wmean = 0.0;
            for (int i = 0; i < g.Length; i++)
            {
                if (sCal[i] <= 1e-9 || g[i] <= 0.0) continue;
                double r = sNow[i] / sCal[i];
                wmean += g[i] * r; wsum += g[i];
            }
            wmean = wsum > 0 ? wmean / wsum : 1.0;
            double wvar = 0.0;
            for (int i = 0; i < g.Length; i++)
            {
                if (sCal[i] <= 1e-9 || g[i] <= 0.0) continue;
                double r = sNow[i] / sCal[i] - wmean;
                wvar += g[i] * r * r;
            }
            wvar = wsum > 0 ? wvar / wsum : 0.0;
            double residualCoV = wmean != 0 ? Math.Sqrt(wvar) / Math.Abs(wmean) : 0.0;

            // Localization after flood correction: acquisition flood F_i = G_i · S_i(t), corrected by ÷ S_cal_i.
            // Counts are scaled by ref0 (the t=0 photopeak total), NOT the current total, so as the peak walks out
            // of the window the acquisition keeps FEWER counts (efficiency droop) → more Poisson noise. The RMS
            // therefore compounds both channels: the spatial flood residual AND the count loss.
            double rms = Localize(baseConfig, img, g, sNow, sCal, nDet, ref0, detW, repeats);

            rows.Add(new ThermalDriftRow(t, drift.AmbientDelta(t), drift.DeltaT((int)(w / 2), (int)(h / 2), t),
                                         efficiency, residualCoV, rms));
        }
        return rows.ToArray();
    }

    private double Localize(SimulationConfig cfg, DetectorImage geo, double[] g, double[] sNow, double[] sCal,
                            double nDet, double ref0, double detW, int repeats)
    {
        if (detW <= 0 || ref0 <= 0) return double.NaN;
        int w = geo.Width, h = geo.Height;
        // Fix the counts scale from the t=0 photopeak total (ref0), so a drooped-efficiency slice keeps
        // proportionally FEWER counts (Σ g·sNow·scale = nDet·efficiency(t)) → higher Poisson noise.
        double scale = nDet / ref0;

        var decoder = _factory.CreateDecoder(cfg)!;
        var rng = _factory.CreateRandom(cfg);
        var work = new DetectorImage(w, h);
        double tx = cfg.Source.Position[0];
        double ty = cfg.Source.Position[1];

        double sumSq = 0.0;
        for (int rep = 0; rep < repeats; rep++)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    double mean = g[i] * sNow[i] * scale;
                    double v = Sampling.Poisson(rng, mean);
                    double s = sCal[i];
                    v = s > 1e-6 ? v / s : v;   // flood correction with the CALIBRATION map
                    work[x, y] = v;
                }

            var est = decoder.Decode(work).Estimate;
            double err = Math.Sqrt((est.Position.X - tx) * (est.Position.X - tx) +
                                   (est.Position.Y - ty) * (est.Position.Y - ty));
            sumSq += err * err;
        }
        return Math.Sqrt(sumSq / repeats);
    }

    public static string ToCsv(ThermalDriftRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("time,ambient_dC,center_dC,efficiency,residual_cov,rms_bias_mm");
        foreach (var r in rows)
            sb.AppendLine($"{r.Time:F3},{r.AmbientDeltaC:F3},{r.CenterDeltaC:F3},{r.Efficiency:F4},{r.ResidualCoV:F4},{r.RmsBiasMm:F3}");
        return sb.ToString();
    }
}
