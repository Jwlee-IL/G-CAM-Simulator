# PLAN.Physics.DepthBias — cause of the depth-from-focus bias at Studio geometry (TODO-23)

Scope: an investigation, not a feature — find why GCAM Studio's focus sweep puts the sharpest plane tens of mm away
from the true source distance, reproducibly and depending on the source's lateral position (Findings 56), and whether
an unbiased depth estimator exists at this geometry. Procedure: [AGENTS.Planning](AGENTS.Planning.md).

Status: **closed** 2026-10-02 — cause established by the review ([PLAN.Physics.DepthBias.Review](PLAN.Physics.DepthBias.Review.md),
Findings 57); no production change (author accepted). H-1 … H-3 are partial contributors, H-4 a modifier, H-5 confirmed;
the plan's premises on grid phase and theme 24's refine were wrong. Follow-up research: TODO-25.

## What is measured (Findings 56)

Studio defaults (rank 13, 0.7 mm cell, D 80 mm, 30 × 0.6 mm, GAGG chain), Cs-137 500 µCi, 5 seeds, 10 / 60 s, focus
sweep 150–950 mm in 10 mm steps: sharpest plane **360** (all events) / 310 (662 window) for a source at 300 mm on axis,
**280** at 30 mrad; **580** on axis and **460** at 30 mrad for 500 mm; 680 / 560 for 700 mm. Seed spread ≤ 20 mm at
60 s, so the offsets are systematic. Theme 24 already called the prominence metric a heuristic, biased +7 mm near and
−18 mm far at the reference lab geometry.

## What exists (checked in the code, 2026-10-02)

| Item | Where | Fact |
|---|---|---|
| Studio metric | `FocusSweepService` | per plane: `ImagingProjection.AtFocus` → decode → peaks from `MixedFieldStudy.TopPeaks`; score = (peak **grid-node value** − grid mean) / grid std; the peak's sub-cell offset (`PeakInterpolation.Estimate`) is applied to the position only, **not to the value** |
| Grid per plane | `ImagingProjection.AtFocus` | half extent 0.95·rank·r/2 and step max(0.2, r/4) with r = cell·z/D: **the grid scales with the plane**, so the source's position between grid nodes changes from plane to plane and differs on and off axis |
| Engine depth methods | `DepthStudy` (themes 18, 19, 24) | single-point calibrated focus at a known lateral position; joint 3D search; the alternating refine with a seed — "a seeded calibrated refine reaches ~1 % of the distance" at the reference lab geometry |
| Sampling | Findings 55 | 1.27 detector pixels per shadow cell at 1 m (aliasing; position-dependent localisation bias, period ≈ 3.4 mm at 0.6 mm pitch) |

## Hypotheses (reference — the implementer may add or drop any)

| # | Hypothesis | Test that separates it |
|---|---|---|
| H-1 | **Peak-value sampling**: the score uses a grid-node value; with the grid scaling with z, the node-to-source offset varies with z, so the score curve is modulated and its maximum moves — lateral-dependent by construction | score with the **interpolated peak value** (or a much finer grid, or a grid fixed in angle) and see whether the bias collapses |
| H-2 | **Normalisation**: grid mean / std include the peak and its sidelobes, and the grid area changes with z (theme 24's explanation) | score by a sidelobe-excluded std, or a fixed-angle grid |
| H-3 | **Shadow undersampling** (theme 55): at 1.27 samples per cell the decoded peak shape depends on the shadow's phase on the pixels | finer detector pitch at constant detector size (0.3 / 0.2 mm) — the bias should shrink if H-3 |
| H-4 | **Physics content**: Compton / scatter / non-cyclic edges shift the curve | ideal geometric detector, single line, mean (noise-free) flood |
| H-5 | **The metric itself**: peak height is not the right focus measure (it rewards a narrower grid footprint) | compare against a calibrated template / likelihood focus (theme 18's single-point method, theme 24's refine) |

## Steps

1. **Review + measurement (no repository code):** a throwaway program outside the tree (or `%TEMP%\gcam-*`) runs the
   separating tests above on noise-free mean floods first (bias without noise), then on the list-mode path at 60 s;
   sources at 300 / 500 / 700 mm, on axis and at 15 / 30 mrad, plus a lateral sweep at one distance to see whether the
   bias is periodic with position (H-1 / H-3 signature). Report each hypothesis confirmed / rejected with numbers.
   Write `docs/PLAN.Physics.DepthBias.Review.md`.
2. Planner writes "Decisions after review": the cause; whether Studio's sweep should change metric (an unbiased one,
   measured), and the precision it then has — **the offset is never subtracted as a calibration constant**.
3. If a metric changes: implement in the engine / Studio with tests whose tolerances come from measured spreads;
   re-measure Findings 56's table; update Findings, EV-33 and the Studio focus-sweep note.

**Not here:** new depth features; desktop tests.
