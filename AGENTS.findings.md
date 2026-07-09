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
from Co-60's direction just like its photopeak, the subtraction is spatially matched — it removes the
contamination *where it actually landed*. Run: `montecarlo compton-strip samples/scenario.json`
(Co-60 ×8 = a stronger dominant source, so the contamination is visible not just countable) →
`samples/compton_strip_combined.png` (plot via `plot_compton_strip.py`).
- **Recovers the true Cs-137 net count to ~0 %** from a **+74–76 %** raw-window over-count, in both
  geometries.
- **Separated sources**: the raw 662 reconstruction shows Cs *and a bright Co-60 ghost*; stripping
  removes the ghost, leaving the clean Cs image.
- **Co-located sources** — the case the spatial decode ALONE cannot separate (theme 15's stated
  limit): raw 662 is one peak inflated +76 %; per-pixel stripping brings it back to the true Cs count.
  So the spectral + spatial levers *together* disentangle even two isotopes at the same position.
- Locked in by `ComptonTests.Stripping_RecoversCsCount_EvenCoLocated`.
- Reproduce: `montecarlo compton-strip samples/scenario.json`.

## 18. Source distance (z) estimation by coded-aperture refocusing — `DepthStudy`
The pipeline had assumed the source distance S known; it can be *recovered*. The mask-shadow
magnification **M = (D+S)/S** depends on S, and the decoder back-projects with `frac = D/(D+S)`.
Decoding one flood map at a range of ASSUMED S, the correlation at the (on-axis) source position is
maximal when the assumed S matches the true S — the coding and decoding align — and falls off
otherwise. So argmax over assumed S estimates the depth (light-field-style refocusing).
- **Focus metric = the single-point correlation at the known on-axis position** (a 1-cell recon grid),
  which avoids the candidate-position aliasing and grid-scale artifacts that fooled a naive
  peak-height or peak/RMS over a full grid. **Estimate = the centroid of the high-focus region** (the
  curve develops a flat top over the band of unresolvable distances; its width IS the depth resolution
  and its centre is the estimate — a bare argmax snaps to the plateau's leading edge).
- **Near field accurate, far field degrades** (`depth_estimation.png`): S 40→39, 60→60, 100→97,
  150→140, 200→214 mm (error grows −1→+14 mm), and the focus-curve width (depth resolution) grows from
  ~50 mm at S=40 to ~240 mm at S≥150. This is the intrinsic limit: sensitivity ∝ dM/dS = −D/S², so a
  near source resolves depth while a far one (M→1, near-parallel shadow) cannot.
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
- Reproduce: `montecarlo depth-joint samples/scenario.json` → `samples/depth_joint.png`,
  `depth_joint.csv` (plot via `plot_depth_joint.py`). Locked in by `DepthTests`.
