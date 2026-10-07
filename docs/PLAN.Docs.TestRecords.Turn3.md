# PLAN.Docs.TestRecords.Turn3 — catalog completion and explicit RTL interpreter

Scope: complete the existing catalog, collect RTL with the installed Python 3.13 interpreter, verify a scratch draft, and measure archive size. No test assertion, physical tolerance, product implementation or Git state was changed. No desktop or render test was executed. Size reductions are proposals only; no compressed archive format was applied.

Status: implementation and local verification complete; planner independently reviews and commits, then collects M1 on the clean implementation commit. No committed milestone or repository VV.Tests.Results.md was created.

## Files by group

- `samples/testing/test_records.py`: `collect --family cocotb --python py -3.13` accepts an interpreter command prefix. The runner and cocotb-version query use that prefix. The record identifies `tool.pythonCommand` and `tool.executionPython`; `tool.python` continues to identify the collector's interpreter. Specifying `--python` for another family is refused. Default invocation behavior is preserved.
- `docs/VV.Tests.md`: established purpose, oracle/limits, inputs, tolerance/rationale and method contracts for all 153 entries. Includes full method setup/acceptance and class helper contracts instead of the former 32-line clipped excerpts. Rounded equality is distinguished from exact comparisons. Legacy CR-RC tolerance is described only for the two legacy configurations; other configurations take its exact repeatability branch. Four checker oracles are explicit. Source fingerprints, exact trace selectors and discovered membership remain checked.
- `docs/VV.Studio.md`: regenerated TEST INVENTORY and AUTOMATED TRACE RESULTS interiors only. Byte comparison after replacing those two interiors proves every other byte matches the turn-start snapshot, including the planner's “local verification host” redaction.
- `docs/PLAN.Docs.TestRecords.Turn3.md`: this report. `.github/workflows/ci.yml`, RTL assertions/runner and all other prior-turn files were left as received in this turn.
- Scratch: `%TEMP%/gcam-todo28/turn3/verified-bundle` is the definitive working collection; `verified-export` is the portable retained-evidence proof. The earlier `bundle`/`export` and their analyses remain retained. Transcription/measurement/report utilities and defect fixtures are also beneath that scratch root. No scratch files were written to docs/assets.

## Catalog coverage and measured inventory

Before: 23 complete / 130 incomplete. After: **153 complete / 0 incomplete**. Lack of a numerical derivation is an established finding, not a missing oracle field. No genuinely unavailable field remains. “Complete” means documented from source, not that every entry ran or that its numerical criterion is independently justified.


| Runner | Entries | Complete | Incomplete |
| --- | --- | --- | --- |
| checker | 4 | 4 | 0 |
| cocotb | 29 | 29 | 0 |
| dotnet | 118 | 118 | 0 |
| unittest | 2 | 2 | 0 |


Normal xUnit counts before → after are unchanged: **118 classes / 843 cases; 815 passed, 28 not executed, 0 failed**. Source method count is 590; theory expansions supply the additional cases. Python has two classes / 39 cases. RTL has 29 configurations / 137 cases. There are four checker entries.


| Family / project | Classes or units | Cases | Passed | Not executed | Failed |
| --- | --- | --- | --- | --- | --- |
| Gcam.Tests | 72 | 481 | 481 | 0 | 0 |
| Gcam.Studio.Tests | 23 | 211 | 211 | 0 | 0 |
| Gcam.Studio.Services.Tests | 14 | 115 | 108 | 7 | 0 |
| Gcam.Studio.UiTests | 8 | 35 | 15 | 20 | 0 |
| Gcam.Studio.RenderTests | 1 | 1 | 0 | 1 | 0 |
| unittest | 2 | 39 | 38 | 1 | 0 |
| cocotb | 29 | 137 | 111 | 26 | 0 |
| checker | 4 | 4 | 4 | 0 | 0 |


## Tolerance stated without a derivation in the code — grouped findings

The following list identifies classes/configurations containing such findings. It does **not** mark their exact, structural or separately derived statistical clauses unjustified. The source contracts in VV.Tests locate every expression and expected-value computation; comments that justify an expectation but not its selected numerical margin are distinguished. Counts: 66 xUnit classes, three RTL configurations and one checker (70 entries). Product targets (for example 4.5:1 contrast and 16 ms redraw) and operational deadlines are not claimed to be statistical derivations.


### Gcam.Tests


| Class / configuration | Finding and boundaries of that finding | Source |
| --- | --- | --- |
| `AlignmentTests` | Pose equivalence uses the same seeded ray decisions after inverse translation/rotation. Localization bias is compared with source truth and mask-offset magnification (D+S)/D; the 1 mm floor and 1.8–3.4 interval have no derived margins. | [tests/Gcam.Tests/AlignmentTests.cs](../tests/Gcam.Tests/AlignmentTests.cs) |
| `AmbientEvidenceTests` | Line-map superposition and power-of-two intensity scaling are exact; ideal subtraction is compared with the same decoder without a field. Count/window/position tallies and probability ranges are structural. Decimal precisions are stated without a rounding-error derivation. | [tests/Gcam.Tests/AmbientEvidenceTests.cs](../tests/Gcam.Tests/AmbientEvidenceTests.cs) |
| `AngularResolutionStudyTests` | Geometry follows one-period FCFOV and shadow magnification formulas; smoke counts are structural, not MC precision evidence. Landed share integrates the open-area fraction cell by cell and uses four binomial standard errors. Geometry decimal precisions have no stated error-budget derivation. | [tests/Gcam.Tests/AngularResolutionStudyTests.cs](../tests/Gcam.Tests/AngularResolutionStudyTests.cs) |
| `AutoFocusTests` | The oracle is the configured source plane. The comment identifies a roughly 18 mm sweep step, but does not derive the chosen ±25 mm margin. | [tests/Gcam.Tests/AutoFocusTests.cs](../tests/Gcam.Tests/AutoFocusTests.cs) |
| `BackgroundTests` | Pedestal and gradient normalization use their stated formulas. Study checks compare clean/heavy BSR and graded/flat mean maps. The +1 mm knee margin is supported by the 64-seed measurements quoted in code; the remaining decimal/fixed RMS, failure-rate, edge-share and near-identity margins are not derived. | [tests/Gcam.Tests/BackgroundTests.cs](../tests/Gcam.Tests/BackgroundTests.cs) |
| `CascadeEmissionTests` | Biased and analog per-decay rates use binomial and weight-bound variances combined in quadrature, four standard errors. Summed-energy and scheme-selection checks are structural; the added 1e-9 energy ceilings have no error-budget derivation. | [tests/Gcam.Tests/CascadeEmissionTests.cs](../tests/Gcam.Tests/CascadeEmissionTests.cs) |
| `CascadeSummingTests` | Co-60 multiplicities follow independent table branchings; angular moments use Legendre coefficients and four sample-mean standard errors, and the histogram uses the stated chi-square quantile. The slope 1.5–2.8 is supported by the quoted 24-seed slope distribution. Initial branching bands, energy windows, decimal comparisons and minimum near-field yield include fixed margins without derivation. | [tests/Gcam.Tests/CascadeSummingTests.cs](../tests/Gcam.Tests/CascadeSummingTests.cs) |
| `ComptonTests` | Kinematic energy conservation, Compton edges and unit length are formula oracles; spatial contamination and stripped Cs counts use configured positions and pure-Cs references. The 1e-6, 2.5 mm, contamination and stripping margins are stated without derivation. | [tests/Gcam.Tests/ComptonTests.cs](../tests/Gcam.Tests/ComptonTests.cs) |
| `CrystalMaterialTests` | NIST elemental mixtures and xraylib photo/Compton anchors are references. Comments explain coherent scattering and the Gd edge, but do not derive the 0.92 lower ratio, decimal precisions, photo-fraction interval or 2× edge margin. | [tests/Gcam.Tests/CrystalMaterialTests.cs](../tests/Gcam.Tests/CrystalMaterialTests.cs) |
| `CsIMaterialTests` | CsI has its own measured maximum NIST discrepancy (rounded upward to 0.00011) plus displayed half-units. Analytic photo anchors/electrons per gram, density propagation and retained decimal half-units are specified in code. Interpolation is the geometric mean; its 12-decimal equality is identified as double arithmetic, without an operation-count derivation. | [tests/Gcam.Tests/CsIMaterialTests.cs](../tests/Gcam.Tests/CsIMaterialTests.cs) |
| `DeadTimeTests` | Hand enumerated arrival decisions are exact. MC rates compare with R/(1+R tau) and R exp(−R tau). Code mentions sparse survivors under extreme load, but does not derive the 5%, 10% or 0.2 turnover margins. | [tests/Gcam.Tests/DeadTimeTests.cs](../tests/Gcam.Tests/DeadTimeTests.cs) |
| `DecoderInvariantTests` | Analytic shadows, linear superposition and the retained pedestal behavior are the oracles. Half a projected resolution element is stated as the plateau/argmax limit; decimal reconstruction comparisons have no operation-count error derivation. | [tests/Gcam.Tests/DecoderInvariantTests.cs](../tests/Gcam.Tests/DecoderInvariantTests.cs) |
| `DepthDesignTests` | The oracle is the sharpness-curve trend with distance and aperture rank×mosaic×pitch. The 1.5× worsening, 150–250 range and one-decimal aperture margin are stated without derivation. | [tests/Gcam.Tests/DepthDesignTests.cs](../tests/Gcam.Tests/DepthDesignTests.cs) |
| `DepthLocalizationTests` | Known source coordinates are matched to distinct reconstructed peaks. Code states true depths 160/260 mm and a 10 mm sweep step; it does not derive the lateral ±1 mm or depth ±25 mm margins. | [tests/Gcam.Tests/DepthLocalizationTests.cs](../tests/Gcam.Tests/DepthLocalizationTests.cs) |
| `DepthTests` | Configured source depth, relative focus widths and comparative joint/3D search errors are the oracles. The 20 mm and 5 mm error ceilings are stated without derivation. | [tests/Gcam.Tests/DepthTests.cs](../tests/Gcam.Tests/DepthTests.cs) |
| `DetectorDefectTests` | Same-seed maps, exclusive dead/hot membership and clean identity are exact/structural. Fraction and repair comparisons use the requested defect fractions and clean/raw references; LLN is mentioned but the chosen intervals, decimal precision and 1.3/1.4 multipliers are not derived. | [tests/Gcam.Tests/DetectorDefectTests.cs](../tests/Gcam.Tests/DetectorDefectTests.cs) |
| `DoiParallaxTests` | The comparison is the difference with and without DOI at known angle/thickness. Code explains depth×tan(angle); the 0.05 mm, 1.5 multiplier and 0.1–1 mm margins are not derived. | [tests/Gcam.Tests/DoiParallaxTests.cs](../tests/Gcam.Tests/DoiParallaxTests.cs) |
| `DoseTests` | ICRP 74 anchors/log-log interpolation, held-out frontal G(E) responses and paralyzable analytic ratios are the oracles. The frontal ±25% is a stated product regression target, not a derived uncertainty; decimal, low-reading and live-clock thresholds have no numerical margin derivation. | [tests/Gcam.Tests/DoseTests.cs](../tests/Gcam.Tests/DoseTests.cs) |
| `EntranceAbsorberTests` | Zero-thickness, monotonic energy and thickness comparisons are structural. 662/32 keV transmission targets (>0.98 and 0.35–0.55) and decimal precisions are stated without derivation. | [tests/Gcam.Tests/EntranceAbsorberTests.cs](../tests/Gcam.Tests/EntranceAbsorberTests.cs) |
| `EventStreamTests` | Incident line energy, requested Poisson arrival rate, photopeak/continuum presence and text round-trip are the oracles. Normal/isotropic photopeak fractions compare with three combined binomial standard errors; other deposit, rate, count and decimal margins have no derived budget. | [tests/Gcam.Tests/EventStreamTests.cs](../tests/Gcam.Tests/EventStreamTests.cs) |
| `FieldOfViewTests` | Square FCFOV angles, source angle/centroid side and non-cyclic/cyclic localization fractions are the references. The rounded angles and performance-fraction/efficiency thresholds are stated without uncertainty derivation. | [tests/Gcam.Tests/FieldOfViewTests.cs](../tests/Gcam.Tests/FieldOfViewTests.cs) |
| `FiniteSourceTests` | Point/small/large source reconstructions and increasing capsule thickness are compared; source broadening and preferential low-energy attenuation are qualitative oracles. Fixed FWHM, contrast and transmission margins are not derived. | [tests/Gcam.Tests/FiniteSourceTests.cs](../tests/Gcam.Tests/FiniteSourceTests.cs) |
| `FocusFusionTests` | The configured source plane and correct/wrong laser plane define comparative focus/SNR expectations. The 240–380, <450 mm and 1.2× criteria are tolerance stated without a derivation in the code. | [tests/Gcam.Tests/FocusFusionTests.cs](../tests/Gcam.Tests/FocusFusionTests.cs) |
| `FrontEndTests` | Photoelectron-budget resolution, 1/sqrt(E) and 1/E terms are formula oracles. Comments compute 8043 photoelectrons and 4.30% FWHM; the decimal precisions, sampled mean/spread and DCR/window margins have no error-budget derivation. | [tests/Gcam.Tests/FrontEndTests.cs](../tests/Gcam.Tests/FrontEndTests.cs) |
| `Ir192Tests` | ENSDF line/half-life data and NIST tungsten ratios are the external tabular oracles. Position checks use one projected resolution element (the comment explains the coarser grid). The one-percent attenuation and tabular decimal margins lack a derived error budget. | [tests/Gcam.Tests/Ir192Tests.cs](../tests/Gcam.Tests/Ir192Tests.cs) |
| `KleinNishinaTests` | Smooth quadrature of the angular density supplies bin probabilities; four binomial standard errors are used. Comments identify rounding in energy ratio/cosine, but do not derive the selected decimal precisions for rotation. | [tests/Gcam.Tests/KleinNishinaTests.cs](../tests/Gcam.Tests/KleinNishinaTests.cs) |
| `ListModeSourceTests` | Independent weighted floods and weight-resampled spectra use w²<=w_max*w and the stated resampling-pool bound. Physical rate and exponential gap moments/CDF use four standard errors; pixels use five. Performance timings are measurement only. The acceptance<=0.1 fixture ceiling is not statistically derived. | [tests/Gcam.Tests/ListModeSourceTests.cs](../tests/Gcam.Tests/ListModeSourceTests.cs) |
| `MaskAttenuationTests` | 662 keV normalization, high/low-energy ordering and endpoint clamping are the oracles. The >20 and 40–60 attenuation targets have no stated margin derivation. | [tests/Gcam.Tests/MaskAttenuationTests.cs](../tests/Gcam.Tests/MaskAttenuationTests.cs) |
| `MaskFabricationTests` | Same-seed hole geometry is exact; transmission is compared with the ideal mask. The degradation t statistic uses 24 paired seeds, k=3, measured effect size and noncentral-t false-failure probability quoted in code. Wander, blocked-transmission, efficiency<0.97 and decimal margins are not derived. | [tests/Gcam.Tests/MaskFabricationTests.cs](../tests/Gcam.Tests/MaskFabricationTests.cs) |
| `MaskGeometryTests` | Hole/focus/taper/pitch alternatives are compared at the same seeded settings. Relative ordering is structural; the straight-edge<0.95 margin is stated without derivation. | [tests/Gcam.Tests/MaskGeometryTests.cs](../tests/Gcam.Tests/MaskGeometryTests.cs) |
| `MaskScatterTests` | Gap, energy-window and balanced-decoder alternatives are compared against primary-only counts/confidence and the known source. Fixed contamination, energy, confidence and localization margins are not derived. | [tests/Gcam.Tests/MaskScatterTests.cs](../tests/Gcam.Tests/MaskScatterTests.cs) |
| `MaskSecondaryTests` | Energy-dependent W photoelectric shares, K-edge fluorescence absence and forward-scatter spectrum are the oracles. Fixed fraction, attenuation, open-share, yield and band multipliers are not derived. | [tests/Gcam.Tests/MaskSecondaryTests.cs](../tests/Gcam.Tests/MaskSecondaryTests.cs) |
| `MixedFieldTests` | Single-source/superposition references, activity/intensity weighting, known coordinates, isotope windows and Co-only calibrated downscatter provide the oracles. Fixed 3%/5%, 2/2.5/4 mm and contamination/stripping margins are not derived. | [tests/Gcam.Tests/MixedFieldTests.cs](../tests/Gcam.Tests/MixedFieldTests.cs) |
| `MlemOptionTests` | The retained pre-change loop and supplied columns are exact references. Pixel averages, binary/vector loops and background fixed points use operation-count unit-roundoff bounds quoted in code. The extra 1e-15 entry enclosure and rounded transmission interval include fixed margins with no separate derivation. | [tests/Gcam.Tests/MlemOptionTests.cs](../tests/Gcam.Tests/MlemOptionTests.cs) |
| `MlemTests` | Known source/pair positions and cross-correlation reconstructions are the comparison. Non-negativity/order and pair verdicts are structural; fixed bias, -1e-6, +0.2 mm and -1e-9 margins are not derived. | [tests/Gcam.Tests/MlemTests.cs](../tests/Gcam.Tests/MlemTests.cs) |
| `MuraGeneratorTests` | Gottesman–Fenimore row/column signature, repeated mosaic cells and decoding origin are exact. The 0.40–0.55 open-fraction interval is stated without derivation. | [tests/Gcam.Tests/MuraGeneratorTests.cs](../tests/Gcam.Tests/MuraGeneratorTests.cs) |
| `NonProportionalityTests` | 662-normalized response tables, proportional-crystal null and per-energy/preset comparisons are the oracles. Fixed response deficits/FWHM ratios and decimal comparisons have no derived error budget. | [tests/Gcam.Tests/NonProportionalityTests.cs](../tests/Gcam.Tests/NonProportionalityTests.cs) |
| `PileUpTests` | Hand specified bursts, extending merge behavior and total energy preservation are exact/structural. The low-rate>0.98 and high-continuum>3× criteria are stated without derivation. | [tests/Gcam.Tests/PileUpTests.cs](../tests/Gcam.Tests/PileUpTests.cs) |
| `PipelineTests` | Configured source truth, cyclic ghost side, analog-vs-biased efficiency and density/leak alternatives are the oracles. The 1.5/5 mm and 20% margins are not derived. | [tests/Gcam.Tests/PipelineTests.cs](../tests/Gcam.Tests/PipelineTests.cs) |
| `RangeLocalizationTests` | The known lateral source coordinate at each standoff is the oracle; the 8 mm ceiling is stated without derivation. | [tests/Gcam.Tests/RangeLocalizationTests.cs](../tests/Gcam.Tests/RangeLocalizationTests.cs) |
| `ResolvedPairTests` | Synthetic tent prominence/widths, order-statistic median and brute-force floor selection supply known answers. Comments give rational-entry and relative-rounding precision and acos amplification 4u/sin(theta). The +1e-12 exceedance guard is stated without a separate derivation. | [tests/Gcam.Tests/ResolvedPairTests.cs](../tests/Gcam.Tests/ResolvedPairTests.cs) |
| `ScattererTests` | Known incident energy, Compton-vs-photoabsorption ordering and bare/entrance-scatter valley counts provide the oracle. The >5× interaction and >1.5× valley margins are tolerance stated without a derivation in the code. | [tests/Gcam.Tests/ScattererTests.cs](../tests/Gcam.Tests/ScattererTests.cs) |
| `SoilAirMaterialsTests` | Composed air/NIST comparisons allow half a unit of four-significant-figure table rounding (5e-4 relative); grid and edge values are the table oracles. Selected decimal equality precisions are stated without a separate operation-count derivation. | [tests/Gcam.Tests/SoilAirMaterialsTests.cs](../tests/Gcam.Tests/SoilAirMaterialsTests.cs) |
| `SoilAirTransportTests` | Slab-averaged uncollided analytic kernels use four per-history second-moment standard errors. Reproducible tallies, thresholds and bookkeeping identities are exact/structural; decimal bookkeeping precision is stated as rounding only without a derived operation count. | [tests/Gcam.Tests/SoilAirTransportTests.cs](../tests/Gcam.Tests/SoilAirTransportTests.cs) |
| `SourceDistanceTests` | Detector-biasing inverse-square weight and old shared-plane replay provide the oracle. The 0.20–0.30 interval around one quarter is stated without derivation. | [tests/Gcam.Tests/SourceDistanceTests.cs](../tests/Gcam.Tests/SourceDistanceTests.cs) |
| `SubCellTests` | Synthetic tent/Gaussian apex and fallback identities are analytic references. Quantization RMS is step/sqrt(12). Decimal comparisons and the 0.7–1.5 and half-RMS study margins are stated without error-budget derivation. | [tests/Gcam.Tests/SubCellTests.cs](../tests/Gcam.Tests/SubCellTests.cs) |
| `TerrestrialCatalogTests` | Pinned catalog/sidecar hashes, table branch feed and malformed-data rejection are exact/structural; the .9999–1 ratio and decimal precisions have no separate margin derivation. | [tests/Gcam.Tests/TerrestrialCatalogTests.cs](../tests/Gcam.Tests/TerrestrialCatalogTests.cs) |
| `ThermalDriftTests` | Calibration null, spatial temperature/gain pattern and compensated/uncompensated studies are compared. The 5 mm localization ceiling comes from the quoted 64-seed floor/ghost separation; remaining efficiency/residual multipliers and decimal margins are not derived. | [tests/Gcam.Tests/ThermalDriftTests.cs](../tests/Gcam.Tests/ThermalDriftTests.cs) |
| `ThermalReadoutTests` | The doubling-interval DCR formula and low-energy/temperature alternatives are the oracles; fixed 3.9/10/0.9 and decimal margins are stated without derivation. | [tests/Gcam.Tests/ThermalReadoutTests.cs](../tests/Gcam.Tests/ThermalReadoutTests.cs) |
| `TransportInvariantTests` | Rectangle solid angle, Beer–Lambert slant absorption and open-fraction-plus-leak are closed-form oracles. Four weighted-mean/binomial standard errors are used where coded. Decimal absorption and deposit-roundoff ceilings have no separate derived numerical budget. | [tests/Gcam.Tests/TransportInvariantTests.cs](../tests/Gcam.Tests/TransportInvariantTests.cs) |
| `WaveformTests` | Captured Python integer-reference golden values are exact on deterministic paths; pulse floating samples use the coded decimal precision. ADC math is analytic. Integer-filter proportionality .02 and recovered-energy 15 keV margins are stated without an accumulated-error derivation. | [tests/Gcam.Tests/WaveformTests.cs](../tests/Gcam.Tests/WaveformTests.cs) |

### Gcam.Studio.Services.Tests


| Class / configuration | Finding and boundaries of that finding | Source |
| --- | --- | --- |
| `OpticsProjectionTests` | Retained event identity/reprojection and one projected resolution element are oracles. Small-sample RMS ratio .85 comes from the quoted five-seed maximum plus twice the observed range, rounded upward; it protects only those pinned regressions. The full-sweep .75 improvement thresholds are stated without derivation. | [tests/Gcam.Studio.Services.Tests/OpticsProjectionTests.cs](../tests/Gcam.Studio.Services.Tests/OpticsProjectionTests.cs) |
| `WaveformServiceTests` | Legacy rasterization, retained pulse/event identity, chain/settings consistency and bounded plot work are the oracles. Performance reports are measurement only; pulse-readout numerical comparisons retain their coded precision, without a general derived rounding budget. | [tests/Gcam.Studio.Services.Tests/WaveformServiceTests.cs](../tests/Gcam.Studio.Services.Tests/WaveformServiceTests.cs) |

### Gcam.Studio.Tests


| Class / configuration | Finding and boundaries of that finding | Source |
| --- | --- | --- |
| `DetectorFaceTests` | Active area ((pitch−gap)/pitch)², tiled areas and crystal/gap positions are geometric oracles. Code labels 1e-12 edge arithmetic as roundoff; the fixed 12-decimal and absolute budgets have no operation-count derivation. | [tests/Gcam.Studio.Tests/DetectorFaceTests.cs](../tests/Gcam.Studio.Tests/DetectorFaceTests.cs) |
| `FocusSweepMathTests` | Uniform inverse-distance endpoints, synthetic half-height interpolation/censoring/modes and one-to-one angle linkage supply analytic fixtures. Decimal geometry comparisons are tolerance stated without a derivation in the code. | [tests/Gcam.Studio.Tests/FocusSweepMathTests.cs](../tests/Gcam.Studio.Tests/FocusSweepMathTests.cs) |
| `HeatmapViewportTests` | Known fit dimensions, round-trip/anchor coordinates, clipping and pixel-centre conventions are geometric oracles. Six/nine-decimal equality margins are stated without a derived rounding-error budget; integer cells and zoom limits are exact/structural. | [tests/Gcam.Studio.Tests/HeatmapViewportTests.cs](../tests/Gcam.Studio.Tests/HeatmapViewportTests.cs) |
| `HistogramPlotTests` | Bin geometry, independent extrema/boundary counts and layout exclusion are structural or exact references. Timings are measurement only; decimal geometric margins are stated without an error-budget derivation. | [tests/Gcam.Studio.Tests/HistogramPlotTests.cs](../tests/Gcam.Studio.Tests/HistogramPlotTests.cs) |
| `MeasurementMathTests` | Euclidean distance, vertex-angle and pixel-centre ROI sums are recomputed on synthetic fixtures. Decimal geometric equality margins are tolerance stated without a derivation in the code; ROI counts/clip domains are exact. | [tests/Gcam.Studio.Tests/MeasurementMathTests.cs](../tests/Gcam.Studio.Tests/MeasurementMathTests.cs) |
| `NiceTicksTests` | Explicit 1/2/5 steps, decade/minor tick sets and formatted labels are fixture oracles. Tick membership is structural; decimal step comparisons retain their stated precision without a rounding-error derivation. | [tests/Gcam.Studio.Tests/NiceTicksTests.cs](../tests/Gcam.Studio.Tests/NiceTicksTests.cs) |
| `OpticsPolicyTests` | Preset field tuples, geometry formulas and invalid effective-text/intensity/rank/gap/overflow inputs provide the oracle. Exact/structural plus coded rounded geometry equality; those decimal margins have no derived error budget. | [tests/Gcam.Studio.Tests/OpticsPolicyTests.cs](../tests/Gcam.Studio.Tests/OpticsPolicyTests.cs) |
| `OverlayLabelLayoutTests` | Preferred positions and explicitly occupied rectangles test containment, separation and omission. Exact/structural geometry; any decimal coordinate margin is stated without a derivation in the code. | [tests/Gcam.Studio.Tests/OverlayLabelLayoutTests.cs](../tests/Gcam.Studio.Tests/OverlayLabelLayoutTests.cs) |
| `PlotViewportTests` | Analytic linear/log mappings, equal decade distances, coordinate round trips and anchor-preserving zoom/pan are geometric references. Decimal comparison margins are tolerance stated without a derivation in the code. | [tests/Gcam.Studio.Tests/PlotViewportTests.cs](../tests/Gcam.Studio.Tests/PlotViewportTests.cs) |

### Gcam.Studio.UiTests


| Class / configuration | Finding and boundaries of that finding | Source |
| --- | --- | --- |
| `FloodOracleTests` | Known view/image coordinates, Euclidean/vertex geometry, whole-pixel centre counts and parser fixtures are the oracles. The /5 long-arm comparison is geometric scaling, not sampled accuracy; selected decimal margins have no rounding-budget derivation. | [tests/Gcam.Studio.UiTests/FloodOracleTests.cs](../tests/Gcam.Studio.UiTests/FloodOracleTests.cs) |
| `ScenarioTests` | Product lifecycle, independently computed pixel-centre ROI/distance/angle and configured source position provide the oracle. FloodOracle derives distance tolerance as 2*sqrt(2) device pixels plus .05 mm display rounding; Verdict derives angle tolerance from arm length and two-pixel endpoint/vertex errors. The additional .006+1e-12 one-cell comparison and 1.5 mm localization allowance are tolerance stated without a derivation in the code; the localization comment only says it allows acquisition photon noise. | [tests/Gcam.Studio.UiTests/ScenarioTests.cs](../tests/Gcam.Studio.UiTests/ScenarioTests.cs) |
| `WorkspaceOracleTests` | Hand-specified histogram centres/counts, half-open arrival windows and synthetic half-max curves provide the oracle. Counts/index sets are exact; decimal interpolated endpoints retain the coded precision without an operation-count derivation. | [tests/Gcam.Studio.UiTests/WorkspaceOracleTests.cs](../tests/Gcam.Studio.UiTests/WorkspaceOracleTests.cs) |

### Gcam.Studio.RenderTests


| Class / configuration | Finding and boundaries of that finding | Source |
| --- | --- | --- |
| `PlotViewRenderTests` | Analytic drawing/fake acquisition fixtures, direct control/viewmodel state and layout/binding expectations are the oracles; saved PNGs are measurement only, with no golden-pixel comparison. Full partial-class helpers are covered below. Fixed ±5% focus and .01 layout/decimal margins are not derived. The 60 s join is a deadlock deadline. | [tests/Gcam.Studio.RenderTests/PlotViewRenderTests.cs](../tests/Gcam.Studio.RenderTests/PlotViewRenderTests.cs) |

### cocotb


| Class / configuration | Finding and boundaries of that finding | Source |
| --- | --- | --- |
| `test_blr@baseline_restorer` | Python trap_ref.blr on the retained shaped MC stream is exact after best latency 0–4 (no contractual latency asserted). Baseline/pulse checks require raw_min<−15000, \|blr_min\|<\|raw_min\|/3 and blr_max>100000; these margins are stated without a derivation in the code. CAP=200000 samples. | [rtl/test_blr.py](../rtl/test_blr.py) |
| `test_crrc@legacy-f0` | Independent fixture math sets ORDER/A/K/F; trap_ref.crrc_int supplies exact sample outputs including invalid-clock holds and signed full scale. Seed 20261002. For legacy only, instantaneous-tail peaks require \|p800/p400−2\|<.05; the comment says this is the retained tolerance, with no derivation in code. Legacy F=0 also requires golden peak 304 and sample[35]=23. Non-legacy repeatability is exact. C# vector comparison is exact and opt-in; absence is not a pass. | [rtl/test_crrc.py](../rtl/test_crrc.py) |
| `test_crrc@legacy-f12` | Independent fixture math sets ORDER/A/K/F; trap_ref.crrc_int supplies exact sample outputs including invalid-clock holds and signed full scale. Seed 20261002. For legacy only, instantaneous-tail peaks require \|p800/p400−2\|<.05; the comment says this is the retained tolerance, with no derivation in code. Legacy F=0 also requires golden peak 304 and sample[35]=23. Non-legacy repeatability is exact. C# vector comparison is exact and opt-in; absence is not a pass. | [rtl/test_crrc.py](../rtl/test_crrc.py) |

### checker


| Class / configuration | Finding and boundaries of that finding | Source |
| --- | --- | --- |
| `calibration` | Exact hashes, identity/structural checks and byte equality. Integer-count recovery allows <0.33 count; tolerance stated without a derivation in the code. This checker verifies reported calibration evidence, not new physical measurements. | [samples/evidence/calibration_record.py](../samples/evidence/calibration_record.py) |


Derived examples kept distinct: Stat.Within's estimator-specific standard errors and default k=4; six-SE ambient-field Poisson/binomial checks; joint-EM operation-count roundoff; CR-RC recurrence floors; CsI's own measured discrepancy/printed half-units; Currie PMF/Cornish–Fisher/lattice bounds; paired mask-fabrication t test; measurement correlation/binomial variance; gain-mixture fourth moments; histogram two-bin plus five-width-SE allowance; four-SE Chebyshev strip-count bounds; independent detector-gap predictive spread; pinned empirical sampling and thermal localization margins. Their derivations come from their own code/comments, never from another scenario.

## RTL collection and scratch proof

The earlier interpreter startup failure is resolved. Both turn-3 RTL collections actually ran the full matrix through **`py -3.13`: CPython 3.13.4, cocotb 2.0.1**, Icarus/VVP 12.0 development build. The collector itself used Python 3.11.9; these identities are separate record fields. `--python py -3.13` argument parsing was exercised by the real collection, not only a fixture.

Result: **111 passed / 26 not executed / 0 failed / 0 missing**, across 29 results XML/configuration pairs. Optional `csharp_vectors_match_python_and_rtl` is skipped once for each of 26 CR-RC configurations because GCAM_CRRC_VECTORS is unset. No C# vectors were invented/exported. Both trapezoid tops and the BLR retained event-stream test ran successfully. Built sim.vvp hashes match before/after. Tests' assertions and numerical criteria are unchanged.

The definitive bundle has 11 invocation records, 153 catalog units with results, and **1,023 selected cases: 968 passed, 55 not executed, 0 failed, 0 missing**. Source snapshot is common to all family records; overlapping subjects are bound by hash. Draft provenance is expected from this uncommitted tree and remains separate from pass/fail and coverage. The 55 skips are 20 desktop, one render, seven numerical-evidence xUnit cases, one optional provenance Git fixture and 26 optional RTL vector cases. NumPy/SciPy background-shape tests did run locally (10 passed); those dependencies remain an explicit CI gap. No evidence/render/desktop opt-in was enabled.


Source snapshot: `4c48613284e559d5013cb5c11b09906a967501f01082a9cb7d9614da50c5e689`; source commit: `266b74072f30883b84c42bd949cd0ed61ecd767b`. All runner invocations exited 0.



Verification:

- `dotnet build Gcam.sln -c Release`: passed, zero warnings/errors; repeated after command-local TEMP/TMP redirection.
- Full `DOTNET_CLI_UI_LANGUAGE=en dotnet test Gcam.sln -c Release`: passed, 815/28/0; repeated after temp redirection. Per-project collector --no-build runs agree.
- Both Python suites through the stdlib adapter: 38 passed / one opt-in skipped; repeated in the definitive bundle. No install and no Git-fixture mutation.
- `python samples/evidence/calibration_record.py --check`: four records up to date; all checks passed. The recorded calibration checker also passed its --release checks.
- `check-catalog --discover`: 153 units, zero incomplete, membership/source/expanded discovery passed.
- `check` of the working bundle, both generated Studio blocks and sanitized portable export: passed; retained raw TRX/XML/JSON re-normalize to the recorded cases and generated report bytes.
- `self-test`: 22 precise refusals plus eight adapter/sanitizer checks passed; recorded invocation and separate final invocation passed.
- Archived replay currently finds zero committed milestones, explicitly reported as zero; this is not an M1 acceptance claim.

The first unredirected normal .NET verification/collection used existing test fixture names `%TEMP%/gcam-roundtrip-*.json` and `%TEMP%/gcam-lenient-*.json`, outside the narrower TODO-28 scratch directory. This was a scratch placement mistake. Subsequent full verification and definitive family collection set TEMP and TMP only for their command process to `%TEMP%/gcam-todo28/turn3/runtime-temp`; no test was edited and no manual delete/move was executed. Earlier scratch evidence is retained, not replaced or deleted. The final standalone self-test already specifies its scratch output explicitly.

## Retained size: measured bytes, not estimates

The review's 0.3–0.5 MiB JSON estimate was too low. The implemented records repeat complete source manifests and before/after subject maps, and retain per-case normalization plus explicit selection/generation provenance. This accounts for substantially more JSON than a compact outcomes-only summary. All sizes below are actual UTF-8/export bytes, not Git pack sizes or filesystem allocation.


| File kind | Turn-2 files | Turn-2 bytes | Turn-3 files | Turn-3 bytes |
| --- | --- | --- | --- | --- |
| trx | 5 | 1,209,452 | 5 | 1,209,449 |
| json | 19 | 2,338,518 | 47 | 2,437,705 |
| xml | 0 | 0 | 29 | 35,278 |
| log | 16 | 98,473 | 16 | 221,354 |
| md | 4 | 1,378,317 | 4 | 2,878,673 |


TRX + JSON: turn 2 **3,547,970 bytes**; turn 3 **3,647,154 bytes**. Full retained export: **6,782,459 bytes** (6.468 MiB); the full total additionally includes XML, logs and the pinned catalog/report/generated-block Markdown.


### Turn-2 retained example by family


| Family | TRX | JSON | XML | Logs | Markdown | Total bytes |
| --- | --- | --- | --- | --- | --- | --- |
| dotnet | 1,209,452 | 1,216,077 | 0 | 91,989 | 0 | 2,517,518 |
| unittest | 0 | 177,304 | 0 | 5,029 | 0 | 182,333 |
| cocotb | 0 | 141,710 | 0 | 1,200 | 0 | 142,910 |
| checker | 0 | 556,379 | 0 | 255 | 0 | 556,634 |
| shared | 0 | 247,048 | 0 | 0 | 1,378,317 | 1,625,365 |

### Turn-3 definitive export by family


| Family | TRX | JSON | XML | Logs | Markdown | Total bytes |
| --- | --- | --- | --- | --- | --- | --- |
| dotnet | 1,209,449 | 1,216,188 | 0 | 92,134 | 0 | 2,517,771 |
| unittest | 0 | 177,331 | 0 | 5,029 | 0 | 182,360 |
| cocotb | 0 | 224,423 | 35,278 | 123,938 | 0 | 383,639 |
| checker | 0 | 556,397 | 0 | 253 | 0 | 556,650 |
| shared | 0 | 263,366 | 0 | 0 | 2,878,673 | 3,142,039 |


“Shared” is selection.json, generation.json, pinned catalog.md, report.md, inventory.md and trace.md. Current JSON consists of 11 run records, four checker invocation documents, one unittest adapter document, 29 RTL configuration documents, selection and generation. No built DLL/vvp, copied fixture trees or raw unsanitized TRX is in the portable export. The larger catalog is additional Markdown, not counted as JSON: it deliberately retains the full reviewed contracts and helper context rather than the old clipped excerpts.

### Proposed reductions — not applied

Deterministic gzip was measured in memory (`compresslevel=9`, `mtime=0`) and each file was decompressed to byte equality and recompressed to the same bytes. No compressed archive files were written and no record schema/reader changed.


| Candidate packaging | Current bytes | gzip bytes | Saved bytes |
| --- | --- | --- | --- |
| trx | 1,209,449 | 208,354 | 1,001,095 |
| json | 2,437,705 | 727,878 | 1,709,827 |
| xml | 35,278 | 13,768 | 21,510 |
| log | 221,354 | 28,040 | 193,314 |
| md | 2,878,673 | 354,294 | 2,524,379 |


Recommendations and effects:


- Deterministic gzip of sanitized TRX: **1,209,449 → 208,354 bytes**. Retain hashes for canonical uncompressed bytes and compressed storage bytes; replay decompresses, verifies idempotent sanitization, then re-normalizes. This keeps TD-2's proof intact but needs an adopted archive-encoding decision and reader/self-test changes.

- Compress canonical JSON too, if accepted: TRX + JSON becomes **936,232 bytes** before other file kinds. Every field is retained. Gzip of all retained file kinds totals **1,332,334 bytes**; that is an upper bound for individually compressed packaging, not a promised Git pack size. Markdown may be kept readable instead.

- Lossless content-addressed source/subject-manifest pooling: **2,149,498 → 943,599 bytes** for the run records plus eight unique pooled manifests, saving **1,205,899 bytes**. Scratch expansion reconstructed every original canonical run record byte-for-byte. Keep both pre/post references and equality checks. This requires an archive envelope/reference resolver and corruption self-tests; it is not silently substituted into schema v1.

- Optional normalized fields `displayName`, `adapterId`, `started`, `finished`: omitting only these saves **224,493 bytes** in run records. They are recoverable from retained runner evidence/identities; per-run timestamps, per-case duration, verdict/raw outcome, identity/trace selector, skip reason and failure subtype remain. Re-normalization must compare an explicitly defined projection, so this is a schema/adapter decision, not something --check currently accepts. Prefer compression/pooling before removing fields.


Short version banners instead of complete Icarus copyright/license output could also reduce duplicated identification/report text; full logs would preserve the original banners. No size claim is assigned without implementing and measuring that projection. No tolerance, raw result, source hash, subject identity, selected earlier failure or missing/skip diagnostic should be discarded to shrink records.

## CI status and remaining unverified work

No workflow edit was needed for this interpreter fix: Linux CI's setup-python already selects 3.13 and the collector default remains that executable. Local Windows now explicitly selects the launcher prefix. The prior-turn per-project TRX, always-report/upload, family aggregate, catalog/replay/self-test and 90-day retention wiring remains as received.

GitHub Actions was not run. Hosted checkout/build, Linux platform execution, actual artifact upload/download/retention and aggregate job orchestration remain unverified here. The local full RTL matrix now ran successfully; the earlier Windows VPI startup limitation is resolved. CI still intentionally omits numerical background-shape dependencies and exported C# vector comparisons. No desktop sign-off is implied.

## Updated exact M1 commands — planner after implementation commit

Run from `<repo>`, on a clean implementation commit. These commands are proposed, not executed against a repository milestone here. Use fresh scratch/output destinations. All families are collected before generated repository outputs make the working tree dirty. Runtime fixture temp paths are redirected inside TODO-28 for this command process only. The Python unittest command includes installed NumPy/SciPy suites; retain any adapter/import error if the chosen machine lacks them. `py -3.13` must already be installed; no install command is proposed. Existing vector/desktop omissions stay explicit.

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
$env:PYTHONIOENCODING='utf-8'
$env:DOTNET_CLI_UI_LANGUAGE='en'
$env:GIT_OPTIONAL_LOCKS='0'
$env:GCAM_UI_TESTS='0'
$env:GCAM_RENDER_SNAPSHOTS='0'
$env:GCAM_EVIDENCE_TESTS='0'
$env:GCAM_UI_BREAK_VERDICT='0'
$env:GCAM_README_CAPTURE_ONLY='0'
$env:GCAM_CRRC_VECTORS=$null
$env:GCAM_PROVENANCE_GIT_TESTS='0'
$taskM1 = Join-Path $env:TEMP 'gcam-todo28/M1'
if (Test-Path $taskM1) { throw 'Choose a fresh scratch path; preserve earlier evidence.' }
if (Test-Path 'samples/testing/records/M1') { throw 'M1 destination must not exist.' }
$taskM1GitState = git --no-optional-locks status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'Git source query failed.' }
if ($taskM1GitState) { throw 'Implementation tree must be clean before M1 collection.' }
$taskM1Temp = Join-Path $taskM1 'runtime-temp'
New-Item -ItemType Directory -Path $taskM1Temp -Force | Out-Null
$env:TEMP=$taskM1Temp
$env:TMP=$taskM1Temp
dotnet build Gcam.sln -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
python -B samples/testing/test_records.py check-catalog --discover
if ($LASTEXITCODE -ne 0) { throw 'Catalog failed.' }
python -B samples/testing/test_records.py collect --family dotnet --no-build --out $taskM1
$taskM1DotnetExit=$LASTEXITCODE
python -B samples/testing/test_records.py collect --family unittest --include-background --out $taskM1
$taskM1PythonExit=$LASTEXITCODE
python -B samples/testing/test_records.py collect --family cocotb --python py -3.13 --out $taskM1
$taskM1RtlExit=$LASTEXITCODE
python -B samples/testing/test_records.py collect --family checker --checker calibration --out $taskM1
$taskM1CalibrationExit=$LASTEXITCODE
python -B samples/testing/test_records.py collect --family checker --checker test-catalog --out $taskM1
$taskM1CatalogExit=$LASTEXITCODE
python -B samples/testing/test_records.py collect --family checker --checker archived-replay --out $taskM1
$taskM1ReplayExit=$LASTEXITCODE
python -B samples/testing/test_records.py collect --family checker --checker test-record-self-test --out $taskM1
$taskM1SelfTestExit=$LASTEXITCODE
python -B samples/testing/test_records.py generate --bundle $taskM1 --output docs/VV.Tests.Results.md --studio docs/VV.Studio.md --export samples/testing/records/M1
if ($LASTEXITCODE -ne 0) { throw 'Generation refused; retain errors and inspect the scratch bundle.' }
python -B samples/testing/test_records.py check --bundle samples/testing/records/M1 --output docs/VV.Tests.Results.md --studio docs/VV.Studio.md
if ($LASTEXITCODE -ne 0) { throw 'M1 replay refused.' }
python -B samples/testing/test_records.py check --archives
if ($LASTEXITCODE -ne 0) { throw 'Archived replay refused.' }
Write-Output "Invocation exits: dotnet=$taskM1DotnetExit python=$taskM1PythonExit rtl=$taskM1RtlExit calibration=$taskM1CalibrationExit catalog=$taskM1CatalogExit replay=$taskM1ReplayExit self-test=$taskM1SelfTestExit"
```

Each native exit is retained, rather than hidden by successful generation. Review nonzero exits/partial evidence before committing the milestone; provenance is not a pass/fail judgment. The commands neither commit nor mutate Git state. Evidence/render profiles are separate future collections; desktop still needs the author's go. Do not silently replace a repeated case selection.

## Proposed doc wording — not applied

The turn-2 proposals remain applicable. Add to the build/test instructions: “For local RTL records, select the compatible installed interpreter explicitly: `python -B samples/testing/test_records.py collect --family cocotb --python py -3.13 --out <scratch-bundle>`. The record identifies the execution interpreter separately from the collector. Redirect process TEMP/TMP into the task scratch directory when repository tests create temporary fixtures.”

For catalog conventions: “An absent derivation is documented as `tolerance stated without a derivation in the code`; it does not alone make the catalog entry incomplete. Mark incomplete only when an actual field cannot be established, naming that field and why. Exact, structural, measured-only and product-target checks remain distinct.”

For the size decision: “The initial compact JSON estimate did not include repeated source/subject manifests. Any later compressed/deduplicated archive encoding must retain deterministic byte/hash reconstruction and raw-result re-normalization. Keep schema v1 and uncompressed archival evidence until that encoding is adopted and its refusal tests pass.”

## Every command that wrote anything — audit

Commands below are shown with `%TEMP%/gcam-todo28` or `<scratch>` for public path hygiene; the tool log retains their actual arguments and complete inline/patch bodies. Read-only reads/searches/version queries/checks are excluded. Process-local variable assignments are part of their command processes, never persistent settings. No watcher or detached process was started; every returned execution session finished.

| Command / edit | Writes and repetitions |
|---|---|
| apply_patch to `samples/testing/test_records.py` (one patch) | Explicit interpreter option, invocation and tool identification only. No product/test assertion changes. |
| `New-Item -ItemType Directory -Path <scratch>/turn3 -Force`; inline `python -B -c` snapshot script | Created scratch root and saved turn-start `VV.Studio.before.bin`. |
| apply_patch creating/updating `<scratch>/turn3/complete_catalog.py` | One creation, five updates; source transcription notes, checker contracts, attribute/comment context, findings classification and per-configuration relevance. Scratch utility only. |
| `python -B <scratch>/turn3/complete_catalog.py` | Five invocations; rewrote only docs/VV.Tests.md and scratch tolerance-findings.json. Last invocation produced identical catalog bytes while refining the grouped finding list. |
| `dotnet build Gcam.sln -c Release` | Initial command, then repeated in the redirected-temp full verification command. Normal bin/obj/allowed NuGet outputs. |
| `dotnet test Gcam.sln -c Release` with English CLI and three gates off | Initial full command, then redirected-temp full command; bin/obj plus existing test fixture writes. Neither ran desktop/render/evidence opt-ins. |
| `collect --family cocotb --python py -3.13 --out <scratch>/turn3/bundle` | First successful interpreter-specific matrix; output-root builds, configuration/XML/logs, normalized record and selection. |
| `collect --family dotnet --no-build --out <scratch>/turn3/bundle` | Five per-project sanitized TRX/discovery/logs, records and selection. |
| `collect --family unittest --include-background --out <scratch>/turn3/bundle` | Suite adapter JSON/log, fixtures under invocation output, record and selection. Git fixture off. |
| `collect --family checker --checker <name> --out <scratch>/turn3/bundle` for calibration, test-catalog, archived-replay and test-record-self-test | Four recorded invocations; last also writes retained defect fixtures. Underlying catalog/archive/calibration checks do not rewrite their docs. |
| `generate --bundle <scratch>/turn3/bundle --studio docs/VV.Studio.md --export <scratch>/turn3/export` | Scratch catalog/report/inventory/trace/generation pins and portable export; only generated Studio interiors. |
| `self-test --out <scratch>/turn3/self-test` | Defect and adapter fixtures, exact-refusal results. |
| apply_patch creating/updating `<scratch>/turn3/measure_bundle.py` | One creation, two updates; optional-field projection and definitive-export argument. Scratch measurement code only. |
| `python -B <scratch>/turn3/measure_bundle.py` | Two invocations, rewriting scratch measurements.json. The definitive-export invocation is listed separately. Compression experiments in memory only. |
| `New-Item -ItemType Directory -Path <scratch>/turn3/runtime-temp -Force` with command-local TEMP/TMP, followed by Release build/full test | Created redirected runtime scratch; repeated required verification without editing test code. |
| One sequential command process with command-local TEMP/TMP and `collect --out <scratch>/turn3/verified-bundle` | dotnet --no-build; cocotb --python py -3.13; unittest --include-background; four named checkers; each invocation once. Then generate --studio docs/VV.Studio.md --export <scratch>/turn3/verified-export. It also performed read-only bundle/export checks. Each native exit was checked before the next command. |
| `python -B <scratch>/turn3/measure_bundle.py verified-export` | Scratch measurements-verified.json, exact sizes and round-trip compression/deduplication proofs. |
| `self-test --out <scratch>/turn3/self-test-final` | Final explicit-output refusal/adapter fixtures; passed. |
| apply_patch creating/updating `<scratch>/turn3/write_report.py`; `python -B <scratch>/turn3/write_report.py` | One creation/one update and two invocations; scratch report-writing utility and this LF/UTF-8 public report. |

Plain check/check-catalog/calibration --check/archived replay invocations only read. No install, manual delete, rename/overwrite move, dotnet clean, process stop, Git mutation, upload or GUI launch was executed. No approval-requiring command is pending.

APPROVAL REQUESTS: none.
