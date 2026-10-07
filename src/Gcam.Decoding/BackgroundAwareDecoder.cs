using Gcam.Core;

namespace Gcam.Decoding;

/// <summary>Explicit calibrated decoding of raw images. Holds no simulation or ambient generator reference.
/// E6 subtracts signed values without clipping; E5 adds a fixed mean to the Poisson forward model.</summary>
public sealed class BackgroundAwareDecoder : IDecoder
{
    private readonly MlemDecoder _mlem;
    private readonly IDecoder _correlation;
    private readonly CodedApertureGeometry _geometry;
    private readonly BackgroundCalibration _calibration;
    private readonly BackgroundEstimator _estimator;
    private readonly int _iterations;
    private readonly SubCellMethod _subCell;
    private readonly double _expectedBackground;
    private readonly double _calibrationExpected;
    private readonly double _calibrationUncertainty;
    public JointBackgroundMlem JointModel { get; }

    public BackgroundAwareDecoder(MlemDecoder mlem, IDecoder correlation, CodedApertureGeometry geometry,
        BackgroundCalibration calibration, BackgroundCalibrationMetadata acquisitionMetadata,
        BackgroundEstimator estimator, int iterations, SubCellMethod subCell, double liveTimeS,
        double? knownBackgroundCounts = null)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        calibration.RequireMatch(acquisitionMetadata);
        if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(iterations));
        if (!Enum.IsDefined(estimator)) throw new ArgumentOutOfRangeException(nameof(estimator));
        if (knownBackgroundCounts is { } b && (!double.IsFinite(b) || b < 0)) throw new ArgumentOutOfRangeException(nameof(knownBackgroundCounts));
        if (estimator == BackgroundEstimator.JointMlem && knownBackgroundCounts is not null)
            throw new ArgumentException("Joint fitting does not accept a known amplitude.");
        _mlem = mlem; _correlation = correlation; _geometry = geometry; _calibration = calibration;
        _estimator = estimator; _iterations = iterations; _subCell = subCell;
        _calibrationExpected = calibration.ExpectedCounts(liveTimeS);
        _calibrationUncertainty = calibration.RateStandardUncertaintyCps * liveTimeS;
        _expectedBackground = knownBackgroundCounts ?? _calibrationExpected;
        JointModel = new(mlem.SystemMatrix(acquisitionMetadata.PixelsX, acquisitionMetadata.PixelsY), calibration.Shape.Count);
    }

    public DecodeResult Decode(DetectorImage image) => DecodeCalibrated(image).Reconstruction;

    public BackgroundDecodeResult DecodeCalibrated(DetectorImage image)
    {
        if (image.Width != _calibration.Metadata.PixelsX || image.Height != _calibration.Metadata.PixelsY)
            throw new ArgumentException("Image dimensions differ from the declared calibration.");
        var raw = image.Raw.ToArray();
        if (raw.Any(v => !double.IsFinite(v) || v < 0)) throw new ArgumentException("The acquisition image needs finite non-negative raw counts.");
        DecodeResult decoded;
        double beta = _expectedBackground;
        if (_estimator == BackgroundEstimator.JointMlem)
        {
            var snapshot = JointModel.Snapshots(raw, _calibration.Shape, [_iterations])[0];
            decoded = Reconstruct(snapshot.Source); beta = snapshot.BackgroundCounts;
        }
        else if (_estimator == BackgroundEstimator.KnownScaleMlem)
            decoded = _mlem.Decode(image, _calibration.Shape.Select(p => p * beta).ToArray());
        else
        {
            var signed = new DetectorImage(image.Width, image.Height);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    signed[x, y] = image[x, y] - beta * _calibration.Shape[y * image.Width + x];
            decoded = _correlation.Decode(signed);
        }
        return new(decoded, beta, _calibrationExpected, _calibrationUncertainty);
    }

    /// <summary>Extract a single-source answer from a joint snapshot on the unchanged pixel-area grid.
    /// Confidence zero deliberately supplies no new gate or pair-resolution statistic.</summary>
    public DecodeResult Reconstruct(IReadOnlyList<double> source)
    {
        var (n, origin, step) = _mlem.Grid;
        if (source.Count != n * n) throw new ArgumentException("Snapshot does not match this source grid.");
        var image = new DetectorImage(n, n);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) image[x, y] = source[y * n + x];
        var (bx, by) = PeakInterpolation.Argmax(image);
        var (dx, dy) = PeakInterpolation.Estimate(image, bx, by, _subCell);
        return new(new(new(origin + (bx + dx) * step, origin + (by + dy) * step, _geometry.SourcePlaneZ), 0), image, origin, step);
    }
}
