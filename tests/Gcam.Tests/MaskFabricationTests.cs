using Gcam.Configuration;
using Gcam.Core;
using Gcam.Masks;
using Gcam.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace Gcam.Tests;

/// <summary>
/// Mask fabrication tolerances: a real tungsten mask is not the ideal MURA the decoder assumes (mis-placed /
/// mis-sized holes, blocked cells, depth drill wander through a thick slab). The errors live on the mask's
/// Transmit only, so a mis-machined mask is a forward-model mismatch — imaging degrades with the tolerance.
/// </summary>
public class MaskFabricationTests(ITestOutputHelper output)
{
    // --- MaskFabrication model ---

    [Fact]
    public void SameSeed_IsDeterministic()
    {
        var a = new MaskFabrication(14, 14, 1.0, 0.71, 0.05, 0.03, 0.02, 0.1, seed: 5);
        var b = new MaskFabrication(14, 14, 1.0, 0.71, 0.05, 0.03, 0.02, 0.1, seed: 5);
        for (int y = 0; y < 14; y++)
            for (int x = 0; x < 14; x++)
                Assert.Equal(a.Blocked(x, y), b.Blocked(x, y));
        Assert.Equal(a.Hole(3, 4, 0.2).cx0, b.Hole(3, 4, 0.2).cx0, 12);
    }

    [Fact]
    public void Wander_DriftsTheHoleCentreWithDepth()
    {
        var f = new MaskFabrication(14, 14, 1.0, 0.71, 0.0, 0.0, 0.0, wanderMm: 0.2, seed: 3);
        var front = f.Hole(6, 6, 0.0);
        var mid = f.Hole(6, 6, 0.5);
        var back = f.Hole(6, 6, 1.0);
        // Mid-plane has no wander offset; front and back are displaced symmetrically about it.
        Assert.True(System.Math.Abs(front.cx0 - back.cx0) > 1e-6, "wander must move the centre through depth");
        Assert.Equal(mid.cx0, 0.5 * (front.cx0 + back.cx0), 9);
    }

    [Fact]
    public void HoleHalfWidth_FollowsNominalFraction()
    {
        var f = new MaskFabrication(4, 4, 1.0, nominalHoleFraction: 0.6, 0.0, 0.0, 0.0, 0.0, seed: 1);
        Assert.Equal(0.3, f.Hole(1, 1, 0.5).hw, 9);   // half of 0.6, no size jitter
    }

    // --- CodedApertureMask with fabrication: blocked cells cut open-channel transmission ---

    [Fact]
    public void BlockedCells_ReduceTransmission()
    {
        var pattern = MuraGenerator.Mosaic(7, 2, 2);
        double pitch = 1.0, thick = 10.0, mu = 0.178;
        var ideal = new CodedApertureMask(pattern, 0.0, pitch, thick, mu, holeFraction: 0.71);
        var blockedFab = new MaskFabrication(pattern.Width, pattern.Height, pitch, 0.71,
            positionJitterMm: 0.0, sizeJitterMm: 0.0, blockedProbability: 0.5, wanderMm: 0.0, seed: 2);
        var blocked = new CodedApertureMask(pattern, 0.0, pitch, thick, mu, holeFraction: 0.71, fabrication: blockedFab);

        int idealPass = CountStraightTransmission(ideal, pattern, pitch);
        int blockedPass = CountStraightTransmission(blocked, pattern, pitch);
        // 50% blocked OPEN cells cut the open-channel counts, but closed-cell leakage (17% through 10 mm W @662)
        // dilutes the total — so expect a clear reduction, not a full halving.
        Assert.True(blockedPass < idealPass * 0.85, $"50% blocked cells should cut open transmission: {blockedPass} vs {idealPass}");
    }

    // Fire a grid of straight-down rays across the mask face, count how many pass (662 keV, deterministic RNG).
    private static int CountStraightTransmission(CodedApertureMask mask, MaskPattern pattern, double pitch)
    {
        var rng = new DefaultRandom(999);
        double halfW = pattern.Width * pitch / 2.0, halfH = pattern.Height * pitch / 2.0;
        int pass = 0;
        for (int i = 0; i < 60; i++)
            for (int j = 0; j < 60; j++)
            {
                double x = -halfW + (i + 0.5) / 60.0 * pattern.Width * pitch;
                double y = -halfH + (j + 0.5) / 60.0 * pattern.Height * pitch;
                var ray = new Ray(new Vector3(x, y, 20.0), new Vector3(0, 0, -1));
                if (mask.Transmit(ray, 661.7, rng)) pass++;
            }
        return pass;
    }

    // --- Study: loosening the tolerance degrades localization (vs the ideal decoder) ---

    // Multi-seed statistical test (TODO-26). The study's RMS at one seed is too noisy to carry the claim: over 64 seeds
    // RMS(160 µm) − RMS(0) has mean 1.59 mm, sd 0.99 mm (seeds 2001–2064) and 1.99 / 0.99 mm (seeds 5001–5064), and it
    // is negative in 3 / 64 — the earlier single-seed assertions (factor 1.5, then direction) were lucky-seed pins
    // (seed 12345 gave RMS 3.51 → 5.89 mm, ratio 1.68). Design: N = 24 independent transport seeds, paired difference
    // d_i = RMS_i(160) − RMS_i(0) at the study's own settings (30 repeats, 3 fabricated masks), assert
    // t = mean(d) / (sd(d)/√N) > k = 3. With the conservative measured effect size δ/σ = 1.61 the noncentral-t
    // false-fail probability P(t < 3 | ν = 23, ncp = 1.61·√24) is 3.9·10⁻⁶ (< 10⁻⁴); with no degradation at all the
    // test would pass with probability 3.2·10⁻³. Cost ≈ 2.6 core-s per seed; the seeds run in parallel.
    [Fact]
    public void LooserTolerance_DegradesLocalization()
    {
        const int n = 24, firstSeed = 7001;
        const double k = 3.0;
        var r0 = new double[n]; var r1 = new double[n]; var e1 = new double[n]; var p0 = new double[n]; var p1 = new double[n];
        Parallel.For(0, n, i =>
        {
            var rows = new MaskFabricationStudy(new DefaultSimulationFactory())
                .Run(new SimulationConfig { PhotonCount = 400_000, Seed = firstSeed + i }, [0.0, 160.0],
                     photonBudget: 400_000.0, repeats: 30, fabSeeds: 3);
            Assert.Equal(2, rows.Length);
            Assert.Equal(1.0, rows[0].EfficiencyRel, 3);                    // σ=0 is the reference
            r0[i] = rows[0].RmsMm; r1[i] = rows[1].RmsMm; e1[i] = rows[1].EfficiencyRel;
            p0[i] = rows[0].Psr; p1[i] = rows[1].Psr;
        });

        var d = r1.Zip(r0, (a, b) => a - b).ToArray();
        double mean = d.Average(), sd = Math.Sqrt(d.Sum(x => (x - mean) * (x - mean)) / (n - 1)), se = sd / Math.Sqrt(n);
        output.WriteLine($"N={n}: RMS(0) {r0.Average():F3} mm, RMS(160) {r1.Average():F3} mm; d mean {mean:F3} ± {se:F3} (sd {sd:F3}), t = {mean / se:F2} (k = {k}); efficiency {e1.Average():F4}; PSR {p0.Average():F3} → {p1.Average():F3}");
        Assert.True(mean > k * se, $"σ=160µm should scatter localization: mean ΔRMS {mean:F3} mm, SE {se:F3} mm, t = {mean / se:F2} ≤ {k}");
        Assert.True(e1.Average() < 0.97, $"blocked/shrunken holes should cut efficiency: {e1.Average()}");
        Assert.True(p1.Average() < p0.Average(), "coded fidelity (PSR) should fall as the shadow blurs");
    }
}
