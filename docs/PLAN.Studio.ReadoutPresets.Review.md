# PLAN.Studio.ReadoutPresets.Review — measured diagnosis and a proposed qualification policy

Scope: TODO-41 review only against MainRoot `a2c7580`. The reference plan was read from the read-only main tree. Only this review is written in the repository. Scratch sources, probes, floods, proposed study inputs and logs are under `%TEMP%\gcam-todo41\`. No desktop, product-code change, git mutation, installation or long validation run.

Status: diagnostic review, 2026-10-08. Decisions and implementation remain with the author and planner. None of the pilot results establishes a hardware-performance claim or validates a new LUT/default.

## Corrections to the reference plan

The plan is incomplete in a way that would make opening the gate unsafe: preparation and status text are not the only consumers hard-coded to the Baseline head. Imaging, Waveform and the circuit renderer also assume 12 columns. Catalogue-size sensors and a light guide are a legitimate published architecture, but their success at these pitches is not established by published practice. The measured B candidate fails; it is not a ready replacement preset.

| “What exists” row | Checked file/member | Verdict/correction |
|---|---|---|
| Studio presets | `src/Gcam.Studio.Core/Optics/OpticsPreset.cs`, `All`, `Match`; `src/Gcam.Configuration/SceneConfigBuilder.cs`, `OpticsSettings`, `Build` | Counts/pitches and Sharp defaults are correct. Wide: rank 17, cell 1 mm, D 50 mm; High-res: rank 13, cell 0.5 mm, D 100 mm. Custom is a null preset/matching fallback, not an unrestricted physical-readout contract. Builder clamps count to 4–64, pitch/cell to at least 0.05 mm, D to at least 1 mm and snaps rank to a prime. |
| Geometry gate/key | `src/Gcam.Studio.Core/Services/ReadoutPolicy.cs`, `GeometryError`, `Key` | Gate requires exact detector count/pitch **and** rank 7/cell 1/D 60. It does not check gap or absorbers. Key contains version, count, pitch, gap, entrance and backing; fixed material/depth/readout/seed/budget policy are implicit in the version. Mask settings need not invalidate a uniform calibration, but acquisition geometry still needs its own identity. |
| Studio preset/defaults | `src/Gcam.Studio.Services/ReadoutPreparationService.cs`, `Preset`, `Build`; `src/Gcam.Configuration/Readout/*.cs` | Correct: FourOutputAnger, DPC 10/1000 ohm, matched virtual sensors, specular 0.98, eight depth bins, 100,000 photons/bin, 2,000 scored histories/crystal, 32 bins/crystal, sigma 1 bin, nominal separation 0.5. Also: couplant 0.05 mm, guide 0, threshold Sum 50 keV-equivalent, window ±15%, minimum peak fraction 0.02, unlimited cells. Build forces GAGG and legacy gain sigma zero. Bare engine network defaults to ratio 0.1; Studio overrides it to 0.01. |
| Hard-coded geometry | `ReadoutPreparationService.Build`; `src/Gcam.Studio.Core/ViewModels/DetectorWorkspaceViewModel.cs`, `CalibrationState` | Listed 144/12 assumptions are correct, but the row misses `src/Gcam.Studio.Services/PhysicalReadoutProjection.cs`, `Imaging` (12×12 window flood, `%12`, `/12`); `PhysicalReadoutWaveform.cs`, `Process` (event pixel coordinates); and `src/Gcam.Studio/Controls/ReadoutMapView.cs`, `OnRender` (144 sensor nodes, coordinates and widths divided by 15). These are mandatory changes, not cosmetic follow-ups. |
| Sensors | `src/Gcam.Configuration/Readout/ReadoutSensorConfig.cs`; `src/Gcam.Detector/Readout/SensorLayout.cs`, `From`, `SensorAt` | Correct virtual matched geometry and explicit count/pitch/width/offsets. Active width is pitch minus shared reflector gap, not pitch itself. The representative sensor response is the 3×3 mm S13360-3050 budget. The plan's blanket assertion that no small catalogue sensors exist is not established by this repository or a finite literature search; say “no catalogue implementation is specified for these matched arrays.” Array/package pitch, active width and microcell pitch are different quantities. |
| Optical table/classes | `src/Gcam.Detector/Readout/OpticalResponse.cs`, constructor, `TraceDirection`, `Land` | Correct mirror/transpose reduction, no translation reuse. Exact centred square even-n class count is m(m+1)/2, m=n/2: 21/120/153/253 for Baseline/Sharp/Wide/High. Offset layouts lose reflections; rectangular layouts lose transpose. Guide edges absorb; table averages lateral positions and bins DOI. |
| LUT | `src/Gcam.Detector/Readout/FloodLut.cs`, `Calibrate`, `Irregular` | Correct sequence: local maxima → global nominal-radius suppression → highest NxNy → sort by y into equal-sized rows and by x within row → monotonicity/regularity → watershed/Voronoi. `PeaksFound` is **all retained maxima before taking NxNy**, not the number of selected/ordered crystal markers. `Peaks` is empty on failure. A 0.02 height cut is relative to the global maximum. |
| TODO-40 measurements | `docs/archive/PLAN.Studio.Readout.Review.md`, preparation and event-cost tables; `.md`, SD-2; `.Turn2.md` | Quoted historical numbers are correct. Three Sharp calibration repeats reuse the same flood and response/noise seeds; they are timing/determinism repetitions, not three seed trials. Prior per-hit costs are batch/time-stream measurements and cannot be compared directly with an isolated synthetic-hit cost. |
| Theme 68/evidence/limitations | `docs/AGENTS.Findings.Part2.md`, theme 68; `docs/VV.Gcam.Evidence.md`, EV-36; `docs/VV.Gcam.Limitations.md`, LIM-11; `docs/VV.Gcam.Decisions.md`, D-56/57; `docs/HW.Readout.md` | Correct conditional 1 mm selection/confirmation, no fine-pitch qualification. EV-36's 12×12 lab head uses different optics and gaps from Studio. The “study only/nothing yet” descriptions in EV-36, LIM-11 and theme 68 now predate TODO-40's explicit signal-only `PhysicalListModeSource`; planner should synchronise their scope wording without replacing their original measurement conditions. Engine ordinary paths remain guarded. |

`ReadoutCalibration.Build` counts positive-position triggered outcomes in `Triggered`; it does not separately retain every hardware trigger. `ReadoutFlood.Transport` returns exactly the requested number of **scored** histories after retrying non-scoring photons. “Events per crystal” is an array-average input budget, not a guarantee of equal occupancy or accepted photopeak statistics per crystal. The existing Studio validation diagnostics are single-crystal histories at **all deposited energies**, not the theme-68 single-crystal photopeak accuracy metric.

## Probe contract and interpretation

The Release net9.0 probe compiles copied sources from the six engine projects, excluding bin/obj. No package is required; scratch `NuGet.Config` clears package sources. `sources.sha256` identifies the engine source bytes. The scratch project references no repository project, so build output stays in scratch.

Every head uses 10 mm GAGG, gap 0.1 mm, entrance 0.15 mm, backing 2 mm, zero legacy gain, and the Studio readout defaults above. A is the current virtual 1:1 sensor, active widths 0.9/0.5/0.65/0.3 mm. B is fixed before measurement: centred square sensors of active width 3 mm on 3.2 mm pitch, count `ceil(detector face / 3.2)` in each axis, 2 mm PMMA guide (index 1.49), unchanged DPC/trigger/electronics/reflector.

Each A/B optical table uses seed 402 and 100,000 photons × eight bins × the stated symmetry classes. Each head shares one transported calibration flood, seed 403, 2,000 scored histories/crystal. Two response/ADC seed pairs, 404/405 and 51404/51405, calibrate that same flood/table. Thus there are **two response trials, one optical realization and one transport realization per head**, not two independent outer-seed validations. Truth-labelled isolated 661.7 keV deposits use 400 events/crystal, uniform DOI over 10 mm, streams 51001/51002. These are raw-plane diagnostic spots; lateral coordinates are averaged by the engine's optical table. They provide centres and sigma without a LUT.

Spacing is the neighbouring centres' coordinate difference along the corresponding grid axis. Width/spacing is `2.3548 × mean(neighbour sigmas) / spacing`, a Gaussian-equivalent width (not a fitted multimodal FWHM). Edge compression is x=0→1 spacing divided by the two middle crystals' spacing in the middle row, estimated from those sampled centres. A guide can make DOI mixtures non-Gaussian; a small median does not establish edge resolution.

P/V uses the **real calibration-window density**, sampled along the independently labelled true-centre links, never along LUT markers. Profiles have 65 samples; a half-profile maximum at the midpoint is unresolved; zero valleys are censored, not physically infinite contrast. Min finite P/V below is separate from the unresolved count. The nearest-centre oracle labels transported, true single-crystal full-energy histories **within the global calibration window**. Its errors diagnose raw-plane overlap; its truth-assisted mapping is not a production LUT, a Bayes-optimal classifier, held-out validation, or a guarantee for rejected events. B's global energy window can select a badly biased subset, especially on the small control head.

Budget diagnostics were declared in the probe before measurements: nested first-quarter/whole scored flood (500/2,000 histories/crystal), 16/32/64 histogram bins with sigma kept at one bin, and 20,000/100,000 optical photons using the same optical seed. Changing histogram bins changes smoothing in raw-coordinate units; any successful 16-bin outcome is a retrospective diagnostic, **not a validated new default**. The optical-budget comparison measures noiseless centre shifts at every crystal/depth, not full optical convergence. Eight versus sixteen DOI bins and 100k versus 400k photons remain development work for the planner's run.

## RP-1: measured A diagnosis by preset

Both trials below reuse optical/transport realizations; response/ADC seeds differ. P/V values are medians of resolved profiles at 32 bins/crystal, followed by the unresolved profile count. Spot widths come from the separate 400 truth-labelled events/crystal, not from LUT markers.

| Head A | Minimum / median raw spacing | Median / p95 / worst FWHM:spacing | Edge:centre spacing | P/V median, seeds 404 / 51404 | Unresolved / total neighbour links, same seeds | Truth nearest-centre error in calibration window |
|---|---|---|---|---|---|---|
| Baseline 12×12 @ 1 | 0.14755 / 0.15262 | 0.153 / 0.181 / 0.190 | 0.988 | 30.26 / 29.60 | 0 / 264, both | 0/44,783, both |
| Sharp 30×30 @ 0.6 | 0.05812 / 0.06354 | 0.379 / 0.456 / 0.504 | 0.985 | 6.906 / 6.958 | 0 / 1,740, both | 1,678/259,077 = 0.6477%; 1,721/259,078 = 0.6643% |
| Wide 34×34 @ 0.75 | 0.04973 / 0.05617 | 0.428 / 0.521 / 0.579 | 0.989 | 5.073 / 5.078 | 1 / 2,244; 0 / 2,244 | 5,991/341,631 = 1.7536%; 6,146/341,631 = 1.7990% |
| High 44×44 @ 0.4 | 0.03810 / 0.04350 | 0.558 / 0.679 / 0.757 | 1.003 | 3.454 / 3.470 | 24 / 3,784; 22 / 3,784 | 43,199/535,159 = 8.0722%; 43,497/535,150 = 8.1280% |

The minimum separation divided by the root-sum-square neighbour sigmas is 8.765/3.307/2.875/2.199 for Baseline/Sharp/Wide/High. All fine A isolated-spot worst widths are below spacing. This establishes an identifiable raw grid in this model, with increasingly significant tails/overlap; it does not establish every spot in the mixed Compton calibration density as resolved. The small unresolved counts and their histogram sensitivity must stay visible. Minimum finite P/V over the two response trials is 13.12/2.571/1.919/1.138 respectively; an unresolved link would have P/V=1 under the engine's usual summary convention.

| A head / trial | Local maxima before suppression | Retained before highest-N | Missing / multiply selected truth centres among highest-N | Markers put into the wrong truth row by global y grouping | Failure location |
|---|---:|---:|---:|---:|---|
| Baseline / 404 | 3,076 | 265 for 144 | 0 / 0 | 0 | succeeds |
| Baseline / 51404 | 3,085 | 265 for 144 | 0 / 0 | 0 | succeeds |
| Sharp / 404 | 26,991 | 1,988 for 900 | 2 / 2 | 26 | row 0, columns 24/25 |
| Sharp / 51404 | 26,861 | 1,979 for 900 | 4 / 4 | 31 | column 23, rows 22/23 |
| Wide / 404 | 36,541 | 2,578 for 1,156 | 17 / 17 | 147 | column 19, rows 5/6 |
| Wide / 51404 | 36,418 | 2,590 for 1,156 | 16 / 16 | 114 | column 11, rows 12/13 |
| High / 404 | 66,997 | 4,477 for 1,936 | 272 / 262 | 1,600 | column 11, rows 24/25 |
| High / 51404 | 66,702 | 4,435 for 1,936 | 261 / 255 | 1,568 | column 34, rows 2/3 |

“Missing/multiply selected” associates each selected maximum with its nearest independently truth-labelled spot centre. It diagnoses selected-marker coverage; it is not a truth-informed replacement calibration. A centre can receive three maxima, hence missing and duplicated-centre counts need not match. The selected markers are the highest smoothed-bin densities after nominal-radius suppression; true spots are not privileged. In fine arrays, broad spot shoulders and the multi-crystal continuum create many local maxima. Sampling, highest-N replacement and global row grouping compound each other.

A supplementary same-seed replay retains actual selected positions/heights in `{head}-A-markers.json`. Sharp/404 specifically **drops crystals 0 and 899** (opposite corners) and assigns two selected maxima each to crystals 640 (column 10,row 21) and 728 (column 8,row 24). Their weaker shoulder maxima have smoothed heights about 1.67 and 1.69, still high enough to displace corner candidates. This reproduces 1,988 retained peaks and the row-0 columns-24/25 failure. Wide/404 drops truth IDs **1,4,9,31,35,100,102,272,338,1087,1088,1123,1125,1126,1127,1130,1151**, mostly borders; the full duplicate-marker identity/position/height list is in its marker file. IDs are row-major `y*n+x`, zero based. This directly demonstrates why changing only the order of the same highest-N markers is insufficient.

High/404 also replays exactly: 4,477 retained, 272 missing/262 multiply selected centres, column 11 rows 24/25 failure. Its missing list starts with IDs 0,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,19,20,21,22,23,24,26,28: much of the first border row is displaced. The complete 1,936 selected raw coordinates/heights and all missing/duplicate IDs are retained in `High-A-markers.json`, rather than copying hundreds of IDs into this document.

| A head | First-quarter flood, 500 histories/crystal | Whole flood at 16 bins/crystal | At 32 | At 64 |
|---|---|---|---|---|
| Baseline | 265 / 265 retained; 2/2 success | 230 / 223 retained; 2/2 success | 2/2 success | 265 / 266 retained; 2/2 success |
| Sharp | 1,968 / 1,970 retained; both fail | 1,496 / 1,512 retained; **2/2 success** | 2/2 failure | 2,074 / 2,088; both fail |
| Wide | 2,579 / 2,574; both fail | 1,901 / 1,914; both fail | 2/2 failure | 2,726 / 2,745; both fail |
| High | 4,438 / 4,358; both fail | 3,323 / 3,340; both fail | 2/2 failure | 4,757 / 4,721; both fail |

The first-quarter population is a nested transport sample, not a new trial. A fourfold event increase does not repair the default LUT. Bins strongly matter because sigma is in bins: at 16, smoothing is twice as wide in raw coordinates, at 64 half as wide. Higher P/V at 64 is partly a sparsely sampled/censored valley effect and is not better accuracy. Sharp's 16-bin pass is an observed development candidate, not a chosen or validated policy.

For 20k versus 100k optical photons, mean/max noiseless centre shifts in units of nominal `2/n` spacing are Baseline 0.00125/0.00396, Sharp 0.00183/0.00574, Wide 0.00160/0.00508 and High 0.00261/0.00901. They are much smaller than A's stochastic spot widths. Optical shot noise at 20k is not the principal explanation for A's missing highest-N markers. This is not a 100k-versus-400k convergence result, nor an energy-width convergence check.

Per-preset causes: **Baseline A: no diagnosed calibration defect at defaults. Sharp A: algorithm and histogram sampling scale, with modest physical overlap; not physically merged. Wide A: algorithm and sampling scale dominate calibration failure, with a larger physical error floor and a marginal mixed-flood link. High A: both algorithm/sampling and materially significant physical overlap/mixed-flood contrast; an ordering repair alone cannot promise acceptance.** No fine-pitch default is validated by these trials. None shows simple “too few total calibration events” as the sole explanation.

## RP-2: published-practice design B and its limitation

Published fine-crystal scintillator detectors use coarser SiPM arrays, optical light guides and multiplexed position readout; edge resolvability is a known design problem. This supports the architecture, not a guarantee for submillimetre GAGG, the present reflector law or this DPC. See [PET Detectors Based on Multi-Resolution SiPM Arrays](https://pubmed.ncbi.nlm.nih.gov/39553417/) and [Development of a multiplexed readout with high position resolution for positron emission tomography](https://www.sciencedirect.com/science/article/abs/pii/S0168900217300268). No former-employer geometry or electronics enters this design.

| Head | Crystal face | B sensor array | B active area/grid span | Guide |
|---|---|---|---|---|
| Baseline control | 12 mm square | 4×4 (16) | 3 mm active / 12.8 mm span | 2 mm |
| Sharp | 18 mm square | 6×6 (36) | 3 mm active / 19.2 mm span | 2 mm |
| Wide FOV | 25.5 mm square | 8×8 (64) | 3 mm active / 25.6 mm span | 2 mm |
| High-res | 17.6 mm square | 6×6 (36) | 3 mm active / 19.2 mm span | 2 mm |

The 3.2 mm sensor pitch is a model layout assumption for an array of catalogue-size active areas, not a claim that individually packaged S13360-3050 devices can be mounted on this pitch. An actual implementation must name a compatible array/package and its borders. Guide lateral bounds currently follow the crystal face, not the larger sensor span. The tracer models refracted straight propagation with absorbing guide edges, not a complete reflecting manufactured light-guide assembly. A partitioned/reflecting guide would be a new model and new study, not a parameter fix to these failures.

| B head | Minimum / median raw spacing | Median / p95 / worst FWHM:spacing | Edge:centre spacing | P/V median, 404 / 51404 | Unresolved neighbour links, same seeds | Nearest-centre error in global calibration window |
|---|---|---|---|---|---|---|
| Baseline | 0.04091 / 0.10187 | 0.229 / 0.733 / 1.035 | 0.345 | 10.79 / 10.73 | 29 / 27 of 264 | 27.67% / 27.71% (only 1,236/1,234 true full-energy singles remain in this window) |
| Sharp | 0.00977 / 0.04691 | 0.494 / 2.521 / 4.681 | 0.213 | 3.931 / 3.871 | 162 / 185 of 1,740 | 4.833% / 4.878% |
| Wide | 0.00587 / 0.04728 | 0.492 / 3.219 / 7.562 | 0.175 | 4.003 / 3.915 | 189 / 192 of 2,244 | 6.003% / 6.054% |
| High | 0.00406 / 0.03122 | 0.760 / 3.652 / 13.285 | 0.204 | 2.303 / 2.316 | 449 / 499 of 3,784 | 21.26% / 21.24% |

| B head / trial | Local candidates → retained (expected) | Missing / multiply selected truth centres | Default failure |
|---|---|---|---|
| Baseline / 404; 51404 | 1,047→118; 1,051→116 (144) | 32/6; 34/6 | fewer peaks, both |
| Sharp / 404; 51404 | 12,053→941; 11,995→942 (900) | 201/170; 198/171 | column 23 rows 4/5; column 16 rows 26/27 |
| Wide / 404; 51404 | 21,976→1,619; 22,141→1,619 (1,156) | 269/239; 274/241 | column 31 rows 1/2; column 26 rows 3/4 |
| High / 404; 51404 | 31,134→2,064; 31,093→2,105 (1,936) | 475/426; 507/452 | column 22 rows 27/28; column 38 rows 2/3 |

No B calibration succeeds at 16/32/64 bins or at the first-quarter event budget. The median widths conceal severe edge/phase compression. **B cause, per preset: Baseline has optical/energy-selection and marginal edge-resolution problems plus too few retained markers; Sharp/Wide/High have physically overlapping compressed edges in this sampled model, compounded by the algorithm and numerical table noise.** This fixed B does not support a ready offer on any head. It cannot be rescued honestly by assigning a fitted grid to unresolved spots.

B's 20k→100k optical centre shifts, mean/max in nominal crystal spacing, are 0.0268/0.0771 (Baseline), 0.0470/0.1480 (Sharp), 0.0419/0.1281 (Wide) and 0.0751/0.2682 (High). Thus B is more numerically sensitive than A; a higher-photon/DOI convergence study is required before making precise physical error claims. The observed broad overlap is already present at the full 100k budget. More flood histories alone do not address compression. This review does not isolate B's global-photopeak estimator from its depth-dependent optical collection; its very low Baseline full-energy window population is specifically a reason not to quote conditional accuracy as total efficiency.

LIM-11 applies differently to the two designs. Peak sensor photoelectrons at 662 keV near the exit face, divided by `400 cells/mm² × active area`, are A **10.60/28.69/18.61/62.39** pe per 50-micrometre cell versus B **0.282/0.223/0.246/0.180**, in Baseline/Sharp/Wide/High order. These are one central-crystal/exit-depth occupancy diagnostics, not the maximum over the whole detector or a recovery prediction. A's fine-pitch identification relies on an increasingly severe unlimited-cell assumption. B lowers occupancy through larger active areas and sharing, but is not proven linear: even 0.2 pe/cell implies a nonzero no-recovery compression, and recovery/RC/real optics remain omitted. Do not describe B as a saturation correction or A as catalogue hardware.

### Flood sketches and retained images

The following centre sketch is qualitative; the numbers above, not this drawing, define compression:

```text
A middle row:      o---o---o---o---o---o---o---o      edge spacing ~ centre
B middle row:        ooo--o----o----o----o--ooo        compressed edge / phase
                    ^ broad DOI-dependent edge spots overlap
```

Actual 80×80 ASCII rasters are `%TEMP%\gcam-todo41\{Baseline,Sharp,Wide,High}-{A,B}-flood.txt`. They integrate the default smoothed density into display cells, use square-root brightness and show positive raw Y upwards. A has a nearly full-plane regular grid with background between spots; B occupies a smaller central area and bunches/merges border spots. At 44×44, this 80-cell view cannot resolve every crystal; it is a visual orientation aid, not a resolution measurement. `{head}-{design}-centres.json` retains every independently truth-labelled centre and sigma, plus the windowed transported single-crystal full-energy points. Marker-detail files described below identify rejected/duplicate coverage explicitly.

### Measured costs and machine load

One default optical/device construction per design is timed; the two calibrations differ only in response/ADC seeds. An additional 20k build is a budget diagnostic. Values are seconds except isolated synthetic single-crystal hit costs (microseconds); transport is shared between A and B. Calibration excludes flood transport, diagnostic replays and Studio publication/validation/view work.

| Head/design | Optical table | DC network | Device total | Calibration, seeds 404 / 51404 | Isolated synthetic input hit (us) |
|---|---:|---:|---:|---:|---:|
| Baseline A | 6.789 | 0.0165 | 6.806 | 0.781 / 0.696 | 5.214 |
| Baseline B | 6.126 | 0.00015 | 6.126 | 0.304 / 0.306 | 1.021 |
| Sharp A | 36.854 | 2.130 | 38.984 | 17.642 / 17.819 | 10.302 |
| Sharp B | 38.749 | 0.00023 | 38.749 | 3.667 / 3.811 | 2.112 |
| Wide A | 45.328 | 3.779 | 49.107 | 28.686 / 28.371 | 12.607 |
| Wide B | 45.019 | 0.00098 | 45.020 | 5.531 / 5.667 | 2.213 |
| High A | 75.284 | 17.657 | 92.941 | 75.492 / 75.573 | 18.462 |
| High B | 75.043 | 0.00023 | 75.043 | 6.703 / 6.689 | 1.589 |

Shared transport costs: Baseline 0.392 s/288,000 scored histories; Sharp 2.064 s/1,800,000; Wide 2.458 s/2,312,000; High 4.195 s/3,872,000. First calibration-window populations A/B are 91,037/38,330; 634,207/483,788; 879,638/719,712; 1,357,130/1,016,327. B's calibration is cheaper partly because the network has fewer sensors and fewer candidates survive, **not because it produces a usable LUT**.

The synthetic-hit timer includes Respond, trigger/hold/ADC via `ProcessIsolated` and raw Position for 400 inputs/crystal, excluding transport/LUT/gain lookup, rate overlap and Studio projection. It is a repeatable input distribution for comparing sensor counts; it is not a live acquisition speed claim. A supplementary transported-hit replay includes attempted LUT/gain lookup and preserves the distinction between failed and successful calibrations.

That supplementary replay processes the first 10,000 seed-403 transported histories with response/ADC 604/605 after replaying the exact seed-404 calibration. A costs **6.056/8.452/10.447/17.333 us/input** for Baseline/Sharp/Wide/High, with 9,230/9,237/9,297/9,240 trigger outcomes. It includes Respond, isolated hold/ADC, Position, attempted LUT lookup and calibrated energy; the three fine-head LUTs fail, so their lookup/energy cannot represent a successful Imaging path. It excludes gamma transport, rate overlap and publication/decoding/rendering. These are single short timer observations, not high-rate or end-to-end Studio guarantees.

This is a **busy-machine pilot**, not the plan's requested idle-machine benchmark. The runtime reports 24 logical processors. A one-second CPU-delta observation while the probe ran showed the probe consuming about one CPU-second, another dotnet process about 1.03, and another Python process about 1.41. No process was stopped and no unrelated process path/data was inspected. One build each gives no timing confidence interval; historical TODO-40 timings differ. In particular, High A already spends ~173 s in construction+transport+first calibration before independent diagnostics/publication. Do not promise completion inside the existing 90-second desktop timeout. The exhaustive diagnostic bin scans/oracle comparisons take additional wall time and are outside the stage cost table.

## RP-5: exact cost reductions and cancellation

**Translation reuse of an “interior optical class” is not exact for the current finite device.** Even with opaque crystal walls and guide=0, the 0.05 mm couplant shifts a transmitted grazing ray by `t × sin(theta)/cos(theta)`. As the transmitted angle approaches 90 degrees this shift is unbounded. A ray landing on an active sensor after one translation can leave the finite grid after another; with a guide, `Land` additionally checks the finite crystal-face boundary. There is no finite interior radius that makes all such paths translation-equivalent. Coarse B has crystal/sensor phase differences as well. Limiting ray reach or imposing periodic/infinite boundaries changes the physical model. Reusing exit phase-space samples and applying each landing geometry is possible, but shared RNG draws change table correlations and do not preserve existing sampled tables bit for bit.

Exact reuse **does** exist for identical immutable optical/network artifacts keyed by every resolved physical value, seed/budget and algorithm version. Current preparation cache has two successful entries; a failed result is not cached. Persisted tables need a versioned schema, checksums, atomic completeness and a resolved content key, not a preset name. No persistence is implemented in this turn.

The circuit has another exact reduction in mathematical arithmetic. Let symmetric grounded nodal conductance be G. Sensor k injects e_k, output c measures `b_c^T G^-1 e_k`, where b_c contains conductances into that grounded output. By symmetry this equals `(G^-1 b_c)_k`: **four adjoint solves instead of one solve per sensor**. With finite output input resistance, include the output nodes in G and use `b_c = e_output/R_input`. The present `ChargeDivisionNetwork.Solve` factorises once, then solves and calculates a dense residual for every input. An adjoint implementation preserves the circuit's linear solution; floating-point results are not necessarily byte-identical. Verify against the old solve and independent SPICE solver with conditioning-derived bounds, including finite input impedance. Sparse/banded factorisation also preserves the equations, but requires an independent numerical check; do not advertise a measured speedup from this proof alone.

`PhysicalListModeSource` constructs a new `ReadoutDevice(device.Optics, readout)` at Start, which currently solves the network again despite successful Prepare. Sharing an immutable, content-matched network artifact while keeping each device's mutable response buffers private can remove that repeated solve exactly. Preparation cancellation is cooperative in optical tracing, transport and calibration, but the dense network solve has no token. Add bounded cancellation checks there if it becomes a material delay; Task.Run alone does not make it cancellable.

## RP-3: proposed LUT change, frozen before validation

For a raw plane with resolved spots, repairing only the final row sort is insufficient: highest-N selection has already removed true centres. Propose an **opt-in topology-aware calibration method**, keeping the legacy method and Baseline artifacts unchanged.

1. Extract the existing local maxima **before** global nominal-radius suppression. Keep their measured density/contrast, not truth IDs.
2. Initialise a monotone rectangular lattice from measured projections/border chains, with known counts only. Fit smoothly varying row/column curves; assign measured maxima to slots with a deterministic one-to-one minimum-cost matching. Score local contrast and geometric continuity together. Retain unassigned candidates as rejected diagnostics; never turn a fitted empty slot into a measured marker.
3. Use local fitted spacing for duplicate rejection. Global 0.5×nominal spacing can remove true compressed edge spots. Pin the fit model, neighbourhood, objective weights, contrast gates, tie-breaking and failure rules on development seeds before selection. No hidden per-preset smoothing/separation tuning.
4. Require all slots supported by measured maxima and reject merged/ambiguous/nonmonotone grids explicitly. Keep watershed as the initial segmentation so marker selection and segmentation are not changed simultaneously. Test row curvature and sparse valleys separately.
5. Calibration gets only its flood, counts and declared method. Truth centres and `ReadoutDevice.Expected` may diagnose accuracy in the probe but must not supply calibration markers. A simulated-truth marker table would abandon the blind criterion.

This is a proposed implementation design, not a measured corrected LUT. B's unresolved edge spots must not be forced into a topology. The new method may fail; refusal remains an acceptable outcome. Do not change Baseline smoothing, seeds, budgets or legacy LUT merely to align it with the new algorithm.

### Prepared families, pins, commands and timing pilot

Scratch `StudyRunner.cs` provides a headless `study <recipe> <outer-seed> <output>` adapter over the **existing** `ReadoutStudy`. `study-recipe-v1.json` contains all four actual Studio geometry scenarios × A/B (eight cases), each with its own mask geometry and the same paired direct reference. It clones/mutates resolved configuration instead of replacing detector sections with partial defaults. Full request: 2,000 calibration histories/crystal; 3,000 validation histories/crystal at 122.06/661.7/1332.5 keV; 200,000 detector-biased localisation histories/line; 10,000 rate hits at 1e3/1e4/1e5/3e5/1e6 cps; current 100k/eight-bin optics and 32-bin/sigma-1 calibration. B is a fixed unsuccessful reference, not a thickness search. The existing study request has no per-geometry crystal count or per-variant sensor override, so separate scenario/base-readout cases are necessary; simply adding fine pitches to the old readout request would still test 12×12 arrays.

The scratch adapter is usable for legacy study data. **Before qualification runs, implement the opt-in method, add displacement/truth-centre/acceptance-denominator metrics and a failing-closed aggregate/selector through the repository's evidence machinery, adopt the product rule, then re-pin the final source/binary/recipe/selector.** The current preparation pin explicitly states legacy LUT and proposal-only status; it must not certify new-LUT confirmation. Old EV-36 scripts and their 1 mm rule are not a TODO-41 selector.

`study-seeds-v1.json` contains T41_DEV4 = `41000001+104729*i`, i=0…3; T41_SELECTION16 = `42000001+104729*i`, i=0…15; T41_CONFIRM32 = `43000001+104729*i`, i=0…31; T41_REPAIR32 = `47000001+104729*i`, i=0…31. All 84 seeds were checked disjoint from each other, the repository/depth-likelihood lists and review streams. They are proposed outer-seed families; purpose streams are keyed independently by the study. Development fixes one algorithm and numerical recipe; selection ranks the frozen candidates within each geometry; confirmation evaluates the selected design unchanged. Any adjustment after confirmation consumes the third fresh family with a new pin and retains the failed record.

`rule-v1.md` pins the proposed sequence, geometry-derived spatial allocation, eligibility-before-ranking, paired seed differences >2 SE, no per-preset smoothing rescue, Baseline invariance and failure retention. `run-family.ps1` checks all pinned source/binary/recipe/rule/seed bytes, runs serially without a watcher and refuses existing output roots. Preparation hashes:

| File | SHA-256 |
|---|---|
| `rule-v1.md` | `f9c363e427ee0c1719030b38a5b78245c04f115c88de37a1b9de878af19e1a04` |
| `study-recipe-v1.json` | `79160d9adfcd071f108897eb9542f840bea20af6855fe88ef1d9375d790d17bd` |
| `study-seeds-v1.json` | `2a40ce26258c2b7e7e31f3891129debb578dd68da6e8fa02838ad5d81d806095` |
| `study-pin-v1.json` | `a722f5f547d08d370fbdae872da4dd5af9582548c7cb59c79005e0336c40e26e` |

Exact prepared commands, **planner-run only after implementation/adoption/re-pinning**, using a fresh output root each time:

```powershell
$taskRoot = Join-Path $env:TEMP 'gcam-todo41'
& (Join-Path $taskRoot 'run-family.ps1') -Family T41_DEV4 -Out (Join-Path $taskRoot 'dev-v1')
& (Join-Path $taskRoot 'run-family.ps1') -Family T41_SELECTION16 -Out (Join-Path $taskRoot 'selection-v1')
& (Join-Path $taskRoot 'run-family.ps1') -Family T41_CONFIRM32 -Out (Join-Path $taskRoot 'confirmation-v1')
# Only if a declared post-confirmation repair is necessary, with a new pin:
& (Join-Path $taskRoot 'run-family.ps1') -Family T41_REPAIR32 -Out (Join-Path $taskRoot 'repair-v2')
```

Do not execute all four commands as one automatic sequence: freeze/adopt/select between phases. The scratch runner writes per-seed data, not a qualification verdict. Port the final families to `samples/evidence/` with staged executable closures, provenance validation and a pinned selector before publishing findings. Report failed calibration rows; do not aggregate only survivors. New calibration and validation floods are purpose-keyed and independent within each outer seed. For close comparisons use paired **seed delta** uncertainty, not independent-SE addition that discards pairing. Use seed-cluster intervals and simultaneous weakest-crystal bounds; a few diagnostic seeds cannot set those limits.

The eight-case stage timing pilot measured roughly 352 s of full-budget device construction and 139 s of first calibrations, plus ~9 s shared transport (or ~18 s if separate A/B study calls repeat it). A full successful study also transports/responds to three validation lines (~37.2 million scored histories/design over all heads) and performs localisation/rate work. Using the measured isolated costs only as a rough planning calculation gives **about 15–25 min per full outer seed**, not a measured full-family duration: serial selection16 ~4–7 h, confirmation32 ~8–13 h before aggregation. Legacy failures skip downstream metrics and would appear deceptively faster. Run one actual adopted full-family timing seed first; start with one worker and measure peak memory before choosing concurrency. The short diagnostic probe's bin scans/truth-oracle matching are not included in these stage estimates. No full-family timing, development, selection or confirmation ensemble was started here.

The prepared adapter's API smoke ran once, outer seed 41000001, **1.280 s wall**, exit 0: Baseline A only, 5k optical photons, 200 calibration/30 validation histories per crystal, 1,000 localisation histories and 100 rate hits. Calibration succeeds (263 retained maxima, 144 ordered markers, no gain fallback). `study-smoke-result.json` and `study-smoke-timing.json` retain it. It exercises request loading, the existing study and JSON output; this reduced-budget calibration is not a qualification result or a full-family timing estimate. This smoke seed belongs to development only; it does not expose selection/confirmation data.

## RP-4: acceptance metrics derived from each geometry

Report both readout association and source reconstruction; do not borrow EV-36's near-field millimetres, mis-ID cut or FWHM from the 12×12 lab experiment.

At the axis in the far-field approximation a wrong crystal step has angular scale `q = pixel pitch / D`; continuous geometric checks should use `atan(delta x/D)` and the acquired focus geometry. Uniform within-pixel uncertainty gives per-axis RMS `q/sqrt(12)`. The author may allocate that much **additional** per-axis RMS to readout assignment; this is an engineering allocation, not measured hardware accuracy.

| Head | q (mrad / degrees) | Per-axis allocation q/sqrt(12) (degrees) | Mask element cell/D (degrees) |
|---|---|---|---|
| Baseline | 16.667 / 0.955 | 0.276 | 0.955 |
| Sharp | 7.500 / 0.430 | 0.124 | 0.501 |
| Wide FOV | 15.000 / 0.859 | 0.248 | 1.146 |
| High-res | 4.000 / 0.229 | 0.0662 | 0.286 |

Recommendations, requiring adoption before confirmation:

- Every confirmation calibration succeeds with exactly one measured marker per crystal, no truth-assisted assignment and no unexplained gain fallback. With zero failures in 32 seeds, the one-sided 95% binomial upper failure probability is still `1-0.05^(1/32)=8.94%`; this is not a claim of 99% field reliability.
- Independently measure raw centres, projected covariance and true-neighbour contrast. Record every unresolved/censored profile and centre/edge/corner results. For two equal Gaussian spots, two modes require d>2 sigma (Gaussian-equivalent FWHM/spacing<1.177); this is a diagnostic derivation, not a guarantee for unequal or DOI-mixed spots. Use measured modal support and held-out error rather than a median P/V pass.
- Held-out 661.7-keV photopeak mis-ID is wrong/assigned true single-crystal histories in the measured window. Also report true full-energy single-crystal results regardless of the measured window, wrong/scored, trigger/scored, assigned/scored, window/scored, outside-plane/invalid counts and the confusion displacement matrix. Rejecting difficult edge crystals must not improve the quoted accuracy invisibly.
- Proposed offered-head spatial gate: the upper one-sided 95% seed-cluster bound on each axis of readout displacement RMS is at most that geometry's q/sqrt(12); bias is included. The displacement-weighted gate penalises multi-pixel errors. A single universal percentage mis-ID cannot express this. Require a specified weakest-crystal acceptance allocation too; recommend the author define it from intended Imaging count uniformity before selection, rather than adopting the historical 0.50/0.90 cuts.
- At 122/1332 keV, report per-crystal trigger/assignment/photopeak acceptance, conditional and unconditional error, robust and conventional full-energy energy width and gain fallback. A 662-only offer must say so; Sum50 does not imply low-energy identification quality.
- Compare CC/MLEM localisation with the **same geometry's** direct reference at equal accepted counts over in-FCFOV positions, plus equal emitted fluence to show efficiency loss. Report angular RMS, bias and source-grid/element scale; do not borrow near-field 0.406 mm. MLEM's current forward response omits readout migration.
- Rate results use the realised four-output pulse stream at the pinned rates, measuring throughput, piled photopeak migration and unknowns. Timing is a separate observation, not a physics eligibility gate.
- Numerical budget acceptance belongs to development: vary optical photons, DOI bins, calibration histories and histogram scale; require paired changes in the adopted spatial/energy metrics to fit an explicitly allocated numerical error budget (recommend at most 10% of the above spatial RMS allocation). Freeze the first adequate recipe before selection. This pilot does not prove 100k photons converged for a fine-pitch head or B.

## RP-6: Custom and resolved identity

Recommend Custom physical-readout eligibility by an **offered detector-module fingerprint**, not by the optics preset's display name or rank/cell/D. It must match qualified crystal count/pitch/gap/depth/material, sensor geometry, optical model, electronics and calibration policy. Changing mask geometry/source/focus does not physically change a uniform calibration and should not trigger an optical rebuild. It does change acquisition/projection identity and any angular-performance interpretation.

Only fingerprint combinations that the author actually offers are eligible. A Custom 30×30/0.6 head matching a qualified Sharp module gets the same design and independent Prepare; an arbitrary 31×31 or changed pitch/gap is refused as unqualified before expensive tracing. Successful calibration alone does **not** establish held-out acceptance or accuracy on an arbitrary Custom module. Changed absorber/backing must invalidate calibration; whether to allow such unqualified variants as explicitly experimental is an author decision. Preserve the existing Baseline scope until that decision is made.

Use resolved builder values rather than raw editor values in keys. Validate positive active width before work. Capture a UI-free immutable geometry/design record in preparation/snapshot: crystal counts/pitch/gap/depth; sensor counts/pitch/active width/offsets; guide/model; content/algorithm version and seed/budget policy. Circuit/Imaging/Waveform derive their dimensions from that acquired record. Never infer them from `sqrt(Peaks.Count)` because failure has no markers.

## RP-7: proposed requirement wording, tests and desktop changes

These are proposed texts; the planner applies them after decisions. Existing SR-DET-06…09 and SR-RUN-30…33 still apply. Add the resolved module/design identity to SR-DET-07's matching-cache promise and make SR-DET-08 name the separate scored/triggered/windowed/assigned denominators.

| Row | Proposed normative wording |
|---|---|
| SR-DET-05 replacement | Detector shall offer DirectCrystal by default and an explicitly experimental GAGG FourOutputAnger design for each qualified detector module. Each optics preset shall show its resolved readout design or an actionable measured refusal. Physical selection shall preserve all other input values. A matching successful independent preparation and its resolved module/design/calibration identity shall be required at Start and locked while acquiring or while data exist. IndependentSipm remains outside this Studio implementation. |
| SR-RUN-34 replacement | Physical Start shall refuse nonzero ambient/BSR, unqualified resolved detector/readout combinations and absent, failed or mismatched preparation before transport. Refusal shall preserve inputs and identify the required correction or measured failure. DirectCrystal shall retain its supported inputs and seeded stream. Engine default and microcell policy shall remain unchanged. |
| SR-DET-10 new | Physical preparation shall expose crystal and sensor dimensions, optical/model assumptions, calibration method, seed/budget policy, retained-candidate and ordered-marker counts, stage costs and independent validation denominators. A failed or unresolved calibration shall not publish a usable LUT or substitute truth/direct crystal assignment. Display geometry, event pixel coordinates and projected measured floods shall use the captured acquired module dimensions. |
| SR-DET-11 new | Custom physical selection shall be eligible only when its resolved detector-module fingerprint matches an offered qualified design. Other Custom combinations shall report unqualified geometry before preparation. A qualified match shall still require matching successful Prepare; calibration success shall not be represented as independent validation. |

Tests proposed for implementation (no product tests run here):

| Layer | Meaningful oracle/check |
|---|---|
| LUT | Curved monotone grid, compressed edges, unequal peak heights, extra continuum/noise maxima and missing/merged spots. Ensure one-to-one measured markers, deterministic tie-breaking, refusal for unsupported slots; no generated truth marker inserted. Fixed fixtures distinguish a row-sort fix from a candidate-selection fix. Keep legacy Baseline density/labels/markers/gains byte-equivalent except timing. |
| Engine evidence | Separate truth-independent calibration and held-out transport/response/ADC streams. Record failures rather than dropping them; test missing/corrupt/changed pins, incomplete families and mismatched binary/source closures. New derived-metric oracles use displacement counts and independent formulas, not a test copy of marker implementation. |
| Services | Each preset's qualified/refused outcome; count-based diagnostics and denominators; correct larger flood dimensions; edge/corner crystal IDs beyond 143 through Imaging and Waveform; independent acceptance/confusion populations; preparation match/mismatch, absorber/gap invalidation, cancellation and obsolete-result rejection. Immutable acquired geometry survives editing/reset/continuation. |
| Core | Name-independent Custom match; invalid active width; resolved clamp/key policy; status uses expected count even for failed preparation. Unsupported geometry preserves selected editor inputs. Baseline existing fixed-seed continuation/count identity remains exact. |
| Numerical solve/cache | Old versus four-adjoint DC weights and independent exported netlist, all four module sizes and finite input impedance, conditioning-derived round-off bounds. Network reuse does not share mutable response scratch or alter RNG progression. Content changes invalidate the correct artifact. |
| Detached render | Synthetic 30/34/44 grids, 6×6/8×8 sensors and circuit, ready/preparing/failed/acquired states, calibration versus live axes and overlays, both themes, 1280×800 and 1440×900. Verify first/last sensor positions, dynamic footprint scale and LUT alignment; failed state cannot index absent markers. No HWND or desktop input. |

The current map renderer loops every density bin and boundary on every render. High-res at 32 bins/crystal has 1408×1408 bins (~23.8 MB of density+int labels before copies); two cached artifacts and publication copies matter. Rasterise density and cache boundary geometry from the exact LUT on publication; use bounded redraw work and zoom/LOD for display only. Do not change the numerical LUT/bin budget to speed up drawing. Circuit layout must derive physical sensor and crystal extents, not parse sensor names into a fixed 15-unit Baseline diagram.

Desktop scenarios are changes to prepare, not run: move the old “Sharp refuses” case to an explicitly unqualified Custom module; keep nonzero ambient/BSR refusal separate. For every offered head, Prepare → acquired geometry → shared Imaging/Spectrum/Waveform counts/IDs → Stop/Continue/Reset and locked fields. Add a measured failing calibration case, matching/mismatched Custom cases, cancellation during long construction and cache reuse, plus large-grid tabs/zoom/first-and-last sensors. Preparation timeouts must reflect measured cost and stage progress, not retain the old unconditional 90 seconds. Each new scenario needs its intended break-verdict failure followed by recovery; desktop runs last on the author's go. No screenshot or synthetic render is physics evidence.

## Author questions and recommendations

| Decision | Recommendation |
|---|---|
| Must all presets succeed, or can TODO-41 finish with measured refusals? | Allow explicit refusals, as the reference plan's done-criterion already does. Do not promise a catalogue implementation for every 0.4–0.75 mm head. |
| Offer A or B? | Preserve Baseline A. Develop the topology-aware LUT for fine-head A as an explicitly virtual/unlimited-cell engineering model. Do not offer measured B as ready: its edge response is physically unresolved. A hardware-realistic B redesign would require a separate optical/guide/package study. |
| What is the engineering error allocation? | Adopt the proposed geometry-derived added angular RMS allocation, plus an explicit weakest-crystal acceptance requirement before selection. Decide whether the offer covers 662 keV only or the full line/rate matrix. Do not choose limits from the best pilot point. |
| Can arbitrary Custom modules be prepared? | Initially require a qualified detector-module fingerprint. Permit different mask/source/focus with the same module and label the changed geometry's performance scope. Calibration success alone is insufficient for arbitrary Custom support. |
| Can optical/network construction change? | First use immutable exact-content reuse and avoid the redundant Start solve. Four-adjoint solving is a justified exact-equation optimisation, but independently verify numerics before changing default artifacts. Reject finite “interior translation class” reuse as an exact shortcut. |
| Budget or algorithm first? | Freeze a development-only numerical-convergence recipe and one topology-aware method; then selection/confirmation on disjoint seeds. Do not ship the observed 16-bin Sharp success as validation. Keep all failed outcomes. |
| Preparation responsiveness/persistence? | Keep explicit Prepare with stage progress/cancellation and bounded cache. Add network cancellation and measure end-to-end costs under the author-controlled idle condition later. Persisted optical artifacts are optional; fingerprint every physical and numerical input. |

## Write audit, verification and approval requests

The commands below are an exhaustive inventory of write-producing invocations; complete command text/patch bodies are retained in the turn log. All scratch paths resolve below `%TEMP%\gcam-todo41\`. Repository write scope is only this review. No deletion/cleanup or installation was attempted.

1. Scratch setup PowerShell command: `New-Item -ItemType Directory` for scratch/source directories; `Copy-Item -LiteralPath` for source-only files from `src/Gcam.{Core,Configuration,Masks,Detector,Decoding,Simulation}`; `[IO.File]::WriteAllLines` for `sources.sha256`; `[IO.File]::WriteAllText` for `Probe.csproj` and package-source-clearing `NuGet.Config`. No project reference back to the repository.
2. `apply_patch` added scratch `Program.cs` (diagnostic A/B probe).
3. From scratch: `dotnet run --project Probe.csproj -c Release > pilot.log 2> pilot.err`. Restore/build wrote scratch obj/bin (package cache permitted); the program wrote/updated `results.json`, eight `*-flood.txt` rasters and eight `*-centres.json` files. Exit 0; no packages needed. The initial diagnostic binary is archived separately below.
4. While that already-built diagnostic ran, four `apply_patch` calls extended scratch source for later commands: added `StudyRunner.cs` and argument dispatch; added `rule-v1.md`/`run-family.ps1`; added `DetailRunner.cs`/selected-marker diagnostics; then included the Baseline detail control. These source edits did not change the running diagnostic binary or its measurements.
5. Seed-preparation PowerShell command: `[IO.File]::WriteAllText` created `study-seeds-v1.json` after checking all 84 proposed seeds against each other and existing seed lists. No family was launched.
6. Binary-archive PowerShell command: `New-Item` for fresh `pilot-bin`; `Copy-Item` from scratch Release bin; `[IO.File]::WriteAllText` for `pilot-binary-sha256.json`. This preserved the exact original diagnostic executable closure before the next build.
7. From scratch: `dotnet build Probe.csproj -c Release > study-build.log 2> study-build.err`. Exit 0, zero warnings/errors; updates scratch obj/bin only.
8. From scratch: `dotnet bin/Release/net9.0/Probe.dll details > details.log 2> details.err`. Replays A at seed 404, writes four `*-A-markers.json` files including marker identities/heights and transported input timers. Exit 0. It is a same-seed reproduction/detail probe, not an independent validation.
9. From scratch: `dotnet bin/Release/net9.0/Probe.dll prepare`. Writes `study-recipe-v1.json` and `study-smoke-v1.json`, exit 0; no study run.
10. Pin-preparation PowerShell command: `[IO.File]::WriteAllText` for `study-pin-v1.json`, binding scratch source files, copied engine sources, proposed recipe/rule/seeds/driver and current Release executable closure. No family was launched.
11. Smoke PowerShell command: `dotnet bin/Release/net9.0/Probe.dll study study-smoke-v1.json 41000001 study-smoke-result.json > study-smoke.log 2> study-smoke.err`, followed by `[IO.File]::WriteAllText` for `study-smoke-timing.json`. Exit 0; the program wrote the smoke result. This is the only new outer-seed study invocation.
12. `apply_patch` added and completed `docs/PLAN.Studio.ReadoutPresets.Review.md`, including a checked member-name correction and measured tables/commands/audit. Every repository patch in this turn targets this one file.

Read-only commands inspected code/docs, public primary-source papers, scratch outputs, git HEAD/status/diff and process-name/CPU counters. Some read commands failed on guessed file names, PowerShell wildcard usage or a formatting/parser error; they wrote nothing and were corrected. No forbidden folder, registry, credential store or unrelated process data was read. No external memory directory was read.

Only the diagnostic and detail replay used background command handles; they ran **sequentially**, without watchers, and both completed before this turn ends. The API smoke completed synchronously. No Studio/GUI/UI tests, product test suite or long evidence ensemble ran. The review is checked for LF, prohibited local-user paths and repository write scope. No idle-machine or physical-hardware validation is claimed.

### APPROVAL REQUESTS

None. No irreversible action is needed to complete this review.
