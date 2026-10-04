# PLAN.Physics.EvidenceRefresh.Review — audit and seed distributions for TODO-27

Scope: review R-1–R-4 against the current engine; inventory Monte Carlo quotations in all 33 Gcam evidence entries, README headlines/caption, PAPER.ko, and the associated plot annotations; measure their seed distributions. This is a review, not approval to implement or change a product requirement.

## Review outcome

Inventory: **33 deduplicated stochastic quotation families** (29 EV families + four PAPER/plan additions), with **865 expanded scalar rows** including related conditions and diagnostic quantities. All 33 EV entries are classified; EV-24/26/27/28 contain analytic quantities rather than MC estimates. README's four headlines and PAPER's numeric MC statements/companion figures are covered. This count is families, not 865 independently published sentences.

Propose distribution/recipe updates for **32 families**. P3's original gap series is not reproducible from the checked-in recipe and remains unverified; its replacement is labelled a different experiment. E04's historical wide-field input is also absent: the published default command and an explicitly defined new D20 variant are measured, but neither silently renews that archived recipe. **Eleven conclusion/interpretation flags** are sent to the planner below; P3 is an additional provenance decision. Unchanged qualitative conclusions still need seed-qualified numeric citations. These counts do not imply that every rounded central value moved.

R-1 is supported with isolated seed drivers and recipe metadata; R-2 needs discrete frequencies/censoring labels in addition to SD; R-3 must synchronise all repeated quotes and preserve old values; R-4 must resolve the flagged claims before implementation. No code/product/evidence changes are made in this turn.

## Basis and interpretation

The measurement build is main-tree `55af61591ab21eaa4c90c6dac74f91c14af8f765` (2026-10-02). The initial working tree already had a modified `docs/AGENTS.Todo.md` and an untracked reference plan. Neither was edited. The other worktree was not accessed. All probe programs, seeded JSON copies, binaries and raw outputs are under `%TEMP%/gcam-evidence-refresh-20261002/`; the only requested repository edit is this review.

Verified in code: `DefaultRandom.NextDouble` uses xoshiro256** / SplitMix64 and 53-bit doubles. There is no legacy-generator environment switch in this build. `RealizationRandom.For` offsets the config seed by 90,001; TODO-26 therefore changed the studies' repeated Poisson draws as well as the generator. The survey's temporary-switch numbers cannot be substituted for measurements of this build. `Program.cs` and the command handlers load the JSON `Seed`; there is no CLI `--seed` option. Several handlers ignore an optional output argument and write fixed `samples/...` paths relative to the process cwd.

An outer seed identifies a whole study, not one detected event. The SD below is the **sample SD of the study statistic across outer seeds**, not the RMS of individual localisation errors, the SE of its mean, or a confidence/tolerance guarantee. A mean-map study is still Monte Carlo. A peak that lands on the same grid cell in every measured run is **grid-stable in this ensemble**, not a deterministic transport quantity. Efficiencies and counts are effective weighted counts where appropriate.

Fixed manufactured-pattern seeds require special care: `MaskFabricationStudy` uses masks 100–105, `DetectorDefectStudy` uses maps 200–207, and `UniformityStudy` inherits the fixed `Detector.UniformitySeed`. Sweeping config `Seed` changes transport/counting noise, not those populations. The fixtures are the current xoshiro-generated maps at those numbered seeds; replacing the generator also changed the maps relative to LCG. Fixed seed labels do not prove that the pre-TODO-26 and current fixtures are identical. The proposed citations must retain that conditioning; a claim about arbitrary manufactured devices needs a second pattern-seed axis.

Code anchors checked for the recipe/estimator claims: [DefaultRandom](../../src/Gcam.Core/DefaultRandom.cs), [RealizationRandom](../../src/Gcam.Simulation/RealizationRandom.cs), [CLI entry](../../src/Gcam.Cli/Program.cs), [ThicknessStudy](../../src/Gcam.Simulation/ThicknessStudy.cs), [ShieldStudy](../../src/Gcam.Simulation/ShieldStudy.cs), [FieldOfViewStudy](../../src/Gcam.Simulation/FieldOfViewStudy.cs), [DepthDesignStudy](../../src/Gcam.Simulation/DepthDesignStudy.cs), [SceneConfigBuilder](../../src/Gcam.Configuration/SceneConfigBuilder.cs), and [FocusSweepService](../../src/Gcam.Studio.Services/FocusSweepService.cs). In particular, EffectiveRangeMm's first-failing-row branch returns rows[0].DistanceMm, which explains the150mm clamp.

## Complete quotation inventory

The unit of inventory is a **quotation family**: one result with its related scalar values and conditions, deduplicated across documents. Each row preserves the original numeric wording; separate measurement tables expand the scalar values. There are 29 EV families with stochastic numeric MC or synthetic-stimulus results (EV-17 also contains analytic and deterministic RTL data), four purely analytic entries outside MC scope, and four extra MC families in PAPER or the plan. Repeated appearances are listed below rather than counted as independent evidence. Configuration inputs, isotope energies, analytic identities, requirements/gates and deterministic test counts do not become Monte Carlo estimates merely by appearing beside a result.

`B` below means `samples/scenario.json`; `H` means `samples/scenario_handheld.json`; `O` means `samples/scenario_orig_gagg.json`. A command denotes the **current** CLI body executed with a seeded clone, in a private output directory. All stochastic rows were historically single outer-seed citations (normally 12345), even when the body already averaged hundreds of Poisson realisations or several fixed defect patterns. Exceptions and missing recipes are explicit.

| Family / entry | Original quote, including related numbers | Source command / origin; seed qualification | Other appearances to synchronise |
|---|---|---|---|
| E01 / EV-01 | centred ≈0.5 mm; 6 → 5.8 mm; ghost 12 → −6.6 mm; lab 282/148 of 625; hand-held 360/172; margin 1.35 | single `B`, `scenario_offaxis.json`, `scenario_ghost.json`; `sweep B`, `sweep H`; `H` margin. MC, one outer seed | README first caption + headline 1; Findings 1/2/22/52; `plot_sweep.py` (data-derived title) |
| E02 / EV-02 | NC x 1m/5m: 7/7.5°, 6/7°, 4/6.5°; cyclic 3/3.5°, 3/3°, 3/3°; diagonal NC 9/4.5°, 4.5/3°, 0.5/0.5°; low-count diagonal cyclic 2°; side cue 1–14.5°, 1.5–14°, 1.5–13°; outside flag 4–12.5°, 6–11.5°, never (68% at 9.5°); wrong spots 50–90%, ≤3% unflagged without BG / 10–45% with BG; ~60% in-field past 14° | `fov H`: N0=500/5000, BSR=0/1, 100 repeats, 0.5° sampling. All intervals are thresholded single-series realisations; FCFOV 3.64°/5.14° and 1.04° success gate are analytic/configuration | Findings 53; `plot_fov.py` |
| E03 / EV-03 | rank11/pitch1/D30 ±21.5mm, ≥90%; ~2.5× rank7/D60 ±8.6mm (93%); rank23/D20 0.149 usable, median65mm; thin0.5mm 0.752 at constant μt; 2mm cells/rank≥13 below40% | `scan B`; `ParameterScan.Run(...,11,200000)` and thin-slab variant. The published candidate and its gate are MC; global optimum needs the whole grid, not just its selected row | Findings 3; PAPER §2 still says ~7×; `plot_scan.py` (0.959 is an explicitly historical marker) |
| E04 / EV-04 | optimum8–10mm; edge/centre0.56 at8 →0.25 at32mm | `thickness B`, 60 repeats/radial point, 400k physical emitted budget, failure(err>resolution)<=0.5. The quoted edge ratios actually match archived `thickness_wide.csv` (resolution6mm), whose full input is absent; B gives different ratios. An explicit B clone with D20 is measured separately as a new wide-field recipe. “Below7mm leak>35%” is a separate analytic statement: with μ=.178/mm, 35% leakage is at5.90mm, and 7mm gives28.8%, not35% | Findings 4; PAPER §3 and repeated sweet-spot list/plain-language passages |
| E05 / EV-05 | 25mm straight edge/centre0.82 →0.99 at~4°; centre efficiency +35%; edge RMS~0.43mm | `masktaper B`, 400 effective counts/200 repeats; ideal acceptance model, not a true bevel solid | Findings 23; `plot_masktaper.py` hard-coded 0.82,0.99,0.43 |
| E06 / EV-06 | 6×6/0.8pixels per shadow:21%fail; 16×16/~2pixels per shadow halves12×12error | `array B`, fixed12mm width, 250 repeats. Array sizes and sampling ratios are inputs/geometry, failures and RMS ratio are MC | Findings 7; PAPER §4 |
| E07 / EV-07 | below~25counts collapse; from~50counts sub-mm; biasing within~1% using~100×fewer photons | `noise B`; 4π comparison is **not produced by noise**: `SimulationRunner`, same config with `Source.DirectionalBiasing=false` and enlarged history count. Count threshold and variance benefit need their own measured protocol | Findings 9; PAPER §8/plain-language; README engine description |
| E08 / EV-08 | step1.8mm:0.52→0.13mm; step2.4mm:0.66→0.19mm; ~4× | `subcell B`, source-position mean maps. `step/√12` is an analytic 1D uniform-cell floor, not an exact 2D RMS identity for every scene | Findings 43; PAPER §7 |
| E09 / EV-09 | H/O efficiencies2.48e−4/1.00e−4, ratio2.48×; H on-axis/edge floors0.24/0.53mm; 250/500counts0.26/0.55mm; O floor0.36mm; ~1s at1MBq; analytic2.44e−4 within2% | single H/O; `noise H`, `noise O` (5000-count row defines the finite-count “floor”). Time and analytic-to-MC ratios derive from measured efficiencies and .851 branching, not separate MC | Findings22/52; `plot_handheld_validation.py` retains older 2.45×,2.65e−4/1.08e−4,0.34/0.55mm |
| E10 / EV-10 | three sources0.55/0.27/0.93mm, each<1mm | `mixedfield B`: high-count mean map, K=3 known, fixed grid. Absence of Poisson resampling does not make photon transport seed-independent | Findings26/52; `plot_mixedfield.py` |
| E11 / EV-11 | resolves2–3mm vs cross-corr~3.5mm; at3mm valley0.82/0.17; cross min−103; FWHM1.76/2.50mm; bias0.87/0.55mm; “800kphotons” | **current command** `mlem B` uses1.5M photons and80iterations, not800k. Resolution criterion valley>.25, truth-centred/optimistic below2mm | README headline2; Findings48; PAPER §7; `mlem.csv`, `_profile.csv` |
| E12 / EV-12 | BSR2→4 RMS2.95→7.11mm, failures9→65%; calibrated subtraction matches antimask within0.07mm; floor throughBSR1 | `background B`; `antimask B`; `antimask-scene B`. .07 is a single-run max across tested levels, not a guaranteed method equivalence bound; directional-source image is a separate scene | Findings5/28; plot assets/data |
| E13 / EV-13 | 9mm off-axis:5mm crystal shift0.10mm,30mm shift0.30mm; zero on-axis | `doi B`, 2.5M rays, paired DOI/no-DOI maps. “Zero” is a symmetry statement about signed expectation; finite-sample signed decoded-position difference need not equal0 | Findings50; `doi.csv` |
| E14 / EV-14 | Argmax/per-pixel12/6%idealcounts (2×); RMS3.51/5.65mm; failure16/46%; budget400 | `compton B`: current stdout says400counts; body scales from ideal400 effective detected counts, leaving~50/~25accepted; it is **not400emittedphotons** | Findings15/52; `plot_compton.py`; related PAPER strategy conclusion |
| E15 / EV-15 | equal-budget Co window share~55%; spatial holdsCo×2, fails×4/×8 (Cs error8.75mm); oracle R=.469/error0–4%; mixed calibrated R=3.97/error0–11% from3truecounts | `compton B`, `compton-strip B`, `mixediso B`, `mixedstrip B`; supplementary matched scene at activity ratios1/2/4/8. Percent share and activity ratios are distinct quantities; errors quoted from weighted mean maps, not three observed Poisson counts | README headline3; Findings15/16/17/26/52; strip/mixed plot assets |
| E16 / EV-16 (synthetic stimulus MC) | FWHM15.8→16.2% | `rtl/multi_isotope_study.py`, Python/RTL RNG. NumPy field-seed sweep; noise fixture9 fixed; actual integrating RTL. Not xoshiro transport | Findings14; PAPER dynamic-range discussion |
| E17 / EV-17 (mixed grade) | C# GAGG5.7%; RTL6.4%,6.41/6.40%,cusp1.02/CRRC1.10/trap1.74%,40/~65%at2Mcps;59→119MHz | `frontend B` is a deterministic budget formula; no across-seed SD belongs on5.7%. Resolution/recovery widths are stochastic Python reference benchmarks and are remeasured with a fresh 1500-event CLI MC list plus timing/noise seeds. Synthesis MHz and bit-equality test counts remain deterministic. Frozen event-stream vectors are distinct from regenerated transport | Findings25/30–33; README headline4; PAPER §5/6 |
| E18 / EV-18 (mixed grade) | MC position robustness for one fixed pattern; energy7.4→18%at15%gainσ is RTL | `uniformity B` measures centred positional robustness over config seeds with the same manufactured pattern; `rtl/pixel_uniformity_study.py` owns the energy numbers; measured with fixed gain/decay fixture999 and pixel timing/noise seeds outer+i, 25 pixels, 500 events/pixel | Findings8; PAPER §4 |
| E19 / EV-19 | NaI.59,LaBr3.76,CeBr3.77,GAGG1.00,LYSO1.13,BGO1.23 ×10−4 | single `samples/materials/{name}.json`, each preset's actual anchored attenuation/depth. Material recommendation's ~4% resolution is a preset/literature input, not a measured transport SD | Findings6/52; PAPER §4 |
| E20 / EV-20 | Ir/Cs on-axis error.3/.4mm,margin1.15/1.16; H efficiencies2.62/2.48×10−4; ~7% analytic shortfall;1.07Mcps/kcps | `scenario_ir192.json`, `B`, `scenario_handheld_ir192.json`, `H`. 2.137γ/decay and evaluated line energies are decay-data inputs; count-rate extrapolation derives from MC ratio | Findings51/52 |
| E21 / EV-21 (synthetic stimulus MC) | GAGG.2%at1Mcps;CeBr3 87%at2Mcps;peakdetector~44%at1.5Mcps | Python/RTL `material_rate_study.py`, `run.sh`; NumPy stimulus-seed sweep; actual peak-detector RTL, not xoshiro | Findings10; PAPER §4/6 |
| E22 / EV-22 | nonpara912kcps at10Mcps;para366kcps at1Mcps and550cps at10Mcps | `deadtime B`, timed MC source/deposit train; analytic curves/τ1µs separately identified | Findings42; PAPER §8 gives equations only |
| E23 / EV-23 | heldout1.05/.90/1.13/.91/1.04/1.07; reference sources1.06/.91/1/.90/1.05;662angle.91/.82/.66/.40/.22/.08;60angle1.07/.79/.44/.11/.00;overrange.86/.53/.004 and corrected.90/.90/.90;everyfrontalenergy within13%;10°reading.11–.66;fails~150mSv/h | `dose H`: refit G(E) separately for each seed, then evaluate held-out quantities; energy/angle/source keys must be preserved. Dead-time collapse/correction limit also contain an analytic factor | Findings54; `plot_dose.py` |
| EV-24 (AN, excluded) | shieldmass2/3–3/4;6/12mm1.5/3kg;12→20mmhead2.7→3.4kg;206mm/.47L/CoM89mm | analytic Python design models | Findings22; PAPER §9/plain-language |
| E25 / EV-25 | scattered~8mm/~1.2kg;Cs~20mm/~5kg or12–15mmcalibrated;Co~30mm/~11kg;directional6→8mm;floor.34mm inplot | `shield B`: actual knee code chooses first row≤1.15×minimumRMS, RMS<3mm, failure<.10; report **pick frequencies** and selected masses, not a mean thickness. Calibrated thickness recipe must state its separate criterion | Findings22/28; `plot_shield.py` hardcodes6mm/.65kg,20mm/4.49kg,30mm/9.8kg and.34mm |
| EV-26 (AN, excluded) | b40mm→7.6°at.3m,2.3°at1m,2.2m crossover | analytic exact arctangent | Findings22 |
| EV-27 (AN, excluded) | 4/12W→8/24°C;335J/K;.7°C;pointing.83°;~240cps | analytic thermal/motion model | Findings22 |
| EV-28 (AN, excluded) | −1.8/−.25/−.17%per°C;3.2/25/36°C windows;referenceσ.3/2.5% | analytic SiPM model, not transport-MC sample estimates | Findings22 |
| E29 / EV-29 | counts100→90.6%,residual6.7%;comp99.9%/.1%;position~.6mm | `thermal B`: efficiency **normalised to t=0 within each arm**; residual is analytic sensitivity-weighted; RMS is Poisson/MC conditional on static uniformity map | Findings36; PAPER §8 |
| E30 / EV-30 | floor~.95mm throughσ40µm;80µm1.9mm,160µm3.6mm;PSR4.4→3.8;within~2×idealfloor→40µmrequirement | `maskfab B`: six fixed manufacturing patterns, repeated transport/Poisson seeds. Conditional results cannot guarantee an arbitrary manufacturing tolerance | Findings38; PAPER §3,summary/plain-language |
| E31 / EV-31 | maskoffset1mm→~2.5mmbias;registration~.4mmfor1mmbudget;spacing2mm→~.4mm;roll2°→~.1mm | `align B`: retain both total bias and vector change from aligned baseline. An analytic magnification2.667 is not the same as a measured residual-inclusive bias | Findings39 |
| E32 / EV-32 | repairedfloor.60→.68mm at0→8%bad;raw~1.2–1.8mm | `defects B`: eight fixed maps200–207, hot5×mean. Outer config seeds do not reselect these maps | Findings40; PAPER §4/summary/plain-language |
| E33 / EV-33 | S40/100/200→39/97/214mm;width~50→240mm;near-noisy sub-mm/farfloor~10mm atS150;lat~mm;resolution~z^1.5;18mmmask±10%range~.15m;viewer300–500mmbias+60–80onaxis/−20–−140offaxis;~700mm lowerbound | `depth B`, `depth-joint B`, `depth3d B`; `depthdesign` needs **Sharp rank13/.7mm/D80/30×30@.6** config for18mm aperture, not `depthdesign B` (14mm aperture). Viewer claim uses a separate headless list-mode/focus probe and must ship its actual recipe | Findings18/19/24/34/56/57; `plot_depth*.py` |
| P1 / PAPER §3 | MURA~4×cleaner thanrandomarray | `samples/open_fraction_study.py`: **Python MC** on a coding/noise model, NumPy RNG12345; 40arrays×40trials perρ; not the C# slab engine | Findings23; script plot title is data-derived |
| P2 / PAPER §4 | GAGG nonproportionality~1.5%@662keV, smaller than3–5%intrinsicfloor | `nonprop B`: 662keV/GAGG row; the3–5% total floor is an input/context value, not this experiment | Findings46 |
| P3 / PAPER §4 | gap0/20/40/100/200µm gives9/13/20/69/45%;optimum~100µm | **Original probe/config and denominator absent** from checked-in sources. A clearly labelled replacement experiment uses B,2Mphotons,PerPixelWindow±10%,contact.4,physicalGAGG; original numbers must not be compared as the same quantity until denominator/model is supplied | Findings35/56; PAPER summary/plain-language |
| P4 / plan + Findings44 | cascade log-log slope2.16 (other paragraph still2.04);18mmsumyield3.6e−6 | `cascade samples/scenario_co60.json`: current8Mdecays at18/24/32/43/57/76/100mm; regression excludes zero-sum rows, so the fitted slope is noisy/censoring-sensitive | Findings44/52; no EV entry; plan table's “2.05±.18 over24seeds” describes a different earlier probe unless its exact recipe is supplied |

README has **four headlines with MC content** (sweep, MLEM, isotope separation, mixed-grade front-end evidence) and a repeat of the sweep in its opening caption. Its fourth headline mixes stochastic waveform/recovery evidence with deterministic RTL bit matching and timing evidence; the stochastic parts are included and the deterministic parts retained as labelled fixtures. PAPER has no embedded numbered figures or image links: “figure” coverage here includes every numeric MC statement and the companion plot annotations, rather than assuming absent figures.

## Decisions proposed for implementation

Prefer a manifest-driven seed driver to a CLI flag alone. It must clone the complete JSON, overwrite only Seed, invoke the Release CLI by absolute path in an isolated cwd with a `samples/` subdirectory, capture stdout/stderr/exit status/timing and CSVs, and refuse to aggregate missing/failed runs. Output schemas/keys and counts must be checked before joining runs. Preserve stdout for stripping and MLEM metrics that are not exported in their CSVs. Record the engine commit, configuration hashes, seed list, history count, repeats, fixed pattern seeds, and metric/gate definitions next to aggregates. A seed flag alone leaves fixed-output-name collisions unresolved.

For Python coding MC, expose its existing NumPy seed as a process argument and collect results without generating 64 plots. Preserve its RNG family; do not describe NumPy results as xoshiro. For the viewer-depth and ambiguous gap results, add a checked-in, headless reproduce recipe before publishing a renewed precision claim. Publish aggregates as data files and have plot annotations read them; regenerate a representative single-seed image only with an explicit seed label. Do not replace an image with an unlabeled mean of incompatible reconstruction grids.

Use mean ± sample SD for smooth quantities, and median plus quantiles or pick frequencies for thresholded/skewed results. Never silently substitute SE for SD. Stable displayed grid positions should say how many seeds gave that position; SD=0 at printed precision is not proof of an analytic invariant. A condition-dependent statement must preserve the history/count budget, source geometry, decoder and manufacturing-pattern restriction. Old→new values, including moved values within the old spread, belong in the Evidence model-history row and the corresponding Findings correction; historical observations remain labelled as history.

The review does not authorise changing requirements, relabeling a coarse success gate as sub-mm precision, enlarging a physics tolerance, adding a legacy RNG switch, regenerating frozen RTL vectors, or running desktop verification. R-4 decisions are identified separately from mere moved digits.


## Reference-plan table verified against this build

| Plan row | Current measurement / disposition |
|---|---|
| EV-01 lab 282/148 | Seed12345 is **273/144**; across64: 278.14 ± 3.2653 (N=64); median 278 [276, 280.25] / 146 ± 1.2971 (N=64); median 146 [145, 147] of625. |
| EV-01 ghost −6.6mm | Seed12345 **−6.4006857mm**; -6.4016 ± 0.00080555 (N=32); median -6.4018 [-6.4022, -6.4009]. Moved, qualitative alias preserved. |
| EV-25 shield8/20/30mm | Seed12345 scattered8/Cs20/Co25. Over128: scattered8=109,10=19; Cs20=100,25=28; Co25=83,30=45. Plot6mm/.34mm needs the current recipe rather than reuse. |
| EV-32 .60→.68mm | Seed12345 .488→.518mm; 0.47505 ± 0.041348 (N=64); median 0.473 [0.442, 0.508] → 0.51013 ± 0.028685 (N=64); median 0.508 [0.49475, 0.518]. |
| Findings44 slope2.16 / prior2.05±.18 | Current8M-decay per-seed zero-censored fit: 1.7144 ± 0.45629 (N=128); median 1.63 [1.3675, 2.045]. Earlier24-seed spread cannot be borrowed; pooled fit is a separate estimand below. |
| EV-02 4.0° | The plan does not specify the row: x at1m,N0=500,BSR1 has the quoted4°; retain distance/direction/count/BG keys and frequencies below. It is a thresholded interval, not a continuous deterministic field angle. |
| EV-14 PPW5.65mm | Seed12345 **5.419mm**; complete budget/window comparison in the table below. |
| mixed-field R3.97 | Seed12345 **4.076**; 3.9882 ± 0.035404 (N=64); median 3.9825 [3.9688, 4.0103]. |



## Ensembles and reproducibility

The exact ordered seed lists are given here so a TEMP directory is not required to identify the experiment.

* O = `[12345] + [1000003 + 7919*i for i=0..126]`. Standard CLI families use its first64; noise/array/antimask/maskfab/shield/cascade use all128. Supplementary spatial ratios use128. Precise transport/bias/selected scan/gap/Sharp-depth/new-D20 thickness use first32; unrounded material efficiencies use first64; viewer and Python coding MC use first32.
* FOV uses first64 of F below. The old arithmetic seed progression overlaps `FOV`'s own7919 per-angle offsets, so a fresh hash-derived list was used and checked for within-study mean-map stream overlap. F = `[12345] + [1 + int.from_bytes(SHA256('gcam-todo27-20261002-'+str(i)).digest()[:4], 'little') % 1900000000 for i=0..126]`.
* RTL material uses O128; peak detector/front-end use O32. Pixel uses `[1000]+O[1:32]`, with manufactured gain/decay fixture999. Multi-isotope uses `[2027]+O[1:32]`, noise fixture9. These preserve original NumPy PCG64 / Python stdlib timing/noise generators; current C# eventstream inputs use xoshiro. Synthetic RTL is stochastic evidence but is not the C# photon-transport ensemble.
* Fixed study pattern seeds and internal resampling budgets are retained. This measures conditional transport/noise spread, not all-device fabrication variability. Driver writes a complete deep-cloned config before launching; it never constructs a partial `new SimulationConfig` clone.

O128: `12345, 1000003, 1007922, 1015841, 1023760, 1031679, 1039598, 1047517, 1055436, 1063355, 1071274, 1079193, 1087112, 1095031, 1102950, 1110869, 1118788, 1126707, 1134626, 1142545, 1150464, 1158383, 1166302, 1174221, 1182140, 1190059, 1197978, 1205897, 1213816, 1221735, 1229654, 1237573, 1245492, 1253411, 1261330, 1269249, 1277168, 1285087, 1293006, 1300925, 1308844, 1316763, 1324682, 1332601, 1340520, 1348439, 1356358, 1364277, 1372196, 1380115, 1388034, 1395953, 1403872, 1411791, 1419710, 1427629, 1435548, 1443467, 1451386, 1459305, 1467224, 1475143, 1483062, 1490981, 1498900, 1506819, 1514738, 1522657, 1530576, 1538495, 1546414, 1554333, 1562252, 1570171, 1578090, 1586009, 1593928, 1601847, 1609766, 1617685, 1625604, 1633523, 1641442, 1649361, 1657280, 1665199, 1673118, 1681037, 1688956, 1696875, 1704794, 1712713, 1720632, 1728551, 1736470, 1744389, 1752308, 1760227, 1768146, 1776065, 1783984, 1791903, 1799822, 1807741, 1815660, 1823579, 1831498, 1839417, 1847336, 1855255, 1863174, 1871093, 1879012, 1886931, 1894850, 1902769, 1910688, 1918607, 1926526, 1934445, 1942364, 1950283, 1958202, 1966121, 1974040, 1981959, 1989878, 1997797`.

FOV64: `12345, 640123487, 675790107, 854504903, 1878392845, 1398290582, 458931533, 1456846672, 1324837636, 706350884, 1184426798, 866188455, 1644189847, 423146067, 294867660, 48703761, 437549475, 688473057, 814791935, 1685601417, 1611695444, 1460453698, 1205294779, 276383961, 292367297, 802823065, 371874671, 877561449, 1858556915, 974171114, 729555374, 1628641851, 132956921, 974745992, 327764218, 1241199456, 746579409, 1098089244, 414718211, 1195445679, 54816871, 67080432, 808596871, 59267405, 517443920, 649213897, 1569120867, 161548287, 844901978, 316095225, 827386043, 1575357001, 1819841349, 446872370, 454817590, 225405640, 189988618, 1240906564, 1216435120, 1402361925, 319346937, 1136477137, 1253783089, 1315118003`.

TEMP entry points: `run.ps1`, `fast-run.ps1`, `special.ps1`, `probe/Program.cs`, `probe-v2/Program.cs`, `run-wide.ps1`, `materials-probe/Program.cs`, `viewer/Program.cs`, `run-viewer-current.ps1`, `rtl-probe.py`, `rtl-frontend.py`, `run-rtl.ps1`, `extend-material.ps1`, and `analyze.py`. Accepted per-run `done.json` has exit0; duplicate originals/cache runs are joined by seed, never counted twice. Failed pilots and the first spatial probe's empty tuple JSON are excluded. The renderer consumes the full-precision summary where available; original CSV/stdout precision remains a measurement-resolution limit elsewhere.

### Temporary decoder acceleration, equivalence and scope

The original Release CLI and a headless Studio.Services build succeeded with zero warnings/errors. Repeated decoding dominated the initial native runs. A copy of the decoder in TEMP caches the unchanged geometric coefficients, retaining their original row-major arithmetic order and zero/outside skips. The measurement executable otherwise contains the original CLI/engine assemblies. No physics/RNG/history budget is changed, and no decoder source in the repository is edited. Cached/native output comparisons (`cache-comparison.json`) have **212 matched files, zero mismatches**, plus explicit FOV seed12345 stdout and the new D20 thickness CSV equality. This is measured equivalence for those paired cases, not a proof for every conceivable config; the planner can rerun selected native cases independently. The production driver proposal does not require shipping this cache change as part of TODO-27.



## Spread stability and reporting precision

N was increased from initial32/64 to128 for seed-sensitive count thresholds, sampling, subtraction, mask fabrication, shield picks and sparse cascade fits; material-rate RTL was also expanded to128 after its first32 revealed that the historical GAGG .2% run was unrepresentative. Smooth transport/helper estimates retain32 or64. The complete tables expose first-half/second-half SD rather than hiding non-convergence. For approximately Gaussian independent statistics, the reference relative uncertainty of an SD is about13%,9%,6% atN32,64,128 respectively; this is a sampling reference, not a physical tolerance. Publish SD with **one or at most two significant digits**, not the five diagnostic digits retained in this review.

Below: seed-resampling bootstrap,2000 replicates,stdlib bootstrap seed27 for each metric. The interval describes uncertainty of the **estimated SD**, not a localisation acceptance bound. For multimodal/tail/threshold rows, prefer the provided median/IQR or pick frequencies; do not claim a universal stable Gaussian spread. Conditional subsets, especially censored FOV/viewer intervals and GAGG fits with too few peaks, retain their denominator. More seeds do not repair a wrong estimand or unknown recipe.

| Metric | N | SD | Bootstrap SD 2.5–97.5% | First / second half SD |
|---|---:|---:|---|---|
| `noise/noise.csv/centered/50/rms_error_mm` | 128 | 0.29178 | 0.25012–0.33197 | 0.28969 / 0.29245 |
| `noise/noise.csv/centered/250/rms_error_mm` | 128 | 0.013963 | 0.012373–0.01537 | 0.012933 / 0.014905 |
| `array/rms16_over12` | 128 | 0.25547 | 0.21524–0.29591 | 0.2717 / 0.23846 |
| `antimask/max_method_gap` | 128 | 0.19804 | 0.17495–0.21968 | 0.19499 / 0.20102 |
| `cascade/slope` | 128 | 0.45629 | 0.3845–0.52991 | 0.45567 / 0.4559 |
| `mixedstrip/R` | 64 | 0.035404 | 0.028557–0.041464 | 0.034575 / 0.036767 |
| `scan/11/1.00/30.0/usable_fraction` | 32 | 0.0073319 | 0.0054654–0.0089923 | 0.0050596 / 0.0086253 |
| `depthsharp/first150_fwhm_fraction` | 32 | 0.00081658 | 0.00063355–0.00095433 | 0.00083104 / 0.00082805 |
| `depthsharp/power` | 32 | 0.0012655 | 0.00092682–0.0015449 | 0.0011394 / 0.0014129 |
| `rtl_material/GAGG/efficiency` | 128 | 0.018818 | 0.016616–0.020763 | 0.016992 / 0.020613 |
| `rtl_material/CeBr3/efficiency` | 128 | 0.0081105 | 0.007244–0.0089438 | 0.0072748 / 0.0089019 |
| `rtl_frontend/full/fwhm` | 32 | 0.24657 | 0.16358–0.32077 | 0.25604 / 0.24407 |
| `rtl_frontend/cusp/noise_fwhm` | 32 | 0.031899 | 0.02283–0.039397 | 0.034063 / 0.030568 |
| `rtl_pixel/0.15/raw_fwhm` | 32 | 0.10767 | 0.080702–0.12782 | 0.099112 / 0.1137 |

The intervals above quantify the remaining spread uncertainty; an interval spanning materially different publication digits is a reason to round or use an empirical distribution, not to declare an arbitrary borrowed stability tolerance. Pick frequencies are observations with finite N; zero failures means zero observed, not zero probability.

Cascade alternative estimand: fit the log of pooled per-distance mean yields over128 seeds: slope **1.9743**. Of2000 seed-bootstrap replicates, **15** have a zero pooled sum-yield point and no logarithmic fit; among the1985 finite fits SD=0.098961, interval=1.8472–2.2635. These are **conditional** bootstrap statistics. No zero point is replaced by a pseudocount. This pooled result is compatible with the quadratic mechanism but cannot be substituted silently for the per-seed slope distribution.



## R-4 decisions requiring the planner's judgment

The following are changes to a claim's interpretation, not merely moved digits. Keep the old value as an observation with its model/seed; do not silently enlarge a tolerance.

1. **E07: 50 counts is not a sub-mm RMS threshold.** Distinguish coarse detection/5-mm success from sub-mm precision. Publish the 50/100/250-count RMS distributions and the fractions with RMS<1 mm; recommend 250 as the demonstrated robust sub-mm point for this lab recipe. Do not reinterpret the 5-mm success gate as 1-mm accuracy.
2. **E04: the default lab CLI first pick is 6 mm; the historical wide-field quote has a different recipe.** The command maximises usable radius and takes the first row on a tie, in ascending thickness order. The expanded table reports which thicknesses share the maximum. A 6-mm first pick is not proof of a unique physical optimum; 8–10 mm needs a stated design objective beyond this radius-only tie rule. Correct the separate analytic leakage statement (7 mm is 28.8% at the configured attenuation).
3. **E03: the rank-11 candidate's ≥90% claim is an observed gate, not a guaranteed property.** Publish the gate's pick/pass frequency; the several-fold gain remains useful. The full global scan was not repeated across seeds, so avoid calling the selected candidate a newly certified global optimum. PAPER's ~7× is historical thin-mask evidence and needs to move to the finite-slab statement.
4. **E06: “16×16 halves the error” needs replacement.** Retain the measured 6×6 failure rate and the 16/12 RMS ratio distribution, with the count budget. The seed-12345 ratio is worse than one. This does not invalidate the geometric samples-per-shadow calculation, but the quantitative performance claim cannot be retained as a universal sampling rule.
5. **E12: withdraw the 0.07-mm equivalence bound.** The measured difference between calibrated subtraction and antimask has a long tail. Publish both method distributions at each background level. Both suppress common-mode contamination, but a single-run maximum is not an equivalence margin and does not establish a physically movable mask as unnecessary for every budget/background.
6. **E25: Co-60 does not always require 30 mm, and directional leakage does not always move 6→8 mm.** Report selected-thickness/mass frequencies under the current knee rule. The tested Co picks still require several kg of this full enclosure: the design preference for coded separation remains supported, while “only at 30 mm” and the exact directional increment are removed. “Not carriable” must retain the author's mass criterion, not an invented one.
7. **E30: the 40-µm requirement is not identified by the stated “within twice ideal RMS” criterion.** The ideal RMS itself is seed-sensitive and there is no monotonic per-seed guarantee. Keep 40 µm only as an explicitly conservative author-selected manufacturing target, or choose a defined population/gate study. No arbitrary-device tolerance follows from six fixed patterns.
8. **P4: a per-seed zero-censored slope is not a robust confirmation of ε².** Publish its distribution and the zero-event incidence; distinguish a fit to pooled yields from the distribution of individual fits. A more precise validation needs more far-distance sum events or a specified pooled fit, not reuse of the earlier probe's SD.
9. **E33: qualify the depth scaling and censored widths.** The Sharp-configuration power fit is empirical over its six distances. At150mm the measured width already fails the FWHM/distance<=0.20 gate, yet EffectiveRangeMm returns the first sampled distance when the first row fails. The returned150mm is a clamp, not a passing precision limit: quote range<150mm until closer points locate the crossing. The lab plot's 240-mm plateau reaches the 20–260-mm search limits; it is a censored interval, not a resolved precision. The current viewer service and historical manual-plane probe are different estimators. Preserve the conclusion that depth is weak/bias-limited in the far field and avoid a fusion verdict.
10. **E17: separate present stimulus settings and deposition width from electronic noise.** The current reference defaults use rise1sample, and the electronic-only proxy selects MC deposits>620keV, so it can include physical Compton-tail width even with intrinsic smearing disabled. Renew the exact scripted estimator with its recipe and ordering frequencies; do not call every part of its width electronic ENC. Fixed RTL bit-match/fmax evidence remains fixed.

12. **P3: the original gap percentages cannot be renewed without their denominator/recipe.** The explicit replacement below has a different estimand. It supports a tested gap optimum, not identity with 9/13/20/69/45%. Retain the old series as unverified historical evidence until the author supplies the source, or replace it with a labelled new experiment after a decision.

11. **E15: the activity-ratio spatial claim is probabilistic.** Report the Cs localisation success frequency at Co:Cs=2 rather than assuming every high-count map is unaffected; the higher-ratio failure remains demonstrated. Do not interpret three effective counts in a weighted map as three observed events.

The MLEM 2–3-mm advantage, physical material efficiency ordering, high-count axial FOV extension, gamma-isotope stripping, thermal energy-window effect, dead-time collapse, and the need to distinguish depth bias from lateral accuracy retain their qualitative conclusions. The dose ±13% claim applies to the tested frontal energy grid, not all energies/angles or a regulatory accuracy specification.


## Reading the expanded measurements

`mlem/stdout/line2/n0,n1` are cross-correlation/MLEM bias; n2,n3 their minima; n4 is the literal zero in “≥0”; n5,n6 are cross/MLEM FWHM. `compton-strip/<scene>/n3,n4` are raw/stripped percentage error. `mixedfield/match5,6,7` are the three source errors in Cs/Co/Co57 order. Percent-bearing RTL width/recovery metrics use percent, so ADCdelta is percentage points; CSV efficiencies/failure rates are fractions unless their schema says otherwise. Do not mix these units.

`depth/plot_width` reproduces the plot's min/max-normalised half-range width;240mm reaches the20–260mm search window. `gap/relativeIdeal` is normalised to the explicit B basic/no-Compton ideal count, not an inferred denominator of the old series. `relativeZeroGapPPW` is a new supplementary normalisation. `maskfab/within2` is this seed's RMS≤twice this seed's ideal RMS. `noise/submm` and `spatial/co2_cs_submm` use an explicit RMS/error<1mm diagnostic; they do not replace the older coarse success gates.

Viewer rows distinguish the historical/manual81 planes150–950mm step10 from current headless `FocusSweepService` (81 reciprocal planes110–3000mm, default Sharp optics,500µCi,60s,z300/500/700,angles0/15/30mrad). Current-service censor/multimode frequencies are retained; finite interval widths alone do not describe a censored population. No desktop application was launched.

In RTL GAGG, FWHM is unavailable when insufficient peaks are reconstructed; the smaller finite-width N is conditional and is not an estimate from all128 runs. Pixel historical seed is1000 and multi-isotope2027, so their12345 column is absent by design. Original synthetic-stimulus seed12345 GAGG efficiency=.002 is preserved; compare it with the full distribution before quoting a representative efficiency.



## Expanded measurements and proposed numeric quotations

Each key starts with its command/mode from the inventory. CSV keys preserve the row parameters; the final component is the measured column. Values use the column units (mm, degrees, rates or fractions). The plus/minus term is sample SD. The 12345 column preserves the moved single realisation; a dash means not captured by that pilot. Median/IQR and frequencies are preferred for picks, skewed quantities and censored depth estimators. These are proposals, not implemented evidence.

| Command / metric key | N | Current seed 12345 | Mean and sample SD | Median [Q1, Q3] | SD first half / second half |
|---|---:|---|---|---|---|
| `align/align.csv/offset_x/0.000/bias_mm` | 64 | 0.294 | 0.27572 ± 0.019206 | 0.272 [0.261, 0.285] | 0.017111 / 0.021342 |
| `align/align.csv/offset_x/0.000/increment_mm` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `align/align.csv/offset_x/1.000/bias_mm` | 64 | 2.621 | 2.6194 ± 0.011383 | 2.618 [2.613, 2.6262] | 0.011049 / 0.011858 |
| `align/align.csv/offset_x/1.000/increment_mm` | 64 | 2.7485 | 2.6596 ± 0.042598 | 2.659 [2.6311, 2.6877] | 0.041254 / 0.044189 |
| `align/align.csv/offset_z/0.000/bias_mm` | 64 | 0.294 | 0.27572 ± 0.019206 | 0.272 [0.261, 0.285] | 0.017111 / 0.021342 |
| `align/align.csv/offset_z/0.000/increment_mm` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `align/align.csv/offset_z/1.000/bias_mm` | 64 | 0.343 | 0.35383 ± 0.026301 | 0.352 [0.33725, 0.37025] | 0.029861 / 0.022562 |
| `align/align.csv/offset_z/1.000/increment_mm` | 64 | 0.1319 | 0.21376 ± 0.053968 | 0.22022 [0.17239, 0.25831] | 0.049747 / 0.058684 |
| `align/align.csv/offset_z/2.000/bias_mm` | 64 | 0.463 | 0.48333 ± 0.032252 | 0.4845 [0.4605, 0.50425] | 0.031597 / 0.033352 |
| `align/align.csv/offset_z/2.000/increment_mm` | 64 | 0.28282 | 0.38399 ± 0.057527 | 0.38103 [0.34718, 0.42242] | 0.051449 / 0.063745 |
| `align/align.csv/roll_deg/0.000/bias_mm` | 64 | 0.294 | 0.27572 ± 0.019206 | 0.272 [0.261, 0.285] | 0.017111 / 0.021342 |
| `align/align.csv/roll_deg/0.000/increment_mm` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `align/align.csv/roll_deg/1.000/bias_mm` | 64 | 0.331 | 0.32869 ± 0.02025 | 0.33 [0.31525, 0.34025] | 0.021263 / 0.019442 |
| `align/align.csv/roll_deg/1.000/increment_mm` | 64 | 0.092698 | 0.075527 ± 0.032486 | 0.075571 [0.050766, 0.094171] | 0.0337 / 0.031744 |
| `align/align.csv/roll_deg/2.000/bias_mm` | 64 | 0.391 | 0.39266 ± 0.026001 | 0.3915 [0.371, 0.408] | 0.027693 / 0.024591 |
| `align/align.csv/roll_deg/2.000/increment_mm` | 64 | 0.13509 | 0.13391 ± 0.031384 | 0.13253 [0.10874, 0.15372] | 0.029917 / 0.033236 |
| `antimask/antimask.csv/bg=0 (ideal)/0.00/antimask_fail` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `antimask/antimask.csv/bg=0 (ideal)/0.00/antimask_rms_mm` | 128 | 0.306 | 0.30941 ± 0.0076059 | 0.3085 [0.30475, 0.31325] | 0.0076801 / 0.0075443 |
| `antimask/antimask.csv/bg=0 (ideal)/0.00/calib_fail` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `antimask/antimask.csv/bg=0 (ideal)/0.00/calib_rms_mm` | 128 | 0.371 | 0.3828 ± 0.0093391 | 0.383 [0.37675, 0.388] | 0.0097916 / 0.0089395 |
| `antimask/antimask.csv/bg=0.5/px/0.50/antimask_fail` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `antimask/antimask.csv/bg=0.5/px/0.50/antimask_rms_mm` | 128 | 0.316 | 0.32542 ± 0.010524 | 0.325 [0.317, 0.333] | 0.0093605 / 0.011637 |
| `antimask/antimask.csv/bg=0.5/px/0.50/calib_fail` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `antimask/antimask.csv/bg=0.5/px/0.50/calib_rms_mm` | 128 | 0.375 | 0.39795 ± 0.011946 | 0.3975 [0.389, 0.407] | 0.012659 / 0.01093 |
| `antimask/antimask.csv/bg=1/px/1.00/antimask_fail` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `antimask/antimask.csv/bg=1/px/1.00/antimask_rms_mm` | 128 | 0.363 | 0.34337 ± 0.012053 | 0.341 [0.33575, 0.351] | 0.012062 / 0.012138 |
| `antimask/antimask.csv/bg=1/px/1.00/calib_fail` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `antimask/antimask.csv/bg=1/px/1.00/calib_rms_mm` | 128 | 0.449 | 0.4248 ± 0.015461 | 0.4245 [0.412, 0.434] | 0.014957 / 0.016066 |
| `antimask/antimask.csv/bg=2/px/2.00/antimask_fail` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `antimask/antimask.csv/bg=2/px/2.00/antimask_rms_mm` | 128 | 0.365 | 0.3764 ± 0.01578 | 0.374 [0.365, 0.387] | 0.015566 / 0.016094 |
| `antimask/antimask.csv/bg=2/px/2.00/calib_fail` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `antimask/antimask.csv/bg=2/px/2.00/calib_rms_mm` | 128 | 0.485 | 0.46488 ± 0.017576 | 0.467 [0.45275, 0.477] | 0.018869 / 0.016155 |
| `antimask/antimask.csv/bg=4/px/4.00/antimask_fail` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `antimask/antimask.csv/bg=4/px/4.00/antimask_rms_mm` | 128 | 0.474 | 0.44009 ± 0.017147 | 0.441 [0.43075, 0.452] | 0.016311 / 0.017757 |
| `antimask/antimask.csv/bg=4/px/4.00/calib_fail` | 128 | 0 | 0.00027344 ± 0.0011413 | 0 [0, 0] | 0.0014689 / 0.000625 |
| `antimask/antimask.csv/bg=4/px/4.00/calib_rms_mm` | 128 | 0.553 | 0.5548 ± 0.070814 | 0.541 [0.52675, 0.555] | 0.091313 / 0.039705 |
| `antimask/antimask.csv/bg=8/px/8.00/antimask_fail` | 128 | 0.01 | 0.001875 ± 0.0030092 | 0 [0, 0.005] | 0.0031644 / 0.0028684 |
| `antimask/antimask.csv/bg=8/px/8.00/antimask_rms_mm` | 128 | 1.058 | 0.62859 ± 0.14264 | 0.5645 [0.538, 0.67675] | 0.12739 / 0.15707 |
| `antimask/antimask.csv/bg=8/px/8.00/calib_fail` | 128 | 0.01 | 0.00625 ± 0.0056121 | 0.005 [0, 0.01] | 0.0051749 / 0.0060581 |
| `antimask/antimask.csv/bg=8/px/8.00/calib_rms_mm` | 128 | 1.086 | 0.91133 ± 0.23265 | 0.912 [0.66925, 1.067] | 0.22329 / 0.24267 |
| `antimask/antimask.csv/gradient ~4/4.00/antimask_fail` | 128 | 0 | 3.9063e-05 ± 0.00044194 | 0 [0, 0] | 0 / 0.000625 |
| `antimask/antimask.csv/gradient ~4/4.00/antimask_rms_mm` | 128 | 0.407 | 0.41588 ± 0.026208 | 0.4135 [0.401, 0.42725] | 0.01714 / 0.032454 |
| `antimask/antimask.csv/gradient ~4/4.00/calib_fail` | 128 | 0 | 0.00027344 ± 0.0011413 | 0 [0, 0] | 0.0012199 / 0.0010652 |
| `antimask/antimask.csv/gradient ~4/4.00/calib_rms_mm` | 128 | 0.499 | 0.52587 ± 0.091085 | 0.505 [0.49275, 0.52025] | 0.10558 / 0.074398 |
| `antimask/max_method_gap` | 128 | 0.12 | 0.35333 ± 0.19804 | 0.349 [0.16725, 0.477] | 0.19499 / 0.20102 |
| `array/16_better_than12` | 128 | 0 | 0.89062 ± 0.31334 | 1 [1, 1] | 0.33333 / 0.29378 |
| `array/array.csv/12/fail_rate` | 128 | 0.008 | 0.022031 ± 0.0090214 | 0.02 [0.016, 0.028] | 0.0086316 / 0.0093868 |
| `array/array.csv/12/rms_mm` | 128 | 1.099 | 1.4675 ± 0.27085 | 1.4775 [1.2735, 1.6565] | 0.2753 / 0.26377 |
| `array/array.csv/16/fail_rate` | 128 | 0.02 | 0.009625 ± 0.0062984 | 0.008 [0.004, 0.012] | 0.0069454 / 0.0056217 |
| `array/array.csv/16/rms_mm` | 128 | 1.314 | 0.95506 ± 0.25367 | 0.926 [0.76525, 1.1792] | 0.2574 / 0.25191 |
| `array/array.csv/20/fail_rate` | 128 | 0.008 | 0.0059375 ± 0.0049688 | 0.004 [0.004, 0.008] | 0.0043347 / 0.0055631 |
| `array/array.csv/20/rms_mm` | 128 | 0.943 | 0.75345 ± 0.24977 | 0.735 [0.58125, 0.92875] | 0.22066 / 0.27761 |
| `array/array.csv/24/fail_rate` | 128 | 0.008 | 0.0071875 ± 0.0051033 | 0.008 [0.004, 0.012] | 0.0049377 / 0.005296 |
| `array/array.csv/24/rms_mm` | 128 | 0.886 | 0.80099 ± 0.26255 | 0.8255 [0.6175, 1.0172] | 0.25827 / 0.2688 |
| `array/array.csv/30/fail_rate` | 128 | 0.004 | 0.0026875 ± 0.0033338 | 0 [0, 0.004] | 0.0035074 / 0.0030237 |
| `array/array.csv/30/rms_mm` | 128 | 0.636 | 0.49249 ± 0.2448 | 0.3335 [0.292, 0.65325] | 0.26429 / 0.21212 |
| `array/array.csv/6/fail_rate` | 128 | 0.236 | 0.19994 ± 0.025148 | 0.2 [0.184, 0.217] | 0.024162 / 0.026216 |
| `array/array.csv/6/rms_mm` | 128 | 4.371 | 3.8525 ± 0.27721 | 3.8515 [3.7147, 4.038] | 0.2669 / 0.28681 |
| `array/array.csv/8/fail_rate` | 128 | 0.016 | 0.029125 ± 0.011889 | 0.028 [0.02, 0.036] | 0.012259 / 0.011576 |
| `array/array.csv/8/rms_mm` | 128 | 1.539 | 1.6162 ± 0.2674 | 1.613 [1.4455, 1.7835] | 0.26653 / 0.26662 |
| `array/rms16_over12` | 128 | 1.1956 | 0.68191 ± 0.25547 | 0.62974 [0.49183, 0.82148] | 0.2717 / 0.23846 |
| `background/background_gradient.csv/1.000/fail_rate` | 64 | 0 | 0.0035156 ± 0.0042426 | 0 [0, 0.005] | 0.0039624 / 0.0045348 |
| `background/background_gradient.csv/1.000/rms_mm` | 64 | 0.583 | 0.77942 ± 0.2256 | 0.62 [0.59675, 0.8875] | 0.22825 / 0.22435 |
| `background/background_gradient.csv/2.000/fail_rate` | 64 | 0.215 | 0.20703 ± 0.025083 | 0.21 [0.19, 0.225] | 0.025509 / 0.025008 |
| `background/background_gradient.csv/2.000/rms_mm` | 64 | 4.453 | 4.4867 ± 0.29495 | 4.517 [4.32, 4.7058] | 0.30462 / 0.28889 |
| `background/background_gradient.csv/4.000/fail_rate` | 64 | 0.93 | 0.91609 ± 0.017806 | 0.92 [0.90375, 0.93] | 0.01959 / 0.016102 |
| `background/background_gradient.csv/4.000/rms_mm` | 64 | 9.736 | 9.6212 ± 0.12741 | 9.6105 [9.5548, 9.711] | 0.12804 / 0.12652 |
| `background/background_sweep.csv/1.000/fail_rate` | 64 | 0 | 0.0014062 ± 0.0027413 | 0 [0, 0] | 0.0023546 / 0.0030454 |
| `background/background_sweep.csv/1.000/rms_mm` | 64 | 0.663 | 0.75523 ± 0.12615 | 0.7055 [0.67875, 0.73875] | 0.11772 / 0.133 |
| `background/background_sweep.csv/2.000/fail_rate` | 64 | 0.12 | 0.095547 ± 0.019541 | 0.095 [0.085, 0.11] | 0.019675 / 0.018654 |
| `background/background_sweep.csv/2.000/rms_mm` | 64 | 3.269 | 2.8942 ± 0.28547 | 2.907 [2.686, 3.0938] | 0.26993 / 0.2872 |
| `background/background_sweep.csv/4.000/fail_rate` | 64 | 0.705 | 0.66375 ± 0.038586 | 0.6625 [0.63375, 0.69625] | 0.041005 / 0.036632 |
| `background/background_sweep.csv/4.000/rms_mm` | 64 | 7.525 | 7.3383 ± 0.23101 | 7.3335 [7.153, 7.5175] | 0.22569 / 0.23968 |
| `bias/ratio` | 32 | 1.0048 | 0.99928 ± 0.0061562 | 0.99812 [0.99431, 1.0039] | 0.0060351 / 0.0063286 |
| `cascade/cascade.csv/18.0/sum_peak_per_decay` | 128 | 4.25e-06 | 4.2686e-06 ± 7.1869e-07 | 4.25e-06 [3.75e-06, 4.7813e-06] | 6.9159e-07 / 7.3361e-07 |
| `cascade/nonzero_points` | 128 | 6 | 4.3125 ± 0.85803 | 4 [4, 5] | 0.79433 / 0.90633 |
| `cascade/slope` | 128 | 1.41 | 1.7144 ± 0.45629 | 1.63 [1.3675, 2.045] | 0.45567 / 0.4559 |
| `cascade/zero_points` | 128 | 1 | 2.6875 ± 0.85803 | 3 [2, 3] | 0.79433 / 0.90633 |
| `compton-strip/R` | 64 | 0.469 | 0.46856 ± 0.0014015 | 0.469 [0.468, 0.47] | 0.0014591 / 0.0013619 |
| `compton-strip/co-located/n3` | 64 | 403 | 402.08 ± 1.6456 | 402 [401, 403.25] | 1.6547 / 1.6286 |
| `compton-strip/co-located/n4` | 64 | 1 | 0.75 ± 0.43644 | 1 [0.75, 1] | 0.47093 / 0.39656 |
| `compton-strip/separated/n3` | 64 | 403 | 401.53 ± 1.6994 | 401 [400, 403] | 1.7958 / 1.6261 |
| `compton-strip/separated/n4` | 64 | 4 | 4.125 ± 0.33333 | 4 [4, 4] | 0.33601 / 0.33601 |
| `compton/compton_strategies.csv/AntiCoincidence/bias_mm` | 64 | 0.352 | 0.3452 ± 0.0040205 | 0.345 [0.342, 0.348] | 0.0038938 / 0.0041551 |
| `compton/compton_strategies.csv/AntiCoincidence/efficiency_rel` | 64 | 0.06 | 0.06 ± 0.00043644 | 0.06 [0.06, 0.06] | 0.00053788 / 0.00030946 |
| `compton/compton_strategies.csv/AntiCoincidence/fail_rate` | 64 | 0.385 | 0.39406 ± 0.034294 | 0.395 [0.37, 0.42] | 0.029538 / 0.038915 |
| `compton/compton_strategies.csv/AntiCoincidence/rms_mm` | 64 | 5.224 | 5.4124 ± 0.32516 | 5.413 [5.2257, 5.6185] | 0.27069 / 0.37157 |
| `compton/compton_strategies.csv/Argmax/bias_mm` | 64 | 0.37 | 0.36759 ± 0.0037149 | 0.3675 [0.36475, 0.37] | 0.0036184 / 0.0037503 |
| `compton/compton_strategies.csv/Argmax/efficiency_rel` | 64 | 0.122 | 0.12253 ± 0.00056256 | 0.1225 [0.122, 0.123] | 0.00056796 / 0.0005644 |
| `compton/compton_strategies.csv/Argmax/fail_rate` | 64 | 0.18 | 0.17969 ± 0.028645 | 0.18 [0.16, 0.195] | 0.030147 / 0.027536 |
| `compton/compton_strategies.csv/Argmax/rms_mm` | 64 | 3.518 | 3.6146 ± 0.32356 | 3.6285 [3.4068, 3.8135] | 0.33886 / 0.31284 |
| `compton/compton_strategies.csv/Centroid/bias_mm` | 64 | 0.412 | 0.40259 ± 0.0053056 | 0.403 [0.399, 0.405] | 0.0044753 / 0.0055344 |
| `compton/compton_strategies.csv/Centroid/efficiency_rel` | 64 | 0.122 | 0.12253 ± 0.00056256 | 0.1225 [0.122, 0.123] | 0.00056796 / 0.0005644 |
| `compton/compton_strategies.csv/Centroid/fail_rate` | 64 | 0.395 | 0.31 ± 0.03118 | 0.31 [0.28375, 0.33] | 0.031926 / 0.029971 |
| `compton/compton_strategies.csv/Centroid/rms_mm` | 64 | 5.165 | 4.4843 ± 0.2754 | 4.4555 [4.3193, 4.657] | 0.25848 / 0.29534 |
| `compton/compton_strategies.csv/PerPixelWindow/bias_mm` | 64 | 0.353 | 0.3467 ± 0.0040461 | 0.347 [0.343, 0.35] | 0.0038222 / 0.0042558 |
| `compton/compton_strategies.csv/PerPixelWindow/efficiency_rel` | 64 | 0.061 | 0.061219 ± 0.00045316 | 0.061 [0.061, 0.061] | 0.00047093 / 0.00043994 |
| `compton/compton_strategies.csv/PerPixelWindow/fail_rate` | 64 | 0.415 | 0.40359 ± 0.03778 | 0.405 [0.36875, 0.435] | 0.03845 / 0.037618 |
| `compton/compton_strategies.csv/PerPixelWindow/rms_mm` | 64 | 5.419 | 5.4563 ± 0.28817 | 5.4335 [5.2183, 5.6935] | 0.29016 / 0.28614 |
| `compton/contamination_pct` | 64 | 56 | 55.297 ± 0.46049 | 55 [55, 56] | 0.39656 / 0.49899 |
| `deadtime/deadtime.csv/1000000/analytic_nonpara_cps` | 64 | 4.9957e+05 | 5.001e+05 ± 600.86 | 5.0007e+05 [4.9958e+05, 5.0055e+05] | 637.57 / 571.87 |
| `deadtime/deadtime.csv/1000000/analytic_para_cps` | 64 | 3.6788e+05 | 3.6788e+05 ± 1.3183 | 3.6788e+05 [3.6788e+05, 3.6788e+05] | 1.4615 / 1.176 |
| `deadtime/deadtime.csv/1000000/live_nonpara` | 64 | 0.5022 | 0.50095 ± 0.00081435 | 0.50105 [0.50048, 0.50143] | 0.00063217 / 0.00096912 |
| `deadtime/deadtime.csv/1000000/live_para` | 64 | 0.3669 | 0.36623 ± 0.0011106 | 0.3664 [0.3655, 0.36702] | 0.00088107 / 0.0013143 |
| `deadtime/deadtime.csv/1000000/r_tau` | 64 | 0.9983 | 1.0004 ± 0.0024025 | 1.0002 [0.9983, 1.0022] | 0.0025526 / 0.0022829 |
| `deadtime/deadtime.csv/1000000/rec_nonpara_cps` | 64 | 5.0135e+05 | 5.0115e+05 ± 840.63 | 5.0133e+05 [5.0072e+05, 5.0171e+05] | 979.14 / 688.24 |
| `deadtime/deadtime.csv/1000000/rec_para_cps` | 64 | 3.6632e+05 | 3.6638e+05 ± 764.39 | 3.6636e+05 [3.6594e+05, 3.6674e+05] | 630.81 / 887.6 |
| `deadtime/deadtime.csv/10000000/analytic_nonpara_cps` | 64 | 9.0895e+05 | 9.0912e+05 ± 198.55 | 9.0911e+05 [9.0895e+05, 9.0927e+05] | 210.61 / 189.04 |
| `deadtime/deadtime.csv/10000000/analytic_para_cps` | 64 | 461 | 452.42 ± 9.7895 | 453 [445, 461] | 10.432 / 9.2667 |
| `deadtime/deadtime.csv/10000000/live_nonpara` | 64 | 0.0915 | 0.091216 ± 0.00019698 | 0.0912 [0.091, 0.0914] | 0.00020038 / 0.0001959 |
| `deadtime/deadtime.csv/10000000/live_para` | 64 | 0.0001 | 5.4688e-05 ± 5.0173e-05 | 0.0001 [0, 0.0001] | 4.9899e-05 / 5.08e-05 |
| `deadtime/deadtime.csv/10000000/r_tau` | 64 | 9.983 | 10.004 ± 0.024048 | 10.003 [9.9833, 10.022] | 0.025517 / 0.022888 |
| `deadtime/deadtime.csv/10000000/rec_nonpara_cps` | 64 | 9.1319e+05 | 9.1251e+05 ± 661.23 | 9.125e+05 [9.1203e+05, 9.1298e+05] | 748.68 / 562.57 |
| `deadtime/deadtime.csv/10000000/rec_para_cps` | 64 | 599 | 501.06 ± 141.32 | 500 [400, 599.25] | 157.69 / 125.37 |
| `defects/defects.csv/0.00/rms_corrected_mm` | 64 | 0.488 | 0.47505 ± 0.041348 | 0.473 [0.442, 0.508] | 0.04463 / 0.037978 |
| `defects/defects.csv/0.00/rms_raw_mm` | 64 | 0.488 | 0.47505 ± 0.041348 | 0.473 [0.442, 0.508] | 0.04463 / 0.037978 |
| `defects/defects.csv/1.00/rms_corrected_mm` | 64 | 0.487 | 0.47475 ± 0.016454 | 0.474 [0.463, 0.484] | 0.016105 / 0.016928 |
| `defects/defects.csv/1.00/rms_raw_mm` | 64 | 0.56 | 0.55766 ± 0.02775 | 0.5545 [0.54275, 0.56275] | 0.035185 / 0.016885 |
| `defects/defects.csv/2.00/rms_corrected_mm` | 64 | 0.486 | 0.47809 ± 0.021884 | 0.474 [0.46775, 0.486] | 0.011713 / 0.02855 |
| `defects/defects.csv/2.00/rms_raw_mm` | 64 | 0.753 | 0.65436 ± 0.072329 | 0.6135 [0.596, 0.69125] | 0.071057 / 0.074411 |
| `defects/defects.csv/4.00/rms_corrected_mm` | 64 | 0.495 | 0.48297 ± 0.021398 | 0.48 [0.473, 0.49125] | 0.012944 / 0.027399 |
| `defects/defects.csv/4.00/rms_raw_mm` | 64 | 0.48 | 0.52073 ± 0.062246 | 0.4885 [0.4645, 0.573] | 0.058936 / 0.066193 |
| `defects/defects.csv/8.00/rms_corrected_mm` | 64 | 0.518 | 0.51013 ± 0.028685 | 0.508 [0.49475, 0.518] | 0.031093 / 0.026507 |
| `defects/defects.csv/8.00/rms_raw_mm` | 64 | 1.276 | 1.1461 ± 0.11924 | 1.1595 [1.0545, 1.2223] | 0.12472 / 0.11528 |
| `depth-joint/depth_joint.csv/offaxis_far/1000/depth_bias_mm` | 64 | -15.71 | -13.932 ± 1.9415 | -13.775 [-15.425, -12.545] | 1.9716 / 1.8134 |
| `depth-joint/depth_joint.csv/offaxis_far/1000/depth_rms_mm` | 64 | 24.11 | 20.108 ± 3.8421 | 20.51 [16.828, 22.847] | 3.9225 / 3.6468 |
| `depth-joint/depth_joint.csv/offaxis_far/1000/lateral_rms_mm` | 64 | 1.293 | 1.0791 ± 0.45296 | 0.822 [0.694, 1.3728] | 0.44942 / 0.46271 |
| `depth-joint/depth_joint.csv/offaxis_far/3000/depth_bias_mm` | 64 | -12.09 | -10.763 ± 1.0329 | -10.78 [-11.31, -9.9675] | 0.97745 / 1.0818 |
| `depth-joint/depth_joint.csv/offaxis_far/3000/depth_rms_mm` | 64 | 14.51 | 12.93 ± 1.4962 | 12.95 [12.027, 13.58] | 1.184 / 1.7719 |
| `depth-joint/depth_joint.csv/offaxis_far/3000/lateral_rms_mm` | 64 | 0.654 | 0.61489 ± 0.11189 | 0.6 [0.5845, 0.62] | 0.029584 / 0.1558 |
| `depth-joint/depth_joint.csv/offaxis_near/1000/depth_bias_mm` | 64 | 4.9 | 4.5394 ± 4.2481 | 5.235 [1.095, 7.56] | 4.0505 / 4.5009 |
| `depth-joint/depth_joint.csv/offaxis_near/1000/depth_rms_mm` | 64 | 30.06 | 29.093 ± 2.0348 | 28.89 [27.865, 30.06] | 1.5467 / 2.399 |
| `depth-joint/depth_joint.csv/offaxis_near/1000/lateral_rms_mm` | 64 | 3.729 | 3.626 ± 0.45253 | 3.6675 [3.3755, 3.8628] | 0.41951 / 0.4843 |
| `depth-joint/depth_joint.csv/offaxis_near/3000/depth_bias_mm` | 64 | -2.3 | 1.0398 ± 4.0316 | 1.2 [-1.5825, 3.9] | 4.0713 / 4.0286 |
| `depth-joint/depth_joint.csv/offaxis_near/3000/depth_rms_mm` | 64 | 22.1 | 25.745 ± 2.1623 | 25.735 [24.523, 27.453] | 2.1115 / 2.2458 |
| `depth-joint/depth_joint.csv/offaxis_near/3000/lateral_rms_mm` | 64 | 3.197 | 3.1672 ± 0.44835 | 3.1235 [2.914, 3.443] | 0.44491 / 0.45125 |
| `depth-joint/depth_joint.csv/onaxis_far/1000/depth_bias_mm` | 64 | -8.58 | -7.7625 ± 1.1298 | -7.84 [-8.585, -7.265] | 1.1777 / 1.0981 |
| `depth-joint/depth_joint.csv/onaxis_far/1000/depth_rms_mm` | 64 | 11.69 | 11.549 ± 0.78354 | 11.39 [10.91, 11.963] | 0.78639 / 0.77379 |
| `depth-joint/depth_joint.csv/onaxis_far/1000/lateral_rms_mm` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `depth-joint/depth_joint.csv/onaxis_far/3000/depth_bias_mm` | 64 | -9.87 | -9.8438 ± 0.34434 | -9.885 [-10.082, -9.6225] | 0.32202 / 0.36809 |
| `depth-joint/depth_joint.csv/onaxis_far/3000/depth_rms_mm` | 64 | 10.15 | 10.179 ± 0.21959 | 10.14 [10.045, 10.322] | 0.19934 / 0.24053 |
| `depth-joint/depth_joint.csv/onaxis_far/3000/lateral_rms_mm` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `depth-joint/depth_joint.csv/onaxis_near/1000/depth_bias_mm` | 64 | -0.44 | -0.43625 ± 0.084731 | -0.44 [-0.49, -0.38] | 0.085563 / 0.085185 |
| `depth-joint/depth_joint.csv/onaxis_near/1000/depth_rms_mm` | 64 | 0.74 | 0.76797 ± 0.13059 | 0.77 [0.6875, 0.8425] | 0.13668 / 0.12556 |
| `depth-joint/depth_joint.csv/onaxis_near/1000/lateral_rms_mm` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `depth-joint/depth_joint.csv/onaxis_near/3000/depth_bias_mm` | 64 | -0.15 | -0.18078 ± 0.037514 | -0.18 [-0.2, -0.1575] | 0.04368 / 0.030559 |
| `depth-joint/depth_joint.csv/onaxis_near/3000/depth_rms_mm` | 64 | 0.28 | 0.33437 ± 0.054971 | 0.33 [0.2975, 0.37] | 0.057796 / 0.052302 |
| `depth-joint/depth_joint.csv/onaxis_near/3000/lateral_rms_mm` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `depth/estimate/100.0` | 64 | 97.34 | 97.365 ± 0.034778 | 97.37 [97.34, 97.39] | 0.039867 / 0.028277 |
| `depth/estimate/150.0` | 64 | 140 | 140.01 ± 0.017548 | 140 [140, 140.01] | 0.019917 / 0.01513 |
| `depth/estimate/200.0` | 64 | 213.93 | 213.97 ± 0.086134 | 213.97 [213.92, 214.04] | 0.095706 / 0.074347 |
| `depth/estimate/40.0` | 64 | 39 | 39.001 ± 0.11934 | 38.99 [38.927, 39.09] | 0.10009 / 0.13679 |
| `depth/estimate/60.0` | 64 | 60 | 60 ± 0 | 60 [60, 60] | 0 / 0 |
| `depth/plot_width/100.0` | 64 | 135 | 135 ± 0 | 135 [135, 135] | 0 / 0 |
| `depth/plot_width/150.0` | 64 | 240 | 240 ± 0 | 240 [240, 240] | 0 / 0 |
| `depth/plot_width/200.0` | 64 | 240 | 240 ± 0 | 240 [240, 240] | 0 / 0 |
| `depth/plot_width/40.0` | 64 | 50 | 50 ± 0 | 50 [50, 50] | 0 / 0 |
| `depth/plot_width/60.0` | 64 | 60 | 60 ± 0 | 60 [60, 60] | 0 / 0 |
| `depth3d/depth3d.csv/3d/offaxis_far/1000/depth_bias_mm` | 64 | -16.77 | -16.668 ± 0.88089 | -16.695 [-17.328, -16.17] | 0.95463 / 0.81454 |
| `depth3d/depth3d.csv/3d/offaxis_far/1000/depth_rms_mm` | 64 | 17.98 | 17.896 ± 0.79738 | 17.935 [17.447, 18.383] | 0.87883 / 0.71965 |
| `depth3d/depth3d.csv/3d/offaxis_far/1000/lateral_rms_mm` | 64 | 0.577 | 0.58334 ± 0.020458 | 0.582 [0.57175, 0.5985] | 0.024606 / 0.015642 |
| `depth3d/depth3d.csv/3d/offaxis_far/3000/depth_bias_mm` | 64 | -18.5 | -18.417 ± 0.67408 | -18.395 [-18.84, -17.955] | 0.73017 / 0.6216 |
| `depth3d/depth3d.csv/3d/offaxis_far/3000/depth_rms_mm` | 64 | 19.13 | 18.886 ± 0.6408 | 18.895 [18.515, 19.295] | 0.70057 / 0.58281 |
| `depth3d/depth3d.csv/3d/offaxis_far/3000/lateral_rms_mm` | 64 | 0.58 | 0.58047 ± 0.014507 | 0.58 [0.569, 0.58825] | 0.014607 / 0.014639 |
| `depth3d/depth3d.csv/3d/offaxis_near/1000/depth_bias_mm` | 64 | 7.66 | 7.6683 ± 0.057003 | 7.675 [7.63, 7.7] | 0.059099 / 0.055764 |
| `depth3d/depth3d.csv/3d/offaxis_near/1000/depth_rms_mm` | 64 | 7.68 | 7.6808 ± 0.056857 | 7.69 [7.6475, 7.72] | 0.059338 / 0.055211 |
| `depth3d/depth3d.csv/3d/offaxis_near/1000/lateral_rms_mm` | 64 | 0.32 | 0.31342 ± 0.012427 | 0.314 [0.30625, 0.32075] | 0.011877 / 0.013019 |
| `depth3d/depth3d.csv/3d/offaxis_near/3000/depth_bias_mm` | 64 | 7.81 | 7.8034 ± 0.038057 | 7.8 [7.78, 7.83] | 0.035264 / 0.038808 |
| `depth3d/depth3d.csv/3d/offaxis_near/3000/depth_rms_mm` | 64 | 7.82 | 7.8077 ± 0.037065 | 7.805 [7.78, 7.8325] | 0.033636 / 0.038834 |
| `depth3d/depth3d.csv/3d/offaxis_near/3000/lateral_rms_mm` | 64 | 0.352 | 0.35225 ± 0.0081338 | 0.352 [0.347, 0.361] | 0.0079808 / 0.0079219 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_far/1000/depth_bias_mm` | 64 | -0.81 | -0.61062 ± 0.48184 | -0.56 [-0.8775, -0.3675] | 0.48347 / 0.48444 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_far/1000/depth_rms_mm` | 64 | 3.9 | 3.5289 ± 0.53662 | 3.54 [3.1175, 3.8625] | 0.5795 / 0.49857 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_far/1000/lateral_rms_mm` | 64 | 0.533 | 0.52628 ± 0.0081191 | 0.525 [0.522, 0.528] | 0.0053294 / 0.010026 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_far/3000/depth_bias_mm` | 64 | -0.37 | -0.27813 ± 0.25313 | -0.32 [-0.4125, -0.0875] | 0.26394 / 0.23947 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_far/3000/depth_rms_mm` | 64 | 1.55 | 1.6622 ± 0.25243 | 1.625 [1.5175, 1.77] | 0.2111 / 0.28998 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_far/3000/lateral_rms_mm` | 64 | 0.52 | 0.52078 ± 0.0015376 | 0.521 [0.52, 0.522] | 0.0016458 / 0.0014024 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_near/1000/depth_bias_mm` | 64 | -6.81 | -6.6844 ± 0.089865 | -6.68 [-6.74, -6.63] | 0.087367 / 0.093308 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_near/1000/depth_rms_mm` | 64 | 6.85 | 6.7166 ± 0.091482 | 6.705 [6.6675, 6.77] | 0.0887 / 0.095093 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_near/1000/lateral_rms_mm` | 64 | 0.418 | 0.41708 ± 0.001636 | 0.417 [0.416, 0.418] | 0.001306 / 0.0018489 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_near/3000/depth_bias_mm` | 64 | -6.53 | -6.6434 ± 0.051739 | -6.645 [-6.68, -6.62] | 0.056568 / 0.045869 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_near/3000/depth_rms_mm` | 64 | 6.54 | 6.6531 ± 0.052429 | 6.655 [6.62, 6.69] | 0.056394 / 0.047413 |
| `depth3d/depth3d.csv/alt_oracle/offaxis_near/3000/lateral_rms_mm` | 64 | 0.415 | 0.41744 ± 0.00097386 | 0.417 [0.417, 0.418] | 0.0011248 / 0.00080259 |
| `depth3d/depth3d.csv/alternating/offaxis_far/1000/depth_bias_mm` | 64 | -15.71 | -13.932 ± 1.9415 | -13.775 [-15.425, -12.545] | 1.9716 / 1.8134 |
| `depth3d/depth3d.csv/alternating/offaxis_far/1000/depth_rms_mm` | 64 | 24.11 | 20.108 ± 3.8421 | 20.51 [16.828, 22.847] | 3.9225 / 3.6468 |
| `depth3d/depth3d.csv/alternating/offaxis_far/1000/lateral_rms_mm` | 64 | 1.293 | 1.0791 ± 0.45296 | 0.822 [0.694, 1.3728] | 0.44942 / 0.46271 |
| `depth3d/depth3d.csv/alternating/offaxis_far/3000/depth_bias_mm` | 64 | -12.09 | -10.763 ± 1.0329 | -10.78 [-11.31, -9.9675] | 0.97745 / 1.0818 |
| `depth3d/depth3d.csv/alternating/offaxis_far/3000/depth_rms_mm` | 64 | 14.51 | 12.93 ± 1.4962 | 12.95 [12.027, 13.58] | 1.184 / 1.7719 |
| `depth3d/depth3d.csv/alternating/offaxis_far/3000/lateral_rms_mm` | 64 | 0.654 | 0.61489 ± 0.11189 | 0.6 [0.5845, 0.62] | 0.029584 / 0.1558 |
| `depth3d/depth3d.csv/alternating/offaxis_near/1000/depth_bias_mm` | 64 | 4.9 | 4.5394 ± 4.2481 | 5.235 [1.095, 7.56] | 4.0505 / 4.5009 |
| `depth3d/depth3d.csv/alternating/offaxis_near/1000/depth_rms_mm` | 64 | 30.06 | 29.093 ± 2.0348 | 28.89 [27.865, 30.06] | 1.5467 / 2.399 |
| `depth3d/depth3d.csv/alternating/offaxis_near/1000/lateral_rms_mm` | 64 | 3.729 | 3.626 ± 0.45253 | 3.6675 [3.3755, 3.8628] | 0.41951 / 0.4843 |
| `depth3d/depth3d.csv/alternating/offaxis_near/3000/depth_bias_mm` | 64 | -2.3 | 1.0398 ± 4.0316 | 1.2 [-1.5825, 3.9] | 4.0713 / 4.0286 |
| `depth3d/depth3d.csv/alternating/offaxis_near/3000/depth_rms_mm` | 64 | 22.1 | 25.745 ± 2.1623 | 25.735 [24.523, 27.453] | 2.1115 / 2.2458 |
| `depth3d/depth3d.csv/alternating/offaxis_near/3000/lateral_rms_mm` | 64 | 3.197 | 3.1672 ± 0.44835 | 3.1235 [2.914, 3.443] | 0.44491 / 0.45125 |
| `depthsharp/1400/fwhm` | 32 | 1976 | 1976.1 ± 0.48786 | 1976.1 [1975.8, 1976.4] | 0.4206 / 0.50869 |
| `depthsharp/150/fwhm` | 32 | 35.386 | 35.463 ± 0.12249 | 35.463 [35.384, 35.547] | 0.12466 / 0.12421 |
| `depthsharp/250/fwhm` | 32 | 148.48 | 148.66 ± 0.34429 | 148.56 [148.46, 148.92] | 0.26856 / 0.41498 |
| `depthsharp/400/fwhm` | 32 | 397.05 | 397.58 ± 0.66616 | 397.52 [397.08, 397.99] | 0.51676 / 0.80277 |
| `depthsharp/600/fwhm` | 32 | 557.12 | 557.12 ± 0.4293 | 557.18 [556.9, 557.42] | 0.43217 / 0.41673 |
| `depthsharp/900/fwhm` | 32 | 1190.6 | 1190.4 ± 0.4174 | 1190.3 [1190.1, 1190.7] | 0.43783 / 0.41031 |
| `depthsharp/first150_fwhm_fraction` | 32 | 0.23591 | 0.23642 ± 0.00081658 | 0.23642 [0.2359, 0.23698] | 0.00083104 / 0.00082805 |
| `depthsharp/first150_pass10` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `depthsharp/power` | 32 | 1.7442 | 1.7432 ± 0.0012655 | 1.7432 [1.7423, 1.7441] | 0.0011394 / 0.0014129 |
| `depthsharp/range10` | 32 | 150 | 150 ± 0 | 150 [150, 150] | 0 / 0 |
| `depthsharp/range20` | 32 | 195.84 | 195.67 ± 0.22683 | 195.67 [195.54, 195.8] | 0.17225 / 0.27624 |
| `doi/doi.csv/0.00/10.0/doi_shift_mm` | 64 | -0.0089 | -0.0088969 ± 0.00027887 | -0.0089 [-0.009025, -0.0087] | 0.00028141 / 0.00028048 |
| `doi/doi.csv/0.00/20.0/doi_shift_mm` | 64 | -0.0084 | -0.0085906 ± 0.00027529 | -0.0086 [-0.0087, -0.0084] | 0.00025979 / 0.00029194 |
| `doi/doi.csv/0.00/30.0/doi_shift_mm` | 64 | -0.0078 | -0.0077219 ± 0.00027744 | -0.0077 [-0.0079, -0.0075] | 0.00032104 / 0.00023062 |
| `doi/doi.csv/0.00/5.0/doi_shift_mm` | 64 | -0.0062 | -0.0056141 ± 0.0002468 | -0.0056 [-0.0058, -0.0054] | 0.00023655 / 0.00024363 |
| `doi/doi.csv/9.00/10.0/doi_shift_mm` | 64 | 0.1928 | 0.19234 ± 0.0019392 | 0.19235 [0.1908, 0.19375] | 0.0018609 / 0.0019903 |
| `doi/doi.csv/9.00/20.0/doi_shift_mm` | 64 | 0.2796 | 0.27782 ± 0.0013721 | 0.278 [0.277, 0.2788] | 0.0014824 / 0.0012483 |
| `doi/doi.csv/9.00/30.0/doi_shift_mm` | 64 | 0.298 | 0.29923 ± 0.001372 | 0.2994 [0.29828, 0.2998] | 0.0015541 / 0.001187 |
| `doi/doi.csv/9.00/5.0/doi_shift_mm` | 64 | 0.0952 | 0.093398 ± 0.0015462 | 0.0935 [0.09235, 0.0943] | 0.0015509 / 0.0015251 |
| `dose/dose_overrange.csv/1000/live_corrected_ratio` | 64 | 0.90802 | 0.90695 ± 0.0032624 | 0.90737 [0.90449, 0.90905] | 0.0032304 / 0.0033448 |
| `dose/dose_overrange.csv/1000/raw_ratio` | 64 | 0.86057 | 0.85956 ± 0.0030031 | 0.85997 [0.85727, 0.86146] | 0.0029826 / 0.0030703 |
| `dose/dose_overrange.csv/1E+04/live_corrected_ratio` | 64 | 0.90802 | 0.90695 ± 0.0032624 | 0.90737 [0.90449, 0.90905] | 0.0032304 / 0.0033448 |
| `dose/dose_overrange.csv/1E+04/raw_ratio` | 64 | 0.53084 | 0.53032 ± 0.0014124 | 0.53055 [0.52937, 0.53123] | 0.001456 / 0.0013907 |
| `dose/dose_overrange.csv/1E+05/live_corrected_ratio` | 64 | 0.90802 | 0.90695 ± 0.0032624 | 0.90737 [0.90449, 0.90905] | 0.0032304 / 0.0033448 |
| `dose/dose_overrange.csv/1E+05/raw_ratio` | 64 | 0.0042343 | 0.0042381 ± 4.3156e-05 | 0.004234 [0.0042127, 0.0042717] | 3.949e-05 / 4.7094e-05 |
| `dose/dose_ratio.csv/Am-241/0.0/0.0/ratio` | 64 | 1.0604 | 1.0631 ± 0.0023192 | 1.0631 [1.0615, 1.0646] | 0.0024241 / 0.0021909 |
| `dose/dose_ratio.csv/Co-57/0.0/0.0/ratio` | 64 | 0.9047 | 0.90736 ± 0.0020928 | 0.907 [0.90597, 0.90865] | 0.0021741 / 0.0020334 |
| `dose/dose_ratio.csv/Co-60/0.0/0.0/ratio` | 64 | 1.0529 | 1.0503 ± 0.003315 | 1.0505 [1.0478, 1.0529] | 0.0031449 / 0.0035171 |
| `dose/dose_ratio.csv/Cs-137/0.0/0.0/ratio` | 64 | 0.908 | 0.90695 ± 0.0032643 | 0.9074 [0.90447, 0.90908] | 0.0032272 / 0.0033515 |
| `dose/dose_ratio.csv/Ir-192/0.0/0.0/ratio` | 64 | 1.0046 | 1.0027 ± 0.0018107 | 1.0029 [1.0015, 1.0039] | 0.0017452 / 0.0018072 |
| `dose/dose_ratio.csv/angle/122.0/0.0/ratio` | 64 | 0.9042 | 0.90392 ± 0.0023533 | 0.9037 [0.90235, 0.90512] | 0.0021624 / 0.0025637 |
| `dose/dose_ratio.csv/angle/122.0/10.0/ratio` | 64 | 0.0905 | 0.090811 ± 0.00073401 | 0.0909 [0.090375, 0.0913] | 0.00080402 / 0.00063012 |
| `dose/dose_ratio.csv/angle/122.0/2.5/ratio` | 64 | 0.6596 | 0.66009 ± 0.0024854 | 0.65995 [0.6583, 0.662] | 0.0027714 / 0.0020673 |
| `dose/dose_ratio.csv/angle/122.0/20.0/ratio` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `dose/dose_ratio.csv/angle/122.0/30.0/ratio` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `dose/dose_ratio.csv/angle/122.0/45.0/ratio` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `dose/dose_ratio.csv/angle/122.0/5.0/ratio` | 64 | 0.3702 | 0.371 ± 0.0015533 | 0.3709 [0.3701, 0.3716] | 0.0016344 / 0.0014739 |
| `dose/dose_ratio.csv/angle/122.0/60.0/ratio` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `dose/dose_ratio.csv/angle/1250.0/0.0/ratio` | 64 | 1.0464 | 1.0495 ± 0.0032613 | 1.0492 [1.0471, 1.0518] | 0.0030109 / 0.0035423 |
| `dose/dose_ratio.csv/angle/1250.0/10.0/ratio` | 64 | 0.6625 | 0.66199 ± 0.0028429 | 0.662 [0.65995, 0.66385] | 0.0027049 / 0.0030039 |
| `dose/dose_ratio.csv/angle/1250.0/2.5/ratio` | 64 | 0.9804 | 0.98133 ± 0.0040405 | 0.98085 [0.97848, 0.9837] | 0.0040167 / 0.0041265 |
| `dose/dose_ratio.csv/angle/1250.0/20.0/ratio` | 64 | 0.4708 | 0.4723 ± 0.002798 | 0.47195 [0.4699, 0.47467] | 0.0026788 / 0.0029523 |
| `dose/dose_ratio.csv/angle/1250.0/30.0/ratio` | 64 | 0.3798 | 0.3771 ± 0.0023074 | 0.3774 [0.37543, 0.3788] | 0.0019572 / 0.0026289 |
| `dose/dose_ratio.csv/angle/1250.0/45.0/ratio` | 64 | 0.2136 | 0.21183 ± 0.0014572 | 0.212 [0.21077, 0.21272] | 0.0013605 / 0.00156 |
| `dose/dose_ratio.csv/angle/1250.0/5.0/ratio` | 64 | 0.8557 | 0.85957 ± 0.0031791 | 0.85945 [0.85752, 0.86122] | 0.0029714 / 0.0034049 |
| `dose/dose_ratio.csv/angle/1250.0/60.0/ratio` | 64 | 0.069 | 0.069456 ± 0.00065219 | 0.0695 [0.069, 0.0699] | 0.00059174 / 0.00064458 |
| `dose/dose_ratio.csv/angle/316.0/0.0/ratio` | 64 | 1.0609 | 1.0563 ± 0.0031992 | 1.0558 [1.0543, 1.0578] | 0.0036143 / 0.0027335 |
| `dose/dose_ratio.csv/angle/316.0/10.0/ratio` | 64 | 0.1508 | 0.15118 ± 0.0013146 | 0.15135 [0.1504, 0.1519] | 0.0011441 / 0.0014768 |
| `dose/dose_ratio.csv/angle/316.0/2.5/ratio` | 64 | 0.8251 | 0.81932 ± 0.003531 | 0.81985 [0.81703, 0.82162] | 0.002864 / 0.0040501 |
| `dose/dose_ratio.csv/angle/316.0/20.0/ratio` | 64 | 0.0071 | 0.0073672 ± 0.0002928 | 0.0074 [0.0072, 0.007525] | 0.00030386 / 0.00026027 |
| `dose/dose_ratio.csv/angle/316.0/30.0/ratio` | 64 | 0.0042 | 0.0040078 ± 0.0001837 | 0.004 [0.0039, 0.0041] | 0.00020625 / 0.00016136 |
| `dose/dose_ratio.csv/angle/316.0/45.0/ratio` | 64 | 0.0007 | 0.00075937 ± 7.7087e-05 | 0.0008 [0.0007, 0.0008] | 7.1772e-05 / 8.2733e-05 |
| `dose/dose_ratio.csv/angle/316.0/5.0/ratio` | 64 | 0.5179 | 0.51995 ± 0.0023377 | 0.5198 [0.51882, 0.52137] | 0.0023477 / 0.0023266 |
| `dose/dose_ratio.csv/angle/316.0/60.0/ratio` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `dose/dose_ratio.csv/angle/60.0/0.0/ratio` | 64 | 1.068 | 1.067 ± 0.0015762 | 1.0669 [1.0659, 1.068] | 0.0015901 / 0.0015846 |
| `dose/dose_ratio.csv/angle/60.0/10.0/ratio` | 64 | 0.1156 | 0.11326 ± 0.00085907 | 0.11335 [0.11265, 0.1139] | 0.00084795 / 0.00081126 |
| `dose/dose_ratio.csv/angle/60.0/2.5/ratio` | 64 | 0.7865 | 0.78518 ± 0.0022916 | 0.7852 [0.7836, 0.7869] | 0.0021196 / 0.0023636 |
| `dose/dose_ratio.csv/angle/60.0/20.0/ratio` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `dose/dose_ratio.csv/angle/60.0/30.0/ratio` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `dose/dose_ratio.csv/angle/60.0/45.0/ratio` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `dose/dose_ratio.csv/angle/60.0/5.0/ratio` | 64 | 0.4415 | 0.44149 ± 0.0015786 | 0.4415 [0.44017, 0.4426] | 0.0016168 / 0.0015185 |
| `dose/dose_ratio.csv/angle/60.0/60.0/ratio` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `dose/dose_ratio.csv/angle/662.0/0.0/ratio` | 64 | 0.9198 | 0.91776 ± 0.0034281 | 0.9177 [0.91535, 0.92005] | 0.0034028 / 0.0035072 |
| `dose/dose_ratio.csv/angle/662.0/10.0/ratio` | 64 | 0.4047 | 0.4064 ± 0.0021131 | 0.4067 [0.40505, 0.4078] | 0.0020954 / 0.0021516 |
| `dose/dose_ratio.csv/angle/662.0/2.5/ratio` | 64 | 0.8125 | 0.81681 ± 0.0030209 | 0.8172 [0.81443, 0.819] | 0.0031572 / 0.0028859 |
| `dose/dose_ratio.csv/angle/662.0/20.0/ratio` | 64 | 0.2233 | 0.2234 ± 0.0012804 | 0.2232 [0.2228, 0.22443] | 0.0012807 / 0.0012943 |
| `dose/dose_ratio.csv/angle/662.0/30.0/ratio` | 64 | 0.1695 | 0.1669 ± 0.0013812 | 0.1667 [0.1661, 0.1681] | 0.0013213 / 0.0014485 |
| `dose/dose_ratio.csv/angle/662.0/45.0/ratio` | 64 | 0.0785 | 0.077031 ± 0.00075085 | 0.07705 [0.0765, 0.0775] | 0.0007607 / 0.00074246 |
| `dose/dose_ratio.csv/angle/662.0/5.0/ratio` | 64 | 0.6559 | 0.65464 ± 0.0026406 | 0.65455 [0.65288, 0.6562] | 0.002697 / 0.0025726 |
| `dose/dose_ratio.csv/angle/662.0/60.0/ratio` | 64 | 0.0161 | 0.016375 ± 0.00029867 | 0.0164 [0.0162, 0.0166] | 0.00032761 / 0.00025509 |
| `dose/dose_ratio.csv/check/1173.0/0.0/ratio` | 64 | 1.0358 | 1.035 ± 0.0037949 | 1.0353 [1.0327, 1.0376] | 0.0036727 / 0.0039046 |
| `dose/dose_ratio.csv/check/122.0/0.0/ratio` | 64 | 0.9042 | 0.90392 ± 0.0023533 | 0.9037 [0.90235, 0.90512] | 0.0021624 / 0.0025637 |
| `dose/dose_ratio.csv/check/1332.0/0.0/ratio` | 64 | 1.0592 | 1.0639 ± 0.0042989 | 1.0638 [1.0609, 1.0663] | 0.0040975 / 0.0042776 |
| `dose/dose_ratio.csv/check/250.0/0.0/ratio` | 64 | 1.1222 | 1.1247 ± 0.0026957 | 1.1247 [1.123, 1.1266] | 0.0027851 / 0.0025105 |
| `dose/dose_ratio.csv/check/662.0/0.0/ratio` | 64 | 0.9198 | 0.91776 ± 0.0034281 | 0.9177 [0.91535, 0.92005] | 0.0034028 / 0.0035072 |
| `dose/dose_ratio.csv/check/70.0/0.0/ratio` | 64 | 1.0421 | 1.0397 ± 0.0022073 | 1.0398 [1.0385, 1.0408] | 0.0019334 / 0.0023498 |
| `dose/frontal_max_abs_error` | 64 | 0.1222 | 0.12473 ± 0.0026957 | 0.1247 [0.12298, 0.1266] | 0.0027851 / 0.0025105 |
| `dose/frontal_within13` | 64 | 1 | 0.98438 ± 0.125 | 1 [1, 1] | 0.17678 / 0 |
| `fov/1000/0/500/0.00/false_infield_14_20_max` | 64 | 0.7 | 0.70578 ± 0.027302 | 0.7 [0.69, 0.7225] | 0.029139 / 0.025582 |
| `fov/1000/0/500/0.00/false_infield_14_20_min` | 64 | 0.56 | 0.52 ± 0.038586 | 0.52 [0.5, 0.55] | 0.034827 / 0.041192 |
| `fov/1000/0/500/0.00/false_infield_7.5_11_max` | 64 | 0.89 | 0.87219 ± 0.028811 | 0.87 [0.85, 0.89] | 0.030755 / 0.026486 |
| `fov/1000/0/500/0.00/false_infield_7.5_11_min` | 64 | 0.51 | 0.52375 ± 0.047292 | 0.52 [0.4875, 0.57] | 0.048716 / 0.046601 |
| `fov/1000/0/500/0.00/false_infield_unflagged_7.5_11_max` | 64 | 0.04 | 0.046562 ± 0.01879 | 0.04 [0.03, 0.06] | 0.020417 / 0.017341 |
| `fov/1000/0/500/0.00/loc_cyclic` | 64 | 3 | 3 ± 0 | 3 [3, 3] | 0 / 0 |
| `fov/1000/0/500/0.00/loc_noncyclic` | 64 | 6 | 6.125 ± 0.28172 | 6 [6, 6] | 0.29056 / 0.27633 |
| `fov/1000/0/500/0.00/outside_centroid_any` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `fov/1000/0/500/0.00/outside_centroid_at9.5` | 64 | 1 | 0.99938 ± 0.0024398 | 1 [1, 1] | 0 / 0.0033601 |
| `fov/1000/0/500/0.00/outside_centroid_from` | 64 | 6 | 5.9141 ± 0.26057 | 6 [6, 6] | 0.23277 / 0.28398 |
| `fov/1000/0/500/0.00/outside_centroid_max` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `fov/1000/0/500/0.00/outside_centroid_to` | 64 | 11.5 | 10.898 ± 1.8434 | 11.5 [11.5, 11.5] | 1.8611 / 1.8513 |
| `fov/1000/0/500/0.00/side_centroid_from` | 64 | 1 | 1.2891 ± 0.24888 | 1.5 [1, 1.5] | 0.25201 / 0.2495 |
| `fov/1000/0/500/0.00/side_centroid_to` | 64 | 13.5 | 13.836 ± 0.23662 | 14 [13.5, 14] | 0.2535 / 0.19828 |
| `fov/1000/0/500/1.00/false_infield_14_20_max` | 64 | 0.62 | 0.62109 ± 0.025456 | 0.62 [0.6, 0.64] | 0.023418 / 0.027237 |
| `fov/1000/0/500/1.00/false_infield_14_20_min` | 64 | 0.45 | 0.45891 ± 0.025205 | 0.46 [0.45, 0.48] | 0.025303 / 0.024887 |
| `fov/1000/0/500/1.00/false_infield_7.5_11_max` | 64 | 0.78 | 0.85031 ± 0.03091 | 0.85 [0.83, 0.8725] | 0.027706 / 0.033503 |
| `fov/1000/0/500/1.00/false_infield_7.5_11_min` | 64 | 0.55 | 0.55484 ± 0.040825 | 0.56 [0.53, 0.58] | 0.043175 / 0.036708 |
| `fov/1000/0/500/1.00/false_infield_unflagged_7.5_11_max` | 64 | 0.49 | 0.43562 ± 0.039193 | 0.43 [0.41, 0.46] | 0.03569 / 0.042951 |
| `fov/1000/0/500/1.00/loc_cyclic` | 64 | 3 | 3 ± 0 | 3 [3, 3] | 0 / 0 |
| `fov/1000/0/500/1.00/loc_noncyclic` | 64 | 5 | 4.8906 ± 0.3017 | 5 [5, 5] | 0.19508 / 0.37264 |
| `fov/1000/0/500/1.00/outside_centroid_any` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `fov/1000/0/500/1.00/outside_centroid_at9.5` | 64 | 0.73 | 0.67672 ± 0.054629 | 0.68 [0.65, 0.72] | 0.053362 / 0.056622 |
| `fov/1000/0/500/1.00/outside_centroid_max` | 64 | 0.73 | 0.70609 ± 0.04583 | 0.71 [0.67, 0.74] | 0.047839 / 0.043626 |
| `fov/1000/0/500/1.00/side_centroid_from` | 64 | 1.5 | 1.5078 ± 0.0625 | 1.5 [1.5, 1.5] | 0.088388 / 0 |
| `fov/1000/0/500/1.00/side_centroid_to` | 64 | 13 | 13.07 ± 0.19654 | 13 [13, 13] | 0.21001 / 0.17678 |
| `fov/1000/0/5000/0.00/false_infield_14_20_max` | 64 | 0.74 | 0.72719 ± 0.045963 | 0.72 [0.6975, 0.76] | 0.047617 / 0.045009 |
| `fov/1000/0/5000/0.00/false_infield_14_20_min` | 64 | 0.34 | 0.355 ± 0.031371 | 0.36 [0.3375, 0.38] | 0.030544 / 0.032588 |
| `fov/1000/0/5000/0.00/false_infield_7.5_11_max` | 64 | 0.99 | 0.99219 ± 0.0089918 | 0.99 [0.99, 1] | 0.0081258 / 0.0098732 |
| `fov/1000/0/5000/0.00/false_infield_7.5_11_min` | 64 | 0.36 | 0.41625 ± 0.04061 | 0.41 [0.39, 0.45] | 0.039493 / 0.040937 |
| `fov/1000/0/5000/0.00/false_infield_unflagged_7.5_11_max` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `fov/1000/0/5000/0.00/loc_cyclic` | 64 | 3 | 3 ± 0 | 3 [3, 3] | 0 / 0 |
| `fov/1000/0/5000/0.00/loc_noncyclic` | 64 | 7 | 7 ± 0 | 7 [7, 7] | 0 / 0 |
| `fov/1000/0/5000/0.00/outside_centroid_any` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `fov/1000/0/5000/0.00/outside_centroid_at9.5` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `fov/1000/0/5000/0.00/outside_centroid_from` | 64 | 4 | 4 ± 0 | 4 [4, 4] | 0 / 0 |
| `fov/1000/0/5000/0.00/outside_centroid_max` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `fov/1000/0/5000/0.00/outside_centroid_to` | 64 | 12.5 | 12.617 ± 0.21348 | 12.5 [12.5, 12.5] | 0.21001 / 0.21997 |
| `fov/1000/0/5000/0.00/side_centroid_from` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `fov/1000/0/5000/0.00/side_centroid_to` | 64 | 14.5 | 14.5 ± 0 | 14.5 [14.5, 14.5] | 0 / 0 |
| `fov/1000/0/5000/1.00/false_infield_14_20_max` | 64 | 0.48 | 0.51469 ± 0.048891 | 0.51 [0.49, 0.5525] | 0.050466 / 0.042269 |
| `fov/1000/0/5000/1.00/false_infield_14_20_min` | 64 | 0.18 | 0.17094 ± 0.024084 | 0.17 [0.16, 0.1825] | 0.024288 / 0.02423 |
| `fov/1000/0/5000/1.00/false_infield_7.5_11_max` | 64 | 1 | 0.99516 ± 0.0061701 | 1 [0.99, 1] | 0.0067127 / 0.0056796 |
| `fov/1000/0/5000/1.00/false_infield_7.5_11_min` | 64 | 0.29 | 0.34656 ± 0.04681 | 0.345 [0.31, 0.38] | 0.043255 / 0.050478 |
| `fov/1000/0/5000/1.00/false_infield_unflagged_7.5_11_max` | 64 | 0.09 | 0.084375 ± 0.027073 | 0.08 [0.06, 0.1] | 0.030266 / 0.02391 |
| `fov/1000/0/5000/1.00/loc_cyclic` | 64 | 3 | 3 ± 0 | 3 [3, 3] | 0 / 0 |
| `fov/1000/0/5000/1.00/loc_noncyclic` | 64 | 6 | 6 ± 0 | 6 [6, 6] | 0 / 0 |
| `fov/1000/0/5000/1.00/outside_centroid_any` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `fov/1000/0/5000/1.00/outside_centroid_at9.5` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `fov/1000/0/5000/1.00/outside_centroid_from` | 64 | 5 | 4.9922 ± 0.16648 | 5 [5, 5] | 0.14807 / 0.17678 |
| `fov/1000/0/5000/1.00/outside_centroid_max` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `fov/1000/0/5000/1.00/outside_centroid_to` | 64 | 6.5 | 8.4453 ± 1.7731 | 7 [7, 10.5] | 1.674 / 1.8122 |
| `fov/1000/0/5000/1.00/side_centroid_from` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `fov/1000/0/5000/1.00/side_centroid_to` | 64 | 14.5 | 14.094 ± 0.1967 | 14 [14, 14] | 0.19828 / 0.19828 |
| `fov/1000/45/500/0.00/loc_cyclic` | 64 | 4.5 | 4.5 ± 0 | 4.5 [4.5, 4.5] | 0 / 0 |
| `fov/1000/45/500/0.00/loc_noncyclic` | 64 | 6 | 3.4453 ± 0.91338 | 3 [3, 3] | 0.96668 / 0.87168 |
| `fov/1000/45/500/1.00/loc_cyclic` | 64 | 2.5 | 2.3125 ± 0.24398 | 2.5 [2, 2.5] | 0.24593 / 0.24593 |
| `fov/1000/45/500/1.00/loc_noncyclic` | 64 | 0 | 0.17969 ± 0.32571 | 0 [0, 0.5] | 0.21001 / 0.40161 |
| `fov/1000/45/5000/0.00/loc_cyclic` | 64 | 4.5 | 4.5 ± 0 | 4.5 [4.5, 4.5] | 0 / 0 |
| `fov/1000/45/5000/0.00/loc_noncyclic` | 64 | 9 | 9 ± 0 | 9 [9, 9] | 0 / 0 |
| `fov/1000/45/5000/1.00/loc_cyclic` | 64 | 3 | 3 ± 0 | 3 [3, 3] | 0 / 0 |
| `fov/1000/45/5000/1.00/loc_noncyclic` | 64 | 3 | 3 ± 0 | 3 [3, 3] | 0 / 0 |
| `fov/5000/0/500/0.00/loc_cyclic` | 64 | 3.5 | 3.375 ± 0.21822 | 3.5 [3.375, 3.5] | 0.23546 / 0.19828 |
| `fov/5000/0/500/0.00/loc_noncyclic` | 64 | 7 | 7 ± 0 | 7 [7, 7] | 0 / 0 |
| `fov/5000/0/500/1.00/loc_cyclic` | 64 | 3 | 3 ± 0 | 3 [3, 3] | 0 / 0 |
| `fov/5000/0/500/1.00/loc_noncyclic` | 64 | 6.5 | 6.4141 ± 0.20996 | 6.5 [6.5, 6.5] | 0.16801 / 0.24542 |
| `fov/5000/0/5000/0.00/loc_cyclic` | 64 | 3.5 | 3.5 ± 0 | 3.5 [3.5, 3.5] | 0 / 0 |
| `fov/5000/0/5000/0.00/loc_noncyclic` | 64 | 7.5 | 7.5 ± 0 | 7.5 [7.5, 7.5] | 0 / 0 |
| `fov/5000/0/5000/1.00/loc_cyclic` | 64 | 3.5 | 3.5 ± 0 | 3.5 [3.5, 3.5] | 0 / 0 |
| `fov/5000/0/5000/1.00/loc_noncyclic` | 64 | 6.5 | 6.5 ± 0 | 6.5 [6.5, 6.5] | 0 / 0 |
| `fov/5000/45/500/0.00/loc_cyclic` | 64 | 4.5 | 4.5 ± 0 | 4.5 [4.5, 4.5] | 0 / 0 |
| `fov/5000/45/500/0.00/loc_noncyclic` | 64 | 3 | 3.2109 ± 0.52557 | 3 [3, 3] | 0.59484 / 0.44422 |
| `fov/5000/45/500/1.00/loc_cyclic` | 64 | 2 | 1.2891 ± 0.78074 | 2 [0.5, 2] | 0.80931 / 0.762 |
| `fov/5000/45/500/1.00/loc_noncyclic` | 64 | 0.5 | 0.53125 ± 0.17537 | 0.5 [0.5, 0.5] | 0.17678 / 0.17678 |
| `fov/5000/45/5000/0.00/loc_cyclic` | 64 | 4.5 | 4.8125 ± 0.24398 | 5 [4.5, 5] | 0.2495 / 0.24128 |
| `fov/5000/45/5000/0.00/loc_noncyclic` | 64 | 7.5 | 6.375 ± 1.4639 | 7.5 [4.5, 7.5] | 1.3704 / 1.521 |
| `fov/5000/45/5000/1.00/loc_cyclic` | 64 | 4.5 | 4.5 ± 0 | 4.5 [4.5, 4.5] | 0 / 0 |
| `fov/5000/45/5000/1.00/loc_noncyclic` | 64 | 2 | 2.0078 ± 0.14064 | 2 [2, 2] | 0 / 0.20018 |
| `gap/0.02/relativeIdeal` | 32 | 0.012003 | 0.012032 ± 0.00010623 | 0.012021 [0.01196, 0.01211] | 0.00012256 / 7.5114e-05 |
| `gap/0.02/relativeZeroGapPPW` | 32 | 1.5964 | 1.5894 ± 0.016254 | 1.5924 [1.5764, 1.6012] | 0.017509 / 0.015313 |
| `gap/0.04/relativeIdeal` | 32 | 0.019134 | 0.019065 ± 0.00012485 | 0.019068 [0.018987, 0.019142] | 0.00012934 / 0.00012174 |
| `gap/0.04/relativeZeroGapPPW` | 32 | 2.5448 | 2.5186 ± 0.024466 | 2.5198 [2.5031, 2.5377] | 0.02481 / 0.022503 |
| `gap/0.1/relativeIdeal` | 32 | 0.050032 | 0.04995 ± 0.00018818 | 0.04996 [0.049813, 0.05005] | 0.00020952 / 0.00017012 |
| `gap/0.1/relativeZeroGapPPW` | 32 | 6.6542 | 6.5987 ± 0.06808 | 6.5987 [6.5548, 6.6466] | 0.073742 / 0.06058 |
| `gap/0.2/relativeIdeal` | 32 | 0.04058 | 0.040349 ± 0.000196 | 0.0403 [0.040192, 0.040497] | 0.00019801 / 0.00020033 |
| `gap/0.2/relativeZeroGapPPW` | 32 | 5.3971 | 5.3305 ± 0.062975 | 5.3252 [5.2806, 5.3768] | 0.070438 / 0.054957 |
| `gap/0/relativeIdeal` | 32 | 0.0075189 | 0.0075704 ± 8.3489e-05 | 0.0075805 [0.0075263, 0.0076273] | 0.00010025 / 6.2332e-05 |
| `gap/0/relativeZeroGapPPW` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `head/ir_ratio` | 32 | 1.0556 | 1.0578 ± 0.0012193 | 1.058 [1.0569, 1.0585] | 0.0012919 / 0.0011835 |
| `head/ratio` | 32 | 2.478 | 2.4791 ± 0.0033548 | 2.4787 [2.4773, 2.4811] | 0.0034384 / 0.0033653 |
| `headir/analytic_ratio` | 32 | 1.0744 | 1.0745 ± 0.00085183 | 1.0745 [1.0742, 1.0751] | 0.00070309 / 0.0010025 |
| `maskfab/maskfab.csv/0/psr` | 128 | 4.318 | 4.3769 ± 0.060488 | 4.376 [4.3257, 4.4203] | 0.062666 / 0.057289 |
| `maskfab/maskfab.csv/0/rms_mm` | 128 | 1.506 | 1.3457 ± 0.42452 | 1.4345 [1.069, 1.6778] | 0.42845 / 0.41696 |
| `maskfab/maskfab.csv/10/psr` | 128 | 4.372 | 4.3887 ± 0.038799 | 4.385 [4.364, 4.411] | 0.038662 / 0.039216 |
| `maskfab/maskfab.csv/10/rms_mm` | 128 | 1.25 | 1.2886 ± 0.19433 | 1.293 [1.1627, 1.4052] | 0.19274 / 0.19626 |
| `maskfab/maskfab.csv/160/psr` | 128 | 3.75 | 3.7701 ± 0.023554 | 3.772 [3.753, 3.7843] | 0.023032 / 0.024238 |
| `maskfab/maskfab.csv/160/rms_mm` | 128 | 3.649 | 3.651 ± 0.18461 | 3.643 [3.537, 3.765] | 0.19758 / 0.17206 |
| `maskfab/maskfab.csv/20/psr` | 128 | 4.365 | 4.3579 ± 0.032006 | 4.358 [4.3365, 4.3803] | 0.032688 / 0.031242 |
| `maskfab/maskfab.csv/20/rms_mm` | 128 | 1.553 | 1.4153 ± 0.19266 | 1.4025 [1.2692, 1.548] | 0.19232 / 0.19005 |
| `maskfab/maskfab.csv/40/psr` | 128 | 4.307 | 4.314 ± 0.026725 | 4.3155 [4.2935, 4.3283] | 0.02747 / 0.025936 |
| `maskfab/maskfab.csv/40/rms_mm` | 128 | 1.405 | 1.5203 ± 0.18319 | 1.541 [1.388, 1.641] | 0.17825 / 0.18941 |
| `maskfab/maskfab.csv/80/psr` | 128 | 4.209 | 4.1751 ± 0.029108 | 4.175 [4.1555, 4.193] | 0.030215 / 0.027951 |
| `maskfab/maskfab.csv/80/rms_mm` | 128 | 1.725 | 1.9297 ± 0.20875 | 1.9355 [1.808, 2.0797] | 0.21409 / 0.20241 |
| `maskfab/relativeRms/10` | 128 | 0.83001 | 1.0745 ± 0.43267 | 0.88798 [0.77411, 1.2787] | 0.41618 / 0.44914 |
| `maskfab/relativeRms/160` | 128 | 2.423 | 3.0654 ± 1.1893 | 2.6527 [2.2119, 3.3975] | 1.138 / 1.2332 |
| `maskfab/relativeRms/20` | 128 | 1.0312 | 1.1785 ± 0.45851 | 1.0248 [0.85419, 1.3357] | 0.47152 / 0.44755 |
| `maskfab/relativeRms/40` | 128 | 0.93293 | 1.2767 ± 0.51679 | 1.0849 [0.93127, 1.4509] | 0.49478 / 0.53611 |
| `maskfab/relativeRms/80` | 128 | 1.1454 | 1.6179 ± 0.64802 | 1.3695 [1.1479, 1.8676] | 0.64033 / 0.64939 |
| `maskfab/within2/10` | 128 | 1 | 0.96094 ± 0.1945 | 1 [1, 1] | 0.17537 / 0.21304 |
| `maskfab/within2/160` | 128 | 0 | 0.125 ± 0.33202 | 0 [0, 0] | 0.36596 / 0.29378 |
| `maskfab/within2/20` | 128 | 1 | 0.92969 ± 0.25668 | 1 [1, 1] | 0.24398 / 0.27049 |
| `maskfab/within2/40` | 128 | 1 | 0.84375 ± 0.36452 | 1 [1, 1] | 0.33333 / 0.3934 |
| `maskfab/within2/80` | 128 | 1 | 0.78125 ± 0.41502 | 1 [1, 1] | 0.38025 / 0.44516 |
| `masktaper/gain4` | 64 | 0.35148 | 0.35244 ± 0.0017809 | 0.35239 [0.35128, 0.35348] | 0.0019701 / 0.0015171 |
| `masktaper/masktaper.csv/0.0/edge_ratio` | 64 | 0.822 | 0.82498 ± 0.0016154 | 0.8247 [0.8239, 0.82587] | 0.0017217 / 0.0015022 |
| `masktaper/masktaper.csv/0.0/eff_center` | 64 | 0.00016536 | 0.00016499 ± 1.9569e-07 | 0.00016497 [0.00016486, 0.00016515] | 2.0224e-07 / 1.8411e-07 |
| `masktaper/masktaper.csv/0.0/rms_edge_mm` | 64 | 0.255 | 0.25645 ± 0.0069349 | 0.255 [0.251, 0.261] | 0.0069488 / 0.0070218 |
| `masktaper/masktaper.csv/4.0/edge_ratio` | 64 | 0.9895 | 0.99141 ± 0.0013672 | 0.9914 [0.9902, 0.99232] | 0.0013268 / 0.0014274 |
| `masktaper/masktaper.csv/4.0/eff_center` | 64 | 0.00022348 | 0.00022314 ± 2.2415e-07 | 0.00022315 [0.00022301, 0.00022331] | 2.5754e-07 / 1.889e-07 |
| `masktaper/masktaper.csv/4.0/rms_edge_mm` | 64 | 0.264 | 0.262 ± 0.004814 | 0.262 [0.258, 0.266] | 0.0045151 / 0.0051242 |
| `mat_BGO/eff` | 64 | 0.0001228 | 0.00012254 ± 1.9092e-07 | 0.00012252 [0.00012241, 0.00012267] | 1.9007e-07 / 1.9471e-07 |
| `mat_CeBr3/eff` | 64 | 7.7043e-05 | 7.6884e-05 ± 1.1978e-07 | 7.6866e-05 [7.6802e-05, 7.6964e-05] | 1.1925e-07 / 1.2216e-07 |
| `mat_GAGG/eff` | 64 | 0.00010019 | 9.9986e-05 ± 1.5578e-07 | 9.9962e-05 [9.9878e-05, 0.00010009] | 1.5508e-07 / 1.5887e-07 |
| `mat_LYSO/eff` | 64 | 0.00011346 | 0.00011323 ± 1.7641e-07 | 0.0001132 [0.00011311, 0.00011335] | 1.7562e-07 / 1.7991e-07 |
| `mat_LaBr3/eff` | 64 | 7.6351e-05 | 7.6194e-05 ± 1.1871e-07 | 7.6176e-05 [7.6112e-05, 7.6273e-05] | 1.1818e-07 / 1.2106e-07 |
| `mat_NaI/eff` | 64 | 5.9269e-05 | 5.9148e-05 ± 9.2151e-08 | 5.9134e-05 [5.9084e-05, 5.9209e-05] | 9.1739e-08 / 9.3979e-08 |
| `mixedfield/match5/error` | 64 | 0.55 | 0.55 ± 0 | 0.55 [0.55, 0.55] | 0 / 0 |
| `mixedfield/match6/error` | 64 | 0.27 | 0.27 ± 0 | 0.27 [0.27, 0.27] | 0 / 0 |
| `mixedfield/match7/error` | 64 | 0.93 | 0.93 ± 0 | 0.93 [0.93, 0.93] | 0 / 0 |
| `mixediso/match1/error` | 64 | 8 | 8 ± 0 | 8 [8, 8] | 0 / 0 |
| `mixediso/match7/error` | 64 | 8.75 | 8.6647 ± 0.24537 | 8.75 [8.75, 8.75] | 0.28774 / 0.19183 |
| `mixediso/match8/error` | 64 | 1.1 | 1.1 ± 0 | 1.1 [1.1, 1.1] | 0 / 0 |
| `mixedstrip/R` | 64 | 4.076 | 3.9882 ± 0.035404 | 3.9825 [3.9688, 4.0103] | 0.034575 / 0.036767 |
| `mixedstrip/co-located/raw_error_pct` | 64 | 1160 | 1161.6 ± 16.1 | 1159 [1150.8, 1171] | 14.343 / 17.675 |
| `mixedstrip/co-located/stripped` | 64 | 3 | 3.4688 ± 0.50297 | 3 [3, 4] | 0.49899 / 0.50701 |
| `mixedstrip/co-located/stripped_error_pct` | 64 | 10 | 14.062 ± 9.545 | 13 [8, 20.25] | 8.6621 / 10.466 |
| `mixedstrip/separated/raw_error_pct` | 64 | 1177 | 1164.9 ± 14.278 | 1164 [1156, 1175.2] | 13.893 / 14.856 |
| `mixedstrip/separated/stripped` | 64 | 3 | 3.6406 ± 0.48361 | 4 [3, 4] | 0.49899 / 0.47093 |
| `mixedstrip/separated/stripped_error_pct` | 64 | 2 | 17.625 ± 9.2453 | 18 [12.5, 23] | 9.2588 / 9.3773 |
| `mlem/mlem.csv/1.50/cross_resolved` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `mlem/mlem.csv/1.50/cross_valley_depth` | 64 | 0.0389 | 0.039077 ± 0.00026947 | 0.0391 [0.0389, 0.039225] | 0.00027938 / 0.0002602 |
| `mlem/mlem.csv/1.50/mlem_resolved` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `mlem/mlem.csv/1.50/mlem_valley_depth` | 64 | 0.3475 | 0.32875 ± 0.016349 | 0.32855 [0.3167, 0.33797] | 0.016918 / 0.01576 |
| `mlem/mlem.csv/2.00/cross_resolved` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `mlem/mlem.csv/2.00/cross_valley_depth` | 64 | 0.1082 | 0.10844 ± 0.0007604 | 0.10835 [0.108, 0.1088] | 0.00083786 / 0.00068427 |
| `mlem/mlem.csv/2.00/mlem_resolved` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `mlem/mlem.csv/2.00/mlem_valley_depth` | 64 | 0.5616 | 0.55242 ± 0.0096039 | 0.55265 [0.5462, 0.5564] | 0.0094723 / 0.0097501 |
| `mlem/mlem.csv/3.00/cross_resolved` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `mlem/mlem.csv/3.00/cross_valley_depth` | 64 | 0.1714 | 0.16975 ± 0.0012175 | 0.1698 [0.16905, 0.1705] | 0.0013886 / 0.001038 |
| `mlem/mlem.csv/3.00/mlem_resolved` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `mlem/mlem.csv/3.00/mlem_valley_depth` | 64 | 0.8225 | 0.82187 ± 0.0043788 | 0.82185 [0.8195, 0.82438] | 0.0045933 / 0.0041753 |
| `mlem/mlem.csv/3.50/cross_resolved` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `mlem/mlem.csv/3.50/cross_valley_depth` | 64 | 0.3831 | 0.38093 ± 0.0025095 | 0.38065 [0.37875, 0.38293] | 0.0021142 / 0.0028409 |
| `mlem/mlem.csv/3.50/mlem_resolved` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `mlem/mlem.csv/3.50/mlem_valley_depth` | 64 | 0.8942 | 0.89334 ± 0.0028967 | 0.8936 [0.8913, 0.8955] | 0.0029179 / 0.0028194 |
| `mlem/stdout/line2/n0` | 64 | 0.55 | 0.55 ± 0 | 0.55 [0.55, 0.55] | 0 / 0 |
| `mlem/stdout/line2/n1` | 64 | 0.87 | 0.87 ± 0 | 0.87 [0.87, 0.87] | 0 / 0 |
| `mlem/stdout/line2/n2` | 64 | -103.7 | -103.51 ± 0.38401 | -103.5 [-103.7, -103.27] | 0.38739 / 0.38088 |
| `mlem/stdout/line2/n3` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `mlem/stdout/line2/n4` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `mlem/stdout/line2/n5` | 64 | 2.5 | 2.4997 ± 0.0017537 | 2.5 [2.5, 2.5] | 0 / 0.0024593 |
| `mlem/stdout/line2/n6` | 64 | 1.95 | 1.932 ± 0.050871 | 1.95 [1.94, 1.95] | 0.053397 / 0.048923 |
| `noise/noise.csv/centered/100/failure_rate` | 128 | 0.003 | 0.00425 ± 0.0038801 | 0.003 [0, 0.007] | 0.0042102 / 0.003509 |
| `noise/noise.csv/centered/100/rms_error_mm` | 128 | 0.873 | 0.85929 ± 0.18157 | 0.8625 [0.66125, 0.966] | 0.19149 / 0.17258 |
| `noise/noise.csv/centered/25/failure_rate` | 128 | 0.347 | 0.38223 ± 0.025564 | 0.38 [0.367, 0.4] | 0.02345 / 0.027076 |
| `noise/noise.csv/centered/25/rms_error_mm` | 128 | 5.278 | 5.339 ± 0.20168 | 5.343 [5.213, 5.4908] | 0.1961 / 0.20676 |
| `noise/noise.csv/centered/250/failure_rate` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise/noise.csv/centered/250/rms_error_mm` | 128 | 0.442 | 0.43748 ± 0.013963 | 0.4375 [0.427, 0.448] | 0.012933 / 0.014905 |
| `noise/noise.csv/centered/50/failure_rate` | 128 | 0.07 | 0.096336 ± 0.019653 | 0.097 [0.087, 0.11] | 0.019809 / 0.019416 |
| `noise/noise.csv/centered/50/rms_error_mm` | 128 | 2.437 | 2.7764 ± 0.29178 | 2.7955 [2.617, 2.9845] | 0.28969 / 0.29245 |
| `noise/noise.csv/centered/500/failure_rate` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise/noise.csv/centered/500/rms_error_mm` | 128 | 0.365 | 0.37037 ± 0.0055114 | 0.37 [0.366, 0.374] | 0.005348 / 0.0057112 |
| `noise/noise.csv/centered/5000/failure_rate` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise/noise.csv/centered/5000/rms_error_mm` | 128 | 0.354 | 0.35391 ± 0.0012825 | 0.354 [0.353, 0.355] | 0.0012901 / 0.0012692 |
| `noise/noise.csv/edge_8mm/100/failure_rate` | 128 | 0.01 | 0.014383 ± 0.0074041 | 0.013 [0.007, 0.02] | 0.0074341 / 0.0074056 |
| `noise/noise.csv/edge_8mm/100/rms_error_mm` | 128 | 1.586 | 1.4812 ± 0.36438 | 1.4745 [1.257, 1.7505] | 0.36659 / 0.36368 |
| `noise/noise.csv/edge_8mm/25/failure_rate` | 128 | 0.397 | 0.45435 ± 0.032797 | 0.457 [0.43225, 0.48] | 0.032678 / 0.033153 |
| `noise/noise.csv/edge_8mm/25/rms_error_mm` | 128 | 7.437 | 7.9381 ± 0.34303 | 7.939 [7.6872, 8.1777] | 0.33455 / 0.35368 |
| `noise/noise.csv/edge_8mm/250/failure_rate` | 128 | 0 | 4.6875e-05 ± 0.00037352 | 0 [0, 0] | 0.000375 / 0.000375 |
| `noise/noise.csv/edge_8mm/250/rms_error_mm` | 128 | 0.53 | 0.53648 ± 0.036053 | 0.532 [0.527, 0.538] | 0.035557 / 0.036805 |
| `noise/noise.csv/edge_8mm/50/failure_rate` | 128 | 0.143 | 0.14901 ± 0.023128 | 0.147 [0.133, 0.163] | 0.023131 / 0.023304 |
| `noise/noise.csv/edge_8mm/50/rms_error_mm` | 128 | 4.226 | 4.4831 ± 0.40535 | 4.488 [4.264, 4.7515] | 0.445 / 0.36492 |
| `noise/noise.csv/edge_8mm/500/failure_rate` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise/noise.csv/edge_8mm/500/rms_error_mm` | 128 | 0.5 | 0.49906 ± 0.0058535 | 0.499 [0.495, 0.503] | 0.0065041 / 0.0051714 |
| `noise/noise.csv/edge_8mm/5000/failure_rate` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise/noise.csv/edge_8mm/5000/rms_error_mm` | 128 | 0.5 | 0.49854 ± 0.0022092 | 0.498 [0.497, 0.5] | 0.0023212 / 0.0021081 |
| `noise/submm/100` | 128 | 1 | 0.77344 ± 0.42025 | 1 [1, 1] | 0.44516 / 0.3934 |
| `noise/submm/250` | 128 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `noise/submm/50` | 128 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise_head/noise.csv/centered/100/failure_rate` | 64 | 0.027 | 0.039875 ± 0.012149 | 0.04 [0.03, 0.04775] | 0.012021 / 0.011884 |
| `noise_head/noise.csv/centered/100/rms_error_mm` | 64 | 1.63 | 1.8218 ± 0.2884 | 1.8315 [1.6158, 2.01] | 0.30788 / 0.26224 |
| `noise_head/noise.csv/centered/25/failure_rate` | 64 | 0.54 | 0.49691 ± 0.033502 | 0.5015 [0.47, 0.524] | 0.029814 / 0.03527 |
| `noise_head/noise.csv/centered/25/rms_error_mm` | 64 | 6.451 | 6.1096 ± 0.21862 | 6.105 [5.944, 6.2753] | 0.1965 / 0.22665 |
| `noise_head/noise.csv/centered/250/failure_rate` | 64 | 0 | 0.0006875 ± 0.0015824 | 0 [0, 0] | 0.0019404 / 0.0011067 |
| `noise_head/noise.csv/centered/250/rms_error_mm` | 64 | 0.254 | 0.33341 ± 0.1472 | 0.267 [0.26, 0.28125] | 0.17537 / 0.11157 |
| `noise_head/noise.csv/centered/50/failure_rate` | 64 | 0.21 | 0.22584 ± 0.023441 | 0.225 [0.21, 0.243] | 0.0234 / 0.023724 |
| `noise_head/noise.csv/centered/50/rms_error_mm` | 64 | 4.104 | 4.1781 ± 0.23611 | 4.1435 [4.0135, 4.321] | 0.24544 / 0.22375 |
| `noise_head/noise.csv/centered/500/failure_rate` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise_head/noise.csv/centered/500/rms_error_mm` | 64 | 0.244 | 0.24181 ± 0.0037027 | 0.242 [0.239, 0.244] | 0.0038185 / 0.0036434 |
| `noise_head/noise.csv/centered/5000/failure_rate` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise_head/noise.csv/centered/5000/rms_error_mm` | 64 | 0.247 | 0.24733 ± 0.0016814 | 0.247 [0.246, 0.248] | 0.0015024 / 0.0018448 |
| `noise_head/noise.csv/edge_8mm/100/failure_rate` | 64 | 0.163 | 0.15578 ± 0.021425 | 0.155 [0.14, 0.17075] | 0.019544 / 0.023278 |
| `noise_head/noise.csv/edge_8mm/100/rms_error_mm` | 64 | 4.189 | 4.5953 ± 0.35727 | 4.5975 [4.3703, 4.8723] | 0.35244 / 0.34842 |
| `noise_head/noise.csv/edge_8mm/25/failure_rate` | 64 | 0.66 | 0.65591 ± 0.024275 | 0.655 [0.64, 0.674] | 0.023724 / 0.02354 |
| `noise_head/noise.csv/edge_8mm/25/rms_error_mm` | 64 | 9.314 | 9.4454 ± 0.27336 | 9.426 [9.3268, 9.6172] | 0.26082 / 0.26705 |
| `noise_head/noise.csv/edge_8mm/250/failure_rate` | 64 | 0.007 | 0.0084219 ± 0.0050578 | 0.01 [0.003, 0.01] | 0.0050353 / 0.0049757 |
| `noise_head/noise.csv/edge_8mm/250/rms_error_mm` | 64 | 1.268 | 1.2005 ± 0.2954 | 1.251 [1.0268, 1.418] | 0.32521 / 0.26168 |
| `noise_head/noise.csv/edge_8mm/50/failure_rate` | 64 | 0.38 | 0.41322 ± 0.029781 | 0.41 [0.39225, 0.433] | 0.033405 / 0.025733 |
| `noise_head/noise.csv/edge_8mm/50/rms_error_mm` | 64 | 6.918 | 7.4048 ± 0.36055 | 7.3935 [7.2097, 7.5975] | 0.41972 / 0.29668 |
| `noise_head/noise.csv/edge_8mm/500/failure_rate` | 64 | 0 | 9.375e-05 ± 0.0005261 | 0 [0, 0] | 0.0007378 / 0 |
| `noise_head/noise.csv/edge_8mm/500/rms_error_mm` | 64 | 0.547 | 0.55541 ± 0.060354 | 0.5445 [0.54, 0.551] | 0.08407 / 0.010092 |
| `noise_head/noise.csv/edge_8mm/5000/failure_rate` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise_head/noise.csv/edge_8mm/5000/rms_error_mm` | 64 | 0.544 | 0.53114 ± 0.0090936 | 0.53 [0.525, 0.539] | 0.008999 / 0.0092942 |
| `noise_orig/noise.csv/centered/100/failure_rate` | 64 | 0.003 | 0.0049531 ± 0.0043696 | 0.003 [0.00225, 0.007] | 0.0045516 / 0.0042498 |
| `noise_orig/noise.csv/centered/100/rms_error_mm` | 64 | 0.873 | 0.87425 ± 0.20338 | 0.8535 [0.6815, 1.0065] | 0.19956 / 0.21007 |
| `noise_orig/noise.csv/centered/25/failure_rate` | 64 | 0.347 | 0.37859 ± 0.023078 | 0.3785 [0.363, 0.39] | 0.024239 / 0.021787 |
| `noise_orig/noise.csv/centered/25/rms_error_mm` | 64 | 5.278 | 5.3239 ± 0.19791 | 5.2905 [5.2133, 5.477] | 0.21163 / 0.18388 |
| `noise_orig/noise.csv/centered/250/failure_rate` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise_orig/noise.csv/centered/250/rms_error_mm` | 64 | 0.446 | 0.44069 ± 0.013208 | 0.442 [0.431, 0.45025] | 0.012365 / 0.014165 |
| `noise_orig/noise.csv/centered/50/failure_rate` | 64 | 0.07 | 0.093984 ± 0.019941 | 0.093 [0.083, 0.107] | 0.02185 / 0.018114 |
| `noise_orig/noise.csv/centered/50/rms_error_mm` | 64 | 2.435 | 2.74 ± 0.28647 | 2.7245 [2.5895, 2.916] | 0.32249 / 0.25 |
| `noise_orig/noise.csv/centered/500/failure_rate` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise_orig/noise.csv/centered/500/rms_error_mm` | 64 | 0.365 | 0.37006 ± 0.0053031 | 0.37 [0.366, 0.374] | 0.0052697 / 0.0054087 |
| `noise_orig/noise.csv/centered/5000/failure_rate` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise_orig/noise.csv/centered/5000/rms_error_mm` | 64 | 0.355 | 0.35395 ± 0.0013736 | 0.354 [0.353, 0.355] | 0.0015263 / 0.0012165 |
| `noise_orig/noise.csv/edge_8mm/100/failure_rate` | 64 | 0.01 | 0.013953 ± 0.0069108 | 0.013 [0.01, 0.01775] | 0.0062032 / 0.0076511 |
| `noise_orig/noise.csv/edge_8mm/100/rms_error_mm` | 64 | 1.589 | 1.4656 ± 0.35371 | 1.4235 [1.2348, 1.6377] | 0.33477 / 0.37705 |
| `noise_orig/noise.csv/edge_8mm/25/failure_rate` | 64 | 0.393 | 0.45913 ± 0.031689 | 0.455 [0.44, 0.48075] | 0.032364 / 0.031483 |
| `noise_orig/noise.csv/edge_8mm/25/rms_error_mm` | 64 | 7.496 | 7.9691 ± 0.32413 | 7.9755 [7.7065, 8.211] | 0.33547 / 0.31553 |
| `noise_orig/noise.csv/edge_8mm/250/failure_rate` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise_orig/noise.csv/edge_8mm/250/rms_error_mm` | 64 | 0.532 | 0.53238 ± 0.0080839 | 0.533 [0.528, 0.538] | 0.0067346 / 0.0092108 |
| `noise_orig/noise.csv/edge_8mm/50/failure_rate` | 64 | 0.153 | 0.14875 ± 0.022948 | 0.147 [0.133, 0.167] | 0.024871 / 0.02067 |
| `noise_orig/noise.csv/edge_8mm/50/rms_error_mm` | 64 | 4.358 | 4.4541 ± 0.42554 | 4.495 [4.2255, 4.7467] | 0.46106 / 0.38218 |
| `noise_orig/noise.csv/edge_8mm/500/failure_rate` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise_orig/noise.csv/edge_8mm/500/rms_error_mm` | 64 | 0.5 | 0.49908 ± 0.0056577 | 0.498 [0.495, 0.50225] | 0.0056454 / 0.0057592 |
| `noise_orig/noise.csv/edge_8mm/5000/failure_rate` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `noise_orig/noise.csv/edge_8mm/5000/rms_error_mm` | 64 | 0.501 | 0.49856 ± 0.0018676 | 0.498 [0.497, 0.5] | 0.001877 / 0.0018619 |
| `nonprop/nonprop.csv/662/GAGG/full_energy_frac` | 64 | 0.196 | 0.19606 ± 0.00034886 | 0.1961 [0.1958, 0.1963] | 0.00037221 / 0.00032922 |
| `nonprop/nonprop.csv/662/GAGG/intrinsic_fwhm_pct` | 64 | 1.364 | 1.3657 ± 0.0016779 | 1.366 [1.3647, 1.367] | 0.0016848 / 0.0016965 |
| `nonprop/nonprop.csv/662/GAGG/peak_shift_pct` | 64 | 0.89 | 0.88858 ± 0.0014343 | 0.889 [0.888, 0.88925] | 0.0014142 / 0.001456 |
| `openfraction/mura` | 32 | 46.162 | 46.207 ± 0.052929 | 46.194 [46.164, 46.247] | 0.050904 / 0.055398 |
| `openfraction/ratio_best` | 32 | 3.9393 | 3.9835 ± 0.050657 | 3.9894 [3.9585, 4.0208] | 0.057128 / 0.03881 |
| `openfraction/ratio_half` | 32 | 4.0742 | 4.1194 ± 0.058268 | 4.1295 [4.0726, 4.1576] | 0.068096 / 0.048362 |
| `precise/scenario/eff` | 32 | 0.00024966 | 0.00024904 ± 2.7149e-07 | 0.00024899 [0.00024883, 0.00024921] | 2.3802e-07 / 3.0633e-07 |
| `precise/scenario/error` | 32 | 0.35354 | 0.35267 ± 0.0011635 | 0.35283 [0.35174, 0.35355] | 0.0011194 / 0.0011579 |
| `precise/scenario/margin` | 32 | 1.1562 | 1.1574 ± 0.0016513 | 1.1573 [1.1566, 1.1588] | 0.0012825 / 0.0019715 |
| `precise/scenario/x` | 32 | 0.29527 | 0.29423 ± 0.0013952 | 0.29442 [0.2931, 0.29527] | 0.001342 / 0.0013888 |
| `precise/scenario_ghost/eff` | 32 | 0.00021783 | 0.00021794 ± 2.5273e-07 | 0.0002179 [0.0002178, 0.00021811] | 2.7216e-07 / 2.3292e-07 |
| `precise/scenario_ghost/error` | 32 | 18.402 | 18.403 ± 0.0008055 | 18.403 [18.402, 18.403] | 0.00086654 / 0.00070395 |
| `precise/scenario_ghost/margin` | 32 | 1.1981 | 1.1969 ± 0.0030195 | 1.1969 [1.1954, 1.1983] | 0.0032447 / 0.0028823 |
| `precise/scenario_ghost/x` | 32 | -6.4007 | -6.4016 ± 0.00080555 | -6.4018 [-6.4022, -6.4009] | 0.00086659 / 0.00070399 |
| `precise/scenario_handheld/eff` | 32 | 0.00024836 | 0.00024786 ± 2.2778e-07 | 0.00024784 [0.00024773, 0.00024799] | 2.7646e-07 / 1.7524e-07 |
| `precise/scenario_handheld/error` | 32 | 0.24861 | 0.24924 ± 0.00094211 | 0.24919 [0.24858, 0.25012] | 0.00097396 / 0.00092497 |
| `precise/scenario_handheld/margin` | 32 | 1.3531 | 1.3521 ± 0.003422 | 1.353 [1.3491, 1.3545] | 0.003511 / 0.0031489 |
| `precise/scenario_handheld/x` | 32 | 0.21555 | 0.21538 ± 0.00062533 | 0.21539 [0.21494, 0.21565] | 0.00073638 / 0.00044061 |
| `precise/scenario_handheld_ir192/eff` | 32 | 0.00026216 | 0.00026219 ± 2.0785e-07 | 0.00026217 [0.0002621, 0.00026233] | 1.7156e-07 / 2.4462e-07 |
| `precise/scenario_handheld_ir192/error` | 32 | 0.24112 | 0.24078 ± 0.0029409 | 0.24137 [0.24065, 0.24201] | 0.0032439 / 0.0027096 |
| `precise/scenario_handheld_ir192/margin` | 32 | 1.3417 | 1.3692 ± 0.11009 | 1.341 [1.3392, 1.3443] | 0.1129 / 0.11089 |
| `precise/scenario_handheld_ir192/x` | 32 | 0.20695 | 0.20669 ± 0.0010527 | 0.20676 [0.2063, 0.20705] | 0.0012449 / 0.00083285 |
| `precise/scenario_ir192/eff` | 32 | 0.00020476 | 0.0002047 ± 1.7217e-07 | 0.00020466 [0.00020456, 0.00020482] | 1.8303e-07 / 1.5967e-07 |
| `precise/scenario_ir192/error` | 32 | 0.3247 | 0.32337 ± 0.00060728 | 0.32339 [0.323, 0.32368] | 0.00064582 / 0.00057669 |
| `precise/scenario_ir192/margin` | 32 | 1.1542 | 1.154 ± 0.0011599 | 1.1541 [1.1534, 1.1547] | 0.0011012 / 0.0012031 |
| `precise/scenario_ir192/x` | 32 | 0.26004 | 0.25837 ± 0.00076 | 0.25841 [0.25792, 0.25876] | 0.00080819 / 0.00072178 |
| `precise/scenario_offaxis/eff` | 32 | 0.00024632 | 0.00024576 ± 2.2351e-07 | 0.00024578 [0.00024562, 0.00024585] | 2.5888e-07 / 1.902e-07 |
| `precise/scenario_offaxis/error` | 32 | 0.19642 | 0.19642 ± 0 | 0.19642 [0.19642, 0.19642] | 0 / 0 |
| `precise/scenario_offaxis/margin` | 32 | 1.2708 | 1.2691 ± 0.0026915 | 1.27 [1.268, 1.2708] | 0.0029126 / 0.002526 |
| `precise/scenario_offaxis/x` | 32 | 6.0278 | 6.0278 ± 0 | 6.0278 [6.0278, 6.0278] | 0 / 0 |
| `precise/scenario_orig_gagg/eff` | 32 | 0.00010023 | 9.9977e-05 ± 1.0899e-07 | 9.9955e-05 [9.9891e-05, 0.00010004] | 9.555e-08 / 1.2298e-07 |
| `precise/scenario_orig_gagg/error` | 32 | 0.35353 | 0.35266 ± 0.0011633 | 0.35282 [0.35172, 0.35353] | 0.0011192 / 0.0011576 |
| `precise/scenario_orig_gagg/margin` | 32 | 1.1562 | 1.1575 ± 0.0016514 | 1.1573 [1.1567, 1.1588] | 0.0012827 / 0.0019716 |
| `precise/scenario_orig_gagg/x` | 32 | 0.29525 | 0.29421 ± 0.0013949 | 0.2944 [0.29308, 0.29525] | 0.0013417 / 0.0013885 |
| `rtl_frontend/CR-RC^4/noise_fwhm` | 32 | 1.1192 | 1.0872 ± 0.038559 | 1.0892 [1.0633, 1.112] | 0.045023 / 0.032336 |
| `rtl_frontend/CR-RC^4/photopeak_events` | 32 | 479 | 488.16 ± 20.274 | 486.5 [473.25, 503] | 19.523 / 21.601 |
| `rtl_frontend/CR-RC^4/within5_pct` | 32 | 64.718 | 68.745 ± 2.8224 | 69.316 [66.827, 70.43] | 3.05 / 2.6137 |
| `rtl_frontend/adc_delta` | 32 | 0.0028102 | 0.0024773 ± 0.0017767 | 0.0024943 [0.0015324, 0.0037453] | 0.0015077 / 0.0018977 |
| `rtl_frontend/cusp/noise_fwhm` | 32 | 1.0373 | 1.0079 ± 0.031899 | 1.0111 [0.99655, 1.0312] | 0.034063 / 0.030568 |
| `rtl_frontend/cusp/photopeak_events` | 32 | 479 | 488.16 ± 20.274 | 486.5 [473.25, 503] | 19.523 / 21.601 |
| `rtl_frontend/cusp/within5_pct` | 32 | 68.476 | 70.865 ± 2.4658 | 70.716 [69.542, 72.253] | 2.222 / 2.7137 |
| `rtl_frontend/cusp_beats_crrc` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `rtl_frontend/electronic/fwhm` | 32 | 1.6305 | 1.6993 ± 0.062353 | 1.7072 [1.6593, 1.744] | 0.067622 / 0.050422 |
| `rtl_frontend/full/fwhm` | 32 | 6.3193 | 6.1563 ± 0.24657 | 6.1709 [6.0589, 6.2992] | 0.25604 / 0.24407 |
| `rtl_frontend/full_no_adc_noise/fwhm` | 32 | 6.3165 | 6.1538 ± 0.24658 | 6.1711 [6.0557, 6.2951] | 0.25556 / 0.24465 |
| `rtl_frontend/intrinsic/fwhm` | 32 | 6.1028 | 5.9174 ± 0.24725 | 5.9182 [5.7654, 6.0902] | 0.24464 / 0.2577 |
| `rtl_frontend/trapezoid/noise_fwhm` | 32 | 1.7105 | 1.6883 ± 0.057518 | 1.6797 [1.6469, 1.7305] | 0.056833 / 0.059582 |
| `rtl_frontend/trapezoid/photopeak_events` | 32 | 479 | 488.16 ± 20.274 | 486.5 [473.25, 503] | 19.523 / 21.601 |
| `rtl_frontend/trapezoid/within5_pct` | 32 | 39.875 | 42.4 ± 2.347 | 42.499 [40.225, 43.785] | 2.2509 / 2.4805 |
| `rtl_material/CeBr3/efficiency` | 128 | 0.86867 | 0.86183 ± 0.0081105 | 0.862 [0.856, 0.86733] | 0.0072748 / 0.0089019 |
| `rtl_material/CeBr3/fwhm` | 128 | 7.5245 | 7.2182 ± 0.16771 | 7.2118 [7.1299, 7.3195] | 0.17491 / 0.15615 |
| `rtl_material/CeBr3/rate` | 128 | 2e+06 | 2e+06 ± 0 | 2e+06 [2e+06, 2e+06] | 0 / 0 |
| `rtl_material/GAGG/efficiency` | 128 | 0.002 | 0.042557 ± 0.018818 | 0.042 [0.030667, 0.056667] | 0.016992 / 0.020613 |
| `rtl_material/GAGG/fwhm` | 101 | - | 5.6696 ± 1.2652 | 5.5584 [4.7457, 6.3388] | 1.2159 / 1.3223 |
| `rtl_material/GAGG/rate` | 128 | 1e+06 | 1e+06 ± 0 | 1e+06 [1e+06, 1e+06] | 0 / 0 |
| `rtl_multi/cs/fwhm122` | 32 | - | 15.91 ± 0.23766 | 15.877 [15.764, 16.049] | 0.2244 / 0.25596 |
| `rtl_multi/cs/fwhm662` | 32 | - | 3.5187 ± 0.08636 | 3.5127 [3.4534, 3.563] | 0.083813 / 0.090164 |
| `rtl_multi/low/fwhm122` | 32 | - | 16.284 ± 0.22262 | 16.264 [16.135, 16.436] | 0.22895 / 0.21757 |
| `rtl_multi/low/fwhm662` | 32 | - | 3.5113 ± 0.085207 | 3.5303 [3.4568, 3.569] | 0.086298 / 0.085381 |
| `rtl_peak/all/efficiency` | 32 | 0.4395 | 0.45284 ± 0.012506 | 0.45075 [0.44563, 0.46163] | 0.01205 / 0.013185 |
| `rtl_peak/all/fwhm` | 32 | 8.245 | 7.9811 ± 0.2528 | 7.9486 [7.8231, 8.0931] | 0.27098 / 0.23286 |
| `rtl_pixel/0.15/cal_fwhm` | 32 | - | 7.2912 ± 0.057491 | 7.2808 [7.2536, 7.3284] | 0.046343 / 0.060852 |
| `rtl_pixel/0.15/raw_fwhm` | 32 | - | 18.425 ± 0.10767 | 18.428 [18.334, 18.504] | 0.099112 / 0.1137 |
| `rtl_pixel/0/cal_fwhm` | 32 | - | 7.3213 ± 0.058663 | 7.3142 [7.285, 7.3578] | 0.049333 / 0.058945 |
| `rtl_pixel/0/raw_fwhm` | 32 | - | 7.3213 ± 0.058663 | 7.3142 [7.285, 7.3578] | 0.049333 / 0.058945 |
| `scan/11/1.00/30.0/median_error_mm` | 32 | 1.1 | 1.1037 ± 0.009552 | 1.104 [1.1, 1.108] | 0.0097483 / 0.0096713 |
| `scan/11/1.00/30.0/usable_fraction` | 32 | 0.901 | 0.90728 ± 0.0073319 | 0.909 [0.901, 0.909] | 0.0050596 / 0.0086253 |
| `scan/11/1.00/30.0/usable_half_mm` | 32 | 21.49 | 21.566 ± 0.088999 | 21.588 [21.49, 21.588] | 0.062236 / 0.10429 |
| `scan/13/1.50/20.0/median_error_mm` | 32 | 6.182 | 6.5089 ± 0.3287 | 6.618 [6.481, 6.6833] | 0.43404 / 0.16239 |
| `scan/13/1.50/20.0/usable_fraction` | 32 | 0.628 | 0.62825 ± 0.0055591 | 0.628 [0.628, 0.63] | 0.0043512 / 0.0061968 |
| `scan/13/1.50/20.0/usable_half_mm` | 32 | 44.045 | 44.053 ± 0.20133 | 44.045 [44.045, 44.117] | 0.15785 / 0.22433 |
| `scan/23/1.00/20.0/median_error_mm` | 32 | 65.443 | 66.412 ± 1.224 | 66.5 [65.447, 66.583] | 1.0054 / 1.4363 |
| `scan/23/1.00/20.0/usable_fraction` | 32 | 0.149 | 0.15025 ± 0.0029512 | 0.149 [0.149, 0.149] | 0.0035777 / 0.002 |
| `scan/23/1.00/20.0/usable_half_mm` | 32 | 25.282 | 25.391 ± 0.2555 | 25.282 [25.282, 25.282] | 0.30973 / 0.17315 |
| `scan/7/1.00/60.0/median_error_mm` | 32 | 0.282 | 0.27963 ± 0.00407 | 0.279 [0.27775, 0.2825] | 0.0029861 / 0.0050183 |
| `scan/7/1.00/60.0/usable_fraction` | 32 | 0.934 | 0.92916 ± 0.0055306 | 0.93 [0.926, 0.934] | 0.0051051 / 0.0060869 |
| `scan/7/1.00/60.0/usable_half_mm` | 32 | 8.5688 | 8.5463 ± 0.025322 | 8.5498 [8.5308, 8.5688] | 0.02356 / 0.027697 |
| `scan/r11/pass90` | 32 | 1 | 0.96875 ± 0.17678 | 1 [1, 1] | 0 / 0.25 |
| `scanextra/0/23/1.00/20.0/median_error_mm` | 32 | 0.928 | 0.92381 ± 0.021507 | 0.927 [0.9095, 0.93925] | 0.024484 / 0.018576 |
| `scanextra/0/23/1.00/20.0/usable_fraction` | 32 | 0.769 | 0.764 ± 0.0068533 | 0.7645 [0.76, 0.769] | 0.0069666 / 0.0068118 |
| `scanextra/1/13/2.00/20.0/median_error_mm` | 32 | 78.564 | 79.09 ± 1.8527 | 78.548 [78.18, 78.581] | 1.5165 / 2.1426 |
| `scanextra/1/13/2.00/20.0/usable_fraction` | 32 | 0.289 | 0.28984 ± 0.0026653 | 0.289 [0.289, 0.289] | 0.003628 / 0 |
| `scanextra/1/13/2.00/30.0/median_error_mm` | 32 | 87.124 | 87.148 ± 0.012923 | 87.149 [87.139, 87.159] | 0.013079 / 0.013185 |
| `scanextra/1/13/2.00/30.0/usable_fraction` | 32 | 0.05 | 0.05 ± 0 | 0.05 [0.05, 0.05] | 0 / 0 |
| `scanextra/1/13/2.00/40.0/median_error_mm` | 32 | 69.975 | 69.973 ± 0.07333 | 69.985 [69.979, 69.992] | 0.011039 / 0.10298 |
| `scanextra/1/13/2.00/40.0/usable_fraction` | 32 | 0.025 | 0.025 ± 0 | 0.025 [0.025, 0.025] | 0 / 0 |
| `scanextra/1/13/2.00/60.0/median_error_mm` | 32 | 52.621 | 52.621 ± 0 | 52.621 [52.621, 52.621] | 0 / 0 |
| `scanextra/1/13/2.00/60.0/usable_fraction` | 32 | 0.008 | 0.008 ± 0 | 0.008 [0.008, 0.008] | 0 / 0 |
| `scanextra/1/13/2.00/80.0/median_error_mm` | 32 | 44.399 | 44.399 ± 0 | 44.399 [44.399, 44.399] | 0 / 0 |
| `scanextra/1/13/2.00/80.0/usable_fraction` | 32 | 0.008 | 0.008 ± 0 | 0.008 [0.008, 0.008] | 0 / 0 |
| `scanextra/1/17/2.00/20.0/median_error_mm` | 32 | 84.736 | 85.749 ± 0.60431 | 85.662 [85.643, 85.688] | 0.64918 / 0.56872 |
| `scanextra/1/17/2.00/20.0/usable_fraction` | 32 | 0.025 | 0.025 ± 0 | 0.025 [0.025, 0.025] | 0 / 0 |
| `scanextra/1/17/2.00/30.0/median_error_mm` | 32 | 66.688 | 67.176 ± 0.95175 | 67.371 [66.707, 67.462] | 0.8369 / 1.045 |
| `scanextra/1/17/2.00/30.0/usable_fraction` | 32 | 0.058 | 0.0585 ± 0.0019675 | 0.058 [0.058, 0.058] | 0 / 0.0027325 |
| `scanextra/1/17/2.00/40.0/median_error_mm` | 32 | 49.265 | 49.032 ± 0.14363 | 48.972 [48.957, 49.159] | 0.13302 / 0.15777 |
| `scanextra/1/17/2.00/40.0/usable_fraction` | 32 | 0.058 | 0.058 ± 0 | 0.058 [0.058, 0.058] | 0 / 0 |
| `scanextra/1/17/2.00/60.0/median_error_mm` | 32 | 40.546 | 39.603 ± 0.93097 | 39.219 [39.06, 40.201] | 0.80167 / 1.0628 |
| `scanextra/1/17/2.00/60.0/usable_fraction` | 32 | 0.066 | 0.0615 ± 0.0040321 | 0.058 [0.058, 0.066] | 0.0040988 / 0.0040988 |
| `scanextra/1/17/2.00/80.0/median_error_mm` | 32 | 32.413 | 32.059 ± 0.73857 | 32.419 [31.052, 32.464] | 0.79822 / 0.6853 |
| `scanextra/1/17/2.00/80.0/usable_fraction` | 32 | 0.025 | 0.0285 ± 0.0040321 | 0.025 [0.025, 0.033] | 0.0040988 / 0.0038297 |
| `scanextra/1/19/2.00/20.0/median_error_mm` | 32 | 123.71 | 125.03 ± 1.3257 | 125.11 [124.33, 126.05] | 0.97576 / 1.6327 |
| `scanextra/1/19/2.00/20.0/usable_fraction` | 32 | 0.083 | 0.082656 ± 0.0034418 | 0.083 [0.083, 0.083] | 0.0038275 / 0.0031085 |
| `scanextra/1/19/2.00/30.0/median_error_mm` | 32 | 87.474 | 87.514 ± 0.024759 | 87.509 [87.497, 87.53] | 0.026486 / 0.020445 |
| `scanextra/1/19/2.00/30.0/usable_fraction` | 32 | 0.008 | 0.008 ± 0 | 0.008 [0.008, 0.008] | 0 / 0 |
| `scanextra/1/19/2.00/40.0/median_error_mm` | 32 | 67.844 | 66.834 ± 0.58474 | 66.529 [66.504, 66.871] | 0.53188 / 0.64072 |
| `scanextra/1/19/2.00/40.0/usable_fraction` | 32 | 0.041 | 0.039 ± 0.004064 | 0.041 [0.041, 0.041] | 0.0038297 / 0.0043512 |
| `scanextra/1/19/2.00/60.0/median_error_mm` | 32 | 52.722 | 52.722 ± 0 | 52.722 [52.722, 52.722] | 0 / 0 |
| `scanextra/1/19/2.00/60.0/usable_fraction` | 32 | 0.008 | 0.008 ± 0 | 0.008 [0.008, 0.008] | 0 / 0 |
| `scanextra/1/19/2.00/80.0/median_error_mm` | 32 | 44.484 | 44.484 ± 0 | 44.484 [44.484, 44.484] | 0 / 0 |
| `scanextra/1/19/2.00/80.0/usable_fraction` | 32 | 0.008 | 0.008 ± 0 | 0.008 [0.008, 0.008] | 0 / 0 |
| `scanextra/1/23/2.00/20.0/median_error_mm` | 32 | 134.04 | 132.09 ± 2.8605 | 133.98 [129.07, 134.1] | 2.804 / 2.9949 |
| `scanextra/1/23/2.00/20.0/usable_fraction` | 32 | 0.008 | 0.008 ± 0 | 0.008 [0.008, 0.008] | 0 / 0 |
| `scanextra/1/23/2.00/30.0/median_error_mm` | 32 | 83.11 | 83.129 ± 0.049754 | 83.13 [83.089, 83.157] | 0.042089 / 0.052613 |
| `scanextra/1/23/2.00/30.0/usable_fraction` | 32 | 0.017 | 0.017 ± 0 | 0.017 [0.017, 0.017] | 0 / 0 |
| `scanextra/1/23/2.00/40.0/median_error_mm` | 32 | 68.344 | 67.012 ± 1.1084 | 67.066 [66.415, 67.384] | 1.2142 / 1.0306 |
| `scanextra/1/23/2.00/40.0/usable_fraction` | 32 | 0.017 | 0.017 ± 0 | 0.017 [0.017, 0.017] | 0 / 0 |
| `scanextra/1/23/2.00/60.0/median_error_mm` | 32 | 50.426 | 50.425 ± 0.0093774 | 50.426 [50.418, 50.433] | 0.0095531 / 0.0095007 |
| `scanextra/1/23/2.00/60.0/usable_fraction` | 32 | 0.008 | 0.008 ± 0 | 0.008 [0.008, 0.008] | 0 / 0 |
| `scanextra/1/23/2.00/80.0/median_error_mm` | 32 | 41.899 | 41.899 ± 0 | 41.899 [41.899, 41.899] | 0 / 0 |
| `scanextra/1/23/2.00/80.0/usable_fraction` | 32 | 0.008 | 0.008 ± 0 | 0.008 [0.008, 0.008] | 0 / 0 |
| `shield/directional_delta` | 128 | 0 | 0.1875 ± 1.1065 | 0 [0, 0] | 1.087 / 1.1335 |
| `shield/knee/co` | 128 | 25 | 26.758 ± 2.3967 | 25 [25, 30] | 2.2258 / 2.5 |
| `shield/knee/cs` | 128 | 20 | 21.094 ± 2.0751 | 20 [20, 20] | 1.967 / 2.1822 |
| `shield/knee/dir` | 128 | 8 | 8.4844 ± 0.86018 | 8 [8, 8] | 0.85391 / 0.87287 |
| `shield/knee/scattered` | 128 | 8 | 8.2969 ± 0.71386 | 8 [8, 8] | 0.73193 / 0.70076 |
| `shield/picked_mass/co` | 128 | 6.836 | 7.8728 ± 1.4136 | 6.836 [6.836, 9.785] | 1.3128 / 1.4745 |
| `shield/picked_mass/cs` | 128 | 4.491 | 5.004 ± 0.97323 | 4.491 [4.491, 4.491] | 0.92252 / 1.0234 |
| `shield/picked_mass/scattered` | 128 | 0.983 | 1.0424 ± 0.14277 | 0.983 [0.983, 0.983] | 0.14639 / 0.14015 |
| `shield/shield.csv/Co60_1250keV/12.0/rms_calib_mm` | 128 | 0.613 | 0.63075 ± 0.1208 | 0.579 [0.559, 0.60625] | 0.11837 / 0.12371 |
| `shield/shield.csv/Co60_1250keV/15.0/rms_calib_mm` | 128 | 0.493 | 0.52926 ± 0.061368 | 0.519 [0.501, 0.53525] | 0.071276 / 0.049866 |
| `shield/shield.csv/Cs137_662keV/12.0/rms_calib_mm` | 128 | 0.446 | 0.46987 ± 0.032663 | 0.469 [0.454, 0.48225] | 0.021406 / 0.040988 |
| `shield/shield.csv/Cs137_662keV/15.0/rms_calib_mm` | 128 | 0.438 | 0.43123 ± 0.015212 | 0.43 [0.42075, 0.443] | 0.015476 / 0.015041 |
| `shield/shield.csv/scattered_250keV/12.0/rms_calib_mm` | 128 | 0.391 | 0.38227 ± 0.008299 | 0.382 [0.376, 0.388] | 0.0080398 / 0.0086112 |
| `shield/shield.csv/scattered_250keV/15.0/rms_calib_mm` | 128 | 0.384 | 0.38079 ± 0.0084431 | 0.38 [0.375, 0.386] | 0.008469 / 0.0084233 |
| `shield/shield.csv/scattered_250keV_dir/12.0/rms_calib_mm` | 128 | 0.385 | 0.38079 ± 0.008197 | 0.38 [0.375, 0.386] | 0.008224 / 0.0082031 |
| `shield/shield.csv/scattered_250keV_dir/15.0/rms_calib_mm` | 128 | 0.37 | 0.37838 ± 0.007741 | 0.3765 [0.373, 0.384] | 0.0084786 / 0.0069668 |
| `spatial/1/error0` | 128 | 0.4714 | 0.4714 ± 0 | 0.4714 [0.4714, 0.4714] | 0 / 0 |
| `spatial/1/error1` | 128 | 1.0995 | 1.0995 ± 0 | 1.0995 [1.0995, 1.0995] | 0 / 0 |
| `spatial/2/error0` | 128 | 0.4714 | 0.60069 ± 1.0302 | 0.4714 [0.4714, 0.4714] | 1.0343 / 1.0343 |
| `spatial/2/error1` | 128 | 1.0995 | 1.0995 ± 0 | 1.0995 [1.0995, 1.0995] | 0 / 0 |
| `spatial/4/error0` | 128 | 8.7458 | 8.6971 ± 0.18922 | 8.7458 [8.7458, 8.7458] | 0.18997 / 0.18997 |
| `spatial/4/error1` | 128 | 1.0995 | 1.0995 ± 0 | 1.0995 [1.0995, 1.0995] | 0 / 0 |
| `spatial/8/error0` | 128 | 8.7458 | 8.6667 ± 0.23613 | 8.7458 [8.7458, 8.7458] | 0.24494 / 0.22875 |
| `spatial/8/error1` | 128 | 1.0995 | 1.0995 ± 0 | 1.0995 [1.0995, 1.0995] | 0 / 0 |
| `spatial/co2_cs_submm` | 128 | 1 | 0.98438 ± 0.12451 | 1 [1, 1] | 0.125 / 0.125 |
| `subcell/subcell.csv/1.80/rms_gauss` | 64 | 0.1298 | 0.13019 ± 0.0018526 | 0.1304 [0.12887, 0.13123] | 0.0018646 / 0.0018589 |
| `subcell/subcell.csv/1.80/rms_none` | 64 | 0.5241 | 0.52705 ± 0.0042592 | 0.5241 [0.5241, 0.5331] | 0.0045361 / 0.0037801 |
| `subcell/subcell.csv/1.80/rms_parab` | 64 | 0.1706 | 0.17048 ± 0.0013359 | 0.1706 [0.1697, 0.17122] | 0.0012961 / 0.0013507 |
| `subcell/subcell.csv/1.80/rms_tent` | 64 | 0.1305 | 0.1307 ± 0.0017739 | 0.13085 [0.1295, 0.1317] | 0.0017524 / 0.0018074 |
| `subcell/subcell.csv/2.40/rms_gauss` | 64 | 0.2704 | 0.27092 ± 0.0011445 | 0.2708 [0.27017, 0.27153] | 0.0012373 / 0.0010088 |
| `subcell/subcell.csv/2.40/rms_none` | 64 | 0.6638 | 0.6638 ± 0 | 0.6638 [0.6638, 0.6638] | 0 / 0 |
| `subcell/subcell.csv/2.40/rms_parab` | 64 | 0.2632 | 0.26399 ± 0.0010956 | 0.264 [0.2633, 0.26472] | 0.0012837 / 0.00084118 |
| `subcell/subcell.csv/2.40/rms_tent` | 64 | 0.1916 | 0.19422 ± 0.0016515 | 0.19435 [0.1931, 0.19523] | 0.0018801 / 0.0013877 |
| `sweep/cyclic` | 64 | 144 | 146 ± 1.2971 | 146 [145, 147] | 1.1914 / 1.3678 |
| `sweep/noncyclic` | 64 | 273 | 278.14 ± 3.2653 | 278 [276, 280.25] | 3.532 / 3.0134 |
| `sweep_head/cyclic` | 64 | 173 | 173.67 ± 1.9925 | 174 [172, 175] | 1.8217 / 2.0652 |
| `sweep_head/noncyclic` | 64 | 362 | 361.23 ± 1.9334 | 361 [360, 362] | 2.1841 / 1.6261 |
| `thermal/thermal_off.csv/1.000/efficiency` | 64 | 0.9064 | 0.90623 ± 9.8349e-05 | 0.9062 [0.9062, 0.9063] | 0.00010035 / 9.7499e-05 |
| `thermal/thermal_off.csv/1.000/residual_cov` | 64 | 0.0671 | 0.067072 ± 6.539e-05 | 0.0671 [0.067, 0.0671] | 7.0639e-05 / 6.0158e-05 |
| `thermal/thermal_off.csv/1.000/rms_bias_mm` | 64 | 0.509 | 0.47856 ± 0.034679 | 0.479 [0.45375, 0.50325] | 0.032691 / 0.036391 |
| `thermal/thermal_on.csv/1.000/efficiency` | 64 | 0.9994 | 0.9994 ± 0 | 0.9994 [0.9994, 0.9994] | 0 / 0 |
| `thermal/thermal_on.csv/1.000/residual_cov` | 64 | 0.0006 | 0.0006 ± 0 | 0.0006 [0.0006, 0.0006] | 0 / 0 |
| `thermal/thermal_on.csv/1.000/rms_bias_mm` | 64 | 0.428 | 0.45416 ± 0.032069 | 0.448 [0.435, 0.47625] | 0.030239 / 0.033956 |
| `thickness/best` | 64 | 6 | 6 ± 0 | 6 [6, 6] | 0 / 0 |
| `thickness/maxradius_member/10.0` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `thickness/maxradius_member/14.0` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `thickness/maxradius_member/18.0` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `thickness/maxradius_member/2.0` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `thickness/maxradius_member/24.0` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `thickness/maxradius_member/32.0` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `thickness/maxradius_member/4.0` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `thickness/maxradius_member/6.0` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `thickness/maxradius_member/8.0` | 64 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `thickness/thickness.csv/10.0/edge_ratio` | 64 | 0.94473 | 0.94598 ± 0.0012731 | 0.94617 [0.94501, 0.94658] | 0.0012577 / 0.0012958 |
| `thickness/thickness.csv/10.0/eff_center` | 64 | 0.0002497 | 0.00024902 ± 2.6737e-07 | 0.000249 [0.00024887, 0.0002492] | 2.6879e-07 / 2.6753e-07 |
| `thickness/thickness.csv/10.0/eff_edge` | 64 | 0.0002359 | 0.00023557 ± 2.5503e-07 | 0.0002356 [0.0002354, 0.0002358] | 2.5495e-07 / 2.5903e-07 |
| `thickness/thickness.csv/10.0/usable_radius_mm` | 64 | 8.87 | 8.87 ± 0 | 8.87 [8.87, 8.87] | 0 / 0 |
| `thickness/thickness.csv/32.0/edge_ratio` | 64 | 0.68698 | 0.68847 ± 0.0017676 | 0.68844 [0.68753, 0.68978] | 0.0015833 / 0.001889 |
| `thickness/thickness.csv/32.0/eff_center` | 64 | 0.0001383 | 0.00013789 ± 2.1286e-07 | 0.0001379 [0.0001377, 0.00013803] | 2.3416e-07 / 1.8247e-07 |
| `thickness/thickness.csv/32.0/eff_edge` | 64 | 9.501e-05 | 9.4933e-05 ± 1.6906e-07 | 9.4955e-05 [9.4857e-05, 9.5022e-05] | 1.3587e-07 / 1.9693e-07 |
| `thickness/thickness.csv/32.0/usable_radius_mm` | 64 | 8.87 | 8.87 ± 0 | 8.87 [8.87, 8.87] | 0 / 0 |
| `thickness/thickness.csv/6.0/edge_ratio` | 64 | 0.97803 | 0.97901 ± 0.0011101 | 0.97902 [0.97833, 0.97968] | 0.00097039 / 0.0012436 |
| `thickness/thickness.csv/6.0/eff_center` | 64 | 0.0002959 | 0.00029538 ± 2.6829e-07 | 0.00029535 [0.0002952, 0.00029553] | 2.482e-07 / 2.8954e-07 |
| `thickness/thickness.csv/6.0/eff_edge` | 64 | 0.0002894 | 0.00028917 ± 2.4753e-07 | 0.0002892 [0.00028907, 0.00028932] | 2.4021e-07 / 2.5833e-07 |
| `thickness/thickness.csv/6.0/usable_radius_mm` | 64 | 8.87 | 8.87 ± 0 | 8.87 [8.87, 8.87] | 0 / 0 |
| `thickness/thickness.csv/8.0/edge_ratio` | 64 | 0.96405 | 0.96512 ± 0.0012543 | 0.96509 [0.96426, 0.96592] | 0.0011978 / 0.0013142 |
| `thickness/thickness.csv/8.0/eff_center` | 64 | 0.0002698 | 0.00026916 ± 2.7631e-07 | 0.00026915 [0.000269, 0.0002693] | 2.7251e-07 / 2.8268e-07 |
| `thickness/thickness.csv/8.0/eff_edge` | 64 | 0.0002601 | 0.00025977 ± 2.3111e-07 | 0.0002598 [0.0002596, 0.0002599] | 2.326e-07 / 2.3245e-07 |
| `thickness/thickness.csv/8.0/usable_radius_mm` | 64 | 8.87 | 8.87 ± 0 | 8.87 [8.87, 8.87] | 0 / 0 |
| `thickness_wide/best` | 32 | 14 | 11.188 ± 1.9582 | 10 [10, 14] | 2.0616 / 1.9149 |
| `thickness_wide/thickness.csv/10.0/edge_ratio` | 32 | 0.73938 | 0.74004 ± 0.00095268 | 0.74027 [0.73935, 0.74075] | 0.00095922 / 0.00094186 |
| `thickness_wide/thickness.csv/10.0/eff_center` | 32 | 0.0004355 | 0.00043447 ± 3.9919e-07 | 0.00043445 [0.0004342, 0.0004348] | 4.3818e-07 / 3.6878e-07 |
| `thickness_wide/thickness.csv/10.0/eff_edge` | 32 | 0.000322 | 0.00032153 ± 3.3815e-07 | 0.00032155 [0.0003212, 0.0003218] | 3.4779e-07 / 3.2838e-07 |
| `thickness_wide/thickness.csv/10.0/usable_radius_mm` | 32 | 16.62 | 17.716 ± 0.90971 | 18.29 [16.62, 18.29] | 0.79945 / 1.0322 |
| `thickness_wide/thickness.csv/32.0/edge_ratio` | 32 | 0.29923 | 0.29962 ± 0.0015843 | 0.29942 [0.29863, 0.30063] | 0.0010724 / 0.0019967 |
| `thickness_wide/thickness.csv/32.0/eff_center` | 32 | 0.0002085 | 0.00020806 ± 2.8496e-07 | 0.00020805 [0.0002079, 0.0002082] | 2.6458e-07 / 2.977e-07 |
| `thickness_wide/thickness.csv/32.0/eff_edge` | 32 | 6.239e-05 | 6.2338e-05 ± 2.93e-07 | 6.23e-05 [6.214e-05, 6.2567e-05] | 1.9886e-07 / 3.635e-07 |
| `thickness_wide/thickness.csv/32.0/usable_radius_mm` | 32 | 13.3 | 13.767 ± 0.75829 | 13.3 [13.3, 14.96] | 0.567 / 0.8505 |
| `thickness_wide/thickness.csv/6.0/edge_ratio` | 32 | 0.85025 | 0.8493 ± 0.00090445 | 0.84922 [0.84867, 0.85011] | 0.0010131 / 0.00081417 |
| `thickness_wide/thickness.csv/6.0/eff_center` | 32 | 0.0005242 | 0.00052408 ± 3.316e-07 | 0.0005241 [0.0005238, 0.0005243] | 3.1198e-07 / 3.4034e-07 |
| `thickness_wide/thickness.csv/6.0/eff_edge` | 32 | 0.0004457 | 0.0004451 ± 4.1035e-07 | 0.0004451 [0.0004448, 0.00044542] | 4.5014e-07 / 3.7232e-07 |
| `thickness_wide/thickness.csv/6.0/usable_radius_mm` | 32 | 0 | 2.7009 ± 3.3457 | 1.66 [0, 4.99] | 3.4616 / 3.3253 |
| `thickness_wide/thickness.csv/8.0/edge_ratio` | 32 | 0.79806 | 0.79761 ± 0.00099259 | 0.7977 [0.79675, 0.79816] | 0.0011388 / 0.00085661 |
| `thickness_wide/thickness.csv/8.0/eff_center` | 32 | 0.0004749 | 0.00047447 ± 3.832e-07 | 0.0004745 [0.0004742, 0.0004748] | 4.4977e-07 / 3.1807e-07 |
| `thickness_wide/thickness.csv/8.0/eff_edge` | 32 | 0.000379 | 0.00037844 ± 4.4855e-07 | 0.00037855 [0.0003781, 0.0003788] | 5.1409e-07 / 3.8794e-07 |
| `thickness_wide/thickness.csv/8.0/usable_radius_mm` | 32 | 16.62 | 15.79 ± 0.94367 | 14.96 [14.96, 16.62] | 1.0009 / 0.79466 |
| `uniformity/uniformity.csv/0.000/fail_corrected` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `uniformity/uniformity.csv/0.000/fail_raw` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `uniformity/uniformity.csv/0.000/gain_gradient` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `uniformity/uniformity.csv/0.000/gain_sigma` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `uniformity/uniformity.csv/0.000/rms_corrected_mm` | 64 | 0.414 | 0.45503 ± 0.022844 | 0.4525 [0.43975, 0.474] | 0.023787 / 0.022233 |
| `uniformity/uniformity.csv/0.000/rms_raw_mm` | 64 | 0.435 | 0.48522 ± 0.024016 | 0.4865 [0.467, 0.50425] | 0.024264 / 0.023992 |
| `uniformity/uniformity.csv/0.250/fail_corrected` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `uniformity/uniformity.csv/0.250/fail_raw` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `uniformity/uniformity.csv/0.250/gain_gradient` | 64 | 0.15 | 0.15 ± 0 | 0.15 [0.15, 0.15] | 0 / 0 |
| `uniformity/uniformity.csv/0.250/gain_sigma` | 64 | 0.1 | 0.1 ± 0 | 0.1 [0.1, 0.1] | 0 / 0 |
| `uniformity/uniformity.csv/0.250/rms_corrected_mm` | 64 | 0.445 | 0.46692 ± 0.020465 | 0.465 [0.45375, 0.48025] | 0.017852 / 0.023073 |
| `uniformity/uniformity.csv/0.250/rms_raw_mm` | 64 | 0.456 | 0.48998 ± 0.024684 | 0.4895 [0.4685, 0.508] | 0.019894 / 0.028987 |
| `uniformity/uniformity.csv/0.500/fail_corrected` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `uniformity/uniformity.csv/0.500/fail_raw` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `uniformity/uniformity.csv/0.500/gain_gradient` | 64 | 0.3 | 0.3 ± 0 | 0.3 [0.3, 0.3] | 0 / 0 |
| `uniformity/uniformity.csv/0.500/gain_sigma` | 64 | 0.2 | 0.2 ± 0 | 0.2 [0.2, 0.2] | 0 / 0 |
| `uniformity/uniformity.csv/0.500/rms_corrected_mm` | 64 | 0.463 | 0.47237 ± 0.023201 | 0.47 [0.45875, 0.489] | 0.018029 / 0.027364 |
| `uniformity/uniformity.csv/0.500/rms_raw_mm` | 64 | 0.523 | 0.50487 ± 0.024639 | 0.5045 [0.485, 0.523] | 0.020645 / 0.027641 |
| `uniformity/uniformity.csv/0.750/fail_corrected` | 64 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `uniformity/uniformity.csv/0.750/fail_raw` | 64 | 0 | 0.000125 ± 0.001 | 0 [0, 0] | 0.0014142 / 0 |
| `uniformity/uniformity.csv/0.750/gain_gradient` | 64 | 0.45 | 0.45 ± 0 | 0.45 [0.45, 0.45] | 0 / 0 |
| `uniformity/uniformity.csv/0.750/gain_sigma` | 64 | 0.3 | 0.3 ± 0 | 0.3 [0.3, 0.3] | 0 / 0 |
| `uniformity/uniformity.csv/0.750/rms_corrected_mm` | 64 | 0.494 | 0.4718 ± 0.021618 | 0.472 [0.45675, 0.49] | 0.021726 / 0.02145 |
| `uniformity/uniformity.csv/0.750/rms_raw_mm` | 64 | 0.527 | 0.52044 ± 0.073055 | 0.5145 [0.48775, 0.532] | 0.098286 / 0.02955 |
| `uniformity/uniformity.csv/1.000/fail_corrected` | 64 | 0 | 0.000125 ± 0.001 | 0 [0, 0] | 0.0014142 / 0 |
| `uniformity/uniformity.csv/1.000/fail_raw` | 64 | 0 | 0.000125 ± 0.001 | 0 [0, 0] | 0.0014142 / 0 |
| `uniformity/uniformity.csv/1.000/gain_gradient` | 64 | 0.6 | 0.6 ± 0 | 0.6 [0.6, 0.6] | 0 / 0 |
| `uniformity/uniformity.csv/1.000/gain_sigma` | 64 | 0.4 | 0.4 ± 0 | 0.4 [0.4, 0.4] | 0 / 0 |
| `uniformity/uniformity.csv/1.000/rms_corrected_mm` | 64 | 0.502 | 0.49938 ± 0.065685 | 0.497 [0.476, 0.5115] | 0.08979 / 0.025449 |
| `uniformity/uniformity.csv/1.000/rms_raw_mm` | 64 | 0.577 | 0.54898 ± 0.06712 | 0.5415 [0.52375, 0.562] | 0.090934 / 0.023932 |
| `uniformity/uniformity.csv/1.500/fail_corrected` | 64 | 0 | 0.007125 ± 0.0073366 | 0.008 [0, 0.01025] | 0.0070103 / 0.0077605 |
| `uniformity/uniformity.csv/1.500/fail_raw` | 64 | 0 | 0.0005 ± 0.0019518 | 0 [0, 0] | 0.0019675 / 0.0019675 |
| `uniformity/uniformity.csv/1.500/gain_gradient` | 64 | 0.9 | 0.9 ± 0 | 0.9 [0.9, 0.9] | 0 / 0 |
| `uniformity/uniformity.csv/1.500/gain_sigma` | 64 | 0.6 | 0.6 ± 0 | 0.6 [0.6, 0.6] | 0 / 0 |
| `uniformity/uniformity.csv/1.500/rms_corrected_mm` | 64 | 0.591 | 0.90227 ± 0.31114 | 0.9155 [0.589, 1.1772] | 0.30866 / 0.31627 |
| `uniformity/uniformity.csv/1.500/rms_raw_mm` | 64 | 0.597 | 0.64433 ± 0.10694 | 0.621 [0.6015, 0.64325] | 0.10543 / 0.11009 |
| `uniformity/uniformity.csv/2.000/fail_corrected` | 64 | 0.008 | 0.021859 ± 0.013508 | 0.021 [0.008, 0.033] | 0.014235 / 0.012945 |
| `uniformity/uniformity.csv/2.000/fail_raw` | 64 | 0 | 0.000375 ± 0.0017043 | 0 [0, 0] | 0.0019675 / 0.0014142 |
| `uniformity/uniformity.csv/2.000/gain_gradient` | 64 | 1.2 | 1.2 ± 0 | 1.2 [1.2, 1.2] | 0 / 0 |
| `uniformity/uniformity.csv/2.000/gain_sigma` | 64 | 0.8 | 0.8 ± 0 | 0.8 [0.8, 0.8] | 0 / 0 |
| `uniformity/uniformity.csv/2.000/rms_corrected_mm` | 64 | 1.06 | 1.3539 ± 0.39362 | 1.3685 [1.0615, 1.633] | 0.42878 / 0.36138 |
| `uniformity/uniformity.csv/2.000/rms_raw_mm` | 64 | 0.755 | 0.76684 ± 0.094974 | 0.754 [0.73375, 0.771] | 0.10675 / 0.082813 |
| `viewer/current/300/0/all/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/0/all/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/0/all/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/300/0/all/bias` | 32 | 50.737 | 48.972 ± 9.9859 | 50.737 [50.737, 50.737] | 14.122 / 0 |
| `viewer/current/300/0/window/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/0/window/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/0/window/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/300/0/window/bias` | 32 | -5.7512 | 4.2064 ± 12.98 | 4.0424 [-5.7512, 4.0424] | 13.532 / 12.779 |
| `viewer/current/300/15/all/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/15/all/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/15/all/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/300/15/all/bias` | 32 | 50.737 | 50.737 ± 0 | 50.737 [50.737, 50.737] | 0 / 0 |
| `viewer/current/300/15/window/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/15/window/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/15/window/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/300/15/window/bias` | 32 | 50.737 | 48.972 ± 9.9859 | 50.737 [50.737, 50.737] | 0 / 14.122 |
| `viewer/current/300/30/all/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/30/all/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/30/all/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/300/30/all/bias` | 32 | -23.56 | -23.004 ± 3.1482 | -23.56 [-23.56, -23.56] | 4.4522 / 0 |
| `viewer/current/300/30/window/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/30/window/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/300/30/window/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/300/30/window/bias` | 32 | -23.56 | -27.473 ± 7.8536 | -31.68 [-31.68, -23.56] | 8.8944 / 6.9538 |
| `viewer/current/500/0/all/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/0/all/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/0/all/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/500/0/all/bias` | 32 | 69.334 | 69.334 ± 0 | 69.334 [69.334, 69.334] | 0 / 0 |
| `viewer/current/500/0/window/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/0/window/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/0/window/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/500/0/window/bias` | 32 | 69.334 | 27.912 ± 57.197 | 69.334 [-44.278, 69.334] | 58.208 / 56.803 |
| `viewer/current/500/15/all/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/15/all/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/15/all/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/500/15/all/bias` | 32 | -44.278 | -44.278 ± 0 | -44.278 [-44.278, -44.278] | 0 / 0 |
| `viewer/current/500/15/window/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/15/window/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/15/window/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/500/15/window/bias` | 32 | -44.278 | -44.278 ± 0 | -44.278 [-44.278, -44.278] | 0 / 0 |
| `viewer/current/500/30/all/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/30/all/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/30/all/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/500/30/all/bias` | 32 | -44.278 | -42.782 ± 5.8849 | -44.278 [-44.278, -44.278] | 5.9822 / 5.9822 |
| `viewer/current/500/30/window/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/30/window/FarCensored` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/500/30/window/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/500/30/window/bias` | 32 | -44.278 | -36.537 ± 16.582 | -44.278 [-44.278, -38.295] | 8.1732 / 21.292 |
| `viewer/current/700/0/all/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/700/0/all/FarCensored` | 32 | 0 | 0.84375 ± 0.3689 | 1 [1, 1] | 0.44721 / 0.25 |
| `viewer/current/700/0/all/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/700/0/all/bias` | 32 | -539.86 | -86.66 ± 177.67 | -49.593 [-49.593, 0.26525] | 206.6 / 138.78 |
| `viewer/current/700/0/window/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/700/0/window/FarCensored` | 32 | 1 | 0.8125 ± 0.39656 | 1 [1, 1] | 0.47871 / 0.25 |
| `viewer/current/700/0/window/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/700/0/window/bias` | 32 | -49.593 | -68.184 ± 132.45 | -49.593 [-49.593, -49.593] | 176.29 / 48.669 |
| `viewer/current/700/15/all/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/700/15/all/FarCensored` | 32 | 1 | 0.84375 ± 0.3689 | 1 [1, 1] | 0.25 / 0.44721 |
| `viewer/current/700/15/all/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/700/15/all/bias` | 32 | -130.67 | -169.03 ± 121.18 | -130.67 [-130.67, -130.67] | 102.3 / 139.77 |
| `viewer/current/700/15/window/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/700/15/window/FarCensored` | 32 | 1 | 0.625 ± 0.49187 | 1 [0, 1] | 0.51235 / 0.47871 |
| `viewer/current/700/15/window/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/700/15/window/bias` | 32 | -130.67 | -140.83 ± 73.699 | -130.67 [-130.67, -130.67] | 0 / 104.9 |
| `viewer/current/700/30/all/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/700/30/all/FarCensored` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/700/30/all/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/700/30/all/bias` | 32 | -130.67 | -99.357 ± 67.423 | -130.67 [-130.67, -110.4] | 68.537 / 68.537 |
| `viewer/current/700/30/window/BoundaryMaximum` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/current/700/30/window/FarCensored` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/700/30/window/MultipleModes` | 32 | 1 | 1 ± 0 | 1 [1, 1] | 0 / 0 |
| `viewer/current/700/30/window/bias` | 32 | -130.67 | -93.897 ± 91.378 | -130.67 [-164.07, -49.593] | 78.169 / 100.49 |
| `viewer/manual/300/0/all/bias` | 32 | 60 | 56.875 ± 12.297 | 60 [60, 60] | 0 / 17.078 |
| `viewer/manual/300/0/all/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/300/0/window/bias` | 32 | 10 | 10 ± 0 | 10 [10, 10] | 0 / 0 |
| `viewer/manual/300/0/window/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/300/15/all/bias` | 32 | 60 | 60 ± 0 | 60 [60, 60] | 0 / 0 |
| `viewer/manual/300/15/all/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/300/15/window/bias` | 32 | 50 | 54.062 ± 14.78 | 60 [60, 60] | 12.633 / 16.931 |
| `viewer/manual/300/15/window/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/300/30/all/bias` | 32 | -20 | -20 ± 0 | -20 [-20, -20] | 0 / 0 |
| `viewer/manual/300/30/all/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/300/30/window/bias` | 32 | -20 | -21.25 ± 3.3601 | -20 [-20, -20] | 4.0311 / 2.5 |
| `viewer/manual/300/30/window/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/500/0/all/bias` | 32 | 80 | 80 ± 0 | 80 [80, 80] | 0 / 0 |
| `viewer/manual/500/0/all/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/500/0/window/bias` | 32 | 80 | 59.375 ± 47.853 | 80 [80, 80] | 49.46 / 47.815 |
| `viewer/manual/500/0/window/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/500/15/all/bias` | 32 | -30 | -10.625 ± 38.01 | -30 [-30, -30] | 36.28 / 40.697 |
| `viewer/manual/500/15/all/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/500/15/window/bias` | 32 | -40 | -31.875 ± 17.678 | -30 [-40, -30] | 6.2915 / 24.35 |
| `viewer/manual/500/15/window/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/500/30/all/bias` | 32 | -40 | -40 ± 0 | -40 [-40, -40] | 0 / 0 |
| `viewer/manual/500/30/all/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/500/30/window/bias` | 32 | -40 | -39.375 ± 2.4593 | -40 [-40, -40] | 2.5 / 2.5 |
| `viewer/manual/500/30/window/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/700/0/all/bias` | 32 | -20 | -37.812 ± 92.064 | -20 [-20, -20] | 129.77 / 0 |
| `viewer/manual/700/0/all/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/700/0/window/bias` | 32 | -20 | -47.5 ± 137.63 | -20 [-20, -20] | 188.54 / 36.697 |
| `viewer/manual/700/0/window/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/700/15/all/bias` | 32 | -120 | -156.88 ± 100.98 | -140 [-140, -120] | 101.72 / 103.46 |
| `viewer/manual/700/15/all/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/700/15/window/bias` | 32 | -120 | -125.94 ± 13.88 | -120 [-140, -120] | 10.247 / 16.621 |
| `viewer/manual/700/15/window/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/700/30/all/bias` | 32 | -140 | -140 ± 0 | -140 [-140, -140] | 0 / 0 |
| `viewer/manual/700/30/all/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |
| `viewer/manual/700/30/window/bias` | 32 | -140 | -116.25 ± 75.552 | -140 [-140, -140] | 62.5 / 88.032 |
| `viewer/manual/700/30/window/edge` | 32 | 0 | 0 ± 0 | 0 [0, 0] | 0 / 0 |

### Discrete results (frequencies, not average thickness or angle)

| Metric | Pick : occurrences / N |
|---|---|
| `array/16_better_than12` | 0.0: 14/128, 1.0: 114/128 |
| `dose/frontal_within13` | 0.0: 1/64, 1.0: 63/64 |
| `fov/1000/0/500/0.00/loc_cyclic` | 3.0: 64/64 |
| `fov/1000/0/500/0.00/loc_noncyclic` | 6.0: 52/64, 6.5: 8/64, 7.0: 4/64 |
| `fov/1000/0/500/0.00/outside_centroid_any` | 1.0: 64/64 |
| `fov/1000/0/500/1.00/loc_cyclic` | 3.0: 64/64 |
| `fov/1000/0/500/1.00/loc_noncyclic` | 4.0: 6/64, 4.5: 2/64, 5.0: 56/64 |
| `fov/1000/0/500/1.00/outside_centroid_any` | 0.0: 64/64 |
| `fov/1000/0/5000/0.00/loc_cyclic` | 3.0: 64/64 |
| `fov/1000/0/5000/0.00/loc_noncyclic` | 7.0: 64/64 |
| `fov/1000/0/5000/0.00/outside_centroid_any` | 1.0: 64/64 |
| `fov/1000/0/5000/1.00/loc_cyclic` | 3.0: 64/64 |
| `fov/1000/0/5000/1.00/loc_noncyclic` | 6.0: 64/64 |
| `fov/1000/0/5000/1.00/outside_centroid_any` | 1.0: 64/64 |
| `fov/1000/45/500/0.00/loc_cyclic` | 4.5: 64/64 |
| `fov/1000/45/500/0.00/loc_noncyclic` | 3.0: 50/64, 4.5: 9/64, 6.0: 5/64 |
| `fov/1000/45/500/1.00/loc_cyclic` | 2.0: 24/64, 2.5: 40/64 |
| `fov/1000/45/500/1.00/loc_noncyclic` | 0.0: 45/64, 0.5: 17/64, 1.5: 2/64 |
| `fov/1000/45/5000/0.00/loc_cyclic` | 4.5: 64/64 |
| `fov/1000/45/5000/0.00/loc_noncyclic` | 9.0: 64/64 |
| `fov/1000/45/5000/1.00/loc_cyclic` | 3.0: 64/64 |
| `fov/1000/45/5000/1.00/loc_noncyclic` | 3.0: 64/64 |
| `fov/5000/0/500/0.00/loc_cyclic` | 3.0: 16/64, 3.5: 48/64 |
| `fov/5000/0/500/0.00/loc_noncyclic` | 7.0: 64/64 |
| `fov/5000/0/500/1.00/loc_cyclic` | 3.0: 64/64 |
| `fov/5000/0/500/1.00/loc_noncyclic` | 5.5: 1/64, 6.0: 9/64, 6.5: 54/64 |
| `fov/5000/0/5000/0.00/loc_cyclic` | 3.5: 64/64 |
| `fov/5000/0/5000/0.00/loc_noncyclic` | 7.5: 64/64 |
| `fov/5000/0/5000/1.00/loc_cyclic` | 3.5: 64/64 |
| `fov/5000/0/5000/1.00/loc_noncyclic` | 6.5: 64/64 |
| `fov/5000/45/500/0.00/loc_cyclic` | 4.5: 64/64 |
| `fov/5000/45/500/0.00/loc_noncyclic` | 3.0: 55/64, 4.5: 9/64 |
| `fov/5000/45/500/1.00/loc_cyclic` | 0.5: 31/64, 2.0: 32/64, 3.0: 1/64 |
| `fov/5000/45/500/1.00/loc_noncyclic` | 0.5: 62/64, 1.5: 2/64 |
| `fov/5000/45/5000/0.00/loc_cyclic` | 4.5: 24/64, 5.0: 40/64 |
| `fov/5000/45/5000/0.00/loc_noncyclic` | 4.5: 24/64, 7.5: 40/64 |
| `fov/5000/45/5000/1.00/loc_cyclic` | 4.5: 64/64 |
| `fov/5000/45/5000/1.00/loc_noncyclic` | 1.5: 1/64, 2.0: 62/64, 3.0: 1/64 |
| `maskfab/within2/10` | 0.0: 5/128, 1.0: 123/128 |
| `maskfab/within2/160` | 0.0: 112/128, 1.0: 16/128 |
| `maskfab/within2/20` | 0.0: 9/128, 1.0: 119/128 |
| `maskfab/within2/40` | 0.0: 20/128, 1.0: 108/128 |
| `maskfab/within2/80` | 0.0: 28/128, 1.0: 100/128 |
| `mlem/mlem.csv/1.50/cross_resolved` | 0.0: 64/64 |
| `mlem/mlem.csv/1.50/mlem_resolved` | 1.0: 64/64 |
| `mlem/mlem.csv/2.00/cross_resolved` | 0.0: 64/64 |
| `mlem/mlem.csv/2.00/mlem_resolved` | 1.0: 64/64 |
| `mlem/mlem.csv/3.00/cross_resolved` | 0.0: 64/64 |
| `mlem/mlem.csv/3.00/mlem_resolved` | 1.0: 64/64 |
| `mlem/mlem.csv/3.50/cross_resolved` | 1.0: 64/64 |
| `mlem/mlem.csv/3.50/mlem_resolved` | 1.0: 64/64 |
| `noise/submm/100` | 0.0: 29/128, 1.0: 99/128 |
| `noise/submm/250` | 1.0: 128/128 |
| `noise/submm/50` | 0.0: 128/128 |
| `rtl_frontend/cusp_beats_crrc` | 1.0: 32/32 |
| `scan/r11/pass90` | 0.0: 1/32, 1.0: 31/32 |
| `shield/directional_delta` | -2.0: 14/128, 0.0: 88/128, 2.0: 26/128 |
| `shield/knee/co` | 25.0: 83/128, 30.0: 45/128 |
| `shield/knee/cs` | 20.0: 100/128, 25.0: 28/128 |
| `shield/knee/dir` | 8.0: 97/128, 10.0: 31/128 |
| `shield/knee/scattered` | 8.0: 109/128, 10.0: 19/128 |
| `spatial/co2_cs_submm` | 0.0: 2/128, 1.0: 126/128 |
| `thickness/best` | 6.0: 64/64 |
| `thickness/maxradius_member/10.0` | 1.0: 64/64 |
| `thickness/maxradius_member/14.0` | 1.0: 64/64 |
| `thickness/maxradius_member/18.0` | 1.0: 64/64 |
| `thickness/maxradius_member/2.0` | 0.0: 64/64 |
| `thickness/maxradius_member/24.0` | 1.0: 64/64 |
| `thickness/maxradius_member/32.0` | 1.0: 64/64 |
| `thickness/maxradius_member/4.0` | 0.0: 64/64 |
| `thickness/maxradius_member/6.0` | 1.0: 64/64 |
| `thickness/maxradius_member/8.0` | 1.0: 64/64 |
| `thickness_wide/best` | 8.0: 1/32, 10.0: 21/32, 14.0: 10/32 |
| `viewer/current/300/0/all/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/300/0/all/FarCensored` | 0.0: 32/32 |
| `viewer/current/300/0/all/MultipleModes` | 1.0: 32/32 |
| `viewer/current/300/0/window/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/300/0/window/FarCensored` | 0.0: 32/32 |
| `viewer/current/300/0/window/MultipleModes` | 1.0: 32/32 |
| `viewer/current/300/15/all/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/300/15/all/FarCensored` | 0.0: 32/32 |
| `viewer/current/300/15/all/MultipleModes` | 1.0: 32/32 |
| `viewer/current/300/15/window/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/300/15/window/FarCensored` | 0.0: 32/32 |
| `viewer/current/300/15/window/MultipleModes` | 1.0: 32/32 |
| `viewer/current/300/30/all/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/300/30/all/FarCensored` | 0.0: 32/32 |
| `viewer/current/300/30/all/MultipleModes` | 1.0: 32/32 |
| `viewer/current/300/30/window/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/300/30/window/FarCensored` | 0.0: 32/32 |
| `viewer/current/300/30/window/MultipleModes` | 1.0: 32/32 |
| `viewer/current/500/0/all/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/500/0/all/FarCensored` | 0.0: 32/32 |
| `viewer/current/500/0/all/MultipleModes` | 1.0: 32/32 |
| `viewer/current/500/0/window/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/500/0/window/FarCensored` | 0.0: 32/32 |
| `viewer/current/500/0/window/MultipleModes` | 1.0: 32/32 |
| `viewer/current/500/15/all/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/500/15/all/FarCensored` | 0.0: 32/32 |
| `viewer/current/500/15/all/MultipleModes` | 1.0: 32/32 |
| `viewer/current/500/15/window/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/500/15/window/FarCensored` | 0.0: 32/32 |
| `viewer/current/500/15/window/MultipleModes` | 1.0: 32/32 |
| `viewer/current/500/30/all/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/500/30/all/FarCensored` | 0.0: 32/32 |
| `viewer/current/500/30/all/MultipleModes` | 1.0: 32/32 |
| `viewer/current/500/30/window/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/500/30/window/FarCensored` | 0.0: 32/32 |
| `viewer/current/500/30/window/MultipleModes` | 1.0: 32/32 |
| `viewer/current/700/0/all/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/700/0/all/FarCensored` | 0.0: 5/32, 1.0: 27/32 |
| `viewer/current/700/0/all/MultipleModes` | 1.0: 32/32 |
| `viewer/current/700/0/window/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/700/0/window/FarCensored` | 0.0: 6/32, 1.0: 26/32 |
| `viewer/current/700/0/window/MultipleModes` | 1.0: 32/32 |
| `viewer/current/700/15/all/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/700/15/all/FarCensored` | 0.0: 5/32, 1.0: 27/32 |
| `viewer/current/700/15/all/MultipleModes` | 1.0: 32/32 |
| `viewer/current/700/15/window/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/700/15/window/FarCensored` | 0.0: 12/32, 1.0: 20/32 |
| `viewer/current/700/15/window/MultipleModes` | 1.0: 32/32 |
| `viewer/current/700/30/all/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/700/30/all/FarCensored` | 1.0: 32/32 |
| `viewer/current/700/30/all/MultipleModes` | 1.0: 32/32 |
| `viewer/current/700/30/window/BoundaryMaximum` | 0.0: 32/32 |
| `viewer/current/700/30/window/FarCensored` | 1.0: 32/32 |
| `viewer/current/700/30/window/MultipleModes` | 1.0: 32/32 |
| `viewer/manual/300/0/all/edge` | 0.0: 32/32 |
| `viewer/manual/300/0/window/edge` | 0.0: 32/32 |
| `viewer/manual/300/15/all/edge` | 0.0: 32/32 |
| `viewer/manual/300/15/window/edge` | 0.0: 32/32 |
| `viewer/manual/300/30/all/edge` | 0.0: 32/32 |
| `viewer/manual/300/30/window/edge` | 0.0: 32/32 |
| `viewer/manual/500/0/all/edge` | 0.0: 32/32 |
| `viewer/manual/500/0/window/edge` | 0.0: 32/32 |
| `viewer/manual/500/15/all/edge` | 0.0: 32/32 |
| `viewer/manual/500/15/window/edge` | 0.0: 32/32 |
| `viewer/manual/500/30/all/edge` | 0.0: 32/32 |
| `viewer/manual/500/30/window/edge` | 0.0: 32/32 |
| `viewer/manual/700/0/all/edge` | 0.0: 32/32 |
| `viewer/manual/700/0/window/edge` | 0.0: 32/32 |
| `viewer/manual/700/15/all/edge` | 0.0: 32/32 |
| `viewer/manual/700/15/window/edge` | 0.0: 32/32 |
| `viewer/manual/700/30/all/edge` | 0.0: 32/32 |
| `viewer/manual/700/30/window/edge` | 0.0: 32/32 |


## Reproduction cost and implementation proposal

Measured wall times below are per outer run under concurrent load, not uncontended CPU benchmarks or guaranteed future runtime. Cache and native timings describe different executables. Summing job wall times gives serial occupied-run hours, not elapsed review hours. Full global scan and arbitrary-pattern populations cost more than the selected candidate/pattern studies here.

| Temporary route / command | Successful timing records (including duplicate/pilot work) | Median seconds per outer run | Sum run hours |
|---|---:|---:|---:|
| `fast/runs/align` | 50 | 35.631 | 0.54604 |
| `fast/runs/antimask` | 113 | 25.49 | 0.74446 |
| `fast/runs/antimask-scene` | 49 | 12.166 | 0.1579 |
| `fast/runs/array` | 113 | 34.343 | 1.0767 |
| `fast/runs/background` | 49 | 20.33 | 0.24119 |
| `fast/runs/cascade` | 114 | 43.632 | 1.5092 |
| `fast/runs/compton` | 49 | 34.023 | 0.42941 |
| `fast/runs/compton-strip` | 50 | 160.86 | 2.3296 |
| `fast/runs/deadtime` | 50 | 44.595 | 0.56204 |
| `fast/runs/defects` | 50 | 12.507 | 0.19432 |
| `fast/runs/depth` | 50 | 11.915 | 0.19082 |
| `fast/runs/depth-joint` | 50 | 40.358 | 0.77012 |
| `fast/runs/depth3d` | 51 | 364.23 | 5.8589 |
| `fast/runs/depthdesign` | 50 | 25.477 | 0.39043 |
| `fast/runs/doi` | 49 | 11.981 | 0.18143 |
| `fast/runs/dose` | 50 | 18.945 | 0.24955 |
| `fast/runs/frontend` | 49 | 0.80511 | 0.011734 |
| `fast/runs/ghost` | 49 | 4.1005 | 0.056596 |
| `fast/runs/head` | 49 | 4.9732 | 0.06914 |
| `fast/runs/headir` | 49 | 5.023 | 0.066222 |
| `fast/runs/ir` | 49 | 3.5597 | 0.050162 |
| `fast/runs/maskfab` | 114 | 77.755 | 2.7918 |
| `fast/runs/masktaper` | 49 | 42.28 | 0.79215 |
| `fast/runs/mat_BGO` | 49 | 3.0434 | 0.038615 |
| `fast/runs/mat_CeBr3` | 49 | 2.9577 | 0.039458 |
| `fast/runs/mat_GAGG` | 49 | 2.9664 | 0.03958 |
| `fast/runs/mat_GAGG_Mg` | 49 | 2.935 | 0.037756 |
| `fast/runs/mat_LYSO` | 49 | 3.0108 | 0.040822 |
| `fast/runs/mat_LaBr3` | 49 | 2.9181 | 0.038081 |
| `fast/runs/mat_NaI` | 49 | 3.1683 | 0.038881 |
| `fast/runs/mixedfield` | 49 | 10.801 | 0.13453 |
| `fast/runs/mixediso` | 49 | 16.654 | 0.23656 |
| `fast/runs/mixedstrip` | 49 | 80.793 | 1.0692 |
| `fast/runs/mlem` | 49 | 12.658 | 0.16031 |
| `fast/runs/noise` | 113 | 31.882 | 0.87303 |
| `fast/runs/noise_head` | 51 | 23.305 | 0.38945 |
| `fast/runs/noise_orig` | 51 | 27.363 | 0.373 |
| `fast/runs/nonprop` | 50 | 10.917 | 0.15276 |
| `fast/runs/off` | 49 | 3.9949 | 0.051745 |
| `fast/runs/orig` | 49 | 5.0099 | 0.062906 |
| `fast/runs/shield` | 115 | 71.687 | 2.3059 |
| `fast/runs/single` | 49 | 4.2038 | 0.059892 |
| `fast/runs/subcell` | 50 | 212.55 | 3.3739 |
| `fast/runs/sweep` | 49 | 166.87 | 2.1135 |
| `fast/runs/sweep_head` | 51 | 173.21 | 2.3994 |
| `fast/runs/thermal` | 50 | 12.859 | 0.17373 |
| `fast/runs/thickness` | 50 | 198.85 | 2.9465 |
| `fast/runs/thickness_wide` | 32 | 217.84 | 1.9746 |
| `fast/runs/uniformity` | 49 | 21.875 | 0.32583 |
| `fast/special/bias` | 11 | 123.34 | 0.38313 |
| `fast/special/depthsharp` | 12 | 64.29 | 0.20335 |
| `fast/special/fov` | 64 | 697.55 | 12.886 |
| `fast/special/gap` | 11 | 45.415 | 0.14725 |
| `fast/special/precise` | 11 | 19.983 | 0.064935 |
| `fast/special/scan` | 11 | 60.272 | 0.17469 |
| `fast/special/spatial` | 12 | 61.262 | 0.20815 |
| `materials/materials` | 64 | 17.683 | 0.29025 |
| `rtl-runs/frontend` | 32 | 38.917 | 0.37535 |
| `rtl-runs/material` | 128 | 1.8691 | 0.095883 |
| `rtl-runs/multi` | 32 | 73.752 | 0.76766 |
| `rtl-runs/peak` | 32 | 3.0632 | 0.031488 |
| `rtl-runs/pixel` | 32 | 173.39 | 1.9091 |
| `runs/align` | 23 | 80.97 | 1.0105 |
| `runs/antimask` | 23 | 116.21 | 1.2479 |
| `runs/antimask-scene` | 24 | 13.685 | 0.14464 |
| `runs/array` | 24 | 55.893 | 0.78593 |
| `runs/background` | 24 | 97.434 | 1.0501 |
| `runs/cascade` | 22 | 58.855 | 0.65107 |
| `runs/compton` | 23 | 44.443 | 0.40796 |
| `runs/compton-strip` | 22 | 226.12 | 2.8464 |
| `runs/deadtime` | 23 | 44.616 | 0.62491 |
| `runs/defects` | 23 | 82.593 | 1.062 |
| `runs/depth` | 23 | 14.235 | 0.20351 |
| `runs/depth-joint` | 22 | 75.35 | 0.97268 |
| `runs/depth3d` | 20 | 781.52 | 6.3399 |
| `runs/depthdesign` | 22 | 33.52 | 0.38961 |
| `runs/doi` | 23 | 16.503 | 0.18927 |
| `runs/dose` | 23 | 21.843 | 0.26062 |
| `runs/frontend` | 23 | 1.3488 | 0.031488 |
| `runs/ghost` | 24 | 4.5289 | 0.11052 |
| `runs/head` | 24 | 5.1152 | 0.13791 |
| `runs/headir` | 24 | 5.6307 | 0.12088 |
| `runs/ir` | 24 | 3.9459 | 0.15477 |
| `runs/maskfab` | 22 | 142.34 | 1.9129 |
| `runs/masktaper` | 24 | 122.19 | 1.4368 |
| `runs/mat_BGO` | 24 | 3.1064 | 0.084496 |
| `runs/mat_CeBr3` | 24 | 3.4722 | 0.088918 |
| `runs/mat_GAGG` | 24 | 3.1878 | 0.08651 |
| `runs/mat_GAGG_Mg` | 24 | 3.1672 | 0.089414 |
| `runs/mat_LYSO` | 24 | 3.292 | 0.076577 |
| `runs/mat_LaBr3` | 24 | 3.4527 | 0.085285 |
| `runs/mat_NaI` | 24 | 3.6866 | 0.086181 |
| `runs/mixedfield` | 24 | 9.3442 | 0.15869 |
| `runs/mixediso` | 24 | 16.753 | 0.2449 |
| `runs/mixedstrip` | 24 | 84.257 | 1.0511 |
| `runs/mlem` | 24 | 12.934 | 0.16649 |
| `runs/noise` | 24 | 120.83 | 1.835 |
| `runs/noise_head` | 22 | 186.1 | 2.5813 |
| `runs/noise_orig` | 22 | 120.06 | 1.9113 |
| `runs/nonprop` | 22 | 12.867 | 0.15532 |
| `runs/off` | 24 | 4.5481 | 0.10534 |
| `runs/orig` | 24 | 5.5315 | 0.13588 |
| `runs/shield` | 21 | 653.02 | 6.4764 |
| `runs/single` | 24 | 3.877 | 0.10936 |
| `runs/subcell` | 22 | 396.1 | 3.479 |
| `runs/sweep` | 23 | 177.86 | 1.7652 |
| `runs/sweep_head` | 22 | 211.73 | 1.8481 |
| `runs/thermal` | 23 | 40.922 | 0.53094 |
| `runs/thickness` | 23 | 433.53 | 4.2308 |
| `runs/uniformity` | 24 | 52.85 | 0.83577 |
| `special/bias` | 26 | 153.7 | 1.7966 |
| `special/depthsharp` | 26 | 71.838 | 0.9775 |
| `special/fov` | 19 | 1382.5 | 13.966 |
| `special/gap` | 26 | 65.328 | 0.70008 |
| `special/precise` | 26 | 22.893 | 0.34175 |
| `special/scan` | 26 | 48.718 | 0.71037 |
| `special/spatial` | 26 | 63.806 | 0.84629 |
| `v2/scanextra` | 32 | 319.59 | 2.7673 |
| `v2/spatial` | 128 | 68.659 | 2.4494 |
| `viewer-current/viewer-current` | 33 | 144.2 | 1.5857 |

Proposed work: one manifest driver + strict CSV/stdout parsers/aggregate JSON, then annotate Evidence/README/Findings/PAPER/plots from that aggregate. A CLI seed flag is optional convenience; independent cwd remains necessary. Add headless helper recipes for precise weighted transport, selected-scan validation, bias, Sharp-depth/viewer and gap; use complete config cloning. Python scripts need seed/no-plot/data-output options while retaining their generator and fixed fixtures. RTL pixel measured the quoted0/.15 endpoints; reproducing its whole five-σ figure multiplies endpoint work by about2.5, with the same recipe. NumPy material/peak/multi figures likewise need all rate/gain rows if the whole image is renewed, rather than just its quoted endpoints.

The costs of document/parser implementation are estimates pending the planner's chosen scope, not measured execution claims. Splitting the work into driver/schema, helper provenance, and quote/annotation synchronisation makes each reviewable. No new CI requirement or production numerical tolerance is proposed. Store final seed lists/aggregates/config hashes in the repository at implementation time; TEMP outputs alone are not permanent published evidence.

## Audit, verification and remaining limits

Reproduce inputs/raw CSV/stdout/stderr, per-run exit/timing records, seed manifests, scripts and comparison results remain under `%TEMP%/gcam-evidence-refresh-20261002/`. No files were written in `.git`, the other worktree, Windows settings, credential stores or another user's directories. No installation, deletion, git state mutation, process stop, network upload or GUI/UI test was performed. Native worker priorities were changed to BelowNormal to allow completed equivalent probes to progress; this is transient/reversible, with prior values in `native-priority-before.json`.

Command-log caveats: an initial wrong CLI DLL path, Python console/relative-path pilots, and the old tuple-serialisation spatial probe failed and were excluded. A mistaken `run-viewer-current.ps1 -N0` invoked PowerShell's0..−1 range and ran two TEMP probes: repeated12345 (identical result) and an extra last-O seed. The driver now rejectsN≤0; aggregation includes only the declared first32. No failed/extra probe was turned into a favourable evidence sample. Decoder equivalence is reported separately from source-engine verification.

Not run: full solution tests/headless rendering/UI tests (no source change); frozen RTL-vector regeneration/cocotb inventory/Fmax resynthesis; a full global scan over every rank/pitch/distance across64 seeds; complete multi-seed plot grids for every unquoted stimulus rate/gain; a new random-manufacturing-pattern population; missing historical gap/wide-field recipes. Those limits prevent claims of global optimality, all-device tolerance, or an exact renewal of unknown historical experiments. The retained deterministic/analytic evidence is classified, not falsely assigned a sample SD.

At final read-only git status, unrelated changes also appeared in CLAUDE.md, PLAN.Physics.RigReadout.md and its review; they were not edited by this review. HEAD remained55af61591ab21eaa4c90c6dac74f91c14af8f765.

The original unaccelerated drivers still have redundant work queued; their native completed results are retained and excluded as duplicate seeds when equivalent cached results exist. They were not stopped because the author's hard limit2 requires approval. The command below is prepared for a subsequent turn; it guards both parent identity and owned TEMP executable paths and does not touch unrelated workers.

## APPROVAL REQUESTS

Exact command (not executed):

```powershell
& "$env:TEMP\gcam-evidence-refresh-20261002\stop-redundant.ps1"
```

Reason: stop this review's redundant original native drivers/children after all declared measurements are complete. Destruction: only in-flight duplicate computation is lost; completed results and every file remain. Undo: processes cannot be resumed; rerun the preserved `run.ps1` / `special.ps1` commands if native repetition is wanted. Required by the author's explicit prohibition on stopping processes without approval, not by an inferred skill rule. Script contents are below for audit.

```powershell
$ownedRoot = Join-Path $env:TEMP 'gcam-evidence-refresh-20261002'
$driverIds = @(46084, 62580, 63304)
foreach ($driverId in $driverIds) {
    $candidate = Get-CimInstance Win32_Process -Filter "ProcessId=$driverId"
    if ($candidate.Name -eq 'pwsh.exe' -and
        $candidate.CommandLine -like '*gcam-evidence-refresh-20261002*' -and
        ($candidate.CommandLine -like '*run.ps1*' -or $candidate.CommandLine -like '*special.ps1*')) {
        Stop-Process -Id $driverId
    }
}
$nativeCli = Join-Path $ownedRoot 'artifacts\bin\Gcam.Cli\release\Gcam.Cli.dll'
$nativeProbe = Join-Path $ownedRoot 'probe\bin\Release\net9.0\probe.dll'
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
    Where-Object {
        $_.CommandLine -like "*$nativeCli *" -or
        $_.CommandLine -like "*$nativeProbe *"
    } | ForEach-Object { Stop-Process -Id $_.ProcessId }
```
