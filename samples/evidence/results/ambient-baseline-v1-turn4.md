# TODO-30 baseline: turn 4 implementation and verification

Date: 2026-10-03. Status: **partial implementation; terrestrial spectrum and numerical evidence blocked**. No commit. This report describes the working tree, not a released physics model. Earlier prerequisite and turn-3 reports remain historical records.

## Specification blocker: AB-4b

The corrected nuclear cascade count bound can be applied conditionally to the evaluated Po-218 alpha feeding. The proposed transported air-kerma bound cannot be applied as written:

1. Feeding times excitation energy is energy per decay. Multiplication by air mass energy-absorption coefficient gives a response in Gy cm² per decay, not the required air kerma at 1 m in nGy/h per Bq/kg. A source-to-point transport response through soil and air is missing. Dividing this response by a chain's UNSCEAR coefficient would compare incompatible units.
2. Soil and air scattering can produce photons below the smallest nuclear level spacing. The maximum coefficient restricted to the emission interval therefore does not establish a conservative bound on the transported spectrum.
3. The evaluated At-218 adopted-level record explicitly reports no information from Po-218 beta decay. A record listing only the ground state does not prove absent excited-state feeding. The beta branch consequently lacks the feeding data needed for the correction's bound.

No omission has been certified, no feeding has been substituted for photon intensities, and the chain-kerma threshold remains 10^-3. A conservative transport-response bound covering scattered energies and evaluated beta feeding, or another explicitly approved treatment of that missing branch, is needed. This is a failed premise, not an oracle discrepancy that can be remedied by tuning.

### Evaluated sources and conditional Po-218 envelope

Sources accessed for this turn:

- Shaofei Zhu and E. A. McCutchan, *Nuclear Data Sheets* **175**, 1 (2021), A=214 evaluation, literature cutoff 2021-05-01: [Po-218 alpha decay, 3.097 min](https://www.nndc.bnl.gov/ensnds/214/Pb/a_decay_3.097_m.pdf) and [Pb-214 adopted levels and gammas](https://www.nndc.bnl.gov/ensnds/214/Pb/adopted.pdf).
- B. Singh, M. S. Basunia, M. Martin et al., *Nuclear Data Sheets* **160**, 405 (2019), A=218 evaluation, literature cutoff 2019-10-30: [At-218 adopted levels](https://www.nndc.bnl.gov/ensnds/218/At/adopted.pdf). This source states that information from Po-218 beta decay is unavailable.
- J. H. Hubbell and S. M. Seltzer, *Tables of X-Ray Mass Attenuation Coefficients and Mass Energy-Absorption Coefficients*, NISTIR 5632 (1995), [dry-air coefficient table](https://physics.nist.gov/PhysRefData/XrayMassCoef/ComTab/air.html).

The alpha-decay evaluation lists a 99.980(2)% alpha branch and relative excited-level alpha feeding of 0.0011% at 837(2) keV. Its adopted-level counterpart is the 835(1) keV Pb-214 level, with ground state below it. Thus the conditional maximum nuclear gamma multiplicity is one, not an arbitrary cascade multiplicity. Using nominal values:

| Quantity | Conditional nominal envelope | Method |
|---|---:|---|
| Absolute excited-level feeding | 1.0997800000000002e-5 / parent decay | 0.0011 / 100 × 0.99980 |
| Nuclear gamma count | <= 1.0997800000000002e-5 / parent decay | Feeding × one adopted level-to-ground step |
| Nuclear photon energy | <= 0.0092051586 keV / parent decay | Feeding × 837 keV |
| Maximum air mu_en/rho over 835–837 keV | 0.02863916503402434 cm²/g | Log-log interpolation of 800 keV: 0.02882 and 1000 keV: 0.02789 |
| Energy × coefficient | 0.00026362805630976844 keV cm²/g / parent decay | Product of preceding rows |
| Corresponding response | 4.223787118863473e-17 Gy cm² / parent decay | Product × 1.602176634e-13 J/g per keV/g |
| Incident fluence share of chain | **Unknown** | Transport response not established |
| Air-kerma share of chain | **Unknown** | Transport response not established |

These are nominal evaluated-input calculations, N=0 transport histories, **not confidence upper bounds or omission certification**. The weak feeding has no quoted uncertainty in this table; using the nominal excitation energy does not absorb its stated uncertainty. The count covers nuclear de-excitation photons, not atomic relaxation photons. For Po-218 beta decay to At-218, count and energy bounds are both **unknown**, because evaluated feeding is unavailable. Other missing-photon nuclides have not been certified: the full chain audit and generator stop at this unresolved prerequisite. No UNSCEAR ratio or MC uncertainty is available for K, U or Th.

## AB row disposition

| Row | Delivered / remaining |
|---|---|
| AB-1 | Independent absolute photon field, ICRP-74 fluence normalization including integrated continuum weights, incident Poisson process, detected Poisson flood counts, actual deposit-site pixels. Development spectrum only; validated terrestrial normalization pending. |
| AB-2 | Bare crystal ray-box entry on six faces and front-only cosine entry transported through the existing mask slab. Both implemented and tested. No realistic housing, side-wall transport, entrance or rear backing in the ambient bare-crystal bound. |
| AB-3 | Existing high-energy transport remains unchanged; no pair production and existing tungsten coefficient clamp remain. Actual terrestrial high-energy lines await the blocked generator. TODO-31 remains deferred. |
| AB-4 / 4a / 4b | Evaluated sources checked and conditional alpha envelope reported above. Soil/air generator, complete chain data, angular distribution, validated spectrum and UNSCEAR comparison **not delivered** because the premise fails. |
| AB-5 | Photon H*(10) input, named development preset, default zero/off, ideal-environment label, bound selector, separate editable legacy BSR, derived ambient BSR, acquisition locking. Draft SRS/SDS rows below; protected VV documents were not edited. |
| AB-6 | Source-free acquisitions and fixed-time APIs progress across empty intervals. Background-only Studio imaging and spectrum workspaces supported without fabricated isotope bands. |
| AB-7 | Gate calibration/selection/validation **not run**. N=0 background-only acquisitions for each bound. The required >=1000 acquisitions/configuration and independent selection/validation seeds remain unchanged. No trusted-location criterion is claimed. |
| AB-8 | Ambient-null binary-record/RNG golden tests, source+BSR replay, and existing Stop/Continue tests pass. New ambient acquisition Stop/Continue tests pass for both bounds. Intrinsic activity remains excluded. |
| AB-9 | Seed driver accepts a selected versioned manifest and refuses unvalidated ambient spectra before running the CLI; engine additionally checks the spectrum payload hash. Ambient study recipes, manifests and remeasurements remain pending; none use the development placeholder. |

The currently supported incident angular model is isotropic. Non-isotropic spectra are explicitly rejected, rather than silently losing the angular distribution that AB-4 must eventually produce. Consuming that distribution remains required once its validated representation is specified. Legacy weighted photon-budget study APIs explicitly reject non-null Ambient; migrating the requested studies to physical fixed exposure is still required.

## Files by group

Paths are repository-relative. Existing planner changes in Planning, Todo, PLAN and VV documents were retained, not authored in this turn.

- Configuration: `src/Gcam.Configuration/{SimulationConfig,AmbientFieldConfig,AmbientGeometry,IncidentSpectrum,IncidentLine,IncidentContinuumBin}.cs`.
- Physics and acquisition: `src/Gcam.Detector/ComptonCrystalDetector.cs`; `src/Gcam.Simulation/{AmbientPhotonProcess,AmbientAcquisition,ListModeSource,SimulationRunner,EventStreamStudy,DoseStudy}.cs`.
- CLI: `src/Gcam.Cli/Program.cs`, `src/Gcam.Cli/Commands/AmbientCommands.cs`.
- Studio Core: services `IAcquisitionService.cs`, `AcquisitionSnapshot.cs`, `SpectrumSettings.cs`; view models `MainViewModel.cs`, `ImagingWorkspaceViewModel.cs`, `SpectrumWorkspaceViewModel.cs`.
- Studio services/view: `AcquisitionSession.cs`, `SimulationService.cs`, `SpectrumService.cs`, `ImagingService.cs`; `src/Gcam.Studio/Views/MainWindow.xaml`.
- Engine/Core/services tests: `tests/Gcam.Tests/AmbientFieldTests.cs`, `tests/Gcam.Studio.Tests/AmbientViewModelTests.cs`, `tests/Gcam.Studio.Services.Tests/AmbientAcquisitionTests.cs`.
- Headless rendering: new `AmbientRenderTests.cs`; modified `PlotViewRenderTests.cs`, `MainWindowRenderTests.cs`, `DetectorFocusRenderTests.cs`, `WaveformRenderTests.cs` under `tests/Gcam.Studio.RenderTests/`. Optional render-output override directs artifacts to TEMP.
- Samples/evidence: `samples/ambient/development-mono662-v1-NOT-VALIDATED.json` and detached `.sha256`; `samples/evidence/run_seeds.py`; this report. Earlier prerequisite report has portable snapshot paths, URLs and hashes preserved.

## API and development-only reproduction

`SimulationConfig.Ambient = null` retains the legacy path. A non-null `AmbientFieldConfig` has dose rate in microSv/h, geometry, spectrum and `RequireValidatedSpectrum`. `IncidentSpectrum` carries version, provenance, angular-model name, validation flag, canonical payload hash, discrete fluence weights and non-overlapping continuum bins. Continuum bins represent uniform energy density within each bin, normalized against the exact integral of the existing ICRP-74 interpolation.

`ListModeSource.AdvanceUntil(horizonS)` processes at most one history at a time and advances elapsed time even when no event is detected. Call repeatedly until the horizon is reached. For a non-null Ambient, `Advance()` rejects the ambiguous photon-budget API. `SimulationRunner.RunFixedTime` and `EventStreamStudy.GenerateFixedTime` expose fixed-duration acquisition. `AmbientPhotonProcess.AcquireFlood` accumulates event counts directly. Separate streams isolate ambient time, entry, energy, mask and crystal draws from legacy source RNG. No deposited event is placed uniformly at random.

The Studio input uses the clearly labelled synthetic development preset while the physical spectrum is blocked. The selected input is frozen at acquisition start. Ambient BSR is estimated detected ambient rate divided by the physical source rate; source-free BSR is undefined. This is a readout, not an input or physics normalization. Ambient-only All-channel imaging/spectrum is supported; isotope-specific channels require actual source lines.

Reproduce the **development placeholder**, not a soil spectrum:

```powershell
dotnet run --project src/Gcam.Cli -c Release -- ambient-placeholder <output-spectrum.json>
dotnet run --project src/Gcam.Cli -c Release -- ambient-fixed <configured-scenario.json> <output.json>
```

The generated JSON's file SHA256 is `3600e22bf8ce4594adf3c362bddfa938c531a35c7318956c7817b4b5926710bd`. Its canonical spectrum-payload SHA256 is `76981e08086fe332b601778c6d0c47e914314eb145d7fce7bb8e6183b909538b`. File hash and payload hash have different purposes. The file declares `IsValidated=false` and synthetic 661.7 keV isotropic fluence weight 1. It has no terrestrial-mixture claim. Evidence requires both an accepted validated spectrum and a matching payload hash; the seed driver forces `RequireValidatedSpectrum=true`.

## Verification and numerical smoke checks

Build recipe: `dotnet build Gcam.sln -c Release`. Final incremental build: 0 errors, 0 emitted warnings, 1.14 s. The two pre-existing analyzer warnings observed when their test sources were recompiled remain: xUnit2012 in WaveformServiceTests and xUnit2000 in ImagingServiceTests. They have not been fixed or suppressed.

Test recipe: set process-local `GCAM_UI_TESTS=0`, `GCAM_RENDER_SNAPSHOTS=0`, `GCAM_EVIDENCE_TESTS=0`; run `dotnet test Gcam.sln -c Release --no-build --logger trx` with results under the TEMP work folder. The preceding Release build supplies the binaries. Final counts:

| Assembly | Before passed | After passed | Skipped |
|---|---:|---:|---:|
| Engine | 298 | 319 | 0 |
| Studio.Core | 179 | 184 | 0 |
| Studio services | 83 | 87 | 7 |
| UI project, headless cases only | 13 | 13 | 14 desktop cases |
| Render, normal opt-in disabled | 0 | 0 | 1 |

Separate headless-render recipe: `GCAM_UI_TESTS=0`, `GCAM_RENDER_SNAPSHOTS=1`, `GCAM_RENDER_OUTPUT=<TEMP work folder>/render`; `dotnet test tests/Gcam.Studio.RenderTests -c Release`. Result: 1 passed, 0 skipped, approximately 8 s. The fixture renders both themes at 1280x800 and 1440x900; ambient dose, bound, source-free readout and locking assertions pass. No desktop window is shown. PNGs remain TEMP-only.

Legacy golden records were captured from the pre-implementation engine with seed 12345, serializing every binary event field and final history/weight counters. Exact SHA256 oracles:

- BSR 0: `6C9B4AD52DA2F4F2C232EB5095603BD4C9AC315101C8D612BA2207A5C1B3ACF5`.
- BSR 1 and electronic dark rate 0.001 kcps: `D70F1457599566E172E7B38412D75B23C5640AF9F4867EE50B61FAF6958ECA92`.

New statistical oracles state their sample sizes and derive tolerances in the tests:

- Incident Poisson bins: N=10000, lambda=1; mean and variance tolerances are six standard errors using the Poisson moments.
- Parallel-beam interaction probability: N=20000, p=1-exp(-0.1/mm × 10 mm); six binomial standard errors.
- Per-pixel detected Poisson law: N=2000 time bins over 144 pixels; Var(sample variance minus sample mean)=2 lambda²/(N-1), with lambda bounded using the observed total-count six-standard-error envelope.
- Continuum dose closure: analytic integration of the existing piecewise power-law interpolation; independent midpoint quadrature N=10000, error bound derived from the interpolation's second derivative and quadrature step, plus machine-rounding allowance.
- Exact tests cover six-face deposit placement, both geometry interval partitions, source-activity independence, cancellation before draws, empty-field time progress, source-free dark counts, flood/event identity, invalid/hash-stale spectra, ambient-null replay and Stop/Continue.

A conservation check exposed a two-ulp accumulated cascade-energy excess in the new ambient event projection. The ambient projection now caps reported summed energy at incident energy; the legacy detector callback/RNG behavior is unchanged. The oracle tolerance was not loosened.

### Synthetic CLI smoke numbers — explicitly excluded from evidence

Recipe: clone `samples/scenario.json`; seed 12345; source activity 0 Bq with `Sources` unset; exposure 10 s; dose 0.10 microSv/h; synthetic isotropic 661.7 keV placeholder above; 12x12, 1 mm pixel pitch, 10 mm GAGG crystal; legacy BSR and dark rate zero. Invoke `ambient-fixed` separately for each bound. N=1 acquisition per bound:

| Bound | Detected counts | CLI compute time |
|---|---:|---:|
| BareCrystalAllFaces | 49 | 0.0269977 s |
| FrontOnlyThroughMask | 0 | 0.0153304 s |

These are plumbing checks, not environmental predictions, rate estimates or uncertainty-qualified Monte Carlo evidence. The acquisition's source distance is irrelevant because it contains no source. Config and output artifacts are relative to the TEMP work folder: `placeholder-bare.json`, `placeholder-front.json`, `placeholder-bare-result.json`, `placeholder-front-result.json`.

## Measurements still required

Soil transport: N=0 for each chain; no spectrum angular distribution, kerma ratio or MC uncertainty. PR-SENS-02: N=0 for each bound in selection and validation; >=1000 background-only acquisitions/configuration must still be measured with disjoint seeds. EV-07, EV-02, EV-12, EV-15, EV-01 and EV-09: N=0 ambient runs for each bound at every requested field level, 0 / 0.05 / 0.10 / 0.20 microSv/h. These were blocked by field validation, not silently reduced to fit a time budget. Synthetic timings above cannot estimate the complete gate/EV budget.

After the premise is resolved, complete the chain audit, cited soil/air properties, headless generator and versioned output. Validate the UNSCEAR K/U/Th coefficients 0.0417/0.462/0.604 nGy/h per Bq/kg with sample-derived MC uncertainty and declared model limitations, without tuning. Add the resulting angular sampler, fixed-time study recipes and versioned ambient manifests recording field, activity, distance, exposure, window, bound, seeds and spectrum hashes. Then run gate selection/validation, EV-07 first, and remaining requested evidence in order. High-energy corrections and realistic housing remain TODO-31/TODO-32.

## Proposed Studio requirement/design rows for the planner

The instruction prohibiting edits to VV.* takes precedence over directly adding SRS/SDS rows this turn. These are provisional rows, to be assigned stable IDs by the planner and inserted into the protected documents:

| Proposed SRS row | Requirement | Verification / design |
|---|---|---|
| Ambient input | A distinct photon H*(10) microSv/h input shall default to zero; zero shall display ideal environment, and nonzero shall identify the preset and validation state. | AmbientViewModelTests; ambient render fixture. MainViewModel dose and status properties; input bound in MainWindow. |
| Ambient geometry | The user shall select either bare all-face crystal or front-only mask entry before acquisition; the selection shall be frozen during acquisition. | Locking/view-model/service tests and headless render assertions; frozen AmbientFieldConfig clone. |
| BSR coexistence | Legacy BSR shall remain editable separately. Ambient BSR shall be a derived detected-rate/source-rate readout and undefined for source-free acquisitions. | Snapshot/readout tests; independent process counters, no feedback into field normalization. |
| Source-free timing | Positive ambient acquisitions shall be startable with no sources and shall progress for the declared live time through empty intervals; Stop/Continue shall retain event identity. | Both-bound service tests with a virtual clock; fixed-time ListModeSource API and session routing. |
| Background-only projections | Background-only retained data shall produce All-channel imaging and spectrum projections without invented source isotope bands. | AmbientAcquisitionTests; optional incident maximum energy in spectrum settings. |

## Author decisions needed / approvals

Resolve the AB-4b transport-bound premise and unavailable Po-218 beta feeding before spectrum certification. Confirm the representation and acceptance oracle for the generated non-isotropic angular distribution. The development preset remains explicitly unvalidated and default-off until those prerequisites pass. Planner insertion of SRS/SDS rows and evidence documentation remains pending.

APPROVAL REQUESTS: none. No destructive commands, installs, process stops, desktop/UI automation, commits, protected documentation edits or network uploads were performed.
