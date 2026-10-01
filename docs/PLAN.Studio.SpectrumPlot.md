# PLAN.Studio.SpectrumPlot — a spectrum graph that reads like an MCA display (TODO-15)

Scope: the graph in the Spectrum workspace — how `PlotView` draws a histogram, keeps the view during live
acquisition, scales Y, reports the hovered bin and places band labels — plus headless render snapshots so it can
be polished without desktop UI tests. The spectrum's physics (smearing, bands, pile-up) is
[PLAN.Studio.Spectrum](PLAN.Studio.Spectrum.md); the order of work is [PLAN.Studio.Migration](PLAN.Studio.Migration.md).

Status: handed to Codex 2026-10-01 (after TODO-08 phase A).

## What the graph does today (checked in the code, 2026-10-01)

| # | Finding | Where |
|---|---|---|
| F-1 | **Zoom is lost during acquisition.** Every snapshot (4 Hz) sets `Series` → `Prepare` → `Configure` → `PlotViewport.Configure` → `Reset()`, so any zoom or pan returns to the full range within 250 ms. | `PlotView.Prepare` / `Configure`, `PlotViewport.Configure` |
| F-2 | **Y is scaled to the whole data set**, not the visible range: zoomed onto 662 keV, the axis still runs to the Ba K maximum. | `PlotView.Prepare` (`_yMin` / `_yMax` over all samples) |
| F-3 | **Bins are drawn as a polyline through bin centres**, not as bins. With 256 bins over ~700 px most pixel columns hold no centre and are skipped, so the curve interpolates between centres; on log Y an empty bin becomes a spike to the floor. | `PlotView.DrawSeries`; `SpectrumService` gives centres only |
| F-4 | **The readout is the cursor position**, "x 251.29, y 141.06": continuous y, no units, too many digits — not the counts of the bin under the cursor. (Survey P-07.) | `PlotView.OnMouseMove` |
| F-5 | **Band labels are clipped and can collide**: a label starts at the band's left edge, so a band at the left end ("32.1 + 36.4 keV") is cut by the clip; neighbouring bands' labels overlap. (Survey P-06.) | `PlotView.OnRender` |
| F-6 | Nothing renders the control without a desktop, so its look can only be judged through the opt-in desktop tests. | — |

## Decisions (planner, 2026-10-01; the author may override)

| # | Decision | Why |
|---|---|---|
| G-1 | A **`Histogram` series kind** with explicit bin edges (origin + width, or an edge array). Drawn as **steps** with an area fill to the floor while a bin is ≥ 2 device pixels wide; when bins are narrower than pixels, the existing min/max pyramid envelope is used. An empty bin is a step down to the floor, never a spike. | An MCA spectrum is a histogram: a bin's height is its counts over its whole width. |
| G-2 | **Data updates keep the view.** The X view resets only when the full X range changes (a new acquisition, a pile-up toggle that extends the axis) or on user reset (double-click, `0`, Home). | Live zoom on a photopeak is the main use while acquiring. |
| G-3 | **Y auto-scales to the visible X range** with headroom (linear: +8 %; log: to the next ½ decade). During live updates the top only grows; it is recomputed when the view changes. | Zooming must reveal the zoomed peak; a top that jumps at 4 Hz is unreadable. |
| G-4 | **Hover snaps to a bin**: a thin cursor line and a highlighted bin; `Readout` = "662.4 keV (660.9 – 663.9) · 1,234 counts", from new `XUnit` / `YUnit` / format properties, digits matched to the bin width. | The reading the user wants is the bin's counts, in units. |
| G-5 | **Band labels are laid out, not just drawn**: centred on the band, clamped inside the plot area, and moved to a second (third) row when they would overlap. Layout is computed in Core (pure) and unit-tested. | Fixes P-06 for every scene, including dense ones like Ir-192. |
| G-6 | **"Zoom to window"**: selecting a row of the emission-window table zooms the plot to that band ± one band width. The ViewModel exposes the requested range; `PlotView` takes it as a one-way dependency property. | The table and the graph should work together, as on an MCA. |
| G-7 | **Headless render snapshots**: an opt-in test (not a desktop UI test — no mouse, no window focus) renders `PlotView` with a fixed spectrum to PNG through `RenderTargetBitmap`, dark and light, normal and zoomed, for review. | Polishing needs pictures; the desktop tests stay last. |

## Steps

1. **Core (`Gcam.Studio.Core/Plotting`, pure, unit-tested):** histogram geometry (step points for a visible range and
   pixel width, with the envelope fallback), visible-range Y extent with headroom and the grow-only live rule,
   bin lookup for a cursor X, band-label layout (positions and rows given label widths), viewport persistence rule.
2. **`PlotView`:** the `Histogram` kind (G-1); keep the viewport across data updates (G-2); Y auto-scale (G-3);
   hover cursor, bin highlight and the new readout (G-4); laid-out band labels (G-5); a `ViewRange` DP (G-6). The
   10 M-sample line-series gate still holds (re-run its headless measurement).
3. **Spectrum:** `SpectrumService` / `SpectrumView` return bin edges with the counts; the ViewModel builds a
   `Histogram` series, passes units ("keV", "counts"), and maps a selected table row to `ViewRange` (G-6). The
   readout `TextBlock` shows the new readout.
4. **Render snapshots (G-7):** `docs/assets/studio-plot/spectrum-{dark,light}-{full,zoom662}.png` from a fixed
   Cs-137 + Co-60 spectrum (no MC in the test — a recorded or analytic histogram).
5. **Tests:** Core unit tests for every rule above; a ViewModel test that a snapshot update keeps the view and a row
   selection requests the band range; existing tests unchanged.
6. **Docs:** DESIGN.Controls (`PlotView` histogram, viewport, hover, labels), VV.Studio.SRS `SR-PLOT-*` / `SR-SPEC-*`
   rows for the new behaviour, SDS, VV matrix; the polish survey marks P-06 and P-07 as addressed.

**Not here:** desktop UI tests (last, per the author), Y zoom by the user, multiple Y axes, export.
