# AGENTS.UiAutomation — driving GCAM Studio through UI Automation

Scope: automated UI runs against the real GCAM Studio window (`tests/Gcam.Studio.UiTests`). Unit tests of the
ViewModels and geometry are not covered here (see [DESIGN.Architecture](DESIGN.Architecture.md#testing-strategy));
what the UI runs verify is traced in [VV.Studio](VV.Studio.md).

The work goes through stages, and a stage is not skipped because a later one looks easy. Each stage has an exit
condition and a record below.

| Stage | Question | Exit condition | State (2026-10-01) |
|---|---|---|---|
| 0 Charter | what is automated, for whom, with what permission | purpose, scope, non-scope, success criteria written | **done** |
| 1 Safety | what can a run write, start, or reach | every write surface confirmed absent, isolated or blocked; process ownership rules in code | **done** |
| 2 Measurement | how is each kind of control found, driven and judged | every interaction contract measured on the running app | **done** for the contracts the pilot uses + run / cancel |
| 3 Pilot | does one complete flow work end to end | start → act → judge → clean up; 2 passes in a row; a deliberately broken verdict fails; a recovery run passes | **done** |
| 4 Scale-out | scenarios traced to requirements and risks | each target requirement automated, manual, or excluded with a reason | **done** for the measuring and run flows; coverage gaps listed |
| 5 Evidence | are results tied to build, script, input, environment | manifest per run; artefacts opened and checked | manifest + failure bundle exist; exploratory only |
| 6 Operation | who runs it, when, how flakiness and baselines are handled | written run policy | rules below; no CI runner (needs a desktop) |

## 0. Charter

| Item | Decision |
|---|---|
| Purpose | Regression checks of Studio's user flows that unit tests cannot reach: the adorner's pointer gestures, hit-testing, the async run as seen from the window, and the automation surface itself |
| Result use | **Exploratory (profile P1)** until stage 4: results guide development and are not evidence for [VV.Studio](VV.Studio.md) beyond "performed" |
| Readers | the author and reviewers of this repository |
| In scope | `Gcam.Studio.exe` built from this working tree; main window; dark theme; default optics |
| Out of scope (for now) | light theme, display scaling other than the test machine's, keyboard-only flows, screenshots as pass/fail criteria |
| Allowed changes | test code; **test hooks in the product**: `AutomationProperties.AutomationId` on controls, the run state as `ItemStatus` on the status line |
| Forbidden effects | none exist to forbid (no data, devices, network or files — stage 1); the runs still never touch processes or folders they did not create |
| Environment | the developer's interactive desktop. A run moves the real mouse and takes focus, so it is **opt-in** (`GCAM_UI_TESTS=1`) and must not run while someone is using the machine |
| Success criteria | the product's own result (run state, a results-table row and its value) matches an independently computed expectation |

## 1. Safety

**Write surfaces.** Searched every project Studio loads (`Gcam.Studio*`, `Gcam.Simulation` and its dependencies)
for file, directory, registry, environment, process, network, clipboard and mutex APIs. Only
`ConfigLoader.Save` / `Load` and `Waveform`'s JSON read use the file system, and neither is reachable from Studio
(CLI paths). Studio keeps no settings: the theme choice is not persisted.

| Surface | Declared | Resolved in a run | State |
|---|---|---|---|
| User / app data, settings, logs, caches | none in code | `TEMP` / `TMP` (and env-var readers of `APPDATA` / `LOCALAPPDATA`) redirected into an owned sandbox folder; **0 files written** in every run so far | confirmed absent; the redirect is a partial boundary — see below |
| Network | none in code | no TCP connections observed from the app's PID during an audit run | confirmed absent |
| Child processes | none in code | none observed | confirmed absent |
| Shared resources | the interactive desktop and the mouse | one run at a time (`DisableTestParallelization`), opt-in | blocked by policy |

**What the redirect does not cover.** `Environment.GetFolderPath(ApplicationData)`, isolated storage and other
known-folder lookups resolve through the shell's known-folder registry, not the process environment, so they would
still reach the real profile. The boundary for those is the source survey (no such call is reachable) plus the
audit — not the redirect. If Studio ever gains settings or a cache, isolate them through an explicit path the
harness can set, and re-run this stage.

**Audit run.** Before any automation: the app started (sandboxed) and closed with no UI interaction while
child processes, TCP connections and the sandbox were watched. Nothing was written, started or connected. A global
before/after file diff was tried first and rejected: other processes on the machine write to the same folders, so it
cannot attribute writes. The owned sandbox can.

**Ownership** (`Harness/StudioProcess.cs`).

- A `Gcam.Studio` process that the run did not start is a conflict: the run fails and never attaches to or kills it.
- The run records PID, start time, image path and product version; at the end it closes **that PID only**, killing
  it after 5 s if needed, and records how it ended.
- The sandbox carries an owner marker. It is deleted only if it is under `%TEMP%` and the marker is still the run's.
- `GCAM_UIA_RUN=<runId>` is passed to the app so a later restart could be tied to the run.

**Build identity.** The SDK stamps the commit into the binary (`ProductVersion` = `1.0.0+<sha>`). That stamp does
not cover uncommitted changes, so each manifest also records the number of dirty files in the working tree.

## 2. Measurement

Measured on 2026-10-01 against the running app (Windows 11, ko-KR, 1440×900 window).

**Tree facts.** 75 elements in the control view; 26 AutomationIds, all unique. Names are **not** unique: a panel
title and its heatmap are both "Detector flood map", a field label and its combo box are both "Isotope". Selectors
therefore use AutomationIds and require exactly one match.

| Contract | Implementation | Find | Act | Read back the input | Judge the product's handler |
|---|---|---|---|---|---|
| Button | `Button` (Add / Remove source, Run, Cancel, Clear, Delete, theme) | AutomationId | `InvokePattern.Invoke` | — | the effect, e.g. source rows 1 → 2 |
| Segment | `RadioButton.Segment` (tool picker) | AutomationId | `SelectionItemPattern.Select` | `IsSelected` | tool hint text changes |
| Text field | `TextBox` (X, Y, distance, activity, photons) | AutomationId | `ValuePattern.SetValue` | `ValuePattern.Value` | **only after focus leaves** — the binding commits on LostFocus; the source row label then updates |
| Combo | `ComboBox` (isotope) | AutomationId | `ExpandCollapse.Expand` → item `Select` → `Collapse` | selection | source row label |
| List row | `ListBoxItem` (sources, measurements) | list AutomationId → children | `SelectionItemPattern.Select` | `IsSelected` | row name is the item's description |
| Heatmap | `HeatmapView` (custom peer, type Image) | AutomationId | focus + keys (`+`, `−`, arrows, `0`) | — | `ItemStatus` ("zoom 1.3x; x … mm …") |
| Overlay | `MeasurementAdorner` | **not in the tree**; position from the heatmap's rectangle | real pointer input (`SetCursorPos` + `SendInput`) | cursor position | a new row in the results table |
| Async run | Run → `StatusText` | AutomationId | `Invoke` | — | `ItemStatus` Idle → Running → Succeeded (≈ 0.7 s at 500 k photons) |
| Cancel | Cancel button, present only while running | AutomationId while Running | `Invoke` | — | `ItemStatus` Cancelled within 0.1 s; peak chip unchanged; Cancel leaves the tree |

**Product defects found by this measurement (fixed in the same change).**

- Source list rows were announced as `Gcam.Studio.Core.ViewModels.SourceItemViewModel` (no name on the container).
- The status line, tool hint and measurement detail had a fixed `AutomationProperties.Name` that **hid their text**:
  the polite live region announced only "Status".

**Pointer coordinates.** The test process makes itself per-monitor DPI aware, so UIA rectangles, `SetCursorPos` and
the screen are all physical pixels. The window is normalised to 1440×900 before measuring.

## 3. Pilot

`PilotTests.Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength`: start sandboxed → normalise the window →
check the start state (Idle, no measurements) → Run and wait for `Succeeded` and, separately, for the flood map to
have an image → select the Distance tool (read back, and the hint must change) → drag across the flood map at fixed
fractions of the image → expect exactly one row `M1`, Distance, Flood, with the expected length → close the app,
check it exited with 0 and wrote nothing.

**Oracle.** `FloodOracle` re-derives where the image sits (whole device pixels per cell, centred) and which mm a
screen pixel is, from the documented layout rules and the default optics — it does not call the app's geometry code.
Tolerance: one device pixel of error at each end plus the display rounding (0.204 mm on the test machine).
`FloodOracleTests` (5 cases) test the oracle itself and run in every `dotnet test`, desktop or not.

**Record, 2026-10-01** (build `1.0.0+4efe95a` plus uncommitted changes):

| Run | Expected | Actual | Result |
|---|---|---|---|
| 1 | 13.400 mm ± 0.204 | 13.4 mm | pass |
| 2 | 13.400 mm ± 0.204 | 13.4 mm | pass |
| 3, verdict broken on purpose (`GCAM_UI_BREAK_VERDICT=1`, expected + 1 mm) | 14.400 mm | 13.4 mm | **fail, as intended**; failure bundle written |
| 4, recovery | 13.400 mm | 13.4 mm | pass |

No `Gcam.Studio` process or sandbox folder was left after the four runs.

**Harness defects found along the way:** the configuration folder was derived from a path with a trailing separator
(the run failed cleanly with a manifest); the process exit was read after disposal. Both fixed.

## 4. Scale-out

`ScenarioTests` adds five scenarios to the pilot. Each starts a fresh, sandboxed app (`Harness/Scenario.cs`), so no
tool, selection, zoom or result leaks between them (~2–3 s each; the suite takes ~20 s). Expected values never come
from the app's own code.

| Scenario | Traces to | Oracle (independent of the app) | Recovery-run result |
|---|---|---|---|
| `Pilot_SimulateThenMeasureDistance_AddsRowWithExpectedLength` | VAL-01, VAL-03 · SR-RUN-09, SR-MEAS-01 | length from screen points through the documented fit rule | 13.4 mm vs 13.400 ± 0.204 |
| `StopContinueReset_KeepsAccumulatesAndDiscards` (rewritten 2026-10-02 for Start / Stop / Continue / Reset, not run; the row's result is the earlier `Stop_KeepsAcquiredData_LocksThenUnlocksScene`) | VAL-02 · SR-RUN-10, SR-RUN-12, SR-RUN-23, SR-RUN-26 | state machine Acquiring → Stopped; counts frozen after Stop (two refresh periods); Start clears | Start replaced by Stop, scene / live time / speed locked; 35 counts kept and unchanged; editable again; restart began at 0 |
| `Roi_OnFloodMap_CountsWholePixelsByCentre` | VAL-03 · SR-MEAS-03 | pixel count by centre, with corners on cell **boundaries** so one pixel of error can't change it | 42 px, 3.6 × 4.2 mm |
| `Angle_EscAbandonsDraft_DeleteRemovesSelected` | VAL-03 · SR-MEAS-02, SR-MEAS-07 | angle from the three screen points (scale- and flip-invariant); Esc is proven by the value — an un-abandoned first click would have produced a different angle | 69.8° vs 70.02 ± 2.29; Delete removed it |
| `MoveSource_ResetEditStart_PutsPeakOnTheSource` (rewritten 2026-10-02: the drag is withdrawn; Reset, X / Y edit, Start; not run; the row's result is the earlier drag version) | VAL-04, VAL-05 · SR-RUN-12, SR-RUN-26, SR-VIEW-08 | physics: after a re-run the decoded peak must sit on the moved source (≤ 1.5 mm) | list-mode, 60 s: source (21.4, 16.0) → peak (21.7, 17.4), 1.43 mm — inside but near the tolerance; chip shown, then cleared |
| `Readout_AndOneCellRoi_MatchAbsolutePositionAndValue` | SR-VIEW-05, SR-VIEW-07, SR-MEAS-03 | absolute mm of two cells (both signs) from the oracle; a one-cell ROI must sum to the readout's value — closes the blind spot below | (-6.3, 5.1) and (6.3, -4.5) mm exact; Σ = cell value |
| `ThemeToggle_RelabelsAndSwitchesBack` | SR-THEME-01 | the label names the other theme; the app still simulates after two swaps | pass (the visual swap stays manual) |

**Blind spot found by review, then closed.** Length, angle and ROI-count verdicts are translation- and
flip-invariant: an origin off by a whole pixel or a mirrored y axis would have passed all of them. The readout
scenario pins absolute positions. Re-run after the audit fixes: 3 × 17/17 (10 oracle + 7 desktop); broken run
fails all 7 desktop scenarios at their corrupted assertions.

**Verdict checks.** `GCAM_UI_BREAK_VERDICT=1` corrupts every scenario's own expectation (+1 mm, +1 pixel, +5°,
source + 3 mm, wrong label, wrong peak text). Record, 2026-10-01: normal runs 3 × 14/14 pass → broken run: **all 6
desktop scenarios fail, each at its corrupted assertion** (checked in the manifests), the 8 oracle tests pass →
recovery run 14/14. No process or sandbox left behind.

**Oracle defects found by its own tests.** ROI corners were first placed on cell centres — exactly where the
"by centre" rule flips — so half a pixel decided the count. Moved to cell boundaries; a test now proves a pixel of
error changes nothing.

**Product defect found.** The theme button's accessible name was a fixed "Switch theme" while it shows "Light theme"
/ "Dark theme" — the spoken name didn't contain the visible label (WCAG 2.5.3). The fixed name was removed.

### Coverage

| Axis | Covered | Not covered, and why |
|---|---|---|
| Requirements (system level) | SR-RUN-01/02/05/07, SR-VIEW-08, SR-MEAS-01/02/03/07/08, SR-THEME-01, SR-A11Y-05 (all selectors are AutomationIds) | SR-VIEW-01…07 zoom / pan / readout — unit-tested in Core, keyboard zoom measured once (stage 2); next candidates. SR-THEME-02 visual swap — would need a pixel verdict, excluded by charter. SR-A11Y-03 keyboard walkthrough, VAL-07 — manual. VAL-08 display scaling — needs a second machine setting, manual |
| Screen | the one window, idle / running / succeeded / cancelled; both images with and without data | light theme beyond the label (charter) |
| Controls operated | Run, Cancel, photon budget, tool picker (Pan, Distance, Angle, ROI), both heatmaps (pointer, Esc, Delete), source marker drag, theme toggle | operated only in stage 2 measurement: Add source, X / Y fields, isotope combo. Never operated: Remove source, Distance / Activity fields, Clear and − (delete) buttons, source list selection |
| Judged results | every scenario judges product state (run state, rows, values, fields, chips); none judges pixels | — |
| Environment | Windows 11, ko-KR, 100 % scaling, 1440×900 window, Release build | other scaling, other cultures |
| Manual only | gesture thresholds (4 px drag, right-click abandon) — inspection; screen-reader session — not done | |

## 5. Evidence

Each run writes `ui-runs/<runId>/manifest.json` under the test output folder (ignored by git): profile, purpose,
build stamp and executable, dirty-file count, culture, DPI awareness, window rectangle, oracle inputs, expected and
actual values, process ending, sandbox writes, step log. A failed run adds `tree.txt` (control-view dump) and
`window.png`.

`window.png` is the app's **visible frame** (`DWMWA_EXTENDED_FRAME_BOUNDS`). The UIA rectangle includes the invisible
resize borders; the first capture used it and picked up another application's text along the window's edge. That
image was deleted and the capture fixed. Open a failure bundle's image before sharing it.

## 6. Operation

- `GCAM_UI_TESTS=1 dotnet test tests/Gcam.Studio.UiTests -c Release` — only when the desktop is free.
  Without the variable the desktop tests are **skipped** and reported as skipped, never as passed.
- Runs are serial. A run that fails is not retried automatically; read its manifest first.
- CI (`.github/workflows/ci.yml`) runs the oracle tests but not the desktop tests — it has no interactive session. Say so in its
  summary rather than reporting the UI suite green.
- Re-measure (stage 2) and re-pilot (stage 3) after changing AutomationIds, the window layout, the adorner's input
  handling, or the test machine's display scaling.

## Next

- Heatmap zoom / pan / readout scenarios (SR-VIEW-03, -04, -07) through keys and `ItemStatus`.
- The never-operated controls above (Remove source, Clear, Delete button, source list selection).
- A keyboard crosshair for creating measurements (AN-01), then a keyboard-only scenario.
- Promote from exploratory to regression use (profile P2) once the suite has a run policy owner and history.

Keep screenshots diagnostic-only; judge on product state.

**Re-run on list-mode acquisition (2026-10-01).** The scenarios moved from Simulate / Cancel / photon budget to
Start / Stop / live time (`StudioWindow.Acquire`, status counts parsed from the status line); the cancel scenario
became `Stop_KeepsAcquiredData_LocksThenUnlocksScene`. Normal run: 19 / 19 (10 oracle, 7 desktop scenarios, the
plot gate, the polish survey). Broken run (`GCAM_UI_BREAK_VERDICT=1`): all 7 desktop scenarios fail at their
corrupted assertions. Two findings: the source-drag localisation passed at 1.43 mm against a 1.5 mm tolerance
(in-crystal Compton scatter is now in the image and the peak is the grid argmax — a thin margin, not a pass to rely
on), and **UIA Select on the workspace switch checks the button without changing the workspace** (only a click or
`Ctrl+1…4` runs the command) — an accessibility defect; the survey switches with `Ctrl+2` until it is fixed.
