# TODO-30 baseline — turn 6 (AB-4 generator under AB-4d)

2026-10-04. **This turn was done by a substitute Claude implementer (Claude subagent), not by Codex.** Specification:
`docs/PLAN.Physics.AmbientBackground.md`, AB-4 with Decision AB-4d and the angular addendum. Delivered: evaluated
source catalog, cited soil/air material data, a collided soil/air Monte Carlo generator (C#, CLI `ambient-terrestrial`),
a versioned, hashed `IncidentSpectrum` marked **NOT-VALIDATED**, validation (a) and the UNSCEAR ratios (b).
**Stopped at the ratios**: no acceptance tolerance applied, nothing tuned, no gate study (AB-7), no EV re-measurement
(AB-9), no Studio default change, no VV/README/PAPER/Findings/PLAN/Todo edit, no git state change. Machine-readable
results: `ambient-baseline-v1-turn6.json` (summary) and `ambient-baseline-v1-turn6-generator.json` (full generator
record) in this folder.

## Results first — validation (b): air kerma at 1 m per Bq/kg vs UNSCEAR

N = 16 independent outer seeds per chain (`samples/evidence/seeds.json` O128[0:16] = 12345, 1000003, 1007922, 1015841,
1023760, 1031679, 1039598, 1047517, 1055436, 1063355, 1071274, 1079193, 1087112, 1095031, 1102950, 1110869), 1e7 source
histories each (4.8e8 histories in all, 177 s on 12 threads). SE = sample SD of the 16 per-seed means / √16 (it agrees
with the pooled per-history SE: K 1.2e-5, U 1.3e-4, Th 1.8e-4 nGy/h per Bq/kg). UNSCEAR = 2000 Report Vol. I Annex B,
Table 6 dose coefficients (which cite ICRU Report 53 and K. Saito and P. Jacob, Radiat. Prot. Dosim. 58(1) 29–45 (1995)).

| Chain | MC air kerma, nGy/h per Bq/kg | UNSCEAR | **Ratio MC / UNSCEAR ± SE** | Uncollided / 511-keV annihilation / scattered share | Kerma share of photons > 1332 keV |
|---|---:|---:|---:|---|---:|
| K-40 | 0.0422787 ± 0.0000133 | 0.0417 | **1.01388 ± 0.00032** | 49.73 % / 0.05 % / 50.21 % | 54.1 % |
| U-238 series | 0.460800 ± 0.000148 | 0.462 | **0.99740 ± 0.00032** | 45.81 % / 0.08 % / 54.12 % | 28.9 % |
| Th-232 series | 0.617699 ± 0.000157 | 0.604 | **1.02268 ± 0.00026** | 47.68 % / 0.19 % / 52.13 % | 36.1 % |

- At the UNSCEAR activities (420 / 33 / 45 Bq/kg) the field is 17.76 + 15.21 + 27.80 = **60.76 nGy/h** (UNSCEAR's own
  products 17.51 + 15.25 + 27.18 = 59.94).
- "Uncollided" = never interacted (coherent is not modelled, see limits). Kerma from photons below 10 keV: **0** in all
  4.8e8 histories (no scattered photon below 10 keV was scored in the slab). The > 1332 keV shares are the AB-3 / TODO-31
  exposure: those photons enter the camera transport whose tungsten μ is clamped above 1332 keV and that has no pair
  production.
- Hybrid check (analytic uncollided + MC rest): 0.0422903 / 0.460807 / 0.617671 → ratios 1.01416 / 0.99742 / 1.02264.
- The differences from 1 are many SE (MC statistics are not what separates the numbers); per AB-4d **no pass/fail is
  declared** — the author sets the tolerance. Known reasons for a difference, stated factually (not used to adjust
  anything):
  1. *UNSCEAR's basis is another calculation*: its coefficients come from ICRU 53 / Saito & Jacob (1995), with their decay
     data, soil composition, density, air model and transport code. Those papers were not accessed in this turn, so
     their inputs could not be compared one by one. UNSCEAR quotes three significant figures (rounding up to ±0.12 % for
     0.0417).
  2. *Decay data*: this catalog uses the current IAEA LiveChart/ENSDF evaluations (e.g. A=214 2021, A=212 2020, A=228 2012,
     A=208 2007); a 1990s calculation used older line intensities.
  3. *Soil*: HASL-258 Table 2 soil with 10 % water. For a uniform source the kerma per Bq/kg depends on the soil only
     through the shape of μ(E) (density cancels), but composition changes the photoelectric part at low energy.
  4. *This model's own omissions* (all would *raise* the computed kerma, so they cannot explain the K and Th excess):
     transitions without intensities (AB-4d, listed below), β and secondary-electron bremsstrahlung (not transported),
     K fluorescence (bounded below, ≤ 0.12–0.26 %). Coherent scattering (≤ 0.12 %, see limits) and free-electron
     Klein–Nishina angles (unquantified, < 100 keV region) can go either way.

## Validation (a): uncollided line fluence and zenith distribution vs the analytic half-space kernel

Rule declared before the production run: |MC − analytic| ≤ 6 SE, SE from the pooled per-history second moments (each
history scores at most one uncollided crossing, so per-cell squares are per-history moments), for the chain total, each
of the 10 upward zenith bins of the total, and the total and each upward bin of the five lines with the largest
uncollided kerma; downward uncollided bins must be exactly zero. The estimator scores the slab h ± 10 cm, so it is
compared with the slab-averaged kernel (`HalfSpaceUncollided` with the transport's own XCOM coefficients, Simpson over
the height). **Rule change, disclosed:** a 2×200000-history smoke run showed cells with zero crossings (K-40 Ar X-rays at
2.96 keV, expected ~1e-19 per Bq/kg, and the 2e-5-yield K-40 511-keV line), where a per-history SE is zero and a k·SE
rule is undefined. Before the production run the rule was amended: cells with fewer than 30 crossings are reported "not
tested" (never "passed"). No tolerance was changed.

| Chain | Total uncollided fluence MC (cm⁻² s⁻¹ per Bq/kg) | Analytic (slab) | Ratio | z | Cells tested / not tested | Max abs z | Downward sum |
|---|---:|---:|---:|---:|---:|---:|---:|
| K-40 | 9.73367e-4 ± 3.5e-7 | 9.73904e-4 | 0.99945 | −1.52 | 31 / 24 | 2.09 | 0 |
| U-238 series | 1.441430e-2 ± 7.4e-6 | 1.441410e-2 | 1.00001 | +0.03 | 66 / 0 | 3.82 | 0 |
| Th-232 series | 1.86011e-2 ± 1.1e-5 | 1.86104e-2 | 0.99950 | −0.89 | 66 / 0 | 2.42 | 0 |

All tested cells pass. Principal lines (all upward bins): K-40 1460.82 keV 0.99945 (z −1.52); Bi-214 1764.491 1.00050
(+0.59), 609.321 0.99949 (−0.49), 1120.294 0.99957 (−0.36), 2204.1 1.00128 (+0.99); Pb-214 351.932 0.99720 (−1.63);
Tl-208 2614.511 1.00025 (+0.49), 583.187 0.99860 (−0.87); Ac-228 911.204 1.00023 (+0.18), 968.971 0.99969 (−0.19),
1588.2 1.00063 (+0.25). Per-cell values: `ambient-baseline-v1-turn6-generator.json`, `Chains[].UncollidedValidation`.
The slab average differs from the point value by ~μ_a δ²/(6h) ≈ 2e-5 (relative) for the uncollided kernel.

## Source catalog (AB-4 + AB-4d)

`samples/ambient/build_catalog.py` → `samples/ambient/terrestrial-source-catalog-v1.json`. Data: IAEA LiveChart of
Nuclides API (ENSDF decay radiations, X-ray rows, ground-state decay modes), 96 snapshots under
`samples/ambient/source-data/v1/` (3 per nuclide: `<n>.csv` photons, `<n>-x.csv` X-ray shells, `<n>-gs.csv` decay modes;
plus the existing `214bi.csv` of 2026-10-02, kept unchanged — today's download is byte-identical apart from the
extraction-date column), every SHA256 in `fetch-manifest.json` with URL and extraction date (2026-10-02 / 2026-10-04),
checked before use. Reproduce the snapshot with `python samples/ambient/fetch_source_data.py --out <folder>` (never
overwrites an existing file). Evaluators/cutoffs per member are written into the catalog (`Members[].Evaluation`).

| Chain | Lines included | Photons per chain decay | Emitted photon energy per chain decay | Not-included entries |
|---|---:|---:|---:|---:|
| K-40 | 4 | 0.115432 | 155.760 keV | 3 |
| U-238 series | 939 | 3.062080 | 1762.655 keV | 115 |
| Th-232 series | 406 | 3.832563 | 2360.527 keV | 23 |

Rules and normalisation:
- Only photon rows with an evaluated absolute intensity. LiveChart intensities are per 100 decays of the parent state
  (all modes): Bi-212 727.33 keV 6.67 % sits on the 64.06 % β branch; Tl-208 2614.511 keV 99.754 % per Tl-208 decay; turn 5
  checked Bi-214 609 keV against the ENSDF normalisation. Yield per chain decay = occurrence × I/100.
- The photon file already contains the X-ray rows; they are identified against the X-ray file by (energy, intensity,
  parent level, mode). LiveChart's `KB` row is the sum of the K'β1 + K'β2 rows (checked to rounding of the printed digits
  for every nuclide) and is excluded to avoid double counting.
- Occurrences from the evaluated branchings (`<n>-gs.csv`): Pb-214 0.9998 (Po-218 α), At-218 0.0002 (β), Rn-218 1e-7,
  Bi-214 0.9999999, Po-214 0.99979, Tl-210 0.00021, Hg-206 2e-8, Tl-206 1.34e-6; Th chain: Po-212 0.6406, **Tl-208 0.3594
  (fed only through the Bi-212 α branch)**. The evaluated Po-216 record has α 100 % only, so no At-216 member (its
  snapshot is kept as the record of that check).
- **Pa-234 / Pa-234m, resolved from the current evaluation** (S. Ota, Nuclear Data Sheets 207, 351 (2026), cutoff
  2023-12-01; adopted levels and Th-234 β-decay PDFs, hashes in the summary JSON): the isomer is the (0−) 76.5-keV level
  (LiveChart's 2006 A=234 data still label it 73.92 keV; only its line intensities are used, e.g. 1001.03 keV 0.842 %
  = 2026's 0.842 per 100 decays). Th-234 β feeds the 73.920-keV (3+) level directly with 0.150(17) % (→ Pa-234 g.s.);
  every other Th-234 decay reaches the isomer, %IT = 0.16(4), %β = 99.84(4). So per chain decay n(Pa-234m) = 0.99850 and
  n(Pa-234 g.s.) = 0.00150 + 0.99850 × 0.0016 = 0.0030976 — **not 1 each**. The evaluation also states that the IT path
  (unobserved 2.6-keV transition) ends at the same 73.920 level; if the observed 73.92-keV de-excitation were wholly the
  IT path, n(Pa-234 g.s.) would be 0.0016 instead. Pa-234 g.s. lines carry 4.746 keV of the U chain's 1762.655 keV
  (0.27 %); the alternative would lower the chain's emitted energy by 0.13 %. Both published values are used as given.
- AB-4c omission bounds of turn 5 kept on record unchanged (Po-218 α, Po-218 β, Bi-214 α, and the stopped Bi-214
  unresolved-β envelope), copied into the catalog and the spectrum's `NotIncluded`.

**Full "not included" list** (also in the catalog and, with the AB-4c records and the 4 lines below 10 keV, as the 149
`NotIncluded` entries of the spectrum file; energies in keV, `<nuclide> <mode>`; `234pam` = the isomer, `234pa` = g.s.):

| Chain | Reason | Entries |
|---|---|---|
| K-40 | no evaluated photon intensity (0) | — |
| K-40 | K'β sum rows; their K'β1/K'β2 components are included (2) | 40k 3.19, 40k 3.19 |
| K-40 | below the 1 keV lower limit of the XCOM transport data | 40k 0.265 (Ar L X-ray) |
| U-238 series | no evaluated photon intensity (98) | 206tl B-: 1166.4; 210bi A: 265.832, 304.896; 214bi A: 62.5, 191.1; 214bi B-: 36.8, 61, 71.1, 104.4, 230.66, 247.2, 255.16, 280.6, 297.81, 314.9, 422, 551.9, 579.14, 581.9, 598.5, 710.27, 866, 891.8, 965, 1011.8, 1206.4, 1253.14, 1317.7, 1361.2, 1387.5, 1415.49, 1532.8, 1644, 1693.4, 1723.7, 1739.1, 1747.2, 1782.1, 1943.7, 1953.4, 2017.31, 2184.8, 2193.3, 2358, 2396.5, 2413.1, 2421, 2430, 2459, 2469.4, 2529.7, 2540.3, 2555.1; 214pb B-: 9.5; 218at A: 10, 53.3; 226ra A: 187.1; 230th A: 67.81; 234pa B-: 41.82, 75, 233.6, 235.9, 275.04, 365, 446.6, 461.5, 468, 478.6, 498, 529.1, 558, 565.2, 596.9, 617, 634.3, 653.7, 685.1, 699.03, 713.7, 755, 799.7, 810, 1009.9, 1151.4, 1838; 234pam B-: 41.82, 43.498, 99.9, 233.6, 236, 811; 234pam IT: 10; 234th B-: 10, 73.92, 92, 103.71, 132.9; 234u A: 634.9 |
| U-238 series | K'β sum rows; components included (16) | 238u 106.894, 234th 109.74, 234pam 112.645, 234pa 112.645, 234u 106.894, 230th 101.37, 226ra 96.054, 222rn 90.941, 214pb 88.458, 218rn 90.941, 214bi 90.941, 214po 86.022, 210tl 86.022, 206hg 83.631, 210po 86.022, 206tl 86.022 |
| U-238 series | no evaluated photon-emission data at all (LiveChart no-data response) | 218po (AB-4c bounds on record) |
| Th-232 series | no evaluated photon intensity (13) | 212bi A: 124.1, 144.94; 212bi B-: 130, 1800.2; 212pb B-: 42.11, 47.91, 48.56, 52.91, 56.72, 123.5, 164.2; 228ra B-: 15.15, 30.6 |
| Th-232 series | K'β sum rows; components included (10) | 232th 101.37, 228ac 106.894, 228th 101.37, 224ra 96.054, 220rn 90.941, 216po 86.022, 212pb 88.458, 212bi 83.631, 212bi 90.941, 208tl 86.022 |
| spectrum file only | transported, but below the 10 keV ICRP 74 table limit used by the engine | K-40 2.956, 2.958 (Ar Kα); Ra-228 6.28, 6.67 |

Parent states present in the snapshots but not populated by the chains (not transitions of the chain; listed in the
catalog's `ExcludedParentStates`): U-238 2557.9-keV fission isomer, Bi-210m 271.31, Tl-206m 2643.1, Bi-212m 239,
Po-212m 2930.

## Material data

`samples/ambient/build_materials.py` → `samples/ambient/materials-v1.json` (provenance and hashes:
`source-data/v1/materials-sources.json`). No coefficient is fitted; the mixture rule is the only operation.
- **Photon cross sections:** NIST XCOM (M. J. Berger, J. H. Hubbell, S. M. Seltzer et al., NIST Standard Reference
  Database 8), element files `MDATX3.zzz` and `ATWTS.DAT` extracted unchanged from
  https://physics.nist.gov/PhysRefData/Xcom/XCOM.tar.gz (archive SHA256 b2bdd060…cc0de; H, C, N, O, Al, Si, Ar, Fe
  committed under `source-data/v1/xcom/`): coherent, incoherent, photoelectric, pair (nuclear + electron), barns/atom →
  cm²/g with XCOM's own Avogadro constant and atomic weights. The XCOM web form needs a POST, which this turn did not
  use (read-only GET only), so the element data files were used instead.
- **Air μ_en/ρ:** NIST, J. H. Hubbell and S. M. Seltzer, NISTIR 5632 (1995), dry air table (`nist/air.html`).
- **Dry air:** NIST ESTAR material 104 — C 0.000124, N 0.755267, O 0.231781, Ar 0.012827 by mass, density
  1.20479e-3 g/cm³. NIST does not print the temperature/pressure of that density; the ideal-gas law with this
  composition as N2/O2/Ar/CO2 (mean molar mass 28.964 g/mol, R = 8.314462618 J/(mol K), exact SI) gives 293.0 K
  (19.8 °C) at 101.325 kPa — stated conditions: **≈ 20 °C, 101.325 kPa**, derived, not quoted. Kerma per Bq/kg depends
  on air density only through μ_a h ≈ 0.01, so this is not a sensitive input.
- **Soil:** H. L. Beck, J. DeCampo and C. Gogolak, HASL-258 (1972), Table 2: Al2O3 13.5 %, Fe2O3 4.5 %, SiO2 67.5 %,
  CO2 4.5 %, H2O 10 % by weight (p. 44), density 1.6 g/cm³ (p. 10); read in the DOE compendium PDF (SHA256 015411a7…,
  pages 70 and 104; not committed, 8 MB). Elemental mass fractions: H 0.01119, C 0.01228, O 0.55809, Al 0.07145,
  Si 0.31552, Fe 0.03147.
- **Checks:** composed air μ/ρ vs NIST's tabulated air μ/ρ at shared grid energies: worst |ratio − 1| = 4.45e-4 (NIST
  prints 4 significant figures). Each partial is interpolated log-log separately; the difference to a log-log quadratic
  at mid-grid points is ≤ 0.4 % (interpolating the sum would reach 4 % near the photoelectric/Compton crossover).

## Generator physics and limits

`src/Gcam.Simulation/SoilAirTransport.cs` (+ `KleinNishina`, `ExponentialIntegral`, `PhotonMaterial`,
`SoilAirMaterials`, `TerrestrialCatalog`, `TerrestrialSpectrumBuilder`), CLI `montecarlo ambient-terrestrial`.
- Geometry: uniform, laterally infinite soil half-space, infinite uniform dry air above, scoring at h = 100 cm.
  Lateral invariance → point fluence = plane average; track-length estimator in the slab h ± 10 cm (unbiased for the
  slab average; slab-vs-point ≈ 2e-5).
- Source: lines sampled ∝ yield·E/μ_soil, depth from λe^(−λd), λ = 0.5 μ_soil(E₀), weight e^(λd)/λ (unbiased; finite
  variance because μ never falls below μ_soil(E₀) along a history below 20 MeV); isotropic emission. Everything else
  analog (no roulette, no splitting).
- Photoelectric: absorption ends the history. **K fluorescence not emitted; bounded**: a next-event tally gives each
  photoabsorption one K X-ray (ω_K = 1) at the worst-case energy between 1 keV and the largest K edge below the absorbed
  energy, attenuated by photoelectric absorption only, read conservatively from a distance table. Bound ≤ **0.116 %**
  (K-40), **0.247 %** (U), **0.255 %** (Th) of the air kerma; 8–9 % of it from soil absorptions, the rest from air within
  centimetres of the point (Ar). L/M-shell fluorescence (< 1 keV for Z ≤ 26) is below the data range and **not bounded**.
- Incoherent: XCOM bound-electron cross section for the interaction probability; **free-electron Klein–Nishina** angles
  and energies (Kahn's rejection method), no incoherent scattering function, no Doppler broadening. Expected effect:
  too much small-angle scattering below ~100 keV, i.e. the low-energy end of the scattered continuum; not quantified.
- Coherent: **treated as no interaction** (energy kept; small-angle at these energies). Opposite extreme computed for
  the uncollided kerma (coherent removing the photon): difference 0.05 % (K), 0.12 % (U), 0.12 % (Th) of the total.
- Pair (nuclear + electron field): two back-to-back isotropic 511-keV photons at the interaction point; positron range
  and annihilation in flight neglected. 3.6e5 / 9.2e5 / 3.1e6 pair events (K / U / Th).
- Not modelled: β bremsstrahlung (out of scope) and secondary-electron bremsstrahlung; atmospheric density gradient;
  ground roughness, buildings, vegetation; energies below 1 keV (cutoff; no history reached it).
- Kerma: Σ track × E × (μ_en/ρ)_air × 0.5767835882 → nGy/h.
- Reproducibility: one random stream per (chain, seed) (`DefaultRandom.FromKey(Key(seed, 30001 + chain))`); a full re-run
  with the final build reproduced the results JSON and the spectrum file byte for byte.

## The spectrum file

`samples/ambient/terrestrial-unscear2000-v1-NOT-VALIDATED.json` (+ `.sha256`), compact JSON, 1.9 MB. File SHA256
`a4f735010fa7f97685d32974d0dddd425fa9d5717be539b26b5fddbb5ccfb41c`, content hash
`1b2b3f479f2669d7ab9ff07af2fb13a54ac06b027ebca6ca432ddf3c9d21fd2e`. `IsValidated = false`; the engine accepts it for
development acquisitions and rejects it where `RequireValidatedSpectrum` is set (tested).
- Weights: scalar fluence, photons/(cm² s), at the UNSCEAR activities. 1346 discrete lines: 1345 catalog lines ≥ 10 keV
  (uncollided, analytic kernel with the transport's coefficients, validated by (a)), plus the uncollided 511-keV
  annihilation line (MC). 204 scattered-continuum bins (MC; 5 keV wide 10–100 keV, 20 keV wide 100–4000 keV; empty bins
  omitted). 16292 `EnergyZenith` rows, 20 equal zenith-cosine bins over [−1, 1]; zenith cosine = propagation direction ·
  world +y (up), ground below, azimuth uniform — the turn-5 convention; uncollided ground lines have μ ≥ 0 only.
- `NotIncluded` (149, new optional `IncidentSpectrum` member, hashed when present): the catalog's list, the four
  AB-4c records and the four lines below 10 keV. Spectra without it keep their old payload hash (the placeholder file
  hash `3600e22b…` test still passes).

## Tests and build

`dotnet build Gcam.sln -c Release`: 0 errors, 2 warnings (the pre-existing xUnit2012 in WaveformServiceTests and
xUnit2000 in ImagingServiceTests). `dotnet test Gcam.sln -c Release --no-build` with process-local
`GCAM_UI_TESTS=0 GCAM_RENDER_SNAPSHOTS=0 GCAM_EVIDENCE_TESTS=0`:

| Suite | Before | After |
|---|---:|---:|
| Engine (Gcam.Tests) | 330 | **355** passed |
| Studio.Core | 184 | 184 passed |
| Studio services | 87 + 7 skipped | 87 + 7 skipped |
| UI project (headless) | 13 + 14 skipped | 13 + 14 skipped |
| Render (opt-in off) | 0 + 1 skipped | 0 + 1 skipped |

25 new engine tests (6 files): Klein–Nishina sampler vs the analytic density (3 energies, 20 bins, N = 200000, 4σ
binomial) and rotation identities; E₁ vs independent quadrature; materials (NIST-rounding check 5e-4, exact grid
reproduction, rejection of tampered hash / negative / unordered / short arrays / out-of-range energy); catalog (AB-4d
facts: Bi-214 β 36.8/61.0/71.1/104.4 listed and not lines, Po-218 no-data, 4 AB-4c records, Tl-208 = 0.3594 × 0.99754;
rejection of wrong hash, negative or zero yield, zero energy, totals not matching lines, empty reason, no chains, wrong
schema, missing activity, duplicate chain name); snapshot manifests still hash-match; transport reproducibility per seed,
uncollided vs analytic kernel (N = 400000, 4σ per-history SE, two lines, 10 bins each), exact-zero downward bins, no pair
production below threshold, bookkeeping identities; committed spectrum: sidecar and content hashes, NOT-VALIDATED state,
NotIncluded content, tamper detection, exact marginal identities, μ ≥ 0 for catalog lines, and loading into
`AmbientPhotonProcess` (accepted for development, rejected when a validated spectrum is required). Ambient-null /
legacy Stop-Continue tests unchanged and passing (AB-8). One new test assertion of my own was ill-posed (comparing a
Compton identity to 12 *decimal places* across a rounding boundary) and was corrected to a 1e-12 relative rounding
check before the final run; no physics tolerance was changed.

## Deviations from the plan / prompt, and items for the planner

1. Coherent scattering omitted as "no interaction" (allowed by the prompt with a stated effect; quantified above).
2. Lines in the spectrum file come from the analytic kernel (validated by the MC) rather than MC tallies; the 511-keV
   annihilation line and the continuum are MC.
3. Validation (a) rule amended after the smoke run, before production: < 30 crossings = "not tested" (disclosed above).
4. New file `samples/ambient/source-data/.gitattributes` (`* -text`): the repository normalises text to LF, which would
   change the bytes (and SHA256) of snapshots that arrived with CRLF (the NIST HTML pages). The existing `214bi.csv` is
   LF and unaffected. Planner: please confirm this repository-config addition.
5. Python builders write LF explicitly so the pinned catalog/material hashes (`terrestrial-generator-v1.json`) are the
   same on every OS.
6. Pa-234 g.s. normalisation uses both published Ota (2026) values (0.150 % direct β feeding + 0.16 % IT); alternative
   effect 0.13 % of the U chain's emitted photon energy (above).
7. Data access: XCOM via the downloadable element files (POST form not used); Saito & Jacob (1995) and ICRU 53 not
   accessed (UNSCEAR's inputs not compared one by one).

## What could not be run / not done

UI tests and render snapshots (not authorised / not needed: no view change). AB-7, AB-9, Studio default, VV/README/PAPER/
Findings — stopped per AB-4d. No secondary-electron bremsstrahlung bound; no L-shell fluorescence bound; no
incoherent-scattering-function correction.

## Reproduce

```powershell
python samples/ambient/fetch_source_data.py --out samples/ambient/source-data/v1        # skips existing snapshots, rewrites the manifest
python samples/ambient/build_catalog.py --out samples/ambient/terrestrial-source-catalog-v1.json
python samples/ambient/build_materials.py --out samples/ambient/materials-v1.json
dotnet build Gcam.sln -c Release
dotnet src/Gcam.Cli/bin/Release/net9.0/Gcam.Cli.dll ambient-terrestrial samples/ambient/terrestrial-generator-v1.json <output-folder>
```

APPROVAL REQUESTS: none. No delete, move-over, install, git state change, process stop, upload or desktop action was
performed; temporary files are under `%TEMP%\gcam-ambient-turn6`.
