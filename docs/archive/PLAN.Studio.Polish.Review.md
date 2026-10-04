# PLAN.Studio.Polish.Review — batch 3 workspace layout

Scope: the substitute implementer's review of [PLAN.Studio.Polish](PLAN.Studio.Polish.md) "Batch 3 — workspace layout"
(rows L-1 … L-10), 2026-10-02: each row's cause checked in the code, a verdict, a proposed fix and its size, plus the
issues found in a fresh render of the working tree (`GCAM_RENDER_SNAPSHOTS=1`, 1 test passed, all PNGs in
`docs/assets/studio-render/` regenerated; only the `waveform-*` PNGs changed bytes — see L-17). Review only: no source,
test or other doc was changed.

Verdicts: **confirmed** (row and cause right), **corrected** (issue real, cause or fix different), **new** (L-11 …).
Sizes: S = one or two files, low risk; M = several files or a shared control / tests to update; L = layout restructure
or a shared control used by every workspace.

## Summary

| # | Verdict | One line | Size |
|---|---|---|---|
| L-1 | corrected | Collapsing the chain alone does not fix the scroll; empty error lines and the expanded-by-default Optics / Detector sections do | S–M |
| L-2 | confirmed, widened | Measurements (Tools + table) are never visible in any Imaging render; static paragraphs dominate | M |
| L-3 | corrected | (0.0, 0.0) is a **render-fixture artefact**; in the app the chip shows the All-channel argmax — one of two sources, unlabelled | S |
| L-4 | corrected | Root cause for ticks: decimals derived from the tick **range**, not the **step** (`TickFormatter`) | M |
| L-5 | confirmed | `PanelHeader` is `LastChildFill=False`; Spectrum / Waveform chips have no `DockPanel.Dock="Right"` | S |
| L-6 | corrected | Fits in the app's Display text mode at defaults; cut in Ideal mode (two render fixtures) and with longer values | S |
| L-7 | corrected | Images are **width**-limited at both sizes; "size to the available height" cannot fill the row | M–L (decision) |
| L-8 | corrected | Header grid sits outside the ListBox (no `Pad.ListItem`), star columns; Ba K row shows "Cs-137" with nothing marking it as X-rays | S–M |
| L-9 | corrected | Band labels are **also** drawn inside the plot; marker labels stack 4 rows deep in rate study | M–L |
| L-10 | corrected | Status = MC expected rate, panel = counts / live time; the 79 vs 50.6 gap is mostly the fixture's `+1 s` live time | S |
| L-11 | new | Empty message `TextBlock`s reserve a line each (left and right panels) | S |
| L-12 | new | Colour bar wider than the image (pixel snapping shrinks the image; bar keeps the panel width); Detector bar spans the whole panel | M |
| L-13 | new | Imaging right panel: inconsistent heading levels, "Focus sweep" has no section gap above it | S (with L-2) |
| L-14 | new | Stale indicator differs per workspace: "outdated" chip / "Outdated" chip / warning-coloured caption | S |
| L-15 | new | Detector identity "Pending detector · no acquisition" is drawn in the warning colour | S (with L-14) |
| L-16 | new | Render fixtures disagree with the app: Waveform / Detector renders skip `TextFormattingMode.Display`; the imaging fixture copies a (0, 0) estimate | S |
| L-17 | new | Waveform renders are not reproducible (measured worker ms printed) | S |
| L-18 | new | "1 events" (Waveform summary) | S |

## Rows L-1 … L-10

### L-1 Left panel scrolls at 1280 × 800 — corrected

Seen: the scroll bar is present in **every** render at 1280 × 800, including those with Physical optics and Detector
collapsed (`detector-before-*`: the Detector header is cut at the bottom).

Cause (checked):
- `MainViewModel.IsOpticsExpanded` / `IsDetectorExpanded` default to `true` (`MainViewModel.cs` 99–100). Expanded, the
  panel is roughly 500 px taller than the 676 px available (Physical optics ≈ 290 px, Detector ≈ 230 px).
- Three empty message lines take ≈ 16 px each even when there is no message: `Optics.Error`, `Acquisition.InputError`
  (`MainWindow.xaml` 248–251) and `GapError` (272). A WPF `TextBlock` with empty text still measures one line (see L-11).
  With both sections collapsed, these two empty lines between the summary and the divider are what pushes the
  Detector header below the fold.
- `ChainPanel.xaml` 4: `IsExpanded="True"` is hard-coded (not bound, unlike Optics / Detector), and 21–24 always show
  both "Next acquisition: …" and "Acquired: …" — each wraps to two lines (≈ 70 px) and the "Next" line repeats the three
  combo boxes directly above it.

Proposed fix (improves on the plan):
1. L-11 first (empty messages collapse) — removes ≈ 48 px with no behaviour change.
2. Physical optics and Detector **collapsed by default** (`false` in `MainViewModel`); `StartAsync` already expands
   them on a validation error (269–273). Their one-line summaries stay.
3. Detection chain: bind `IsExpanded` to a new `MainViewModel.IsChainExpanded` (default `true` — it is edited more
   often than optics) and show a one-line collapsed summary (scintillator · sensor · preamp short names), as Optics
   does. Drop the "Next acquisition" line (the combos are the next acquisition); show the acquired line only when it
   differs from the pending chain, worded "Results acquired with: …". Keep `Chain.Pending` / `Chain.Acquired`
   AutomationIds on whatever remains, or update `WaveformRenderTests` (85–90 assert both texts differ) and
   `WaveformWorkspaceTests` (67 `Contains("Next acquisition")`) — a planner decision, since DESIGN.Layout
   (Waveform section) documents "separate wrapped summaries".
With 1+2 the default state fits at 1280 × 800 (estimate from the renders: content ≈ 620 px of 676); 3 adds headroom
for two sources.

Size: S–M (`MainViewModel.cs`, `ChainPanel.xaml`, `MainWindow.xaml`, two tests, DESIGN.Layout).

### L-2 Imaging right panel overflows — confirmed, widened

Seen: at 1280 × 800 the panel shows focal plane → channel → part of Focus sweep; at 1440 × 900 it reaches "Use as
focus". **The Tools picker and the measurement table are below the fold in every Imaging render** — the primary
interactive feature of the workspace needs scrolling.

Cause (checked, `ImagingOptionsPanel.xaml`): one flat `StackPanel` of ~25 elements, including three static
paragraphs that never change: `SamplingEvidence` (4 lines, line 14; required by SR-OPT-05), the Compton-strip model
note (line 23, 2 lines) and `SweepNote` (line 55). Plus empty `FocusError`, `FocusNote`, `SweepError`,
`ExternalRangeError`, `Error` lines (L-11).

Proposed fix:
- Sections as `Expander.Section` (themed, already used on the left): **Focal plane** (expanded; collapsed summary
  "1000 mm · element 8.75 mm"), **Channel** (expanded), **Focus sweep** (K, Sweep / Cancel, results, external range,
  Use as focus — collapsed by default, auto-expanded when a sweep result arrives), then **Tools / Measurements**
  unchanged. Bind the expanded states on `ImagingWorkspaceViewModel` so they survive workspace switches.
- Static notes become captions of their own section, shortened, with the full sentence in a `ToolTip`: SR-OPT-05 asks
  for "a conditional sampling-precision note", not a four-line paragraph; keep a one-line caption ("Precision is
  position-dependent — see tooltip") so the requirement stays met visibly. Same for the strip note.
- All AutomationIds unchanged.
Combined with L-11 this brings Tools into view at 1280 × 800 with Focus sweep collapsed (estimate, to be checked in
the renders).

Size: M (`ImagingOptionsPanel.xaml`, `ImagingWorkspaceViewModel`(.Focus).cs, DESIGN.Layout; renders).

### L-3 Reconstruction chip "peak (0.0, 0.0) mm" in two-source scenes — corrected

Trace: `PeakText` (`ImagingWorkspaceViewModel.cs` 62) = `Result?.Estimate`; `Result` (59) = the selected channel's
image, else the shared snapshot's. In the app each channel image comes from `ImagingProjection.Project`
(`ImagingProjection.cs` 42–44), whose `Estimate` is the decoder's **global argmax** of that channel's
reconstruction (`CrossCorrelationDecoder.cs` 78–83). In the render fixture, `FixtureImaging` builds every channel as
`snapshot.Imaging with { Reconstruction = … }` (`MainWindowRenderTests.cs` 266), which **keeps the fixture snapshot's
`Estimate` at (0, 0, 1080)** (203) whatever the sources are.

So: (0.0, 0.0) is a fixture artefact. The real app shows a real position — but on the All channel of a two-source
scene it is the brighter of the two peaks, unlabelled, while the panel says "2 found". That is the actual defect.

Proposed fix:
- `PeakText`: when the selected channel has ≥ 2 found peaks → "{n} peaks found" (the list is in the right panel and
  on the image); otherwise keep "peak (x, y) mm" from `Estimate` (unchanged for one source, which the UI test
  `ScenarioTests.cs` 200 parses with `Verdict.ParsePeak`, and `MainViewModelTests.cs` 61 asserts).
- Fixture: `FixtureImaging` sets each channel's `Estimate` to the brightest of its sources (or null for All with two
  sources), so renders can no longer show a false reading.
- One ViewModel test: two found peaks → "2 peaks found".

Size: S (`ImagingWorkspaceViewModel.cs`, `MainWindowRenderTests.cs`, one test).

### L-4 Inconsistent readout digits — corrected (cause found)

Causes (checked):
- **Ticks** (colour bars and every PlotView axis): `NiceTicks.Linear` passes `first … last` to
  `TickFormatter.Labels` (`NiceTicks.cs` 18), which sets decimals from the **range** (`TickFormatter.cs` 20:
  `2 − floor(log10(range))`). A 0–20 count scale with step 10 gets one decimal ("0.0 10.0 20.0" under the flood map);
  shaped output "10.0 5.0 0.0"; prominence "6.00 4.00 2.00"; time "−2.00 0.00 2.00 µs". The step decides the
  decimals that are meaningful. Also no thousands grouping ("1000 2000") while readouts group ("3,077").
- **Heatmap readout**: `HeatmapView.cs` 433 `{2:#,0.####}` → up to four decimals ("18.5323 (decoded)").
- **Ratios**: `StripRatio.cs` 8 `R:F4` ("0.2750").
- **Others seen**: `WaveformEvent.cs` 8 acquisition time to 9 decimals ("0.853889538 s"); Detector readout
  "Gain σ 3.0%" (`DetectorWorkspaceViewModel.cs` 29, `P1`) next to "gain σ 3%" (20, `0.#`).

Proposed fix — one rule per kind, in Core:
- Ticks: decimals = `max(0, −floor(log10(step × scale)))`, clamped 0…3; grouped (`N{d}`); shared ×10ⁿ rule kept.
  Pass the step into `TickFormatter` (new overload) rather than re-deriving it.
- Continuous readouts (decoded intensity, ratios, prominence): 4 significant digits, grouped, trailing zeros
  dropped — "18.53", "3,077", "0.275". One helper (e.g. `TickFormatter.Significant(value, digits)`).
- Counts: integers `N0` (already). Percent: one rule (`0.#%`). Absolute acquisition time: µs resolution
  (`0.000000 s`) — unless the 9 digits are meant as an event identity (ask the planner).
- `TickFormatter` / `NiceTicks` unit tests (18 references in `tests/Gcam.Studio.Tests`) need their expected strings
  updated; plot snapshots change everywhere.

Size: M (Core formatting + 4 call sites + tests + DESIGN.Typography / DESIGN.Controls).

### L-5 "Outdated" chip touches the title — confirmed

Cause: `PanelHeader` (`Controls.xaml` 90–94) is a `DockPanel` with `LastChildFill=False`. Imaging docks its chips
`Right` (`MainWindow.xaml` 318, 343, 348); `SpectrumView.xaml` 8–13 and `WaveformView.xaml` 9–14 give neither child a
`DockPanel.Dock`, so both dock Left with no gap.

Fix: `DockPanel.Dock="Right"` on the chip (title `Left`), as Imaging — the chip then sits at the panel's right edge,
the same place in every workspace. Fold in L-14 (same text, tooltip and AutomationId `ResultStale` everywhere).

Size: S (two XAML files).

### L-6 Collapsed optics summary cut — corrected

Cause: `MainWindow.xaml` 246 — `TextTrimming="CharacterEllipsis"`, no wrap, mono caption, ≈ 276 px wide minus the
scroll bar. The default string ("rank 13 · 0.7 mm · D 80 mm · 30×30 @ 0.6 mm", `OpticsEditorViewModel.cs` 24) **fits**
in the Imaging renders, which use the app's `TextFormattingMode.Display`; it is cut in the Waveform / Detector renders,
which do not set it (L-16), and would be cut in the app with longer values ("0.75 mm · D 120.5 mm … @ 0.65 mm").

Fix: `TextWrapping="Wrap"` (and drop the trimming) on both collapsed summaries (optics 246, detector 291); keep the
tooltip. With L-1 the scroll bar usually disappears, which also gains its width back.

Size: S.

### L-7 Empty height under the images — corrected

Cause: `ImageStackPanel.MeasureOverride` (`ImageStackPanel.cs` 19–33) sizes the square to `min(width, height −
below)` and top-aligns. At 1280 × 800 each image column is ≈ 286 px wide and ≈ 640 px tall; at 1440 × 900 ≈ 364 ×
740. The images are **width**-limited at both target sizes, so "size the square to the available height" cannot use
the space; ≈ 300–370 px stays empty in each column.

Options:
- (a) Focus curve takes the leftover height (`Height="*"`-like, min `Size.FocusPlot`) instead of a fixed 240 px — small,
  but only helps after a sweep.
- (b) Move **Tools + Measurements** from the right panel into the centre column under the image pair (a full-width
  strip that takes the rest of the height, the focus curve beside or above it). Fills the empty space, and is the
  strongest fix for L-2 (right panel then holds only settings). Larger: a layout change to DESIGN.Layout's screen
  grid; AutomationIds unchanged, so UI tests keep working.
- (c) Leave as is (DESIGN.Layout and the `ImageStackPanel` remark chose top-alignment deliberately).

Recommendation: (a) now; (b) as a planner / author decision — it changes the documented screen grid.

Size: (a) S; (b) M–L.

### L-8 Emission-window table — corrected

Causes (checked, `SpectrumView.xaml` 22–47):
- The header `Grid` sits outside the `ListBox`; each row is inside a `ListBoxItem` with `Pad.ListItem` (8 px left and
  right, `Controls.xaml` 394). Both use five equal star columns over different widths, so the header is offset 8 px
  and the drift grows to the right. All cells are left-aligned, numbers included.
- The Ba K row: `SpectrumBand.Isotope` (`SpectrumBand.cs` 7) joins line isotopes, so both rows read "Cs-137"; the plan's
  quoted "Cs-137 · 32.1 + 36.4" is not what is drawn — the row is "Cs-137 | 32.1 + 36.4" and nothing says X-rays.
  The X-ray origin is not in any data the Studio sees: `Isotopes.All` lines are `(EnergyKeV, Intensity)` tuples
  (`Scene.cs` 5, 19); only a comment says they are Ba K X-rays.

Fix:
- Do as the measurement table already does (`MainWindow.xaml` 421–435): header wrapped in a `Border` with
  `Pad.ListItem`; fixed numeric columns from named `Size.Column.*` keys (Line, Window, Counts, Share), the label column
  `*`; numbers and their headers right-aligned.
- Label: add an optional origin to the line data where the knowledge lives — e.g. an `init` property on `IsotopeInfo`
  (`LineOrigins`: 32.1 → "Ba K X-ray", 36.4 → "Ba K X-ray"), carried by `SpectrumLine` (new optional `Origin`) so the
  row reads "Cs-137 Ba K X-rays" and the plot band label can stay "32.1 + 36.4 keV". Additive; no engine caller
  changes. Planner decision: touching `Gcam.Configuration` vs a Studio-side lookup table (duplicated knowledge).

Size: S (alignment) + S–M (label, if the data route is taken; one ViewModel test).

### L-9 Waveform marker labels hide pulse tops — corrected

Cause (`PlotView.cs` 266–300): band labels **and** marker labels are drawn inside the plot rectangle from `r.Top`,
over the data; marker rows start below the band rows. Auto-scale leaves only 8 % linear headroom
(`PlotAutoScale.cs`). In rate study, nine markers stack four label rows, ≈ 40 % of each 1280 × 800 waveform plot, on
both the ADC and the shaped plot. The plan's "as band labels" premise is wrong: band labels are inside the plot too
(the Spectrum's 32 keV peak sits just under its plate; the focus curve's "sharpest plane" / "External surface range"
plates cover the curve's top in `focus-near-resolved-*`).

Fix:
- A label strip **above** the plot rectangle: lay out band and marker labels first (their x positions depend only on
  the plot's left / width), then `MeasurePlotRect` moves `top` down by the rows used. Lines stay inside the plot.
- Marker labels short in the strip ("#64"); the full text ("#64 (0,11) 100.2 keV") is already in the event list —
  add it to the hover readout. With short labels nine markers fit in one or two rows.
- A `PlotView.ShowMarkerLabels` flag: off for the lower (shaped) waveform plot, which shares the time axis.
- Re-check B2-2 (band label plates) and the plot snapshots (`PlotViewRenderTests`).

Size: M–L (`PlotView.cs`, `WaveformWorkspaceViewModel` labels, `WaveformView.xaml`, plot render tests; all plots move).

### L-10 Status "79 cps" vs Detector panel "50.6 cps" — corrected

Cause: the status line prints `snapshot.RateCps` (`MainViewModel.cs` 297) = `ListModeSource.RateCps`
(`ListModeSource.cs` 29) = the MC's running **expected** rate (weighted efficiency × emission × (1 + BSR) + dark).
The Detector panel prints observed counts / live time (`DetectorWorkspaceViewModel.cs` 32–34). In the app these agree
within Poisson scatter. The render gap is the fixture: `WaveformAcquisition` sets `LiveTimeS = last arrival + 1 s`
(`WaveformRenderTests.cs` 136) and `IsCompleted = true` at 2.5 s of 60 s — the status line contradicts itself
(128 counts / 2.5 s ≈ 51, printed 79).

Fix:
- Status bar shows observed counts / live time (what an instrument displays, and consistent with the t and counts on
  the same line); one definition in both places. Optional: the MC expected rate as the status tooltip. SRS status
  table (`VV.Studio.SRS.md` 245–247) says "<rate> cps" — add "counts / live time" there.
- Fixture: live time = last arrival time.

Size: S (`MainViewModel.cs`, `WaveformRenderTests.cs`, SRS row).

## New rows

### L-11 Empty message lines reserve height — new

Cause: a `TextBlock` with empty / null text still measures one line. Unconditional message lines: left —
`Optics.Error`, `Acquisition.InputError`, `GapError`; Imaging right — `FocusError`, `FocusNote`, `SweepError`,
`ExternalRangeError`, `Error`; Spectrum / Waveform — `Error`. Visible as the ≈ 40 px gap between the focal-plane box
and the geometry facts in `imaging-dark-1280x800`.

Fix: in `Text.CaptionWarning` (`Typography.xaml` 58) add a trigger collapsing the block when `Text` is empty; for
`FocusNote` (a `Text.Caption`) use `NullToCollapsedConverter` on that element. One style change covers all error lines.
Size: S.

### L-12 Colour bar not at the image's width — new

Cause: `ImageStackPanel` arranges the colour bar at the square's side, but `HeatmapView` snaps cells to whole device
pixels (`HeatmapView.cs` ConfigureViewport → `HeatmapViewport` snap), so a 30-cell flood in a 356 px box draws 330 px
— the bar overhangs the image by up to cells − 1 px (≈ 34 px at 1440 × 900). The heatmap's `Gap.Field` margin also
makes its box 8 px shorter than wide. Detector: `DetectorView.xaml` docks the bar at the panel's full width under a
centred square face.

Fix: `HeatmapView.MeasureOverride` returns the snapped square (when an image is set); `ImageStackPanel` arranges the
legend at the first child's desired width and centres the group. Detector face + bar + legend inside an
`ImageStackPanel` too. Size: M (shared control; check zoom / pan hit area, measurement adorner positions).

### L-13 Imaging right-panel heading rhythm — new

`ImagingOptionsPanel.xaml`: "Decoder focal plane" and "Focus sweep" are `Text.SectionTitle`, "Imaging channel" and
"Tools" `Text.PanelTitle`; "Focus sweep" follows `WorkerCosts` with no `Gap.Section`, so it touches the worker line.
Fix within L-2: one level for the panel's sections, `Gap.Section` before each. Size: S.

### L-14 Stale indicator differs per workspace — new

Imaging: chip "outdated" with tooltip and AutomationId `ResultStale` (`MainWindow.xaml` 348–352). Spectrum / Waveform:
chip "Outdated", no tooltip, no AutomationId. Detector: a warning-coloured caption line ("Acquired detector · settings
outdated", `DetectorView.xaml` 9). Fix: one chip (same text, tooltip, AutomationId) in each panel header, docked right
(L-5); the Detector identity line keeps only the pending / acquired fact. Size: S.

### L-15 Pending detector state drawn as a warning — new

`DetectorView.xaml` 9 uses `Text.CaptionWarning` for every `Identity`, so "Pending detector · no acquisition" is
amber. Fix with L-14: neutral caption; the warning colour only for the stale chip. Size: S.

### L-16 Render fixtures differ from the app — new

`WaveformRenderTests.cs` 53–64 and `DetectorFocusRenderTests.cs` 72–79 do not set `TextFormattingMode.Display` /
`SnapsToDevicePixels` as `MainWindowRenderTests.cs` 68–71 does, so text is wider than in the app (L-6's cut summary).
`FixtureImaging` keeps a (0, 0) estimate (L-3); `WaveformAcquisition` adds 1 s of live time (L-10). Fix: one shared
"detach and prepare MainWindow content" helper in the render tests; the two fixture corrections. Size: S.

### L-17 Waveform renders not reproducible — new

Re-rendering changed only the `waveform-*` PNGs: the summary prints measured worker time ("worker 0.36 ms",
`WaveformWorkspaceViewModel.cs` 31). Every render run makes git noise. Fix: keep it in the app; the render fixture
supplies a fixed processing time (or the test masks it). Size: S.

### L-18 "1 events" — new

`WaveformWorkspaceViewModel.cs` 31: `{v.Events.Count} events`. Singular for 1. Size: S.

## Proposed order

1. **Foundations** — L-11, L-16, L-17, L-18 (renders become trustworthy before judging the rest).
2. **Small view fixes** — L-5 + L-14 + L-15, L-6, L-3, L-10.
3. **Left panel** — L-1 (after L-11; decide on the chain text and default-collapsed sections).
4. **Imaging right panel** — L-2 + L-13; then L-7 (a), and (b) only if the planner takes it.
5. **Tables and numbers** — L-8, L-4 (L-4 changes every plot snapshot; do it before L-9 to re-render once).
6. **Shared controls** — L-12, then L-9 (largest; touches every plot).
Render all workspaces (both themes, both sizes) after each group.

## Could not check

- The real app: no desktop by rule. L-3's in-app value (the All-channel argmax) is traced in the code, not observed;
  the in-app optics summary width (L-6) is inferred from the Display-mode renders.
- DPI 125 / 150 %: the renders are 96 DPI; L-12's overhang grows with cell snapping at other scales (not measured).
- Fit estimates for L-1 / L-2 are read off the renders (pixel positions), not measured layout; the implementation turn
  should assert `ScrollViewer.ScrollableHeight == 0` for the left panel at 1280 × 800 in the default state.
- Whether SR-OPT-05's precision note may move to a tooltip with a one-line caption, and whether `Chain.Pending` must
  stay as a separate line, are requirement / design-record questions for the planner.
