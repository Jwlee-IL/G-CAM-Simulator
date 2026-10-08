using Gcam.Configuration;
using Gcam.Core;

namespace Gcam.Detector;

/// <summary>
/// The physical readout of one detector (TODO-19): interaction sites → scintillation light at each site's own XYZ →
/// optical response table → SiPM photoelectrons (PDE, intrinsic fluctuation, Poisson, avalanche gain ENF, dark counts,
/// optional no-recovery saturation) → charge-division network (four outputs, or one channel per sensor) → analogue
/// channel amplitudes in ADC-code units. Digitisation (electronic noise + quantisation) and the trigger / hold happen in
/// <see cref="ReadoutPulseProcessor"/>; crystal identification in <see cref="FloodLut"/>.
/// <para>Statistics per event: one common light-yield factor g ~ 1 + σ_int·N(0,1) (σ_int = intrinsic FWHM / 2.3548);
/// signal photoelectrons n_k ~ Poisson(g·μ_k), μ_k = PDE · LY · Σ_sites E_s · P(k | crystal_s, depth_s) — independent
/// across sensors because a Poisson photon number thinned by the optical response is Poisson in each sensor; dark counts
/// d_k ~ Poisson(λ_d), baseline-subtracted by λ_d; charge Q_k = m_k + √((ENF − 1)·m_k)·N(0,1) with m_k = n_k + d_k (exact
/// first two moments of the gain sum). Hence Cov(Q)_kl = δ_kl·ENF·(μ_k + λ_d) + σ_int²·μ_k·μ_l, and the outputs carry
/// Wᵀ·Cov(Q)·W (the RD-6 covariance check). No light is invented or lost after the optical table: every
/// photoelectron's charge leaves through the outputs (Σ_c W[k,c] = 1 for a lossless network).</para>
/// </summary>
public sealed class ReadoutDevice
{
    private readonly double[] _mu, _q;
    private readonly double[] _xi, _eta;           // sensor centre / half span (IndependentSipm centroid)
    private readonly AdcPreset _adc;

    public ReadoutMode Mode { get; }
    public CrystalArrayGeometry Crystals { get; }
    public SensorLayout Sensors { get; }
    public OpticalResponse Optics { get; }
    public ChargeDivisionNetwork Network { get; }
    public int Channels => Network.Outputs;

    public double PhotonsPerKeV { get; }
    public double Pde { get; }
    public double Enf { get; }
    public double IntrinsicSigma { get; }

    /// <summary>Mean dark counts per sensor in the integration window.</summary>
    public double DarkMeanPe { get; }

    /// <summary>Microcells per sensor for the saturation limit (0 = infinite).</summary>
    public double CellsPerSensor { get; }

    /// <summary>Mean photoelectrons per keV of a full deposit (LY · PDE · mean optical collection).</summary>
    public double PePerKeVReference { get; }

    /// <summary>Reference gain: ADC codes per keV-equivalent of one channel (full-scale code / full-scale keV).</summary>
    public double CodesPerKeV { get; }

    /// <summary>ADC codes per photoelectron (so a reference full deposit of FullScaleKeV in one channel = full scale).</summary>
    public double CodesPerPe { get; }

    public int AdcMax => _adc.AdcMax;

    /// <summary>Electronic noise per channel, RMS in codes: analogue noise ⊕ the ADC's own ENOB noise.</summary>
    public double NoiseCodes { get; }

    /// <summary>Channels below this many noise σ are zeroed before position / energy (0 = off).</summary>
    public double ZeroSuppressionCodes { get; }

    public ReadoutDevice(DetectorConfig detector, ReadoutConfig readout, int opticsSeed)
        : this(CrystalArrayGeometry.From(detector), readout, opticsSeed)
    {
    }

    public ReadoutDevice(CrystalArrayGeometry crystals, ReadoutConfig readout, int opticsSeed)
        : this(BuildOptics(crystals, readout, opticsSeed), readout)
    {
    }

    /// <summary>A readout on an already traced optical table (several readouts of one geometry can share it; the sensor
    /// layout of <paramref name="readout"/> must be the table's).</summary>
    public ReadoutDevice(OpticalResponse optics, ReadoutConfig readout)
    {
        ArgumentNullException.ThrowIfNull(optics);
        ArgumentNullException.ThrowIfNull(readout);
        if (readout.Mode == ReadoutMode.DirectCrystal)
            throw new ArgumentException("DirectCrystal has no physical readout chain; use the direct event path.");
        if (SensorLayout.From(readout.Sensors, optics.Crystals) != optics.Sensors)
            throw new ArgumentException("The readout's sensor layout differs from the optical table's.");
        Mode = readout.Mode;
        Crystals = optics.Crystals;
        Sensors = optics.Sensors;
        Optics = optics;
        Network = Mode == ReadoutMode.FourOutputAnger
            ? ChargeDivisionNetwork.Build(readout.Network, Sensors.CountX, Sensors.CountY)
            : ChargeDivisionNetwork.Identity(Sensors.Count);

        var gagg = FrontEndParts.Scintillators[0];
        var mppc = FrontEndParts.Sensors[0];
        const double MppcAreaMm2 = 9.0;                                         // S13360-3050: 3 × 3 mm
        PhotonsPerKeV = readout.Scintillation.LightYieldPhPerKeV ?? gagg.LightYieldPhPerKeV;
        IntrinsicSigma = (readout.Scintillation.IntrinsicResolutionFwhm ?? gagg.NonPropFwhm) / 2.3548;
        Pde = readout.Sipm.Pde ?? mppc.Pde;
        Enf = readout.Sipm.ExcessNoiseFactor ?? mppc.Enf;
        double dcrPerMm2 = readout.Sipm.DarkCountRatePerMm2Hz ?? mppc.DcrHz / MppcAreaMm2;
        double integrationNs = readout.Sipm.IntegrationTimeNs ?? FrontEndParts.Default.Preamp.IntegrationNs;
        DarkMeanPe = dcrPerMm2 * Sensors.ActiveAreaMm2 * integrationNs * 1e-9;
        CellsPerSensor = readout.Sipm.MicrocellsPerMm2 * Sensors.ActiveAreaMm2;
        if (!(PhotonsPerKeV > 0) || !(IntrinsicSigma >= 0) || Pde is <= 0 or > 1 || !(Enf >= 1) || !(DarkMeanPe >= 0)
            || !(CellsPerSensor >= 0))
            throw new ArgumentException("Invalid scintillation / SiPM parameters.");

        var d = readout.Digitizer;
        if (d.Bits is < 2 or > 24 || !(d.FullScaleKeV > 0) || !(d.Enob > 0) || !(d.NoiseKeV >= 0))
            throw new ArgumentException("Invalid digitiser parameters.");
        _adc = Waveform.DeriveAdc("readout channel", d.Bits, d.FullScaleKeV, d.Enob);
        PePerKeVReference = PhotonsPerKeV * Pde * Optics.MeanCollection;
        if (!(PePerKeVReference > 0)) throw new ArgumentException("No light reaches the sensors in this geometry.");
        CodesPerKeV = _adc.AdcPerKev;
        CodesPerPe = CodesPerKeV / PePerKeVReference;
        NoiseCodes = Math.Sqrt(Math.Pow(d.NoiseKeV * CodesPerKeV, 2) + _adc.AdcNoiseCodes * _adc.AdcNoiseCodes);
        ZeroSuppressionCodes = d.ZeroSuppressionSigma * NoiseCodes;

        _mu = new double[Sensors.Count];
        _q = new double[Sensors.Count];
        _xi = new double[Sensors.Count];
        _eta = new double[Sensors.Count];
        for (int ky = 0; ky < Sensors.CountY; ky++)
            for (int kx = 0; kx < Sensors.CountX; kx++)
            {
                _xi[ky * Sensors.CountX + kx] = Sensors.CenterX(kx) / Sensors.HalfSpanX;
                _eta[ky * Sensors.CountX + kx] = Sensors.CenterY(ky) / Sensors.HalfSpanY;
            }
    }

    /// <summary>Trace the optical table of a geometry for <paramref name="readout"/>'s sensors and optics.</summary>
    public static OpticalResponse BuildOptics(CrystalArrayGeometry crystals, ReadoutConfig readout, int opticsSeed)
    {
        ArgumentNullException.ThrowIfNull(crystals);
        ArgumentNullException.ThrowIfNull(readout);
        if (readout.Mode == ReadoutMode.DirectCrystal)
            throw new ArgumentException("DirectCrystal has no physical readout chain; use the direct event path.");
        return new OpticalResponse(crystals, SensorLayout.From(readout.Sensors, crystals), readout.Optics, opticsSeed);
    }

    /// <summary>Expected signal photoelectrons per sensor (μ_k, no intrinsic factor, no dark counts).</summary>
    public void ExpectedPhotoelectrons(IReadOnlyList<InteractionSite> sites, double[] perSensor)
    {
        ArgumentNullException.ThrowIfNull(sites);
        Array.Clear(perSensor, 0, Sensors.Count);
        double scale = PhotonsPerKeV * Pde;
        for (int i = 0; i < sites.Count; i++)
        {
            var s = sites[i];
            int cx = Math.Clamp(s.CrystalX, 0, Crystals.CountX - 1), cy = Math.Clamp(s.CrystalY, 0, Crystals.CountY - 1);
            foreach (var share in Optics.Response(cy * Crystals.CountX + cx, Optics.DepthBin(s.ZMm)))
                perSensor[share.Sensor] += scale * s.EnergyKeV * share.Probability;
        }
    }

    /// <summary>Noiseless expected channel amplitudes (codes): W applied to μ. The light centroid of the event.</summary>
    public double Expected(IReadOnlyList<InteractionSite> sites, double[] channels)
    {
        ExpectedPhotoelectrons(sites, _mu);
        return Combine(_mu, channels);
    }

    /// <summary>One stochastic response: analogue channel amplitudes (codes, before electronic noise); returns their sum.</summary>
    public double Respond(IReadOnlyList<InteractionSite> sites, IRandom rng, double[] channels)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ExpectedPhotoelectrons(sites, _mu);
        double g = 1.0;
        if (IntrinsicSigma > 0) g = Math.Max(0.0, 1.0 + IntrinsicSigma * Sampling.Gaussian(rng));
        double enfVar = Enf - 1.0;
        for (int k = 0; k < _mu.Length; k++)
        {
            double n = _mu[k] > 0 ? Sampling.Poisson(rng, g * _mu[k]) : 0;
            if (DarkMeanPe > 0) n += Sampling.Poisson(rng, DarkMeanPe);
            if (CellsPerSensor > 0) n = CellsPerSensor * (1.0 - Math.Exp(-n / CellsPerSensor));
            double q = n;
            if (enfVar > 0 && n > 0) q += Math.Sqrt(enfVar * n) * Sampling.Gaussian(rng);
            _q[k] = q - DarkMeanPe;
        }
        return Combine(_q, channels);
    }

    private double Combine(double[] perSensor, double[] channels)
    {
        int outputs = Network.Outputs;
        Array.Clear(channels, 0, outputs);
        if (Mode == ReadoutMode.IndependentSipm)
            for (int k = 0; k < perSensor.Length; k++) channels[k] = perSensor[k] * CodesPerPe;
        else
            for (int k = 0; k < perSensor.Length; k++)
            {
                double q = perSensor[k];
                if (q == 0) continue;
                var row = Network.Row(k);
                for (int c = 0; c < outputs; c++) channels[c] += row[c] * q * CodesPerPe;
            }
        double sum = 0;
        for (int c = 0; c < outputs; c++) sum += channels[c];
        return sum;
    }

    /// <summary>Electronic noise and quantisation of one held analogue value: signed, pedestal-subtracted code.</summary>
    public int Digitize(double analogCodes, IRandom rng)
    {
        double v = analogCodes + (NoiseCodes > 0 ? NoiseCodes * Sampling.Gaussian(rng) : 0);
        v = Math.Clamp(v, -_adc.AdcMax, _adc.AdcMax);
        return (int)Math.Round(v);
    }

    /// <summary>Sum of the (zero-suppressed) channel codes.</summary>
    public double Sum(ReadOnlySpan<double> codes)
    {
        double s = 0;
        foreach (double c in codes) if (Math.Abs(c) >= ZeroSuppressionCodes) s += c;
        return s;
    }

    /// <summary>Raw position in [−1, 1]²: the four-corner Anger ratio X = (c1 + c3 − c0 − c2)/Σ, Y = (c2 + c3 − c0 − c1)/Σ
    /// (no correction), or for IndependentSipm the charge-weighted centroid of the sensor centres normalised by the
    /// sensor array's half span. False when the sum is not positive.</summary>
    public bool Position(ReadOnlySpan<double> codes, out double x, out double y)
    {
        x = y = double.NaN;
        double sum = Sum(codes);
        if (!(sum > 0)) return false;
        double zs = ZeroSuppressionCodes;
        if (Mode == ReadoutMode.FourOutputAnger)
        {
            double c0 = Kept(codes[0], zs), c1 = Kept(codes[1], zs), c2 = Kept(codes[2], zs), c3 = Kept(codes[3], zs);
            x = (c1 + c3 - c0 - c2) / sum;
            y = (c2 + c3 - c0 - c1) / sum;
            return true;
        }
        double sx = 0, sy = 0;
        for (int k = 0; k < codes.Length; k++)
        {
            double c = Kept(codes[k], zs);
            sx += c * _xi[k];
            sy += c * _eta[k];
        }
        x = sx / sum;
        y = sy / sum;
        return true;
    }

    private static double Kept(double code, double threshold) => Math.Abs(code) >= threshold ? code : 0;

    /// <summary>The direct engine assignment of a history: crystal with the largest summed deposit (first maximum in
    /// site order, as ComptonCrystalDetector aggregates), or −1 for no sites.</summary>
    public static int DirectCrystal(IReadOnlyList<InteractionSite> sites, int countX)
    {
        int best = -1;
        double bestE = -1;
        var seen = new List<(int Crystal, double E)>(sites.Count);
        foreach (var s in sites)
        {
            int c = s.CrystalY * countX + s.CrystalX;
            int i = seen.FindIndex(p => p.Crystal == c);
            if (i >= 0) seen[i] = (c, seen[i].E + s.EnergyKeV); else seen.Add((c, s.EnergyKeV));
        }
        foreach (var (c, e) in seen) if (e > bestE) { bestE = e; best = c; }
        return best;
    }
}
