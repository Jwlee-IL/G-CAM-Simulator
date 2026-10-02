# TODO-30 baseline — turn 5

2026-10-03. **Partial implementation; source audit stopped on the unresolved Bi-214 beta photon component.** AB-4c itself has no new premise objection. It is used as a source-term modelling approximation, not a transported-dose bound. No threshold, statistical oracle or sample requirement was relaxed. No commit.

## Source-term audit and stop

Reproduce:

```powershell
python samples/ambient/audit_omissions.py --out <audit-output.json>
```

The default input is the repository's immutable public evaluated snapshot, `samples/ambient/source-data/v1/214bi.csv`. The script verifies its SHA256 before calculating anything. It emits the audit even when blocked and returns native exit code 2. The committed result is `ambient-baseline-v1-turn5-omissions.json` in this directory. All paths in it are portable.

The conservative evaluated U-chain photon-energy denominator uses only the known-intensity, ground-state Bi-214 beta radiation rows. Their evaluated energy sum is **1477.656716084652 keV per Bi-214 decay**. Multiplication by the main-path Po-218 alpha occurrence, 0.99980, gives **1477.3611847414352 keV per U-chain decay**. Other U-chain photons are deliberately excluded from the denominator: this is a lower bound from a subset, not a claim that the full source inventory has been completed. Using a smaller denominator is conservative for a passing omission check. Intensities are absolute percent per parent decay; they are not multiplied a second time by the Bi beta fraction. For example, the 609-keV row agrees with the ENSDF beta-branch normalization times 0.999790.

The snapshot contains 272 ground-state beta rows with specified intensities. Missing-intensity rows were not assigned zero. Snapshot SHA256: `8116f807ec12b5bdd8612af4fda49e9a148520a99c9757adcea3298730036adf`.

| Branch eligible for nominal source approximation | Energy envelope, keV/chain decay | Ratio upper bound using the subset denominator | Nuclear photon count envelope |
|---|---:|---:|---:|
| Po-218 alpha | 0.0092051586 | 6.230811188945017e-6 | 1.0997800000000002e-5/chain decay |
| Po-218 beta | 0.051800000000000006 | 3.506251587966685e-5 | Not bounded without a photon-energy cutoff/level data |
| Bi-214 alpha | 0.011114021751 | 7.522887338444021e-6 | The adopted-level step bound is recorded in the JSON |
| Sum of the above | 0.07211918035100001 | 4.8816214407055886e-5 | Not a total atomic-photon count bound |

Thus both Po branches together are below **4.129332706861187e-5**, and the three listed omissions together are below **1e-4**. The bounds use the evaluated nominal parameters, conditional on those model inputs; they are not statistical confidence bounds on nuclear-data uncertainties. The weak Po alpha feeding has no quoted uncertainty in its table. The Po beta data now supply Q_beta=259(12) keV and beta fraction 0.020(2)%. Feeding and excitation energies are bound-only inputs and were never substituted for line intensities. No transported kerma/fluence share is inferred from these numbers.

**Stop: Bi-214 beta unresolved photon component.** The evaluated record has inferred 36.8, 61.0, 71.1 and 104.4 keV transitions with no photon intensities. It explicitly identifies selected feeding values as lower limits or as excluding unknown transition contributions. Such feedings cannot establish the upper-bound population needed for omission certification. Reserving the full beta energy as a conservative unknown-component envelope gives

`0.99980 * 0.999790 * 3269 = 3267.659847298 keV/chain decay`,

or **2.211821916703395** relative to the conservative denominator. This envelope does not pass the 1e-4 criterion. It is an intentionally conservative envelope, **not an estimate that the missing photons actually carry that much energy**, and not proof that a tighter source could not pass. No whole beta branch or known line was dropped. A tighter evaluated photon-yield bound or an explicitly revised source approximation is required. “Not observed” without an intensity limit is not numerical zero.

The full K/U/Th audit remains incomplete. The Pa-234/Th-234 inspection also found that the cached 2006 LiveChart dataset and the current 2026 ENSDF evaluation differ: the old unknown-shift Pa isomer assignment is now tentatively placed at 76.5 keV, with an inferred unobserved 2.6-keV transition. The snapshot must not be used as if Pa ground and metastable parents each had one decay per chain. Those source-normalization and remaining incomplete-intensity records have not been certified or used to generate a field. This audit stops at the failed Bi prerequisite; it is not a completed chain source file.

### Full references

- B. Singh, M. S. Basunia, M. Martin et al., *Nuclear Data Sheets* **160**, 405 (2019), cutoff 2019-10-30: [Po-218 adopted levels](https://www.nndc.bnl.gov/ensnds/218/Po/adopted.pdf), including beta Q value and branching fraction.
- S. Zhu and E. A. McCutchan, *Nuclear Data Sheets* **175**, 1 (2021), cutoff 2021-05-01: [Po-218 alpha decay to Pb-214](https://www.nndc.bnl.gov/ensnds/214/Pb/a_decay_3.097_m.pdf), [Pb-214 adopted levels](https://www.nndc.bnl.gov/ensnds/214/Pb/adopted.pdf), and [Bi-214 beta decay to Po-214](https://www.nndc.bnl.gov/ensnds/214/Po/beta_decay.pdf). Relevant beta gaps are on printed pages 3–5; their feeding qualifications preclude treating the quoted feeding as an upper bound.
- M. Shamsuzzoha Basunia, *Nuclear Data Sheets* **121**, 561 (2014), cutoff 2014-03-31: [Bi-214 alpha decay to Tl-210](https://www.nndc.bnl.gov/ensnds/210/Tl/a_decay_19.9_m.pdf), [Tl-210 adopted levels](https://www.nndc.bnl.gov/ensnds/210/Tl/adopted.pdf).
- S. Ota, *Nuclear Data Sheets* **207**, 351 (2026), cutoff 2023-12-01: [Pa-234 adopted levels](https://www.nndc.bnl.gov/ensnds/234/Pa/adopted.pdf), [Th-234 beta decay](https://www.nndc.bnl.gov/ensnds/234/Pa/beta_decay.pdf). This is an audit warning about older cached data, not an implemented replacement source model.
- IAEA Nuclear Data Section, [LiveChart API documentation](https://nds.iaea.org/relnsd/vcharthtml/api_v0_guide.html), ENSDF decay-radiation extraction: [Bi-214 gamma request](https://nds.iaea.org/relnsd/v1/data?fields=decay_rads&nuclides=214bi&rad_types=g). Snapshot extraction date 2026-10-02. The API documentation distinguishes parent states and decay modes and identifies blank fields as unavailable/unknown.

## Independent implementation delivered

The angular-distribution addendum can be implemented independently of a complete nuclear source catalog. `IncidentSpectrum.AngularModel="EnergyZenithTable"` now carries `EnergyZenith[]` rows with `EnergyIndex`, `LowCosine`, `HighCosine` and integrated scalar `FluenceWeight`. Indices address Lines followed by Continuum. Line energies stay discrete; continuum density is uniform within each rectangle. Angular row sums must reproduce each existing energy marginal. Overlapping intervals, invalid indices/cosines/weights and stale hashes are rejected. Azimuth is uniform.

**Coordinate convention:** mu is the photon propagation direction dotted with world +y, the displayed vertical axis. The optical axis remains +z. Uncollided ground photons have mu>0. This establishes a horizontal camera with the ground normal along +y; other camera tilts are not implemented. The convention is stated next to the public data type and sampler. It must also be stated by the eventual generator.

For an anisotropic field, scalar fluence is not sampled directly as incoming current. The sampler integrates the projected area in each angular bin, selects energy/angle bins by current weight, then samples the conditional projected-area distribution and the appropriate entry face. If `A_z=width*height`, `A_x=height*depth`, `A_y=width*depth`, azimuth-averaged projected areas are

`bare: A_y*abs(mu) + (A_x+A_z)*(2/pi)*sqrt(1-mu²)`,

`front only: A_z/pi*sqrt(1-mu²)`.

The front case includes only directions entering z=0 and back-projects the ray outside the mask slab. Bare rays enter the selected crystal face. Direction-specific acceptance changes sampling efficiency, not the physical rate. Bin-restricted rejection envelopes keep near-zenith bins efficient. Actual deposit-site scoring remains the existing engine sink. H*(10) conversion continues to use the scalar energy marginal and the existing ICRP table.

The new angular field is integrated into the existing incident Poisson process and fixed-time API. It has its own existing ambient RNG streams. The isotropic path's sampling code is retained. Null angular tables are omitted from JSON and keep the previous canonical hash payload; the old development placeholder reproduces byte-for-byte with file SHA256 `3600e22bf8ce4594adf3c362bddfa938c531a35c7318956c7817b4b5926710bd`.

### Independent uncollided kernel

`HalfSpaceUncollided` provides the infinite uniform half-space, monoenergetic **uncollided** reference and an MC estimator. With soil source strength q photons/(cm³ s), linear attenuation mu_s and air mu_a at height h:

`dPhi/dmu = q/(2*mu_s) * exp(-mu_a*h/mu), 0<mu<=1`.

For one Bq/kg, q=`density_g_per_cm3/1000 * photonYieldPerDecay`. Integration gives the Beck-type exponential-integral form. Deterministic adaptive integration is independent of the MC analog air-survival draws; the soil source integral is importance-collapsed analytically. Numerical angular-integration error control is 1e-13 absolute integral error, separate from the MC oracle. Nonconvergence throws rather than accepting a looser result.

Reference: H. L. Beck, J. DeCampo and C. Gogolak, *In Situ Ge(Li) and NaI(Tl) Gamma-Ray Spectrometry*, USAEC Health and Safety Laboratory Report HASL-258 (September 1972), uniform-source equations (4)–(5). Accessible as Appendix I of the DOE-hosted [compendium](https://lmpublicsearch.lm.doe.gov/sitedocs/sw-a-000582.pdf), PDF pages 66–68. This kernel accepts caller-supplied coefficients; it does not invent cited soil/air material properties.

Headless reproduction:

```powershell
dotnet src/Gcam.Cli/bin/Release/net9.0/Gcam.Cli.dll ambient-uncollided samples/ambient/uncollided-kernel-check-NOT-VALIDATED.json <output.json>
```

The command checks total and angular-bin fluence against analytic values, writes the recipe, raw counts, ratio and MC uncertainty, and returns nonzero on failure. It is explicitly **not** the collided terrestrial-spectrum generator and cannot support ambient evidence. The committed kernel result is `ambient-baseline-v1-turn5-uncollided-check.json`.

## Numerical kernel check — synthetic, not environmental evidence

Recipe: photon yield 0.3/decay; synthetic density 1.7 g/cm³; synthetic soil mu 0.12/cm; synthetic air mu 0.003/cm; height 100 cm; seed 7283; **one outer seed, 200000 incident proposals**, 16 equal upward-zenith bins. These coefficients are mathematical fixtures, not a claimed published soil/air composition.

- MC scalar fluence: **0.000996040625 photons/(cm² s) per Bq/kg**, standard error **2.371175893886543e-6**.
- Analytic scalar fluence: **0.0009968698535052986** in the same units.
- MC/analytic: **0.9991681677378619**, standard error **0.002378621327095774**.
- CLI compute time: **0.0080256 s**.
- All 16 bin checks passed the predeclared six-standard-error rule; their raw counts and both fluences are in the JSON. Per-bin SE uses p_i(1-p_i)/200000 over all proposals, not an error estimated from the random survivor count. The total uses the same binomial law.

This quantity is upstream scalar fluence and has no detector housing bound. It does not constitute either a bare-crystal or front-only rate prediction. **No environmental acquisition numbers are quoted for either bound this turn.** No UNSCEAR ratio or MC uncertainty is available for K/U/Th: real source/soil/air transport histories N=0 for each chain. The synthetic check is not substituted for that comparison.

## Verification

`dotnet build Gcam.sln -c Release`: **0 errors, 2 warnings**, both the existing xUnit analyzer warnings (WaveformServiceTests xUnit2012; ImagingServiceTests xUnit2000), approximately 4.20 s.

`dotnet test Gcam.sln -c Release --no-build`, after that build, with process-local `GCAM_UI_TESTS=0`, `GCAM_RENDER_SNAPSHOTS=0`, `GCAM_EVIDENCE_TESTS=0`, TRX output in the TEMP work folder:

| Suite | Turn-2 baseline passed | Turn-4 passed | Turn-5 passed | Turn-5 skipped |
|---|---:|---:|---:|---:|
| Engine | 298 | 319 | 330 | 0 |
| Studio.Core | 179 | 184 | 184 | 0 |
| Studio services | 83 | 87 | 87 | 7 |
| UI project headless cases | 13 | 13 | 13 | 14 desktop |
| Render opt-in disabled | 0 | 0 | 0 | 1 |

All passed; engine approximately 19 s and services approximately 14 s. No view changed this turn, so no additional headless render was needed. No desktop/UI automation ran. The previous render result remains historical.

Eleven new engine cases: seven angular cases and four uncollided cases. The angular MC tests use N=20000 rays per geometry, seed 2831 for angle and 2832 for continuum energy. Energy marginal and conditional zenith-CDF comparisons use six binomial SE derived from N, expected p and the actually selected conditional sample count. Exact partitioned-time tests cover both bounds. Isotropic projected-area and normalization tests use analytic identities with floating-point allowances only. Rejection tests cover inconsistent marginals, overlapping bins and hashed-table mutation.

The uncollided tests use N=200000 proposals per synthetic air coefficient (0.0001, 0.003, 0.03 per cm), 16 zenith bins, seed 7283, and six sample-derived binomial SE. The zero-air case uses N=1000 and exact conservation/mass-unit identities. The existing ambient-null binary record hashes and legacy Stop/Continue tests also pass in the full suite. No golden expectation or tolerance was changed.

## Files by group, this turn

- Configuration: new `src/Gcam.Configuration/IncidentAngularBin.cs`; modified `IncidentSpectrum.cs` for hashed optional joint angular table while preserving isotropic serialization.
- Engine: new `src/Gcam.Simulation/AmbientAngularSampler.cs`, `HalfSpaceUncollided.cs`, `HalfSpaceUncollidedResult.cs`; modified `AmbientPhotonProcess.cs` to use current-weighted angular sampling.
- CLI: modified `src/Gcam.Cli/Program.cs` and `Commands/AmbientCommands.cs` for the independent uncollided check.
- Tests: new `tests/Gcam.Tests/AmbientAngularSamplerTests.cs`, `HalfSpaceUncollidedTests.cs`.
- Public data/reproduction: new `samples/ambient/audit_omissions.py`, `source-data/v1/214bi.csv`, `uncollided-kernel-check-NOT-VALIDATED.json`.
- Results: this report, `ambient-baseline-v1-turn5-omissions.json`, `ambient-baseline-v1-turn5-uncollided-check.json`.

The cached evaluated snapshot is committed as immutable reproducibility input because an online API's extraction date can change its bytes. It is not a validated complete source catalog. Source URLs and hash are in the audit; machine paths are excluded. Earlier turn files and existing planner document changes were retained.

## Every AB row and remaining work

| Row | Status |
|---|---|
| AB-1 | Absolute Poisson field and actual deposit placement from turn 4 retained; joint angular sampling now integrated. No validated terrestrial preset. |
| AB-2 | Both bare all-face and mask-only front bounds retained and tested; angular current weighting works for each. |
| AB-3 | Deferred high-energy physics unchanged. No synthetic extension or hidden correction to transport coefficients. |
| AB-4c | Nominal source omission checks pass for the two Po branches and Bi alpha; Bi beta unknown component stops source certification. No new objection to the criterion itself. |
| AB-4 angular addendum | Joint table, sampler and independent uncollided kernel delivered. Real line/zenith acceptance and total UNSCEAR comparison remain pending. |
| AB-4 generator | Complete evaluated chain source, cited soil/air inputs, collided continuum, versioned validated spectrum and all three UNSCEAR comparisons **not delivered**. Source prerequisites are blocked; the uncollided tool does not claim completion. |
| AB-5 | Turn-4 Studio controls retained, default zero/off, development preset labelled not validated. Protected SRS/SDS edits remain for the planner; prior draft rows are in the turn-4 report. |
| AB-6 | Background-only fixed-time acquisition retained; angular Stop/Continue partition tests added for both bounds. |
| AB-7 | No gate study: selection N=0 and validation N=0 per configuration for both bounds. Required >=1000 null acquisitions and independent selection/validation seeds unchanged. |
| AB-8 | Ambient-null exact record/RNG oracles and Stop/Continue continue to pass; placeholder byte hash unchanged. |
| AB-9 | Requested EV-07, EV-02, EV-12, EV-15, EV-01/EV-09 ambient runs N=0 for both bounds at 0/0.05/0.10/0.20 microSv/h. Existing seed-driver rejection of unvalidated spectra retained; validated ambient manifests and fixed-time study migrations remain pending. |

The next required action is to establish a sufficiently tight evaluated bound for the unresolved beta component, or obtain an explicit source-model decision covering it. Then finish the full chain audit and collided soil/air generator; attach every approved omission to its versioned spectrum; validate the real uncollided angular table and UNSCEAR kerma without tuning. Only then run gate selection/validation and EV-07 first, followed by the remaining requested families with both geometry bounds and declared recipes. Current kernel runtime cannot be extrapolated into a collided-transport or gate-study budget. No sample size was silently reduced to fit this turn.

APPROVAL REQUESTS: none. No destructive command, installation, upload, process stop, desktop launch, commit or protected VV/README/PAPER/Findings/PLAN/Todo edit was performed.
