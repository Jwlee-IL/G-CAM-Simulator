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
