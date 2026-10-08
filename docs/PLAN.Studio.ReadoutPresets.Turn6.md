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

**69. Studio physical-readout presets: experimental calibration offer with measured imaging cost (TODO-41, development).** Blind lattice growth v2 resolves the bowed fine-pitch flood without truth-assisted placement. Keep the 12 × 12 Legacy LUT/solve and DirectCrystal fixtures unchanged. At 2,000 histories/crystal High-res 662 keV calibration failed; the fixed development budget study selects 32,000 from the predetermined 8k/32k levels. V4 offers a preset/line experimentally only if every CONFIRM32 calibration succeeds. Imaging costs are descriptive: absolute source RMS, per-position bias, remote-peak fraction and equal-fluence MSE ratio F, with seed-cluster intervals at 1,000/4,000 direct-reference counts, isolated and 100,000 input hits/s. Crystal diagnostics remain beside them, not gates. No confirmation offering is published yet. Four development seeds cannot establish a low calibration-failure risk.

The fresh-history discrepancy experiment measured Baseline 662 keV RMS 0.653° Anger / 0.163° Direct, bias 0.292° / 0.152°, F 15.97 [13.17, 19.36]; Sharp 1332 keV RMS 1.131° / 0.103°, bias 0.556° / 0.073°, F 119.51 [102.41, 139.47]. Conditions: nine central in-FCFOV positions at 1 m, 1,000 expected direct counts, own flood correction, finite-mask correlation. These are DEV4 exploratory estimates, not CONFIRM32 qualification. Approximately 70% of multi-crystal histories migrate relative to the largest-deposit assignment; replacing Anger placement on its accepted histories restores direct localisation, while replacing only the energy window does not. ReadoutStudy and the independent probe agree to 1e-9 mm in theme-68 conditions. The earlier conditional history-pool estimates remain records; the two fresh-history rows supersede their population-cost interpretation. See the exploration discrepancy check and Turn6 recipe for scope and reproducibility.

### Theme 68 scope note

The quoted 0.406 ± 0.006 mm against 0.367 ± 0.001 mm is mean radial localisation error at equal accepted counts for one centred near-field lab source, at thousands of counts. It is not field-wide source RMS or an equal-fluence imaging-cost bound at 1 m. Development reproduction gives 0.394 / 0.362 mm and matches ReadoutStudy event-for-event. At low counts across the Studio field, multi-crystal migration can let competing remote decoded peaks dominate the source MSE; theme 69 measures that separate regime. Successful flood calibration alone establishes experimental usability, not localisation performance.

### EV-36 scope note

The selected readout's near-field equal-count localisation result applies to the declared lab geometry and centred-source study. It does not imply a ten-percent imaging penalty across Studio optics, field positions, lower counts or equal emitted fluence. TODO-41's development imaging study finds substantially larger, exposure-dependent source errors from multi-crystal migration and remote peaks. Preset/line offerings remain experimental, require every pinned confirmation calibration to succeed, and display measured source RMS, bias, remote-peak probability and F with declared conditions/seed uncertainty. F is a source-MSE ratio, not a universal measurement-time multiplier. Broader confirmation is pending; DirectCrystal stays the engine default. Studio currently provides the experimental Baseline preparation path; generalized offers are a later stage.

### LIM-11 scope note

The physical path uses virtual matched sensors, assumed optical constants and unlimited microcells. These assumptions are more consequential at finer pitch; calibration success cannot validate saturation, recovery or measured detector performance. Multi-crystal centroid migration also adds an exposure/geometry-dependent imaging cost: low-count remote decoded peaks can dominate errors despite a valid flood LUT. Show experimental preset/line availability beside measured absolute RMS, bias, remote-peak probability and equal-fluence MSE ratio, with scope and seed uncertainty. DirectCrystal remains the default; no performance threshold is inferred from calibration success. The physical Studio option remains an engineering simulation, not a validated instrument claim.

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
