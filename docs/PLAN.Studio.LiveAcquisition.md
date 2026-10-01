# PLAN.Studio.LiveAcquisition — live list-mode acquisition feeding every workspace (TODO-13)

Scope: why the batch run is replaced, the author's decisions A-1 … A-3, the steps and the effect on
[PLAN.Studio.Spectrum](PLAN.Studio.Spectrum.md).

Status: implemented (`0ed17e0`), headless tests pass, reviewed 2026-10-01; R-1, R-3, R-6 done; R-2 / R-4 / R-5 go
with TODO-08.

Implementation checkpoint (2026-10-01): fresh event producer, rejection, 4 Hz immutable snapshots, Start / Stop,
preset completion and headless physics / service / virtual-clock ViewModel tests implemented. Desktop validation
is deferred by the author's restriction; the UI-test project is untouched and required migrations are listed in
[VV.Studio.Acquisition](VV.Studio.Acquisition.md). TODO-07 remains unimplemented.

**Why (author, 2026-10-01, on S-1 / S-4).** A gamma camera records **independent photon events, one at a time,
over live time**; a whole image appearing at once is the less physical picture and the less convincing demo.
Studio today shows one MC-weighted image per Simulate (photon budget, no time). This step replaces that with a
live acquisition that every workspace reads: the image sharpens and the spectrum builds as counts arrive, and the
~25–50-count localisation threshold (Findings) becomes something you can watch.

**What `Gcam.Wpf` does, and what changes.** `Gcam.Wpf` already accumulates over live time, but from two
*separate* fixed shapes: a flood PDF and a spectrum PDF, each Poisson-sampled every 250 ms. Its image counts and
spectrum counts are therefore not the same events, and its 20 000-event pool repeats. Here every count is one
detected event carrying **pixel, deposited energy and arrival time**, each produced by its own MC history and used
once — so the flood, the spectrum, later the per-nuclide windowed images (TODO-08) and the waveform (TODO-10) are
all views of the same event list.

**Design (A-1 … A-3 accepted by the author, 2026-10-01):**

| # | Question | Decision | Why |
|---|---|---|---|
| A-1 | Where do events come from? | **A background MC producer streams fresh detected events; each event is used once.** If the MC cannot keep up with *rate × speed*, live time advances more slowly and the status says so ("MC-limited ×k"). | Independent events, no repeats; honest about compute. A resampled pool (as in `Gcam.Wpf`) repeats events once live time × rate exceeds the pool. |
| A-2 | Weighted MC → physical events | **Rejection on the importance weight** (accept a detected history with probability w / w_max; w_max is bounded because the biased source aims at the detector). Arrival times: Poisson at the physical detected rate R = Σ emission rate × efficiency, efficiency from the producer's running ΣW / N_emitted. | Exact physical event distribution without giving up directional biasing (analog 4π would be ~100× slower). |
| A-3 | Run controls | **Start / Stop with a preset live time** (default 60 s, stops automatically) and a speed (live seconds per wall second, default ×10). Stop keeps what was acquired; Start clears and begins a new acquisition; the scene stays locked while acquiring. The photon budget field goes away. | Matches how the instrument is used and how `Gcam.Wpf` behaves; one control model for every workspace. |

Refresh: accumulate and re-render at 4 Hz; the reconstruction is re-decoded from the accumulated flood each
refresh (cross-correlation on the current grid is cheap — measure it and say so).

**Steps**
1. **Engine: per-event output.** The detector's event sink reports the scored **pixel** with the deposit and
   weight (today `Action<double, double>` gives deposit and weight only; `ComptonCrystalDetector` line ~199).
   Keep the existing callers working (overload or a richer record). Add a list-mode producer in
   `src/Gcam.Simulation` (e.g. `ListModeSource`) that streams `DetectedEvent(PixelX, PixelY, DepositKeV,
   ArrivalTimeS)` for a config: biased emission → mask → Compton crystal → weight rejection → Poisson arrival at the
   running physical rate. Cancellable; deterministic for a seed.
2. **Engine tests (physics, k·σ):** over many events the per-pixel histogram matches the weighted MC flood of the
   same config; the deposit histogram matches `EventStreamStudy`'s weight-resampled spectrum; the event rate
   matches emission × `DetectedWeight / PhotonsEmitted`; inter-arrival times are exponential; no event is used
   twice.
3. **Studio service.** Replace `ISimulationService.RunAsync` with an acquisition contract (in `Gcam.Studio.Core`):
   start(scene, optics, live time, speed) → a session that publishes immutable snapshots at 4 Hz (live time,
   counts, flood, reconstruction, estimate, the event list or the parts later workspaces need), supports Stop, and
   reports MC-limited speed. The time base is injectable so ViewModel tests run on a virtual clock.
4. **ViewModel.** `MainViewModel` run state becomes Idle / Acquiring / Stopped / Completed / Failed; the top bar
   gets Live time and Speed instead of Photons; Start / Stop replaces Simulate / Cancel (same place, primary);
   the status line shows "t = 12 s of 60 s · 1 834 counts · 153 cps" and the MC-limited note when it applies.
   Imaging reads the latest snapshot; measurements stay valid across snapshots (same grid). The stale chip
   keeps its meaning: a scene edit after an acquisition marks it outdated.
5. **Requirements.** Rewrite the affected `SR-RUN-*` rows in VV.Studio.SRS (run → acquisition, cancel → stop,
   progress → live time) — withdrawn rows keep their IDs, new rows are added; SDS, VV matrix and the status-line
   table (§ states) follow. The photon-budget row in the input table is replaced by live time and speed.
6. **Tests.** ViewModel on a virtual clock: Start → snapshots arrive, counts never decrease, Stop keeps the last
   snapshot, the preset time ends the acquisition, editing is locked while acquiring, a scene edit afterwards
   marks stale. Service against the real engine: a short acquisition of a Cs-137 scene localises inside the FCFOV
   once counts pass the documented threshold.
7. **UI tests.** The desktop scenarios press `RunSimulation` and read the photon budget, so they must change.
   **Do not edit `tests/Gcam.Studio.UiTests` while another session works there** — list the needed changes in the
   report; they are applied once the author says that project and the desktop are free.

**Effect on TODO-07.** S-1 and S-4 are resolved by this step: the spectrum Y axis is counts acquired in live
time, built live from the same events; pile-up is applied to those events' real arrival times. TODO-07 reads the
acquisition's events instead of running a second MC.

**Done when** the engine physics tests and `dotnet test Gcam.sln` pass, Imaging acquires live with Start / Stop and
a preset live time, the requirement rows are rewritten, and the needed UI-test changes are listed. No commit — the
author commits.

## Review of the implementation (2026-10-01)

Headless verification re-run by the reviewer: build clean; 254 engine (the original 246 unchanged), 76 Studio,
14 service and 10 UI-oracle tests pass; 9 desktop tests skipped. Physics checks and measurements are recorded in
[VV.Studio.Acquisition](VV.Studio.Acquisition.md). MC throughput is 0.33–0.44 M accepted events/s against detected
rates of ~10²–10³ cps at the default scenes, so "MC-limited" should be rare; decode runs on the worker
(~5–29 ms per 250 ms refresh), never on the UI thread.

Open points, to settle before or in the next steps:

| # | Point | Proposal |
|---|---|---|
| R-1 | The batch path (`Photons`, `RunCommand`, `ISimulationService.RunAsync`) is kept "for compatibility" but no view uses it, while its SR-RUN rows are withdrawn. | **Done** (2026-10-01): batch members, `ISimulationService` and the batch-only tests removed; the flood-axis convention test that guarded mm measurements was re-added on the acquisition path. |
| R-2 | `ListModeSource` has no per-pixel gain non-uniformity. (The original note also claimed `EventStreamStudy` adds decay cascades — wrong: it emits singles too; see PLAN.Studio.ImagingOptions "Corrections".) | Gain in TODO-08's measurement stage; cascades in TODO-14. |
| R-3 | The imaging model changed: Studio scored floods with the geometric `CrystalDetector`; events now come from `ComptonCrystalDetector` (Argmax), so in-crystal Compton mispositioning is in the image. More physical, but a behaviour change. | **Done** (2026-10-01): stated in VV.Studio.SRS (SR-RUN-16, present behaviour only), SDS SU-19 (with the history), VV.Studio.Acquisition and README. The size of the effect is not measured yet. |
| R-4 | Arrival gaps use the running efficiency ΣW / N, which is noisy for the first few hundred histories. | Negligible at this throughput; if wanted, a short pilot run fixes the rate before the first event. |
| R-5 | A non-zero background is rejected (`NotSupportedException`). | TODO-08 adds a transported background producer with the BSR input. |
| R-6 | The desktop UI tests still press `RunSimulation` / read `PhotonBudget`. | **Done** (2026-10-01): migrated in a separate worktree and run on the desktop — 19 / 19 pass; broken verdicts fail all 7 scenarios. New anomalies: AN-10 (workspace switch ignores UIA Select), AN-11 (source-drag localisation 1.43 mm vs 1.5 mm tolerance). |
