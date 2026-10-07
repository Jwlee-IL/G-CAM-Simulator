# VV.Studio — verification and validation of GCAM Studio

Scope: the three Studio projects (`Gcam.Studio.Core`, `Gcam.Studio.Services`, `Gcam.Studio`) and their tests.
Not covered: the simulation engine (verified by its own test project, `tests/Gcam.Tests`; the product-level
evidence is [VV.Gcam.Evidence](VV.Gcam.Evidence.md)) and the original code-behind viewer `src/Gcam.Wpf` (removed in TODO-12 (2026-10-02); last present at `85b2ed1`). This document says **how we know Studio does what it must**; it is
one of a set:

| Document | IEC 62304 activity | Holds |
|---|---|---|
| [VV.Gcam.URS](VV.Gcam.URS.md) · [VV.Gcam.PRS](VV.Gcam.PRS.md) | system level, above §5.2 | user needs and product requirements for the whole Gcam product concept; Studio is its subsystem SS-4 |
| [VV.Studio.SRS](VV.Studio.SRS.md) | §5.2 requirements analysis | the requirements (`SR-*`), inputs / outputs, messages, risk-control requirements |
| [VV.Studio.SDS](VV.Studio.SDS.md) | §5.3 architecture, §5.4 detailed design | software items and units (`SU-*`), interfaces, SOUP, requirement allocation |
| VV.Studio (this page) | §5.5–5.7 verification, validation, §7 risk, §9 problems | traceability to tests, validation scenarios, anomalies, risk table |

The working design guides are [DESIGN.Architecture](DESIGN.Architecture.md) and the other `DESIGN.*` pages.

**At a glance**
- 109 active software requirement IDs plus nine withdrawn IDs (§4). Live acquisition verification and
  measurements are recorded in [VV.Studio.Acquisition](VV.Studio.Acquisition.md); desktop acquisition
  validation passed in the final desktop pass on 2026-10-02.
- Twelve regression scenarios, a plot gate and a 16-frame diagnostic survey are opt-in desktop tests.
  Scenarios judge product state using independent oracles; every corrupted verdict fails. VAL-09 … VAL-13 below
  cover Spectrum, Waveform, Detector, isotope imaging and retained-flood focus. Screenshots never decide a numerical verdict.
- The layering that keeps the logic testable is compiler-enforced (SR-ARCH-01, -04).
- Open problems are in §6 (AN-01 … AN-15); no screen-reader session has been run.

Dated implementation records below retain their execution limits and counts. Subsequent analysis identified the Co-60 bias as undersampling; current verification totals are in [Current test inventory](#current-test-inventory).

TODO-08 phase B verification (Release, 2026-10-02): the reviewer reported 259 engine, 86 Studio Core,
28 services and 10 UI-oracle tests passing and a build with 0 errors. The measured imaging results,
including the Co-60 bias finding TODO-17 with its cause open, are in [VV.Studio.Imaging](VV.Studio.Imaging.md).
Local `dotnet build Gcam.sln -c Release -m:1` passed with 0 warnings and 0 errors (the default parallel
invocation exited 1 without a diagnostic error). The offscreen render opt-in passed after removing the
options panel's Border ancestor binding, regenerating eight mixed-scene PNGs plus the existing eight
workspace and four plot PNGs. Local `dotnet test Gcam.sln -c Release -m:1 --no-build` also completed
successfully: 259 engine, 86 Studio Core, 28 services and 10 UI-oracle tests passed; nine desktop tests
and the independent opt-in render case skipped. A later shell creation failed with CreateProcessAsUserW error 5; no further
commands were launched. The final binding-identity assertions compiled in the local build but were added
after the passing render run; their rerun and visual inspection were not performed. Desktop validation
remains pending, and no Studio launch or desktop UI test was performed.

TODO-09 adds effective editable optics and retained-data refocus. The added deterministic mixed-preset
regressions use 20,000 accepted list-mode events per preset/plane, complete isotope lines and activities
Cs-137:Co-60:Co-57=1:1:1.5 at normalized positions (-0.5h,-0.3h),(0.5h,-0.3h),(0,0.5h).
The gate is one resolution element per isotope channel, with/without stripping; it is not a broadband or
sub-mm guarantee. The sampling regression uses geometric Cs-137, 1 m, 18 mm detector, pitches 0.6/0.3 mm,
y=-10..10 in 1 mm steps, 1,000,000 biased histories/position, seed 12345 and 0.25 mm recon grid. It asserts
improvement with finer sampling across positions, not a universal threshold. Offscreen images are explicitly
analytic drawing fixtures, not MC evidence. Release verification (2026-10-02):
`dotnet build Gcam.sln -c Release -m:1` passed (0 errors; one existing xUnit2000 warning).
`dotnet test Gcam.sln -c Release -m:1 --no-build` passed: 259 engine, 102 Core, 40 services
and 10 UI-oracle cases; nine desktop cases and the opt-in render case skipped.
The separately logged sampling regression (started before the command block) also passed:
RMS/max 1.0734/1.9966 mm at 0.6 mm pitch and 0.5847/0.9525 mm at 0.3 mm pitch (re-measured 2026-10-02 with the replaced random generator; previously 1.0810/1.9966 and 0.5837/0.9511) under
the 1,000,000-history conditions above. This reduced-budget run is distinct from the theme-55
3,000,000-history evidence quoted in the UI.
With `GCAM_RENDER_SNAPSHOTS=1`, the render case passed without opening a window, generating
16 additional F=800 expanded/collapsed optics images alongside the refreshed existing captures in
`assets/studio-render/`. Desktop validation remains deferred; Studio was not launched.
Visual inspection found the preset selection displayed the record's diagnostic string. A final
`OpticsPreset.ToString()` override now returns the preset name. Its rebuild command,
`dotnet build Gcam.sln -c Release -m:1 --no-restore`, was blocked before process creation with
`CreateProcessAsUserW failed: 5 (access denied)`. No further verification commands were launched.
The counts above precede this final display-only change. The planner subsequently verified the final
Release build (0 errors), all 259/102/40/10 tests and the render case (1/1), and inspected all 16 new
optics snapshots, including the derived geometry at F=800.

Long numerical measurements now use `GCAM_EVIDENCE_TESTS=1`; ordinary test runs and CI report them
as skipped. The full sampling sweep and the 20-seed precision measurement keep their existing budgets
and output. Their evidence and reproduction command are in [VV.Studio.Imaging](VV.Studio.Imaging.md).
The separate small off-axis sampling regression remains in the default suite; its seed-spread measurement
also uses the evidence opt-in. Follow-up command execution was blocked while reading engine code
(`CreateProcessAsUserW failed: 5`, access denied); no further commands were started, so the revised
default service-suite duration and final regression verification are unavailable locally.
The already-running budget calibration completed successfully: 100,000 histories/position at
y={-8,-4,4,8}, five seeds, fine/coarse RMS ratios 0.463150..0.591137. The revised gate is <0.85
(observed max + twice the observed range, rounded up). Full-grid calibration took 193.9 s; the
final fast test restricts reconstruction to ±12.125 mm on the same 0.25 mm grid phase. Its margin,
seed table and the distinction between measured calibration and unverified final runtime are in
[VV.Studio.Imaging](VV.Studio.Imaging.md).

## Current test inventory

Verification of the ambient default and the fixed spectrum axis, 2026-10-04, on the tree committed as `13387fb`.
Commands: `dotnet build Gcam.sln -c Release` and `dotnet test Gcam.sln -c Release --no-build`.
Build: zero errors, zero warnings; the two existing xUnit analyzer warnings (`ImagingServiceTests.cs:106`,
`WaveformServiceTests.cs:118`) reappear only when `Gcam.Studio.Services.Tests` is fully recompiled. Normal test
execution:

| Assembly / suite | Passed | Skipped | Inventory |
|---|---:|---:|---|
| `Gcam.Tests` | 393 | 0 | engine physics / compatibility |
| `Gcam.Studio.Tests` | 205 | 0 | Core ViewModels, geometry, policies and token contrast |
| `Gcam.Studio.Services.Tests` | 92 | 7 | real-engine services; long numerical evidence opt-in |
| `Gcam.Studio.UiTests` | 13 | 14 | headless oracles pass; 12 desktop scenarios, plot gate and survey opt out |
| `Gcam.Studio.RenderTests` | 0 | 1 | independent offscreen render opt-in |
| **Total** | **703** | **22** | **725 cases; zero failures** |

No desktop, render or long-evidence opt-in was enabled for this run. The retained final desktop record
reports 27/27 total UI cases (13 headless oracles + 12 scenarios + gate + survey); **27 is a suite total,
not a scenario count**. The retained RTL execution record in `rtl/README.md` reports 137 cocotb cases
with C# vectors (26 configurations, 452,608 exact sample comparisons). Neither desktop nor cocotb
was rerun for this record. **Desktop pending:** the desktop scenarios have not been run since Studio starts with
the ambient field on (SR-RUN-29) and draws the fixed 0–2000 keV spectrum axis (SR-SPEC-01); no desktop scenario
covers the ambient panel or the dose warning yet. Dated inventories and execution restrictions below remain history;
use this section for current normal-run totals.

## 1. Scope and intended use

**Intended use.** GCAM Studio is a portfolio demo viewer for Gcam, a Monte Carlo simulator of a coded-aperture
gamma camera. A user places gamma sources in a scene, runs the simulation, inspects the detector flood map and the
decoded reconstruction, and measures on both images in mm. Users are engineers and reviewers reading the code; the
output informs no decision about people or real sources.

**Relationship to IEC 62304.** This document is structured after IEC 62304 practices — software requirements
(§5.2), unit, integration and system verification (§5.5–5.7), risk control (§7) and problem resolution (§9) —
because that is the discipline the author works in. **GCAM Studio is not a medical device and not a regulated
product. No compliance or certification is claimed.** The safety class below is illustrative.

**Illustrative software safety class: B.** *If* the same viewer displayed real localisation data that an operator
acted on, a wrong coordinate, a wrong ROI sum, or an old image shown as current could mislead that operator. The
viewer does not control anything, so the worst plausible outcome is a wrong reading that a person then checks —
non-serious injury at most, hence B rather than C. As actually used (a demo on simulated data) it would be Class A.

**Engine boundary.** Physics, decoding and the scene → config builder belong to the engine. Studio relies on them
through Core acquisition, spectrum, imaging, waveform, detector-face and focus-sweep service contracts. The engine inventory is recorded below;
`SpectrumServiceTests` additionally verifies the measured spectrum against real-engine deposits.

## 2. Software items

Items SI-1 … SI-4 and their units SU-01 … SU-26 are defined (Waveform and Detector/focus designs extend them in the later sections) in [VV.Studio.SDS §2](VV.Studio.SDS.md#2-software-items-and-units-531-541).
How each item is verified:

| Item | Project | Verified by |
|---|---|---|
| SI-1 Presentation logic | `Gcam.Studio.Core` (net9.0, no WPF) | unit tests with fakes |
| SI-2 View geometry and measurement maths | `Gcam.Studio.Core/Imaging` | unit tests, pure maths |
| SI-3 Simulation adapter | `Gcam.Studio.Services` | integration tests against the real engine (`AcquisitionServiceTests`) + engine-level tests |
| SI-4 WPF shell | `Gcam.Studio` (net9.0-windows) | inspection + manual UI Automation |
| SOUP ([VV.Studio.SDS §4](VV.Studio.SDS.md#4-soup-and-required-platform-533-534)) | NuGet | used as published; not separately verified |

Only SI-1 and SI-2 can be unit-tested with fakes, by design: everything with logic lives in a project that has no UI
stack. SI-3 is tested through the real engine.

## 3. Software requirements

The requirements are specified in [VV.Studio.SRS](VV.Studio.SRS.md) — **108 active** in eighteen groups, including
navigation, plotting and acquisition. Seven withdrawn batch rows retain their IDs. The matrix cites IDs only;
the SRS is the single source of their wording.

## 4. Verification

Levels follow IEC 62304: **unit** (§5.5) = one class in isolation; **integration** (§5.6) = ViewModels together, or
Studio against the real engine; **system** (§5.7) = the running app. Methods: **T** automated test (exact
`Class.Method`), **C** compiler-enforced, **I** inspection of code / XAML, **M** manual check driven through UI
Automation. Status: **pass** (evidence on 2026-10-01), **partial**, **open** (no evidence yet).

### Traceability matrix

| Req | Level | Method | Evidence | Status |
|---|---|---|---|---|
| SR-OPT-01, -02 | unit + offscreen | T, I | `OpticsPolicyTests.Presets_ApplyAtomically_MatchPhysicalFieldsAndShowCustom`; `OpticsViewModelTests.Focus_AcquiringStoppedCompleted_KeepsEventsAndSpectrum_OpticsLockedWithData`; physical input bindings | pass headless + offscreen; desktop deferred |
| SR-OPT-03 | unit + integration | T | `OpticsPolicyTests` finite/text/cross-field cases; `OpticsViewModelTests.InvalidInput_StartExpandsSectionsAndDoesNotStartTransport`; service config validation | pass headless; desktop deferred |
| SR-OPT-04 | unit + integration | T | `OpticsProjectionTests.Refocus_ReprojectsAllChannels_WithoutMeasurementCalibrationOrNewEvents`, `EmptySnapshot_RefocusKeepsEmptyImagesAndDoesNotCalibrate`; `OpticsViewModelTests` lifecycle/revision/acquisition tests | pass headless; desktop deferred |
| SR-OPT-05 | unit + integration + offscreen | T, I | `OpticsPolicyTests.Geometry_UsesEffectiveFieldsAndHasNoPerformanceBand`; `OpticsProjectionTests.Sampling_SmallOffAxisSample_FinerPixelsReduceLocalizationError` (default); full position sweep (GCAM_EVIDENCE_TESTS=1); [sampling evidence](VV.Studio.Imaging.md); one-line caption with the figures as tooltip / help text (`Imaging.SamplingEvidence`) inspected in the renders | prior full sweep passed; revised default regression verification pending |
| SR-OPT-06 | offscreen | T, I | `PlotViewRenderTests.Spectrum_BothThemesFullAndZoom_RenderWithoutWindow`: expanded/collapsed optics at F=800, both themes, 1280×800/1440×900; asserts the default scene panel has no scrollable overflow in every workspace at both sizes; IDs Optics.*, Detector.Section, Chain.Section, Imaging.FocalPlane/Geometry/FocusNote, Imaging.FocalSection/ChannelSection/SweepSection; Tools and measurements under the images inspected | pass offscreen; desktop deferred |
| SR-IMG-01 | unit + integration | T | `ImagingWorkspaceTests.SharedWindow_SelectorStripAndRoi_ReuseFrozenAcquisition`; `ImagingServiceTests.WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing` | pass (headless); desktop pending |
| SR-IMG-02 | integration | T, I | `ImagingServiceTests`; shared `SpectrumService.BuildBands` and `MeasurementStage`; [imaging evidence](VV.Studio.Imaging.md) | pass (headless); desktop pending |
| SR-IMG-03 | unit + offscreen | T | `ImagingWorkspaceTests.SharedWindow_SelectorStripAndRoi_ReuseFrozenAcquisition`; two-isotope All / Cs-137 offscreen renders | pass (headless + offscreen); desktop pending |
| SR-IMG-04 | unit + integration + offscreen | T, I | `ImagingServiceTests.Channels_OffAxisCsAndCo_LocalizeAtTheirOwnSource`, `Precision_TwentySeeds_MeasuresMixedIsotopesAndCoOnlyControl`; `OverlayLabelLayoutTests` (6 cases: gaps, marker/measurement clearance, bounds, recalculation, deterministic packing, omission and invalid geometry); crowded equal-Y and edge-chip renders with diamond overlay and coordinate list | pass (headless + offscreen); measured Co-60 bias in [imaging evidence](VV.Studio.Imaging.md); updated layout desktop pending |
| SR-IMG-05 | integration | T | `ImagingServiceTests.Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount`, `WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing` | pass (headless); desktop pending |
| SR-IMG-06 | unit + integration | T, I | `ImagingWorkspaceTests.LateResponse_CannotReplaceNewerWindowResult`; serialized `Task.Run` worker and separate stopwatches | pass (headless); desktop pending |
| SR-IMG-07 | unit + integration + offscreen | T, I | `ImagingWorkspaceTests.Reconstruction_IsAReprojectionSetting_KeepsMeasurementsAndSweep`, `ReconstructionNote_FlagsUnmeasuredOptics_AndTheAcquisitionImageIsNotShownAsMlem`, `StripCount_IsLabelledClippedForCrossCorrelation_AndNetForMlem`; `ImagingServiceTests.Mlem_ChannelsLocalizeAtTheirOwnSource_NonNegative`, `Mlem_BuildsOneMatrixPerGeometryAndLine_AndReusesItAcrossRefreshes`, `CrossCorrelation_IsUnchangedByTheMethodField`; `MlemReconstructionTests` (factory MLEM bit-identical to the evidence construction); MLEM offscreen renders; [imaging evidence](VV.Studio.Imaging.md) | pass (headless + offscreen); desktop pending |
| SR-RUN-01 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-09, -19 |
| SR-RUN-02 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-10 |
| SR-RUN-03 | unit | T | `AcquisitionViewModelTests.Failure_WithData_KeepsItLocked_ResetOnly`, `Failure_WithoutData_BehavesAsEmpty` | pass |
| SR-RUN-04 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-11 |
| SR-RUN-05 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-12 |
| SR-RUN-06 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-13 |
| SR-RUN-07 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-14 |
| SR-RUN-08 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-15 |
| SR-RUN-09 | unit + integration | T, I | `AcquisitionViewModelTests.Reset_DiscardsData_UnlocksInputs_NextStartIsANewAcquisitionWithANewSeed`; `AcquisitionServiceTests.ShortAcquisition_LocalizesAfterCountThreshold_SnapshotsAreImmutable`; worker inspected | pass (headless); desktop pending |
| SR-RUN-10 | unit + integration | T | `AcquisitionViewModelTests.Start_SnapshotsGrow_StopKeepsDataAndLocks_ContinueAccumulates`; `AcquisitionServiceTests.Stop_KeepsConsumedPrefix_AtExtremeMcLimitedSpeed` | pass (headless); desktop pending |
| SR-RUN-11 | unit + integration | T | `AcquisitionViewModelTests.Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset`; `AcquisitionServiceTests.InjectedClock_AdvancesLiveTimeAndPresetWithoutWallDelay` | pass |
| SR-RUN-12 | unit + offscreen + inspection | T, I | `AcquisitionViewModelTests.Start_CapturesDetectorInputs_AndLocksThemWhileDataExist` (every physical writer refused with data), `LiveTimeAndSpeed_LockedWhileAcquiring_InvalidValuesFallBack`; `AmbientViewModelTests.AmbientPhysicalInputsLockAndDerivedBsrHasNoSetter`; render assertions: source editor and chain selectors disabled with data; `CanEditInputs` bindings inspected | pass (T/I); desktop pending |
| SR-RUN-13 | unit + inspection | T, I | `AcquisitionViewModelTests.Start_SnapshotsGrow_StopKeepsDataAndLocks_ContinueAccumulates` (Start / Continue label and availability), `Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset` (guard inside the command); `AmbientViewModelTests.DefaultIsValidatedFrontOnlyField_ZeroIsIdeal_SourceFreeStartRequiresAbsoluteField` (source-free Start with a field, disabled at 0); `IsDefault` removed (I) | pass (T/I) |
| SR-RUN-14 | removed | — | stale flag, chips and their tests removed | withdrawn → SR-RUN-12, SR-RUN-23, SR-RUN-26 |
| SR-RUN-15 | integration | T | existing nearest-prime / finite-grid builder tests; `AcquisitionServiceTests.InvalidInputs_FailBeforeStarting` (4 cases) | pass |
| SR-RUN-16 | engine + integration | T, I | `ListModeSourceTests` (weighted flood, spectrum, rate, exponential gaps, seed, no duplicates); `CascadeEmissionTests` (biased Co-60 decays vs analog 4π decays: detected and coincident probability per decay and the rate within 4σ; one summed event per decay with one arrival time; a single-line config keeps its line); `CascadeSummingTests` (Co-60 branching, W(θ) moments and histogram, inverse CDF, Na-22 table values); service immutable / count conservation test | pass |
| SR-RUN-17 | integration + inspection | T, I | injected-clock test (250 ms); acquisition ViewModel test (ROI grows, old snapshot unchanged); fixed grid and decode per tick inspected; timings in acquisition record | pass (T/I); desktop pending |
| SR-RUN-18 | integration + unit | T | extreme MC-limited service test; virtual ViewModel test verifies note and retained prefix | pass |
| SR-RUN-19 | unit + integration | T | Completed ViewModel test; service preset completion test; defaults inspected | pass |
| SR-RUN-20 | integration + unit + inspection | T, I | `DetectorRealismTests.StudioDefaults_AreExplicit_AndSceneBuilderRemainsBare`; `MainViewModelTests.DetectorInputs_DefaultsAndValidationFallbacks_EditableWithoutData`; geometry fields and `CanEditInputs` binding inspected | pass headless; desktop fields pending |
| SR-RUN-21 | integration + unit | T | `DetectorRealismTests.Gain_WidensPhotopeakInQuadrature_AndReducesTightWindowAcceptance`, `Measurement_UsesFrozenInputs_AndReplaysAcrossSnapshotsAndPileUp`; `AcquisitionViewModelTests.Start_CapturesDetectorInputs_AndLocksThemWhileDataExist` | pass |
| SR-RUN-22 | engine + inspection | T, I | `ListModeBackgroundTests` verifies rate, spatial profile, energy response, exact disabled stream, seed and cancellation; BSR field and worker inspected | pass headless; desktop BSR input pending |
| SR-RUN-23 | integration + unit | T | `AcquisitionContinuationTests.StopAndContinue_ReproducesTheUninterruptedEventStream` (3 cases, real session, one with Co-60 summed events), `Completed_RaisedPreset_ContinuesExactly`, `Continue_IsRejectedWhileRunning`; `OpticsViewModelTests.Focus_AcquiringStoppedCompleted_KeepsEventsAndSpectrum_OpticsLockedWithData` (one acquisition id across Continue) | pass |
| SR-RUN-24 | unit | T | `AcquisitionViewModelTests.Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset`, `LiveTimeAndSpeed_LockedWhileAcquiring_InvalidValuesFallBack` | pass |
| SR-RUN-25 | integration + unit | T | `AcquisitionContinuationTests.Seed_FixedReproduces_OtherSeedIsAnIndependentAcquisition`; `AcquisitionViewModelTests.Reset_DiscardsData_UnlocksInputs_NextStartIsANewAcquisitionWithANewSeed` | pass |
| SR-RUN-26 | unit + offscreen | T, I | `AcquisitionViewModelTests.Reset_DiscardsData_UnlocksInputs_NextStartIsANewAcquisitionWithANewSeed`; `DetectorWorkspaceTests.Face_FollowsPendingBeforeStart_AcquiredAndLockedAfter_PendingAgainAfterReset`; `FocusSweepViewModelTests.Sweep_RejectsLateResult_WhenIdentityChanges` ("reset"); Reset button placement in the renders (I) | pass headless; desktop pending |
| SR-RUN-27 | unit | T | `AcquisitionViewModelTests.Failure_WithData_KeepsItLocked_ResetOnly`, `Failure_WithoutData_BehavesAsEmpty` | pass |
| SR-RUN-28 | unit + offscreen | T, I | `AcquisitionViewModelTests.Start_SnapshotsGrow_StopKeepsDataAndLocks_ContinueAccumulates` (status rate = counts / live time); status bar and Detector panel agree in the renders (84 vs 83.6 cps) | pass |
| SR-RUN-29 | integration + unit + offscreen | T, I | `AmbientViewModelTests.DefaultIsValidatedFrontOnlyField_ZeroIsIdeal_SourceFreeStartRequiresAbsoluteField`, `PresetListOffersOnlyTheValidatedSpectrum_ByPinnedReference`, `Start_DefaultPassesValidatedPresetReference_ZeroTakesTheLegacyPath`, `AmbientPhysicalInputsLockAndDerivedBsrHasNoSetter`, `InvalidAmbientDoseIsRefused_PreviousValueStays`, `DoseTextOutsideThePattern_IsRefused_PreviousValueStays_StartBlocked` (11 entries), `DoseTextMatchingThePattern_SetsTheDoseRate` (6 entries), `DoseText_FollowsProgrammaticValue_AndLocksWithData`; `AmbientPresetTests.Pin_EqualsRepositorySidecar_AndDeployedBytesAreTheRepositoryFile`, `BuildConfig_ResolvesTheValidatedSpectrum_OnTheFrozenCopyOnly`, `WrongPinOrMissingFile_IsRefusedAtStart`, `DefaultField_AcquiresWithTheValidatedSpectrum`; `AmbientAcquisitionTests.BackgroundOnly_StopContinueRetainsExactlyTheFixedTimeStream`, `EmptyZeroField_CompletesAndBuildConfigFreezesInputs`, `BackgroundOnly_WorkspacesShowCountsWithoutInventingIsotopeLines`; offscreen panel in the default and refused states (`ambient-inputs-*`, `ambient-inputs-invalid-*`) from `PlotViewRenderTests.Spectrum_BothThemesFullAndZoom_RenderWithoutWindow` (opt-in) inspected | pass headless; offscreen PNGs inspected from a run past AN-15; desktop pending |
| SR-SCENE-01 | unit | T | `MainViewModelTests.SourceItem_ClampsValues_LabelFollowsEdits`, `MainViewModelTests.IsotopePicker_OffersIr192_AndKeepsCs137AsTheDefault` | pass |
| SR-SCENE-02 | unit | T | `MainViewModelTests.Startup_HasOneSelectedSource`, `MainViewModelTests.AddRemove_SelectsNewSourceThenNeighbour` | pass |
| SR-VIEW-01 | unit | T | `HeatmapViewportTests.Fit_PreservesAspectAndCentres` | pass |
| SR-VIEW-02 | unit | T, I | `HeatmapViewportTests.Snapping_FitsWholeDevicePixelsPerCell` (100 %, 125 %, sub-pixel); `HeatmapView` passes `PixelsPerDip` and `snapToWholePixels: true` (I) | pass |
| SR-VIEW-03 | unit | T | `HeatmapViewportTests.ZoomAt_KeepsAnchorPointFixed`, `HeatmapViewportTests.ZoomAt_IsClamped_FullZoomOutRefits` | pass |
| SR-VIEW-04 | unit | T | `HeatmapViewportTests.PanBy_CannotDragImageOutOfView` | pass |
| SR-VIEW-05 | unit + integration | T, I | `HeatmapViewportTests.ScreenImage_RoundTripWithYUp`, `HeatmapViewportTests.PixelAt_RespectsBoundsAndOrientation`, `HeatmapViewportTests.MmMapping_PutsPixelCentresOnGrid`; flood origin `-(N−1)/2·pitch` in `AcquisitionSession` (I), pinned by `AcquisitionServiceTests.FloodAxis_MatchesTheDecodersPixelCentres` | pass |
| SR-VIEW-06 | unit | T | `HeatmapViewportTests.Configure_ResizeKeepsZoom_NewImageSizeRefits` | pass |
| SR-VIEW-07 | system + unit | T, I, M | readout format and key handling in `HeatmapView` (I); value format `TickFormatterTests.Significant_FourDigitsGroupedWithoutTrailingZeros` (7 cases); readout exposed via `ItemStatus`; UI scenario `ScenarioTests.Readout_AndOneCellRoi_MatchAbsolutePositionAndValue` (§5) | partial — readout automated; the refit keys (double-click, `0`, Home) by inspection only |
| SR-VIEW-08 | unit | T | `MainViewModelTests.Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsKeepState`; `ImagingWorkspaceTests.PeakChip_CountsSeveralFoundPeaks_AndNamesASingleOne` | pass |
| SR-VIEW-09 | offscreen | I | colour bar and readout at the drawn image width (flood, reconstruction, Detector face) inspected in the 1280×800 / 1440×900 renders | pass offscreen; desktop deferred |
| SR-MEAS-01 | unit + integration | T, M | `MeasurementMathTests.Distance_IsEuclidean`, `MeasurementsViewModelTests.Add_NumbersSequentially_AndSelectsTheNewOne`; VAL-03 (12.3 mm) | pass |
| SR-MEAS-02 | unit | T, M | `MeasurementMathTests.AngleDeg_MeasuresAtTheVertex`, `MeasurementMathTests.AngleDeg_DegenerateArm_IsZero`; VAL-03 (67.0°) | pass |
| SR-MEAS-03 | unit | T, M | `MeasurementMathTests.Roi_CountsPixelsByCentre_CornersInAnyOrder`, `MeasurementMathTests.Roi_ClipsToTheImage_AndIsEmptyBetweenCentres`; VAL-03 (ROI on flood) | pass |
| SR-MEAS-04 | unit + integration | T | `MeasurementsViewModelTests.Roi_HasNoValueBeforeARun_AndFollowsEachNewResult`, `MeasurementsViewModelTests.Roi_OnAPaneWithoutData_StaysEmpty`, `MeasurementsViewModelTests.Roi_WithNoPixelCentreInside_HasNoValue`, `MainViewModelTests.NewResult_RefreshesMeasurements` | pass |
| SR-MEAS-05 | unit | T | `MeasurementsViewModelTests.Add_NumbersSequentially_AndSelectsTheNewOne`, `MeasurementsViewModelTests.Delete_SelectsTheNeighbour_ClearRestartsNumbering`, `MeasurementsViewModelTests.Add_WrongNumberOfPoints_Throws` | pass |
| SR-MEAS-06 | unit | T | `MeasurementsViewModelTests.ToolHint_FollowsTheActiveTool` | pass |
| SR-MEAS-07 | system | I, M | `MeasurementAdorner` (`MinDragPx = 4`, Esc / right-click / Delete handlers, `Clamp`); VAL-03 (Delete, Esc) | partial — 4 px threshold, right-click and clamping by I only |
| SR-MEAS-08 | removed | — | Studio sets `CanMoveMarkers = False`; drag scenario rewritten | withdrawn → SR-RUN-12 |
| SR-THEME-01 | unit | T | `MainViewModelTests.ThemeToggle_FlipsThemeAndRelabels` (with `FakeTheme`) | pass |
| SR-THEME-02 | system | I, M | `ThemeService.Apply` replaces in place (I); both-theme visual check | partial — no dated manual record |
| SR-ENV-01 | build | C, I | `Gcam.Studio.csproj` targets `net9.0-windows`, the other two `net9.0` (C); DWM return values ignored in `ThemeService.ApplyTitleBar` (I) | pass — run on Windows 11 only |
| SR-ENV-02 | system | I, M | `App.OnStartup` maximises when `SystemParameters.WorkArea` is smaller than the window (I); VAL-08 | partial — VAL-08 open |
| SR-A11Y-01 | system | I, M | 30 `AutomationProperties.Name` in `MainWindow.xaml`, row name bound to `Description` (I); controls located by name in the VAL-03 session | pass |
| SR-A11Y-02 | system | I, M | `HeatmapViewAutomationPeer` (I); heatmaps located by name + class `HeatmapView` in the VAL-03 session | pass |
| SR-A11Y-03 | system | I, M | `IsDefault="True"` on Simulate, `HeatmapView.OnKeyDown`, `_Pan` / `_Distance` / `_Angle` / `_ROI` (I) | partial — keyboard-only walkthrough open; AN-01 |
| SR-A11Y-04 | system | I | `LiveSetting="Polite"` on the status line; selected row = accent bar + fill; selected overlay = thicker line + inverted chip | pass (I); no screen-reader check |
| SR-A11Y-05 | unit + offscreen | T, I | `ThemeContrastTests` (8 cases against production tokens); acquired-seed foreground/fill assertions; both themes' 1280×800 renders inspected | pass: Surface 5.164:1 dark / 5.154:1 light; Canvas/Raised ≥4.5:1; desktop follow-up pending |
| SR-SEC-01 | inspection | I | no `System.IO`, `File`, HTTP or socket use in the three Studio projects; `SimulationService` passes an in-memory config (I, 2026-10-01) | pass (I) |
| SR-ARCH-01 | build | C | `Gcam.Studio.Core.csproj` targets plain `net9.0` without `UseWPF`: a WPF type does not compile | pass |
| SR-ARCH-02 | build | C, I | Core has no reference to `Gcam.Simulation` (C); the shell could reach it transitively through Services, so for the shell it is I only | pass — see AN-08 |
| SR-ARCH-03 | inspection | I | `MainWindow.xaml.cs` is `InitializeComponent()` only; cited literal sizes replaced by theme metrics — AN-05 closed | pass by inspection |
| SR-ARCH-04 | build | C, I | `Gcam.Studio.Tests.csproj` references only `Gcam.Studio.Core` and targets `net9.0` | pass |
| SR-NAV-01 | unit + I | T, I | `MainViewModelTests.Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsKeepState`; workspace-type centre / panel templates | pass |
| SR-NAV-02 | unit + system | T, I | workspace tests, `WorkspaceActivation_SelectsShell_WhenActiveIsSetWithoutCommand` and `Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives`: checked-state activation, identities, unavailable index and retained selection; segmented switch / Ctrl+1…4 inspected | partial — desktop SelectionItem switching pending |
| SR-NAV-03 | unit | T | `AcquisitionViewModelTests.Start_CapturesDetectorInputs_AndLocksThemWhileDataExist` (physical writers refused, window N still applied), `Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsKeepState` | pass |
| SR-PLOT-01 | unit + I | T, I | `PlotSeriesTests.LowerBound_HandlesUniformAndIrregularX_AndEnds`; validation / cap / band / marker properties inspected | pass |
| SR-PLOT-02 | unit | T | `PlotViewportTests.Mapping_RoundTrips_AndClampsLogFloor`, `NiceTicksTests.Linear_Uses125Steps_EngineeringLabels_NoNegativeZero`, `Logarithmic_LabelsDecades_WithEightMinorTicks`, `TickFormatterTests.StepLabels_TakeDecimalsFromTheStep_AndGroupThousands` | pass |
| SR-PLOT-03 | unit | T | `MinMaxPyramidTests.Query_EqualsBruteForce_IncludingBothEnds` (5 cases), `Range_ClipsOutsideData_RejectsNonFiniteSamples` | pass |
| SR-PLOT-04 | unit + system | T, I | `PlotViewportTests.ZoomPanReset_PreservesAnchor_AndBounds`; `PlotViewTests.TenMillionSamples_ZoomAndResizeRedraw_Within16Milliseconds` checks production peer, + key and reset; pointer / other keys / readout by inspection | partial — full input walkthrough pending |
| SR-PLOT-05 | system | T | `PlotViewTests.TenMillionSamples_ZoomAndResizeRedraw_Within16Milliseconds`; measured record in §5 | pass (CPU redraw only) |
| SR-PLOT-06 | unit + offscreen render | T | `HistogramPlotTests.Steps_IncludePartialBins_AndEmptyBinHasFullWidth`, `Envelope_RetainsExtremaOfBinsCrossingColumns`, histogram validation; `PlotViewRenderTests` PNGs | pass headless |
| SR-PLOT-07 | unit + offscreen render | T | `Configure_PreservesZoomAndPanUntilRangeChanges_AndResetStillFits`; acquisition snapshot VM test; render test replaces counts under zoom | pass headless |
| SR-PLOT-08 | unit + offscreen render | T | `VisibleExtent_ExcludesDistantPeak_IncludesIntersectingBins`; render test checks live top retention | pass headless |
| SR-PLOT-09 | unit + inspection | T, I | `BinLookup_UsesHalfOpenEdges_AndReadoutUsesCountsAndUnits`; `PlotView.UpdateHover` and drawing inspected | pass headless; desktop pointer walkthrough deferred |
| SR-PLOT-10 | unit + offscreen render | T, I | `Labels_ClampBothEdges_AndUseAdditionalRowsWithoutCollisions`; Spectrum, Waveform and focus-curve renders show band / marker labels in the strip above the data, the shaped waveform plot without marker labels | pass headless |
| SR-PLOT-11 | offscreen render | T | `PlotViewRenderTests.Spectrum_BothThemesFullAndZoom_RenderWithoutWindow`, independent opt-in, four PNGs | pass |
| SR-SPEC-01 | integration + I | T, I | `SpectrumServiceTests.CsAcquisition_PhotopeakBinAndFwhmMatchChain`, `Axis_IsFixedAt2000keV_ForAnyLinesFieldOrPileUp_AndOverflowKeepsEveryPulse`; `MainViewModelTests.SpectrumSummary_NamesTheOverflowAndTheFixedAxisEnd`; Histogram binding / explicit edges, axis labels and no synthetic noise inspected | pass headless; retained four-workspace desktop survey; no pixel pass/fail oracle |
| SR-SPEC-02 | integration | T | `CsAcquisition_PhotopeakBinAndFwhmMatchChain`; engine `FrontEndPartsTests` verifies legacy chain and pulse compatibility | pass |
| SR-SPEC-03 | integration | T | `HighRate_PileUpLosesPulsesAndMovesCountsAbovePhotopeak`; exact pulse-count agreement with engine `ApplyPileUp` | pass |
| SR-SPEC-04 | integration + unit | T | `MixedCsCo_HasFourBandsAndUnionCountsEachBinOnce`, `Merge_UsesResolutionRatherThanWindowOverlap` (2 cases), `Merge_SingleLineGivesOneBand` | pass |
| SR-SPEC-05 | integration + unit + offscreen | T, I | `MixedCsCo_HasFourBandsAndUnionCountsEachBinOnce`; `SpectrumBandTests.BandOfXRayLines_IsNamedByEmitter_GammaBandByIsotope`; engine `EmissionKindTests` (3 cases); `VerifyEmissionTable` asserts shared header/row column edges, full strings and ≥8-DIP gaps, including both Co-60 windows and grouped counts at both sizes/themes | pass headless/offscreen; updated table desktop pending |
| SR-SPEC-06 | unit + I | T, I | `AcquisitionViewModelTests.Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives` (acquisition and its state unchanged) | pass headless and final desktop band/window scenario |
| SR-SPEC-07 | integration + I | T, I | `SeedAndSnapshotPartition_AreDeterministic_ToggleReplaysExactly` (2 cases), `Processing_MeasuresFullAndIncrementalWorkAt100000Events`; service `Task.Run` inspected | pass |
| SR-SPEC-08 | inspection + system | I | `SpectrumView.xaml`, `SpectrumPanel.xaml`, inherited `PlotView` peer | partial — both themes surveyed; screen-reader/keyboard-only walkthrough pending |
| SR-SPEC-09 | unit + integration + inspection | T, I | acquisition VM test checks range request / selection persistence; spectrum service verifies every bin edge / centre; XAML one-way range / units inspected | pass headless; desktop selection walkthrough deferred |

### Coverage summary

The tables below preserve the pre-acquisition baseline (47 rows); withdrawn RUN rows are historical evidence.
The acquisition, spectrum and additional plot rows are verified above by tests / inspection and retained offscreen rendering. The final four-workspace desktop pass below adds current acquisition and workspace-path evidence; keyboard-only, screen-reader and SelectionItem coverage remain incomplete;
the prior batch desktop passes must not be treated as acquisition validation.

| Primary method | Requirements |
|---|---|
| Automated test (alone or with I / M) | 33 — NAV 01–03, PLOT 01–05, RUN 01–08, SCENE 01–02, VIEW 01–06 + 08, MEAS 01–06 + 08, THEME-01 |
| Manual UI Automation + inspection | 7 — VIEW-07, MEAS-07, THEME-02, ENV-02, A11Y-01, A11Y-02, A11Y-03 |
| Inspection only | 3 — A11Y-04, SEC-01, ARCH-03 |
| Compiler-enforced (+ inspection) | 4 — ENV-01, ARCH-01, ARCH-02, ARCH-04 |

| Status | Count |
|---|---|
| pass | 38 |
| partial | 9 — NAV-02, PLOT-04, RUN-04, VIEW-07, MEAS-07, THEME-02, ENV-02, A11Y-03, ARCH-03 |
| open | 0 requirements; system-level scenarios are open in §5 |

### Spectrum physics and processing evidence

`SpectrumServiceTests` acquires fresh real-engine events with transport seed 12345 and processing seed 909.
The default chain is GAGG(Ce) / Hamamatsu MPPC S13360-3050 / CSP + CR-RC, with 4.5692% FWHM at 662 keV.

Seeded values re-measured 2026-10-02 after the random generator was replaced (old generator: 31,053 deposits;
Ba ratios 0.480119 / 0.626183; FWHM 30.3436 keV; pile-up 85,108 / 4,735 / 456; union share 37.1900 %); every check
passes with its unchanged tolerance. The bin-dependent values (photopeak bin, FWHM, pile-up counts, union share) are
re-measured on the fixed 0–2000 keV axis of 2 keV bins (2026-10-04); the physics checks are unchanged.

| Check | Measurement | Acceptance / tolerance |
|---|---|---|
| Cs-137 photopeak, 100,000 events (31,300 full-energy deposits) | regional maximum bin 330, containing 661.7 keV; bin width 2.0000 keV | exact bin match, maximum above 478 keV through axis end |
| Ba K absorber response | uncollided ratio 0.474932 vs narrow-beam 0.474899; measured-band ratio 0.619148 vs independent weighted MC 0.619379 | 4σ tolerances 0.014987 and 0.028971; global peak ordering is reported rather than asserted; detailed conditions in [acquisition evidence](VV.Studio.Acquisition.md#detector-realism-and-background) |
| Cs photopeak FWHM | histogram 30.2798 keV vs `FrontEndModel` 30.2373 keV; error 0.0425 keV | ≤4.6043 keV = two bins + 5·FWHM/√(2(N−1)), N = full-energy deposits |
| High-rate Cs pile-up (activity 10⁶ µCi, 100,000 events) | 100,000 → 85,217 pulses; above 794.04 keV: 0 → 5,265; overflow 50; resolving time 730 ns | strictly fewer pulses and more counts above 1.2×661.7; pulse count exactly matches `ApplyPileUp` at 1 ps time rounding |
| Cs + Co, 20,000 events | four bands: 32.1 + 36.4, 661.7, 1173.2, 1332.5 keV; union share 37.0650% | exact labels / band count and union share; default Co windows need not overlap |
| Merge unit fixtures | Ba K merged; single line retained; injected constant 10% FWHM keeps 1173.2 / 1332.5 separate despite overlapping N=1.5 windows | exact band counts; separation 159.3 keV > FWHM(mean) 125.285 keV; fixture is not a physical-chain claim |
| Determinism, 10,000 events | batch, prefix 503 + remainder, and toggle replay agree with / without pile-up | exact histogram and overflow equality |
| `Axis_IsFixedAt2000keV_ForAnyLinesFieldOrPileUp_AndOverflowKeepsEveryPulse` | 1000 bins of 2 keV over 0–2000 keV for 3 line lists (Cs, Co, both) × 2 field maxima (none, 3960.9 keV) × pile-up off / on; 2505.7 and 3000 keV pulses in the overflow; total = drawn + overflow; histogram and 662 band identical with and without the field maximum | exact |

Worker elapsed time measured by `Stopwatch` inside `Task.Run` (five warmed Release runs; excludes queue / gate
waiting, includes worker preemption). No hard timing tolerance is imposed on this measurement.

| Work | Five measurements (ms) |
|---|---|
| Smear + bin full 100,000-event snapshot | 102.166, 176.062, 101.702, 116.910, 101.117 |
| Increment 1,000 events over cached 99,000 | 1.267, 1.054, 1.034, 1.323, 1.065 |
| Pile-up toggle, replay 100,000 events | 135.560, 102.186, 102.762, 112.295, 101.615 |

Full processing exceeds a few ms, so it stays on the service worker. This is processing evidence, not a
desktop responsiveness measurement. Once the desktop and UI-test project are free, add live-count growth /
Stop retention, Cs photopeak / Ba K and mixed four-band display, setting reuse, workspace switching
and selection retention, plot keyboard / readout / focus, accessible control and table names, and both-theme checks.

## 5. Validation

User-level scenarios run on the real app. "Performed" means driven through UI Automation with real pointer and key
input (`SetCursorPos` + `mouse_event`): controls are found by `AutomationId`, the run state is read from the status
line's `ItemStatus`, and each verdict is judged on the app's own state against a value computed independently of the
app's code (an oracle that re-derives the image layout and the screen → mm mapping from the documented rules).

| ID | Scenario | Acceptance criteria | Status |
|---|---|---|---|
| VAL-01 | Start the default scene and read the result | Completed status, flood and reconstruction present, counts / live time shown | Performed 2026-10-02: pilot passes |
| VAL-02 | Stop, Continue and Reset an acquisition | Stop freezes counts and retains locked data; Continue adds counts under one acquisition; Reset discards and unlocks | Performed 2026-10-02: rewritten acquisition scenario passes |
| VAL-03 | Measure on both images | distance and ROI drawn on the flood map, angle on the reconstruction; values appear in "Measurement results" and on the image; Delete removes the selected one; Esc abandons a half-drawn angle | **performed 2026-10-01** — by hand: distance 12.3 mm and an ROI on the flood map, angle 67.0° on the reconstruction, Delete and Esc as specified; automated: three scenarios below |
| VAL-04 | Move a source after an acquisition | Data lock fields / markers; Reset unlocks; edit X / Y and Start; found peak follows the new position | Performed 2026-10-02: reset/edit/start scenario passes |
| VAL-05 | Locate a source and measure its offset from the decoded peak | Distance tool from the source marker to the peak gives the offset in mm; it agrees with the peak chip and source X / Y to within one recon step | **partial 2026-10-01** — automated: after moving the source and re-acquiring, the decoded peak lay within 1.5 mm of the source; the Distance-tool step is not automated |
| VAL-06 | Use both themes | Text, chips, plots and overlays inspected in all four workspaces at two sizes | Performed 2026-10-02: 16-frame desktop survey; faint locked inputs recorded; keyboard-focus walkthrough excluded |
| VAL-07 | Keyboard only | every function reachable without a pointer except creating measurements (AN-01) | open |
| VAL-08 | Small screen and display scaling | at 1366×768 the window starts maximised; at 125 % / 150 % heatmap cells stay equal width | open |

### Plot performance gate

2026-10-01, DESKTOP-4D9CRJT, Windows 11 build 26200, .NET SDK 9.0.311 / runtime 9.0.13, Release,
100% DPI, 10,000,000 samples, visible 1280×800 host resized to 1440 width. The tool shell reports 24 processors;
`DOTNET_PROCESSOR_COUNT=4` constrained build / test runtime concurrency. Three warm-up zoom redraws preceded
five measured zoom / resize pairs. Data preparation is outside the interactive redraw measurement.

| CPU redraw | Five measurements (ms) | Maximum | Gate |
|---|---|---|---|
| Zoom | 2.0368, 12.1177, 3.2046, 1.0483, 1.3064 | 12.118 ms | ≤16 ms: pass |
| Resize | 2.6021, 2.6420, 0.7743, 0.7808, 0.6819 | 2.642 ms | ≤16 ms: pass |

CPU `OnRender` includes axes / ticks, exact extrema query, frozen geometry and WPF drawing commands.
It excludes dispatcher scheduling and compositor presentation. Event-to-render resize latency was
280.517…315.842 ms; this is **not** evidence of a ≤16 ms end-to-end resize. Full record:
[performance.json](assets/studio-polish-survey/performance.json). No data were removed or extrema dropped.
The host test also checks the production automation peer and keyboard + / reset.

### Latest desktop regression and polish survey attempt

The opt-in Release run on 2026-10-01 returned **11 pass / 8 fail / 0 skipped**: 10 oracle cases and the plot gate
passed; all seven existing scenarios and the survey failed. The pilot reached Succeeded and found the flood map,
then `SetCursorPos` failed. The remaining scenarios failed at UIA `SetFocus` while normalising the owned window.
Screen-copy diagnostics also failed (invalid handle). Existing assertions were not weakened. All owned app
processes exited with 0 and their sandbox-write lists were empty. This does not establish regression success.

Failure manifests / tree dumps are under the ignored test output `ui-runs/20261001-154607-7bf65f/` through
`ui-runs/20261001-154654-dfd8b1/`. The dated earlier scenario results below are historical evidence.
The four requested Imaging captures and observed polish issue list are **pending**, as is a keyboard walkthrough;
[survey record and intended paths](assets/studio-polish-survey/README.md). No screenshot or issue is invented.

### Automated scenarios (`tests/Gcam.Studio.UiTests`, opt-in with `GCAM_UI_TESTS=1`)

They move the real mouse, so they run only on a free interactive desktop; in an ordinary `dotnet test` and in CI
they are skipped and reported as skipped, not passed. Each scenario starts its own sandboxed app.

| Scenario | Traces to | Oracle (independent of the app) | Result 2026-10-01 |
|---|---|---|---|
| `PilotTests.Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength` | VAL-01, VAL-03 · SR-RUN-09, SR-MEAS-01 | length from screen points through the documented fit rule | 13.4 mm vs 13.400 ± 0.204 |
| `ScenarioTests.StopContinueReset_KeepsAccumulatesAndDiscards` | VAL-02 · SR-RUN-10, SR-RUN-12, SR-RUN-13, SR-RUN-23, SR-RUN-26 | state machine Acquiring → Stopped → Acquiring (Continue) → Stopped → Reset → Empty; counts frozen after Stop, growing after Continue, zero after Reset; inputs locked until Reset | pass 2026-10-02: frozen after Stop, accumulated after Continue, unlocked after Reset |
| `ScenarioTests.Roi_OnFloodMap_CountsWholePixelsByCentre` | VAL-03 · SR-MEAS-03 | pixel count by centre, corners on cell boundaries so one pixel of error cannot change it | 42 px, 3.6 × 4.2 mm |
| `ScenarioTests.Angle_EscAbandonsDraft_DeleteRemovesSelected` | VAL-03 · SR-MEAS-02, SR-MEAS-07 | angle from the three screen points; Esc is proven by the value | 69.8° vs 70.02 ± 2.29; Delete removed it |
| `ScenarioTests.MoveSource_ResetEditStart_PutsPeakOnTheSource` | VAL-04, VAL-05 · SR-RUN-12, SR-RUN-26, SR-VIEW-08 | physics: after Reset, an X / Y edit and a new acquisition the decoded peak must sit on the moved source (≤ 1.5 mm) | pass 2026-10-02: peak (21.6, 17.1), source (21.4, 16.0) mm; existing 1.5 mm tolerance |
| `ScenarioTests.Readout_AndOneCellRoi_MatchAbsolutePositionAndValue` | SR-VIEW-05, SR-VIEW-07, SR-MEAS-03 | absolute mm of two cells (both signs); a one-cell ROI must sum to the readout's value | (−6.3, 5.1) and (6.3, −4.5) mm exact; sum = cell value |
| `ScenarioTests.ThemeToggle_RelabelsAndSwitchesBack` | SR-THEME-01 | the label names the other theme; the app still simulates after two swaps | pass (the visual swap stays manual) |

Record (list-mode acquisition, 2026-10-01): all 19 UI tests passed (10 oracle, 7 desktop scenarios, the plot gate,
the polish survey); with every scenario's expectation deliberately corrupted (`GCAM_UI_BREAK_VERDICT=1`) all 7
desktop scenarios failed, each at its corrupted assertion, so the verdicts can fail. No app process or sandbox
folder was left behind. UIA Select on the workspace switch does not change the workspace (see anomalies).

## 6. Known anomalies, gaps and risks

### Anomalies and gaps

| ID | Finding | Effect | Plan |
|---|---|---|---|
| AN-01 | Measurements can only be **created** with the pointer (documented in [DESIGN.Controls](DESIGN.Controls.md#measurementadorner-via-measurementoverlay)) | keyboard users can review, select and delete, not create | keyboard crosshair |
| AN-02 | UI automation is desktop-only and opt-in | the twelve scenarios are not part of CI; keyboard-only flows and other display scaling remain unverified | heatmap navigation scenarios next |
| AN-03 | No high-contrast mode | Windows high-contrast themes are not honoured | planned with the keyboard crosshair |
| AN-04 | Closed: legacy batch progress race | unused batch progress implementation removed; acquisition uses sequential snapshots | batch regression removed with the code it guarded; acquisition progress verified by `AcquisitionViewModelTests.Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset` |
| AN-05 | Closed: the cited literal sizes in `MainWindow.xaml` (photon box `Width="120"`, readout `Height="18"`, `Margin="0,4,0,0"`, colour-bar `Margin="0,8,0,0"`) were replaced by theme metrics | original spacing defect retained here as history | current MainWindow uses named metric resources |
| AN-06 | Closed (2026-10-02): the "outdated" chip was removed with the stale state | physical inputs are locked while data exist, so images always match the inputs shown; the state is announced by the status line (live region) | — |
| AN-07 | WPF items (`HeatmapView` rendering, `ColorBar` ticks, `ThemeService`) have no unit tests, by design | defects show only when the app runs | covered by AN-02 plan |
| AN-08 | SDK-style project references are transitive, so the shell *could* call the engine directly | layering rule for the shell is inspection-only | optional: `PrivateAssets` on the Services → engine reference |
| AN-09 | *Closed* — the photon budget no longer exists (list-mode acquisition) | — | — |
| AN-10 | Workspace checked-state activation now selects the shell workspace; `WorkspaceActivation_SelectsShell_WhenActiveIsSetWithoutCommand` passes | screen-reader SelectionItem switching still needs desktop verification | ViewModel fix implemented; add a desktop Select regression and verify the displayed workspace, without relying on the checked state alone |
| AN-11 | The source-drag scenario localised at 1.43 mm against its 1.5 mm tolerance. **Cause found (2026-10-02):** with the default optics at a 1 m focal plane the detector samples the mask-cell shadow at 1.27 pixels per cell (0.761 mm shadow, 0.6 mm pixels), below Nyquist, so the decoded peak shifts with the source position (RMS 0.95 mm, max 1.91 mm over y = −10 … 10 mm; 0.24 / 0.41 mm at 3.8 samples per cell) — see VV.Studio.Imaging | source drag is withdrawn; the current scenario verifies Reset / field edit / new acquisition, while undersampling remains a physical limitation | use optics with ≥ 2 samples per cell at the working focal plane; retain the localisation tolerance |
| AN-12 | Closed: desktop input and screen capture available in the final pass (2026-10-02) | all scenarios, the plot gate and four-workspace survey executed | evidence below |
| AN-13 | CPU plot redraw meets the gate, but resize event-to-render delay is materially larger | CPU evidence does not establish end-to-end responsiveness | distinguish timings; investigate dispatcher / desktop latency before making a presentation-latency claim |
| AN-14 | `TickFormatterTests` (colour-bar tick labels: one shared multiplier, one decimal count, no negative zero) test behaviour that no SRS row states | the behaviour is verified but not required, so a change to it would not be traced | add an `SR-VIEW` row for colour-bar labels |
| AN-15 | The opt-in offscreen render test (`PlotViewRenderTests.Spectrum_BothThemesFullAndZoom_RenderWithoutWindow`) fails on one machine (2026-10-04) in its emission-table check ("Truncated table value: Window (keV)") before it reaches the later renders; it fails the same way at a commit before the ambient default and fixed axis, so it predates them (likely a font or display difference on that machine — not established) | on that machine the repository test does not complete SR-SPEC-05's table check or produce the renders after it; the ambient-panel PNGs (SR-RUN-29) came from a scratch copy that logged that assertion instead | find why the window column truncates there, then re-run the opt-in render test unchanged |

No screen-reader (Narrator / NVDA) session has been run. Not verified.

### Risk table (illustrative, IEC 62304 §7 style)

| Hazardous situation | Cause | Control | Verified by |
|---|---|---|---|
| Wrong position read off an image | y-flip or half-pixel error in screen ↔ mm | one mapping in `HeatmapViewport`; pixel-centre convention shared with the decoder | SR-VIEW-05 tests; VAL-03 |
| Old image taken as current | scene edited after acquisition | physical inputs locked while data exist; Reset discards the data before an edit; Stop keeps acquired data with state text | SR-RUN-10, SR-RUN-12, SR-RUN-26 tests; desktop acquisition validation pending |
| ROI sum from the wrong image or frame | measurement not refreshed, or pane mixed up | measurements stored per pane in mm; refreshed on every result | SR-MEAS-04 tests |
| False precision | sub-pixel ROI weighting | whole pixels by centre; values at 0.1 resolution | SR-MEAS-03 tests |
| App hangs or crashes on a long or failing acquisition | MC on the UI thread; unhandled exception | worker transport / decode; cooperative Stop; failure → `State = Failed` | SR-RUN-03, SR-RUN-09, SR-RUN-10 tests |
| Status missed by colour-blind or screen-reader users | colour-only cues | text next to every status dot; selection by shape; live status line | SR-A11Y-04 (I); AN-06 |

## 7. Configuration and regression

**Re-run verification**

```bash
dotnet build Gcam.sln -c Release                      # all projects incl. the WPF shell
dotnet test  Gcam.sln -c Release                      # engine + Studio
dotnet test  tests/Gcam.Studio.Tests -c Release       # Studio logic only, no UI stack needed
dotnet test  tests/Gcam.Studio.Services.Tests -c Release   # the service against the real engine
dotnet run   --project src/Gcam.Studio -c Release     # for the §5 scenarios
```

A verification record is valid for one working tree: note the commit, the SDK version and the pass counts when
the matrix in §4 is updated.

**Rules that keep this document true**

- Test names follow `Subject_ExpectedBehaviour[_Condition]`, one class per type under test, so a matrix row names
  one method.
- A change that adds or alters behaviour adds its requirement row ([SRS](VV.Studio.SRS.md)), its unit allocation
  ([SDS §7](VV.Studio.SDS.md#7-requirement-allocation)), its matrix row here and its test in the **same commit**; docs and code
  never drift apart.
- Renaming a test means updating its row here; a cited test that no longer exists is a defect in this document.
- Problems found during V&V go to §6 with an `AN-` ID and stay there until fixed.

## Dated verification records

The dated matrices and passes (historical test inventory 2026-10-01, Waveform and shared-chain matrix, Detector and
retained-flood focus matrix, final desktop verification, UI polish verification) are kept in
[VV.Studio.History](VV.Studio.History.md); the current state is this document.

