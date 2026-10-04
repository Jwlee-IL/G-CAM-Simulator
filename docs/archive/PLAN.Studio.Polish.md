# PLAN.Studio.Polish — UI polish of GCAM Studio, in batches (TODO-16)

Scope: the polish work the author made the focus on 2026-10-01 — what each batch fixes, why, and how it is judged
(headless render snapshots, not desktop UI tests, which run last). Issues come from the desktop survey
(`docs/assets/studio-polish-survey/README.md`, P-xx) and the plot snapshots (`docs/assets/studio-plot/`). Order and
the testing rule: [PLAN.Studio.Migration](PLAN.Studio.Migration.md).

Status: batch 1 done 2026-10-01 (renders in `docs/assets/studio-render/`); batch 2 done the same day (detector section, label plates, no
Window-ancestor bindings; FCFOV now in the renders); batch 3 (workspace layout) **done** 2026-10-02 by the substitute implementer (Codex out of credits): all rows except L-14, L-15
and the status-bar part of L-10, held for TODO-24 (Start / Stop / Reset removes the stale state); plus the Ba K X-ray
origin in the engine's line data and the desktop-test readout parser. Planner re-verified: build 0 / 0, tests
262 / 162 / 67 (+7) / 10 (+9), renders reviewed. Batch 4 (desktop-survey items P-12 … P-16) **done** 2026-10-02 (Codex).

## Batch 1 — design-system level (new workspaces inherit these)

Each cause below was checked in the code before writing this plan.

| # | Issue | Cause in the code | Fix |
|---|---|---|---|
| B1-1 (P-01) | Flood chip says "weighted counts" | literal text in `MainWindow.xaml` (flood panel header) | "counts" — every list-mode count is one event |
| B1-2 (P-02) | Heatmap readout ends in a bare number ("· 7") | `HeatmapView`: `"x {0:F1} mm, y {1:F1} mm · {2:G4}"` | a `ValueUnit` property on `HeatmapView` (flood: "counts" → "· 7 counts"; reconstruction: its decoded-intensity label, e.g. "· 3,077 (decoded)") |
| B1-3 (P-03) | Colour-bar ticks not round (6.2, 12.5; "−1") | `ColorBar` places `TickFormatter.Labels(min, max, n)` evenly from min to max | ticks at 1-2-5 values inside [min, max] (reuse `NiceTicks.Linear`), each placed at its own position on the bar; keep the shared ×10ⁿ rule |
| B1-4 (P-05) | Live time / Speed boxes fill the 48 px bar and are wider than needed; units sit in the labels | top-bar `TextBox`es have no vertical alignment, so the horizontal `StackPanel` stretches them; `Size.NumberInput` = 120 | vertically centred at `Size.Control`; a narrower named width for short numbers; units through the existing `Tag` suffix ("s", "×") as the left panel does; labels "Live time", "Speed" |
| B1-5 (P-08) | "Pile-up" title above a "Pile-up" checkbox | `SpectrumPanel.xaml` | checkbox text says what it does ("Merge pulses within the resolving time"), title stays |
| B1-6 (P-09) | Preset named with a first-person label | `FrontEndParts.cs` preset name (moved from `Gcam.Wpf`) | "CSP + CR-RC (200 ns)"; the `Gcam.Wpf` comment that quotes it follows |
| B1-7 | Log-axis top tick reads "10 ×10³" | `NiceTicks.Logarithmic` labels decades through the colour-bar `TickFormatter` (shared exponent) | decade labels as plain grouped numbers up to 10⁵ ("10,000"), superscript powers above ("10⁶") |
| B1-8 | Y-axis title "counts" crowds the top tick label | `PlotView` draws `YLabel` at the top-left corner inside the plot margin | reserve a title row above the plot area (measured text height + gap) |
| B1-9 | Window bands blend with the data (light theme especially) | `Color.Plot.Band` is Series1 green at low alpha (`#266ACD91`) | a neutral band tint distinct from every series colour, with a band edge line; both themes; contrast checked (band vs background vs series fill) |

## Judging: whole-window render snapshots

Extend `tests/Gcam.Studio.RenderTests` (opt-in `GCAM_RENDER_SNAPSHOTS=1`, no window shown, no mouse) to render the
**main window's content** offscreen at 1280 × 800 and 1440 × 900, dark and light, for the Imaging and Spectrum
workspaces, from a deterministic acquisition snapshot supplied by a fake acquisition service (no MC in the test).
Output: `docs/assets/studio-render/{imaging,spectrum}-{dark,light}-{1280x800,1440x900}.png`, plus the existing plot
snapshots re-rendered. Before / after for this batch = the survey captures vs these renders.

## Batch 2 — seen in the batch-1 renders

| # | Issue | Cause (checked) |
|---|---|---|
| B2-1 | "Background B…" label truncated; the editable Gain σ / Gain seed / Background fields sit under the "Geometry — read-only" header | phase A added inputs to the read-only geometry block; `Size.FieldLabel` (84) is too narrow for "Background BSR" |
| B2-2 | A narrow band's label ("32.1 + 36.4 keV") is crossed by the band's own edge lines | edges are drawn over the label row; the label is wider than the band |
| B2-3 | FCFOV value blank in the renders (fine in the app) | `MainWindow.xaml` binds through `RelativeSource AncestorType=Window`, which the offscreen render (content detached from the window) cannot resolve — such bindings also tie views to the window type |

Fixes:
- **B2-1** Split the left panel's lower part into **Geometry** (read-only facts: mask, pitch, distances, detector,
  focal plane, FCFOV) and **Detector** (read-only facts: entrance, backing, reflector gap; editable run inputs:
  gain σ with a "%" suffix, gain seed, background with a "× signal" (BSR) suffix). Labels short enough for
  `Size.FieldLabel`; the "read-only" caption only on the section that is. AutomationIds of the fields unchanged.
- **B2-2** Band labels are drawn after the band edges on a plate of the plot background colour (padding from the
  metrics), so no edge or grid line crosses the text; a label wider than its band stays centred and clamped as now.
- **B2-3** No `RelativeSource AncestorType=Window` bindings in views (three today: the workspace command, the
  selected-source `IsEnabled`, FCFOV). Bind to the view model through the data context that is already there (an
  element name on the root content, or the workspace / panel's own context), so a view works the same inside the
  window and in the offscreen render; the FCFOV value then shows in the renders.

## Batch 3 — workspace layout (reference plan, 2026-10-02)

All four workspaces exist (TODO-11 done). Issues below are seen in the renders of `7d0fd73`
(`docs/assets/studio-render/`); causes are **not yet checked in the code** — each row is *verify* for the implementer's
review, which may also add issues it finds in the renders.

| # | Issue (render) | Proposed direction |
|---|---|---|
| L-1 | Left panel scrolls at 1280 × 800 (every workspace): sources, detection chain with two long "Next / Acquired" lines, selected source, physical optics, detector | collapsible Detection chain like Physical optics, with a one-line summary; "Next acquisition" shown only when it differs from "Acquired" |
| L-2 | Imaging right panel: Focus sweep and External range fall below the fold at 1280 × 800; focal-plane facts paragraph is long | group into collapsible sections (Focus / Channel / Focus sweep / External range) or move the sweep and range into one section; keep AutomationIds |
| L-3 | Reconstruction chip reads "peak (0.0, 0.0) mm" in the two-source scenes while the peaks are found at (15, 8) and (−15, −8) | *verify* where the chip's estimate comes from (All channel's single estimate vs found peaks); show what is true for the selected channel (e.g. "2 found", or the strongest found peak) |
| L-4 | Inconsistent readout digits: "18.5323 (decoded)", "R = 0.2750", colour-bar 1000/2000 vs readout | one formatting rule per quantity (significant digits from the design system), applied to heatmap readouts, chips and ratios |
| L-5 | "Outdated" chip touches the panel title ("Waveform · energy sum[Outdated]") | header layout: title and chips as separate columns with the standard gap, as in Imaging |
| L-6 | Collapsed optics summary cut ("30×30 @ 0.6…") | wrap to two lines or shorten the summary format |
| L-7 | Empty height under the flood / reconstruction images (P-04), large at 1440 × 900 | let the image panels fill the row (square images sized to the available height), or give the space to the focus-curve panel |
| L-8 | Spectrum emission-window table: values not aligned to headers; the Ba K row reads "Cs-137 · 32.1 + 36.4" (P-10) | tabular alignment (right-aligned numbers under right-aligned headers); row label "Cs-137 Ba K X-rays" for the merged band |
| L-9 | Waveform event-marker labels (plated) hide the tops of tall pulses | label rows above the plot area (as band labels), markers as lines only inside the plot |
| L-10 | Status bar "79 cps" vs Detector panel "50.6 cps" in the same scene | label each: status bar "expected rate" (MC) vs panel "observed (counts / live time)"; or show one |

**Judging:** the same offscreen renders (both themes, both sizes, all scenes) before / after; no desktop tests.

### Batch 3 — decisions after review (2026-10-02)

Review: [PLAN.Studio.Polish.Review](PLAN.Studio.Polish.Review.md) (substitute implementer — Codex out of credits):
2 rows right, 8 with corrected causes, 8 new (L-11 … L-18). Adopted as written there, in its order (renders after
each group): **1** L-11, L-16, L-17, L-18 → **2** L-5 + L-14 + L-15, L-6, L-3, L-10 → **3** L-1 → **4** L-2 + L-13, L-7
→ **5** L-8, L-4 → **6** L-12, L-9. The planner's answers to the review's questions:

| Question | Decision | Reason |
|---|---|---|
| L-1: "Chain.Pending" conditional | **yes** — shown only when the pending chain differs from the acquired one; update the two tests and the SRS row | it repeats the three combo boxes above it |
| L-2: SR-OPT-05 precision note | **yes** — one-line caption with the full text in a tooltip; SRS row reworded, the evidence numbers stay in the VV document | the note is evidence, not an instruction; the fold matters more |
| L-7: measurements under the images | **yes** — Tools and the measurements table move under the images (fixes L-2's fold too); AutomationIds unchanged | the images are width-limited, so only content can use the empty height |
| L-8: Ba K label source | **engine data** — an emission-line kind (gamma / X-ray) on the isotope lines, default gamma; Studio names a merged X-ray band "Ba K X-rays" | the line's nature is physics data, one source of truth; Studio must not keep its own isotope table |
| L-4: acquisition time digits | **keep ns resolution** (9 decimals) in the event list — it is the event's identity at the ADC's 8 ns sampling; group the digits for readability | — |
| L-10 | status bar shows counts / live time (observed), like the Detector panel; fix the Waveform fixture's +1 s | one rate everywhere, the measured one |
| L-17 | renders deterministic (fixture-supplied worker time) | renders are review artefacts; noise in diffs hides real changes |

The author may override any row; desktop checks stay in TODO-22.

## Batch 4 — desktop survey items (reference plan, 2026-10-02)

From the TODO-22 desktop survey (`docs/assets/studio-polish-survey/README.md`). Causes unchecked — *verify* rows.

| # | Issue | Proposed direction |
|---|---|---|
| P-12 | Spectrum emission table: a 4-digit line energy touches its window column ("1173.2" + "1100.4–1246.0") | column widths from the widest formatted value (or a minimum gap token), both sizes |
| P-13 | Imaging, 1280 × 800: found-peak labels of nearby sources crowd / nearly touch | reuse the label layout (collision avoidance) for overlay labels, or shorten "Found Cs-137" when crowded |
| P-14 | Locked (disabled) inputs look too faint, especially in light theme | measure the disabled text contrast in both themes; raise it to a stated target (design-system token), keeping "disabled" distinguishable |
| P-15 | Shaped waveform: marker lines without labels | **intended** (batch-3 L-9 decision: labels only on the upper plot) — *verify*; if kept, say so in the survey README instead of changing it |
| P-16 | Detector: empty space under the geometry panel's text | *verify* whether useful content belongs there (e.g. the per-crystal gain statistics) or the panel should shrink |

### Batch 4 — decisions after review (2026-10-02)

**Done** 2026-10-02 (Codex): disabled-text contrast now 5.16 / 5.65 / 4.52 : 1 (dark: surface / canvas / raised) and
5.15 / 4.72 / 4.63 : 1 (light); planner re-verified build 0 / 0, tests 262 / 178 / 72 (+7) / 13 (+14), renders reviewed.

Review: [PLAN.Studio.Polish.Batch4.Review](PLAN.Studio.Polish.Batch4.Review.md) — adopted as written: P-12 content-sized
numeric columns with `Gap.Inline`; P-13 overlay chips measured and laid out as a group (minimum gap, bounds clamp),
markers unmoved; P-14 the shared disabled-text token raised to **≥ 4.5 : 1** on the disabled surface in both themes
(measured 3.67 dark / 2.51 light), inputs stay visibly disabled; P-15 dropped as a defect (upper-only labels are the
batch-3 decision; the survey README says so); P-16 the Detector panel card top-aligned to its content.

## Later batches (after the workspaces exist)

- The TODO-22 survey items P-12 … P-16 are batch 4 above.

- Seen in the TODO-10 renders: ~~the chain combo boxes show the preset record text (`ScintPreset { Name = … }`)
  instead of the part name~~ (fixed in TODO-11: the ComboBox template ignored `DisplayMemberPath`); ~~rate-study event
  labels pile on top of each other~~ (fixed in TODO-11: plot marker labels use the band-label layout — re-check the
  Waveform renders: no longer on top of each other, but the plated labels now hide the tops of tall pulses — move them above the trace or thin them);
  the "Outdated" chip touches the panel title; the collapsed optics summary is cut ("30×30 @ 0.6…").

- Workspace layout: the left panel scrolls at 1280 × 800 since the Detector section was added (compact or
  collapsible sections), empty height under the images (P-04), emission-window table alignment and a Ba K row label
  (P-10), and the layouts of the Waveform / Detector workspaces when they land.
- Then the desktop UI tests, the desktop survey and the README screenshot (last).
