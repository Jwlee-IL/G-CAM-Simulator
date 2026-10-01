# DESIGN.Layout — spacing, sizes and the screen grid

Scope: GCAM Studio layout metrics (`src/Gcam.Studio/Themes/Metrics.xaml`) and how `MainWindow` is laid out.

## Spacing: a 4 px grid

| Kind | Keys (value) |
|---|---|
| Scale | `Space.1` 4 · `Space.2` 8 · `Space.3` 12 · `Space.4` 16 · `Space.5` 24 · `Space.6` 32 |
| Padding (`Pad.*`, inside a container) | `Pad.Window` 12 · `Pad.Panel` 12 · `Pad.Bar` 16,0 · `Pad.Control` 8,0 · `Pad.Button` 12,0 · `Pad.ListItem` 8,0 |
| Margin (`Gap.*`, after an element) | `Gap.Field` 0,0,0,8 · `Gap.Section` 0,0,0,16 · `Gap.Title` 0,0,0,8 · `Gap.Inline` 0,0,8,0 · `Gap.InlineWide` 0,0,16,0 |
| Grid gutter | `Gutter` 12 (a `GridLength` for gutter columns) |

**Padding vs margin rule.** A container owns its inner space (`Pad.*`). Space *between siblings* is the
preceding sibling's bottom or right margin (`Gap.*`) — never split one gap between two elements, and never
combine a container's padding with a child's margin for the same edge.

## Sizes and shape

| Key | Value | Used for |
|---|---|---|
| `Size.Control` | 28 | min height of buttons, inputs, list rows |
| `Size.Bar` / `Size.StatusBar` | 48 / 28 | top bar / status bar height |
| `Size.SidePanel` | 300 | scene panel width |
| `Size.FieldLabel` | 84 | label column in forms |
| `Radius.Control` / `Radius.Panel` / `Radius.Chip` | 3 / 6 / 10 | corners |
| `Border.Hairline` | 1 | all borders |

## Screen grid (`MainWindow`)

```
Row 0  Top bar (Size.Bar)          identity · theme toggle · photon budget · Cancel · ▶ Simulate (primary, rightmost)
Row 1  Content (Pad.Window)
         Col 0  Scene panel (Size.SidePanel)   sources · selected source · geometry (read-only)
         Col 1  Gutter
         Col 2  Images: [flood map] Gutter [reconstruction]
                each = header (title + chip) · HeatmapView · ColorBar · readout line
Row 2  Status bar (Size.StatusBar) state dot · status sentence (live region) · progress + %
```

The primary action sits at the same place on every screen (top right); the status of the last run is always
visible at the bottom, full width, regardless of which panel has focus.

## Window sizing: fixed grid, flexible centre

- Side panels are a **fixed** width; the image area takes the rest (`*`).
- The window has a **minimum** size (1280×800) and a default of 1440×900 instead of a fixed size. A fixed
  window breaks on 1366×768 laptops and overflows the screen at 125 / 150 % scaling.
- If the work area is smaller than the default, the window starts maximised (`App.xaml.cs`).
- `UseLayoutRounding` and `SnapsToDevicePixels` are on; heatmap cells additionally snap to whole device pixels
  ([DESIGN.Controls](DESIGN.Controls.md#heatmapview)).

## Adding layout values

Stay on the 4 px grid, add a named key to `Metrics.xaml` instead of a literal in a view, and pick the kind by
role (`Pad` for inside, `Gap` for after, `Size` for fixed dimensions).
