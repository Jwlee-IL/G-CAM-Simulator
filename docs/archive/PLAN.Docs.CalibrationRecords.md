# PLAN.Docs.CalibrationRecords — calibration records in a fixed form, generated from the committed files

Scope: first step of TODO-28 (test documentation and generated test reports), asked by the author on 2026-10-06 after
TODO-34 / 35: values chosen per configuration on Monte Carlo data must be recorded in a fixed form ("테스트 양식을
만들어두고 그 양식에 맞춰 기록"). The form is [VV.Gcam.Calibration.Template](../VV.Gcam.Calibration.Template.md) (written by the
planner). This plan builds the generator and the three existing records. Process only from the author's other project
(a reusable template file + one filled report per case); no content from it.

Status: **done** 2026-10-07 — review and implementation by a substitute Claude subagent ([review](PLAN.Docs.CalibrationRecords.Review.md), [turn 2](PLAN.Docs.CalibrationRecords.Turn2.md)); the planner signed off the drafted prose of CAL-01 … 04, applied the citation edits (PR-SENS-02, PR-NRG-04, PR-IMG-04, EV-11 / 15 / 34, D-48), recorded D-50 / D-51 and switched CI to `--release`; `--release` passes.

## What exists (checked, 2026-10-06)

| Calibration | Selection file | Validation | Used by |
|---|---|---|---|
| PR-SENS-02 trust thresholds (96 configurations) | `samples/evidence/ambient/gate-thresholds-v2.json` (rule, α, family, seed list / offset, `RunsDigest`, `PerConfiguration`, `Selection`, `Comparison`); selector `ambient/select_thresholds.py` | `samples/evidence/results/ambient-baseline-v1-turn8-gate.json` (and turn-7 for v1) via `ambient/aggregate_gate.py` — verify which file holds the 96 / 96 validation | PR-SENS-02, EV-34, AB-7 / AB-11 |
| Stripped-image Z_s thresholds (144) | `samples/evidence/ambient/csco-thresholds-v1.json` (same schema + `NullsPerConfiguration`); `ambient/select_csco_thresholds.py` | `samples/evidence/results/csco-v1-validation.json` via `ambient/aggregate_csco.py` | PR-NRG-04, D-44, EV-15 |
| Pair-test significance floors | inside `samples/evidence/results/angres-v2-floor.json` (`Families`), selector in `samples/evidence/angres/aggregate_angres.py --floor` | the same file (validation rows) | PR-IMG-04, D-48, EV-11 |
| Manifests / seeds | `samples/evidence/manifest-*.json`, `samples/evidence/seeds.json` | | |

## Proposed decisions (reference — verify / improve)

| ID | Decision | State |
|---|---|---|
| CR-1 | One generator `samples/evidence/calibration_record.py` with one adapter per calibration kind (gate, csco, angres floor) reading the committed files; output: the generated parts of `docs/VV.Gcam.Calibration.md` between per-record markers (`<!-- CAL-nn generated: begin/end -->`), prose outside the markers untouched | proposed |
| CR-2 | Records: CAL-01 PR-SENS-02 gate thresholds (v2), CAL-02 stripped Z_s thresholds (csco v1), CAL-03 pair-test significance floors (angres v2); CAL-00-style superseded records only where a superseded file is still cited (gate v1 → mark superseded by CAL-01 if cited) | verify which files are cited |
| CR-3 | The generator checks: pinned SHA-256 of every file it reads (bytes as git stores them); selection and validation seed sets disjoint (expand seed lists from `seeds.json` + offsets, as the manifests define them); every rate printed with N; FAIL rows kept; exits non-zero on any check failure | proposed |
| CR-4 | Long tables (96 / 144 rows) are printed in full but grouped (one sub-table per head / distance or per decoder), with a summary line first | proposed — verify readability |
| CR-5 | A test runs the generator in check mode (`--check`: regenerate and compare with the committed `VV.Gcam.Calibration.md`) so a changed file without a regenerated record fails; Python test or an xUnit test that shells out — choose the one that fits the repo's CI (verify `.github/workflows`) | verify |
| CR-6 | The prose fields (purpose, rule rationale, triggers, limits) are drafted by the implementer from the plans / evidence and marked for the planner's review; the planner owns their final wording | proposed |

## Decisions after review (2026-10-07)

The planner adopts the review's twelve proposals (file table, four records, hash pins in a committed spec, cross-checks,
draft markers, form changes, generator design, CI job) and has already applied #12 (EV-15 / Findings 65 / Backlog:
135 / 135 informative configurations, pooled 0.303 %; checked by recomputation). The author answered the three
questions:

| ID | Decision | Source |
|---|---|---|
| CD-1 | **Four records**, chronological: CAL-01 gate thresholds v1 (superseded by CAL-02; its 3 FAIL rows shown), CAL-02 gate thresholds v2, CAL-03 stripped Z_s thresholds (csco v1), CAL-04 pair-test significance floors (angres v2). "Used by" lists VV IDs only (PR / EV / D) | review #1, #2 |
| CD-2 | **Hash pins in a committed spec** `samples/evidence/calibration-records.json` (files, SHA-256 of the git bytes, manifests, families); the generator refuses CR bytes, never calls git, stdlib only, UTF-8 / LF, byte-stable output; `--check` regenerates and compares; a new small ubuntu CI job runs it | review #3, #11 |
| CD-3 | **Cross-checks** in the generator: thresholds equal across files, seed lists equal the manifest slices, manifest → request → thresholds pins, stored verdicts and pooled sums recomputed; seed disjointness | review #4 |
| CD-4 | **Form changes** (the planner edits the template): generated sections contiguous (one marker pair per record); engine field admits "not recorded — code landed in <commit>"; "value fixed before validation" field; "not applicable — reason" verdict; interval method and pass criterion stated per record; "date measured (manifest)"; drafted prose visibly marked `*(draft — for review)*`; never print the files' `_about` / `Rule` strings (they carry plan IDs) | review #5 – #10 |
| CD-5 | **CAL-04 is judged by the one-sided upper limit (author):** a floor's validation row fails when the bootstrap upper 95 % limit of the single-source false split exceeds 5 % — the same standard as CAL-01 … 03. The 4 rows at 1 m above 5 % (worst cross-correlation, axis, Δ 2, 4000 counts, 1 : 1: 4.28 % [2.70, 6.23]) are FAIL. The implementer reports which resolved separations of EV-11 / PR-IMG-04 change under this standard (re-derived from the committed results, no new runs unless they cannot answer it) | **author** ("상한으로 판정"); review Q1 |
| CD-6 | **Between grid points, the more conservative neighbour (author):** a configuration that lies between validated grid points (ambient level, bound, exposure, total counts) uses the more conservative of the neighbouring calibrated values — the higher threshold / floor — and an unbracketed configuration (outside the grid) has no valid calibration. The rule is stated in every record's §6 / §8 and as a product rule | **author** ("보수적 이웃값 규칙"); review Q2 |
| CD-7 | **CAL-04 table:** the worst validation row per floor (54 rows) plus the FAIL rows; the full 414 rows stay in the JSON | **author** (recommended); review Q3 |
| CD-8 | **Engine commit of past runs:** "not recorded" plus the commit where the code landed; a new Todo item makes the run driver's `run-info.json` part of the committed summaries in future | review #7 |

## Steps

1. Review turn (no code): verify the file table and CR rows, inspect the three files' schemas, say what each record can
   and cannot fill from the files (e.g. engine commit of the runs), propose corrections →
   `docs/PLAN.Docs.CalibrationRecords.Review.md`.
2. Decisions after review; implement; the planner reviews the generated document and the prose.

## Done when

- `docs/VV.Gcam.Calibration.md` holds CAL-01 … CAL-03 in the template's form, tables generated, checks passing.
- `--check` mode exists and is exercised by a test or CI.
- PR-SENS-02, PR-NRG-04, PR-IMG-04 and the related EV entries cite their CAL record.

## Not here

- The rest of TODO-28 (documenting every test suite; generated test-result report from TRX) — next steps.
