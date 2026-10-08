# PLAN.Physics.RigReadout — a conventional Anger readout: light sharing, SiPM pitch, four-channel positioning (TODO-19)

Scope: model how the author's instrument actually turned scintillation light into an event position and energy —
GAGG pixels on a SiPM array, read through **four 14-bit ADC channels** with Anger-type (relative-signal) positioning,
crystal identified from the flood map — and with it the SiPM pitch, light spread between crystals (crosstalk) and
crystal mis-identification. Today the engine assigns each event directly to its crystal (arg-max site). Procedure:
[AGENTS.Planning](../AGENTS.Planning.md). Runs before TODO-12 (author, 2026-10-02).

Status: **done (stage 1)** 2026-10-08 — review 2026-10-02 (Codex, [review](PLAN.Physics.RigReadout.Review.md)); decisions RD-1 … RD-14; implementation turns 2–5 by a substitute Claude implementer in worktree `C:\gw\w19b` ([report](PLAN.Physics.RigReadout.Turn2.md)); planner checks: build 0 / 0, tests 815 → 868, Python 43, calibration and catalog checks; one seed of each readout family re-run identical apart from timing; selection 16 + validation 32 + pitch confirmation 32 seeds run by the planner. Result: four outputs, DPC ratio 0.01, sum trigger 50 keV, specular reflector, 1 mm pitch (theme 68, EV-36, LIM-11, D-56 / D-57); engine default stays DirectCrystal; Studio integration is TODO-40.

## What exists (checked in the code, 2026-10-02)

| Item | Where | Fact |
|---|---|---|
| Event position | `ComptonCrystalDetector` list-mode sink | one pulse per history at the **arg-max deposit site** (crystal index); energy = total deposit before any light spread |
| Other strategies | `ComptonStrategy` {PerPixelWindow, AntiCoincidence, …, Centroid} | `Centroid` positions a multi-site history at its energy centroid — the closest existing analogue of light-centroid positioning, but on deposits, not light |
| Light budget | `FrontEndModel` | N_pe = light yield · E · collection · PDE; resolution from ENF, DCR, intrinsic floor — **one lumped channel**, no spatial light distribution |
| Crosstalk | `ComptonCrystalDetector.ApplyOpticalCrosstalk` | redistributes deposits to 4 neighbours (contact × exp(−gap/40 µm)); on the list-mode path a no-op for energy and nearly so for position (Findings 56); inconsistent (energy before, position after the spread) |
| SiPM pitch | the deleted `Gcam.Wpf` only (`BlockifyFlood`, at commit `85b2ed1`) | block average of the flood — not a light-sharing model; not carried into Studio |
| Pile-up | `EventStreamStudy.ResolvingSamples`, Studio spectrum | array-wide in time and energy; **position of piled-up pulses not modelled** |
| Waveform | `WaveformService` | the summed energy channel only; "four position channels are not modelled" is stated in the UI |

## Readout preset and defaults (2026-10-02)

**Confidentiality.** This is a clean-room project: the repository holds no former-employer design data. The readout
model is therefore built from **standard, published practice**, and its defaults are chosen here by physics and by
measurement — not taken from any specific instrument.

| Choice | "Conventional Anger" preset (literature practice) | Alternatives the study sweeps |
|---|---|---|
| Charge division | symmetric resistive network, four outputs | per-SiPM readout; row / column summing |
| Position | four-corner Anger ratio, no correction | linearised / LUT-corrected positioning |
| Trigger | per-channel thresholds, set to reject low-energy background | sum trigger; threshold swept |
| Crystal identification | flood-map LUT by watershed on a Gaussian-smoothed flood | nearest-peak (Voronoi), other segmentations |
| Digitisation | peak-hold, the four channels sampled together | continuous sampling with digital shaping |
| Coupling | direct (no light guide) | light guide, thickness swept |
| Geometry | crystal pitch, reflector wall and SiPM size are **parameters**; defaults from a commercial SiPM (the engine's S13360-3050 preset) and literature reflector thicknesses, set in "Decisions after review" | pitch, wall and SiPM size swept |

**Goal (author, 2026-10-02):** simulate the **best combination physically**. The conventional preset is one point; the
default becomes whatever the measurement shows is best.

## Model sketch (reference — to be reviewed and measured)

Per detected history:
1. For each interaction site, scintillation photons N_ph = LY · E_dep (Poisson / Fano-like statistics), spread over the
   SiPM plane by a **light-spread function** set by the crystal pixel, reflector (gap, crosstalk) and the light guide
   (if any); each SiPM collects its share × PDE × fill factor → Poisson photoelectrons, plus ENF and dark counts.
2. The SiPM signals go through the **charge-division network** into four outputs (A, B, C, D); each is digitised
   (14-bit, the chain's shaping, noise).
3. Position (X, Y) from the Anger ratios (e.g. X = (A+B−C−D)/Σ, Y = (A−B+C−D)/Σ — the conventional four-corner form); energy from Σ.
4. Crystal identification: a flood-map **LUT** built from a calibration flood (peak finding / segmentation), applied
   to every event → crystal index; mis-identification near dead regions and at the array edge emerges from the model.
5. Pile-up: two pulses within the resolving time sum in all four channels → energy sum **and** a position at their
   light centroid.

Validation targets: the flood map shows separated crystal spots with edge compression; peak-to-valley ratio and the
mis-identification rate are measured, not assumed; with 1:1 coupling and no noise the model must reproduce today's
direct assignment.

## Instrument details

Not recorded here. The author decided (2026-10-02) that defaults are chosen by physics and measurement; details of any
former-employer instrument stay out of the repository (clean-room rule).

## Decisions after review (2026-10-08)

The review corrected the plan in seven places (its "Recommendation and material corrections"): list-mode records keep
only the crystal index, so the pre-optical interaction sites must be preserved; Anger positioning equals direct
assignment only for single-site events (51 % of accepted 662 keV histories are multi-crystal; Anger differs from the
arg-max for 29–34 %); edge compression must emerge, not be inserted; trigger logic is a physics choice (per-channel OR at
100 keV accepts 3–7 % of 122 keV events, at favourable positions; a sum trigger 99 %); Gaussian smoothing broadens
spots; PDE already contains the microcell fill; no measured ceramic optical attenuation exists. Its prototypes are one
seed per point and predate the xoshiro256** generator (TODO-26): they guide, they are not evidence. The author answered
four questions on 2026-10-08.

| ID | Decision | Source |
|---|---|---|
| RD-1 | **Compared readouts:** `DirectCrystal` (today, reference and default until a study selects otherwise), `FourOutputAnger` (the conventional preset from published practice) and `IndependentSipm` (one channel per SiPM) as the comparison that prices the four-ADC constraint | **author** (Q1, recommended) |
| RD-2 | **Trigger:** a sum (Σ) trigger is the baseline; per-channel OR and AND with swept thresholds are compared; threshold units (ADC code or calibrated channel energy) are explicit in the model | **author** (Q2, recommended) |
| RD-3 | **Objective includes count rate:** default selection weighs 662 keV positioning (crystal mis-identification, localisation at equal counts), 122 / 1332 keV acceptance and performance, **and high-rate behaviour** — pile-up that sums in all four channels moves the position to the light centroid, so rate is part of the readout's quality; this needs a time-domain pulse model of the four outputs (shaping, hold, resolving time) rather than DC amplitudes only | **author** (Q3) |
| RD-4 | **Stage 1 = engine and study, Studio later:** pre-optical interaction records (XYZ, time, energy); light spread → SiPM photoelectrons (PDE, ENF, DCR); charge-division network → four outputs; the time-domain four-channel pulse and pile-up model needed by RD-3; digitisation; trigger; LUT calibration (watershed with explicit failure); paired-seed study against `DirectCrystal`. Studio integration (Detector / Waveform four channels, LUT flood) is stage 2, decided after stage 1's results | **author** (Q4, recommended) |
| RD-5 | **Limitations stated, not modelled in stage 1:** gamma transport in the ceramic / reflector walls, finite microcell recovery beyond the existing saturation model, and an RC network impulse response beyond the shaping chain — unless the review turn shows one of them changes a stage-1 conclusion | planner (review's open decision 2) |
| RD-6 | **Verification:** the review's table (disabled path bit-identical; light and charge conservation; network against independently solved small circuits; four-channel covariance against wᵀCov(pe)w; single-site ideal limit = original crystal ID; multisite tests assert the light centroid, not the arg-max; calibration train / test split); tolerances from MC uncertainty and observed spread, never borrowed | review |
| RD-7 | **Evidence:** study families through `run_seeds.py` with provenance, selection and validation seeds disjoint, a timing pilot, long runs started by the planner; Findings / EV entries for flood peak-to-valley, mis-identification, trigger acceptance, pile-up mispositioning and localisation | planner |
| RD-9 | **Readout schematic (pseudo-artwork), after stage 1's model is settled:** a schematic-level drawing generated from the same readout configuration the engine uses — SiPM grid matched 1:1 to the crystal array, the charge-division network, the four outputs A–D, preamp / shaper, four 14-bit ADC channels, the sum trigger, and the FPGA's Anger ratio + LUT — as an SVG (and a page in the docs), so a changed pitch or topology redraws it; illustrative, not a buildable board design; the network shown is the generic published charge-division form, never a former-employer layout | **author** (2026-10-08: "완벽하진 않더라도, 1번 정도는 흉내 낼 수준이면 충분") |
| RD-10 | **Network netlist as an independent check, with RD-9:** the same configuration exports a SPICE netlist of the charge-division network (sources at the SiPM nodes, the resistor graph, the four output loads); an independent solver — a standard-library Python nodal analysis by default, ngspice only if the author approves installing it — solves it, and the four output fractions per SiPM must equal the engine's DC charge-division fractions within a bound derived from floating-point conditioning (RD-6's "network vs an independently solved circuit"); the netlist and the comparison are committed beside the schematic | **author** (2026-10-08: "2번도 고려 부탁해") |
| RD-8 | **Clean room:** geometry, network and thresholds are parameters with defaults chosen by measurement or cited published practice; no former-employer instrument detail in any file, prompt or commit | CLAUDE.md |

**Decisions after turn 2 (2026-10-08).** Turn 2 (substitute implementer, worktree `C:\gw\w19b`; report
[Turn2](PLAN.Physics.RigReadout.Turn2.md)) built stage 1 (planner re-check: build 0 / 0, tests 815 → 867, calibration and
catalog checks pass) and corrected three premises: the engine had no microcell saturation model (a no-recovery option
was added, off by default; ~9 pe per cell at 662 keV on 1 mm sensors, so stage-1 numbers assume unlimited cells); no
geometry defaults had been set (pitch / wall 1.0 / 0, 2.2 / 0.2, 3.2 / 0.2, 3.2 / 0.5 mm are swept); the review's
diffuse 0.96 reflector gives depth-dependent collection 0.60 → 0.37 and a 662 keV FWHM of ~36 %. Author answers:

| ID | Decision | Source |
|---|---|---|
| RD-11 | **Optics default: specular 0.98 reflector**; the diffuse reflector stays a compared variant, and its ~36 % FWHM is recorded as a model finding whose realism is a stated limitation | **author** (recommended) |
| RD-12 | **DPC column / row resistance ratio is swept in the selection run** (not fixed at 0.1); equal resistors stay as the known failing case | **author** |
| RD-13 | **Validation runs the full matrix** on the validation seeds, not only the configuration chosen on selection | **author** (recommended) |
| RD-14 | **After turn 4:** selection (rule written before reading; one amendment — realisable readouts only — declared before validation) picked four-output Anger, DPC ratio 0.01, sum trigger 50 keV, specular reflector, 1 mm pitch; validation confirmed it (662 keV mis-ID 0.00021, localisation +0.039 ± 0.006 mm vs direct, pile-up mispositioning 1.6 / 4.3 % at 1e5 / 3e5 cps), but the same rule on validation alone picks 3.2 mm / 0.5 mm wall / ratio 0.03 (differences ~1e-4, ~2 SE). **The author chose to settle the pitch on fresh confirmation seeds** with a rule pinned before the run; the engine default stays DirectCrystal; Studio integration (stage 2) goes to the Backlog | **author** (2026-10-08) |

## Steps

2. Implementer's review: check the rows, propose the light-spread model (measured / cited, not invented), estimate the
   cost per event, propose validation; write `docs/PLAN.Physics.RigReadout.Review.md`.
3. Decisions after review; implementation in the engine (readout model behind a config switch, default = today's
   direct assignment until validated), then Studio (Detector workspace: SiPM grid, flood map with LUT; Waveform: four
   channels; Spectrum / Imaging: position from the LUT).
4. Findings / EV entries for every measured effect (flood peak-to-valley, mis-identification, pile-up mispositioning).

**Not here:** Studio desktop tests (TODO-22).
