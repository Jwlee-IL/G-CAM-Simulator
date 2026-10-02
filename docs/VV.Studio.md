# VV.Studio — verification and validation of GCAM Studio

Scope: the three Studio projects (`Gcam.Studio.Core`, `Gcam.Studio.Services`, `Gcam.Studio`) and their tests.
Not covered: the simulation engine (verified by its own test project, `tests/Gcam.Tests`; the product-level
evidence is [VV.Gcam.Evidence](VV.Gcam.Evidence.md)) and the original code-behind viewer `src/Gcam.Wpf`. This document says **how we know Studio does what it must**; it is
one of a set:

| Document | IEC 62304 activity | Holds |
|---|---|---|
| [VV.Gcam.URS](VV.Gcam.URS.md) · [VV.Gcam.PRS](VV.Gcam.PRS.md) | system level, above §5.2 | user needs and product requirements for the whole Gcam product concept; Studio is its subsystem SS-4 |
| [VV.Studio.SRS](VV.Studio.SRS.md) | §5.2 requirements analysis | the requirements (`SR-*`), inputs / outputs, messages, risk-control requirements |
| [VV.Studio.SDS](VV.Studio.SDS.md) | §5.3 architecture, §5.4 detailed design | software items and units (`SU-*`), interfaces, SOUP, requirement allocation |
| VV.Studio (this page) | §5.5–5.7 verification, validation, §7 risk, §9 problems | traceability to tests, validation scenarios, anomalies, risk table |

The working design guides are [DESIGN.Architecture](DESIGN.Architecture.md) and the other `DESIGN.*` pages.

**At a glance**
- 74 active software requirements plus seven withdrawn batch rows (§4). Live acquisition verification and
  measurements are recorded in [VV.Studio.Acquisition](VV.Studio.Acquisition.md); desktop acquisition
  validation is pending while the desktop and UI-test project are occupied.
- Seven regression scenarios, a plot gate and a diagnostic survey are opt-in desktop tests. The latest regression / survey attempt was blocked by desktop input; the plot CPU gate passed (§5). Historical scenarios judge the running app against
  independently computed values (§5): VAL-01 … VAL-04 performed, VAL-05 partial, VAL-06 … VAL-08 open.
- The layering that keeps the logic testable is compiler-enforced (SR-ARCH-01, -04).
- Open problems are in §6 (AN-01 … AN-12); no screen-reader session has been run.

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
RMS/max 1.0810/1.9966 mm at 0.6 mm pitch and 0.5837/0.9511 mm at 0.3 mm pitch under
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
through `IAcquisitionService` and `ISpectrumService`. The engine inventory is recorded below;
`SpectrumServiceTests` additionally verifies the measured spectrum against real-engine deposits.

## 2. Software items

Items SI-1 … SI-4 and their units SU-01 … SU-22 are defined in [VV.Studio.SDS §2](VV.Studio.SDS.md#2-software-items-and-units-531-541).
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

The requirements are specified in [VV.Studio.SRS](VV.Studio.SRS.md) — **74 active** in fourteen groups, including
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
| SR-OPT-01, -02 | unit + offscreen | T, I | `OpticsPolicyTests.Presets_ApplyAtomically_MatchPhysicalFieldsAndShowCustom`; `OpticsViewModelTests.Focus_AcquiringStoppedCompleted_KeepsEventsSpectrumAndStaleState`; physical input bindings | pass headless + offscreen; desktop deferred |
| SR-OPT-03 | unit + integration | T | `OpticsPolicyTests` finite/text/cross-field cases; `OpticsViewModelTests.InvalidInput_StartExpandsSectionsAndDoesNotStartTransport`; service config validation | pass headless; desktop deferred |
| SR-OPT-04 | unit + integration | T | `OpticsProjectionTests.Refocus_ReprojectsAllChannels_WithoutMeasurementCalibrationOrNewEvents`, `EmptySnapshot_RefocusKeepsEmptyImagesAndDoesNotCalibrate`; `OpticsViewModelTests` lifecycle/revision/acquisition tests | pass headless; desktop deferred |
| SR-OPT-05 | unit + integration + offscreen | T, I | `OpticsPolicyTests.Geometry_UsesEffectiveFieldsAndHasNoPerformanceBand`; `OpticsProjectionTests.Sampling_SmallOffAxisSample_FinerPixelsReduceLocalizationError` (default); full position sweep (GCAM_EVIDENCE_TESTS=1); [sampling evidence](VV.Studio.Imaging.md); one-line caption with the figures as tooltip / help text (`Imaging.SamplingEvidence`) inspected in the renders | prior full sweep passed; revised default regression verification pending |
| SR-OPT-06 | offscreen | T, I | `PlotViewRenderTests.Spectrum_BothThemesFullAndZoom_RenderWithoutWindow`: expanded/collapsed optics at F=800, both themes, 1280×800/1440×900; asserts the default scene panel has no scrollable overflow in every workspace at both sizes; IDs Optics.*, Detector.Section, Chain.Section, Imaging.FocalPlane/Geometry/FocusNote, Imaging.FocalSection/ChannelSection/SweepSection; Tools and measurements under the images inspected | pass offscreen; desktop deferred |
| SR-IMG-01 | unit + integration | T | `ImagingWorkspaceTests.SharedWindow_SelectorStripAndRoi_ReuseFrozenAcquisition`; `ImagingServiceTests.WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing` | pass (headless); desktop pending |
| SR-IMG-02 | integration | T, I | `ImagingServiceTests`; shared `SpectrumService.BuildBands` and `MeasurementStage`; [imaging evidence](VV.Studio.Imaging.md) | pass (headless); desktop pending |
| SR-IMG-03 | unit + offscreen | T | `ImagingWorkspaceTests.SharedWindow_SelectorStripAndRoi_ReuseFrozenAcquisition`; two-isotope All / Cs-137 offscreen renders | pass (headless + offscreen); desktop pending |
| SR-IMG-04 | integration + offscreen | T | `ImagingServiceTests.Channels_OffAxisCsAndCo_LocalizeAtTheirOwnSource`, `Precision_TwentySeeds_MeasuresMixedIsotopesAndCoOnlyControl`; diamond overlay and coordinate list | pass (headless + offscreen); measured Co-60 bias in [imaging evidence](VV.Studio.Imaging.md); desktop pending |
| SR-IMG-05 | integration | T | `ImagingServiceTests.Strip_CoLocatedCsAndDoubleActivityCo_RecoversSameLiveTimeCsCount`, `WindowChange_ReplaysRetainedEvents_RecalibratesAndMatchesFreshProcessing` | pass (headless); desktop pending |
| SR-IMG-06 | unit + integration | T, I | `ImagingWorkspaceTests.LateResponse_CannotReplaceNewerWindowResult`; serialized `Task.Run` worker and separate stopwatches | pass (headless); desktop pending |
| SR-RUN-01 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-09, -19 |
| SR-RUN-02 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-10 |
| SR-RUN-03 | unit | T | `AcquisitionViewModelTests.Failure_ReportsMessage_KeepsAcquiredData` | pass |
| SR-RUN-04 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-11 |
| SR-RUN-05 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-12 |
| SR-RUN-06 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-13 |
| SR-RUN-07 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-14 |
| SR-RUN-08 | historical batch | — | batch implementation and batch-only tests removed; historical desktop evidence below | withdrawn → SR-RUN-15 |
| SR-RUN-09 | unit + integration | T, I | `AcquisitionViewModelTests.Preset_Completes_NewStartClears_ResultAndEvents`; `AcquisitionServiceTests.ShortAcquisition_LocalizesAfterCountThreshold_SnapshotsAreImmutable`; worker inspected | pass (headless); desktop pending |
| SR-RUN-10 | unit + integration | T | `AcquisitionViewModelTests.Start_SnapshotsGrow_StopKeepsData_UnlocksAndEditMarksStale`; `AcquisitionServiceTests.Stop_KeepsConsumedPrefix_AtExtremeMcLimitedSpeed` | pass (headless); desktop pending |
| SR-RUN-11 | unit + integration | T | `AcquisitionViewModelTests.Preset_Completes_NewStartClears_ResultAndEvents`; `AcquisitionServiceTests.InjectedClock_AdvancesLiveTimeAndPresetWithoutWallDelay` | pass |
| SR-RUN-12 | unit + inspection | T, I | acquisition ViewModel test (commands locked); source fields, `CanMoveMarkers`, live-time/speed `IsIdle` bindings inspected | pass (T/I); desktop pending |
| SR-RUN-13 | unit + inspection | T, I | acquisition test (disabled while active); `CanStart` requires sources (I); existing empty-scene test covers collection | pass (T/I) |
| SR-RUN-14 | unit | T | `AcquisitionViewModelTests.Start_SnapshotsGrow_StopKeepsData_UnlocksAndEditMarksStale`, `LiveTimeAndSpeed_MarkStale_WorkspaceAndMeasurementsDoNot` | pass |
| SR-RUN-15 | integration | T | existing nearest-prime / finite-grid builder tests; `AcquisitionServiceTests.InvalidInputs_FailBeforeStarting` (4 cases) | pass |
| SR-RUN-16 | engine + integration | T, I | `ListModeSourceTests` (weighted flood, spectrum, rate, exponential gaps, seed, no duplicates); service immutable / count conservation test | pass |
| SR-RUN-17 | integration + inspection | T, I | injected-clock test (250 ms); acquisition ViewModel test (ROI grows, old snapshot unchanged); fixed grid and decode per tick inspected; timings in acquisition record | pass (T/I); desktop pending |
| SR-RUN-18 | integration + unit | T | extreme MC-limited service test; virtual ViewModel test verifies note and retained prefix | pass |
| SR-RUN-19 | unit + integration | T | preset / restart ViewModel test; service preset completion test; defaults inspected | pass |
| SR-RUN-20 | integration + unit + inspection | T, I | `DetectorRealismTests.StudioDefaults_AreExplicit_AndSceneBuilderRemainsBare`; `MainViewModelTests.DetectorInputs_DefaultsAndStaleStateFollowEdits`; geometry fields and IsIdle binding inspected | pass headless; desktop fields pending |
| SR-RUN-21 | integration + unit | T | `DetectorRealismTests.Gain_WidensPhotopeakInQuadrature_AndReducesTightWindowAcceptance`, `Measurement_UsesFrozenInputs_AndReplaysAcrossSnapshotsAndPileUp`; `AcquisitionViewModelTests.Start_CapturesDetectorInputs_AndEditingMarksOnlyTheResultStale` | pass |
| SR-RUN-22 | engine + inspection | T, I | `ListModeBackgroundTests` verifies rate, spatial profile, energy response, exact disabled stream, seed and cancellation; BSR field and worker inspected | pass headless; desktop BSR input pending |
| SR-SCENE-01 | unit | T | `MainViewModelTests.SourceItem_ClampsValues_LabelFollowsEdits`, `MainViewModelTests.IsotopePicker_OffersIr192_AndKeepsCs137AsTheDefault` | pass |
| SR-SCENE-02 | unit | T | `MainViewModelTests.Startup_HasOneSelectedSource`, `MainViewModelTests.AddRemove_SelectsNewSourceThenNeighbour` | pass |
| SR-VIEW-01 | unit | T | `HeatmapViewportTests.Fit_PreservesAspectAndCentres` | pass |
| SR-VIEW-02 | unit | T, I | `HeatmapViewportTests.Snapping_FitsWholeDevicePixelsPerCell` (100 %, 125 %, sub-pixel); `HeatmapView` passes `PixelsPerDip` and `snapToWholePixels: true` (I) | pass |
| SR-VIEW-03 | unit | T | `HeatmapViewportTests.ZoomAt_KeepsAnchorPointFixed`, `HeatmapViewportTests.ZoomAt_IsClamped_FullZoomOutRefits` | pass |
| SR-VIEW-04 | unit | T | `HeatmapViewportTests.PanBy_CannotDragImageOutOfView` | pass |
| SR-VIEW-05 | unit + integration | T, I | `HeatmapViewportTests.ScreenImage_RoundTripWithYUp`, `HeatmapViewportTests.PixelAt_RespectsBoundsAndOrientation`, `HeatmapViewportTests.MmMapping_PutsPixelCentresOnGrid`; flood origin `-(N−1)/2·pitch` in `AcquisitionSession` (I), pinned by `AcquisitionServiceTests.FloodAxis_MatchesTheDecodersPixelCentres` | pass |
| SR-VIEW-06 | unit | T | `HeatmapViewportTests.Configure_ResizeKeepsZoom_NewImageSizeRefits` | pass |
| SR-VIEW-07 | system + unit | T, I, M | readout format and key handling in `HeatmapView` (I); value format `TickFormatterTests.Significant_FourDigitsGroupedWithoutTrailingZeros` (7 cases); readout exposed via `ItemStatus`; UI scenario `ScenarioTests.Readout_AndOneCellRoi_MatchAbsolutePositionAndValue` (§5) | partial — readout automated; the refit keys (double-click, `0`, Home) by inspection only |
| SR-VIEW-08 | unit | T | `MainViewModelTests.Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsDoNotMarkStale`; `ImagingWorkspaceTests.PeakChip_CountsSeveralFoundPeaks_AndNamesASingleOne` | pass |
| SR-VIEW-09 | offscreen | I | colour bar and readout at the drawn image width (flood, reconstruction, Detector face) inspected in the 1280×800 / 1440×900 renders | pass offscreen; desktop deferred |
| SR-MEAS-01 | unit + integration | T, M | `MeasurementMathTests.Distance_IsEuclidean`, `MeasurementsViewModelTests.Add_NumbersSequentially_AndSelectsTheNewOne`; VAL-03 (12.3 mm) | pass |
| SR-MEAS-02 | unit | T, M | `MeasurementMathTests.AngleDeg_MeasuresAtTheVertex`, `MeasurementMathTests.AngleDeg_DegenerateArm_IsZero`; VAL-03 (67.0°) | pass |
| SR-MEAS-03 | unit | T, M | `MeasurementMathTests.Roi_CountsPixelsByCentre_CornersInAnyOrder`, `MeasurementMathTests.Roi_ClipsToTheImage_AndIsEmptyBetweenCentres`; VAL-03 (ROI on flood) | pass |
| SR-MEAS-04 | unit + integration | T | `MeasurementsViewModelTests.Roi_HasNoValueBeforeARun_AndFollowsEachNewResult`, `MeasurementsViewModelTests.Roi_OnAPaneWithoutData_StaysEmpty`, `MeasurementsViewModelTests.Roi_WithNoPixelCentreInside_HasNoValue`, `MainViewModelTests.NewResult_RefreshesMeasurements` | pass |
| SR-MEAS-05 | unit | T | `MeasurementsViewModelTests.Add_NumbersSequentially_AndSelectsTheNewOne`, `MeasurementsViewModelTests.Delete_SelectsTheNeighbour_ClearRestartsNumbering`, `MeasurementsViewModelTests.Add_WrongNumberOfPoints_Throws` | pass |
| SR-MEAS-06 | unit | T | `MeasurementsViewModelTests.ToolHint_FollowsTheActiveTool` | pass |
| SR-MEAS-07 | system | I, M | `MeasurementAdorner` (`MinDragPx = 4`, Esc / right-click / Delete handlers, `Clamp`); VAL-03 (Delete, Esc) | partial — 4 px threshold, right-click and clamping by I only |
| SR-MEAS-08 | system + unit | I, M, T | `MeasurementAdorner` (`MarkerSnapDigits = 1`, `Clamp`, `HitTestCore` honours `CanMoveMarkers`); VAL-04 drag to (18.9, 16.1) mm; stale part by `AcquisitionViewModelTests.Start_SnapshotsGrow_StopKeepsData_UnlocksAndEditMarksStale` | pass |
| SR-THEME-01 | unit | T | `MainViewModelTests.ThemeToggle_FlipsThemeAndRelabels` (with `FakeTheme`) | pass |
| SR-THEME-02 | system | I, M | `ThemeService.Apply` replaces in place (I); both-theme visual check | partial — no dated manual record |
| SR-ENV-01 | build | C, I | `Gcam.Studio.csproj` targets `net9.0-windows`, the other two `net9.0` (C); DWM return values ignored in `ThemeService.ApplyTitleBar` (I) | pass — run on Windows 11 only |
| SR-ENV-02 | system | I, M | `App.OnStartup` maximises when `SystemParameters.WorkArea` is smaller than the window (I); VAL-08 | partial — VAL-08 open |
| SR-A11Y-01 | system | I, M | 30 `AutomationProperties.Name` in `MainWindow.xaml`, row name bound to `Description` (I); controls located by name in the VAL-03 session | pass |
| SR-A11Y-02 | system | I, M | `HeatmapViewAutomationPeer` (I); heatmaps located by name + class `HeatmapView` in the VAL-03 session | pass |
| SR-A11Y-03 | system | I, M | `IsDefault="True"` on Simulate, `HeatmapView.OnKeyDown`, `_Pan` / `_Distance` / `_Angle` / `_ROI` (I) | partial — keyboard-only walkthrough open; AN-01 |
| SR-A11Y-04 | system | I | `LiveSetting="Polite"` on the status line; selected row = accent bar + fill; selected overlay = thicker line + inverted chip | pass (I); no screen-reader check |
| SR-SEC-01 | inspection | I | no `System.IO`, `File`, HTTP or socket use in the three Studio projects; `SimulationService` passes an in-memory config (I, 2026-10-01) | pass (I) |
| SR-ARCH-01 | build | C | `Gcam.Studio.Core.csproj` targets plain `net9.0` without `UseWPF`: a WPF type does not compile | pass |
| SR-ARCH-02 | build | C, I | Core has no reference to `Gcam.Simulation` (C); the shell could reach it transitively through Services, so for the shell it is I only | pass — see AN-08 |
| SR-ARCH-03 | inspection | I | `MainWindow.xaml.cs` is `InitializeComponent()` only; literal sizes found — AN-05 | partial |
| SR-ARCH-04 | build | C, I | `Gcam.Studio.Tests.csproj` references only `Gcam.Studio.Core` and targets `net9.0` | pass |
| SR-NAV-01 | unit + I | T, I | `MainViewModelTests.Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsDoNotMarkStale`; workspace-type centre / panel templates | pass |
| SR-NAV-02 | unit + system | T, I | workspace tests, `WorkspaceActivation_SelectsShell_WhenActiveIsSetWithoutCommand` and `Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives`: checked-state activation, identities, unavailable index and retained selection; segmented switch / Ctrl+1…4 inspected | partial — desktop SelectionItem switching pending |
| SR-NAV-03 | unit | T | `AcquisitionViewModelTests.LiveTimeAndSpeed_MarkStale_WorkspaceAndMeasurementsDoNot`, `Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsDoNotMarkStale` | pass |
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
| SR-SPEC-01 | integration + I | T, I | `SpectrumServiceTests.CsAcquisition_PhotopeakBinAndFwhmMatchChain`; Histogram binding / explicit edges, axis labels and no synthetic noise inspected | pass headless; live desktop appearance pending |
| SR-SPEC-02 | integration | T | `CsAcquisition_PhotopeakBinAndFwhmMatchChain`; engine `FrontEndPartsTests` verifies legacy chain and pulse compatibility | pass |
| SR-SPEC-03 | integration | T | `HighRate_PileUpLosesPulsesAndMovesCountsAbovePhotopeak`; exact pulse-count agreement with engine `ApplyPileUp` | pass |
| SR-SPEC-04 | integration + unit | T | `MixedCsCo_HasFourBandsAndUnionCountsEachBinOnce`, `Merge_UsesResolutionRatherThanWindowOverlap` (2 cases), `Merge_SingleLineGivesOneBand` | pass |
| SR-SPEC-05 | integration + unit + I | T, I | `MixedCsCo_HasFourBandsAndUnionCountsEachBinOnce`; `SpectrumBandTests.BandOfXRayLines_IsNamedByEmitter_GammaBandByIsotope`; engine `EmissionKindTests` (3 cases); right-aligned columns and bin-centre selection inspected | pass headless; desktop table pending |
| SR-SPEC-06 | unit + I | T, I | `AcquisitionViewModelTests.Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives`; shared stale binding inspected | pass headless; desktop controls pending |
| SR-SPEC-07 | integration + I | T, I | `SeedAndSnapshotPartition_AreDeterministic_ToggleReplaysExactly` (2 cases), `Processing_MeasuresFullAndIncrementalWorkAt100000Events`; service `Task.Run` inspected | pass |
| SR-SPEC-08 | inspection + system | I | `SpectrumView.xaml`, `SpectrumPanel.xaml`, inherited `PlotView` peer | partial — desktop accessibility and themes pending |
| SR-SPEC-09 | unit + integration + inspection | T, I | acquisition VM test checks range request / selection persistence; spectrum service verifies every bin edge / centre; XAML one-way range / units inspected | pass headless; desktop selection walkthrough deferred |

### Coverage summary

The tables below preserve the pre-acquisition baseline (47 rows); withdrawn RUN rows are historical evidence.
The current fourteen acquisition rows, nine spectrum rows and six additional plot rows (PLOT-06 … -11) are verified above by tests / inspection and offscreen rendering. Their desktop paths are pending;
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

### Test inventory (run 2026-10-01, `dotnet test Gcam.sln -c Release`, .NET SDK 9.0.311)

| Class | Level | Cases | Result |
|---|---|---|---|
| `HeatmapViewportTests` | unit | 11 (9 methods, one theory × 3) | 11 / 11 pass |
| `MeasurementMathTests` | unit | 5 | 5 / 5 pass |
| `MeasurementsViewModelTests` | unit / integration | 7 | 7 / 7 pass |
| `MainViewModelTests` | unit / integration (with workspace and measurements) | 10 | 10 / 10 pass |
| `AcquisitionViewModelTests` | unit / integration with virtual acquisition clock | 6 | 6 / 6 pass |
| `TickFormatterTests` | unit | 9 | 9 / 9 pass — no requirement yet (AN-10) |
| `MinMaxPyramidTests` | unit | 19 | 19 / 19 pass (includes concurrent plot edge / storage cases) |
| `PlotViewportTests` | unit | 3 | 3 / 3 pass |
| `NiceTicksTests` | unit | 4 | 4 / 4 pass — colour-scale range and grouped log decades included |
| `PlotSeriesTests` | unit | 1 | 1 / 1 pass |
| `HistogramPlotTests` | unit / headless geometry timing | 9 | 9 / 9 pass |
| **Gcam.Studio.Tests** | | **84** | **84 / 84 pass** |
| `AcquisitionServiceTests` (in Gcam.Studio.Services.Tests) | integration, real engine + virtual scheduler | 8 | 8 / 8 pass |
| `SpectrumServiceTests` (in Gcam.Studio.Services.Tests) | physics / deterministic processing / timing and band grouping | 9 | 9 / 9 pass |
| `DetectorRealismTests` (in Gcam.Studio.Services.Tests) | physics / defaults / measurement replay / throughput | 6 | 6 / 6 pass; service project total 23 |
| `FloodOracleTests` (in Gcam.Studio.UiTests) | the UI scenarios' oracle, tested on its own | 10 | 10 / 10 pass |
| `PilotTests`, `ScenarioTests` (in Gcam.Studio.UiTests) | system, desktop (opt-in) | 7 | skipped normally; latest opt-in run: 7 fail at desktop input / focus (§5) |
| `PlotViewTests` | system, desktop (opt-in) | 1 | CPU redraw gate pass; skipped normally |
| `PolishSurveyTests` | diagnostic, desktop (opt-in) | 1 | blocked at UIA focus; skipped normally |
| `PlotViewRenderTests` (Gcam.Studio.RenderTests) | offscreen STA, independent opt-in | 1 | skipped normally; opt-in pass, four PNGs |
| `SceneConfigBuilderTests` (in Gcam.Tests) | integration (engine) | 7 (one theory × 4) | pass |
| `ListModeSourceTests` (engine) | physics / compatibility / throughput / decode measurements | 8 | 8 / 8 pass |
| `ListModeBackgroundTests` (engine) | physics / deterministic disabled and enabled background | 3 | 3 / 3 pass |
| `FrontEndPartsTests` (engine Configuration compatibility) | unit | 2 | 2 / 2 pass |
| **Gcam.Tests** | | **259** | **259 / 259 pass; previous 256 assertions unchanged** |

The solution, including the WPF shell and unchanged UI-test project, built in Release without errors.
NU1900 reports unreachable NuGet vulnerability metadata; verification used existing restore assets, one
MSBuild node and disabled node reuse. `dotnet test` prints the current totals.

Spectrum-plot verification on 2026-10-01 adds nine Core cases: inventory 365 -> 374 passing headless cases
(259 engine, 82 Core, 23 services, 10 UI oracles). Nine desktop cases and the separate render case skip normally;
the render opt-in passes independently. The prior 365 count is the pre-change inventory, not a fresh baseline
execution. Full solution Release build uses cached packages and one MSBuild node because network restore of the
legacy WPF project's floating ScottPlot version is unavailable. No desktop test was enabled and Studio was not
launched. Snapshot evidence: [studio-plot](assets/studio-plot/README.md).

UI polish batch 1 Release verification adds two `NiceTicksTests` cases: measured baseline 374 -> 376
passing headless cases (259 engine, 84 Core, 23 services, 10 UI oracles). Nine desktop cases and the
one render case skip in the normal solution run; the render opt-in passes separately. Release build
has zero warnings and errors. Cached packages, `--no-restore`, `--disable-build-servers` and one
MSBuild node are used; the legacy floating ScottPlot restore is unavailable on the restricted network.
The existing render case now creates eight [whole-window content snapshots](assets/studio-render/README.md)
at 1280 × 800 / 1440 × 900 in both themes, plus the four plot snapshots. It uses a fake acquisition
service and schematic analytic drawing data, never shows a window or sends desktop input.
The captures verify counts / decoded readout units, value-positioned colour ticks, centred short acquisition
inputs, the descriptive pile-up label, original-rig preset name, grouped log decades, Y-title separation
and neutral window bands with contrasted edges. They are visual review evidence, not desktop validation
or detector physics evidence. AutomationIds are unchanged; no desktop test was enabled or edited.

Core timing (Release, 1200 device columns, 100 warmed iterations): 10 M line geometry plus visible-range query
median 0.490 ms / p95 0.575 ms; zoomed 0.426 / 0.509 ms. Pyramid construction 28.335 ms. A 256-bin stepped
histogram takes 0.010 / 0.011 ms; a 10 M histogram envelope 0.513 / 0.567 ms. These measure Core query / point
construction, excluding WPF text, drawing commands and composition; the changed control's real-window 16 ms
gate is deferred to the author. `LineGeometry_PreservesSingletonsAndRightEndpoint` protects the existing line
geometry at sparse sample columns and the final endpoint.

Live-acquisition Release verification on 2026-10-01 passed 341 cases (254 engine + 69 Core + 8 service +
10 UI oracles), skipped all nine desktop cases and launched no Studio window. Zero build errors; NU1900
remains a restore warning. Measurements and pending desktop migration are in
[VV.Studio.Acquisition](VV.Studio.Acquisition.md).

Spectrum Release verification on 2026-10-01 passed 353 cases (256 engine + 70 Core + 17 service +
10 UI oracles), with all nine desktop cases skipped. Build included the unchanged UI-test project and had
zero errors and one NU1900 warning. Existing restore assets were used (`--no-restore`), one MSBuild node,
node reuse disabled; test runtime had `DOTNET_PROCESSOR_COUNT=4`. No Studio window was launched.

Detector-realism and background Release verification passed 365 cases (259 engine + 73 Core + 23 service +
10 UI oracles), with nine desktop cases skipped. Existing engine assertions are unchanged. The solution,
including the unchanged UI-test project, builds with zero errors and one NU1900 warning. Detector physics,
source/background preservation, fixed gain measurement, throughput and pending UI checks are in
[VV.Studio.Acquisition](VV.Studio.Acquisition.md#detector-realism-and-background).

### Spectrum physics and processing evidence

`SpectrumServiceTests` acquires fresh real-engine events with transport seed 12345 and processing seed 909.
The default chain is GAGG(Ce) / Hamamatsu MPPC S13360-3050 / CSP + CR-RC, with 4.5692% FWHM at 662 keV.

| Check | Measurement | Acceptance / tolerance |
|---|---|---|
| Cs-137 photopeak, 100,000 events (31,053 full-energy deposits) | regional maximum bin 222, containing 661.7 keV; bin width 2.9725 keV | exact bin match, maximum above 478 keV through axis end |
| Ba K absorber response | uncollided ratio 0.480119 vs narrow-beam 0.474422; measured-band ratio 0.626183 vs independent weighted MC 0.623270 | 4σ tolerances 0.014926 and 0.028881; global peak ordering is reported rather than asserted; detailed conditions in [acquisition evidence](VV.Studio.Acquisition.md#detector-realism-and-background) |
| Cs photopeak FWHM | histogram 30.3436 keV vs `FrontEndModel` 30.2373 keV; error 0.1063 keV | ≤6.5516 keV = two bins + 5·FWHM/√(2(N−1)), N = full-energy deposits |
| High-rate Cs pile-up (activity 10⁶ µCi, 100,000 events) | 100,000 → 85,108 pulses; above 794.04 keV: 0 → 4,735; overflow 456; resolving time 730 ns | strictly fewer pulses and more counts above 1.2×661.7; pulse count exactly matches `ApplyPileUp` at 1 ps time rounding |
| Cs + Co, 20,000 events | four bands: 32.1 + 36.4, 661.7, 1173.2, 1332.5 keV; union share 37.1900% | exact labels / band count and union share; default Co windows need not overlap |
| Merge unit fixtures | Ba K merged; single line retained; injected constant 10% FWHM keeps 1173.2 / 1332.5 separate despite overlapping N=1.5 windows | exact band counts; separation 159.3 keV > FWHM(mean) 125.285 keV; fixture is not a physical-chain claim |
| Determinism, 10,000 events | batch, prefix 503 + remainder, and toggle replay agree with / without pile-up | exact histogram and overflow equality |

Worker elapsed time measured by `Stopwatch` inside `Task.Run` (five warmed Release runs; excludes queue / gate
waiting, includes worker preemption). No hard timing tolerance is imposed on this measurement.

| Work | Five measurements (ms) |
|---|---|
| Smear + bin full 100,000-event snapshot | 102.166, 176.062, 101.702, 116.910, 101.117 |
| Increment 1,000 events over cached 99,000 | 1.267, 1.054, 1.034, 1.323, 1.065 |
| Pile-up toggle, replay 100,000 events | 135.560, 102.186, 102.762, 112.295, 101.615 |

Full processing exceeds a few ms, so it stays on the service worker. This is processing evidence, not a
desktop responsiveness measurement. Once the desktop and UI-test project are free, add live-count growth /
Stop retention, Cs photopeak / Ba K and mixed four-band display, setting reuse without stale, workspace switching
and selection retention, plot keyboard / readout / focus, accessible control and table names, and both-theme checks.

## 5. Validation

User-level scenarios run on the real app. "Performed" means driven through UI Automation with real pointer and key
input (`SetCursorPos` + `mouse_event`): controls are found by `AutomationId`, the run state is read from the status
line's `ItemStatus`, and each verdict is judged on the app's own state against a value computed independently of the
app's code (an oracle that re-derives the image layout and the screen → mm mapping from the documented rules).

| ID | Scenario | Acceptance criteria | Status |
|---|---|---|---|
| VAL-01 | Run the default scene and read the result | progress moves to 100 %; flood map and reconstruction appear with colour bars; peak chip shows mm; status line names counts and time; UI stays responsive | **performed 2026-10-01** (automated pilot: the run reaches Succeeded and the flood map has an image) |
| VAL-02 | Cancel a long run | Cancel appears only while running; images keep the previous result; status "Cancelled — previous result kept"; scene editable again | **performed 2026-10-01** (automated: scene locked while running, Cancelled, peak text unchanged, editable again) |
| VAL-03 | Measure on both images | distance and ROI drawn on the flood map, angle on the reconstruction; values appear in "Measurement results" and on the image; Delete removes the selected one; Esc abandons a half-drawn angle | **performed 2026-10-01** — by hand: distance 12.3 mm and an ROI on the flood map, angle 67.0° on the reconstruction, Delete and Esc as specified; automated: three scenarios below |
| VAL-04 | Move a source by dragging, then re-run | marker follows the pointer only with the Pan tool and when idle; X / Y fields show the snapped value; "outdated" chip appears; a re-run clears it | **performed 2026-10-01** — by hand: source dragged to (18.9, 16.1) mm on a 0.1 mm grid; automated: chip shown after the drag, cleared by the re-run |
| VAL-05 | Locate a source and measure its offset from the decoded peak | Distance tool from the source marker to the peak gives the offset in mm; it agrees with the peak chip and source X / Y to within one recon step | **partial 2026-10-01** — automated: after a drag and re-run the decoded peak lies within 1.5 mm of the source; the Distance-tool step is not automated |
| VAL-06 | Use both themes | all text, focus rings, chips and overlays legible in dark and light; title bar follows | open |
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
| `ScenarioTests.Stop_KeepsAcquiredData_LocksThenUnlocksScene` | VAL-02 · SR-RUN-10, SR-RUN-12, SR-RUN-14, SR-RUN-19 | state machine Acquiring → Stopped; counts frozen after Stop; Start clears | inputs locked; 35 counts kept and unchanged; editable again; restart began at 0 |
| `ScenarioTests.Roi_OnFloodMap_CountsWholePixelsByCentre` | VAL-03 · SR-MEAS-03 | pixel count by centre, corners on cell boundaries so one pixel of error cannot change it | 42 px, 3.6 × 4.2 mm |
| `ScenarioTests.Angle_EscAbandonsDraft_DeleteRemovesSelected` | VAL-03 · SR-MEAS-02, SR-MEAS-07 | angle from the three screen points; Esc is proven by the value | 69.8° vs 70.02 ± 2.29; Delete removed it |
| `ScenarioTests.SourceDrag_MarksOutdated_RerunPutsPeakOnTheSource` | VAL-04, VAL-05 · SR-MEAS-08, SR-RUN-14, SR-VIEW-08 | physics: after the re-run the decoded peak must sit on the moved source (≤ 1.5 mm) | 60 s list-mode: 1.43 mm (source (21.4, 16.0), peak (21.7, 17.4)) — near the tolerance; chip shown, then cleared |
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
| AN-02 | UI automation is desktop-only and opt-in | the seven scenarios of §5 are not part of CI; zoom and pan, the refit keys, drag-snapping limits and the visual theme swap are still checked by inspection or by hand | heatmap navigation scenarios next |
| AN-03 | No high-contrast mode | Windows high-contrast themes are not honoured | planned with the keyboard crosshair |
| AN-04 | Closed: legacy batch progress race | unused batch progress implementation removed; acquisition uses sequential snapshots | batch regression removed with the code it guarded; acquisition progress verified by `AcquisitionViewModelTests.Preset_Completes_NewStartClears_ResultAndEvents` |
| AN-05 | Literal sizes in `MainWindow.xaml` (photon box `Width="120"`, readout `Height="18"`, `Margin="0,4,0,0"`, colour-bar `Margin="0,8,0,0"`) despite the no-literals rule | cosmetic; spacing doesn't follow `Metrics.xaml` | move to named keys |
| AN-06 | The "outdated" chip is visual only (tooltip, not a live region) | a screen reader is not told that the images no longer match the scene | announce via the status line or a live region |
| AN-07 | WPF items (`HeatmapView` rendering, `ColorBar` ticks, `ThemeService`) have no unit tests, by design | defects show only when the app runs | covered by AN-02 plan |
| AN-08 | SDK-style project references are transitive, so the shell *could* call the engine directly | layering rule for the shell is inspection-only | optional: `PrivateAssets` on the Services → engine reference |
| AN-09 | *Closed* — the photon budget no longer exists (list-mode acquisition) | — | — |
| AN-10 | Workspace checked-state activation now selects the shell workspace; `WorkspaceActivation_SelectsShell_WhenActiveIsSetWithoutCommand` passes | screen-reader SelectionItem switching still needs desktop verification | ViewModel fix implemented; add a desktop Select regression and verify the displayed workspace, without relying on the checked state alone |
| AN-11 | The source-drag scenario localised at 1.43 mm against its 1.5 mm tolerance. **Cause found (2026-10-02):** with the default optics at a 1 m focal plane the detector samples the mask-cell shadow at 1.27 pixels per cell (0.761 mm shadow, 0.6 mm pixels), below Nyquist, so the decoded peak shifts with the source position (RMS 0.95 mm, max 1.91 mm over y = −10 … 10 mm; 0.24 / 0.41 mm at 3.8 samples per cell) — see VV.Studio.Imaging | the scenario passes or fails depending on where the source is dropped | optics with ≥ 2 samples per cell at the working focal plane (TODO-09 presets); do not widen the tolerance |
| AN-12 | Latest desktop input and screen capture unavailable | regression scenarios and four-view polish survey remain unverified in this working tree | re-run on an accessible interactive desktop; preserve verdicts |
| AN-12 | CPU plot redraw meets the gate, but resize event-to-render delay is 280–316 ms | CPU evidence does not establish end-to-end responsiveness | distinguish timings; investigate dispatcher / desktop latency before making a presentation-latency claim |
| AN-10 | `TickFormatterTests` (colour-bar tick labels: one shared multiplier, one decimal count, no negative zero) test behaviour that no SRS row states | the behaviour is verified but not required, so a change to it would not be traced | add an `SR-VIEW` row for colour-bar labels |

No screen-reader (Narrator / NVDA) session has been run. Not verified.

### Risk table (illustrative, IEC 62304 §7 style)

| Hazardous situation | Cause | Control | Verified by |
|---|---|---|---|
| Wrong position read off an image | y-flip or half-pixel error in screen ↔ mm | one mapping in `HeatmapViewport`; pixel-centre convention shared with the decoder | SR-VIEW-05 tests; VAL-03 |
| Old image taken as current | scene edited after acquisition | stale flag and "outdated" chip; Start clears old data; Stop keeps acquired data with state text | SR-RUN-10, SR-RUN-14 tests; desktop acquisition validation pending |
| ROI sum from the wrong image or frame | measurement not refreshed, or pane mixed up | measurements stored per pane in mm; refreshed on every result | SR-MEAS-04 tests |
| False precision | sub-pixel ROI weighting, unrounded drags | whole pixels by centre; drags snapped to 0.1 mm; values at 0.1 resolution | SR-MEAS-03 tests; SR-MEAS-08 I + M |
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

## Waveform and shared-chain verification matrix (2026-10-02)

The Waveform workspace and physical-chain selection are implemented; execution status is **not verified in the implementer's sandbox**. This section supersedes earlier exclusion/read-only-chain descriptions and adds traceability without increasing the verified test totals until the independent run completes. Conditions, commands and expected render paths are in [VV.Studio.Waveform](VV.Studio.Waveform.md).

| Requirement | Automated verification | Current execution status |
|---|---|---|
| SR-CHAIN-01…03 | WaveformServiceTests chain/response tests; WaveformWorkspaceTests stale/frozen/disabled-edit tests; Evidence mixed-field calibration | Added, not run |
| SR-WAVE-01…02 | Origin-relative window/prehistory/association tests; next/latest/held-window VM tests; actual MC offscreen scope | Added, not run |
| SR-WAVE-03…05 | Rate-study immutability/determinism; shared response; zero-noise ideal tests; real/rate/ideal render labels | Added, not run |
| SR-WAVE-06 | Acquired readout and invalid-energy suppression tests; no CR-RC energy precision assertion | Added, not run |
| SR-WAVE-07 | ScopeWindow policies; empty window/noise/late timestamp tests; maximum-cap worker measurement (Evidence) | Added, not run |
| SR-WAVE-08 | Visible-only/reuse/latest-revision/new-acquisition/error VM tests; prepared-plot identity and two-way render navigation | Added, not run |
| Waveform UI, shared selector | Existing offscreen renderer extended to 12 MC-based scope images, two themes × two sizes × three modes | Generation pending; no desktop tests |

The initially launched Studio Release build completed with 0 warnings/errors before later test/document/final edits. Subsequent shell creation failed with access denied; a final build/test count, services-suite runtime and rendered PNG inspection must come from the independent verification run. Existing engine tests were not edited; no RTL/cocotb code was changed.

UI polish batch 3 (workspace layout, 2026-10-02) Release verification: 262 engine, 162 Core, 67 service and
10 UI-oracle cases pass (previous 259 / 151 / 67 / 10); seven service evidence, nine desktop and the render case
skip in the normal run, and the render opt-in passes separately. New cases: three engine `EmissionKindTests`
(Cs-137 32.1 / 36.4 keV are X-rays emitted by barium, "Ba K", every other line gamma without an X-ray origin, an energy–intensity pair converts to a gamma line; no
energy or intensity changed), the peak-chip, X-ray band name, step-tick, significant-digit and grouped-fraction
Core cases. The renders are now reproducible byte for byte (fixture-supplied waveform worker time; two
consecutive render runs gave identical PNGs) and use the window's own Display text formatting in every
workspace. The Waveform fixture's live time ends at its last event, and the imaging fixture reports each
channel's argmax as its estimate. The status-bar rate and the outdated indicators are unchanged in this batch.
Visual review evidence only; no desktop test was enabled. The desktop-test readout parser
(`FloodOracle.ParseReadout`) now accepts the unit suffix and grouped four-digit values, checked by the non-desktop
`FloodOracleTests.ReadoutAndSumParsers_ReadTheAppsFormats`.

## Detector and retained-flood focus verification matrix

| Requirement | Automated verification | Execution status |
|---|---|---|
| SR-DET-01 | DetectorWorkspaceTests.Gap_InvalidEditPreventsStart, Gap_RevalidatesAfterPitchChange_AndConvertsUnits; FocusSweepServiceTests.Config_RejectsGapAtServiceBoundary | Passed in Release, 2026-10-02 |
| SR-DET-02 | Face_FollowsPendingBeforeStart_AndAcquiredAfterStaleEdit; MainViewModelTests.Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsDoNotMarkStale | Passed in Release, 2026-10-02 |
| SR-DET-03 | DetectorFaceTests.Geometry_ExactGapCoverageAndArea; Face_GainsMatchMeasurementPattern; DetectorGapEvidenceTests | Deterministic tests passed; opt-in seed-spread evidence awaits environment-change authorization |
| SR-DET-04 | RenderDetectorAndFocus before/stale Detector, dark/light, two sizes | Generated and inspected 2026-10-02 (batch-3 layout pass) |
| SR-FOCUS-01 | FocusSweepServiceTests.Sweep_UsesRetainedFlood_WithoutEventsOrSceneTruth, Sweep_PreCancelledWorkerCannotPublish, Sweep_EmptyFloodIsUnresolved; Sweep_FreezesIdentity_UserK_AndNeverDelaysAcquisition; Sweep_DoesNotHoldSnapshotConsumption_AndLabelsFrozenPrefix | Passed in Release, 2026-10-02 |
| SR-FOCUS-02 | Planes_UniformInverseDistance_ExactBounds; Linking_UsesAngle_NotMillimetres_AndIsOneToOne; user-K identity test | Passed in Release, 2026-10-02 |
| SR-FOCUS-03 | Interval_InterpolatesRawHalfMaximum_WithoutNoiseInterpretation, Interval_CensorsSweepEdges, Interval_FlagsDisjointModes_AndBoundaryMaximum; resolved/censored render fixture | Deterministic tests passed; render requires opt-in authorization |
| SR-FOCUS-04 | Sweep_RejectsLateResult_WhenIdentityChanges; ImagingWorkspaceTests.FocusSweep_ChannelChangeCancelsAndRejectsLateResult; descriptive labels and frozen-prefix identity | Passed in Release, 2026-10-02 |
| SR-FOCUS-05 | ExternalRange_OnlyExplicitActionChangesFocus; PlotView marker assertions | Core tests passed; render requires opt-in authorization |

Exact conditions, tolerance derivation, limitations and reproducible commands are self-contained in
[VV.Studio.Detector](VV.Studio.Detector.md). The final ordinary suite passed with 259 engine, 147 Core,
67 service and 10 UI-oracle cases; seven service evidence, nine desktop and one render cases were skipped.
MC evidence and detached renders still require authorization to change process-local opt-in environment
variables. No new desktop validation is claimed.
