using Gcam.Configuration;
using Gcam.Detector;
using Gcam.Simulation;
using Xunit;

namespace Gcam.Tests;

/// <summary>
/// Thermal drift during acquisition: the SiPM gain temperature coefficient walks the photopeak out of the
/// fixed per-crystal window (efficiency droop) and, via the self-heating spatial gradient, leaves a flood-
/// correction residual — while bias-compensation recovers both. Confirms the real-rig fact that drift is an
/// energy-window (efficiency) problem, not a localization one.
/// </summary>
public class ThermalDriftTests
{
    // --- ThermalDrift model unit tests ---

    [Fact]
    public void AtCalibration_NoShift()
    {
        var d = new ThermalDrift(12, 12, ambientRatePerT: 3.0, selfHeatC: 8.0);
        Assert.Equal(0.0, d.AmbientDelta(0.0), 12);
        Assert.Equal(0.0, d.CentroidShift(6, 6, 0.0), 12);
    }

    [Fact]
    public void Ambient_IsUniformAcrossArray()
    {
        var d = new ThermalDrift(12, 12, ambientRatePerT: 3.0, selfHeatC: 0.0);
        // With no self-heating, every crystal sees the same (ambient-only) shift.
        Assert.Equal(d.CentroidShift(0, 0, 1.0), d.CentroidShift(11, 11, 1.0), 12);
    }

    [Fact]
    public void SelfHeating_CentreHotterThanCorner()
    {
        var d = new ThermalDrift(12, 12, ambientRatePerT: 0.0, selfHeatC: 8.0, selfHeatTau: 0.3, edgeFactor: 0.4);
        double centre = d.DeltaT(6, 6, 1.0);
        double corner = d.DeltaT(0, 0, 1.0);
        Assert.True(centre > corner, $"centre {centre} should exceed corner {corner}");
    }

    [Fact]
    public void BiasComp_ShrinksTheShift()
    {
        var off = new ThermalDrift(12, 12, biasCompFraction: 0.0, ambientRatePerT: 3.0, selfHeatC: 8.0);
        var on  = new ThermalDrift(12, 12, biasCompFraction: 0.9, ambientRatePerT: 3.0, selfHeatC: 8.0);
        Assert.True(System.Math.Abs(on.CentroidShift(6, 6, 1.0)) < System.Math.Abs(off.CentroidShift(6, 6, 1.0)) * 0.2);
    }

    // --- Photopeak acceptance: a centroid shift walks the peak out of the fixed window ---

    [Fact]
    public void ShiftReducesAcceptance_ReducesToStaticAtZero()
    {
        double a0 = CrystalUniformity.PhotopeakAcceptance(0.08, 0.10, 0.0);
        double a1 = CrystalUniformity.PhotopeakAcceptance(0.08, 0.10, 0.05);   // peak walked 5% high
        Assert.True(a1 < a0, "a shifted peak must lose window acceptance");
        Assert.True(a0 > 0.95, "±10% window over 8% FWHM keeps almost all counts when centred");
    }

    // --- Study: efficiency droop, flood residual, and bias-comp recovery ---

    private static SimulationConfig DriftConfig()
    {
        var cfg = new SimulationConfig { PhotonCount = 400_000, Seed = 12345 };
        cfg.Source.Position = [5.0, 0.0, 0.0];
        cfg.Detector.EnergyResolutionFwhm = 0.08;
        cfg.Detector.EnergyResolutionFwhmSigma = 0.30;
        cfg.Detector.EnergyWindowFraction = 0.10;
        cfg.Detector.GainSigma = 0.05;
        cfg.Detector.GainGradient = 0.10;
        cfg.Detector.UniformitySeed = 7;
        return cfg;
    }

    private static ThermalDrift Drift(double comp) => new ThermalDrift(12, 12,
        alphaPerC: -0.007, biasCompFraction: comp,
        ambientRatePerT: 3.0, selfHeatC: 8.0, selfHeatTau: 0.3, edgeFactor: 0.4);

    [Fact]
    public void DriftDroopsEfficiency_BiasCompRecovers()
    {
        var cfg = DriftConfig();
        double[] times = [0.0, 0.5, 1.0];
        var study = new ThermalDriftStudy(new DefaultSimulationFactory());
        var off = study.Run(cfg, Drift(0.0), times, 400_000.0, 12);
        var on  = study.Run(cfg, Drift(0.9), times, 400_000.0, 12);

        // Calibration point: no drift.
        Assert.Equal(1.0, off[0].Efficiency, 3);
        Assert.True(off[0].ResidualCoV < 1e-6);

        // Uncompensated drift walks the photopeak out of the window (efficiency droop) and
        // leaves a flood-correction residual the static calibration cannot remove.
        Assert.True(off[^1].Efficiency < 0.95, $"efficiency should droop, got {off[^1].Efficiency}");
        Assert.True(off[^1].ResidualCoV > 0.03, $"residual should grow, got {off[^1].ResidualCoV}");

        // Bias-compensation recovers both.
        Assert.True(on[^1].Efficiency > 0.99, $"comp should hold efficiency, got {on[^1].Efficiency}");
        Assert.True(on[^1].ResidualCoV < off[^1].ResidualCoV * 0.2, "comp should shrink the residual");

        // Drift is an energy-window problem, not a localization one: localization stays BOUNDED near the decoder
        // floor (no positional blow-up, which would push RMS to many mm as in the ghost cases). The counts now
        // also droop with efficiency, so at these low reps (12) Poisson noise dominates the last-slice RMS — the
        // flat trend proper is the CLI/plot's job; here we only guard against a blow-up.
        // Bound measured (TODO-26, 64 seeds 2001–2064): the last-slice RMS is 0.36–0.71 mm in 48 seeds and
        // 1.3–4.3 mm in 16 — one of the 12 reps landing on a wrong peak (≤ ~15 mm → RMS ≤ 15/√12 ≈ 4.3 mm). A blow-up
        // (half the reps at ghost scale) is ≥ 8 mm. 5 mm separates the two; the old 2.5 mm bound failed 10 / 64 seeds.
        Assert.True(off[^1].RmsBiasMm < 5.0, $"localization stays bounded, got {off[^1].RmsBiasMm}");
    }
}
