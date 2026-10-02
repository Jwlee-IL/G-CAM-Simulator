# PLAN.Physics.RigReadout — the original rig's readout: light sharing, SiPM pitch, four-channel Anger positioning (TODO-19)

Scope: model how the author's instrument actually turned scintillation light into an event position and energy —
GAGG pixels on a SiPM array, read through **four 14-bit ADC channels** with Anger-type (relative-signal) positioning,
crystal identified from the flood map — and with it the SiPM pitch, light spread between crystals (crosstalk) and
crystal mis-identification. Today the engine assigns each event directly to its crystal (arg-max site). Procedure:
[AGENTS.Planning](AGENTS.Planning.md). Runs before TODO-12 (author, 2026-10-02).

Status: **reference plan, draft** 2026-10-02 — **needs the author's rig details** (table below) before the review.

## What exists (checked in the code, 2026-10-02)

| Item | Where | Fact |
|---|---|---|
| Event position | `ComptonCrystalDetector` list-mode sink | one pulse per history at the **arg-max deposit site** (crystal index); energy = total deposit before any light spread |
| Other strategies | `ComptonStrategy` {PerPixelWindow, AntiCoincidence, …, Centroid} | `Centroid` positions a multi-site history at its energy centroid — the closest existing analogue of light-centroid positioning, but on deposits, not light |
| Light budget | `FrontEndModel` | N_pe = light yield · E · collection · PDE; resolution from ENF, DCR, intrinsic floor — **one lumped channel**, no spatial light distribution |
| Crosstalk | `ComptonCrystalDetector.ApplyOpticalCrosstalk` | redistributes deposits to 4 neighbours (contact × exp(−gap/40 µm)); on the list-mode path a no-op for energy and nearly so for position (Findings 56); inconsistent (energy before, position after the spread) |
| SiPM pitch | `Gcam.Wpf` only (`BlockifyFlood`) | block average of the flood — not a light-sharing model; not carried into Studio |
| Pile-up | `EventStreamStudy.ResolvingSamples`, Studio spectrum | array-wide in time and energy; **position of piled-up pulses not modelled** |
| Waveform | `WaveformService` | the summed energy channel only; "four position channels are not modelled" is stated in the UI |

## Model sketch (reference — to be reviewed and measured)

Per detected history:
1. For each interaction site, scintillation photons N_ph = LY · E_dep (Poisson / Fano-like statistics), spread over the
   SiPM plane by a **light-spread function** set by the crystal pixel, reflector (gap, crosstalk) and the light guide
   (if any); each SiPM collects its share × PDE × fill factor → Poisson photoelectrons, plus ENF and dark counts.
2. The SiPM signals go through the **charge-division network** into four outputs (A, B, C, D); each is digitised
   (14-bit, the chain's shaping, noise).
3. Position (X, Y) from the Anger ratios (e.g. X = (A+B−C−D)/Σ, Y = (A−B+C−D)/Σ — the rig's exact formula is an author
   input); energy from Σ.
4. Crystal identification: a flood-map **LUT** built from a calibration flood (peak finding / segmentation), applied
   to every event → crystal index; mis-identification near dead regions and at the array edge emerges from the model.
5. Pile-up: two pulses within the resolving time sum in all four channels → energy sum **and** a position at their
   light centroid.

Validation targets: the flood map shows separated crystal spots with edge compression; peak-to-valley ratio and the
mis-identification rate are measured, not assumed; with 1:1 coupling and no noise the model must reproduce today's
direct assignment.

## Author input needed (the plan cannot be finished without it)

| # | Question | If unknown |
|---|---|---|
| Q-1 | Charge-division network: resistor topology (e.g. a DPC / symmetric resistive chain per row and column), which four outputs | a standard symmetric resistive network from the literature, stated as an assumption |
| Q-2 | The exact position formula used (Anger ratios, any correction) | the standard four-corner Anger formula |
| Q-3 | Trigger and thresholds: on the sum? per channel? | trigger on the sum |
| Q-4 | How the crystal LUT was built from the flood map (manual, watershed, peak finding), and how often | peak finding + nearest-peak (Voronoi) segmentation |
| Q-5 | ADC sampling: the four channels sampled continuously (125 MSPS like the engine's AD9648?) or peak-held per event | the engine's AD9648 settings |
| Q-6 | Light guide between crystals and SiPMs: present? thickness? | none (direct coupling) |
| Q-7 | SiPM device: size, pitch, active-area fill factor (the engine preset is Hamamatsu S13360-3050 — 3 mm, 50 µm cells) | the preset's datasheet values |
| Q-8 | Crystal pixel size and count of the rig (12 × 12? pitch?) and reflector material (ceramic, per Findings 35) | 12 × 12 at the rig pitch from the scenario files |

## Steps

1. Author answers Q-1 … Q-8 (or accepts the fallbacks).
2. Implementer's review: check the rows, propose the light-spread model (measured / cited, not invented), estimate the
   cost per event, propose validation; write `docs/PLAN.Physics.RigReadout.Review.md`.
3. Decisions after review; implementation in the engine (readout model behind a config switch, default = today's
   direct assignment until validated), then Studio (Detector workspace: SiPM grid, flood map with LUT; Waveform: four
   channels; Spectrum / Imaging: position from the LUT).
4. Findings / EV entries for every measured effect (flood peak-to-valley, mis-identification, pile-up mispositioning).

**Not here:** Studio desktop tests (TODO-22).
