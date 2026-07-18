using Genoray.MonteCarlo.Configuration;
using Genoray.MonteCarlo.Core;
using Genoray.MonteCarlo.Detector;
using Genoray.MonteCarlo.Masks;

namespace Genoray.MonteCarlo.Simulation;

/// <summary>One decode variant: the coded-image contrast and localization it yields.</summary>
public sealed record MaskScatterRow(
    string Variant,              // "primary" | "primary+scatter" | "primary+scatter+window"
    double ContaminationPct,     // scatter counts folded in, as % of the coded primary flux
    double Confidence,           // decoder peak/secondary ratio — a coded-image contrast (PSR) proxy
    double LocalizationBiasMm);  // |decoded − true| on the source plane

/// <summary>
/// Folds MASK FORWARD-SCATTER into the coded image and measures what it does to reconstruction — the imaging
/// consequence of the theme-41 secondaries, which that study only tallied as an escape spectrum. A primary that hits
/// a CLOSED tungsten cell does not just vanish: it can Compton-scatter FORWARD (lower energy, small angle) and reach
/// the detector, landing as a blurred, mostly-uncoded pedestal/halo AROUND the true shadow. That contamination is not
/// in the decoder's forward model, so it lowers the coded-image contrast (peak-to-sidelobe) and can bias localization.
///
/// The study builds the flood by real photon transport (biased point source → mask cell → primary OR a transported
/// mask-scatter photon via <see cref="MaskSecondary"/>), then decodes THREE variants — primary only, primary+scatter,
/// and primary+scatter after an ARRIVING-ENERGY window (a proxy for the detector photopeak window) — so it shows both
/// the degradation and how much the window (which rejects the down-shifted scatter) recovers. The residual in-window,
/// small-angle forward scatter is the irreducible part (theme 41's honest point).
///
/// Scope: self-contained (it does NOT change the main pipeline's mask <see cref="CodedApertureMask"/> Transmit). The
/// mask is a SIMPLIFIED ideal slab — a single mid-plane cell lookup (no hole-fraction / taper / focus / fabrication /
/// alignment / oblique channel clipping) — valid for the default straight full-cell mask. Deposits go straight into
/// the flood (no crystal stopping-power / resolution), and the window is a cut on the ARRIVING photon energy, not a
/// measured detector-energy window. The source proposal covers the whole-mask back-projection so the mask-scatter
/// contamination is unbiased (not just primaries whose straight path lands on the detector).
/// </summary>
public sealed class MaskScatterStudy
{
    private readonly ISimulationFactory _factory;

    public MaskScatterStudy(ISimulationFactory? factory = null)
    {
        _factory = factory ?? new DefaultSimulationFactory();
    }

    public (MaskScatterRow[] rows, double[] scatterSpectrum, double specBinKeV, double specMaxKeV) Run(
        SimulationConfig config, double sourceXMm, double sourceYMm, long photonCount,
        double windowLoKeV, double windowHiKeV, int? seed = null, int specBins = 140, double specMaxKeV = 700.0)
    {
        var m = config.Mask;
        var pattern = MuraGenerator.Mosaic(m.Rank, m.MosaicX, m.MosaicY);
        double pitch = m.CellPitchMm;
        double maskHalfW = pattern.Width * pitch / 2.0, maskHalfH = pattern.Height * pitch / 2.0;
        double maskZ = config.Geometry.MaskDetectorDistanceMm;
        double sourceZ = maskZ + config.Geometry.SourceMaskDistanceMm;
        double t = m.ThicknessMm;
        double mu662 = m.LinearAttenuationPerMm;
        double primaryE = config.Source.EnergyKeV > 0 ? config.Source.EnergyKeV : 661.7;
        double mu0 = mu662 * MaskSecondary.MuRel(primaryE);

        int W = config.Detector.PixelsX, H = config.Detector.PixelsY;
        double detPitch = config.Detector.PixelPitchMm;
        double detHalfW = W * detPitch / 2.0, detHalfH = H * detPitch / 2.0;

        var src = new Vector3(sourceXMm, sourceYMm, sourceZ);
        var sec = new MaskSecondary();
        var rng = new DefaultRandom(seed ?? config.Seed ?? 0);

        // Source-support fix (Codex theme-45 review): the biased proposal must cover EVERY closed-cell interaction
        // that can scatter onto the detector — including primaries whose straight path would MISS the detector but
        // that hit a closed cell and scatter in. Aim over the detector-plane region that back-projects across the
        // WHOLE mask (aim = maskHalf × sourceZ/sourceMaskDist), so the mask-scatter numerator is unbiased, not just
        // the primaries whose straight path lands on the detector. Off-mask rays are the surrounding shield (skipped).
        double aimScale = sourceZ / config.Geometry.SourceMaskDistanceMm;
        double aimHalfW = maskHalfW * aimScale, aimHalfH = maskHalfH * aimScale;
        double norm = (2.0 * aimHalfW) * (2.0 * aimHalfH) / (4.0 * Math.PI);

        var fPrimary = new DetectorImage(W, H);
        var fScatterAll = new DetectorImage(W, H);
        var fScatterWin = new DetectorImage(W, H);
        var spectrum = new double[specBins];
        double specBinKeV = specMaxKeV / specBins;
        double primW = 0.0, scatW = 0.0, scatWinW = 0.0;

        for (long n = 0; n < photonCount; n++)
        {
            // Biased emission over the whole-mask back-projection (see source-support fix above).
            double tx = (rng.NextDouble() * 2.0 - 1.0) * aimHalfW;
            double ty = (rng.NextDouble() * 2.0 - 1.0) * aimHalfH;
            var aim = new Vector3(tx, ty, 0.0);
            var delta = aim - src;
            double r = delta.Length;
            var d = delta * (1.0 / r);
            if (d.Z >= 0.0) continue;
            double weight = norm * Math.Abs(d.Z) / (r * r);

            // Which mask cell does the ray cross (at the slab mid-plane)?
            var maskHit = src + d * ((maskZ - sourceZ) / d.Z);
            double u = maskHit.X + maskHalfW, v = maskHit.Y + maskHalfH;
            if (u < 0.0 || v < 0.0 || u >= 2.0 * maskHalfW || v >= 2.0 * maskHalfH) continue;  // off the mask → shield
            int cx = (int)(u / pitch), cy = (int)(v / pitch);
            bool openCell = cx < pattern.Width && cy < pattern.Height && pattern[cx, cy];

            if (openCell) { Deposit(fPrimary, aim, weight, detHalfW, detHalfH, detPitch); primW += weight; continue; }

            // Closed cell: interact in the tungsten, or leak straight through as a primary?
            double slant = t / Math.Abs(d.Z);
            double interactProb = 1.0 - Math.Exp(-mu0 * slant);
            if (rng.NextDouble() >= interactProb)
            {
                Deposit(fPrimary, aim, weight, detHalfW, detHalfH, detPitch); primW += weight; continue;
            }

            // Interaction point along the ray inside the slab (path-length sampled, truncated to the slab).
            double p = -Math.Log(1.0 - rng.NextDouble() * interactProb) / mu0;      // 0..slant
            var frontHit = src + d * ((maskZ + t / 2.0 - sourceZ) / d.Z);
            var interPt = frontHit + d * p;

            var (produced, eSec, newDir, _) = sec.Interact(primaryE, d, rng);
            if (!produced || newDir.Z >= 0.0) continue;                              // absorbed, or not heading forward

            // Self-absorption escaping the back face, then transport to the detector plane.
            double pathOutMask = (interPt.Z - (maskZ - t / 2.0)) / Math.Abs(newDir.Z);
            double escape = Math.Exp(-mu662 * MaskSecondary.MuRel(eSec) * pathOutMask);
            if (rng.NextDouble() >= escape) continue;
            var land = interPt + newDir * ((0.0 - interPt.Z) / newDir.Z);
            if (Math.Abs(land.X) > detHalfW || Math.Abs(land.Y) > detHalfH) continue;

            Deposit(fScatterAll, land, weight, detHalfW, detHalfH, detPitch);
            scatW += weight;
            int sb = (int)(eSec / specBinKeV);
            if (sb >= 0 && sb < specBins) spectrum[sb] += weight;
            if (eSec >= windowLoKeV && eSec <= windowHiKeV)
            {
                Deposit(fScatterWin, land, weight, detHalfW, detHalfH, detPitch);
                scatWinW += weight;
            }
        }

        var decoder = _factory.CreateDecoder(config)!;
        var rows = new[]
        {
            Decode(decoder, fPrimary, null, "primary", 0.0, sourceXMm, sourceYMm, sourceZ),
            Decode(decoder, fPrimary, fScatterAll, "primary+scatter", primW > 0 ? 100.0 * scatW / primW : 0.0,
                   sourceXMm, sourceYMm, sourceZ),
            Decode(decoder, fPrimary, fScatterWin, "primary+scatter+window", primW > 0 ? 100.0 * scatWinW / primW : 0.0,
                   sourceXMm, sourceYMm, sourceZ),
        };
        return (rows, spectrum, specBinKeV, specMaxKeV);
    }

    private static MaskScatterRow Decode(IDecoder decoder, DetectorImage primary, DetectorImage? add, string variant,
                                         double contaminationPct, double srcX, double srcY, double srcZ)
    {
        DetectorImage img = primary;
        if (add != null)
        {
            img = new DetectorImage(primary.Width, primary.Height);
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                    img[x, y] = primary[x, y] + add[x, y];
        }
        var res = decoder.Decode(img);
        double bias = Math.Sqrt((res.Estimate.Position.X - srcX) * (res.Estimate.Position.X - srcX) +
                                (res.Estimate.Position.Y - srcY) * (res.Estimate.Position.Y - srcY));
        return new MaskScatterRow(variant, contaminationPct, res.Estimate.Confidence, bias);
    }

    private static void Deposit(DetectorImage img, Vector3 pos, double w, double halfW, double halfH, double pitch)
    {
        int ix = (int)((pos.X + halfW) / pitch);
        int iy = (int)((pos.Y + halfH) / pitch);
        if (ix >= 0 && iy >= 0 && ix < img.Width && iy < img.Height) img.Add(ix, iy, w);
    }

    public static string ToCsv(MaskScatterRow[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("variant,contamination_pct,confidence,localization_bias_mm");
        foreach (var r in rows)
            sb.AppendLine($"{r.Variant},{r.ContaminationPct:F3},{r.Confidence:F4},{r.LocalizationBiasMm:F4}");
        return sb.ToString();
    }
}
