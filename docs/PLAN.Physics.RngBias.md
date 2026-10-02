# PLAN.Physics.RngBias — the seeded DefaultRandom bias: exposure survey and replacement (TODO-26)

Scope: `DefaultRandom(seed)` wraps the legacy seeded `System.Random` (subtractive generator), which is biased by
1.6–2.1 % when a stream's draw count varies between trials (Findings 60, reproduced by the planner). Find which code
paths and which published numbers are exposed, measure how much each moves with a sound generator, then replace the
algorithm and re-pin the evidence. Procedure: [AGENTS.Planning](AGENTS.Planning.md).

Status: **reference plan** 2026-10-02 — survey first (author), substitute Claude subagent (Codex out of credits).

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

1. Survey + measurement in a worktree (S-1 … S-5); write `docs/PLAN.Physics.RngBias.Review.md`.
2. Decisions after review (author: whether to replace, and how to present moved numbers).
3. Replace, re-measure, re-pin; Findings / Evidence model-history rows for every moved number.

**Rule:** a number that moves is reported as moved, with old and new values — never kept by widening a tolerance.
