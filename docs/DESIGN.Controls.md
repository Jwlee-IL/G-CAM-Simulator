# DESIGN.Controls — shared control styles and custom controls

Scope: re-templated WPF controls (`src/Gcam.Studio/Themes/Controls.xaml`) and Studio's own controls
(`src/Gcam.Studio/Controls`). Where controls fit in the layers: [DESIGN.ViewLayer](DESIGN.ViewLayer.md).

## Why re-template

Default WPF (Aero2) templates hard-code system colours — white combo popups, grey list selection, a black
dotted focus rectangle — and the button template ignores `Background` on hover. Every control used in Studio
is therefore re-templated against the tokens in [DESIGN.Color](DESIGN.Color.md).

## Shared styles

| Style | Key | States / notes |
|---|---|---|
| Focus ring | `FocusVisual` | 2 px `Brush.Focus`, 3 px outside the control, on every focusable control |
| Button | implicit | raised fill · hover · pressed · disabled |
| Primary button | `Button.Primary` | accent fill, `OnAccent` text — **one per screen** (Simulate) |
| Ghost button | `Button.Ghost` | borderless, for bars and panel headers |
| TextBox | implicit | mono text; unit suffix via `Tag` (`Tag="mm"`); accent 2 px border on focus; error border + tooltip when `Validation.HasError`; themed caret and selection |
| ComboBox / ComboBoxItem | implicit | full template including popup, arrow and item highlight |
| ListBox / ListBoxItem | implicit | selection = subtle fill **+ 2 px accent bar** (not colour alone), stays visible when unfocused, inset focus ring |
| ScrollBar | implicit | 8 px, no arrow buttons |
| ProgressBar | implicit | flat 4 px, no Aero glow animation |
| ToolTip | implicit | raised surface |
| Panel / Divider / Chip | `Panel.Surface` / `Divider` / `Chip` | `Border` styles for layout blocks |

Add a shared style only when two or more views need it; otherwise keep it in the view's `Resources`, still
built from tokens.

## Custom controls

Write a control (rather than XAML composition) when an element draws pixels itself or handles its own pointer
and keyboard gestures. Each control follows the same five rules:

1. **Logic out, drawing in.** UI-free geometry lives in `Gcam.Studio.Core` and is unit-tested
   (`HeatmapViewport`); the control translates input into calls on it and draws the result.
2. **Dependency properties** for everything a host sets (`AffectsRender` where a change needs a repaint), and
   **read-only DPs** for what a host may bind to.
3. **No colours of its own.** Brushes are DPs the view sets from tokens. The only fixed colours are image
   overlays (white on a black chip).
4. **Keyboard parity** — anything the mouse can do has a key.
5. **Own focus ring and automation peer.** The default `FocusVisualStyle` is off, and a bare
   `FrameworkElement` has no automation peer, which makes it invisible to screen readers and UI tests.

### HeatmapView

| Aspect | Implementation |
|---|---|
| Base | `FrameworkElement` + `WriteableBitmap` |
| Data | `Image` (`DetectorImage`, row 0 at the bottom); `OriginMm` / `StepMm` — pixel *i* is centred at `origin + i·step` mm |
| Rendering | bitmap rebuilt only when `Image` changes (viridis LUT, min–max normalised); zoom / pan only move the destination rect in `OnRender`; nearest-neighbour scaling; 1 px `FrameBrush` outline so dark colormap ends don't melt into a dark panel |
| Pixel accuracy | at fit, a whole number of **device** pixels per cell (`PixelsPerDip`), so cells stay equal width at 125 / 150 % scaling |
| Input | wheel: zoom about the cursor · drag: pan · double-click: fit · `+` / `−`: zoom · arrows: pan · `0` / Home: fit |
| Bindable output | `Readout` ("x, y mm · value" of the hovered pixel), `DataMin` / `DataMax`, `Zoom` |
| Options | `ShowReadoutOverlay` (draw the readout on the image, or let the host show `Readout`), `EmptyText`, `Foreground`, `FrameBrush`, `FocusBrush` |
| Accessibility | `HeatmapViewAutomationPeer`: control type Image, class `HeatmapView`, keyboard-focusable; `ItemStatus` = zoom + readout |
| Geometry | `HeatmapViewport` (Core): fit, snapping, zoom about an anchor, pan limits, screen ↔ image ↔ mm — 11 tests |

### ColorBar

`FrameworkElement` drawing the colormap gradient with `TickCount` numeric labels between `Minimum` and
`Maximum`, in `Font.Mono`. Bind it to a heatmap: `Minimum="{Binding DataMin, ElementName=Flood}"`. Tick labels
pick their format from the range and never show `-0`.

## Known dark-theme pitfalls (handled)

ComboBox popup and highlight · button hover background · TextBox caret / selection · black dotted focus ·
ListBox inactive-selection grey · Aero progress glow · light title bar (DWM) · `-0` labels from rounding.
Not styled yet because unused: CheckBox, Slider, ContextMenu, DataGrid — style them when they are introduced.
