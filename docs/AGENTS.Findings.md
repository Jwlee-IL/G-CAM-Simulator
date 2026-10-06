# AGENTS.Findings — simulation results, by theme

The quantitative results GCAM has produced, grouped by theme. Each item lists the
finding, how to reproduce it (CLI command / script), and the artifact it wrote to
`samples/` (or `rtl/`). See [AGENTS.md](../AGENTS.md) for architecture and conventions.

Baseline scenario unless noted: Cs-137, rank-7 MURA (2×2 mosaic), 10 mm tungsten,
12×12 / 1 mm detector, D = 60 mm (mask–detector), S = 100 mm (source–mask).

**Themes cited by the V&V set.** The V&V documents never link here (D-35); they cite the evidence register
`VV.Gcam.Evidence.md`, whose entries restate the result, conditions, limits, reproduce command and tests. When a
theme below changes, update its EV entries in the same commit (`AGENTS.Conventions.Docs`, keeping docs in sync).

| Theme | EV | Theme | EV | Theme | EV |
|---|---|---|---|---|---|
| 1, 2 | EV-01 | 16, 17 | EV-15 | 38 | EV-30 |
| 3 | EV-03 | 18, 19, 24, 34 (depth) | EV-33 | 39 | EV-31 |
| 4 | EV-04 | 22 | EV-09, EV-24 … EV-28 | 40 | EV-32 |
| 5, 28 | EV-12 | 23 | EV-05 | 42 | EV-22 |
| 6 | EV-19 | 25, 30–33 | EV-17 | 43 | EV-08 |
| 7 | EV-06 | 26 | EV-10, EV-15 | 48 | EV-11 |
| 8 | EV-18 | 36 | EV-29 | 50 | EV-13 |
| 9 | EV-07 | 51 | EV-20 | 53 | EV-02 |
| 10 | EV-21 | 52 | EV-09, EV-14, EV-15, EV-19, EV-20; model history | 54 | EV-23 |
| 14 | EV-16 | 15 | EV-14, EV-15 | 63 | every MC entry; model history |
| 64 | EV-34; ambient items of EV-01, -02, -07, -09, -12, -15; Evidence §1; model history | | | | |

## Index

The themes are split over two files; this table is the entry point (one row per theme, heading as written).

| Theme | File |
|---|---|
| 1 | [Geometry & source localization](AGENTS.Findings.Part1.md#1-geometry--source-localization) |
| 2 | [Decoding & ghost artifacts](AGENTS.Findings.Part1.md#2-decoding--ghost-artifacts) |
| 3 | [Configuration optimization (maximise FOV)](AGENTS.Findings.Part1.md#3-configuration-optimization-maximise-fov) |
| 4 | [Mask design — tungsten thickness](AGENTS.Findings.Part1.md#4-mask-design--tungsten-thickness) |
| 5 | [Mask / antimask (two-exposure technique)](AGENTS.Findings.Part1.md#5-mask--antimask-two-exposure-technique) |
| 6 | [Crystal material](AGENTS.Findings.Part1.md#6-crystal-material) |
| 7 | [Detector array (sampling)](AGENTS.Findings.Part1.md#7-detector-array-sampling) |
| 8 | [Crystal uniformity — position robust, energy fragile](AGENTS.Findings.Part1.md#8-crystal-uniformity--position-robust-energy-fragile) |
| 9 | [Statistics & variance reduction](AGENTS.Findings.Part1.md#9-statistics--variance-reduction) |
| 10 | [RTL front-end (energy & rate) — `rtl/`](AGENTS.Findings.Part1.md#10-rtl-front-end-energy--rate--rtl) |
| 11 | [SiPM + ADC front-end characteristics — `rtl/frontend_model.py`](AGENTS.Findings.Part1.md#11-sipm--adc-front-end-characteristics--rtlfrontend_modelpy) |
| 12 | [Front-end energy trust — peak-hold failure modes (Phase A) — `rtl/ballistic_deficit_study.py`](AGENTS.Findings.Part1.md#12-front-end-energy-trust--peak-hold-failure-modes-phase-a--rtlballistic_deficit_studypy) |
| 13 | [Front-end fix — charge integration + pile-up rejection (Phase A2) — `rtl/integrating_peak_detector.sv`](AGENTS.Findings.Part1.md#13-front-end-fix--charge-integration--pile-up-rejection-phase-a2--rtlintegrating_peak_detectorsv) |
| 14 | [Multi-isotope discrimination + ADC dynamic range (Phase B) — `rtl/multi_isotope_study.py`](AGENTS.Findings.Part1.md#14-multi-isotope-discrimination--adc-dynamic-range-phase-b--rtlmulti_isotope_studypy) |
| 15 | [Crystal Compton scattering — positioning strategy + multi-isotope spatial separation — `ComptonModel`/`ComptonCrystalDetector`](AGENTS.Findings.Part1.md#15-crystal-compton-scattering--positioning-strategy--multi-isotope-spatial-separation--comptonmodelcomptoncrystaldetector) |
| 16 | [Compton stripping — the spectral complement to spatial separation — `rtl/compton_stripping_study.py`](AGENTS.Findings.Part1.md#16-compton-stripping--the-spectral-complement-to-spatial-separation--rtlcompton_stripping_studypy) |
| 17 | [Combined lever — per-pixel Compton stripping inside the coded pipeline — `ComptonStudy.RunStripping`](AGENTS.Findings.Part1.md#17-combined-lever--per-pixel-compton-stripping-inside-the-coded-pipeline--comptonstudyrunstripping) |
| 18 | [Source distance (z) estimation by coded-aperture refocusing — `DepthStudy`](AGENTS.Findings.Part1.md#18-source-distance-z-estimation-by-coded-aperture-refocusing--depthstudy) |
| 19 | [Depth under Poisson noise + joint lateral–depth estimation — `DepthStudy.RunNoisyDepth/RunNoisyJoint`](AGENTS.Findings.Part1.md#19-depth-under-poisson-noise--joint-lateraldepth-estimation--depthstudyrunnoisydepthrunnoisyjoint) |
| 20 | [Mask channel geometry — hole size and focused (converging) channels — `MaskGeometryStudy`](AGENTS.Findings.Part1.md#20-mask-channel-geometry--hole-size-and-focused-converging-channels--maskgeometrystudy) |
| 21 | [The "right size" — cell (feature) size and open fraction — `montecarlo masksize`](AGENTS.Findings.Part1.md#21-the-right-size--cell-feature-size-and-open-fraction--montecarlo-masksize) |
| 22 | [Productization — a ~3 kg handheld locator: weight, size, form, and field hardening](AGENTS.Findings.Part1.md#22-productization--a-3-kg-handheld-locator-weight-size-form-and-field-hardening) |
| 23 | [Mask geometry follow-ups — tapered channels (wide FOV) + empirical open fraction](AGENTS.Findings.Part1.md#23-mask-geometry-follow-ups--tapered-channels-wide-fov--empirical-open-fraction) |
| 24 | [Joint (x,y,S) depth — the alternating trap was the SEED, not the alternation — `DepthStudy.RunNoisyJoint3D`](AGENTS.Findings.Part1.md#24-joint-xys-depth--the-alternating-trap-was-the-seed-not-the-alternation--depthstudyrunnoisyjoint3d) |
| 25 | [Trapezoidal shaper in RTL + first cocotb co-sim — `rtl/trapezoidal_shaper.sv`](AGENTS.Findings.Part1.md#25-trapezoidal-shaper-in-rtl--first-cocotb-co-sim--rtltrapezoidal_shapersv) |
| 26 | [Mixed-isotope field imaged in one run — `MixedFieldSource` / `MixedFieldStudy`](AGENTS.Findings.Part1.md#26-mixed-isotope-field-imaged-in-one-run--mixedfieldsource--mixedfieldstudy) |
| 27 | [MC → RTL: drive the cocotb shaper from the Monte Carlo event stream — `EventStreamStudy` / `rtl/event_stream.py`](AGENTS.Findings.Part1.md#27-mc--rtl-drive-the-cocotb-shaper-from-the-monte-carlo-event-stream--eventstreamstudy--rtlevent_streampy) |
| 28 | [Ambient background — a controllable, opt-in noise field — `BackgroundConfig` / `BackgroundStudy`](AGENTS.Findings.Part1.md#28-ambient-background--a-controllable-opt-in-noise-field--backgroundconfig--backgroundstudy) |
| 29 | [RTL baseline restoration — cancel the trapezoidal shaper's pole-zero walk — `rtl/baseline_restorer.sv`](AGENTS.Findings.Part1.md#29-rtl-baseline-restoration--cancel-the-trapezoidal-shapers-pole-zero-walk--rtlbaseline_restorersv) |
| 30 | [Realistic ADC front-end — the shaper stops seeing an idealized signal — `rtl/event_stream.py`](AGENTS.Findings.Part1.md#30-realistic-adc-front-end--the-shaper-stops-seeing-an-idealized-signal--rtlevent_streampy) |
| 31 | [Pulse-shaper comparison — CR-RC vs trapezoidal vs cusp — `rtl/shapers.py` / `rtl/shaper_compare.py`](AGENTS.Findings.Part1.md#31-pulse-shaper-comparison--cr-rc-vs-trapezoidal-vs-cusp--rtlshaperspy--rtlshaper_comparepy) |
| 32 | [Physical front-end folded into the C# pipeline — `FrontEndModel` / `FrontEndConfig`](AGENTS.Findings.Part1.md#32-physical-front-end-folded-into-the-c-pipeline--frontendmodel--frontendconfig) |
| 33 | [CR-RC^4 shaper in RTL — `rtl/crrc_shaper.sv`](AGENTS.Findings.Part1.md#33-cr-rc4-shaper-in-rtl--rtlcrrc_shapersv) |
| 34 | [WPF viewer app + native C# waveform + interactive source localization — `src/Gcam.Wpf`](AGENTS.Findings.Part1.md#34-wpf-viewer-app--native-c-waveform--interactive-source-localization--srcgcamwpf) |
| 35 | [Spectrum & detector realism — make the WPF Cs-137 look like a real measurement, all from physics](AGENTS.Findings.Part1.md#35-spectrum--detector-realism--make-the-wpf-cs-137-look-like-a-real-measurement-all-from-physics) |
| 36 | [Thermal drift DURING an acquisition + flood-field correction — `ThermalDrift` / `ThermalDriftStudy` / `montecarlo thermal`](AGENTS.Findings.Part2.md#36-thermal-drift-during-an-acquisition--flood-field-correction--thermaldrift--thermaldriftstudy--montecarlo-thermal) |
| 37 | [Random-coincidence pile-up SUM continuum in the spectrum — `EventStreamStudy.ApplyPileUp` / `montecarlo pileup`](AGENTS.Findings.Part2.md#37-random-coincidence-pile-up-sum-continuum-in-the-spectrum--eventstreamstudyapplypileup--montecarlo-pileup) |
| 38 | [Mask fabrication tolerances — a mis-machined tungsten mask vs an ideal decoder — `MaskFabrication` / `MaskFabricationStudy` / `montecarlo maskfab`](AGENTS.Findings.Part2.md#38-mask-fabrication-tolerances--a-mis-machined-tungsten-mask-vs-an-ideal-decoder--maskfabrication--maskfabricationstudy--montecarlo-maskfab) |
| 39 | [Mask–detector alignment / pose error — `CodedApertureMask` pose + `AlignmentStudy` / `montecarlo align`](AGENTS.Findings.Part2.md#39-maskdetector-alignment--pose-error--codedaperturemask-pose--alignmentstudy--montecarlo-align) |
| 40 | [Bad (dead / hot) detector pixels + bad-pixel-map repair — `DetectorDefects` / `DetectorDefectStudy` / `montecarlo defects`](AGENTS.Findings.Part2.md#40-bad-dead--hot-detector-pixels--bad-pixel-map-repair--detectordefects--detectordefectstudy--montecarlo-defects) |
| 41 | [Mask tungsten fluorescence + Compton scatter — `MaskSecondary` / `MaskSecondaryStudy` / `montecarlo masksec`](AGENTS.Findings.Part2.md#41-mask-tungsten-fluorescence--compton-scatter--masksecondary--masksecondarystudy--montecarlo-masksec) |
| 42 | [Counting-system dead time / count-rate saturation — `DeadTime` / `DeadTimeStudy` / `montecarlo deadtime`](AGENTS.Findings.Part2.md#42-counting-system-dead-time--count-rate-saturation--deadtime--deadtimestudy--montecarlo-deadtime) |
| 43 | [Sub-cell peak interpolation — `PeakInterpolation` / `SubCellStudy` / `montecarlo subcell`](AGENTS.Findings.Part2.md#43-sub-cell-peak-interpolation--peakinterpolation--subcellstudy--montecarlo-subcell) |
| 44 | [True (cascade) coincidence summing — `DecayScheme` / `CascadeSummingStudy` / `montecarlo cascade`](AGENTS.Findings.Part2.md#44-true-cascade-coincidence-summing--decayscheme--cascadesummingstudy--montecarlo-cascade) |
| 45 | [Mask forward-scatter folded into the coded image — `MaskScatterStudy` / `montecarlo maskscatter`](AGENTS.Findings.Part2.md#45-mask-forward-scatter-folded-into-the-coded-image--maskscatterstudy--montecarlo-maskscatter) |
| 46 | [Scintillator non-proportionality — the intrinsic-resolution COMPONENT from first principles — `NonProportionality` / `NonProportionalityStudy` / `montecarlo nonprop`](AGENTS.Findings.Part2.md#46-scintillator-non-proportionality--the-intrinsic-resolution-component-from-first-principles--nonproportionality--nonproportionalitystudy--montecarlo-nonprop) |
| 47 | [Finite source size + capsule self-attenuation — `FiniteSourceStudy` / `montecarlo finitesrc`](AGENTS.Findings.Part2.md#47-finite-source-size--capsule-self-attenuation--finitesourcestudy--montecarlo-finitesrc) |
| 48 | [MLEM (Poisson-likelihood) reconstruction — `MlemDecoder` / `MlemStudy` / `montecarlo mlem`](AGENTS.Findings.Part2.md#48-mlem-poisson-likelihood-reconstruction--mlemdecoder--mlemstudy--montecarlo-mlem) |
| 49 | [Thermal DCR / PDE readout effects — `ThermalDrift` (extended) / `ThermalReadoutStudy` / `montecarlo thermalro`](AGENTS.Findings.Part2.md#49-thermal-dcr--pde-readout-effects--thermaldrift-extended--thermalreadoutstudy--montecarlo-thermalro) |
| 50 | [Depth-of-interaction (DOI) parallax in reconstruction — `DoiParallaxStudy` / `montecarlo doi`](AGENTS.Findings.Part2.md#50-depth-of-interaction-doi-parallax-in-reconstruction--doiparallaxstudy--montecarlo-doi) |
| — | [Post-theme-50 follow-ons (2026-07-20/21) — WPF nuclide separation, Compton strip, CR-RC fix](AGENTS.Findings.Part2.md#post-theme-50-follow-ons-2026-07-2021--wpf-nuclide-separation-compton-strip-cr-rc-fix) |
| 51 | [Ir-192 (industrial radiography) + tungsten μ(E) between 200 and 600 keV — `Isotopes` / `samples/isotopes/ir192.json` (2026-10-01)](AGENTS.Findings.Part2.md#51-ir-192-industrial-radiography--tungsten-μe-between-200-and-600-kev--isotopes--samplesisotopesir192json-2026-10-01) |
| 52 | [Crystal attenuation from tabulated cross sections — `CrystalMaterial` (2026-10-01)](AGENTS.Findings.Part2.md#52-crystal-attenuation-from-tabulated-cross-sections--crystalmaterial-2026-10-01) |
| 53 | [Field of view at field distance + out-of-field cue — `FieldOfViewStudy` / `montecarlo fov` (2026-10-01)](AGENTS.Findings.Part2.md#53-field-of-view-at-field-distance--out-of-field-cue--fieldofviewstudy--montecarlo-fov-2026-10-01) |
| 54 | [Dose rate from the detector spectrum — `DoseStudy` / `montecarlo dose` (2026-10-01)](AGENTS.Findings.Part2.md#54-dose-rate-from-the-detector-spectrum--dosestudy--montecarlo-dose-2026-10-01) |
| 55 | [Localisation bias from undersampling the mask shadow — Studio optics at 1 m (2026-10-02)](AGENTS.Findings.Part2.md#55-localisation-bias-from-undersampling-the-mask-shadow--studio-optics-at-1-m-2026-10-02) |
| 56 | [Detector gap, crosstalk and depth from focus on Studio's list-mode path (2026-10-02)](AGENTS.Findings.Part2.md#56-detector-gap-crosstalk-and-depth-from-focus-on-studios-list-mode-path-2026-10-02) |
| 57 | [Why the focus sweep's depth is biased — a heuristic score on a mismatched forward model (2026-10-02)](AGENTS.Findings.Part2.md#57-why-the-focus-sweeps-depth-is-biased--a-heuristic-score-on-a-mismatched-forward-model-2026-10-02) |
| 58 | [CR-RC shaper: whole-code state lost low-energy pulses; Q12 fractional state and explicit shaping times (2026-10-02)](AGENTS.Findings.Part2.md#58-cr-rc-shaper-whole-code-state-lost-low-energy-pulses-q12-fractional-state-and-explicit-shaping-times-2026-10-02) |
| 59 | [CsI(Tl) added to the crystal table — what-if material, same method as theme 52 (2026-10-02)](AGENTS.Findings.Part2.md#59-csitl-added-to-the-crystal-table--what-if-material-same-method-as-theme-52-2026-10-02) |
| 60 | [The seeded `DefaultRandom` (legacy `System.Random`) is biased when draw counts vary (2026-10-02)](AGENTS.Findings.Part2.md#60-the-seeded-defaultrandom-legacy-systemrandom-is-biased-when-draw-counts-vary-2026-10-02) |
| 61 | [Per-decay cascade emission in list mode — correct, and negligible at 1 m (2026-10-02)](AGENTS.Findings.Part2.md#61-per-decay-cascade-emission-in-list-mode--correct-and-negligible-at-1-m-2026-10-02) |
| 62 | [Joint forward-likelihood depth beats the sharpest plane — but is not yet a Studio replacement (2026-10-02)](AGENTS.Findings.Part2.md#62-joint-forward-likelihood-depth-beats-the-sharpest-plane--but-is-not-yet-a-studio-replacement-2026-10-02) |
| 63 | [Evidence quoted over seed ensembles — `samples/evidence` (2026-10-02)](AGENTS.Findings.Part2.md#63-evidence-quoted-over-seed-ensembles--samplesevidence-2026-10-02) |
| 64 | [Absolute ambient background — terrestrial field, calibrated gate, and what it changes (2026-10-04)](AGENTS.Findings.Part2.md#64-absolute-ambient-background--terrestrial-field-calibrated-gate-and-what-it-changes-2026-10-04) |
| 65 | [Cs-137 under Co-60's Compton continuum — the detection limit, the side window and the cost of stripping (2026-10-06)](AGENTS.Findings.Part2.md#65-cs-137-under-co-60s-compton-continuum--the-detection-limit-the-side-window-and-the-cost-of-stripping-2026-10-06) |
