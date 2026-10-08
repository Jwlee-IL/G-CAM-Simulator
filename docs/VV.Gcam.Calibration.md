# VV.Gcam.Calibration — calibration records: values selected from data, how they were checked, when to redo them

Scope: every calibration a Gcam requirement relies on — a number selected from Monte Carlo data rather than derived
from physics (trust thresholds, significance floors) — recorded in the fixed form
[VV.Gcam.Calibration.Template](VV.Gcam.Calibration.Template.md). Not covered: the physics models' own validation (see
[VV.Gcam.Evidence](VV.Gcam.Evidence.md)) and the instrument calibrations a product would carry out on hardware.

**At a glance**
- Four records. CAL-01 is the first selection of PR-SENS-02's trust thresholds, superseded by CAL-02 and kept with its
  three FAIL rows. CAL-02 holds PR-SENS-02's thresholds, CAL-03 PR-NRG-04's stripped-image thresholds under Co-60, and
  CAL-04 PR-IMG-04's pair-test significance floors.
- Every value is selected on one set of seeds and validated on a disjoint set. Every rate is printed with its N, every
  file is pinned by SHA-256, and a failed row stays visible.
- Tables (sections 6–8) are generated from the committed evidence files. They are never typed by hand. Sections 1–5
  are written; drafts carry a review marker until the planner signs them off (all four signed off 2026-10-07).
- A value is valid only at its grid point. Between grid points the more conservative neighbouring value applies (the
  higher threshold or floor). Outside the grid there is no valid calibration.
- No record states the engine commit of its runs: it was not recorded at run time. Each record names the commit in
  which the runs' code was committed.
- None of these values is read by GCAM Studio. They apply to the evidence studies that produced them.

**Regenerate and check.** `python samples/evidence/calibration_record.py` rewrites the generated blocks from the files
pinned in `samples/evidence/calibration-records.json`. The generator refuses to write if any pin or cross-check fails.
`python samples/evidence/calibration_record.py --check` regenerates in memory and fails on any difference; CI runs it.
A changed evidence file therefore needs a deliberate pin update and a reviewed record.

<!-- CAL index generated: begin -->
| Record | Calibration | Selection file | Values | Validation rows passing | FAIL | Not applicable |
|---|---|---|---|---|---|---|
| CAL-01 | PR-SENS-02 trust thresholds, first selection (gate v1) | `gate-thresholds-v1.json` | 96 | 93 / 96 | 3 | 0 |
| CAL-02 | PR-SENS-02 trust thresholds (gate v2) | `gate-thresholds-v2.json` | 96 | 96 / 96 | 0 | 0 |
| CAL-03 | Stripped-image trust thresholds under Co-60 (csco v1) | `csco-thresholds-v1.json` | 144 | 135 / 135 | 0 | 9 |
| CAL-04 | Pair-test significance floors (angres v2) | `angres-v2-floor.json` | 54 | 410 / 414 | 4 | 0 |
<!-- CAL index generated: end -->

## CAL-01 — PR-SENS-02 trust thresholds, first selection (superseded)

| Field | Value |
|---|---|
| Record | CAL-01 |
| Used by | EV-34 (as history: "a first selection … passed 93 / 96") |
| Quantity | Threshold T on the search significance Z, the largest studentised correlation over the decoder grid (dimensionless; EV-34). A location is trusted when Z > T (strict). |
| Configuration key | case (lab geometry, hand-held head at its scenario distance, hand-held head at 1 m, at 5 m) × exposure (10, 60 s) × ambient field (0.05, 0.10, 0.20 µSv/h photon H*(10)) × bound (bare crystal, front only) × window (open, 662 keV) — 96 values |
| Status | superseded by CAL-02 — three configurations failed validation (see section 7); the selection was repeated with 16 × the nulls on the same seeds and validated on new seeds |

### 1. Purpose and acceptance
A background-only acquisition must give a trusted location in at most 1 % of acquisitions, for
each configuration separately. Pass criterion: the one-sided 95 % Clopper–Pearson upper limit of the false-trusted
rate, over the validation acquisitions of that configuration, is ≤ 1 %. Interval method: exact binomial
(Clopper–Pearson), one-sided, 95 %.

### 2. Selection rule
For each configuration, the maximum Z of every selection acquisition (background only) is
pooled, n = 64 seeds × 64 acquisitions = 4,096. With k = ⌊α n⌋ and α = 0.3 %, k = 12, and T is the (k + 1)-th largest
value, so at most 12 selection acquisitions exceed it. An acquisition without counts has no Z and never exceeds.

α sits well below the 1 % target so that the true rate at T stays below the target: with 12 allowed exceedances in
4,096, the one-sided 95 % upper limit of the true rate is 0.47 %. This record shows that the margin was too small for
the validation size. 2,048 validation nulls pass only with ≤ 12 false trusted, which a true 0.30 % rate exceeds with
probability 1.1 % per configuration (about 1 of 96 by chance) and a true 0.50 % rate with 23 %. Where the expected
background is below one count, Z takes few distinct values and the selected quantile moves in steps.

### 3. Use between and beyond the grid
Not for use: superseded by CAL-02. While it was valid, the rule was the one of CAL-02.

### 4. Re-calibration triggers
As CAL-02. This record was re-calibrated because its validation failed in three configurations
(by 0.007 percentage points each, 13 / 2,048), not because of a model change.

### 5. Known limits
As CAL-02. In addition: 4,096 selection nulls resolve the 0.3 % quantile only coarsely where Z
takes few values.

<!-- CAL-01 generated: begin -->
### 6. Data

| Item | Value |
|---|---|
| Date measured (manifest) | 2026-10-04 |
| Selection family | `gate_selection`: F128[1:65], 64 seeds × 64 background-only acquisitions = 4,096 per configuration |
| Validation family | `gate_validation`: O128[0:128], 128 seeds × 16 = 2,048 background-only acquisitions per configuration |
| Selection level α; comparison | 0.003; trusted when Z > T (the file names no comparison: strict) |
| Selection runs digest | `78968ce758dbd1e39062b61e404743c92cefc62a120822ce7f00b108d29ab18b` (SHA-256 over the per-seed run files; the runs are not committed) |
| Seed overlap between selection and validation | none (checked) |
| Value fixed before validation | yes — `gate_validation`'s overrides pin `samples/evidence/ambient/gate-thresholds-v1.json` by SHA-256, and the validation summary records the same hash |
| Engine commit of the runs | not recorded; the code landed in `75e7ce3` |

Files read (SHA-256 of the bytes git stores; pinned in `samples/evidence/calibration-records.json`):

| File | SHA-256 |
|---|---|
| `samples/ambient/terrestrial-unscear2000-v1.json` | `4d48bc4272ce4c25ff6ae9b13ed7583e3c8f467afffdb0225956a70cc435b3f4` |
| `samples/evidence/ambient/gate-request-v1.json` | `551fb0096025f5826f48e5227fc2ba00e9d575b9afd1262df2e8b16dc812225a` |
| `samples/evidence/ambient/gate-thresholds-v1.json` | `83b931ac8fc720fecd2133350ecf0106c0c2af00ef1f1806bb870fd9ee678105` |
| `samples/evidence/manifest-ambient-v1.json` | `655fefed68a903ed2292cb70d3d111759680aab89433d87c6557e948689f8ce8` |
| `samples/evidence/results/ambient-baseline-v1-turn7-gate.json` | `2854259ed2e3a45c90162308c03e8f1441037cf625afaf02afd2ada47a553b19` |
| `samples/evidence/seeds.json` | `f8c3b2c1385b6d11520abd7d224181ed79d5b0e2719de300d95341c77bd82583` |
| `samples/scenario.json` | `eb5b96835c0b68a88c5c26965540f85f1a313acf1cc0b49955cf7921463edddc` |
| `samples/scenario_handheld.json` | `f988784c8ee266e6dd28eb898cdbbfac0e2452cb01fd3929874fd866edc98795` |

### 7. Values and validation

**Summary.** 96 values; **93 / 96 judged configurations pass** (one-sided 95 % Clopper–Pearson upper limit of the false-trusted rate ≤ 1 %); 0 not applicable; 3 FAIL. Pooled over the judged configurations: 603 / 196,608 = 0.307 %. Worst: `head5m|t=10|F=0.1|FrontOnlyThroughMask|cs662`, 13 / 2,048, upper limit 1.007 % (3 configurations share this limit).

Columns: the threshold; the selection acquisitions at or above it (strict rule: above it); the expected background counts per validation acquisition; the validation false-trusted count and its one-sided 95 % Clopper–Pearson upper limit; the verdict against the target.

**lab** — lab geometry (24 configurations)

| Exposure | Field µSv/h | Bound | Window | Threshold T | Selection ≥ T (k / N) | Expected background counts | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| 10 s | 0.05 | bare | open | 4.4597 | 12 / 4,096 | 81.4 | 7 / 2,048 | 0.641 % | pass |
| 10 s | 0.05 | bare | cs662 | 2.3544 | 12 / 4,096 | 0.82 | 9 / 2,048 | 0.766 % | pass |
| 10 s | 0.05 | front-only | open | 1.6895 | 12 / 4,096 | 0.292 | 11 / 2,048 | 0.887 % | pass |
| 10 s | 0.05 | front-only | cs662 | 1.2755 | 12 / 4,096 | 0.0231 | 4 / 2,048 | 0.446 % | pass |
| 10 s | 0.10 | bare | open | 4.5174 | 12 / 4,096 | 163 | 4 / 2,048 | 0.446 % | pass |
| 10 s | 0.10 | bare | cs662 | 2.7508 | 12 / 4,096 | 1.64 | 3 / 2,048 | 0.378 % | pass |
| 10 s | 0.10 | front-only | open | 2.0329 | 12 / 4,096 | 0.583 | 4 / 2,048 | 0.446 % | pass |
| 10 s | 0.10 | front-only | cs662 | 1.2932 | 12 / 4,096 | 0.0461 | 6 / 2,048 | 0.577 % | pass |
| 10 s | 0.20 | bare | open | 4.5516 | 12 / 4,096 | 326 | 8 / 2,048 | 0.704 % | pass |
| 10 s | 0.20 | bare | cs662 | 3.1589 | 12 / 4,096 | 3.28 | 5 / 2,048 | 0.513 % | pass |
| 10 s | 0.20 | front-only | open | 2.5568 | 12 / 4,096 | 1.17 | 4 / 2,048 | 0.446 % | pass |
| 10 s | 0.20 | front-only | cs662 | 1.3311 | 12 / 4,096 | 0.0922 | 5 / 2,048 | 0.513 % | pass |
| 60 s | 0.05 | bare | open | 4.6272 | 12 / 4,096 | 488 | 3 / 2,048 | 0.378 % | pass |
| 60 s | 0.05 | bare | cs662 | 3.3831 | 12 / 4,096 | 4.92 | 7 / 2,048 | 0.641 % | pass |
| 60 s | 0.05 | front-only | open | 2.7273 | 12 / 4,096 | 1.75 | 3 / 2,048 | 0.378 % | pass |
| 60 s | 0.05 | front-only | cs662 | 1.7473 | 12 / 4,096 | 0.138 | 8 / 2,048 | 0.704 % | pass |
| 60 s | 0.10 | bare | open | 4.5247 | 12 / 4,096 | 977 | 5 / 2,048 | 0.513 % | pass |
| 60 s | 0.10 | bare | cs662 | 3.8403 | 12 / 4,096 | 9.84 | 6 / 2,048 | 0.577 % | pass |
| 60 s | 0.10 | front-only | open | 3.0777 | 12 / 4,096 | 3.5 | 11 / 2,048 | 0.887 % | pass |
| 60 s | 0.10 | front-only | cs662 | 2.0598 | 12 / 4,096 | 0.277 | 4 / 2,048 | 0.446 % | pass |
| 60 s | 0.20 | bare | open | 4.5612 | 12 / 4,096 | 1,953 | 4 / 2,048 | 0.446 % | pass |
| 60 s | 0.20 | bare | cs662 | 4.2049 | 12 / 4,096 | 19.7 | 4 / 2,048 | 0.446 % | pass |
| 60 s | 0.20 | front-only | open | 3.5675 | 12 / 4,096 | 7 | 6 / 2,048 | 0.577 % | pass |
| 60 s | 0.20 | front-only | cs662 | 2.1981 | 12 / 4,096 | 0.553 | 3 / 2,048 | 0.378 % | pass |

**head** — hand-held head, scenario distance (24 configurations)

| Exposure | Field µSv/h | Bound | Window | Threshold T | Selection ≥ T (k / N) | Expected background counts | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| 10 s | 0.05 | bare | open | 4.4937 | 12 / 4,096 | 168 | 5 / 2,048 | 0.513 % | pass |
| 10 s | 0.05 | bare | cs662 | 2.9734 | 12 / 4,096 | 2.17 | 1 / 2,048 | 0.231 % | pass |
| 10 s | 0.05 | front-only | open | 2.1581 | 12 / 4,096 | 0.644 | 12 / 2,048 | 0.948 % | pass |
| 10 s | 0.05 | front-only | cs662 | 1.3531 | 11 / 4,096 | 0.0501 | 1 / 2,048 | 0.231 % | pass |
| 10 s | 0.10 | bare | open | 4.5073 | 12 / 4,096 | 335 | 9 / 2,048 | 0.766 % | pass |
| 10 s | 0.10 | bare | cs662 | 3.3861 | 12 / 4,096 | 4.35 | 6 / 2,048 | 0.577 % | pass |
| 10 s | 0.10 | front-only | open | 2.6452 | 12 / 4,096 | 1.29 | 3 / 2,048 | 0.378 % | pass |
| 10 s | 0.10 | front-only | cs662 | 1.7285 | 12 / 4,096 | 0.1 | 10 / 2,048 | 0.827 % | pass |
| 10 s | 0.20 | bare | open | 4.5847 | 12 / 4,096 | 670 | 7 / 2,048 | 0.641 % | pass |
| 10 s | 0.20 | bare | cs662 | 3.7919 | 12 / 4,096 | 8.69 | 10 / 2,048 | 0.827 % | pass |
| 10 s | 0.20 | front-only | open | 3.0664 | 12 / 4,096 | 2.57 | 2 / 2,048 | 0.307 % | pass |
| 10 s | 0.20 | front-only | cs662 | 1.8309 | 12 / 4,096 | 0.2 | 7 / 2,048 | 0.641 % | pass |
| 60 s | 0.05 | bare | open | 4.6202 | 12 / 4,096 | 1,005 | 10 / 2,048 | 0.827 % | pass |
| 60 s | 0.05 | bare | cs662 | 3.9561 | 12 / 4,096 | 13 | 12 / 2,048 | 0.948 % | pass |
| 60 s | 0.05 | front-only | open | 3.2430 | 12 / 4,096 | 3.86 | 1 / 2,048 | 0.231 % | pass |
| 60 s | 0.05 | front-only | cs662 | 1.8576 | 12 / 4,096 | 0.301 | 9 / 2,048 | 0.766 % | pass |
| 60 s | 0.10 | bare | open | 4.5073 | 12 / 4,096 | 2,010 | 11 / 2,048 | 0.887 % | pass |
| 60 s | 0.10 | bare | cs662 | 4.2728 | 12 / 4,096 | 26.1 | 11 / 2,048 | 0.887 % | pass |
| 60 s | 0.10 | front-only | open | 3.6338 | 12 / 4,096 | 7.72 | 12 / 2,048 | 0.948 % | pass |
| 60 s | 0.10 | front-only | cs662 | 2.3551 | 12 / 4,096 | 0.601 | 4 / 2,048 | 0.446 % | pass |
| 60 s | 0.20 | bare | open | 4.5873 | 12 / 4,096 | 4,021 | 9 / 2,048 | 0.766 % | pass |
| 60 s | 0.20 | bare | cs662 | 4.5599 | 12 / 4,096 | 52.2 | 2 / 2,048 | 0.307 % | pass |
| 60 s | 0.20 | front-only | open | 4.0921 | 12 / 4,096 | 15.4 | 11 / 2,048 | 0.887 % | pass |
| 60 s | 0.20 | front-only | cs662 | 2.6534 | 12 / 4,096 | 1.2 | 7 / 2,048 | 0.641 % | pass |

**head1m** — hand-held head, 1 m (24 configurations)

| Exposure | Field µSv/h | Bound | Window | Threshold T | Selection ≥ T (k / N) | Expected background counts | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| 10 s | 0.05 | bare | open | 4.5055 | 12 / 4,096 | 168 | 10 / 2,048 | 0.827 % | pass |
| 10 s | 0.05 | bare | cs662 | 2.7462 | 12 / 4,096 | 2.17 | 5 / 2,048 | 0.513 % | pass |
| 10 s | 0.05 | front-only | open | 2.1301 | 12 / 4,096 | 0.644 | 4 / 2,048 | 0.446 % | pass |
| 10 s | 0.05 | front-only | cs662 | 1.2101 | 12 / 4,096 | 0.0501 | 3 / 2,048 | 0.378 % | pass |
| 10 s | 0.10 | bare | open | 4.5195 | 12 / 4,096 | 335 | 8 / 2,048 | 0.704 % | pass |
| 10 s | 0.10 | bare | cs662 | 3.2393 | 12 / 4,096 | 4.35 | 3 / 2,048 | 0.378 % | pass |
| 10 s | 0.10 | front-only | open | 2.4103 | 12 / 4,096 | 1.29 | 10 / 2,048 | 0.827 % | pass |
| 10 s | 0.10 | front-only | cs662 | 1.6033 | 12 / 4,096 | 0.1 | 7 / 2,048 | 0.641 % | pass |
| 10 s | 0.20 | bare | open | 4.5249 | 12 / 4,096 | 670 | 5 / 2,048 | 0.513 % | pass |
| 10 s | 0.20 | bare | cs662 | 3.6285 | 12 / 4,096 | 8.69 | 6 / 2,048 | 0.577 % | pass |
| 10 s | 0.20 | front-only | open | 2.8433 | 12 / 4,096 | 2.57 | 6 / 2,048 | 0.577 % | pass |
| 10 s | 0.20 | front-only | cs662 | 1.6366 | 12 / 4,096 | 0.2 | 6 / 2,048 | 0.577 % | pass |
| 60 s | 0.05 | bare | open | 4.5718 | 12 / 4,096 | 1,005 | 5 / 2,048 | 0.513 % | pass |
| 60 s | 0.05 | bare | cs662 | 3.9071 | 12 / 4,096 | 13 | 5 / 2,048 | 0.513 % | pass |
| 60 s | 0.05 | front-only | open | 3.0894 | 12 / 4,096 | 3.86 | 9 / 2,048 | 0.766 % | pass |
| 60 s | 0.05 | front-only | cs662 | 1.6781 | 11 / 4,096 | 0.301 | 13 / 2,048 | 1.007 % | **FAIL** |
| 60 s | 0.10 | bare | open | 4.6432 | 12 / 4,096 | 2,010 | 5 / 2,048 | 0.513 % | pass |
| 60 s | 0.10 | bare | cs662 | 4.2720 | 12 / 4,096 | 26.1 | 3 / 2,048 | 0.378 % | pass |
| 60 s | 0.10 | front-only | open | 3.5810 | 12 / 4,096 | 7.72 | 8 / 2,048 | 0.704 % | pass |
| 60 s | 0.10 | front-only | cs662 | 2.1125 | 12 / 4,096 | 0.601 | 9 / 2,048 | 0.766 % | pass |
| 60 s | 0.20 | bare | open | 4.5471 | 12 / 4,096 | 4,021 | 6 / 2,048 | 0.577 % | pass |
| 60 s | 0.20 | bare | cs662 | 4.5196 | 12 / 4,096 | 52.2 | 4 / 2,048 | 0.446 % | pass |
| 60 s | 0.20 | front-only | open | 4.0649 | 12 / 4,096 | 15.4 | 5 / 2,048 | 0.513 % | pass |
| 60 s | 0.20 | front-only | cs662 | 2.5390 | 12 / 4,096 | 1.2 | 3 / 2,048 | 0.378 % | pass |

**head5m** — hand-held head, 5 m (24 configurations)

| Exposure | Field µSv/h | Bound | Window | Threshold T | Selection ≥ T (k / N) | Expected background counts | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| 10 s | 0.05 | bare | open | 4.1324 | 12 / 4,096 | 168 | 5 / 2,048 | 0.513 % | pass |
| 10 s | 0.05 | bare | cs662 | 2.6182 | 12 / 4,096 | 2.17 | 2 / 2,048 | 0.307 % | pass |
| 10 s | 0.05 | front-only | open | 1.8649 | 12 / 4,096 | 0.644 | 13 / 2,048 | 1.007 % | **FAIL** |
| 10 s | 0.05 | front-only | cs662 | 1.1606 | 12 / 4,096 | 0.0501 | 7 / 2,048 | 0.641 % | pass |
| 10 s | 0.10 | bare | open | 4.0596 | 12 / 4,096 | 335 | 5 / 2,048 | 0.513 % | pass |
| 10 s | 0.10 | bare | cs662 | 3.0127 | 12 / 4,096 | 4.35 | 7 / 2,048 | 0.641 % | pass |
| 10 s | 0.10 | front-only | open | 2.3578 | 12 / 4,096 | 1.29 | 2 / 2,048 | 0.307 % | pass |
| 10 s | 0.10 | front-only | cs662 | 1.5171 | 12 / 4,096 | 0.1 | 13 / 2,048 | 1.007 % | **FAIL** |
| 10 s | 0.20 | bare | open | 4.1198 | 12 / 4,096 | 670 | 7 / 2,048 | 0.641 % | pass |
| 10 s | 0.20 | bare | cs662 | 3.4418 | 12 / 4,096 | 8.69 | 7 / 2,048 | 0.641 % | pass |
| 10 s | 0.20 | front-only | open | 2.6998 | 12 / 4,096 | 2.57 | 5 / 2,048 | 0.513 % | pass |
| 10 s | 0.20 | front-only | cs662 | 1.5862 | 12 / 4,096 | 0.2 | 8 / 2,048 | 0.704 % | pass |
| 60 s | 0.05 | bare | open | 4.1819 | 12 / 4,096 | 1,005 | 6 / 2,048 | 0.577 % | pass |
| 60 s | 0.05 | bare | cs662 | 3.5864 | 12 / 4,096 | 13 | 10 / 2,048 | 0.827 % | pass |
| 60 s | 0.05 | front-only | open | 2.9242 | 12 / 4,096 | 3.86 | 7 / 2,048 | 0.641 % | pass |
| 60 s | 0.05 | front-only | cs662 | 1.9040 | 12 / 4,096 | 0.301 | 2 / 2,048 | 0.307 % | pass |
| 60 s | 0.10 | bare | open | 4.1631 | 12 / 4,096 | 2,010 | 6 / 2,048 | 0.577 % | pass |
| 60 s | 0.10 | bare | cs662 | 3.9811 | 12 / 4,096 | 26.1 | 3 / 2,048 | 0.378 % | pass |
| 60 s | 0.10 | front-only | open | 3.3602 | 12 / 4,096 | 7.72 | 5 / 2,048 | 0.513 % | pass |
| 60 s | 0.10 | front-only | cs662 | 2.0513 | 12 / 4,096 | 0.601 | 6 / 2,048 | 0.577 % | pass |
| 60 s | 0.20 | bare | open | 4.2387 | 12 / 4,096 | 4,021 | 6 / 2,048 | 0.577 % | pass |
| 60 s | 0.20 | bare | cs662 | 4.0309 | 12 / 4,096 | 52.2 | 5 / 2,048 | 0.513 % | pass |
| 60 s | 0.20 | front-only | open | 3.7181 | 12 / 4,096 | 15.4 | 12 / 2,048 | 0.948 % | pass |
| 60 s | 0.20 | front-only | cs662 | 2.4247 | 12 / 4,096 | 1.2 | 0 / 2,048 | 0.146 % | pass |

### 8. Reproduce

```bash
dotnet build Gcam.sln -c Release
python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-ambient-v1.json --out <dir> --family gate_selection
python samples/evidence/ambient/select_thresholds.py --runs <dir> --manifest samples/evidence/manifest-ambient-v1.json --family gate_selection --rule strict --out <thresholds.json>   # = gate-thresholds-v1.json
python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-ambient-v1.json --out <dir> --family gate_validation
python samples/evidence/ambient/aggregate_gate.py --runs <dir> --manifest samples/evidence/manifest-ambient-v1.json --family gate_validation --out <gate.json>   # = ambient-baseline-v1-turn7-gate.json
python samples/evidence/calibration_record.py            # rewrites this block
python samples/evidence/calibration_record.py --check    # verifies it
```
<!-- CAL-01 generated: end -->

## CAL-02 — PR-SENS-02 trust thresholds

| Field | Value |
|---|---|
| Record | CAL-02 |
| Used by | PR-SENS-02, EV-34; PR-NRG-04 and EV-15 (the raw 662 keV window counter-case under Co-60 uses these thresholds) |
| Quantity | Threshold T on the search significance Z, the largest studentised correlation over the decoder grid, recorded to 4 decimals (dimensionless; EV-34). A location is trusted when the recorded Z ≥ T. |
| Configuration key | case (lab geometry, hand-held head at its scenario distance, hand-held head at 1 m, at 5 m) × exposure (10, 60 s) × ambient field (0.05, 0.10, 0.20 µSv/h photon H*(10)) × bound (bare crystal, front only) × window (open, 662 keV) — 96 values |
| Status | valid |

### 1. Purpose and acceptance
A background-only acquisition must give a trusted location in at most 1 % of acquisitions, for
each configuration separately (PR-SENS-02). Pass criterion: the one-sided 95 % Clopper–Pearson upper limit of the
false-trusted rate, over the validation acquisitions of that configuration, is ≤ 1 %. Interval method: exact binomial
(Clopper–Pearson), one-sided, 95 %.

### 2. Selection rule
For each configuration, the maximum Z of every selection acquisition (background only) is
recorded to 4 decimals and pooled: n = 64 seeds × 1,024 acquisitions = 65,536. With k = ⌊α n⌋ and α = 0.3 %, k = 196.
T is the smallest recorded value v with at most 196 acquisitions at Z ≥ v, so the selection exceedance share is ≤ α
**including ties** (ties count as trusted). If the largest value alone occurs more than k times, T is one recording
step (10⁻⁴) above it. An acquisition without counts has no Z and is never trusted. The first 64 acquisitions per seed
are CAL-01's selection, continued.

Why α = 0.3 % against a 1 % target:
- At T, 196 exceedances in 65,536 bound the true rate at 0.337 % (one-sided 95 % upper limit).
- 8,192 validation nulls pass with up to 66 false trusted. A true rate of 0.30 / 0.35 / 0.50 % fails with probability
  1.2 × 10⁻¹² / 6.8 × 10⁻¹⁰ / 1.1 × 10⁻⁴ per configuration (binomial; the per-seed counts' dispersion index was 0.78–2.37,
  which widens these but keeps them negligible).

### 3. Use between and beyond the grid
Valid at the 96 grid points only. Between grid points the more conservative neighbour applies,
here the higher T among the neighbouring configurations:
- an ambient level between 0.05 and 0.10 µSv/h uses the higher of the two thresholds;
- an exposure between 10 and 60 s, likewise;
- an instrument housing between the two bounds (a real housing lies between bare crystal and front only) uses the
  higher of the bare and front-only thresholds.

There is no valid calibration outside the grid: below 0.05 or above 0.20 µSv/h, outside 10–60 s, other distances
than the four cases, or other windows. The universal value (the largest T over the grid) is not a calibration either;
it is printed in the selection file for comparison only.

### 4. Re-calibration triggers
Any change to the decoder or its grid; the energy windows or the energy resolution; the head
geometry (mask, distance, pixel array); the instrument's background-shape model; the ambient spectrum file or its
transport; the statistic (studentisation, recording precision, tie rule); or the field grid. Such a change marks this
record superseded and needs a new selection and validation.

### 5. Known limits
The calibration applies to:
- background-only nulls from a terrestrial ambient field (the soil chain only: no cosmic component, radon, room
  scatter or intrinsic crystal activity);
- two bounds that bracket an unknown housing, not a housing model;
- Poisson draws from expected maps rather than event-by-event transport.

It is not valid under Co-60 or other strong sources: the raw 662 keV window would trust Co-60's downscatter, which is
why CAL-03 exists. It says nothing about locating a source correctly; that is the count gate of PR-SENS-02 / EV-34.

<!-- CAL-02 generated: begin -->
### 6. Data

| Item | Value |
|---|---|
| Date measured (manifest) | 2026-10-04 |
| Selection family | `gate_selection_v2`: F128[1:65], 64 seeds × 1,024 background-only acquisitions = 65,536 per configuration |
| Validation family | `gate_validation_v2`: F256[128:256], 128 seeds × 64 = 8,192 background-only acquisitions per configuration |
| Selection level α; comparison | 0.003; trusted when the recorded Z (4 decimals) ≥ T, ties count as exceedances |
| Selection runs digest | `3c492e5b3ea2f6158b51f81bb4ad862612e2d9818e49565ea875ea2049215764` (SHA-256 over the per-seed run files; the runs are not committed) |
| Seed overlap between selection and validation | none (checked) |
| Value fixed before validation | yes — `gate_validation_v2`'s overrides pin `samples/evidence/ambient/gate-thresholds-v2.json` by SHA-256, and the validation summary records the same hash |
| Engine commit of the runs | not recorded; the code landed in `aed4b54` |

Files read (SHA-256 of the bytes git stores; pinned in `samples/evidence/calibration-records.json`):

| File | SHA-256 |
|---|---|
| `samples/ambient/terrestrial-unscear2000-v1.json` | `4d48bc4272ce4c25ff6ae9b13ed7583e3c8f467afffdb0225956a70cc435b3f4` |
| `samples/evidence/ambient/gate-request-v1.json` | `551fb0096025f5826f48e5227fc2ba00e9d575b9afd1262df2e8b16dc812225a` |
| `samples/evidence/ambient/gate-thresholds-v2.json` | `6352b2f692894fde1d83c6ef2c3ac2c04ee1b332c511e150d66342a516b6cdd0` |
| `samples/evidence/manifest-ambient-v2.json` | `5d546bb4c0ec51a50f1cbad6b719ff50cead20b64666625ba101d3773c542db2` |
| `samples/evidence/results/ambient-baseline-v1-turn8-gate.json` | `d2d55d5ec8c1bede10d601ac4fe3f9052c29691314612ffea54e91dda6ffcf51` |
| `samples/evidence/seeds.json` | `f8c3b2c1385b6d11520abd7d224181ed79d5b0e2719de300d95341c77bd82583` |
| `samples/scenario.json` | `eb5b96835c0b68a88c5c26965540f85f1a313acf1cc0b49955cf7921463edddc` |
| `samples/scenario_handheld.json` | `f988784c8ee266e6dd28eb898cdbbfac0e2452cb01fd3929874fd866edc98795` |

### 7. Values and validation

**Summary.** 96 values; **96 / 96 judged configurations pass** (one-sided 95 % Clopper–Pearson upper limit of the false-trusted rate ≤ 1 %); 0 not applicable; 0 FAIL. Pooled over the judged configurations: 2,424 / 786,432 = 0.308 %. Worst: `lab|t=10|F=0.1|FrontOnlyThroughMask|cs662`, 40 / 8,192, upper limit 0.635 %.

Columns: the threshold; the selection acquisitions at or above it (strict rule: above it); the expected background counts per validation acquisition; the validation false-trusted count and its one-sided 95 % Clopper–Pearson upper limit; the verdict against the target.

**lab** — lab geometry (24 configurations)

| Exposure | Field µSv/h | Bound | Window | Threshold T | Selection ≥ T (k / N) | Expected background counts | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| 10 s | 0.05 | bare | open | 4.4543 | 196 / 65,536 | 81.4 | 20 / 8,192 | 0.355 % | pass |
| 10 s | 0.05 | bare | cs662 | 2.3704 | 196 / 65,536 | 0.844 | 18 / 8,192 | 0.326 % | pass |
| 10 s | 0.05 | front-only | open | 1.6960 | 192 / 65,536 | 0.288 | 29 / 8,192 | 0.482 % | pass |
| 10 s | 0.05 | front-only | cs662 | 1.2800 | 189 / 65,536 | 0.0219 | 20 / 8,192 | 0.355 % | pass |
| 10 s | 0.10 | bare | open | 4.4926 | 196 / 65,536 | 163 | 32 / 8,192 | 0.524 % | pass |
| 10 s | 0.10 | bare | cs662 | 2.7183 | 196 / 65,536 | 1.69 | 12 / 8,192 | 0.237 % | pass |
| 10 s | 0.10 | front-only | open | 2.0487 | 196 / 65,536 | 0.576 | 27 / 8,192 | 0.454 % | pass |
| 10 s | 0.10 | front-only | cs662 | 1.2954 | 193 / 65,536 | 0.0438 | 40 / 8,192 | 0.635 % | pass |
| 10 s | 0.20 | bare | open | 4.5079 | 196 / 65,536 | 326 | 23 / 8,192 | 0.398 % | pass |
| 10 s | 0.20 | bare | cs662 | 3.1414 | 196 / 65,536 | 3.38 | 31 / 8,192 | 0.510 % | pass |
| 10 s | 0.20 | front-only | open | 2.5179 | 196 / 65,536 | 1.15 | 32 / 8,192 | 0.524 % | pass |
| 10 s | 0.20 | front-only | cs662 | 1.6776 | 195 / 65,536 | 0.0876 | 24 / 8,192 | 0.412 % | pass |
| 60 s | 0.05 | bare | open | 4.5193 | 196 / 65,536 | 488 | 22 / 8,192 | 0.383 % | pass |
| 60 s | 0.05 | bare | cs662 | 3.4218 | 196 / 65,536 | 5.06 | 21 / 8,192 | 0.369 % | pass |
| 60 s | 0.05 | front-only | open | 2.7291 | 196 / 65,536 | 1.73 | 26 / 8,192 | 0.440 % | pass |
| 60 s | 0.05 | front-only | cs662 | 1.7428 | 196 / 65,536 | 0.131 | 25 / 8,192 | 0.426 % | pass |
| 60 s | 0.10 | bare | open | 4.5152 | 196 / 65,536 | 977 | 20 / 8,192 | 0.355 % | pass |
| 60 s | 0.10 | bare | cs662 | 3.8370 | 196 / 65,536 | 10.1 | 36 / 8,192 | 0.580 % | pass |
| 60 s | 0.10 | front-only | open | 3.1345 | 196 / 65,536 | 3.46 | 26 / 8,192 | 0.440 % | pass |
| 60 s | 0.10 | front-only | cs662 | 1.8670 | 193 / 65,536 | 0.263 | 16 / 8,192 | 0.296 % | pass |
| 60 s | 0.20 | bare | open | 4.5608 | 196 / 65,536 | 1,954 | 23 / 8,192 | 0.398 % | pass |
| 60 s | 0.20 | bare | cs662 | 4.1962 | 196 / 65,536 | 20.3 | 17 / 8,192 | 0.311 % | pass |
| 60 s | 0.20 | front-only | open | 3.5874 | 196 / 65,536 | 6.92 | 26 / 8,192 | 0.440 % | pass |
| 60 s | 0.20 | front-only | cs662 | 2.1894 | 195 / 65,536 | 0.526 | 31 / 8,192 | 0.510 % | pass |

**head** — hand-held head, scenario distance (24 configurations)

| Exposure | Field µSv/h | Bound | Window | Threshold T | Selection ≥ T (k / N) | Expected background counts | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| 10 s | 0.05 | bare | open | 4.5272 | 196 / 65,536 | 168 | 26 / 8,192 | 0.440 % | pass |
| 10 s | 0.05 | bare | cs662 | 2.9422 | 195 / 65,536 | 2.19 | 22 / 8,192 | 0.383 % | pass |
| 10 s | 0.05 | front-only | open | 2.2722 | 196 / 65,536 | 0.648 | 29 / 8,192 | 0.482 % | pass |
| 10 s | 0.05 | front-only | cs662 | 1.3298 | 191 / 65,536 | 0.0535 | 17 / 8,192 | 0.311 % | pass |
| 10 s | 0.10 | bare | open | 4.5644 | 196 / 65,536 | 336 | 20 / 8,192 | 0.355 % | pass |
| 10 s | 0.10 | bare | cs662 | 3.3701 | 196 / 65,536 | 4.37 | 20 / 8,192 | 0.355 % | pass |
| 10 s | 0.10 | front-only | open | 2.6218 | 196 / 65,536 | 1.3 | 25 / 8,192 | 0.426 % | pass |
| 10 s | 0.10 | front-only | cs662 | 1.7270 | 196 / 65,536 | 0.107 | 26 / 8,192 | 0.440 % | pass |
| 10 s | 0.20 | bare | open | 4.5800 | 196 / 65,536 | 671 | 17 / 8,192 | 0.311 % | pass |
| 10 s | 0.20 | bare | cs662 | 3.7946 | 196 / 65,536 | 8.75 | 22 / 8,192 | 0.383 % | pass |
| 10 s | 0.20 | front-only | open | 2.9919 | 196 / 65,536 | 2.59 | 25 / 8,192 | 0.426 % | pass |
| 10 s | 0.20 | front-only | cs662 | 1.8125 | 196 / 65,536 | 0.214 | 37 / 8,192 | 0.594 % | pass |
| 60 s | 0.05 | bare | open | 4.5594 | 196 / 65,536 | 1,007 | 31 / 8,192 | 0.510 % | pass |
| 60 s | 0.05 | bare | cs662 | 4.0223 | 196 / 65,536 | 13.1 | 21 / 8,192 | 0.369 % | pass |
| 60 s | 0.05 | front-only | open | 3.2514 | 196 / 65,536 | 3.89 | 29 / 8,192 | 0.482 % | pass |
| 60 s | 0.05 | front-only | cs662 | 2.0306 | 196 / 65,536 | 0.321 | 25 / 8,192 | 0.426 % | pass |
| 60 s | 0.10 | bare | open | 4.5741 | 195 / 65,536 | 2,014 | 20 / 8,192 | 0.355 % | pass |
| 60 s | 0.10 | bare | cs662 | 4.3405 | 196 / 65,536 | 26.2 | 17 / 8,192 | 0.311 % | pass |
| 60 s | 0.10 | front-only | open | 3.6891 | 196 / 65,536 | 7.78 | 39 / 8,192 | 0.621 % | pass |
| 60 s | 0.10 | front-only | cs662 | 2.2885 | 196 / 65,536 | 0.642 | 32 / 8,192 | 0.524 % | pass |
| 60 s | 0.20 | bare | open | 4.6063 | 196 / 65,536 | 4,028 | 20 / 8,192 | 0.355 % | pass |
| 60 s | 0.20 | bare | cs662 | 4.4943 | 196 / 65,536 | 52.5 | 25 / 8,192 | 0.426 % | pass |
| 60 s | 0.20 | front-only | open | 4.0891 | 196 / 65,536 | 15.6 | 22 / 8,192 | 0.383 % | pass |
| 60 s | 0.20 | front-only | cs662 | 2.6441 | 196 / 65,536 | 1.28 | 25 / 8,192 | 0.426 % | pass |

**head1m** — hand-held head, 1 m (24 configurations)

| Exposure | Field µSv/h | Bound | Window | Threshold T | Selection ≥ T (k / N) | Expected background counts | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| 10 s | 0.05 | bare | open | 4.5011 | 196 / 65,536 | 168 | 27 / 8,192 | 0.454 % | pass |
| 10 s | 0.05 | bare | cs662 | 2.7678 | 195 / 65,536 | 2.19 | 33 / 8,192 | 0.538 % | pass |
| 10 s | 0.05 | front-only | open | 2.1232 | 196 / 65,536 | 0.648 | 28 / 8,192 | 0.468 % | pass |
| 10 s | 0.05 | front-only | cs662 | 1.1963 | 183 / 65,536 | 0.0535 | 21 / 8,192 | 0.369 % | pass |
| 10 s | 0.10 | bare | open | 4.5289 | 196 / 65,536 | 336 | 23 / 8,192 | 0.398 % | pass |
| 10 s | 0.10 | bare | cs662 | 3.1891 | 196 / 65,536 | 4.37 | 26 / 8,192 | 0.440 % | pass |
| 10 s | 0.10 | front-only | open | 2.4181 | 196 / 65,536 | 1.3 | 22 / 8,192 | 0.383 % | pass |
| 10 s | 0.10 | front-only | cs662 | 1.5992 | 195 / 65,536 | 0.107 | 25 / 8,192 | 0.426 % | pass |
| 10 s | 0.20 | bare | open | 4.5622 | 196 / 65,536 | 671 | 27 / 8,192 | 0.454 % | pass |
| 10 s | 0.20 | bare | cs662 | 3.6628 | 196 / 65,536 | 8.75 | 19 / 8,192 | 0.340 % | pass |
| 10 s | 0.20 | front-only | open | 2.8436 | 195 / 65,536 | 2.59 | 26 / 8,192 | 0.440 % | pass |
| 10 s | 0.20 | front-only | cs662 | 1.6519 | 188 / 65,536 | 0.214 | 27 / 8,192 | 0.454 % | pass |
| 60 s | 0.05 | bare | open | 4.5481 | 196 / 65,536 | 1,007 | 24 / 8,192 | 0.412 % | pass |
| 60 s | 0.05 | bare | cs662 | 3.9542 | 196 / 65,536 | 13.1 | 18 / 8,192 | 0.326 % | pass |
| 60 s | 0.05 | front-only | open | 3.1067 | 196 / 65,536 | 3.89 | 38 / 8,192 | 0.608 % | pass |
| 60 s | 0.05 | front-only | cs662 | 1.9154 | 196 / 65,536 | 0.321 | 17 / 8,192 | 0.311 % | pass |
| 60 s | 0.10 | bare | open | 4.5572 | 196 / 65,536 | 2,014 | 26 / 8,192 | 0.440 % | pass |
| 60 s | 0.10 | bare | cs662 | 4.2688 | 196 / 65,536 | 26.2 | 32 / 8,192 | 0.524 % | pass |
| 60 s | 0.10 | front-only | open | 3.5839 | 196 / 65,536 | 7.78 | 26 / 8,192 | 0.440 % | pass |
| 60 s | 0.10 | front-only | cs662 | 2.1652 | 196 / 65,536 | 0.642 | 24 / 8,192 | 0.412 % | pass |
| 60 s | 0.20 | bare | open | 4.6245 | 196 / 65,536 | 4,028 | 28 / 8,192 | 0.468 % | pass |
| 60 s | 0.20 | bare | cs662 | 4.4635 | 196 / 65,536 | 52.5 | 30 / 8,192 | 0.496 % | pass |
| 60 s | 0.20 | front-only | open | 4.0034 | 196 / 65,536 | 15.6 | 31 / 8,192 | 0.510 % | pass |
| 60 s | 0.20 | front-only | cs662 | 2.5051 | 196 / 65,536 | 1.28 | 18 / 8,192 | 0.326 % | pass |

**head5m** — hand-held head, 5 m (24 configurations)

| Exposure | Field µSv/h | Bound | Window | Threshold T | Selection ≥ T (k / N) | Expected background counts | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| 10 s | 0.05 | bare | open | 4.1014 | 196 / 65,536 | 168 | 23 / 8,192 | 0.398 % | pass |
| 10 s | 0.05 | bare | cs662 | 2.6069 | 196 / 65,536 | 2.19 | 26 / 8,192 | 0.440 % | pass |
| 10 s | 0.05 | front-only | open | 2.0421 | 196 / 65,536 | 0.648 | 28 / 8,192 | 0.468 % | pass |
| 10 s | 0.05 | front-only | cs662 | 1.1630 | 189 / 65,536 | 0.0535 | 28 / 8,192 | 0.468 % | pass |
| 10 s | 0.10 | bare | open | 4.1116 | 196 / 65,536 | 336 | 17 / 8,192 | 0.311 % | pass |
| 10 s | 0.10 | bare | cs662 | 3.0034 | 196 / 65,536 | 4.37 | 23 / 8,192 | 0.398 % | pass |
| 10 s | 0.10 | front-only | open | 2.3363 | 195 / 65,536 | 1.3 | 24 / 8,192 | 0.412 % | pass |
| 10 s | 0.10 | front-only | cs662 | 1.5273 | 195 / 65,536 | 0.107 | 23 / 8,192 | 0.398 % | pass |
| 10 s | 0.20 | bare | open | 4.1367 | 196 / 65,536 | 671 | 19 / 8,192 | 0.340 % | pass |
| 10 s | 0.20 | bare | cs662 | 3.4277 | 196 / 65,536 | 8.75 | 31 / 8,192 | 0.510 % | pass |
| 10 s | 0.20 | front-only | open | 2.7003 | 196 / 65,536 | 2.59 | 20 / 8,192 | 0.355 % | pass |
| 10 s | 0.20 | front-only | cs662 | 1.5857 | 196 / 65,536 | 0.214 | 38 / 8,192 | 0.608 % | pass |
| 60 s | 0.05 | bare | open | 4.1646 | 196 / 65,536 | 1,007 | 22 / 8,192 | 0.383 % | pass |
| 60 s | 0.05 | bare | cs662 | 3.6485 | 196 / 65,536 | 13.1 | 32 / 8,192 | 0.524 % | pass |
| 60 s | 0.05 | front-only | open | 2.9475 | 196 / 65,536 | 3.89 | 38 / 8,192 | 0.608 % | pass |
| 60 s | 0.05 | front-only | cs662 | 1.8181 | 196 / 65,536 | 0.321 | 36 / 8,192 | 0.580 % | pass |
| 60 s | 0.10 | bare | open | 4.1595 | 196 / 65,536 | 2,014 | 28 / 8,192 | 0.468 % | pass |
| 60 s | 0.10 | bare | cs662 | 3.9377 | 196 / 65,536 | 26.2 | 29 / 8,192 | 0.482 % | pass |
| 60 s | 0.10 | front-only | open | 3.3593 | 195 / 65,536 | 7.78 | 27 / 8,192 | 0.454 % | pass |
| 60 s | 0.10 | front-only | cs662 | 2.0655 | 196 / 65,536 | 0.642 | 28 / 8,192 | 0.468 % | pass |
| 60 s | 0.20 | bare | open | 4.2167 | 196 / 65,536 | 4,028 | 15 / 8,192 | 0.282 % | pass |
| 60 s | 0.20 | bare | cs662 | 4.0686 | 196 / 65,536 | 52.5 | 19 / 8,192 | 0.340 % | pass |
| 60 s | 0.20 | front-only | open | 3.7615 | 196 / 65,536 | 15.6 | 21 / 8,192 | 0.369 % | pass |
| 60 s | 0.20 | front-only | cs662 | 2.3710 | 196 / 65,536 | 1.28 | 24 / 8,192 | 0.412 % | pass |

### 8. Reproduce

```bash
dotnet build Gcam.sln -c Release
python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-ambient-v2.json --out <dir> --family gate_selection_v2
python samples/evidence/ambient/select_thresholds.py --runs <dir> --manifest samples/evidence/manifest-ambient-v2.json --family gate_selection_v2 --rule inclusive --out <thresholds.json>   # = gate-thresholds-v2.json
python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-ambient-v2.json --out <dir> --family gate_validation_v2
python samples/evidence/ambient/aggregate_gate.py --runs <dir> --manifest samples/evidence/manifest-ambient-v2.json --family gate_validation_v2 --out <gate.json>   # = ambient-baseline-v1-turn8-gate.json
python samples/evidence/calibration_record.py            # rewrites this block
python samples/evidence/calibration_record.py --check    # verifies it
```
<!-- CAL-02 generated: end -->

## CAL-03 — Stripped-image trust thresholds under Co-60

| Field | Value |
|---|---|
| Record | CAL-03 |
| Used by | PR-NRG-04, D-44, EV-15 |
| Quantity | Threshold T on the stripped search significance Z_s: the decoded stripped image (662 keV window minus the per-pixel ratio R_i times the side reference window), studentised by its Poisson variance, largest over the decoder grid, recorded to 4 decimals (dimensionless). A Cs-137 location is trusted when the recorded Z_s ≥ T. |
| Configuration key | scene (Cs-137 and Co-60 co-located; two; five angular elements apart) × exposure (10, 60, 300 s) × environment (ideal; 0.10 µSv/h bare crystal; 0.10 µSv/h front only) × Co-60 activity at 1 m (0, 0.1, 1, 3, 10, 28.72 = 10 µSv/h at the head, 100 MBq; front only: 0 and 0.1 MBq) — hand-held head at 1 m, 144 values |
| Status | valid |

### 1. Purpose and acceptance
A Cs-free acquisition (Co-60 and ambient only) must give a trusted Cs-137 location in at most
1 % of acquisitions, for each configuration separately (D-44, by the PR-SENS-02 rule). Pass criterion: the one-sided
95 % Clopper–Pearson upper limit of the false-trusted rate is ≤ 1 %. Interval method: exact binomial
(Clopper–Pearson), one-sided, 95 %. A configuration without counts (ideal, no Co-60) cannot trust anything. It is
marked "not applicable — no counts" and is not counted as a pass.

### 2. Selection rule
CAL-02's inclusive rule, unchanged, applied to Z_s: 64 seeds × 1,024 Cs-free acquisitions =
65,536 per configuration, α = 0.3 %, k = 196, ties count as trusted. The reason for α is that of CAL-02, with the same
selection and validation sizes.

### 3. Use between and beyond the grid
Valid at the 144 grid points only. Between grid points the higher neighbouring T applies:
- a Co-60 activity between two levels uses the higher of the two thresholds;
- an exposure between 10 and 60 s, or between 60 and 300 s, likewise;
- an ambient field between none and 0.10 µSv/h uses the higher of the ideal and 0.10 µSv/h values;
- a housing between the two bounds uses the higher of the bare and front-only values (front-only is measured only at
  Co-60 = 0 and 0.1 MBq; above 0.1 MBq a front-only configuration has no valid calibration);
- a source separation between the three scenes uses the highest of the bracketing scenes.

There is no valid calibration outside the grid: Co-60 above 100 MBq (the claim stops at 10 µSv/h, D-45), ambient above
0.10 µSv/h, exposures outside 10–300 s, distances other than 1 m, another head.

### 4. Re-calibration triggers
Any change to the stripping (reference window, per-pixel R_i calibration, gain model); the
energy windows or resolution; the decoder or its grid; the head geometry or distance; the ambient spectrum; the
statistic (studentisation, recording precision, tie rule); or the Co-60 / Cs-137 scene set.

### 5. Known limits
The calibration covers the hand-held head at 1 m only, one ambient level (0.10 µSv/h) and the
three scene geometries. The ratio R_i is calibrated with Co-60 on axis; other Co-60 directions are covered only through
the scenes. It is computed before housing and room scatter and before a four-channel readout model. The 9 ideal Co-60 = 0
configurations test nothing (no counts).

<!-- CAL-03 generated: begin -->
### 6. Data

| Item | Value |
|---|---|
| Date measured (manifest) | 2026-10-06 |
| Selection family | `csco_selection`: F256[1:65], 64 seeds × 1,024 Cs-free acquisitions = 65,536 per configuration |
| Validation family | `csco_validation`: F256[128:256], 128 seeds × 64 = 8,192 Cs-free acquisitions per configuration |
| Selection level α; comparison | 0.003; trusted when the recorded Z_s (4 decimals) ≥ T, ties count as exceedances |
| Selection runs digest | `1687bb28b75492c3130db7dde258c5ae26457840a7b25712aa4f4a8e6eda3859` (SHA-256 over the per-seed run files; the runs are not committed) |
| Seed overlap between selection and validation | none (checked) |
| Value fixed before validation | yes — the validation request `samples/evidence/ambient/csco-request-v1-validation.json` pins `samples/evidence/ambient/csco-thresholds-v1.json` by SHA-256, and the manifest pins the request |
| Engine commit of the runs | not recorded; the code landed in `afbb0ae` |

Files read (SHA-256 of the bytes git stores; pinned in `samples/evidence/calibration-records.json`):

| File | SHA-256 |
|---|---|
| `samples/ambient/terrestrial-unscear2000-v1.json` | `4d48bc4272ce4c25ff6ae9b13ed7583e3c8f467afffdb0225956a70cc435b3f4` |
| `samples/evidence/ambient/csco-request-v1-selection.json` | `bcb55d4671d9e533298f169d22f83be97198bed4a234b523ce18b5b30667b330` |
| `samples/evidence/ambient/csco-request-v1-validation.json` | `d6e210c72ed1b8b08c546facae3f6899a071cd3e5be956484e61e28ea3f24d4b` |
| `samples/evidence/ambient/csco-thresholds-v1.json` | `fac811b2d2c148c5ff366f88e24781fd1975d9ad345aa99009495636dbb1cd22` |
| `samples/evidence/ambient/gate-thresholds-v2.json` | `6352b2f692894fde1d83c6ef2c3ac2c04ee1b332c511e150d66342a516b6cdd0` |
| `samples/evidence/manifest-csco-v1.json` | `5c5fcaf6eb97b1971efc01941d571be22c633b9a76ce486a5162fdc5abfc00ac` |
| `samples/evidence/results/csco-v1-validation.json` | `79cc4d006c49167833c3a928bcb81c9045c028096eccfe082bd8c37eaced452b` |
| `samples/evidence/seeds.json` | `f8c3b2c1385b6d11520abd7d224181ed79d5b0e2719de300d95341c77bd82583` |
| `samples/scenario_handheld.json` | `f988784c8ee266e6dd28eb898cdbbfac0e2452cb01fd3929874fd866edc98795` |

### 7. Values and validation

**Summary.** 144 values; **135 / 135 judged configurations pass** (one-sided 95 % Clopper–Pearson upper limit of the false-trusted rate ≤ 1 %); 9 not applicable; 0 FAIL. Pooled over the judged configurations: 3,350 / 1,105,920 = 0.303 % (all rows: 3,350 / 1,179,648 = 0.284 %). Worst: `sep5|t=60|F=0.1|BareCrystalAllFaces|Co=1000000`, 38 / 8,192, upper limit 0.608 %.

Scenes: `coloc` Cs-137 and Co-60 co-located; `sep2` / `sep5` two / five angular elements apart. Columns as in the gate records; the expected counts are the median over the validation seeds.

**coloc, 10 s** (16 configurations)

| Environment | Co-60 MBq | Threshold T | Selection ≥ T (k / N) | Expected 662 keV counts (median) | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|
| ideal | 0 | — (no counts) | 0 / 65,536 | 0 | 0 / 8,192 | 0.037 % | not applicable — no counts |
| ideal | 0.1 | 2.6371 | 194 / 65,536 | 0.9183 | 17 / 8,192 | 0.311 % | pass |
| ideal | 1 | 4.1176 | 196 / 65,536 | 9.183 | 26 / 8,192 | 0.440 % | pass |
| ideal | 3 | 4.4603 | 196 / 65,536 | 27.55 | 28 / 8,192 | 0.468 % | pass |
| ideal | 10 | 4.5244 | 196 / 65,536 | 91.83 | 33 / 8,192 | 0.538 % | pass |
| ideal | 28.72 (10 µSv/h) | 4.5563 | 196 / 65,536 | 263.7 | 30 / 8,192 | 0.496 % | pass |
| ideal | 100 | 4.5862 | 196 / 65,536 | 918.3 | 30 / 8,192 | 0.496 % | pass |
| 0.10 µSv/h, bare | 0 | 3.4874 | 196 / 65,536 | 4.285 | 30 / 8,192 | 0.496 % | pass |
| 0.10 µSv/h, bare | 0.1 | 3.6871 | 196 / 65,536 | 5.202 | 16 / 8,192 | 0.296 % | pass |
| 0.10 µSv/h, bare | 1 | 4.2686 | 196 / 65,536 | 13.46 | 33 / 8,192 | 0.538 % | pass |
| 0.10 µSv/h, bare | 3 | 4.4669 | 195 / 65,536 | 31.83 | 24 / 8,192 | 0.412 % | pass |
| 0.10 µSv/h, bare | 10 | 4.5277 | 196 / 65,536 | 96.11 | 24 / 8,192 | 0.412 % | pass |
| 0.10 µSv/h, bare | 28.72 (10 µSv/h) | 4.5464 | 196 / 65,536 | 268 | 28 / 8,192 | 0.468 % | pass |
| 0.10 µSv/h, bare | 100 | 4.6008 | 196 / 65,536 | 922.5 | 28 / 8,192 | 0.468 % | pass |
| 0.10 µSv/h, front-only | 0 | 1.4242 | 194 / 65,536 | 0.1041 | 15 / 8,192 | 0.282 % | pass |
| 0.10 µSv/h, front-only | 0.1 | 2.6416 | 194 / 65,536 | 1.023 | 17 / 8,192 | 0.311 % | pass |

**coloc, 60 s** (16 configurations)

| Environment | Co-60 MBq | Threshold T | Selection ≥ T (k / N) | Expected 662 keV counts (median) | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|
| ideal | 0 | — (no counts) | 0 / 65,536 | 0 | 0 / 8,192 | 0.037 % | not applicable — no counts |
| ideal | 0.1 | 3.8596 | 195 / 65,536 | 5.51 | 24 / 8,192 | 0.412 % | pass |
| ideal | 1 | 4.4913 | 196 / 65,536 | 55.1 | 22 / 8,192 | 0.383 % | pass |
| ideal | 3 | 4.5430 | 196 / 65,536 | 165.3 | 32 / 8,192 | 0.524 % | pass |
| ideal | 10 | 4.5413 | 195 / 65,536 | 551 | 21 / 8,192 | 0.369 % | pass |
| ideal | 28.72 (10 µSv/h) | 4.6118 | 196 / 65,536 | 1,582 | 21 / 8,192 | 0.369 % | pass |
| ideal | 100 | 4.7287 | 196 / 65,536 | 5,510 | 24 / 8,192 | 0.412 % | pass |
| 0.10 µSv/h, bare | 0 | 4.3826 | 196 / 65,536 | 25.71 | 18 / 8,192 | 0.326 % | pass |
| 0.10 µSv/h, bare | 0.1 | 4.4549 | 196 / 65,536 | 31.21 | 25 / 8,192 | 0.426 % | pass |
| 0.10 µSv/h, bare | 1 | 4.5235 | 196 / 65,536 | 80.79 | 20 / 8,192 | 0.355 % | pass |
| 0.10 µSv/h, bare | 3 | 4.5756 | 196 / 65,536 | 191 | 22 / 8,192 | 0.383 % | pass |
| 0.10 µSv/h, bare | 10 | 4.5547 | 196 / 65,536 | 576.7 | 23 / 8,192 | 0.398 % | pass |
| 0.10 µSv/h, bare | 28.72 (10 µSv/h) | 4.6257 | 196 / 65,536 | 1,608 | 25 / 8,192 | 0.426 % | pass |
| 0.10 µSv/h, bare | 100 | 4.7214 | 196 / 65,536 | 5,535 | 25 / 8,192 | 0.426 % | pass |
| 0.10 µSv/h, front-only | 0 | 2.2553 | 195 / 65,536 | 0.6244 | 28 / 8,192 | 0.468 % | pass |
| 0.10 µSv/h, front-only | 0.1 | 3.8819 | 196 / 65,536 | 6.138 | 28 / 8,192 | 0.468 % | pass |

**coloc, 300 s** (16 configurations)

| Environment | Co-60 MBq | Threshold T | Selection ≥ T (k / N) | Expected 662 keV counts (median) | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|
| ideal | 0 | — (no counts) | 0 / 65,536 | 0 | 0 / 8,192 | 0.037 % | not applicable — no counts |
| ideal | 0.1 | 4.4370 | 196 / 65,536 | 27.55 | 19 / 8,192 | 0.340 % | pass |
| ideal | 1 | 4.5296 | 196 / 65,536 | 275.5 | 37 / 8,192 | 0.594 % | pass |
| ideal | 3 | 4.5817 | 196 / 65,536 | 826.4 | 29 / 8,192 | 0.482 % | pass |
| ideal | 10 | 4.6432 | 196 / 65,536 | 2,755 | 28 / 8,192 | 0.468 % | pass |
| ideal | 28.72 (10 µSv/h) | 4.8067 | 196 / 65,536 | 7,912 | 18 / 8,192 | 0.326 % | pass |
| ideal | 100 | 5.2768 | 196 / 65,536 | 27,548 | 29 / 8,192 | 0.482 % | pass |
| 0.10 µSv/h, bare | 0 | 4.6052 | 196 / 65,536 | 128.6 | 23 / 8,192 | 0.398 % | pass |
| 0.10 µSv/h, bare | 0.1 | 4.6349 | 196 / 65,536 | 156.1 | 26 / 8,192 | 0.440 % | pass |
| 0.10 µSv/h, bare | 1 | 4.5736 | 196 / 65,536 | 403.9 | 27 / 8,192 | 0.454 % | pass |
| 0.10 µSv/h, bare | 3 | 4.5896 | 196 / 65,536 | 954.9 | 30 / 8,192 | 0.496 % | pass |
| 0.10 µSv/h, bare | 10 | 4.6359 | 196 / 65,536 | 2,883 | 27 / 8,192 | 0.454 % | pass |
| 0.10 µSv/h, bare | 28.72 (10 µSv/h) | 4.7914 | 196 / 65,536 | 8,041 | 13 / 8,192 | 0.252 % | pass |
| 0.10 µSv/h, bare | 100 | 5.3059 | 196 / 65,536 | 27,676 | 23 / 8,192 | 0.398 % | pass |
| 0.10 µSv/h, front-only | 0 | 3.3405 | 196 / 65,536 | 3.122 | 26 / 8,192 | 0.440 % | pass |
| 0.10 µSv/h, front-only | 0.1 | 4.4562 | 196 / 65,536 | 30.69 | 18 / 8,192 | 0.326 % | pass |

**sep2, 10 s** (16 configurations)

| Environment | Co-60 MBq | Threshold T | Selection ≥ T (k / N) | Expected 662 keV counts (median) | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|
| ideal | 0 | — (no counts) | 0 / 65,536 | 0 | 0 / 8,192 | 0.037 % | not applicable — no counts |
| ideal | 0.1 | 2.6346 | 195 / 65,536 | 0.9104 | 25 / 8,192 | 0.426 % | pass |
| ideal | 1 | 4.1180 | 196 / 65,536 | 9.104 | 25 / 8,192 | 0.426 % | pass |
| ideal | 3 | 4.4321 | 196 / 65,536 | 27.31 | 30 / 8,192 | 0.496 % | pass |
| ideal | 10 | 4.5337 | 196 / 65,536 | 91.04 | 31 / 8,192 | 0.510 % | pass |
| ideal | 28.72 (10 µSv/h) | 4.5516 | 196 / 65,536 | 261.5 | 24 / 8,192 | 0.412 % | pass |
| ideal | 100 | 4.5675 | 196 / 65,536 | 910.4 | 21 / 8,192 | 0.369 % | pass |
| 0.10 µSv/h, bare | 0 | 3.4744 | 196 / 65,536 | 4.285 | 19 / 8,192 | 0.340 % | pass |
| 0.10 µSv/h, bare | 0.1 | 3.6327 | 196 / 65,536 | 5.196 | 35 / 8,192 | 0.566 % | pass |
| 0.10 µSv/h, bare | 1 | 4.2365 | 196 / 65,536 | 13.38 | 25 / 8,192 | 0.426 % | pass |
| 0.10 µSv/h, bare | 3 | 4.4552 | 196 / 65,536 | 31.6 | 28 / 8,192 | 0.468 % | pass |
| 0.10 µSv/h, bare | 10 | 4.5257 | 196 / 65,536 | 95.34 | 25 / 8,192 | 0.426 % | pass |
| 0.10 µSv/h, bare | 28.72 (10 µSv/h) | 4.5539 | 196 / 65,536 | 265.8 | 27 / 8,192 | 0.454 % | pass |
| 0.10 µSv/h, bare | 100 | 4.6115 | 196 / 65,536 | 914.8 | 30 / 8,192 | 0.496 % | pass |
| 0.10 µSv/h, front-only | 0 | 1.4243 | 193 / 65,536 | 0.1041 | 23 / 8,192 | 0.398 % | pass |
| 0.10 µSv/h, front-only | 0.1 | 2.6407 | 193 / 65,536 | 1.015 | 29 / 8,192 | 0.482 % | pass |

**sep2, 60 s** (16 configurations)

| Environment | Co-60 MBq | Threshold T | Selection ≥ T (k / N) | Expected 662 keV counts (median) | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|
| ideal | 0 | — (no counts) | 0 / 65,536 | 0 | 0 / 8,192 | 0.037 % | not applicable — no counts |
| ideal | 0.1 | 3.8576 | 196 / 65,536 | 5.463 | 31 / 8,192 | 0.510 % | pass |
| ideal | 1 | 4.5032 | 196 / 65,536 | 54.63 | 23 / 8,192 | 0.398 % | pass |
| ideal | 3 | 4.5498 | 196 / 65,536 | 163.9 | 24 / 8,192 | 0.412 % | pass |
| ideal | 10 | 4.6012 | 196 / 65,536 | 546.3 | 19 / 8,192 | 0.340 % | pass |
| ideal | 28.72 (10 µSv/h) | 4.6149 | 196 / 65,536 | 1,569 | 29 / 8,192 | 0.482 % | pass |
| ideal | 100 | 4.7331 | 195 / 65,536 | 5,463 | 18 / 8,192 | 0.326 % | pass |
| 0.10 µSv/h, bare | 0 | 4.4029 | 196 / 65,536 | 25.71 | 27 / 8,192 | 0.454 % | pass |
| 0.10 µSv/h, bare | 0.1 | 4.4694 | 196 / 65,536 | 31.17 | 29 / 8,192 | 0.482 % | pass |
| 0.10 µSv/h, bare | 1 | 4.5088 | 196 / 65,536 | 80.31 | 25 / 8,192 | 0.426 % | pass |
| 0.10 µSv/h, bare | 3 | 4.5588 | 196 / 65,536 | 189.6 | 27 / 8,192 | 0.454 % | pass |
| 0.10 µSv/h, bare | 10 | 4.5509 | 196 / 65,536 | 572 | 24 / 8,192 | 0.412 % | pass |
| 0.10 µSv/h, bare | 28.72 (10 µSv/h) | 4.6166 | 196 / 65,536 | 1,595 | 22 / 8,192 | 0.383 % | pass |
| 0.10 µSv/h, bare | 100 | 4.7455 | 196 / 65,536 | 5,489 | 26 / 8,192 | 0.440 % | pass |
| 0.10 µSv/h, front-only | 0 | 2.2562 | 196 / 65,536 | 0.6244 | 22 / 8,192 | 0.383 % | pass |
| 0.10 µSv/h, front-only | 0.1 | 3.8670 | 195 / 65,536 | 6.087 | 22 / 8,192 | 0.383 % | pass |

**sep2, 300 s** (16 configurations)

| Environment | Co-60 MBq | Threshold T | Selection ≥ T (k / N) | Expected 662 keV counts (median) | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|
| ideal | 0 | — (no counts) | 0 / 65,536 | 0 | 0 / 8,192 | 0.037 % | not applicable — no counts |
| ideal | 0.1 | 4.4610 | 196 / 65,536 | 27.31 | 31 / 8,192 | 0.510 % | pass |
| ideal | 1 | 4.5647 | 195 / 65,536 | 273.1 | 20 / 8,192 | 0.355 % | pass |
| ideal | 3 | 4.5713 | 196 / 65,536 | 819.4 | 36 / 8,192 | 0.580 % | pass |
| ideal | 10 | 4.6609 | 196 / 65,536 | 2,731 | 20 / 8,192 | 0.355 % | pass |
| ideal | 28.72 (10 µSv/h) | 4.8183 | 196 / 65,536 | 7,844 | 21 / 8,192 | 0.369 % | pass |
| ideal | 100 | 5.2879 | 196 / 65,536 | 27,313 | 23 / 8,192 | 0.398 % | pass |
| 0.10 µSv/h, bare | 0 | 4.6197 | 196 / 65,536 | 128.6 | 20 / 8,192 | 0.355 % | pass |
| 0.10 µSv/h, bare | 0.1 | 4.6132 | 196 / 65,536 | 155.9 | 26 / 8,192 | 0.440 % | pass |
| 0.10 µSv/h, bare | 1 | 4.5468 | 196 / 65,536 | 401.5 | 32 / 8,192 | 0.524 % | pass |
| 0.10 µSv/h, bare | 3 | 4.5800 | 196 / 65,536 | 948.1 | 22 / 8,192 | 0.383 % | pass |
| 0.10 µSv/h, bare | 10 | 4.6584 | 196 / 65,536 | 2,860 | 22 / 8,192 | 0.383 % | pass |
| 0.10 µSv/h, bare | 28.72 (10 µSv/h) | 4.7948 | 196 / 65,536 | 7,974 | 17 / 8,192 | 0.311 % | pass |
| 0.10 µSv/h, bare | 100 | 5.3090 | 195 / 65,536 | 27,443 | 19 / 8,192 | 0.340 % | pass |
| 0.10 µSv/h, front-only | 0 | 3.3486 | 196 / 65,536 | 3.122 | 23 / 8,192 | 0.398 % | pass |
| 0.10 µSv/h, front-only | 0.1 | 4.4391 | 196 / 65,536 | 30.44 | 36 / 8,192 | 0.580 % | pass |

**sep5, 10 s** (16 configurations)

| Environment | Co-60 MBq | Threshold T | Selection ≥ T (k / N) | Expected 662 keV counts (median) | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|
| ideal | 0 | — (no counts) | 0 / 65,536 | 0 | 0 / 8,192 | 0.037 % | not applicable — no counts |
| ideal | 0.1 | 2.4478 | 196 / 65,536 | 0.8508 | 25 / 8,192 | 0.426 % | pass |
| ideal | 1 | 4.1032 | 196 / 65,536 | 8.508 | 20 / 8,192 | 0.355 % | pass |
| ideal | 3 | 4.4328 | 196 / 65,536 | 25.52 | 24 / 8,192 | 0.412 % | pass |
| ideal | 10 | 4.5425 | 196 / 65,536 | 85.08 | 23 / 8,192 | 0.398 % | pass |
| ideal | 28.72 (10 µSv/h) | 4.5597 | 196 / 65,536 | 244.4 | 31 / 8,192 | 0.510 % | pass |
| ideal | 100 | 4.6271 | 196 / 65,536 | 850.8 | 23 / 8,192 | 0.398 % | pass |
| 0.10 µSv/h, bare | 0 | 3.4895 | 196 / 65,536 | 4.285 | 29 / 8,192 | 0.482 % | pass |
| 0.10 µSv/h, bare | 0.1 | 3.6363 | 196 / 65,536 | 5.136 | 26 / 8,192 | 0.440 % | pass |
| 0.10 µSv/h, bare | 1 | 4.2355 | 196 / 65,536 | 12.79 | 19 / 8,192 | 0.340 % | pass |
| 0.10 µSv/h, bare | 3 | 4.4319 | 196 / 65,536 | 29.82 | 27 / 8,192 | 0.454 % | pass |
| 0.10 µSv/h, bare | 10 | 4.5116 | 196 / 65,536 | 89.36 | 24 / 8,192 | 0.412 % | pass |
| 0.10 µSv/h, bare | 28.72 (10 µSv/h) | 4.5552 | 196 / 65,536 | 248.6 | 26 / 8,192 | 0.440 % | pass |
| 0.10 µSv/h, bare | 100 | 4.6076 | 196 / 65,536 | 855.1 | 14 / 8,192 | 0.267 % | pass |
| 0.10 µSv/h, front-only | 0 | 1.4244 | 187 / 65,536 | 0.1041 | 21 / 8,192 | 0.369 % | pass |
| 0.10 µSv/h, front-only | 0.1 | 2.6391 | 196 / 65,536 | 0.9551 | 26 / 8,192 | 0.440 % | pass |

**sep5, 60 s** (16 configurations)

| Environment | Co-60 MBq | Threshold T | Selection ≥ T (k / N) | Expected 662 keV counts (median) | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|
| ideal | 0 | — (no counts) | 0 / 65,536 | 0 | 0 / 8,192 | 0.037 % | not applicable — no counts |
| ideal | 0.1 | 3.8030 | 196 / 65,536 | 5.105 | 25 / 8,192 | 0.426 % | pass |
| ideal | 1 | 4.4947 | 196 / 65,536 | 51.05 | 27 / 8,192 | 0.454 % | pass |
| ideal | 3 | 4.5469 | 196 / 65,536 | 153.1 | 27 / 8,192 | 0.454 % | pass |
| ideal | 10 | 4.5927 | 196 / 65,536 | 510.5 | 24 / 8,192 | 0.412 % | pass |
| ideal | 28.72 (10 µSv/h) | 4.5955 | 196 / 65,536 | 1,466 | 21 / 8,192 | 0.369 % | pass |
| ideal | 100 | 4.7625 | 196 / 65,536 | 5,105 | 17 / 8,192 | 0.311 % | pass |
| 0.10 µSv/h, bare | 0 | 4.4121 | 196 / 65,536 | 25.71 | 20 / 8,192 | 0.355 % | pass |
| 0.10 µSv/h, bare | 0.1 | 4.4507 | 196 / 65,536 | 30.82 | 22 / 8,192 | 0.383 % | pass |
| 0.10 µSv/h, bare | 1 | 4.5061 | 196 / 65,536 | 76.75 | 38 / 8,192 | 0.608 % | pass |
| 0.10 µSv/h, bare | 3 | 4.5240 | 196 / 65,536 | 178.9 | 27 / 8,192 | 0.454 % | pass |
| 0.10 µSv/h, bare | 10 | 4.5708 | 196 / 65,536 | 536.2 | 20 / 8,192 | 0.355 % | pass |
| 0.10 µSv/h, bare | 28.72 (10 µSv/h) | 4.6128 | 196 / 65,536 | 1,492 | 23 / 8,192 | 0.398 % | pass |
| 0.10 µSv/h, bare | 100 | 4.6876 | 196 / 65,536 | 5,131 | 34 / 8,192 | 0.552 % | pass |
| 0.10 µSv/h, front-only | 0 | 2.2572 | 195 / 65,536 | 0.6244 | 25 / 8,192 | 0.426 % | pass |
| 0.10 µSv/h, front-only | 0.1 | 3.8602 | 196 / 65,536 | 5.73 | 24 / 8,192 | 0.412 % | pass |

**sep5, 300 s** (16 configurations)

| Environment | Co-60 MBq | Threshold T | Selection ≥ T (k / N) | Expected 662 keV counts (median) | Validation false trusted (k / N) | Upper 95 % | Verdict |
|---|---|---|---|---|---|---|---|
| ideal | 0 | — (no counts) | 0 / 65,536 | 0 | 0 / 8,192 | 0.037 % | not applicable — no counts |
| ideal | 0.1 | 4.4283 | 196 / 65,536 | 25.52 | 31 / 8,192 | 0.510 % | pass |
| ideal | 1 | 4.5741 | 196 / 65,536 | 255.2 | 26 / 8,192 | 0.440 % | pass |
| ideal | 3 | 4.5774 | 196 / 65,536 | 765.7 | 36 / 8,192 | 0.580 % | pass |
| ideal | 10 | 4.6610 | 196 / 65,536 | 2,552 | 16 / 8,192 | 0.296 % | pass |
| ideal | 28.72 (10 µSv/h) | 4.7645 | 196 / 65,536 | 7,331 | 25 / 8,192 | 0.426 % | pass |
| ideal | 100 | 5.3266 | 196 / 65,536 | 25,524 | 31 / 8,192 | 0.510 % | pass |
| 0.10 µSv/h, bare | 0 | 4.6140 | 196 / 65,536 | 128.6 | 21 / 8,192 | 0.369 % | pass |
| 0.10 µSv/h, bare | 0.1 | 4.6277 | 196 / 65,536 | 154.1 | 18 / 8,192 | 0.326 % | pass |
| 0.10 µSv/h, bare | 1 | 4.5829 | 196 / 65,536 | 383.8 | 27 / 8,192 | 0.454 % | pass |
| 0.10 µSv/h, bare | 3 | 4.6109 | 196 / 65,536 | 894.5 | 21 / 8,192 | 0.369 % | pass |
| 0.10 µSv/h, bare | 10 | 4.6604 | 196 / 65,536 | 2,681 | 18 / 8,192 | 0.326 % | pass |
| 0.10 µSv/h, bare | 28.72 (10 µSv/h) | 4.7732 | 196 / 65,536 | 7,459 | 19 / 8,192 | 0.340 % | pass |
| 0.10 µSv/h, bare | 100 | 5.2978 | 196 / 65,536 | 25,653 | 35 / 8,192 | 0.566 % | pass |
| 0.10 µSv/h, front-only | 0 | 3.3413 | 196 / 65,536 | 3.122 | 18 / 8,192 | 0.326 % | pass |
| 0.10 µSv/h, front-only | 0.1 | 4.4396 | 196 / 65,536 | 28.65 | 33 / 8,192 | 0.538 % | pass |

### 8. Reproduce

```bash
dotnet build Gcam.sln -c Release
python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-csco-v1.json --out <dir> --family csco_selection
python samples/evidence/ambient/select_csco_thresholds.py --runs <dir> --manifest samples/evidence/manifest-csco-v1.json --family csco_selection --out <thresholds.json>   # = csco-thresholds-v1.json
python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-csco-v1.json --out <dir> --family csco_validation
python samples/evidence/ambient/aggregate_csco.py --runs <dir> --manifest samples/evidence/manifest-csco-v1.json --family csco_validation --out <validation.json>   # = csco-v1-validation.json
python samples/evidence/calibration_record.py            # rewrites this block
python samples/evidence/calibration_record.py --check    # verifies it
```
<!-- CAL-03 generated: end -->

## CAL-04 — Pair-test significance floors

| Field | Value |
|---|---|
| Record | CAL-04 |
| Used by | PR-IMG-04, D-48, EV-11 |
| Quantity | Floor F on the second peak's absolute topographic prominence, in the decoder's own image units (not comparable between decoders). A pair counts as resolved only when the shape test of D-48 passes **and** the second peak's prominence ≥ F. |
| Configuration key | distance (1 m, 5 m) × decoder (cross-correlation `cc`; pixel-area MLEM at 120 iterations `area@120`; pixel-centre MLEM at 8 iterations `binary@8`) × total counts (1 m: 500, 1,250, 2,000, 5,000, 8,000, 20,000, 32,000, 80,000; 5 m: 2,000, 8,000) × placement (on axis; near the fully coded field's edge — 1 m only) — hand-held head, 662 keV window, 54 values |
| Status | valid, with four FAIL validation rows (section 7) |

### 1. Purpose and acceptance
A single source must be reported as a resolved pair ("false split") in at most 5 % of
acquisitions, at every pair separation of the test grid (D-48). Pass criterion (author's decision: the same standard
as the other records): the upper bootstrap limit of the false-split rate is ≤ 5 % in every validation condition the
floor applies to. Interval method: percentile bootstrap over the validation seeds (2,000 resamples). The evidence file
stores the two-sided 95 % interval, so the limit applied is its upper end, a one-sided 97.5 % limit — slightly stricter
than the one-sided 95 % limit of the other records, which is not stored. Each seed has its own sampling phase, so a
binomial interval would be too narrow. A floor fails when any of its conditions fails.

### 2. Selection rule
For each decoder × total counts × placement, the single-source acquisitions of the selection
seeds give, per separation, the second peak's prominence when the shape test passes. F is the smallest candidate (0,
or just above an observed value) at which the false-split share is ≤ α = 3 % at **every** separation of the grid
(inclusive). A sensitivity floor, hypothesis-free (a second peak anywhere above F), is reported beside it.

Why α = 3 %: it is 5 % minus about two standard errors of a 1,600-acquisition selection rate (√(0.05 × 0.95 / 1,600) =
0.55 %), sized so that the validation rate stays under 5 %. Judged by the bootstrap upper limit, this margin is thin
where the false split sits near α. The four FAIL rows in section 7 come from that, and they all lie in cells where no
separation is resolved anyway.

### 3. Use between and beyond the grid
Valid at the 54 grid points only. Between grid points the higher neighbouring F applies:
- total counts between two levels use the higher of the two floors;
- a source position between on axis and the edge of the fully coded field uses the higher of the axis and edge floors;
- a distance between 1 m and 5 m has no valid floor (they are separate grids, and F is in image units that change with
  distance).

There is no valid calibration outside the grid: counts below 500 or above 80,000 (1 m), outside 2,000–8,000 (5 m),
off-axis at 5 m, positions outside the fully coded field (D-47), other decoders or iteration counts.

### 4. Re-calibration triggers
Any change to the decoder (forward model, iteration count, regularisation), the peak finder or
the prominence definition; the valley parameter (0.25); the reconstruction grid; the head geometry, distance or energy
window; or the pair-test separations.

### 5. Known limits
Ideal pixel identification, a homogeneous crystal, no inter-pixel dead regions (before a
four-channel readout model); a still head; no ambient field in the selection (the ambient check of EV-11 leaves the
resolved separations unchanged). The floor bounds false splits at the tested pair geometry only. The floors exist
only in the evidence aggregation; no reconstruction code reads them.

<!-- CAL-04 generated: begin -->
### 6. Data

| Item | Value |
|---|---|
| Date measured (manifest) | 2026-10-06 |
| Selection family, 1 m | `angres_floor_select_1m`: F256[193:225], 32 seeds; 1,600 single-source acquisitions per separation × 8 separations per floor |
| Validation family, 1 m | `angres_floor_1m`: F256[33:161], 128 seeds; 6,400 single-source acquisitions per condition (separation × counts per source × ratio × placement) |
| Selection family, 5 m | `angres_floor_select_5m`: F256[193:225], 32 seeds; 1,600 single-source acquisitions per separation × 8 separations per floor |
| Validation family, 5 m | `angres_floor_5m`: F256[33:161], 128 seeds; 6,400 single-source acquisitions per condition (separation × counts per source × ratio × placement) |
| Selection level α; comparison | 0.03 at every separation of the grid; a second peak counts when its absolute prominence ≥ F (valley 0.25) |
| Seed overlap between selection and validation | none (checked) |
| Value fixed before validation | not needed — the validation runs record the raw per-acquisition statistic, so the floors are selected and applied in one aggregation step; the aggregator refuses overlapping seeds |
| Engine commit of the runs | not recorded; the code landed in `98bc133` |

Files read (SHA-256 of the bytes git stores; pinned in `samples/evidence/calibration-records.json`):

| File | SHA-256 |
|---|---|
| `samples/ambient/terrestrial-unscear2000-v1.json` | `4d48bc4272ce4c25ff6ae9b13ed7583e3c8f467afffdb0225956a70cc435b3f4` |
| `samples/evidence/angres/angres-request-v2-floor-1m.json` | `c78b9b1ccd44ca86be3f1fba1f08d01f35f5aeaeb11c6f4755ddd4177af466db` |
| `samples/evidence/angres/angres-request-v2-floor-5m.json` | `1c82c25f75935f17f275b82e67a6abd4a56526243ca3adfd213b3a8d450bc8a9` |
| `samples/evidence/manifest-angres-v2.json` | `849128670a602194dc18ebd5b9225325611437034075bcc40bbc05c16b72edd8` |
| `samples/evidence/results/angres-v2-floor.json` | `50eee9df3225b7a8a7dac40cf07faba1b6d06598d70ad7788aa4a4ae3b6438b2` |
| `samples/evidence/seeds.json` | `f8c3b2c1385b6d11520abd7d224181ed79d5b0e2719de300d95341c77bd82583` |
| `samples/scenario_handheld.json` | `f988784c8ee266e6dd28eb898cdbbfac0e2452cb01fd3929874fd866edc98795` |

### 7. Values and validation

**Summary.** 54 floors (3 with a FAIL row); **410 / 414 validation rows pass** (upper end of the two-sided 95 % seed-bootstrap interval (a one-sided 97.5 % limit) of the single-source false split ≤ 5 %); 4 FAIL; 0 not applicable. Pooled false split over all rows: 21,267 / 2,649,600 = 0.803 %. Worst: 1 m, `axis|D=2|C=4000|R=1|ideal`, cc, 274 / 6,400 = 4.28 % [2.70 %, 6.23 %].

Per floor (decoder × total counts × placement): F in the decoder's own image units (the primary floor, assigned to the hypothesised pair) and the hypothesis-free sensitivity floor; the selection N; the validation condition with the highest upper limit among the conditions this floor applies to, its false split k / N and the two-sided 95 % percentile bootstrap interval over seeds (2,000 resamples; the file stores only this interval, so a one-sided 95 % limit is not available); the verdict (FAIL when any of its conditions fails). The selection statistic at F is not recorded in the file (the rule bounds it at ≤ α per separation). All 414 condition rows stay in the JSON.

**1 m, cc** (16 floors)

| Placement | Total counts | Floor F | Hypothesis-free F | Selection N per separation | Conditions | Worst condition | False split (k / N) | Rate [95 % interval] | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| axis | 500 | 38 | 96 | 1,600 | 8 | Δ 2, 250 / source, 1 : 1 | 193 / 6,400 | 3.02 % [2.33 %, 3.75 %] | pass |
| axis | 1,250 | 170 | 191 | 1,600 | 8 | Δ 3, 250 / source, 1 : 4 | 184 / 6,400 | 2.88 % [2.36 %, 3.41 %] | pass |
| axis | 2,000 | 100 | 276 | 1,600 | 8 | Δ 2, 1,000 / source, 1 : 1 | 214 / 6,400 | 3.34 % [2.53 %, 4.25 %] | pass |
| axis | 5,000 | 538 | 620 | 1,600 | 8 | Δ 2.5, 1,000 / source, 1 : 4 | 231 / 6,400 | 3.61 % [2.83 %, 4.50 %] | pass |
| axis | 8,000 | 0 | 917 | 1,600 | 8 | Δ 2, 4,000 / source, 1 : 1 | 274 / 6,400 | 4.28 % [2.70 %, 6.23 %] | **FAIL** |
| axis | 20,000 | 1,984 | 2,280 | 1,600 | 8 | Δ 2.5, 4,000 / source, 1 : 4 | 171 / 6,400 | 2.67 % [1.52 %, 4.08 %] | pass |
| axis | 32,000 | 0 | 3,364 | 1,600 | 8 | Δ 2, 16,000 / source, 1 : 1 | 154 / 6,400 | 2.41 % [1.11 %, 4.16 %] | pass |
| axis | 80,000 | 7,720 | 8,916 | 1,600 | 8 | Δ 2.5, 16,000 / source, 1 : 4 | 106 / 6,400 | 1.66 % [0.56 %, 3.14 %] | pass |
| edge | 500 | 62 | 103 | 1,600 | 8 | Δ 2, 250 / source, 1 : 1 | 131 / 6,400 | 2.05 % [1.39 %, 2.73 %] | pass |
| edge | 1,250 | 199 | 220 | 1,600 | 8 | Δ 3, 250 / source, 1 : 4 | 135 / 6,400 | 2.11 % [1.56 %, 2.64 %] | pass |
| edge | 2,000 | 196 | 309 | 1,600 | 8 | Δ 2, 1,000 / source, 1 : 1 | 134 / 6,400 | 2.09 % [1.31 %, 2.89 %] | pass |
| edge | 5,000 | 666 | 755 | 1,600 | 8 | Δ 2, 1,000 / source, 1 : 4 | 120 / 6,400 | 1.87 % [1.11 %, 2.73 %] | pass |
| edge | 8,000 | 680 | 1,055 | 1,600 | 8 | Δ 2, 4,000 / source, 1 : 1 | 122 / 6,400 | 1.91 % [1.03 %, 2.89 %] | pass |
| edge | 20,000 | 2,430 | 2,920 | 1,600 | 8 | Δ 2, 4,000 / source, 1 : 4 | 151 / 6,400 | 2.36 % [1.00 %, 3.94 %] | pass |
| edge | 32,000 | 2,438 | 3,942 | 1,600 | 8 | Δ 2.5, 16,000 / source, 1 : 1 | 84 / 6,400 | 1.31 % [0.19 %, 2.64 %] | pass |
| edge | 80,000 | 9,555 | 11,608 | 1,600 | 8 | Δ 2, 16,000 / source, 1 : 4 | 129 / 6,400 | 2.02 % [0.56 %, 3.84 %] | pass |

**1 m, area@120** (16 floors)

| Placement | Total counts | Floor F | Hypothesis-free F | Selection N per separation | Conditions | Worst condition | False split (k / N) | Rate [95 % interval] | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| axis | 500 | 0.009932 | 0.0823 | 1,600 | 8 | Δ 2, 250 / source, 1 : 1 | 235 / 6,400 | 3.67 % [3.22 %, 4.16 %] | pass |
| axis | 1,250 | 0.01889 | 0.06783 | 1,600 | 8 | Δ 3, 250 / source, 1 : 4 | 244 / 6,400 | 3.81 % [3.22 %, 4.42 %] | pass |
| axis | 2,000 | 0 | 0.06026 | 1,600 | 8 | Δ 2, 1,000 / source, 1 : 1 | 82 / 6,400 | 1.28 % [1.00 %, 1.59 %] | pass |
| axis | 5,000 | 0.02236 | 0.05282 | 1,600 | 8 | Δ 2.5, 1,000 / source, 1 : 4 | 185 / 6,400 | 2.89 % [2.27 %, 3.58 %] | pass |
| axis | 8,000 | 0 | 0.06023 | 1,600 | 8 | Δ 2, 4,000 / source, 1 : 1 | 63 / 6,400 | 0.98 % [0.56 %, 1.47 %] | pass |
| axis | 20,000 | 0.04538 | 0.08726 | 1,600 | 8 | Δ 3, 4,000 / source, 1 : 4 | 168 / 6,400 | 2.62 % [1.64 %, 3.73 %] | pass |
| axis | 32,000 | 0 | 0.1237 | 1,600 | 8 | Δ 2, 16,000 / source, 1 : 1 | 31 / 6,400 | 0.48 % [0.16 %, 0.91 %] | pass |
| axis | 80,000 | 0.1524 | 0.2473 | 1,600 | 8 | Δ 2.5, 16,000 / source, 1 : 4 | 95 / 6,400 | 1.48 % [0.47 %, 2.78 %] | pass |
| edge | 500 | 0.01479 | 0.08597 | 1,600 | 8 | Δ 2, 250 / source, 1 : 1 | 169 / 6,400 | 2.64 % [2.20 %, 3.08 %] | pass |
| edge | 1,250 | 0.01409 | 0.07413 | 1,600 | 8 | Δ 3, 250 / source, 1 : 4 | 171 / 6,400 | 2.67 % [2.22 %, 3.16 %] | pass |
| edge | 2,000 | 0 | 0.06162 | 1,600 | 8 | Δ 2, 1,000 / source, 1 : 1 | 129 / 6,400 | 2.02 % [1.66 %, 2.39 %] | pass |
| edge | 5,000 | 0.007493 | 0.06392 | 1,600 | 8 | Δ 2.5, 1,000 / source, 1 : 4 | 210 / 6,400 | 3.28 % [2.56 %, 4.05 %] | pass |
| edge | 8,000 | 0 | 0.05951 | 1,600 | 8 | Δ 2, 4,000 / source, 1 : 1 | 67 / 6,400 | 1.05 % [0.73 %, 1.44 %] | pass |
| edge | 20,000 | 0 | 0.1043 | 1,600 | 8 | Δ 2.5, 4,000 / source, 1 : 4 | 150 / 6,400 | 2.34 % [1.45 %, 3.44 %] | pass |
| edge | 32,000 | 0 | 0.1182 | 1,600 | 8 | Δ 2, 16,000 / source, 1 : 1 | 33 / 6,400 | 0.52 % [0.25 %, 0.86 %] | pass |
| edge | 80,000 | 0 | 0.3069 | 1,600 | 8 | Δ 2.5, 16,000 / source, 1 : 4 | 93 / 6,400 | 1.45 % [0.59 %, 2.50 %] | pass |

**1 m, binary@8** (16 floors)

| Placement | Total counts | Floor F | Hypothesis-free F | Selection N per separation | Conditions | Worst condition | False split (k / N) | Rate [95 % interval] | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| axis | 500 | 0.001475 | 0.008364 | 1,600 | 7 | Δ 2, 250 / source, 1 : 1 | 213 / 6,400 | 3.33 % [2.84 %, 3.81 %] | pass |
| axis | 1,250 | 0.008521 | 0.01227 | 1,600 | 7 | Δ 2.5, 250 / source, 1 : 4 | 197 / 6,400 | 3.08 % [2.41 %, 3.84 %] | pass |
| axis | 2,000 | 0 | 0.01724 | 1,600 | 7 | Δ 2, 1,000 / source, 1 : 1 | 113 / 6,400 | 1.77 % [1.31 %, 2.27 %] | pass |
| axis | 5,000 | 0.02454 | 0.03179 | 1,600 | 7 | Δ 2.5, 1,000 / source, 1 : 4 | 212 / 6,400 | 3.31 % [2.25 %, 4.48 %] | pass |
| axis | 8,000 | 0 | 0.05056 | 1,600 | 7 | Δ 1.75, 4,000 / source, 1 : 1 | 40 / 6,400 | 0.63 % [0.31 %, 1.03 %] | pass |
| axis | 20,000 | 0.08976 | 0.1053 | 1,600 | 7 | Δ 2.5, 4,000 / source, 1 : 4 | 175 / 6,400 | 2.73 % [1.44 %, 4.22 %] | pass |
| axis | 32,000 | 0 | 0.1833 | 1,600 | 7 | Δ 1.75, 16,000 / source, 1 : 1 | 8 / 6,400 | 0.13 % [0.00 %, 0.30 %] | pass |
| axis | 80,000 | 0.3334 | 0.3913 | 1,600 | 7 | Δ 2.5, 16,000 / source, 1 : 4 | 223 / 6,400 | 3.48 % [1.44 %, 5.97 %] | **FAIL** |
| edge | 500 | 0.002342 | 0.009028 | 1,600 | 7 | Δ 2, 250 / source, 1 : 1 | 200 / 6,400 | 3.12 % [2.58 %, 3.73 %] | pass |
| edge | 1,250 | 0.004835 | 0.01548 | 1,600 | 7 | Δ 2.5, 250 / source, 1 : 4 | 207 / 6,400 | 3.23 % [2.56 %, 3.91 %] | pass |
| edge | 2,000 | 0 | 0.01703 | 1,600 | 7 | Δ 2, 1,000 / source, 1 : 1 | 209 / 6,400 | 3.27 % [2.50 %, 4.14 %] | pass |
| edge | 5,000 | 0.01217 | 0.04151 | 1,600 | 7 | Δ 2.5, 1,000 / source, 1 : 4 | 201 / 6,400 | 3.14 % [2.38 %, 3.87 %] | pass |
| edge | 8,000 | 0.01622 | 0.04653 | 1,600 | 7 | Δ 1.75, 4,000 / source, 1 : 1 | 159 / 6,400 | 2.48 % [1.75 %, 3.33 %] | pass |
| edge | 20,000 | 0.03979 | 0.1411 | 1,600 | 7 | Δ 3, 4,000 / source, 1 : 4 | 197 / 6,400 | 3.08 % [1.83 %, 4.44 %] | pass |
| edge | 32,000 | 0.06868 | 0.1596 | 1,600 | 7 | Δ 1.75, 16,000 / source, 1 : 1 | 93 / 6,400 | 1.45 % [0.69 %, 2.48 %] | pass |
| edge | 80,000 | 0.1583 | 0.5233 | 1,600 | 7 | Δ 2.5, 16,000 / source, 1 : 4 | 199 / 6,400 | 3.11 % [1.56 %, 5.14 %] | **FAIL** |

**5 m, cc** (2 floors)

| Placement | Total counts | Floor F | Hypothesis-free F | Selection N per separation | Conditions | Worst condition | False split (k / N) | Rate [95 % interval] | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| axis | 2,000 | 0 | 210 | 1,600 | 8 | Δ 3, 1,000 / source, 1 : 1 | 104 / 6,400 | 1.63 % [0.97 %, 2.44 %] | pass |
| axis | 8,000 | 0 | 673 | 1,600 | 8 | Δ 3, 4,000 / source, 1 : 1 | 149 / 6,400 | 2.33 % [1.27 %, 3.67 %] | pass |

**5 m, area@120** (2 floors)

| Placement | Total counts | Floor F | Hypothesis-free F | Selection N per separation | Conditions | Worst condition | False split (k / N) | Rate [95 % interval] | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| axis | 2,000 | 0 | 0.09187 | 1,600 | 8 | Δ 2, 1,000 / source, 1 : 1 | 187 / 6,400 | 2.92 % [2.44 %, 3.45 %] | pass |
| axis | 8,000 | 0 | 0.08198 | 1,600 | 8 | Δ 2, 4,000 / source, 1 : 1 | 77 / 6,400 | 1.20 % [0.78 %, 1.67 %] | pass |

**5 m, binary@8** (2 floors)

| Placement | Total counts | Floor F | Hypothesis-free F | Selection N per separation | Conditions | Worst condition | False split (k / N) | Rate [95 % interval] | Verdict |
|---|---|---|---|---|---|---|---|---|---|
| axis | 2,000 | 0 | 0.01446 | 1,600 | 7 | Δ 3, 1,000 / source, 1 : 1 | 113 / 6,400 | 1.77 % [1.16 %, 2.44 %] | pass |
| axis | 8,000 | 0 | 0.04118 | 1,600 | 7 | Δ 3, 4,000 / source, 1 : 1 | 43 / 6,400 | 0.67 % [0.22 %, 1.25 %] | pass |

**Every FAIL row** (4 of 414):

| Distance | Placement | Separation | Counts per source | Ratio | Decoder | False split (k / N) | Rate [95 % interval] | Verdict |
|---|---|---|---|---|---|---|---|---|
| 1 m | axis | Δ 2 | 4,000 | 1 : 1 | cc | 274 / 6,400 | 4.28 % [2.70 %, 6.23 %] | **FAIL** |
| 1 m | axis | Δ 2 | 16,000 | 1 : 4 | binary@8 | 218 / 6,400 | 3.41 % [1.11 %, 5.92 %] | **FAIL** |
| 1 m | axis | Δ 2.5 | 16,000 | 1 : 4 | binary@8 | 223 / 6,400 | 3.48 % [1.44 %, 5.97 %] | **FAIL** |
| 1 m | edge | Δ 2.5 | 16,000 | 1 : 4 | binary@8 | 199 / 6,400 | 3.11 % [1.56 %, 5.14 %] | **FAIL** |

### 8. Reproduce

```bash
dotnet build Gcam.sln -c Release
python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-angres-v2.json --out <dir> --family angres_floor_select_1m angres_floor_1m angres_floor_select_5m angres_floor_5m
python samples/evidence/angres/aggregate_angres.py --runs <dir> --manifest samples/evidence/manifest-angres-v2.json --floor angres_floor_select_1m angres_floor_1m angres_floor_select_5m angres_floor_5m --out <floor.json>   # = angres-v2-floor.json
python samples/evidence/calibration_record.py            # rewrites this block
python samples/evidence/calibration_record.py --check    # verifies it
```
<!-- CAL-04 generated: end -->
