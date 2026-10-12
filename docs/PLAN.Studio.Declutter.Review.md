# PLAN.Studio.Declutter — implementer review (turn 1, read-only)

Scope: review of [PLAN.Studio.Declutter](PLAN.Studio.Declutter.md) (TODO-44 + TODO-45) at commit `ab350cf`, written by a
substitute implementer (Claude subagent) while Codex is unavailable. No product, test or plan file was changed. Every
statement below was read in the code (file, member) or measured; measurements say how. Items that need the desktop are
marked *desktop check* and were not run (no GUI, no UI tests in this turn).

## 0. Material findings first

| # | Finding | Effect on the plan |
|---|---|---|
| F1 | **`HelpMark` tooltip throws.** `Controls/HelpMark.cs` constructor: `_content.SetResourceReference(MaxWidthProperty, "Size.SidePanel")`, but `Size.SidePanel` is a `GridLength` (`Themes/Metrics.xaml` line 34). A probe that merges the four theme dictionaries into an `Application` and constructs a `HelpMark` gets `InvalidOperationException: '300' is not a valid value for property 'MaxWidth'` on the first read of the tooltip content's `MaxWidth`, and the same exception when the tooltip is opened (`ToolTip.IsOpen = true`). App has no `DispatcherUnhandledException` handler, so the first hover or keyboard focus of any help mark is expected to end the process. Not seen so far because UI tests read `HelpText` through UIA and the offscreen renders never open a tooltip. | **Blocking for D7**, which multiplies help marks. Stage 1 step 0: a `sys:Double` key (e.g. `Size.HelpText` = 300) and a headless render assertion that reads the tooltip content's `MaxWidth`. *Desktop check*: hover one mark once fixed. |
| F2 | **Speed is not locked by data.** `MainViewModel.CanEditSpeed => !IsRunning`; SR-RUN-24: "Speed only paces the acquisition and is editable in every state but Acquiring." D3's "dialog opens read-only while data exist — the SR-RUN-12 rule unchanged" would lock speed after Stop (a regression of SR-RUN-24, which the plan does not list). | Keep Speed in the top bar beside Live time (recommended), or give the dialog per-field enables (`CanEditSpeed` for speed). See §4. |
| F3 | **"Shown and disabled for a non-Custom preset" is wrong.** The optics fields are always shown and enabled while `OpticsEditorViewModel.IsEditable`; editing a field under a preset makes `Validate()` set `SelectedPreset = OpticsPreset.Match(Effective)` (Custom, or another preset that matches). `ReadoutScenarioTests.CustomModules_…` edits Rank / CellPitch / Distance under "High-res" and asserts the selector reads Custom. | D5 is a real behaviour change (fields hidden for a preset). It also needs an explicit Custom mode, or the fields vanish mid-typing when an edit happens to match a preset (§8 P10). |
| F4 | **Several D7 texts are required to be visible inline**: FocusNote (SR-OPT-04 "with a visible explanation"), SweepNote (SR-FOCUS-04 "Visible text describes …"), the state parts of the Waveform note (SR-WAVE-03 / -04 / -07 "labelled"), the Spectrum chain name and resolving time (§5 Outputs "Front-end chain"), the StripNote image-role labels (SR-IMG-07 "count and image-role labels") and the MLEM ReconstructionNote (SR-IMG-07 "A note states …"). | Split these (short inline label + help mark) or rewrite the rows with the SR-DET-10 convention ("displayed" includes a help mark). Table in §2. |
| F5 | **D8 removes more than the cost card.** The removed `StackPanel` also holds `Imaging.ChannelUnavailable` (why the selected physical channel has no image — a refusal, D1) and `Imaging.ReadoutCurrentCounts` (this acquisition's raw window counts — a key number). | Move both into the Imaging channel section (physical mode only). The flood-correction check box is an Imaging re-projection setting (SD-A4), better placed there than in the readout panel (§8 P4). |
| F6 | **D12 reverses a recorded decision.** SR-RUN-26 ("discards the acquisition without a confirmation") and `AGENTS.Rationale` (Reset row: "a fixed seed reproduces a discarded one, so a dialog would cost more than it protects") say the opposite, and DESIGN.Controls' Button row repeats it. | Add SR-RUN-26, the Rationale row and DESIGN.Controls to the rows to rewrite, with the new reason. |
| F7 | **Stop has no icon.** `MainWindow.xaml`: `<Button Content="Stop" …>`; only Start / Continue carries `Icon.Play`. The only icon geometry is `Icon.Play` (`Themes/Controls.xaml` line 111). | D11 "Start / Continue / Stop keep icon + text" needs an `Icon.Stop`; about 16 new icon geometries in total (§8). |

## 1. "What exists" and *verify* rows, checked in the code

| Plan statement | Verdict | Where / correction |
|---|---|---|
| Window 1440 × 900, minimum 1280 × 800, OS chrome | confirmed | `MainWindow.xaml` root attributes; no `WindowChrome` / `WindowStyle` |
| Top bar `Size.Bar` 48 | confirmed | `Metrics.xaml` line 32, `MainWindow.xaml` row 0 |
| Logo, title, workspace switch, ThemeToggle (text), Live time, Speed, Seed, Reset (text, no confirmation), Start / Continue / Stop (icon + text) | confirmed, **one correction** | ThemeToggle is `Button.Ghost` with `Content="{Binding ThemeToggleLabel}"`; Reset is the implicit style with tooltip "Discard the acquired data and unlock the physical inputs"; **Stop is text only** (F7). Live time and Seed carry long tooltips; Speed has none |
| OS title bar follows the theme through DWM, SR-THEME-02 | confirmed | `Services/ThemeService.ApplyTitleBar` (`DWMWA_USE_IMMERSIVE_DARK_MODE` = 20 + `SetWindowPos(SWP_FRAMECHANGED)`), called from `App.OnStartup` on `SourceInitialized` and for every window in `Apply` |
| Left panel: Sources with `AddSource` / `RemoveSource` (+ / −) | confirmed | the buttons' content is the glyph characters "+" / "−" — against DESIGN.Controls' "no glyph characters as icons"; D11 fixes it |
| `ChainPanel`: Detection chain expander, collapsed summary | confirmed | `Views/ChainPanel.xaml`; selectors enabled by `CanEditLegacy` (disabled in physical mode even without data), starts expanded (`IsChainExpanded = true`) |
| Selected source (isotope, X, Y, distance, activity) | confirmed | `IsEnabled="{Binding IsEditable}"` on the `SourceItemViewModel` |
| Physical optics: preset combo, rank, cell pitch, D, pixels, pixel pitch "shown and disabled for a non-Custom preset", caption "Performance depends on …" | **wrong** on "disabled" | F3. Also present: the caption "Engineering geometry" above the combo; outside the expander the collapsed summary (`OpticsEditor.Summary`), `Optics.Error` and `Acquisition.InputError` |
| Detector expander: `ReadoutPanel`, entrance / backing, Gap, Gain σ, Gain seed, Background BSR, Photon H*(10), preset / environment captions, Ambient bound, Ambient BSR | confirmed | order is Gap, GapError, Gain σ, Gain seed, Background, H*(10), AmbientDoseError, preset name, environment label, bound, ambient BSR. Gain σ / seed use `CanEditLegacy`, the rest the panel's `CanEditInputs` |
| Imaging right panel contents incl. the Physical readout cost section | confirmed, incomplete | also: caption "mm from detector · retained-data projection", `FocalSummary`, `FocusError`, `ReconstructionNote` (MLEM), legend "○ Truth / ◇ Found (read-only)", `Error`, `ChannelSummary`, `SweepSummary`. The cost section is visible only in physical mode (`ReadoutCostStatus` is "" for DirectCrystal; `NullToCollapsedConverter` collapses empty strings) |
| Spectrum, Waveform, Detector panels | confirmed, two placements | "pile-up text" = the check-box label "Merge pulses within the resolving time" plus the `ResolvingTime` value; the Waveform note is in `WaveformView.xaml` (centre), not `WaveformPanel`; the area caveat is in `DetectorPanel.xaml`, the gain-pattern caption in `DetectorView.xaml` |
| `HelpMark`, `ContentItemsList` exist; used on readout screens and the Imaging sampling caption only | confirmed, incomplete | also the two cost-section marks (`Imaging.ReadoutCostHelp`, `Imaging.ReadoutCostScope`) and `Detector.ReadoutAssumptionsHelp`; **defect F1** |
| SiPM view off-centre toward the lower left | confirmed, cause found | §6; offscreen render of the 12 × 12 fixture: square 720 px, margins left / right 48 / 96 px, top / bottom 96 / 48 px — exactly 1 : 2 sensor cells |
| No dialog exists (`ShowDialog` / `MessageBox`) | confirmed | none in `src/Gcam.Studio*` (also no `WindowChrome`) |
| `CanEditInputs => !IsPreparingReadout && !IsRunning && !HasData` (SR-RUN-12) | confirmed, incomplete | `CanEditLiveTime`, **`CanEditSpeed => !IsRunning`** (F2) and `CanEditLegacy => CanEditInputs && !IsPhysicalReadout` also gate dialog fields |
| D13: Start already refused during preparation | confirmed | `CanStart()` begins with `!IsPreparingReadout`; `PrepareReadoutCommand` and `CanEditInputs` follow `IsPreparingReadout` |
| Requirements touching moved controls (list) | **incomplete** | add SR-RUN-24 (speed), SR-RUN-25 + status table "Invalid seed … in the status bar", SR-RUN-26 (no confirmation), SR-RUN-29 ("the panel names the preset and the bound"; ambient BSR shown), SR-OPT-02 ("Custom after divergence"), SR-OPT-04 (FocusNote), SR-OPT-06 (chain expanded in the left panel; default panel fits 1280 × 800), SR-IMG-05 / -07, SR-FOCUS-04, SR-WAVE-03 / -04 / -07, SR-DET-11 (cost inheritance), SR-ENV-01 (themed title bar), SR-A11Y-01 (icon buttons' names), §5 Outputs "Front-end chain"; outside the SRS: `AGENTS.Rationale` (Reset row), DESIGN.Controls (Button row, Icon row, dark-theme pitfalls "light title bar (DWM)"), DESIGN.Color (theme step 3, DWM), VV.Studio.SDS (SU-12, external-interface row for `dwmapi`, the theme paragraph), AGENTS.Studio ("Verifying a UI change": "top-bar toggle"), AGENTS.UiAutomation §6 (re-measure / re-pilot after a window-layout change) |
| UI-automation reference counts | confirmed as counted, **incomplete** | same counts (`ThemeToggle` 9, `AcquisitionSeed` 9, `AcquisitionSpeed` 6, `ResetAcquisition` 10, `AddSource` 5, `Acquisition.AmbientDose` 6, `Detector.Gap` 4, `Imaging.ReadoutCost*` 4, `Detector.GainSigma` 2, `Chain.*` 1, `Optics.Rank` / `CellPitch` 1). Missing: `Optics.Distance` / `Pixels` / `PixelPitch` 1 each, `Optics.Preset` 2, `Optics.Section` 1, `Detector.Section` 4, `Imaging.FloodCorrection` 1, `Imaging.ChannelUnavailable` 2, `Imaging.ReadoutCurrentCounts` 1, `Imaging.StripNote` 3, `Waveform.Note` 2, `Imaging.ReconstructionNote` 2, `Readout.Prepare` 8, `Readout.Cancel` 1, `Imaging.Sweep` 4. One whole scenario is about the removed card: `ReadoutScenarioTests.WideCostCard_ShowsNeutralFAndTheDirectBiasExplanation` |
| D8 premise: "inherited" state unreachable for every standard scene | confirmed (stronger) | `ImagingWorkspaceViewModel.ReadoutCostInherited` needs `SceneDifferences(...) == 0`: one source, exactly one emission line with intensity > 0, distance = benchmark focus, one of nine positions, and a snapshot with `RateCps == 100000` exactly (no snapshot always adds a difference). Analysis lines are 122.06 / 661.7 / 1332.5 keV (`ReadoutPolicy.AnalysisLines`); no isotope in `Isotopes.All` is mono-line at one of them (Cs-137 lists Ba K X-rays, Co-60 two lines, Co-57 is 122.1 not 122.06). The SRS §8 TODO-41 note already says so |

## 2. D7 — every inline explanation and tooltip, per screen

Tier: **main** = stays inline; **help** = behind a `HelpMark`; **Options** = moves with its field into the dialog;
**split** = a short inline label stays and the rest goes behind a mark. "Req." = the SRS row that mentions it; **bold**
= the row requires it inline ("visible", "labelled", "states", "shown", a §5 output).

### Shell (`MainWindow.xaml`)

| Element | Text (short) | Tier | Req. |
|---|---|---|---|
| Live time tooltip | "Preset live time. After Stop or Completed it can be raised …" | help (or keep as tooltip of a top-bar field) | SR-RUN-24 (behaviour, not display) |
| Seed tooltip | "Monte Carlo seed. Blank: a new seed …" | Options (help mark beside the field) | SR-RUN-25 |
| Reset tooltip | "Discard the acquired data and unlock the physical inputs" | main (icon tooltip + name, D11 / D12) | SR-RUN-26 |
| Add / remove source tooltips | "Add source", "Remove selected source" | main (icon tooltips) | SR-A11Y-01 |
| Optics caption | "Engineering geometry" | main (field label) | — |
| Optics caption | "Performance depends on source positions, counts, isotope channels and focus." | help | SR-OPT-02 (statement, ambiguous; reword "is conditional … (help)") |
| Optics / Detector / chain collapsed summaries | effective values | main | SR-OPT-06 |
| `Optics.Error`, `Acquisition.InputError`, `GapError` | errors | main (GapError must stay reachable on main once Gap moves: it is already folded into `ValidationError`) | SR-OPT-03, SR-DET-01 |
| `AmbientDoseError` | "The ambient dose rate must be …" | Options (under the field) | **SR-RUN-29 / status table "under the field"** — still true inside the dialog |
| `AmbientPresetName`, `AmbientEnvironmentLabel` | preset id, "ideal environment" / bound | Options + summary line | **SR-RUN-29 "the panel names the preset and the bound"** — rewrite to "the Options dialog names …; the summary line shows the environment when not default" |
| `AmbientBsrReadout` | "off" / "x × signal" / "available during acquisition" | **main** (it is a derived result after acquisition, D1) — summary line when data exist; also in the dialog | **SR-RUN-29 "shown read-only"** |
| Flood chip tooltip `FloodHelp` | "Detected events per crystal …" | help (or keep as chip tooltip) | — |
| Heatmap / plot `AutomationProperties.HelpText` (navigation keys) | accessibility help | unchanged (not visible text) | SR-A11Y |
| `ToolHint` | per-tool instruction | main | **SR-MEAS-06 "The tool hint follows the active tool"** |
| Measurements empty text | "No measurements yet. Pick a tool …" | main | — |
| Clear / Delete measurement tooltips | | main (icon tooltips) | SR-A11Y-01 |
| Status bar `LiveTimeError`, `SeedError`, `Status` | | main (`SeedError` disappears from the bar once Seed is validated in the dialog) | status table "Invalid seed … in the status bar" — rewrite |

### Imaging right panel (`ImagingOptionsPanel.xaml`)

| Element | Text (short) | Tier | Req. |
|---|---|---|---|
| Focal-plane caption | "mm from detector · retained-data projection" | split: "mm from detector" main, "retained-data projection" help | SR-OPT-04 |
| `FocusNote` | "Reconstruction measurements cleared: their mm plane changed. Flood measurements retained." | **main** (D7 lists it for help — disagree) | **SR-OPT-04 "with a visible explanation"** |
| `GeometryText` | element, field, samples, coverage, width | main (key numbers) | **SR-OPT-05 "Display …"** |
| `SamplingCaption` + `Imaging.SamplingHelp` | one line + evidence | unchanged (already split) | **SR-OPT-05 "a one-line caption"** |
| `FocalSummary`, `FocusError` | | main | SR-OPT-06, SR-OPT-03 |
| Reconstruction combo tooltip / HelpText | "Re-decodes the retained floods. The focus sweep always uses cross-correlation …" | help | SR-IMG-07 (behaviour) |
| `ReconstructionNote` (MLEM only) | false second peak, iterations measured or not, no background term | **main** or split ("MLEM: low-count false peaks; no background term" inline + rest help) — not in D7's list, flagged | **SR-IMG-07 "A note states …"** |
| Strip check-box tooltip / HelpText | "One-pass scalar estimate … Ambient counts are not subtracted." | help (an improvement: today a check-box tooltip is not keyboard reachable beyond HelpText) | **SR-IMG-05 "states the one-pass scalar model limitation"** — reword with the help-mark convention |
| `Ratios` (each R) | | main | **SR-IMG-05 "lists each R"** |
| `Summary` (counts / net ± σ) | | main | **SR-IMG-05** |
| `Summary` tooltip | "1σ includes counting and calibration uncertainty, not scalar-model error …" | help | SR-IMG-05 |
| `StripNote` | "Display: clipped strip flood. Cross-correlation: signed difference. Net count: scalar model; no ambient subtraction. Calibration uncertainty is shared … Focus sweep: clipped flood." | **split**: the two role phrases inline, the rest help | **SR-IMG-07 "count and image-role labels follow the published result"** |
| `WorkerCosts` | "Worker: channels … ms · calibration … · decode …" | help | SR-IMG-06 "publish separate processing costs" (ambiguous; diagnostic) |
| `Error` | | main | — |
| Legend "○ Truth / ◇ Found (read-only)" | | main (short legend) | SR-IMG-04 |
| `Peaks` list, `ChannelSummary` | | main | SR-IMG-04 |
| Cost section: flood check box, `ChannelUnavailable`, `ReadoutCurrentCounts`, cost status + help, card, full table, scope help | | removed by D8 except: check box → Imaging channel (P4), `ChannelUnavailable` → main, `ReadoutCurrentCounts` → main (F5) | **SR-DET-10** (rewrite), SR-DET-11, SR-RUN-33 |
| Sweep tracks, `SweepError`, `ExternalRangeError`, `SweepSummary` | | main | SR-FOCUS-03 / -05 |
| `SweepNote` | "Experimental focus analysis: reproducible near-field bias … Half-max width is not uncertainty …" | **split**: "Experimental · near-field bias · half-max width is not uncertainty" inline, rest help (D7 lists it for help — conflicts) | **SR-FOCUS-04 "Visible text describes …"**; DESIGN.Layout "The bias/bound note is visible in the options" |

### Left-panel readout (`ReadoutPanel.xaml`) and chain (`ChainPanel.xaml`)

Already tiered by TODO-41 (`ReadoutModeSummary` main, `ReadoutAssumptions` help, line rows summary + local inline with
details behind each row's mark, `ReadoutState` / `WindowLine` / `ReadoutMessage` main). D8 adds one line "experimental
readout · localisation cost (EV-36)" with a mark. ChainPanel has labels only; the summary line moves to the Options
summary (D2 / D4).

### Spectrum (`SpectrumPanel.xaml`, `SpectrumView.xaml`)

| Element | Tier | Req. |
|---|---|---|
| Pile-up check-box label "Merge pulses within the resolving time" | main (control label) | SR-SPEC-03 |
| `ResolvingTime` ("Effective resolving interval … ns") | **main** — D7 "pile-up text" conflicts | **§5 Outputs "Front-end chain: … resolving time in ns"** |
| `Resolution` | main | **§5 Outputs**, SR-SPEC-02 |
| `Chain` (DirectCrystal: chain name) | **main** — D7 "chain text" conflicts | **§5 Outputs "read-only chain name"** |
| `Chain` (physical: the long `ReadoutAssumptions` text) | help (split by mode: inline "Experimental GAGG four-output readout", details help) | SR-DET-06 |
| `Summary` (counts, overflow), `Error` | main | **SR-SPEC-01 "shown in the spectrum summary"** |

### Waveform (`WaveformView.xaml`, `WaveformPanel.xaml`)

| Element | Tier | Req. |
|---|---|---|
| `View.Note` — mode ("Rate study: arrivals re-spaced at … — not the measured rate." / "Real acquired arrival times."), ideal / ADC-simulation statement, "Window clipped …", "Partial acquisition window …" | **split**: these state parts inline | **SR-WAVE-03 "labelled"**, **SR-WAVE-04 "Labels shall distinguish …"**, **SR-WAVE-07 "Clipping and missing acquired future shall be labelled"** |
| `View.Note` — constant caveats ("Summed energy channel; four position channels are not modelled.", "Finite filter warm-up; uncorrected shaped baseline.") | help | DESIGN.Layout "No controls imply that the position channels … have been simulated" |
| `Summary`, `PulseReadout`, `Error`, `View.ChainReadout` | main | SR-WAVE-06 "Readouts shall identify …" |

The note is composed in `Gcam.Studio.Services/WaveformService` (one string in the Core `WaveformView` record), so the split
needs a second record field (e.g. `NoteDetails`) and touches three `WaveformServiceTests` assertions on `view.Note`.

### Detector (`DetectorView.xaml`, `DetectorPanel.xaml`, `ReadoutDetectorView.xaml`)

| Element | Tier | Req. |
|---|---|---|
| `Identity` ("Settings for the next acquisition" / "Acquired settings · locked until Reset") | main | **SR-DET-02 "captioned"** |
| `GainLegend`, `Counts`, geometry `Readout` card | main | SR-DET-03 |
| "Static relative gain · viridis scale · gaps are dead geometric area" | help | DESIGN.Controls DetectorFaceView "labels the static gain pattern" (the panel title "Detector face · gain pattern" already labels it) |
| "Geometric active-area fraction describes the face, not detection efficiency …" | help | SR-DET-03 (behaviour) |
| `ReadoutDetectorView` texts | unchanged (TODO-41 tiering) | SR-DET-06 / -08 / -10 |

Recommendation for the SR text: extend the SR-DET-10 convention ("displayed" includes text behind a keyboard-focusable
help mark whose tooltip opens on hover and focus and whose HelpText carries the same text) from SR-DET-10 to SRS §2
Conventions, so every row can use it — and keep the rows that say *visible* / *labelled* inline as above.

## 3. Controls that move or change form

| Control | AutomationId | UI tests (`tests/Gcam.Studio.UiTests`) | Render tests | Headless tests | SR / DESIGN rows |
|---|---|---|---|---|---|
| Theme toggle → sun / moon icon (D11) | `ThemeToggle` | `ScenarioTests.ThemeToggle_…` (reads `Text` = UIA Name 3×, invokes 2×), `PolishSurveyTests` (Name compare 2×, invoke 2×) | — | `MainViewModelTests` (label) | SR-THEME-01 ("label names the theme it switches to" → accessible name / tooltip), DESIGN.Layout row 0, AGENTS.Studio. Keep `AutomationProperties.Name="{Binding ThemeToggleLabel}"` and the tests stay unchanged |
| Theme in an Options "Display" tab (D2) | new | — | dialog render | Options tests | duplicates the top-bar icon (§8 P7) |
| Reset → icon + confirmation (D11, D12) | `ResetAcquisition` (+ new `Confirm.Accept` / `Confirm.Cancel` / dialog id) | invoked in `ScenarioTests` (2), `MlemScenarioTests` (1), `ReadoutScenarioTests` (3), `WorkspaceScenarioTests` (1); `IsEnabled` asserts in `ScenarioTests` (3) | — | `ResetCommand` 16× in `AcquisitionViewModelTests` (9), `WaveformWorkspaceTests` (3), `FocusSweepViewModelTests` (2), `DetectorWorkspaceTests` (1), `OpticsViewModelTests` (1) | SR-RUN-26, AGENTS.Rationale, DESIGN.Controls Button row, DESIGN.Layout row 0 text |
| Add / remove source → icons | `AddSource`, `RemoveSource` | `AddSource` 5 (invoke 2, IsEnabled 3) | — | — | DESIGN.Controls Icon row |
| Clear / delete measurement → icons | `ClearMeasurements`, `DeleteMeasurement` | 0 | — | — | — |
| Prepare / Cancel prepare → icons | `Readout.Prepare`, `Readout.Cancel` | 8 / 1 (invoke only) | readout renders | — | (§8 P12) |
| Sweep / Cancel sweep → icons | `Imaging.Sweep`, `Imaging.CancelSweep` | 4 / 0 | detector-focus renders | — | — |
| Waveform Latest / Next → icons | `Waveform.Latest`, `Waveform.Next` | 0 | waveform renders | — | — |
| Stop (icon + text) | `StopAcquisition` | 8 (unchanged by Name) | — | — | needs `Icon.Stop` |
| Seed → Options | `AcquisitionSeed` | 9 `SetText` (`ScenarioTests` 2, `MlemScenarioTests` 4, `ReadoutScenarioTests` 1 in `SelectHead`, `WorkspaceScenarioTests` 2) | `MainWindowRenderTests`: asserted disabled + disabled colours, and 80 × 28 size | seed tests in `AcquisitionViewModelTests` | SR-RUN-12, -25, status table, input table, DESIGN.Layout row 0 |
| Speed → Options (or stays, F2) | `AcquisitionSpeed` | 6 (`SetText` 5, `IsEnabled` while acquiring 1) | 80 × 28 size assert | speed tests | SR-RUN-24, input table |
| Live time (stays) | `AcquisitionLiveTime` | 11 | 80 × 28 size assert | — | — |
| Detection chain → Options | `Chain.Section`, `Chain.Scintillator`, `Chain.Sensor`, `Chain.Preamp` | `Chain.Scintillator` 1 (IsEnabled) | `WaveformRenderTests` (combo present, locked, 5 items) | `WaveformWorkspaceTests` chain lock | SR-CHAIN-02, SR-OPT-06, DESIGN.Layout (scene panel, Waveform section) |
| Gap, Gain σ, Gain seed → Options | `Detector.Gap`, `Detector.GainSigma`, `Detector.GainSeed` | 4 / 2 / 0 | — | `DetectorWorkspaceTests`, `AcquisitionViewModelTests` | SR-RUN-12, -20, SR-DET-01, input table |
| Entrance / backing facts → Options | none | — | — | — | SR-RUN-20 |
| Background BSR, Photon H*(10), Ambient bound → Options | `Acquisition.Background`, `Acquisition.AmbientDose`, `Acquisition.AmbientBound` (+ `Acquisition.AmbientDoseError`) | 0 / 6 / 0 | `MainWindowRenderTests` and `AmbientRenderTests` find them in the main tree when Detector is expanded | `AmbientViewModelTests` | SR-RUN-12, -22, -29, input table, status table |
| `Detector.Section` expander (left with only the readout panel) | `Detector.Section` | 4 `Expand` | — | — | DESIGN.Layout |
| Optics fields only for Custom (D5) | `Optics.Rank`, `Optics.CellPitch`, `Optics.Distance`, `Optics.Pixels`, `Optics.PixelPitch`; `Optics.Preset`, `Optics.Section` | 1 each; `Optics.Preset` 2, `Optics.Section` 1 (`ReadoutScenarioTests`) — `CustomModules_…` must choose Custom first | `MainWindowRenderTests` optics-expanded fixtures | `OpticsViewModelTests` | SR-OPT-01, -02, -06 |
| Removed cost section (D8) | `Imaging.ReadoutCostSection`, `…CostStatus`, `…CostCard`, `…CostTableSection`, `…CostTable`, `…CostHelp`, `…CostScope`, `Imaging.FloodCorrection`, `Imaging.ChannelUnavailable`, `Imaging.ReadoutCurrentCounts` | `WideCostCard_…` (whole scenario: toggle flood correction, status, card, help), `CustomModules_…` (status), `SharpTwoLineBank_…` (`ReadoutCurrentCounts`, `ChannelUnavailable`) | `ReadoutRenderTests` help-mark inventory output | `ReadoutViewModelTests` (`ReadoutCostStatus` 13, `ReadoutCostRows` 4, `ReadoutCostHelp` 2, `ReadoutCostCard` 1, `ApplyFloodCorrection` 3); `ReadoutBankTests` (Services, flood correction 2 — unaffected) | SR-DET-10, SR-DET-11, VV.Studio matrix rows of both, AGENTS.Studio "Physical readout" paragraph ("requirement-critical state stays inline (SR-DET-10)") |
| OS caption → custom caption buttons (D10) | new `Window.Minimize`, `Window.Maximize`, `Window.Close` | none today use the OS caption (`TitleBar` 0); the harness uses `WindowPattern` / `TransformPattern` | top-bar render | — | SR-THEME-02, SR-ENV-01, DESIGN.Layout row 0 + window sizing, DESIGN.Color, SDS SU-12 |
| Notes split by D7 | `Imaging.StripNote`, `Waveform.Note`, `Imaging.ReconstructionNote`, `Imaging.FocusNote` | `StripNote` 3 (`Contains("signed difference")`, "MLEM: raw low"), `Waveform.Note` 2 (`StartsWith("Rate study: …")`), `ReconstructionNote` 2 | `MainWindowRenderTests` (StripNote text = VM, contains "signed difference" / "MLEM: raw low"), `WaveformRenderTests` (note text = `View.Note`) | `ImagingWorkspaceTests` (ReconstructionNote 5), `WaveformServiceTests` (Note 3) | see §2 |

## 4. D2 / D3 / D4 — Options view model

**Recommended shape: a draft copy applied on OK** — `OptionsViewModel` in `Gcam.Studio.Core/ViewModels`, created by a
`MainViewModel.OpenOptionsCommand` from the current values, holding **text** fields (seed, speed, gap µm, gain σ,
gain seed, H*(10), BSR) plus the chain / bound selections.

Why not bind the existing `MainViewModel` properties directly:
- every keystroke would run the live setters (`OnReflectorGapUmChanged` → `NotifyReadout` → readout plans, Detector
  workspace refresh) and Cancel would have to reverse a chain of side effects;
- the live setters coerce silently (`GainSigmaPercent` invalid → 3, `BackgroundToSignalRatio` → 0, `Speed` → 10),
  which contradicts D2's "invalid fields keep the dialog open with the field message";
- the double-typed bindings (`GainSigmaPercent`, `GainSeed`, BSR) leave unparsable text as a WPF binding error with
  no message.

Validation on OK, field by field, reusing the existing rules and texts (one source each):

| Field | Rule (existing) | In the dialog |
|---|---|---|
| Seed | `OnSeedTextChanged`: blank or integer ≥ 0, message "The seed must be blank …" | message under the field; never reaches the status bar from the dialog (status table row rewritten) |
| Speed | finite > 0 (`OnSpeedChanged` coerces to 10) | message, dialog stays open; enabled by `CanEditSpeed` (F2) |
| Gap | `ValidateGap`: finite, ≥ 0, < `OpticsEditor.Effective.PixelPitchMm` | same rule against the **pending** pitch at OK time. A later pitch edit on the main screen still revalidates the applied gap through `ValidateGap` → `GapError` → `ValidationError` (`Acquisition.InputError`) on the main screen; the summary line marks the gap and points to Options. Start keeps refusing (`BeginAcquisition`), and must not open the modal by itself — today it sets `IsDetectorExpanded = true`, which then opens nothing useful |
| Gain σ / seed | finite ≥ 0 / integer | message instead of silent 3 |
| H*(10) | `AmbientDosePattern` + finite; SR-RUN-29 message | message under the field (SR-RUN-29 "under the field" stays true); "the previous value stays" is automatic (draft not applied) |
| BSR | finite ≥ 0 | message instead of silent 0 |

Apply writes the validated values through the existing setters (seed text, speed, chain, gap text, σ, gain seed, dose
text, bound, BSR — gap after nothing else in the dialog, since pitch is not in it), so the view-model guards
(`Locked`, `LockedLegacy`) stay the single enforcement point for every writer (SR-RUN-12 "refused by the view model").
The setters keep their coercion for other writers; the input-table rows change from "invalid UI σ → 3 %" to "the Options
dialog refuses it with a message; the view model coerces other writers' invalid values".

**Read-only with data (D3)** — per field, not per dialog: `CanEditInputs` for seed, gap, BSR, H*(10), bound;
`CanEditLegacy` for chain, gain σ, gain seed (inactive in physical mode even without data — `ReadoutAssumptions` says
"Legacy gain and chain are inactive"); `CanEditSpeed` for speed if it moves. When nothing is editable the dialog shows
the existing `DetectorWorkspace.Identity` caption ("Acquired settings · locked until Reset") and one Close button
(OK / Cancel are pointless). Disabled fields keep the retained-value contrast (SR-A11Y-05). Opening the dialog itself is
allowed in every state.

**Summary line (D4)** — compare against **one Core record of defaults**, e.g. `StudioOptionDefaults` (seed blank,
speed 10, `FrontEndParts.Default`, gap 100 µm, σ 3 %, gain seed 1, H*(10) `DefaultAmbientDoseRateMicroSvPerHour`,
bound front-only, BSR 0), which the `MainViewModel` initialisers and coercion fallbacks also use (today the defaults
are literals spread over the field initialisers and the `OnXChanged` fallbacks). Compare parsed values (gap "100" vs
"100.0", dose by value). Recommended form: list **only** the non-default items, text plus a semibold weight (not colour
alone, SR-A11Y-04), e.g. "Options: seed 12345 · ideal environment · NaI(Tl)", and "Options: defaults" otherwise; in physical
mode mark chain / gain "inactive" instead of emphasising them; with data, append the ambient BSR result. Entrance /
backing are constants and never listed; theme is not a result input and is not listed. Options stay session-only (not
persisted) — AGENTS.UiAutomation stage 1 relies on Studio writing no settings.

**Alternative worth deciding before stage 2 (recommended):** render the Options panel and the Reset confirmation as an
**in-window modal layer** (a scrim over the content, focus kept inside, Escape = Cancel) instead of separate `Window`s.
It stays one HWND and one UIA tree, so the harness, `EnsureForeground`, the failure capture and graceful shutdown work
unchanged; it inherits the token dictionary and needs no DWM title bar; and the render tests can draw it detached like
the rest of the window. Cost: focus trapping and Escape handling written once in a small control. The separate-window
path is specified below in case D2's "dialog" means a real window.

## 5. D10 — WindowChrome on WPF / .NET 9, and UI automation

**Chrome settings** (keep `WindowStyle="SingleBorderWindow"`, do not use `None`: maximise then respects the work area
and the taskbar, and Win+arrow / drag snap keep working):
- `WindowChrome.CaptionHeight` = top-bar height (`Size.Bar` less its 1 px bottom border);
  `ResizeBorderThickness` = `SystemParameters.WindowResizeBorderThickness`; `UseAeroCaptionButtons="False"`;
  `CornerRadius="0"`; `GlassFrameThickness` small but non-zero (e.g. `0,0,0,1`) so DWM keeps the shadow and the
  Windows 11 rounded corners — *desktop check*: `0` drops the shadow on some builds.
- Hit-testing: everything interactive in the caption needs `WindowChrome.IsHitTestVisibleInChrome="True"`. The
  property inherits, so set it on the top bar's two `StackPanel`s; their transparent gaps still fall through to the
  caption (drag, double-click maximise / restore, right-click system menu). HTCAPTION from WindowChrome gives drag,
  Aero snap and Alt+Space; Alt+F4 is `WM_SYSCOMMAND` and stays. The snap-layout flyout needs `HTMAXBUTTON`, which
  WindowChrome does not return — the recorded gap is correct.
- **Maximised inset:** a maximised `SingleBorderWindow` extends past the monitor by the frame thickness, so the outer
  pixels (top bar, Pad.Window edge, status bar) are cut off. Pad the root by
  `WindowResizeBorderThickness` (+ padded border) while `WindowState == Maximized`. `SystemParameters` reports the
  primary monitor's DPI only; on a second monitor with other scaling the inset is off by a few pixels unless computed per
  DPI (`GetSystemMetricsForDpi(SM_CXSIZEFRAME) + SM_CXPADDEDBORDER`) — record as a known limit or compute it.
- **Caption buttons:** SR-ARCH-03 forbids code-behind beyond `InitializeComponent()`, so put the three buttons in a
  small control (`Controls/CaptionButtons`) that finds its window and calls `SystemCommands.MinimizeWindow /
  MaximizeWindow / RestoreWindow / CloseWindow`, swaps the maximise / restore icon on `StateChanged`, and exposes
  AutomationIds `Window.Minimize` / `Window.Maximize` (Name "Maximize" / "Restore") / `Window.Close`; tab stops, focus
  visual, tooltips. The same control (or a behaviour) can own the maximised inset, which avoids an
  `AncestorType=Window` binding in the view (a planner caution).
- **Theme:** with no OS caption the top bar *is* the caption and already follows the tokens. `ApplyTitleBar` can stay
  for what DWM still draws (Windows 11 frame border; the system menu's theme where supported — *desktop check*) and for
  any dialog that keeps OS chrome. SR-THEME-02 → "every window repaints; the main window's caption is the themed top bar;
  windows with an OS title bar ask DWM to match (best effort, SR-ENV-01)". SDS SU-12 and DESIGN.Color step 3 follow.
- **Geometry consequence:** with WindowChrome the client area equals the window size, so at the harness's 1440 × 900 the
  content grows by the old frame (about 16 × 39 DIP at 100 % on Windows 11 — *desktop check*). The offscreen renders
  already lay out the content at exactly 1280 × 800 / 1440 × 900, i.e. today they overstate the real window's content by
  that frame (the "default left panel fits 1280 × 800" assertion holds for the render, not for the real minimum
  window). After D10 render and window agree. AGENTS.UiAutomation §6 requires stage 2 re-measurement and a stage 3
  re-pilot after a window-layout change.

**UI automation:**
- The OS `TitleBar` with its Minimize / Maximize / Close elements is not used by any test. Because `WS_CAPTION` stays,
  the UIA non-client proxy may still report a phantom title bar at the OS positions (duplicate "Close" for a screen
  reader) — check in the stage 2 tree dump; selectors use AutomationIds, so tests are unaffected.
- `WindowPattern` and `TransformPattern.Resize` (used by `StudioWindow.Normalise` and `EnsureForeground`) remain.
- **Finding a modal window** (separate-window path): give the dialog `AutomationProperties.AutomationId`
  (e.g. `Options.Dialog`, `Confirm.Dialog`) and `Owner = MainWindow`. Search the owned main window's descendants first,
  then `AutomationElement.RootElement` children with `ProcessId` = the owned PID and that AutomationId (owned top-level
  windows are usually reparented under their owner in UIA, but do not depend on it). Wrap it as a second `StudioWindow`
  so `ById`, `Invoke`, `SetText` work in it; `EnsureForeground` already accepts any handle of the owned PID.
- Opening: `InvokePattern.Invoke` on a WPF button returns at once (the peer posts the click), so invoking `Options…` or
  `ResetAcquisition` does not block on the modal loop; then `WaitUntil(dialog exists)`.
- Pointer clicks on the main window while a modal is up fail by design (`Click` → `EnsureForeground` sees the dialog in
  front); dialogs are driven by patterns only.
- **Shutdown safety:** `StudioProcess.Dispose` sends `CloseMainWindow` and never force-stops. A test that fails with a
  dialog open must first close it (`WindowPattern.Close` = Cancel) or the graceful close may not end the process and the
  run reports "still running" (force-stop needs the author's approval). Add that step to `Scenario.Run`'s `finally`.
- Helpers: `ui.ConfirmReset(accept: true)` and `ui.WithOptions(o => { o.SetText("Options.Seed", "12345", "Options.Speed"); })`
  (opens, edits, OK, waits for close and reads the summary back). With the in-window layer the same helpers work on the
  main tree.

## 6. D9 — cause of the off-centre SiPM array

`Controls/ReadoutMapView.cs`, `Node(...)` and `BuildCircuit(...)`: the unit square is divided into `sx + 3` (x) and
`sy + 3` (y) cells; a sensor `S{i}_{j}` is centred at `(i + 1.5)·cellX`, `(j + 1.5)·cellY`, and its cell rectangle is
± half a cell. Sensors therefore occupy cells `[1, sx + 1]`: **one cell of margin on the left / bottom and two on the
right / top** (y is up in the normalised frame). The row-chain ends `L{r}` / `R{r}` sit at x = 0.025 / 0.975 and the
outputs at the corners (0.025 / 0.975), symmetric, so only the array is shifted by half a cell toward the lower left.
Measured on the offscreen 12 × 12 fixture (`ReadoutRenderTests`, 720 px square, cell = 48 px): margins 48 / 96 px left /
right and 96 / 48 px top / bottom.

Fix: centre at `(i + 2)·cellX`, `(j + 2)·cellY` (1.5 cells each side), and the chain ends at `(r + 2)·cellY` so the row
wires stay on the sensor rows. Better: move the normalised layout into Core (e.g. `Detector/ReadoutCircuitLayout`) per
DESIGN.Controls rule 1, with a headless test of equal margins for 12 × 12 and 44 × 44 and of `L` / `R` alignment with
their row. Also check in the new renders: the output letters are drawn with their top-left corner at the anchor, so the
right (B, D) and bottom (A, B) letters sit off-centre and can clip at small sizes; centring the text on the anchor
(half its measured width / height) is a one-line change.

## 7. D12 — the confirmation interface

- Contract in Core (no WPF), per DESIGN.ViewLayer ("dialogs → service behind a Core contract"):
  `Gcam.Studio.Core/Services/IConfirmationService.cs` with `Task<bool> ConfirmAsync(ConfirmationRequest request)`;
  `ConfirmationRequest(string Title, string Message, string AcceptLabel, string CancelLabel)` (Cancel is the default and
  Escape answers it).
- `MainViewModel` gains an optional constructor parameter `IConfirmationService? confirmation = null`. `ResetAsync`:
  `if (!CanReset()) return; if (!await confirm(...)) return; if (!CanReset()) return;` then the existing discard. The
  message is built in Core from the snapshot, e.g. "Discard the acquired data (60.0 s live, 6,463 counts)? Inputs unlock
  for the next acquisition." (numbers from `Snapshot.LiveTimeS` / `Counts`).
- Default when null: an explicit `ConfirmationService.Always` (accepts). The 16 existing `ResetCommand` calls stay as
  they are; production must register the WPF implementation in `App.OnStartup` (no headless test can see the composition
  root, SR-ARCH-04, so the render fixture or a desktop scenario must prove the box appears). New headless tests with a
  fake: declining keeps snapshot, state, session and Continue (`StartLabel` "Continue", `CanEditInputs` false); accepting
  discards exactly as today; Reset while acquiring never asks; the message carries live time and counts.
- WPF implementation: `src/Gcam.Studio/Services/ConfirmationService.cs` showing a themed confirmation (the in-window
  layer, or a `Window` with `Owner`, `ShowInTaskbar=False`, `IsCancel` on Cancel, focus on Cancel, result set by the
  service, not code-behind). `MessageBox` is not themable (a white box in the dark theme) — avoid it.
- UI tests: the seven Reset invocations become `ui.ConfirmReset(accept: true)`; `StopContinueReset_…` gains a Cancel step
  (data and counts kept, Continue still possible) — the Done-when's "Cancel keeps the data (desktop)"; its broken-verdict
  variant expects the wrong state after Cancel.

## 8. Change size, and proposals

### Estimate per stage

| | Stage 1 (TODO-44: F1 fix, D7, D8, D9, D11, D12) | Stage 2 (TODO-45: D2–D6, D10) |
|---|---|---|
| Product files | ~20: 7 views (`MainWindow`, `ImagingOptionsPanel`, `ReadoutPanel`, `SpectrumPanel`, `WaveformView`, `DetectorView`, `DetectorPanel`), `HelpMark.cs`, `ReadoutMapView.cs` (+ Core layout), `Themes/Controls.xaml` (~16 icon geometries, an icon-button style), `Metrics.xaml`; Core: `MainViewModel` (+ `.Readout`), `ImagingWorkspaceViewModel` (+ `.Readout`, cost members removed), `IConfirmationService` + request, `WaveformView` record; Services: `WaveformService`; Studio: `ConfirmationService` (+ dialog / layer view), `App.xaml.cs` | ~12–15: `MainWindow.xaml` (chrome, top bar, left panel, summary line), Options view, `CaptionButtons` control, themes; Core: `OptionsViewModel`, defaults record, `MainViewModel` (open command, summary), `OpticsEditorViewModel` (Custom mode); `App.xaml.cs`; possibly `ThemeService` |
| Headless tests | `ReadoutViewModelTests` (≈20 cost assertions removed / rewritten to the new line + help), `AcquisitionViewModelTests` (+4 confirmation tests), `ImagingWorkspaceTests` (note split), `WaveformWorkspaceTests`; `WaveformServiceTests` (3 note assertions); + Core layout test for D9. 231 → ≈ 230–240 | new `OptionsViewModelTests` (apply / cancel / per-field validation / read-only with data / physical-mode inactive fields / speed after Stop / gap revalidation after a pitch edit / summary non-defaults) ≈ 15; `OpticsViewModelTests` (Custom mode, no jump) ≈ 3 |
| Render tests | `MainWindowRenderTests` (StripNote), `WaveformRenderTests` (note), `ReadoutRenderTests` (help marks, SiPM centring, readout cost line), + a tooltip `MaxWidth` assertion | `MainWindowRenderTests` (seed / speed / ambient assertions move to a dialog fixture; top bar; summary line; left panel still fits), `AmbientRenderTests`, `WaveformRenderTests` (`Chain.Scintillator` no longer in the main tree), new dialog renders in both themes and sizes |
| UI tests | harness: dialog finder, `ConfirmReset`, close-dialog-before-shutdown; `ScenarioTests`, `MlemScenarioTests`, `ReadoutScenarioTests` (cost scenario rewritten, `CustomModules_…` cost assertion, `SharpTwoLineBank_…` counts / unavailable ids), `WorkspaceScenarioTests`; one new Reset-Cancel scenario | ≈ 33 call sites move behind `WithOptions` (seed 9, speed 6, H*(10) 6, gap 4, gain σ 2, chain 1, optics 5 + "Custom" first); `Normalise` semantics (client = window); stage 2 re-measure + stage 3 re-pilot |
| Desktop scenarios to rerun | effectively the whole suite (27 without the long High-res case): Reset is pressed in 7 scenarios, ThemeToggle in 2, StripNote / Waveform note / ReconstructionNote are read in 6, the cost scenario is rewritten; broken-verdict runs for the new Reset-Cancel scenario, the rewritten cost scenario and every scenario whose assertion text changed; survey PNGs regenerated | whole suite again (window geometry changes every pointer position; seed / speed are set in nearly every scenario), broken-verdict for those touching the dialog, recovery run, survey |
| Docs to propose | SRS ≈ 18 rows + §2 convention + §5 tables; VV.Studio matrix rows; DESIGN.Controls (Button, Icon, HelpMark, caption buttons, confirmation), DESIGN.Layout (row 0, scene panel, window sizing, Imaging panel), AGENTS.Rationale (Reset), AGENTS.Studio, SDS (new units: confirmation service, options VM, caption control) | SRS (SR-RUN-12 / -24 / -25 / -29, SR-CHAIN-02, SR-DET-01, SR-OPT-01 / -02 / -06, SR-THEME-01 / -02, SR-ENV-01, input + status tables), DESIGN.Layout, DESIGN.Color, AGENTS.UiAutomation (dialog driving, re-measure record) |

### Proposals and disagreements

| # | Proposal | Reason |
|---|---|---|
| P1 | Fix F1 first (stage 1 step 0) with a test | every D7 move adds a mark whose tooltip throws today |
| P2 | Keep **Speed** in the top bar beside Live time (or per-field `CanEditSpeed` in the dialog) | SR-RUN-24; speed is changed between Stop and Continue; `MlemScenarioTests` / `ReadoutScenarioTests` set it in nearly every run |
| P3 | Keep FocusNote inline; split SweepNote, StripNote, ReconstructionNote, the Waveform note and the physical-mode Spectrum chain text; keep the DirectCrystal chain name and resolving time inline | the SR rows in §2 require them visible / labelled / as outputs; D1's own rule (state and key numbers stay) covers most of them |
| P4 | D8: move the flood-correction check box to the Imaging channel section (physical mode), with `ChannelUnavailable` and `ReadoutCurrentCounts` | it is an Imaging re-projection setting (SD-A4, `ApplyFloodCorrection` "never a transport or calibration input"); the left panel holds acquisition inputs that lock with data (SR-NAV-03); the two texts are a refusal and a key number (D1) |
| P5 | D8: delete the UI-only cost members (`ReadoutCostStatus`, `…Details`, `…Card`, `…Rows`, `…Help`, `…Scope`, `…Inherited`, `ReadoutCostRowView`) and their tests; keep the record, loader, `CostRows` and the CI record check; build the new line's help from the record (benchmark scope, the F range of the module's offered cells, `BiasExplanations` for F below 1, and the mandated sentence "F is an equal-fluence MSE ratio, not a time factor.") | no dead view-model surface; SR-DET-10 still requires that sentence beside any F value; SR-DET-11's cost-inheritance clause needs rewriting too |
| P6 | Record D12 as a reversal: rewrite SR-RUN-26 and the AGENTS.Rationale row with the author's new reason | the old reason is still in both documents |
| P7 | Drop the Options "Display" tab while the top-bar theme icon stays (D11), or state that both exist | two controls for one setting; a one-item tab |
| P8 | Ambient BSR (a derived result) on the main screen via the summary line once data exist | D1: key numbers never move into the dialog |
| P9 | Options and Reset confirmation as an in-window modal layer instead of separate windows | one UIA tree, no foreground / shutdown hazards in the harness, theme for free, offscreen renderable (§4) |
| P10 | D5: an explicit "Custom" mode flag set only by the selector; while it is on, `Validate()` must not re-select a matching preset (or the fields stay visible until the selector changes) | today an edit that matches a preset switches the selector (`OpticsPreset.Match`), which with D5 would hide the fields under the cursor (`UpdateSourceTrigger=PropertyChanged` matches mid-typing) |
| P11 | Summary line lists only non-default items (text + weight), defaults from one Core record | short enough for the 300 DIP panel and still "no hidden setting changes a result unseen" |
| P12 | Keep icon + text for **Prepare** and **Sweep focus** (icon-only for their Cancel twins) | they start long computations (Prepare up to 90 min), like Start; the plan already keeps text for "Use as focus" and the benchmark window |
| P13 | Add `Icon.Stop` and a shared icon-button style (`Size.Control` square, `Size.Icon` path, ghost states, tooltip + name required) | F7; DESIGN.Controls "add a shared style when two or more views need it" |
| P14 | Options are session-only, never persisted | AGENTS.UiAutomation stage 1 (Studio writes no settings) would otherwise need a re-run |
| P15 | Move the D12 confirmation's harness support into stage 1, where the first modal appears | stage 2 then reuses the finder and the shutdown guard |

## Commands run that wrote anything

- `dotnet build Gcam.sln -c Release` (repository `bin` / `obj`, ignored by git).
- `dotnet test` of `Gcam.Studio.Tests` (231 passed), `Gcam.Studio.RenderTests` (2 passed, 2 opt-in skipped) and
  `Gcam.Studio.UiTests` (23 passed, 28 desktop skipped) with `--no-build` (test result folders under `tests/*/TestResults`,
  ignored by git).
- One opt-in offscreen render: `GCAM_RENDER_SNAPSHOTS=1`, `GCAM_RENDER_OUTPUT` under the task scratch folder,
  `--filter ExperimentalDetectorAndFourLanes` (1 passed; PNGs in scratch only).
- A throw-away WPF probe project in the task scratch folder (four `dotnet run`s) for F1; one run briefly created an
  off-screen, non-activated probe window (not Studio) to open a tooltip, closed by the probe itself.
- This file.
