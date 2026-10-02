using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>Ambient dose equivalent per unit photon fluence, H*(10)/Φ, from ICRP Publication 74 (1996) Table A.21
/// (ICRU 47 with Hubbell &amp; Seltzer 1995 air-kerma data). Log–log interpolation between the tabulated energies;
/// valid 10 keV – 10 MeV.</summary>
public static class AmbientDose
{
    private static readonly double[] _keV = { 10, 15, 20, 30, 40, 50, 60, 80, 100, 150, 200, 300, 400, 500, 600, 800,
                                              1000, 1500, 2000, 3000, 4000, 5000, 6000, 8000, 10000 };
    private static readonly double[] _pSvCm2 = { 0.061, 0.83, 1.05, 0.81, 0.64, 0.55, 0.51, 0.53, 0.61, 0.89, 1.20, 1.80,
                                                 2.38, 2.93, 3.44, 4.38, 5.20, 6.90, 8.60, 11.1, 13.4, 15.5, 17.6, 21.6, 25.6 };

    /// <summary>H*(10)/Φ in pSv·cm² at <paramref name="energyKeV"/>.</summary>
    public static double PerFluence(double energyKeV)
    {
        if (!(energyKeV >= _keV[0] && energyKeV <= _keV[^1]))
            throw new ArgumentOutOfRangeException(nameof(energyKeV), "ICRP 74 table covers 10 keV – 10 MeV");
        int i = 1; while (energyKeV > _keV[i]) i++;
        double t = Math.Log(energyKeV / _keV[i - 1]) / Math.Log(_keV[i] / _keV[i - 1]);
        return Math.Exp(Math.Log(_pSvCm2[i - 1]) + t * Math.Log(_pSvCm2[i] / _pSvCm2[i - 1]));
    }

    /// <summary>Exact integral of the table's piecewise power laws for a uniform-energy continuum bin.</summary>
    public static double AveragePerFluence(double lowKeV, double highKeV)
    {
        _ = PerFluence(lowKeV); _ = PerFluence(highKeV);
        if (!(highKeV > lowKeV)) throw new ArgumentOutOfRangeException(nameof(highKeV));
        double integral = 0, a = lowKeV;
        foreach (double b in _keV.Where(e => e > lowKeV && e < highKeV).Append(highKeV))
        {
            double ha = PerFluence(a), hb = PerFluence(b);
            double power = Math.Log(hb / ha) / Math.Log(b / a);
            integral += Math.Abs(power + 1) < 1e-12 ? ha * a * Math.Log(b / a)
                : ha * a * (Math.Pow(b / a, power + 1) - 1) / (power + 1);
            a = b;
        }
        return integral / (highKeV - lowKeV);
    }
}

/// <summary>The head's pulse-height response to one mono-energetic source: counts per energy bin per unit photon
/// fluence at the detector centre (cm²), plus the fluence-to-dose truth for that energy.</summary>
public sealed record DoseResponse(double EnergyKeV, double AngleDeg, double[] CountsPerFluence, double DosePerFluence)
{
    public double TotalCountsPerFluence => CountsPerFluence.Sum();
}

/// <summary>One row of the estimate-vs-truth comparison.</summary>
public sealed record DoseRatioRow(string Label, double EnergyKeV, double AngleDeg, double Ratio);

/// <summary>One dose rate of the over-range sweep (paralyzable front end).</summary>
public sealed record OverRangeRow(double TrueMicroSvPerH, double TrueRateCps, double RecordedRateCps,
                                  double LiveFraction, double RawRatio, double LiveCorrectedRatio);

/// <summary>
/// Dose rate from the imaging head (TODO-05, PR-SAFE-01 / PR-SENS-05, D-21). A scintillator survey meter does not
/// measure dose: it weights its pulse-height spectrum with a function G(E) so that Σ G(Eᵢ)·Nᵢ / t tracks the ambient
/// dose equivalent rate. The simulator knows the truth: the unscattered fluence at the detector position times the
/// ICRP 74 coefficient (H*(10) is defined in the field WITHOUT the instrument, so the truth is free-in-air).
///
/// <para>Response: a biased MC of a point source at distance S through the mask and the Compton crystal; every
/// scored event's TOTAL deposit (all pixels — the dose channel sums the array) is smeared by the configured energy
/// resolution and histogrammed with its importance weight. Counts per emitted photon × 4π r² = counts per unit
/// fluence (r = source–detector distance in cm).</para>
///
/// <para>G(E) = Σₖ aₖ·(ln(E/662 keV))ᵏ is fitted by least squares on the RELATIVE error over a set of frontal
/// mono-energetic responses, then checked on other energies, the reference sources' line mixtures and oblique
/// incidence without refitting.</para>
/// </summary>
public sealed class DoseStudy
{
    public const double BinKeV = 5.0;
    public const double MaxKeV = 2000.0;
    public static int Bins => (int)(MaxKeV / BinKeV);

    private readonly double _lldKeV;
    public DoseStudy(double lldKeV = 30.0) => _lldKeV = lldKeV;

    /// <summary>Pulse-height response to a mono-energetic point source at <paramref name="distanceMm"/> from the mask,
    /// <paramref name="angleDeg"/> off axis (along x).</summary>
    public DoseResponse Response(SimulationConfig baseConfig, double energyKeV, double angleDeg, double distanceMm,
                                 long photons, int seed)
    {
        var cfg = baseConfig.Clone();
        cfg.Sources = null;
        cfg.Background = null;
        cfg.PhotonCount = photons;
        cfg.Seed = seed;
        cfg.Geometry.SourceMaskDistanceMm = distanceMm;
        double planeZ = cfg.Geometry.MaskDetectorDistanceMm + distanceMm;
        double x = planeZ * Math.Tan(angleDeg * Math.PI / 180.0);
        cfg.Source.Position = [x, 0.0, 0.0];
        cfg.Source.EnergyKeV = energyKeV;
        cfg.Source.Lines = null;
        cfg.Source.DirectionalBiasing = true;

        var factory = new DefaultSimulationFactory();
        var rng = factory.CreateRandom(cfg);
        var source = factory.CreateSource(cfg);
        var mask = factory.CreateMask(cfg);
        var d = cfg.Detector;
        var smearRng = new DefaultRandom(seed + 31);
        // Resolution: the physical front-end model when the config has one, else the 1/√E FWHM anchored at 662 keV.
        var frontEnd = d.FrontEnd is null ? null : new FrontEndModel(d.FrontEnd);
        var hist = new double[Bins];
        void Sink(double dep, double w)
        {
            double e = frontEnd is null ? Smear(dep, d.EnergyResolutionFwhm, smearRng) : frontEnd.Measure(dep, smearRng);
            if (e < _lldKeV || e >= MaxKeV) return;
            hist[(int)(e / BinKeV)] += w;
        }
        var detector = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm,
            windowCenterKeV: 661.7, windowFraction: 1.0, ComptonStrategy.Argmax, new DefaultRandom(seed + 777),
            muAt662PerMm: d.CrystalAttenuationPerMm > 0.0 ? d.CrystalAttenuationPerMm : null, d.CrystalThicknessMm,
            planeZ: 0.0, eventSink: Sink,
            entranceAbsorber: d.EntranceAbsorberMm > 0.0 ? new EntranceAbsorber(d.EntranceAbsorberMm) : null,
            backingScatterer: d.BackingScatterMm > 0.0 ? new EntranceAbsorber(d.BackingScatterMm) : null,
            reflectorGapMm: d.ReflectorGapMm, opticalCrosstalk: d.OpticalCrosstalkFraction,
            material: CrystalMaterial.ForConfig(d.Material));

        long emitted = 0;
        foreach (var photon in source.Emit(rng, photons))
        {
            emitted++;
            if (!mask.Transmit(photon.Ray, photon.EnergyKeV, rng)) continue;
            detector.Score(photon);
        }

        double rCm = Math.Sqrt(x * x + planeZ * planeZ) / 10.0;
        double perFluence = 4.0 * Math.PI * rCm * rCm / Math.Max(1, emitted);
        for (int b = 0; b < hist.Length; b++) hist[b] *= perFluence;
        return new DoseResponse(energyKeV, angleDeg, hist, AmbientDose.PerFluence(energyKeV));
    }

    // Gaussian resolution with FWHM ∝ 1/√E, anchored at 662 keV (the config's EnergyResolutionFwhm).
    private static double Smear(double e, double fwhmAt662, IRandom rng)
    {
        if (!(fwhmAt662 > 0.0) || e <= 0.0) return e;
        double sigma = fwhmAt662 * Math.Sqrt(661.7 * e) / 2.3548;
        return e + sigma * Sampling.Gaussian(rng);
    }

    /// <summary>Least-squares fit of G(E) = Σₖ aₖ (ln(E/662))ᵏ, k = 0…<paramref name="order"/>, minimising the
    /// relative error of the dose estimate over <paramref name="fit"/>. Returns the coefficients.</summary>
    public static double[] FitG(IReadOnlyList<DoseResponse> fit, int order)
    {
        int k = order + 1;
        var ata = new double[k, k];
        var atb = new double[k];
        foreach (var r in fit)
        {
            var row = new double[k];
            for (int b = 0; b < r.CountsPerFluence.Length; b++)
            {
                double c = r.CountsPerFluence[b];
                if (c == 0.0) continue;
                double l = Math.Log(BinCentre(b) / 661.7), p = 1.0;
                for (int j = 0; j < k; j++) { row[j] += c * p / r.DosePerFluence; p *= l; }
            }
            for (int i = 0; i < k; i++)
            {
                atb[i] += row[i];
                for (int j = 0; j < k; j++) ata[i, j] += row[i] * row[j];
            }
        }
        return Solve(ata, atb);
    }

    public static double G(double[] a, double energyKeV)
    {
        double l = Math.Log(energyKeV / 661.7), p = 1.0, g = 0.0;
        foreach (double c in a) { g += c * p; p *= l; }
        return g;
    }

    /// <summary>Estimated H*(10) per unit fluence from a response: Σ G(E_b)·N_b (pSv·cm²).</summary>
    public static double Estimate(double[] a, double[] countsPerFluence)
    {
        double s = 0.0;
        for (int b = 0; b < countsPerFluence.Length; b++)
            if (countsPerFluence[b] != 0.0) s += G(a, BinCentre(b)) * countsPerFluence[b];
        return s;
    }

    /// <summary>Estimate / truth for a line mixture: Σ yᵢ·responseᵢ against Σ yᵢ·h(Eᵢ).</summary>
    public static double MixtureRatio(double[] a, IReadOnlyList<(DoseResponse r, double yield)> lines)
    {
        double est = 0.0, truth = 0.0;
        foreach (var (r, y) in lines) { est += y * Estimate(a, r.CountsPerFluence); truth += y * r.DosePerFluence; }
        return est / truth;
    }

    /// <summary>Over-range of a paralyzable front end with dead time <paramref name="tauUs"/>: the true count rate
    /// scales with dose rate (counts per µSv from <paramref name="countsPerMicroSv"/>), the recorded rate is n·e^(−nτ)
    /// (theme 42), the live fraction is e^(−nτ). RawRatio = recorded / true (what an uncorrected meter reads relative
    /// to the truth); LiveCorrectedRatio divides by the live fraction measured to <paramref name="liveResolution"/>
    /// (a live-time clock cannot resolve a live fraction below it, so the correction saturates there). Both ratios
    /// include the spectrum-weighting error at low rate, <paramref name="calibrationRatio"/> (estimate / truth).</summary>
    public static OverRangeRow[] OverRange(double countsPerMicroSv, double tauUs, double[] microSvPerH,
                                           double calibrationRatio = 1.0, double liveResolution = 1e-3)
    {
        if (microSvPerH.Any(h => !(h > 0.0))) throw new ArgumentException("dose rates must be positive", nameof(microSvPerH));
        double tau = tauUs * 1e-6;
        return microSvPerH.Select(h =>
        {
            double n = countsPerMicroSv * h / 3600.0;
            double live = Math.Exp(-n * tau);
            double m = n * live;
            double corrected = m / Math.Max(live, liveResolution);
            return new OverRangeRow(h, n, m, live, calibrationRatio * m / n, calibrationRatio * corrected / n);
        }).ToArray();
    }

    public static double BinCentre(int b) => (b + 0.5) * BinKeV;

    private static double[] Solve(double[,] a, double[] b)
    {
        int n = b.Length;
        var m = (double[,])a.Clone();
        var x = (double[])b.Clone();
        for (int c = 0; c < n; c++)
        {
            int p = c;
            for (int r = c + 1; r < n; r++) if (Math.Abs(m[r, c]) > Math.Abs(m[p, c])) p = r;
            if (Math.Abs(m[p, c]) < 1e-300) throw new InvalidOperationException("G(E) fit is singular");
            if (p != c)
            {
                for (int j = 0; j < n; j++) (m[c, j], m[p, j]) = (m[p, j], m[c, j]);
                (x[c], x[p]) = (x[p], x[c]);
            }
            for (int r = c + 1; r < n; r++)
            {
                double f = m[r, c] / m[c, c];
                for (int j = c; j < n; j++) m[r, j] -= f * m[c, j];
                x[r] -= f * x[c];
            }
        }
        for (int r = n - 1; r >= 0; r--)
        {
            double s = x[r];
            for (int j = r + 1; j < n; j++) s -= m[r, j] * x[j];
            x[r] = s / m[r, r];
        }
        return x;
    }
}
