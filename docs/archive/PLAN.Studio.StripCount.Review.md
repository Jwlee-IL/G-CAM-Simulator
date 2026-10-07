# PLAN.Studio.StripCount.Review — measured implementer review of TODO-39

Scope: turn 1, review only, 2026-10-07. No production or test code changed. Measurements and build outputs are under `%TEMP%\gcam-todo39\`.

Status: **plan needs corrections before implementation**. SC-1 is a valid first-order uncertainty model for a clean, disjoint-window pair, not a general exact or unbiased estimator. SC-3 cannot literally hold for SC-2(a), or for today's MLEM background path. Implementation stops here for the planner's decisions.

## At a glance

| Item | Finding / recommendation |
|---|---|
| Existing behaviour | Confirmed: CC displays and decodes the clipped flood and reports its sum; MLEM displays that same flood but decodes raw low with a background and reports the signed net. |
| Count correction | Use the signed net for both methods. Separate count metadata from the displayed flood and decode input; retain negative estimates. Describe it as a net estimate, unbiased **with an exact applicable R**, approximately unbiased with finite calibration. |
| Decode choice | Recommend SC-2(a): signed CC input, clipped display. RMS is slightly lower in all six measured scenes; returned peaks and false-location indicators do not worsen. This is conditional evidence at the default optics, not a universal superiority claim. |
| Uncertainty | The proposed formula is valid to first order for disjoint Cs/Co windows. Fixed-total multinomial calibration does not invalidate it. Window overlap does invalidate the independent-window formula. |
| Numerical result | 16 fresh seeds × 50 acquisitions per scene: ideal-R signed / clipped RMS 0.039–0.073° / 0.039–0.083°. Net bias −3.1…+1.6 counts; clipped bias +50…+250 counts. Independent finite calibration: empirical SD / predicted sigma 0.942…1.042. |
| ROI | Flood ROI reads displayed clipped pixels in both methods. Reconstruction ROI reads correlation values or MLEM lambda. Neither is a source-count estimator. Keep these image statistics, label their units, and show the net count separately. |
| Verification limits | Actual list-mode calibration ensembles, overlapping-window ensembles, ambient background and desktop behaviour were not measured. No tests or Studio were launched. Scratch build: zero errors, zero warnings. |

## What exists — every reference-plan row checked

References below name the current file and member, with line numbers for this review's checkout.

| Plan row | Verified location | Verdict |
|---|---|---|
| Strip | `src/Gcam.Studio.Services/ImagingService.cs:128`, **Process** (called by `ProcessAsync`) | The block is in `Process`, not directly in `ProcessAsync`. It subtracts simultaneous `_floods` using all applicable ratios and floors each pixel at zero. The high arrays are raw, never recursively corrected. `corrected[j]` is displayed and passed to CC. Only MLEM with at least one ratio receives raw low, the background array, and the signed `EffectiveCounts` override. All remains the original broadband image. |
| Projection / effective count | `src/Gcam.Studio.Services/ImagingProjection.cs:32`, `Project` | Default effective count is the displayed flood sum. CC decodes that flood only when its sum is positive. MLEM uses `mlem.Flood ?? flood`, tests that input's sum, and can override the effective count. Found peaks are from the decoded reconstruction. |
| Count label | `src/Gcam.Studio.Core/ViewModels/ImagingWorkspaceViewModel.cs:110`, `Summary`, `CountLabel` | Confirmed. Strip plus a contaminating ratio selects the clipped/net labels; otherwise it says counts. Method-specific labelling uses **DisplayedMethod**, which follows `View.Method`, not the pending selector. TODO-38's distinction must survive this change. |
| Ratio | `src/Gcam.Studio.Services/ImagingService.cs:191`, `Calibrate`; `src/Gcam.Studio.Core/Services/StripRatio.cs`, `StripRatio.R` | Independent H-only transport, all real emission lines, fixed 100,000 accepted events, measured once per accepted event. The actual record is `(LowIsotope, HighIsotope, Events, LowCounts, HighCounts)`; **R is a computed property**, not a constructor argument. Counts are classifications of a fixed accepted-event total, not independently stopped Poisson totals. Calibration occurs on a new acquisition/configuration or a window-width change, including when Strip is off. |
| Existing strip test | `tests/Gcam.Studio.Services.Tests/ImagingServiceTests.cs:41`, `Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount` | Confirmed: compares today's clipped effective count against a separate Cs-only acquisition, using 4 × sqrt(low + R² high + expected + high² Var(R)). The additional expected term belongs to that independent reference, not the displayed channel's uncertainty. It also requires nonnegative displayed floods and retention of the All flood. |
| SRS | `docs/VV.Studio.SRS.md:142`, SR-IMG-05; `:144`, SR-IMG-07 | SR-IMG-05 requires per-pixel flooring and describes H-only calibration and the scalar limitation. SR-IMG-07 explicitly requires the current two different count labels and MLEM's background input. Both need changes after the author decides; no requirement text was edited in this turn. SR-IMG-03 / SR-MEAS-03 / SR-MEAS-04 also govern image statistics and should remain coherent. |

Calibration provenance matters: `ProcessAsync` calls `SimulationService.BuildConfig`, which calls `SceneConfigBuilder.Build(scene, optics, 1)` with the default seed **12345**. `Calibrate` sets that seed plus 6007 plus high-group index × 1009. `AcquisitionSnapshot.Seed` is not used here. For Cs then Co the calibration transport seed is **19361**, independent of the acquisition seed. `MeasurementStage` additionally uses its default seed 909, addressed by accepted-event index. Repeating an unchanged scene therefore repeats its calibration realization. This is a limitation of a proposed calibration-spread test, not a request to alter seed behaviour in this task.

## SC-1 — adopt the net count, correct the statistical claim

For an exact scalar R, disjoint raw acquisition windows L and H, and independent Poisson counts,

`N = L − R H`, `E[N] = muL − R muH`, `Var(N | R) = muL + R² muH`.

This equals the channel's own low-window expectation only when the calibration model applies and the channel does not contribute materially to H. It does not subtract ambient counts or fix direction dependence, gain effects, cascades, or the one-pass scalar model's residuals. EV-15 explicitly demonstrates direction-dependent residual bias; removing clipping is not a guarantee that all systematic bias disappears.

For fixed calibration total T, disjoint calibration probabilities p and q:

`Var(Lcal) = T p (1−p)`, `Var(Hcal) = T q (1−q)`, `Cov(Lcal,Hcal) = −T p q`.

Apply the ratio gradient `(1/Hcal, −Lcal/Hcal²)`. The fixed-total terms cancel, giving the delta-method estimate

`Var(Rhat) ≈ Rhat² (1/Lcal + 1/Hcal)`.

Thus SC-1's formula is justified for the measured Cs/Co pair. It is a **first-order** plug-in estimate, not an exact interval or a detection statistic. A noisy denominator also introduces approximately `E[Rhat] − R ≈ R/E[Hcal]`, making the net slightly low. In this experiment that leading bias is about **−0.02…−0.38 counts** across the six conditions, far below the measured counting spread.

The unconditional independent-calibration variance can be written exactly in terms of the ratio's actual moments:

`Var(N) = muL + E[Rhat]² muH + Var(Rhat) (muH² + muH)`.

The proposed observed-count formula approximates this; ratio moments and low-count behaviour need separate handling. In particular, evaluate the calibration variance as `Lcal/Hcal² + Lcal²/Hcal³` for the disjoint case rather than producing `0 × infinity` when Lcal = 0. Zero Hcal already throws. Zero Lcal does not establish zero uncertainty about an unknown contamination probability.

**Necessary correction for overlap.** `BuildBands` merges by unresolved line spacing, never by window overlap. `MainViewModel.OnWindowFwhmChanged` and `ProcessAsync` accept every finite positive width. At N = 10, the default Cs and Co primary windows are approximately **359.3–964.1** and **687.8–1658.6 keV**, so they overlap while Co remains a contaminant. If Ccal counts calibration events in both windows,

`Var(Rhat) ≈ Lcal/Hcal² + Lcal²/Hcal³ − 2 Lcal Ccal/Hcal³`.

The acquisition term also needs `−2 R Cdata`. With several references it needs the cross terms between reference windows. A practical general construction is to sum the squared per-event contribution `I_low − Σ R_h I_high,h`, then add the calibration term from its covariance matrix. `StripRatio` currently retains no intersection counts. Independent H-only runs do not make **the acquisition's** overlapping reference windows independent. If implementation does not retain the necessary covariances, state that uncertainty is unavailable outside the supported disjoint-window case; do not show the current formula as general 1 sigma.

For a held calibration, the calibration term is common uncertainty, not additional frame-to-frame counting noise. An independent-calibration ensemble measures the total term; a retained-calibration acquisition ensemble measures its conditional counting term. Name which is displayed.

## SC-2 — paired signed versus clipped CC measurement

### Method and seeds

- **N:** 16 outer seeds × 50 exact-Poisson acquisitions = **800 per condition**, six conditions, 4,800 acquisitions. Both decoder paths use the same acquisition draws. A second experiment uses the same draws with independent finite calibration; it is paired, not another 4,800 independent acquisitions.
- **Seeds:** `923113872 677484217 964928158 69367969 1002972882 16779615 140968980 1871079785 1490487761 624892624 1715149750 293155432 58593089 519280618 792611083 1681676650`.
- Generated by the MD-11 rule, `1 + UInt32LE(SHA256("gcam-todo27-20261002-" + i)[0:4]) % 1900000000`, for **i = 271…286**, immediately after MD-11's third set. Exact numeric-token searches of repository `.cs`, `.md`, `.json`, `.py` files found no occurrences before writing this review, including the evidence seed lists. A preliminary substring search matched digits within floating-point values; those were not seed occurrences. The seeds are distinct and disjoint from the repository's recorded evidence sets and MD-6 / MD-11 sets.
- **Studio default optics:** rank 13, pitch 0.7 mm, D = 80 mm, detector 30 × 30 at 0.6 mm, 10 mm tungsten, 1,000 mm source/focal plane, non-cyclic CC, tent refinement. Resolution element = **8.75 mm**, angular element = **0.501325°**; reconstruction step = **2.1875 mm**. These come from this configuration, not another scene's tolerances.
- Same MD-6 sampling recipe: Cs uniformly phased over ±0.5 element in both axes, Co three angular elements away along x or y according to seed parity; Cs low-window expectation **1,000 / 4,000**, Co:Cs **activity** ratios **1 / 2 / 4**.
- Per-line transport maps: 4,000,000 incident histories per line per outer seed, Cs 661.7 keV × 0.851; Co 1173.2 and 1332.5 keV × 0.999 each. Purposes 400001 (phase), 960000…960002 (maps), 970000 (Poisson draws), 980000 (calibration draws), keyed with the existing stream helpers.
- Windows from the actual `SpectrumService.BuildBands` and default chain at N = 1.5: Cs **616.344–707.056**, Co **1100.390–1246.010 keV**, FWHM anchor **4.56964%**. Ideal R is the expected Co-low / Co-high map ratio, median **0.451286**.
- Exact reproduction of MD-6's **map model**, not a claim to reproduce all Studio detector realism: `GateResponse.Source` uses a homogeneous crystal without entrance/backing/gaps/gain and `CountingWindow` uses a Gaussian sqrt(E) smear anchored at the chain's 662 keV width. Co lines are transported separately, without list-mode cascade summing. These were also MD-6's limits. The raw windows are disjoint and drawn independently per pixel with `Sampling.PoissonExact`.
- CC uses the actual factory decoder on double-valued signed or clipped detector images. Peaks use Studio's actual `TopPeaks(K=1, separation=one element)` plus configured tent refinement. Direct `Decode` bypasses today's positive-total gate; none of these measured net totals required that bypass to produce a peak.
- Error is `ResolvedPair.AngleDeg` from the true Cs position. False-location indicators are outside one derived angular resolution element, or nearer Co than Cs. These follow MD-6; they are not a calibrated false-alarm criterion.
- Confidence intervals below: 4,000 percentile bootstrap resamples of **outer seeds**, retaining the paired paths and all 50 repeats, aggregation RNG seed 39007. They are exploratory marginal 95% intervals, not simultaneous acceptance bounds.

### Ideal calibration, matching MD-6

| Cs expectation | Co:Cs | Signed RMS (°) | Clipped RMS (°) | Net bias (counts) | Clipped bias (counts) | Empirical net SD | Predicted sigma = sqrt(mean variance) |
|---|---:|---:|---:|---:|---:|---:|---:|
| 1000 | 1 | 0.05664 | 0.05798 | −0.65 | +87.15 | 39.89 | 38.25 |
| 1000 | 2 | 0.06292 | 0.06561 | −0.58 | +147.82 | 44.45 | 43.90 |
| 1000 | 4 | 0.07316 | 0.08258 | +0.80 | +250.07 | 55.39 | 53.45 |
| 4000 | 1 | 0.03882 | 0.03923 | +1.63 | +50.33 | 74.19 | 76.55 |
| 4000 | 2 | 0.04116 | 0.04173 | −3.06 | +100.80 | 86.07 | 87.82 |
| 4000 | 4 | 0.04648 | 0.04834 | −0.96 | +225.94 | 106.20 | 106.91 |

Ideal-R sigma omits the calibration term because R is exact in this experiment. All six bias intervals include zero; empirical SD / prediction is **0.969…1.043**, with all six bootstrap intervals including one. The clipped excess is **1.26…25.01%**, depending on counts and contamination. These fresh phases need not reproduce MD-6's exact RMS or its historical 4.5–25% range.

Paired 95% intervals for signed RMS minus clipped RMS (degrees), in table order:

`[−0.003582,+0.000528]`, `[−0.005023,−0.000335]`, `[−0.012644,−0.005888]`,
`[−0.001026,−0.000001]`, `[−0.000975,−0.000209]`, `[−0.003392,−0.000524]`.

The 1000-count 1:1 result does not establish a difference; the nearly-zero 4000-count 1:1 endpoint is not a useful superiority margin. Recommend signed decoding for linear consistency and removal of flooring bias, with the observed small localisation improvement as supporting evidence.

### Independent finite calibration, checking SC-1's predicted sigma

For each acquisition, draw **exactly 100,000 categorical accepted events**, assigning low / high / neither from the Co maps normalized by their open-window accepted rate. This is an exact multinomial draw, with the actual calibration's fixed-total construction. The expected high probability is **0.13408…0.13505**, so expected high calibration counts are about 13,400…13,500. Use Rhat = Lcal/Hcal and its disjoint-window delta variance in both decode paths and the reported sigma. This isolates count and calibration statistics; it does **not** execute `ImagingService.Calibrate` 800 times or validate its physical response model.

| Cs expectation | Co:Cs | Signed / clipped RMS (°) | Net bias, 95% interval (counts) | Clipped bias | Empirical SD / predicted sigma | SD / sigma, 95% interval |
|---|---:|---:|---:|---:|---:|---:|
| 1000 | 1 | 0.05663 / 0.05797 | −0.72 [−3.65,+2.09] | +87.10 | 40.19 / 38.57 | 1.042 [0.988,1.093] |
| 1000 | 2 | 0.06288 / 0.06526 | −0.28 [−2.73,+2.25] | +148.01 | 45.91 / 45.00 | 1.020 [0.962,1.072] |
| 1000 | 4 | 0.07286 / 0.08278 | +1.47 [−1.41,+4.50] | +250.48 | 57.68 / 56.99 | 1.012 [0.948,1.074] |
| 4000 | 1 | 0.03883 / 0.03925 | +1.62 [−3.72,+6.95] | +50.34 | 77.55 / 79.06 | 0.981 [0.929,1.032] |
| 4000 | 2 | 0.04092 / 0.04174 | −2.56 [−6.32,+1.28] | +101.32 | 90.77 / 96.32 | 0.942 [0.904,0.976] |
| 4000 | 4 | 0.04649 / 0.04832 | −3.38 [−14.66,+7.06] | +224.24 | 126.18 / 133.05 | 0.948 [0.906,0.986] |

All bias intervals include zero. The uncertainty has the right scale to about **6%** in this small ensemble, but the last two exploratory intervals exclude one: do not record exact empirical agreement or choose a test tolerance to make these results pass. A future variance assertion must have a pre-derived sampling bound and separately exercise calibration variation. For a fixed-R two-Poisson estimator, the fourth central moment is `3 V² + muL + R⁴ muH`, and the unbiased sample-variance sampling variance is `(mu4 − (n−3)/(n−1) V²)/n`. An independent-calibration test needs the corresponding ratio/product moments or a separately generated reference distribution, with the test family and error rate decided before running it. Neither a borrowed percent bound nor repeated deterministic calibration is adequate.

### Found counts and false peaks

In **every condition, either ratio model, either path**, Studio's K = 1 rule returns **exactly one peak in 800/800 frames**. Selected-peak failures are **0/800** outside one element and **0/800** nearer Co. Zero observed failures is not zero failure probability: a conditional iid-binomial one-sided 95% upper bound would be `1 − 0.05^(1/800) = 0.374%`; the 16 sampled geometries limit extrapolation beyond this ensemble.

There is no significance gate in `MixedFieldStudy.TopPeaks`: it greedily returns up to K maxima even when they are noise or negative. Thus the found-count comparison is structurally constrained and does not demonstrate absence of additional false image peaks. As a diagnostic only, asking for K = 2 with one-element blanking finds a second maximum at least one quarter of the first above the reconstruction median in **800/800 frames for both paths in all scenes**. That is a CC sidelobe diagnostic, not an additional Studio marker or a validated source detection. No new detection threshold is proposed here.

## SC-3 — revise the row; ROI is a statistic of its pane

The current route is `ImagingWorkspaceViewModel.NotifyResult` → `Measurements.Refresh(Result)` → `MeasurementsViewModel.Refresh(MeasurementViewModel)` → `MeasurementViewModel.Refresh` → `MeasurementMath.Roi`.

- **Flood:** `Result.Flood`, its flood origin and pitch. With stripping, this is the clipped flood for **both** methods. A whole-detector ROI therefore equals the clipped sum, including under MLEM where the channel summary already reports a different net count.
- **Reconstruction:** `Result.Reconstruction`, reconstruction origin and step. Under CC, sums are sums of correlation values; under MLEM, sums are lambda values, not `Σ sensitivity × lambda` or a net count.
- Pixels are included by centre, rectangle corners in either order, clipped to image bounds. `MeasurementMath.Roi` computes sum, mean and max and already accepts negative values. The scratch probe checks full-image ROI sum against the corresponding image sum for every decoded flood, without changing that function.
- `MeasurementViewModel` says `Σ …` plus pixel count, mean and max; it receives **no method, strip or value-unit metadata**. `MainWindow.xaml:354` also hardcodes flood readout `ValueUnit="counts"`. Reconstruction readout follows `ReconstructionUnit`, but ROI descriptions do not. These are the places needing explicit units if the display remains clipped.

The plan's instruction that ROI statistics use the same image the decoder used is **wrong as a general rule**: it contradicts option (a) and the existing MLEM background path. Keep flood ROI on the displayed flood, label it **clipped strip sum / values**, and keep reconstruction ROI on the displayed reconstruction with decoded/lambda units. Do not silently show a signed ROI sum over a visibly clipped image. Found peaks already use the reconstruction and will naturally follow whichever CC input is chosen.

`FocusSweepService.SweepAsync` projects `request.Image.Flood`. `ImagingWorkspaceViewModel.Focus` passes the selected `Result` and gates the command using positive `EffectiveCounts`. With option (a), the focus sweep will still consume the clipped retained flood unless explicitly changed; a signed count must not accidentally become its data-presence test. Keep the existing sweep for this task, retain the clipped-flood caveat, and record that it differs from the signed main CC reconstruction. Any change to the sweep would need separate measurement.

## SC-4 — revise the verification plan

Keep the existing comparison to the independent Cs-only run and its **extra reference-count variance term**. Changing the estimator may reduce its residual, but does not justify tightening the same derived 4-sigma tolerance without a new statistical design. Check the estimator algebra exactly, the displayed flood flooring separately, and that MLEM and CC report the same net and uncertainty for the same retained windows.

Add meaningful cases for overlapping windows or explicitly unsupported uncertainty, zero calibration low counts, signed zero/negative net, multiple contaminants, and pending selector changes versus published method/strip metadata. Found peaks must follow the decoded reconstruction; display ROI must remain a statistic of its displayed image. An uncertainty test must distinguish held calibration from independent calibration and use a derived variance-sampling bound as described above. The present seed experiment is review evidence, not a passing unit-test assertion.

Planner-owned SRS edits should name display flooring separately from signed CC decoding and from the net estimator, define uncertainty applicability, and keep the MLEM raw/background contract. Update SR-IMG-05 / SR-IMG-07 and the affected Studio design/verification and measurement unit descriptions after decisions; leave the historical evidence numbers intact.

## SC-5 — confirmed, with scope clarified

No engine algorithm or EV-15 numerical evidence needs changing. `ComptonStudy.RunStripping` still floors its own study image and reports that image's sum. EV-15 spans several models and geometries; its absolute-field table reports +16.8 / +17.3% floored bias at 8:1, and is not Studio's 1 m MD-6 measurement. Keep those historical results. Add new conditional **Studio** evidence for the adopted behaviour; do not imply that the new Studio decode path reproduces the existing engine-study results.

## Proposed changes to the plan

1. **Qualify SC-1:** net estimate, unbiased with exact applicable R; first-order total statistical/calibration uncertainty for supported windows. Include overlap covariance or explicitly withhold sigma when unavailable. Handle zero calibration counts without NaN and keep the scalar-model/ambient limitations visible.
2. **Adopt SC-2(a):** CC decodes the signed difference; display remains clipped. Store display image, decode data and net count/variance independently. Preserve MLEM's raw-low/background input and All's unchanged semantics.
3. **Replace SC-3's shared-input rule with pane semantics:** found peaks and reconstruction ROI follow the reconstruction; flood ROI follows the displayed flood. Label clipped flood readout/ROI statistics; show net ± sigma separately, following published result metadata rather than pending settings.
4. **Define zero/negative-count handling:** keep the signed estimate; use raw acquisition support to decide whether projection is possible. Do not reuse a signed sum as a data-presence gate in `ImagingProjection` or the sweep. Keep the sweep's current clipped input and disclose the difference.
5. **Correct SC-4:** preserve the independent-reference term and derived comparison tolerance; exact algebra and state/ROI checks plus a separately designed uncertainty test. Vary calibration explicitly in scratch/test design; acquisition seed variation alone does not do it. Do not fit a tolerance to the two low SD/sigma bootstrap results.
6. **Keep SC-5's boundary:** record this experiment in Studio's verification documentation after adoption; retain EV-15 and engine-study values. Correct the plan's `ProcessAsync` strip-block reference and `StripRatio` constructor description.

## Open questions for the author

1. **Which CC strip view should users see?** Recommendation: option (a), signed decoding with clipped flood display. It preserves the current display and gives small measured improvements; name the two roles explicitly. Option (b) needs a signed colour scale and a wider UI change that this turn did not measure.
2. **Should flood ROI report displayed values or a hidden signed net?** Recommendation: displayed values labelled as clipped strip statistics, with channel-wide net ± sigma in the summary. A separately selectable signed ROI mode would be a new feature, not an implicit reinterpretation of the current ROI.
3. **How should weak/negative net estimates and their uncertainty read?** Recommendation: show signed `N ± sigma net counts (1 sigma, counting + calibration)` where supported, and `uncertainty unavailable` otherwise; add a short scalar-model caveat. Keep existing found-coordinate behaviour without presenting sigma or a returned peak as a detection verdict. Do not add a new detection gate in TODO-39.

## What could not be checked

- No desktop, app launch, UI automation or UI tests, as required. No colour-scale, wrapping or accessibility rendering claim is made.
- No normal repository test run: this turn builds and executes only the scratch evidence probe. No test inventory changes.
- No full Studio list-mode calibration/acquisition ensemble with gain, gaps, backing, entrance and cascades. The finite-calibration experiment validates the fixed-total **statistical construction**, not every dependency or response in `Calibrate`.
- No overlap or multi-contaminant numerical ensemble, other optics, count-null false-alarm distribution, ambient field, or focus-sweep remeasurement. Those cannot inherit this experiment's location or sigma evidence.

## Commands and edits that wrote anything

No destructive command, git mutation, installation, persistent environment change, process stop or upload was needed. Initial git status already contained the author's modified `docs/AGENTS.Todo.md` and untracked reference plan; they were not edited.

| Write operation | Destination / effect |
|---|---|
| PowerShell `New-Item -ItemType Directory -Force -Path (Join-Path $env:TEMP 'gcam-todo39')`; SHA-256 seed generator and `File.WriteAllText(... 'seeds.txt', ...)` | Scratch directory and the 16-seed list. |
| PowerShell project here-string and `File.WriteAllText(... 'Probe39.csproj', ...Replace(CRLF,LF))` | Scratch project referencing existing Services and Decoding projects. The repository seed-token search in that command was read-only. |
| `apply_patch`, add `Program.cs`; second `apply_patch`, correct the seed-file path | Scratch probe only. |
| `dotnet build "$env:TEMP/gcam-todo39/Probe39.csproj" -c Release --artifacts-path "$env:TEMP/gcam-todo39/artifacts" -p:NuGetAudit=false` | All project restore/build outputs redirected to scratch artifacts; NuGet cache allowed. Zero warnings/errors. |
| `dotnet "$env:TEMP/gcam-todo39/artifacts/bin/Probe39/release/Probe39.dll" "$env:TEMP/gcam-todo39" > "$env:TEMP/gcam-todo39/run.txt"` | One background run; `configuration.json`, 16 `seed-*.json` files and `run.txt`, all scratch. The worker parallelizes seeds inside that one process. |
| `apply_patch`, add `aggregate.ps1` | Scratch aggregation and seed-bootstrap script. |
| `& "$env:TEMP/gcam-todo39/aggregate.ps1" > "$env:TEMP/gcam-todo39/aggregate.txt"` | Scratch `summary.json` and `aggregate.txt`. |
| `apply_patch`, add this review | **Only repository file written:** `docs/PLAN.Studio.StripCount.Review.md`, English, LF. |

Scratch source, raw data, configuration and aggregation are retained for the planner's audit. No approval request is necessary for the work completed in this turn.
