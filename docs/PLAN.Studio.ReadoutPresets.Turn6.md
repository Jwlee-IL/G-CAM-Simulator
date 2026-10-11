# PLAN.Studio.ReadoutPresets.Turn6 — v4 experimental offering and confirmation preparation

Scope: TODO-41 recipe / rule v4, the development-only High-res calibration budget, verification and a prepared confirmation command. No Studio stage, no confirmation photon histories and no qualification published. Historical v1–v3 files and the exploration tables are retained.

## High-res 662 keV budget

The fixed development levels are 8,000 and 32,000 **scored flood histories per crystal on average**, i.e. 15,488,000 and 61,952,000 histories for the 44 × 44 module. Uniform normally incident GAGG transport, 0.4 mm pitch, 0.1 mm reflector gaps, 0.15 mm entrance absorber, 2 mm backing, virtual matched sensors, unlimited microcells, DPC 10/1000 ohm, Adjoint network solve, LatticeGrowth LUT v2, 32 histogram bins per crystal, 100,000 optical photons per bin and eight DOI bins are unchanged. The larger flood replays the same transport / response / noise stream prefixes. No imaging was performed in this budget experiment. Budget success is the engine's blind LUT success, not a truth-assisted grid fit.

| Development seed | Nominal histories/crystal | Outcome | Measured maxima | Photopeak-window events | Calibration seconds | Exact engine failure |
|---|---:|---|---:|---:|---:|---|
| 41000001 | 8,000 | refused | 1,922 | 5,429,737 | 299.35 | measured lattice spans 44 × 44 with 1922 maxima, expected 44 × 44; unsupported slots (43, 8), (42, 13), (42, 18) |
| 41000001 | 32,000 | success | 1,936 | 21,726,676 | 1214.01 | — |
| 41104730 | 8,000 | refused | 1,920 | 5,429,629 | 300.64 | measured lattice spans 44 × 44 with 1920 maxima, expected 44 × 44; unsupported slots (16, 0), (0, 9), (0, 13) |
| 41104730 | 32,000 | success | 1,936 | 21,726,390 | 1189.84 | — |
| 41209459 | 8,000 | refused | 1,919 | 5,429,848 | 290.20 | measured lattice spans 44 × 44 with 1919 maxima, expected 44 × 44; unsupported slots (4, 0), (36, 0), (34, 1) |
| 41209459 | 32,000 | success | 1,936 | 21,726,566 | 1154.98 | — |
| 41314188 | 8,000 | refused | 1,927 | 5,429,694 | 291.34 | measured lattice spans 44 × 44 with 1927 maxima, expected 44 × 44; unsupported slots (0, 6), (0, 7), (42, 18) |
| 41314188 | 32,000 | success | 1,936 | 21,726,537 | 1152.57 | — |

Propose **32,000 histories per crystal**, the smallest tested budget that succeeds on all four development seeds. This is the sole permitted geometry/line budget change. Every other line/head retains 2,000. There is no change to smoothing, support significance, lattice tolerances, optics, energy window or electronics after seeing results.

Zero failures in four independent trials gives a nominal one-sided binomial upper 95% failure probability of 52.71%. Because the level was selected from two development candidates, a conservative Bonferroni bound over those candidates is 60.24%. These sparse development observations are not validation. All 32 independent confirmation calibrations must succeed; zero failures there would give an upper one-sided 95% bound of 8.94% for another seed under the same model. No confirmation failure triggers a larger budget or a parameter repair in v4.

The portable development budget probe has exactly the scratch measurement source bytes; SHA-256 `4142b6b7806f5336a41f17eba1f6c0a8f9627bebb6199d420980bc0b412bfd3e`. Its result is explicitly development-only and pinned as an input of v4. Formal confirmation executions use the repository provenance machinery, immutable staged input/binary closures and complete-ensemble aggregation.

## Frozen v4 measurement definitions

- **Candidate and calibration.** One frozen candidate; T41_SELECTION16 is skipped. Use T41_CONFIRM32 for every (preset, line), including 122.06, 661.7 and 1332.5 keV. Baseline retains the Legacy LUT and Legacy solve; the other heads use LatticeGrowth v2 and Adjoint. Every calibration failure is a successful recipe execution with its exact failure message and seed recorded, not a crashed or dropped run. Missing/corrupt executions and provenance fail closed before publication.
- **Offering.** A (preset, line) is offered, labelled experimental, iff calibration succeeds on every confirmation seed. RMS, bias, remote peaks, F, G, mis-ID, sensitivity and numerical performance are never offer gates. The offer is per line; rate and exposure are dimensions of the descriptive cost table.
- **Geometry and positions.** Detector-to-source distance 1,000 mm. Nine source positions: centre, four axes and four corners at ±0.45 of the nominal FCFOV half extent. Axis offsets are Baseline 26.25 mm, Sharp 25.59375 mm, Wide 76.5 mm, High-res 14.625 mm. Source positions and all current optics are in the recipe; the scene contains only the declared monoenergetic point source.
- **Decoder.** Finite-mask cross-correlation, reconstruction half extent 0.95 of nominal FCFOV half, grid step max(0.2 mm, one quarter of a projected mask element), Tent peak interpolation. The cached linear correlations reuse engine PointResponse and engine peak interpolation. First-trial images are checked against the engine decoder at the discrepancy probe's unchanged 1e-9 mm agreement bound.
- **Direct reference and acceptance.** Ideal largest-summed-deposit crystal assignment from the same history; true total deposit within ±15% of the line. Anger accepts a trigger with a valid blind LUT label and that assigned crystal's calibrated energy within the same ±15% physical energy interval. Every multi-crystal event remains eligible. The ideal reference has neither electronic losses nor pile-up; the physical readout's losses, gains and energy migration appear in its actual accepted counts.
- **Exposure.** Targets of 1,000 and 4,000 expected direct-reference counts at each position. An independent 120,000-history direct pilot determines a fixed mean proposal budget N = round(target × 120,000 / pilot direct acceptance). Each trial generates an exact Poisson(N) number of new proposals, including null histories. The two paths see those same fresh photons. Directional-bias rejection/normalisation is common to both paths; equal proposal exposure therefore represents equal physical emitted fluence within each paired scene. Stored N denotes biased proposals, not an isotropic 4π emitted-photon count. Actual accepted counts are reported with seed intervals; no truncation to equal accepted counts and no empirical-history resampling.
- **Flood correction.** Replay the uniform calibration flood, with each path's imaging acceptance and its own assigned-crystal histogram. Weight is the path histogram mean divided by that bin count; no clipping, minimum-count substitution or retuning. An empty bin makes that path's cost unavailable with an explicit measured reason; it does not change a successful LUT's offer. The same isolated correction is used at rate.
- **Rate.** Isolated plus 100,000 scored input hits/s across the array. Poisson arrivals; every scored deposit, including continuum/subthreshold hits, contributes four actual channel pulses. Use the full engine ReadoutPulseProcessor.Process for imaging, not dominant-hit placement shortcuts. Direct remains ideal. Rate and isolated repeat identical fresh transport/response and proposal budgets, with their appropriate noise/timing processing.
- **Source error.** ex = [atan(decoded x / 1,000) − atan(source x / 1,000)] in degrees, likewise y. MSE is the equal-position/trial mean of ex² + ey²; absolute radial RMS is sqrt(MSE). Bias is sqrt(mean over positions of the squared per-position mean error vector). Opposite field biases cannot cancel. Bias is the finite-64-trial estimate of each position's mean error, not an asserted asymptotic systematic floor; no finite-trial noise subtraction is made.
- **Remote.** Radial angular error greater than cell pitch / mask–detector distance radians, converted to degrees: one nominal paraxial mask element, the optical resolving scale rather than crystal pitch. It is independent of the readout choice. Every remote peak stays in the RMS. Probability is the equal-position mean remote fraction.
- **F.** Ratio of the outer-seed mean Anger MSE to the outer-seed mean Direct MSE, at the same declared exposure. Direct reference F = 1. F is an equal-fluence source-MSE penalty; bias and remote peaks prevent calling it a universal measurement-time factor.
- **Crystal diagnostics.** Held-out uniform flood, 3,000 scored histories/crystal, plus a 10,000-hit 100,000 cps cell. Anger global code-sum photopeak window, ±15%, preserves v3's diagnostic convention. Paired first-interaction G per axis is mean(atan((Anger−first)×pitch/D)² − atan((largest-deposit−first)×pitch/D)²), rad², on the same Anger-accepted events. Single-crystal mis-ID and multi-crystal share use that accepted population. Full-energy means deposit within the engine's unchanged 1 + 1e-6 keV transport cutoff tolerance. Sensitivity[k] = assigned-windowed events at k / full-energy histories whose first interaction is k; report the map and weakest/median. The rate G/mis-ID/share use the dominant hit's own truth, one acceptance/history, as v3; its sensitivity display uses the isolated map. This diagnostic truth never enters calibration or imaging.

## Populations, intervals and proposed cost reduction

Calibration **and crystal diagnostics use all 32 confirmation seeds**. The proposed imaging-cost subset is the predeclared first eight T41_CONFIRM32 seeds, fixed by membership in recipe v4 before confirmation. Development uses all four T41_DEV4 seeds. There are 64 fresh trials at each of nine positions for every exposure/rate, giving 4,608 trials per imaging row across eight outer seeds. A missing calibration run refuses publication; an observed failed calibration refuses the corresponding offer. An unavailable Anger path does not produce a survivor-only interval. Failure of a calibration seed outside the eight cost seeds still refuses offering; the hand-computed provenance fixture explicitly checks this.

Intervals are nominal two-sided 95% Student intervals at the outer-seed level, equal seed weights. RMS/bias intervals operate on MSE/bias-squared then square root, with a nonnegative lower endpoint. Remote fraction/count intervals use seed means (probability endpoints clipped to [0,1]). F uses the paired log-ratio delta influence (a_s − F d_s) / mean(a), then exp(±t SE). Quantiles are pinned for 4, 8 and 32 clusters. These are outer-cluster approximations, not exact small-sample or rare-peak coverage guarantees. Zero between-seed variance gives a degenerate interval and does not establish zero remote risk. No interval endpoint is a gate.

At equal between-seed variance, eight rather than 32 cost clusters widen a Student interval by approximately **2.32×** (t7/t31 × sqrt(32/8)). Within-seed trial noise is also part of the cluster variation. Crystal diagnostics retain 32 clusters. This reduction is a proposal embedded in the prepared v4 candidate; it must be accepted before the planner starts confirmation. Changing the populations requires new pins, not an override on completed evidence.

## Hashes and identity

| New file | SHA-256 |
|---|---|
| `samples/evidence/studio-readout/rule-v4.json` | `854adf86627977ef4be05e509dd69e550b2b8ce0372939a2379c1501bfb30d5f` |
| `samples/evidence/studio-readout/recipe-v4.json` | `057b9e161ea91ea41f40b631cedf85faaffc0b7170f4d623e6fcf3202f678a70` |
| `samples/evidence/manifest-studio-readout-v4.json` | `c8f1d052a4f2b93343652c8046a7eaeb424f1131b89e83d9984e9e10245832b3` |
| `samples/evidence/studio-readout/development-calibration-budget-v4.json` | `688f10c6bcffafe246cf68b1f4294172afdb2e62f4751794fa161e8a170dc6b5` |
| `samples/evidence/studio_readout_v4_driver.py` | `451d2f8f0b3f13d9c70adb660395a51feba2acf2cf00659367b6ffd74c5936e6` |
| `samples/evidence/studio_readout_v4_select.py` | `bb400764b9e1e9ad939c9fb18077783f635cd9efe27750c0cd3363598785a53a` |
| `samples/evidence/studio_readout_v4_confirm.ps1` | `c2fb1424ff43537132a24beb3ae4edf790076efcf9891fdfdadc569b2681e781` |
| `samples/evidence/probe-v4/Program.cs` | `487cbe865e8bd80c82e08da3e59a8326af29b814b9589ba53e7775ccd6d7ea74` |
| `samples/evidence/probe-v4/StudioReadoutRecipeV4.cs` | `31fcd063005ada528a3f86d7578d3c32040ae940c294f98be56b983fd6ba19d0` |
| `samples/evidence/probe-v4/ImagingDecoder.cs` | `ebc68d1f1e0fee394acc954e17ec5defb30b68a8d3ff22081e132beb99470dd9` |
| `samples/evidence/probe-v4/Gcam.EvidenceProbe.csproj` | `dba497688bb0404cb3caa0bb4a30ffb603a95549238f8cc41494d35896c66648` |
| `samples/evidence/probe-v4-budget/Program.cs` | `4142b6b7806f5336a41f17eba1f6c0a8f9627bebb6199d420980bc0b412bfd3e` |
| `samples/evidence/probe-v4-budget/Gcam.ReadoutCalibrationBudget.csproj` | `ddb7d57b160cb8c619b3d3180c03fcd290a4f02e88e0276690849938ce2acd2e` |
| `samples/evidence/tests/test_studio_readout_v4.py` | `22eb656271e7a3dfae1b0e4012b40eab8eb295085188e2cc7e5fd51d7c28cd27` |

Frozen managed-closure digest: `3a323fcd4d27941503498aeef887cc7392f601e6c6f07375199cb762438e837a`; main v4 probe DLL: `889d0b5e8f50b15dbe52a017ef1969a7a31525fdc4e50d58f20833bd96441f9d`. The manifest embeds the full file/hash map, recipe/rule pins and adapter hash for every family. No old probe dispatch or v1–v3 adapter/selector was changed.

Before/after audit: **474 earlier source/input files byte-identical**, including every existing src file, the old probe sources, older rule/recipe files, and existing engine tests. The main-tree plan was read only. DirectCrystal and legacy defaults have no source changes. Full .NET tests pass the pre-readout list-mode/image/config SHA fixtures and the Legacy-default LUT tests. Baseline scenario remains `eb5b96835c0b68a88c5c26965540f85f1a313acf1cc0b49955cf7921463edddc`; pre-readout Baseline event stream remains `0057f59cd68691b61d179c499bff4f3f201832032ff01269d7c356398e57ceb1`; serialized lab configuration remains `d833711a64bf423f408d6321a29140e3cc2f25a7df12c3df89a4a0a5d1fbecd1`. seeds.json (`e418ea695107000062465880211924b5dca02b5372e8bb7b2062a9c4e9b9d6d9`) and the calibration confirmation pin (`2d1b73cb2d643e83a77b50a823b62246a8d957b7d2824fdaf7025882fe40478d`) match their committed bytes.

## Verification

| Check | Result |
|---|---|
| Release solution build; v4 imaging and portable budget probe builds | success, zero warnings/errors |
| Full dotnet test | 912 passed, 32 opt-in skipped, 0 failed; 944 discovered cases |
| Python unittests | 78 passed, 1 existing Git-mutation fixture skipped, 0 failed; 79 cases |
| New v4 selector tests | 14 passed; analytic interval/ratio/bias/diagnostic oracles and complete synthetic provenance ensemble |
| calibration_record --check | four records up to date; all checks pass; no record edited |
| check-catalog --discover | 176 units, zero incomplete descriptions; updated authorized VV.Tests entry/discovery block |
| Reduced actual v4 timing pilot | DEV seed 41000001, Baseline; four trials/position; declared exposures/rates; full optics/calibration/diagnostic budgets |
| Pilot direct pairing checks | 216 exact trial matches between isolated/rate (same counts and decoded errors); remote classification consistent |

TEMP/TMP for dotnet tests and GCAM_PROVENANCE_TEST_ROOT for Python are process-local under the task scratch root. UI/render/evidence opt-ins were disabled; no Studio/window was launched. Normal nongated component tests ran as required by the full suite. The timing-only pilot is not a confirmation or population-cost quote, and its reduced trial count is not used for the frozen 64-trial measurements.

## Timing and exact prepared confirmation command

Measured Baseline timing at four trials: build 6.55 s; calibration 122/662/1332 keV 0.98 / 0.91 / 1.00 s; corresponding cost stages 2.15 / 14.25 / 23.28 s. Cost-stage ×16 is a conservative 64-trial projection because it also multiplies fixed flood, diagnostic, decoder-cache and oracle work. Final code reports diagnostics separately and performs them on every seed; the pilot's earlier combined stage timing remains retained.

Sharp/Wide/High-res estimates combine this measured actual-rate pilot, crystal-channel work scaling, earlier same-machine four-seed fresh-history timings (Baseline 24–25 s, Sharp 134–145 s for the isolated 1,000-count diagnostic contract), earlier geometry costs and the new measured High-res calibration. They are planning ranges, not claimed full v4 pilots. High-res 32k calibration alone takes about 20 minutes; the same flood replay adds cost on imaging seeds. Channel work grows with the number of sensors, and higher-energy photopeaks require more scored pulses per accepted reference count. No additional long timing run was started.

| Geometry | Calibration + diagnostics only seed (planning estimate) | Full imaging-cost seed (planning estimate) |
|---|---:|---:|
| Baseline | under 1 min | about 11 min |
| Sharp | about 2–4 min | about 45–75 min |
| Wide | about 3–6 min | about 65–110 min |
| High-res | about 25–30 min | about 150–220 min |

With two workers, 128 complete geometry/seed runs and 32 imaging-cost runs (eight per geometry), allow **about 28–48 hours** including contention. Imaging on all 32 seeds per geometry would instead be roughly **3–5 days** with two workers. The first-eight proposal preserves all-32 calibration and diagnostic conditions. The previous memory planning record identifies 32 GB; two workers are conservative for the streamed High-res calibration (large transient tuple/window buffers still exist) and private optical tables. Avoid larger jobs counts; no memory cap or optical/calibration budget is lowered.

Prepared, **not started**, from the repository root, as a single background command owned by the planner:

```powershell
pwsh -NoProfile -File samples/evidence/studio_readout_v4_confirm.ps1
```

The child process uses a fresh task-relative output root named `confirm32-v4-20261009`, process-local TEMP/TMP below it, `--jobs 2`, the separate v4 DLL and the four confirmation families. It then aggregates with the v4 selector only after successful completion. The output root did not exist during preparation. Rerun that same child-process command to resume per (geometry, seed); completed valid runs are reused, incompatible/corrupt reuse is refused, failed attempts remain retained. No --force, selection family, repair family, watcher or background monitor is involved. Do not edit/rebuild the closure during a run. Both the run driver and selector check pins/closure; calibration failures themselves do not make the recipe process fail.

## Proposed wording — not applied

### New Findings theme 69

**69. Studio physical-readout presets: experimental calibration offer with measured imaging cost (TODO-41, confirmation).** Blind lattice growth v2 resolves the bowed fine-pitch flood without truth-assisted placement. Keep the 12 × 12 Legacy LUT/solve and DirectCrystal fixtures unchanged. At 2,000 histories/crystal High-res 662 keV calibration failed; the fixed development budget study selects 32,000 from the predetermined 8k/32k levels. V4 offers a preset/line experimentally only if every CONFIRM32 calibration succeeds. Imaging costs are descriptive: absolute source RMS, per-position bias, remote-peak fraction and equal-fluence MSE ratio F, with seed-cluster intervals at 1,000/4,000 direct-reference counts, isolated and 100,000 input hits/s. Crystal diagnostics remain beside them, not gates. CONFIRM32 v4 offers Baseline and Sharp at 661.7/1332.5 keV, Wide at 1332.5 keV and High-res at 1332.5 keV, all experimental. Every 122.06 keV cell is refused; Wide/661.7 fails on four seeds and High-res/661.7 fails on one despite the fixed 32k budget. No failure is repaired or excluded. The full confirmation cost/diagnostic tables and literal failure messages follow in the Confirmation section.

The fresh-history discrepancy experiment measured Baseline 662 keV RMS 0.653° Anger / 0.163° Direct, bias 0.292° / 0.152°, F 15.97 [13.17, 19.36]; Sharp 1332 keV RMS 1.131° / 0.103°, bias 0.556° / 0.073°, F 119.51 [102.41, 139.47]. Conditions: nine central in-FCFOV positions at 1 m, 1,000 expected direct counts, own flood correction, finite-mask correlation. These retained values are DEV4 exploratory estimates. Independent confirmation at 1,000 direct counts gives Baseline/662 RMS Anger/Direct 0.6258/0.1636 degrees, bias 0.2964/0.1524 degrees, remote 5.078%/0%, F 14.629 [12.530,17.080]; Sharp/1332 RMS 1.1368/0.1035 degrees, bias 0.5671/0.0732 degrees, remote 13.260%/0%, F 120.538 [112.747, 128.869]. At 4,000 counts their isolated F values become 1.664 [1.334,2.074] and 5.004 (full interval below). Cost populations contain eight fixed outer seeds; calibration contains all32. Approximately 70% of multi-crystal histories migrate relative to the largest-deposit assignment; replacing Anger placement on its accepted histories restores direct localisation, while replacing only the energy window does not. ReadoutStudy and the independent probe agree to 1e-9 mm in theme-68 conditions. The earlier conditional history-pool estimates remain records; the two fresh-history rows supersede their population-cost interpretation. See the exploration discrepancy check and Turn6 recipe for scope and reproducibility.

### Theme 68 scope note

The quoted 0.406 ± 0.006 mm against 0.367 ± 0.001 mm is mean radial localisation error at equal accepted counts for one centred near-field lab source, at thousands of counts. It is not field-wide source RMS or an equal-fluence imaging-cost bound at 1 m. Development reproduction gives 0.394 / 0.362 mm and matches ReadoutStudy event-for-event. At low counts across the Studio field, multi-crystal migration can let competing remote decoded peaks dominate the source MSE; theme 69 measures that separate regime. Successful flood calibration alone establishes experimental usability, not localisation performance.

### EV-36 scope note

The selected readout's near-field equal-count localisation result applies to the declared lab geometry and centred-source study. It does not imply a ten-percent imaging penalty across Studio optics, field positions, lower counts or equal emitted fluence. TODO-41's independently confirmed imaging study finds substantially larger, exposure-dependent source errors from multi-crystal migration and remote peaks: Baseline/662 at 1,000 direct-reference counts has source RMS Anger/Direct 0.6258/0.1636 degrees, F 14.629 [12.530,17.080], and Anger remote probability 5.078% [4.515,5.642]; Sharp/1332 has 1.1368/0.1035 degrees, F 120.538 [112.747, 128.869], and Anger remote probability 13.260% (full interval below). Both are experimental offers after all32 calibrations succeed. Preset/line offerings remain experimental, require every pinned confirmation calibration to succeed, and display measured source RMS, bias, remote-peak probability and F with declared conditions/seed uncertainty. F is a source-MSE ratio, not a universal measurement-time multiplier. CONFIRM32 v4 yields six experimental preset/line offers and six refusals; High-res/662 is refused on its one failed confirmation seed at the unchanged 32k budget. Full source costs use eight predeclared outer seeds, crystal diagnostics all32. DirectCrystal stays the engine default. Studio currently provides the experimental Baseline preparation path; generalized offers are a later stage.

### LIM-11 scope note

The physical path uses virtual matched sensors, assumed optical constants and unlimited microcells. These assumptions are more consequential at finer pitch; calibration success cannot validate saturation, recovery or measured detector performance. Multi-crystal centroid migration also adds an exposure/geometry-dependent imaging cost: low-count remote decoded peaks can dominate errors despite a valid flood LUT. Show experimental preset/line availability beside measured absolute RMS, bias, remote-peak probability and equal-fluence MSE ratio, with scope and seed uncertainty. Confirmed examples show why absolute costs are required: Baseline/1332 has Direct/Anger RMS 1.652/2.091 degrees at 1,000 counts despite F only1.603; High-res/1332 has 0.04881/0.51139 degrees and F109.777, falling to F2.669 at 4,000 counts. DirectCrystal remains the default; no performance threshold is inferred from calibration success. The physical Studio option remains an engineering simulation, not a validated instrument claim.

### Replacement SR rows

| Row | Proposed requirement |
|---|---|
| SR-DET-05 | Detector shall default to DirectCrystal and offer the experimental GAGG FourOutputAnger readout per preset/calibration line according to the pinned calibration-only confirmation record. Every confirmation seed shall have succeeded for an offered preset/line; a refusal shall display the measured calibration failure. Readout/preparation identity and acquired module geometry shall be captured at Start and locked while acquiring or while data exist. All detector, imaging, waveform and circuit views shall use that captured geometry. IndependentSipm remains outside this Studio implementation. |
| SR-RUN-34 | Physical Start shall refuse nonzero ambient/BSR and a refused or unprepared preset/line before transport, retain entered values and display an actionable reason. Custom shall follow SR-DET-11. Measured RMS, bias, remote-peak probability, F and crystal diagnostics shall not block an otherwise calibrated experimental readout. DirectCrystal shall retain its supported inputs, seeded stream and defaults; the microcell policy is unchanged. |
| SR-DET-10 | For each measured preset/line, Detector shall display experimental calibration availability, complete confirmation population, refusal seeds/messages, and the separate descriptive cost table. At declared exposures and rate conditions the table shall show both readouts' absolute angular source RMS, per-position bias magnitude, remote-peak probability, actual accepted counts and paired equal-fluence MSE ratio F (Direct reference 1), with outer-seed intervals and population/geometry/decoder scope. Crystal paired G, single-crystal mis-ID, weakest/median sensitivity with map and multi-crystal share shall remain diagnostics. Unavailable measurements shall be explicit, failed seeds shall remain recorded, and missing/corrupt evidence shall fail closed. F shall not be labelled a general measurement-time factor; zero observed remote peaks shall not imply zero risk. |
| SR-DET-11 | Any Custom geometry may Prepare with the declared experimental calibration policy. Successful blind calibration may run as experimental, unvalidated; failed calibration shall refuse with its own failure. A module fingerprint matching an offered preset may inherit its per-line calibration-availability evidence. Source imaging cost shall only be asserted for matching full imaging conditions (mask/geometry, line, decoder, field positions and exposures); otherwise inherited costs shall be labelled as measurements of the named reference preset, with Custom cost unmeasured. Preparation/acquisition identity shall remain immutable. |

**PD-4 scope clarification needed.** A module fingerprint omits mask rank, cell size, mask–detector distance and decoder. It cannot establish the Custom source-imaging cost after those change. The proposed SR-DET-11 therefore preserves module calibration inheritance while separating its scope from the source-cost evidence. This is proposed wording for the author, not a Studio/policy change in this turn.

### Backlog proposal

Investigate a migration-aware coded-aperture forward model and MLEM, using measured simulated detector response including multi-crystal energy/position migration, acceptance and rate. Compare absolute field-wide source errors/remote peaks at equal fluence on independent histories against the current correlation decoder. Study multi-crystal handling separately; preserve DirectCrystal defaults and require new frozen evidence before claiming improved costs.

## Compute, deviations and complete write-command inventory

The calibration-only development batch consumed **103.21 minutes** summed build/calibration wall time, plus the short builds/tests and about 49 s timing pilot. This exceeded the approximate 90-minute aim. I should have timed one budget seed before launching the four-seed batch. Once running, stopping a process required author approval under the guard, so the already-started batch was allowed to finish; no larger confirmation/imaging study was started. The Baseline timing command was intended as foreground but exceeded the tool's 30 s wait and briefly became a second background command while that batch was active. It completed successfully; that overlap violated the one-background-command constraint and is recorded here rather than hidden. No processes remain running from this turn.

Commands/patches are retained verbatim in the conversation log. Let R denote the task's `explore/v4` scratch directory and T its `turn6-tests` scratch directory. Every filesystem write was within the working tree or task scratch root:

1. `New-Item -ItemType Directory -Force R`; `Copy-Item` of the earlier scratch Check.csproj/Helpers.cs to R/Budget.csproj and R/Helpers.cs. Both destinations were new.
2. `apply_patch` created the scratch Main.cs budget probe and scratch catalog_v4.py, refresh_catalog_v4.py, pin_v4.py, pilot_checks.py and this report_v4.py; it created/updated only new v4 repository source/driver/selector/test/rule/confirmation-script files listed in the hash table. Earlier pinned versions were not edited.
3. `dotnet build R/Budget.csproj -c Release --nologo` (one invocation): scratch bin/obj/restore outputs.
4. One serial background command: `foreach($devSeed in @(41000001,41104730,41209459,41314188)) { dotnet (Join-Path R 'bin/Release/net9.0/Budget.dll') $devSeed (Join-Path R "budget-$devSeed.json"); if($LASTEXITCODE -ne 0){throw 'budget failed'} }`. Each development process records its two levels; four distinct result files, no selection/confirmation/repair photons.
5. The logged PowerShell before-hash capture writes R/before.json; `New-Item samples/evidence/probe-v4`; `Copy-Item` earlier Helpers.cs to the new ImagingDecoder.cs. No old source was replaced.
6. Three successful `dotnet build samples/evidence/probe-v4/Gcam.EvidenceProbe.csproj -c Release --nologo` invocations, following initial implementation, rate diagnostics and all-seed diagnostics. They write repository bin/obj and allowed restore outputs.
7. `python -B R/catalog_v4.py` adds the authorized VV.Tests v4 entry/count; `python -B R/refresh_catalog_v4.py` updates only that reviewed entry's source hash/methods. `python -B samples/testing/test_records.py check-catalog --update-discovery` updates the authorized discovery block (two completed invocations before the final reviewed source refresh; final discover check follows). Read-only discover checks publish no evidence.
8. `New-Item -ItemType Directory -Force T`; `dotnet build Gcam.sln -c Release --nologo`; `dotnet test Gcam.sln -c Release --no-build --nologo --logger 'console;verbosity=minimal'`, with process-local TEMP/TMP under T and opt-ins disabled. Build/test artifacts and temporary fixtures remain in authorized locations.
9. `python -B -m unittest discover -s samples/evidence/tests -v`, then the updated full suite without `-v`; v4-only unittest invocations after the provenance fixture addition and all-seed diagnostic change. GCAM_PROVENANCE_TEST_ROOT is T/provenance, Git mutation opt-in is 0, bytecode disabled. Only retained scratch fixture files were written; earlier selector-only test invocations before adding fixture writes did not write files.
10. The logged PowerShell command creates R/recipe-timing.json (unpinned, four trials), then `dotnet samples/evidence/probe-v4/bin/Release/net9.0/Gcam.EvidenceProbe.dll studio-readout-v4 41000001 samples R/recipe-timing.json Baseline dev timing-unpublished > R/timing-Baseline.json 2> R/timing-Baseline.stderr.txt`. Both redirection targets were checked task-relative targets. `python -B R/pilot_checks.py` writes R/pilot-checks.json.
11. `New-Item samples/evidence/probe-v4-budget`; `Copy-Item R/Main.cs samples/evidence/probe-v4-budget/Program.cs` (new destination); `dotnet build samples/evidence/probe-v4-budget/Gcam.ReadoutCalibrationBudget.csproj -c Release --nologo` writes its bin/obj/restore outputs only. The copied development source hash matches exactly.
12. `python -B R/pin_v4.py` writes the new portable development budget record, recipe-v4.json and manifest-studio-readout-v4.json; all use UTF-8/LF. It refuses incomplete budget data or no successful level; it starts no command.
13. `python -B R/report_v4.py` writes R/audit-final.json and this new Turn6 report, verifies every before-hash and the frozen binary closure, and checks seeds/calibration pin/scenario against committed bytes using read-only `git show`.

No product source changes, deletions, Git mutations, installs, desktop, uploads or process stops. No edits to the main-tree plan, AGENTS, other VV records, earlier PLAN records, README, CLAUDE or pinned v1–v3 files. Only the new Turn6 report and authorized VV.Tests entries are documentation writes. Calibration_record --check wrote nothing. Confirmation script preparation is a file edit; it was not executed.

## APPROVAL REQUESTS

None. The confirmation command is prepared for the planner and has not been started.


## Confirmation

The planner completed all 128 geometry/seed runs with exit 0, resuming twice after host memory pressure and reducing concurrency to one. Aggregation validates all 128 completed run provenance records, stdout hashes, frozen executors, recipe/rule identities and populations. No photon histories were generated in this short turn. Rule, recipe and manifest bytes remain unchanged. The six offer decisions below depend only on all-32 calibration success; costs do not gate. Numerical aggregation is validated, but summary publication is pending the sidecar output-scope clarification below.

### Selector publication repair and verification

The reported exception was a dict passed to provenance.analysis, which calls vars(). main now passes argparse.Namespace(Phase=args.phase), matching the older selector. A second publication bug was exposed on review: pv.publish had its outputs/provenance arguments reversed. Both calls now match the production provenance API. No aggregation formula, tolerance or offer rule was changed.

The new main-entry regression test uses a complete small synthetic provenance fixture (one geometry, one line, two trials/position; the required 32 synthetic IDs and eight cost clusters). It runs the real main, summary_provenance and publish functions, reads the published JSON, checks offered status and hand-computed F=4, checks analysis arguments/current selector SHA and validates the hash-bound sidecar. The earlier aggregate-only tests could not catch either publication error.

Python: 80 cases, 79 passed, one existing Git-mutation fixture skipped, zero failures; v4: 15 passed. check-catalog --discover: 176 units, zero incomplete descriptions. VV.Tests received only the authorized reviewed v4 entry and generated discovery update. No C# change or .NET build/test was needed.

The selector is not in the frozen executor file map or any v4 recipe/rule/manifest pin. It was captured in historical source snapshots, and its earlier Turn6 hash remains a record, not an execution pin. Upon publication the analysis will bind the repaired selector hash; original run provenance is preserved, including resumed-run source records. New selector SHA: 0e6530ec5cdc1e49b46da2f427372cb24e8c33d2064389fb68d4cb33a370b510. New test SHA: 7137e463abbd9fb7c3b7816a53b60a9048613086dae70712cfafc358d292c429.

### Offers and every calibration failure

All energies are keV; High denotes High-res. Successful calibration is not a performance claim. High/662 at the fixed 32k budget succeeds on 31/32 but is refused. Its failed seed is outside the predeclared eight imaging seeds, so its cost table is complete while its all-32 diagnostics are unavailable. Wide/662 fails on four seeds, two inside the imaging subset; no survivor-only Anger cost or diagnostic interval is reported. Every 122 keV calibration fails. Other offers all succeed on 32/32; zero observed failures in 32 independent seeds has a nominal one-sided 95% binomial upper failure-probability bound of 8.94%, not zero.

| Preset | Line | Decision | Calibration passes /32 | Failures |
|---|---|---|---|---|
| Baseline | 122.06 | refused | 0 | 32 |
| Baseline | 661.7 | offered (experimental) | 32 | 0 |
| Baseline | 1332.5 | offered (experimental) | 32 | 0 |
| Sharp | 122.06 | refused | 0 | 32 |
| Sharp | 661.7 | offered (experimental) | 32 | 0 |
| Sharp | 1332.5 | offered (experimental) | 32 | 0 |
| Wide | 122.06 | refused | 0 | 32 |
| Wide | 661.7 | refused | 28 | 4 |
| Wide | 1332.5 | offered (experimental) | 32 | 0 |
| High | 122.06 | refused | 0 | 32 |
| High | 661.7 | refused | 31 | 1 |
| High | 1332.5 | offered (experimental) | 32 | 0 |

The following literal messages retain every failed seed (including repeated messages):

| Preset | Line | Seed | Measured failure |
|---|---|---|---|
| Baseline | 122.06 | 43000001 | flood peaks cannot be ordered into a 12 × 12 grid (column 6, rows 0/1) |
| Baseline | 122.06 | 43104730 | flood peaks cannot be ordered into a 12 × 12 grid (row 4, columns 3/4) |
| Baseline | 122.06 | 43209459 | flood peaks do not form a regular 12 × 12 grid (row 0, columns 1/2) |
| Baseline | 122.06 | 43314188 | flood peaks cannot be ordered into a 12 × 12 grid (row 1, columns 5/6) |
| Baseline | 122.06 | 43418917 | flood peaks do not form a regular 12 × 12 grid (row 0, columns 2/3) |
| Baseline | 122.06 | 43523646 | flood peaks cannot be ordered into a 12 × 12 grid (row 0, columns 5/6) |
| Baseline | 122.06 | 43628375 | flood peaks cannot be ordered into a 12 × 12 grid (row 7, columns 0/1) |
| Baseline | 122.06 | 43733104 | flood peaks cannot be ordered into a 12 × 12 grid (row 0, columns 7/8) |
| Baseline | 122.06 | 43837833 | flood peaks cannot be ordered into a 12 × 12 grid (row 0, columns 4/5) |
| Baseline | 122.06 | 43942562 | flood peaks cannot be ordered into a 12 × 12 grid (row 10, columns 5/6) |
| Baseline | 122.06 | 44047291 | flood peaks do not form a regular 12 × 12 grid (row 0, columns 3/4) |
| Baseline | 122.06 | 44152020 | flood peaks cannot be ordered into a 12 × 12 grid (row 2, columns 4/5) |
| Baseline | 122.06 | 44256749 | flood peaks cannot be ordered into a 12 × 12 grid (column 2, rows 1/2) |
| Baseline | 122.06 | 44361478 | flood peaks cannot be ordered into a 12 × 12 grid (row 2, columns 7/8) |
| Baseline | 122.06 | 44466207 | flood peaks cannot be ordered into a 12 × 12 grid (row 0, columns 10/11) |
| Baseline | 122.06 | 44570936 | flood peaks cannot be ordered into a 12 × 12 grid (row 4, columns 6/7) |
| Baseline | 122.06 | 44675665 | flood peaks cannot be ordered into a 12 × 12 grid (row 6, columns 3/4) |
| Baseline | 122.06 | 44780394 | flood peaks cannot be ordered into a 12 × 12 grid (row 10, columns 8/9) |
| Baseline | 122.06 | 44885123 | flood peaks do not form a regular 12 × 12 grid (row 0, columns 0/1) |
| Baseline | 122.06 | 44989852 | flood peaks cannot be ordered into a 12 × 12 grid (row 11, columns 0/1) |
| Baseline | 122.06 | 45094581 | flood peaks cannot be ordered into a 12 × 12 grid (row 0, columns 5/6) |
| Baseline | 122.06 | 45199310 | flood peaks do not form a regular 12 × 12 grid (row 0, columns 0/1) |
| Baseline | 122.06 | 45304039 | flood peaks cannot be ordered into a 12 × 12 grid (row 0, columns 7/8) |
| Baseline | 122.06 | 45408768 | flood peaks do not form a regular 12 × 12 grid (row 0, columns 0/1) |
| Baseline | 122.06 | 45513497 | flood peaks cannot be ordered into a 12 × 12 grid (column 7, rows 2/3) |
| Baseline | 122.06 | 45618226 | flood peaks cannot be ordered into a 12 × 12 grid (row 2, columns 6/7) |
| Baseline | 122.06 | 45722955 | flood peaks cannot be ordered into a 12 × 12 grid (column 5, rows 1/2) |
| Baseline | 122.06 | 45827684 | flood peaks cannot be ordered into a 12 × 12 grid (row 10, columns 10/11) |
| Baseline | 122.06 | 45932413 | flood peaks cannot be ordered into a 12 × 12 grid (row 4, columns 2/3) |
| Baseline | 122.06 | 46037142 | flood peaks cannot be ordered into a 12 × 12 grid (row 3, columns 10/11) |
| Baseline | 122.06 | 46141871 | flood peaks cannot be ordered into a 12 × 12 grid (row 2, columns 8/9) |
| Baseline | 122.06 | 46246600 | flood peaks cannot be ordered into a 12 × 12 grid (row 11, columns 6/7) |
| Sharp | 122.06 | 43000001 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 43104730 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 43209459 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 43314188 | measured lattice spans 1 × 2 with 2 maxima, expected 30 × 30 |
| Sharp | 122.06 | 43418917 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 43523646 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 43628375 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 43733104 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 43837833 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 43942562 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 44047291 | measured lattice spans 2 × 1 with 2 maxima, expected 30 × 30 |
| Sharp | 122.06 | 44152020 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 44256749 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 44361478 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 44466207 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 44570936 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 44675665 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 44780394 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 44885123 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 44989852 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 45094581 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 45199310 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 45304039 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 45408768 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 45513497 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 45618226 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 45722955 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 45827684 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 45932413 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 46037142 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 46141871 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Sharp | 122.06 | 46246600 | measured lattice spans 1 × 1 with 1 maxima, expected 30 × 30 |
| Wide | 122.06 | 43000001 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 43104730 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 43209459 | measured lattice spans 2 × 1 with 2 maxima, expected 34 × 34 |
| Wide | 122.06 | 43314188 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 43418917 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 43523646 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 43628375 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 43733104 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 43837833 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 43942562 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 44047291 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 44152020 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 44256749 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 44361478 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 44466207 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 44570936 | measured lattice spans 2 × 1 with 2 maxima, expected 34 × 34 |
| Wide | 122.06 | 44675665 | measured lattice spans 2 × 1 with 2 maxima, expected 34 × 34 |
| Wide | 122.06 | 44780394 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 44885123 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 44989852 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 45094581 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 45199310 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 45304039 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 45408768 | measured lattice spans 1 × 2 with 2 maxima, expected 34 × 34 |
| Wide | 122.06 | 45513497 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 45618226 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 45722955 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 45827684 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 45932413 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 46037142 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 46141871 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 122.06 | 46246600 | measured lattice spans 1 × 1 with 1 maxima, expected 34 × 34 |
| Wide | 661.7 | 43523646 | measured lattice spans 34 × 34 with 1155 maxima, expected 34 × 34; unsupported slots (0, 17) |
| Wide | 661.7 | 43628375 | measured lattice spans 34 × 34 with 1155 maxima, expected 34 × 34; unsupported slots (1, 11) |
| Wide | 661.7 | 45513497 | measured lattice spans 34 × 34 with 1155 maxima, expected 34 × 34; unsupported slots (33, 16) |
| Wide | 661.7 | 45722955 | no significant valley between lattice neighbours (0, 17) and (1, 17) |
| High | 122.06 | 43000001 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 43104730 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 43209459 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 43314188 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 43418917 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 43523646 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 43628375 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 43733104 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 43837833 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 43942562 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 44047291 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 44152020 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 44256749 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 44361478 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 44466207 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 44570936 | measured lattice spans 2 × 1 with 2 maxima, expected 44 × 44 |
| High | 122.06 | 44675665 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 44780394 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 44885123 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 44989852 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 45094581 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 45199310 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 45304039 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 45408768 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 45513497 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 45618226 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 45722955 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 45827684 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 45932413 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 46037142 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 46141871 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 122.06 | 46246600 | measured lattice spans 1 × 1 with 1 maxima, expected 44 × 44 |
| High | 661.7 | 46141871 | measured lattice spans 44 × 44 with 1935 maxima, expected 44 × 44; unsupported slots (0, 17) |

### Imaging costs — eight fixed outer seeds

Values are estimate [nominal 95% seed-cluster lower, upper]. RMS and bias are degrees; remote probability is percent. Bias is the RMS of per-position mean angular-error vectors, estimated from 64 trials/position. Remote means error greater than one nominal mask element (cell/D radians); all remote errors remain in RMS. Nine declared in-FCFOV positions, 1 m, finite-mask cross-correlation with Tent interpolation, each path's own isolated flood correction; 1,000/4,000 are expected ideal Direct counts at the shared emitted fluence, not guaranteed accepted Anger counts. F is the paired equal-fluence MSE ratio, not a universal measurement-time multiplier. Intervals are Student cluster approximations; a printed 0 [0,0] remote interval means no observed peaks and does not prove zero risk. Full-precision values and accepted-count intervals are in the JSON.

Direct reference below applies identically to isolated and 100,000 input hits/s cells. It has no physical electronics pile-up; its photons/counts/errors are paired across rates. F_Direct is 1 by definition.

| Preset | Line | Direct exposure | RMS deg | Bias deg | Remote % | F_Direct |
|---|---|---|---|---|---|---|
| Baseline | 122.06 | 1000 | 0.1072 [0.1071, 0.1073] | 0.1027 [0.1025, 0.1028] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Baseline | 122.06 | 4000 | 0.1022 [0.1022, 0.1023] | 0.1009 [0.1009, 0.1009] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Baseline | 661.7 | 1000 | 0.1636 [0.1632, 0.1641] | 0.1524 [0.1523, 0.1525] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Baseline | 661.7 | 4000 | 0.1550 [0.1547, 0.1553] | 0.1523 [0.1523, 0.1524] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Baseline | 1332.5 | 1000 | 1.6519 [1.6421, 1.6617] | 1.2195 [1.2024, 1.2365] | 24.5877 [24.3086, 24.8667] | 1 [1, 1] |
| Baseline | 1332.5 | 4000 | 1.4169 [1.4145, 1.4193] | 1.2191 [1.2171, 1.2210] | 18.7934 [18.6908, 18.8960] | 1 [1, 1] |
| Sharp | 122.06 | 1000 | 0.0276 [0.0276, 0.0277] | 0.0214 [0.0214, 0.0215] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Sharp | 122.06 | 4000 | 0.0230 [0.0230, 0.0230] | 0.0213 [0.0213, 0.0213] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Sharp | 661.7 | 1000 | 0.0776 [0.0774, 0.0778] | 0.0649 [0.0647, 0.0651] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Sharp | 661.7 | 4000 | 0.0673 [0.0673, 0.0673] | 0.0641 [0.0641, 0.0641] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Sharp | 1332.5 | 1000 | 0.1035 [0.1034, 0.1037] | 0.0732 [0.0730, 0.0735] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Sharp | 1332.5 | 4000 | 0.0877 [0.0875, 0.0879] | 0.0757 [0.0755, 0.0759] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Wide | 122.06 | 1000 | 0.1132 [0.1132, 0.1132] | 0.1121 [0.1121, 0.1121] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Wide | 122.06 | 4000 | 0.1118 [0.1118, 0.1118] | 0.1116 [0.1116, 0.1116] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Wide | 661.7 | 1000 | 0.5074 [0.5068, 0.5080] | 0.4930 [0.4921, 0.4939] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Wide | 661.7 | 4000 | 0.5168 [0.5166, 0.5170] | 0.5114 [0.5112, 0.5116] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| Wide | 1332.5 | 1000 | 0.6248 [0.5248, 0.7109] | 0.4651 [0.4562, 0.4739] | 0.2604 [0.1828, 0.3380] | 1 [1, 1] |
| Wide | 1332.5 | 4000 | 0.5208 [0.5203, 0.5212] | 0.5035 [0.5029, 0.5041] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| High | 122.06 | 1000 | 0.0158 [0.0158, 0.0158] | 0.0141 [0.0141, 0.0141] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| High | 122.06 | 4000 | 0.0147 [0.0147, 0.0147] | 0.0142 [0.0142, 0.0143] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| High | 661.7 | 1000 | 0.0364 [0.0363, 0.0364] | 0.0319 [0.0318, 0.0319] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| High | 661.7 | 4000 | 0.0320 [0.0320, 0.0320] | 0.0308 [0.0308, 0.0308] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| High | 1332.5 | 1000 | 0.0488 [0.0488, 0.0488] | 0.0364 [0.0363, 0.0365] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |
| High | 1332.5 | 4000 | 0.0417 [0.0417, 0.0418] | 0.0376 [0.0376, 0.0376] | 0.0000 [0.0000, 0.0000] | 1 [1, 1] |

Anger follows; unavailable means the complete fixed seed population cannot support an estimate, not zero error. Refused cells retain any complete measured cost population without gaining offer eligibility.

| Preset | Line | Direct exposure | Rate cps | RMS deg | Bias deg | Remote % | F_Anger |
|---|---|---|---|---|---|---|---|
| Baseline | 122.06 | 1000 | isolated | unavailable | unavailable | unavailable | unavailable |
| Baseline | 122.06 | 1000 | 100,000 | unavailable | unavailable | unavailable | unavailable |
| Baseline | 122.06 | 4000 | isolated | unavailable | unavailable | unavailable | unavailable |
| Baseline | 122.06 | 4000 | 100,000 | unavailable | unavailable | unavailable | unavailable |
| Baseline | 661.7 | 1000 | isolated | 0.6258 [0.5750, 0.6728] | 0.2964 [0.2667, 0.3234] | 5.0781 [4.5147, 5.6416] | 14.629 [12.530, 17.080] |
| Baseline | 661.7 | 1000 | 100,000 | 0.6804 [0.6381, 0.7202] | 0.3075 [0.2786, 0.3339] | 5.8811 [5.0920, 6.6701] | 17.292 [15.366, 19.460] |
| Baseline | 661.7 | 4000 | isolated | 0.1999 [0.1762, 0.2211] | 0.1583 [0.1575, 0.1590] | 0.2387 [0.0848, 0.3927] | 1.664 [1.334, 2.074] |
| Baseline | 661.7 | 4000 | 100,000 | 0.2021 [0.1639, 0.2341] | 0.1598 [0.1577, 0.1618] | 0.2604 [0.0031, 0.5177] | 1.699 [1.210, 2.387] |
| Baseline | 1332.5 | 1000 | isolated | 2.0914 [2.0571, 2.1251] | 1.7960 [1.7589, 1.8323] | 45.3993 [44.6235, 46.1751] | 1.603 [1.553, 1.654] |
| Baseline | 1332.5 | 1000 | 100,000 | 2.1175 [2.0932, 2.1415] | 1.8013 [1.7597, 1.8419] | 46.6146 [45.3830, 47.8462] | 1.643 [1.604, 1.684] |
| Baseline | 1332.5 | 4000 | isolated | 1.9713 [1.9553, 1.9872] | 1.8751 [1.8625, 1.8877] | 42.2092 [41.6805, 42.7379] | 1.936 [1.907, 1.964] |
| Baseline | 1332.5 | 4000 | 100,000 | 1.9838 [1.9622, 2.0051] | 1.8807 [1.8604, 1.9008] | 42.5564 [41.7338, 43.3791] | 1.960 [1.918, 2.003] |
| Sharp | 122.06 | 1000 | isolated | unavailable | unavailable | unavailable | unavailable |
| Sharp | 122.06 | 1000 | 100,000 | unavailable | unavailable | unavailable | unavailable |
| Sharp | 122.06 | 4000 | isolated | unavailable | unavailable | unavailable | unavailable |
| Sharp | 122.06 | 4000 | 100,000 | unavailable | unavailable | unavailable | unavailable |
| Sharp | 661.7 | 1000 | isolated | 0.1045 [0.1034, 0.1055] | 0.0689 [0.0678, 0.0700] | 0.0000 [0.0000, 0.0000] | 1.813 [1.777, 1.850] |
| Sharp | 661.7 | 1000 | 100,000 | 0.1185 [0.0922, 0.1399] | 0.0703 [0.0689, 0.0717] | 0.0217 [0.0000, 0.0730] | 2.331 [1.570, 3.462] |
| Sharp | 661.7 | 4000 | isolated | 0.0960 [0.0952, 0.0968] | 0.0751 [0.0739, 0.0762] | 0.0000 [0.0000, 0.0000] | 2.034 [2.001, 2.067] |
| Sharp | 661.7 | 4000 | 100,000 | 0.0976 [0.0966, 0.0987] | 0.0760 [0.0750, 0.0770] | 0.0000 [0.0000, 0.0000] | 2.104 [2.059, 2.150] |
| Sharp | 1332.5 | 1000 | isolated | 1.1368 [1.0979, 1.1744] | 0.5671 [0.5198, 0.6107] | 13.2595 [12.4206, 14.0985] | 120.538 [112.747, 128.869] |
| Sharp | 1332.5 | 1000 | 100,000 | 1.2194 [1.1835, 1.2542] | 0.6380 [0.6102, 0.6647] | 16.3628 [15.6614, 17.0643] | 138.682 [130.924, 146.899] |
| Sharp | 1332.5 | 4000 | isolated | 0.1961 [0.1461, 0.2358] | 0.0903 [0.0870, 0.0934] | 0.2604 [0.0704, 0.4505] | 5.004 [3.201, 7.822] |
| Sharp | 1332.5 | 4000 | 100,000 | 0.2122 [0.1773, 0.2422] | 0.0911 [0.0893, 0.0927] | 0.3906 [0.2620, 0.5193] | 5.860 [4.333, 7.926] |
| Wide | 122.06 | 1000 | isolated | unavailable | unavailable | unavailable | unavailable |
| Wide | 122.06 | 1000 | 100,000 | unavailable | unavailable | unavailable | unavailable |
| Wide | 122.06 | 4000 | isolated | unavailable | unavailable | unavailable | unavailable |
| Wide | 122.06 | 4000 | 100,000 | unavailable | unavailable | unavailable | unavailable |
| Wide | 661.7 | 1000 | isolated | unavailable | unavailable | unavailable | unavailable |
| Wide | 661.7 | 1000 | 100,000 | unavailable | unavailable | unavailable | unavailable |
| Wide | 661.7 | 4000 | isolated | unavailable | unavailable | unavailable | unavailable |
| Wide | 661.7 | 4000 | 100,000 | unavailable | unavailable | unavailable | unavailable |
| Wide | 1332.5 | 1000 | isolated | 5.3598 [5.2485, 5.4689] | 2.9017 [2.7744, 3.0236] | 30.6858 [30.0332, 31.3383] | 73.580 [54.172, 99.942] |
| Wide | 1332.5 | 1000 | 100,000 | 5.9126 [5.7965, 6.0265] | 3.4744 [3.3171, 3.6249] | 35.6988 [34.6581, 36.7395] | 89.540 [67.087, 119.509] |
| Wide | 1332.5 | 4000 | isolated | 1.6027 [1.4301, 1.7584] | 0.5456 [0.4504, 0.6264] | 3.8194 [3.2876, 4.3513] | 9.471 [7.729, 11.607] |
| Wide | 1332.5 | 4000 | 100,000 | 1.9892 [1.8259, 2.1400] | 0.6603 [0.5723, 0.7379] | 5.3819 [4.7711, 5.9928] | 14.590 [12.469, 17.072] |
| High | 122.06 | 1000 | isolated | unavailable | unavailable | unavailable | unavailable |
| High | 122.06 | 1000 | 100,000 | unavailable | unavailable | unavailable | unavailable |
| High | 122.06 | 4000 | isolated | unavailable | unavailable | unavailable | unavailable |
| High | 122.06 | 4000 | 100,000 | unavailable | unavailable | unavailable | unavailable |
| High | 661.7 | 1000 | isolated | 0.0662 [0.0612, 0.0709] | 0.0364 [0.0359, 0.0369] | 0.0651 [0.0000, 0.1402] | 3.316 [2.862, 3.841] |
| High | 661.7 | 1000 | 100,000 | 0.0793 [0.0630, 0.0928] | 0.0361 [0.0354, 0.0367] | 0.1736 [0.0639, 0.2833] | 4.753 [3.283, 6.881] |
| High | 661.7 | 4000 | isolated | 0.0514 [0.0512, 0.0516] | 0.0372 [0.0367, 0.0376] | 0.0000 [0.0000, 0.0000] | 2.582 [2.563, 2.601] |
| High | 661.7 | 4000 | 100,000 | 0.0526 [0.0524, 0.0529] | 0.0373 [0.0371, 0.0376] | 0.0000 [0.0000, 0.0000] | 2.712 [2.683, 2.742] |
| High | 1332.5 | 1000 | isolated | 0.5114 [0.4848, 0.5367] | 0.1716 [0.1540, 0.1876] | 11.2196 [10.2633, 12.1759] | 109.777 [99.191, 121.493] |
| High | 1332.5 | 1000 | 100,000 | 0.5962 [0.5792, 0.6128] | 0.2152 [0.2011, 0.2285] | 15.2778 [14.2369, 16.3186] | 149.211 [141.069, 157.822] |
| High | 1332.5 | 4000 | isolated | 0.0682 [0.0674, 0.0689] | 0.0461 [0.0453, 0.0469] | 0.0000 [0.0000, 0.0000] | 2.669 [2.610, 2.729] |
| High | 1332.5 | 4000 | 100,000 | 0.0707 [0.0695, 0.0718] | 0.0467 [0.0460, 0.0475] | 0.0000 [0.0000, 0.0000] | 2.867 [2.775, 2.961] |

### Crystal diagnostics — all 32 outer seeds

Values are estimate [95% seed-cluster lower, upper]. G is paired RMS_Anger^2 minus RMS_direct^2 against first-interaction truth, per axis, displayed in units of 10^-6 rad^2; negative favours Anger in that crystal-level comparison. Single mis-ID and multi-crystal share are percent of their declared accepted populations. Weakest/median sensitivity comes from the isolated held-out flood and is repeated beside rate diagnostics; it is not a rate sensitivity map. All maps remain in the JSON. Calibration failures leave diagnostics unavailable for the full ensemble; no 28/31-seed survivor statistic replaces the requested 32-seed statistic.

| Preset | Line | Rate cps | Gx (10^-6 rad^2) | Gy (10^-6 rad^2) | Single mis-ID % | Weakest/median | Multi-crystal % |
|---|---|---|---|---|---|---|---|
| Baseline | 122.06 | isolated | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters |
| Baseline | 122.06 | 100,000 | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters |
| Baseline | 661.7 | isolated | -33.977 [-34.247, -33.707] | -37.162 [-37.291, -37.033] | 0.0172 [0.0153, 0.0192] | 0.6695 [0.6596, 0.6793] | 50.7942 [50.7929, 50.7956] |
| Baseline | 661.7 | 100,000 | -17.099 [-19.728, -14.470] | -19.002 [-21.406, -16.598] | 1.4186 [1.3119, 1.5253] | 0.6695 [0.6596, 0.6793] | 51.0312 [50.9317, 51.1306] |
| Baseline | 1332.5 | isolated | 70.559 [70.247, 70.871] | 69.477 [69.134, 69.820] | 0.0000 [0.0000, 0.0000] | 0.6765 [0.6677, 0.6853] | 61.8643 [61.7972, 61.9315] |
| Baseline | 1332.5 | 100,000 | 92.558 [89.924, 95.191] | 91.590 [89.095, 94.085] | 3.3156 [3.0844, 3.5467] | 0.6765 [0.6677, 0.6853] | 60.7513 [60.5480, 60.9547] |
| Sharp | 122.06 | isolated | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters |
| Sharp | 122.06 | 100,000 | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters |
| Sharp | 661.7 | isolated | -26.294 [-26.324, -26.265] | -26.867 [-26.894, -26.840] | 0.6531 [0.6482, 0.6579] | 0.6218 [0.6193, 0.6243] | 58.9893 [58.9888, 58.9897] |
| Sharp | 661.7 | 100,000 | -7.555 [-9.584, -5.525] | -3.369 [-5.818, -0.920] | 2.8993 [2.7302, 3.0684] | 0.6218 [0.6193, 0.6243] | 59.8385 [59.7369, 59.9401] |
| Sharp | 1332.5 | isolated | 85.049 [85.015, 85.083] | 85.708 [85.669, 85.747] | 0.0003 [0.0002, 0.0004] | 0.5638 [0.5601, 0.5676] | 71.9348 [71.9256, 71.9440] |
| Sharp | 1332.5 | 100,000 | 80.908 [79.059, 82.757] | 85.840 [83.197, 88.483] | 4.2763 [3.9159, 4.6366] | 0.5638 [0.5601, 0.5676] | 70.4739 [70.3162, 70.6316] |
| Wide | 122.06 | isolated | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters |
| Wide | 122.06 | 100,000 | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters |
| Wide | 661.7 | isolated | unavailable; 4 missing clusters | unavailable; 4 missing clusters | unavailable; 4 missing clusters | unavailable; 4 missing clusters | unavailable; 4 missing clusters |
| Wide | 661.7 | 100,000 | unavailable; 4 missing clusters | unavailable; 4 missing clusters | unavailable; 4 missing clusters | unavailable; 4 missing clusters | unavailable; 4 missing clusters |
| Wide | 1332.5 | isolated | 396.614 [396.464, 396.764] | 402.074 [401.969, 402.179] | 0.0022 [0.0018, 0.0025] | 0.5437 [0.5380, 0.5494] | 74.2894 [74.2820, 74.2967] |
| Wide | 1332.5 | 100,000 | 462.986 [456.338, 469.634] | 485.940 [478.354, 493.526] | 3.8500 [3.6147, 4.0854] | 0.5437 [0.5380, 0.5494] | 73.6798 [73.5177, 73.8419] |
| High | 122.06 | isolated | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters |
| High | 122.06 | 100,000 | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters | unavailable; 32 missing clusters |
| High | 661.7 | isolated | unavailable; 1 missing clusters | unavailable; 1 missing clusters | unavailable; 1 missing clusters | unavailable; 1 missing clusters | unavailable; 1 missing clusters |
| High | 661.7 | 100,000 | unavailable; 1 missing clusters | unavailable; 1 missing clusters | unavailable; 1 missing clusters | unavailable; 1 missing clusters | unavailable; 1 missing clusters |
| High | 1332.5 | isolated | 51.277 [51.259, 51.295] | 51.784 [51.770, 51.798] | 0.0753 [0.0740, 0.0766] | 0.5323 [0.5278, 0.5368] | 72.2117 [72.2066, 72.2168] |
| High | 1332.5 | 100,000 | 61.605 [60.446, 62.764] | 59.248 [58.147, 60.350] | 4.8934 [4.5168, 5.2700] | 0.5323 [0.5278, 0.5368] | 72.4209 [72.2758, 72.5661] |

### Development comparison and surprises

The fresh independent-history DEV4 anchors agree with confirmation: Baseline/662 at 1,000 isolated counts changes from RMS_A/D 0.6529/0.1634 degrees, B_A/D 0.2917/0.1524, F 15.968 [13.172,19.358] to 0.6258/0.1636, 0.2964/0.1524, F 14.629 [12.530,17.080]. Sharp/1332 changes from 1.1309/0.1034, 0.5558/0.0732, F 119.514 [102.410,139.474] to 1.1368/0.1035, 0.5671/0.0732, F 120.538 [112.747, 128.869] (use the full-precision table interval above). These are independent outer families, with fresh histories; development and confirmation agree in the separate population-cost regime identified by the discrepancy check.

Earlier conditional history-pool rows are not like-for-like confirmation baselines. Sharp/1332's old 4,000-count conditional F=119.640 is superseded for population interpretation by the fresh confirmation F=5.004; Wide/1332's old conditional F=51.171 becomes 9.471. Such large shifts support the recorded warning about a noisy reused empirical mean shadow. No old exploration rows are deleted. High/1332 now succeeds on all32 whereas earlier DEV4 at 2k had one failure; neither small development set nor this confirmation proves universal calibration success. High/662's four development 32k successes did not predict an all-32 pass: its single missing maximum correctly causes refusal. Wide/662 continues to be fragile at the unchanged 2k budget. No budget/tolerance is repaired after these results.

Exposure matters strongly: isolated F at 1,000 -> 4,000 counts is 14.629 -> 1.664 for Baseline/662, 120.538 -> 5.004 for Sharp/1332, 73.580 -> 9.471 for Wide/1332 and 109.777 -> 2.669 for High/1332. This is competing-peak suppression plus geometry-dependent bias, not a smooth 1/sqrt(counts) law. At 1,000 counts the rate cell increases these F values to 17.292, 138.682, 89.540 and 149.211 respectively, consistent with pile-up/acceptance loss; point differences at 4,000 can be small or have overlapping intervals, so they are not claimed statistically significant.

Baseline/1332 is a warning against quoting F alone: at 1,000 counts F is only 1.603, yet Direct RMS is 1.652 degrees and Anger RMS 2.091 degrees, with remote shares 24.59% and 45.40%. At 4,000 both remain poor (1.417/1.971 degrees). Sharp/662 has modest F but accepted multi-crystal share about59%; favourable paired G does not establish a favourable source cost. Absolute errors, bias and remote probability must stay beside F. Fine heads show very large F at 1332 because their ideal reference errors are small; High/1332's 1,000-count Anger RMS is 0.511 degrees despite F near110. Descriptive costs do not reverse any calibration-only offer.

### Proposed record wording — confirmed update, not applied

The proposed theme69, EV-36 and LIM-11 paragraphs above now use the confirmation record and exact offer list. Theme68's near-field centred equal-count scope note and proposed SR-DET-05/SR-RUN-34/SR-DET-10/SR-DET-11/PD-4 wording remain proposed; their requirements do not change with these numerical costs. Custom inheritance still cannot assert source costs when mask/D/decoder differ. Migration-aware reconstruction remains the proposed Backlog item. No Findings, evidence, limitation, requirement, decision or plan record was edited.

### Short-turn write-command inventory

1. apply_patch edits only studio_readout_v4_select.py and tests/test_studio_readout_v4.py: the two API repairs and complete main-publication regression fixture.
2. python -B - (logged stdin catalog script) edits the authorized VV.Tests v4 metadata/source fingerprint and oracle text; it removes duplicate oracle sentences. python -B samples/testing/test_records.py check-catalog --update-discovery updates its discovery block. The subsequent --discover check is read-only.
3. python -B -m unittest discover -s samples/evidence/tests -p test_studio_readout_v4.py and the full discover command write retained synthetic test fixtures only under GCAM_PROVENANCE_TEST_ROOT, set process-locally to the task's short-v4-tests directory; bytecode and Git-mutation fixtures are disabled.
4. New-Item creates the task's explore/v4-short scratch directory; apply_patch creates report_confirmation.py there. python -B that script updates this Turn6 file's proposed wording and appends only this Confirmation section, preserving earlier tables/hashes as historical records; a follow-up apply_patch clarifies the G display unit. No local paths/names enter the document.
5. The exact aggregation-only command from the prepared script (expanded output-root variable): python -B samples/evidence/studio_readout_v4_select.py --runs <task-scratch>/confirm32-v4-20261009 --manifest samples/evidence/manifest-studio-readout-v4.json --phase confirm --out <task-scratch>/confirm32-v4-20261009/confirmation-v4.json. This command is prepared but has not been executed pending the output-scope clarification: the publisher writes the new summary and its mandatory hash-bound .provenance.json sidecar. No run/stdout/done/snapshot files are touched. No confirmation transport, cleanup, rerun, Git mutation, deletion, process stop, install or desktop action occurs.

### APPROVAL REQUESTS

Output-scope clarification is pending: the exact aggregation command necessarily writes both confirmation-v4.json and confirmation-v4.json.provenance.json. The author restricted writes in the run root to the former. Authorize the companion sidecar before executing the command; no existing run file is modified or destroyed. All numerical tables above come from the validated aggregate in memory; publication has not occurred.

Exact command, from the working-tree root:

```powershell
$confirmationRoot = Join-Path $env:TEMP 'gcam-todo41/confirm32-v4-20261009'
python -B samples/evidence/studio_readout_v4_select.py --runs $confirmationRoot --manifest samples/evidence/manifest-studio-readout-v4.json --phase confirm --out (Join-Path $confirmationRoot 'confirmation-v4.json')
```

Reason: the standard provenance publisher binds the summary to its output hash through the companion sidecar. Both summary destinations were absent when checked. Destroys: nothing; completed run files are read only. Undo: original runs require no restoration; removing the two new summary files would need separate deletion approval under the guard.


## Repair (v5)

The author declared a final, separate REPAIR32 judgement for Wide FOV/661.7 and High-res/661.7, after v4 refused them on 4/32 at 2k and 1/32 at 32k respectively. Every other cell retains its published v4 verdict. The author reports that the planner has now published the v4 summary/sidecar and is independently replaying one seed per family. This turn does not read, edit or launch anything in either planner output root. The earlier Confirmation section's publication-pending note is a historical turn state.

### Pre-stated budget selection and observations

Before running a calibration, the declaration fixed Wide candidates at 8k then 32k (32k only if any DEV4 8 k failure), and High at 64 k. Select the smallest candidate that succeeds on all four T41_DEV4 seeds; otherwise stop without a frozen repair candidate. No LUT method, optical/photon budget, smoothing, peak fraction, search radius, valley significance, energy window, detector/readout geometry or other calibration constant changes. Scored flood size is n^2 times the stated histories/crystal, produced one history at a time. No source imaging is used to choose a budget. The four DEV4 IDs remain the existing family; REPAIR32 and all other seed files/calibration pins are unchanged.

The v4 failure messages retained in the Confirmation section show three Wide cases missing a single lattice slot and one failing a valley test; High misses one edge slot. LatticeGrowth v2 uses fixed sigma=BinsPerCrystal/8, search radius 0.4 nominal step and three-sigma valley significance. For a resolved density pair, increasing histories by a factor k scales mean contrast by k and counting uncertainty by sqrt(k), improving significance by sqrt(k): nominally 2x for Wide 2k to 8k and sqrt(2) for High 32k to 64k. These development results are consistent with a count-limited failure mode, but do not prove that all fresh repair calibrations will succeed. A true merged peak or persistent geometric/search failure does not improve by this argument. The measurements below test the proposed statistical explanation rather than changing a threshold.

| Cell | DEV seed | Histories/crystal | Result | Markers | Windowed | Build s | Calibration s | Peak working GiB | Peak paged GiB | Failure |
|---|---|---|---|---|---|---|---|---|---|---|
| High | 41000001 | 64000 | success | 1936 | 43447895 | 79.23 | 2394.83 | 5.381 | 6.170 | none |
| High | 41104730 | 64000 | success | 1936 | 43447204 | 75.76 | 2317.17 | 5.333 | 6.142 | none |
| High | 41209459 | 64000 | success | 1936 | 43447666 | 71.89 | 2206.07 | 5.381 | 6.163 | none |
| High | 41314188 | 64000 | success | 1936 | 43447612 | 71.46 | 2229.67 | 5.379 | 6.166 | none |
| Wide | 41000001 | 8000 | success | 1156 | 3515543 | 43.35 | 112.69 | 0.531 | 0.541 | none |
| Wide | 41104730 | 8000 | success | 1156 | 3515503 | 42.47 | 118.94 | 0.532 | 0.542 | none |
| Wide | 41209459 | 8000 | success | 1156 | 3515550 | 43.47 | 113.37 | 0.532 | 0.542 | none |
| Wide | 41314188 | 8000 | success | 1156 | 3515583 | 42.40 | 116.40 | 0.528 | 0.538 | none |

Both selected budgets are measured on DEV4 and the new recipe/manifest are frozen.

Risk: zero failures in four development seeds has a nominal one-sided 95% binomial upper failure-probability bound of 52.71%. A conservative two-level Bonferroni bound for Wide is 60.24%. These are descriptive bounds for reused/development-selected data, not independent validation. The earlier confirmation empiricals are 12.5% Wide failure at 2k and 3.125% High failure at 32k. At unchanged failure probabilities, the plug-in chance of all 32 succeeding would be 1.39% and 36.21%, respectively. They motivate the declared budget increase; they cannot be pooled with higher-budget measurements or used as estimates of the repaired failure rate. Four previous High 32 k development successes were compatible with the later 1/32 failure (zero failures in four has probability 0.881 at that plug-in rate). Only fresh REPAIR32 will judge the two new cells. Zero/32 at a fixed new budget would give an individual nominal one-sided 95% failure upper bound of 8.94%; it still does not establish zero risk.

### Versioned definitions, code and pins

The independent probe-v5 copies the v4 imaging and diagnostic calculation blocks byte-for-byte. Changes are version/schema/phase identity, exactly two geometries, exactly 661.7 keV, per-geometry selected calibration budget, fixed line-purpose offset 100 and process memory telemetry. Fixing that offset preserves the v4 physical stream convention despite removing line indices 0 and 2. The decoder helper is byte-identical to v4. No product source hook was needed. The old v4 managed closure remains exactly its 17 pinned files. The six newly built shared engine DLLs have informational version 1.0.0+1d40a41345055c2d7d266e4ea59a755f0c1a6916 versus v4 version 1.0.0+21841247e81d3bfb7fa5ee5886af0e5f36db97a3; their binary hashes differ, so v5 pins its separate rebuilt closure and makes no cross-version binary-identity claim. The source/input audit verifies 525 prior files unchanged, including all earlier pinned v1-v4 evidence sources/data and engine source; DirectCrystal/legacy defaults and Baseline scenario remain untouched.

Rule v5: experimental offer iff all 32 repair calibrations succeed, retaining each failure seed and literal message otherwise. This repair outcome is final for these two cells; other v4 verdicts remain in the v4 record. Missing/corrupt runs or provenance fail closed before publication. RMS, per-position bias, remote probability, accepted counts, F and crystal diagnostics are descriptive and never gates. The first eight T41_REPAIR32 IDs are fixed for costs before repair. Both readouts report absolute source RMS in degrees, per-position bias magnitude, remote-peak probability and F with seed-cluster intervals; Direct F is identically 1. All 32 remain calibration/diagnostic clusters; no survivor-only interval is substituted when a path/calibration is unavailable.

Imaging definitions are exactly v4: nine positions at 0 and +/-0.45 nominal FCFOV half-extent on axes/corners at 1 m, finite-mask correlation with Tent interpolation, each path's own calibration-flood correction; Direct total deposit within +/-15% of the line, and Anger LUT-assigned per-crystal calibrated energy within +/-15% of the line, independent 120k-history pilot fixing exposures of 1,000/4,000 expected Direct counts, 64 fresh trials per position, paired photons for both readouts and across isolated/rate replay. Rate is 100,000 scored input hits/s through the actual pulse processor. Remote means angular error beyond one nominal mask element, cell/D radians (Wide 1.1459 degrees; High 0.2865 degrees). F is paired equal-fluence source MSE ratio with ideal Direct reference 1, not a general time multiplier. Student seed-cluster intervals and paired log-ratio delta intervals remain unchanged. Calibration/diagnostics use all 32 fresh repair seeds and costs the first eight. Seed purpose constants remain unchanged, with 662's line offset 100 explicit.

The preparation helper preflights every DEV4 record and selects only a complete successful level before writing any recipe/manifest. It then pins the budget-evidence record, rule, recipe, driver, complete managed output closure and v4 base input hashes. It refuses to replace an existing frozen v5 recipe/manifest. The synthetic tests exercise this path; their printed fixture hashes are not production pins.

| New versioned file | SHA-256 |
|---|---|
| samples/evidence/manifest-studio-readout-v5.json | 137377a3ad908166d6f47e10d5b07aa9037d5c054a1a79c8a789d70ec80ebdcd |
| samples/evidence/probe-v5/Gcam.EvidenceProbe.csproj | dba497688bb0404cb3caa0bb4a30ffb603a95549238f8cc41494d35896c66648 |
| samples/evidence/probe-v5/ImagingDecoder.cs | ebc68d1f1e0fee394acc954e17ec5defb30b68a8d3ff22081e132beb99470dd9 |
| samples/evidence/probe-v5/Program.cs | 6601861253bed2d74ecf6d3e1656f238d849208f4a6bb9ebdd40b3bd236ed96e |
| samples/evidence/probe-v5/StudioReadoutRecipeV5.cs | 6e38e41f684d1b8135aa337100539468e100a9e3bbc1dd3ada13a37b2a0cc669 |
| samples/evidence/probe-v5-budget/Gcam.ReadoutCalibrationBudget.csproj | ddb7d57b160cb8c619b3d3180c03fcd290a4f02e88e0276690849938ce2acd2e |
| samples/evidence/probe-v5-budget/Program.cs | b880fc5ba3e85a48704d9ed77f9d6b98b2fe3c3938cc78c1de207051ccfc3593 |
| samples/evidence/studio-readout/development-calibration-budget-v5.json | 8da4a2e7fdf524eca658f9ad8fb6b50a839aeb9fb3cb2e19d3c9248821179bda |
| samples/evidence/studio-readout/recipe-v5.json | e2d5f1f3093db0f13e17de7694bf88683944523f592cd56b17d6a9474698c34b |
| samples/evidence/studio-readout/rule-v5.json | 5c088587263cb0cf2830793a783aac7c7c3d3870336a09b8852b0f29dc1a687d |
| samples/evidence/studio_readout_v5_development.ps1 | 6ec104bfb7f85d1c66eb438b82f9c0a0450050f4db84b3413c9ac64ccdb58387 |
| samples/evidence/studio_readout_v5_driver.py | 6655261cec20a649282d18c547be83a41da36cd56d005ce57348fd6fa523ca79 |
| samples/evidence/studio_readout_v5_prepare.py | 03d9f69c5efecbb86daddab57e01515cf6c1d4e72ad400e44271144a4b79531a |
| samples/evidence/studio_readout_v5_repair.ps1 | 358a65ee5828543b00e08d196dc4382b852160ec95885c8fde13de972ccafca9 |
| samples/evidence/studio_readout_v5_select.py | 3b2818bdb9aa2db37190529e3232ca23031cd5b58ea9ab6b19863f6a8ad85e64 |
| samples/evidence/tests/test_studio_readout_v5.py | ba80bacf667a88d945414a81e4964634700b3f1c878d12478a0f13cb74806d4b |

### Verification

V5 selector/preparation tests: 10 passed, including literal offer/refusal, missing/duplicate/corrupt statuses, hand-computed RMS/remote/F intervals, per-position bias, no survivor-only cost, both-cell complete provenance main publication with the real publisher and sidecar, failure outside the eight cost seeds, missing/corrupt run rejection before publication, scope rejection, incomplete development refusing pins, minimum successful budget and complete synthetic freeze preserving v4. Full Python: 90 cases, 89 passed, one existing Git-mutation fixture skipped, zero failures. The ten new v5 methods all pass. Full .NET: 944 cases, 912 passed, 32 opt-in skipped, zero failures; process-local TEMP/TMP under task scratch. Catalog update/discovery both pass with 177 units and zero incomplete descriptions. UI, rendering and extended evidence opt-ins were disabled. No desktop was launched. The serial development/pilot/verification command exited 0 and no command remains running. Calibration_record --check reports four records up to date, no record edited.

Initial v5 probe compilation exposed an inline comment swallowing the migrated energy declaration; it was corrected before any v5 imaging measurement. Scratch budget and portable budget probes build successfully. The first report-generator invocation failed on the machine locale decoding of seeds.json before any report write; explicit UTF-8 reads corrected the scratch helper. No tolerance was loosened.

### Timing, memory and prepared commands

Calibration-critical pilots measure Windows process high-water marks through Process.PeakWorkingSet64 and PeakPagedMemorySize64, with current private/managed observations retained in the raw scratch JSON. The streamed flood avoids storing InteractionSite histories, but ReadoutCalibration.Build still retains its event/window/position lists, so memory cannot be inferred from streaming alone. One worker is the planned repair concurrency. A full High imaging memory/time pilot is not claimed: its largest calibration buffers are measured directly; its additional diagnostic/imaging arrays are estimated, and every completed v5 recipe records its actual process high-water marks for audit.

The full 64-trial Wide development imaging pilot (seed 41000001, 8 k, nine positions, both exposures/rates) took 9.79 min: build 42.53s, calibration 107.47s, diagnostics 40.57s, costs 396.84s. Peak working set 0.526 GiB; peak paged/commit observation 0.537 GiB. It is a timing pilot, not a repair qualification; no repair-family photons were used.

The latest repair instruction explicitly requested complete DEV4 calibration selection. That serial development batch is complete; summed measured numerical calibration, optical-table build and Wide imaging-pilot time is 177.8 minutes, exceeding the earlier roughly 90-minute exploration aim. This longer development batch was stated before starting its remainder; no REPAIR32 run was started. The resumable development command used (valid pilot results reused, corrupt records refused, no --force/delete) was:

```powershell
pwsh -NoProfile -File samples/evidence/studio_readout_v5_development.ps1
```

It completes the predeclared levels on DEV4 and calls studio_readout_v5_prepare.py to create the immutable production recipe/manifest only after both selections succeed. It never starts REPAIR32. The exact full repair command, for the planner to launch as a detached child with the selected budgets and completed freeze, is:

```powershell
pwsh -NoProfile -File samples/evidence/studio_readout_v5_repair.ps1
```

This script has not been started. It uses a fresh task-relative root repair32-v5-20261010, the separate v5 probe, families t41-repair-Wide-v5 and t41-repair-High-v5 on T41_REPAIR32, --jobs 1 and process-local TEMP/TMP. It resumes completed valid per-geometry/per-seed attempts in that root, retains failed attempts, and aggregates to repair-v5.json plus its hash-bound provenance sidecar only after all 64 runs complete. It fails before creating an output root if no frozen v5 manifest exists. No watcher or automatic further repair occurs. Both prepared scripts parse successfully.

Planning estimate at Wide 8 k/High 64 k: about 32.5 hours centre, allow 26-42 hours with one worker. This combines measured calibration/build stages for all 32 per cell, the measured Wide full pilot, and High cost-flood replay scaled from its measured calibration plus 1.8x Wide source/decoder work; contention and GC can widen it. It is a justified scaled estimate, not an observed full High repair run. Wide 8 k succeeded on all four, so the predeclared 32 k fallback was not run; High 64 k succeeded on all four. No other constant changed. The new recipe/manifest are frozen by hash; no claim of a final repair offer or cost is made yet.

### Complete write-command inventory for this repair turn

1. New-Item creates the task's explore/v5 scratch directory. Logged python -B - writes scratch before.json, BudgetV5.csproj and the pre-stated budget-selection.json, deriving the standalone project from the earlier scratch compiler layout; no old project/source is edited.
2. apply_patch creates scratch Main.cs, the independent new v5 selector/tests/preparation/development/repair scripts and the memory telemetry edits to the new probe-v5. Logged python -B - copies only earlier source bytes into new probe-v5/probe-v5-budget paths, creating their project/decoder/Program/calculation files, and creates rule-v5.json. Earlier pinned files remain unchanged.
3. dotnet build the scratch BudgetV5 project; dotnet build the new portable budget probe; three new imaging-probe builds (initial failed, two successful), writing only allowed bin/obj/restore outputs. dotnet build Gcam.sln -c Release also succeeded with two existing xUnit analyzer warnings. The inline-comment compile error is recorded above.
4. One serial pilot command runs scratch BudgetV5.dll with DEV41000001 for Wide 8 k then High 64 k, writing their separate budget JSON records under task scratch. The second serial command runs studio_readout_v5_development.ps1 on the remaining development seeds, then the unpublished Wide timing pilot, dotnet test and catalog update/discovery; all its stages completed with exit 0: remaining DEV4 calibrations, prepare/freeze, the Wide timing pilot, full .NET tests, catalog update and discovery. Successful prepare execution writes the three new production budget-evidence/recipe/manifest pins. No repair/confirmation/selection photon stream is run.
5. Logged Python commands write scratch recipe-pilot-unpublished.json and pilot-closure.json, check byte-identical v4 calculation blocks, and update only authorized VV.Tests entry/method/source metadata and its overview count. The full timing recipe is explicitly unpublished and is not a selected/frozen candidate.
6. V5-only and full Python unittest commands retain synthetic fixtures solely under task-relative v5-tests/provenance, with GCAM_PROVENANCE_TEST_ROOT process-local, bytecode disabled and Git-mutation fixtures off. Preparation's main is also invoked on the incomplete actual scratch set and refuses before any production pin write; synthetic pin outputs are scratch fixtures only.
7. apply_patch and logged python -B - edits create/update this scratch report_v5.py; python -B it appends only this Repair(v5) section and verifies the protected source/input audit. Any later verification addendum uses the same authorized Turn6 file. No local absolute path/name is written into the document.

No old v4 verdict, root, summary, sidecar, source, rule, recipe or manifest is altered; no product source change, Git mutation, deletion, process stop, install, desktop or upload. No process from this turn is left running when handed back.

### Completed command details

The calibration-only development batch used studio_readout_v5_development.ps1. After complete selection it invoked:

```powershell
python -B samples/evidence/studio_readout_v5_prepare.py --development $developmentResultRoot
```

The unpublished Wide timing command, with redirects confined to the task scratch directory, was:

```powershell
dotnet samples/evidence/probe-v5/bin/Release/net9.0/Gcam.EvidenceProbe.dll studio-readout-v5 41000001 samples samples/evidence/studio-readout/recipe-v5.json Wide dev timing-unpublished > (Join-Path $taskScratch 'pilot-Wide-41000001.json') 2> (Join-Path $taskScratch 'pilot-Wide-41000001.stderr.txt')
```

Verification commands that can create allowed build/test/catalog outputs:

```powershell
dotnet build Gcam.sln -c Release
dotnet build samples/evidence/probe-v5/Gcam.EvidenceProbe.csproj -c Release
dotnet build samples/evidence/probe-v5-budget/Gcam.ReadoutCalibrationBudget.csproj -c Release
dotnet build (Join-Path $taskScratch 'BudgetV5.csproj') -c Release
dotnet test Gcam.sln -c Release --no-build --nologo --logger 'console;verbosity=minimal'
python -B -m unittest discover -s samples/evidence/tests -p 'test_*.py'
python -B -m unittest discover -s samples/evidence/tests -p 'test_studio_readout_v5.py'
python -B samples/testing/test_records.py check-catalog --update-discovery
```

Process-local GCAM_PROVENANCE_TEST_ROOT stays under task scratch; the Git-mutation fixture is disabled and test fixtures are retained. Separate read-only check-catalog --discover, calibration_record --check, source/hash/line-ending audits and script parsing passed. New-Item and the logged Python/apply_patch file-writing operations listed above are the remaining write-command classes; no hidden launch or run-output edit occurred.

The pilot's nine positions, four exposure/rate combinations and 64 trials each are complete with both paths available and no cost failure. Its 9,248,000 scored and 3,515,543 windowed calibration counts and 1,156 markers exactly match the portable calibration-only seed 41000001, supporting the budget harness's identity with the actual pinned recipe path.

### APPROVAL REQUESTS

None. The full REPAIR32 command is prepared and unstarted; no irreversible action or sidecar approval is needed.
