# PLAN.Physics.DepthLikelihood — joint x / y / z forward-likelihood depth estimator (TODO-25, research)

Scope: Findings 57 showed the focus sweep's "sharpest plane" is a heuristic on a forward-mismatched decode; a forward
likelihood with a **known** bearing reduced the bias to a few mm. This research task asks whether a likelihood that also
estimates the bearing from the data gives an unbiased, honestly-intervalled depth at Studio's geometry, before any Studio
change. Procedure: [AGENTS.Planning](AGENTS.Planning.md); prior review:
[PLAN.Physics.DepthBias.Review](PLAN.Physics.DepthBias.Review.md).

Status: **reference plan** 2026-10-02 — implementer: a substitute Claude subagent (Codex out of credits).

## Proposed protocol (reference — verify / improve)

| # | Item |
|---|---|
| L-1 | Forward model per (x, y, z): the expected flood from MC templates with pixel-area, slab, crystal and measurement response; intensity and a flat background as profiled nuisance parameters; interpolation between template nodes measured, not assumed |
| L-2 | Templates converged independently (budget doubling until the estimate stops moving beyond its seed spread); grid origins varied; truths **off** template nodes; held-out seeds for validation |
| L-3 | Estimator: maximise the Poisson likelihood jointly (coarse grid + local refine); report bias, spread, wrong-maximum rate and the coverage of a likelihood-ratio interval at 300 / 500 / 700 / 1000 mm, on axis and off axis, 10 s and 60 s |
| L-4 | Cost (templates, per-fit time) and whether it fits a Studio worker |
| L-5 | Decision gate: only if bias ≪ spread, the wrong-maximum rate is small and coverage is near nominal does a Studio replacement get planned; otherwise the result goes to Findings and the "sharpest plane" stays |

## Steps

1. Review + measurement under `%TEMP%\gcam-*` (no repository code), writing `docs/PLAN.Physics.DepthLikelihood.Review.md`.
2. Decisions after review (author). 3. Only then any implementation.
