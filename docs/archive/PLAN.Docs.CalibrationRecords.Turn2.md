# PLAN.Docs.CalibrationRecords.Turn2 — implementer's turn-2 report: generator, records, CI check

Scope: TODO-28 step 1, turn 2 (implementation) of [PLAN.Docs.CalibrationRecords](PLAN.Docs.CalibrationRecords.md),
"Decisions after review" CD-1 … CD-8. The work was done by the substitute implementer (a Claude subagent). No C# was
changed, and no git state was changed. Scratch files are under `%TEMP%\gcam-cal\`: the CD-5 script, the negative-test
script with its tree copy, and the test log.

Status: implemented, 2026-10-07. The planner still has to review the prose drafts (sections 1–5 of every record are
marked *(draft — for review)*) and apply the citation edits listed below.

## Files

| File | What |
|---|---|
| `samples/evidence/calibration_record.py` | new. The generator: stdlib only; adapters `gate` (v1 + v2), `csco`, `angres_floor`; all CD-3 cross-checks; refuses CR bytes and unpinned reads; never calls git; UTF-8 / LF; fixed number formats. Modes: write (default), `--check`, `--release` (as `--check` and refuses draft markers) |
| `samples/evidence/calibration-records.json` | new. The spec: 20 pinned files (SHA-256 of the LF bytes), four records (kind, manifest, selection / validation file and families, comparison, target, the commit where the code landed, reproduce commands). A pinned file that is never read fails the check |
| `docs/VV.Gcam.Calibration.md` | new. Header (scope, at a glance, regenerate), generated index, CAL-01 … CAL-04. Sections 1–5 are drafted; sections 6–8 are generated (922 lines in all) |
| `.github/workflows/ci.yml` | new job `calibration-records` (ubuntu, Python 3.13, `python samples/evidence/calibration_record.py --check`). The existing jobs are unchanged |
| `docs/PLAN.Docs.CalibrationRecords.Turn2.md` | this report |

## Results (generated, all cross-checks passing)

| Record | Values | Validation | Pooled | Worst |
|---|---|---|---|---|
| CAL-01 gate v1 (superseded) | 96 | 93 / 96, 3 FAIL | 603 / 196,608 = 0.307 % | 13 / 2,048, upper 1.007 % (3 configurations) |
| CAL-02 gate v2 | 96 | 96 / 96 | 2,424 / 786,432 = 0.308 % | lab \| 10 s \| 0.10 \| front-only \| cs662, 40 / 8,192, upper 0.635 % |
| CAL-03 csco v1 | 144 | 135 / 135, 9 not applicable (no counts) | 3,350 / 1,105,920 = 0.303 % (all rows 0.284 %) | sep5 \| 60 s \| 0.10 bare \| Co 1 MBq, 38 / 8,192, upper 0.608 % |
| CAL-04 angres v2 floors | 54 floors | 410 / 414 condition rows; 4 FAIL in 3 floors | 21,267 / 2,649,600 = 0.803 % | 1 m cc axis Δ 2, 4,000 / source, 1 : 1: 274 / 6,400 = 4.28 % [2.70, 6.23] |

**Negative tests** (on a copy of the needed tree under `%TEMP%\gcam-cal\neg`; each case exits 1 and writes nothing):

| Case | Result |
|---|---|
| a. one validation count changed, pin not updated | pin mismatch + PooledNulls mismatch |
| b. the same with the pin updated | PooledNulls mismatch |
| c. CRLF `seeds.json` | CR refused + pin mismatch |
| d. hand edit inside a generated block (`--check`) | "out of date", first differing line shown |
| e. validation seed offset moved to overlap (pin updated) | seed slice mismatch + overlap |
| f. one selection threshold changed (pin updated) | Selection / validation threshold mismatches + request pin mismatch |
| g. `--release` with draft prose | refused |

Unmodified copy: `--check` passes.

## CD-5 — impact on EV-11 / PR-IMG-04

Re-derived from `angres-v2-floor.json` (`Conditions`, the floor statistic). Old rule: D-48 as aggregated, pooled pass
≥ 95 % and pooled false split ≤ 5 % at Δ and every larger Δ. New rule: the same, with the false split judged by its
bootstrap upper limit ≤ 5 %. The old rule reproduces the file's stored `Resolved` separations in every cell (checked;
the generator also checks it).

**No resolved separation changes, for any decoder, ratio, placement, count level or distance.** The 4 FAIL rows all lie
in cells that resolve no separation under either rule:

| Distance | Placement | Counts per source | Ratio | Decoder | FAIL row | Resolved, old → new |
|---|---|---|---|---|---|---|
| 1 m | axis | 4,000 | 1 : 1 | cc | Δ 2: 4.28 % [2.70, 6.23] | not reached → not reached (best pass 92.5 %) |
| 1 m | axis | 16,000 | 1 : 4 | binary@8 | Δ 2: 3.41 % [1.11, 5.92]; Δ 2.5: 3.48 % [1.44, 5.97] | not reached → not reached (best 66.3 %) |
| 1 m | edge | 16,000 | 1 : 4 | binary@8 | Δ 2.5: 3.11 % [1.56, 5.14] | not reached → not reached (best 75.4 %) |

The upper false-split limit at and beyond each claimed separation, under the new rule, is as follows:
- pixel-area MLEM 1 : 1 on axis, 1.25 elements: ≤ 1.59 % (1,000 / 4,000 / 16,000 counts per source);
- 1 : 4 on axis, 1.5 elements: ≤ 3.73 %;
- edge, 1.5 / 1.75 elements: ≤ 4.05 %;
- 5 m, 1.75 elements: ≤ 3.45 %.

**One caveat for the planner.** The file stores the **two-sided** 95 % percentile interval (`boot_ci`: 2.5 % / 97.5 %
quantiles), so the "upper 95 % limit" used here is a one-sided **97.5 %** limit. That is slightly stricter than the
one-sided 95 % Clopper–Pearson limit of CAL-01 … 03. A one-sided 95 % bootstrap limit is not stored and would need the
runs. The edge row (upper 5.14 %) might pass under it. The record says this in section 1 and in the table notes. If the
author wants exactly one-sided 95 %, the aggregator would need re-running on the machine-local runs, which are not in
the repository.

## Proposed citation edits (for the planner; not applied)

1. **VV.Gcam.PRS PR-SENS-02** (evidence column `EV-34, EV-07`) → `EV-34, EV-07, CAL-02`. In the text, after "a
   threshold set per configuration …", add "(calibration record CAL-02)".
2. **PR-NRG-04** (evidence `EV-15`) → `EV-15, CAL-03, CAL-02`. After "a stripped trust statistic calibrated on Cs-free
   Co-60 nulls (D-44)", add "(CAL-03)". After "the raw 662 keV window would report Co-60 as a trusted Cs-137 source in
   92–100 % of acquisitions", add "(PR-SENS-02 thresholds, CAL-02)".
3. **PR-IMG-04** (evidence `EV-11`) → `EV-11, CAL-04`. After "Resolved as defined in D-48", add "(significance floor:
   CAL-04)". The resolved figures stay unchanged (CD-5 table).
4. **VV.Gcam.Evidence EV-34**, "The gate (PR-SENS-02)" bullet: after "pinned before validation", add "(calibration
   record CAL-02)". After "A first selection on 4,096 nulls … passed 93 / 96", add "(CAL-01, superseded)".
5. **EV-15**, the "*Imaging.*" bullet: after "thresholds selected per configuration on 65 536 Cs-free Co-60 + ambient
   nulls by the PR-SENS-02 rule", add "(CAL-03)".
6. **EV-11**, "Shows — at use distance": after "a significance floor calibrated on single-source acquisitions (≤ 3 %
   false split at selection, disjoint seeds)", add "(CAL-04)". Add to the bullet or to Limits: "Judged by the upper
   bootstrap limit (the upper end of the two-sided 95 % interval), 4 of 414 validation rows exceed 5 % false split, all
   in cells that resolve no separation (cross-correlation 1 : 1 at 4,000 counts; pixel-centre MLEM 1 : 4 at 16,000);
   no resolved separation changes."
7. **VV.Gcam.Decisions D-48** (planner's choice): note that the false-split limit is judged by the upper bootstrap
   limit (author, CD-5), with "no resolved separation changed".
8. **Index documents**: add `VV.Gcam.Calibration` and `VV.Gcam.Calibration.Template` to the companion-doc table in
   `AGENTS.md`, to the VV examples in `AGENTS.Conventions.Docs`, and to `samples/evidence/README.md` (the spec and the
   generator). Also add the new CI job wherever CI jobs are listed.

## Template

The form can be filled as written, with two deviations stated in the records:
- **Section 7, "the selection statistic at the value with its N":** CAL-04's file stores only F and N, so the record
  says "not recorded (the rule bounds it at ≤ α per separation)".
- **Section 6, "seed list and range":** the records print the slice, e.g. `F256[128:256]`, and the count. The lists
  themselves are in the pinned `seeds.json`.

The document adds a generated index block (`<!-- CAL index generated: … -->`) that the template does not mention.

## Verification

- `python samples/evidence/calibration_record.py` → "rewritten (4 records); all checks passed". Then `--check` →
  "4 records up to date; all checks passed", exit 0. This works with and without `-X utf8`: the console stream is
  switched to UTF-8.
- `python -W error -m py_compile samples/evidence/calibration_record.py` → clean.
- `DOTNET_CLI_UI_LANGUAGE=en dotnet test Gcam.sln -c Release` → exit 0. Results by assembly:
  - Gcam.Tests: 448 passed;
  - Gcam.Studio.Tests: 205 passed;
  - Gcam.Studio.Services.Tests: 92 passed, 7 skipped (opt-in);
  - Gcam.Studio.UiTests: 13 passed, 14 skipped (desktop).
- Not run: the workflow on GitHub; Python 3.13 (only 3.11 is on this machine; the output uses no version-dependent
  formatting).

## Commands that wrote anything

- The edit / write tools, on the five repository files above.
- Python one-off patch scripts (heredoc to stdin, nothing saved) that edited `samples/evidence/calibration_record.py`
  and `docs/VV.Gcam.Calibration.md`. A `sed -i` did the same on `calibration_record.py`.
- `python samples/evidence/calibration_record.py` (it rewrote `docs/VV.Gcam.Calibration.md`).
- Under `%TEMP%\gcam-cal\` only:
  - `mkdir -p /tmp/gcam-cal`, plus `cd5.py` and `negtest.sh`;
  - the tree copy `neg/`, which the negative tests edited and rewrote;
  - `test.txt`, the `dotnet test` log.
- `dotnet test` (build outputs under `bin/` and `obj/`).

APPROVAL REQUESTS: none.
