using Gcam.Core;
using Gcam.Simulation;
using Gcam.Studio.Core.Services;
using Xunit.Abstractions;

namespace Gcam.Studio.Services.Tests;

public sealed class StripCountEstimatorTests(ITestOutputHelper output)
{
    [Fact]
    public void Overlap_IdenticalWindows_HaveExactlyZeroNetAndVariance()
    {
        var ratio = new StripRatio("low", "high", 100, 40, 40) { OverlapCounts = 40 };
        var estimate = StripCountEstimator.ForPair(12, 12, 12, ratio);
        Assert.Equal(0, estimate.NetCounts);
        Assert.Equal(0, estimate.Variance);
        Assert.True(estimate.HasRawCounts);
    }

    [Fact]
    public void ZeroCalibrationLow_OrMissingCovariance_ReportsUnavailableUncertainty()
    {
        var ratio = new StripRatio("low", "high", 100, 0, 40) { OverlapCounts = 0 };
        var estimate = StripCountEstimator.ForPair(0, 10, 0, ratio);
        Assert.Equal(0, estimate.NetCounts);
        Assert.Null(estimate.Variance);
        Assert.True(estimate.HasRawCounts);
        Assert.Null(StripCountEstimator.ForPair(3, 10, 0, ratio with { LowCounts = 5, OverlapCounts = null }).Variance);
    }

    [Theory]
    [InlineData(0, 0, 0, false)]
    [InlineData(5, 10, 0, true)]
    [InlineData(3, 10, -2, true)]
    public void NetSign_IsIndependentOfAcquisitionSupport(double low, double high, double net, bool hasData)
    {
        var ratio = new StripRatio("low", "high", 100, 20, 40) { OverlapCounts = 0 };
        var estimate = StripCountEstimator.ForPair(low, high, 0, ratio);
        Assert.Equal(net, estimate.NetCounts);
        Assert.Equal(hasData, estimate.HasRawCounts);
    }

    [Fact]
    public void IndependentFixedTotalCalibration_ReportedVarianceMatchesDerivedSamplingBounds()
    {
        const int total = 2000, repeats = 4096;
        const double pLow = 0.2, pHigh = 0.4, muLow = 1000, muHigh = 4000;
        // Independent expectation, before any draws: H ~ Binomial(T,pH),
        // L|H ~ Binomial(T-H,pL/(1-pH)). Integrate exact ratio moments and the plug-in variance moments.
        // H=0 is excluded just as calibration rejects it; P(H=0)=0.6^2000 (<double's representable range).
        var rMoments = new double[5];
        double sumWeight = 0, meanVarianceR = 0, secondVarianceR = 0;
        for (int h = 1; h <= total; h++)
        {
            double weight = Math.Exp(StrippingStatistics.LogGamma(total + 1) - StrippingStatistics.LogGamma(h + 1) -
                StrippingStatistics.LogGamma(total - h + 1) + h * Math.Log(pHigh) + (total - h) * Math.Log(1 - pHigh));
            var l = BinomialMoments(total - h, pLow / (1 - pHigh));
            sumWeight += weight;
            for (int k = 0; k <= 4; k++) rMoments[k] += weight * l[k] / Math.Pow(h, k);
            // Var(Rhat) plug-in = L/H² + L²/H³, no simulation or call to ForPair in this expectation.
            meanVarianceR += weight * (l[1] / Math.Pow(h, 2) + l[2] / Math.Pow(h, 3));
            secondVarianceR += weight * (l[2] / Math.Pow(h, 4) + 2*l[3] / Math.Pow(h, 5) + l[4] / Math.Pow(h, 6));
        }
        for (int k = 0; k <= 4; k++) rMoments[k] /= sumWeight;
        meanVarianceR /= sumWeight; secondVarianceR /= sumWeight;
        double Expect(double[] polynomial) => polynomial.Select((v,k) => v * rMoments[k]).Sum();
        double mean = muLow - rMoments[1]*muHigh;
        double[] shift = [muHigh*rMoments[1], -muHigh];
        double[] k2 = [muLow,0,muHigh], k3 = [muLow,0,0,-muHigh], k4 = [muLow,0,0,0,muHigh];
        double variance = Expect(k2) + Expect(Multiply(shift,shift));
        // Conditional Poisson cumulants and the random conditional mean give the exact fourth central moment.
        var shift2 = Multiply(shift,shift);
        double fourth = Expect(k4) + 3*Expect(Multiply(k2,k2)) + 4*Expect(Multiply(shift,k3)) +
            6*Expect(Multiply(shift2,k2)) + Expect(Multiply(shift2,shift2));
        double varianceSe = Math.Sqrt((fourth - (repeats-3.0)/(repeats-1)*variance*variance)/repeats);
        double meanSe = Math.Sqrt(variance/repeats);
        double h2 = muHigh*muHigh+muHigh;
        double h4 = Math.Pow(muHigh,4)+6*Math.Pow(muHigh,3)+7*muHigh*muHigh+muHigh;
        double reportedExpectation = muLow+rMoments[2]*muHigh+h2*meanVarianceR;
        // Cauchy: SD(A+B+C) <= SD(A)+SD(B)+SD(C), including their shared H and calibration dependence.
        double reportedSeBound = (Math.Sqrt(muLow) +
            Math.Sqrt(rMoments[4]*h2-Math.Pow(rMoments[2]*muHigh,2)) +
            Math.Sqrt(h4*secondVarianceR-Math.Pow(h2*meanVarianceR,2))) / Math.Sqrt(repeats);
        // Distribution-free Chebyshev bounds: each assertion has failure probability <=1/16 at four SE.
        // These bounds and the finite-ratio bias are calculated before sampling; no percent tolerance is fitted.
        const double standardErrors = 4;
        var random = new DefaultRandom(390039);
        var observed = new double[repeats];
        double sumReported = 0;
        for (int repeat = 0; repeat < repeats; repeat++)
        {
            int l = 0, h = 0;
            for (int t = 0; t < total; t++)
            {
                double u = random.NextDouble();
                if (u < pLow) l++; else if (u < pLow+pHigh) h++;
            }
            Assert.True(l > 0 && h > 0); // The specified probabilities make the unsupported case negligible here.
            var ratio = new StripRatio("low", "high", total, l, h) { OverlapCounts = 0 };
            var estimate = StripCountEstimator.ForPair(Sampling.PoissonExact(random,muLow),
                Sampling.PoissonExact(random,muHigh),0,ratio);
            observed[repeat] = estimate.NetCounts;
            sumReported += estimate.Variance!.Value;
        }
        double actualMean = observed.Average();
        double actualVariance = observed.Sum(v => (v-actualMean)*(v-actualMean))/(repeats-1);
        double reported = sumReported/repeats;
        output.WriteLine($"Independent calibration T={total}, N={repeats}, seed=390039: mean {actualMean:F6}, expected {mean:F6}, bound {standardErrors*meanSe:F6}; variance {actualVariance:F6}, exact {variance:F6}, bound {standardErrors*varianceSe:F6}; mean reported variance {reported:F6}, expected {reportedExpectation:F6}, bound {standardErrors*reportedSeBound:F6}; empirical SD {Math.Sqrt(actualVariance):F6}, reported sigma {Math.Sqrt(reported):F6}");
        Assert.InRange(Math.Abs(actualMean-mean),0,standardErrors*meanSe);
        Assert.InRange(Math.Abs(actualVariance-variance),0,standardErrors*varianceSe);
        Assert.InRange(Math.Abs(reported-reportedExpectation),0,standardErrors*reportedSeBound);
        // Direct empirical-vs-reported comparison includes the exact first-order approximation's bias.
        Assert.InRange(Math.Abs(actualVariance-reported),0,
            Math.Abs(variance-reportedExpectation)+standardErrors*(varianceSe+reportedSeBound));
    }

    private static double[] BinomialMoments(int n, double p)
    {
        double a=n*p, b=n*(n-1.0)*p*p, c=n*(n-1.0)*(n-2.0)*p*p*p,
            d=n*(n-1.0)*(n-2.0)*(n-3.0)*Math.Pow(p,4);
        return [1,a,a+b,a+3*b+c,a+7*b+6*c+d];
    }

    private static double[] Multiply(double[] a, double[] b)
    {
        var result = new double[a.Length+b.Length-1];
        for (int i=0;i<a.Length;i++) for (int j=0;j<b.Length;j++) result[i+j]+=a[i]*b[j];
        return result;
    }
}
