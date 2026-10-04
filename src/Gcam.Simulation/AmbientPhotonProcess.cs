using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;

namespace Gcam.Simulation;

/// <summary>Incident Poisson photons, followed by physical transport and thinning, never retries at a fixed event time.
/// Supports isotropic fluence and a tabulated energy/zenith fluence; it is not the AB-4 soil generator.</summary>
public sealed class AmbientPhotonProcess
{
    private readonly AmbientFieldConfig _field;
    private readonly IRandom _time, _entry, _energy, _maskRandom;
    private readonly ComptonCrystalDetector _detector;
    private readonly IMask _mask;
    private readonly AmbientAngularSampler? _angular;
    private readonly double _width, _height, _depth, _maskFrontZ, _weight;
    private readonly int _pixelsX, _pixelsY;
    private (int X, int Y, double Energy)? _deposit;
    private double _incidentEnergy;
    private double _next;
    public double TimeS { get; private set; }
    public double FluencePerCm2PerSecond { get; }
    public double IncidentRateCps { get; }
    public long Histories { get; private set; }
    public long Detected { get; private set; }
    public double DetectedRateCps => Histories > 0 ? IncidentRateCps * Detected / Histories : 0;

    public AmbientPhotonProcess(SimulationConfig configuration)
    {
        var config = configuration.Clone();
        _field = config.Ambient ?? throw new ArgumentException("An ambient field is required.");
        if (!double.IsFinite(_field.DoseRateMicroSvPerHour) || _field.DoseRateMicroSvPerHour < 0
            || !Enum.IsDefined(_field.Geometry)) throw new ArgumentOutOfRangeException(nameof(configuration));
        if (_field.SpectrumFile is not null)
            throw new InvalidOperationException("The ambient spectrum file reference is unresolved; load the scenario through ConfigLoader.Load or call ConfigLoader.ResolveAmbientSpectrum.");
        var spectrum = _field.Spectrum ?? throw new ArgumentException("An incident spectrum is required.");
        if (spectrum.Lines is null || spectrum.Continuum is null || string.IsNullOrWhiteSpace(spectrum.Id) || spectrum.Version < 1)
            throw new ArgumentException("Spectrum requires an identifier, positive version, and line/bin arrays.");
        if (spectrum.AngularModel != "Isotropic" && spectrum.AngularModel != "EnergyZenithTable") throw new NotSupportedException("Unknown incident angular model.");
        if (spectrum.AngularModel == "Isotropic" && spectrum.EnergyZenith is not null) throw new ArgumentException("An isotropic spectrum cannot carry an unused angular table.");
        if (_field.RequireValidatedSpectrum && (!spectrum.IsValidated || spectrum.Id.Contains("NOT-VALIDATED", StringComparison.Ordinal)
            || spectrum.ContentHash != spectrum.ComputeContentHash() || !CarriesItsAcceptance(spectrum)))
            throw new InvalidOperationException("Ambient evidence requires a validated, hashed spectrum with its acceptance record; the development placeholder is not validated.");
        double dose = 0;
        foreach (var line in spectrum.Lines)
        {
            CheckWeight(line.FluenceWeight);
            dose += line.FluenceWeight * AmbientDose.PerFluence(line.EnergyKeV);
        }
        double previousHigh = 0;
        foreach (var bin in spectrum.Continuum.OrderBy(b => b.LowKeV))
        {
            CheckWeight(bin.FluenceWeight);
            if (bin.LowKeV < previousHigh) throw new ArgumentException("Continuum bins must not overlap.");
            dose += bin.FluenceWeight * AmbientDose.AveragePerFluence(bin.LowKeV, bin.HighKeV);
            previousHigh = bin.HighKeV;
        }
        _weight = spectrum.Lines.Sum(l => l.FluenceWeight) + spectrum.Continuum.Sum(b => b.FluenceWeight);
        if (!(_weight > 0) || !double.IsFinite(_weight) || !double.IsFinite(dose)) throw new ArgumentException("Spectrum requires finite positive total weight.");
        FluencePerCm2PerSecond = _field.DoseRateMicroSvPerHour * 1e6 / 3600 / (dose / _weight);
        var d = config.Detector;
        _pixelsX = d.PixelsX; _pixelsY = d.PixelsY;
        _width = d.PixelsX * d.PixelPitchMm; _height = d.PixelsY * d.PixelPitchMm;
        _depth = d.CrystalThicknessMm;
        _maskFrontZ = config.Geometry.MaskDetectorDistanceMm + config.Mask.ThicknessMm / 2;
        if (!(d.PixelsX > 0 && d.PixelsY > 0 && _width > 0 && _height > 0 && _depth > 0)
            || !double.IsFinite(_width + _height + _depth + _maskFrontZ) || _maskFrontZ < 0)
            throw new ArgumentException("Crystal dimensions must be finite and positive.");
        bool bare = _field.Geometry == AmbientGeometry.BareCrystalAllFaces;
        double areaMm2 = bare ? 2 * (_width * _height + _width * _depth + _height * _depth) : _width * _height;
        if (spectrum.AngularModel == "EnergyZenithTable") _angular = new(spectrum, _field.Geometry, _width, _height, _depth);
        IncidentRateCps = FluencePerCm2PerSecond * (_angular?.EffectiveAreaMm2 ?? areaMm2 / 4) / 100;
        if (!double.IsFinite(IncidentRateCps)) throw new ArgumentException("Incident rate is outside the finite numeric range.");
        int seed = config.Seed ?? 0;
        _time = new DefaultRandom(unchecked(seed + 29001)); _entry = new DefaultRandom(unchecked(seed + 29002));
        _energy = new DefaultRandom(unchecked(seed + 29003)); _maskRandom = new DefaultRandom(unchecked(seed + 29004));
        _mask = new DefaultSimulationFactory().CreateMask(config);
        // Bare bound is the homogeneous nominal crystal envelope, with no passive entrance or backing.
        _detector = new ComptonCrystalDetector(d.PixelsX, d.PixelsY, d.PixelPitchMm, 1, double.MaxValue,
            ComptonStrategy.Argmax, new DefaultRandom(unchecked(seed + 29005)),
            d.CrystalAttenuationPerMm > 0 ? d.CrystalAttenuationPerMm : null, _depth,
            material: CrystalMaterial.ForConfig(d.Material), entryThroughAllFaces: bare,
            // Enforce energy conservation in this new event projection: floating-point addition of cascade
            // deposits can otherwise exceed incident energy by a few ulps. Legacy detector records stay untouched.
            pixelEventSink: (x, y, e, _) => _deposit = (x, y, Math.Min(e, _incidentEnergy)));
        _next = Gap();
    }

    /// <summary>A validated spectrum names its decision and every compared ratio lies inside the accepted band — the
    /// record is self-consistent. (Whether the band was the right choice is the author's decision, not checked here.)</summary>
    private static bool CarriesItsAcceptance(IncidentSpectrum spectrum)
        => spectrum.Validation is { } v && !string.IsNullOrWhiteSpace(v.Decision) && v.Ratios.Length > 0
           && v.AgreementBandFraction > 0 && v.Ratios.All(r => double.IsFinite(r.Ratio) && Math.Abs(r.Ratio - 1) <= v.AgreementBandFraction);

    private static void CheckWeight(double weight)
    {
        if (!double.IsFinite(weight) || weight < 0) throw new ArgumentException("Fluence weights must be finite and nonnegative.");
    }

    private double Gap() => IncidentRateCps == 0 ? double.PositiveInfinity : -Math.Log(1 - _time.NextDouble()) / IncidentRateCps;

    /// <summary>At most one incident history. Null may mean a miss; TimeS proves the interval already processed.
    /// Call again until TimeS reaches the fixed horizon. Empty intervals advance without fabricated detections.</summary>
    public DetectedEvent? AdvanceUntil(double horizonS, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!double.IsFinite(horizonS) || horizonS < TimeS) throw new ArgumentOutOfRangeException(nameof(horizonS));
        if (_next > horizonS) { TimeS = horizonS; return null; }
        TimeS = _next; _next += Gap(); Histories++;
        var sampled = _angular?.Sample(_entry, _energy, _maskFrontZ);
        double energy = sampled?.EnergyKeV ?? SampleEnergy();
        _incidentEnergy = energy;
        var ray = sampled?.Ray ?? SampleRay();
        _deposit = null;
        if (_field.Geometry == AmbientGeometry.FrontOnlyThroughMask && !_mask.Transmit(ray, energy, _maskRandom)) return null;
        _detector.Score(new Photon { Ray = ray, EnergyKeV = energy, Weight = 1 });
        if (_deposit is not { } deposit) return null;
        Detected++;
        return new(deposit.X, deposit.Y, deposit.Energy, TimeS);
    }

    private double SampleEnergy()
    {
        double u = _energy.NextDouble() * _weight;
        foreach (var line in _field.Spectrum.Lines)
        {
            u -= line.FluenceWeight; if (u < 0) return line.EnergyKeV;
        }
        foreach (var bin in _field.Spectrum.Continuum)
        {
            u -= bin.FluenceWeight;
            if (u < 0) return bin.LowKeV + _energy.NextDouble() * (bin.HighKeV - bin.LowKeV);
        }
        throw new InvalidOperationException("Spectrum cumulative weight did not close.");
    }

    private Ray SampleRay()
    {
        double x = (_entry.NextDouble() - .5) * _width, y = (_entry.NextDouble() - .5) * _height;
        int face = 0;
        if (_field.Geometry == AmbientGeometry.BareCrystalAllFaces)
        {
            double[] areas = [_width * _height, _width * _height, _height * _depth, _height * _depth, _width * _depth, _width * _depth];
            double u = _entry.NextDouble() * areas.Sum();
            while (face < 5 && (u -= areas[face]) >= 0) face++;
        }
        double cosine = Math.Sqrt(_entry.NextDouble()), sine = Math.Sqrt(1 - cosine * cosine);
        double phi = 2 * Math.PI * _entry.NextDouble();
        double a = sine * Math.Cos(phi), b = sine * Math.Sin(phi);
        double z = -_entry.NextDouble() * _depth;
        var (position, direction) = face switch
        {
            0 => (new Vector3(x, y, 0), new Vector3(a, b, -cosine)),
            1 => (new Vector3(x, y, -_depth), new Vector3(a, b, cosine)),
            2 => (new Vector3(-_width / 2, y, z), new Vector3(cosine, a, b)),
            3 => (new Vector3(_width / 2, y, z), new Vector3(-cosine, a, b)),
            4 => (new Vector3(x, -_height / 2, z), new Vector3(a, cosine, b)),
            _ => (new Vector3(x, _height / 2, z), new Vector3(a, -cosine, b))
        };
        // Start outside the crystal; for the front bound start outside the whole mask slab.
        double back = _field.Geometry == AmbientGeometry.FrontOnlyThroughMask
            ? (_maskFrontZ + 1) / cosine : 1;
        return new(position - direction * back, direction);
    }

    /// <summary>A Poisson flood realization of this same transported event process, with an explicit duration.</summary>
    public DetectorImage AcquireFlood(double durationS, CancellationToken cancellationToken = default)
    {
        if (!(durationS >= 0) || !double.IsFinite(durationS)) throw new ArgumentOutOfRangeException(nameof(durationS));
        var image = new DetectorImage(_pixelsX, _pixelsY);
        double horizon = TimeS + durationS;
        while (TimeS < horizon)
            if (AdvanceUntil(horizon, cancellationToken) is { } e) image.Add(e.PixelX, e.PixelY, 1);
        return image;
    }
}
