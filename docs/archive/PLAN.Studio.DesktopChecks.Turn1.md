# PLAN.Studio.DesktopChecks.Turn1 — implementer's turn: TODO-38 cause and fix, MLEM desktop scenarios

Scope: [PLAN.Studio.DesktopChecks](PLAN.Studio.DesktopChecks.md) DC-1 … DC-4, review and implementation in one turn, by
the substitute implementer (a Claude subagent; Codex unavailable). I had no material disagreement with any DC row. I
did not edit any plan, AGENTS, VV, CLAUDE or README file and changed no git state; scratch is under `%TEMP%\gcam-todo38\`.
No desktop UI test and no Studio launch were run (DC-4): the planner runs the desktop suite.

Status: implemented, headless-verified, 2026-10-07. The desktop run is pending (planner).

## At a glance

- **TODO-38's cause is not the ambient field.** The Co-60 channel of the pair scene (20 µCi) holds about 40 counts in
  its window. Its peak falls on the wrong side in **27 % of acquisitions**, so the scenario was underpowered and passed
  only through its realisation.
  - The ambient default deposits 0 counts in the Cs-137 or Co-60 window (12 events in 60 s, all energies). It changes
    only the random-number sequence, and so the realisation.
  - With the ambient field off, the same seed also fails: the Co-60 peak is at −38.9 mm.
  - Not a regression: the measured Co-60 rate (1.98 counts per µCi in 60 s) matches the 41 counts seen.
- **Fix (DC-2, by a derived criterion):** the scenario's Co-60 activity is 400 µCi
  (`WorkspaceScenarioTests.SeparatedCoActivityUCi`). Its channel-side assertions then fail with probability ≤ 10⁻³ per
  channel, by a Bonferroni bound computed from the measured window shapes:
  - Co-60: 1.3 × 10⁻⁴ (about 792 counts);
  - Cs-137, unstripped, with Co downscatter: 1.3 × 10⁻⁵.

  At seed 12345 the realisation gives Co-60 (21.38, −0.12) mm and Cs-137 (−20.33, 0.41) mm with the ambient default, and
  (21.55, 1.28) / (−20.30, 0.36) mm with it off. The scenario keeps the default environment, which is what a user sees.
  The survey keeps its documented 20 µCi scene, because it is diagnostic only.
- **DC-3: six MLEM desktop scenarios** in `MlemScenarioTests`, each with an independent oracle. `FloodOracle` (`Verdict`)
  learns the readout unit, including "(MLEM λ)". One read-only evidence hook was added in the product.
- **Tests:** full headless suite green; desktop cases reported as skipped. Counts are below.

## DC-1 — the failing scene, headless

**Method.** Scratch probes `%TEMP%\gcam-todo38\probe` and `probe2`.
- They drive Studio's own `MainViewModel` with the real `SimulationService`, `SpectrumService` and `ImagingService`.
  The scene is the UI test's: Cs-137 500 µCi at (−20, 0) mm and Co-60 20 µCi at (20, 0) mm, 1 m, live time 60 s, seed
  12345, default detector and chain (gain σ 3 %, gap 100 µm).
- The ambient field is the Studio default (0.10 µSv/h, terrestrial UNSCEAR 2000 v1, front-only) or off.
- Each channel's window comes from the scene, as in Studio.

| Run | Events (60 s) | Cs-137 window 616.3–707.1 keV: counts, peak | Co-60 window 1100.4–1246.0 keV: counts, peak |
|---|---|---|---|
| Ambient default, Co 20 µCi | 4,268 | 1,270; (−21.69, 0.66) mm | **41; (−38.72, 4.27) mm** (wrong side) |
| Ambient off, Co 20 µCi | 4,256 | 1,262; (−21.53, 0.63) mm | **41; (−38.94, 4.27) mm** (wrong side) |
| Ambient only (same field, detector, seed) | 12 | **0** | **0** |
| Ambient default, Co 400 µCi | 9,935 | 1,593; (−20.33, 0.41) mm | 822; (21.38, −0.12) mm |
| Ambient off, Co 400 µCi | 9,923 | 1,591; (−20.30, 0.36) mm | 789; (21.55, 1.28) mm |

**How underpowered 20 µCi is.**
- The Co-60 window shape and rate come from a high-statistics Co-only acquisition: 20,000 µCi, 60 s, ambient off, seed
  999. It gave 39,614 window counts, which is **1.98 counts per µCi** in 60 s; 46.7 % of that again lands in the Cs-137
  window.
- I then made 20,000 exact Poisson draws λ = N·p, reconstructed each with Studio's own cross-correlation (via
  `CorrelationSearch`, bit-exact to it), and counted draws whose argmax lies at x ≤ 0:

| Co-60 window counts N | Activity (60 s) | Argmax on the wrong side |
|---|---|---|
| 41 | 20 µCi | **26.96 %** |
| 200 | ≈ 100 µCi | 3.51 % |
| 410 | ≈ 200 µCi | 0.22 % |

**Why it passed before.**
- At 41 counts a fixed seed gives a fixed realisation that is wrong in about one case in four.
- TODO-30 turned the ambient field on by default, which takes another path through the list-mode source (`AdvanceUntil`)
  and consumes the random stream differently. With the same seed the realisation changed (4,268 against 4,256 events
  here).
- With the field off the current build fails as well. Other changes since the last pass (b8fddcd) had already moved the
  realisation; a fixed seed at 41 counts is a coin with a 27 % bias.
- This is not a physics regression: the expected Co-60 window counts at 20 µCi are 1.98 × 20 = 39.6, against the 41
  observed.

## DC-2 — fix and criterion

**Criterion.** The scenario asserts that the Co-60 channel's found peak has x > 0 and the Cs-137 channel's has x < 0.
- **Sufficient condition for each:** the reconstruction at the grid node nearest the source, k₀, exceeds every grid
  point θ on the wrong side.
- **Moments:** for D(θ) = recon(k₀) − recon(θ) = Σᵢ nᵢ (Gᵢ[k₀] − Gᵢ[θ]) with Poisson nᵢ of mean λᵢ, the moments are
  exact: E = Σ λᵢ dᵢ and Var = Σ λᵢ dᵢ².
  - The Gᵢ are Studio's projection weights, read off `CorrelationSearch` for the scene's own config.
  - The λᵢ come from the measured window shapes.
- **Bound:** P(fail) ≤ Σ over θ of Φ(−E/√Var) (Bonferroni, Gaussian tails).
- **Requirement:** ≤ α = 10⁻³ per channel. The test is fixed-seed and deterministic, so α is the chance that a change
  of realisation (a new seed path, a refactor) flips a correct product to red.

| Co-60 activity | Co-60 window N | Co-60 bound | Cs-137 window (Cs + Co downscatter) | Cs-137 bound (peak at x ≥ 0) |
|---|---|---|---|---|
| 20 µCi | 40 | 1.0 × 10² (vacuous) | 1,230 | 6.1 × 10⁻¹⁰ |
| 200 µCi | 396 | 5.0 × 10⁻² | 1,396 | 1.3 × 10⁻⁷ |
| 300 µCi | 594 | 2.2 × 10⁻³ | 1,489 | 1.5 × 10⁻⁶ |
| **400 µCi** | **792** | **1.3 × 10⁻⁴** | **1,581** | **1.3 × 10⁻⁵** |
| 500 µCi | 990 | 8.6 × 10⁻⁶ | 1,674 | 8.8 × 10⁻⁵ |

- **Choice:** 400 µCi, the smallest round activity meeting α for both channels; 300 µCi misses it.
- **Conservatism:** the bound is conservative. Empirically, 410 counts already fail only 0.22 %, where the bound gives
  about 4 %.
- **Rate uncertainty:** the rate rests on 39,614 counts (0.5 % relative SE).
- **Ambient term:** the criterion needs none. The default field puts 0 counts in either window in 60 s, against an
  expected total of 12 events at all energies.
- **Environment:** the scenario keeps the default environment, so it tests what the user sees. The alternative, setting
  the ambient field off, would not have fixed it, since the cause is the count level.

**Change.**
- `WorkspaceScenarioTests.TwoIsotopes(ui, coActivityUCi = "20")`; `Imaging_ChannelAndStrip_MatchRetainedFloodAndFoundPeaks`
  passes `SeparatedCoActivityUCi = "400"`.
- The comment states the criterion. No assertion was loosened or removed. The strip oracle, the half-cell peak checks
  and the 2-peak All check are unchanged.
- `PolishSurveyTests` keeps 20 µCi, its documented diagnostic scene, which has no side assertion.

## DC-3 — MLEM desktop scenarios (`tests/Gcam.Studio.UiTests/MlemScenarioTests.cs`)

Every acquisition fixes seed 12345 and every scenario starts a fresh owned process (`Scenario.Run`).
`GCAM_UI_BREAK_VERDICT=1` corrupts one expectation in each, so a broken run must fail all six.

**Support:**
- **Product test hook.** `AutomationEvidence` (Imaging) now also returns `Method`, `ReconstructionUnit`,
  `ReconstructionNote`, `Summary` and `EffectiveCounts`. These are read-only and appear only in a harness-owned process
  (`GCAM_UIA_RUN`), as before.
- **Oracle and harness.**
  - `Verdict.ParseReadoutUnit` reads the heatmap readout's unit ("counts", "(decoded)", "(MLEM λ)"), with a new plain
    oracle test, `FloodOracleTests.ReadoutUnit_NamesTheImageKind_IncludingMlem`.
  - `StudioWindow.SelectedItem` reads back a selector's selection through `SelectionPattern`.

| Scenario | What it asserts | Oracle | Tolerance and derivation |
|---|---|---|---|
| `Mlem_SelectedDuringAcquisition_NonNegativeWithUnitNoteAndAdvancingStatus` | Default item "Cross-correlation" (MD-3), no note. Switching to "MLEM (pixel-area, 400 iterations)" while acquiring (600 s at ×1, Cs-137 channel) gives an MLEM decode with every λ ≥ 0. The readout unit is "(MLEM λ)" and the note says "400 iterations" and "≈50%" but not "not measured". Status counts keep rising while Acquiring. After Stop, the found peak and the chip agree with the λ maximum. Switching back gives "(decoded)" and an empty note | spec strings (MD-3 / MD-10 / SR-IMG-07); hovered readout parsed by `Verdict`; non-negativity of the multiplicative update; argmax of the bound image | λ ≥ 0 exact. Peak within half a grid cell, and the chip half a cell + 0.051 mm (text rounding), the existing `AssertPeakAtMaximum` derivation. Status advance within 10 s at 34 cps in the Cs window |
| `Mlem_WithStrip_ReportsNetCountsAndFindsCsOnItsSide` | Pair scene at 400 µCi, Cs-137 channel, strip on. Cross-correlation: count = Σ max(0, low − R·high), labelled "counts (clipped strip sum)". MLEM: λ ≥ 0; the displayed flood equals the clipped strip flood; count = Σ low − R·Σ high, labelled "net counts (Σ low − R·Σ high)", and the summary number equals it rounded; net < raw; peak at maximum; Cs-137 peak at x < 0 | raw low / high floods and the calibration counts from the product's evidence, recomputed here (as the existing strip scenario); spec labels (MD-11) | Counts to 6 decimals: the service sums R·highᵢ per pixel and the oracle multiplies the total (~10⁻¹² relative). The Cs side rests on a **measurement**, not a derived bound (TODO-36 MD-6: MLEM + b within one element of Cs in ≥ 99.9 % at 1000 Cs counts, Co:Cs ≤ 4; here ~1,590 Cs counts, Co:Cs 0.8). Headless at seed 12345: −21.19 mm, 1,210 net counts |
| `Mlem_FocalPlane1500_FlagsTheUnmeasuredIterationCount` | MLEM at 1000 mm: no "not measured". At 1500 mm: the step ratio is exactly 1.5, λ ≥ 0, and the note says "Iteration count not measured for these optics.". Back at 1000 mm: the flag is gone | MD-4 (the count was selected at the default optics and the 1000 mm plane only); step = cell·F / (4D) | ratio to 10⁻¹² (both steps well above the 0.2 mm floor) |
| `MethodSwitch_KeepsMeasurementsOnTheSameGrid` | An angle and an ROI on the reconstruction under cross-correlation. After switching to MLEM both rows remain (M1 Angle Recon, M2 ROI Recon), the angle text is identical and within its oracle, the ROI pixel count is unchanged, and the MLEM ROI Σ ≥ 0 | angle from the three screen points (`Verdict.AngleDeg`); same grid gives identical geometry; λ ≥ 0 | the existing `AngleToleranceDeg` (one diagonal pixel per click + 0.05°); pixel count exact |
| `FocusSweep_UnderMlem_IsTheCrossCorrelationSweep` | Sweep under cross-correlation, switch to MLEM, sweep again (a fresh result is recognised by its own worker time). Tracks are identical; each interval equals the half-max oracle | the cross-correlation sweep of the same retained flood (SR-IMG-07: the sweep always cross-correlates); `WorkspaceOracle.HalfMax` | exact JSON equality; interval to 7 decimals (as the existing sweep scenario) |
| `MlemSelection_SurvivesStopContinueAndReset_WithoutAStaleImage` | MLEM chosen while acquiring. After Stop, after Continue → Stop, and after Reset → a new 60 s acquisition, the selection is still MLEM (read back from the selector), λ ≥ 0, and the All flood sums to the status counts. After Reset there is no image | each list-mode event adds one count to the acquisition flood, which All shows; the status line counts the events | exact count equality, once the worker is idle on that snapshot |

## Files

| Group | File | Change |
|---|---|---|
| UI tests | `tests/Gcam.Studio.UiTests/WorkspaceScenarioTests.cs` | `SeparatedCoActivityUCi` with its criterion; `TwoIsotopes` takes the Co activity; the Imaging scenario uses 400 µCi; three helpers made `internal` for reuse |
| UI tests | `tests/Gcam.Studio.UiTests/MlemScenarioTests.cs` (new) | six desktop scenarios |
| UI tests | `tests/Gcam.Studio.UiTests/Harness/FloodOracle.cs` | `Verdict.ParseReadoutUnit` |
| UI tests | `tests/Gcam.Studio.UiTests/Harness/StudioWindow.cs` | `SelectedItem` |
| UI tests | `tests/Gcam.Studio.UiTests/FloodOracleTests.cs` | `ReadoutUnit_NamesTheImageKind_IncludingMlem` (plain, always runs) |
| Product (test hook) | `src/Gcam.Studio/Controls/AutomationEvidence.cs` | Imaging evidence: `Method`, `ReconstructionUnit`, `ReconstructionNote`, `Summary`, `EffectiveCounts` (harness-owned process only) |

## Tests

- `DOTNET_CLI_UI_LANGUAGE=en dotnet build Gcam.sln -c Release`: 0 errors, the 2 pre-existing xUnit analyser warnings.
- `DOTNET_CLI_UI_LANGUAGE=en dotnet test Gcam.sln -c Release --no-build`, passed / skipped / total:

| Assembly | Before (TODO-36 final) | After |
|---|---|---|
| Gcam.Tests | 465 / 0 / 465 | 465 / 0 / 465 |
| Gcam.Studio.Tests | 208 / 0 / 208 | 208 / 0 / 208 |
| Gcam.Studio.Services.Tests | 95 / 7 / 102 | 95 / 7 / 102 |
| Gcam.Studio.UiTests | 13 / 14 / 27 | **14 / 20 / 34**: +1 oracle test (runs); +6 desktop scenarios reported as **skipped** (no `GCAM_UI_TESTS`) |

- Render snapshots (`GCAM_RENDER_SNAPSHOTS=1`, output `%TEMP%\gcam-todo38
ender`): 1 / 0 / 1 passed; nothing in the
  rendered UI changed in this turn.

## Desktop command for the planner

In a worktree at this change, with the desktop free (PowerShell):

```powershell
dotnet build Gcam.sln -c Release
$env:GCAM_UI_TESTS = '1'
dotnet test tests/Gcam.Studio.UiTests -c Release --no-build --logger 'console;verbosity=detailed'
# verdict check (must fail all 18 desktop scenarios at their corrupted expectations), then a recovery run:
$env:GCAM_UI_BREAK_VERDICT = '1'
dotnet test tests/Gcam.Studio.UiTests -c Release --no-build --filter 'FullyQualifiedName~MlemScenarioTests|FullyQualifiedName~Imaging_ChannelAndStrip'
Remove-Item Env:GCAM_UI_BREAK_VERDICT
dotnet test tests/Gcam.Studio.UiTests -c Release --no-build --filter 'FullyQualifiedName~MlemScenarioTests|FullyQualifiedName~Imaging_ChannelAndStrip'
Remove-Item Env:GCAM_UI_TESTS
```

Expected:
- Normal run: 34 total, 34 passed (14 oracle + 18 desktop scenarios + plot gate + survey).
- Broken-verdict run of the filtered set: all 7 fail at their corrupted assertions.
- The MLEM scenarios take longer than the CC ones:
  - each MLEM refresh is about 0.15 s per channel;
  - the long 600 s acquisitions are stopped early by the scenarios.

## Deviations

1. **The Cs-side assertion of the MLEM + strip scenario rests on TODO-36's MD-6 measurement**, not on a derived bound.
   A bound like DC-2's would need MLEM's sampling distribution, which has no closed form. The headless realisation at
   seed 12345 is −21.19 mm against the source at −20 mm.
2. **The survey keeps 20 µCi.** A future survey run will still show a low-count Co-60 channel, which is diagnostic only.
   AGENTS.UiAutomation describes the pair scene as 20 µCi; the planner may want to add that the channel scenario now
   uses 400 µCi.
3. **Not covered: keyboard-only reachability of the selector, and a both-themes survey of it.** Neither is cheap in the
   existing pattern. The offscreen renders (both themes) show the selector and note.
4. **One product change, the evidence hook.** It is the allowed kind (read-only, harness-owned process only).

## What could not be run

- The desktop scenarios themselves (DC-4).
- The "fresh sweep" detection relies on two sweeps having different worker times (`ProcessingTime`). This is
  practically certain at 100 ns resolution, but it is only confirmed on the desktop.

## Commands that wrote anything

- Scratch probes under `%TEMP%\gcam-todo38\probe` and `probe2`, built with `dotnet build -c Release`. They reference the
  repository's engine and Studio projects, whose Release outputs were refreshed. They were run in the foreground, with
  outputs `dc1-co20.txt`, `dc1-co400.txt`, `dc2-criterion.txt`, `dc2-criterion-cs.txt` and `dc3-strip-check.txt`.
- `DOTNET_CLI_UI_LANGUAGE=en dotnet build tests/Gcam.Studio.UiTests -c Release`.
- One background run: `dotnet build Gcam.sln -c Release`, `dotnet test Gcam.sln -c Release --no-build`, then the
  render snapshots with `GCAM_RENDER_SNAPSHOTS=1 GCAM_RENDER_OUTPUT=%TEMP%\gcam-todo38\render`.
  - Logs: `%TEMP%\gcam-todo38\{build,test,render}.txt`.
  - `docs/assets` was not touched.
- Repository edits: the files above and this report.

## Turn 2 — the two failing MLEM scenarios (planner's desktop run at f48b6af + turn 1)

### What the desktop runs showed

| Run | Scenario | Result | Where it failed |
|---|---|---|---|
| `20261007-132419-8d8e19` (full) | `Mlem_SelectedDuringAcquisition…` | fail | `MlemScenarioTests.cs:81`, the MLEM wait just after the switch during acquisition: "timed out after 30 s" |
| `20261007-132302-9bae54` (full) | `MlemSelection_Survives…` | pass | — |
| `20261007-132506-d355b3` (broken) | `MlemSelection_Survives…` | fail by timeout | `:255`, the same live MLEM wait. It never reached its corrupted verdict |
| `20261007-132645-808aa1` (recovery) | `MlemSelection_Survives…` | fail | `:255`, the same live MLEM wait |
| `20261007-132755-712d5f` (recovery) | `Mlem_SelectedDuringAcquisition…` | fail | `:47` via `:77`: "readout without a unit: ''", the CC readout read during acquisition |

Every failure sits at a wait or a read during a live acquisition. None occurred after Stop or completion.

### Cause — test timing, plus two product defects it exposed

**Reproduction.** I reproduced the switch-during-acquisition path headless (`%TEMP%\gcam-todo38\probe3`):
- `MainViewModel` with the real `SimulationService` / `SpectrumService` / `ImagingService`, seed 12345, 600 s at ×1,
  Cs-137 channel;
- the switch to MLEM after 2 s;
- logging of every published view (and the method that decoded it) and every `IsProcessing` false edge.

Log: `t2-switch-log.txt`.
- **Cross-correlation phase:** a view every ~0.25 s (one per snapshot). The worker is idle for most of each tick, since
  the decode takes ~40 ms.
- **After the switch:**
  - The first MLEM request takes 954 ms, because it builds the matrix; the following ones take 386–561 ms.
  - Each takes longer than the 0.25 s tick, so the next snapshot is already waiting and the worker runs back to back.
  - `IsProcessing` went false 11 times in 6 s, each only for the instant between a publication and the next request.
  - The first MLEM image was published 1.34 s after the switch.

**1. Test timing (the timeouts).** `Decoded` required `IsProcessing == false` together with the method. During an MLEM
acquisition that state lasts an instant, and UIA polling at ≥ 50 ms (each poll serialises the evidence JSON) cannot
observe it. Cross-correlation idles ~200 ms per tick, so the CC waits passed. `MlemSelection_Survives…` passed once by
catching an instant. The waits were not too short; they waited for a state the product does not hold while acquiring
under MLEM.

**2. Product defect: the label described the selector, not the displayed image.** `ReconstructionUnit`, the strip
count label, and the evidence's `Method` followed the selector at once. The pane kept showing the previous
cross-correlation image until the worker published the MLEM one (1.34 s here, up to about 1 s per later refresh). For
that time:
- the CC image, which has negative values, was labelled "(MLEM λ)";
- a CC count was labelled "net counts".

A test that keyed on the selector could even have judged a CC image as MLEM.

**3. Product defect: every image refresh cleared the hovered readout.** `HeatmapView.OnImageChanged` dropped the hover on
any new image. In a live acquisition the reconstruction is replaced four times a second, so a user holding the pointer
still saw the readout vanish at each refresh. The test, re-reading the readout after its wait, met an empty string
(`712d5f`). A pointer resting on the image never re-creates the hover by itself, because a cursor that does not move
raises no mouse-move event. For a refresh of the same size the viewport is unchanged and the hovered pixel is still
under the pointer.

### Fix

**Product.**
- `ImagingView.Method` records the method that decoded the view; `ImagingService` sets it on every return path.
- `ImagingWorkspaceViewModel.DisplayedMethod` = the published view's method, or cross-correlation for the acquisition's
  own image.
- `ReconstructionUnit` and the strip count label follow `DisplayedMethod`. The selector and the caveat note still follow
  the setting at once.
- `HeatmapView` keeps the hover across a refresh of the same size and re-reads its value. It also re-reads when
  `OriginMm` / `StepMm` change, for a re-gridded projection of the same size. A new size refits the viewport and drops
  the hover, as before.
- The evidence hook adds `DisplayedMethod`.

**Tests.**
- `Decoded(ui, method, also, idle)` keys on `DisplayedMethod` (the image shown was decoded by that method). It requires
  the worker idle only when `idle` is true, which is used only after Stop or completion, where the worker drains and
  stays idle.
- The two live waits pass `idle: false`. Their derivation, from the measured refresh times above, is in the method's
  remarks.
- `ReadoutUnit` judges exactly the string that satisfied its wait (one read).
- No timeout was lengthened.
- **Broken-verdict mode in `MlemSelection_Survives…`:** with the live wait fixed, the scenario reaches its first
  corrupted verdict, `AssertNotStale` after Stop (expected counts + 1).

**Regression tests (headless).**
- `ImagingWorkspaceTests.ReconstructionUnit_DescribesTheDisplayedImage_UntilTheReDecodedViewArrives` (Studio.Tests):
  with the MLEM request held in flight, the unit stays "(decoded)" and the count label "counts (clipped strip sum)" while
  the note already shows. On publication they become "(MLEM λ)" and "net counts (Σ low − R·Σ high)".
- `ImagingServiceTests.Mlem_BuildsOneMatrixPerGeometryAndLine_AndReusesItAcrossRefreshes` (Services): additionally, each
  view's `Method` equals the requested method.
- `HeatmapReadoutChecks.CheckHeatmapReadoutAcrossRefresh` (RenderTests, offscreen, inside the opt-in render case). Exact
  readout strings:
  - "x 0.5 mm, y 0.5 mm · 155 (MLEM λ)" before;
  - "… · 255 (MLEM λ)" after a same-size refresh;
  - "x 5.5 mm, y 5.5 mm · 255 (MLEM λ)" after a step change;
  - empty after a size change.

  This is the only WPF-hosting test project; it runs offscreen with `GCAM_RENDER_SNAPSHOTS=1`.

### Turn-2 files

| Group | File |
|---|---|
| Product | `src/Gcam.Studio.Core/Services/ImagingView.cs` (`Method`); `src/Gcam.Studio.Services/ImagingService.cs` (sets it); `src/Gcam.Studio.Core/ViewModels/ImagingWorkspaceViewModel.cs` (`DisplayedMethod`; unit and count label follow it); `src/Gcam.Studio/Controls/HeatmapView.cs` (hover kept on same-size refresh; re-read on origin / step change); `src/Gcam.Studio/Controls/AutomationEvidence.cs` (`DisplayedMethod`) |
| Tests | `tests/Gcam.Studio.UiTests/MlemScenarioTests.cs` (wait on the displayed method; idle only after Stop; one-read readout); `tests/Gcam.Studio.Tests/ImagingWorkspaceTests.cs` (+1 test; the fake records the method); `tests/Gcam.Studio.Services.Tests/ImagingServiceTests.cs` (method assertions); `tests/Gcam.Studio.RenderTests/HeatmapReadoutChecks.cs` (new), `PlotViewRenderTests.cs` (calls it), `MainWindowRenderTests.cs` (the fixture records the method) |

### Turn-2 verification

- `DOTNET_CLI_UI_LANGUAGE=en dotnet build Gcam.sln -c Release`: 0 errors. The first build showed the 2 pre-existing
  xUnit analyser warnings; the incremental final build showed none.
- `DOTNET_CLI_UI_LANGUAGE=en dotnet test Gcam.sln -c Release --no-build`, passed / skipped / total:

  | Assembly | Turn 1 | Turn 2 |
  |---|---|---|
  | Gcam.Tests | 465 / 0 / 465 | 465 / 0 / 465 |
  | Gcam.Studio.Tests | 208 / 0 / 208 | **209 / 0 / 209** (+1 regression test) |
  | Gcam.Studio.Services.Tests | 95 / 7 / 102 | 95 / 7 / 102 (method assertions added to an existing test) |
  | Gcam.Studio.UiTests | 14 / 20 / 34 | 14 / 20 / 34 (desktop cases skipped) |

  My first full run had the new Studio test fail: it checked the summary's count label while the request was in flight,
  when the summary reads "Building channels…". I removed that check; the label is checked after publication. The re-run
  is green.
- Render snapshots (`GCAM_RENDER_SNAPSHOTS=1`, output `%TEMP%\gcam-todo38ender2`): 1 / 0 / 1 passed, including the new
  `CheckHeatmapReadoutAcrossRefresh`.

### Desktop re-run command

The same worktree procedure as before (PowerShell, desktop free):

```powershell
dotnet build Gcam.sln -c Release
$env:GCAM_UI_TESTS = '1'
dotnet test tests/Gcam.Studio.UiTests -c Release --no-build --logger 'console;verbosity=detailed'
$env:GCAM_UI_BREAK_VERDICT = '1'
dotnet test tests/Gcam.Studio.UiTests -c Release --no-build --filter 'FullyQualifiedName~MlemScenarioTests|FullyQualifiedName~Imaging_ChannelAndStrip'
Remove-Item Env:GCAM_UI_BREAK_VERDICT
dotnet test tests/Gcam.Studio.UiTests -c Release --no-build --filter 'FullyQualifiedName~MlemScenarioTests|FullyQualifiedName~Imaging_ChannelAndStrip'
Remove-Item Env:GCAM_UI_TESTS
```

Expected:
- Full run: 34 / 34.
- Broken run: 7 / 7 fail, each at its corrupted assertion. For `MlemSelection_Survives…` that is `AssertNotStale`
  after Stop; check the manifests' error lines.
- Recovery run: 7 / 7 pass.

### Turn-2 commands that wrote anything

- `%TEMP%\gcam-todo38\probe3` (build and one foreground run; log `t2-switch-log.txt`) and `readman.py` (reads the
  planner's manifests, read-only).
- `dotnet build src/Gcam.Studio -c Release`.
- One background run: `dotnet build Gcam.sln -c Release`, `dotnet test Gcam.sln -c Release --no-build`, and the render
  snapshots to `%TEMP%\gcam-todo38\render2`. Logs: `build2.txt`, `test2.txt`, `render2.txt`.
- The repository edits listed above.
