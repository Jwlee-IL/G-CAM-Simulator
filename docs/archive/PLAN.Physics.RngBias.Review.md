# PLAN.Physics.RngBias.Review — no published number moves; the legacy generator's real defect is seed linearity

Scope: TODO-26 survey and measurement (S-1 … S-5 of [PLAN.Physics.RngBias](PLAN.Physics.RngBias.md)), substitute
implementer, 2026-10-02, worktree `C:\gw\w26` at fb4c630. No repository change is meant to be kept: the only code
change is a temporary, environment-switched `DefaultRandom` (diff in §6) used to re-run the published numbers; it is
to be discarded. Probes, scripts and every output are under `%TEMP%\gcam-rngbias\` (§7).

## 1. Result and recommendation

1. **The Findings-60 probe bias does not transfer to the engine's transport.** Direct tests of every exposed
   stream's quantity (§3) find no generator effect on the transport outputs at 0.01–0.1 % precision: the 4π
   isotropic efficiency (the probe's closest analogue, 10⁹ photons × 4 seeds) is −0.04 ± 0.10 % legacy against
   +0.02 ± 0.10 % xoshiro256**; crystal stopping, photopeak fraction, mean deposit, Poisson means and the Co-60
   decay sampler show none either. Two legacy defects are measurable but tiny: the Klein–Nishina rejection sampler
   (mean ε −0.010 %, z −5.6; backscatter fraction −0.033 %, z −4.2) and lag correlations of 0.15–0.55 % between
   successive Knuth-Poisson samples (P2b); xoshiro is clean on both.
2. **No published number moves beyond its seed noise** (§4). All 43 published CLI runs (every EV-xx with an MC
   reproduce command, the README headlines, theme 44) were run with both generators at 5 seeds each (the published
   seed 12345 + 4); 828 varying output numbers, 12 flagged at |z| ≥ 3 against ~14 expected by chance; the flagged
   ones were re-run at 20 seeds and all fell to |z| < 3 except **one intermediate, unpublished value: the shield
   study's calibrated RMS at its background knee** (Co-60, 8 mm W: median 0.79 → 1.02 mm, Mann–Whitney p = 1·10⁻⁴;
   EV-25's thickness picks do not move). Studio evidence tests: 7 / 7 pass under both, numbers unmoved (§4.3).
3. **The real defect is structural: the legacy seeded generator is affine in its seed.** Two legacy streams whose
   seeds differ by d are the same stream shifted mod 1 by a draw-dependent constant (correlations up to +0.99
   between draw j of seed s and of seed s + d, the same for every s; §3, P7). Within one long run this averages out
   (P8: |corr| ≤ 0.001), so run-level results are unaffected — but a consumer that uses **only the first few draws
   of many nearby seeds** gets a lattice instead of random numbers. The engine has exactly one:
   **`MeasurementStage` (Studio) seeds a fresh generator per event** (`seed + index·104729`, two draws). Consecutive
   events' energy smears are correlated −0.807 (z ≈ −3·10⁵), and the energy-window count fluctuation from the smear
   is **5 % (1 000 events) and 1.4 % (10 000 events) of the binomial value** — Studio spectra and window counts
   are smoother than physical. Marginals are correct (mean, variance, tails to 4σ).
4. **Tests (§5):** under xoshiro, 3 engine tests and 1 Studio-service test fail. None is a physics tolerance
   breaking: the three engine ones are single-seed regression pins whose statistic fails at a fraction of seeds
   under **either** generator (thermal 4/32 vs 4/32, background knee 6/32 vs 10/32, mask fabrication 2/8 vs 4/8;
   no generator difference established); the Studio one is a test defect
   (histogram index unguarded above 761 keV).
5. **Also found:** the studies' Poisson realisations restart the mean-map transport stream with the same seed
   (§2 note 0); several published single-seed values sit at the edge of their own seed spread under both
   generators (EV-02 4.0°, EV-03 0.90 gate, EV-25 Co-60 30 mm, theme 44 slope 2.16 ± 0.4); and a few published
   numbers differ from today's legacy seed-12345 output independently of the generator (end of §4.2).
6. **Recommendation:** replace the algorithm (§6: xoshiro256** behind `IRandom`, SplitMix64 seeding, 53-bit
   doubles, same API) — for the reseeding defect, not for a transport bias. Give `MeasurementStage` a 64-bit
   (seed, index) key (P6b: z −0.5 against +5.5 for the plain int-wrap key under xoshiro), and the studies'
   realisation streams their own seed offset. Re-pin the four tests on their measured distributions, not on a lucky
   seed. Present it as "no published number moved; Studio's per-event smear was a lattice; the shield study's knee
   RMS moved (figure only)", not as a corrected transport bias.

## 2. S-1 — every random stream

Classification: **exposed** = the stream's own values decide how many draws a trial takes (rejection loop, branch-
dependent draw), so which draw feeds a quantity depends on earlier draws; **fixed-draw** = every draw of the stream
serves the same purpose in sequence (how many trials there are may depend on other streams); **reseed** = a fresh
generator per item. Line numbers are at fb4c630.

| Stream (seed) | Created at | Consumers (draws per trial) | Class | Measured (§3) |
|---|---|---|---|---|
| Transport (config seed) | `DefaultSimulationFactory.cs:13` → `SimulationRunner.cs:34`, `ListModeSource.cs:65`, `EventStreamStudy.cs:41`, `DoseStudy.cs:85`, studies' `_factory.CreateRandom` | `DetectorBiasedSource.cs:36-37` (2); `MixedFieldSource.cs:58,67-68` (3); `IsotropicSource.cs:23` / `DefaultRandom.NextOnUnitSphere` (2); `CodedApertureMask.cs:166` (1 draw **only when the ray crosses tungsten**) | exposed (branch) | P1a, P1b: none |
| Per-decay emission (TODO-14, transport stream; HEAD 0d22aed) | `ListModeSource.cs:92,95` (`DecayEmitter` when a source has a cascade scheme) | `DecayEmitter.Next` (`ListModeSource.cs:282-321`): emitter pick (1), `DecayScheme.SampleEnergies` (`DecayScheme.cs:119-135`, one branch draw per line), aimed-photon pick (1 if n > 1) + `Aim` (2) or `NextOnUnitSphere` (2), `Directions` (`:142-193`: Co-60 partner by inverse CDF `Co60Cosine` + azimuth, 2 per partner; Na-22 pair / 1275 keV, 0–2) | exposed (branch: the number of photons, hence of direction draws, depends on the branch draws) | not measured separately; TODO-14's analog-vs-biased check (`CascadeEmissionTests`) passes under the new generator, sum fractions re-measured (implementation report) |
| Poisson realisations (config seed, or `+999` `ComptonStudy.cs:88`, `+3210` `BackgroundStudy.cs:156`, per-point seeds `FieldOfViewStudy.cs:123`) | `NoiseStudy.cs:46`, `ArrayStudy.cs:61`, `AlignmentStudy.cs:69`, `DepthStudy.cs:66/97/125`, `DetectorDefectStudy.cs:89`, `MaskFabricationStudy.cs:93`, `MaskGeometryStudy.cs:65`, `ThicknessStudy.cs:78`, `UniformityStudy.cs:59`, `ThermalDriftStudy.cs:127`, `ShieldStudy.cs:47`, `MaskAntimaskStudy.cs:51` | `Sampling.Poisson` (`Sampling.cs:22-33`): Knuth product loop for λ < 30 (k + 1 draws), Box–Muller for λ ≥ 30 (2) | exposed (loop) | P2: none |
| Crystal cascade (`+777`; `+555` background) | `ComptonFactory.cs:39`, `ListModeSource.cs:72`, `ListModeBackground.cs:29`, `EventStreamStudy.cs:51,203`, `DoseStudy.cs:100` | `ComptonCrystalDetector.cs:135,147,165,169,181,185,216`; Klein–Nishina rejection loop `ComptonModel.cs:30-41` (3 per try) + azimuth `:52`; `EntranceAbsorber.cs:46-48` | exposed (loop, variable number of interactions) | P3: KN −0.010 % (legacy); P4: none |
| Front-end smear (`+888`; dose `+31`) | `ComptonFactory.cs:43`, `DoseStudy.cs:89` | `FrontEndModel.Measure` → `Sampling.Gaussian` (2 per call) | fixed-draw | — |
| List-mode rejection / time / background / dark (`+8181`, `+4242`, `+909`, `+1717`) | `ListModeSource.cs:66,67,44,45` | 1 per detected history / accepted event / gap | fixed-draw | — |
| List-mode background entry / pixel (`+556`, `+557`) | `ListModeBackground.cs:24,25` | 4 per history / 2 per placement | fixed-draw | — |
| Event-stream time / background / dark / entry (`+4242`, `+909`, `+1717`, `+556`) | `EventStreamStudy.cs:72,108,115,212` | resample (1 + one per element) then 1 per gap; 2 per background event; 1; 2 or 4 per try | fixed-draw | — |
| Single-stream studies | `CascadeSummingStudy.cs:69`, `NonProportionalityStudy.cs:54`, `MaskScatterStudy.cs:65`, `MaskSecondaryStudy.cs:46`, `DoiParallaxStudy.cs:50` | emission + branch draws + KN loop (`DecayScheme.cs:68-85`, `MaskSecondary.cs:49-60`); DOI: interaction draw only if `interactProb > 0` (`:71-72`) | exposed | P5 (decay sampler): none |
| Fixed-count studies | `MlemStudy.cs:102` (3 per photon), `FiniteSourceStudy.cs:76,125` (3 + 2) | — | fixed-draw | — |
| Waveform noise / amplitude, window raster, Studio waveform rate | `Waveform.cs:200,224`, `WindowRasterizer.cs:37`, `WaveformService.cs:39` | Gaussian (2) per sample / event; 1 per event | fixed-draw | — |
| **Per-event Studio measurement** (`909 + index·104729`) | `MeasurementStage.cs:44` | a new generator per event, 2 draws (Gaussian smear) | **reseed** | **P6: lag-1 −0.807; P6c: window variance 1.4–50 % of binomial** |
| Direct `System.Random` (not `IRandom`) | `CrystalUniformity.cs:38`, `DetectorDefects.cs:42`, `MaskFabrication.cs:41`; tests `CrrcContractTests.cs:99`, `MinMaxPyramidTests.cs:19,54` | fixed per-element pattern generation (`DetectorDefects`: hot drawn only if not dead) | fixed-draw, legacy algorithm | P9: no detectable cross-seed overlap (low power) |
| Unseeded | `DefaultRandom(null)` (`DefaultRandom.cs:9`); `config.Seed + 777/888/999/4242` stay null when `Seed` is null (`ComptonFactory.cs:39,43`, `ComptonStudy.cs:88`, `EventStreamStudy.cs:51,72`); `MainViewModel.cs:394` picks a seed from `Random.Shared` | `new Random()` = .NET's xoshiro-based unseeded generator | not reproducible | — |

Notes. (0) **Stream reuse:** studies that build a mean map with `SimulationRunner.Run(cfg)` and then call
`_factory.CreateRandom(cfg)` for the Poisson realisations (`ShieldStudy.cs:41,47`, `NoiseStudy`, `ArrayStudy`,
`AlignmentStudy`, `MaskFabricationStudy`, `ThermalDriftStudy`, `DetectorDefectStudy`, `MaskGeometryStudy`,
`ThicknessStudy`, `UniformityStudy`, `DepthStudy`, `MaskAntimaskStudy`) restart **the same stream with the same
seed**, so the realisation noise re-uses the uniforms that produced the mean map. Generator-independent and small at
these photon counts, but a separate-stream fix (`seed + offset`) belongs with the replacement. (a) Stream offsets (`+777`, `+888`, …) under legacy give streams that are exact shifts of the transport
stream (P7) — harmless within a run (P8) but not independent in the sense the code comments assume. (b)
`samples/isotopes/*.json` have no `seed`, so runs from them are unseeded and not reproducible.

## 3. S-2 — direct bias tests

Probe `%TEMP%\gcam-rngbias\probe\Program.cs` (net9.0, references the engine projects in `C:\gw\w26`; two private
`IRandom` wrappers: `LegacyRng` = `new Random(seed)`, `XoshiroRng` = xoshiro256** seeded by SplitMix64 from the
sign-extended seed). σ is the batch-means standard error (100 batches per seed); "combined" is the inverse-variance
mean over seeds; z is against the stated reference.

| Test | Quantity, N | Reference | Legacy (combined) | xoshiro (combined) |
|---|---|---|---|---|
| P1a | reference lab geometry, biased efficiency (source 2 draws + mask 0/1), 2·10⁷ × 12 seeds | legacy − xoshiro | 2.490307e-4 ± 1.4e-8 | 2.490464e-4 ± 1.5e-8; difference −0.006 % (z −0.8) |
| P1b | reference lab geometry, **4π isotropic** efficiency (EV-07's 4π reference), 10⁹ × 4 seeds | biased value 2.490384e-4 ± 1.0e-8 | −0.040 % ± 0.10 % (z −0.4) | +0.017 % ± 0.10 % (z +0.2) |
| P2 | `Sampling.Poisson` mean, λ = 0.3 / 2 / 8 / 25 (Knuth) / 50 (Gaussian), 2·10⁷ × 4 | λ | z +0.5 / +1.2 / +0.3 / +0.1 / 0.0 | z +2.6 / +1.7 / +0.9 / +0.5 / +1.0 |
| P2b | Knuth Poisson, successive samples: var/λ; autocorrelation lags 1–40, λ = 2 / 8 / 10 / 20, 10⁷ × 3 | 1; 0 (σ 3e-4) | var/λ 0.999–1.001; **lags with \|corr\| 0.15–0.55 % (z 5–18)**: λ 2 k16; λ 8 k5, k7, k8; λ 10 k4, k6; λ 20 k2, k3 | var/λ 0.999–1.001; max \|corr\| ≤ 0.10 % |
| P3 | KN sampler at 662 keV, mean ε, 10⁸ × 4 | quadrature 0.6184407 | **−0.010 % (z −5.6)** | +0.003 % (z +1.5) |
| P3 | KN mean cos θ | 0.3186999 | +0.006 % (z +0.6) | +0.014 % (z +1.6) |
| P3 | KN backscatter fraction | 0.2914936 | **−0.033 % (z −4.2)** | −0.010 % (z −1.3) |
| P4 | `ComptonCrystalDetector`, GAGG:Mg 15 mm, pencil beam: P(interaction) 662 / 1332 keV, 2·10⁷ × 4 | 1 − e^(−μt) | 0.000 % (z 0.0) / +0.004 % (z +0.3) | +0.008 % (z +0.8) / −0.006 % (z −0.4) |
| P4 | photopeak fraction 662 / 1332 keV | legacy − xoshiro | | −0.006 % (z −0.2) / +0.024 % (z +0.5) |
| P4 | mean deposit 662 / 1332 keV | legacy − xoshiro | | −0.006 % (z −0.3) / +0.035 % (z +1.5) |
| P5 | Co-60 `DecayScheme` + isotropic hit on a 12 mm square at 100 mm (rare hit after a variable-draw decay), 10⁸ decays × 4 | Ω/4π = 1.141808e-3 | +0.14 % ± 0.11 % (z +1.3) | +0.12 % ± 0.11 % (z +1.1) |
| P6 | per-event reseeded Gaussian (`seed + i·104729`, MeasurementStage), 2·10⁷ × 8; mean / E[x²] / P(\|x\|>2) | 0 / 1 / 0.0455 | all within z 3.8 (bases 909, 12345) | all within z 1.4 |
| P6 | same, **lag-1 E[xᵢxᵢ₋₁]** | 0 | **−0.8072 (both bases)** | −4e-5 ± 8e-5 (base 909); **+3.9e-4 ± 0.7e-4 (base 12345, z +5.5)** |
| P6b | xoshiro, key = 64-bit `(seed << 32) \| index` through SplitMix64 | 0 | — | −9e-5 ± 8e-5 (z −1.1); −3.5e-5 ± 7.7e-5 (z −0.5) |
| P6c | in-window (\|x\| < 1.5) count variance ÷ binomial, blocks of 100 / 1 000 / 10 000 consecutive events | 1 | **0.50 / 0.048 / 0.014** | 1.02 / 0.99 / 1.00 |
| P6d | tails, 2·10⁶ events: P(x > 3.5) / P(x > 4) | 2.33e-4 / 3.17e-5 | 2.32e-4 / 2.95e-5 | 2.25e-4 / 2.95e-5 |
| P7 | corr(u_j(s), u_j(s+d)) over 20 000 base seeds, d = 1 … 1 000 003, j = 0 … 10 000 (σ ≈ 0.007) | 0 | **−0.50 … +0.99** (e.g. d = 777: +0.58, +0.92 at j = 0, 1; d = 10: +0.99 at j = 54) | ≤ 0.018 |
| P8 | within one run, corr over j of u_j(s) and u_(j+k)(s+d), k = −2 … 2, 10⁷ draws, d = streams the engine uses | 0 (σ 3e-4) | ≤ 9e-4 | ≤ 9e-4 |
| P9 | `DetectorDefects` dead-pixel overlap between seeds 200 … 215 | independent 144p² | 0.825 vs 0.949 (8 %) | (xoshiro pattern) 0.792 vs 0.922 |

Why legacy is affine: `Random(int)`'s seeding and recurrence (`x_n = x_(n−55) − x_(n−34)` mod 2³¹−1, .NET's
inherited `inextp = 21` variant of Knuth's subtractive generator) are linear mod 2³¹−1 apart from `|seed|`, so
`u_j(s + d) − u_j(s)` mod 1 depends on j and d only. P7 confirms it (the correlation is the same for every base
seed). Findings 60's 1.6–2.1 % probe bias is a different, narrower symptom — a rare-event quantity drawn right after
a rejection loop on the same lagged-Fibonacci stream — and it was not reproduced on any engine quantity (P1b, P5 are
the closest).

## 4. S-3 — published numbers, legacy vs xoshiro256**

Method: the temporary `DefaultRandom` (§6) picks xoshiro256** when the process variable `GCAM_RNG=xoshiro` is set;
one Release build serves both. Driver `s3run.sh` writes a seeded copy of the scenario, runs the CLI in a private
working directory (default `samples/…` outputs land there), stdout to `s3/<tag>_<gen>_<seed>.txt`; `runjobs.sh` runs
the job list `jobs_base.txt` (43 commands = every MC reproduce command in VV.Gcam.Evidence plus theme 44's
`cascade`) for both generators at seeds 12345 (published), 141421, 271828, 314159, 577215. `compare.py` aligns every
numeric token of every output line across the 10 runs and computes Welch z = (mean_X − mean_L) / SE. Legacy at seed
12345 is the "old" value; it reproduces the published text unless noted under §4.2.

### 4.1 Overview

- 828 varying numbers; **12 at |z| ≥ 3 against ~14 expected under no change** (Welch t, ~8 dof, p ≈ 0.017 each;
  tokens are not independent).
- The flagged ones were re-run at **20 seeds per generator** (`single_off`, `doi`, `cascade`, `thermal`, `align`,
  `dose`): all fell to |z| < 3 (largest: thermal compensated-arm RMS z +2.4, cascade single/decay at 57 mm z +2.7).
- Deterministic or seed-invariant outputs (identical across all 10 runs): `mixedfield` (EV-10: 0.55 / 0.27 / 0.93 mm),
  `mixediso`, `frontend` (EV-17's C# 5.7 %), most of `compton` / `compton-strip` / `mixedstrip` at their printed
  precision.

### 4.2 Per evidence entry (old = legacy seed 12345; spreads are the sample sd over 5 seeds unless "n = 20")

| EV / headline | Published | Legacy mean ± sd | xoshiro mean ± sd | Verdict |
|---|---|---|---|---|
| EV-01 centred error | ≈ 0.5 mm | 0.38 ± 0.045 | 0.38 ± 0.045 | unmoved |
| EV-01 sweep, hand-held (non-cyclic / cyclic of 625) | 360 / 172 | 362.6 ± 2.9 / 175.6 ± 2.9 | 362.4 ± 1.1 / 174.4 ± 0.9 | unmoved |
| EV-01 sweep, reference lab geometry | 282 / 148 | 279.6 ± 2.5 / 146.2 ± 1.1 | 279.8 ± 4.6 / 145.8 ± 1.3 | unmoved |
| EV-02 usable half-field, N0 5000, x, 1 m / 5 m | 7.0° / 7.5° | 7.0 / 7.5 (all seeds) | 7.0 / 7.5 (all seeds) | unmoved |
| EV-02 N0 500 + BSR 1, 1 m | 4.0° | 4.8 ± 0.45 (4.0 only at 12345) | 5.0 ± 0 | unmoved; published value is the low edge of its seed spread |
| EV-03 rank 11 / 1 mm / D 30 usable fraction | 0.90 (±21.5 mm) | 0.907 ± 0.007 | 0.904 ± 0.004 | unmoved; sits on the 90 % gate under both |
| EV-03 rank 23 / 1 mm / D 20 | 0.149 | 0.151 ± 0.004 | 0.149 ± 0 | unmoved |
| EV-04 thickness: efficiency columns | 0.56 → 0.25 edge / centre | all z ≤ 1.6 except 18 mm, second efficiency column: 1.670e-4 (all 5) | 1.678e-4 ± 4.5e-7 (z +4.0; printed to 3 digits) | *recheck §4.4* |
| EV-05 taper 0 → 4° edge / centre | 0.82 → 0.99 | 0.824 ± 0.006 → 0.99 (all runs) | 0.824 ± 0.006 → 0.99 | unmoved |
| EV-06 6 × 6 failures / RMS | 21 % | 18 ± 2.9 % / 3.66 ± 0.41 mm | 18.4 ± 3.5 % / 3.76 ± 0.39 mm | unmoved |
| EV-07 threshold (lab, 25 counts RMS / fail) | — | 5.24 ± 0.15 mm / 36.8 ± 2.9 % | 5.29 ± 0.11 / 37.2 ± 1.1 % | unmoved |
| EV-07 4π vs biased | within ~1 % | P1b −0.04 ± 0.10 % | +0.02 ± 0.10 % | unmoved |
| EV-08 sub-cell tent, step 2.4 mm | 0.19 mm | 0.195 ± 0.002 | 0.195 ± 0.002 | unmoved |
| EV-09 efficiency hand-held / original | 2.48e-4 / 1.00e-4 | 247.9 ± 0.15 / 100.0 ± 0.08 counts | 247.9 ± 0.3 / 99.98 ± 0.13 | unmoved |
| EV-09 floor on axis / edge (hand-held) | 0.24 / 0.53 mm | 0.248 ± 0.005 / 0.530 ± 0.012 | 0.246 ± 0.006 / 0.542 ± 0.008 | unmoved |
| EV-10 mixed field | 0.55 / 0.27 / 0.93 mm | identical in all runs | identical | unmoved (noise-free) |
| EV-11 MLEM 3 mm valley / cross-corr. min / FWHM | 0.82 / −103 / 1.76 mm | 0.823 ± 0.004 / −103.3 ± 0.4 / 1.88 ± 0.10 | 0.823 ± 0.003 / −103.5 ± 0.2 / 1.91 ± 0.08 | unmoved |
| EV-12 BSR 4 RMS / failures | 7.11 mm / 65 % | 7.21 ± 0.26 / 63.6 ± 4.2 % | 7.31 ± 0.37 / 67 ± 5 % | unmoved |
| EV-12 `antimask`, bg 1/px, third RMS column | — (0.32 mm at seed 12345) | 0.332 ± 0.013 | 0.354 ± 0.009 (z +3.1 at 5 seeds) | *recheck §4.4* |
| EV-13 DOI 30 mm crystal shift (n = 20) | 0.30 mm | 0.2987 ± 0.0015 | 0.2984 ± 0.0013 | unmoved |
| EV-14 Argmax vs per-pixel (kept / RMS / fail) | 12 % vs 6 %; 3.51 vs 5.65 mm; 16 vs 46 % | 12 / 6 %; 3.47 ± 0.21 vs 5.28 ± 0.3; 16.2 ± 0.8 vs 40 ± 4.6 % | 12 / 6 %; 3.57 ± 0.23 vs 5.40 ± 0.13; 17.6 ± 2.5 vs 39.8 ± 3 % | unmoved |
| EV-15 Co-60 share of the 662 window | 55 % | 55 % all runs | 55 % | unmoved |
| EV-15 compton-strip R / co-located error | 0.469 / 0–4 % | 0.468 ± 0.001 / 0–1 % | 0.469 ± 0.001 / 1 % | unmoved |
| EV-15 mixedstrip R | 3.97 | 3.965 (all) | 3.965 | unmoved |
| EV-18 position vs gain σ | robust | RMS 0.47–0.56 mm over σ 0–1 | same within z ≤ 2.8 | unmoved |
| EV-20 Ir-192 efficiency (hand-held) | 2.62e-4 | 262.3 ± 0.36 counts | 262.3 ± 0.26 | unmoved |
| EV-22 dead time at 10 Mcps (np / p) | 912 k / 550 cps | 912.3 k ± 0.5 k / 560 ± 120 | 912.6 k ± 0.4 k / 440 ± 110 | unmoved |
| EV-23 dose, frontal Cs / Co / Ir (n = 20) | 0.90 / 1.05 / 1.00 | all ratios within z < 3 | | unmoved |
| EV-25 useful shield, scattered / 662 / Co-60 | ~8 / ~20 / ~30 mm | seed 12345: 10 / 20 / 30; see §4.4 (n = 20) | | picks unmoved; knee RMS moved (§4.4) |
| EV-29 thermal droop / residual / RMS (n = 20) | 90.6 % / 6.7 % / ~0.6 mm | 90.6 / 6.7 / 0.489 ± 0.038 | 90.6 / 6.7 / 0.467 ± 0.035 | unmoved |
| EV-30 maskfab RMS at 160 µm / PSR | 3.6 mm / 4.4 → 3.8 | 3.65 ± 0.08 / 4.42 → 3.78 | 3.64 ± 0.18 / 4.38 → 3.76 | unmoved |
| EV-31 align 1 mm offset bias (n = 20) | ~2.5 mm | 2.623 ± 0.010 | 2.623 ± 0.011 | unmoved |
| EV-32 defects 8 %, repaired | 0.68 mm | 0.512 ± 0.011 | 0.496 ± 0.018 | unmoved (see 4.2 note) |
| EV-33 depth S = 40 / 100 / 200 | 39 / 97 / 214 mm | 39.0 / 97.4 / 214.0 | 38.96 / 97.34 / 214.0 | unmoved |
| Theme 44 cascade sum / decay at 18 mm (n = 20) | 3.6e-6 | 3.78e-6 ± 0.71e-6 | 3.84e-6 ± 0.39e-6 | unmoved |
| Theme 44 sum-vs-single slope (n = 20) | 2.16 | 1.70 ± 0.47 | 1.69 ± 0.38 | unmoved; the published slope is one seed of a ±0.4 spread |

Pre-existing differences between the published text and today's legacy seed-12345 output (not RNG — both
generators agree): EV-01 ghost 12 → −6.4 mm (published −6.6), off-axis 6 → 6.0 (published 5.8), centred 0.4
(published ≈ 0.5); EV-32 repaired floor 0.42 → 0.52 mm and unrepaired 1.10 mm (published 0.60 → 0.68, ~1.2–1.8);
EV-25 lightest useful shield 10 mm at seed 12345 (published ~8 mm). These deserve a separate evidence refresh.

### 4.3 Studio evidence tests

`GCAM_EVIDENCE_TESTS=1 dotnet test tests/Gcam.Studio.Services.Tests -c Release --filter Category=Evidence`, once per
generator (logs `s3_studio_evidence_{legacy,xoshiro}.txt`): **7 / 7 pass under both** (5.4 / 5.7 min).

| Evidence (VV.Studio.*) | Legacy (published) | xoshiro | Verdict |
|---|---|---|---|
| Gap rate ratio 100 µm, expected 0.69444: calibration mean / s / tolerance | 0.69428 / 0.0167 / 0.0709 | 0.69846 / 0.0147 / 0.0625 | unmoved (SE of the 8-seed mean ≈ 0.006) |
| Gap rate ratio 200 µm, expected 0.44444 | 0.45548 / 0.0152 / 0.0645 | 0.44753 / 0.0107 / 0.0456 | unmoved (difference ≈ 1.1 SE); the derived tolerance tightens by 30 % |
| Precision, mixed, Cs refined: bias (x, y) / RMS / max (20 seeds) | (−0.093, −0.084) / 0.167 / 0.227 mm | (−0.088, −0.063) / 0.190 / 0.375 mm | unmoved (RMS SE ≈ 16 % at 20 samples); max is one realisation |
| Precision, mixed, Co refined | (−0.194, −1.378) / 1.395 / 1.456 | (−0.188, −1.375) / 1.391 / 1.448 | unmoved — the TODO-17 Co-60 y bias stands (−1.378 → −1.375 mm) |
| Precision, Co-only refined | (−0.200, −1.367) / 1.384 / 1.465 | (−0.172, −1.383) / 1.399 / 1.450 | unmoved |
| Raw grid peaks (Cs, Co) | Co (0.344, −2.281), RMS = max = 2.307 | identical | unmoved (grid-quantised) |
| Optics sampling sweep, pitch 0.6 / 0.3 mm: RMS / max | 1.0810 / 1.9966; 0.5837 / 0.9511 | 1.0734 / 1.9966; 0.5847 / 0.9525 | unmoved (5-seed coarse RMS spread 1.046–1.085 under both) |
| Waveform evidence (single-event pulse readouts) | one smear realisation per case | different realisations | not comparable as numbers; the assertions pass under both |

### 4.4 Rechecks at 20 seeds

| Item (5-seed flag) | Legacy (n = 20) | xoshiro (n = 20) | Verdict |
|---|---|---|---|
| EV-01 off-axis detected counts (z +3.5) | 245.7 ± 0.23 | 245.8 ± 0.21 (z +1.4) | unmoved |
| EV-13 DOI 20 mm (z +3.3) | 0.2777 ± 0.0015 | 0.2780 ± 0.0011 (z +0.7) | unmoved |
| Theme 44 single / decay at 57 mm (z +3.0) | 4.623e-4 ± 6.3e-6 | 4.675e-4 ± 6.1e-6 (z +2.7) | not established (+1.1 %, below 3σ) |
| EV-29 thermal RMS at t = 0.67 (z −3.5) | 0.472 ± 0.029 | 0.462 ± 0.032 (z −1.0) | unmoved |
| EV-31 roll 2° bias (z −4.1) | 0.6225 ± 0.015 | 0.617 ± 0.016 (z −1.1) | unmoved |
| EV-23 1173 keV ratio (z +3.0) | all 84 dose numbers |z| < 3 | | unmoved |
| EV-04 18 mm efficiency column (z +4.0) | 1.675e-4 ± 5.1e-7 | 1.675e-4 ± 5.1e-7 (z 0.0) | unmoved (print quantisation) |
| EV-12 antimask bg 1/px (z +3.1) | 0.342 ± 0.014 | 0.346 ± 0.012 (z +1.0) | unmoved |
| EV-25 useful thickness, scattered / 662 / Co-60 | 8 mm (17/20; 10 mm 3/20) / 20 mm (17/20; 25 mm 3/20) / 30 mm 9/20, 25 mm 11/20 | 8 mm (19/20; 10 mm 1/20) / 20 mm (17/20; 25 mm 3/20) / 30 mm 5/20, 25 mm 15/20 | **picks unmoved** (Fisher p ≈ 0.3 for Co-60); the published Co-60 "~30 mm" is 25–30 mm by seed under both |
| EV-25 shield study, **Co-60 8 mm calibrated RMS** (z +3.2 → +4.5) | median 0.79 mm (0.62–1.17) | median 1.02 mm (0.66–1.62) | **moved beyond noise** (Mann–Whitney p = 1·10⁻⁴; ≈ 0.007 after the 66 rows of the study) |

The shield-study shift is confined to the background **knee** (the thickness where the calibrated RMS falls from
failure-dominated to the floor): Co-60 8 mm p = 1·10⁻⁴, Co-60 10 mm p = 0.06, Cs-137 4 mm p = 0.04, every other
row unchanged (medians equal to 0.01–0.02 mm). xoshiro gives the larger knee RMS, i.e. legacy produced fewer
outlier failures there. The mechanism is not established: the knee RMS is dominated by a few failed realisations
out of the Knuth-Poisson pixel draws (λ ≈ 6–10 per pixel), and legacy Knuth samples carry a small lag structure
(P2b: ±0.3–0.5 % correlations at lags 2–16 depending on λ, absent under xoshiro) — too small, on its own, to
explain a 25 % RMS change. EV-25 quotes only the picks, which did not move; the figure `samples/shield.png` plots
the RMS curves and would change shape at the knee.

### 4.5 Not re-run

- EV-16, EV-21 (Python / RTL studies, Python RNG); EV-24, EV-26 … EV-28 (analytic Python models); EV-19 crystal
  presets (same transport stream as EV-09 / EV-20, deterministic crystal scoring — covered by P1a / P4, not run);
  `masksec`, `maskscatter`, `nonprop`, `finitesrc`, `pileup`, `thermalro`, `maskgeo`, `masksize` (not cited by an
  EV entry); `noise` CSV-only columns beyond stdout; the cocotb benches and `rtl/event_stream.txt` regeneration.
- The Studio desktop and render tests (no desktop).

## 5. S-4 — tests that change

Runs: `dotnet test tests/Gcam.Tests` (Debug), `tests/Gcam.Studio.Services.Tests`, `tests/Gcam.Studio.Tests`, each
with `GCAM_RNG` unset/`legacy` and `xoshiro`.

| Suite | Legacy | xoshiro |
|---|---|---|
| Gcam.Tests | 284 / 284 pass | 281 pass, **3 fail** |
| Gcam.Studio.Services.Tests | 81 pass, 7 skipped (evidence / desktop) | 80 pass, **1 fail**, 7 skipped |
| Gcam.Studio.Tests | 179 / 179 | 179 / 179 |

The failing statistics over seeds (`s4probe`, same study code and configs as the tests; 32 seeds 1001–1032 per generator for thermal and background, 8 seeds for mask fabrication):

| Test | Assertion | Legacy seeds failing | xoshiro seeds failing | Kind | Recommendation |
|---|---|---|---|---|---|
| `ThermalDriftTests.DriftDroopsEfficiency_BiasCompRecovers` | last-slice RMS < 2.5 mm (12 reps) | 4 / 32 (seed 12345: 1.85) | 4 / 32 (12345: 2.54) | regression pin on one seed; the statistic is bimodal (~0.5 mm, or ~2–3 mm when one of 12 reps fails) | assert on a rep count / median, or derive the bound from the measured distribution |
| `MaskFabricationTests.LooserTolerance_DegradesLocalization` | RMS(160 µm) > 1.5 × RMS(0) | 2 / 8 (ratios 1.40–3.31) | 4 / 8 (1.07–2.17) | regression pin; the σ = 0 baseline RMS (1.6–4.6 mm in this default config) dominates the spread | more repeats or a paired-seed comparison; no generator effect established (σ = 0 means 3.12 vs 3.59 mm, ~1.2σ) |
| `BackgroundTests.GradedBackground_…ThenBiasesAtTheKnee` | grad − flat bias at BSR 4 > 2 mm | flat = 9.39 mm in 6 / 32 (fails) | 10 / 32 | regression pin; "deterministic mean-map decode" is not seed-independent — the flat mean map has two competing maxima (7.45 / 9.39 mm) | assert the BSR-1 equality and the gradient's direction, or average the mean map over seeds |
| `DetectorRealismTests.Gain_WidensPhotopeakInQuadrature_…` | — | pass | IndexOutOfRange at line 179 | **test defect**: 256-bin histogram up to 761 keV, an event smeared above it crashes; tails are correct under both (P6d) | clamp or skip out-of-range energies |

Other pinned values:
- **Bit-exact RTL / C# vectors are unaffected**: `WaveformTests` goldens run the deterministic path;
  `CrrcContractTests` (the 26 vectors, 452,608 samples) draw stimulus from `new Random(20261002)` directly, not
  `DefaultRandom`. `rtl/event_stream.txt` is a checked-in `montecarlo eventstream` output that both the RTL and
  the Python reference consume; regenerating it under xoshiro changes its content but not any equality — keep it
  frozen unless the plots are regenerated.
- Seeded determinism tests (`TransportInvariantTests.Run_IsDeterministicForASeed_AndDiffersAcrossSeeds` and the
  Studio replay / cache equality tests) compare a run with itself — generator-independent; they passed.
- Closed-form k·σ tests (`TransportInvariantTests`, `SamplingTests`, `ConfigLoaderTests`) are physics tolerances and
  passed under both; keep them as they are.
- `DetectorGapEvidenceTests` (window counts through `MeasurementStage`) and every Studio spectrum statistic inherit
  the P6c smoothing under legacy; their tolerances are derived from measured seed spreads, which are themselves too
  small under legacy (window part only) — re-measure them after the replacement (§4.3).

## 6. S-5 — replacement design

| Item | Proposal |
|---|---|
| Algorithm | xoshiro256** (Blackman & Vigna 2018), 256-bit state, `NextDouble = (next >> 11) · 2⁻⁵³` (53-bit doubles; legacy gives 31 bits). Private implementation inside `DefaultRandom`; `IRandom` unchanged |
| Seeding | `DefaultRandom(int? seed)` keeps its signature; the state is four SplitMix64 outputs from `(ulong)(long)seed` (exactly the measured variant). Add `DefaultRandom(ulong key)` for 64-bit keys |
| Derived streams | Keep the existing `seed + offset` call sites (P7: xoshiro + SplitMix64 removes the cross-seed correlation; no churn in 30 call sites). Optional later: `DefaultRandom.Derive(seed, streamName)` |
| Per-event streams | `MeasurementStage.MeasureAmplitude` builds its generator from the 64-bit key `((ulong)(uint)seed << 32) \| (uint)index` (P6b clean); the int-wrap `seed + index·104729` leaves a measurable lag-1 correlation (+3.9e-4, z +5.5) even under xoshiro |
| Unseeded path | Today `new Random()` (already xoshiro-based in .NET 6+, OS-seeded). Proposal: seed the same xoshiro from `Random.Shared.NextInt64()` and expose the chosen seed (property) so an unseeded run can be logged and replayed; behaviour otherwise unchanged |
| `NextOnUnitSphere` | unchanged formula (2 draws), now on the new `NextDouble` |
| Direct `System.Random` users (`CrystalUniformity`, `DetectorDefects`, `MaskFabrication`) | leave for now: a seed names one fixed manufactured pattern (EV-18 / EV-30 / EV-32 refer to specific seeds); switching them changes those patterns. Decide separately (author) |
| Test fixtures (`CrrcContractTests`, `MinMaxPyramidTests`) | leave on `new Random(seed)`: they are reproducible stimuli, not physics, and the RTL vectors stay bit-identical |
| RTL vectors | no change needed; `rtl/event_stream.txt` stays frozen (regenerate only together with its plots) |
| Thread safety | unchanged: one instance per stream, not shared across threads |
| Repository test (the plan's "TODO-26 brings a repository test") | `RandomQualityTests`: (1) cross-seed independence — corr(u_j(s), u_j(s+d)) over 2 000 base seeds within 4σ for d ∈ {1, 777, 104729}; (2) MeasurementStage window-count variance ÷ binomial within its χ² bound over 200 blocks of 1 000; (3) KN mean ε against quadrature at 10⁷ (4σ ≈ 0.01 %, legacy fails at 10⁸ only — so (1) and (2) are the discriminating ones) |
| Re-pin | the four tests in §5 from their measured distributions; Studio evidence tolerances re-measured (their spreads widen where the smear lattice is removed) |

Temporary measurement change (to be discarded; `git diff src/Gcam.Core/DefaultRandom.cs`): `DefaultRandom` gained a
static `UseXoshiro` read from `GCAM_RNG`, a SplitMix64 seeding of four `ulong` state words when seeded and
`UseXoshiro`, a private `NextXoshiro()` (xoshiro256**, 53-bit double), and `NextOnUnitSphere` now calls
`NextDouble()`; with `GCAM_RNG` unset it is bit-identical to fb4c630 (verified: the legacy seed-12345 outputs
reproduce the published text, and all 284 + 81 + 179 tests pass).

## 7. Probes and outputs (`%TEMP%\gcam-rngbias\`)

| Path | What |
|---|---|
| `probe\Program.cs` (+ `probe.csproj`) | S-2 probes P1–P9 (`probe <test> [N] [seeds]`: `probe4pi`, `p1a`, `p1b`, `poisson`, `kn`, `crystal`, `cascade`, `reseed`, `reseed64`, `window`, `tail`, `xseed`, `xrun`, `defects`); builds in `bin*` |
| `s2_*.txt` | S-2 outputs |
| `s3run.sh`, `runjobs.sh`, `jobs_base.txt`, `jobs_recheck.txt`, `jobs_shield.txt` | S-3 driver (process-scoped `GCAM_RNG`) and job lists |
| `s3\<tag>_<gen>_<seed>.txt` (+ `.csv`, private `.d\` working directories), `s3\scen\` | every CLI run's stdout / CSV; seeded scenario copies |
| `compare.py`, `compact.py`, `cmp_*.txt` | token alignment and Welch z; comparison outputs |
| `s4probe\Program.cs`, `s4_stats.txt`, `s4_tb.txt` | S-4 seed distributions of the failing tests' statistics |
| `s4_*_{legacy,xoshiro}.txt`, `s3_studio_evidence_*.txt` | test-suite logs |

`s3\single_legacy_12345.txt` is a stray from a first runner bug (xargs joined lines ending in a blank); ignore it.
