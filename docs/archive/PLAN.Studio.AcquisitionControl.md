# PLAN.Studio.AcquisitionControl — Start / Stop / Reset acquisition state machine (TODO-24)

Scope: replacing GCAM Studio's acquisition control (Start always starts afresh, Stop ends the session, edits mark
results "Outdated") with the multichannel-analyser model the author chose: **Start / Stop / Reset**. Procedure:
[AGENTS.Planning](../AGENTS.Planning.md); live acquisition design: [PLAN.Studio.LiveAcquisition](PLAN.Studio.LiveAcquisition.md).

Status: **done** 2026-10-02 (E-1 … E-10) by the substitute implementer (Codex out of credits). Stop / continue
equivalence re-measured on the real session (1,810 and 2,728 events identical to the uninterrupted run with stops at
×0.7–×11; the test fails when the look-ahead event is dropped). Planner re-verified: tests 262 / 164 / 72 (+7) / 10 (+9),
renders reviewed. Rewritten desktop scenarios run in TODO-22. Unused drag code in `MeasurementAdorner` remains.

## Author's decisions (2026-10-02)

| # | Decision |
|---|---|
| A-1 | **Start after Stop continues accumulating** (MCA semantics): Start = acquire / continue, Stop = pause keeping everything, Reset = discard the data. |
| A-2 | **Physical inputs are locked while data exists** (sources, optics, detector, chain, background): editable only after Reset. The "Outdated / stale" state and its indicators disappear. View settings (decoder focus, window N, channel, strip, focus sweep, scope window, display) stay editable at any time, as they only re-project retained data. |

## What exists (checked in the code, 2026-10-02)

| Item | Where | Fact |
|---|---|---|
| Run state | `MainViewModel.RunState` {Idle, Failed, Acquiring, Stopped, Completed}; `IsRunning` | Start clears `Result` / `Snapshot`, starts a new session; the end state is Completed or Stopped from the last snapshot |
| Session | `IAcquisitionSession` {`ReadSnapshotsAsync`, `Stop`}; `AcquisitionSession` | **one-shot**: `Stop()` cancels a `CancellationTokenSource`, the loop drains a terminal snapshot and the session is disposed with `await using` in `StartAsync` — it cannot continue |
| Stale | `IsResultStale`, `MarkStale()` on source / optics / detector / speed edits; used by `MainWindow.xaml`, `SpectrumView.xaml`, `WaveformView.xaml`, `DetectorWorkspaceViewModel` | the "Outdated" chips, the Detector "settings outdated" scene, pending-vs-acquired displays ("Next acquisition" / "Acquired") |
| Edit locks | `IsRunning` gates sources and chain setters; optics editor `IsEditable = !IsRunning` | edits are blocked only while acquiring |

## Proposed design (reference — the implementer may improve any of it)

| # | Proposal | Status |
|---|---|---|
| F-1 | States: **Empty** (no data) → Start → **Acquiring** → Stop → **Stopped** → Start → Acquiring …; Acquiring → preset live time reached → **Completed**; any state with data → Reset → Empty. **Failed** keeps whatever data was acquired and allows only Reset. Commands enabled exactly by state (table in the view model, tested). | proposed |
| F-2 | **The session survives Stop**: `IAcquisitionSession` gains pause / resume (or the view model keeps the session and starts a new read loop); the list-mode source, its RNG streams, the accumulated flood / events and the live-time clock continue — live time does not advance while stopped. Snapshots stay monotonic under one acquisition id, so the workspace services keep appending instead of restarting. | proposed — *verify* the services' caches (keyed on acquisition id and consumed event count) need no change; *verify* the Poisson arrival process continues exactly (memoryless, the next gap drawn after resume) |
| F-3 | Preset live time: Start is disabled in Completed; the preset may be **raised** while Stopped or Completed (then Start continues to the new preset), never lowered below the acquired live time. Speed (pacing, not physics) is editable in any state. | proposed — *verify* the author's intent if unclear |
| F-4 | Physical inputs editable only in Empty (A-2). Remove `IsResultStale` / `MarkStale`, the Outdated chips, the Detector stale scene and the pending-vs-acquired dual displays (in a state with data, pending = acquired). The Detector workspace shows the locked settings. | proposed |
| F-5 | Top bar: Start (label "Start" / "Continue" by state), Stop, Reset (asks no confirmation? *verify*: Reset discards data — a confirmation, or an undo-free but deliberate placement); status bar shows the state name. Keyboard and AutomationIds: `StartAcquisition`, `StopAcquisition`, new `ResetAcquisition`. | proposed — *verify* the confirmation question against DESIGN.Controls |
| F-6 | Tests: the state table (every command × state), continue-after-stop accumulates (counts and live time add up, no event used twice, arrival times strictly increasing across the pause), lock enforcement, Reset clears every workspace; UI-oracle and desktop test updates (desktop run stays in TODO-22). | proposed |

## Decisions after review (2026-10-02)

Review: [PLAN.Studio.AcquisitionControl.Review](PLAN.Studio.AcquisitionControl.Review.md). Measured there: a stopped and
continued acquisition reproduces the uninterrupted event stream exactly (25 stops at ×0.5–×10, preset raised after
Completed) **provided the session keeps its one already-drawn pending event**; dropping it loses one real count per
stop. Speed only paces events; decoder focus does not change them.

| # | Decision | Basis |
|---|---|---|
| E-1 | The session runs in **segments**: state in fields, one worker per Start / Continue; `IAcquisitionSession.Continue(preset, speed)` (default interface method so test fakes compile); the pending event, RNG streams, live-time clock and acquisition id persist; workspaces are not re-`Begin`-ed on Continue. A service test re-measures the equivalence on the real session (event-by-event, arrival times strictly increasing). | review F-2, measurement |
| E-2 | States Empty / Acquiring / Stopped / Completed / Failed with the review's state table; Failed with data keeps it, locks inputs, offers only Reset; Failed with no data behaves as Empty. | review |
| E-3 | Preset may be **raised** in Stopped and Completed; Start/Continue enabled only when preset > acquired live time; lower values rejected; SR-RUN-11 reworded (progress is relative to the current preset). Speed editable in every state but Acquiring. | review F-3 |
| E-4 | **Locks in the view model**, not only in XAML: every physical setter (sources, optics incl. the public `Optics` setter, detector, chain, background, live-time lowering) guarded by state; command guards inside the commands (`ExecuteAsync` skips `CanExecute`). | review (edit locks mostly view-only) |
| E-5 | Remove the stale concept entirely (the review's list: 11 `MarkStale` sites, `IsResultStale`, Outdated chips, Pending lines, Detector identity text) and the held batch-3 rows L-14 / L-15 / L-10 status bar resolve with it (status bar shows the state and counts / live time). | review, A-2 |
| E-6 | Reset: non-primary button, enabled only in Stopped / Completed / Failed-with-data, no shortcut, **no confirmation dialog**; clears every workspace and the scene Imaging froze at Start; keeps the measurement-tool list. Start is no longer `IsDefault` (Enter in a field must not resume counting). | review F-5, DESIGN.Controls |
| E-7 | **Source drag is withdrawn** (author, 2026-10-02): under A-2 it could never run; source positions are edited in the left panel. | author, review N-1 |
| E-8 | **A new seed per acquisition** (author, 2026-10-02): Reset + Start is an independent measurement; the seed is shown (status / provenance) and can be fixed in an input for reproduction; tests and render fixtures fix it explicitly. Continue keeps the acquisition's seed. | author, review N-2; physics: independent events |
| E-9 | Detector workspace shows the acquired (locked) settings with a neutral caption; no Pending line. | review |
| E-10 | Desktop scenarios that contradict A-1 / A-2 / E-7 are rewritten (not run — TODO-22). | review |

## Steps

1. **Review (no code):** check F-1 … F-6 in the code; measure that continuing reproduces the same event stream as an
   uninterrupted run with the same seed (or explain why not); list every place the stale concept is used. Write
   `docs/archive/PLAN.Studio.AcquisitionControl.Review.md`.
2. Planner writes "Decisions after review"; open questions go to the author.
3. Implement (Core state machine, session pause / resume, view bindings, removal of the stale path), tests, renders.
4. Docs: SRS / SDS / VV rows for acquisition control, DESIGN.Layout top bar, AGENTS.Studio.

**Not here:** desktop UI tests (TODO-22). **Order:** after the layout batch 3 (shared files), before TODO-22.
