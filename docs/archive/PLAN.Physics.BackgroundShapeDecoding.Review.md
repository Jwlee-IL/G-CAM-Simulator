# PLAN.Physics.BackgroundShapeDecoding.Review - measured review of TODO-33

Scope: review only, 2026-10-07. No product or test source edits. Development measurements are headless scratch probes under `%TEMP%/gcam-todo33/`. The reference plan is a starting point, not an implementation specification.

Status: review complete; author decisions are required before implementation. All measurements below are development observations, not validation evidence or product precision guarantees.

## Code audit: every "What exists" row

| Reference-plan row | Verified implementation | Correction / qualification |
|---|---|---|
| Decoder as a matrix | `CorrelationSearch` constructor, `Reconstruct`, `ReconstructExpected`, `DecoderEstimate`; `CrossCorrelationDecoder.Decode`; `CorrelationSearchTests` | Correct for the configured cross-correlation decoder. Integer acquisition maps use exact integer accumulation, including signed differences. Real expected maps have a separate double path. First row-major maximum and configured interpolation are preserved. Construction explicitly rejects `Decoder.Method != CrossCorrelation`. This class is in Simulation, not the decoding layer. |
| Background model | `CorrelationSearch.ModelFor` | The equations in the plan are correct for fixed total N and normalized p. This is a multinomial covariance, not independent-Poisson variance at a known background mean. The implementation checks size and positive total but does not adequately reject negative / nonfinite individual entries; new public calibration inputs need those checks. |
| Search statistic | `CorrelationSearch.Search` | Correct: acquisition total is used; zero counts / zero contrast have no candidate; result is a grid point without sub-cell refinement. Source counts contribute to N. Subtracting their share can change the position, not just significance. The source does not make N an unbiased estimate of B. The class comment's "only lowers Z" is not a general theorem: the subtraction changes by -S E1(theta), whose sign and spatial variation matter, and the variance denominator also changes. |
| Gate study | `AmbientGateStudy.Run`; `GateResponse.Ambient/Source` | Correct: independent truth and calibration MC streams (purposes 50000 and 51000), exact per-pixel Poisson, raw signed errors and separate search/gate answers. A calibration map is reused across acquisitions of a seed; they are not independent calibration replicates. `GateResponse` uses a homogeneous crystal, largest deposit site, no pixel gaps / entrance / backing; this is the AB-2 bound, not a housing calibration. Activities for source-count levels use the pinned pilot rate, not the seed's true rate. |
| Bias baseline | `samples/evidence/ambient/bias_baseline.py`; `manifest-ambient-v2.json`; inherited result JSON | Correct for gate validation: F256[128:256], 128 outer seeds, 300 acquisitions per condition; paired ideal means share the source map, not necessarily the Poisson realization. Its scalar "excess" is norm(mean field error) minus norm(mean ideal error), not the length of their vector difference. A rotating bias can therefore be hidden. The optional EV-01 sweep uses a different ensemble (64 seeds), different count budgets, and only 30 MLEM acquisitions per point. Do not attach the gate's 128 x 300 label to its MLEM numbers. |
| Calibrated subtraction | `AmbientAntimaskStudy.Run` and request `ev12-antimask-request-v1.json` | Correct subtraction: observed counts minus field x live time x independent model rate map, signed values retained. The quoted 2.24 / 1.69 mm are the subtraction / antimask RMS at the lab, centred, 400 expected source counts, highest bare-open level, N=128; they are not a generic estimator tolerance and are not edge-source performance. Scale is exact only as a simulated field input; model rate and shape have MC error. |
| MLEM background term | `MlemDecoder.Decode(image, background)`, `Snapshots`; `ImagingProjection.Project` / `ImagingService` strip path; `AngularResolutionStudy.Run` | Fixed additive expected counts b_i in a Poisson forward model, raw counts retained. **The plan's "not used by any ambient study" is wrong:** `AngularResolutionStudy` creates independent truth/model ambient maps (stream purposes 400800/400801), and variants with `BackgroundTerm=true` run `Snapshots(..., bModel)` as the `+b` comparison. The AB-12 gate/sweep/antimask studies do not use that MLEM path. No jointly fitted background amplitude exists. `FromColumns` requires exactly n*n source columns, so appending a background column through that public constructor is not supported; it must be represented separately from the source grid. |
| MLEM ambient sweep | `AmbientSweepStudy.Mlem`; `ev01-sweep-request-v1.json` | Correct pixel-centre model, but specify **80 iterations**, 30 draws per fixed point, no b. Current factory MLEM is already pixel-area via `MlemReconstruction` (8 x 8 pixel samples and exp(-mu t) leakage). Engine/config default is **120**; Studio's **400** is a separate rule measured at `StudioMlem.MeasuredOptics`, not at all AB-12 heads/distances. Inherited low-count pixel-centre performance cannot establish pixel-area performance. |
| Ambient in Studio | `MainViewModel` default field and geometry; `AmbientPreset`; `SimulationService.BuildConfig` | Correct default 0.10 microSv/h, front-only. The <=0.73 mm observation belongs to the measured theme-64 configurations, not every Studio position, optics, exposure, source mixture or decoder. Studio can also select the bare bound. Deferring Studio integration is reasonable; "Studio does not show the effect today" is too absolute. |

The AB-12 premise is real: a nonuniform transported field biases raw correlation. The stronger premise that accounting for shape must recover ideal precision at every count level is wrong. Subtracting a mean does not remove acquisition noise, finite calibration noise, or forward-model mismatch. The existing search/gate was calibrated for its own statistic and selected counts; it does not guarantee an unbiased localization on every acquisition. The existing `angres_ambient` recipe already has an area-plus-background comparison: hand-held at 1 m, non-cyclic, 0.10 microSv/h, 60 s, 662 keV window, 120 pixel-area iterations, N=64 seeds with 50 pair plus 50 single-source acquisitions per condition. That is prior evidence, not a new measurement in this turn; its narrow energy window does not cover the open-window AB-12 failure.

## BG-1 through BG-7

| Decision | Reviewed disposition |
|---|---|
| BG-1 | Compare the six named estimators, but define E3's actual fit and E4's initialization / iteration rule. Add E6 (known-scale signed CC subtraction) to separate a scale problem from an MLEM problem, and E7 (one-source plus background Poisson likelihood search). E7 explicitly assumes one source; it is not a substitute for a multi-source image. Do not add sub-cell interpolation to E2 while reusing its calibrated thresholds or location success claim. |
| BG-2 | Image-only scale is a nuisance estimate, not measured ambient dose. A separate dose counter measures source plus ambient dose; D-37 does not supply a source-free ambient channel or an uncertainty for the transfer from dose to imaging-window count rate. Use a source-free, same-head background acquisition as the direct scale reference; a dose counter can track changes only under a declared spectral/angular transfer and source-contamination rule. Study +/-10% as an illustrative uncertainty, not a counter specification. |
| BG-3 | Normalize independent transported or measured maps, validate geometry/window/calibration metadata, and propagate their uncertainty. Time alone is not a shape-quality requirement. Test front-only shape against bare truth at the same true B to isolate shape mismatch; using its rate as well introduces another, much larger scale error. |
| BG-4 | The four cases, windows, bounds, fields, exposures and source levels are appropriate. Preserve the inherited cyclic settings to reproduce AB-12, but also measure non-cyclic product decoding separately. Separate development, iteration selection, gate selection and locked validation. Record signed vector differences as well as the old scalar excess. Report unsuccessful low-count regimes, rather than silently loosening a tolerance. New final evidence must use the TODO-37 snapshot/provenance driver. |
| BG-5 | Default-null opt-in background correction; explicit calibration and scale mode. Keep generation (`Ambient`) separate from decoder knowledge: never silently pass truth to an estimator. `CorrelationSearch` can remain a study/gate facade, but a reusable decoder in Decoding must not depend on Simulation (which already depends on Decoding). A weighted linear reconstruction helper belongs in Decoding. |
| BG-6 | Defer Studio as requested. It needs per-channel calibration/window metadata and uncertainty before an ambient correction can be meaningful. Front-only default alone does not prove the feature unnecessary for every Studio configuration. |
| BG-7 | Propose theme 67 and a new EV entry; the planner must check current IDs before reserving them. Preserve EV-01 / EV-12 / AB-12 historical raw results and identify new corrected estimates separately. Add a residual/noise/calibration limitation. D-52 onward are proposals for the author, not silently adopted decisions. |

## Measurement definitions and reproducibility

Development seeds: **330001, 330007, 330019**, checked absent from O128, F256 and both RTL lists. Reserve them permanently for development. Every condition has 16 exact Poisson acquisitions per seed, **48 acquisitions / 3 independent outer seeds**. Truth and calibration have independent 1,000,000-history ambient maps per seed/head/bound; each source has 1,000,000 biased photons. The pinned validated spectrum is `terrestrial-unscear2000-v1.json`, SHA-256 `4d48bc4272ce4c25ff6ae9b13ed7583e3c8f467afffdb0225956a70cc435b3f4`. Source counts are normalized to the seed's transported source total for this development probe; final AB-12 reproduction must also use its pilot-normalized activities and the default 1 MBq activity.

Bare-crystal truth, open window, cyclic settings exactly as the two sample JSONs, Tent interpolation except E2. Cases are lab z=160 mm / x=8 mm, head z=155 / x=8, head1m z=1000 / x=52.408, head5m z=5000 / x=262.04; y=0. Fields are 0, 0.05, 0.10, 0.20 microSv/h, source levels 25, 100, 250, 1000 expected net counts, exposures 10 and 60 s. The count grid includes the raw switch and background-dominated saturation. It is not an exhaustive product validation grid.

| Case | Grid step mm (exact geometry) | Nominal lateral element z*cell/D mm | Angular element deg |
|---|---:|---:|---:|
| lab | 0.388889 | 2.666667 | atan(1/60) in degrees |
| head | 0.410985 | 2.818182 | atan(1/55) in degrees |
| head1m | 2.651515 | 18.181818 | atan(1/55) in degrees |
| head5m | 13.257576 | 90.909091 | atan(1/55) in degrees |

Within-element success is evaluated using the actual angle between estimated and true directions, not a flat mm radius. A norm of signed mean error is the pull; RMS includes all acquisitions, including wrong peaks. Mean +/- SD refers to the sample SD of **outer-seed means**, not the SE of 48 independent maps. SE=SD/sqrt(3). Pull uncertainty uses the projected seed covariance (delta method); it is unreliable near zero. With three outer seeds these are exploratory uncertainties; a normal 95% interval is not justified. A scalar mean's illustrative t interval uses t(2)=4.303 times SE and still assumes exchangeable approximately normal seed means. All 48 successes cannot establish a 95% success guarantee: even conditionally independent draws give a one-sided 95% lower bound of only 0.05^(1/48)=0.9395. Shared maps require seed-cluster uncertainty as well.

| Estimator | Exact development implementation |
|---|---|
| E0 | Raw configured cross-correlation, original Tent estimate. |
| E1 | Tent peak of G*y - N*G*p; N=sum observed acquisition counts. |
| E2 | Existing `Search` argmax of (G*y - N*G*p)/sqrt(N*V1), grid point only. No changed trust threshold. |
| E3 | At each grid point fit y_i ~ S*q_i + B*p_i, q=normalized pixel-area column, by 30 two-component EM steps from S=B=N/2. Choose maximum Poisson log likelihood, retain fitted B, then Tent estimate from G*y - B*G*p. This is an approximate finite-iteration profile fit, not a claim of exact optimization. |
| E4 | Joint grid MLEM plus one nonnegative normalized background component: beta <- beta*sum p_i*y_i/mu_i, lambda_j <- lambda_j*sum A_ij*y_i/mu_i / sensitivity_j. Start beta=N/2, lambda_j=N/(2*gridPoints*sensitivity_j), so half the initial expected count budget is in each component. SIMD float projections and double updates, same numerical layout as the pixel-area decoder. This initialization is part of the proposal; it differs from E5's historical flat lambda=1. |
| E5 | Existing pixel-area `MlemDecoder.Snapshots` with fixed b=B*p, original lambda=1 initialization. B is the true expected background total in the model variant, so this is a known-scale benchmark, not an image-derived estimate. |
| E6 | Tent peak of G*y - B*G*p. Same known-scale benchmark, retaining signed differences without clipping. |
| E7 | Tent estimate on E3's grid of Poisson log likelihoods. A single-source likelihood localizer, not an EM reconstructed image. Tent is only a development choice; compare Parabolic and grid-only on separate selection seeds before adoption. |

All MLEM matrices use `MlemReconstruction.Create`: 8x8 pixel-area samples, leakage at 661.7 keV, the physical mosaic, actual source plane, and the same 49x49 grid. The main iteration count is **120**, a fixed development candidate from the current config default. At S=250, F=0 and 0.10, t=60, the model variant also evaluates 60, 240 and 400 for E4/E5. No one of these counts is selected or validated by this turn. The D-46 / DR-5 rule concerns blind pair separation plus single-source false splits; adding a fitted background changes the likelihood and requires repeating that rule if pair-resolution claims are retained. A single-source-only option can use a separate predeclared localization rule, with its narrower scope explicitly stated.

Scale sensitivity at S=250, t=60, F=0 and 0.10: Bhat/B in {0.5,0.75,0.9,1,1.1,1.25,1.5}, all the same acquisition data. E1/E2/E4 ignore the external scale; their repeated values are useful identity checks, not additional independent data. Finite measured shape: one source-free Poisson background acquisition per seed/head at Fcal=0.10, durations 600 and 3600 s, normalized to p; reused by the acquisitions of that seed. Wrong-bound variant: normalized front-only calibration shape against bare truth with B known exactly. Oracle-shape variant: normalized truth map itself, labeled as a diagnostic upper bound. All have separate model/shape uncertainty from the acquisition Poisson draws.

These scratch results are **not** certified TODO-37 evidence: the scratch executable reads live sample files; no frozen snapshot was staged before launch. Input, code, output and managed-closure hashes are retained in scratch `audit.json`; they support rerunning this development experiment but do not retrospectively create a snapshot identity. Final implementation must add a manifest family / recipe to `run_seeds.py`, whose isolated attempts snapshot the executable closure, scripts and inputs, pin the runtime, bind outputs and reject incompatible reuse. Do not backfill these scratch results into an EV provenance record.

## Scale, shape and count limits: proposed derivation

A known-scale CC residual has expected reconstruction

`r(theta) = S*G*q(theta) + B*G*(p_truth - (1+epsilon)*p_cal)(theta)`.

For a target grid point j and a competitor k the expected peak ordering survives only if

`S*(G*q[j]-G*q[k]) + B*((G*p_truth-G*p_cal)[j]-(G*p_truth-G*p_cal)[k]) > epsilon*B*(G*p_cal[j]-G*p_cal[k])`.

Intersect these linear inequalities over competitors outside the accepted cell/element. This yields a **condition-specific noiseless scale interval**; there is no universal "10% is safe" number. With acquisition/calibration noise, use the same signed weight differences and their covariance to obtain ordering uncertainty; far from a tie a conservative k*SE margin is possible. At ties or peak switches, measure the full discrete peak distribution instead of linearizing the position. A residual scale of epsilon leaves roughly |epsilon|*B/S in raw-pull units, so required scale accuracy tightens as B/S grows. This last approximation is intuition, not a tolerance or guarantee.

For a normalized finite background acquisition with M expected counts, multinomial shape covariance is approximately `(diag(p)-p*p')/M`. Its correlation-template contribution is `B^2*V1/M`; the unsubtracted acquisition retains Poisson variance. At equal calibration/measurement field and live-time ratio t/Tcal, known-background correlation noise grows approximately by sqrt(1+t/Tcal): t=60 s gives sqrt(1.1)=1.0488 for 600 s, sqrt(1+1/60)=1.0083 for 3600 s; t=10 s gives 1.0083 and 1.0014. These are **contrast-noise factors**, not predicted localization RMS multipliers; normalization removes total-count noise and source variance/peak switching also matter. At a different field the ratio is `(F/Fcal)*(t/Tcal)`. Zero-count calibration is invalid; do not normalize it or silently invent a uniform map.

If Bhat=t*Hdot*R, propagate relative standard uncertainty as `u_rel(Bhat)^2 = u_rel(Hdot)^2 + u_rel(R)^2 + u_rel(t)^2` only for independent inputs; include covariance and spectral/angular transfer error when shared. A same-condition measured rate with M counts has counting uncertainty about 1/sqrt(M). A hypothetical dose counter with 10% standard uncertainty plus a 1% rate calibration has combined standard uncertainty about 10.05%, not a hard +/-10% bound; the +/-10% development rows therefore do not certify it at 95% coverage. More importantly, total dose from the source cannot be attributed to ambient merely by applying R. Prefer image-only nuisance fitting with a background-only calibration rate as a reference/check; require a drift/mismatch diagnostic before using a dose-derived prior.

Published practice only: separate sample/background counting and propagated counting/calibration uncertainty follow [MARLAP chapter 19](https://www.epa.gov/sites/default/files/2015-05/documents/402-b-04-001c-19-final.pdf), especially section 19.5; deviations due to count loss require additional treatment, as explained by [Pomme, Keightley and Fitzgerald, *Uncertainty of nuclear counting*](https://www.nist.gov/publications/uncertainty-nuclear-counting). Coded-aperture MLEM and its runtime cost are published in [Meissner et al., 2023](https://arxiv.org/abs/2204.14113). The nuisance-background EM column here follows directly from the declared Poisson likelihood; the paper is not cited as validating these particular estimators, tolerances, readout hardware or calibration durations. No employer-specific readout or calibration procedure is used.

## Proposed engine/config surface

Default `Decoder.BackgroundCorrection = null`. With null, follow the current factory and `Decode` calls exactly: no map load, new allocations, RNG draws, truth injection or changed arithmetic. Default-off equality means same binaries' pre-change paths compared on retained fixtures, not a broad promise based only on the option name.

An opt-in correction has `Estimator` (`KnownScaleCorrelation`, `JointBackgroundMlem`; optionally `FittedScaleCorrelation`), a calibration file reference (relative path and SHA-256), and a scale policy (`Fit`, `KnownExpectedCounts`, `CalibratedRate`). Per-call `DecodingBackground` carries normalized p, expected B where applicable, scale/shape uncertainty, calibration identity and live time. Calibration metadata binds pixel dimensions/pitch/material/thickness, mask/head/bound, orientation/angular-field assumptions, pulse-height window/resolution, gains/bad-pixel treatment, measurement live time and total counts, and rate units. Require finite nonnegative pixels and scale, nonzero shape total, matching dimensions/window/geometry; reject unsupported combinations. A measured map can have zero pixels; preserve them for CC, and explicitly test EM model support rather than adding an unrecorded pseudocount.

Do not add a general "ambient correction" flag that reads `Ambient` truth. Supply calibration knowledge explicitly from the caller. KnownExpectedCounts supports controlled benchmarks; CalibratedRate computes B from live time and a source-free calibrated rate/transfer; Fit estimates beta from the image. Report beta, uncertainty/mismatch status and the still-separate trust statistic. An amplitude fit is not proof of the field's dose or the number of sources. Never clip CC residuals before decoding or feed signed residuals to Poisson MLEM.

Place shared linear reconstruction / background estimation in Decoding (or a dependency-neutral helper), expose fixed and fitted background components on MLEM without treating the background as a spatial source pixel. Keep `CorrelationSearch.Search` and its frozen gate behavior unchanged. Add a background-aware overload/context while preserving the old `IDecoder.Decode(image)` entry point; avoid changing all legacy strategies unnecessarily. Studies with historical decoder assumptions retain their explicit guard; new evidence recipes opt in deliberately. Absolute-count correction belongs on `RunFixedTime` / fixed-time evidence paths. The normal CLI photon-budget `Run` rejects ambient counts; a physical B must not be silently mixed into weighted effective photon counts. Supporting a single run therefore needs a documented fixed-time entry point, not just the factory option.

## Verification and acceptance proposed for the next turn

Guaranteed arithmetic checks: null/off routes return exactly the retained old result and RNG sequence; p normalization is invariant to positive scaling; expected known-background CC subtraction is linear and agrees with direct signed-image decoding to a rounding bound derived from sum|terms| and double summation length; fixed-b MLEM preserves raw nonnegative count input; a fitted beta=0 boundary is representable; first EM update conserves total fitted expected counts to the float projection error bound; log likelihood should not decrease beyond the derived numerical bound. Check the normalized component update against a double scalar reference on a small matrix, and fixed-background results against the existing decoder. These are meaningful tests for the new numerical path, not tests added this turn.

For localization, preserve the one angular element association criterion from geometry, and explicitly distinguish **association**, **systematic bias**, **RMS/noise cost**, and **trust**. A precision is recorded rather than asserted unless validated with a predeclared scope and criterion. For an optional grid-equivalence bias target, a move within one argmax cell with Tent offsets can differ by up to one step per axis; a radial budget sqrt(2)*step is therefore a derived cell-equivalence convention (lab 0.5500, head 0.5812, 1 m 3.7498, 5 m 18.7491 mm), **not a physical accuracy guarantee**. The author must decide whether to adopt that convention. Test the paired signed-vector excess with a seed-cluster simultaneous confidence bound against it; do not use the old difference of norms. If it fails, record the residual and condition, do not widen it.

For no-field cost, known-scale E6 with B=0 must equal E0 and E5 with B=0 must equal no-background pixel-area MLEM at the same iterations/init/interpolation. E1/E2/E3/E4 can sacrifice source contrast or overfit a background even in an ideal environment. Record their paired RMS / association / bias cost, including failure distributions; no percentage loss allowance is borrowed from EV-12 or another head. If an author wants a noninferiority gate, derive its margin from the allowed grid change or the observed ideal error distribution on **selection** seeds, pin it before validation, and label it an engineering criterion. Testing difference from zero at k*SE is a difference test, not proof of noninferiority.

Select nuisance-MLEM iterations per declared geometry/decoder scope on selection seeds, not validation. For retained pair claims repeat DR-5 with D-48's blind prominence/valley/significance and D-50 confidence-bound false-split rule; ties favor fewer iterations. For a single-source-only correction, select from {60,120,240,400,800} by minimized worst-condition signed-vector excess among association-valid count levels, record ideal cost, then freeze; this is a proposed new rule, not DR-5. Never change the rule after inspecting validation failures without a third independent seed set.

## Records proposed for the planner

Findings theme 67: "Background shape removes a systematic term, not its counting noise: estimator, count regime, scale/shape sensitivity and ideal cost"; publish per-estimator signed errors, vector excess, scalar legacy excess, RMS, angular association, false-trusted rate, seed uncertainty, iteration selection and run identity. Keep theme 64's raw decoder finding intact.

New EV entry: corrected-estimator results, geometry/count/window/field/bound/calibration/scale/iteration scope, published statistical practice, pinned recipes and validation records. EV-01 and EV-12 gain clearly separate corrected alternatives where remeasured, without replacing the historical baseline. EV-34 points to the new background-aware qualification. PR-SENS-02/PR-IMG-02 or calibration templates change only if the author accepts a scoped claim. Do not reuse an ambient-search threshold for a different localization/likelihood statistic or with a different finite-calibration distribution.

Limitation wording proposed: "Background correction retains acquisition counting noise and can fail at low source-to-background contrast. A fitted amplitude does not resolve shape/geometry mismatch or source contamination of an external dose scale. Measured calibration is valid only for the declared head, window, field spectrum/angular distribution and live-time/statistical budget; outside that scope localization precision is unvalidated." Preserve the existing bare/front-only housing and high-energy-transport limitations; background fitting does not fix either physics model.

## Commands and write audit

Initial repository state already had a modification to `docs/AGENTS.Todo.md` and an untracked reference plan. Neither was edited by this turn. No git state-changing commands, deletion, move, install, settings change, process stop, network upload, GUI launch or UI test was run. Read-only git commands were status, diff/stat and rev-parse.

Writing operations are recorded below, including scratch code edits and generated build/output files; ordinary read/search commands are omitted. No approval-requiring operation was needed. Final numerical appendices, recommendations and the write ledger follow below. During the turn another untracked `docs/PLAN.Docs.TestRecords.md` and further `AGENTS.Todo.md` changes appeared; they were not written or modified by this review. The final scoped git diff of `src/` and `tests/` is empty.

## Final measured results and recommendation

The raw pull is reproduced, but its saturation is source-position/count dependent: a background-only estimate at a fixed grid point produces a different signed error for a centre source and an edge source. The old 8/9/19/40-50 mm shorthand is not an upper bound. E1 and E2 can cost substantial ideal performance and fail at the far edge. No method passes an ideal-recovery claim across the low-count, high-background cases. The primary candidate is **E4, joint background MLEM, as an opt-in localizer with a count/trust limitation**. Keep **E5** as the known-scale Poisson benchmark and supported optional mode; keep **E6** as a cheap signed-subtraction benchmark and optional CC mode. Do not select E1/E2 as the general localization decoder. E3/E7 remain single-source diagnostics; the CC peak after a scalar fit is less robust than fitting the complete raw Poisson model. This is a development ranking to confirm, not an adopted default.

In the no-field case E4 usually matches E5 closely, but switching from CC to pixel-area MLEM is itself a different estimator and sometimes costs low-count ideal performance (lab S=100 is an example). E4 also differs from E5 in initialization; its fitted beta absorbs source-model mismatch and is not an unbiased ambient-rate readout. A low norm of the average error can hide large opposite-direction failures; retain both signed errors and RMS/association.

### Main t=60 comparison (source switch / saturation and ideal cost)

All rows: development seeds 330001, 330007, 330019; N=3 outer seeds x16 acquisitions (48). RMS and fractions are mean +/- outer-seed SD; pull is pooled signed mean length +/- delta-method SE. Units mm unless stated. These are not confidence-bound pass claims.

| Case | t | F | S | Estimator | Pull +/- SE | RMS +/- SD | Within element +/- SD |
|---|---:|---:|---:|---|---:|---:|---:|
| head | 60 | 0 | 250 | E0 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 |
| head | 60 | 0 | 250 | E1/model | 0.356 +/- 0.026 | 0.465 +/- 0.058 | 1.000 +/- 0.000 |
| head | 60 | 0 | 250 | E2/model | 0.274 +/- 0.029 | 0.393 +/- 0.081 | 1.000 +/- 0.000 |
| head | 60 | 0 | 250 | E3/model | 0.782 +/- 0.123 | 2.294 +/- 1.395 | 0.938 +/- 0.062 |
| head | 60 | 0 | 250 | E4/model | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 |
| head | 60 | 0 | 250 | E5/model | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 |
| head | 60 | 0 | 250 | E6/model | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 |
| head | 60 | 0 | 250 | E7/model | 0.400 +/- 0.020 | 0.512 +/- 0.015 | 1.000 +/- 0.000 |
| head | 60 | 0 | 1000 | E0 | 0.498 +/- 0.029 | 0.828 +/- 0.086 | 1.000 +/- 0.000 |
| head | 60 | 0 | 1000 | E1/model | 0.380 +/- 0.002 | 0.389 +/- 0.002 | 1.000 +/- 0.000 |
| head | 60 | 0 | 1000 | E2/model | 0.228 +/- 0.009 | 0.235 +/- 0.026 | 1.000 +/- 0.000 |
| head | 60 | 0 | 1000 | E3/model | 0.483 +/- 0.004 | 0.789 +/- 0.064 | 1.000 +/- 0.000 |
| head | 60 | 0 | 1000 | E4/model | 0.479 +/- 0.004 | 0.497 +/- 0.010 | 1.000 +/- 0.000 |
| head | 60 | 0 | 1000 | E5/model | 0.479 +/- 0.004 | 0.497 +/- 0.010 | 1.000 +/- 0.000 |
| head | 60 | 0 | 1000 | E6/model | 0.498 +/- 0.029 | 0.828 +/- 0.086 | 1.000 +/- 0.000 |
| head | 60 | 0 | 1000 | E7/model | 0.421 +/- 0.009 | 0.437 +/- 0.017 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 250 | E0 | 7.637 +/- 0.555 | 9.497 +/- 0.275 | 0.000 +/- 0.000 |
| head | 60 | 0.1 | 250 | E1/model | 2.604 +/- 0.398 | 6.481 +/- 0.417 | 0.646 +/- 0.036 |
| head | 60 | 0.1 | 250 | E2/model | 2.078 +/- 0.249 | 5.647 +/- 1.077 | 0.708 +/- 0.036 |
| head | 60 | 0.1 | 250 | E3/model | 6.600 +/- 0.702 | 9.498 +/- 0.996 | 0.188 +/- 0.000 |
| head | 60 | 0.1 | 250 | E4/model | 2.138 +/- 0.576 | 6.031 +/- 0.793 | 0.688 +/- 0.165 |
| head | 60 | 0.1 | 250 | E5/model | 1.751 +/- 0.659 | 5.470 +/- 0.829 | 0.729 +/- 0.191 |
| head | 60 | 0.1 | 250 | E6/model | 5.667 +/- 1.172 | 8.867 +/- 1.496 | 0.271 +/- 0.095 |
| head | 60 | 0.1 | 250 | E7/model | 1.979 +/- 0.759 | 5.638 +/- 0.947 | 0.729 +/- 0.180 |
| head | 60 | 0.1 | 1000 | E0 | 7.235 +/- 0.054 | 9.756 +/- 0.060 | 0.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E1/model | 0.348 +/- 0.011 | 0.401 +/- 0.014 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E2/model | 0.254 +/- 0.009 | 0.313 +/- 0.026 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E3/model | 1.338 +/- 0.431 | 3.220 +/- 1.466 | 0.896 +/- 0.072 |
| head | 60 | 0.1 | 1000 | E4/model | 0.542 +/- 0.016 | 0.580 +/- 0.009 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E5/model | 0.529 +/- 0.016 | 0.567 +/- 0.009 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E6/model | 1.121 +/- 0.158 | 3.914 +/- 0.509 | 0.854 +/- 0.036 |
| head | 60 | 0.1 | 1000 | E7/model | 0.455 +/- 0.021 | 0.502 +/- 0.028 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 250 | E0 | 7.353 +/- 0.495 | 9.590 +/- 0.254 | 0.000 +/- 0.000 |
| head | 60 | 0.2 | 250 | E1/model | 4.513 +/- 0.953 | 8.555 +/- 1.463 | 0.438 +/- 0.108 |
| head | 60 | 0.2 | 250 | E2/model | 4.524 +/- 0.943 | 8.439 +/- 1.628 | 0.458 +/- 0.095 |
| head | 60 | 0.2 | 250 | E3/model | 7.207 +/- 0.902 | 10.555 +/- 1.513 | 0.083 +/- 0.036 |
| head | 60 | 0.2 | 250 | E4/model | 4.490 +/- 0.669 | 9.073 +/- 1.160 | 0.458 +/- 0.144 |
| head | 60 | 0.2 | 250 | E5/model | 3.860 +/- 0.975 | 8.280 +/- 1.632 | 0.542 +/- 0.130 |
| head | 60 | 0.2 | 250 | E6/model | 5.883 +/- 0.774 | 9.724 +/- 1.522 | 0.208 +/- 0.157 |
| head | 60 | 0.2 | 250 | E7/model | 2.847 +/- 0.528 | 7.299 +/- 1.036 | 0.562 +/- 0.108 |
| head | 60 | 0.2 | 1000 | E0 | 7.797 +/- 0.172 | 9.456 +/- 0.067 | 0.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E1/model | 0.335 +/- 0.009 | 0.447 +/- 0.063 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E2/model | 0.256 +/- 0.035 | 0.408 +/- 0.119 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E3/model | 1.306 +/- 0.804 | 4.568 +/- 0.997 | 0.854 +/- 0.095 |
| head | 60 | 0.2 | 1000 | E4/model | 0.581 +/- 0.049 | 0.644 +/- 0.097 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E5/model | 0.550 +/- 0.038 | 0.607 +/- 0.070 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E6/model | 1.599 +/- 0.863 | 5.324 +/- 1.078 | 0.812 +/- 0.062 |
| head | 60 | 0.2 | 1000 | E7/model | 0.481 +/- 0.042 | 0.564 +/- 0.103 | 1.000 +/- 0.000 |
| head1m | 60 | 0 | 250 | E0 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 |
| head1m | 60 | 0 | 250 | E1/model | 20.185 +/- 2.083 | 36.363 +/- 4.204 | 0.521 +/- 0.130 |
| head1m | 60 | 0 | 250 | E2/model | 22.418 +/- 1.825 | 38.740 +/- 3.567 | 0.458 +/- 0.130 |
| head1m | 60 | 0 | 250 | E3/model | 16.386 +/- 2.584 | 47.648 +/- 5.272 | 0.833 +/- 0.036 |
| head1m | 60 | 0 | 250 | E4/model | 3.247 +/- 0.283 | 8.836 +/- 7.365 | 0.979 +/- 0.036 |
| head1m | 60 | 0 | 250 | E5/model | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 |
| head1m | 60 | 0 | 250 | E6/model | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 |
| head1m | 60 | 0 | 250 | E7/model | 5.122 +/- 0.227 | 6.788 +/- 0.654 | 1.000 +/- 0.000 |
| head1m | 60 | 0 | 1000 | E0 | 2.598 +/- 2.449 | 13.372 +/- 13.969 | 0.979 +/- 0.036 |
| head1m | 60 | 0 | 1000 | E1/model | 4.198 +/- 0.264 | 7.510 +/- 5.865 | 0.979 +/- 0.036 |
| head1m | 60 | 0 | 1000 | E2/model | 6.387 +/- 1.713 | 17.365 +/- 12.511 | 0.896 +/- 0.095 |
| head1m | 60 | 0 | 1000 | E3/model | 2.642 +/- 2.446 | 13.482 +/- 13.876 | 0.979 +/- 0.036 |
| head1m | 60 | 0 | 1000 | E4/model | 3.897 +/- 0.076 | 4.018 +/- 0.142 | 1.000 +/- 0.000 |
| head1m | 60 | 0 | 1000 | E5/model | 3.920 +/- 0.091 | 4.041 +/- 0.168 | 1.000 +/- 0.000 |
| head1m | 60 | 0 | 1000 | E6/model | 2.598 +/- 2.449 | 13.372 +/- 13.969 | 0.979 +/- 0.036 |
| head1m | 60 | 0 | 1000 | E7/model | 5.903 +/- 0.212 | 6.283 +/- 0.206 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 250 | E0 | 56.119 +/- 6.478 | 72.302 +/- 9.442 | 0.333 +/- 0.036 |
| head1m | 60 | 0.1 | 250 | E1/model | 46.692 +/- 2.227 | 68.738 +/- 0.467 | 0.167 +/- 0.095 |
| head1m | 60 | 0.1 | 250 | E2/model | 46.052 +/- 3.801 | 67.301 +/- 4.180 | 0.167 +/- 0.095 |
| head1m | 60 | 0.1 | 250 | E3/model | 50.215 +/- 2.931 | 73.198 +/- 3.380 | 0.333 +/- 0.072 |
| head1m | 60 | 0.1 | 250 | E4/model | 30.978 +/- 5.962 | 61.284 +/- 9.113 | 0.521 +/- 0.095 |
| head1m | 60 | 0.1 | 250 | E5/model | 29.749 +/- 7.988 | 59.767 +/- 10.075 | 0.479 +/- 0.130 |
| head1m | 60 | 0.1 | 250 | E6/model | 52.561 +/- 1.949 | 75.031 +/- 3.078 | 0.271 +/- 0.095 |
| head1m | 60 | 0.1 | 250 | E7/model | 25.768 +/- 7.360 | 57.806 +/- 9.267 | 0.521 +/- 0.144 |
| head1m | 60 | 0.1 | 1000 | E0 | 14.175 +/- 4.368 | 28.254 +/- 20.094 | 0.792 +/- 0.072 |
| head1m | 60 | 0.1 | 1000 | E1/model | 13.476 +/- 5.197 | 28.149 +/- 12.553 | 0.646 +/- 0.237 |
| head1m | 60 | 0.1 | 1000 | E2/model | 16.485 +/- 5.186 | 31.700 +/- 14.117 | 0.583 +/- 0.253 |
| head1m | 60 | 0.1 | 1000 | E3/model | 0.901 +/- 3.026 | 21.819 +/- 13.466 | 0.958 +/- 0.036 |
| head1m | 60 | 0.1 | 1000 | E4/model | 3.647 +/- 0.091 | 3.833 +/- 0.181 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E5/model | 3.623 +/- 0.094 | 3.801 +/- 0.173 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E6/model | 0.843 +/- 2.634 | 21.866 +/- 13.395 | 0.958 +/- 0.036 |
| head1m | 60 | 0.1 | 1000 | E7/model | 4.976 +/- 0.223 | 5.615 +/- 0.258 | 1.000 +/- 0.000 |
| head1m | 60 | 0.2 | 250 | E0 | 62.363 +/- 5.165 | 75.567 +/- 7.360 | 0.250 +/- 0.125 |
| head1m | 60 | 0.2 | 250 | E1/model | 45.770 +/- 2.902 | 68.159 +/- 2.255 | 0.104 +/- 0.130 |
| head1m | 60 | 0.2 | 250 | E2/model | 45.745 +/- 4.706 | 66.479 +/- 1.114 | 0.104 +/- 0.130 |
| head1m | 60 | 0.2 | 250 | E3/model | 42.212 +/- 5.323 | 67.455 +/- 5.102 | 0.292 +/- 0.191 |
| head1m | 60 | 0.2 | 250 | E4/model | 45.210 +/- 9.981 | 68.900 +/- 10.722 | 0.354 +/- 0.157 |
| head1m | 60 | 0.2 | 250 | E5/model | 47.031 +/- 9.859 | 71.055 +/- 8.327 | 0.250 +/- 0.165 |
| head1m | 60 | 0.2 | 250 | E6/model | 39.893 +/- 1.989 | 66.179 +/- 4.388 | 0.167 +/- 0.072 |
| head1m | 60 | 0.2 | 250 | E7/model | 42.325 +/- 6.510 | 67.218 +/- 3.136 | 0.208 +/- 0.201 |
| head1m | 60 | 0.2 | 1000 | E0 | 37.416 +/- 9.226 | 55.460 +/- 16.150 | 0.562 +/- 0.165 |
| head1m | 60 | 0.2 | 1000 | E1/model | 27.872 +/- 1.075 | 47.443 +/- 7.901 | 0.479 +/- 0.036 |
| head1m | 60 | 0.2 | 1000 | E2/model | 29.560 +/- 2.211 | 49.369 +/- 8.954 | 0.438 +/- 0.062 |
| head1m | 60 | 0.2 | 1000 | E3/model | 22.903 +/- 11.608 | 50.714 +/- 25.036 | 0.771 +/- 0.157 |
| head1m | 60 | 0.2 | 1000 | E4/model | 1.446 +/- 4.514 | 15.985 +/- 21.084 | 0.958 +/- 0.072 |
| head1m | 60 | 0.2 | 1000 | E5/model | 1.055 +/- 2.174 | 11.947 +/- 14.238 | 0.979 +/- 0.036 |
| head1m | 60 | 0.2 | 1000 | E6/model | 24.897 +/- 13.677 | 52.345 +/- 27.072 | 0.750 +/- 0.188 |
| head1m | 60 | 0.2 | 1000 | E7/model | 3.360 +/- 3.913 | 25.212 +/- 18.849 | 0.938 +/- 0.062 |
| head5m | 60 | 0 | 250 | E0 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 |
| head5m | 60 | 0 | 250 | E1/model | 60.613 +/- 4.581 | 128.392 +/- 34.871 | 0.875 +/- 0.000 |
| head5m | 60 | 0 | 250 | E2/model | 67.848 +/- 4.642 | 130.836 +/- 34.661 | 0.875 +/- 0.000 |
| head5m | 60 | 0 | 250 | E3/model | 34.290 +/- 10.016 | 67.015 +/- 69.538 | 0.979 +/- 0.036 |
| head5m | 60 | 0 | 250 | E4/model | 12.242 +/- 1.444 | 13.328 +/- 2.556 | 1.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E5/model | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E6/model | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 |
| head5m | 60 | 0 | 250 | E7/model | 12.063 +/- 16.970 | 82.758 +/- 75.940 | 0.958 +/- 0.072 |
| head5m | 60 | 0 | 1000 | E0 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 |
| head5m | 60 | 0 | 1000 | E1/model | 39.143 +/- 0.028 | 39.144 +/- 0.048 | 1.000 +/- 0.000 |
| head5m | 60 | 0 | 1000 | E2/model | 46.148 +/- 0.000 | 46.148 +/- 0.000 | 1.000 +/- 0.000 |
| head5m | 60 | 0 | 1000 | E3/model | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 |
| head5m | 60 | 0 | 1000 | E4/model | 10.947 +/- 0.299 | 11.382 +/- 0.639 | 1.000 +/- 0.000 |
| head5m | 60 | 0 | 1000 | E5/model | 10.903 +/- 0.305 | 11.339 +/- 0.647 | 1.000 +/- 0.000 |
| head5m | 60 | 0 | 1000 | E6/model | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 |
| head5m | 60 | 0 | 1000 | E7/model | 36.150 +/- 1.681 | 39.069 +/- 0.896 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 250 | E0 | 96.149 +/- 24.785 | 170.591 +/- 94.295 | 0.729 +/- 0.095 |
| head5m | 60 | 0.1 | 250 | E1/model | 211.580 +/- 14.217 | 315.769 +/- 14.302 | 0.188 +/- 0.125 |
| head5m | 60 | 0.1 | 250 | E2/model | 217.775 +/- 11.308 | 321.917 +/- 12.787 | 0.188 +/- 0.125 |
| head5m | 60 | 0.1 | 250 | E3/model | 169.771 +/- 9.611 | 297.752 +/- 9.501 | 0.562 +/- 0.062 |
| head5m | 60 | 0.1 | 250 | E4/model | 58.415 +/- 17.205 | 186.933 +/- 38.924 | 0.688 +/- 0.108 |
| head5m | 60 | 0.1 | 250 | E5/model | 81.240 +/- 5.391 | 215.303 +/- 21.379 | 0.625 +/- 0.062 |
| head5m | 60 | 0.1 | 250 | E6/model | 180.903 +/- 14.173 | 302.740 +/- 29.855 | 0.417 +/- 0.130 |
| head5m | 60 | 0.1 | 250 | E7/model | 107.093 +/- 29.239 | 242.847 +/- 65.174 | 0.583 +/- 0.095 |
| head5m | 60 | 0.1 | 1000 | E0 | 38.532 +/- 0.060 | 38.535 +/- 0.105 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E1/model | 35.497 +/- 0.592 | 71.760 +/- 28.177 | 0.958 +/- 0.036 |
| head5m | 60 | 0.1 | 1000 | E2/model | 42.771 +/- 0.640 | 75.968 +/- 25.825 | 0.958 +/- 0.036 |
| head5m | 60 | 0.1 | 1000 | E3/model | 22.064 +/- 0.723 | 27.421 +/- 0.483 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E4/model | 9.511 +/- 0.090 | 9.997 +/- 0.080 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/model | 9.529 +/- 0.069 | 9.968 +/- 0.032 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E6/model | 26.235 +/- 0.419 | 27.646 +/- 1.382 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E7/model | 16.876 +/- 10.515 | 75.439 +/- 64.498 | 0.979 +/- 0.036 |
| head5m | 60 | 0.2 | 250 | E0 | 134.686 +/- 5.202 | 211.015 +/- 10.620 | 0.438 +/- 0.000 |
| head5m | 60 | 0.2 | 250 | E1/model | 296.024 +/- 22.554 | 395.880 +/- 31.525 | 0.104 +/- 0.095 |
| head5m | 60 | 0.2 | 250 | E2/model | 298.351 +/- 22.474 | 397.766 +/- 31.430 | 0.104 +/- 0.095 |
| head5m | 60 | 0.2 | 250 | E3/model | 272.869 +/- 47.036 | 379.255 +/- 67.045 | 0.312 +/- 0.188 |
| head5m | 60 | 0.2 | 250 | E4/model | 113.094 +/- 17.345 | 240.836 +/- 32.087 | 0.500 +/- 0.062 |
| head5m | 60 | 0.2 | 250 | E5/model | 129.122 +/- 18.317 | 252.681 +/- 28.479 | 0.438 +/- 0.062 |
| head5m | 60 | 0.2 | 250 | E6/model | 299.646 +/- 25.457 | 408.089 +/- 28.935 | 0.208 +/- 0.130 |
| head5m | 60 | 0.2 | 250 | E7/model | 150.554 +/- 7.509 | 270.612 +/- 20.149 | 0.333 +/- 0.095 |
| head5m | 60 | 0.2 | 1000 | E0 | 39.803 +/- 0.055 | 39.807 +/- 0.098 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E1/model | 65.289 +/- 13.731 | 127.755 +/- 45.496 | 0.854 +/- 0.095 |
| head5m | 60 | 0.2 | 1000 | E2/model | 73.388 +/- 13.990 | 133.667 +/- 49.506 | 0.833 +/- 0.095 |
| head5m | 60 | 0.2 | 1000 | E3/model | 42.094 +/- 10.603 | 107.535 +/- 69.871 | 0.958 +/- 0.036 |
| head5m | 60 | 0.2 | 1000 | E4/model | 10.468 +/- 0.481 | 10.942 +/- 0.888 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E5/model | 10.419 +/- 0.466 | 10.853 +/- 0.857 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E6/model | 44.215 +/- 10.009 | 108.080 +/- 69.610 | 0.958 +/- 0.036 |
| head5m | 60 | 0.2 | 1000 | E7/model | 8.021 +/- 13.551 | 113.624 +/- 63.803 | 0.958 +/- 0.036 |
| lab | 60 | 0 | 250 | E0 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 250 | E1/model | 0.349 +/- 0.372 | 2.979 +/- 2.167 | 0.958 +/- 0.036 |
| lab | 60 | 0 | 250 | E2/model | 1.092 +/- 0.339 | 4.878 +/- 1.042 | 0.917 +/- 0.036 |
| lab | 60 | 0 | 250 | E3/model | 0.551 +/- 0.022 | 0.651 +/- 0.032 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 250 | E4/model | 0.317 +/- 0.014 | 0.443 +/- 0.017 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 250 | E5/model | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 250 | E6/model | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 250 | E7/model | 0.359 +/- 0.003 | 0.441 +/- 0.019 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 1000 | E0 | 0.658 +/- 0.007 | 0.660 +/- 0.011 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 1000 | E1/model | 0.391 +/- 0.010 | 0.437 +/- 0.008 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 1000 | E2/model | 0.489 +/- 0.039 | 0.638 +/- 0.028 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 1000 | E3/model | 0.648 +/- 0.008 | 0.650 +/- 0.013 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 1000 | E4/model | 0.317 +/- 0.032 | 0.390 +/- 0.040 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 1000 | E5/model | 0.327 +/- 0.031 | 0.398 +/- 0.039 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 1000 | E6/model | 0.658 +/- 0.007 | 0.660 +/- 0.011 | 1.000 +/- 0.000 |
| lab | 60 | 0 | 1000 | E7/model | 0.377 +/- 0.010 | 0.395 +/- 0.021 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 250 | E0 | 10.281 +/- 0.194 | 10.906 +/- 0.389 | 0.000 +/- 0.000 |
| lab | 60 | 0.1 | 250 | E1/model | 6.482 +/- 1.042 | 10.510 +/- 1.395 | 0.604 +/- 0.095 |
| lab | 60 | 0.1 | 250 | E2/model | 6.803 +/- 0.774 | 10.813 +/- 0.937 | 0.583 +/- 0.072 |
| lab | 60 | 0.1 | 250 | E3/model | 1.664 +/- 0.538 | 5.392 +/- 1.470 | 0.792 +/- 0.036 |
| lab | 60 | 0.1 | 250 | E4/model | 0.139 +/- 0.283 | 1.718 +/- 1.780 | 0.958 +/- 0.072 |
| lab | 60 | 0.1 | 250 | E5/model | 0.740 +/- 0.917 | 2.782 +/- 3.596 | 0.938 +/- 0.108 |
| lab | 60 | 0.1 | 250 | E6/model | 2.478 +/- 0.711 | 6.297 +/- 1.660 | 0.750 +/- 0.062 |
| lab | 60 | 0.1 | 250 | E7/model | 0.716 +/- 0.933 | 2.781 +/- 3.583 | 0.938 +/- 0.108 |
| lab | 60 | 0.1 | 1000 | E0 | 6.401 +/- 0.874 | 8.196 +/- 1.099 | 0.354 +/- 0.130 |
| lab | 60 | 0.1 | 1000 | E1/model | 0.070 +/- 0.249 | 1.713 +/- 2.181 | 0.979 +/- 0.036 |
| lab | 60 | 0.1 | 1000 | E2/model | 0.614 +/- 0.029 | 4.282 +/- 0.003 | 0.938 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E3/model | 0.645 +/- 0.015 | 0.649 +/- 0.026 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E4/model | 0.350 +/- 0.003 | 0.406 +/- 0.023 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/model | 0.357 +/- 0.005 | 0.417 +/- 0.024 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/model | 0.643 +/- 0.017 | 0.648 +/- 0.029 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E7/model | 0.403 +/- 0.013 | 0.436 +/- 0.022 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 250 | E0 | 10.957 +/- 0.189 | 11.537 +/- 0.328 | 0.000 +/- 0.000 |
| lab | 60 | 0.2 | 250 | E1/model | 6.408 +/- 0.495 | 10.529 +/- 0.859 | 0.542 +/- 0.072 |
| lab | 60 | 0.2 | 250 | E2/model | 6.763 +/- 0.305 | 10.801 +/- 0.550 | 0.521 +/- 0.036 |
| lab | 60 | 0.2 | 250 | E3/model | 3.466 +/- 0.226 | 6.977 +/- 0.585 | 0.562 +/- 0.108 |
| lab | 60 | 0.2 | 250 | E4/model | 2.673 +/- 0.349 | 6.622 +/- 0.582 | 0.688 +/- 0.108 |
| lab | 60 | 0.2 | 250 | E5/model | 3.040 +/- 0.387 | 6.995 +/- 1.362 | 0.729 +/- 0.036 |
| lab | 60 | 0.2 | 250 | E6/model | 3.995 +/- 0.528 | 7.913 +/- 1.305 | 0.542 +/- 0.095 |
| lab | 60 | 0.2 | 250 | E7/model | 2.813 +/- 0.288 | 6.868 +/- 1.241 | 0.750 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E0 | 10.170 +/- 0.162 | 10.358 +/- 0.284 | 0.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E1/model | 0.043 +/- 0.253 | 1.717 +/- 2.192 | 0.979 +/- 0.036 |
| lab | 60 | 0.2 | 1000 | E2/model | 0.302 +/- 0.318 | 3.042 +/- 2.145 | 0.958 +/- 0.036 |
| lab | 60 | 0.2 | 1000 | E3/model | 0.627 +/- 0.013 | 0.632 +/- 0.021 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E4/model | 0.331 +/- 0.026 | 0.395 +/- 0.045 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/model | 0.340 +/- 0.029 | 0.408 +/- 0.051 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E6/model | 0.625 +/- 0.013 | 0.630 +/- 0.021 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E7/model | 0.383 +/- 0.019 | 0.429 +/- 0.040 | 1.000 +/- 0.000 |

### Scale / shape sensitivity at 1000 net counts

All rows: development seeds 330001, 330007, 330019; N=3 outer seeds x16 acquisitions (48). RMS and fractions are mean +/- outer-seed SD; pull is pooled signed mean length +/- delta-method SE. Units mm unless stated. These are not confidence-bound pass claims.

| Case | t | F | S | Estimator | Pull +/- SE | RMS +/- SD | Within element +/- SD |
|---|---:|---:|---:|---|---:|---:|---:|
| head | 60 | 0.1 | 1000 | E4/measured3600 | 0.547 +/- 0.020 | 0.584 +/- 0.015 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E4/measured600 | 0.543 +/- 0.021 | 0.583 +/- 0.019 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E4/model | 0.542 +/- 0.016 | 0.580 +/- 0.009 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E4/oracleShape | 0.541 +/- 0.015 | 0.580 +/- 0.007 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E4/wrongFront | 1.477 +/- 1.141 | 5.623 +/- 2.728 | 0.812 +/- 0.108 |
| head | 60 | 0.1 | 1000 | E5/measured3600 | 0.535 +/- 0.020 | 0.572 +/- 0.015 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E5/measured600 | 0.523 +/- 0.021 | 0.564 +/- 0.019 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E5/model | 0.529 +/- 0.016 | 0.567 +/- 0.009 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E5/oracleShape | 0.528 +/- 0.014 | 0.567 +/- 0.006 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E5/scale0.5 | 0.645 +/- 0.010 | 0.684 +/- 0.006 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E5/scale0.75 | 0.566 +/- 0.015 | 0.605 +/- 0.008 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E5/scale0.9 | 0.541 +/- 0.015 | 0.579 +/- 0.008 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E5/scale1.1 | 0.518 +/- 0.016 | 0.558 +/- 0.009 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E5/scale1.25 | 0.510 +/- 0.016 | 0.550 +/- 0.009 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E5/scale1.5 | 0.507 +/- 0.016 | 0.545 +/- 0.007 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E5/wrongFront | 10.920 +/- 0.749 | 11.517 +/- 1.377 | 0.021 +/- 0.036 |
| head | 60 | 0.1 | 1000 | E6/measured3600 | 1.281 +/- 0.063 | 3.941 +/- 0.495 | 0.854 +/- 0.036 |
| head | 60 | 0.1 | 1000 | E6/measured600 | 1.054 +/- 0.147 | 3.892 +/- 0.490 | 0.854 +/- 0.036 |
| head | 60 | 0.1 | 1000 | E6/model | 1.121 +/- 0.158 | 3.914 +/- 0.509 | 0.854 +/- 0.036 |
| head | 60 | 0.1 | 1000 | E6/oracleShape | 1.056 +/- 0.242 | 3.875 +/- 0.534 | 0.854 +/- 0.036 |
| head | 60 | 0.1 | 1000 | E6/scale0.5 | 7.179 +/- 0.400 | 10.060 +/- 0.212 | 0.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E6/scale0.75 | 6.340 +/- 0.135 | 10.187 +/- 0.412 | 0.042 +/- 0.036 |
| head | 60 | 0.1 | 1000 | E6/scale0.9 | 3.005 +/- 0.348 | 7.434 +/- 0.673 | 0.542 +/- 0.036 |
| head | 60 | 0.1 | 1000 | E6/scale1.1 | 0.452 +/- 0.038 | 0.764 +/- 0.045 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E6/scale1.25 | 0.359 +/- 0.026 | 0.516 +/- 0.041 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E6/scale1.5 | 0.347 +/- 0.010 | 0.401 +/- 0.014 | 1.000 +/- 0.000 |
| head | 60 | 0.1 | 1000 | E6/wrongFront | 10.396 +/- 0.343 | 10.646 +/- 0.225 | 0.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E4/measured3600 | 0.584 +/- 0.057 | 0.647 +/- 0.106 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E4/measured600 | 0.589 +/- 0.062 | 0.648 +/- 0.117 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E4/model | 0.581 +/- 0.049 | 0.644 +/- 0.097 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E4/oracleShape | 0.581 +/- 0.051 | 0.643 +/- 0.100 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E4/wrongFront | 10.926 +/- 0.668 | 12.821 +/- 1.103 | 0.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E5/measured3600 | 0.552 +/- 0.048 | 0.609 +/- 0.082 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E5/measured600 | 0.546 +/- 0.054 | 0.602 +/- 0.096 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E5/model | 0.550 +/- 0.038 | 0.607 +/- 0.070 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E5/oracleShape | 0.550 +/- 0.040 | 0.607 +/- 0.072 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E5/scale0.5 | 0.589 +/- 0.180 | 1.541 +/- 1.232 | 0.979 +/- 0.036 |
| head | 60 | 0.2 | 1000 | E5/scale0.75 | 0.645 +/- 0.051 | 0.709 +/- 0.101 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E5/scale0.9 | 0.585 +/- 0.048 | 0.648 +/- 0.096 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E5/scale1.1 | 0.533 +/- 0.037 | 0.589 +/- 0.067 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E5/scale1.25 | 0.520 +/- 0.035 | 0.576 +/- 0.065 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E5/scale1.5 | 0.519 +/- 0.031 | 0.572 +/- 0.061 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E5/wrongFront | 10.829 +/- 0.166 | 11.118 +/- 0.364 | 0.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E6/measured3600 | 1.193 +/- 0.705 | 4.661 +/- 1.134 | 0.854 +/- 0.072 |
| head | 60 | 0.2 | 1000 | E6/measured600 | 1.137 +/- 0.598 | 5.013 +/- 1.067 | 0.812 +/- 0.062 |
| head | 60 | 0.2 | 1000 | E6/model | 1.599 +/- 0.863 | 5.324 +/- 1.078 | 0.812 +/- 0.062 |
| head | 60 | 0.2 | 1000 | E6/oracleShape | 1.594 +/- 0.888 | 5.111 +/- 1.430 | 0.833 +/- 0.095 |
| head | 60 | 0.2 | 1000 | E6/scale0.5 | 7.186 +/- 0.178 | 9.834 +/- 0.202 | 0.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E6/scale0.75 | 7.590 +/- 0.164 | 10.282 +/- 0.222 | 0.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E6/scale0.9 | 5.225 +/- 0.404 | 9.544 +/- 0.798 | 0.229 +/- 0.036 |
| head | 60 | 0.2 | 1000 | E6/scale1.1 | 0.332 +/- 0.228 | 1.818 +/- 1.966 | 0.979 +/- 0.036 |
| head | 60 | 0.2 | 1000 | E6/scale1.25 | 0.337 +/- 0.010 | 0.447 +/- 0.064 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E6/scale1.5 | 0.373 +/- 0.012 | 0.443 +/- 0.072 | 1.000 +/- 0.000 |
| head | 60 | 0.2 | 1000 | E6/wrongFront | 10.273 +/- 0.059 | 10.309 +/- 0.101 | 0.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E4/measured3600 | 3.647 +/- 0.102 | 3.833 +/- 0.197 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E4/measured600 | 3.614 +/- 0.076 | 3.795 +/- 0.168 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E4/model | 3.647 +/- 0.091 | 3.833 +/- 0.181 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E4/oracleShape | 3.612 +/- 0.097 | 3.801 +/- 0.190 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E4/wrongFront | 5.613 +/- 0.228 | 13.968 +/- 12.897 | 0.979 +/- 0.036 |
| head1m | 60 | 0.1 | 1000 | E5/measured3600 | 3.612 +/- 0.116 | 3.789 +/- 0.206 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E5/measured600 | 3.567 +/- 0.077 | 3.737 +/- 0.158 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E5/model | 3.623 +/- 0.094 | 3.801 +/- 0.173 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E5/oracleShape | 3.577 +/- 0.104 | 3.758 +/- 0.187 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E5/scale0.5 | 3.838 +/- 0.037 | 4.224 +/- 0.147 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E5/scale0.75 | 3.686 +/- 0.089 | 3.881 +/- 0.184 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E5/scale0.9 | 3.641 +/- 0.091 | 3.825 +/- 0.175 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E5/scale1.1 | 3.601 +/- 0.099 | 3.774 +/- 0.177 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E5/scale1.25 | 3.591 +/- 0.104 | 3.757 +/- 0.182 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E5/scale1.5 | 3.562 +/- 0.099 | 3.719 +/- 0.174 | 1.000 +/- 0.000 |
| head1m | 60 | 0.1 | 1000 | E5/wrongFront | 6.693 +/- 1.480 | 18.400 +/- 19.497 | 0.958 +/- 0.072 |
| head1m | 60 | 0.1 | 1000 | E6/measured3600 | 1.600 +/- 2.479 | 13.726 +/- 13.853 | 0.979 +/- 0.036 |
| head1m | 60 | 0.1 | 1000 | E6/measured600 | 8.692 +/- 9.641 | 31.380 +/- 25.949 | 0.896 +/- 0.130 |
| head1m | 60 | 0.1 | 1000 | E6/model | 0.843 +/- 2.634 | 21.866 +/- 13.395 | 0.958 +/- 0.036 |
| head1m | 60 | 0.1 | 1000 | E6/oracleShape | 1.433 +/- 2.484 | 13.701 +/- 13.878 | 0.979 +/- 0.036 |
| head1m | 60 | 0.1 | 1000 | E6/scale0.5 | 10.012 +/- 7.923 | 25.702 +/- 28.367 | 0.875 +/- 0.165 |
| head1m | 60 | 0.1 | 1000 | E6/scale0.75 | 6.887 +/- 5.935 | 28.899 +/- 22.168 | 0.896 +/- 0.095 |
| head1m | 60 | 0.1 | 1000 | E6/scale0.9 | 3.332 +/- 4.441 | 25.805 +/- 17.764 | 0.938 +/- 0.062 |
| head1m | 60 | 0.1 | 1000 | E6/scale1.1 | 1.973 +/- 1.216 | 21.440 +/- 14.298 | 0.958 +/- 0.036 |
| head1m | 60 | 0.1 | 1000 | E6/scale1.25 | 7.061 +/- 0.643 | 31.236 +/- 1.907 | 0.896 +/- 0.036 |
| head1m | 60 | 0.1 | 1000 | E6/scale1.5 | 13.160 +/- 4.891 | 28.116 +/- 12.522 | 0.646 +/- 0.237 |
| head1m | 60 | 0.1 | 1000 | E6/wrongFront | 4.797 +/- 0.638 | 6.796 +/- 0.867 | 1.000 +/- 0.000 |
| head1m | 60 | 0.2 | 1000 | E4/measured3600 | 1.060 +/- 2.292 | 11.981 +/- 14.188 | 0.979 +/- 0.036 |
| head1m | 60 | 0.2 | 1000 | E4/measured600 | 1.625 +/- 4.465 | 16.039 +/- 21.104 | 0.958 +/- 0.072 |
| head1m | 60 | 0.2 | 1000 | E4/model | 1.446 +/- 4.514 | 15.985 +/- 21.084 | 0.958 +/- 0.072 |
| head1m | 60 | 0.2 | 1000 | E4/oracleShape | 1.527 +/- 4.574 | 15.942 +/- 21.093 | 0.958 +/- 0.072 |
| head1m | 60 | 0.2 | 1000 | E4/wrongFront | 12.461 +/- 5.885 | 33.301 +/- 22.600 | 0.854 +/- 0.157 |
| head1m | 60 | 0.2 | 1000 | E5/measured3600 | 1.142 +/- 1.981 | 11.882 +/- 14.266 | 0.979 +/- 0.036 |
| head1m | 60 | 0.2 | 1000 | E5/measured600 | 0.897 +/- 2.084 | 12.011 +/- 14.362 | 0.979 +/- 0.036 |
| head1m | 60 | 0.2 | 1000 | E5/model | 1.055 +/- 2.174 | 11.947 +/- 14.238 | 0.979 +/- 0.036 |
| head1m | 60 | 0.2 | 1000 | E5/oracleShape | 1.044 +/- 2.085 | 11.861 +/- 14.281 | 0.979 +/- 0.036 |
| head1m | 60 | 0.2 | 1000 | E5/scale0.5 | 5.645 +/- 2.953 | 25.118 +/- 17.774 | 0.938 +/- 0.062 |
| head1m | 60 | 0.2 | 1000 | E5/scale0.75 | 1.804 +/- 3.494 | 16.240 +/- 20.868 | 0.958 +/- 0.072 |
| head1m | 60 | 0.2 | 1000 | E5/scale0.9 | 1.127 +/- 2.254 | 12.045 +/- 14.156 | 0.979 +/- 0.036 |
| head1m | 60 | 0.2 | 1000 | E5/scale1.1 | 1.211 +/- 1.822 | 11.889 +/- 14.292 | 0.979 +/- 0.036 |
| head1m | 60 | 0.2 | 1000 | E5/scale1.25 | 1.341 +/- 1.517 | 11.885 +/- 14.298 | 0.979 +/- 0.036 |
| head1m | 60 | 0.2 | 1000 | E5/scale1.5 | 1.969 +/- 2.589 | 18.359 +/- 0.688 | 0.938 +/- 0.000 |
| head1m | 60 | 0.2 | 1000 | E5/wrongFront | 12.701 +/- 5.480 | 33.450 +/- 22.211 | 0.875 +/- 0.125 |
| head1m | 60 | 0.2 | 1000 | E6/measured3600 | 22.598 +/- 10.720 | 50.874 +/- 24.473 | 0.771 +/- 0.144 |
| head1m | 60 | 0.2 | 1000 | E6/measured600 | 29.046 +/- 13.729 | 56.221 +/- 26.771 | 0.708 +/- 0.201 |
| head1m | 60 | 0.2 | 1000 | E6/model | 24.897 +/- 13.677 | 52.345 +/- 27.072 | 0.750 +/- 0.188 |
| head1m | 60 | 0.2 | 1000 | E6/oracleShape | 25.295 +/- 12.211 | 52.992 +/- 26.345 | 0.750 +/- 0.165 |
| head1m | 60 | 0.2 | 1000 | E6/scale0.5 | 25.690 +/- 4.740 | 49.242 +/- 9.957 | 0.667 +/- 0.130 |
| head1m | 60 | 0.2 | 1000 | E6/scale0.75 | 20.292 +/- 7.109 | 46.137 +/- 14.263 | 0.771 +/- 0.157 |
| head1m | 60 | 0.2 | 1000 | E6/scale0.9 | 21.619 +/- 8.728 | 50.968 +/- 18.717 | 0.792 +/- 0.130 |
| head1m | 60 | 0.2 | 1000 | E6/scale1.1 | 21.773 +/- 11.630 | 47.465 +/- 22.521 | 0.750 +/- 0.165 |
| head1m | 60 | 0.2 | 1000 | E6/scale1.25 | 27.901 +/- 3.916 | 48.506 +/- 12.935 | 0.521 +/- 0.072 |
| head1m | 60 | 0.2 | 1000 | E6/scale1.5 | 42.047 +/- 1.124 | 57.802 +/- 2.305 | 0.000 +/- 0.000 |
| head1m | 60 | 0.2 | 1000 | E6/wrongFront | 6.149 +/- 0.649 | 8.304 +/- 1.062 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E4/measured3600 | 9.487 +/- 0.252 | 9.976 +/- 0.356 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E4/measured600 | 9.696 +/- 0.047 | 10.162 +/- 0.072 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E4/model | 9.511 +/- 0.090 | 9.997 +/- 0.080 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E4/oracleShape | 9.409 +/- 0.166 | 9.901 +/- 0.210 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E4/wrongFront | 10.833 +/- 0.051 | 11.497 +/- 0.067 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/measured3600 | 9.471 +/- 0.272 | 9.911 +/- 0.381 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/measured600 | 9.773 +/- 0.065 | 10.179 +/- 0.110 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/model | 9.529 +/- 0.069 | 9.968 +/- 0.032 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/oracleShape | 9.403 +/- 0.163 | 9.850 +/- 0.195 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/scale0.5 | 9.633 +/- 0.073 | 10.191 +/- 0.078 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/scale0.75 | 9.516 +/- 0.079 | 10.019 +/- 0.064 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/scale0.9 | 9.519 +/- 0.074 | 9.983 +/- 0.045 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/scale1.1 | 9.540 +/- 0.063 | 9.955 +/- 0.023 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/scale1.25 | 9.550 +/- 0.056 | 9.931 +/- 0.027 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/scale1.5 | 9.556 +/- 0.053 | 9.888 +/- 0.053 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E5/wrongFront | 10.179 +/- 0.195 | 21.779 +/- 17.064 | 0.958 +/- 0.072 |
| head5m | 60 | 0.1 | 1000 | E6/measured3600 | 25.600 +/- 0.414 | 26.835 +/- 1.417 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E6/measured600 | 23.963 +/- 2.283 | 27.374 +/- 1.683 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E6/model | 26.235 +/- 0.419 | 27.646 +/- 1.382 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E6/oracleShape | 25.810 +/- 0.553 | 27.112 +/- 1.247 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E6/scale0.5 | 23.134 +/- 2.771 | 34.285 +/- 1.390 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E6/scale0.75 | 17.999 +/- 0.706 | 29.311 +/- 0.818 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E6/scale0.9 | 22.067 +/- 0.725 | 27.415 +/- 0.489 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E6/scale1.1 | 31.262 +/- 1.251 | 31.941 +/- 2.117 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E6/scale1.25 | 36.881 +/- 0.274 | 37.088 +/- 0.395 | 1.000 +/- 0.000 |
| head5m | 60 | 0.1 | 1000 | E6/scale1.5 | 35.509 +/- 0.592 | 71.776 +/- 28.178 | 0.958 +/- 0.036 |
| head5m | 60 | 0.1 | 1000 | E6/wrongFront | 39.023 +/- 0.041 | 39.027 +/- 0.071 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E4/measured3600 | 10.333 +/- 0.353 | 10.785 +/- 0.657 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E4/measured600 | 10.840 +/- 0.660 | 11.293 +/- 1.140 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E4/model | 10.468 +/- 0.481 | 10.942 +/- 0.888 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E4/oracleShape | 10.216 +/- 0.407 | 10.687 +/- 0.757 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E4/wrongFront | 15.808 +/- 9.137 | 97.935 +/- 72.595 | 0.958 +/- 0.036 |
| head5m | 60 | 0.2 | 1000 | E5/measured3600 | 10.241 +/- 0.367 | 10.652 +/- 0.653 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E5/measured600 | 10.817 +/- 0.617 | 11.211 +/- 1.053 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E5/model | 10.419 +/- 0.466 | 10.853 +/- 0.857 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E5/oracleShape | 10.148 +/- 0.386 | 10.580 +/- 0.705 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E5/scale0.5 | 11.117 +/- 0.592 | 11.702 +/- 1.121 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E5/scale0.75 | 10.614 +/- 0.520 | 11.170 +/- 0.980 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E5/scale0.9 | 10.466 +/- 0.484 | 10.947 +/- 0.899 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E5/scale1.1 | 10.388 +/- 0.451 | 10.780 +/- 0.822 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E5/scale1.25 | 10.356 +/- 0.433 | 10.696 +/- 0.779 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E5/scale1.5 | 10.324 +/- 0.419 | 10.638 +/- 0.748 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E5/wrongFront | 90.416 +/- 7.326 | 99.393 +/- 7.326 | 0.250 +/- 0.165 |
| head5m | 60 | 0.2 | 1000 | E6/measured3600 | 43.756 +/- 9.941 | 107.866 +/- 69.434 | 0.958 +/- 0.036 |
| head5m | 60 | 0.2 | 1000 | E6/measured600 | 44.047 +/- 11.308 | 112.534 +/- 73.803 | 0.938 +/- 0.062 |
| head5m | 60 | 0.2 | 1000 | E6/model | 44.215 +/- 10.009 | 108.080 +/- 69.610 | 0.958 +/- 0.036 |
| head5m | 60 | 0.2 | 1000 | E6/oracleShape | 44.210 +/- 9.832 | 108.290 +/- 69.099 | 0.958 +/- 0.036 |
| head5m | 60 | 0.2 | 1000 | E6/scale0.5 | 38.566 +/- 0.071 | 38.573 +/- 0.125 | 1.000 +/- 0.000 |
| head5m | 60 | 0.2 | 1000 | E6/scale0.75 | 42.932 +/- 10.658 | 110.799 +/- 66.052 | 0.958 +/- 0.036 |
| head5m | 60 | 0.2 | 1000 | E6/scale0.9 | 41.300 +/- 11.739 | 108.388 +/- 68.535 | 0.958 +/- 0.036 |
| head5m | 60 | 0.2 | 1000 | E6/scale1.1 | 50.806 +/- 9.753 | 111.182 +/- 66.341 | 0.958 +/- 0.036 |
| head5m | 60 | 0.2 | 1000 | E6/scale1.25 | 66.958 +/- 14.575 | 132.197 +/- 50.645 | 0.833 +/- 0.095 |
| head5m | 60 | 0.2 | 1000 | E6/scale1.5 | 226.219 +/- 3.774 | 299.629 +/- 14.630 | 0.021 +/- 0.036 |
| head5m | 60 | 0.2 | 1000 | E6/wrongFront | 41.348 +/- 0.423 | 41.569 +/- 0.907 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E4/measured3600 | 0.348 +/- 0.004 | 0.404 +/- 0.024 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E4/measured600 | 0.326 +/- 0.020 | 0.392 +/- 0.038 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E4/model | 0.350 +/- 0.003 | 0.406 +/- 0.023 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E4/oracleShape | 0.349 +/- 0.005 | 0.406 +/- 0.023 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E4/wrongFront | 0.328 +/- 0.004 | 0.366 +/- 0.016 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/measured3600 | 0.354 +/- 0.005 | 0.414 +/- 0.026 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/measured600 | 0.324 +/- 0.026 | 0.400 +/- 0.042 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/model | 0.357 +/- 0.005 | 0.417 +/- 0.024 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/oracleShape | 0.357 +/- 0.007 | 0.417 +/- 0.024 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/scale0.5 | 0.349 +/- 0.000 | 0.398 +/- 0.017 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/scale0.75 | 0.354 +/- 0.003 | 0.409 +/- 0.021 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/scale0.9 | 0.355 +/- 0.004 | 0.413 +/- 0.023 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/scale1.1 | 0.361 +/- 0.006 | 0.422 +/- 0.025 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/scale1.25 | 0.369 +/- 0.007 | 0.432 +/- 0.027 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/scale1.5 | 0.391 +/- 0.008 | 0.452 +/- 0.026 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E5/wrongFront | 0.294 +/- 0.006 | 0.336 +/- 0.017 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/measured3600 | 0.638 +/- 0.013 | 0.643 +/- 0.023 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/measured600 | 0.634 +/- 0.023 | 0.639 +/- 0.040 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/model | 0.643 +/- 0.017 | 0.648 +/- 0.029 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/oracleShape | 0.642 +/- 0.016 | 0.647 +/- 0.027 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/scale0.5 | 0.707 +/- 0.011 | 0.709 +/- 0.019 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/scale0.75 | 0.679 +/- 0.014 | 0.682 +/- 0.024 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/scale0.9 | 0.659 +/- 0.016 | 0.663 +/- 0.028 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/scale1.1 | 0.627 +/- 0.017 | 0.633 +/- 0.031 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/scale1.25 | 0.601 +/- 0.018 | 0.608 +/- 0.033 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/scale1.5 | 0.554 +/- 0.018 | 0.562 +/- 0.032 | 1.000 +/- 0.000 |
| lab | 60 | 0.1 | 1000 | E6/wrongFront | 5.305 +/- 1.197 | 7.344 +/- 1.728 | 0.479 +/- 0.191 |
| lab | 60 | 0.2 | 1000 | E4/measured3600 | 0.325 +/- 0.024 | 0.391 +/- 0.043 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E4/measured600 | 0.288 +/- 0.042 | 0.366 +/- 0.065 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E4/model | 0.331 +/- 0.026 | 0.395 +/- 0.045 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E4/oracleShape | 0.331 +/- 0.022 | 0.394 +/- 0.041 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E4/wrongFront | 0.271 +/- 0.016 | 0.307 +/- 0.035 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/measured3600 | 0.333 +/- 0.028 | 0.404 +/- 0.048 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/measured600 | 0.289 +/- 0.054 | 0.378 +/- 0.081 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/model | 0.340 +/- 0.029 | 0.408 +/- 0.051 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/oracleShape | 0.342 +/- 0.025 | 0.405 +/- 0.048 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/scale0.5 | 0.318 +/- 0.019 | 0.369 +/- 0.036 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/scale0.75 | 0.330 +/- 0.023 | 0.390 +/- 0.041 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/scale0.9 | 0.336 +/- 0.028 | 0.400 +/- 0.048 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/scale1.1 | 0.347 +/- 0.031 | 0.417 +/- 0.054 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/scale1.25 | 0.361 +/- 0.033 | 0.431 +/- 0.060 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/scale1.5 | 0.406 +/- 0.042 | 0.480 +/- 0.077 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E5/wrongFront | 2.696 +/- 1.733 | 4.733 +/- 4.201 | 0.771 +/- 0.253 |
| lab | 60 | 0.2 | 1000 | E6/measured3600 | 0.615 +/- 0.020 | 0.620 +/- 0.034 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E6/measured600 | 0.606 +/- 0.012 | 0.611 +/- 0.019 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E6/model | 0.625 +/- 0.013 | 0.630 +/- 0.021 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E6/oracleShape | 0.624 +/- 0.016 | 0.628 +/- 0.026 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E6/scale0.5 | 7.677 +/- 1.083 | 9.490 +/- 1.046 | 0.271 +/- 0.201 |
| lab | 60 | 0.2 | 1000 | E6/scale0.75 | 0.712 +/- 0.249 | 2.831 +/- 1.879 | 0.938 +/- 0.062 |
| lab | 60 | 0.2 | 1000 | E6/scale0.9 | 0.656 +/- 0.013 | 0.659 +/- 0.021 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E6/scale1.1 | 0.591 +/- 0.012 | 0.597 +/- 0.020 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E6/scale1.25 | 0.535 +/- 0.011 | 0.542 +/- 0.019 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E6/scale1.5 | 0.401 +/- 0.005 | 0.456 +/- 0.011 | 1.000 +/- 0.000 |
| lab | 60 | 0.2 | 1000 | E6/wrongFront | 11.250 +/- 0.446 | 11.575 +/- 0.827 | 0.000 +/- 0.000 |

### Ideal-environment cost, explicitly paired by outer seed

Reference for E1/E2/E3/E6 is E0; reference for E4 is E5 at the same 120 iterations. E5 zero-background cost relative to its own no-background path is exactly zero. E7 is compared with E5 as a separate single-source model. Positive RMS difference means a cost. Numbers are mean +/- SD over the three seed differences, not independent-ensemble subtraction.

| Case | S | Estimator | Reference | RMS cost mm +/- SD | Within-element change +/- SD |
|---|---:|---|---|---:|---:|
| lab | 25 | E1/model | E0 | 3.901 +/- 1.008 | -0.146 +/- 0.036 |
| lab | 25 | E2/model | E0 | 5.039 +/- 0.913 | -0.229 +/- 0.072 |
| lab | 25 | E3/model | E0 | -0.273 +/- 1.511 | 0.062 +/- 0.062 |
| lab | 25 | E4/model | E5/model | -0.150 +/- 0.558 | 0.000 +/- 0.062 |
| lab | 25 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| lab | 25 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| lab | 25 | E7/model | E5/model | -0.027 +/- 0.344 | 0.021 +/- 0.036 |
| lab | 100 | E1/model | E0 | 4.806 +/- 0.923 | -0.104 +/- 0.036 |
| lab | 100 | E2/model | E0 | 5.815 +/- 0.718 | -0.146 +/- 0.036 |
| lab | 100 | E3/model | E0 | -0.012 +/- 0.013 | 0.000 +/- 0.000 |
| lab | 100 | E4/model | E5/model | 0.447 +/- 0.776 | -0.021 +/- 0.036 |
| lab | 100 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| lab | 100 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| lab | 100 | E7/model | E5/model | -1.232 +/- 2.207 | 0.021 +/- 0.036 |
| lab | 250 | E1/model | E0 | 2.314 +/- 2.160 | -0.042 +/- 0.036 |
| lab | 250 | E2/model | E0 | 4.213 +/- 1.016 | -0.083 +/- 0.036 |
| lab | 250 | E3/model | E0 | -0.014 +/- 0.012 | 0.000 +/- 0.000 |
| lab | 250 | E4/model | E5/model | -0.005 +/- 0.002 | 0.000 +/- 0.000 |
| lab | 250 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| lab | 250 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| lab | 250 | E7/model | E5/model | -0.007 +/- 0.032 | 0.000 +/- 0.000 |
| lab | 1000 | E1/model | E0 | -0.223 +/- 0.005 | 0.000 +/- 0.000 |
| lab | 1000 | E2/model | E0 | -0.022 +/- 0.018 | 0.000 +/- 0.000 |
| lab | 1000 | E3/model | E0 | -0.010 +/- 0.002 | 0.000 +/- 0.000 |
| lab | 1000 | E4/model | E5/model | -0.008 +/- 0.000 | 0.000 +/- 0.000 |
| lab | 1000 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| lab | 1000 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| lab | 1000 | E7/model | E5/model | -0.003 +/- 0.031 | 0.000 +/- 0.000 |
| head | 25 | E1/model | E0 | -4.220 +/- 2.726 | 0.500 +/- 0.165 |
| head | 25 | E2/model | E0 | -4.488 +/- 2.336 | 0.521 +/- 0.130 |
| head | 25 | E3/model | E0 | -0.288 +/- 0.992 | 0.042 +/- 0.036 |
| head | 25 | E4/model | E5/model | -0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head | 25 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head | 25 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head | 25 | E7/model | E5/model | -0.934 +/- 0.393 | 0.062 +/- 0.000 |
| head | 100 | E1/model | E0 | -7.342 +/- 1.870 | 0.542 +/- 0.157 |
| head | 100 | E2/model | E0 | -7.334 +/- 1.858 | 0.542 +/- 0.157 |
| head | 100 | E3/model | E0 | -0.670 +/- 0.196 | 0.083 +/- 0.036 |
| head | 100 | E4/model | E5/model | -0.001 +/- 0.002 | 0.000 +/- 0.000 |
| head | 100 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head | 100 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head | 100 | E7/model | E5/model | -2.883 +/- 1.806 | 0.042 +/- 0.036 |
| head | 250 | E1/model | E0 | -2.140 +/- 1.847 | 0.083 +/- 0.095 |
| head | 250 | E2/model | E0 | -2.212 +/- 1.867 | 0.083 +/- 0.095 |
| head | 250 | E3/model | E0 | -0.311 +/- 0.501 | 0.021 +/- 0.036 |
| head | 250 | E4/model | E5/model | -0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head | 250 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head | 250 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head | 250 | E7/model | E5/model | -0.091 +/- 0.051 | 0.000 +/- 0.000 |
| head | 1000 | E1/model | E0 | -0.439 +/- 0.088 | 0.000 +/- 0.000 |
| head | 1000 | E2/model | E0 | -0.593 +/- 0.092 | 0.000 +/- 0.000 |
| head | 1000 | E3/model | E0 | -0.039 +/- 0.034 | 0.000 +/- 0.000 |
| head | 1000 | E4/model | E5/model | -0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head | 1000 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head | 1000 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head | 1000 | E7/model | E5/model | -0.061 +/- 0.022 | 0.000 +/- 0.000 |
| head1m | 25 | E1/model | E0 | -11.908 +/- 5.445 | -0.083 +/- 0.072 |
| head1m | 25 | E2/model | E0 | -12.733 +/- 4.334 | -0.083 +/- 0.072 |
| head1m | 25 | E3/model | E0 | -9.252 +/- 5.693 | 0.000 +/- 0.062 |
| head1m | 25 | E4/model | E5/model | 0.007 +/- 0.001 | 0.000 +/- 0.000 |
| head1m | 25 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head1m | 25 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head1m | 25 | E7/model | E5/model | -0.158 +/- 6.619 | 0.042 +/- 0.219 |
| head1m | 100 | E1/model | E0 | -11.234 +/- 4.421 | -0.292 +/- 0.036 |
| head1m | 100 | E2/model | E0 | -12.198 +/- 6.183 | -0.292 +/- 0.036 |
| head1m | 100 | E3/model | E0 | 0.973 +/- 1.793 | -0.021 +/- 0.036 |
| head1m | 100 | E4/model | E5/model | -4.215 +/- 7.298 | 0.021 +/- 0.036 |
| head1m | 100 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head1m | 100 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head1m | 100 | E7/model | E5/model | -2.727 +/- 3.187 | 0.042 +/- 0.036 |
| head1m | 250 | E1/model | E0 | -11.285 +/- 7.205 | -0.312 +/- 0.108 |
| head1m | 250 | E2/model | E0 | -8.909 +/- 7.623 | -0.375 +/- 0.108 |
| head1m | 250 | E3/model | E0 | -0.001 +/- 0.001 | 0.000 +/- 0.000 |
| head1m | 250 | E4/model | E5/model | -0.017 +/- 0.018 | 0.000 +/- 0.000 |
| head1m | 250 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head1m | 250 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head1m | 250 | E7/model | E5/model | -2.064 +/- 7.495 | 0.021 +/- 0.036 |
| head1m | 1000 | E1/model | E0 | -5.862 +/- 8.103 | 0.000 +/- 0.000 |
| head1m | 1000 | E2/model | E0 | 3.993 +/- 10.484 | -0.083 +/- 0.072 |
| head1m | 1000 | E3/model | E0 | 0.110 +/- 0.179 | 0.000 +/- 0.000 |
| head1m | 1000 | E4/model | E5/model | -0.023 +/- 0.029 | 0.000 +/- 0.000 |
| head1m | 1000 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head1m | 1000 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head1m | 1000 | E7/model | E5/model | 2.243 +/- 0.043 | 0.000 +/- 0.000 |
| head5m | 25 | E1/model | E0 | -69.170 +/- 47.761 | -0.083 +/- 0.036 |
| head5m | 25 | E2/model | E0 | -67.925 +/- 46.404 | -0.083 +/- 0.036 |
| head5m | 25 | E3/model | E0 | -5.987 +/- 11.364 | 0.000 +/- 0.000 |
| head5m | 25 | E4/model | E5/model | -0.017 +/- 0.004 | 0.000 +/- 0.000 |
| head5m | 25 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 25 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 25 | E7/model | E5/model | 5.906 +/- 45.876 | 0.042 +/- 0.036 |
| head5m | 100 | E1/model | E0 | 41.235 +/- 77.322 | -0.167 +/- 0.072 |
| head5m | 100 | E2/model | E0 | 33.558 +/- 82.383 | -0.167 +/- 0.072 |
| head5m | 100 | E3/model | E0 | 4.141 +/- 7.163 | 0.000 +/- 0.000 |
| head5m | 100 | E4/model | E5/model | -0.025 +/- 0.047 | 0.000 +/- 0.000 |
| head5m | 100 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 100 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 100 | E7/model | E5/model | -22.998 +/- 30.399 | 0.062 +/- 0.062 |
| head5m | 250 | E1/model | E0 | 21.184 +/- 51.951 | -0.083 +/- 0.036 |
| head5m | 250 | E2/model | E0 | 23.628 +/- 51.083 | -0.083 +/- 0.036 |
| head5m | 250 | E3/model | E0 | -40.193 +/- 69.617 | 0.021 +/- 0.036 |
| head5m | 250 | E4/model | E5/model | 0.019 +/- 0.014 | 0.000 +/- 0.000 |
| head5m | 250 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 250 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 250 | E7/model | E5/model | 69.450 +/- 73.895 | -0.042 +/- 0.072 |
| head5m | 1000 | E1/model | E0 | 13.127 +/- 0.048 | 0.000 +/- 0.000 |
| head5m | 1000 | E2/model | E0 | 20.131 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 1000 | E3/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 1000 | E4/model | E5/model | 0.043 +/- 0.009 | 0.000 +/- 0.000 |
| head5m | 1000 | E5/model | E5/model | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 1000 | E6/model | E0 | 0.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 1000 | E7/model | E5/model | 27.729 +/- 1.253 | 0.000 +/- 0.000 |

### Rates, finite calibration budget and wrong-bound scale

Ambient open-window rate per microSv/h, mean +/- outer-seed SD, N=3 development seeds, 1,000,000 histories per map.

| Head | Bare truth cps | Bare independent model cps | Front-only model cps | Expected 600 s counts at 0.10 | Expected 3600 s counts at 0.10 |
|---|---:|---:|---:|---:|---:|
| scenario.json | 162.799 +/- 0.129 | 162.743 +/- 0.182 | 0.574 +/- 0.006 | 9767.9 +/- 7.7 | 58607.5 +/- 46.3 |
| scenario_handheld.json | 335.311 +/- 0.156 | 335.328 +/- 0.196 | 1.277 +/- 0.009 | 20118.7 +/- 9.4 | 120712.0 +/- 56.2 |

These expected-count spreads are MC/outer-seed uncertainty, not Poisson uncertainty of the actual calibration counts. Conditional Poisson calibration-count SD is sqrt(expected counts). One normalized measured calibration per seed/head is shared at all distances. The high-count probe records the actual calibration totals below (these reproduce the earlier measured shapes exactly).

| Head / case | Calibration seconds | Seed | Actual counts | Expected counts | Conditional Poisson SD |
|---|---:|---:|---:|---:|---:|
| lab | 600 | 330001 | 9642 | 9761.673 | 98.801 |
| lab | 3600 | 330001 | 58282 | 58570.039 | 242.012 |
| head | 600 | 330001 | 20211 | 20125.071 | 141.863 |
| head | 3600 | 330001 | 121150 | 120750.427 | 347.492 |
| head1m | 600 | 330001 | 20211 | 20125.071 | 141.863 |
| head1m | 3600 | 330001 | 121150 | 120750.427 | 347.492 |
| head5m | 600 | 330001 | 20211 | 20125.071 | 141.863 |
| head5m | 3600 | 330001 | 121150 | 120750.427 | 347.492 |
| lab | 600 | 330007 | 9724 | 9776.553 | 98.876 |
| lab | 3600 | 330007 | 58547 | 58659.317 | 242.197 |
| head | 600 | 330007 | 20044 | 20122.990 | 141.856 |
| head | 3600 | 330007 | 120309 | 120737.939 | 347.474 |
| head1m | 600 | 330007 | 20044 | 20122.990 | 141.856 |
| head1m | 3600 | 330007 | 120309 | 120737.939 | 347.474 |
| head5m | 600 | 330007 | 20044 | 20122.990 | 141.856 |
| head5m | 3600 | 330007 | 120309 | 120737.939 | 347.474 |
| lab | 600 | 330019 | 9696 | 9765.533 | 98.821 |
| lab | 3600 | 330019 | 58330 | 58593.195 | 242.060 |
| head | 600 | 330019 | 20199 | 20107.914 | 141.802 |
| head | 3600 | 330019 | 120563 | 120647.485 | 347.343 |
| head1m | 600 | 330019 | 20199 | 20107.914 | 141.802 |
| head1m | 3600 | 330019 | 120563 | 120647.485 | 347.343 |
| head5m | 600 | 330019 | 20199 | 20107.914 | 141.802 |
| head5m | 3600 | 330019 | 120563 | 120647.485 | 347.343 |

### Noiseless scale ordering intervals (not a noise guarantee)

E6: keep the ideal maximum ahead of every rival more than one nominal lateral element away, using transported truth, independent calibration and the inequality in the derivation above. Epsilon=Bhat/B-1. Per-seed endpoints have no sampling error conditional on those maps; the displayed interval is the conservative intersection over the three development maps, not a confidence interval for all calibrations. Grid-element neighbourhood is used only for this ordering diagnostic; acquired-frame association above uses actual angles.

| Case | S | F | Intersection epsilon lower | upper |
|---|---:|---:|---:|---:|
| lab | 250 | 0.1 | -0.24476 | 0.34980 |
| lab | 250 | 0.2 | -0.11966 | 0.17335 |
| lab | 1000 | 0.1 | -0.98601 | 1.40785 |
| lab | 1000 | 0.2 | -0.49241 | 0.70272 |
| head | 250 | 0.1 | -0.03007 | 0.28297 |
| head | 250 | 0.2 | -0.00971 | 0.12724 |
| head | 1000 | 0.1 | -0.14907 | 1.17300 |
| head | 1000 | 0.2 | -0.07080 | 0.58291 |
| head1m | 250 | 0.1 | -0.11587 | 0.04633 |
| head1m | 250 | 0.2 | -0.04770 | 0.01182 |
| head1m | 1000 | 0.1 | -0.52488 | 0.25340 |
| head1m | 1000 | 0.2 | -0.25221 | 0.11535 |
| head5m | 250 | 0.1 | -0.07758 | 0.13250 |
| head5m | 250 | 0.2 | -0.02786 | 0.05923 |
| head5m | 1000 | 0.1 | -0.37592 | 0.55749 |
| head5m | 1000 | 0.2 | -0.17703 | 0.27542 |

An empty interval or one excluding zero says this strong ordering criterion already fails with the correct scale; it must not be repaired by choosing a biased scale. The acquisition tables are the relevant noisy-frame result. Increasing an overestimate sometimes appears to improve localization by suppressing a competing peak, but this is not a calibration recommendation. For S=250, successful ideal-recovery scope is already absent at correct scale in several cases; no positive universal allowed counter error can be certified there.

### Full measurement proposal and runtime

Use separately generated, pinned seed lists: selection `530001 + 104729*i` for i=0..15; locked validation `730001 + 130363*i` for i=0..31; reserve confirmation `970001 + 154858*i` for i=0..31. All 80 proposed seeds were checked distinct from each other, repository lists and development seeds in this turn; recheck before committing the manifest and bind their hashes. Do not substitute F256 gate validation seeds: earlier tasks and this review already used their results to set context. Selection and confirmation must not become validation by relabeling. Validation runs remain unopened until estimator/iterations/calibration rules and tolerances are pinned.

Core matrix: the four heads/distances, centre plus specified edge; open plus 595.53-727.87 keV; bare and front-only; F={0.05,0.10,0.20}, t={10,60}; source levels {25,50,100,250,500,1000} plus 1 MBq using the existing pilot/activity recipe. That is 1344 field conditions plus 224 ideal conditions per decoding mode. Compare E0 and the frozen selected correction; retain E5/E6 benchmark on the worst-edge and transition cells. Run cyclic reproduction and non-cyclic product qualification separately (3136 total conditions if both are fully repeated). Validation: 32 outer seeds x300 acquisitions per condition (9600 per condition), independent truth/calibration MC maps, record seed means and all switch/failure counts. Include one 600 s and one 3600 s measured calibration per seed/head/window/bound, scale offsets +/-10%, +/-25%, +/-50%, and wrong-bound normalization on the specified edge transition and saturation cells; do not multiply the entire broad grid by every sensitivity variant. At S=250  and 1000, t=60, F=0.10/0.20, the shape/scale variants are paired across estimators and acquisitions. Add per-seed repeated background-calibration maps to characterize the calibration distribution if a product calibration duration is selected.

Trust: either retain the existing statistic unchanged and prove its location/correction relationship on the new finite-calibration domain, or select a new threshold on background-only selection acquisitions, then evaluate 32x256=8192 independent-acquisition null draws per condition with independent seed calibration maps. Preserve the existing AB-7 targets (<=1% false trusted, >=95% associated trusted localization) as requirements, not borrowed mm tolerances. Conditional zero-failure minima follow directly from binomial coverage: ceil(log(0.05)/log(0.99))=299 nulls for a 1% upper bound, ceil(log(0.05)/log(0.95))=59 successes for a 95% lower bound. Shared MC/calibration maps still require seed-cluster uncertainty; the large repeat count alone does not solve calibration uncertainty. Report Clopper-Pearson conditional intervals plus seed-cluster intervals/range and clearly state the calibration population sampled.

Measured scratch runtimes are listed below; these are single-run elapsed times with no timing uncertainty estimated. They include map generation and all diagnostics, so extrapolation is a budget estimate, not a benchmark guarantee.

| Run | seconds | seed count | acquired conditions per seed |
|---|---:|---:|---:|
| t60 | 404.75 | 3 | 64 |
| t10 | 305.90 | 3 | 64 |
| high | 224.75 | 3 | 12 |

A coarse upper budget can be obtained by scaling the t=10 core (two MLEM estimators plus a 30-step single-source profile fit) from 64x16 conditions/draws per seed to 3136x300 and 32 seeds: multiply its total three-seed time by (3136*300*32)/(64*16*3). That includes repeated map overhead and the unselected profile fit, so it deliberately overestimates a shared-map E0+one-MLEM run. Do a one-selection-seed timing pilot with the final estimator and chosen iterations before scheduling; a broad 120-iteration run is days of serial work, not a short test. Shape/scale sensitivity is an additional targeted run. If iterations rise to400, projection work grows about400/120, without a demonstrated accuracy improvement from this review. An initial staged validation can cover the four open-bare edge cases and all source/field/time levels, then the full matrix; both stages must be reported, and stopping after the first is not full qualification. No unmeasured speedup from parallel jobs is promised.


A separate cached-matrix timing probe (seed 330001, S=1000,F=0.10,t=60,120 iterations, one warm-up then20 repeated decodes of the same flood per case) measured the following. Timing mean +/- sample SD, in milliseconds; the high-count study was running concurrently, so contention is included. Reflection invokes the exact retained scratch Fits implementation; E4/E5 times exclude final peak interpolation and output serialization. These are timing samples, not20 independent physics acquisitions.

| Case | operation | N timings | mean ms +/- SD | min ms | max ms |
|---|---|---:|---:|---:|---:|
| lab | E0 | 20 | 0.0723 +/- 0.1546 | 0.0313 | 0.7283 |
| lab | E3profile | 20 | 19.2157 +/- 1.1687 | 18.5500 | 23.3542 |
| lab | E4 | 20 | 5.4056 +/- 0.4788 | 5.0206 | 6.9878 |
| lab | E5 | 20 | 6.3536 +/- 0.2745 | 6.0133 | 7.0368 |
| head | E0 | 20 | 0.0391 +/- 0.0024 | 0.0371 | 0.0483 |
| head | E3profile | 20 | 33.4576 +/- 0.6747 | 32.7291 | 35.7149 |
| head | E4 | 20 | 9.5588 +/- 0.7674 | 8.8003 | 12.1750 |
| head | E5 | 20 | 9.8386 +/- 0.2710 | 9.3910 | 10.4259 |
| head1m | E0 | 20 | 0.0373 +/- 0.0068 | 0.0337 | 0.0654 |
| head1m | E3profile | 20 | 46.3819 +/- 19.1267 | 32.8001 | 81.3274 |
| head1m | E4 | 20 | 12.2020 +/- 2.6605 | 9.6806 | 17.8442 |
| head1m | E5 | 20 | 14.6097 +/- 4.3756 | 9.5530 | 19.8269 |
| head5m | E0 | 20 | 0.1115 +/- 0.2419 | 0.0504 | 1.1385 |
| head5m | E3profile | 20 | 46.9231 +/- 18.0320 | 32.7714 | 80.8383 |
| head5m | E4 | 20 | 11.7091 +/- 3.5149 | 9.5361 | 20.9340 |
| head5m | E5 | 20 | 13.7704 +/- 5.8997 | 9.3255 | 26.7286 |

For the full two-mode validation matrix (30,105,600 acquired frames), E4 projection/update time at the observed per-case mean range gives about 45.2-102.0 serial hours. E4+E5 gives about 98.3-224.2 hours. These are extrapolated ranges across measured cases, not confidence intervals or guaranteed completion times. Add MC map generation, final peak extraction, gates, records and targeted scale/shape validation; reserve 4-10 serial days at 120 iterations depending on benchmarks retained, then revise from a final-recipe one-seed pilot. At 400 projection work is approximately3.33 times larger. Full E0+E4 at 120 is feasible as sustained numerical evidence, but is not a routine unit-test run.

## Author decisions (options and recommendations)

| Question | Options | Recommendation and reason |
|---|---|---|
| Which decoder is exposed? | E4 joint MLEM; E5 fixed-background MLEM; E6 fixed-background CC; E1/E2 | Expose E4 as the primary default-off correction candidate, plus fixed-b E5 and signed E6 as explicit known-scale alternatives. E1/E2 have substantial ideal/far-edge costs; retain them as the existing search/gate statistic only. Confirm on new seeds before naming a preferred product mode. |
| What scale source is allowed? | Image-only beta; calibrated source-free rate; separate counter total dose; hybrid | Fit beta, retain a same-head source-free calibrated-rate reference, and use the separate counter only as a qualified drift check/prior when source contamination and spectral/angular transfer are controlled. D-37 alone does not establish an ambient scale. Never tune a systematic over-subtraction because it happens to improve this seed set. |
| What calibration requirement? | Fixed10 min; fixed1 h; counts/metadata/uncertainty qualification | Qualify count budget and matching head/window/orientation/spectrum; record live time. 1 h is a better baseline candidate than 10 min, but the development spread cannot guarantee a localization target even at 1 h. Wrong bound must be invalid calibration, not silently accepted. |
| Is ideal precision recovery mandatory at every count? | All cells; scoped count/trust regime; measured-only correction | Scoped count/trust regime, with residual RMS and bias reported everywhere. The all-cells premise fails even with known scale/shape because noise and source-model mismatch remain. Do not promise ideal RMS. |
| How are iterations selected? |120 config default;400 Studio rule; new per-scope selection | Use120 only as the development starting point. Repeat DR-5 if keeping pair claims; otherwise adopt the explicit single-source localization rule above on separate selection seeds, including ideal cost, and freeze it. The existing400 rule does not cover these optics. |
| What bias tolerance? | Derive a cell-equivalence target; choose another engineering precision scope; descriptive only | Record precision and cost descriptively first; if a pass gate is required, accept the sqrt(2)*grid-step cell-equivalence convention with simultaneous paired-vector confidence bounds, only for count levels that independently meet the association/trust criterion. It is a declared engineering criterion with a geometric derivation, not a detector accuracy guarantee. |
| How broad is the next evidence run? | Full cyclic+non-cyclic matrix; stage worst edge then complete grid; edge only | Stage worst edge then full grid, preserve the complete planned matrix and runtime budget. Stop/review failures before authorizing any scientific claim. Keep all frozen-validation outcomes, including failures. |
| Studio now? | Include; defer | Defer. Engine calibration/context and evidence first; Studio needs per-channel calibration and transfer semantics. |
| Where are records changed? | Replace raw EV results; add corrected alternatives | Add theme67/new EV and calibrated limitation, preserve historical raw figures. Reserve decision/calibration IDs only after the planner checks current indices. |


### Transported source rates (development maps)

Open-window rate per Bq, mean +/- outer-seed SD, N=3 seeds x1,000,000 source photons at the specified edge; the development source levels normalize these maps exactly to 25/100/250/1000 counts. The1 MBq columns below are derived count budgets, not additional acquired-frame measurements.

| Case | Rate cps/Bq +/- SD |1 MBq x10 s expected counts +/- SD |1 MBq x60 s expected counts +/- SD |
|---|---:|---:|---:|
| lab | 7.71718054e-05 +/- 1.32e-08 | 771.718 +/- 0.132 | 4630.308 +/- 0.794 |
| head | 0.00017341398 +/- 2.43e-07 | 1734.140 +/- 2.426 | 10404.839 +/- 14.558 |
| head1m | 4.03876916e-06 +/- 1.03e-08 | 40.388 +/- 0.103 | 242.326 +/- 0.618 |
| head5m | 1.57340315e-07 +/- 7.6e-11 | 1.573 +/- 0.001 | 9.440 +/- 0.005 |

The normal default activity at 5 m can have fewer than25 counts, below this development grid; its full acquired-frame outcome remains in the proposed validation matrix, not certified by this turn. The new source-rate uncertainty is conditional on the fixed incident spectrum/model and excludes fabrication/environment systematic uncertainty.

### Excess over the ideal: paired signed vectors

Main t=60 conditions at S=250  and 1000,F=0.10/0.20. Field and ideal acquisition means share each outer seed/source map, but not identical Poisson draws. Delta dx/dy are mean +/- SD of seed differences; vector excess is length of their pooled mean +/- delta-method SE. Legacy scalar excess is the difference of pooled pull norms +/- SE from paired projected seed covariance. These delta intervals are exploratory, especially near zero. RMS cost is field RMS minus that estimator's ideal RMS, mean +/- paired-seed SD. All rows N=3 seeds x16 acquisitions in each environment, not96 independent calibrations.

| Case | S | F | Estimator | Delta dx +/- SD | Delta dy +/- SD | Vector excess +/- SE | Legacy scalar excess +/- SE | RMS cost +/- SD |
|---|---:|---:|---|---:|---:|---:|---:|---:|
| lab | 250 | 0.1 | E0 | -6.765 +/- 0.367 | -7.998 +/- 0.178 | 10.475 +/- 0.213 | 9.719 +/- 0.218 | 10.241 +/- 0.366 |
| lab | 250 | 0.1 | E1/model | -6.133 +/- 1.835 | -0.105 +/- 0.244 | 6.134 +/- 1.061 | 6.132 +/- 1.058 | 7.531 +/- 2.598 |
| lab | 250 | 0.1 | E2/model | -5.720 +/- 1.781 | -0.308 +/- 0.381 | 5.728 +/- 1.030 | 5.711 +/- 1.026 | 5.935 +/- 1.835 |
| lab | 250 | 0.1 | E3/model | -1.990 +/- 0.771 | -0.617 +/- 0.711 | 2.083 +/- 0.519 | 1.113 +/- 0.518 | 4.741 +/- 1.486 |
| lab | 250 | 0.1 | E4/model | -0.423 +/- 0.455 | 0.030 +/- 0.293 | 0.425 +/- 0.253 | -0.177 +/- 0.296 | 1.276 +/- 1.773 |
| lab | 250 | 0.1 | E5/model | -1.029 +/- 1.541 | -0.075 +/- 0.474 | 1.031 +/- 0.905 | 0.414 +/- 0.930 | 2.334 +/- 3.591 |
| lab | 250 | 0.1 | E6/model | -2.493 +/- 0.701 | -1.336 +/- 1.276 | 2.829 +/- 0.660 | 1.915 +/- 0.706 | 5.632 +/- 1.640 |
| lab | 250 | 0.1 | E7/model | -1.064 +/- 1.583 | -0.151 +/- 0.394 | 1.075 +/- 0.934 | 0.356 +/- 0.932 | 2.340 +/- 3.570 |
| lab | 250 | 0.2 | E0 | -7.880 +/- 0.330 | -7.949 +/- 0.322 | 11.193 +/- 0.190 | 10.395 +/- 0.214 | 10.872 +/- 0.314 |
| lab | 250 | 0.2 | E1/model | -6.055 +/- 1.007 | 0.230 +/- 0.772 | 6.059 +/- 0.595 | 6.059 +/- 0.596 | 7.550 +/- 2.251 |
| lab | 250 | 0.2 | E2/model | -5.679 +/- 0.260 | 0.049 +/- 0.729 | 5.680 +/- 0.147 | 5.672 +/- 0.133 | 5.923 +/- 0.499 |
| lab | 250 | 0.2 | E3/model | -3.556 +/- 0.344 | -1.508 +/- 0.251 | 3.862 +/- 0.218 | 2.915 +/- 0.219 | 6.326 +/- 0.611 |
| lab | 250 | 0.2 | E4/model | -2.934 +/- 0.611 | -0.368 +/- 0.432 | 2.957 +/- 0.358 | 2.356 +/- 0.339 | 6.179 +/- 0.573 |
| lab | 250 | 0.2 | E5/model | -3.341 +/- 0.672 | -0.142 +/- 0.202 | 3.344 +/- 0.383 | 2.715 +/- 0.402 | 6.547 +/- 1.373 |
| lab | 250 | 0.2 | E6/model | -4.314 +/- 0.980 | -1.111 +/- 0.768 | 4.454 +/- 0.536 | 3.432 +/- 0.517 | 7.248 +/- 1.331 |
| lab | 250 | 0.2 | E7/model | -3.159 +/- 0.517 | -0.289 +/- 0.142 | 3.172 +/- 0.292 | 2.454 +/- 0.285 | 6.428 +/- 1.223 |
| lab | 1000 | 0.1 | E0 | -3.963 +/- 1.314 | -5.269 +/- 0.963 | 6.593 +/- 0.900 | 5.743 +/- 0.880 | 7.536 +/- 1.110 |
| lab | 1000 | 0.1 | E1/model | -0.340 +/- 0.604 | 0.001 +/- 0.024 | 0.340 +/- 0.349 | -0.320 +/- 0.240 | 1.275 +/- 2.188 |
| lab | 1000 | 0.1 | E2/model | -1.086 +/- 0.085 | -0.032 +/- 0.056 | 1.086 +/- 0.048 | 0.124 +/- 0.011 | 3.645 +/- 0.031 |
| lab | 1000 | 0.1 | E3/model | -0.003 +/- 0.041 | 0.000 +/- 0.000 | 0.003 +/- 0.024 | -0.003 +/- 0.023 | -0.000 +/- 0.039 |
| lab | 1000 | 0.1 | E4/model | 0.020 +/- 0.061 | -0.040 +/- 0.113 | 0.045 +/- 0.054 | 0.033 +/- 0.029 | 0.016 +/- 0.049 |
| lab | 1000 | 0.1 | E5/model | 0.015 +/- 0.060 | -0.046 +/- 0.115 | 0.049 +/- 0.059 | 0.030 +/- 0.027 | 0.019 +/- 0.047 |
| lab | 1000 | 0.1 | E6/model | -0.015 +/- 0.043 | 0.000 +/- 0.000 | 0.015 +/- 0.025 | -0.015 +/- 0.024 | -0.012 +/- 0.040 |
| lab | 1000 | 0.1 | E7/model | 0.030 +/- 0.038 | -0.025 +/- 0.032 | 0.039 +/- 0.021 | 0.026 +/- 0.021 | 0.041 +/- 0.043 |
| lab | 1000 | 0.2 | E0 | -6.555 +/- 0.103 | -8.071 +/- 0.263 | 10.397 +/- 0.155 | 9.512 +/- 0.169 | 9.698 +/- 0.294 |
| lab | 1000 | 0.2 | E1/model | -0.358 +/- 0.629 | 0.022 +/- 0.084 | 0.359 +/- 0.365 | -0.348 +/- 0.262 | 1.280 +/- 2.185 |
| lab | 1000 | 0.2 | E2/model | -0.778 +/- 0.568 | 0.016 +/- 0.171 | 0.778 +/- 0.328 | -0.187 +/- 0.325 | 2.404 +/- 2.146 |
| lab | 1000 | 0.2 | E3/model | -0.021 +/- 0.027 | 0.000 +/- 0.000 | 0.021 +/- 0.015 | -0.020 +/- 0.015 | -0.018 +/- 0.024 |
| lab | 1000 | 0.2 | E4/model | 0.002 +/- 0.066 | -0.035 +/- 0.085 | 0.035 +/- 0.051 | 0.014 +/- 0.049 | 0.005 +/- 0.078 |
| lab | 1000 | 0.2 | E5/model | -0.002 +/- 0.069 | -0.043 +/- 0.086 | 0.043 +/- 0.048 | 0.013 +/- 0.052 | 0.010 +/- 0.083 |
| lab | 1000 | 0.2 | E6/model | -0.035 +/- 0.026 | 0.000 +/- 0.000 | 0.035 +/- 0.015 | -0.033 +/- 0.014 | -0.030 +/- 0.024 |
| lab | 1000 | 0.2 | E7/model | 0.011 +/- 0.054 | -0.045 +/- 0.072 | 0.047 +/- 0.047 | 0.006 +/- 0.029 | 0.034 +/- 0.061 |
| head | 250 | 0.1 | E0 | -4.404 +/- 1.016 | 7.017 +/- 2.082 | 8.284 +/- 0.712 | 6.639 +/- 0.411 | 6.892 +/- 1.983 |
| head | 250 | 0.1 | E1/model | -2.782 +/- 0.638 | -0.658 +/- 0.282 | 2.858 +/- 0.386 | 2.248 +/- 0.389 | 6.016 +/- 0.366 |
| head | 250 | 0.1 | E2/model | -2.209 +/- 0.399 | -0.651 +/- 0.257 | 2.303 +/- 0.262 | 1.805 +/- 0.239 | 5.254 +/- 1.088 |
| head | 250 | 0.1 | E3/model | -5.330 +/- 1.572 | -2.986 +/- 0.998 | 6.109 +/- 0.647 | 5.818 +/- 0.617 | 7.204 +/- 0.399 |
| head | 250 | 0.1 | E4/model | -2.567 +/- 1.011 | 0.452 +/- 0.922 | 2.606 +/- 0.592 | 1.643 +/- 0.584 | 5.428 +/- 0.739 |
| head | 250 | 0.1 | E5/model | -2.154 +/- 1.137 | 0.589 +/- 0.782 | 2.233 +/- 0.669 | 1.255 +/- 0.663 | 4.866 +/- 0.788 |
| head | 250 | 0.1 | E6/model | -4.445 +/- 1.720 | -2.201 +/- 1.124 | 4.960 +/- 0.910 | 4.669 +/- 0.921 | 6.262 +/- 0.687 |
| head | 250 | 0.1 | E7/model | -2.307 +/- 1.298 | 0.560 +/- 0.713 | 2.374 +/- 0.766 | 1.578 +/- 0.759 | 5.126 +/- 0.933 |
| head | 250 | 0.2 | E0 | -4.653 +/- 1.137 | 6.441 +/- 2.145 | 7.945 +/- 0.620 | 6.355 +/- 0.257 | 6.985 +/- 2.036 |
| head | 250 | 0.2 | E1/model | -4.824 +/- 1.637 | 0.301 +/- 0.734 | 4.833 +/- 0.968 | 4.158 +/- 0.945 | 8.090 +/- 1.427 |
| head | 250 | 0.2 | E2/model | -4.786 +/- 1.673 | 0.009 +/- 1.075 | 4.786 +/- 0.967 | 4.251 +/- 0.933 | 8.046 +/- 1.594 |
| head | 250 | 0.2 | E3/model | -6.821 +/- 1.975 | -1.256 +/- 0.761 | 6.936 +/- 1.067 | 6.425 +/- 1.025 | 8.261 +/- 2.678 |
| head | 250 | 0.2 | E4/model | -4.915 +/- 1.119 | -0.143 +/- 0.814 | 4.917 +/- 0.651 | 3.994 +/- 0.657 | 8.470 +/- 1.211 |
| head | 250 | 0.2 | E5/model | -4.214 +/- 1.486 | -0.585 +/- 0.999 | 4.255 +/- 0.923 | 3.364 +/- 0.963 | 7.677 +/- 1.685 |
| head | 250 | 0.2 | E6/model | -5.379 +/- 1.594 | -0.729 +/- 0.891 | 5.428 +/- 0.969 | 4.884 +/- 1.002 | 7.119 +/- 3.264 |
| head | 250 | 0.2 | E7/model | -3.074 +/- 0.565 | -0.753 +/- 1.095 | 3.165 +/- 0.467 | 2.447 +/- 0.508 | 6.787 +/- 1.030 |
| head | 1000 | 0.1 | E0 | -5.591 +/- 0.136 | 5.320 +/- 0.052 | 7.718 +/- 0.045 | 6.737 +/- 0.076 | 8.928 +/- 0.140 |
| head | 1000 | 0.1 | E1/model | -0.035 +/- 0.035 | -0.013 +/- 0.072 | 0.037 +/- 0.033 | -0.032 +/- 0.012 | 0.012 +/- 0.014 |
| head | 1000 | 0.1 | E2/model | 0.026 +/- 0.026 | -0.000 +/- 0.044 | 0.026 +/- 0.015 | 0.026 +/- 0.015 | 0.078 +/- 0.052 |
| head | 1000 | 0.1 | E3/model | -0.793 +/- 0.569 | -0.780 +/- 0.533 | 1.112 +/- 0.450 | 0.856 +/- 0.434 | 2.431 +/- 1.457 |
| head | 1000 | 0.1 | E4/model | 0.070 +/- 0.048 | 0.016 +/- 0.056 | 0.072 +/- 0.034 | 0.063 +/- 0.018 | 0.083 +/- 0.018 |
| head | 1000 | 0.1 | E5/model | 0.055 +/- 0.048 | 0.011 +/- 0.053 | 0.056 +/- 0.033 | 0.050 +/- 0.018 | 0.070 +/- 0.019 |
| head | 1000 | 0.1 | E6/model | -0.961 +/- 0.097 | -0.463 +/- 0.486 | 1.066 +/- 0.142 | 0.622 +/- 0.136 | 3.086 +/- 0.539 |
| head | 1000 | 0.1 | E7/model | 0.035 +/- 0.046 | 0.012 +/- 0.058 | 0.037 +/- 0.036 | 0.034 +/- 0.025 | 0.065 +/- 0.028 |
| head | 1000 | 0.2 | E0 | -4.802 +/- 0.114 | 6.764 +/- 0.467 | 8.295 +/- 0.193 | 7.299 +/- 0.159 | 8.628 +/- 0.151 |
| head | 1000 | 0.2 | E1/model | -0.054 +/- 0.036 | -0.036 +/- 0.071 | 0.066 +/- 0.040 | -0.045 +/- 0.009 | 0.058 +/- 0.061 |
| head | 1000 | 0.2 | E2/model | 0.026 +/- 0.051 | -0.034 +/- 0.065 | 0.043 +/- 0.024 | 0.028 +/- 0.027 | 0.173 +/- 0.099 |
| head | 1000 | 0.2 | E3/model | -0.987 +/- 0.463 | -0.627 +/- 1.259 | 1.169 +/- 0.594 | 0.823 +/- 0.809 | 3.779 +/- 1.058 |
| head | 1000 | 0.2 | E4/model | 0.115 +/- 0.095 | 0.037 +/- 0.013 | 0.121 +/- 0.054 | 0.103 +/- 0.050 | 0.147 +/- 0.101 |
| head | 1000 | 0.2 | E5/model | 0.080 +/- 0.077 | 0.026 +/- 0.012 | 0.085 +/- 0.044 | 0.071 +/- 0.039 | 0.110 +/- 0.074 |
| head | 1000 | 0.2 | E6/model | -1.507 +/- 0.534 | -0.594 +/- 1.637 | 1.619 +/- 0.569 | 1.101 +/- 0.891 | 4.496 +/- 1.164 |
| head | 1000 | 0.2 | E7/model | 0.062 +/- 0.085 | 0.025 +/- 0.023 | 0.067 +/- 0.045 | 0.060 +/- 0.048 | 0.128 +/- 0.116 |
| head1m | 250 | 0.1 | E0 | -37.621 +/- 13.552 | 15.347 +/- 1.536 | 40.631 +/- 7.312 | 39.727 +/- 7.584 | 24.653 +/- 11.945 |
| head1m | 250 | 0.1 | E1/model | -26.155 +/- 1.358 | 6.053 +/- 10.333 | 26.846 +/- 0.653 | 26.507 +/- 0.248 | 32.375 +/- 3.745 |
| head1m | 250 | 0.1 | E2/model | -23.587 +/- 2.658 | 7.236 +/- 6.763 | 24.673 +/- 2.345 | 23.635 +/- 2.027 | 28.561 +/- 6.107 |
| head1m | 250 | 0.1 | E3/model | -33.324 +/- 2.518 | 7.304 +/- 8.129 | 34.115 +/- 1.866 | 33.829 +/- 1.614 | 25.550 +/- 1.972 |
| head1m | 250 | 0.1 | E4/model | -33.589 +/- 9.772 | 2.823 +/- 3.255 | 33.708 +/- 5.682 | 27.731 +/- 6.234 | 52.448 +/- 11.492 |
| head1m | 250 | 0.1 | E5/model | -32.372 +/- 13.126 | 2.396 +/- 3.754 | 32.461 +/- 7.595 | 26.503 +/- 8.271 | 50.915 +/- 11.396 |
| head1m | 250 | 0.1 | E6/model | -35.589 +/- 5.576 | 7.901 +/- 7.995 | 36.456 +/- 2.176 | 36.168 +/- 2.488 | 27.383 +/- 4.918 |
| head1m | 250 | 0.1 | E7/model | -30.585 +/- 12.177 | 4.255 +/- 3.472 | 30.880 +/- 7.170 | 20.646 +/- 7.581 | 51.018 +/- 9.878 |
| head1m | 250 | 0.2 | E0 | -44.087 +/- 8.225 | 15.330 +/- 1.542 | 46.676 +/- 4.622 | 45.971 +/- 4.607 | 27.918 +/- 8.309 |
| head1m | 250 | 0.2 | E1/model | -25.505 +/- 2.363 | 3.048 +/- 11.600 | 25.687 +/- 1.833 | 25.584 +/- 1.571 | 31.795 +/- 5.291 |
| head1m | 250 | 0.2 | E2/model | -23.477 +/- 4.975 | 4.143 +/- 10.217 | 23.840 +/- 3.777 | 23.327 +/- 3.179 | 27.739 +/- 2.481 |
| head1m | 250 | 0.2 | E3/model | -25.582 +/- 4.378 | 4.735 +/- 3.566 | 26.017 +/- 2.860 | 25.827 +/- 2.826 | 19.807 +/- 1.800 |
| head1m | 250 | 0.2 | E4/model | -47.482 +/- 17.940 | 7.544 +/- 7.842 | 48.077 +/- 10.935 | 41.962 +/- 9.756 | 60.064 +/- 16.404 |
| head1m | 250 | 0.2 | E5/model | -49.216 +/- 17.425 | 8.340 +/- 9.427 | 49.918 +/- 10.822 | 43.785 +/- 9.638 | 62.203 +/- 14.684 |
| head1m | 250 | 0.2 | E6/model | -23.430 +/- 6.077 | 2.491 +/- 5.466 | 23.562 +/- 3.749 | 23.500 +/- 3.592 | 18.531 +/- 9.217 |
| head1m | 250 | 0.2 | E7/model | -46.999 +/- 10.315 | 6.430 +/- 12.274 | 47.436 +/- 6.593 | 37.203 +/- 6.472 | 60.430 +/- 2.686 |
| head1m | 1000 | 0.1 | E0 | -12.917 +/- 5.910 | 9.769 +/- 1.630 | 16.195 +/- 2.925 | 11.577 +/- 6.693 | 14.882 +/- 6.716 |
| head1m | 1000 | 0.1 | E1/model | -10.425 +/- 9.715 | 1.262 +/- 3.451 | 10.501 +/- 5.423 | 9.278 +/- 5.339 | 20.640 +/- 14.678 |
| head1m | 1000 | 0.1 | E2/model | -10.385 +/- 7.111 | -0.497 +/- 0.598 | 10.397 +/- 4.112 | 10.098 +/- 3.920 | 14.335 +/- 8.095 |
| head1m | 1000 | 0.1 | E3/model | -3.433 +/- 5.458 | 0.792 +/- 1.357 | 3.523 +/- 3.247 | -1.740 +/- 4.464 | 8.336 +/- 13.418 |
| head1m | 1000 | 0.1 | E4/model | -0.251 +/- 0.076 | 0.381 +/- 0.136 | 0.456 +/- 0.087 | -0.250 +/- 0.032 | -0.184 +/- 0.098 |
| head1m | 1000 | 0.1 | E5/model | -0.293 +/- 0.082 | 0.140 +/- 0.155 | 0.325 +/- 0.075 | -0.298 +/- 0.045 | -0.239 +/- 0.115 |
| head1m | 1000 | 0.1 | E6/model | -3.390 +/- 5.188 | -0.235 +/- 0.957 | 3.398 +/- 2.952 | -1.755 +/- 4.282 | 8.494 +/- 13.585 |
| head1m | 1000 | 0.1 | E7/model | -0.894 +/- 0.056 | 0.494 +/- 0.487 | 1.021 +/- 0.109 | -0.927 +/- 0.028 | -0.668 +/- 0.077 |
| head1m | 1000 | 0.2 | E0 | -37.065 +/- 12.872 | 14.611 +/- 0.459 | 39.841 +/- 7.003 | 34.819 +/- 11.671 | 42.087 +/- 2.934 |
| head1m | 1000 | 0.2 | E1/model | -22.693 +/- 6.448 | -7.951 +/- 9.101 | 24.045 +/- 1.787 | 23.674 +/- 1.154 | 39.933 +/- 11.133 |
| head1m | 1000 | 0.2 | E2/model | -21.654 +/- 5.226 | -8.286 +/- 7.048 | 23.185 +/- 1.387 | 23.172 +/- 1.248 | 32.004 +/- 10.054 |
| head1m | 1000 | 0.2 | E3/model | -25.465 +/- 20.335 | 1.852 +/- 3.799 | 25.532 +/- 11.578 | 20.262 +/- 12.134 | 37.232 +/- 25.370 |
| head1m | 1000 | 0.2 | E4/model | -5.311 +/- 8.174 | 0.472 +/- 0.743 | 5.332 +/- 4.663 | -2.451 +/- 4.495 | 11.968 +/- 21.078 |
| head1m | 1000 | 0.2 | E5/model | -2.861 +/- 3.822 | 0.150 +/- 0.756 | 2.865 +/- 2.181 | -2.865 +/- 2.182 | 7.907 +/- 14.245 |
| head1m | 1000 | 0.2 | E6/model | -27.494 +/- 23.999 | 0.083 +/- 3.542 | 27.494 +/- 13.850 | 22.299 +/- 13.936 | 38.973 +/- 27.991 |
| head1m | 1000 | 0.2 | E7/model | -9.170 +/- 6.953 | 0.035 +/- 1.137 | 9.170 +/- 4.012 | -2.543 +/- 3.783 | 18.929 +/- 18.743 |
| head5m | 250 | 0.1 | E0 | -47.986 +/- 62.429 | 57.241 +/- 2.598 | 74.694 +/- 24.146 | 51.618 +/- 32.441 | 63.382 +/- 151.877 |
| head5m | 250 | 0.1 | E1/model | -155.884 +/- 29.506 | -16.316 +/- 91.282 | 156.735 +/- 15.052 | 150.967 +/- 18.330 | 187.377 +/- 46.621 |
| head5m | 250 | 0.1 | E2/model | -154.119 +/- 30.252 | -20.715 +/- 86.306 | 155.505 +/- 13.824 | 149.927 +/- 15.500 | 191.080 +/- 47.142 |
| head5m | 250 | 0.1 | E3/model | -141.242 +/- 17.360 | 16.764 +/- 25.825 | 142.234 +/- 9.960 | 135.481 +/- 9.635 | 230.738 +/- 70.906 |
| head5m | 250 | 0.1 | E4/model | -70.480 +/- 31.489 | 1.807 +/- 42.680 | 70.503 +/- 17.587 | 46.173 +/- 16.161 | 173.605 +/- 36.864 |
| head5m | 250 | 0.1 | E5/model | -93.276 +/- 11.833 | 3.344 +/- 41.026 | 93.336 +/- 5.991 | 69.022 +/- 4.520 | 201.994 +/- 21.263 |
| head5m | 250 | 0.1 | E6/model | -137.211 +/- 43.390 | -16.064 +/- 15.024 | 138.148 +/- 24.199 | 136.372 +/- 22.360 | 195.532 +/- 99.439 |
| head5m | 250 | 0.1 | E7/model | -118.502 +/- 53.654 | 7.360 +/- 33.023 | 118.731 +/- 31.687 | 95.030 +/- 35.994 | 160.089 +/- 91.623 |
| head5m | 250 | 0.2 | E0 | -88.427 +/- 18.809 | 58.822 +/- 2.329 | 106.204 +/- 9.733 | 90.155 +/- 9.349 | 103.806 +/- 66.230 |
| head5m | 250 | 0.2 | E1/model | -244.244 +/- 52.971 | -2.849 +/- 44.600 | 244.261 +/- 30.382 | 235.411 +/- 25.973 | 267.487 +/- 65.732 |
| head5m | 250 | 0.2 | E2/model | -240.294 +/- 53.205 | -1.933 +/- 43.981 | 240.301 +/- 30.581 | 230.503 +/- 25.904 | 266.929 +/- 65.533 |
| head5m | 250 | 0.2 | E3/model | -243.885 +/- 102.403 | 35.078 +/- 28.667 | 246.395 +/- 57.294 | 238.579 +/- 56.702 | 312.240 +/- 136.211 |
| head5m | 250 | 0.2 | E4/model | -124.960 +/- 28.607 | 8.774 +/- 30.012 | 125.268 +/- 15.870 | 100.852 +/- 18.719 | 227.508 +/- 34.638 |
| head5m | 250 | 0.2 | E5/model | -137.569 +/- 32.822 | 32.318 +/- 11.156 | 141.314 +/- 16.975 | 116.904 +/- 19.758 | 239.372 +/- 30.913 |
| head5m | 250 | 0.2 | E6/model | -259.010 +/- 63.638 | 3.879 +/- 12.991 | 259.039 +/- 36.625 | 255.115 +/- 35.622 | 300.881 +/- 98.288 |
| head5m | 250 | 0.2 | E7/model | -149.515 +/- 46.998 | 63.817 +/- 27.085 | 162.565 +/- 18.825 | 138.490 +/- 18.236 | 187.854 +/- 79.531 |
| head5m | 1000 | 0.1 | E0 | 0.000 +/- 0.000 | 54.575 +/- 0.116 | 54.575 +/- 0.067 | 12.515 +/- 0.060 | 12.518 +/- 0.105 |
| head5m | 1000 | 0.1 | E1/model | -8.798 +/- 7.619 | 10.747 +/- 9.408 | 13.889 +/- 6.989 | -3.646 +/- 0.608 | 32.616 +/- 28.154 |
| head5m | 1000 | 0.1 | E2/model | -8.286 +/- 7.176 | 11.048 +/- 9.568 | 13.810 +/- 6.905 | -3.377 +/- 0.640 | 29.821 +/- 25.825 |
| head5m | 1000 | 0.1 | E3/model | 0.000 +/- 0.000 | 5.554 +/- 1.928 | 5.554 +/- 1.113 | -3.953 +/- 0.723 | 1.405 +/- 0.483 |
| head5m | 1000 | 0.1 | E4/model | -1.325 +/- 0.691 | 1.441 +/- 0.487 | 1.957 +/- 0.341 | -1.436 +/- 0.384 | -1.385 +/- 0.715 |
| head5m | 1000 | 0.1 | E5/model | -1.290 +/- 0.667 | 0.851 +/- 0.492 | 1.546 +/- 0.341 | -1.374 +/- 0.373 | -1.371 +/- 0.679 |
| head5m | 1000 | 0.1 | E6/model | 0.000 +/- 0.000 | -0.284 +/- 0.944 | 0.284 +/- 0.545 | 0.218 +/- 0.419 | 1.630 +/- 1.382 |
| head5m | 1000 | 0.1 | E7/model | -19.865 +/- 19.089 | 2.303 +/- 2.700 | 19.998 +/- 10.771 | -19.274 +/- 8.922 | 36.371 +/- 65.224 |
| head5m | 1000 | 0.2 | E0 | 0.000 +/- 0.000 | 55.981 +/- 0.106 | 55.981 +/- 0.061 | 13.786 +/- 0.055 | 13.791 +/- 0.098 |
| head5m | 1000 | 0.2 | E1/model | -37.856 +/- 21.654 | -0.385 +/- 10.870 | 37.858 +/- 12.561 | 26.146 +/- 13.713 | 88.611 +/- 45.480 |
| head5m | 1000 | 0.2 | E2/model | -40.601 +/- 24.899 | 3.867 +/- 12.684 | 40.785 +/- 14.128 | 27.241 +/- 13.990 | 87.520 +/- 49.506 |
| head5m | 1000 | 0.2 | E3/model | -23.477 +/- 20.332 | 7.570 +/- 3.707 | 24.667 +/- 11.824 | 16.077 +/- 10.603 | 81.519 +/- 69.871 |
| head5m | 1000 | 0.2 | E4/model | -0.368 +/- 0.760 | 1.431 +/- 0.444 | 1.478 +/- 0.142 | -0.479 +/- 0.407 | -0.440 +/- 0.595 |
| head5m | 1000 | 0.2 | E5/model | -0.396 +/- 0.748 | 0.889 +/- 0.383 | 0.973 +/- 0.044 | -0.484 +/- 0.399 | -0.487 +/- 0.565 |
| head5m | 1000 | 0.2 | E6/model | -23.477 +/- 20.332 | 1.590 +/- 3.021 | 23.531 +/- 11.823 | 18.198 +/- 10.009 | 82.063 +/- 69.610 |
| head5m | 1000 | 0.2 | E7/model | -26.614 +/- 23.166 | 9.513 +/- 5.273 | 28.263 +/- 11.809 | -28.129 +/- 12.891 | 74.555 +/- 63.741 |

## Complete measured-number appendices

The following tables include every estimator/variant, signed mean, RMS, association fraction, fitted B where relevant and uncertainty from this development experiment. All default/no-field results are included. The 10 s ideal rows and the high-count model rows intentionally reproduce the same maps/RNG streams as the 60 s experiment; **do not pool duplicates as96 observations**. Scale-only E1/E2/E4 rows intentionally repeat values because those estimators do not consume the external scale.

### 60 s core and sensitivities

All tables: mean +/- sample SD of three outer-seed means, seeds 330001, 330007, 330019; 16 acquisitions per seed (48 per condition). Pull is the length of the pooled signed mean; its uncertainty is the delta-method SE, not the SD. Fractions are descriptive; 48/48 alone cannot certify 95% success (one-sided 95% lower bound 93.95%, conditional independence only).

| Case | t s | F uSv/h | S | Estimator | B mean +/- SD | dx mm mean +/- SD | dy mm mean +/- SD | Pull mm +/- SE | RMS mm mean +/- SD | Within element mean +/- SD | Bfit mean +/- SD |
|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|
| head | 60 | 0 | 25 | E0 | 0.000 +/- 0.000 | -6.518 +/- 2.691 | -0.963 +/- 1.877 | 6.588 +/- 1.686 | 10.094 +/- 2.330 | 0.188 +/- 0.188 | - |
| head | 60 | 0 | 25 | E1/model | 0.000 +/- 0.000 | -2.284 +/- 1.487 | -0.483 +/- 0.303 | 2.335 +/- 0.859 | 5.874 +/- 1.941 | 0.688 +/- 0.062 | - |
| head | 60 | 0 | 25 | E2/model | 0.000 +/- 0.000 | -2.041 +/- 1.485 | -0.539 +/- 0.185 | 2.111 +/- 0.856 | 5.605 +/- 1.911 | 0.708 +/- 0.072 | - |
| head | 60 | 0 | 25 | E3/model | 0.000 +/- 0.000 | -5.969 +/- 2.033 | -0.234 +/- 2.091 | 5.974 +/- 1.218 | 9.805 +/- 1.557 | 0.229 +/- 0.157 | 0.723 +/- 0.225 |
| head | 60 | 0 | 25 | E4/model | 0.000 +/- 0.000 | -3.708 +/- 0.828 | 0.570 +/- 0.744 | 3.751 +/- 0.425 | 8.016 +/- 0.787 | 0.500 +/- 0.062 | 0.035 +/- 0.061 |
| head | 60 | 0 | 25 | E5/model | 0.000 +/- 0.000 | -3.707 +/- 0.827 | 0.570 +/- 0.744 | 3.751 +/- 0.425 | 8.016 +/- 0.788 | 0.500 +/- 0.062 | - |
| head | 60 | 0 | 25 | E6/model | 0.000 +/- 0.000 | -6.518 +/- 2.691 | -0.963 +/- 1.877 | 6.588 +/- 1.686 | 10.094 +/- 2.330 | 0.188 +/- 0.188 | - |
| head | 60 | 0 | 25 | E7/model | 0.000 +/- 0.000 | -3.073 +/- 0.583 | 0.366 +/- 1.095 | 3.095 +/- 0.309 | 7.083 +/- 0.643 | 0.562 +/- 0.062 | 0.723 +/- 0.225 |
| head | 60 | 0 | 100 | E0 | 0.000 +/- 0.000 | -3.148 +/- 1.191 | -2.464 +/- 0.654 | 3.997 +/- 0.734 | 8.033 +/- 1.603 | 0.458 +/- 0.157 | - |
| head | 60 | 0 | 100 | E1/model | 0.000 +/- 0.000 | 0.296 +/- 0.072 | -0.252 +/- 0.135 | 0.389 +/- 0.020 | 0.691 +/- 0.268 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 100 | E2/model | 0.000 +/- 0.000 | 0.263 +/- 0.059 | -0.197 +/- 0.165 | 0.328 +/- 0.030 | 0.699 +/- 0.256 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 100 | E3/model | 0.000 +/- 0.000 | -2.502 +/- 1.004 | -1.546 +/- 0.368 | 2.941 +/- 0.576 | 7.363 +/- 1.431 | 0.542 +/- 0.130 | 5.888 +/- 1.078 |
| head | 60 | 0 | 100 | E4/model | 0.000 +/- 0.000 | -0.291 +/- 0.677 | -0.424 +/- 0.557 | 0.514 +/- 0.483 | 3.694 +/- 1.852 | 0.938 +/- 0.000 | 0.019 +/- 0.030 |
| head | 60 | 0 | 100 | E5/model | 0.000 +/- 0.000 | -0.290 +/- 0.678 | -0.422 +/- 0.556 | 0.512 +/- 0.483 | 3.695 +/- 1.850 | 0.938 +/- 0.000 | - |
| head | 60 | 0 | 100 | E6/model | 0.000 +/- 0.000 | -3.148 +/- 1.191 | -2.464 +/- 0.654 | 3.997 +/- 0.734 | 8.033 +/- 1.603 | 0.458 +/- 0.157 | - |
| head | 60 | 0 | 100 | E7/model | 0.000 +/- 0.000 | 0.361 +/- 0.155 | -0.065 +/- 0.019 | 0.367 +/- 0.087 | 0.812 +/- 0.404 | 0.979 +/- 0.036 | 5.888 +/- 1.078 |
| head | 60 | 0 | 250 | E0 | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E1/measured3600 | 0.000 +/- 0.000 | 0.319 +/- 0.016 | -0.157 +/- 0.073 | 0.356 +/- 0.023 | 0.459 +/- 0.050 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E1/measured600 | 0.000 +/- 0.000 | 0.321 +/- 0.015 | -0.152 +/- 0.101 | 0.355 +/- 0.022 | 0.450 +/- 0.072 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E1/model | 0.000 +/- 0.000 | 0.313 +/- 0.006 | -0.169 +/- 0.090 | 0.356 +/- 0.026 | 0.465 +/- 0.058 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E1/oracleShape | 0.000 +/- 0.000 | 0.313 +/- 0.008 | -0.171 +/- 0.088 | 0.357 +/- 0.025 | 0.466 +/- 0.059 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E1/scale0.5 | 0.000 +/- 0.000 | 0.313 +/- 0.006 | -0.169 +/- 0.090 | 0.356 +/- 0.026 | 0.465 +/- 0.058 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E1/scale0.75 | 0.000 +/- 0.000 | 0.313 +/- 0.006 | -0.169 +/- 0.090 | 0.356 +/- 0.026 | 0.465 +/- 0.058 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E1/scale0.9 | 0.000 +/- 0.000 | 0.313 +/- 0.006 | -0.169 +/- 0.090 | 0.356 +/- 0.026 | 0.465 +/- 0.058 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E1/scale1.1 | 0.000 +/- 0.000 | 0.313 +/- 0.006 | -0.169 +/- 0.090 | 0.356 +/- 0.026 | 0.465 +/- 0.058 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E1/scale1.25 | 0.000 +/- 0.000 | 0.313 +/- 0.006 | -0.169 +/- 0.090 | 0.356 +/- 0.026 | 0.465 +/- 0.058 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E1/scale1.5 | 0.000 +/- 0.000 | 0.313 +/- 0.006 | -0.169 +/- 0.090 | 0.356 +/- 0.026 | 0.465 +/- 0.058 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E1/wrongFront | 0.000 +/- 0.000 | 0.317 +/- 0.045 | -0.095 +/- 0.140 | 0.331 +/- 0.048 | 0.604 +/- 0.020 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E2/measured3600 | 0.000 +/- 0.000 | 0.263 +/- 0.030 | -0.077 +/- 0.089 | 0.274 +/- 0.027 | 0.373 +/- 0.087 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E2/measured600 | 0.000 +/- 0.000 | 0.263 +/- 0.030 | -0.077 +/- 0.068 | 0.274 +/- 0.025 | 0.372 +/- 0.098 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E2/model | 0.000 +/- 0.000 | 0.263 +/- 0.030 | -0.077 +/- 0.112 | 0.274 +/- 0.029 | 0.393 +/- 0.081 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E2/oracleShape | 0.000 +/- 0.000 | 0.263 +/- 0.030 | -0.086 +/- 0.097 | 0.276 +/- 0.028 | 0.387 +/- 0.091 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E2/scale0.5 | 0.000 +/- 0.000 | 0.263 +/- 0.030 | -0.077 +/- 0.112 | 0.274 +/- 0.029 | 0.393 +/- 0.081 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E2/scale0.75 | 0.000 +/- 0.000 | 0.263 +/- 0.030 | -0.077 +/- 0.112 | 0.274 +/- 0.029 | 0.393 +/- 0.081 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E2/scale0.9 | 0.000 +/- 0.000 | 0.263 +/- 0.030 | -0.077 +/- 0.112 | 0.274 +/- 0.029 | 0.393 +/- 0.081 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E2/scale1.1 | 0.000 +/- 0.000 | 0.263 +/- 0.030 | -0.077 +/- 0.112 | 0.274 +/- 0.029 | 0.393 +/- 0.081 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E2/scale1.25 | 0.000 +/- 0.000 | 0.263 +/- 0.030 | -0.077 +/- 0.112 | 0.274 +/- 0.029 | 0.393 +/- 0.081 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E2/scale1.5 | 0.000 +/- 0.000 | 0.263 +/- 0.030 | -0.077 +/- 0.112 | 0.274 +/- 0.029 | 0.393 +/- 0.081 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E2/wrongFront | 0.000 +/- 0.000 | 0.237 +/- 0.083 | -0.068 +/- 0.141 | 0.247 +/- 0.066 | 0.627 +/- 0.038 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E3/model | 0.000 +/- 0.000 | -0.093 +/- 0.150 | -0.776 +/- 0.200 | 0.782 +/- 0.123 | 2.294 +/- 1.395 | 0.938 +/- 0.062 | 14.149 +/- 4.626 |
| head | 60 | 0 | 250 | E4/measured3600 | 0.000 +/- 0.000 | 0.440 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | 0.377 +/- 0.568 |
| head | 60 | 0 | 250 | E4/measured600 | 0.000 +/- 0.000 | 0.440 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | 0.427 +/- 0.621 |
| head | 60 | 0 | 250 | E4/model | 0.000 +/- 0.000 | 0.440 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | 0.333 +/- 0.488 |
| head | 60 | 0 | 250 | E4/oracleShape | 0.000 +/- 0.000 | 0.440 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | 0.352 +/- 0.523 |
| head | 60 | 0 | 250 | E4/scale0.5 | 0.000 +/- 0.000 | 0.440 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | 0.333 +/- 0.488 |
| head | 60 | 0 | 250 | E4/scale0.75 | 0.000 +/- 0.000 | 0.440 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | 0.333 +/- 0.488 |
| head | 60 | 0 | 250 | E4/scale0.9 | 0.000 +/- 0.000 | 0.440 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | 0.333 +/- 0.488 |
| head | 60 | 0 | 250 | E4/scale1.1 | 0.000 +/- 0.000 | 0.440 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | 0.333 +/- 0.488 |
| head | 60 | 0 | 250 | E4/scale1.25 | 0.000 +/- 0.000 | 0.440 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | 0.333 +/- 0.488 |
| head | 60 | 0 | 250 | E4/scale1.5 | 0.000 +/- 0.000 | 0.440 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | 0.333 +/- 0.488 |
| head | 60 | 0 | 250 | E4/wrongFront | 0.000 +/- 0.000 | 0.440 +/- 0.031 | -0.227 +/- 0.092 | 0.495 +/- 0.013 | 0.602 +/- 0.054 | 1.000 +/- 0.000 | 2.027 +/- 0.342 |
| head | 60 | 0 | 250 | E4@240/model | 0.000 +/- 0.000 | 0.451 +/- 0.048 | -0.207 +/- 0.079 | 0.496 +/- 0.020 | 0.628 +/- 0.061 | 1.000 +/- 0.000 | 0.211 +/- 0.361 |
| head | 60 | 0 | 250 | E4@400/model | 0.000 +/- 0.000 | 0.448 +/- 0.054 | -0.199 +/- 0.077 | 0.490 +/- 0.022 | 0.633 +/- 0.066 | 1.000 +/- 0.000 | 0.165 +/- 0.286 |
| head | 60 | 0 | 250 | E4@60/model | 0.000 +/- 0.000 | 0.425 +/- 0.022 | -0.240 +/- 0.096 | 0.488 +/- 0.030 | 0.568 +/- 0.007 | 1.000 +/- 0.000 | 0.593 +/- 0.579 |
| head | 60 | 0 | 250 | E5/measured3600 | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5/measured600 | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5/model | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5/oracleShape | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5/scale0.5 | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5/scale0.75 | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5/scale0.9 | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5/scale1.1 | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5/scale1.25 | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5/scale1.5 | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5/wrongFront | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5@240/model | 0.000 +/- 0.000 | 0.451 +/- 0.048 | -0.207 +/- 0.078 | 0.496 +/- 0.020 | 0.628 +/- 0.061 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5@400/model | 0.000 +/- 0.000 | 0.448 +/- 0.054 | -0.199 +/- 0.077 | 0.490 +/- 0.022 | 0.633 +/- 0.067 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E5@60/model | 0.000 +/- 0.000 | 0.429 +/- 0.020 | -0.241 +/- 0.095 | 0.492 +/- 0.030 | 0.572 +/- 0.005 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 250 | E6/measured3600 | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E6/measured600 | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E6/model | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E6/oracleShape | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E6/scale0.5 | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E6/scale0.75 | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E6/scale0.9 | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E6/scale1.1 | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E6/scale1.25 | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E6/scale1.5 | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E6/wrongFront | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 60 | 0 | 250 | E7/model | 0.000 +/- 0.000 | 0.373 +/- 0.022 | -0.145 +/- 0.103 | 0.400 +/- 0.020 | 0.512 +/- 0.015 | 1.000 +/- 0.000 | 14.149 +/- 4.626 |
| head | 60 | 0 | 1000 | E0 | 0.000 +/- 0.000 | 0.269 +/- 0.171 | -0.419 +/- 0.050 | 0.498 +/- 0.029 | 0.828 +/- 0.086 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | 0.375 +/- 0.005 | -0.061 +/- 0.028 | 0.380 +/- 0.002 | 0.389 +/- 0.002 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | 0.228 +/- 0.015 | 0.000 +/- 0.000 | 0.228 +/- 0.009 | 0.235 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | 0.220 +/- 0.141 | -0.430 +/- 0.064 | 0.483 +/- 0.004 | 0.789 +/- 0.064 | 1.000 +/- 0.000 | 48.895 +/- 7.653 |
| head | 60 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 0.456 +/- 0.016 | -0.145 +/- 0.043 | 0.479 +/- 0.004 | 0.497 +/- 0.010 | 1.000 +/- 0.000 | 0.333 +/- 0.527 |
| head | 60 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 0.456 +/- 0.016 | -0.145 +/- 0.044 | 0.479 +/- 0.004 | 0.497 +/- 0.010 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | 0.269 +/- 0.171 | -0.419 +/- 0.050 | 0.498 +/- 0.029 | 0.828 +/- 0.086 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 0.419 +/- 0.018 | -0.035 +/- 0.039 | 0.421 +/- 0.009 | 0.437 +/- 0.017 | 1.000 +/- 0.000 | 48.895 +/- 7.653 |
| head | 60 | 0.05 | 25 | E0 | 1005.933 +/- 0.468 | -5.560 +/- 0.375 | 4.321 +/- 1.003 | 7.042 +/- 0.199 | 9.804 +/- 0.076 | 0.000 +/- 0.000 | - |
| head | 60 | 0.05 | 25 | E1/model | 1005.933 +/- 0.468 | -6.943 +/- 0.745 | -0.139 +/- 0.565 | 6.944 +/- 0.424 | 10.265 +/- 1.007 | 0.062 +/- 0.000 | - |
| head | 60 | 0.05 | 25 | E2/model | 1005.933 +/- 0.468 | -6.964 +/- 0.681 | -0.180 +/- 0.556 | 6.966 +/- 0.386 | 10.262 +/- 0.942 | 0.062 +/- 0.000 | - |
| head | 60 | 0.05 | 25 | E3/model | 1005.933 +/- 0.468 | -7.582 +/- 1.171 | 0.495 +/- 0.417 | 7.598 +/- 0.667 | 11.089 +/- 0.949 | 0.021 +/- 0.036 | 916.975 +/- 6.981 |
| head | 60 | 0.05 | 25 | E4/model | 1005.933 +/- 0.468 | -7.387 +/- 1.388 | -1.260 +/- 1.710 | 7.493 +/- 0.689 | 10.885 +/- 0.885 | 0.083 +/- 0.036 | 912.439 +/- 10.975 |
| head | 60 | 0.05 | 25 | E5/model | 1005.933 +/- 0.468 | -7.267 +/- 1.243 | -1.047 +/- 1.937 | 7.342 +/- 0.606 | 10.911 +/- 0.932 | 0.083 +/- 0.036 | - |
| head | 60 | 0.05 | 25 | E6/model | 1005.933 +/- 0.468 | -7.499 +/- 0.838 | 0.499 +/- 0.012 | 7.516 +/- 0.483 | 10.865 +/- 0.994 | 0.083 +/- 0.036 | - |
| head | 60 | 0.05 | 25 | E7/model | 1005.933 +/- 0.468 | -7.217 +/- 1.210 | -0.889 +/- 1.262 | 7.271 +/- 0.660 | 10.857 +/- 0.931 | 0.042 +/- 0.036 | 916.975 +/- 6.981 |
| head | 60 | 0.05 | 100 | E0 | 1005.933 +/- 0.468 | -5.273 +/- 0.422 | 4.607 +/- 1.329 | 7.002 +/- 0.326 | 9.645 +/- 0.110 | 0.000 +/- 0.000 | - |
| head | 60 | 0.05 | 100 | E1/model | 1005.933 +/- 0.468 | -5.599 +/- 2.270 | -1.385 +/- 1.878 | 5.768 +/- 1.532 | 9.624 +/- 1.662 | 0.333 +/- 0.253 | - |
| head | 60 | 0.05 | 100 | E2/model | 1005.933 +/- 0.468 | -6.056 +/- 2.278 | -1.447 +/- 1.773 | 6.227 +/- 1.502 | 9.912 +/- 1.658 | 0.333 +/- 0.237 | - |
| head | 60 | 0.05 | 100 | E3/model | 1005.933 +/- 0.468 | -6.930 +/- 0.757 | -1.624 +/- 1.462 | 7.118 +/- 0.556 | 10.494 +/- 0.674 | 0.062 +/- 0.062 | 957.227 +/- 7.396 |
| head | 60 | 0.05 | 100 | E4/model | 1005.933 +/- 0.468 | -7.168 +/- 1.392 | 0.406 +/- 0.543 | 7.179 +/- 0.790 | 11.270 +/- 0.941 | 0.250 +/- 0.062 | 914.165 +/- 20.975 |
| head | 60 | 0.05 | 100 | E5/model | 1005.933 +/- 0.468 | -6.570 +/- 1.159 | -0.409 +/- 0.220 | 6.582 +/- 0.663 | 10.819 +/- 0.669 | 0.354 +/- 0.072 | - |
| head | 60 | 0.05 | 100 | E6/model | 1005.933 +/- 0.468 | -6.607 +/- 1.106 | -1.422 +/- 1.562 | 6.758 +/- 0.805 | 10.202 +/- 0.945 | 0.083 +/- 0.095 | - |
| head | 60 | 0.05 | 100 | E7/model | 1005.933 +/- 0.468 | -6.443 +/- 1.293 | -0.466 +/- 1.341 | 6.459 +/- 0.723 | 10.786 +/- 0.863 | 0.292 +/- 0.095 | 957.227 +/- 7.396 |
| head | 60 | 0.05 | 250 | E0 | 1005.933 +/- 0.468 | -5.476 +/- 0.464 | 4.608 +/- 1.322 | 7.157 +/- 0.301 | 9.811 +/- 0.154 | 0.000 +/- 0.000 | - |
| head | 60 | 0.05 | 250 | E1/model | 1005.933 +/- 0.468 | -0.237 +/- 0.374 | -0.519 +/- 0.537 | 0.571 +/- 0.349 | 2.949 +/- 1.644 | 0.896 +/- 0.036 | - |
| head | 60 | 0.05 | 250 | E2/model | 1005.933 +/- 0.468 | -0.679 +/- 0.835 | -0.437 +/- 0.495 | 0.808 +/- 0.452 | 3.653 +/- 2.320 | 0.875 +/- 0.062 | - |
| head | 60 | 0.05 | 250 | E3/model | 1005.933 +/- 0.468 | -4.049 +/- 1.007 | -1.683 +/- 0.783 | 4.385 +/- 0.390 | 8.645 +/- 0.780 | 0.438 +/- 0.125 | 1007.089 +/- 10.662 |
| head | 60 | 0.05 | 250 | E4/model | 1005.933 +/- 0.468 | -1.157 +/- 0.507 | -0.056 +/- 0.596 | 1.158 +/- 0.287 | 5.031 +/- 0.751 | 0.875 +/- 0.062 | 896.887 +/- 23.079 |
| head | 60 | 0.05 | 250 | E5/model | 1005.933 +/- 0.468 | -1.021 +/- 0.313 | -0.361 +/- 0.298 | 1.083 +/- 0.221 | 4.835 +/- 0.637 | 0.896 +/- 0.036 | - |
| head | 60 | 0.05 | 250 | E6/model | 1005.933 +/- 0.468 | -4.405 +/- 1.486 | -1.163 +/- 0.876 | 4.556 +/- 0.732 | 8.899 +/- 1.185 | 0.375 +/- 0.108 | - |
| head | 60 | 0.05 | 250 | E7/model | 1005.933 +/- 0.468 | -0.081 +/- 0.436 | 0.001 +/- 0.610 | 0.081 +/- 0.253 | 2.405 +/- 1.474 | 0.938 +/- 0.062 | 1007.089 +/- 10.662 |
| head | 60 | 0.05 | 1000 | E0 | 1005.933 +/- 0.468 | -5.554 +/- 1.089 | 4.901 +/- 2.290 | 7.407 +/- 0.408 | 9.858 +/- 0.392 | 0.000 +/- 0.000 | - |
| head | 60 | 0.05 | 1000 | E1/model | 1005.933 +/- 0.468 | 0.377 +/- 0.005 | -0.075 +/- 0.038 | 0.384 +/- 0.007 | 0.427 +/- 0.035 | 1.000 +/- 0.000 | - |
| head | 60 | 0.05 | 1000 | E2/model | 1005.933 +/- 0.468 | 0.254 +/- 0.039 | 0.026 +/- 0.000 | 0.255 +/- 0.023 | 0.293 +/- 0.056 | 1.000 +/- 0.000 | - |
| head | 60 | 0.05 | 1000 | E3/model | 1005.933 +/- 0.468 | -0.118 +/- 0.232 | -0.773 +/- 0.281 | 0.782 +/- 0.176 | 2.273 +/- 1.266 | 0.958 +/- 0.036 | 1037.215 +/- 7.577 |
| head | 60 | 0.05 | 1000 | E4/model | 1005.933 +/- 0.468 | 0.515 +/- 0.013 | -0.103 +/- 0.041 | 0.525 +/- 0.004 | 0.552 +/- 0.010 | 1.000 +/- 0.000 | 841.341 +/- 4.865 |
| head | 60 | 0.05 | 1000 | E5/model | 1005.933 +/- 0.468 | 0.499 +/- 0.013 | -0.112 +/- 0.039 | 0.511 +/- 0.003 | 0.539 +/- 0.010 | 1.000 +/- 0.000 | - |
| head | 60 | 0.05 | 1000 | E6/model | 1005.933 +/- 0.468 | -0.326 +/- 0.567 | -0.952 +/- 0.615 | 1.006 +/- 0.440 | 2.856 +/- 1.854 | 0.938 +/- 0.062 | - |
| head | 60 | 0.05 | 1000 | E7/model | 1005.933 +/- 0.468 | 0.448 +/- 0.024 | -0.031 +/- 0.030 | 0.449 +/- 0.013 | 0.482 +/- 0.023 | 1.000 +/- 0.000 | 1037.215 +/- 7.577 |
| head | 60 | 0.1 | 25 | E0 | 2011.866 +/- 0.936 | -5.332 +/- 0.710 | 4.316 +/- 1.807 | 6.860 +/- 0.343 | 9.694 +/- 0.228 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 25 | E1/model | 2011.866 +/- 0.936 | -6.727 +/- 2.188 | -0.073 +/- 2.454 | 6.728 +/- 1.268 | 10.263 +/- 1.909 | 0.146 +/- 0.157 | - |
| head | 60 | 0.1 | 25 | E2/model | 2011.866 +/- 0.936 | -6.570 +/- 1.965 | -0.077 +/- 1.968 | 6.571 +/- 1.140 | 10.078 +/- 1.760 | 0.146 +/- 0.157 | - |
| head | 60 | 0.1 | 25 | E3/model | 2011.866 +/- 0.936 | -6.790 +/- 1.108 | -0.625 +/- 1.822 | 6.818 +/- 0.686 | 10.498 +/- 0.930 | 0.021 +/- 0.036 | 1866.132 +/- 16.888 |
| head | 60 | 0.1 | 25 | E4/model | 2011.866 +/- 0.936 | -8.202 +/- 0.989 | -0.999 +/- 2.052 | 8.263 +/- 0.688 | 11.735 +/- 0.773 | 0.062 +/- 0.000 | 1896.618 +/- 19.782 |
| head | 60 | 0.1 | 25 | E5/model | 2011.866 +/- 0.936 | -8.099 +/- 0.756 | -1.101 +/- 1.538 | 8.173 +/- 0.431 | 11.644 +/- 0.360 | 0.083 +/- 0.036 | - |
| head | 60 | 0.1 | 25 | E6/model | 2011.866 +/- 0.936 | -6.603 +/- 2.324 | -0.395 +/- 1.691 | 6.615 +/- 1.336 | 10.206 +/- 2.191 | 0.104 +/- 0.095 | - |
| head | 60 | 0.1 | 25 | E7/model | 2011.866 +/- 0.936 | -8.257 +/- 1.290 | -0.775 +/- 2.022 | 8.294 +/- 0.819 | 11.758 +/- 1.196 | 0.104 +/- 0.036 | 1866.132 +/- 16.888 |
| head | 60 | 0.1 | 100 | E0 | 2011.866 +/- 0.936 | -5.150 +/- 0.578 | 5.185 +/- 1.319 | 7.308 +/- 0.316 | 9.687 +/- 0.210 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 100 | E1/model | 2011.866 +/- 0.936 | -7.142 +/- 0.716 | -1.096 +/- 0.491 | 7.226 +/- 0.449 | 10.599 +/- 1.033 | 0.167 +/- 0.095 | - |
| head | 60 | 0.1 | 100 | E2/model | 2011.866 +/- 0.936 | -7.161 +/- 0.606 | -1.370 +/- 0.714 | 7.291 +/- 0.342 | 10.592 +/- 1.020 | 0.208 +/- 0.095 | - |
| head | 60 | 0.1 | 100 | E3/model | 2011.866 +/- 0.936 | -6.486 +/- 1.117 | -2.630 +/- 1.617 | 6.999 +/- 0.756 | 10.462 +/- 0.352 | 0.021 +/- 0.036 | 1898.976 +/- 12.674 |
| head | 60 | 0.1 | 100 | E4/model | 2011.866 +/- 0.936 | -6.300 +/- 3.008 | -1.263 +/- 0.157 | 6.425 +/- 1.701 | 10.483 +/- 2.573 | 0.271 +/- 0.191 | 1880.565 +/- 18.637 |
| head | 60 | 0.1 | 100 | E5/model | 2011.866 +/- 0.936 | -6.008 +/- 2.345 | -0.871 +/- 0.621 | 6.071 +/- 1.331 | 10.316 +/- 2.189 | 0.312 +/- 0.165 | - |
| head | 60 | 0.1 | 100 | E6/model | 2011.866 +/- 0.936 | -6.546 +/- 0.239 | -1.706 +/- 0.798 | 6.765 +/- 0.154 | 10.324 +/- 0.732 | 0.125 +/- 0.062 | - |
| head | 60 | 0.1 | 100 | E7/model | 2011.866 +/- 0.936 | -4.763 +/- 1.561 | -0.997 +/- 1.354 | 4.866 +/- 0.732 | 9.495 +/- 1.707 | 0.333 +/- 0.180 | 1898.976 +/- 12.674 |
| head | 60 | 0.1 | 250 | E0 | 2011.866 +/- 0.936 | -4.659 +/- 0.777 | 6.051 +/- 1.808 | 7.637 +/- 0.555 | 9.497 +/- 0.275 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 250 | E1/measured3600 | 2011.866 +/- 0.936 | -1.587 +/- 0.516 | 0.092 +/- 0.733 | 1.590 +/- 0.299 | 5.114 +/- 1.254 | 0.750 +/- 0.062 | - |
| head | 60 | 0.1 | 250 | E1/measured600 | 2011.866 +/- 0.936 | -1.320 +/- 0.377 | -0.170 +/- 0.355 | 1.331 +/- 0.195 | 4.786 +/- 0.210 | 0.708 +/- 0.095 | - |
| head | 60 | 0.1 | 250 | E1/model | 2011.866 +/- 0.936 | -2.469 +/- 0.642 | -0.827 +/- 0.275 | 2.604 +/- 0.398 | 6.481 +/- 0.417 | 0.646 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E1/oracleShape | 2011.866 +/- 0.936 | -2.219 +/- 0.345 | -0.542 +/- 0.277 | 2.284 +/- 0.202 | 6.161 +/- 0.302 | 0.688 +/- 0.000 | - |
| head | 60 | 0.1 | 250 | E1/scale0.5 | 2011.866 +/- 0.936 | -2.469 +/- 0.642 | -0.827 +/- 0.275 | 2.604 +/- 0.398 | 6.481 +/- 0.417 | 0.646 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E1/scale0.75 | 2011.866 +/- 0.936 | -2.469 +/- 0.642 | -0.827 +/- 0.275 | 2.604 +/- 0.398 | 6.481 +/- 0.417 | 0.646 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E1/scale0.9 | 2011.866 +/- 0.936 | -2.469 +/- 0.642 | -0.827 +/- 0.275 | 2.604 +/- 0.398 | 6.481 +/- 0.417 | 0.646 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E1/scale1.1 | 2011.866 +/- 0.936 | -2.469 +/- 0.642 | -0.827 +/- 0.275 | 2.604 +/- 0.398 | 6.481 +/- 0.417 | 0.646 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E1/scale1.25 | 2011.866 +/- 0.936 | -2.469 +/- 0.642 | -0.827 +/- 0.275 | 2.604 +/- 0.398 | 6.481 +/- 0.417 | 0.646 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E1/scale1.5 | 2011.866 +/- 0.936 | -2.469 +/- 0.642 | -0.827 +/- 0.275 | 2.604 +/- 0.398 | 6.481 +/- 0.417 | 0.646 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E1/wrongFront | 2011.866 +/- 0.936 | -8.924 +/- 0.410 | -5.333 +/- 0.041 | 10.397 +/- 0.195 | 10.494 +/- 0.452 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 250 | E2/measured3600 | 2011.866 +/- 0.936 | -1.364 +/- 0.364 | -0.265 +/- 0.486 | 1.390 +/- 0.155 | 4.884 +/- 0.771 | 0.792 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E2/measured600 | 2011.866 +/- 0.936 | -1.056 +/- 0.579 | -0.214 +/- 0.413 | 1.078 +/- 0.290 | 4.062 +/- 0.848 | 0.771 +/- 0.144 | - |
| head | 60 | 0.1 | 250 | E2/model | 2011.866 +/- 0.936 | -1.947 +/- 0.374 | -0.728 +/- 0.233 | 2.078 +/- 0.249 | 5.647 +/- 1.077 | 0.708 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E2/oracleShape | 2011.866 +/- 0.936 | -2.238 +/- 0.306 | -0.625 +/- 0.053 | 2.323 +/- 0.171 | 6.103 +/- 0.163 | 0.708 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E2/scale0.5 | 2011.866 +/- 0.936 | -1.947 +/- 0.374 | -0.728 +/- 0.233 | 2.078 +/- 0.249 | 5.647 +/- 1.077 | 0.708 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E2/scale0.75 | 2011.866 +/- 0.936 | -1.947 +/- 0.374 | -0.728 +/- 0.233 | 2.078 +/- 0.249 | 5.647 +/- 1.077 | 0.708 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E2/scale0.9 | 2011.866 +/- 0.936 | -1.947 +/- 0.374 | -0.728 +/- 0.233 | 2.078 +/- 0.249 | 5.647 +/- 1.077 | 0.708 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E2/scale1.1 | 2011.866 +/- 0.936 | -1.947 +/- 0.374 | -0.728 +/- 0.233 | 2.078 +/- 0.249 | 5.647 +/- 1.077 | 0.708 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E2/scale1.25 | 2011.866 +/- 0.936 | -1.947 +/- 0.374 | -0.728 +/- 0.233 | 2.078 +/- 0.249 | 5.647 +/- 1.077 | 0.708 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E2/scale1.5 | 2011.866 +/- 0.936 | -1.947 +/- 0.374 | -0.728 +/- 0.233 | 2.078 +/- 0.249 | 5.647 +/- 1.077 | 0.708 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E2/wrongFront | 2011.866 +/- 0.936 | -8.968 +/- 0.499 | -5.343 +/- 0.000 | 10.438 +/- 0.247 | 10.537 +/- 0.537 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 250 | E3/model | 2011.866 +/- 0.936 | -5.423 +/- 1.721 | -3.763 +/- 0.819 | 6.600 +/- 0.702 | 9.498 +/- 0.996 | 0.188 +/- 0.000 | 1972.224 +/- 22.080 |
| head | 60 | 0.1 | 250 | E4/measured3600 | 2011.866 +/- 0.936 | -2.107 +/- 1.621 | 0.283 +/- 0.580 | 2.125 +/- 0.927 | 6.023 +/- 1.731 | 0.750 +/- 0.165 | 1874.014 +/- 18.384 |
| head | 60 | 0.1 | 250 | E4/measured600 | 2011.866 +/- 0.936 | -1.999 +/- 1.483 | -0.031 +/- 0.850 | 1.999 +/- 0.859 | 5.872 +/- 1.657 | 0.708 +/- 0.201 | 1810.916 +/- 33.553 |
| head | 60 | 0.1 | 250 | E4/model | 2011.866 +/- 0.936 | -2.127 +/- 0.980 | 0.224 +/- 0.919 | 2.138 +/- 0.576 | 6.031 +/- 0.793 | 0.688 +/- 0.165 | 1879.466 +/- 21.291 |
| head | 60 | 0.1 | 250 | E4/oracleShape | 2011.866 +/- 0.936 | -2.185 +/- 1.060 | 0.125 +/- 0.984 | 2.188 +/- 0.612 | 6.098 +/- 0.909 | 0.688 +/- 0.165 | 1883.793 +/- 19.526 |
| head | 60 | 0.1 | 250 | E4/scale0.5 | 2011.866 +/- 0.936 | -2.127 +/- 0.980 | 0.224 +/- 0.919 | 2.138 +/- 0.576 | 6.031 +/- 0.793 | 0.688 +/- 0.165 | 1879.466 +/- 21.291 |
| head | 60 | 0.1 | 250 | E4/scale0.75 | 2011.866 +/- 0.936 | -2.127 +/- 0.980 | 0.224 +/- 0.919 | 2.138 +/- 0.576 | 6.031 +/- 0.793 | 0.688 +/- 0.165 | 1879.466 +/- 21.291 |
| head | 60 | 0.1 | 250 | E4/scale0.9 | 2011.866 +/- 0.936 | -2.127 +/- 0.980 | 0.224 +/- 0.919 | 2.138 +/- 0.576 | 6.031 +/- 0.793 | 0.688 +/- 0.165 | 1879.466 +/- 21.291 |
| head | 60 | 0.1 | 250 | E4/scale1.1 | 2011.866 +/- 0.936 | -2.127 +/- 0.980 | 0.224 +/- 0.919 | 2.138 +/- 0.576 | 6.031 +/- 0.793 | 0.688 +/- 0.165 | 1879.466 +/- 21.291 |
| head | 60 | 0.1 | 250 | E4/scale1.25 | 2011.866 +/- 0.936 | -2.127 +/- 0.980 | 0.224 +/- 0.919 | 2.138 +/- 0.576 | 6.031 +/- 0.793 | 0.688 +/- 0.165 | 1879.466 +/- 21.291 |
| head | 60 | 0.1 | 250 | E4/scale1.5 | 2011.866 +/- 0.936 | -2.127 +/- 0.980 | 0.224 +/- 0.919 | 2.138 +/- 0.576 | 6.031 +/- 0.793 | 0.688 +/- 0.165 | 1879.466 +/- 21.291 |
| head | 60 | 0.1 | 250 | E4/wrongFront | 2011.866 +/- 0.936 | -9.297 +/- 0.535 | -1.096 +/- 1.160 | 9.362 +/- 0.295 | 12.008 +/- 0.718 | 0.000 +/- 0.000 | 0.022 +/- 0.005 |
| head | 60 | 0.1 | 250 | E4@240/model | 2011.866 +/- 0.936 | -2.287 +/- 1.090 | 0.386 +/- 1.101 | 2.319 +/- 0.658 | 5.955 +/- 0.536 | 0.667 +/- 0.180 | 1868.991 +/- 22.745 |
| head | 60 | 0.1 | 250 | E4@400/model | 2011.866 +/- 0.936 | -2.560 +/- 1.273 | 0.226 +/- 0.946 | 2.570 +/- 0.768 | 6.422 +/- 0.985 | 0.667 +/- 0.191 | 1863.845 +/- 23.175 |
| head | 60 | 0.1 | 250 | E4@60/model | 2011.866 +/- 0.936 | -2.466 +/- 1.874 | 0.434 +/- 0.680 | 2.504 +/- 1.092 | 6.586 +/- 1.807 | 0.688 +/- 0.225 | 1893.080 +/- 22.247 |
| head | 60 | 0.1 | 250 | E5/measured3600 | 2011.866 +/- 0.936 | -1.743 +/- 2.276 | 0.283 +/- 0.556 | 1.766 +/- 1.300 | 4.820 +/- 3.565 | 0.771 +/- 0.201 | - |
| head | 60 | 0.1 | 250 | E5/measured600 | 2011.866 +/- 0.936 | -1.099 +/- 0.733 | -0.488 +/- 0.557 | 1.203 +/- 0.517 | 4.696 +/- 1.421 | 0.812 +/- 0.108 | - |
| head | 60 | 0.1 | 250 | E5/model | 2011.866 +/- 0.936 | -1.714 +/- 1.105 | 0.361 +/- 0.762 | 1.751 +/- 0.659 | 5.470 +/- 0.829 | 0.729 +/- 0.191 | - |
| head | 60 | 0.1 | 250 | E5/oracleShape | 2011.866 +/- 0.936 | -1.784 +/- 1.179 | 0.282 +/- 0.859 | 1.806 +/- 0.690 | 5.553 +/- 0.933 | 0.729 +/- 0.191 | - |
| head | 60 | 0.1 | 250 | E5/scale0.5 | 2011.866 +/- 0.936 | -7.881 +/- 1.477 | -0.066 +/- 1.114 | 7.881 +/- 0.848 | 11.252 +/- 1.113 | 0.146 +/- 0.072 | - |
| head | 60 | 0.1 | 250 | E5/scale0.75 | 2011.866 +/- 0.936 | -4.480 +/- 1.027 | -0.133 +/- 1.083 | 4.482 +/- 0.577 | 8.707 +/- 0.729 | 0.500 +/- 0.125 | - |
| head | 60 | 0.1 | 250 | E5/scale0.9 | 2011.866 +/- 0.936 | -2.058 +/- 1.117 | 0.191 +/- 1.188 | 2.067 +/- 0.644 | 6.082 +/- 0.922 | 0.688 +/- 0.165 | - |
| head | 60 | 0.1 | 250 | E5/scale1.1 | 2011.866 +/- 0.936 | -1.207 +/- 1.711 | 0.038 +/- 0.223 | 1.208 +/- 0.988 | 4.278 +/- 2.921 | 0.812 +/- 0.165 | - |
| head | 60 | 0.1 | 250 | E5/scale1.25 | 2011.866 +/- 0.936 | -0.650 +/- 1.170 | 0.015 +/- 0.547 | 0.650 +/- 0.676 | 3.793 +/- 2.527 | 0.875 +/- 0.108 | - |
| head | 60 | 0.1 | 250 | E5/scale1.5 | 2011.866 +/- 0.936 | -0.687 +/- 1.141 | -0.081 +/- 0.415 | 0.692 +/- 0.655 | 3.715 +/- 2.549 | 0.896 +/- 0.095 | - |
| head | 60 | 0.1 | 250 | E5/wrongFront | 2011.866 +/- 0.936 | -8.938 +/- 0.341 | -4.870 +/- 0.085 | 10.178 +/- 0.152 | 10.464 +/- 0.436 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 250 | E5@240/model | 2011.866 +/- 0.936 | -1.750 +/- 1.112 | 0.271 +/- 0.592 | 1.771 +/- 0.653 | 5.431 +/- 0.818 | 0.750 +/- 0.165 | - |
| head | 60 | 0.1 | 250 | E5@400/model | 2011.866 +/- 0.936 | -1.890 +/- 1.283 | 0.260 +/- 0.691 | 1.907 +/- 0.737 | 5.488 +/- 0.934 | 0.729 +/- 0.180 | - |
| head | 60 | 0.1 | 250 | E5@60/model | 2011.866 +/- 0.936 | -1.870 +/- 1.298 | 0.190 +/- 0.981 | 1.880 +/- 0.743 | 5.771 +/- 1.248 | 0.729 +/- 0.191 | - |
| head | 60 | 0.1 | 250 | E6/measured3600 | 2011.866 +/- 0.936 | -4.075 +/- 2.058 | -2.433 +/- 1.163 | 4.746 +/- 1.260 | 8.458 +/- 1.290 | 0.333 +/- 0.157 | - |
| head | 60 | 0.1 | 250 | E6/measured600 | 2011.866 +/- 0.936 | -4.589 +/- 1.994 | -2.694 +/- 0.755 | 5.321 +/- 1.209 | 8.988 +/- 1.637 | 0.312 +/- 0.125 | - |
| head | 60 | 0.1 | 250 | E6/model | 2011.866 +/- 0.936 | -4.700 +/- 2.076 | -3.166 +/- 1.224 | 5.667 +/- 1.172 | 8.867 +/- 1.496 | 0.271 +/- 0.095 | - |
| head | 60 | 0.1 | 250 | E6/oracleShape | 2011.866 +/- 0.936 | -4.673 +/- 2.168 | -3.047 +/- 1.222 | 5.579 +/- 1.261 | 8.797 +/- 1.595 | 0.271 +/- 0.072 | - |
| head | 60 | 0.1 | 250 | E6/scale0.5 | 2011.866 +/- 0.936 | -5.286 +/- 1.114 | 5.182 +/- 2.509 | 7.402 +/- 0.557 | 9.713 +/- 0.368 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 250 | E6/scale0.75 | 2011.866 +/- 0.936 | -5.881 +/- 0.171 | 2.594 +/- 1.899 | 6.428 +/- 0.452 | 10.010 +/- 0.349 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 250 | E6/scale0.9 | 2011.866 +/- 0.936 | -6.526 +/- 1.165 | -1.872 +/- 0.586 | 6.789 +/- 0.630 | 10.363 +/- 0.735 | 0.021 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E6/scale1.1 | 2011.866 +/- 0.936 | -2.920 +/- 0.517 | -1.214 +/- 0.311 | 3.162 +/- 0.208 | 7.046 +/- 0.497 | 0.604 +/- 0.036 | - |
| head | 60 | 0.1 | 250 | E6/scale1.25 | 2011.866 +/- 0.936 | -0.343 +/- 0.963 | -0.020 +/- 0.158 | 0.344 +/- 0.550 | 2.349 +/- 2.672 | 0.938 +/- 0.108 | - |
| head | 60 | 0.1 | 250 | E6/scale1.5 | 2011.866 +/- 0.936 | 0.355 +/- 0.009 | -0.071 +/- 0.120 | 0.362 +/- 0.009 | 0.753 +/- 0.106 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 250 | E6/wrongFront | 2011.866 +/- 0.936 | -8.656 +/- 0.163 | -5.383 +/- 0.035 | 10.193 +/- 0.078 | 10.227 +/- 0.143 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 250 | E7/model | 2011.866 +/- 0.936 | -1.935 +/- 1.276 | 0.415 +/- 0.658 | 1.979 +/- 0.759 | 5.638 +/- 0.947 | 0.729 +/- 0.180 | 1972.224 +/- 22.080 |
| head | 60 | 0.1 | 1000 | E0 | 2011.866 +/- 0.936 | -5.322 +/- 0.130 | 4.901 +/- 0.004 | 7.235 +/- 0.054 | 9.756 +/- 0.060 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/model | 2011.866 +/- 0.936 | 0.340 +/- 0.031 | -0.074 +/- 0.056 | 0.348 +/- 0.011 | 0.401 +/- 0.014 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/model | 2011.866 +/- 0.936 | 0.254 +/- 0.015 | -0.000 +/- 0.044 | 0.254 +/- 0.009 | 0.313 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E3/model | 2011.866 +/- 0.936 | -0.573 +/- 0.599 | -1.209 +/- 0.544 | 1.338 +/- 0.431 | 3.220 +/- 1.466 | 0.896 +/- 0.072 | 2082.819 +/- 12.668 |
| head | 60 | 0.1 | 1000 | E4/model | 2011.866 +/- 0.936 | 0.526 +/- 0.032 | -0.129 +/- 0.035 | 0.542 +/- 0.016 | 0.580 +/- 0.009 | 1.000 +/- 0.000 | 1824.052 +/- 11.395 |
| head | 60 | 0.1 | 1000 | E5/model | 2011.866 +/- 0.936 | 0.511 +/- 0.032 | -0.134 +/- 0.036 | 0.529 +/- 0.016 | 0.567 +/- 0.009 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E6/model | 2011.866 +/- 0.936 | -0.692 +/- 0.200 | -0.882 +/- 0.437 | 1.121 +/- 0.158 | 3.914 +/- 0.509 | 0.854 +/- 0.036 | - |
| head | 60 | 0.1 | 1000 | E7/model | 2011.866 +/- 0.936 | 0.454 +/- 0.039 | -0.023 +/- 0.056 | 0.455 +/- 0.021 | 0.502 +/- 0.028 | 1.000 +/- 0.000 | 2082.819 +/- 12.668 |
| head | 60 | 0.2 | 25 | E0 | 4023.732 +/- 1.873 | -4.564 +/- 0.191 | 6.344 +/- 0.501 | 7.815 +/- 0.186 | 9.474 +/- 0.073 | 0.000 +/- 0.000 | - |
| head | 60 | 0.2 | 25 | E1/model | 4023.732 +/- 1.873 | -7.396 +/- 0.625 | -0.613 +/- 1.359 | 7.422 +/- 0.298 | 11.061 +/- 0.429 | 0.062 +/- 0.062 | - |
| head | 60 | 0.2 | 25 | E2/model | 4023.732 +/- 1.873 | -7.118 +/- 1.030 | -0.462 +/- 1.253 | 7.133 +/- 0.562 | 10.818 +/- 0.716 | 0.083 +/- 0.072 | - |
| head | 60 | 0.2 | 25 | E3/model | 4023.732 +/- 1.873 | -6.749 +/- 1.141 | -1.051 +/- 1.174 | 6.830 +/- 0.661 | 10.574 +/- 1.283 | 0.021 +/- 0.036 | 3773.321 +/- 18.212 |
| head | 60 | 0.2 | 25 | E4/model | 4023.732 +/- 1.873 | -9.692 +/- 0.996 | -1.300 +/- 0.935 | 9.779 +/- 0.501 | 12.980 +/- 1.259 | 0.000 +/- 0.000 | 3882.563 +/- 21.128 |
| head | 60 | 0.2 | 25 | E5/model | 4023.732 +/- 1.873 | -9.446 +/- 0.530 | -0.933 +/- 1.266 | 9.492 +/- 0.233 | 12.784 +/- 0.760 | 0.021 +/- 0.036 | - |
| head | 60 | 0.2 | 25 | E6/model | 4023.732 +/- 1.873 | -7.242 +/- 0.320 | -0.224 +/- 1.482 | 7.246 +/- 0.208 | 10.855 +/- 0.350 | 0.062 +/- 0.108 | - |
| head | 60 | 0.2 | 25 | E7/model | 4023.732 +/- 1.873 | -9.015 +/- 1.342 | -0.663 +/- 0.597 | 9.039 +/- 0.751 | 12.631 +/- 1.435 | 0.042 +/- 0.036 | 3773.321 +/- 18.212 |
| head | 60 | 0.2 | 100 | E0 | 4023.732 +/- 1.873 | -4.306 +/- 0.310 | 6.919 +/- 0.502 | 8.150 +/- 0.161 | 9.379 +/- 0.123 | 0.000 +/- 0.000 | - |
| head | 60 | 0.2 | 100 | E1/model | 4023.732 +/- 1.873 | -6.407 +/- 1.860 | -0.603 +/- 0.123 | 6.436 +/- 1.064 | 10.069 +/- 1.222 | 0.104 +/- 0.130 | - |
| head | 60 | 0.2 | 100 | E2/model | 4023.732 +/- 1.873 | -6.373 +/- 1.871 | -0.591 +/- 0.093 | 6.401 +/- 1.072 | 10.059 +/- 1.237 | 0.104 +/- 0.130 | - |
| head | 60 | 0.2 | 100 | E3/model | 4023.732 +/- 1.873 | -7.864 +/- 0.324 | -2.611 +/- 0.986 | 8.286 +/- 0.124 | 11.313 +/- 0.593 | 0.000 +/- 0.000 | 3796.614 +/- 28.082 |
| head | 60 | 0.2 | 100 | E4/model | 4023.732 +/- 1.873 | -8.649 +/- 0.868 | 0.853 +/- 1.243 | 8.691 +/- 0.486 | 12.038 +/- 1.058 | 0.104 +/- 0.036 | 3867.405 +/- 35.483 |
| head | 60 | 0.2 | 100 | E5/model | 4023.732 +/- 1.873 | -8.161 +/- 0.654 | 0.556 +/- 1.529 | 8.180 +/- 0.387 | 11.767 +/- 0.840 | 0.104 +/- 0.036 | - |
| head | 60 | 0.2 | 100 | E6/model | 4023.732 +/- 1.873 | -7.036 +/- 1.583 | -1.115 +/- 0.587 | 7.124 +/- 0.895 | 10.481 +/- 1.049 | 0.042 +/- 0.072 | - |
| head | 60 | 0.2 | 100 | E7/model | 4023.732 +/- 1.873 | -8.027 +/- 1.128 | 0.057 +/- 0.836 | 8.027 +/- 0.649 | 11.513 +/- 0.591 | 0.104 +/- 0.036 | 3796.614 +/- 28.082 |
| head | 60 | 0.2 | 250 | E0 | 4023.732 +/- 1.873 | -4.907 +/- 0.734 | 5.476 +/- 1.808 | 7.353 +/- 0.495 | 9.590 +/- 0.254 | 0.000 +/- 0.000 | - |
| head | 60 | 0.2 | 250 | E1/model | 4023.732 +/- 1.873 | -4.511 +/- 1.631 | 0.132 +/- 0.757 | 4.513 +/- 0.953 | 8.555 +/- 1.463 | 0.438 +/- 0.108 | - |
| head | 60 | 0.2 | 250 | E2/model | 4023.732 +/- 1.873 | -4.524 +/- 1.648 | -0.068 +/- 1.147 | 4.524 +/- 0.943 | 8.439 +/- 1.628 | 0.458 +/- 0.095 | - |
| head | 60 | 0.2 | 250 | E3/model | 4023.732 +/- 1.873 | -6.914 +/- 1.844 | -2.033 +/- 0.922 | 7.207 +/- 0.902 | 10.555 +/- 1.513 | 0.083 +/- 0.036 | 3890.771 +/- 8.114 |
| head | 60 | 0.2 | 250 | E4/model | 4023.732 +/- 1.873 | -4.475 +/- 1.127 | -0.370 +/- 0.902 | 4.490 +/- 0.669 | 9.073 +/- 1.160 | 0.458 +/- 0.144 | 3836.296 +/- 18.319 |
| head | 60 | 0.2 | 250 | E5/model | 4023.732 +/- 1.873 | -3.774 +/- 1.510 | -0.813 +/- 1.073 | 3.860 +/- 0.975 | 8.280 +/- 1.632 | 0.542 +/- 0.130 | - |
| head | 60 | 0.2 | 250 | E6/model | 4023.732 +/- 1.873 | -5.633 +/- 1.299 | -1.694 +/- 0.561 | 5.883 +/- 0.774 | 9.724 +/- 1.522 | 0.208 +/- 0.157 | - |
| head | 60 | 0.2 | 250 | E7/model | 4023.732 +/- 1.873 | -2.702 +/- 0.567 | -0.899 +/- 1.193 | 2.847 +/- 0.528 | 7.299 +/- 1.036 | 0.562 +/- 0.108 | 3890.771 +/- 8.114 |
| head | 60 | 0.2 | 1000 | E0 | 4023.732 +/- 1.873 | -4.532 +/- 0.196 | 6.344 +/- 0.500 | 7.797 +/- 0.172 | 9.456 +/- 0.067 | 0.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/model | 4023.732 +/- 1.873 | 0.320 +/- 0.032 | -0.098 +/- 0.074 | 0.335 +/- 0.009 | 0.447 +/- 0.063 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/model | 4023.732 +/- 1.873 | 0.254 +/- 0.065 | -0.034 +/- 0.065 | 0.256 +/- 0.035 | 0.408 +/- 0.119 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E3/model | 4023.732 +/- 1.873 | -0.767 +/- 0.594 | -1.057 +/- 1.322 | 1.306 +/- 0.804 | 4.568 +/- 0.997 | 0.854 +/- 0.095 | 4056.112 +/- 12.233 |
| head | 60 | 0.2 | 1000 | E4/model | 4023.732 +/- 1.873 | 0.571 +/- 0.080 | -0.109 +/- 0.031 | 0.581 +/- 0.049 | 0.644 +/- 0.097 | 1.000 +/- 0.000 | 3731.087 +/- 20.372 |
| head | 60 | 0.2 | 1000 | E5/model | 4023.732 +/- 1.873 | 0.537 +/- 0.061 | -0.119 +/- 0.032 | 0.550 +/- 0.038 | 0.607 +/- 0.070 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E6/model | 4023.732 +/- 1.873 | -1.237 +/- 0.666 | -1.013 +/- 1.686 | 1.599 +/- 0.863 | 5.324 +/- 1.078 | 0.812 +/- 0.062 | - |
| head | 60 | 0.2 | 1000 | E7/model | 4023.732 +/- 1.873 | 0.481 +/- 0.073 | -0.009 +/- 0.029 | 0.481 +/- 0.042 | 0.564 +/- 0.103 | 1.000 +/- 0.000 | 4056.112 +/- 12.233 |
| head1m | 60 | 0 | 25 | E0 | 0.000 +/- 0.000 | -54.483 +/- 7.805 | -2.871 +/- 9.699 | 54.558 +/- 4.549 | 74.813 +/- 7.478 | 0.125 +/- 0.000 | - |
| head1m | 60 | 0 | 25 | E1/model | 0.000 +/- 0.000 | -44.162 +/- 10.015 | 3.935 +/- 19.149 | 44.337 +/- 6.271 | 62.905 +/- 7.146 | 0.042 +/- 0.072 | - |
| head1m | 60 | 0 | 25 | E2/model | 0.000 +/- 0.000 | -43.514 +/- 8.807 | 1.602 +/- 17.208 | 43.544 +/- 5.334 | 62.079 +/- 5.779 | 0.042 +/- 0.072 | - |
| head1m | 60 | 0 | 25 | E3/model | 0.000 +/- 0.000 | -42.549 +/- 13.434 | 4.660 +/- 14.414 | 42.803 +/- 7.126 | 65.560 +/- 12.894 | 0.125 +/- 0.062 | 0.501 +/- 0.227 |
| head1m | 60 | 0 | 25 | E4/model | 0.000 +/- 0.000 | -39.192 +/- 6.571 | 2.052 +/- 7.776 | 39.246 +/- 3.996 | 62.839 +/- 6.443 | 0.188 +/- 0.108 | 0.011 +/- 0.013 |
| head1m | 60 | 0 | 25 | E5/model | 0.000 +/- 0.000 | -39.189 +/- 6.569 | 2.052 +/- 7.770 | 39.243 +/- 3.995 | 62.832 +/- 6.442 | 0.188 +/- 0.108 | - |
| head1m | 60 | 0 | 25 | E6/model | 0.000 +/- 0.000 | -54.483 +/- 7.805 | -2.871 +/- 9.699 | 54.558 +/- 4.549 | 74.813 +/- 7.478 | 0.125 +/- 0.000 | - |
| head1m | 60 | 0 | 25 | E7/model | 0.000 +/- 0.000 | -38.996 +/- 7.166 | 3.034 +/- 0.908 | 39.113 +/- 4.101 | 62.674 +/- 5.181 | 0.229 +/- 0.144 | 0.501 +/- 0.227 |
| head1m | 60 | 0 | 100 | E0 | 0.000 +/- 0.000 | -32.482 +/- 8.623 | -2.596 +/- 4.339 | 32.585 +/- 5.065 | 61.981 +/- 9.230 | 0.583 +/- 0.095 | - |
| head1m | 60 | 0 | 100 | E1/model | 0.000 +/- 0.000 | -30.488 +/- 6.513 | -3.432 +/- 8.883 | 30.681 +/- 3.172 | 50.747 +/- 4.810 | 0.292 +/- 0.095 | - |
| head1m | 60 | 0 | 100 | E2/model | 0.000 +/- 0.000 | -29.207 +/- 4.924 | -2.541 +/- 5.507 | 29.318 +/- 2.562 | 49.783 +/- 3.052 | 0.292 +/- 0.095 | - |
| head1m | 60 | 0 | 100 | E3/model | 0.000 +/- 0.000 | -33.443 +/- 7.979 | -1.325 +/- 3.582 | 33.469 +/- 4.681 | 62.954 +/- 8.162 | 0.562 +/- 0.108 | 3.595 +/- 0.601 |
| head1m | 60 | 0 | 100 | E4/model | 0.000 +/- 0.000 | -10.252 +/- 7.558 | -0.423 +/- 4.064 | 10.261 +/- 4.433 | 37.441 +/- 12.496 | 0.750 +/- 0.062 | 0.005 +/- 0.004 |
| head1m | 60 | 0 | 100 | E5/model | 0.000 +/- 0.000 | -12.649 +/- 3.996 | -0.270 +/- 4.312 | 12.652 +/- 2.331 | 41.656 +/- 6.519 | 0.729 +/- 0.072 | - |
| head1m | 60 | 0 | 100 | E6/model | 0.000 +/- 0.000 | -32.482 +/- 8.623 | -2.596 +/- 4.339 | 32.585 +/- 5.065 | 61.981 +/- 9.230 | 0.583 +/- 0.095 | - |
| head1m | 60 | 0 | 100 | E7/model | 0.000 +/- 0.000 | -9.008 +/- 2.950 | -0.612 +/- 4.534 | 9.029 +/- 1.610 | 38.929 +/- 5.127 | 0.771 +/- 0.095 | 3.595 +/- 0.601 |
| head1m | 60 | 0 | 250 | E0 | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E1/measured3600 | 0.000 +/- 0.000 | -20.416 +/- 3.459 | -0.355 +/- 1.850 | 20.419 +/- 1.985 | 36.420 +/- 5.808 | 0.521 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E1/measured600 | 0.000 +/- 0.000 | -20.759 +/- 4.796 | -1.318 +/- 2.059 | 20.801 +/- 2.793 | 37.395 +/- 6.885 | 0.500 +/- 0.165 | - |
| head1m | 60 | 0 | 250 | E1/model | 0.000 +/- 0.000 | -20.183 +/- 3.616 | -0.312 +/- 0.547 | 20.185 +/- 2.083 | 36.363 +/- 4.204 | 0.521 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E1/oracleShape | 0.000 +/- 0.000 | -20.423 +/- 3.444 | -0.283 +/- 1.906 | 20.425 +/- 1.978 | 36.324 +/- 5.729 | 0.521 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E1/scale0.5 | 0.000 +/- 0.000 | -20.183 +/- 3.616 | -0.312 +/- 0.547 | 20.185 +/- 2.083 | 36.363 +/- 4.204 | 0.521 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E1/scale0.75 | 0.000 +/- 0.000 | -20.183 +/- 3.616 | -0.312 +/- 0.547 | 20.185 +/- 2.083 | 36.363 +/- 4.204 | 0.521 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E1/scale0.9 | 0.000 +/- 0.000 | -20.183 +/- 3.616 | -0.312 +/- 0.547 | 20.185 +/- 2.083 | 36.363 +/- 4.204 | 0.521 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E1/scale1.1 | 0.000 +/- 0.000 | -20.183 +/- 3.616 | -0.312 +/- 0.547 | 20.185 +/- 2.083 | 36.363 +/- 4.204 | 0.521 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E1/scale1.25 | 0.000 +/- 0.000 | -20.183 +/- 3.616 | -0.312 +/- 0.547 | 20.185 +/- 2.083 | 36.363 +/- 4.204 | 0.521 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E1/scale1.5 | 0.000 +/- 0.000 | -20.183 +/- 3.616 | -0.312 +/- 0.547 | 20.185 +/- 2.083 | 36.363 +/- 4.204 | 0.521 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E1/wrongFront | 0.000 +/- 0.000 | -4.464 +/- 6.521 | -2.460 +/- 0.875 | 5.097 +/- 3.521 | 25.629 +/- 18.318 | 0.938 +/- 0.062 | - |
| head1m | 60 | 0 | 250 | E2/measured3600 | 0.000 +/- 0.000 | -22.523 +/- 3.336 | -3.977 +/- 3.096 | 22.872 +/- 1.693 | 39.271 +/- 2.648 | 0.458 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E2/measured600 | 0.000 +/- 0.000 | -20.369 +/- 4.961 | -3.259 +/- 1.266 | 20.628 +/- 2.822 | 37.594 +/- 7.415 | 0.500 +/- 0.165 | - |
| head1m | 60 | 0 | 250 | E2/model | 0.000 +/- 0.000 | -22.247 +/- 3.424 | -2.762 +/- 1.935 | 22.418 +/- 1.825 | 38.740 +/- 3.567 | 0.458 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E2/oracleShape | 0.000 +/- 0.000 | -22.247 +/- 3.424 | -2.762 +/- 1.935 | 22.418 +/- 1.825 | 38.740 +/- 3.567 | 0.458 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E2/scale0.5 | 0.000 +/- 0.000 | -22.247 +/- 3.424 | -2.762 +/- 1.935 | 22.418 +/- 1.825 | 38.740 +/- 3.567 | 0.458 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E2/scale0.75 | 0.000 +/- 0.000 | -22.247 +/- 3.424 | -2.762 +/- 1.935 | 22.418 +/- 1.825 | 38.740 +/- 3.567 | 0.458 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E2/scale0.9 | 0.000 +/- 0.000 | -22.247 +/- 3.424 | -2.762 +/- 1.935 | 22.418 +/- 1.825 | 38.740 +/- 3.567 | 0.458 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E2/scale1.1 | 0.000 +/- 0.000 | -22.247 +/- 3.424 | -2.762 +/- 1.935 | 22.418 +/- 1.825 | 38.740 +/- 3.567 | 0.458 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E2/scale1.25 | 0.000 +/- 0.000 | -22.247 +/- 3.424 | -2.762 +/- 1.935 | 22.418 +/- 1.825 | 38.740 +/- 3.567 | 0.458 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E2/scale1.5 | 0.000 +/- 0.000 | -22.247 +/- 3.424 | -2.762 +/- 1.935 | 22.418 +/- 1.825 | 38.740 +/- 3.567 | 0.458 +/- 0.130 | - |
| head1m | 60 | 0 | 250 | E2/wrongFront | 0.000 +/- 0.000 | -4.736 +/- 6.508 | -2.762 +/- 1.013 | 5.483 +/- 3.528 | 25.735 +/- 18.228 | 0.938 +/- 0.062 | - |
| head1m | 60 | 0 | 250 | E3/model | 0.000 +/- 0.000 | -16.384 +/- 4.492 | -0.191 +/- 1.535 | 16.386 +/- 2.584 | 47.648 +/- 5.272 | 0.833 +/- 0.036 | 11.882 +/- 0.732 |
| head1m | 60 | 0 | 250 | E4/measured3600 | 0.000 +/- 0.000 | 2.624 +/- 1.767 | -1.914 +/- 2.027 | 3.248 +/- 0.282 | 8.835 +/- 7.366 | 0.979 +/- 0.036 | 0.000 +/- 0.000 |
| head1m | 60 | 0 | 250 | E4/measured600 | 0.000 +/- 0.000 | 2.624 +/- 1.767 | -1.913 +/- 2.027 | 3.247 +/- 0.282 | 8.834 +/- 7.365 | 0.979 +/- 0.036 | 0.001 +/- 0.001 |
| head1m | 60 | 0 | 250 | E4/model | 0.000 +/- 0.000 | 2.624 +/- 1.767 | -1.913 +/- 2.026 | 3.247 +/- 0.283 | 8.836 +/- 7.365 | 0.979 +/- 0.036 | 0.001 +/- 0.000 |
| head1m | 60 | 0 | 250 | E4/oracleShape | 0.000 +/- 0.000 | 2.624 +/- 1.767 | -1.913 +/- 2.026 | 3.247 +/- 0.282 | 8.835 +/- 7.365 | 0.979 +/- 0.036 | 0.000 +/- 0.000 |
| head1m | 60 | 0 | 250 | E4/scale0.5 | 0.000 +/- 0.000 | 2.624 +/- 1.767 | -1.913 +/- 2.026 | 3.247 +/- 0.283 | 8.836 +/- 7.365 | 0.979 +/- 0.036 | 0.001 +/- 0.000 |
| head1m | 60 | 0 | 250 | E4/scale0.75 | 0.000 +/- 0.000 | 2.624 +/- 1.767 | -1.913 +/- 2.026 | 3.247 +/- 0.283 | 8.836 +/- 7.365 | 0.979 +/- 0.036 | 0.001 +/- 0.000 |
| head1m | 60 | 0 | 250 | E4/scale0.9 | 0.000 +/- 0.000 | 2.624 +/- 1.767 | -1.913 +/- 2.026 | 3.247 +/- 0.283 | 8.836 +/- 7.365 | 0.979 +/- 0.036 | 0.001 +/- 0.000 |
| head1m | 60 | 0 | 250 | E4/scale1.1 | 0.000 +/- 0.000 | 2.624 +/- 1.767 | -1.913 +/- 2.026 | 3.247 +/- 0.283 | 8.836 +/- 7.365 | 0.979 +/- 0.036 | 0.001 +/- 0.000 |
| head1m | 60 | 0 | 250 | E4/scale1.25 | 0.000 +/- 0.000 | 2.624 +/- 1.767 | -1.913 +/- 2.026 | 3.247 +/- 0.283 | 8.836 +/- 7.365 | 0.979 +/- 0.036 | 0.001 +/- 0.000 |
| head1m | 60 | 0 | 250 | E4/scale1.5 | 0.000 +/- 0.000 | 2.624 +/- 1.767 | -1.913 +/- 2.026 | 3.247 +/- 0.283 | 8.836 +/- 7.365 | 0.979 +/- 0.036 | 0.001 +/- 0.000 |
| head1m | 60 | 0 | 250 | E4/wrongFront | 0.000 +/- 0.000 | 2.536 +/- 1.673 | -1.962 +/- 2.020 | 3.206 +/- 0.236 | 8.942 +/- 7.275 | 0.979 +/- 0.036 | 6.397 +/- 2.357 |
| head1m | 60 | 0 | 250 | E4@240/model | 0.000 +/- 0.000 | 1.241 +/- 1.728 | 0.343 +/- 5.138 | 1.287 +/- 0.986 | 14.762 +/- 9.422 | 0.938 +/- 0.062 | 0.000 +/- 0.000 |
| head1m | 60 | 0 | 250 | E4@400/model | 0.000 +/- 0.000 | 1.304 +/- 1.649 | 0.323 +/- 5.131 | 1.343 +/- 0.941 | 14.773 +/- 9.388 | 0.938 +/- 0.062 | 0.000 +/- 0.000 |
| head1m | 60 | 0 | 250 | E4@60/model | 0.000 +/- 0.000 | 3.466 +/- 0.303 | -0.759 +/- 0.160 | 3.548 +/- 0.183 | 4.336 +/- 0.337 | 1.000 +/- 0.000 | 0.145 +/- 0.031 |
| head1m | 60 | 0 | 250 | E5/measured3600 | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E5/measured600 | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E5/model | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E5/oracleShape | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E5/scale0.5 | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E5/scale0.75 | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E5/scale0.9 | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E5/scale1.1 | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E5/scale1.25 | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E5/scale1.5 | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E5/wrongFront | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E5@240/model | 0.000 +/- 0.000 | 1.244 +/- 1.725 | 0.342 +/- 5.140 | 1.290 +/- 0.984 | 14.763 +/- 9.419 | 0.938 +/- 0.062 | - |
| head1m | 60 | 0 | 250 | E5@400/model | 0.000 +/- 0.000 | 1.308 +/- 1.645 | 0.322 +/- 5.132 | 1.347 +/- 0.937 | 14.775 +/- 9.387 | 0.938 +/- 0.062 | - |
| head1m | 60 | 0 | 250 | E5@60/model | 0.000 +/- 0.000 | 3.482 +/- 0.305 | -0.717 +/- 0.164 | 3.555 +/- 0.183 | 4.359 +/- 0.347 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0 | 250 | E6/measured3600 | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E6/measured600 | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E6/model | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E6/oracleShape | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E6/scale0.5 | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E6/scale0.75 | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E6/scale0.9 | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E6/scale1.1 | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E6/scale1.25 | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E6/scale1.5 | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E6/wrongFront | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 60 | 0 | 250 | E7/model | 0.000 +/- 0.000 | 5.024 +/- 0.281 | -0.996 +/- 0.676 | 5.122 +/- 0.227 | 6.788 +/- 0.654 | 1.000 +/- 0.000 | 11.882 +/- 0.732 |
| head1m | 60 | 0 | 1000 | E0 | 0.000 +/- 0.000 | 2.597 +/- 4.239 | -0.052 +/- 0.211 | 2.598 +/- 2.449 | 13.372 +/- 13.969 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | -2.939 +/- 1.765 | -2.997 +/- 1.090 | 4.198 +/- 0.264 | 7.510 +/- 5.865 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | -5.786 +/- 3.357 | -2.707 +/- 1.906 | 6.387 +/- 1.713 | 17.365 +/- 12.511 | 0.896 +/- 0.095 | - |
| head1m | 60 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | 2.605 +/- 4.242 | -0.436 +/- 0.460 | 2.642 +/- 2.446 | 13.482 +/- 13.876 | 0.979 +/- 0.036 | 48.460 +/- 1.679 |
| head1m | 60 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 3.893 +/- 0.135 | -0.191 +/- 0.184 | 3.897 +/- 0.076 | 4.018 +/- 0.142 | 1.000 +/- 0.000 | 0.002 +/- 0.002 |
| head1m | 60 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 3.915 +/- 0.162 | -0.203 +/- 0.168 | 3.920 +/- 0.091 | 4.041 +/- 0.168 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | 2.597 +/- 4.239 | -0.052 +/- 0.211 | 2.598 +/- 2.449 | 13.372 +/- 13.969 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 5.867 +/- 0.383 | -0.653 +/- 0.133 | 5.903 +/- 0.212 | 6.283 +/- 0.206 | 1.000 +/- 0.000 | 48.460 +/- 1.679 |
| head1m | 60 | 0.05 | 25 | E0 | 1005.933 +/- 0.468 | -69.148 +/- 9.501 | 14.992 +/- 0.409 | 70.754 +/- 5.320 | 81.346 +/- 7.797 | 0.146 +/- 0.036 | - |
| head1m | 60 | 0.05 | 25 | E1/model | 1005.933 +/- 0.468 | -59.427 +/- 9.501 | -5.041 +/- 4.512 | 59.640 +/- 5.501 | 77.796 +/- 10.152 | 0.062 +/- 0.000 | - |
| head1m | 60 | 0.05 | 25 | E2/model | 1005.933 +/- 0.468 | -59.921 +/- 10.131 | -4.033 +/- 3.895 | 60.056 +/- 5.920 | 78.013 +/- 10.680 | 0.062 +/- 0.000 | - |
| head1m | 60 | 0.05 | 25 | E3/model | 1005.933 +/- 0.468 | -58.668 +/- 6.552 | -2.906 +/- 5.524 | 58.740 +/- 3.622 | 75.754 +/- 5.209 | 0.125 +/- 0.000 | 924.504 +/- 10.328 |
| head1m | 60 | 0.05 | 25 | E4/model | 1005.933 +/- 0.468 | -47.274 +/- 16.682 | -2.955 +/- 0.089 | 47.367 +/- 9.610 | 68.716 +/- 13.826 | 0.083 +/- 0.072 | 948.250 +/- 19.557 |
| head1m | 60 | 0.05 | 25 | E5/model | 1005.933 +/- 0.468 | -49.540 +/- 16.394 | -1.418 +/- 2.329 | 49.561 +/- 9.459 | 71.657 +/- 13.371 | 0.042 +/- 0.036 | - |
| head1m | 60 | 0.05 | 25 | E6/model | 1005.933 +/- 0.468 | -57.396 +/- 12.163 | -3.666 +/- 3.741 | 57.513 +/- 7.082 | 76.030 +/- 12.001 | 0.062 +/- 0.000 | - |
| head1m | 60 | 0.05 | 25 | E7/model | 1005.933 +/- 0.468 | -49.487 +/- 15.852 | -2.630 +/- 5.730 | 49.557 +/- 9.113 | 71.384 +/- 12.449 | 0.042 +/- 0.036 | 924.504 +/- 10.328 |
| head1m | 60 | 0.05 | 100 | E0 | 1005.933 +/- 0.468 | -66.130 +/- 4.820 | 14.834 +/- 0.394 | 67.773 +/- 2.682 | 77.885 +/- 0.932 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.05 | 100 | E1/model | 1005.933 +/- 0.468 | -46.642 +/- 10.409 | -4.740 +/- 7.289 | 46.882 +/- 5.557 | 68.923 +/- 5.253 | 0.083 +/- 0.095 | - |
| head1m | 60 | 0.05 | 100 | E2/model | 1005.933 +/- 0.468 | -46.718 +/- 10.303 | -7.623 +/- 10.374 | 47.336 +/- 5.044 | 68.974 +/- 5.312 | 0.083 +/- 0.095 | - |
| head1m | 60 | 0.05 | 100 | E3/model | 1005.933 +/- 0.468 | -53.750 +/- 8.724 | 0.490 +/- 5.198 | 53.753 +/- 5.059 | 74.386 +/- 5.827 | 0.125 +/- 0.062 | 939.401 +/- 8.022 |
| head1m | 60 | 0.05 | 100 | E4/model | 1005.933 +/- 0.468 | -41.646 +/- 4.081 | -2.668 +/- 3.969 | 41.731 +/- 2.218 | 65.108 +/- 4.230 | 0.250 +/- 0.165 | 915.726 +/- 13.620 |
| head1m | 60 | 0.05 | 100 | E5/model | 1005.933 +/- 0.468 | -43.975 +/- 6.862 | 1.917 +/- 6.813 | 44.016 +/- 4.127 | 67.792 +/- 5.701 | 0.229 +/- 0.180 | - |
| head1m | 60 | 0.05 | 100 | E6/model | 1005.933 +/- 0.468 | -50.264 +/- 13.897 | -8.942 +/- 3.708 | 51.053 +/- 7.551 | 74.193 +/- 8.848 | 0.146 +/- 0.095 | - |
| head1m | 60 | 0.05 | 100 | E7/model | 1005.933 +/- 0.468 | -45.007 +/- 6.590 | -1.100 +/- 5.467 | 45.020 +/- 3.728 | 68.885 +/- 5.012 | 0.167 +/- 0.072 | 939.401 +/- 8.022 |
| head1m | 60 | 0.05 | 250 | E0 | 1005.933 +/- 0.468 | -39.054 +/- 8.112 | 13.754 +/- 1.475 | 41.405 +/- 4.699 | 59.922 +/- 7.273 | 0.500 +/- 0.062 | - |
| head1m | 60 | 0.05 | 250 | E1/model | 1005.933 +/- 0.468 | -37.480 +/- 3.692 | -2.742 +/- 4.126 | 37.580 +/- 2.248 | 57.506 +/- 1.831 | 0.208 +/- 0.036 | - |
| head1m | 60 | 0.05 | 250 | E2/model | 1005.933 +/- 0.468 | -36.886 +/- 3.609 | -2.210 +/- 6.878 | 36.952 +/- 2.254 | 56.980 +/- 3.510 | 0.208 +/- 0.036 | - |
| head1m | 60 | 0.05 | 250 | E3/model | 1005.933 +/- 0.468 | -35.242 +/- 12.428 | 1.516 +/- 3.876 | 35.274 +/- 7.073 | 61.379 +/- 11.007 | 0.521 +/- 0.095 | 949.208 +/- 7.443 |
| head1m | 60 | 0.05 | 250 | E4/model | 1005.933 +/- 0.468 | -22.582 +/- 4.759 | -1.725 +/- 4.463 | 22.648 +/- 2.573 | 52.603 +/- 3.709 | 0.667 +/- 0.130 | 873.842 +/- 6.824 |
| head1m | 60 | 0.05 | 250 | E5/model | 1005.933 +/- 0.468 | -22.670 +/- 4.838 | -2.653 +/- 3.807 | 22.825 +/- 2.534 | 52.620 +/- 3.765 | 0.667 +/- 0.130 | - |
| head1m | 60 | 0.05 | 250 | E6/model | 1005.933 +/- 0.468 | -34.419 +/- 6.838 | 0.780 +/- 3.697 | 34.428 +/- 3.910 | 59.830 +/- 7.766 | 0.479 +/- 0.095 | - |
| head1m | 60 | 0.05 | 250 | E7/model | 1005.933 +/- 0.468 | -23.065 +/- 11.787 | 1.301 +/- 9.551 | 23.101 +/- 7.059 | 53.285 +/- 11.217 | 0.604 +/- 0.157 | 949.208 +/- 7.443 |
| head1m | 60 | 0.05 | 1000 | E0 | 1005.933 +/- 0.468 | -7.059 +/- 3.834 | 4.634 +/- 0.051 | 8.445 +/- 1.837 | 34.001 +/- 6.714 | 0.896 +/- 0.036 | - |
| head1m | 60 | 0.05 | 1000 | E1/model | 1005.933 +/- 0.468 | -11.879 +/- 2.107 | -1.501 +/- 4.974 | 11.973 +/- 1.110 | 25.155 +/- 1.641 | 0.708 +/- 0.072 | - |
| head1m | 60 | 0.05 | 1000 | E2/model | 1005.933 +/- 0.468 | -14.292 +/- 1.730 | 0.276 +/- 5.994 | 14.295 +/- 1.035 | 27.713 +/- 2.546 | 0.646 +/- 0.072 | - |
| head1m | 60 | 0.05 | 1000 | E3/model | 1005.933 +/- 0.468 | -7.864 +/- 4.544 | -0.103 +/- 0.764 | 7.864 +/- 2.617 | 37.405 +/- 6.688 | 0.896 +/- 0.036 | 927.834 +/- 14.957 |
| head1m | 60 | 0.05 | 1000 | E4/model | 1005.933 +/- 0.468 | 1.392 +/- 4.280 | -0.139 +/- 0.121 | 1.399 +/- 2.465 | 12.184 +/- 14.123 | 0.979 +/- 0.036 | 631.636 +/- 15.601 |
| head1m | 60 | 0.05 | 1000 | E5/model | 1005.933 +/- 0.468 | 1.385 +/- 4.343 | -0.402 +/- 0.109 | 1.442 +/- 2.425 | 12.302 +/- 14.217 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0.05 | 1000 | E6/model | 1005.933 +/- 0.468 | -7.759 +/- 4.270 | -0.404 +/- 0.683 | 7.769 +/- 2.442 | 37.403 +/- 6.683 | 0.896 +/- 0.036 | - |
| head1m | 60 | 0.05 | 1000 | E7/model | 1005.933 +/- 0.468 | 0.283 +/- 5.012 | -0.522 +/- 0.163 | 0.594 +/- 1.410 | 21.836 +/- 13.278 | 0.958 +/- 0.036 | 927.834 +/- 14.957 |
| head1m | 60 | 0.1 | 25 | E0 | 2011.866 +/- 0.936 | -65.537 +/- 14.320 | 15.207 +/- 0.057 | 67.278 +/- 8.047 | 77.933 +/- 8.915 | 0.167 +/- 0.130 | - |
| head1m | 60 | 0.1 | 25 | E1/model | 2011.866 +/- 0.936 | -54.119 +/- 7.033 | 4.665 +/- 5.124 | 54.320 +/- 4.300 | 75.973 +/- 6.024 | 0.021 +/- 0.036 | - |
| head1m | 60 | 0.1 | 25 | E2/model | 2011.866 +/- 0.936 | -54.120 +/- 6.886 | 4.585 +/- 4.986 | 54.314 +/- 4.204 | 75.827 +/- 5.921 | 0.021 +/- 0.036 | - |
| head1m | 60 | 0.1 | 25 | E3/model | 2011.866 +/- 0.936 | -53.868 +/- 3.436 | 6.350 +/- 8.519 | 54.240 +/- 2.362 | 74.192 +/- 3.965 | 0.104 +/- 0.036 | 1847.158 +/- 7.279 |
| head1m | 60 | 0.1 | 25 | E4/model | 2011.866 +/- 0.936 | -48.024 +/- 7.522 | -2.522 +/- 6.117 | 48.090 +/- 4.368 | 69.977 +/- 2.879 | 0.062 +/- 0.062 | 1906.272 +/- 12.077 |
| head1m | 60 | 0.1 | 25 | E5/model | 2011.866 +/- 0.936 | -49.523 +/- 7.517 | 0.153 +/- 13.199 | 49.523 +/- 4.321 | 71.107 +/- 2.857 | 0.021 +/- 0.036 | - |
| head1m | 60 | 0.1 | 25 | E6/model | 2011.866 +/- 0.936 | -57.943 +/- 10.386 | 3.339 +/- 6.105 | 58.039 +/- 6.005 | 78.973 +/- 7.280 | 0.021 +/- 0.036 | - |
| head1m | 60 | 0.1 | 25 | E7/model | 2011.866 +/- 0.936 | -51.019 +/- 3.154 | 2.044 +/- 12.321 | 51.060 +/- 1.661 | 73.423 +/- 0.234 | 0.000 +/- 0.000 | 1847.158 +/- 7.279 |
| head1m | 60 | 0.1 | 100 | E0 | 2011.866 +/- 0.936 | -62.035 +/- 10.386 | 15.195 +/- 0.064 | 63.869 +/- 5.830 | 77.373 +/- 8.059 | 0.250 +/- 0.062 | - |
| head1m | 60 | 0.1 | 100 | E1/model | 2011.866 +/- 0.936 | -50.318 +/- 10.456 | -1.414 +/- 13.701 | 50.337 +/- 6.253 | 72.708 +/- 12.557 | 0.125 +/- 0.125 | - |
| head1m | 60 | 0.1 | 100 | E2/model | 2011.866 +/- 0.936 | -49.480 +/- 10.192 | -2.044 +/- 13.755 | 49.522 +/- 6.207 | 72.399 +/- 12.404 | 0.125 +/- 0.125 | - |
| head1m | 60 | 0.1 | 100 | E3/model | 2011.866 +/- 0.936 | -49.635 +/- 2.339 | 4.620 +/- 9.685 | 49.850 +/- 1.067 | 70.620 +/- 2.667 | 0.208 +/- 0.036 | 1873.610 +/- 8.600 |
| head1m | 60 | 0.1 | 100 | E4/model | 2011.866 +/- 0.936 | -46.542 +/- 10.751 | -3.062 +/- 5.308 | 46.642 +/- 6.020 | 69.897 +/- 8.784 | 0.167 +/- 0.072 | 1900.391 +/- 1.728 |
| head1m | 60 | 0.1 | 100 | E5/model | 2011.866 +/- 0.936 | -47.433 +/- 2.419 | -1.965 +/- 7.177 | 47.474 +/- 1.531 | 70.514 +/- 4.578 | 0.125 +/- 0.108 | - |
| head1m | 60 | 0.1 | 100 | E6/model | 2011.866 +/- 0.936 | -50.963 +/- 6.938 | -0.395 +/- 14.224 | 50.965 +/- 4.068 | 74.286 +/- 6.715 | 0.125 +/- 0.125 | - |
| head1m | 60 | 0.1 | 100 | E7/model | 2011.866 +/- 0.936 | -47.417 +/- 11.793 | -5.147 +/- 0.840 | 47.696 +/- 6.723 | 71.416 +/- 9.169 | 0.146 +/- 0.036 | 1873.610 +/- 8.600 |
| head1m | 60 | 0.1 | 250 | E0 | 2011.866 +/- 0.936 | -54.013 +/- 11.656 | 15.230 +/- 0.014 | 56.119 +/- 6.478 | 72.302 +/- 9.442 | 0.333 +/- 0.036 | - |
| head1m | 60 | 0.1 | 250 | E1/measured3600 | 2011.866 +/- 0.936 | -44.045 +/- 3.842 | -4.597 +/- 12.412 | 44.284 +/- 1.840 | 66.272 +/- 0.115 | 0.146 +/- 0.072 | - |
| head1m | 60 | 0.1 | 250 | E1/measured600 | 2011.866 +/- 0.936 | -50.144 +/- 3.729 | -0.572 +/- 14.141 | 50.147 +/- 2.133 | 70.957 +/- 2.358 | 0.083 +/- 0.036 | - |
| head1m | 60 | 0.1 | 250 | E1/model | 2011.866 +/- 0.936 | -46.338 +/- 2.888 | 5.741 +/- 10.736 | 46.692 +/- 2.227 | 68.738 +/- 0.467 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E1/oracleShape | 2011.866 +/- 0.936 | -44.499 +/- 6.038 | 0.202 +/- 11.308 | 44.500 +/- 3.484 | 66.507 +/- 2.469 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E1/scale0.5 | 2011.866 +/- 0.936 | -46.338 +/- 2.888 | 5.741 +/- 10.736 | 46.692 +/- 2.227 | 68.738 +/- 0.467 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E1/scale0.75 | 2011.866 +/- 0.936 | -46.338 +/- 2.888 | 5.741 +/- 10.736 | 46.692 +/- 2.227 | 68.738 +/- 0.467 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E1/scale0.9 | 2011.866 +/- 0.936 | -46.338 +/- 2.888 | 5.741 +/- 10.736 | 46.692 +/- 2.227 | 68.738 +/- 0.467 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E1/scale1.1 | 2011.866 +/- 0.936 | -46.338 +/- 2.888 | 5.741 +/- 10.736 | 46.692 +/- 2.227 | 68.738 +/- 0.467 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E1/scale1.25 | 2011.866 +/- 0.936 | -46.338 +/- 2.888 | 5.741 +/- 10.736 | 46.692 +/- 2.227 | 68.738 +/- 0.467 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E1/scale1.5 | 2011.866 +/- 0.936 | -46.338 +/- 2.888 | 5.741 +/- 10.736 | 46.692 +/- 2.227 | 68.738 +/- 0.467 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E1/wrongFront | 2011.866 +/- 0.936 | -6.114 +/- 8.365 | 6.168 +/- 0.793 | 8.685 +/- 3.676 | 22.477 +/- 17.466 | 0.833 +/- 0.072 | - |
| head1m | 60 | 0.1 | 250 | E2/measured3600 | 2011.866 +/- 0.936 | -44.730 +/- 4.787 | -3.149 +/- 14.063 | 44.840 +/- 2.400 | 66.863 +/- 0.752 | 0.146 +/- 0.072 | - |
| head1m | 60 | 0.1 | 250 | E2/measured600 | 2011.866 +/- 0.936 | -50.033 +/- 4.161 | -2.872 +/- 12.595 | 50.115 +/- 2.377 | 70.776 +/- 2.451 | 0.083 +/- 0.036 | - |
| head1m | 60 | 0.1 | 250 | E2/model | 2011.866 +/- 0.936 | -45.834 +/- 6.058 | 4.474 +/- 7.946 | 46.052 +/- 3.801 | 67.301 +/- 4.180 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E2/oracleShape | 2011.866 +/- 0.936 | -45.613 +/- 5.372 | 0.829 +/- 10.406 | 45.621 +/- 3.151 | 68.154 +/- 1.672 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E2/scale0.5 | 2011.866 +/- 0.936 | -45.834 +/- 6.058 | 4.474 +/- 7.946 | 46.052 +/- 3.801 | 67.301 +/- 4.180 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E2/scale0.75 | 2011.866 +/- 0.936 | -45.834 +/- 6.058 | 4.474 +/- 7.946 | 46.052 +/- 3.801 | 67.301 +/- 4.180 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E2/scale0.9 | 2011.866 +/- 0.936 | -45.834 +/- 6.058 | 4.474 +/- 7.946 | 46.052 +/- 3.801 | 67.301 +/- 4.180 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E2/scale1.1 | 2011.866 +/- 0.936 | -45.834 +/- 6.058 | 4.474 +/- 7.946 | 46.052 +/- 3.801 | 67.301 +/- 4.180 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E2/scale1.25 | 2011.866 +/- 0.936 | -45.834 +/- 6.058 | 4.474 +/- 7.946 | 46.052 +/- 3.801 | 67.301 +/- 4.180 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E2/scale1.5 | 2011.866 +/- 0.936 | -45.834 +/- 6.058 | 4.474 +/- 7.946 | 46.052 +/- 3.801 | 67.301 +/- 4.180 | 0.167 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E2/wrongFront | 2011.866 +/- 0.936 | -5.896 +/- 8.420 | 5.745 +/- 0.913 | 8.232 +/- 3.795 | 22.415 +/- 17.593 | 0.833 +/- 0.072 | - |
| head1m | 60 | 0.1 | 250 | E3/model | 2011.866 +/- 0.936 | -49.709 +/- 3.852 | 7.113 +/- 8.921 | 50.215 +/- 2.931 | 73.198 +/- 3.380 | 0.333 +/- 0.072 | 1912.135 +/- 19.218 |
| head1m | 60 | 0.1 | 250 | E4/measured3600 | 2011.866 +/- 0.936 | -26.901 +/- 11.201 | 0.439 +/- 3.622 | 26.904 +/- 6.479 | 57.020 +/- 10.823 | 0.562 +/- 0.125 | 1871.603 +/- 21.354 |
| head1m | 60 | 0.1 | 250 | E4/measured600 | 2011.866 +/- 0.936 | -28.873 +/- 12.755 | 4.293 +/- 5.760 | 29.191 +/- 7.565 | 58.303 +/- 10.402 | 0.542 +/- 0.072 | 1804.226 +/- 17.315 |
| head1m | 60 | 0.1 | 250 | E4/model | 2011.866 +/- 0.936 | -30.965 +/- 10.313 | 0.910 +/- 5.236 | 30.978 +/- 5.962 | 61.284 +/- 9.113 | 0.521 +/- 0.095 | 1877.743 +/- 23.765 |
| head1m | 60 | 0.1 | 250 | E4/oracleShape | 2011.866 +/- 0.936 | -29.409 +/- 13.058 | 2.539 +/- 7.286 | 29.518 +/- 7.606 | 59.521 +/- 10.692 | 0.542 +/- 0.072 | 1883.231 +/- 27.150 |
| head1m | 60 | 0.1 | 250 | E4/scale0.5 | 2011.866 +/- 0.936 | -30.965 +/- 10.313 | 0.910 +/- 5.236 | 30.978 +/- 5.962 | 61.284 +/- 9.113 | 0.521 +/- 0.095 | 1877.743 +/- 23.765 |
| head1m | 60 | 0.1 | 250 | E4/scale0.75 | 2011.866 +/- 0.936 | -30.965 +/- 10.313 | 0.910 +/- 5.236 | 30.978 +/- 5.962 | 61.284 +/- 9.113 | 0.521 +/- 0.095 | 1877.743 +/- 23.765 |
| head1m | 60 | 0.1 | 250 | E4/scale0.9 | 2011.866 +/- 0.936 | -30.965 +/- 10.313 | 0.910 +/- 5.236 | 30.978 +/- 5.962 | 61.284 +/- 9.113 | 0.521 +/- 0.095 | 1877.743 +/- 23.765 |
| head1m | 60 | 0.1 | 250 | E4/scale1.1 | 2011.866 +/- 0.936 | -30.965 +/- 10.313 | 0.910 +/- 5.236 | 30.978 +/- 5.962 | 61.284 +/- 9.113 | 0.521 +/- 0.095 | 1877.743 +/- 23.765 |
| head1m | 60 | 0.1 | 250 | E4/scale1.25 | 2011.866 +/- 0.936 | -30.965 +/- 10.313 | 0.910 +/- 5.236 | 30.978 +/- 5.962 | 61.284 +/- 9.113 | 0.521 +/- 0.095 | 1877.743 +/- 23.765 |
| head1m | 60 | 0.1 | 250 | E4/scale1.5 | 2011.866 +/- 0.936 | -30.965 +/- 10.313 | 0.910 +/- 5.236 | 30.978 +/- 5.962 | 61.284 +/- 9.113 | 0.521 +/- 0.095 | 1877.743 +/- 23.765 |
| head1m | 60 | 0.1 | 250 | E4/wrongFront | 2011.866 +/- 0.936 | -46.581 +/- 1.604 | 8.629 +/- 0.411 | 47.374 +/- 0.953 | 62.168 +/- 2.414 | 0.271 +/- 0.095 | 0.008 +/- 0.003 |
| head1m | 60 | 0.1 | 250 | E4@240/model | 2011.866 +/- 0.936 | -33.221 +/- 6.369 | 3.478 +/- 7.278 | 33.402 +/- 3.683 | 61.929 +/- 6.020 | 0.521 +/- 0.130 | 1846.023 +/- 25.434 |
| head1m | 60 | 0.1 | 250 | E4@400/model | 2011.866 +/- 0.936 | -31.331 +/- 6.439 | 5.616 +/- 8.378 | 31.830 +/- 4.325 | 58.724 +/- 9.382 | 0.458 +/- 0.130 | 1837.694 +/- 25.021 |
| head1m | 60 | 0.1 | 250 | E4@60/model | 2011.866 +/- 0.936 | -30.503 +/- 10.065 | 3.054 +/- 4.029 | 30.656 +/- 5.903 | 59.967 +/- 8.203 | 0.542 +/- 0.095 | 1896.388 +/- 19.834 |
| head1m | 60 | 0.1 | 250 | E5/measured3600 | 2011.866 +/- 0.936 | -24.487 +/- 17.647 | 1.977 +/- 2.979 | 24.567 +/- 10.102 | 55.119 +/- 15.298 | 0.542 +/- 0.130 | - |
| head1m | 60 | 0.1 | 250 | E5/measured600 | 2011.866 +/- 0.936 | -29.382 +/- 18.063 | 0.832 +/- 4.912 | 29.393 +/- 10.421 | 58.709 +/- 16.101 | 0.500 +/- 0.165 | - |
| head1m | 60 | 0.1 | 250 | E5/model | 2011.866 +/- 0.936 | -29.745 +/- 13.836 | 0.490 +/- 5.743 | 29.749 +/- 7.988 | 59.767 +/- 10.075 | 0.479 +/- 0.130 | - |
| head1m | 60 | 0.1 | 250 | E5/oracleShape | 2011.866 +/- 0.936 | -29.419 +/- 16.753 | 2.984 +/- 3.536 | 29.570 +/- 9.610 | 59.375 +/- 14.313 | 0.479 +/- 0.130 | - |
| head1m | 60 | 0.1 | 250 | E5/scale0.5 | 2011.866 +/- 0.936 | -25.326 +/- 6.905 | 6.706 +/- 0.634 | 26.199 +/- 3.800 | 51.321 +/- 8.423 | 0.667 +/- 0.072 | - |
| head1m | 60 | 0.1 | 250 | E5/scale0.75 | 2011.866 +/- 0.936 | -28.199 +/- 4.077 | 2.624 +/- 1.741 | 28.320 +/- 2.437 | 56.699 +/- 3.553 | 0.583 +/- 0.191 | - |
| head1m | 60 | 0.1 | 250 | E5/scale0.9 | 2011.866 +/- 0.936 | -29.206 +/- 15.235 | -0.597 +/- 3.690 | 29.213 +/- 8.818 | 58.545 +/- 13.789 | 0.542 +/- 0.072 | - |
| head1m | 60 | 0.1 | 250 | E5/scale1.1 | 2011.866 +/- 0.936 | -29.878 +/- 9.980 | -1.763 +/- 6.625 | 29.930 +/- 5.539 | 58.851 +/- 8.201 | 0.438 +/- 0.165 | - |
| head1m | 60 | 0.1 | 250 | E5/scale1.25 | 2011.866 +/- 0.936 | -34.281 +/- 8.625 | -2.657 +/- 7.684 | 34.384 +/- 4.678 | 61.787 +/- 8.518 | 0.333 +/- 0.157 | - |
| head1m | 60 | 0.1 | 250 | E5/scale1.5 | 2011.866 +/- 0.936 | -32.743 +/- 6.265 | -3.093 +/- 7.078 | 32.888 +/- 3.280 | 60.420 +/- 6.137 | 0.333 +/- 0.157 | - |
| head1m | 60 | 0.1 | 250 | E5/wrongFront | 2011.866 +/- 0.936 | -32.042 +/- 6.177 | 9.550 +/- 0.147 | 33.435 +/- 3.435 | 56.775 +/- 6.513 | 0.562 +/- 0.165 | - |
| head1m | 60 | 0.1 | 250 | E5@240/model | 2011.866 +/- 0.936 | -31.328 +/- 11.281 | 0.898 +/- 5.980 | 31.341 +/- 6.507 | 59.915 +/- 9.855 | 0.438 +/- 0.165 | - |
| head1m | 60 | 0.1 | 250 | E5@400/model | 2011.866 +/- 0.936 | -33.704 +/- 7.380 | 2.319 +/- 9.407 | 33.783 +/- 4.422 | 60.845 +/- 8.255 | 0.417 +/- 0.130 | - |
| head1m | 60 | 0.1 | 250 | E5@60/model | 2011.866 +/- 0.936 | -28.831 +/- 12.605 | -0.251 +/- 4.846 | 28.832 +/- 7.287 | 59.092 +/- 9.187 | 0.500 +/- 0.108 | - |
| head1m | 60 | 0.1 | 250 | E6/measured3600 | 2011.866 +/- 0.936 | -50.240 +/- 2.207 | 3.593 +/- 8.453 | 50.368 +/- 1.538 | 73.519 +/- 3.464 | 0.312 +/- 0.108 | - |
| head1m | 60 | 0.1 | 250 | E6/measured600 | 2011.866 +/- 0.936 | -43.186 +/- 7.534 | 7.250 +/- 4.659 | 43.790 +/- 4.055 | 66.485 +/- 6.673 | 0.292 +/- 0.036 | - |
| head1m | 60 | 0.1 | 250 | E6/model | 2011.866 +/- 0.936 | -51.981 +/- 3.544 | 7.785 +/- 9.054 | 52.561 +/- 1.949 | 75.031 +/- 3.078 | 0.271 +/- 0.095 | - |
| head1m | 60 | 0.1 | 250 | E6/oracleShape | 2011.866 +/- 0.936 | -48.135 +/- 2.303 | 6.477 +/- 8.009 | 48.569 +/- 1.370 | 72.865 +/- 2.660 | 0.292 +/- 0.072 | - |
| head1m | 60 | 0.1 | 250 | E6/scale0.5 | 2011.866 +/- 0.936 | -48.218 +/- 4.080 | 14.636 +/- 1.233 | 50.390 +/- 2.449 | 67.654 +/- 3.036 | 0.375 +/- 0.108 | - |
| head1m | 60 | 0.1 | 250 | E6/scale0.75 | 2011.866 +/- 0.936 | -42.255 +/- 7.821 | 11.327 +/- 0.373 | 43.747 +/- 4.410 | 63.210 +/- 4.726 | 0.417 +/- 0.191 | - |
| head1m | 60 | 0.1 | 250 | E6/scale0.9 | 2011.866 +/- 0.936 | -40.588 +/- 6.380 | 8.851 +/- 5.230 | 41.542 +/- 3.164 | 65.635 +/- 8.071 | 0.417 +/- 0.036 | - |
| head1m | 60 | 0.1 | 250 | E6/scale1.1 | 2011.866 +/- 0.936 | -47.203 +/- 5.035 | 2.496 +/- 13.948 | 47.269 +/- 3.082 | 69.947 +/- 4.112 | 0.188 +/- 0.062 | - |
| head1m | 60 | 0.1 | 250 | E6/scale1.25 | 2011.866 +/- 0.936 | -40.156 +/- 0.894 | -6.215 +/- 15.339 | 40.634 +/- 1.427 | 57.526 +/- 3.456 | 0.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 250 | E6/scale1.5 | 2011.866 +/- 0.936 | -39.259 +/- 0.733 | -9.377 +/- 14.953 | 40.363 +/- 2.028 | 57.788 +/- 3.112 | 0.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 250 | E6/wrongFront | 2011.866 +/- 0.936 | -5.789 +/- 4.378 | 6.630 +/- 0.896 | 8.802 +/- 1.988 | 23.936 +/- 11.544 | 0.854 +/- 0.036 | - |
| head1m | 60 | 0.1 | 250 | E7/model | 2011.866 +/- 0.936 | -25.561 +/- 12.437 | 3.259 +/- 4.054 | 25.768 +/- 7.360 | 57.806 +/- 9.267 | 0.521 +/- 0.144 | 1912.135 +/- 19.218 |
| head1m | 60 | 0.1 | 1000 | E0 | 2011.866 +/- 0.936 | -10.320 +/- 9.934 | 9.718 +/- 1.741 | 14.175 +/- 4.368 | 28.254 +/- 20.094 | 0.792 +/- 0.072 | - |
| head1m | 60 | 0.1 | 1000 | E1/model | 2011.866 +/- 0.936 | -13.364 +/- 8.782 | -1.735 +/- 4.540 | 13.476 +/- 5.197 | 28.149 +/- 12.553 | 0.646 +/- 0.237 | - |
| head1m | 60 | 0.1 | 1000 | E2/model | 2011.866 +/- 0.936 | -16.171 +/- 8.888 | -3.204 +/- 2.488 | 16.485 +/- 5.186 | 31.700 +/- 14.117 | 0.583 +/- 0.253 | - |
| head1m | 60 | 0.1 | 1000 | E3/model | 2011.866 +/- 0.936 | -0.828 +/- 5.374 | 0.356 +/- 0.938 | 0.901 +/- 3.026 | 21.819 +/- 13.466 | 0.958 +/- 0.036 | 1932.675 +/- 14.821 |
| head1m | 60 | 0.1 | 1000 | E4/model | 2011.866 +/- 0.936 | 3.642 +/- 0.165 | 0.189 +/- 0.271 | 3.647 +/- 0.091 | 3.833 +/- 0.181 | 1.000 +/- 0.000 | 1608.726 +/- 30.504 |
| head1m | 60 | 0.1 | 1000 | E5/model | 2011.866 +/- 0.936 | 3.622 +/- 0.159 | -0.063 +/- 0.288 | 3.623 +/- 0.094 | 3.801 +/- 0.173 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E6/model | 2011.866 +/- 0.936 | -0.792 +/- 5.128 | -0.287 +/- 0.820 | 0.843 +/- 2.634 | 21.866 +/- 13.395 | 0.958 +/- 0.036 | - |
| head1m | 60 | 0.1 | 1000 | E7/model | 2011.866 +/- 0.936 | 4.973 +/- 0.389 | -0.159 +/- 0.515 | 4.976 +/- 0.223 | 5.615 +/- 0.258 | 1.000 +/- 0.000 | 1932.675 +/- 14.821 |
| head1m | 60 | 0.2 | 25 | E0 | 4023.732 +/- 1.873 | -51.312 +/- 7.508 | 15.151 +/- 0.033 | 53.502 +/- 4.160 | 69.710 +/- 6.812 | 0.354 +/- 0.095 | - |
| head1m | 60 | 0.2 | 25 | E1/model | 4023.732 +/- 1.873 | -52.963 +/- 10.401 | 0.936 +/- 2.981 | 52.972 +/- 5.976 | 75.540 +/- 8.748 | 0.021 +/- 0.036 | - |
| head1m | 60 | 0.2 | 25 | E2/model | 4023.732 +/- 1.873 | -52.242 +/- 10.230 | 0.608 +/- 3.414 | 52.246 +/- 5.885 | 75.324 +/- 8.325 | 0.042 +/- 0.036 | - |
| head1m | 60 | 0.2 | 25 | E3/model | 4023.732 +/- 1.873 | -57.420 +/- 3.962 | 5.648 +/- 7.332 | 57.697 +/- 2.508 | 76.165 +/- 5.466 | 0.188 +/- 0.062 | 3746.383 +/- 20.807 |
| head1m | 60 | 0.2 | 25 | E4/model | 4023.732 +/- 1.873 | -57.010 +/- 9.092 | 4.453 +/- 6.334 | 57.184 +/- 5.311 | 75.840 +/- 7.541 | 0.042 +/- 0.036 | 3861.357 +/- 13.762 |
| head1m | 60 | 0.2 | 25 | E5/model | 4023.732 +/- 1.873 | -50.939 +/- 5.387 | 1.793 +/- 4.124 | 50.971 +/- 3.112 | 72.619 +/- 3.463 | 0.021 +/- 0.036 | - |
| head1m | 60 | 0.2 | 25 | E6/model | 4023.732 +/- 1.873 | -49.413 +/- 7.507 | -1.237 +/- 8.285 | 49.429 +/- 4.442 | 72.461 +/- 6.123 | 0.062 +/- 0.000 | - |
| head1m | 60 | 0.2 | 25 | E7/model | 4023.732 +/- 1.873 | -47.684 +/- 7.603 | 5.043 +/- 1.504 | 47.950 +/- 4.447 | 69.485 +/- 8.329 | 0.042 +/- 0.036 | 3746.383 +/- 20.807 |
| head1m | 60 | 0.2 | 100 | E0 | 4023.732 +/- 1.873 | -56.489 +/- 6.508 | 15.168 +/- 0.019 | 58.490 +/- 3.632 | 73.649 +/- 3.828 | 0.292 +/- 0.072 | - |
| head1m | 60 | 0.2 | 100 | E1/model | 4023.732 +/- 1.873 | -61.349 +/- 4.864 | -6.854 +/- 4.395 | 61.731 +/- 2.603 | 81.835 +/- 5.331 | 0.021 +/- 0.036 | - |
| head1m | 60 | 0.2 | 100 | E2/model | 4023.732 +/- 1.873 | -60.142 +/- 2.580 | -6.684 +/- 4.429 | 60.512 +/- 1.240 | 80.551 +/- 3.825 | 0.021 +/- 0.036 | - |
| head1m | 60 | 0.2 | 100 | E3/model | 4023.732 +/- 1.873 | -51.283 +/- 12.444 | 4.550 +/- 4.711 | 51.484 +/- 6.985 | 70.114 +/- 7.684 | 0.250 +/- 0.062 | 3771.922 +/- 14.763 |
| head1m | 60 | 0.2 | 100 | E4/model | 4023.732 +/- 1.873 | -53.967 +/- 9.277 | -0.840 +/- 9.514 | 53.974 +/- 5.334 | 75.903 +/- 5.956 | 0.188 +/- 0.062 | 3864.176 +/- 14.233 |
| head1m | 60 | 0.2 | 100 | E5/model | 4023.732 +/- 1.873 | -51.937 +/- 14.473 | -4.471 +/- 12.193 | 52.129 +/- 8.403 | 73.500 +/- 12.031 | 0.104 +/- 0.072 | - |
| head1m | 60 | 0.2 | 100 | E6/model | 4023.732 +/- 1.873 | -62.594 +/- 2.781 | -6.043 +/- 3.108 | 62.885 +/- 1.493 | 82.182 +/- 2.483 | 0.062 +/- 0.000 | - |
| head1m | 60 | 0.2 | 100 | E7/model | 4023.732 +/- 1.873 | -46.343 +/- 12.037 | -3.694 +/- 18.091 | 46.490 +/- 7.363 | 69.140 +/- 11.539 | 0.104 +/- 0.072 | 3771.922 +/- 14.763 |
| head1m | 60 | 0.2 | 250 | E0 | 4023.732 +/- 1.873 | -60.479 +/- 9.223 | 15.214 +/- 0.012 | 62.363 +/- 5.165 | 75.567 +/- 7.360 | 0.250 +/- 0.125 | - |
| head1m | 60 | 0.2 | 250 | E1/model | 4023.732 +/- 1.873 | -45.688 +/- 4.324 | 2.736 +/- 12.041 | 45.770 +/- 2.902 | 68.159 +/- 2.255 | 0.104 +/- 0.130 | - |
| head1m | 60 | 0.2 | 250 | E2/model | 4023.732 +/- 1.873 | -45.724 +/- 7.795 | 1.381 +/- 12.104 | 45.745 +/- 4.706 | 66.479 +/- 1.114 | 0.104 +/- 0.130 | - |
| head1m | 60 | 0.2 | 250 | E3/model | 4023.732 +/- 1.873 | -41.967 +/- 8.730 | 4.544 +/- 5.097 | 42.212 +/- 5.323 | 67.455 +/- 5.102 | 0.292 +/- 0.191 | 3806.403 +/- 38.293 |
| head1m | 60 | 0.2 | 250 | E4/model | 4023.732 +/- 1.873 | -44.858 +/- 16.240 | 5.631 +/- 9.693 | 45.210 +/- 9.981 | 68.900 +/- 10.722 | 0.354 +/- 0.157 | 3846.832 +/- 16.470 |
| head1m | 60 | 0.2 | 250 | E5/model | 4023.732 +/- 1.873 | -46.589 +/- 15.716 | 6.434 +/- 11.291 | 47.031 +/- 9.859 | 71.055 +/- 8.327 | 0.250 +/- 0.165 | - |
| head1m | 60 | 0.2 | 250 | E6/model | 4023.732 +/- 1.873 | -39.822 +/- 3.182 | 2.374 +/- 4.562 | 39.893 +/- 1.989 | 66.179 +/- 4.388 | 0.167 +/- 0.072 | - |
| head1m | 60 | 0.2 | 250 | E7/model | 4023.732 +/- 1.873 | -41.974 +/- 10.185 | 5.434 +/- 12.803 | 42.325 +/- 6.510 | 67.218 +/- 3.136 | 0.208 +/- 0.201 | 3806.403 +/- 38.293 |
| head1m | 60 | 0.2 | 1000 | E0 | 4023.732 +/- 1.873 | -34.467 +/- 17.099 | 14.559 +/- 0.663 | 37.416 +/- 9.226 | 55.460 +/- 16.150 | 0.562 +/- 0.165 | - |
| head1m | 60 | 0.2 | 1000 | E1/model | 4023.732 +/- 1.873 | -25.632 +/- 5.303 | -10.948 +/- 8.284 | 27.872 +/- 1.075 | 47.443 +/- 7.901 | 0.479 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E2/model | 4023.732 +/- 1.873 | -27.440 +/- 6.024 | -10.993 +/- 5.247 | 29.560 +/- 2.211 | 49.369 +/- 8.954 | 0.438 +/- 0.062 | - |
| head1m | 60 | 0.2 | 1000 | E3/model | 4023.732 +/- 1.873 | -22.860 +/- 20.339 | 1.416 +/- 3.357 | 22.903 +/- 11.608 | 50.714 +/- 25.036 | 0.771 +/- 0.157 | 3866.392 +/- 17.632 |
| head1m | 60 | 0.2 | 1000 | E4/model | 4023.732 +/- 1.873 | -1.419 +/- 8.147 | 0.281 +/- 0.901 | 1.446 +/- 4.514 | 15.985 +/- 21.084 | 0.958 +/- 0.072 | 3583.936 +/- 5.685 |
| head1m | 60 | 0.2 | 1000 | E5/model | 4023.732 +/- 1.873 | 1.054 +/- 3.816 | -0.053 +/- 0.909 | 1.055 +/- 2.174 | 11.947 +/- 14.238 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E6/model | 4023.732 +/- 1.873 | -24.897 +/- 23.693 | 0.032 +/- 3.368 | 24.897 +/- 13.677 | 52.345 +/- 27.072 | 0.750 +/- 0.188 | - |
| head1m | 60 | 0.2 | 1000 | E7/model | 4023.732 +/- 1.873 | -3.303 +/- 6.705 | -0.618 +/- 1.151 | 3.360 +/- 3.913 | 25.212 +/- 18.849 | 0.938 +/- 0.062 | 3866.392 +/- 17.632 |
| head5m | 60 | 0 | 25 | E0 | 0.000 +/- 0.000 | -261.123 +/- 22.911 | -15.764 +/- 52.978 | 261.598 +/- 14.449 | 383.746 +/- 21.476 | 0.229 +/- 0.157 | - |
| head5m | 60 | 0 | 25 | E1/model | 0.000 +/- 0.000 | -212.971 +/- 57.428 | -14.694 +/- 21.784 | 213.478 +/- 32.956 | 314.576 +/- 69.037 | 0.146 +/- 0.157 | - |
| head5m | 60 | 0 | 25 | E2/model | 0.000 +/- 0.000 | -215.915 +/- 55.289 | -18.782 +/- 21.163 | 216.730 +/- 31.724 | 315.821 +/- 67.767 | 0.146 +/- 0.157 | - |
| head5m | 60 | 0 | 25 | E3/model | 0.000 +/- 0.000 | -257.524 +/- 20.030 | 5.931 +/- 56.768 | 257.592 +/- 11.477 | 377.759 +/- 16.459 | 0.229 +/- 0.157 | 0.225 +/- 0.122 |
| head5m | 60 | 0 | 25 | E4/model | 0.000 +/- 0.000 | -179.193 +/- 49.692 | 61.316 +/- 43.102 | 189.393 +/- 30.982 | 298.033 +/- 37.939 | 0.333 +/- 0.260 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 25 | E5/model | 0.000 +/- 0.000 | -179.212 +/- 49.682 | 61.332 +/- 43.088 | 189.417 +/- 30.974 | 298.050 +/- 37.941 | 0.333 +/- 0.260 | - |
| head5m | 60 | 0 | 25 | E6/model | 0.000 +/- 0.000 | -261.123 +/- 22.911 | -15.764 +/- 52.978 | 261.598 +/- 14.449 | 383.746 +/- 21.476 | 0.229 +/- 0.157 | - |
| head5m | 60 | 0 | 25 | E7/model | 0.000 +/- 0.000 | -169.556 +/- 56.200 | 47.841 +/- 14.115 | 176.176 +/- 33.434 | 303.956 +/- 39.377 | 0.375 +/- 0.225 | 0.225 +/- 0.122 |
| head5m | 60 | 0 | 100 | E0 | 0.000 +/- 0.000 | -63.701 +/- 43.696 | -33.493 +/- 19.178 | 71.970 +/- 27.025 | 150.921 +/- 104.557 | 0.854 +/- 0.072 | - |
| head5m | 60 | 0 | 100 | E1/model | 0.000 +/- 0.000 | -88.450 +/- 18.964 | -52.787 +/- 36.916 | 103.004 +/- 10.154 | 192.156 +/- 27.266 | 0.688 +/- 0.000 | - |
| head5m | 60 | 0 | 100 | E2/model | 0.000 +/- 0.000 | -85.825 +/- 19.296 | -63.250 +/- 27.548 | 106.613 +/- 12.970 | 184.479 +/- 28.358 | 0.688 +/- 0.000 | - |
| head5m | 60 | 0 | 100 | E3/model | 0.000 +/- 0.000 | -68.424 +/- 43.691 | -26.246 +/- 27.988 | 73.285 +/- 26.920 | 155.062 +/- 105.660 | 0.854 +/- 0.072 | 1.540 +/- 0.785 |
| head5m | 60 | 0 | 100 | E4/model | 0.000 +/- 0.000 | -34.401 +/- 19.384 | -7.296 +/- 28.000 | 35.166 +/- 12.002 | 146.523 +/- 53.956 | 0.854 +/- 0.036 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 100 | E5/model | 0.000 +/- 0.000 | -34.430 +/- 19.356 | -7.275 +/- 28.007 | 35.190 +/- 11.984 | 146.547 +/- 53.910 | 0.854 +/- 0.036 | - |
| head5m | 60 | 0 | 100 | E6/model | 0.000 +/- 0.000 | -63.701 +/- 43.696 | -33.493 +/- 19.178 | 71.970 +/- 27.025 | 150.921 +/- 104.557 | 0.854 +/- 0.072 | - |
| head5m | 60 | 0 | 100 | E7/model | 0.000 +/- 0.000 | -9.376 +/- 22.473 | -23.705 +/- 17.883 | 25.492 +/- 12.776 | 123.550 +/- 72.995 | 0.917 +/- 0.095 | 1.540 +/- 0.785 |
| head5m | 60 | 0 | 250 | E0 | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E1/measured3600 | 0.000 +/- 0.000 | -49.324 +/- 12.641 | -35.170 +/- 20.499 | 60.579 +/- 4.580 | 128.376 +/- 34.888 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E1/measured600 | 0.000 +/- 0.000 | -44.921 +/- 5.083 | -41.048 +/- 15.041 | 60.851 +/- 5.324 | 121.012 +/- 22.800 | 0.896 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E1/model | 0.000 +/- 0.000 | -49.321 +/- 12.639 | -35.234 +/- 20.524 | 60.613 +/- 4.581 | 128.392 +/- 34.871 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E1/oracleShape | 0.000 +/- 0.000 | -49.316 +/- 12.644 | -35.236 +/- 20.507 | 60.611 +/- 4.591 | 128.384 +/- 34.893 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E1/scale0.5 | 0.000 +/- 0.000 | -49.321 +/- 12.639 | -35.234 +/- 20.524 | 60.613 +/- 4.581 | 128.392 +/- 34.871 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E1/scale0.75 | 0.000 +/- 0.000 | -49.321 +/- 12.639 | -35.234 +/- 20.524 | 60.613 +/- 4.581 | 128.392 +/- 34.871 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E1/scale0.9 | 0.000 +/- 0.000 | -49.321 +/- 12.639 | -35.234 +/- 20.524 | 60.613 +/- 4.581 | 128.392 +/- 34.871 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E1/scale1.1 | 0.000 +/- 0.000 | -49.321 +/- 12.639 | -35.234 +/- 20.524 | 60.613 +/- 4.581 | 128.392 +/- 34.871 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E1/scale1.25 | 0.000 +/- 0.000 | -49.321 +/- 12.639 | -35.234 +/- 20.524 | 60.613 +/- 4.581 | 128.392 +/- 34.871 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E1/scale1.5 | 0.000 +/- 0.000 | -49.321 +/- 12.639 | -35.234 +/- 20.524 | 60.613 +/- 4.581 | 128.392 +/- 34.871 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E1/wrongFront | 0.000 +/- 0.000 | -28.513 +/- 20.332 | -34.258 +/- 0.061 | 44.571 +/- 7.517 | 75.402 +/- 64.546 | 0.979 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E2/measured3600 | 0.000 +/- 0.000 | -55.167 +/- 12.684 | -39.497 +/- 20.454 | 67.848 +/- 4.642 | 130.836 +/- 34.661 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E2/measured600 | 0.000 +/- 0.000 | -55.167 +/- 12.684 | -45.297 +/- 14.876 | 71.380 +/- 6.325 | 127.323 +/- 28.841 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E2/model | 0.000 +/- 0.000 | -55.167 +/- 12.684 | -39.497 +/- 20.454 | 67.848 +/- 4.642 | 130.836 +/- 34.661 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E2/oracleShape | 0.000 +/- 0.000 | -55.167 +/- 12.684 | -39.497 +/- 20.454 | 67.848 +/- 4.642 | 130.836 +/- 34.661 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E2/scale0.5 | 0.000 +/- 0.000 | -55.167 +/- 12.684 | -39.497 +/- 20.454 | 67.848 +/- 4.642 | 130.836 +/- 34.661 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E2/scale0.75 | 0.000 +/- 0.000 | -55.167 +/- 12.684 | -39.497 +/- 20.454 | 67.848 +/- 4.642 | 130.836 +/- 34.661 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E2/scale0.9 | 0.000 +/- 0.000 | -55.167 +/- 12.684 | -39.497 +/- 20.454 | 67.848 +/- 4.642 | 130.836 +/- 34.661 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E2/scale1.1 | 0.000 +/- 0.000 | -55.167 +/- 12.684 | -39.497 +/- 20.454 | 67.848 +/- 4.642 | 130.836 +/- 34.661 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E2/scale1.25 | 0.000 +/- 0.000 | -55.167 +/- 12.684 | -39.497 +/- 20.454 | 67.848 +/- 4.642 | 130.836 +/- 34.661 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E2/scale1.5 | 0.000 +/- 0.000 | -55.167 +/- 12.684 | -39.497 +/- 20.454 | 67.848 +/- 4.642 | 130.836 +/- 34.661 | 0.875 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E2/wrongFront | 0.000 +/- 0.000 | -35.004 +/- 20.092 | -39.773 +/- 0.000 | 52.983 +/- 7.664 | 81.467 +/- 61.176 | 0.979 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E3/model | 0.000 +/- 0.000 | -28.513 +/- 20.332 | -19.048 +/- 2.210 | 34.290 +/- 10.016 | 67.015 +/- 69.538 | 0.979 +/- 0.036 | 3.146 +/- 0.872 |
| head5m | 60 | 0 | 250 | E4/measured3600 | 0.000 +/- 0.000 | 12.063 +/- 2.601 | -2.069 +/- 1.007 | 12.239 +/- 1.447 | 13.325 +/- 2.559 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4/measured600 | 0.000 +/- 0.000 | 12.070 +/- 2.591 | -2.069 +/- 1.006 | 12.246 +/- 1.442 | 13.331 +/- 2.552 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4/model | 0.000 +/- 0.000 | 12.065 +/- 2.596 | -2.070 +/- 1.007 | 12.242 +/- 1.444 | 13.328 +/- 2.556 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4/oracleShape | 0.000 +/- 0.000 | 12.064 +/- 2.600 | -2.070 +/- 1.008 | 12.240 +/- 1.446 | 13.326 +/- 2.559 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4/scale0.5 | 0.000 +/- 0.000 | 12.065 +/- 2.596 | -2.070 +/- 1.007 | 12.242 +/- 1.444 | 13.328 +/- 2.556 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4/scale0.75 | 0.000 +/- 0.000 | 12.065 +/- 2.596 | -2.070 +/- 1.007 | 12.242 +/- 1.444 | 13.328 +/- 2.556 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4/scale0.9 | 0.000 +/- 0.000 | 12.065 +/- 2.596 | -2.070 +/- 1.007 | 12.242 +/- 1.444 | 13.328 +/- 2.556 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4/scale1.1 | 0.000 +/- 0.000 | 12.065 +/- 2.596 | -2.070 +/- 1.007 | 12.242 +/- 1.444 | 13.328 +/- 2.556 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4/scale1.25 | 0.000 +/- 0.000 | 12.065 +/- 2.596 | -2.070 +/- 1.007 | 12.242 +/- 1.444 | 13.328 +/- 2.556 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4/scale1.5 | 0.000 +/- 0.000 | 12.065 +/- 2.596 | -2.070 +/- 1.007 | 12.242 +/- 1.444 | 13.328 +/- 2.556 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4/wrongFront | 0.000 +/- 0.000 | 11.991 +/- 2.650 | -2.061 +/- 0.992 | 12.167 +/- 1.473 | 13.269 +/- 2.588 | 1.000 +/- 0.000 | 10.716 +/- 2.669 |
| head5m | 60 | 0 | 250 | E4@240/model | 0.000 +/- 0.000 | 7.322 +/- 7.508 | -7.951 +/- 10.140 | 10.809 +/- 1.495 | 40.308 +/- 46.895 | 0.979 +/- 0.036 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4@400/model | 0.000 +/- 0.000 | 2.545 +/- 11.369 | -4.123 +/- 14.140 | 4.845 +/- 8.026 | 63.473 +/- 40.901 | 0.958 +/- 0.036 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 250 | E4@60/model | 0.000 +/- 0.000 | 10.987 +/- 1.944 | -1.673 +/- 0.591 | 11.114 +/- 1.098 | 11.678 +/- 2.045 | 1.000 +/- 0.000 | 0.008 +/- 0.005 |
| head5m | 60 | 0 | 250 | E5/measured3600 | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E5/measured600 | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E5/model | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E5/oracleShape | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E5/scale0.5 | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E5/scale0.75 | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E5/scale0.9 | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E5/scale1.1 | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E5/scale1.25 | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E5/scale1.5 | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E5/wrongFront | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E5@240/model | 0.000 +/- 0.000 | 7.313 +/- 7.511 | -7.941 +/- 10.141 | 10.795 +/- 1.495 | 40.303 +/- 46.898 | 0.979 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E5@400/model | 0.000 +/- 0.000 | 2.539 +/- 11.371 | -4.119 +/- 14.140 | 4.839 +/- 8.027 | 63.473 +/- 40.906 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E5@60/model | 0.000 +/- 0.000 | 10.976 +/- 1.953 | -1.604 +/- 0.590 | 11.092 +/- 1.105 | 11.667 +/- 2.053 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 250 | E6/measured3600 | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E6/measured600 | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E6/model | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E6/oracleShape | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E6/scale0.5 | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E6/scale0.75 | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E6/scale0.9 | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E6/scale1.1 | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E6/scale1.25 | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E6/scale1.5 | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E6/wrongFront | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0 | 250 | E7/model | 0.000 +/- 0.000 | 11.471 +/- 28.916 | -3.732 +/- 6.127 | 12.063 +/- 16.970 | 82.758 +/- 75.940 | 0.958 +/- 0.072 | 3.146 +/- 0.872 |
| head5m | 60 | 0 | 1000 | E0 | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -35.366 +/- 0.053 | 39.143 +/- 0.028 | 39.144 +/- 0.048 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | -23.404 +/- 0.000 | -39.773 +/- 0.000 | 46.148 +/- 0.000 | 46.148 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | 14.952 +/- 2.839 |
| head5m | 60 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 10.835 +/- 0.552 | -1.561 +/- 0.401 | 10.947 +/- 0.299 | 11.382 +/- 0.639 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 10.795 +/- 0.561 | -1.532 +/- 0.403 | 10.903 +/- 0.305 | 11.339 +/- 0.647 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 34.562 +/- 2.498 | -10.598 +/- 1.786 | 36.150 +/- 1.681 | 39.069 +/- 0.896 | 1.000 +/- 0.000 | 14.952 +/- 2.839 |
| head5m | 60 | 0.05 | 25 | E0 | 1005.933 +/- 0.468 | -259.629 +/- 44.589 | 38.527 +/- 0.365 | 262.472 +/- 25.494 | 335.672 +/- 33.872 | 0.188 +/- 0.062 | - |
| head5m | 60 | 0.05 | 25 | E1/model | 1005.933 +/- 0.468 | -302.877 +/- 28.517 | 0.459 +/- 18.059 | 302.878 +/- 16.456 | 403.071 +/- 29.444 | 0.021 +/- 0.036 | - |
| head5m | 60 | 0.05 | 25 | E2/model | 1005.933 +/- 0.468 | -307.613 +/- 30.070 | -3.314 +/- 17.925 | 307.631 +/- 17.423 | 399.844 +/- 25.296 | 0.021 +/- 0.036 | - |
| head5m | 60 | 0.05 | 25 | E3/model | 1005.933 +/- 0.468 | -347.786 +/- 22.504 | -14.893 +/- 23.133 | 348.105 +/- 12.410 | 418.290 +/- 18.949 | 0.021 +/- 0.036 | 928.597 +/- 4.299 |
| head5m | 60 | 0.05 | 25 | E4/model | 1005.933 +/- 0.468 | -250.477 +/- 32.335 | 32.297 +/- 53.282 | 252.551 +/- 21.881 | 351.533 +/- 23.634 | 0.062 +/- 0.000 | 937.624 +/- 4.676 |
| head5m | 60 | 0.05 | 25 | E5/model | 1005.933 +/- 0.468 | -260.017 +/- 23.623 | 33.305 +/- 30.052 | 262.141 +/- 15.531 | 355.284 +/- 20.188 | 0.062 +/- 0.000 | - |
| head5m | 60 | 0.05 | 25 | E6/model | 1005.933 +/- 0.468 | -328.279 +/- 19.735 | 4.743 +/- 6.456 | 328.313 +/- 11.340 | 407.990 +/- 24.797 | 0.021 +/- 0.036 | - |
| head5m | 60 | 0.05 | 25 | E7/model | 1005.933 +/- 0.468 | -252.369 +/- 49.967 | 10.162 +/- 10.276 | 252.573 +/- 29.018 | 363.685 +/- 52.104 | 0.042 +/- 0.036 | 928.597 +/- 4.299 |
| head5m | 60 | 0.05 | 100 | E0 | 1005.933 +/- 0.468 | -257.294 +/- 106.139 | 38.334 +/- 0.607 | 260.134 +/- 60.560 | 344.015 +/- 88.033 | 0.354 +/- 0.144 | - |
| head5m | 60 | 0.05 | 100 | E1/model | 1005.933 +/- 0.468 | -293.265 +/- 63.638 | -15.732 +/- 32.954 | 293.687 +/- 35.863 | 388.977 +/- 40.057 | 0.062 +/- 0.062 | - |
| head5m | 60 | 0.05 | 100 | E2/model | 1005.933 +/- 0.468 | -287.450 +/- 70.593 | -14.362 +/- 25.409 | 287.809 +/- 39.992 | 381.822 +/- 52.868 | 0.062 +/- 0.062 | - |
| head5m | 60 | 0.05 | 100 | E3/model | 1005.933 +/- 0.468 | -309.144 +/- 141.274 | -8.130 +/- 32.023 | 309.251 +/- 81.052 | 403.723 +/- 97.006 | 0.208 +/- 0.144 | 929.521 +/- 11.890 |
| head5m | 60 | 0.05 | 100 | E4/model | 1005.933 +/- 0.468 | -205.220 +/- 71.413 | 31.067 +/- 21.298 | 207.558 +/- 39.952 | 319.012 +/- 63.012 | 0.208 +/- 0.036 | 901.921 +/- 8.700 |
| head5m | 60 | 0.05 | 100 | E5/model | 1005.933 +/- 0.468 | -213.596 +/- 64.573 | 33.807 +/- 12.510 | 216.254 +/- 37.264 | 327.605 +/- 57.053 | 0.188 +/- 0.062 | - |
| head5m | 60 | 0.05 | 100 | E6/model | 1005.933 +/- 0.468 | -308.282 +/- 95.156 | -15.651 +/- 56.147 | 308.679 +/- 53.251 | 406.500 +/- 69.267 | 0.125 +/- 0.108 | - |
| head5m | 60 | 0.05 | 100 | E7/model | 1005.933 +/- 0.468 | -205.850 +/- 86.574 | 10.673 +/- 6.503 | 206.127 +/- 49.725 | 325.191 +/- 70.659 | 0.208 +/- 0.072 | 929.521 +/- 11.890 |
| head5m | 60 | 0.05 | 250 | E0 | 1005.933 +/- 0.468 | -95.671 +/- 84.895 | 36.046 +/- 0.078 | 102.236 +/- 45.882 | 179.167 +/- 130.570 | 0.812 +/- 0.165 | - |
| head5m | 60 | 0.05 | 250 | E1/model | 1005.933 +/- 0.468 | -213.355 +/- 97.338 | -36.248 +/- 54.913 | 216.413 +/- 58.729 | 324.277 +/- 90.780 | 0.312 +/- 0.062 | - |
| head5m | 60 | 0.05 | 250 | E2/model | 1005.933 +/- 0.468 | -221.439 +/- 104.768 | -40.325 +/- 55.320 | 225.080 +/- 63.013 | 327.223 +/- 93.992 | 0.292 +/- 0.095 | - |
| head5m | 60 | 0.05 | 250 | E3/model | 1005.933 +/- 0.468 | -154.208 +/- 65.628 | 9.842 +/- 15.653 | 154.522 +/- 37.690 | 277.611 +/- 58.157 | 0.646 +/- 0.130 | 919.836 +/- 16.075 |
| head5m | 60 | 0.05 | 250 | E4/model | 1005.933 +/- 0.468 | -50.741 +/- 78.877 | 5.535 +/- 14.502 | 51.042 +/- 44.379 | 140.333 +/- 128.636 | 0.771 +/- 0.130 | 854.197 +/- 16.646 |
| head5m | 60 | 0.05 | 250 | E5/model | 1005.933 +/- 0.468 | -38.234 +/- 52.138 | -4.195 +/- 22.185 | 38.464 +/- 31.231 | 132.319 +/- 99.987 | 0.771 +/- 0.072 | - |
| head5m | 60 | 0.05 | 250 | E6/model | 1005.933 +/- 0.468 | -163.455 +/- 87.274 | -3.887 +/- 23.816 | 163.501 +/- 50.378 | 285.047 +/- 75.285 | 0.604 +/- 0.144 | - |
| head5m | 60 | 0.05 | 250 | E7/model | 1005.933 +/- 0.468 | -66.036 +/- 107.505 | 4.886 +/- 10.461 | 66.216 +/- 61.687 | 170.890 +/- 143.436 | 0.708 +/- 0.130 | 919.836 +/- 16.075 |
| head5m | 60 | 0.05 | 1000 | E0 | 1005.933 +/- 0.468 | -16.775 +/- 0.000 | 18.014 +/- 5.167 | 24.615 +/- 2.183 | 34.593 +/- 1.067 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.05 | 1000 | E1/model | 1005.933 +/- 0.468 | -21.094 +/- 7.481 | -35.521 +/- 0.337 | 41.312 +/- 2.372 | 48.908 +/- 16.972 | 0.979 +/- 0.036 | - |
| head5m | 60 | 0.05 | 1000 | E2/model | 1005.933 +/- 0.468 | -27.547 +/- 7.176 | -39.773 +/- 0.000 | 48.381 +/- 2.359 | 54.763 +/- 14.922 | 0.979 +/- 0.036 | - |
| head5m | 60 | 0.05 | 1000 | E3/model | 1005.933 +/- 0.468 | -16.775 +/- 0.000 | -18.777 +/- 1.921 | 25.179 +/- 0.827 | 26.299 +/- 0.489 | 1.000 +/- 0.000 | 780.498 +/- 21.919 |
| head5m | 60 | 0.05 | 1000 | E4/model | 1005.933 +/- 0.468 | 9.854 +/- 0.894 | -0.298 +/- 0.177 | 9.858 +/- 0.513 | 10.190 +/- 0.823 | 1.000 +/- 0.000 | 575.370 +/- 30.588 |
| head5m | 60 | 0.05 | 1000 | E5/model | 1005.933 +/- 0.468 | 9.768 +/- 0.877 | -0.980 +/- 0.115 | 9.817 +/- 0.499 | 10.100 +/- 0.807 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.05 | 1000 | E6/model | 1005.933 +/- 0.468 | -16.775 +/- 0.000 | -20.164 +/- 0.480 | 26.229 +/- 0.213 | 26.295 +/- 0.482 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.05 | 1000 | E7/model | 1005.933 +/- 0.468 | 32.868 +/- 5.718 | -5.799 +/- 1.995 | 33.375 +/- 3.415 | 40.955 +/- 1.690 | 1.000 +/- 0.000 | 780.498 +/- 21.919 |
| head5m | 60 | 0.1 | 25 | E0 | 2011.866 +/- 0.936 | -289.158 +/- 67.459 | 39.838 +/- 0.480 | 291.890 +/- 38.556 | 352.185 +/- 57.445 | 0.083 +/- 0.036 | - |
| head5m | 60 | 0.1 | 25 | E1/model | 2011.866 +/- 0.936 | -310.370 +/- 51.503 | -60.429 +/- 63.189 | 316.198 +/- 22.388 | 401.563 +/- 38.682 | 0.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 25 | E2/model | 2011.866 +/- 0.936 | -314.242 +/- 50.456 | -64.078 +/- 63.019 | 320.708 +/- 21.486 | 405.564 +/- 38.157 | 0.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 25 | E3/model | 2011.866 +/- 0.936 | -334.000 +/- 27.437 | 1.288 +/- 83.764 | 334.003 +/- 16.014 | 411.456 +/- 26.243 | 0.042 +/- 0.036 | 1887.603 +/- 14.898 |
| head5m | 60 | 0.1 | 25 | E4/model | 2011.866 +/- 0.936 | -298.481 +/- 72.221 | 8.773 +/- 27.511 | 298.610 +/- 41.355 | 380.104 +/- 63.568 | 0.083 +/- 0.072 | 1922.316 +/- 26.288 |
| head5m | 60 | 0.1 | 25 | E5/model | 2011.866 +/- 0.936 | -289.152 +/- 80.671 | -3.648 +/- 8.165 | 289.175 +/- 46.630 | 376.486 +/- 66.783 | 0.062 +/- 0.062 | - |
| head5m | 60 | 0.1 | 25 | E6/model | 2011.866 +/- 0.936 | -316.502 +/- 79.345 | -37.625 +/- 78.259 | 318.730 +/- 40.157 | 406.585 +/- 59.919 | 0.042 +/- 0.036 | - |
| head5m | 60 | 0.1 | 25 | E7/model | 2011.866 +/- 0.936 | -283.638 +/- 76.435 | -7.425 +/- 20.510 | 283.735 +/- 44.055 | 371.941 +/- 73.291 | 0.042 +/- 0.036 | 1887.603 +/- 14.898 |
| head5m | 60 | 0.1 | 100 | E0 | 2011.866 +/- 0.936 | -231.827 +/- 28.978 | 38.977 +/- 0.123 | 235.081 +/- 16.489 | 320.468 +/- 19.541 | 0.292 +/- 0.144 | - |
| head5m | 60 | 0.1 | 100 | E1/model | 2011.866 +/- 0.936 | -340.363 +/- 21.310 | -49.735 +/- 64.550 | 343.978 +/- 15.236 | 435.042 +/- 4.752 | 0.062 +/- 0.062 | - |
| head5m | 60 | 0.1 | 100 | E2/model | 2011.866 +/- 0.936 | -340.481 +/- 21.033 | -47.230 +/- 55.144 | 343.741 +/- 13.666 | 433.838 +/- 7.838 | 0.062 +/- 0.062 | - |
| head5m | 60 | 0.1 | 100 | E3/model | 2011.866 +/- 0.936 | -335.908 +/- 12.723 | -12.469 +/- 25.582 | 336.140 +/- 6.795 | 421.163 +/- 22.282 | 0.104 +/- 0.036 | 1892.026 +/- 20.293 |
| head5m | 60 | 0.1 | 100 | E4/model | 2011.866 +/- 0.936 | -233.013 +/- 66.866 | 13.199 +/- 13.806 | 233.387 +/- 38.095 | 359.251 +/- 50.026 | 0.146 +/- 0.036 | 1903.181 +/- 18.435 |
| head5m | 60 | 0.1 | 100 | E5/model | 2011.866 +/- 0.936 | -229.834 +/- 70.476 | 29.938 +/- 36.019 | 231.775 +/- 38.944 | 353.795 +/- 48.558 | 0.125 +/- 0.062 | - |
| head5m | 60 | 0.1 | 100 | E6/model | 2011.866 +/- 0.936 | -353.199 +/- 3.746 | -47.813 +/- 50.405 | 356.421 +/- 3.184 | 446.863 +/- 5.707 | 0.062 +/- 0.062 | - |
| head5m | 60 | 0.1 | 100 | E7/model | 2011.866 +/- 0.936 | -231.483 +/- 61.683 | 26.139 +/- 14.817 | 232.954 +/- 34.732 | 353.253 +/- 37.084 | 0.104 +/- 0.095 | 1892.026 +/- 20.293 |
| head5m | 60 | 0.1 | 250 | E0 | 2011.866 +/- 0.936 | -88.238 +/- 46.661 | 38.193 +/- 0.732 | 96.149 +/- 24.785 | 170.591 +/- 94.295 | 0.729 +/- 0.095 | - |
| head5m | 60 | 0.1 | 250 | E1/measured3600 | 2011.866 +/- 0.936 | -205.231 +/- 35.684 | -14.568 +/- 95.072 | 205.747 +/- 24.301 | 309.351 +/- 38.007 | 0.229 +/- 0.157 | - |
| head5m | 60 | 0.1 | 250 | E1/measured600 | 2011.866 +/- 0.936 | -211.425 +/- 48.674 | -64.454 +/- 71.506 | 221.031 +/- 22.590 | 326.914 +/- 41.777 | 0.188 +/- 0.108 | - |
| head5m | 60 | 0.1 | 250 | E1/model | 2011.866 +/- 0.936 | -205.204 +/- 21.997 | -51.549 +/- 72.844 | 211.580 +/- 14.217 | 315.769 +/- 14.302 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E1/oracleShape | 2011.866 +/- 0.936 | -205.512 +/- 21.762 | -51.834 +/- 73.341 | 211.948 +/- 14.356 | 316.000 +/- 14.108 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E1/scale0.5 | 2011.866 +/- 0.936 | -205.204 +/- 21.997 | -51.549 +/- 72.844 | 211.580 +/- 14.217 | 315.769 +/- 14.302 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E1/scale0.75 | 2011.866 +/- 0.936 | -205.204 +/- 21.997 | -51.549 +/- 72.844 | 211.580 +/- 14.217 | 315.769 +/- 14.302 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E1/scale0.9 | 2011.866 +/- 0.936 | -205.204 +/- 21.997 | -51.549 +/- 72.844 | 211.580 +/- 14.217 | 315.769 +/- 14.302 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E1/scale1.1 | 2011.866 +/- 0.936 | -205.204 +/- 21.997 | -51.549 +/- 72.844 | 211.580 +/- 14.217 | 315.769 +/- 14.302 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E1/scale1.25 | 2011.866 +/- 0.936 | -205.204 +/- 21.997 | -51.549 +/- 72.844 | 211.580 +/- 14.217 | 315.769 +/- 14.302 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E1/scale1.5 | 2011.866 +/- 0.936 | -205.204 +/- 21.997 | -51.549 +/- 72.844 | 211.580 +/- 14.217 | 315.769 +/- 14.302 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E1/wrongFront | 2011.866 +/- 0.936 | -63.855 +/- 33.585 | 40.372 +/- 0.613 | 75.547 +/- 16.356 | 134.737 +/- 77.998 | 0.938 +/- 0.062 | - |
| head5m | 60 | 0.1 | 250 | E2/measured3600 | 2011.866 +/- 0.936 | -207.076 +/- 34.328 | -18.782 +/- 95.416 | 207.926 +/- 24.533 | 310.785 +/- 38.867 | 0.229 +/- 0.157 | - |
| head5m | 60 | 0.1 | 250 | E2/measured600 | 2011.866 +/- 0.936 | -212.324 +/- 47.172 | -77.336 +/- 65.351 | 225.970 +/- 23.199 | 327.799 +/- 40.309 | 0.188 +/- 0.108 | - |
| head5m | 60 | 0.1 | 250 | E2/model | 2011.866 +/- 0.936 | -209.286 +/- 20.836 | -60.211 +/- 68.610 | 217.775 +/- 11.308 | 321.917 +/- 12.787 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E2/oracleShape | 2011.866 +/- 0.936 | -207.353 +/- 22.249 | -56.068 +/- 73.373 | 214.799 +/- 14.285 | 318.055 +/- 14.434 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E2/scale0.5 | 2011.866 +/- 0.936 | -209.286 +/- 20.836 | -60.211 +/- 68.610 | 217.775 +/- 11.308 | 321.917 +/- 12.787 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E2/scale0.75 | 2011.866 +/- 0.936 | -209.286 +/- 20.836 | -60.211 +/- 68.610 | 217.775 +/- 11.308 | 321.917 +/- 12.787 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E2/scale0.9 | 2011.866 +/- 0.936 | -209.286 +/- 20.836 | -60.211 +/- 68.610 | 217.775 +/- 11.308 | 321.917 +/- 12.787 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E2/scale1.1 | 2011.866 +/- 0.936 | -209.286 +/- 20.836 | -60.211 +/- 68.610 | 217.775 +/- 11.308 | 321.917 +/- 12.787 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E2/scale1.25 | 2011.866 +/- 0.936 | -209.286 +/- 20.836 | -60.211 +/- 68.610 | 217.775 +/- 11.308 | 321.917 +/- 12.787 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E2/scale1.5 | 2011.866 +/- 0.936 | -209.286 +/- 20.836 | -60.211 +/- 68.610 | 217.775 +/- 11.308 | 321.917 +/- 12.787 | 0.188 +/- 0.125 | - |
| head5m | 60 | 0.1 | 250 | E2/wrongFront | 2011.866 +/- 0.936 | -66.767 +/- 34.026 | 39.773 +/- 0.000 | 77.715 +/- 16.878 | 136.421 +/- 78.796 | 0.938 +/- 0.062 | - |
| head5m | 60 | 0.1 | 250 | E3/model | 2011.866 +/- 0.936 | -169.756 +/- 16.325 | -2.283 +/- 24.969 | 169.771 +/- 9.611 | 297.752 +/- 9.501 | 0.562 +/- 0.062 | 1875.974 +/- 7.983 |
| head5m | 60 | 0.1 | 250 | E4/measured3600 | 2011.866 +/- 0.936 | -56.815 +/- 30.923 | -3.905 +/- 37.721 | 56.949 +/- 19.270 | 184.766 +/- 41.099 | 0.708 +/- 0.130 | 1827.607 +/- 9.551 |
| head5m | 60 | 0.1 | 250 | E4/measured600 | 2011.866 +/- 0.936 | -73.185 +/- 46.495 | -2.872 +/- 60.861 | 73.241 +/- 28.201 | 200.900 +/- 47.858 | 0.646 +/- 0.130 | 1758.918 +/- 7.334 |
| head5m | 60 | 0.1 | 250 | E4/model | 2011.866 +/- 0.936 | -58.414 +/- 29.624 | -0.263 +/- 42.689 | 58.415 +/- 17.205 | 186.933 +/- 38.924 | 0.688 +/- 0.108 | 1836.070 +/- 11.504 |
| head5m | 60 | 0.1 | 250 | E4/oracleShape | 2011.866 +/- 0.936 | -58.600 +/- 29.540 | -0.259 +/- 42.656 | 58.600 +/- 17.155 | 186.901 +/- 38.845 | 0.688 +/- 0.108 | 1838.685 +/- 7.865 |
| head5m | 60 | 0.1 | 250 | E4/scale0.5 | 2011.866 +/- 0.936 | -58.414 +/- 29.624 | -0.263 +/- 42.689 | 58.415 +/- 17.205 | 186.933 +/- 38.924 | 0.688 +/- 0.108 | 1836.070 +/- 11.504 |
| head5m | 60 | 0.1 | 250 | E4/scale0.75 | 2011.866 +/- 0.936 | -58.414 +/- 29.624 | -0.263 +/- 42.689 | 58.415 +/- 17.205 | 186.933 +/- 38.924 | 0.688 +/- 0.108 | 1836.070 +/- 11.504 |
| head5m | 60 | 0.1 | 250 | E4/scale0.9 | 2011.866 +/- 0.936 | -58.414 +/- 29.624 | -0.263 +/- 42.689 | 58.415 +/- 17.205 | 186.933 +/- 38.924 | 0.688 +/- 0.108 | 1836.070 +/- 11.504 |
| head5m | 60 | 0.1 | 250 | E4/scale1.1 | 2011.866 +/- 0.936 | -58.414 +/- 29.624 | -0.263 +/- 42.689 | 58.415 +/- 17.205 | 186.933 +/- 38.924 | 0.688 +/- 0.108 | 1836.070 +/- 11.504 |
| head5m | 60 | 0.1 | 250 | E4/scale1.25 | 2011.866 +/- 0.936 | -58.414 +/- 29.624 | -0.263 +/- 42.689 | 58.415 +/- 17.205 | 186.933 +/- 38.924 | 0.688 +/- 0.108 | 1836.070 +/- 11.504 |
| head5m | 60 | 0.1 | 250 | E4/scale1.5 | 2011.866 +/- 0.936 | -58.414 +/- 29.624 | -0.263 +/- 42.689 | 58.415 +/- 17.205 | 186.933 +/- 38.924 | 0.688 +/- 0.108 | 1836.070 +/- 11.504 |
| head5m | 60 | 0.1 | 250 | E4/wrongFront | 2011.866 +/- 0.936 | -165.010 +/- 38.428 | 32.673 +/- 13.677 | 168.214 +/- 23.049 | 281.619 +/- 19.431 | 0.542 +/- 0.157 | 0.019 +/- 0.005 |
| head5m | 60 | 0.1 | 250 | E4@240/model | 2011.866 +/- 0.936 | -62.266 +/- 22.462 | 5.155 +/- 41.177 | 62.479 +/- 10.974 | 190.436 +/- 28.083 | 0.646 +/- 0.095 | 1813.558 +/- 11.884 |
| head5m | 60 | 0.1 | 250 | E4@400/model | 2011.866 +/- 0.936 | -83.012 +/- 41.733 | 14.582 +/- 26.173 | 84.283 +/- 21.375 | 219.359 +/- 47.624 | 0.562 +/- 0.108 | 1808.840 +/- 11.602 |
| head5m | 60 | 0.1 | 250 | E4@60/model | 2011.866 +/- 0.936 | -54.676 +/- 23.265 | 11.461 +/- 25.421 | 55.864 +/- 11.026 | 185.495 +/- 36.901 | 0.688 +/- 0.108 | 1862.571 +/- 13.653 |
| head5m | 60 | 0.1 | 250 | E5/measured3600 | 2011.866 +/- 0.936 | -84.961 +/- 26.184 | 14.933 +/- 43.154 | 86.263 +/- 10.647 | 216.889 +/- 27.702 | 0.646 +/- 0.130 | - |
| head5m | 60 | 0.1 | 250 | E5/measured600 | 2011.866 +/- 0.936 | -95.515 +/- 35.689 | -24.887 +/- 74.410 | 98.704 +/- 30.489 | 235.205 +/- 24.482 | 0.604 +/- 0.157 | - |
| head5m | 60 | 0.1 | 250 | E5/model | 2011.866 +/- 0.936 | -81.230 +/- 10.004 | 1.302 +/- 41.509 | 81.240 +/- 5.391 | 215.303 +/- 21.379 | 0.625 +/- 0.062 | - |
| head5m | 60 | 0.1 | 250 | E5/oracleShape | 2011.866 +/- 0.936 | -75.924 +/- 28.328 | -5.065 +/- 28.367 | 76.092 +/- 16.988 | 210.330 +/- 37.303 | 0.625 +/- 0.108 | - |
| head5m | 60 | 0.1 | 250 | E5/scale0.5 | 2011.866 +/- 0.936 | -54.625 +/- 9.853 | 4.811 +/- 10.235 | 54.836 +/- 6.140 | 177.319 +/- 15.426 | 0.812 +/- 0.000 | - |
| head5m | 60 | 0.1 | 250 | E5/scale0.75 | 2011.866 +/- 0.936 | -60.572 +/- 18.254 | 12.720 +/- 14.226 | 61.893 +/- 10.400 | 194.448 +/- 26.618 | 0.688 +/- 0.108 | - |
| head5m | 60 | 0.1 | 250 | E5/scale0.9 | 2011.866 +/- 0.936 | -58.402 +/- 29.653 | -0.198 +/- 42.646 | 58.402 +/- 17.197 | 186.941 +/- 38.941 | 0.688 +/- 0.108 | - |
| head5m | 60 | 0.1 | 250 | E5/scale1.1 | 2011.866 +/- 0.936 | -92.296 +/- 20.500 | 4.585 +/- 23.413 | 92.410 +/- 11.169 | 226.985 +/- 31.793 | 0.562 +/- 0.108 | - |
| head5m | 60 | 0.1 | 250 | E5/scale1.25 | 2011.866 +/- 0.936 | -109.689 +/- 32.138 | 11.863 +/- 15.217 | 110.329 +/- 17.505 | 245.513 +/- 46.773 | 0.542 +/- 0.144 | - |
| head5m | 60 | 0.1 | 250 | E5/scale1.5 | 2011.866 +/- 0.936 | -112.885 +/- 43.412 | 4.968 +/- 21.938 | 112.995 +/- 24.511 | 241.216 +/- 70.132 | 0.479 +/- 0.144 | - |
| head5m | 60 | 0.1 | 250 | E5/wrongFront | 2011.866 +/- 0.936 | -154.621 +/- 40.734 | 74.582 +/- 2.297 | 171.669 +/- 21.414 | 255.004 +/- 49.839 | 0.229 +/- 0.095 | - |
| head5m | 60 | 0.1 | 250 | E5@240/model | 2011.866 +/- 0.936 | -81.121 +/- 20.901 | -2.811 +/- 24.732 | 81.169 +/- 12.490 | 217.339 +/- 26.792 | 0.583 +/- 0.072 | - |
| head5m | 60 | 0.1 | 250 | E5@400/model | 2011.866 +/- 0.936 | -67.455 +/- 26.098 | 4.692 +/- 30.887 | 67.618 +/- 13.794 | 198.562 +/- 26.036 | 0.625 +/- 0.108 | - |
| head5m | 60 | 0.1 | 250 | E5@60/model | 2011.866 +/- 0.936 | -63.967 +/- 26.109 | 3.462 +/- 44.116 | 64.061 +/- 13.680 | 188.704 +/- 37.443 | 0.688 +/- 0.108 | - |
| head5m | 60 | 0.1 | 250 | E6/measured3600 | 2011.866 +/- 0.936 | -158.882 +/- 44.809 | -24.962 +/- 21.744 | 160.831 +/- 27.212 | 283.883 +/- 42.319 | 0.479 +/- 0.130 | - |
| head5m | 60 | 0.1 | 250 | E6/measured600 | 2011.866 +/- 0.936 | -139.203 +/- 30.278 | 11.135 +/- 31.878 | 139.648 +/- 16.151 | 255.466 +/- 12.303 | 0.542 +/- 0.095 | - |
| head5m | 60 | 0.1 | 250 | E6/model | 2011.866 +/- 0.936 | -177.463 +/- 25.748 | -35.111 +/- 14.552 | 180.903 +/- 14.173 | 302.740 +/- 29.855 | 0.417 +/- 0.130 | - |
| head5m | 60 | 0.1 | 250 | E6/oracleShape | 2011.866 +/- 0.936 | -177.458 +/- 25.776 | -34.455 +/- 12.252 | 180.772 +/- 13.917 | 302.191 +/- 30.313 | 0.417 +/- 0.130 | - |
| head5m | 60 | 0.1 | 250 | E6/scale0.5 | 2011.866 +/- 0.936 | -104.012 +/- 21.536 | 34.148 +/- 4.099 | 109.474 +/- 12.491 | 210.841 +/- 37.480 | 0.729 +/- 0.036 | - |
| head5m | 60 | 0.1 | 250 | E6/scale0.75 | 2011.866 +/- 0.936 | -107.663 +/- 38.493 | 26.850 +/- 10.867 | 110.960 +/- 20.432 | 214.658 +/- 54.431 | 0.750 +/- 0.062 | - |
| head5m | 60 | 0.1 | 250 | E6/scale0.9 | 2011.866 +/- 0.936 | -148.365 +/- 50.986 | 7.078 +/- 21.833 | 148.533 +/- 28.810 | 263.558 +/- 44.084 | 0.625 +/- 0.188 | - |
| head5m | 60 | 0.1 | 250 | E6/scale1.1 | 2011.866 +/- 0.936 | -193.152 +/- 36.173 | -48.829 +/- 58.791 | 199.228 +/- 16.743 | 304.062 +/- 37.292 | 0.250 +/- 0.108 | - |
| head5m | 60 | 0.1 | 250 | E6/scale1.25 | 2011.866 +/- 0.936 | -203.479 +/- 7.910 | -71.772 +/- 66.119 | 215.766 +/- 16.166 | 308.412 +/- 13.542 | 0.104 +/- 0.095 | - |
| head5m | 60 | 0.1 | 250 | E6/scale1.5 | 2011.866 +/- 0.936 | -223.108 +/- 1.359 | -85.274 +/- 39.986 | 238.849 +/- 7.510 | 313.733 +/- 15.755 | 0.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 250 | E6/wrongFront | 2011.866 +/- 0.936 | -64.329 +/- 33.857 | 39.932 +/- 0.552 | 75.715 +/- 16.580 | 135.230 +/- 78.556 | 0.917 +/- 0.072 | - |
| head5m | 60 | 0.1 | 250 | E7/model | 2011.866 +/- 0.936 | -107.031 +/- 50.483 | 3.628 +/- 27.189 | 107.093 +/- 29.239 | 242.847 +/- 65.174 | 0.583 +/- 0.095 | 1875.974 +/- 7.983 |
| head5m | 60 | 0.1 | 1000 | E0 | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | 34.689 +/- 0.116 | 38.532 +/- 0.060 | 38.535 +/- 0.105 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E1/model | 2011.866 +/- 0.936 | -25.573 +/- 7.619 | -24.619 +/- 9.382 | 35.497 +/- 0.592 | 71.760 +/- 28.177 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E2/model | 2011.866 +/- 0.936 | -31.690 +/- 7.176 | -28.725 +/- 9.568 | 42.771 +/- 0.640 | 75.968 +/- 25.825 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E3/model | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | -14.333 +/- 1.928 | 22.064 +/- 0.723 | 27.421 +/- 0.483 | 1.000 +/- 0.000 | 1760.742 +/- 19.616 |
| head5m | 60 | 0.1 | 1000 | E4/model | 2011.866 +/- 0.936 | 9.511 +/- 0.154 | -0.120 +/- 0.213 | 9.511 +/- 0.090 | 9.997 +/- 0.080 | 1.000 +/- 0.000 | 1547.093 +/- 28.643 |
| head5m | 60 | 0.1 | 1000 | E5/model | 2011.866 +/- 0.936 | 9.505 +/- 0.108 | -0.681 +/- 0.202 | 9.529 +/- 0.069 | 9.968 +/- 0.032 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E6/model | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | -20.171 +/- 0.944 | 26.235 +/- 0.419 | 27.646 +/- 1.382 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E7/model | 2011.866 +/- 0.936 | 14.697 +/- 21.438 | -8.295 +/- 1.540 | 16.876 +/- 10.515 | 75.439 +/- 64.498 | 0.979 +/- 0.036 | 1760.742 +/- 19.616 |
| head5m | 60 | 0.2 | 25 | E0 | 4023.732 +/- 1.873 | -264.889 +/- 24.183 | 40.595 +/- 0.588 | 267.982 +/- 13.751 | 330.712 +/- 17.999 | 0.104 +/- 0.036 | - |
| head5m | 60 | 0.2 | 25 | E1/model | 4023.732 +/- 1.873 | -326.597 +/- 24.783 | -9.475 +/- 61.420 | 326.734 +/- 15.052 | 418.230 +/- 31.198 | 0.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 25 | E2/model | 4023.732 +/- 1.873 | -329.433 +/- 24.807 | -13.534 +/- 62.981 | 329.711 +/- 15.416 | 421.561 +/- 31.953 | 0.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 25 | E3/model | 4023.732 +/- 1.873 | -344.318 +/- 4.066 | 17.450 +/- 62.251 | 344.760 +/- 1.835 | 421.767 +/- 13.674 | 0.083 +/- 0.036 | 3774.054 +/- 33.691 |
| head5m | 60 | 0.2 | 25 | E4/model | 4023.732 +/- 1.873 | -288.851 +/- 51.937 | 34.030 +/- 60.863 | 290.849 +/- 33.749 | 397.130 +/- 35.715 | 0.021 +/- 0.036 | 3851.506 +/- 35.537 |
| head5m | 60 | 0.2 | 25 | E5/model | 4023.732 +/- 1.873 | -283.199 +/- 33.508 | 34.071 +/- 82.314 | 285.241 +/- 23.561 | 394.798 +/- 27.457 | 0.021 +/- 0.036 | - |
| head5m | 60 | 0.2 | 25 | E6/model | 4023.732 +/- 1.873 | -309.294 +/- 27.607 | -8.228 +/- 48.128 | 309.403 +/- 15.857 | 401.413 +/- 24.190 | 0.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 25 | E7/model | 4023.732 +/- 1.873 | -298.006 +/- 39.047 | 32.863 +/- 112.375 | 299.813 +/- 28.767 | 399.366 +/- 36.719 | 0.042 +/- 0.072 | 3774.054 +/- 33.691 |
| head5m | 60 | 0.2 | 100 | E0 | 4023.732 +/- 1.873 | -193.934 +/- 76.618 | 40.428 +/- 0.427 | 198.103 +/- 43.262 | 262.961 +/- 92.450 | 0.208 +/- 0.095 | - |
| head5m | 60 | 0.2 | 100 | E1/model | 4023.732 +/- 1.873 | -312.885 +/- 69.697 | -17.487 +/- 48.444 | 313.373 +/- 38.994 | 408.190 +/- 56.530 | 0.021 +/- 0.036 | - |
| head5m | 60 | 0.2 | 100 | E2/model | 4023.732 +/- 1.873 | -316.175 +/- 70.115 | -20.991 +/- 49.559 | 316.871 +/- 38.984 | 411.366 +/- 56.845 | 0.021 +/- 0.036 | - |
| head5m | 60 | 0.2 | 100 | E3/model | 4023.732 +/- 1.873 | -347.496 +/- 24.691 | 17.634 +/- 10.038 | 347.943 +/- 14.529 | 428.592 +/- 20.515 | 0.083 +/- 0.095 | 3815.632 +/- 28.977 |
| head5m | 60 | 0.2 | 100 | E4/model | 4023.732 +/- 1.873 | -223.043 +/- 20.247 | 20.686 +/- 30.355 | 224.000 +/- 11.793 | 317.762 +/- 26.875 | 0.167 +/- 0.036 | 3868.328 +/- 26.301 |
| head5m | 60 | 0.2 | 100 | E5/model | 4023.732 +/- 1.873 | -214.414 +/- 30.976 | 1.232 +/- 20.436 | 214.417 +/- 17.949 | 309.978 +/- 41.291 | 0.125 +/- 0.062 | - |
| head5m | 60 | 0.2 | 100 | E6/model | 4023.732 +/- 1.873 | -349.128 +/- 35.189 | 17.164 +/- 26.432 | 349.550 +/- 20.963 | 434.870 +/- 26.829 | 0.021 +/- 0.036 | - |
| head5m | 60 | 0.2 | 100 | E7/model | 4023.732 +/- 1.873 | -220.452 +/- 22.144 | -12.207 +/- 35.969 | 220.789 +/- 11.618 | 312.988 +/- 28.628 | 0.083 +/- 0.036 | 3815.632 +/- 28.977 |
| head5m | 60 | 0.2 | 250 | E0 | 4023.732 +/- 1.873 | -128.678 +/- 9.405 | 39.775 +/- 0.123 | 134.686 +/- 5.202 | 211.015 +/- 10.620 | 0.438 +/- 0.000 | - |
| head5m | 60 | 0.2 | 250 | E1/model | 4023.732 +/- 1.873 | -293.564 +/- 41.826 | -38.082 +/- 25.769 | 296.024 +/- 22.554 | 395.880 +/- 31.525 | 0.104 +/- 0.095 | - |
| head5m | 60 | 0.2 | 250 | E2/model | 4023.732 +/- 1.873 | -295.460 +/- 41.861 | -41.430 +/- 25.078 | 298.351 +/- 22.474 | 397.766 +/- 31.430 | 0.104 +/- 0.095 | - |
| head5m | 60 | 0.2 | 250 | E3/model | 4023.732 +/- 1.873 | -272.398 +/- 82.427 | 16.031 +/- 26.643 | 272.869 +/- 47.036 | 379.255 +/- 67.045 | 0.312 +/- 0.188 | 3818.565 +/- 32.292 |
| head5m | 60 | 0.2 | 250 | E4/model | 4023.732 +/- 1.873 | -112.895 +/- 31.043 | 6.705 +/- 30.874 | 113.094 +/- 17.345 | 240.836 +/- 32.087 | 0.500 +/- 0.062 | 3839.899 +/- 36.713 |
| head5m | 60 | 0.2 | 250 | E5/model | 4023.732 +/- 1.873 | -125.523 +/- 35.409 | 30.277 +/- 11.541 | 129.122 +/- 18.317 | 252.681 +/- 28.479 | 0.438 +/- 0.062 | - |
| head5m | 60 | 0.2 | 250 | E6/model | 4023.732 +/- 1.873 | -299.262 +/- 43.408 | -15.168 +/- 14.661 | 299.646 +/- 25.457 | 408.089 +/- 28.935 | 0.208 +/- 0.130 | - |
| head5m | 60 | 0.2 | 250 | E7/model | 4023.732 +/- 1.873 | -138.044 +/- 23.312 | 60.085 +/- 32.941 | 150.554 +/- 7.509 | 270.612 +/- 20.149 | 0.333 +/- 0.095 | 3818.565 +/- 32.292 |
| head5m | 60 | 0.2 | 1000 | E0 | 4023.732 +/- 1.873 | -16.775 +/- 0.000 | 36.095 +/- 0.106 | 39.803 +/- 0.055 | 39.807 +/- 0.098 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E1/model | 4023.732 +/- 1.873 | -54.631 +/- 21.654 | -35.751 +/- 10.913 | 65.289 +/- 13.731 | 127.755 +/- 45.496 | 0.854 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E2/model | 4023.732 +/- 1.873 | -64.005 +/- 24.899 | -35.906 +/- 12.684 | 73.388 +/- 13.990 | 133.667 +/- 49.506 | 0.833 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E3/model | 4023.732 +/- 1.873 | -40.252 +/- 20.332 | -12.316 +/- 3.707 | 42.094 +/- 10.603 | 107.535 +/- 69.871 | 0.958 +/- 0.036 | 3706.297 +/- 13.871 |
| head5m | 60 | 0.2 | 1000 | E4/model | 4023.732 +/- 1.873 | 10.467 +/- 0.834 | -0.129 +/- 0.076 | 10.468 +/- 0.481 | 10.942 +/- 0.888 | 1.000 +/- 0.000 | 3513.239 +/- 14.430 |
| head5m | 60 | 0.2 | 1000 | E5/model | 4023.732 +/- 1.873 | 10.399 +/- 0.811 | -0.643 +/- 0.047 | 10.419 +/- 0.466 | 10.853 +/- 0.857 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E6/model | 4023.732 +/- 1.873 | -40.252 +/- 20.332 | -18.296 +/- 3.021 | 44.215 +/- 10.009 | 108.080 +/- 69.610 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.2 | 1000 | E7/model | 4023.732 +/- 1.873 | 7.948 +/- 24.180 | -1.085 +/- 3.822 | 8.021 +/- 13.551 | 113.624 +/- 63.803 | 0.958 +/- 0.036 | 3706.297 +/- 13.871 |
| lab | 60 | 0 | 25 | E0 | 0.000 +/- 0.000 | -3.382 +/- 0.693 | -1.688 +/- 1.053 | 3.780 +/- 0.490 | 7.821 +/- 0.969 | 0.562 +/- 0.125 | - |
| lab | 60 | 0 | 25 | E1/model | 0.000 +/- 0.000 | -8.065 +/- 1.285 | 0.802 +/- 0.485 | 8.105 +/- 0.758 | 11.723 +/- 0.933 | 0.417 +/- 0.095 | - |
| lab | 60 | 0 | 25 | E2/model | 0.000 +/- 0.000 | -9.556 +/- 0.725 | 1.142 +/- 0.368 | 9.624 +/- 0.436 | 12.860 +/- 0.484 | 0.333 +/- 0.072 | - |
| lab | 60 | 0 | 25 | E3/model | 0.000 +/- 0.000 | -3.380 +/- 2.476 | -0.621 +/- 0.932 | 3.437 +/- 1.402 | 7.549 +/- 2.479 | 0.625 +/- 0.165 | 0.587 +/- 0.336 |
| lab | 60 | 0 | 25 | E4/model | 0.000 +/- 0.000 | -3.344 +/- 0.897 | 0.274 +/- 0.501 | 3.356 +/- 0.518 | 7.443 +/- 1.380 | 0.646 +/- 0.130 | 0.001 +/- 0.002 |
| lab | 60 | 0 | 25 | E5/model | 0.000 +/- 0.000 | -3.444 +/- 0.402 | 0.018 +/- 0.347 | 3.444 +/- 0.232 | 7.593 +/- 0.828 | 0.646 +/- 0.072 | - |
| lab | 60 | 0 | 25 | E6/model | 0.000 +/- 0.000 | -3.382 +/- 0.693 | -1.688 +/- 1.053 | 3.780 +/- 0.490 | 7.821 +/- 0.969 | 0.562 +/- 0.125 | - |
| lab | 60 | 0 | 25 | E7/model | 0.000 +/- 0.000 | -3.351 +/- 0.913 | 0.257 +/- 0.745 | 3.361 +/- 0.539 | 7.565 +/- 1.169 | 0.667 +/- 0.095 | 0.587 +/- 0.336 |
| lab | 60 | 0 | 100 | E0 | 0.000 +/- 0.000 | 0.377 +/- 0.071 | -0.252 +/- 0.001 | 0.453 +/- 0.034 | 0.638 +/- 0.023 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 100 | E1/model | 0.000 +/- 0.000 | -1.522 +/- 0.587 | 0.118 +/- 0.156 | 1.526 +/- 0.335 | 5.443 +/- 0.923 | 0.896 +/- 0.036 | - |
| lab | 60 | 0 | 100 | E2/model | 0.000 +/- 0.000 | -2.240 +/- 0.590 | 0.154 +/- 0.115 | 2.245 +/- 0.335 | 6.453 +/- 0.739 | 0.854 +/- 0.036 | - |
| lab | 60 | 0 | 100 | E3/model | 0.000 +/- 0.000 | 0.427 +/- 0.038 | -0.232 +/- 0.033 | 0.486 +/- 0.015 | 0.626 +/- 0.036 | 1.000 +/- 0.000 | 5.165 +/- 1.172 |
| lab | 60 | 0 | 100 | E4/model | 0.000 +/- 0.000 | -0.240 +/- 0.573 | -0.110 +/- 0.121 | 0.264 +/- 0.310 | 2.233 +/- 1.968 | 0.958 +/- 0.036 | 0.102 +/- 0.115 |
| lab | 60 | 0 | 100 | E5/model | 0.000 +/- 0.000 | -0.080 +/- 0.670 | -0.071 +/- 0.068 | 0.107 +/- 0.298 | 1.786 +/- 2.228 | 0.979 +/- 0.036 | - |
| lab | 60 | 0 | 100 | E6/model | 0.000 +/- 0.000 | 0.377 +/- 0.071 | -0.252 +/- 0.001 | 0.453 +/- 0.034 | 0.638 +/- 0.023 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 100 | E7/model | 0.000 +/- 0.000 | 0.348 +/- 0.047 | 0.043 +/- 0.100 | 0.351 +/- 0.020 | 0.554 +/- 0.065 | 1.000 +/- 0.000 | 5.165 +/- 1.172 |
| lab | 60 | 0 | 250 | E0 | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E1/measured3600 | 0.000 +/- 0.000 | 0.008 +/- 0.604 | 0.038 +/- 0.090 | 0.039 +/- 0.099 | 1.717 +/- 2.173 | 0.979 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E1/measured600 | 0.000 +/- 0.000 | -0.361 +/- 0.634 | 0.049 +/- 0.109 | 0.364 +/- 0.369 | 2.973 +/- 2.175 | 0.958 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E1/model | 0.000 +/- 0.000 | -0.348 +/- 0.641 | 0.027 +/- 0.077 | 0.349 +/- 0.372 | 2.979 +/- 2.167 | 0.958 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E1/oracleShape | 0.000 +/- 0.000 | -0.349 +/- 0.643 | 0.028 +/- 0.077 | 0.350 +/- 0.373 | 2.980 +/- 2.166 | 0.958 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E1/scale0.5 | 0.000 +/- 0.000 | -0.348 +/- 0.641 | 0.027 +/- 0.077 | 0.349 +/- 0.372 | 2.979 +/- 2.167 | 0.958 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E1/scale0.75 | 0.000 +/- 0.000 | -0.348 +/- 0.641 | 0.027 +/- 0.077 | 0.349 +/- 0.372 | 2.979 +/- 2.167 | 0.958 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E1/scale0.9 | 0.000 +/- 0.000 | -0.348 +/- 0.641 | 0.027 +/- 0.077 | 0.349 +/- 0.372 | 2.979 +/- 2.167 | 0.958 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E1/scale1.1 | 0.000 +/- 0.000 | -0.348 +/- 0.641 | 0.027 +/- 0.077 | 0.349 +/- 0.372 | 2.979 +/- 2.167 | 0.958 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E1/scale1.25 | 0.000 +/- 0.000 | -0.348 +/- 0.641 | 0.027 +/- 0.077 | 0.349 +/- 0.372 | 2.979 +/- 2.167 | 0.958 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E1/scale1.5 | 0.000 +/- 0.000 | -0.348 +/- 0.641 | 0.027 +/- 0.077 | 0.349 +/- 0.372 | 2.979 +/- 2.167 | 0.958 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E1/wrongFront | 0.000 +/- 0.000 | 0.165 +/- 0.076 | 0.068 +/- 0.045 | 0.178 +/- 0.045 | 0.498 +/- 0.055 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E2/measured3600 | 0.000 +/- 0.000 | -1.081 +/- 0.582 | 0.154 +/- 0.154 | 1.092 +/- 0.339 | 4.878 +/- 1.042 | 0.917 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E2/measured600 | 0.000 +/- 0.000 | -0.360 +/- 0.605 | 0.032 +/- 0.184 | 0.361 +/- 0.355 | 3.045 +/- 2.133 | 0.958 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E2/model | 0.000 +/- 0.000 | -1.081 +/- 0.582 | 0.154 +/- 0.154 | 1.092 +/- 0.339 | 4.878 +/- 1.042 | 0.917 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E2/oracleShape | 0.000 +/- 0.000 | -1.081 +/- 0.582 | 0.154 +/- 0.154 | 1.092 +/- 0.339 | 4.878 +/- 1.042 | 0.917 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E2/scale0.5 | 0.000 +/- 0.000 | -1.081 +/- 0.582 | 0.154 +/- 0.154 | 1.092 +/- 0.339 | 4.878 +/- 1.042 | 0.917 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E2/scale0.75 | 0.000 +/- 0.000 | -1.081 +/- 0.582 | 0.154 +/- 0.154 | 1.092 +/- 0.339 | 4.878 +/- 1.042 | 0.917 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E2/scale0.9 | 0.000 +/- 0.000 | -1.081 +/- 0.582 | 0.154 +/- 0.154 | 1.092 +/- 0.339 | 4.878 +/- 1.042 | 0.917 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E2/scale1.1 | 0.000 +/- 0.000 | -1.081 +/- 0.582 | 0.154 +/- 0.154 | 1.092 +/- 0.339 | 4.878 +/- 1.042 | 0.917 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E2/scale1.25 | 0.000 +/- 0.000 | -1.081 +/- 0.582 | 0.154 +/- 0.154 | 1.092 +/- 0.339 | 4.878 +/- 1.042 | 0.917 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E2/scale1.5 | 0.000 +/- 0.000 | -1.081 +/- 0.582 | 0.154 +/- 0.154 | 1.092 +/- 0.339 | 4.878 +/- 1.042 | 0.917 +/- 0.036 | - |
| lab | 60 | 0 | 250 | E2/wrongFront | 0.000 +/- 0.000 | 0.134 +/- 0.134 | 0.081 +/- 0.074 | 0.157 +/- 0.072 | 0.622 +/- 0.054 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E3/model | 0.000 +/- 0.000 | 0.524 +/- 0.031 | -0.172 +/- 0.040 | 0.551 +/- 0.022 | 0.651 +/- 0.032 | 1.000 +/- 0.000 | 14.084 +/- 0.416 |
| lab | 60 | 0 | 250 | E4/measured3600 | 0.000 +/- 0.000 | 0.301 +/- 0.018 | -0.098 +/- 0.060 | 0.316 +/- 0.014 | 0.442 +/- 0.017 | 1.000 +/- 0.000 | 0.072 +/- 0.060 |
| lab | 60 | 0 | 250 | E4/measured600 | 0.000 +/- 0.000 | 0.301 +/- 0.018 | -0.097 +/- 0.059 | 0.317 +/- 0.014 | 0.442 +/- 0.017 | 1.000 +/- 0.000 | 0.059 +/- 0.040 |
| lab | 60 | 0 | 250 | E4/model | 0.000 +/- 0.000 | 0.301 +/- 0.018 | -0.098 +/- 0.059 | 0.317 +/- 0.014 | 0.443 +/- 0.017 | 1.000 +/- 0.000 | 0.055 +/- 0.039 |
| lab | 60 | 0 | 250 | E4/oracleShape | 0.000 +/- 0.000 | 0.301 +/- 0.018 | -0.098 +/- 0.059 | 0.317 +/- 0.014 | 0.442 +/- 0.017 | 1.000 +/- 0.000 | 0.056 +/- 0.036 |
| lab | 60 | 0 | 250 | E4/scale0.5 | 0.000 +/- 0.000 | 0.301 +/- 0.018 | -0.098 +/- 0.059 | 0.317 +/- 0.014 | 0.443 +/- 0.017 | 1.000 +/- 0.000 | 0.055 +/- 0.039 |
| lab | 60 | 0 | 250 | E4/scale0.75 | 0.000 +/- 0.000 | 0.301 +/- 0.018 | -0.098 +/- 0.059 | 0.317 +/- 0.014 | 0.443 +/- 0.017 | 1.000 +/- 0.000 | 0.055 +/- 0.039 |
| lab | 60 | 0 | 250 | E4/scale0.9 | 0.000 +/- 0.000 | 0.301 +/- 0.018 | -0.098 +/- 0.059 | 0.317 +/- 0.014 | 0.443 +/- 0.017 | 1.000 +/- 0.000 | 0.055 +/- 0.039 |
| lab | 60 | 0 | 250 | E4/scale1.1 | 0.000 +/- 0.000 | 0.301 +/- 0.018 | -0.098 +/- 0.059 | 0.317 +/- 0.014 | 0.443 +/- 0.017 | 1.000 +/- 0.000 | 0.055 +/- 0.039 |
| lab | 60 | 0 | 250 | E4/scale1.25 | 0.000 +/- 0.000 | 0.301 +/- 0.018 | -0.098 +/- 0.059 | 0.317 +/- 0.014 | 0.443 +/- 0.017 | 1.000 +/- 0.000 | 0.055 +/- 0.039 |
| lab | 60 | 0 | 250 | E4/scale1.5 | 0.000 +/- 0.000 | 0.301 +/- 0.018 | -0.098 +/- 0.059 | 0.317 +/- 0.014 | 0.443 +/- 0.017 | 1.000 +/- 0.000 | 0.055 +/- 0.039 |
| lab | 60 | 0 | 250 | E4/wrongFront | 0.000 +/- 0.000 | 0.313 +/- 0.020 | -0.098 +/- 0.062 | 0.328 +/- 0.016 | 0.450 +/- 0.018 | 1.000 +/- 0.000 | 0.877 +/- 0.534 |
| lab | 60 | 0 | 250 | E4@240/model | 0.000 +/- 0.000 | 0.313 +/- 0.038 | -0.059 +/- 0.083 | 0.319 +/- 0.030 | 0.489 +/- 0.029 | 1.000 +/- 0.000 | 0.001 +/- 0.002 |
| lab | 60 | 0 | 250 | E4@400/model | 0.000 +/- 0.000 | 0.310 +/- 0.058 | -0.049 +/- 0.114 | 0.313 +/- 0.031 | 0.510 +/- 0.016 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| lab | 60 | 0 | 250 | E4@60/model | 0.000 +/- 0.000 | 0.290 +/- 0.018 | -0.122 +/- 0.084 | 0.314 +/- 0.022 | 0.417 +/- 0.012 | 1.000 +/- 0.000 | 0.802 +/- 0.155 |
| lab | 60 | 0 | 250 | E5/measured3600 | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5/measured600 | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5/model | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5/oracleShape | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5/scale0.5 | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5/scale0.75 | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5/scale0.9 | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5/scale1.1 | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5/scale1.25 | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5/scale1.5 | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5/wrongFront | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5@240/model | 0.000 +/- 0.000 | 0.317 +/- 0.039 | -0.060 +/- 0.085 | 0.323 +/- 0.031 | 0.491 +/- 0.029 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5@400/model | 0.000 +/- 0.000 | 0.314 +/- 0.062 | -0.046 +/- 0.119 | 0.318 +/- 0.032 | 0.512 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E5@60/model | 0.000 +/- 0.000 | 0.302 +/- 0.017 | -0.109 +/- 0.067 | 0.321 +/- 0.019 | 0.426 +/- 0.009 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E6/measured3600 | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E6/measured600 | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E6/model | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E6/oracleShape | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E6/scale0.5 | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E6/scale0.75 | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E6/scale0.9 | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E6/scale1.1 | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E6/scale1.25 | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E6/scale1.5 | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E6/wrongFront | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 250 | E7/model | 0.000 +/- 0.000 | 0.356 +/- 0.010 | 0.049 +/- 0.048 | 0.359 +/- 0.003 | 0.441 +/- 0.019 | 1.000 +/- 0.000 | 14.084 +/- 0.416 |
| lab | 60 | 0 | 1000 | E0 | 0.000 +/- 0.000 | 0.629 +/- 0.013 | -0.194 +/- 0.000 | 0.658 +/- 0.007 | 0.660 +/- 0.011 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | 0.387 +/- 0.011 | -0.053 +/- 0.052 | 0.391 +/- 0.010 | 0.437 +/- 0.008 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | 0.483 +/- 0.049 | -0.081 +/- 0.122 | 0.489 +/- 0.039 | 0.638 +/- 0.028 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | 0.618 +/- 0.014 | -0.194 +/- 0.000 | 0.648 +/- 0.008 | 0.650 +/- 0.013 | 1.000 +/- 0.000 | 55.121 +/- 4.306 |
| lab | 60 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 0.303 +/- 0.040 | -0.093 +/- 0.092 | 0.317 +/- 0.032 | 0.390 +/- 0.040 | 1.000 +/- 0.000 | 0.863 +/- 0.210 |
| lab | 60 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 0.314 +/- 0.039 | -0.092 +/- 0.092 | 0.327 +/- 0.031 | 0.398 +/- 0.039 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | 0.629 +/- 0.013 | -0.194 +/- 0.000 | 0.658 +/- 0.007 | 0.660 +/- 0.011 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 0.371 +/- 0.018 | 0.069 +/- 0.051 | 0.377 +/- 0.010 | 0.395 +/- 0.021 | 1.000 +/- 0.000 | 55.121 +/- 4.306 |
| lab | 60 | 0.05 | 25 | E0 | 488.396 +/- 0.386 | -6.772 +/- 1.113 | -7.928 +/- 0.153 | 10.427 +/- 0.477 | 11.090 +/- 0.895 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.05 | 25 | E1/model | 488.396 +/- 0.386 | -7.935 +/- 2.131 | 0.408 +/- 0.548 | 7.946 +/- 1.242 | 11.027 +/- 1.571 | 0.104 +/- 0.095 | - |
| lab | 60 | 0.05 | 25 | E2/model | 488.396 +/- 0.386 | -7.708 +/- 1.862 | 0.348 +/- 0.553 | 7.716 +/- 1.087 | 10.844 +/- 1.404 | 0.104 +/- 0.095 | - |
| lab | 60 | 0.05 | 25 | E3/model | 488.396 +/- 0.386 | -7.264 +/- 1.180 | -1.418 +/- 1.474 | 7.401 +/- 0.554 | 10.832 +/- 0.633 | 0.104 +/- 0.036 | 421.997 +/- 5.417 |
| lab | 60 | 0.05 | 25 | E4/model | 488.396 +/- 0.386 | -6.743 +/- 2.745 | -0.175 +/- 1.917 | 6.745 +/- 1.555 | 9.957 +/- 1.784 | 0.104 +/- 0.130 | 386.261 +/- 15.274 |
| lab | 60 | 0.05 | 25 | E5/model | 488.396 +/- 0.386 | -8.141 +/- 2.736 | 1.542 +/- 1.884 | 8.285 +/- 1.754 | 11.026 +/- 1.762 | 0.104 +/- 0.130 | - |
| lab | 60 | 0.05 | 25 | E6/model | 488.396 +/- 0.386 | -8.193 +/- 2.226 | 0.363 +/- 0.605 | 8.201 +/- 1.288 | 11.068 +/- 1.601 | 0.125 +/- 0.062 | - |
| lab | 60 | 0.05 | 25 | E7/model | 488.396 +/- 0.386 | -7.403 +/- 2.111 | 0.762 +/- 1.368 | 7.442 +/- 1.291 | 10.555 +/- 1.217 | 0.104 +/- 0.130 | 421.997 +/- 5.417 |
| lab | 60 | 0.05 | 100 | E0 | 488.396 +/- 0.386 | -6.426 +/- 1.561 | -7.935 +/- 0.167 | 10.211 +/- 0.594 | 11.112 +/- 0.932 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.05 | 100 | E1/model | 488.396 +/- 0.386 | -6.823 +/- 1.007 | 0.636 +/- 0.396 | 6.852 +/- 0.599 | 10.096 +/- 1.430 | 0.354 +/- 0.036 | - |
| lab | 60 | 0.05 | 100 | E2/model | 488.396 +/- 0.386 | -7.546 +/- 1.860 | 0.543 +/- 0.282 | 7.566 +/- 1.081 | 10.597 +/- 2.100 | 0.312 +/- 0.108 | - |
| lab | 60 | 0.05 | 100 | E3/model | 488.396 +/- 0.386 | -5.262 +/- 0.526 | -0.486 +/- 0.560 | 5.284 +/- 0.332 | 8.687 +/- 0.612 | 0.271 +/- 0.036 | 459.374 +/- 12.380 |
| lab | 60 | 0.05 | 100 | E4/model | 488.396 +/- 0.386 | -5.210 +/- 0.473 | 0.117 +/- 0.708 | 5.212 +/- 0.265 | 8.871 +/- 0.631 | 0.333 +/- 0.036 | 394.228 +/- 21.905 |
| lab | 60 | 0.05 | 100 | E5/model | 488.396 +/- 0.386 | -6.374 +/- 1.108 | 1.177 +/- 0.889 | 6.482 +/- 0.596 | 10.068 +/- 0.460 | 0.354 +/- 0.036 | - |
| lab | 60 | 0.05 | 100 | E6/model | 488.396 +/- 0.386 | -4.946 +/- 0.528 | 0.038 +/- 0.190 | 4.946 +/- 0.306 | 8.435 +/- 1.184 | 0.292 +/- 0.130 | - |
| lab | 60 | 0.05 | 100 | E7/model | 488.396 +/- 0.386 | -4.852 +/- 0.760 | 0.602 +/- 0.406 | 4.890 +/- 0.449 | 8.967 +/- 0.398 | 0.458 +/- 0.072 | 459.374 +/- 12.380 |
| lab | 60 | 0.05 | 250 | E0 | 488.396 +/- 0.386 | -7.821 +/- 0.597 | -8.081 +/- 0.207 | 11.247 +/- 0.254 | 12.058 +/- 0.727 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.05 | 250 | E1/model | 488.396 +/- 0.386 | -2.472 +/- 0.501 | -0.063 +/- 0.507 | 2.473 +/- 0.296 | 6.824 +/- 0.642 | 0.812 +/- 0.000 | - |
| lab | 60 | 0.05 | 250 | E2/model | 488.396 +/- 0.386 | -3.082 +/- 0.694 | -0.186 +/- 0.432 | 3.088 +/- 0.390 | 7.584 +/- 0.826 | 0.771 +/- 0.072 | - |
| lab | 60 | 0.05 | 250 | E3/model | 488.396 +/- 0.386 | -0.125 +/- 0.490 | -0.111 +/- 0.193 | 0.167 +/- 0.284 | 3.045 +/- 1.437 | 0.938 +/- 0.000 | 480.614 +/- 13.484 |
| lab | 60 | 0.05 | 250 | E4/model | 488.396 +/- 0.386 | 0.001 +/- 0.517 | -0.116 +/- 0.030 | 0.116 +/- 0.016 | 1.490 +/- 1.701 | 0.979 +/- 0.036 | 377.277 +/- 19.438 |
| lab | 60 | 0.05 | 250 | E5/model | 488.396 +/- 0.386 | 0.018 +/- 0.522 | -0.117 +/- 0.035 | 0.118 +/- 0.035 | 1.511 +/- 1.698 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.05 | 250 | E6/model | 488.396 +/- 0.386 | -0.157 +/- 0.514 | -0.215 +/- 0.351 | 0.267 +/- 0.329 | 3.273 +/- 1.740 | 0.938 +/- 0.000 | - |
| lab | 60 | 0.05 | 250 | E7/model | 488.396 +/- 0.386 | 0.039 +/- 0.542 | -0.018 +/- 0.069 | 0.043 +/- 0.268 | 1.594 +/- 1.709 | 0.979 +/- 0.036 | 480.614 +/- 13.484 |
| lab | 60 | 0.05 | 1000 | E0 | 488.396 +/- 0.386 | 0.681 +/- 0.004 | -0.203 +/- 0.015 | 0.710 +/- 0.004 | 0.714 +/- 0.012 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.05 | 1000 | E1/model | 488.396 +/- 0.386 | 0.398 +/- 0.012 | -0.051 +/- 0.074 | 0.402 +/- 0.011 | 0.449 +/- 0.011 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.05 | 1000 | E2/model | 488.396 +/- 0.386 | 0.466 +/- 0.014 | -0.113 +/- 0.140 | 0.480 +/- 0.024 | 0.629 +/- 0.008 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.05 | 1000 | E3/model | 488.396 +/- 0.386 | 0.607 +/- 0.009 | -0.194 +/- 0.000 | 0.638 +/- 0.005 | 0.641 +/- 0.008 | 1.000 +/- 0.000 | 500.551 +/- 9.882 |
| lab | 60 | 0.05 | 1000 | E4/model | 488.396 +/- 0.386 | 0.305 +/- 0.037 | -0.121 +/- 0.071 | 0.328 +/- 0.027 | 0.386 +/- 0.029 | 1.000 +/- 0.000 | 327.515 +/- 12.002 |
| lab | 60 | 0.05 | 1000 | E5/model | 488.396 +/- 0.386 | 0.310 +/- 0.034 | -0.127 +/- 0.077 | 0.335 +/- 0.025 | 0.397 +/- 0.028 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.05 | 1000 | E6/model | 488.396 +/- 0.386 | 0.610 +/- 0.007 | -0.194 +/- 0.000 | 0.640 +/- 0.004 | 0.643 +/- 0.007 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.05 | 1000 | E7/model | 488.396 +/- 0.386 | 0.380 +/- 0.053 | 0.035 +/- 0.036 | 0.382 +/- 0.030 | 0.412 +/- 0.047 | 1.000 +/- 0.000 | 500.551 +/- 9.882 |
| lab | 60 | 0.1 | 25 | E0 | 976.792 +/- 0.772 | -7.087 +/- 0.644 | -8.164 +/- 0.116 | 10.811 +/- 0.199 | 11.474 +/- 0.088 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 25 | E1/model | 976.792 +/- 0.772 | -8.057 +/- 0.878 | 0.850 +/- 1.530 | 8.102 +/- 0.568 | 11.071 +/- 0.577 | 0.062 +/- 0.062 | - |
| lab | 60 | 0.1 | 25 | E2/model | 976.792 +/- 0.772 | -8.130 +/- 0.858 | 0.997 +/- 1.357 | 8.190 +/- 0.562 | 11.125 +/- 0.587 | 0.062 +/- 0.062 | - |
| lab | 60 | 0.1 | 25 | E3/model | 976.792 +/- 0.772 | -7.624 +/- 1.359 | -3.886 +/- 0.836 | 8.557 +/- 0.869 | 11.168 +/- 1.338 | 0.021 +/- 0.036 | 873.338 +/- 13.276 |
| lab | 60 | 0.1 | 25 | E4/model | 976.792 +/- 0.772 | -6.139 +/- 1.617 | -1.704 +/- 1.057 | 6.371 +/- 1.026 | 9.646 +/- 1.368 | 0.104 +/- 0.036 | 857.937 +/- 10.823 |
| lab | 60 | 0.1 | 25 | E5/model | 976.792 +/- 0.772 | -7.089 +/- 1.151 | -0.229 +/- 1.685 | 7.093 +/- 0.669 | 10.436 +/- 1.160 | 0.125 +/- 0.000 | - |
| lab | 60 | 0.1 | 25 | E6/model | 976.792 +/- 0.772 | -7.594 +/- 1.102 | 0.817 +/- 1.275 | 7.638 +/- 0.636 | 10.711 +/- 0.989 | 0.083 +/- 0.095 | - |
| lab | 60 | 0.1 | 25 | E7/model | 976.792 +/- 0.772 | -7.524 +/- 0.480 | -0.179 +/- 2.814 | 7.526 +/- 0.239 | 10.807 +/- 0.244 | 0.104 +/- 0.036 | 873.338 +/- 13.276 |
| lab | 60 | 0.1 | 100 | E0 | 976.792 +/- 0.772 | -6.351 +/- 1.081 | -8.197 +/- 0.228 | 10.369 +/- 0.442 | 11.087 +/- 0.666 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 100 | E1/model | 976.792 +/- 0.772 | -7.235 +/- 1.124 | 0.066 +/- 1.360 | 7.235 +/- 0.643 | 10.943 +/- 0.987 | 0.250 +/- 0.062 | - |
| lab | 60 | 0.1 | 100 | E2/model | 976.792 +/- 0.772 | -7.263 +/- 1.114 | 0.024 +/- 1.368 | 7.263 +/- 0.641 | 10.951 +/- 0.968 | 0.250 +/- 0.062 | - |
| lab | 60 | 0.1 | 100 | E3/model | 976.792 +/- 0.772 | -5.228 +/- 1.479 | -3.467 +/- 0.714 | 6.273 +/- 0.692 | 9.343 +/- 1.502 | 0.208 +/- 0.036 | 913.722 +/- 6.147 |
| lab | 60 | 0.1 | 100 | E4/model | 976.792 +/- 0.772 | -4.630 +/- 0.485 | -1.167 +/- 2.154 | 4.775 +/- 0.451 | 8.835 +/- 0.856 | 0.312 +/- 0.125 | 854.600 +/- 20.960 |
| lab | 60 | 0.1 | 100 | E5/model | 976.792 +/- 0.772 | -3.637 +/- 1.283 | -0.148 +/- 1.118 | 3.640 +/- 0.766 | 7.991 +/- 1.519 | 0.417 +/- 0.130 | - |
| lab | 60 | 0.1 | 100 | E6/model | 976.792 +/- 0.772 | -6.033 +/- 1.505 | -1.994 +/- 1.814 | 6.354 +/- 1.109 | 10.077 +/- 1.576 | 0.250 +/- 0.000 | - |
| lab | 60 | 0.1 | 100 | E7/model | 976.792 +/- 0.772 | -3.649 +/- 0.082 | -0.448 +/- 1.048 | 3.676 +/- 0.110 | 7.843 +/- 0.430 | 0.417 +/- 0.072 | 913.722 +/- 6.147 |
| lab | 60 | 0.1 | 250 | E0 | 976.792 +/- 0.772 | -6.232 +/- 0.396 | -8.177 +/- 0.131 | 10.281 +/- 0.194 | 10.906 +/- 0.389 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 250 | E1/measured3600 | 976.792 +/- 0.772 | -6.170 +/- 1.898 | -0.098 +/- 0.407 | 6.170 +/- 1.096 | 10.215 +/- 1.462 | 0.604 +/- 0.095 | - |
| lab | 60 | 0.1 | 250 | E1/measured600 | 976.792 +/- 0.772 | -4.257 +/- 3.396 | 0.353 +/- 0.403 | 4.272 +/- 1.944 | 8.000 +/- 3.150 | 0.708 +/- 0.191 | - |
| lab | 60 | 0.1 | 250 | E1/model | 976.792 +/- 0.772 | -6.481 +/- 1.804 | -0.077 +/- 0.168 | 6.482 +/- 1.042 | 10.510 +/- 1.395 | 0.604 +/- 0.095 | - |
| lab | 60 | 0.1 | 250 | E1/oracleShape | 976.792 +/- 0.772 | -6.140 +/- 1.943 | -0.017 +/- 0.291 | 6.140 +/- 1.122 | 10.188 +/- 1.496 | 0.625 +/- 0.108 | - |
| lab | 60 | 0.1 | 250 | E1/scale0.5 | 976.792 +/- 0.772 | -6.481 +/- 1.804 | -0.077 +/- 0.168 | 6.482 +/- 1.042 | 10.510 +/- 1.395 | 0.604 +/- 0.095 | - |
| lab | 60 | 0.1 | 250 | E1/scale0.75 | 976.792 +/- 0.772 | -6.481 +/- 1.804 | -0.077 +/- 0.168 | 6.482 +/- 1.042 | 10.510 +/- 1.395 | 0.604 +/- 0.095 | - |
| lab | 60 | 0.1 | 250 | E1/scale0.9 | 976.792 +/- 0.772 | -6.481 +/- 1.804 | -0.077 +/- 0.168 | 6.482 +/- 1.042 | 10.510 +/- 1.395 | 0.604 +/- 0.095 | - |
| lab | 60 | 0.1 | 250 | E1/scale1.1 | 976.792 +/- 0.772 | -6.481 +/- 1.804 | -0.077 +/- 0.168 | 6.482 +/- 1.042 | 10.510 +/- 1.395 | 0.604 +/- 0.095 | - |
| lab | 60 | 0.1 | 250 | E1/scale1.25 | 976.792 +/- 0.772 | -6.481 +/- 1.804 | -0.077 +/- 0.168 | 6.482 +/- 1.042 | 10.510 +/- 1.395 | 0.604 +/- 0.095 | - |
| lab | 60 | 0.1 | 250 | E1/scale1.5 | 976.792 +/- 0.772 | -6.481 +/- 1.804 | -0.077 +/- 0.168 | 6.482 +/- 1.042 | 10.510 +/- 1.395 | 0.604 +/- 0.095 | - |
| lab | 60 | 0.1 | 250 | E1/wrongFront | 976.792 +/- 0.772 | -6.283 +/- 0.850 | -8.409 +/- 0.077 | 10.497 +/- 0.329 | 11.230 +/- 0.726 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 250 | E2/measured3600 | 976.792 +/- 0.772 | -7.206 +/- 1.190 | -0.332 +/- 0.511 | 7.214 +/- 0.679 | 11.155 +/- 0.828 | 0.542 +/- 0.036 | - |
| lab | 60 | 0.1 | 250 | E2/measured600 | 976.792 +/- 0.772 | -5.237 +/- 3.272 | 0.292 +/- 0.413 | 5.245 +/- 1.879 | 9.055 +/- 2.620 | 0.646 +/- 0.191 | - |
| lab | 60 | 0.1 | 250 | E2/model | 976.792 +/- 0.772 | -6.801 +/- 1.337 | -0.154 +/- 0.234 | 6.803 +/- 0.774 | 10.813 +/- 0.937 | 0.583 +/- 0.072 | - |
| lab | 60 | 0.1 | 250 | E2/oracleShape | 976.792 +/- 0.772 | -7.157 +/- 1.042 | -0.292 +/- 0.368 | 7.163 +/- 0.605 | 11.140 +/- 0.663 | 0.562 +/- 0.062 | - |
| lab | 60 | 0.1 | 250 | E2/scale0.5 | 976.792 +/- 0.772 | -6.801 +/- 1.337 | -0.154 +/- 0.234 | 6.803 +/- 0.774 | 10.813 +/- 0.937 | 0.583 +/- 0.072 | - |
| lab | 60 | 0.1 | 250 | E2/scale0.75 | 976.792 +/- 0.772 | -6.801 +/- 1.337 | -0.154 +/- 0.234 | 6.803 +/- 0.774 | 10.813 +/- 0.937 | 0.583 +/- 0.072 | - |
| lab | 60 | 0.1 | 250 | E2/scale0.9 | 976.792 +/- 0.772 | -6.801 +/- 1.337 | -0.154 +/- 0.234 | 6.803 +/- 0.774 | 10.813 +/- 0.937 | 0.583 +/- 0.072 | - |
| lab | 60 | 0.1 | 250 | E2/scale1.1 | 976.792 +/- 0.772 | -6.801 +/- 1.337 | -0.154 +/- 0.234 | 6.803 +/- 0.774 | 10.813 +/- 0.937 | 0.583 +/- 0.072 | - |
| lab | 60 | 0.1 | 250 | E2/scale1.25 | 976.792 +/- 0.772 | -6.801 +/- 1.337 | -0.154 +/- 0.234 | 6.803 +/- 0.774 | 10.813 +/- 0.937 | 0.583 +/- 0.072 | - |
| lab | 60 | 0.1 | 250 | E2/scale1.5 | 976.792 +/- 0.772 | -6.801 +/- 1.337 | -0.154 +/- 0.234 | 6.803 +/- 0.774 | 10.813 +/- 0.937 | 0.583 +/- 0.072 | - |
| lab | 60 | 0.1 | 250 | E2/wrongFront | 976.792 +/- 0.772 | -6.396 +/- 0.876 | -8.450 +/- 0.078 | 10.598 +/- 0.328 | 11.324 +/- 0.722 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 250 | E3/model | 976.792 +/- 0.772 | -1.466 +/- 0.740 | -0.788 +/- 0.713 | 1.664 +/- 0.538 | 5.392 +/- 1.470 | 0.792 +/- 0.036 | 959.693 +/- 6.518 |
| lab | 60 | 0.1 | 250 | E4/measured3600 | 976.792 +/- 0.772 | -0.264 +/- 0.704 | -0.217 +/- 0.549 | 0.342 +/- 0.507 | 2.018 +/- 2.253 | 0.938 +/- 0.108 | 828.632 +/- 7.764 |
| lab | 60 | 0.1 | 250 | E4/measured600 | 976.792 +/- 0.772 | -0.783 +/- 0.608 | -0.426 +/- 0.268 | 0.891 +/- 0.371 | 3.610 +/- 1.077 | 0.896 +/- 0.036 | 801.630 +/- 9.920 |
| lab | 60 | 0.1 | 250 | E4/model | 976.792 +/- 0.772 | -0.122 +/- 0.457 | -0.067 +/- 0.257 | 0.139 +/- 0.283 | 1.718 +/- 1.780 | 0.958 +/- 0.072 | 829.066 +/- 4.737 |
| lab | 60 | 0.1 | 250 | E4/oracleShape | 976.792 +/- 0.772 | -0.429 +/- 0.521 | -0.156 +/- 0.337 | 0.457 +/- 0.347 | 2.597 +/- 1.754 | 0.938 +/- 0.062 | 831.474 +/- 7.215 |
| lab | 60 | 0.1 | 250 | E4/scale0.5 | 976.792 +/- 0.772 | -0.122 +/- 0.457 | -0.067 +/- 0.257 | 0.139 +/- 0.283 | 1.718 +/- 1.780 | 0.958 +/- 0.072 | 829.066 +/- 4.737 |
| lab | 60 | 0.1 | 250 | E4/scale0.75 | 976.792 +/- 0.772 | -0.122 +/- 0.457 | -0.067 +/- 0.257 | 0.139 +/- 0.283 | 1.718 +/- 1.780 | 0.958 +/- 0.072 | 829.066 +/- 4.737 |
| lab | 60 | 0.1 | 250 | E4/scale0.9 | 976.792 +/- 0.772 | -0.122 +/- 0.457 | -0.067 +/- 0.257 | 0.139 +/- 0.283 | 1.718 +/- 1.780 | 0.958 +/- 0.072 | 829.066 +/- 4.737 |
| lab | 60 | 0.1 | 250 | E4/scale1.1 | 976.792 +/- 0.772 | -0.122 +/- 0.457 | -0.067 +/- 0.257 | 0.139 +/- 0.283 | 1.718 +/- 1.780 | 0.958 +/- 0.072 | 829.066 +/- 4.737 |
| lab | 60 | 0.1 | 250 | E4/scale1.25 | 976.792 +/- 0.772 | -0.122 +/- 0.457 | -0.067 +/- 0.257 | 0.139 +/- 0.283 | 1.718 +/- 1.780 | 0.958 +/- 0.072 | 829.066 +/- 4.737 |
| lab | 60 | 0.1 | 250 | E4/scale1.5 | 976.792 +/- 0.772 | -0.122 +/- 0.457 | -0.067 +/- 0.257 | 0.139 +/- 0.283 | 1.718 +/- 1.780 | 0.958 +/- 0.072 | 829.066 +/- 4.737 |
| lab | 60 | 0.1 | 250 | E4/wrongFront | 976.792 +/- 0.772 | -3.400 +/- 1.135 | -5.873 +/- 0.619 | 6.786 +/- 0.547 | 8.947 +/- 0.638 | 0.271 +/- 0.072 | 0.284 +/- 0.086 |
| lab | 60 | 0.1 | 250 | E4@240/model | 976.792 +/- 0.772 | -0.804 +/- 0.870 | -0.502 +/- 0.655 | 0.947 +/- 0.623 | 3.548 +/- 2.552 | 0.896 +/- 0.095 | 815.140 +/- 8.521 |
| lab | 60 | 0.1 | 250 | E4@400/model | 976.792 +/- 0.772 | -0.781 +/- 0.857 | -0.646 +/- 0.371 | 1.014 +/- 0.517 | 4.016 +/- 1.754 | 0.875 +/- 0.062 | 804.672 +/- 10.404 |
| lab | 60 | 0.1 | 250 | E4@60/model | 976.792 +/- 0.772 | -0.136 +/- 0.461 | -0.074 +/- 0.244 | 0.155 +/- 0.284 | 1.711 +/- 1.786 | 0.958 +/- 0.072 | 851.226 +/- 4.514 |
| lab | 60 | 0.1 | 250 | E5/measured3600 | 976.792 +/- 0.772 | -0.731 +/- 1.564 | -0.122 +/- 0.420 | 0.741 +/- 0.925 | 2.799 +/- 3.583 | 0.938 +/- 0.108 | - |
| lab | 60 | 0.1 | 250 | E5/measured600 | 976.792 +/- 0.772 | -1.043 +/- 0.962 | -0.201 +/- 0.444 | 1.062 +/- 0.577 | 3.735 +/- 2.869 | 0.917 +/- 0.072 | - |
| lab | 60 | 0.1 | 250 | E5/model | 976.792 +/- 0.772 | -0.719 +/- 1.543 | -0.175 +/- 0.421 | 0.740 +/- 0.917 | 2.782 +/- 3.596 | 0.938 +/- 0.108 | - |
| lab | 60 | 0.1 | 250 | E5/oracleShape | 976.792 +/- 0.772 | -0.734 +/- 1.544 | -0.185 +/- 0.366 | 0.757 +/- 0.911 | 2.772 +/- 3.602 | 0.938 +/- 0.108 | - |
| lab | 60 | 0.1 | 250 | E5/scale0.5 | 976.792 +/- 0.772 | -2.300 +/- 1.230 | -2.624 +/- 1.529 | 3.490 +/- 1.128 | 6.558 +/- 1.436 | 0.646 +/- 0.237 | - |
| lab | 60 | 0.1 | 250 | E5/scale0.75 | 976.792 +/- 0.772 | -0.905 +/- 0.199 | -0.773 +/- 0.111 | 1.190 +/- 0.129 | 4.489 +/- 0.297 | 0.875 +/- 0.062 | - |
| lab | 60 | 0.1 | 250 | E5/scale0.9 | 976.792 +/- 0.772 | -0.494 +/- 1.110 | -0.067 +/- 0.259 | 0.498 +/- 0.648 | 2.375 +/- 2.917 | 0.938 +/- 0.108 | - |
| lab | 60 | 0.1 | 250 | E5/scale1.1 | 976.792 +/- 0.772 | -0.693 +/- 1.549 | -0.158 +/- 0.400 | 0.711 +/- 0.917 | 2.786 +/- 3.576 | 0.938 +/- 0.108 | - |
| lab | 60 | 0.1 | 250 | E5/scale1.25 | 976.792 +/- 0.772 | -2.573 +/- 1.744 | -0.211 +/- 0.251 | 2.581 +/- 1.009 | 6.753 +/- 2.185 | 0.833 +/- 0.095 | - |
| lab | 60 | 0.1 | 250 | E5/scale1.5 | 976.792 +/- 0.772 | -5.032 +/- 1.909 | -0.144 +/- 0.589 | 5.034 +/- 1.111 | 9.476 +/- 1.643 | 0.688 +/- 0.108 | - |
| lab | 60 | 0.1 | 250 | E5/wrongFront | 976.792 +/- 0.772 | -8.046 +/- 0.621 | -8.288 +/- 0.021 | 11.551 +/- 0.241 | 12.245 +/- 0.210 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 250 | E5@240/model | 976.792 +/- 0.772 | -0.777 +/- 0.937 | -0.158 +/- 0.306 | 0.793 +/- 0.552 | 3.263 +/- 2.576 | 0.917 +/- 0.095 | - |
| lab | 60 | 0.1 | 250 | E5@400/model | 976.792 +/- 0.772 | -1.101 +/- 1.077 | -0.221 +/- 0.595 | 1.123 +/- 0.643 | 3.975 +/- 2.923 | 0.875 +/- 0.125 | - |
| lab | 60 | 0.1 | 250 | E5@60/model | 976.792 +/- 0.772 | -0.726 +/- 1.547 | -0.181 +/- 0.413 | 0.748 +/- 0.918 | 2.781 +/- 3.600 | 0.938 +/- 0.108 | - |
| lab | 60 | 0.1 | 250 | E6/measured3600 | 976.792 +/- 0.772 | -1.984 +/- 0.745 | -1.156 +/- 1.219 | 2.296 +/- 0.595 | 6.305 +/- 1.675 | 0.750 +/- 0.062 | - |
| lab | 60 | 0.1 | 250 | E6/measured600 | 976.792 +/- 0.772 | -1.567 +/- 0.403 | -0.752 +/- 1.080 | 1.738 +/- 0.471 | 5.607 +/- 1.297 | 0.812 +/- 0.062 | - |
| lab | 60 | 0.1 | 250 | E6/model | 976.792 +/- 0.772 | -1.960 +/- 0.701 | -1.516 +/- 1.276 | 2.478 +/- 0.711 | 6.297 +/- 1.660 | 0.750 +/- 0.062 | - |
| lab | 60 | 0.1 | 250 | E6/oracleShape | 976.792 +/- 0.772 | -1.964 +/- 0.700 | -1.532 +/- 1.297 | 2.491 +/- 0.717 | 6.300 +/- 1.664 | 0.750 +/- 0.062 | - |
| lab | 60 | 0.1 | 250 | E6/scale0.5 | 976.792 +/- 0.772 | -5.869 +/- 0.508 | -8.014 +/- 0.097 | 9.933 +/- 0.209 | 10.966 +/- 0.378 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 250 | E6/scale0.75 | 976.792 +/- 0.772 | -4.494 +/- 0.431 | -6.088 +/- 0.099 | 7.567 +/- 0.194 | 9.764 +/- 0.339 | 0.250 +/- 0.000 | - |
| lab | 60 | 0.1 | 250 | E6/scale0.9 | 976.792 +/- 0.772 | -2.895 +/- 0.685 | -3.141 +/- 1.456 | 4.271 +/- 0.645 | 7.901 +/- 0.451 | 0.583 +/- 0.095 | - |
| lab | 60 | 0.1 | 250 | E6/scale1.1 | 976.792 +/- 0.772 | -1.579 +/- 0.441 | -0.072 +/- 0.329 | 1.581 +/- 0.262 | 5.413 +/- 0.980 | 0.854 +/- 0.036 | - |
| lab | 60 | 0.1 | 250 | E6/scale1.25 | 976.792 +/- 0.772 | -6.120 +/- 2.888 | -0.075 +/- 0.460 | 6.121 +/- 1.669 | 10.065 +/- 2.266 | 0.625 +/- 0.165 | - |
| lab | 60 | 0.1 | 250 | E6/scale1.5 | 976.792 +/- 0.772 | -14.468 +/- 0.979 | 0.088 +/- 0.666 | 14.469 +/- 0.563 | 15.726 +/- 0.489 | 0.125 +/- 0.062 | - |
| lab | 60 | 0.1 | 250 | E6/wrongFront | 976.792 +/- 0.772 | -6.604 +/- 0.540 | -8.450 +/- 0.051 | 10.725 +/- 0.211 | 11.353 +/- 0.357 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 250 | E7/model | 976.792 +/- 0.772 | -0.708 +/- 1.588 | -0.102 +/- 0.364 | 0.716 +/- 0.933 | 2.781 +/- 3.583 | 0.938 +/- 0.108 | 959.693 +/- 6.518 |
| lab | 60 | 0.1 | 1000 | E0 | 976.792 +/- 0.772 | -3.334 +/- 1.326 | -5.464 +/- 0.963 | 6.401 +/- 0.874 | 8.196 +/- 1.099 | 0.354 +/- 0.130 | - |
| lab | 60 | 0.1 | 1000 | E1/model | 976.792 +/- 0.772 | 0.047 +/- 0.614 | -0.052 +/- 0.039 | 0.070 +/- 0.249 | 1.713 +/- 2.181 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.1 | 1000 | E2/model | 976.792 +/- 0.772 | -0.603 +/- 0.037 | -0.113 +/- 0.074 | 0.614 +/- 0.029 | 4.282 +/- 0.003 | 0.938 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E3/model | 976.792 +/- 0.772 | 0.614 +/- 0.027 | -0.194 +/- 0.000 | 0.645 +/- 0.015 | 0.649 +/- 0.026 | 1.000 +/- 0.000 | 968.402 +/- 15.682 |
| lab | 60 | 0.1 | 1000 | E4/model | 976.792 +/- 0.772 | 0.323 +/- 0.025 | -0.134 +/- 0.056 | 0.350 +/- 0.003 | 0.406 +/- 0.023 | 1.000 +/- 0.000 | 765.896 +/- 6.765 |
| lab | 60 | 0.1 | 1000 | E5/model | 976.792 +/- 0.772 | 0.329 +/- 0.026 | -0.139 +/- 0.059 | 0.357 +/- 0.005 | 0.417 +/- 0.024 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/model | 976.792 +/- 0.772 | 0.613 +/- 0.030 | -0.194 +/- 0.000 | 0.643 +/- 0.017 | 0.648 +/- 0.029 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E7/model | 976.792 +/- 0.772 | 0.400 +/- 0.020 | 0.044 +/- 0.023 | 0.403 +/- 0.013 | 0.436 +/- 0.022 | 1.000 +/- 0.000 | 968.402 +/- 15.682 |
| lab | 60 | 0.2 | 25 | E0 | 1953.584 +/- 1.544 | -6.881 +/- 0.599 | -8.175 +/- 0.124 | 10.685 +/- 0.168 | 11.252 +/- 0.340 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 25 | E1/model | 1953.584 +/- 1.544 | -7.838 +/- 0.762 | -1.535 +/- 0.975 | 7.987 +/- 0.454 | 10.689 +/- 0.856 | 0.083 +/- 0.095 | - |
| lab | 60 | 0.2 | 25 | E2/model | 1953.584 +/- 1.544 | -8.883 +/- 0.778 | -1.499 +/- 1.463 | 9.009 +/- 0.493 | 11.624 +/- 0.702 | 0.062 +/- 0.108 | - |
| lab | 60 | 0.2 | 25 | E3/model | 1953.584 +/- 1.544 | -8.272 +/- 1.694 | -5.623 +/- 0.637 | 10.002 +/- 0.872 | 11.596 +/- 1.322 | 0.021 +/- 0.036 | 1762.427 +/- 20.128 |
| lab | 60 | 0.2 | 25 | E4/model | 1953.584 +/- 1.544 | -6.206 +/- 0.463 | -3.040 +/- 1.756 | 6.911 +/- 0.566 | 9.630 +/- 0.293 | 0.062 +/- 0.062 | 1771.135 +/- 34.164 |
| lab | 60 | 0.2 | 25 | E5/model | 1953.584 +/- 1.544 | -7.562 +/- 1.190 | -1.359 +/- 0.253 | 7.683 +/- 0.650 | 10.616 +/- 1.029 | 0.062 +/- 0.062 | - |
| lab | 60 | 0.2 | 25 | E6/model | 1953.584 +/- 1.544 | -8.517 +/- 1.043 | -1.816 +/- 0.745 | 8.709 +/- 0.676 | 11.420 +/- 0.916 | 0.062 +/- 0.108 | - |
| lab | 60 | 0.2 | 25 | E7/model | 1953.584 +/- 1.544 | -7.731 +/- 1.483 | -0.858 +/- 0.356 | 7.778 +/- 0.861 | 10.762 +/- 1.278 | 0.062 +/- 0.062 | 1762.427 +/- 20.128 |
| lab | 60 | 0.2 | 100 | E0 | 1953.584 +/- 1.544 | -7.606 +/- 0.743 | -8.219 +/- 0.238 | 11.198 +/- 0.383 | 11.775 +/- 0.541 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 100 | E1/model | 1953.584 +/- 1.544 | -8.067 +/- 1.615 | 1.175 +/- 1.453 | 8.152 +/- 0.952 | 11.243 +/- 1.022 | 0.208 +/- 0.144 | - |
| lab | 60 | 0.2 | 100 | E2/model | 1953.584 +/- 1.544 | -8.275 +/- 1.945 | 1.037 +/- 1.310 | 8.340 +/- 1.136 | 11.417 +/- 1.290 | 0.229 +/- 0.130 | - |
| lab | 60 | 0.2 | 100 | E3/model | 1953.584 +/- 1.544 | -6.872 +/- 0.656 | -3.097 +/- 1.398 | 7.538 +/- 0.089 | 10.292 +/- 0.571 | 0.104 +/- 0.072 | 1849.065 +/- 19.611 |
| lab | 60 | 0.2 | 100 | E4/model | 1953.584 +/- 1.544 | -5.444 +/- 2.155 | -1.546 +/- 0.326 | 5.659 +/- 1.166 | 9.329 +/- 1.774 | 0.188 +/- 0.188 | 1831.701 +/- 25.148 |
| lab | 60 | 0.2 | 100 | E5/model | 1953.584 +/- 1.544 | -6.609 +/- 2.875 | 0.063 +/- 1.552 | 6.609 +/- 1.661 | 10.119 +/- 2.150 | 0.250 +/- 0.217 | - |
| lab | 60 | 0.2 | 100 | E6/model | 1953.584 +/- 1.544 | -7.046 +/- 1.455 | -0.586 +/- 0.997 | 7.071 +/- 0.809 | 10.544 +/- 0.640 | 0.146 +/- 0.095 | - |
| lab | 60 | 0.2 | 100 | E7/model | 1953.584 +/- 1.544 | -6.431 +/- 3.317 | 0.070 +/- 0.772 | 6.431 +/- 1.919 | 10.010 +/- 2.637 | 0.292 +/- 0.237 | 1849.065 +/- 19.611 |
| lab | 60 | 0.2 | 250 | E0 | 1953.584 +/- 1.544 | -7.347 +/- 0.354 | -8.129 +/- 0.274 | 10.957 +/- 0.189 | 11.537 +/- 0.328 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 250 | E1/model | 1953.584 +/- 1.544 | -6.403 +/- 0.829 | 0.258 +/- 0.727 | 6.408 +/- 0.495 | 10.529 +/- 0.859 | 0.542 +/- 0.072 | - |
| lab | 60 | 0.2 | 250 | E2/model | 1953.584 +/- 1.544 | -6.760 +/- 0.512 | 0.203 +/- 0.745 | 6.763 +/- 0.305 | 10.801 +/- 0.550 | 0.521 +/- 0.036 | - |
| lab | 60 | 0.2 | 250 | E3/model | 1953.584 +/- 1.544 | -3.032 +/- 0.332 | -1.680 +/- 0.269 | 3.466 +/- 0.226 | 6.977 +/- 0.585 | 0.562 +/- 0.108 | 1877.558 +/- 14.878 |
| lab | 60 | 0.2 | 250 | E4/model | 1953.584 +/- 1.544 | -2.632 +/- 0.593 | -0.466 +/- 0.488 | 2.673 +/- 0.349 | 6.622 +/- 0.582 | 0.688 +/- 0.108 | 1753.525 +/- 26.697 |
| lab | 60 | 0.2 | 250 | E5/model | 1953.584 +/- 1.544 | -3.031 +/- 0.688 | -0.242 +/- 0.261 | 3.040 +/- 0.387 | 6.995 +/- 1.362 | 0.729 +/- 0.036 | - |
| lab | 60 | 0.2 | 250 | E6/model | 1953.584 +/- 1.544 | -3.781 +/- 0.964 | -1.291 +/- 0.745 | 3.995 +/- 0.528 | 7.913 +/- 1.305 | 0.542 +/- 0.095 | - |
| lab | 60 | 0.2 | 250 | E7/model | 1953.584 +/- 1.544 | -2.803 +/- 0.509 | -0.239 +/- 0.167 | 2.813 +/- 0.288 | 6.868 +/- 1.241 | 0.750 +/- 0.000 | 1877.558 +/- 14.878 |
| lab | 60 | 0.2 | 1000 | E0 | 1953.584 +/- 1.544 | -5.926 +/- 0.116 | -8.265 +/- 0.263 | 10.170 +/- 0.162 | 10.358 +/- 0.284 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E1/model | 1953.584 +/- 1.544 | 0.029 +/- 0.619 | -0.032 +/- 0.035 | 0.043 +/- 0.253 | 1.717 +/- 2.192 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E2/model | 1953.584 +/- 1.544 | -0.295 +/- 0.569 | -0.065 +/- 0.056 | 0.302 +/- 0.318 | 3.042 +/- 2.145 | 0.958 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E3/model | 1953.584 +/- 1.544 | 0.596 +/- 0.023 | -0.194 +/- 0.000 | 0.627 +/- 0.013 | 0.632 +/- 0.021 | 1.000 +/- 0.000 | 1935.574 +/- 7.694 |
| lab | 60 | 0.2 | 1000 | E4/model | 1953.584 +/- 1.544 | 0.305 +/- 0.027 | -0.129 +/- 0.052 | 0.331 +/- 0.026 | 0.395 +/- 0.045 | 1.000 +/- 0.000 | 1690.982 +/- 5.602 |
| lab | 60 | 0.2 | 1000 | E5/model | 1953.584 +/- 1.544 | 0.312 +/- 0.032 | -0.135 +/- 0.057 | 0.340 +/- 0.029 | 0.408 +/- 0.051 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E6/model | 1953.584 +/- 1.544 | 0.594 +/- 0.024 | -0.194 +/- 0.000 | 0.625 +/- 0.013 | 0.630 +/- 0.021 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E7/model | 1953.584 +/- 1.544 | 0.382 +/- 0.036 | 0.024 +/- 0.058 | 0.383 +/- 0.019 | 0.429 +/- 0.040 | 1.000 +/- 0.000 | 1935.574 +/- 7.694 |

### 10 s core

All tables: mean +/- sample SD of three outer-seed means, seeds 330001, 330007, 330019; 16 acquisitions per seed (48 per condition). Pull is the length of the pooled signed mean; its uncertainty is the delta-method SE, not the SD. Fractions are descriptive; 48/48 alone cannot certify 95% success (one-sided 95% lower bound 93.95%, conditional independence only).

| Case | t s | F uSv/h | S | Estimator | B mean +/- SD | dx mm mean +/- SD | dy mm mean +/- SD | Pull mm +/- SE | RMS mm mean +/- SD | Within element mean +/- SD | Bfit mean +/- SD |
|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|
| head | 10 | 0 | 25 | E0 | 0.000 +/- 0.000 | -6.518 +/- 2.691 | -0.963 +/- 1.877 | 6.588 +/- 1.686 | 10.094 +/- 2.330 | 0.188 +/- 0.188 | - |
| head | 10 | 0 | 25 | E1/model | 0.000 +/- 0.000 | -2.284 +/- 1.487 | -0.483 +/- 0.303 | 2.335 +/- 0.859 | 5.874 +/- 1.941 | 0.688 +/- 0.062 | - |
| head | 10 | 0 | 25 | E2/model | 0.000 +/- 0.000 | -2.041 +/- 1.485 | -0.539 +/- 0.185 | 2.111 +/- 0.856 | 5.605 +/- 1.911 | 0.708 +/- 0.072 | - |
| head | 10 | 0 | 25 | E3/model | 0.000 +/- 0.000 | -5.969 +/- 2.033 | -0.234 +/- 2.091 | 5.974 +/- 1.218 | 9.805 +/- 1.557 | 0.229 +/- 0.157 | 0.723 +/- 0.225 |
| head | 10 | 0 | 25 | E4/model | 0.000 +/- 0.000 | -3.708 +/- 0.828 | 0.570 +/- 0.744 | 3.751 +/- 0.425 | 8.016 +/- 0.787 | 0.500 +/- 0.062 | 0.035 +/- 0.061 |
| head | 10 | 0 | 25 | E5/model | 0.000 +/- 0.000 | -3.707 +/- 0.827 | 0.570 +/- 0.744 | 3.751 +/- 0.425 | 8.016 +/- 0.788 | 0.500 +/- 0.062 | - |
| head | 10 | 0 | 25 | E6/model | 0.000 +/- 0.000 | -6.518 +/- 2.691 | -0.963 +/- 1.877 | 6.588 +/- 1.686 | 10.094 +/- 2.330 | 0.188 +/- 0.188 | - |
| head | 10 | 0 | 25 | E7/model | 0.000 +/- 0.000 | -3.073 +/- 0.583 | 0.366 +/- 1.095 | 3.095 +/- 0.309 | 7.083 +/- 0.643 | 0.562 +/- 0.062 | 0.723 +/- 0.225 |
| head | 10 | 0 | 100 | E0 | 0.000 +/- 0.000 | -3.148 +/- 1.191 | -2.464 +/- 0.654 | 3.997 +/- 0.734 | 8.033 +/- 1.603 | 0.458 +/- 0.157 | - |
| head | 10 | 0 | 100 | E1/model | 0.000 +/- 0.000 | 0.296 +/- 0.072 | -0.252 +/- 0.135 | 0.389 +/- 0.020 | 0.691 +/- 0.268 | 1.000 +/- 0.000 | - |
| head | 10 | 0 | 100 | E2/model | 0.000 +/- 0.000 | 0.263 +/- 0.059 | -0.197 +/- 0.165 | 0.328 +/- 0.030 | 0.699 +/- 0.256 | 1.000 +/- 0.000 | - |
| head | 10 | 0 | 100 | E3/model | 0.000 +/- 0.000 | -2.502 +/- 1.004 | -1.546 +/- 0.368 | 2.941 +/- 0.576 | 7.363 +/- 1.431 | 0.542 +/- 0.130 | 5.888 +/- 1.078 |
| head | 10 | 0 | 100 | E4/model | 0.000 +/- 0.000 | -0.291 +/- 0.677 | -0.424 +/- 0.557 | 0.514 +/- 0.483 | 3.694 +/- 1.852 | 0.938 +/- 0.000 | 0.019 +/- 0.030 |
| head | 10 | 0 | 100 | E5/model | 0.000 +/- 0.000 | -0.290 +/- 0.678 | -0.422 +/- 0.556 | 0.512 +/- 0.483 | 3.695 +/- 1.850 | 0.938 +/- 0.000 | - |
| head | 10 | 0 | 100 | E6/model | 0.000 +/- 0.000 | -3.148 +/- 1.191 | -2.464 +/- 0.654 | 3.997 +/- 0.734 | 8.033 +/- 1.603 | 0.458 +/- 0.157 | - |
| head | 10 | 0 | 100 | E7/model | 0.000 +/- 0.000 | 0.361 +/- 0.155 | -0.065 +/- 0.019 | 0.367 +/- 0.087 | 0.812 +/- 0.404 | 0.979 +/- 0.036 | 5.888 +/- 1.078 |
| head | 10 | 0 | 250 | E0 | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 10 | 0 | 250 | E1/model | 0.000 +/- 0.000 | 0.313 +/- 0.006 | -0.169 +/- 0.090 | 0.356 +/- 0.026 | 0.465 +/- 0.058 | 1.000 +/- 0.000 | - |
| head | 10 | 0 | 250 | E2/model | 0.000 +/- 0.000 | 0.263 +/- 0.030 | -0.077 +/- 0.112 | 0.274 +/- 0.029 | 0.393 +/- 0.081 | 1.000 +/- 0.000 | - |
| head | 10 | 0 | 250 | E3/model | 0.000 +/- 0.000 | -0.093 +/- 0.150 | -0.776 +/- 0.200 | 0.782 +/- 0.123 | 2.294 +/- 1.395 | 0.938 +/- 0.062 | 14.149 +/- 4.626 |
| head | 10 | 0 | 250 | E4/model | 0.000 +/- 0.000 | 0.440 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | 0.333 +/- 0.488 |
| head | 10 | 0 | 250 | E5/model | 0.000 +/- 0.000 | 0.441 +/- 0.032 | -0.228 +/- 0.093 | 0.496 +/- 0.013 | 0.603 +/- 0.054 | 1.000 +/- 0.000 | - |
| head | 10 | 0 | 250 | E6/model | 0.000 +/- 0.000 | -0.255 +/- 0.403 | -0.965 +/- 0.351 | 0.998 +/- 0.253 | 2.605 +/- 1.790 | 0.917 +/- 0.095 | - |
| head | 10 | 0 | 250 | E7/model | 0.000 +/- 0.000 | 0.373 +/- 0.022 | -0.145 +/- 0.103 | 0.400 +/- 0.020 | 0.512 +/- 0.015 | 1.000 +/- 0.000 | 14.149 +/- 4.626 |
| head | 10 | 0 | 1000 | E0 | 0.000 +/- 0.000 | 0.269 +/- 0.171 | -0.419 +/- 0.050 | 0.498 +/- 0.029 | 0.828 +/- 0.086 | 1.000 +/- 0.000 | - |
| head | 10 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | 0.375 +/- 0.005 | -0.061 +/- 0.028 | 0.380 +/- 0.002 | 0.389 +/- 0.002 | 1.000 +/- 0.000 | - |
| head | 10 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | 0.228 +/- 0.015 | 0.000 +/- 0.000 | 0.228 +/- 0.009 | 0.235 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 10 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | 0.220 +/- 0.141 | -0.430 +/- 0.064 | 0.483 +/- 0.004 | 0.789 +/- 0.064 | 1.000 +/- 0.000 | 48.895 +/- 7.653 |
| head | 10 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 0.456 +/- 0.016 | -0.145 +/- 0.043 | 0.479 +/- 0.004 | 0.497 +/- 0.010 | 1.000 +/- 0.000 | 0.333 +/- 0.527 |
| head | 10 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 0.456 +/- 0.016 | -0.145 +/- 0.044 | 0.479 +/- 0.004 | 0.497 +/- 0.010 | 1.000 +/- 0.000 | - |
| head | 10 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | 0.269 +/- 0.171 | -0.419 +/- 0.050 | 0.498 +/- 0.029 | 0.828 +/- 0.086 | 1.000 +/- 0.000 | - |
| head | 10 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 0.419 +/- 0.018 | -0.035 +/- 0.039 | 0.421 +/- 0.009 | 0.437 +/- 0.017 | 1.000 +/- 0.000 | 48.895 +/- 7.653 |
| head | 10 | 0.05 | 25 | E0 | 167.655 +/- 0.078 | -6.123 +/- 0.874 | 2.054 +/- 2.343 | 6.459 +/- 0.079 | 10.106 +/- 0.346 | 0.000 +/- 0.000 | - |
| head | 10 | 0.05 | 25 | E1/model | 167.655 +/- 0.078 | -7.509 +/- 0.531 | -0.234 +/- 2.579 | 7.512 +/- 0.288 | 11.357 +/- 0.263 | 0.188 +/- 0.108 | - |
| head | 10 | 0.05 | 25 | E2/model | 167.655 +/- 0.078 | -7.418 +/- 0.819 | -0.111 +/- 1.955 | 7.419 +/- 0.477 | 11.210 +/- 0.709 | 0.208 +/- 0.095 | - |
| head | 10 | 0.05 | 25 | E3/model | 167.655 +/- 0.078 | -7.142 +/- 1.218 | -1.146 +/- 1.519 | 7.233 +/- 0.714 | 10.841 +/- 0.809 | 0.083 +/- 0.036 | 145.384 +/- 0.815 |
| head | 10 | 0.05 | 25 | E4/model | 167.655 +/- 0.078 | -7.540 +/- 1.911 | 0.644 +/- 1.541 | 7.568 +/- 1.175 | 11.299 +/- 1.131 | 0.188 +/- 0.000 | 127.214 +/- 2.354 |
| head | 10 | 0.05 | 25 | E5/model | 167.655 +/- 0.078 | -7.693 +/- 1.994 | -0.026 +/- 1.754 | 7.693 +/- 1.148 | 11.481 +/- 1.209 | 0.188 +/- 0.000 | - |
| head | 10 | 0.05 | 25 | E6/model | 167.655 +/- 0.078 | -7.152 +/- 0.448 | -0.844 +/- 1.060 | 7.202 +/- 0.230 | 11.068 +/- 0.188 | 0.125 +/- 0.108 | - |
| head | 10 | 0.05 | 25 | E7/model | 167.655 +/- 0.078 | -6.799 +/- 1.748 | -0.702 +/- 2.228 | 6.836 +/- 0.876 | 10.926 +/- 1.127 | 0.188 +/- 0.062 | 145.384 +/- 0.815 |
| head | 10 | 0.05 | 100 | E0 | 167.655 +/- 0.078 | -7.221 +/- 0.573 | 0.395 +/- 0.694 | 7.232 +/- 0.324 | 10.801 +/- 0.560 | 0.000 +/- 0.000 | - |
| head | 10 | 0.05 | 100 | E1/model | 167.655 +/- 0.078 | -0.884 +/- 0.628 | -0.065 +/- 0.418 | 0.887 +/- 0.366 | 4.029 +/- 1.541 | 0.896 +/- 0.036 | - |
| head | 10 | 0.05 | 100 | E2/model | 167.655 +/- 0.078 | -0.371 +/- 0.547 | -0.283 +/- 0.118 | 0.466 +/- 0.273 | 2.941 +/- 2.088 | 0.958 +/- 0.036 | - |
| head | 10 | 0.05 | 100 | E3/model | 167.655 +/- 0.078 | -3.999 +/- 1.184 | -1.802 +/- 0.694 | 4.386 +/- 0.481 | 8.368 +/- 1.218 | 0.458 +/- 0.095 | 160.637 +/- 4.070 |
| head | 10 | 0.05 | 100 | E4/model | 167.655 +/- 0.078 | -1.806 +/- 0.953 | 0.083 +/- 0.195 | 1.808 +/- 0.545 | 5.762 +/- 1.343 | 0.792 +/- 0.095 | 117.869 +/- 3.725 |
| head | 10 | 0.05 | 100 | E5/model | 167.655 +/- 0.078 | -1.341 +/- 0.805 | 0.038 +/- 0.317 | 1.341 +/- 0.461 | 5.291 +/- 1.031 | 0.875 +/- 0.062 | - |
| head | 10 | 0.05 | 100 | E6/model | 167.655 +/- 0.078 | -4.467 +/- 1.065 | -0.997 +/- 0.916 | 4.577 +/- 0.485 | 8.604 +/- 1.102 | 0.375 +/- 0.000 | - |
| head | 10 | 0.05 | 100 | E7/model | 167.655 +/- 0.078 | -1.543 +/- 0.667 | 0.133 +/- 0.657 | 1.549 +/- 0.353 | 5.607 +/- 1.067 | 0.854 +/- 0.036 | 160.637 +/- 4.070 |
| head | 10 | 0.05 | 250 | E0 | 167.655 +/- 0.078 | -6.733 +/- 0.714 | 0.456 +/- 1.318 | 6.749 +/- 0.463 | 10.374 +/- 0.546 | 0.021 +/- 0.036 | - |
| head | 10 | 0.05 | 250 | E1/model | 167.655 +/- 0.078 | 0.325 +/- 0.066 | -0.112 +/- 0.028 | 0.343 +/- 0.031 | 0.475 +/- 0.015 | 1.000 +/- 0.000 | - |
| head | 10 | 0.05 | 250 | E2/model | 167.655 +/- 0.078 | 0.288 +/- 0.090 | -0.043 +/- 0.090 | 0.291 +/- 0.048 | 0.486 +/- 0.037 | 1.000 +/- 0.000 | - |
| head | 10 | 0.05 | 250 | E3/model | 167.655 +/- 0.078 | -1.092 +/- 1.262 | -1.556 +/- 1.190 | 1.901 +/- 0.980 | 4.410 +/- 2.979 | 0.792 +/- 0.180 | 181.567 +/- 1.762 |
| head | 10 | 0.05 | 250 | E4/model | 167.655 +/- 0.078 | 0.137 +/- 0.667 | -0.082 +/- 0.031 | 0.160 +/- 0.337 | 1.923 +/- 2.188 | 0.979 +/- 0.036 | 108.863 +/- 2.815 |
| head | 10 | 0.05 | 250 | E5/model | 167.655 +/- 0.078 | 0.491 +/- 0.029 | -0.091 +/- 0.041 | 0.500 +/- 0.021 | 0.636 +/- 0.014 | 1.000 +/- 0.000 | - |
| head | 10 | 0.05 | 250 | E6/model | 167.655 +/- 0.078 | -1.941 +/- 0.733 | -1.245 +/- 1.687 | 2.306 +/- 0.875 | 6.200 +/- 0.997 | 0.708 +/- 0.095 | - |
| head | 10 | 0.05 | 250 | E7/model | 167.655 +/- 0.078 | 0.451 +/- 0.064 | -0.021 +/- 0.046 | 0.452 +/- 0.037 | 0.601 +/- 0.036 | 1.000 +/- 0.000 | 181.567 +/- 1.762 |
| head | 10 | 0.05 | 1000 | E0 | 167.655 +/- 0.078 | -0.522 +/- 0.307 | -0.445 +/- 0.291 | 0.686 +/- 0.026 | 4.066 +/- 1.324 | 0.854 +/- 0.095 | - |
| head | 10 | 0.05 | 1000 | E1/model | 167.655 +/- 0.078 | 0.386 +/- 0.022 | -0.065 +/- 0.007 | 0.391 +/- 0.012 | 0.398 +/- 0.020 | 1.000 +/- 0.000 | - |
| head | 10 | 0.05 | 1000 | E2/model | 167.655 +/- 0.078 | 0.254 +/- 0.039 | 0.000 +/- 0.000 | 0.254 +/- 0.023 | 0.274 +/- 0.059 | 1.000 +/- 0.000 | - |
| head | 10 | 0.05 | 1000 | E3/model | 167.655 +/- 0.078 | 0.414 +/- 0.396 | -0.380 +/- 0.042 | 0.562 +/- 0.177 | 0.924 +/- 0.176 | 1.000 +/- 0.000 | 202.494 +/- 5.557 |
| head | 10 | 0.05 | 1000 | E4/model | 167.655 +/- 0.078 | 0.492 +/- 0.018 | -0.147 +/- 0.027 | 0.514 +/- 0.005 | 0.532 +/- 0.012 | 1.000 +/- 0.000 | 49.350 +/- 12.552 |
| head | 10 | 0.05 | 1000 | E5/model | 167.655 +/- 0.078 | 0.483 +/- 0.015 | -0.159 +/- 0.027 | 0.508 +/- 0.004 | 0.528 +/- 0.011 | 1.000 +/- 0.000 | - |
| head | 10 | 0.05 | 1000 | E6/model | 167.655 +/- 0.078 | 0.454 +/- 0.468 | -0.381 +/- 0.042 | 0.593 +/- 0.215 | 0.950 +/- 0.205 | 1.000 +/- 0.000 | - |
| head | 10 | 0.05 | 1000 | E7/model | 167.655 +/- 0.078 | 0.449 +/- 0.042 | -0.047 +/- 0.022 | 0.451 +/- 0.023 | 0.473 +/- 0.039 | 1.000 +/- 0.000 | 202.494 +/- 5.557 |
| head | 10 | 0.1 | 25 | E0 | 335.311 +/- 0.156 | -6.236 +/- 1.396 | 1.960 +/- 3.563 | 6.537 +/- 0.153 | 9.995 +/- 0.347 | 0.000 +/- 0.000 | - |
| head | 10 | 0.1 | 25 | E1/model | 335.311 +/- 0.156 | -7.390 +/- 1.877 | -0.299 +/- 1.584 | 7.397 +/- 1.073 | 11.232 +/- 1.311 | 0.083 +/- 0.036 | - |
| head | 10 | 0.1 | 25 | E2/model | 335.311 +/- 0.156 | -7.358 +/- 1.876 | -0.462 +/- 1.892 | 7.372 +/- 1.060 | 11.150 +/- 1.287 | 0.083 +/- 0.036 | - |
| head | 10 | 0.1 | 25 | E3/model | 335.311 +/- 0.156 | -8.154 +/- 1.997 | -1.604 +/- 1.641 | 8.311 +/- 1.092 | 12.108 +/- 1.353 | 0.000 +/- 0.000 | 292.805 +/- 5.501 |
| head | 10 | 0.1 | 25 | E4/model | 335.311 +/- 0.156 | -8.643 +/- 0.753 | 0.984 +/- 0.890 | 8.699 +/- 0.377 | 11.752 +/- 0.424 | 0.104 +/- 0.036 | 276.732 +/- 5.485 |
| head | 10 | 0.1 | 25 | E5/model | 335.311 +/- 0.156 | -8.568 +/- 1.327 | 0.551 +/- 0.858 | 8.586 +/- 0.734 | 11.786 +/- 0.617 | 0.146 +/- 0.095 | - |
| head | 10 | 0.1 | 25 | E6/model | 335.311 +/- 0.156 | -7.202 +/- 2.085 | -0.552 +/- 1.256 | 7.223 +/- 1.224 | 11.273 +/- 1.521 | 0.062 +/- 0.062 | - |
| head | 10 | 0.1 | 25 | E7/model | 335.311 +/- 0.156 | -8.580 +/- 1.822 | 0.423 +/- 0.596 | 8.590 +/- 1.042 | 11.725 +/- 1.285 | 0.104 +/- 0.072 | 292.805 +/- 5.501 |
| head | 10 | 0.1 | 100 | E0 | 335.311 +/- 0.156 | -5.926 +/- 0.559 | 4.603 +/- 1.019 | 7.504 +/- 0.108 | 10.231 +/- 0.329 | 0.000 +/- 0.000 | - |
| head | 10 | 0.1 | 100 | E1/model | 335.311 +/- 0.156 | -5.379 +/- 1.555 | 0.372 +/- 0.816 | 5.391 +/- 0.928 | 8.979 +/- 1.462 | 0.375 +/- 0.062 | - |
| head | 10 | 0.1 | 100 | E2/model | 335.311 +/- 0.156 | -4.738 +/- 1.766 | 0.471 +/- 0.554 | 4.761 +/- 1.042 | 8.418 +/- 1.721 | 0.438 +/- 0.062 | - |
| head | 10 | 0.1 | 100 | E3/model | 335.311 +/- 0.156 | -5.726 +/- 2.280 | -2.134 +/- 0.968 | 6.110 +/- 1.420 | 9.357 +/- 1.186 | 0.104 +/- 0.072 | 325.234 +/- 8.752 |
| head | 10 | 0.1 | 100 | E4/model | 335.311 +/- 0.156 | -4.520 +/- 0.188 | 1.080 +/- 1.673 | 4.647 +/- 0.120 | 8.159 +/- 0.352 | 0.312 +/- 0.062 | 275.902 +/- 4.219 |
| head | 10 | 0.1 | 100 | E5/model | 335.311 +/- 0.156 | -4.718 +/- 0.415 | 0.977 +/- 1.292 | 4.818 +/- 0.382 | 8.208 +/- 0.549 | 0.354 +/- 0.036 | - |
| head | 10 | 0.1 | 100 | E6/model | 335.311 +/- 0.156 | -6.135 +/- 1.416 | -0.919 +/- 1.964 | 6.204 +/- 0.977 | 9.476 +/- 0.543 | 0.104 +/- 0.072 | - |
| head | 10 | 0.1 | 100 | E7/model | 335.311 +/- 0.156 | -4.200 +/- 0.379 | 0.571 +/- 0.695 | 4.238 +/- 0.192 | 7.987 +/- 0.466 | 0.396 +/- 0.095 | 325.234 +/- 8.752 |
| head | 10 | 0.1 | 250 | E0 | 335.311 +/- 0.156 | -5.935 +/- 0.465 | 4.744 +/- 0.748 | 7.598 +/- 0.127 | 10.230 +/- 0.353 | 0.000 +/- 0.000 | - |
| head | 10 | 0.1 | 250 | E1/model | 335.311 +/- 0.156 | 0.266 +/- 0.062 | -0.189 +/- 0.082 | 0.326 +/- 0.048 | 0.539 +/- 0.042 | 1.000 +/- 0.000 | - |
| head | 10 | 0.1 | 250 | E2/model | 335.311 +/- 0.156 | 0.288 +/- 0.039 | -0.137 +/- 0.090 | 0.319 +/- 0.042 | 0.482 +/- 0.148 | 1.000 +/- 0.000 | - |
| head | 10 | 0.1 | 250 | E3/model | 335.311 +/- 0.156 | -2.651 +/- 0.908 | -2.353 +/- 0.357 | 3.545 +/- 0.403 | 7.262 +/- 1.417 | 0.583 +/- 0.095 | 344.324 +/- 2.636 |
| head | 10 | 0.1 | 250 | E4/model | 335.311 +/- 0.156 | 0.134 +/- 0.653 | -0.142 +/- 0.138 | 0.195 +/- 0.301 | 1.967 +/- 2.211 | 0.979 +/- 0.036 | 259.462 +/- 5.012 |
| head | 10 | 0.1 | 250 | E5/model | 335.311 +/- 0.156 | 0.110 +/- 0.648 | -0.152 +/- 0.136 | 0.188 +/- 0.263 | 1.948 +/- 2.225 | 0.979 +/- 0.036 | - |
| head | 10 | 0.1 | 250 | E6/model | 335.311 +/- 0.156 | -3.251 +/- 1.142 | -1.477 +/- 0.212 | 3.571 +/- 0.566 | 7.910 +/- 1.670 | 0.521 +/- 0.157 | - |
| head | 10 | 0.1 | 250 | E7/model | 335.311 +/- 0.156 | 0.454 +/- 0.067 | -0.084 +/- 0.121 | 0.462 +/- 0.039 | 0.635 +/- 0.114 | 1.000 +/- 0.000 | 344.324 +/- 2.636 |
| head | 10 | 0.1 | 1000 | E0 | 335.311 +/- 0.156 | -3.438 +/- 0.990 | 1.233 +/- 0.820 | 3.652 +/- 0.698 | 8.049 +/- 0.955 | 0.396 +/- 0.144 | - |
| head | 10 | 0.1 | 1000 | E1/model | 335.311 +/- 0.156 | 0.370 +/- 0.006 | -0.071 +/- 0.045 | 0.377 +/- 0.002 | 0.397 +/- 0.016 | 1.000 +/- 0.000 | - |
| head | 10 | 0.1 | 1000 | E2/model | 335.311 +/- 0.156 | 0.237 +/- 0.015 | 0.009 +/- 0.015 | 0.237 +/- 0.009 | 0.256 +/- 0.033 | 1.000 +/- 0.000 | - |
| head | 10 | 0.1 | 1000 | E3/model | 335.311 +/- 0.156 | 0.210 +/- 0.088 | -0.511 +/- 0.033 | 0.552 +/- 0.037 | 0.817 +/- 0.085 | 1.000 +/- 0.000 | 368.076 +/- 15.902 |
| head | 10 | 0.1 | 1000 | E4/model | 335.311 +/- 0.156 | 0.476 +/- 0.011 | -0.147 +/- 0.019 | 0.498 +/- 0.004 | 0.518 +/- 0.001 | 1.000 +/- 0.000 | 203.765 +/- 17.037 |
| head | 10 | 0.1 | 1000 | E5/model | 335.311 +/- 0.156 | 0.466 +/- 0.011 | -0.155 +/- 0.016 | 0.491 +/- 0.004 | 0.510 +/- 0.003 | 1.000 +/- 0.000 | - |
| head | 10 | 0.1 | 1000 | E6/model | 335.311 +/- 0.156 | 0.117 +/- 0.282 | -0.662 +/- 0.216 | 0.672 +/- 0.095 | 1.413 +/- 0.940 | 0.979 +/- 0.036 | - |
| head | 10 | 0.1 | 1000 | E7/model | 335.311 +/- 0.156 | 0.428 +/- 0.012 | -0.057 +/- 0.029 | 0.432 +/- 0.005 | 0.455 +/- 0.013 | 1.000 +/- 0.000 | 368.076 +/- 15.902 |
| head | 10 | 0.2 | 25 | E0 | 670.622 +/- 0.312 | -5.481 +/- 0.379 | 4.935 +/- 0.867 | 7.376 +/- 0.181 | 9.785 +/- 0.147 | 0.000 +/- 0.000 | - |
| head | 10 | 0.2 | 25 | E1/model | 670.622 +/- 0.312 | -8.017 +/- 2.328 | 0.615 +/- 0.913 | 8.041 +/- 1.308 | 11.072 +/- 1.671 | 0.104 +/- 0.095 | - |
| head | 10 | 0.2 | 25 | E2/model | 670.622 +/- 0.312 | -8.317 +/- 2.613 | 0.950 +/- 1.223 | 8.371 +/- 1.479 | 11.252 +/- 1.822 | 0.104 +/- 0.095 | - |
| head | 10 | 0.2 | 25 | E3/model | 670.622 +/- 0.312 | -8.285 +/- 1.007 | 1.557 +/- 1.652 | 8.430 +/- 0.604 | 11.352 +/- 0.702 | 0.062 +/- 0.062 | 607.002 +/- 8.021 |
| head | 10 | 0.2 | 25 | E4/model | 670.622 +/- 0.312 | -7.418 +/- 2.777 | -0.790 +/- 1.022 | 7.460 +/- 1.560 | 10.532 +/- 2.577 | 0.146 +/- 0.144 | 595.765 +/- 8.894 |
| head | 10 | 0.2 | 25 | E5/model | 670.622 +/- 0.312 | -6.994 +/- 3.149 | -0.510 +/- 1.331 | 7.012 +/- 1.759 | 10.309 +/- 2.951 | 0.146 +/- 0.144 | - |
| head | 10 | 0.2 | 25 | E6/model | 670.622 +/- 0.312 | -8.343 +/- 2.348 | 1.087 +/- 2.061 | 8.413 +/- 1.381 | 11.330 +/- 1.457 | 0.104 +/- 0.095 | - |
| head | 10 | 0.2 | 25 | E7/model | 670.622 +/- 0.312 | -7.569 +/- 3.175 | -0.819 +/- 1.165 | 7.613 +/- 1.756 | 10.619 +/- 3.076 | 0.125 +/- 0.165 | 607.002 +/- 8.021 |
| head | 10 | 0.2 | 100 | E0 | 670.622 +/- 0.312 | -5.489 +/- 0.453 | 4.889 +/- 0.860 | 7.351 +/- 0.204 | 9.813 +/- 0.213 | 0.000 +/- 0.000 | - |
| head | 10 | 0.2 | 100 | E1/model | 670.622 +/- 0.312 | -3.813 +/- 0.942 | 0.674 +/- 1.098 | 3.872 +/- 0.512 | 7.730 +/- 0.825 | 0.375 +/- 0.062 | - |
| head | 10 | 0.2 | 100 | E2/model | 670.622 +/- 0.312 | -3.642 +/- 1.216 | 0.402 +/- 1.258 | 3.664 +/- 0.705 | 7.635 +/- 0.907 | 0.417 +/- 0.095 | - |
| head | 10 | 0.2 | 100 | E3/model | 670.622 +/- 0.312 | -7.179 +/- 0.509 | -1.335 +/- 1.989 | 7.302 +/- 0.137 | 10.891 +/- 0.039 | 0.146 +/- 0.036 | 633.704 +/- 6.886 |
| head | 10 | 0.2 | 100 | E4/model | 670.622 +/- 0.312 | -4.002 +/- 1.040 | -0.096 +/- 1.721 | 4.003 +/- 0.584 | 8.559 +/- 0.883 | 0.479 +/- 0.095 | 575.436 +/- 10.655 |
| head | 10 | 0.2 | 100 | E5/model | 670.622 +/- 0.312 | -3.663 +/- 1.131 | 0.662 +/- 1.047 | 3.723 +/- 0.744 | 8.299 +/- 0.897 | 0.521 +/- 0.072 | - |
| head | 10 | 0.2 | 100 | E6/model | 670.622 +/- 0.312 | -6.168 +/- 0.337 | -0.439 +/- 2.084 | 6.183 +/- 0.178 | 10.095 +/- 0.411 | 0.229 +/- 0.036 | - |
| head | 10 | 0.2 | 100 | E7/model | 670.622 +/- 0.312 | -3.128 +/- 1.124 | 0.166 +/- 1.456 | 3.132 +/- 0.688 | 7.709 +/- 1.287 | 0.500 +/- 0.062 | 633.704 +/- 6.886 |
| head | 10 | 0.2 | 250 | E0 | 670.622 +/- 0.312 | -5.467 +/- 0.414 | 4.610 +/- 0.495 | 7.151 +/- 0.170 | 9.771 +/- 0.241 | 0.000 +/- 0.000 | - |
| head | 10 | 0.2 | 250 | E1/model | 670.622 +/- 0.312 | 0.251 +/- 0.101 | -0.151 +/- 0.211 | 0.293 +/- 0.059 | 0.617 +/- 0.075 | 1.000 +/- 0.000 | - |
| head | 10 | 0.2 | 250 | E2/model | 670.622 +/- 0.312 | 0.211 +/- 0.059 | -0.077 +/- 0.180 | 0.225 +/- 0.038 | 0.638 +/- 0.069 | 1.000 +/- 0.000 | - |
| head | 10 | 0.2 | 250 | E3/model | 670.622 +/- 0.312 | -3.723 +/- 1.229 | -1.111 +/- 0.627 | 3.886 +/- 0.661 | 7.576 +/- 1.440 | 0.542 +/- 0.191 | 670.631 +/- 0.872 |
| head | 10 | 0.2 | 250 | E4/model | 670.622 +/- 0.312 | 0.531 +/- 0.133 | -0.006 +/- 0.134 | 0.531 +/- 0.076 | 0.832 +/- 0.021 | 1.000 +/- 0.000 | 568.200 +/- 6.365 |
| head | 10 | 0.2 | 250 | E5/model | 670.622 +/- 0.312 | 0.515 +/- 0.105 | -0.031 +/- 0.140 | 0.516 +/- 0.058 | 0.779 +/- 0.059 | 1.000 +/- 0.000 | - |
| head | 10 | 0.2 | 250 | E6/model | 670.622 +/- 0.312 | -3.908 +/- 1.329 | -1.064 +/- 0.867 | 4.051 +/- 0.826 | 7.950 +/- 1.256 | 0.542 +/- 0.191 | - |
| head | 10 | 0.2 | 250 | E7/model | 670.622 +/- 0.312 | 0.504 +/- 0.097 | 0.046 +/- 0.158 | 0.506 +/- 0.059 | 0.788 +/- 0.046 | 1.000 +/- 0.000 | 670.631 +/- 0.872 |
| head | 10 | 0.2 | 1000 | E0 | 670.622 +/- 0.312 | -5.066 +/- 0.452 | 5.937 +/- 0.607 | 7.804 +/- 0.122 | 9.771 +/- 0.260 | 0.000 +/- 0.000 | - |
| head | 10 | 0.2 | 1000 | E1/model | 670.622 +/- 0.312 | 0.375 +/- 0.007 | -0.046 +/- 0.015 | 0.378 +/- 0.005 | 0.390 +/- 0.008 | 1.000 +/- 0.000 | - |
| head | 10 | 0.2 | 1000 | E2/model | 670.622 +/- 0.312 | 0.254 +/- 0.015 | 0.000 +/- 0.000 | 0.254 +/- 0.009 | 0.278 +/- 0.022 | 1.000 +/- 0.000 | - |
| head | 10 | 0.2 | 1000 | E3/model | 670.622 +/- 0.312 | 0.391 +/- 0.250 | -0.393 +/- 0.058 | 0.555 +/- 0.125 | 0.918 +/- 0.138 | 1.000 +/- 0.000 | 716.213 +/- 19.008 |
| head | 10 | 0.2 | 1000 | E4/model | 670.622 +/- 0.312 | 0.500 +/- 0.031 | -0.060 +/- 0.027 | 0.504 +/- 0.020 | 0.527 +/- 0.033 | 1.000 +/- 0.000 | 541.491 +/- 25.604 |
| head | 10 | 0.2 | 1000 | E5/model | 670.622 +/- 0.312 | 0.488 +/- 0.029 | -0.071 +/- 0.031 | 0.493 +/- 0.019 | 0.517 +/- 0.032 | 1.000 +/- 0.000 | - |
| head | 10 | 0.2 | 1000 | E6/model | 670.622 +/- 0.312 | 0.438 +/- 0.236 | -0.393 +/- 0.055 | 0.588 +/- 0.122 | 0.965 +/- 0.132 | 1.000 +/- 0.000 | - |
| head | 10 | 0.2 | 1000 | E7/model | 670.622 +/- 0.312 | 0.448 +/- 0.026 | 0.001 +/- 0.023 | 0.448 +/- 0.015 | 0.474 +/- 0.026 | 1.000 +/- 0.000 | 716.213 +/- 19.008 |
| head1m | 10 | 0 | 25 | E0 | 0.000 +/- 0.000 | -54.483 +/- 7.805 | -2.871 +/- 9.699 | 54.558 +/- 4.549 | 74.813 +/- 7.478 | 0.125 +/- 0.000 | - |
| head1m | 10 | 0 | 25 | E1/model | 0.000 +/- 0.000 | -44.162 +/- 10.015 | 3.935 +/- 19.149 | 44.337 +/- 6.271 | 62.905 +/- 7.146 | 0.042 +/- 0.072 | - |
| head1m | 10 | 0 | 25 | E2/model | 0.000 +/- 0.000 | -43.514 +/- 8.807 | 1.602 +/- 17.208 | 43.544 +/- 5.334 | 62.079 +/- 5.779 | 0.042 +/- 0.072 | - |
| head1m | 10 | 0 | 25 | E3/model | 0.000 +/- 0.000 | -42.549 +/- 13.434 | 4.660 +/- 14.414 | 42.803 +/- 7.126 | 65.560 +/- 12.894 | 0.125 +/- 0.062 | 0.501 +/- 0.227 |
| head1m | 10 | 0 | 25 | E4/model | 0.000 +/- 0.000 | -39.192 +/- 6.571 | 2.052 +/- 7.776 | 39.246 +/- 3.996 | 62.839 +/- 6.443 | 0.188 +/- 0.108 | 0.011 +/- 0.013 |
| head1m | 10 | 0 | 25 | E5/model | 0.000 +/- 0.000 | -39.189 +/- 6.569 | 2.052 +/- 7.770 | 39.243 +/- 3.995 | 62.832 +/- 6.442 | 0.188 +/- 0.108 | - |
| head1m | 10 | 0 | 25 | E6/model | 0.000 +/- 0.000 | -54.483 +/- 7.805 | -2.871 +/- 9.699 | 54.558 +/- 4.549 | 74.813 +/- 7.478 | 0.125 +/- 0.000 | - |
| head1m | 10 | 0 | 25 | E7/model | 0.000 +/- 0.000 | -38.996 +/- 7.166 | 3.034 +/- 0.908 | 39.113 +/- 4.101 | 62.674 +/- 5.181 | 0.229 +/- 0.144 | 0.501 +/- 0.227 |
| head1m | 10 | 0 | 100 | E0 | 0.000 +/- 0.000 | -32.482 +/- 8.623 | -2.596 +/- 4.339 | 32.585 +/- 5.065 | 61.981 +/- 9.230 | 0.583 +/- 0.095 | - |
| head1m | 10 | 0 | 100 | E1/model | 0.000 +/- 0.000 | -30.488 +/- 6.513 | -3.432 +/- 8.883 | 30.681 +/- 3.172 | 50.747 +/- 4.810 | 0.292 +/- 0.095 | - |
| head1m | 10 | 0 | 100 | E2/model | 0.000 +/- 0.000 | -29.207 +/- 4.924 | -2.541 +/- 5.507 | 29.318 +/- 2.562 | 49.783 +/- 3.052 | 0.292 +/- 0.095 | - |
| head1m | 10 | 0 | 100 | E3/model | 0.000 +/- 0.000 | -33.443 +/- 7.979 | -1.325 +/- 3.582 | 33.469 +/- 4.681 | 62.954 +/- 8.162 | 0.562 +/- 0.108 | 3.595 +/- 0.601 |
| head1m | 10 | 0 | 100 | E4/model | 0.000 +/- 0.000 | -10.252 +/- 7.558 | -0.423 +/- 4.064 | 10.261 +/- 4.433 | 37.441 +/- 12.496 | 0.750 +/- 0.062 | 0.005 +/- 0.004 |
| head1m | 10 | 0 | 100 | E5/model | 0.000 +/- 0.000 | -12.649 +/- 3.996 | -0.270 +/- 4.312 | 12.652 +/- 2.331 | 41.656 +/- 6.519 | 0.729 +/- 0.072 | - |
| head1m | 10 | 0 | 100 | E6/model | 0.000 +/- 0.000 | -32.482 +/- 8.623 | -2.596 +/- 4.339 | 32.585 +/- 5.065 | 61.981 +/- 9.230 | 0.583 +/- 0.095 | - |
| head1m | 10 | 0 | 100 | E7/model | 0.000 +/- 0.000 | -9.008 +/- 2.950 | -0.612 +/- 4.534 | 9.029 +/- 1.610 | 38.929 +/- 5.127 | 0.771 +/- 0.095 | 3.595 +/- 0.601 |
| head1m | 10 | 0 | 250 | E0 | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 10 | 0 | 250 | E1/model | 0.000 +/- 0.000 | -20.183 +/- 3.616 | -0.312 +/- 0.547 | 20.185 +/- 2.083 | 36.363 +/- 4.204 | 0.521 +/- 0.130 | - |
| head1m | 10 | 0 | 250 | E2/model | 0.000 +/- 0.000 | -22.247 +/- 3.424 | -2.762 +/- 1.935 | 22.418 +/- 1.825 | 38.740 +/- 3.567 | 0.458 +/- 0.130 | - |
| head1m | 10 | 0 | 250 | E3/model | 0.000 +/- 0.000 | -16.384 +/- 4.492 | -0.191 +/- 1.535 | 16.386 +/- 2.584 | 47.648 +/- 5.272 | 0.833 +/- 0.036 | 11.882 +/- 0.732 |
| head1m | 10 | 0 | 250 | E4/model | 0.000 +/- 0.000 | 2.624 +/- 1.767 | -1.913 +/- 2.026 | 3.247 +/- 0.283 | 8.836 +/- 7.365 | 0.979 +/- 0.036 | 0.001 +/- 0.000 |
| head1m | 10 | 0 | 250 | E5/model | 0.000 +/- 0.000 | 2.627 +/- 1.761 | -1.906 +/- 2.028 | 3.246 +/- 0.287 | 8.852 +/- 7.351 | 0.979 +/- 0.036 | - |
| head1m | 10 | 0 | 250 | E6/model | 0.000 +/- 0.000 | -16.392 +/- 4.486 | -0.116 +/- 1.550 | 16.392 +/- 2.584 | 47.649 +/- 5.271 | 0.833 +/- 0.036 | - |
| head1m | 10 | 0 | 250 | E7/model | 0.000 +/- 0.000 | 5.024 +/- 0.281 | -0.996 +/- 0.676 | 5.122 +/- 0.227 | 6.788 +/- 0.654 | 1.000 +/- 0.000 | 11.882 +/- 0.732 |
| head1m | 10 | 0 | 1000 | E0 | 0.000 +/- 0.000 | 2.597 +/- 4.239 | -0.052 +/- 0.211 | 2.598 +/- 2.449 | 13.372 +/- 13.969 | 0.979 +/- 0.036 | - |
| head1m | 10 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | -2.939 +/- 1.765 | -2.997 +/- 1.090 | 4.198 +/- 0.264 | 7.510 +/- 5.865 | 0.979 +/- 0.036 | - |
| head1m | 10 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | -5.786 +/- 3.357 | -2.707 +/- 1.906 | 6.387 +/- 1.713 | 17.365 +/- 12.511 | 0.896 +/- 0.095 | - |
| head1m | 10 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | 2.605 +/- 4.242 | -0.436 +/- 0.460 | 2.642 +/- 2.446 | 13.482 +/- 13.876 | 0.979 +/- 0.036 | 48.460 +/- 1.679 |
| head1m | 10 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 3.893 +/- 0.135 | -0.191 +/- 0.184 | 3.897 +/- 0.076 | 4.018 +/- 0.142 | 1.000 +/- 0.000 | 0.002 +/- 0.002 |
| head1m | 10 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 3.915 +/- 0.162 | -0.203 +/- 0.168 | 3.920 +/- 0.091 | 4.041 +/- 0.168 | 1.000 +/- 0.000 | - |
| head1m | 10 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | 2.597 +/- 4.239 | -0.052 +/- 0.211 | 2.598 +/- 2.449 | 13.372 +/- 13.969 | 0.979 +/- 0.036 | - |
| head1m | 10 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 5.867 +/- 0.383 | -0.653 +/- 0.133 | 5.903 +/- 0.212 | 6.283 +/- 0.206 | 1.000 +/- 0.000 | 48.460 +/- 1.679 |
| head1m | 10 | 0.05 | 25 | E0 | 167.655 +/- 0.078 | -63.363 +/- 8.151 | 13.297 +/- 1.172 | 64.743 +/- 4.563 | 76.960 +/- 4.949 | 0.188 +/- 0.108 | - |
| head1m | 10 | 0.05 | 25 | E1/model | 167.655 +/- 0.078 | -56.637 +/- 12.050 | -3.253 +/- 6.177 | 56.730 +/- 6.897 | 77.476 +/- 5.388 | 0.042 +/- 0.036 | - |
| head1m | 10 | 0.05 | 25 | E2/model | 167.655 +/- 0.078 | -55.943 +/- 11.803 | -0.718 +/- 2.569 | 55.948 +/- 6.795 | 76.425 +/- 4.432 | 0.042 +/- 0.036 | - |
| head1m | 10 | 0.05 | 25 | E3/model | 167.655 +/- 0.078 | -65.484 +/- 15.841 | 2.571 +/- 3.093 | 65.535 +/- 9.176 | 83.929 +/- 7.743 | 0.083 +/- 0.095 | 141.421 +/- 5.094 |
| head1m | 10 | 0.05 | 25 | E4/model | 167.655 +/- 0.078 | -50.493 +/- 18.069 | -0.450 +/- 11.007 | 50.495 +/- 10.396 | 71.392 +/- 12.242 | 0.083 +/- 0.072 | 126.687 +/- 8.038 |
| head1m | 10 | 0.05 | 25 | E5/model | 167.655 +/- 0.078 | -47.053 +/- 13.988 | -3.697 +/- 8.897 | 47.198 +/- 7.653 | 68.418 +/- 9.370 | 0.083 +/- 0.072 | - |
| head1m | 10 | 0.05 | 25 | E6/model | 167.655 +/- 0.078 | -61.615 +/- 7.336 | -4.280 +/- 4.156 | 61.763 +/- 4.349 | 81.346 +/- 1.379 | 0.062 +/- 0.062 | - |
| head1m | 10 | 0.05 | 25 | E7/model | 167.655 +/- 0.078 | -52.129 +/- 15.986 | -6.584 +/- 3.096 | 52.543 +/- 8.935 | 73.568 +/- 14.538 | 0.083 +/- 0.072 | 141.421 +/- 5.094 |
| head1m | 10 | 0.05 | 100 | E0 | 167.655 +/- 0.078 | -48.066 +/- 16.373 | 9.541 +/- 1.453 | 49.004 +/- 9.202 | 68.410 +/- 13.825 | 0.375 +/- 0.125 | - |
| head1m | 10 | 0.05 | 100 | E1/model | 167.655 +/- 0.078 | -40.501 +/- 9.090 | -9.959 +/- 9.593 | 41.707 +/- 6.418 | 58.930 +/- 10.203 | 0.104 +/- 0.180 | - |
| head1m | 10 | 0.05 | 100 | E2/model | 167.655 +/- 0.078 | -41.360 +/- 8.721 | -12.871 +/- 9.608 | 43.316 +/- 6.350 | 59.950 +/- 9.411 | 0.104 +/- 0.180 | - |
| head1m | 10 | 0.05 | 100 | E3/model | 167.655 +/- 0.078 | -45.599 +/- 8.360 | 3.376 +/- 2.652 | 45.724 +/- 4.841 | 69.629 +/- 5.116 | 0.229 +/- 0.130 | 149.431 +/- 3.752 |
| head1m | 10 | 0.05 | 100 | E4/model | 167.655 +/- 0.078 | -25.788 +/- 3.500 | -5.399 +/- 9.137 | 26.347 +/- 3.003 | 54.295 +/- 4.249 | 0.417 +/- 0.095 | 107.918 +/- 1.890 |
| head1m | 10 | 0.05 | 100 | E5/model | 167.655 +/- 0.078 | -20.183 +/- 2.547 | -7.940 +/- 7.817 | 21.689 +/- 2.832 | 45.829 +/- 7.778 | 0.417 +/- 0.095 | - |
| head1m | 10 | 0.05 | 100 | E6/model | 167.655 +/- 0.078 | -38.271 +/- 7.285 | 2.077 +/- 3.362 | 38.327 +/- 4.106 | 62.020 +/- 7.222 | 0.271 +/- 0.201 | - |
| head1m | 10 | 0.05 | 100 | E7/model | 167.655 +/- 0.078 | -21.536 +/- 6.861 | -9.508 +/- 5.978 | 23.541 +/- 4.650 | 47.840 +/- 5.426 | 0.458 +/- 0.036 | 149.431 +/- 3.752 |
| head1m | 10 | 0.05 | 250 | E0 | 167.655 +/- 0.078 | -16.293 +/- 6.630 | 4.968 +/- 0.874 | 17.033 +/- 3.630 | 44.372 +/- 7.638 | 0.812 +/- 0.062 | - |
| head1m | 10 | 0.05 | 250 | E1/model | 167.655 +/- 0.078 | -25.596 +/- 8.611 | -9.224 +/- 4.228 | 27.208 +/- 5.403 | 45.384 +/- 9.853 | 0.375 +/- 0.165 | - |
| head1m | 10 | 0.05 | 250 | E2/model | 167.655 +/- 0.078 | -26.832 +/- 10.067 | -10.275 +/- 6.501 | 28.732 +/- 6.638 | 46.325 +/- 11.752 | 0.354 +/- 0.191 | - |
| head1m | 10 | 0.05 | 250 | E3/model | 167.655 +/- 0.078 | -11.447 +/- 17.454 | 2.156 +/- 1.970 | 11.648 +/- 10.093 | 32.779 +/- 26.797 | 0.854 +/- 0.157 | 147.470 +/- 10.174 |
| head1m | 10 | 0.05 | 250 | E4/model | 167.655 +/- 0.078 | -7.156 +/- 6.494 | -1.228 +/- 1.823 | 7.261 +/- 3.826 | 31.337 +/- 17.636 | 0.896 +/- 0.036 | 69.318 +/- 7.129 |
| head1m | 10 | 0.05 | 250 | E5/model | 167.655 +/- 0.078 | -7.251 +/- 6.467 | -1.772 +/- 1.664 | 7.464 +/- 3.788 | 31.468 +/- 17.590 | 0.896 +/- 0.036 | - |
| head1m | 10 | 0.05 | 250 | E6/model | 167.655 +/- 0.078 | -15.938 +/- 17.937 | 1.591 +/- 1.921 | 16.017 +/- 10.371 | 42.654 +/- 20.372 | 0.812 +/- 0.165 | - |
| head1m | 10 | 0.05 | 250 | E7/model | 167.655 +/- 0.078 | -5.337 +/- 4.791 | -1.056 +/- 3.197 | 5.440 +/- 2.869 | 30.491 +/- 12.183 | 0.896 +/- 0.036 | 147.470 +/- 10.174 |
| head1m | 10 | 0.05 | 1000 | E0 | 167.655 +/- 0.078 | 2.899 +/- 4.243 | 0.992 +/- 0.648 | 3.064 +/- 2.228 | 13.609 +/- 13.868 | 0.979 +/- 0.036 | - |
| head1m | 10 | 0.05 | 1000 | E1/model | 167.655 +/- 0.078 | -6.137 +/- 5.597 | -2.524 +/- 1.297 | 6.635 +/- 3.002 | 15.040 +/- 11.786 | 0.896 +/- 0.130 | - |
| head1m | 10 | 0.05 | 1000 | E2/model | 167.655 +/- 0.078 | -7.664 +/- 5.269 | -2.486 +/- 0.438 | 8.057 +/- 2.870 | 17.818 +/- 12.556 | 0.854 +/- 0.130 | - |
| head1m | 10 | 0.05 | 1000 | E3/model | 167.655 +/- 0.078 | 0.274 +/- 8.541 | -0.000 +/- 0.307 | 0.274 +/- 4.931 | 17.405 +/- 20.767 | 0.958 +/- 0.072 | 162.315 +/- 11.352 |
| head1m | 10 | 0.05 | 1000 | E4/model | 167.655 +/- 0.078 | 4.022 +/- 0.173 | -0.153 +/- 0.040 | 4.025 +/- 0.099 | 4.129 +/- 0.172 | 1.000 +/- 0.000 | 2.329 +/- 0.140 |
| head1m | 10 | 0.05 | 1000 | E5/model | 167.655 +/- 0.078 | 4.033 +/- 0.178 | -0.380 +/- 0.121 | 4.051 +/- 0.108 | 4.191 +/- 0.252 | 1.000 +/- 0.000 | - |
| head1m | 10 | 0.05 | 1000 | E6/model | 167.655 +/- 0.078 | 0.274 +/- 8.540 | -0.021 +/- 0.313 | 0.274 +/- 4.925 | 17.405 +/- 20.767 | 0.958 +/- 0.072 | - |
| head1m | 10 | 0.05 | 1000 | E7/model | 167.655 +/- 0.078 | 5.775 +/- 0.441 | -0.620 +/- 0.103 | 5.808 +/- 0.260 | 6.117 +/- 0.530 | 1.000 +/- 0.000 | 162.315 +/- 11.352 |
| head1m | 10 | 0.1 | 25 | E0 | 335.311 +/- 0.156 | -58.830 +/- 7.377 | 13.482 +/- 0.952 | 60.355 +/- 4.267 | 73.776 +/- 7.665 | 0.271 +/- 0.095 | - |
| head1m | 10 | 0.1 | 25 | E1/model | 335.311 +/- 0.156 | -38.794 +/- 6.010 | -2.633 +/- 8.792 | 38.883 +/- 3.118 | 63.246 +/- 6.262 | 0.083 +/- 0.072 | - |
| head1m | 10 | 0.1 | 25 | E2/model | 335.311 +/- 0.156 | -40.863 +/- 4.764 | -1.326 +/- 7.324 | 40.884 +/- 2.621 | 65.206 +/- 4.544 | 0.083 +/- 0.072 | - |
| head1m | 10 | 0.1 | 25 | E3/model | 335.311 +/- 0.156 | -38.399 +/- 5.009 | 1.952 +/- 5.027 | 38.449 +/- 3.003 | 65.200 +/- 5.358 | 0.104 +/- 0.036 | 291.494 +/- 0.518 |
| head1m | 10 | 0.1 | 25 | E4/model | 335.311 +/- 0.156 | -46.012 +/- 6.211 | 3.541 +/- 1.515 | 46.148 +/- 3.525 | 69.949 +/- 5.868 | 0.208 +/- 0.095 | 285.294 +/- 1.666 |
| head1m | 10 | 0.1 | 25 | E5/model | 335.311 +/- 0.156 | -45.412 +/- 5.672 | 0.328 +/- 0.727 | 45.414 +/- 3.272 | 70.009 +/- 3.683 | 0.167 +/- 0.130 | - |
| head1m | 10 | 0.1 | 25 | E6/model | 335.311 +/- 0.156 | -41.662 +/- 4.468 | -3.840 +/- 6.392 | 41.838 +/- 2.518 | 66.718 +/- 2.662 | 0.104 +/- 0.095 | - |
| head1m | 10 | 0.1 | 25 | E7/model | 335.311 +/- 0.156 | -48.805 +/- 4.190 | 2.687 +/- 1.502 | 48.879 +/- 2.378 | 72.487 +/- 4.579 | 0.188 +/- 0.108 | 291.494 +/- 0.518 |
| head1m | 10 | 0.1 | 100 | E0 | 335.311 +/- 0.156 | -42.520 +/- 9.160 | 10.496 +/- 0.925 | 43.796 +/- 5.232 | 61.702 +/- 11.881 | 0.438 +/- 0.062 | - |
| head1m | 10 | 0.1 | 100 | E1/model | 335.311 +/- 0.156 | -36.988 +/- 2.167 | -2.327 +/- 3.894 | 37.061 +/- 1.185 | 63.689 +/- 4.425 | 0.208 +/- 0.072 | - |
| head1m | 10 | 0.1 | 100 | E2/model | 335.311 +/- 0.156 | -39.316 +/- 1.911 | -4.419 +/- 3.787 | 39.564 +/- 0.875 | 65.385 +/- 4.687 | 0.146 +/- 0.072 | - |
| head1m | 10 | 0.1 | 100 | E3/model | 335.311 +/- 0.156 | -43.371 +/- 7.096 | 3.198 +/- 0.298 | 43.489 +/- 4.077 | 68.827 +/- 7.629 | 0.438 +/- 0.000 | 306.163 +/- 6.345 |
| head1m | 10 | 0.1 | 100 | E4/model | 335.311 +/- 0.156 | -33.877 +/- 10.452 | 1.342 +/- 16.873 | 33.904 +/- 5.756 | 60.329 +/- 8.983 | 0.438 +/- 0.108 | 267.548 +/- 8.859 |
| head1m | 10 | 0.1 | 100 | E5/model | 335.311 +/- 0.156 | -36.362 +/- 12.253 | -0.971 +/- 17.755 | 36.375 +/- 7.306 | 62.912 +/- 11.287 | 0.354 +/- 0.036 | - |
| head1m | 10 | 0.1 | 100 | E6/model | 335.311 +/- 0.156 | -39.413 +/- 2.159 | 9.678 +/- 2.550 | 40.584 +/- 0.968 | 65.418 +/- 3.050 | 0.354 +/- 0.036 | - |
| head1m | 10 | 0.1 | 100 | E7/model | 335.311 +/- 0.156 | -25.805 +/- 4.145 | 1.610 +/- 13.772 | 25.855 +/- 2.826 | 56.941 +/- 0.188 | 0.479 +/- 0.036 | 306.163 +/- 6.345 |
| head1m | 10 | 0.1 | 250 | E0 | 335.311 +/- 0.156 | -34.324 +/- 7.951 | 8.752 +/- 1.552 | 35.422 +/- 4.332 | 58.033 +/- 9.305 | 0.542 +/- 0.072 | - |
| head1m | 10 | 0.1 | 250 | E1/model | 335.311 +/- 0.156 | -30.195 +/- 6.099 | 0.274 +/- 4.092 | 30.197 +/- 3.506 | 48.165 +/- 5.391 | 0.312 +/- 0.125 | - |
| head1m | 10 | 0.1 | 250 | E2/model | 335.311 +/- 0.156 | -32.245 +/- 4.644 | -0.773 +/- 2.699 | 32.255 +/- 2.714 | 50.077 +/- 5.537 | 0.271 +/- 0.144 | - |
| head1m | 10 | 0.1 | 250 | E3/model | 335.311 +/- 0.156 | -25.175 +/- 11.597 | 0.505 +/- 1.398 | 25.180 +/- 6.710 | 51.598 +/- 13.118 | 0.667 +/- 0.130 | 302.076 +/- 6.584 |
| head1m | 10 | 0.1 | 250 | E4/model | 335.311 +/- 0.156 | -2.007 +/- 3.312 | 1.040 +/- 2.737 | 2.260 +/- 1.004 | 18.768 +/- 9.622 | 0.833 +/- 0.036 | 213.376 +/- 5.463 |
| head1m | 10 | 0.1 | 250 | E5/model | 335.311 +/- 0.156 | -2.119 +/- 3.245 | 0.581 +/- 3.342 | 2.198 +/- 1.664 | 19.671 +/- 9.640 | 0.833 +/- 0.036 | - |
| head1m | 10 | 0.1 | 250 | E6/model | 335.311 +/- 0.156 | -26.656 +/- 17.192 | -0.780 +/- 2.606 | 26.668 +/- 9.880 | 51.902 +/- 18.601 | 0.667 +/- 0.144 | - |
| head1m | 10 | 0.1 | 250 | E7/model | 335.311 +/- 0.156 | -9.033 +/- 10.707 | 0.040 +/- 1.229 | 9.033 +/- 6.182 | 31.642 +/- 19.342 | 0.833 +/- 0.072 | 302.076 +/- 6.584 |
| head1m | 10 | 0.1 | 1000 | E0 | 335.311 +/- 0.156 | -0.227 +/- 4.204 | 2.098 +/- 0.244 | 2.110 +/- 0.130 | 21.604 +/- 13.775 | 0.958 +/- 0.036 | - |
| head1m | 10 | 0.1 | 1000 | E1/model | 335.311 +/- 0.156 | -7.581 +/- 6.887 | -3.201 +/- 1.744 | 8.229 +/- 3.352 | 18.471 +/- 13.783 | 0.833 +/- 0.191 | - |
| head1m | 10 | 0.1 | 1000 | E2/model | 335.311 +/- 0.156 | -9.873 +/- 6.825 | -1.271 +/- 3.066 | 9.955 +/- 3.753 | 22.289 +/- 11.429 | 0.771 +/- 0.191 | - |
| head1m | 10 | 0.1 | 1000 | E3/model | 335.311 +/- 0.156 | -0.245 +/- 3.920 | 0.296 +/- 0.416 | 0.384 +/- 1.393 | 21.296 +/- 14.329 | 0.958 +/- 0.036 | 318.053 +/- 14.208 |
| head1m | 10 | 0.1 | 1000 | E4/model | 335.311 +/- 0.156 | 3.910 +/- 0.317 | -0.178 +/- 0.113 | 3.914 +/- 0.184 | 4.010 +/- 0.313 | 1.000 +/- 0.000 | 59.773 +/- 8.750 |
| head1m | 10 | 0.1 | 1000 | E5/model | 335.311 +/- 0.156 | 3.920 +/- 0.322 | -0.430 +/- 0.144 | 3.944 +/- 0.190 | 4.046 +/- 0.324 | 1.000 +/- 0.000 | - |
| head1m | 10 | 0.1 | 1000 | E6/model | 335.311 +/- 0.156 | -0.226 +/- 4.068 | 0.123 +/- 0.632 | 0.258 +/- 1.930 | 21.391 +/- 14.183 | 0.958 +/- 0.036 | - |
| head1m | 10 | 0.1 | 1000 | E7/model | 335.311 +/- 0.156 | 5.648 +/- 0.668 | -0.668 +/- 0.615 | 5.687 +/- 0.403 | 5.984 +/- 0.665 | 1.000 +/- 0.000 | 318.053 +/- 14.208 |
| head1m | 10 | 0.2 | 25 | E0 | 670.622 +/- 0.312 | -51.215 +/- 3.022 | 14.080 +/- 0.551 | 53.115 +/- 1.603 | 65.685 +/- 5.774 | 0.271 +/- 0.095 | - |
| head1m | 10 | 0.2 | 25 | E1/model | 670.622 +/- 0.312 | -56.550 +/- 14.507 | -3.837 +/- 0.887 | 56.680 +/- 8.330 | 77.979 +/- 11.246 | 0.062 +/- 0.062 | - |
| head1m | 10 | 0.2 | 25 | E2/model | 670.622 +/- 0.312 | -56.772 +/- 14.593 | -4.143 +/- 0.829 | 56.923 +/- 8.373 | 78.124 +/- 11.296 | 0.062 +/- 0.062 | - |
| head1m | 10 | 0.2 | 25 | E3/model | 670.622 +/- 0.312 | -64.863 +/- 15.369 | 8.717 +/- 1.599 | 65.446 +/- 8.798 | 82.428 +/- 10.814 | 0.083 +/- 0.095 | 603.633 +/- 4.782 |
| head1m | 10 | 0.2 | 25 | E4/model | 670.622 +/- 0.312 | -52.046 +/- 6.381 | 0.615 +/- 7.698 | 52.050 +/- 3.632 | 76.397 +/- 4.580 | 0.062 +/- 0.062 | 606.603 +/- 3.238 |
| head1m | 10 | 0.2 | 25 | E5/model | 670.622 +/- 0.312 | -50.836 +/- 7.454 | -2.823 +/- 5.703 | 50.914 +/- 4.402 | 75.660 +/- 5.624 | 0.062 +/- 0.062 | - |
| head1m | 10 | 0.2 | 25 | E6/model | 670.622 +/- 0.312 | -56.043 +/- 20.328 | -3.751 +/- 6.295 | 56.169 +/- 11.663 | 77.858 +/- 16.046 | 0.062 +/- 0.062 | - |
| head1m | 10 | 0.2 | 25 | E7/model | 670.622 +/- 0.312 | -52.258 +/- 6.390 | -1.133 +/- 7.673 | 52.270 +/- 3.772 | 75.729 +/- 4.706 | 0.042 +/- 0.036 | 603.633 +/- 4.782 |
| head1m | 10 | 0.2 | 100 | E0 | 670.622 +/- 0.312 | -62.796 +/- 8.693 | 14.974 +/- 0.416 | 64.557 +/- 4.938 | 77.526 +/- 4.714 | 0.208 +/- 0.095 | - |
| head1m | 10 | 0.2 | 100 | E1/model | 670.622 +/- 0.312 | -44.133 +/- 8.459 | -15.207 +/- 9.612 | 46.679 +/- 2.898 | 65.137 +/- 5.299 | 0.083 +/- 0.095 | - |
| head1m | 10 | 0.2 | 100 | E2/model | 670.622 +/- 0.312 | -44.840 +/- 8.773 | -14.031 +/- 11.216 | 46.984 +/- 3.031 | 65.357 +/- 5.090 | 0.083 +/- 0.095 | - |
| head1m | 10 | 0.2 | 100 | E3/model | 670.622 +/- 0.312 | -56.461 +/- 5.578 | -2.046 +/- 0.666 | 56.498 +/- 3.231 | 76.327 +/- 3.868 | 0.250 +/- 0.125 | 623.431 +/- 3.070 |
| head1m | 10 | 0.2 | 100 | E4/model | 670.622 +/- 0.312 | -38.454 +/- 4.769 | -4.676 +/- 6.933 | 38.737 +/- 3.188 | 62.217 +/- 5.639 | 0.292 +/- 0.072 | 589.215 +/- 2.634 |
| head1m | 10 | 0.2 | 100 | E5/model | 670.622 +/- 0.312 | -37.606 +/- 5.506 | -1.676 +/- 8.154 | 37.644 +/- 3.385 | 61.351 +/- 5.104 | 0.250 +/- 0.062 | - |
| head1m | 10 | 0.2 | 100 | E6/model | 670.622 +/- 0.312 | -48.865 +/- 9.135 | -7.306 +/- 4.166 | 49.408 +/- 5.269 | 68.332 +/- 7.318 | 0.188 +/- 0.125 | - |
| head1m | 10 | 0.2 | 100 | E7/model | 670.622 +/- 0.312 | -37.636 +/- 10.003 | -3.898 +/- 6.211 | 37.837 +/- 6.082 | 60.746 +/- 8.795 | 0.271 +/- 0.095 | 623.431 +/- 3.070 |
| head1m | 10 | 0.2 | 250 | E0 | 670.622 +/- 0.312 | -31.085 +/- 1.428 | 12.444 +/- 0.509 | 33.483 +/- 0.805 | 54.148 +/- 1.771 | 0.542 +/- 0.036 | - |
| head1m | 10 | 0.2 | 250 | E1/model | 670.622 +/- 0.312 | -38.558 +/- 2.795 | -3.044 +/- 5.434 | 38.678 +/- 1.778 | 57.793 +/- 2.435 | 0.208 +/- 0.072 | - |
| head1m | 10 | 0.2 | 250 | E2/model | 670.622 +/- 0.312 | -37.493 +/- 2.758 | -1.657 +/- 1.882 | 37.530 +/- 1.580 | 55.773 +/- 0.579 | 0.188 +/- 0.062 | - |
| head1m | 10 | 0.2 | 250 | E3/model | 670.622 +/- 0.312 | -34.358 +/- 16.979 | 2.591 +/- 5.585 | 34.455 +/- 9.905 | 61.131 +/- 16.609 | 0.542 +/- 0.130 | 635.563 +/- 13.887 |
| head1m | 10 | 0.2 | 250 | E4/model | 670.622 +/- 0.312 | -14.839 +/- 7.547 | 2.825 +/- 3.167 | 15.105 +/- 4.621 | 42.898 +/- 10.726 | 0.792 +/- 0.095 | 555.550 +/- 13.744 |
| head1m | 10 | 0.2 | 250 | E5/model | 670.622 +/- 0.312 | -15.576 +/- 10.676 | 2.231 +/- 2.840 | 15.735 +/- 6.260 | 43.113 +/- 13.510 | 0.750 +/- 0.125 | - |
| head1m | 10 | 0.2 | 250 | E6/model | 670.622 +/- 0.312 | -27.509 +/- 17.325 | 1.743 +/- 5.722 | 27.564 +/- 10.063 | 54.841 +/- 20.113 | 0.604 +/- 0.157 | - |
| head1m | 10 | 0.2 | 250 | E7/model | 670.622 +/- 0.312 | -23.838 +/- 1.938 | 1.734 +/- 4.205 | 23.901 +/- 1.104 | 55.435 +/- 2.329 | 0.729 +/- 0.036 | 635.563 +/- 13.887 |
| head1m | 10 | 0.2 | 1000 | E0 | 670.622 +/- 0.312 | -3.951 +/- 7.066 | 3.624 +/- 0.255 | 5.361 +/- 2.965 | 26.114 +/- 17.493 | 0.938 +/- 0.062 | - |
| head1m | 10 | 0.2 | 1000 | E1/model | 670.622 +/- 0.312 | -7.233 +/- 4.087 | -3.101 +/- 2.994 | 7.869 +/- 2.848 | 23.145 +/- 5.082 | 0.833 +/- 0.095 | - |
| head1m | 10 | 0.2 | 1000 | E2/model | 670.622 +/- 0.312 | -7.332 +/- 4.261 | -2.320 +/- 2.913 | 7.691 +/- 2.852 | 22.971 +/- 5.190 | 0.833 +/- 0.095 | - |
| head1m | 10 | 0.2 | 1000 | E3/model | 670.622 +/- 0.312 | -0.685 +/- 8.691 | 0.125 +/- 0.827 | 0.697 +/- 4.928 | 17.451 +/- 20.687 | 0.958 +/- 0.072 | 633.604 +/- 3.700 |
| head1m | 10 | 0.2 | 1000 | E4/model | 670.622 +/- 0.312 | 4.059 +/- 0.188 | 0.020 +/- 0.233 | 4.059 +/- 0.109 | 4.162 +/- 0.184 | 1.000 +/- 0.000 | 352.704 +/- 3.932 |
| head1m | 10 | 0.2 | 1000 | E5/model | 670.622 +/- 0.312 | 4.050 +/- 0.195 | -0.248 +/- 0.262 | 4.057 +/- 0.110 | 4.163 +/- 0.189 | 1.000 +/- 0.000 | - |
| head1m | 10 | 0.2 | 1000 | E6/model | 670.622 +/- 0.312 | -0.690 +/- 8.696 | -0.116 +/- 0.734 | 0.700 +/- 4.959 | 17.427 +/- 20.710 | 0.958 +/- 0.072 | - |
| head1m | 10 | 0.2 | 1000 | E7/model | 670.622 +/- 0.312 | 5.779 +/- 0.334 | -0.863 +/- 0.829 | 5.843 +/- 0.128 | 6.288 +/- 0.066 | 1.000 +/- 0.000 | 633.604 +/- 3.700 |
| head5m | 10 | 0 | 25 | E0 | 0.000 +/- 0.000 | -261.123 +/- 22.911 | -15.764 +/- 52.978 | 261.598 +/- 14.449 | 383.746 +/- 21.476 | 0.229 +/- 0.157 | - |
| head5m | 10 | 0 | 25 | E1/model | 0.000 +/- 0.000 | -212.971 +/- 57.428 | -14.694 +/- 21.784 | 213.478 +/- 32.956 | 314.576 +/- 69.037 | 0.146 +/- 0.157 | - |
| head5m | 10 | 0 | 25 | E2/model | 0.000 +/- 0.000 | -215.915 +/- 55.289 | -18.782 +/- 21.163 | 216.730 +/- 31.724 | 315.821 +/- 67.767 | 0.146 +/- 0.157 | - |
| head5m | 10 | 0 | 25 | E3/model | 0.000 +/- 0.000 | -257.524 +/- 20.030 | 5.931 +/- 56.768 | 257.592 +/- 11.477 | 377.759 +/- 16.459 | 0.229 +/- 0.157 | 0.225 +/- 0.122 |
| head5m | 10 | 0 | 25 | E4/model | 0.000 +/- 0.000 | -179.193 +/- 49.692 | 61.316 +/- 43.102 | 189.393 +/- 30.982 | 298.033 +/- 37.939 | 0.333 +/- 0.260 | 0.000 +/- 0.000 |
| head5m | 10 | 0 | 25 | E5/model | 0.000 +/- 0.000 | -179.212 +/- 49.682 | 61.332 +/- 43.088 | 189.417 +/- 30.974 | 298.050 +/- 37.941 | 0.333 +/- 0.260 | - |
| head5m | 10 | 0 | 25 | E6/model | 0.000 +/- 0.000 | -261.123 +/- 22.911 | -15.764 +/- 52.978 | 261.598 +/- 14.449 | 383.746 +/- 21.476 | 0.229 +/- 0.157 | - |
| head5m | 10 | 0 | 25 | E7/model | 0.000 +/- 0.000 | -169.556 +/- 56.200 | 47.841 +/- 14.115 | 176.176 +/- 33.434 | 303.956 +/- 39.377 | 0.375 +/- 0.225 | 0.225 +/- 0.122 |
| head5m | 10 | 0 | 100 | E0 | 0.000 +/- 0.000 | -63.701 +/- 43.696 | -33.493 +/- 19.178 | 71.970 +/- 27.025 | 150.921 +/- 104.557 | 0.854 +/- 0.072 | - |
| head5m | 10 | 0 | 100 | E1/model | 0.000 +/- 0.000 | -88.450 +/- 18.964 | -52.787 +/- 36.916 | 103.004 +/- 10.154 | 192.156 +/- 27.266 | 0.688 +/- 0.000 | - |
| head5m | 10 | 0 | 100 | E2/model | 0.000 +/- 0.000 | -85.825 +/- 19.296 | -63.250 +/- 27.548 | 106.613 +/- 12.970 | 184.479 +/- 28.358 | 0.688 +/- 0.000 | - |
| head5m | 10 | 0 | 100 | E3/model | 0.000 +/- 0.000 | -68.424 +/- 43.691 | -26.246 +/- 27.988 | 73.285 +/- 26.920 | 155.062 +/- 105.660 | 0.854 +/- 0.072 | 1.540 +/- 0.785 |
| head5m | 10 | 0 | 100 | E4/model | 0.000 +/- 0.000 | -34.401 +/- 19.384 | -7.296 +/- 28.000 | 35.166 +/- 12.002 | 146.523 +/- 53.956 | 0.854 +/- 0.036 | 0.000 +/- 0.000 |
| head5m | 10 | 0 | 100 | E5/model | 0.000 +/- 0.000 | -34.430 +/- 19.356 | -7.275 +/- 28.007 | 35.190 +/- 11.984 | 146.547 +/- 53.910 | 0.854 +/- 0.036 | - |
| head5m | 10 | 0 | 100 | E6/model | 0.000 +/- 0.000 | -63.701 +/- 43.696 | -33.493 +/- 19.178 | 71.970 +/- 27.025 | 150.921 +/- 104.557 | 0.854 +/- 0.072 | - |
| head5m | 10 | 0 | 100 | E7/model | 0.000 +/- 0.000 | -9.376 +/- 22.473 | -23.705 +/- 17.883 | 25.492 +/- 12.776 | 123.550 +/- 72.995 | 0.917 +/- 0.095 | 1.540 +/- 0.785 |
| head5m | 10 | 0 | 250 | E0 | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 10 | 0 | 250 | E1/model | 0.000 +/- 0.000 | -49.321 +/- 12.639 | -35.234 +/- 20.524 | 60.613 +/- 4.581 | 128.392 +/- 34.871 | 0.875 +/- 0.000 | - |
| head5m | 10 | 0 | 250 | E2/model | 0.000 +/- 0.000 | -55.167 +/- 12.684 | -39.497 +/- 20.454 | 67.848 +/- 4.642 | 130.836 +/- 34.661 | 0.875 +/- 0.000 | - |
| head5m | 10 | 0 | 250 | E3/model | 0.000 +/- 0.000 | -28.513 +/- 20.332 | -19.048 +/- 2.210 | 34.290 +/- 10.016 | 67.015 +/- 69.538 | 0.979 +/- 0.036 | 3.146 +/- 0.872 |
| head5m | 10 | 0 | 250 | E4/model | 0.000 +/- 0.000 | 12.065 +/- 2.596 | -2.070 +/- 1.007 | 12.242 +/- 1.444 | 13.328 +/- 2.556 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 10 | 0 | 250 | E5/model | 0.000 +/- 0.000 | 12.046 +/- 2.588 | -2.041 +/- 1.004 | 12.218 +/- 1.441 | 13.309 +/- 2.543 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0 | 250 | E6/model | 0.000 +/- 0.000 | -40.252 +/- 20.332 | -19.047 +/- 2.210 | 44.531 +/- 10.262 | 107.208 +/- 69.594 | 0.958 +/- 0.036 | - |
| head5m | 10 | 0 | 250 | E7/model | 0.000 +/- 0.000 | 11.471 +/- 28.916 | -3.732 +/- 6.127 | 12.063 +/- 16.970 | 82.758 +/- 75.940 | 0.958 +/- 0.072 | 3.146 +/- 0.872 |
| head5m | 10 | 0 | 1000 | E0 | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -35.366 +/- 0.053 | 39.143 +/- 0.028 | 39.144 +/- 0.048 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | -23.404 +/- 0.000 | -39.773 +/- 0.000 | 46.148 +/- 0.000 | 46.148 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | 14.952 +/- 2.839 |
| head5m | 10 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 10.835 +/- 0.552 | -1.561 +/- 0.401 | 10.947 +/- 0.299 | 11.382 +/- 0.639 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 10 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 10.795 +/- 0.561 | -1.532 +/- 0.403 | 10.903 +/- 0.305 | 11.339 +/- 0.647 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 34.562 +/- 2.498 | -10.598 +/- 1.786 | 36.150 +/- 1.681 | 39.069 +/- 0.896 | 1.000 +/- 0.000 | 14.952 +/- 2.839 |
| head5m | 10 | 0.05 | 25 | E0 | 167.655 +/- 0.078 | -325.206 +/- 50.914 | 28.231 +/- 13.239 | 326.429 +/- 29.238 | 391.774 +/- 38.653 | 0.146 +/- 0.130 | - |
| head5m | 10 | 0.05 | 25 | E1/model | 167.655 +/- 0.078 | -268.884 +/- 48.986 | -75.462 +/- 77.466 | 279.273 +/- 23.044 | 370.483 +/- 40.188 | 0.083 +/- 0.072 | - |
| head5m | 10 | 0.05 | 25 | E2/model | 167.655 +/- 0.078 | -276.126 +/- 53.565 | -72.364 +/- 74.137 | 285.451 +/- 24.842 | 375.200 +/- 41.143 | 0.083 +/- 0.072 | - |
| head5m | 10 | 0.05 | 25 | E3/model | 167.655 +/- 0.078 | -287.783 +/- 33.258 | -19.030 +/- 93.402 | 288.411 +/- 17.661 | 384.230 +/- 22.558 | 0.146 +/- 0.130 | 141.875 +/- 1.994 |
| head5m | 10 | 0.05 | 25 | E4/model | 167.655 +/- 0.078 | -226.144 +/- 9.602 | 29.905 +/- 16.463 | 228.113 +/- 5.917 | 335.678 +/- 9.799 | 0.167 +/- 0.072 | 125.289 +/- 2.269 |
| head5m | 10 | 0.05 | 25 | E5/model | 167.655 +/- 0.078 | -219.326 +/- 18.773 | 14.525 +/- 23.306 | 219.807 +/- 11.694 | 331.294 +/- 5.506 | 0.188 +/- 0.108 | - |
| head5m | 10 | 0.05 | 25 | E6/model | 167.655 +/- 0.078 | -297.143 +/- 36.038 | -40.255 +/- 60.789 | 299.857 +/- 22.691 | 391.117 +/- 26.129 | 0.104 +/- 0.095 | - |
| head5m | 10 | 0.05 | 25 | E7/model | 167.655 +/- 0.078 | -216.837 +/- 41.762 | 6.201 +/- 18.804 | 216.926 +/- 24.254 | 336.581 +/- 30.885 | 0.188 +/- 0.125 | 141.875 +/- 1.994 |
| head5m | 10 | 0.05 | 100 | E0 | 167.655 +/- 0.078 | -167.865 +/- 77.274 | 16.663 +/- 1.162 | 168.690 +/- 44.458 | 279.910 +/- 84.147 | 0.646 +/- 0.095 | - |
| head5m | 10 | 0.05 | 100 | E1/model | 167.655 +/- 0.078 | -181.216 +/- 39.798 | -27.444 +/- 55.482 | 183.282 +/- 18.350 | 297.753 +/- 44.080 | 0.375 +/- 0.125 | - |
| head5m | 10 | 0.05 | 100 | E2/model | 167.655 +/- 0.078 | -184.152 +/- 38.829 | -31.487 +/- 55.069 | 186.824 +/- 17.230 | 299.668 +/- 43.363 | 0.375 +/- 0.125 | - |
| head5m | 10 | 0.05 | 100 | E3/model | 167.655 +/- 0.078 | -193.320 +/- 47.002 | 0.493 +/- 17.120 | 193.320 +/- 27.157 | 322.951 +/- 48.358 | 0.542 +/- 0.072 | 138.057 +/- 7.238 |
| head5m | 10 | 0.05 | 100 | E4/model | 167.655 +/- 0.078 | -126.795 +/- 40.029 | -5.923 +/- 38.558 | 126.933 +/- 22.874 | 249.740 +/- 31.807 | 0.521 +/- 0.144 | 108.888 +/- 7.185 |
| head5m | 10 | 0.05 | 100 | E5/model | 167.655 +/- 0.078 | -116.896 +/- 50.782 | -2.828 +/- 28.322 | 116.930 +/- 29.226 | 232.869 +/- 50.274 | 0.542 +/- 0.130 | - |
| head5m | 10 | 0.05 | 100 | E6/model | 167.655 +/- 0.078 | -204.919 +/- 60.907 | -12.618 +/- 40.387 | 205.307 +/- 34.466 | 336.411 +/- 60.187 | 0.479 +/- 0.072 | - |
| head5m | 10 | 0.05 | 100 | E7/model | 167.655 +/- 0.078 | -93.910 +/- 64.280 | 22.035 +/- 5.849 | 96.461 +/- 36.792 | 234.620 +/- 76.782 | 0.646 +/- 0.095 | 138.057 +/- 7.238 |
| head5m | 10 | 0.05 | 250 | E0 | 167.655 +/- 0.078 | -86.791 +/- 0.718 | -8.702 +/- 1.914 | 87.227 +/- 0.522 | 205.923 +/- 1.931 | 0.875 +/- 0.000 | - |
| head5m | 10 | 0.05 | 250 | E1/model | 167.655 +/- 0.078 | -82.385 +/- 5.905 | -49.226 +/- 19.770 | 95.971 +/- 2.963 | 184.721 +/- 20.625 | 0.771 +/- 0.072 | - |
| head5m | 10 | 0.05 | 250 | E2/model | 167.655 +/- 0.078 | -87.206 +/- 5.975 | -53.307 +/- 19.544 | 102.208 +/- 3.241 | 185.833 +/- 21.997 | 0.771 +/- 0.072 | - |
| head5m | 10 | 0.05 | 250 | E3/model | 167.655 +/- 0.078 | -63.314 +/- 20.700 | -16.548 +/- 3.342 | 65.441 +/- 11.559 | 165.615 +/- 35.767 | 0.917 +/- 0.036 | 99.271 +/- 7.578 |
| head5m | 10 | 0.05 | 250 | E4/model | 167.655 +/- 0.078 | -12.569 +/- 21.556 | -0.760 +/- 0.534 | 12.592 +/- 12.439 | 97.429 +/- 72.590 | 0.958 +/- 0.036 | 42.273 +/- 7.958 |
| head5m | 10 | 0.05 | 250 | E5/model | 167.655 +/- 0.078 | -1.171 +/- 21.456 | -1.500 +/- 0.639 | 1.903 +/- 7.790 | 55.528 +/- 74.707 | 0.979 +/- 0.036 | - |
| head5m | 10 | 0.05 | 250 | E6/model | 167.655 +/- 0.078 | -51.576 +/- 0.718 | -21.854 +/- 0.461 | 56.015 +/- 0.278 | 146.040 +/- 2.734 | 0.938 +/- 0.000 | - |
| head5m | 10 | 0.05 | 250 | E7/model | 167.655 +/- 0.078 | -47.279 +/- 17.855 | -5.844 +/- 4.710 | 47.639 +/- 9.973 | 199.243 +/- 16.198 | 0.875 +/- 0.000 | 99.271 +/- 7.578 |
| head5m | 10 | 0.05 | 1000 | E0 | 167.655 +/- 0.078 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.05 | 1000 | E1/model | 167.655 +/- 0.078 | -16.775 +/- 0.000 | -35.398 +/- 0.035 | 39.171 +/- 0.018 | 39.172 +/- 0.032 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.05 | 1000 | E2/model | 167.655 +/- 0.078 | -23.404 +/- 0.000 | -39.773 +/- 0.000 | 46.148 +/- 0.000 | 46.148 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.05 | 1000 | E3/model | 167.655 +/- 0.078 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | 72.080 +/- 7.438 |
| head5m | 10 | 0.05 | 1000 | E4/model | 167.655 +/- 0.078 | 10.584 +/- 0.124 | -1.223 +/- 0.182 | 10.654 +/- 0.080 | 11.258 +/- 0.201 | 1.000 +/- 0.000 | 0.116 +/- 0.086 |
| head5m | 10 | 0.05 | 1000 | E5/model | 167.655 +/- 0.078 | 10.430 +/- 0.136 | -1.514 +/- 0.174 | 10.539 +/- 0.085 | 11.109 +/- 0.175 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.05 | 1000 | E6/model | 167.655 +/- 0.078 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.05 | 1000 | E7/model | 167.655 +/- 0.078 | 34.425 +/- 1.666 | -8.605 +/- 4.694 | 35.484 +/- 1.589 | 39.791 +/- 0.108 | 1.000 +/- 0.000 | 72.080 +/- 7.438 |
| head5m | 10 | 0.1 | 25 | E0 | 335.311 +/- 0.156 | -299.864 +/- 25.593 | 29.506 +/- 5.255 | 301.313 +/- 14.972 | 370.322 +/- 29.637 | 0.188 +/- 0.062 | - |
| head5m | 10 | 0.1 | 25 | E1/model | 335.311 +/- 0.156 | -270.324 +/- 60.828 | -56.748 +/- 15.715 | 276.217 +/- 32.696 | 371.506 +/- 69.034 | 0.021 +/- 0.036 | - |
| head5m | 10 | 0.1 | 25 | E2/model | 335.311 +/- 0.156 | -273.088 +/- 61.592 | -60.488 +/- 15.743 | 279.707 +/- 32.989 | 374.806 +/- 68.569 | 0.021 +/- 0.036 | - |
| head5m | 10 | 0.1 | 25 | E3/model | 335.311 +/- 0.156 | -274.438 +/- 61.028 | -29.453 +/- 31.978 | 276.014 +/- 33.712 | 377.781 +/- 59.913 | 0.104 +/- 0.036 | 296.682 +/- 5.146 |
| head5m | 10 | 0.1 | 25 | E4/model | 335.311 +/- 0.156 | -198.880 +/- 76.373 | -7.234 +/- 4.805 | 199.012 +/- 44.056 | 305.121 +/- 64.261 | 0.125 +/- 0.062 | 280.968 +/- 5.941 |
| head5m | 10 | 0.1 | 25 | E5/model | 335.311 +/- 0.156 | -204.913 +/- 50.156 | -13.054 +/- 25.294 | 205.328 +/- 29.827 | 315.305 +/- 34.213 | 0.104 +/- 0.036 | - |
| head5m | 10 | 0.1 | 25 | E6/model | 335.311 +/- 0.156 | -284.401 +/- 58.297 | -52.252 +/- 7.314 | 289.162 +/- 33.828 | 381.084 +/- 67.132 | 0.021 +/- 0.036 | - |
| head5m | 10 | 0.1 | 25 | E7/model | 335.311 +/- 0.156 | -202.811 +/- 41.831 | -21.920 +/- 26.173 | 203.992 +/- 24.358 | 321.982 +/- 46.181 | 0.125 +/- 0.000 | 296.682 +/- 5.146 |
| head5m | 10 | 0.1 | 100 | E0 | 335.311 +/- 0.156 | -146.619 +/- 45.844 | 28.408 +/- 5.373 | 149.346 +/- 26.359 | 260.074 +/- 50.598 | 0.667 +/- 0.095 | - |
| head5m | 10 | 0.1 | 100 | E1/model | 335.311 +/- 0.156 | -223.360 +/- 33.153 | -46.892 +/- 40.745 | 228.229 +/- 20.632 | 338.998 +/- 28.769 | 0.188 +/- 0.000 | - |
| head5m | 10 | 0.1 | 100 | E2/model | 335.311 +/- 0.156 | -220.610 +/- 23.334 | -46.402 +/- 38.034 | 225.437 +/- 14.229 | 333.395 +/- 13.550 | 0.188 +/- 0.000 | - |
| head5m | 10 | 0.1 | 100 | E3/model | 335.311 +/- 0.156 | -182.463 +/- 52.458 | 15.207 +/- 43.670 | 183.096 +/- 28.672 | 307.969 +/- 51.173 | 0.479 +/- 0.095 | 287.970 +/- 2.372 |
| head5m | 10 | 0.1 | 100 | E4/model | 335.311 +/- 0.156 | -123.339 +/- 65.645 | 0.325 +/- 30.216 | 123.339 +/- 37.855 | 245.976 +/- 68.012 | 0.542 +/- 0.157 | 252.383 +/- 0.447 |
| head5m | 10 | 0.1 | 100 | E5/model | 335.311 +/- 0.156 | -112.267 +/- 51.878 | -9.453 +/- 30.031 | 112.664 +/- 31.021 | 240.185 +/- 56.830 | 0.542 +/- 0.157 | - |
| head5m | 10 | 0.1 | 100 | E6/model | 335.311 +/- 0.156 | -209.907 +/- 58.906 | -16.989 +/- 40.861 | 210.594 +/- 35.450 | 340.480 +/- 53.335 | 0.438 +/- 0.125 | - |
| head5m | 10 | 0.1 | 100 | E7/model | 335.311 +/- 0.156 | -107.028 +/- 52.688 | -11.647 +/- 28.347 | 107.660 +/- 30.588 | 238.462 +/- 58.249 | 0.583 +/- 0.191 | 287.970 +/- 2.372 |
| head5m | 10 | 0.1 | 250 | E0 | 335.311 +/- 0.156 | -56.565 +/- 4.101 | 22.694 +/- 5.200 | 60.947 +/- 3.114 | 151.517 +/- 2.161 | 0.896 +/- 0.036 | - |
| head5m | 10 | 0.1 | 250 | E1/model | 335.311 +/- 0.156 | -94.750 +/- 36.850 | -22.048 +/- 26.662 | 97.281 +/- 21.930 | 197.384 +/- 50.550 | 0.708 +/- 0.130 | - |
| head5m | 10 | 0.1 | 250 | E2/model | 335.311 +/- 0.156 | -104.330 +/- 42.754 | -31.211 +/- 32.308 | 108.898 +/- 26.892 | 205.301 +/- 58.428 | 0.688 +/- 0.165 | - |
| head5m | 10 | 0.1 | 250 | E3/model | 335.311 +/- 0.156 | -66.020 +/- 17.741 | 0.453 +/- 6.075 | 66.022 +/- 10.263 | 171.096 +/- 36.180 | 0.875 +/- 0.062 | 263.812 +/- 1.948 |
| head5m | 10 | 0.1 | 250 | E4/model | 335.311 +/- 0.156 | 5.484 +/- 9.924 | 9.941 +/- 16.590 | 11.353 +/- 5.620 | 43.718 +/- 54.160 | 0.958 +/- 0.072 | 207.901 +/- 4.126 |
| head5m | 10 | 0.1 | 250 | E5/model | 335.311 +/- 0.156 | 2.425 +/- 7.514 | 11.230 +/- 15.244 | 11.489 +/- 7.834 | 55.637 +/- 47.781 | 0.938 +/- 0.062 | - |
| head5m | 10 | 0.1 | 250 | E6/model | 335.311 +/- 0.156 | -66.042 +/- 17.731 | -1.063 +/- 14.099 | 66.050 +/- 10.210 | 174.448 +/- 34.235 | 0.875 +/- 0.062 | - |
| head5m | 10 | 0.1 | 250 | E7/model | 335.311 +/- 0.156 | -20.180 +/- 34.877 | 1.767 +/- 8.865 | 20.257 +/- 19.877 | 138.966 +/- 76.512 | 0.917 +/- 0.036 | 263.812 +/- 1.948 |
| head5m | 10 | 0.1 | 1000 | E0 | 335.311 +/- 0.156 | -16.775 +/- 0.000 | -18.780 +/- 1.917 | 25.181 +/- 0.825 | 26.296 +/- 0.484 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.1 | 1000 | E1/model | 335.311 +/- 0.156 | -21.151 +/- 7.579 | -31.556 +/- 6.736 | 37.989 +/- 0.795 | 52.212 +/- 22.508 | 0.979 +/- 0.036 | - |
| head5m | 10 | 0.1 | 1000 | E2/model | 335.311 +/- 0.156 | -27.547 +/- 7.176 | -35.906 +/- 6.697 | 45.255 +/- 0.546 | 57.460 +/- 19.593 | 0.979 +/- 0.036 | - |
| head5m | 10 | 0.1 | 1000 | E3/model | 335.311 +/- 0.156 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | 182.880 +/- 3.497 |
| head5m | 10 | 0.1 | 1000 | E4/model | 335.311 +/- 0.156 | 9.920 +/- 0.837 | -0.887 +/- 0.053 | 9.960 +/- 0.482 | 10.449 +/- 0.878 | 1.000 +/- 0.000 | 18.931 +/- 1.470 |
| head5m | 10 | 0.1 | 1000 | E5/model | 335.311 +/- 0.156 | 10.007 +/- 0.851 | -1.557 +/- 0.047 | 10.128 +/- 0.484 | 10.553 +/- 0.832 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.1 | 1000 | E6/model | 335.311 +/- 0.156 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.1 | 1000 | E7/model | 335.311 +/- 0.156 | 35.734 +/- 2.799 | -8.662 +/- 1.086 | 36.769 +/- 1.596 | 41.405 +/- 1.343 | 1.000 +/- 0.000 | 182.880 +/- 3.497 |
| head5m | 10 | 0.2 | 25 | E0 | 670.622 +/- 0.312 | -329.997 +/- 54.752 | 39.107 +/- 1.081 | 332.306 +/- 31.323 | 398.643 +/- 45.664 | 0.146 +/- 0.036 | - |
| head5m | 10 | 0.2 | 25 | E1/model | 670.622 +/- 0.312 | -304.110 +/- 19.720 | -30.130 +/- 19.686 | 305.599 +/- 10.368 | 414.012 +/- 8.524 | 0.042 +/- 0.036 | - |
| head5m | 10 | 0.2 | 25 | E2/model | 670.622 +/- 0.312 | -310.651 +/- 24.890 | -37.287 +/- 43.127 | 312.881 +/- 11.455 | 414.326 +/- 11.329 | 0.042 +/- 0.036 | - |
| head5m | 10 | 0.2 | 25 | E3/model | 670.622 +/- 0.312 | -330.508 +/- 20.409 | 3.118 +/- 37.988 | 330.522 +/- 11.578 | 420.851 +/- 39.298 | 0.042 +/- 0.036 | 610.313 +/- 3.128 |
| head5m | 10 | 0.2 | 25 | E4/model | 670.622 +/- 0.312 | -267.275 +/- 47.689 | 35.844 +/- 26.820 | 269.668 +/- 26.708 | 378.634 +/- 22.721 | 0.083 +/- 0.036 | 606.544 +/- 4.431 |
| head5m | 10 | 0.2 | 25 | E5/model | 670.622 +/- 0.312 | -246.068 +/- 58.913 | 16.638 +/- 15.652 | 246.630 +/- 33.838 | 363.526 +/- 30.731 | 0.062 +/- 0.062 | - |
| head5m | 10 | 0.2 | 25 | E6/model | 670.622 +/- 0.312 | -334.099 +/- 27.087 | -31.023 +/- 47.162 | 335.536 +/- 14.900 | 434.528 +/- 24.856 | 0.021 +/- 0.036 | - |
| head5m | 10 | 0.2 | 25 | E7/model | 670.622 +/- 0.312 | -268.220 +/- 9.355 | 1.455 +/- 54.485 | 268.224 +/- 5.429 | 385.092 +/- 25.637 | 0.042 +/- 0.072 | 610.313 +/- 3.128 |
| head5m | 10 | 0.2 | 100 | E0 | 670.622 +/- 0.312 | -153.012 +/- 16.809 | 37.391 +/- 0.142 | 157.515 +/- 9.413 | 260.629 +/- 15.050 | 0.562 +/- 0.062 | - |
| head5m | 10 | 0.2 | 100 | E1/model | 670.622 +/- 0.312 | -253.761 +/- 23.973 | -28.796 +/- 35.211 | 255.389 +/- 13.342 | 368.816 +/- 21.479 | 0.167 +/- 0.130 | - |
| head5m | 10 | 0.2 | 100 | E2/model | 670.622 +/- 0.312 | -256.516 +/- 25.463 | -32.039 +/- 35.873 | 258.509 +/- 14.083 | 371.920 +/- 20.932 | 0.167 +/- 0.130 | - |
| head5m | 10 | 0.2 | 100 | E3/model | 670.622 +/- 0.312 | -242.407 +/- 40.030 | 22.626 +/- 39.305 | 243.460 +/- 21.005 | 368.771 +/- 37.211 | 0.292 +/- 0.036 | 605.203 +/- 15.439 |
| head5m | 10 | 0.2 | 100 | E4/model | 670.622 +/- 0.312 | -166.746 +/- 80.209 | 5.075 +/- 19.817 | 166.823 +/- 46.058 | 277.015 +/- 76.672 | 0.375 +/- 0.272 | 575.029 +/- 15.105 |
| head5m | 10 | 0.2 | 100 | E5/model | 670.622 +/- 0.312 | -177.853 +/- 63.003 | -0.077 +/- 36.532 | 177.853 +/- 36.375 | 291.814 +/- 49.059 | 0.333 +/- 0.260 | - |
| head5m | 10 | 0.2 | 100 | E6/model | 670.622 +/- 0.312 | -260.009 +/- 14.802 | 9.606 +/- 21.813 | 260.186 +/- 8.524 | 378.250 +/- 15.698 | 0.229 +/- 0.157 | - |
| head5m | 10 | 0.2 | 100 | E7/model | 670.622 +/- 0.312 | -176.271 +/- 26.392 | 3.237 +/- 28.029 | 176.301 +/- 15.089 | 309.778 +/- 17.259 | 0.375 +/- 0.225 | 605.203 +/- 15.439 |
| head5m | 10 | 0.2 | 250 | E0 | 670.622 +/- 0.312 | -52.227 +/- 35.572 | 33.361 +/- 1.790 | 61.973 +/- 16.847 | 128.160 +/- 80.267 | 0.917 +/- 0.095 | - |
| head5m | 10 | 0.2 | 250 | E1/model | 670.622 +/- 0.312 | -133.506 +/- 42.611 | -39.034 +/- 25.085 | 139.095 +/- 22.104 | 243.494 +/- 49.407 | 0.562 +/- 0.062 | - |
| head5m | 10 | 0.2 | 250 | E2/model | 670.622 +/- 0.312 | -137.750 +/- 42.791 | -42.259 +/- 24.285 | 144.086 +/- 22.159 | 244.640 +/- 49.713 | 0.562 +/- 0.062 | - |
| head5m | 10 | 0.2 | 250 | E3/model | 670.622 +/- 0.312 | -97.444 +/- 17.699 | -2.135 +/- 10.897 | 97.467 +/- 10.092 | 217.791 +/- 29.495 | 0.792 +/- 0.036 | 592.395 +/- 2.773 |
| head5m | 10 | 0.2 | 250 | E4/model | 670.622 +/- 0.312 | -20.691 +/- 27.590 | -5.390 +/- 10.323 | 21.382 +/- 16.669 | 107.934 +/- 86.939 | 0.938 +/- 0.062 | 526.255 +/- 3.785 |
| head5m | 10 | 0.2 | 250 | E5/model | 670.622 +/- 0.312 | -21.286 +/- 28.474 | -6.022 +/- 10.369 | 22.121 +/- 17.204 | 107.786 +/- 86.828 | 0.938 +/- 0.062 | - |
| head5m | 10 | 0.2 | 250 | E6/model | 670.622 +/- 0.312 | -78.475 +/- 39.617 | -8.337 +/- 8.111 | 78.917 +/- 22.500 | 187.272 +/- 59.546 | 0.812 +/- 0.000 | - |
| head5m | 10 | 0.2 | 250 | E7/model | 670.622 +/- 0.312 | -53.131 +/- 43.463 | -6.128 +/- 5.514 | 53.483 +/- 25.134 | 187.892 +/- 62.928 | 0.854 +/- 0.072 | 592.395 +/- 2.773 |
| head5m | 10 | 0.2 | 1000 | E0 | 670.622 +/- 0.312 | -16.775 +/- 0.000 | -2.086 +/- 10.199 | 16.904 +/- 0.727 | 30.263 +/- 2.329 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.2 | 1000 | E1/model | 670.622 +/- 0.312 | -16.775 +/- 0.000 | -35.444 +/- 0.120 | 39.213 +/- 0.063 | 39.216 +/- 0.108 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.2 | 1000 | E2/model | 670.622 +/- 0.312 | -23.404 +/- 0.000 | -39.773 +/- 0.000 | 46.148 +/- 0.000 | 46.148 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.2 | 1000 | E3/model | 670.622 +/- 0.312 | -16.775 +/- 0.000 | -18.779 +/- 1.917 | 25.181 +/- 0.826 | 26.296 +/- 0.485 | 1.000 +/- 0.000 | 480.463 +/- 10.644 |
| head5m | 10 | 0.2 | 1000 | E4/model | 670.622 +/- 0.312 | 10.539 +/- 0.751 | -0.207 +/- 0.356 | 10.541 +/- 0.429 | 11.066 +/- 0.802 | 1.000 +/- 0.000 | 278.712 +/- 14.612 |
| head5m | 10 | 0.2 | 1000 | E5/model | 670.622 +/- 0.312 | 10.468 +/- 0.788 | -0.926 +/- 0.395 | 10.509 +/- 0.433 | 10.949 +/- 0.826 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.2 | 1000 | E6/model | 670.622 +/- 0.312 | -16.775 +/- 0.000 | -20.165 +/- 0.483 | 26.230 +/- 0.214 | 26.297 +/- 0.486 | 1.000 +/- 0.000 | - |
| head5m | 10 | 0.2 | 1000 | E7/model | 670.622 +/- 0.312 | 33.966 +/- 3.441 | -4.333 +/- 3.126 | 34.241 +/- 1.816 | 40.723 +/- 0.677 | 1.000 +/- 0.000 | 480.463 +/- 10.644 |
| lab | 10 | 0 | 25 | E0 | 0.000 +/- 0.000 | -3.382 +/- 0.693 | -1.688 +/- 1.053 | 3.780 +/- 0.490 | 7.821 +/- 0.969 | 0.562 +/- 0.125 | - |
| lab | 10 | 0 | 25 | E1/model | 0.000 +/- 0.000 | -8.065 +/- 1.285 | 0.802 +/- 0.485 | 8.105 +/- 0.758 | 11.723 +/- 0.933 | 0.417 +/- 0.095 | - |
| lab | 10 | 0 | 25 | E2/model | 0.000 +/- 0.000 | -9.556 +/- 0.725 | 1.142 +/- 0.368 | 9.624 +/- 0.436 | 12.860 +/- 0.484 | 0.333 +/- 0.072 | - |
| lab | 10 | 0 | 25 | E3/model | 0.000 +/- 0.000 | -3.380 +/- 2.476 | -0.621 +/- 0.932 | 3.437 +/- 1.402 | 7.549 +/- 2.479 | 0.625 +/- 0.165 | 0.587 +/- 0.336 |
| lab | 10 | 0 | 25 | E4/model | 0.000 +/- 0.000 | -3.344 +/- 0.897 | 0.274 +/- 0.501 | 3.356 +/- 0.518 | 7.443 +/- 1.380 | 0.646 +/- 0.130 | 0.001 +/- 0.002 |
| lab | 10 | 0 | 25 | E5/model | 0.000 +/- 0.000 | -3.444 +/- 0.402 | 0.018 +/- 0.347 | 3.444 +/- 0.232 | 7.593 +/- 0.828 | 0.646 +/- 0.072 | - |
| lab | 10 | 0 | 25 | E6/model | 0.000 +/- 0.000 | -3.382 +/- 0.693 | -1.688 +/- 1.053 | 3.780 +/- 0.490 | 7.821 +/- 0.969 | 0.562 +/- 0.125 | - |
| lab | 10 | 0 | 25 | E7/model | 0.000 +/- 0.000 | -3.351 +/- 0.913 | 0.257 +/- 0.745 | 3.361 +/- 0.539 | 7.565 +/- 1.169 | 0.667 +/- 0.095 | 0.587 +/- 0.336 |
| lab | 10 | 0 | 100 | E0 | 0.000 +/- 0.000 | 0.377 +/- 0.071 | -0.252 +/- 0.001 | 0.453 +/- 0.034 | 0.638 +/- 0.023 | 1.000 +/- 0.000 | - |
| lab | 10 | 0 | 100 | E1/model | 0.000 +/- 0.000 | -1.522 +/- 0.587 | 0.118 +/- 0.156 | 1.526 +/- 0.335 | 5.443 +/- 0.923 | 0.896 +/- 0.036 | - |
| lab | 10 | 0 | 100 | E2/model | 0.000 +/- 0.000 | -2.240 +/- 0.590 | 0.154 +/- 0.115 | 2.245 +/- 0.335 | 6.453 +/- 0.739 | 0.854 +/- 0.036 | - |
| lab | 10 | 0 | 100 | E3/model | 0.000 +/- 0.000 | 0.427 +/- 0.038 | -0.232 +/- 0.033 | 0.486 +/- 0.015 | 0.626 +/- 0.036 | 1.000 +/- 0.000 | 5.165 +/- 1.172 |
| lab | 10 | 0 | 100 | E4/model | 0.000 +/- 0.000 | -0.240 +/- 0.573 | -0.110 +/- 0.121 | 0.264 +/- 0.310 | 2.233 +/- 1.968 | 0.958 +/- 0.036 | 0.102 +/- 0.115 |
| lab | 10 | 0 | 100 | E5/model | 0.000 +/- 0.000 | -0.080 +/- 0.670 | -0.071 +/- 0.068 | 0.107 +/- 0.298 | 1.786 +/- 2.228 | 0.979 +/- 0.036 | - |
| lab | 10 | 0 | 100 | E6/model | 0.000 +/- 0.000 | 0.377 +/- 0.071 | -0.252 +/- 0.001 | 0.453 +/- 0.034 | 0.638 +/- 0.023 | 1.000 +/- 0.000 | - |
| lab | 10 | 0 | 100 | E7/model | 0.000 +/- 0.000 | 0.348 +/- 0.047 | 0.043 +/- 0.100 | 0.351 +/- 0.020 | 0.554 +/- 0.065 | 1.000 +/- 0.000 | 5.165 +/- 1.172 |
| lab | 10 | 0 | 250 | E0 | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 10 | 0 | 250 | E1/model | 0.000 +/- 0.000 | -0.348 +/- 0.641 | 0.027 +/- 0.077 | 0.349 +/- 0.372 | 2.979 +/- 2.167 | 0.958 +/- 0.036 | - |
| lab | 10 | 0 | 250 | E2/model | 0.000 +/- 0.000 | -1.081 +/- 0.582 | 0.154 +/- 0.154 | 1.092 +/- 0.339 | 4.878 +/- 1.042 | 0.917 +/- 0.036 | - |
| lab | 10 | 0 | 250 | E3/model | 0.000 +/- 0.000 | 0.524 +/- 0.031 | -0.172 +/- 0.040 | 0.551 +/- 0.022 | 0.651 +/- 0.032 | 1.000 +/- 0.000 | 14.084 +/- 0.416 |
| lab | 10 | 0 | 250 | E4/model | 0.000 +/- 0.000 | 0.301 +/- 0.018 | -0.098 +/- 0.059 | 0.317 +/- 0.014 | 0.443 +/- 0.017 | 1.000 +/- 0.000 | 0.055 +/- 0.039 |
| lab | 10 | 0 | 250 | E5/model | 0.000 +/- 0.000 | 0.310 +/- 0.020 | -0.100 +/- 0.061 | 0.326 +/- 0.016 | 0.448 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 10 | 0 | 250 | E6/model | 0.000 +/- 0.000 | 0.533 +/- 0.035 | -0.180 +/- 0.048 | 0.562 +/- 0.026 | 0.665 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 10 | 0 | 250 | E7/model | 0.000 +/- 0.000 | 0.356 +/- 0.010 | 0.049 +/- 0.048 | 0.359 +/- 0.003 | 0.441 +/- 0.019 | 1.000 +/- 0.000 | 14.084 +/- 0.416 |
| lab | 10 | 0 | 1000 | E0 | 0.000 +/- 0.000 | 0.629 +/- 0.013 | -0.194 +/- 0.000 | 0.658 +/- 0.007 | 0.660 +/- 0.011 | 1.000 +/- 0.000 | - |
| lab | 10 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | 0.387 +/- 0.011 | -0.053 +/- 0.052 | 0.391 +/- 0.010 | 0.437 +/- 0.008 | 1.000 +/- 0.000 | - |
| lab | 10 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | 0.483 +/- 0.049 | -0.081 +/- 0.122 | 0.489 +/- 0.039 | 0.638 +/- 0.028 | 1.000 +/- 0.000 | - |
| lab | 10 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | 0.618 +/- 0.014 | -0.194 +/- 0.000 | 0.648 +/- 0.008 | 0.650 +/- 0.013 | 1.000 +/- 0.000 | 55.121 +/- 4.306 |
| lab | 10 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 0.303 +/- 0.040 | -0.093 +/- 0.092 | 0.317 +/- 0.032 | 0.390 +/- 0.040 | 1.000 +/- 0.000 | 0.863 +/- 0.210 |
| lab | 10 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 0.314 +/- 0.039 | -0.092 +/- 0.092 | 0.327 +/- 0.031 | 0.398 +/- 0.039 | 1.000 +/- 0.000 | - |
| lab | 10 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | 0.629 +/- 0.013 | -0.194 +/- 0.000 | 0.658 +/- 0.007 | 0.660 +/- 0.011 | 1.000 +/- 0.000 | - |
| lab | 10 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 0.371 +/- 0.018 | 0.069 +/- 0.051 | 0.377 +/- 0.010 | 0.395 +/- 0.021 | 1.000 +/- 0.000 | 55.121 +/- 4.306 |
| lab | 10 | 0.05 | 25 | E0 | 81.399 +/- 0.064 | -7.450 +/- 0.901 | -8.000 +/- 0.319 | 10.931 +/- 0.257 | 11.786 +/- 0.740 | 0.000 +/- 0.000 | - |
| lab | 10 | 0.05 | 25 | E1/model | 81.399 +/- 0.064 | -9.767 +/- 0.509 | 0.200 +/- 0.801 | 9.769 +/- 0.287 | 12.375 +/- 0.421 | 0.104 +/- 0.095 | - |
| lab | 10 | 0.05 | 25 | E2/model | 81.399 +/- 0.064 | -10.017 +/- 0.730 | -0.081 +/- 0.593 | 10.018 +/- 0.421 | 12.646 +/- 0.279 | 0.104 +/- 0.095 | - |
| lab | 10 | 0.05 | 25 | E3/model | 81.399 +/- 0.064 | -6.495 +/- 1.309 | -1.725 +/- 0.150 | 6.720 +/- 0.708 | 10.386 +/- 1.244 | 0.125 +/- 0.000 | 66.665 +/- 1.687 |
| lab | 10 | 0.05 | 25 | E4/model | 81.399 +/- 0.064 | -6.895 +/- 0.731 | -1.455 +/- 0.059 | 7.047 +/- 0.407 | 9.789 +/- 0.620 | 0.104 +/- 0.036 | 43.170 +/- 3.827 |
| lab | 10 | 0.05 | 25 | E5/model | 81.399 +/- 0.064 | -7.040 +/- 0.747 | -0.737 +/- 0.305 | 7.078 +/- 0.443 | 10.158 +/- 0.554 | 0.188 +/- 0.062 | - |
| lab | 10 | 0.05 | 25 | E6/model | 81.399 +/- 0.064 | -7.456 +/- 1.023 | -1.250 +/- 0.138 | 7.560 +/- 0.596 | 10.746 +/- 1.459 | 0.146 +/- 0.036 | - |
| lab | 10 | 0.05 | 25 | E7/model | 81.399 +/- 0.064 | -7.053 +/- 0.645 | -0.364 +/- 0.262 | 7.062 +/- 0.379 | 10.183 +/- 0.601 | 0.167 +/- 0.036 | 66.665 +/- 1.687 |
| lab | 10 | 0.05 | 100 | E0 | 81.399 +/- 0.064 | -5.944 +/- 1.844 | -5.955 +/- 0.949 | 8.414 +/- 1.122 | 10.337 +/- 1.319 | 0.271 +/- 0.130 | - |
| lab | 10 | 0.05 | 100 | E1/model | 81.399 +/- 0.064 | -5.716 +/- 1.152 | 0.123 +/- 0.634 | 5.717 +/- 0.673 | 9.980 +/- 1.055 | 0.646 +/- 0.072 | - |
| lab | 10 | 0.05 | 100 | E2/model | 81.399 +/- 0.064 | -7.093 +/- 1.009 | 0.421 +/- 0.868 | 7.105 +/- 0.611 | 11.193 +/- 0.829 | 0.562 +/- 0.062 | - |
| lab | 10 | 0.05 | 100 | E3/model | 81.399 +/- 0.064 | -0.479 +/- 0.373 | -0.581 +/- 0.618 | 0.753 +/- 0.214 | 3.668 +/- 0.591 | 0.833 +/- 0.036 | 76.812 +/- 0.232 |
| lab | 10 | 0.05 | 100 | E4/model | 81.399 +/- 0.064 | -0.585 +/- 0.395 | 0.109 +/- 0.625 | 0.595 +/- 0.163 | 3.142 +/- 0.599 | 0.854 +/- 0.036 | 28.471 +/- 1.493 |
| lab | 10 | 0.05 | 100 | E5/model | 81.399 +/- 0.064 | -1.092 +/- 0.505 | 0.116 +/- 0.499 | 1.098 +/- 0.269 | 4.153 +/- 1.053 | 0.833 +/- 0.036 | - |
| lab | 10 | 0.05 | 100 | E6/model | 81.399 +/- 0.064 | -0.350 +/- 0.410 | -0.659 +/- 0.340 | 0.746 +/- 0.183 | 3.059 +/- 0.149 | 0.854 +/- 0.072 | - |
| lab | 10 | 0.05 | 100 | E7/model | 81.399 +/- 0.064 | -0.745 +/- 1.034 | -0.044 +/- 0.292 | 0.747 +/- 0.599 | 3.434 +/- 2.408 | 0.896 +/- 0.095 | 76.812 +/- 0.232 |
| lab | 10 | 0.05 | 250 | E0 | 81.399 +/- 0.064 | 0.549 +/- 0.129 | -0.267 +/- 0.032 | 0.610 +/- 0.063 | 0.705 +/- 0.058 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.05 | 250 | E1/model | 81.399 +/- 0.064 | -2.183 +/- 2.252 | 0.076 +/- 0.193 | 2.185 +/- 1.302 | 5.466 +/- 4.407 | 0.854 +/- 0.130 | - |
| lab | 10 | 0.05 | 250 | E2/model | 81.399 +/- 0.064 | -2.515 +/- 2.745 | 0.024 +/- 0.216 | 2.515 +/- 1.586 | 5.833 +/- 4.665 | 0.833 +/- 0.157 | - |
| lab | 10 | 0.05 | 250 | E3/model | 81.399 +/- 0.064 | 0.534 +/- 0.100 | -0.223 +/- 0.028 | 0.579 +/- 0.049 | 0.631 +/- 0.054 | 1.000 +/- 0.000 | 80.162 +/- 5.834 |
| lab | 10 | 0.05 | 250 | E4/model | 81.399 +/- 0.064 | 0.308 +/- 0.032 | -0.071 +/- 0.031 | 0.316 +/- 0.014 | 0.432 +/- 0.011 | 1.000 +/- 0.000 | 20.600 +/- 4.420 |
| lab | 10 | 0.05 | 250 | E5/model | 81.399 +/- 0.064 | 0.319 +/- 0.036 | -0.056 +/- 0.041 | 0.324 +/- 0.019 | 0.449 +/- 0.021 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.05 | 250 | E6/model | 81.399 +/- 0.064 | 0.536 +/- 0.102 | -0.223 +/- 0.028 | 0.580 +/- 0.049 | 0.631 +/- 0.056 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.05 | 250 | E7/model | 81.399 +/- 0.064 | 0.364 +/- 0.044 | 0.038 +/- 0.068 | 0.366 +/- 0.029 | 0.453 +/- 0.042 | 1.000 +/- 0.000 | 80.162 +/- 5.834 |
| lab | 10 | 0.05 | 1000 | E0 | 81.399 +/- 0.064 | 0.635 +/- 0.012 | -0.194 +/- 0.000 | 0.664 +/- 0.007 | 0.666 +/- 0.012 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.05 | 1000 | E1/model | 81.399 +/- 0.064 | 0.393 +/- 0.011 | -0.086 +/- 0.054 | 0.402 +/- 0.010 | 0.442 +/- 0.010 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.05 | 1000 | E2/model | 81.399 +/- 0.064 | 0.126 +/- 0.660 | -0.178 +/- 0.101 | 0.218 +/- 0.255 | 1.861 +/- 2.095 | 0.979 +/- 0.036 | - |
| lab | 10 | 0.05 | 1000 | E3/model | 81.399 +/- 0.064 | 0.615 +/- 0.013 | -0.194 +/- 0.000 | 0.645 +/- 0.007 | 0.647 +/- 0.013 | 1.000 +/- 0.000 | 111.548 +/- 4.418 |
| lab | 10 | 0.05 | 1000 | E4/model | 81.399 +/- 0.064 | 0.295 +/- 0.005 | -0.069 +/- 0.028 | 0.303 +/- 0.001 | 0.383 +/- 0.011 | 1.000 +/- 0.000 | 12.716 +/- 2.263 |
| lab | 10 | 0.05 | 1000 | E5/model | 81.399 +/- 0.064 | 0.305 +/- 0.005 | -0.061 +/- 0.043 | 0.311 +/- 0.003 | 0.394 +/- 0.010 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.05 | 1000 | E6/model | 81.399 +/- 0.064 | 0.621 +/- 0.013 | -0.194 +/- 0.000 | 0.650 +/- 0.007 | 0.653 +/- 0.013 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.05 | 1000 | E7/model | 81.399 +/- 0.064 | 0.377 +/- 0.009 | 0.064 +/- 0.027 | 0.382 +/- 0.007 | 0.404 +/- 0.017 | 1.000 +/- 0.000 | 111.548 +/- 4.418 |
| lab | 10 | 0.1 | 25 | E0 | 162.799 +/- 0.129 | -7.217 +/- 0.887 | -8.117 +/- 0.083 | 10.861 +/- 0.307 | 11.454 +/- 0.589 | 0.000 +/- 0.000 | - |
| lab | 10 | 0.1 | 25 | E1/model | 162.799 +/- 0.129 | -6.262 +/- 2.364 | -0.377 +/- 1.939 | 6.274 +/- 1.364 | 9.901 +/- 1.706 | 0.188 +/- 0.108 | - |
| lab | 10 | 0.1 | 25 | E2/model | 162.799 +/- 0.129 | -7.084 +/- 2.151 | -0.324 +/- 1.932 | 7.092 +/- 1.265 | 10.511 +/- 1.647 | 0.167 +/- 0.095 | - |
| lab | 10 | 0.1 | 25 | E3/model | 162.799 +/- 0.129 | -6.056 +/- 1.993 | -3.921 +/- 0.499 | 7.215 +/- 0.961 | 10.016 +/- 1.455 | 0.125 +/- 0.000 | 134.628 +/- 7.133 |
| lab | 10 | 0.1 | 25 | E4/model | 162.799 +/- 0.129 | -5.347 +/- 0.621 | -2.557 +/- 0.419 | 5.927 +/- 0.329 | 9.086 +/- 1.166 | 0.208 +/- 0.036 | 110.393 +/- 9.282 |
| lab | 10 | 0.1 | 25 | E5/model | 162.799 +/- 0.129 | -5.352 +/- 0.980 | -1.414 +/- 1.349 | 5.536 +/- 0.631 | 8.865 +/- 1.311 | 0.229 +/- 0.036 | - |
| lab | 10 | 0.1 | 25 | E6/model | 162.799 +/- 0.129 | -5.945 +/- 1.596 | -1.904 +/- 0.980 | 6.242 +/- 0.713 | 9.693 +/- 1.344 | 0.146 +/- 0.072 | - |
| lab | 10 | 0.1 | 25 | E7/model | 162.799 +/- 0.129 | -5.727 +/- 0.722 | -1.799 +/- 1.008 | 6.003 +/- 0.462 | 9.330 +/- 1.025 | 0.167 +/- 0.036 | 134.628 +/- 7.133 |
| lab | 10 | 0.1 | 100 | E0 | 162.799 +/- 0.129 | -7.950 +/- 0.901 | -7.985 +/- 0.102 | 11.268 +/- 0.409 | 12.054 +/- 0.574 | 0.000 +/- 0.000 | - |
| lab | 10 | 0.1 | 100 | E1/model | 162.799 +/- 0.129 | -7.764 +/- 3.345 | 0.792 +/- 0.467 | 7.805 +/- 1.944 | 11.419 +/- 2.600 | 0.458 +/- 0.253 | - |
| lab | 10 | 0.1 | 100 | E2/model | 162.799 +/- 0.129 | -8.486 +/- 2.189 | 0.745 +/- 0.419 | 8.519 +/- 1.273 | 12.017 +/- 1.595 | 0.417 +/- 0.180 | - |
| lab | 10 | 0.1 | 100 | E3/model | 162.799 +/- 0.129 | -1.821 +/- 1.834 | -0.030 +/- 0.683 | 1.821 +/- 1.064 | 5.172 +/- 2.422 | 0.688 +/- 0.217 | 155.937 +/- 2.895 |
| lab | 10 | 0.1 | 100 | E4/model | 162.799 +/- 0.129 | -0.532 +/- 0.227 | -0.121 +/- 0.668 | 0.546 +/- 0.206 | 3.623 +/- 0.782 | 0.875 +/- 0.000 | 102.482 +/- 2.595 |
| lab | 10 | 0.1 | 100 | E5/model | 162.799 +/- 0.129 | -0.871 +/- 0.102 | 0.294 +/- 0.235 | 0.920 +/- 0.058 | 4.635 +/- 0.160 | 0.875 +/- 0.000 | - |
| lab | 10 | 0.1 | 100 | E6/model | 162.799 +/- 0.129 | -1.523 +/- 1.346 | -0.505 +/- 0.544 | 1.605 +/- 0.797 | 5.183 +/- 1.779 | 0.708 +/- 0.130 | - |
| lab | 10 | 0.1 | 100 | E7/model | 162.799 +/- 0.129 | -1.252 +/- 1.429 | 0.571 +/- 0.297 | 1.376 +/- 0.812 | 4.893 +/- 2.995 | 0.833 +/- 0.095 | 155.937 +/- 2.895 |
| lab | 10 | 0.1 | 250 | E0 | 162.799 +/- 0.129 | -0.883 +/- 0.723 | -2.212 +/- 0.392 | 2.382 +/- 0.360 | 5.268 +/- 0.847 | 0.750 +/- 0.062 | - |
| lab | 10 | 0.1 | 250 | E1/model | 162.799 +/- 0.129 | -1.818 +/- 1.104 | 0.025 +/- 0.058 | 1.818 +/- 0.638 | 5.900 +/- 1.580 | 0.875 +/- 0.062 | - |
| lab | 10 | 0.1 | 250 | E2/model | 162.799 +/- 0.129 | -2.564 +/- 0.641 | 0.024 +/- 0.042 | 2.564 +/- 0.370 | 6.916 +/- 0.776 | 0.833 +/- 0.036 | - |
| lab | 10 | 0.1 | 250 | E3/model | 162.799 +/- 0.129 | 0.512 +/- 0.042 | -0.213 +/- 0.045 | 0.555 +/- 0.032 | 0.698 +/- 0.017 | 1.000 +/- 0.000 | 162.697 +/- 2.259 |
| lab | 10 | 0.1 | 250 | E4/model | 162.799 +/- 0.129 | 0.288 +/- 0.083 | -0.239 +/- 0.346 | 0.374 +/- 0.107 | 0.992 +/- 0.936 | 0.979 +/- 0.036 | 92.353 +/- 5.722 |
| lab | 10 | 0.1 | 250 | E5/model | 162.799 +/- 0.129 | 0.352 +/- 0.075 | -0.084 +/- 0.084 | 0.362 +/- 0.049 | 0.494 +/- 0.071 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.1 | 250 | E6/model | 162.799 +/- 0.129 | 0.510 +/- 0.042 | -0.213 +/- 0.044 | 0.553 +/- 0.032 | 0.697 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.1 | 250 | E7/model | 162.799 +/- 0.129 | 0.422 +/- 0.072 | 0.007 +/- 0.082 | 0.422 +/- 0.041 | 0.542 +/- 0.056 | 1.000 +/- 0.000 | 162.697 +/- 2.259 |
| lab | 10 | 0.1 | 1000 | E0 | 162.799 +/- 0.129 | 0.634 +/- 0.016 | -0.194 +/- 0.000 | 0.663 +/- 0.009 | 0.666 +/- 0.014 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.1 | 1000 | E1/model | 162.799 +/- 0.129 | 0.372 +/- 0.030 | -0.086 +/- 0.013 | 0.381 +/- 0.016 | 0.428 +/- 0.016 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.1 | 1000 | E2/model | 162.799 +/- 0.129 | 0.426 +/- 0.120 | -0.130 +/- 0.056 | 0.445 +/- 0.071 | 0.608 +/- 0.064 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.1 | 1000 | E3/model | 162.799 +/- 0.129 | 0.601 +/- 0.017 | -0.194 +/- 0.000 | 0.632 +/- 0.009 | 0.635 +/- 0.014 | 1.000 +/- 0.000 | 180.029 +/- 5.083 |
| lab | 10 | 0.1 | 1000 | E4/model | 162.799 +/- 0.129 | 0.281 +/- 0.031 | -0.118 +/- 0.050 | 0.305 +/- 0.006 | 0.378 +/- 0.002 | 1.000 +/- 0.000 | 49.333 +/- 5.484 |
| lab | 10 | 0.1 | 1000 | E5/model | 162.799 +/- 0.129 | 0.289 +/- 0.032 | -0.124 +/- 0.045 | 0.314 +/- 0.009 | 0.389 +/- 0.005 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.1 | 1000 | E6/model | 162.799 +/- 0.129 | 0.605 +/- 0.017 | -0.194 +/- 0.000 | 0.635 +/- 0.009 | 0.639 +/- 0.014 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.1 | 1000 | E7/model | 162.799 +/- 0.129 | 0.356 +/- 0.035 | 0.025 +/- 0.036 | 0.357 +/- 0.021 | 0.394 +/- 0.021 | 1.000 +/- 0.000 | 180.029 +/- 5.083 |
| lab | 10 | 0.2 | 25 | E0 | 325.597 +/- 0.257 | -6.931 +/- 0.794 | -8.133 +/- 0.111 | 10.686 +/- 0.336 | 11.269 +/- 0.571 | 0.000 +/- 0.000 | - |
| lab | 10 | 0.2 | 25 | E1/model | 325.597 +/- 0.257 | -7.089 +/- 1.004 | 0.634 +/- 0.737 | 7.118 +/- 0.613 | 10.135 +/- 0.800 | 0.146 +/- 0.072 | - |
| lab | 10 | 0.2 | 25 | E2/model | 325.597 +/- 0.257 | -7.400 +/- 0.714 | 0.348 +/- 0.941 | 7.409 +/- 0.437 | 10.375 +/- 0.539 | 0.146 +/- 0.072 | - |
| lab | 10 | 0.2 | 25 | E3/model | 325.597 +/- 0.257 | -6.209 +/- 1.315 | -3.180 +/- 1.249 | 6.975 +/- 0.960 | 9.612 +/- 0.803 | 0.104 +/- 0.072 | 281.093 +/- 7.655 |
| lab | 10 | 0.2 | 25 | E4/model | 325.597 +/- 0.257 | -5.679 +/- 0.785 | -1.959 +/- 1.003 | 6.007 +/- 0.400 | 9.223 +/- 0.700 | 0.146 +/- 0.036 | 246.529 +/- 14.224 |
| lab | 10 | 0.2 | 25 | E5/model | 325.597 +/- 0.257 | -7.059 +/- 1.238 | -1.009 +/- 0.340 | 7.131 +/- 0.731 | 10.041 +/- 0.627 | 0.146 +/- 0.072 | - |
| lab | 10 | 0.2 | 25 | E6/model | 325.597 +/- 0.257 | -7.137 +/- 0.484 | -0.805 +/- 1.509 | 7.182 +/- 0.180 | 10.026 +/- 0.497 | 0.104 +/- 0.072 | - |
| lab | 10 | 0.2 | 25 | E7/model | 325.597 +/- 0.257 | -6.752 +/- 1.276 | -1.572 +/- 0.555 | 6.933 +/- 0.754 | 9.850 +/- 1.038 | 0.146 +/- 0.036 | 281.093 +/- 7.655 |
| lab | 10 | 0.2 | 100 | E0 | 325.597 +/- 0.257 | -7.447 +/- 0.814 | -8.200 +/- 0.282 | 11.077 +/- 0.254 | 11.742 +/- 0.484 | 0.000 +/- 0.000 | - |
| lab | 10 | 0.2 | 100 | E1/model | 325.597 +/- 0.257 | -9.207 +/- 3.206 | -0.144 +/- 0.137 | 9.208 +/- 1.852 | 12.382 +/- 2.085 | 0.292 +/- 0.191 | - |
| lab | 10 | 0.2 | 100 | E2/model | 325.597 +/- 0.257 | -9.799 +/- 3.666 | -0.089 +/- 0.134 | 9.799 +/- 2.117 | 12.772 +/- 2.378 | 0.271 +/- 0.201 | - |
| lab | 10 | 0.2 | 100 | E3/model | 325.597 +/- 0.257 | -4.485 +/- 1.304 | -0.786 +/- 1.195 | 4.553 +/- 0.857 | 8.970 +/- 0.594 | 0.458 +/- 0.036 | 312.417 +/- 4.000 |
| lab | 10 | 0.2 | 100 | E4/model | 325.597 +/- 0.257 | -3.655 +/- 1.285 | -0.498 +/- 0.804 | 3.689 +/- 0.754 | 7.705 +/- 0.831 | 0.542 +/- 0.095 | 238.689 +/- 10.947 |
| lab | 10 | 0.2 | 100 | E5/model | 325.597 +/- 0.257 | -4.626 +/- 2.190 | -0.206 +/- 0.641 | 4.631 +/- 1.274 | 8.697 +/- 1.751 | 0.583 +/- 0.095 | - |
| lab | 10 | 0.2 | 100 | E6/model | 325.597 +/- 0.257 | -4.707 +/- 0.569 | -0.154 +/- 0.729 | 4.710 +/- 0.342 | 9.178 +/- 0.710 | 0.438 +/- 0.062 | - |
| lab | 10 | 0.2 | 100 | E7/model | 325.597 +/- 0.257 | -4.625 +/- 1.654 | 0.250 +/- 0.351 | 4.632 +/- 0.964 | 8.870 +/- 1.467 | 0.583 +/- 0.095 | 312.417 +/- 4.000 |
| lab | 10 | 0.2 | 250 | E0 | 325.597 +/- 0.257 | -6.925 +/- 0.185 | -7.931 +/- 0.509 | 10.529 +/- 0.243 | 11.358 +/- 0.100 | 0.042 +/- 0.072 | - |
| lab | 10 | 0.2 | 250 | E1/model | 325.597 +/- 0.257 | -2.225 +/- 1.283 | 0.054 +/- 0.145 | 2.226 +/- 0.741 | 6.311 +/- 1.810 | 0.854 +/- 0.072 | - |
| lab | 10 | 0.2 | 250 | E2/model | 325.597 +/- 0.257 | -3.706 +/- 1.666 | 0.016 +/- 0.149 | 3.706 +/- 0.961 | 8.007 +/- 1.789 | 0.771 +/- 0.095 | - |
| lab | 10 | 0.2 | 250 | E3/model | 325.597 +/- 0.257 | 0.464 +/- 0.111 | -0.238 +/- 0.042 | 0.522 +/- 0.064 | 0.677 +/- 0.041 | 1.000 +/- 0.000 | 320.315 +/- 10.639 |
| lab | 10 | 0.2 | 250 | E4/model | 325.597 +/- 0.257 | 0.290 +/- 0.029 | -0.093 +/- 0.038 | 0.304 +/- 0.015 | 0.453 +/- 0.032 | 1.000 +/- 0.000 | 234.855 +/- 6.200 |
| lab | 10 | 0.2 | 250 | E5/model | 325.597 +/- 0.257 | -0.059 +/- 0.637 | -0.078 +/- 0.022 | 0.098 +/- 0.231 | 1.769 +/- 2.243 | 0.979 +/- 0.036 | - |
| lab | 10 | 0.2 | 250 | E6/model | 325.597 +/- 0.257 | 0.460 +/- 0.113 | -0.239 +/- 0.042 | 0.518 +/- 0.065 | 0.675 +/- 0.037 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.2 | 250 | E7/model | 325.597 +/- 0.257 | -0.005 +/- 0.634 | 0.053 +/- 0.036 | 0.053 +/- 0.043 | 1.796 +/- 2.228 | 0.979 +/- 0.036 | 320.315 +/- 10.639 |
| lab | 10 | 0.2 | 1000 | E0 | 325.597 +/- 0.257 | 0.651 +/- 0.018 | -0.194 +/- 0.000 | 0.679 +/- 0.010 | 0.681 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.2 | 1000 | E1/model | 325.597 +/- 0.257 | 0.017 +/- 0.643 | -0.102 +/- 0.014 | 0.104 +/- 0.065 | 1.715 +/- 2.207 | 0.979 +/- 0.036 | - |
| lab | 10 | 0.2 | 1000 | E2/model | 325.597 +/- 0.257 | 0.118 +/- 0.674 | -0.194 +/- 0.049 | 0.227 +/- 0.223 | 1.860 +/- 2.093 | 0.979 +/- 0.036 | - |
| lab | 10 | 0.2 | 1000 | E3/model | 325.597 +/- 0.257 | 0.593 +/- 0.019 | -0.194 +/- 0.000 | 0.624 +/- 0.010 | 0.627 +/- 0.018 | 1.000 +/- 0.000 | 345.228 +/- 12.156 |
| lab | 10 | 0.2 | 1000 | E4/model | 325.597 +/- 0.257 | 0.276 +/- 0.003 | -0.127 +/- 0.034 | 0.304 +/- 0.007 | 0.366 +/- 0.013 | 1.000 +/- 0.000 | 185.075 +/- 11.655 |
| lab | 10 | 0.2 | 1000 | E5/model | 325.597 +/- 0.257 | 0.280 +/- 0.002 | -0.132 +/- 0.037 | 0.310 +/- 0.008 | 0.374 +/- 0.011 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.2 | 1000 | E6/model | 325.597 +/- 0.257 | 0.597 +/- 0.021 | -0.194 +/- 0.000 | 0.628 +/- 0.012 | 0.631 +/- 0.020 | 1.000 +/- 0.000 | - |
| lab | 10 | 0.2 | 1000 | E7/model | 325.597 +/- 0.257 | 0.368 +/- 0.025 | 0.035 +/- 0.019 | 0.369 +/- 0.015 | 0.403 +/- 0.025 | 1.000 +/- 0.000 | 345.228 +/- 12.156 |

### 60 s high-count sensitivities

All tables: mean +/- sample SD of three outer-seed means, seeds 330001, 330007, 330019; 16 acquisitions per seed (48 per condition). Pull is the length of the pooled signed mean; its uncertainty is the delta-method SE, not the SD. Fractions are descriptive; 48/48 alone cannot certify 95% success (one-sided 95% lower bound 93.95%, conditional independence only).

| Case | t s | F uSv/h | S | Estimator | B mean +/- SD | dx mm mean +/- SD | dy mm mean +/- SD | Pull mm +/- SE | RMS mm mean +/- SD | Within element mean +/- SD | Bfit mean +/- SD |
|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|
| head | 60 | 0 | 1000 | E0 | 0.000 +/- 0.000 | 0.269 +/- 0.171 | -0.419 +/- 0.050 | 0.498 +/- 0.029 | 0.828 +/- 0.086 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | 0.375 +/- 0.005 | -0.061 +/- 0.028 | 0.380 +/- 0.002 | 0.389 +/- 0.002 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | 0.228 +/- 0.015 | 0.000 +/- 0.000 | 0.228 +/- 0.009 | 0.235 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | 0.220 +/- 0.141 | -0.430 +/- 0.064 | 0.483 +/- 0.004 | 0.789 +/- 0.064 | 1.000 +/- 0.000 | 48.895 +/- 7.653 |
| head | 60 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 0.456 +/- 0.016 | -0.145 +/- 0.043 | 0.479 +/- 0.004 | 0.497 +/- 0.010 | 1.000 +/- 0.000 | 0.333 +/- 0.527 |
| head | 60 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 0.456 +/- 0.016 | -0.145 +/- 0.044 | 0.479 +/- 0.004 | 0.497 +/- 0.010 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | 0.269 +/- 0.171 | -0.419 +/- 0.050 | 0.498 +/- 0.029 | 0.828 +/- 0.086 | 1.000 +/- 0.000 | - |
| head | 60 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 0.419 +/- 0.018 | -0.035 +/- 0.039 | 0.421 +/- 0.009 | 0.437 +/- 0.017 | 1.000 +/- 0.000 | 48.895 +/- 7.653 |
| head | 60 | 0.1 | 1000 | E0 | 2011.866 +/- 0.936 | -5.322 +/- 0.130 | 4.901 +/- 0.004 | 7.235 +/- 0.054 | 9.756 +/- 0.060 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/measured3600 | 2011.866 +/- 0.936 | 0.345 +/- 0.023 | -0.058 +/- 0.044 | 0.350 +/- 0.010 | 0.390 +/- 0.024 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/measured600 | 2011.866 +/- 0.936 | 0.338 +/- 0.037 | -0.060 +/- 0.087 | 0.343 +/- 0.018 | 0.387 +/- 0.023 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/model | 2011.866 +/- 0.936 | 0.340 +/- 0.031 | -0.074 +/- 0.056 | 0.348 +/- 0.011 | 0.401 +/- 0.014 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/oracleShape | 2011.866 +/- 0.936 | 0.339 +/- 0.029 | -0.080 +/- 0.049 | 0.349 +/- 0.010 | 0.402 +/- 0.014 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/scale0.5 | 2011.866 +/- 0.936 | 0.340 +/- 0.031 | -0.074 +/- 0.056 | 0.348 +/- 0.011 | 0.401 +/- 0.014 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/scale0.75 | 2011.866 +/- 0.936 | 0.340 +/- 0.031 | -0.074 +/- 0.056 | 0.348 +/- 0.011 | 0.401 +/- 0.014 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/scale0.9 | 2011.866 +/- 0.936 | 0.340 +/- 0.031 | -0.074 +/- 0.056 | 0.348 +/- 0.011 | 0.401 +/- 0.014 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/scale1.1 | 2011.866 +/- 0.936 | 0.340 +/- 0.031 | -0.074 +/- 0.056 | 0.348 +/- 0.011 | 0.401 +/- 0.014 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/scale1.25 | 2011.866 +/- 0.936 | 0.340 +/- 0.031 | -0.074 +/- 0.056 | 0.348 +/- 0.011 | 0.401 +/- 0.014 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/scale1.5 | 2011.866 +/- 0.936 | 0.340 +/- 0.031 | -0.074 +/- 0.056 | 0.348 +/- 0.011 | 0.401 +/- 0.014 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E1/wrongFront | 2011.866 +/- 0.936 | -9.103 +/- 0.494 | -5.249 +/- 0.079 | 10.508 +/- 0.263 | 10.606 +/- 0.544 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/measured3600 | 2011.866 +/- 0.936 | 0.254 +/- 0.015 | -0.000 +/- 0.044 | 0.254 +/- 0.009 | 0.313 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/measured600 | 2011.866 +/- 0.936 | 0.254 +/- 0.015 | -0.000 +/- 0.051 | 0.254 +/- 0.009 | 0.311 +/- 0.051 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/model | 2011.866 +/- 0.936 | 0.254 +/- 0.015 | -0.000 +/- 0.044 | 0.254 +/- 0.009 | 0.313 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/oracleShape | 2011.866 +/- 0.936 | 0.254 +/- 0.015 | -0.000 +/- 0.044 | 0.254 +/- 0.009 | 0.313 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/scale0.5 | 2011.866 +/- 0.936 | 0.254 +/- 0.015 | -0.000 +/- 0.044 | 0.254 +/- 0.009 | 0.313 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/scale0.75 | 2011.866 +/- 0.936 | 0.254 +/- 0.015 | -0.000 +/- 0.044 | 0.254 +/- 0.009 | 0.313 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/scale0.9 | 2011.866 +/- 0.936 | 0.254 +/- 0.015 | -0.000 +/- 0.044 | 0.254 +/- 0.009 | 0.313 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/scale1.1 | 2011.866 +/- 0.936 | 0.254 +/- 0.015 | -0.000 +/- 0.044 | 0.254 +/- 0.009 | 0.313 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/scale1.25 | 2011.866 +/- 0.936 | 0.254 +/- 0.015 | -0.000 +/- 0.044 | 0.254 +/- 0.009 | 0.313 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/scale1.5 | 2011.866 +/- 0.936 | 0.254 +/- 0.015 | -0.000 +/- 0.044 | 0.254 +/- 0.009 | 0.313 +/- 0.026 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E2/wrongFront | 2011.866 +/- 0.936 | -9.079 +/- 0.553 | -5.300 +/- 0.074 | 10.513 +/- 0.295 | 10.615 +/- 0.597 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E3/model | 2011.866 +/- 0.936 | -0.573 +/- 0.599 | -1.209 +/- 0.544 | 1.338 +/- 0.431 | 3.220 +/- 1.466 | 0.896 +/- 0.072 | 2082.819 +/- 12.668 |
| head | 60 | 0.1 | 1000 | E4/measured3600 | 2011.866 +/- 0.936 | 0.532 +/- 0.038 | -0.130 +/- 0.023 | 0.547 +/- 0.020 | 0.584 +/- 0.015 | 1.000 +/- 0.000 | 1819.214 +/- 16.260 |
| head | 60 | 0.1 | 1000 | E4/measured600 | 2011.866 +/- 0.936 | 0.530 +/- 0.039 | -0.118 +/- 0.048 | 0.543 +/- 0.021 | 0.583 +/- 0.019 | 1.000 +/- 0.000 | 1776.264 +/- 13.769 |
| head | 60 | 0.1 | 1000 | E4/model | 2011.866 +/- 0.936 | 0.526 +/- 0.032 | -0.129 +/- 0.035 | 0.542 +/- 0.016 | 0.580 +/- 0.009 | 1.000 +/- 0.000 | 1824.052 +/- 11.395 |
| head | 60 | 0.1 | 1000 | E4/oracleShape | 2011.866 +/- 0.936 | 0.525 +/- 0.030 | -0.132 +/- 0.035 | 0.541 +/- 0.015 | 0.580 +/- 0.007 | 1.000 +/- 0.000 | 1827.291 +/- 14.847 |
| head | 60 | 0.1 | 1000 | E4/scale0.5 | 2011.866 +/- 0.936 | 0.526 +/- 0.032 | -0.129 +/- 0.035 | 0.542 +/- 0.016 | 0.580 +/- 0.009 | 1.000 +/- 0.000 | 1824.052 +/- 11.395 |
| head | 60 | 0.1 | 1000 | E4/scale0.75 | 2011.866 +/- 0.936 | 0.526 +/- 0.032 | -0.129 +/- 0.035 | 0.542 +/- 0.016 | 0.580 +/- 0.009 | 1.000 +/- 0.000 | 1824.052 +/- 11.395 |
| head | 60 | 0.1 | 1000 | E4/scale0.9 | 2011.866 +/- 0.936 | 0.526 +/- 0.032 | -0.129 +/- 0.035 | 0.542 +/- 0.016 | 0.580 +/- 0.009 | 1.000 +/- 0.000 | 1824.052 +/- 11.395 |
| head | 60 | 0.1 | 1000 | E4/scale1.1 | 2011.866 +/- 0.936 | 0.526 +/- 0.032 | -0.129 +/- 0.035 | 0.542 +/- 0.016 | 0.580 +/- 0.009 | 1.000 +/- 0.000 | 1824.052 +/- 11.395 |
| head | 60 | 0.1 | 1000 | E4/scale1.25 | 2011.866 +/- 0.936 | 0.526 +/- 0.032 | -0.129 +/- 0.035 | 0.542 +/- 0.016 | 0.580 +/- 0.009 | 1.000 +/- 0.000 | 1824.052 +/- 11.395 |
| head | 60 | 0.1 | 1000 | E4/scale1.5 | 2011.866 +/- 0.936 | 0.526 +/- 0.032 | -0.129 +/- 0.035 | 0.542 +/- 0.016 | 0.580 +/- 0.009 | 1.000 +/- 0.000 | 1824.052 +/- 11.395 |
| head | 60 | 0.1 | 1000 | E4/wrongFront | 2011.866 +/- 0.936 | -1.454 +/- 1.987 | -0.263 +/- 0.287 | 1.477 +/- 1.141 | 5.623 +/- 2.728 | 0.812 +/- 0.108 | 0.545 +/- 0.122 |
| head | 60 | 0.1 | 1000 | E5/measured3600 | 2011.866 +/- 0.936 | 0.517 +/- 0.038 | -0.137 +/- 0.021 | 0.535 +/- 0.020 | 0.572 +/- 0.015 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E5/measured600 | 2011.866 +/- 0.936 | 0.508 +/- 0.039 | -0.127 +/- 0.053 | 0.523 +/- 0.021 | 0.564 +/- 0.019 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E5/model | 2011.866 +/- 0.936 | 0.511 +/- 0.032 | -0.134 +/- 0.036 | 0.529 +/- 0.016 | 0.567 +/- 0.009 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E5/oracleShape | 2011.866 +/- 0.936 | 0.510 +/- 0.027 | -0.139 +/- 0.033 | 0.528 +/- 0.014 | 0.567 +/- 0.006 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E5/scale0.5 | 2011.866 +/- 0.936 | 0.639 +/- 0.022 | -0.089 +/- 0.044 | 0.645 +/- 0.010 | 0.684 +/- 0.006 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E5/scale0.75 | 2011.866 +/- 0.936 | 0.554 +/- 0.030 | -0.115 +/- 0.037 | 0.566 +/- 0.015 | 0.605 +/- 0.008 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E5/scale0.9 | 2011.866 +/- 0.936 | 0.526 +/- 0.031 | -0.127 +/- 0.037 | 0.541 +/- 0.015 | 0.579 +/- 0.008 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E5/scale1.1 | 2011.866 +/- 0.936 | 0.498 +/- 0.032 | -0.141 +/- 0.038 | 0.518 +/- 0.016 | 0.558 +/- 0.009 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E5/scale1.25 | 2011.866 +/- 0.936 | 0.487 +/- 0.033 | -0.152 +/- 0.040 | 0.510 +/- 0.016 | 0.550 +/- 0.009 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E5/scale1.5 | 2011.866 +/- 0.936 | 0.479 +/- 0.031 | -0.168 +/- 0.043 | 0.507 +/- 0.016 | 0.545 +/- 0.007 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E5/wrongFront | 2011.866 +/- 0.936 | -9.879 +/- 1.376 | -4.653 +/- 0.241 | 10.920 +/- 0.749 | 11.517 +/- 1.377 | 0.021 +/- 0.036 | - |
| head | 60 | 0.1 | 1000 | E6/measured3600 | 2011.866 +/- 0.936 | -0.672 +/- 0.193 | -1.090 +/- 0.247 | 1.281 +/- 0.063 | 3.941 +/- 0.495 | 0.854 +/- 0.036 | - |
| head | 60 | 0.1 | 1000 | E6/measured600 | 2011.866 +/- 0.936 | -0.623 +/- 0.110 | -0.850 +/- 0.387 | 1.054 +/- 0.147 | 3.892 +/- 0.490 | 0.854 +/- 0.036 | - |
| head | 60 | 0.1 | 1000 | E6/model | 2011.866 +/- 0.936 | -0.692 +/- 0.200 | -0.882 +/- 0.437 | 1.121 +/- 0.158 | 3.914 +/- 0.509 | 0.854 +/- 0.036 | - |
| head | 60 | 0.1 | 1000 | E6/oracleShape | 2011.866 +/- 0.936 | -0.600 +/- 0.231 | -0.869 +/- 0.491 | 1.056 +/- 0.242 | 3.875 +/- 0.534 | 0.854 +/- 0.036 | - |
| head | 60 | 0.1 | 1000 | E6/scale0.5 | 2011.866 +/- 0.936 | -5.987 +/- 0.534 | 3.961 +/- 1.794 | 7.179 +/- 0.400 | 10.060 +/- 0.212 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E6/scale0.75 | 2011.866 +/- 0.936 | -5.820 +/- 0.333 | 2.514 +/- 0.973 | 6.340 +/- 0.135 | 10.187 +/- 0.412 | 0.042 +/- 0.036 | - |
| head | 60 | 0.1 | 1000 | E6/scale0.9 | 2011.866 +/- 0.936 | -2.862 +/- 0.574 | -0.915 +/- 0.228 | 3.005 +/- 0.348 | 7.434 +/- 0.673 | 0.542 +/- 0.036 | - |
| head | 60 | 0.1 | 1000 | E6/scale1.1 | 2011.866 +/- 0.936 | 0.158 +/- 0.088 | -0.424 +/- 0.077 | 0.452 +/- 0.038 | 0.764 +/- 0.045 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E6/scale1.25 | 2011.866 +/- 0.936 | 0.151 +/- 0.044 | -0.325 +/- 0.070 | 0.359 +/- 0.026 | 0.516 +/- 0.041 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E6/scale1.5 | 2011.866 +/- 0.936 | 0.340 +/- 0.029 | -0.072 +/- 0.054 | 0.347 +/- 0.010 | 0.401 +/- 0.014 | 1.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E6/wrongFront | 2011.866 +/- 0.936 | -9.036 +/- 0.407 | -5.140 +/- 0.501 | 10.396 +/- 0.343 | 10.646 +/- 0.225 | 0.000 +/- 0.000 | - |
| head | 60 | 0.1 | 1000 | E7/model | 2011.866 +/- 0.936 | 0.454 +/- 0.039 | -0.023 +/- 0.056 | 0.455 +/- 0.021 | 0.502 +/- 0.028 | 1.000 +/- 0.000 | 2082.819 +/- 12.668 |
| head | 60 | 0.2 | 1000 | E0 | 4023.732 +/- 1.873 | -4.532 +/- 0.196 | 6.344 +/- 0.500 | 7.797 +/- 0.172 | 9.456 +/- 0.067 | 0.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/measured3600 | 4023.732 +/- 1.873 | 0.332 +/- 0.026 | -0.099 +/- 0.055 | 0.347 +/- 0.014 | 0.454 +/- 0.073 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/measured600 | 4023.732 +/- 1.873 | 0.326 +/- 0.042 | -0.089 +/- 0.118 | 0.338 +/- 0.025 | 0.422 +/- 0.060 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/model | 4023.732 +/- 1.873 | 0.320 +/- 0.032 | -0.098 +/- 0.074 | 0.335 +/- 0.009 | 0.447 +/- 0.063 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/oracleShape | 4023.732 +/- 1.873 | 0.325 +/- 0.029 | -0.106 +/- 0.067 | 0.342 +/- 0.013 | 0.450 +/- 0.069 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/scale0.5 | 4023.732 +/- 1.873 | 0.320 +/- 0.032 | -0.098 +/- 0.074 | 0.335 +/- 0.009 | 0.447 +/- 0.063 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/scale0.75 | 4023.732 +/- 1.873 | 0.320 +/- 0.032 | -0.098 +/- 0.074 | 0.335 +/- 0.009 | 0.447 +/- 0.063 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/scale0.9 | 4023.732 +/- 1.873 | 0.320 +/- 0.032 | -0.098 +/- 0.074 | 0.335 +/- 0.009 | 0.447 +/- 0.063 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/scale1.1 | 4023.732 +/- 1.873 | 0.320 +/- 0.032 | -0.098 +/- 0.074 | 0.335 +/- 0.009 | 0.447 +/- 0.063 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/scale1.25 | 4023.732 +/- 1.873 | 0.320 +/- 0.032 | -0.098 +/- 0.074 | 0.335 +/- 0.009 | 0.447 +/- 0.063 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/scale1.5 | 4023.732 +/- 1.873 | 0.320 +/- 0.032 | -0.098 +/- 0.074 | 0.335 +/- 0.009 | 0.447 +/- 0.063 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E1/wrongFront | 4023.732 +/- 1.873 | -8.666 +/- 0.136 | -5.319 +/- 0.020 | 10.168 +/- 0.072 | 10.190 +/- 0.127 | 0.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/measured3600 | 4023.732 +/- 1.873 | 0.271 +/- 0.044 | -0.043 +/- 0.059 | 0.274 +/- 0.028 | 0.435 +/- 0.134 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/measured600 | 4023.732 +/- 1.873 | 0.237 +/- 0.053 | -0.034 +/- 0.097 | 0.239 +/- 0.033 | 0.378 +/- 0.084 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/model | 4023.732 +/- 1.873 | 0.254 +/- 0.065 | -0.034 +/- 0.065 | 0.256 +/- 0.035 | 0.408 +/- 0.119 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/oracleShape | 4023.732 +/- 1.873 | 0.254 +/- 0.059 | -0.051 +/- 0.077 | 0.259 +/- 0.035 | 0.438 +/- 0.135 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/scale0.5 | 4023.732 +/- 1.873 | 0.254 +/- 0.065 | -0.034 +/- 0.065 | 0.256 +/- 0.035 | 0.408 +/- 0.119 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/scale0.75 | 4023.732 +/- 1.873 | 0.254 +/- 0.065 | -0.034 +/- 0.065 | 0.256 +/- 0.035 | 0.408 +/- 0.119 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/scale0.9 | 4023.732 +/- 1.873 | 0.254 +/- 0.065 | -0.034 +/- 0.065 | 0.256 +/- 0.035 | 0.408 +/- 0.119 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/scale1.1 | 4023.732 +/- 1.873 | 0.254 +/- 0.065 | -0.034 +/- 0.065 | 0.256 +/- 0.035 | 0.408 +/- 0.119 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/scale1.25 | 4023.732 +/- 1.873 | 0.254 +/- 0.065 | -0.034 +/- 0.065 | 0.256 +/- 0.035 | 0.408 +/- 0.119 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/scale1.5 | 4023.732 +/- 1.873 | 0.254 +/- 0.065 | -0.034 +/- 0.065 | 0.256 +/- 0.035 | 0.408 +/- 0.119 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E2/wrongFront | 4023.732 +/- 1.873 | -8.651 +/- 0.127 | -5.343 +/- 0.000 | 10.168 +/- 0.062 | 10.196 +/- 0.108 | 0.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E3/model | 4023.732 +/- 1.873 | -0.767 +/- 0.594 | -1.057 +/- 1.322 | 1.306 +/- 0.804 | 4.568 +/- 0.997 | 0.854 +/- 0.095 | 4056.112 +/- 12.233 |
| head | 60 | 0.2 | 1000 | E4/measured3600 | 4023.732 +/- 1.873 | 0.574 +/- 0.097 | -0.107 +/- 0.045 | 0.584 +/- 0.057 | 0.647 +/- 0.106 | 1.000 +/- 0.000 | 3725.535 +/- 26.586 |
| head | 60 | 0.2 | 1000 | E4/measured600 | 4023.732 +/- 1.873 | 0.582 +/- 0.099 | -0.093 +/- 0.066 | 0.589 +/- 0.062 | 0.648 +/- 0.117 | 1.000 +/- 0.000 | 3611.829 +/- 21.459 |
| head | 60 | 0.2 | 1000 | E4/model | 4023.732 +/- 1.873 | 0.571 +/- 0.080 | -0.109 +/- 0.031 | 0.581 +/- 0.049 | 0.644 +/- 0.097 | 1.000 +/- 0.000 | 3731.087 +/- 20.372 |
| head | 60 | 0.2 | 1000 | E4/oracleShape | 4023.732 +/- 1.873 | 0.569 +/- 0.085 | -0.116 +/- 0.020 | 0.581 +/- 0.051 | 0.643 +/- 0.100 | 1.000 +/- 0.000 | 3740.037 +/- 30.148 |
| head | 60 | 0.2 | 1000 | E4/scale0.5 | 4023.732 +/- 1.873 | 0.571 +/- 0.080 | -0.109 +/- 0.031 | 0.581 +/- 0.049 | 0.644 +/- 0.097 | 1.000 +/- 0.000 | 3731.087 +/- 20.372 |
| head | 60 | 0.2 | 1000 | E4/scale0.75 | 4023.732 +/- 1.873 | 0.571 +/- 0.080 | -0.109 +/- 0.031 | 0.581 +/- 0.049 | 0.644 +/- 0.097 | 1.000 +/- 0.000 | 3731.087 +/- 20.372 |
| head | 60 | 0.2 | 1000 | E4/scale0.9 | 4023.732 +/- 1.873 | 0.571 +/- 0.080 | -0.109 +/- 0.031 | 0.581 +/- 0.049 | 0.644 +/- 0.097 | 1.000 +/- 0.000 | 3731.087 +/- 20.372 |
| head | 60 | 0.2 | 1000 | E4/scale1.1 | 4023.732 +/- 1.873 | 0.571 +/- 0.080 | -0.109 +/- 0.031 | 0.581 +/- 0.049 | 0.644 +/- 0.097 | 1.000 +/- 0.000 | 3731.087 +/- 20.372 |
| head | 60 | 0.2 | 1000 | E4/scale1.25 | 4023.732 +/- 1.873 | 0.571 +/- 0.080 | -0.109 +/- 0.031 | 0.581 +/- 0.049 | 0.644 +/- 0.097 | 1.000 +/- 0.000 | 3731.087 +/- 20.372 |
| head | 60 | 0.2 | 1000 | E4/scale1.5 | 4023.732 +/- 1.873 | 0.571 +/- 0.080 | -0.109 +/- 0.031 | 0.581 +/- 0.049 | 0.644 +/- 0.097 | 1.000 +/- 0.000 | 3731.087 +/- 20.372 |
| head | 60 | 0.2 | 1000 | E4/wrongFront | 4023.732 +/- 1.873 | -10.695 +/- 1.310 | -2.234 +/- 0.889 | 10.926 +/- 0.668 | 12.821 +/- 1.103 | 0.000 +/- 0.000 | 0.209 +/- 0.033 |
| head | 60 | 0.2 | 1000 | E5/measured3600 | 4023.732 +/- 1.873 | 0.540 +/- 0.082 | -0.113 +/- 0.047 | 0.552 +/- 0.048 | 0.609 +/- 0.082 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E5/measured600 | 4023.732 +/- 1.873 | 0.536 +/- 0.083 | -0.106 +/- 0.074 | 0.546 +/- 0.054 | 0.602 +/- 0.096 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E5/model | 4023.732 +/- 1.873 | 0.537 +/- 0.061 | -0.119 +/- 0.032 | 0.550 +/- 0.038 | 0.607 +/- 0.070 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E5/oracleShape | 4023.732 +/- 1.873 | 0.535 +/- 0.066 | -0.126 +/- 0.022 | 0.550 +/- 0.040 | 0.607 +/- 0.072 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E5/scale0.5 | 4023.732 +/- 1.873 | 0.579 +/- 0.350 | -0.111 +/- 0.177 | 0.589 +/- 0.180 | 1.541 +/- 1.232 | 0.979 +/- 0.036 | - |
| head | 60 | 0.2 | 1000 | E5/scale0.75 | 4023.732 +/- 1.873 | 0.640 +/- 0.084 | -0.075 +/- 0.038 | 0.645 +/- 0.051 | 0.709 +/- 0.101 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E5/scale0.9 | 4023.732 +/- 1.873 | 0.576 +/- 0.079 | -0.102 +/- 0.033 | 0.585 +/- 0.048 | 0.648 +/- 0.096 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E5/scale1.1 | 4023.732 +/- 1.873 | 0.516 +/- 0.058 | -0.135 +/- 0.031 | 0.533 +/- 0.037 | 0.589 +/- 0.067 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E5/scale1.25 | 4023.732 +/- 1.873 | 0.497 +/- 0.056 | -0.153 +/- 0.028 | 0.520 +/- 0.035 | 0.576 +/- 0.065 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E5/scale1.5 | 4023.732 +/- 1.873 | 0.490 +/- 0.050 | -0.172 +/- 0.026 | 0.519 +/- 0.031 | 0.572 +/- 0.061 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E5/wrongFront | 4023.732 +/- 1.873 | -9.642 +/- 0.311 | -4.930 +/- 0.069 | 10.829 +/- 0.166 | 11.118 +/- 0.364 | 0.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E6/measured3600 | 4023.732 +/- 1.873 | -0.684 +/- 0.445 | -0.977 +/- 1.199 | 1.193 +/- 0.705 | 4.661 +/- 1.134 | 0.854 +/- 0.072 | - |
| head | 60 | 0.2 | 1000 | E6/measured600 | 4023.732 +/- 1.873 | -0.912 +/- 0.356 | -0.678 +/- 1.394 | 1.137 +/- 0.598 | 5.013 +/- 1.067 | 0.812 +/- 0.062 | - |
| head | 60 | 0.2 | 1000 | E6/model | 4023.732 +/- 1.873 | -1.237 +/- 0.666 | -1.013 +/- 1.686 | 1.599 +/- 0.863 | 5.324 +/- 1.078 | 0.812 +/- 0.062 | - |
| head | 60 | 0.2 | 1000 | E6/oracleShape | 4023.732 +/- 1.873 | -1.082 +/- 0.806 | -1.171 +/- 1.415 | 1.594 +/- 0.888 | 5.111 +/- 1.430 | 0.833 +/- 0.095 | - |
| head | 60 | 0.2 | 1000 | E6/scale0.5 | 4023.732 +/- 1.873 | -5.511 +/- 0.498 | 4.611 +/- 1.000 | 7.186 +/- 0.178 | 9.834 +/- 0.202 | 0.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E6/scale0.75 | 4023.732 +/- 1.873 | -6.264 +/- 0.620 | 4.286 +/- 1.338 | 7.590 +/- 0.164 | 10.282 +/- 0.222 | 0.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E6/scale0.9 | 4023.732 +/- 1.873 | -5.224 +/- 0.712 | 0.137 +/- 0.458 | 5.225 +/- 0.404 | 9.544 +/- 0.798 | 0.229 +/- 0.036 | - |
| head | 60 | 0.2 | 1000 | E6/scale1.1 | 4023.732 +/- 1.873 | -0.041 +/- 0.410 | -0.329 +/- 0.346 | 0.332 +/- 0.228 | 1.818 +/- 1.966 | 0.979 +/- 0.036 | - |
| head | 60 | 0.2 | 1000 | E6/scale1.25 | 4023.732 +/- 1.873 | 0.319 +/- 0.033 | -0.107 +/- 0.071 | 0.337 +/- 0.010 | 0.447 +/- 0.064 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E6/scale1.5 | 4023.732 +/- 1.873 | 0.357 +/- 0.006 | 0.106 +/- 0.056 | 0.373 +/- 0.012 | 0.443 +/- 0.072 | 1.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E6/wrongFront | 4023.732 +/- 1.873 | -8.734 +/- 0.115 | -5.408 +/- 0.014 | 10.273 +/- 0.059 | 10.309 +/- 0.101 | 0.000 +/- 0.000 | - |
| head | 60 | 0.2 | 1000 | E7/model | 4023.732 +/- 1.873 | 0.481 +/- 0.073 | -0.009 +/- 0.029 | 0.481 +/- 0.042 | 0.564 +/- 0.103 | 1.000 +/- 0.000 | 4056.112 +/- 12.233 |
| head1m | 60 | 0 | 1000 | E0 | 0.000 +/- 0.000 | 2.597 +/- 4.239 | -0.052 +/- 0.211 | 2.598 +/- 2.449 | 13.372 +/- 13.969 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | -2.939 +/- 1.765 | -2.997 +/- 1.090 | 4.198 +/- 0.264 | 7.510 +/- 5.865 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | -5.786 +/- 3.357 | -2.707 +/- 1.906 | 6.387 +/- 1.713 | 17.365 +/- 12.511 | 0.896 +/- 0.095 | - |
| head1m | 60 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | 2.605 +/- 4.242 | -0.436 +/- 0.460 | 2.642 +/- 2.446 | 13.482 +/- 13.876 | 0.979 +/- 0.036 | 48.460 +/- 1.679 |
| head1m | 60 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 3.893 +/- 0.135 | -0.191 +/- 0.184 | 3.897 +/- 0.076 | 4.018 +/- 0.142 | 1.000 +/- 0.000 | 0.002 +/- 0.002 |
| head1m | 60 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 3.915 +/- 0.162 | -0.203 +/- 0.168 | 3.920 +/- 0.091 | 4.041 +/- 0.168 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | 2.597 +/- 4.239 | -0.052 +/- 0.211 | 2.598 +/- 2.449 | 13.372 +/- 13.969 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 5.867 +/- 0.383 | -0.653 +/- 0.133 | 5.903 +/- 0.212 | 6.283 +/- 0.206 | 1.000 +/- 0.000 | 48.460 +/- 1.679 |
| head1m | 60 | 0.1 | 1000 | E0 | 2011.866 +/- 0.936 | -10.320 +/- 9.934 | 9.718 +/- 1.741 | 14.175 +/- 4.368 | 28.254 +/- 20.094 | 0.792 +/- 0.072 | - |
| head1m | 60 | 0.1 | 1000 | E1/measured3600 | 2011.866 +/- 0.936 | -14.372 +/- 8.725 | -1.810 +/- 2.083 | 14.485 +/- 4.942 | 30.127 +/- 12.340 | 0.625 +/- 0.225 | - |
| head1m | 60 | 0.1 | 1000 | E1/measured600 | 2011.866 +/- 0.936 | -16.382 +/- 8.637 | -4.749 +/- 7.928 | 17.057 +/- 5.279 | 32.630 +/- 15.702 | 0.583 +/- 0.219 | - |
| head1m | 60 | 0.1 | 1000 | E1/model | 2011.866 +/- 0.936 | -13.364 +/- 8.782 | -1.735 +/- 4.540 | 13.476 +/- 5.197 | 28.149 +/- 12.553 | 0.646 +/- 0.237 | - |
| head1m | 60 | 0.1 | 1000 | E1/oracleShape | 2011.866 +/- 0.936 | -14.751 +/- 8.514 | -2.820 +/- 5.026 | 15.018 +/- 5.128 | 29.032 +/- 12.278 | 0.625 +/- 0.225 | - |
| head1m | 60 | 0.1 | 1000 | E1/scale0.5 | 2011.866 +/- 0.936 | -13.364 +/- 8.782 | -1.735 +/- 4.540 | 13.476 +/- 5.197 | 28.149 +/- 12.553 | 0.646 +/- 0.237 | - |
| head1m | 60 | 0.1 | 1000 | E1/scale0.75 | 2011.866 +/- 0.936 | -13.364 +/- 8.782 | -1.735 +/- 4.540 | 13.476 +/- 5.197 | 28.149 +/- 12.553 | 0.646 +/- 0.237 | - |
| head1m | 60 | 0.1 | 1000 | E1/scale0.9 | 2011.866 +/- 0.936 | -13.364 +/- 8.782 | -1.735 +/- 4.540 | 13.476 +/- 5.197 | 28.149 +/- 12.553 | 0.646 +/- 0.237 | - |
| head1m | 60 | 0.1 | 1000 | E1/scale1.1 | 2011.866 +/- 0.936 | -13.364 +/- 8.782 | -1.735 +/- 4.540 | 13.476 +/- 5.197 | 28.149 +/- 12.553 | 0.646 +/- 0.237 | - |
| head1m | 60 | 0.1 | 1000 | E1/scale1.25 | 2011.866 +/- 0.936 | -13.364 +/- 8.782 | -1.735 +/- 4.540 | 13.476 +/- 5.197 | 28.149 +/- 12.553 | 0.646 +/- 0.237 | - |
| head1m | 60 | 0.1 | 1000 | E1/scale1.5 | 2011.866 +/- 0.936 | -13.364 +/- 8.782 | -1.735 +/- 4.540 | 13.476 +/- 5.197 | 28.149 +/- 12.553 | 0.646 +/- 0.237 | - |
| head1m | 60 | 0.1 | 1000 | E1/wrongFront | 2011.866 +/- 0.936 | 2.873 +/- 1.724 | 4.350 +/- 0.188 | 5.213 +/- 0.624 | 7.320 +/- 0.799 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E2/measured3600 | 2011.866 +/- 0.936 | -14.237 +/- 8.187 | -0.718 +/- 2.322 | 14.255 +/- 4.663 | 29.896 +/- 12.579 | 0.625 +/- 0.225 | - |
| head1m | 60 | 0.1 | 1000 | E2/measured600 | 2011.866 +/- 0.936 | -17.110 +/- 9.446 | -3.812 +/- 6.891 | 17.529 +/- 5.683 | 32.442 +/- 15.885 | 0.562 +/- 0.250 | - |
| head1m | 60 | 0.1 | 1000 | E2/model | 2011.866 +/- 0.936 | -16.171 +/- 8.888 | -3.204 +/- 2.488 | 16.485 +/- 5.186 | 31.700 +/- 14.117 | 0.583 +/- 0.253 | - |
| head1m | 60 | 0.1 | 1000 | E2/oracleShape | 2011.866 +/- 0.936 | -15.508 +/- 7.872 | -2.872 +/- 2.025 | 15.772 +/- 4.544 | 30.783 +/- 12.923 | 0.604 +/- 0.219 | - |
| head1m | 60 | 0.1 | 1000 | E2/scale0.5 | 2011.866 +/- 0.936 | -16.171 +/- 8.888 | -3.204 +/- 2.488 | 16.485 +/- 5.186 | 31.700 +/- 14.117 | 0.583 +/- 0.253 | - |
| head1m | 60 | 0.1 | 1000 | E2/scale0.75 | 2011.866 +/- 0.936 | -16.171 +/- 8.888 | -3.204 +/- 2.488 | 16.485 +/- 5.186 | 31.700 +/- 14.117 | 0.583 +/- 0.253 | - |
| head1m | 60 | 0.1 | 1000 | E2/scale0.9 | 2011.866 +/- 0.936 | -16.171 +/- 8.888 | -3.204 +/- 2.488 | 16.485 +/- 5.186 | 31.700 +/- 14.117 | 0.583 +/- 0.253 | - |
| head1m | 60 | 0.1 | 1000 | E2/scale1.1 | 2011.866 +/- 0.936 | -16.171 +/- 8.888 | -3.204 +/- 2.488 | 16.485 +/- 5.186 | 31.700 +/- 14.117 | 0.583 +/- 0.253 | - |
| head1m | 60 | 0.1 | 1000 | E2/scale1.25 | 2011.866 +/- 0.936 | -16.171 +/- 8.888 | -3.204 +/- 2.488 | 16.485 +/- 5.186 | 31.700 +/- 14.117 | 0.583 +/- 0.253 | - |
| head1m | 60 | 0.1 | 1000 | E2/scale1.5 | 2011.866 +/- 0.936 | -16.171 +/- 8.888 | -3.204 +/- 2.488 | 16.485 +/- 5.186 | 31.700 +/- 14.117 | 0.583 +/- 0.253 | - |
| head1m | 60 | 0.1 | 1000 | E2/wrongFront | 2011.866 +/- 0.936 | 3.274 +/- 1.754 | 3.812 +/- 0.331 | 5.025 +/- 0.775 | 7.382 +/- 0.955 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E3/model | 2011.866 +/- 0.936 | -0.828 +/- 5.374 | 0.356 +/- 0.938 | 0.901 +/- 3.026 | 21.819 +/- 13.466 | 0.958 +/- 0.036 | 1932.675 +/- 14.821 |
| head1m | 60 | 0.1 | 1000 | E4/measured3600 | 2011.866 +/- 0.936 | 3.644 +/- 0.180 | 0.135 +/- 0.269 | 3.647 +/- 0.102 | 3.833 +/- 0.197 | 1.000 +/- 0.000 | 1603.185 +/- 31.533 |
| head1m | 60 | 0.1 | 1000 | E4/measured600 | 2011.866 +/- 0.936 | 3.608 +/- 0.141 | 0.207 +/- 0.315 | 3.614 +/- 0.076 | 3.795 +/- 0.168 | 1.000 +/- 0.000 | 1561.730 +/- 43.831 |
| head1m | 60 | 0.1 | 1000 | E4/model | 2011.866 +/- 0.936 | 3.642 +/- 0.165 | 0.189 +/- 0.271 | 3.647 +/- 0.091 | 3.833 +/- 0.181 | 1.000 +/- 0.000 | 1608.726 +/- 30.504 |
| head1m | 60 | 0.1 | 1000 | E4/oracleShape | 2011.866 +/- 0.936 | 3.607 +/- 0.173 | 0.188 +/- 0.249 | 3.612 +/- 0.097 | 3.801 +/- 0.190 | 1.000 +/- 0.000 | 1612.023 +/- 30.206 |
| head1m | 60 | 0.1 | 1000 | E4/scale0.5 | 2011.866 +/- 0.936 | 3.642 +/- 0.165 | 0.189 +/- 0.271 | 3.647 +/- 0.091 | 3.833 +/- 0.181 | 1.000 +/- 0.000 | 1608.726 +/- 30.504 |
| head1m | 60 | 0.1 | 1000 | E4/scale0.75 | 2011.866 +/- 0.936 | 3.642 +/- 0.165 | 0.189 +/- 0.271 | 3.647 +/- 0.091 | 3.833 +/- 0.181 | 1.000 +/- 0.000 | 1608.726 +/- 30.504 |
| head1m | 60 | 0.1 | 1000 | E4/scale0.9 | 2011.866 +/- 0.936 | 3.642 +/- 0.165 | 0.189 +/- 0.271 | 3.647 +/- 0.091 | 3.833 +/- 0.181 | 1.000 +/- 0.000 | 1608.726 +/- 30.504 |
| head1m | 60 | 0.1 | 1000 | E4/scale1.1 | 2011.866 +/- 0.936 | 3.642 +/- 0.165 | 0.189 +/- 0.271 | 3.647 +/- 0.091 | 3.833 +/- 0.181 | 1.000 +/- 0.000 | 1608.726 +/- 30.504 |
| head1m | 60 | 0.1 | 1000 | E4/scale1.25 | 2011.866 +/- 0.936 | 3.642 +/- 0.165 | 0.189 +/- 0.271 | 3.647 +/- 0.091 | 3.833 +/- 0.181 | 1.000 +/- 0.000 | 1608.726 +/- 30.504 |
| head1m | 60 | 0.1 | 1000 | E4/scale1.5 | 2011.866 +/- 0.936 | 3.642 +/- 0.165 | 0.189 +/- 0.271 | 3.647 +/- 0.091 | 3.833 +/- 0.181 | 1.000 +/- 0.000 | 1608.726 +/- 30.504 |
| head1m | 60 | 0.1 | 1000 | E4/wrongFront | 2011.866 +/- 0.936 | 1.044 +/- 4.249 | 5.515 +/- 0.577 | 5.613 +/- 0.228 | 13.968 +/- 12.897 | 0.979 +/- 0.036 | 0.396 +/- 0.049 |
| head1m | 60 | 0.1 | 1000 | E5/measured3600 | 2011.866 +/- 0.936 | 3.610 +/- 0.197 | -0.137 +/- 0.287 | 3.612 +/- 0.116 | 3.789 +/- 0.206 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E5/measured600 | 2011.866 +/- 0.936 | 3.566 +/- 0.128 | -0.089 +/- 0.369 | 3.567 +/- 0.077 | 3.737 +/- 0.158 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E5/model | 2011.866 +/- 0.936 | 3.622 +/- 0.159 | -0.063 +/- 0.288 | 3.623 +/- 0.094 | 3.801 +/- 0.173 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E5/oracleShape | 2011.866 +/- 0.936 | 3.576 +/- 0.179 | -0.066 +/- 0.266 | 3.577 +/- 0.104 | 3.758 +/- 0.187 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E5/scale0.5 | 2011.866 +/- 0.936 | 3.636 +/- 0.225 | 1.231 +/- 0.718 | 3.838 +/- 0.037 | 4.224 +/- 0.147 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E5/scale0.75 | 2011.866 +/- 0.936 | 3.670 +/- 0.167 | 0.344 +/- 0.278 | 3.686 +/- 0.089 | 3.881 +/- 0.184 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E5/scale0.9 | 2011.866 +/- 0.936 | 3.640 +/- 0.161 | 0.085 +/- 0.294 | 3.641 +/- 0.091 | 3.825 +/- 0.175 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E5/scale1.1 | 2011.866 +/- 0.936 | 3.596 +/- 0.165 | -0.195 +/- 0.279 | 3.601 +/- 0.099 | 3.774 +/- 0.177 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E5/scale1.25 | 2011.866 +/- 0.936 | 3.573 +/- 0.166 | -0.360 +/- 0.265 | 3.591 +/- 0.104 | 3.757 +/- 0.182 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E5/scale1.5 | 2011.866 +/- 0.936 | 3.518 +/- 0.145 | -0.564 +/- 0.270 | 3.562 +/- 0.099 | 3.719 +/- 0.174 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E5/wrongFront | 2011.866 +/- 0.936 | -1.772 +/- 8.251 | 6.454 +/- 0.394 | 6.693 +/- 1.480 | 18.400 +/- 19.497 | 0.958 +/- 0.072 | - |
| head1m | 60 | 0.1 | 1000 | E6/measured3600 | 2011.866 +/- 0.936 | 1.587 +/- 4.311 | -0.203 +/- 0.755 | 1.600 +/- 2.479 | 13.726 +/- 13.853 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0.1 | 1000 | E6/measured600 | 2011.866 +/- 0.936 | -8.689 +/- 16.697 | 0.242 +/- 1.326 | 8.692 +/- 9.641 | 31.380 +/- 25.949 | 0.896 +/- 0.130 | - |
| head1m | 60 | 0.1 | 1000 | E6/model | 2011.866 +/- 0.936 | -0.792 +/- 5.128 | -0.287 +/- 0.820 | 0.843 +/- 2.634 | 21.866 +/- 13.395 | 0.958 +/- 0.036 | - |
| head1m | 60 | 0.1 | 1000 | E6/oracleShape | 2011.866 +/- 0.936 | 1.433 +/- 4.298 | -0.038 +/- 1.148 | 1.433 +/- 2.484 | 13.701 +/- 13.878 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0.1 | 1000 | E6/scale0.5 | 2011.866 +/- 0.936 | -7.994 +/- 16.451 | 6.028 +/- 1.248 | 10.012 +/- 7.923 | 25.702 +/- 28.367 | 0.875 +/- 0.165 | - |
| head1m | 60 | 0.1 | 1000 | E6/scale0.75 | 2011.866 +/- 0.936 | -6.205 +/- 11.058 | 2.987 +/- 0.882 | 6.887 +/- 5.935 | 28.899 +/- 22.168 | 0.896 +/- 0.095 | - |
| head1m | 60 | 0.1 | 1000 | E6/scale0.9 | 2011.866 +/- 0.936 | -3.140 +/- 7.897 | 1.114 +/- 0.894 | 3.332 +/- 4.441 | 25.805 +/- 17.764 | 0.938 +/- 0.062 | - |
| head1m | 60 | 0.1 | 1000 | E6/scale1.1 | 2011.866 +/- 0.936 | -1.203 +/- 3.142 | -1.564 +/- 0.379 | 1.973 +/- 1.216 | 21.440 +/- 14.298 | 0.958 +/- 0.036 | - |
| head1m | 60 | 0.1 | 1000 | E6/scale1.25 | 2011.866 +/- 0.936 | -6.648 +/- 0.522 | -2.379 +/- 2.028 | 7.061 +/- 0.643 | 31.236 +/- 1.907 | 0.896 +/- 0.036 | - |
| head1m | 60 | 0.1 | 1000 | E6/scale1.5 | 2011.866 +/- 0.936 | -13.038 +/- 8.225 | -1.785 +/- 4.593 | 13.160 +/- 4.891 | 28.116 +/- 12.522 | 0.646 +/- 0.237 | - |
| head1m | 60 | 0.1 | 1000 | E6/wrongFront | 2011.866 +/- 0.936 | 2.142 +/- 1.843 | 4.292 +/- 0.320 | 4.797 +/- 0.638 | 6.796 +/- 0.867 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.1 | 1000 | E7/model | 2011.866 +/- 0.936 | 4.973 +/- 0.389 | -0.159 +/- 0.515 | 4.976 +/- 0.223 | 5.615 +/- 0.258 | 1.000 +/- 0.000 | 1932.675 +/- 14.821 |
| head1m | 60 | 0.2 | 1000 | E0 | 4023.732 +/- 1.873 | -34.467 +/- 17.099 | 14.559 +/- 0.663 | 37.416 +/- 9.226 | 55.460 +/- 16.150 | 0.562 +/- 0.165 | - |
| head1m | 60 | 0.2 | 1000 | E1/measured3600 | 4023.732 +/- 1.873 | -23.885 +/- 2.702 | -11.197 +/- 6.093 | 26.380 +/- 2.903 | 48.076 +/- 4.971 | 0.521 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E1/measured600 | 4023.732 +/- 1.873 | -27.427 +/- 0.939 | -13.156 +/- 4.495 | 30.419 +/- 0.641 | 50.153 +/- 4.428 | 0.417 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E1/model | 4023.732 +/- 1.873 | -25.632 +/- 5.303 | -10.948 +/- 8.284 | 27.872 +/- 1.075 | 47.443 +/- 7.901 | 0.479 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E1/oracleShape | 4023.732 +/- 1.873 | -26.914 +/- 5.699 | -13.712 +/- 6.951 | 30.206 +/- 2.126 | 49.280 +/- 9.243 | 0.479 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E1/scale0.5 | 4023.732 +/- 1.873 | -25.632 +/- 5.303 | -10.948 +/- 8.284 | 27.872 +/- 1.075 | 47.443 +/- 7.901 | 0.479 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E1/scale0.75 | 4023.732 +/- 1.873 | -25.632 +/- 5.303 | -10.948 +/- 8.284 | 27.872 +/- 1.075 | 47.443 +/- 7.901 | 0.479 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E1/scale0.9 | 4023.732 +/- 1.873 | -25.632 +/- 5.303 | -10.948 +/- 8.284 | 27.872 +/- 1.075 | 47.443 +/- 7.901 | 0.479 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E1/scale1.1 | 4023.732 +/- 1.873 | -25.632 +/- 5.303 | -10.948 +/- 8.284 | 27.872 +/- 1.075 | 47.443 +/- 7.901 | 0.479 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E1/scale1.25 | 4023.732 +/- 1.873 | -25.632 +/- 5.303 | -10.948 +/- 8.284 | 27.872 +/- 1.075 | 47.443 +/- 7.901 | 0.479 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E1/scale1.5 | 4023.732 +/- 1.873 | -25.632 +/- 5.303 | -10.948 +/- 8.284 | 27.872 +/- 1.075 | 47.443 +/- 7.901 | 0.479 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E1/wrongFront | 4023.732 +/- 1.873 | 3.573 +/- 1.054 | 5.376 +/- 0.889 | 6.455 +/- 0.763 | 8.525 +/- 1.107 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.2 | 1000 | E2/measured3600 | 4023.732 +/- 1.873 | -26.335 +/- 2.632 | -13.037 +/- 6.712 | 29.385 +/- 3.077 | 50.144 +/- 5.441 | 0.458 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E2/measured600 | 4023.732 +/- 1.873 | -28.213 +/- 1.823 | -12.484 +/- 5.109 | 30.852 +/- 0.390 | 50.602 +/- 4.339 | 0.396 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E2/model | 4023.732 +/- 1.873 | -27.440 +/- 6.024 | -10.993 +/- 5.247 | 29.560 +/- 2.211 | 49.369 +/- 8.954 | 0.438 +/- 0.062 | - |
| head1m | 60 | 0.2 | 1000 | E2/oracleShape | 4023.732 +/- 1.873 | -27.881 +/- 7.206 | -12.981 +/- 8.295 | 30.755 +/- 2.574 | 49.929 +/- 9.890 | 0.458 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E2/scale0.5 | 4023.732 +/- 1.873 | -27.440 +/- 6.024 | -10.993 +/- 5.247 | 29.560 +/- 2.211 | 49.369 +/- 8.954 | 0.438 +/- 0.062 | - |
| head1m | 60 | 0.2 | 1000 | E2/scale0.75 | 4023.732 +/- 1.873 | -27.440 +/- 6.024 | -10.993 +/- 5.247 | 29.560 +/- 2.211 | 49.369 +/- 8.954 | 0.438 +/- 0.062 | - |
| head1m | 60 | 0.2 | 1000 | E2/scale0.9 | 4023.732 +/- 1.873 | -27.440 +/- 6.024 | -10.993 +/- 5.247 | 29.560 +/- 2.211 | 49.369 +/- 8.954 | 0.438 +/- 0.062 | - |
| head1m | 60 | 0.2 | 1000 | E2/scale1.1 | 4023.732 +/- 1.873 | -27.440 +/- 6.024 | -10.993 +/- 5.247 | 29.560 +/- 2.211 | 49.369 +/- 8.954 | 0.438 +/- 0.062 | - |
| head1m | 60 | 0.2 | 1000 | E2/scale1.25 | 4023.732 +/- 1.873 | -27.440 +/- 6.024 | -10.993 +/- 5.247 | 29.560 +/- 2.211 | 49.369 +/- 8.954 | 0.438 +/- 0.062 | - |
| head1m | 60 | 0.2 | 1000 | E2/scale1.5 | 4023.732 +/- 1.873 | -27.440 +/- 6.024 | -10.993 +/- 5.247 | 29.560 +/- 2.211 | 49.369 +/- 8.954 | 0.438 +/- 0.062 | - |
| head1m | 60 | 0.2 | 1000 | E2/wrongFront | 4023.732 +/- 1.873 | 3.937 +/- 1.148 | 5.082 +/- 0.942 | 6.428 +/- 0.829 | 8.591 +/- 1.038 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.2 | 1000 | E3/model | 4023.732 +/- 1.873 | -22.860 +/- 20.339 | 1.416 +/- 3.357 | 22.903 +/- 11.608 | 50.714 +/- 25.036 | 0.771 +/- 0.157 | 3866.392 +/- 17.632 |
| head1m | 60 | 0.2 | 1000 | E4/measured3600 | 4023.732 +/- 1.873 | 1.050 +/- 3.888 | 0.140 +/- 0.886 | 1.060 +/- 2.292 | 11.981 +/- 14.188 | 0.979 +/- 0.036 | 3570.863 +/- 10.859 |
| head1m | 60 | 0.2 | 1000 | E4/measured600 | 4023.732 +/- 1.873 | -1.597 +/- 8.033 | 0.298 +/- 0.923 | 1.625 +/- 4.465 | 16.039 +/- 21.104 | 0.958 +/- 0.072 | 3446.064 +/- 36.104 |
| head1m | 60 | 0.2 | 1000 | E4/model | 4023.732 +/- 1.873 | -1.419 +/- 8.147 | 0.281 +/- 0.901 | 1.446 +/- 4.514 | 15.985 +/- 21.084 | 0.958 +/- 0.072 | 3583.936 +/- 5.685 |
| head1m | 60 | 0.2 | 1000 | E4/oracleShape | 4023.732 +/- 1.873 | -1.504 +/- 8.192 | 0.260 +/- 0.879 | 1.527 +/- 4.574 | 15.942 +/- 21.093 | 0.958 +/- 0.072 | 3591.007 +/- 17.664 |
| head1m | 60 | 0.2 | 1000 | E4/scale0.5 | 4023.732 +/- 1.873 | -1.419 +/- 8.147 | 0.281 +/- 0.901 | 1.446 +/- 4.514 | 15.985 +/- 21.084 | 0.958 +/- 0.072 | 3583.936 +/- 5.685 |
| head1m | 60 | 0.2 | 1000 | E4/scale0.75 | 4023.732 +/- 1.873 | -1.419 +/- 8.147 | 0.281 +/- 0.901 | 1.446 +/- 4.514 | 15.985 +/- 21.084 | 0.958 +/- 0.072 | 3583.936 +/- 5.685 |
| head1m | 60 | 0.2 | 1000 | E4/scale0.9 | 4023.732 +/- 1.873 | -1.419 +/- 8.147 | 0.281 +/- 0.901 | 1.446 +/- 4.514 | 15.985 +/- 21.084 | 0.958 +/- 0.072 | 3583.936 +/- 5.685 |
| head1m | 60 | 0.2 | 1000 | E4/scale1.1 | 4023.732 +/- 1.873 | -1.419 +/- 8.147 | 0.281 +/- 0.901 | 1.446 +/- 4.514 | 15.985 +/- 21.084 | 0.958 +/- 0.072 | 3583.936 +/- 5.685 |
| head1m | 60 | 0.2 | 1000 | E4/scale1.25 | 4023.732 +/- 1.873 | -1.419 +/- 8.147 | 0.281 +/- 0.901 | 1.446 +/- 4.514 | 15.985 +/- 21.084 | 0.958 +/- 0.072 | 3583.936 +/- 5.685 |
| head1m | 60 | 0.2 | 1000 | E4/scale1.5 | 4023.732 +/- 1.873 | -1.419 +/- 8.147 | 0.281 +/- 0.901 | 1.446 +/- 4.514 | 15.985 +/- 21.084 | 0.958 +/- 0.072 | 3583.936 +/- 5.685 |
| head1m | 60 | 0.2 | 1000 | E4/wrongFront | 4023.732 +/- 1.873 | -10.160 +/- 12.396 | 7.215 +/- 0.150 | 12.461 +/- 5.885 | 33.301 +/- 22.600 | 0.854 +/- 0.157 | 0.083 +/- 0.020 |
| head1m | 60 | 0.2 | 1000 | E5/measured3600 | 4023.732 +/- 1.873 | 1.068 +/- 3.910 | -0.403 +/- 0.728 | 1.142 +/- 1.981 | 11.882 +/- 14.266 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E5/measured600 | 4023.732 +/- 1.873 | 0.892 +/- 3.710 | -0.085 +/- 0.908 | 0.897 +/- 2.084 | 12.011 +/- 14.362 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E5/model | 4023.732 +/- 1.873 | 1.054 +/- 3.816 | -0.053 +/- 0.909 | 1.055 +/- 2.174 | 11.947 +/- 14.238 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E5/oracleShape | 4023.732 +/- 1.873 | 1.006 +/- 3.921 | -0.278 +/- 0.714 | 1.044 +/- 2.085 | 11.861 +/- 14.281 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E5/scale0.5 | 4023.732 +/- 1.873 | -4.353 +/- 7.447 | 3.594 +/- 1.065 | 5.645 +/- 2.953 | 25.118 +/- 17.774 | 0.938 +/- 0.062 | - |
| head1m | 60 | 0.2 | 1000 | E5/scale0.75 | 4023.732 +/- 1.873 | -1.442 +/- 8.142 | 1.085 +/- 0.760 | 1.804 +/- 3.494 | 16.240 +/- 20.868 | 0.958 +/- 0.072 | - |
| head1m | 60 | 0.2 | 1000 | E5/scale0.9 | 4023.732 +/- 1.873 | 1.082 +/- 3.798 | 0.316 +/- 0.923 | 1.127 +/- 2.254 | 12.045 +/- 14.156 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E5/scale1.1 | 4023.732 +/- 1.873 | 1.087 +/- 3.848 | -0.533 +/- 0.722 | 1.211 +/- 1.822 | 11.889 +/- 14.292 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E5/scale1.25 | 4023.732 +/- 1.873 | 1.063 +/- 3.840 | -0.816 +/- 0.723 | 1.341 +/- 1.517 | 11.885 +/- 14.298 | 0.979 +/- 0.036 | - |
| head1m | 60 | 0.2 | 1000 | E5/scale1.5 | 4023.732 +/- 1.873 | 0.395 +/- 0.565 | -1.929 +/- 4.566 | 1.969 +/- 2.589 | 18.359 +/- 0.688 | 0.938 +/- 0.000 | - |
| head1m | 60 | 0.2 | 1000 | E5/wrongFront | 4023.732 +/- 1.873 | -9.876 +/- 12.073 | 7.986 +/- 0.177 | 12.701 +/- 5.480 | 33.450 +/- 22.211 | 0.875 +/- 0.125 | - |
| head1m | 60 | 0.2 | 1000 | E6/measured3600 | 4023.732 +/- 1.873 | -22.587 +/- 18.688 | 0.699 +/- 3.614 | 22.598 +/- 10.720 | 50.874 +/- 24.473 | 0.771 +/- 0.144 | - |
| head1m | 60 | 0.2 | 1000 | E6/measured600 | 4023.732 +/- 1.873 | -29.018 +/- 23.825 | -1.262 +/- 3.601 | 29.046 +/- 13.729 | 56.221 +/- 26.771 | 0.708 +/- 0.201 | - |
| head1m | 60 | 0.2 | 1000 | E6/model | 4023.732 +/- 1.873 | -24.897 +/- 23.693 | 0.032 +/- 3.368 | 24.897 +/- 13.677 | 52.345 +/- 27.072 | 0.750 +/- 0.188 | - |
| head1m | 60 | 0.2 | 1000 | E6/oracleShape | 4023.732 +/- 1.873 | -25.293 +/- 21.196 | 0.356 +/- 3.151 | 25.295 +/- 12.211 | 52.992 +/- 26.345 | 0.750 +/- 0.165 | - |
| head1m | 60 | 0.2 | 1000 | E6/scale0.5 | 4023.732 +/- 1.873 | -23.927 +/- 8.905 | 9.354 +/- 0.242 | 25.690 +/- 4.740 | 49.242 +/- 9.957 | 0.667 +/- 0.130 | - |
| head1m | 60 | 0.2 | 1000 | E6/scale0.75 | 4023.732 +/- 1.873 | -19.160 +/- 12.697 | 6.685 +/- 1.073 | 20.292 +/- 7.109 | 46.137 +/- 14.263 | 0.771 +/- 0.157 | - |
| head1m | 60 | 0.2 | 1000 | E6/scale0.9 | 4023.732 +/- 1.873 | -21.476 +/- 15.417 | 2.481 +/- 1.730 | 21.619 +/- 8.728 | 50.968 +/- 18.717 | 0.792 +/- 0.130 | - |
| head1m | 60 | 0.2 | 1000 | E6/scale1.1 | 4023.732 +/- 1.873 | -21.022 +/- 20.260 | -5.668 +/- 2.673 | 21.773 +/- 11.630 | 47.465 +/- 22.521 | 0.750 +/- 0.165 | - |
| head1m | 60 | 0.2 | 1000 | E6/scale1.25 | 4023.732 +/- 1.873 | -25.166 +/- 9.443 | -12.047 +/- 4.084 | 27.901 +/- 3.916 | 48.506 +/- 12.935 | 0.521 +/- 0.072 | - |
| head1m | 60 | 0.2 | 1000 | E6/scale1.5 | 4023.732 +/- 1.873 | -39.726 +/- 1.617 | -13.776 +/- 1.348 | 42.047 +/- 1.124 | 57.802 +/- 2.305 | 0.000 +/- 0.000 | - |
| head1m | 60 | 0.2 | 1000 | E6/wrongFront | 4023.732 +/- 1.873 | 3.110 +/- 0.809 | 5.304 +/- 0.831 | 6.149 +/- 0.649 | 8.304 +/- 1.062 | 1.000 +/- 0.000 | - |
| head1m | 60 | 0.2 | 1000 | E7/model | 4023.732 +/- 1.873 | -3.303 +/- 6.705 | -0.618 +/- 1.151 | 3.360 +/- 3.913 | 25.212 +/- 18.849 | 0.938 +/- 0.062 | 3866.392 +/- 17.632 |
| head5m | 60 | 0 | 1000 | E0 | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -35.366 +/- 0.053 | 39.143 +/- 0.028 | 39.144 +/- 0.048 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | -23.404 +/- 0.000 | -39.773 +/- 0.000 | 46.148 +/- 0.000 | 46.148 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | 14.952 +/- 2.839 |
| head5m | 60 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 10.835 +/- 0.552 | -1.561 +/- 0.401 | 10.947 +/- 0.299 | 11.382 +/- 0.639 | 1.000 +/- 0.000 | 0.000 +/- 0.000 |
| head5m | 60 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 10.795 +/- 0.561 | -1.532 +/- 0.403 | 10.903 +/- 0.305 | 11.339 +/- 0.647 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | -16.775 +/- 0.000 | -19.886 +/- 0.000 | 26.017 +/- 0.000 | 26.017 +/- 0.000 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 34.562 +/- 2.498 | -10.598 +/- 1.786 | 36.150 +/- 1.681 | 39.069 +/- 0.896 | 1.000 +/- 0.000 | 14.952 +/- 2.839 |
| head5m | 60 | 0.1 | 1000 | E0 | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | 34.689 +/- 0.116 | 38.532 +/- 0.060 | 38.535 +/- 0.105 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E1/measured3600 | 2011.866 +/- 0.936 | -21.185 +/- 7.639 | -29.873 +/- 9.267 | 36.622 +/- 1.814 | 55.251 +/- 28.115 | 0.979 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E1/measured600 | 2011.866 +/- 0.936 | -29.943 +/- 13.138 | -20.643 +/- 14.074 | 36.369 +/- 1.723 | 79.207 +/- 36.448 | 0.938 +/- 0.062 | - |
| head5m | 60 | 0.1 | 1000 | E1/model | 2011.866 +/- 0.936 | -25.573 +/- 7.619 | -24.619 +/- 9.382 | 35.497 +/- 0.592 | 71.760 +/- 28.177 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E1/oracleShape | 2011.866 +/- 0.936 | -25.573 +/- 7.619 | -24.634 +/- 9.476 | 35.508 +/- 0.630 | 71.736 +/- 28.059 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E1/scale0.5 | 2011.866 +/- 0.936 | -25.573 +/- 7.619 | -24.619 +/- 9.382 | 35.497 +/- 0.592 | 71.760 +/- 28.177 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E1/scale0.75 | 2011.866 +/- 0.936 | -25.573 +/- 7.619 | -24.619 +/- 9.382 | 35.497 +/- 0.592 | 71.760 +/- 28.177 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E1/scale0.9 | 2011.866 +/- 0.936 | -25.573 +/- 7.619 | -24.619 +/- 9.382 | 35.497 +/- 0.592 | 71.760 +/- 28.177 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E1/scale1.1 | 2011.866 +/- 0.936 | -25.573 +/- 7.619 | -24.619 +/- 9.382 | 35.497 +/- 0.592 | 71.760 +/- 28.177 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E1/scale1.25 | 2011.866 +/- 0.936 | -25.573 +/- 7.619 | -24.619 +/- 9.382 | 35.497 +/- 0.592 | 71.760 +/- 28.177 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E1/scale1.5 | 2011.866 +/- 0.936 | -25.573 +/- 7.619 | -24.619 +/- 9.382 | 35.497 +/- 0.592 | 71.760 +/- 28.177 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E1/wrongFront | 2011.866 +/- 0.936 | -17.346 +/- 0.989 | 35.634 +/- 0.197 | 39.632 +/- 0.341 | 39.731 +/- 0.749 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E2/measured3600 | 2011.866 +/- 0.936 | -31.690 +/- 7.176 | -28.725 +/- 9.568 | 42.771 +/- 0.640 | 75.968 +/- 25.825 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E2/measured600 | 2011.866 +/- 0.936 | -35.833 +/- 12.429 | -24.858 +/- 14.159 | 43.611 +/- 1.346 | 83.007 +/- 33.622 | 0.938 +/- 0.062 | - |
| head5m | 60 | 0.1 | 1000 | E2/model | 2011.866 +/- 0.936 | -31.690 +/- 7.176 | -28.725 +/- 9.568 | 42.771 +/- 0.640 | 75.968 +/- 25.825 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E2/oracleShape | 2011.866 +/- 0.936 | -31.690 +/- 7.176 | -28.725 +/- 9.568 | 42.771 +/- 0.640 | 75.968 +/- 25.825 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E2/scale0.5 | 2011.866 +/- 0.936 | -31.690 +/- 7.176 | -28.725 +/- 9.568 | 42.771 +/- 0.640 | 75.968 +/- 25.825 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E2/scale0.75 | 2011.866 +/- 0.936 | -31.690 +/- 7.176 | -28.725 +/- 9.568 | 42.771 +/- 0.640 | 75.968 +/- 25.825 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E2/scale0.9 | 2011.866 +/- 0.936 | -31.690 +/- 7.176 | -28.725 +/- 9.568 | 42.771 +/- 0.640 | 75.968 +/- 25.825 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E2/scale1.1 | 2011.866 +/- 0.936 | -31.690 +/- 7.176 | -28.725 +/- 9.568 | 42.771 +/- 0.640 | 75.968 +/- 25.825 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E2/scale1.25 | 2011.866 +/- 0.936 | -31.690 +/- 7.176 | -28.725 +/- 9.568 | 42.771 +/- 0.640 | 75.968 +/- 25.825 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E2/scale1.5 | 2011.866 +/- 0.936 | -31.690 +/- 7.176 | -28.725 +/- 9.568 | 42.771 +/- 0.640 | 75.968 +/- 25.825 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E2/wrongFront | 2011.866 +/- 0.936 | -23.956 +/- 0.957 | 39.773 +/- 0.000 | 46.430 +/- 0.285 | 46.503 +/- 0.616 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E3/model | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | -14.333 +/- 1.928 | 22.064 +/- 0.723 | 27.421 +/- 0.483 | 1.000 +/- 0.000 | 1760.742 +/- 19.616 |
| head5m | 60 | 0.1 | 1000 | E4/measured3600 | 2011.866 +/- 0.936 | 9.486 +/- 0.435 | -0.117 +/- 0.155 | 9.487 +/- 0.252 | 9.976 +/- 0.356 | 1.000 +/- 0.000 | 1539.861 +/- 29.106 |
| head5m | 60 | 0.1 | 1000 | E4/measured600 | 2011.866 +/- 0.936 | 9.695 +/- 0.080 | -0.089 +/- 0.387 | 9.696 +/- 0.047 | 10.162 +/- 0.072 | 1.000 +/- 0.000 | 1499.207 +/- 37.861 |
| head5m | 60 | 0.1 | 1000 | E4/model | 2011.866 +/- 0.936 | 9.511 +/- 0.154 | -0.120 +/- 0.213 | 9.511 +/- 0.090 | 9.997 +/- 0.080 | 1.000 +/- 0.000 | 1547.093 +/- 28.643 |
| head5m | 60 | 0.1 | 1000 | E4/oracleShape | 2011.866 +/- 0.936 | 9.408 +/- 0.285 | -0.111 +/- 0.207 | 9.409 +/- 0.166 | 9.901 +/- 0.210 | 1.000 +/- 0.000 | 1549.176 +/- 33.448 |
| head5m | 60 | 0.1 | 1000 | E4/scale0.5 | 2011.866 +/- 0.936 | 9.511 +/- 0.154 | -0.120 +/- 0.213 | 9.511 +/- 0.090 | 9.997 +/- 0.080 | 1.000 +/- 0.000 | 1547.093 +/- 28.643 |
| head5m | 60 | 0.1 | 1000 | E4/scale0.75 | 2011.866 +/- 0.936 | 9.511 +/- 0.154 | -0.120 +/- 0.213 | 9.511 +/- 0.090 | 9.997 +/- 0.080 | 1.000 +/- 0.000 | 1547.093 +/- 28.643 |
| head5m | 60 | 0.1 | 1000 | E4/scale0.9 | 2011.866 +/- 0.936 | 9.511 +/- 0.154 | -0.120 +/- 0.213 | 9.511 +/- 0.090 | 9.997 +/- 0.080 | 1.000 +/- 0.000 | 1547.093 +/- 28.643 |
| head5m | 60 | 0.1 | 1000 | E4/scale1.1 | 2011.866 +/- 0.936 | 9.511 +/- 0.154 | -0.120 +/- 0.213 | 9.511 +/- 0.090 | 9.997 +/- 0.080 | 1.000 +/- 0.000 | 1547.093 +/- 28.643 |
| head5m | 60 | 0.1 | 1000 | E4/scale1.25 | 2011.866 +/- 0.936 | 9.511 +/- 0.154 | -0.120 +/- 0.213 | 9.511 +/- 0.090 | 9.997 +/- 0.080 | 1.000 +/- 0.000 | 1547.093 +/- 28.643 |
| head5m | 60 | 0.1 | 1000 | E4/scale1.5 | 2011.866 +/- 0.936 | 9.511 +/- 0.154 | -0.120 +/- 0.213 | 9.511 +/- 0.090 | 9.997 +/- 0.080 | 1.000 +/- 0.000 | 1547.093 +/- 28.643 |
| head5m | 60 | 0.1 | 1000 | E4/wrongFront | 2011.866 +/- 0.936 | 9.511 +/- 0.182 | 5.185 +/- 0.374 | 10.833 +/- 0.051 | 11.497 +/- 0.067 | 1.000 +/- 0.000 | 0.505 +/- 0.097 |
| head5m | 60 | 0.1 | 1000 | E5/measured3600 | 2011.866 +/- 0.936 | 9.446 +/- 0.463 | -0.679 +/- 0.132 | 9.471 +/- 0.272 | 9.911 +/- 0.381 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E5/measured600 | 2011.866 +/- 0.936 | 9.745 +/- 0.119 | -0.743 +/- 0.447 | 9.773 +/- 0.065 | 10.179 +/- 0.110 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E5/model | 2011.866 +/- 0.936 | 9.505 +/- 0.108 | -0.681 +/- 0.202 | 9.529 +/- 0.069 | 9.968 +/- 0.032 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E5/oracleShape | 2011.866 +/- 0.936 | 9.380 +/- 0.270 | -0.666 +/- 0.208 | 9.403 +/- 0.163 | 9.850 +/- 0.195 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E5/scale0.5 | 2011.866 +/- 0.936 | 9.519 +/- 0.180 | 1.482 +/- 0.331 | 9.633 +/- 0.073 | 10.191 +/- 0.078 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E5/scale0.75 | 2011.866 +/- 0.936 | 9.515 +/- 0.141 | 0.132 +/- 0.273 | 9.516 +/- 0.079 | 10.019 +/- 0.064 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E5/scale0.9 | 2011.866 +/- 0.936 | 9.510 +/- 0.120 | -0.418 +/- 0.229 | 9.519 +/- 0.074 | 9.983 +/- 0.045 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E5/scale1.1 | 2011.866 +/- 0.936 | 9.500 +/- 0.098 | -0.871 +/- 0.180 | 9.540 +/- 0.063 | 9.955 +/- 0.023 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E5/scale1.25 | 2011.866 +/- 0.936 | 9.493 +/- 0.091 | -1.043 +/- 0.150 | 9.550 +/- 0.056 | 9.931 +/- 0.027 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E5/scale1.5 | 2011.866 +/- 0.936 | 9.492 +/- 0.095 | -1.107 +/- 0.108 | 9.556 +/- 0.053 | 9.888 +/- 0.053 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E5/wrongFront | 2011.866 +/- 0.936 | 6.520 +/- 6.875 | 7.816 +/- 5.330 | 10.179 +/- 0.195 | 21.779 +/- 17.064 | 0.958 +/- 0.072 | - |
| head5m | 60 | 0.1 | 1000 | E6/measured3600 | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | -19.338 +/- 0.950 | 25.600 +/- 0.414 | 26.835 +/- 1.417 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E6/measured600 | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | -17.112 +/- 5.538 | 23.963 +/- 2.283 | 27.374 +/- 1.683 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E6/model | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | -20.171 +/- 0.944 | 26.235 +/- 0.419 | 27.646 +/- 1.382 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E6/oracleShape | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | -19.616 +/- 1.259 | 25.810 +/- 0.553 | 27.112 +/- 1.247 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E6/scale0.5 | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | 15.931 +/- 6.969 | 23.134 +/- 2.771 | 34.285 +/- 1.390 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E6/scale0.75 | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | -6.525 +/- 3.374 | 17.999 +/- 0.706 | 29.311 +/- 0.818 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E6/scale0.9 | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | -14.338 +/- 1.933 | 22.067 +/- 0.725 | 27.415 +/- 0.489 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E6/scale1.1 | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | -26.380 +/- 2.568 | 31.262 +/- 1.251 | 31.941 +/- 2.117 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E6/scale1.25 | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | -32.845 +/- 0.533 | 36.881 +/- 0.274 | 37.088 +/- 0.395 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E6/scale1.5 | 2011.866 +/- 0.936 | -25.574 +/- 7.620 | -24.635 +/- 9.383 | 35.509 +/- 0.592 | 71.776 +/- 28.178 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.1 | 1000 | E6/wrongFront | 2011.866 +/- 0.936 | -16.775 +/- 0.000 | 35.234 +/- 0.078 | 39.023 +/- 0.041 | 39.027 +/- 0.071 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.1 | 1000 | E7/model | 2011.866 +/- 0.936 | 14.697 +/- 21.438 | -8.295 +/- 1.540 | 16.876 +/- 10.515 | 75.439 +/- 64.498 | 0.979 +/- 0.036 | 1760.742 +/- 19.616 |
| head5m | 60 | 0.2 | 1000 | E0 | 4023.732 +/- 1.873 | -16.775 +/- 0.000 | 36.095 +/- 0.106 | 39.803 +/- 0.055 | 39.807 +/- 0.098 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E1/measured3600 | 4023.732 +/- 1.873 | -55.905 +/- 25.918 | -27.368 +/- 17.205 | 62.245 +/- 12.583 | 131.511 +/- 53.665 | 0.854 +/- 0.072 | - |
| head5m | 60 | 0.2 | 1000 | E1/measured600 | 4023.732 +/- 1.873 | -59.052 +/- 17.893 | -27.538 +/- 17.512 | 65.157 +/- 8.095 | 136.049 +/- 41.863 | 0.833 +/- 0.036 | - |
| head5m | 60 | 0.2 | 1000 | E1/model | 4023.732 +/- 1.873 | -54.631 +/- 21.654 | -35.751 +/- 10.913 | 65.289 +/- 13.731 | 127.755 +/- 45.496 | 0.854 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E1/oracleShape | 4023.732 +/- 1.873 | -63.449 +/- 18.396 | -33.513 +/- 18.724 | 71.756 +/- 7.447 | 149.447 +/- 27.516 | 0.812 +/- 0.062 | - |
| head5m | 60 | 0.2 | 1000 | E1/scale0.5 | 4023.732 +/- 1.873 | -54.631 +/- 21.654 | -35.751 +/- 10.913 | 65.289 +/- 13.731 | 127.755 +/- 45.496 | 0.854 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E1/scale0.75 | 4023.732 +/- 1.873 | -54.631 +/- 21.654 | -35.751 +/- 10.913 | 65.289 +/- 13.731 | 127.755 +/- 45.496 | 0.854 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E1/scale0.9 | 4023.732 +/- 1.873 | -54.631 +/- 21.654 | -35.751 +/- 10.913 | 65.289 +/- 13.731 | 127.755 +/- 45.496 | 0.854 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E1/scale1.1 | 4023.732 +/- 1.873 | -54.631 +/- 21.654 | -35.751 +/- 10.913 | 65.289 +/- 13.731 | 127.755 +/- 45.496 | 0.854 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E1/scale1.25 | 4023.732 +/- 1.873 | -54.631 +/- 21.654 | -35.751 +/- 10.913 | 65.289 +/- 13.731 | 127.755 +/- 45.496 | 0.854 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E1/scale1.5 | 4023.732 +/- 1.873 | -54.631 +/- 21.654 | -35.751 +/- 10.913 | 65.289 +/- 13.731 | 127.755 +/- 45.496 | 0.854 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E1/wrongFront | 4023.732 +/- 1.873 | -23.382 +/- 1.951 | 38.733 +/- 0.779 | 45.244 +/- 0.955 | 45.843 +/- 1.686 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E2/measured3600 | 4023.732 +/- 1.873 | -65.386 +/- 26.545 | -32.039 +/- 17.189 | 72.814 +/- 13.954 | 136.779 +/- 53.496 | 0.833 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E2/measured600 | 4023.732 +/- 1.873 | -68.148 +/- 17.925 | -32.039 +/- 17.189 | 75.304 +/- 9.772 | 140.655 +/- 41.441 | 0.812 +/- 0.062 | - |
| head5m | 60 | 0.2 | 1000 | E2/model | 4023.732 +/- 1.873 | -64.005 +/- 24.899 | -35.906 +/- 12.684 | 73.388 +/- 13.990 | 133.667 +/- 49.506 | 0.833 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E2/oracleShape | 4023.732 +/- 1.873 | -72.291 +/- 11.209 | -33.973 +/- 17.001 | 79.876 +/- 5.312 | 155.463 +/- 19.901 | 0.792 +/- 0.036 | - |
| head5m | 60 | 0.2 | 1000 | E2/scale0.5 | 4023.732 +/- 1.873 | -64.005 +/- 24.899 | -35.906 +/- 12.684 | 73.388 +/- 13.990 | 133.667 +/- 49.506 | 0.833 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E2/scale0.75 | 4023.732 +/- 1.873 | -64.005 +/- 24.899 | -35.906 +/- 12.684 | 73.388 +/- 13.990 | 133.667 +/- 49.506 | 0.833 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E2/scale0.9 | 4023.732 +/- 1.873 | -64.005 +/- 24.899 | -35.906 +/- 12.684 | 73.388 +/- 13.990 | 133.667 +/- 49.506 | 0.833 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E2/scale1.1 | 4023.732 +/- 1.873 | -64.005 +/- 24.899 | -35.906 +/- 12.684 | 73.388 +/- 13.990 | 133.667 +/- 49.506 | 0.833 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E2/scale1.25 | 4023.732 +/- 1.873 | -64.005 +/- 24.899 | -35.906 +/- 12.684 | 73.388 +/- 13.990 | 133.667 +/- 49.506 | 0.833 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E2/scale1.5 | 4023.732 +/- 1.873 | -64.005 +/- 24.899 | -35.906 +/- 12.684 | 73.388 +/- 13.990 | 133.667 +/- 49.506 | 0.833 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E2/wrongFront | 4023.732 +/- 1.873 | -29.204 +/- 1.435 | 39.773 +/- 0.000 | 49.343 +/- 0.490 | 49.774 +/- 0.870 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E3/model | 4023.732 +/- 1.873 | -40.252 +/- 20.332 | -12.316 +/- 3.707 | 42.094 +/- 10.603 | 107.535 +/- 69.871 | 0.958 +/- 0.036 | 3706.297 +/- 13.871 |
| head5m | 60 | 0.2 | 1000 | E4/measured3600 | 4023.732 +/- 1.873 | 10.331 +/- 0.610 | -0.147 +/- 0.183 | 10.333 +/- 0.353 | 10.785 +/- 0.657 | 1.000 +/- 0.000 | 3502.310 +/- 16.928 |
| head5m | 60 | 0.2 | 1000 | E4/measured600 | 4023.732 +/- 1.873 | 10.840 +/- 1.144 | -0.069 +/- 0.439 | 10.840 +/- 0.660 | 11.293 +/- 1.140 | 1.000 +/- 0.000 | 3389.727 +/- 42.717 |
| head5m | 60 | 0.2 | 1000 | E4/model | 4023.732 +/- 1.873 | 10.467 +/- 0.834 | -0.129 +/- 0.076 | 10.468 +/- 0.481 | 10.942 +/- 0.888 | 1.000 +/- 0.000 | 3513.239 +/- 14.430 |
| head5m | 60 | 0.2 | 1000 | E4/oracleShape | 4023.732 +/- 1.873 | 10.216 +/- 0.706 | -0.112 +/- 0.074 | 10.216 +/- 0.407 | 10.687 +/- 0.757 | 1.000 +/- 0.000 | 3520.786 +/- 22.962 |
| head5m | 60 | 0.2 | 1000 | E4/scale0.5 | 4023.732 +/- 1.873 | 10.467 +/- 0.834 | -0.129 +/- 0.076 | 10.468 +/- 0.481 | 10.942 +/- 0.888 | 1.000 +/- 0.000 | 3513.239 +/- 14.430 |
| head5m | 60 | 0.2 | 1000 | E4/scale0.75 | 4023.732 +/- 1.873 | 10.467 +/- 0.834 | -0.129 +/- 0.076 | 10.468 +/- 0.481 | 10.942 +/- 0.888 | 1.000 +/- 0.000 | 3513.239 +/- 14.430 |
| head5m | 60 | 0.2 | 1000 | E4/scale0.9 | 4023.732 +/- 1.873 | 10.467 +/- 0.834 | -0.129 +/- 0.076 | 10.468 +/- 0.481 | 10.942 +/- 0.888 | 1.000 +/- 0.000 | 3513.239 +/- 14.430 |
| head5m | 60 | 0.2 | 1000 | E4/scale1.1 | 4023.732 +/- 1.873 | 10.467 +/- 0.834 | -0.129 +/- 0.076 | 10.468 +/- 0.481 | 10.942 +/- 0.888 | 1.000 +/- 0.000 | 3513.239 +/- 14.430 |
| head5m | 60 | 0.2 | 1000 | E4/scale1.25 | 4023.732 +/- 1.873 | 10.467 +/- 0.834 | -0.129 +/- 0.076 | 10.468 +/- 0.481 | 10.942 +/- 0.888 | 1.000 +/- 0.000 | 3513.239 +/- 14.430 |
| head5m | 60 | 0.2 | 1000 | E4/scale1.5 | 4023.732 +/- 1.873 | 10.467 +/- 0.834 | -0.129 +/- 0.076 | 10.468 +/- 0.481 | 10.942 +/- 0.888 | 1.000 +/- 0.000 | 3513.239 +/- 14.430 |
| head5m | 60 | 0.2 | 1000 | E4/wrongFront | 4023.732 +/- 1.873 | -12.511 +/- 19.665 | 9.663 +/- 0.445 | 15.808 +/- 9.137 | 97.935 +/- 72.595 | 0.958 +/- 0.036 | 0.146 +/- 0.018 |
| head5m | 60 | 0.2 | 1000 | E5/measured3600 | 4023.732 +/- 1.873 | 10.220 +/- 0.624 | -0.644 +/- 0.210 | 10.241 +/- 0.367 | 10.652 +/- 0.653 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E5/measured600 | 4023.732 +/- 1.873 | 10.792 +/- 1.084 | -0.737 +/- 0.546 | 10.817 +/- 0.617 | 11.211 +/- 1.053 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E5/model | 4023.732 +/- 1.873 | 10.399 +/- 0.811 | -0.643 +/- 0.047 | 10.419 +/- 0.466 | 10.853 +/- 0.857 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E5/oracleShape | 4023.732 +/- 1.873 | 10.129 +/- 0.672 | -0.607 +/- 0.044 | 10.148 +/- 0.386 | 10.580 +/- 0.705 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E5/scale0.5 | 4023.732 +/- 1.873 | 10.563 +/- 0.972 | 3.464 +/- 0.363 | 11.117 +/- 0.592 | 11.702 +/- 1.121 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E5/scale0.75 | 4023.732 +/- 1.873 | 10.579 +/- 0.891 | 0.860 +/- 0.155 | 10.614 +/- 0.520 | 11.170 +/- 0.980 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E5/scale0.9 | 4023.732 +/- 1.873 | 10.465 +/- 0.840 | -0.160 +/- 0.076 | 10.466 +/- 0.484 | 10.947 +/- 0.899 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E5/scale1.1 | 4023.732 +/- 1.873 | 10.342 +/- 0.784 | -0.980 +/- 0.051 | 10.388 +/- 0.451 | 10.780 +/- 0.822 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E5/scale1.25 | 4023.732 +/- 1.873 | 10.274 +/- 0.750 | -1.301 +/- 0.057 | 10.356 +/- 0.433 | 10.696 +/- 0.779 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E5/scale1.5 | 4023.732 +/- 1.873 | 10.220 +/- 0.722 | -1.461 +/- 0.073 | 10.324 +/- 0.419 | 10.638 +/- 0.748 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E5/wrongFront | 4023.732 +/- 1.873 | -57.982 +/- 13.991 | 69.377 +/- 6.561 | 90.416 +/- 7.326 | 99.393 +/- 7.326 | 0.250 +/- 0.165 | - |
| head5m | 60 | 0.2 | 1000 | E6/measured3600 | 4023.732 +/- 1.873 | -40.252 +/- 20.332 | -17.157 +/- 4.227 | 43.756 +/- 9.941 | 107.866 +/- 69.434 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.2 | 1000 | E6/measured600 | 4023.732 +/- 1.873 | -42.620 +/- 22.663 | -11.118 +/- 9.333 | 44.047 +/- 11.308 | 112.534 +/- 73.803 | 0.938 +/- 0.062 | - |
| head5m | 60 | 0.2 | 1000 | E6/model | 4023.732 +/- 1.873 | -40.252 +/- 20.332 | -18.296 +/- 3.021 | 44.215 +/- 10.009 | 108.080 +/- 69.610 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.2 | 1000 | E6/oracleShape | 4023.732 +/- 1.873 | -40.252 +/- 20.332 | -18.285 +/- 3.632 | 44.210 +/- 9.832 | 108.290 +/- 69.099 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.2 | 1000 | E6/scale0.5 | 4023.732 +/- 1.873 | -16.775 +/- 0.000 | 34.726 +/- 0.137 | 38.566 +/- 0.071 | 38.573 +/- 0.125 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E6/scale0.75 | 4023.732 +/- 1.873 | -40.252 +/- 20.332 | 14.932 +/- 3.788 | 42.932 +/- 10.658 | 110.799 +/- 66.052 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.2 | 1000 | E6/scale0.9 | 4023.732 +/- 1.873 | -40.252 +/- 20.332 | -9.245 +/- 2.466 | 41.300 +/- 11.739 | 108.388 +/- 68.535 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.2 | 1000 | E6/scale1.1 | 4023.732 +/- 1.873 | -40.252 +/- 20.332 | -31.000 +/- 1.458 | 50.806 +/- 9.753 | 111.182 +/- 66.341 | 0.958 +/- 0.036 | - |
| head5m | 60 | 0.2 | 1000 | E6/scale1.25 | 4023.732 +/- 1.873 | -58.930 +/- 25.623 | -31.789 +/- 12.573 | 66.958 +/- 14.575 | 132.197 +/- 50.645 | 0.833 +/- 0.095 | - |
| head5m | 60 | 0.2 | 1000 | E6/scale1.5 | 4023.732 +/- 1.873 | -218.716 +/- 6.950 | -57.779 +/- 5.254 | 226.219 +/- 3.774 | 299.629 +/- 14.630 | 0.021 +/- 0.036 | - |
| head5m | 60 | 0.2 | 1000 | E6/wrongFront | 4023.732 +/- 1.873 | -18.040 +/- 1.096 | 37.205 +/- 0.292 | 41.348 +/- 0.423 | 41.569 +/- 0.907 | 1.000 +/- 0.000 | - |
| head5m | 60 | 0.2 | 1000 | E7/model | 4023.732 +/- 1.873 | 7.948 +/- 24.180 | -1.085 +/- 3.822 | 8.021 +/- 13.551 | 113.624 +/- 63.803 | 0.958 +/- 0.036 | 3706.297 +/- 13.871 |
| lab | 60 | 0 | 1000 | E0 | 0.000 +/- 0.000 | 0.629 +/- 0.013 | -0.194 +/- 0.000 | 0.658 +/- 0.007 | 0.660 +/- 0.011 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 1000 | E1/model | 0.000 +/- 0.000 | 0.387 +/- 0.011 | -0.053 +/- 0.052 | 0.391 +/- 0.010 | 0.437 +/- 0.008 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 1000 | E2/model | 0.000 +/- 0.000 | 0.483 +/- 0.049 | -0.081 +/- 0.122 | 0.489 +/- 0.039 | 0.638 +/- 0.028 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 1000 | E3/model | 0.000 +/- 0.000 | 0.618 +/- 0.014 | -0.194 +/- 0.000 | 0.648 +/- 0.008 | 0.650 +/- 0.013 | 1.000 +/- 0.000 | 55.121 +/- 4.306 |
| lab | 60 | 0 | 1000 | E4/model | 0.000 +/- 0.000 | 0.303 +/- 0.040 | -0.093 +/- 0.092 | 0.317 +/- 0.032 | 0.390 +/- 0.040 | 1.000 +/- 0.000 | 0.863 +/- 0.210 |
| lab | 60 | 0 | 1000 | E5/model | 0.000 +/- 0.000 | 0.314 +/- 0.039 | -0.092 +/- 0.092 | 0.327 +/- 0.031 | 0.398 +/- 0.039 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 1000 | E6/model | 0.000 +/- 0.000 | 0.629 +/- 0.013 | -0.194 +/- 0.000 | 0.658 +/- 0.007 | 0.660 +/- 0.011 | 1.000 +/- 0.000 | - |
| lab | 60 | 0 | 1000 | E7/model | 0.000 +/- 0.000 | 0.371 +/- 0.018 | 0.069 +/- 0.051 | 0.377 +/- 0.010 | 0.395 +/- 0.021 | 1.000 +/- 0.000 | 55.121 +/- 4.306 |
| lab | 60 | 0.1 | 1000 | E0 | 976.792 +/- 0.772 | -3.334 +/- 1.326 | -5.464 +/- 0.963 | 6.401 +/- 0.874 | 8.196 +/- 1.099 | 0.354 +/- 0.130 | - |
| lab | 60 | 0.1 | 1000 | E1/measured3600 | 976.792 +/- 0.772 | 0.033 +/- 0.613 | -0.043 +/- 0.024 | 0.054 +/- 0.204 | 1.703 +/- 2.189 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.1 | 1000 | E1/measured600 | 976.792 +/- 0.772 | 0.003 +/- 0.636 | 0.009 +/- 0.047 | 0.010 +/- 0.093 | 1.717 +/- 2.228 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.1 | 1000 | E1/model | 976.792 +/- 0.772 | 0.047 +/- 0.614 | -0.052 +/- 0.039 | 0.070 +/- 0.249 | 1.713 +/- 2.181 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.1 | 1000 | E1/oracleShape | 976.792 +/- 0.772 | 0.044 +/- 0.610 | -0.044 +/- 0.026 | 0.062 +/- 0.258 | 1.709 +/- 2.184 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.1 | 1000 | E1/scale0.5 | 976.792 +/- 0.772 | 0.047 +/- 0.614 | -0.052 +/- 0.039 | 0.070 +/- 0.249 | 1.713 +/- 2.181 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.1 | 1000 | E1/scale0.75 | 976.792 +/- 0.772 | 0.047 +/- 0.614 | -0.052 +/- 0.039 | 0.070 +/- 0.249 | 1.713 +/- 2.181 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.1 | 1000 | E1/scale0.9 | 976.792 +/- 0.772 | 0.047 +/- 0.614 | -0.052 +/- 0.039 | 0.070 +/- 0.249 | 1.713 +/- 2.181 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.1 | 1000 | E1/scale1.1 | 976.792 +/- 0.772 | 0.047 +/- 0.614 | -0.052 +/- 0.039 | 0.070 +/- 0.249 | 1.713 +/- 2.181 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.1 | 1000 | E1/scale1.25 | 976.792 +/- 0.772 | 0.047 +/- 0.614 | -0.052 +/- 0.039 | 0.070 +/- 0.249 | 1.713 +/- 2.181 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.1 | 1000 | E1/scale1.5 | 976.792 +/- 0.772 | 0.047 +/- 0.614 | -0.052 +/- 0.039 | 0.070 +/- 0.249 | 1.713 +/- 2.181 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.1 | 1000 | E1/wrongFront | 976.792 +/- 0.772 | -1.491 +/- 1.444 | -1.648 +/- 1.202 | 2.222 +/- 1.070 | 4.827 +/- 2.403 | 0.812 +/- 0.165 | - |
| lab | 60 | 0.1 | 1000 | E2/measured3600 | 976.792 +/- 0.772 | -0.635 +/- 1.070 | -0.105 +/- 0.061 | 0.644 +/- 0.613 | 3.642 +/- 2.756 | 0.938 +/- 0.062 | - |
| lab | 60 | 0.1 | 1000 | E2/measured600 | 976.792 +/- 0.772 | -0.295 +/- 1.267 | 0.000 +/- 0.084 | 0.295 +/- 0.731 | 2.417 +/- 3.121 | 0.958 +/- 0.072 | - |
| lab | 60 | 0.1 | 1000 | E2/model | 976.792 +/- 0.772 | -0.603 +/- 0.037 | -0.113 +/- 0.074 | 0.614 +/- 0.029 | 4.282 +/- 0.003 | 0.938 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E2/oracleShape | 976.792 +/- 0.772 | -0.603 +/- 0.037 | -0.097 +/- 0.049 | 0.611 +/- 0.026 | 4.282 +/- 0.003 | 0.938 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E2/scale0.5 | 976.792 +/- 0.772 | -0.603 +/- 0.037 | -0.113 +/- 0.074 | 0.614 +/- 0.029 | 4.282 +/- 0.003 | 0.938 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E2/scale0.75 | 976.792 +/- 0.772 | -0.603 +/- 0.037 | -0.113 +/- 0.074 | 0.614 +/- 0.029 | 4.282 +/- 0.003 | 0.938 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E2/scale0.9 | 976.792 +/- 0.772 | -0.603 +/- 0.037 | -0.113 +/- 0.074 | 0.614 +/- 0.029 | 4.282 +/- 0.003 | 0.938 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E2/scale1.1 | 976.792 +/- 0.772 | -0.603 +/- 0.037 | -0.113 +/- 0.074 | 0.614 +/- 0.029 | 4.282 +/- 0.003 | 0.938 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E2/scale1.25 | 976.792 +/- 0.772 | -0.603 +/- 0.037 | -0.113 +/- 0.074 | 0.614 +/- 0.029 | 4.282 +/- 0.003 | 0.938 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E2/scale1.5 | 976.792 +/- 0.772 | -0.603 +/- 0.037 | -0.113 +/- 0.074 | 0.614 +/- 0.029 | 4.282 +/- 0.003 | 0.938 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E2/wrongFront | 976.792 +/- 0.772 | -1.859 +/- 1.916 | -1.815 +/- 1.424 | 2.598 +/- 1.364 | 5.196 +/- 2.940 | 0.792 +/- 0.201 | - |
| lab | 60 | 0.1 | 1000 | E3/model | 976.792 +/- 0.772 | 0.614 +/- 0.027 | -0.194 +/- 0.000 | 0.645 +/- 0.015 | 0.649 +/- 0.026 | 1.000 +/- 0.000 | 968.402 +/- 15.682 |
| lab | 60 | 0.1 | 1000 | E4/measured3600 | 976.792 +/- 0.772 | 0.324 +/- 0.024 | -0.125 +/- 0.050 | 0.348 +/- 0.004 | 0.404 +/- 0.024 | 1.000 +/- 0.000 | 766.804 +/- 8.614 |
| lab | 60 | 0.1 | 1000 | E4/measured600 | 976.792 +/- 0.772 | 0.313 +/- 0.041 | -0.091 +/- 0.074 | 0.326 +/- 0.020 | 0.392 +/- 0.038 | 1.000 +/- 0.000 | 742.819 +/- 10.118 |
| lab | 60 | 0.1 | 1000 | E4/model | 976.792 +/- 0.772 | 0.323 +/- 0.025 | -0.134 +/- 0.056 | 0.350 +/- 0.003 | 0.406 +/- 0.023 | 1.000 +/- 0.000 | 765.896 +/- 6.765 |
| lab | 60 | 0.1 | 1000 | E4/oracleShape | 976.792 +/- 0.772 | 0.322 +/- 0.025 | -0.135 +/- 0.058 | 0.349 +/- 0.005 | 0.406 +/- 0.023 | 1.000 +/- 0.000 | 768.355 +/- 6.657 |
| lab | 60 | 0.1 | 1000 | E4/scale0.5 | 976.792 +/- 0.772 | 0.323 +/- 0.025 | -0.134 +/- 0.056 | 0.350 +/- 0.003 | 0.406 +/- 0.023 | 1.000 +/- 0.000 | 765.896 +/- 6.765 |
| lab | 60 | 0.1 | 1000 | E4/scale0.75 | 976.792 +/- 0.772 | 0.323 +/- 0.025 | -0.134 +/- 0.056 | 0.350 +/- 0.003 | 0.406 +/- 0.023 | 1.000 +/- 0.000 | 765.896 +/- 6.765 |
| lab | 60 | 0.1 | 1000 | E4/scale0.9 | 976.792 +/- 0.772 | 0.323 +/- 0.025 | -0.134 +/- 0.056 | 0.350 +/- 0.003 | 0.406 +/- 0.023 | 1.000 +/- 0.000 | 765.896 +/- 6.765 |
| lab | 60 | 0.1 | 1000 | E4/scale1.1 | 976.792 +/- 0.772 | 0.323 +/- 0.025 | -0.134 +/- 0.056 | 0.350 +/- 0.003 | 0.406 +/- 0.023 | 1.000 +/- 0.000 | 765.896 +/- 6.765 |
| lab | 60 | 0.1 | 1000 | E4/scale1.25 | 976.792 +/- 0.772 | 0.323 +/- 0.025 | -0.134 +/- 0.056 | 0.350 +/- 0.003 | 0.406 +/- 0.023 | 1.000 +/- 0.000 | 765.896 +/- 6.765 |
| lab | 60 | 0.1 | 1000 | E4/scale1.5 | 976.792 +/- 0.772 | 0.323 +/- 0.025 | -0.134 +/- 0.056 | 0.350 +/- 0.003 | 0.406 +/- 0.023 | 1.000 +/- 0.000 | 765.896 +/- 6.765 |
| lab | 60 | 0.1 | 1000 | E4/wrongFront | 976.792 +/- 0.772 | 0.304 +/- 0.020 | -0.123 +/- 0.036 | 0.328 +/- 0.004 | 0.366 +/- 0.016 | 1.000 +/- 0.000 | 4.999 +/- 1.254 |
| lab | 60 | 0.1 | 1000 | E5/measured3600 | 976.792 +/- 0.772 | 0.330 +/- 0.027 | -0.128 +/- 0.052 | 0.354 +/- 0.005 | 0.414 +/- 0.026 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E5/measured600 | 976.792 +/- 0.772 | 0.314 +/- 0.046 | -0.077 +/- 0.098 | 0.324 +/- 0.026 | 0.400 +/- 0.042 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E5/model | 976.792 +/- 0.772 | 0.329 +/- 0.026 | -0.139 +/- 0.059 | 0.357 +/- 0.005 | 0.417 +/- 0.024 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E5/oracleShape | 976.792 +/- 0.772 | 0.328 +/- 0.026 | -0.140 +/- 0.062 | 0.357 +/- 0.007 | 0.417 +/- 0.024 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E5/scale0.5 | 976.792 +/- 0.772 | 0.325 +/- 0.019 | -0.128 +/- 0.049 | 0.349 +/- 0.000 | 0.398 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E5/scale0.75 | 976.792 +/- 0.772 | 0.328 +/- 0.023 | -0.133 +/- 0.056 | 0.354 +/- 0.003 | 0.409 +/- 0.021 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E5/scale0.9 | 976.792 +/- 0.772 | 0.328 +/- 0.026 | -0.136 +/- 0.059 | 0.355 +/- 0.004 | 0.413 +/- 0.023 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E5/scale1.1 | 976.792 +/- 0.772 | 0.332 +/- 0.027 | -0.143 +/- 0.057 | 0.361 +/- 0.006 | 0.422 +/- 0.025 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E5/scale1.25 | 976.792 +/- 0.772 | 0.337 +/- 0.027 | -0.152 +/- 0.052 | 0.369 +/- 0.007 | 0.432 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E5/scale1.5 | 976.792 +/- 0.772 | 0.352 +/- 0.023 | -0.170 +/- 0.038 | 0.391 +/- 0.008 | 0.452 +/- 0.026 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E5/wrongFront | 976.792 +/- 0.772 | 0.271 +/- 0.023 | -0.115 +/- 0.042 | 0.294 +/- 0.006 | 0.336 +/- 0.017 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/measured3600 | 976.792 +/- 0.772 | 0.608 +/- 0.024 | -0.194 +/- 0.000 | 0.638 +/- 0.013 | 0.643 +/- 0.023 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/measured600 | 976.792 +/- 0.772 | 0.603 +/- 0.041 | -0.194 +/- 0.000 | 0.634 +/- 0.023 | 0.639 +/- 0.040 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/model | 976.792 +/- 0.772 | 0.613 +/- 0.030 | -0.194 +/- 0.000 | 0.643 +/- 0.017 | 0.648 +/- 0.029 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/oracleShape | 976.792 +/- 0.772 | 0.612 +/- 0.028 | -0.194 +/- 0.000 | 0.642 +/- 0.016 | 0.647 +/- 0.027 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/scale0.5 | 976.792 +/- 0.772 | 0.679 +/- 0.019 | -0.194 +/- 0.000 | 0.707 +/- 0.011 | 0.709 +/- 0.019 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/scale0.75 | 976.792 +/- 0.772 | 0.651 +/- 0.025 | -0.194 +/- 0.000 | 0.679 +/- 0.014 | 0.682 +/- 0.024 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/scale0.9 | 976.792 +/- 0.772 | 0.629 +/- 0.029 | -0.194 +/- 0.000 | 0.659 +/- 0.016 | 0.663 +/- 0.028 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/scale1.1 | 976.792 +/- 0.772 | 0.596 +/- 0.032 | -0.194 +/- 0.000 | 0.627 +/- 0.017 | 0.633 +/- 0.031 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/scale1.25 | 976.792 +/- 0.772 | 0.569 +/- 0.034 | -0.194 +/- 0.000 | 0.601 +/- 0.018 | 0.608 +/- 0.033 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/scale1.5 | 976.792 +/- 0.772 | 0.519 +/- 0.033 | -0.194 +/- 0.000 | 0.554 +/- 0.018 | 0.562 +/- 0.032 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.1 | 1000 | E6/wrongFront | 976.792 +/- 0.772 | -2.603 +/- 1.411 | -4.622 +/- 1.584 | 5.305 +/- 1.197 | 7.344 +/- 1.728 | 0.479 +/- 0.191 | - |
| lab | 60 | 0.1 | 1000 | E7/model | 976.792 +/- 0.772 | 0.400 +/- 0.020 | 0.044 +/- 0.023 | 0.403 +/- 0.013 | 0.436 +/- 0.022 | 1.000 +/- 0.000 | 968.402 +/- 15.682 |
| lab | 60 | 0.2 | 1000 | E0 | 1953.584 +/- 1.544 | -5.926 +/- 0.116 | -8.265 +/- 0.263 | 10.170 +/- 0.162 | 10.358 +/- 0.284 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E1/measured3600 | 1953.584 +/- 1.544 | -0.342 +/- 0.621 | -0.013 +/- 0.043 | 0.342 +/- 0.358 | 2.991 +/- 2.216 | 0.958 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E1/measured600 | 1953.584 +/- 1.544 | -0.016 +/- 0.651 | 0.064 +/- 0.068 | 0.066 +/- 0.121 | 1.705 +/- 2.212 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E1/model | 1953.584 +/- 1.544 | 0.029 +/- 0.619 | -0.032 +/- 0.035 | 0.043 +/- 0.253 | 1.717 +/- 2.192 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E1/oracleShape | 1953.584 +/- 1.544 | 0.027 +/- 0.614 | -0.031 +/- 0.036 | 0.042 +/- 0.247 | 1.716 +/- 2.196 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E1/scale0.5 | 1953.584 +/- 1.544 | 0.029 +/- 0.619 | -0.032 +/- 0.035 | 0.043 +/- 0.253 | 1.717 +/- 2.192 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E1/scale0.75 | 1953.584 +/- 1.544 | 0.029 +/- 0.619 | -0.032 +/- 0.035 | 0.043 +/- 0.253 | 1.717 +/- 2.192 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E1/scale0.9 | 1953.584 +/- 1.544 | 0.029 +/- 0.619 | -0.032 +/- 0.035 | 0.043 +/- 0.253 | 1.717 +/- 2.192 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E1/scale1.1 | 1953.584 +/- 1.544 | 0.029 +/- 0.619 | -0.032 +/- 0.035 | 0.043 +/- 0.253 | 1.717 +/- 2.192 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E1/scale1.25 | 1953.584 +/- 1.544 | 0.029 +/- 0.619 | -0.032 +/- 0.035 | 0.043 +/- 0.253 | 1.717 +/- 2.192 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E1/scale1.5 | 1953.584 +/- 1.544 | 0.029 +/- 0.619 | -0.032 +/- 0.035 | 0.043 +/- 0.253 | 1.717 +/- 2.192 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E1/wrongFront | 1953.584 +/- 1.544 | -8.517 +/- 0.822 | -8.563 +/- 0.075 | 12.077 +/- 0.364 | 12.319 +/- 0.640 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E2/measured3600 | 1953.584 +/- 1.544 | -0.668 +/- 1.046 | -0.049 +/- 0.084 | 0.670 +/- 0.602 | 3.622 +/- 2.786 | 0.938 +/- 0.062 | - |
| lab | 60 | 0.2 | 1000 | E2/measured600 | 1953.584 +/- 1.544 | -0.012 +/- 0.694 | 0.097 +/- 0.084 | 0.098 +/- 0.086 | 1.816 +/- 2.130 | 0.979 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E2/model | 1953.584 +/- 1.544 | -0.295 +/- 0.569 | -0.065 +/- 0.056 | 0.302 +/- 0.318 | 3.042 +/- 2.145 | 0.958 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E2/oracleShape | 1953.584 +/- 1.544 | -0.660 +/- 1.059 | -0.081 +/- 0.074 | 0.665 +/- 0.609 | 3.623 +/- 2.787 | 0.938 +/- 0.062 | - |
| lab | 60 | 0.2 | 1000 | E2/scale0.5 | 1953.584 +/- 1.544 | -0.295 +/- 0.569 | -0.065 +/- 0.056 | 0.302 +/- 0.318 | 3.042 +/- 2.145 | 0.958 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E2/scale0.75 | 1953.584 +/- 1.544 | -0.295 +/- 0.569 | -0.065 +/- 0.056 | 0.302 +/- 0.318 | 3.042 +/- 2.145 | 0.958 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E2/scale0.9 | 1953.584 +/- 1.544 | -0.295 +/- 0.569 | -0.065 +/- 0.056 | 0.302 +/- 0.318 | 3.042 +/- 2.145 | 0.958 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E2/scale1.1 | 1953.584 +/- 1.544 | -0.295 +/- 0.569 | -0.065 +/- 0.056 | 0.302 +/- 0.318 | 3.042 +/- 2.145 | 0.958 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E2/scale1.25 | 1953.584 +/- 1.544 | -0.295 +/- 0.569 | -0.065 +/- 0.056 | 0.302 +/- 0.318 | 3.042 +/- 2.145 | 0.958 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E2/scale1.5 | 1953.584 +/- 1.544 | -0.295 +/- 0.569 | -0.065 +/- 0.056 | 0.302 +/- 0.318 | 3.042 +/- 2.145 | 0.958 +/- 0.036 | - |
| lab | 60 | 0.2 | 1000 | E2/wrongFront | 1953.584 +/- 1.544 | -8.624 +/- 0.832 | -8.515 +/- 0.061 | 12.119 +/- 0.367 | 12.364 +/- 0.639 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E3/model | 1953.584 +/- 1.544 | 0.596 +/- 0.023 | -0.194 +/- 0.000 | 0.627 +/- 0.013 | 0.632 +/- 0.021 | 1.000 +/- 0.000 | 1935.574 +/- 7.694 |
| lab | 60 | 0.2 | 1000 | E4/measured3600 | 1953.584 +/- 1.544 | 0.305 +/- 0.023 | -0.113 +/- 0.058 | 0.325 +/- 0.024 | 0.391 +/- 0.043 | 1.000 +/- 0.000 | 1692.031 +/- 7.680 |
| lab | 60 | 0.2 | 1000 | E4/measured600 | 1953.584 +/- 1.544 | 0.279 +/- 0.054 | -0.072 +/- 0.096 | 0.288 +/- 0.042 | 0.366 +/- 0.065 | 1.000 +/- 0.000 | 1626.503 +/- 14.100 |
| lab | 60 | 0.2 | 1000 | E4/model | 1953.584 +/- 1.544 | 0.305 +/- 0.027 | -0.129 +/- 0.052 | 0.331 +/- 0.026 | 0.395 +/- 0.045 | 1.000 +/- 0.000 | 1690.982 +/- 5.602 |
| lab | 60 | 0.2 | 1000 | E4/oracleShape | 1953.584 +/- 1.544 | 0.304 +/- 0.023 | -0.131 +/- 0.045 | 0.331 +/- 0.022 | 0.394 +/- 0.041 | 1.000 +/- 0.000 | 1695.679 +/- 3.228 |
| lab | 60 | 0.2 | 1000 | E4/scale0.5 | 1953.584 +/- 1.544 | 0.305 +/- 0.027 | -0.129 +/- 0.052 | 0.331 +/- 0.026 | 0.395 +/- 0.045 | 1.000 +/- 0.000 | 1690.982 +/- 5.602 |
| lab | 60 | 0.2 | 1000 | E4/scale0.75 | 1953.584 +/- 1.544 | 0.305 +/- 0.027 | -0.129 +/- 0.052 | 0.331 +/- 0.026 | 0.395 +/- 0.045 | 1.000 +/- 0.000 | 1690.982 +/- 5.602 |
| lab | 60 | 0.2 | 1000 | E4/scale0.9 | 1953.584 +/- 1.544 | 0.305 +/- 0.027 | -0.129 +/- 0.052 | 0.331 +/- 0.026 | 0.395 +/- 0.045 | 1.000 +/- 0.000 | 1690.982 +/- 5.602 |
| lab | 60 | 0.2 | 1000 | E4/scale1.1 | 1953.584 +/- 1.544 | 0.305 +/- 0.027 | -0.129 +/- 0.052 | 0.331 +/- 0.026 | 0.395 +/- 0.045 | 1.000 +/- 0.000 | 1690.982 +/- 5.602 |
| lab | 60 | 0.2 | 1000 | E4/scale1.25 | 1953.584 +/- 1.544 | 0.305 +/- 0.027 | -0.129 +/- 0.052 | 0.331 +/- 0.026 | 0.395 +/- 0.045 | 1.000 +/- 0.000 | 1690.982 +/- 5.602 |
| lab | 60 | 0.2 | 1000 | E4/scale1.5 | 1953.584 +/- 1.544 | 0.305 +/- 0.027 | -0.129 +/- 0.052 | 0.331 +/- 0.026 | 0.395 +/- 0.045 | 1.000 +/- 0.000 | 1690.982 +/- 5.602 |
| lab | 60 | 0.2 | 1000 | E4/wrongFront | 1953.584 +/- 1.544 | 0.239 +/- 0.016 | -0.127 +/- 0.033 | 0.271 +/- 0.016 | 0.307 +/- 0.035 | 1.000 +/- 0.000 | 2.202 +/- 0.554 |
| lab | 60 | 0.2 | 1000 | E5/measured3600 | 1953.584 +/- 1.544 | 0.313 +/- 0.027 | -0.114 +/- 0.067 | 0.333 +/- 0.028 | 0.404 +/- 0.048 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E5/measured600 | 1953.584 +/- 1.544 | 0.284 +/- 0.075 | -0.057 +/- 0.126 | 0.289 +/- 0.054 | 0.378 +/- 0.081 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E5/model | 1953.584 +/- 1.544 | 0.312 +/- 0.032 | -0.135 +/- 0.057 | 0.340 +/- 0.029 | 0.408 +/- 0.051 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E5/oracleShape | 1953.584 +/- 1.544 | 0.309 +/- 0.029 | -0.146 +/- 0.041 | 0.342 +/- 0.025 | 0.405 +/- 0.048 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E5/scale0.5 | 1953.584 +/- 1.544 | 0.291 +/- 0.018 | -0.127 +/- 0.043 | 0.318 +/- 0.019 | 0.369 +/- 0.036 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E5/scale0.75 | 1953.584 +/- 1.544 | 0.304 +/- 0.024 | -0.127 +/- 0.049 | 0.330 +/- 0.023 | 0.390 +/- 0.041 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E5/scale0.9 | 1953.584 +/- 1.544 | 0.309 +/- 0.030 | -0.131 +/- 0.055 | 0.336 +/- 0.028 | 0.400 +/- 0.048 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E5/scale1.1 | 1953.584 +/- 1.544 | 0.318 +/- 0.034 | -0.137 +/- 0.059 | 0.347 +/- 0.031 | 0.417 +/- 0.054 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E5/scale1.25 | 1953.584 +/- 1.544 | 0.328 +/- 0.042 | -0.150 +/- 0.051 | 0.361 +/- 0.033 | 0.431 +/- 0.060 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E5/scale1.5 | 1953.584 +/- 1.544 | 0.377 +/- 0.057 | -0.153 +/- 0.053 | 0.406 +/- 0.042 | 0.480 +/- 0.077 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E5/wrongFront | 1953.584 +/- 1.544 | -1.807 +/- 2.162 | -2.001 +/- 2.092 | 2.696 +/- 1.733 | 4.733 +/- 4.201 | 0.771 +/- 0.253 | - |
| lab | 60 | 0.2 | 1000 | E6/measured3600 | 1953.584 +/- 1.544 | 0.583 +/- 0.036 | -0.194 +/- 0.000 | 0.615 +/- 0.020 | 0.620 +/- 0.034 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E6/measured600 | 1953.584 +/- 1.544 | 0.574 +/- 0.021 | -0.194 +/- 0.000 | 0.606 +/- 0.012 | 0.611 +/- 0.019 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E6/model | 1953.584 +/- 1.544 | 0.594 +/- 0.024 | -0.194 +/- 0.000 | 0.625 +/- 0.013 | 0.630 +/- 0.021 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E6/oracleShape | 1953.584 +/- 1.544 | 0.592 +/- 0.029 | -0.194 +/- 0.000 | 0.624 +/- 0.016 | 0.628 +/- 0.026 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E6/scale0.5 | 1953.584 +/- 1.544 | -4.714 +/- 1.340 | -6.060 +/- 1.363 | 7.677 +/- 1.083 | 9.490 +/- 1.046 | 0.271 +/- 0.201 | - |
| lab | 60 | 0.2 | 1000 | E6/scale0.75 | 1953.584 +/- 1.544 | 0.077 +/- 0.512 | -0.708 +/- 0.482 | 0.712 +/- 0.249 | 2.831 +/- 1.879 | 0.938 +/- 0.062 | - |
| lab | 60 | 0.2 | 1000 | E6/scale0.9 | 1953.584 +/- 1.544 | 0.627 +/- 0.023 | -0.194 +/- 0.000 | 0.656 +/- 0.013 | 0.659 +/- 0.021 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E6/scale1.1 | 1953.584 +/- 1.544 | 0.558 +/- 0.022 | -0.194 +/- 0.000 | 0.591 +/- 0.012 | 0.597 +/- 0.020 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E6/scale1.25 | 1953.584 +/- 1.544 | 0.499 +/- 0.020 | -0.194 +/- 0.000 | 0.535 +/- 0.011 | 0.542 +/- 0.019 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E6/scale1.5 | 1953.584 +/- 1.544 | 0.401 +/- 0.008 | -0.016 +/- 0.003 | 0.401 +/- 0.005 | 0.456 +/- 0.011 | 1.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E6/wrongFront | 1953.584 +/- 1.544 | -7.289 +/- 1.203 | -8.569 +/- 0.045 | 11.250 +/- 0.446 | 11.575 +/- 0.827 | 0.000 +/- 0.000 | - |
| lab | 60 | 0.2 | 1000 | E7/model | 1953.584 +/- 1.544 | 0.382 +/- 0.036 | 0.024 +/- 0.058 | 0.383 +/- 0.019 | 0.429 +/- 0.040 | 1.000 +/- 0.000 | 1935.574 +/- 7.694 |

## Write-command / artifact ledger and approval requests

Shell command texts below enumerate every writing invocation; the local repository path is normalized to <repo-root> for the public review, while the tool log retains the literal executed path; source bodies edited with apply_patch are represented by their files rather than copied again. Environment variables are used only as path references; none were persisted. The setup/copy commands used process-local variables, New-Item and .NET WriteAllText, without deleting or moving any existing file.

1. `New-Item -ItemType Directory -Path (Join-Path $env:TEMP 'gcam-todo33') -Force` (via local `$scratch`): creates scratch directory; seed collision check writes only console.
2. `apply_patch`: creates/edits scratch `Probe.csproj`, `Program.cs`, `summarize.py`, `scale_bounds.py`, `finalize_review.py`, `Rerun.csproj`, plus `t10/Program.cs` and `high/Program.cs`; creates/edits this review. All files are LF.
3. `dotnet build "$env:TEMP/gcam-todo33/Probe.csproj" -c Release --nologo`: scratch bin/obj and referenced engine bin/obj (NuGet cache permitted); succeeded, 0 warnings/errors.
4. `dotnet "$env:TEMP/gcam-todo33/bin/Release/net9.0/Probe.dll" '<repo-root>' "$env:TEMP/gcam-todo33"`: `rows.jsonl`, `maps.jsonl`, `runtime.json`.
5. t10 preparation: `$scratch=Join-Path $env:TEMP 'gcam-todo33'; $ten=Join-Path $scratch 't10'; New-Item -ItemType Directory -Path $ten -Force`; read original Program.cs, replace t=60 with10, disable sensitivity branch/extra iteration snapshots, write `t10/Program.cs` and `t10/Probe10.csproj` with `[IO.File]::WriteAllText(...,[Text.UTF8Encoding]::new($false))`. The command briefly inserted then removed a query placeholder before writing; the actual query export was added by apply_patch. Original running assembly was untouched.
6. `python -B "$env:TEMP/gcam-todo33/summarize.py" "$env:TEMP/gcam-todo33" '<repo-root>'`: an early call wrote partial summary.json then failed formatting incomplete seed SD; fixed the script and reran after completion. Final writes are `summary.json`, `tables.md`, `rates.json`, `audit.json`. No incomplete data entered the report.
7. `dotnet build "$env:TEMP/gcam-todo33/t10/Probe10.csproj" -c Release --nologo`: succeeded with one CS0162 scratch warning for deliberately disabled sensitivity code;0 errors. In the same shell invocation the completed main summary was generated with command6.
8. `dotnet "$env:TEMP/gcam-todo33/t10/bin/Release/net9.0/Probe10.dll" '<repo-root>' "$env:TEMP/gcam-todo33/t10"`: t10 rows/maps/runtime.
9. high preparation: `$scratch=Join-Path $env:TEMP 'gcam-todo33'; $high=Join-Path $scratch 'high'; New-Item -ItemType Directory -Path $high -Force`; read original code, keep S=1000,F={0,0.1,0.2}, enable shape/scale variants only at F>0 and only 120 iterations, write `high/Program.cs` and `high/ProbeHigh.csproj` via UTF8 WriteAllText.
10. `dotnet build "$env:TEMP/gcam-todo33/high/ProbeHigh.csproj" -c Release --nologo`: succeeded, 0 warnings/errors; scratch bin/obj and referenced engine bin/obj.
11. `dotnet "$env:TEMP/gcam-todo33/high/bin/Release/net9.0/ProbeHigh.dll" '<repo-root>' "$env:TEMP/gcam-todo33/high"`: high rows/maps/runtime.
12. `python -B "$env:TEMP/gcam-todo33/summarize.py" <root> '<repo-root>'`, roots main,t10,high: final summaries/tables/rates/audits (three invocations).
13. `python -B "$env:TEMP/gcam-todo33/scale_bounds.py" "$env:TEMP/gcam-todo33"`: writes scale-bounds.json.
14. `New-Item -ItemType Directory -Path (Join-Path $env:TEMP 'gcam-todo33/bench') -Force`; apply_patch creates `bench/Bench.csproj`, `bench/Program.cs`; `dotnet build "$env:TEMP/gcam-todo33/bench/Bench.csproj" -c Release --nologo` (0 warnings/errors); `dotnet "$env:TEMP/gcam-todo33/bench/bin/Release/net9.0/Bench.dll" "$env:TEMP/gcam-todo33" '<repo-root>'` writes `bench/timing.json`. The command completed synchronously in 6.65 s; it was not another background/watcher job.
15. `python -B "$env:TEMP/gcam-todo33/finalize_review.py" "$env:TEMP/gcam-todo33" '<repo-root>'`: completes this review with recommendations, paired ideal costs, finite-calibration counts, ordering bounds and full uncertainty tables. Re-running replaces its generated section, rather than appending duplicate sections.

Post-measurement checks passed: all summary cells contain exactly the three declared seeds and48 acquisitions; E6 atB=0 equals E0 exactly; high-count model rows reproduce the main run exactly;10 s ideal rows reproduce the60 s ideal draws exactly;80 proposed future seeds are mutually disjoint and absent from existing lists/development. These checks certify reproducibility of the scratch calculations, not their physical accuracy or product validation.

Scratch is retained; no cleanup/deletion was attempted. Rerun the original 60 s experiment with `Rerun.csproj` (explicit Program.cs compilation excludes the later nested probes), or execute the retained original binary; use a fresh subfolder under the authorized scratch directory. Re-run 10 s/high using their respective projects and fresh output roots. Timings and assembly identity can change; numerical outputs must be compared with bound inputs and identical code.

APPROVAL REQUESTS: None. No irreversible action is necessary for this review.
