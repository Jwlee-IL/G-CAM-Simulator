# PLAN.Studio.AcquisitionControl — Start / Stop / Reset acquisition state machine (TODO-24)

Scope: replacing GCAM Studio's acquisition control (Start always starts afresh, Stop ends the session, edits mark
results "Outdated") with the multichannel-analyser model the author chose: **Start / Stop / Reset**. Procedure:
[AGENTS.Planning](AGENTS.Planning.md); live acquisition design: [PLAN.Studio.LiveAcquisition](PLAN.Studio.LiveAcquisition.md).

Status: **reference plan** 2026-10-02 — waiting for the implementer's review.

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

## Steps

1. **Review (no code):** check F-1 … F-6 in the code; measure that continuing reproduces the same event stream as an
   uninterrupted run with the same seed (or explain why not); list every place the stale concept is used. Write
   `docs/PLAN.Studio.AcquisitionControl.Review.md`.
2. Planner writes "Decisions after review"; open questions go to the author.
3. Implement (Core state machine, session pause / resume, view bindings, removal of the stale path), tests, renders.
4. Docs: SRS / SDS / VV rows for acquisition control, DESIGN.Layout top bar, AGENTS.Studio.

**Not here:** desktop UI tests (TODO-22). **Order:** after the layout batch 3 (shared files), before TODO-22.
