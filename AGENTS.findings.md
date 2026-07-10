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
