using System.Text.Json;
using Gcam.Configuration;
using Gcam.Simulation;
using Gcam.Tests.Harness;

namespace Gcam.Tests;

public sealed class GateResponseTests
{
    private static IncidentSpectrum Validated()
        => JsonSerializer.Deserialize<IncidentSpectrum>(File.ReadAllBytes(RepoPaths.Sample("ambient/terrestrial-unscear2000-v1.json")))!;

    /// <summary>Independent Φ: the Maclaurin series of erf (accurate in double for |z| ≤ 4); beyond |z| = 6 the tail is
    /// below 1e-9 and Φ is taken as 0 or 1.</summary>
    private static double Phi(double z)
    {
        if (Math.Abs(z) > 6) return z > 0 ? 1 : 0;
        double x = z / Math.Sqrt(2), term = x, sum = x;
        for (int n = 1; n < 200; n++) { term *= -x * x / n; sum += term / (2 * n + 1); }
        return 0.5 * (1 + 2 / Math.Sqrt(Math.PI) * sum);
    }

    [Theory]
    [InlineData(-3.0)]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    [InlineData(1.5)]
    [InlineData(3.4)]
    public void NormalCdf_AgreesWithTheErfSeries(double z)
        // erfcc's documented fractional error bound is 1.2e-7 on erfc; Φ ≤ 1, so 1.2e-7 absolute covers it.
        => Assert.InRange(CountingWindow.NormalCdf(z) - Phi(z), -1.2e-7, 1.2e-7);

    [Fact]
    public void Acceptance_IsTheWindowProbabilityOfTheSmearedDeposit()
    {
        var open = CountingWindow.Open();
        Assert.Equal(1, open.Acceptance(5)); Assert.Equal(0, open.Acceptance(0));
        var hard = new CountingWindow("cs", 595.53, 727.87, 0);
        Assert.Equal(1, hard.Acceptance(661.7)); Assert.Equal(0, hard.Acceptance(595.5)); Assert.Equal(1, hard.Acceptance(595.53));
        // 7 % FWHM at 661.7 keV: σ = 0.07·661.7/2.35482 = 19.67 keV; a 661.7-keV deposit is inside ±66.17 keV with
        // probability Φ(3.364) − Φ(−3.364).
        var smeared = new CountingWindow("cs", 595.53, 727.87, 0.07);
        double sigma = 0.07 * 661.7 / 2.3548200450309493;
        Assert.InRange(smeared.Acceptance(661.7) - (Phi(66.17 / sigma) - Phi(-66.17 / sigma)), -2.4e-7, 2.4e-7);
        // FWHM ∝ √E: at 750 keV σ grows by √(750/661.7).
        double far = 750, sigmaFar = sigma * Math.Sqrt(far / 661.7);
        Assert.InRange(smeared.Acceptance(far) - (Phi((727.87 - far) / sigmaFar) - Phi((595.53 - far) / sigmaFar)), -2.4e-7, 2.4e-7);
    }

    [Fact]
    public void AmbientOpenWindowRate_IsTheProcessDetectedRate()
    {
        // The map is the engine's own process tallied: with an open window its total equals the process's detected rate
        // for the same seed and histories (an identity, no tolerance beyond summation rounding).
        var config = Rigs.Lab(seed: 3201);
        const long histories = 20_000;
        const int seed = 3202;
        var maps = GateResponse.Ambient(config, AmbientGeometry.BareCrystalAllFaces, Validated(), histories, seed, [CountingWindow.Open()]);
        var run = config.Clone(); run.Seed = seed;
        run.Ambient = new() { DoseRateMicroSvPerHour = 1, Spectrum = Validated(), RequireValidatedSpectrum = true };
        var process = new AmbientPhotonProcess(run);
        for (int h = 0; h < histories; h++) process.AdvanceUntil(1e300);
        Assert.Equal(process.Detected, maps.Events);
        Assert.Equal(process.DetectedRateCps, maps.TotalRate(0), 1e-9 * process.DetectedRateCps);
    }

    [Fact]
    public void AmbientMap_RefusesTheNotValidatedSpectrum()
    {
        var spectrum = JsonSerializer.Deserialize<IncidentSpectrum>(File.ReadAllBytes(RepoPaths.Sample("ambient/terrestrial-unscear2000-v1-NOT-VALIDATED.json")))!;
        Assert.Throws<InvalidOperationException>(() =>
            GateResponse.Ambient(Rigs.Lab(), AmbientGeometry.BareCrystalAllFaces, spectrum, 10, 1, [CountingWindow.Open()]));
    }

    [Fact]
    public void SourceRate_ScalesWithBranchingAndSplitsAcrossWindows()
    {
        // Same seed → same transport: the rate is exactly proportional to the branching ratio, and a window never
        // counts more than the open total (acceptance ≤ 1 per event).
        var config = Rigs.Handheld(seed: 3203);
        CountingWindow[] windows = [CountingWindow.Open(), new("cs662", 595.53, 727.87, config.Detector.EnergyResolutionFwhm)];
        var a = GateResponse.Source(config, 50_000, 3204, windows);
        config.Source.BranchingRatio /= 2;
        var b = GateResponse.Source(config, 50_000, 3204, windows);
        Assert.Equal(a.TotalRate(0) / 2, b.TotalRate(0), 1e-12 * a.TotalRate(0));
        Assert.True(a.TotalRate(1) > 0 && a.TotalRate(1) < a.TotalRate(0));
    }
}
