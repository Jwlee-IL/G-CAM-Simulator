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
| `Text.PanelTitle` | Label · SemiBold · Secondary, bottom `Gap.Title` | panel and section headings |
| `Text.Body` | Body · Regular · Primary | default text |
| `Text.Label` | Label · Regular · Secondary | field labels, secondary text, status sentence |
| `Text.Caption` | Caption · Regular · Secondary | chips, small notes |
| `Text.Mono` | Label · Mono · Primary | coordinates, values with units |
| `Text.MonoCaption` | Caption · Mono · Secondary | readouts, unit suffixes, ticks |

All roles derive from `Text.Base` (UI font, Body size, Primary colour, vertical centre, ellipsis trimming).
Colours inside roles are `DynamicResource`, so text follows the theme.

## Adding a role

Derive from the nearest existing role (`BasedOn`), change one or two properties, name it `Text.<Purpose>`, and
add a row here.
