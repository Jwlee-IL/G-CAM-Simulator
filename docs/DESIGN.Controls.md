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
| Segmented picker | `Segment.Track` (Border) + `RadioButton.Segment` | one radio button per option in a `UniformGrid`; checked = subtle fill + accent text + semibold (not colour alone); access keys (`_Distance` → Alt+D). Bind with `EnumMatchConverter` (tool picker) |
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
| Overlay API | `ViewChanged` (data, resize, DPI, zoom, pan), `ScreenToMm` / `MmToScreen`, `ExtentMm`, `HasImage` — what an adorner needs to draw in mm |
| Options | `ShowReadoutOverlay` (draw the readout on the image, or let the host show `Readout`), `EmptyText`, `Foreground`, `FrameBrush`, `FocusBrush` |
| Accessibility | `HeatmapViewAutomationPeer`: control type Image, class `HeatmapView`, keyboard-focusable; `ItemStatus` = zoom + readout |
| Geometry | `HeatmapViewport` (Core): fit, snapping, zoom about an anchor, pan limits, screen ↔ image ↔ mm — 11 tests |

### MeasurementAdorner (via MeasurementOverlay)

Measurements and draggable markers drawn **over** a `HeatmapView`, in the window's adorner layer. The heatmap stays
a pure data viewer; everything about measuring lives in the adorner.

| Aspect | Implementation |
|---|---|
| Attaching | attached properties on the heatmap, so the view stays XAML-only: `MeasurementOverlay.Session` (the `MeasurementsViewModel`), `Pane` (`Flood` / `Reconstruction`), `Markers` (any `IPlaneMarker` items — the scene's sources), `SelectedMarker` (two-way), `CanMoveMarkers` (bound to `IsIdle`). The adorner is added on `Loaded` and removed on `Unloaded`, so it never outlives the element or holds the ViewModels |
| State | none on screen: measurements and markers are mm values in the ViewModels, mapped through `HeatmapView.MmToScreen` on every render, so they follow zoom and pan. Only the gesture in progress (draft points, the marker being dragged) lives in the adorner |
| Input ownership | **Pan** tool: `HitTestCore` returns nothing except over a marker, so drags and the wheel fall through to the heatmap. **Measuring** tools: the adorner takes the pointer and forwards the wheel (`HeatmapView.ZoomStep`) and the hover readout (`HoverAt`) |
| Gestures | distance and ROI: press–drag–release (shorter than 4 px is ignored) · angle: three clicks, vertex second · Esc or right-click: abandon the draft · Delete (heatmap focused): remove the selected measurement · drag a marker (Pan tool, idle only): move the source, snapped to 0.1 mm. Points are clamped to the image |
| Result | a finished gesture becomes a `MeasurementDraft` sent through `MeasurementsViewModel.AddCommand` — the control never constructs a measurement itself |
| Drawing | white 1.5 px lines on a 4 px black halo, labels as white-on-black chips (as `HeatmapView`'s own). **Selected** = 2.5 px line + inverted chip, not a colour: the accent would collide with viridis' teal ([DESIGN.Color](DESIGN.Color.md#data-colours-vs-ui-colours)). Drafts are dashed with a live value |
| Accessibility | the adorner itself is not in the automation tree; every measurement is a row in the results table, named by `MeasurementViewModel.Description` ("M2 ROI on Flood: Σ 1,234"). Sources can be moved from the keyboard with the X / Y fields |
| Maths | `MeasurementMath` (Core): distance, angle at a vertex, ROI sum / mean / max by pixel centre — 5 tests |

**Known gap (rule 4):** measurements are created with the pointer only. Keyboard users can review, select and delete
them; a keyboard crosshair for creating them is planned with step 4.

### ColorBar

`FrameworkElement` drawing the colormap gradient with `TickCount` numeric labels between `Minimum` and
`Maximum`, in `Font.Mono`. Bind it to a heatmap: `Minimum="{Binding DataMin, ElementName=Flood}"`. Tick labels
pick their format from the range and never show `-0`.

## Known dark-theme pitfalls (handled)

ComboBox popup and highlight · button hover background · TextBox caret / selection · black dotted focus ·
ListBox inactive-selection grey · Aero progress glow · light title bar (DWM) · `-0` labels from rounding.
Not styled yet because unused: CheckBox, Slider, ContextMenu, DataGrid — style them when they are introduced
(the results table is a `ListBox` with a column template, so it reuses the list style instead of a `DataGrid`).
