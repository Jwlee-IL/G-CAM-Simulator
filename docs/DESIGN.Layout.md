# DESIGN.Layout — spacing, sizes and the screen grid

Scope: GCAM Studio layout metrics (`src/Gcam.Studio/Themes/Metrics.xaml`) and how `MainWindow` is laid out.

## Spacing: a 4 px grid

| Kind | Keys (value) |
|---|---|
| Scale | 4 · 8 · 12 · 16 · 24 · 32 — expressed as the `Pad.*` / `Gap.*` Thickness keys below (XAML can't build a `Thickness` from `Double` keys, so there are no separate `Space.*` keys) |
| Padding (`Pad.*`, inside a container) | `Pad.Window` 12 · `Pad.Panel` 12 · `Pad.Bar` 16,0 · `Pad.Control` 8,0 · `Pad.Button` 12,0 · `Pad.ListItem` 8,0 · `Pad.None` 0 (icon buttons, segment tracks of control height) |
| Plot label plate | `Pad.Plot.BandLabel` 4 on every side — prevents edges, grid and traces from touching band text; padded widths also determine collision rows |
| Margin (`Gap.*`, after an element) | `Gap.Tight` 0,0,0,4 · `Gap.Field` 0,0,0,8 · `Gap.Section` 0,0,0,16 · `Gap.Title` 0,0,0,8 · `Gap.Inline` 0,0,8,0 · `Gap.InlineWide` 0,0,16,0 · `Gap.InlineWide.Leading` 16,0,0,0 (before an item that follows another on the line: status-bar messages) · `Gap.Tool` 0,0,4,0 (between tool toggles) · `Gap.RowAction` 8,0,0,0 (before a row's action button) · `Gap.TabContent` 0,12,0,0 (between a tab strip and its content) · `Gap.ScrollGutter` 0,0,12,0 (a list header over scrolling rows) · `Gap.Caption.Focus` 2 (caption-button focus cue inset, Options badge offset) |
| Grid gutter | `Gutter` 12 (a `GridLength` for gutter columns) · `Gap.Row` 0,0,0,12 (the same gutter between stacked panels) |

**Padding vs margin rule.** A container owns its inner space (`Pad.*`). Space *between siblings* is the
preceding sibling's bottom or right margin (`Gap.*`) — never split one gap between two elements, and never
combine a container's padding with a child's margin for the same edge.

## Sizes and shape

| Key | Value | Used for |
|---|---|---|
| `Size.Control` | 28 | min height of buttons, inputs, list rows |
| `Size.Bar` | 48 | top bar height (`GridLength`); the status bar is one `Size.Control` line plus its top hairline |
| `Size.Caption` · `Size.CaptionButton` | 47 · 46 | `WindowChrome` caption height (`Size.Bar` − 1, so the bar's bottom line runs under the caption buttons) · caption-button width |
| `Size.Control.Row` | 20 | a row action button inside a `Size.Control` list row (`Button.Icon.Row`: a `Size.Icon` glyph plus 4 on each side) |
| `Size.ScrollBar` · `Size.ScrollGutter` | 8 · 12 | scroll bar width · the gutter every vertically scrolling viewer reserves at its right (4 gap + 8 bar), whether or not the bar shows |
| `Size.Options` · `Size.Options.LockCaption` · `Size.OptionsBadge` | 520 · 16 · 4 | Options dialog height (one size across its tabs) · its reserved lock-caption line · the non-default badge on the cog |
| `Size.SidePanel` | 300 | left panel and workspace right panel width (`GridLength`) |
| `Size.SourceList` / `Size.ResultsList` | 168 / 280 | **max** heights: lists grow with their rows, then scroll — no reserved empty space |
| `Size.NumberInput` · `Size.Progress` | 120 · 160 | general numeric input width · progress bar |
| `Size.NumberInput.Short` | 80 | short live-time / speed values with s / × suffixes; preserves top-bar space |
| `Size.ColorBar` · `Size.ReadoutLine` | 8 · 16 | colour-bar strip · the reserved readout line under an image |
| `Size.Logo` · `Size.Icon` · `Size.StatusDot` | 16 · 12 · 8 | top-bar mark · button icon · run-state dot |
| `Size.Column.Id` / `Size.Column.Short` / `Size.Column.Value` | 36 / 60 / 96 | fixed results-table columns; Spectrum uses local shared Auto numeric columns and `Gap.Inline` instead, with the label column taking the rest and numbers right-aligned |
| `Size.Imaging.Lower` | 240 | height always left under the Imaging image pair for measurements and the focus curve |
| `Size.FieldLabel` | 84 | label column in forms |
| `Radius.Control` / `Radius.Panel` / `Radius.Chip` | 3 / 6 / 10 | corners |
| `Border.Hairline` | 1 | all borders |

## Screen grid (`MainWindow`)

```
Row 0  Top bar (Size.Bar, the window caption)  identity · workspace switch · live time (s) · speed × · ▶ Start / Continue / ■ Stop · Reset (icon, confirms) · theme (icon) · Options (cog, badge) · caption buttons (minimise, maximise / restore, close)
Row 1  Content (a Border with Pad.Window)
         Col 0  Left panel (Size.SidePanel), a TabControl   Scene (Sources with + / −, Selected source) · Optics (Engineering
                geometry: preset, the five fields, geometry-performance help) · Detector (readout panel: Readout · Calibration
                lines · Preparation · Energy window); input errors at the panel foot, outside the tabs
         Col 1  Gutter
         Col 2  Selected workspace centre (DataTemplate by workspace type)
                Imaging (ImageAreaPanel): [flood map] Gutter [reconstruction] on top, each = PanelHeader (title + chips) ·
                ImageStackPanel: square HeatmapView (+ adorner), ColorBar and readout directly beneath, at the drawn
                image's width; below (Gap.Row), filling the remaining height: Measurements (header: title, Pan / Distance /
                Angle / ROI icon toggles, clear-all trash at the right; hint; table # · Image · Kind · Value · row delete;
                selected-row detail) full width, or left half beside the focus curve
                Spectrum: stepped histogram PlotView with line bands · readout · acquired-pulse summary · emission-window table (select a row to zoom to its window)
                Waveform: heading · view tabs Energy sum / Four outputs (A–D) · the plots (or the empty state)
                Detector: heading · view tabs Face / SiPM / Anger / Calibration / Diagnostics · the face and its colour bar,
                a readout view, or the empty state
         Col 3  Gutter
         Col 4  Selected workspace panel (DataTemplate by workspace type, Size.SidePanel)
                Imaging: scrollable collapsible sections — Decoder focal plane (field, geometry rows, one-line precision
                caption with the evidence behind a help mark) · Imaging channel (selector, physical-mode channel controls,
                reconstruction, window N, strip, result lines, found peaks) · Focus sweep (collapsed until used; opens on a result)
                Spectrum: Display (log Y) · Window (N × FWHM) · Pile-up (resolving time) · Resolution (read-only chain)
                Waveform: Scope window · Selected event · Rate study · Acquired chain · Events in window (table)
                Detector: Detector geometry · Gain pattern · Acquired, or on the physical readout Detector geometry ·
                Readout network · Measured conversions · Calibration (this session) · Confirmed offers
Row 2  Status bar (one Size.Control line)  state dot · run status, a refusal or the preparation outcome (live region) · Options summary right · top-bar input messages · one progress bar (acquisition or preparation, only while running)
Modal  ModalHost layer over the whole window: the Reset confirmation, the Options dialog (Size.Options high)
```

Top-bar live-time and speed inputs are vertically centred at `Size.Control` (28 DIP), with
`Size.NumberInput.Short` width (80 DIP), and Start / Continue / Stop share that height and centre line. Labels are
"Live time" / "Speed"; `Tag` supplies "s" / "×" inside the inputs, matching the scene fields. The seed moved to the
Options dialog's Acquisition tab (blank means a new seed per acquisition). Reset is an icon button right of the primary
action (then theme and the Options cog), never primary, with no shortcut and no default; the primary button reads Start
without data and Continue with data, and is not the window's default button, so Enter in a field cannot resume
counting. Every interactive top-bar control is hit-testable in the `WindowChrome` caption; the rest of the bar drags the
window. The caption buttons fill `Size.Caption` at the far right and rest transparent on the bar, whose bottom line runs
under them. Messages of the top-bar inputs (a rejected preset, an invalid seed) appear at the right of the status bar,
which has room for them.

The primary action sits at the same place on every screen (top right); the status of the last run is always
visible at the bottom, full width, regardless of which panel has focus.

Imaging, Spectrum, Waveform, Detector and Compare are registered. The switch uses `Segment.Track` /
`RadioButton.Segment`, workspace titles and `Workspace.*` AutomationIds. Ctrl+1…5 selects a registered workspace;
an unavailable index leaves selection unchanged. Acquisition preserves selection. Physical inputs (scene, optics,
detector, chain, seed) are disabled while acquired data exist and unlock after Reset; workspace view settings stay
editable in every state and only re-render retained data.

The left panel is a TabControl (Scene · Optics · Detector) in the shared `TabControl.Segment` style; each tab is a
scroller with `Gap.TabContent` above its sections. The Optics tab holds the engineering presets and the editable
geometry, always shown (a preset fills and disables the fields, Custom enables them), while decoder focus remains an
Imaging view setting. The Detector tab holds the readout panel: Readout (mode, summary, the experimental localisation
line), Calibration lines (keV · Record · Local), Preparation (accent Prepare, cancel, the ready table keV · Time ·
Windowed · Scored, a short state line) and Energy window (low / high side by side, the window line, the ±10 / ±15 / ±20 /
±25 % presets). Input errors stay at the foot of the panel whichever tab is selected.

The Options dialog (in the modal layer, `Size.Options` high on every tab, OK / Cancel or Close fixed at the bottom
right) holds the inputs changed rarely: Acquisition (seed), Detector (scintillator, photosensor, preamp / shaper; entrance
0.15 mm and backing 2 mm steel-equivalent facts; reflector gap, default 100 µm; Gain σ, default 3, `%` suffix; Gain seed,
default 1) and Environment (photon H*(10), preset and bound captions, ambient bound, Background, default 0, `× signal`
BSR suffix — detected background/source ratio at 200 keV — and the acquired ambient BSR). It edits a draft applied on OK;
no button is the default, so Enter in a field neither applies nor discards the draft (Escape cancels); while data exist it
shows the values read-only with the lock caption and one Close button. These inputs are disabled
during acquisition and while acquired data exist (until Reset); a refused Start selects the tab holding the invalid
input, or marks the cog for an Options input, and states the reason in the status bar (see Screen rules). Workspace
activation follows `IsActive` as well as commands, so a UI Automation SelectionItem selection changes the centre and
right panel.

## Screen rules (author's desktop review, 2026-10-13)

Normative; each rule with its reason is also in [AGENTS.Rationale](AGENTS.Rationale.md#visual-design).

- **No OS title bar.** The window is `WindowStyle="None"` with `WindowChrome`; the top bar is the caption (drag,
  double-click maximise / restore, snap, resize borders, Alt+Space / Alt+F4). Caption buttons follow the Windows
  convention: frameless at rest, full bar height, flush top-right, Close's hover red.
- **Program-wide settings live in the global chrome.** Options open from a cog icon in the top bar, never from a
  button inside a content panel. A non-default option is never hidden: a badge on the cog and a summary in the
  status bar.
- **Top-bar order:** workspace switch · acquisition inputs (live time, speed) · primary action (Start / Continue /
  Stop) · secondary global actions (Reset, theme, Options) · caption buttons.
- **One height per row.** Controls in one row share `Size.Control` and one centre line — a button is never taller
  than the text boxes beside it; only the caption buttons take the bar height.
- **A side panel holding several independent groups is a TabControl**, not a stack of expanders (left panel:
  Scene · Optics · Detector).
- **Every panel is sections:** a heading, a divider between sections, information as aligned label / value rows or
  a table. No run-on stacks of small text blocks, and no "·"-joined line of more than about three facts.
- **One visual language across panels** — the same heading style, divider and row layout in the left tabs, the
  workspace right panels and the dialogs.
- **The centre shows the visual; its legend facts and captions go to the side panel.** An image keeps only its
  colour bar and readout line beneath it.
- **Controls do not appear and vanish with a mode.** Fields driven by a preset are always shown: a preset writes its
  values and disables them; Custom enables them.
- **A refused action says why where the user is looking** — the status bar — and leads to the input (selects the
  tab holding it, or marks the cog for an Options input); a field message alone, on a hidden tab or in a closed
  dialog, is not enough.
- **The status bar is one line in every state**, fixed height: run status left, the Options summary right, both
  trimmed with the full text in a tooltip.
- **A dialog keeps one size across its tabs**; its OK / Cancel (or Close) never move.
- **Scrollbars keep a gap** from the content and from the panel border.

## Window sizing: fixed grid, flexible centre

- Side panels (the left tabs, the workspace panel right) are a **fixed** width; the image area takes the rest (`*`). At the
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

The Imaging right panel's Imaging channel section holds: All / isotope selector,
the shell-owned window N (also shown in Spectrum), Compton strip, calibrated R values, worker cost readout,
and found-peak coordinates. It reuses Gap.Field, Gap.Tight, Gap.Section and the existing text/control roles.
Selection changes both image grids and their bars/readouts together; ROI geometry stays in mm and its values
refresh against the selected grid. All shows the union of isotope-labelled found markers over the acquisition
reconstruction. A neutral diamond and "Found" label distinguish read-only peaks from display-only true-source rings.

The additional render fixtures cover two isotopes with All and Cs-137 selected, both themes and both target
sizes (1280×800 and 1440×900). Retained snapshots and the final four-workspace desktop survey are documented in [VV.Studio](VV.Studio.md);
the survey identified label crowding, subsequently addressed by the overlay layout and spacing changes. Marker metrics are documented in
[DESIGN.Controls](DESIGN.Controls.md).

## Editable optics and focus

The Optics and Detector inputs sit on the left panel's tabs (until 2026-10-13 they were `Expander.Section` sections
with one-line summaries). Errors stay visible outside the tab content, and a refused Start selects the tab holding the
invalid input. Physical fields and atomic engineering presets are disabled while acquiring and while data exist, until Reset.
The decoder focal plane is a separate Imaging view setting, in mm from the detector, enabled during and after
acquisition. Its readout uses acquired physical geometry after a run, including when pending inputs differ.
Geometry readings carry no pass/fail glyph; sampling has a conditional precision-evidence note. No depth-reach
hint is shown. The Imaging right panel scrolls independently so measurements remain reachable at small sizes.
Focus changes visibly explain why reconstruction measurements were cleared; flood measurements stay.
Offscreen render cases cover F=800 and each left tab at 1280×800 and 1440×900 in both themes.

## Waveform workspace and shared chain (2026-10-02)

The top workspace switch now includes Waveform (Ctrl+3). The detection chain's scintillator / photosensor / preamp selectors are on the Options dialog's Detector tab (until 2026-10-13 a Detection chain section in the left panel); they are disabled during acquisition, while acquired data exist and under the physical readout. Retained data are never repainted with other physics.

Waveform centre: a heading that follows the view, the view tabs Energy sum / Four outputs (A–D), then the plots. Energy sum stacks two flexible-height PlotViews: ADC energy sum above shaped output (fractional code equivalents for Q12 CR-RC; integer codes for trapezoid). Both share the time-from-trigger axis (µs), zoom, pan and reset; Y remains independent. Event markers identify acquired index, pixel and deposit, with an event list in the right panel also giving the original absolute acquisition time. Four outputs shows the physical readout's lanes, or under DirectCrystal the empty state ("Available with the experimental physical readout", "Choose readout…"). The mode note, the window summary and the pulse readout moved to the right panel (2026-10-13).

The right ScrollViewer holds headed sections: Scope window (Latest / Next in the header, Follow latest, event index, window µs, the mode note with its help mark), Selected event (window summary, pulse readout), Rate study (toggle, simulated energy-channel kcps, ideal stimulus), Acquired chain (label / value rows) and Events in window (a table # · Pixel · Energy · Scope). Default is real 10 µs with 20% pretrigger; wider windows may be clipped to the sample work budget. Rate study and ideal status are explicit in the mode note. No controls imply that the position channels or their pile-up behavior have been simulated.

New views use the existing surface, type, field, gap and button tokens. Automation IDs are `Chain.Section`, `Chain.Scintillator`, `Chain.Sensor`, `Chain.Preamp` (in the Options dialog's Detector tab since 2026-10-13), `Waveform.Adc`, `Waveform.Shaped`, `Waveform.FollowLatest`, `Waveform.Latest`, `Waveform.Next`, `Waveform.Index`, `Waveform.Window`, `Waveform.RateStudy`, `Waveform.Rate`, `Waveform.Ideal`, `Waveform.Events`, `Waveform.ChainReadout` and `Waveform.Note`. Accessible names include visible labels/units. No new desktop accessibility/layout pass is claimed; the 1280×800 and 1440×900 offscreen paths are prepared but were not executed after sandbox denial.

## Detector and focus sweep

Detector is Ctrl+4. Its centre has a heading that follows the view and the readout mode, the view tabs Face / SiPM /
Anger / Calibration / Diagnostics, and the view: Face draws a square vector face with exact active and reflector-gap
rectangles, opaque viridis colours encoding seeded relative gain, with only its numeric colour bar beneath; the
readout views show the physical readout or, under DirectCrystal, the empty state. Since 2026-10-13 the captions and
legend facts are in the right panel: Detector geometry (an identity line — "Settings for the next acquisition" without
data, "Acquired settings · locked until Reset" with data, neutral caption text, not the warning colour — then material
and geometry rows and the active area), Gain pattern (gain σ / seed, relative-gain range, face size) and Acquired
(counts, live-time rate); on the physical readout Readout network, Measured conversions, Calibration (this session) and
Confirmed offers. The panel is top-aligned to its content, with the standard panel padding and scrolling when height is
constrained; the shell's workspace host remains stretched.
The gap editor (Options dialog, Detector tab) uses µm; OK refuses invalid text, and physical pitch edits revalidate the applied gap.

Imaging's scrollable options contain K (1–4), Sweep focus/Cancel sweep, sharpest-plane and half-max
descriptions, optional external surface range and Use as focus. A result opens FocusSweepView beside the
measurements under the image pair (each half the centre width, the height the images leave), with X in
detector-referenced mm, prominence on Y, half-max bands and sharpest/external markers in the label strip above
the plot. No result: the measurements take the full width. The bias/bound note is visible in
the options and distinguishes descriptive width from uncertainty. Censored intervals display bounds,
boundary maxima and disjoint modes have text labels, and empty complete-track results say unresolved.
Gap.Field/Section/Tight and existing surface, type and control styles supply all view metrics.
DetectorFaceView exposes an Image automation peer; all new editors/actions have meaningful unique IDs.
The new detached fixtures cover before/acquired Detector and resolved/censored Imaging in both themes and
sizes. Their curves are drawing fixtures, not MC depth evidence; final PNG generation is not verified here.
