# PLAN.Physics.CsIData.Review — CsI(Tl) transport data and Studio availability

Scope: review only of TODO-21: add a CsI host transport table by the theme-52 method, validate it, and expose the existing CsI(Tl) front-end preset. No implementation, installation, physics-data generation, desktop activity, or changes to other plans or task/result logs in this turn.

Status: reviewed 2026-10-02; implementation awaits the planner's adopted decisions and a permitted data-generation route.

## Assessment and checked premises

TODO-21 and [PLAN.Studio.Waveform](PLAN.Studio.Waveform.md), D-3, agree. The reference geometry uses GAGG; GAGG remains the default and CsI is a what-if comparison. There is no separate CsIData implementation plan in this checkout; this review supplies proposed decisions for the planner to adopt.

| Checked code | Finding |
|---|---|
| `samples/materials/gen_crystal_tables.py` | Existing reproducible method: xraylib compound photoelectric and incoherent cross sections through 800 keV, automatic K-edge pairs, then analytic Compton plus extrapolated photoelectric absorption. CsI is absent from `mats`. Output is `crystal_tables.json` in the invocation's working directory. |
| `src/Gcam.Detector/CrystalMaterial.cs` | Seven entries; arrays rounded for C#; log-log interpolation, endpoint clamping, density conversion, reference normalization. Unknown/ideal names fall back to GAGG. NaI:Tl transport already uses the undoped NaI formula. |
| `src/Gcam.Configuration/FrontEndParts.cs` | CsI(Tl) response already exists: density 4.51 g/cm³, light yield 54 photons/keV, decay 1000 ns, intrinsic floor 0.045. Default is the first, GAGG, preset. |
| `src/Gcam.Configuration/FrontEndMaterials.cs` | Filters CsI(Tl) out; explicit mapping lacks CsI; unsupported names throw. |
| `src/Gcam.Studio.Core/ViewModels/MainViewModel.cs` | Selector consumes `FrontEndMaterials.Scintillators`. Existing physical-input locks require Reset before changing material when data exist, rather than marking a retained acquisition stale. Preserve this current behavior. |
| `src/Gcam.Studio.Services/SimulationService.cs`, `WaveformService.cs` | BuildConfig maps the selected material; waveform processing validates the same mapping. |
| `src/Gcam.Simulation/ListModeSource.cs`, `src/Gcam.Detector/ComptonCrystalDetector.cs` | List-mode passes `CrystalMaterial.ForConfig(d.Material)` into transport. With no explicit positive attenuation override, Compton transport uses that material's own reference attenuation. |

## Proposed material and generation decisions

1. Add `("CsI", "CsI", 4.51, "CsI:Tl")` to the generator, and a corresponding entry in `CrystalMaterial.All` with key `CsI`, label `CsI:Tl`, formula `CsI`, density **4.51 g/cm³**. Composition is one Cs and one I atom: NIST mass fractions Cs **0.511549**, I **0.488451**. Density and composition are independently published in [NIST Table 2](https://physics.nist.gov/PhysRefData/XrayMassCoef/tab2.html).
2. Treat Tl as an omitted activator in the transport composition, exactly as the existing NaI:Tl table omits Tl and other activated crystals omit their dopants. Keep the existing CsI(Tl) response preset. Do not invent a Tl concentration, attenuation correction, or Tl K edge. Document this host-material approximation; its error has not been quantified here. A concentration-specific transport model would need the actual supplier composition and separate validation.
3. Retain the base grid, in keV: `20, 30, 40, 50, 60, 80, 100, 150, 200, 300, 400, 500, 600, 661.7, 800, 1000, 1250, 1500, 2000, 3000`. Add two pairs using the existing algorithm `round(EdgeEnergy(Z, K_SHELL) * (1 ± 1e-4), 4)` for I (Z=53) and Cs (Z=55). These represent the **33.2 and 36.0 keV** K edges, respectively; do not use those rounded descriptions as the actual grid nodes. NIST gives I **33.1694 keV** and Cs **35.9846 keV** in its [CsI attenuation table](https://physics.nist.gov/PhysRefData/XrayMassCoef/ComTab/cesium.html). At those edge energies the computed pairs would be `33.1661 / 33.1727` and `35.9810 / 35.9882` keV; verify the precise xraylib 4.3.0 edge values when generating. The expected CsI grid has 24 points.
4. For `E ≤ 800 keV`, use `p = CS_Photo_CP("CsI", E)` and `c = CS_Compt_CP("CsI", E)` in cm²/g. Store `mu = p + c`, `pf = p / (p + c)`. Obtain composition/atomic weights from `CompoundParser`/`AtomicWeight`, as the existing generator does. Preserve API/version provenance; [xraylib's API declarations](https://github.com/tschoonj/xraylib/blob/master/include/xraylib.h) expose these compound functions and [4.3.0](https://pypi.org/project/xraylib/4.3.0/) is the version used in theme 52.
5. Respect the existing **800 keV spline limit**. For higher grid energies, use `c(E) = kn(E) * N_A * sum(w_Z * Z / A_Z)` and `p(E) = p(800) * (E/800)^a`, where `a = ln[p(800)/p(600)] / ln(800/600)`. Do not call the xraylib photo/incoherent splines above their limit. Keep the same electron rest energy, classical radius and Avogadro constant as the generator. This is an approximation above 800 keV, not additional tabulated xraylib data.
6. Keep coherent scattering and pair production excluded, consistently with theme 52. Do not confuse the stored interaction attenuation with NIST total attenuation, or NIST energy-absorption attenuation with photoelectric share. Continue the existing 20–3000 keV table range and clamping; do not extend the engine's physics claims to 3 MeV merely because a table node exists there.
7. Generate into a permitted scratch directory, retain full-precision JSON and a provenance/check report in the repository as reviewable evidence, and copy only the new CsI row into C# with the existing precision (mu about five significant digits; pf about four). Regenerate existing rows for comparison, but leave their committed C# arrays untouched unless a discrepancy is explained and separately adopted. Record generator revision, Python/package versions, command, units, source URLs, and edge energies. No Python dependency is added to the .NET runtime.

## Data generation prerequisite and independent checks

The planner reports that xraylib is absent from this machine's Python. I have not independently imported/probed Python: its installation and libraries may be outside the allowed read roots. No install was attempted. The preferred route is the scoped installation requested below, followed by running the repository generator with the isolated target explicitly added to its import path and bytecode writing disabled.

An equally traceable route without installing here is for the planner to supply full-precision JSON and a generation report from **this generator with the CsI tuple**, run under xraylib 4.3.0 in a separately authorized environment. The report must include version, exact edge grid, component cross sections, high-energy calculation and NIST checks. Put supplied artifacts inside this worktree for review; do not silently substitute NaI data. NIST's static total/energy-absorption table alone cannot supply photoelectric fractions. A NIST XCOM component export could be a separate traceable alternative, but changing generator methodology would need an explicit planner decision.

Independent verification should include:

- At exact NIST nodes 300, 600, 800, 1000 and 1250 keV, use total μ/ρ references **0.1818, 0.08373, 0.06769, 0.05848, 0.05110 cm²/g**, respectively. These are from the [NIST CsI table](https://physics.nist.gov/PhysRefData/XrayMassCoef/ComTab/cesium.html). Below/equal 800, separately add xraylib coherent scattering to photo+incoherent when comparing like totals. Above 800, report residuals of the analytic extension and the contributions omitted from the NIST total; do not call it an exact match.
- At 661.7 keV, record generated `p`, `c`, `mu`, `pf`, and `MuPerMm = mu * 4.51 / 10`. Check a mass-weighted elemental Cs/I calculation against the compound result. Use NIST elemental interpolation only with its interpolation method and uncertainty stated; 661.7 is not an exact node in the static compound table.
- Print attenuation and photo fractions immediately on both sides of **each** K edge. The Cs jump is smaller than the iodine jump; NIST total jumps are about 1.76× and 3.07×, respectively. Reusing the Gd test's `> 2×` threshold for Cs would be wrong. Confirm direct component jumps, not a fabricated smooth low-energy curve.
- Check sorted unique grid nodes, equal array lengths, finite positive attenuation, photo fractions strictly between zero and one, unit conversion, reference normalization, endpoint clamping, and the high-energy formula at 1000/1250/1500 keV. Check the 800-keV transition against both component prescriptions and report any discontinuity rather than tuning it away.

No CsI cross sections, 662-keV anchor or photoelectric fractions were generated in this review. Numerical assertions for those values must be derived from the traceable output before implementation; they are not guessed here.

## Tests and Studio changes for implementation

| Area | Proposed change and verification |
|---|---|
| `tests/Gcam.Tests/CrystalMaterialTests.cs` | Add a CsI counterpart to the independent NIST-node test. Propose the existing `0.92 ≤ model/NIST total ≤ 1.0` bound at 300/600/1000/1250 keV; first measure CsI component residuals to confirm that bound, and stop/report if it fails. It is not a license to loosen the bound. Add a rounded generated 661.7-keV anchor test for mu, pf and linear attenuation with precision-derived rounding tolerances, plus `MuRel(661.7) = 1`. |
| Material edges and lookup | Add separate I and Cs edge cases at the actual straddling grid nodes: upward jump in attenuation and photo fraction. Assert `above > below`; freeze source-derived jump ratios only after generation with rounding-derived error bounds. Add `Find("csi")`/trim and `ForConfig("CsI")` assertions to prove actual lookup, preserve unknown/null GAGG fallback, update count 7→8. Existing all-material 122/662/1332-keV photo-fraction ordering automatically covers CsI; do not assert monotonicity across K edges. Add high-energy reference cases from the documented extension and endpoint checks. |
| `src/Gcam.Configuration/FrontEndMaterials.cs` | Remove the CsI exclusion and map `"CsI(Tl)" => "CsI"`. Retain throwing for any unmapped scintillator. Keep all existing response numbers and defaults in `FrontEndParts`. |
| `tests/Gcam.Studio.Services.Tests/WaveformServiceTests.cs` | Replace index-based material cases with name-based cases for all five presets (natural order becomes GAGG, NaI, LYSO, CsI, BGO). Assert returned keys resolve to actual `CrystalMaterial` entries, including CsI. Replace CsI rejection with a synthetic unsupported `ScintPreset` rejection. Retain engine-default checks. Cover CsI through the existing waveform/spectrum/imaging shared-response test and assert the captured snapshot carries the CsI chain. |
| `tests/Gcam.Studio.Tests` | Verify the selector includes CsI, default remains GAGG, Reset allows choosing CsI for a new acquisition, and physical setters remain locked while running/while data exist. Review positional preset usages (e.g. `WaveformWorkspaceTests` index 3 previously meant BGO); use named selection when identity matters. |
| `tests/Gcam.Studio.RenderTests/WaveformRenderTests.cs` | Change selector count 4→5 and CsI absence to presence. This assertion is a current dependency of D-3. Do not run WPF render/UI tests under this turn's no-GUI/no-UI-test restriction. |
| Documentation after implementation | Planner must revise D-3 and TODO/results logs. Update the affected non-plan behavior docs: `VV.Studio.SRS` SR-CHAIN-03, `VV.Studio.SDS` detection-chain paragraph, and `VV.Studio.Waveform` availability/evidence rows. Record actual validation, not merely planned cases. These documents currently explicitly prohibit or reject CsI. |

Studio's existing selector binding and transport plumbing are sufficient; no XAML redesign is required. Selecting CsI before Start changes transport as well as the front-end response. Stored deposits must not be reinterpreted as a new transport material: the existing Reset/acquisition capture policy stays in force. Preserve GAGG as the first/default chain and the engine's documented unknown-material fallback; the explicit Studio mapping continues to reject unsupported presets before acquisition.

Recommended implementation validation, with no desktop/UI tests:

```powershell
dotnet build Gcam.sln -c Release
dotnet test tests/Gcam.Tests -c Release
dotnet test tests/Gcam.Studio.Tests -c Release
dotnet test tests/Gcam.Studio.Services.Tests -c Release
```

Run generated-data checks before these tests. No build/test was needed or run for this documentation-only review. No desktop, render, GUI or UI automation was run. Original worktree status was clean; the only authorized edit is this review file.

## APPROVAL REQUESTS

Preferred local generation requires this exact installation command (not executed):

```powershell
python -I -B -m pip --isolated install --index-url https://pypi.org/simple --no-cache-dir --only-binary=:all: --target "$env:TEMP\gcam-csi-data\packages" xraylib==4.3.0
```

- **Why:** obtain the same pinned xraylib version as theme 52; dependencies are installed into the same temporary target. `--only-binary` avoids building/installing compilers, `--isolated` ignores pip user configuration, and no package cache or global Python installation is updated. Record resolved dependency versions and wheel hashes in generation evidence.
- **Path prerequisite:** the planner must identify a usable Python interpreter and explicitly authorize reading its runtime/standard library and pip for this command and subsequent generator execution if they are outside the allowed roots. I have not resolved or inspected that installation. Approval of a package install alone does not waive the read-root limits. Do not proceed through a Store installer/launcher; if `python` cannot run this command directly, report it rather than installing Python.
- **What it destroys:** no intentional deletion or global replacement. It creates package files in `%TEMP%\gcam-csi-data\packages`; if that target already exists, inspect only that permitted target and stop on a collision rather than overwrite/upgrade it.
- **Undo:** global settings/PATH remain unchanged. Leaving the temporary target unused is sufficient; removing that target later is a separate deletion requiring the author's approval.

This request follows the author's HARD LIMITS 1–3 (restricted read roots and explicit approval before installation). Alternatively, the planner may supply the versioned generator output and checks described above; then no local installation is needed.
