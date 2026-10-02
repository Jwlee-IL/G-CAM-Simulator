# PLAN.Studio.AcquisitionControl.Review — Start / Stop / Reset checked in the code, continuation measured

Scope: the substitute implementer's review of [PLAN.Studio.AcquisitionControl](PLAN.Studio.AcquisitionControl.md)
(TODO-24), 2026-10-02: the "What exists" rows and proposals F-1 … F-6 checked in the code, the session-continuation
design, a headless measurement of stop + continue against an uninterrupted run, recommended answers to the open
questions, the complete removal list for the stale / outdated / pending-vs-acquired concept, a proposed state table,
size and order. Review only: no source, test or other doc was changed. Baseline before any change (Release, headless):
`Gcam.Studio.Tests` 162 passed; `Gcam.Studio.Services.Tests` 67 passed, 7 skipped (evidence).

Verdicts: **confirmed** (row right), **corrected** (row right in spirit, facts different or incomplete), **new** (N-1 …).

## Summary

| # | Verdict | One line |
|---|---|---|
| Run state | confirmed, detail added | five states; Failed keeps `Result` / `Snapshot` and unlocks (sets `IsRunning = false`) |
| Session | confirmed, detail added | one-shot; the look-ahead event, the live clock, flood and events are **locals** of `ProduceAsync` and die with the worker |
| Stale | corrected — wider | 11 `MarkStale()` call sites incl. live time, speed and BSR; plus the Chain / Detector "Pending" lines and the Detector identity |
| Edit locks | corrected | only the chain setters and `OpticsEditor.IsEditable` lock in the ViewModel; every other input locks only through XAML `IsEnabled` |
| F-1 | confirmed with changes | states fine; "Failed with no data" needs a rule; name `Empty` vs today's `Idle` changes one desktop-harness line |
| F-2 | confirmed — **measured** | continuation reproduces the uninterrupted stream bit-exactly **iff the look-ahead event is kept**; services' caches work unchanged |
| F-3 | confirmed with changes | raise-only preset works (measured); SR-RUN-11 "progress never decreases" must change; Speed is pure pacing (measured) |
| F-4 | confirmed with one blocker | removal list below; **source drag becomes unreachable** under A-2 (N-1) — author decision |
| F-5 | corrected | no confirmation dialog (no dialog infrastructure; data are reproducible from the fixed seed); Reset not primary |
| F-6 | confirmed, extended | the measurement becomes a service test; 10 fake acquisition implementations constrain the contract change |
| N-1 | new | source-marker drag needs an image (`HasImage`), so with inputs locked whenever an image exists it can never run |
| N-2 | new | every acquisition uses seed 12345: Reset + Start with the same inputs reproduces the same events |
| N-3 | new | decoder focus does not change transport (measured): Continue after a focus change is the same physics |
| N-4 | new | `AsyncRelayCommand.ExecuteAsync` does not check `CanExecute`; tests call it directly — the guard must be inside |
| N-5 | new | `Imaging.Begin` freezes `_scene` / `_optics`; Reset must clear them or geometry keeps showing the old optics |
| N-6 | new | Start is `IsDefault="True"`: Enter in any field will **continue** an acquisition once Start means Continue |
| N-7 | new | desktop scenario VAL-02 asserts "Start clears" and the source-drag scenario re-runs after Completed — both contradict A-1 / A-2 |
| N-8 | new | `MainViewModel.Optics` has a public, unguarded setter; tests and `OpticsEditor.Changed` write it |

## "What exists" rows

### Run state — confirmed, detail added
`MainViewModel.cs` 13–20 (`RunState`), 214–224, 266–322. Facts the row leaves out:
- `StartAsync` validates first (275–283; gap, optics, scene, focus, `Imaging.FocusError`) and expands sections on error.
- It clears `Result`, `Snapshot`, `IsResultStale` (287–289) and calls `Spectrum.Begin`, `Imaging.Begin`, `Waveform.Begin`
  (293–295) — each makes a new acquisition identity (`Guid.NewGuid()` in Spectrum / Imaging).
- End state: `Snapshot?.IsCompleted ? Completed : Stopped` (309). On exception: `Failed`, status "Failed: …", and
  `finally` sets `IsRunning = false` — so after Failed the inputs **unlock** and Start is enabled; `Result` and
  `Snapshot` are kept (test `Failure_ReportsMessage_KeepsAcquiredData`).
- `CanStart() = IsIdle && Sources.Count > 0` (266); `Stop` CanExecute = `IsRunning` (268).

### Session — confirmed, detail added
`IAcquisitionSession` (`ReadSnapshotsAsync`, `Stop`, `IAsyncDisposable`); `AcquisitionSession.cs` 57–153. The worker
starts in the constructor (`Task.Run(ProduceAsync)`); `Stop()` cancels `_stop`; the loop publishes one terminal snapshot
and completes the channel; `StartAsync` disposes it with `await using` (298). Everything a continuation needs is
local to `ProduceAsync` (74–83): the `ListModeSource` (`using var`, disposed when the worker ends), decoder, flood,
event list, **the look-ahead `pending` event**, `live`, and the wall references `start` / `previous` /
`previousPublished`. `firstRefresh` forces `target = 0` (91). Config seed: `SceneConfigBuilder.Build(…, seed = 12345)`
— fixed for every acquisition (N-2). Background (BSR > 0) is supported now (`ListModeBackground`; R-5 of
PLAN.Studio.LiveAcquisition is done), with its own look-ahead inside `ListModeSource._pendingSignal`, which persists
with the source object.

### Stale — corrected (wider than the row)
Not only source / optics / detector / speed: `MarkStale()` is called from the optics-editor error path (45),
`OnOpticsChanged` (113), chain changes (150), gap (159), gain σ (177), gain seed (179), **BSR** (183), **live time**
(205), speed (211), source add/remove (329) and source edits (334). The concept also lives in the pending-vs-acquired
displays: `PendingChain` / `AcquiredChain` / `ChainDiffers` (139–148, 190; `ChainPanel.xaml` 28–34), the Detector
`Pending` line (`DetectorWorkspaceViewModel` 20; `DetectorPanel.xaml` 46–47) and `Identity`'s stale branch (18–19;
`DetectorView.xaml` 9–10, drawn in the warning colour — also L-15 of the batch-3 review). Full list below.

### Edit locks — corrected
- ViewModel-level: only `Scintillator` / `Sensor` / `Preamp` setters (`!IsRunning`, 126–136), `AddSource` /
  `RemoveSource` CanExecute (`IsIdle`), and `OpticsEditor.IsEditable = !IsRunning` (115), which also guards the preset
  selector (`OpticsEditorViewModel` 287).
- View-only (`IsEnabled="{Binding IsIdle}"`): live time and speed (`MainWindow.xaml` 102–108), selected-source fields
  (181, **via `RelativeSource AncestorType=ScrollViewer`** — an ancestor lookup the planning cautions flag), detector gap /
  gain / seed / BSR (266), chain combos (`ChainPanel.xaml` 9), source drag (`CanMoveMarkers="{Binding Shared.IsIdle}"`, 360).
- Unguarded in the ViewModel: `MainViewModel.Optics` (public setter, N-8), `ReflectorGapUm`, `GainSigmaPercent`,
  `GainSeed`, `BackgroundToSignalRatio`, `LiveTimeS`, `Speed`, every `SourceItemViewModel` property.
F-6's "lock enforcement" test therefore needs ViewModel guards, not only bindings (see the design below).

## F-1 … F-6

### F-1 States — confirmed with changes
The cycle Empty → Acquiring → Stopped → Acquiring … → Completed, Reset → Empty is sound. Changes:
1. **Failed with no data** (failure before the first snapshot, e.g. `ListModeSource` constructor or `Start` throwing):
   there is nothing to lock or reset. Recommend: inputs editable and Start enabled exactly as in Empty, State stays
   `Failed` so the status shows the message (command availability follows *has data*, not the state name).
2. **Failed with data**: keep the last snapshot, lock, offer only Reset (see the open questions).
3. Rename `Idle` → `Empty`: the status line's `ItemStatus` is the enum name; one desktop line asserts "Idle"
   (`tests/Gcam.Studio.UiTests/Harness/Scenario.cs` 31) and the harness comment (`StudioWindow.cs` 78). Cheap; recommend
   the rename because the state now means "no data", which Stopped / Completed also are not.
4. The command table belongs in the ViewModel as computed properties (`HasData`, `CanEditInputs`, `CanStart`,
   `CanStop`, `CanReset`, `StartLabel`) with one test per state × command.

### F-2 Session survives Stop — confirmed, measured (design below)
Both *verify* items hold: the Poisson process continues exactly (measured: bit-identical stream) provided the
look-ahead event is kept; the services' caches need no change provided the ViewModel keeps the acquisition id
(does not call the workspaces' `Begin` on Continue).

### F-3 Preset and speed — confirmed with changes
- Raising the preset after Completed and continuing is exact (measured: completed at 10 s, raised to 30 s → identical
  to an uninterrupted 30 s run).
- **SR-RUN-11** says progress "never decreases within a session"; raising 60 → 120 s at Completed drops progress from
  100 % to 50 %. The requirement must say "acquired live time / current preset".
- Speed changes no event (measured: ×2000, ×333, ×7.3 and an MC-limited run give the same stream) — it is pacing only.
  It may change in any state except Acquiring (live change mid-segment would need a channel into the worker; not
  worth it now).

### F-4 Lock and removal — confirmed, one blocker (N-1)
Removal list below. With A-2 every physical input, including the source drag, is disabled whenever data exist. The
drag adorner draws and hit-tests markers only on an image (`MeasurementAdorner.cs` 148, 305: `if (!_view.HasImage)
return`), and the reconstruction has an image only when data exist — so **the drag can never be used**. Options for
the author: (a) withdraw source drag (SR-MEAS-08, VAL-04, the desktop scenario) — sources are moved in the X / Y fields
after Reset; (b) a drag in Stopped / Completed performs an implicit Reset (data discarded by a pointer gesture —
contradicts F-5's "deliberate" Reset); (c) draw the reconstruction frame (grid known from optics + focus) with markers
in Empty, so the drag plans the scene before Start — a new empty-state surface in `HeatmapView` (M). Recommendation:
**(a) now**, (c) as a later polish item if the author wants it.

### F-5 Top bar — corrected
- No confirmation dialog: Studio has no dialog infrastructure (no `MessageBox` / window anywhere in `src/Gcam.Studio*`);
  DESIGN.ViewLayer requires dialogs as a service behind a Core contract (new contract, WPF implementation, fake, and a
  modal the desktop tests must handle). The cost buys little: every acquisition uses seed 12345 (N-2), so Reset + Start
  with the same inputs and preset reproduces the discarded data exactly (measured indirectly: the real session at
  two speeds equals the direct source stream). DESIGN.Controls has no confirmation rule; it has "one primary button
  per screen" (row 27) — so Reset is a `Button.Ghost` (or the implicit button) left of Start / Stop, disabled while
  Acquiring and in Empty, no keyboard default, no shortcut.
- Start keeps `AutomationId="StartAcquisition"`; its visible label and accessible name switch "Start" / "Continue"
  (rule 4 of AGENTS.Studio: the name contains the visible label). N-6: `IsDefault="True"` makes Enter press it; with
  Continue that resumes counting from a text field. Recommend dropping `IsDefault` (or keeping it only in Empty).
- Status bar already shows the state (dot colour + `ItemStatus`); add the state word in Empty ("Ready") — unchanged.

### F-6 Tests — confirmed, extended
Add: (1) a **service** test on `ManualTimeProvider`: stop mid-run, advance the clock while stopped, continue at
another speed, compare the events with an uninterrupted session of the same seed (identical, live time not advanced
while stopped, counts and live time monotonic, arrivals strictly increasing); (2) Continue after Completed with a
raised preset; (3) ViewModel state table (every command × Empty / Acquiring / Stopped / Completed / Failed-with-data /
Failed-without-data); (4) lock enforcement on the ViewModel setters (N-8); (5) Reset clears Snapshot, Result, all
workspace views, the focus sweep and `Imaging._scene` (N-5), keeps measurements' shapes (as today's Start does);
(6) Continue does not call the workspaces' `Begin` and the services see one acquisition id.
Constraint: **10 fake `IAcquisitionService` / `IAcquisitionSession` implementations** exist (Studio.Tests:
Acquisition, DetectorWorkspace, FocusSweep, ImagingWorkspace, MainViewModel, Optics, Waveform; RenderTests:
MainWindow, Waveform ×2 classes). The contract change below is shaped so they compile unchanged.

## Session continuation design (F-2)

**Contract** (`Gcam.Studio.Core/Services/IAcquisitionSession.cs`):
```csharp
public interface IAcquisitionSession : IAsyncDisposable
{
    IAsyncEnumerable<AcquisitionSnapshot> ReadSnapshotsAsync(CancellationToken cancellationToken = default); // current segment
    void Stop();                                                       // ends the current segment, keeps everything
    void Continue(double presetLiveTimeS, double speed) => throw new NotSupportedException(); // arms the next segment
}
```
`IAcquisitionService.Start(…)` stays as is and runs segment 1. The default `Continue` keeps the ten fakes compiling;
only the fakes of continuation tests implement it.

**`AcquisitionSession`** — persistent state moves to fields; one worker per segment:
- Fields: config, detector, clock, `ListModeSource` (created by the first segment's worker, disposed in
  `DisposeAsync`), decoder, flood, `List<DetectedEvent>`, **`DetectedEvent? _pending`**, `double _live`, `bool _failed`.
- Per segment: new `CancellationTokenSource`, new bounded channel, new worker `Task`. `Stop()` cancels the current CTS.
  `ReadSnapshotsAsync` drains the current channel and then **awaits the worker**, so the next `Continue` starts after
  the previous worker has fully exited (one writer at a time; the await is the happens-before edge).
- `Continue(preset, speed)`: rejects a running segment, a failed session and `preset <= _live` (nothing to acquire);
  otherwise starts the next worker.
- Live-time clock: per segment, take `start = previous = previousPublished = clock.GetTimestamp()`; the first tick
  publishes at `target = _live` (today `0`: same value for segment 1). Live time cannot advance while stopped because
  no worker runs and `dt` is measured from the segment's own `previous`.
- Pacing: unchanged per tick (`target = min(preset, live + speed·dt)`, 200 ms transport budget, MC-limited rule).
  `ActualSpeed` of a segment's first snapshot is 0 (no interval yet) — acceptable, or carry nothing over.
- RNG streams: all live in `ListModeSource` (`_transport`, `_rejection`, `_time`, detector `+777`, background `+909`,
  dark `+1717`) and continue untouched. The Poisson clock `_signalTime` continues; the next arrival is the one drawn
  after the last event, not a new draw at resume.
- Snapshots: counts = `events.Count`, monotonic across segments; `LiveTimeS` monotonic (stop-time `live` is
  `max(previous live, last consumed arrival)` — never decreases); `IsCompleted = live >= current preset`. No snapshot
  id is needed: the acquisition identity is the ViewModel's Guid, kept across Continue.
- Failure: a worker exception completes the channel with the error and sets `_failed`; the source may be mid-history
  (e.g. the weight-bound check throws after `DetectedWeight` was incremented), so the session is not continuable.
- `ImagingResult.Elapsed` (wall time) becomes per segment; nothing in the views reads it (checked).

**Services' caches — work unchanged** (checked in the code):
- `ImagingService.Process` resets only on `id != _id`, `snapshot.Detector != _detector` or fewer events than measured
  (58); otherwise it measures new events by index (`MeasurementStage.Measure(ev, i)`) and appends window floods from
  `_consumed`. Calibration reruns only on a window change. Same id + growing prefix → append.
- `SpectrumService.Process` resets on id / pile-up / seed / fewer events / range / detector / pixels (248–251);
  otherwise appends from `_consumed`. The open pile-up group keeps `_lastArrival`; because live time is continuous
  across the pause (no wall gap inserted into arrival times), the grouping equals the uninterrupted run's.
- `WaveformService` is stateless per request; the ViewModel key (settings, covered time, detector) grows with live
  time; a trigger near the stop shows "Partial acquisition window" until Continue fills it — correct.
- Focus sweep: the identity carries counts and live time, so an earlier result stays labelled with its prefix (as during
  Acquiring today); Continue need not cancel it; Reset must (`InvalidateSweep`).
- Index-addressed measurement randomness (`MeasurementStage`, seed by event index) is preserved because indices are.

**ViewModel** (`MainViewModel`): keep `_session` across Stop (no `await using` in `StartAsync`); split `StartAsync`
into *first start* (validate, `Begin` the workspaces, `_acquisition.Start`) and *continue* (`_session.Continue(preset,
Speed)`, no validation of locked inputs, no `Begin`, no clearing); both then read the segment. `Reset`: dispose the
session, `Snapshot = Result = null`, workspace `Reset()` (Spectrum / Waveform reuse `Begin`'s clearing; Imaging also
clears `_scene` so `ProjectionOptics` follows pending optics again — N-5), State Empty, Progress 0, Status "Ready".
Guard inside `StartAsync` / `Reset` too (N-4: `ExecuteAsync` skips `CanExecute`).

## Measurement — stop + continue vs uninterrupted

Program: throwaway console app `%TEMP%\gcam-acqctl` referencing `src/Gcam.Studio.Services` (Release). It builds the
config with `SimulationService.BuildConfig` (Studio defaults, seed 12345) and compares event lists by exact
`DetectedEvent` equality (pixel, deposit and arrival time, bitwise). "Copy loop" is a field-for-field copy of
`AcquisitionSession.ProduceAsync`'s consumption loop with its state moved to fields (the design above), driven by a
virtual 250 ms wall tick; stops are emulated between ticks and at the k-th `Advance` entry inside a tick (where
`ListModeSource.Advance` checks the token, before any random draw); some segments get a 25-history transport budget
to force MC-limited ticks.

```
cd %TEMP%\gcam-acqctl
dotnet run -c Release            # continuation comparison
dotnet run -c Release -- focus   # does the decoder focus change transport?
```

| Case (T = 30 s live) | Cs-137 (15, 8) mm, 1 m, 500 µCi, BSR 0 | Cs-137 + Co-60 (300 µCi), BSR 0.5 |
|---|---|---|
| Truth: `ListModeSource.Advance` until arrival > T | 1 910 events (63.7 cps) | 6 423 events (214.1 cps) |
| Real `AcquisitionSession` (system clock), speed ×2000 and ×333 | identical, identical | identical, identical |
| Copy loop, uninterrupted ×7.3 | identical | identical |
| Copy loop, MC-limited every tick (40 histories / tick; 312 / 811 limited ticks) | identical | identical |
| Copy loop, **26 segments**, 25 stops (live 0.5 … 9.6 s, speeds ×0.5 … ×10, look-ahead **kept**) | **identical**, arrivals strictly increasing | **identical**, strictly increasing |
| Same stops, look-ahead **dropped** at each stop (naive restart of the loop on a kept source) | 1 907 events: −3 = the 3 stops that held a look-ahead | 6 419: −4 = the 4 stops that held one |
| Completed at 10 s, preset raised to 30 s, continued | identical | identical |
| New session with the **same seed**, times offset by 10 s | 1 899 events; the continued part replays all 631 histories of the first 10 s | 6 375; replays all 2 127 |
| New session with seed + 1 after 10 s | prefix identical, rest an independent draw (2 066 events) | (6 636 events) |
| Focus 1000 vs 500 vs 2500 mm, first 2 000 events | identical | — |

**Result.** With the look-ahead event kept, a stopped-and-continued acquisition produces **exactly** the event
sequence of an uninterrupted run to the same live time, for any stop position, speed, MC-limited pacing, background
and a raised preset. Why: the event stream is a pure function of the `ListModeSource` call sequence (per-history RNG
draws, rejection, Poisson gap from the running efficiency); pacing only decides *when* the worker consumes, and the
consumption rule (take `pending` only if its arrival ≤ target, never draw past a known later arrival) loses no draw.

**What breaks it, and whether it matters physically:**
- *Dropping the look-ahead* (the existing loop restarted on a kept source — `pending` is a local today) loses one
  real count per stop that held one. The following arrivals are absolute, so this is a hole in the Poisson process:
  an under-count of one event per such stop, not a different but equivalent draw. Small (3 of 1 910 for 25 stops) but
  unphysical and avoidable — keep `_pending` as a field.
- *Restart with the same seed* replays the earlier histories: wrong (correlated duplicates).
- *Restart with a new seed* (or redrawing the gap at resume) is physically valid in distribution — the Poisson process
  is memoryless, so a fresh process started at the stop time is still Poisson — but it is not reproducible, re-estimates
  the detected rate from a cold efficiency estimate (R-4 noise again) and needs offset arrival times. No reason to
  prefer it.
- Wall-clock pacing never enters the events; live time, not wall time, is the physical clock, and it does not advance
  while stopped.

## Recommended answers to the open questions

| Question | Recommendation | Reason |
|---|---|---|
| Raise the preset after Completed (F-3) | **Yes**: in Stopped and Completed the preset is editable; Start ("Continue") is enabled only when preset > acquired live time; a value below the acquired live time is rejected (field reverts, message under the top bar); equal = nothing to acquire. Lowering in Stopped to a value still above the live time is allowed. | Measured exact; MCA practice (extend a preset count); SR-RUN-11 changes to "live / current preset". |
| Reset confirmation (F-5) | **No dialog.** Reset is a non-primary button apart from Start, disabled while Acquiring (Stop first) and in Empty, no default / shortcut; the status line announces "Ready" after it. | DESIGN.Controls has no confirmation pattern and one primary per screen; a dialog needs a new Core contract + WPF service + desktop-test handling (DESIGN.ViewLayer); the data are reproducible from the fixed seed (N-2). |
| What Failed keeps | **With data:** the last published snapshot stays visible and measurable, status "Failed: …", inputs locked, only Reset offered (no Continue). **Without data:** behave as Empty (inputs editable, Start enabled), status keeps the message until the next Start. | Published snapshots are immutable and valid; the source may be mid-history; with a fixed seed a Continue would deterministically hit the same failure. Today Failed *unlocks* — that must change to satisfy A-2. |
| Speed while Stopped | **Yes**, in every state but Acquiring; it applies to the next segment. | Measured: speed changes no event; it is pacing only. |
| Detector workspace when locked | Face, colour bar and readout from the acquired settings (they equal the pending ones while locked — the existing `Snapshot ?? pending` code already does this). Replace `Identity` by a neutral caption (not warning colour): Empty "Settings for the next acquisition", with data "Acquired settings · locked until Reset". Remove the `Detector.Pending` line (the readout already lists gap, gain σ and seed). | Pending = acquired whenever data exist, so a second line repeats the first; warning colour without a warning is L-15. |
| Source drag (N-1, new) | (a) withdraw now; (c) empty-state reconstruction frame later if wanted | see F-4 |

## Removal list (stale / outdated / pending-vs-acquired)

**Source**
- `src/Gcam.Studio.Core/ViewModels/MainViewModel.cs`: `IsResultStale` (230), `OnIsResultStaleChanged` (231),
  `MarkStale()` (339–342) and its calls at 45, 113, 150, 159, 177, 179, 183, 205, 211, 329, 334; `IsResultStale = false`
  in `StartAsync` (289); `PendingChain` (139), `AcquiredChain` (140), `ChainDiffers` (141–143) and their notifications
  (147–148, 190). `OnSourceEdited` (332–335) then has no purpose.
- `src/Gcam.Studio.Core/ViewModels/DetectorWorkspaceViewModel.cs`: `Identity` stale branch (18–19), `Pending` (20) and
  their names in `Refresh` (48).
- `src/Gcam.Studio/Views/MainWindow.xaml` 345–351: `ResultStale` chip ("outdated", tooltip "Simulate again…").
- `src/Gcam.Studio/Views/SpectrumView.xaml` 10–12 and `WaveformView.xaml` 11–13: "Outdated" chips.
- `src/Gcam.Studio/Views/ChainPanel.xaml` 28–34: `Chain.Pending` / `Chain.Acquired`.
- `src/Gcam.Studio/Views/DetectorPanel.xaml` 46–47: `Detector.Pending`; `DetectorView.xaml` 9–10: `Detector.Identity`
  restyled to `Text.Caption`.
- `src/Gcam.Studio/Themes/Typography.xaml` 57 comment ("outdated" chip example); `src/Gcam.Studio.Core/README.md` 9
  ("stale result").
- Lock bindings to change from `IsIdle` to the new `CanEditInputs`: `MainWindow.xaml` 103, 107 (live time / speed get
  their own rules), 181 (also removes the ancestor lookup if bound through the root DataContext), 266, 360
  (`CanMoveMarkers`), `ChainPanel.xaml` 9; `OpticsEditor.IsEditable` (`MainViewModel` 115).

**Tests**
- `tests/Gcam.Studio.Tests/AcquisitionViewModelTests.cs`: `Start_CapturesDetectorInputs_AndEditingMarksOnlyTheResultStale`,
  `Start_SnapshotsGrow_StopKeepsData_UnlocksAndEditMarksStale`, `Preset_Completes_NewStartClears_ResultAndEvents`
  (contradicts A-1), `Failure_ReportsMessage_KeepsAcquiredData` (asserts `IsIdle` → unlock), `Spectrum_…` (126 stale
  assert), `LiveTimeAndSpeed_MarkStale_WorkspaceAndMeasurementsDoNot`.
- `MainViewModelTests.cs`: 23, `DetectorInputs_DefaultsAndStaleStateFollowEdits`,
  `Workspace_SharedResultAndSelectionSurviveSnapshot_ViewSettingsDoNotMarkStale` (63).
- `DetectorWorkspaceTests.Face_FollowsPendingBeforeStart_AndAcquiredAfterStaleEdit`.
- `WaveformWorkspaceTests.Chain_EditMarksStaleButKeepsAcquiredIdentity`, `ScopeFailure_IsVisibleAndLocalControlsNeverMarkStale` (149).
- `OpticsViewModelTests.Focus_AcquiringStoppedCompleted_KeepsEventsSpectrumAndStaleState` (81–88, 133+ edits after data).
- `ImagingWorkspaceTests` 109; `FocusSweepViewModelTests` 93 and `Sweep_RejectsLateResult_WhenIdentityChanges`
  cases `"optics"` (edit now locked → use Reset) and `"acquisition"` (Start after Completed is disabled → Reset + Start).
- Keep (service-level facts, still true): `WaveformServiceTests.AcquiredChain_IsUsedAfterPendingSelectionChanges`,
  `MixedFieldCalibration_UsesAcquiredChainAndInvalidatesRatios`.
- `tests/Gcam.Studio.RenderTests/DetectorFocusRenderTests.cs` 33–51, 79–80 (`detector-stale` fixture → a
  "detector-acquired" fixture); `WaveformRenderTests.cs` 70–77 (`Chain.Pending` / `Chain.Acquired`); PNGs
  `docs/assets/studio-render/detector-stale-*`.
- Desktop (list only, run in TODO-22): `ScenarioTests.Stop_KeepsAcquiredData_LocksThenUnlocksScene` (asserts unlock after
  Stop and "Start clears" — both reversed), `SourceDrag_MarksOutdated_RerunPutsPeakOnTheSource` (N-1),
  `Harness/Scenario.cs` 31 ("Idle"), `Harness/StudioWindow.cs` 67–79 (`Acquire` after Completed needs Reset first).

**Docs** (implementation step 4)
- `VV.Studio.SRS.md`: SR-NAV-03, SR-SPEC-06, SR-RUN-09…14, -19, -21, SR-IMG-06, SR-MEAS-08, SR-OPT-01, -04, SR-CHAIN-02,
  SR-WAVE-03, SR-DET-01, -02, -04; status table (246–251: add Empty / Reset, "Continue"); hazard row "Images no longer
  match the scene" (251).
- `VV.Studio.SDS.md` 56, 132, 235, 308, 425, 466; `VV.Studio.md` rows 133, 153, 157, 163, 164, 175, 184, 198, 200, 217,
  VAL-04 (359), 407, AN-06 (427 — resolved by removal), 444, 481, 499, 509, 511; `VV.Studio.Acquisition.md` 73, 78, 107,
  167; `VV.Studio.Detector.md` 45, 50; `VV.Studio.Waveform.md` 7, 22.
- `DESIGN.Layout.md` 40 (top bar: Reset), 63–69, 77, 129, 131, 143, 155; `DESIGN.Typography.md` 37; `DESIGN.Controls.md` 27
  ("Simulate" → Start / Continue); `DESIGN.Architecture.md` 168; `AGENTS.Rationale.md` 94; `AGENTS.UiAutomation.md` 138;
  `VV.Gcam.PRS.md` PR-SW-02 ("flags stale results" — VV.Gcam is self-contained, reword without links).
- History rows left as they are: `AGENTS.Studio.md` roadmap step 3, `PLAN.Studio.LiveAcquisition.md` A-3.

## Proposed state table

*Data* = a snapshot exists. Physical inputs = sources (list, fields, drag), optics, detector, BSR, chain.

| State | Entered by | Data | Physical inputs | Preset | Speed | Start / Continue | Stop | Reset | Status |
|---|---|---|---|---|---|---|---|---|---|
| Empty | launch, Reset | no | editable | editable | editable | "Start", enabled with ≥ 1 source | hidden | disabled | "Ready" |
| Acquiring | Start, Continue | growing | locked | locked | locked | hidden | enabled | disabled | "t = … of … s · counts · cps [· MC-limited ×k]" |
| Stopped | Stop | kept | locked | editable, ≥ live | editable | "Continue", enabled if preset > live | hidden | enabled | "Stopped · t = …" |
| Completed | live ≥ preset | kept | locked | editable, raise | editable | "Continue", enabled if preset > live | hidden | enabled | "Completed · t = …" |
| Failed (data) | exception after ≥ 1 snapshot | last snapshot | locked | locked | editable | disabled | hidden | enabled | "Failed: …" |
| Failed (no data) | exception before a snapshot | no | editable | editable | editable | "Start", enabled | hidden | disabled | "Failed: …" |

View settings (focus, window N, channel, strip, K, sweep, scope, log Y, pile-up, theme, workspace) are editable in
every state (A-2); measurements are kept across Continue and Reset (as across Start today).

## Size and order

Size **M–L**: one engine-free service change (session fields + segments, ~120 lines), a ViewModel state machine with
guards, view edits in five XAML files, ~15 tests rewritten / added across 8 test files, two render fixtures, docs in
~12 files. No engine (`src/Gcam.*` outside Studio) change.

1. **Services**: `AcquisitionSession` segments + `Continue`; contract default method; service tests (continuation
   identity on `ManualTimeProvider`, raised preset, failed session not continuable). Existing service tests unchanged.
2. **Core**: state machine, `HasData` / `CanEditInputs` / command table, ViewModel guards on every physical setter
   (incl. `Optics`, source items), `Reset`, workspace `Reset()` (Imaging clears `_scene`), stale / pending removal;
   ViewModel tests (state table, continue keeps id, locks, Reset).
3. **Views**: Reset button, Start label, remove chips and pending lines, lock bindings, drop `IsDefault`, Detector caption.
4. **Renders**: replace `detector-stale`, chain assertions; regenerate and inspect.
5. **Docs**: SRS / SDS / VV / DESIGN / Rationale rows as listed.
6. **Desktop** changes listed for TODO-22 (not run).

## Could not check

- No desktop run (by rule): the Reset button's placement at 1280 × 800 and keyboard order are judged later in the
  renders and TODO-22.
- The copy loop is a copy, not the production class; its fidelity was checked only for uninterrupted runs (it equals the
  real `AcquisitionSession` there). The production continuation must be re-measured by the F-6 service test.
- Thread-safety of the segment hand-over is argued (await the worker before Continue), not stress-tested.
- Whether the author wants measurements' shapes cleared on Reset (recommended: kept, as Start keeps them today).
- Timings of a long continued acquisition (the per-snapshot `events.ToArray()` copy grows with raised presets) were not
  measured; it is today's behaviour, not new.

No APPROVAL REQUESTS: the throwaway program lives in `%TEMP%\gcam-acqctl` and nothing in the repository was deleted.
