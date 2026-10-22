# AGENTS.UiAutomation — driving GCAM Studio through UI Automation

Scope: automated UI runs against the real GCAM Studio window (`tests/Gcam.Studio.UiTests`). Unit tests of the
ViewModels and geometry are not covered here (see [DESIGN.Architecture](DESIGN.Architecture.md#testing-strategy));
what the UI runs verify is traced in [VV.Studio](VV.Studio.md).

Current suite (2026-10-13): 24 headless oracle cases plus 29 opt-in desktop cases — 26 regression scenarios (`ScenarioTests` 6, `WorkspaceScenarioTests` 5, `MlemScenarioTests` 6, `ReadoutScenarioTests` 8 of which one is the long opt-in, `ChromeScenarioTests` 1), the pilot, the plot gate and the survey — 53 total. The retained final desktop run (2026-10-02) passed 27/27 of the suite as it then stood (13 oracle, 12 scenarios, plot gate, survey); it is not re-executed by a normal test run. Start / Stop / Continue / Reset and all four workspaces are covered. Source drag and stale-result marking were withdrawn; the initial charter, pilot and coverage tables below are historical records. Current totals: [VV.Studio](VV.Studio.md#current-test-inventory).

The work follows the author's UI-automation procedure **v4.0** (2026-10-21; TODO-54, 2026-10-22). It is gated: a gate is not
skipped because a later one looks easy. The gates replace the stages 0–6 of the procedure v3.x; the sections below keep
those stages' records as history.

| Gate | Question | Passes when | State (2026-10-22) |
|---|---|---|---|
| G0 Charter | what is automated, for whom, with what permission | this charter (§0) approved by the author on the target commit (act `charter`) | rewritten for v4.0 on 2026-10-22; approval on its commit pending |
| G1 Safety | what can a run write, start or reach | an observed launch-idle-exit run on the current isolation folder and deployment: every positive control seen, the file, process and network channels live, no `unsafe_effect`, no product failure, cleanup complete | to re-establish: TODO-54 changed the isolation folder (previous basis `probe-20261016-03`, procedure v3.3) |
| G3 Pilot | does one complete flow work end to end | on the v4 harness: the pilot passes twice in a row, a deliberately broken verdict fails at its assertion, a recovery run passes, each with a passing video check | to run after G1 |
| G4 Scale-out | is every target requirement automated, manual or excluded with a reason | the coverage table (§4) holds, and each automated case has passed on the v4 harness with a passing video | after G3 |
| G5 Production | may a desktop result enter a test record or a manual edition | machine-checked: the approval package names G0 and G1 `passed`; the exact run's video status is `passed` under contract `v4`; otherwise the generators refuse, and failed or superseded attempts go to the final-review list | in code (TODO-54 turn C) |

G2 (measurement, §2) and G6 (operation, §6) are recommended practice, not gates. The planner judges G3 and G4 from the run
records. The approval package carries only G0 and G1, so the generators do not machine-check G3 / G4 (a recorded
difference, TODO-54).

## 0. Charter

The author approves this table as a whole, on the commit that carries it (act `charter`). The 2026-10-01 charter
(exploratory profile P1, stages 0–6) is in git history.

| Item | Decision |
|---|---|
| Purpose | Regression checks of the user flows that unit tests cannot reach: the adorner's pointer gestures, hit-testing, the async run as seen from the window, the report export and the automation surface. Also the desktop evidence for the Studio manual and for the software test report |
| Readers | the author, reviewers of this repository, readers of the software demonstration set |
| Result use | named per run in its record: **exploratory** (development), **regression** (a desktop `dotnet test` run), **test evidence** (a test-record milestone's desktop profile, `test_records.py collect --profile desktop`), **manual** (an edition of the Studio manual). Only test evidence and manual pass through G5 |
| Target build | `Gcam.Studio.exe`, Release, built from a clean commit of this repository. It is identified by the source commit, the deployment fingerprint and the isolation-folder fingerprint (`stage0.py pin`). Builds are source-determined (TODO-51) |
| Environment and launch permission | the author's interactive desktop (Windows 11, ko-KR, 100 % scaling, 1440×900 window), shared with other automation sessions. Desktop tests are opt-in (`GCAM_UI_TESTS=1`). Every desktop run needs the author's go for that run (`desktop-go`, the author's standing rule). Each run is announced to the other sessions with its start and end times, and pauses when another session announces an audit |
| G1 lists | `tests/Gcam.Studio.UiTests/Harness/Isolation/`: `surface-list.json` (write surfaces and their isolation), `classification-rules.json` (observation scope, shell record locations, OS run records), `action-policy.json` (allowed actions and launch values), `test-data.json` (test data and the peer state). A change there is an isolation change: a new observed run re-establishes G1 |
| Test data | There is no user import slot. Studio reads two fixed model inputs: the deployed ambient JSON (SHA-256 `4d48bc42…`) and the embedded readout offer resource (`24b3bf67…`). The harness scripts type every scene (`harness-typed-scenes`, synthetic). The pre-launch check refuses a deployment whose file set or data hashes differ from `test-data.json`. The deployment fingerprint covers DLL, EXE and JSON files only |
| Allowed peers | None. Connections are not blocked technically: a firewall rule needs elevation and changes an OS setting on every run, and an AppContainer launch would change the environment under test. This is a charter exception (`test-data.json` `peers`). Evidence: `SurfaceSurveyTests` finds no network API in the Studio shell assembly, and the owned-instance connection sampler checks the TCP / UDP tables in every launch against a harness positive control. Residual risk: a native framework or driver connection that opens and closes between two samples |
| Manual or excluded | Manual: keyboard-only and screen-reader flows, and gesture thresholds (inspection). Excluded: the light theme beyond its label, display scalings and cultures other than the test machine's, and screenshots or pixels as pass / fail criteria. Kept in code but unused: the background-filter machinery (no active filter), the conditional scope, and the kernel-trace diagnostic |
| Observation | Every launch runs the process ledger (the owned image and its descendants), the liveness observer, the Documents watcher on the declared export pattern (with a positive control created and removed by its own run), the connection sampler, and a read-back of the sandbox writes. Observed runs add snapshots A / B / C, classified attribution first (§1). A channel that cannot cover its scope is a run issue (`blocked`), never "no effect" |
| Crash and hang detection | In the harness (`Harness/Observation/LivenessObserver.cs`, wired through `OwnedStudioProcess` and `Scenario`), from launch to exit. Codes: `not_responding` — the main window does not answer a bounded `WM_NULL` for **10 s continuously** (provisional: about twice the largest simple-UI gap, 4.6 s in 134 passed runs; a breach is reported, the limit is never enlarged; time before the main window exists does not count); `app_exited` — Studio ended before the harness asked it to close, also with exit code 0; `abnormal_exit` — a non-zero exit code; `launch_window_timeout` — the main window never appeared. Each fails the scenario even after correct assertions. Each is a product failure, never a run issue. There is no Application-log correlation (lighter cut L1): an unhandled exception shows as one of these endings |
| Input after a failure | The guard is checked immediately before every raw input, and no key, click or move follows a recorded product failure. One exception (author, 2026-10-22): when the failure falls between the harness's own button-down and its button-up, exactly one matching button-up is sent. It is sent only when the foreground window belongs to the owned Studio instance (PID and start time) and the cursor is inside its main window. Otherwise nothing is sent, and the record says the button may still be pressed (a `physical` act releases it) |
| Video criteria | Every app-driving desktop test is recorded. After the app exits, the video is machine-checked (`VideoChecker`): it decodes to the end without error; its decoded duration matches the independently recorded interval within 0.2 s; undeclared single-colour frames last ≤ 0.2 s at a time and make up ≤ 1 % of frames; no decoded timestamp gap exceeds 0.3 s; 100 % of executed step IDs map to a timed caption or a declared privacy interval. A privacy interval covers the native save dialog. It is declared with its start, end, reason and step IDs, it must match the masked samples exactly, and it has no share cap. It ends when no owned modal remains and the export command has returned. The video never changes a scenario's verdict; G5 refuses a passed result whose video did not pass |
| Video exceptions | `ObservedRunTests`: recording would break the observation boundary. `PlotViewTests` is outside the app-driving scope (an in-process control benchmark) |
| Real-world effects | E6: an export scenario's typed file name enters the Windows save-dialog history and the recent-document lists. It is allowed only by a named `real-world-effect` permission for that run (D4 (a), 2026-10-21), usually given in the same statement as the desktop go. Any other effect outside isolation ends the run `unsafe_effect` |
| Harness markers | New v4 run IDs begin with `gcam-`, so the marker is unique to this product (the procedure's marker-uniqueness rule). Old records keep their IDs |
| People | Four acts, recorded by name in the approval package: `charter` (this charter, per target commit), `real-world-effect` (E6, per run), `final-review` (the generators' final-review list) and `physical` (an act only a person can do, such as a click that releases a pressed button). The author's `desktop-go` per run is added to them. The runner never asks the author to operate Studio. Committed records with HUM-xx acts replay unchanged |
| Success criteria | The product's own result (run state, rows, values, files) matches an independently computed expectation, there is no product failure, and the run's records and video pass their checks |
| Artefacts | Run records and manifests: `%TEMP%` run folders (machine-local). Observed runs: `%TEMP%\gcam-todo43\<runId>`; snapshots stay local (A4). Videos: `artifacts/ui-videos/` of the main checkout (git-ignored, with the index and the result cache). Committed: test-record milestones (`samples/testing/records/`), manual editions (`docs/manual/editions/`) and [VV.Studio.History](VV.Studio.History.md) |

## 1. Safety

**Write surfaces.** Searched every project Studio loads (`Gcam.Studio*`, `Gcam.Simulation` and its dependencies)
for file, directory, registry, environment, process, network, clipboard and mutex APIs. Only
`ConfigLoader.Save` / `Load` and `Waveform`'s JSON read use the file system, and neither is reachable from Studio
(CLI paths). Studio keeps no settings: the theme choice is not persisted. Since TODO-46 (2026-10-15) Studio has one
write surface, the report export: it writes the path the user confirms in the save dialog (HTML, PDF, Word or CSV),
`<name>.scene.json` beside it and, for event data, `<name>.meta.json`, and nothing else (`ReportFileWriter`).

| Surface | Declared | Resolved in a run | State |
|---|---|---|---|
| User / app data, settings, logs, caches | none in code | `TEMP` / `TMP` (and env-var readers of `APPDATA` / `LOCALAPPDATA`) redirected into an owned sandbox folder; **0 files written** in every run so far, apart from the export scenario's two declared report files (row below) | confirmed absent; the redirect is a partial boundary — see below |
| Report export (TODO-46) | the confirmed path and its declared sidecars (`.scene.json`; `.meta.json` for event data), chosen in the save dialog; Studio remembers no folder or setting, but the Windows save dialog records the file in the user's recent list and the last folder per application (E6, observed 2026-10-16; under v4.0 a real-world effect allowed per run by a named permission, §0) | `ReportExportScenarioTests` (one case per format) types a path inside the owned sandbox; the run asserts exactly `report-<run-id>.<ext>` and `report-<run-id>.scene.json` there (plus `report-<run-id>.meta.json` for CSV) and nothing else in the sandbox; the run ID in the typed name is a harness marker (TODO-52 D4) and is never shown in a manual figure | declared and isolated; observed runs passed 2026-10-15 (all four formats in run 6) |
| Network | none in code | no TCP connections observed from the app's PID during an audit run | confirmed absent |
| Child processes | none in code | none observed | confirmed absent |
| Shared resources | the interactive desktop and the mouse | one run at a time (`DisableTestParallelization`), opt-in | blocked by policy |

**What the redirect does not cover.** `Environment.GetFolderPath(ApplicationData)`, isolated storage and other
known-folder lookups resolve through the shell's known-folder registry, not the process environment, so they would
still reach the real profile. The boundary for those is the source survey (no such call is reachable) plus the
audit — not the redirect. If Studio ever gains settings or a cache, isolate them through an explicit path the
harness can set, and re-run this stage. The report export is that case: its explicit path is the one the user (or the
harness, acting as the user) types into the save dialog.

**Audit run.** Before any automation: the app started (sandboxed) and closed with no UI interaction while
child processes, TCP connections and the sandbox were watched. Nothing was written, started or connected. A global
before/after file diff was tried first and rejected: other processes on the machine write to the same folders, so it
cannot attribute writes. The owned sandbox can.

**Observed runs: attribution first** (procedure v4.0; TODO-54 R1, R2; contract `v4`). An observed run (`ObservedRunTests`,
the observed manual batch) compares snapshots A, B and C (`StateSnapshot`). `stage0.py diff-classify` first sorts each B→C
change into one of three groups:

1. **Harness operations.** The run's harness-owned locations: sandboxes, run folder, lease, registry marker and the
   Documents control.
2. **Studio-attributed effects.** Any change that meets one of three conditions:
   - It lies in Studio's product-specific scope: a write surface's declared default, this run's resolved value or its
     isolation location (`surface-list.json`), or a shell record location of data Studio hands the shell
     (`classification-rules.json` `shellRecordLocations`: the save-dialog history and the recent-document links).
   - It carries a harness marker: the run ID, in a path, a name or registry value data. The snapshot flags a marked value
     as it reads it, because the stored hash hides the data afterwards.
   - It belongs to an owned process (from the process ledger).
3. **References.** Everything else. A change in a shared location such as Documents, or in the E1 / E3 framework locations,
   is a `shared-location-reference`; any other is an `unattributed-reference`. References are counted per channel and
   listed, redacted, in a local file beside the result. They never block.

An attributed effect is then judged on three grounds: is it inside isolation, does the registered child / peer policy cover
it, or does an exact `real-world-effect` permission cover it? That permission must match the run's package, the author's
statement and its scope; a covered effect is `authorized-real-world-effect`, which is compatible with `completed`. Any other
attributed effect is `unsafe_effect`. In a new run, no outside write is permitted merely because it is declared.

The following only annotate references and can never clear an attributed or marked item: time windows, the A→B control
window, the null window, git working trees, recorded filters and the OS-run-record rules.

The run's eligibility combines the snapshot verdict with the run manifest's process issues, monitor errors, live-channel gaps,
product failures, and the transient effects the Documents watcher saw. An item that no rule decides is re-judged from the
run's own records; a rerun comes only when the records cannot answer, and a run is not attempted a third time: the cause is
recorded for the author. Observations without a contract (before TODO-54) keep their historical judgement and replay
unchanged. The v3.3 / v3.4 rules they were judged by — the HUM-08 classification of undecided candidates, the "recorded
difference" and the filter re-classification of `manual-20261016-02` (RC-11) — are in this document's git history before
TODO-54.

| Blind spot | Residual risk |
|---|---|
| A Studio effect outside the scope without a marker (framework registry, shell cache, crash locations; E1 / E3) | a framework or driver write there is a reference, not seen as Studio's. Studio's managed code has no write except the export, no network and no child process (`SurfaceSurveyTests` checks the Studio shell assembly); what remains is native framework and driver behaviour, of the kind seen as E1 and E3 in the 2026-10-14 kernel traces |
| Jump lists; the per-extension recent-documents lists | no name pattern of Studio's own (application-ID file names; lists shared with every program); covered only by a marker in a name or in registry value data |
| A marker inside file content | files are matched by name only (content scanning is out: cost) |
| The Documents watcher | a junction under the Documents root is a named blind spot; a change made and undone between two watcher events is seen as transient, not missed, unless the watcher's buffer overflows (then the run is `blocked`) |
| Snapshots taken before TODO-52 | no value-data marker flags; names are still matched |

**Ownership** (`Harness/StudioProcess.cs`).

- A `Gcam.Studio` process that the run did not start is a conflict: the run fails and never attaches to or kills it.
- The run records PID, start time, image path and product version; at the end it requests graceful window closure
  of **that PID only** and records how it ended. After 5 s it reports a still-running process; it never force-kills.
- The sandbox carries an owner marker and is retained under `%TEMP%/gcam-uia-*` for audit. Deletion needs author approval.
- `GCAM_UIA_RUN=<runId>` is passed to the app so a later restart could be tied to the run.

**Build identity.** A build is determined by its source (TODO-51 step 1): the informational version is `1.0.0`
without the commit and the PDBs carry no source-control data, so a docs- or test-only commit leaves the deployment
fingerprint unchanged. The fingerprint is the same per checkout folder, not across worktrees (the PDB path is embedded).
Each manifest records the source commit and the number of dirty files in the working tree; a binary maps to its source
by a rebuild with the same deployment fingerprint.

## 2. Measurement

Measured on 2026-10-01 against the running app (Windows 11, ko-KR, 1440×900 window).

**Tree facts.** 75 elements in the control view; 26 AutomationIds, all unique. Names are **not** unique: a panel
title and its heatmap are both "Detector flood map", a field label and its combo box are both "Isotope". Selectors
therefore use AutomationIds and require exactly one match.

| Contract | Implementation | Find | Act | Read back the input | Judge the product's handler |
|---|---|---|---|---|---|
| Button | `Button` (Add / Remove source, Run, Cancel, Clear, Delete, theme) | AutomationId | `InvokePattern.Invoke` | — | the effect, e.g. source rows 1 → 2 |
| Tool toggle | `IconToggle` (a `RadioButton`, class name unchanged) with `RadioButton.Icon` (Pan / Distance / Angle / ROI: `ToolPan`, `ToolDistance`, `ToolAngle`, `ToolRoi`; a segmented `RadioButton.Segment` until 2026-10-13) | AutomationId | `SelectionItemPattern.Select` | `IsSelected` | tool hint text changes |
| Text field | `TextBox` (X, Y, distance, activity, photons) | AutomationId | `ValuePattern.SetValue` | `ValuePattern.Value` | **only after focus leaves** — the binding commits on LostFocus; the source row label then updates |
| Combo | `ComboBox` (isotope) | AutomationId | `ExpandCollapse.Expand` → item `Select` → `Collapse` | selection | source row label |
| List row | `ListBoxItem` (sources, measurements) | list AutomationId → children | `SelectionItemPattern.Select` | `IsSelected` | row name is the item's description |
| Heatmap | `HeatmapView` (custom peer, type Image) | AutomationId | focus + keys (`+`, `−`, arrows, `0`) | — | `ItemStatus` ("zoom 1.3x; x … mm …") |
| Overlay | `MeasurementAdorner` | **not in the tree**; position from the heatmap's rectangle | real pointer input (`SetCursorPos` + `SendInput`) | cursor position | a new row in the results table |
| Async run | Run → `StatusText` | AutomationId | `Invoke` | — | `ItemStatus` Idle → Running → Succeeded (≈ 0.7 s at 500 k photons) |
| Cancel | Cancel button, present only while running | AutomationId while Running | `Invoke` | — | `ItemStatus` Cancelled within 0.1 s; peak chip unchanged; Cancel leaves the tree |
| Modal layer | `ModalHost` in-window layer (Reset confirmation, 2026-10-12; Options dialog, 2026-10-13) | `Modal.Layer` in the same window's tree (no second window), Name = the hosted content's title | its buttons by AutomationId (`Modal.Cancel`, `Reset.Discard`, `Options.Apply`, `Modal.Close`) via `Invoke`; harness `ConfirmReset(bool)` | layer present / gone | Cancel keeps the snapshot and the lock; Discard returns to "Ready". The harness closes an open layer before graceful shutdown (`CloseModalLayer`), and `Normalise` refuses to run with a layer open |
| Options dialog | `OptionsView` in the modal layer, opened by the top-bar cog `OpenOptions` (its HelpText is the Options summary, "Options: defaults" otherwise); tabs `Options.AcquisitionTab` / `DetectorTab` / `EnvironmentTab`; fields keep their former ids (`AcquisitionSeed`, `Chain.*`, `Detector.Gap` / `GainSigma` / `GainSeed`, `Acquisition.AmbientDose` / `AmbientBound` / `Background`); `Options.LockCaption` while locked | AutomationId inside the layer | harness `OpenOptions(tab)` (Invoke the cog, wait for the tab, check the layer's Name, Select the tab), `SetOption(id, value)` (open the field's tab, type, commit by moving focus to `Options.Apply`, `ApplyOptions`), `ApplyOptions()` (Invoke `Options.Apply`, wait until the layer closes), `CancelOptions()` (`Modal.Close` when locked, else `Modal.Cancel`), `OptionEnabled(id)` / `OptionValue(id)` (open, read, cancel) | the field's `ValuePattern` / `IsEnabled` after reopening; `Options.Summary` in the status bar | an applied value appears in the summary and the cog's HelpText; a cancelled draft changes nothing; with data the fields are disabled and only Close shows |
| Left tab | `TabItem` `Left.SceneTab` / `Left.OpticsTab` / `Left.DetectorTab` | AutomationId | `SelectionItemPattern.Select` (harness `SelectLeftTab(name)`); `ById` first selects the tab owning `Optics.*`, `Readout.*`, `Detector.Section`, `Source*`, `AddSource` / `RemoveSource` (also for `Readout.*` ids of the Detector workspace — a harmless extra selection) | `IsSelected` | the tab's controls become reachable (WPF builds only the selected tab's content) |
| Caption | `CaptionBar` `Window.TopBar` (Pane) and the caption buttons `Window.Minimize`, `Window.Maximize` (Name "Restore" while maximised), `Window.Close` | AutomationId | `InvokePattern.Invoke` | `WindowPattern.WindowVisualState`; bounds | maximise fills the monitor work area; restore returns; no element of control type `TitleBar` exists in the window |
| Status bar | `Window.StatusBar` holding `StatusText` (run state as `ItemStatus`, a refusal or the preparation outcome as text), `Options.Summary`, `AcquisitionLiveTimeError`, `AcquisitionSeedError`, `RunProgress` / `Readout.Progress` (only while their run is active) | AutomationId | — | Name (a TextBlock's name is its text) | one line in every state |
| View tabs | Detector `Detector.Views` (`Detector.FaceTab`, `Readout.SipmTab`, `Readout.AngerTab`, `Readout.CalibrationTab`, `Readout.DiagnosticsTab`); Waveform `Waveform.Views` (`Waveform.EnergySumTab`, `Waveform.FourOutputsTab`); under DirectCrystal a readout view shows `Readout.EmptyState` and the button `Readout.ChooseReadout` | AutomationId | `SelectionItemPattern.Select`; Choose readout by `Invoke` | `IsSelected` | the view's surface appears; Choose readout selects `Left.DetectorTab` and focuses `Readout.Mode` |

**Product defects found by this measurement (fixed in the same change).**

- Source list rows were announced as `Gcam.Studio.Core.ViewModels.SourceItemViewModel` (no name on the container).
- The status line, tool hint and measurement detail had a fixed `AutomationProperties.Name` that **hid their text**:
  the polite live region announced only "Status".

**Pointer coordinates.** The test process makes itself per-monitor DPI aware, so UIA rectangles, `SetCursorPos` and
the screen are all physical pixels. The window is normalised to 1440×900 before measuring.

**Borderless window (2026-10-13).** The window has no OS title bar: the top bar is the caption and starts at the
window's top edge, so every position comes from the target element's UIA bounds, never from the window rectangle plus
a title-bar offset. `ChromeScenarioTests.BorderlessCaption_HasNoNativeTitleBar_AndStartsAtWindowTop` asserts that no
`TitleBar` element exists, that `Window.TopBar` and `Window.Close` sit within 1 px of the window's top and right
edges, and that Maximize fills the monitor work area; the pilot checks that the caption buttons lie inside the owned
frame and above the flood map. After the geometry change both were redone on 2026-10-13: the no-title-bar scenario and
the pilot passed, and the pilot's broken-verdict / recovery pair behaved as intended (VV.Studio History, "Desktop
verification — declutter stage 2 and TODO-42").

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
| `StopContinueReset_KeepsAccumulatesAndDiscards` | VAL-02 · SR-RUN-10, SR-RUN-12, SR-RUN-23, SR-RUN-26 | Stop freezes counts, Continue grows them, Reset discards; inputs lock until Reset | pass 2026-10-02 |
| `Roi_OnFloodMap_CountsWholePixelsByCentre` | VAL-03 · SR-MEAS-03 | pixel count by centre, with corners on cell **boundaries** so one pixel of error can't change it | 42 px, 3.6 × 4.2 mm |
| `Angle_EscAbandonsDraft_DeleteRemovesSelected` | VAL-03 · SR-MEAS-02, SR-MEAS-07 | angle from the three screen points (scale- and flip-invariant); Esc is proven by the value — an un-abandoned first click would have produced a different angle | 69.8° vs 70.02 ± 2.29; Delete removed it |
| `MoveSource_ResetEditStart_PutsPeakOnTheSource` | VAL-04, VAL-05 · SR-RUN-12, SR-RUN-26, SR-VIEW-08 | Reset, edit source, Start; unchanged existing 1.5 mm tolerance | pass 2026-10-02: (21.6, 17.1) peak vs (21.4, 16.0) source |
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

**Manual captures (TODO-43).** The manual scenarios (`ManualScenarioTests`, `[ManualChapter]`) also write a manual run
record and captures for the Studio manual. A capture is document evidence for an edition, never a verdict: one that fails
its quality check (owned, foreground, unoccluded, expected size, stable, no account or machine name in the window text)
keeps no pixels and becomes a missing figure, not a product failure. Marks are UIA bounding rectangles measured at capture
time and converted to image pixels; their label is the visible text, their target the AutomationId. The generator
(`samples/manual/manual_records.py`) prints a chapter only from the latest run on the edition's build.

**Video of each desktop test (TODO-48).** `Scenario.Run` / `RunReportExport` / `ManualScenario.Run` record the owned
Studio window while the test drives it (`Harness/VideoRecorder.cs`): a capture thread at a 100 ms period and an encoding
thread (Media Foundation sink writer, RGB32 → H.264, 2 Mbit/s, built into Windows), at most one pending and one active
frame, media time from a `Stopwatch` with dropped slots kept as gaps. Recording starts after the window is normalised and
its start state checked, so a launch failure has no video; pixel capture stops before Studio is closed.

| Item | Rule |
|---|---|
| Opt-out | `GCAM_UI_VIDEO=0` creates no thread, file or COM object; pass / fail / skip are unchanged |
| Not recorded | `ObservedRunTests` (the observation boundary: launch, idle, exit without automation) and `PlotViewTests` (a redraw-timing benchmark the recorder would perturb). `VideoModelTests` fails when a new desktop method neither goes through a recording wrapper nor is one of these two |
| Picture | the window's visible frame (DWM bounds) copied from the screen into a fixed even-sized canvas set at start; a resized window is letterboxed into it; the cursor is drawn in. The outer two pixels are always neutral: the DWM bounds can exceed the window's painted pixels by one, and the first desktop run (2026-10-16) showed glyph tops of the terminal behind Studio in that edge. A popup of Studio reaching outside the frame is clipped (in-window layers such as Options and Reset are inside it) |
| Foreign windows | the window stack above Studio is surveyed just before and after each frame; every other process's window over the frame is filled black and counted (`maskedFrames`, `maskRanges`); a failed survey gives a whole black frame |
| Private dialog | while `RunRecord.OmitWindowDiagnosticsReason` is set (the native save dialog of the export), frames are neutral and step captions are dropped except one generic caption, although the dialog belongs to Studio |
| Captions | `<case>.vtt` from the scenario's steps (`RunRecord.Step`, the reader text of `ManualRun.BeginStep`), escaped and stripped of paths and identities; polling diagnostics stay in the run record only. Captions are an aid, never a verdict |
| Promotion | only after the test's final verdict — after cleanup, the exact sandbox writes and, for a manual chapter, its finalised record — and only for a pass: `artifacts/ui-videos/<case>.mp4`, `.vtt`, then `index.json` last (schema `gcam.ui-videos` 1: run id, commit, dirty flag, binary and deployment hashes, file hashes, frames, masked and suppressed frames). Each file is copied to a staging name in that folder and replaced on its own; a run interrupted between them leaves a hash mismatch that the next publication drops from the index |
| Failure | a failed, skipped or aborted test keeps the previous passing video; its attempt (`attempt.mp4`, `attempt.vtt`, `video-status.json`) stays in the run record folder under `%TEMP%` |
| Faults | an unavailable encoder, a capture or COM error, a stop that exceeds 5 s (the recording is abandoned; the threads are background threads) or a publication error never changes the scenario's verdict: a passing test can show `video unavailable` in its status file, so count videos from `index.json`, not from passes. Under v4.0 the generators (G5) refuse to produce a passed desktop result whose video is missing or failed its check, and list the attempt for final review |
| Case id | class, method and theory arguments, made file-safe with a short hash (`ReportExportScenarioTests.ExportReport_….html-<hash>`) |
| Manual record | a chapter run pins its own encoded video and captions by hash (`manual-run.json` `video`, optional); replay of an edition never needs the video |

Limits. The two surveys bracket the screen copy but cannot see a window that appears and vanishes between them; only a
compositor-level capture could exclude that race. `artifacts/` is git-ignored and inside the working tree: `git clean -fdx`
deletes it, and a run in a worktree writes that worktree's folder and goes with it, so only runs in the main checkout keep
their videos. The videos are local derived files, never committed and never evidence on their own. One exception, as documentation:
three clips converted to GIF for the README demo (`docs/assets/studio-demo/`, with their test case, run and build), replaced
only when the screens they show change. An observed batch lists
`artifacts/ui-videos` among the harness-owned destinations (`surface-list.json#harness-ui-videos`).

**Result cache (TODO-51).** A desktop case can reuse its latest pass instead of driving Studio while nothing that can
change its outcome has changed (`Harness/ResultCache.cs`). Every desktop case is discovered as a cache-aware xUnit case
(`DesktopFact` / `DesktopTheory` → `CachedDesktopTestCase`, one entry per theory row); the decision is taken before the
case starts, so a reused case never launches Studio.

| Item | Rule |
|---|---|
| Opt-in | `GCAM_UI_CACHE=1` enables reuse. Unset (the default) runs every case as before; the test-record collector sets `GCAM_UI_CACHE=0` and refuses a record collected with the cache on |
| Key | the deployment fingerprint of the Studio folder; the live hashes of the sources the class's catalog entry lists in [VV.Tests](VV.Tests.md) (for a UI test class that includes every harness file and the isolation folder's rules; no entry, no reuse; a listed hash that differs from the catalog, no reuse), plus the own files of every other test class those own files name, transitively (`ManualScenarioTests` calls `WorkspaceScenarioTests` helpers, which use `PilotTests`); the isolation-folder fingerprint; the OS build (with the update revision), the normalised window size, the harness's per-monitor-v2 DPI awareness and the display scaling, the formatting and UI culture; the desktop opt-in switches (`GCAM_UI_TESTS`, `GCAM_UI_LONG_TESTS`, `GCAM_UI_PROBE`, `GCAM_README_CAPTURE_ONLY`, each "1" or "off"). `GCAM_UI_VIDEO` is not part of it: recording never changes a verdict |
| Record | after the case's final xUnit outcome (every cleanup assertion, the exact sandbox writes and, for a manual chapter, its finalised record): a pass records or refreshes `artifacts/ui-videos/cache.json` (schema `gcam.ui-cache` 1: case id → source run id, pass time, key, OS build, key components), also with `GCAM_UI_VIDEO=0`; a failure removes the case's entry, so an older pass never stands for a newer failure. A skipped or aborted case writes nothing; a run with `GCAM_UI_BREAK_VERDICT=1` leaves the file untouched. Written like the video index: a staging copy in the same folder, then a replace. A sibling file rather than a field of `index.json`, because index entries exist only with a published video |
| Reuse | a key equal to a pass younger than 7 days on the same OS build: the case ends as an xUnit **skip** with the fixed reason `[cache-reused] reused from run <runId> (key <first 12 hex>)` — reported as not executed (test records: reason code `cache_reused` with `sourceRunId` and `cacheKey`), never as a pass. An unreadable cache file reuses nothing |
| Never reused | `ObservedRunTests` (the observation itself) and `PolishSurveyTests` (it exists to rewrite the committed screenshots) — neither reused nor recorded; any case while `GCAM_UI_BREAK_VERDICT=1` or while `GCAM_UI_OBSERVE_DIRECTORY` is set (the observed manual batch). `ManualScenarioTests` outside an observed batch may reuse; `PlotViewTests` and other cases without a run record are never recorded, so never reused |
| What would run | `dotnet test tests/Gcam.Studio.UiTests -c Release --filter "FullyQualifiedName~ResultCacheTests.Plan_" --logger "console;verbosity=detailed"` lists every desktop case with what `GCAM_UI_CACHE=1` would do: `reuse` with the reason, or `run` with why (no recorded pass, expired, or the key components that changed — values for OS, display, culture and switches, names for the hashed ones). Headless and read-only; a C# test so that it computes the key with the harness's own code |

Limits. The cache trusts the key: an outcome that depends on something outside it (machine load, another window, a
flaky timing) is reused as it last passed for up to seven days. A test-project file outside the class's catalog sources
(the project file, `AssemblyInfo.cs`) is not in the key. Like the videos, the file is repository-local and git-ignored,
and a worktree has its own.

## 6. Operation

- `GCAM_UI_TESTS=1 dotnet test tests/Gcam.Studio.UiTests -c Release` — only when the desktop is free.
- **Desktop session under v4.0** (each run on the author's go, announced to the other sessions):
  1. Commit, build (`dotnet build Gcam.sln -c Release`) and make a fresh run folder `%TEMP%\gcam-todo43\<runId>` with a
     new ID `gcam-<kind>-YYYYMMDD-NN`.
  2. `stage0.py pin --out <runDir>\pin.json`, then the approval package `<runDir>pprovals.json`: `contract: "v4"`, run ID,
     commit and commit date, isolation and deployment fingerprints, and the named acts `charter` and `desktop-go` (the
     author's statements, quoted after the commit; one statement may carry both, and an export run's `real-world-effect`).
     For production also the charter scope, scenario IDs and gates G0 / G1 with status and reason. Personal statements and
     paths stay local.
  3. `stage0.py preflight --run-directory <runDir>` must pass before the launch.
  4. G1: the observed run (`GCAM_UI_TESTS=1`, `GCAM_UI_PROBE=1`, `GCAM_UI_PROBE_RUN`, `GCAM_UI_PROBE_DIRECTORY`,
     filter `ObservedRunTests`), then `stage0.py diff-classify`. Then G3 (pilot ×2, broken verdict, recovery), then the
     affected scenarios with videos; the result goes to [VV.Studio.History](VV.Studio.History.md).
  Without the variable the desktop tests are **skipped** and reported as skipped, never as passed.
- Every recorded desktop test leaves its latest passing video in `artifacts/ui-videos/` (§5); set `GCAM_UI_VIDEO=0` to
  run without recording.
- A regression run after a commit that touched no product source can add `GCAM_UI_CACHE=1`: cases whose key is unchanged
  are reported as `[cache-reused]` skips and only the rest drive Studio (§5, "Result cache"); list them first with the
  `ResultCacheTests.Plan_` case. A run for evidence (a test-record collection, an observed batch, a broken-verdict check)
  never reuses.
- An observed run (the launch-idle-exit probe, an observed manual batch) is reused while its `validFor` holds. With
  source-determined binaries a new one is needed only when the deployment fingerprint changes (product source, build
  settings or package versions — or a different checkout folder, since the PDB path is embedded), the isolation folder
  `Harness/Isolation` changes (rules, surface list, action policy, scripts), or the registered images change; a docs- or
  test-only commit does not need one.
- Long scenarios need `GCAM_UI_LONG_TESTS=1` as well: today only `ReadoutScenarioTests.HighResCold_…` (one cold 64 k
  High-res 661.7 keV preparation, ≈ 45 min, ≈ 6 GiB). Ordinary desktop runs skip it; run it alone, on request.
- Runs are serial. A run that fails is not retried automatically; read its manifest first.
- CI (`.github/workflows/ci.yml`) runs the oracle tests but not the desktop tests — it has no interactive session. Say so in its
  summary rather than reporting the UI suite green.
- Re-measure (§2) and re-pilot (G3) after changing AutomationIds, the window layout, the adorner's input
  handling, or the test machine's display scaling. Due now: the borderless window, the Options dialog and the left tabs
  (2026-10-13) changed the layout and moved controls; the desktop suite has not been run since.

## Remaining coverage

- Heatmap zoom / pan / readout scenarios (SR-VIEW-03, -04, -07) through keys and `ItemStatus`.
- Controls absent from the final scenario assertions (Remove source, Clear, Delete button, source list selection), plus SelectionItemPattern activation and keyboard-only/screen-reader flows.
- A keyboard crosshair for creating measurements (AN-01), then a keyboard-only scenario.
- Name regression as a standing result use (§0) once the suite has a run policy owner and history.

Keep screenshots diagnostic-only; judge on product state.

## Final four-workspace desktop pass (2026-10-02)

Release at b8fddcd plus the changes in this pass. New scenarios are in `WorkspaceScenarioTests`; every acquisition
fixes seed 12345 through `AcquisitionSeed`. Window size is 1440×900, default detector / chain; acquisitions use
60 s at ×10. The pair scene has Cs-137 (−20, 0) mm / 500 µCi and Co-60 (20, 0) mm / 20 µCi at 1000 mm.
Each scenario starts a fresh owned process. The harness now retains its sandbox and never force-stops a process.

`AutomationEvidence` supplies read-only numerical JSON through the image peers' HelpText only when
`GCAM_UIA_RUN` identifies a harness-owned process. Ordinary accessibility help remains unchanged. Evidence includes
the histogram bound to the plot, acquired arrival times, bound marker positions, retained floods, bound reconstruction
and found markers, calibration counts, and focus samples / bound bands. It contains no test expectations and cannot
mutate the product. `WorkspaceOracle` independently computes the arithmetic; three plain tests exercise it without
desktop input. Failure images remain diagnostic-only.

| Scenario | Independent oracle and measured result | Requirements |
|---|---|---|
| `Spectrum_BandCountAndWindowChange_MatchRetainedHistogram` | Sum histogram bins whose centres lie in the 661.7 keV band; UIA emission row agrees: N=1 → 1,143 counts, N=2 → 1,373. Window width scales with N; acquired histogram is unchanged and matches the bound plot. | SR-SPEC-05, -06; SR-IMG-01 |
| `Waveform_SelectedEventListAndMarkers_MatchArrivalWindow` | Filter acquired times within [trigger−0.2W, trigger+0.8W); event #10 is the sole event at W=10 µs. List indices and both plots' marker labels / relative positions agree. Rate-study toggle publishes its 50 kcps label without changing acquired times. | SR-WAVE-01, -02, -03 |
| `Detector_FaceBeforeStart_LockedUntilReset` | Default inputs imply 30²=900 crystals on an 18 mm face, visible before acquisition. After acquisition the caption says acquired / locked until Reset; gap, gain and source fields lock; Reset unlocks and restores the next-acquisition caption. | SR-DET-02, -04; SR-RUN-26 |
| `Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks` | All has two peaks; channel peaks are on their source-input sides. Find the reconstruction argmax independently; found / chip coordinates are within half a grid cell plus text rounding, and the bound reconstruction / markers match. Per cell, strip = max(0, raw Cs − (calibration low/high)·raw Co). R=6066/12867; stripped total 1,215.271158778 counts, exactly matching the independent sum. Peak value changes 506 → 502.228491490. | SR-IMG-03, -04, -05 |
| `Imaging_RefocusAndSweep_MatchProjectionAndHalfMaxInterval` | Focus 1000 → 500 mm halves recon step (2.1875 → 1.09375 mm), keeps flood/counts; independent grid maximum agrees with the chip. Derive contiguous raw half-max crossings from 81 curve samples and verify interval, censor flags and plotted band: 507.803601533–3000 mm, far edge censored. No depth-accuracy assertion. | SR-OPT-04; SR-FOCUS-01, -03 |

Workspace tests click the workspace selector and wait for its surface, never treating a checked button as proof of
activation. The earlier SelectionItem defect and `Ctrl+1…4` command workaround remain relevant: this pass verifies
the pointer command path; it does not establish a keyboard-only or screen-reader flow.

Diagnostic survey: all four workspaces × both themes × 1280×800 / 1440×900 = 16 visible-frame PNGs,
opened and inspected; reproduction and P-12 … P-16 observations in
[survey README](assets/studio-polish-survey/README.md). No layout fixes. Representative two-isotope dark Imaging
capture: [studio-desktop-imaging.png](assets/studio-desktop-imaging.png). The README image was then a separate capture
with Co-60 at 200 µCi (found peaks Cs-137 (−20.6, 0.3), Co-60 (20.2, 0.5) mm); since 2026-10-13 it is a copy of the
survey's `imaging-dark-1440x900.png`.
Run `20261002-130331-aa0ebe` passed the truth-distance check before writing the capture; 6,974 acquired counts,
exit 0, no sandbox writes. Reproduce only this image with `GCAM_UI_TESTS=1`, `GCAM_README_CAPTURE_ONLY=1` and
`dotnet test tests/Gcam.Studio.UiTests -c Release --filter FullyQualifiedName~PolishSurveyTests`.
Ordinary survey execution writes only its 16 survey frames so it cannot overwrite the README's distinct scene.

**Observed presentation defects (survey record, before subsequent polish).** `Views/SpectrumView.xaml` uses adjacent fixed-width, right-aligned
Line and Window columns: run the pair scene, open Spectrum at either size; the Co-60 values touch (P-12).
`MeasurementOverlay` found labels crowd at 1280×800 in that pair scene (P-13). `WaveformView.xaml` omits marker labels
on the shaped plot while retaining marker lines (P-15); this is a display choice to review, not a numerical defect.
No transport, reconstruction or acquisition defect was found by the new scenarios.

Subsequent polish addresses P-12 with shared Auto-sized Spectrum columns and P-13 with deterministic overlay chip placement (OverlayLabelLayout). The shaped waveform plot still omits marker labels by design. These code changes do not imply a new desktop run; the survey observations above remain the dated record.

**Execution record:** final desktop recovery 27/27 (13 oracle, 12 scenarios, plot gate, survey); deliberately
broken run 12/12 scenario failures, all at the corrupted expectations. Ordinary suite: 262 / 164 / 72 / 13
passes, 22 explicitly opted-out cases. Build: 0 warnings / errors. Plot CPU redraw max: zoom 5.5822 ms,
resize 3.9559 ms, both below 16 ms. Resize event-to-render samples were 238.0788–595.2849 ms; this is
not a ≤16 ms end-to-end latency claim. Every owned app exited 0, zero sandbox writes, no app left running;
owned sandboxes retained. Compact record and capture hashes:
[desktop evidence](assets/studio-desktop-evidence.json).

**Re-run on list-mode acquisition (2026-10-01).** The scenarios moved from Simulate / Cancel / photon budget to
Start / Stop / live time (`StudioWindow.Acquire`, status counts parsed from the status line); the cancel scenario
became `Stop_KeepsAcquiredData_LocksThenUnlocksScene`. Normal run: 19 / 19 (10 oracle, 7 desktop scenarios, the
plot gate, the polish survey). Broken run (`GCAM_UI_BREAK_VERDICT=1`): all 7 desktop scenarios fail at their
corrupted assertions. Two findings: the source-drag localisation passed at 1.43 mm against a 1.5 mm tolerance
(in-crystal Compton scatter is now in the image and the peak is the grid argmax — a thin margin, not a pass to rely
on), and **UIA Select on the workspace switch checks the button without changing the workspace** (only a click or
`Ctrl+1…4` runs the command) — an accessibility defect; the survey switches with `Ctrl+2` until it is fixed.
