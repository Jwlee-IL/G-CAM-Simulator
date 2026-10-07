# PLAN.Physics.AngularResolution.Review — implementer's turn-1 review: angular point response and two-source separation at 1 m / 5 m

Scope: TODO-34 turn 1 (review only) of [PLAN.Physics.AngularResolution](PLAN.Physics.AngularResolution.md), by the
substitute implementer (a Claude subagent; Codex unavailable). Every *verify* row and every "What exists" claim was
checked in the code, and the quantitative parts were measured with short headless probes. No production code, plan,
Todo or VV document was changed. Scratch code and outputs are under `%TEMP%\gcam-todo34\` (machine-local, not
evidence).

Status: review, 2026-10-06. The numbers below are **rough scoping numbers** from one outer seed; their N is given with
each one. None of them is evidence-grade.

## At a glance

- **The plan's skeleton holds**: transported maps at `SourceDetectorMm`, non-cyclic decoding, a blind resolved-pair
  test with seed pass rates, MLEM against cross-correlation (CC). Six premises need correcting before implementation:
  1. **The literal AR-3 criterion does not work on these images.** "Two largest local maxima + straight-line valley"
     picks plateau steps and ripples of one source. Same acquisitions, CC, 1000 counts per source, Δ = 2 elements:
     75 / 200 pass with the literal test, 196 / 200 with a topographic-prominence test. Use prominence (the saddle on
     the best path, not the straight line), measured above a baseline, cap the assignment radius at one element, and
     **report a single-source null** with every pass rate.
  2. **The engine's MLEM splits single sources.** Its binary pixel-centre matrix, run past ~20 iterations, makes two
     "resolved" peaks out of one source in 20–50 % of acquisitions (1000 + 1000 counts, Δ = 1–2 elements). AR-5's rule
     ("the iteration count that maximises the pass rate") would pick 40 iterations, where the null false-pass is 21 %.
     The cause is pixel-centre sampling, not the missing tungsten leak: an analytic matrix that integrates pixel area
     stops the splitting (null ≤ 2 %) at no Monte Carlo cost.
  3. **The decoded image is not shift-invariant at 1 m and 5 m**, because the head has one pixel per projected mask
     cell (shadow cell / pixel = (S + D) / S = 1.058 at 1 m, 1.011 at 5 m, 1.6 at the lab). The result depends on
     where the pair sits within an element. CC, 16 000 counts per source, Δ = 1.5 elements, pair centre moved by
     0 / 0.1 / 0.25 / 0.4 element: 200 / 200 / 120 / 0 of 200 pass. **The pair position must be jittered** over an
     element (per seed); otherwise the result is one sampling phase's luck.
  4. **The baseline actually delivered is wider than 1.042°.** CC point-response FWHM at 1 m, over 32 positions within
     one element, median 1.46° along x (range 1.08–1.94°) and 1.20° along y (0.97–1.56°). At 5 m the median is
     1.09–1.11°, but a source on a sampling boundary gives 2.1° (the decoder has only 225 distinct columns of 2401 at
     5 m). "With and without sub-cell interpolation" does not apply to an FWHM: sub-cell refines only the reported
     position.
  5. **CC never reaches a monotone 95 % at 1 m within 3 elements**, at any count level. CC's response to one source has
     sub-peaks 0.3–0.45 element from the maximum with relative prominence up to 0.29, and two equal sources come out
     with peak heights up to ~25 % apart. Phase-averaged, effectively noiseless (10⁶ counts each): 56 / 97 / 90 / 81 %
     at Δ = 1.5 / 2 / 2.5 / 3 elements (v = 0.25). "The smallest Δ with ≥ 95 %" needs a monotone-closure rule, and
     for CC the answer is "not reached by 3 elements" plus its best pass rate.
  6. **`AmbientSweepStudy` is not at 1 m.** It takes the distance from the scenario: S = 100 mm for
     `scenario_handheld.json`. Only `AmbientGateStudy` and `CsUnderCoStudy` set S = `SourceDetectorMm` − D.
- **The MLEM gain at 1 m, rough** (on axis, 1 : 1, phase-averaged, v = 0.25, 95 % with null ≤ 5 %):

  | Decoder | Separation resolved | Counts per source | Iterations |
  |---|---|---|---|
  | CC | not reached by 3 elements (2.0 elements only at 16 000 counts, 96.5 %) | 1000 | — |
  | binary MLEM | 1.5 elements | ≥ 1000 | 10 |
  | pixel-area MLEM | 1.25 elements | ≥ 1000 | 160 |
  | transported "matched" MLEM (single phase) | 1.25 elements | 1000 | 160 |
  | transported "matched" MLEM (single phase) | 1.0 element | 16 000 | 320 |

  At 250 counts per source nothing reaches 95 % by 3 elements. One element = 1.042° = 18.2 mm at 1 m.
- **Q3 is cheap and already partly explained.** With transported floods and the blind test at the lab geometry
  (1.5 × 10⁵ counts), CC resolves 3 mm and binary MLEM 2 mm in 77 / 100. EV-11 said 3.5 mm and 1.5 mm. Cyclic and
  non-cyclic decoding are identical there, because every pixel ray hits the 2 × 2 mosaic. The lab's 1.6 pixels per
  shadow cell is why the near-field number does not transfer.
- **Run cost**: about 20–35 CPU-h for the proposed ensemble (128 seeds × 50 repeats, both distances, three MLEMs), so
  1.5–2.5 h wall on 16 jobs. The dense MLEMs dominate. No cut is needed; cuts are listed under AR-9.

## How the numbers were obtained

| Probe | What | N / precision |
|---|---|---|
| `check` | Replica of `MlemDecoder` (same matrix, same update, iteration snapshots) against `MlemDecoder.Decode` at 60 iterations, hand-held head at 1 m and 5 m (S = SD − D). Grid of `CorrelationSearch` vs MLEM | max \|Δ\| / max = 9 × 10⁻¹⁶; both 49 × 49, step 0.1519°, half 3.641° at both distances |
| `cols` | Distinct decoder columns (the pattern of +1 / −1 / 0 over the 256 pixels) over the default grid | exact |
| `fwhm`, `fwhmj` | `GateResponse.Source` maps (cs662 window 595.53–727.87 keV, 7 % FWHM), noiseless CC by `CorrelationSearch.ReconstructExpected`, non-cyclic, one-period grid; FWHM along x and y through the maximum, linear interpolation, baseline 0 or image median. MLEM at snapshots | `fwhm`: 8 maps × 4 × 10⁶ photons at one position (on axis, and 2.5 elements off). `fwhmj`: 32 positions (Halton jitter over ±0.5 element in x and y) × 4 × 10⁶. Poisson spread: 60 draws per count level |
| `pairs`, `pairsj` | Two-source acquisitions: λ = c₁·m₁/Σm₁ + c₂·m₂/Σm₂ from two independent transported maps; exact Poisson (`AmbientEvidence.Draw`); CC via `CorrelationSearch.Reconstruct`, binary MLEM replica with snapshots. **Null**: one source with c₁ + c₂ counts at the pair's intensity centroid, judged against the same hypothesised pair | `pairs`: one pair position, 200 repeats per Δ. `pairsj`: 16 jittered positions × 25 repeats (400 per cell), pair axis alternating x / y; noiseless check 32 × 4 at 10⁶ counts |
| `areaj` | Dense MLEM with an analytic matrix: 8 × 8 sub-points per pixel back-projected through the thin mask; closed cells and the frame transmit exp(−μt) = exp(−0.178 × 10) = 0.169 (the scenario's μ and thickness) | 16 positions × 15 (240 per cell) |
| `matched` | Dense MLEM whose 2401 columns are `GateResponse.Source` maps at the grid points (4 × 10⁵ photons each, own seed stream), data from independent maps | 1 position, 200 (or 100) repeats |
| `lab` | Lab `scenario.json`, transported floods (open window), pairs at ±sep/2 mm, prominence test, MLEM 80 iterations, cyclic and non-cyclic | 100 repeats; 4 × 10⁶ photons per map |
| `cost` | Single-thread timings | wall clock |

"Pass" below is the corrected criterion (AR-3), v = 0.25 unless stated. "Element" = atan(1 / 55) = 1.0416°. "Counts"
= expected counts of one source in the cs662 window; at 1 m, 1 MBq of Cs-137 gives 1.88 cps in that window (EV-34), so
1000 counts in 60 s is ≈ 8.9 MBq and 16 000 counts ≈ 142 MBq.

## Per row

### What exists

| Claim | Verdict | Evidence |
|---|---|---|
| `MlemStudy`: lab D = 60, S = 100; binary pixel-centre floods; cyclic; truth-centred valley > 0.25 | **holds, one correction** | `MlemStudy.Run` (geometry from the config, `DecoderConfig.Cyclic` default true, `ValleyDepth` with ±2-cell windows at the truth, `resolveThreshold = 0.25`); `DecodingCommands.RunMlem` (1.5 × 10⁶ photons, 80 iterations). Correction: `TwoSourceFlood` aims photons uniformly over the detector area, so the floods are pixel-area integrated, while `MlemDecoder` samples pixel centres. That is a small mismatch (thin binary mask, no leak, no crystal). It is not "no model mismatch" |
| `MlemDecoder`: A_ij = 1 for an open pixel-centre ray; honours `Cyclic`; b_i? | **holds; no b_i** | `MlemDecoder.BuildSystemMatrix`; the summary formula shows `+ b_i`, but `Decode` (update loop) divides by Σ A λ only and takes no background |
| Transported maps **at 1 m** with both decoders in `AmbientSweepStudy` | **wrong premise** | `AmbientSweepStudy.Run` loads the scenario and never sets `SourceMaskDistanceMm`: it runs at the scenario's S (100 mm for the hand-held head). Its `Mlem` helper builds sourceZ = D + S, so it would be right at 1 m if S were set. At 1 m today: `AmbientGateStudy.Run` (S = `SourceDetectorMm` − D) and `CsUnderCoStudy.Run` |
| `CorrelationSearch`, `GateResponse` | **holds** | `CorrelationSearch` reads G off `CrossCorrelationDecoder` (exact integer path, `ReconstructExpected` for noiseless maps); `GateResponse.Source` (directional biasing, slab mask, `ComptonCrystalDetector` largest-deposit pixel, window acceptance on the deposit). Physics limits: homogeneous crystal, no inter-pixel dead regions, ideal pixel identification (no Anger positioning, TODO-19) |
| csco convention: `SourceDetectorMm`, angular elements | **holds** | `CsUnderCoStudy.Run` (`S = SourceDetectorMm − D`, `elemDeg = atan(cell / D)`, positions `SD·tan(el·elemDeg)`) |
| `FieldOfViewStudy` / EV-02 ±7°, fully coded ±3.6° | **holds, other convention** | `FieldOfViewStudy` / `AmbientFieldOfViewStudy` set S = 1000 / 5000 (source–**mask**); this plan says source–**detector**. Use the csco / gate convention; state it |
| Seed ensembles | **holds** | `run_seeds.py --manifest`; `manifest-csco-v1.json` as the model |

### Q1 — point response

**Verdict: correct it.**

- **Sub-cell interpolation does not change an FWHM.** `PeakInterpolation.Estimate` only refines the reported position.
  The image and the pair test both use the grid. Drop it from Q1; it belongs to localisation precision (PR-IMG-02).
- **CC, noiseless, 1 m, median baseline:**
  - fixed on-axis position (8 maps): x 1.173 ± 0.006°, y 1.219 ± 0.004° (baseline 0: 1.240 / 1.257);
  - 32 positions within one element: x median 1.46° [1.08, 1.94], y 1.20° [0.97, 1.56];
  - peak height per count 0.30–0.41;
  - noiseless argmax error median 0.20 element, max 0.44 element.
- **CC at 5 m:**
  - fixed on-axis position: x 1.07°, y 1.06°;
  - jittered positions: x median 1.11° [1.05, 2.11], y median 1.09° [1.04, 2.08];
  - peak height 0.19–0.47;
  - argmax error up to 1.0 element.
- **Cause: decoder sampling.** CC back-projects pixel centres, and the head has ~1 pixel per projected cell. Across
  the 16-pixel array the sampling phase walks by 16 × D/SD cells: 0.88 at 1 m, 0.18 at 5 m and 5.7 at 155 mm.
  Distinct decoder columns out of 2401 grid points:

  | Distance | Distinct columns | Central row |
  |---|---|---|
  | 155 mm | 2209 | 47 / 49 |
  | 1 m | 1681 | 41 / 49 |
  | 2 m | 576 | 24 / 49 |
  | 5 m | 225 | 15 / 49 |

  At 5 m the decoded image is piecewise constant over ~0.5 element, independent of the grid step. This is the
  under-sampling of theme 7 ("≳ 1 pixel per mask-cell shadow; ~2 is the sweet spot"), now seen as shift variance. For
  scale, the usual quadrature approximation for a detector of resolution d, atan(√(c² + d²) / D) = 1.47°, matches the
  x median. The 1.042° baseline is the element, not the delivered FWHM.
- **MLEM FWHM is not a resolution measure.** On Poisson data the binary MLEM (80 iterations) collapses to 1–2 grid
  points: median FWHM 0.19 / 0.26 / 0.57 / 0.84° at 250 / 1000 / 4000 / 16 000 counts. That is the known
  count-dependence of a non-linear estimator.
  - Noiseless (jittered, 1 m): 0.94 / 0.79 / 0.75° (x) at 10 / 20 / 40 iterations.
  - The matched MLEM, noiseless: 1.27 / 0.82 / 0.47 / 0.41 / 0.34° at 10 / 20 / 80 / 160 / 640 iterations.

  Report MLEM FWHM only noiseless, at the frozen iteration, and labelled as such. The two-source test is the
  resolution figure.
- **5 m adds information.** In counts it is redundant with 1 m, but it is the sampling-degenerate case: FWHM up to
  2.1° and peak height ×2.5 over the phase. Keep a slice (AR-2).

### Q2 — two-source separation

**Verdict: holds as a question; the criterion, the null and the position jitter are missing.**

Phase-averaged, 1 m, pair centred on axis ± 0.5 element jitter, 1 : 1, pass / null of 400 (v = 0.25):

| Counts per source | CC best (Δ) | binary MLEM, 10 iterations | binary MLEM, 20 | binary MLEM, 40 |
|---|---|---|---|---|
| 250 | 248 / 37 (2.0) — not reached | ≤ 333 (2.5), null ≤ 19 — not reached | ≤ 265, null up to 85 | null up to 167 |
| 1000 | 324 / 23 (2.0) — not reached | **392 / 4 at 1.5**; 1.25: 280 | 1.75: 383 / 14 | 1.0: 265 / 84 |
| 4000 | 374 / 11 (2.0) — not reached | **398 / 0 at 1.5**; 1.25: 306 | 1.25: 361 / 0 | 1.0: 324 / 15 |
| 16 000 | **386 / 1 at 2.0**, then 356 (2.5), 316 (3.0) | 400 at 1.5; 1.25: 309 | 1.25: 348 | 1.0: 318 / 0 |
| 10⁶ (noiseless) | 124 / 128 at 2.0, 115 (2.5), 104 (3.0) | 128 / 128 at 1.5, 111 at 1.25 | — | 102 at 1.0 |

With v = 0.5, CC never exceeds 365 / 400 (91 %). Binary MLEM at 20 iterations then gives 1.5 elements from 1000 counts
(392–400), and at 40 iterations 1.25 elements from 4000 counts (380–384, null 0).

Other conditions (single seed):

- **Pixel-area MLEM** (analytic matrix, jittered, 240 per cell), pass / null:

  | Counts | Δ = 1.0 | Δ = 1.25 | Δ = 1.5 | Iterations |
  |---|---|---|---|---|
  | 1000 | 173 / 0 | 238 / 0 | 239 / 1 | 160 |
  | 16 000 | 164 / 0 | 239 / 0 | — | 80 (1.25) and 160 (1.0) |

  Pass grows monotonically with iterations up to 160 and the null stays ≤ 4.
  - Pixel area without the leak: no splitting either (null ≤ 5), slight decline at 160 iterations.
  - Centre-sampled matrix with the leak: null 87–121 / 240 at ≥ 80 iterations. The pixel area is the fix.
- **Matched MLEM** (one position):
  - 1000 counts: 1.25 elements 199 / 200 and 1.0 element 127 / 200 at 160 iterations; null ≤ 3 / 200.
  - 16 000 counts: 1.0 element 100 / 100 at 320 iterations; 0.75 element 62 / 100 at 640.
- **1 : 4** (weaker 1000, one position):
  - CC best 188 / 200 (Δ 1.5), falling to 127 at 3 elements. Null up to 26 / 200 (13 %): the strong source's sidelobes
    pass as the weak one.
  - Binary MLEM at 20 iterations ≥ 95 % at 1.25–1.75 and 2.5–3 elements, 176 at 2.0; null ≤ 12 / 200.
  - Keep 1 : 4: it exposes a CC failure mode 1 : 1 does not.
- **Near the edge** (pair centre 2.5 elements, ±6° grid, one position, 1000 counts):
  - CC max 169 / 200 (2.0 elements);
  - binary MLEM ≥ 95 % from 2.0 elements, against 1.5 on axis.
- **Search-grid extent matters.** Same on-axis pairs on a ±6° grid instead of one period: CC at 2 elements drops from
  198 to 137 / 200 (noise peaks in the partially coded field win). Binary MLEM at 10 iterations: 1.5 elements 189,
  1.75 elements 199.
- **5 m** (4000 counts, one position): CC and binary MLEM both 0 / 200 at ≤ 1.25 elements and 191–200 at 1.5. That is
  a step, as the sampling predicts.
- **Count range.** {250, 1000, 4000, 16 000} brackets the MLEM transition (none at 250; 1.5 elements from 1000). CC is
  limited by its deterministic ripple, not by counts (the noiseless row). No higher level is needed.

### Q3 — what the old claim was worth

**Verdict: holds; define the ladder.** Lab `scenario.json`, transported floods (open window), blind prominence test,
MLEM at 80 iterations, pass of 100:

| Counts (total) | CC | binary MLEM |
|---|---|---|
| 1.5 × 10⁵ | 2 mm 0, **3 mm 100** | 1.5 mm 0, **2 mm 77**, 3 mm 100 |
| 2000 | 3 mm 72, **3.5 mm 98** | 3 mm 88, 3.5 mm 90, 4 mm 97, 5 mm 91 |

EV-11 (self-consistent floods, truth-centred valley): CC 3.5 mm, MLEM 1.5 mm. Cyclic and non-cyclic give identical
results for central pairs at the lab, because every pixel ray hits the 2 × 2 mosaic. That attribution step is null
and can be stated rather than run.

Proposed ladder, each step changing one thing:

1. EV-11 as is.
2. Blind criterion + null.
3. Transported floods.
4. Realistic counts.
5. Hand-held head at its own near field.
6. 1 m.

The physical reason the near-field figure does not transfer is the shadow sampling: 1.6 pixels per shadow cell at the
lab, against 1.06 at 1 m. Steps 1–5 cost minutes.

### AR-1 — new evidence family on the transported-map path

**Verdict: holds.** Add a family (e.g. `angres`) with its own spec section in `AmbientEvidenceRequest`, its own study
class and one switch line in `AmbientCommands.RunEvidence`. Reuse:

- `AmbientEvidence.SourceLines` / `GateResponse.Source` for the maps;
- `AmbientEvidence.Draw`;
- `CorrelationSearch` (exact integer `Reconstruct`, `ReconstructExpected`, `AngleBetweenDeg`);
- `MlemDecoder` with the options under AR-6 / AR-7;
- csco's `Point()` convention.

`MlemStudy` / EV-11 stay unchanged. Q3 also needs the lab scenario at its own S, so the spec should allow
"scenario S" as well as `SourceDetectorMm`.

### AR-2 — geometry, non-cyclic, the MLEM matrix at a far source

**Verdict: holds, with corrections.**

- **The MLEM matrix is right for a far source** when it is built with sourceZ = D + S, S = `SourceDetectorMm` − D. This
  is what `AmbientSweepStudy.Mlem` does with the scenario's S. The replica check gives 9 × 10⁻¹⁶ against
  `MlemDecoder`.
- **The grids match in angle.** Both decoders build N = round(2·half / step) + 1 from the same half and step: 49 × 49,
  0.1519° at 1 m and 5 m.
- **What the matrix models:** binary transmission, pixel centres, no leak (closed cells pass 16.9 % at 662 keV), no
  slab collimation, no crystal response. Only 1681 of 2401 grid points have distinct columns at 1 m, and 225 at 5 m.
  MLEM cannot separate duplicates, and from a flat start it keeps them equal.
- **Pair placement.** Pairs must lie inside the search grid. The one-period grid is ±3.5 elements, so "near the edge"
  as written (centre 2.5 elements, Δ up to 3) puts a source at 4 elements, outside. Proposal: outer source at
  2.5 elements (+ jitter, ≤ 3.0), inner one at 2.5 − Δ, one-period grid. A ±7° grid (the EV-02 usable field) at one
  condition, as a sensitivity.
- **Distance convention:** `SourceDetectorMm` (gate / csco), not FOV's S.
- **5 m:** keep a slice (Q1 says why): on axis, 1 : 1, counts {1000, 4000}, all decoders.

### AR-3 — resolved-pair criterion

**Verdict: correct it.** Proposed blind test, per reconstruction:

1. **Peaks and prominence.** Find local maxima (8-connected; plateau-safe tie-break) and their topographic prominence
   by a descending union-find. The saddle is the highest col on any path to a higher peak.
2. **Baseline.** Relative prominence = (P − max(saddle, b)) / (P − b).
   - For CC, b = the image median. CC has a pedestal and negative sidelobes. Unclipped, a peak ringed by negative
     sidelobes gets relative prominence > 1 and outranks the real second source (measured: CC 0–4 / 200 at
     Δ = 1–1.25 elements with unclipped ranking).
   - For MLEM, b = 0.
3. **Pair.** P1 = global maximum. P2 = the highest other peak with relative prominence ≥ v.
4. **Resolved** when P1 and P2 lie within r = **min(Δ/2, 1 element)** of different true sources. With r = Δ/2, a single
   centroid source passes at Δ = 3 elements in 30 / 200 (CC), because the centroid sits on the boundary; with the cap,
   0 / 200.
5. **Null.** With every pass rate, report the false-pass of one source with the same total counts at the pair's
   intensity centroid, judged against the same hypothesised pair.
6. **"Resolved at Δ"** = pass ≥ 95 % with null ≤ 5 %, at Δ **and at every larger Δ of the grid**. Otherwise report
   "not reached by 3 elements" and the best pass rate. CC is non-monotone (Q2).

**Choice of v.** This is a hypothesis test, one source against two (Harris, JOSA 54, 606 (1964); Shahram & Milanfar,
IEEE TIP 13, 677 (2004)): the pass rate is its power and the null rate its size.

- Rayleigh's dip for two equal Airy patterns is 73.5 % of the peak (v = 0.265).
- For two tents of FWHM w (CC's ideal peak shape), the relative prominence is Δ/w − 1, so v = 0.25 ↔ Δ = 1.25 w, and
  Sparrow (v → 0) ↔ Δ = w.
- Sparrow is not usable under noise: CC's null at v = 0.1 reaches 47 / 200 (24 %) at 250 counts (one position),
  against 15 / 200 at v = 0.25.
- Keep **v = 0.25** (Rayleigh-equivalent, EV-11 continuity) and report **v = 0.5** as a sensitivity. 0.5 lies above
  CC's own ripple (≤ 0.29) and changes CC's and MLEM's numbers by up to one Δ step.

### AR-4 — axes

**Verdict: correct it.**

- **Δ:** {1, 1.25, 1.5, 1.75, 2, 2.5, 3} elements; add 0.75 for the pixel-area and matched MLEMs only (nothing else
  passes below 1).
- **Counts per source:** {250, 1000, 4000, 16 000}, as proposed. State the activity × time each one means.
- **Ratio:** 1 : 1 and 1 : 4.
- **Positions:** on axis, and near the edge (AR-2).
- **New: pair position jittered** uniformly over ±0.5 element in x and y per seed, and pair orientation x or y per seed.
  The MURA response differs between x and y: FWHM median 1.46 against 1.20°.

### AR-5 — MLEM iterations

**Verdict: correct it — the rule picks the splitting regime.** At 1000 counts, Δ = 1 element:

| Iterations | Pass | Null |
|---|---|---|
| 10 | 28 / 400 | 0 |
| 20 | 210 / 400 | 5 |
| 40 | 265 / 400 | **84** |

Maximising the pass rate selects 40. Rule instead: on selection seeds, the iteration count that minimises the resolved
Δ (AR-3, including the null ≤ 5 %) at 1000 counts, 1 : 1, on axis; ties go to fewer iterations; then frozen per decoder.

| Decoder | Pick at this probe's resolution | Resolved Δ |
|---|---|---|
| binary MLEM | 10 (v = 0.25) or 20 (v = 0.5) | 1.5 elements |
| pixel-area MLEM | ~160 | 1.25 elements |
| matched MLEM | ~160 | 1.25 elements |

Record snapshots (5 … 640) in the same run, so the sensitivity costs nothing extra.

### AR-6 — model mismatch kept; matched matrix

**Verdict: correct it — three MLEMs, not two.**

1. **Binary (the engine's `MlemDecoder`)**, for continuity with EV-11. It splits single sources past ~20 iterations.
2. **Pixel-area + Beer–Lambert leak**: an analytic matrix from the geometry and the scenario's μ and thickness, no MC.
   It removes the splitting and resolves 1.25 elements at ≥ 1000 counts. This is not an upper bound; it is what a
   careful implementation would use. The plan's "Not here" line ("a transported or pixel-area MLEM … only as an upper
   bound") should move pixel-area out of that line, as a measured variant (not a product reconstruction).
3. **Transported matched** (upper bound), with columns = `GateResponse.Source` maps at the grid points. Cost: 2401 ×
   4 × 10⁵ photons = **29 s wall on 20 threads (~10 CPU-min) per distance**. Build it once per distance from a
   declared, independent stream (an "instrument calibration"), never from the data maps' streams. It is still the same
   transport physics as the data, so it is an upper bound by construction; say so.

The `MlemDecoder` matrix is already dense float, so weighted entries are an option of the builder: sub-samples per
pixel, closed-cell transmission, or supplied columns.

### AR-7 — ambient; MLEM background term

**Verdict: verify done — `MlemDecoder` takes no b_i.**

- Adding an optional per-pixel expected background b (counts) to `Decode` is a small change.
- Test it on the exact property that a noiseless λ_true with data A·λ_true + b is a fixed point.
- Expected effect in the cs662 window at 0.10 µSv/h: the bare bound gives 0.429 cps = 25.7 counts in 60 s, against
  2000 counts for a 1000 + 1000 pair (1.3 %). Front-only gives 0.6 counts.
- Run one condition: bare, 0.10 µSv/h, 60 s, on axis 1 : 1, 250 and 1000 counts, CC with the median baseline, MLEM with
  and without b. Drop front-only.

### AR-8 — single-source FWHM

**Verdict: correct it.**

- **CC:** the distribution over jittered positions (median and range), x and y, plus the 2D half-maximum area as one
  robust number, at 1 m and 5 m.
- **MLEM:** noiseless only, at the frozen iterations, labelled non-linear.
- No sub-cell variant.
- The Poisson-spread part can be dropped for MLEM (Q1) and kept for CC.

### AR-9 — seeds

**Verdict: correct it.**

- **Precision is limited by the number of sampling phases**, not by the repeats. Each seed draws its own pair position
  and orientation; 128 seeds × 50 repeats is worth more than 64 × 200 at lower cost.
- Report pooled k / N with a seed-cluster bootstrap interval; acquisitions within a seed share a phase and maps, so
  plain binomial intervals are too narrow.
- Use selection seeds for AR-5, disjoint from the validation seeds.

**Cost** (measured, single thread):

| Item | Cost |
|---|---|
| Map | 0.65 s per 10⁶ photons |
| Draw + CC reconstruction | 0.05 ms |
| Prominence test | 0.62 ms (LINQ; can be made ~5× faster) |
| Binary MLEM | 0.18 ms per iteration |
| Dense MLEM | 0.56 ms per iteration |

Per seed at 1 m (128 conditions × pair + null × 50):

- CC: 9 s;
- binary MLEM: 122 s;
- pixel-area MLEM on a subset (on axis, both ratios, 1000 and 16 000 counts, 160 iterations): ~300 s;
- matched MLEM on a subset (on axis, 1 : 1, 320 iterations): ~290 s;
- maps: ~42 s.

That is ≈ 13 CPU-min per seed. Over 128 seeds that is ≈ 27 CPU-h; with the 5 m slice and Q3, 20–35 CPU-h in all, or
1.5–2.5 h wall on 16 jobs. A float / SIMD dense MLEM roughly halves it.

Cuts if needed:

- matched MLEM on 64 seeds;
- 5 m on 64 seeds;
- Δ = 0.75 only at 16 000 counts.

### AR-10 — tests

**Verdict: holds; with these tolerances.**

| Test | Assertion |
|---|---|
| Prominence on synthetic tents | Two equal tents of half-base b on grid nodes (Δ/step even): relative prominence = Δ/b − 1 to 1e-12 for b ≤ Δ ≤ 2b; a single plateau gives exactly one peak; a pedestal shift changes nothing with the median baseline (exact) |
| Assignment cap | Single source at the centroid of a hypothesised pair at Δ = 3 elements: not assigned (deterministic) |
| Angle ↔ mm | `Point(el)` = SD·tan(el × atan(c/D)); `AngleBetweenDeg` of (0,0) and (x,0) = atan(x/SD) to 1e-12; one element at 1 m = 18.18 mm |
| MLEM far source | Data = A·e_k (binary model, noiseless) at SD = 1000 for a grid node whose column is unique: argmax at k after 200 iterations (exact). At 5 m: the reconstructed mass lies in k's duplicate-column set (≥ 1 − 1e-9) |
| Pixel-area matrix | sub = 1 and leak = 0 reproduces `MlemDecoder`'s A exactly; each entry lies in [leak, 1]; column sums are invariant under a whole-cell shift inside the fully coded field to 1e-12 |
| b_i | b = 0 reproduces `Decode` to 1e-15; λ_true is a fixed point for data A·λ_true + b to 1e-12 |
| No MC-precision assertion | pass and null rates are evidence outputs, not test assertions |

## Proposed changes to the plan

1. **What exists:** `AmbientSweepStudy` runs at the scenario's S, not 1 m. `MlemStudy`'s floods are pixel-area
   integrated, so EV-11 had a small model mismatch, not none. `MlemDecoder` takes no b_i.
2. **Q1:** drop "with and without sub-cell". The baseline delivered is a distribution over sub-element position
   (CC 1 m median 1.46° x / 1.20° y, range 0.97–1.94°). State 1.042° as the element and the measured FWHM as the
   baseline. MLEM FWHM only noiseless and labelled.
3. **AR-3:** topographic prominence with a baseline (CC: median, MLEM: 0); P2 = highest other peak with relative
   prominence ≥ v; assignment radius min(Δ/2, 1 element); a single-source null with every pass rate; "resolved at Δ"
   = pass ≥ 95 % and null ≤ 5 % at Δ and every larger Δ, else "not reached" with the best rate. v = 0.25
   (Rayleigh-equivalent), v = 0.5 as a sensitivity; Sparrow rejected.
4. **AR-4 / AR-9:** jitter the pair position over ±0.5 element (x, y) and alternate the orientation per seed; 128
   seeds × 50 repeats; seed-cluster intervals; Δ grid {1 … 3} (+ 0.75 for the area and matched MLEMs).
5. **AR-2:** "near the edge" = outer source at 2.5 elements (+ jitter), inner at 2.5 − Δ, one-period grid. A ±7° grid
   as one sensitivity condition. `SourceDetectorMm` convention. 5 m as a slice (on axis, 1 : 1, 1000 / 4000).
6. **AR-5:** the iteration rule minimises the resolved Δ including the null, on selection seeds; snapshots for the
   sensitivity.
7. **AR-6:** three MLEMs: binary (continuity), pixel-area + leak (measured variant), transported matched (upper bound,
   built once per distance from a declared stream, ~10 CPU-min). Edit "Not here" accordingly.
8. **AR-7:** add an optional b to `MlemDecoder`; one ambient condition (bare 0.10 µSv/h, 60 s); no front-only.
9. **Q3:** the attribution ladder of the Q3 row. The cyclic step is null at the lab and is stated, not run. The
   shadow-sampling explanation (1.6 against 1.06 pixels per shadow cell) goes into the restated EV-11.
10. **Step 4 (planner):** PR-IMG-02's 0.25 mm comes from S = 100 mm. At 1 m the noiseless CC argmax is off by up to
    0.44 element because of the lumpy top. Flag it with D-41's issue; it was not measured with sub-cell interpolation
    here.
11. **Limits / not here:** ideal pixel identification, homogeneous crystal, no inter-pixel dead regions (the reference
    readout's four-channel Anger positioning, TODO-19, would add mis-positioning); still head (PR-ENV-04).

## Open questions for the author

1. **Which reconstruction does PR-IMG-04 claim?** The engine's binary MLEM resolves ~1.5 elements but needs few
   iterations and splits single sources beyond them. A pixel-area MLEM resolves ~1.25 elements without splitting. CC
   does not reach 95 % within 3 elements. This plan is design-only, so the question is what the requirement promises:
   "an optional likelihood reconstruction", or that reconstruction with a pixel-area forward model.
2. **Over which search field is resolution claimed?** The fully coded field (one period, ±3.6°) or the non-cyclic
   usable field (±7°, EV-02)? CC at 2 elements: 99 % on the first, 68 % on the second.
3. **The resolved-pair definition in the PRS:** v = 0.25 (Rayleigh-like) or 0.5, and a single-source false-split
   ceiling of 5 %. These become the published meaning of "resolves".

## What was not run

- No seed ensemble. Every pass rate comes from one outer seed: either one pair position × 200 repeats, or 16
  positions × 25 repeats.
- The matched MLEM was run at one position only. The near-edge and 1 : 4 conditions were not phase-averaged.
- No ambient run (arithmetic only), no b_i, no ±7° grid (±6° was used), no 2D half-maximum area, no sub-cell
  localisation at 1 m.
- The EV-11 recipe itself was not re-run (its committed N = 64 numbers were used).
- `dotnet test` was not run (no code changed).

## Commands run that touched anything beyond reading

- `dotnet build Gcam.sln -c Release` (repository build outputs only; 0 warnings, 0 errors).
- In `%TEMP%\gcam-todo34\probe`: `dotnet build -c Release` of a scratch console that references the Release DLLs by
  path (no repository project references). Runs of `probe.dll` with the modes `check`, `cost`, `cols`, `fwhm`,
  `fwhmj`, `pairs`, `pairsj`, `nullq`, `diag`, `matched`, `areaj` and `lab`. Outputs are in
  `%TEMP%\gcam-todo34\out\`, helper snippets in `%TEMP%\gcam-todo34\aux\`.
- Python one-off edits of the scratch `Program.cs` only.
- Repository write: this file only.

No approval requests.
