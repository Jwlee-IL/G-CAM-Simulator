# PLAN.Physics.RigReadout.Review — physical four-output readout, measured prototypes and decisions needed

Scope: TODO-19 implementer's review of the current reference plan, code audit, headless throwaway measurements, proposed physical model, configuration, default-selection study and Studio integration. This is a review, not an implementation specification or hardware-performance validation.

Status: review and measurement only, 2026-10-02. The reference plan was read from the main tree; all repository writes in this turn are confined to this review in the w19 worktree. No plan, Todo or Findings was edited. Decisions belong to the planner's next revision, following [AGENTS.Planning](AGENTS.Planning.md).

## Recommendation and material corrections

Implement a physical readout as an explicit alternative to `DirectCrystal`, retaining the latter as a reference until a paired study selects a winner. The conventional Anger preset is a baseline drawn from published practice. Geometry, network topology and threshold calibration are explicit simulation parameters; defaults remain subject to measured comparison.

1. **Preserve raw interaction sites before optical processing.** Present list-mode records have already discarded the information needed to simulate light. Crystal index alone cannot reconstruct subcrystal position or DOI, and the existing sites callback contains post-crosstalk crystal aggregates.
2. **An ideal Anger readout cannot reproduce arg-max for all histories.** For a single deposit with isolated, matched coupling, noiseless LUT assignment can equal direct crystal assignment. For a multi-crystal Compton history, four-channel charge ratios encode its light centroid. This differs physically from the largest deposit even with perfect electronics. Restrict the reference plan's no-noise equivalence target to single-site events.
3. **Do not require edge compression by construction.** It depends on optical boundaries, the resistor graph, input impedance and timing. An ideal symmetric divider can be linear with isolated 1:1 coupling. A uniformly connected grid with four corner drains is a different network. Measure both; do not insert a compression polynomial.
4. **Trigger configuration requires a truth table and channel energy scale.** OR, AND, multiplicity and summed triggers cannot be conflated. If a channel threshold is expressed in its share of total calibrated energy, AND at 100 keV requires at least 400 keV total; 122 keV cannot pass. If each channel's calibration multiplies its center response by four, the same nominal threshold means something else. Specify threshold codes or channel calibration explicitly for each simulation option.
5. **Gaussian smoothing does not sharpen spots.** It suppresses shot noise and broadens them. Watershed needs controlled markers, plateau handling and an explicit failed-calibration result. Do not force 144 identifiable peaks in an unresolved flood.
6. **PDE and fill have different meanings.** Manufacturer PDE includes microcell geometric fill. Apply the photosensitive footprint/package dead area through ray interception; do not multiply the same microcell fill into PDE again.
7. **There is no measured ceramic attenuation length in this repository.** The legacy 40 micrometre optical length is an assumption, not a ceramic material measurement. A 1 mm ceramic wall cannot be extrapolated reliably from it. Optical wall reflectance/transmittance and gamma transport in the wall also need distinct treatment.

## Every “What exists” row checked

References below were checked against this worktree's current source.

| Plan row | Code checked | Verdict and qualification |
|---|---|---|
| Event position and energy | [ComptonCrystalDetector](../src/Gcam.Detector/ComptonCrystalDetector.cs), `Score`, lines 205–236; [ListModeSource](../src/Gcam.Simulation/ListModeSource.cs), constructor, `MergeSites`, `AdvanceSignal`, `AdvanceDecay` | Confirmed with a correction: pulse total is computed **before** optical spreading; the selected arg-max pixel is **after** spreading. Cascade sources merge post-spread pixel aggregates across photons of one decay, then choose the largest aggregate. `DetectedEvent` retains only integer X/Y, total keV and arrival time. |
| Other strategies | `ComptonCrystalDetector.Deposit`, `ComptonStrategy` | Confirmed: PerPixelWindow, AntiCoincidence, Argmax and Centroid. Centroid uses energy times **pixel center**, then floors to a pixel; no SiPM, noise, resistor network, continuous flood coordinate or calibration LUT. |
| Light budget | [FrontEndModel](../src/Gcam.Detector/FrontEndModel.cs), `Photoelectrons`, `FwhmFraction`, `Measure`; [FrontEndParts](../src/Gcam.Configuration/FrontEndParts.cs), `BuildConfig` | Confirmed lumped scalar measurement. Important distinction: bare `FrontEndConfig` defaults are 45 ph/keV, collection .60, PDE .45, ENF 1.20 and intrinsic .032; Studio's GAGG chain is 50 ph/keV, collection .50, PDE .40, ENF 1.03, intrinsic .035, DCR 500 kcps, integration 200 ns. A prototype must name its own parameters. |
| Crosstalk | `ComptonCrystalDetector` constructor, `ApplyOpticalCrosstalk`, `Leak` | Confirmed effective contact × exp(−gap/.04 mm), fluctuating leak uniform from 0 to twice effective fraction, capped .95. Four neighbours share leaked energy; outside-array shares are dropped. Thus **collected light is not conserved at edges**, although the list-mode energy stays the original total. Theme 56's no-change observation is a measured special case, not a mathematical guarantee that arg-max never changes. |
| SiPM pitch / deleted viewer | Local `git show 85b2ed1:src/Gcam.Wpf/MainWindow.xaml.cs`, `BlockifyFlood`, its caller; current Studio Detector contracts | Confirmed: block averages write the same mean into every crystal in a block. They are neither optical transport nor Anger positioning. No current spatial SiPM/readout model was found. The deleted implementation was read from the local history without changing git state. |
| Pile-up | [EventStreamStudy](../src/Gcam.Simulation/EventStreamStudy.cs), `StreamEvent`, `ResolvingSamples`, `ApplyPileUp`; [SpectrumService](../src/Gcam.Studio.Services/SpectrumService.cs), `_groupEnergy`, `_lastArrival` | Confirmed energy-only extending/paralyzable grouping. `rise + 2 tail` is explicitly an **effective** resolving interval, not a peak-hold hardware measurement. EventStreamStudy samples photon deposits; it is not the current per-decay cascade source. ListModeSource separately models true-coincidence decay merging. |
| Waveform | [WaveformService](../src/Gcam.Studio.Services/WaveformService.cs), `Process`, rasterizer call and explanatory note; [Waveform](../src/Gcam.Detector/Waveform.cs) | Confirmed summed energy stimulus and scalar shaping. The note explicitly excludes four position channels. Default ADC is AD9648, signed 14-bit coding, 125 MSPS and 2000 keV full scale; `NoiseKev=3` is analog RMS, not a derived charge-noise value for a four-output network. |

Additional integration checks:

- [MeasurementStage](../src/Gcam.Studio.Services/MeasurementStage.cs) applies one pixel gain and one scalar smear with an index-addressed RNG. It exposes no continuous position or multi-site gain response.
- [ImagingService](../src/Gcam.Studio.Services/ImagingService.cs) applies measured-energy windows, then writes `ev.PixelX/Y` into each image. Its “All” image comes from the acquisition's direct-index flood. Neither image follows Spectrum's pile-up grouping.
- [AcquisitionSession](../src/Gcam.Studio.Services/AcquisitionSession.cs) stores fresh list-mode events and increments its flood at their integer index. The source look-ahead and RNG state survive Stop/Continue. Readout must preserve this continuation contract.
- Gamma transport checks the gap at entry/back re-entry; **internal flight/interactions are still in a continuous homogeneous slab**, and gaps do not interrupt those flights. The sites list merges all deposits in one pixel, losing individual XYZ and electron-deposit energies. A wide ceramic wall makes this transport approximation more consequential.
- Findings themes 35, 52 and 56 were read. Their earlier per-pixel-window crosstalk optimum, block-average pitch failure and list-mode no-effect result cannot select an Anger default. The current GAGG material table, not the obsolete fixed attenuation curve, drives the probes.

## Geometry parameter sweeps and parameterisation

| Parameter case | Active width a | Pitch p | Shared intercrystal gap g | Array width with current half-gap exterior convention | Geometrical crystal fill |
|---|---:|---:|---:|---:|---:|
| Pitch / shared-wall sweep | 1 mm | 2 mm | 1 mm | 24 mm | 25.0% |
| Active-width / shared-wall sweep | 2 mm | 3 mm | 1 mm | 36 mm | 44.44% |
| Exterior-wall sensitivity, independent of interpixel wall | unknown | 2 mm sweep option | unknown, smaller | unknown | not determined |
| Repository baseline | 1 mm | 1 mm | 0 mm | 12 mm | 100% |

The first two are distinct parameter combinations in the simulation study. A commercial GAGG array example specifies **2×2 mm crystal pixels and .2 mm interpixel reflector** independently, supporting the need to parameterise those quantities independently. [CRYLINK GAGG datasheet](https://www.scintillator-crylink.com/wp-content/uploads/Ce%EF%BC%9AGAGG-scintillator-crystals.pdf).

Keep existing `PixelPitchMm` and `ReflectorGapMm` as the authoritative shared-gap geometry, with `ActiveWidthMm = pitch − gap` derived and shown explicitly. Do not accept three independently mutable dimensions. Add exterior reflector/housing thickness separately. A coating thickness on each crystal face implies a shared gap of **twice** that coating, whereas a shared separator thickness equals the gap. Validate positive active width and sensor footprints that do not overlap. Explicitly parameterise sensor counts, active width, pitch, placement offset and rotation.

The S13360-3050CS is a **3×3 mm** active device with 3600 50 micrometre cells, not a buildable 1:1 sensor at a 1 or 2 mm pitch. It is the repository's representative light-budget component; physical geometry must use its published dimensions. [Hamamatsu product specifications](https://hep.hamamatsu.com/jp/ja/products/S13360-3050CS.html). Do not silently shrink that physical preset; a virtual matched sensor must be labelled as an assumption.

Geometry sweeps must preserve and report mask shadow coverage and sampling. With the repository's rank 7, 1 mm mask cells and D=60/S=100 mm, one mask-cell shadow is 1 × (D+S)/S = **1.600 mm** wide at the detector. Detector pitch 2 or 3 mm changes sampling to .800 or .533 pixels per shadow cell, independently of readout quality. A fixed 14 mm mask also covers different fractions of 12, 24 and 36 mm detector faces. Compare direct and readout on the **same geometry**, then repeat at matched optics; never attribute a geometry change to the readout.

## Physical model proposed

The event chain should be `raw interactions → optical detection → SiPM charges/times → electrical four outputs → synchronized digitisation and trigger → Anger coordinate/total charge → calibration LUT → crystal counts → decoder`. Keep true deposits, analog response and measured events separate. All omitted mechanisms need visible assumption/provenance fields in study output, not hidden constants.

| Ingredient | Model and parameter origin | Alternatives for the combination study |
|---|---|---|
| Scintillation at each interaction | LY(Eelectron) × deposit, isotropic photons with material emission/time distribution. Start with the existing GAGG 50 ph/keV and 90 ns as repository representative values, with material-grade dependence. Manufacturer grades span different yields/decays; n≈1.9 and emission near 520–530 nm are supported. Use the existing non-proportional electron response where requested. Poisson photons are a baseline assumption; a measured scintillation variance/Fano parameter can replace it. Do not independently add the same non-proportional resolution floor twice. | GAGG grades / existing material presets, proportional vs tabulated electron response; propagate uncertainty in LY and decay. [CRYLINK](https://www.scintillator-crylink.com/wp-content/uploads/Ce%EF%BC%9AGAGG-scintillator-crystals.pdf). |
| Light-spread function | Build photon-transport response tables over crystal XYZ (and emission wavelength where useful), tracing Fresnel refraction/reflection, bulk attenuation, reflector BRDF, transmission and optical-interface geometry. Runtime samples **joint SiPM photoelectron counts** from those tables. Include escaping and absorbed categories so energy/light accounting is explicit. No Gaussian spread chosen to make the flood look right. | Polished / rough crystal surfaces; opaque diffuse / specular / measured ceramic; PTFE / ESR with their own data; coupling index/thickness; direct coupling vs explicit guide thickness and guide material. |
| Ceramic wall | Identity/composition, R(λ,angle), T(λ,angle), diffuse fraction and absorption are unknown. Keep wall width tied to geometry, but keep material optical coefficients separate. In the prototype R=.96, T=0 and bulk length=100 mm are **assumptions**, not borrowed ceramic data. Fit R/T only to independent optical/device calibration, not a localisation target. | Opaque lower-leak bound versus measured transmitting reflector; R=.90/.96/.99 and diffuse fraction 0/.5/1 are sensitivity points, not confidence limits. For gamma transport, model wall composition/density or explicitly retain the dead-gap approximation; “ceramic” alone cannot supply μ(E). |
| Direct coupling, no guide | Conventional Anger preset: guide thickness zero; coupling grease/window remain finite. Refraction at GAGG/couplant/window determines the detected footprint. Sensor package dead area is geometric. | Couplant thickness/index; alignment; active sensor/crystal ratio; guide 0 to several mm with separately stated materials. A guide is not automatically better for matched isolated crystals. |
| SiPM PDE, fill, ENF and dark counts | PDE integrated over emitted spectrum and overvoltage; manufacturer's PDE includes microcell fill. Footprint and package gap are separate. Datasheet supports 74% microcell fill and typical 500 kcps for the 3×3 mm S13360-3050CS, at specified conditions; its peak-wavelength PDE is not necessarily GAGG-weighted PDE. Use either explicit branching/afterpulses or ENF, not both for the same process. Dark avalanches are distributed across the **entire sensor array**, baseline-subtracted with the stated integration/estimator covariance. | Bias/PDE–DCR–crosstalk trade-off; microcell pitch/count; temperature; correlated avalanche model vs low-occupancy ENF approximation. [Hamamatsu series datasheet](https://www.hamamatsu.com/content/dam/hamamatsu-photonics/sites/documents/99_SALES_LIBRARY/ssd/s13360_series_kapd1052e.pdf). |
| SiPM saturation/recovery | Finite microcell count and recovery are necessary once a small matched sensor receives thousands of photons. A no-recovery pulse gives Ncells·(1−exp(−Npe/Ncells)); the GAGG decay requires a time-dependent recovery model, so that expression is only a limiting check. Device recovery is unknown here. | Cell count and recovery time from the chosen device; compare finite-cell and infinite-cell bounds. The first prototypes use infinite cells and cannot establish absolute energy linearity. |
| Symmetric resistive charge division | Solve an explicitly documented resistor/capacitor graph and output load, first DC fractions, then impulse responses. The literature distinguishes SCD (equal branching into X rows/Y columns) from DPC corner multiplexing; “symmetric” does not select a unique graph. Network values and topology must be explicit simulation choices supported by published circuits. The first probes use ideal separable/bilinear charge fractions and a unit-conductance 2D grid with corner drains as **two assumptions**, not equivalent SCD circuits. | SCD with staged X/Y reduction, corner DPC, grounding/load choices, calibrated per-sensor digital readout as a comparison; resistor tolerances and sensor capacitance. Published SCD measurements show the resistance/load trade-off changes bandwidth and electrical crosstalk; do not copy another detector's P/V or optimum resistors. [Wang et al.](https://pmc.ncbi.nlm.nih.gov/articles/PMC7945691/), [Georgiou et al. abstract](https://ir.lib.uth.gr/xmlui/handle/11615/33458). |
| Channel thresholds | Threshold four output voltages/codes, with explicit calibration and OR/AND/multiplicity/sum truth table. Thresholds determine event triggering; do not erase a below-threshold output from a triggered event's position calculation unless that readout is explicitly selected. | 0/50/100/125/150 keV channel-equivalent and summed triggers, with spatial acceptance and noise-trigger rate measured. |
| Peak-hold and synchronized digitisation | Propagate four impulse responses and trigger delay. Define whether a common hold time samples all channels, or each channel tracks its own peak then all holds are converted together. These differ for unequal shapes and pile-up. Bits alone give quantisation, not ENOB/noise. AD9648 is the continuous-sampling alternative from the repository. | Common-time track/hold vs synchronized independent peak holds vs continuous ADC plus peak/integration estimators; hold window, droop, reset dead time, channel skew, phase, gain, pedestal, ADC range and measured ENC. |
| Anger without correction | For corner labels TL/TR/BL/BR: X=(TR+BR−TL−BL)/Σ and Y=(BL+BR−TL−TR)/Σ. Σ must use pedestal-subtracted outputs; explicitly reject nonpositive sums or clipped outputs. No position correction in the conventional Anger preset. Energy calibration can be separate from coordinate correction. | X/Y row/column formulas for actual SCD, gain equalisation, calibration-derived coordinate linearisation, likelihood position from individual sensors (a different electronics budget). |
| Gaussian + watershed LUT | Independent uniform calibration flood; Gaussian σ in raw coordinate/bin units; find peaks with prominence/separation and monotonic grid ordering; watershed of negative density with deterministic plateaus. Record missing/merged markers, boundary/out-of-calibration rejection and uncertainty. Training labels cannot enter runtime assignment. | σ sweep; nearest-peak Voronoi; energy-specific LUTs; mixture/likelihood classification; manual marker calibration as a separate option. Gaussian is smoothing, not deconvolution. [Beucher, watershed method](https://digitalcommons.usu.edu/microscopy/vol1992/iss6/28/). |
| Pile-up and event selection | Sum overlapping **four analog waveforms before** hold/ADC/ratios. For identical zero-delay shapes, unresolved pulses reduce to a charge-weighted centroid; for delayed or unequal shapes it need not be an energy centroid. Include trigger/hold busy time and afterglow. | The legacy energy-only grouping is a limiting reference; sweep rate, source mixtures, shaper and hold window. Compare both energy-window counts and spatial ghosts/mispositioning. |

### Avoiding duplicate measurement and inconsistent workspaces

Add an engine-domain immutable `InteractionSite` carrying XYZ, crystal ID, deposit and relative time, plus a history/decay ID. Preserve the original direct event API when disabled. Publish a measured readout record with four charges, raw Anger XY, total calibrated energy, LUT crystal ID, trigger/quality flags and arrival time. Raw site ownership must be copied or flattened before the detector reuses its buffer.

One engine measurement pipeline should produce the readout sequence used by all Studio consumers. `MeasurementStage` can adapt that pipeline; it must not apply scalar `FrontEndModel.Measure` again to an energy already fluctuated by photoelectron sampling. Pixel gain applies at each emitting site, sensor gain at each SiPM and output gain at each electrical channel. These are different calibration errors. Strip-ratio calibration must use exactly the selected readout/trigger/LUT response too.

Array-wide pile-up grouping, trigger rejection and index-addressed randomness need shared definitions. Retain the untriggered underlying arrival stream so rejecting an event does not shorten simulated time or reuse RNG indices. Stop/Continue must keep open four-channel holds/groups, look-ahead, readout RNG states and calibration immutable. Store LUT version/geometry/material/network/calibration seed and energy with each acquisition; a changed device requires Reset or a new calibration.

## Headless measurements

### Probe and assumptions

Measured against worktree HEAD `55af61591ab21eaa4c90c6dac74f91c14af8f765`, .NET SDK 9.0.311, Release. Temporary projects compile the current Core/Configuration/Masks/Detector/Decoding/Simulation source files directly, excluding repository bin/obj. No project build output goes into the repository. Three retained measurement projects are:

- `%TEMP%\gcam-rigreadout-refined-1919`: optical kernel, four-channel response, calibration/watershed, flood SVGs, geometry/energy/trigger sweep, paired 661.7 keV transport and runtime benchmark.
- `%TEMP%\gcam-rigreadout-profiles-1919`: an independent uniform-flood validation using the saved calibration LUTs/kernels; raw histogram CSVs, correctly located profile maxima, per-energy P/V and accepted-event confusion.
- `%TEMP%\gcam-rigreadout-energy-1919`: actual 122/662/1332 keV transport/localisation with the same saved LUTs/kernels. Zero-trigger images are explicitly reported as unavailable, never a valid peak at the decoder's empty-image tie location.

The exploratory `%TEMP%\gcam-rigreadout-c36df1c4d6c54961bc475562878bfca5` is retained for audit. Its specular-wall optical kernels provide a sensitivity comparison, but its original watershed lacked plateau-distance handling and its fixed P/V denominator was unsuitable. **Do not use its confusion/P/V/localisation as final evidence.** The corrected projects use a monotone flooding level with deterministic plateau distance and independent validation. A second profile pass finds maxima separately in the two halves of each neighbour profile rather than confusing a 662-keV calibration-marker height with a low-energy peak. There is no numerical valley floor in the reported final P/V.

Prototype parameters, all explicit:

| Item | Value and status |
|---|---|
| Crystals | 12×12 GAGG, depth 10 mm, geometry points in the tables below; LY 50 ph/keV, n=1.9. |
| Optical tracing | 100,000 photons per geometry/network replicate; uniform XYZ over one active crystal; isotropic emission; absorption length 100 mm; side/top R=.96, opaque T=0; Lambertian diffuse reflections in the refined study. R, bulk length and surface law are sensitivity assumptions. |
| Coupling/sensor | No guide; .05 mm direct coupling, index 1.46, Fresnel/TIR tracing; virtual 1:1 SiPM photosensitive footprint equals active crystal footprint. PDE=.40, not multiplied by microcell fill. Couplant dimensions/index, matched virtual device and GAGG-weighted PDE are assumptions. Optical probabilities include .40 PDE and explicit lost photons. |
| Photoelectrons | Poisson below 30 expected p.e.; Gaussian large-count approximation with ENF=1.20 above 30; common intrinsic fluctuation of 4% FWHM. This is an assumed effective charge model, not a calibrated avalanche-statistics model or exact compound-Poisson ENF at small counts. |
| Noise/digitisation | Independent sensor dark counts, baseline mean subtracted; rate 500 kcps × active sensor area/9 mm²; integration 200 ns. This area scaling is an assumption. Four independent output noise terms of 3 keV RMS, borrowed only as a **labelled noise sensitivity value** from `Waveform.NoiseKev`, not as a validated four-output noise value or tolerance. Unsigned amplitude quantisation 0–4000 keV at 14 bits; step .2442 keV (essentially the repository signed positive-range step). Negative measured amplitudes clamp to zero; this can bias low-energy ratios near corners. No finite-cell saturation or output RC waveform in this first charge prototype. |
| Networks | “ideal” = bilinear corner weights at each sensor center; “grid” = unit-conductance nearest-neighbour resistor graph, four grounded corner drains, other borders insulating; fractions solved by harmonic relaxation. Both are symmetric simulation candidates, not equivalent SCD circuits. |
| Holds / position | Isolated equal-shaped pulses, common normalized peak, synchronized four amplitudes; uncorrected corner Anger. This is the isolated-pulse peak-hold limit, not a measured dynamic hold implementation. |
| Calibration | 662-keV single-crystal deposits, 2,000 events/crystal; separate mean positions help locate markers, then local peak refinement, Gaussian σ=1 bin and watershed. Histogram 384×384, coordinates −1…1. **Geometry-assisted markers use calibration truth**; this does not establish blind automatic discovery of 144 spots. LUT reused across all energies. |
| Validation | Independent stream, 1,000 deposits/crystal/energy = 144,000 events per row. RNG xoshiro256**, seed 191900. Confusion is versus the known emitting crystal in these uniform single-site floods. |
| Transport/localisation | Current physical GAGG Compton table and finite-thickness mask; legacy optical crosstalk disabled; true total-deposit window ±15% of the selected line. 500,000 detector-biased photon proposals per point, seed 1919; detector transport stream seed 1920. Compare matched histories with importance-weighted floods. The four-channel sum's measured-energy window is not additionally applied here, so this isolates position/trigger, not a complete photopeak-throughput validation. |

The optical kernel averages uniform XYZ before runtime sampling. It does **not** preserve event-specific subcrystal position/DOI or optical variation with each gamma interaction's true XYZ. Actual gamma sites are the repository's pixel aggregates. Calibration excludes Compton multisites and is untriggered; in particular, it can contain crystals absent from a triggered 122-keV exposure. Finite cells/recovery, real ceramic gamma transport, RC loading/skew, dynamic peak-hold, afterglow and pile-up are not measured by these first prototypes. These omissions and assumed component parameters prevent hardware-performance validation or a default decision.

### Floods, ceramic width and single-crystal identification

Final table is from `profiles.csv` (independent validation); percentages are measured, not pass/fail tolerances. P/V is measured along 132 horizontal adjacent-crystal connecting profiles after σ=1-bin smoothing: strongest profile samples in each half define peaks, minimum between them defines valley. A half-profile maximum at the shared midpoint is separately counted as unresolved. Zero valleys mean **sampling/display censoring**, not physically infinite P/V.

| Network | Pitch / gap (mm) | Mis-ID 122 keV | Mis-ID 662 keV | Mis-ID 1332 keV | 122-keV median / minimum P/V | Noiseless edge/center spacing |
|---|---|---:|---:|---:|---:|---:|
| Ideal | 2 / .02 | 21.081% | 0 / 144,000 | 0 / 144,000 | 2.692 / 1.174 | .9934 |
| Ideal | 2 / .10 | 20.900% | 0 / 144,000 | 0 / 144,000 | 2.776 / 1.208 | .9986 |
| Ideal | 2 / .50 | 20.903% | 0 / 144,000 | 0 / 144,000 | 2.901 / 1.129 | .9999 |
| Ideal | **2 / 1.00** | **21.081%** | **0 / 144,000** | **0 / 144,000** | **2.898 / 1.220** | **1.0000** |
| Ideal | **3 / 1.00** | **20.845%** | **0 / 144,000** | **0 / 144,000** | **2.868 / 1.182** | **1.0000** |
| Ideal | 1 / .10 | 21.149% | 0 / 144,000 | 0 / 144,000 | 2.827 / 1.414 | .9972 |
| Grid | 2 / .02 | 60.382% | 2.209% | .0847% | 1.334 / 1.000 | .3141 |
| Grid | 2 / .10 | 60.481% | 2.065% | .0632% | 1.359 / 1.000 | .3144 |
| Grid | 2 / .50 | 60.174% | 2.141% | .0729% | 1.308 / 1.000 | .3142 |
| Grid | **2 / 1.00** | **60.260%** | **2.112%** | **.0764%** | **1.323 / 1.000** | **.3142** |
| Grid | **3 / 1.00** | **60.458%** | **2.164%** | **.0750%** | **1.350 / 1.000** | **.3142** |
| Grid | 1 / .10 | 60.401% | 2.106% | .0660% | 1.329 / 1.000 | .3141 |

Edge compression definition: the X separation between crystals x=0 and x=1 in row y=5, divided by x=5→6 separation in the same row, computed from the noiseless mean response. Thus the grid's edge spacing is compressed about **68.6%** in this location; the ideal divider is essentially linear. Finite calibration noise produces slightly different sample-mean ratios in `metrics.csv`; the deterministic ratios above avoid confusing estimation noise with compression. This is a local simulated spacing metric, not a whole-border average or hardware measurement.

At p2/g1, grid minimum P/V is **1.005 at 662 keV** and **2.444 at 1332 keV**. Respectively 55/132 and 107/132 valleys contain zero density at this finite histogram/filter resolution, so the very large/undefined median is not a useful physical claim. All 132 ideal-divider valleys are zero at 662/1332 keV. At 122 keV the grid has 13/132 midpoint maxima; the ideal divider has 3/132. The counts show where low-energy spot separation needs investigation rather than proving a universal P/V requirement.

The optical yield, rather than a hand-added blur, changes with active width: at p2 and wall .02/.10/.50/1.00 mm, mean detected p.e./keV is **5.464 / 5.128 / 4.287 / 3.014** in the ideal-divider optical replicate. Collection before PDE is respectively .2732/.2564/.2143/.1507, far below the repository's lumped .50 assumption for these long, narrow crystals and assumed boundaries. For p3/g1 it is 5.331 p.e./keV. Specular versus diffuse assumptions at p2/g1 give about **4.044 versus 3.014 p.e./keV**; that sensitivity alone prevents an absolute efficiency prediction. Optical table cost was .33–1.11 s per 100,000-ray replicate in this run, separately from event cost.

Measured single-site energy widths for p2/g1 ideal are **17.96 / 7.34 / 5.82%** at 122/662/1332 keV, from 2.3548 × sample σ/mean. These are effective Gaussian-equivalent widths, not fitted photopeak FWHMs or a validation of the assumed 4% floor. Wall-width effects on mis-ID are small in this charge model because isolated 1:1 signals and the assumed 3-keV output noise dominate ratios; this must not be generalized to transmitting walls or light guides.

For zero observed mis-IDs, the one-sided 95% counting upper bound is 1−.05^(1/144000) = **.00208%** under the simulated distribution. At 60% confusion, the conservative Bernoulli counting standard error is about .129 percentage points; at 2.11% about .038 points. These are counting precision only. One seed and assumed optics/electronics give no hardware uncertainty bound.

### Trigger selection is strongly spatial

Uniform single-site flood, p2/g1:

| Trigger | Ideal at 122 / 662 / 1332 keV | Grid at 122 / 662 / 1332 keV |
|---|---|---|
| OR, each output ≥100 keV | 7.17 / 100 / 100% | 3.36 / 100 / 100% |
| OR, each output ≥150 keV | 0 / 100 / 100% | .0007 / 100 / 100% in the refined scan; the rare 122-keV crossing is a noise fluctuation |
| AND, all outputs ≥100 keV | 0 / 8.48 / 35.31% | 0 / 54.97 / 91.67% |
| AND, all outputs ≥150 keV | 0 / .041 / 17.61% | 0 / 1.90 / 82.56% |
| Sum ≥100 keV | 99.20 / 100 / 100% | 99.04 / 100 / 100% |

OR100 accepted-event mis-ID in the independent 122-keV uniform flood is 15.51% ideal and 7.83% grid, versus 21.08% and 60.26% before the trigger. The apparent improvement is **selection of a few favourable positions**, not a better detector. The source/mask exposure below samples different crystal positions and has still lower acceptance. Trigger logic and channel calibration are therefore physics decisions, not UI details.

### Coded-aperture localisation with the same transport histories

The table uses exact 122/662/1332-keV source lines, repository mask rank7, 1 mm cell, 10 mm W slab, mosaic2×2, D=60/S=100 mm, repository auto reconstruction grid and Tent interpolation. Errors are Euclidean source-lateral millimetres, **one seed per point**, not RMS or validated tolerances. “Direct” is today's post-transport largest-deposit crystal, optical crosstalk zero. “Anger” uses the saved independent 662-keV LUT. “Triggered” adds OR100 after digitisation.

| Geometry/network | Energy | Source x,y (mm) | In-window histories / triggered | Direct error | Anger error | Triggered error |
|---|---:|---|---:|---:|---:|---:|
| p2/g1 ideal | 122 | 0,0 | 33,589 / 765 | .134 | .133 | .825 |
| p2/g1 grid | 122 | 0,0 | 33,589 / 128 | .134 | .192 | **6.413** |
| p2/g1 ideal | 122 | 3,2 | 33,351 / 1,038 | .307 | .306 | 1.493 |
| p2/g1 grid | 122 | 3,2 | 33,351 / 332 | .307 | .307 | **13.268** |
| p2/g1 ideal | 662 | 0,0 | 9,605 / 9,605 | .173 | .179 | .179 |
| p2/g1 grid | 662 | 0,0 | 9,605 / 9,605 | .173 | .825 | .825 |
| p2/g1 ideal | 662 | 3,2 | 9,181 / 9,181 | .309 | .307 | .307 |
| p2/g1 grid | 662 | 3,2 | 9,181 / 9,181 | .309 | .307 | .307 |
| p2/g1 ideal | 1332 | 0,0 | 5,172 / 5,172 | .199 | .825 | .825 |
| p2/g1 grid | 1332 | 0,0 | 5,172 / 5,172 | .199 | .825 | .825 |
| p2/g1 grid | 1332 | 3,2 | 4,989 / 4,989 | .308 | .306 | .306 |
| p3/g1 ideal | 122 | 0,0 | 28,813 / **0** | .436 | .446 | **Unavailable** |
| p3/g1 grid | 122 | 0,0 | 28,813 / **0** | .436 | .505 | **Unavailable** |
| p3/g1 grid | 122 | 3,2 | 23,468 / **0** | 1.074 | 1.148 | **Unavailable** |
| p3/g1 grid | 662 | 0,0 | 12,040 / 12,040 | .492 | .504 | .504 |
| p3/g1 grid | 662 | 3,2 | 11,256 / 11,256 | 1.150 | 1.167 | 1.167 |
| p3/g1 grid | 1332 | 0,0 | 8,050 / 8,050 | .534 | .545 | .545 |
| p3/g1 ideal | 1332 | 3,2 | 7,823 / 7,823 | 1.178 | 1.190 | 1.190 |
| p3/g1 grid | 1332 | 3,2 | 7,823 / 7,823 | 1.178 | **10.494** | **10.494** |

The full 24-row table is `energy-localisation.csv`. Histories are detector-biased proposals, **not an absolute physical count-rate prediction**. The large p3/grid/1332 error is a peak-selection failure in this one undersampled fixed-mask scenario. It needs a seed/matched-optics follow-up before assigning a failure probability. The zeros at p3/g1/122 arise because the illuminated portion of this finite mask excludes the high-corner-charge crystals that pass an OR100 uniform flood. It is incorrect to feed their empty image to the decoder and report its tie location as a source.

For p2/g1 on-axis at 662 keV, **50.86%** of accepted transport histories are multi-crystal; Anger differs from direct arg-max for **28.89% ideal / 33.59% grid**. At 122 keV, multisites are only 2.91%, while disagreements are 22.78% / 65.06% because low-energy electronic ratio noise dominates. At 1332 keV multisites rise to 64.93%, with 28.63% / 32.79% disagreement. This demonstrates why a single-site noiseless identity test cannot characterize the actual imaging task.

The separate 661.7-keV ceramic sweep (`localisation.csv`) gives, for ideal p2 and wall .02/.10/.50/1.00 mm, on-axis direct→Anger errors **.825→.825 / .825→.825 / .189→.825 / .185→.825 mm**; at source (3,2) they are **.311→.306 / .310→.307 / .310→.306 / .314→.310 mm**. On-axis grid p2/g1 is .185→.825 mm. No universal localisation penalty can be inferred from this small seed/grid sample. Fixed-mask geometry and peak ties are visible even in the direct reference.

### Runtime and retained visual artifacts

Warmed Release, 50,000 isolated 662-keV responses per geometry, charge generation + four outputs + LUT only: **15.1–32.8 microseconds/event** across the 12 cases. p2/g1 ideal/grid are **26.65 / 15.08 µs**; p3/g1 ideal/grid **24.95 / 31.46 µs**. The diagnostic sweep with nearest-center searching/histogram work takes longer and is not used as the runtime estimate. These are wall-clock measurements while other sessions may be active, not a hardware-qualified throughput guarantee. Two double arrays per response allocate about 1.23 kB/event; table/stack buffers and sparse covariance sampling can remove that cost in implementation. The small bounded optical kernels and 384² integer LUT are setup state; full ray tracing is excluded from event runtime.

Flood visuals are generated **from simulated event histograms**, not drawn reference spots: `bin/Release/net9.0/flood-{ideal|grid}-p{pitch}-g{gap}-E{122|662|1332}.svg` in the refined probe, logarithmic density with cyan calibration markers. The illustrated sweep cases are `flood-grid-p2-g1-E122.svg`, `...-E662.svg`, `...-E1332.svg`; the second geometry uses p3-g1. Calibration LUTs are adjacent `lut-*.csv`. The profiles probe retains raw `density-*.csv` histograms and `profiles.csv`, allowing independent plot/segmentation checks without a GUI.

Reproduce in dependency order (existing temp source projects are retained; no install required):

```powershell
dotnet run --project "$env:TEMP\gcam-rigreadout-refined-1919\Probe.csproj" -c Release -p:UseSharedCompilation=false
dotnet run --project "$env:TEMP\gcam-rigreadout-profiles-1919\Probe.csproj" -c Release -p:UseSharedCompilation=false
dotnet run --project "$env:TEMP\gcam-rigreadout-energy-1919\Probe.csproj" -c Release -p:UseSharedCompilation=false
```

Temporary sources retain ordinary unused-function/nullable/unreachable exploratory-code warnings; these are not production code or a clean repository build claim. The energy probe was corrected to return NaN for empty images and its existing CSV was correspondingly normalized; no nonempty-image number changed. The refined probe's setup CSV initialization was made repeatable after measurement; it does not change RNG draws or responses. The earlier invalid prototype results remain intact for audit, not overwritten into evidence.

## Proposed configuration and presets

Under `Detector.Readout`, use discriminated modes `DirectCrystal`, `FourOutputAnger` and, for comparison, `IndependentSipm`. Group options by physical role rather than a long flat list:

| Group | Proposed surface |
|---|---|
| Geometry | Existing pitch/gap/thickness + exterior wall; sensor Nx/Ny, pitch, active area, offset/rotation; derived crystal fill and sensor coverage. |
| Optics | Material/reflector/couplant/guide IDs; guide thickness; optical-table resolution, seed and convergence budget; explicit overrides labelled assumed/measured. |
| Sensor | Device/PDE spectral data, overvoltage, cell count/recovery, DCR per sensor, avalanche statistics and temperature. |
| Network | Topology ID, resistor graph/values, load and capacitance, tolerance seed, channel gain/pedestal/noise covariance; DC or impulse-response mode. |
| Digitisation/trigger | ADC/hold option, bits/range/ENOB; hold estimator/window/reset/droop/skew; four thresholds and units/calibration; logic/multiplicity; retain all four sampled values. |
| Position/calibration | Formula/orientation, correction None or calibration-derived; LUT method, flood energy/counts, Gaussian σ, marker policy, rejection policy, immutable calibration ID. |
| Study | Training/validation seeds, event/rate/source set, optical assumptions, held-out device realisations, paired-reference output and performance budget. |

Presets: `LegacyDirect` (exact disabled compatibility); `AngerPitch2Wall1` (p2/g1, matched virtual SiPM, unresolved parameters visible); `AngerPitch3Wall1` (p3/g1); `AngerThinWallSensitivity` (p2 with separately chosen wall); `CandidateSCD`, `CandidateDPC`, `CandidateDigital`. Do not call a candidate “best” until held-out measurements select it. Prefer typed validated configurations and preset expansion into values rather than magic switches that silently override a user's config. All study variants use `Clone()` plus targeted mutations.

## Default-selection experiment

Use a staged study rather than the Cartesian product of all knobs. First validate transport/light/network limiting cases, then screen geometry/coupling/network/trigger/LUT combinations with independent calibration. Refine the nondominated set with rate/temperature/misalignment and measured-parameter uncertainty. The conventional Anger preset remains in every comparison.

Report per-energy/per-crystal confusion matrices, trigger/rejection/unknown fractions, min/median P/V, edge versus center separation/spot width, calibrated energy bias/FWHM/nonlinearity, photopeak throughput, localisation RMS/bias/tails/failure and event cost/memory. Include 122, 662, 1332 keV single-site and actual transport histories; include co-located/mixed sources, Compton multisites and true/random coincidence. Compare at fixed emitted fluence **and** fixed accepted counts so efficiency and positional quality remain distinguishable. Run paired seeds with common transport, distinct optical/electronics seeds and held-out device realisations. Train calibration on separate data, including triggered calibration to reveal missing edge spots.

For optics, compare both fixed existing mask and matched coverage/sampling. Use source center, lateral edge and beyond-field controls; energy-specific truth is arg-max **only as the legacy comparison**, not a physical proof that Anger is wrong. Also evaluate the primary-interaction crystal and source-position task, because an energy centroid cannot determine the first interaction of every multi-site event.

Default selection requires explicit objectives and resource constraints (four ADCs required or independent channels allowed, acceptable count-rate range, mass/cost, localisation versus 122 keV sensitivity). Select a Pareto winner with confidence intervals from measured seed/device spread; no borrowed millimetre or percent tolerance. Current prototypes do **not** justify a new default.

## Studio, tests and implementation size

Detector workspace: explicit crystal/sensor geometry with active versus package areas, optical-response/SiPM charge grid, **continuous Anger calibration flood distinct from the crystal-count image**, LUT boundaries/markers, per-crystal confusion/acceptance/unknown diagnostics and calibration state. Provide an energy selector and guide/network/readout preset options. Reuse Core geometry/plot contracts and tokenised views; physics remains in the engine/Services.

Waveform workspace: A/B/C/D traces with common time axis, threshold lines, trigger/hold markers, saturation/busy state, summed energy and raw XY/LUT-ID readouts for the selected event. A simulated/assumed peak-hold estimator should be identified in its plot description. The selected acquired event must refer to the same measured record as Spectrum and Imaging; rate-study retiming remains explicitly separate.

Imaging, Spectrum and acquisition flood must use the selected LUT IDs and trigger/hold results consistently, including pile-up. Show direct/physical comparisons only as study views, not two secretly different acquisition interpretations. Core stays WPF-free; Services own engine adapters; views keep InitializeComponent-only code-behind. Desktop validation remains a later author-approved activity.

| Verification | What is guaranteed / how tolerance is obtained |
|---|---|
| Disabled compatibility | Exact original event sequence, deposits, times and indices for fixed seed; no new RNG draws. |
| Light accounting | Per photon/category conservation; table probabilities nonnegative and sum ≤1; missing probability explicitly loss. Optical MC standard errors and table refinement, not an arbitrary energy tolerance. |
| Spatial optical table | Agreement with direct photon tracing over held-out XYZ, wall/guide and boundary cases, bounded by measured Monte Carlo variance and interpolation convergence. |
| Network | Kirchhoff residual/charge conservation, reflection/rotation symmetry; DC fractions agree with independently solved small circuits. Saturation/ground loss distinguishable from conservation. |
| Four-channel covariance | Analytical wᵀ Cov(pe) w against sampled outputs; common intrinsic fluctuations cancel from ratios in single-site ideal limit; avoid four independent photon-budget samples. |
| Single-site ideal limit | 1:1 isolated response, infinite photons, no noise/threshold, correct calibration → exact original crystal ID. Multisite tests assert the derived centroid, **not** arg-max equivalence. |
| Trigger/hold/ADC | Explicit OR/AND logic, all four outputs retained, code/range/pedestal tests; common-time versus independent peaks; delayed two-pulse and channel-skew cases, no hand-tuned phase tolerance. |
| Calibration | Independent train/test, missing/merged peaks fail clearly; deterministic plateau handling/orientation; energy and geometry mismatch rejection; empirical binomial confusion intervals. |
| Acquisition integration | Identical measured records in Spectrum/Imaging/Waveform; partitioning and Stop/Continue event-for-event parity, retained open holds, cancellation and immutable LUT versions. |
| Source localisation | Paired multi-seed regression on each named geometry; limits come from observed spread/MC uncertainty and actual decoder grid, not theme 52/56's tolerances. |
| Performance | Table build separately from per-event runtime; warmed Release benchmark with allocation and multisite counts; no full optical ray transport on Studio's event hot path. |

Estimate, subject to planner scope: 8–12 engine/configuration units (~1,500–2,500 lines), 5–8 Studio Core/Services units plus Detector/Waveform views (~800–1,400 lines), and ~600–1,000 lines of targeted tests/study code. A full optical table builder + finite-cell/RC time-domain model adds roughly 1,000–2,000 lines beyond a DC charge/table prototype. Split into raw-sites/light validation; four-output measurement/calibration; paired study; Studio integration. Sharing only four scalar amplitudes is small; preserving all raw sites per acquisition can dominate memory, so retain flat site buffers and make full optical diagnostics opt-in.

## Open simulation decisions

1. For default selection, require four-output electronics or allow independent channels as a comparison? Specify the energy/rate range, localisation-versus-efficiency objective, resource constraints and which parameter combinations to sweep.
2. Include pre-optical XYZ/time interaction records and a shared measured-event stage? Define whether ceramic gamma transport and finite-cell recovery belong to the first implementation stage or are explicitly documented limitations.

## What was not run and approval requests

No repository solution build/test, engine implementation, Studio launch, GUI/UI automation, desktop tests, installation, deletion, process stop, persistent setting change, network upload or git state change was run. The temporary probe compiles current engine sources directly into its own SDK project; its output/build/restore files stay under the allowed temporary directory. Public primary sources were read through web search; no local credential or prohibited machine directory was inspected.

## APPROVAL REQUESTS

None. This review and its throwaway measurements require no irreversible commands. Implementation awaits the planner's decisions in the next turn; that is the agreed review workflow, not an irreversible-command approval request.
