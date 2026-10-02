# PLAN.Physics.AmbientBackground — an absolute, source-independent ambient background (TODO-30)

Scope: every published MC number so far comes from an ideal environment — no ambient background, or a background
whose rate is set **relative to the source** (BSR). A real camera sits in a natural radiation field whose rate does not
depend on the source: removing the source leaves it, and a weaker source is buried by it. Add that field as physics
(dose rate + spectrum → fluence → transport through the head → Poisson events), re-measure the results it changes, and
fill the "Under ambient background" column of Evidence §1. Procedure: [AGENTS.Planning](AGENTS.Planning.md).

Status: **reference plan** 2026-10-02 (author: "백그라운드 방사선도 포아송 분포로 발생할 수 있음을 전제하에 만들었던 것
같은데, 그게 빠져있으면 반쪽짜리 시뮬레이터야"). Implementer: **Codex** — turn 1 is the review; the planner waits for the
author's go to start it.

## What exists (checked in the code, 2026-10-02 — *verify*)

| Item | Where | What it does |
|---|---|---|
| Config | `BackgroundConfig` (`SimulationConfig.cs`) | `BackgroundToSignalRatio` (detected background ÷ detected source counts), one `EnergyKeV` (default 200), optional `DarkCountRateKcps`; no absolute rate, no spectrum |
| Flood-map path | `BackgroundStudy` (`PedestalPerPixel`, `PoissonPixel` = Poisson(source mean + pedestal)) | a uniform pedestal of BSR × the detected source budget per acquisition, Poisson-realised per pixel; also a linear gradient variant. Used by `background`, `antimask`, `antimask-scene`, `shield` (`bg0PerPixel`), `fov` (BSR 0 / 1) |
| List-mode path | `ListModeSource` line ~141: background arrivals are a Poisson process with rate **`BSR × SourceRateCps`**; `ListModeBackground` | each background history enters the crystal top face with a cosine flux at the single `EnergyKeV`, unmasked and unshielded, deposit from `ComptonCrystalDetector`, **pixel placed uniformly at random, independent of the deposit site** |
| Event stream | `EventStreamStudy.BackgroundDepositSpectrum(config, E, n, isotropic)` | deposit spectrum of a diffuse mono-energetic field |
| Dose conversion | `DoseStudy.cs` — ICRP 74 H*(10)/Φ (10 keV–10 MeV) | photon fluence ↔ ambient dose equivalent, already used for the dose evidence (EV-23) |
| Studio | `MainViewModel.BackgroundToSignalRatio` (default 0) → `SimulationService` | the only background control; ratio-based |
| Studies without background | `NoiseStudy` (EV-07 count gates, PR-SENS-02), single runs, sweep, MLEM, depth, mixed field, Compton studies | ideal |

## Proposal (reference — verify / improve)

| # | Proposal | Status |
|---|---|---|
| A-1 | **Ambient field as input:** H*(10) rate in µSv/h plus a spectrum shape; fluence per energy bin from ICRP 74 (existing table). Default preset "terrestrial, typical": the K-40 (1461 keV), U-series (Bi-214 609 / 1120 / 1764, Pb-214 295 / 352) and Th-series (Tl-208 583 / 2614, Ac-228 911 / 969) lines plus their scattered continuum, and a cosmic component — intensities and continuum fraction **from published in-situ spectra / reference data, cited**, never hand-set | verify: sources and numbers |
| A-2 | **Angular distribution:** isotropic as the baseline; a ground-weighted (2π lower + skyshine) option if a cited model exists | verify |
| A-3 | **Transport through the head:** front hemisphere through the existing mask slab (diffuse, so nearly uniform but physically modulated), sides / rear through the shield model where one exists; the pixel comes from where the photon actually deposits (replaces the uniform random placement) | verify against `ShieldStudy` / FOV front-plate-only model |
| A-4 | **Absolute rate, source-independent:** detected background rate = Σ_E Φ(E) × effective area(E, direction) from that transport; arrivals Poisson in time (list mode) and Poisson per pixel (flood map); removing the source leaves the background unchanged | — |
| A-5 | **Intrinsic crystal background** for the what-if materials (LYSO Lu-176, LaBr3 La-138 / Ac-227; GAGG, NaI, CsI, BGO, CeBr3 low) from published activities | verify; optional in the first pass |
| A-6 | **Compatibility:** BSR stays as a controlled stress knob (documented as relative); ambient and BSR are separate fields; existing BSR studies and their numbers stay reproducible | — |
| A-7 | **Studio:** an ambient dose-rate input with the preset spectrum, **on by default at a typical indoor / outdoor level** (author to confirm the default), BSR shown as a derived readout | author decision |
| A-8 | **Count gate (PR-SENS-02):** re-measure the EV-07 gates with ambient background over source activity × distance; measure a reconstruction-significance quantity (peak-to-sidelobe or background-subtracted SNR) against localisation error; propose a gate on net source counts or significance that holds with background | — |
| A-9 | **Re-measure what changes:** with the TODO-27 seed driver (`samples/evidence/`), re-run the families listed in Evidence §1 "Ideal conditions and background" under the default ambient field at stated source activities and distances; fill that table's last column with measured values (N seeds), keep the ideal values as the best-case bound | — |
| A-10 | **Sanity anchors:** compare the simulated background count rate and spectrum of a bare crystal with a published measurement of a similar scintillator at a stated dose rate (cite); the dose-rate path (EV-23) reads the ambient field back within its stated accuracy | verify: which published anchor |

## Steps
1. **Turn 1 — review and measurement (Codex):** verify the "What exists" table and every *verify* row in the code; find
   and cite the data for A-1 / A-2 / A-5 / A-10; measure the current BSR model's limits (e.g. what a 0.1 µSv/h field
   corresponds to in BSR for the Studio and lab scenarios at their default activities); propose the config / API shape
   and the test oracles; write `docs/PLAN.Physics.AmbientBackground.Review.md`. No code in this turn.
2. Decisions after review (author: A-7 default and anything that changes a conclusion).
3. Implement in the same Codex conversation; tests; Studio input; re-measurements (A-8, A-9) through the seed driver.
4. Planner verification; Evidence §1 column, EV entries, PRS PR-SENS-02, LIM-06, README, PAPER updated with measured
   values; Findings theme.

## Tests (proposed)
Rate independence (source removed → background rate unchanged within Poisson); Poisson dispersion of counts per time
bin and per pixel; spectral lines at their energies with the transported continuum; ICRP 74 round trip (dose rate →
fluence → dose rate); BSR legacy path bit-for-bit unchanged; seed reproducibility.

Done when: a scenario can carry an absolute ambient field; the engine, CLI studies and Studio use it; PR-SENS-02 has a
measured gate that holds under background; Evidence §1 lists measured values under ambient background for every
family it names.

Not here: room-scatter geometry of a specific site, plant-specific spectra (beyond a user-supplied spectrum), radon
progeny time variation, neutron background.
