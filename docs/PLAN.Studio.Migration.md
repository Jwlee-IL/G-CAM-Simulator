# PLAN.Studio.Migration — moving every `Gcam.Wpf` feature into GCAM Studio

Scope: the umbrella for TODO-06 … TODO-13 — why, the decisions that hold for every step, the screen structure and the
order. Each step has its own plan; the task list is [AGENTS.Todo](AGENTS.Todo.md).

Status: **done** 2026-10-02 — every kept `Gcam.Wpf` feature lives in Studio and `Gcam.Wpf` is deleted (TODO-12, last
present at `85b2ed1`). The conventional Anger readout model (TODO-19) continues as a physics task.

## Plans and order

| Order | Task | Plan | State |
|---|---|---|---|
| 1 | TODO-06 workspace shell, `PlotView` | [PLAN.Studio.Shell](PLAN.Studio.Shell.md) | **done** — desktop-verified 2026-10-01 |
| 2 | TODO-13 live list-mode acquisition | [PLAN.Studio.LiveAcquisition](PLAN.Studio.LiveAcquisition.md) | committed `0ed17e0`; desktop-verified |
| 3 | TODO-07 Spectrum workspace | [PLAN.Studio.Spectrum](PLAN.Studio.Spectrum.md) | committed `45eece8`; desktop-verified (survey) |
| 4 | TODO-08 detector realism, background, per-nuclide imaging, Compton strip | [PLAN.Studio.ImagingOptions](PLAN.Studio.ImagingOptions.md) | **done** 2026-10-02 |
| 4a | TODO-15 spectrum graph (histogram steps, live zoom, Y auto-scale, bin readout, band labels) | [PLAN.Studio.SpectrumPlot](PLAN.Studio.SpectrumPlot.md) | implemented; follow-ups in its plan |
| 4b | TODO-16 UI polish, batches 1–4 | [PLAN.Studio.Polish](PLAN.Studio.Polish.md) | **done** 2026-10-02 |
| 5 | TODO-09 editable optics + presets | [PLAN.Studio.Optics](PLAN.Studio.Optics.md) | **done** 2026-10-02 |
| 6 | TODO-10 Waveform workspace, front-end chain selection | [PLAN.Studio.Waveform](PLAN.Studio.Waveform.md) | **done** 2026-10-02 |
| 7 | TODO-11 Detector workspace, depth (3D) / rangefinder | [PLAN.Studio.Detector](PLAN.Studio.Detector.md) | **done** 2026-10-02 |
| 7b | TODO-24 acquisition control Start / Stop / Reset (author 2026-10-02) | [PLAN.Studio.AcquisitionControl](PLAN.Studio.AcquisitionControl.md) | **done** 2026-10-02 |
| 7c | TODO-22 final desktop pass (UI tests, survey, 16 ms gate, README screenshot) | — | **done** 2026-10-02 |
| 7d | TODO-19 conventional Anger readout (SiPM pitch, crosstalk) — moved before the deletion, author 2026-10-02 | [PLAN.Physics.RigReadout](PLAN.Physics.RigReadout.md) | draft; waiting for the author's rig details |
| 8 | TODO-12 delete `Gcam.Wpf` (README screenshot done in TODO-22) | [PLAN.Studio.WpfRemoval](PLAN.Studio.WpfRemoval.md) | **done** 2026-10-02 (before TODO-19, author) |

**Order of polish and testing (author, 2026-10-01).** UI polish is the focus; **desktop UI tests run last**, once
the workspaces are complete and polished — running them (and the polish survey) per step only re-collects the same
remarks on screens that are still changing. Per step: headless tests plus headless render snapshots for review.
Design-system-level polish (units in readouts, round ticks, input sizing, labels — survey P-01, P-02, P-03, P-05,
P-08, P-09) goes early because new workspaces inherit it; workspace layout polish (P-04, P-10) comes once the
workspaces exist; then one desktop run, the survey and the README screenshot, then TODO-12.

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
- **One acquisition feeds every workspace** ([PLAN.Studio.LiveAcquisition](PLAN.Studio.LiveAcquisition.md)); scene
  and optics are inputs shared by all workspaces.
- **Calculations leave the code-behind**: `ApplyResolution`, `BlockifyFlood`, strip assembly and the waveform
  stream move to `Gcam.Studio.Services` or the engine, with tests — never copied into views.
- **Each step updates** VV.Studio.SRS (new rows; §7 exclusions it lifts), VV.Studio.SDS, the VV.Studio matrix,
  tests, and is checked through UI automation ([AGENTS.Studio](AGENTS.Studio.md), "Verifying a UI change").

## Screen structure

The grid of [DESIGN.Layout](DESIGN.Layout.md#screen-grid-mainwindow) stays; the top bar gains a workspace switch,
the left panel stays shared, the centre and right panel belong to the selected workspace.

```
┌ Top bar ───────────────────────────────────────────────────────────────────────────────────────────┐
│ ■ GCAM Studio   [ Imaging | Spectrum | Waveform | Detector ]   theme · Live time · Speed · ▶ Start / ■ Stop │
├ Scene (shared) ──┬ Workspace centre ───────────────────────────────┬ Workspace panel ──────────────┤
│ Sources  + −     │ Imaging:   [flood map]   [reconstruction]       │ Imaging:  tools · measurements │
│ Selected source  │ Spectrum:  [spectrum PlotView, line bands      ]│ Spectrum: window ±×FWHM ·      │
│ Geometry / Optics│            [per-line table: keV · window ·      ]│   resolution · log Y · pile-up │
│  (editable, 09)  │            [ counts in window                   ]│   · noise floor                │
│                  │ Waveform:  [ADC trace PlotView               ]   │ Waveform: scintillator ·       │
│                  │            [shaped trace PlotView (shared X) ]   │   sensor · preamp · scope rate │
│                  │ Detector:  [light-sharing heatmap]              │ Detector: gap · SiPM · crosstalk│
├ Status bar (shared): state dot · live time · counts · cps ──────────────────────────────────────────┤
```

Rules behind it:
- **Run inputs vs view settings.** A setting that changes what the Monte Carlo produces lives in the shared left
  panel or the top bar and marks the result *outdated*; a setting applied to an existing result (log Y, window
  width, scope zoom, post-hoc resolution) lives in the workspace panel and re-renders at once, never stale.
- **Workspace switch** uses the existing segment style (`Segment.Track` / `RadioButton.Segment`), `Ctrl+1…4`,
  AutomationIds `Workspace.Imaging`, `Workspace.Spectrum`, …; the selection survives a run. It is shown only when
  two or more workspaces exist — no placeholder tabs (an empty "coming soon" tab reads as unfinished work).
- Start / Stop, live time, speed and the status bar stay where they are on every workspace.
