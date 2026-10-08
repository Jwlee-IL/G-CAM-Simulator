# PLAN.Physics.RigReadout.Turn2 — stage 1 implementation report (substitute implementer)

Scope: TODO-19 stage 1 (engine + study, no Studio), implemented in the worktree on branch `todo19-readout` against
[PLAN.Physics.RigReadout](PLAN.Physics.RigReadout.md) "Decisions after review (2026-10-08)", RD-1 … RD-8. Report of the
turn; the planner verifies, records and commits. No git state was changed.

Status: implemented and verified locally (build 0 errors / 0 warnings in the new code, all tests pass); one-seed timing
pilot and a two-seed pilot through `run_seeds.py` done; long runs not started (planner's step).

## 1. Disagreements

None material; no RD row is contradicted. Three premise notes for the plan:

1. **RD-5 refers to "the existing saturation model" — the engine has none** (no SiPM microcell model anywhere before this
   turn). Stage 1 adds the no-recovery limit n_fired = N·(1 − e^(−n/N)) as an opt-in (`Sipm.MicrocellsPerMm2`, default
   off) and reports the expected photoelectrons per 50 µm cell; time-dependent recovery stays a limitation (RD-5).
2. **The plan's preset table says the geometry defaults are "set in Decisions after review"; RD-1 … RD-8 set none.** The
   study therefore sweeps geometry points from published practice (below); no engine default geometry was introduced.
3. **The review's optical prototype averaged the response over depth**, which hid a large effect: with its assumed
   diffuse 0.96 wall, depth-binned tracing gives collection 0.60 at the exit face falling to 0.37 at the entrance face of a
   10 mm pixel — a ≈ 36 % FWHM photopeak at 662 keV (measured, sanity probe, 3.2 mm pitch, ideal divider). That is far
   from what GAGG arrays are reported to resolve, so the optics default became a specular 0.98 reflector (multilayer film
   class, published ≈ 98 %), with the diffuse model kept as a swept geometry point. Planner decision requested (§ 9).

## 2. What was built (A–D)

**A. Interaction records and readout configuration.**
- `InteractionSite` (Core): XYZ, time after crystal entry (flight / c), energy, crystal index — recorded inside the
  Compton transport loop of `ComptonCrystalDetector` (new `interactionSink`), before and independent of the legacy light
  spread. Bookkeeping only: no random number is drawn for it.
- `ListModeSource(config, recordInteractions)`: `LastInteractions` = the sites of the last returned event (all photons of a
  cascade decay merged). Refused (explicitly) with an ambient field, a relative background or dark counts.
- `DetectorConfig.Readout` (`ReadoutConfig`, null-ignored on write so every existing configuration serialises
  byte-identically) with `Mode` {DirectCrystal (default), FourOutputAnger, IndependentSipm} and typed sections:
  `Sensors` (count, pitch, active width, offset; null = 1:1), `Optics` (indices, couplant / guide, wall R / T / surface law,
  top reflectance, bulk absorption, depth bins, photon budget), `Scintillation` (light yield, intrinsic FWHM), `Sipm` (PDE —
  not multiplied by microcell fill —, ENF, DCR per mm², integration window, microcells), `Network` (IdealBilinear /
  Dpc / CornerGrid, resistances, input impedance), `Digitizer` (bits, full-scale keV, ENOB, analogue noise, zero
  suppression), `Trigger` (Sum / Or / And, threshold, unit AdcCode / KeVEquivalent), `Pulse` (rise / tail, step, hold
  window, dead time, hold mode CommonAtSumPeak / IndependentPeaks), `Calibration` (line, budget, window, bins, smoothing,
  marker rules, Watershed / NearestPeak).
- Physics classes (Detector): `OpticalResponse` (photon tracer → crystal × depth-bin → sensor table, symmetry-reduced),
  `ChargeDivisionNetwork` (Kirchhoff nodal solve), `ReadoutDevice` (sites → photoelectrons → channels; position;
  digitisation), `ReadoutPulseProcessor` (time domain), `FloodLut` (LUT, explicit failure, peak-to-valley).
- Calibration / study (Simulation): `ReadoutFlood` (uniform normal-incidence transported flood), `ReadoutCalibration`
  (photopeak → LUT → per-crystal gains; train / test by independent floods), `ReadoutStudy` + request types,
  `ReadoutGuard` (the direct paths — `ListModeSource`, both factories' `CreateDetector`, `EventStreamStudy.Generate` —
  refuse a non-direct readout instead of silently ignoring it; stage 1 runs it in the study only).
- **Default path bit-identical:** pinned on fixtures recorded from the unmodified engine (aeac875) before any edit —
  list-mode streams (lab Cs-137, Co-60 cascade, gap + crosstalk + entrance + backing), the Compton runner image and the
  serialised scenario bytes — with and without interaction recording (`ReadoutDefaultPathTests`).

**B. Time-domain four-output model (RD-3).** Every channel = Σ_hits (channel charge) × u(t − t_hit), u the engine's
bi-exponential front-end pulse (Waveform.BiexpPulse form) with the default chain's constants from
`FrontEndParts.Default.PulseSamples` (rise 90 ns, tail 320 ns), normalised to 1 at an isolated pulse's sampled peak. So
overlapping pulses add in all four channels before the hold, and a piled-up event's Anger position is the light centroid
weighted by the pulse heights at the hold instant (the charge-weighted centroid for coincident pulses). Trigger on the
noiseless analogue waveform (8 ns grid, origin reset at an idle arrival) → hold window (default = time-to-peak) searched
for the sum peak (or each channel's peak) → digitise (noise + rounding) → busy for the dead time → re-arm only after the
condition drops (edge trigger). The legacy energy-only merge (`EventStreamStudy.ApplyPileUp` with
`ResolvingSamples(rise, tail)` = 730 ns) is reported beside it on the same hits. Approximations: § 5.

**C. Tests (RD-6)** — 7 new classes, 41 methods, 52 cases; § 4.

**D. Study (RD-7)** — `ReadoutStudy`, probe modes `readout-{pilot,timing,selection,validation}`,
`samples/evidence/readout/request-v1.json` (+ `request-pilot-v1.json`), `manifest-readout-v1.json`, seed lists
`RO_SELECTION16` / `RO_VALIDATION32` appended to `seeds.json`, `aggregate_readout.py`. § 6–7.

## 3. Files by group

| Group | Files |
|---|---|
| Core | `src/Gcam.Core/InteractionSite.cs` (new) |
| Configuration | `src/Gcam.Configuration/SimulationConfig.cs` (`DetectorConfig.Readout`); new `src/Gcam.Configuration/Readout/`: `ReadoutConfig`, `ReadoutMode`, `ReadoutSensorConfig`, `ReadoutOpticsConfig`, `ReflectorSurface`, `ReadoutScintillationConfig`, `ReadoutSipmConfig`, `ChargeNetworkConfig`, `NetworkTopology`, `ReadoutDigitizerConfig`, `ReadoutTriggerConfig`, `TriggerLogic`, `ThresholdUnit`, `ReadoutPulseConfig`, `HoldMode`, `FloodCalibrationConfig`, `FloodSegmentation` |
| Detector | `src/Gcam.Detector/ComptonCrystalDetector.cs` (interaction sink); new `src/Gcam.Detector/Readout/`: `CrystalArrayGeometry`, `SensorLayout`, `OpticalResponse` (+ `SensorShare`, `OpticalFate`), `ChargeDivisionNetwork`, `ReadoutDevice`, `ReadoutHit`, `ReadoutEvent`, `ReadoutPulseProcessor`, `FloodLut`, `PeakToValleyResult` |
| Simulation | `ListModeSource.cs` (recording, guard), `DefaultSimulationFactory.cs`, `ComptonFactory.cs`, `EventStreamStudy.cs` (guard); new `src/Gcam.Simulation/Readout/`: `ReadoutFlood`, `ReadoutCalibration`, `ReadoutStudy`, `ReadoutStudyRequest`, `ReadoutStudyGeometry`, `ReadoutStudyVariant`, `ReadoutStudyTrigger`, `ReadoutGuard` |
| Tests | new `tests/Gcam.Tests/`: `ReadoutDefaultPathTests`, `ChargeDivisionNetworkTests`, `OpticalResponseTests`, `ReadoutDeviceTests`, `ReadoutPulseProcessorTests`, `FloodLutTests`, `ReadoutStudyTests` |
| Evidence | `samples/evidence/probe/Program.cs` (modes), new `probe/ReadoutRecipe.cs`; new `samples/evidence/readout/{request-v1.json, request-pilot-v1.json, aggregate_readout.py}`; new `samples/evidence/manifest-readout-v1.json`; `samples/evidence/seeds.json` (two lists + `_rules_readout` appended after the last list; earlier bytes untouched); `samples/evidence/calibration-records.json` (seeds pin `1de7a0c6…` → `f0838e24…`) |
| Docs (generated / allowed) | `docs/VV.Gcam.Calibration.md` (rewritten by the generator: only the four `seeds.json` hash lines changed); `docs/VV.Tests.md` (7 new entries + the generated discovery rows; nothing else) |
| Report | this file |

`docs/PLAN.Physics.RigReadout.md` shows as modified in the worktree — the planner's updated plan, not touched here.

## 4. Tests: counts and every tolerance

`dotnet test Gcam.sln -c Release` (process-local TEMP/TMP): **before 815 passed / 28 skipped → after 867 passed / 28
skipped, 0 failed** (Gcam.Tests 481 → 533; Studio 211, Services 108 + 7 skipped, UiTests 15 + 20 skipped, Render 1
skipped — unchanged). Python `unittest discover`: 39 OK (1 skipped), unchanged. `calibration_record.py --check`: up to
date. `test_records.py check-catalog --discover`: 160 units (153 + 7), 0 incomplete. Build: 0 errors, 0 warnings from the
new code (the two xUnit analyzer warnings in untouched Studio service tests are pre-existing).

Mutation check (then reverted): dropping the intrinsic factor fails both covariance cases; summing only the first hit in
the waveform fails both pile-up cases — nothing else.

| Test (class) | Expectation | Tolerance | Derivation |
|---|---|---|---|
| Default path, recording on/off (ReadoutDefaultPath) | SHA-256 of pre-readout fixtures | exact | recorded at aeac875, same seeds / budgets |
| Recorded sites (ReadoutDefaultPath) | Σ E_site = event deposit; index from XY; arg-max = pixel | 1e-12·E | summation round-off ≤ n·ε·E, n ≤ 32 |
| DPC one row of two, R = r (ChargeDivisionNetwork) | 5/16, 3/16, 5/16, 3/16; general (2−α)/(3−α) | 1e-9 | hand-solved nodal equations; Cholesky round-off of ≤ ~200 nodes |
| DPC single row (ChargeDivisionNetwork) | right share ((k+1)R + r/2)/((S+1)R + r) | 1e-9 | series chain (each end grounded through r/2) |
| Corner grid 2 × 2 | 7/15, 3/15, 3/15, 2/15 | 1e-9 | hand-solved by symmetry |
| Full 12 × 12, all topologies | Σ_c W = 1, mirror symmetry, W ≥ 0 | 1e-9 | round-off |
| Black box, h = 1 / 5 mm (OpticalResponse) | Ω/4π = 0.24341 / 0.02631 (a = 3.0 mm) | ± 0.00384 / 0.00143 | 4 binomial σ, N = 200 000 |
| Normal-incidence Fresnel | T = 1 − (0.44/3.36)² = 0.98285 | ± 0.00116 | 4 binomial σ |
| TIR at 55° (> 50.2°), lossless box, fates | never detected; all detected; Σ fates = 1 | exact / 1e-12 | deterministic / counting |
| Symmetry classes | 21 (symmetric 12 × 12), 144 (offset) | exact | 6·7/2 triangle of the quadrant |
| Covariance, ideal & DPC (ReadoutDevice) | CodesPerPe²·Wᵀ[δ·ENF(μ+λ_d) + σ_int²μμᵀ]W; mean CodesPerPe·Wᵀμ | 4·√((Σ_aa Σ_bb + Σ_ab²)/(N−1)); 4·√(Σ_aa/N); N = 20 000 | normal-theory SE of sample covariance; Poisson-rounding variance 1/12 ≪ band |
| Conservation | Σ μ = PDE·LY·Σ E·collection; Σ channels = CodesPerPe·Σ μ | 1e-9 relative | round-off |
| Single-site ideal limit (ideal, DPC, independent) | LUT crystal = original, every crystal × depth | exact | noiseless, calibrated on the ideal response |
| Multisite (ideal divider) | Anger X = Σμξ/Σμ; crystal 2 (centroid 2.25), not arg-max 1 | 1e-12; exact | algebra of the bilinear divider; 0.25-crystal margin ≫ ‰ collection differences |
| Saturation | Q_k < N cells | exact | N(1 − e^(−n/N)) < N |
| Trigger truth table (ReadoutPulseProcessor) | Sum / Or / And on constructed vectors; keV → codes | exact | definition |
| Time domain vs shortcut | identical codes and hold time | exact | shared pulse shape: condition holds iff at the peak |
| Coincident pile-up | X = (X1Σ1 + X2Σ2)/(Σ1+Σ2) | 4/Σ (sum ± 2 codes) | rounding ½ code × 4 channels × |s − X| ≤ 2 |
| Delayed pile-up (40 ns) | held = Σ a_h u(t* − t_h), t* from an independent window scan | 4/Σ | as above |
| Support / busy tail / ordering | separate events; no second event; refusal | exact | structural |
| Synthetic LUT (FloodLut) | markers at true centres | 0.02 raw | 2 bins (2/192) + σ/√400 |
| Train vs test (synthetic; transported 662 keV) | equal mis-ID | 4·√(p̄(1−p̄)(1/n₁+1/n₂)) | difference of binomial fractions, pooled |
| Unresolved / merged / outside | explicit failure; −1 | exact | structural |
| Study (ReadoutStudy) | same seed → identical JSON (timing aside); paired event counts; 500 keV noise → failed calibration | exact | structural |

## 5. What is approximated (stated in the code)

- **Optics:** response averaged over the lateral position inside a crystal (depth binned, 8 bins); Fresnel only at the
  crystal exit face (couplant → guide → sensor window index-matched; TIR at couplant → guide counted lost); straight
  crossing of a transmitting wall (absorbed if it would leave the neighbour's face); light-guide edges absorbing; wall /
  ceramic optical constants are assumptions (no measured data); bulk absorption off by default. Table Monte Carlo noise is
  not calibrated out (it mimics depth-dependent collection): 100 000 photons per bin is converged (662 keV FWHM 6.86 /
  5.87 / 5.58 / 5.56 % at 5 k / 20 k / 100 k / 400 k).
- **Gamma transport:** unchanged homogeneous slab; a site in a reflector gap is attributed to its pitch cell's crystal
  (RD-5, walls not transported).
- **Scintillation / SiPM:** Poisson photons (no Fano factor), proportional light yield, intrinsic floor as one common
  factor per event; ENF as a Gaussian gain sum with exact first two moments (no explicit crosstalk / afterpulse
  branching); DCR scaled by area, baseline-subtracted by its mean; saturation only as the opt-in no-recovery limit.
  Occupancy for a 662 keV deposit at the exit face: 9.1 pe per 50 µm cell at 1 mm sensors, 2.5 at 2 mm, 1.2 at 3 mm —
  the 1 mm virtual sensor would be deep in saturation; stage-1 numbers are infinite-cell numbers.
- **Network / electronics:** DC charge fractions; all outputs share the shaping chain's pulse (no RC response, skew,
  droop, trigger jitter); ideal virtual-ground inputs by default; 3 keV-equivalent analogue noise per channel is the
  energy channel's value used as a labelled sensitivity value; ADC ENOB 11.8 (AD9648 preset).
- **Time domain:** 8 ns grid; a pulse leaves the waveform below 10⁻³ of its peak; dead time beyond re-arm 0 by default.
- **Calibration:** blind markers (no geometry assistance), photopeak window ±15 %, per-crystal gain = iterated window
  mean; the AND trigger and unresolved floods fail rather than being forced.
- **Study:** the direct reference windows the TRUE deposit (ideal energy); localisation runs through the lab scenario's
  mask at every geometry, so a geometry change also changes mask sampling (3.2 mm pitch: 0.5 pixel per mask-cell
  shadow) — compare readouts within a geometry, not across.

## 6. Pilot results and timing

**Two-seed pilot through the driver** (`readout_pilot_v1`, RO_SELECTION16[0:2], 2 geometries × 3 readouts × 2 triggers;
5 000-photon tables, so FWHM is ~1 point high): 2 runs, 0 failed, ≈ 20 s each; `aggregate_readout.py` validated the
provenance (2 896 keys). A direct probe re-run of seed 6 100 001 is identical to the driver's output apart from timing
fields. Selected means over the 2 seeds (Sum50 unless stated):

| Geometry / readout | Mis-ID single-crystal photopeak 122 / 662 / 1332 keV | Trigger acceptance 122 keV (Sum50 / Or100) |
|---|---|---|
| p3.2-g0.2 / Anger-Ideal | 0.377 / 0.0002 / 0 | 0.988 / 0.074 |
| p3.2-g0.2 / Anger-DPC | 0.424 / 0.0002 / 0 | 0.988 / 0.019 |
| p3.2-g0.2 / Independent-ZS3 | 0.129 / 0.0011 / 0 | 0.988 / 0.980 |
| p1.0-g0 / Anger-DPC (Or100 calibration failed on 1 of 2 seeds) | 0.414 / 0 / 0 | 0.981 / 0.007 |

**One-seed timing pilot of the full request** (seed 6 100 001; one seed — guidance, not evidence; 20 000-photon tables in
that run, before the default became 100 000): 60 of 140 readout × trigger calibrations succeed; every failure is
explicit — the equal-resistor DPC (hour-glass distortion, ≈ 102–116 of 144 peaks), the corner grid (≈ 41–48 of 144;
noiseless edge-to-centre spacing 0.314, matching the review's independent 0.3142), the unsuppressed independent readout
(144 × 3 keV noise, markers cannot be ordered) and every AND50 trigger (edge spots missing). Highlights at 3.2 mm pitch,
DPC, Sum50 (20 000-photon tables): 662 keV FWHM 5.9 %, 1332 keV 5.0 %, 122 keV 15.0 %; photopeak P/V median 69 (ideal divider 169); photopeak
disagreement with the direct arg-max 30–36 % at 662 keV (the review's 29–34 %), with the noiseless light centroid 4–6 %;
rate axis 1 k / 10 k / 100 k / 300 k / 1 M cps: throughput 0.92 / 0.92 / 0.85 / 0.71 / 0.37, piled fraction 0.001 /
0.009 / 0.08 / 0.22 / 0.54 (legacy merge 0.001 / 0.007 / 0.073 / 0.20 / 0.52), piled photopeak events placed in another
crystal than their dominant hit 0.21–0.29 against a noise-only baseline of 0.08 for unpiled photopeak events.
Diffuse 0.99 optics: collection 0.76 (specular 0.32) but 662 keV FWHM 11.3 % (depth dependence).

**Final timing (final code, full request, one seed, single process, 24 logical CPUs, other work running):**
416 s wall for seed 6 100 001 (`readout-timing`, exit 0; about one minute of it concurrent with a test run). Per
geometry: optical table 9–13 s (100 000 photons per bin; 24 s for the diffuse model, more bounces), transport ≈ 1 s,
the 28 readout × trigger calibrations ≈ 38–48 s, and the floods, localisation and rate axis the rest; 12 of the 28
calibrations succeed per geometry (the same 60 / 140 as in the first run). With the converged tables, 3.2 mm pitch, DPC,
Sum50: FWHM 14.9 % / 5.55 % / 4.6 % at 122 / 662 / 1332 keV.

## 7. Long-run commands and wall time (planner starts them)

```bash
dotnet build Gcam.sln -c Release
dotnet build samples/evidence/probe/Gcam.EvidenceProbe.csproj -c Release
python samples/evidence/run_seeds.py --out <dir> --manifest samples/evidence/manifest-readout-v1.json \
    --family readout_selection_v1 --jobs 16
python samples/evidence/readout/aggregate_readout.py --runs <dir> --family readout_selection_v1 \
    --out <dir>/readout-selection-v1.json
# after a default is chosen from the selection aggregate only:
python samples/evidence/run_seeds.py --out <dir> --manifest samples/evidence/manifest-readout-v1.json \
    --family readout_validation_v1 --jobs 24
python samples/evidence/readout/aggregate_readout.py --runs <dir> --family readout_validation_v1 \
    --out <dir>/readout-validation-v1.json
```

Estimate (24 logical CPUs; per-seed 416 s single-process; parallel slow-down not measured, assumed 1.3–2× from shared
cores and memory bandwidth): selection, 16 seeds in one wave at `--jobs 16` ≈ 10–15 min; validation, 32 seeds in two
waves (24 + 8) at `--jobs 24` ≈ 20–30 min; both ≈ 35–45 min. Memory per process estimated from the stored data (sites of
≈ 0.5 M histories per geometry, one geometry at a time) at well under 1 GB — not measured. Each run writes one
≈ 0.45 MB JSON (`stdout.txt`). Run on a committed tree if the provenance should show a clean source (a dirty tree is
allowed and recorded as dirty).

## 8. Proposed Findings / EV / doc wording (for the planner; numbers after the long runs)

- **Findings, new theme ("A physical four-output readout: light, network, trigger, pile-up — TODO-19 stage 1"):**
  "The engine now models the conventional Anger readout from published practice beside the direct crystal assignment:
  pre-optical interaction sites → a traced optical table (crystal × depth) → SiPM photoelectrons → a Kirchhoff-solved
  charge-division network → time-domain four outputs (pulses sum in every channel) → trigger / hold / 14-bit conversion →
  flood-map LUT. Measured over N seeds (RO_SELECTION16 / RO_VALIDATION32): single-crystal photopeak mis-identification
  122 / 662 / 1332 keV = …; disagreement with the direct arg-max at 662 keV = … (multi-crystal Compton histories are
  placed at their light centroid, by design); trigger acceptance by position … (a per-channel OR at 100 keV-equivalent
  accepts … of 122 keV events, only near the corners; a sum trigger …); flood P/V …; FWHM …; localisation error at equal
  counts vs direct …; piled fraction and mispositioning vs rate …. Deterministic structure: an equal-resistor DPC and a
  corner-drained grid (edge spacing 0.31 of the centre) cannot be calibrated blindly at 662 keV; an AND trigger loses the
  edge spots; 144 unsuppressed channels at 3 keV each cannot be calibrated, with 3σ zero suppression they identify 122 keV
  crystals ≈ 3× better than four outputs. Optics are assumptions: the reflector law decides the energy resolution
  (diffuse 0.96: ≈ 36 % at 662 keV from depth-dependent collection; specular 0.98: ≈ 5.6 %)."
- **EV (new row, after validation):** "EV-nn — Four-output Anger readout vs direct assignment (TODO-19): …" quoting the
  validation aggregate only, with the seed list and the request hash.
- **Limitations (new row):** "LIM-nn — The physical readout's optics, SiPM recovery and network impulse response are
  modelled from assumptions (no measured reflector data; no microcell recovery; DC network). Fix: measured optical and
  device data; cost: a bench measurement campaign."
- **AGENTS.md (solution layout, Detector / Simulation rows):** add "`Readout/` — physical readout (OpticalResponse,
  ChargeDivisionNetwork, ReadoutDevice, ReadoutPulseProcessor, FloodLut)" and "`Readout/` — ReadoutFlood,
  ReadoutCalibration, ReadoutStudy (TODO-19)"; Notes & gotchas: "`Detector.Readout` other than DirectCrystal is honoured
  only by ReadoutStudy in stage 1; the direct paths refuse it (`ReadoutGuard`)".
- **VV.Tests "At a glance":** "160 units: 125 xUnit classes, 2 unittest classes, 29 cocotb configurations, 4 checkers"
  (the counts line still says 153 / 118; only the new entries and the generated discovery block were edited).
- **samples/evidence/README.md file table:** add `manifest-readout-v1.json`, `readout/` (requests, aggregator).

## 9. Decisions the planner / author should take

1. Optics default specular 0.98 instead of the review's diffuse 0.96 (§ 1.3) — or make the diffuse model the default
   and accept its ≈ 36 % photopeak as the assumption's consequence.
2. DPC default resistance ratio column/row = 0.1 (equal resistors give the hour-glass flood above and fail calibration;
   both are in the request).
3. Whether the validation family should run the full matrix or only the configuration chosen from the selection
   aggregate (the request supports either; the manifest currently runs the full matrix).

## 10. Every command that wrote anything

- `dotnet build` / `dotnet test` of `Gcam.sln`, the test project, the Simulation project and the evidence probe (bin / obj
  in the worktree; NuGet cache), each with process-local `TEMP`/`TMP` = `%TEMP%\gcam-todo19\runtime`.
- Repository files written through the file tools or small Python edit scripts (LF): every file in § 3.
- `python samples/evidence/calibration_record.py` (generator; rewrote the four `seeds.json` hash lines of
  `docs/VV.Gcam.Calibration.md`), then `--check`.
- `python samples/testing/test_records.py check-catalog --discover --update-discovery` (generated discovery rows in
  `docs/VV.Tests.md`), then `check-catalog --discover`.
- `mkdir` of `src/Gcam.Configuration/Readout`, `src/Gcam.Detector/Readout` (by the Write tool),
  `src/Gcam.Simulation/Readout`, `samples/evidence/readout`.
- Scratch under `%TEMP%\gcam-todo19\`: `fixture/` (baseline-hash probe), `sanity/` (physics sanity probe, flood PNGs),
  `pilot1/`, `timing/`, `timing2/` (copies of the probe output directory, `cp -r`), `runs/`, `runs-final/`, `runs-final2/`
  (`run_seeds.py` outputs), aggregates, two catalog-edit scripts, `runtime/`.
- `sed -i` on `samples/evidence/readout/request-pilot-v1.json` with a pattern that matched nothing (file unchanged;
  the edit was then made with Python).
- **A stray `rm -rf /dev/null 2>/dev/null`** slipped into one command line (while copying the probe binary). `/dev/null`
  is Git Bash's virtual device: nothing was deleted (it still exists, checked with `ls -la /dev/null`), but it is a delete
  command the guard reserves for approval — reported here for the audit.
- Read-only: `git status / diff / log`, `tasklist`, `nproc`, the main tree's uncommitted
  `samples/evidence/depthlik/seeds.json` (read once to keep the new seed lists disjoint from it).

## APPROVAL REQUESTS

None required to use this turn's work. (No deletion, git state change, install or process stop was needed; the stray
`rm` above deleted nothing and needs no undo.) The background timing run finished before this report was closed.

## Turn 3 (2026-10-08) — RD-9 … RD-13

Same constraints as turn 2. No delete-class command, no git state change, no install; ngspice not used.

### RD-11 / RD-12 / RD-13

- **RD-11 (optics default specular 0.98).** Already the engine default (`ReadoutOpticsConfig`); the diffuse 0.99 model
  stays a compared geometry point (`p3.2-g0.2-diffuse`). Model finding to record: with the review's diffuse 0.96 wall,
  depth-binned tracing gives collection 0.60 → 0.37 from exit to entrance face of a 10 mm pixel and a ≈ 36 % FWHM
  photopeak at 662 keV (diffuse 0.99: 11.3 %; specular 0.98: 5.6 %). Realism limitation: both surface laws are bounding
  models without measured reflector data (a perfect specular box traps the light outside the escape cone; a diffuse box
  makes collection depth-dependent).
- **RD-12 (DPC ratio swept).** The selection request now has the discretised positioning circuit at column / row
  resistance ratios **1.0, 0.3, 0.2, 0.1, 0.03, 0.01**. Derivation, from the engine-exported charge fractions (noiseless,
  12 × 12): the minimum middle-row spot spacing relative to the ideal 2/S is 0.15 / 0.43 / 0.54 / 0.69 / 0.84 / 0.89 at
  those ratios (middle-row / edge-row span 0.24 / 0.57 / 0.67 / 0.81 / 0.94 / 0.98), approaching the separable limit
  S/(S + 1) = 0.923. The LUT's marker suppression radius is 0.5 of the nominal spacing, so blind calibration was
  predicted to fail between 0.3 and 0.2. Set: 1.0 = equal resistors, the known failure; 0.3 / 0.2 either side of the
  predicted boundary; 0.1 (the previous default); 0.03; 0.01 within 4 % of the limit. Measured, one seed (timing run,
  Sum50, all five geometries): 1.0, 0.3 **and 0.2** fail calibration (101–108 / about 175 / 187–198 candidates for 144
  crystals), 0.1, 0.03 and 0.01 succeed — the real boundary lies between 0.2 and 0.1; the 0.5-spacing criterion is
  necessary, not sufficient (spot width and the regularity check also act). Pilot (2 seeds): 0.3 fails, 0.1 succeeds on
  both geometries. If the selection should bracket the boundary more finely, 0.15 is the natural addition (+1 variant).
  The table and the reasoning are also in `docs/HW.Readout.md`.
- **RD-13 (validation = full matrix).** `readout_validation_v1` runs `request-v1.json` in full on RO_VALIDATION32; the
  manifest's text says so. No code change needed.

### RD-9 schematic and RD-10 netlist

- `montecarlo readout-export <scenario> <request> <geometry> <readout> <trigger> <prefix>` (new CLI command,
  `ReadoutCommands`; `ReadoutExport` in Simulation): resolves one geometry × readout × trigger exactly as the study does
  and writes `<prefix>.config.json` (every value the drawing shows, after the engine's defaults, plus the engine's DC
  charge fractions, round-trip exact) and `<prefix>.cir` (SPICE netlist of a solved circuit: `I_S<k> 0 <node> DC 0` per
  SiPM, the resistor graph, `V_<c> OUT_<c> 0 DC 0` ammeters for virtual-ground inputs or `R_LOAD_<c>`). The network keeps
  its circuit (`ChargeNetworkCircuit`, `ToSpice`); the ideal divider has none.
- `samples/readout/schematic.py` (stdlib): SVG from the config — crystal / SiPM grid to scale (pitch vs wall), the
  network in the generic published form of its topology (DPC: row chains into two column chains; corner grid: mesh with
  corner drains), outputs A–D, preamp / shaper (pulse constants), four 14-bit ADCs, FPGA (sum trigger, hold, Anger ratio,
  LUT, energy). Labelled illustrative, not a buildable design.
- `samples/readout/netlist_check.py` (stdlib, no engine call): parses the netlist, Gaussian elimination with partial
  pivoting (the engine uses Cholesky), output currents per 1 A injection; bound per solver ‖g_out‖₁·‖v‖∞·(k/(1 − k) + γ_d),
  k = 2·γ_{3n}·κ∞(G), γ_m = m·u/(1 − m·u), u = 2⁻⁵³ (Higham Thm 7.2, growth ≤ 2 for a diagonally dominant matrix),
  doubled for two solvers.
- `samples/readout/regenerate.py` rebuilds `docs/assets/readout/*` (verified byte-identical on a repeated run);
  `samples/evidence/tests/test_readout_netlist.py` (`ReadoutNetlistTests`, 4 tests): hand-solved circuits for the Python
  solver, and the committed netlists against the engine within the bound with the stored reports reproduced exactly.
- Page: `docs/HW.Readout.md` (embeds both SVGs, the comparison table and the RD-12 table). Name proposal: DESIGN.* is
  scoped to Studio in AGENTS.Conventions.Docs — rename to `PAPER.Readout.md` (or widen DESIGN's scope) if preferred.

| Netlist (`docs/assets/readout/`) | Unknowns | κ∞(G) | Derived bound | Max deviation | Max abs(Σ − 1) |
|---|---:|---:|---:|---:|---:|
| `readout-p3.2-g0.2-dpc-r0.1.cir` | 168 | 1.50e3 | 1.14e-8 | 2.4e-15 | 6.9e-15 |
| `readout-p3.2-g0.2-dpc-r1.0.cir` | 168 | 1.01e3 | 1.09e-9 | 6.7e-15 | 7.1e-15 |
| `readout-p3.2-g0.2-grid.cir` | 144 | 6.29e2 | 1.59e-10 | 1.0e-15 | 4.0e-15 |

### Verification (turn 3)

Build: 0 errors (the only warnings are the two pre-existing xUnit analyzer warnings in untouched Studio service tests).
`dotnet test Gcam.sln -c Release`, process-local TEMP/TMP: **868 passed / 28 skipped / 0 failed** (Gcam.Tests 533 → 534:
`SolvedCircuit_ExportsAsSpice_IdealDividerHasNone`). Python unittests 39 → **43** OK (1 skipped).
`calibration_record.py --check` up to date (seeds unchanged this turn). `check-catalog --discover`: **161 units**
(ChargeDivisionNetworkTests entry updated, `ReadoutNetlistTests` entry added, discovery rows regenerated).

Pilot re-run (the pilot request changed: DPC r0.1 and r0.3): `readout_pilot_v1`, 2 / 2 OK, about 22 s each; the
aggregator validated provenance (2 931 keys). Timing run of the updated full request (10 readouts): **535 s** for seed
6 100 001, single process, exit 0; 100 of 230 readout × trigger calibrations succeed (every failure explicit).

### Updated long-run commands and estimate

```bash
dotnet build Gcam.sln -c Release
dotnet build samples/evidence/probe/Gcam.EvidenceProbe.csproj -c Release
python samples/evidence/run_seeds.py --out <dir> --manifest samples/evidence/manifest-readout-v1.json \
    --family readout_selection_v1 --jobs 16
python samples/evidence/readout/aggregate_readout.py --runs <dir> --family readout_selection_v1 \
    --out <dir>/readout-selection-v1.json
python samples/evidence/run_seeds.py --out <dir> --manifest samples/evidence/manifest-readout-v1.json \
    --family readout_validation_v1 --jobs 24
python samples/evidence/readout/aggregate_readout.py --runs <dir> --family readout_validation_v1 \
    --out <dir>/readout-validation-v1.json
```

Estimate (24 logical CPUs, 535 s per seed single-process, parallel slow-down assumed 1.3–2×, not measured): selection,
16 seeds in one wave, about 12–18 min; validation, 32 seeds in two waves (24 + 8), about 25–35 min; together about
40–55 min. Memory per process estimated well under 1 GB (not measured); about 0.7 MB JSON per run.

### Files (turn 3)

New: `src/Gcam.Detector/Readout/ChargeNetworkCircuit.cs`, `src/Gcam.Simulation/Readout/ReadoutExport.cs`,
`src/Gcam.Cli/Commands/ReadoutCommands.cs`, `samples/readout/{schematic.py, netlist_check.py, regenerate.py}`,
`samples/evidence/tests/test_readout_netlist.py`, `docs/HW.Readout.md`, `docs/assets/readout/` (3 × `.cir`,
`.config.json`, `.comparison.json`; 2 × `.svg`). Changed: `ChargeDivisionNetwork.cs` (keeps its circuit),
`src/Gcam.Cli/Program.cs` (command and usage line), `samples/evidence/readout/request-v1.json` (DPC ratio set, text),
`request-pilot-v1.json` (DPC r0.1 / r0.3), `manifest-readout-v1.json` (hashes, RD-12 / RD-13 text),
`tests/Gcam.Tests/ChargeDivisionNetworkTests.cs` (+1 test), `docs/VV.Tests.md` (that entry, the new unittest entry,
generated rows), this file. Proposed doc wording beyond § 8: AGENTS.md study table row "`readout-export` | engine-resolved
readout configuration + DC charge fractions + SPICE netlist of the network (TODO-19) → `<prefix>.config.json`, `.cir`";
samples/evidence/README.md is unaffected; a `samples/readout/README.md` map can be added if wanted.

### Every command that wrote anything (turn 3)

`dotnet build` (solution, CLI, Detector, probe) and `dotnet test`, with process-local TEMP/TMP; file-tool writes and
small Python edit scripts on the files above; `montecarlo readout-export` into scratch (`%TEMP%\gcam-todo19\export`,
`ratio`) and, through `regenerate.py` (run three times), into `docs/assets/readout/`; `netlist_check.py --write` and
`schematic.py` (through `regenerate.py` and once on scratch); scratch preview renders (`svgpreview.py`, matplotlib PNGs
in scratch); `test_records.py check-catalog --discover --update-discovery`; `run_seeds.py` into
`%TEMP%\gcam-todo19\runs-turn3` and the aggregator into scratch; `cp -r` of the probe output to
`%TEMP%\gcam-todo19\timing3\bin` and the timing run there; `mkdir` of scratch folders and (through the tools)
`samples/readout`, `docs/assets/readout`. Two shell heredocs failed to parse (nothing ran); their content was written
with the file tool instead.

### APPROVAL REQUESTS (turn 3)

None.

## Turn 4 (2026-10-08) — judgement of the long runs, evidence files, proposed records

Inputs (run by the planner, no turn open): `readout_selection_v1` (RO_SELECTION16, 16 / 16 OK) and
`readout_validation_v1` (RO_VALIDATION32, full matrix per RD-13, 32 / 32 OK), aggregated by `aggregate_readout.py`.
Seed = cluster; SE = SD/√N over seeds. No new long run; no C# change this turn.

### 1. Decision rule — fixed before reading either aggregate (11:31:27, scratch `turn4-rule.md`)

Unit = geometry × readout × trigger; DirectCrystal is the ideal reference, not a candidate.
- **Eligible:** calibration succeeded on every selection seed.
- **Constraints (selection means):** K1 122 keV flood acceptance ≥ 0.90 and per-crystal minimum ≥ 0.50 (no blind
  crystals); K2 1332 keV acceptance ≥ 0.90 and single-crystal photopeak mis-ID ≤ 0.002; K3 662 keV FWHM ≤ 0.08.
- **Objective, lexicographic, keeping every unit within 2 combined SE of the best at each step:** L1 662 keV
  single-crystal photopeak mis-ID; L2 662 keV localisation penalty at equal counts against the geometry's own direct
  reference (pairing ignored — conservative); L3 122 keV photopeak mis-ID; L4 photopeak events mispositioned by pile-up
  at 1e5 cps (pooled mispositioned-piled fraction × piled share), then at 3e5 cps; ties: 4 channels before 144, lower
  threshold, request order.
- **Outcome statement:** the winner is the stage-1 physical readout preset; DirectCrystal stays the engine default
  unless the winner is a physical readout AND validation confirms it — and even then switching the engine default is a
  stage-2 (Studio) decision, because it changes every existing result path.
- **Validation (rule unchanged):** V1 eligible on 32 / 32 and K1–K3 on validation means; V2 within 2 combined SE of the
  validation best on L1 and L2 among the selection's L1 survivors; reversals reported, the choice not changed.

**Amendment A1 (11:32:48 — after reading selection, before reading validation).** The literal rule selects
`p1.0-g0 / Anger-Ideal / Sum50`; Anger-Ideal is the analytic bilinear divider, a reference and not a circuit, which the
candidate set should have excluded. A1 = the same rule over realisable readouts. A1 is post hoc with respect to the
selection seeds (not the validation seeds); both winners are checked on validation. The rule and A1 are implemented in
`samples/evidence/readout/select_readout.py`.

### 2. Selection (16 seeds)

99 eligible units → 44 pass K1–K3 → L1 17 → L2 13 → L3 → L4.
- Literal: **p1.0-g0 / Anger-Ideal / Sum50**.
- **A1 (realisable): p1.0-g0 / Anger-DPC-r0.01 / Sum50** — FourOutputAnger, DPC column / row ratio 0.01, sum trigger
  50 keV, specular 0.98 reflector, 1.0 mm pitch with matched 1 mm (virtual) SiPMs.
- What each step removed: K1 removes every OR trigger (per-channel OR leaves blind crystals at 122 keV; at 1 mm, DPC
  r0.01: OR50 per-crystal minimum 0.00, OR100 accepts 1.2 %); K3 removes the diffuse reflector (FWHM 11 %) and the 1 mm
  zero-suppressed independent readout (8.5 %); **L1 removes IndependentSipm-ZS3** (662 keV mis-ID 0.0013–0.0016 against
  0.00014–0.0003 for four outputs) — although it is ≈ 3× better at 122 keV (0.13 against 0.35–0.43); L1 keeps all
  p1.0 four-output units and a few p2.2 / p3.2-g0.5 units (the geometry is decided at the ≈ 1e-4 mis-ID level, about
  2 SE); L3 ranks the DPC ratios (122 keV mis-ID r0.01 0.377, r0.03 0.380, r0.1 0.412 at 1 mm); L4 prefers Sum50 to
  Sum100 (pile-up-mispositioned photopeak fraction 0.0158 against 0.0246 at 1e5 cps).
- Never eligible (every seed, both families, every geometry): DPC ratio 1.0 / 0.3 / 0.2, the corner grid, the
  unsuppressed independent readout, every AND50 trigger (explicit calibration failures).

### 3. Validation check (32 seeds, rule unchanged)

| Check | Literal (Anger-Ideal) | A1 (Anger-DPC-r0.01) |
|---|---|---|
| V1 eligible 32 / 32 and K1–K3 | pass | pass |
| V2 L1 within 2 SE of best | pass (it is the best, 0.00013 ± 0.00002) | pass (0.00021 ± 0.00004 vs best 0.00016 ± 0.00003) |
| V2 L2 within 2 SE of best | pass (+0.040 ± 0.005 mm vs best −0.145 ± 0.102) | pass (+0.039 ± 0.006 vs best −0.140 ± 0.135) |
| **Confirmed** | **yes** | **yes** |
| Rule re-run on validation alone (information only) | same unit | **p3.2-g0.5 / Anger-DPC-r0.03 / Sum50** |

The A1 winner, selection → validation (mean ± SE over seeds):

| Metric | Selection (16) | Validation (32) |
|---|---|---|
| 662 keV single-crystal photopeak mis-ID | 0.00014 ± 0.00003 | 0.00021 ± 0.00004 |
| 662 keV localisation at equal counts, readout / direct (mm) | 0.398 ± 0.006 / 0.364 ± 0.002 | 0.406 ± 0.006 / 0.367 ± 0.001 |
| localisation penalty (mm) | +0.034 ± 0.006 | +0.039 ± 0.006 |
| 122 keV photopeak mis-ID | 0.377 ± 0.001 | 0.375 ± 0.001 |
| 122 keV acceptance / weakest crystal | 0.981 / 0.937 | 0.981 / 0.935 |
| 1332 keV acceptance / mis-ID | 0.968 / 0 | 0.968 / 0 |
| FWHM 122 / 662 / 1332 keV | 0.149 / 0.059 / 0.050 | 0.149 / 0.059 / 0.049 |
| 662 keV disagreement with the direct arg-max (photopeak) | 0.359 | 0.359 |
| min flood P/V at 662 keV | 16.7 ± 0.9 | 16.5 ± 0.6 |
| photopeak mispositioned by pile-up, 1e5 / 3e5 cps | 0.0158 / 0.0414 | 0.0156 / 0.0430 |
| throughput / piled fraction at 1e6 cps | 0.385 / 0.544 | 0.388 / 0.542 |

**Confirmations.** The network and trigger choice is robust: DPC ratios ≤ 0.1 calibrate on every seed at every
geometry, except r0.1 at 1 mm, which failed 1 of 32 validation calibrations, so r0.1 is marginal and 0.03–0.01 are
safe. The sum trigger is the only trigger family meeting K1. Four outputs beat independent channels at 662 keV and lose
at 122 keV in both families.

**Reversals, reported.** (a) The geometry is not stably ranked: on validation alone the same rule would pick 3.2 mm pitch
with a 0.5 mm wall and DPC r0.03; the geometry ranking rests on 662 keV mis-ID differences of ≈ 1e-4 (about 2 SE). (b)
The localisation penalty of the 1 mm units is small but significant (+0.039 ± 0.006 mm, about 11 % of 0.37 mm); it
passes V2 only because the 3.2 mm units' penalties are noise-dominated (SE 0.1–0.2 mm; that geometry samples the mask
at 0.5 pixel per cell and localises multimodally). (c) The 1 mm geometry uses virtual 1 mm SiPMs that would receive about
9 photoelectrons per 50 µm cell at 662 keV — deep in saturation, while the study assumes infinite cells; the 3.2 mm
geometry sits at about 1.2. **The rule's geometry choice is therefore not physically trustworthy until saturation and
recovery are modelled.**

**Recommendation.** Keep **DirectCrystal** as the engine default (stage 1 is study-only; the switch is a stage-2
decision). Record the physical readout preset as **FourOutputAnger, DPC column / row ratio 0.01 (≤ 0.03 robust), sum
trigger 50 keV, specular 0.98 reflector**. Record the geometry as **not decided by stage 1** — the rule's 1 mm pick is
validated by the V-checks but not robust, and saturation limits it. A changed choice (e.g. 3.2 mm) would need fresh seeds
under a pre-fixed rule.

### 4. Evidence files

- Committed: **`samples/evidence/results/readout-v1-decision.json`** (789 kB) + `.provenance.json` sidecar (32 kB),
  written by `select_readout.py --publish` through `pv.publish`. It holds the merged provenance of both input aggregates
  (sidecars validated first), the rule text, steps and winners (literal and A1), the validation checks, the per-unit
  summary of every geometry × readout × trigger for both families (calibration, L1–L4, acceptance, mis-ID, FWHM, P/V,
  disagreement, throughput and pile-up), and per-geometry optics plus the direct reference. Re-publishing from the same
  aggregates is byte-identical apart from the recorded `--publish` argument string.
- Not committed (reproducible): the full aggregates, `readout-selection-v1.json` (8.52 MB) and
  `readout-validation-v1.json` (8.55 MB, 30 017 keys each); rebuild with `aggregate_readout.py --runs <dir> --family …`
  from the run roots. Proposal: keep them out of git; the decision file is the quotable summary.
- Reproduce the decision:
  `python samples/evidence/readout/select_readout.py --selection <sel>.json --validation <val>.json --publish samples/evidence/results/readout-v1-decision.json`.

### 5. Proposed record wording (planner applies; not applied here)

**Findings, Part 2, new theme:**

> ## 68. A physical four-output readout — light, network, trigger, pile-up (TODO-19 stage 1, 2026-10-08)
>
> [PLAN.Physics.RigReadout](PLAN.Physics.RigReadout.md) (RD-1 … RD-13), substitute-implementer turns 2–4. The engine now
> models the conventional Anger readout from published practice beside the direct crystal assignment: pre-optical
> interaction sites → a traced optical table (crystal × depth, specular 0.98 reflector) → SiPM photoelectrons (PDE, ENF,
> dark counts) → a Kirchhoff-solved charge-division network → time-domain outputs in which overlapping pulses sum in
> every channel → trigger, hold, 14-bit conversion → blind flood-map LUT. DirectCrystal stays the default; the physical
> readout runs in `ReadoutStudy` only (other paths refuse it).
>
> - **Selection and validation** (16 + 32 seeds, disjoint, full matrix; rule pinned before the data, one declared
>   amendment excluding the analytic divider before validation): the realisable winner, **four outputs, DPC column /
>   row ratio 0.01, sum trigger at 50 keV**, is confirmed on validation — 662 keV single-crystal photopeak mis-ID
>   0.00021 ± 0.00004; 122 keV 0.375 ± 0.001; acceptance 0.98 at 122 keV (weakest crystal 0.935) and 0.97 at 1332 keV;
>   FWHM 14.9 / 5.9 / 4.9 % at 122 / 662 / 1332 keV; localisation at equal counts 0.406 ± 0.006 mm against the direct
>   assignment's 0.367 ± 0.001 mm (1 mm pitch, lab head); photopeak events mispositioned by pile-up 1.6 % at 1e5 cps,
>   4.3 % at 3e5 cps; at 1 Mcps throughput 0.39 and 54 % of events piled.
> - **Where it differs from the direct assignment:** 36 % of 662 keV photopeak events land in another crystal than the
>   largest deposit — multi-crystal Compton histories are placed at their light centroid, by design, not by error.
> - **What fails, on every seed:** a DPC with column / row ratio ≥ 0.2 (hour-glass flood: middle-row spots at ≤ 0.54
>   of the ideal spacing — 0.15 for equal resistors), a corner-drained grid (edge spacing 0.31 of the centre), 144
>   unsuppressed channels at 3 keV noise each, and any AND trigger (edge spots lost). Ratio 0.1 is marginal (1 / 32
>   failures at 1 mm).
> - **Triggers:** a per-channel OR at 50 / 100 keV-equivalent leaves crystals blind to 122 keV (only corners pass at
>   100 keV); a sum trigger accepts 98 %.
> - **The price of four ADCs:** one channel per SiPM (3σ zero suppression) identifies 122 keV crystals ≈ 3× better
>   (mis-ID 0.13) but is ≈ 5× worse at 662 keV (0.0013) and costs 144 channels.
> - **Optics are assumptions:** the reflector law decides the energy resolution — diffuse 0.96 gives ≈ 36 % FWHM at
>   662 keV (depth-dependent collection 0.60 → 0.37), diffuse 0.99 11 %, specular 0.98 5.6 % (RD-11).
> - **Not decided:** the crystal pitch. The rule's 1 mm pick is not stable (validation alone would pick 3.2 mm, 0.5 mm
>   wall, ratio 0.03) and rests on infinite microcells, while a 1 mm sensor would see ≈ 9 photoelectrons per 50 µm cell.
> - Reproduce: `run_seeds.py --manifest samples/evidence/manifest-readout-v1.json --family readout_selection_v1
>   readout_validation_v1`, `aggregate_readout.py`, `select_readout.py --publish`; summary
>   `samples/evidence/results/readout-v1-decision.json`; schematic and netlist check in [HW.Readout](../HW.Readout.md).

**Evidence register, new entry EV-36** (next free ID; the header's count becomes 36 entries, EV-01 … EV-36, "head
design" group):

> ### EV-36 — Four-output Anger readout against direct crystal assignment
>
> - **What.** A physical readout model (optics, SiPM, Kirchhoff-solved charge division, time-domain four outputs with
>   pile-up summed in every channel, trigger, 14-bit conversion, blind flood-map LUT) compared with the direct
>   largest-deposit assignment on the same transported histories. Study-only (stage 1); the direct assignment stays the
>   default.
> - **Conditions.** Lab head (rank 7, D 60 mm, S 100 mm), 12 × 12 GAGG, 10 mm, pitch / wall 1.0 / 0, 2.2 / 0.2,
>   3.2 / 0.2 (also diffuse 0.99), 3.2 / 0.5 mm, matched 1:1 virtual SiPMs (S13360-3050 PDE / ENF / DCR per area);
>   readouts: ideal divider, DPC at column / row ratio 1.0 / 0.3 / 0.2 / 0.1 / 0.03 / 0.01, corner grid, one channel
>   per SiPM with and without 3σ zero suppression; triggers sum 50 / 100 keV, OR 50 / 100 keV-equivalent, AND 50;
>   122 / 662 / 1332 keV; rates 1e3 … 1e6 cps. 16 selection seeds, 32 disjoint validation seeds.
> - **Rule (pinned before the data; amendment excluding the analytic divider declared before validation).** Constraints
>   on 122 / 1332 keV acceptance and mis-ID and 662 keV FWHM; lexicographic 2-SE ranking on 662 keV mis-ID,
>   localisation penalty at equal counts, 122 keV mis-ID, pile-up mispositioning.
> - **Result.** Selected and confirmed: four outputs, DPC ratio 0.01, sum trigger 50 keV (values in theme 68's first
>   bullet). Disagreement with the direct assignment 36 % of 662 keV photopeak events (light centroid of multi-crystal
>   histories). The pitch is not decided (validation alone ranks 3.2 mm / 0.5 mm wall first; saturation not modelled).
> - **Not covered.** Microcell recovery and saturation, measured optical constants, the network's RC response, gamma
>   transport in the walls, Studio integration (stage 2).

**Limitations, new row LIM-11:**

> | LIM-11 | The physical readout model rests on assumed optics and infinite SiPM microcells | UN-… | medium |
>
> **What the user sees.** Nothing yet — the physical readout is a study model; the shown images use direct
> assignment. **Why.** No measured reflector data exist in the repository (the reflector law alone moves the 662 keV
> resolution from 5.6 % to 36 %); microcell saturation and recovery are not modelled although a 1 mm sensor would see
> ≈ 9 photoelectrons per 50 µm cell; the network is DC only. **Options:** A. measured reflector / sensor data (bench
> campaign); B. a time-dependent microcell model (moderate code, needs device recovery data); C. keep the direct
> assignment for every claim (chosen for now).

**Decisions, proposed rows** (author to confirm):

> | D-56 | **Optics default: specular 0.98 reflector**; diffuse kept as a compared variant whose ≈ 36 % FWHM is a model finding | chosen (RD-11) | diffuse 0.96 (review prototype) | EV-36, LIM-11 |
> | D-57 | **Readout preset: four outputs, DPC column / row ratio 0.01 (≤ 0.03 robust), sum trigger 50 keV; engine default stays DirectCrystal; pitch left open** | rule-selected on 16 seeds, confirmed on 32 (RD-3, RD-12, RD-13) | independent channels (better at 122 keV, worse at 662 keV, 144 ADCs); OR / AND triggers (blind crystals) | EV-36, theme 68 |

**HW.Readout, new section "Selected preset (TODO-19 stage 1)":**

> The readout study (16 selection + 32 validation seeds) selected and confirmed four outputs through a discretised
> positioning circuit with column / row resistance ratio 0.01 (R_col = 10 Ω against R_row = 1 kΩ) and a sum trigger at
> 50 keV. The drawing above shows ratio 0.1 (100 Ω), now marginal (1 / 32 calibration failures at 1 mm pitch); equal
> resistors and ratios down to 0.2 cannot be calibrated (see the RD-12 table). The crystal pitch is not decided; the
> drawings show 3.2 mm. To draw the preset, add an `Anger-DPC-r0.01` entry to `regenerate.py` and rerun it.

**AGENTS.md:** study table row `| readout-export | engine-resolved readout configuration, DC charge fractions and the SPICE
netlist of the network → <prefix>.config.json, .cir (feeds HW.Readout) | 68 |`; solution-layout additions
"`Readout/` — OpticalResponse, ChargeDivisionNetwork, ReadoutDevice, ReadoutPulseProcessor, FloodLut" (Detector) and
"`Readout/` — ReadoutFlood, ReadoutCalibration, ReadoutStudy, ReadoutExport, ReadoutGuard" (Simulation); Findings
headline "**Physical readout (68)**: four outputs (DPC ratio 0.01, sum trigger) confirmed against the direct assignment;
36 % of 662 keV photopeak events move to their light centroid; OR / AND triggers and near-equal-resistor networks fail;
optics and saturation are assumptions"; Notes & gotchas "`Detector.Readout` other than DirectCrystal is honoured only
by ReadoutStudy (stage 1); the direct paths refuse it".

**README.md (feature list):** "Physical readout study: light spread, SiPMs, a Kirchhoff-solved four-output charge
division, trigger and pile-up in the time domain, flood-map LUT — with a generated readout schematic and SPICE netlist
([HW.Readout](../HW.Readout.md))."

**samples/evidence/README.md, file table:** `manifest-readout-v1.json` (TODO-19 families), `readout/` (requests,
`aggregate_readout.py`, `select_readout.py`), `results/readout-v1-decision.json` (selection + validation decision
summary with provenance sidecar).

**VV.Tests "At a glance" (stale line 9):** "161 units: 125 xUnit classes, 3 unittest classes, 29 cocotb configurations,
4 checkers." (118 + 7 xUnit, 2 + 1 unittest.)

### 6. Verification (turn 4)

C# unchanged this turn. Build 0 errors / 0 warnings (incremental); `dotnet test` 868 passed / 28 skipped / 0 failed;
Python unittests 43 OK (1 skipped); `calibration_record.py --check` up to date; `check-catalog --discover` 161 units (no
new test class this turn; `select_readout.py` is analysis code without a unit test — its output is reproduced
byte-identically, as stated above).

### 7. Files (turn 4)

New: `samples/evidence/readout/select_readout.py`, `samples/evidence/results/readout-v1-decision.json` (+
`.provenance.json`). Changed: this file. (The planner's edits — `docs/HW.Readout.md`, `docs/AGENTS.Conventions.Docs.md`,
references in this file — are theirs.)

### 8. Every command that wrote anything (turn 4)

File-tool writes: the rule and amendment in scratch (`%TEMP%\gcam-todo19\turn4-rule.md`, amendment appended with a one-line
Python write), `select_readout.py` (+ two Python edit scripts and one Edit on it), this section (staged in scratch,
appended with Python). `select_readout.py` runs: on selection only (stdout), `--out` to scratch
(`turn4-selection.json`, `turn4-decision.json`), `--publish samples/evidence/results/readout-v1-decision.json` (which
created `samples/evidence/results` content and the sidecar) and once `--publish` into scratch (`repub/`) for the
reproducibility check; `mkdir -p samples/evidence/results` (already existed); `dotnet build` / `dotnet test` with
process-local TEMP/TMP; the Python check commands (read-only).

### APPROVAL REQUESTS (turn 4)

None.

## Turn 5 (2026-10-08) — preparing the pitch confirmation on fresh seeds

Author's decision: settle the pitch on fresh seeds before recording; Studio integration (stage 2) goes to the Backlog.
No long run; the rule below was pinned before any confirmation run.

### 1. Sizing from the observed spread

- **The spread is counting noise.** The seed SD of 662 keV single-crystal photopeak mis-ID equals the binomial SD —
  validation, 1 mm r0.01: 2.01e-4 vs 1.75e-4; 3.2 mm / 0.5 mm r0.03: 1.69e-4 vs 1.55e-4. That is about two
  mis-identifications per unit per seed at 300 flood histories per crystal (6,870 / 9,330 photopeak single-crystal
  events). The lever is events per unit, not seeds as such.
- **The gap is smaller than turn 4 suggested.** Pooled over selection and validation (48 seeds): 1 mm r0.01
  1.854e-4 (61 / 329,000), 3.2 mm / 0.5 mm r0.03 2.278e-4 (102 / 447,800); δ = 4.23e-5, SE 3.27e-5 (1.3 SE). The
  turn-4 "1e-4" was the selection-only difference.
- **Requirement.** To resolve δ = 4.2e-5 at the rule's 2-SE criterion with 80 % power: SE_δ ≤ δ/2.84 = 1.49e-5, i.e.
  about 1.61 × 10⁶ photopeak single-crystal events per unit at 1 mm (1.36× more at 3.2 mm).
- **Choice: 32 fresh seeds × 3,000 flood histories per crystal** (10× the selection budget): about 2.2 × 10⁶ events at
  1 mm, expected SE_δ 1.27e-5, z = 3.3 for the observed gap, power ≈ 0.91. A seeds-only alternative at 300 per crystal
  would need about 235 seeds — needlessly long. The seed clusters still carry any calibration-to-calibration variance.

### 2. Request — `samples/evidence/readout/request-confirmation-v1.json`

Geometries p1.0-g0, p2.2-g0.2, p3.2-g0.2, p3.2-g0.5. Readouts FourOutputAnger DPC r0.03 and r0.01 (row 1 kΩ). Trigger:
sum at 50 keV. Base optics specular 0.98 with 100,000 photons per bin. DirectCrystal is the built-in reference. Lines
122 / 662 / 1332 keV; rates 1e3 … 1e6 cps. Flood 3,000 per crystal; calibration 2,000; localisation 200,000; rate hits
10,000. This covers the two rule picks (p1.0-g0 r0.01, p3.2-g0.5 r0.03) and their nearest alternatives (both ratios at
every pitch; both walls at 3.2 mm; 2.2 mm as context).

### 3. The pinned rule — `samples/evidence/readout/confirmation-pin-v1.json`

SHA-256 `2d1b73cb2d643e83a77b50a823b62246a8d957b7d2824fdaf7025882fe40478d`, pinned 2026-10-08T02:52:09Z, before the
pilot. It pins the request, `confirm_readout.py` and `select_readout.py` by hash; the script refuses to decide on any
mismatch. The manifest records both the pin and the request hash.

- **Uncertainty:** seed-cluster SE = SD/√32; a difference counts only when |δ| > 2·√(SE_a² + SE_b²).
- **Step 0:** eligible = calibration on all seeds, plus the turn-4 constraints K1–K3.
- **Step 1, pitch 1.0 vs 3.2 mm:** each pitch's eligible unit with the lowest mean M1 (662 keV mis-ID). Compare M1, then
  M2 (662 keV localisation penalty at equal counts against its own direct reference). **No difference → 3.2 mm**: it is
  less saturating (≈ 1.2 vs ≈ 9 photoelectrons per 50 µm cell) and a real catalogue sensor size at its array pitch,
  where 1.0 mm needs a virtual sensor. 2.2 mm is context only: if it beats both on M1, that is reported as a finding
  needing fresh seeds.
- **Step 2, wall (only if 3.2 mm wins):** 0.2 vs 0.5 mm by M1, then M2. No difference → 0.2 mm (fill 0.88 vs 0.71; the
  published array example).
- **Step 3, DPC ratio in the chosen geometry:** r0.01 vs r0.03 by M3 (122 keV mis-ID), then M1. No difference → r0.03
  (the less extreme resistor ratio, robust on every seed).
- **Limitation:** microcell saturation and recovery are not modelled; this stays a stated limitation whatever wins. The
  engine default stays DirectCrystal.

### 4. Pilot

`readout_confirmation_pilot_v1` = the family's first two seeds (8900001, 9000044), run through `run_seeds.py`: 2 / 2 OK,
88 s each with two in parallel. The aggregator validated provenance (2,493 keys). `confirm_readout.py --pilot` ran the
pinned rule end to end (pilot output, not a decision; publishing a pilot is refused). Per-unit 662 keV mis-ID SE with
2 seeds ≈ 3e-5, consistent with the tenfold event count. The two pilot seeds are part of the 32; their outputs are
deterministic and identical in the family run.

### 5. Commands and estimate

```bash
dotnet build Gcam.sln -c Release
dotnet build samples/evidence/probe/Gcam.EvidenceProbe.csproj -c Release
python samples/evidence/run_seeds.py --out <dir> --manifest samples/evidence/manifest-readout-v1.json \
    --family readout_confirmation_v1 --jobs 16
python samples/evidence/readout/aggregate_readout.py --runs <dir> --family readout_confirmation_v1 \
    --out <dir>/readout-confirmation-v1.json
python samples/evidence/readout/confirm_readout.py --aggregate <dir>/readout-confirmation-v1.json \
    --publish samples/evidence/results/readout-confirmation-v1-decision.json
```

Estimate: about 88 s per seed (measured with two in parallel); 32 seeds at `--jobs 16` on 24 logical CPUs is two waves,
assuming a 1.3–2× parallel slow-down ≈ 4–6 min, plus about a minute for aggregation. Far under the 2 h limit.

### 6. Files (turn 5)

New: `samples/evidence/readout/{request-confirmation-v1.json, confirmation-pin-v1.json, confirm_readout.py}`. Changed:
`samples/evidence/seeds.json` (RO_CONFIRMATION32 = 8900001 + 100043·i, i = 0…31, and `_rules_readout_confirmation`,
appended after the last list; checked disjoint from every list, from the depth-likelihood lists and from 777),
`samples/evidence/calibration-records.json` (seeds pin → `f8c3b2c1…`), `docs/VV.Gcam.Calibration.md` (generator only:
the four seed-hash lines), `samples/evidence/manifest-readout-v1.json` (two families, two hashes, text),
`samples/evidence/probe/{ReadoutRecipe.cs, Program.cs}` (phase / mode `confirmation`),
`samples/evidence/readout/aggregate_readout.py` (two family entries), this file.

### 7. Verification (turn 5)

Build 0 errors / 0 warnings (solution and probe). `dotnet test` 868 passed / 28 skipped / 0 failed. Python unittests
43 OK (1 skipped). `calibration_record.py --check` up to date. `check-catalog --discover` 161 units (no new test class).

### 8. Every command that wrote anything (turn 5)

- Python write scripts on `seeds.json`, `calibration-records.json`, the pin and the manifest.
- `calibration_record.py` (generator), then `--check`.
- File-tool writes and Python edits of the request, `confirm_readout.py`, `ReadoutRecipe.cs`, `Program.cs`,
  `aggregate_readout.py`, and this section (staged in scratch, appended with Python).
- `dotnet build` / `dotnet test` with process-local TEMP/TMP.
- `run_seeds.py` into `%TEMP%\gcam-todo19\runs-conf-pilot`, the aggregator into scratch, `confirm_readout.py --pilot`
  (stdout only).

### APPROVAL REQUESTS (turn 5)

None.
