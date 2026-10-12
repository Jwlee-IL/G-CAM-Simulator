# PLAN.Studio.Declutter — help marks, an Options dialog and a quieter window (TODO-44, TODO-45)

Scope: TODO-44 and TODO-45, designed together because both decide where each GCAM Studio control and text lives. Three
tiers: the **main screen** keeps what a run touches every time, results, state, errors / refusals and key numbers;
**help marks** carry explanations, assumptions, provenance and long reasons; an **Options dialog** holds inputs changed
rarely. Analysis controls that re-read retained data (window N × FWHM, Compton strip, reconstruction, focus sweep, the
Spectrum / Waveform display controls) stay in their workspace panels — they are not acquisition inputs. The window loses
the OS title bar; secondary actions become icon buttons. Studio stays the simulator's engineering viewer (D-34); no
physics, defaults or acquisition behaviour change.

Status: **planned — reference plan written 2026-10-12** from the author's design discussion (decisions below, "author:
recommended" = the author accepted the planner's recommendation and will give feedback on the result). Next: turn 1
(implementer review, read-only) when Codex is available.

## What exists (checked in the repo, 2026-10-12, at `7d9c9e5`)

- **Window and top bar:** `src/Gcam.Studio/Views/MainWindow.xaml` — a normal OS-chromed window (1440 × 900, minimum
  1280 × 800). Top bar (`Size.Bar` 48): logo, title, workspace switch, `ThemeToggle` (text button), Live time, Speed,
  Seed (text boxes), `ResetAcquisition` (text button, no confirmation), Start / Continue / Stop (icon + text). The OS
  title bar follows the theme through DWM (`Services/ThemeService.cs`, SR-THEME-02). Layout record: DESIGN.Layout
  ("Row 0 Top bar").
- **Left panel (scene):** Sources list with `AddSource` / `RemoveSource` (+ / −); `ChainPanel` (Detection chain
  expander: scintillator, photosensor, preamp / shaper, collapsed summary line); Selected source (isotope, X, Y,
  distance, activity); Physical optics expander (preset combo, then rank, cell pitch, mask–detector, pixels / side,
  pixel pitch — shown and disabled for a non-Custom preset — and the caption "Performance depends on …"); Detector
  expander holding `ReadoutPanel` plus entrance / backing facts, Gap, Gain σ, Gain seed, Background BSR, Photon H*(10),
  ambient preset / environment captions, Ambient bound, Ambient BSR.
- **Imaging right panel:** `Views/ImagingOptionsPanel.xaml` — focal plane, FocusNote, geometry text, sampling caption
  with a `HelpMark`; channel, reconstruction (long tooltip), window N × FWHM, Compton strip (long tooltip), ratios,
  summary (1σ tooltip), StripNote, WorkerCosts, peaks; the **Physical readout cost** section (flood-correction check box,
  channel-unavailable warning, current counts, cost status + help, cost card, full cost table, benchmark conditions);
  Focus sweep.
- **Other panels:** Spectrum (`SpectrumPanel` — Log Y, window, pile-up text, resolution, chain), Waveform
  (`WaveformPanel` — scope window, Latest / Next, index, length, rate study, ideal, acquired chain, events), Detector
  (`DetectorPanel`, `DetectorView` — geometry readout with the area caveat, gain-pattern captions).
- **Help control:** `Controls/HelpMark.cs` and `Controls/ContentItemsList.cs` exist (TODO-41; DESIGN.Controls "HelpMark
  and ContentItemsList"); used on the readout screens and the Imaging sampling caption only.
- **SiPM view:** `Controls/ReadoutMapView.cs` draws the array inside the A / B / C / D output frame; in the 12 × 12
  render the array sits off-centre toward the lower left (left / right margin ≈ 30 / 78 px, top / bottom ≈ 73 / 35 px at
  1440 × 900).
- **No dialog exists yet** in Studio (no `ShowDialog` / `MessageBox`): the Options dialog and the Reset confirmation are
  the first modal windows, also for UI automation.
- **Locking:** `MainViewModel.CanEditInputs => !IsPreparingReadout && !IsRunning && !HasData` (SR-RUN-12). Start is
  already refused while a readout preparation runs (`CanStart`), so no new acquisition guard is needed.
- **Requirements touching moved controls:** SR-RUN-12 / -20 / -29, SR-CHAIN-01 / -02, SR-DET-01, SR-DET-10 (cost card),
  SR-THEME-01 / -02, the input table rows (VV.Studio.SRS "Inputs"), DESIGN.Layout row 0.
- **UI-automation references** (tests/Gcam.Studio.UiTests): `ThemeToggle` 9, `AcquisitionSeed` 9, `AcquisitionSpeed` 6,
  `ResetAcquisition` 10, `AddSource` 5, `Acquisition.AmbientDose` 6, `Detector.Gap` 4, `Imaging.ReadoutCost*` 4,
  `Detector.GainSigma` 2, `Chain.*` 1, `Optics.Rank` / `CellPitch` 1 each.

## Decisions (author 2026-10-12; *verify* = check in the code during turn 1)

| # | Decision | Kind |
|---|---|---|
| D1 | Three tiers as in the scope; errors, refusals, experimental labels, state, progress and key numbers never move behind a help mark or into the dialog. | author |
| D2 | **Options dialog**, modal, OK / Cancel, validation on OK (invalid fields keep the dialog open with the field message; Cancel restores). Tabs: **Acquisition** (seed, speed), **Detector** (detection chain; entrance / backing facts; gap, gain σ, gain seed), **Environment** (photon H*(10), ambient bound, background BSR, ambient BSR readout and preset captions), **Display** (theme). | author: recommended |
| D3 | While data exist (or a preparation / acquisition runs) the dialog opens read-only with "Acquired settings · locked until Reset" — the SR-RUN-12 rule unchanged; Display stays editable. | author: recommended |
| D4 | The left panel keeps one **summary line** of the Options in effect with an **Options…** button; values that differ from the Studio defaults are emphasised, so no hidden setting changes a result unseen. | author: recommended |
| D5 | Optical numeric fields appear only for **Custom**; a preset shows a one-line summary (rank · cell · D · pixels · pitch). The preset combo stays on the main screen. | author: recommended |
| D6 | Window N × FWHM, Compton strip, reconstruction, focus sweep, Spectrum / Waveform display controls stay in their panels. | author: recommended |
| D7 | Help marks replace inline explanations: "Performance depends on …", FocusNote, reconstruction / strip / 1σ tooltips, StripNote, WorkerCosts, SweepNote, pile-up text, chain text, Waveform note, Detector area caveat, gain-pattern captions (*verify* the full list per screen; keep anything a requirement calls displayed inline). | author |
| D8 | **Remove the Imaging "Physical readout cost" section** (status, card, full table, scope help): its "inherited" state is unreachable for every standard scene. The flood-correction check box moves to the readout panel; the readout panel gains one line "experimental readout · localisation cost (EV-36)" with a help mark (benchmark scope, F range, F below 1 = direct-reference bias). The offer record and its cost rows stay (evidence; CI check unchanged). SR-DET-10 is rewritten. | author |
| D9 | **SiPM view centred:** the array is centred inside its A / B / C / D frame (*verify* the cause in `ReadoutMapView`: frame margins vs. array origin). | author |
| D10 | **No OS title bar:** `WindowChrome`; the top bar is the caption (drag to move, double-click maximise / restore, OS snap, resize borders, Alt+F4 kept). Icon buttons at the right: minimise, maximise / restore, close, each with tooltip, keyboard focus and automation name. The Windows 11 snap-layout flyout on the maximise button is **not** in scope (recorded as a known gap). SR-THEME-02's "title bar follows (DWM)" is rewritten. | author: recommended |
| D11 | **Icon buttons** for secondary actions: theme (sun / moon), add / remove source, delete / clear measurements, Prepare / Cancel prepare, Sweep / Cancel sweep, Waveform Latest / Next. **Start / Continue / Stop keep icon + text**; Use as focus and Use ±15 % benchmark window keep text. Icons are theme Path resources (like `Icon.Play`); every icon button has a tooltip and `AutomationProperties.Name`. | author |
| D12 | **Reset:** icon button plus a confirmation box — "Discard the acquired data (live s, counts)? Inputs unlock for the next acquisition." Default button Cancel; Escape cancels. Core asks through an interface (e.g. `IConfirmationService`), faked in headless tests. | author |
| D13 | No new acquisition guard during flood preparation (already refused). | author |
| D14 | One plan, two stages in one implementer conversation: stage 1 = TODO-44 (D1, D7–D9, D11–D12 where independent of the dialog), stage 2 = TODO-45 (D2–D6, D10). Each stage: build, normal suite, offscreen renders; desktop scenarios only on the author's go, listing exactly the scenarios each stage changes. | planner |

## Steps

1. Turn 1 (review, read-only): check every *verify* row and the "What exists" list; propose the per-screen help-mark
   list (D7) as a table; map each moved control to its requirement rows and UI-automation references; propose the
   dialog's view-model shape (one `OptionsViewModel` over the existing `MainViewModel` properties vs. a copy applied on
   OK) and how UI automation drives a modal window; estimate the scenario changes.
2. Planner: "Decisions after review".
3. Stage 1 (TODO-44) — implementation, tests (headless: confirmation fake, help-mark texts, removed cost section; render:
   both themes and sizes), SR / DESIGN wording proposals.
4. Stage 2 (TODO-45) — dialog, summary line, custom-only optics fields, window chrome; headless dialog tests (apply /
   cancel / read-only with data / validation), renders of the dialog and the new top bar.
5. Planner verification after each stage; desktop runs on the author's go; records; close both TODOs.

## Done when

- Every control and text has a tier, and the main screen holds only tier-1 content (renders inspected in both themes,
  1280 × 800 and 1440 × 900).
- The dialog applies, cancels and locks exactly as SR-RUN-12 says; the summary line shows every non-default value.
- Reset asks before discarding; Cancel keeps the data (headless and desktop).
- The window has no OS title bar and keeps move / maximise / snap / resize / Alt+F4; caption buttons are reachable by
  keyboard and UI automation.
- SR rows (SR-RUN-12 / -20 / -29, SR-CHAIN-01 / -02, SR-DET-01 / -10, SR-THEME-01 / -02, input table), DESIGN.Layout /
  DESIGN.Controls and AGENTS.UiAutomation are updated; the affected desktop scenarios pass with broken-verdict / recovery
  pairs.

## Not here

- The Windows 11 snap-layout flyout (D10). The manual (TODO-43) follows this plan. No change to defaults, physics, the
  offer record or acquisition behaviour. The README screenshot is retaken after this plan.
