# PLAN.Studio.ReadoutPresets.Turn8 — Studio stage implementation (TODO-41)

Date: 2026-10-12. Implementation turn after the author's Studio-stage decisions (SD-A1 … A16 in
[PLAN.Studio.ReadoutPresets](PLAN.Studio.ReadoutPresets.md)), continuing the stopped Codex turn as a substitute
implementer. Headless only: no desktop scenario ran (they are compiled, waiting for the author's go). The record wording
below is a proposal; the planner applies it to VV / AGENTS / Findings.

## 1. What is implemented

| SD | Where | Behaviour |
|---|---|---|
| A1 … A3 per-line banks | `ReadoutPolicy` (Core), `ReadoutPreparationService`, `PhysicalListModeSource`, `AcquisitionSession`, `PhysicalReadoutProjection` | Every analysis line (122.06 / 661.7 / 1332.5 keV) gets a plan per module: offered / refused (record) / local (unmatched Custom) / unavailable (record missing). The lines an acquisition needs are the preparable lines the scene emits, by **exact** energy equality (Co-57's 122.1 keV emission is not the 122.06 keV row; Cs-137 Ba X-rays and Co-60 1173.2 keV are incidental). Prepare runs them one at a time; Start requires all of them and captures them as one bank (reference first: 661.7 keV when captured). The held ADC facts are acquired once; each line's calibration derives its own crystal and energy from them (`PhysicalMeasuredEvent.Assignments`, `ReadoutSnapshot.Lines`). A window is read only through the calibration of the one analysis line it contains; a window containing a refused / uncaptured line, no line, or two lines is **unavailable** with its reason (never a zero-count image, never another line's LUT). Refused and incidental lines stay in transport and never block. |
| A4, A5 flood correction | `ReadoutCalibration.Build(collectCorrectionHistogram)`, `PhysicalReadoutProjection.Window` | Every preparation carries a correction histogram counted from the events calibration already holds (finished LUT, per-crystal gains, ±window); Imaging applies it only when "Flood correction from the line calibration (opt-in)" is ticked. Weight = mean / bin, no clipping; raw integer counts stay the reported count; one empty bin makes the corrected image unavailable with the count of empty bins (raw image available with the correction off). Verified equal to an independent replay (engine test, 4 × 4 legacy and 8 × 8 LatticeGrowth / adjoint) and applied exactly (service test). |
| A6 cache | `ReadoutPreparationService` | Split keys: device by module + solve (line-independent, shared by every line of a module); calibration by module + line + budget + method + solve + seed scheme. Session-only. Entries a live acquisition holds are pinned (`Capture` / `Release`); others are evicted oldest first above an accounted-byte limit (LUT density + labels and their published copies, gains, correction counts); devices are dropped once no calibration references them. Limit 512 MiB — provisional, see §6. |
| A7, A8 session-only, progress | same; `MainViewModel.PrepareReadoutAsync` | No persisted cache, no new write surface (UiAutomation §1 unchanged; the record is an embedded resource). Background Prepare on one worker with real progress: stage, histories done / total of the streamed calibration and validation floods (every 4 096 histories), line; a progress bar and the state text show it. Cancellation inside the streamed flood publishes nothing; finished lines are kept. |
| Streamed floods, Growth / Adjoint | `ReadoutPreparationService.Build`, `StreamedFlood` | Every flood (calibration and the local 200-history validation) is streamed, never materialised. Matched fine heads use LatticeGrowth + adjoint solve at the record's pinned budget (Wide 661.7 at 8 000, High 661.7 at 64 000, every other offered cell 2 000); the 12 × 12 Baseline keeps the legacy LUT and solve on both lines. New entries use declared streams: 402 000 000 + 1 000 · line index (1 = 122.06, 2 = 661.7, 3 = 1332.5) + purpose (3 flood, 4 response, 5 ADC, 6 – 8 validation); the optical table / network seed stays 402 for every module. |
| Legacy identity | `ReadoutLegacyIdentityTests` | Baseline 661.7 keV keeps seeds 402 – 408, the legacy LUT / solve and its outputs byte for byte (§3). |
| Start carries the captured policy | `SimulationService.Start` | The acquisition configuration's readout is the reference entry's resolved policy (solve, method, line, budget); the prepared network is reused (the device guard refuses a different network). |
| Captured geometry | `ReadoutModuleGeometry` on every preparation; `AcquisitionSession`, `PhysicalReadoutWaveform`, `DetectorWorkspaceViewModel`, `ReadoutMapView` | Deposition, waveform coordinates, detector text and the circuit follow the captured module (crystal and sensor counts, pitch, gap, sensor pitch and active width); no 12 / 144 literal remains on the physical path. |
| A9, A10 Custom | `ReadoutPolicy.MatchModule`, `.Plan`, `.MatchesImagingProfile`; `ImagingWorkspaceViewModel.ReadoutCost*` | A module that matches a confirmed geometry (crystal count and pitch, gap 0.1 mm, entrance absorber 0.15 mm, backing 2 mm) inherits that geometry's offer or refusal and budget whatever the preset label (Custom cannot bypass High's 64 k). The reference cost is inherited only on a full imaging-profile match (rank, cell, distance, focus 1 000 mm, cross-correlation, the exact ±15 % window, flood correction on); otherwise "Imaging cost unmeasured for this configuration: <differences>" with the matched table shown as reference only. An unmatched module calibrates locally at 2 000 histories per crystal with LatticeGrowth / adjoint, labelled "experimental, unvalidated", imaging cost unmeasured. |
| A11, A12 exposures, MLEM | `ImagingWorkspaceViewModel.Readout.cs` | Both benchmark exposures (1 000 / 4 000 ideal-Direct counts) and both rates (isolated / 100 000 input hits/s) are listed; the acquisition's own window counts are shown separately; nothing is interpolated or mapped. MLEM decodes with the window line's energy and gets the "benchmark not applicable" notice. |
| A13 shipped record | `samples/evidence/studio_readout_offer_record.py`, `offer-cost-v1.json`, `ReadoutOfferPin`, `ReadoutOfferRecordLoader` | §2. |
| A14 large grids | `ReadoutMapView` | Cached raster (one pixel per LUT bin, viridis, built once per density image), cached frozen geometries for LUT boundaries (merged straight runs), peak marks and the circuit; zoom (wheel, + / −, 0 resets, up to 32×), pan (drag, arrows) and inspection (LUT crystal, row / column, density under the pointer, shown below the map and in the automation help text). A live snapshot tick only replaces the live raster. Actual sensor pitch and active width from the captured module. |
| A15 desktop | `tests/Gcam.Studio.UiTests/ReadoutScenarioTests.cs` | §5 (compiled, not run). |
| A16 ideal Direct | cost table, scope text | "Direct (ideal)" / "the ideal largest-deposit / true-energy reference, not Studio's legacy chain". F is shown without colour or badge with the exact sentence "F is an equal-fluence MSE ratio, not a time factor."; for every row with F < 1 the card adds the data's own explanation (Direct RMS, bias, bias share of its MSE) and "F below 1 is chiefly direct-reference bias under this decoder; it does not establish a general Anger advantage." (Wide 661.7 / 4 k isolated: 97.9 %). |

UI controls added (AutomationIds): `Readout.Lines`, `Readout.Progress`, `Readout.WindowLine`, `Readout.BenchmarkWindow`,
`Readout.Line`, `Readout.Offers`, `Readout.OfferDetails`, `Readout.MapInspection`, `Imaging.ReadoutCostSection`,
`Imaging.FloodCorrection`, `Imaging.ChannelUnavailable`, `Imaging.ReadoutCurrentCounts`, `Imaging.ReadoutCostStatus`,
`Imaging.ReadoutCostCard`, `Imaging.ReadoutCostTableSection`, `Imaging.ReadoutCostTable`, `Imaging.ReadoutCostScope`.
The map's automation ItemStatus now reports line, peaks, captured module and sensor pitch, and zoom.

## 2. Shipped record

The published parents measured 9 288 846 bytes (`confirmation-v4.json`) and 5 522 986 bytes (`repair-v5.json`). The
stopped turn's draft archived them as a 1.94 MB gzip plus a 4.98 MB record (about 6.9 MB), which broke the 1 MB rule.
Chosen instead: **one committed file, `samples/evidence/studio-readout/offer-cost-v1.json`, 242 860 bytes** (29 kB
gzip-compressed in git), which is itself the reduced, hash-chained archive:

- each parent as a projection: the parent JSON without its `Provenance` envelope and without the 32 per-seed
  `SensitivityMapsPerSeed` arrays (no averaged map is shipped either; the weakest / median scalar stays); every other
  value verbatim (literal failures, cost rows with intervals, diagnostics scalars);
- `Cells`: the 12 (preset, line) cells as Studio reads them — v4 with exactly Wide 661.7 and High 661.7 replaced by the
  v5 repair cells (budgets 8 000 / 64 000, source record and phase), the superseded v4 outcome kept in `Superseded`
  (Wide 4 / 32 failures at 2 000, High 1 / 32 at 32 000);
- the benchmark scope from the recipe (identical in v4 and v5, checked), the F meaning and interval method / caution.

Hash chain: full parent SHA-256 and bytes (pinned in the generator) → publisher sidecar SHA-256 and provenance digest →
projection digest → record bytes → the SHA-256 compiled into Studio (`ReadoutOfferPin.Sha256`).

| Link | confirmation-v4.json | repair-v5.json |
|---|---|---|
| Parent SHA-256 | `165280179215db6170e19dacb001d2c6b7724ad84fdd44e1c0b118f998fcc8ad` | `4dc2a598a108ac8974b35a76c0ec884da70ac72afe3559a8ce67edb2bdfb3d61` |
| Sidecar SHA-256 | `6c76287b9f2fd6941744ff07ab395d850516a56d126a9d707b3e1a1e863daf12` | `306ac3b8d78eb1dab3f75d708f841921cff6051b7728cc93b66c2a6b990f32d4` |
| Provenance digest | `fbf397670729f8fcbb2957ad05c353a919874d4287ea7342f592ecfb8bb9ae0b` | `b5d91fa46463fdc73a1ff6c22ec01e286f33bf9b12bfae5a1ee6cb644ba7f01e` |
| Projection digest | `c16c2fdc8a841927571bd43e296b8b6712a573b746aafba9d909331b89e1355d` | `960592aa5712686c606268356113d812950811c72269b1a2b1f444c46fa90e15` |

Record SHA-256 `e12cf8135759b7d669cdb5cbfacef7bc650a74cf0ac2e91a31933796feb2ff5f` (= `ReadoutOfferPin.Sha256`).

**What `--check` proves.**
- `--check --parents CONFIRMATION REPAIR` (parents present): the committed record is byte for byte the deterministic
  reduction of the two published parents whose bytes match the pinned SHA-256 and sizes, whose sidecars validate (output-set
  binding, provenance schema, envelope agreement), whose recipe / rule hashes are the repository files', and Studio's
  compiled pin is the record's SHA-256. Run this turn: passed.
- `--check` (parents absent — CI, through the new `studio-readout-offer` checker in `test_records.py` and `ci.yml`): the
  record is canonical; every embedded projection hashes to its recorded digest; the cells are exactly the declared
  overlay of the embedded projections (offers recomputed from failures, 32-seed populations, budgets from the recipe
  files whose hashes match the projections); the parent hashes are the generator's pins; the compiled pin matches. It
  does **not** prove the projections equal the parents' content — a coordinated edit of projection, digests and pin
  would pass, but it would be a visible change of the pinned record and of `ReadoutOfferPin.cs` in review.

Runtime: Studio reads only the embedded bytes, checks them against the pin and the schema; any mismatch, malformed data
or corrupt population makes the whole record unavailable (every line then plans as "Unavailable", nothing is offered).
`offer-parents-v1.json.gz` is obsolete (APPROVAL REQUEST in the turn report). The parents stay in the planner's scratch.

## 3. Legacy byte identity

`ReadoutLegacyIdentityTests` pins three SHA-256 fixtures recorded from the pre-bank code (branch head `0dc0b68` plus the
stopped turn's correction histogram, default off) before any service change: preparation `6a8b291c…24ae82` (every
published fact: density, labels, peaks, diagnostics, counts, timing, circuit), acquisition `6b169b2f…67b16d` (seed 4711,
500 µCi, 2 s: 68 records, 82 hits, every field, truth samples, floods) and projections `74a59000…edfa71` (imaging
channels, spectrum). All three still match after the bank, streaming and captured-geometry changes. The existing
TODO-40 tests (`PhysicalReadoutTests`) pass unchanged apart from the removed geometry gate.

## 4. Measurements

| Item | Value |
|---|---|
| Local 8 × 8 Custom (LatticeGrowth / adjoint, 2 000 per crystal) | 4.0 s preparation, 64 / 64 peaks (service test) |
| Accounted bytes per entry | Baseline 384² bins: ≈ 3.5 MB; High 1408² bins: ≈ 47.6 MB (formula in the service) |
| ReadoutMapView 44 × 44 offscreen (RenderTargetBitmap, 1408² bins, 1936 peaks, 1 280 × 800 and 1 440 × 900 views) | Two runs, both themes and sizes. Anger: cache build 37 – 112 ms once per preparation; cached redraw 29 – 37 ms; zoom 4× redraw 6 – 12 ms. SiPM: cache build 2 – 9 ms; redraw 39 – 55 ms; zoom 4× 10 – 21 ms |
| ReadoutMapView 12 × 12 offscreen | 5 – 18 ms redraw |

Before the boundary-run merge the 44 × 44 Anger redraw took 154 – 178 ms. Offscreen software rendering is not desktop
frame latency; the desktop pilot remains the planner's.

## 5. Desktop scenarios (compiled, not run)

`ReadoutScenarioTests`: `RefusedLineAndField_ExplainWithoutZeroing` (Sharp physical, 122.06 refusal, field refusal),
`PreparationCancelRetryAndTabs_AreExplicit` (Baseline cancel / retry / session hit / tabs / stale bank),
`FixedSeedRepeatAndContinuation_ConserveCountsAndExposeFourLanes` (unchanged intent),
`SharpTwoLineBank_SwitchesWindowAndLineWithoutTruthDispatch`, `WideCostCard_ShowsNeutralFAndTheDirectBiasExplanation`,
`HighResCold_ProgressTabsCornersAndSessionHit_Bundled` (the one cold 64 k High scenario),
`CustomModules_InheritByModuleNotCost_UnknownCalibratesLocally`. Waits: Baseline 90 s, Sharp 180 s, Wide 420 / 240 s,
High 5 400 / 420 s (Turn 7 rule); cancellation and redraw waits stay provisional. A missing / corrupt record cannot be
staged on the desktop without a modified build; it is covered headlessly (`ReadoutOfferRecordTests`). Headless oracles
(`CountOracle…`, `ModuleOracle…`) always run.

After desktop run 1 (4 of 7 new scenarios failed, all test-side): the map is read on the Anger tab (it is not in the
tree on Diagnostics); window edges are committed by focusing each other, high edge first (scene inputs are disabled once
data exist, so focusing `SourceY` failed — no product focus trap); `Optics.Rank` is a ComboBox (`Choose`); and the
harness never pointer-clicks unless the owned Studio window is foreground, restoring it by its own handle and logging a
step when another window had taken the foreground (the High run's workspace click had landed on a covering window).

After desktop run 2 (two failures, one product bug). SharpTwoLineBank now selects the "Energy window" channel before
reading the window counts ("All" is the labelled reference-line reading, SD-A2). CustomModules read an empty string:
the Lines rows are data items whose text is their name (no Text children), so the scenarios now read names and help
texts (`Describe`) and wait for the expected fragments after optics edits. Product bug fixed: the benchmark-window match
compared doubles exactly (`ReadoutPolicy.MatchesImagingProfile`, `lowKeV == low && highKeV == high`, and the same in the
cost-reason list); 1332.5 × 1.15 is 1532.3749999999998 in binary, so a typed 1532.375 never matched. The edges are now
exact decimals (record line and fraction as decimals: 1 + 2 = 3 places) and a typed edge matches iff it lies within half
a unit of that third decimal (0.0005 keV); the button sets the same decimal edges (562.445 / 760.955, 1132.625 /
1532.375 keV).

### 5a. Help marks (author's request, run 2)

A reusable `Controls/HelpMark` (circled "?", theme tokens `Size.HelpMark`, `Gap.HelpMark`, accent on hover / keyboard
focus, focus visual) carries secondary explanation: keyboard-focusable tab stop, tooltip on hover and on keyboard focus,
the same text as automation HelpText, one AutomationId per mark. Moved behind marks, screen by screen:

| Screen | Stays inline | Behind a help mark (AutomationId) |
|---|---|---|
| Readout panel | mode, "Experimental GAGG four-output readout", one line per calibration line ("661.7 keV · Offered (experimental)", "122.06 keV · Refused · calibration failed on 32 / 32 seeds", "Local · experimental, unvalidated"), local state, progress, state text, window inputs, window line | readout-model assumptions (`Readout.AssumptionsHelp`); per line: record sentence with population, budget, source file, phase, full first failure (`Readout.LineHelp.<keV>`) |
| Detector geometry panel | module / counts facts | readout-model assumptions (`Detector.ReadoutAssumptionsHelp`) |
| Detector map | map, live-density switch, inspection | map legend and zoom / pan help (`Readout.MapHelp`) |
| Detector Calibration tab | local preparation facts, one summary line per calibration line | shipped-record SHA, literal failures, superseded outcomes, confirmed diagnostics (`Readout.OfferDetails`) |
| Detector Diagnostics tab | per-crystal rows | what the local check is and is not (`Readout.DiagnosticsHelp`) |
| Imaging | "Experimental LUT image", short cost status, card with key numbers (RMS, F) and "F is an equal-fluence MSE ratio, not a time factor.", current window counts, unavailable-channel reason, flood-correction switch, full table rows | image explanation (`Imaging.SamplingHelp`); status reasons ("unmeasured because …"), provenance, the F-below-1 direct-bias explanation, benchmark scope (`Imaging.ReadoutCostHelp`); benchmark conditions under the full table (`Imaging.ReadoutCostScope`) |

After desktop run 3: the calibration-line rows are a `ContentItemsList` (an ItemsControl whose automation peer exposes the
row content instead of WPF data-item peers); each row's help mark is named after the row (visible summary and local state)
and carries the full record sentence as HelpText, so screen readers and UI tests reach it after rows are rebuilt.
Verified headlessly on the provider-side automation tree (`LineRows_ExposeTheirDetailsToAutomation_AcrossRowRebuilds`).

Proposed SR wording for SR-DET-10 and the readout rows: "Displayed" includes text behind a keyboard-focusable help mark
whose tooltip opens on hover and keyboard focus and whose automation HelpText carries the same text. The experimental
label, a refusal and its short reason, local state, progress and the key cost numbers (RMS, F) shall stay inline.
Proposed DESIGN.Controls note (for TODO-44): HelpMark is the Studio control for secondary explanation; never put a
requirement-critical state only behind it.

## 6. Deviations from SD-A1 … A16 and open points

1. **Line selection by window.** The selected analysis line is the one line inside the measured energy window (with a
   "Use ±15 % benchmark window" button); the Detector has its own line selector for the map / facts / diagnostics.
   Broadband readings (All channel, spectrum histogram, waveform reading) use the reference calibration (661.7 keV when
   captured, else the captured line), labelled. A scene with no preparable line (e.g. Co-57 only, Ir-192 only) cannot
   start a physical acquisition — previously TODO-40 read every scene through one 661.7 keV LUT.
2. **Correction histogram collected always, applied opt-in.** Collection is one pass over the held events and changes no
   calibration output (tests). The replay oracle runs on small modules (4 × 4 legacy, 8 × 8 LatticeGrowth / adjoint),
   not on each preset (a 64 k High preparation is out of the normal suite).
3. **Cache limit provisional.** 512 MiB of accounted calibration bytes; optical tables / networks are not measured or
   accounted (dropped when unreferenced). Only live acquisitions pin entries; a pending bank is not pinned (with the
   limit, a High 661.7 + 1332.5 bank accounts ≈ 95 MB, so a few module switches do not evict it).
4. **No averaged sensitivity map** is shipped (Turn 7 proposed one as a declared visual summary); the 32 maps remain in
   the parents only.
5. **Cost inheritance needs the exact benchmark window** (button) and flood correction on; a hand-typed 562.445 keV may
   not equal 661.7 × 0.85 in floating point.
6. **Device-build progress** has a stage but no count (the optical table and network expose none); cancellation there is
   the engine's existing token.
7. The local check in Diagnostics keeps TODO-40's design (200 histories per crystal, all energies, single-crystal), now at
   the line's energy, labelled apart from the confirmed 32-seed diagnostics (Calibration tab, `OfferDetails`).

## 7. Proposed record wording (implemented behaviour)

| Row | Proposed wording |
|---|---|
| SR-DET-05 | Detector shall retain DirectCrystal as default and support experimental GAGG FourOutputAnger on any valid geometry. Each calibration line (122.06 / 661.7 / 1332.5 keV) of the pending module shall be planned from the shipped, hash-pinned offer record and the module fingerprint: offered (experimental, at the pinned budget) when the confirmed cell's calibration succeeded on every seed, refused with its literal published reason and population otherwise, locally calibrated (experimental, unvalidated) for an unmatched module, and unavailable when the record is missing or malformed. Preparation shall capture the resolved module geometry, line, policy, budget, seeds and calibration identity; the 12×12 Baseline at 661.7 keV keeps its TODO-40 preparation byte for byte. Start shall capture an immutable bank of the required line preparations, used by Detector, Imaging, Spectrum, Waveform and circuit views. IndependentSipm remains outside this Studio implementation. |
| SR-RUN-34 | Physical Start shall require a matching successful preparation for every preparable calibration line the scene emits (exact energy equality) and shall refuse an unprepared, failed or stale line, a scene without a preparable line, and nonzero ambient/BSR before transport, preserving entered values and displaying an actionable reason. Refused and incidental emissions remain in transport and do not block. Imaging-cost values and crystal diagnostics shall never gate an otherwise calibrated experimental readout. DirectCrystal inputs, seeded stream, defaults and microcell policy shall remain unchanged. |
| SR-DET-10 | Detector and Imaging shall display, per line, the confirmed offer or refusal (population, budget, source record and phase, literal failures, superseded outcome) separately from the local preparation state and its stage / history progress. Imaging shall show, for the window's calibration line, a reference card and the full shipped table: both readouts' absolute angular source RMS, per-position bias and remote-peak probability, accepted counts and F with seed-cluster intervals at 1000 / 4000 expected ideal-Direct counts, isolated and 100000 scored input hits/s, the declared benchmark scope, the sentence "F is an equal-fluence MSE ratio, not a time factor.", and for every F below 1 the Direct path's own bias share with the statement that it does not establish a general Anger advantage. F carries no colour. The acquisition's own counts are shown separately; nothing is interpolated or mapped. MLEM, other windows and unmatched profiles show "imaging cost unmeasured for this configuration" with the reference table labelled not applicable. |
| SR-DET-11 | Any valid Custom geometry may Prepare. A module matching a confirmed geometry (crystal count, pitch, gap, entrance absorber, backing) shall inherit that geometry's per-line offer or refusal and pinned budget irrespective of the preset label; the reference cost is inherited only when the full imaging profile (mask rank and cell, distance, focus, decoder, window, flood correction) also matches. An unmatched module calibrates locally at 2000 histories per crystal with LatticeGrowth and the adjoint solve, labelled experimental and unvalidated, with imaging cost unmeasured; its failure shows its own reason. Held ADC facts shall be acquired once and projected through each captured line calibration, never dispatched by generating line or true energy. |
| SR-IMG (new or note under SR-DET-10) | The physical window image may apply an opt-in flood correction from the line calibration's own photopeak-assigned flood counts (weight = mean / bin, no clipping); raw counts remain the reported count; an empty correction bin makes the corrected image unavailable, the raw image remaining available. |

**EV-36 scope note.** Retain the original lab result, rule and conditions; its localisation is mean radial error, not
TODO-41 RMS. TODO-41: the v4 confirmation plus the final declared v5 repair support experimental calibration offers for
Baseline / Sharp / Wide / High at 661.7 and 1332.5 keV, 32 / 32 seeds each, at 2 000 histories per crystal except Wide
661.7 (8 000) and High 661.7 (64 000); 122.06 keV is refused on every head. Studio implements these offers per line from
a generated, hash-pinned record and streams every calibration flood; costs use eight predeclared seeds, 1 000 / 4 000
ideal-Direct exposures, isolated / 100 000 input hits/s, per-path correction and finite correlation at 1 m. F is not a
time factor; Wide 661.7 / 4 k F below 1 reflects direct-reference bias. Saturation / recovery and arbitrary Custom
source geometry are not validated.

**LIM-11 scope note.** Virtual matched sensors, assumed optical / reflector constants and unlimited cells remain
assumptions, increasingly consequential at fine pitch. Numerical flood success does not validate an instrument or remove
multi-crystal migration / remote peaks. Studio offers the four module designs at 661.7 / 1332.5 keV (experimental) and
refuses 122.06 keV; it shows absolute RMS / bias / remote / F with their scope, never a performance or duration promise.
High-res 661.7 keV needs a streamed 64 k flood (≈ 45 min, ≈ 6 GiB, one worker); reuse is session-only.

**Theme 68 scope note.** The 0.406 ± 0.006 mm versus 0.367 ± 0.001 mm result is mean radial localisation error at equal
accepted counts for one centred near-field lab source, not field-wide source RMS / equal-fluence cost at 1 m. TODO-41
calibrates per line, reports absolute costs, and Studio's physical path now runs on every preset with per-line banks;
calibration success, not imaging cost, decides an offer. Ordinary engine defaults remain DirectCrystal.

**Findings 69 proposed entry.** TODO-41 separates per-line experimental calibration availability from measured cost. V4
confirmed Baseline / Sharp 661.7 / 1332.5 and Wide / High 1332.5; four Wide 661.7 and one High 661.7 failures remain
published; the final declared v5 repair fixed 8 k / 64 k on DEV4 and passed 32 / 32 fresh seeds each — 8 offers, 4
refusals at 122.06 keV. Costs: 8 fixed seeds, nine positions at 1 m, fresh paired histories, per-path correction, equal
emitted fluence, 1 k / 4 k ideal-Direct counts, isolated / 1e5 input hits/s (Wide 661.7 isolated F 2.078 [1.363, 3.170]
/ 0.8295 [0.8231, 0.8358]; High 4.668 [3.063, 7.115] / 2.586 [2.550, 2.623]). Studio stage: per-line banks with held
facts acquired once and per-line projection, streamed floods with record budgets (LatticeGrowth / adjoint on the fine
heads, Baseline 661.7 byte-identical), an opt-in exact flood correction, Custom inheritance by module (cost only on a
full profile), a 243 kB generated record checked against its parents and a compiled pin, and a cached large-grid map
(44 × 44 offscreen redraw 29 – 37 ms after a 37 – 112 ms cache build).

**AGENTS.Studio "Physical readout" paragraph (proposal).** Readout: DirectCrystal remains the default. Experimental
FourOutputAnger runs on any valid geometry; each calibration line is offered, refused, local (unmatched Custom) or
unavailable from the embedded, hash-pinned record (`ReadoutOfferPin`, `studio_readout_offer_record.py --check`). Prepare
streams one flood per required line on one worker with progress; the session cache pins captured entries; nothing is
persisted. Physical workspaces consume one immutable measured prefix with per-line projections; never read a window
through another line's LUT; do not apply legacy MeasurementStage or energy-only pile-up to it. The 12 × 12 Baseline 661.7
keV entry is pinned byte for byte (`ReadoutLegacyIdentityTests`). A 64 k High-res preparation never runs in the normal
suite. Keep Core WPF-free. New readout renders require GCAM_RENDER_OUTPUT under task scratch; desktop
ReadoutScenarioTests remain opt-in on the author's go.

## 8. What remains

- Desktop runs of §5 (author's go; one cold High-res scenario ≈ 45 min) and the desktop redraw / cancellation pilot.
- A measured cache limit including optical-table sizes (the 512 MiB accounted limit is provisional).
- The planner's application of §7 to VV.Studio.SRS / SDS / VV.Studio / VV.Gcam.Evidence / Limitations, AGENTS.Studio,
  AGENTS.Findings, and the Todo row.
- Optional: an evidence-switch (`GCAM_EVIDENCE_TESTS=1`) test that prepares a real fine-head cell through the service
  (Sharp 661.7 ≈ 60 s) — not added this turn.
