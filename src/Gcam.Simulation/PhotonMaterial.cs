namespace Gcam.Simulation;

/// <summary>A homogeneous material's partial photon mass coefficients on an energy grid (from NIST XCOM element data
/// mixed by mass fraction in samples/ambient/build_materials.py). Each partial is interpolated log-log linearly
/// on its own (linear where an endpoint is zero, below the pair thresholds); interpolating the partials separately
/// keeps the interpolation difference to a log-log quadratic below ~0.4 % on this grid, whereas interpolating the
/// sum would reach ~4 % near the photoelectric/Compton crossover.</summary>
public sealed class PhotonMaterial
{
    private readonly double[] _energyKeV, _coherent, _incoherent, _photoelectric, _pair;

    public string Name { get; }
    public double DensityGPerCm3 { get; }
    public double MinEnergyKeV => _energyKeV[0];
    public double MaxEnergyKeV => _energyKeV[^1];

    public PhotonMaterial(string name, double densityGPerCm3, double[] energyKeV, double[] coherent, double[] incoherent,
        double[] photoelectric, double[] pairNuclear, double[] pairElectron)
    {
        ArgumentNullException.ThrowIfNull(energyKeV);
        if (!(densityGPerCm3 > 0) || !double.IsFinite(densityGPerCm3)) throw new ArgumentOutOfRangeException(nameof(densityGPerCm3));
        int n = energyKeV.Length;
        if (n < 2 || new[] { coherent, incoherent, photoelectric, pairNuclear, pairElectron }.Any(a => a is null || a.Length != n))
            throw new ArgumentException("Every partial coefficient needs one value per grid energy.");
        for (int i = 0; i < n; i++)
        {
            if (!(energyKeV[i] > 0) || !double.IsFinite(energyKeV[i]) || i > 0 && !(energyKeV[i] > energyKeV[i - 1]))
                throw new ArgumentException("The energy grid must be finite, positive and strictly increasing.");
            foreach (double v in new[] { coherent[i], incoherent[i], photoelectric[i], pairNuclear[i], pairElectron[i] })
                if (!(v >= 0) || !double.IsFinite(v)) throw new ArgumentException("Coefficients must be finite and nonnegative.");
            if (!(incoherent[i] + photoelectric[i] > 0)) throw new ArgumentException("A material must attenuate at every grid energy.");
        }
        Name = name;
        DensityGPerCm3 = densityGPerCm3;
        _energyKeV = (double[])energyKeV.Clone();
        _coherent = (double[])coherent.Clone(); _incoherent = (double[])incoherent.Clone();
        _photoelectric = (double[])photoelectric.Clone();
        _pair = pairNuclear.Zip(pairElectron, (a, b) => a + b).ToArray();
    }

    /// <summary>Mass coefficients at <paramref name="energyKeV"/>; outside the grid is a programming error.</summary>
    public PhotonCoefficients At(double energyKeV)
    {
        if (!(energyKeV >= _energyKeV[0] && energyKeV <= _energyKeV[^1]))
            throw new ArgumentOutOfRangeException(nameof(energyKeV), $"{Name} data cover {_energyKeV[0]}–{_energyKeV[^1]} keV.");
        int hi = Array.BinarySearch(_energyKeV, energyKeV);
        if (hi >= 0) return new(_coherent[hi], _incoherent[hi], _photoelectric[hi], _pair[hi]);
        hi = ~hi;
        int lo = hi - 1;
        double t = Math.Log(energyKeV / _energyKeV[lo]) / Math.Log(_energyKeV[hi] / _energyKeV[lo]);
        return new(LogLog(_coherent, lo, hi, t), LogLog(_incoherent, lo, hi, t), LogLog(_photoelectric, lo, hi, t),
            LogLog(_pair, lo, hi, t, energyKeV));
    }

    /// <summary>Linear attenuation coefficient used by the transport (coherent excluded), 1/cm.</summary>
    public double LinearMuPerCm(double energyKeV) => DensityGPerCm3 * At(energyKeV).WithoutCoherent;

    private double LogLog(double[] y, int lo, int hi, double t, double energyKeV = double.NaN)
    {
        if (y[lo] > 0 && y[hi] > 0) return Math.Exp(Math.Log(y[lo]) + t * Math.Log(y[hi] / y[lo]));
        // Below a pair threshold the tabulated value is zero; interpolate linearly in energy there.
        double x = double.IsNaN(energyKeV) ? Math.Exp(Math.Log(_energyKeV[lo]) + t * Math.Log(_energyKeV[hi] / _energyKeV[lo])) : energyKeV;
        return y[lo] + (y[hi] - y[lo]) * (x - _energyKeV[lo]) / (_energyKeV[hi] - _energyKeV[lo]);
    }
}
