# PLAN.Physics.BackgroundShapeDecoding — decoding with the background's shape under the ambient field

Scope: TODO-33 (from TODO-30 AB-12). Under the absolute ambient field in the bare-crystal bound the transported
background is not flat (edge / centre 2.7 lab, 3.3 hand-held), and the raw cross-correlation decoder's peak is pulled
by it: a switch around B/S ≈ 0.6–1.3, saturating at ~8 mm lab (2.9°), ~9 mm hand-held (3.4°), ~19 mm at 1 m (1.1°),
~40–50 mm at 5 m (0.5°); MLEM without a background term is pulled more slowly but is worse at low counts even ideal
(Findings theme 64, `samples/evidence/results/ambient-baseline-v1-turn8-bias.json`). TODO-30's calibrated search
statistic (background shape subtracted, studentised) locates correctly. This plan makes the decoders use a background
shape, measures what that recovers and what it costs, and decides how the scale of the background is known.

Status: **done** 2026-10-07 — review and implementation by Codex (session `01a1151e-8e7f-7a70-9404-c6ff4c8cb8e5`; [review](PLAN.Physics.BackgroundShapeDecoding.Review.md), turn reports [2](PLAN.Physics.BackgroundShapeDecoding.Turn2.md), [3](PLAN.Physics.BackgroundShapeDecoding.Turn3.md), [4](PLAN.Physics.BackgroundShapeDecoding.Turn4.md), [5](PLAN.Physics.BackgroundShapeDecoding.Turn5.md)); the long validation and sensitivity runs were started by the planner (no implementer turn open); planner checks: build 0 errors (2 known analyzer warnings), full tests 815 passed / 28 skipped, calibration `--release` passes, one seed of each new family re-run identical apart from timing; records: Findings theme 67, EV-35, LIM-10, D-52 … D-55, Backlog stage 2.

## What exists (checked in the code, 2026-10-07)

| Item | Where | What it does |
|---|---|---|
| Decoder as a matrix | `src/Gcam.Simulation/CorrelationSearch.cs` | the scenario's cross-correlation decoder read off as ±1 / 0 columns G; `Reconstruct` reproduces `CrossCorrelationDecoder.Decode` exactly; `DecoderEstimate` = argmax + sub-cell, as the decoder |
| Background model | `CorrelationSearch.ModelFor(shape)` | per-count moments E₁(θ) = Σ G p, V₁(θ) = Σ G² p − E₁² for a normalised shape p |
| Search statistic | `CorrelationSearch.Search(model, total, recon)` | max over the grid of Z = (recon − N·E₁) / √(N·V₁), **N = the acquisition total** (with a source present this over-estimates the background); returns Z and its grid point (grid point only, no sub-cell) |
| Gate study | `src/Gcam.Simulation/AmbientGateStudy.cs` | Poisson flood maps λ = activity·t·source map + field·t·**truth** ambient map; the shape model comes from an **independent** MC map (`calibration`, other stream seed, `CalibrationHistories`); per acquisition records the raw decoder estimate (signed error → the AB-12 pull) and the search hit (gate) |
| Bias baseline | `samples/evidence/ambient/bias_baseline.py` → `results/ambient-baseline-v1-turn8-bias.json` | signed mean pull per condition (family `gate_validation_v2`, 128 seeds × 300) and the paired ideal pull; EV-01 sweep's fixed points (cross-correlation and MLEM pulls, noiseless pull of the expected ambient map) |
| Calibrated subtraction | `src/Gcam.Simulation/AmbientAntimaskStudy.cs` (EV-12 (b)) | image minus **field × t × model map** (scale known exactly), decoded by the scenario decoder: 2.24 / 1.69 mm at the highest level where raw single-mask collapses to ~9 mm — i.e. the subtraction already works **when the scale is known** |
| MLEM background term | `src/Gcam.Decoding/MlemDecoder.cs` `Decode(image, background)`, `Snapshots(…, background)` | forward model Σ A λ + b with a **fixed, known** b_i; used by Studio's strip path; not used by any ambient study |
| MLEM in the ambient sweep | `AmbientSweepStudy.Mlem` | pixel-centre model (default `MlemSystemModel` = null), the sweep's own iteration count, no background term — **not** the pixel-area model of D-46 / Studio (400 iterations) |
| Ambient in Studio | Studio default 0.10 µSv/h, **front-only** bound | where the field's excess pull is ≤ 0.73 mm (theme 64): Studio does not show the effect today |

## Proposed decisions (verify / improve)

| ID | Decision |
|---|---|
| BG-1 | **Estimators to compare** (verify the list; propose better ones): E0 raw cross-correlation (baseline); E1 cross-correlation minus N·E₁ (the Z numerator, total as background scale — note it also subtracts the source counts' share of the shape, which may pull the other way); E2 the Z argmax (existing, grid point only — add sub-cell?); E3 cross-correlation minus B̂·E₁ with B̂ from a data-driven estimate (e.g. iterate B̂ = N − Ŝ, or a two-parameter fit at the peak); E4 MLEM with an extra **background column** p whose amplitude is estimated by the EM itself (non-negative, no external scale); E5 MLEM with a fixed b = B̂·p (B̂ known, see BG-2). Use the pixel-area MLEM (D-46) and say what iteration rule applies |
| BG-2 | **Where the scale comes from (verify; likely an author decision):** (a) from the image alone (E1 / E3 / E4); (b) from the separate dose counter (D-37): field × t × calibrated rate per µSv/h, with the counter's and the rate calibration's uncertainty propagated — measure how wrong the scale may be before the pull returns; (c) both, data-driven with the dose counter as a check |
| BG-3 | **Where the shape comes from (verify):** the independent transported map (as the gate study) and, as the realistic product case, a **measured background acquisition** of finite live time (Poisson-noisy shape) — measure the pull and the cost vs that live time (e.g. 10 min / 1 h) and under a wrong bound (front-only shape on a bare truth), so the requirement can state what calibration the shape needs |
| BG-4 | **Measurement:** the AB-12 conditions (cases lab / head / 1 m / 5 m, centre and edge, open and 662 keV windows, both bounds, the three fields, the source levels) with selection / development seeds disjoint from validation seeds; per estimator signed pull, excess pull over the ideal, RMS, fraction within one resolution element, and the **ideal-environment cost** (an estimator must not degrade the no-field case beyond a derived tolerance); runs through the TODO-37 provenance driver (new manifest family) |
| BG-5 | **Engine surface (verify):** a background-shape option for the decoders (cross-correlation and MLEM) usable by studies and the single run, configured explicitly (a shape file or map + scale rule), default off so every existing result is bit-identical; propose the config form and whether `CorrelationSearch` hosts it |
| BG-6 | **Studio (proposed: not in this task):** Studio's default field is front-only, where the excess is ≤ 0.73 mm; Studio gets the option only if the author wants it after the measurement |
| BG-7 | **Records:** Findings theme 67, EV entries whose ambient numbers change (EV-01 / EV-12 single mask / the AB-12 baseline), a limitation if a residual remains, decisions D-52… for the author's choices — proposed by the implementer, applied by the planner |

## Decisions after review (2026-10-07)

The review ([PLAN.Physics.BackgroundShapeDecoding.Review](PLAN.Physics.BackgroundShapeDecoding.Review.md); development
seeds 330001 / 330007 / 330019, 3 × 16 acquisitions per condition, 1,968 summaries) corrected the plan: the existing
`AngularResolutionStudy` already runs a fixed-b MLEM against independent ambient maps (narrow window only); the AB-12
MLEM was pixel-centre at 80 iterations, not D-46's model; the engine default is 120 iterations and Studio's 400 is a rule
for Studio's optics only; `CorrelationSearch`'s "only lowers Z" is not general; the old scalar excess (difference of
norms) can hide a rotating bias. Measured: the raw pull is reproduced; joint background MLEM (E4) matches the known-scale
MLEM (E5) without knowing the scale and beats raw cross-correlation even ideal at 1 m / 5 m (e.g. 1 m, S = 1000, 0.10
µSv/h, 60 s: E4 RMS 3.83 mm vs E0 28.3 mm; 5 m 10.0 vs 38.5 mm); the total-count subtraction E1 / E2 costs ideal
performance and fails at the far edge; **no estimator recovers ideal precision at low counts and high background** (1 m,
S = 250, 0.10 µSv/h, 60 s: E4 RMS 61.3 mm, 52 % within one element). The author answered the four questions with the
recommended option each time.

| ID | Decision | Source |
|---|---|---|
| BD-1 | **Estimators:** E4 joint background MLEM (a normalised background component p with a non-negative amplitude fitted by the EM, kept separate from the source grid) is the opt-in correction; E5 (fixed b = B·p, MLEM) and E6 (signed known-scale cross-correlation subtraction, no clipping) are explicit known-scale modes and benchmarks; E1 / E2 stay only as the existing search / gate statistic; E3 / E7 are not built | **author** (Q1, recommended); review |
| BD-2 | **Scale:** E4 estimates the amplitude from the image; a source-free calibration rate of the same head is kept as a cross-check (reported beside the fitted amplitude); the separate dose counter (D-37) is not a scale source — it measures source + ambient — and may serve only as a drift check under a declared transfer; the fitted amplitude is not reported as an ambient dose | **author** (Q2, recommended); review |
| BD-3 | **Claims:** every condition's signed error, vector excess, RMS and association are recorded; a pass verdict applies only in count regimes that independently meet association (one angular element) and trust, against the √2 × grid-step cell-equivalence target (lab 0.550, head 0.581, 1 m 3.750, 5 m 18.749 mm — a declared engineering convention, not a detector accuracy), judged by a seed-cluster simultaneous confidence bound on the **paired signed-vector** excess; target and regimes pinned before validation; failures recorded, never widened | **author** (Q3, recommended); review |
| BD-4 | **Evidence now = stage 1:** open window, bare bound, the four heads at their specified edge source, source levels {25, 50, 100, 250, 500, 1000} + the 1 MBq default, fields {0.05, 0.10, 0.20} µSv/h, t {10, 60} s, plus the ideal cells; cyclic (AB-12 reproduction) **and** non-cyclic (product decoding); E0 and the frozen E4 everywhere, E5 / E6 on the transition and saturation cells; 32 validation seeds × 300 acquisitions. The full matrix (front-only, 662 keV window, centre sources) is parked in the Backlog for a decision after stage 1. A one-seed timing pilot first; stop and report if the estimate exceeds 12 serial hours | **author** (Q4, recommended) |
| BD-5 | **Seeds:** development 330001 / 330007 / 330019 (used, never validation); selection `530001 + 104729·i`, i = 0…15; locked validation `730001 + 130363·i`, i = 0…31; reserve confirmation `970001 + 154858·i`, i = 0…31 — re-check disjointness and pin them in `seeds.json` before the manifest | review; planner |
| BD-6 | **Iterations (single-source localisation rule, new):** on selection seeds choose from {60, 120, 240, 400, 800} the count minimising the worst-condition paired signed-vector excess among association-valid levels, ties to fewer iterations, ideal cost recorded; frozen before validation; a value changed after seeing validation needs the confirmation seeds. E4 makes **no pair-resolution claim** (D-46 … D-48 stay for MLEM without a background term) | review; planner |
| BD-7 | **Calibration knowledge is explicit:** the estimator never reads the generating `Ambient` truth; the main runs use the independent transported map; sensitivities on the transition / saturation cells: measured background acquisitions of 600 s and 3600 s, scale offsets ±10 / 25 / 50 % (E5 / E6), and a front-only shape on bare truth — a wrong-bound calibration is refused by metadata in the product surface and appears only as a diagnostic | review; planner |
| BD-8 | **Trust:** the existing gate (Z, AB-11 thresholds) is unchanged and reported beside each E4 answer; no new threshold in this task — the relationship is recorded and its absence of a guarantee is a limitation | planner (review offered two paths; the cheaper one, recorded) |
| BD-9 | **Engine surface:** `Decoder.BackgroundCorrection`, default null → every existing path bit-identical (tested on retained fixtures); the reusable background-aware reconstruction lives in `Gcam.Decoding` (no dependency on Simulation); explicit calibration input (normalised map + metadata, finite non-negative, matching dimensions / window / geometry); the old `IDecoder.Decode(image)` stays; `CorrelationSearch.Search` and the frozen gate unchanged; studies keep `StudyDecoderGuard`; the new evidence recipe opts in. The CLI single run and Studio are **not** in this task (the single run needs a fixed-time entry point; Studio stays as BG-6) | review; planner |
| BD-10 | **Evidence path:** a new manifest family through the TODO-37 driver (`run_seeds.py`, staged execution copies, provenance) and an aggregator that reports signed vectors (not the difference of norms), RMS, association, trust, the fitted amplitude vs the calibration rate, and failures; the scratch development results are not backfilled into evidence | review |
| BD-11 | **Records (proposed by the implementer, applied by the planner):** Findings theme 67; EV-35 (corrected estimator, scoped); EV-01 / EV-12 / EV-34 keep their raw numbers and gain a pointer; LIM-10 (counting noise retained, scale / shape / calibration scope, no trust guarantee for E4); decisions D-52 … D-55 from BD-1 … BD-4 | review; planner |

**Correction (2026-10-07, after turn 2).** Turn 2 stopped correctly on a conflict the plan missed: BD-5's new seed
lists go into `samples/evidence/seeds.json`, whose whole-file hash is pinned by `calibration-records.json` (CAL records,
D-50). Decision (planner): the seeds.json change must be append-only (turn 2's JSON dump had re-escaped an existing em
dash), then the pin is updated and the calibration generator re-run; only the generator may change
`VV.Gcam.Calibration.md`. Turn 2's selection run ended with the turn (one seed done); it restarts in a fresh folder in
turn 3. Engine surface and 16 arithmetic tests were in place (full suite 799 → 815 passed, 28 skipped).

**Correction (2026-10-07, after turn 3).** Selection froze E4 at **400 iterations** (worst paired signed-vector
excess 3.31 mm, 5 m non-cyclic, against 6.64 / 8.47 / 6.38 / 5.97 mm for 60 / 120 / 240 / 800) and pinned 75 regimes
(`samples/evidence/background-shape/pinned-v1.json`) before any validation acquisition. The one-seed pilot extrapolated
stage 1 to 90.7 serial hours (+ 61.4 h for the sensitivities), above BD-4's 12 h limit, so the turn stopped. **Author
decision:** validation repeats 300 → **100** per condition per seed (32 × 100 = 3,200 acquisitions per condition; the
seed-cluster spread dominates the bias bound), run with 16 parallel workers; the sensitivities stay in. The long runs are
started by the planner as background commands, so no implementer turn is open (and spending tokens) while they run; the
implementer prepares the families and commands and aggregates afterwards. The seeds.json change was made append-only
and the calibration generator updated the four seed-hash cells in `VV.Gcam.Calibration.md` (no other change).

## Steps

1. Review turn (no code): check every row above in the code; measure E0…E5 on a small development seed set for the
   worst AB-12 conditions (bare, open window, lab / hand-held edge, 1 m, 5 m) including the ideal cost and BG-2 /
   BG-3 sensitivities; propose estimators, the scale and shape rules, the config form, the full measurement and its
   cost in run time → `docs/PLAN.Physics.BackgroundShapeDecoding.Review.md`.
2. Decisions after review (author for BG-2, BG-3's requirement, BG-6); implement in the same conversation; full
   measurement on validation seeds; headless verification.
3. Planner: independent rebuild / tests, one seed per new evidence family re-run, records, commit on the author's word.

## Done when

- The chosen estimator(s) remove the AB-12 pull in the bare bound to within a derived tolerance of the ideal pull, on
  validation seeds disjoint from the ones used to choose them, with the ideal-environment cost measured and stated.
- The scale and shape requirements (what the instrument must know, how well) are measured and recorded.
- Default decoding paths are bit-identical; full test suite passes.

## Not here

- Realistic head housing (Backlog, formerly TODO-32) and high-energy transport (Backlog, formerly TODO-31): the bounds
  stay bounds.
- The Cs-under-Co stripping path (TODO-35): its background is the Co continuum, measured there.
- Studio integration, unless the author asks for it after the measurement (BG-6).
