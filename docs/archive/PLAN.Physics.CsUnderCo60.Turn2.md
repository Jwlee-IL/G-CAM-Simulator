# PLAN.Physics.CsUnderCo60.Turn2 — implementation and seed ensembles (TODO-35 turn 2)

Scope: the implementer's turn-2 report for [PLAN.Physics.CsUnderCo60](PLAN.Physics.CsUnderCo60.md), decisions
DA-1 … DA-12 ("Decisions after review"). By the substitute implementer, a Claude subagent. Every number below gives its
expectation, its tolerance and how the tolerance was derived. No plan, review, Todo, VV, AGENTS, CLAUDE or README file
was edited. EV-15 was not touched (DA-13 is the planner's).

Status: implemented, measured, 2026-10-06. Not committed (git state unchanged).

## At a glance

- **No material disagreement with DA-1 … DA-12.** One flag on DA-7 (see "DA-7 check"): the side window holds a
  measurable Cs-137 tail, 3.84 × 10⁻⁴ of the 662-window Cs counts. It is the 3.4 σ Gaussian tail of the resolution
  model. It lowers the stripped Cs signal by 0.033 %, and the estimator carries it exactly. I judged it immaterial and
  continued; **the planner should confirm** that this is not the "tail" DA-7 meant to stop on.
- **Q1 — the count detection limit is validated.** Exact Currie L_D was computed per condition and per seed. All 864
  rule-A checks (false positives at Cs = 0, detection at the exact L_D) lie within 4 SE of their exact expectation,
  pooled N = 128 000 each. The 864 z-scores have mean +0.03 and SD 1.01.
  - At the claim ceiling, 10 µSv/h at the head (Co-60 28.7 MBq at 1 m), side window, co-located, ideal field:
    L_D = 75.5 / 181 / 402 Cs-137 counts, i.e. A_D = 4.0 / 1.61 / 0.71 MBq, at 10 / 60 / 300 s.
  - As a ratio, k_D = Co : Cs = 7.1 / 17.9 / 40.3.
- **Q2 — stripped imaging works where the raw window cannot.**
  - The new statistic Z_s uses per-pixel R_i and 144 per-configuration thresholds (3.50 … 5.33), selected on 65 536
    Cs-free nulls each. On 8 192 fresh nulls per configuration, 144 / 144 meet the ≤ 1 % false-trusted limit; the worst
    upper limit is 0.61 %, and the pooled rate 0.284 %.
  - At 10 µSv/h, sources two elements apart, ≥ 95 % trusted and correct needs S = 500 / 1000 / 2000 Cs-137 counts at
    10 / 60 / 300 s (k ≈ 1.0 / 3.1 / 7.6). That is about 5–7× the count L_D.
  - Raw window through the PR-SENS-02 gate, Cs-free: it trusts a false "Cs" at Co-60's position in 92–100 % of
    acquisitions at 10 µSv/h (0.10 µSv/h ambient, 10–60 s). Z_s does so in ≤ 0.06 %.
- **Q3 — the side window removes the dominant systematic.**
  - Per 1 % gain error, R moves by +0.8 % (side), −13 % (co1332), −3.6 % (both photopeaks).
  - At 10 µSv/h, 60 s, a +1 % gain error lifts the count false-positive rate to 8 % with the side window. With co1332
    a −1 % error lifts it to 93 % and a +1 % error kills detection (0.4 % at L_D).
  - The side window's R_i is flat across the array (0.851 → 0.862 edge to centre). co1332's runs 0.92 → 0.55.
  - Direction moves the side window's R by ≤ 0.1 % out to 3.5°.
  - A δR of −10 % leaves a trusted false "Cs" at Co-60 in 2.2 % of Cs-free acquisitions at 10 µSv/h, 300 s
    (28 % at 35 µSv/h); |δR| ≤ 5 % gives ≤ 0.2 % inside the claim.
- **Ambient (0.10 µSv/h, bare bound):** raises L_D only at weak Co. At 60 s: Co = 0: 3.0 → 22.8 counts; 1 MBq: +16 %;
  ≥ 10 MBq: ≤ 2 %. The background model's MC error at 300 s is a per-seed bias with quartiles ±0.15 σ₀ at Co = 0 and
  negligible at Co ≥ 1 MBq.
- **Tests:** 703 passed / 22 skipped (725) → **723 passed / 22 skipped (745)**, 0 failed (`dotnet test Gcam.sln -c
  Release`, full).

## Files by group

| Group | File | What |
|---|---|---|
| Engine | `src/Gcam.Simulation/CsUnderCoStudy.cs` (new) | the `csco` family (DA-1): maps, count mode, imaging nulls / sources, residual study, ratio and ambient summaries |
| Engine | `src/Gcam.Simulation/StrippingStatistics.cs` (new) | general-form Currie (normal and exact two-Poisson), exact Poisson sampler (Knuth < 10, PTRS ≥ 10), ln Γ, gain-shifted window |
| Engine | `src/Gcam.Simulation/StrippedSearch.cs` (new) | the Z_s statistic on the decoder grid (per-pixel R_i, plug-in variance, E₀) |
| Engine | `src/Gcam.Simulation/AmbientEvidenceRequest.cs` | `CsUnderCoSpec` / `CsCoScene` added; existing members unchanged |
| CLI | `src/Gcam.Cli/Commands/AmbientCommands.cs` | one dispatch line `"csco" => CsUnderCoStudy.Run(request)` in `ambient-evidence` |
| Tests | `tests/Gcam.Tests/CsUnderCoTests.cs` (new) | 20 tests (DA-10), tolerances derived in comments |
| Evidence inputs | `samples/evidence/ambient/csco-request-v1-selection.json`, `csco-request-v1-validation.json`, `samples/evidence/manifest-csco-v1.json` (new) | requests and manifest (families `csco_selection` F256[1:65], `csco_validation` F256[128:256]) |
| Evidence scripts | `samples/evidence/ambient/select_csco_thresholds.py`, `aggregate_csco.py` (new) | AB-11 selection (imports `select_thresholds.inclusive_threshold`), aggregation |
| Evidence results | `samples/evidence/ambient/csco-thresholds-v1.json` (new, pinned by SHA-256 in the validation request) | the 144 selected Z_s thresholds with their selection report |
| Evidence results | `samples/evidence/results/csco-v1-validation.json` (new, 3.3 MB) | the aggregated validation summary (128 seeds) |
| Report | `docs/PLAN.Physics.CsUnderCo60.Turn2.md` (this file) | |

Raw per-seed outputs (64 + 128 runs) stay in `%TEMP%\gcam-todo35\runs\` (not committed).

## What was run

- **Geometry:** hand-held head (`samples/scenario_handheld.json`: 16 × 16 @ 1 mm, 15 mm GAGG:Ce,Mg, 7 % FWHM at 662 keV
  scaled as √E, D = 55 mm, rank-7 2 × 2 mosaic, 10 mm W). Source–detector distance 1000 mm (S = 945 mm, the gate's
  convention, DA-2). One element = atan(1/55) = 1.0416°.
- **Scenes in elements:** co-located (0, 0); "sep2" Cs (−1, 0), Co (+1, 0); "sep5" Cs (−2.5, 0), Co (+2.5, 0).
- **Co-60 grid (DA-3):** 0, 0.1, 1, 3, 10, 28.72, 100 MBq. 28.72 MBq is the activity giving 10 µSv/h H*(10) at the head,
  computed as 0.34818 µSv/h per MBq (ICRP 74 table in `AmbientDose.PerFluence`, unattenuated). 100 MBq ≈ 34.8 µSv/h is
  beyond the claim (DA-11).
- **Live times and field:** 10 / 60 / 300 s. Ideal field, and 0.10 µSv/h in both bounds; front-only only at
  Co ≤ 0.1 MBq (DA-9).
- **Windows** on the event's total deposit: cs662 595.53–727.87 keV; references side 727.87–860.21 (product, DA-7),
  co1332 1199.25–1465.75 and coBoth 1055.88–1465.75 (comparisons). Every window is also scaled by 1/(1+g) for
  g = −2, −1, +1, +2 %, which is exact for a gain error (test `ForGain_IsTheWindowOfAScaledPulseHeight`).
- **Maps per seed:**
  - "truth" maps at 2 × 10⁶ photons per line;
  - calibration maps of declared precision at 4 × 10⁶ per line (on axis, and at each scene's Co direction);
  - ambient truth and model at 5 × 10⁵ histories each per bound.
- **Seeds:**
  - Selection: 64 seeds (F256[1:65]) × 1024 Cs-free nulls per configuration = 65 536 per configuration.
  - Validation: 128 seeds (F256[128:256], disjoint from selection), with:
    - 64 nulls per configuration (8 192 pooled);
    - 1000 two-window count acquisitions per Cs multiple and per gain;
    - 200 imaging acquisitions per Cs count (25 600 pooled);
    - 200 residual acquisitions per variant (25 600 pooled).
- **Wall time:** 6.6 min (selection, 22 jobs) + 39.6 min (validation, 22 jobs). One seed alone takes 39 s (selection)
  and 103 s (validation). Well inside the 3 h budget, so no reduced N.

Reproduce:

```bash
dotnet build Gcam.sln -c Release
python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-csco-v1.json --out <dir> --family csco_selection --jobs 22
python samples/evidence/ambient/select_csco_thresholds.py --runs <dir> --family csco_selection --out samples/evidence/ambient/csco-thresholds-v1.json
python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-csco-v1.json --out <dir> --family csco_validation --jobs 22
python samples/evidence/ambient/aggregate_csco.py --runs <dir> --family csco_validation --out samples/evidence/results/csco-v1-validation.json
```

## DA-7 check — Cs-137 in the side window

Measured Cs-137 counts in the side window per Cs-137 count in cs662: **3.8436 × 10⁻⁴**, identical in all scenes and
seeds (N = 128).

- **Expectation:** the Gaussian tail of the resolution model. The window starts 66.2 keV above 661.7 keV. σ(661.7) =
  0.07 × 661.7 / 2.3548 = 19.67 keV, so the edge is 3.37 σ away and 1 − Φ(3.37) = 3.8 × 10⁻⁴. Agreement to the
  displayed digits.
- **Effect:** the stripped Cs signal is S·(1 − R·leak) = S·(1 − 3.3 × 10⁻⁴). `CurrieNormal` / `CurrieExact` and the
  count acquisitions include the leak exactly (mean and, in the normal form, variance).
- **Judgement:** immaterial (0.033 % against ≥ 10 % statistical precision at L_D), so I continued. Not modelled: any
  real high-energy tail beyond the Gaussian, such as pile-up. That belongs to the four-channel readout (TODO-19,
  DA-12).

## Q1 — detection limit (count mode)

Definitions (DA-4):
- Y = n₆₆₂ − R·n_ref, with σ₀² = μ₆₆₂|H₀ + R²μ_ref.
- Exact critical level: Y_C = the smallest support value of Y with P(Y > Y_C | H₀) ≤ 5 %.
- Exact L_D = the smallest Cs count S in cs662 with P(Y > Y_C | S) ≥ 95 %.
- Normal form for comparison: L_D = 2.706 + 3.29 σ₀ (with the leak).

L_D is quoted as the median over 128 seeds; quartiles are within ±0.5 % everywhere shown. k_D = A_Co / A_D.

**Side window (product), co-located scene, Cs-137 counts / activity A_D / k_D:**

| Co-60 at 1 m (µSv/h at head) | ideal 10 s | ideal 60 s | ideal 300 s | bare 0.10 µSv/h, 60 s |
|---|---|---|---|---|
| 0 | 3.0 / 160 kBq | 3.0 / 26.6 kBq | 3.0 / 5.3 kBq | 22.8 / 202 kBq |
| 0.1 MBq (0.035) | 6.3 / 336 kBq / 0.30 | 13.2 / 117 kBq / 0.85 | 26.1 / 46 kBq / 2.15 | 25.4 / 225 kBq / 0.44 |
| 1 MBq (0.35) | 16.2 / 862 kBq / 1.16 | 35.9 / 319 kBq / 3.13 | 77.1 / 137 kBq / 7.3 | 41.5 / 369 kBq / 2.71 |
| 3 MBq (1.04) | 26.1 / 1.39 MBq / 2.15 | 60.3 / 536 kBq / 5.6 | 132 / 234 kBq / 12.8 | 63.7 / 566 kBq / 5.3 |
| 10 MBq (3.48) | 45.6 / 2.43 MBq / 4.1 | 108 / 959 kBq / 10.4 | 238 / 423 kBq / 23.6 | 110 / 975 kBq / 10.3 |
| **28.7 MBq (10.0, claim ceiling)** | **75.5 / 4.02 MBq / 7.1** | **181 / 1.61 MBq / 17.9** | **402 / 713 kBq / 40.3** | 182 / 1.62 MBq / 17.7 |
| 100 MBq (34.8, beyond claim) | 139 / 7.38 MBq / 13.5 | 336 / 2.98 MBq / 33.6 | 747 / 1.33 MBq / 75.4 | 336 / 2.99 MBq / 33.5 |

- **Other windows, same scene, ideal, 60 s, at 1 / 10 / 28.7 / 100 MBq:**
  - co1332: 34.5 / 103 / 173 / 321 counts;
  - coBoth: 30.8 / 91.5 / 153 / 284 counts;
  - side: 35.9 / 108 / 181 / 336 counts.

  The side window costs **+4.1–4.6 %** in L_D against co1332 and +17 % against coBoth. As predicted: σ₀² ≈ D(1 + R)
  with R = 0.856 / 0.700 / 0.323.
- **Fixed k (DA-3 statement):** at fixed k = Co : Cs and no field, S_D in counts does not depend on t. Example: k_D =
  2.15 at (0.1 MBq, 300 s) and at (3 MBq, 10 s), both with S_D = 26.1 counts. Hence A_D ∝ 1/t at fixed k, while at
  fixed Co A_D ∝ √(A_Co / t) (ratio of 60 s / 300 s A_D at 28.7 MBq: 2.25 against √5 = 2.24).
- **Separated scenes:** L_D in counts is within 1 % of co-located (35.7 / 34.6 vs 35.9 at 1 MBq, 60 s). A_D is higher
  by the off-axis Cs efficiency: Cs-137 cs662 rate 1.876 / 1.776 / 1.550 × 10⁻⁶ cps/Bq co-located / sep2 / sep5.
- **Window counting** (no stripping, CC-6): Co-60 reads as **0.489** Bq of Cs-137 per Bq on axis (0.513 sep2, 0.549
  sep5). At 10 µSv/h, 60 s, that is 1 582 Cs-equivalent counts, about 8.7 × L_D. Window counting has no detection limit
  in this sense; its error is this bias.

**Validation (rule A, the exact a-priori decision with the true R):**
- Expectation: the false-positive rate at Cs = 0 is the exact actual α per seed (≤ 5 %; < 5 % only where the
  distribution is discrete, e.g. 0.50 % for front-only, Co = 0, 10 s). The detection rate at the exact L_D is 0.95.
- Tolerance: 4 binomial SE at the pooled N = 128 000 (1 000 × 128): ±0.24 points at 5 %, ±0.24 at 95 %.
- Result: **864 / 864 within tolerance** (432 conditions × 2), z-scores in −2.91 … +3.52, mean +0.03, SD 1.01 — as a
  standard normal should be.

**The two plug-in rules** (what an instrument computes; no a-priori σ₀):
- (P) (Y − model) / √(n₆₆₂ + R²n_ref) > 1.645 with the true R;
- (B) the same with the calibrated R, from an independent 4 × 10⁶-photon on-axis map.

Results (DA-4 b):
- At counts ≥ ~30 the plug-in rules match rule A: false positives 4.5–5.3 % (P); detection at L_D 0.94–0.95.
- At very low counts the plug-in is conservative: ideal, Co = 0, detection at L_D = 3 counts is 0.58, because
  √n > 1.645 needs n ≥ 3.
- The calibrated R's precision (R SD over seeds: 0.27 % side, 0.26 % co1332, 0.25 % coBoth) raises the false-positive
  rate only at the highest counts. Maximum 9.4 % (coBoth, 100 MBq, 300 s); side at 10 µSv/h, 300 s: 5.6 %. That is the
  DA-4 b answer: a calibration of this precision is adequate within the claim.

## Q2 — imaging: stripped statistic vs raw window

**Thresholds (DA-5).**
- 144 configurations (3 scenes × 3 t × environments × Co levels) give T* = 3.50 … 5.33. Front-only and low-count
  configurations go down to 1.42. Nine ideal Co = 0 configurations have no counts at all, so any candidate is trusted
  there.
- Rule: AB-11 inclusive, α = 0.003, on 65 536 selection nulls per configuration.
- The threshold rises with the Co-60 counts. Examples, sep2, ideal: 4.50 (1 MBq, 60 s) → 4.61 (10 µSv/h, 60 s) →
  4.82 (10 µSv/h, 300 s) → 5.33 (sep5, 100 MBq, 300 s). This is the residual structure of the calibration's per-pixel
  noise and of R_i taken on axis.

**Null validation (seeds disjoint from selection).**
- Expectation ≤ 0.3 % false trusted. Pass criterion: one-sided 95 % Clopper–Pearson upper limit ≤ 1 % (PR-SENS-02).
- **144 / 144 pass**; pooled 3 350 / 1 179 648 = 0.284 %.
- Worst: sep5, 60 s, bare, 1 MBq — 38 / 8 192 = 0.46 %, upper limit 0.61 %.
- Z_s trusted *at Co-60's position* on Cs-free acquisitions: ≤ 0.06 % in every configuration.

**Raw-window counter-case (DA-6)**, PR-SENS-02 gate (`gate-thresholds-v2.json`, head1m, cs662, bare 0.10 µSv/h; gate
thresholds exist only at 10 / 60 s under the field). Cs-free acquisitions, share trusted at Co-60's position, N = 8 192
each:

| Co-60 | co-located 10 s / 60 s | sep2 10 s / 60 s | sep5 10 s / 60 s |
|---|---|---|---|
| 1 MBq | 3.2 % / 1.8 % | 3.5 % / 1.7 % | 2.0 % / 0.9 % |
| 3 MBq | 12.8 % / 27.3 % | 12.1 % / 24.2 % | 7.9 % / 10.9 % |
| 10 MBq | 46 % / 98.5 % | 44.5 % / 98.0 % | 29.2 % / 85.6 % |
| 28.7 MBq (10 µSv/h) | 93.4 % / 100 % | 92.3 % / 100 % | 75.6 % / 100 % |
| 100 MBq | 100 % / 100 % | 100 % / 100 % | 99.8 % / 100 % |

**Sources: ≥ 95 % "trusted and within one element of Cs-137"** (stripped). Smallest count on the grid 100 / 250 / 500 /
1000 / 2000 / 4000. The grid steps are factors of ~2, so each entry is an upper bound within one step. Per-cell rates
are in the summary, N = 25 600.

| Scene, field | t | Co 0 | 1 MBq | 10 MBq | 28.7 MBq (10 µSv/h) | 100 MBq |
|---|---|---|---|---|---|---|
| sep2, ideal | 10 s | 250 | 250 | 500 | 500 (k 1.0) | 1000 |
| sep2, ideal | 60 s | 250 | 500 | 1000 | 1000 (k 3.1) | 2000 |
| sep2, ideal | 300 s | 250 | 500 | 2000 | 2000 (k 7.6) | 4000 |
| sep5, ideal | 10 / 60 / 300 s | 250 / 250 / 250 | 250 / 500 / 500 | 500 / 1000 / 2000 | 500 / 1000 / 2000 | 1000 / 2000 / 4000 |
| sep2, bare 0.10 | 10 / 60 / 300 s | 250 / 250 / 500 | 250 / 500 / 1000 | 500 / 1000 / 2000 | 500 / 1000 / 2000 | 1000 / 2000 / 4000 |
| co-located (detection), ideal | 10 / 60 / 300 s | 250 / 250 / 250 | 250 / 500 / 500 | 500 / 1000 / 2000 | 500 / 1000 / 2000 | 1000 / 2000 / 4000 |

- **Comparison with counting:** imaging needs 5–7× the count L_D. At 10 µSv/h: 500 / 75.5 = 6.6 (10 s), 1000 / 181 =
  5.5 (60 s), 2000 / 402 = 5.0 (300 s).
- **Same acquisitions, raw window** (sep2, bare, gate trusted and within one element of Cs). At 10 µSv/h, 60 s:
  - S = 1000: 0.03 raw against 0.98 stripped;
  - S = 2000: 0.88 raw;
  - at 100 MBq the raw window never reaches 95 % on the grid.
- **Raw decoder argmax without a gate** (sep2, ideal, 10 µSv/h): needs 2000 counts at 60 s (stripped 1000), and does not
  reach 95 % at 300 s even with 4000.
- **Co-located:** the raw decoder is "correct" at Co-60's own position without any Cs, so its co-located entries are
  not reported as location (DA-5). The stripped co-located entries are detection against the calibrated Cs-free null.
- **Edge of field (sep5, ±2.6°):** costs ~12 % Cs efficiency (A_D higher) but no grid step in counts against sep2.

## Q3 — systematics of R

**R by window, gain, direction, pixel.** Mean over 128 seeds; per-seed SD 0.0023 / 0.0018 / 0.0008.

| Window | R on axis | g −2 % | g −1 % | g +1 % | g +2 % | ring R edge → centre (8 rings) | direction (worst of 5, to 3.5°) |
|---|---|---|---|---|---|---|---|
| side | 0.8561 | −1.80 % | −0.87 % | +0.81 % | +1.56 % | 0.851 → 0.862 (flat) | ≤ 0.1 % (median; quartiles ±0.45 %) |
| co1332 | 0.7000 | +18.9 % | +11.2 % | −13.0 % | −25.2 % | 0.921 → 0.554 | −1.56 % at 2.4° diagonal (quartiles −2.0, −1.3) |
| coBoth | 0.3234 | +8.2 % | +3.9 % | −3.6 % | −7.0 % | 0.399 → 0.267 | −1.07 % at 2.4° diagonal |

- **Expectation and tolerance:** the turn-1 scoping run (8 seeds) gave side ∓0.9 % / co1332 −13 % / coBoth −3.6 % per
  1 % gain. The 128-seed means agree within their SE: 0.002 in R, from the SD over √128.
- **Why the side window is flat in R_i:** both its windows are continuum. Edge escape changes the photopeak (co1332)
  but hardly the continuum ratio.
- **Per-pixel R_i noise floor:** RMS of R_i(truth)/R_i(calibration) − 1 over pixels is 8.1 % on axis (both maps on
  axis, so pure MC noise: 2 × 10⁶ vs 4 × 10⁶ photons per line). The direction maps give 8.1–8.6 %, the same.
  **Direction dependence of single-pixel R_i is therefore not resolvable at this precision.** The ring profiles above
  are the resolvable form, and they show none for the side window.

**Per-direction calibration (DA-8; "Done when": does per-direction R remove the separated bias?).**
- Count mode, 10 µSv/h, 300 s, calibrated axis R. Stripped H₀ bias:
  - side: −5.5 / −3.9 / +2.9 counts (co-located / sep2 / sep5), quartiles ±25, against σ₀ = 121;
  - co1332: +4.8 / −2.1 / −16.4 counts.

  So there is no separated-scene bias to remove with the side window inside the fully coded field. co1332 shows the
  expected one at 2.6° (R −0.22 %).
- Image: expected Z at Co-60 with R_i calibrated at Co-60's own direction ("Ri=direction") is |Z| ≤ 0.07 in every
  configuration (≤ 0.04 within the claim), against ≤ 0.18 (≤ 0.10) with the on-axis R_i at the same δR = 0. The per-direction R_i removes the small
  residual, but the residual was already below the threshold scale (Z_s* ≈ 4.5–5.3).

**Gain error in count mode** (side vs comparisons, co-located, ideal; nominal R and threshold, acquisitions in the
shifted windows; N = 128 000 each). Rows: g = −2 / −1 / +1 / +2 %.

| Co-60, t | side: bias / σ₀ | side: false-positive rate | co1332: false-positive rate | co1332: detection at L_D | coBoth: false-positive rate |
|---|---|---|---|---|---|
| 1 MBq, 60 s | −0.10 / −0.05 / +0.04 / +0.08 | 4.1 / 4.6 / 5.4 / 5.8 % | 24 / 14 / 0.6 / 0.0 % | 0.99 / 0.99 / 0.82 / 0.51 | 11 / 7.0 / 2.4 / 1.4 % |
| 10 MBq, 60 s | −0.33 / −0.15 / +0.14 / +0.26 | 2.5 / 3.6 / 6.6 / 8.4 % | 92 / 59 / 0.0 / 0.0 % | 1.00 / 1.00 / 0.20 / 0.00 | 48 / 19 / 0.8 / 0.1 % |
| **28.7 MBq, 10 s** | −0.23 / −0.11 / +0.09 / +0.18 | 3.0 / 4.0 / 6.0 / 7.0 % | 67 / 36 / 0.0 / 0.0 % | 1.00 / 1.00 / 0.48 / 0.02 | 29 / 13 / 1.4 / 0.3 % |
| **28.7 MBq, 60 s** | −0.56 / −0.26 / +0.23 / +0.44 | 1.4 / 2.9 / 8.0 / 11.5 % | 100 / 94 / 0.0 / 0.0 % | 1.00 / 1.00 / 0.004 / 0.00 | 85 / 38 / 0.2 / 0.0 % |
| **28.7 MBq, 300 s** | −1.24 / −0.58 / +0.52 / +0.98 | 0.3 / 1.6 / 13.8 / 26.3 % | 100 / 100 / 0.0 / 0.0 % | 1.00 / 1.00 / 0.00 / 0.00 | 100 / 90 / 0.0 / 0.0 % |
| 100 MBq, 60 s | −1.04 / −0.49 / +0.43 / +0.82 | 0.5 / 1.9 / 11.7 / 21.2 % | 100 / 100 / 0.0 / 0.0 % | 1.00 / 1.00 / 0.00 / 0.00 | 100 / 80 / 0.0 / 0.0 % |

The rates follow from the bias. Expected false-positive rate = 1 − Φ(1.645 − b/σ₀): b/σ₀ = +0.52 gives 13.0 % against
13.8 % measured.

**Consequence for the gain requirement within the claim (side window):** keeping the count false-positive rate ≤ 10 %
at 10 µSv/h needs b/σ₀ ≤ 1.645 − 1.282 = 0.36, i.e. |g| ≲ 1.6 % at 60 s and |g| ≲ 0.7 % at 300 s, from the measured
bias slope 0.23 σ₀ (60 s) and 0.52 σ₀ (300 s) per +1 %. With co1332 the same rate would need |g| ≲ 0.12 % (bias
+3.1 σ₀ per −1 % at 60 s).

**Residual image of a wrong R** (Cs-free, ideal, R_i × (1 + δR) on axis, false-trusted at Co-60's position; N = 25 600
per cell). Expected Z at Co-60 grows as |δR|·√(P_ref): ≈ 0.14 per 1 % at P_ref = 6 400 reference counts and 0.31 at
32 000, ratio 2.2 against √5 = 2.24.

| δR | co-located 10 µSv/h 60 s (P_ref 1 849) | co-located 10 µSv/h 300 s (9 243) | sep2 10 µSv/h 300 s (9 167) | co-located 100 MBq 300 s (32 183) |
|---|---|---|---|---|
| −10 % | 0.2 % (E[Z] +0.82) | 2.2 % (+1.84) | 2.3 % (+1.69) | 27.8 % (+3.44) |
| −5 % | 0.0 % (+0.42) | 0.1 % (+0.95) | 0.2 % (+0.84) | 1.0 % (+1.77) |
| −2 % | 0.0 % | 0.0 % | 0.0 % | 0.1 % |
| +1 … +10 % | 0.0 % | 0.0 % | ≤ 0.1 % | 0.0 % |
| gain ±1 / ±2 % | 0.0 % | 0.0 % | ≤ 0.1 % | ≤ 0.1 % (E[Z] −0.51 … +0.62) |

- A positive δR over-subtracts and only lowers Z at Co-60.
- Inside the claim the image hot spot needs δR ≲ −10 % (count bias ≈ −0.1·R·P_ref, e.g. −790 counts = −6.5 σ₀ at
  300 s). Long before that, the **count** decision flags it.
- With the side window the gain-driven δR is < 2 %, so no hot spot is expected within the claim. This matches turn 1:
  the false hot spot is weak in the image because tungsten passes ~35 % at 1.25 MeV, and the crystal blurs the
  downscatter.

## Ambient field (DA-9)

- **Rates per µSv/h** (128 seeds, mean): cs662 4.287 (bare) / 0.104 (front-only) cps; side 2.673 / 0.0841; co1332
  2.220 / 0.0949; coBoth 3.989 / 0.167. They agree with EV-34's 4.28 / 0.104 within the SE of 0.056 / 0.0037.
- **Effect on L_D (side, co-located, bare 0.10 µSv/h vs ideal):**
  - 60 s: Co = 0: 22.8 vs 3.0; 1 MBq: 41.5 vs 35.9 (+16 %); 10 MBq: +1.8 %; 28.7 MBq: +0.7 %.
  - 300 s: Co = 0: 47.7 vs 3.0; 28.7 MBq: +0.6 %.
- Front-only (Co ≤ 0.1 MBq): Co = 0: 4.6 / 5.9 / 9.9 counts at 10 / 60 / 300 s.
- **Background model MC error at 300 s** (500 k histories). Per-seed bias of the stripped background
  (truth − model, side, Co = 0) over σ₀:

  | Bound | 10 s | 60 s | 300 s |
  |---|---|---|---|
  | bare | +0.005 (IQR 0.06) | +0.013 (IQR 0.13) | +0.03 (IQR 0.30) |
  | front-only | — | — | −0.003 (IQR 0.13) |

  At bare 300 s, Co = 0, its effect on the plug-in false-positive rate is 5.4 % against 5.0 % expected. At
  Co ≥ 1 MBq the same counts are < 3 % of σ₀. **The model budget suffices within the claim.** A Co = 0, 300 s, bare
  quote should carry the ±0.15 σ₀ quartile; 4× the histories would halve it.

## Tests (DA-10) — tolerances

| Test | Checks | Tolerance and derivation |
|---|---|---|
| `LogGamma_MatchesFactorials` | ln Γ(n+1) = ln n!, n ≤ 30 | 10 decimals (Stirling series accurate to ~1e-15 relative at x ≥ 7) |
| `Poisson_HasPoissonMeanVarianceAndTail` (λ = 4, 40, 4000) | the exact sampler | 4 SE: SE(mean) = √(λ/M); SE(var) = λ√(2/(M−1) + 1/(λM)) (Poisson excess kurtosis 1/λ); tail vs the exact PMF sum, binomial SE |
| `StrippedCount_VarianceIsN662PlusRSquaredNRef` | Var Y = μ₁ + R²μ₂ | 4 SE, SE_rel = √(2/(M−1) + κ/M), κ = (μ₁ + R⁴μ₂)/σ⁴ |
| `CurrieExact_ZeroBackground_IsMinusLnBeta` | Y_C = 0, α = 0, L_D = −ln 0.05 | 6 decimals (bisection to 1e-9 relative) |
| `CurrieExact_MatchesBruteForceEnumeration` (3 cases) | Y_C, actual α, L_D bracket against full pair enumeration | Y_C 1e-9, α 1e-12, detection ≥ 0.95 at L_D and < 0.95 at L_D − 1e-4 |
| `CurrieExact_LargeMeans_AgreesWithTheOtherSummationOrder` (3 cases, means to 32 000) | an independent summation over n₁ | α 1e-9; detection bracket at 1e-6 |
| `CurrieExact_ConvergesToCornishFisher` (3 cases) | exact vs first-order Cornish–Fisher | 3 × the next CF order + the lattice-ripple bound (1/π)∫_π^T \|φ_Y\|/t dt over the density, + 2.5e-4 for finer periods (derivation in the test) |
| `ForGain_IsTheWindowOfAScaledPulseHeight` | window scaling = gain | 4 binomial SE against 10⁶ direct Gaussian draws |
| `StrippingRatio_SameMapStripsToZero_…` | (b): own-map total → 0; two independent maps' R differ by their MC error | 1e-12 relative; 4 √(SE₁² + SE₂²) with the delta-method SE (covariance of exclusive window scores negligible, argued in the comment) |
| `PerPixelRatios_StripTheirOwnMapToZero_GlobalRatioLeavesAnEdgeHeavyResidual` | (c): R_i from the same map → zero image; global R → edge R > centre R (co1332) | 1e-9 of Σ\|D\|; edge / centre R − 1 > 4 SE of its mean over 6 independent maps |
| `StrippedStatistic_IsLinear_ExpectedNullIsZeroEverywhere` | Z_s linearity with E₀ | 1e-9 of Σ\|λ₁\| |
| `Family_SelectionThenValidation_RunsAndReportsProbabilities` | smoke test of the family, the ICRP-74 dose per Bq | counts and rates in range; dose to 15 decimals against the table |

Two tests failed at first and were corrected by derivation, not by loosening:
1. **Brute-force enumeration with R = 0.7.** Values equal in exact arithmetic, e.g. 7 − 0.7·10, are split by double
   rounding (−9 × 10⁻¹⁶). The reference now merges them at 1e-9, the same tie guard as the implementation; the
   discrepancy was 7 × 10⁻⁹ in α.
2. **Cornish–Fisher, first version.** Its tolerance had only the next CF order and a crude lattice term, and it failed
   by 0.004–0.011 counts at large means. Cause: Y = n₁ − R·n₂ is a near-lattice variable; |φ_Y| has secondary peaks
   where t and R·t are both near multiples of 2π. That ripple is a real property of the exact distribution. The
   tolerance now contains its inversion-formula bound (0.03–0.24 counts at these means). The failures were inside it,
   with the exact code unchanged and checked independently by the summation-order test.

Test counts: before 703 passed / 22 skipped / 0 failed (725). After **723 / 22 / 0 (745)**: the new
`CsUnderCoTests` = 20. Gcam.Tests 393 → 413; the other projects are unchanged.

## Deviations from the plan

1. **Imaging and the residual study use the product window only (side).** co1332 and coBoth are measured in count mode,
   R and gain, as DA-7's "comparisons". Imaging them would need separate threshold sets.
2. **Raw-window gate counter-case** only where `gate-thresholds-v2.json` has a threshold: head1m, cs662, 10 / 60 s,
   under the field. None exists ideal or at 300 s, and no new raw threshold was made.
3. **Three count decision rules** instead of one: the exact a-priori rule validates Currie; the plug-in rules are what
   an instrument computes (DA-4 b). Their differences are reported, not hidden.
4. **Per-pixel R_i calibration of declared precision:** 4 × 10⁶ photons per line. Its per-pixel MC noise (~4–5 % per
   pixel, 8 % RMS between two maps) is part of what the Z_s thresholds absorb. It makes single-pixel direction
   dependence unresolvable (Q3); rings are reported instead.
5. **Selection seeds** F256[1:65] are the same list the gate's selection used, but a different study with independent
   streams (purpose keys 300 000+). Validation F256[128:256] is disjoint from selection.
6. **Imaging Cs grid** 100 … 4000 in steps of ~2. The "smallest count" entries are upper bounds within one step.
7. **The aggregated summary is 3.3 MB** (count section reduced to median / quartiles and k / N). Larger than earlier
   result files (≤ 1.75 MB).
8. **DA-7 tail**, see above: measured, quantified, carried, not stopped on. Flagged for the planner.

## What could not be run / limits

- No mask, housing or room scatter of Co-60 into the 662 / side windows (DA-12). The side-window advantage is "before
  scatter". The side window tracks scattered continuum and a photopeak reference does not; that is argued, not measured.
- No cascade summing (negligible at 1 m: ~2 × 10⁻⁵ of 4π subtended); results are before the four-channel readout
  (TODO-19).
- No desktop or UI tests (not needed; no Studio change).
- `EV-15`, `PR-NRG-04`, `LIM-08`, Findings: not written (the planner's step 5).

## Commands that wrote outside reading

- `dotnet build Gcam.sln -c Release` (several times; repository build outputs).
- `dotnet test Gcam.sln -c Release --no-build` (before and after, full); `dotnet test tests/Gcam.Tests -c Release
  --no-build --filter …CsUnderCoTests` (iterations).
- Scratch probe (turn-1 console in `%TEMP%\gcam-todo35\probe`, unchanged this turn); pilot runs
  `Gcam.Cli.dll ambient-evidence` on requests in `%TEMP%\gcam-todo35\pilot\` (outputs there).
- `python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-csco-v1.json --out %TEMP%\gcam-todo35\runs
  --family csco_selection --jobs 22`, then `--family csco_validation --jobs 22`. Writes `run-info.json` and `runs\…` under
  `%TEMP%\gcam-todo35\runs`.
- `python samples/evidence/ambient/select_csco_thresholds.py … --out samples/evidence/ambient/csco-thresholds-v1.json`.
- `python samples/evidence/ambient/aggregate_csco.py … --out samples/evidence/results/csco-v1-validation.json`
  (re-run after compacting the output format).
- Python helper scripts in `%TEMP%\gcam-todo35\` (`make_validation.py` wrote the validation request and updated the new
  manifest; table scripts read only).
- The test `Family_SelectionThenValidation_…` writes and deletes its own temporary file `%TEMP%\gcam-csco-test-<guid>.json`
  at run time.
- Python's import of the evidence scripts created `samples/evidence/ambient/__pycache__/` (git-ignored, `.gitignore:22`).
- No deletes, moves, git state changes, installs or environment changes were run by me.

No approval requests.
