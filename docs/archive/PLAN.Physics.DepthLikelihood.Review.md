# PLAN.Physics.DepthLikelihood.Review — joint x / y / z forward likelihood: design, measurements, gate

Scope: TODO-25 review and measurement turn, 2026-10-02, by a substitute implementer (Claude subagent; Codex out of
credits). Designs the joint (x, y, z) likelihood of [PLAN.Physics.DepthLikelihood](PLAN.Physics.DepthLikelihood.md)
L-1, measures L-2 / L-3 at Studio's default optics, the cost (L-4) and recommends the gate (L-5). No repository code or
test changes; the harness and all outputs are under `%TEMP%\gcam-depthlik-20261002`. Prior work:
[PLAN.Physics.DepthBias.Review](PLAN.Physics.DepthBias.Review.md).

## Result and recommendation

**A joint (x, y, z) Poisson likelihood with the engine's own transport as the forward model localises depth without
the sharpest-plane offsets at every measured point where the search finds the right maximum, with intervals that are
honest except at the highest count rate.**
On the same 60 s floods Studio's sharpest plane (all events) is off by +51 mm and +69 mm on axis at 300 and 500 mm, and by
−19 … −131 mm at 30 mrad up to 700 mm. Its seed-to-seed SD reaches 150–350 mm at 700–1000 mm. The likelihood's mean
error is within ±1.5 mm at 300–700 mm (all events). Its sample spread is 0.7 / 3–3.4 / 4.9–5.1 /
16–20 mm at 300 / 500 / 700 / 1000 mm (60 s, all events), roughly √6 larger at 10 s.

| Gate criterion (L-5) | Measured | Verdict |
|---|---|---|
| bias ≪ spread | 60 s (search v3, 12 seeds per point): \|mean error\| ≤ 0.64 × sample SD and within 2.2 s.e. of zero everywhere except 300 mm / 30 mrad / all events: +0.64 ± 0.20 mm = 0.9 SD at 0.9M histories, +0.12 ± 0.29 mm = 0.16 SD on 7 of those floods refitted at 3.6M, so it is the model budget. 1000 mm all events: +9.9 ± 4.6 (0 mrad), +11.2 ± 5.9 (30 mrad), −3.4 ± 6.3 mm (15 mrad, 8 seeds) — ≤ 0.63 SD, not established as a bias | **met** where the model budget suffices; a few-mm effect at 1000 mm cannot be excluded at this seed count |
| wrong-maximum rate small | 60 s, search v3: **0 / 190** fits (+ 0 / 32 at 15 mrad). 10 s, search v2 only: **11 / 192** — 10 at 300 mm, 1 at 500 mm window. Search v3 repaired all 5 v2 failures in the 60 s 300 mm data; its 10 s pass is still running and is **not** in these numbers | **met at 60 s; not yet measured with v3 at 10 s** |
| coverage near nominal | 60 s without 300 mm all events: 95 % **0.934**, 68 % **0.633** (166 fits). 300 mm all events, 60 s: 95 % **14 / 24** at 0.9M, **14 / 16** at 3.6M (68 %: 8 / 24, 7 / 16). 10 s (v2 fits that are not wrong maxima): 95 % **0.950**, 68 % **0.685** (181 fits) | **met except 300 mm / 60 s / all events** (≈ 45 000 counts), where it depends on the model budget and is not converged at 3.6M histories |
| cost fits a Studio worker | 0.22–0.41 ·10⁹ transported histories per flood (both channels) → ≈ 70–130 s per channel single-threaded at the unloaded 0.62 µs / history; no template build | a cancellable background job after acquisition, not a live 4 Hz update |

**Recommendation for the gate: not yet; go conditionally.** Record the result in Findings now. Plan a Studio
replacement only after three items:

1. The search v3 wrong-maximum rate at 10 s. That pass is running (below); if it stays at 0 / 192, the criterion is met.
2. A model-budget rule set by the data counts. 0.9M histories kept budget-driven shifts ≤ 0.16 seed SD up to
   ≈ 17 000 counts (500 mm / 60 s), though mean Λ there is 1.1–1.7 rather than 1. At ≈ 46 000 counts (300 mm / 60 s)
   the intervals under-cover at 0.9M and still at 3.6M; convergence there is not shown.
3. A mismatch study. Everything here is the simulator fitted with itself. A real camera adds error in the
   mask–detector distance, mask pose, gain map and crystal response, and a D error maps straight into depth: derived,
   **not measured**, δD = +0.5 mm gives ẑ ≈ −6 mm at 1 m.

Until then the descriptive "sharpest plane" stays in Studio.

## Design (L-1)

### Forward model: the engine's own transport, replayed with common random numbers

| Choice | What | Why |
|---|---|---|
| Expected flood | μ_i(x, y, z) = expected detections in pixel i per emitted photon, all events and 662-keV window, for a point source at (x, y, z) | the data are a list-mode acquisition whose pixel distribution is ∝ μ (rejection sampling on the importance weight in `ListModeSource`); with intensity profiled, only the shape matters |
| Emission | the default Cs-137 lines (661.7 keV 0.851, 32.1 keV 0.056, 36.4 keV 0.014) read from `BuildConfig`; entry points uniform on the 18 × 18 mm face (pixel-stratified), weight A·cosθ/(4π r²) | identical to `MixedFieldSource`'s biased emission |
| Mask | the **expected** transmission of the engine's 12-step slab march (`CodedApertureMask.Transmit` without its Bernoulli draw), energy-scaled by `TungstenMuRel` | removes the mask's Bernoulli noise from the model without changing its mean; replica checked against the engine (below) |
| Entrance absorber, crystal, backing, reflector gap | the engine's `ComptonCrystalDetector` itself (Argmax pixel, GAGG tables, 0.15 mm steel, 2 mm backing, 0.1 mm gap), called per history | no re-implementation of the physics; the pixel and deposit come from the same code as the data |
| Window | analytic mean of `MeasurementStage`: deposit × per-pixel gain (σ 0.03, seed 1), then the chain's Gaussian smear, P(|E − 661.7| ≤ 1.5 FWHM) | the smear is Gaussian in `FrontEndModel.Measure`, so the window probability is exact; the gain map is the known calibration map |
| Common random numbers (CRN) | each history k has a fixed entry point, line and a counter-based crystal stream; the **same** histories are replayed for every candidate (x, y, z) | the model's Monte Carlo error is then one fixed, smooth-in-parameters realisation instead of independent noise per template: no template library and no interpolation between independently noisy nodes (the H-7 hazard) |
| Budget | N = 0.9M histories per evaluation (main grids), 1.8M / 3.6M for the convergence checks; a nested 90k prefix as the coarse model | measured below (roughness and budget doubling) |
| Nuisances | intensity A ≥ 0 and a flat per-pixel background B ≥ 0, profiled exactly (Newton on the concave 2-parameter Poisson likelihood, KKT check for B = 0) | activity is unknown in use, so depth must come from the shadow's shape, not from 1/r²; a flat background is the plan's nuisance — the default scene has none, so B̂ is small but not zero |
| Parameters | φ = x/(z − D), ψ = y/(z − D) (bearing seen from the mask) and v = 1/(z − D) | the shadow is shifted by −D·φ and scaled by 1 + D·v, so bearing and depth are nearly orthogonal and the depth information is linear in v |
| Range | z 110 … 3000 mm (Studio's sweep range), all bearings | no truth-centred window |

**Model = acquisition expectation (validated).** Mask replica vs engine (`Transmit` bisected on its uniform): max
|ΔT| = 2.2·10⁻¹⁶ over 40 000 rays at 150–2000 mm, both 662 and 32 keV. Model (1.8M histories) vs independent long
list-mode acquisitions, Pearson χ²/dof over 900 pixels — expected 1 + counts / model-effective-counts if the model is the
expectation and the only extra scatter is its own MC noise (model effective counts measured from two independent model
streams):

| Point | Channel | Data counts (live) | Model eff. counts | Expected χ²/dof | Measured |
|---|---|---:|---:|---:|---:|
| 300 mm, 0 | all | 233 483 (300 s) | 462 700 | 1.50 | 1.47 |
| 300 mm, 0 | win | 71 570 | 140 400 | 1.51 | 1.51 |
| 500 mm, 15 mrad | all | 168 468 (600 s) | 452 600 | 1.37 | 1.28 |
| 500 mm, 15 mrad | win | 51 215 | 143 200 | 1.36 | 1.40 |
| 1000 mm, 30 mrad | all | 114 514 (1800 s) | 512 800 | 1.22 | 1.20 |
| 1000 mm, 30 mrad | win | 35 809 | 143 200 | 1.25 | 1.29 |

(s.d. of χ²/dof for 899 dof ≈ 0.047.) No mismatch beyond the model's own noise is seen.

### Interpolation between template nodes (measured, not assumed)

A node library is not needed with CRN, but the plan asks what interpolation would cost. Measured noise-free: the truth
and the nodes come from the **same** CRN stream (1.8M histories), so only node spacing acts; expected-data (Asimov)
fit of a linear mix of the two neighbouring normalised templates, truth at the node midpoint (worst case) or quarter
point:

| Node spacing | Max \|depth bias\| over 300 / 500 / 700 / 1000 mm, both channels | Expected 60 s LL loss at 300 mm (all) |
|---|---:|---:|
| 5 mm | 0.18 mm | 0.23 |
| 10 mm | 0.34 mm | 0.93 |
| 20 mm | 0.45 mm | 7.96 |
| 40 mm | 1.12 mm | 76.6 |
| bearing 1 mrad | 0.025 mrad | 0.82 |
| bearing 2 mrad | 0.079 mrad | 9.01 |

So the previous review's library offsets of 5–8 mm (H-7) were template-noise, not interpolation: interpolation alone
costs ≤ 0.5 mm at 20 mm nodes. A library would still need nodes ≲ 10 mm near 300 mm to keep the likelihood shape
(LL loss < 1), and noise-correlated templates. Lateral nodes ≤ 1 mrad.

### Model roughness and budget

With CRN the likelihood is a sum of per-history step functions (pixel crossings, slab-cell crossings), so it is
smooth only down to a small roughness. Measured along v at fixed bearing (81 points over ±4σ, quartic fit residual):

| z (60 s, one data seed) | Channel | 0.9M | 1.8M | 3.6M | Smoothed maximum moves (0.9 → 1.8 → 3.6M) |
|---|---|---:|---:|---:|---|
| 500 mm | all | 0.17 | 0.16 | 0.08 | 500.2 → 500.4 → 499.8 mm (σ ≈ 2.5) |
| 500 mm | win | 0.22 | 0.13 | 0.07 | 505.4 → 503.0 → 502.7 mm (σ ≈ 5) |
| 1000 mm | all | 0.09 | 0.06 | 0.04 | 1003 → 1008 → 999 mm (σ ≈ 15) |
| 1000 mm | win | 0.07 | 0.07 | 0.04 | 1055 → 1055 → 1055 mm (σ ≈ 40) |

(residual RMS in log-likelihood units.) The roughness in LL units grows with the data counts, so the most demanding case
is 300 mm / 60 s (≈ 46 000 counts).

### Estimator

1. **Coarse depth scan**: 140 nodes uniform in v over 110–3000 mm on the 90k coarse model, grid origin offset per data
   set (no node can coincide with a truth), at the bearing Studio's decoder finds at 1000 mm.
2. **Candidates** (search v3, below): the three best coarse depth maxima, each re-seeded with Studio's decode at its own
   plane, plus one candidate seeded from Studio's own focus sweep (81 planes, sharpest prominence); each optimised in
   3-D on the coarse model (Nelder–Mead).
3. **Refine** on the full model by a **quadratic response surface**: a 3³ factorial design around the candidate, fitted
   by least squares, re-centred and re-sized to ±2.5 σ from its own Hessian (2–4 rounds). This averages over the
   roughness and adapts to the very different φ and v scales; Nelder–Mead on the full model stalled on the roughness
   (negative Λ down to −3.5 in the first smoke test). Fallback (flagged): Nelder–Mead.
4. **Interval / coverage**: Λ = 2[ℓ_p(v̂) − ℓ_p(v_true)], where ℓ_p is the bearing-profiled log-likelihood, both terms
   computed by the **same** 3² bearing surface (an earlier version mixing the 3-D and 2-D surfaces gave Λ < 0 by 1–3
   units). The 68 % / 95 % LR interval contains the truth iff Λ ≤ 1 / 3.84 (χ²₁, Wilks), so coverage is counted from Λ
   without root-finding. σ_Hessian = marginal σ_z from the 3-D surface (for width reporting only).
5. **Wrong maximum**: Λ > 25 (truth excluded at > 5σ: a different maximum won) or Λ < −10 (a higher maximum near the
   truth exists that the search missed).

### Search history (why v3)

| Version | Bearing seed | Failure found |
|---|---|---|
| v1 | Studio decode at 1000 mm, re-decoded at the coarse-best plane; second coarse maximum refined only within 25 coarse LL units | 500 mm / 15 mrad / 60 s window: the 1000 mm decode put the bearing ~4–5 mrad off; at that bearing a near-field artefact (≈ 148 mm) won the coarse scan by 63 units and the truth basin was dropped, although its likelihood is ~760 units higher |
| v2 | top three coarse maxima, each with Studio's decode at its own plane and a coarse 3-D optimisation before the margin | 300 mm window: Studio's 1000 mm decode of a 300 mm source returns a spurious peak at ψ ≈ −45 mrad (field edge) in every seed; in 5 of 16 window fits all three candidates inherited wrong bearings and the fit settled on a background-dominated maximum (B̂ 4–12 per pixel, against ≈ 0.3 in good fits) |
| v3 | v2 plus a candidate seeded by Studio's own focus sweep (sharpest-prominence plane and its bearing); every candidate distinct from v2's answer (> 1 mrad or > 2·10⁻⁴ in v) is refined; the larger likelihood wins | none in the grids below (wrong maxima counted per condition) |

v3 is implemented as v2 followed by the extra candidate (the v2 result is kept unless the extra candidate refines to a
higher likelihood), which is the same estimator as running v3 from scratch. No step uses the truth.

## Measurements (L-2, L-3)

### Conditions

Studio defaults through `SimulationService.BuildConfig`: rank 13 MURA 2×2 mosaic, 0.7 mm cell, D = 80 mm, 10 mm W
(0.178 /mm at 662 keV), 30 × 0.6 mm GAGG 10 mm, 0.15 mm entrance absorber, 2 mm backing, 0.1 mm gap, gain σ 0.03
(seed 1), default chain (window ±1.5 FWHM = ±45.3 keV around 661.7 keV). Source: Cs-137 500 µCi (17 038 500 photons/s
over its three lines) at (z·tan a, 0, z), z = distance from the detector. Data: `ListModeSource` → `MeasurementStage`,
events up to the live time. No background, dark counts or pile-up in the data (B is still profiled).

| Set | Points | Live | Data seeds | Search | Model |
|---|---|---|---|---|---|
| 60 s grid | z 300 / 500 / 700 / 1000 mm × 0 / 30 mrad | 60 s | 12 per point: 30 000 000 + 1000 z + 37 a + 7 T + 100 003 s, s = 0 … 11 | v3 | 0.9M, stream 1 |
| 15 mrad set | 500 / 1000 mm × 15 mrad | 60 s | 8 per point: base 9 100 000, same formula | v1 + the v3 sweep candidate | 0.9M, stream 1 |
| 10 s grid | as the 60 s grid | 10 s | 12 per point: base 20 000 000 | **v2** (v3 pass running) | 0.9M, stream 1 |
| Budget doubling | the 15 mrad set's data | 60 s | 8 per point | v1 | 0.9M / 1.8M / 3.6M (nested, stream 1) and 1.8M stream 2 |
| 300 mm budget | 16 of the 24 floods of the 60 s grid at 300 mm | 60 s | as above | refine from the v3 estimate | 3.6M |

The data seeds are disjoint from the model's counter-based streams, so every validation is held out from the model. No
truth enters the estimator, and the coarse depth grid's origin is offset per data set.

Columns: bias and s.e. over the fits that are not wrong maxima; σ_Hessian = median marginal σ_z from the response
surface; coverage counts every fit, wrong maxima included; mean Λ excludes wrong maxima. Wrong maximum = Λ > 25, Λ < −10
or a bearing more than 3 mrad from the truth (a third of a resolution element; the bearing precision is 0.1–0.5 mrad).
"Surface fallback" = the response surface was not negative definite and full-model Nelder–Mead was used; those fits
have no σ_Hessian.

### 60 s, search v3 (190 of 192 fits)

| live | channel | z mm | angle mrad | seeds | mean counts | bias ± s.e. mm | median error mm | sample SD mm | median σ_Hessian mm | SD / σ | wrong max | surface fallback | 68 % cover | 95 % cover | mean Λ |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 60 s | all | 300 | 0 | 12 | 46407 | -0.13 ± 0.20 | -0.08 | 0.71 | 0.56 | 1.26 | 0 | 0 | 6/12 | 9/12 | 1.50 |
| 60 s | all | 300 | 30 | 12 | 44208 | +0.64 ± 0.20 | +0.84 | 0.71 | 0.52 | 1.35 | 0 | 0 | 2/12 | 5/12 | 3.95 |
| 60 s | all | 500 | 0 | 12 | 17044 | -1.45 ± 0.97 | -1.59 | 3.36 | 2.88 | 1.17 | 0 | 0 | 6/12 | 11/12 | 1.67 |
| 60 s | all | 500 | 30 | 12 | 15507 | -0.76 ± 0.85 | -1.10 | 2.96 | 2.78 | 1.07 | 0 | 0 | 9/12 | 10/12 | 1.09 |
| 60 s | all | 700 | 0 | 12 | 8774 | +0.17 ± 1.41 | -0.03 | 4.88 | 5.67 | 0.86 | 0 | 0 | 7/12 | 12/12 | 0.81 |
| 60 s | all | 700 | 30 | 12 | 7815 | +1.27 ± 1.46 | +1.03 | 5.05 | 6.66 | 0.76 | 0 | 0 | 10/12 | 12/12 | 0.25 |
| 60 s | all | 1000 | 0 | 12 | 4395 | +9.87 ± 4.56 | +10.54 | 15.78 | 18.72 | 0.84 | 0 | 0 | 7/12 | 12/12 | 0.92 |
| 60 s | all | 1000 | 30 | 11 | 3805 | +11.15 ± 5.94 | +16.35 | 19.70 | 21.46 | 0.92 | 0 | 0 | 6/11 | 11/11 | 1.10 |
| 60 s | win | 300 | 0 | 12 | 14262 | -0.03 ± 0.31 | +0.18 | 1.07 | 1.20 | 0.89 | 0 | 0 | 9/12 | 12/12 | 0.09 |
| 60 s | win | 300 | 30 | 12 | 13666 | -0.03 ± 0.38 | -0.04 | 1.33 | 1.08 | 1.23 | 0 | 0 | 6/12 | 10/12 | 1.91 |
| 60 s | win | 500 | 0 | 12 | 5220 | -0.13 ± 1.92 | +1.04 | 6.64 | 5.77 | 1.15 | 0 | 0 | 6/12 | 10/12 | 1.72 |
| 60 s | win | 500 | 30 | 12 | 4833 | +0.27 ± 1.70 | +0.09 | 5.89 | 5.76 | 1.02 | 0 | 0 | 6/12 | 11/12 | 1.42 |
| 60 s | win | 700 | 0 | 12 | 2682 | -7.95 ± 3.60 | -9.01 | 12.49 | 12.78 | 0.98 | 0 | 0 | 6/12 | 11/12 | 1.49 |
| 60 s | win | 700 | 30 | 12 | 2445 | -1.82 ± 3.72 | -1.01 | 12.88 | 15.27 | 0.84 | 0 | 0 | 9/12 | 12/12 | 0.35 |
| 60 s | win | 1000 | 0 | 12 | 1340 | +5.79 ± 9.73 | +10.83 | 33.72 | 42.80 | 0.79 | 0 | 0 | 9/12 | 11/12 | 0.75 |
| 60 s | win | 1000 | 30 | 11 | 1191 | -22.35 ± 14.09 | -27.85 | 46.72 | 44.17 | 1.06 | 0 | 2 | 9/11 | 10/11 | 0.66 |

Pooled: 68 % 113 / 190 = 0.595, 95 % 169 / 190 = 0.889; without 300 mm all events 0.633 / 0.934 (166 fits, mean Λ
1.02). Missing: one flood at 1000 mm / 30 mrad (seed 31 201 536), whose v3 pass did not run (the resumed pass could not
open its output file). Its v2 result: all events 1019.7 mm, Λ 1.32; window 958.7 mm, Λ −1.07.

Studio's sharpest plane on the **same** floods (81 inverse-distance planes, prominence, K = 1), mean error / seed SD in
mm, against the likelihood:

| Channel | z | Angle | Sharpest plane | Likelihood |
|---|---:|---:|---:|---:|
| all | 300 | 0 | +50.7 / 0.0 | −0.13 / 0.71 |
| all | 300 | 30 | −19.1 / 8.1 | +0.64 / 0.71 |
| all | 500 | 0 | +69.3 / 0.0 | −1.45 / 3.36 |
| all | 500 | 30 | −40.3 / 9.3 | −0.76 / 2.96 |
| all | 700 | 0 | −69.7 / 150.1 | +0.17 / 4.88 |
| all | 700 | 30 | −130.7 / 0.0 | +1.27 / 5.05 |
| all | 1000 | 0 | +59.1 / 351.1 | +9.87 / 15.78 |
| all | 1000 | 30 | +18.8 / 299.4 | +11.15 / 19.70 |
| win | 300 | 0 | +11.0 / 18.8 | −0.03 / 1.07 |
| win | 300 | 30 | −26.0 / 10.0 | −0.03 / 1.33 |
| win | 500 | 0 | +22.0 / 58.5 | −0.13 / 6.64 |
| win | 500 | 30 | −36.3 / 11.8 | +0.27 / 5.89 |
| win | 700 | 0 | −29.6 / 58.5 | −7.95 / 12.49 |
| win | 700 | 30 | −107.5 / 57.4 | −1.82 / 12.88 |
| win | 1000 | 0 | +111.6 / 317.0 | +5.79 / 33.72 |
| win | 1000 | 30 | +17.7 / 138.2 | −22.35 / 46.72 |

### 60 s, 15 mrad (v1 search + the v3 sweep candidate)

| live | channel | z mm | angle mrad | seeds | mean counts | bias ± s.e. mm | median error mm | sample SD mm | median σ_Hessian mm | SD / σ | wrong max | surface fallback | 68 % cover | 95 % cover | mean Λ |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 60 s | all | 500 | 15 | 8 | 16770 | -0.74 ± 0.88 | -0.75 | 2.48 | 2.37 | 1.04 | 0 | 0 | 5/8 | 8/8 | 1.00 |
| 60 s | all | 1000 | 15 | 8 | 4137 | -3.36 ± 6.29 | +0.28 | 17.80 | 18.21 | 0.98 | 0 | 0 | 6/8 | 7/8 | 0.73 |
| 60 s | win | 500 | 15 | 8 | 5107 | -0.26 ± 1.07 | -0.51 | 3.03 | 5.25 | 0.58 | 0 | 0 | 6/8 | 8/8 | 0.63 |
| 60 s | win | 1000 | 15 | 8 | 1275 | -13.95 ± 12.26 | -10.21 | 34.67 | 37.88 | 0.92 | 0 | 0 | 7/8 | 7/8 | 1.24 |

The v1 search alone had one wrong maximum here (500 mm window, seed 10 100 990: 148.6 mm); the sweep candidate repaired it.

### 10 s, search v2 (complete; v3 not yet applied)

| live | channel | z mm | angle mrad | seeds | mean counts | bias ± s.e. mm | median error mm | sample SD mm | median σ_Hessian mm | SD / σ | wrong max | surface fallback | 68 % cover | 95 % cover | mean Λ |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 10 s | all | 300 | 0 | 12 | 7726 | -1.20 ± 0.44 | -1.03 | 1.45 | 1.31 | 1.10 | 1 | 0 | 5/12 | 11/12 | 2.19 |
| 10 s | all | 300 | 30 | 12 | 7314 | +0.85 ± 0.45 | +1.12 | 1.43 | 1.33 | 1.08 | 2 | 1 | 6/12 | 9/12 | 1.56 |
| 10 s | all | 500 | 0 | 12 | 2880 | +1.73 ± 1.20 | +2.74 | 4.17 | 6.21 | 0.67 | 0 | 0 | 9/12 | 12/12 | 0.83 |
| 10 s | all | 500 | 30 | 12 | 2591 | +0.84 ± 1.61 | +1.33 | 5.57 | 6.56 | 0.85 | 0 | 0 | 9/12 | 12/12 | 0.77 |
| 10 s | all | 700 | 0 | 12 | 1448 | -0.42 ± 4.40 | -1.75 | 15.25 | 13.76 | 1.11 | 0 | 0 | 8/12 | 11/12 | 0.90 |
| 10 s | all | 700 | 30 | 12 | 1321 | -0.21 ± 5.27 | -3.57 | 18.26 | 16.22 | 1.13 | 0 | 0 | 8/12 | 11/12 | 1.15 |
| 10 s | all | 1000 | 0 | 12 | 706 | +0.45 ± 10.21 | +0.13 | 35.36 | 48.70 | 0.73 | 0 | 0 | 9/12 | 12/12 | 0.62 |
| 10 s | all | 1000 | 30 | 12 | 643 | +2.55 ± 10.29 | -6.52 | 35.63 | 52.67 | 0.68 | 0 | 0 | 10/12 | 12/12 | 0.52 |
| 10 s | win | 300 | 0 | 12 | 2365 | -1.13 ± 0.58 | -1.77 | 1.74 | 2.90 | 0.60 | 3 | 5 | 5/12 | 9/12 | 1.62 |
| 10 s | win | 300 | 30 | 12 | 2261 | +0.56 ± 1.15 | +1.27 | 3.24 | 2.90 | 1.12 | 4 | 4 | 7/12 | 9/12 | 1.62 |
| 10 s | win | 500 | 0 | 12 | 870 | +0.53 ± 3.03 | +4.28 | 10.04 | 14.02 | 0.72 | 1 | 1 | 10/12 | 12/12 | 0.34 |
| 10 s | win | 500 | 30 | 12 | 811 | -2.35 ± 3.09 | -4.18 | 10.71 | 14.92 | 0.72 | 0 | 0 | 9/12 | 12/12 | 0.45 |
| 10 s | win | 700 | 0 | 12 | 442 | -7.68 ± 9.49 | -9.88 | 32.87 | 38.02 | 0.86 | 0 | 1 | 7/12 | 11/12 | 1.22 |
| 10 s | win | 700 | 30 | 12 | 420 | -24.35 ± 9.80 | -12.90 | 33.94 | 40.42 | 0.84 | 0 | 3 | 10/12 | 11/12 | 0.64 |
| 10 s | win | 1000 | 0 | 12 | 211 | -1.26 ± 24.59 | -11.14 | 85.18 | 124.36 | 0.68 | 0 | 0 | 8/12 | 12/12 | 0.69 |
| 10 s | win | 1000 | 30 | 12 | 204 | -49.09 ± 22.92 | -48.06 | 79.41 | 123.43 | 0.64 | 0 | 2 | 9/12 | 11/12 | 0.51 |

Pooled: 68 % 129 / 192, 95 % 177 / 192 over all fits; over the 181 that are not wrong maxima 0.685 / 0.950 (mean Λ
0.94). Wrong maxima (11): 10 at 300 mm (7 window, 3 all events) and 1 at 500 mm window. Their bearings are 4–77 mrad off,
or the truth basin is 136–1586 log-likelihood units higher than the reported maximum. These are search failures of the
kind v3 repaired at 60 s. On the 78 10 s fits that the v1 search also completed, v1 had 12 wrong maxima and v2 had 4.

### 300 mm, 60 s, refitted at 3.6M histories (from the v3 estimates)

| live | channel | z mm | angle mrad | seeds | mean counts | bias ± s.e. mm | median error mm | sample SD mm | median σ_Hessian mm | SD / σ | wrong max | surface fallback | 68 % cover | 95 % cover | mean Λ |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 60 s | all | 300 | 0 | 9 | 46425 | +0.24 ± 0.27 | +0.21 | 0.81 | 0.55 | 1.47 | 0 | 0 | 5/9 | 8/9 | 1.93 |
| 60 s | all | 300 | 30 | 7 | 44330 | +0.12 ± 0.29 | +0.27 | 0.77 | 0.55 | 1.42 | 0 | 0 | 2/7 | 6/7 | 2.38 |
| 60 s | win | 300 | 0 | 9 | 14265 | +0.67 ± 0.41 | +1.13 | 1.23 | 1.19 | 1.04 | 0 | 0 | 5/9 | 9/9 | 1.34 |
| 60 s | win | 300 | 30 | 7 | 13689 | +0.20 ± 0.38 | +0.26 | 1.00 | 1.14 | 0.87 | 0 | 0 | 5/7 | 7/7 | 0.66 |

Paired with the same floods at 0.9M, five of the six all-events fits with Λ > 4 fall (7.20 → 2.05, 8.56 → 2.35,
6.49 → 2.64, 4.11 → 0.70, 5.67 → 3.43; one 5.87 → 6.34). At 3.6M, Λ agrees with (error / σ_Hessian)², the 30 mrad
bias falls from +0.64 to +0.12 mm, and the 95 % coverage rises from 14 / 24 to 14 / 16. The excess is the model
budget, not the likelihood. It is still above nominal at 3.6M (SD / σ 1.4–1.5, 68 % coverage 7 / 16), so ≈ 46 000
counts need more than 3.6M histories or a smoother model. The window channel (≈ 14 000 counts) is nominal already at
0.9M.

### Convergence (budget doubling, L-2)

Same 8 data floods per point (500 / 1000 mm, 15 mrad, 60 s), refit with nested budgets; differences to 3.6M (stream 1);
one v1 search failure excluded from the 500 mm window pairs:

| Point | Channel | Seed SD | 0.9M − 3.6M mean / SD | 1.8M − 3.6M mean / SD | 1.8M stream 2 − 3.6M mean / SD |
|---|---|---:|---:|---:|---:|
| 500 mm | all | 2.5 | −0.40 / 0.33 | −0.10 / 0.23 | +0.04 / 0.29 |
| 500 mm | win | 3.2–4.2 | +0.48 / 1.22 | +0.24 / 0.93 | −0.46 / 1.20 |
| 1000 mm | all | 17–18 | +0.34 / 3.36 | +0.18 / 2.14 | +0.59 / 3.71 |
| 1000 mm | win | 35–37 | −2.17 / 13.66 | +2.34 / 5.15 | −3.24 / 5.60 |

(mm.) Mean shifts from 0.9M are ≤ 0.16 seed SD, so the 0.9M estimates have stopped moving beyond the seed spread at
these counts. Per-fit jitter is ≤ 0.2 SD, except 0.4 SD for the 1000 mm window (≈ 1300 counts, flat likelihood). At
300 mm / 60 s (≈ 46 000 counts) 0.9M is not converged (above).

## Cost (L-4)

| Item | Measured |
|---|---|
| Model evaluation | 0.62 µs per history single-threaded (0.9M → 0.56 s), measured on the shared machine before the long runs |
| Template build | none: the model is evaluated on demand; the CRN arrays for 0.9M histories are ≈ 28 MB |
| One flood, both channels, v2 search + intervals | 218 M (1000 mm / 60 s) … 415 M (300 mm / 10 s) transported histories |
| v3's extra candidate | 81 Studio decodes + about 40 M coarse histories, plus one full refine (≈ 100 M) when it is distinct |
| Wall time observed | 330 … 4340 s per flood single-threaded under today's load (1.5–12.8 µs / history; other sessions saturated the CPU). Unloaded: ≈ 70–130 s per channel |

A Studio worker could run it as a cancellable background job after an acquisition. The history loop parallelises
trivially (one crystal instance per thread), so on 24 logical cores it is of order 10 s per channel — an **estimate,
not measured**. It is not a live 4 Hz update; Studio's sweep is 81 decodes.

## What could not be run / limits

- **10 s grid with search v3**: running (`v3 … main10v3`), not in the numbers; the 10 s wrong-maximum rate above is v2's.
- One 60 s flood (1000 mm / 30 mrad, seed 31 201 536) lacks its v3 pass (v2 result given above).
- 15 mrad only at 500 / 1000 mm, 60 s, 8 seeds, and with the v1 search plus the sweep candidate rather than v2 plus
  it; none at 300 / 700 mm or 10 s.
- Budget doubling only at 500 / 1000 mm, 15 mrad, 60 s (8 seeds), and a 3.6M refit of 16 floods at 300 mm. No 7.2M,
  none at 700 mm or 10 s (lower counts). Model-stream swap only at 1.8M.
- 12 seeds per point: coverage per point is ±1–2 fits; the pooled coverage (166–190 fits) is the reliable figure.
- Coverage is counted from Λ at the truth (equivalent to "truth inside the LR interval" while the profile is monotone);
  interval end points were not root-found; widths are Hessian σ.
- Simulation fitted with itself. Not measured: D, mask pose, gain-map or crystal-model errors; y / diagonal bearings;
  background in the data; several sources; a real detector. The D sensitivity in the result section is derived, not
  measured.
- Unit tests not run: no repository code changed. No desktop, Studio launch or UI tests.
- Process state: two of my runs are still running (APPROVAL REQUESTS). The v1 10 s grid was lowered to Idle priority
  (reversible) so it only uses spare cycles; it is used here only for the v1 / v2 comparison.

## Reproduction

Harness: `%TEMP%\gcam-depthlik-20261002`.

| File | Contents |
|---|---|
| `Probe.csproj` | references `src/Gcam.Studio.Services` of this worktree |
| `Model.cs` | constants from `BuildConfig`, acquisition, CRN forward model, analytic window |
| `Lik.cs` | profiled Poisson likelihood |
| `Fit.cs` | coarse scan, search, response-surface refine, matched profiles, CSV row |
| `Rsm.cs`, `V3.cs`, `Refit.cs`, `Interp.cs`, `Rough.cs` | response surface; sweep-seeded candidate pass; budget refit; interpolation; roughness |
| `Program.cs` | modes |
| `v1_search_block.txt` | the v1 search, to rebuild the v1 runs |
| `tables.py`, `conv.py`, `rough.py`, `summ.py` | summaries |

| Output folder | Contents |
|---|---|
| `out4` | roughness |
| `out5` | interpolation |
| `out8` | v1 build: `fit_conv_*`, `fit_main10` |
| `out9` | v2 build: `fit_main60v2`, `fit_main10v2` |
| `out12` | current build: `fit_main60v3`, `fit_main10v3` (in progress), `fit_conv15v3`, `fit_r300_n36`, `cost.txt` |
| `out6`, `out7`, `out11` | superseded smoke runs (Nelder–Mead refine, mixed-surface Λ); no result here uses them |

```powershell
$d = Join-Path $env:TEMP 'gcam-depthlik-20261002'; cd $d
dotnet build Probe.csproj -c Release -o outR
dotnet outR\Probe.dll maskcheck                       # mask replica vs engine
dotnet outR\Probe.dll validate 1800000                # model vs long acquisitions (chi2)
dotnet outR\Probe.dll scan 180000 600                 # LL along depth at the true bearing
dotnet outR\Probe.dll interp 1800000 > interp.csv     # node-interpolation cost (noise-free)
dotnet outR\Probe.dll rough 3600000                   # roughness vs budget
# fit <N> <nCoarse> <kCoarse> <seeds> <seedBase> <z;..> <a;..> <T;..> <tag> [modelStream] [bg] [threads] [firstSeed]
dotnet outR\Probe.dll fit 900000 90000 140 12 30000000 "300;500;700;1000" "0;30" 60 main60v2 1 1 8
dotnet outR\Probe.dll v3 900000 90000 140 outR\fit_main60v2.csv main60v3 8
dotnet outR\Probe.dll fit 900000 90000 140 12 20000000 "300;500;700;1000" "0;30" 10 main10v2 1 1 8
dotnet outR\Probe.dll v3 900000 90000 140 outR\fit_main10v2.csv main10v3 10
dotnet outR\Probe.dll refit 3600000 1 outR\fit_main60v3.csv 300 r300_n36 8
dotnet outR\Probe.dll cost 900000
# budget doubling / 15 mrad: v1 build (v1_search_block.txt pasted into Fit.cs), then
#   fit {900000|1800000|3600000} 90000 140 8 9100000 "500;1000" 15 60 conv_n{09|18|36}_s1 1 1 6
#   fit 1800000 90000 140 8 9100000 "500;1000" 15 60 conv_n18_s2 2 1 6 ; v3 900000 90000 140 fit_conv_n09_s1.csv conv15v3 6
python tables.py outR\fit_main60v3.csv
```

The v2 grids were built before the `Finish` refactor and the history counter, which only reformat output. Results are
deterministic per data seed, model stream and budget. The 300 mm refit used a 16-flood snapshot of the 60 s grid.

## APPROVAL REQUESTS

None needed for the results above. Two of my processes are still running, writing only under `%TEMP%`:

| PID | Run | Suggestion |
|---|---|---|
| 63228 | `v3 … v2_main10.csv main10v3 10` — the 10 s grid with search v3 (gate item 1) | let it finish (≈ 1–2 h at today's load); or `Stop-Process -Id 63228`, which loses only unfinished rows (finished rows stay; the pass resumes from them) |
| 50696 | `fit … main10 …` — v1 10 s grid, Idle priority, 78 / 192 fits | `Stop-Process -Id 50696`: v1 is superseded; loses only unfinished v1 rows; undo = rerun the v1 build |
