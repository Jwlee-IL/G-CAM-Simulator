# PLAN.Docs.CalibrationRecords.Review — implementer's turn-1 review: the three calibrations, the form, the generator

Scope: TODO-28 step 1, turn 1 (review only) of [PLAN.Docs.CalibrationRecords](PLAN.Docs.CalibrationRecords.md) and the
form [VV.Gcam.Calibration.Template](../VV.Gcam.Calibration.Template.md). The reviewer is the substitute implementer (a Claude
subagent; Codex is unavailable). Every row of "What exists" and every CR row was checked against the committed files,
the scripts and git. No code, plan, Todo or VV document was changed. Scratch work is in `%TEMP%\gcam-cal\` (one schema
dump script, machine-local).

Status: review, 2026-10-07. Every number below was read or recomputed from committed files (Python 3.11, stdlib +
the repository's JSON). No Monte Carlo was run.

## At a glance

- **The plan holds in outline.** The three calibrations exist where the plan says, with three gaps:
  - the 96 / 96 validation is in `ambient-baseline-v1-turn8-gate.json`;
  - the CAL-02 validation summary does not name its threshold file (the pin sits in the request);
  - CAL-03's floors carry no seed list, no manifest name and no selection statistic.

  Every selection / validation seed pair expands from `seeds.json` and the manifests, and all five pairs are disjoint
  (gate v1, gate v2, csco, angres 1 m, angres 5 m). Every pinned hash in the manifests and requests matches the bytes,
  and the threshold values inside each validation file equal the selection file's, key for key.
- **The engine commit cannot be filled honestly from the repository, for any record.**
  - The driver writes `engine_commit` / `engine_tree_dirty` only to the machine-local `run-info.json`.
  - The one committed statement (turn-8 report: "HEAD `9caa3af` plus uncommitted changes") names a commit that **does not
    exist in this repository**. Nor do `9f5ed23`, `aa9fcc4`, `b6ebb26`, `cf469aa`, `6ac21c0` from the later turn reports:
    the history was rewritten when it moved machines.
  - TODO-34's runs were on a dirty tree (archived Turn-2 report). TODO-35's are unrecorded.
  - What can be stated: "runs on an uncommitted working tree; the code was committed in `aed4b54` / `afbb0ae` /
    `98bc133`" (git log of the selection files). That is an inference, not a record. The template field needs to allow
    exactly that.
- **Gate v1 is still cited.** EV-34 says "a first selection on 4,096 nulls per configuration passed 93 / 96". That makes
  it a superseded record with three FAIL rows: 13 / 2,048, upper limit 1.0073 %. This is what the form's
  "a FAIL row stays visible" rule exists for.
- **EV-15's "144 / 144, pooled 0.284 %" counts 9 empty configurations.** These are ideal, Co-60 = 0, so there are no
  counts and the threshold is the no-candidate floor −10³⁰⁰. Over the 135 informative configurations the result is
  **135 / 135, pooled 3,350 / 1,105,920 = 0.303 %**. The record must show the 9 as "not applicable — no counts", not
  as passes. The EV-15 sentence (and Findings 65) should be corrected in the same commit.
- **CAL-03's verdict depends on a criterion that the files leave open.** Under D-48's rule (pooled single-source false
  split ≤ 5 %), all 414 (condition × decoder) rows pass. Four rows at 1 m have a bootstrap upper 95 % limit above 5 %;
  the worst is cross-correlation, on axis, Δ = 2, 4,000 counts per source, 1 : 1: 4.28 % [2.70, 6.23]. CAL-01 and
  CAL-02 judge by the upper limit, and the form's own example does too. CAL-03 must state its criterion, and that is a
  decision (question 1).
- **The form needs five changes** (proposals 6–10):
  - generated sections contiguous, so one marker pair per record works;
  - an engine field that admits "not recorded";
  - a "value fixed before validation" field;
  - a "not applicable" verdict;
  - the interval method stated.

  The generator is a stdlib-only Python script with per-kind adapters, a committed pin file and `--check`. It runs in
  a new small CI job. Cost: one implementation turn, no Monte Carlo (proposal 11).

## Per row

### What exists

| Row | Verdict | Evidence |
|---|---|---|
| CAL-01 selection `ambient/gate-thresholds-v2.json`: rule, α, family, seed list / offset, `RunsDigest`, `PerConfiguration`, `Selection`, `Comparison` | **holds** | Keys: `_about`, `Rule`, `Alpha` 0.003, `Family` `gate_selection_v2`, `SeedList` F128, `SeedOffset` 1, `Seeds` (64, explicit), `RunsDigest`, `PerConfiguration` (96), `Universal` 4.6245, `Selection` (96 × {Acquisitions 65,536, WithCandidate, AllowedExceedances 196, SelectionExceedances, SelectionRate, SelectionUpper95, Threshold, TiedAtThreshold, DistinctValues, MedianZ}), `Comparison` `RoundedAtLeast`, `NullsPerConfiguration` 65,536. `Seeds` == F128[1:65] |
| Selector `ambient/select_thresholds.py` | **holds** | `--rule inclusive` writes v2; `--rule strict` reproduces v1 (docstring; turn-8 report) |
| Validation: "turn-8 (and turn-7 for v1) — verify which holds 96 / 96" | **holds: turn 8** | `results/ambient-baseline-v1-turn8-gate.json`: `Family` gate_validation_v2, F256, offset 128, `Seeds` 128 (a count, not a list), `Thresholds` {File v2, Sha256 6352b2f6…} = the file's hash, `TargetFalseUpper95` 0.01, `PooledNulls` 2,424 / 786,432, `ConfigurationsPassing` 96 / 96; `Nulls` (96 × {Acquisitions 8,192, ExpectedBackgroundCounts, Threshold, FalseTrusted, Rate, Upper95, Pass, DispersionIndex, Universal…}). Recomputed: pooled sum matches; `Pass` == (Upper95 ≤ 0.01) in every row; thresholds == v2 key for key. Turn 7 (`turn7-gate.json`, v1, O128, 2,048 per configuration) passes **93 / 96**. Its `_about` says "turn 7" in both files (copied string, harmless) |
| "Used by PR-SENS-02, EV-34, AB-7 / AB-11" | **correct it** | AB-* are plan IDs; a VV record cannot cite them (no AB-/DA-/DR- ID appears in any `VV.*` today). The v2 thresholds are also used by CAL-02's raw-window counter-case (`csco-request-v1-validation.json` `RawGateThresholds` → v2), which PR-NRG-04 quotes ("would report Co-60 as a trusted Cs-137 source in 92–100 %"). Used by: PR-SENS-02, EV-34, and PR-NRG-04 / EV-15 (counter-case) |
| CAL-02 selection `ambient/csco-thresholds-v1.json`, "same schema + `NullsPerConfiguration`" | **holds, with one schema difference** | Same keys as gate v2 (gate v2 also has `NullsPerConfiguration`, and `FirstNulls`). **Keys are ragged:** `coloc\|t=10\|ideal\|Co=0` (4 fields) and `coloc\|t=10\|F=0.1\|BareCrystalAllFaces\|Co=0` (5). The generator must parse fields by name, not position. 144 = 3 scenes × 3 times × 16 (7 Co levels ideal, 7 bare, 2 front-only). 9 entries = −1e300 (`NO_CANDIDATE_FLOOR`, ideal Co = 0). `Seeds` == F256[1:65] (the same 64 seeds as the gate selection; irrelevant across records) |
| CAL-02 validation `results/csco-v1-validation.json` via `aggregate_csco.py` | **holds, but no threshold pin in the file** | `NullValidation` (144 × {Threshold, Trusted {K, N 8,192, Rate, Lower95, Upper95}, TrustedAtCo, ExpectedCounts662/Ref, PassesOnePercent, Raw…}). `Seeds` (128, explicit) == F256[128:256]. Thresholds == csco v1 key for key. The pin is in `csco-request-v1-validation.json` (`Thresholds.Sha256` fac811b2… = the file's hash), which is pinned in `manifest-csco-v1.json` `config_sha256`. So the generator checks the chain manifest → request → thresholds |
| CAL-03 "inside `results/angres-v2-floor.json` (`Families`)", selector `aggregate_angres.py --floor` | **holds** | `Families.{angres_floor_1m, angres_floor_5m}` = {SelectionFamily, SelectionSeeds (a count: 32), ValidationFamily, ValidationSeeds (128), AlphaSelection 0.03, Valley 0.25, SourceDetectorMm, ElementDeg, `Floors` (48 / 6 × {Decoder, TotalCounts, Placement, Assigned, HypothesisFree, SelectionNullsPerSeparation 1600}), `Resolved`, `Conditions` (128 / 16, per decoder {N 6,400, Shape / Floor / FloorHypothesisFree {Pass, Null, PassCi, NullCi}})} |
| CAL-03 "validation: the same file (validation rows)" | **holds, with three gaps** | (1) No seed lists and no manifest name: expand from `manifest-angres-v2.json` (pinned by the generator) → F256[193:225] / F256[33:161]. (2) No selection statistic at the floor: only the floor and N. (3) Rates are rounded to 4 decimals with a percentile bootstrap CI over seeds (2,000 resamples), not k and a binomial limit. k = round(rate × 6,400) recovers k exactly (rounding error ≤ 0.32 counts; checked in all rows). Floor units = "the image's own units" (`ResolvedPair.SecondPeaks`), so they are decoder-specific and not comparable across decoders. `binary@8` has floors but is not the claimed decoder (D-46: `area@120`) |
| Manifests / seeds | **holds** | `seeds.json`: F256[0:128] == F128; all lists internally distinct; O128 ∩ F256 = {12345}. Every `config_sha256` in ambient-v1/v2, csco-v1, angres-v2 matches today's bytes. Manifests are **mutable**: ambient-v2 got the AB-13 families appended after the gate runs (turn-8 report). A pin taken today pins today's manifest, not the run-time one |
| `.github/workflows/ci.yml` | **checked** | Three jobs: `dotnet` (windows, build + test), `rtl` (ubuntu, Python 3.13 + cocotb), `badges`. `actions/checkout@v4` is **shallow** (depth 1), so a generator that needs git history fails in CI |

### CR rows

| Row | Verdict | Evidence / correction |
|---|---|---|
| CR-1 one generator, adapters, per-record markers, prose outside untouched | **correct it** | The form interleaves generated (§3, 4, 5, 7) and written (§1, 2, 6, 8) sections, and the header table mixes both ("Number of values" is generated). One begin/end pair per record would then overwrite prose. Either reorder the form so the generated block is contiguous (proposal 6), or use one marker pair per generated section. Also: never print the files' `_about` / `Rule` strings verbatim. They carry "AB-11", "TODO-30 turn 8" and plan IDs that a VV reader cannot resolve |
| CR-2 CAL-01 v2, CAL-02 csco v1, CAL-03 angres v2; a superseded record only where cited | **correct it** | Gate v1 is cited (EV-34 "first selection … 93 / 96 … 13 / 2,048, upper 1.007 %"; Findings). So it gets a superseded record. Every field is fillable from `gate-thresholds-v1.json` + `turn7-gate.json`. v1 has no `Comparison` key: the adapter must state "strict, trusted = Z > T", not default to the v2 rule. Numbering: proposal 2 |
| CR-3 checks: pinned SHA-256, disjoint seeds expanded from `seeds.json` + offsets, N with every rate, FAIL rows kept, non-zero exit | **holds; extend** | Disjointness verified for all five pairs (table below). Add cheap cross-checks that would have caught real drift (proposal 4). Pins must live outside the generated text, or "refuses to write on mismatch" is a check against itself (proposal 3) |
| CR-4 full long tables, grouped, summary line first | **holds; refine** | CAL-01 96 rows → 4 groups (case) × 24; CAL-02 144 → 9 groups (scene × time) × 16; CAL-03 54 floors → groups (distance × decoder). For CAL-01 / 02 the value and validation keys coincide, so one combined row (value, selection k / N, validation k / N, upper, verdict) halves the length. CAL-03's validation keys (separation × counts per source × ratio) differ from the floor key: print per floor the worst validation row and the number of conditions it covers. The full 414 rows go in a `<details>` block or are left out (question 3). Sort axes numerically (the files order `t=300` before `t=60`) |
| CR-5 `--check` exercised by a test or CI | **holds; choose CI** | No Python test harness exists (the only `test_*.py` are cocotb files under `rtl/`). No xUnit test shells out to Python. A Python `--check` in a new ubuntu job is the cheapest and most direct choice. An xUnit test would make every local `dotnet test` depend on a Python on PATH (proposal 11) |
| CR-6 prose drafted by the implementer, marked for review | **holds** | Sources: the archived plans and turn reports (they cannot be linked from VV) and EV-11 / 15 / 34, D-44 / D-48. A visible draft marker (e.g. `*(draft — for review)*`) that `--check` refuses once the planner signs off gives a mechanical end state (proposal 5) |

**Seed disjointness** (expanded with `manifest.families[*].seeds / seed_offset / n` over `seeds.json`, as
`run_seeds.jobs_for` does):

| Record | Selection | Validation | Overlap |
|---|---|---|---|
| gate v1 | F128[1:65] (64) | O128[0:128] (128) | 0 |
| gate v2 | F128[1:65] (64) | F256[128:256] (128) | 0 |
| csco v1 | F256[1:65] (64) | F256[128:256] (128) | 0 |
| angres 1 m / 5 m | F256[193:225] (32) | F256[33:161] (128) | 0 / 0 |

Every outer stream in `AmbientGateStudy` is keyed by the outer seed (`GateResponse.StreamSeed(q.Seed, …)`,
`DefaultRandom.Key(q.Seed, …)`). So disjoint outer seeds mean independent maps and draws. Across records seeds are
reused (the gate and csco selection share F256[1:65]; angres selection lies inside the gate v2 / csco validation range).
That is harmless: they are different studies, and the record should say "disjoint within this record".

## Per record — what the committed files can fill

Legend: **file** = generated from a committed file; **derived** = computed by the generator from files; **written** =
prose; **cannot** = not recorded anywhere committed.

### CAL-01 — PR-SENS-02 trust thresholds, v2 (and the superseded v1)

| Field | Fill | Source / value |
|---|---|---|
| Record date | file (manifest) | `manifest-ambient-v2.json` `measured` 2026-10-04. The selection run's own date is not in the threshold file |
| Used by | written | PR-SENS-02, EV-34; PR-NRG-04 / EV-15 (raw-window counter-case) |
| Quantity | written | largest studentised correlation Z over the decoder grid, recorded to 4 decimals; trusted when recorded Z ≥ T (v1: Z > T) |
| Configuration key | file | case {lab, head, head1m, head5m} × t {10, 60 s} × field {0.05, 0.10, 0.20 µSv/h} × bound {bare, front-only} × window {open, cs662} |
| Number of values | derived | 96 |
| Status | written | valid (v1: superseded by v2) |
| §1 target, pass criterion | file + written | `TargetFalseUpper95` 0.01 (one-sided 95 % Clopper–Pearson); wording written |
| §2 α, comparison, N | file | α 0.003, `RoundedAtLeast`, 65,536 per configuration, allowed 196. The rationale (P(fail) table: 1.2 × 10⁻¹² at a true 0.30 %) is written |
| §3 families, seeds, N | file + derived | selection gate_selection_v2, F128[1:65], 64 × 1,024; validation gate_validation_v2, F256[128:256], 128 × 64 = 8,192; overlap 0 |
| §3 files + SHA-256 | derived | thresholds v2 6352b2f6…, turn-8 gate d2d55d5e…, manifest, seeds, request 551fb009…, spectrum 4d48bc42… |
| §3 RunsDigest | file | 3c492e5b… (hash of the machine-local selection runs; printable but not verifiable) |
| §3 engine commit | **cannot** | turn-8 report: "HEAD `9caa3af` + uncommitted"; `9caa3af` is not in this repository. Code landed in `aed4b54` (git log — inference) |
| §3 pinned before validation | file | `turn8-gate.json` `Thresholds.Sha256` == file hash; manifest override pin. Timing (16:18:56 vs 16:19:27) only in the turn report |
| §4 values | file | Threshold, SelectionExceedances / Acquisitions, SelectionUpper95, TiedAtThreshold, WithCandidate |
| §5 validation | file + derived | FalseTrusted / 8,192, Upper95, Pass, ExpectedBackgroundCounts; pooled 2,424 / 786,432 = 0.308 % (0.298–0.319 %); worst lab \| 10 s \| 0.10 \| front-only \| cs662, 40 / 8,192, upper 0.635 % |
| §7 reproduce | written (fixed text) | the turn-8 report's commands (selection, `select_thresholds.py --rule inclusive`, validation, `aggregate_gate.py`) |
| §6, §8 | written | triggers: decoder, grid, window definition, background-shape model, spectrum file, head geometry, the Z recording precision. Limits: bounds bracket an unknown housing; valid at the grid points only; ambient-only nulls (not valid under Co-60, see CAL-02); no product / Studio code reads the file (only `AmbientGateStudy`, `CsUnderCoStudy`) |

v1 (superseded): every field as above from `gate-thresholds-v1.json` (83b931ac…) and `turn7-gate.json` (2854259e…).
It used 4,096 / 2,048 nulls per configuration, the strict comparison, and validation on O128[0:128], with **93 / 96**
passing. The three FAIL rows are head1m \| 60 \| 0.05 \| front-only \| cs662, head5m \| 10 \| 0.05 \| front-only \| open and
head5m \| 10 \| 0.10 \| front-only \| cs662, each 13 / 2,048 with upper 1.0073 %. `turn7-gate.json` has no `PooledNulls`
and no `SeedOffset`, so the generator derives the pooled value and assumes offset 0 from the manifest.

### CAL-02 — stripped-image Z_s thresholds (csco v1)

| Field | Fill | Source / value |
|---|---|---|
| Record date | file (manifest) | `manifest-csco-v1.json` `measured` 2026-10-06 |
| Used by | written | PR-NRG-04, D-44, EV-15 |
| Quantity | written | largest stripped studentised correlation Z_s (per-pixel R_i, side reference window); trusted when recorded Z_s ≥ T |
| Configuration key | file (ragged) | scene {coloc, sep2, sep5} × t {10, 60, 300 s} × environment {ideal, 0.10 µSv/h bare, 0.10 µSv/h front-only} × Co-60 {0, 0.1, 1, 3, 10, 28.72 (= 10 µSv/h), 100 MBq}; front-only only at Co 0 and 28.72 MBq |
| Number of values | derived | 144, of which 9 are `NO_CANDIDATE_FLOOR` (ideal, Co = 0: no counts) |
| §1 / §2 | file + written | α 0.003, `RoundedAtLeast`, 65,536 per configuration; pass = upper ≤ 1 % (`PassesOnePercent`, consistent with `Upper95` in every row) |
| §3 seeds, N, overlap | file + derived | F256[1:65] × 1,024; F256[128:256] × 64 = 8,192; overlap 0 |
| §3 pins | derived | thresholds fac811b2…; validation 79cc4d00…; request pin == file; manifest pins the request |
| §3 engine commit | **cannot** | not recorded in any committed file; code landed in `afbb0ae` (inference) |
| §4 values | file | as CAL-01 (`Selection` has the same ten fields) |
| §5 validation | file + derived | Trusted K / 8,192, CP limits, verdict; also TrustedAtCo and the raw-gate counter-case where present. Summary: **135 / 135 informative pass, pooled 3,350 / 1,105,920 = 0.303 %; 9 not applicable**; including them, 0.284 % (EV-15's figure). Worst: sep5 \| 60 s \| 0.10 bare \| Co 1 MBq, 38 / 8,192, upper 0.608 % |
| §7 | written (fixed text) | `run_seeds.py --manifest manifest-csco-v1.json --family csco_selection` → `select_csco_thresholds.py` → `--family csco_validation` → `aggregate_csco.py` |
| §6, §8 | written | triggers: R_i calibration, reference window, gain model, Co-60 / Cs-137 scene geometry, the gate's grid; limits: 1 m, hand-held head, one field level (0.10 µSv/h), three scene geometries, before housing / room scatter and a four-channel readout model |

### CAL-03 — pair-test significance floors (angres v2)

| Field | Fill | Source / value |
|---|---|---|
| Record date | file (manifest) | `manifest-angres-v2.json` `measured` 2026-10-06 |
| Used by | written | PR-IMG-04, D-48, EV-11 |
| Quantity | written | floor F on the second peak's absolute topographic prominence, **in the decoder's own image units**; a pair counts only when the shape test passes and prominence ≥ F |
| Configuration key | file | distance {1 m, 5 m} × decoder {cc, area@120, binary@8} × total counts {500 … 80,000 at 1 m; 2,000, 8,000 at 5 m} × placement {axis, edge at 1 m; axis at 5 m} |
| Number of values | derived | 54 primary (`Assigned`) + 54 sensitivity (`HypothesisFree`). None is ∞ (no `None` in the file) |
| §2 rule, α, N | file + written | α_sel 0.03 at every separation of the grid; 1,600 selection nulls per separation (32 seeds × 50); rule text from the aggregator's docstring, rewritten without plan IDs |
| §3 seeds | derived only | not in the file: from `manifest-angres-v2.json` (the generator must pin it, since the result file does not name it) |
| §3 engine commit | **cannot** | dirty tree (archived Turn-2 report, machine-local `run-info.json`); code landed in `98bc133` (inference) |
| §3 pinned before validation | **not applicable** — must be said | the floors are selected and applied in one aggregation step. The validation runs record the raw statistic (`NullStat` / `PairStat`), so they do not depend on F; disjointness is enforced in `floor_family` |
| §4 selection statistic at the value | **cannot** | only F and N are stored; the achieved false-split share at F needs the runs (re-aggregation if the machine-local runs still exist, otherwise 2 × 32 seeds re-run). The rule guarantees ≤ 3 % at every separation |
| §5 validation | file + derived | per floor: worst `Floor.Null` over its conditions (k = rate × 6,400 exactly), bootstrap CI, conditions covered. All 414 rows ≤ 5 % pooled; 4 rows at 1 m have a CI upper limit > 5 % (worst cc \| axis \| Δ 2 \| 4,000 \| 1 : 1, 4.28 % [2.70, 6.23]); 5 m: none |
| §6, §8 | written | triggers: iteration counts (TODO-36 may change them), forward model, valley v, peak finder, grid, distance; limits: false splits bounded at the tested pair geometry only (EV-11), still head, ideal pixel identification; the floors exist only inside the aggregator. No code path reads them, so a Studio pair test (TODO-36) would need them as a data file |

## Proposed changes to the plan

1. **Correct the "What exists" table.**
   - CAL-01's validation file is `ambient-baseline-v1-turn8-gate.json` (96 / 96); turn 7 belongs to the superseded v1.
   - "Used by" lists VV IDs only, with PR-NRG-04 / EV-15 added for CAL-01 (counter-case).
   - CAL-02's validation summary does not name its thresholds: the pin chain is manifest → request → thresholds.
   - CAL-03's file has no seed lists, no manifest name and no selection statistic.
2. **Records and numbering.** Four records: the three current ones plus gate v1, superseded. Recommended IDs in
   chronological order: CAL-01 gate v1 (superseded by CAL-02), CAL-02 gate v2, CAL-03 csco v1, CAL-04 angres v2. The
   alternative keeps CAL-01…03 as in the plan and adds v1 as CAL-04. Either works, but the IDs must be fixed before
   PR / EV rows cite them.
3. **Pins in a committed spec, not in the generator's output.** Add `samples/evidence/calibration-records.json`. Per
   record it holds: kind (adapter), files with SHA-256 (selection, validation, manifest, `seeds.json`, the requests),
   selection / validation family IDs, and the written header fields (Used by, Quantity, Status, superseded-by).
   - Write mode refuses on any hash mismatch.
   - Changing a pin is a deliberate edit that also prompts a Status review.
   - Hash the raw bytes and refuse a file that contains `\r` (`.gitattributes` forces LF, so the working tree equals
     the blob; verified for all seven data files). Do not call git: the CI checkout is shallow.
4. **Cross-checks beyond CR-3**, all cheap and all passing today:
   - the thresholds in the validation file == the selection file's, key for key;
   - the selection `Seeds` list == the manifest slice, and the validation `SeedList` / `SeedOffset` / count == the
     manifest family;
   - every `config_sha256` in the manifest matches;
   - the threshold pin in the validation file or request == the selection file's hash;
   - every stored verdict == its criterion recomputed (Upper95 ≤ target);
   - stored pooled sums == recomputed;
   - CAL-04: every validation condition maps to a floor, and k = rate × N is an integer within 0.33.
5. **Prose markers.** Drafted prose carries a visible `*(draft — for review)*`. A `--release` flag (or `--check`
   once the planner removes the markers) refuses any left. The generator never prints `_about` / `Rule` strings or
   other plan IDs.
6. **Reorder the form so the generated part is contiguous:**
   - header (written);
   - 1 Purpose and acceptance, 2 Selection rule, 3 Re-calibration triggers, 4 Known limits (written);
   - then one generated block: 5 Data (with "Number of values"), 6 Values, 7 Validation, 8 Reproduce.

   One marker pair per record (`<!-- CAL-nn generated: begin -->` / `end`) then suffices, as CR-1 intended.
7. **Engine field.** Replace "engine commit of the runs" with "Engine: commit + dirty flag as recorded at run time, or
   *not recorded* plus the commit in which the run's code was committed (inferred from history)". For the future, the
   aggregators (`aggregate_gate.py`, `aggregate_csco.py`, `aggregate_angres.py`, the selectors) should copy
   `run-info.json`'s `engine_commit`, `engine_tree_dirty` and `manifest_sha256` into the committed summary, and the
   driver should refuse evidence runs on a dirty tree unless told otherwise. That is outside this step: a Todo item.
8. **Add "Value fixed before validation"** to §5 Data: *yes — file + hash pinned in the validation request / manifest*
   (CAL-01…03), or *not needed — validation runs record the raw statistic and the value is applied in aggregation*
   (CAL-04).
9. **Add the verdict "not applicable — <reason>"** (e.g. no counts). The summary line reports passed / informative,
   the not-applicable count, and the pooled rate over informative rows, with the all-rows figure beside it.
10. **State the interval method and the pass criterion per record:** exact one-sided Clopper–Pearson (CAL-01…03) or a
    percentile bootstrap over seeds, 2,000 resamples (CAL-04). Keep "date of the selection run" as "date measured
    (manifest)", because the threshold files carry no date.
11. **Generator design and CI.**
    - **Files.** `samples/evidence/calibration_record.py`: stdlib only (no SciPy / NumPy; every limit it prints is
      already in the files), every read and write with `encoding='utf-8'` and `newline='\n'` (this machine's Python
      defaults to cp949 and fails on the manifests otherwise).
    - **Adapters.** One per kind, each returning a neutral record model (keys parsed by field name, numeric sort,
      values, selection, validation rows, pooled summary, seed sets): `gate` (v1 + v2, with v1's missing
      `Comparison` / `PooledNulls` / `SeedOffset`), `csco` (ragged keys, no-candidate floor), `angres_floor` (k from
      the rate, bootstrap CIs, worst per floor).
    - **Rendering.** A shared renderer with fixed-precision formatting and no timestamps, so the output is
      byte-stable across Python versions.
    - **Modes.** Write mode replaces each record's marker block in `docs/VV.Gcam.Calibration.md` and a generated
      index table at the top. `--check` regenerates in memory, runs every check and exits non-zero on any check
      failure or text difference, printing the first differing lines.
    - **CI.** A new job `calibration-records` on `ubuntu-latest` (`actions/setup-python@v5`, 3.13,
      `python samples/evidence/calibration_record.py --check`), about 20 s with no dependencies. Run the same
      command locally before committing a changed evidence file.
    - **Cost.** About 450–600 lines of Python and a ~80-line spec, plus a generated document of roughly 700–900
      lines. Prose drafts take about 4 × 8 short paragraphs. Docs edits: AGENTS.md table, the Conventions examples,
      `samples/evidence/README.md`, the CAL citations in PR-SENS-02 / PR-NRG-04 / PR-IMG-04 and EV-11 / 15 / 34,
      and the EV-15 + Findings 65 pooled-rate correction (proposal 12). One turn, no Monte Carlo.
12. **Correct EV-15 / Findings 65:** "144 / 144 (pooled 0.284 %)" → "135 / 135 informative configurations (9 ideal
    Co-60 = 0 configurations have no counts), pooled 0.303 %". The worst-case upper limit (0.61 %) is unchanged.

## Open questions for the author

1. **CAL-04 (angres floors) pass criterion.** D-48 says single-source false splits ≤ 5 % as a pooled rate, and all 414
   rows meet it. On the one-sided upper-limit standard of the other records, 4 rows at 1 m exceed 5 % (worst 4.28 %,
   upper 6.23 %). Options:
   - (a) keep D-48's point criterion and record the upper limits beside it (recommended: D-48 is the author's
     definition and the floor's 3 % selection α was sized for it);
   - (b) adopt the upper limit for CAL-04 and mark those 4 rows FAIL, which reopens PR-IMG-04's wording.
2. **Using a threshold between grid points.** Every value is validated only at its grid point: field 0.05 / 0.10 /
   0.20 µSv/h, bound bare / front-only, exposure 10 / 60 / 300 s, total counts. A product must also know the ambient
   level and its own housing, which the model brackets but does not know. Should the record only say "grid points
   only, bounds are brackets" (recommended for now), or should a rule be decided, such as taking the more conservative
   neighbour?
3. **CAL-04 length.** Per floor, print the worst condition (54 rows), or also all 414 condition rows in a collapsed
   block? Recommended: the worst per floor, plus the 4 rows whose upper limit exceeds 5 %.

## What could not be checked

- The engine state of any run (the commits in the turn reports are not in this repository; the `run-info.json` files
  are machine-local).
- Whether the selectors reproduce the committed files: that needs the machine-local runs or a re-run (gate selection
  64 seeds, csco 64, angres 2 × 32).
- CAL-04's achieved selection false-split share at each floor (same reason).
- The run-time state of `manifest-ambient-v2.json` (it was appended after the gate runs; the gate families are said to
  be unchanged).

## Commands that wrote anything

- A schema-dump script written by heredoc to `/tmp/sk.py`, which Git Bash maps to `%TEMP%\sk.py`. **That is outside
  `%TEMP%\gcam-*`, a breach of the path limit.** It was then copied to `%TEMP%\gcam-cal\sk.py` (`mkdir -p
  "$TEMP/gcam-cal"`). The stray copy is a 1 KB read-only JSON printer.
- This file, `docs/PLAN.Docs.CalibrationRecords.Review.md`.

## APPROVAL REQUESTS

- `Remove-Item "$env:TEMP\sk.py"`. Why: it removes the stray scratch file written outside `%TEMP%\gcam-*`. It destroys
  only that file, and an identical copy stays in `%TEMP%\gcam-cal\sk.py`. Undo: copy it back from `%TEMP%\gcam-cal\sk.py`.
