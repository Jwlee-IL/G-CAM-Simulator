# PLAN.Physics.CsUnderCo60.Review — implementer's turn-1 review: count rates at 1 m, Currie limits, R systematics, the gate

Scope: TODO-35 turn 1 (review only) of [PLAN.Physics.CsUnderCo60](PLAN.Physics.CsUnderCo60.md), by the substitute
implementer (a Claude subagent; Codex unavailable). Every *verify* row and every "What exists" claim was checked in the
code, and the quantitative parts were measured with short headless probes. No production code, plan, Todo or VV
document was changed. Scratch code and outputs are under `%TEMP%\gcam-todo35\` (machine-local, not evidence).

Status: review, 2026-10-06. The numbers below are **rough scoping numbers**: their N and precision are given with each
one. None of them is evidence-grade.

## At a glance

- **The plan's skeleton holds**: build on the absolute-activity separation pipeline, use Currie limits, and treat R
  systematics as a separate question. Five premises need correcting before implementation:
  1. **The separation study cannot run the hand-held head at 1 m as it stands.** `scenario_handheld.json` puts the
     source at S = 100 mm (155 mm from the detector), and `SeparationSpec` has no distance field. The study also
     hard-codes two millimetre criteria, 1 mm "located" and 3 mm peak separation. At 1 m those are 0.055 and 0.17
     angular elements (one element = 18.2 mm).
  2. **CC-7's stop condition is met.** PR-SENS-02 thresholds exist for hand-held, 1 m, cs662 only at 10 s and 60 s, and
     only under the field. They were calibrated on **ambient-only** nulls with the raw window. Under Co-60 the raw 662
     window gate trusts the Co downscatter as a source at Co-60's position: 99.7 % of Cs-free acquisitions at
     Co 10 MBq / 60 s. The stripped image is not the multinomial model the gate's Z assumes. **A new studentised
     statistic and a new threshold, calibrated on Cs-free Co + ambient nulls, are unavoidable.**
  3. **The dominant R systematic is the gain, not the direction.** The Co reference window (co1332, 1199–1466 keV)
     has its lower edge only 1.0 σ above the 1173 keV photopeak. A ±1 % gain shift therefore moves R by **+11 % / −13 %**
     (±2 %: +19 % / −25 %). Off-axis Co-60 moves R by ≤ 0.3 % along x out to 2.1°, and by ≤ 1.9 % at 5° diagonal.
     Two alternative reference windows were measured:
     - a side window above the Cs peak (728–860 keV): ∓0.9 % per 1 % gain;
     - both Co photopeaks (1056–1466 keV): ∓3.8 % per 1 % gain, and it also gives the lowest statistical L_D.
  4. **R is strongly pixel-dependent.** R_i runs from 0.92 at the array edge to 0.55 at the centre, against a global 0.70.
     A global R applied pixel by pixel leaves |residual| = 14 % of the downscatter counts. At high Co counts this
     inflates the stripped image's null maximum. CC-11's test "δR = 0 leaves no residual on a Co-only noiseless map" is
     therefore **false for the image** (it holds only for the total).
  5. **The CC-3 axis mixes two things.** At fixed k = Co : Cs the Cs detection limit in counts does not depend on t
     (ambient-free): A_D ∝ k / t. At fixed Co-60 activity, A_D ∝ √(A_Co / t), the literature form. Sweep A_Co (with its
     dose rate at the device) and Cs; report k_D = A_Co / A_D as a derived quantity.
- **EV-15's "not investigated" separated-scene bias is explained by data already in the repository.** The scene's own
  R (from the turn-9 expected counts) is 1.0 % below the on-axis calibrated R. That predicts −4.71 % against −4.48 %
  measured (8 : 1 separated), and −0.86 / −1.18 / −0.21 % against −1.11 / −1.18 / −0.35 % for the other three
  conditions. The quartile spread is R's map Monte Carlo noise (SD 0.8 % over seeds).
- **Count rates at 1 m (hand-held head, on axis, per Bq):**
  - Cs-137, cs662 window: 1.877 × 10⁻⁶ cps.
  - Co-60 (both lines), cs662 window: 9.19 × 10⁻⁷ cps; window counting reads Co as **0.49 Bq of Cs per Bq of Co**.
  - Co-60, co1332 window: 1.312 × 10⁻⁶ cps; R = 0.700.
  - Co downscatter share of the 662 window: 33 % at k = 1.
  - Ambient at 0.10 µSv/h, 662 window: bare 0.429 cps, front-only 0.0105 cps.
- **Currie limits (analytic, checked against exact Poisson):** at 60 s, L_D = 35 / 103 / 321 Cs counts at Co-60 1 / 10 /
  100 MBq (0.35 / 3.5 / 35 µSv/h at 1 m), that is A_D = 0.31 / 0.92 / 2.85 MBq. The bare ambient bound matters only
  below Co ≈ 0.5 MBq. Imaging needs 5–7× more Cs than counting (rough, stand-in threshold).
- **Run cost:** the full proposed ensemble is about 1 h wall on 10 jobs (≈ 3 min per seed, measured component times),
  provided the acquisition loops use `CorrelationSearch`'s integer path. Nothing needs cutting for cost; cuts are
  proposed for redundancy.

## How the numbers were obtained

| Probe | What | N / precision |
|---|---|---|
| `rates` (scratch console referencing the Release DLLs) | `GateResponse.Source` maps for Cs-137 (661.7 keV, 0.851) and Co-60 (1173.2 + 1332.5 keV, 0.999 each); hand-held head with `SourceMaskDistanceMm = 1000 − D = 945` (the gate study's 1 m convention). Co-60 at 0°, 1.042°, 2.084°, 2.6°, 3.6°, 5° along x and at 45°. Windows: cs662, co1332, side 727.87–860.21 keV, coBoth 1055.88–1465.75 keV, co1173. Every window was also scaled by 1/(1+g) for g = ±1 %, ±2 %. That is exact for a gain error with the current `CountingWindow.Acceptance`, because σ is evaluated at the true deposit. `GateResponse.Ambient` for both bounds | 8 seeds × 4 × 10⁶ photons per line per position; ambient 4 seeds × 2 × 10⁶ histories per bound. Quoted ± = SE over seeds |
| cross-check through the real CLI | `montecarlo ambient-evidence` on a scratch request: `ev15-separation-request-v2a.json` with a scratch copy of the hand-held scenario at S = 945 mm, scenes "two elements apart" (Cs −18.18, Co +18.18 mm) and co-located, field 0.10 µSv/h | 1 seed (777); R = 0.7026, Cs 1.869 × 10⁻⁶, Co-in-662 9.11 × 10⁻⁷ cps/Bq (agree with the probe within map noise). 17.6 s per seed; acquisitions take < 0.1 s of that |
| `imaging` | head at 1 m on the gate's own decoder grid (cyclic, one period, 49 × 49, 2.65 mm step), so the existing head1m thresholds apply. Scenes co-located, ±1.04° and ±2.6°; Co 0.1 / 1 / 10 / 100 MBq; 10 / 60 s; ideal and bare 0.10 µSv/h; Cs 0 / 100 / 250 / 500 / 1000 counts; Cs-free δR ∈ {−5, 0, 1, 2, 5, 10} %. Raw 662 window through the PR-SENS-02 gate (thresholds from `gate-thresholds-v2.json`). Stripped image studentised as Z_s(θ) = (recon(n662) − R·recon(nRef) − E₀(θ)) / √(Σ G²(n662 + R²·nRef)), with E₀ the background model's stripped reconstruction. Run once with a global R and once with a per-pixel R_i | **1 seed**, 1000 acquisitions per condition, 2 × 10⁶ photons per map. "Trusted" for Z_s uses a **stand-in threshold of 4.5**, about the null q99 at 10–1000 Co counts. It is **not calibrated**: indicative only |
| Currie arithmetic | normal-approximation Currie (L_C = 1.645 σ₀, L_D = 2.706 + 3.29 σ₀) from the probe rates. Checked against an **exact** computation (full convolution of the two Poisson windows) at low counts | exact vs normal differ ≤ 0.4 counts in L_D at 0.9–550 expected counts; at zero background L_D = 3.00 (exact) vs 2.71 |
| EV-15 re-reading | `samples/evidence/results/ambient-baseline-v1-turn9-ev15{a,b}.json`: ExpectedWindowCounts − TrueCsCounts = Co in 662; ÷ ExpectedCoWindowCounts = the scene's own R | N = 128 (committed) |

## Per row

### What exists

| Claim | Verdict | Evidence |
|---|---|---|
| Absolute-activity Cs + Co separation, `ambient-evidence`, requests v2a/b, manifest v3 | **holds** | `AmbientCommands.RunEvidence` (family switch, `"separation" => AmbientSeparationStudy.Run`); `manifest-ambient-v3.json` families `ev15_abs_a/b`, seeds O128, n 128 |
| Lab geometry, 60 s, 1 MBq + 8 / 2 MBq, windows on total deposit at the largest-deposit pixel | **holds** | request v2a/b; `GateResponse.Source` (`ComptonStrategy.Argmax`, `pixelEventSink`, energy = min(deposit, incident)); `CountingWindow.Acceptance` |
| 200 exact-Poisson acquisitions | **holds** | `AmbientEvidence.Draw` → `Sampling.PoissonExact`; `Repeats = 200` |
| R on a separate noiseless Co-only map at the axis | **holds** | `AmbientSeparationStudy.Run`: `coCal = SourceLines(At([0,0]), …, 100000u)`, `r = coCal.TotalRate(cs) / coCal.TotalRate(co)` — an independent stream from the scene maps (100208u + 16s) |
| Three Cs counts (floored, unfloored, model-subtracted) | **holds** | `floored`, `unfloored`, `subtracted` in the repeat loop |
| Cs position: two peaks ≥ 3 mm; stripped decode; "located" = 1 mm | **holds, but in mm** | `PeakSeparationMm = 3.0`, `LocatedWithinMm = 1.0` are constants in millimetres at the source plane. They are meaningless at 1 m (CC-2) |
| EV-15 numbers quoted in the plan | **holds** | turn-9 summaries (R 1.1338 ± 0.0092; −4.5 % [−8.7, −1.0]; SD 8.4 / 4.8 %) |
| "Separated bias consistent with R on axis while Co off axis (not investigated)" | **now explained** | see At a glance. The scene's R is 1.0 % lower than the on-axis R (lab, Co at (−5, 3) mm ≈ 2.1°) |
| Legacy studies, per-pixel-window R = 3.99 | **holds** (not re-run) | EV-15 text; `ComptonStrategy.PerPixelWindow` |
| Calibrated trust gate PR-SENS-02 / EV-34 | **holds, with a scope limit** | `AmbientGateStudy.Run`: nulls are ambient-only draws (`lambda = field·t·truth`), thresholds per (case, t, field, bound, window). Not valid under Co-60 (CC-7) |
| Seed ensembles | **holds** | `run_seeds.py --manifest`, `ambient/aggregate_ev.py` (`separation()` summariser) |

### Q1 — detection limit

**Verdict: correct it** (the statistics need a sharper statement).

- **The right Currie form.** The "blank" is estimated from the Co window, not from a paired blank. Under H₀ (no Cs) the
  stripped net count N = n662 − R·nRef − (b̂662 − R·b̂Ref)·t has
  σ₀² = μ662|H₀ + R²·μRef. Then L_C = z·σ₀ and L_D = z² + 2·L_C = 2.706 + 3.29·σ₀ (α = β = 5 %, Var|H₁ = σ₀² + S).
  The literature step's 2.71 + 4.65√B is the special case σ₀² = 2B. With Co dominating, σ₀² = D·(1 + R), so
  L_D ≈ 2.71 + 4.29√D for co1332 (R = 0.70), + 3.78√D for coBoth (R = 0.32), + 4.48√D for the side window (R = 0.86).
- **Exact rather than normal.** At ≲ 10 expected counts the normal form misplaces L_C. Example: bare, 10 s, no Co —
  exact L_C 6.6 vs normal 3.8; L_D agrees within 0.1. With no background, L_D = 3.00 counts (−ln 0.05), not 2.71. The
  two-window distribution is a cheap convolution, so compute L_C and L_D exactly per condition.
- **"var R" does not belong in σ₀.** R is one constant per instrument, so its error is a **common bias** δR·R·μRef,
  not per-acquisition noise. Adding it to σ₀ hides the effect Q3 is meant to show. Keep Q1 statistical, with R known.
  Report the calibration requirement separately. Example: at Co 10 MBq / 60 s, keeping the bias ≤ 0.2 σ₀ needs
  δR ≤ 1.1 %, i.e. ≳ 2 × 10⁴ calibration counts in the Co reference window.
- **Validation is nearly tautological for counts.** Each total is exactly Poisson of the summed mean. A false-positive
  check at L_C only re-tests the Poisson sampler — unless R differs from the truth, which is the real failure mode.
  The study's own R map noise already does that: per-seed SD ≈ 0.5 % at 10⁶ photons per line (lab: 0.8 %). At
  Co 100 MBq / 60 s, a 0.5 % δR is 0.28 σ₀ and lifts the 5 % false-positive rate to ≈ 9 %. So either:
  - the "statistical" Q1 uses R from the **same** truth map (true R), and the validation then checks the pipeline;
  - or a stated-precision calibration map is used and its effect is reported as a systematic.
- **Fixed k vs fixed Co.** At fixed k with no ambient field, S_D in counts depends only on k:
  - k = 1 / 2 / 4 / 8 / 16 / 32 gives S_D = 14 / 23 / 41 / 77 / 150 / 294 counts at every t;
  - A_D ∝ 1/t: at 60 s, 123 kBq … 2.6 MBq.

  "Detectable Cs : Co falls as 1/√t" is true only at fixed Co activity.

Measured Currie numbers. Hand-held head, 1 m, on axis, stripping with co1332, Cs activity at L_D; normal form,
exact within 0.4 counts above 1 count:

| Co-60 at 1 m (H*(10) at the head, unattenuated) | 10 s | 60 s | 300 s |
|---|---|---|---|
| 0 (ideal) | 3.0 c / 160 kBq | 3.0 c / 27 kBq | 3.0 c / 5.3 kBq |
| 0, bare 0.10 µSv/h | 10.3 c / 551 kBq | 21.4 c / 190 kBq | 44.5 c / 79 kBq |
| 0, front-only 0.10 µSv/h | 4.0 c / 213 kBq | 5.9 c / 52 kBq | 9.7 c / 17 kBq |
| 0.1 MBq (0.035 µSv/h) | 6.8 c / 363 kBq (k_D 0.3) | 12.8 c / 114 kBq (0.9) | 25.2 c / 45 kBq (2.2) |
| 1 MBq (0.35 µSv/h) | 15.7 c / 0.84 MBq (1.2) | 34.6 c / 0.31 MBq (3.3) | 73.9 c / 0.13 MBq (7.6) |
| 10 MBq (3.5 µSv/h) | 43.8 c / 2.3 MBq (4.3) | 103 c / 0.92 MBq (10.9) | 228 c / 0.40 MBq (24.7) |
| 100 MBq (35 µSv/h) | 133 c / 7.1 MBq (14.1) | 321 c / 2.85 MBq (35.1) | 715 c / 1.27 MBq (78.8) |

The "0" rows are ideal or field only. The bare 0.10 µSv/h field moves the Co ≥ 1 MBq rows by ≤ 15 %, and the
front-only field by < 1 %. The dose column is ICRP 74 H*(10)/Φ (the table in `AmbientDose.PerFluence`) × the
unattenuated 1 m fluence. That gives 0.348 µSv/h per MBq of Co-60 and 0.091 for Cs-137; the implementation should
compute it, not copy it.

### Q2 — imaging cost

**Verdict: open — needs a new statistic and threshold** (see CC-7). Rough numbers:

- **Stripped image, ≥ 90 % trusted and within one element**, at 60 s, both ideal and bare 0.10, both scene distances:
  - Co 1 MBq (55 Co counts in 662): S ≈ 250 Cs counts;
  - Co 10 MBq (550): S ≈ 500;
  - Co 100 MBq (5500): S > 1000 (0.80–0.87 at 1000).

  That is 5–7× the count L_D, the multiplexing penalty (Fenimore). Measured with the stand-in Z_s ≥ 4.5, global R,
  1 seed × 1000.
- **Raw 662 window + PR-SENS-02 gate**, scene ±1.04°, bare 0.10, 60 s, trusted and within one element at Cs:
  - Co 1 MBq: 0.66 at S = 250 (k ≈ 0.4), 1.00 at S = 500;
  - Co 10 MBq: 0.96 at S = 1000 (k ≈ 1.1), 0.22 at S = 500 (k ≈ 2.1).

  The raw-window boundary at 1 m sits near k ≈ 1–2, in line with EV-15's lab 2 : 1. Stripping moves it to
  k ≈ 2 (Co 10 MBq) and k ≈ 11 (Co 100 MBq) at these counts.
- **Co-located scene:** "within one element of Cs" is also "at Co". The raw gate is then "correct" with no Cs at all:
  1.000 trusted at Co, Cs-free, Co 100 MBq. Location success in the co-located scene means nothing unless it is
  paired with the Cs-free false-trusted rate at the same point. For co-located, report detection (count and image
  statistic) against the Cs-free null, not location.

### Q3 — systematic cost of R

**Verdict: correct the emphasis** — gain and reference window first, direction second.

- **(a) Direction.** Global R vs Co-60 direction at 1 m (co1332; 8 seeds × 4 × 10⁶, SE ≤ 0.0008):

  | Direction | R change |
  |---|---|
  | along x, 1.04° | +0.04 % |
  | along x, 2.08° | +0.02 % |
  | along x, 2.6° | −0.26 % |
  | along x, 3.6° | −0.42 % |
  | along x, 5° | −0.41 % |
  | 45°, 2.08° | −0.79 % |
  | 45°, 2.6° | −1.31 % |
  | 45°, 3.6° | −1.49 % |
  | 45°, 5° | −1.86 % |

  coBoth changes about two thirds as much; the side window ≤ 0.2 %. At the lab geometry the same effect is 1.0 % at
  (−5, 3) mm, which is the EV-15 bias.
- **(a) Gain / window.** R under a gain shift g, with calibration and acquisition at the same g, nominal R in brackets:

  | Reference window | −2 % | −1 % | +1 % | +2 % | Apparent Cs (Bq) per Co Bq if R is kept at nominal, at +1 % gain |
  |---|---|---|---|---|---|
  | co1332 (0.700) | +18.9 % | +11.2 % | −13.0 % | −25.2 % | −0.072 |
  | coBoth (0.324) | +8.2 % | +3.9 % | −3.6 % | −7.0 % | −0.018 |
  | side 728–860 keV (0.856) | −1.8 % | −0.9 % | +0.8 % | +1.6 % | +0.004 |

  Cause: co1332's lower edge (1199.25 keV) is 1.0 σ above the 1173.2 keV peak (σ = 26 keV at 7 % FWHM @ 662, √E).
  Example at Co 10 MBq / 60 s with co1332 (σ₀ = 30.6 counts):
  - a −1 % gain error with nominal R biases the stripped count by +56 counts = 1.8 σ₀, and the count-mode
    false-positive rate goes from 5 % to ≈ 58 %;
  - a +1 % gain error biases it by −81 counts, and real Cs at L_D is mostly missed.

  Both rates follow from the bias; they were not simulated. Either is far larger than any direction effect.

  A physics caveat the plan should state: room, housing and mask scatter of Co-60 add degraded photons to the 662
  window. They do not add to the photopeak windows. A side window tracks them; a photopeak reference does not.
  `GateResponse.Source` models none of them: the mask only absorbs, and there is no room or housing (TODO-32,
  theme 45).
- **(b) Residual image from δR.** The false "Cs" hot spot at Co-60 is **weak in the image**:
  - expected Z at Co ≈ 0.13 per 1 % δR at 7 800 Co reference counts, scaling as √counts (1 seed, noiseless maps);
  - reason: tungsten passes 35 % at 1.25 MeV and the crystal blurs the downscatter, so the Co residual is poorly coded;
  - at Co 100 MBq / 60 s, δR = −5 % moves the trusted rate from 0.16–0.19 to 0.30–0.33 (stand-in threshold);
  - most of that 0.16–0.19 baseline is not from δR (next point).

  The **count** bias is the strong effect, not the hot spot.
- **(b') Pixel structure of R.** R_i by ring from the array edge (Co on axis):

  | Ring | R_i | vs global |
  |---|---|---|
  | 0 (edge) | 0.92 | +32 % |
  | 1 | 0.78 | +11 % |
  | 2 | 0.70 | −0.5 % |
  | 3 | 0.64 | −9 % |
  | 4 | 0.60 | −15 % |
  | 5 | 0.58 | −18 % |
  | 6 | 0.56 | −20 % |
  | 7 (centre) | 0.55 | −22 % |

  The cause is escape near the edges: more downscatter, less photopeak. With a global R, the Cs-free stripped null max
  rises with Co counts: median 3.1 → 4.0, q99 4.3 → 5.5, from 10 to 100 MBq at 60 s. With the per-pixel R_i from the
  calibration map it stays at median 3.2–3.4, q99 4.4–4.7. The remaining offset at Co comes from R_i taken on axis
  while the shadow pattern moves with direction. EV-15's "local residuals … not checked" is therefore real and
  measurable.
- **(c) Per-direction R.** At 1 m, the direction correction is ≤ 0.4 % along x inside the fully coded field and
  ≤ 1.5 % diagonal. It matters only where δR·R·μRef is comparable with σ₀. Per-direction **per-pixel** R_i is the
  version that addresses the image residual.

### CC-1 — build on `AmbientSeparationStudy` / `ambient-evidence`

**Verdict: holds, as a new family.** Add a family (e.g. `csco`) with its own spec section in `AmbientEvidenceRequest`
and its own study class. It should reuse `AmbientEvidence.SourceLines`, `GateResponse`, `CorrelationSearch` and the
background-model pattern of `AmbientSeparationStudy`. Leave `separation` bit-identical so EV-15 stays reproducible. The
dispatch is one switch line in `AmbientCommands.RunEvidence`.

### CC-2 — geometry

**Verdict: correct it.**

- `AmbientSeparationStudy.Run` takes the distance from the scenario file (`At()` sets only x, y). The hand-held
  scenario is at S = 100 mm, so the study as it stands runs at 155 mm, not 1 m.
- Add a `SourceDetectorMm` field (the gate's convention: S = 1000 − D = 945 mm, as `AmbientGateStudy` does). The FOV
  family's `SourceMaskDistancesMm = 1000` is a different convention (S = 1000).
- Express scene positions and criteria in angles (one element = atan(cell / D) = 1.042°). Never use the hard-coded
  1 mm / 3 mm.
- Count rates at 1 m are measured above. Cs-137 gives 18.8 / 113 / 563 counts per MBq at 10 / 60 / 300 s, so imaging
  at 10 s needs several MBq.
- The lab secondary is fine for continuity, but it uses a different crystal and zero resolution (R = 1.13 against
  0.70). Do not mix their R values.

### CC-3 — strength axis

**Verdict: correct it.**

- Primary axis: Co-60 activity at 1 m, about {0, 0.1, 0.3, 1, 3, 10, 30, 100} MBq, each with its H*(10) at the head.
  t ∈ {10, 60, 300} s.
- For counts, sweep Cs in multiples of the **predicted** L_D for that condition, {0.5, 0.75, 1, 1.25, 1.5, 2} ×
  L_D(A_Co, t). The table above shows L_D spans 3–715 counts (5 kBq – 7 MBq). A fixed activity list cannot bracket
  that, but multiples of the prediction always do.
- For imaging, sweep absolute Cs counts {100, 250, 500, 1000, 2000, 4000}. Imaging needs 250 to > 1000.
- Report k_D = A_Co / A_D as derived. If the author wants a k-axis table, it falls out of the same runs, noting that
  at fixed k, S_D is independent of t.

### CC-4 — scenes

**Verdict: holds with a correction.**

- Co-located, plus ±1.04° (two elements apart, Cs −18.2 mm, Co +18.2 mm) and ±2.6° (five elements).
- Both separated scenes fit inside the fully coded half-field (3.64° at 1 m). But at 2.6° the Cs efficiency is −12 %
  along x, and EV-34 calls 3° marginal (1000 counts needed). So quote a Cs-only (Co = 0) imaging baseline at the same
  positions, so the edge-of-field cost is not booked to Co-60.
- Place the two sources symmetrically, not with Co at 5.2°: that would be outside the fully coded field and would
  ghost with the gate's cyclic grid.

### CC-5 — detection statistic

**Verdict: correct it.** Use the Q1 form: σ₀² = μ662|H₀ + R²·μRef, with exact two-window quantiles, and no var R
inside σ₀. Validate with Cs-free acquisitions at L_C and Cs at L_D, as proposed, but with the R source stated (true R,
or a calibration of declared precision). Report false-positive and detection rates with one-sided 95 %
Clopper–Pearson limits, as EV-34 does.

Plug-in σ from the observed counts (n662 + R²·nRef) is what an instrument would use. Under H₀ it is unbiased in the
mean but correlated with N. Report both the plug-in-decision rate and the expected-σ rate; they differ at low counts.

### CC-6 — window counting counter-case

**Verdict: holds.** Co reads as 0.49 Bq of Cs per Bq of Co on axis (d/ε). Add the imaging counter-case: the raw
window + PR-SENS-02 gate on Cs-free Co acquisitions gives a trusted false Cs at Co-60's position:

| Co-60 | 10 s | 60 s |
|---|---|---|
| 1 MBq | 4 % | 2 % |
| 10 MBq | 44–52 % | ≈ 99 % |
| 100 MBq | 100 % | 100 % |

Bare 0.10, 1 seed × 1000. This is the user-visible form of UN-03.

### CC-7 — imaging criterion

**Verdict: correct it — the stop condition is met.**

- **What exists.** Per-configuration thresholds for `head1m|t ∈ {10, 60}|F ∈ {0.05, 0.10, 0.20}|{Bare, FrontOnly}|cs662`
  (`gate-thresholds-v2.json`, e.g. 3.1891 / 4.2688 at bare 0.10 µSv/h, 10 / 60 s). None for 300 s, none ideal, and
  none for the stripped image.
- **Why they do not transfer:**
  1. The nulls were ambient-only.
  2. The gate's Z assumes a multinomial background of total N, which a stripped image (non-integer, possibly negative)
     is not.
  3. The thresholds belong to the gate's decoder grid: cyclic, one period, 49 × 49 at 1 m (`DefaultSimulationFactory`
     default, since `AmbientGateStudy` does not set `Cyclic`). The separation family decodes non-cyclic on its own grid.
- **Proposal: a stripped statistic Z_s** as defined under "How the numbers were obtained" (with per-pixel R_i). Its
  threshold should be selected and validated like AB-11:
  - nulls = Cs-free Co-60 + ambient acquisitions;
  - one threshold per configuration (scene, t, environment, Co level);
  - select at ≤ 0.3 % on selection seeds, validate ≤ 1 % on fresh seeds.

  The null maximum depends on counts (median 1.4 at ~1 count, 3.0–3.2 at 10–1000, up to 3.4 with R_i at 7 800).
  A single universal threshold is therefore not appropriate, but a threshold keyed on the observed Co reference
  counts is implementable in an instrument.
- Location = within one angular element (1.042°), on the same grid as the null. This is a new threshold, so it is
  a planner / author decision (see Open questions).

### CC-8 — R error study

**Verdict: holds; reorder and extend it.**

- Measure, per seed, R (global) and R_i (per pixel) per Co-60 direction: 0°, 1.04°, 2.6° along x, and 2.6° diagonal.
- Measure them per gain g ∈ {−2, −1, +1, +2} % by window scaling, which is exact (see the rates probe row).
- Do this for **three reference windows: co1332, coBoth, side.**
- Inject δR ∈ {±1, ±2, ±5, ±10} % **and** gain errors (acquire at g, R at nominal) on Cs-free acquisitions. Report:
  - the count false-positive rate;
  - the image false-trusted rate at Co;
  - both against δR·R·μRef / σ₀ (count) and against expected Z at Co (image), which are the natural scales.
- The co1332 window is probably the wrong default. Let the study decide between the three windows by L_D **and**
  gain sensitivity. Choosing the window is the user-facing calibration consequence.

### CC-9 — ambient field

**Verdict: holds; measured.** Ambient at 0.10 µSv/h:

| Window | Bound | cps | Counts at 10 / 60 / 300 s |
|---|---|---|---|
| 662 | bare | 0.429 | 4.3 / 25.7 / 129 |
| 662 | front-only | 0.0105 | 0.11 / 0.63 / 3.2 |
| co1332 | bare | 0.222 | 2.2 / 13.3 / 66.5 |
| co1332 | front-only | 0.0096 | 0.10 / 0.58 / 2.9 |

These agree with EV-34's 4.28 / 0.104 cps per µSv/h. Co-60 at 1 MBq puts 0.92 cps into 662, so the bare field equals
the Co contribution at Co ≈ 0.47 MBq. Below that it sets L_D (k = 0: 10 / 21 / 45 counts against 3 ideal). Above
1 MBq it changes L_D by ≤ 15 %, and front-only by < 1 %.

Cut: run front-only only at Co ∈ {0, 0.1} MBq. Note that at 300 s the background model's own MC error (500 k
histories) should be checked against the 129-count bare background; it was not measured here.

### CC-10 — seeds and reporting

**Verdict: holds.** N ≥ 64; 128 for the rates near 5 % and 95 %. With 200–300 acquisitions per seed, the pooled rate
SD is ≈ 0.14 % at 5 %. The seed spread then mostly reflects R's map noise, so record R and R_i per seed. Version the
new manifest (`manifest-ambient-v4.json` or a TODO-35 manifest) and add a family summariser to `aggregate_ev.py`. The
threshold selection follows `select_thresholds.py` (the AB-11 rule) on selection seeds disjoint from validation seeds.

**Cost** (measured components, single-threaded):

- maps ≈ 25–30 s per seed (one run of the existing family at 1 m took 17.6 s for a calibration map, 2 scenes and
  4 ambient maps);
- acquisitions ≈ 0.2 ms each on `CorrelationSearch`'s integer path (the scratch probe's double loops took ≈ 2 ms);
- a full design of about 600 k acquisitions per seed (sources, nulls, δR) ≈ 2–4 min per seed;
- 128 validation + 64 selection seeds ≈ 8–12 CPU-h ≈ **1 h wall on 10 jobs**.

No cut is needed for cost. Cut for redundancy:

- front-only beyond k = 0;
- imaging at 300 s only at three Co levels;
- the legacy floored estimate (known +17 % bias): keep it only in `separation`.

### CC-11 — tests

**Verdict: correct it.**

| Proposed test | Status | Replacement / tolerance |
|---|---|---|
| (a) variance formula vs a direct Poisson simulation | keep, with a derived tolerance | The sample variance of N over M draws has relative SE √(2/(M−1) + κ/M), with κ = (μ₁ + R⁴μ₂)/σ⁴ the excess kurtosis of N. Assert within 4 SE; e.g. M = 10⁵ at μ = (50, 80) gives ~0.5 % |
| (b) "R recovered from a noiseless Co-only map" | tautological as written (R is defined as that ratio) | Instead: the stripped total of a map with R calibrated on **the same** map is 0 to 1e-9 relative; with R from an independent map, \|δR\| ≤ 4 × the delta-method SE from `TotalRateStandardError` of both windows |
| (c) "δR = 0 leaves no residual on a Co-only noiseless map" | **false for the image** with a global R (14 % residual by physics) | Keep it for the total. For the image, test with per-pixel R_i from the same map: residual reconstruction 0 to 1e-9. Add a physics test that the global-R image residual is non-zero, with edge R_i > centre R_i |
| new: gain by window scaling | — | Acceptance of [L/(1+g), H/(1+g)] at deposit E equals P(L ≤ (1+g)(E+n) ≤ H) exactly (closed form, 1e-12) |
| new: exact Currie | — | Zero background: L_D = −ln β = 2.9957; at large μ the exact L_D converges to 2.706 + 3.29σ₀ within O(1) count — assert within 1 count at μ ≥ 500 |
| new: Z_s under H₀ | — | With true maps and per-pixel R_i, E[recon_s] − E₀ = 0 at every grid point to 1e-9 (linearity) |

## Proposed changes to the plan

1. **CC-1:** implement as a new evidence family with its own spec and study class. `separation` stays unchanged.
2. **CC-2:** add `SourceDetectorMm`, with the gate's convention (S = 1000 − D). Express scenes and every criterion
   (located, peak separation) in angles: one element = 1.042°. Remove the millimetre constants from the new family.
3. **CC-3:** primary axis = Co-60 activity at 1 m with its computed H*(10) at the head. Cs swept as multiples of the
   predicted L_D for counting, and in absolute counts {100 … 4000} for imaging. k_D derived. Say explicitly that at
   fixed k, S_D in counts does not depend on t.
4. **Q1 / CC-5:** use Currie's general form σ₀² = μ662|H₀ + R²μRef, L_D = 2.706 + 3.29σ₀ with exact two-window
   quantiles. Take var R out of σ₀ and treat R error as a systematic (Q3). Validate with true R, and separately with a
   calibration of declared precision.
5. **CC-7:** state that no validated threshold applies, and why. Define the stripped statistic Z_s with per-pixel R_i.
   Calibrate per-configuration thresholds on Cs-free Co + ambient nulls by the AB-11 rule, on the gate's grid, with
   selection seeds disjoint from validation seeds. Report co-located results as detection against the Cs-free null,
   not as location.
6. **CC-6:** add the imaging counter-case: the raw-window gate's trusted false Cs at Co-60's position on Cs-free
   acquisitions.
7. **CC-8 / Q3:** make gain the first systematic and direction the second. Compare three reference windows (co1332,
   coBoth 1056–1466 keV, side 728–860 keV) on both L_D and gain / direction sensitivity. Add per-pixel R_i and
   per-direction R_i. Report rates against bias / σ₀ and expected Z at Co.
8. **EV-15 follow-up:** the "not investigated" separated-scene bias can be closed from the committed turn-9 data: the
   scene's own R is 1.0 % below the axis R, and that predicts the bias within 0.25 points. The planner can restate it
   without a new run.
9. **CC-9:** run front-only only where it matters (Co ≤ 0.1 MBq). Check the background model's MC error at 300 s.
10. **CC-11:** replace tests (b) and (c) as in the table. Add the gain-scaling, exact-Currie and Z_s-linearity tests,
    each with its derived tolerance.
11. **Not here / limits:** add "no mask, housing or room scatter of Co-60 into the 662 window (`GateResponse.Source`
    only absorbs in the mask)", so the side-window vs photopeak-reference comparison is stated as before scatter.
    Co-60 lines are transported independently: no cascade summing, which is negligible at 1 m — the head subtends
    ~2 × 10⁻⁵ of 4π.

## Open questions for the author

1. **Which Co reference window may the product use?**
   - co1332 photopeak: today's choice. It loses 11–13 % of R per 1 % gain error.
   - Both Co photopeaks: L_D ≈ 11 % lower, ≈ 3.6–4 % R per 1 % gain.
   - A side window above the Cs peak, the GUALI practice: ≈ 0.9 % R per 1 % gain, L_D ≈ 4 % higher. It also tracks
     scatter the photopeak reference cannot see.

   This changes the gain-stability requirement the product has to meet (D-22: peak tracking + window widening), so
   it is a product decision. The study can measure all three.
2. **Does the author accept a new, separately calibrated trust threshold for stripped images (CC-7)?** The plan said
   "no new threshold". No existing threshold is valid under Co-60, and the raw-window gate reports Co-60's downscatter
   as a trusted Cs source.
3. **What is the claimed operating range of Co-60 field?** For example up to 3.5 µSv/h (10 MBq at 1 m) or 35 µSv/h.
   The R-calibration precision, gain tolerance and the Co-level grid all follow from the largest Co field PR-NRG-04
   will claim.

## What was not run

- No seed ensemble. Imaging numbers come from 1 seed × 1000 acquisitions with an uncalibrated stand-in threshold
  (Z_s ≥ 4.5). Rates come from 8 seeds, ambient from 4.
- No 300 s imaging, no front-only imaging, no per-direction per-pixel R_i, no MLEM.
- No mask, housing or room scatter (not in `GateResponse`).
- The background model's MC error at 300 s was not measured.
- `dotnet test` was not run (no code changed).

## Commands run that touched anything beyond reading

- `dotnet build Gcam.sln -c Release` (repository build outputs only; 0 errors).
- In `%TEMP%\gcam-todo35\probe`: `dotnet build -c Release` of a scratch console that references the Release DLLs by
  path (no repository project references, so no repository build output). Runs: `probe.dll rates` (3×) and
  `probe.dll imaging` (3×, incl. one `perpixel`).
- `dotnet src/Gcam.Cli/bin/Release/net9.0/Gcam.Cli.dll ambient-evidence <scratch request> <scratch output>` (3×). The
  request, the scenario copy (S = 945 mm) and the outputs are all in `%TEMP%\gcam-todo35\`.
- Python scripts in `%TEMP%\gcam-todo35\` (read-only on repository files).
- Repository write: this file only.

No approval requests.
