# VV.Studio.History — dated verification records of GCAM Studio

Moved verbatim from [VV.Studio](VV.Studio.md) (the current V&V state, traceability and test inventory). These records
are kept as run on their dates; later changes are verified in VV.Studio and its workspace documents.

---

## Historical test inventory (run 2026-10-01, `dotnet test Gcam.sln -c Release`, .NET SDK 9.0.311)

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
inputs, the descriptive pile-up label, neutral preset name, grouped log decades, Y-title separation
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


## Waveform and shared-chain verification matrix (2026-10-02)

The Waveform workspace and physical-chain selection are implemented; execution status is **not verified in the implementer's sandbox**. This section supersedes earlier exclusion/read-only-chain descriptions and adds traceability without increasing the verified test totals until the independent run completes. Conditions, commands and expected render paths are in [VV.Studio.Waveform](VV.Studio.Waveform.md).

| Requirement | Automated verification | Current execution status |
|---|---|---|
| SR-CHAIN-01…03 | WaveformServiceTests chain/response tests; WaveformWorkspaceTests locked/frozen/disabled-edit tests; Evidence mixed-field calibration | Default headless suite passed; Evidence/renders not run for this change |
| SR-WAVE-01…02 | Origin-relative window/prehistory/association tests; next/latest/held-window VM tests; actual MC offscreen scope | Headless suite passed; renders not run for this change |
| SR-WAVE-03…05 | Rate-study immutability/determinism; shared response; zero-noise ideal tests; real/rate/ideal render labels | Headless suite passed; renders not run for this change |
| SR-WAVE-06 | Acquired readout/invalid-energy suppression, Q12 plotted scaling and simulation-time labels; exact C#/Python/RTL arithmetic matrix; no CR-RC energy-estimator precision assertion | Headless suite passed; renders not run for this change |
| SR-WAVE-07 | ScopeWindow policies; empty window/noise/late timestamp tests; maximum-cap worker measurement (Evidence) | Default headless suite passed; maximum-cap Evidence/renders not run for this change |
| SR-WAVE-08 | Visible-only/reuse/latest-revision/new-acquisition/error VM tests; prepared-plot identity and two-way render navigation | Headless suite passed; renders not run for this change |
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

Acquisition control (Start / Stop / Continue / Reset, 2026-10-02) Release verification: 262 engine, 164 Core, 72
service and 10 UI-oracle cases pass (previous 262 / 162 / 67 / 10); seven service evidence, nine desktop and the
render case skip in the normal run; the render opt-in passes and its PNGs were regenerated (Detector "stale"
fixture replaced by "acquired"). New service cases: `AcquisitionContinuationTests` (stop-and-continue equals the
uninterrupted stream event for event, with and without background; raised preset; Continue rejected while running;
seed reproduces / differs). The two rewritten desktop scenarios (`StopContinueReset_KeepsAccumulatesAndDiscards`,
`MoveSource_ResetEditStart_PutsPeakOnTheSource`) were not run. Measurements: [VV.Studio.Acquisition](VV.Studio.Acquisition.md).

## Detector and retained-flood focus verification matrix

| Requirement | Automated verification | Execution status |
|---|---|---|
| SR-DET-01 | DetectorWorkspaceTests.Gap_InvalidEditPreventsStart, Gap_RevalidatesAfterPitchChange_AndConvertsUnits; FocusSweepServiceTests.Config_RejectsGapAtServiceBoundary | Passed in Release, 2026-10-02 |
| SR-DET-02 | Face_FollowsPendingBeforeStart_AcquiredAndLockedAfter_PendingAgainAfterReset; MainViewModelTests.Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsKeepState | Passed in Release, 2026-10-02 |
| SR-DET-03 | DetectorFaceTests.Geometry_ExactGapCoverageAndArea; Face_GainsMatchMeasurementPattern; DetectorGapEvidenceTests | Deterministic tests passed; opt-in seed-spread evidence awaits environment-change authorization |
| SR-DET-04 | RenderDetectorAndFocus before/acquired Detector, dark/light, two sizes; local geometry card top alignment and natural height asserted | Generated 2026-10-02; affected 1280×800 dark/light cards inspected after UI polish; desktop follow-up pending |
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

## Final desktop verification and validation (2026-10-02)

Release at b8fddcd plus the final desktop-pass changes, Windows 11, 100 % display scaling, .NET 9.0.13.
This record supersedes earlier desktop-unavailable / not-run statements above. Build: zero warnings / errors.
Ordinary tests: 262 engine, 164 Studio Core, 72 services and 13 UI-oracle cases pass; seven service evidence,
14 desktop and one render case skip by their explicit opt-ins. Desktop: 27/27 pass (13 oracle, 12 scenarios,
plot gate, survey). Corrupted-verdict run: all 12/12 scenarios fail at their deliberately corrupted assertions;
their manifests were inspected, every owned process exited with code 0, and no sandbox writes occurred.
Temporary owned sandboxes are retained for audit; no automatic deletion or forced process termination.

Commands (PowerShell; opt-ins apply only to the shell process running that invocation):

```powershell
dotnet build Gcam.sln -c Release
dotnet test Gcam.sln -c Release --no-build
$env:GCAM_UI_TESTS='1'; dotnet test tests/Gcam.Studio.UiTests -c Release --no-build
$env:GCAM_UI_TESTS='1'; $env:GCAM_UI_BREAK_VERDICT='1'; dotnet test tests/Gcam.Studio.UiTests -c Release --no-build --filter 'FullyQualifiedName~ScenarioTests|FullyQualifiedName~PilotTests'
```

The positive recovery run uses a fresh command process without the break-verdict variable. Per-run manifests,
inputs, expected / actual values, build stamp, dirty-file count and failure bundles are recorded under the test
output's `ui-runs`. A compact reviewable record is [desktop evidence](assets/studio-desktop-evidence.json).
The numerical automation evidence is read-only, enabled only in a harness-owned process. Expected values are
computed in `WorkspaceOracle` or in the scenario, without calling the production band counter, peak finder,
stripper or focus-interval implementation. Bound plot / heatmap arrays and markers are checked as well as the
workspace results. Screenshot pixels are never a numerical oracle.

The ten-million-sample plot CPU gate passed: maximum zoom redraw 5.5822 ms and resize redraw 3.9559 ms
(limit 16 ms). Resize event-to-render samples were 238.0788–595.2849 ms; no end-to-end ≤16 ms claim is made.

| Requirement | New desktop verification (`WorkspaceScenarioTests`) | Result |
|---|---|---|
| SR-SPEC-05, SR-SPEC-06, SR-IMG-01 | `Spectrum_BandCountAndWindowChange_MatchRetainedHistogram` | N=1: 1,143; N=2: 1,373 counts in the 661.7 keV band, independently summed by bin centre; unchanged retained histogram; bound plot matches |
| SR-WAVE-01, SR-WAVE-02, SR-WAVE-03 | `Waveform_SelectedEventListAndMarkers_MatchArrivalWindow` | Selected #10 is the sole event in the 10 µs arrival window; both plots' labels and marker times agree; rate-study label is shown, acquired arrivals unchanged |
| SR-DET-02, SR-DET-04, SR-RUN-26 | `Detector_FaceBeforeStart_LockedUntilReset` | 900-crystal, 18 mm face visible before Start; inputs and caption locked with acquired data; Reset restores editable inputs / next-acquisition caption |
| SR-IMG-03, SR-IMG-04, SR-IMG-05 | `Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks` | Two All peaks, selected isotope peaks on the appropriate source sides; independent grid maxima match found markers / chip; every stripped cell matches raw low minus calibrated high, clipped at zero |
| SR-OPT-04, SR-FOCUS-01, SR-FOCUS-03 | `Imaging_RefocusAndSweep_MatchProjectionAndHalfMaxInterval` | 1000→500 mm halves grid step, retains counts / flood; interval and plotted band match independent half-max crossings / censor flags; no depth-accuracy claim |

| ID | Validation scenario | Expected and observed outcome | Status |
|---|---|---|---|
| VAL-09 | Fixed-seed acquisition, open Spectrum, edit window N | Emission row agrees with the retained histogram's independently counted bins; width scales with N and histogram is unchanged | Performed, pass |
| VAL-10 | Open Waveform, select event #10, enable rate study | Event membership / both marker sets agree with an independent acquired-time filter; rate-study label is explicit and acquired times persist | Performed, pass |
| VAL-11 | Open Detector before acquisition, acquire, Reset | Face visible from pending inputs; acquired inputs lock until Reset and caption describes that state | Performed, pass |
| VAL-12 | Fixed-seed two-isotope scene, All / Co-60 / Cs-137, Compton strip | All shows two peaks, selected peaks change with channel; strip changes Cs peak value and follows independent per-pixel subtraction | Performed, pass |
| VAL-13 | Retained-data refocus, then focus sweep | Projection grid changes with focus while flood / acquired counts persist; one 81-sample curve produces a descriptive half-max interval with correct plotted endpoints / censor flags | Performed, pass; depth accuracy excluded |

The rewritten acquisition desktop validations also pass: Stop freezes counts, Continue increases them, Reset
unlocks / discards; the reset/edit/start scenario puts the peak at (21.6, 17.1) mm for source (21.4, 16.0) mm,
inside its existing 1.5 mm tolerance. This does not establish a general sub-mm guarantee (AN-11).

The survey opened all four workspaces in both themes at 1280×800 and 1440×900 and inspected all 16
[desktop frames](assets/studio-polish-survey/README.md). Observations P-12 … P-16 include touching Spectrum
line/window columns, crowded small-window found labels, faint locked inputs, the shaped trace's omitted text
marker labels, and Detector panel whitespace. No layout fix was made. The dark two-isotope Imaging
[representative desktop capture](assets/studio-desktop-imaging.png) is referenced in the repository README.
That README image is a separate approved retake with Co-60 at 200 µCi and Cs-137 at 500 µCi, positions
(20, 0) / (−20, 0) mm, 1000 mm distance, seed 12345, 60 s, All channel, dark theme. It acquired 6,974 counts.
Found Cs-137 (−20.6, 0.3) mm and Co-60 (20.2, 0.5) mm have unrounded errors 0.676 / 0.546 mm,
both below one 8.75 mm resolution element. The read-only product-state truth check passed before the file was
written; run `20261002-130331-aa0ebe` exited 0 with no sandbox writes. The original 16 survey frames and
regression measurements retain the 20 µCi Co-60 scene. The retake build had zero warnings / errors and its
single opt-in capture invocation passed; this is not another full-suite run.
No keyboard-only or screen-reader validation, other DPI scaling, or numerical depth-accuracy test is claimed.

## UI polish verification — 2026-10-02

The emission table now measures numeric headers and rows together with explicit 8-DIP cell gaps; the mixed
Spectrum fixture includes both Co-60 windows and grouped counts. Truth/found chips use pure Core rectangle
packing with 4-DIP gaps and visible-image bounds, including selection outlines and marker halos. Marker
centres remain at their original mapped coordinates. A chip without a safe full-size placement is omitted,
with the coordinate/measurement list retained. Detector's geometry card takes its natural text height.
The shaped waveform's upper-only event-label policy is unchanged; render assertions check both flags.

`ThemeContrastTests` reads production XAML tokens copied into the plain Core test output. Disabled text
measures Surface/Canvas/Raised **5.164006 / 5.653886 / 4.516509:1** dark and
**5.154465 / 4.716330 / 4.625225:1** light by sRGB relative luminance. All meet the retained-value target
of at least 4.5:1; disabled action opacity and antialiased edge pixels are not certified by these numbers.

Release normal solution verification passed: **262 engine / 178 Studio Core / 72 services / 13 UI-oracle**
cases (525 total); seven numerical evidence, fourteen desktop and one render case skipped as designed.
This change adds six pure overlay-layout cases and eight token-contrast cases (Core 164 → 178).
The solution build had zero errors and two existing xUnit analyzer warnings in unchanged service tests:
ImagingServiceTests.cs:106 (`xUnit2000`) and WaveformServiceTests.cs:98 (`xUnit2012`).

The process-scoped `GCAM_RENDER_SNAPSHOTS=1` Release invocation passed separately (one orchestrating test),
regenerating **76 whole-content and four standalone plot PNGs**. Affected 1280×800 dark/light mixed Spectrum,
crowded/edge Imaging, before/acquired Detector and real-arrival Waveform renders were opened and inspected.
Numeric strings fit with aligned column edges, full found chips separate into lanes and remain inside the
image, locked inputs are readable with subdued borders, and the geometry card ends below its final caption.
The renderer also checks actual chip drawing footprints, table cell gaps/full strings, disabled seed resources,
natural card height and upper-only waveform labels. This is offscreen visual and geometry evidence, not a new
desktop validation or physics measurement. The prior desktop survey captures remain unchanged.

## Desktop verification — 2026-10-07

Run by the planner on the author's go, in an isolated git worktree (`C:\gw\wui`) so that a concurrent implementation in
the main working tree could not change the build under test. `GCAM_UI_TESTS=1 dotnet test tests/Gcam.Studio.UiTests
-c Release`, Windows 11 (ko-KR), per-monitor DPI aware; manifests under the test output's `ui-runs/`.

| Run | Build | Result |
|---|---|---|
| Full suite | `01dbca6` (before TODO-36) | 26 / 27 pass; `Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks` failed deterministically (run `20261007-121043-87fbdc`, repeated) — TODO-38 |
| Full suite | `f48b6af` + TODO-38 fix + six MLEM scenarios | 33 / 34; `Mlem_SelectedDuringAcquisition_…` timed out; the Stop / Continue / Reset scenario intermittent — waits keyed on an idle worker that MLEM never leaves during acquisition, plus two product defects (unit label followed the selector instead of the displayed image; every refresh cleared the hovered readout) |
| Full suite | the above + the fixes | **34 / 34 pass** (19 desktop scenarios, runs `20261007-134131` … `134502`) |
| Broken verdict (`GCAM_UI_BREAK_VERDICT=1`), MLEM + channel scenarios | same | **7 / 7 fail, each at its corrupted assertion** (runs `134515` … `134616`) |
| Recovery, same 7 | same | **7 / 7 pass** (runs `134628` … `134741`) |

TODO-38's cause: not the ambient default (it puts 0 counts in either window; the scene fails with the field off too) but
an under-powered scene — Co-60 20 µCi gives ~41 counts in its channel at 1 m in 60 s, and the peak lands on the wrong
side in 27 % of such acquisitions. The scene now uses Co-60 400 µCi, chosen by a bound (wrong-side probability
≤ 10⁻³ per channel from exact Poisson moments of Studio's decoding weights: 1.3 × 10⁻⁴ for Co-60, 1.3 × 10⁻⁵ for
Cs-137). Not covered by automation: keyboard-only reachability and a both-themes survey of the Reconstruction selector.

## Desktop verification — strip count, 2026-10-07

TODO-39 (signed net strip count, signed cross-correlation decoding, ROI units). Planner's run in the worktree
`C:\gw\wui` at `fde8c35` plus the TODO-39 change (24 files), Release build: full suite **35 / 35 pass**; broken
verdict on the six MLEM scenarios and the channel / strip scenario **7 / 7 fail, each at its corrupted assertion (no
timeout)**; recovery **7 / 7 pass** (runs `20261007-151850` … `152124`). Another session tried to use the desktop near
the end of the recovery run; every result was judged by its exact oracle and none shows interference.

## Desktop verification — physical readout, 2026-10-08

TODO-40 (experimental four-output readout in Studio). Planner's runs in the worktree `C:\gw\w40` on the TODO-40 change
over `01592a5`, Release build. First full run: 38 / 39 — `ReadoutScenarioTests.UnsupportedGeometryAndField_ExplainRefusalWithoutZeroing`
found a real defect: a refused readout selection left the combo box showing the rejected value while the view model kept
DirectCrystal (also present for the optics preset under physical readout). Fixed by refreshing after the binding commit,
with an offscreen test on the bound controls' selected item. The new scenarios lacked broken-verdict handling, added.
Final runs: full suite **39 / 39 pass**; `GCAM_UI_BREAK_VERDICT=1` on `ReadoutScenarioTests`: the three desktop scenarios
**fail, each at its corrupted assertion (no timeout)**, the headless count oracle passes as intended; recovery **39 / 39
pass**.

## Desktop verification — readout presets, 2026-10-12

TODO-41 (physical readout on every Studio preset). Planner's runs in the worktree `C:\gw\w41`, Release build.
- Before the review fixes: full suite pass with every readout scenario, including one cold High-res 661.7 keV
  preparation (48 min); two product defects found and fixed on the way (exact double comparison of the ±15 % benchmark
  window; calibration-line details unreachable to UI Automation, fixed with `ContentItemsList`).
- After the review fixes: the cold High-res scenario failed once 5 min 44 s into its wait (state element missing, Detector
  expander collapsed; cause not established). By the author's decision it became a long scenario, run only with
  `GCAM_UI_LONG_TESTS=1` as well; `HighResCoLine_…` (Co-60, 1332.5 keV, 2 000 histories per crystal) carries the 44 × 44
  checks in the routine run. Polling waits now treat transient UIA errors as "not yet" and record their count.
- Final: readout scenarios and the full suite pass (49 pass, the long scenario skipped); `GCAM_UI_BREAK_VERDICT=1`: every
  readout desktop scenario fails at its corrupted assertion (six in the run before the polling change, HighResCoLine
  after it at its module geometry, 0 transient UIA errors); recovery passes.

## Desktop verification — declutter stage 1, 2026-10-12

TODO-44 (help marks, cost card removed, SiPM view centred, icon buttons, Reset confirmation in an in-window modal
layer). Planner's runs in the worktree `C:\gw\w44`, Release build, on the author's go.
- Full desktop suite (28 scenarios; the long cold High-res scenario skipped): 27 pass. `WideLocalisationHelp_…` failed
  before its verdict — the rewritten scenario chose a preset named "Wide" instead of "Wide FOV" (test code; no other
  name mismatch found, and a headless test now checks its three strings against the shipped record).
- `GCAM_UI_BREAK_VERDICT=1` on the scenarios whose assertions changed: `StopContinueReset_…` (cancel keeps the data, then
  discard), `CustomModules_…` and, after the fix, `WideLocalisationHelp_…` fail at their corrupted assertion; recovery
  passes.
- The polish survey refreshed `docs/assets/studio-polish-survey/` with the new top bar, icon buttons and help marks.

## Declutter stage 2 and desktop-review redesign, 2026-10-13

TODO-45 and the author's desktop review of the stage-2 build (Options dialog, borderless window with a themed caption,
left panel as Scene / Optics / Detector tabs with the optics fields always shown, sectioned panels with label / value
rows, measurement tool toggles and per-row delete, readout view tabs with an empty state, accent Prepare, energy-window
presets, visible Start refusals, scrollbar gutter, one-line status bar carrying acquisition or preparation progress).
Planner's verification in the worktree `C:\gw\w44` on `0d21524`, Release build:
- Build: 0 warnings, 0 errors.
- Normal suite: **1,010 passed / 48 skipped** (the opt-in desktop, render and long-evidence cases skip).
- Opt-in offscreen renders (`GCAM_RENDER_SNAPSHOTS=1`): 25 / 25 pass — the main-window render test now runs as ten tests
  (five screen groups × two themes) on one shared render host, keeping every capture and assertion; the captures cover
  both themes at 1280×800 and 1440×900.
- Test catalog: 193 units, `check-catalog --discover` passes; the CI checkers (calibration record, readout offer record,
  test-record self-test, archived replay) pass.
- **Not run: the desktop.** The desktop was not available after stage 2. Pending the author's go: the whole desktop
  suite (the long cold High-res scenario excluded), broken-verdict / recovery runs for every scenario whose assertions
  changed (Options helpers, left-tab selection, `Detector.Identity`, readout view tabs), and the UI-automation re-measure
  and re-pilot for the borderless window, including `ChromeScenarioTests.BorderlessCaption_HasNoNativeTitleBar_AndStartsAtWindowTop`
  (no OS title bar) and the pilot's caption-button bounds. Until then the desktop statuses in VV.Studio predate stage 2.

## Physical readout with the ambient field and BSR (TODO-42), 2026-10-13

Worktree `C:\gw\w42`, branch `todo42-readout-field`. Release build 0 warnings; normal suite 1,050 passed / 49 skipped;
opt-in offscreen renders 27 / 27 (refusal, missing-bank and ready states with ambient and BSR inspected); catalog 197
units; CI checkers (studio-readout-offer, test-catalog, archived-replay, test-record-self-test) pass. DirectCrystal
exact-hash fixtures pass. Evidence families readout-field-v1 (ambient) and readout-field-v2 (BSR), 16 outer seeds each,
pass and were re-run identically (Findings 70). Pending (desktop unavailable): the desktop scenarios
`PhysicalBsrAndAmbient_ContinuationConservesMeasuredCounts` and `RefusedLineAndMissingPreparation_ExplainWithoutZeroing`
with broken-verdict / recovery.

## Desktop verification — declutter stage 2 and TODO-42, 2026-10-13

Main tree at `ed1298b` (Release). Full desktop suite with `GCAM_UI_TESTS=1` (the long High-res cold test excluded): 28 of 29
desktop scenarios passed, including `ChromeScenarioTests.BorderlessCaption_HasNoNativeTitleBar_AndStartsAtWindowTop` (no
native title bar in the real window), the pilot (caption geometry), every scenario that now goes through the Options
dialog, the readout scenarios and the polish survey (16 PNGs refreshed in `docs/assets/studio-polish-survey/`). Failures:
`PhysicalBsrAndAmbient_ContinuationConservesMeasuredCounts` (never run before) could not choose the ambient bound in the
Options dialog — a product defect in the modal focus boundary (VV.Studio AN-17), fixed with `ModalFocusRenderTests`; and
`RefusedLineAndMissingPreparation_ExplainWithoutZeroing` failed once on a harness race (the layer looked up before it
existed), fixed by waiting for the layer. Broken-verdict runs of the five changed scenarios failed on their verdicts;
after the fixes the re-run (both scenarios plus four regression scenarios for the stricter `Choose`) passed, its
broken-verdict pair failed on the verdict and the recovery passed. Not covered by a scenario: real Alt-key routing for
the tool toggles and Enter inside the Options dialog (headless tests only).

## Studio manual, first edition, 2026-10-15

Worktree `C:\gw\w43`, branch `todo43-studio-manual`. Six manual desktop scenarios (`ManualScenarioTests`, one per generated
chapter) as one observed batch, each with the author's go. Runs 01 and 02 failed every scenario at "owned window
foreground": a Windows firewall prompt held the foreground (closed with the author's OK). Run 03 passed 3 of 6; fixes in
`663eb77` (labels read again after Start / Stop replace them, an absent empty text counts as cleared, Log Y starts on).
Run 04 (`663eb77`) passed 6 / 6, observed run completed with two recorded OS background filters (`65683fc`). Figure fixes
(badges outside their marks, crops that keep whole panels) in `0399f80`; 20 figures inspected. Edition `2026-10-15-1`
generated from run 04 (2.0 MB, draft until the notice list and static chapters are approved) and replayed by
`manual_records.py check --all`. Release build 0 warnings; normal suite 1,095 passed / 56 skipped; catalog 220 units;
generator self-test 24 / 24.

Approval, same day: after the figure labels became "screen name: purpose" (purposes sourced in `docs/manual/purposes.json`)
and the edition gained PDF and Word renderings, the author approved the notice list, static chapters 1.1 and 5.2, the
figures, the mark purposes, the charter and the background filters. The edition was regenerated from run 04 as formal
(75 files, 3.8 MB; `911d8c2`); generator self-test 41, tests 25, `check --all` byte for byte.

## Report export (TODO-46), 2026-10-15

Worktree `C:\gw\w46`, branch `todo46-report-export`. Desktop scenario `ReportExportScenarioTests` (the author's go each
run): run 1 looked for the save dialog among the desktop's children and run 2 did not invoke the dialog's own Save button
(both harness defects, fixed in `487dbfd` and `f3798d3`); run 3 (`f3798d3`) passed in 6 s — the dialog's Save (IDOK) was
invoked, exactly `report.html` and `report.scene.json` appeared in the owned sandbox, the HTML holds the displayed estimate
and found-peak rows, and the scene file loads. That run's HTML was 4.3 MB, almost all of it the ambient spectrum tables
repeated from the scene file; the HTML now shows their entry counts only (default scene without figures: 3,869,095 →
10,831 bytes; scene file unchanged). Offscreen renders of the main window (dark / light, 1280×800 / 1440×900): "Export
report…" sits beside Reset at the height of the other top-bar buttons, unclipped. Release build 0 warnings; normal suite
1,066 passed / 50 skipped; catalog 203 units. No broken-verdict or recovery run was made for this exploratory scenario.

Run 4 (`7caf9ff`) passed on the reduced HTML (450,860 bytes). Stage 2, the same day (decisions X1 … X8): a document icon
button opens an export popup — HTML, PDF (PDFsharp-WPF), Word and event data (CSV with a metadata file, under every
readout). Run 5 (`13a6969`) passed the theory for HTML, PDF and Word through the popup; run 6 (`ab34330`) passed all four
formats (CSV 6 s, the others 5 s each), each writing exactly its declared files into its owned sandbox (three for CSV),
with no process left. Samples read by the planner: the PDF (7 pages, configuration as an appendix), the Word package, the
CSV and meta file of a direct and of a physical-readout acquisition. Release build 0 warnings; normal suite 1,093 passed /
51 skipped; catalog 208 units.

## Desktop verification after merging TODO-43 and TODO-46, 2026-10-15

Main tree at `86ccd3b` (Release). The full desktop suite ran with `GCAM_UI_TESTS=1`, the long High-res cold test
excluded: 107 of 119 passed, 2 were skipped and 10 failed.

- **CSV export case.** The merged harness flagged every sandbox write as an undeclared effect, including the export
  theory's declared files. Only writes outside the scenario's declared list are flagged now (`36c73a5`).
- **The other nine.** These failed when the harness first looked for `ThemeToggle`; one of them, High-res Co, lost the
  foreground during its preparation instead. The failure dumps showed the main window with names but no AutomationIds.
  A launch probe outside the harness found the cause: a UI Automation element taken from the main window handle
  before WPF attaches the window's content stays empty for good. This happened in 3 of 12 launches (window element at
  about 0.90 s instead of 0.98 s), and a fresh element taken 1 s later found the content at once.
  `OwnedStudioProcess.MainWindow` now takes a fresh element until the window exposes its children.
- **Re-runs.** The first re-run, after the CSV fix only, passed 4 of the 10 (CSV, HTML, MLEM, High-res Co). After the
  window fix, the remaining six and the whole export theory passed: 8 of 8, including Detector, PDF and Word export
  and the three readout scenarios.
- The run refreshed the 16 polish-survey PNGs; the title bar now shows the export icon.

## Readout comparison (TODO-47), 2026-10-15

Worktree `C:\gw\w47`, branch `todo47-readout-compare`; review and three implementation turns by Codex. The Compare
workspace reads one physical-readout acquisition as the ideal direct assignment and as the Anger readout from the same
retained inputs (SR-CMP-01 … 05). On Studio's Baseline preset (661.7 keV, on-axis Cs-137 at 1 m, seed 47001, 10 s,
ambient off) the same-crystal share is 71.39 % (257 / 360) with no unknown conversions, both estimates at (0.833, 0.833)
mm; the review measured 52.6 % on Sharp. Existing DirectCrystal and readout hashes unchanged. Refresh cost p50 / p95:
Baseline 4.1 / 5.7 ms, Sharp 23.1 / 36.1 ms (All). Offscreen renders inspected at pixel level after two layout turns
(found labels, flood-only difference toggle, image sizing). Release build 0 warnings; normal suite 1,150 passed / 61
skipped; catalog 233 units. Desktop (author's go, same day, Release build of `7a16d63`): the Compare scenario and the
scenarios its changes touch (title-bar workspace tabs, physical hit stream) — Compare, the five Workspace scenarios, the
readout scenarios without the long-running High-res cold run, manual Navigate, chrome and pilot — 20 passed, 1 skipped
(the opt-in long run) in 8.6 min; no Studio or test host left, no tree changes.


## Studio manual, second edition, and desktop-test videos (TODO-48), 2026-10-16

Worktree `C:\gw\w48`, branch `todo48-manual-second-edition`; review and headless turn by Codex, finished by a substitute
agent. Every desktop test that drives Studio now records an MP4 of the owned window (10 fps, H.264 through Media
Foundation, cursor drawn, `.vtt` step captions); a pass publishes it to `artifacts/ui-videos/` with `index.json`.

Desktop turn (author's go each run). Pilot `probe-20261016-01` on `10b55bf`: controls seen; two out-of-declaration values
of the shell's MuiCache (a new executable path). Observed manual batch `manual-20261016-01`: 6 of 8 passed; the new
chapters 3.7 and 4.4 failed at completion (a label read only while its popup or tab is shown; `753b04c`). The batch also
showed the Windows save dialog recording the exports in the user's per-extension recent list and last folder (HKCU
`ComDlg32`). Broken verdicts: all eight chapters fail at their own mutated check; recovery passes. Regression with the
recorder on: 32 recorded scenarios and the eight chapters pass, twice; the long opt-in run skipped. The timing-sensitive
scenarios (readout cancel, two focus sweeps) take 13 / 17 / 14 s with the recorder on and off. Recorder cost: 40 videos,
12.2 min, 95 MB (7.7 MB/min), no dropped frame; 138 neutral frames, all during the save dialog; no foreign window masked.
Found and fixed during the turn: frames and screenshots showed one row and column of the window behind Studio at the
frame edge (now a neutral two-pixel edge everywhere, `39e4bfa`, `af45c5f`; edition 1's figures keep that row, MD13); the
export figure's marks left no label room (three marks); the Compare reference lies below the panel's fold (read, not
pictured, `2765ccf`); the Store `python` passed the interpreter probe and then refused a start (launcher first).

Author decisions: the dialog's history is declared as the OS dialog's record (E6), the MuiCache values are a background
filter, and the classification-rule changes were approved. Final runs on `46c9ada` (author's go): pilot
`probe-20261016-02` completed (G1 basis); observed manual batch `manual-20261016-02` passed 8 / 8 and, with three filters
it evidenced (certificate revocation URL cache, Store publisher folder, a desktop messenger's data; `0e245c2`),
re-classifies completed. Edition `2026-10-16-1` (ten chapters, 29 figures) generated from that batch; the author approved
the notices (N-NOT-SAVED re-worded, new N-EXPORT-SCOPE), static chapters 1.1 and 5.2 and the mark purposes; formal (104
files, 6.2 MB), both editions replay byte for byte. Release build 0 warnings; normal suite 1,171 passed / 63 skipped;
catalog 238 units; generator self-test 44.

## Test-record milestone M2 with a desktop profile (TODO-49), 2026-10-16

The test-record collector gained a desktop profile (`collect --family dotnet --profile desktop`, author's go): the UI
test project with the desktop opt-in on, the long run and the observed probe off, the polish survey not run (it rewrites
committed screenshots); a desktop case replaces a normal-profile selection that was not executed, and M1 still replays
byte for byte. Milestone M2 was collected on the clean `f029b3b` checkout: .NET (normal), Python with the numerical
tests, cocotb (Python 3.13) and seven checkers, then the desktop profile (140 passed, the long run and the probe not
executed). Report `docs/VV.Tests.Results.md` formal: 1,518 cases passed, 53 not executed, none failed; export
`samples/testing/records/M2`; bundle, export and both archived milestones replay. Two earlier collection attempts were
discarded (a desktop lease left by a stopped run; a scratch generation that wrote into this repository's VV.Studio);
the collection scripts now keep scratch output out of the repository.

## Approved pilot under the procedure v3.3 and the desktop result cache (TODO-51, TODO-52), 2026-10-16

One desktop session on `a927311` (author's go, recorded as HUM-01 / 04 / 07; the deployment fingerprint is the one every
commit since the source-determined build keeps). Approved pilot `probe-20261016-03`: preflight passed on the pinned isolation
and deployment fingerprints; the observed run passed (2 min) and classifies **completed** under the procedure v3.3 — all
three positive controls seen, nothing out of declaration, 12 candidates (11 harness-owned, 1 declared), 2 unattributed
changes (counted, kept locally), 4 OS run records, no background filter. It replaces `probe-20261016-02` as the G1 basis.
Result cache: the four report-export cases (first desktop run of the `report-<run-id>.<ext>` export names) and the chrome
case passed (5 / 5, 30 s) and wrote `artifacts/ui-videos/cache.json`; the same selection with `GCAM_UI_CACHE=1` skipped all
five as `[cache-reused]` from those runs (0.7 s).

## Observed manual batch and the manual's third edition, 2026-10-17

After the charter check against the procedure v3.4 (`fc7a641`, docs only), one observed manual batch on that commit
(author's go, recorded as HUM-01 / 04; the G1 basis `probe-20261016-03` still holds: the pin's isolation and deployment
fingerprints equal it). `manual-20261017-01`: the eight chapter scenarios passed (8 / 8, 4 min) and the batch classifies
**completed** — all positive controls seen, nothing out of declaration or undecided, 223 candidates (208 harness-owned, 15
declared), 24 unattributed changes (counted, kept locally), 4 OS run records, no background filter. Edition `2026-10-17-1`
(formal, ten chapters, 29 figures) was generated from that batch on deployment `2e7e30ec32af`; against `2026-10-16-1` its
text differs only in the deployment and one live count read in chapter 3.1 (12 → 11 counts under the pointer). All three
committed editions replay; the README links the new one.

## Test-record milestone M3, 2026-10-17

Collected on the clean `161e74e` checkout right after the observed manual batch (same deployment fingerprint), with the
commands of M2: .NET (normal), the desktop profile (154 desktop-project cases passed with a video each; the long run and
the observed probe not executed), Python with the numerical tests, cocotb (Python 3.13) and seven checkers. Report
`docs/VV.Tests.Results.md` formal: 1,534 passed, 53 not executed, **1 failed** — `CompareWorkspaceTests.SettingsAndReset_…`
timed out once in the normal run; 20 repeats of its assembly all passed. The failure stays the selected result (a repeat is
retained, never swapped in) and is anomaly AN-18 in VV.Studio with its likely cause, a thread race that only the
test fixture can hit. Export `samples/testing/records/M3`; bundle, export and the three archived milestones replay.
