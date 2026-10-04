# PLAN.Studio.Shell — workspace shell and a first-party plot control (TODO-06)

Scope: the steps of TODO-06. Decisions shared by every migration step: [PLAN.Studio.Migration](PLAN.Studio.Migration.md).

Status: **done** (`bb3f00e`, 2026-10-01). Desktop regression and polish survey ran the same day on list-mode
acquisition: 19 / 19 UI tests pass, broken verdicts fail all 7 scenarios; survey issues P-01 … P-11 are in
`docs/assets/studio-polish-survey/README.md` for the author to choose from.

## Steps

Implementation checkpoint (2026-10-01): workspace shell and first-party plot are implemented; CPU redraw gate
passed (10M, max zoom 12.118 ms / resize 2.642 ms). TODO-06 remains open: the opt-in regression / survey run
failed at `SetCursorPos` / UIA `SetFocus`; screen capture also failed (invalid handle). See
[VV.Studio](../VV.Studio.md#plot-performance-gate) and [survey record](../assets/studio-polish-survey/README.md).
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
