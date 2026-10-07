namespace Gcam.Decoding;

/// <summary>An immutable normalized background shape and independently calibrated count rate (not ambient dose).
/// The caller owns the physical validity of the metadata identifiers; mismatches are refused rather than inferred.</summary>
public sealed class BackgroundCalibration
{
    private readonly double[] _shape;
    public BackgroundCalibrationMetadata Metadata { get; }
    public IReadOnlyList<double> Shape { get; }
    public double RateCps { get; }
    public double RateStandardUncertaintyCps { get; }
    public double? CalibrationLiveTimeS { get; }
    public double? CalibrationCounts { get; }

    public BackgroundCalibration(IEnumerable<double> map, BackgroundCalibrationMetadata metadata, double rateCps,
        double rateStandardUncertaintyCps = 0, double? calibrationLiveTimeS = null, double? calibrationCounts = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(metadata);
        _shape = map.ToArray();
        if (metadata.PixelsX < 1 || metadata.PixelsY < 1 || !(metadata.PixelPitchMm > 0) || !double.IsFinite(metadata.PixelPitchMm)
            || new[] { metadata.HeadResponseId, metadata.WindowId, metadata.Bound, metadata.FieldDistributionId }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Calibration needs a finite geometry and explicit head/window/bound/field identity.");
        if (_shape.Length != checked(metadata.PixelsX * metadata.PixelsY) || _shape.Any(v => !double.IsFinite(v) || v < 0))
            throw new ArgumentException("Calibration needs one finite non-negative value per detector pixel.");
        double sum = _shape.Sum();
        if (!(sum > 0) || !double.IsFinite(sum)) throw new ArgumentException("Calibration shape must have a positive finite total.");
        if (!(rateCps >= 0) || !double.IsFinite(rateCps) || !(rateStandardUncertaintyCps >= 0) || !double.IsFinite(rateStandardUncertaintyCps))
            throw new ArgumentOutOfRangeException(nameof(rateCps));
        if (calibrationLiveTimeS is { } t && (!(t > 0) || !double.IsFinite(t))) throw new ArgumentOutOfRangeException(nameof(calibrationLiveTimeS));
        if (calibrationCounts is { } n && (!(n > 0) || !double.IsFinite(n))) throw new ArgumentOutOfRangeException(nameof(calibrationCounts));
        for (int i = 0; i < _shape.Length; i++) _shape[i] /= sum;
        Metadata = metadata; Shape = Array.AsReadOnly(_shape); RateCps = rateCps;
        RateStandardUncertaintyCps = rateStandardUncertaintyCps; CalibrationLiveTimeS = calibrationLiveTimeS; CalibrationCounts = calibrationCounts;
    }

    public void RequireMatch(BackgroundCalibrationMetadata acquisition)
    {
        if (Metadata != acquisition) throw new ArgumentException("Background calibration metadata does not match the acquisition.");
    }

    public double ExpectedCounts(double liveTimeS)
    {
        if (!(liveTimeS >= 0) || !double.IsFinite(liveTimeS)) throw new ArgumentOutOfRangeException(nameof(liveTimeS));
        double counts = liveTimeS * RateCps;
        if (!double.IsFinite(counts)) throw new ArgumentOutOfRangeException(nameof(liveTimeS));
        return counts;
    }
}
