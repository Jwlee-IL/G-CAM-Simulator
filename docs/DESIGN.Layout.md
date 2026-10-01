# DESIGN.Layout — spacing, sizes and the screen grid

Scope: GCAM Studio layout metrics (`src/Gcam.Studio/Themes/Metrics.xaml`) and how `MainWindow` is laid out.

## Spacing: a 4 px grid

| Kind | Keys (value) |
|---|---|
| Scale | 4 · 8 · 12 · 16 · 24 · 32 — expressed as the `Pad.*` / `Gap.*` Thickness keys below (XAML can't build a `Thickness` from `Double` keys, so there are no separate `Space.*` keys) |
| Padding (`Pad.*`, inside a container) | `Pad.Window` 12 · `Pad.Panel` 12 · `Pad.Bar` 16,0 · `Pad.Control` 8,0 · `Pad.Button` 12,0 · `Pad.ListItem` 8,0 |
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
| `Size.NumberInput` · `Size.Progress` | 120 · 160 | live-time / speed boxes · progress bar |
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
         Col 0  Scene panel (Size.SidePanel, scrollable)   sources · selected source · geometry · detector · background
         Col 1  Gutter
         Col 2  Selected workspace centre (DataTemplate by workspace type)
                Imaging: [flood map] Gutter [reconstruction]
                each = PanelHeader (title + chips) · ImageStackPanel: square HeatmapView (+ adorner), ColorBar and
                readout directly beneath, at the image's width; spare height collects below
                Spectrum: Area PlotView with line bands · readout · acquired-pulse summary · emission-window table
         Col 3  Gutter
         Col 4  Selected workspace panel (DataTemplate by workspace type, Size.SidePanel)
                Imaging: tool picker · hint · results table · selected row detail
                Spectrum: Display (log Y) · Window (N × FWHM) · Pile-up (resolving time) · Resolution (read-only chain)
Row 2  Status bar (Size.StatusBar) state dot · live time / counts / cps / MC-limited (live region) · live-time progress
```

The primary action sits at the same place on every screen (top right); the status of the last run is always
visible at the bottom, full width, regardless of which panel has focus.

Imaging and Spectrum are registered: there are no placeholder workspaces. The switch uses `Segment.Track` /
`RadioButton.Segment`, workspace titles and `Workspace.*` AutomationIds. Ctrl+1…4 selects a registered workspace;
an unavailable index leaves selection unchanged. Acquisition preserves selection. Shared scene / optics / live-time / speed inputs
mark an existing result outdated; workspace view settings re-render without invalidating that result.

The geometry section shows the detector entrance (0.15 mm steel-equivalent), backing (2 mm) and reflector
gap (0.1 mm) read-only. Gain σ (%) and gain seed are editable acquisition inputs (defaults 3 and 1), followed
by Background BSR (default 0, detected background/source ratio at 200 keV). These inputs are disabled during
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
