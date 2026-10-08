# PLAN.Physics.DepthLikelihood — joint x / y / z forward-likelihood depth estimator (TODO-25, research)

Scope: Findings 57 showed the focus sweep's "sharpest plane" is a heuristic on a forward-mismatched decode; a forward
likelihood with a **known** bearing reduced the bias to a few mm. This research task asks whether a likelihood that also
estimates the bearing from the data gives an unbiased, honestly-intervalled depth at Studio's geometry, before any Studio
change. Procedure: [AGENTS.Planning](AGENTS.Planning.md); prior review:
[PLAN.Physics.DepthBias.Review](archive/PLAN.Physics.DepthBias.Review.md).

Status: **continuation, 2026-10-08** — review 2026-10-02 by a substitute Claude subagent ([review](PLAN.Physics.DepthLikelihood.Review.md), Findings 62: gate not yet); continuation turn 1 with Codex (see "Continuation" below).

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

## Continuation (2026-10-08)

The review left three gate items open (Findings 62) and raw material in `samples/evidence/depthlik/` built against
`85ff1ec`, i.e. **before** the xoshiro256** generator replacement (TODO-26) — none of it may be quoted until re-run on
the current tree. Since then the evidence path gained per-seed provenance (TODO-37) and the rule that the planner, not
an implementer turn, starts long runs (AGENTS.Planning).

| ID | Item (verify / improve) |
|---|---|
| C-1 | **Historical 10 s v3 pass:** analyse `results/fit_main10v3.csv` (pre-xoshiro) for the wrong-maximum rate and coverage, as a historical observation that tells what to re-measure — not as evidence |
| C-2 | **Port the probe to the current tree** (`samples/evidence/depthlik/probe`): build, run one seed per condition, compare with the old numbers in distribution (the random streams changed, so values differ; the statistics should not); a one-seed timing pilot per condition |
| C-3 | **Gate criteria fixed before the re-run:** wrong-maximum rate (an upper confidence bound, D-50 style), bias against spread, coverage of 68 / 95 % intervals with their binomial bounds — numbers and seed counts proposed with their derivation |
| C-4 | **Count-scaled model budget:** a rule for model histories from the data's counts, chosen on selection seeds (300 mm / 60 s is the hard case, ≈ 46,000 counts) and validated on disjoint seeds |
| C-5 | **Model mismatch:** fit floods made with perturbed truth (mask–detector distance δD, mask pose, gain map / crystal response within the tolerances in the evidence register) with the nominal model; measure the depth shift and interval coverage, compare δD with the derived −6 mm at 1 m for +0.5 mm |
| C-6 | **Evidence path:** the re-runs through `run_seeds.py` (a manifest family with provenance), seeds disjoint from the 2026-10-02 lists; the implementer prepares families, pilots and exact commands and stops; the planner runs them |
| C-7 | **Decision afterwards:** gate verdict with the measured items; if met, a Studio replacement is planned as a separate task; if not, Findings 62 is updated and the sharpest plane stays |

**Decision (author, 2026-10-08, after continuation turn 1 — [continuation review](PLAN.Physics.DepthLikelihood.Continuation.Review.md)).**
The review found that the current approximate profiles give negative likelihood ratios, so interval validation needs
probe work (consistent profiles, interval endpoints, count-scaled budget) first, and that a gate-grade validation needs
≥ 2,048 seeds per condition (≈ 190 h on 24 cores at 109–460 s per fit). The author chose: **run the prepared screening
family only (planner), record the current-tree descriptive result in Findings 62, keep the sharpest plane in Studio,
park profile corrections and the gate validation in the Backlog with their cost, and close TODO-25.**

**Resumability for the parked gate validation (author, 2026-10-08).** A ≈ 190 h run must survive an interruption.
The seed driver already resumes: a (family, seed) whose `done.json` records exit 0 is skipped when the same command is
re-run, so an interruption loses only the runs in flight. Today one depth-likelihood run is one seed × all 16
conditions × both channels (≈ 1.5 h under load in the screening run), which is too coarse for a 190 h job. When the
validation is picked up: split the families **per condition** (depth × angle × live time, one channel each) so one run
is one seed × one condition (minutes), keep each run's output immutable, state the resume command next to the start
command, and run a deliberate interrupt-and-resume test on a small family before the long run.
