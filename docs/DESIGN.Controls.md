# DESIGN.Controls — shared control styles and custom controls

Scope: re-templated WPF controls (`src/Gcam.Studio/Themes/Controls.xaml`) and Studio's own controls
(`src/Gcam.Studio/Controls`). Where controls fit in the layers: [DESIGN.ViewLayer](DESIGN.ViewLayer.md).

## Why re-template

Default WPF (Aero2) templates hard-code system colours — white combo popups, grey list selection, a black
dotted focus rectangle — and the button template ignores `Background` on hover. Every control used in Studio
is therefore re-templated against the tokens in [DESIGN.Color](DESIGN.Color.md).

## Shared styles

Imaging's found peaks use the existing measurement overlay's neutral halo / line / chip treatment. A diamond
and the text "Found <isotope>" distinguish them from true-source rings without relying on colour. Found peaks
are read-only and excluded from drag hit testing. The coordinate list provides an accessible text equivalent.
`Size.Imaging.FoundMarker` is a 7-DIP diamond radius (matching the truth ring's scale);
`Space.Imaging.FoundLabel` is a 4-DIP gap to its label. Both live in Metrics.xaml and are theme-independent.
The Imaging options panel reuses the existing label, caption, mono-caption, control and gap resources; it adds
no new colour token. It shows All / isotope, shared window N, strip, R, worker costs and found coordinates above
the measurement tools.

| Style | Key | States / notes |
|---|---|---|
| Focus ring | `FocusVisual` | 2 px `Brush.Focus`, 3 px outside the control, on every focusable control |
| Button | implicit | raised fill · hover · pressed · disabled |
| Primary button | `Button.Primary` | accent fill, `OnAccent` text — **one per screen** (Simulate) |
| Ghost button | `Button.Ghost` | borderless, for bars and panel headers |
| TextBox | implicit | mono text; unit suffix via `Tag` (`Tag="mm"`); accent 2 px border on focus; error border + tooltip when `Validation.HasError`; themed caret and selection |
| CheckBox | implicit | themed box and visible check mark, focus ring, hover border and disabled state; used for Spectrum log Y / pile-up; DockPanel constrains wrapping content to the available width |
| ComboBox / ComboBoxItem | implicit | full template including popup, arrow and item highlight |
| ListBox / ListBoxItem | implicit | selection = subtle fill **+ 2 px accent bar** (not colour alone), stays visible when unfocused, inset focus ring |
| Segmented picker | `Segment.Track` (Border) + `RadioButton.Segment` | one radio button per option in a `UniformGrid`; checked = subtle fill + accent text + semibold (not colour alone); access keys (`_Distance` → Alt+D). Bind with `EnumMatchConverter` (tool picker) |
| ScrollBar | implicit | 8 px, no arrow buttons |
| ProgressBar | implicit | flat 4 px, no Aero glow animation |
| ToolTip | implicit | raised surface |
| Panel / Divider / Chip | `Panel.Surface` / `Divider` / `Chip`, `Chip.Warning` | `Border` styles for layout blocks; the warning chip pairs with `Text.CaptionWarning` |
| Header row | `PanelHeader` (DockPanel) | title left, actions / chips right; min height `Size.Control`, `Gap.Title` below |
| Icon | `Icon.Play` (Geometry) | drawn with a `Path` in the button's foreground — no glyph characters as icons |

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
| Bindable output | `Readout` ("x, y mm · value unit" of the hovered pixel), `DataMin` / `DataMax`, `Zoom` |
| Overlay API | `ViewChanged` (data, resize, DPI, zoom, pan), `ScreenToMm` / `MmToScreen`, `ExtentMm`, `HasImage` — what an adorner needs to draw in mm |
| Options | `ValueUnit` ("counts" for flood, "(decoded)" for reconstruction), `ShowReadoutOverlay` (draw the readout on the image, or let the host show `Readout`), `EmptyText`, `Foreground`, `FrameBrush`, `FocusBrush` |
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
| Capture loss | Alt+Tab, the Windows key or a modal can take the mouse mid-gesture: `OnLostMouseCapture` ends a marker drag and abandons a press-drag draft, and a marker moves only while the adorner holds the capture |
| Gestures | distance and ROI: press–drag–release (shorter than 4 px is ignored) · angle: three clicks, vertex second · Esc or right-click: abandon the draft · Delete (heatmap focused): remove the selected measurement · drag a marker (Pan tool, idle only): move the source, snapped to 0.1 mm. Points are clamped to the image |
| Result | a finished gesture becomes a `MeasurementDraft` sent through `MeasurementsViewModel.AddCommand` — the control never constructs a measurement itself |
| Drawing | white 1.5 px lines on a 4 px black halo, labels as white-on-black chips (as `HeatmapView`'s own) in the inherited mono font. A source marker is highlighted only while dragged or when there are several sources (with one, "selected" says nothing and would compete with the selected measurement). **Selected** = 2.5 px line + inverted chip, not a colour: the accent would collide with viridis' teal ([DESIGN.Color](DESIGN.Color.md#data-colours-vs-ui-colours)). Drafts are dashed with a live value |
| Accessibility | the adorner itself is not in the automation tree; every measurement is a row in the results table, named by `MeasurementViewModel.Description` ("M2 ROI on Flood: Σ 1,234"). Sources can be moved from the keyboard with the X / Y fields |
| Maths | `MeasurementMath` (Core): distance, angle at a vertex, ROI sum / mean / max by pixel centre — 5 tests |

**Known gap (rule 4):** measurements are created with the pointer only. Keyboard users can review, select and delete
them; a keyboard crosshair for creating them is planned with step 4.

### PlotView

`FrameworkElement` with `OnRender`, first-party and independent of ScottPlot. Axis / viewport / tick maths and
the exact min/max pyramid live in Core `Plotting/`. Host properties: `Series`, `Bands`, `Markers`, `LogY`,
`XLabel`, `YLabel`, `XUnit`, `YUnit`, `XFormat`, `YFormat`, `ViewRange`, `EmptyText`; `Readout` is read-only (line pointer coordinates or histogram bin counts / bounds), shown by a host TextBlock
like HeatmapView with its overlay disabled; line pointer readout updates do not redraw the series. `PlotSeries` arrays are
immutable after publication, with increasing explicit X or origin + positive step, Line / Area / Histogram, and a colour role.
Histograms use N+1 explicit edges or edge origin + positive width; negative counts are rejected.
Each series is capped at 10 million finite samples; a new `Series` value prepares the pyramid once. Stored
extrema start at complete 64-sample blocks, then double in size, using at most N/16 extra doubles.

Rendering uses the coarsest aligned blocks wholly inside a pixel column, scanning raw samples only for
unaligned head / tail fragments within 64-sample blocks. Axis margins use measured tick / label text; X tick
labels are centred by their measured width.
One frozen `StreamGeometry` per series preserves column extrema (about two points per device column), with a
translucent baseline fill for Area. Log Y clamps counts at 1, matching the legacy spectrum; linear ticks use
1–2–5 steps with `TickFormatter` engineering labels. Log ticks have decades and 2…9 minor ticks;
decades through 10⁵ use grouped numbers ("10,000"), higher decades use superscript powers ("10⁶").
The Y title occupies its own measured text row plus a 4-DIP gap above the top tick label.
Bands use neutral `BandBrush` tint and `BandEdgeBrush` boundaries drawn above the series fill, so windows
remain distinct from data in both themes. Brushes come from the implicit theme style.

Wheel / + / − zoom X; drag / arrows pan; double-click / 0 / Home reset. Resizing retains the X viewport;
capture loss ends dragging. A focus ring and `PlotViewAutomationPeer` expose keyboard focus, zoom and readout.
The opt-in 10M desktop gate measures CPU `OnRender` after zoom and resize, including ticks, query, geometry and
drawing commands; dispatcher / compositor delay is recorded separately. Results and limitations are in
[VV.Studio](VV.Studio.md#plot-performance-gate).

Histogram bins at least two device pixels wide draw steps filled to zero (one on log Y); narrower bins use
exact min/max columns, including bins intersecting each column. Empty bins occupy their full width at the floor.
`PlotGeometry` supplies both paths in Core. Data replacement preserves X zoom / pan for an unchanged full X
range; clearing data, changing the full range or explicit reset fits X. Log switching and resize preserve X.
A one-way `ViewRange` requests a clamped X interval.

Y follows visible bins / crossing trace segments: 8 percent linear headroom, or the next half decade on log Y.
Data updates only grow the top; navigation and log changes recompute it. Histogram hover highlights a bin and
draws a thin centre cursor with a readout such as `662.4 keV (660.9 – 663.9) · 1,234 counts`; default precision
follows bin width, with optional X / Y formats. Line readout retains continuous coordinates without redrawing.
Core `PlotBandLayout` centres labels on bands, clamps them to the plot and moves collisions to additional rows;
text wider than the plot is ellipsized. Layout includes `BandLabelPadding` from `Pad.Plot.BandLabel`
(4 DIP on every side). Labels draw last on opaque `BandLabelBrush` plates using `Brush.Bg.Surface`,
so band edges, grid lines and data cannot cross the text, even when the label is wider than its band.

Spectrum table selection requests the window plus one window width on each side. Snapshot row replacement
retains selection without re-requesting zoom. Whole-window content and plot offscreen evidence uses `Gcam.Studio.RenderTests`, opt-in with
`GCAM_RENDER_SNAPSHOTS=1`, and creates no window or desktop input.

### ColorBar

`FrameworkElement` drawing the colormap gradient with a target `TickCount` of numeric labels between `Minimum` and
`Maximum`. `NiceTicks.Linear` chooses 1–2–5 steps inside the actual range, with tick marks placed by their values;
labels centre on each mark and clamp to the strip edges. A constant image has one labelled value.
Text uses `Font.Mono`; strip height `BarHeight` is set from `Size.ColorBar`. Bind it to a heatmap:
`Minimum="{Binding DataMin, ElementName=Flood}"`. Labels come from `TickFormatter` (Core, tested): one shared power
of ten when values are very small or large (`1.6 … 16.1 ×10⁻³` instead of `0.00157 … 0.0161`), the same number
of decimals on every tick, never `-0`.

### ImageStackPanel

Lays out an image with its legend: the first child stays **square** and as large as fits; the colour bar and the
readout stack directly beneath it at the image's width; spare height collects below. The images are square grids —
letting the heatmap fill a tall panel centred the image in empty space with the legend ~175 px away from it.

## Known dark-theme pitfalls (handled)

ComboBox popup and highlight · button hover background · TextBox caret / selection · black dotted focus ·
ListBox inactive-selection grey · Aero progress glow · light title bar (DWM) · `-0` labels from rounding.
Not styled yet because unused: Slider, ContextMenu, DataGrid — style them when they are introduced
(the results table is a `ListBox` with a column template, so it reuses the list style instead of a `DataGrid`).

## DetectorFaceView

`DetectorFaceView` is a static reusable FrameworkElement. Face is the pure geometric model;
GapBrush comes from DynamicResource Brush.Bg.Canvas. It fits a square face without
snapping thin gaps to a raster subcell. Exact vector rectangles encode active crystals and disjoint dead-area
strips, with physical row zero at the bottom. Opaque viridis colours map the seeded relative gain range;
the surrounding Detector view supplies a colour bar with numeric minimum/maximum and labels the static gain
pattern. This visual mapping does not modify counts. A FrameworkElementAutomationPeer exposes Image,
the host's accessible name and help text. The acquired face stays fixed after pending detector edits.
