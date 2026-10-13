# PLAN.Studio.ReadoutField.Review — TODO-42 turn 1

Review date: 2026-10-13. Baseline: `todo42-readout-field`, HEAD
`0d215246026f3210cbc696d9e748ddff1a1cae72`. Review only; no implementation or numerical evidence collected.
The reference plan was the sole untracked file at the initial read-only status check.
Read the task's scratch `report-estimate.md`, repository guidance, the reference plan including D1–D8 and
calibration convention, archived TODO-40 / TODO-41 plans, and the specified SRS rows.
All source references below are relative to this repository and refer to this HEAD.

## Assessment and decisions needed before implementation

Stage A is feasible, but removing the refusals alone is incorrect. Event/site ownership must survive all nested
look-aheads, and pulse observation must advance through proved empty intervals. D7 is confirmed.

Accept D1, D2, D3's source-only bank and measured-record selection, D4's stage-A refusal, D6–D8. D5 requires a
correction: the existing detector does not apply passive materials correctly to all-face *incoming* rays.
Merely passing the signal constructor's arguments would reject rear incidence even when the entrance slab
transmits unchanged. Decide the physical all-face boundary convention before implementing both-bound support.
The recommended narrow correction and its cost are below. This is a modelling decision, not a command-approval request.

Do not claim the patents/preprints establish accurate positioning of arbitrary field deposits with a source-line
LUT. The repository implements an explicit approximation: acquire held codes once, derive every captured line's
assignment, use the labelled reference for broadband views, and select a calibrated line for an energy window.
That is defensible as a declared simulator convention; its off-energy accuracy remains unvalidated. Field-only
refusal is a product decision based on the offered calibration set, not a physical impossibility or a universal
rule that a background-only scene has no analysable energy. No external literature retrieval was performed in
this restricted review; the cited papers' full claims have not been independently verified here.

## Current-code audit: every “What exists” reference and verify row

| Plan claim / decision | Current reference and finding |
|---|---|
| Ambient independent streams, pixel callback, no sites | `src/Gcam.Simulation/AmbientPhotonProcess.cs:75–85`: time/entry/energy/mask seeds +29001…29004; detector RNG +29005; callback records the pixel and clamped scalar. No interaction sink. References current. |
| One incident history to a fixed horizon, misses included | Same file `:100–119`: next arrival beyond horizon advances TimeS to the horizon; otherwise histories increment once, then mask/detector can miss. A null result does not necessarily finish an interval. |
| Front-mask / all-face sampling | Same file `:69–72,137–163`: incident normalization uses projected envelope area (or angular effective area); front entry is back-projected outside mask; all-face chooses six envelope faces and cosine incidence. References current. |
| Deposit clamp | Same file `:83–85`: scalar is `Math.Min(e, _incidentEnergy)`. Sites would retain the physical deposits, not this diagnostic clamp. |
| Ambient merge pending source | `src/Gcam.Simulation/AmbientAcquisition.cs:11,56–75`: only DetectedEvent retained; ambient transported to min(horizon,pending source,dark). Ambient at a tied arrival is returned first; then dark at equality; then source. |
| Source-free transport exists | Same file `:30–48`: empty Sources array or zero single-source activity suppresses source transport. BSR times zero is zero; dark has independent placement. This remains supported for DirectCrystal even though D4 refuses Studio physical Start. |
| Recording refusal | `src/Gcam.Simulation/ListModeSource.cs:75–81`: recording currently refuses any non-null Ambient, positive BSR or dark; then ambient delegates to AmbientAcquisition. Correct the plan's approximate `75–80` to `75–81`. |
| Fixed-horizon look-ahead | Same file `:273–286`: ambient delegates with horizon; otherwise `_fixedPending` stores a bare event and survives interval boundaries. The reference is current, but a site pair is absent here as well. |
| Physical producer refusal | `src/Gcam.Simulation/Readout/PhysicalListModeSource.cs:43–45`: any non-null Ambient, positive BSR/dark, nonzero legacy optical crosstalk refused. |
| Physical truth cap, response / ADC streams | Same file `:23,52–54,75`: 256 histories; independently keyed response 40001 and pulse-stream digitization 40002. Sites copied into a separate pending array at `:65–67`; observation limited by the pending event at `:69–81`. |
| BSR discards transported location | `src/Gcam.Simulation/ListModeBackground.cs:28–31,38–49`: diffuse front-entry transport gives scalar energy; Advance calls Place; Place draws two independent uniform pixel coordinates. D2 correctly requires a separate physical return contract. |
| Detector sink / reused buffer | `src/Gcam.Detector/ComptonCrystalDetector.cs:63,209,214,260–263`: raw sites accumulated before optical spread and callback invoked with reused list. Copy before another Score. Early misses can occur before `_raw.Clear()` at `:162–163`; producers must clear their own last-result state before every attempt, not assume a miss invokes the sink. |
| Photoelectron needs | `src/Gcam.Detector/Readout/ReadoutDevice.cs:148–159`: uses crystal X/Y, depth Z and site energy to index optics and scale photons/PDE. It clamps crystal indices; tests must establish valid indices rather than accept this clamp as validation. |
| Source line policy | `src/Gcam.Studio.Core/Services/ReadoutPolicy.cs:79–93`: isotope emission energies matched exactly to AnalysisLines, then only CanPrepare plans required. Ambient contributes no required line. D3 fits current semantics. |
| Service bank capture and guards | `src/Gcam.Studio.Services/SimulationService.cs:24–40`: Capture, complete required energy-set check, resolved policy from bank reference, release on failure. StartAmbient physical refusal `:53`; BuildConfig ambient/BSR guard `:65`. References current. Capture alone is not the full validation; AcquisitionSession checks module key too. |
| Shell pre-Start refusal | `src/Gcam.Studio.Core/ViewModels/MainViewModel.cs:478–490`, not the plan's `487–495`: field refusal has precedence over missing-line/preparation reasons. `MainViewModel.Readout.cs:42` still says fields must be zero. `MainViewModel.Options.cs:16,31–32` also enforces the old message in OptionsProblem; this additional site must change. |
| Snapshot null-source defect | `src/Gcam.Studio.Services/AcquisitionSession.cs:124–126,132,224–225`: prepared session initializes physical source only; local direct source is null. AmbientRate would silently publish zero; SourceRate would dereference null with Ambient set. D7 verified. |
| Geometry difference / D5 verify | `ListModeSource.cs:128–137` supplies entrance/backing objects, gap, material, attenuation, thickness and interaction sink. PhysicalListModeSource clones config, clears Readout and uses this constructor `:46–49`. Ambient `:78–85` and BSR `:28–31` omit gap/passives. Verified, with the all-face qualification below. |
| Transport files unchanged since TODO-40 | `git diff 1ef455e HEAD --` the four named transport files returned no diff. Their tracked bytes are unchanged. TODO-41 bank projection is visible at PhysicalListModeSource `:85–96`; preparation/LUT/adjoint redesign is outside this task. |

Other plan decisions checked: D1's dark/crosstalk exclusions match the physical constructor; D2 preserves the
existing schedule at ListModeSource `:170–193`; D4 currently falls through the generic zero-required-line reason,
not an explicit field-only reason; D6 needs additional fixtures; D8 has no current field-readout evidence.
Archived SD-3 is superseded only for source-plus-ambient in A and BSR in B, not wholesale.

## Stage A: owned transport envelope and horizons

Propose one sealed `RecordedTransportEvent` in its own Simulation file, with DetectedEvent, owned read-only
InteractionSite collection, and TransportOrigin (`Signal`, `Ambient`, later `DiffuseBackground`, enum in its own
file). Constructor copies a callback buffer exactly once; no writable array escapes. A copied source decay merges
all its photons' sites before wrapping, as ListModeSource already does at `:233–268`. Keep DetectedEvent free of
readout state. Origin is diagnostic provenance; it never selects a calibration or excludes an input from a window.
No speculative conversion-origin label: pile-up conversions can have contributors of several origins.

Use an explicit recording/physical-geometry route. Recording alone must remain pure bookkeeping for legacy
geometry; do not redefine `recordInteractions: true` as “change the detector”. PhysicalListModeSource selects
module geometry independently when constructing its internal direct transport. Preserve the public direct scalar
methods and their disabled fast path. Recording methods return the envelope (or provide an internal paired result
that scalar methods project); a mutable LastInteractions property must not be the ownership protocol.

Ownership through each pending boundary:

1. AdvanceSignal creates an owned envelope only for an accepted event, after all mask/score/rejection operations.
   Rejected histories clear last-result bookkeeping and return null. `_pendingSignal` carries the pair while
   another input is emitted. Stage A refuses BSR/dark recording, but the slot must still be pair-capable for B.
2. AmbientPhotonProcess clears deposit and site-result state before mask transmission; interaction callback copies
   the buffer for this history. Accepted arrival wraps this copied result with Ambient origin. A miss, cancellation
   before draws or empty interval returns no new envelope, never the preceding event's sites.
3. AmbientAcquisition retains a source envelope in `_pending`, not event plus a later read of source.LastInteractions.
   The child must finish transporting one candidate before ambient can interleave. Return the ambient envelope or
   transfer the pending source envelope. Retain the exact scalar cutoff/order: ambient wins equality; dark then
   source in the legacy path. Nested signal merge retains its current comparisons: source wins ties with BSR/dark;
   BSR wins a BSR/dark tie strictly before source. Do not unify these into a new sort order.
4. ListModeSource's `_fixedPending` holds the same envelope when an accepted source candidate lies beyond the
   horizon. Restore/transfer the pair only when returned. Clearing “last returned” metadata must not clear pending
   ownership. ArrivalTimeS is proved observation time, not the future candidate's arrival.
5. PhysicalListModeSource consumes envelopes via the fixed-horizon route for ambient. After each bounded advance,
   use the transport's proved time, never blindly the requested horizon. Append a hit only for a returned event
   whose arrival is observed; call device.Respond once, then stream.AdvanceTo(provedTime * 1e9). Even when null,
   advance the pulse stream to the proved time so pending holds complete through empty intervals. A source miss
   that proves no extra time must not advance pulse observation. Do not use Advance() for an ambient wrapper
   (it currently throws at ListModeSource `:157`).

For zero-field physical acquisition retain the existing scheduling behavior, or prove an equivalent unified
AdvanceUntil implementation against pre-change physical hashes before replacing it. No extra response or ADC draw
for null/miss/future envelope. Future source look-ahead is permitted, future pulse realization is not. Events at
the horizon remain inclusive as today. Relative site flight times remain site facts; preserve the existing physical
input timestamp convention rather than silently adding a second time offset.

Stop cancellation remains checked before each bounded transport call; after a consumed input, append, advance and
publish its completed conversions together. Keep the transport object, paired pending slots, all RNGs and pulse
stream across Continue. Do not flush unfinished holds on Stop/Completed; a final horizon can still have a pending
hold. Continue to a later horizon completes it identically to uninterrupted acquisition. An initially empty
field interval must complete even without any hit to use as a clock.

Storage is bounded by the nesting depth: one transient/current envelope per producer and at most one pending
candidate per applicable merge/wrapper slot. Transfer references, do not recopy arrays through every wrapper.
There can be a future source envelope plus a currently returned ambient envelope; “one envelope total” would be
incorrect. Retained truth stays capped at 256 and holds immutable sites. Remaining history retention is compact
realised-hit and conversion records, as today; no cumulative site list. The pulse stream's active-hit storage is
rate/kernel dependent, not bounded by the 256 truth cap, and should be reported separately in the pilot.

Test ownership by keeping several returned envelopes, making additional Score calls and verifying earlier sites
unchanged, including multisite and cascade histories. Inject deterministic tied arrivals at the merge seam to
verify tie order; randomly sampled streams cannot reliably exercise equality. Partition horizons immediately
before/at/after arrivals and inside holds, plus long empty intervals; compare complete records and every line's
assignments, not only counts. Assert truth count never exceeds 256.

## D5: construction and the two incidence conventions

The physical signal detector is ComptonCrystalDetector with configured pixel grid/pitch, crystal depth,
attenuation override (positive or null), material, reflectorGapMm, entrance and backing EntranceAbsorber objects;
Argmax, broad scoring window and seed +777 (`ListModeSource.cs:128–137`). Physical mode excludes legacy optical
crosstalk; readout optics supply light sharing. ReadoutDevice supplies the response module, not gamma transport.

Smallest safe front-only change: add an explicit physical-module transport option to AmbientPhotonProcess, then
pass the same depth/material/gap/passive configuration as signal while retaining ambient seeds, window, mask,
unit weight and callback semantics. A small shared internal construction helper may remove the duplicated module
arguments; keep caller RNG and sinks explicit. Do not route the DirectCrystal constructor through a changed
default. Its no-gap/no-passive homogeneous-envelope detector and every random call stay unchanged. The direct
evidence remains scoped to that construction, not to the new physical numbers.

Front-only still means front-envelope incidence through the mask; adding module loss changes efficiency but not
this proposal or absolute fluence normalization. All-face with module materials no longer means “bare homogeneous
crystal”. It means an unmasked all-face *module-envelope* proposal. It cannot be asserted to remain an upper bound
on all measured outcomes: added backing scatter and readout trigger/window effects prevent that general ordering.
Keep the existing enum for compatibility if desired, but physical help/captured metadata must say “unmasked
all-face module approximation”; do not present it simply as bare-crystal evidence.

Specific blocker: ComptonCrystalDetector `:131–145` permits box entry but `:153–158` applies entrance.Interact to
all faces and rejects dir.Z >= 0. A rear ray can pass the slab unchanged and still be rejected. Backing at
`:177–201` handles outgoing rear escape/backscatter, not incoming rear transmission. Reflector handling is also
an envelope-entry XY gap check and merged slab transport, not explicit tracking through separate reflector volumes.
Copying constructor parameters is therefore insufficient for D5's physical all-face meaning.

Recommended limited extension: an opt-in boundary-aware module mode, preserving default Score byte behavior.
Determine entering face; apply front entrance only on front entry, rear backing as an incoming passive proxy only
on rear entry, no side housing unless separately modelled; accept scattering only when direction continues inward
relative to that face. Retain existing outgoing backing proxy. Test face selection and unchanged-pass directions
with deterministic rays before numerical evidence; test zero-passive mode and seeded front path equality.
Document nominal-envelope sampling, passive slabs as lumped steel-equivalent proxies (no explicit passive-volume
intersection or angle-dependent path length), and the current gap/transport approximation. This is not a full
housing simulation. If the author instead chooses no passive materials on all-face ambient, D5 must explicitly
grant that exception; it is smaller but does not meet “same construction”. Neither choice should be hidden in code.

## D6 / D7: exact fixtures, component rates and null safety

Capture new SHA-256 fixtures from the unchanged HEAD **before implementation**, in task scratch. Never manufacture
expected hashes from the modified implementation. Canonical BinaryWriter bytes: ordered pixel/deposit/time records,
call-by-call null/result and proved-time sequence for fixed-horizon tests, final histories/weights/accepted/cascade
counters and source/ambient/total rate double bits. Exclude wall time, CPU speed and unordered dictionary order.
Use two fixed seeds (12345 and 4711), fixed horizons and a fixed continuation suffix so RNG drift after the prefix
is visible. Hash identity checks prove the exercised progression; they do not exhaust all possible RNG states.

Proposed configurations / streams:

| Regression | Configuration and scope |
|---|---|
| Existing source fixtures | Keep ReadoutDefaultPathTests `DefaultListMode_MatchesPreReadoutFixtures`, `InteractionRecording_LeavesEveryStreamIdentical`: GAGG scenario.json (2000 accepted), scenario_co60.json (1000), realism (gap .1, optical crosstalk .4, entrance 1, backing 2; 1000), seed 12345. Keep their published hashes and runner/config fixtures. |
| Direct ambient alone and merge | Validated terrestrial spectrum; dose 0, .1 and 1 µSv/h; both bounds; source-free and Cs source, plus Cs/Co scene for multisource/cascade merge. 10 s final horizon, then 1 s suffix; uninterrupted and .25 s partitions. Nonzero configured gap .1/entrance .15/backing 2 deliberately ensure direct ambient continues to ignore them. |
| Angular branch | Separate engine-only isotropic and EnergyZenithTable fixtures using existing test spectrum/table definitions; both geometries. These are development fixtures, not claims about the validated terrestrial model. |
| Direct BSR / Place | BSR 0, .5 and 1; energy 200 keV; Cs and Co scenes, with/without .1 ambient at both bounds; fixed 10 s plus suffix. Include dark-only nuisance and BSR+dark legacy configurations for RNG/ordering. Hash standalone Place sequence (energies 3/200 alternating, known times) through existing friend-test access. New physical BSR must never consume the legacy pixel RNG. |
| Zero-field physical | Keep ReadoutLegacyIdentityTests `Baseline661_PreparationAcquisitionAndProjections_MatchPreBankFixtures`. Add a zero-dose Ambient-object vs null test only if the physical API promises equivalence; Studio normally omits zero ambient. Pin held ADC/analogue facts, realised hits, trigger/hold times, reference and every captured assignment, raw window/flood/spectrum arrays for a Cs+Co bank. Preserve IndependentSipm engine zero-field coverage; Studio still refuses it. |

Where recording uses unchanged legacy geometry, pair recording-on/off streams exactly. Where D5 changes physical
geometry, do not compare physical ambient deposits with DirectCrystal deposits and call differences regressions.
Test separate constructed detector equality and response pairing instead. Energy conservation uses original sites;
the ambient scalar may differ by clamping/summation round-off. Derive the rounding allowance from site count and
actual sums (unit round-off u = 2^-53; gamma_n = n*u/(1-n*u), accounting for both reduction orders and clamp).
Include the transport's iterative energy-subtraction error budget; no reused universal 1e-12 tolerance without
that derivation. No rewriting sites to force equality with the scalar.

D7 repair: expose SourceRateCps and AmbientRateCps from PhysicalListModeSource's internal transport, alongside
InputRateCps. In AcquisitionSession `:224–225`, choose physical producer when present, else direct producer, else
zero; preserve current no-ambient SourceRateCps display convention (0). Do not merely null-coalesce source! to
zero: that avoids the exception but loses physical rates. Publish all three from the same producer state.
These are pre-trigger transport estimates, not measured assigned rate. The status count rate stays assigned/live
time. Ambient/source BSR is undefined with zero source rate and must not divide by zero. Early estimates can be
zero; future source look-ahead counters are transport estimates, not observed conversion counts.

Services tests must cover first zero-time publication, empty interval publication, both ambient bounds,
Stop/Continue, all line assignments and immutable earlier snapshots. StartAmbient must share Start's bank capture,
required-line validation, resolved policy and release-on-failure. Capture once and release once on construction
failure/disposal; no bank retained on a refused field-only start. Field dose is not a calibration-cache key.

## D3 / D4: policy, refusals and TODO-45 status path

Keep ReadoutPolicy.LinesInScene / RequiredLines source-derived and exact-energy matched. Add a shared Core policy
reason for physical source-free acquisition (and stage-specific BSR exclusion), used by shell and service before
capture or transport; distinguish empty scene from a source scene with no preparable line. Do not include ambient
spectrum lines in RequiredLines or select the LUT by generating energy. Service BuildConfig and StartAmbient must
retain independent validation for callers bypassing the shell. Engine recording can still support source-free
ambient; D4 is the Studio calibration/product policy, not a reason to break direct source-free transport.

Exact proposed messages (shared text wherever the same condition is exposed):

- Field-only: “Physical readout cannot start a field-only scene: no field calibration line is offered. Add a source with a preparable calibration line, or select DirectCrystal.”
- Stage-A BSR: “Physical readout requires BSR = 0 in this stage: diffuse background interaction recording is not supported yet. Set BSR to 0, or select DirectCrystal.”
- Dark engine guard: “Physical readout does not support electronic dark arrivals: gamma interaction sites are unavailable.”
- Legacy crosstalk engine guard: “Physical readout requires legacy optical crosstalk = 0: light sharing is provided by the readout optics.”
- Missing bank: “Prepare every required source calibration line before Start (required: {ascending invariant-culture energies} keV).”
- No preparable source line: retain the current “No preparable calibration line is present in the scene: ” plus published plan reasons.

ReadoutAssumptions replacement sentence: “Source and ambient deposits share one measured pulse stream. Only the
source's preparable analysis lines are calibrated; other-energy deposits use those captured calibrations and
their positioning accuracy is unvalidated. Field-only scenes and nonzero BSR are unavailable in this stage.”
Retain the existing physical-chain assumptions around it. Add the physical incidence construction caveat to ambient
help and retain it with acquisition inputs, rather than adding a performance/applicability card.

MainViewModel.BeginAcquisition `:478–490`: check field-only first, then BSR, then required lines / readiness.
Nonzero ambient with a source is no longer itself a refusal. Field-only selects SceneTab and calls RefuseStart
without options; BSR selects DetectorTab with options:true (input is in Options). Missing bank selects DetectorTab,
without an Options warning. MainViewModel.Options.cs `:16,31–32` must remove the blanket field rule: OptionsProblem
is invalid seed/dose/gap or stage-A BSR, not valid ambient. Keep CanStart as the current invocation gate so a
source-free physical scene with nonzero field can visibly explain its refusal; disabling Start hides the reason.

Use TODO-45's RefuseStart `MainViewModel.Options.cs:37–45`: Status/StartRefusal become “Start refused: ” + reason
or “Options: ” + reason; IsStatusWarning remains true; no dialog opens. Preserve all entered values, no service
calls and no acquisition/workspace Begin on shell refusal. ClearStartRefusal before a successful new start/Reset.
Service exceptions for a bypass caller remain the existing Failed boundary behavior; do not mislabel a started
worker's runtime failure as a shell refusal.

Revise StartRefusalTests `AmbientOrBsrWithPhysicalReadout_ShowsOptionsReason_SelectsDetectorTab_WarnsOnTheCog`
to BSR-only wording; add field-only SceneTab and ambient-with-source missing-bank cases, plus prepared
source+ambient reaching StartAmbient and clearing refusal without Options warning. CountingAcquisition should
explicitly count StartAmbient as well as Start so the fixture cannot hide the routing distinction.

## Proposed requirements and verification wording only

The planner applies the following clauses; no SRS edit is made in this turn.

| ID | Proposed replacement/addition |
|---|---|
| SR-RUN-13 | Retain current enable/Continue rule. Add: “An enabled physical Start shall explain a field-only or preparation refusal before acquisition; source-free nonzero ambient acquisition remains available with DirectCrystal.” |
| SR-RUN-22 (A) | Add: “Nonzero BSR is refused before physical acquisition, preserving the input; the DirectCrystal detected-pedestal contract is unchanged.” |
| SR-RUN-22 (B) | Replace that addition with: “For physical readout, BSR schedules diffuse detected inputs relative to the running pre-trigger source-rate estimate. Each accepted unmasked front-entry history contributes its own interaction sites and location, without uniform pixel replacement. It contributes zero at zero source rate; retries are cancellable between histories. Triggered, assigned and window ratios need not equal the entered BSR.” |
| SR-RUN-29 | Add: “With a source and prepared source-line bank, physical ambient deposits enter the common response/trigger/hold stream. Its detector uses the captured module geometry; front-only is masked front incidence and all-face is the declared unmasked module approximation. DirectCrystal retains its homogeneous bare-envelope construction and evidence. Published source/ambient rates and their derived ratio are pre-trigger transport estimates; zero source rate makes the ratio undefined.” Preserve validation/hash/dose grammar and zero-environment clauses. |
| SR-RUN-34 (A) | Replace nonzero ambient/BSR refusal clause with: “shall refuse a field-only scene and nonzero BSR before transport; source-plus-ambient shall require the same complete source-line bank as source-only acquisition.” Add: “Ambient energies never create required calibration lines or dispatch held facts by truth; off-energy positioning through a captured source-line calibration is unvalidated.” Keep all existing failure/stale/refused/incidental/DirectCrystal clauses. |
| SR-RUN-34 (B) | Remove the BSR exclusion after the physical detected-input contract and evidence are recorded. Keep field-only refusal. |
| SR-DET-05 | Add: “The required bank is derived from preparable source analysis lines only, even with ambient enabled; field-only physical Start is unavailable.” |
| SR-DET-11 | Add: “The same held-fact projection rule applies to source, ambient and later diffuse background inputs; origin and true energy never choose a calibration.” |
| SR-RUN-30 / -31 / -32 / -33 | No behavioral relaxation. Extend verification selectors to mixed input origins, observed empty intervals, boundary holds, both incidence conventions and every captured line. |
| SR-WAVE-09 / -10 / -11 | No normative change needed. Extend verification to field contributors and mixed-origin pile-up; contributor/dominant-hit diagnostics do not classify a conversion as wholly one origin. |
| SR-DET-10 | Keep benchmark-reference-only help. No ambient imaging-performance claim, scene applicability mapping or offer-record edit. |

Input-table proposed additions (`VV.Studio.SRS.md:239–241`): BSR row adds “physical: refused in ambient stage;
later pre-trigger diffuse detected-input ratio”; ambient dose row adds “physical: source scene and complete
source-line bank required”; bound row adds “physical all-face uses the declared unmasked module approximation;
DirectCrystal retains the bare-envelope bound”. If a different D5 convention is adopted, rewrite these clauses
before implementation rather than preserving misleading “bare” text.

Desktop source/scenario changes proposed (none run):

- `ReadoutScenarioTests.RefusedLineAndField_ExplainWithoutZeroing` (`:97–117`): rename to
  `RefusedLineAndMissingPreparation_ExplainWithoutZeroing`; retain literal 122.06 refusal/offer assertions;
  nonzero default field plus Cs source now gives missing source-bank reason, status warning, DetectorTab,
  unchanged dose and Empty state, not “zero”. Its existing refused-window selection does not change the required Cs bank.
- Add `PhysicalAmbient_BothBounds_ContinuationConservesMeasuredCounts`: prepare Baseline once, source+ambient
  fixed seed, each bound; uninterrupted vs Stop/Continue final assigned/unknown/triggered tuple, stopped stable
  count, source/ambient estimate display, retained bound/inputs and Spectrum/Imaging window count agreement.
  Broken verdict changes an expected count by one, recovery restores it; no statistical localisation threshold.
- Add `PhysicalFieldOnlyAndBsr_RefuseWithoutChangingInputs`: field-only exact reason/status/SceneTab; source+BSR
  exact Options reason/cog/DetectorTab; correcting BSR allows ambient acquisition with prepared bank. Separate
  corrupted key refusal verdict and recovery.
- Retain `FixedSeedRepeatAndContinuation_ConserveCountsAndExposeFourLanes` as zero-field guard and
  `SharpTwoLineBank_SwitchesWindowAndLineWithoutTruthDispatch` as bank guard; extend the latter with ambient
  only if reusing preparation, asserting the window's line and same measured count population.
- Stage B add `PhysicalDiffuseBackground_UsesCommonStreamAndPreservesInputs`, including BSR+ambient.
  Desktop checks demonstrate UI contract, not statistical BSR equality. Keep ordinary DirectCrystal
  source-free ambient desktop coverage unchanged.

Headless revisions include ReadoutDefaultPathTests `Recording_IsRefusedWithBackgroundOrAmbient` (ambient now
recordable, BSR/dark still refused in A), PhysicalReadoutTests `UnsupportedInputs_AreRefusedWithoutMutatingThem`,
ReadoutViewModelTests `StartRefusesFieldAndMissingPreparationWithoutSilentZeroing`, and the StartRefusal case
above. Extend `Continuation_ConservesMeasuredRecordsAndImmutablePrefixesExactly` and ReadoutBankTests
`HeldFactsOnce_EachLineEqualsItsOwnSingleLineAcquisition_WindowsReadOnlyTheirLine` with ambient. Keep
ReadoutStreamTests `HoldWaitsForObservedFuture_AndCancellationConsumesNoNoise`. Detached render fixtures later
cover new status/readiness/help wording in both themes; no render or GUI executed here.

## D8: timing pilot and focused seed family

No timing probe run: current physical ambient path refuses the inputs, so timing an unchanged producer would not
measure the proposed integration. No build was needed. The following is a future implementation recipe, not a
claim that a new CLI entry point already exists.

Build the headless probe and all referenced projects with both output and intermediate artifacts redirected under
task scratch (per-project artifacts layout, not one shared obj folder). Use existing cached packages; no installation.
One worker, Release, no desktop, seed 4711; Baseline and Sharp at 661.7, fixed pinned preparation budgets. Record
cold preparation separately, retain preparations for the pilot/family and report warm identity. Do not include
High-res 64k cold preparation in a short pilot.

Pilot cells: two modules × both incidence choices × .1 and 1 µSv/h, Cs source fixed at the repository default
scene geometry/activity; additional field zero and engine ambient-only controls. Start at a .25 s horizon, then
1 s and 10 s; stop each pilot cell at a predeclared 30 s wall cap and report the consumed live prefix if limited.
Warm up one excluded cell before timing. Repeat a measured cell three times for timing spread, not physics evidence.
Separate timers/counters for incident sampling+mask+gamma transport, scored site copying, device.Respond, pulse
AdvanceTo, per-line projection, and Services publication/decoding. Compare site-recording-only transport and full
physical producer with identical seeds/geometry (no extra transport RNG draws).

Report incident histories/s, scored inputs/s, µs/incident and µs/input, output conversions/s, assigned/unknown,
per-line window counts, proved horizon, peak allocations/working set, truth count, active pulse-input count,
achieved live-time/wall-time ratio. Exercise cancellation during a low-acceptance interval and a pending hold;
record latency, no partial envelope and successful deterministic Continue. Rate assertions are not latency guarantees.
Fit projected cost as incident histories × incident cost + scored inputs × response/pulse cost + publications ×
publication cost; dose and speed multiply work per wall second, not gamma efficiency. Account for missing inputs
and prefix transport in the extrapolation rather than treating all incident photons as readout hits.

Freeze family recipe and disjoint seeds before execution: 16 outer seeds initially, 32 only if a precision target
chosen from the timing pilot requires it (choose N before looking at confirmation results). Baseline+Sharp,
both bounds, Cs source at .1 µSv/h and matched zero-field control; one Cs+Co bank check per seed on Baseline.
Stage B extends the same recipe with BSR .5/1, with and without ambient; it is not automatically covered by A.
Engine ambient-only cells can test transport without promising Studio field-only Start. Reuse one declared fixed
bank per module to isolate acquisition variability; label results conditional on those preparations. Calibration
seed uncertainty requires a different, larger study and is not implied by this family.

Per seed collect incident count, scored ambient inputs, accepted source inputs, realised total inputs, conversions,
assigned/unknown for each line, in-window assigned, pending hold and observed horizon. Exact checks:
every consumed envelope produces one realised input; total observed inputs equal sum by origin; conversions
equal assigned+unknown per line; window counts equal independent enumeration of that line's stored assignments;
partitioned/continued facts equal uninterrupted facts. Do not require one conversion per input, nor additive
conversion counts when ambient is enabled: trigger, overlap and busy time change that population.

Statistical checks derive their own uncertainty:

- Incident count at fixed live time T is Poisson with mean IncidentRateCps × T. Use exact Poisson interval/tails
  for low populations; for sufficiently large means an explicitly justified k*sqrt(mean) check. Across N equal
  cells, pooled mean is N*rate*T, not the single-seed mean.
- For a fixed incident-history transport reference M, detection efficiency p has binomial variance p*(1-p)/M;
  use exact/Wilson intervals at sparse counts. Compare fixed-time scored count S to an **independent** fixed-M
  module-geometry estimate p_hat: variance includes rate*T*p and (rate*T)^2*p*(1-p)/M. Do not compare S/T to
  this run's DetectedRateCps as an independent oracle. Do not use EV-34 direct efficiency as physical truth.
- Plan pooled incident/scored counts from the pilot to obtain the requested relative SE (roughly 1/sqrt(count)
  for independent Poisson counts); for 1% SE this requires about 10,000 counts, before confidence multiplier.
  Use between-seed s/sqrt(N) and Student-t intervals for empirical rate/assignment/window summaries. Samples
  correlated by pile-up are not binomial trials: use outer-seed spread, not sqrt(p*(1-p)/hit count).
- Set a family error budget before running (e.g. .01 for J declared stochastic checks, each tail budget .01/J).
  Derive critical values from those distributions/sample sizes. Record J, N, M and counts with every interval.
  Source arrival gaps use an adaptive MC efficiency estimate, so no naive fixed-rate Poisson oracle for source.
  Stage-B schedule tests use integrated pre-trigger source-rate intensity or paired schedule fixtures, not
  BSR × measured assigned counts.

Order-of-magnitude estimate, unmeasured: cold Baseline+Sharp 662 preparation about 10^2 s from archived Turn7
`:25–28` (8.11 / 59.59 s); bounded pilot about 10^2–10^3 s; focused reused-bank 16–32-seed family about
10^3–10^4 s (tens of minutes to a few hours), depending on horizon selected for sufficient ambient counts and
module incidence correction. No measured current field rate or per-hit cost is claimed. Recompute the runtime
from the pilot; planner launches the long job and independently reruns one seed. Do not rebuild a cold bank per
acquisition seed or repeat the TODO-41 offer campaign.

## Risks and revised size

Main risks are mispaired arrays, premature observed-horizon advance, starvation while searching for a rare source
candidate, passive-material face handling, confusing transport rates with assigned rates, and losing per-line bank
validation/release on the new StartAmbient route. Preserve bounded calls even when the source candidate is rare;
Stop must remain responsive. Changed physical gap/passive construction changes ambient numbers by design.
Flood correction is not ambient subtraction and does not remove field Poisson noise. At higher dose/speed,
compact measured-history storage still grows despite capped truth; no new universal localisation or responsiveness
claim follows from these count checks.

Revised estimate in added or materially changed lines, not measured diff:

| Work | Stage A production | Stage A tests |
|---|---:|---:|
| Envelope, recording, nested merge/horizon delivery | 190–300 | 150–240 |
| Physical producer observation and component rates | 65–105 | 90–145 |
| Service/session/policy/shell/Options and compact captured construction description | 145–235 | 140–225 |
| D5 opt-in incoming-face passive convention/helper | 60–110 | 90–150 |
| Desktop scenario source and detached render fixtures | included above for production | 100–170 |
| **A total** | **460–750** | **570–930** |
| **B incremental, D2** | **150–250** | **190–310** |

A roughly 15–20 production/model files plus test files, B another 3–6 touched production files (many overlap A).
Compared with 490–790 / 450–740: remove general source-free physical preparation and rate/UI work; retain an
explicit D4 refusal, add Options/status integration and the incoming-face qualification, raise test allowance for
geometry and exact fixtures. If D5 instead retains passive-free all-face transport, subtract about 40–80 production
and 60–110 test lines, but revise the decision/claims. A full explicit passive/reflector volume simulation is outside
these numbers. B keeps conditional retries at scheduled detected-input times; poor acceptance increases runtime.
Evidence tooling 120–200 lines and documentation/catalog 160–260 lines are separate, shared A/B allowances.
No new Options dialog, arbitrary analysis-line editor, K-40 offer or calibration solver work is included.

## Commands that wrote anything / audit

Only one writing tool call: `tools.apply_patch`, adding `docs/PLAN.Studio.ReadoutField.Review.md` with this review.
No shell command wrote a file. No scratch artifacts, build/test/probe output, installs, environment changes,
desktop operations or Git state-changing commands were performed. Read commands were Get-Content / Select-Object /
ForEach-Object line-number formatting, rg file/text searches, `git --no-optional-locks status --short`,
`git rev-parse HEAD`, and `git diff 1ef455e HEAD --` the four transport files. Two exploratory rg calls used
nonexistent/glob paths and reported read errors; corrected discovery used `rg --files` and explicit filenames.
No forbidden memory/user directories or other worktree were accessed. The caller's requested `-o` capture is
outside the commands executed by this review.

## APPROVAL REQUESTS

None. Resolve the D5 modelling decision and calibration-convention wording in the plan before implementation;
no restricted command is required to complete this review.
