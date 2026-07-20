using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Detector;

/// <summary>
/// Physical SiPM front-end: energy resolution from the photoelectron budget (the C# port of
/// rtl/frontend_model.py). For a deposit of energy E:
///   N_pe(E)   = lightYield · E · collection · PDE                         (detected photoelectrons)
///   R_stat(E) = 2.355 · √(ENF / N_pe(E))                                  (counting statistics, ∝ 1/√E)
///   R_dcr(E)  = 2.355 · √(ENF · DCR · τ_int) / N_pe(E)                    (SiPM dark counts, PARALLEL noise, ∝ 1/E)
///   R_tot(E)  = √(R_stat² + R_intrinsic² + R_dcr²)                        (+ crystal non-proportionality floor)
/// R_tot is a FWHM FRACTION. Dark counts accumulated during the integration window add a fixed p.e. variance,
/// so R_dcr scales 1/E — DCR degrades LOW-energy resolution most (weak/low-line sources), matching the design-
/// layer finding that "DCR matters only for weak sources; calibration handles it" (theme 11). <see cref="Measure"/>
/// smears a true deposit by R_tot so the detector's energy discrimination is realistic and energy-dependent.
/// </summary>
public sealed class FrontEndModel
{
    private readonly double _lyCollPde;      // lightYield · collection · PDE  (N_pe per keV)
    private readonly double _enf;
    private readonly double _intrinsic;      // FWHM fraction floor
    private readonly double _darkVarPe;      // ENF · DCR · τ_int  (dark p.e. variance, energy-independent)

    public FrontEndModel(FrontEndConfig cfg)
    {
        _lyCollPde = cfg.LightYieldPhPerKeV * cfg.CollectionEfficiency * cfg.SipmPde;
        _enf = cfg.ExcessNoiseFactor;
        _intrinsic = cfg.IntrinsicResolutionFwhm;
        _darkVarPe = cfg.ExcessNoiseFactor * cfg.DarkCountRateHz * (cfg.IntegrationTimeNs * 1e-9);
    }

    /// <summary>Detected photoelectrons for a deposit of <paramref name="energyKeV"/>.</summary>
    public double Photoelectrons(double energyKeV) => _lyCollPde * energyKeV;

    /// <summary>DCR (parallel-noise) resolution FWHM fraction at <paramref name="energyKeV"/> — scales 1/E.
    /// Assumes a single integration gate; an event-by-event equal-width baseline subtraction would double the
    /// dark variance (×√2 here). IntegrationTimeNs is the effective noise integration window.</summary>
    public double DcrFwhmFraction(double energyKeV)
    {
        double npe = Photoelectrons(energyKeV);
        return _darkVarPe > 0.0 && npe > 0.0 ? 2.3548 * Math.Sqrt(_darkVarPe) / npe : 0.0;
    }

    /// <summary>Total energy-resolution FWHM (as a fraction of E) at <paramref name="energyKeV"/>.</summary>
    public double FwhmFraction(double energyKeV)
    {
        if (energyKeV <= 0.0) return _intrinsic;
        double npe = Photoelectrons(energyKeV);
        double rStat = npe > 0.0 ? 2.3548 * Math.Sqrt(_enf / npe) : 0.0;   // FWHM fraction, ∝ 1/√E
        double rDcr = DcrFwhmFraction(energyKeV);                          // ∝ 1/E
        return Math.Sqrt(rStat * rStat + _intrinsic * _intrinsic + rDcr * rDcr);
    }

    /// <summary>Smear a TRUE deposited energy by the resolution: a Gaussian fluctuation of relative width
    /// FWHM(E)/2.355. Returns the measured energy (never negative).</summary>
    public double Measure(double trueEnergyKeV, IRandom rng)
    {
        if (trueEnergyKeV <= 0.0) return 0.0;
        double sigmaRel = FwhmFraction(trueEnergyKeV) / 2.3548;
        double measured = trueEnergyKeV * (1.0 + sigmaRel * Sampling.Gaussian(rng));
        return measured < 0.0 ? 0.0 : measured;
    }
}
