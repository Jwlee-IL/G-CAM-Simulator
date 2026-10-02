# DESIGN.Typography — fonts, type scale and text roles

Scope: GCAM Studio text (`src/Gcam.Studio/Themes/Typography.xaml` + the font keys in `Metrics.xaml`).

## Fonts

| Key | Stack | Why |
|---|---|---|
| `Font.UI` | Segoe UI Variable Text → Segoe UI → Malgun Gothic | Windows UI font; Malgun Gothic covers Korean |
| `Font.Mono` | Cascadia Mono → Consolas | numbers, coordinates and units line up in columns |

Both ship with Windows, so nothing is embedded. Text renders with `TextFormattingMode=Display` for crisp small
sizes.

## Scale

| Key | Size | Use |
|---|---|---|
| `FontSize.Caption` | 11 | chips, colour-bar ticks, readouts |
| `FontSize.Label` | 12 | labels, panel titles, status line |
| `FontSize.Body` | 13 | default text, inputs, buttons |
| `FontSize.Title` | 14 | app name |

## Text roles

Views pick a role; they never set `FontSize`, `FontWeight` or `Foreground` on a `TextBlock` directly.

| Role | Size · weight · colour | Use |
|---|---|---|
| `Text.AppTitle` | Title · SemiBold · Primary | app name in the top bar |
| `Text.PanelTitle` | Body · SemiBold · Primary | top of a panel ("Sources", "Detector flood map") |
| `Text.SectionTitle` | Label · SemiBold · Secondary | a group inside a panel ("Selected source", "Measurements") |
| `Text.Strong` | Body · SemiBold · Primary | emphasised body text (the isotope in a source row) |
| `Text.Body` | Body · Regular · Primary | default text |
| `Text.Label` | Label · Regular · Secondary | field labels, secondary text, status sentence |
| `Text.Caption` | Caption · Regular · Secondary | chips, small notes |
| `Text.CaptionWarning` | Caption · Regular · Status.Warning | a status chip ("outdated") |
| `Text.ColumnHeader` | Caption · SemiBold · Secondary | table column headers |
| `Text.Mono` | Label · Mono · Primary | coordinates, values with units |
| `Text.MonoCaption` | Caption · Mono · Secondary | unit suffixes, the peak chip, measurement detail |
| `Text.Readout` | MonoCaption, height `Size.ReadoutLine` | the hovered-pixel line under an image (reserved, so the layout doesn't jump) |

All roles derive from `Text.Base` (UI font, Body size, Primary colour, vertical centre, ellipsis trimming).
Colours inside roles are `DynamicResource`, so text follows the theme. Headings carry no margin — the
`PanelHeader` row owns the gap ([DESIGN.Layout](DESIGN.Layout.md#screen-grid-mainwindow)).

Text drawn by controls (heatmap labels, overlay chips, colour-bar ticks) uses the inherited
`TextElement.FontFamily`, which the view sets to `Font.Mono` on the heatmaps — no control hard-codes a typeface,
and every number on screen is in the same mono face.

Plot Y titles reserve a measured text-height row and a 4-DIP gap above the plot; the top tick
label keeps its own half-height margin. Log decades use grouped integer labels through 100,000,
then superscript powers, avoiding a second shared multiplier on an individual log tick.

## Numbers

One rule per kind of number (Core `TickFormatter`, `NumberFormat`, unit-tested):

| Kind | Rule | Example |
|---|---|---|
| Linear tick (axis, colour bar) | decimals the tick **step** needs, thousands grouped, shared ×10ⁿ outside 0.1…9999 | `0 10 20`, `1,000 2,000`, `−2 0 2` |
| Continuous reading (decoded intensity, ratio R) | four significant digits, grouped, trailing fractional zeros dropped; integers in full | `18.53`, `3,077`, `0.275` |
| Counts | integer, grouped | `6,463` |
| Fixed-resolution time (event acquisition time) | all digits kept (ns), fraction grouped in threes by a narrow no-break space | `0.853 889 538 s` |
| Percent | gain σ at most one decimal (`3%`); a resolution keeps its stated two decimals (`4.57%`) | `3%`, `4.57%` |

Message lines (`Text.CaptionWarning`) collapse when they have no text, so an empty error takes no height.

## Adding a role

Derive from the nearest existing role (`BasedOn`), change one or two properties, name it `Text.<Purpose>`, and
add a row here.
