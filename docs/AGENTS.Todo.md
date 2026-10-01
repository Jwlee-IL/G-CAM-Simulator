# AGENTS.Todo — open work items handed between sessions

Scope: concrete, ready-to-start tasks with enough context that another session (or person) can pick one up
cold. Longer-term or undecided work stays in [AGENTS.Backlog](AGENTS.Backlog.md); results go to
[AGENTS.Findings](AGENTS.Findings.md). A task that is finished is deleted here and recorded in the Backlog's done
list (and Findings, if it produced a result) in the same commit.

| ID | Task | Origin | Owner | State |
|---|---|---|---|---|
| TODO-01 | Add Ir-192 to the engine's isotope set | [VV.Gcam.URS §5](VV.Gcam.URS.md#5-reference-sources) reference source RS-1 | Studio session | **done** 2026-10-01 (themes 51–52; Backlog done list) — PRS/URS updates below are for the VV owner |
| TODO-02 | Energy- and material-dependent crystal attenuation | found while doing TODO-01 (theme 51) | Studio session | **done** 2026-10-01 (theme 52; option chosen: re-run and re-record the affected themes) |
| TODO-03 | Re-run the PRS rows that still quote the pre-theme-52 crystal model (marked † in the PRS) | [VV.Gcam.PRS](VV.Gcam.PRS.md) crystal-model caveat; theme 52 | — (pair session proposed) | open |
| TODO-04 | Simulate the adopted narrow-FOV path: usable field of non-cyclic decoding, and an out-of-field cue | [VV.Gcam.Limitations](VV.Gcam.Limitations.md#lim-01--narrow-field-of-view) LIM-01, decision D-16 | — (pair session proposed) | open |

---

## TODO-01 — Add Ir-192 to the engine's isotope set

**Why.** Ir-192 (industrial gamma radiography, IAEA category 2, typical 3.7 TBq) is reference source **RS-1** in
[VV.Gcam.URS §5](VV.Gcam.URS.md#5-reference-sources), and PR-SENS-07 in [VV.Gcam.PRS](VV.Gcam.PRS.md) needs it (PR-SENS-06 was withdrawn)
simulated. Today `Isotopes.All` has only Cs-137, Co-60, Co-57, Na-22 and Am-241, so RS-1 cannot be run.

### Nuclear data (to verify before use)

IAEA LiveChart of Nuclides, ENSDF evaluation (C. M. Baglin, 2012), ground state only (T½ = **73.829 d** =
0.20213 y; β⁻ 95.24 %, EC 4.76 %). Intensities are photons per 100 parent decays:
<https://www-nds.iaea.org/relnsd/v1/data?fields=decay_rads&nuclides=192ir&rad_types=g>

| keV | per 100 decays | branch |
|---|---|---|
| 316.506 | 82.86 | β⁻ |
| 468.069 | 47.84 | β⁻ |
| 308.455 | 29.70 | β⁻ |
| 295.957 | 28.71 | β⁻ |
| 604.411 | 8.216 | β⁻ |
| 612.462 | 5.34 | β⁻ |
| 588.6 | 4.522 | β⁻ |
| 205.8 | 3.31 | EC |
| 484.6 | 3.19 | EC |

Lines above 50 keV sum to ≈ 2.33 photons per decay. Pt / Os K X-rays (61–72 keV, a few % each) exist; include
them only if a study needs the low-energy region (Cs-137 includes its Ba K X-rays for that reason — see the
comment in `Isotopes.All`).

### Steps

1. **`src/Gcam.Configuration/Scene.cs` → `Isotopes.All`**: add
   `new IsotopeInfo("Ir-192", 0.20213, [(316.5, 0.8286), (468.1, 0.4784), (308.5, 0.2970), (296.0, 0.2871), (604.4, 0.0822), (612.5, 0.0534), (588.6, 0.0452), (205.8, 0.0331), (484.6, 0.0319)])`.
   Keep 316.5 **first**: `Lines[0]` is treated as the primary line / photopeak centre (`SceneSource.ToConfig`,
   `MixedFieldTests`, the WPF spectrum window).
2. **`samples/isotopes/ir192.json`**: a preset in the same format as `co60.json` (`name`, `halfLifeDays`,
   `cascadeCoincident`, `lines[{keV, branching}]`, `notes`). `rtl/multi_isotope_study.py` globs this folder, so the
   RTL multi-isotope study picks it up automatically — check its plots still make sense.
3. **Energy-dependent attenuation**: `CodedApertureMask` (tungsten μ(E)/μ(662)) and `EntranceAbsorber` (iron) use
   NIST-XCOM ratio tables. Confirm the tables cover 200–620 keV with points close enough for these lines; add XCOM
   points if needed. Same check for the crystal attenuation in `ComptonModel`.
4. **Cascade (`src/Gcam.Simulation/DecayScheme.cs`)**: Ir-192 has real cascades (e.g. 468 → 316 keV), so true
   coincidence summing exists. Either model the main cascade with its correlation, or leave Ir-192 out of
   `DecayScheme` and say so in the preset's `notes` and in Findings. Do not invent a correlation.
5. **Studio / WPF**: both list `Isotopes.All`, so Ir-192 appears in the isotope pickers automatically. Check that
   `SourceItemViewModel`'s "unknown isotope → first entry" rule still behaves (Cs-137 must stay first).
6. **Tests** (`tests/Gcam.Tests`): line data sum and ordering (`Lines[0]` = 316.5); a mixed-field run with Ir-192
   localises within the existing tolerance; an energy-window test around 316 keV.
7. **Verify**: cross-check the line table against a second source (NNDC NuDat) and run a Codex read-only review
   of the change (project rule: every physics input is verified).
8. **Use it**: simulate RS-1 at the reference conditions of URS §5 (10 mSv/h and 10 µSv/h at the device) and
   compare the detected count rate with the analytical estimate there (1.0 Mcps and 1.0 kcps). Record the result as
   a new Findings theme and update PR-SENS-05 / PR-SENS-07 in the PRS (grade AN → MC).

### Done when

- Ir-192 runs in `montecarlo` single-run, `mixedfield`, `compton` and in GCAM Studio;
- tests above pass (`dotnet test Gcam.sln`), counts updated in AGENTS.md;
- the RS-1 comparison is in Findings and the PRS rows are re-graded.

### Status (2026-10-01, Studio session)

Steps 1–7 done and recorded as [Findings theme 51](AGENTS.Findings.md): isotope entry + preset, NIST tungsten points
200–600 keV (the old table was 2–7 % too opaque at these lines), cascade explicitly not modelled
(`DecayScheme.From("Ir-192")` throws), Studio picker, 12 engine tests + 1 Studio test, Codex review passed.
Notes: `montecarlo mixedfield` and `rtl/multi_isotope_study.py` use fixed Cs / Co / Co-57 fields, so Ir-192 is
exercised there only through `MixedFieldStudy` in `Ir192Tests`; `montecarlo compton` uses the config's primary line.
**Step 8 is blocked by TODO-02**: with the current crystal model the MC would reproduce the URS's 662 keV-stopping
assumption instead of testing it.

---

## TODO-02 — Energy- and material-dependent crystal attenuation

**Why.** Found while checking TODO-01 step 3 against NIST (computed from the elemental μ/ρ tables for
Gd₃Al₂Ga₃O₁₂):

| keV | NIST GAGG μ(E)/μ(662) | `ComptonModel.MuRel` | `CrystalDetector` |
|---|---|---|---|
| 205.8 | 4.16 | 6.18 | 1 (energy-independent) |
| 316.5 | 2.06 | 3.16 | 1 |
| 468.1 | 1.32 | 1.72 | 1 |
| 1332 | 0.64 | 0.34 | 1 |

`CrystalDetector` (the default path) scores every energy with the 662 keV stopping — 55 % for 15 mm GAGG where NIST
gives ~81 % at 316 keV. `ComptonModel.MuRel` is one power law for every crystal material.

**Proposed.** A μ(E)/μ(662) table per crystal preset (GAGG, GAGG:Ce,Mg, CeBr₃, LaBr₃, LYSO, BGO, NaI), computed from
NIST elemental μ/ρ and the composition, used by both detectors; tests pin a few points against NIST.

**Decision needed first.** This changes the Compton / cascade / non-proportionality studies (themes 15–17, 44, 46)
and the efficiency of every non-662 keV line in the default path. Re-run and re-record those themes, or keep them
as dated results and apply the fix forward only.

### Hand-over for the VV documents (2026-10-01)

- URS §5 "reproduces the MC lab efficiency within 8 % (2.44 × 10⁻⁴ vs 2.65 × 10⁻⁴)": with the corrected crystal the
  MC (Cs-137, theme-22 head) is **2.48 × 10⁻⁴** — the analytic estimate is now within 2 %.
- RS-1 (Ir-192): per photon the MC gives 2.62 × 10⁻⁴ vs the analytic 2.44 × 10⁻⁴ (662 keV stopping for every line), so
  the RS-1 rates are ~7 % higher: **≈ 1.07 Mcps at 10 mSv/h, ≈ 10.7 kcps at 100 µSv/h, ≈ 1.07 kcps at 10 µSv/h**.
  PR-SENS-05 / PR-SENS-07 can move from AN to MC on these numbers (`samples/scenario_handheld_ir192.json`).
- Any requirement or claim citing themes 15–17 / 26 for spatial Cs/Co separation: it now holds only up to
  Co:Cs ≈ 2:1 activity; the spectral (stripping) lever still recovers the Cs count (theme 52).
- When both TODOs are acknowledged, delete them here (the Backlog done list already records them).

**VV owner acknowledgement (2026-10-01).** Done on the requirement side: the spatial-separation limit is in
PR-NRG-04 and the new LIM-08; PR-SENS-01 is updated to 2.48 × 10⁻⁴; the URS §5 estimates quote the theme-52 MC
(2 % for Cs-137, Ir-192 ≈ 1.07 Mcps / 1.07 kcps). PR-SENS-05 / -07 stay AN until they are simulated at field
distance. TODO-01 and TODO-02 can be deleted once the author agrees.

---

## TODO-03 — Re-run the PRS rows that still quote the pre-theme-52 crystal model

**Why.** Theme 52 replaced the crystal model with tabulated GAGG cross sections; the old one absorbed too much and
too photoelectrically. Three PRS rows still quote numbers measured with it and are marked **†**:

| PRS row | Claim as written | Source theme | Re-run |
|---|---|---|---|
| PR-IMG-02 | localisation floor ≤ 0.34 mm RMS; sub-mm at ≥ 250 counts on axis, ≥ 500 at the FCFOV edge | 22 (`samples/scenario_handheld.json`, `noise` / `sweep`) | `montecarlo noise` and `sweep` on the handheld scenario |
| PR-IMG-03 | three isotopes (Cs-137 + Co-60 + Co-57) each localised < 1 mm in one run | 26 (`mixedfield`) | `montecarlo mixedfield` |
| PR-NRG-03 | Argmax positioning: ~1.6× the counts of per-pixel windowing (38 % vs 24 %) at equal or better RMS (0.74 vs 0.78 mm) | 15 (`compton`) | `montecarlo compton` — theme 52 already shows per-pixel at 6 % / 5.65 mm; Argmax not reported |

Also: the "2.45× the original rig" sensitivity ratio (theme 22, `scenario_orig_gagg.json`) was dropped from
PR-SENS-01 because it used the old model — re-compute it if the comparison is still wanted.

**Done when** each row's numbers are re-measured and recorded (Findings, as an addendum to the source theme or in
theme 52), and the VV owner updates the PRS rows and removes the † marks.

## TODO-04 — Simulate the adopted narrow-FOV path

**Why.** Decision D-16 adopted a software path for the narrow field of view (fully coded ±3.6° at the theme-22
geometry). Two of its parts have no simulation evidence yet ([LIM-01](VV.Gcam.Limitations.md#lim-01--narrow-field-of-view)):

1. **Usable field of non-cyclic decoding.** The PRS estimates ~±5° from the theme-22 area ratio (360 / 625 vs
   171 / 625 grid positions, ≈ 2.1× the area). Measure the localisation success and RMS versus off-axis angle at
   a **field distance** (e.g. 1–5 m) with the theme-52 crystal model, ideally with an ambient background
   (`BackgroundConfig`, theme 28).
2. **Out-of-field cue.** A source outside the fully-coded field: can the detector say which side it is on — from
   the counts the in-field decode cannot explain, or from the partially-coded edge — and at what angle and count
   level does the cue become reliable? Nothing exists yet; start with a study that sweeps one source from 0° to
   ~20° off-axis and reports what a left / right decision would get right.

**Done when** both are in Findings with reproduce commands, and the VV owner can move PR-IMG-10 / PR-IMG-08 and
LIM-01 / LIM-07 from estimates to MC.
