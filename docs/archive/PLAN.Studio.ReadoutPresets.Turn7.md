# PLAN.Studio.ReadoutPresets.Turn 7 — Studio design and preparation measurements

Date: 2026-10-12. Design/headless measurement only. No product implementation, desktop, pinned-input change or published-root write. Recommendations below are not applied author decisions.

## 1. Per-line calibration

ReadoutPreparationService.PrepareAsync takes optics/detector, no line set. Preset defaults to a 661.7 keV flood, 2000 histories/crystal and Legacy solve/LUT. Build materialises 144 * 2000 histories and stores one calibration behind a Guid. Independent validation is also 661.7, 144 * 200; its single-crystal diagnostics include all energies, not the confirmation's photopeak sample. SimulationService retrieves one artifact; PhysicalListModeSource permanently assigns Crystal/Energy from it; all windows filter those values. One 661.7 LUT therefore serves every window today, including 1332 windows.

V4 loops 122.06/661.7/1332.5 and builds calibration separately with that line's energy/budget. V5 contains only 661.7 with Wide 8k/High 64k. Optical purpose 1 is independent of line, while flood/response/noise keys include its offset. There is no cross-line equivalence test. DOI, Compton and shot-noise distributions differ;122 fails under these Studio modules despite 662/1332 success. Option(b), one LUT reused across lines, is not supported.

Recommend(a): prepare each offered analysis line present, not every incidental emission, at its pinned budget. Share immutable energy-independent optical/network artifacts, not LUTs/gains. Capture a successful bank at Start. Acquire held A/B/C/D, analogue values, raw X/Y and times once; each selected line derives its assignment and measured window from its own entry. Never dispatch by generating isotope/true energy or diagnostic sites. Overlapping views are not independent events and must not be summed into acquisition totals. Broadband All/spectrum/scalar waveform readings need a labelled reference-line calibration and are outside the mono-line cost scope.

Mixed scenes keep all emitted histories and pile-up. Selecting 122 as a spatial channel shows Unavailable — calibration refused on 32/32, no resolved spot structure, with literal failures in details. This is neither a zero-count image nor a662-LUT fallback. DirectCrystal remains explicitly available under existing Reset/mode rules. Incidental 122/unknown emissions must not block another selected offered channel; otherwise Cs X-rays and Co 1173 would block normal use. Unknown lines are not automatically offered.

## 2. Preparation cost and options

Literal current Prepare refuses all fine heads before work. Its API cannot separately request 1332 even on Baseline. No fine-head result below is presented as current-product preparation.

A scope question was sent early. With no reply, the stated assumption was an isolated proposed-service copy in scratch. It retains SimulationService.BuildConfig, PrepareAsync semaphore/cache and Studio density/labels/ReadoutPreparation construction. Changes: captured dimensions, requested line/pinned budget, fine-head Growth/Adjoint, DEV keyed streams and streamed calibration/validation. It is not the evidence probe or landed Studio code. It retains 200 histories/crystal all-energy single-crystal runtime diagnostics; these are separate from the confirmed 32-seed record.

One worker, fresh process per cell, DEV seed 41000001; optical purpose 1, line calibration purposes 2/3/4. Same-service repeat measures the actual cache and view identity. Whole-process PeakWorkingSet 64/PeakPagedMemorySize 64 are high-water marks. One seed on a busy host gives timing/memory, not a speed guarantee or validation ensemble.

| Preset | Line keV | Histories/crystal | Cold wall s | Peak working GiB | Peak committed GiB | Warm hit ms | Peaks / diagnostics |
|---|---|---|---|---|---|---|---|
| Baseline | 661.7 | 2000 | 8.11 | 0.076 | 0.052 | 0.054 | 144 / 144 |
| Baseline | 1332.5 | 2000 | 9.14 | 0.075 | 0.049 | 0.097 | 144 / 144 |
| Sharp | 661.7 | 2000 | 59.59 | 0.185 | 0.172 | 0.058 | 900 / 900 |
| Sharp | 1332.5 | 2000 | 68.00 | 0.171 | 0.150 | 0.058 | 900 / 900 |
| Wide FOV | 661.7 | 8000 | 177.45 | 0.538 | 0.548 | 0.056 | 1156 / 1156 |
| Wide FOV | 1332.5 | 2000 | 89.52 | 0.200 | 0.187 | 0.067 | 1156 / 1156 |
| High-res | 661.7 | 64000 | 2672.44 | 5.382 | 6.172 | 0.066 | 1936 / 1936 |
| High-res | 1332.5 | 2000 | 178.49 | 0.312 | 0.291 | 0.056 | 1936 / 1936 |

Actual current Baseline 661.7:8.325s, 144 peaks,288000 scored/91037 windowed; peak working 0.106 GiB, committed 0.081 GiB. Cache repeat 0.122 ms returns the same view. Current Sharp/Wide/High refuse in 0.0167/0.00245/0.00257s. Requested budgets/line are not accepted by that API.

A 100k-history retention pilot through Studio BuildConfig(High/GAGG) measured 10321456 bytes,103.21456 bytes/history and 148303 sites. Input-list extrapolation to 123904000 histories is 11.91 GiB, before the roughly 6.17 GiB calibration buffers and views. Combined estimate exceeds 18 GiB. This is not an observed full retained-flood run; that unsafe run was not attempted. Stream exact histories, never cut 64k.

Split cache keys: optics/network excludes line; calibration includes line/budget/window/method/numerics/seed/schema and every resolved physical input. Pin acquired entries. Today's two-entry cache can evict a costly High entry after a few switches; fix accounted-byte limits only after retained-size measurement. Optical reuse saves construction, not dominant calibration. Actual seed values belong to artifact-cache identity, not the calibration-offer lookup: the evidence samples a method/budget across seeds, and each local seed must still succeed. Preserve the matching Baseline661.7 legacy402-408 streams/results; declare independent stable streams for new entries. The harness DEV streams do not propose replacing those Baseline artifacts. Session reuse is measured; cross-line optical reuse remains an exact implementation proposal.

Recommend session-only now. Persisted optics/calibration introduces runtime writes, changing AGENTS.UiAutomation section 1. It needs an author decision, injected isolated root, model/format/checksum validation, atomic completed-entry publication, invalidation/size/cleanup policy and renewed audit; no implicit APPDATA/registry fallback. Background Prepare already uses Task.Run and cancellation. Real line/stage/history progress is required: streamed transport/readout occurs inside Calibration.Build, so today's adjacent Transporting/Finding labels followed by a long silence are misleading.

Live physical Imaging currently lacks the flood correction used in the cost study. Build already retains all triggered(X,Y,Sum) events. A future opt-in hook can count assignments through the finished LUT/gains and +/-15% line window from those same facts, exactly matching replay with the same RNGs. Verify histogram equality against replay before adoption. It can avoid another full High flood; its added pass is not measured/included above. No hook is implemented now. Apply local per-line weights before decode; keep integer raw counts separate. Empty bins are explicit, never divide or cap silently; allow only a labelled raw-diagnostic fallback if the author chooses.

## 3. Display and data

Detector shows per-preset/line experimental availability, 32/32 population, budget and v4/v5 source; refusal reason/details; then local Not prepared/Preparing/Ready/Failed state separately. Show line/module dimensions/pitch, virtual sensors/unlimited cells, identity/seed/policy, expected/actual peaks/fallbacks, progress/cost/cache hit and literal local failure. Raw/circuit/LUT views follow the selected bank entry and captured module. Distinguish confirmed 32-seed diagnostics from local 200-history all-energy checks.

Imaging links a compact reference card to a full table: readout; expected ideal-Direct exposure 1000/4000; isolated/100000 input hits/s; absolute source RMS(degrees), per-position bias(degrees), remote probability(%), F and 95% seed-cluster interval(n=8). Direct F=1. State exactly: F is an equal-fluence MSE ratio, not a time factor. Remote is radial angular error beyond cell/D radians; include all remote peaks in RMS. Approximate outer-cluster intervals do not guarantee rare-tail coverage.

Wide 662/4k isolated: Direct RMS/bias 0.51664/0.51108 deg; Anger 0.47053/0.44821 deg; F0.82946[0.82312,0.83584]. About 97.9% of Direct MSE is bias. Show: F below 1 is chiefly direct-reference bias under this decoder; it does not establish a general Anger advantage. Keep absolute measurements visible and F colour neutral.

Map exact engine emissions to line rows, not primary-isotope shortcuts or rounded energy labels. Snapshot.RateCps is expected scored-input rate; assigned/live-time is another quantity. Isolated means ProcessIsolated, not a live 0cps scene. Show both rates without nearest/interpolated qualification. Live Anger counts are not ideal-Direct exposure counts; show both benchmark exposures and current counts separately. Benchmark: one mono-line point source, nine positions at 0/+/-0.45FCFOV half extent at 1m, finite correlation/Tent, per-path flood correction,+/-15% windows, equal emitted fluence and 64 fresh trials/position. Other focus, source mixtures/sampling, windows or MLEM are reference-only/unmatched, without a performance block. Ideal Direct means true total-deposit window/largest deposit; optional Studio legacy chain/gain stays unchanged and is not that reference.

Proposed runtime file: samples/evidence/studio-readout/offer-cost-v1.json. Proposed generator: samples/evidence/studio_readout_offer_record.py, reading archived published confirmation-v4.json/repair-v5.json and publisher sidecars. Embed/deploy read-only bytes with a generated trusted SHA pin compiled into Studio; never use TEMP/download/write at runtime. Validate sidecars, schema/phase, recipe/rule identity, complete 32-seed populations, eight cost clusters, twelve unique cells and repair scope. Overlay exactly Wide 661.7 and High 661.7 entire cells from v5; retain v4 history/provenance, never pool populations. Copy floats/intervals/maps verbatim; display rounding is formatting. Generate fingerprints from resolved configuration. Check mode regenerates byte-for-byte and checks parent hashes/sidecars/pin; missing/corrupt inputs fail before publication. Runtime malformed/missing evidence is unavailable, no older-cell or fabricated-offer fallback.

This turn validates both parents and generates only a scratch candidate:12 cells, 8 offers,48 cost rows including refused-line Direct controls. It is not shipped/new judgement. Parent hashes:

- confirmation-v4.json:165280179215db6170e19dacb001d2c6b7724ad84fdd44e1c0b118f998fcc8ad
- repair-v5.json:4dc2a598a108ac8974b35a76c0ec884da70ac72afe3559a8ce67edb2bdfb3d61

Archive parents/sidecars with provenance in samples/evidence/results; propose generator --check --archives in CI and a runtime hash/schema test. Generate an averaged diagnostic sensitivity map only as a declared visual summary; keep the recorded weakest/median scalar and 32 maps. Those truth-referenced validation ratios are not local calibration flood weights and must never substitute for them.

## 4. Custom

Calibration fingerprint: canonical counts/pitch/gap/depth/material/absorbers, sensor layout/area/offsets, full optics/scintillation/SiPM/electronics/pulse/trigger/network, calibration line/method/window/numerical/seed/model version. Mask/distance/decoder are not needed for a normal-incidence module flood. A module match inherits offer OR refusal and budget regardless of Custom label. Matching High 662 cannot obtain a2k shortcut by choosing Custom.

Cost fingerprint additionally captures mask rank/pitch/mosaic/thickness/material attenuation/pose, D/focal distance, transport/secondary/background assumptions, decoder/grid/interpolation, line window/correction and the source/rate/exposure sampling scope. Module-only: Calibration inherited; imaging cost unmeasured for this configuration. Full reference match: inherited reference benchmark, not arbitrary-scene prediction. Nothing inherited: Locally calibrated — experimental, unvalidated; no confirmed offer or cost record. Failure shows its literal reason. Propose 2k fixed Growth/Adjoint for unmatched Custom; matching Baseline retains Legacy. This unknown-module policy is an author decision, not a cut to a confirmed budget.

## 5. Generalisation inventory

| File/member | Required change / intentional constant |
|---|---|
| src/Gcam.Studio.Services/ReadoutPreparationService.cs — Preset, Build, PrepareAsync | 144 * EventsPerCrystal, 144 * 200, arrays[144], DirectCrystal(...,12), CrystalY*12, c<144 and fixed 661.7 validation must follow captured Nx/Ny/product and line/budget. Use streamed floods. Preserve matching Baseline Legacy; select Growth/Adjoint for other confirmed modules. Cache/API need a line bank. |
| src/Gcam.Studio.Core/Services/ReadoutPolicy.cs — GeometryError, Key | Exact 12/rank 7/cell 1/D60/pitch 1 gate and baseline reason; key omits depth/material/line/complete readout/calibration policy. Replace with valid geometry plus evidence/Custom policy and canonical keys. |
| src/Gcam.Studio.Core/ViewModels/MainViewModel.Readout.cs and MainViewModel.cs — ReadoutMode, MatchingPreparation, PrepareReadoutAsync, Detector, OnOpticsChanged, constructor OpticsEditor.CanSelectPreset delegate, StartAsync | Baseline guard repeats in selection, optics changes and Start. One prepared Guid and fixed 403 text become bank/line state. Keep rejected values, Reset locks and ambient/BSR guards. |
| src/Gcam.Studio.Core/ViewModels/DetectorWorkspaceViewModel.cs — CalibrationState, Preparation, Diagnostics | Literal /144 peaks and fallback crystals; fixed seed text. Use captured expected crystal count and entry line/policy/seed. Do not infer geometry from pending controls or LUT-bin count. |
| src/Gcam.Studio.Services/PhysicalReadoutProjection.cs — Imaging, Spectrum | new DetectorImage(12,12), Crystal%12 and /12; MlemProjection 661.7. Use captured Nx/Ny and selected line. Add local flood correction. Spectrum filters permanently derived single-calibration energies today. |
| src/Gcam.Studio.Services/PhysicalReadoutWaveform.cs — Process | Crystal%12,/12 coordinates and singular preparation/scalar energy. Use captured Nx and labelled selected/reference calibration. Four outputs plus Sum remain intentional architecture. |
| src/Gcam.Studio.Services/AcquisitionSession.cs — constructor, ProduceAsync | Crystal%12,/12 deposition; singular prepared tuple/source and identity reconstructed from partial optics. Capture module/bank. _flood is already config-sized. Raw 256x256 and 128 scale are display resolution, not crystals; keep explicit raw-frame metadata. |
| src/Gcam.Studio.Services/SimulationService.cs — BuildConfig, Start; src/Gcam.Simulation/Readout/PhysicalListModeSource.cs — constructor, AdvanceUntil | Start recreates default Legacy readout policy; cached fine Adjoints policy would be rejected by ReadoutDevice prepared-network guard. Carry captured resolved policy. The list-mode source has one calibration; retain held facts and derive per-line views without truth dispatch. Preserve shared network at Start. |
| src/Gcam.Studio.Core/Services/ReadoutPreparation.cs, ReadoutSnapshot.cs, MeasuredReadoutRecord.cs, DetectorSettings.cs — record fields; IReadoutPreparationService.cs — PrepareAsync signature | No captured dimensions/sensor pitch/line/budget; one Guid and permanent Crystal/Energy. Add immutable bank/module identities and distinguish measured held facts from derived line assignments. |
| src/Gcam.Studio/Controls/ReadoutMapView.cs — OnRender.Node and SiPM loop | 144 sensors; node/width/cell division by 15 (12+3). Use captured sensor counts and physical pitch. Active width scales as activeWidth/pitch, not raw millimetres times cell. Use actual circuit nodes/resistors. |
| src/Gcam.Studio/Controls/ReadoutMapView.cs — OnRender density/labels/peaks, MapPeer | Per-bin rectangle drawing and label scans every render; Raw.ToArray allocation; fixed 6 px crosses. Cache raster/max/boundaries and declare captured line/dimensions in automation. Zoom/inspection needs approval. |
| src/Gcam.Studio/Views/ReadoutDetectorView.xaml — preparation/map/calibration bindings; tests/Gcam.Studio.UiTests/ReadoutScenarioTests.cs — SelectHead, Prepare, refusal/cancel/continuation scenarios | Singular bindings and fixed expectations need bank/offer/cost/captured-size coverage. Baseline preset definitions, default engine 12, unrelated 12 px spacing and cache capacity 12 remain intentional. |

Stage interfaces/captured models require changes as well as literals. Current raw 256x256, first 256 truth samples, four ADC channels, plot spacing 12 and MlemCache capacity 12 are not 12-crystal hard-codes.

Large-grid arithmetic: LUT sides 384/960/1088/1408 at 32 bins/crystal. High has 1,982,464 bins,1,936 peaks and up to 3.96M label-neighbour checks/render. MainWindow 1440x900 has 300 px side panels,12 px gutters and 12 px window padding: centre upper width 792 px(632 px at 1280 minimum), before inner padding/tabs/height limits. At an illustrative 600 px square,44 cells are 13.6 px wide(12.8 px with n+3 circuit padding), raw bins 0.426 px. Actual map bounds/frame latency are unmeasured without desktop. Cache raster/boundaries/max, retain full-resolution calibration, downsample only display, add zoom/inspection. Do not promise tiny labels or 16 ms from layout arithmetic.

## 6. Proposed record wording

| Row | Exact proposed wording |
|---|---|
| SR-DET-05 | Detector shall retain DirectCrystal as default and support experimental GAGG FourOutputAnger preparation for valid geometry. A confirmed preset/calibration line shall be offered only from a shipped, hash-checked record whose complete confirmation or declared repair population succeeded. Known refused lines shall show the measured reason and population. Preparation shall capture resolved module geometry, readout policy, line, numerical budget and calibration identity. Start shall capture an immutable bank of successful selected-line preparations, used by Detector, Imaging, Spectrum, Waveform and circuit views. IndependentSipm remains outside this Studio implementation. |
| SR-RUN-34 | Physical Start shall require a matching successful preparation for every selected analysis line and shall refuse a selected refused, unprepared, failed or stale line and nonzero ambient/BSR before transport, preserving entered values and displaying an actionable reason. Unselected incidental emissions shall remain in transport and shall not block other prepared selected channels. Custom shall follow SR-DET-11. Imaging-cost values and crystal diagnostics shall never gate an otherwise calibrated experimental readout. DirectCrystal inputs, seeded stream, defaults and microcell policy shall remain unchanged. |
| SR-DET-10 | Detector and Imaging shall display experimental calibration availability per preset/line, complete population, budget, source record and refusal messages, separately from local preparation state. The shipped descriptive table shall report both readouts' absolute angular source RMS, per-position bias magnitude and remote-peak probability, accepted counts and paired equal-fluence source-MSE ratio F with seed-cluster intervals at 1000/4000 expected ideal-Direct counts, isolated and 100000 scored input hits/s. Direct F is 1. Paired G, single-crystal photopeak mis-ID, sensitivity maps with weakest/median and multi-crystal share shall remain diagnostics. The table shall declare distance, source sampling, decoder, window, flood correction and population. It shall state: F is an equal-fluence MSE ratio, not a time factor. Wide 662 at 4000 counts shall explain the direct-reference bias behind F below 1. Unmatched scene/rate/decoder/window conditions shall be identified without inventing an interpolated cost or performance gate. |
| SR-DET-11 | Any valid Custom geometry may explicitly Prepare under a declared calibration policy; success permits experimental, unvalidated acquisition and failure shows its own measured reason. A canonical module/line/policy fingerprint matching a confirmed module shall inherit that line's calibration offer or refusal and pinned budget, irrespective of the Custom label. Module inheritance shall not imply source-imaging-cost inheritance. Reference cost may be inherited only when the complete imaging-profile fingerprint also matches, retaining its declared benchmark scope. Otherwise cost shall be labelled unmeasured for this geometry/configuration. Selected calibration banks and acquired geometry shall remain immutable; held ADC facts shall never be mapped using generating-line truth. |

**Theme 68 scope note.** The 0.406+/-0.006mm versus 0.367+/-0.001mm result is mean radial localisation error at equal accepted counts for one centred near-field lab source, not field-wide source RMS/equal-fluence cost at 1m. Independent reproduction matches ReadoutStudy event-for-event. Multi-crystal migration lets remote peaks dominate at low counts. TODO-41 calibrates per line and reports absolute costs; calibration is not an imaging-performance gate. Ordinary defaults remain DirectCrystal; Studio has an explicit signal-only physical path, currently Baseline-only before generalisation.

**EV-36 scope note.** Retain the original lab result/rule/conditions; its localisation is mean radial error, not TODO-41 RMS. The model also has an explicit signal-only Studio path(TODO-40). V4 confirmation plus final declared v5 repair supports experimental calibration offers for Baseline/Sharp/Wide/High at 661.7/1332.5, 32/32 each:2k except Wide 661.7 at 8k and High 661.7 at 64k.122.06 is refused on all heads. Costs use eight predeclared seeds,1k/4k ideal-Direct exposures, isolated/100000 input hits/s, per-path correction and finite correlation at 1m. Crystal diagnostics use 32 seeds. F is not time; Wide 4k F below 1 reflects direct-reference bias. Per-line UI generalisation remains pending. Saturation/recovery and arbitrary Custom source geometry are not validated.

**LIM-11 scope note.** Virtual matched sensors, assumed optical/reflector constants and unlimited cells remain assumptions, increasingly consequential at fine pitch. Numerical flood success does not validate an instrument or eliminate multi-crystal migration/remote peaks. Four module designs have experimental offers at 662/1332 after complete confirmation/repair;122 is refused. Show absolute RMS/bias/remote/F and scope, never a performance/duration promise. Wide 4k F below 1 is dominated by its direct-reference bias. High 66264k needs streamed floods and one worker; persisted reuse introduces a write surface. Current UI remains Baseline-only until this stage lands.

**Findings 69 proposed entry.** TODO-41 separates per-line experimental calibration availability from measured cost. V4 confirmed Baseline/Sharp 662/1332 and Wide/High 1332; four Wide 662 and one High 662 failures remain published. Final declared v5 repair fixed 8k/64k on DEV 4 and passed 32/32 fresh seeds each. Final evidence:8 offers,4 refusals at 122. Costs:8 fixed seeds, nine positions/1m, fresh paired histories, per-path correction, equal emitted fluence,1k/4k ideal-Direct counts, isolated/1e5 input hits/s. Wide 662 isolated F2.078[1.363,3.170]/0.8295[0.8231,0.8358]; High 4.668[3.063,7.115]/2.586[2.550,2.623]. Quote absolute error/direct bias; repair single-crystal mis-ID about 1.66%/7.9%, multi share 0.61/0.60. Only complete calibration gates offers. Theme 68 is narrower near-field equal-count evidence. Per-line banks, captured geometry, exact streamed preparation and generated hash-checked cost data require the author's Studio decisions; no product change here.

Turn 6 proposal blocks/SR rows are updated to final v5; historical tables/pins remain intact. No Findings/VV/main plan is edited.

## 7. Desktop scenarios and waits — not run

Replace Sharp unsupported-geometry refusal with known 122-line refusal; retain ambient/BSR value-preservation. Add Sharp 662 default preparation/geometry;1332 bank/window switch; Wide 8k/neutral F below 1; High 64k cold progress/tabs/44x44 corners; Custom same-module changed-mask calibration inheritance with no own cost; unknown Custom success/failure; missing/corrupt asset; stale bank after edits; same-session hits; Stop/Continue/Reset conservation and held facts across workspaces. Check captured geometry after pending edits, largest crystal index in waveform, fixed seeds/four lanes, assigned+unknown=triggered and unchanged Direct.

Preparation waits: max(existing 90s,2*measured wall+10s), rounded upward to whole minutes below. This is scheduling allowance, not physics tolerance or measured UI latency. One cold High scenario should bundle related checks and then use real session hits. Scenario.Run creates a fresh process per scenario, so separate High cases repeatedly incur cold cost. Existing cancellation 10s/ordinary interaction waits remain provisional until planner measures large global-pass/GC/cancel/redraw latency. Never extend waits to hide frozen work or relax assertions. Large-grid renders and broken-verdict/recovery run on author's desktop go only.

| Head / line | Cold wall s | Proposed Prepare timeout s |
|---|---|---|
| Baseline/661.7 | 8.11 | 90 |
| Baseline/1332.5 | 9.14 | 90 |
| Sharp/661.7 | 59.59 | 180 |
| Sharp/1332.5 | 68.00 | 180 |
| Wide FOV/661.7 | 177.45 | 420 |
| Wide FOV/1332.5 | 89.52 | 240 |
| High-res/661.7 | 2672.44 | 5400 |
| High-res/1332.5 | 178.49 | 420 |

## 8. Author decisions

| Decision | Recommendation | Reason |
|---|---|---|
| A1 Per-line policy | Adopt(a), prepare offered analysis lines present in scene. | No cross-line LUT/gain-equivalence evidence; validation is per line. |
| A2 Event/line dispatch | Capture bank; acquire ADC once; project each line using its entry. Let the user choose a labelled reference calibration for broadband readings. | Never choose a LUT using generating isotope/true energy. |
| A3 Refused/incidental/unknown lines | Refuse selected 122 spatial image; keep incidental emissions in transport. Unknown lines are not evaluated; explicit unvalidated experiments need a stated policy. | Blanket blocking would exclude Cs X-rays and Co 1173 while their offered lines remain useful. |
| A4 Flood correction | Opt-in exact histogram from already-held calibration events; verify against replay; keep raw counts distinct. | Live physical Imaging lacks the validated correction. Avoid a second long High flood. |
| A5 Empty correction bins | Show corrected image unavailable; permit only an explicit raw diagnostic view, no capped/divided weights. | Keep offer and correction failure separate; no performance gate. |
| A6 Session cache | Separate immutable optics/network and per-line artifacts; serialize; pin active bank entries; account bytes before choosing a cap. | Measured warm reuse is fast; current two-entry eviction can lose expensive High entries. |
| A7 Persistence charter | Session-only this stage; consider persistence separately if repeated cold waits are unacceptable. | Persistence needs explicit path/format/hash/invalidation/atomic writes and a renewed section 1 audit. |
| A8 Progress/readiness | Explicit background Prepare with real line/stage/history progress and cancellation; one worker; require selected entries ready. | 124M histories cannot appear stalled for tens of minutes. |
| A9 Custom scope | Module+line+policy inherits calibration, full imaging profile inherits only reference cost. | Mask, D, focus and decoder can change source error without changing the normal flood. |
| A10 Unknown Custom budget/method | Declare 2k and fixed Growth/Adjoint as unvalidated local policy; matching Baseline keeps Legacy; matching known cells keep 8k/64k. | Not a cut to confirmed budgets or an automatic post-failure repair. |
| A11 Scene/rate/exposure mapping | Show both benchmark exposures/rates; expected scored-input rate separate from assigned rate; no nearest-row interpolation or Anger-count-to-Direct mapping. | F is not a time factor and the benchmark is mono-line at nine sampled positions. |
| A12 MLEM/windows | Keep calibrated engineering controls with benchmark-not-applicable notices; MLEM uses selected energy. Refused 122 image stays unavailable. | Cost evidence is finite correlation/Tent and +/-15%; migration-aware MLEM remains Backlog. |
| A13 Data asset | Archive published parents/sidecars; generate read-only record plus trusted compiled hash; overlay exactly two cells. | No hand-entered costs, runtime TEMP lookup, download or write. |
| A14 Large-grid display | Cached raster/boundaries, actual sensor pitch, zoom/inspection; no labels on every cell. | High has 1.98M LUT bins and 1936 peaks; render performance is unmeasured. |
| A15 Desktop cost | One cold High scenario bundles checks and subsequent session hits; use preparation waits from measured cost. | Fresh-process-per-scenario would repeatedly pay the long prepare; redraw/cancel still require the desktop pilot. |
| A16 Ideal Direct wording | Retain DirectCrystal defaults and label the table ideal largest-deposit/true-energy reference. | Studio Direct may have a legacy chain/gain enabled; it is not that numerical reference. |

## Generated reference costs

Formatted directly from published parents with the declared two-cell overlay, not typed by hand. Estimate[lower 95,upper 95]; RMS/bias degrees; remote%; F unitless. Cost clusters 8, calibration/diagnostics 32. Zero-width remote intervals do not prove zero risk. These are benchmark values, not current-scene predictions.

| Preset | keV | Ideal-Direct counts | Input cps | Direct RMS deg; bias deg; remote % | Anger RMS deg; bias deg; remote % | F |
|---|---|---|---|---|---|---|
| Baseline | 661.7 | 1000 | isolated | 0.1636 [0.1632, 0.1641]; 0.1524 [0.1523, 0.1525]; 0 [0, 0] | 0.6258 [0.575, 0.6728]; 0.2964 [0.2667, 0.3234]; 5.078 [4.515, 5.642] | 14.63 [12.53, 17.08] |
| Baseline | 661.7 | 1000 | 100000 | 0.1636 [0.1632, 0.1641]; 0.1524 [0.1523, 0.1525]; 0 [0, 0] | 0.6804 [0.6381, 0.7202]; 0.3075 [0.2786, 0.3339]; 5.881 [5.092, 6.67] | 17.29 [15.37, 19.46] |
| Baseline | 661.7 | 4000 | isolated | 0.155 [0.1547, 0.1553]; 0.1523 [0.1523, 0.1524]; 0 [0, 0] | 0.1999 [0.1762, 0.2211]; 0.1583 [0.1575, 0.159]; 0.2387 [0.08477, 0.3927] | 1.664 [1.334, 2.074] |
| Baseline | 661.7 | 4000 | 100000 | 0.155 [0.1547, 0.1553]; 0.1523 [0.1523, 0.1524]; 0 [0, 0] | 0.2021 [0.1639, 0.2341]; 0.1598 [0.1577, 0.1618]; 0.2604 [0.003106, 0.5177] | 1.699 [1.21, 2.387] |
| Baseline | 1332.5 | 1000 | isolated | 1.652 [1.642, 1.662]; 1.22 [1.202, 1.236]; 24.59 [24.31, 24.87] | 2.091 [2.057, 2.125]; 1.796 [1.759, 1.832]; 45.4 [44.62, 46.18] | 1.603 [1.553, 1.654] |
| Baseline | 1332.5 | 1000 | 100000 | 1.652 [1.642, 1.662]; 1.22 [1.202, 1.236]; 24.59 [24.31, 24.87] | 2.117 [2.093, 2.141]; 1.801 [1.76, 1.842]; 46.61 [45.38, 47.85] | 1.643 [1.604, 1.684] |
| Baseline | 1332.5 | 4000 | isolated | 1.417 [1.415, 1.419]; 1.219 [1.217, 1.221]; 18.79 [18.69, 18.9] | 1.971 [1.955, 1.987]; 1.875 [1.862, 1.888]; 42.21 [41.68, 42.74] | 1.936 [1.907, 1.964] |
| Baseline | 1332.5 | 4000 | 100000 | 1.417 [1.415, 1.419]; 1.219 [1.217, 1.221]; 18.79 [18.69, 18.9] | 1.984 [1.962, 2.005]; 1.881 [1.86, 1.901]; 42.56 [41.73, 43.38] | 1.96 [1.918, 2.003] |
| Sharp | 661.7 | 1000 | isolated | 0.07758 [0.07739, 0.07778]; 0.06489 [0.06465, 0.06512]; 0 [0, 0] | 0.1045 [0.1034, 0.1055]; 0.06888 [0.06775, 0.06999]; 0 [0, 0] | 1.813 [1.777, 1.85] |
| Sharp | 661.7 | 1000 | 100000 | 0.07758 [0.07739, 0.07778]; 0.06489 [0.06465, 0.06512]; 0 [0, 0] | 0.1185 [0.09221, 0.1399]; 0.07028 [0.06888, 0.07165]; 0.0217 [0, 0.07302] | 2.331 [1.57, 3.462] |
| Sharp | 661.7 | 4000 | isolated | 0.06731 [0.06727, 0.06735]; 0.0641 [0.06406, 0.06414]; 0 [0, 0] | 0.09599 [0.0952, 0.09678]; 0.07506 [0.07392, 0.07618]; 0 [0, 0] | 2.034 [2.001, 2.067] |
| Sharp | 661.7 | 4000 | 100000 | 0.06731 [0.06727, 0.06735]; 0.0641 [0.06406, 0.06414]; 0 [0, 0] | 0.09763 [0.09658, 0.09868]; 0.076 [0.07495, 0.07703]; 0 [0, 0] | 2.104 [2.059, 2.15] |
| Sharp | 1332.5 | 1000 | isolated | 0.1035 [0.1034, 0.1037]; 0.07324 [0.07301, 0.07347]; 0 [0, 0] | 1.137 [1.098, 1.174]; 0.5671 [0.5198, 0.6107]; 13.26 [12.42, 14.1] | 120.5 [112.7, 128.9] |
| Sharp | 1332.5 | 1000 | 100000 | 0.1035 [0.1034, 0.1037]; 0.07324 [0.07301, 0.07347]; 0 [0, 0] | 1.219 [1.183, 1.254]; 0.638 [0.6102, 0.6647]; 16.36 [15.66, 17.06] | 138.7 [130.9, 146.9] |
| Sharp | 1332.5 | 4000 | isolated | 0.08768 [0.08748, 0.08787]; 0.07573 [0.07553, 0.07592]; 0 [0, 0] | 0.1961 [0.1461, 0.2358]; 0.09027 [0.08698, 0.09345]; 0.2604 [0.07038, 0.4505] | 5.004 [3.201, 7.822] |
| Sharp | 1332.5 | 4000 | 100000 | 0.08768 [0.08748, 0.08787]; 0.07573 [0.07553, 0.07592]; 0 [0, 0] | 0.2122 [0.1773, 0.2422]; 0.09106 [0.08934, 0.09274]; 0.3906 [0.262, 0.5193] | 5.86 [4.333, 7.926] |
| Wide | 661.7 | 1000 | isolated | 0.5088 [0.5082, 0.5094]; 0.4947 [0.494, 0.4954]; 0 [0, 0] | 0.7335 [0.5576, 0.8747]; 0.4244 [0.4065, 0.4417]; 0.651 [0.3242, 0.9779] | 2.078 [1.363, 3.17] |
| Wide | 661.7 | 1000 | 100000 | 0.5088 [0.5082, 0.5094]; 0.4947 [0.494, 0.4954]; 0 [0, 0] | 0.851 [0.7009, 0.9782]; 0.4138 [0.3973, 0.4296]; 1.042 [0.8222, 1.261] | 2.797 [2.027, 3.859] |
| Wide | 661.7 | 4000 | isolated | 0.5166 [0.5163, 0.517]; 0.5111 [0.5107, 0.5115]; 0 [0, 0] | 0.4705 [0.4685, 0.4726]; 0.4482 [0.446, 0.4504]; 0 [0, 0] | 0.8295 [0.8231, 0.8358] |
| Wide | 661.7 | 4000 | 100000 | 0.5166 [0.5163, 0.517]; 0.5111 [0.5107, 0.5115]; 0 [0, 0] | 0.4697 [0.4676, 0.4718]; 0.445 [0.4417, 0.4483]; 0 [0, 0] | 0.8267 [0.8187, 0.8347] |
| Wide | 1332.5 | 1000 | isolated | 0.6248 [0.5248, 0.7109]; 0.4651 [0.4562, 0.4739]; 0.2604 [0.1828, 0.338] | 5.36 [5.249, 5.469]; 2.902 [2.774, 3.024]; 30.69 [30.03, 31.34] | 73.58 [54.17, 99.94] |
| Wide | 1332.5 | 1000 | 100000 | 0.6248 [0.5248, 0.7109]; 0.4651 [0.4562, 0.4739]; 0.2604 [0.1828, 0.338] | 5.913 [5.797, 6.026]; 3.474 [3.317, 3.625]; 35.7 [34.66, 36.74] | 89.54 [67.09, 119.5] |
| Wide | 1332.5 | 4000 | isolated | 0.5208 [0.5203, 0.5212]; 0.5035 [0.5029, 0.5041]; 0 [0, 0] | 1.603 [1.43, 1.758]; 0.5456 [0.4504, 0.6264]; 3.819 [3.288, 4.351] | 9.471 [7.729, 11.61] |
| Wide | 1332.5 | 4000 | 100000 | 0.5208 [0.5203, 0.5212]; 0.5035 [0.5029, 0.5041]; 0 [0, 0] | 1.989 [1.826, 2.14]; 0.6603 [0.5723, 0.7379]; 5.382 [4.771, 5.993] | 14.59 [12.47, 17.07] |
| High | 661.7 | 1000 | isolated | 0.03632 [0.03628, 0.03637]; 0.03184 [0.03179, 0.03189]; 0 [0, 0] | 0.07848 [0.05963, 0.09361]; 0.03693 [0.03622, 0.03763]; 0.1302 [0.02758, 0.2328] | 4.668 [3.063, 7.115] |
| High | 661.7 | 1000 | 100000 | 0.03632 [0.03628, 0.03637]; 0.03184 [0.03179, 0.03189]; 0 [0, 0] | 0.07856 [0.05457, 0.09678]; 0.03654 [0.03581, 0.03726]; 0.1302 [0.001553, 0.2589] | 4.678 [2.784, 7.861] |
| High | 661.7 | 4000 | isolated | 0.03195 [0.03194, 0.03196]; 0.03081 [0.03079, 0.03083]; 0 [0, 0] | 0.05138 [0.05102, 0.05174]; 0.03754 [0.03719, 0.03788]; 0 [0, 0] | 2.586 [2.55, 2.623] |
| High | 661.7 | 4000 | 100000 | 0.03195 [0.03194, 0.03196]; 0.03081 [0.03079, 0.03083]; 0 [0, 0] | 0.05244 [0.05218, 0.05269]; 0.03769 [0.03744, 0.03794]; 0 [0, 0] | 2.694 [2.667, 2.721] |
| High | 1332.5 | 1000 | isolated | 0.04881 [0.04879, 0.04883]; 0.0364 [0.03634, 0.03645]; 0 [0, 0] | 0.5114 [0.4848, 0.5367]; 0.1716 [0.154, 0.1876]; 11.22 [10.26, 12.18] | 109.8 [99.19, 121.5] |
| High | 1332.5 | 1000 | 100000 | 0.04881 [0.04879, 0.04883]; 0.0364 [0.03634, 0.03645]; 0 [0, 0] | 0.5962 [0.5792, 0.6128]; 0.2152 [0.2011, 0.2285]; 15.28 [14.24, 16.32] | 149.2 [141.1, 157.8] |
| High | 1332.5 | 4000 | isolated | 0.04173 [0.04167, 0.04179]; 0.03759 [0.03755, 0.03763]; 0 [0, 0] | 0.06817 [0.0674, 0.06894]; 0.04612 [0.04532, 0.04691]; 0 [0, 0] | 2.669 [2.61, 2.729] |
| High | 1332.5 | 4000 | 100000 | 0.04173 [0.04167, 0.04179]; 0.03759 [0.03755, 0.03763]; 0 [0, 0] | 0.07065 [0.06945, 0.07183]; 0.04674 [0.04601, 0.04746]; 0 [0, 0] | 2.867 [2.775, 2.961] |

## Verification and write-command inventory

Scratch harness build passed 0 warnings/errors after a scratch-only interface rename correction. No product/desktop tests; no product source changed. Current and proposed paths are labelled separately. Parent outputs/sidecars stay read-only. A final byte audit covers protected files and permitted report edits; English/LF and no local absolute paths/personal names.

1. New-Item creates task studio 1 and process-local temp directories.
2. python -B - writes before.json, scratch project/source/scope, fixes interface rename, writes measure.ps1.
3. dotnet build StudioPreparePilot.csproj -c Release(two invocations: initial compile error, then success), allowed bin/obj/restore outputs only.
4. pwsh -NoProfile -File measure.ps1 invokes dotnet StudioPreparePilot.dll serially: four current calls,100k retention pilot,eight proposed cells; task JSON outputs; repeated cache checks inside calls.
5. python -B - validates publisher sidecars and writes candidate-studio-offer-cost.json. Long draft shell command failed before execution at Windows command-line length; the edit tool then created scratch build_turn7.py.
6. python -B build_turn7.py writes Turn 7, authorised Turn 6 proposal updates and scratch audit. Later report clarifications, if any, target only those two reports.

Retained measurement root is Join-Path $env:TEMP 'gcam-todo 41/studio 1'; exact completed command: pwsh -NoProfile -File (Join-Path $measurementRoot 'measure.ps1'). Project absolute reference stays only in scratch. One worker/background command, no watcher. No process remains running. No deletion/Git state change/process stop/install/desktop/upload/persistent environment change.

### APPROVAL REQUESTS

None. Architecture/charter decisions above need the author's judgement, not an irreversible command.
