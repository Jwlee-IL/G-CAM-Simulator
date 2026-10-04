# PLAN.Physics.RngBias — the seeded DefaultRandom bias: exposure survey and replacement (TODO-26)

Scope: `DefaultRandom(seed)` wraps the legacy seeded `System.Random` (subtractive generator), which is biased by
1.6–2.1 % when a stream's draw count varies between trials (Findings 60, reproduced by the planner). Find which code
paths and which published numbers are exposed, measure how much each moves with a sound generator, then replace the
algorithm and re-pin the evidence. Procedure: [AGENTS.Planning](../AGENTS.Planning.md).

Status: **done** 2026-10-02 (substitute Claude subagent, worktree `C:\gw\w26`); Findings 60. Planner re-verified: build 0 errors,
tests 298 / 179 / 83 (+7) / 13 (+14); the single-seed mask-fabrication re-pin was sent back and replaced by a 24-seed
paired t-test.

## What is known

| Item | Fact |
|---|---|
| Probe | per trial: two draws, isotropic direction on an 18 mm square at 100 mm, rejection-sampled partner angle on the same stream; 10⁸ trials × 3 seeds: legacy −1.58 / −2.10 / −1.66 % (z −8…−11), xoshiro256** −0.07 / +0.13 / −0.04 % |
| Trigger | a data-dependent number of draws between trials (rejection sampling); a fixed-draw sampler was clean in the TODO-14 review |
| Exposure | `DefaultRandom` is constructed in 35 places across 21 source files; transport has rejection loops (Klein–Nishina in `ComptonModel`); `ListModeSource` already splits transport / rejection / time into separate streams |

## Survey and measurement (the first turn — no repository change kept)

| # | Item |
|---|---|
| S-1 | List every `IRandom` stream: where it is created, what draws from it, and whether its per-trial draw count is data-dependent (rejection loops, early exits, branch-dependent draws). Classify each: **exposed** / **fixed-draw** / **unclear**. |
| S-2 | For the exposed streams, a direct bias test of the quantity they feed (as the probe does for a solid angle): legacy vs xoshiro256** at a stated precision. |
| S-3 | **Published numbers:** for the VV.Gcam.Evidence results (EV-xx), the README headline results and the Studio evidence tests, re-run with a xoshiro256** `DefaultRandom` (a temporary change in the worktree only) and report old vs new with each one's own statistical spread — which moved beyond noise, which did not. Prioritise by exposure; say what was not re-run. |
| S-4 | Tests: which currently pinned golden values / seeded assertions would change, and whether each is a physics tolerance (keep, re-measure) or a regression pin (re-pin with a reason). |
| S-5 | Replacement design: xoshiro256** (or another well-tested generator) behind `IRandom`, seeding via SplitMix64, same public API; how to keep RTL / C# vector tests reproducible; whether the unseeded path changes. |

## Steps

1. Survey + measurement in a worktree (S-1 … S-5); write `docs/archive/PLAN.Physics.RngBias.Review.md`.
2. Decisions after review (author: whether to replace, and how to present moved numbers).
3. Replace, re-measure, re-pin; Findings / Evidence model-history rows for every moved number.

**Rule:** a number that moves is reported as moved, with old and new values — never kept by widening a tolerance.

## Decisions after review (2026-10-02)

Review: [PLAN.Physics.RngBias.Review](PLAN.Physics.RngBias.Review.md) (substitute implementer). Result: **no published
number moves** with xoshiro256** (43 MC reproduce commands × 5 seeds, 828 numbers, 12 flags ≈ chance; 20-seed re-runs
cleared all but the shield study's knee RMS, 0.79 → 1.02 mm, not quoted in Evidence). Transport is clean at
0.01–0.1 %; the planner's probe bias is pattern-specific. The **real defect** is that legacy `Random(seed)` is affine in
its seed: `MeasurementStage` reseeds per event (`seed + index·104729`), so consecutive events' energy smears are
correlated (−0.81; planner check: raw draws +0.555 / −0.477) and Studio's window counts fluctuate far less than
physically. The author chose to replace the generator.

| # | Decision | Basis |
|---|---|---|
| G-1 | `DefaultRandom` = xoshiro256** seeded by SplitMix64 from the existing seed, 53-bit doubles, `IRandom` unchanged; the unseeded path seeds from `Random.Shared` and exposes the chosen seed | review S-5 |
| G-2 | `MeasurementStage` keyed by a 64-bit (seed, index) hash — no per-event reseeding of an affine generator | review S-2 |
| G-3 | Studies' Poisson-realisation streams get their own seed offset (no reuse of the mean-map stream) | review S-1 |
| G-4 | The three single-seed regression pins are re-pinned from their **measured** multi-seed distributions (stated); the histogram-index test defect is fixed; `RandomQualityTests` added (cross-seed independence, window-count variance vs binomial, Klein–Nishina vs quadrature) | review S-4 |
| G-5 | Direct `System.Random` users with fixed manufactured patterns and the frozen `rtl/event_stream.txt` / C# vector stimuli stay as they are | review S-5 |
| G-6 | Re-measure the Studio evidence tolerances and the shield knee; regenerate renders / `samples/shield.png` where they change; Findings and Evidence model-history rows for what moved | rule |
| G-7 | Published values that already differ from today's seed-12345 output independent of the generator (EV-01 ghost, EV-32 floor, EV-25 lightest shield, theme 44 slope) → **TODO-27**, re-measured over seeds after this change | author |
