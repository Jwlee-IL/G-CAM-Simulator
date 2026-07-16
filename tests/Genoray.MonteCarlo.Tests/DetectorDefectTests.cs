using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Detector;
using Genoray.MonteCarlo.Simulation;
using Xunit;

namespace Genoray.MonteCarlo.Tests;

/// <summary>
/// Bad (dead / hot) detector pixels imprint fixed, non-coded structure on the flood that pulls the ideal
/// decoder's correlation off the true peak; a known bad-pixel map repairs most of it by interpolation (the
/// discrete analogue of flood-field correction).
/// </summary>
public class DetectorDefectTests
{
    // --- DetectorDefects model ---

    [Fact]
    public void SameSeed_IsDeterministic()
    {
        var a = new DetectorDefects(12, 12, 0.05, 0.03, 5.0, seed: 4);
        var b = new DetectorDefects(12, 12, 0.05, 0.03, 5.0, seed: 4);
        for (int i = 0; i < 144; i++)
        {
            Assert.Equal(a.Dead[i], b.Dead[i]);
            Assert.Equal(a.Hot[i], b.Hot[i]);
        }
    }

    [Fact]
    public void APixelIsNeverBothDeadAndHot()
    {
        var d = new DetectorDefects(16, 16, 0.1, 0.1, 5.0, seed: 1);
        for (int i = 0; i < 256; i++)
            Assert.False(d.Dead[i] && d.Hot[i]);
        Assert.True(d.DeadCount > 0 && d.HotCount > 0);
    }

    [Fact]
    public void DefectCounts_TrackTheFractions()
    {
        // Large array so the law of large numbers makes the counts close to the requested fractions.
        var d = new DetectorDefects(100, 100, 0.05, 0.02, 5.0, seed: 3);
        Assert.InRange(d.DeadCount / 10000.0, 0.04, 0.06);
        Assert.InRange(d.HotCount / 10000.0, 0.015, 0.025);
    }

    // --- Study: defects degrade localization; a known bad-pixel map repairs it ---

    [Fact]
    public void BadPixels_DegradeThenRepairRecovers()
    {
        var cfg = new SimulationConfig { PhotonCount = 400_000, Seed = 12345 };
        double[] badPct = [0.0, 8.0];
        var rows = new DetectorDefectStudy(new DefaultSimulationFactory())
            .Run(cfg, badPct, deadShare: 0.6, hotFactor: 5.0, photonBudget: 400_000.0, repeats: 30, seeds: 6);

        Assert.Equal(2, rows.Length);
        var clean = rows[0];
        var bad = rows[1];

        // A defect-free array: raw == repaired (nothing to fix).
        Assert.Equal(clean.RmsRawMm, clean.RmsCorrMm, 3);
        // 8% bad pixels degrade the raw decode, and the repair recovers most of it (back near the clean floor).
        Assert.True(bad.RmsRawMm > clean.RmsRawMm * 1.3, $"defects should degrade raw RMS: {bad.RmsRawMm} vs {clean.RmsRawMm}");
        Assert.True(bad.RmsCorrMm < bad.RmsRawMm, $"repair should help: {bad.RmsCorrMm} vs {bad.RmsRawMm}");
        Assert.True(bad.RmsCorrMm < clean.RmsRawMm * 1.4, $"repaired RMS should return near the floor: {bad.RmsCorrMm}");
    }
}
