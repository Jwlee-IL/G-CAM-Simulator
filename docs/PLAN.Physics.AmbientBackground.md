# PLAN.Physics.AmbientBackground — an absolute, source-independent ambient background (TODO-30)

Scope: every published MC number so far comes from an ideal environment — no ambient background, or a background
whose rate is set **relative to the source** (BSR). A real camera sits in a natural radiation field whose rate does not
depend on the source: removing the source leaves it, and a weaker source is buried by it. Add that field as physics
(dose rate + spectrum → fluence → transport through the head → Poisson events), re-measure the results it changes, and
fill the "Under ambient background" column of Evidence §1. Procedure: [AGENTS.Planning](AGENTS.Planning.md).

Status: **in progress — measurements and V&V docs done**, 2026-10-04. Turns 6–9 by substitute Claude implementers
(Codex unavailable), each audited and re-verified by the planner (final: build 0 errors; tests engine 393, Core 184,
services 87 (+7 skipped), UI 13 (+14 skipped), all pass). Results: validated terrestrial spectrum (AB-10), gate 96 / 96
on fresh seeds (AB-11), EV-01 / 02 / 07 / 09 / 12 / 15 under the field (AB-13, AB-14), decoder-pull baseline → TODO-33
(AB-12). Docs: Evidence §1 measured column + **EV-34**, PR-SENS-02, LIM-06 / LIM-07, Findings **theme 64**, README /
Overview / PAPER wording. **Remaining:** AB-5 Studio default (author decision), Studio's "not validated" label and its
SRS / SDS rows, then close (Todo row → Backlog, plan → archive). Reports: `samples/evidence/results/ambient-baseline-v1-turn{6..9}.md`.
History: turns 1–5 Codex (`56f0a17`); 6 `b9310f8`; 7 `caade67`; 8 `aa9fcc4`; 9 `c9a1e38`.

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

## Decisions after review (2026-10-03)

Review: [PLAN.Physics.AmbientBackground.Review](PLAN.Physics.AmbientBackground.Review.md) (Codex). It stopped A-1/A-2
(no citable numerical incident spectrum), A-3 (no ray-traced head shield; crystal entry is top-face only), the high-energy
physics (tungsten μ clamps above 1332 keV, no pair production, dose path cuts deposits ≥ 2000 keV), A-4 (source-free
acquisition impossible), and corrected A-8 (net counts alone do not keep a precision gate). Its order-of-magnitude
estimate: at the lab distance ambient is a few % of the source, but **1 MBq at ~1 m gives BSR ~2.5–3**, so the
250-count sub-mm gate can become ~1000 counts. The author decided a **baseline first** and deferred the rest.

| # | Decision | Basis |
|---|---|---|
| AB-1 | **Baseline scope:** absolute ambient field (photon H*(10) µSv/h + spectrum → fluence by the existing ICRP 74 table), source-independent, Poisson in time (list mode) and per pixel (flood map), **pixel from the actual deposit site** (no random placement) | author, 2026-10-03: "먼저 베이스라인부터 잡아내는 게 좋겠어" |
| AB-2 | **Two bounding geometries instead of a housing model:** upper = bare crystal exposed on all faces (needs ray–box entry on every face); lower = entry through the front (mask) only, sides / rear perfectly shielded. Report every ambient result as this range | review §1 / §4.4; realistic housing deferred to **TODO-32** |
| AB-3 | Lines above 1332 keV (K-40 1461, Bi-214 1764, Tl-208 2614) are **included with today's physics** and the limits stated (tungsten μ clamped, no pair production, dose cut); high-energy transport deferred to **TODO-31** | author: the high-energy correction is a later task |
| AB-4 | **Spectrum built by the engine, not borrowed:** UNSCEAR 2000 Annex B population-weighted soil activities K / U / Th = 420 / 33 / 45 Bq/kg in a uniform soil half-space, transported through air to a detector height of 1 m; line + scattered continuum and angular distribution come out of that transport. Decay data from an evaluated source (ENSDF / DDEP / NNDC — cite; never from memory). **Validation oracle:** the computed air kerma per Bq/kg against UNSCEAR's 0.0417 / 0.462 / 0.604 nGy/h, tolerance derived from the MC sample size plus the stated limits of AB-3; a discrepancy is reported, not tuned away. The result is a versioned, hashed spectrum file | planner recommendation, accepted ("2~6 수용") |
| AB-5 | **Studio:** ambient input (photon H*(10), preset spectrum named) **default 0 / off with the "ideal environment" label** until AB-4 is validated; then the default becomes 0.10 µSv/h (a later decision row). BSR stays an editable, separate stress input; a derived ambient BSR readout | accepted |
| AB-6 | **Source-free (background-only) acquisitions** allowed in engine and Studio, with a fixed-time event API that progresses through empty intervals | review A-4 |
| AB-7 | **PR-SENS-02 gate targets:** ≤ 1 % false trusted location per acquisition on background-only acquisitions (≥ 1000 null acquisitions per configuration; 299 is the zero-failure minimum), plus ≥ 95 % within one declared angular resolution element for located sources; calibrated search statistic per review §7, gate chosen and validated on separate seeds | accepted |
| AB-8 | **Compatibility:** with ambient off, legacy BSR and disabled paths stay **bit-for-bit identical** (including Stop / Continue and RNG draw order). Intrinsic crystal activity (LYSO, LaBr3, …) is out of scope (later task); EV-23 dose / separate counter unchanged | accepted |
| AB-9 | **Re-measurement priority:** EV-07 / PR-SENS-02, EV-02, EV-12, EV-15, EV-01 / EV-09 first, through the seed driver with versioned ambient manifests (field, activity, distance, exposure, window, bound); other families get a written "unchanged / why" or wait. Every quote keeps the ideal value as the best-case bound | accepted |

**Correction AB-4a (planner, 2026-10-03, implementation turn 2).** Codex stopped on Po-218: no evaluated photon
emission data is reachable (IAEA LiveChart returns no gamma rows; the ENSDF α-decay evaluation, Nucl. Data Sheets 175,
1 (2021), gives an excited Pb-214 level fed by a very weak α branch but no photon intensities; DDEP unreachable). Rule
adopted: a nuclide (or branch) without evaluated photon data **may be omitted only with a quantified upper bound** —
photon yield per decay ≤ the evaluated α / β feeding of excited levels, and emitted photon energy per decay ≤ Σ feeding ×
level energy; the bound on its share of the chain's photon fluence and air kerma is computed from the evaluated
feeding and written next to the spectrum. If a bound exceeds **10⁻³ of its chain's air kerma**, stop and report
instead of omitting. Feeding data are used only for the bound, never as substitute line intensities. Also: committed
result files carry no machine paths (snapshot paths relative to the TEMP work folder or omitted; hashes kept).

**Correction AB-4b (planner, 2026-10-03, turn 3).** Codex was right: "photon yield ≤ feeding" is false when one feeding
can de-excite through a cascade (the adopted Pb-214 scheme has a two-photon cascade). Replace the count bound: each
γ step moves to a strictly lower level, so photons per feeding ≤ the number of levels between the fed level and the
ground state in the **adopted level scheme** (cited); conversion electrons only lower it. Energy bound unchanged
(≤ Σ feeding × level energy). Kerma bound: energy bound × the maximum air μ_en/ρ over [smallest level spacing below the
fed level, fed-level energy] (cited μ_en/ρ table). The 10⁻³-of-chain-kerma threshold and the stop rule are unchanged.

**Correction AB-4c (planner, 2026-10-03, turn 4) — supersedes the kerma part of AB-4a/4b.** Codex showed the kerma
bound was ill-posed (energy × μ_en/ρ is not a kerma at 1 m without a transport response, and soil / air scatter makes
photons below any level spacing) and that the At-218 record has no Po-218 β-feeding data. The omission criterion moves
to the **source term**: an unresolved branch may be omitted when its **emitted photon energy per chain decay is ≤ 10⁻⁴
of the chain's evaluated emitted photon energy**. Upper bounds: α branch = Σ feeding × level energy (AB-4b count bound
kept for the record); β branch without feeding data = branch fraction × Q_β (all decay energy taken as photons — a
strict bound needing no level data). This is a declared **modelling approximation** of the source term, not a certified
transported-kerma bound; the spectrum file lists every omitted branch with its bound, and the UNSCEAR comparison states
it. Exceeding 10⁻⁴ → stop and report.

**Decision AB-4d (author, 2026-10-04) — supersedes the 10⁻⁴ omission certification for transitions without
evaluated photon intensities.** The source term uses **evaluated photon lines with intensities only**. A transition
the evaluation lists without a photon intensity (e.g. Bi-214 β: 36.8 / 61.0 / 71.1 / 104.4 keV) is **listed in the
spectrum file as "not included"** — never assigned a substitute intensity and never certified as negligible. The AB-4c
omission bounds already computed stay on record. Validation is the computed air kerma per Bq/kg against UNSCEAR
(K / U / Th: 0.0417 / 0.462 / 0.604 nGy/h) with the MC's own standard error; the **acceptance tolerance is decided by
the author after seeing the ratios**, and a discrepancy is reported, never tuned away.

**Decision AB-10 (author, 2026-10-04) — UNSCEAR comparison accepted.** Turn 6 (16 outer seeds × 10⁷ histories per
chain) gave air kerma at 1 m per Bq/kg, MC / UNSCEAR: K-40 **1.0139 ± 0.0003**, U series **0.9974 ± 0.0003**, Th series
**1.0227 ± 0.0003**; uncollided agreement with the analytic kernel within 6 SE in every tested cell. The gaps are many SE
wide — a model-to-model difference (UNSCEAR's coefficients come from another calculation; decay-data versions; soil
composition), and the model's own omissions would raise, not lower, the kerma. Accepted with an **agreement band of ±3 %**,
chosen after seeing the ratios: this is a **model comparison, not a statistical test**, and is quoted as such. The
spectrum keeps its NOT-VALIDATED file name until it is re-issued as v1 validated in turn 7 (content unchanged).
Also accepted: `samples/ambient/source-data/.gitattributes` (`* -text`) so the hashed snapshots are stored byte-exact.

**Decisions AB-11 … AB-13 (author, 2026-10-04, after turn 7).** Turn 7: per-configuration thresholds (α = 0.003 on
4096 selection nulls) passed the ≤ 1 % false-trusted target in 93 / 96 configurations — three front-only configurations
with < 1 expected background count missed by 0.007 points (13 / 2048, upper 1.007 %) because Z is coarse there; the
pre-declared universal threshold Z = 4.643 passed 96 / 96. The bare bound also showed a **decoder bias of ~9 mm**
(non-flat transported background) while the gate statistic located 100 % correctly.
- **AB-11 — gate rule:** keep **per-configuration thresholds**; re-select on **more selection nulls** with ties handled
  conservatively (exceedances counted with ties at the threshold), and re-validate on **seeds not used in turn 7**
  (neither F128[1:65] nor O128[0:128]). The turn-7 validation is not re-used to choose. Failure is reported, not tuned.
- **AB-12 — decoder bias:** recorded as a finding and a separate task **TODO-33** (background-shape-aware decoding);
  not fixed inside TODO-30.
- **AB-13 — remaining families:** EV-02, EV-12 mask / antimask, EV-15 and **EV-01 at the legacy EV-01 recipe's activity
  and exposure** with the ambient field added, through the seed driver, both bounds, ideal value kept as best-case.

**Decision AB-14 (author, 2026-10-04, after turn 8).** EV-15's legacy recipe (1 Bq Cs + 8 Bq Co and a photon budget,
no live time) gives ~6 Cs counts when read literally and is unmeasurable even without background. Re-measure EV-15 at
absolute activities in the lab geometry, 60 s, both bounds and field levels, ideal beside: **(a) Cs 1 MBq + Co 8 MBq**
(the legacy 8 : 1 ratio) and **(b) Cs 1 MBq + Co 2 MBq** (the ratio where the spatial lever still holds, theme 52).
Also run EV-02 in the **cs662 window** (dropped in turn 8 for time).

**Decision AB-15 (author, 2026-10-04) — Studio default, replaces AB-5's "later decision row".** Studio's ambient preset
becomes the validated terrestrial spectrum (`terrestrial-unscear2000-v1`), **on by default at 0.10 µSv/h** photon
H*(10) with the **front-only** bound; 0 restores the ideal environment (labelled as such). The development mono662
placeholder leaves the user-facing preset list.

**Angular distribution (AB-4 addendum).** Representation: a tabulated joint distribution, energy bins × zenith-cosine
bins at the detector point (azimuth uniform), versioned with the spectrum. Acceptance oracle: the **uncollided** line
fluence per Bq/kg and its zenith distribution from the MC against the analytic uniform-half-space result (soil μ, air μ,
height h; Beck-type exponential-integral form, cited) within the MC's own statistical error; the total (collided +
uncollided) is then checked only through the UNSCEAR kerma comparison.

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
