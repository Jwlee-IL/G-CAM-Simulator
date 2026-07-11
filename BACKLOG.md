# Backlog

Deferred work, captured so we don't forget. Not in current scope. See
`AGENTS.findings.md` (themes 1–22) for everything that IS done.

## Open

### Core capability
- Multi-line isotope model + mixed field — **DONE (theme 26)**: `SimulationConfig.Sources[]` +
  `SourceConfig.Lines[]` + `MixedFieldSource`/`MixedFieldStudy` image a real mixed field (Cs+Co+Co-57)
  in one run and localize all sources. **Remaining next step**: **energy-window the mixed field at the
  PIPELINE level** to separate ISOTOPES (not just positions) — combine `MixedFieldSource` with the
  crystal-Compton detector + per-pixel stripping (themes 15–17), so the 662 window shows Cs at its
  position + Co downscatter contamination at Co's, from a true mixed source. Also: energy-dependent mask
  μ (currently one μ for all lines).

### RTL
- **Drive the cocotb testbench from the C# MC per-event stream** — the cocotb Icarus runner is now in
  place (theme 25); feed it real event times/energies instead of the synthetic stimulus. (The generic
  "cocotb wrapper" and "trapezoidal shaping in RTL" items are done — theme 25.)
- **CR-RC / cusp shaping variants** for comparison; fold the trapezoid's resolution-vs-rate into the
  material rate study.
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
