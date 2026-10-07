# PLAN.Studio.StripCount.Turn2 — signed strip decoding, count uncertainty and displayed ROI units

Scope: TODO-39 implementation of SD-1 through SD-5, 2026-10-07. No material disagreement with those decisions. Production changes stay in Studio; engine algorithms, evidence, reference plan, review and protected documentation were not edited.

Status: **implemented; build, full headless tests and offscreen renders pass**. Planner-owned VV updates and desktop verification remain. No desktop was launched and no git state-changing command was run.

## Result

- CC decodes the **signed** difference; the displayed flood remains clipped. MLEM retains raw low plus its higher-line background. Both methods report the same signed net and first-order counting-plus-calibration uncertainty.
- A single contaminant includes covariance from overlapping acquisition and calibration windows. Multiple contaminants, zero calibration low counts, or missing/invalid overlap metadata report **uncertainty unavailable**; the net remains available. The calibration high window must be nonempty, as before.
- Zero and negative net estimates do not gate projection or the sweep. Raw acquisition support is stored separately. No significance/detection gate was added: K-known maxima retain their existing meaning.
- Displayed strip/count metadata controls summary, flood caption/readout, role note and ROI units, including while a selector change is pending. Flood ROIs sum displayed clipped pixels, not a hidden signed image. Reconstruction ROI units follow the published decoder.
- The UI states signed CC versus clipped display, MLEM raw/background versus clipped display, and the clipped-flood focus sweep. It qualifies the scalar model, ambient omission, and shared calibration uncertainty. At 1280 × 800 the MLEM side panel scrolls; no claim of a desktop readability or keyboard validation is made.

## Files by group

| Group | Files | Purpose |
|---|---|---|
| Core models | `src/Gcam.Studio.Core/Services/StripCountEstimate.cs` (new), `ImagingResult.cs`, `StripRatio.cs` | immutable net/variance/raw-support metadata; `HasData` independent of net sign; calibration overlap count |
| Core ViewModels | `src/Gcam.Studio.Core/ViewModels/ImagingWorkspaceViewModel.cs`, `ImagingWorkspaceViewModel.Focus.cs`, `MeasurementsViewModel.cs`, `MeasurementViewModel.cs` | net ± sigma summary, published-image units and roles, raw-support sweep command, pane-specific ROI units |
| Services | `src/Gcam.Studio.Services/StripCountEstimator.cs` (new), `StripProjection.cs` (new), `ImagingService.cs`, `ImagingProjection.cs` | pair covariance calculation, retained overlap tallies, separate display/CC/MLEM inputs, shared signed estimate |
| WPF | `src/Gcam.Studio/Views/ImagingOptionsPanel.xaml`, `MainWindow.xaml`, `Controls/AutomationEvidence.cs` | bound clipped-values caption/help/readout, `Imaging.StripNote`, numerical evidence for updated desktop oracles; no code-behind changes |
| Services tests | `tests/Gcam.Studio.Services.Tests/StripCountEstimatorTests.cs` (new), `StripProjectionTests.cs` (new), `ImagingServiceTests.cs`, `OpticsProjectionTests.cs` | independent statistical expectation, zero/negative/empty support, covariance and replay, net/count identity, signed refocus oracle |
| Core tests | `tests/Gcam.Studio.Tests/ImagingWorkspaceTests.cs` | net label for both methods, pending-strip metadata/ROI coherence, negative net with unavailable uncertainty and enabled sweep |
| UI scenarios/oracles | `tests/Gcam.Studio.UiTests/WorkspaceScenarioTests.cs`, `MlemScenarioTests.cs`, `Harness/FloodOracle.cs`, `FloodOracleTests.cs` | updated count/sigma oracle, clipped one-pixel ROI and units across method switch, signed summary parser and unit-aware ROI parser |
| Headless renders | `tests/Gcam.Studio.RenderTests/MainWindowRenderTests.cs` | strip metadata fixture and production bindings asserted under both themes/sizes |
| Report | `docs/PLAN.Studio.StripCount.Turn2.md` | this turn's verification, limits and proposed documentation edits |

Total: 14 production files and 10 test files, plus this report. All changed/new source and test files were checked for LF line endings. The owner's `docs/AGENTS.Planning.md`, `docs/AGENTS.Todo.md`, reference plan and review remain separate existing/concurrent changes.

## Statistical construction

For one contaminant, with raw acquisition counts L, H and overlap C, and calibration counts l, h and overlap c:

`R = l/h`, `N = L − R H`.

`Var(R) ≈ [l + R² h − 2 R c] / h²`.

`sigma² ≈ L + R² H − 2 R C + H² Var(R)`.

The code evaluates both expressions as sums of disjoint-category terms:

`(L−C) + R²(H−C) + (1−R)² C`, and the analogous calibration expression divided by h².

This avoids cancellation without clipping the uncertainty. For fixed-total multinomial calibration, the negative fixed-total covariance terms cancel in the ratio gradient. The overlap covariance does not cancel and is retained. Calibration uncertainty is common to frames reusing that calibration, not an extra per-frame noise draw.

Unbiasedness is conditional on an exact applicable scalar ratio and negligible own-channel contribution to the reference window. Finite calibration ratios have a small denominator bias; ambient and scalar-model residuals are excluded from sigma. No calibration seed behaviour was changed. Acquisitions alone still do not vary the deterministic calibration realization, so the uncertainty test explicitly varies synthetic calibration counts.

## Verification and numerical expectations

### Build and inventory

Final `dotnet build Gcam.sln -c Release`: **0 errors**, the same two pre-existing analyser warnings (`xUnit2012` in `WaveformServiceTests`, `xUnit2000` in the existing window-change test).

Ordinary full-suite counts, passed / skipped / total:

| Assembly | Before | After |
|---|---:|---:|
| Gcam.Tests | 465 / 0 / 465 | 465 / 0 / 465 |
| Gcam.Studio.Tests | 209 / 0 / 209 | 211 / 0 / 211 |
| Gcam.Studio.Services.Tests | 95 / 7 / 102 | 108 / 7 / 115 |
| Gcam.Studio.UiTests | 14 / 20 / 34 | 15 / 20 / 35 |
| Gcam.Studio.RenderTests, ordinary opt-out | 0 / 1 / 1 | 0 / 1 / 1 |
| **Ordinary total** | **783 / 28 / 811** | **799 / 28 / 827** |

The UI assembly's 15 passing cases are headless oracles; all 20 desktop/plot/survey cases were skipped. Render opt-in, run separately: **1 passed / 0 skipped / 1 total**, producing **92 PNGs** under `%TEMP%\gcam-todo39\render-final-t2\`. No `docs/assets` output. The four minimum-size CC/MLEM, dark/light strip fixtures were visually inspected across the two render passes; the final light MLEM fixture shows the corrected clipped-values header. Renders are analytic drawing fixtures, not physics evidence.

### Existing physical count comparison, now asserting the net

`Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount`, unchanged scene/seeds and 600 s:

| Quantity | Result / expectation / bound |
|---|---|
| Mixed raw Cs window L / Co reference H | **23,009 / 20,410** observed counts |
| H-only calibration l / h / accepted total | **6,018 / 12,787 / 100,000**, overlap zero |
| Signed net | **13,403.3552**, algebraically `L − (6018/12787) H`; exact equality asserted |
| Independent Cs-only reference | **13,331** counts |
| Residual | **+72.3552** counts |
| Var(R) | **5.4127694 × 10⁻⁵**, from the disjoint-window delta expression |
| Comparison tolerance | **1007.2427 counts**, the existing `4 sqrt(L + R² H + reference + H² Var(R))`. The reference term is retained because the Cs-only acquisition is independent. No tighter tolerance was substituted. |

The displayed floods remain nonnegative. Count metadata agrees with the independently expanded variance. Exact image/estimate equality proves signed CC input; CC and MLEM have exactly equal net/variance/display flood on one retained scene. Reprojection reuses measured energies and calibration and leaves acquisition/spectrum data intact.

The existing off-axis association test, 600 s, unchanged source geometry and seed:

| Channel | Signed-CC error | Existing derived association bound |
|---|---:|---:|
| Cs-137 | **0.3064 mm** | **3.0936 mm** |
| Co-60 | **1.4188 mm** | **3.0936 mm** |

Bound = reconstruction step × sqrt(2), **2.1875 mm × sqrt(2)** at these optics. These are association checks, not new precision claims. The existing MLEM check remains within the same bound (Cs **0.6401 mm**, Co **0.4691 mm**).

Overlap integration uses N = **10 × FWHM**, for the overlapping Cs/Co primary windows identified in the review. It counts the real retained energies in each window/intersection and expands both covariance formulas independently. Incremental prefixes, a fresh service and replay after another window width produce identical count metadata. No localisation tolerance is imposed for these broad windows. A three-isotope Co-57/Cs-137/Co-60 scene confirms two contaminants yield a finite net and unavailable variance.

### Independent calibration uncertainty test — pre-derived bounds

`IndependentFixedTotalCalibration_ReportedVarianceMatchesDerivedSamplingBounds`:

- **N = 4096** independent synthetic acquisitions/calibrations, RNG seed **390039**.
- Each calibration classifies **T = 2000** accepted events, disjoint probabilities **pLow = 0.2**, **pHigh = 0.4**, neither = 0.4. Each acquisition draws exact Poisson counts with **muLow = 1000**, **muHigh = 4000**.
- This deliberately stresses finite-ratio uncertainty and a negative net. T is a test design, not a change to production's 100,000 events or a camera performance condition.
- Before sampling, integrate `H ~ Binomial(T,pHigh)`, `L|H ~ Binomial(T−H,pLow/(1−pHigh))` over H > 0 to obtain ratio moments through order four and moments of the plug-in variance. H = 0 has probability `0.6^2000`, below binary64's representable range; production also rejects a zero calibration high count.
- Conditional Poisson cumulants and the random conditional mean give exact net mean, variance and fourth central moment. Sample-variance SE follows `(mu4 − (n−3)/(n−1) V²)/n`; mean SE follows `V/n`. For the reported-variance mean, Cauchy's SD sum bounds dependence between its three terms.
- Bounds use **four independently calculated SE**. Chebyshev gives failure probability at most **1/16 per individual moment assertion**, without assuming a Gaussian sampling distribution. The direct empirical-versus-reported comparison uses the triangle sum of these bounds plus the analytically computed approximation bias; no observed spread determines a tolerance.

| Statistic | Observed | Independent expectation | Precomputed absolute bound |
|---|---:|---:|---:|
| Mean net | **−1002.614379** | **−1002.505015**, includes finite-denominator bias | **8.166744** |
| Sample variance | **16715.235004** | **17074.100996**, exact ratio/product moments | **1524.135174** |
| Mean reported variance | **17081.669744** | **17090.457614**, expected first-order plug-in expression | **112.438821** |
| Sample variance versus mean reported variance | absolute difference **366.434740** | approximation difference **16.356618** | **1652.930614**, approximation difference + both four-SE bounds |

Empirical SD **129.287412 counts**, reported sigma `sqrt(mean reported variance)` **130.696862 counts**. All bounds pass. This tests the statistical construction independently; it is not a new full Studio list-mode calibration ensemble.

Other exact regressions cover identical overlapping windows (net = variance = **0**), zero calibration low counts (unavailable variance), missing overlap metadata (unavailable variance), zero/negative signed input with raw support (projection retained), empty raw support (no image/peaks), and high-only raw support under MLEM (negative net retained). The MLEM support test uses one iteration solely to test gating; production remains at 400 iterations.

Arithmetic identity checks either compare exact arrays/counts or bound equivalent variance expansions by **32 × 2⁻⁵² × sum of absolute terms**, covering fewer than 32 binary64 operations. This is arithmetic roundoff, not a physical tolerance. Existing desktop JSON net checks retain their six-decimal round-trip allowance; new sigma comparisons use the derived binary64 bound. ROI display checks use exact N0 / three-significant-digit formatting and exact whole-pixel counts, not a borrowed spatial or count tolerance.

## Deviations and resolved interim failures

1. Multiple-contaminant calibration covariances are not supplied. This takes SD-1's explicitly allowed **uncertainty unavailable** branch; pair overlap is fully included. Zero calibration low counts take the same branch rather than asserting zero uncertainty.
2. The first full suite exposed the pre-existing refocus oracle's assumption that the display flood was the decoder input. It now constructs the signed input from retained raw windows and ratios, while keeping exact image/estimate/peak checks and invariant count metadata. This implements SD-2; no physical tolerance was loosened.
3. A later build caught a missing `Gcam.Configuration` import in the added desktop ROI helper. It was corrected, then build and the full suite were rerun successfully. Visual inspection also caught the old flood-header counts label; its caption/help now follow published strip metadata, with added render assertions.
4. The full list-mode calibration seed policy is unchanged. Statistical validation explicitly varies calibration counts in the test, rather than treating repeated acquisition seeds as new calibration draws.
5. VV edits were intentionally not applied, per this turn's instruction. No normal opt-in long evidence suite or desktop scenario was enabled. No new MD-6 localisation ensemble was run: this turn verifies that production uses the signed decoder already measured in turn 1.

## Desktop runs for the planner

On the author's desktop go, follow the repository's UI verification procedure: full suite; broken-verdict run on the two changed scenarios (each must fail at its corrupted assertion, not by timeout); recovery. Use a separate detached worktree if other implementation is running.

1. `WorkspaceScenarioTests.Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks`: clipped display per pixel, signed net, sigma, role note, readout units and a clipped one-pixel flood ROI.
2. `MlemScenarioTests.Mlem_WithStrip_ReportsNetCountsAndFindsCsOnItsSide`: identical CC/MLEM net and uncertainty, preserved clipped flood/ROI, appropriate method role note, retained MLEM source-side check.
3. Existing `MethodSwitch_KeepsMeasurementsOnTheSameGrid`, `FocusSweep_UnderMlem_IsTheCrossCorrelationSweep`, `MlemSelection_SurvivesStopContinueAndReset_WithoutAStaleImage`, ROI/readout scenarios and the full suite for regressions.
4. Both themes, 1280 × 800 and 1440 × 900: scroll the MLEM channel panel with keyboard, read the count/caveat/role note, verify header/readout/ROI units, and switch Strip or method while a refresh is pending. Check the unavailable-uncertainty path with multiple contaminants and zero/negative net display without treating found maxima as detections.

The numeric UIA evidence now includes `StripCount`, `FloodUnit` and `StripNote`. New read-only text IDs are `Imaging.StripNote` and `FloodValueUnit`. No new interactive control was added. Desktop runs were **not executed** here.

## Proposed VV documentation edits — not applied

1. **VV.Studio.SRS, SR-IMG-05:** replace a single generic subtraction statement with: “Compton strip displays per-pixel max(0, low − Σ R·high). Cross-correlation decodes the signed difference. Both methods report signed net Σ low − Σ R·Σ high with first-order 1 sigma counting plus calibration uncertainty. Single-contaminant window overlap is included; unsupported covariance or zero calibration low counts reports uncertainty unavailable. Negative net is retained; raw acquisition support controls data presence. No detection gate is implied. The scalar model assumes an applicable calibrated ratio and does not remove ambient background.” Retain the existing calibration budget, one-pass raw-high rule and approximation caveat.
2. **SR-IMG-07:** remove the obsolete CC clipped-sum label. State the common net/uncertainty metadata and preserve MLEM's raw-low plus background input, selector behaviour, units and existing 400-iteration evidence. State that count/image-role labels follow the published result while a new method/strip setting is pending. The focus sweep still uses the displayed clipped flood and always cross-correlates.
3. **SR-IMG-03 / SR-MEAS-03 / SR-MEAS-04:** add explicit pane-value units. A stripped flood ROI is a displayed clipped-value statistic; a reconstruction ROI is a decoded/lambda statistic; neither replaces the channel-wide net count. Geometry and whole-pixel-centre inclusion are unchanged. Add the clipped-values flood caption/readout and coherent refresh of unit labels.
4. **VV.Studio.SDS:** extend SU-23 with `StripCountEstimator`, the acquisition/calibration overlap tallies and `StripRatio.OverlapCounts`, supported pair variance and unavailable branches. Extend SU-25 with `StripProjection`, signed CC versus clipped display/MLEM raw-background, and raw-support gating. Extend SU-16 with `CountSummary`, published `StripCountEstimate`, `FloodUnit`, `FloodCaption`, `FloodHelp`, `StripNote` and raw-support sweep eligibility. Extend SU-03 / SU-04 with refreshed ROI units; SU-06's arithmetic stays unchanged. Allocate helpers within these existing units instead of inventing duplicate unit IDs.
5. **VV.Studio traceability:** SR-IMG-05 → existing net-count reference test, `Strip_UsesSignedCcInput_AndMatchesMlemCountAndUncertainty`, `Strip_OverlapCovariance_ReplaysWithWindowChangesAndIncrementalPrefixes`, `Strip_MultipleContaminants_ReportNetWithUnavailableUncertainty`, `StripCountEstimatorTests`, `StripProjectionTests`. SR-IMG-07 → rename MD-11's test to `StripCount_IsLabelledNetWithUncertainty_ForBothMethods`; retain existing method/cache tests and add published metadata and negative-net tests. SR-IMG-03 / SR-MEAS-03 / SR-MEAS-04 → `StripMetadata_AndRoiUnits_FollowPublishedViewWhileStripIsPending`, the updated desktop ROI/oracles and render assertions. Keep historical desktop passes as historical; mark new expectations awaiting desktop execution.
6. **VV.Studio.Imaging / History:** record the accepted turn-1 conditional signed-versus-clipped evidence separately from this analytic statistical test and the integration regressions, with methods, budgets and limits. Record this turn's headless counts and render result. Do not revise EV-15, engine-study claims, or imply a new desktop pass.

## Every command/edit that wrote anything

All shell environment variables below were process-local. Shell commands are listed in execution order; log paths are all under `%TEMP%\gcam-todo39\`.

| Operation | Exact executable command(s), destination and outcome |
|---|---|
| Baseline | `$env:DOTNET_CLI_UI_LANGUAGE='en'; dotnet test Gcam.sln -c Release > "$env:TEMP/gcam-todo39/test-before-t2.txt"` — passed, repository bin/obj and allowed restore cache |
| Focused service verification | `dotnet test tests/Gcam.Studio.Services.Tests -c Release --filter 'FullyQualifiedName~StripCountEstimatorTests\|FullyQualifiedName~StripProjectionTests\|FullyQualifiedName~ImagingServiceTests' --logger 'console;verbosity=detailed' > "$env:TEMP/gcam-todo39/services-focused-t2.txt"` — passed, with process-local language en; the filter's separators are literal `|` characters, not shell pipes |
| Focused Core verification | `dotnet test tests/Gcam.Studio.Tests -c Release --filter ImagingWorkspaceTests --logger 'console;verbosity=detailed' > "$env:TEMP/gcam-todo39/core-focused-t2.txt"` — passed, process-local language en |
| First solution build | `dotnet build Gcam.sln -c Release > "$env:TEMP/gcam-todo39/build-t2.txt"` — passed, process-local language en |
| First build/full-suite run | `dotnet build Gcam.sln -c Release > "$env:TEMP/gcam-todo39/build-final-t2.txt"`; on successful build, `dotnet test Gcam.sln -c Release > "$env:TEMP/gcam-todo39/test-after-t2.txt"` — old clipped refocus oracle failed, process-local language en |
| Corrected refocus verification | `dotnet build Gcam.sln -c Release > "$env:TEMP/gcam-todo39/build-verified-t2.txt"`; on success, `dotnet test Gcam.sln -c Release > "$env:TEMP/gcam-todo39/test-final-t2.txt"` — passed, process-local language en |
| First offscreen render | `$env:DOTNET_CLI_UI_LANGUAGE='en'; $env:GCAM_RENDER_SNAPSHOTS='1'; $env:GCAM_RENDER_OUTPUT=Join-Path $env:TEMP 'gcam-todo39/render-t2'; dotnet test tests/Gcam.Studio.RenderTests -c Release --logger 'console;verbosity=detailed' > "$env:TEMP/gcam-todo39/render-t2.txt"` — passed; PNGs only in scratch |
| Desktop-oracle rebuild | `dotnet build Gcam.sln -c Release > "$env:TEMP/gcam-todo39/build-complete-t2.txt"` — missing import failed; guarded full-suite command was not executed, process-local language en |
| Import/header correction verification | `dotnet build Gcam.sln -c Release > "$env:TEMP/gcam-todo39/build-complete-v2-t2.txt"`; on success, `dotnet test Gcam.sln -c Release > "$env:TEMP/gcam-todo39/test-complete-v2-t2.txt"`; on success, `GCAM_RENDER_SNAPSHOTS=1`, `GCAM_RENDER_OUTPUT=%TEMP%\gcam-todo39\render-final-t2`, `dotnet test tests/Gcam.Studio.RenderTests -c Release --logger 'console;verbosity=detailed' > "$env:TEMP/gcam-todo39/render-final-t2.txt"` — all passed, variables set in that command's PowerShell process only |
| Final derived-roundoff assertions | `dotnet build Gcam.sln -c Release > "$env:TEMP/gcam-todo39/build-approved-spec-t2.txt"`; on success, `dotnet test Gcam.sln -c Release > "$env:TEMP/gcam-todo39/test-approved-spec-t2.txt"` — all passed, process-local language en. Only test/oracle roundoff assertions changed after the final render. |
| Edits | Ten `apply_patch` calls edited/added the 24 source/test files listed by group; one `apply_patch` added this report. No protected document was edited. |

Each long run was one tool-managed command process, with sequential dependencies and no watcher loop. No scratch file was written outside the task folder. No deletion, move/rename, installation, persistent setting, process stop, git mutation or upload was needed. **No approval request.**
