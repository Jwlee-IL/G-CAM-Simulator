# Backlog

Deferred work, captured so we don't forget. Not in current scope. See
`AGENTS.findings.md` (themes 1–22) for everything that IS done.

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
- **CR-RC / cusp shaping variants** for comparison; fold the trapezoid's resolution-vs-rate into the
  material rate study.
- **Baseline restoration (BLR) in the RTL** — theme 27 showed the Q8 pole-zero leaves a slow baseline walk
  over a long pulse train; the study reads the flat top relative to the local baseline, but the RTL itself
  has no BLR. A gated-baseline / moving-average restorer in `trapezoidal_shaper.sv` would make the absolute
  output usable, not just the relative read.
- **Exact Artix-7 Fmax in Vivado** — DEFERRED (Vivado licence/install friction). `rtl/vivado_trap.tcl`
  is ready to run (`vivado -mode batch -source vivado_trap.tcl -tclargs <top> <part> <period>`) whenever
  a Vivado is available. Meanwhile the open **nextpnr (Lattice ECP5)** flow gives a real STA Fmax as a
  proxy — direct vs pipelined shaper — see theme 25.

### Front-end integration
- Fold `rtl/frontend_model.py` (photoelectron budget → energy resolution) into the C#
  `EnergyResolutionFwhm` derivation, and model DCR-as-background.

### Productization (theme 22 — design-only; user is NOT building this now)
- Integrate the SiPM / thermal / gain-stabilization + shield models into the C# pipeline
  (currently Python design layers on top of the validated MC).
- `ShieldStudy` uses a uniform additive background; a directional / isotropic real background
  source through the geometry would be more precise than the analytical leak term.
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
