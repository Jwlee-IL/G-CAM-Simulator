# TODO-30 baseline — turn 10 (AB-15: GCAM Studio default)

2026-10-04. **This turn was done by a substitute Claude implementer (Claude subagent), not by Codex.** Specification:
`docs/PLAN.Physics.AmbientBackground.md`, decision AB-15 (with AB-5, AB-6, AB-8). Base: HEAD `cf469aa`, clean tree. No
docs / PLAN / Todo / VV / README / PAPER / CLAUDE / AGENTS edits, no engine change, no git state change. No desktop UI
was launched; render snapshots were offscreen.

## 1. What changed

| Group | File | Change |
|---|---|---|
| Studio.Core | `src/Gcam.Studio.Core/Services/AmbientPreset.cs` (new) | `AmbientPreset(Id, Name, FileName, Sha256)`; `TerrestrialUnscear2000V1` pinned to `4d48bc42…3f4`; `All` = that preset only (mono662 is not in the list); `Field(dose, bound)` builds an `AmbientFieldConfig` **by reference** (`SpectrumFile`, `SpectrumFileSha256`, `RequireValidatedSpectrum = true`) — no I/O in Core |
| Studio.Core | `src/Gcam.Studio.Core/ViewModels/MainViewModel.cs` | default `AmbientDoseRateMicroSvPerHour = 0.10` (`DefaultAmbientDoseRateMicroSvPerHour`), default `AmbientGeometry = FrontOnlyThroughMask`; `AmbientPreset` property; `AmbientPresetName` = "Terrestrial UNSCEAR 2000 v1 — validated"; `AmbientEnvironmentLabel` = "ideal environment" at 0, otherwise "terrestrial-unscear2000-v1 · front-only bound" / "… · bare-crystal bound" (re-raised on a bound change); Start passes `AmbientPreset.Field(dose, bound)`. Zero still takes the unchanged `Start(...)` path |
| Studio.Services | `src/Gcam.Studio.Services/SimulationService.cs` | after the existing freeze (`config.Clone()`), `ConfigLoader.ResolveAmbientSpectrum(config, AmbientSpectrumDirectory)`; `AmbientSpectrumDirectory = <AppContext.BaseDirectory>/ambient` |
| Studio.Services | `src/Gcam.Studio.Services/Gcam.Studio.Services.csproj` | `samples/ambient/terrestrial-unscear2000-v1.json` linked as `ambient/terrestrial-unscear2000-v1.json`, copy to output and publish (`PreserveNewest`) |
| Studio (view) | `src/Gcam.Studio/Views/MainWindow.xaml` | `TextWrapping="Wrap"` on the environment label (the first render showed it trimmed to "…front-only…" at 1280 × 800) |
| Tests | see §3 | |

**How Studio finds and checks the spectrum.** The file is deployed byte-for-byte next to the assemblies by MSBuild
(it appears in the output of Gcam.Studio, Gcam.Studio.Services.Tests, Gcam.Studio.RenderTests and Gcam.Studio.UiTests
through the project references). At every Start with a non-zero field, `SimulationService` resolves the reference with
the engine's own `ConfigLoader.ResolveAmbientSpectrum` → `IncidentSpectrumFile.Load`, which checks the file bytes
against the SHA-256 pinned **in code** (not the deployed sidecar, which would only certify itself) and the payload
against its content hash; `AmbientPhotonProcess` then enforces `RequireValidatedSpectrum` (validated, no
"NOT-VALIDATED" id, content hash, acceptance record). A missing file or wrong pin makes Start fail with the engine's
message (Failed state, SR-RUN-27); the placeholder can never stand in, because an unresolved reference is refused by
the engine. Why copy rather than embed: it reuses the engine's file-plus-pin loader unchanged (embedding would need a
new stream overload in Gcam.Configuration or a temp file), keeps the exact pinned bytes visible beside the program, and
involves no machine path. Cost: the 1.9 MB file is read, hashed, parsed and cloned on the UI thread inside Start —
measured 30–53 ms per Start after the first (first 151 ms, JIT); an ideal-environment Start is 0.1 ms. A cache was not
added, so every Start re-verifies the bytes.

## 2. Compatibility (AB-8)

With the field at 0 the ViewModel calls the unchanged `IAcquisitionService.Start(...)`; `BuildConfig` only resolves
inside the existing `ambient is not null` branch, and the source-free imaging call (`new AmbientFieldConfig()`, no file)
is a no-op there. No engine file changed. The ambient-null / zero-BSR tests (`ListModeBackgroundTests`,
`AcquisitionContinuationTests`, `AmbientAcquisitionTests`, engine `AmbientFieldTests`) pass unchanged. No physics golden
value changed.

## 3. Tests

Build `dotnet build Gcam.sln -c Release`: **0 errors, 0 warnings** on the final incremental build; a full recompile of
Gcam.Studio.Services.Tests shows the 2 pre-existing xUnit analyzer warnings (`ImagingServiceTests.cs:106` xUnit2000,
`WaveformServiceTests.cs:118` xUnit2012), none in touched files.

`dotnet test Gcam.sln -c Release --no-build` (GCAM_UI_TESTS=0, GCAM_RENDER_SNAPSHOTS=0, GCAM_EVIDENCE_TESTS=0):

| Assembly | Before | After |
|---|---|---|
| Gcam.Tests (engine) | 393 통과 | 393 통과 |
| Gcam.Studio.Tests (Core) | 184 통과 | **186** 통과 (+2) |
| Gcam.Studio.Services.Tests | 87 통과, 7 건너뜀 | **91** 통과 (+4), 7 건너뜀 |
| Gcam.Studio.UiTests | 13 통과, 14 건너뜀 | 13 통과, 14 건너뜀 |
| Gcam.Studio.RenderTests | 1 건너뜀 | 1 건너뜀 |

0 실패 everywhere. Before the test edits, the new default made 32 Core tests fail: their fakes implement only `Start`,
so Start went to `StartAmbient` and the interface default threw `NotSupportedException`.

**Default under test — updated / added**
- `AmbientViewModelTests.DefaultIsIdealAndSourceFreeStartRequiresAbsoluteField` → replaced by
  `DefaultIsValidatedFrontOnlyField_ZeroIsIdeal_SourceFreeStartRequiresAbsoluteField` (default 0.10, front-only, preset
  name without "not validated", both bound labels, source-free Start enabled by default and disabled at 0, "ideal
  environment" / "off" at 0).
- New `AmbientViewModelTests.PresetListOffersOnlyTheValidatedSpectrum_ByPinnedReference`,
  `Start_DefaultPassesValidatedPresetReference_ZeroTakesTheLegacyPath`.
- New `AmbientPresetTests` (Services): `Pin_EqualsRepositorySidecar_AndDeployedBytesAreTheRepositoryFile`,
  `BuildConfig_ResolvesTheValidatedSpectrum_OnTheFrozenCopyOnly`, `WrongPinOrMissingFile_IsRefusedAtStart`,
  `DefaultField_AcquiresWithTheValidatedSpectrum`.
- `AmbientRenderTests` (`RenderAmbientInputs`): no longer sets 0.1 / front-only; asserts and renders the default.

**Purpose is something else — `AmbientDoseRateMicroSvPerHour = 0` set explicitly, expectations unchanged**
(each uses a fake / fixture acquisition service that implements only the ideal `Start`, or asserts the no-source
Start rule):
- `AcquisitionViewModelTests`: helper `Model(...)` (used by `Start_SnapshotsGrow_StopKeepsDataAndLocks_ContinueAccumulates`,
  `Completed_StartDisabledUntilPresetRaised_LowerPresetRejected_ProgressFollowsPreset`,
  `LiveTimeAndSpeed_LockedWhileAcquiring_InvalidValuesFallBack`, `Failure_WithData_KeepsItLocked_ResetOnly`,
  `Failure_WithoutData_BehavesAsEmpty`), and the inline models of `Start_CapturesDetectorInputs_AndLocksThemWhileDataExist`,
  `Reset_DiscardsData_UnlocksInputs_NextStartIsANewAcquisitionWithANewSeed`,
  `Spectrum_SnapshotsGrow_ViewSettingsReuseAcquisition_SelectionSurvives`.
- `DetectorWorkspaceTests.Model(...)` helper — used by `DetectorWorkspaceTests.Face_FollowsPendingBeforeStart_AcquiredAndLockedAfter_PendingAgainAfterReset`
  and the `FocusSweepViewModelTests` (`Sweep_FreezesIdentity_UserK_AndNeverDelaysAcquisition`,
  `Sweep_RejectsLateResult_WhenIdentityChanges` × 6, `Sweep_DoesNotHoldSnapshotConsumption_AndLabelsFrozenPrefix`);
  the other helper callers (`Gap_InvalidEditPreventsStart`, `Gap_RevalidatesAfterPitchChange_AndConvertsUnits`,
  `Gap_ZeroAllowsPitchBelowOldDefaultGap`) never reach the service and passed either way.
- `ImagingWorkspaceTests`: `FocusSweep_ChannelChangeCancelsAndRejectsLateResult`, `PeakChip_CountsSeveralFoundPeaks_AndNamesASingleOne`,
  `SharedWindow_SelectorStripAndRoi_ReuseFrozenAcquisition`, `LateResponse_CannotReplaceNewerWindowResult`.
- `OpticsViewModelTests`: `Focus_AcquiringStoppedCompleted_KeepsEventsAndSpectrum_OpticsLockedWithData`,
  `LatestFocus_CoalescesChangesAndRejectsLateSnapshotProjection`, `OldAcquisitionWorker_CannotPublishIntoNewAcquisition`.
- `WaveformWorkspaceTests`: `CsI_IsOfferedAndRequiresResetForANewAcquisition`, `Chain_LockedWithData_ScopeUsesAcquiredChain`,
  `ActiveAcquisition_DisablesPhysicalChainChanges`, `HiddenScope_DoesNotGenerateAndHeldTriggerReusesWindow`,
  `LatestSelectionWinsAndNewAcquisitionCancelsOldScope`, `ScopeFailure_IsVisibleAndLocalControlsKeepTheAcquisition`.
- `MainViewModelTests.RemoveAll_DisablesRemoveAndStart` — the rule "no source and no field → Start disabled".
- Render fixtures (opt-in): `MainWindowRenderTests.RenderWindows`, `DetectorFocusRenderTests` (both models),
  `WaveformRenderTests` — their fixture services implement only `Start` and draw synthetic snapshots; their PNGs
  therefore show the ideal-environment label, not Studio's startup default.

## 4. The startup scene under the default field

Headless probe (in `%TEMP%`, not committed) driving the Studio services exactly as the ViewModel does: Cs-137 500 µCi
at (0, 0, 1000 mm), default optics (rank 13, 0.7 mm, D 80 mm, 30 × 0.6 mm) focused at 1000 mm, default detector, 60 s,
`SimulationService` → `SpectrumService` (N = 1.5) → `ImagingService`. Seeds 1–8 for the scene (same seed in each case);
ambient-only rates from source-free 600 s acquisitions, seeds 101–104.

| Quantity | Ideal (0) | **Default: 0.10 µSv/h, front-only** | 0.10 µSv/h, bare (reference) |
|---|---|---|---|
| Detected ambient rate (source-free, 4 × 600 s) | 0 | **0.137 ± 0.008 cps** (329 counts) | 30.44 ± 0.11 cps |
| … in the Cs-137 662 window (616–707 keV) | 0 | 0.0063 ± 0.0016 cps | 0.214 ± 0.009 cps |
| Source rate (engine expected) | 73.2 cps | 73.2 cps | 73.2 cps |
| Ambient share of all counts | 0 | **0.19 %** (≈ 8 of ~4390 counts in 60 s) | 29 % |
| Ambient share in the 662 window | 0 | **0.03 %** (≈ 0.4 of ~1340 counts) | ≈ 1 % |
| Counts in 60 s, mean ± sd (8 seeds) | 4380 ± 49 | 4389 ± 52 | 6243 ± 59 |
| Found Cs-137 peaks / radial offset from truth | 1 / 0.43 mm mean (max 0.71) | 1 / 0.40 mm (max 0.63) | 1 / 0.47 mm (max 0.85) |
| Ambient BSR readout | off | ≈ 0.002 × signal | ≈ 0.42 × signal |
| **Spectrum axis (256 bins)** | 0–761 keV, 2.97 keV bins | **0–4555 keV, 17.8 keV bins** | 0–4555 keV |
| 662-band counts (mean) | 1350 | 1336 | 1347 |

**Localisation:** no visible change — one Cs-137 peak in every run, offsets within the seed-to-seed scatter of the
ideal case. **Spectrum: visibly changed, but not by the ambient counts.** With a field on, `AcquisitionSession` sets
`AmbientMaximumEnergyKeV` to the spectrum's highest energy (3960.9 keV, a weak terrestrial line), and `SpectrumService`
draws to 1.15 × that, so the default Spectrum workspace now spans 0–4.6 MeV in 17.8 keV bins instead of 0–761 keV in
3 keV bins: the 662 keV peak occupies a few bins at the left sixth of the plot, and per-bin counts are ~6× higher. The
ambient itself adds ≈ 8 counts per minute, spread over the range. The 1 % lower 662-band count (1336 vs 1350, ~2 SE)
comes from the coarser bins (band membership is by bin centre), not from physics. This is existing turn-4 behaviour
that the new default exposes on every startup; a fix is a view decision for the planner / author (options: keep the
source-line axis and report ambient overflow counts; or clip the axis at the highest energy that has counts; or a
fixed axis per preset) — **not changed here**. The bare bound (selectable) shows a 29 % share, as in EV-34.

## 5. Render snapshots (offscreen, `GCAM_RENDER_SNAPSHOTS=1`, `GCAM_RENDER_OUTPUT=%TEMP%\gcam-turn10\render`)

**Pre-existing failure:** at HEAD `cf469aa` (clean `git archive` copy) the single opt-in render test already fails in
`VerifyEmissionTable` ("Truncated table value: Window (keV)", `MainWindowRenderTests.cs:201`) on this machine, before it
reaches the ambient render. To obtain the snapshots, a scratch copy of the working tree in `%TEMP%\gcam-turn10\work`
turned that one assertion into a log line; the repository test is unchanged. With that, the run passed and wrote 84 PNGs
under `%TEMP%\gcam-turn10\render\` — the relevant ones: `studio-render/ambient-inputs-{dark,light}-{1280x800,1440x900}.png`
(default panel: "0.1 µSv/h", "Terrestrial UNSCEAR 2000 v1 — validated", "terrestrial-unscear2000-v1 · front-only bound"
wrapped on two lines, bound "FrontOnlyThroughMask", "undefined (source-free)"). The fixture-driven imaging / spectrum /
waveform / detector PNGs use synthetic snapshots and the field at 0 (§3), so they do not show the startup default; the
real default-scene spectrum on / off is plotted from the probe in `%TEMP%\gcam-turn10\probe\startup-spectrum-ambient.png`.

## 6. Deviations and open points

- Invalid dose text still falls back to **0 (ideal)**, as before (`InvalidAmbientDoseReturnsToOff`), not to the new 0.10
  default; most other inputs fall back to their default. Author's choice.
- The bound selector still shows the enum names (`FrontOnlyThroughMask`, `BareCrystalAllFaces`); unchanged.
- The Spectrum-axis change of §4 and the 30–150 ms Start cost of §1 are reported, not changed.
- The opt-in render test fails at HEAD on this machine (§5) — independent of this turn.
- Desktop UI tests not run (no desktop). They launch the real Studio, which now starts with the field on.

## 7. Draft V&V text for the planner (docs not edited)

**`docs/VV.Studio.SRS.md`**

- SR-RUN-12 — current: "… gain σ / seed, background ratio, detection chain and the Monte Carlo seed — are editable only
  in Empty …" → proposed: "… gain σ / seed, background ratio, **ambient dose rate and bound**, detection chain and the
  Monte Carlo seed — are editable only in Empty …" (the rest unchanged).
- SR-RUN-13 — current: "Without acquired data, Start is enabled when at least one source exists." → proposed: "Without
  acquired data, Start is enabled when at least one source exists **or the ambient field is non-zero (a source-free,
  background-only acquisition)**." (the rest unchanged).
- New **SR-RUN-29** — proposed: "An absolute ambient field — photon H*(10) in µSv/h, finite ≥ 0, **default 0.10**, with a
  bound, **front-only through the mask (default)** or bare crystal on all faces — adds the engine's source-independent
  ambient photon process using the validated terrestrial spectrum `terrestrial-unscear2000-v1`, the only offered preset.
  The spectrum ships with Studio and is used only if its bytes match the SHA-256 pinned in Studio and the engine accepts
  it as validated with its acceptance record; otherwise Start fails (SR-RUN-27). The panel names the preset and the
  bound; 0 is labelled "ideal environment" and runs the unchanged ideal acquisition, bit for bit. A derived ambient
  BSR (ambient ÷ source detected rate) is shown read-only."
- Input table — new rows after "Background BSR":
  `| Ambient photon H*(10) | µSv/h | finite ≥ 0; default 0.10; invalid UI input → 0 (ideal environment) | SR-RUN-29 |`
  `| Ambient bound | — | front-only through the mask (default) or bare crystal on all faces; locked while data exist | SR-RUN-29 |`

**`docs/VV.Studio.md`** (traceability) — new row:
`| SR-RUN-29 | integration + unit + offscreen | T, I | AmbientViewModelTests (default, preset list, Start reference / zero path, locks, invalid input), AmbientPresetTests (pin = sidecar = deployed bytes, resolution on the frozen copy, wrong pin / missing file refused, default acquisition), AmbientAcquisitionTests (source-free, Stop / Continue, workspaces); AmbientRenderTests offscreen panel | pass headless; desktop pending |`;
SR-RUN-13's evidence adds `AmbientViewModelTests.DefaultIsValidatedFrontOnlyField_ZeroIsIdeal_SourceFreeStartRequiresAbsoluteField`.

**`docs/VV.Studio.SDS.md`**

- SU-01 responsibility — append: "…, the ambient field input (dose rate, bound, preset and environment labels)".
- SU-08 — current: "validate inputs, build the engine config and start an acquisition session" → proposed: "validate
  inputs, build the engine config (resolving the ambient preset reference from the deployed `ambient` folder through the
  engine loader) and start an acquisition session".
- New unit row (next free id **SU-26**): `| SU-26 | SI-1 | AmbientPreset | Core/Services/AmbientPreset.cs | the offered ambient spectra as hash-pinned references (file name + SHA-256); builds the field config; no I/O |`
- Paragraph after "… Nuclear emissions remain independent singles." (~line 268) — proposed: "An absolute ambient field
  (SR-RUN-29) is a separate, source-independent process: dose rate and bound plus the preset reference are frozen at
  Start; SU-08 resolves the reference from `<application>/ambient/` with the engine's `ConfigLoader.ResolveAmbientSpectrum`,
  which checks the bytes against the SHA-256 pinned in SU-26, and the engine refuses a spectrum that is not validated.
  The session then advances by fixed live-time targets, so empty intervals progress and a source-free acquisition is
  possible. A field of 0 takes the ideal path and consumes no RNG draws."
- Trace table — add `| SR-RUN-29 | SU-01, SU-26, SU-08, SU-19, engine AmbientPhotonProcess |`.

**`docs/VV.Studio.Acquisition.md`** (~line 145)

- Current: "The ambient energy response matches the existing unmasked cosine-flux crystal model; pixel placement matches
  the uniform detected pedestal. BSR is defined after detection, …" → proposed first words: "**The BSR background's**
  energy response matches …" (it describes the relative BSR model, not the absolute field).
- New paragraph after it — proposed: "**Absolute ambient field (default).** Studio starts with 0.10 µSv/h photon H*(10),
  validated terrestrial spectrum, front-only bound. In the startup scene (Cs-137 500 µCi at 1 m, default optics and
  detector, 60 s) the field adds 0.137 ± 0.008 cps (4 × 600 s source-free) against 73 cps from the source — 0.19 % of
  the counts, 0.03 % of the 662 keV window; localisation is unchanged within the seed scatter (8 seeds). The bare-crystal
  bound gives 30.4 ± 0.1 cps (29 %). With any field on, the spectrum axis extends to 1.15 × the spectrum's highest
  energy (3961 keV), so its 256 bins are 17.8 keV wide instead of 3 keV." (Superseded by turn 11 — see T11.6.)

---

# Turn 11 (AB-16: fixed 0–2000 keV spectrum axis with overflow; pattern-validated dose input)

2026-10-04, **substitute Claude implementer** again, building on the uncommitted turn-10 tree; specification AB-16
(`6ac21c0`). No docs, git or engine changes and no desktop.

## T11.1 What changed

| File | Change |
|---|---|
| `src/Gcam.Studio.Services/SpectrumService.cs` | `AxisMaximumKeV = 2000`, `BinWidthKeV = 2`, `BinCount = 1000`. The axis no longer follows the line list, `IncidentMaximumEnergyKeV` or pile-up; the `1.15 × / 2.15 ×` rule and its cache key are gone. A measured pulse ≥ 2000 keV goes to `OverflowCounts` exactly as before: counted in `TotalCounts`, never dropped |
| `src/Gcam.Studio.Core/Services/SpectrumSettings.cs` | doc comment: `IncidentMaximumEnergyKeV` only admits a source-free spectrum; it does not set the axis |
| `src/Gcam.Studio.Core/ViewModels/SpectrumWorkspaceViewModel.cs` | summary line ends "… · N overflow ≥ 2000 keV" (axis end read from the view's last bin edge); it was "N above plot range" |
| `src/Gcam.Studio.Core/ViewModels/MainViewModel.cs` | new `AmbientDoseText` (the bound text) and `AmbientDoseError`. Pattern `AmbientDosePatternText` = `^\s*(\d+(\.\d*)?|\.\d+)\s*$`: invariant digits with at most one decimal point; no sign, exponent, NaN, ∞, comma or empty entry. A matching entry sets the dose rate. Any other entry is **refused**: the dose rate keeps its previous value, the warning shows, and Start does nothing until it is corrected (the seed idiom). A programmatic invalid double is also refused (previous value kept) instead of falling back to 0. The text follows programmatic values and locks with data |
| `src/Gcam.Studio/Views/MainWindow.xaml` | dose `TextBox` bound to `AmbientDoseText` (`UpdateSourceTrigger=PropertyChanged`, like Gap); a `Text.CaptionWarning` line under it bound to `AmbientDoseError` (AutomationId `Acquisition.AmbientDoseError`), the Gap field's existing invalid-input state. Bound drop-down unchanged |

**Bin width: 2 keV (1000 bins).**
- **Floor:** the 2.97 keV the old rule gave the Cs-only startup scene. 2 keV is finer: the default chain's 30.2 keV FWHM at 662 keV now spans ~15 bins instead of ~10.
- **Narrowest offered peak:** Ba K at 32 keV has a 4.4 keV FWHM with GAGG(Ce) or CsI(Tl) (NaI 5.2, LYSO 5.7, BGO 10.7 keV). 2 keV keeps at least 2 bins per FWHM there (Nyquist); the old rule gave 1.5.
- **Why not 1 keV:** per-bin statistics stay comparable to before (0.67 × the old bin content); 1 keV bins would be noisier.
- **Cost:** for 100,000 events, smear and bin take 6.8–7.4 ms; the pile-up toggle takes 8.4–8.8 ms.

**Consequences stated per AB-16.**
- The Co-60 cascade sum (2505 keV) and pile-up sums above 2000 keV are now overflow: counted, not drawn.
- 662 + 662 keV pile-up sums (1323 keV) and the Co-60 single lines stay on the axis.
- An ideal Cs-only scene also draws 0–2000 keV, so its peak sits in the left third.
- With pile-up the old axis ended at 2.15 × 661.7 = 1423 keV. In `HighRate_PileUpLosesPulsesAndMovesCountsAbovePhotopeak` the overflow drops 423 → 50 and the counts above 794 keV rise 4,885 → 5,265 (same 85,217 pulses).

## T11.2 Every consumer of the spectrum axis / bins

| Consumer | Uses | Status |
|---|---|---|
| `SpectrumService` bands (table counts, shares, union share) | bin centres within E ± N·FWHM | consistent; finer quantisation (Cs + Co union share 36.93 % → 37.065 %) |
| `SpectrumWorkspaceViewModel` series, window markers (`PlotBand`), line-selection zoom (`ViewRange` = band ± width) | bin edges; keV band limits | code unchanged; edges now 0–2000 |
| `PlotView` (default X range from first / last edge, hover bin readout, readout precision from bin width) | edges | automatic: full view 0–2000 keV; readout precision follows 2 keV |
| `SpectrumWorkspaceViewModel.Summary` | overflow, last edge | text updated (above) |
| `ImagingService` energy windows | per-event measured energy against band limits (`BuildBands`), not bins | not affected |
| `WaveformService` / Waveform workspace, Detector workspace | `MeasurementStage` amplitudes; no spectrum bins | not affected |
| `AutomationEvidence` (`CentresKeV`, `Counts`, `Bands` for the UI oracles) and `WorkspaceOracle.BandCount` | bin centres | consistent by construction |
| `AcquisitionSnapshot.AmbientMaximumEnergyKeV` | now only a "field present" flag for source-free spectrum / imaging | unchanged |
| Render fixtures (`MainWindowRenderTests` synthetic 256 / 512-bin spectra, `PlotViewRenderTests`) | their own synthetic bins | not the service; unchanged |
| Measurements / ROI | images only | not affected |
| Engine / CLI histograms | separate code | not affected |

## T11.3 Tests

Build `dotnet build Gcam.sln -c Release`: 0 errors, 0 warnings. The 2 pre-existing xUnit analyzer warnings reappear only
when Gcam.Studio.Services.Tests is fully recompiled.

`dotnet test … --no-build` (UI / render / evidence opt-ins off):

| Assembly | Before (turn 10) | After |
|---|---|---|
| Gcam.Tests | 393 통과 | 393 통과 |
| Gcam.Studio.Tests | 186 통과 | **205** 통과 |
| Gcam.Studio.Services.Tests | 91 통과, 7 건너뜀 | **92** 통과, 7 건너뜀 |
| Gcam.Studio.UiTests | 13 통과, 14 건너뜀 | 13 통과, 14 건너뜀 |
| Gcam.Studio.RenderTests | 1 건너뜀 | 1 건너뜀 |

0 실패. Touched tests:
- `SpectrumServiceTests.CsAcquisition_PhotopeakBinAndFwhmMatchChain` — **the axis rule is under test.** The end
  assertion `1.15 × max line` became `AxisMaximumKeV`, and a bin-width assertion was added. The physics checks are
  unchanged and pass: peak bin 330 = expected 330 at 2.0000 keV; FWHM 30.2798 vs chain 30.2373 keV. Their tolerance
  (2 bins + 5σ) is now 4.6043 keV (6.5492 with 2.97 keV bins).
- `DetectorRealismTests.Gain_WidensPhotopeakInQuadrature_AndReducesTightWindowAcceptance` — its own histogram reused the
  service's old width (`661.7 × 1.15 / BinCount`) with a hard-coded 256-bin array; it now uses `BinWidthKeV` ×
  `BinCount`. Result: FWHM 54.5845 vs quadrature 55.5917 keV, tolerance 4.8482 keV. The quadrature physics is unchanged.
- `WaveformServiceTests.Response_AgreesAcrossWaveformSpectrumAndImaging` (8 cases) — its expected histogram restated the
  old axis rule; it now uses `AxisMaximumKeV`. Its purpose (waveform = spectrum = imaging amplitudes) is unchanged.
- `AmbientViewModelTests.InvalidAmbientDoseReturnsToOff` → `InvalidAmbientDoseIsRefused_PreviousValueStays` — **the
  fall-back rule is under test** (AB-16 replaces "→ 0" by "refused").
- New tests:
  - `SpectrumServiceTests.Axis_IsFixedAt2000keV_ForAnyLinesFieldOrPileUp_AndOverflowKeepsEveryPulse`: Cs / Co / both line
    lists × field maximum none or 3960.9 keV × pile-up off / on. It checks 1000 bins of 2 keV over 0–2000 keV, puts a
    2505.7 keV cascade sum and a 3000 keV pulse in the overflow, checks total = drawn + overflow, and that the histogram
    and the 662 band are identical with and without the field maximum.
  - `MainViewModelTests.SpectrumSummary_NamesTheOverflowAndTheFixedAxisEnd`.
  - `AmbientViewModelTests.DoseTextOutsideThePattern_IsRefused_PreviousValueStays_StartBlocked`, 11 entries: "", " ",
    "-1", "+0.1", "1e-1", "NaN", "Infinity", "abc", "0.1.2", "0,1", ".".
  - `AmbientViewModelTests.DoseTextMatchingThePattern_SetsTheDoseRate`, 6 entries including "0" → ideal environment.
  - `AmbientViewModelTests.DoseText_FollowsProgrammaticValue_AndLocksWithData`.
- `AmbientRenderTests` (opt-in) also renders the refused state (`ambient-inputs-invalid-*`).

No physics golden value changed and no other test was edited.

## T11.4 Startup spectrum on the fixed axis (same probe and seeds as §4)

| | Ideal (0) | Default 0.10 µSv/h front-only | 0.10 µSv/h bare |
|---|---|---|---|
| 662-band count, mean ± sd over 8 seeds (window 616.3–707.1 keV) | **1349.4 ± 20.0** (turn 10: 1349.6) | **1353.8 ± 20.3** (turn 10: 1336.0, on 17.8 keV bins) | 1367.6 ± 19.2 (turn 10: 1347.0) |
| Overflow ≥ 2000 keV, total over 8 × 60 s | 0 | 2 | 42 (5.3 per run) |
| Overflow with pile-up on | identical to singles at this 73 cps rate | same | same |
| Source-free 4 × 600 s: overflow / counts | — | 17 / 329 (5 %) | 185 / 73,066 (0.25 %) |

**The 662 band no longer depends on the field's line list.** For every case and seed, the histogram and the band count
with `IncidentMaximumEnergyKeV` = 3960.9 keV equal those without it (24 of 24 identical).
- **Front-only minus ideal**, paired by seed: +4.4 ± 2.5 (SE) counts. This is compatible with the ≈ 0.4 ambient counts
  expected in the window plus the re-indexed per-event smearing that any inserted ambient event causes. Turn 10's −14
  was the bin-quantisation artefact.
- **Bare:** +18, against ≈ 13 ambient counts expected in the window.
- **Front-only overflow is relatively larger (5 %)** because the tungsten mask removes the soft part of the field.

Plot: `%TEMP%\gcam-turn10\probe\t11\startup-spectrum-fixed-axis.png`.

## T11.5 Render snapshots

Same scratch-copy method as turn 10. In `%TEMP%\gcam-turn10\work` only the pre-existing `VerifyEmissionTable` truncation
assertion was turned into a log line. The repository test is unchanged and still fails at HEAD on this machine with
"Truncated table value: Window (keV)". The run wrote 88 PNGs to `%TEMP%\gcam-turn10\render11\`. The relevant ones:
- `studio-render/ambient-inputs-{dark,light}-{1280x800,1440x900}.png` — the default panel.
- `studio-render/ambient-inputs-invalid-{dark,light}-{1280x800,1440x900}.png` — "-0.1" refused: warning line under the
  field, dose stays 0.1, labels unchanged.

The render fixtures draw synthetic spectra, so the fixed service axis does not appear in these PNGs; the real axis is
in the probe plot above.

## T11.6 Draft V&V text (replaces / extends §7 where they overlap)

**`docs/VV.Studio.SRS.md`**
- SR-SPEC-01 — current: "Spectrum displays a live 256-bin stepped, filled histogram …" → proposed: "Spectrum displays a
  live stepped, filled histogram of the shared acquisition's measured event deposits on a **fixed 0–2000 keV axis in
  1000 bins of 2 keV**, for every scene, ambient field and pile-up setting. A pulse measured at or above 2000 keV is not
  drawn: it is counted as **overflow**, shown in the spectrum summary ("N overflow ≥ 2000 keV") and included in the
  total. It adds no synthetic noise floor or independent event pool." Note in the row or its rationale: Co-60 2505 keV
  cascade sums and pile-up sums above 2000 keV fall into the overflow.
- SR-RUN-29 (the §7 draft) — replace "finite ≥ 0, default 0.10" by: "a dose-rate entry matching
  `^\s*(\d+(\.\d*)?|\.\d+)\s*$` (digits with at most one decimal point), default 0.10; any other entry is refused with a
  warning under the field, the previous dose rate stays and Start does nothing until it is corrected".
- Input table — the ambient row becomes: `| Ambient photon H*(10) | µSv/h | digits with at most one decimal point (no sign, exponent, NaN, ∞); default 0.10; 0 = ideal environment; an invalid entry is refused with a warning, the previous value stays and Start is blocked | SR-RUN-29 |`
- Views table (~line 245) — current: "256-bin stepped histogram, measured energy (keV) / acquired counts; …" → proposed:
  "stepped histogram on a fixed 0–2000 keV axis (2 keV bins) with an overflow count, measured energy (keV) / acquired
  counts; …".

**`docs/VV.Studio.SDS.md`**
- ~line 111 — current: "257 explicit bin edges, 256 centres and acquired counts, …" → proposed: "1001 explicit bin
  edges (0–2000 keV), 1000 centres and acquired counts, …".
- ~line 324 — current: "Axis range is 1.15 × highest emission energy (2.15 × with pile-up); pulses above it are counted
  as overflow." → proposed: "The axis is fixed at 0–2000 keV in 2 keV bins (AB-16), independent of the emission lines,
  the ambient field and pile-up. 2 keV is finer than the 2.97 keV the old line-based axis gave at 662 keV and keeps at
  least 2 bins per FWHM at the narrowest offered peak (Ba K, 4.4 keV). Pulses at or above 2000 keV are counted as
  overflow (Co-60 cascade sum, pile-up sums). `IncidentMaximumEnergyKeV` only admits a source-free spectrum."
- SU-01 — add: "the ambient dose text, its pattern validation and warning".

**`docs/VV.Studio.md`** (spectrum evidence rows, ~lines 286–292)
- Photopeak row → "regional maximum bin 330, containing 661.7 keV; bin width 2.0000 keV".
- FWHM row → "histogram 30.2798 keV vs `FrontEndModel` 30.2373 keV; error 0.0425 keV | ≤ 4.6043 keV = two bins +
  5·FWHM/√(2(N−1))".
- Pile-up row → "100,000 → 85,217 pulses; above 794.04 keV: 0 → 5,265; overflow 50; resolving time 730 ns".
- Cs + Co row → "union share 37.0650 %".
- New row: `Axis_IsFixedAt2000keV_ForAnyLinesFieldOrPileUp_AndOverflowKeepsEveryPulse` | 1000 bins of 2 keV for 3 line
  lists × 2 field maxima × pile-up on/off; 2505.7 and 3000 keV pulses in overflow; histogram and 662 band identical
  with and without the field maximum | exact.
- SR-RUN-29 evidence adds the dose-text tests (`DoseTextOutsideThePattern_…`, `DoseTextMatchingThePattern_…`,
  `DoseText_FollowsProgrammaticValue_AndLocksWithData`, `InvalidAmbientDoseIsRefused_PreviousValueStays`) and the
  offscreen `ambient-inputs-invalid-*` render.

**`docs/VV.Studio.Acquisition.md`**
- ~line 119 — current: "The Cs spectrum has 256 bins of width 2.9725 keV." → proposed: "The spectrum has a fixed
  0–2000 keV axis of 1000 bins, 2 keV wide." The absorber test still passes. Its printed Ba K/662 peak-bin ratios are now
  4.187313 → 2.320998 with the global maximum at 33.0 keV; re-measure that table if the planner wants its band numbers
  on the new bins.
- The §7 "absolute ambient field" paragraph — its last sentence becomes: "On the fixed 0–2000 keV axis the 662 keV band
  count is the same with or without the field's line list (1349 ideal, 1354 default, 8 seeds); the default field adds
  ≈ 0.25 overflow counts per minute."

**`src/Gcam.Studio/README.md`** (folder map, not edited): nothing to add — no new folder or control.

## T11.7 Open points

- Start stays clickable while the dose text is invalid. Clicking it does nothing, as with an invalid seed, and the
  warning line explains why. Disabling the button would mean adding the error to `CanStart`; not done, because the
  seed and gap inputs do not do it either.
- The Ba K/662 peak-bin ratio printed by the absorber test depends on the bin width (descriptive, not asserted).
