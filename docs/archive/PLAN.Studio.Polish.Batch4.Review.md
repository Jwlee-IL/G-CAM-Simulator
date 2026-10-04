# PLAN.Studio.Polish.Batch4.Review — checked causes and proposed UI polish

Scope: review only of TODO-16 batch 4, P-12 through P-16, against the main working tree on 2026-10-02; code causes, contrast measurements, survey captures and fresh offscreen renders. Implementation and desktop validation are outside this turn.

Status: review complete; proposals await the planner's decisions. Only this review document was changed in the repository by this review.

## Disposition

| Row | Verdict | Checked cause (file and member/element) | Proposed fix |
|---|---|---|---|
| P-12 | Confirm; correct the sizing premise | [SpectrumView.xaml](../../src/Gcam.Studio/Views/SpectrumView.xaml), emission header Grid and ListBox.ItemTemplate Grid: Line, Window and Counts each use the same fixed 96-DIP `Size.Column.Value`, without cell margins. [Metrics.xaml](../../src/Gcam.Studio/Themes/Metrics.xaml) sizes that token for the shorter Cs window. | Content-sized shared numeric columns and an explicit `Gap.Inline` between cells, in header and rows. Keep right alignment, identical outer padding and the flexible emission column. |
| P-13 | Confirm; also handle edge clipping | [MeasurementAdorner.cs](../../src/Gcam.Studio/Controls/MeasurementAdorner.cs), `OnRender`, `DrawFoundPeak`, `DrawMarker`, `Chip`: each label is drawn immediately at a fixed offset; no occupied-rectangle collection, collision layout or bounds clamp exists. | Measure chips first and place them as a group, preserving marker positions and full truth/found labels. Use a deterministic layout of rectangles with a named minimum gap and bounds handling. |
| P-14 | Confirm measured low contrast | [Controls.xaml](../../src/Gcam.Studio/Themes/Controls.xaml), disabled TextBox and ComboBox triggers and `ComboBoxToggle`: switch to `Brush.Text.Disabled` on `Brush.Bg.Surface`. Both [token dictionaries](../../src/Gcam.Studio/Themes/Tokens.Dark.xaml) supply deliberately muted values. | Raise the shared disabled-text token to the measured target below in both themes; retain the disabled surface, subdued border, cursor and actual input locks. |
| P-15 | Drop as a defect; keep intended behavior | [WaveformView.xaml](../../src/Gcam.Studio/Views/WaveformView.xaml), `Waveform.Shaped`: explicitly sets `ShowMarkerLabels="False"`, while both plots bind the same `Markers` and `ViewRange`. [PlotView.cs](../../src/Gcam.Studio/Controls/PlotView.cs), `ShowMarkerLabels` and marker-label preparation: the flag suppresses labels, not marker lines. | No visual change. In the implementation turn, explain the upper-only label policy in the survey README. |
| P-16 | Confirm whitespace; reject missing-content premise | [MainWindow.xaml](../../src/Gcam.Studio/Views/MainWindow.xaml), right workspace ContentControl: stretches vertically across the content row. [DetectorPanel.xaml](../../src/Gcam.Studio/Views/DetectorPanel.xaml), outer surface Border: inherits stretch around a short ScrollViewer/StackPanel. | Top-align the Detector panel's surface Border so the card takes its natural content height, retaining width and scrolling when constrained. Do not invent statistics to occupy the space. |

## P-12 — a wider window cell needs a real gap

The defect is visible in all four surveyed Spectrum frames, including 1440×900. Increasing window width alone does not introduce a guaranteed gap between right-aligned values: the next cell's string consumes its leading spare space. `Pad.ListItem` pads the whole row, not each column. The larger window gives the emission-name column more space while these numeric columns remain 96 DIP.

A scratch offscreen WPF `FormattedText` measurement at 96 DPI, using the shipped `Font.Mono` family stack, normal weight and `Text.Mono` size 12, gives:

| String | Width (DIP) |
|---|---:|
| `1173.2` | 42.18 |
| `1100.4–1246.0` | 91.39 |
| `1251.2–1413.8` | 91.39 |
| `32.1 + 36.4` | 77.33 |

The long window leaves only 4.61 DIP before it in a 96-DIP cell, with no explicit inter-column margin. These are ideal FormattedText measurements, not a pixel-gap assertion about Display-mode TextBlocks; the actual render tests retain Display formatting, layout rounding and snapping.

Use a local `Grid.IsSharedSizeScope` enclosing the header and list, with `Auto` numeric columns sharing named `SharedSizeGroup`s. Give each preceding cell an 8-DIP right margin through the existing `Gap.Inline`, consistently in header and rows. The emission column remains `*`; do not globally enlarge `Size.Column.Value`, which other tables also use. Let WPF measure both headers and formatted data, including grouped counts, rather than assume one maximum energy width. Verify shared sizing across the ListBox template boundary; if it does not participate, report that and use one measured column-width source for both grids.

The current `MainWindowRenderTests.FixtureSpectrum.ProcessAsync` returns only Ba K and Cs-137 bands, even for a mixed scene. A successful current Spectrum render therefore does **not** reproduce P-12. Add a mixed Spectrum drawing fixture with both Co-60 rows, four-digit bounds and grouped counts. At both sizes and in both themes, require complete numeric strings, at least the 8-DIP inter-cell gap, and aligned header/row column edges. Preserve row selection and window zoom.

## P-13 — overlay geometry, not reconstruction

The surveyed scene has almost equal found Y coordinates, so the preferred lower-right chips occupy one horizontal lane. `Chip` adds 10 DIP to text width and 4 DIP to text height; its box is neither checked against earlier chips nor kept inside the heatmap. Scratch measurements at the control's current 11-DIP size give `Found Cs-137` a 77.36-DIP text width and `Found Co-60` 70.91 DIP, before padding.

The fresh mixed-All 1280×800 renders show a related defect: the Cs-137 chip reaches beyond the image's right edge. Their peaks are at (15.5, 8.3) and (−14.5, −7.7) mm, so their labels occupy different vertical lanes; they do not reproduce the survey's close, nearly equal-Y case.

Collect truth and found chip requests in `OnRender`, including their measured padded bounds. Lay them out together with a proposed 4-DIP minimum gap, preferring the existing positions, then alternate sides or vertically separated positions when crowded. Keep chips within the visible image/control bounds; use a neutral leader when displacement would make the marker association ambiguous. Draw marker geometry at its unchanged `MmToScreen` position. Preserve `Found <isotope>`, truth-ring/found-diamond distinction, selection inversion, read-only peaks and the accessible coordinate list.

`PlotBandLayout.Arrange` is a useful precedent for measured widths, stable ordering and gaps, but it allocates horizontal rows in a label strip. It cannot directly resolve arbitrary two-dimensional truth/found chips. Put any new layout calculation in a small UI-free Core helper; keep text measurement and drawing in the adorner. Do not change PlotView's layout for this row or shorten away the truth/found distinction.

Add an equal-Y two-source drawing fixture matching the survey's crowding and an edge case, in both sizes/themes. Check padded chip bounds and the gap, unchanged marker centres, resize/zoom recalculation, and no overlap introduced with existing measurement labels. For an extreme view too small for all labels, explicitly define a bounded fallback rather than claiming unlimited packing is possible.

## P-14 — measured token contrast and target

The source editor, chain selectors and acquisition seed are disabled through `IsEditable` / `CanEditInputs` bindings in MainWindow and ChainPanel. The visual faintness is not an accidental extra opacity on these TextBox/ComboBox contents: their disabled templates use opaque `Text.Disabled` and `Bg.Surface`. Unit suffixes have their own `Text.MonoCaption` role and remain `Text.Secondary`.

Calculated from the current XAML RGB tokens with standard sRGB relative luminance: each normalized component becomes `c / 12.92` for `c <= 0.04045`, otherwise `((c + 0.055) / 1.055)^2.4`; luminance is `0.2126 R + 0.7152 G + 0.0722 B`, and contrast is `(Llighter + 0.05) / (Ldarker + 0.05)`. These measurements describe full foreground/background colors, not antialiased edge pixels.

| Theme / disabled foreground | Surface (actual locked-input fill) | Canvas | Raised |
|---|---:|---:|---:|
| Current dark `#6B7883` | 3.670:1 (`#191F25`) | 4.019:1 (`#121619`) | 3.210:1 (`#222A32`) |
| Current light `#9AA5AE` | 2.510:1 (`#FFFFFF`) | 2.296:1 (`#F3F5F7`) | 2.252:1 (`#F0F3F5`) |
| Proposed dark `#85919C` | 5.164:1 | 5.654:1 | 4.517:1 |
| Proposed light `#626F7A` | 5.154:1 | 4.716:1 | 4.625:1 |

Proposed product target: **at least 4.5:1 for retained-value disabled text on Surface, Canvas and Raised**. The actual locked-input fill exceeds 5.15:1 in both themes with these candidates. This is a readability target for acquired values, not a claim that disabled controls currently violate an accessibility requirement. The candidate token stays dimmer than Secondary on Surface (7.62:1 dark / 6.16:1 light) and much dimmer than Primary.

Update `Color.Text.Disabled` in both dictionaries, leaving the matching brush indirection intact. Keep lock semantics, subdued disabled borders, the disabled surface and existing acquired-settings caption. Do not put another opacity multiplier on the value text. Audit other token users visually because this shared change also affects arrows and disabled action text. Primary-button and CheckBox templates separately apply opacity; this calculation does not certify their composited text at 4.5:1. Changing those unrelated action states is not needed to fix the surveyed retained inputs.

Implementation should update DESIGN.Color's token/contrast table and verify resolved foreground/background resources in acquired and empty fixtures, along with the token arithmetic. Inspect both themes at both sizes for a visible enabled/locked distinction; do not weaken the acquisition locks to make fields easier to read.

## P-15 — L-9 is implemented as decided

The batch-3 review's L-9 explicitly proposed turning marker labels off on the lower shaped plot; the planner adopted that review. Current code and fresh real-arrival/rate-study renders agree: labels sit above the ADC trace, marker lines remain on both traces, and event details remain in the right list. Keep this behavior and annotate P-15 as intended in the survey README in the implementation turn. No duplicate labels or extra label strip should be added to the shaped plot.

## P-16 — shrink the geometry card

`DetectorWorkspaceViewModel.Readout` already provides material, crystal dimensions, pitch, gap, active width, configured gain sigma/seed and active-area fraction. `GainLegend`, `MinimumGain` and `MaximumGain` already expose realized per-crystal extrema; `DetectorView` displays the extrema with its face legend. `Counts` supplies the observed rate beneath the face. There is no hidden statistics section waiting for binding in the geometry panel.

Keep the existing facts and explanatory captions, and set the local surface Border to `VerticalAlignment="Top"`. Keep the shell's workspace host stretched so this decision does not alter Imaging or Waveform. Retain the ScrollViewer and verify that short available height still permits reaching all text. Judge before/acquired fixtures in both themes and sizes: the geometry card should end after its final caption plus standard panel padding, while the face and gain legend keep their current available space. Adding gain histograms, calibration metrics or a new transport model would require a separate feature decision.

## Execution and evidence limits

- Read the planning and Studio instructions, all six DESIGN documents, batch-4 rows, batch-3 decisions and L-9 review, survey README, production controls/views/tokens and render harness.
- Opened all 16 existing survey captures. Ran the unmodified render suite in a scratch copy of the current source/test tree so its PNG writer could not overwrite repository evidence. `GCAM_RENDER_SNAPSHOTS=1` was set only in that command's process; no desktop opt-in was enabled.
- Command: `dotnet test tests/Gcam.Studio.RenderTests -c Release --logger 'console;verbosity=normal'`. Result: **1 passed, 0 failed, 0 skipped** (one test orchestrates the scenes); build/restore completed without reported compiler warnings/errors. It generated **68 whole-content PNGs and 4 standalone plot PNGs**, both themes and both sizes for the workspace scenes.
- Opened the fresh mixed-All Imaging, Spectrum, real-arrival Waveform and acquired Detector renders in both themes and sizes, plus the dark 1280×800 rate-study Waveform. The temporary images retain the current fixtures' differing scene/count content; they are drawing evidence, not new physics evidence or exact survey reproductions.
- Temporary reproduction tree and artifacts remain under `%TEMP%\gcam-batch4-review-c826ca1c9cb04c4c873ac91b780bbbd5`; PNGs are in its `docs/assets/studio-render` and `docs/assets/studio-plot`. A separate scratch `diagnostics` console measured FormattedText without creating a window. No cleanup/deletion command was run.
- No Studio window was shown, no HWND/input automation was used, and no UI tests ran. Desktop verification was deliberately not run under this turn's restrictions; there was no blocked required command. Full engine/service tests were not run for this documentation-only review.
- Read-only status before writing the review showed an existing modification to `docs/archive/PLAN.Studio.Polish.md`; it was left untouched. Separate implementer worktrees were not accessed. No approval-requiring command was needed.

For implementation, first add the missing crowded/mixed drawing fixtures, then address the table, overlay layout, contrast and Detector card in separate reviewable steps. Run the complete offscreen suite after the shared visual changes, keeping existing physics tolerances unchanged. Update the relevant DESIGN pages and the survey's current observations in that later turn; keep Findings, Todo and other PLAN documents for the planner.
