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
- **CR-RC / cusp shaping variants** — **DONE (theme 31)**: `rtl/shapers.py` (CR-RC^4, cusp) + `shaper_compare.py`
  compare noise (cusp 1.02% < CR-RC 1.10% < trap 1.74% ENC) vs pile-up (∼support width; the trapezoid's flat top
  is a ballistic-deficit feature, not a rate advantage). Codex-verified. `rtl/shaper_compare.png`.
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
  Still open: model **DCR-as-background** (the SiPM dark-count term isn't in the C# noise yet).
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
  - **Structured background** (a second directional source / non-uniform field) — currently only the diffuse
    uniform component; a directional background is really just another source in the mixed field (theme 26).

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
