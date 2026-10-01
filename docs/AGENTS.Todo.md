# AGENTS.Todo — open work items handed between sessions

Scope: concrete, ready-to-start tasks with enough context that another session (or person) can pick one up
cold. Longer-term or undecided work stays in [AGENTS.Backlog](AGENTS.Backlog.md); results go to
[AGENTS.Findings](AGENTS.Findings.md). A task that is finished is deleted here and recorded in the Backlog's done
list (and Findings, if it produced a result) in the same commit.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-06 | Studio shell: workspace tabs sharing one run result, a first-party `PlotView`, UI polish; README marks `Gcam.Wpf` as legacy | author decision 2026-10-01 (one viewer for the portfolio) | Studio session | implemented (uncommitted); desktop regression + polish survey wait for a free desktop |
| TODO-07 | Spectrum workspace: per-line windows, chain-derived resolution, log Y, pile-up | `Gcam.Wpf` Spectrum tab | Studio session | plan written, decisions S-1…S-4 recorded — after TODO-13 |
| TODO-08 | Imaging options: Compton strip, background BSR, pixel gain σ | `Gcam.Wpf` Imaging tab | Studio session | open (after 07) |
| TODO-09 | Editable optics + presets (lifts the "optics read-only" exclusion in VV.Studio.SRS §7) | `Gcam.Wpf` Optics / Presets tab | Studio session | open |
| TODO-10 | Waveform workspace: front-end chain presets, ADC + shaped traces | `Gcam.Wpf` Waveform tab | Studio session | open (needs `PlotView` decimation) |
| TODO-11 | Detector workspace (reflector gap, SiPM pitch, crosstalk) and depth (3D) / rangefinder in Imaging | `Gcam.Wpf` Detector tab, Imaging depth | Studio session | open |
| TODO-12 | Delete `src/Gcam.Wpf`; update `Gcam.sln`, CI, AGENTS / README / VV.Studio; README screenshot of Studio | end of the migration | Studio session | open (last) |
| TODO-13 | Live list-mode acquisition: independent detected events (pixel, energy, arrival time) over live time, Start / Stop, feeding every workspace | author decision 2026-10-01 (S-1 / S-4 in TODO-07) | Studio session | A-1…A-3 accepted; handed to Codex 2026-10-01 — before TODO-07 |

Next free ID: TODO-14. Order: 06 → 13 → 07 → 08 …

---

## TODO-06 … TODO-12 — Move `Gcam.Wpf` into GCAM Studio

**Why.** Two WPF viewers confuse a reviewer of the portfolio. `src/Gcam.Wpf` (code-behind, ScottPlot) still has the
Spectrum, Waveform, Optics and Detector tabs and the Compton-strip / depth controls; GCAM Studio has only the imaging
workflow. Each feature moves in its own step; `Gcam.Wpf` is deleted when nothing is left only there (it stays in
git history).

**Decisions (author, 2026-10-01).**
- **Charts are first-party** (`PlotView` in `src/Gcam.Studio/Controls`, axis / tick / viewport maths in
  `Gcam.Studio.Core`), not ScottPlot: one design system (theme tokens, typography, automation peers), no SOUP
  (ScottPlot pulls 27 packages incl. SkiaSharp / OpenTK; `Gcam.Wpf` output 94 MB vs Studio 1.2 MB).
  **Acceptance gate:** a 10-million-sample trace redraws in ≤ 16 ms in the real window, via a per-pixel-column
  min/max pyramid (headless pre-check on the original machine: pyramid redraw 0.24 ms at 10 M, 0.86 ms at 50 M;
  build 16 ms at 10 M). If the gate fails, put ScottPlot behind the same dependency-property interface.
  Cap the scope window at an input limit (~10 M samples) — memory, not drawing, is the real bound.
- **One run feeds every workspace**; scene and optics are inputs shared by all tabs.
- **Calculations leave the code-behind**: `ApplyResolution`, `BlockifyFlood`, strip assembly and the waveform
  stream move to `Gcam.Studio.Services` or the engine, with tests — never copied into views.
- **Each step updates** VV.Studio.SRS (new rows; §7 exclusions it lifts), VV.Studio.SDS, the VV.Studio matrix,
  tests, and is checked through UI automation ([AGENTS.Studio](AGENTS.Studio.md), "Verifying a UI change").

### Screen structure (draft for the author's review)

The grid of [DESIGN.Layout](DESIGN.Layout.md#screen-grid-mainwindow) stays; the top bar gains a workspace switch,
the left panel stays shared, the centre and right panel belong to the selected workspace.

```
┌ Top bar ───────────────────────────────────────────────────────────────────────────────────────────┐
│ ■ GCAM Studio   [ Imaging | Spectrum | Waveform | Detector ]        theme · Photons [500,000] · ▶ Simulate │
├ Scene (shared) ──┬ Workspace centre ───────────────────────────────┬ Workspace panel ──────────────┤
│ Sources  + −     │ Imaging:   [flood map]   [reconstruction]       │ Imaging:  tools · measurements │
│ Selected source  │ Spectrum:  [spectrum PlotView, line bands      ]│ Spectrum: window ±×FWHM ·      │
│ Geometry / Optics│            [per-line table: keV · window ·      ]│   resolution · log Y · pile-up │
│  (editable, 09)  │            [ counts in window                   ]│   · noise floor                │
│                  │ Waveform:  [ADC trace PlotView               ]   │ Waveform: scintillator ·       │
│                  │            [shaped trace PlotView (shared X) ]   │   sensor · preamp · scope rate │
│                  │ Detector:  [light-sharing heatmap]              │ Detector: gap · SiPM · crosstalk│
├ Status bar (shared): state dot · status sentence · progress ───────────────────────────────────────┤
```

Rules behind it:
- **Run inputs vs view settings.** A setting that changes what the Monte Carlo produces lives in the shared left
  panel or the top bar and marks the result *outdated*; a setting applied to an existing result (log Y, window
  width, scope zoom, post-hoc resolution) lives in the workspace panel and re-renders at once, never stale.
- **Workspace switch** uses the existing segment style (`Segment.Track` / `RadioButton.Segment`), `Ctrl+1…4`,
  AutomationIds `Workspace.Imaging`, `Workspace.Spectrum`, …; the selection survives a run. It is shown only when
  two or more workspaces exist — no placeholder tabs (an empty "coming soon" tab reads as unfinished work).
- The Simulate button, photon budget and status bar stay where they are on every workspace.

### TODO-06 — steps

Implementation checkpoint (2026-10-01): workspace shell and first-party plot are implemented; CPU redraw gate
passed (10M, max zoom 12.118 ms / resize 2.642 ms). TODO-06 remains open: the opt-in regression / survey run
failed at `SetCursorPos` / UIA `SetFocus`; screen capture also failed (invalid handle). See
[VV.Studio](VV.Studio.md#plot-performance-gate) and [survey record](assets/studio-polish-survey/README.md).
Re-run on an accessible interactive desktop; do not replace the existing assertions or mark the survey performed.

1. **Workspaces in the ViewModel.** `MainViewModel` keeps the scene, optics, run state and the shared result;
   imaging-only state (`Measurements`, `PeakText`) moves to an `ImagingWorkspaceViewModel`. Add a small
   `WorkspaceViewModel` base (title, AutomationId key) and `MainViewModel.Workspaces` / `SelectedWorkspace`.
   The view picks centre + panel by `DataTemplate` on the workspace type. Existing tests keep passing (adapt
   paths, don't weaken assertions).
2. **`PlotView` maths in `Gcam.Studio.Core` (`Plotting/`), no WPF:**
   - `PlotSeries` record (name, `Y[]`, either `X[]` or origin + step, kind Line / Area, colour role),
     `PlotBand` (lo, hi, label), `PlotMarker` (x, label).
   - `PlotViewport` — data ↔ pixel for linear and log10 Y (empty or ≤ 0 counts clamp to the log floor, as
     `Gcam.Wpf.DrawSpectrum` does), X zoom / pan / reset.
   - `NiceTicks` — 1-2-5 linear ticks; log decades with 2…9 minor ticks; labels consistent with `TickFormatter`.
   - `MinMaxPyramid` — built once per data set (O(N)); `Query(x0, x1, columns)` returns min / max per pixel column
     from aligned blocks starting at 64 samples (at most N/16 extra doubles), scanning raw head / tail fragments. Unit tests: equals a brute-force scan on random data
     and edge cases (N < columns, N = 1, NaN-free, ranges at the ends).
3. **`PlotView` control** (`src/Gcam.Studio/Controls`, `FrameworkElement` + `OnRender`, same pattern as
   `HeatmapView`): dependency properties `Series`, `Bands`, `Markers`, `LogY`, `XLabel`, `YLabel`, `EmptyText`,
   brushes set by style; read-only `Readout` (x, y under the pointer) for a host TextBlock, without series redraw; wheel / `+ −` zoom X, drag / arrows pan,
   double-click / `0` resets; an automation peer like `HeatmapViewAutomationPeer`. Draw one frozen
   `StreamGeometry` per series from the pyramid query (≈ 2 points per pixel column). New colour tokens
   (`Brush.Plot.Series1…n`, `Brush.Plot.Band`, `Brush.Plot.Grid`) in both `Tokens.*.xaml`.
4. **Performance gate** — an opt-in desktop test (`GCAM_UI_TESTS=1`, like the existing ones) hosts `PlotView` in a
   window with a 10-million-sample series and measures redraw after a zoom step and after a resize: **≤ 16 ms**.
   Record the number, machine and date in VV.Studio. If it fails, stop and report — do not tune by dropping data.
5. **README**: one line in the layout table that `Gcam.Wpf` is the legacy viewer being folded into GCAM Studio
   (TODO-06 … 12). Do not otherwise edit `src/Gcam.Wpf`.
6. **Docs in the same change:** DESIGN.Layout (screen grid + workspace switch), DESIGN.Controls (`PlotView`),
   DESIGN.Color (plot tokens), DESIGN.Architecture (workspaces), AGENTS.Studio roadmap (new row: migration),
   VV.Studio.SRS (`SR-NAV-*`, `SR-PLOT-*`), VV.Studio.SDS (new units), VV.Studio matrix, test counts in AGENTS.md
   and CLAUDE/README where quoted.
7. **Polish survey, not polish:** run the app through UI automation, capture the Imaging workspace in both themes
   at 1280×800 and 1440×900, and list concrete polish issues (spacing, alignment, truncation, contrast, focus
   order) with screenshots. The author picks which to fix.

**Done when** `dotnet build Gcam.sln -c Release` and `dotnet test Gcam.sln` pass, the opt-in performance test
meets the gate, the Imaging workspace behaves exactly as before (existing UI scenarios pass), and the docs above
are updated. No commit — the author commits.

### TODO-07 — Spectrum workspace (after TODO-13)

**What `Gcam.Wpf` does today** (`PrepareLive`, `LiveTimer_Tick`, `RenderSpectrum`, `UpdateFrontEnd`): a second MC
(`EventStreamStudy.Generate`, 20 000 events) gives a deposit pool; optional pile-up from the chain's pulse
(`EventStreamStudy.ApplyPileUp`, `ResolvingSamples(rise, tail)`); each deposit smeared by `FrontEndModel.Measure` of
the selected chain (`FrontEndParts`, `src/Gcam.Wpf/FrontEnd.cs` — the "Resolution %@662" box is overwritten by the
chain at start-up); 256 bins up to 1.15 × the highest line (2.15 × with pile-up), normalised and Poisson-sampled
every 250 ms of live time; a hand-shaped exp(−E/30 keV) noise wall; one ROI band per distinct line,
E·(1 ± N·FWHM₆₆₂), N default 1.5, and the share of counts inside the windows.

**Decisions (author, 2026-10-01)**

| # | Decision | Reason |
|---|---|---|
| S-1 | The Y axis is **counts acquired in live time** — the TODO-13 events, not a separate pool | independent events over time are the physical picture and the convincing one |
| S-2 | **No noise floor** | it is a hand-shaped curve; the repository rule is "never hand-add a tail / line" |
| S-3 | **Resolution derived from a front-end chain**, no hand-set % box; TODO-07 shows the default chain read-only, TODO-10 adds chain selection | same rule; it is what `Gcam.Wpf` effectively does |
| S-4 | **Live**, built as the acquisition runs (TODO-13), not one batch result | as S-1 |

**Run inputs vs view settings.** The scene, live time and speed are run inputs (TODO-13). View settings re-render
from the acquired events without a new acquisition and never mark the result stale: log Y; peak window N (in
TODO-07 it moves the ROI bands and the in-window share only — it becomes a run input in TODO-08 when it drives the
imaging windows); pile-up on / off.

**Steps**
1. **Front-end presets into the engine.** Move `ScintPreset`, `SensorPreset`, `PreampPreset`, `FrontEndParts` from
   `src/Gcam.Wpf/FrontEnd.cs` to `src/Gcam.Configuration` (they only build a `FrontEndConfig`); point `Gcam.Wpf`
   at the moved types (delete its copy — a `using` change, nothing else). Add `FrontEndParts.Default` (the chain
   `Gcam.Wpf` selects at start-up) and one tested helper for the pulse rise / tail in ADC samples as
   `UpdateFrontEnd` derives them.
2. **`ISpectrumService`** (contract in `Gcam.Studio.Core`, implementation in `Gcam.Studio.Services`): from the
   acquisition's events (deposit, arrival time) and the view settings, build a `SpectrumView` — bin centres,
   counts, one band per distinct line, in-window share, resolution at 662 keV. Smearing uses `FrontEndModel` of the
   chain; pile-up merges events within the chain's resolving time using their real arrival times. Deterministic
   for a seed. Incremental where cheap (new events since the last snapshot are smeared once and added); a pile-up
   toggle re-processes the kept events.
3. **`SpectrumWorkspaceViewModel`** (title "Spectrum", `Workspace.Spectrum`): view settings as observable
   properties; follows the acquisition snapshots; exposes `PlotSeries` (Area), `PlotBand`s labelled with the line
   energy, and a per-line table (isotope, line keV, window lo–hi keV, counts in window, share). Adding it makes
   the workspace switch appear.
4. **View.** Centre: `PlotView` (X "measured energy (keV)", Y "counts", log Y) with its readout `TextBlock`
   beneath, then the per-line table. Right panel: Display (log Y), Window (N × FWHM), Pile-up (checkbox with the
   derived resolving time shown), Resolution (read-only, from the chain). Stale chip as on Imaging. Accessible
   names and AutomationIds `Spectrum.Plot`, `Spectrum.LogY`, `Spectrum.Window`, `Spectrum.PileUp`,
   `Spectrum.Lines`.
5. **Tests.** Services (real engine): a Cs-137 acquisition puts the histogram maximum in the bin holding
   661.7 keV; the photopeak FWHM matches `FrontEndModel.FwhmFraction(661.7)` within a stated k·σ / bin tolerance;
   pile-up lowers the total and puts counts above 1.2 × 661.7 keV at a high rate; a Cs + Co scene gets three bands
   (662, 1173, 1332); same seed → identical histogram. Configuration: the moved presets build the same
   `FrontEndConfig` and pulse times the old code path produced. ViewModel (fake service, virtual clock): a view
   setting re-renders without a new acquisition and without stale; counts grow with snapshots.
6. **Docs in the same change:** VV.Studio.SRS (`SR-SPEC-*`), VV.Studio.SDS, VV.Studio matrix, DESIGN.Layout
   (Spectrum centre / panel), DESIGN.Architecture (spectrum service), AGENTS.Studio roadmap row; test counts where
   quoted.

**Not in TODO-07:** chain selection (TODO-10), per-nuclide windowed imaging and Compton strip (TODO-08), desktop UI
tests until the author says the desktop is free.

**Done when** build and `dotnet test Gcam.sln` pass, the Spectrum workspace builds the Cs-137 photopeak live with
its band and the Cs + Co case shows three bands (checked in the real window once the desktop is free), and the docs
above are updated. No commit — the author commits.

### TODO-13 — Live list-mode acquisition (before TODO-07)

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
