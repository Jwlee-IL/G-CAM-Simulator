# VV.Studio — verification and validation of GCAM Studio

Scope: the three Studio projects (`Gcam.Studio.Core`, `Gcam.Studio.Services`, `Gcam.Studio`) and their tests.
Not covered: the simulation engine (verified by its own tests, see [AGENTS.md](../AGENTS.md#solution-layout)) and
the original code-behind viewer `src/Gcam.Wpf`. This document says **how we know Studio does what it must**; it is
one of a set:

| Document | IEC 62304 activity | Holds |
|---|---|---|
| [VV.Gcam.URS](VV.Gcam.URS.md) · [VV.Gcam.PRS](VV.Gcam.PRS.md) | system level, above §5.2 | user needs and product requirements for the whole Gcam product concept; Studio is its subsystem SS-4 |
| [VV.Studio.SRS](VV.Studio.SRS.md) | §5.2 requirements analysis | the requirements (`SR-*`), inputs / outputs, messages, risk-control requirements |
| [VV.Studio.SDS](VV.Studio.SDS.md) | §5.3 architecture, §5.4 detailed design | software items and units (`SU-*`), interfaces, SOUP, requirement allocation |
| VV.Studio (this page) | §5.5–5.7 verification, validation, §7 risk, §9 problems | traceability to tests, validation scenarios, anomalies, risk table |

The working design guides are [DESIGN.Architecture](DESIGN.Architecture.md) and the other `DESIGN.*` pages.

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
through `ISimulationService` and does not re-verify them; the engine suite (156 cases, all passing on 2026-10-01)
is the evidence. The four `SceneConfigBuilderTests` cases that pin the Studio-facing contract are cited below.

## 2. Software items

Items SI-1 … SI-4 and their units SU-01 … SU-15 are defined in [VV.Studio.SDS §2](VV.Studio.SDS.md#2-software-items-and-units-531-541).
How each item is verified:

| Item | Project | Verified by |
|---|---|---|
| SI-1 Presentation logic | `Gcam.Studio.Core` (net9.0, no WPF) | unit tests with fakes |
| SI-2 View geometry and measurement maths | `Gcam.Studio.Core/Imaging` | unit tests, pure maths |
| SI-3 Simulation adapter | `Gcam.Studio.Services` | engine-level tests + inspection |
| SI-4 WPF shell | `Gcam.Studio` (net9.0-windows) | inspection + manual UI Automation |
| SOUP ([VV.Studio.SDS §4](VV.Studio.SDS.md#4-soup-and-required-platform-533-534)) | NuGet | used as published; not separately verified |

Only SI-1 and SI-2 can be unit-tested, by design: everything with logic lives in a project that has no UI stack.

## 3. Software requirements

The requirements are specified in [VV.Studio.SRS](VV.Studio.SRS.md) — **39** in nine groups (`SR-RUN`, `SR-SCENE`,
`SR-VIEW`, `SR-MEAS`, `SR-THEME`, `SR-ENV`, `SR-A11Y`, `SR-SEC`, `SR-ARCH`). The matrix below cites them by ID only;
the SRS is the single source of their wording.

## 4. Verification

Levels follow IEC 62304: **unit** (§5.5) = one class in isolation; **integration** (§5.6) = ViewModels together, or
Studio against the real engine; **system** (§5.7) = the running app. Methods: **T** automated test (exact
`Class.Method`), **C** compiler-enforced, **I** inspection of code / XAML, **M** manual check driven through UI
Automation. Status: **pass** (evidence on 2026-10-01), **partial**, **open** (no evidence yet).

### Traceability matrix

| Req | Level | Method | Evidence | Status |
|---|---|---|---|---|
| SR-RUN-01 | unit | T, I | `MainViewModelTests.Run_PassesSceneAndPublishesResult`, `MainViewModelTests.RunState_TracksOutcome_PeakTextFollowsResult`; `Task.Run` in `SimulationService` (I) | pass |
| SR-RUN-02 | unit + integration | T | `MainViewModelTests.Cancel_KeepsPreviousResult_ReenablesEditing`; engine side `SceneConfigBuilderTests.Runner_ReportsProgress_HonoursCancellation` | pass |
| SR-RUN-03 | unit | T | `MainViewModelTests.Failure_IsReportedNotThrown`, `MainViewModelTests.RunState_TracksOutcome_PeakTextFollowsResult` | pass |
| SR-RUN-04 | unit + integration | T, I | `MainViewModelTests.Run_PassesSceneAndPublishesResult` (Progress = 1.0); `SceneConfigBuilderTests.Runner_ReportsProgress_HonoursCancellation` (monotonic, ends at 1.0); guard in `MainViewModel.RunAsync` (I) | partial — see AN-04 |
| SR-RUN-05 | unit | T, I | `MainViewModelTests.Cancel_KeepsPreviousResult_ReenablesEditing` (add disabled while running); photon box `IsEnabled="{Binding IsIdle}"`, `CanMoveMarkers` bound to `IsIdle` (I) | pass |
| SR-RUN-06 | unit | T | `MainViewModelTests.RemoveAll_DisablesRemoveAndRun`, `MainViewModelTests.Startup_HasOneSelectedSource` | pass |
| SR-RUN-07 | unit | T, I | `MainViewModelTests.EditingTheSceneAfterARun_MarksTheResultStale_UntilTheNextRun`; chip bound to `IsResultStale` (I) | pass |
| SR-RUN-08 | integration (engine) | T | `SceneConfigBuilderTests.NearestPrime_SnapsRankToNearestPrime` (4 cases), `SceneConfigBuilderTests.Build_MultiSourceFiniteMaskConfig`, `SceneConfigBuilderTests.Build_RejectsNonPositivePhotonBudget` | pass |
| SR-SCENE-01 | unit | T, I | `MainViewModelTests.SourceItem_ClampsValues_LabelFollowsEdits`; photon clamp in `MainViewModel.OnPhotonsChanged` (I only) | pass (photon clamp: I) |
| SR-SCENE-02 | unit | T | `MainViewModelTests.Startup_HasOneSelectedSource`, `MainViewModelTests.AddRemove_SelectsNewSourceThenNeighbour` | pass |
| SR-VIEW-01 | unit | T | `HeatmapViewportTests.Fit_PreservesAspectAndCentres` | pass |
| SR-VIEW-02 | unit | T, I | `HeatmapViewportTests.Snapping_FitsWholeDevicePixelsPerCell` (100 %, 125 %, sub-pixel); `HeatmapView` passes `PixelsPerDip` and `snapToWholePixels: true` (I) | pass |
| SR-VIEW-03 | unit | T | `HeatmapViewportTests.ZoomAt_KeepsAnchorPointFixed`, `HeatmapViewportTests.ZoomAt_IsClamped_FullZoomOutRefits` | pass |
| SR-VIEW-04 | unit | T | `HeatmapViewportTests.PanBy_CannotDragImageOutOfView` | pass |
| SR-VIEW-05 | unit | T, I | `HeatmapViewportTests.ScreenImage_RoundTripWithYUp`, `HeatmapViewportTests.PixelAt_RespectsBoundsAndOrientation`, `HeatmapViewportTests.MmMapping_PutsPixelCentresOnGrid`; flood origin `-(N−1)/2·pitch` in `SimulationService` (I) | pass |
| SR-VIEW-06 | unit | T | `HeatmapViewportTests.Configure_ResizeKeepsZoom_NewImageSizeRefits` | pass |
| SR-VIEW-07 | system | I, M | readout format and key handling in `HeatmapView` (I); readout exposed via `ItemStatus` | partial — no dated manual record |
| SR-VIEW-08 | unit | T | `MainViewModelTests.RunState_TracksOutcome_PeakTextFollowsResult` | pass |
| SR-MEAS-01 | unit + integration | T, M | `MeasurementMathTests.Distance_IsEuclidean`, `MeasurementsViewModelTests.Add_NumbersSequentially_AndSelectsTheNewOne`; VAL-03 (12.3 mm) | pass |
| SR-MEAS-02 | unit | T, M | `MeasurementMathTests.AngleDeg_MeasuresAtTheVertex`, `MeasurementMathTests.AngleDeg_DegenerateArm_IsZero`; VAL-03 (67.0°) | pass |
| SR-MEAS-03 | unit | T, M | `MeasurementMathTests.Roi_CountsPixelsByCentre_CornersInAnyOrder`, `MeasurementMathTests.Roi_ClipsToTheImage_AndIsEmptyBetweenCentres`; VAL-03 (ROI on flood) | pass |
| SR-MEAS-04 | unit + integration | T | `MeasurementsViewModelTests.Roi_HasNoValueBeforeARun_AndFollowsEachNewResult`, `MeasurementsViewModelTests.Roi_OnAPaneWithoutData_StaysEmpty`, `MainViewModelTests.NewResult_RefreshesMeasurements` | pass |
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

### Coverage summary

| Primary method | Requirements |
|---|---|
| Automated test (alone or with I / M) | 25 — RUN 01–08, SCENE 01–02, VIEW 01–06 + 08, MEAS 01–06 + 08, THEME-01 |
| Manual UI Automation + inspection | 7 — VIEW-07, MEAS-07, THEME-02, ENV-02, A11Y-01, A11Y-02, A11Y-03 |
| Inspection only | 3 — A11Y-04, SEC-01, ARCH-03 |
| Compiler-enforced (+ inspection) | 4 — ENV-01, ARCH-01, ARCH-02, ARCH-04 |

| Status | Count |
|---|---|
| pass | 32 |
| partial | 7 — RUN-04, VIEW-07, MEAS-07, THEME-02, ENV-02, A11Y-03, ARCH-03 |
| open | 0 requirements; system-level scenarios are open in §5 |

### Test inventory (run 2026-10-01, `dotnet test Gcam.sln -c Release`, .NET SDK 9.0.311)

| Class | Level | Cases | Result |
|---|---|---|---|
| `HeatmapViewportTests` | unit | 11 (9 methods, one theory × 3) | 11 / 11 pass |
| `MeasurementMathTests` | unit | 5 | 5 / 5 pass |
| `MeasurementsViewModelTests` | unit / integration | 6 | 6 / 6 pass |
| `MainViewModelTests` | unit / integration (with `MeasurementsViewModel`) | 11 | 11 / 11 pass |
| **Gcam.Studio.Tests** | | **33** | **33 / 33 pass** |
| `SceneConfigBuilderTests` (in Gcam.Tests) | integration (engine) | 7 (one theory × 4) | pass |
| **Gcam.Tests** (engine, out of scope) | | **156** | **156 / 156 pass** |

The solution, including the WPF shell, built in Release without errors in the same run. Canonical test totals
are kept in [AGENTS.md](../AGENTS.md#build--run); this table is the dated record.

## 5. Validation

User-level scenarios run on the real app. "Performed" means driven through UI Automation with real pointer and key
input (`SetCursorPos` + `mouse_event`, as described in [AGENTS.Studio](AGENTS.Studio.md#verifying-a-ui-change)).

| ID | Scenario | Acceptance criteria | Status |
|---|---|---|---|
| VAL-01 | Run the default scene and read the result | progress moves to 100 %; flood map and reconstruction appear with colour bars; peak chip shows mm; status line names counts and time; UI stays responsive | open — implied by VAL-03 (ROI needs a result), no separate record |
| VAL-02 | Cancel a long run | Cancel appears only while running; images keep the previous result; status "Cancelled — previous result kept"; scene editable again | open |
| VAL-03 | Measure on both images | distance and ROI drawn on the flood map, angle on the reconstruction; values appear in "Measurement results" and on the image; Delete removes the selected one; Esc abandons a half-drawn angle | **performed 2026-10-01** — distance 12.3 mm and an ROI on the flood map, angle 67.0° on the reconstruction, Delete and Esc behaved as specified |
| VAL-04 | Move a source by dragging, then re-run | marker follows the pointer only with the Pan tool and when idle; X / Y fields show the snapped value; "outdated" chip appears; a re-run clears it | **partial 2026-10-01** — source dragged to (18.9, 16.1) mm on a 0.1 mm grid; chip and re-run not recorded |
| VAL-05 | Locate a source and measure its offset from the decoded peak | Distance tool from the source marker to the peak gives the offset in mm; it agrees with the peak chip and source X / Y to within one recon step | open |
| VAL-06 | Use both themes | all text, focus rings, chips and overlays legible in dark and light; title bar follows | open |
| VAL-07 | Keyboard only | every function reachable without a pointer except creating measurements (AN-01) | open |
| VAL-08 | Small screen and display scaling | at 1366×768 the window starts maximised; at 125 % / 150 % heatmap cells stay equal width | open |

**Automation plan.** Roadmap step 4 turns VAL-01 … VAL-04 into UI Automation smoke tests ([AGENTS.Studio](AGENTS.Studio.md#roadmap));
work has started. Until then these are manual and not part of the regression run.

## 6. Known anomalies, gaps and risks

### Anomalies and gaps

| ID | Finding | Effect | Plan |
|---|---|---|---|
| AN-01 | Measurements can only be **created** with the pointer (documented in [DESIGN.Controls](DESIGN.Controls.md#measurementadorner-via-measurementoverlay)) | keyboard users can review, select and delete, not create | keyboard crosshair, step 4 |
| AN-02 | No automated UI tests | adorner hit-testing, gestures, drag snapping, theme swap and rendering are checked only by inspection or by hand | UIA smoke tests, step 4 |
| AN-03 | No high-contrast mode | Windows high-contrast themes are not honoured | step 4 |
| AN-04 | The progress-race test is timing-dependent: the fake reports 0.5 asynchronously, but nothing forces it to arrive after completion | the guard is covered by inspection; a regression might not fail the test every time | make the late report deterministic |
| AN-05 | Literal sizes in `MainWindow.xaml` (photon box `Width="120"`, readout `Height="18"`, `Margin="0,4,0,0"`, colour-bar `Margin="0,8,0,0"`) despite the no-literals rule | cosmetic; spacing doesn't follow `Metrics.xaml` | move to named keys |
| AN-06 | The "outdated" chip is visual only (tooltip, not a live region) | a screen reader is not told that the images no longer match the scene | announce via the status line or a live region |
| AN-07 | WPF items (`HeatmapView` rendering, `ColorBar` ticks, `ThemeService`) have no unit tests, by design | defects show only when the app runs | covered by AN-02 plan |
| AN-08 | SDK-style project references are transitive, so the shell *could* call the engine directly | layering rule for the shell is inspection-only | optional: `PrivateAssets` on the Services → engine reference |
| AN-09 | Photon budget clamp (≥ 1,000) is untested | low | add a test |

No screen-reader (Narrator / NVDA) session has been run. Not verified.

### Risk table (illustrative, IEC 62304 §7 style)

| Hazardous situation | Cause | Control | Verified by |
|---|---|---|---|
| Wrong position read off an image | y-flip or half-pixel error in screen ↔ mm | one mapping in `HeatmapViewport`; pixel-centre convention shared with the decoder | SR-VIEW-05 tests; VAL-03 |
| Old image taken as current | scene edited after the run | stale flag and "outdated" chip; cancel keeps and says "previous result kept" | SR-RUN-02, SR-RUN-07 tests; AN-06 open |
| ROI sum from the wrong image or frame | measurement not refreshed, or pane mixed up | measurements stored per pane in mm; refreshed on every result | SR-MEAS-04 tests |
| False precision | sub-pixel ROI weighting, unrounded drags | whole pixels by centre; drags snapped to 0.1 mm; values at 0.1 resolution | SR-MEAS-03 tests; SR-MEAS-08 I + M |
| App hangs or crashes on a long or failing run | MC on the UI thread; unhandled exception | `Task.Run` in the service; cancellation; failure → `State = Failed` | SR-RUN-01…03 tests |
| Status missed by colour-blind or screen-reader users | colour-only cues | text next to every status dot; selection by shape; live status line | SR-A11Y-04 (I); AN-06 |

## 7. Configuration and regression

**Re-run verification**

```bash
dotnet build Gcam.sln -c Release                      # all projects incl. the WPF shell
dotnet test  Gcam.sln -c Release                      # engine + Studio
dotnet test  tests/Gcam.Studio.Tests -c Release       # Studio only, no UI stack needed
dotnet run   --project src/Gcam.Studio -c Release     # for the §5 scenarios
```

A verification record is valid for one working tree: note the commit, the SDK version and the pass counts when
the matrix in §4 is updated.

**Rules that keep this document true**

- Test names follow `Subject_ExpectedBehaviour[_Condition]`, one class per type under test
  ([AGENTS.Conventions.Code](AGENTS.Conventions.Code.md#tests-xunit)), so a matrix row names one method.
- A change that adds or alters behaviour adds its requirement row ([SRS](VV.Studio.SRS.md)), its unit allocation
  ([SDS §7](VV.Studio.SDS.md#7-requirement-allocation)), its matrix row here and its test in the **same commit**; docs and code
  never drift apart ([AGENTS.Conventions.Docs](AGENTS.Conventions.Docs.md#keeping-docs-in-sync)).
- Renaming a test means updating its row here; a cited test that no longer exists is a defect in this document.
- Problems found during V&V go to §6 with an `AN-` ID, or to [AGENTS.Backlog](AGENTS.Backlog.md) once scheduled.
