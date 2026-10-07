# PLAN.Physics.AngularResolution.Turn3 — significance floor on the resolved-pair test

Scope: TODO-34 turn 3, after [Turn 2](PLAN.Physics.AngularResolution.Turn2.md). Turn 2 found that the DR-2 test is
shape-only, so a strong single source's artefact near the weak source's position qualifies as a "second peak". Every
1 : 4 case then failed the ≤ 5 % false-split rule, although its pass rate was ≥ 97.5 %. The author chose to strengthen the
criterion now. This turn adds a significance floor that an instrument can apply without truth. It keeps the DR-2 shape
test unchanged and reports both. No VV, AGENTS, plan, review or Todo file was edited; no git state was changed.

Status: implemented and measured, 2026-10-06. Summary: `samples/evidence/results/angres-v2-floor.json`.

## At a glance

- **Design: (A), a calibrated floor on the second peak's absolute prominence** (P2 − max(saddle, baseline), in the
  image's own units).
  - The floor is selected per decoder × observed total counts × field region on single-source nulls of selection seeds,
    disjoint from the validation seeds.
  - Selection rule: the smallest floor at which the assigned false split is ≤ 3 % at every separation.
  - At use, it is one number per (decoder, total counts, field region). No truth is needed.
  - Option (B), a likelihood-ratio test of two sources against one, was not needed. Its cost is under "Design".
- **1 : 4 is resolved under the floor.** On axis, the pixel-area MLEM resolves at 1.5 el (1.56°) for 1000, 4000 and
  16 000 counts. Its false split stays ≤ 2.9 % at every separation (shape-only: 7–15 %, not resolved). Near the edge it
  is unchanged: 1.75 / 1.5 / 1.5 el. Cross-correlation and the engine MLEM still do not resolve 1 : 4: the floor cuts
  their false splits, but their pass rates stay ≤ 66 %.
- **No cost on 1 : 1.** Every 1 : 1 resolved separation at 1 m and 5 m is identical under shape-only and shape + floor:
  - pixel-area MLEM 1.25 el on axis and 1.5 el at the edge;
  - engine MLEM 1.5 / 1.5 / 1.5 el on axis and 1.75 / 1.5 / 1.5 el at the edge (1000 / 4000 / 16 000 counts);
  - at 5 m, pixel-area 1.75 el.
  The selected floor is 0 for most 1 : 1 cells, because their nulls already sit under 3 %.
- **The stricter hypothesis-free floor costs resolution** (sensitivity, not adopted). It caps a second peak anywhere,
  not only at the hypothesised position. Engine MLEM 1 : 1 at 1000 counts moves from 1.5 to 2.5 el, and pixel-area edge
  1 : 4 at 1000 counts is no longer resolved.
- **Re-run check:** the turn-3 runs reproduce turn 2's shape-only counts exactly — 52 992 decoder × condition cells over
  the 128 + 128 validation seeds, 0 mismatches — and the recorded per-acquisition statistics re-derive those counts.
- **Tests:** Gcam.Tests 444 → 448, full suite green. The default `MlemDecoder` is untouched this turn.
- **Run time:** 320 runs, ~1 h 25 min wall.

## Design

**Why a floor is needed.** In turn 2 the shape-only test's false split for 1 : 4 came from a strong single source (5000
counts at the pair's intensity centroid). Its reconstruction carries a secondary maximum near the weak source's
hypothesised position. That maximum has relative prominence ≥ 0.25 against its own surroundings, but it is a small
structure in absolute terms. A real weak source of 1000 counts gives a second peak with a larger absolute prominence. An
absolute-prominence floor separates the two. It needs no truth: the instrument knows its decoder, its total counts and
where the second peak lies.

**The test.** "DR-2 + floor": the DR-2 shape test as before (P1 = the global maximum; P2 = the highest other peak with
relative prominence ≥ v = 0.25; each within min(Δ/2, 1 el) of a different true source), and also prominence(P2) ≥ F.
The shape result is unchanged, and the floor can only remove passes.

**Selection** (per decoder × total counts × placement, on 32 selection seeds, F256[193:225], never used by an angres
family before):

- **Primary rule (adopted).** F is the smallest value, among 0 and the values just above each observed null statistic,
  at which the assigned single-source false split is ≤ α_sel at every separation of the grid. Inclusive: a statistic
  equal to F passes.
- **Choice of α_sel = 3 %.** Each selection rate rests on 32 × 50 = 1600 nulls. Its binomial SE at 5 % is
  √(0.05 · 0.95 / 1600) = 0.55 %. Selecting at 5 % − ~2 SE keeps the validation rate under DR-2's 5 %.
- **Sensitivity rule (reported, not adopted).** The hypothesis-free floor: the smallest F at which the second peak,
  wherever it lies, exceeds F in ≤ α_sel of the nulls. It bounds any false second peak, not only one near a
  hypothesised pair, and it is stricter.

**Why calibration may use truth.** The floor is fixed by known nulls, as PR-SENS-02's thresholds were. At use it is one
number per decoder, total-count level and field region, and no truth enters. The field region (on axis or near the edge)
is known to the instrument from where P2 lies. The total count is the acquisition's own count.

**Why not (B), a two-source / one-source likelihood-ratio test.** It fits one source and the best pair under the forward
model.

- Cost: a one-source fit is a scan over 2401 grid points. A two-source fit is a non-convex search over ~2.9 × 10⁶ grid
  pairs (or an iterative fit with restarts), each evaluated against 256 pixels. That is ~10⁸–10⁹ operations per
  acquisition, against ~0.5 ms for the prominence test, and the same null calibration is needed.
- It also has to choose a forward model, which is the very mismatch this study measures.

(A) does the job (the results below), so (B) was not built.

## Implementation

| File | Change |
|---|---|
| `src/Gcam.Simulation/ResolvedPair.cs` | New overloads `SecondPeaks(…, prominence)` and `Test(…, resolved, prominence)` return P2's absolute prominence. The existing overloads call them and give the same verdicts. New `PassesFloor` and `SelectFloor` (the selection rule; reference implementation) |
| `src/Gcam.Simulation/AngularResolutionStudy.cs` | With `RecordStatistics`, each condition stores per acquisition, decoder and v the signed statistic: +prominence when the shape test passed, −prominence otherwise. Stat > 0 ⇔ shape-resolved. Draws, streams and counts are unchanged |
| `src/Gcam.Simulation/AmbientEvidenceRequest.cs` | `AngularResolutionSpec.RecordStatistics` |
| `samples/evidence/angres/angres-request-v2-floor-{1m,5m}.json` | v1-1m / v1-5m with the point section off, v = 0.25 only, statistics recorded |
| `samples/evidence/manifest-angres-v2.json` | New manifest. Families: selection `angres_floor_select_{1m,5m}` (F256[193:225], 32 seeds); validation `angres_floor_{1m,5m}` (F256[33:161], 128 seeds — the turn-2 seeds, so shape-only counts must reproduce) |
| `samples/evidence/angres/aggregate_angres.py` | `--floor SELECT VALIDATE …` (selection, application, bootstrap intervals, resolved rows for Shape, Floor and FloorHypothesisFree); `--selftest`. The turn-2 modes are unchanged; a re-aggregated `angres-v1-5m.json` is byte-identical |
| `tests/Gcam.Tests/ResolvedPairTests.cs` | +4 cases (below) |
| `samples/evidence/results/angres-v2-floor.json` | Floors, resolved rows (all three criteria), per-condition pass and null with seed-bootstrap intervals (393 kB) |

Tests, with derived tolerances:

| Test | What it checks |
|---|---|
| `SecondPeak_AbsoluteProminence_IsTheAnalyticValue` (×3) | Equal tents of half-base b give absolute prominence Δ/b − 1 for b ≤ Δ ≤ 2b. Non-overlapping tents give the weak tent's height. Exact to 1e-12 (rational entries). The shape-only overload gives the same verdict. `PassesFloor` holds at F = the prominence and fails at the next double up |
| `SelectFloor_IsTheSmallestFloorWithExceedanceAtMostAlpha` | Brute force over random statistics with ties and zeros, α ∈ {0, 1, 5, 20} %. The selected F lets ≤ α through, and every observed smaller value lets more than α through. Exact |

`aggregate_angres.py --selftest` runs the same brute-force check on the Python rule that is actually used.

Full suite, `DOTNET_CLI_UI_LANGUAGE=en dotnet test Gcam.sln -c Release`, passed / skipped / total:

| Assembly | Result |
|---|---|
| Gcam.Tests | 448 / 0 / 448 (turn 2: 444) |
| Gcam.Studio.Tests | 205 / 0 / 205 |
| Gcam.Studio.Services.Tests | 92 / 7 / 99 |
| Gcam.Studio.UiTests | 13 / 14 / 27 (as before) |

## Selected floors (1 m; primary rule; image units)

The floors scale with counts, as expected for an absolute prominence. Units: cross-correlation in correlation counts; MLEM
in λ units (the reconstruction's own scale). Each selection rate rests on 32 seeds × 50 nulls = 1600 per separation.

| Total counts (ratio, weaker counts) | CC axis / edge | Engine MLEM axis / edge | Pixel-area MLEM axis / edge |
|---|---|---|---|
| 500 (1:1, 250) | 38 / 62 | 0.0015 / 0.0023 | 0.0099 / 0.0148 |
| 1250 (1:4, 250) | 170 / 199 | 0.0085 / 0.0048 | 0.0189 / 0.0141 |
| 2000 (1:1, 1000) | 100 / 196 | 0 / 0 | 0 / 0 |
| 5000 (1:4, 1000) | 538 / 666 | 0.0245 / 0.0122 | 0.0224 / 0.0075 |
| 8000 (1:1, 4000) | 0 / 680 | 0 / 0.0162 | 0 / 0 |
| 20 000 (1:4, 4000) | 1984 / 2430 | 0.0898 / 0.0398 | 0.0454 / 0 |
| 32 000 (1:1, 16 000) | 0 / 2438 | 0 / 0.0687 | 0 / 0 |
| 80 000 (1:4, 16 000) | 7720 / 9555 | 0.333 / 0.158 | 0.152 / 0 |

5 m (1:1 only): every floor is 0. The shape-only nulls there were already ≤ 3 %.

## Resolved separations — shape-only against shape + floor (1 m, v = 0.25)

128 validation seeds × 50 pair + 50 null acquisitions per cell (6400 each). Resolved separation in elements (1 el =
1.042°). "—" means not reached by 3.0 el, with the best pass in brackets. The worst false split over the grid is after
the semicolon.

**1 : 4 ratio**

| Placement | Counts | Decoder | Shape-only | Shape + floor |
|---|---|---|---|---|
| axis | 250 | pixel-area | — (78 %); 7.4 % | — (77 %); 3.8 % |
| axis | 250 | engine | — (57 %); 8.2 % | — (32 %); 3.1 % |
| axis | 250 | CC | — (50 %); 20.5 % | — (22 %); 2.9 % |
| axis | 1000 | pixel-area | — (99.7 %); 8.3 % | **1.5; 2.9 %** |
| axis | 1000 | engine | — (79 %); 9.1 % | — (54 %); 3.3 % |
| axis | 1000 | CC | — (65 %); 19.7 % | — (39 %); 3.6 % |
| axis | 4000 | pixel-area | — (100 %); 11.3 % | **1.5; 2.7 %** |
| axis | 4000 | engine | — (86 %); 8.7 % | — (63 %); 2.7 % |
| axis | 4000 | CC | — (69 %); 18.5 % | — (46 %); 2.7 % |
| axis | 16 000 | pixel-area | — (100 %); 11.2 % | **1.5; 1.5 %** |
| axis | 16 000 | engine | — (89 %); 7.5 % | — (66 %); 3.5 % |
| axis | 16 000 | CC | — (70 %); 17.7 % | — (49 %); 1.7 % |
| edge | 250 | pixel-area | — (70 %) | — (69 %) |
| edge | 1000 | pixel-area | 1.75; 3.6 % | 1.75; 3.3 % |
| edge | 4000 | pixel-area | 1.5; 2.3 % | 1.5; 2.3 % |
| edge | 16 000 | pixel-area | 1.5; 1.5 % | 1.5; 1.5 % |

Near the edge, the engine MLEM (best 44–75 %) and CC (best 42–47 % shape-only, 10–17 % under the floor) are not
resolved under either criterion.

Key 1 : 4 cells (pixel-area, on axis, 1000 counts), with seed-bootstrap 95 % intervals:

| Δ | Criterion | Pass | False split |
|---|---|---|---|
| 1.5 el | shape-only | 0.975 [0.970, 0.980] | 0.006 [0.004, 0.008] |
| 1.5 el | shape + floor | 0.969 [0.963, 0.974] | 0.0014 [0.0006, 0.0023] |
| 2.5 el | shape-only | 0.997 | 0.075 [0.063, 0.087] |
| 2.5 el | shape + floor | 0.997 | 0.029 [0.023, 0.036] |
| 3.0 el | shape-only | 0.997 | 0.083 [0.067, 0.099] |
| 3.0 el | shape + floor | 0.997 | 0.028 [0.021, 0.036] |

At 16 000 counts and 3.0 el the false split falls from 0.112 [0.078, 0.147] to 0.013 [0.005, 0.025].

**1 : 1 ratio** (both criteria give the same resolved separation in every cell)

| Placement | Decoder | 250 | 1000 | 4000 | 16 000 |
|---|---|---|---|---|---|
| axis | pixel-area | — | 1.25 | 1.25 | 1.25 |
| axis | engine | — | 1.5 | 1.5 | 1.5 |
| axis | CC | — | — | — | — |
| edge | pixel-area | — | 1.5 | 1.5 | 1.5 |
| edge | engine | — | 1.75 | 1.5 | 1.5 |
| edge | CC | — | — | — | — |

What the floor changes for 1 : 1:

- **Pixel-area, on axis, 1000 counts, Δ 1.25:** pass 0.984 [0.980, 0.988] and false split 0.0005 under both criteria
  (floor 0).
- **CC:** its worst false split drops (axis 1000 counts: 6.0 % → 3.3 %; edge: 8.8 % → 2.1 %), but its passes stay
  < 95 %.
- **Pass cost where the floor is non-zero:** at most a few points, e.g. CC at 250 counts on axis, best pass 60.2 % →
  58.5 %. The cost is nil wherever the floor is 0.

**5 m, 1 : 1, on axis** (128 seeds): pixel-area 1.75 el (1000 and 4000 counts); engine — at 1000 counts and 3.0 el at
4000; CC —. Identical under both criteria (floors 0).

**Hypothesis-free floor (sensitivity).** It caps a false second peak anywhere: assigned false splits ≤ 1.4 % in validation. It costs:

- engine 1 : 1: 1.5 → 2.5 el at 1000 counts (axis and edge), and 1.5 → 1.75 at 4000;
- pixel-area 1 : 4: 1.5 → 1.75 at 1000 counts on axis, and edge 1000 counts no longer resolved (best 96.2 %, but not
  ≥ 95 % at every larger Δ);
- 5 m pixel-area at 1000 counts: 1.75 → 2.0.

It is reported, not adopted: it controls a broader error (any spurious peak) than DR-2's false split.

## Deviations

1. **Floor key includes the field region** (on axis or near the edge) as well as decoder and total counts. Floors
   differ between the two (e.g. CC 1:1 at 8000 total: 0 against 680). The instrument knows the region from P2's
   position. A single floor per (decoder, total counts) would be the maximum of the two.
2. **α_sel = 3 %, not 5 %**, so the validation rate stays under DR-2's 5 %. Derived above from the selection sample
   size. Every validated false split under the adopted floor is ≤ 3.8 %.
3. **Validation reuses the turn-2 seeds** (F256[33:161]). The draws are identical, so the shape-only columns reproduce
   turn 2 exactly. Only the selection seeds are new.
4. **Only v = 0.25 was recorded** (the claimed valley). The v = 0.5 sensitivity stays from turn 2. The point section,
   ambient, ±7° and matched families were not re-run: the floor was not asked of them.

## What could not be run

- No floor for the matched upper bound, the ±7° grid or ambient (not requested; their turn-2 shape-only results stand).
- No 5 m 1 : 4 or 5 m edge (not in the turn-2 5 m design either).

## Commands that wrote anything

- `dotnet build Gcam.sln -c Release`; `dotnet build src/Gcam.Simulation -c Release`.
- `dotnet test tests/Gcam.Tests -c Release --filter "FullyQualifiedName~ResolvedPairTests"` (development).
- `DOTNET_CLI_UI_LANGUAGE=en dotnet test Gcam.sln -c Release` (full; output in `%TEMP%\gcam-todo34\test-turn3.txt`).
- Smoke run: `python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-angres-v2.json --out %TEMP%\gcam-todo34\smoke-floor --family angres_floor_5m --n 1 --jobs 1`.
- `python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-angres-v2.json --out %TEMP%\gcam-todo34\runs-v2 --jobs 22`
  (one background command; it exited when the 320 runs ended).
- `python samples/evidence/angres/aggregate_angres.py --selftest`, and
  `--floor angres_floor_select_1m angres_floor_1m angres_floor_select_5m angres_floor_5m --out samples/evidence/results/angres-v2-floor.json`.
  A turn-2 re-aggregation went to `%TEMP%\gcam-todo34\check-5m.json` (byte-identical to the committed file).
- Python scripts in `%TEMP%\gcam-todo34\aux\` that wrote the two v2 requests and `manifest-angres-v2.json`, plus
  read-only checks.
- Repository files created or edited: those in the Implementation table, and this report.
