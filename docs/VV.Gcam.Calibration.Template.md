# VV.Gcam.Calibration.Template — the form every calibration record follows

Scope: the fixed form for recording a **calibration** in the Gcam V&V set — any number that is not derived from
physics alone but **selected from data** (thresholds, floors, ratios, iteration counts) and then relied on by a
requirement. Each calibration gets one record `CAL-nn` in [VV.Gcam.Calibration](VV.Gcam.Calibration.md), filled in this
form. The tables of a record are **generated** from the committed selection and validation files by
`samples/evidence/calibration_record.py`, never typed by hand; the prose fields are written by a person and say so.

**At a glance**
- Why a form: several requirements now rest on values chosen per configuration on Monte Carlo data (PR-SENS-02's trust
  thresholds, PR-NRG-04's stripped-image thresholds, PR-IMG-04's significance floor). A product would carry these as
  calibration tables; each must say what it is for, how it was chosen, on which data, how it was checked, and when it
  must be redone.
- Rule of the form: **selection and validation never share seeds**; every number in a table carries its N; every data
  file is pinned by SHA-256 of the bytes git stores; a failed validation row stays in the record, marked.
- A record is complete only when every field below is filled or explicitly "not applicable — because …".

## The form

Copy this block for a new record. Sections 1–5 are written by a person (a draft is marked *(draft — for review)* until
signed off); sections 6–8 sit between one pair of markers and are produced by the generator from the files pinned in
`samples/evidence/calibration-records.json`; the generator refuses to write a record whose pinned hashes or
cross-checks fail.

```markdown
## CAL-nn — <what is calibrated, one line>

| Field | Value |
|---|---|
| Record | CAL-nn |
| Used by | <VV IDs only: requirements, evidence entries, decisions — e.g. PR-SENS-02, EV-34, D-44> |
| Quantity | <what the calibrated number is, its unit, and which way it acts (e.g. "trust when Z ≥ T")> |
| Configuration key | <the axes one value is chosen per, e.g. head × distance × exposure × field × bound × window> |
| Status | valid / superseded by CAL-mm / invalid — <reason> |

### 1. Purpose and acceptance
What the value must guarantee, as a testable statement with its target (e.g. "≤ 1 % false trusted locations per
acquisition on background-only data"), the **pass criterion** used in validation and the **interval method** (e.g.
"one-sided 95 % Clopper–Pearson upper limit ≤ 1 %", "seed-bootstrap upper 95 % limit ≤ 5 %"). Written by a person.

### 2. Selection rule
The rule that turns data into the value (statistic, level α, inclusive / exclusive, tie handling, the candidate set),
and why α sits where it does relative to the acceptance target. Written by a person.

### 3. Use between and beyond the grid
Validated only at the grid points of the configuration key. A configuration between grid points uses the **more
conservative neighbouring value** (the higher threshold or floor); a configuration outside the grid has **no valid
calibration**. State here what "more conservative" means for this quantity. Written by a person.

### 4. Re-calibration triggers
What change invalidates the record (decoder, window, head geometry, background model, forward model, iteration count,
…) — the record is then marked superseded and a new CAL is made. Written by a person.

### 5. Known limits
What the calibration does not cover. Written by a person.

<!-- CAL-nn generated: begin -->
### 6. Data
*(generated)* Date measured (from the manifest); selection family, seed list and range, N per configuration;
validation family, seed list and range, N per configuration; **seed overlap: none** (checked); **value fixed before
validation: yes / no** (the validation request pins the selection file by hash); every file read with its SHA-256;
engine commit of the runs — or "not recorded; the code landed in <commit>".

### 7. Values and validation
*(generated)* One row per configuration key: the value, the selection statistic at the value with its N, the validated
rate k / N, its interval, the verdict (**pass / FAIL / not applicable — reason**). Summary line first: passed / judged,
not-applicable count, pooled rate, worst row.

### 8. Reproduce
*(generated)* The commands that rebuild the selection file and the validation summary from the seed runs, and the
generator command that rebuilds this record.
<!-- CAL-nn generated: end -->
```

## Field rules

| Rule | Why |
|---|---|
| Tables are generated, prose is written | a copied threshold table drifts from the file the code reads; prose (purpose, limits) needs judgement |
| Selection and validation seeds are disjoint, and the generator checks it | a value validated on its own selection data passes by construction |
| Every N is printed beside its rate | a rate without N cannot be judged |
| A FAIL row stays visible | removing it would turn a calibration into a claim |
| Hashes are of the bytes git stores (LF) | a CRLF checkout must not change a pinned hash |
| Superseded records are kept, marked | the evidence register and decisions cite them by ID |
| A record never prints a file's own description strings | they may name working documents; the V&V set is self-contained |
| "Not applicable" needs a reason | e.g. a configuration with no counts at all tests nothing and is not counted as a pass |
