# TODO-25 continuation review (2026-10-08)

Status: research preparation only. C-1 is a historical observation; C-2 is a short current-tree pilot; C-3 through C-6 are proposals for the author. No Studio replacement is justified. No product code, tests, existing results, original plan, earlier review or evidence records were edited.

## Premises requiring correction

The research premise (joint forward likelihood can improve on the focus heuristic) remains plausible. The earlier review's claims that small bias, small wrong-maximum rate and honest intervals were already "met" are too strong. Failure to reject zero bias is not equivalence to a small bias. Pooled coverage can hide bad conditions. Zero failures in 12 trials gives a one-sided 95% upper failure limit of 22.1%, not a small certified rate.

The probe reports approximate LR-at-truth diagnostics, not root-found interval endpoints. Its response surfaces at the optimum and truth are fitted separately; their difference can be negative. An exact maximized LR is non-negative because the unrestricted parameter space contains the restricted space. Increasing the number of validation seeds does not fix inconsistent profiles. Numerical profile consistency must be addressed in a subsequent research-probe turn before certifying interval coverage. Do not silently clamp negative values, exclude them, inflate intervals, or retune on validation seeds.

The current imaging measurement path uses MeasurementStage's default smear seed 909, as does this probe. Thus these ensembles condition on that smear stream and the gain map (sigma 0.03, seed 1); they do not validate arbitrary manufactured maps or every measurement-noise stream. The CRN transport model deliberately retains its own counter generator; the engine/data generator changed. Distinct integer seeds alone are not a proof of independent random streams.

## C-1: historical 10 s v3 pass, before the generator change

Source: samples/evidence/depthlik/results/fit_main10v3.csv and log_main10v3.txt, built against 85ff1ec before e0c36a1. The CSV and log have exactly the same 192 data rows as multisets. All 96 floods have both channels; all 16 conditions have 12 rows, with no duplicate condition/seed/channel. Some rows retain tag main10v2 because v3 kept that answer; that does not mean the sweep candidate was omitted. There are 22 switches.

Historical wrong maximum retains the old definition ONLY: lambda_truth > 25, lambda_truth < -10, or Euclidean fitted mask-bearing error > 3 mrad. Bias/spread below include every row because none meets that definition. Coverage uses lambda_truth <= 1 / 3.84, including negative values, exactly as the old review did. Lengths are mm, counts are seed means, SD is sample SD, SE = SD/sqrt(12). Sigma is the median finite Hessian width. This is historical descriptive material, not current evidence.

| channel | z | angle mrad | counts | bias | SE | SD | median error | sigma | wrong | surface fallback | 68% | 95% | mean LR |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|---:|
| all | 300 | 0 | 7726 | -1.166 | .401 | 1.388 | -.989 | 1.321 | 0 | 0 | 5/12 | 11/12 | 1.994 |
| all | 300 | 30 | 7314 | .857 | .418 | 1.450 | 1.115 | 1.317 | 0 | 0 | 7/12 | 11/12 | 1.670 |
| all | 500 | 0 | 2880 | 1.734 | 1.204 | 4.170 | 2.742 | 6.211 | 0 | 0 | 9/12 | 12/12 | .826 |
| all | 500 | 30 | 2591 | .838 | 1.609 | 5.574 | 1.326 | 6.563 | 0 | 0 | 9/12 | 12/12 | .769 |
| all | 700 | 0 | 1448 | -.416 | 4.403 | 15.252 | -1.752 | 13.762 | 0 | 0 | 8/12 | 11/12 | .898 |
| all | 700 | 30 | 1321 | -.214 | 5.271 | 18.258 | -3.571 | 16.216 | 0 | 0 | 8/12 | 11/12 | 1.146 |
| all | 1000 | 0 | 706 | .451 | 10.206 | 35.355 | .134 | 48.696 | 0 | 0 | 9/12 | 12/12 | .619 |
| all | 1000 | 30 | 643 | 2.555 | 10.286 | 35.632 | -6.517 | 52.667 | 0 | 0 | 10/12 | 12/12 | .521 |
| win | 300 | 0 | 2365 | -1.213 | .595 | 2.062 | -1.912 | 2.929 | 0 | 2 | 5/12 | 11/12 | 1.770 |
| win | 300 | 30 | 2261 | .215 | .998 | 3.457 | .997 | 2.988 | 0 | 1 | 6/12 | 11/12 | 1.451 |
| win | 500 | 0 | 870 | -.466 | 2.961 | 10.258 | 2.526 | 13.993 | 0 | 1 | 10/12 | 12/12 | .362 |
| win | 500 | 30 | 811 | -2.549 | 3.010 | 10.426 | -4.208 | 14.631 | 0 | 0 | 9/12 | 12/12 | .506 |
| win | 700 | 0 | 442 | -7.682 | 9.489 | 32.872 | -9.884 | 38.024 | 0 | 1 | 7/12 | 11/12 | 1.225 |
| win | 700 | 30 | 420 | -24.323 | 9.713 | 33.646 | -12.895 | 40.421 | 0 | 3 | 10/12 | 11/12 | .654 |
| win | 1000 | 0 | 211 | 31.215 | 44.689 | 154.809 | -6.817 | 124.359 | 0 | 2 | 8/12 | 12/12 | .567 |
| win | 1000 | 30 | 204 | -48.310 | 22.856 | 79.177 | -43.367 | 123.431 | 0 | 2 | 9/12 | 11/12 | .519 |

Pooled historical rate: 0/192 wrong, 129/192 = 67.19% at 68%, 183/192 = 95.31% at 95%. v2 had 11/192 wrong and 177/192 coverage at 95%. The 68% numerator stays 129. All-events coverage is 65/96 and 92/96; window coverage is 64/96 and 91/96. There are 12 surface fallbacks, zero profile fallbacks, and 40 negative LRs. The 1000 mm / 0 / window seed 21000070 gives 1441.668 mm, LR -1.1716 and no Hessian width. That single point substantially enlarges the SD; zero "wrong maxima" does not mean small depth errors. At 300 mm / 0 / all, absolute bias/SD is 0.84, inconsistent with describing every observed bias as much smaller than spread.

For zero failures the exact one-sided bound is U = 1 - alpha^(1/n). At alpha=.05, U(192)=1.548%, U(12)=22.092%. Pooling the two channels is not 192 independent floods: the window is a subset of the all-event data. The pooled bound is only a binomial illustration; per-condition bounds are the relevant ones. Do not upgrade this retained evidence with current provenance.

For scale, unadjusted exact CP95 coverage bounds are [60.06%,73.78%] for 129/192 and [91.29%,97.83%] for 183/192 under an independent-binomial assumption; they are not a valid pooled certificate for these correlated channels. Per-cell examples, where seeds represent distinct floods: 5/12 -> [15.17%,72.33%], 11/12 -> [61.52%,99.79%], 12/12 -> [73.54%,100%]. The uncertainty is large even at apparently perfect coverage.

Reproduce descriptively from <repo>:

```powershell
python -B samples/evidence/depthlik/analyze.py samples/evidence/depthlik/results/fit_main10v3.csv
```

## C-2: current-tree build and pilot

The retained Probe.csproj already uses a relative project reference. Both Release builds succeeded without any API port or source change to the probe. Two existing nullable warnings remain in Fit.cs (CS8604 and CS8619). No physical or optimizer changes were made. Product tests were not needed for this unchanged research executable. Current HEAD observed by the adapter's provenance is aeac875991d2ecf968f28312a126a1b23f658c8c, SDK 9.0.311. The smoke source record is dirty and binds snapshot 3d62c9e23d552c210947a32a5a7994d089975c84aac9f6a7d25edaa9dbdfd633; its execution identity is 191136ff01cf30b98eb7b04a3e989def393192234cc92945858a092de5c72a05. These identifiers describe the adapter smoke, not retrospectively the direct pilot. Direct pilots remain exploratory raw scratch measurements, without per-seed driver provenance.

The main pilot uses one flood per z=300/500/700/1000, angle=0/30 mrad and live=10/60 s, both channels, N=900000, coarse N=90000, K=140, stream 1. Seed base 110000000 is independent of the selection/validation lists. Sixteen flood workers share the CRN arrays, with one Model per worker. The v3 sweep pass follows the v2 pass; do not read the replaced v3 row's seconds as the total fit cost. Total channel fit time is original v2.seconds + v3.v3_seconds. Acquisition and process startup are additional.

The main pilot completed, including both search stages. Its 32 rows have zero historical wrong maxima, four v3 switches, seven negative LRs and zero final surface/profile fallbacks. v2 alone had two failures, both repaired: 300/30/10/window, 228.220 -> 294.010 mm; 300/0/60/window, 135.944 -> 298.985 mm. Historical-style coverage is 21/32 and 29/32, descriptive only. The original v2 times sum to 4074.8 seconds and incremental v3 times to 805.3 seconds. Individual channel fit costs total 4880.1 worker-seconds; median 116.1 seconds, range 109.0-459.5 seconds. Main conditions and timings below were measured under 16 flood workers, not a quiet serial core; acquisition and startup are outside these per-channel timers.

| z mm | angle mrad | T s | all counts | win counts | all error mm | win error mm | all fit s | win fit s |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 300 | 0 | 10 | 7594 | 2316 | 2.241 | 6.980 | 166.6 | 155.7 |
| 300 | 30 | 10 | 7225 | 2202 | .777 | -5.990 | 113.5 | 376.7 |
| 500 | 0 | 10 | 2820 | 890 | -.680 | 23.564 | 114.9 | 131.6 |
| 500 | 30 | 10 | 2555 | 826 | -5.209 | 5.614 | 110.1 | 158.2 |
| 700 | 0 | 10 | 1582 | 518 | 5.888 | 51.014 | 113.6 | 109.3 |
| 700 | 30 | 10 | 1382 | 430 | 6.443 | 24.601 | 113.2 | 113.8 |
| 1000 | 0 | 10 | 702 | 193 | 20.691 | -59.321 | 109.4 | 184.2 |
| 1000 | 30 | 10 | 571 | 187 | -84.625 | -155.392 | 110.0 | 267.9 |
| 300 | 0 | 60 | 46459 | 14247 | .033 | -1.015 | 172.1 | 459.5 |
| 300 | 30 | 60 | 44298 | 13774 | .137 | -2.374 | 164.9 | 155.7 |
| 500 | 0 | 60 | 16967 | 5178 | -.018 | -7.168 | 117.3 | 109.0 |
| 500 | 30 | 60 | 15290 | 4780 | -1.026 | -4.424 | 111.7 | 177.4 |
| 700 | 0 | 60 | 8801 | 2667 | 1.971 | 13.605 | 154.6 | 110.1 |
| 700 | 30 | 60 | 7960 | 2492 | 1.118 | -11.007 | 118.4 | 110.3 |
| 1000 | 0 | 60 | 4408 | 1366 | 23.732 | -15.883 | 110.0 | 110.6 |
| 1000 | 30 | 60 | 3805 | 1173 | 21.099 | -39.962 | 114.8 | 135.0 |

One draw cannot estimate current bias, spread, failure probability or coverage, nor demonstrate equality of old/new distributions. At 60 s the historical reference has 11 instead of 12 seeds at 1000 mm / 30 mrad. Standardizing current errors by (current_error - historical_mean)/historical_SD gives a largest magnitude of 3.974 at 300/0/10/window, then 2.455 at 300/0/10/all and 2.447 at 1000/30/10/all. This is a reason to investigate, not a tolerance failure or a distribution-equivalence claim. The far-field window's -155 mm error is approximately 1.35 old SD at 1000/30/10, where the distribution is broad. Poisson sizing of count differences, (new_count - old_mean)/sqrt(old_mean*(1+1/n_old)), gives maximum magnitude 3.461 at 700/0/10/window (518 versus old mean 442), with 3.370 for all (1582 versus 1448). These channels share events; this is one fluctuation, not two independent anomalies. Most costs are lower than the loaded historical cost.txt numbers; no apples-to-apples quiet-core comparison was made.

The separate low-budget adapter smoke completed in 13.5 seconds and provenance.validate_run verified all exported files plus recipe.py in Executor.Files, runtime 9.0.13. Its N=9000, coarse=9000, K=8 fit is infrastructure verification, not physics evidence. The supplementary 15 mrad pilot at 500/1000 mm, 60 s also completed, using two flood workers, the same model/coarse budgets, and the same pilot seed base. It has no wrong maxima or switches, three negative LRs, and approximate coverage 3/4 and 4/4. All four residuals are within 1.63 historical SDs of the earlier 15 mrad set's mean; counts are within 1.59 of the Poisson sizing standard deviation. This is descriptive comparison, not distribution validation. Combined pilots have 36 channel fits (18 floods): zero historical wrong maxima, ten negative LRs, approximate coverage 24/36 and 33/36.

| z mm | channel | counts | error mm | historical mean/SD mm | original v2 s | incremental v3 s | total fit s |
|---:|---|---:|---:|---|---:|---:|---:|
| 500 | all | 16865 | -3.259 | -.74/2.48 | 100.6 | 21.7 | 122.3 |
| 500 | win | 5168 | -5.189 | -.26/3.03 | 103.1 | 20.4 | 123.5 |
| 1000 | all | 4029 | -1.871 | -3.36/17.80 | 98.2 | 81.8 | 180.0 |
| 1000 | win | 1240 | 6.440 | -13.95/34.67 | 114.3 | 95.0 | 209.3 |

The extra v3 candidates at 1000 mm converged near 163 mm and lost to the retained v2 fit, adding substantial cost without changing the answer. This reinforces the need to budget for rejected candidates. Every process started in this turn has exited successfully; none is left running.

## C-3: proposed decision criteria, fixed before validation

These are proposed research acceptance margins derived from an explicit error/risk allocation, not tolerances inherited from another camera. The author must choose the desired risk and cost. Recommend recording screening first, fixing profile consistency and the model budget, then approving the expensive scoped validation. There is no pass verdict in this turn.

Use 32 cells (4 depths x 2 bearings x 2 live times x 2 channels). Keep the two channels separate; do not count them as independent repeats. Allocate overall alpha=.05 over at most five statements per cell: catastrophic-search risk, bias equivalence, 68% coverage, 95% coverage, numerical/profile validity. Bonferroni alpha_cell=.05/160=.0003125 does not require independence between channels or cells. Additional mismatches/bearings require a new declared allocation and seed calculation.

1. **Catastrophic search/association risk:** proposed upper bound <=1%. This is an author's risk choice (at most one unsupported result per hundred acquisitions), not a physical law. Report the historical criterion beside it for comparability, but do not reuse 3 mrad as a new tolerance. The shadow's element in mask-bearing coordinates is pitch/D=.7/80=.00875 rad. Half an element is 4.375 mrad; flag a fitted bearing outside that radius, a non-finite/boundary result or an independently audited better basin. A depth-only false basin can evade the bearing check. Use a validation-only multistart/audit candidate to test it; truth must never seed the estimator. A likelihood-support gap log(100)=4.605 is a proposed 100:1 audit alarm, not a guaranteed optimization error bound. Record smaller gaps and search reproducibility as well. LR-at-truth >25 alone is not proof of a wrong maximum: sampling and physical mismatch also exclude truth.
2. **Small bias:** require a simultaneous confidence interval for mean depth error entirely inside +/-0.25 of the condition's population SD. The margin allocates at most 0.25^2=6.25% of statistical variance to squared bias, so RMS inflation from bias alone is sqrt(1.0625)-1=3.08%. Do not accept merely because zero is inside a confidence interval. Use a studentized seed bootstrap for the standardized mean with an explicit resampling seed and conservative simultaneous bounds, or noncentral-t inversion if the selection data support normal errors; heavy tails invalidate a casual Gaussian certificate. Include all returned estimates in descriptive bias/RMSE, record failures separately, and do not certify an estimator conditional only on successful searches. Report bias, SD, median, IQR, RMSE and tails as measured values.
3. **Coverage equivalence:** proposed 68% band [.62,.74] and 95% band [.92,.98], requiring exact two-sided Clopper-Pearson confidence intervals entirely inside the bands per cell. These are author-selected maximum discrepancies of six and three percentage points (six or three additional exclusions per hundred); they are not guaranteed by Wilks. Exact chi-square cutoffs are 1 and 3.8414588, giving nominal 68.2689% and 95%; the exact nominal can replace .68 in the seed arithmetic. Count failed or invalid intervals as noncoverage. Root-find the profile endpoints, retain disconnected confidence sets, expose boundary censoring and flags; Hessian widths alone are insufficient. Report numerical-invalid counts with the same risk bound as search failures.

**Seed derivation:** With zero failures, the simultaneous 1% bound needs ceil(log(.0003125)/log(.99))=804 independent floods per cell. At n=1024 zero failures gives U=.7851%. For coverage, z_(1-alpha_cell/2)=3.60471. The approximate planning formula n=z^2*p*(1-p)/delta^2 gives 786 seeds for p=.68, delta=.06, and 686 for p=.95, delta=.03. At n=1024 the approximate half-widths are .05255 and .02455; exact CP containment decides the result. Normal-theory uncertainty on a standardized bias is approximately 3.60471/sqrt(1024)=.11265, leaving useful space inside the .25 margin. These counts are planning estimates, not a promise that realized intervals will pass or that normality holds. A numerically exact pre-run CP acceptance/power calculation should accompany the adopted gate; lack of power is inconclusive, never pass.

The reserved 1024 seeds per cell provide a feasible minimum, not a sufficiently powered full-grid recommendation. Report 32 screening seeds first and predeclare a smaller scope if cost is unacceptable. With only 32 seeds, even zero failures gives U=8.94% at unadjusted 95%; that run cannot satisfy the proposed gate. Do not select passing cells after inspecting validation outcomes. If a selected rule changes after validation, use the reserved confirmation list and record the failed validation. Exact CP examples at the adopted per-statement alpha: 699/1024 -> [.62825,.73375], inside the proposed 68% band; 973/1024 -> [.92108,.97123], inside the proposed 95% band. These demonstrate feasible acceptance counts, not the probability of passing.

Power matters: at n=1024 the normal approximation leaves only .06-.05255=.00745 of sampling movement for the 68% coverage gate, while sampling SE is .01458. Thus nominal per-cell acceptance is only approximately 39%, even though some outcomes pass; exact CP power is smaller or different. A 95% per-cell nominal-power planning estimate uses (z_critical+1.96)^2*p*(1-p)/delta^2: approximately 1872 seeds (68%) and 1634 (95%). Recommend 2048 per cell if the author actually commissions the full simultaneous grid, extending the reserved lists deterministically and verifying disjointness before launch. At 2048 the approximate nominal coverage powers are approximately 97% and 99% per cell; that is still not a 95% probability that every one of 32 cells passes. A desired family-wide power needs an explicit additional beta allocation and possibly 4096 or more seeds. Cost estimates below use the currently reserved 1024 as a lower planning scale; double them for 2048. This is why staged scoped validation is the practical recommendation.

## C-4: count-scaled budget selection and confirmation

The budget must depend on measured data counts C for the channel being fitted, never on true z or live time. A candidate rule is N(C)=900*ceil(max(900000,k*C)/900); 900 is the number of pixels, so each prefix is pixel-stratified. Try k=20/40/80/160/320 on selection seeds, paired against a doubled budget and a separate model stream. No value of k is an accepted default yet. Re-fit the global search at each budget; a refit initialized at the lower-budget answer cannot discover a previously missed basin.

Derivation: if independent model error acts like a second multinomial sample with effective size Neff, the first-order relative extra variance is C/Neff. To restrict model jitter to 0.2 statistical SD, allocate extra variance <=.04, hence Neff>=25*C. Historical model effective counts were about .5*N (all) and .16*N (window); this suggests N>=50*C or 156.25*C, respectively. Those fractions are historical observations, not guarantees, and the discontinuous CRN likelihood may violate the first-order argument. Thus the candidate k grid brackets the scale rather than certifying it. At 46000 counts k=160/320 needs about 7.36M/14.72M histories, not 0.9M. The current fixed-budget selection reaches 14.4M to bracket the experiment; a further doubling may be required before adopting anything.

Selection: 16 new seeds at 300 and 1000 mm, 0 and 30 mrad, 60 s, both channels; paired fixed budgets .9/1.8/3.6/7.2/14.4M. This simultaneously probes the high-count near field and low-information far field. Full 32-cell screening supplies counts and identifies profiles/search tails. Add 500/700 mm and 10 s to selection before freezing a rule if the screen identifies a new failure mechanism; screening seeds then count as selection, never validation.

Proposed convergence allocation: mean paired shift <=.10 statistical SD, paired model-stream SD <=.20 statistical SD, and root-found interval endpoints stable within .10 statistical SD. The .10 mean shift reserves less than half of the .25 total bias margin; the .20 random contribution adds at most 4% variance (1.98% SD). Treat these as measured equivalence checks with seed uncertainty, not per-fit accuracy assertions. Establish endpoint stability numerically rather than inferring it from unchanged point estimates. Persist counts, budgets, model stream, fit diagnostics and totals per fit. Select the cheapest passing k and freeze it before Validation1024 is opened.

**Readiness limit:** the unchanged probe fits both channels at one fixed budget and V3.Run hardcodes model stream 1. The wired fixed-budget families are a useful initial selection experiment. They do not yet implement count-dependent budgets, model-stream swaps, root-found intervals or numerical search audits. Those changes belong to the next approved research implementation turn, not an API-only port disguised as a fix. No command here is represented as complete final gate validation.

## C-5: mismatch study and sources of perturbation sizes

Generate truth with a cloned config; alter only truth-side fields and MeasurementStage settings. Keep G.Base/model/calibration/window boundaries nominal. Do not perturb shared static settings, fit with the altered truth config, or drop unrelated fields by hand-cloning. For a change to Geometry.MaskDetectorDistanceMm, set truth SourceMaskDistanceMm=z_true-D_true so the source stays at the specified detector distance; changing D alone otherwise changes source z as well. Distinguish a physical MaskOffsetZMm perturbation from a nominal geometry/calibration error and record both explicitly. Preserve the generator seed for nominal/perturbed pairs. Record realized counts as well as depth shifts and coverage; allow real count changes instead of rescaling them away. Several gain/fabrication maps require an outer map cluster, with transport seeds inside; thousands of transport seeds on one map do not establish a population tolerance.

| Perturbation | Suggested levels | Source and interpretation |
|---|---|---|
| Physical mask-detector spacing | +/-0.125, +/-0.25, +/-0.5 mm | .5 mm is the plan's sensitivity experiment, not an EV tolerance. Halving it supplies a local derivative and nonlinearity check. Also derive calibration sizes from the depth error allocation below. |
| In-plane mask translation, each x/y | +/-0.2, +/-0.4 mm | EV-31's approximately .4 mm registration for the legacy lab camera's approximately 1 mm lateral budget. These are stress sizes, not accepted depth tolerances at Studio optics. |
| Mask roll | +/-1, +/-2 degrees | EV-31 measured a 2-degree roll; its small lateral effect is not evidence of small depth bias. |
| Fabrication position error | sigma 20/40/80 micrometres | EV-30: 40 micrometres passed a legacy seed-mean localization gate, 80 failed. Keep the six fixed patterns 100-105 for comparability; additional held-out pattern seeds are necessary for any population claim. |
| Global gain | +/-0.3%, +/-0.9%; stress +/-1% | EV-28's reference-source precision sigma=.3% motivates one and three sigma sensitivity points; EV-15 explicitly investigated 1% gain error. This camera has no established gain calibration, so these are hypothetical calibration/stress cases. |
| Unknown pixel gain map | truth sigma=.03, seeds 2-9 versus nominal seed 1; optional sigma=.15 stress | .03 is this probe's current configured map, .15 is EV-18's measured energy-smearing case. EV-18 does not establish a residual gain tolerance for depth. Separate known-new-map and incorrectly retained old-map fits. |
| Crystal/measurement response | derive from an actual head calibration; first compare response alternatives at the same nominal geometry | EV-17's 6.16+/-0.25% FWHM is a different front-end stream's measured seed spread, not a manufacturing tolerance. EV-19's different materials are design alternatives, not uncertainty on GAGG. No cross-section or FWHM-error tolerance is established in the evidence register; do not invent +/-5% or +/-10% acceptance errors. The author must supply a calibration uncertainty or accept a clearly labeled sensitivity-only study. |

Spacing derivation: in a thin-plane model magnification is m=z/(z-D). Equating truth with nominal D gives z_fit=D_nom*z_true/D_true. Therefore delta_z approximately -z*delta_D/D. With D_nom=80, D_true=80.5 and z_true=1000, delta_z=-6.211 mm (first order -6.25). Predictions at 300/500/700 mm are -1.863/-3.106/-4.348 mm. Finite slab/crystal and bearing coupling can change this; compare measured slopes with the calculation without imposing an arbitrary agreement tolerance. Check signs and uncertainty from paired seed differences.

If spacing alone gets the .25*SD bias allowance, |delta_D|<=.25*SD*D/z. Using historical Hessian widths only as pilot sizing, at 300 mm / 60 s all with SD about .55 mm this is .0367 mm; at 1000 mm / 60 s all with SD about 19 mm it is .38 mm. If spacing receives only .10*SD of the total allocation, these tighten to .0147/.152 mm. Derive final tolerances from current measured SD and actual calibration uncertainty. Applying the older lateral .4 mm registration tolerance to depth would be invalid. Translation also changes bearing by approximately delta_x/D: .4/80=5 mrad. In a mismatch experiment that is physical calibration bias, not by itself a search failure.

Recommend staged mismatch screening: 16 paired seeds per level at 300/1000 mm, 0/30 mrad, 60 s; both channels. Approximately 30 non-nominal levels imply 1920 perturbed floods plus 64 shared nominal floods, before multiple maps. This is a sensitivity map, not certified coverage. After selection, predeclare a small physically calibrated mismatch envelope and recompute validation multiplicity/sample size. Reserve Validation1024 for that final envelope; retain a nominal comparator on the same validation seeds. Include y and diagonal bearings before claiming a general x/y/z estimator. The wired families currently cover x bearings only.

## C-6: evidence families, provenance and seeds

Added a separate depthlik/manifest.json and depthlik/seeds.json. The global manifest/seeds and every existing family's default paths remain unchanged. New family depthlik-screen has 32 seeds and the full 16-flood grid at .9M. Five depthlik-select-N families have 16 paired seeds, four floods per seed at fixed N=.9/1.8/3.6/7.2/14.4M. Each driver job is serial internally (one worker), so --jobs 24 is at most 24 fit workers, not 24 x 24. This is parallel independent fitting, not a measured 24-core speedup of one fit.

The new optional --seed-file defaults to the old global file. An optional probe recipe_driver invokes the unchanged executable's fit/v3 modes inside the per-seed frozen snapshot. The adapter exports stage2/stage3 CSVs and process timings to the hash-bound run directory. It checks row completeness/uniqueness and subprocess exit status; failure leaves an unsuccessful provenance record. The driver script itself is added to Executor.Files; otherwise recipe.py could change without changing ExecutionId. Existing binary/source/recipe/output hashes, runtime version, dirty-tree source closure and reuse rejection are retained. Do not use --force or --allow-changed-config to bypass identity conflicts; choose a fresh output root. Keep executable output separate from pilot CSVs, because managed_files snapshots the entire directory.

Seeds are bases; probe data_seed = base + 1000*z + 37*angle + 7*T, with one repeat index (s=0). Lists:

- Selection16: 400000000 + 2000003*i, i=0..15.
- Screen32: 500000000 + 200003*i, i=0..31. Once inspected, these are exploratory selection seeds.
- Validation1024: 600000000 + 200003*i, i=0..1023; reserved, unopened.
- Confirmation1024: 900000000 + 200003*i, i=0..1023; reserved for a changed rule, unopened.

All expanded 16-condition data seeds were checked unique within and disjoint between lists and disjoint from every retained depthlik CSV's seed and the global evidence seed lists. Pilot base 110000000 and adapter-smoke base 120000000 are outside those ranges. Model streams belong to the separate CRN generator. Budget families deliberately reuse selection floods for paired comparisons. No selection or screening seed becomes validation. Provenance cannot make historically observed samples independent.

The generic aggregate.py has no depthlik parser. analyze.py is descriptive CSV analysis, not a provenance-preserving gate aggregator. Subsequent aggregation must validate every run with provenance.collect/validate_run, refuse missing/failed/changed inputs, bind the analysis script and arguments, and publish hash-bound summaries through provenance.publish. Historical CSVs must stay outside that merge.

## Planner commands and run budget

Working directory for every command below: <repo>, the G-CAM-Simulator repository root. All outputs are under %TEMP%/gcam-todo25. These are PowerShell command lines. Start each measurement command as one planner-owned background job; no watcher loops and no detached helper. Commands are staged; do not start selection until screening and the profile diagnostics have been reviewed. A completed run can resume only with unchanged execution and recipe identity.

```powershell
dotnet build samples/evidence/depthlik/probe/Probe.csproj -c Release -o "$env:TEMP\gcam-todo25\planner-executor"
python -B samples/evidence/run_seeds.py --manifest samples/evidence/depthlik/manifest.json --seed-file samples/evidence/depthlik/seeds.json --family depthlik-screen --probe "$env:TEMP\gcam-todo25\planner-executor\Probe.dll" --out "$env:TEMP\gcam-todo25\screen-v1" --jobs 24
python -B samples/evidence/run_seeds.py --manifest samples/evidence/depthlik/manifest.json --seed-file samples/evidence/depthlik/seeds.json --family depthlik-select-900000 depthlik-select-1800000 depthlik-select-3600000 depthlik-select-7200000 depthlik-select-14400000 --probe "$env:TEMP\gcam-todo25\planner-executor\Probe.dll" --out "$env:TEMP\gcam-todo25\selection-v1" --jobs 24
```

Exact final validation/mismatch commands cannot honestly be supplied yet: those recipes require the adopted count rule, consistent interval implementation, model-stream support and truth perturbation support, which do not exist in this unchanged probe. After the author's decisions, the next research implementation turn must prepare and pilot those families before the planner starts them. The commands above are executable screening/initial selection preparations, not a substitute for C-4/C-5 validation. This dependency is material and explicitly prevents a premature gate verdict.

Measured-cost runtime sizing: screening is 32 x 4880.1 / 24 = 6506.8 seconds = 1.81 hours of ideal worker throughput. Allow 2-4 hours wall time for process/GC overhead, worker imbalance and other load. Selection's four-flood 60 s subset costs 1422.6 worker-seconds at .9M. Linear scaling of the whole cost (including the fixed coarse component) across budgets sums 1+2+4+8+16=31, so 16 x 1422.6 x 31 / 24 = 8.17 hours; plan 8-16 hours. This is a sizing estimate, not a measured upper bound: search paths can grow, independent processes differ from the shared-array pilot, and saturation/memory pressure can exceed the allowance. The highest-budget family alone may dominate the tail. The planner should record actual per-family wall times before proceeding.

Full 1024-seed, 32-cell validation at the insufficient .9M budget already scales to 57.84 hours ideal. Applying the trial k=160 rule to current counts would multiply the linearly scaled full fit cost by 1.813 (both channels separately), giving approximately 104.88 hours ideal before independent-stream audits, endpoint root finding and mismatch; provisionally 5-10 days rather than a one-evening run. This multiplier is evaluated from pilot counts and assumes linear cost, not convergence or acceptance. A full fixed 14.4M validation would be roughly 16 x 57.84 = 925 hours ideal and is not recommended. A 1920-flood mismatch screen using the near/far 60 s pilot subset is about 7.90 hours ideal at .9M, and perhaps 1-3 days at a count-scaled budget with extra maps; the final adopted matrix determines the cost. No such runs were started.

Scale runtime by transported histories and measured per-condition cost, with a load margin. Do not assert that 24 cores make a serial fit 24 times faster. Memory: Histories stores two doubles, int and ulong per rounded history (28 bytes before array overhead); at 14.4M this is approximately 403 MB per worker process, approximately 9.7 GB across 24 jobs plus .NET/decoder allocations. Choose 12 workers if available memory does not support that; double the ideal throughput estimate. Do not inspect unapproved system directories to discover machine state.

## Author questions and recommendations

1. Gate scope/cost: (A) staged research screening, fix profiles, then scoped validation; (B) full simultaneous 32-cell validation now; (C) record descriptive research only. Recommend A. B cannot start with the current interval implementation; C cannot authorize a replacement.
2. Catastrophic-result cap: (A) simultaneous upper <=1%; (B) <=5%; (C) no pass/fail cap, record only. Recommend A for any replacement claim. With zero failures, B needs 158 seeds for the same alpha allocation, but bias/coverage equivalence still drives larger N. This is a risk decision for the author, not a borrowed camera specification.
3. Bias and coverage allocation: (A) .25 SD bias, coverage discrepancies <=6/3 percentage points; (B) tighter margins with recalculated seed/time cost; (C) descriptive results without equivalence. Recommend A as an explicit research gate, not a product accuracy guarantee.
4. Budget implementation: (A) count rule chosen on selection, independent model streams, consistent profiles; (B) fixed large N for every fit; (C) change to a smoother deterministic/quasi-MC model in a separate research design. Recommend A first; B is simpler but wastes far-field runtime and still does not fix profiles; C changes the estimator and needs fresh selection/validation.
5. Calibration/mismatch: (A) measured calibration uncertainty and nominal-only claim until supplied; (B) stress study at the evidence-derived sizes, explicitly no hardware-tolerance claim; (C) assume the legacy tolerances apply to depth. Recommend A plus B for sensitivity. Reject C: the spacing derivation already shows why it is unsafe to transfer a lateral allowance.
6. Low-count far-field/window and omitted bearings: (A) retain all cells and include y/diagonal before a general claim; (B) predeclare a supported count/bearing scope using selection only; (C) drop failures after validation. Recommend B if runtime is limited, otherwise A; reject C.

## Files, verification and command audit

Repository writes: this review; samples/evidence/depthlik/analyze.py, recipe.py, manifest.json, seeds.json; samples/evidence/run_seeds.py; samples/evidence/provenance.py. The probe itself and src/tests are unchanged. docs/AGENTS.Todo.md and the original plan were already modified when inspected; docs/PAPER.ko.md also appeared modified in a later status check. None of those edits was made by this turn. Existing retained results stay untouched.

Builds: success, zero errors, two retained warnings. Legacy enumeration still gives 3232 jobs and the original default probe path. New enumeration gives 112 jobs (32 screening plus 5 x 16 selection). Provenance tests: 29 run, 28 passed, one explicitly opt-in Git mutation test skipped. These checks do not validate the physics or approximate intervals.

Write-command ledger (full inline Python bodies are in the tool command log; no hidden child/watch process was launched):

1. dotnet build samples/evidence/depthlik/probe/Probe.csproj -c Release -o "$env:TEMP\gcam-todo25\build". Writes repository obj/intermediates, scratch assemblies, potentially NuGet cache.
2. Main pilot shell: `$pilot = Join-Path $env:TEMP 'gcam-todo25/build'; dotnet "$pilot/Probe.dll" fit 900000 90000 140 1 110000000 '300;500;700;1000' '0;30' '10;60' pilot_v2 1 1 16; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; dotnet "$pilot/Probe.dll" v3 900000 90000 140 "$pilot/fit_pilot_v2.csv" pilot_v3 16`. Writes only pilot CSVs beside scratch executable.
3. apply_patch: optional seed-file/recipe-driver handling, provenance driver identity, new recipe.py.
4. First inline `python -B -` manifest/seed generation attempt: failed decoding the existing global seeds.json under the shell's cp949 default before any writes. Corrected by explicit UTF-8; no locale/environment persistence change.
5. Second inline `python -B -`: writes depthlik manifest/seeds, normalizes edited driver/provenance to LF; verifies expanded seed disjointness first.
6. `python -B -m unittest discover -s samples/evidence/tests -p test_provenance.py`: **scratch constraint breach**. The existing test suite defaults its retained fixtures to %TEMP%/gcam-todo37, outside the additional TODO-25-only scratch constraint. No cleanup/deletion or Git mutation ran; the opt-in mutation test was skipped. This was avoidable and is disclosed for the planner's audit. Fixtures remain; no command to delete them is requested.
7. apply_patch: new descriptive analyze.py.
8. `$env:GCAM_PROVENANCE_TEST_ROOT = Join-Path $env:TEMP 'gcam-todo25/provenance-tests'; python -B -m unittest discover -s samples/evidence/tests -p test_provenance.py; python -B samples/evidence/depthlik/analyze.py samples/evidence/depthlik/results/fit_main10v3.csv`. The variable lasts only for that shell process; tests write retained fixtures inside the allowed scratch, analyzer writes stdout only.
9. dotnet build samples/evidence/depthlik/probe/Probe.csproj -c Release -o "$env:TEMP\gcam-todo25\executor". Clean numerical-output-free executor directory; no deletion/clean command.
10. Inline `python -B -`: writes smoke-manifest.json and smoke-seeds.json under %TEMP%/gcam-todo25 for one very small adapter test. This low-budget smoke is not measurement evidence.
11. apply_patch: this review and subsequent measured-result additions.
12. `python -B samples/evidence/run_seeds.py --manifest "$env:TEMP\gcam-todo25\smoke-manifest.json" --seed-file "$env:TEMP\gcam-todo25\smoke-seeds.json" --family depthlik-smoke --probe "$env:TEMP\gcam-todo25\executor\Probe.dll" --out "$env:TEMP\gcam-todo25\adapter-smoke" --jobs 1`. Completed; writes frozen snapshot and hash-bound smoke outputs under the allowed scratch.
13. Supplementary pilot shell: `$probe = Join-Path $env:TEMP 'gcam-todo25/build/Probe.dll'; dotnet $probe fit 900000 90000 140 1 110000000 '500;1000' 15 60 pilot15_v2 1 1 2; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; dotnet $probe v3 900000 90000 140 "$env:TEMP\gcam-todo25\build\fit_pilot15_v2.csv" pilot15_v3 2`. Writes only supplementary pilot CSVs beside the scratch executable.

No approval-controlled destructive action is required. No long ensemble was started. No desktop/GUI was used. All started commands finished before this turn ended. No cleanup commands or approval requests are necessary.
