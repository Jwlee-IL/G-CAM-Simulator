# PLAN.Physics.DepthLikelihood.Review — joint x / y / z forward likelihood: design, measurements, gate

Scope: TODO-25 review and measurement turn, 2026-10-02, by a substitute implementer (Claude subagent; Codex out of
credits). Designs the joint (x, y, z) likelihood of [PLAN.Physics.DepthLikelihood](PLAN.Physics.DepthLikelihood.md)
L-1, measures L-2 / L-3 at Studio's default optics, the cost (L-4) and recommends the gate (L-5). No repository code or
test changes; the harness and all outputs are under `%TEMP%\gcam-depthlik-20261002`. Prior work:
[PLAN.Physics.DepthBias.Review](PLAN.Physics.DepthBias.Review.md).

> **Handoff note (planner, 2026-10-02):** this review is an **unfinished draft** — the substitute implementer was
> still measuring (estimator v3: 60 s main grid and a 15-s convergence set) when the session had to stop. The design
> sections below are complete; **no result, gate recommendation or coverage number is established yet**. The probe and
> outputs lived in a local temporary folder that does not travel with the repository; continue by re-running the design
> below (or start a new review turn from this text).

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
| v2 | top three coarse maxima, each with Studio's decode at its own plane and a coarse 3-D optimisation before the margin | 300 mm window: Studio's 1000 mm decode of a 300 mm source returns a spurious peak at ψ ≈ −45 mrad (field edge) in every seed; in 5 of 16 window fits all three candidates inherited wrong bearings and the fit settled on a background-dominated maximum (B̂ ≈ 12 per pixel) |
| v3 | v2 plus a candidate seeded by Studio's own focus sweep (sharpest-prominence plane and its bearing); every candidate distinct from v2's answer (> 1 mrad or > 2·10⁻⁴ in v) is refined; the larger likelihood wins | none in the grids below (wrong maxima counted per condition) |

v3 is implemented as v2 followed by the extra candidate (the v2 result is kept unless the extra candidate refines to a
higher likelihood), which is the same estimator as running v3 from scratch. No step uses the truth.

@@MEASUREMENTS@@
