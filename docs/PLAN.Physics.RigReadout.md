# PLAN.Physics.RigReadout — a conventional Anger readout: light sharing, SiPM pitch, four-channel positioning (TODO-19)

Scope: model how the author's instrument actually turned scintillation light into an event position and energy —
GAGG pixels on a SiPM array, read through **four 14-bit ADC channels** with Anger-type (relative-signal) positioning,
crystal identified from the flood map — and with it the SiPM pitch, light spread between crystals (crosstalk) and
crystal mis-identification. Today the engine assigns each event directly to its crystal (arg-max site). Procedure:
[AGENTS.Planning](AGENTS.Planning.md). Runs before TODO-12 (author, 2026-10-02).

Status: **reviewed** 2026-10-02 ([PLAN.Physics.RigReadout.Review](PLAN.Physics.RigReadout.Review.md), Codex, rewritten without instrument references) — decisions after review next.

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

## Steps

2. Implementer's review: check the rows, propose the light-spread model (measured / cited, not invented), estimate the
   cost per event, propose validation; write `docs/PLAN.Physics.RigReadout.Review.md`.
3. Decisions after review; implementation in the engine (readout model behind a config switch, default = today's
   direct assignment until validated), then Studio (Detector workspace: SiPM grid, flood map with LUT; Waveform: four
   channels; Spectrum / Imaging: position from the LUT).
4. Findings / EV entries for every measured effect (flood peak-to-valley, mis-identification, pile-up mispositioning).

**Not here:** Studio desktop tests (TODO-22).
