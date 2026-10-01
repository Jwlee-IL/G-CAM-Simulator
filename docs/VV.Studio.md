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
- 51 active software requirements plus seven withdrawn batch rows (§4). Live acquisition verification and
  measurements are recorded in [VV.Studio.Acquisition](VV.Studio.Acquisition.md); desktop acquisition
  validation is pending while the desktop and UI-test project are occupied.
- Seven regression scenarios, a plot gate and a diagnostic survey are opt-in desktop tests. The latest regression / survey attempt was blocked by desktop input; the plot CPU gate passed (§5). Historical scenarios judge the running app against
  independently computed values (§5): VAL-01 … VAL-04 performed, VAL-05 partial, VAL-06 … VAL-08 open.
- The layering that keeps the logic testable is compiler-enforced (SR-ARCH-01, -04).
- Open problems are in §6 (AN-01 … AN-12); no screen-reader session has been run.

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
through `ISimulationService` and does not re-verify them; the engine suite (246 cases, all passing on 2026-10-01)
is the evidence. The four `SceneConfigBuilderTests` cases that pin the Studio-facing contract are cited below.

## 2. Software items

Items SI-1 … SI-4 and their units SU-01 … SU-15 are defined in [VV.Studio.SDS §2](VV.Studio.SDS.md#2-software-items-and-units-531-541).
How each item is verified:

| Item | Project | Verified by |
|---|---|---|
| SI-1 Presentation logic | `Gcam.Studio.Core` (net9.0, no WPF) | unit tests with fakes |
| SI-2 View geometry and measurement maths | `Gcam.Studio.Core/Imaging` | unit tests, pure maths |
| SI-3 Simulation adapter | `Gcam.Studio.Services` | integration tests against the real engine (`SimulationServiceTests`) + engine-level tests |
| SI-4 WPF shell | `Gcam.Studio` (net9.0-windows) | inspection + manual UI Automation |
| SOUP ([VV.Studio.SDS §4](VV.Studio.SDS.md#4-soup-and-required-platform-533-534)) | NuGet | used as published; not separately verified |

Only SI-1 and SI-2 can be unit-tested with fakes, by design: everything with logic lives in a project that has no UI
stack. SI-3 is tested through the real engine.

## 3. Software requirements

The requirements are specified in [VV.Studio.SRS](VV.Studio.SRS.md) — **51 active** in eleven groups, including
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
| SR-RUN-01 | historical batch | T | unchanged batch tests remain for compatibility | withdrawn → SR-RUN-09, -19 |
| SR-RUN-02 | historical batch | T | unchanged batch cancellation tests remain for compatibility | withdrawn → SR-RUN-10 |
| SR-RUN-03 | unit | T | `MainViewModelTests.Failure_IsReportedNotThrown`, `MainViewModelTests.RunState_TracksOutcome_PeakTextFollowsResult` | pass |
| SR-RUN-04 | historical batch | T, I | batch progress guard remains; AN-04 is scoped to that path | withdrawn → SR-RUN-11 |
| SR-RUN-05 | historical batch | T, I | batch locking evidence retained in historical validation below | withdrawn → SR-RUN-12 |
| SR-RUN-06 | historical batch | T | batch command tests remain | withdrawn → SR-RUN-13 |
| SR-RUN-07 | historical batch | T | batch stale tests remain | withdrawn → SR-RUN-14 |
| SR-RUN-08 | historical batch | T | unchanged `SceneConfigBuilderTests` retain builder verification | withdrawn → SR-RUN-15 |
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
| SR-SCENE-01 | unit | T | `MainViewModelTests.SourceItem_ClampsValues_LabelFollowsEdits`, `MainViewModelTests.IsotopePicker_OffersIr192_AndKeepsCs137AsTheDefault` | pass |
| SR-SCENE-02 | unit | T | `MainViewModelTests.Startup_HasOneSelectedSource`, `MainViewModelTests.AddRemove_SelectsNewSourceThenNeighbour` | pass |
| SR-VIEW-01 | unit | T | `HeatmapViewportTests.Fit_PreservesAspectAndCentres` | pass |
| SR-VIEW-02 | unit | T, I | `HeatmapViewportTests.Snapping_FitsWholeDevicePixelsPerCell` (100 %, 125 %, sub-pixel); `HeatmapView` passes `PixelsPerDip` and `snapToWholePixels: true` (I) | pass |
| SR-VIEW-03 | unit | T | `HeatmapViewportTests.ZoomAt_KeepsAnchorPointFixed`, `HeatmapViewportTests.ZoomAt_IsClamped_FullZoomOutRefits` | pass |
| SR-VIEW-04 | unit | T | `HeatmapViewportTests.PanBy_CannotDragImageOutOfView` | pass |
| SR-VIEW-05 | unit | T, I | `HeatmapViewportTests.ScreenImage_RoundTripWithYUp`, `HeatmapViewportTests.PixelAt_RespectsBoundsAndOrientation`, `HeatmapViewportTests.MmMapping_PutsPixelCentresOnGrid`; flood origin `-(N−1)/2·pitch` in `SimulationService`, pinned by `SimulationServiceTests.FloodAxis_MatchesTheDecodersPixelCentres` | pass |
| SR-VIEW-06 | unit | T | `HeatmapViewportTests.Configure_ResizeKeepsZoom_NewImageSizeRefits` | pass |
| SR-VIEW-07 | system | I, M | readout format and key handling in `HeatmapView` (I); readout exposed via `ItemStatus`; UI scenario `ScenarioTests.Readout_AndOneCellRoi_MatchAbsolutePositionAndValue` (§5) | partial — readout automated; the refit keys (double-click, `0`, Home) by inspection only |
| SR-VIEW-08 | unit | T | `MainViewModelTests.RunState_TracksOutcome_PeakTextFollowsResult` | pass |
| SR-MEAS-01 | unit + integration | T, M | `MeasurementMathTests.Distance_IsEuclidean`, `MeasurementsViewModelTests.Add_NumbersSequentially_AndSelectsTheNewOne`; VAL-03 (12.3 mm) | pass |
| SR-MEAS-02 | unit | T, M | `MeasurementMathTests.AngleDeg_MeasuresAtTheVertex`, `MeasurementMathTests.AngleDeg_DegenerateArm_IsZero`; VAL-03 (67.0°) | pass |
| SR-MEAS-03 | unit | T, M | `MeasurementMathTests.Roi_CountsPixelsByCentre_CornersInAnyOrder`, `MeasurementMathTests.Roi_ClipsToTheImage_AndIsEmptyBetweenCentres`; VAL-03 (ROI on flood) | pass |
| SR-MEAS-04 | unit + integration | T | `MeasurementsViewModelTests.Roi_HasNoValueBeforeARun_AndFollowsEachNewResult`, `MeasurementsViewModelTests.Roi_OnAPaneWithoutData_StaysEmpty`, `MeasurementsViewModelTests.Roi_WithNoPixelCentreInside_HasNoValue`, `MainViewModelTests.NewResult_RefreshesMeasurements` | pass |
| SR-MEAS-05 | unit | T | `MeasurementsViewModelTests.Add_NumbersSequentially_AndSelectsTheNewOne`, `MeasurementsViewModelTests.Delete_SelectsTheNeighbour_ClearRestartsNumbering`, `MeasurementsViewModelTests.Add_WrongNumberOfPoints_Throws` | pass |
| SR-MEAS-06 | unit | T | `MeasurementsViewModelTests.ToolHint_FollowsTheActiveTool` | pass |
| SR-MEAS-07 | system | I, M | `MeasurementAdorner` (`MinDragPx = 4`, Esc / right-click / Delete handlers, `Clamp`); VAL-03 (Delete, Esc) | partial — 4 px threshold, right-click and clamping by I only |
| SR-MEAS-08 | system + unit | I, M, T | `MeasurementAdorner` (`MarkerSnapDigits = 1`, `Clamp`, `HitTestCore` honours `CanMoveMarkers`); VAL-04 drag to (18.9, 16.1) mm; stale part by `MainViewModelTests.EditingTheSceneAfterARun_MarksTheResultStale_UntilTheNextRun` | pass |
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
| SR-NAV-01 | unit + I | T, I | `MainViewModelTests.Workspace_SharedResultAndSelectionSurviveRun_ViewSettingsDoNotMarkStale`; workspace-type centre / panel templates | pass |
| SR-NAV-02 | unit + system | T, I | same workspace test: registered identity, unavailable index, retained selection; segmented switch / Ctrl+1…4 inspected | partial — only Imaging registered; desktop survey blocked |
| SR-NAV-03 | unit | T | `MainViewModelTests.RunInputs_MarkSharedResultStale_WhileWorkspaceSelectionDoesNot`, `Workspace_SharedResultAndSelectionSurviveRun_ViewSettingsDoNotMarkStale` | pass |
| SR-PLOT-01 | unit + I | T, I | `PlotSeriesTests.LowerBound_HandlesUniformAndIrregularX_AndEnds`; validation / cap / band / marker properties inspected | pass |
| SR-PLOT-02 | unit | T | `PlotViewportTests.Mapping_RoundTrips_AndClampsLogFloor`, `NiceTicksTests.Linear_Uses125Steps_EngineeringLabels_NoNegativeZero`, `Logarithmic_LabelsDecades_WithEightMinorTicks` | pass |
| SR-PLOT-03 | unit | T | `MinMaxPyramidTests.Query_EqualsBruteForce_IncludingBothEnds` (5 cases), `Range_ClipsOutsideData_RejectsNonFiniteSamples` | pass |
| SR-PLOT-04 | unit + system | T, I | `PlotViewportTests.ZoomPanReset_PreservesAnchor_AndBounds`; `PlotViewTests.TenMillionSamples_ZoomAndResizeRedraw_Within16Milliseconds` checks production peer, + key and reset; pointer / other keys / readout by inspection | partial — full input walkthrough pending |
| SR-PLOT-05 | system | T | `PlotViewTests.TenMillionSamples_ZoomAndResizeRedraw_Within16Milliseconds`; measured record in §5 | pass (CPU redraw only) |

### Coverage summary

The tables below preserve the pre-acquisition baseline (47 rows); withdrawn RUN rows are historical evidence.
The current eleven acquisition rows are verified above by tests / inspection. Their desktop paths are pending;
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

### Test inventory (run 2026-10-01, `dotnet test Gcam.sln` (Debug), .NET SDK 9.0.311)

| Class | Level | Cases | Result |
|---|---|---|---|
| `HeatmapViewportTests` | unit | 11 (9 methods, one theory × 3) | 11 / 11 pass |
| `MeasurementMathTests` | unit | 5 | 5 / 5 pass |
| `MeasurementsViewModelTests` | unit / integration | 7 | 7 / 7 pass |
| `MainViewModelTests` | unit / integration (with workspace and measurements) | 14 | 14 / 14 pass |
| `AcquisitionViewModelTests` | unit / integration with virtual acquisition clock + batch callback regression | 5 | 5 / 5 pass |
| `TickFormatterTests` | unit | 9 | 9 / 9 pass — no requirement yet (AN-10) |
| `MinMaxPyramidTests` | unit | 19 | 19 / 19 pass (includes concurrent plot edge / storage cases) |
| `PlotViewportTests` | unit | 3 | 3 / 3 pass |
| `NiceTicksTests` | unit | 2 | 2 / 2 pass |
| `PlotSeriesTests` | unit | 1 | 1 / 1 pass |
| **Gcam.Studio.Tests** | | **76** | **76 / 76 pass** |
| `SimulationServiceTests` (in Gcam.Studio.Services.Tests) | integration (the service against the real engine) | 7 | 7 / 7 pass |
| `AcquisitionServiceTests` (same project) | integration, real engine + virtual scheduler | 7 | 7 / 7 pass |
| `FloodOracleTests` (in Gcam.Studio.UiTests) | the UI scenarios' oracle, tested on its own | 10 | 10 / 10 pass |
| `PilotTests`, `ScenarioTests` (in Gcam.Studio.UiTests) | system, desktop (opt-in) | 7 | skipped normally; latest opt-in run: 7 fail at desktop input / focus (§5) |
| `PlotViewTests` | system, desktop (opt-in) | 1 | CPU redraw gate pass; skipped normally |
| `PolishSurveyTests` | diagnostic, desktop (opt-in) | 1 | blocked at UIA focus; skipped normally |
| `SceneConfigBuilderTests` (in Gcam.Tests) | integration (engine) | 7 (one theory × 4) | pass |
| `ListModeSourceTests` (engine) | physics / compatibility / throughput / decode measurements | 8 | 8 / 8 pass |
| **Gcam.Tests** (engine, out of scope) | | **254** | **254 / 254 pass; original 246 unchanged** |

The solution, including the WPF shell, built in Release without errors. Restore emitted NU1900 because NuGet
vulnerability metadata was unreachable. Builds used one MSBuild node and disabled node reuse after the default
restore failed without diagnostics. The final ordinary run passed 321 cases and skipped 9 desktop tests.
This table is the dated record; `dotnet test` prints the current totals.

Live-acquisition Release verification on 2026-10-01 passed 354 cases (254 engine + 76 Core + 14 service +
10 UI oracles), skipped all nine desktop cases and launched no Studio window. Zero build errors; NU1900
remains a restore warning. Measurements and pending desktop migration are in
[VV.Studio.Acquisition](VV.Studio.Acquisition.md).

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
| `PilotTests.Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength` | VAL-01, VAL-03 · SR-RUN-01, SR-MEAS-01 | length from screen points through the documented fit rule | 13.4 mm vs 13.400 ± 0.204 |
| `ScenarioTests.Cancel_KeepsPreviousResult_LocksThenUnlocksScene` | VAL-02 · SR-RUN-02, SR-RUN-05 | state machine Running → Cancelled; the peak text before the cancelled run | locked while running; Cancelled; peak unchanged; editable again |
| `ScenarioTests.Roi_OnFloodMap_CountsWholePixelsByCentre` | VAL-03 · SR-MEAS-03 | pixel count by centre, corners on cell boundaries so one pixel of error cannot change it | 42 px, 3.6 × 4.2 mm |
| `ScenarioTests.Angle_EscAbandonsDraft_DeleteRemovesSelected` | VAL-03 · SR-MEAS-02, SR-MEAS-07 | angle from the three screen points; Esc is proven by the value | 69.8° vs 70.02 ± 2.29; Delete removed it |
| `ScenarioTests.SourceDrag_MarksOutdated_RerunPutsPeakOnTheSource` | VAL-04, VAL-05 · SR-MEAS-08, SR-RUN-07, SR-VIEW-08 | physics: after the re-run the decoded peak must sit on the moved source (≤ 1.5 mm) | source (18.3, 13.6) → peak (18.7, 12.9); chip shown, then cleared |
| `ScenarioTests.Readout_AndOneCellRoi_MatchAbsolutePositionAndValue` | SR-VIEW-05, SR-VIEW-07, SR-MEAS-03 | absolute mm of two cells (both signs); a one-cell ROI must sum to the readout's value | (−6.3, 5.1) and (6.3, −4.5) mm exact; sum = cell value |
| `ScenarioTests.ThemeToggle_RelabelsAndSwitchesBack` | SR-THEME-01 | the label names the other theme; the app still simulates after two swaps | pass (the visual swap stays manual) |

Record: three normal runs of all 17 UI tests (10 oracle + 7 desktop) passed; a run with every scenario's expectation
deliberately corrupted (`GCAM_UI_BREAK_VERDICT=1`) failed all 7 desktop scenarios, each at its corrupted assertion,
so the verdicts can fail. No app process or sandbox folder was left behind.

## 6. Known anomalies, gaps and risks

### Anomalies and gaps

| ID | Finding | Effect | Plan |
|---|---|---|---|
| AN-01 | Measurements can only be **created** with the pointer (documented in [DESIGN.Controls](DESIGN.Controls.md#measurementadorner-via-measurementoverlay)) | keyboard users can review, select and delete, not create | keyboard crosshair |
| AN-02 | UI automation is desktop-only and opt-in | the seven scenarios of §5 are not part of CI; zoom and pan, the refit keys, drag-snapping limits and the visual theme swap are still checked by inspection or by hand | heatmap navigation scenarios next |
| AN-03 | No high-contrast mode | Windows high-contrast themes are not honoured | planned with the keyboard crosshair |
| AN-04 | Resolved: a legacy batch progress callback could race completion or arrive in a later session | callback guard/write and terminal progress now share a per-run lock and closed flag; live acquisition uses sequential snapshots | `AcquisitionViewModelTests.LegacyBatch_LateReportCannotOverwriteANewSessionProgress` forces an obsolete report into a new active run; original progress assertion unchanged |
| AN-05 | Literal sizes in `MainWindow.xaml` (photon box `Width="120"`, readout `Height="18"`, `Margin="0,4,0,0"`, colour-bar `Margin="0,8,0,0"`) despite the no-literals rule | cosmetic; spacing doesn't follow `Metrics.xaml` | move to named keys |
| AN-06 | The "outdated" chip is visual only (tooltip, not a live region) | a screen reader is not told that the images no longer match the scene | announce via the status line or a live region |
| AN-07 | WPF items (`HeatmapView` rendering, `ColorBar` ticks, `ThemeService`) have no unit tests, by design | defects show only when the app runs | covered by AN-02 plan |
| AN-08 | SDK-style project references are transitive, so the shell *could* call the engine directly | layering rule for the shell is inspection-only | optional: `PrivateAssets` on the Services → engine reference |
| AN-09 | Photon budget clamp (≥ 1,000) is untested | low | add a test |
| AN-11 | Latest desktop input and screen capture unavailable | regression scenarios and four-view polish survey remain unverified in this working tree | re-run on an accessible interactive desktop; preserve verdicts |
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
