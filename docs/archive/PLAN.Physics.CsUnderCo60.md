# PLAN.Physics.CsUnderCo60 — Cs-137 under Co-60's Compton continuum: detection limit and the cost of stripping

Scope: TODO-35 / decision D-42 (performance-critical). Gcam already shows that per-pixel Compton stripping recovers a
Cs-137 count under a Co-60 field and that spatial separation alone holds only to Co : Cs ≈ 2 : 1 (EV-15, PR-NRG-04).
What is missing is the **price**: how small a Cs-137 source can still be detected, located and counted under a given
Co-60 field and live time (statistical cost), and what an error in the stripping ratio R does (systematic cost — a
residual coded from Co-60's direction, i.e. a false "Cs" hot spot at Co-60's position). This plan measures both, so
PR-NRG-04 / EV-15 state the recovered count **with** its cost. Literature step: done —
[PLAN.Physics.CsUnderCo60.Literature](PLAN.Physics.CsUnderCo60.Literature.md) (no published Co : Cs limit for a
scintillator imager; Currie detection limits; the side-gate subtraction of a pinhole camera; the R-error hot spot is
not quantified anywhere).

Status: **done** 2026-10-06 — review ([PLAN.Physics.CsUnderCo60.Review](PLAN.Physics.CsUnderCo60.Review.md)), turn 2
implemented and measured ([PLAN.Physics.CsUnderCo60.Turn2](PLAN.Physics.CsUnderCo60.Turn2.md)), both by a substitute
Claude implementer (Codex unavailable); verified by the planner (build, full tests 723 passed / 0 failed, one validation
seed re-run identical, the headline L_D re-derived by hand); results in Findings 65, EV-15, PR-NRG-04, LIM-08 / LIM-03,
decisions D-43 … D-45. DA-13 closed in EV-15.

## What exists (checked in the code, 2026-10-06)

| Item | Where | What it does |
|---|---|---|
| Absolute-activity Cs + Co separation | `src/Gcam.Simulation/AmbientSeparationStudy.cs` (TODO-30 AB-13 / AB-14), driven by `montecarlo ambient-evidence <request.json>` (`src/Gcam.Cli/Commands/AmbientCommands.cs`), requests `samples/evidence/ambient/ev15-separation-request-v2{a,b}.json`, manifest `samples/evidence/manifest-ambient-v3.json` (`ev15_abs_a/b`, seeds O128) | Cs-137 1 MBq + Co-60 8 or 2 MBq, 60 s, lab geometry (`samples/scenario.json`); windows on the event's total deposit at its largest-deposit pixel (cs662 595.53–727.87 keV, co1332 1199.25–1465.75 keV); 200 exact-Poisson acquisitions per condition; R calibrated on a separate noiseless Co-only map **at the axis**; Cs count three ways (per-pixel floored, unfloored, background-model-subtracted); Cs position from the raw 662 decode (two peaks ≥ 3 mm) and from the stripped decode recon(n662) − R·recon(nCo); "located" = within 1 mm |
| Its results (EV-15 "Under the absolute ambient field") | `docs/VV.Gcam.Evidence.md` EV-15 | R = 1.1338 ± 0.0092; stripped Cs count −4.5 % [−8.7, −1.0] separated vs −1.1 % co-located at 8 : 1, per-acquisition SD 8.4 % (8 : 1) / 4.8 % (2 : 1); floored stripping biased +17 % / +2 %; stripped reconstruction locates Cs within 1 mm in 0.497 (8 : 1 separated) of acquisitions; the larger separated bias is "consistent with R calibrated on axis while Co sits off axis (not investigated)" |
| Legacy studies | `ComptonStudy` (`compton`, `compton-strip`), `MixedFieldStudy` (`mixediso`, `mixedstrip`), probe mode `spatial` | photon-budget recipes, per-pixel window R = 3.99 (not comparable with the total-deposit R) |
| Calibrated trust gate | PR-SENS-02, EV-34 (TODO-30 AB-7 / AB-11): decoded image studentised against the background-shape model, threshold per configuration | decides when a located peak is trusted; ≤ 1 % false trusted location on null acquisitions |
| Seed ensembles | `samples/evidence/run_seeds.py`, `aggregate.py`, `ambient/aggregate_ev.py`, manifests | the way every EV number is quoted (D-40) |

## Questions

- **Q1 — detection limit (statistical cost).** For a Co-60 field of given strength and a live time t, the smallest
  Cs-137 source the stripped 662 keV count detects (Currie critical level L_C and detection limit L_D, α = β = 5 %),
  and the same for plain window counting. Expected form (literature, to be verified, not assumed): the stripped net
  N = n662 − R·nCo has var ≈ n662 + R²·nCo (+ R's own calibration variance), so L_D grows ≈ √(Co counts) and the
  detectable Cs : Co falls as 1 / √t. Window counting has no estimate of the Co share: its "detection limit" is set
  by the Co contribution itself (a bias, not noise) — report that as the counter-case.
- **Q2 — imaging cost.** Cs-137 location success (trusted and within one angular resolution element, the EV-34 / AB-7
  definition) versus Co : Cs and t, from the raw and the stripped reconstruction — where spatial separation fails
  (EV-15: between 2 : 1 and 4 : 1) and how far stripping moves that boundary.
- **Q3 — systematic cost of R.** (a) R's dependence on Co-60's direction (angle off axis) and on the gain / window
  (a window shift standing for drift, theme 36); (b) the residual left by a wrong R (δR / R = ±1, 2, 5, 10 %) on
  **Cs-free** Co-60 acquisitions: does the stripped image show a trusted false "Cs" source at Co-60's position, and
  from which Co counts × δR on; (c) whether calibrating R per direction (from the Co-60 location, which the Co window
  gives) removes the separated-scene bias.

## Proposed decisions (reference — verify / improve)

| ID | Decision | State |
|---|---|---|
| CC-1 | **Build on `AmbientSeparationStudy` / `ambient-evidence`** (a new family or mode), not on the legacy photon-budget recipes: absolute activities, live time, exact Poisson, the transported ambient field optional | proposed — verify the extension point |
| CC-2 | **Geometry:** primary = the hand-held head (`samples/scenario_handheld.json`) at **1 m**, because D-41 / D-42 concern product performance at use distance; secondary = the lab geometry at EV-15's set-up, for continuity with the existing numbers | verify that the separation study runs the hand-held scenario at 1 m (source z from `SourceMaskDistanceMm`), and the count rates there |
| CC-3 | **Strength axis:** Co : Cs as activity ratio k ∈ {0, 1, 2, 4, 8, 16, 32} plus the Co-60 dose rate at the device (so a user reading "Co at x µSv/h" can use it); Cs strength swept down to below detection at each k; live times t ∈ {10, 60, 300} s | proposed — verify the grid covers L_D at every k (L_D must fall inside the swept range, not at an end) |
| CC-4 | **Scenes:** co-located (worst for the spatial lever) and separated by about two angular resolution elements and by about five (at 1 m) | proposed |
| CC-5 | **Detection statistic:** stripped net count N with its plug-in σ (n662 + R²·nCo + var R); L_C / L_D derived, then **validated by acquisitions**: false-positive rate on Cs-free acquisitions at L_C ≈ 5 %, detection rate at the derived L_D ≈ 95 %, over seeds. If they disagree, the measured rates win and the formula is reported as off | proposed |
| CC-6 | **Window counting counter-case:** the same windows with no stripping; reported as the Co contribution to the 662 keV window in Cs-equivalent counts (the bias), not as an L_D | proposed |
| CC-7 | **Imaging criterion:** location success = trusted by the PR-SENS-02 calibrated statistic **and** within one angular element (hand-held 1.04°); the gate's threshold is the one selected for that configuration in TODO-30 — no new threshold. If no validated threshold exists for this configuration / window, stop and report | verify which gate thresholds exist for the hand-held head, 1 m, cs662 |
| CC-8 | **R error study:** R measured per Co direction (on axis and at the separated scenes' angles) and per window shift (±1 %, ±2 % gain); injected δR on Cs-free Co-60 acquisitions; the false-trusted-"Cs" rate at Co-60's position versus Co counts × δR | proposed |
| CC-9 | **Ambient field:** ideal and the Studio default 0.10 µSv/h (both bounds), as in EV-34; the detection limit at low Cs may depend on it even where EV-15 at 1 MBq did not | verify the field's share of B in the 662 window at 1 m, 10–300 s |
| CC-10 | **Seeds and reporting:** N outer seeds per D-40 (≥ 64; 128 for rates near the 5 % / 95 % points), quantities as median [quartiles] or k / N; a versioned manifest + request files in `samples/evidence/ambient/`, aggregated by the existing aggregator or a small extension | proposed |
| CC-11 | **Tests:** fast unit tests for the parts that have a closed form — the net-count variance formula against a direct Poisson simulation of two windows; R recovered from a noiseless Co-only map; δR = 0 leaves no residual on a Co-only noiseless map — and nothing that asserts an MC precision without a derived tolerance | proposed |

## Decisions after review (2026-10-06)

The review ([PLAN.Physics.CsUnderCo60.Review](PLAN.Physics.CsUnderCo60.Review.md)) found five wrong premises and made
eleven proposals; the planner adopts all eleven. The three product questions were put to the author, who chose the
recommended option each time.

| ID | Decision | Source |
|---|---|---|
| DA-1 | **New evidence family** with its own spec and study class; the existing `separation` family and its results stay unchanged (CC-1 corrected) | review #1 |
| DA-2 | **Distance and angles:** add `SourceDetectorMm` with the gate's convention (S = 1000 − D at 1 m); scenes and every criterion (located, peak separation) in **angular elements** (hand-held 1 element = 1.042°); no millimetre constants in the new family (CC-2 corrected: `scenario_handheld.json` has S = 100 mm, `SeparationSpec` has no distance) | review #2 |
| DA-3 | **Axes:** primary axis = Co-60 activity at 1 m with its computed H*(10) at the head — grid 0.1, 1, 3, 10, the activity giving **10 µSv/h**, and 100 MBq (≈ 35 µSv/h, measured but beyond the claim, DA-11); Cs swept as multiples of the predicted L_D (e.g. 0.5, 0.75, 1, 1.5, 2 ×) for counting and in absolute counts (≈ 100 … 4000) for imaging; live times 10, 60, 300 s; k_D = A_Co / A_D derived. State that at fixed k the count limit S_D does not depend on t (CC-3 corrected) | review #3 |
| DA-4 | **Currie, general form:** σ₀² = μ₆₆₂|H₀ + R²·μ_ref, L_C = 1.645 σ₀, L_D = 2.706 + 3.29 σ₀, checked against exact two-window quantiles; R's error is **not** in σ₀ — it is the Q3 systematic. Validate (a) with the true R and (b) with a calibration of declared precision | review #4 |
| DA-5 | **Stripped trust statistic Z_s** with **per-pixel R_i** (a global R leaves 14 % residual in the image), thresholds calibrated **per configuration** on Cs-free Co-60 + ambient nulls by the AB-11 selection rule (≤ 1 % false trusted), on the gate's grid, selection seeds disjoint from validation seeds. Co-located results are reported as **detection** against the Cs-free null, not as location (CC-7 corrected) | review #5; **author: "도입"** (new threshold accepted) |
| DA-6 | **Imaging counter-case:** the raw-window gate's trusted false "Cs" at Co-60's position on Cs-free acquisitions (the scoping run: 99.7 % at Co 10 MBq / 60 s) — reported beside the stripped result | review #6 |
| DA-7 | **Reference window (author):** the product reference is the **side window above the Cs peak, 728–860 keV** (gain-robust: ≈ 0.9 % R per 1 % gain; L_D ≈ 4 % above co1332; the published side-gate practice). co1332 (continuity with EV-15) and both Co photopeaks (1056–1466 keV) are measured beside it as comparisons. Verify in implementation that the side window holds no Cs-137 photopeak tail at the measured resolution (it starts ≈ 10 % above 661.7 keV) — if it does, report and stop | **author** (Q1, recommended option); review #7 |
| DA-8 | **R systematics, in order:** gain first (±1 %, ±2 % window shift), direction second, per-pixel R_i and per-direction R_i; report rates against bias / σ₀ and the expected Z at Co-60's position | review #7 |
| DA-9 | **Ambient:** ideal and 0.10 µSv/h; front-only bound only where it matters (Co ≤ 0.1 MBq); check the background model's MC error at 300 s | review #9 |
| DA-10 | **Tests:** replace CC-11 (b) and (c) as the review's table says; add the gain-scaling, exact-Currie and Z_s-linearity tests, each with a derived tolerance | review #10 |
| DA-11 | **Claim ceiling (author):** PR-NRG-04 claims performance for Co-60 fields **up to 10 µSv/h at the head** (the iPIX mask-band boundary, < 10 / > 10 µSv/h); 35 µSv/h is measured and reported as beyond the claim | **author** (Q3, recommended option) |
| DA-12 | **Stated limits:** no mask, housing or room scatter of Co-60 into the 662 / side windows (`GateResponse.Source` only absorbs in the mask), so the reference-window comparison is "before scatter"; no cascade summing (negligible at 1 m); results "before the four-channel readout model" (TODO-19) | review #11 |
| DA-13 | **EV-15's separated-scene bias** ("not investigated") is closed by the planner from the committed turn-9 data (scene R 1.0 % below the axis R predicts −4.71 % against −4.48 %); no new run | review #8 |

**Correction (2026-10-06, after turn 2).** DA-7's "if the side window holds a Cs-137 photopeak tail, report and stop" was
too strict as written: the side window holds 3.84 × 10⁻⁴ of the cs662 Cs-137 counts — the Gaussian resolution model's
tail above 728 keV — which lowers the stripped Cs signal by 0.033 % and is carried exactly in the estimator. The
implementer judged it immaterial and continued; the planner accepts that. The stop condition meant a tail that moves a
reported number, and this one does not.

## Steps

1. Review turn (no code): check every *verify* row in the code; measure the count rates at 1 m for the hand-held head
   (Cs and Co per Bq per second in each window, ambient share) to set the CC-3 grid; check that L_D falls inside it;
   propose corrections → `docs/PLAN.Physics.CsUnderCo60.Review.md`.
2. Decisions after review (planner, with the author where it is a product decision).
3. Implement the study extension, requests, manifest, tests (CC-1, CC-5 … CC-11).
4. Run the seed ensembles, aggregate, write a turn report with every number, its expectation, tolerance and how the
   tolerance was derived.
5. Planner verifies (build, tests, re-run one seed, read the diff) and writes: Findings theme 65; EV-15 restated
   (recovered count + L_D curve + R systematics); PR-NRG-04 restated (removing the "performance-critical, not yet
   quantified" mark only if Q1–Q3 are answered); LIM-08 updated; D-42 "lands in" completed.

## Done when

- L_C / L_D (Cs-137 activity and Cs : Co) versus k and t at the hand-held head, 1 m, validated by false-positive and
  detection rates; window-counting bias beside it.
- Location success versus k and t, raw and stripped, with the trusted-gate criterion.
- R versus direction and window shift; the false "Cs" rate versus Co counts × δR; whether per-direction R removes the
  separated bias.
- EV-15 / PR-NRG-04 / LIM-08 restated from these numbers; tests green; nothing quoted without N.

## Not here

- A joint spectral-spatial MLEM with each isotope's full crystal response (the literature's principled successor of
  stripping) — a separate TODO if Q3 shows R systematics dominate.
- High-energy transport above 1.33 MeV (TODO-31), housing transport (TODO-32), the four-channel readout (TODO-19):
  results here are stated as "before the readout model".
- Angular resolution / two-source separation (TODO-34).
