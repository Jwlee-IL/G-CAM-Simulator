# PLAN.Physics.AngularResolution.Turn2 — implementer's turn-2 report: angres family, MLEM options, ensembles

Scope: TODO-34 turn 2 (implementation) of [PLAN.Physics.AngularResolution](PLAN.Physics.AngularResolution.md), decisions
DR-1 … DR-11, by the substitute implementer (a Claude subagent; Codex unavailable). I had no material disagreement with
the DR rows and implemented them as specified, with the deviations listed below. No VV, AGENTS, plan, review or Todo file
was edited, and no git state was changed.

Status: implemented and measured, 2026-10-06. Summaries: `samples/evidence/results/angres-v1-*.json` (8 files).

Units: one element = atan(cell / D) = 1.0416° (hand-held head; 18.2 mm at 1 m). "Counts" = expected cs662-window counts of
the weaker source. "Resolved at Δ" (DR-2) = pooled pass ≥ 95 %, pooled single-source false split ≤ 5 %, at Δ and at
every larger Δ of the grid. Pass rates are pooled k / N. The intervals in brackets are seed-bootstrap 95 % intervals.

## At a glance

- **Default MLEM is bit-identical.** It matches a verbatim copy of the pre-change algorithm on 6 geometries (test). The
  opt-in pixel-area model, the supplied-column ("matched") model and the background term are new, separate paths.
- **The baseline the head actually delivers at 1 m is wider than 1.042°** (Q1, 128 source positions within one element,
  noiseless). Cross-correlation FWHM:
  - median 1.46° along x (range 1.08–1.88°);
  - median 1.22° along y (range 0.96–1.61°);
  - half-maximum diameter 1.29° (range 1.06–1.54°).

  The standard quadrature estimate atan(√(c² + d²) / D) = 1.47° matches the x median. At 5 m the median is 1.10°, but
  the upper quartile is 2.0–2.1°: the decoder samples only ~2 distinct columns per element at 5 m.
- **Resolved separation at 1 m, on axis, 1 : 1, fully coded field, v = 0.25** (128 seeds × 50 + 50 per cell):

  | Reconstruction | 250 counts | 1000 | 4000 | 16 000 |
  |---|---|---|---|---|
  | Cross-correlation | not reached (best 60 %) | not reached (best 84 %) | not reached (best 92.5 %) | not reached (95.3 % at 2.0, 86 % at 2.5, 65 % at 3.0) |
  | Engine MLEM (pixel-centre), 8 iterations | not reached (best 85 %) | **1.5 el** | 1.5 el | 1.5 el |
  | Pixel-area MLEM, 120 iterations (claimed, DR-6) | not reached (best 87 %) | **1.25 el (1.30°)** | 1.25 el | 1.25 el |
  | Transported "matched" MLEM, 120 iterations (upper bound, 32 seeds) | — | 1.25 el | — | 1.25 el |

  1.25 elements is 1.30°, or 23 mm at 1 m. At Δ = 1.25, 1000 counts, the pixel-area MLEM passes 98.4 % [98.0, 98.8] with
  a 0.05 % false split. Nothing resolves 1.0 element at these counts.
- **Near the edge** (outer source 2.5 el + jitter): pixel-area 1.5 el, engine MLEM 1.5–1.75 el, cross-correlation not
  reached.
- **1 : 4 ratio:** the pixel-area MLEM passes ≥ 97.5 % from 1.5 el, but on axis its single-source false split reaches
  7–15 % at 2.5–3 el. By the DR-2 rule that is not resolved. Near the edge it resolves at 1.5–1.75 el. Cross-correlation
  and the engine MLEM never resolve 1 : 4.
- **5 m** (on axis, 1 : 1): pixel-area 1.75 el; engine MLEM not reached at 1000 counts and 3.0 el at 4000;
  cross-correlation not reached. 5 m is worse than 1 m. Cause: decoder sampling — the shadow cell is 1.011 pixel at
  5 m, so the pixels sample nearly the same phase and the phase hardly walks across the array.
- **±7° usable field** (reported, not claimed): pixel-area unchanged at 1.25 el; engine MLEM drops to ≤ 45 % pass;
  cross-correlation best 73 %.
- **Ambient 0.10 µSv/h, 60 s** (25.7 bare / 0.63 front-only background counts): no change. Every resolved separation is
  the same as ideal. Passes move by ≤ 1.4 points with or without b_i.
- **Q3 ladder:** EV-11's recipe reproduces itself (CC 3.5 mm, MLEM 1.5 mm, 64 / 64 seeds). Then, in lab elements:
  - the blind criterion moves the MLEM figure from 0.56 to 1.25 elements (CC 1.31 → 1.5);
  - transported "every-deposit" floods move both MLEMs to 2.5 elements, through false splits at the EV-11 count level;
  - non-cyclic decoding changes nothing at the lab;
  - the hand-held head at 155 mm and at 1 m resolves 1.25 elements with the pixel-area MLEM;
  - the engine MLEM at EV-11's 80 iterations false-splits single sources in ~46–51 % of acquisitions at the hand-held
    head.
- **DR-9:** the noiseless cross-correlation peak at 1 m is off by a median 0.18° and a maximum 0.41° (0.39 el), with a
  systematic −0.11° along y. Poisson precision with the tent sub-cell estimate: RMS 0.38° at 250 counts and 0.21° at
  1000. At the hand-held near field (S = 100 mm) the same pipeline gives RMS 0.51° at 250 counts (1.38 mm at the source
  plane), against PR-IMG-02's 0.25 mm floor (0.09°) and "sub-mm from 250 counts". Flagged for the planner.
- **Iteration choice (DR-5, selection seeds):** engine 8, pixel-area 120, matched 120. Every model splits single
  sources when over-iterated:
  - engine MLEM: false split 49 % at 80 iterations;
  - pixel-area: 9.5 % at 480 iterations;
  - matched: 18 % at 640 iterations.

  This corrects my turn-1 review: the pixel-area matrix delays the splitting, it does not stop it.
- **Run time:** ~5.5 h wall (the ~3 h guidance was exceeded; see Deviations).

## Files

| Group | File | What |
|---|---|---|
| Engine | `src/Gcam.Decoding/MlemDecoder.cs` | Opt-in options added:<br>• `MlemSystemModel(PixelSubSamples, ClosedCellTransmission)` — the analytic pixel-area matrix;<br>• `FromColumns` — a supplied matrix;<br>• `Decode(image, background)` and `Snapshots(y, w, h, iterations, background)` — b_i and iteration snapshots;<br>• `SystemMatrix` and `Grid`.<br>The default (no model, no columns, no b) runs the unchanged float matrix and scalar loop. The opt-in paths keep a double matrix and run a single-precision loop vectorised over pixels |
| Engine | `src/Gcam.Simulation/ResolvedPair.cs` | The DR-2 test:<br>• local maxima: 8-connected, plateau tie-break;<br>• topographic prominence: descending union-find;<br>• baseline: median by quickselect, or 0;<br>• P2 = the highest other peak with relative prominence ≥ v;<br>• assignment radius min(Δ/2, 1 el).<br>Also FWHM and half-maximum diameter |
| Engine | `src/Gcam.Simulation/AngularResolutionStudy.cs` | The `angres` family:<br>• transported maps at `SourceDetectorMm`;<br>• seed phase: ±0.5 el in x and y, axis x for even seeds and y for odd;<br>• pairs and single-source nulls per placement × Δ × counts × ratio × environment;<br>• cross-correlation (median baseline) and every MLEM variant at every snapshot;<br>• point section (Q1, DR-9);<br>• ambient with MLEM ± b_i;<br>• the Q3 ladder (`RunLadder`, with L0 = `MlemStudy` unchanged) |
| Engine | `src/Gcam.Simulation/AmbientEvidenceRequest.cs` | `AngularResolutionSpec`, `AngularMlemVariant`, `AngularLadderSpec` |
| CLI | `src/Gcam.Cli/Commands/AmbientCommands.cs` | One dispatch line: `"angres"` |
| Tests | `tests/Gcam.Tests/MlemOptionTests.cs` | 13 cases, all with derived tolerances (below) |
| Tests | `tests/Gcam.Tests/ResolvedPairTests.cs` | 11 cases |
| Tests | `tests/Gcam.Tests/AngularResolutionStudyTests.cs` | 4 cases (smoke runs of the family and the ladder; geometry; landed share) |
| Evidence | `samples/evidence/angres/angres-request-v1-{select,matched-select,1m,5m,wide,ambient,near,matched,ladder}.json`, `samples/evidence/manifest-angres-v1.json` | Recipes and the versioned manifest (new files only) |
| Evidence | `samples/evidence/angres/aggregate_angres.py` | Pooling, seed-bootstrap intervals (numpy), the DR-2 rule, DR-5 selection (`--select`), point and ladder summaries |
| Results | `samples/evidence/results/angres-v1-{iterations,1m,5m,wide,ambient,near,ladder,matched}.json` | Aggregated summaries (~610 kB in all; 1m is 250 kB with per-condition intervals) |

## Tests

Command: `DOTNET_CLI_UI_LANGUAGE=en dotnet test Gcam.sln -c Release` (full suite). Results are passed / skipped / total.

| Assembly | Before | After |
|---|---|---|
| Gcam.Tests | 413 / 0 / 413 | **444 / 0 / 444** (+31) |
| Gcam.Studio.Tests | 205 / 0 / 205 | 205 / 0 / 205 |
| Gcam.Studio.Services.Tests | 92 / 7 / 99 | 92 / 7 / 99 |
| Gcam.Studio.UiTests | 13 / 14 / 27 | 13 / 14 / 27 (UI scenarios skipped as before; none run) |

Tolerances, each derived in the test:

| Test | Tolerance and its derivation |
|---|---|
| Default MLEM unchanged | Exact bit equality against a verbatim copy of the pre-change `MlemDecoder` (git 5977c23): λ image, estimate and confidence, on lab cyclic / non-cyclic, hand-held at S = 100 mm, 1 m, 5 m and 0 iterations. Also checked with `new MlemDecoder(…, null)`, `Decode(image, null)` and a zero background. A scratch check against the old DLL source agreed on 12 005 grid values with no mismatch |
| Snapshots | Bit-equal to `Decode` at 0 / 5 / 20 / 40 iterations |
| Pixel-area model | With 1 sample and t = 0 it equals the binary matrix exactly. Each entry is (k + (s² − k)·t) / s², checked to s²(s² + 2)·u. With t = 1 every entry is exactly 1 |
| Vector loop | Agrees with the default loop within K·(nSrc + nDet + 2)·2⁻²⁴·max λ (recursive-summation bound, K = 8) |
| Supplied columns | Exactly reproduce the model they came from |
| b_i | With data A·1 + b, the flat start is a fixed point within K·(nSrc + nDet + 2)·u (double, or float for the vector loop). Without b, λ moves by more than 100× that |
| Far source | At 1 m, noiseless model data at a grid node with a unique column reconstruct at that node after 200 iterations (exact index), for both models |
| Beer–Lambert factor | exp(−0.178·μ_rel(661.7)·10) |
| Two-tent prominence | Relative prominence = Δ/w − 1 to 1e-12 |
| Angles | `AngleDeg` within (180/π)·4u / sin θ (acos conditioning) |
| Landed share | The self-consistent flood lands the exactly integrated open-area share of the back-projected patch, within 4 binomial SE |

## How the ensembles were run

`run_seeds.py --manifest samples/evidence/manifest-angres-v1.json --out %TEMP%\gcam-todo34\runs --jobs 22`, in three
invocations:

1. `angres_select`;
2. then `angres_matched_select`, `angres_1m`, `angres_5m`, `angres_wide`, `angres_ambient`, `angres_near` and
   `angres_ladder` together;
3. then `angres_matched` (after its iteration count was frozen).

All 720 runs finished with exit 0. Seeds (F256):

| Family | Seeds | Seeds used |
|---|---|---|
| select | F256[1:33] | 32 |
| matched_select | F256[1:17] | 16 |
| 1m, 5m, wide, near | F256[33:161] | 128 |
| ambient, ladder | F256[33:97] | 64 |
| matched | F256[161:193] | 32 |

The selection seeds are disjoint from every validation seed. The axis split was 58 x / 70 y on the 128-seed families.
Per-seed compute time under 22-way load, median: 1m 668 s, 5m 87 s, wide 644 s, ambient 421 s, near 4 s, ladder ~400 s,
matched 732 s (including the 2401-column transported matrix). Full re-run command: the three invocations above, with
`--family` listing the families.

## Q1 — single-source point response (DR-1)

Method: one 4 × 10⁶-photon transported map per seed, at the seed's jittered point (uniform ±0.5 el in x and y).
Noiseless cross-correlation reconstruction on the one-period grid (0.152° step). FWHM along x and y through the maximum,
with a median baseline. Half-maximum diameter = 2√(solid angle above half height / π).

Expectations:

- The geometric element is 1.042°.
- For the sampled decoder, the triangle (cell ⊗ cell) convolved with the pixel box gives ≈ 1.27 el ≈ 1.32°.
- The quadrature estimate atan(√(c² + d²) / D) gives 1.47°.

No tolerance applies: these are distributions.

Cross-correlation (median [Q1, Q3], range; degrees):

| Geometry (N seeds) | FWHM x | FWHM y | Half-max diameter | Peak per count |
|---|---|---|---|---|
| 1 m (128) | 1.459 [1.188, 1.701], 1.080–1.885 | 1.221 [1.115, 1.344], 0.964–1.610 | 1.294 [1.188, 1.361], 1.057–1.543 | 0.34, 0.30–0.41 |
| 5 m (128) | 1.108 [1.079, 2.053], 1.051–2.109 | 1.103 [1.060, 2.020], 1.042–2.105 | 1.635 [1.200, 1.697], 1.200–2.399 | 0.29, 0.18–0.50 |
| hand-held at S = 100 mm (128) | 1.153 [1.100, 1.316], 0.934–1.457 | 1.062 [0.922, 1.130], 0.606–1.198 | 1.084 [1.014, 1.175], 0.874–1.349 | 0.43, 0.37–0.47 |

MLEM, noiseless, at the frozen iteration counts (a non-linear estimator, so the width depends on iterations and counts;
labelled, not a resolution figure). Median [Q1, Q3], degrees:

| Geometry | Engine (8 iterations), x / y | Pixel-area (120 iterations), x / y |
|---|---|---|
| 1 m | 1.011 [0.956, 1.023] / 0.992 [0.913, 1.024] | 0.498 [0.442, 0.542] / 0.487 [0.462, 0.554] |
| 5 m | 1.104 / 1.056 | 0.614 / 0.533 (range 0.22–1.27) |
| S = 100 mm | 0.745 / 0.895 | 0.429 / 0.452 |

The matched family recorded no point section (see What could not be run).

## Q2 — resolved separation (DR-2 … DR-6)

**1 m, fully coded field** (one-period grid ±3.64°). 128 seeds × 50 pair + 50 null acquisitions = 6400 + 6400 per cell.
Resolved separation in elements. "—" means not reached by 3.0 el; the best pass is given where useful.

| Placement, ratio | Decoder | 250 | 1000 | 4000 | 16 000 | v = 0.5 (250 / 1000 / 4000 / 16 000) |
|---|---|---|---|---|---|---|
| axis 1:1 | CC | — (60 %) | — (84 %) | — (92.5 %) | — (95.3 % at 2.0, then 86 / 65 %) | — / — / — / — (best 97 % at 2.5, 90.5 % at 3.0) |
| axis 1:1 | engine@8 | — (85 %) | 1.5 | 1.5 | 1.5 | — / 2.0 / 1.75 / 1.75 |
| axis 1:1 | **area@120** | — (87 %) | **1.25** | **1.25** | **1.25** | — / 1.5 / 1.25 / 1.25 |
| axis 1:4 | CC | — | — | — | — | — |
| axis 1:4 | engine@8 | — | — | — | — | — |
| axis 1:4 | area@120 | — | — (pass ≥ 97.5 % from 1.5 el; null 4–8 % at 2–3 el) | — (null 9–11 % at 2.5–3) | — (null 10–11 %) | — |
| edge 1:1 | CC | — | — (70 %) | — (79 %) | — (82 %) | — |
| edge 1:1 | engine@8 | — | 1.75 | 1.5 | 1.5 | — / 2.5 / 2.0 / 2.0 |
| edge 1:1 | area@120 | — | 1.5 | 1.5 | 1.5 | — / 1.5 / 1.5 / 1.5 |
| edge 1:4 | CC | — | — | — | — | — |
| edge 1:4 | engine@8 | — | — | — | — | — |
| edge 1:4 | area@120 | — | 1.75 | 1.5 | 1.5 | — / 1.75 / 1.75 / 1.75 |

Selected cells, pass / single-source false split, with seed-bootstrap intervals:

| Condition | Decoder | Pass | False split |
|---|---|---|---|
| axis 1:1, 1000 counts, Δ 1.25 | area | 0.984 [0.980, 0.988] | 0.0005 |
| | engine | 0.579 | 0.0005 |
| | CC | 0.441 | 0.012 |
| axis 1:1, 1000 counts, Δ 1.5 | area | 0.999 | — |
| | engine | 0.975 [0.969, 0.979] | 0.006 |
| | CC | 0.629 [0.581, 0.678] | 0.023 |
| axis 1:1, 16 000 counts, Δ 1.0 | area | 0.411 [0.353, 0.468] | — |
| | engine | 0.000 | — |
| | CC | 0.076 | — |
| axis 1:1, 16 000 counts, CC | Δ 2.0 | 0.953 [0.928, 0.972] | 0.024 |
| | Δ 2.5 | 0.861 [0.810, 0.911] | — |

CC is non-monotone: it passes less at 2.5–3 el than at 2 el. That is the lumpy, shift-variant response the review
found, now with phase averaging. The **1 : 4 cases** fail on the null, not on the pass. A single source of 5000 counts at
the intensity centroid produces a qualifying "second peak" near the weak source's position in up to 11 % (pixel-area) or
24 % (CC) of acquisitions. The DR-2 test is shape-only (relative prominence, no significance floor), so a weak true source
and an artefact of a strong one are judged alike.

Other conditions (v = 0.25; "best" = the highest pass rate on the separation grid):

| Condition | Seeds | Pixel-area | Engine | CC |
|---|---|---|---|---|
| 5 m, on axis, 1:1, 1000 counts | 128 | 1.75 | — (95 % only at 3.0) | — (best 79 %) |
| 5 m, on axis, 1:1, 4000 counts | 128 | 1.75 | 3.0 | — (best 84 %) |
| ±7° grid, on axis, 1:1, 1000 counts | 128 | 1.25 | — (best 45 %, null 0) | — (best 73 % at 1.5) |
| Matched upper bound, 1:1, 1000 counts | 32 | **1.25** (matched; pass 0.972 at 1.25, 0.478 at 1.0) | — | — |
| Matched upper bound, 1:1, 16 000 counts | 32 | 1.25 (matched; pass 0.137 at 1.0) | — | — |
| Matched upper bound, 1:4, 1000 / 16 000 counts | 32 | — / 3.0 (matched, null-limited as for pixel-area) | — | — |

At 5 m the pixel-area MLEM needs 1.75 el against 1.25 at 1 m; the engine MLEM and CC are worse still. Expectation (from
Q1): at 5 m the decoder's columns repeat in ~0.5-el blocks (225 distinct of 2401), so 5 m should resolve no better than
1 m. Confirmed.

On the ±7° grid, the engine MLEM's second peak is usually an artefact in the partially coded field that outranks the
true second source. The pixel-area model is unaffected.

The matched (transported) upper bound gives the same 1.25 el as the analytic pixel-area matrix at its frozen 120
iterations. It reached 1.0 el in the turn-1 single-phase probe only at 320 iterations, which DR-5's rule does not
choose.

## Iteration choice and sensitivity (DR-5)

Selection condition: 1 m, on axis, 1 : 1, 1000 counts, Δ 0.75–3 el, 50 + 50 acquisitions per Δ, v = 0.25. Seeds: 32 for
the engine and pixel-area MLEMs, 16 for matched. The rule: the iteration count with the smallest resolved Δ; ties go to
fewer iterations. The table is also the sensitivity (resolved Δ; worst false split over the grid):

| Iterations | Engine | Iterations | Pixel-area | Iterations | Matched |
|---|---|---|---|---|---|
| 3 | 2.0; 0.4 % | 10 | 2.0; 0 % | 20 | 1.75; 0.3 % |
| 5 | 1.75; 1.0 % | 20 | 1.75; 0.1 % | 40 | 1.5; 2.0 % |
| **8** | **1.5; 1.3 %** | 40 | 1.5; 0.6 % | 80 | 1.5; 3.4 % |
| 10 | 1.5; 1.8 % | 80 | 1.5; 1.0 % | **120** | **1.25; 3.5 %** |
| 15 | 1.5; 2.5 % | **120** | **1.25; 1.3 %** | 160 | 1.25; 4.8 % |
| 20 | —; 3.7 % (pass ≤ 98 %) | 160 | 1.25; 1.9 % | 240 | 2.5; 5.8 % |
| 30 | —; 10.6 % | 240 | 1.25; 2.8 % | 320 | 2.5; 6.8 % |
| 40 | —; 24.6 % | 320 | 1.5; 4.7 % | 480 | —; 13.4 % |
| 80 | —; 48.5 % | 480 | 3.0; 9.5 % | 640 | —; 18.1 % |

Cross-correlation at the same condition: not reached (best 82.6 % at 2.0 el, v = 0.25; 91.4 % at 2.5 el, v = 0.5). The
frozen counts were then confirmed on the validation seeds: engine 1.5 and pixel-area 1.25 at 1000 counts (the Q2 table).

## Q3 — attribution ladder (DR-8)

64 seeds. Lab element atan(1/60) = 0.955°. One change per step. Counts per source: EV-11's own level (half the landed
photons of 1.5 × 10⁶ aimed: 3.70 × 10⁵, median over seeds) and 1000. Engine MLEM at EV-11's 80 iterations; pixel-area at
120. Separation grid 0.5–2.5 lab elements. Resolved separation in elements of the step's geometry:

| Step | Change | Shadow cell / pixel | CC (EV-11 level / 1000) | Engine@80 (EV-11 / 1000) | Pixel-area@120 (EV-11 / 1000) |
|---|---|---|---|---|---|
| L0 | EV-11 as is (MlemStudy, truth-centred valley, cyclic) | 1.6 | 3.5 mm = 1.31 el (64 / 64 seeds) | 1.5 mm = 0.56 el (64 / 64) | — |
| L1 | blind prominence test + null, self-consistent floods, cyclic | 1.6 | 1.5 / 1.5 | 1.25 / — (null 8–15 %) | 1.0 / 1.0 |
| L2 | transported floods, every deposit | 1.6 | — (best 95 %) / — (89 %) | 2.5 / — (null 13–22 %) | 2.5 / 2.5 (null 4–14 % at 1.75–2) |
| L3 | non-cyclic decoding (L2's maps) | 1.6 | — / — | 2.5 / — | 2.5 / 2.5 |
| L4 | hand-held head at its own S = 100 mm | 1.55 | — (70 %) / — (58 %) | — (null 40–50 %) / — | 1.25 / 1.25 |
| L5 | hand-held head at 1 m | 1.058 | — (98 % at 2.0, 92 % at 2.5) / — (92 %) | — (null ~50 %) / — | 1.25 / 1.25 |

Attribution:

| Change | Effect |
|---|---|
| Criterion (L0 → L1) | EV-11's "MLEM resolves 1.5 mm" (0.56 el) becomes 1.25 el once the test is blind and false splits are counted; CC moves 1.31 → 1.5 el |
| Transport (L1 → L2) | Transport of every deposit (Compton in the crystal, slab leak) against the thin-mask forward models makes the pixel-area and engine MLEMs false-split at high counts: both move to 2.5 el |
| Cyclic (L2 → L3) | No effect, as predicted: every pixel ray hits the 2 × 2 mosaic |
| Hand-held head (L3 → L4 / L5) | The pixel-area MLEM resolves 1.25 el at both 155 mm and 1 m. The engine MLEM at 80 iterations is unusable: about half of all single sources split |

EV-11's near-field figure therefore does not survive the blind criterion even at the lab. In angle, the pixel-area result
at 1 m (1.25 el = 1.30°) is the field figure.

## DR-9 by-product — single-source bias and precision at 1 m against the near field

Cross-correlation, the scenario decoder's tent sub-cell estimate. 128 seeds, one jittered position each. Poisson:
200 acquisitions per seed and count level (N = 25 600). Degrees; "dy" is the signed y component of the argmax error.

| Geometry | Noiseless argmax error, median / max | Noiseless tent error, median / max | Signed dy, median | RMS at 250 counts | RMS at 500 counts | RMS at 1000 counts | Within one element (250 / 1000) |
|---|---|---|---|---|---|---|---|
| 1 m | 0.177 / 0.405 | 0.145 / 0.337 | −0.115 | 0.381 (6.6 mm at 1 m) | — | 0.212 (3.7 mm) | 98.9 % / 100 % |
| 5 m | 0.454 / 1.008 | 0.369 / 0.907 | — | 0.812 | — | 0.525 | 92.1 % / 98.5 % |
| S = 100 mm (z = 155 mm) | 0.184 / 0.432 | 0.177 / 0.344 | — | 0.510 (1.38 mm at the source plane) | 0.243 (0.66 mm) | 0.211 (0.57 mm) | 98.1 % / 100 % |

PR-IMG-02 quotes a 0.25 mm floor (= 0.09° at 155 mm) and "sub-mm with ≥ 250 counts". Here the same head at the same
distance, in the 662 keV window, averaged over sub-element position, gives 0.57 mm at 1000 counts and 1.38 mm at 250.
EV-34 also reported 1.00 ± 0.28 mm at 250 counts ideal.

The noiseless error is position-dependent (up to 0.43°): PR-IMG-02's on-axis point is a favourable sampling phase. In
angle, precision at 1 m equals precision at the near field at 1000 counts (0.21°). Flagged for the planner (step 4); no
VV document was edited.

## Ambient (DR-7)

64 seeds. 0.10 µSv/h, 60 s. Transported terrestrial field: truth 5 × 10⁵ histories; the instrument model an independent
5 × 10⁵ histories, used as b_i. Background in the window: bare 4.29 cps per µSv/h → 25.7 counts; front-only 0.105 cps
per µSv/h → 0.63 counts.

Expectation: the bare field adds 1.3 % to a 1000 + 1000 pair and 5 % to 250 + 250, flat in the 662 keV window
(edge/centre 0.99–1.01, EV-34). The CC median baseline absorbs a flat pedestal, and b_i removes it from MLEM, so the
change should stay within the seed-bootstrap spread.

Observed:

- **Resolved separations:** every one is identical to ideal: pixel-area 1.25, engine 1.5 at 1000 counts; none at 250.
- **Passes at 1000 counts, Δ 1.25** (ideal / bare / bare + b):

  | Decoder | Ideal | Bare | Bare + b |
  |---|---|---|---|
  | pixel-area | 0.985 | 0.981 | 0.981 |
  | engine | 0.560 | 0.556 | 0.555 |
  | CC | 0.451 | 0.462 | — |

- **Passes at 250 counts, Δ 2.0** (ideal / bare / bare + b):

  | Decoder | Ideal | Bare | Bare + b |
  |---|---|---|---|
  | pixel-area | 0.834 | 0.825 | 0.839 |
  | engine | 0.830 | 0.817 | 0.823 |

- **Front-only:** within 1 point of ideal throughout.

The b_i term matters only at the lowest count level, by ≤ 1.4 points.

## Deviations

1. **Run time:** ~5.5 h wall against the ~3 h guidance. I underestimated per-seed cost under full load (1 m: 11 min, not
   4). Once the driver had queued the full N, cutting it would have meant stopping processes, which needs approval; I
   let it finish rather than run a second, reduced set. No result depends on the overrun.
2. **"binary" (the engine's matrix) runs through the opt-in vectorised loop** (`MlemSystemModel(1, 0)`): the same matrix,
   single-precision sums. Its agreement with the default loop is tested within a derived bound. The default `Decode`
   path is untouched.
3. **The opt-in loop is single precision** (memory-bound; about 2× faster). λ, the sensitivities and the update stay
   double.
4. **Seed counts:** ambient and ladder use 64 seeds; matched uses 32 (validation) and 16 (selection); selection uses 32.
   DR-3's 128 × 50 applies to 1m, 5m, wide and near. The matched family carries no point section.
5. **The selection request's `_about`** names `angres/select_iterations.py`; the selection is
   `aggregate_angres.py --select`. I did not edit the request after its runs, to keep the hash pinned in the manifest.
6. **The engine tree was dirty** (uncommitted) during all runs; `run-info.json` records that. The CLI was rebuilt once
   after `angres_select` (a ladder-only change: L3 reuses L2's maps), before any family that uses that code.
7. **Ladder L1** uses expected maps from a re-implementation of MlemStudy's flood model (`SelfConsistentFlood`, tested
   against the exact open-area share) plus exact Poisson draws, rather than MlemStudy's own MC floods. L0 is MlemStudy
   itself. The ladder separation grid stops at 2.5 el.
8. **Correction to my turn-1 review:** the pixel-area (and matched) MLEM also false-splits when over-iterated. Its
   advantage is that the splitting starts much later (≥ 240–320 iterations against ≥ 15 for the engine matrix).

## What could not be run

- No matched FWHM (no point section in that family). No matched near the edge, and no 1:4 / edge at 5 m.
- No interval-based ("lower bound ≥ 95 %") version of the DR-2 rule. The point estimate is used, as DR-2 states; the
  intervals are in the JSON.
- The ±7° and ambient conditions were run on axis, 1 : 1 only.
- No desktop or UI tests (none were needed).

## Commands that wrote anything

- `dotnet build Gcam.sln -c Release` (several times; repository build outputs).
- `dotnet test Gcam.sln -c Release` (before and after; outputs in `%TEMP%\gcam-todo34\test-{before,after}.txt`), plus
  filtered `dotnet test tests/Gcam.Tests -c Release --filter …` while developing.
- Scratch builds and runs under `%TEMP%\gcam-todo34\{bitcheck,bench,smoke}` (scratch consoles referencing the Release
  DLLs; `Gcam.Cli.dll ambient-evidence` on scratch requests in `smoke`).
- `python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-angres-v1.json --out %TEMP%\gcam-todo34\runs …`
  (the three invocations above).
- `python samples/evidence/angres/aggregate_angres.py …`, writing to `%TEMP%\gcam-todo34\*.json` and the 8 result files.
- Python generator scripts in `%TEMP%\gcam-todo34\aux\` that wrote the request files and the manifest.
- Repository files created or edited: those in the Files table, and this report.
