# VV.Gcam.Evidence — the simulation evidence behind the Gcam requirements

Scope: every simulation or design-model result that a number in the [URS](VV.Gcam.URS.md), the
[PRS](VV.Gcam.PRS.md) or the [Limitations](VV.Gcam.Limitations.md) rests on — what it shows, under which
conditions, how to reproduce it and which test pins it. Each entry is complete on its own: a reader of the V&V set
does not need any other document to check a number. Not covered: GCAM Studio's software verification
([VV.Studio](VV.Studio.md)).

**At a glance**
- 33 entries, `EV-01` … `EV-33`, grouped as imaging, energy and isotopes, rate and dose, head design,
  manufacturing, and range.
- Each entry gives the grade of its evidence ([PRS §1](VV.Gcam.PRS.md#1-evidence-grades)): **MC** Monte Carlo,
  **RTL** front-end model, **AN** analytical design model.
- Nothing here is measured on hardware, and nothing has been compared with recordings of the original instrument.
- Commands are the repository's CLI, `montecarlo <command> <scenario>` (run as
  `dotnet run --project src/Gcam.Cli -c Release -- <command> <scenario>`), or a Python script; outputs land in
  `samples/` or `rtl/`.

## 1. The tools behind the grades

| Grade | Tool | What it models | What it leaves out |
|---|---|---|---|
| **MC** | C# engine (`src/Gcam.*`); each transport stage is held to a closed form by `ConfigLoaderTests`, `SamplingTests`, `TransportInvariantTests` and `DecoderInvariantTests`, with k·σ tolerances that follow the sample size | point or multi-line sources with directional biasing (unbiased against 4π within ~1 %, EV-07); a ray-marched tungsten mask slab with NIST μ(E); Compton transport in a pixelated crystal with tabulated cross sections per material (xraylib / NIST); energy resolution from a photo-electron budget; Poisson counting; cross-correlation and MLEM decoding | room scatter, the shield's side and rear walls except where an entry says otherwise, hardware drift not listed in an entry |
| **RTL** | SystemVerilog shapers (`rtl/*.sv`) checked bit for bit against an integer reference, plus Python front-end models (`rtl/*.py`) | ADC waveform, pulse shaping, pile-up, afterglow, gain spread | the analog chain beyond its noise figures |
| **AN** | Python design models (`samples/*_study.py`, `samples/hardware_concept.py`) built on MC-validated laws | mass, envelope, balance, parallax, heat, hand motion, SiPM gain drift | transport-grade accuracy — first-order estimates |

Geometries used below:

| Name | Scenario file | Head | Source plane |
|---|---|---|---|
| **lab rig** | `samples/scenario.json` | 12 × 12 × 1 mm pixels, rank-7 MURA (2 × 2 mosaic, 1 mm cells, 10 mm W), mask–detector D = 60 mm | 100 mm from the mask |
| **hand-held head** | `samples/scenario_handheld.json` | 16 × 16 × 1 mm GAGG:Ce,Mg, 15 mm thick, same mask, D = 55 mm | 100 mm from the mask unless the entry says 1 m / 5 m |
| original rig, GAGG | `samples/scenario_orig_gagg.json` | the lab rig with a 10 mm GAGG crystal | 100 mm |

## 2. Index

| ID | Evidence | Grade | Used by |
|---|---|---|---|
| EV-01 | Localisation in the fully coded field; ghosts outside it; non-cyclic decoding | MC | PR-IMG-01, -07; LIM-07; UN-01, UN-11 |
| EV-02 | Field of view at field distance and the out-of-field cue | MC | PR-IMG-01, -07, -08, -10; LIM-01, LIM-06, LIM-07 |
| EV-03 | Field of view ÷ resolution = rank | MC | PR-IMG-09, -10; LIM-01 |
| EV-04 | Mask thickness: leak against collimation | MC | PRS §2; LIM-01 |
| EV-05 | Tapered (bevelled) channels | MC | PR-IMG-10; LIM-01 |
| EV-06 | Detector sampling of the mask shadow | MC | PR-IMG-06; LIM-01, LIM-08 |
| EV-07 | Count threshold and directional biasing | MC | PR-SENS-02 |
| EV-08 | Sub-cell peak interpolation | MC | PR-IMG-02 |
| EV-09 | Hand-held head: sensitivity and precision | MC | PR-IMG-02, PR-SENS-01, -07; URS §5 |
| EV-10 | Several isotopes in one field | MC | PR-IMG-03, PR-NRG-06; LIM-04 |
| EV-11 | MLEM reconstruction | MC | PR-IMG-04; LIM-01, LIM-07 |
| EV-12 | Ambient background and mask / antimask | MC | LIM-07 |
| EV-13 | Depth-of-interaction parallax | MC | LIM-08 |
| EV-14 | Event positioning: total-energy window + largest deposit | MC | PR-NRG-03; LIM-08 |
| EV-15 | Isotope separation: the spatial limit and Compton stripping | MC | PR-NRG-04; LIM-04, LIM-05, LIM-08; UN-03 |
| EV-16 | One gain for 122–1332 keV | RTL | PR-NRG-01, -06; LIM-04 |
| EV-17 | Energy-resolution budget and pulse shapers | RTL, MC | PR-NRG-02, PRS §2; LIM-02, LIM-04, LIM-08 |
| EV-18 | Per-channel gain calibration | RTL, MC | PR-NRG-05 |
| EV-19 | Crystal materials | MC | PRS §2, PR-PHY-05; LIM-08 |
| EV-20 | Ir-192 in the engine | MC | URS §5 |
| EV-21 | Count-rate capability and afterglow | RTL | PRS §2, PR-SENS-03; LIM-02; URS §5 |
| EV-22 | Dead time | MC | PR-SENS-04, -05; LIM-02 |
| EV-23 | Dose rate from the detector spectrum | MC | PR-SAFE-01, -02, PR-SENS-05; LIM-02, LIM-06, LIM-09 |
| EV-24 | Mass, envelope and form | AN | PR-PHY-01 … -03, -05; LIM-01 |
| EV-25 | Shield thickness against background energy | MC | PRS §2, PR-PHY-01; LIM-05 |
| EV-26 | Scene-camera parallax | AN | PR-IMG-05, PR-RNG-01 |
| EV-27 | Electronics heat and hand motion | AN | PR-PHY-03, -06, PR-ENV-03, -04; LIM-01 |
| EV-28 | SiPM gain drift and its compensation | AN | PR-ENV-01, -02, -05; LIM-03 |
| EV-29 | Thermal drift during an acquisition | MC | PR-ENV-05 |
| EV-30 | Mask fabrication tolerance | MC | PR-MFG-01, PR-IMG-07; LIM-01 |
| EV-31 | Mask–detector alignment | MC | PR-MFG-02, PR-DUR-03 |
| EV-32 | Dead and hot pixels | MC | PR-MFG-03 |
| EV-33 | Source distance (depth) | MC | PR-RNG-01; LIM-01; UN-10 |

## 3. Imaging

### EV-01 — Localisation in the fully coded field; ghosts outside it

- **Shows.** A centred source is located to ≈ 0.5 mm and an off-axis source inside the fully coded field to
  sub-mm (6 mm → 5.8 mm). The fully coded field is one mask period, `rank × cell × (D+S)/D`: ±9.3 mm on the lab rig.
  A source outside it aliases to a **ghost on the opposite side** (12 mm → −6.6 mm; one period is 18.7 mm). The
  reconstruction's ghost margin (primary ÷ secondary peak) is reported with every estimate.
- **Cyclic against non-cyclic decoding.** Both decoders search the same ±18 mm grid, wider than one period, and a
  position counts if found within 3 mm. Cyclic decoding cannot tell period replicas apart, so it fails even inside
  the field; the finite-mask (non-cyclic) decoder roughly doubles the area: **lab rig 282 vs 148** of 625 positions,
  **hand-held head 360 vs 172** (period 19.7 mm). At D = 55 mm the cyclic ghost margin is only 1.35.
- **Reproduce.** `montecarlo samples/scenario.json` (and `scenario_offaxis.json`, `scenario_ghost.json`);
  `montecarlo sweep samples/scenario_handheld.json` → `samples/sweep_{cyclic,noncyclic}.csv`, then
  `python samples/plot_sweep.py samples/scenario_handheld.json` → `samples/cyclic_vs_noncyclic.png`.
- **Tests.** `PipelineTests.CenteredSource_LocalizesSubMillimeter`, `.OffAxisInsideFcfov_Tracks`,
  `.OffAxisOutsideFcfov_GhostsToOppositeSide`, `.NonCyclicDecoding_SuppressesGhost`;
  `DecoderInvariantTests.AnalyticShadow_PeaksAtItsSource`, `.Decode_IsLinearInTheImage`.

### EV-02 — Field of view at field distance and the out-of-field cue

- **Set-up.** Hand-held head, Cs-137 at **1 m and 5 m**, moved 0–20° off axis along x and along the diagonal. Fully
  coded field ±3.64° along x (±5.14° on the diagonal); resolution element 1.04° (the success threshold). Per angle,
  100 Poisson realisations of a source of fixed strength, N0 = 500 or 5000 counts if it were on axis, with no
  background or a uniform ambient pedestal equal to N0 (BSR 1).
- **Shows.**

  | | N0 5000 | N0 500 | N0 500 + background = signal |
  |---|---|---|---|
  | usable half-field, non-cyclic (≥ 90 % within 1.04°), along x, 1 m / 5 m | **7.0° / 7.5°** | 6.0° / 7.0° | 4.0° / 6.5° |
  | same, cyclic | 3.0° / 3.5° | 3.0° / 3.0° | 3.0° / 3.0° |
  | same, non-cyclic, diagonal, 1 m / 5 m | 9.0° / 4.5° | 4.5° / 3.0° | 0.5° / 0.5° |
  | correct side from the flood centroid (≥ 95 %) | 1.0–14.5° | 1.5–14.0° | 1.5–13.0° |
  | "outside the field" flag from the centroid (≥ 90 %) | 4.0–12.5° | 6.0–11.5° | never (peaks at 68 %, 9.5°) |

  The usable field is set by angle, not distance, and depends on the count level: ±7–7.5° at N0 5000, ±6–7° at
  N0 500. On the diagonal at low counts with background, the wide
  non-cyclic search is worse than the cyclic one (0.5° vs 2.0°). **Beyond ~7.5° the non-cyclic decoder answers with a
  wrong spot inside the field** in 50–90 % of acquisitions at 7.5–11°: it moves the ghost boundary from 3.6° to ~7°,
  it does not remove it. The flood centroid catches those answers — without background ≤ 3 % of the wrong answers at
  7.5–12° stay unflagged; with background equal to the signal 10–45 % of the 5–12° wrong answers do. **Past ~14°
  nothing gives the direction** (the aperture's shadow leaves the array at ≈ 15°), and the decoder still returns an
  in-field spot in ~60 % of acquisitions.
- **Limits.** One isotope (662 keV); the front plate is an infinite 10 mm W slab around the mask, no side walls;
  flat background; the centroid threshold is calibrated on the same series, so in-field false alarms are ≈ 5 % by
  construction.
- **Reproduce.** `montecarlo fov samples/scenario_handheld.json` (≈ 3 min) → `samples/fov.csv`;
  `python samples/plot_fov.py` → `samples/fov.png`.
- **Tests.** `FieldOfViewTests` (square field, non-cyclic beyond the coded field with the centroid giving the side,
  flag at 10°, no cue at 18°, reproducibility).

### EV-03 — Field of view ÷ resolution = rank

- **Shows.** Nominally, field ÷ resolution = rank: a coarser cell or a shorter D widens the field and coarsens the
  resolution by the same factor, and a higher rank widens the nominal field at the same resolution. The **usable**
  field is limited by the 10 mm tungsten slab: at short D its 1 mm channels collimate oblique sources away. With
  12 × 12 × 1 mm pixels and S = 100 mm, the widest field with ≥ 90 % of the swept points localised within one
  resolution element is **rank 11 / 1 mm cells / D = 30 mm: ±21.5 mm**, ~2.5× the original rank-7 / D-60 field
  (±8.6 mm, 93 %). Rank 23 / 1 mm / D = 20 mm is 0.149 usable (median error 65 mm); with the slab thinned to
  0.5 mm at the same μ·t it is 0.752. Decoding also **collapses** when the detector cannot hold about one period
  of the shadow (2 mm cells: every rank ≥ 13 below 40 % usable).
- **Limits.** One source plane (S = 100 mm), one detector (12 × 12 × 1 mm), one mask thickness; "usable" is a coarse
  gate (error < one resolution element), not a precision. The thinner-slab numbers trade leakage geometry for
  acceptance and were not optimised.
- **Reproduce.** `montecarlo scan samples/scenario.json samples/scan.csv`, then `python samples/plot_scan.py` →
  `samples/scan_collapse.png`, `samples/scan_pareto.png`.
- **Tests.** none specific (a sweep of the pipeline that `PipelineTests` covers).

### EV-04 — Mask thickness: leak against collimation

- **Shows.** Optimum ≈ **8–10 mm** tungsten at 662 keV. Below ~7 mm the closed cells leak > 35 % and coding contrast
  collapses. Thicker masks gain no field and collimate off-axis rays through the open channels (edge / centre
  efficiency 0.56 at 8 mm → 0.25 at 32 mm). A 10 mm, 1 mm-cell mask is a 10 : 1 channel that clips rays more than
  ~atan(1/10) ≈ 5.7° off its axis.
- **Reproduce.** `montecarlo thickness samples/scenario.json samples/thickness.csv` → `samples/thickness_opt.png`.
- **Tests.** `PipelineTests.LeakyMask_AddsCountsVersusOpaque`, `MaskAttenuationTests`,
  `TransportInvariantTests.Mask_PassesItsOpenFractionPlusTheTungstenLeak`.

### EV-05 — Tapered (bevelled) channels

- **Shows.** A thick (25 mm) straight mask has edge / centre efficiency 0.82; bevelling the channel walls by ~4°
  restores **0.99** and raises centre efficiency ~35 %, with edge localisation unchanged (~0.43 mm RMS).
- **Limits.** An idealised per-ray acceptance model — an upper bound. A real bevel removes tungsten from the webs
  near the faces, so the mask is not fully opaque everywhere.
- **Reproduce.** `montecarlo masktaper samples/scenario.json` → `samples/masktaper.png`.
- **Tests.** `MaskGeometryTests.TaperedChannels_WidenTheFovOfAThickMask`.

### EV-06 — Detector sampling of the mask shadow

- **Shows.** Each mask-cell shadow needs ≥ 1 detector pixel. At 0.8 pixel per cell (6 × 6) decoding aliases (21 %
  failures); **~2 pixels per cell (16 × 16) is the sweet spot** and about halves the 12 × 12 error; finer helps only
  marginally.
- **Reproduce.** `montecarlo array samples/scenario.json samples/array.csv` → `samples/array_sampling.png`.
- **Tests.** none specific.

### EV-07 — Count threshold and directional biasing

- **Shows.** Below ~25 detected counts localisation collapses; from ~50 it is sub-mm. Directional biasing (photons
  aimed at the detector with an importance weight) matches the 4π efficiency within ~1 % with ~100× fewer photons.
- **Reproduce.** `montecarlo noise samples/scenario.json samples/noise.csv` → `samples/noise_study.png`.
- **Tests.** `PipelineTests.Biasing_IsUnbiasedVersus4Pi`, `TransportInvariantTests.BiasedSource_MeanWeightIsTheDetectorSolidAngle`;
  the Poisson and direction samplers in `SamplingTests`.

### EV-08 — Sub-cell peak interpolation

- **Shows.** Taking the integer peak cell quantises the estimate (RMS = step/√12, independent of counts). A
  three-point tent fit to the correlation peak beats it at every grid step, more so on coarse grids: step 1.8 mm,
  0.52 → **0.13 mm**; step 2.4 mm, 0.66 → 0.19 mm. Tent is the pipeline default.
- **Reproduce.** `montecarlo subcell samples/scenario.json samples/subcell.csv`.
- **Tests.** `SubCellTests`.

### EV-09 — Hand-held head: sensitivity and precision

- **Shows.** Geometric efficiency for Cs-137 **2.48 × 10⁻⁴**, against 1.00 × 10⁻⁴ for the original rig with GAGG
  (**2.48×**: detector area × stopping ≈ 2.35×, plus a solid-angle factor from the shorter D). Localisation floor
  **0.24 mm RMS on axis**, 0.53 mm at the 8 mm edge; sub-mm with ≥ 250 detected counts on axis (0.26 mm) and ≥ 500 at
  the edge (0.55 mm). The original rig's floor is 0.36 mm. At 1 MBq on axis at the lab distance, 250 counts take ~1 s.
  The URS §5 analytic count-rate estimate (geometry × open fraction × 662 keV stopping) gives 2.44 × 10⁻⁴ — within
  2 % of this MC value.
- **Reproduce.** `montecarlo samples/scenario_handheld.json` and `montecarlo samples/scenario_orig_gagg.json` print the
  efficiencies; `montecarlo noise samples/scenario_handheld.json samples/noise_handheld.csv` (and the same for
  `scenario_orig_gagg.json` → `noise_orig_gagg.csv`) gives the precision against counts.
- **Tests.** none specific to the head.

### EV-10 — Several isotopes in one field

- **Shows.** Cs-137 at (5, 1), Co-60 at (−6, 3) and Co-57 at (0, −6) mm, emitted together in one non-cyclic run on the
  lab rig, are each located < 1 mm: **0.55 / 0.27 / 0.93 mm**. The number of sources is known; the reconstruction
  is limited to the fully coded field.
- **Reproduce.** `montecarlo mixedfield samples/scenario.json` → `samples/mixedfield.png`.
- **Tests.** `MixedFieldTests.MultiSource_AllLocalized_InOneRun`, `.Superposition_IsActivityWeighted`.

### EV-11 — MLEM reconstruction

- **Shows.** A Poisson maximum-likelihood reconstruction with the finite mask as its forward model separates source
  pairs **2–3 mm apart** that cross-correlation merges (at 3 mm, valley depth 0.82 against 0.17). On a single source
  it is non-negative (cross-correlation dips to −103) and sharper (FWHM 1.76 vs 2.50 mm), with a localisation bias of
  0.87 mm against 0.55 mm. At 2 mm MLEM resolves the pair and cross-correlation does not (800 k photons); cross-
  correlation needs ~3.5 mm.
- **Limits.** Ideal high-count study on the lab rig; results depend on the iteration count. The system matrix
  samples pixel centres (no pixel-area integration).
- **Reproduce.** `montecarlo mlem samples/scenario.json samples/mlem.csv`.
- **Tests.** `MlemTests`.

### EV-12 — Ambient background and mask / antimask

- **Shows.** A diffuse background is not coded: it lands as a uniform pedestal. Localisation holds at the floor up
  to a background equal to the detected source counts (BSR ≈ 1), then degrades (BSR 2 → 4: RMS 2.95 → 7.11 mm, failures
  9 % → 65 %). A **calibrated background subtraction** matches a physical two-exposure mask / antimask to 0.07 mm, so a
  rotating mask is not needed for diffuse background. Neither removes a directional background source: it is coded
  like the signal and imaged as a second peak. A background graded across the face is harmless while the source
  wins, then drags the estimate toward its strong side at the knee. The subtraction assumes a static, measurable
  background; a background that changes during the acquisition favours concurrent two-exposure subtraction (with a
  static mask / antimask pair, not a moving part).
- **Reproduce.** `montecarlo background samples/scenario.json`; `montecarlo antimask samples/scenario.json
  samples/antimask.csv`; `montecarlo antimask-scene samples/scenario.json`.
- **Tests.** `BackgroundTests`, `DecoderInvariantTests.Cyclic_RejectsAModestPedestal_ButNotAnUnlimitedOne`.

### EV-13 — Depth-of-interaction parallax

- **Shows.** The decoder back-projects the crystal's front face, but gammas interact at depth; for an oblique ray this
  shifts the estimate. The shift is 0 on axis and grows with crystal thickness off axis: at 9 mm off axis, 0.10 mm for
  a 5 mm crystal, **0.30 mm for 30 mm** — below the grid step, but a floor under sub-cell precision at the field edge.
- **Reproduce.** `montecarlo doi samples/scenario.json samples/doi.csv`.
- **Tests.** `DoiParallaxTests`.

## 4. Energy and isotopes

### EV-14 — Event positioning: total-energy window + largest deposit

- **Shows.** A 662 keV gamma often Compton-scatters between pixels. Windowing the **total** deposit and placing the
  event at the largest-deposit pixel keeps **12 %** of the ideal counts against **6 %** for per-pixel windows (2.0×),
  with lower RMS and fewer failures at a 400-photon budget (3.51 vs 5.65 mm, 16 % vs 46 %). The comparison, not the
  absolute RMS, is the result (only ~50 / ~25 counts remain at that budget).
- **Reproduce.** `montecarlo compton samples/scenario.json` → `samples/compton_strategies.png`.
- **Tests.** `ComptonTests.Argmax_RecoversMoreCountsThanPerPixelWindow`,
  `TransportInvariantTests.ComptonCascade_NeverDepositsMoreThanThePhotonEnergy`.

### EV-15 — Isotope separation: the spatial limit and Compton stripping

- **Shows — the spatial limit.** In a 662 keV window with 1 mm GAGG pixels, Co-60 downscatter is **~55 %** of the
  window's counts in an equal-photon-budget demonstration (not an activity-normalised field). Because that downscatter is coded from Co-60's direction, decoding places it at the Co-60
  position — but the weaker Cs-137 peak survives only while Co-60 is ≤ 2× stronger: at Co × 8 it is lost (8.75 mm
  error), at Co × 2 it holds, from × 4 it fails.
- **Shows — the spectral lever.** Per-pixel Compton stripping subtracts `R ×` each pixel's Co-60 photopeak count from
  the 662 keV window, with R = downscatter-into-662 ÷ Co photopeak, then decodes. It recovers a **co-located**
  Cs-137 count: R = 0.469, error 0–4 % (`compton-strip`, where R is taken from the simulation's true contamination
  map — an oracle); in a true mixed field, with R calibrated from a Co-only run, R = 3.97 and the error 0–11 % from
  only 3 true counts (`mixedstrip`).
- **Limits.** Not blind separation: it needs the lines known in advance and a calibrated ratio per geometry. R is a
  single global scalar applied per pixel; the aggregate count can look right while local residuals remain, and those
  are not checked.
- **Reproduce.** `montecarlo compton`, `compton-strip`, `mixediso`, `mixedstrip`, each with `samples/scenario.json`
  → `samples/compton_contamination.png`, `compton_strip_combined.png`, `mixediso.png`, `mixedstrip.png`.
- **Tests.** `ComptonTests.Contamination_IsImagedAtTheContaminantSource_NotTheTarget`,
  `.Stripping_RecoversCsCount_EvenCoLocated`; `MixedFieldTests.MixedField_ThroughEnergyWindow_SeparatesIsotopesSpatially`
  (at Co × 2), `.ComptonStripping_RecoversCoLocatedCsCount`.

### EV-16 — One gain for 122–1332 keV

- **Shows.** With the ADC gain set for Cs-137, Co-60 saturates. Setting it so 1332 keV sits under full scale resolves
  Co-60's two lines and leaves the 122 keV line unhurt (FWHM 15.8 → 16.2 %): at 12 bits one gain covers the whole span.
  A low line sits on every higher line's Compton continuum, so its window is contaminated by downscatter.
- **Reproduce.** `python rtl/multi_isotope_study.py` → `rtl/multi_isotope.png`.
- **Tests.** none (a Python study).

### EV-17 — Energy-resolution budget and pulse shapers

- **Shows.** With a realistic front end (6 % intrinsic at 662 keV scaling as 1/√E, 3 keV electronic noise, finite
  rise, 14-bit / 125 MSPS ADC), the 662 keV photopeak is **6.4 % FWHM** = √(6.0² + 2.3²): intrinsic and electronic
  add in quadrature. The ADC's own noise is negligible (6.41 vs 6.40 %). The C# photo-electron model gives 5.7 % for
  GAGG. Among shapers (electronic noise only): cusp 1.02 % < CR-RC⁴ 1.10 % < trapezoid 1.74 %; at 2 Mcps the wider
  trapezoid keeps 40 % of events within ±5 % against ~65 % for CR-RC and cusp. The trapezoid and CR-RC⁴ shapers and
  the baseline restorer exist in SystemVerilog, bit-exact to the shared integer reference; pipelining the
  trapezoid raises its Fmax from 59 to 119 MHz (Lattice ECP5, nextpnr timing — a proxy for the Xilinx target).
- **Reproduce.** `montecarlo eventstream samples/scenario.json 500 1500`, then `python rtl/frontend_study.py` and
  `python rtl/shaper_compare.py`; `montecarlo frontend samples/scenario.json`; `python rtl/run_cocotb.py`;
  Fmax: `rtl/README.md` → `rtl/fmax_ecp5.png`.
- **Tests.** `FrontEndTests`, `WaveformTests`; the cocotb benches in `rtl/test_*.py`.

### EV-18 — Per-channel gain calibration

- **Shows.** Position is robust to per-pixel gain scatter (the balanced MURA decode suppresses smooth multiplicative
  distortion). Energy is not: a 15 % gain σ smears the summed photopeak from 7.4 % to 18 % FWHM; per-channel
  calibration restores it.
- **Limits.** The position result is for one centred source and one fixed non-uniformity seed, not an ensemble.
- **Reproduce.** `montecarlo uniformity samples/scenario.json samples/uniformity.csv` →
  `samples/uniformity_robustness.png`; `python rtl/pixel_uniformity_study.py` → `rtl/pixel_uniformity.png`.
- **Tests.** none specific.

### EV-19 — Crystal materials

- **Shows.** Efficiency for a 10 mm crystal follows density: NaI 0.59 < LaBr3 0.76 ≈ CeBr3 0.77 < GAGG 1.00 <
  LYSO 1.13 < BGO 1.23 (× 10⁻⁴). GAGG's weakness is not efficiency but resolution and afterglow, hence GAGG:Ce,Mg
  (co-doped: low afterglow, non-hygroscopic, no internal radioactivity) or CeBr3 (~4 %, fast, but hygroscopic and
  costly). LYSO carries Lu-176 internal background.
- **Reproduce.** presets in `samples/materials/*.json`, e.g. `montecarlo samples/materials/CeBr3.json`;
  `samples/crystal_materials.png`.
- **Tests.** `CrystalMaterialTests`, `PipelineTests.DenserCrystal_DetectsMore`,
  `TransportInvariantTests.Crystal_StopsOneMinusExpOfMuTimesSlantPath`.

### EV-20 — Ir-192 in the engine

- **Shows.** Ir-192 is modelled from the evaluated decay data (nine gammas ≥ 1 % per decay, 2.137 γ / decay). On axis
  it images like Cs-137 (0.3 mm error, ghost margin 1.15 vs 0.4 mm, 1.16). Per emitted photon the hand-held head
  detects 2.62 × 10⁻⁴ for Ir-192 against 2.48 × 10⁻⁴ for Cs-137; the URS §5 analytic estimate, which uses 662 keV
  stopping for every line, under-states Ir-192 by ~7 %: the reference source RS-1 gives ≈ 1.07 Mcps at 10 mSv/h at
  the device and ≈ 1.07 kcps at 10 µSv/h (the analytic estimate says 1.0 / 1.0).
- **Limits.** Ir-192's cascade summing is not modelled.
- **Reproduce.** `montecarlo samples/scenario_ir192.json` (imaging); `montecarlo samples/scenario_handheld_ir192.json`
  and `montecarlo samples/scenario_handheld.json` print the two efficiencies.
- **Tests.** `Ir192Tests`.

## 5. Rate and dose

### EV-21 — Count-rate capability and afterglow

- **Shows.** Crystal decay time sets rate capability. Plain GAGG's afterglow buries the baseline: **0.2 %** of events
  recovered at 1 Mcps. GAGG:Ce,Mg follows its 55 ns decay; CeBr3 keeps 87 % at 2 Mcps. A simple peak detector loses
  efficiency to pile-up (~44 % at 1.5 Mcps), which is the ~1 Mcps practical limit of the front end.
- **Reproduce.** `python rtl/material_rate_study.py` → `rtl/material_rate.png`; `bash rtl/run.sh` →
  `rtl/rtl_lowrate.png`, `rtl/rtl_highrate.png`.
- **Tests.** none (Python studies).

### EV-22 — Dead time

- **Shows.** On the timed event stream with τ = 1 µs, the recorded rate follows the analytic curves: a
  non-paralysable system saturates toward 1/τ (912 kcps at 10 Mcps true); a **paralysable** one peaks at 1 Mcps
  (366 kcps) and then collapses (550 cps at 10 Mcps) — it counts *fewer* events as the rate rises. The live fraction
  (recorded ÷ true) is the correction.
- **Reproduce.** `montecarlo deadtime samples/scenario.json samples/deadtime.csv`.
- **Tests.** `DeadTimeTests`.

### EV-23 — Dose rate from the detector spectrum

- **Set-up.** Truth: the unscattered fluence at the detector × ICRP 74 H*(10) conversion coefficients. Response: MC
  of a point source 1 m away through the mask and crystal; every event's total deposit, smeared by the energy
  resolution, cut at 30 keV. A weighting G(E) is fitted on 14 frontal energies (50–1500 keV) and then checked without
  refitting.
- **Shows.**

  | | Estimate ÷ truth |
  |---|---|
  | frontal, held-out 70 / 122 / 250 / 662 / 1173 / 1332 keV | 1.05 / 0.90 / 1.13 / 0.91 / 1.04 / 1.07 |
  | reference sources, frontal: Am-241 / Co-57 / Ir-192 / Cs-137 / Co-60 | 1.06 / 0.91 / 1.00 / 0.90 / 1.05 |
  | 662 keV at 0 / 2.5 / 5 / 10 / 20 / 45° | 0.91 / 0.82 / 0.66 / 0.40 / 0.22 / 0.08 |
  | 60 keV at 0 / 2.5 / 5 / 10 / 20° | 1.07 / 0.79 / 0.44 / 0.11 / 0.00 |
  | Cs-137 at 1 / 10 / 100 mSv/h, paralysable τ = 1 µs | 0.86 / 0.53 / 0.004 |
  | same, live-time corrected | 0.90 / 0.90 / 0.90; fails past ~150 mSv/h (live fraction < 10⁻³) |

  **Frontally, one G(E) keeps every energy and every reference source within ±13 %.** Off axis the imaging head is a
  collimator: 10° off axis it reads 0.11 (60 keV) to 0.66 (1250 keV) of the dose, ~0 at low energy past 20°. Above
  ~150 mSv/h the live fraction itself keeps falling and is the over-range signature. This result led to the
  decision to read dose from a separate small counter (D-37); that counter is not simulated.
- **Limits.** Front plate only (no side or rear walls), so oblique readings are an upper bound; no room or body
  scatter; one distance; analytic paralysable dead time.
- **Reproduce.** `montecarlo dose samples/scenario_handheld.json` (≈ 10 s) → `samples/dose_*.csv`;
  `python samples/plot_dose.py` → `samples/dose.png`.
- **Tests.** `DoseTests`.

## 6. Head design

### EV-24 — Mass, envelope and form

- **Shows.** The tungsten **shield is ~2/3–3/4 of the mass** and the master weight knob (6 mm ≈ 1.5 kg … 12 mm ≈ 3 kg);
  crystal and mask are a few tens of grams. A 12 → 20 mm detector moves the build from ~2.7 to ~3.4 kg through the
  shield. Envelope: ~206 mm axial × 48 × 48 mm, ~0.47 L at a 12 mm shield, ~0.33 L / ~2 kg at 8 mm. The shield puts the
  centre of mass ~89 mm behind the muzzle (12 mm build), so the grip goes under it and the battery is a rear
  counterweight.
- **Reproduce.** `python samples/handheld_design_study.py` → `samples/handheld_design.png`,
  `samples/handheld_envelope.png`; `python samples/hardware_concept.py` → `samples/hardware_concept.png`.

### EV-25 — Shield thickness against background energy

- **Shows.** Side, top, bottom and rear background is uncoded noise; the thickness that reaches the localisation floor
  depends on its energy: scattered ~250 keV background needs **~8 mm W (~1.2 kg)**; a 662 keV field ~20 mm (~5 kg), or
  12–15 mm with calibrated background subtraction; **Co-60 (~1.25 MeV) ~30 mm (~11 kg)** — not carriable. Leak
  concentrated in the side walls moves the useful thickness from 6 to 8 mm.
- **Limits.** Narrow-beam, uncollided attenuation (no buildup); first-order thicknesses.
- **Reproduce.** `montecarlo shield samples/scenario.json` → `samples/shield.csv`; `python samples/plot_shield.py` →
  `samples/shield.png`.
- **Tests.** `BackgroundTests` (side-leak profile).

### EV-26 — Scene-camera parallax

- **Shows.** The scene camera cannot sit on the mask axis, so a baseline b misregisters the overlay by
  Δθ = arctan(b/z): b = 40 mm gives 7.6° at 0.3 m and 2.3° at 1 m — larger than the ~1° gamma resolution for
  z < ~2.2 m. A measured range z and the known b correct it; one camera axis keeps the correction one-dimensional.
- **Reproduce.** `python samples/camera_parallax_study.py` → `samples/camera_parallax.png`.

### EV-27 — Electronics heat and hand motion

- **Shows.** Heat scales with channel count: a monolithic readout (~30 channels, ~4 W) runs passively at +8 °C; a pixel
  readout (256 channels, ~12 W) needs fins (+24 °C passive). The tungsten shield is a heat sink of 335 J/K
  (4 W for 60 s → +0.7 °C). Pointing tolerance ≈ 0.83° (half a cell shadow at D = 55 mm); free-hand drift of ~1°/s
  smears past ~0.8 s, so sources below ~240 cps need help — per-event IMU de-rotation with camera visual-inertial
  odometry against gyro drift, or a brace.
- **Reproduce.** `python samples/thermal_motion_study.py` → `samples/thermal_motion.png`.

### EV-28 — SiPM gain drift and its compensation

- **Shows.** For the Hamamatsu S13360-3050CS at fixed bias the gain tempco is −1.8 %/°C: a ±10 % energy window loses
  10 % of its counts after only **ΔT = 3.2 °C**. Temperature-compensated bias brings it to −0.25 %/°C (holds to
  ΔT ≈ 25 °C); with an LED pulser −0.17 %/°C (ΔT ≈ 36 °C). The crystal's own light-yield drift (~−0.15 %/°C) is
  invisible to both — only a spectral line catches it. A built-in reference source would track to σ ≈ 0.3 %; natural
  K-40 is too weak (σ ≈ 2.5 %). Global gain drift leaves localisation unchanged; it is an energy-window problem.
- **Reproduce.** `python samples/sipm_thermal_study.py` → `samples/sipm_thermal.png`;
  `python samples/sipm_stab_trend.py` → `samples/sipm_stab_trend.png`.

### EV-29 — Thermal drift during an acquisition

- **Shows.** With ambient +3 °C and self-heating +8 °C (centre hotspot) during one acquisition: without bias
  compensation the photopeak counts droop **100 → 90.6 %** and a flood residual of 6.7 % CoV appears; with 90 %
  compensation 99.9 % and 0.1 %. **Localisation stays at the ~0.6 mm floor either way.** Flood correction taken at
  t = 0 cannot follow a time-varying drift; only the compensation loop can.
- **Reproduce.** `montecarlo thermal samples/scenario.json samples/thermal`.
- **Tests.** `ThermalDriftTests`.

## 7. Manufacturing

### EV-30 — Mask fabrication tolerance

- **Shows.** A seeded per-cell machining error (hole placement σ, hole size, blocked cells, drill wander through the
  10 mm slab), decoded with the ideal pattern: RMS holds the ideal floor to **σ ≈ 40 µm**, then 1.9 mm at 80 µm and
  3.6 mm at 160 µm. The reconstruction's peak-to-sidelobe ratio falls 4.4 → 3.8.
- **Reproduce.** `montecarlo maskfab samples/scenario.json samples/maskfab.csv`.
- **Tests.** `MaskFabricationTests`.

### EV-31 — Mask–detector alignment

- **Shows.** An in-plane mask offset biases the source estimate by the magnification (D+S)/D: 1 mm → ~2.5 mm at the
  lab geometry, so **~0.4 mm registration** holds a ~1 mm bias budget. Spacing (2 mm → ~0.4 mm radial bias) and roll
  (2° → ~0.1 mm) are far more forgiving.
- **Reproduce.** `montecarlo align samples/scenario.json samples/align.csv`.
- **Tests.** `AlignmentTests`.

### EV-32 — Dead and hot pixels

- **Shows.** On the 12 × 12 array, replacing each flagged pixel with the mean of its good neighbours keeps the floor
  at **0.60 → 0.68 mm** for up to 8 % bad pixels; unrepaired, it reaches ~1.2–1.8 mm.
- **Reproduce.** `montecarlo defects samples/scenario.json samples/defects.csv`.
- **Tests.** `DetectorDefectTests`.

## 8. Range

### EV-33 — Source distance (depth)

- **Shows.** The shadow's magnification depends on distance, so refocusing estimates it: S = 40 → 39, 100 → 97,
  200 → 214 mm, with the depth resolution widening from ~50 mm at 40 mm to ~240 mm beyond 150 mm. With noise the near
  field converges to sub-mm, the far field floors at ~10 mm (S = 150 mm). Lateral position stays at ~mm while depth is
  uncertain. Depth resolution worsens about as z^1.5; for an 18 mm mask the ±10 % range reaches only ~0.15 m — a
  coded aperture gives direction, a rangefinder gives distance. At the viewer's default optics (rank 13, 0.7 mm cell,
  D = 80 mm) the sharpest-plane estimate is reproducible near (300–500 mm) but biased by +60…+80 mm on axis and
  −20…−140 mm off axis, and from ~700 mm only a lower bound is measured (2026-10-02). The cause is the focus score itself — a heuristic on a decode that does
  not model pixel area and slab transmission; a forward likelihood with a known bearing reduces the bias to a few mm,
  but no unbiased estimator without a known bearing has been shown.
- **Reproduce.** `montecarlo depth`, `depth-joint`, `depth3d`, `depthdesign`, each with `samples/scenario.json` →
  `samples/depth_estimation.png`, `depth_joint.png`, `depth3d.png`.
- **Tests.** `DepthTests`, `DepthDesignTests`, `RangeLocalizationTests`.

## 9. Model history

Numbers above are from the current engine. Changes that moved earlier results:

| Date | Change | Effect on the evidence |
|---|---|---|
| 2026-10-02 | Depth from focus measured on the viewer's list-mode path at its default optics (1 m standoff) | EV-33 extended: near-field estimate reproducible but biased by tens of mm, far field a lower bound only; earlier near-field figures unchanged |
| 2026-10-02 | Configuration scan re-run with the engine in git (10 mm ray-marched tungsten slab); the earlier figures predated the slab model | EV-03: the max-FOV pick changes from rank 23 / 1 mm / D 20 ("±64 mm, 96 %", not reproducible: 0.149) to rank 11 / 1 mm / D 30 (±21.5 mm, 90 %); "~7×" becomes ~2.5×; high ranks collapse at short D by collimation. |
| 2026-10-01 | Crystal attenuation and photoelectric share taken from tabulated cross sections per material, replacing one 662 keV μ and one power law for every crystal (which absorbed too much, too photoelectrically) | EV-14: per-pixel window 24 → 6 % of ideal counts; EV-15: Co-60 share of the 662 keV window 22 → 55 %, Cs-137 lost from Co × 4 (was shown at × 8); EV-09: hand-held efficiency 2.65 → 2.48 × 10⁻⁴; EV-19: same ranking, lower values. All PRS numbers were re-measured on the new model. |
| 2026-10-01 | Ir-192 added; tungsten μ(E) points at 200–600 keV | EV-20; earlier lines unchanged |
| 2026-10-01 | Field-distance FOV study and dose model added | EV-02, EV-23 replace the estimates the PRS carried before (usable field ±5°, dose "not modelled") |
| 2026-07 | Tent sub-cell interpolation made the pipeline default | lowered localisation floors (EV-08, EV-09) |

## 10. Changing this document

- A new result that a V&V number rests on gets an entry here (next free `EV-` ID) in the same commit as the
  requirement row that cites it; a changed result updates its entry and every row in the "Used by" column.
- IDs are stable: add, don't renumber. An entry whose result is withdrawn keeps its ID, marked *withdrawn*.
- An entry must stand on its own: result, conditions, limits, reproduce command, tests. It cites code, tests and
  outputs, never a working note.
