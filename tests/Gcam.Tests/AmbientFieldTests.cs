using System.Security.Cryptography;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;

namespace Gcam.Tests;

public sealed class AmbientFieldTests
{
    private static SimulationConfig Config(double dose = .1, AmbientGeometry geometry = AmbientGeometry.BareCrystalAllFaces)
        => new() { Seed = 12345, Source = new() { ActivityBq = 0 },
            Ambient = new() { DoseRateMicroSvPerHour = dose, Geometry = geometry } };

    private static List<DetectedEvent> Collect(ListModeSource source, double horizon)
    {
        var events = new List<DetectedEvent>();
        while (source.ArrivalTimeS < horizon)
            if (source.AdvanceUntil(horizon) is { } e) events.Add(e);
        return events;
    }

    [Theory]
    [InlineData(0, "6C9B4AD52DA2F4F2C232EB5095603BD4C9AC315101C8D612BA2207A5C1B3ACF5")]
    [InlineData(1, "D70F1457599566E172E7B38412D75B23C5640AF9F4867EE50B61FAF6958ECA92")]
    public void AmbientNull_MatchesCapturedPreimplementationRecordsAndRng(double bsr, string expected)
    {
        // Captured before any implementation changes: 256 complete records plus history/weight statistics,
        // BinaryWriter little-endian fields, no rounded values and no numerical tolerance.
        var config = new SimulationConfig { Seed = 12345 };
        if (bsr > 0) config.Background = new() { BackgroundToSignalRatio = bsr, DarkCountRateKcps = .001 };
        using var source = new ListModeSource(config);
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
        int count = 0;
        while (count < 256)
            if (source.Advance() is { } e)
            {
                writer.Write(e.PixelX); writer.Write(e.PixelY); writer.Write(e.DepositKeV); writer.Write(e.ArrivalTimeS); count++;
            }
        writer.Write(source.HistoriesEmitted); writer.Write(source.HistoriesDetected);
        writer.Write(source.EventsAccepted); writer.Write(source.DetectedWeight); writer.Flush();
        Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(memory.ToArray())));
    }

    [Theory]
    [InlineData(AmbientGeometry.BareCrystalAllFaces)]
    [InlineData(AmbientGeometry.FrontOnlyThroughMask)]
    public void EmptyIntervalsAndSegmentBoundaries_PreserveEvents(AmbientGeometry geometry)
    {
        using var whole = new ListModeSource(Config(1, geometry));
        using var split = new ListModeSource(Config(1, geometry));
        var expected = Collect(whole, 100);
        var actual = new List<DetectedEvent>();
        for (int i = 1; i <= 100; i++) actual.AddRange(Collect(split, i));
        Assert.Equal(expected, actual);
        Assert.Equal(100, split.ArrivalTimeS);
        Assert.All(actual, e => Assert.True(e.DepositKeV > 0 && e.DepositKeV <= 661.7));
        Assert.True(actual.Zip(actual.Skip(1)).All(p => p.First.ArrivalTimeS < p.Second.ArrivalTimeS));
    }

    [Fact]
    public void SourceFreeZeroField_ProgressesAndRejectsCancellationBeforeRandomDraws()
    {
        using var source = new ListModeSource(Config(0));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => source.AdvanceUntil(10, cancelled.Token));
        Assert.Equal(0, source.ArrivalTimeS);
        Assert.Null(source.AdvanceUntil(10)); Assert.Equal(10, source.ArrivalTimeS);
        Assert.Empty(Collect(source, 20)); Assert.Equal(20, source.ArrivalTimeS);
    }

    [Fact]
    public void AbsoluteField_IsExactlyIndependentOfConfiguredSourceActivity()
    {
        var weak = Config(); var strong = weak.Clone(); strong.Source.ActivityBq = 1e12;
        var a = new AmbientPhotonProcess(weak); var b = new AmbientPhotonProcess(strong);
        for (int i = 1; i <= 1000; i++)
        {
            Assert.Equal(a.AdvanceUntil(i), b.AdvanceUntil(i));
            Assert.Equal(a.TimeS, b.TimeS); Assert.Equal(a.Histories, b.Histories);
        }
    }

    [Fact]
    public void IncidentPoissonBins_MeanAndVarianceUseSampleSizeDerivedTolerances()
    {
        var process = new AmbientPhotonProcess(Config());
        const int bins = 10000; const double lambda = 1;
        double width = lambda / process.IncidentRateCps;
        var counts = new double[bins];
        for (int i = 0; i < bins; i++)
        {
            long before = process.Histories; double horizon = (i + 1) * width;
            while (process.TimeS < horizon) process.AdvanceUntil(horizon);
            counts[i] = process.Histories - before;
        }
        double mean = counts.Average();
        double variance = counts.Sum(x => (x - mean) * (x - mean)) / (bins - 1);
        // Poisson mean variance lambda/N; unbiased sample-variance variance lambda/N + 2*lambda^2/(N-1).
        // Six standard errors are a regression bound derived from this test's N, not a borrowed rate tolerance.
        Assert.InRange(Math.Abs(mean - lambda), 0, 6 * Math.Sqrt(lambda / bins));
        Assert.InRange(Math.Abs(variance - lambda), 0, 6 * Math.Sqrt(lambda / bins + 2 * lambda * lambda / (bins - 1)));
    }

    [Fact]
    public void DoseClosure_IntegratesContinuumAgainstTheSameIcrpTable()
    {
        var config = Config();
        config.Ambient!.Spectrum = new() { Lines = [new() { EnergyKeV = 1460.82, FluenceWeight = 2 }],
            Continuum = [new() { LowKeV = 100, HighKeV = 300, FluenceWeight = 3 }] };
        var process = new AmbientPhotonProcess(config);
        double average = (2 * AmbientDose.PerFluence(1460.82) + 3 * AmbientDose.AveragePerFluence(100, 300)) / 5;
        double dose = process.FluencePerCm2PerSecond * average * 3600 / 1e6;
        // Eight floating-point operations: generous 32 machine-epsilon relative roundoff budget, no MC tolerance.
        Assert.InRange(Math.Abs(dose - .1), 0, 32 * Math.ScaleB(1.0, -52) * .1);
        // Independent midpoint quadrature: for 10,000 subintervals of one power-law table segment,
        // composite midpoint error <= max|h''|*(b-a)^2/(24*N^2) for the bin average.
        const int n = 10000; double a = 100, b = 150, sum = 0;
        for (int i = 0; i < n; i++) sum += AmbientDose.PerFluence(a + (i + .5) * (b - a) / n);
        double power = Math.Log(AmbientDose.PerFluence(b) / AmbientDose.PerFluence(a)) / Math.Log(b / a);
        double second = Math.Max(AmbientDose.PerFluence(a) / (a * a), AmbientDose.PerFluence(b) / (b * b)) * Math.Abs(power * (power - 1));
        Assert.InRange(Math.Abs(sum / n - AmbientDose.AveragePerFluence(a, b)), 0,
            second * (b - a) * (b - a) / (24.0 * n * n) + 64 * Math.ScaleB(1.0, -52));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void EveryCrystalFace_ScoresAtItsActualDepositPixel(int face)
    {
        var rays = new[] {
            new Ray(new(2,3,20), new(0,0,-1)), new Ray(new(2,3,-20), new(0,0,1)),
            new Ray(new(-20,2,-5), new(1,0,0)), new Ray(new(20,2,-5), new(-1,0,0)),
            new Ray(new(2,-20,-5), new(0,1,0)), new Ray(new(2,20,-5), new(0,-1,0)) };
        var expected = new[] { (8,9), (8,9), (0,8), (11,8), (8,0), (8,11) };
        (int,int)? pixel = null;
        var detector = new ComptonCrystalDetector(12,12,1,1,double.MaxValue,ComptonStrategy.Argmax,
            new DefaultRandom(55), muAt662PerMm: 1000, entryThroughAllFaces: true,
            pixelEventSink: (x,y,_,_) => pixel=(x,y));
        Assert.True(detector.Score(new Photon { Ray=rays[face], EnergyKeV=20, Weight=1 }));
        Assert.Equal(expected[face], pixel);
    }

    [Fact]
    public void DevelopmentPlaceholder_CannotBeUsedAsValidatedEvidence()
    {
        var config = Config(); config.Ambient!.RequireValidatedSpectrum = true;
        Assert.Throws<InvalidOperationException>(() => new AmbientPhotonProcess(config));
        Assert.Throws<InvalidOperationException>(() => new SimulationRunner(new DefaultSimulationFactory()).Run(Config()));
    }

    [Fact]
    public void FixedTimeFloodAndEventStream_AgreeWithoutResampling()
    {
        var config = Config(1);
        using var source = new ListModeSource(config);
        var truth = Collect(source, 100);
        var flood = new SimulationRunner(new DefaultSimulationFactory()).RunFixedTime(config, 100);
        Assert.Equal(truth.Count, flood.DetectedWeight);
        foreach (var group in truth.GroupBy(e => (e.PixelX, e.PixelY)))
            Assert.Equal(group.Count(), flood.DetectorImage[group.Key.PixelX, group.Key.PixelY]);
        var stream = new EventStreamStudy().GenerateFixedTime(config, 100, 1000);
        Assert.Equal(truth.Select(e => e.DepositKeV), stream.Select(e => e.EnergyKeV));
        Assert.Equal(truth.Select(e => (long)Math.Round(e.ArrivalTimeS * 1000)), stream.Select(e => e.ArrivalSample));
    }

    [Fact]
    public void ZeroAbsoluteField_WithSourceAndLegacyBsr_ReplaysTheLegacyStreamExactly()
    {
        var original = new SimulationConfig { Seed = 12345, Background = new() { BackgroundToSignalRatio = 1 } };
        var enabled = original.Clone(); enabled.Ambient = new();
        using var legacy = new ListModeSource(original); using var physical = new ListModeSource(enabled);
        Assert.Equal(Collect(legacy, 10), Collect(physical, 10));
    }

    [Fact]
    public void SourceFreeRelativeBsrAddsZero_AndElectronicDarkCountsRemainIndependent()
    {
        var config = Config(0); config.Background = new() { BackgroundToSignalRatio = 100, DarkCountRateKcps = .01 };
        using var source = new ListModeSource(config);
        var events = Collect(source, 100);
        Assert.All(events, e => Assert.Equal(3, e.DepositKeV));
        // Known dark rate 10/s and exposure 100s: counts are Poisson(lambda=1000), six sqrt(lambda) bound.
        Assert.InRange(Math.Abs(events.Count - 1000), 0, 6 * Math.Sqrt(1000));
        Assert.Equal(0, source.SourceRateCps); Assert.Equal(0, source.AmbientRateCps);
    }

    [Fact]
    public void ParallelBeam_InteractionEfficiencyHasBinomialSampleSizeTolerance()
    {
        const int n = 20000; const double mu = .1, depth = 10;
        var detector = new ComptonCrystalDetector(12,12,1,1,double.MaxValue,ComptonStrategy.Argmax,
            new DefaultRandom(73), muAt662PerMm: mu, crystalDepthMm: depth);
        int hit = 0;
        for (int i = 0; i < n; i++)
            if (detector.Score(new Photon { Ray=new(new(0,0,10),new(0,0,-1)), EnergyKeV=CrystalMaterial.ReferenceKeV, Weight=1 })) hit++;
        double p = 1 - Math.Exp(-mu * depth);
        Assert.InRange(Math.Abs(hit / (double)n - p), 0, 6 * Math.Sqrt(p * (1-p) / n));
    }

    [Fact]
    public void DetectedCountsPerPixel_ArePoissonOverFixedTimeBins()
    {
        const int bins = 2000;
        var config = Config(2); var process = new AmbientPhotonProcess(config);
        int pixels = config.Detector.PixelsX * config.Detector.PixelsY;
        var sum = new double[pixels]; var squareSum = new double[pixels];
        for (int i = 1; i <= bins; i++)
        {
            var counts = new int[pixels];
            while (process.TimeS < i)
                if (process.AdvanceUntil(i) is { } e) counts[e.PixelY * config.Detector.PixelsX + e.PixelX]++;
            for (int j = 0; j < pixels; j++) { sum[j] += counts[j]; squareSum[j] += counts[j] * counts[j]; }
        }
        for (int j = 0; j < pixels; j++)
        {
            double mean = sum[j] / bins;
            double variance = (squareSum[j] - bins * mean * mean) / (bins - 1);
            // For Poisson samples, Var(s^2 - mean) = 2*lambda^2/(N-1), exactly.
            // Unknown lambda is conservatively bounded by solving k >= mu - 6*sqrt(mu), with mu=N*lambda.
            // Six SE covers 144 pixel comparisons; no assumption that edge and centre response means are equal.
            double lambdaUpper = Math.Pow((6 + Math.Sqrt(36 + 4 * sum[j])) / 2, 2) / bins;
            Assert.InRange(Math.Abs(variance - mean), 0, 6 * Math.Sqrt(2 * lambdaUpper * lambdaUpper / (bins - 1)));
        }
    }

    [Fact]
    public void EvidenceHashAndInvalidSpectrumAreRejectedWithoutTransport()
    {
        var config = Config(); config.Ambient!.Spectrum.IsValidated = true;
        config.Ambient.RequireValidatedSpectrum = true;
        Assert.Throws<InvalidOperationException>(() => new AmbientPhotonProcess(config)); // stale payload hash
        config.Ambient.RequireValidatedSpectrum = false;
        config.Ambient.Spectrum.Lines[0].FluenceWeight = double.NaN;
        Assert.Throws<ArgumentException>(() => new AmbientPhotonProcess(config));
        config.Ambient.Spectrum.Lines[0].FluenceWeight = 1;
        config.Ambient.Spectrum.AngularModel = "Tabulated";
        Assert.Throws<NotSupportedException>(() => new AmbientPhotonProcess(config));
    }
}
