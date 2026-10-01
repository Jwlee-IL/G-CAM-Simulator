# PLAN.Studio.Polish — UI polish of GCAM Studio, in batches (TODO-16)

Scope: the polish work the author made the focus on 2026-10-01 — what each batch fixes, why, and how it is judged
(headless render snapshots, not desktop UI tests, which run last). Issues come from the desktop survey
(`docs/assets/studio-polish-survey/README.md`, P-xx) and the plot snapshots (`docs/assets/studio-plot/`). Order and
the testing rule: [PLAN.Studio.Migration](PLAN.Studio.Migration.md).

Status: batch 1 done 2026-10-01 (renders in `docs/assets/studio-render/`); batch 2 done the same day (detector section, label plates, no
Window-ancestor bindings; FCFOV now in the renders).

## Batch 1 — design-system level (new workspaces inherit these)

Each cause below was checked in the code before writing this plan.

| # | Issue | Cause in the code | Fix |
|---|---|---|---|
| B1-1 (P-01) | Flood chip says "weighted counts" | literal text in `MainWindow.xaml` (flood panel header) | "counts" — every list-mode count is one event |
| B1-2 (P-02) | Heatmap readout ends in a bare number ("· 7") | `HeatmapView`: `"x {0:F1} mm, y {1:F1} mm · {2:G4}"` | a `ValueUnit` property on `HeatmapView` (flood: "counts" → "· 7 counts"; reconstruction: its decoded-intensity label, e.g. "· 3,077 (decoded)") |
| B1-3 (P-03) | Colour-bar ticks not round (6.2, 12.5; "−1") | `ColorBar` places `TickFormatter.Labels(min, max, n)` evenly from min to max | ticks at 1-2-5 values inside [min, max] (reuse `NiceTicks.Linear`), each placed at its own position on the bar; keep the shared ×10ⁿ rule |
| B1-4 (P-05) | Live time / Speed boxes fill the 48 px bar and are wider than needed; units sit in the labels | top-bar `TextBox`es have no vertical alignment, so the horizontal `StackPanel` stretches them; `Size.NumberInput` = 120 | vertically centred at `Size.Control`; a narrower named width for short numbers; units through the existing `Tag` suffix ("s", "×") as the left panel does; labels "Live time", "Speed" |
| B1-5 (P-08) | "Pile-up" title above a "Pile-up" checkbox | `SpectrumPanel.xaml` | checkbox text says what it does ("Merge pulses within the resolving time"), title stays |
| B1-6 (P-09) | Preset named "CSP + CR-RC (your rig)" | `FrontEndParts.cs` preset name (moved from `Gcam.Wpf`) | "CSP + CR-RC (original rig)"; the `Gcam.Wpf` comment that quotes it follows |
| B1-7 | Log-axis top tick reads "10 ×10³" | `NiceTicks.Logarithmic` labels decades through the colour-bar `TickFormatter` (shared exponent) | decade labels as plain grouped numbers up to 10⁵ ("10,000"), superscript powers above ("10⁶") |
| B1-8 | Y-axis title "counts" crowds the top tick label | `PlotView` draws `YLabel` at the top-left corner inside the plot margin | reserve a title row above the plot area (measured text height + gap) |
| B1-9 | Window bands blend with the data (light theme especially) | `Color.Plot.Band` is Series1 green at low alpha (`#266ACD91`) | a neutral band tint distinct from every series colour, with a band edge line; both themes; contrast checked (band vs background vs series fill) |

## Judging: whole-window render snapshots

Extend `tests/Gcam.Studio.RenderTests` (opt-in `GCAM_RENDER_SNAPSHOTS=1`, no window shown, no mouse) to render the
**main window's content** offscreen at 1280 × 800 and 1440 × 900, dark and light, for the Imaging and Spectrum
workspaces, from a deterministic acquisition snapshot supplied by a fake acquisition service (no MC in the test).
Output: `docs/assets/studio-render/{imaging,spectrum}-{dark,light}-{1280x800,1440x900}.png`, plus the existing plot
snapshots re-rendered. Before / after for this batch = the survey captures vs these renders.

## Batch 2 — seen in the batch-1 renders

| # | Issue | Cause (checked) |
|---|---|---|
| B2-1 | "Background B…" label truncated; the editable Gain σ / Gain seed / Background fields sit under the "Geometry — read-only" header | phase A added inputs to the read-only geometry block; `Size.FieldLabel` (84) is too narrow for "Background BSR" |
| B2-2 | A narrow band's label ("32.1 + 36.4 keV") is crossed by the band's own edge lines | edges are drawn over the label row; the label is wider than the band |
| B2-3 | FCFOV value blank in the renders (fine in the app) | `MainWindow.xaml` binds through `RelativeSource AncestorType=Window`, which the offscreen render (content detached from the window) cannot resolve — such bindings also tie views to the window type |

Fixes:
- **B2-1** Split the left panel's lower part into **Geometry** (read-only facts: mask, pitch, distances, detector,
  focal plane, FCFOV) and **Detector** (read-only facts: entrance, backing, reflector gap; editable run inputs:
  gain σ with a "%" suffix, gain seed, background with a "× signal" (BSR) suffix). Labels short enough for
  `Size.FieldLabel`; the "read-only" caption only on the section that is. AutomationIds of the fields unchanged.
- **B2-2** Band labels are drawn after the band edges on a plate of the plot background colour (padding from the
  metrics), so no edge or grid line crosses the text; a label wider than its band stays centred and clamped as now.
- **B2-3** No `RelativeSource AncestorType=Window` bindings in views (three today: the workspace command, the
  selected-source `IsEnabled`, FCFOV). Bind to the view model through the data context that is already there (an
  element name on the root content, or the workspace / panel's own context), so a view works the same inside the
  window and in the offscreen render; the FCFOV value then shows in the renders.

## Later batches (after the workspaces exist)

- Workspace layout: the left panel scrolls at 1280 × 800 since the Detector section was added (compact or
  collapsible sections), empty height under the images (P-04), emission-window table alignment and a Ba K row label
  (P-10), and the layouts of the Waveform / Detector workspaces when they land.
- Then the desktop UI tests, the desktop survey and the README screenshot (last).
