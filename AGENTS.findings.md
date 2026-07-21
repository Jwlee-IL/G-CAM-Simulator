# AGENTS.findings.md — Simulation results, by theme

The quantitative results GCAM has produced, grouped by theme. Each item lists the
finding, how to reproduce it (CLI command / script), and the artifact it wrote to
`samples/` (or `rtl/`). See `AGENTS.md` for architecture and conventions.

Baseline scenario unless noted: Cs-137, rank-7 MURA (2×2 mosaic), 10 mm tungsten,
12×12 / 1 mm detector, D = 60 mm (mask–detector), S = 100 mm (source–mask).

---

## 1. Geometry & source localization
- Distance fall-off (1/r²), near-field magnification, and off-axis shadow shift are all
  **emergent from the geometry** — none are coded explicitly; they appear once the ray↔plane
  intersections run.
- Localization accuracy: **centered ≈ 0.5 mm**; off-axis inside the FCFOV tracks to sub-mm
  (6 mm → 5.8 mm, 0.4 mm error).
- **FCFOV = one cyclic period** = `rank · cellPitch · (D+S)/D`; for the baseline, ±9.3 mm.
- Reproduce: `montecarlo samples/scenario.json` (and `scenario_offaxis.json`). Sweep →
  `samples/fcfov_map.png`.

## 2. Decoding & ghost artifacts
- Off-axis source **outside the FCFOV** aliases to a **ghost on the opposite side**
  (12 mm → estimate −6.6 mm; matches `12 − period(18.7) = −6.7`). Classic partial-coding artifact.
- **Cyclic vs non-cyclic decoding**: over a wide search grid, non-cyclic (finite-mask)
  decoding roughly **doubles** the correctly-localized area (cyclic ~150–170 / 625,
  non-cyclic ~280–336 / 625). Toggle: `Decoder.Cyclic`.
- Reproduce: `montecarlo sweep samples/scenario.json` → `samples/cyclic_vs_noncyclic.png`,
  `sweep_cyclic.csv`, `sweep_noncyclic.csv`.

## 3. Configuration optimization (maximise FOV)
- **Fundamental law: FOV ÷ resolution = rank.** Widening FOV via cell pitch or D coarsens
  resolution by the same factor; only a **larger rank** buys FOV for free.
- **Max-FOV optimum** (12×12/1 mm fixed, S = 100 mm): large rank, small D, pitch ≤ 1.5.
  Robust pick **rank 23 / pitch 1.0 / D 20 mm → usable ±64 mm (96%)**, ~7× the original
  rank-7/D-60 (±9.3 mm).
- **Collapse cliff**: decoding fails when the detector can't hold ~one basic period of the
  shadow — `pitch 2.0` collapses at rank ≥ 17 / D 20 (usable 22–44%).
- Reproduce: `montecarlo scan samples/scenario.json` → `samples/scan_pareto.png`,
  `scan_collapse.png`, `scan.csv` / `scan_extended.csv`.

## 4. Mask design — tungsten thickness
- Mask is a **ray-marched 3-D slab** (collimation modelled). **Optimum ≈ 8–10 mm.**
- Thin (< ~7 mm): closed-cell leak > 35 % → coding contrast collapses → usable FOV → 0.
- Thick: no FOV gain, and off-axis efficiency falls via open-channel **collimation**
  (edge/center efficiency 0.56 @ 8 mm → 0.25 @ 32 mm). Original 10 mm was near-optimal.
- Reproduce: `montecarlo thickness samples/scenario.json` → `samples/thickness_opt.png`.

## 5. Mask / antimask (two-exposure technique)
- **Redundant with no background** (identical to single mask), **essential once additive
  background is significant**: at 8 counts/px background (source ≈ 2.8/px) single-mask fails
  32 % while mask/antimask stays sub-mm. Crossover ≈ 1–2 counts/px. Structured (gradient)
  background hurts single-mask more.
- Caveat the sim exposed: the textbook "balanced MURA rejects DC → single exposure suffices"
  holds only at low background; the finite near-field candidate-search decoder picks up
  background artifacts (even uniform) that the antimask subtraction cleans.
- **What kind of background matters** (`antimask-scene`): the antimask cancels only
  background that is *diffuse / detector-intrinsic* (uncoded, common to both exposures) —
  it reconstructs one clean peak. A **directional background source** is coded by the mask
  just like the signal, so it survives the difference and is **imaged as a second peak**
  (recon @ the bg source = 227 vs the primary 263). So antimask removes ambient/diffuse
  background but NOT a co-located interfering source (need collimation / energy separation).
  Energy windowing is the orthogonal axis: it fails when the background is at the same
  energy (662 keV), which is exactly when the spatial antimask is the only lever left.
- **Do you need to rotate the mask? (commercialization).** Three methods vs background:
  (a) software flip only [balanced decode, no bg handling] collapses (4.9 mm / 32 % fail at
  8 counts/px); (b) flip + **calibrated background subtraction** (single exposure, no moving
  part) and (c) **physical two-exposure** antimask are **identical** (max diff 0.07 mm across
  all levels). So the mechanical mask rotation is unnecessary: a single coded exposure +
  a calibrated background subtraction matches the two-exposure. Caveat: (b) assumes the
  background is static/measurable; time-varying background favours (c)'s concurrent
  subtraction — but even then use a *static dual* A/Ā arrangement, never a moving part.
- Reproduce: `montecarlo antimask samples/scenario.json` → `samples/antimask.png`,
  `antimask_methods.png`; `montecarlo antimask-scene samples/scenario.json` →
  `samples/antimask_scene.png`.

## 6. Crystal material
- **Detection efficiency tracks density** (1−exp(−μ·depth), 10 mm): NaI 0.67e-4 < CeBr3/LaBr3
  0.87e-4 < GAGG 1.08e-4 < LYSO 1.15e-4 < BGO 1.17e-4.
- **GAGG's weakness is not efficiency** (dense) but poor resolution + high afterglow. Upgrades:
  **GAGG:Ce,Mg** (co-doped, fixes afterglow, keeps ruggedness/no-HV/no-intrinsic) or **CeBr3**
  (fast, low afterglow, ~4 % res; hygroscopic + pricey). Avoid LYSO for weak sources (Lu-176
  intrinsic background).
- Reproduce: presets in `samples/materials/*.json`; `samples/crystal_materials.png`.

## 7. Detector array (sampling)
- The mask-cell shadow must span **≳ 1 detector pixel** (Nyquist). Below that (6×6, 0.8 px/cell)
  it aliases and localization collapses (21 % failure). **~2 px/cell (16×16) is the sweet spot**
  (sub-mm); finer only marginally helps. Original 12×12 (1.6 px/cell) is adequate; 16×16 ≈ halves
  the error.
- Reproduce: `montecarlo array samples/scenario.json` → `samples/array_sampling.png`.

## 8. Crystal uniformity — position robust, energy fragile
- **Position (coded-aperture) is robust**: random per-pixel gain averages over the 144 pixels;
  even a 120 % gain gradient barely shifts the correlation argmax (balanced MURA strongly
  *suppresses sensitivity to* smooth multiplicative distortion — finite/discrete geometry can
  still leak some low-frequency gain structure) — unlike Anger-logic centroiding. Naive flood
  correction can *hurt* photon-starved images (amplifies Poisson noise in low-gain pixels).
  Scope: shown for a centered single source with one fixed uniformity seed (not an ensemble).
- **Energy (pulse-height) is NOT robust**: per-crystal gain scatter smears the aggregate
  photopeak (7.4 % → 18 % at 15 % gain σ); per-channel gain calibration restores it.
- Reproduce: `montecarlo uniformity samples/scenario.json` → `samples/uniformity_robustness.png`;
  RTL `rtl/pixel_uniformity_study.py` → `rtl/pixel_uniformity.png`.

## 9. Statistics & variance reduction
- **Count threshold**: below ~25 detected counts localization collapses; by ~50 it's solid
  (sub-mm). A ~0.55 mm floor above that is set by the decoder grid, not noise. FCFOV-edge sources
  need more counts. At 1 MBq, ~50 counts ≈ 0.3 s of acquisition.
- **Directional biasing** (detector-area importance sampling): unbiased vs 4π (efficiency matches
  within ~1 %) with ~100× fewer photons — sweeps/scans run in seconds.
- Reproduce: `montecarlo noise samples/scenario.json` → `samples/noise_study.png`.

## 10. RTL front-end (energy & rate) — `rtl/`
- The SystemVerilog peak detector (Icarus) **reconstructs the Cs-137 spectrum**: 662 keV
  photopeak at ~7 % FWHM at low rate; **pile-up** drops efficiency to ~44 % at 1.5 Mcps.
  (`rtl/run.sh` → `rtl/rtl_lowrate.png`, `rtl_highrate.png`.)
- **Material decay time → rate capability**: CeBr3 (17 ns) holds 87 % at 2 Mcps; **GAGG collapses
  to 0.2 % at 1 Mcps** (afterglow buries the baseline) while GAGG:Mg tracks its 55 ns decay.
  Quantifies why plain GAGG is rate-limited. (`rtl/material_rate_study.py` → `rtl/material_rate.png`.)
- **Per-crystal gain non-uniformity** broadens the aggregate photopeak; per-channel calibration
  restores it (see theme 8). (`rtl/pixel_uniformity_study.py`.)

## 11. SiPM + ADC front-end characteristics — `rtl/frontend_model.py`
- Energy resolution is a **photoelectron budget**: `N_pe = lightYield · E · collection · PDE`,
  `R_stat(FWHM%) = 235.5·sqrt(ENF/N_pe)`, `R_total = sqrt(R_stat² + R_intrinsic²)`.
- **At typical MPPC PDE (~45%) most crystals show diminishing energy-resolution returns**
  (intrinsic non-proportionality comparable to or above photon statistics: GAGG 2.7% stat
  vs 5.0% intrinsic, LYSO/NaI similar). **Not universal** — LaBr3 stays photon-limited
  (2.4% vs 2.2%) and CeBr3/BGO are only marginal, so for those more PDE still helps. Higher
  PDE also still matters for timing, threshold, poor light collection, or spectral mismatch.
  Verdict: don't over-spec PDE for *resolution* on the crystal-limited crystals; crystal
  choice + spectral match matter more there.
- **Optimal specs**: PDE ~40–50% matched to the crystal's emission wavelength (GAGG 520 nm
  green vs blue-optimized SiPM = wasted PDE); mid microcell (35–50 µm; larger = higher PDE
  but less dynamic range); low DCR / cooling only for weak sources + long integration (DCR
  is a *diffuse* background → theme-5 calibration handles the mean); per-channel gain/bias
  calibration (144 channels → theme-8 energy fragility); ADC 12–14 bit (quantization
  negligible) with sample rate matched to the crystal decay (fast CeBr3 ≥ 200 MHz,
  slow GAGG 50–100 MHz).
- Reproduce: `python rtl/frontend_model.py` → `rtl/frontend_resolution.png`.

## 12. Front-end energy trust — peak-hold failure modes (Phase A) — `rtl/ballistic_deficit_study.py`
Groundwork for multi-isotope discrimination: **when can you believe the energy the FPGA
reports?** The simple baseline `peak_detector.sv` (running peak-hold) is trustworthy ONLY
for an isolated, in-range pulse; two things a real (multi-isotope, busy) field guarantees
both break it.
- **Isolated pulses have NO ballistic deficit** (peak-hold captures the true max): measured =
  true within ~0.3 keV up to full scale. *But* dynamic range bites — with the gain set so
  Cs-137 662 keV = 3000 counts, full scale (4095) is only **~859 keV**, so **Co-60 1173 &
  1332 keV both clip to a single false ~859 keV edge**. Fix is simply to set the gain to the
  highest expected line (see theme 14 — the low-line penalty is negligible at 12-bit), not the
  primary source.
- **Overlap = the "ballistic deficit" that bit the FPGA.** Two pulses closer than the crystal's
  resolving time merge into ONE detection → a lost count **plus** an inflated amplitude (the max
  of the *superposed* waveform, up to ~2× a single pulse — not a clean charge sum) = a
  **fake higher-energy line**. Resolving time scales with decay:
  **CeBr3 (17 ns) ≈ 80 ns, GAGG (90 ns) ≈ 400 ns** — the slow crystal merges over a 5× wider
  window. (Ties theme 10: fast crystals aren't just higher-rate, their energy is
  trustworthy to higher rate.)
- **Rate drifts the truth** (GAGG:Mg 55 ns pulse model): 50 k→2 M cps shifts the 662 keV centroid
  +5.5 keV (662→667.5) and grows a fake sum-tail above 720 keV from **0.8 % → 20.4 %** — at 2 Mcps
  a fifth of *reported detections* are pile-up artifacts, indistinguishable from a real high-energy
  line by energy alone.
- **Consequence**: energy-window isotope separation is only as good as these limits → motivates
  Phase A2 (trapezoidal shaping + flat-top charge integration for better noise/no shaper
  deficit, **pile-up rejection** to discard merged events instead of mis-measuring them, and a
  gain/full-scale choice for the whole isotope span). Then Phase B (multi-isotope) can stand.
- Reproduce: `python rtl/ballistic_deficit_study.py` → `rtl/ballistic_deficit.png`,
  `ballistic_deficit.csv`.

## 13. Front-end fix — charge integration + pile-up rejection (Phase A2) — `rtl/integrating_peak_detector.sv`
The real system *measured every pulse* → contaminated energy at rate (theme 12). The fix
(`integrating_peak_detector.sv`, driven by `shaping_pileup_study.py`) integrates CHARGE over a
runtime `window` (= shaping time) and **rejects** piled-up events instead of mis-measuring them.
- **Naive integration (no rejection) is WORSE than peak-hold**: when an overlapping pulse lands
  inside the window its charge *adds fully* (peaks don't), so counting *all* integrator events
  gives a much higher fake sum-tail than peak-hold (measured: **28 % naive-integrate vs 9 %
  peak-hold vs 4 % with rejection** at 1 Mcps).
  What saves it is a **fast derivative (slope-over-N-samples) arrival detector** that catches a 2nd
  pulse *riding on the first's tail* — the case a slow crystal hides from a level/edge detector
  because the signal never dips back below threshold.
- **Rejection ~halves the fake sum-tail** (>720 keV) vs measure-everything peak-hold: 9.1 %→4.2 %
  at 1 Mcps, 17.1 %→7.7 % at 2 Mcps. The residual is **sub-resolving-time random overlaps** the
  fast channel can't separate (this study injects Poisson pile-up only — no cascade truth; the
  genuine cascade-vs-random distinction is theme 14).
- **Cost is throughput** (accepted / true): at 500 kcps, CeBr3 (200 ns window) keeps **87 %**,
  GAGG (500 ns) **72 %**. Rejected events are lost counts → pushes back on crystal rate capability
  (theme 10) and acquisition time (theme 9).
- **Shaping–window tradeoff is crystal-dependent**: a **fast** crystal (CeBr3) is optimal at a
  *short* window — best resolution AND highest throughput (top-left) — because a longer window only
  integrates more baseline noise. A **slow** crystal (GAGG) needs window ≳ 5×decay (~500 ns) to
  collect the charge; below that it either ballistic-deficits (FWHM 7 %→9.3 % at 300 ns) or rejects
  nearly everything (window < pulse length → "not returned" fires). So fast crystals win *twice*:
  rate capability and trustworthy energy at rate. Match the window to the decay.
- Reproduce: `python rtl/shaping_pileup_study.py` → `rtl/shaping_pileup.png`, `shaping_pileup.csv`.

## 14. Multi-isotope discrimination + ADC dynamic range (Phase B) — `rtl/multi_isotope_study.py`
Drives the A2 charge-integrating + reject front-end with a **Cs-137 + Co-60 + Co-57** field (isotope
presets in `samples/isotopes/*.json`: multi-line + `cascadeCoincident` flag), each line carrying its
**Klein-Nishina Compton continuum**, and reconstructs the spectrum.
- **Dynamic range — set the gain to the HIGHEST line, not the primary source.** With the gain tuned
  to Cs-137 (662→3000, full scale ~859 keV) **Co-60 saturates** into an off-scale smear — the real
  system's mistake. Re-tuned so 1332 keV sits under full scale, **Co-60 1173/1332 resolve AND the
  122 keV line is unhurt** (FWHM 15.8 %→16.2 %, i.e. no penalty — the 122 keV width is set by intrinsic
  ~1/√E statistics, not the gain). So at **12-bit a single gain covers the whole 122→1332 keV span**;
  dual-gain / companding is only needed with fewer ADC bits or a large fixed electronic-noise floor
  (then the low line loses SNR at low gain). Corrects theme 12's "forces dual-gain" note.
- **Compton continuum raises the background under low lines** (not full burial at comparable
  activities): the 122 keV Co-57 peak sits on the Compton continua of 662 / Co-60, worsening its
  peak-to-background — energy windowing on a low line is contaminated by every higher line's downscatter.
- **Cascade vs pile-up sum — rate scaling tells them apart** (`cascade_pileup.png`). Co-60's two
  gammas are one decay → true-coincidence (cascade) **sum events** are **rate-LINEAR** (slope ≈ 1);
  two gammas from *different* decays in one window are random pile-up, **rate-QUADRATIC** (slope ≈
  1.6–2, flattening at high rate as the detector dead-times). The **slope difference is the robust
  result**; the crossover (~200 k/s here) is only qualitative — `ACTIV` is a detected-event-rate
  proxy (the P_BOTH model gives no zero-detection decays) and the same-100 MHz-sample cascade/random
  classification is approximate. These are multi-deposit *events*, not a resolved sum peak (the 2505
  keV Co-60 sum saturates the ADC). Still: it answers the FPGA-era mystery — a fake high-energy sum
  that grows linearly is a real coincidence, one that grows quadratically is pile-up.
- Reproduce: `python rtl/multi_isotope_study.py` → `rtl/multi_isotope.png`, `cascade_pileup.png`.
  Open: fold the multi-line isotope model into C# `SourceConfig` (for coded-aperture multi-source
  imaging).

## 15. Crystal Compton scattering — positioning strategy + multi-isotope spatial separation — `ComptonModel`/`ComptonCrystalDetector`
Crystal-internal Compton is now modelled in the C# coded-aperture pipeline: `ComptonModel` (Kahn
exact Klein-Nishina sampler, energy-dependent photoelectric fraction and attenuation) +
`ComptonCrystalDetector` (tracks the multi-pixel cascade, then applies a positioning strategy).
Run: `montecarlo compton samples/scenario.json` → `samples/compton_strategies.png`,
`compton_contamination.png` (plot via `samples/plot_compton.py`).
- **Multi-pixel positioning — there WAS a better way than the old rig's per-pixel LLD/ULD window.**
  A 662 keV event usually Compton-scatters (electron at A, scattered photon re-absorbs at B or
  escapes), splitting the energy. The old method (per-crystal energy window on *each* pixel) passes
  only single-site full-energy events → clean position but **24 %** efficiency. **Argmax** (window on
  the *total* deposit, count at the largest-deposit pixel) recovers the Compton-split full-energy
  events → **38 %** efficiency (+58 % counts) at *better* localization (RMS 0.74 vs 0.78 mm), closest
  to the ideal no-Compton detector. Anti-coincidence ≈ per-pixel (24 %, worse RMS 0.98); Centroid
  recovers counts but blurs toward B (1.04 mm). Verdict (this scenario/model): **prefer Argmax**
  (total-energy window + max-deposit pixel) to per-pixel windowing — same clean position, ~1.6× the counts.
- **The old unsolved problem — a "valid" count that is really another isotope — is solvable by the
  coded aperture.** A Co-60 source downscatters in the crystal into the Cs-137 662 keV window
  (**19 %** of that window's counts *in this equal-budget demo* — not activity-normalized); an energy
  window *cannot* remove it (the continuum is broadband). But those counts are still **coded from
  Co-60's direction**, so decoding the 662-window flood map images the contamination **at the Co-60
  position, not the Cs-137 one** — the two sources separate *spatially* (locked in by `ComptonTests`).
  Energy window + coded decode together do what neither can alone; spectral Compton-stripping (theme 14
  continuum shapes) is the complementary lever. Caveat: the mask uses a single (Cs-137) attenuation
  for all energies, so Co-60's higher mask leakage/contrast is not energy-correct — the *spatial
  separation* is qualitatively robust, the exact contamination fraction is not.
- Reproduce: `montecarlo compton samples/scenario.json`; tests in `ComptonTests` (KN energy
  conservation, photofraction monotonicity, Argmax > per-pixel efficiency, contaminant-at-its-source).

## 16. Compton stripping — the spectral complement to spatial separation — `rtl/compton_stripping_study.py`
Theme 15 separated the multi-isotope contamination *spatially* (coded decode → contaminant imaged at
its own position). **Compton stripping** is the *spectral* lever for the same problem: it recovers the
net COUNT (sensitivity) that a raw energy window over-reports. Top-down from the highest line, estimate
each photopeak's net area, model its Klein-Nishina continuum (net × (1−pf)/pf × KN shape), and subtract
it from all lower channels.
- **It works well for well-separated lines.** The Cs-137 662 keV window sits under Co-60's two continua:
  raw window over-counts Cs-137 by **+39 %**; stripping recovers it to **−5 %**. Co-60 1173 keV: **+40 %
  → +6 %**. So the "high sensitivity but the counts are another isotope's downscatter" problem is fixed
  in the *count* domain for separated lines.
- **But error accumulates top-down.** A close doublet leaks peak-to-peak (Co-60 1332 keV picks up +15 %
  from the 1173 keV tail — not a continuum, so stripping can't touch it), and that over-estimate
  propagates down, **over-correcting the lowest line** (Co-57 122 keV: +16 % → −26 %). Low-energy
  window capture (~85 % at 122 keV) and baseline-under-peak also bite.
- **Complementary to the spatial lever, which is more robust for the hard cases.** Stripping recovers
  *how many* (counts) but degrades through close/cascade lines and extreme dynamic range; the coded-
  aperture spatial separation (theme 15) recovers *where* and doesn't care about energy overlap (it
  separates by direction). Use both: strip to quantify well-separated isotopes, decode to place and
  disentangle the overlapping/co-located ones.
- Reproduce: `python rtl/compton_stripping_study.py` → `rtl/compton_stripping.png`.

## 17. Combined lever — per-pixel Compton stripping inside the coded pipeline — `ComptonStudy.RunStripping`
Themes 15 (spatial) and 16 (spectral) joined: strip the Co-60 downscatter out of the Cs-137 662 keV
window **per pixel** (subtract `R × the pixel's Co-60-photopeak count`, R = the aggregate
downscatter-into-662 / Co-photopeak ratio ≈ 0.15), **then decode**. Because the Co downscatter is coded
from Co-60's direction just like its photopeak, the subtraction is applied through the Co photopeak IMAGE,
so it removes the contamination *where it actually landed*. Run: `montecarlo compton-strip samples/scenario.json`
(Co-60 ×8 = a stronger dominant source, so the contamination is visible not just countable) →
`samples/compton_strip_combined.png` (plot via `plot_compton_strip.py`).
- **Recovers the true Cs-137 net count to ~0 %** from a **+74–76 %** raw-window over-count, in both
  geometries.
- **Separated sources**: the raw 662 reconstruction shows Cs *and a bright Co-60 ghost*; stripping
  removes the ghost, leaving the clean Cs image.
- **Co-located sources** — the case the spatial decode ALONE cannot separate (theme 15's stated
  limit): raw 662 is one peak inflated +76 %; per-pixel stripping brings it back to the true Cs count.
  So the spectral + spatial levers *together* recover even two isotopes at the same position.
- Locked in by `ComptonTests.Stripping_RecoversCsCount_EvenCoLocated`.
- Reproduce: `montecarlo compton-strip samples/scenario.json`.
- **Codex caveat (round 4)** — this is NOT blind, model-free separation: `R` is computed from the true
  simulated Co contamination map (`Σ coInto662 / Σ coWindow`) and is a **single GLOBAL scalar applied per
  pixel via the Co-window image** (not a verified per-pixel ratio). The honest claim is: *given a known Co
  line and a calibrated downscatter/photopeak ratio, the Cs count is recovered even when co-located*. The
  spatial matching comes from the Co photopeak image; `R` itself is assumed calibrated. Aggregate count
  recovery can look excellent while local residuals remain (R non-uniformity from edge escape / cascade
  positioning / broad Co window is not checked). Count evidence is printed to the console, not in
  `strip_*.csv` (those hold only the reconstruction grids).

## 18. Source distance (z) estimation by coded-aperture refocusing — `DepthStudy`
The pipeline had assumed the source distance S known; it can be *recovered*. The mask-shadow
magnification **M = (D+S)/S** depends on S, and the decoder back-projects with `frac = D/(D+S)`.
Decoding one flood map at a range of ASSUMED S, the correlation at the (on-axis) source position is
*sharpest near* the true S — the coding and decoding align — and falls off away from it. So the
high-focus region of the assumed-S scan estimates the depth (light-field-style refocusing).
- **Focus metric = the single-point correlation at the known on-axis position** (a 1-cell recon grid),
  which avoids the candidate-position aliasing and grid-scale artifacts that fooled a naive
  peak-height or peak/RMS over a full grid. It is *not* fully artifact-free, though — it is a raw,
  unnormalised cyclic MURA correlation sampled at integer mask cells, so the curve is stair-stepped and
  its bare argmax is biased (e.g. true S=40 peaks at ~35). **Estimate = the centroid of the high-focus
  region** (a heuristic: the curve develops a flat top over the band of unresolvable distances; its
  width IS the depth resolution and its centre is a more robust estimate than the argmax, which snaps
  to the plateau's leading edge). Decoder caveat: the decoder scores against a single thin mask plane,
  so these results are for the straight, full-hole, thin mask — a focused/thick/hole mask would need
  the decoder to model the channel.
- **Near field accurate, far field degrades** (`depth_estimation.png`): S 40→39, 60→60, 100→97,
  150→140, 200→214 mm (error grows −1→+14 mm), and the focus-curve width (depth resolution) grows from
  ~50 mm at S=40 to ~240 mm at S≥150. The **scaling intuition** is dM/dS = −D/S² (a near source's
  magnification changes fast with distance, a far one's barely) — the actual resolution also depends on
  cell pitch, detector footprint, sampling, and statistics.
- Caveat: measured on the high-statistics mean flood map (bias-limited); with Poisson noise the broad
  far-field focus curve makes the far depth much noisier — the plotted error bar is that resolution.
- Reproduce: `montecarlo depth samples/scenario.json` → `samples/depth_estimation.png`, `depth.csv`
  (plot via `plot_depth.py`). Locked in by `DepthTests`.

## 19. Depth under Poisson noise + joint lateral–depth estimation — `DepthStudy.RunNoisyDepth/RunNoisyJoint`
Extends theme 18 from the bias-limited mean map to noisy realizations, and drops the on-axis
assumption. Run: `montecarlo depth-joint samples/scenario.json` → `samples/depth_joint.png`.
- **Depth vs counts (known lateral)**: the near field converges to **sub-mm** (S=60: depth RMS
  3.8→0.3 mm as counts 100→3000), the far field floors at **~10 mm** (S=150: 38→10.5 mm, bias ≈ −10)
  — the broad far-field focus curve makes the depth both bias- and noise-limited, so more counts stop
  helping. Depth is a genuine measurement near, a weak one far.
- **Joint (x, y, z) for an off-axis source**: the **lateral position is recovered to ~mm** (RMS
  0.6–5 mm) *even while the depth stays uncertain* — the coded aperture is a strong lateral localiser
  but a weak rangefinder; x, y and z are not equally constrained. The joint depth carries extra
  **coupling noise** (off-axis near depth RMS ~26 mm vs the known-lateral direct 0.3 mm): a noisy
  lateral estimate feeds the sensitive near-field depth focus, so lateral uncertainty leaks into z.
- Takeaway: report lateral (x,y) with confidence at any distance; treat z as a near-field-only
  estimate whose error grows with distance and with lateral uncertainty.
- Method caveat: the joint estimator is a *simple 3-step alternating iteration* (decode lateral →
  centroid depth → repeat) from one nominal S, with no convergence check, damping, or multi-start — it
  can trap (a wrong assumed S rescales the lateral solution; cyclic decode can pick an alias branch).
  This is why the on-axis cases use the direct depth-at-(0,0) method (RunNoisyDepth), not the joint.
- Reproduce: `montecarlo depth-joint samples/scenario.json` → `samples/depth_joint.png`,
  `depth_joint.csv` (plot via `plot_depth_joint.py`). Locked in by `DepthTests`.

## 20. Mask channel geometry — hole size and focused (converging) channels — `MaskGeometryStudy`
`CodedApertureMask` now models two channel-geometry knobs (`Mask.HoleFraction`, `Mask.FocalDistanceMm`).
Run: `montecarlo maskgeo samples/scenario.json` → `samples/maskgeo.png`.
- **Shrinking the hole below the cell only hurts, for this MURA / full-cell decoder** — it loses BOTH
  sensitivity and localization (efficiency 2.5e-4→0.9e-4, RMS 0.57→7.65 mm as the *linear* hole fraction
  goes 1.0→0.3; note the open AREA is HoleFraction², so 0.3 linear ≈ 9 % open area). The single-pinhole
  intuition (smaller hole = sharper) does NOT transfer: this decoder is matched to full open cells and
  the coded resolution is set by the cell pitch, so a smaller hole just dilutes the open/closed contrast
  and throws away signal. Full-open cells are optimal for this design (not a universal statement about
  intra-cell aperture / MTF).
- **Focused (converging) channels are a focal-POINT concentrator, not a uniform gain.** Angling the
  channels to converge on a point at the focal distance (on-axis) removes the off-axis open-channel
  collimation *there*: at a 25 mm mask it gives **+35 % efficiency at the focal point**. But it is
  depth-selective (the focused/straight ratio peaks at the focal distance, 1.35 @100 mm, and falls to
  <1 by 220 mm) AND laterally selective — off-axis is **worse** (edge/center 0.49 vs straight 0.82),
  because an edge source isn't at the on-axis focal point. So focusing trades FOV *and* depth of field
  for on-axis focal performance; it is marginal at 10 mm (little collimation to remove) and pronounced
  at 25 mm. Buildable via stacked-shifted tungsten laminae (a staircase channel).
- **Verdict**: for a fixed on-axis standoff, focused channels help; for general localisation (wide FOV +
  a depth range) straight channels win — and the straight mask's defocus-with-distance is exactly what
  enables depth estimation (theme 18). The user's "angled channels → denser/sharper pattern everywhere"
  intuition is a pinhole idea that doesn't hold for a coded aperture.
- Reproduce: `montecarlo maskgeo samples/scenario.json` → `samples/maskgeo.png`, `maskgeo.csv`
  (plot via `plot_maskgeo.py`). Locked in by `MaskGeometryTests`.

## 21. The "right size" — cell (feature) size and open fraction — `montecarlo masksize`
Follows theme 20 (which discarded angled channels / sub-cell holes). Two mask *sizes* DO have a real
optimum, unlike the sub-cell hole (theme 20, no interior optimum). Run: `montecarlo masksize` →
`samples/masksize.png`.
- **Cell (feature) size has a BOUNDED sweet spot** (the coded-aperture analog of the optimal pinhole
  diameter). Sweeping cell pitch (fixed rank/detector, fixed counts): localization is sub-mm for pitch
  **~0.5–1.5 mm (cell shadow ~0.8–2.5 detector pixels)**; below it **aliases** (shadow < 1 px → RMS
  climbs), above it the **mask outgrows the detector** so the shadow overflows and too few cells are
  sampled → **collapse** (RMS 8–18 mm at 2–3 mm pitch). So "finer is always better" is wrong: the
  feature size is bounded by Nyquist below and the detector footprint above. Optimum ≈ shadow matches
  the detector pixel (≈ theme 7 from the mask side).
- **Open fraction — ~50 % is optimal in the detector-background-limited regime, and MURA sits there.**
  The mask's coding power scales as **√(ρ(1−ρ))** (peaks at 0.5); the full analytical SNR is
  `Φ_s·√(ρ(1−ρ)) / √((1−ρ)Φ_s + ρΦ_b + B_det)`, so the optimum ρ depends on which noise term dominates
  the denominator: **detector background B_det (ρ-independent) → ρ_opt = 0.5** (a MURA); **aperture-
  transmitted background ρΦ_b → ρ_opt < 0.5** (a more closed mask admits less sky); **source counting
  noise (1−ρ)Φ_s → ρ_opt > 0.5** (less coding needed). MURA's 50 % is optimal for the detector-limited
  case and near-optimal when Φ_b ≈ Φ_s. It is not a free knob anyway: tuning ρ needs a random array,
  which is worse (non-zero sidelobes).
- Verdict: the "size" worth tuning is the **cell pitch** (bounded optimum, shadow ≈ 1–2 px); the open
  fraction is already at its optimum in MURA, and the sub-cell hole (theme 20) should stay full-open.
- Reproduce: `montecarlo masksize samples/scenario.json` → `samples/masksize.png`, `masksize.csv`
  (plot via `plot_masksize.py`; open-fraction panel is the analytical SNR). Locked in by `MaskGeometryTests`.

---

## 22. Productization — a ~3 kg handheld locator: weight, size, form, and field hardening
A whole-instrument design pass under a "3 kg large-flashlight" constraint. Most parts are ANALYTICAL
design models (Python) built on top of the validated MC laws; two are new MC (the recommended-config
validation and `ShieldStudy`). Recommended baseline: **GAGG:Ce,Mg, 16×16 @1 mm, D=55 mm, 15 mm crystal,
rank-7, non-cyclic decode.**
- **Weight is the SHIELD, not the crystal** (`samples/handheld_design_study.py` → `handheld_design.png`).
  At 3 kg the crystal is ~20–40 g and the mask ~40 g; the tungsten shield is ~2/3 of the mass. So the
  crystal is chosen for **density + ruggedness** (GAGG:Ce,Mg: non-hygroscopic, SiPM low-V, dense; BGO only
  to minimise barrel length), NOT weight. Sensitivity ∝ detector area × stopping; a 12→20 mm detector is
  ~2.4–3.7× more sensitive but pushes 2.7→3.4 kg via the shield. **Shield thickness is the master weight
  knob** (6 mm≈1.5 kg … 12 mm≈3 kg). Pixel vs monolithic differ ~30 g — decide on angular resolution
  (pixel cell 2 mm→33 mrad) vs channel/power (monolithic ~30 ch) — not mass.
- **MC validation of the recommended config** (`scenario_handheld.json`; `noise`/`sweep`;
  `plot_handheld_validation.py` → `handheld_validation.png`). vs the original 12×12/D60/GAGG-10 mm:
  **efficiency 2.65e-4 vs 1.08e-4 = 2.45× more sensitive** (matches the design model's 2.37× = area 1.78×
  stopping 1.33×), **resolution floor 0.34 mm vs 0.55 mm**, sub-mm at ≥250 counts centered (~1 s @1 MBq) /
  ≥500 edge. Fair axis is RMS-vs-TIME (efficiency folds in), where it clearly wins. Caveat: cyclic ghost
  margin 1.35 (marginal, D=55 is short) → **run the non-cyclic finite-mask decoder** (sweep localized
  360/625 vs cyclic 171/625).
- **Physical envelope** (`handheld_design_study.py` → `handheld_envelope.png`). Axial stack mask 10 + gap
  55 + crystal 15 + SiPM/FE 18 + DAQ/FPGA 55 + battery 35 + caps 18 = **~206 mm**; cross-section
  48×48 mm (~Ø50–55 mm formed) → **~0.47 L, ~3 kg, avg density ~6.3 g/cm³** (dense for its size = the W
  shield). ~60 % of the length is the electronics tail (relocatable to a grip). Shrinks with shield: 8 mm
  → Ø57/0.33 L/~2 kg.
- **Form factor: balance-first pistol grip** (`hardware_concept.py` → `hardware_concept.png`). The shield
  mass sits forward → CoM ~89 mm from the muzzle → put the **grip under the CoM**, **battery in a rear tail
  as counterweight**, camera+ToF on the muzzle, **display offloaded to phone/tablet over Wi-Fi**. An in-line
  torch is nose-heavy; a rear-screen camcorder is bulky.
- **Camera–mask parallax** (`camera_parallax_study.py` → `camera_parallax.png`). The visible camera cannot
  be coaxial (the mask is opaque, facing the scene), so a fixed lateral baseline b gives overlay
  misregistration **Δθ = b/z** — worst up close (b=40 mm: 7.6° @0.3 m, 2.3° @1 m; > the ~1° gamma
  resolution for z < ~2.2 m), negligible far. "Mount as close as possible" (min b) was the right instinct.
  **Fix: known b + measured z → reproject by b/z.** Coded-aperture depth (theme 18) ranges best in the near
  field where parallax is worst (nice synergy); add a **cheap ToF/LiDAR** for exact correction at all ranges;
  mount the camera on ONE axis to make parallax 1-D. The camera also earns its keep as a VIO sensor (below).
- **DAQ thermal + motion** (`thermal_motion_study.py` → `thermal_motion.png`). *Heat*: dissipation scales
  with channel count → monolithic (~30 ch, ~4 W) runs passive (+8 °C); pixel (~256 ch, ~12 W) needs
  fins/airflow (+24 °C passive) — another vote for monolithic. The **2.5 kg tungsten shield is a free heat
  sink** (335 J/K: 4 W·60 s → +0.7 °C) — route DAQ heat into it. *Motion*: pointing tolerance ≈ **0.83°**
  (½ cell shadow smear at D=55); free-hand drift ~1°/s smears past **0.8 s** → sources < ~240 cps need help.
  **Don't add a mechanical gimbal** (weight/power on a nose-heavy device); coded aperture is inherently
  **list-mode**, so an **IMU + per-event de-rotation** (electronic stabilization) fixes it, with the camera
  doing **VIO** to kill gyro drift and lock the overlay. Keep a monopod/brace as the cheap fallback for very
  weak sources (the user's fixture-mount heritage remains valid for the hardest dwells).
- **SiPM gain thermal drift** (`sipm_thermal_study.py` → `sipm_thermal.png`; sensor pinned to
  `samples/sipm/hamamatsu_s13360_3050cs.json`). It is an **ENERGY-WINDOW problem, not a position one**
  (global gain scale leaves the balanced MURA decode invariant → localization flat; the LLD/ULD window
  walks). S13360-3050CS fixed-bias tempco **−1.8 %/°C** → a ±10 % window loses 10 % of counts at only
  **ΔT = 3.2 °C** (why the user had to hold temperature). ① temperature-compensated bias → −0.25 %/°C
  (holds to ΔT ≈ 25 °C); ①+② LED-pulser lock → −0.17 %/°C (ΔT ≈ 36 °C). The residual floor is the
  **crystal light-yield drift (~−0.15 %/°C), which ① and ② cannot see (the LED bypasses the crystal)** —
  only a **spectral reference (K-40 / reference source, ③)** removes it. So the useful order is ①(essential)
  → ③(catches the crystal) → ②(aging backup), NOT ①→②. **③ modelled**: it has NO temperature slope (tracks
  the real scintillation line → whole chain); its residual is a **counts axis, not a temperature one** —
  σ ≈ FWHM_ref/(2.35·√N_ref) + a ~0.3 % extrapolation floor. A built-in source (~300 cps, 10 s update) →
  σ ≈ 0.3 % (flat vs T); **K-40 background (~0.05 cps) → σ ≈ 2.5 %, too weak** → **GAGG has no intrinsic line
  (unlike LYSO's Lu-176), so ③ costs a built-in reference source**. **Telemetry** (`sipm_stab_trend.py` →
  `sipm_stab_trend.{png,csv}`): over a mission (DAQ warm-up +8 °C + a small ambient swing ≈ +10 °C excursion,
  then outdoors −10 °C), an **uncompensated window collapses to ≈0 % kept counts** — modest indoor-scale
  heating alone is enough — while ①/③ hold 100 %. Log `time, T, peak-drift, window-kept` in that CSV format
  to replay a count-rate dip later. ③ only earns its keep for wide ΔT (outdoor) or tight windows (close
  lines); indoors ± a few °C, ① alone suffices. **Codex-verified (round 4)**: tempco −1.8 %/°C (Hamamatsu
  datasheet), the 3.2 °C window-loss point, the ③ centroid-statistics formula, and the "LED bypasses the
  crystal so only ③ catches light-yield drift" logic all check out. Caveats it added: warm-up ALONE is
  −15.6 % → ~3 % kept (the ≈0 % needs the ambient swing too); the SiPM is modelled gain-only, but
  overvoltage-dependent PDE makes the real fixed-bias drift slightly LARGER (conservative — strengthens the
  "you must compensate" conclusion); K-40's σ is update-rate-dependent (~2.5 % at 60 s, ~6 % at 10 s).
- **Optimal 5-sided shield thickness** (`ShieldStudy`, `montecarlo shield` → `samples/shield.{csv,png}`,
  `plot_shield.py`). Side/top/bottom/rear background is pure uncoded noise; wall transmits exp(−μ·t) onto
  the flood → localization RMS vs t vs mass. **The optimum is set by the background ENERGY**: scattered
  ~250 keV (μ 0.50/mm, HVL 1.4 mm) → **~8 mm W (1.2 kg) reaches the 0.34 mm floor**; a mono-662 field needs
  ~20 mm (~5 kg) OR ~12–15 mm + calibrated background subtraction (the software lever ≈ halves the required
  thickness); **Co-60 ~1250 keV is shieldable only at ~30 mm / 11 kg (RMS 1.08 mm) — technically possible
  but NOT carriable** → beat high-energy with **coded separation + Compton stripping, not lead**. Design
  decision: **size the 5-sided noise shield to ~8 mm (realistic scattered background), match only the front
  to the mask (10 mm); this keeps the device ~1.5–2 kg** vs the 12 mm assumed in the weight model when the
  environment is scattered-background-dominated. Blocking the low/mid-energy background alone already pays
  for the shield; Co-60 being impractical to shield is expected and handled in software. **Codex-fixed (round
  4)**: μ_W now uses NIST-XCOM tabulated values per line (the crystal-Compton MuRel power law
  underestimated high-E attenuation ~40 % at 1.25 MeV → had made Co-60 too pessimistic); the knee heuristic
  gained an absolute quality gate (RMS < fail-threshold, fail-rate < 10 %) so a never-localizing background no
  longer reports a bogus knee; mass uses max(PixelsX,PixelsY). Model is **narrow-beam / uncollided** (single
  exp(−μt); Compton buildup + oblique path lengths neglected → the mm/kg knees are first-order, not
  transport-grade).
- Files: design models `samples/handheld_design_study.py`, `plot_handheld_validation.py`,
  `camera_parallax_study.py`, `thermal_motion_study.py`, `sipm_thermal_study.py`, `sipm_stab_trend.py`,
  `plot_shield.py`, `hardware_concept.py`; scenarios `samples/scenario_handheld.json`, `scenario_orig_gagg.json`; SiPM preset
  `samples/sipm/hamamatsu_s13360_3050cs.json`; MC `ShieldStudy.cs` + `montecarlo shield`. NOTE: this theme
  is design synthesis (weight/volume/parallax/thermal/motion are analytical on top of the validated MC);
  only the config validation and `ShieldStudy` are new Monte-Carlo runs.
- **Codex-verified (round 6, low-risk batch)** — no unit/arithmetic bugs (tungsten density, shield volume,
  `1−exp(−0.057·t)` stopping, mass-weighted CoM 88.8 mm, all thermal/motion numbers +8/+24 °C, 335 J/K,
  0.83°/0.83 s/240 cps, parallax crossover 2.2 m all check out). Wording tightened: the shield is **~2/3–3/4**
  of the mass (66 % of the 3.8 kg concept build, 78 % of the 3.04 kg analytical build); the **2.45× MC** is
  led by area×stopping (≈2.35×) with a small **D-dependent solid-angle** factor ((160/155)²≈1.066) on top
  (area×stopping alone is the leading term, not the whole story); the ~Ø50–55 mm "formed" diameter is the
  equivalent-area round (the circumscribed round of the 48 mm square is Ø68). Parallax now uses exact
  `arctan(b/z)` (was small-angle; <5 % only at the closest range).

---

## 23. Mask geometry follow-ups — tapered channels (wide FOV) + empirical open fraction
Two backlog items closing the mask-geometry thread.
- **Tapered (hourglass) channels are the REAL wide-FOV fix for a THICK mask** — `CodedApertureMask`
  gained `TaperAngleDeg` (config `Mask.TaperAngleDeg`); `montecarlo masktaper` → `samples/masktaper.png`.
  A thick straight mask (25 mm) collimates off-axis rays: **edge/center efficiency 0.82**. Bevelling the
  walls (the code stays at the slab MID-PLANE, walls flare toward both faces) removes the collimation:
  **~4° recovers edge/center to 0.99** and lifts center efficiency ~35 % (1.65→2.23e-4), while the edge
  localization RMS stays ~0.43 mm (code stays sharp). It saturates fast (a small bevel already exceeds the
  ray excursion of a 25 mm slab). Implemented by de-shearing each ray's per-depth sample back toward its
  mid-plane crossing by the bevel `|z−mid|·tan(taper)`. This is the **opposite of focusing (theme 20, which
  NARROWS the FOV to a focal point)**: a fully tapered thick mask codes like a THIN mask (wide, uniform FOV).
  Mutually exclusive with `FocalDistanceMm`. Locked in by `MaskGeometryTests.TaperedChannels_WidenTheFovOfAThickMask`.
  **Codex-verified (round 6)**: the de-shear is an **idealized per-ray acceptance model** (an upper bound on
  "does a flared channel stop clipping this ray?"), NOT a true bevel-solid — so the FOV recovery is
  qualitatively real (and the ~4.8° saturation matches the geometry: worst edge slope (7.5+6)/160≈0.084 rad),
  but the efficiency numbers are the ideal-taper limit. Caveat corrected: the "thin-mask FOV **+ full
  thick-mask shielding**" claim is TOO STRONG — a real 25 mm/1 mm slab bevelled 4–5° removes tungsten from the
  neighbouring closed-cell webs near the faces (and adjacent flared holes can overlap), so it preserves the
  coded waist but **not full 25 mm opacity everywhere**. Bug fixed: the post-de-shear bounds guard now rejects
  `us<0 || vs<0` before the `(int)` cast (truncation-toward-zero would have mis-mapped a slightly negative
  de-sheared coordinate into cell 0).
- **Open fraction, empirically (random arrays + balanced decode)** — `samples/open_fraction_study.py`
  (self-contained coding MC, no C# change) → `samples/open_fraction.png`. Confirms theme 21's analytical
  claim: a periodic random binary array (rank-11, 2×2 mosaic) at open fraction ρ, decoded by a balanced
  (mean-subtracted) cyclic cross-correlation, has **reconstruction SNR broadly maximized near ρ≈0.5**
  (consistent with coding power ∝ √(ρ(1−ρ)); the empirical curve is flatter than the pure √ because the
  random array's own sidelobes also scale with ρ). But at the **same ρ=0.5, MURA is ~4× cleaner** (SNR ~46
  vs random-best ~12) — its zero-sidelobe construction gives a clean single-peak reconstruction where a
  random array shows a peak buried in **sidelobe grass** (right-hand recon maps). So ρ≈0.5 is right, and
  MURA beats a random array of the same open fraction — open fraction is not a free tuning knob.
  **Codex-verified (round 6)**: FFT cyclic-correlation convention, the Gottesman–Fenimore `mura_basic`, and
  the mosaic/roll periodic model all check out (no bug). Wording tightened: MURA's advantage is shown under
  the SAME generic balanced (mean-subtracted) decode — it's "much lower sidelobes", not the exact
  zero-sidelobe MURA-`G` construction (which flips `G[0,0]`); the metric is a **source-bin prominence**
  (peak vs off-peak, at the true bin — avoids max-selection bias); and the empirical random curve is
  **flatter than pure √(ρ(1−ρ))** because the random array's own sidelobe RMS also scales with ρ(1−ρ)
  (already noted). No numbers change.

---

## 24. Joint (x,y,S) depth — the alternating trap was the SEED, not the alternation — `DepthStudy.RunNoisyJoint3D`
Theme 19's joint estimator alternated (lateral decode at current S → depth focus at current x,y, ×3) from a
**fixed** nominal S (100) and stalled on off-axis coupling. I built a seed-free full 3D search
(`EstimateJoint3D`, `montecarlo depth3d` → `samples/depth3d.png`): for each assumed S, decode the lateral
FCFOV grid and score the slice by its **peak prominence z = (peak − grid mean)/grid std**; depth = the
plateau-robust centroid of the per-S z-profile, lateral = the argmax of the slice NEAREST that depth (so
(x,y,S) is self-consistent). **Codex round 5 forced an honest reckoning** — a fair 3-way comparison (fixed
seed 100 / oracle seed = true S / seed-free 3D) shows:
- **The alternating iteration's failure was the FIXED BAD SEED, not the alternation.** With an oracle seed it
  converges well: near-field (S=60) lateral RMS **0.42 mm**; far-field (S=150) depth RMS **1.6 mm, bias 0.1**
  at 3000 counts — *better than the 3D search everywhere*, dramatically so far-field.
- **The 3D search's real value is being SEED-FREE** (no initial guess). Near field it converges (lateral RMS
  0.35 mm, comparable to oracle-alt) but its **peak-prominence z is a HEURISTIC, not a calibrated GLRT/SNR**
  (retracted that claim): the grid mean/std include the peak+sidelobes, giving a **systematic bias (+7 mm
  near, −18 mm far)** that the single-point calibrated focus (theme 18) avoids. So 3D is robust but biased.
- **Best practice = hybrid**: a coarse/seed-free pass (3D or the on-axis `RunNoisyDepth`) to SEED, then the
  alternating iteration (calibrated single-point focus) to REFINE → robust AND unbiased. Neither alone wins.
- Far field stays physics-limited only for the *unseeded* estimators; a well-seeded refine reaches ~1 % of
  the distance. `RunDepth3D` now runs all three methods so the seed effect is explicit.
- `Joint3D_BeatsAlternating_OnNearFieldCoupling` still holds (3D beats the FIXED-seed alt — the original
  documented failure). Reproduce: `montecarlo depth3d`.

---

## 25. Trapezoidal shaper in RTL + first cocotb co-sim — `rtl/trapezoidal_shaper.sv`
The noise-optimal shaper, implemented as real SystemVerilog and verified with **cocotb** (the
project's first cocotb co-simulation — it drives the DUT directly, replacing the file-I/O testbench).
- **`trapezoidal_shaper.sv`** — the recursive **Jordanov-Knoll** trapezoidal filter with pole-zero
  (decay) correction: `d[n]=v[n]−v[n−RISE]−v[n−L]+v[n−RISE−L]`, `p+=d`, `r=p+M·d`, `s+=r` (M in Q8
  fixed point = the decay deconvolution constant). Icarus-friendly (no `automatic`/`void'()`).
- **What it buys** (`rtl/trap_shaper.png` via `trap_shaper_study.py`): (1) the **flat top ∝ energy**
  independent of ballistic deficit; (2) the **pole-zero M term deconvolves the exponential tail** so the
  output returns to baseline (M=0 gives a rounded, no-flat-top response that builds up at rate); (3) two
  piled-up pulses give **two resolvable flat tops** where the peak-hold merged them into a fake sum.
- **Verification**: `trap_ref.py` is an integer reference **bit-exact** to the RTL (Python `>>` == SV
  signed `>>>`); `test_trap_shaper.py` (cocotb) drives single + piled pulses and asserts RTL == reference;
  `run_cocotb.py` builds/runs with the Icarus cocotb runner → **TESTS=2 PASS=2**. The plotted curves are
  the bit-exact reference, so `trap_shaper.png` IS the RTL behaviour.
- **Environment gotcha (documented)**: cocotb's VPI cannot dlopen the **Windows-Store Python**'s
  `python311.dll` (access denied in the protected WindowsApps dir) → run cocotb with the **python.org 3.13**
  install (`…/Programs/Python/Python313/python.exe`). This is why the earlier note said "no cocotb on this
  machine"; the blocker was the Python packaging, not the tooling. Icarus needs a `` `timescale `` for the
  cocotb clock (added to the module).
- Reproduce: `cd rtl && <python313> run_cocotb.py` then `python trap_shaper_study.py`.
- **FPGA synthesis (Yosys, real numbers)**: `yosys -p "read_verilog -sv trapezoidal_shaper.sv;
  synth_xilinx -top trapezoidal_shaper; stat"`. One channel on **Artix-7** (WIN 16, WACC 32,
  RISE 10/FLAT 8): **~809 LUT, 512 FF, ~244 carry cells, 0 DSP, 0 BRAM** (the M multiply is a compile-time
  CONSTANT → synthesized as LUTs, not a DSP48; a runtime-variable M would use 1 DSP). That is ~4 % of an
  XC7A35T (20.8 k LUT) — so a **30-channel monolithic front-end ≈ 24 k LUT (fits an A50T) while a
  256-channel pixel array ≈ 207 k LUT (needs an A200T)**: the FPGA cost favours monolithic too (a third
  vote, after weight and thermal). NOTE: Yosys 0.9 needs
  plain `parameter` (not `parameter int`) + `integer` loop vars — the module was made dual-compatible
  (cocotb still bit-exact). EXACT Artix-7 Fmax needs Vivado (not on this machine); Artix-7 is covered by the
  FREE Vivado ML Standard (no paid licence), and `rtl/vivado_trap.tcl` runs synth+place+route+timing in
  batch (`vivado -mode batch -source vivado_trap.tcl [-tclargs <top> <part> <period_ns>]`). nextpnr does not
  target Xilinx (Lattice only), so the open flow can only give a real Fmax on an ECP5/iCE40 proxy.
- **Pipelined variant** `trapezoidal_shaper_pl.sv` — the textbook fix for the un-pipelined critical path.
  Reformulate `s[n] = Σ(p+m) = q[n] + mm[n]` with `q=Σp`, `mm=Σm` so every **feedback loop is a single
  adder** (p+=dkl, q+=p, mm+=m); the FIR, the constant multiply, and the final `q+mm` combine are
  feed-forward register stages. **Bit-exact to the direct shaper (+3 samples latency)** — cocotb runs the
  SAME reference against both (`run_cocotb.py` → 2 tops, each TESTS=2 PASS=2). Cost measured: **512→632 FF,
  ~809→~1056 LUT**. **MEASURED Fmax gain (real place-and-route STA, open flow)**: Yosys 0.9's `ltp`
  cell-count couldn't show it (carry/constant-multiply dominated, direct 225 vs pl 233 — the "~100 MHz
  from ltp" reading was retracted, cell-count ≠ delay), so it was measured with **nextpnr on a Lattice
  ECP5-6** (OSS CAD Suite: `yosys synth_ecp5 -json` → `nextpnr-ecp5 --25k --package CABGA381 --speed 6`):
  **direct 59 MHz vs pipelined 119 MHz = ×2.0** (`rtl/fmax_ecp5.png`). On this ECP5 the direct shaper
  can't meet a 100 MSPS ADC clock while the pipelined one clears it — the 1-adder-loop reformulation
  delivers the predicted ~2× by real STA. (ECP5 is a Lattice proxy; exact Artix-7 Fmax still needs the
  free-for-Artix-7 Vivado via `vivado_trap.tcl -tclargs trapezoidal_shaper_pl` — deferred, licence/install.)
  **Codex-verified (round 5)**: the `s=q+mm` reformulation is bit-exact to the direct filter INCLUDING the
  per-sample `(dkl*M_Q8)>>>8` rounding (and correctly ≠ shifting after summing); the split integrators
  `q`,`mm` drift and can wrap past WACC individually, but the two's-complement `q+mm` recovers the correct
  `s` whenever the true `s` fits WACC — **sound, not a bug**; +3 latency and delay-line indexing confirmed.
  Fixes applied: documented the FIR's context-determined WACC width (the WIN-wide taps sign-extend — no
  overflow; explicit casts avoided to keep Yosys-0.9 compatibility), and the cocotb test now asserts the
  **contractual latency** (direct 0, pipelined 3), not just bit-exactness at some offset.

---

## 26. Mixed-isotope field imaged in one run — `MixedFieldSource` / `MixedFieldStudy`
The core-capability gap closed: a REAL mixed field (several sources, each multi-line) imaged through the
coded aperture in ONE Monte-Carlo run, instead of summing per-line runs. Config: `SimulationConfig.Sources[]`
(scene) + `SourceConfig.Lines[]` (multi-line) + `EmissionLine{EnergyKeV, Intensity, CascadeCoincident}` — all
optional (null = classic single source, backward compatible).
- **Emission model** (`MixedFieldSource`): per photon, pick a (position, line) emitter with probability ∝
  **activity × line-intensity**, emit from it with the `DetectorBiasedSource` importance weight. **No extra
  reweight** — proportional photon allocation makes each emitter's contribution ∝ its weight, and the biasing
  weight carries each source's own geometric efficiency. Validated by **superposition**:
  mixed(A:1 + B:3) detected weight ≈ 0.25·single(A) + 0.75·single(B). **Codex-verified (juncture 1)**: the
  proportional-allocation estimator is correct/unbiased (DetectedWeight estimates
  `PhotonCount·Σ(activity·intensity/Σw · geo-eff)`); `Pick()`, factory weighting, `LinesOf` fallback all
  sound. Fix: reject empty / non-finite / total-nonpositive emitter weights.
- **Multi-source imaging** (`MixedFieldStudy`, `montecarlo mixedfield` → `samples/mixedfield.png`): a
  **Cs-137 @(5,1) + Co-60 @(-6,3) + Co-57 @(0,-6)** field, imaged in one non-cyclic run, is decoded and the
  K strongest peaks extracted by greedy non-max suppression → **all three localized < 1 mm**. The recon grid
  is limited to the FCFOV (beyond it the partial-coding region throws edge artifacts that outshine the weakest
  source — Cs here, being the lowest activity×intensity). **Codex-verified (juncture 2)**: `TopPeaks` correct;
  FCFOV limiting legitimate for **K-known localization inside the FCFOV** (not blind source-counting). Fix:
  **one-to-one** truth↔found matching (a single peak can no longer be reused to "cover" several truths);
  empty-`Sources` handling aligned with the factory; parameter guards.
- **Pipeline-level energy windowing (Stage 3, `montecarlo mixediso` → `samples/mixediso.png`)**: because
  `ComptonFactory` delegates source creation to the default factory, driving it with `config.Sources` images
  the mixed field through the **crystal-Compton detector + a 662 keV ±10 % window in ONE run**. Cs-137 @(4,0)
  + a strong Co-60 @(-5,3) (activity ×8): the 662 window admits BOTH the Cs photopeak AND the Co
  Compton-downscatter, so energy alone can't reject the Co — but the coded aperture encodes direction *before*
  the crystal interaction, so the decode images **Cs at (3.9,−0.5) and the Co contamination at (−6.1,2.7)** —
  theme 15's spatial separation, now from a TRUE mixed field (not summed per-line runs). **Codex-verified
  (juncture 3)**: all four physics points confirmed (662-window flood = Cs photopeak + Co downscatter; the
  ComptonFactory+Sources integration is sound; "Co contamination at Co's position" is the right reading).
  Honest framing (Codex): this separates the two 662-window components by **POSITION**; isotope ID of
  CO-LOCATED sources still needs the spectral lever (per-pixel stripping, themes 16–17), and the test is
  localization-only (doesn't itself prove the accepted Co events are specifically downscatter).
- **Per-pixel stripping on the mixed field (refinement, `montecarlo mixedstrip` → `samples/mixedstrip.png`)**:
  the SPECTRAL lever for CO-LOCATED sources, now from a true mixed field. From one mixed scene it builds
  raw662 (Cs photopeak + Co downscatter) and coWin (Co 1332 photopeak; Cs deposits ≤662 → ~0 there), and
  subtracts `stripped = max(0, raw662 − R·coWin)` with **R = downscatter-into-662 / Co-photopeak ≈ 1.18**
  calibrated from a Co-only run. Both **separated AND co-located**: raw over-counts Cs by **+223 %** (Co
  contamination), stripping recovers it to **≈true (+1–3 %)** — the co-located case is the one spatial decode
  CANNOT split. Key subtlety (found + fixed): the mixed field splits a FIXED photon budget by emission weight,
  so the weak Cs is diluted to ~5 %; the "true Cs" reference is Cs at its budget share
  (`PhotonCount·wCs/Σw`), which makes raw/stripped comparable. **Codex-verified (juncture 4)**: the scaled
  reference is correct, R is budget-independent (a ratio, linear in count), coWin is Co-only, R≈1.18 is
  plausible for a thin/low-photofraction crystal at 1332 keV. Caveats: R depends on crystal/window/geometry
  (not a pure "crystal constant"); recovery is approximate (the max(0,·) floor biases slightly up); the
  Compton detector has no spectral smearing yet. So spatial (coded decode) + spectral (stripping) together
  disentangle even co-located isotopes from a true mixed field — the full two-lever result (themes 15–17).
- **Energy-dependent mask μ (final refinement)**: `CodedApertureMask` now scales its attenuation by the
  tungsten **μ(E)/μ(662)** ratio (NIST-XCOM points 122→28.6, 250→5.0, 662→1.0, 1332→0.55, log–log
  interpolated), so in a mixed field **low-energy Co-57 122 keV is heavily blocked by closed cells while
  Co-60 1332 keV leaks through** (verified: 1332-line detected weight > 122-line at equal emission weight).
  Anchored at 662 = 1.0 so `Mask.LinearAttenuationPerMm` keeps its meaning and all single-662 studies are
  unchanged. Locked in by `MixedFieldTests.MaskAttenuation_IsEnergyDependent`.
- Locked in by `MixedFieldTests` (superposition, single-source equivalence, multi-line split, 3-source
  localization, energy-window isotope separation). 31 tests green. Reproduce: `montecarlo mixedfield` /
  `montecarlo mixediso samples/scenario.json`.

## 27. MC → RTL: drive the cocotb shaper from the Monte Carlo event stream — `EventStreamStudy` / `rtl/event_stream.py`
Closed the top RTL backlog item: the trapezoidal shaper (theme 25) is no longer fed a synthetic pulse —
it is driven by the **real crystal-Compton deposit spectrum from the validated C# MC**, with realistic
Poisson pile-up. End-to-end loop: MC physics → event stream → ADC waveform → gate-level RTL → energy recovery.
- **Event tap** (`ComptonCrystalDetector` optional `eventSink`): records the **total energy deposited across
  the Compton cascade** for EVERY scored event (photopeak *and* continuum/escape), independent of the energy
  window — that sum is the analog pulse height the SiPM/ADC/shaper actually sees. `EventStreamStudy.Generate`
  taps it, then overlays a **Poisson arrival process** (exponential inter-arrival, mean gap `fs/rate` samples)
  at a chosen operating count rate. Energies are MC physics; **rate is an independent operating-point knob**
  (the MC's PhotonCount is a variance-reduction budget, not real counts — same convention as the RTL rate
  studies). `montecarlo eventstream <cfg> [rateKcps] [maxEvents]` writes `rtl/event_stream.txt`
  (`arrival_sample energy_keV` per line). A centered Cs-137 baseline gives ~68 % photopeak + ~31 % continuum.
- **cocotb, driven by the stream** (`rtl/event_stream.py` rasterizer + `test_trap_shaper.mc_event_stream_matches_reference`):
  the rasterizer sums each event's exponential pulse (amp = keV × ADC/keV, decay τ) into one ADC waveform —
  **bit-identical** to summing full-length `exp_pulse` contributions (each pulse is walked until its rounded
  sample first hits 0; monotone decay ⇒ the tail is exactly 0). Feeding that waveform to **both** the direct
  and pipelined shapers matches the integer reference **bit-for-bit** (TESTS=3 PASS=3 each): the RTL processes
  the real MC stream identically to `trap_ref`. **This is the deliverable — the closed MC→RTL loop.**
- **Energy recovery & the baseline-walk finding** (`rtl/event_stream_study.py` → `rtl/event_stream.png`):
  reading each event's flat top recovers deposited energy. The MC stream exposed a real effect a single
  synthetic pulse never shows — the **Q8-quantized pole-zero constant** (M_Q8=1156/256=4.5156 vs exact 4.5167)
  leaves a tiny residual per pulse, so over a 1500-event train the shaper **baseline WALKS by ~−47 000 ADC**
  (monotone). The fix is what real trapezoidal DAQs do — **baseline restoration**: read the flat top RELATIVE
  to the local pre-ramp level, which rejects the slow walk and isolates genuine pile-up. Recovery then degrades
  cleanly with rate: **±5 % recovery 92 % → 78 % → 44 %** at **100 / 500 / 2000 kcps** (pile-up fraction
  11 % → 40 % → 89 %); the recovered spectrum's 662 photopeak shrinks and a sum tail grows past 662 as rate climbs.
- **Codex-verified (2 junctures)**. C# side: sink records total cascade energy before the window ✓, Poisson mean
  gap `fs/rate` correct ✓; **fix** — the sink ignored the directional-biasing `Photon.Weight`, so the raw list
  sampled the *biased proposal*, not the physical spectrum → added **weighted (systematic) resampling** back to
  the physical detected-event distribution (identity for analog/centered runs, matters off-axis). Python side:
  rasterize early-break is truly lossless ✓, baseline subtraction is the right pole-zero-walk correction ✓,
  calibration unaffected ✓; **fix** — baseline index guarded so a first event at sample 0 uses baseline 0.
- Locked in by `EventStreamTests` (deposit bounded by the line energy, photopeak+continuum both present,
  arrivals monotonic, mean gap matches the requested rate, text round-trip). **37 tests green.** Reproduce:
  `montecarlo eventstream samples/scenario.json 500 1500` then `cd rtl && python run_cocotb.py` (Python 3.13)
  and `python event_stream_study.py`.

## 28. Ambient background — a controllable, opt-in noise field — `BackgroundConfig` / `BackgroundStudy`
The simulation had **no ambient/environmental background** (only source counting-noise + mask leakage +
cross-isotope contamination); a real detector sees a diffuse field that raises the noise floor. Added it as a
**controllable, opt-in** model, NOT a global always-on noise (which would silently move every prior baseline
and be un-tunable). Master knob = a dimensionless **background-to-signal ratio (BSR)** = detected background
÷ detected source counts. `SimulationConfig.Background` (nullable, default null → all legacy scenarios/tests
unchanged); the new studies/scenarios opt in, so background is exercised without contaminating the clean runs.
- **Key physics — a diffuse isotropic background is NOT coded.** The mask codes *direction*; an isotropic field
  hits every pixel through the mask's average transmission, so it lands as a **uniform pedestal** on the flood
  map, not a structured image. So modelling it as an uncoded uniform additive rate is the *correct* model, not a
  shortcut — a full MC transport would just converge to the same pedestal at huge cost (and it's what the
  antimask/shield studies already assumed ad hoc).
- **Flood-map / imaging** (`Background.PedestalPerPixel` + `BackgroundStudy.RunSweep`, `montecarlo background`
  → `samples/background_sweep.csv`, `rtl/background.png`): add a uniform Poisson pedestal (mean = BSR·budget/
  pixels) to the source flood map, decode, sweep BSR. Result: a **cyclic MURA decode rejects the flat pedestal
  into DC**, so localization holds far into background — RMS stays at the **0.57 mm floor up to BSR ≈ 1** (equal
  background and source counts!), then knees up at **BSR 2 → 4** (RMS 2.95 → 7.11 mm, fail 9 % → 65 %) and is
  buried by BSR 8. Decode contrast (noisy-realization peak SNR) falls 5.4 → 2.8. So the coded aperture is
  **intrinsically background-robust** — its weakness is the pedestal's *shot noise*, not the pedestal itself.
- **Event-stream / RTL** (`EventStreamStudy` background merge, `montecarlo eventstream <cfg> <rate> <max> <bgRatio>`):
  merges a **second, uncoded Poisson event process** at rate BSR·sourceRate into the pulse train, each event's
  deposit sampled from the crystal response to the background energy (real photopeak+continuum, built by
  transporting background photons through the unmasked detector); optional **DCR** adds low-amplitude nuisance
  pulses. Background raises the true count rate (mean gap 208 → 140 samples at BSR 0.5), and the RTL shaper
  processes the background-laden stream **bit-for-bit vs reference** (cocotb PASS) — so the MC→RTL loop (theme 27)
  now runs on a realistic field. Ships a `samples/scenario_field.json` (BSR 0.5 + DCR) as a first-class "with
  background" case.
- **Codex-verified (2 junctures)**. Flood-map: BSR→pedestal math correct (expected bg sum = BSR·budget) ✓,
  uncoded-uniform model valid as a *detected*-background approximation ✓; **fix** — `PeakSnr` was computed on the
  deterministic mean-map decode (a DC-leakage proxy, not shot-noise SNR) → moved it onto the **noisy** decodes and
  averaged; softened the comments (BSR already folds in mask transmission; DC-rejection is the cyclic-decoder
  reading, non-cyclic sees a mild partial-coding shape). Event-stream: BSR→rate + Poisson merge + deposit-pool
  correct ✓, no ordering/off-by-one bug ✓; **fix** — DCR wording (a few-keV nuisance, not literally sub-keV) and
  a normal-incidence caveat on the deposit pool.
- Locked in by `BackgroundTests` (sweep degrades monotonically + collapses, BSR 0 = clean, pedestal math) and
  `EventStreamTests` (background merges extra events, DCR adds sub-keV pulses). **42 tests green.** Reproduce:
  `montecarlo background samples/scenario.json` → `cd rtl && python background_study.py`; background stream via
  `montecarlo eventstream samples/scenario_field.json 500 1500`.
- **Isotropic angular background (refinement)**: the event-stream deposit pool now samples a **cosine-weighted
  downward hemisphere** (the flux-through-a-plane law) instead of normal incidence — the physically correct
  angular distribution for a diffuse field entering the top (mask) face. Counter-intuitive result the finite
  array makes: the isotropic photopeak fraction is *slightly LOWER* (90.9 % vs 92.0 % at 300 keV, 12×12×10 mm)
  — oblique rays reach the SIDE walls and escape sooner, and that outweighs the longer 1/cosθ vertical path
  (which would only win for an infinite lateral slab). `EventStreamStudy.BackgroundDepositSpectrum(…, isotropic)`
  exposes both models. **Codex-verified**: cosine weighting `p(μ)=2μ` correct, origin/entry geometry correct,
  side-escape explanation sound (fixed the doc comment that wrongly said absorption *rises*). Test
  `IsotropicBackground_DepositsDifferFromNormalIncidence`. **43 tests green.**
- **Directional shield leak (refinement)**: a full photon transport through the shield walls was judged **not
  worth it** — buildup and directional-harm both only bite where the conclusion is already robust (buildup is
  small at the low energy where shielding works, large only at Co-60 which is already unshieldable; directional
  structure hurts most at thin shields where localization already fails). The valuable, cheap piece is the
  **spatial profile** of the leak: `Background.SideLeakProfile(W, H, sideFraction)` gives a mean-1 multiplier
  that puts `sideFraction` of the (same total) leak through the 4 side walls edge-weighted (∝ Σ1/(1+dist)) and
  the rest uniform through the rear. `ShieldStudy.Run` takes `sideLeakFraction` (0 = the old uniform pedestal,
  byte-identical). Result: at the SAME total leak, an 80 %-side-wall directional background degrades
  localization more than a flat pedestal (t=2 mm: RMS 5.3 → 8.3 mm; t=4: 1.0 → 1.7 mm), because the coded
  decode rejects only the uniform DC part — so the **useful knee shifts 6 mm → 8 mm** for the scattered field.
  The uniform model is therefore an *optimistic lower bound* on shield thickness. **Codex-verified** (mean-1
  exact, edge-weighting monotone, sideFraction=0 byte-identical). Test `SideLeakProfile_IsMeanOneAndEdgeWeighted`.
  Regenerated `samples/shield.{csv,png}` (the committed CSV was stale — older geometry; conclusions unchanged).
- **Unification (refactor)**: the "source mean + uncoded pedestal → Poisson" counting primitive now lives in
  one place — `Background.RealizePixel(rng, sourceMean, ped)` and the row-major `Background.Realize(dst, src,
  scale, ped, rng)`. `ShieldStudy`, `MaskAntimaskStudy` (per-pixel, keeps its gradient option + interleaved
  multi-exposure draw order), and `BackgroundStudy` all route through it instead of hand-rolling
  `Sampling.Poisson(...)`. Behavior-preserving — the background sweep CSV is **byte-identical** and
  **Codex-verified** that the RNG draw order and Poisson means are unchanged at all three sites.
- **Structured background — spatial gradient**: `Background.GradientProfile(W, H, angleDeg, contrast)` gives a
  mean-1 *linear* ramp (the diffuse-but-not-flat case: a field stronger on one side — a nearer contaminated
  wall, ground/sky asymmetry). Centroid-centred projection onto the ramp direction, normalized by max|proj| →
  values in [1−c, 1+c], mean **exactly 1 for ANY angle** (**Codex-verified** the projection sums to zero;
  no pixel negative for c∈[0,0.999]). `BackgroundStudy.RunSweep` takes `gradientContrast`/`gradientAngleDeg`
  (0 = the old flat pedestal, byte-identical) and applies the profile to both the deterministic mean map (bias)
  and every Poisson realization. **Sim-revealed result (corrected my framing):** a gradient is NOT a small
  steady bias — while the source peak wins the argmax it is *identical to a flat pedestal* (BSR ≤ 2: bias
  0.55 mm either way; the low-frequency residual doesn't move the peak), then at the background knee it drags
  the estimate systematically toward its strong side (BSR 4: flat 1.1 mm → **graded 9.2 mm**) where a flat
  pedestal only fails *randomly*. So the harm is *negligible-then-catastrophic*, and the flat-pedestal sweep
  is an optimistic model near the knee. The remaining structured case — a *directional discrete* background —
  is not new work: it is just another off-axis source in the mixed field (theme 26). Tests
  `GradientProfile_IsMeanOneAndRampsAlongDirection`, `GradedBackground_IsHarmlessWhileSourceWins_ThenBiasesAtTheKnee`.

## 29. RTL baseline restoration — cancel the trapezoidal shaper's pole-zero walk — `rtl/baseline_restorer.sv`
Fixes the effect theme 27 surfaced: the shaper's **Q8-quantized pole-zero** (M_Q8 = round(M·256) = 1156 vs
exact 4.51665) cancels the exp tail only approximately, so every pulse leaves a residual step and the DC
baseline **WALKS** over a long train (the MC stream drifts **0 → ~-42000 ADC** over 1500 events). Theme 27's
study worked around it by reading each flat top *relative* to the local pre-pulse level; this makes the RTL's
**absolute** output usable.
- **`baseline_restorer.sv`** — a **gated leaky integrator**: a Q(FRAC) baseline estimate `base_acc` that
  updates (`base_acc += x - base`) ONLY in quiet regions (`|x - base| < GATE`, so flat tops freeze it and
  their height is preserved), and the output is `y = x - base`. The Q(FRAC=12) accumulator stops the small
  per-sample leak from rounding to zero; the loop time constant is ~2^FRAC ≈ 4096 samples (≫ a ~28-sample
  pulse, ≪ the train). Composable/standalone (shaper → BLR), Icarus+Yosys-friendly.
- **cocotb** (`rtl/test_blr.py`, `trap_ref.blr`): drive the SHAPED MC stream (200 k samples) through the BLR,
  assert it matches the integer reference **bit-for-bit** AND removes the walk — the raw shaped baseline
  reaches < -15000 while the BLR output stays near zero (min ~-959) with the flat tops intact (max > 100000).
- **Demonstration** (`rtl/blr_study.py` → `rtl/blr.png`): the baseline estimate walks 0 → -41550 ADC over the
  full train; the BLR output holds a flat zero baseline with correct flat-top heights (the estimate = shaped
  − restored). Uses the same integer reference the RTL is bit-exact to, so the curves ARE the RTL behaviour.
- **Codex-verified**: gating freezes on pulses / tracks in quiet correctly; the Q(FRAC) accumulator is
  necessary and correct (τ ≈ 2^FRAC); `trap_ref.blr` is bit-exact (registered output = pre-update baseline,
  arithmetic shift); no overflow (`-47000·4096 ≈ -1.9e8` ≪ 2^43). Documented the inherent gated-BLR caveats
  (GATE between quiet-noise and smallest pulse; a near-100%-busy train has no quiet to track; start-up offset
  must be inside GATE — the stream starts at 0, so it acquires). **44 C# + 4 cocotb tests green.** Reproduce:
  `montecarlo eventstream samples/scenario.json 500 1500` then `cd rtl && python run_cocotb.py` and
  `python blr_study.py`.

## 30. Realistic ADC front-end — the shaper stops seeing an idealized signal — `rtl/event_stream.py`
The waveform driving the RTL (theme 27) was near-ideal: instantaneous-rise single-exponential pulses,
**zero noise**, no clipping — so the recovered photopeak was a delta function and the trapezoidal shaper had
nothing to fight (its whole purpose is to maximize SNR against a noise floor). Made the front-end realistic
(user: "the sim only means something if the front-end is realistic"). `rasterize` now models, all controllable:
- **Intrinsic (photostatistical) resolution** — each pulse's amplitude fluctuates by the scintillator+SiPM
  light statistics, relative FWHM ∝ 1/√E (`intrinsic_fwhm` at `intrinsic_ref_kev`, default 6 % @ 662, GAGG-ish).
- **Finite rise (ballistic deficit)** — a bi-exponential pulse `a·(e^{-t/τ} − e^{-t/τ_rise})` (`trap_ref.biexp_pulse`).
  Its TAIL is still `a·e^{-t/τ}`, so the single-τ pole-zero and the energy calibration are unchanged; the rise
  is the realistic part (it suppresses the VISIBLE peak — τ_rise=1 → peak ≈ 53 % of the tail amplitude). **τ_rise
  is bounded BELOW by the ADC/Nyquist limit** (`tau_rise_study.py`): a rise faster than ~0.6 samples has a signal
  bandwidth above the 62.5 MHz Nyquist (at 125 MSPS) and ALIASES — the FPGA can't faithfully sample it — so
  τ_rise=1 (BW ~39 MHz, ~3 samples on the rising edge) is the fastest realistic value; a real anti-alias filter
  enforces this floor. **Correction of an earlier claim**: τ_rise does NOT change the baseline walk — the walk
  (~-150000 ADC over the stream vs ~-47000 for the theme-29 setup) comes from the pole-zero on the exponential
  TAIL and is flat across τ_rise (and independent of bit depth / clipping); measured, not the rise term.
- **White electronic noise** — a Gaussian floor (`noise_kev` keV-equivalent RMS, default 3) on every sample:
  the noise the shaper actually averages against.
- **ADC clip + quantize** — to ±`adc_max` (16-bit), so pile-up stacks saturate (honours the shaper's input port).
- **Result** (`rtl/frontend_study.py` → `rtl/frontend.png`): the recovered Cs-137 spectrum now has a **realistic
  ~6.4 % FWHM photopeak** (was a delta) plus the Compton continuum and a pile-up tail. The resolution **budget**
  splits cleanly — electronic-only **2.3 %**, intrinsic-only **6.0 %**, FULL **6.4 %** — and the two add in
  **quadrature** (√(6.0²+2.3²)=6.43 %), exactly as they should. The recovery-vs-rate study now reflects real
  resolution (±5 % recovery 79/67/36 % at 100/500/2000 kcps, down from the noiseless 92/78/44 %).
- Bit-exactness is untouched: rasterize is called once and its (noisy, clipped) waveform is fed to BOTH the RTL
  and the integer reference, so the cocotb shaper + BLR tests stay **bit-for-bit** (the noise is identical on
  both sides). Set `intrinsic_fwhm=0, noise_kev=0, tau_rise=0` to recover the old ideal waveform.
- **Codex-verified**: bi-exp preserves the pole-zero tail + ballistic deficit; 1/√E intrinsic scaling on the
  deposited energy is correct photostatistics; white-noise + clip is a sound simplified ADC model; quadrature
  budget checks; calibration is the noiseless deficit-corrected mean gain. **Fix**: `calibrate_flat_per_kev`
  ignored its `adc_per_kev` arg (biexp_pulse hardcoded the constant) → threaded it through. 44 C# + 4 cocotb
  tests green. Reproduce: `montecarlo eventstream samples/scenario.json 500 1500` then `cd rtl && python frontend_study.py`.
- **ADC grounded in a real part, swappable preset** (`samples/adc/*.json`, `event_stream.load_adc`): the ADC
  was a generic 16-bit/gain-4 model — the 662 photopeak sat at only ~3-8 % of full scale (looked
  low-resolution because the gain under-used the range, not because the physics needed it). Now a datasheet
  preset sets everything: `adc_max = 2^{bits-1}-1` (signed FS), `adc_per_kev = adc_max / fullScaleKeV` (gain so
  the highest line reaches near FS), and the ADC's OWN input-referred noise from ENOB (SNR = 6.02·ENOB+1.76,
  noise_codes = FS_rms/10^{SNR/20}) added in **quadrature** with the analog/preamp noise. Presets: **AD9648**
  (14-bit/125 MSPS, default), **AD9268** (16-bit), **AD9235** (12-bit). With AD9648 + fullScaleKeV 2000, the
  662 line sits at **33 % FS** (~12 effective bits) and the ADC's own noise is only **~1.3 codes RMS** (≪ the
  ~12 codes analog floor) — so the part choice barely moves the energy resolution (6.41 % vs 6.40 %), which is
  exactly right: in a well-set-up system the analog chain, not the ADC, sets resolution. `montecarlo eventstream`
  + `python adc_study.py` → `rtl/adc.png` shows the waveform titled with the active part. **Codex-verified** (ENOB
  →noise, gain, quadrature, signed-FS all correct; note: ENOB is SINAD-derived, used as broadband ADC noise).
  Swap with `rasterize(..., adc=event_stream.load_adc("ad9268"))`.

## 31. Pulse-shaper comparison — CR-RC vs trapezoidal vs cusp — `rtl/shapers.py` / `rtl/shaper_compare.py`
With a real electronic-noise floor + realistic pile-up (theme 30) the shaper comparison finally MEANS something
(on a noiseless input every filter gives perfect resolution). All three run on the same ADC stream: each
deconvolves the exp tail (`imp[n]=x[n]-e^{-1/τ}x[n-1]`, pole-zero) then applies its weighting —
**CR-RC^4** (4 single-pole RC low-passes → semi-Gaussian), **trapezoidal** (flat-top), **cusp** (symmetric
sinh FIR, the near-optimal shape for series+parallel noise).
- **Noise (electronic-only photopeak FWHM, isolated events, low rate)**: **cusp 1.02 % < CR-RC 1.10 % <
  trapezoid 1.74 %** — the cusp is near-optimal ENC; the trapezoid pays a little noise for its flat top. (Total
  resolution barely differs — the 6 % intrinsic dominates — so this is visible only with intrinsic OFF.)
- **Pile-up (662 recovered within ±5 %, full front-end, no isolation cut) vs rate**: set by the total SUPPORT
  WIDTH — the flat-top trapezoid, being WIDEST here, degrades FASTEST (40 % @ 2 Mcps vs ~65 % for CR-RC/cusp).
  **This corrected a common misconception** I'd encoded: the trapezoid is NOT "best at high rate" — its flat top
  is a **ballistic-deficit** feature (accurate energy despite variable charge-collection time), which costs both
  noise and width; for pure pile-up a NARROWER filter always wins. This model has a fixed rise, so the
  trapezoid's real deficit-immunity advantage isn't exercised (why real scintillator DAQs still prefer it).
- **Codex-verified**: deconv is the correct exp pole-zero; CR-RC^n cascade is a valid semi-Gaussian (RC coeff is
  Euler-approx, fine); sinh cusp is a defensible finite near-optimal cusp; the physics reading (cusp best ENC;
  pile-up ~ width; flat-top = deficit not rate) is right. Fixed stale "trapezoid holds up best" prose. The ±5 %
  metric is offline energy-recovery accuracy, not hardware dead-time throughput. Reproduce: `montecarlo
  eventstream samples/scenario.json 500 1500` then `cd rtl && python shaper_compare.py` → `rtl/shaper_compare.png`.

## 32. Physical front-end folded into the C# pipeline — `FrontEndModel` / `FrontEndConfig`
Ported `rtl/frontend_model.py` (the photoelectron-budget resolution model, until now a Python/RTL-only
design layer) into the validated C# MC, so the pipeline's energy discrimination reflects a REAL,
energy-dependent resolution instead of a hand-set number.
- **`FrontEndModel`** (Detector): `N_pe(E) = lightYield·collection·PDE·E`; `R_stat(E) = 2.355·√(ENF/N_pe)`
  (a FWHM FRACTION, so 235.5-in-% becomes 2.3548-as-fraction); `R_tot(E) = √(R_stat² + R_intrinsic²)`. The
  statistical part scales **1/√E** (higher lines resolve better), flooring at the crystal's non-proportionality
  `R_intrinsic`. `Measure(e, rng)` smears a true deposit by `e·(1 + N(0,1)·R_tot(E)/2.355)`. Config:
  `DetectorConfig.FrontEnd` (`FrontEndConfig`: light yield, collection, PDE, ENF, intrinsic) — **nullable /
  opt-in**, so every legacy scenario is byte-identical (null = no draw).
- **Wired into `ComptonCrystalDetector`**: when a `FrontEndModel` is present, each deposit is smeared by
  `R_tot(E)` **before the energy-window check** (per-site for PerPixelWindow/AntiCoincidence, on the total for
  Argmax/Centroid). So a 662 photopeak event can now scatter out of a tight window and continuum events near the
  edge fluctuate in/out — realistic energy discrimination for the Compton / mixed-field / stripping studies. The
  smear uses a **separate RNG** (not the cascade stream) so enabling it doesn't perturb the Compton cascade —
  the strategy comparison still replays identical cascades.
- **`montecarlo frontend`** reproduces frontend_model.py's per-crystal table in C#: GAGG 5.7 %, CeBr3 4.3 %,
  LaBr3 3.3 % (only photon-limited one), LYSO 7.8 %, BGO 10.3 %, NaI 6.3 % FWHM @ 662 — plus R_tot@1332 showing
  the 1/√E improvement. **Codex-verified** (percent→fraction correct, 1/√E + intrinsic floor, unbiased smear,
  integration before InWindow, null path byte-identical); used a separate front-end RNG per Codex's replay caveat.
- **DCR as background** (extension): `FrontEndConfig.DarkCountRateHz` + `IntegrationTimeNs` add a SiPM
  dark-count term. Dark p.e. accumulated in the integration window (variance = ENF·DCR·τ_int) add a
  **PARALLEL-noise** contribution `R_dcr(E) = 2.355·√(ENF·DCR·τ_int)/N_pe(E)` that scales **1/E** — distinct
  from the 1/√E statistical term — folded in quadrature. Honest magnitude: at 1 Mcps/200 ns it's **~0.014 %
  @662 vs ~0.078 % @122** (negligible at the photopeak, concentrates at low energy) — matching the design-layer
  "DCR matters only for weak/low-line sources" finding. `montecarlo frontend` prints the energy sweep.
  Codex-verified (correct 1/E parallel-noise, units, magnitude; caveat noted — an equal-width baseline gate
  would ×√2). Default 0 = backward compatible.
- `FrontEndTests` (budget @662 = 8043 pe → 4.30 %, 1/√E scaling, unbiased smear, tight-window counts drop with
  the front-end; DCR 1/E scaling + low-energy concentration). **50 tests green.** Reproduce: `montecarlo frontend`;
  enable in a scenario via `detector.frontEnd` (with optional `darkCountRateHz`).

## 33. CR-RC^4 shaper in RTL — `rtl/crrc_shaper.sv`
The classic semi-Gaussian shaper (theme 31's Python CR-RC) now in synthesizable RTL, the companion to the
trapezoidal shaper (theme 25). Two stages: **pole-zero deconvolution** of the exp tail (`imp = x − A·x[-1]`,
A = exp(−1/τ) in Q16) → an impulse, then **ORDER cascaded single-pole RC low-passes** (`acc += (u−acc)·K`,
K = 1/τ_s in Q16, each stage feeding the next same sample) → a Gamma-shaped semi-Gaussian whose PEAK ∝ energy.
- **Bit-exact** to `trap_ref.crrc_int` (Python `>>` = SV signed `>>>`): cocotb `test_crrc.py` drives a single
  pulse and a piled pair → **0 mismatches**, and confirms the peak is linear in energy (400→800 keV doubles).
  Icarus/Yosys-friendly (plain parameters, `integer` loop vars). **7 cocotb tests now** (trap ×3 both tops,
  BLR, CR-RC ×3).
- **Cusp left as the Python theoretical benchmark** (theme 31): a digital cusp is a ~19-tap FIR (MAC + coeff
  ROM) and is rarely used in real FPGA DAQs — the recursive shapers (trapezoid, CR-RC) are the practical ones;
  theme 31 already establishes the cusp's near-optimal-ENC benchmark. So the RTL shaper family is
  trapezoid + pipelined-trapezoid + CR-RC (+ baseline restorer), with the cusp as the paper reference.

## 34. WPF viewer app + native C# waveform + interactive source localization — `src/Gcam.Wpf`
A ScottPlot WPF app (`Gcam.Wpf`, net9.0-windows) that RUNS the sim interactively — a scene
editor over the tabs plus Waveform / Imaging / Spectrum / Optics tabs. The build arc surfaced real physics
(several corrected my own mistakes — GUI can't be self-verified, so every claim was MC-checked):
- **Native C# waveform** (`Detector/Waveform.cs`): bi-exp rasterizer + trapezoidal & CR-RC^4 shapers + BLR +
  ADC presets, **bit-exact to the Python RTL reference** (signed `>>` = SV `>>>`), cross-checked against
  Python golden values (`WaveformTests`, 8). CR-RC pole-zero constant `round(e^-0.2·65536)=53656`. **FIXED
  (Codex-flagged): the RTL default + cocotb param carried the wrong `53667`** (a stale value ≈ tau 5.005; the
  cocotb test read A from the DUT so it was self-consistent but masked the drift) — realigned `crrc_shaper.sv`,
  `run_cocotb.py`, `trap_ref.py` to `53656` so **C# ↔ RTL CR-RC is now truly bit-exact** (7 cocotb tests re-run green).
- **One acquisition drives all tabs**: ΔN detected events/tick feed the flood map, energy histogram, and
  scope from the SAME stream (not one sim per tab); only the visible tab renders. Live count accumulation
  (Poisson on a normalized shape), realistic detector energy resolution on the spectrum (1/√E), a photopeak
  window PER emission line.
- **Per-source distance** (`DefaultSimulationFactory` honours `Position[2]>0`): real 1/r² efficiency + depth
  defocus (`SourceDistanceTests`).
- **Multi-source localization**: needs a NON-CYCLIC decode (cyclic aliases off-axis into ghosts) + the
  detector must span ~ONE mask period (`det ≈ rank·cell·srcDist/S`) — my first "Sharp" preset undersized the
  detector (12 mm vs 18 mm period) and wrecked off-axis decoding (3-source worst error 14 → 0.29 mm once
  fixed). Optics tab exposes rank/cell/D/detector with a live readout (resolution, FCFOV, Nyquist, period
  coverage) + presets. `MixedFieldStudy.TopPeaks` marks every source.
- **Depth is weak by physics**: a coded aperture gives DIRECTION exactly; distance only comes from
  depth-from-focus (`BestFocalMm`/`LocalizeDepths` — sharpest-plane = distance, metric = peak SNR, comparable
  across planes; raw peak height is not). Depth FWHM grows ~z^1.5 and the ±10% 3D range is only ~0.15 m for
  the 18 mm mask (`DepthDesignStudy` / `montecarlo depthdesign`): range extends with aperture only via MORE
  CELLS (higher rank), not a coarser pitch (aperture gain cancels the lateral-resolution loss). So a handheld
  3D-locates only near/weak sources; far needs a big mask.
- **Realistic ~1 m default** (was ~16 cm): source distance 1 m, focal/distance sliders 200–3000 mm, canvas
  auto-fits the FCFOV (grows ∝ distance), activity default 500 µCi (a 1 m source is ~40× dimmer, 1/r²). At
  1 m the optics still localize laterally to ~1 mm (`RangeLocalizationTests`); depth stays coarse.
- **Rangefinder + focus fusion** (matches the real instrument's laser module): the focal control IS the
  external rangefinder range (aperture gives direction, this scales it to position); `MixedFieldStudy.CheckFocus`
  cross-checks it against depth-from-focus — near it refines the range, far it only flags "target off the laser
  surface" (their error modes are orthogonal: laser ranges the surface it HIT, focus measures the SOURCE).
  `FocusFusionTests`.

## 35. Spectrum & detector realism — make the WPF Cs-137 look like a real measurement, all from physics
An iterative arc driven by "this looks too ideal", each step MC-verified; the recurring principle: **fill features
from real physics, never hand-add a tail/grass**. Several of my proposals were wrong and got corrected by the data
or the user (who built the real rig).
- **Cs-137 Ba K X-rays** (`Scene.cs` isotope lines): 32.1 keV (Kα, 5.6%) + 36.4 keV (Kβ, 1.4%) are a real SOURCE
  emission (internal conversion of the 662 transition), not environmental — belongs in the line list. 661.7 stays
  `Lines[0]`. Gives a genuine low-E peak.
- **The spectrum was never wrong, the DISPLAY was**: the raw MC deposit spectrum already had ~29% Compton continuum
  with the 477 keV edge and the empty edge→peak gap. It looked "artificially clean" only because a LINEAR y-axis
  buries a continuum ~20× under the photopeak. Added a log/linear toggle (default linear — log felt like a trick to
  the user) + 256-bin MCA-style filled-line render (was 128 fat bars).
- **`EntranceAbsorber`** (`Detector/`, opt-in `Detector.EntranceAbsorberMm`, NIST iron μ(E) table): source
  encapsulation + detector window as a stainless-steel slab. Two roles: (1) energy-dependent attenuation tames the
  32 keV X-ray from ~17%→~8% peak area while barely touching 662 (0.15 mm default, sweep-picked); (2) `Interact()`
  is a real Klein-Nishina SCATTERER — forward small-angle scatters fill the photopeak's low-E tail (the edge→peak
  valley, 1.1%→~4%), soft X-rays that interact are mostly photo-absorbed. A **backing scatterer** behind the crystal
  (`Detector.BackingScatterMm`, ~180° backscatter of through-going photons) makes the ~184 keV backscatter peak.
  Valley/backscatter magnitudes scale with the surrounding material. (`EntranceAbsorberTests`, `ScattererTests`.)
- **Detection-chain component presets** (WPF Waveform tab, `FrontEnd.cs` — same datasheet-derived idea as the ADC
  preset): pick a **scintillator** (GAGG/NaI/LYSO/CsI/BGO) + **photosensor** (Hamamatsu MPPC S13360-3050/-6050, PMT)
  + **preamp**. The energy resolution is DERIVED via the existing `FrontEndModel` (`N_pe = lightYield·collection·PDE·E`
  → 1/√E statistics; the scintillator's NON-PROPORTIONALITY floor is the intrinsic term, NOT the total resolution,
  so a good crystal + poor sensor still resolves badly — GAGG+MPPC 4.6% vs NaI 6.4% vs BGO 8.8% @662). Scintillator
  decay sets the pulse rise, preamp tail the fall (min/max of the two constants so the bi-exp never inverts); shaper
  pole-zero matched to the tail (RTL golden τ=5 untouched — it's the default). Selecting parts changes waveform AND
  photopeak width together. On the default GAGG the visible change is small; the payoff is swapping detectors.
- **Per-crystal LLD/ULD is a PHOTOPEAK WINDOW, not a global low-E cut** (I first built the latter — wrong; user
  corrected). WPF imaging now builds the flood via `ComptonFactory(PerPixelWindow, primaryLine, windowFrac)`: only
  photopeak events (per pixel, gain-corrected) form the coded image (~22% of interactions kept; localization still
  0.09 mm). Window half-width is **FWHM-based**: `windowFrac = (Peak window ±×FWHM, default 1.5) × derived FWHM`, so
  a coarser detector lets more scatter into the window. The full spectrum is still shown; the red ROI band marks the
  imaging window.
- **Seeded per-crystal gain spread** (Imaging tab `GainSigma` + `UniformitySeed`, via existing `CrystalUniformity`):
  each pixel's 662 lands slightly off-window so off-gain crystals lose part of the photopeak — the crystal-to-crystal
  non-uniformity a real rig flood-corrects. Seed → reproducible pattern; different seed → different detector.
- **The low-E "noise wall" was the user's real-rig noise, not gamma physics**: our stark photopeak-to-low-E contrast
  is CORRECT for a near-ideal detector (we count only real deposits). A real (SiPM) rig fills the low-E from
  electronic/EMI noise + dark counts crossing the trigger, source-INDEPENDENT so it dominates at low activity. Added
  a "Noise floor (cps)" knob (a device value, honestly NOT datasheet-derived) for a falling low-E wall. Also resolved
  the DCR puzzle: SiPM dark pulses are ~0.1 keV (1 PE) — invisible in the WAVEFORM at gamma scale (so the clean
  baseline is right), but they show up in the SPECTRUM as the low-E wall.
- **Dead region (`Detector.ReflectorGapMm`)**: the reflector / saw-kerf gap between crystals — a photon entering the
  gap is lost, cutting counts by the fill factor ((pitch−gap)/pitch)² (verified 69.4% theory → 69.8% detected) and
  stamping a periodic pattern on the flood. New **Detector tab** renders the detector face at sub-pixel resolution:
  per-crystal gain (colour) + reflector gaps (dark). SiPM↔crystal is modelled 1:1 (each crystal = a readout channel;
  the SiPM's light budget is lumped into `FrontEndModel`, not a separate spatial matrix) — the right choice for a
  pixelated coded-aperture camera with per-crystal LLD/ULD (light-sharing/Anger would fight that windowing).
- **Codex parallel cross-verification** (4 topics, `33b79c7..`): core physics all confirmed; fixed backing back-scatter
  re-entry (actual back-face crossing, not the overshoot; respect the reflector gap; require the scattered photon to
  survive escaping the backing) and a couple of display/clamp nits.
- **Optical inter-crystal crosstalk, COUPLED to the reflector** (`Detector.OpticalCrosstalkFraction`): an imperfect
  reflector leaks scintillation LIGHT to the 4 neighbours (main keeps 1−leak, leak/4 each; edge light lost; per-event
  leak uniform [0, 2·contact] so efficiency rolls off gradually, not a cliff). This is what the reflector prevents
  (vs the dead-region geometry). With a per-crystal LLD/ULD readout that does NOT sum clusters, leaked light drops the
  main channel below its window → crosstalk costs photopeak COUNTS while leaving position UNBIASED (why isolation
  matters; tighter windows are more crosstalk-sensitive). Total light conserved → the total-energy spectrum is
  unaffected; it bites imaging. **Crucially, crosstalk is coupled to reflector THICKNESS**: effective = contact ×
  exp(−gap/λ), λ≈40 µm — so ONE reflector parameter drives both the dead region (fill) and the isolation (crosstalk),
  and an OPTIMUM gap emerges (at 40% contact, windowed efficiency 9/13/20/69/45% at 0/20/40/100/200 µm — thin lets
  crosstalk dominate, thick lets dead area dominate, best ~100 µm). Reviewer (user) caught that they were wrongly
  independent. **Reflector MATERIAL** (ceramic — the real rig — vs 3M ESR / PTFE / BaSO₄ / TiO₂, reflectance ~90–99%)
  is deliberately NOT modelled: it enters only as a second-order tuning of λ (reflectance) and of whether gap-entering
  gammas are fully lost vs partly transmitted/scattered (Z/density) — not a first-order factor for the localization.
- **SiPM readout pitch / crystal↔SiPM matching** (Detector-tab "SiPM pitch"): = crystal pitch is 1:1; > crystal pitch
  is light-sharing (one SiPM reads a block of crystals), modelled by AVERAGING each SiPM block of the flood before the
  decoder — sub-block position is lost, so the flood is read at the SiPM granularity (Detector tab overlays the SiPM
  grid). This makes the match a first-class constraint: the SiPM pitch must still Nyquist-sample the MASK SHADOW or the
  coded decode fails — for a 0.6 mm crystal / 0.7 mm cell, localization holds at 0.6/1.2 mm SiPM then breaks to 11/15 mm
  at 2.4/4.8 mm. So a fine coded aperture needs a fine READOUT (fine SiPM array / sub-pixel light-sharing), not a coarse
  3 mm MPPC over a big block — which is why the 0.6 mm-crystal / 3 mm-MPPC lump is only a datasheet convenience, not a
  buildable 1:1. (The blockify is the no-Anger worst case; Anger centroiding could recover some sub-SiPM position but
  needs a light-spread model — not done.) SiPM is otherwise 1:1 implicit, its light budget lumped in `FrontEndModel`.

## 36. Thermal drift DURING an acquisition + flood-field correction — `ThermalDrift` / `ThermalDriftStudy` / `montecarlo thermal`
First of the "physical realism gaps" queue (Codex gap-review). The real rig's headache: SiPM cooling. We modelled
only STATIC per-crystal gain; this adds the TIME axis and asks what a per-crystal photopeak-window system loses as
the array's temperature drifts mid-acquisition — and what flood correction can and cannot fix.
- **The physics, no hand-tuning**: SiPM gain ∝ over-voltage = V_bias − V_breakdown(T); V_breakdown rises ≈ +21.5 mV/°C
  (Hamamatsu), so at a few volts of over-voltage **dGain/dT ≈ −0.7 %/°C** (`ThermalDrift.AlphaPerC = −0.007`). A gain
  change of `m(t)` walks the 662 photopeak centroid by `ε = m−1` relative to the FIXED calibration window, so the
  acceptance falls asymmetrically: `½[erf((w−ε)/√2σ) + erf((w+ε)/√2σ)]` (`CrystalUniformity.PhotopeakAcceptance`,
  a strict generalization — ε = 0 reduces to the old symmetric `erf(w/√2σ)`).
- **Two physically distinct temperature drivers** (the user flagged that ambient was missing — the rig is 거치형):
  **ambient** (room/HVAC) is spatially UNIFORM → walks every crystal together → a global efficiency droop, no flood
  non-uniformity; **self-heating** has a spatial GRADIENT (centre hotspot) + exponential warm-up → the walk differs
  across the face → a flood-correction RESIDUAL. `T_i(t) = T_amb(t) + ΔT_self·(1−e^{−t/τ})·shape(r_i)`.
- **Flood correction cannot save it, only bias-comp can**: the calibration map `S_cal` is a t = 0 snapshot; the
  acquisition-time sensitivity walks away from it, so dividing by the fixed `S_cal` leaves a residual that GROWS with
  time. A bias-compensation (temperature-compensation) loop that nulls a fraction of α is the only fix. Reusing the
  `UniformityStudy` "flood once, apply per-pixel" trick: the MC geometry flood is run ONCE, every time slice applies
  the analytic `S_i(t) = gain_i · acceptance(fwhm_i, w, ε_i(t))`.
- **Result** (`montecarlo thermal`, ambient +3 °C linear + self-heat +8 °C hotspot τ 0.3, over one acquisition):
  bias-comp OFF droops photopeak counts **100 %→90.6 % (−9.4 %)** and grows the flood residual **0→6.7 % CoV**;
  bias-comp ON (90 %) holds **99.9 %** efficiency, **0.1 %** residual. **Localization barely moves (~0.6 mm, the
  decoder floor) in either case.**
- **Independent confirmation of a prior claim**: the theme-22 note "SiPM thermal drift is an ENERGY-window not a
  position issue" was asserted from design; this MC reproduces it from first principles — drift is an efficiency /
  window-walk problem, and a smooth residual barely pulls the correlation peak. That is exactly why the real rig
  fought it with cooling / bias-comp (an energy-stability problem), not with a position recalibration.
- Flood-field correction (gap #2) was already latent in `UniformityStudy` (÷ calibrated sensitivity recovers static
  non-uniformity perfectly); the honest new content is that it is powerless against the TIME-varying part. (Tests:
  `ThermalDriftTests`, +6.)

## 37. Random-coincidence pile-up SUM continuum in the spectrum — `EventStreamStudy.ApplyPileUp` / `montecarlo pileup`
Second physical-realism gap. Pile-up already lived in the RTL waveform (the shaper sees overlapping pulses) but NOT
in the per-event ENERGY spectrum — the WPF Cs-137 spectrum had no two-events-summing continuum. The honest fix:
realize the pile-up in the energy domain by MERGING the timed MC event stream, never by hand-adding a tail.
- **The physics, from the real event stream**: `EventStreamStudy` already overlays a Poisson arrival process on the
  MC deposit list (it drives the cocotb shaper). `ApplyPileUp` walks that timed stream and records any events whose
  arrivals fall within the shaper's resolving time as ONE event with SUMMED energy (extending / paralyzable — a burst
  piles to 3-fold+). The energies are the real MC spectrum; only their time coincidence is added.
- **Resolving time is an EFFECTIVE value derived from the shaper**, not a free knob (same discipline as theme 36's α):
  `ResolvingSamples = rise + 2·tail` — the bi-exponential pulse's significant duration (peak + ~2 fall constants).
  Default 1 + 2·5 = 11 samples = 88 ns @ 125 MSPS; a faster shaper (shorter tail) resolves pile-up better, as in
  hardware. It is a shaper-coupled model value (the true pulse-pair resolution also depends on the peak picker,
  threshold and tolerated distortion). The RATE is the operating point (detected cps = emission × efficiency, the
  WPF `_liveRateCps`); throughput ≈ exp(−R·τ) is exact for this fixed-dead-time extending Poisson cluster model.
- **Result** (`montecarlo pileup`, Cs-137): throughput 99.6 %→84.7 % and photopeak-kept 99.3 %→73.4 % as the rate
  climbs 50 k→2 M cps (R·τ 0.004→0.176); the counts migrate UP into a **self-convolution continuum** — a 662+662 →
  1323 keV SUM peak, a 662→1324 plateau (662 + Compton edge), and Compton+Compton lower. Throughput tracks exp(−R·τ).
- **Cross-checked against the analytic pile-up math**: the piled excess above the line matches the AUTOCONVOLUTION of
  the singles energy spectrum (`singles ⊛ singles`) in shape — the leading (2-fold) random-coincidence term, which
  dominates the excess at these rates (R·τ ≲ 0.18, so 3-fold ~ (R·τ)² is sub-%). It is a shape cross-check, not an
  identity: the full recorded spectrum is a 1/2/3-fold cluster mixture. Energy is conserved (merging sums, never
  invents, counts).
- **WPF**: a "Pile-up" checkbox on the Spectrum tab applies the merge to the spectrum path only (the Waveform scope
  keeps singles — it renders its own time-domain pulse overlap), and the energy axis auto-extends past the 2× sum
  peak so the continuum is visible. Ties the spectrum realism to the operating count rate. (Tests: `PileUpTests`, +6.)

## 38. Mask fabrication tolerances — a mis-machined tungsten mask vs an ideal decoder — `MaskFabrication` / `MaskFabricationStudy` / `montecarlo maskfab`
Third physical-realism gap, user-flagged (★): tungsten is hard to machine, so a real mask is not the ideal MURA the
decoder assumes. The user's key steer: **model the CONSERVATIVE, usable tolerance, not the best-case a machine can
momentarily hit** — "machinable" ≠ "deployable", especially for a thick brittle tungsten slab.
- **`MaskFabrication`** (Masks project) stamps a fixed, seeded per-cell geometry error on the mask Transmit ONLY —
  the decoder keeps decoding the ideal pattern, so it is a forward-model mismatch. Four errors from one tolerance
  scale σ: **hole placement** jitter (σ), **hole size** jitter (0.7σ, its complement is the tungsten web width),
  **blocked cells** (brittle tungsten fails to open, rate climbs with σ), and **depth drill WANDER** (2.5σ — the
  high-aspect-ratio bore is not a clean straight channel through a thick slab; the hole centre drifts front→back, so
  a straight ray is clipped where the bore has wandered away). Wander is injected at the existing slab ray-march
  (`frac` depth), so it needs no new machinery — it is a per-depth hole-rectangle test. Requires a web (holeFraction
  <1, set to 0.71 = ρ≈0.5) for placement/size to have room to move.
- **Study framing = usability THRESHOLD**, averaged over several manufactured masks (fab seeds) so it is the typical
  mask, not one lucky draw. Sweep σ, decode with the IDEAL decoder, measure localization RMS, efficiency, and the
  reconstruction **PSR = (peak−mean)/std** (coded-shadow fidelity — more sensitive than peak/second-sidelobe).
- **Result** (1 mm pitch, 10 mm thick W, aspect ≈ 1:14, centered Cs-137): RMS holds the ideal-mask floor (~0.95 mm)
  to **σ ≈ 40 µm**, then degrades — 1.9 mm at 80 µm, 3.6 mm at 160 µm; efficiency 100 %→88.7 % (blocked cells 0→4 %
  + shrunken/wandering holes); PSR 4.4→3.8. So the **manufacturing requirement is σ ≲ 40 µm** to stay within ~2× the
  ideal floor — a conservative, actionable number, well below the ~1 mm pitch (a few-% of a cell).
- Burrs / rounded corners fold into the hole-size error; slab WARPING (a global bow → position-dependent shift) is a
  distinct structural defect, deliberately deferred. (Tests: `MaskFabricationTests`, +5.)

## 39. Mask–detector alignment / pose error — `CodedApertureMask` pose + `AlignmentStudy` / `montecarlo align`
Fourth physical-realism gap (HIGH). The decoder back-projects the IDEAL geometry (mask centred at its nominal
plane, unrotated); a rigid-body mis-registration of the real mask casts a systematically shifted/rotated coded
shadow → a SYSTEMATIC localization bias, not just scatter. The other big localization gap after fabrication.
- **Implementation is a single ray transform**: at the top of `Transmit`, express the incoming ray in the mask's
  OWN (nominal) frame — `p_nom = Rz(−roll)·(p − offset)` — so the entire existing slab march (which assumes an ideal
  centred mask at PlaneZ) runs unchanged and composes automatically with focal/taper/fabrication. The decoder,
  still built from the nominal geometry, is unaware of the pose → the mismatch IS the bias. `MaskOffsetX/Y/Z`,
  `MaskRollDeg` on the config; the equivalence (offset mask at p ≡ ideal mask at p−offset; rolled ≡ Rz(−roll)·p) is
  unit-tested directly.
- **Three DOF, three distinct signatures** (swept independently, off-axis source near the FCFOV edge so the leverage
  shows): **in-plane offset** → a parallel bias AMPLIFIED by the magnification (D+S)/D (a 1 mm mask shift → ~2.5 mm
  source bias at D=60/S=100, ref 2.67×) — the dominant, most dangerous error; **spacing (z) error** → a radial
  magnification bias that grows off-axis (~0.4 mm at 2 mm dz); **roll** → a tangential bias that grows off-axis
  (~0.1 mm at 2°, zero on-axis). The aligned baseline sits at the ~sub-mm decoder floor.
- **Tolerance takeaway**: in-plane registration is the driver — the (D+S)/D amplification means the mount must hold
  the mask centre to a fraction of the wanted localization accuracy (≈0.4 mm offset for a ~1 mm bias budget here);
  spacing and roll are far more forgiving at practical off-axis distances. Pitch/yaw TILT (a tilted-slab geometry)
  is deliberately deferred, like warping. (Tests: `AlignmentTests`, +4.)

## 40. Bad (dead / hot) detector pixels + bad-pixel-map repair — `DetectorDefects` / `DetectorDefectStudy` / `montecarlo defects`
Fifth physical-realism gap (MED-HIGH). We modelled per-crystal gain non-uniformity (theme 36) but not the discrete
channel defects a real SiPM array carries. **Dead** pixels (disconnected/failed channel) punch holes in the flood;
**hot** pixels (dark-count / breakdown runaway) add source-INDEPENDENT spikes — both imprint fixed structure that is
NOT part of the mask code, so the ideal decoder's correlation is pulled off the true peak.
- **`DetectorDefects`** (seeded dead + hot maps; a pixel is at most one kind). Applied post-hoc on the geometry
  flood — the "flood once, apply per-pixel" trick (same as `UniformityStudy`), so no MC re-run per level. Dead →
  count set to 0; hot → +HotFactor×(mean live-pixel counts). The decoder stays IDEAL.
- **Repair = the discrete analogue of flood-field correction**: given the KNOWN bad-pixel map, replace every flagged
  pixel with the mean of its non-defective 4-neighbours (interpolate over the defects). Real systems keep exactly
  such a map from calibration.
- **Result** (12×12 array, hot = 5× mean, averaged over defect maps): the repaired decode holds the ~0.60 mm floor
  flat (0.60→0.68 mm) across 0→8 % bad pixels, while the RAW decode degrades and scatters (up to ~1.2–1.8 mm at 8 %;
  the intermediate points are noisy because a 12×12 = 144-pixel array has only ~1 pixel per %). So bad pixels DO pull
  the localization, and a known bad-pixel map recovers it — the same lesson as flood-field, for discrete defects.
- The 144-pixel grid makes single-% defect counts small and the raw curve seed-noisy; the robust, monotone result is
  the repair holding the floor. (Tests: `DetectorDefectTests`, +4.)

## 41. Mask tungsten fluorescence + Compton scatter — `MaskSecondary` / `MaskSecondaryStudy` / `montecarlo masksec`
Sixth physical-realism gap. The mask was PURE ATTENUATION — a photon hitting a closed cell either leaked through or
vanished. A real tungsten mask emits SECONDARIES: a photon that interacts in the tungsten Compton-scatters or (via
photoelectric absorption above the K-edge) fluoresces a W K X-ray, and some head to the detector.
- **`MaskSecondary`** decides the secondary at an interaction: sample W photoelectric-vs-Compton (log-log table, PE
  9.5 % @662), then either Klein-Nishina scatter (reusing `ComptonModel.Scatter`) or, above the 69.5 keV K-edge, a W
  Kα 59.3 / Kβ 67.2 keV X-ray (yield ω_K 0.958 × K-shell 0.88), isotropic. The X-ray energies are the real W
  characteristic lines — no hand-added line. W μ(E) is extended below 122 keV so the fluorescence self-absorption is
  right (μ(59 keV) ≈ 47× μ(662)).
- **`MaskSecondaryStudy`** is a focused slab MC (it does NOT touch the coded pipeline's Transmit): sample an
  interaction depth in the slab, decide the secondary, require it to head to the detector (exit the back face) and
  survive the escape self-absorption. Output = the arriving-energy spectrum, normalized to the open-cell primary flux.
- **Result** (662 keV on 10 mm W, ~49 % open): escaping secondaries ≈ **9 % of the coded primary flux**, and they are
  almost entirely **forward Compton scatter** (backscatter heads away from the detector). The arriving scatter runs
  ~290→662 keV and PEAKS near the primary — so its high-energy tail sits INSIDE the photopeak window and the energy
  window canNOT reject it (a mildly mis-positioned imaging background); only the lower-energy scatter is rejected.
  (The ~9 % is a back-face hemisphere tally — "exits toward the detector side" — so it is an UPPER bound; a finite
  detector solid angle would collect somewhat less.)
- **W K-fluorescence is negligible at the detector** (< 0.2 % of the secondaries): a 59 keV X-ray in tungsten has a
  ~0.1 mm mean free path, and interactions are front-weighted, so almost none escape the 10 mm slab toward the
  detector. Honest result — the W X-ray lines exist but are self-absorbed away; the real mask effect is the forward
  scatter. (Tests: `MaskSecondaryTests`, +5.)

## 42. Counting-system dead time / count-rate saturation — `DeadTime` / `DeadTimeStudy` / `montecarlo deadtime`
Seventh physical-realism gap, the count-rate companion to pile-up (theme 37): pile-up SUMS overlapping pulses in the
spectrum; dead time is the counting THROUGHPUT loss while the DAQ processes each pulse. Reuses the same timed MC event
stream (`EventStreamStudy`), so the loss comes from the real Poisson arrival statistics.
- **`DeadTime`** applies the two classic models to the stream: **non-paralyzable** (dead for τ only after a RECORDED
  event → m = R/(1+Rτ), saturates at 1/τ) and **paralyzable** (EVERY arrival restarts the dead period → m =
  R·exp(−Rτ), peaks at R=1/τ then collapses — the system paralyses at high rate).
- **`DeadTimeStudy` / `montecarlo deadtime`** sweeps the true rate, applies both filters to the stream, and reports
  the recorded rate + **live fraction (= recorded/true = the live-time-vs-real-time correction)**, cross-checked
  against the analytic formulas.
- **Result** (τ = 1 µs, 1/τ = 1 Mcps): the MC recorded rate sits exactly on the analytic curves. Non-paralyzable
  saturates toward 1 Mcps (912 kcps at 10 Mcps true); paralyzable peaks at 1 Mcps (366 kcps ≈ R/e) then COLLAPSES
  (32 kcps at 5 Mcps, 550 cps at 10 Mcps). Live fraction falls 99 %→9 % (non-para) / 99 %→0 % (para) across R·τ
  0.01→10. The classic dead-time curves, reproduced from the event stream. (Tests: `DeadTimeTests`, +5.)
- **Codex cross-verification** (gpt-5.5, read-only): both filters CORRECT — non-paralyzable `deadUntil` semantics
  and `>=` boundary (live at exactly t+τ), paralyzable `prev`-update-every-arrival and `>τ` boundary, unit chain
  (µs→s→ADC samples), and the τ=0 / empty-stream / extreme-Rτ edge cases all sound. One latent limitation found and
  FIXED: the analytic comparison used the nominal source rate `r`, but if `config.Background` (BSR/DCR) is on,
  `EventStreamStudy` merges extra arrivals the DAQ actually sees — so the study now derives the **effective** arrival
  rate from the generated stream (nTrue over its span) for R·τ, recorded, and analytic. Source-only (the normal case)
  is unchanged to within 1/nTrue; background-on is now correct too.

## 43. Sub-cell peak interpolation — `PeakInterpolation` / `SubCellStudy` / `montecarlo subcell`
A precision WIN (not a physics gap): the decoder samples the source plane on a grid of step `ReconStepMm` and
`FindPeak` reported the **integer argmax cell**, so the estimate was quantized to one cell — a deterministic sawtooth
of amplitude ±step/2, **RMS = step/√12, independent of counts**. No amount of statistics beats it; only a finer (more
expensive, O(n²)×pixels) grid did. Interpolating the correlation-peak SHAPE around the argmax recovers a fractional
offset instead.
- **`PeakInterpolation`** (Decoding): separable 3-point estimators returning δ∈[−0.5,0.5] per axis — `Tent` (matched
  to the MURA autocorrelation core, **exact** for an ideal tent: δ=(f₊−f₋)/(2(f₀−f₋))), `Parabolic` (vertex of a
  quadratic; biased toward the cell for a tent — returns u/(2(1−|u|))), `Gaussian` (log-parabola; needs positive
  samples, falls back to parabolic near the ±1 array's negative sidelobes), `None`. All use DIFFERENCES so they
  tolerate the signed sidelobes (unlike a centroid). Wired opt-in on `CrossCorrelationDecoder` + `DecoderConfig`
  (`SubCellInterpolation`, default **Tent**); the decoder default stays `None` so direct-construction tests are
  unchanged; the factory reads the config.
- **`SubCellStudy` / `montecarlo subcell`**: sweeps a point source across the grid in sub-cell steps (Y on-axis) and,
  from ONE high-count (low-noise) mean image per position, decodes with each estimator — lifting the quantization
  sawtooth above the residual Poisson noise — over a range of recon steps.
- **Method choice was decided by the DATA, not a prior** (Codex design consult first flagged tent over Gaussian; the
  MC confirmed and refined it). RMS(mm) vs step, default geometry: interpolation beats the argmax at **every** step,
  and the gain GROWS as the grid coarsens (where the floor is largest) — step 1.8 mm: none 0.52 → tent **0.13 (4×)**;
  step 2.4: 0.66 → 0.19. **Tent is the most robust** (best/tied at 0.6/0.9/1.8/2.4 mm); parabolic edges it only at an
  intermediate 1.2 mm (rounded apex favours the quadratic) and degrades at coarse steps (2.4: parab 0.26 vs tent 0.19);
  Gaussian tracks tent but also degrades coarse. The honest headline: sub-cell interpolation lets you run a COARSE
  (cheap) recon grid and still localize ~3–4× better than the argmax floor. **Gaussian (what the real hardware used) =
  parabolic in log-space**; both are matched only to a bump-shaped peak, which this signed-correlation tent is not.
- Regression: defaulting the pipeline to Tent shifted one pinned bias value in `BackgroundTests` by ~0.0005 mm — the
  gradient's low-frequency residual, which the integer argmax hid, is now resolved sub-cell (loosened bit-identical →
  <0.01 mm). (Tests: `SubCellTests`, +9 — estimator math exact-cases + MC floor-beating.)
- **Codex cross-verification** (gpt-5.5, read-only): estimator MATH and decoder WIRING CORRECT — tent derived exact
  for an ideal tent (both u≥0 and u<0 branches), parabolic sign/guard right, Gaussian = log-parabola exact for a
  Gaussian and scale- (not pedestal-) invariant, argmax→estimate axis mapping consistent, no sign/off-by-one. Minor
  fixes applied from its notes: parabolic tent-bias generalized to u/(2(1−|u|)) (was written for u≥0 only; +a negative-u
  test); `Bias*` renamed `Mae*` (it is mean-|error|, not signed bias); "noise-free" softened to "high-count low-noise";
  `SubCellStudy` source depth pinned to the nominal decode plane (was reading `Position[2]`, which could desync the sim
  and decode planes). The "tent most robust" claim is backed by the 5-step MC sweep, not asserted in the unit test
  (which proves only that interpolation beats the argmax and tent beats the floor).

## 44. True (cascade) coincidence summing — `DecayScheme` / `CascadeSummingStudy` / `montecarlo cascade`
First target from the **physics-realism audit** (5 Codex subsystem audits over themes 1–43; this was the source-audit's
top structural gap): the sim emitted ONE photon per history, so two gammas from the SAME decay were never correlated.
Real cascade isotopes emit coincident gammas that, if BOTH deposit in the crystal, SUM into one recorded event — a sum
peak plus a loss of single-photopeak counts (summing-out). This is the opposite of random pile-up (theme 37, DIFFERENT
decays, ∝ rate): cascade summing is **rate-INDEPENDENT and ∝ ε²** — a geometric effect that grows as the source nears
the camera. (Activates the `EmissionLine.CascadeCoincident` flag that was reserved-but-informational.)
- **`DecayScheme`** models one decay's correlated gammas WITH angular correlation: Cs-137 (single 662, no partner),
  Co-60 (1173+1332 cascade, weak W(θ) → independent isotropic), Na-22 (1275 + a β⁺ annihilation pair of 511s emitted
  **back-to-back**). Each gamma is transported through the crystal with the SAME physics the main detector uses
  (`ComptonModel`: μ(E), photoelectric-vs-Compton, Klein-Nishina), so the sum peak and its continuum are REAL energy
  deposition, not a hand-added line.
- **`CascadeSummingStudy` / `montecarlo cascade`** sweeps source distance (varying ε) and reports single-photopeak vs
  sum-peak yield per decay. **The sum yield scales as the SQUARE of the single yield** — log-log slope **2.04** (Co-60),
  confirming the ∝ε² GEOMETRIC scaling. (A distance sweep alone does NOT separate cascade from random pile-up, which
  also scales ∝ε² in geometry; the true discriminator is that cascade summing is rate/activity-INDEPENDENT per decay.
  Here the pair is same-decay by construction, so this is cascade summing.)
- **Results** (default geometry, ±7 mm detector): Co-60 shows a clean **2505 keV sum peak** sitting ABOVE both Compton
  edges; slope 2.04 = ∝ε². Cs-137 → **exactly 0 summing** (single line — the honest null that confirms the backlog's
  "∝ε² so small" note). Na-22 → the back-to-back 511s can't both reach a one-sided detector, so the **511+511 (1022)
  sum is suppressed** and 511+1275 (1786) dominates; and 1022 sits on the 1275 Compton edge, so it is single-photon
  contaminated anyway. **Honest metric correction found during MC**: a sum window only proves ∝ε² coincidence if it lies
  ABOVE the highest single line (else a single photon's Compton continuum leaks in as a ∝ε event) — the study restricts
  the coincidence metric to sums above max(single line), which turned Na-22's slope from a spurious 1.1 into a true 2.1.
- Magnitude is genuinely small (sum/decay ~1e-5 even at 18 mm), confirming cascade summing is a minor effect for a
  coded-aperture camera's small solid angle — real, ∝ε², but second-order. (Tests: `CascadeSummingTests`, +5 — decay
  scheme correlation incl. back-to-back 511s + MC ∝ε² slope + Cs-137 null.)
- **Codex cross-verification** (gpt-5.5, read-only): physics broadly CORRECT — no sign error, no back-to-back mistake,
  no double-counting; decay schemes/branches, the `t=dist/(−dir.Z)` acceptance geometry, and the clean-sum metric all
  sound. Framing corrections applied: (i) **slope-2 does NOT by itself distinguish cascade from random pile-up** —
  pile-up also scales ∝ε² in a distance sweep; the real discriminator is rate-independence (fixed the "no random
  process reproduces this" overclaim in docs/CLI/tests). (ii) `CrystalDeposit` enters at NORMAL incidence only, so
  absolute yields are approximate (scaling/peak position unaffected) — now documented. (iii) Co-60 W(θ) is A₂≈0.10
  (~10% close-geometry correction), not "few %"; noted as an ignored approximation.

## 45. Mask forward-scatter folded into the coded image — `MaskScatterStudy` / `montecarlo maskscatter`
First target from the physics-realism audit's mask subsystem (audit item B): the mask `Transmit` is binary — a primary
either passes (open cell / straight leakage, full energy) or is absorbed. But a primary hitting a CLOSED tungsten cell
can Compton-scatter FORWARD (lower energy, small angle) and reach the detector as a blurred pedestal/halo AROUND the
true shadow, which the decoder's forward model doesn't expect. Theme 41 tallied that scatter as an escape SPECTRUM (a
hemisphere upper bound); this study folds it into the actual FLOOD by real photon transport and measures the IMAGING
impact + energy-window mitigation.
- **`MaskScatterStudy` / `montecarlo maskscatter`**: biased point source → mask cell → primary OR a transported
  mask-scatter photon (Compton via the shared `MaskSecondary`/`ComptonModel`, self-absorption escape, landing on the
  detector). Decodes THREE variants — primary, primary+scatter, primary+scatter after a ~±10% arriving-energy window
  (a proxy for the detector photopeak window) — over a sweep of the mask–detector gap. Self-contained (does not touch
  the main `Transmit`); the mask is a SIMPLIFIED ideal slab (mid-plane cell lookup, no hole-fraction/taper/fab), and
  deposits go straight into the flood (no crystal stopping-power/resolution). The source proposal covers the whole-mask
  back-projection so the scatter contamination is unbiased.
- **Honest result: mask forward-scatter is a MINOR, doubly-suppressed contaminant.** (1) GEOMETRY: over the mask→
  detector gap a wide-angle scatter drifts laterally off the small detector, so contamination falls from **2.8 % at a
  12 mm gap to 0.21 % at 65 mm** — the nominal geometry's finite-detector value is ~30× BELOW theme 41's ~9 % hemisphere
  upper bound. (2) DECODER: the balanced MURA ±1 array correlates the smooth scatter pedestal to ≈0, so the coded-image
  contrast (peak/secondary) barely moves even with a few-% pedestal.
- **The surviving scatter is the hardest to reject**: what reaches the detector is small-angle forward Compton (mean
  ~613 keV, **~75 % INSIDE a ±10 % window**), because large-angle scatter both down-shifts MORE and drifts off the
  detector. So the window removes only the down-shifted part — **~39 % at a 12 mm gap, falling to ≈0 at wide gaps**
  (where only near-forward in-window scatter survives the drift); the in-window forward tail is irreducible — exactly
  theme 41's point, now quantified as an imaging contamination. (Tests: `MaskScatterTests`, +3 — contamination vs gap,
  window removes the down-shifted part, balanced-decoder pedestal rejection + valid localization.)
- **Codex cross-verification** (gpt-5.5, read-only): geometry/sign conventions CORRECT — mid-plane crossing, front-face
  hit, truncated-exponential interaction depth, back-face escape path, detector landing, KN reuse all sound. One real
  METHODOLOGY bias fixed: the biased proposal originally aimed only at the detector, so mask-scatter from primaries
  whose STRAIGHT path would miss the detector was outside the support (contamination biased). Now the proposal covers
  the whole-mask back-projection → unbiased (contamination refined 3.4→2.8 % at 12 mm). Framing corrected per its
  notes: simplified ideal-slab mask scope, arriving-energy (not measured-detector-energy) window, confidence is an
  internal contrast proxy (not a full SNR), and window-removal is gap-dependent (~39 % close → ≈0 wide), not a flat
  "~25–35 %".

## 46. Scintillator non-proportionality — the intrinsic-resolution COMPONENT from first principles — `NonProportionality` / `NonProportionalityStudy` / `montecarlo nonprop`
Next physics-realism-audit item (crystal subsystem, item D): the front-end sets the crystal's intrinsic resolution as
a hand-tuned CONSTANT (`FrontEndConfig.IntrinsicResolutionFwhm`, "GAGG ~0.05"). Its real origin is NON-PROPORTIONALITY:
the light yield per keV, nP(E), varies with the depositing ELECTRON's energy. A full-energy gamma deposits through a
Compton CASCADE of electrons whose composition varies event-to-event (a single photoelectron vs a recoil + a
photoelectron + …), so the total light Σ Eᵢ·nP(Eᵢ) fluctuates even though Σ Eᵢ is fixed at the photopeak — THAT is the
intrinsic resolution, with zero photon-counting noise.
- **`NonProportionality`** carries representative literature electron-response curves normalized to 1.0 at 662 keV:
  `NaI` (strong low-energy light deficit), `CsI` (intermediate-energy excess), `Gagg` (comparatively proportional,
  a few %), and `Proportional` (nP≡1). Not spectroscopic-grade — the shapes drive the mechanism.
- **`NonProportionalityStudy` / `montecarlo nonprop`** transports the cascade (the SAME `ComptonModel` the detector
  uses), applies every crystal's nP to the SAME cascade, and reports the intrinsic photopeak FWHM = FWHM(Σ Eᵢ·nP(Eᵢ))
  over full-energy events, per energy. Self-contained (does not change the detector).
- **Results**: the intrinsic resolution EMERGES from the physics and is **exactly 0 for a proportional crystal** (the
  mechanism check — the whole effect is non-proportionality, nothing else) and is **ENERGY-DEPENDENT**, which a constant
  floor cannot be: NaI **2.5 % @122 keV → 0.6 % @1332** (its low-energy deficit shrinks as high-energy electrons
  dominate), GAGG ~0.7–1.5 %, CsI ~2–2.9 %. There is also a non-proportional PEAK SHIFT / energy non-linearity (lines
  land off their true keV because nP is normed to 662; CsI +5 % @122 keV).
- **Honest decomposition**: GAGG's derived electron-non-proportionality is only **~1.5 % at 662** — well BELOW the
  config's hand-set ~3–5 % intrinsic floor. So most of that floor is OTHER (light-collection non-uniformity, Ce
  concentration, SiPM), not electron non-proportionality — the study separates the two. GAGG being comparatively
  proportional is exactly why this camera's crystal is a good choice. (Tests: `NonProportionalityTests`, +4 — response
  curves, proportional→0, energy dependence, GAGG < CsI.)
- **Codex cross-verification** (gpt-5.5, read-only): physics CORRECT — Welford variance, FWHM = 2.355·σ/mean, the
  ÷mean normalization, full-energy identification, and the shared-cascade variance reduction all sound; nP tables sane
  and 662-normalized; proportional→0 by construction. Framing tightened per its notes: the reported FWHM is the
  non-proportionality COMPONENT, not the whole intrinsic floor (already the finding's point, now labelled so in
  study/CLI/records); `PeakShift` is a RAW pre-calibration offset (a real Cs-137 calibration zeroes 662); the 662 CSV
  is the full pulse-height spectrum (photopeak + continuum). One documented SIMPLIFICATION: photoelectric absorption
  deposits the whole remaining energy as one electron (no shell-binding + Auger/X-ray sub-cascade), so the
  photoabsorption-event spread is a slight UNDER-estimate — the derived component is a lower bound.

## 47. Finite source size + capsule self-attenuation — `FiniteSourceStudy` / `montecarlo finitesrc`
Physics-realism-audit item (source subsystem, item E): the source was an ideal mathematical POINT. Real sources are a
finite active pellet in a sealed capsule. Two effects:
- **Source SIZE → reconstruction blur, then washout.** `FiniteSourceStudy.Run` builds the coded flood by transporting
  photons from emission points sampled inside a sphere (diameter swept) and decodes it. The reconstruction peak is the
  point response CONVOLVED with the source's projected profile, so the source blur adds roughly in QUADRATURE (a
  small-blur heuristic): negligible while the source is below the point resolution (≈2.5 mm here), then growing —
  **FWHM 2.50 mm (point) → 2.75 mm at 4 mm dia**.
  The dramatic effect is on CONTRAST (peak/secondary): it falls monotonically (1.18 → 1.09 at 4 mm → ~1.03 at 5–6 mm),
  and once the source approaches the coded resolution the shadow **WASHES OUT** — contrast → 1 (no peak above the
  sidelobes) and localization is lost (bias jumps to several mm). A hard source-size limit, not just a blur.
- **CAPSULE self-attenuation** (`CapsuleSweep`): gammas escaping the pellet + capsule attenuate ∝ exp(−μ·path) with an
  energy-dependent μ. The 662 keV primary mostly survives a few mm of steel (T 0.97 → 0.77 at 4 mm), but a **32 keV Ba
  K X-ray is killed far more** (T 0.61 → 0.014) — so a capsule strips the soft lines from a Cs-137 source's spectrum.
- Self-contained (does not change the main source/pipeline). The contrast (peak/secondary) is the clean washout tracker
  — the FWHM metric itself becomes meaningless once the peak dies (returns NaN on a missing half-max crossing). (Tests:
  `FiniteSourceTests`, +2 — blur→washout via contrast, capsule low-E suppression.)
- **Codex cross-verification** (gpt-5.5, read-only): geometry/transport CORRECT — `RandomInSphere` is volume-uniform
  (dir·radius·∛u), depositing at the detector landing `aim` is right (the source offset blurs the shadow through the
  mask-plane crossing, so no extra shift is needed), the `A·|cos|/(4πr²)` weight is correct, and there is NO
  source-support bias here (direct primaries land at `aim`); `RayExitDistanceFromSphere` and the mean exp(−μ·path) are
  correct. Framing tightened per its notes: the flood is the IDEAL binary zero-thickness coded shadow (not the full
  tungsten-slab response); "quadrature" is a small-blur heuristic (non-Gaussian projected sphere + z-defocus);
  "washout" is THIS argmax decoder's limit, not a fundamental information loss; the capsule sweep is a steel-EQUIVALENT
  normal thickness, not exact spherical-shell transport; and `PeakFwhmMm` now returns NaN (not a truncated width) when
  a half-max crossing is missing.

## 48. MLEM (Poisson-likelihood) reconstruction — `MlemDecoder` / `MlemStudy` / `montecarlo mlem`
The biggest physics-realism-audit item (reconstruction subsystem, item C): the only decoder was cross-correlation — a
single linear back-projection with the ±1 decoding array. It is fast but leaves NEGATIVE sidelobes, aliases off-axis
sources into a ghost, and MERGES nearby sources into one blurred peak that its argmax then picks. `MlemDecoder` adds
the statistical alternative: iterate the physical FORWARD model to the source distribution λ ≥ 0 that maximizes the
Poisson likelihood of the detector counts, `λ_j ← (λ_j/s_j)·Σ_i A_ij·y_i/(Σ_j' A_ij' λ_j'+b_i)`, where the system
matrix `A_ij` is 1 when the ray from source cell j to detector pixel i crosses an OPEN mask cell (the true finite
aperture as the forward model).
- **`MlemDecoder` (Decoding)** implements `IDecoder`, builds/caches the system matrix from the same geometry the
  cross-correlation decoder uses, and iterates MLEM. Opt-in — the pipeline still defaults to cross-correlation.
- **`MlemStudy` / `montecarlo mlem`** decodes the SAME coded floods both ways and compares. Headline TWO-SOURCE
  SEPARATION: MLEM deconvolves a close pair into two non-negative peaks where cross-correlation shows one merged blob.
  **MLEM cleanly splits 2–3 mm pairs cross-correlation merges** (min resolvable ~2 mm vs ~3.5 mm), on the default
  geometry (point resolution ≈2.5 mm). At 3 mm the MLEM valley depth is 0.82 (two clear peaks) vs cross-correlation's
  0.17 (merged). (The metric is truth-centred, so its deepest-separation ~1.5 mm number is optimistic; the robust,
  test-backed claim is the 2–3 mm split.)
- **Single-source**: MLEM is **NON-NEGATIVE** (min recon 0.00) while cross-correlation dips to −103 (negative
  sidelobes), and MLEM deconvolves to a **sharper peak** (FWHM 1.76 mm vs 2.50 mm) at comparable localization bias
  (0.87 vs 0.55 mm). The cost is iteration and the usual MLEM resolution/noise trade-off with iteration count.
- Self-contained (builds both decoders + floods from the config geometry; the main pipeline is untouched). (Tests:
  `MlemTests`, +2 — non-negativity & sharpness vs sidelobes, resolves closer pairs than cross-correlation.)
- **Codex cross-verification** (gpt-5.5, read-only): the MLEM CORE is CORRECT — standard zero-background ML-EM update
  (forward-project, ratio, back-project, ÷ sensitivity, multiplicative), `A[j·nDet+i]` indexing consistent, flat λ=1
  start fine, and the system-matrix ray geometry MATCHES the flood generator (no sign/transpose/mirror/index bug).
  Framing tightened per its notes (no code change): `A_ij` is a binary pixel-CENTRE sample (no pixel-area / cos·r²
  integration) — a good approximation, not an exact forward model; the ghost-suppression benefit needs the FINITE
  forward model (`Cyclic = false`) and is a capability, not what the default cyclic demo exercises; the ~1.5 mm number
  is optimistic (truth-centred metric windows overlap below ~2 mm) so the robust claim is the 2–3 mm split; and the
  sharper-peak / closer-resolving results are iteration- and count-dependent (an ideal high-count study, not a general
  camera resolution limit).

## 49. Thermal DCR / PDE readout effects — `ThermalDrift` (extended) / `ThermalReadoutStudy` / `montecarlo thermalro`
Physics-realism-audit item G (readout): the thermal model (theme 36) mapped temperature only to the SiPM gain
(photopeak-centroid drift). It missed the DARK-COUNT and PDE consequences. `ThermalDrift` gains `DcrFactor(ΔT) =
2^(ΔT/doubling)` (silicon dark generation ≈ doubles every 8–10 °C) and `PdeFactor(ΔT)` (PDE follows the over-voltage,
≈ −0.2 %/°C). Key physical point: bias compensation nulls the GAIN drift but does NOT null the thermal dark
generation, so **DCR keeps climbing with temperature even under a comp loop**.
- **`ThermalReadoutStudy` / `montecarlo thermalro`** sweeps ΔT and, via `FrontEndModel`, reports DCR, the low-line vs
  photopeak resolution, and the PDE factor.
- **Result**: DCR rises **×13 over 30 °C** (2× / 8 °C). Its parallel-noise term is ∝1/E, so it degrades the **low line
  (60 keV) ~2× more (relatively) than the 662 keV photopeak** — but only **modestly in absolute terms** (60 keV
  10.1 %→10.4 %, 662 keV 4.30 %→4.36 %) because for this high-light-yield crystal the resolution is
  STATISTICS-dominated, not DCR-dominated. (The ∝1/E is the DCR TERM; the total FWHM is its quadrature sum with the
  statistics and intrinsic floor, so the total is not itself 1/E.) PDE droops −6 % over 30 °C. **Honest headline: the
  dominant thermal-readout effect is the raw DCR growth itself (a dark avalanche / pile-up load, cf. dead time theme
  42), not a resolution hit — and the 662 photopeak (this camera's line) is nearly immune, so the gain-centroid drift
  of theme 36 stays the main thermal concern for Cs-137 imaging.** (Tests: `ThermalReadoutTests`, +2 — DCR doubling &
  comp-immunity, 1/E low-E degradation + PDE droop.)
- **Codex cross-verification** (gpt-5.5, read-only): no implementation bug — `DcrFactor = 2^(ΔT/8)` and the −0.2 %/°C
  PDE droop are physically reasonable, bias compensation correctly affects gain/PDE but NOT the thermal DCR term, and
  `R_dcr = 2.355·√(ENF·DCR·τ)/npe` with npe∝E gives the ∝1/E term consistently. Clarified per its notes: the ∝1/E is
  the DCR TERM (not the total FWHM), and `dark_trigger_kcps = DCR/1000` is the raw dark avalanche-load proxy, not a
  modelled discriminator false-trigger rate.

## 50. Depth-of-interaction (DOI) parallax in reconstruction — `DoiParallaxStudy` / `montecarlo doi`
Physics-realism-audit item F (decoder vs detector response): the decoder back-projects the pixel-CENTRE front-face
crossing, but a gamma interacts at a random DEPTH in the crystal. For an OBLIQUE (off-axis) ray the scintillation
centroid the detector reads is displaced from the front face by depth·tan(incidence) — a parallax the centre-back-
projecting decoder cannot correct. `DoiParallaxStudy` isolates it by building the SAME rays' flood twice — deposit at
the front-face crossing (no DOI) vs at the DOI-displaced interaction point — and comparing the decoded position.
- **Result**: the DOI localization SHIFT is **0 on-axis** (by symmetry the displacement averages out — individual rays
  still have DOI blur; it is the signed shift that vanishes) and grows off-axis WITH crystal thickness (deeper
  interactions → bigger parallax): at 9 mm off-axis it is 0.10 mm for a 5 mm crystal → **0.30 mm for a 30 mm crystal**.
  Sub-mm here because the far source (160 mm) gives a small (~2–4°) obliquity, but it is a real, systematic floor —
  comparable to the recon grid step (0.39 mm) and thus a genuine limit UNDER the sub-cell interpolation (theme 43) at
  the FOV edge with a thick crystal, uncorrected by the decoder.
- Because the shift is often sub-grid-step, the raw argmax quantizes it to 0 until it crosses a cell (one 0.88 mm
  argmax jump in the sweep is quantization behaviour, not smooth DOI scaling) — which is exactly why it matters only
  against the SUB-CELL precision, not the coarse grid. Self-contained. (Tests: `DoiParallaxTests`, +1 — zero on-axis,
  grows with thickness off-axis, sub-mm.)
- **Codex cross-verification** (gpt-5.5, read-only): geometry CORRECT — the lateral displacement `d_xy/|d_z|·depthZ`
  reduces to `d_xy·pathIn` (correct along-ray displacement), and the paired-flood (same rays, DOI on vs off) is a valid
  isolation. Framing tightened per its notes: "zero on-axis" is the SIGNED shift by symmetry (not zero per-ray DOI
  blur); the FWHM columns are a fragile reference (the metric NaNs off-axis, so only the SHIFT is asserted); the 0.88 mm
  row is argmax/quantization behaviour, not physical DOI scaling.

---

## Post-theme-50 follow-ons (2026-07-20/21) — WPF nuclide separation, Compton strip, CR-RC fix

Enhancements on top of the 50-theme base (WPF = theme 34–35, stripping = theme 16–17, CR-RC = theme 33), each
Codex-cross-verified and headless-tested (the WPF GUI can't be tested here, so the underlying physics is proven in
the `Gcam.Simulation`/`Gcam.Detector` layer and the app is verified to compile).

- **Nuclide separation IN THE RECONSTRUCTED IMAGE** (`Gcam.Wpf/{Scene.cs, MainWindow.xaml.cs}`): the imaging tab
  decoded ONE flood through a single window locked to `scene[0].Lines[0]`, so co-measured isotopes separated in the
  SPECTRUM but not the IMAGE. Now **one imaging channel per distinct isotope**, each windowed on its own primary line,
  decoded with the shared geometry-only decoder; per-nuclide recon **normalized + isotope-coloured composite** on the
  scene overlay; per-channel accumulation at each channel's own detected rate; `_floodAccum` is the COMBINED flood so
  depth/autofocus see all sources. Recon heatmap **flipped +y-up** (ScottPlot draws grid row 0 at top) to match the
  canvas + markers; **found peaks labelled by nuclide**. Test `PerNuclideWindow_SeparatesCsFromNa_InTheImage`
  (662 window → Cs, 511 window → Na). Co-60 stays physics-limited (1+ MeV punches through).
- **Compton stripping toggle** (co-located isotopes): per-pixel `max(0, low − Σ R·high)`, `R` calibrated per
  isotope-pair in `PrepareLive` from an isotope-only run (pair gated on the contaminant's MAX emission line, not its
  channel centre — so Na-22's 511 channel strips a Cs 662 window via its 1275 line). One-pass scalar model (matches
  the CLI `mixedstrip`): exact for a clean pair, approximate for 3+ overlap (a per-pixel response-matrix solve is the
  optional-polish full fix). Test `ComptonStripping_RecoversCoLocatedCsCount`.
- **CR-RC⁴ constant fix**: the RTL default + cocotb param carried the wrong `A_Q16 = 53667`; the correct value is
  `round(e^-0.2·65536) = 53656` (the C#/Python formulas). Realigned `crrc_shaper.sv` / `run_cocotb.py` /
  `trap_ref.py` — **C# ↔ RTL CR-RC is now genuinely bit-exact** (7 cocotb re-run green). The cocotb test read A from
  the DUT, so it was self-consistent and had masked the drift.
- **Mask μ(E) extended < 122 keV** for soft lines (Am-241 59.5 → ~47×); documented approximation (K-edge above-edge
  region + < 50 keV clamp — no isotope line there, thick mask opaque regardless). Test `MaskAttenuationTests`.
- Docs: `docs/paper_ko.md` (+ rendered artifact) — a two-tier (expert/plain) physics & sweet-spot note covering
  §1–9 incl. in-crystal Compton transport, dead region, crosstalk, hardware realization (RTL/cocotb/FPGA), and
  productization; Codex physics-verified (6 wording/number fixes applied).
