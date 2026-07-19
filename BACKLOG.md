# Backlog

Deferred work, captured so we don't forget. Not in current scope. See
`AGENTS.findings.md` (themes 1–46) for everything that IS done.

**Genuinely open**: the **Physical realism gaps** list below (Codex gap-review after theme 35) is now mostly DONE —
themes 36–46 cleared thermal drift + flood-field, pile-up sum continuum, mask fabrication tolerances, alignment/pose,
bad/dead/hot pixels, mask fluorescence/scatter, dead time, sub-cell peak interpolation, cascade summing, mask
forward-scatter imaging impact, and non-proportional intrinsic resolution. A 5-subsystem Codex **physics-realism audit**
(2026-07-18) surfaced the next tier — ✓ mask forward-scatter (45), ✓ non-proportionality (46); still open:
**MLEM/likelihood reconstruction** (vs cross-correlation peak-picking → depth & multi-source), **finite source size /
capsule self-attenuation**, decoder pixel-area/DOI PSF, thermal-drift DCR/PDE extension. Still small/open: per-channel
SiPM mismatch, intrinsic activity. Also open: exact Artix-7 Fmax in Vivado (deferred — install friction; `vivado_trap.tcl`
ready); integrate the theme-22 SiPM/thermal/shield productization models into the C# pipeline (design-only, user isn't
building it). Deferred by choice: mask TILT (pitch/yaw), mask WARPING, reflector MATERIAL (all 2nd-order).

## Open

### Core capability
- Multi-line isotope model + mixed field — **DONE (theme 26, Stages 1–3)**: `SimulationConfig.Sources[]`
  + `SourceConfig.Lines[]` + `MixedFieldSource`/`MixedFieldStudy` image a real mixed field, localize all
  sources (`mixedfield`), and — through the crystal-Compton detector + a 662 window in ONE run — separate
  Cs from Co-downscatter contamination by position (`mixediso`), recover the true Cs even for CO-LOCATED
  sources with **per-pixel Compton stripping** (`mixedstrip`), and attenuate each line by an
  **energy-dependent mask μ**. **Fully complete** — no remaining refinements.

### RTL
- **Drive the cocotb testbench from the C# MC per-event stream** — **DONE (theme 27)**: `EventStreamStudy`
  taps the crystal-Compton total-deposit spectrum per event + a Poisson arrival overlay
  (`montecarlo eventstream` → `rtl/event_stream.txt`); `rtl/event_stream.py` rasterizes it to an ADC
  waveform and `test_trap_shaper.mc_event_stream_matches_reference` drives BOTH shapers bit-for-bit
  (TESTS=3 PASS=3). `event_stream_study.py` shows energy recovery vs rate and surfaced the pole-zero
  baseline-walk (needs baseline restoration). Codex-verified (weighted resampling + baseline-index fixes).
- **CR-RC / cusp shaping variants** — **DONE (theme 31 Python compare + theme 33 RTL)**: `rtl/shapers.py`
  (CR-RC^4, cusp) + `shaper_compare.py` compare noise (cusp 1.02% < CR-RC 1.10% < trap 1.74% ENC) vs pile-up.
  `rtl/crrc_shaper.sv` implements CR-RC^4 in synthesizable RTL, cocotb bit-exact. Cusp stays the Python
  theoretical benchmark (a digital cusp is a ~19-tap FIR, rarely used in real FPGA DAQs).
- **Baseline restoration (BLR) in the RTL** — **DONE (theme 29)**: `rtl/baseline_restorer.sv` is a gated
  leaky-integrator BLR that cancels the Q8 pole-zero baseline walk (0 → ~-42000 ADC over the MC stream);
  cocotb-verified bit-exact + walk-removed (`test_blr.py`), demonstrated in `rtl/blr.png`. Codex-verified.
- **Exact Artix-7 Fmax in Vivado** — DEFERRED (Vivado licence/install friction). `rtl/vivado_trap.tcl`
  is ready to run (`vivado -mode batch -source vivado_trap.tcl -tclargs <top> <part> <period>`) whenever
  a Vivado is available. Meanwhile the open **nextpnr (Lattice ECP5)** flow gives a real STA Fmax as a
  proxy — direct vs pipelined shaper — see theme 25.

### Front-end integration
- **Realistic ADC front-end DONE (theme 30)**: `rtl/event_stream.rasterize` now models intrinsic (1/√E)
  resolution, finite rise / ballistic deficit, white electronic noise, and ADC clipping → realistic ~6.4%
  photopeak, resolution budget (electronic ⊕ intrinsic), `rtl/frontend.png`. Codex-verified.
- **Fold `rtl/frontend_model.py` into C# — DONE (theme 32)**: `FrontEndModel`/`FrontEndConfig` derive the
  energy resolution from the photoelectron budget (1/√E), opt-in via `detector.frontEnd`; `ComptonCrystalDetector`
  smears each deposit by R_tot(E) before the window. `montecarlo frontend` reproduces the Python table. Codex-verified.
- **DCR-as-background — DONE (theme 32)**: `FrontEndConfig.DarkCountRateHz`/`IntegrationTimeNs` add the SiPM
  dark-count parallel-noise term (R_dcr ∝ 1/E; negligible at the photopeak, concentrates at low energy). Codex-verified.
- **CR-RC / cusp shaping comparison** now has a real noise floor to be meaningful against (theme 30) — the
  natural next RTL step: sweep shaping time / compare filters on the realistic waveform for the SNR vs
  pile-up optimum (theme 30's frontend_study has the resolution machinery; a rigorous pile-up-inclusive
  photopeak-fit is the missing piece).

### Background (theme 28 — core model DONE, refinements open)
- **Ambient background is now a controllable opt-in** (`SimulationConfig.Background`, BSR knob) — uncoded
  uniform pedestal on the flood map (`BackgroundStudy`, `montecarlo background`) + merged Poisson events in the
  RTL stream (`EventStreamStudy`, `montecarlo eventstream <cfg> <rate> <max> <bgRatio>`). Default off; opt in
  per scenario/study (`samples/scenario_field.json`). Codex-verified. Open refinements:
  - **Unify antimask/shield onto the shared background helper** — DONE: `Background.RealizePixel` /
    `Background.Realize` now own the "source mean + uncoded pedestal → Poisson" primitive; ShieldStudy,
    MaskAntimaskStudy, and BackgroundStudy all route through it (behavior-preserving — byte-identical
    outputs, Codex-verified RNG order).
  - **Isotropic angular background** — DONE for the event-stream deposit pool (cosine-weighted downward
    hemisphere; isotropic photopeak fraction is slightly LOWER than normal incidence because oblique rays
    side-escape a finite array).
  - **Directional shield leak** — DONE (the pragmatic piece): `Background.SideLeakProfile` shapes the leaked
    background across the array (edge-weighted side walls + uniform rear, same total), and `ShieldStudy` shows
    the knee shifts 6→8 mm vs a flat pedestal → the uniform model is an optimistic lower bound. Full photon
    transport through the walls was deliberately SKIPPED (buildup + directional harm only bite where the
    conclusion is already robust; not worth the MC cost for a design tool).
  - **Structured background — spatial gradient DONE**: `Background.GradientProfile` (mean-1 linear ramp,
    any direction, Codex-verified exact) shapes the pedestal, opt-in via `RunSweep(gradientContrast, angle)`.
    Sim-revealed result: a gradient is *harmless while the source peak wins* (identical bias to a flat
    pedestal — the low-frequency residual doesn't move the argmax), then at the background knee it drags the
    estimate systematically toward its strong side (BSR 4: flat 1.1 mm → graded 9.2 mm) where a flat pedestal
    only fails randomly. The remaining structured case — a *directional discrete* background — is not new
    work: it is just another off-axis source in the mixed field (theme 26).

### Physical realism gaps (Codex gap-review after theme 35 — do one-per-session)
A completeness sweep (4 parallel Codex reviews + user) of what a REAL gamma camera has that GCAM does not yet
model. Ranked by impact; **★ = user-flagged / connects to the real rig**. Tackle one at a time.

**Localization (coded-aperture accuracy):**
- ~~**★ Mask fabrication tolerances**~~ — **DONE, theme 38** (`MaskFabrication` / `MaskFabricationStudy` / `montecarlo
  maskfab`). Seeded per-cell hole placement/size jitter + blocked cells + depth drill WANDER on the mask Transmit only
  (decoder stays ideal → forward-model mismatch). Conservative one-σ model averaged over masks → usability threshold
  σ ≲ 40 µm (RMS holds ~0.95 mm floor to 40 µm, 3.6 mm by 160 µm; efficiency 100→89 %). Warping (global bow) deferred.
- ~~**Mask–detector alignment / pose error**~~ — **DONE, theme 39** (`CodedApertureMask` pose / `AlignmentStudy` /
  `montecarlo align`). Rigid-body mask offset (x/y/z) + roll on the Transmit only, decoder stays ideal → systematic
  bias: in-plane offset amplified by (D+S)/D (~2.5 mm bias per 1 mm shift), radial spacing bias, tangential roll bias.
  In-plane registration is the driving tolerance. Pitch/yaw TILT (tilted-slab geometry) deferred.
- ~~**Bad / dead / hot pixels**~~ — **DONE, theme 40** (`DetectorDefects` / `DetectorDefectStudy` / `montecarlo
  defects`). Seeded dead (holes) + hot (spikes) maps applied to the flood, ideal decoder → localization pulled off;
  a known bad-pixel map repairs it by neighbour interpolation (discrete flood-field), holding the ~0.6 mm floor to 8 %
  bad pixels while raw degrades to ~1.2–1.8 mm.
- Sub-cell peak interpolation — DONE (theme 43). `PeakInterpolation` (tent/parabolic/Gaussian), default tent; beats the
  argmax step/√12 floor ~3–4× at coarse recon steps. Method picked by MC data (tent most robust across steps).
- NOTE — DOI/parallax is NOT missing: the flood already carries the depth-of-interaction lateral shift (the cascade
  interacts at depth), so off-axis resolution already degrades with crystal thickness. It's uncorrected, not unmodelled.

**Spectrum realism:**
- ~~**Random-coincidence pile-up SUM continuum in the spectrum**~~ — **DONE, theme 37** (`EventStreamStudy.ApplyPileUp`
  / `montecarlo pileup` / WPF Spectrum "Pile-up"). Merges the timed MC event stream within the shaper-derived resolving
  time (rise+2·tail) → a 662+662 sum peak + self-convolution continuum + rate-dependent throughput loss (99.6 %→84.7 %
  over 50 k→2 M cps); cross-checked vs the analytic `singles⊛singles` autoconvolution.
- ~~**Coincidence / cascade summing**~~ — **DONE, theme 44** (`DecayScheme` / `CascadeSummingStudy` / `montecarlo
  cascade`). Correlated per-decay gammas (Co-60 1173+1332, Na-22 back-to-back 511s) sum in the crystal → 2505 sum peak,
  rate-independent, log-log slope 2.04 = ∝ε²; Cs-137 null; Na-22 511+511 suppressed by back-to-back on a 1-sided detector.
- ~~**Mask tungsten fluorescence + Compton scatter**~~ — **DONE, theme 41** (`MaskSecondary` / `MaskSecondaryStudy` /
  `montecarlo masksec`). Closed-cell interactions emit secondaries: forward Compton scatter ≈9 % of the coded primary
  (its in-window tail is NOT window-rejected → imaging background), while W K X-rays (59/67 keV) are self-absorbed
  away (<0.2 % reach the detector). Focused slab MC, doesn't touch the coded Transmit.
- Scintillator K X-ray escape peak (GAGG Gd ~43 keV below the photopeak) — LOW-MED. A real satellite peak.
- Room / object / operator scatter — MED. We have the entrance+backing scatterer; full environmental scatter is more.

**Operational / calibration (★ real-rig pain points):**
- ~~**★ Thermal / gain drift DURING acquisition**~~ + ~~**★ Flood-field / uniformity correction**~~ — **DONE, theme 36**
  (`ThermalDrift` / `ThermalDriftStudy` / `montecarlo thermal`). Time-varying SiPM gain drift (ambient uniform +
  self-heating gradient, −0.7 %/°C) walks the photopeak out of the fixed per-crystal window → −9.4 % efficiency +
  6.7 % flood residual by end of acquisition; the calibration flood map cannot remove the time-varying part, only
  bias-comp can (holds 99.9 %). Confirmed it is an ENERGY-window (efficiency) problem, not a localization one.
- ~~Dead time / count-rate saturation (paralyzable / non-paralyzable) + live-time vs real-time~~ — **DONE, theme 42**
  (`DeadTime` / `DeadTimeStudy` / `montecarlo deadtime`). Both models on the timed MC stream: non-para saturates at
  1/τ, para peaks at R=1/τ then collapses; live fraction = recorded/true; MC matches the analytic m=R/(1+Rτ), R·exp(−Rτ).
- Per-channel SiPM/preamp gain·PDE·threshold mismatch (beyond crystal gain), microcell saturation, afterpulsing — MED.
- Non-proportionality as deposit-history-dependent (Compton-split vs photoelectric resolve differently) — MED.
- Intrinsic activity (LYSO Lu-176, LaBr₃ La-138) — LOW for GAGG (none); matters only if those scintillators are picked.

Recommended order next session: 36 thermal drift → 37 pile-up → 38 mask fabrication → 39 alignment/pose → 40 bad
pixels → 41 mask fluorescence/scatter — ALL DONE (through theme 44). Remaining small: per-channel SiPM mismatch,
intrinsic activity. Bigger next-tier (from the 2026-07-18 physics-realism audit): mask forward-transport, MLEM
reconstruction, non-proportional light yield, finite source size. Reflector MATERIAL +
mask TILT/WARPING stay deferred. (Dead time DONE theme 42; sub-cell peak interpolation DONE theme 43.)

### Productization (theme 22 — design-only; user is NOT building this now)
- Integrate the SiPM / thermal / gain-stabilization + shield models into the C# pipeline
  (currently Python design layers on top of the validated MC).
  (Codex cross-verification of all theme-22 pieces is now DONE — round 6, no bugs.)

## Done (summary — details in AGENTS.findings by theme)
- **1–11**: geometry & localization, FOV÷resolution=rank, cyclic-ghost, directional biasing, noise
  threshold, tungsten leakage, optimal thickness, crystal uniformity, detector array (Nyquist),
  crystal materials, RTL peak-detector, SiPM+ADC front-end.
- **12–17**: front-end energy trust (ballistic deficit), charge integration + pile-up rejection,
  multi-isotope + ADC dynamic range, crystal Compton (Argmax strategy + spatial separation),
  Compton stripping (spectral lever), combined per-pixel stripping inside the coded pipeline.
- **18–21**: source-distance (z) refocusing, depth-under-noise + joint lateral/depth, mask channel
  geometry (holes / focused channels), optimal mask size (cell pitch + open fraction).
- **22**: handheld productization — weight/volume/form, MC validation (2.45× sensitivity),
  camera–mask parallax, DAQ thermal + motion, SiPM gain thermal drift + stabilization ①②③, optimal
  5-sided shield thickness.
- **23**: mask-geometry follow-ups — tapered (hourglass) channels (wide-FOV fix for thick masks,
  edge/center 0.82→0.99 at ~4°), empirical open-fraction with random arrays (ρ≈0.5, MURA ~4× cleaner).
- **24**: full 3D (x,y,S) joint depth search (peak-prominence GLRT) — removes the alternating
  iteration's near-field coupling trap (lateral RMS 3.6→0.37 mm); far field stays physics-limited.
- **25**: trapezoidal shaper (Jordanov-Knoll) in RTL + first cocotb co-sim (bit-exact vs reference,
  TESTS=2 PASS=2); flat-top energy, pole-zero baseline restoration, pile-up separation.
