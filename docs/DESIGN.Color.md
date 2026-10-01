# DESIGN.Color — colour tokens, contrast and theme switching

Scope: GCAM Studio colours (`src/Gcam.Studio/Themes/Tokens.*.xaml`) and how the theme switches at runtime.
Spacing is in [DESIGN.Layout](DESIGN.Layout.md), text in [DESIGN.Typography](DESIGN.Typography.md).

## Direction: "dark instrument"

Near-neutral cool greys and **one cyan accent used only where it means something** — the primary action,
selection, keyboard focus and progress. No glow, gradients or neon text. Dark is the default because image
data reads better on dark chrome (as in ImageJ, 3D Slicer or PACS viewers); a light theme with the same keys
shows it is a choice, not a limitation.

## Tokens

Every colour exists as a `Color.*` and a matching `Brush.*`; views use the brush with `DynamicResource`.

| Key | Dark | Light | Use |
|---|---|---|---|
| `Brush.Bg.Canvas` | `#121619` | `#F3F5F7` | window background, input background |
| `Brush.Bg.Surface` | `#191F25` | `#FFFFFF` | panels, bars |
| `Brush.Bg.Raised` | `#222A32` | `#F0F3F5` | buttons, popups, progress track |
| `Brush.Bg.Hover` | `#2A343D` | `#E6EBEF` | hover |
| `Brush.Line` | `#2B353E` | `#DDE3E8` | panel borders, dividers (decorative) |
| `Brush.Line.Control` | `#5E6E7A` | `#7F8D99` | input / button borders |
| `Brush.Text.Primary` | `#E8EDF1` | `#1B242D` | main text |
| `Brush.Text.Secondary` | `#A6B1BB` | `#56636F` | labels, secondary text |
| `Brush.Text.Disabled` | `#6B7883` | `#9AA5AE` | disabled text |
| `Brush.Accent` | `#4FC8E3` | `#0D6583` | primary action, selection, progress |
| `Brush.Accent.Hover` / `.Pressed` | `#6FD4EA` / `#38AFCA` | `#0F7596` / `#0A5269` | primary button states |
| `Brush.Accent.Subtle` | accent @ 14 % | accent @ 12 % | selected row background |
| `Brush.OnAccent` | `#0B1215` | `#FFFFFF` | text on the accent |
| `Brush.Focus` | `#7FDCF0` | `#0D6583` | keyboard focus ring |
| `Brush.Status.Success` / `.Warning` / `.Error` | `#58C98B` / `#E3B342` / `#F2766F` | `#1E7F4F` / `#9A6A00` / `#C2362E` | run state, validation |
| `Brush.Selection` | accent @ 35 % | accent @ 30 % | text selection |
| `Brush.Plot.Series1` | `#6ACD91` | `#17693C` | first trace |
| `Brush.Plot.Series2` | `#F2C66D` | `#855500` | second trace |
| `Brush.Plot.Series3` | `#B5A1F2` | `#6742A6` | third trace |
| `Brush.Plot.Band` | `#266ACD91` | `#2617693C` | translucent X interval |
| `Brush.Plot.Grid` | `#394650` | `#CCD4DC` | axis grid |

## Contrast (WCAG 2.1)

Dark, against `Bg.Surface`: Text.Primary 14.1:1 · Text.Secondary 7.6:1 · Accent (as text) 8.5:1 ·
Focus 10.6:1 · Success 8.0 · Warning 8.5 · Error 6.0 — all **AA**. `Line.Control` 3.15:1 meets the 3:1
non-text minimum for control boundaries. `OnAccent` on `Accent` 9.6:1. Light: `Accent` on white 6.5:1,
`Line.Control` 3.4:1 on white and 3.1:1 on `Bg.Canvas` (it was `#8A97A2`, 2.99:1 — just under the minimum).
`Text.Disabled` is for disabled controls only (3.7:1 dark, 2.5:1 light): informational text uses `Text.Secondary`.

## Data colours vs UI colours

- Heatmaps use **viridis** (perceptually uniform, colour-blind safe), always with a colour bar and numeric ticks
  ([DESIGN.Controls](DESIGN.Controls.md#colorbar)).
- **Image overlays never use the accent** — cyan would collide with viridis' teal band. Overlays are white on a
  black chip, which stays legible on any colormap. A *selected* overlay (measurement, source marker) is shown by a
  thicker line and an inverted chip (black on white), so selection needs no colour either and looks the same in
  both themes.
- **No cyan at rest.** The progress bar is shown only while running, and the decoded-peak chip is a neutral
  reading (mono text, control border) — a reading is neither an action nor a selection.
- Status colours are never the only signal: the status dot sits next to a sentence, a selected row also gets
  an accent bar.

## Theme switching

`IThemeService` (contract in `Gcam.Studio.Core`) → `ThemeService` (WPF, `src/Gcam.Studio/Services`):

1. Replace the `Themes/Tokens.*.xaml` dictionary **in place** in `Application.Resources.MergedDictionaries`.
   Inserting a second one would not work: later merged dictionaries win, so the old one would stay in effect.
2. Because views and styles reference colours with `DynamicResource`, every open window repaints.
3. Ask DWM for a matching title bar (`DWMWA_USE_IMMERSIVE_DARK_MODE`) and force a frame refresh
   (`SetWindowPos … SWP_FRAMECHANGED`) — the attribute alone doesn't repaint an existing frame.

The ViewModel exposes `ToggleThemeCommand` and `ThemeToggleLabel`; the top-bar button binds to them.

## Adding a colour

Add it to **both** token files with the same key (`Color.X` + `Brush.X`), check contrast against the surface it
sits on, and use it through `DynamicResource`.
