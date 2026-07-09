# Backlog

Deferred work, captured so we don't forget. Not in current scope.

Everything captured here has been done — the backlog is currently empty.

**Done:**
- Directional biasing (detector-area importance sampling) — `DetectorBiasedSource`,
  on by default (`Source.DirectionalBiasing`); ~100× fewer photons, validated unbiased.
- Noise / statistics study — `NoiseStudy` + `noise` CLI command. `SourceConfig` has
  `ActivityBq` / `AcquisitionTimeSeconds` / `BranchingRatio` (0.851); Poisson
  realizations of the biased mean map → accuracy vs detected counts. Found a
  ~25–50 count threshold below which localization collapses.
- Tungsten partial transmission — closed cells transmit `exp(-mu·path)`,
  `Mask.LinearAttenuationPerMm` default 0.178 (~17% leak for 10 mm W at 662 keV).
  Adds ~17% counts, lowers contrast to ~0.83×, but MURA rejects the near-DC leakage
  so localization is barely affected.

- RTL peak-detector co-simulation — **first version done** in `rtl/` (SystemVerilog
  `peak_detector.sv` + Icarus testbench, Python stimulus/scoring). Reconstructs the
  Cs-137 photopeak; shows pile-up loss at high rate. Remaining: cocotb/ModelSim
  wrapper, drive it from the C# MC's per-event stream, trapezoidal shaping filter.

- Optimal tungsten thickness — **done**: mask is a ray-marched 3D slab
  (`CodedApertureMask`), `thickness` CLI command sweeps it. Optimum ~8–10 mm (thin
  leaks, thick collimates off-axis).
- Crystal non-uniformity (gain + energy resolution) — **done**: `CrystalUniformity`
  (per-pixel sensitivity from gain σ, gradient, energy-window acceptance) + `uniformity`
  CLI command with flood correction. Finding: coded-aperture is intrinsically robust.

- Crystal material + array — **done**: stopping-power efficiency (`CrystalAttenuationPerMm`),
  material presets in `samples/materials/`, `array` sampling sweep. Efficiency ∝ density;
  array needs ≳1 pixel per mask-cell shadow (Nyquist), ~2 is the sweet spot.

- Material decay time → RTL pile-up — **done**: `rtl/material_rate_study.py` sweeps
  count rate per scintillator (decay + GAGG afterglow). GAGG collapses at ~1 Mcps
  (afterglow); GAGG:Mg / CeBr3 hold up. Confirms the material recommendation.

- Per-crystal pulse-shape / gain non-uniformity into the RTL — **done**:
  `rtl/pixel_uniformity_study.py` runs the RTL per crystal (own gain + decay). Per-crystal
  gain scatter smears the photopeak (7.4%→18% at 15% σ); per-channel gain calibration
  restores it. Energy needs calibration even though coded-aperture position is robust.

- SiPM + ADC front-end characteristics — **done (analysis)**: `rtl/frontend_model.py`
  (photoelectron budget → energy resolution vs PDE per crystal). Finding: at typical MPPC
  PDE (~45%) most crystals are already crystal-limited → more PDE is wasted; spend on
  crystal choice + spectral match + per-channel calibration + low DCR (for weak sources).
  ADC 12–14 bit is plenty; sample rate matched to crystal decay. Deeper follow-up (not
  scoped): fold this into the C# `EnergyResolutionFwhm` derivation and DCR-as-background.

- Front-end energy trust (Phase A) — **done**: `rtl/ballistic_deficit_study.py` characterizes
  the baseline peak-hold's failures (saturation/dynamic range, overlap→fake sum line, rate
  drift); `rtl/integrating_peak_detector.sv` + `shaping_pileup_study.py` add charge integration
  + pile-up rejection (fast derivative arrival detector) → ~halves the fake sum-tail at the cost
  of throughput, window↔decay tradeoff. AGENTS.findings themes 12–13.

- Multi-isotope discrimination + dynamic range (Phase B) — **done**: `rtl/multi_isotope_study.py`
  + isotope presets `samples/isotopes/*.json` (multi-line + cascade + KN Compton continuum).
  Findings (theme 14): set the ADC gain to the highest line (12-bit single gain covers 122→1332
  keV, no low-line penalty; dual-gain only if bit-starved); cascade sum (rate-linear) vs random
  pile-up (rate²) separated by activity sweep, crossover ~200 kdecays/s.

- Crystal-internal Compton scatter (the original ask) — **done**: `ComptonModel` (Kahn KN sampler,
  energy-dependent photofraction/attenuation) + `ComptonCrystalDetector` (multi-pixel cascade +
  positioning strategy), CLI `montecarlo compton`, `ComptonTests`. Findings (theme 15): **Argmax**
  (total-energy window + max-deposit pixel) beats the old per-pixel LLD/ULD window — recovers
  Compton-split events (24%→38% counts) at better localization; and the multi-isotope contamination
  (Co-60 downscatter into the Cs 662 window) is separated *spatially* by the coded decode (imaged at
  Co-60's position, not Cs-137's) — solves the user's long-unsolved "valid-count-is-really-noise".

- Compton stripping (spectral lever) — **done**: `rtl/compton_stripping_study.py`. Subtract each
  line's modelled KN continuum top-down to recover net counts. Recovers well-separated lines (Cs-137
  662: raw +39% → stripped −5%) but accumulates error top-down (close Co-60 doublet leak → Co-57
  over-corrected). Complementary to theme-15 spatial separation, which is more robust for close/
  cascade/overlapping lines. AGENTS.findings theme 16.

## Next (front-end thread)
- Fold the multi-line isotope model into C# `SourceConfig` (multi-line + cascadeCoincident) so the MC
  can emit a real multi-isotope field through the coded aperture (multi-source localization + the
  Compton contamination test at the pipeline level, not just per-line runs summed).
- Combined lever (spectral + spatial) — **done**: `ComptonStudy.RunStripping` + CLI
  `montecarlo compton-strip`. Per-pixel Compton stripping (subtract R × the pixel's Co-photopeak
  count from its 662 window), then decode. Recovers true Cs count (+74–76% raw → ~0%), removes the
  Co ghost (separated) and the inflation (co-located — which spatial decode alone can't). Theme 17.
- Depth/z estimation — **done**: `DepthStudy` + CLI `montecarlo depth`. Recovers source distance by
  refocusing (decode at a range of assumed S, on-axis correlation peaks at true S). Near-field
  accurate, far-field degrades (dM/dS = −D/S²). AGENTS.findings theme 18.

- Mask channel geometry (hole size + focused/converging channels) — **done**: `CodedApertureMask`
  `HoleFraction`/`FocalDistanceMm` + `MaskGeometryStudy` + CLI `montecarlo maskgeo`. Smaller holes are
  strictly worse for a coded aperture; focused channels are a focal-point concentrator (+35% on-axis at
  focal, but narrower FOV + depth-of-field). AGENTS.findings theme 20.

## Still open
- Fold multi-line isotope model into C# `SourceConfig` (the combined lever uses per-line runs summed,
  not a real mixed-field source).
- Tapered/diverging holes (accept the FOV angular cone) as the geometry that could uniformly reduce
  off-axis collimation for thick masks — the actual "wider FOV" fix, distinct from point-focusing.
- Depth under Poisson noise + joint lateral+depth — **done**: `DepthStudy.RunNoisyDepth` /
  `RunNoisyJoint` + CLI `montecarlo depth-joint`. Near-field depth → sub-mm, far-field floors ~10mm;
  joint recovers lateral (x,y) to ~mm even while z is uncertain (coded aperture = strong lateral
  localizer, weak rangefinder). AGENTS.findings theme 19.
- cocotb/ModelSim wrapper; trapezoidal shaping in RTL.
- Improve joint depth coupling (near-field off-axis depth is coupling-noise-limited); a full 3D
  (x,y,S) likelihood search instead of the alternating iteration.

## Ideas for later (not yet scoped)
- Depth/z estimation (currently source z is assumed known).
- cocotb/ModelSim wrapper + drive the RTL from the C# MC's per-event stream.
- Trapezoidal/CR-RC shaping in RTL (validated in Python) as the noise-optimal refinement.
