# Backlog

Deferred work, captured so we don't forget. Not in current scope. See
`AGENTS.findings.md` (themes 1–22) for everything that IS done.

## Open

### Core capability
- **Multi-line isotope model in C# `SourceConfig`** (multi-line + `cascadeCoincident`) so the MC
  emits a real *mixed-isotope field* through the coded aperture. The combined lever (theme 17)
  currently sums per-line runs, not a true mixed-field source. **Highest-value open item.**

### Mask geometry
- **Tapered / diverging holes** (accept the FOV angular cone) — the actual "wider FOV" fix for
  thick masks, distinct from theme-20 point-focusing (which narrows FOV).
- **Empirical open-fraction sweep with random arrays** + a double-valued balanced decode — confirm
  the analytical √(ρ(1−ρ)) optimum (theme 21 was analytical) and show MURA beating random arrays
  on sidelobes.

### Depth / RTL
- **Full 3D (x,y,S) likelihood** depth search instead of the alternating iteration (near-field
  off-axis depth is coupling-noise-limited, theme 19).
- **cocotb / ModelSim wrapper** + drive the RTL from the C# MC's per-event stream.
- **Trapezoidal / CR-RC shaping in RTL** (validated in Python) as the noise-optimal refinement.

### Front-end integration
- Fold `rtl/frontend_model.py` (photoelectron budget → energy resolution) into the C#
  `EnergyResolutionFwhm` derivation, and model DCR-as-background.

### Productization (theme 22 — design-only; user is NOT building this now)
- Integrate the SiPM / thermal / gain-stabilization + shield models into the C# pipeline
  (currently Python design layers on top of the validated MC).
- `ShieldStudy` uses a uniform additive background; a directional / isotropic real background
  source through the geometry would be more precise than the analytical leak term.
- **Deferred Codex cross-verification** of the remaining low-risk theme-22 pieces — weight /
  volume / form-factor, camera parallax, DAQ thermal + motion, and the 2.45× MC-validation
  numbers. (Round 4 verified the higher-risk ShieldStudy + SiPM-thermal + combined-lever; these
  are simple geometry / arithmetic, so they were left for a later quota.)

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
