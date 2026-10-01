# DESIGN.Layout — spacing, sizes and the screen grid

Scope: GCAM Studio layout metrics (`src/Gcam.Studio/Themes/Metrics.xaml`) and how `MainWindow` is laid out.

## Spacing: a 4 px grid

| Kind | Keys (value) |
|---|---|
| Scale | 4 · 8 · 12 · 16 · 24 · 32 — expressed as the `Pad.*` / `Gap.*` Thickness keys below (XAML can't build a `Thickness` from `Double` keys, so there are no separate `Space.*` keys) |
| Padding (`Pad.*`, inside a container) | `Pad.Window` 12 · `Pad.Panel` 12 · `Pad.Bar` 16,0 · `Pad.Control` 8,0 · `Pad.Button` 12,0 · `Pad.ListItem` 8,0 |
| Plot label plate | `Pad.Plot.BandLabel` 4 on every side — prevents edges, grid and traces from touching band text; padded widths also determine collision rows |
| Margin (`Gap.*`, after an element) | `Gap.Tight` 0,0,0,4 · `Gap.Field` 0,0,0,8 · `Gap.Section` 0,0,0,16 · `Gap.Title` 0,0,0,8 · `Gap.Inline` 0,0,8,0 · `Gap.InlineWide` 0,0,16,0 |
| Grid gutter | `Gutter` 12 (a `GridLength` for gutter columns) |

**Padding vs margin rule.** A container owns its inner space (`Pad.*`). Space *between siblings* is the
preceding sibling's bottom or right margin (`Gap.*`) — never split one gap between two elements, and never
combine a container's padding with a child's margin for the same edge.

## Sizes and shape

| Key | Value | Used for |
|---|---|---|
| `Size.Control` | 28 | min height of buttons, inputs, list rows |
| `Size.Bar` / `Size.StatusBar` | 48 / 28 | top bar / status bar height (`GridLength`, used directly as row heights) |
| `Size.SidePanel` | 300 | scene and measurement panel width (`GridLength`) |
| `Size.SourceList` / `Size.ResultsList` | 168 / 280 | **max** heights: lists grow with their rows, then scroll — no reserved empty space |
| `Size.NumberInput` · `Size.Progress` | 120 · 160 | general numeric input width · progress bar |
| `Size.NumberInput.Short` | 80 | short live-time / speed values with s / × suffixes; preserves top-bar space |
| `Size.ColorBar` · `Size.ReadoutLine` | 8 · 16 | colour-bar strip · the reserved readout line under an image |
| `Size.Logo` · `Size.Icon` · `Size.StatusDot` | 16 · 12 · 8 | top-bar mark · button icon · run-state dot |
| `Size.Column.Id` / `Size.Column.Short` | 36 / 60 | fixed columns of the results table (header and rows share them; the value column takes the rest) |
| `Size.FieldLabel` | 84 | label column in forms |
| `Radius.Control` / `Radius.Panel` / `Radius.Chip` | 3 / 6 / 10 | corners |
| `Border.Hairline` | 1 | all borders |

## Screen grid (`MainWindow`)

```
Row 0  Top bar (Size.Bar)          identity · workspace switch (when ≥2) · theme · live time (s) · speed × · ▶ Start / Stop
Row 1  Content (a Border with Pad.Window)
         Col 0  Scene panel (Size.SidePanel, scrollable)   sources · selected source · collapsible Physical optics · collapsible Detector
         Col 1  Gutter
         Col 2  Selected workspace centre (DataTemplate by workspace type)
                Imaging: [flood map] Gutter [reconstruction]
                each = PanelHeader (title + chips) · ImageStackPanel: square HeatmapView (+ adorner), ColorBar and
                readout directly beneath, at the image's width; spare height collects below
                Spectrum: stepped histogram PlotView with line bands · readout · acquired-pulse summary · emission-window table (select a row to zoom to its window)
         Col 3  Gutter
         Col 4  Selected workspace panel (DataTemplate by workspace type, Size.SidePanel)
                Imaging: scrollable decoder focal plane / geometry evidence · channel options · tool picker · hint · results table · selected row detail
                Spectrum: Display (log Y) · Window (N × FWHM) · Pile-up (resolving time) · Resolution (read-only chain)
Row 2  Status bar (Size.StatusBar) state dot · live time / counts / cps / MC-limited (live region) · live-time progress
```

Top-bar live-time and speed inputs are vertically centred at `Size.Control` (28 DIP), with
`Size.NumberInput.Short` width (80 DIP). Labels are "Live time" / "Speed"; `Tag` supplies "s" / "×"
inside the inputs, matching the scene fields.

The primary action sits at the same place on every screen (top right); the status of the last run is always
visible at the bottom, full width, regardless of which panel has focus.

Imaging and Spectrum are registered: there are no placeholder workspaces. The switch uses `Segment.Track` /
`RadioButton.Segment`, workspace titles and `Workspace.*` AutomationIds. Ctrl+1…4 selects a registered workspace;
an unavailable index leaves selection unchanged. Acquisition preserves selection. Shared scene / optics / live-time / speed inputs
mark an existing result outdated; workspace view settings re-render without invalidating that result.

Geometry contains only read-only mask, cell pitch, mask–detector distance, detector grid, focal plane and
FCFOV facts, and carries the read-only caption. A separate Detector section contains entrance
(0.15 mm steel-equivalent), backing (2 mm) and reflector gap (0.1 mm) facts, then editable Gain σ
(default 3, `%` suffix), Gain seed (default 1) and Background (default 0, `× signal` BSR suffix;
detected background/source ratio at 200 keV). Short labels fit the existing 84-DIP field column.
These inputs are disabled during
acquisition and mark retained results outdated after an edit. The left panel scrolls when needed so all fields
remain reachable at the minimum window height. Workspace activation follows `IsActive` as well as commands,
so a UI Automation SelectionItem selection changes the centre and right panel.

## Window sizing: fixed grid, flexible centre

- Side panels (scene left, measurements right) are a **fixed** width; the image area takes the rest (`*`). At the
  1280 px minimum each image still gets about 300 px.
- The window has a **minimum** size (1280×800) and a default of 1440×900 instead of a fixed size. A fixed
  window breaks on 1366×768 laptops and overflows the screen at 125 / 150 % scaling.
- If the work area is smaller than the default, the window starts maximised (`App.xaml.cs`).
- `UseLayoutRounding` and `SnapsToDevicePixels` are on; heatmap cells additionally snap to whole device pixels
  ([DESIGN.Controls](DESIGN.Controls.md#heatmapview)).

**Headers.** Every panel and section heading sits in a `PanelHeader` row (min height `Size.Control`, `Gap.Title`
below). Headings carry no margin of their own, so a title lines up with the buttons beside it and all headers have
the same rhythm whether or not they have actions.

## Adding layout values

Stay on the 4 px grid, add a named key to `Metrics.xaml` instead of a literal in a view, and pick the kind by
role (`Pad` for inside, `Gap` for after, `Size` for fixed dimensions).

## Imaging channel options

The Imaging right panel places channel options above the existing measurement tools: All / isotope selector,
the shell-owned window N (also shown in Spectrum), Compton strip, calibrated R values, worker cost readout,
and found-peak coordinates. It reuses Gap.Field, Gap.Tight, Gap.Section and the existing text/control roles.
Selection changes both image grids and their bars/readouts together; ROI geometry stays in mm and its values
refresh against the selected grid. All shows the union of isotope-labelled found markers over the acquisition
reconstruction. A neutral diamond and "Found" label distinguish read-only peaks from draggable true-source rings.

The additional render fixtures cover two isotopes with All and Cs-137 selected, both themes and both target
sizes (1280×800 and 1440×900). Snapshot generation and visual inspection are pending local command execution;
the panel's fit at those sizes is not claimed verified yet. Marker metrics are documented in
[DESIGN.Controls](DESIGN.Controls.md).

## Editable optics and focus

The shared Physical optics and Detector sections use themed `Expander.Section` headers. When collapsed,
a one-line effective summary remains visible (full text in its tooltip). Errors are outside collapsed content;
Start expands invalid sections. Physical fields and atomic engineering presets are disabled while acquiring.
The decoder focal plane is a separate Imaging view setting, in mm from the detector, enabled during and after
acquisition. Its readout uses acquired physical geometry after a run, including when pending inputs differ.
Geometry readings carry no pass/fail glyph; sampling has a conditional precision-evidence note. No depth-reach
hint is shown. The Imaging right panel scrolls independently so measurements remain reachable at small sizes.
Focus changes visibly explain why reconstruction measurements were cleared; flood measurements stay.
Offscreen render cases cover expanded/collapsed panels and F=800 at 1280×800 and 1440×900 in both themes.
