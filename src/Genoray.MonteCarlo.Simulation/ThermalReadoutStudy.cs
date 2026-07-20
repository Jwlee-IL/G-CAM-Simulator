using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Detector;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One temperature deviation: the DCR-driven readout state it produces.</summary>
public sealed record ThermalReadoutRow(
    double DeltaTC,
    double DcrHz,               // SiPM dark-count rate at this temperature (DCR₀·2^(ΔT/doubling))
    double DarkTriggerKcps,     // per-pixel dark trigger load (kcps)
    double ResLowEPct,          // energy-resolution FWHM at a LOW line (%) — the DCR (1/E) term hits it hardest
    double ResPhotopeakPct,     // …at the 662 keV photopeak — nearly immune to DCR
    double PdeFactor);          // PDE relative to calibration

/// <summary>
/// Extends the thermal model (<see cref="ThermalDrift"/>, theme 36) beyond the photopeak-centroid gain drift to the
/// SiPM DARK-COUNT and PDE consequences the centroid-only model missed. Silicon dark generation ≈ DOUBLES every
/// 8–10 °C, and — unlike the gain — a bias-compensation loop does NOT null it, so DCR keeps climbing with temperature.
/// The dark charge accumulated in the integration window adds a PARALLEL-noise variance to the measured energy that
/// scales 1/E (via <see cref="FrontEndModel"/>), so it degrades LOW-energy resolution sharply while the 662 keV
/// photopeak is nearly immune; it also raises the per-pixel dark TRIGGER rate (a count-rate / dead-time load). The
/// study sweeps the temperature deviation from calibration and reports both. Self-contained.
/// </summary>
public sealed class ThermalReadoutStudy
{
    public ThermalReadoutRow[] Run(FrontEndConfig baseFrontEnd, ThermalDrift thermal, double[] deltaTsC,
                                   double lowLineKeV = 60.0, double photopeakKeV = 661.7)
    {
        double dcr0 = baseFrontEnd.DarkCountRateHz > 0 ? baseFrontEnd.DarkCountRateHz : 1.0e6;
        double pde0 = baseFrontEnd.SipmPde;
        var rows = new List<ThermalReadoutRow>();
        foreach (double dt in deltaTsC)
        {
            double dcr = dcr0 * thermal.DcrFactor(dt);
            double pdeF = thermal.PdeFactor(dt);
            // Front-end at this temperature: scaled DCR and PDE (the light-yield/ENF chain is otherwise unchanged).
            var cfg = new FrontEndConfig
            {
                LightYieldPhPerKeV = baseFrontEnd.LightYieldPhPerKeV,
                CollectionEfficiency = baseFrontEnd.CollectionEfficiency,
                SipmPde = pde0 * pdeF,
                ExcessNoiseFactor = baseFrontEnd.ExcessNoiseFactor,
                IntrinsicResolutionFwhm = baseFrontEnd.IntrinsicResolutionFwhm,
                DarkCountRateHz = dcr,
                IntegrationTimeNs = baseFrontEnd.IntegrationTimeNs,
            };
            var fe = new FrontEndModel(cfg);
            rows.Add(new ThermalReadoutRow(
                dt, dcr, dcr / 1000.0,
                100.0 * fe.FwhmFraction(lowLineKeV),
                100.0 * fe.FwhmFraction(photopeakKeV),
                pdeF));
        }
        return rows.ToArray();
    }

    public static string ToCsv(ThermalReadoutRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("delta_t_C,dcr_hz,dark_trigger_kcps,res_lowE_pct,res_photopeak_pct,pde_factor");
        foreach (var r in rows)
            sb.AppendLine($"{r.DeltaTC:F1},{r.DcrHz:F0},{r.DarkTriggerKcps:F1},{r.ResLowEPct:F3},{r.ResPhotopeakPct:F3},{r.PdeFactor:F4}");
        return sb.ToString();
    }
}
