using Gcam.Configuration;
using Gcam.Core;
using Gcam.Decoding;
using Gcam.Masks;
using Gcam.Simulation;
using Gcam.Tests.Harness;
using Xunit;

namespace Gcam.Tests;

public sealed class BackgroundDecodingTests
{
    // Source-major, positive and deliberately asymmetric: no statistical tolerance or random seed.
    private static readonly double[] Matrix = [0.25, 0.5, 1, 0.125, 1, 0.25, 0.125, 0.75, 0.5, 1, 0.25, 0.5];
    private static readonly double[] Counts = [17, 83, 29, 71];
    private static readonly double[] Shape = [1, 3, 2, 4];
    // Standard forward-error bound for k rounded float operations, unit roundoff u=2^-24.
    private static double Gamma(int k) { double ku = k * Math.Pow(2, -24); return ku / (1 - ku); }
    private static BackgroundCalibrationMetadata Metadata(SimulationConfig c)
        => new(c.Detector.PixelsX, c.Detector.PixelsY, c.Detector.PixelPitchMm,
            DefaultSimulationFactory.BackgroundHeadResponseId(c), "open", "bare", "spectrum-v1");

    [Fact]
    public void JointStep_MatchesIndependentDoubleReference()
    {
        var em = new JointBackgroundMlem(Matrix, 4);
        var actual = em.Snapshots(Counts, Shape, [0, 1], diagnostics: true);
        var p = Shape.Select(v => v / Shape.Sum()).ToArray();
        var mu = Enumerable.Range(0, 4).Select(i => actual[0].BackgroundCounts * p[i]
            + Enumerable.Range(0, 3).Sum(j => Matrix[j * 4 + i] * actual[0].Source[j])).ToArray();
        // Projection (2*3+1), ratio (1), dot (2*4), update (2), conversions (4): 22 operations.
        double relativeBound = Gamma(22);
        for (int j = 0; j < 3; j++)
        {
            double sensitivity = Enumerable.Range(0, 4).Sum(i => Matrix[j * 4 + i]);
            double reference = actual[0].Source[j] / sensitivity * Enumerable.Range(0, 4).Sum(i => Matrix[j * 4 + i] * Counts[i] / mu[i]);
            Assert.InRange(Math.Abs(actual[1].Source[j] - reference), 0, relativeBound * reference);
        }
        double beta = actual[0].BackgroundCounts * Enumerable.Range(0, 4).Sum(i => p[i] * Counts[i] / mu[i]);
        Assert.InRange(Math.Abs(actual[1].BackgroundCounts - beta), 0, relativeBound * beta);
    }

    [Fact]
    public void JointEm_ConservesCountsAndDoesNotDecreaseLikelihoodWithinRoundoff()
    {
        var em = new JointBackgroundMlem(Matrix, 4);
        var snapshots = em.Snapshots(Counts, Shape, Enumerable.Range(0, 61).ToArray(), diagnostics: true);
        // One EM step conserves N exactly in real arithmetic. Each update has the 22-op bound above;
        // summing four non-negative expected-count components adds <=4 double operations, bounded here by floats.
        double countBound = Counts.Sum() * Gamma(26);
        for (int k = 1; k < snapshots.Length; k++)
        {
            Assert.InRange(Math.Abs(snapshots[k].ExpectedTotalCounts - Counts.Sum()), 0, countBound);
            // Perturbing positive mu by relative g changes l by at most
            // N*(-log(1-g)) + sum(mu)*g; compare two independently rounded projections.
            double g = Gamma(26);
            double likelihoodBound = 2 * (Counts.Sum() * -Math.Log(1 - g) + snapshots[k].ExpectedTotalCounts * g);
            Assert.True(snapshots[k].LogLikelihood >= snapshots[k - 1].LogLikelihood - likelihoodBound);
        }
    }

    [Fact]
    public void JointBetaZero_IsAnExactBoundary()
    {
        var snapshots = new JointBackgroundMlem(Matrix, 4).Snapshots(Counts, Shape, [0, 1, 60], initialBackgroundFraction: 0);
        Assert.All(snapshots, s => Assert.Equal(0, s.BackgroundCounts));
    }

    [Fact]
    public void EmptyImage_DoesNotCreateCounts()
    {
        var snapshot = new JointBackgroundMlem(Matrix, 4).Snapshots(new double[4], Shape, [60], diagnostics: true)[0];
        Assert.Equal(0, snapshot.ExpectedTotalCounts);
        Assert.Equal(0, snapshot.BackgroundCounts);
        Assert.All(snapshot.Source, v => Assert.Equal(0, v));
        Assert.Equal(0, snapshot.LogLikelihood);
    }

    [Fact]
    public void Normalization_IsInvariantUnderExactPowerOfTwoScaling()
    {
        var em = new JointBackgroundMlem(Matrix, 4);
        var a = em.Snapshots(Counts, Shape, [60])[0];
        var b = em.Snapshots(Counts, Shape.Select(v => v * 1024).ToArray(), [60])[0];
        Assert.Equal(a.Source, b.Source); Assert.Equal(a.BackgroundCounts, b.BackgroundCounts);
        var m = new BackgroundCalibrationMetadata(2, 2, 1, "head", "open", "bare", "field");
        Assert.Equal(new BackgroundCalibration(Shape, m, 1).Shape, new BackgroundCalibration(Shape.Select(v => v * 1024), m, 1).Shape);
    }

    [Theory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidShapeAndCounts_AreRejected(double bad)
    {
        var m = new BackgroundCalibrationMetadata(2, 2, 1, "head", "open", "bare", "field");
        Assert.Throws<ArgumentException>(() => new BackgroundCalibration([bad, 1, 1, 1], m, 1));
        var em = new JointBackgroundMlem(Matrix, 4);
        Assert.Throws<ArgumentException>(() => em.Snapshots([bad, 1, 1, 1], Shape, [1]));
        Assert.Throws<ArgumentException>(() => em.Snapshots(Counts, [bad, 1, 1, 1], [1]));
    }

    [Fact]
    public void Calibration_IsDetachedAndRefusesWrongBoundWindowGeometryAndHead()
    {
        var m = new BackgroundCalibrationMetadata(2, 2, 1, "head", "open", "bare", "field");
        var map = (double[])Shape.Clone(); var calibration = new BackgroundCalibration(map, m, 2, 0.1, 600, 1200);
        map[0] = 100;
        Assert.Equal(0.1, calibration.Shape[0]); Assert.Equal(120, calibration.ExpectedCounts(60));
        foreach (var wrong in new[] { m with { Bound = "front" }, m with { WindowId = "662" },
            m with { PixelPitchMm = 2 }, m with { HeadResponseId = "other" }, m with { FieldDistributionId = "other" } })
            Assert.Throws<ArgumentException>(() => calibration.RequireMatch(wrong));
        Assert.Throws<ArgumentException>(() => new BackgroundCalibration(new double[4], m, 1));
        Assert.Throws<ArgumentException>(() => new BackgroundCalibration([1, 1], m, 1));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void NullFactoryPath_IsExactlyTheRetainedDecoder(bool handheld, bool mlem)
    {
        var c = handheld ? Rigs.Handheld() : Rigs.Lab(); c.Decoder.Method = mlem ? DecoderMethod.Mlem : DecoderMethod.CrossCorrelation;
        c.Decoder.MlemIterations = 3;
        Assert.Null(c.Decoder.BackgroundCorrection);
        Assert.DoesNotContain("BackgroundCorrection", System.Text.Json.JsonSerializer.Serialize(c));
        var factory = new DefaultSimulationFactory(); var image = Image(c);
        var geo = DefaultSimulationFactory.ReconstructionGeometry(c);
        IDecoder reference = mlem ? MlemReconstruction.Create(c, geo, c.Source.EnergyKeV, 3, c.Decoder.SubCellInterpolation)
            : new CrossCorrelationDecoder(MuraGenerator.DecodingArray(c.Mask.Rank), geo, c.Decoder.SubCellInterpolation);
        Equal(reference.Decode(image), factory.CreateDecoder(c)!.Decode(image));
    }

    [Theory]
    [InlineData(BackgroundCorrectionMode.KnownScaleMlem)]
    [InlineData(BackgroundCorrectionMode.KnownScaleCorrelation)]
    public void KnownScaleModes_AreExactlyDirectFixedOrSignedDecoding(BackgroundCorrectionMode mode)
    {
        var c = Rigs.Lab(); c.Decoder.MlemIterations = 3; var image = Image(c);
        var m = Metadata(c); var map = Enumerable.Range(0, image.Raw.Length).Select(i => (double)(1 + i % 5)).ToArray();
        var calibration = new BackgroundCalibration(map, m, 10);
        c.Decoder.BackgroundCorrection = new() { Mode = mode };
        var factory = new DefaultSimulationFactory();
        if (mode == BackgroundCorrectionMode.KnownScaleMlem)
        {
            Equal(factory.CreateMlemDecoder(c, c.Source.EnergyKeV).Decode(image), factory.CreateDecoder(c, calibration, m, 0).Decode(image));
            Equal(factory.CreateMlemDecoder(c, c.Source.EnergyKeV).Decode(image, calibration.Shape.Select(p => p * 600).ToArray()),
                factory.CreateDecoder(c, calibration, m, 60).Decode(image));
        }
        else
        {
            var signed = new DetectorImage(image.Width, image.Height);
            for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
                signed[x, y] = image[x, y] - 600 * calibration.Shape[y * image.Width + x];
            Assert.Contains(signed.Raw.ToArray(), v => v < 0);
            var decoder = new CrossCorrelationDecoder(MuraGenerator.DecodingArray(c.Mask.Rank), DefaultSimulationFactory.ReconstructionGeometry(c), c.Decoder.SubCellInterpolation);
            Equal(decoder.Decode(signed), factory.CreateDecoder(c, calibration, m, 60).Decode(image));
        }
    }

    [Fact]
    public void ConfigCloneAndExplicitOptIn_RequireCalibration()
    {
        var c = Rigs.Lab(); c.Decoder.BackgroundCorrection = new();
        Assert.Equal(BackgroundCorrectionMode.JointMlem, c.Clone().Decoder.BackgroundCorrection!.Mode);
        var f = new DefaultSimulationFactory(); Assert.Throws<InvalidOperationException>(() => f.CreateDecoder(c));
        var m = Metadata(c); var calibration = new BackgroundCalibration(Enumerable.Repeat(1.0, m.PixelsX * m.PixelsY), m, 1);
        c.Geometry.MaskDetectorDistanceMm += 1;
        Assert.Throws<ArgumentException>(() => f.CreateDecoder(c, calibration, m, 60));
    }

    private static DetectorImage Image(SimulationConfig c)
    {
        var image = new DetectorImage(c.Detector.PixelsX, c.Detector.PixelsY);
        for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++) image[x, y] = 1 + (x * 7 + y * 3) % 19;
        return image;
    }

    private static void Equal(DecodeResult expected, DecodeResult actual)
    {
        Assert.Equal(expected.Estimate, actual.Estimate);
        Assert.Equal(expected.ReconOriginMm, actual.ReconOriginMm); Assert.Equal(expected.ReconStepMm, actual.ReconStepMm);
        Assert.Equal(expected.Reconstruction.Raw.ToArray(), actual.Reconstruction.Raw.ToArray());
    }
}
