# AGENTS.md — Gcam (GCAM)

Guidance for AI agents and contributors working on this repository.

**Companion docs** (all in `docs/`, named `KEYWORD.Section.md` — see `AGENTS.Conventions.Docs`):

| Document | Covers |
|---|---|
| `AGENTS.Findings.md` | theme-organized results log — put new results there |
| `AGENTS.Backlog.md` | done + deferred work |
| `AGENTS.Conventions.Code.md` / `AGENTS.Conventions.Docs.md` | C# / XAML / test / commit style; doc naming, placement and sync rules |
| `AGENTS.Studio.md` | entry point for GCAM Studio (the MVVM viewer) — read before touching `src/Gcam.Studio*` |
| `DESIGN.Architecture.md`, `DESIGN.ViewLayer.md`, `DESIGN.Controls.md`, `DESIGN.Color.md`, `DESIGN.Layout.md`, `DESIGN.Typography.md` | GCAM Studio design |
| `PAPER.ko.md` | two-tier (expert / plain) physics write-up, Korean |

Folder maps: `rtl/README.md` (SystemVerilog front-end), `src/Gcam.Studio*/README.md`.

## What this is

A Monte Carlo simulator for a **coded-aperture gamma-source localization** system,
re-creating (in software) a real instrument: a **Cs-137** source emits 661.7 keV
photons through a **tungsten MURA coded-aperture mask**, which cast a coded shadow
onto a **12×12 pixelated scintillator (crystal) flood map**; cross-correlation
decoding of that image localizes the source. Historically the source position was
found by physically moving the isotope around; this project replaces that with
simulation so the geometry, decoder, and mask design can be swept programmatically.

Started 2026-07-08. It began as a hobby rebuild and grew into a 50-theme experiment
platform: FCFOV/ghost mapping and configuration optimization, then crystal-Compton
multi-isotope separation, depth estimation, MLEM reconstruction, a full detector/front-end
chain (with a SystemVerilog + cocotb RTL path and a WPF viewer app), and a physical-realism
modelling pass (thermal, pile-up, fabrication/alignment tolerances, non-proportionality, DOI).

## Solution layout

`Gcam.sln`, all projects target **net9.0** (C#). Shared build settings
in `Directory.Build.props` (nullable + implicit usings enabled).

| Project | Role |
|---|---|
| `src/…Core` | Domain primitives: `Vector3`, `Ray`, `Photon`, `IRandom`+`DefaultRandom`, `DetectorImage`, `Sampling`, `SubCellMethod`, result records, and the pipeline interfaces (`ISource`, `IMask`, `IDetector`, `IDecoder`) |
| `src/…Configuration` | `SimulationConfig` (+ `Source`/`Mask`/`Detector`/`Geometry`/`Decoder`/`FrontEnd`/`Background` sections; multi-source `Sources[]` + `SourceConfig.Lines[]`/`EmissionLine` for mixed fields) and `ConfigLoader` (System.Text.Json) |
| `src/…Masks` | `MaskPattern`, `MuraGenerator` (rank-p MURA, mosaic, decoding array), `CodedApertureMask` (`IMask`; focal/taper/pose/fabrication-error transforms), `MaskFabrication` (per-cell machining error) |
| `src/…Detector` | `CrystalDetector` / `ComptonCrystalDetector` (`IDetector` — ray→pixel scoring, Compton transport), `ComptonModel` (Klein-Nishina), `CrystalUniformity` (per-pixel gain/resolution + photopeak window), `FrontEndModel` (photoelectron-budget resolution + DCR), `Waveform` (native C# shaper, bit-exact to RTL), `ThermalDrift` (gain/DCR/PDE vs T), `NonProportionality` (electron-response curves), `DetectorDefects` (dead/hot maps + repair), `MaskSecondary` (W fluorescence/scatter), `EntranceAbsorber` (source capsule/window) |
| `src/…Decoding` | `CrossCorrelationDecoder` (`IDecoder`, ±1 back-projection + optional sub-cell interp), `MlemDecoder` (`IDecoder`, Poisson-likelihood ML-EM), `PeakInterpolation` (tent/parabolic/gaussian), `CodedApertureGeometry` |
| `src/…Simulation` | `SimulationRunner`, `ISimulationFactory`/`DefaultSimulationFactory` (+ `ComptonFactory`), sources (`IsotropicSource`, `DetectorBiasedSource`, `MixedFieldSource`), `DecayScheme` (per-decay correlated gammas), `EventStreamStudy` (timed MC stream → pile-up), and one study class per theme (`SourceSweep`, `ParameterScan`, `NoiseStudy`, `ThicknessStudy`, `UniformityStudy`, `ArrayStudy`, `ComptonStudy`, `DepthStudy`/`DepthDesignStudy`, `MaskGeometryStudy`, `BackgroundStudy`, `ShieldStudy`, `MixedFieldStudy`, `MaskAntimaskStudy`, plus the realism-gap studies: `ThermalDriftStudy`/`ThermalReadoutStudy`, `MaskFabricationStudy`, `AlignmentStudy`, `DetectorDefectStudy`, `MaskSecondaryStudy`/`MaskScatterStudy`, `DeadTime`/`DeadTimeStudy`, `SubCellStudy`, `CascadeSummingStudy`, `NonProportionalityStudy`, `FiniteSourceStudy`, `MlemStudy`, `DoiParallaxStudy`) |
| `src/…Wpf` | `Gcam.Wpf` (net9.0-windows, ScottPlot 5): interactive scene editor + Waveform/Imaging/Spectrum/Optics/Detector tabs; one acquisition drives all tabs. Can't be headless-tested — verify it *compiles* (theme 34–35) |
| `src/…Studio*` | MVVM rewrite of the viewer (imaging workflow first), layered so the boundaries are enforced by the compiler: **`Gcam.Studio.Core`** (net9.0, no WPF — models, `ISimulationService`, ViewModels on CommunityToolkit.Mvvm) → **`Gcam.Studio.Services`** (net9.0 — `SimulationService`, the only Studio layer that touches the engine; runs the MC off the UI thread via `SimulationRunner.Run(config, progress, ct)`) → **`Gcam.Studio`** (net9.0-windows — views, converters, DI composition root). Scene → config via `Configuration.SceneConfigBuilder`. `tests/Gcam.Studio.Tests` (net9.0) covers the ViewModels without a UI stack |
| `src/…Cli` | Console entrypoint `montecarlo`: single run + **~39** study sub-commands (`sweep`, `scan`, `noise`, the Compton/depth/mask-geometry/mixed-field/front-end set, and the realism-gap set `thermal`…`doi` — see Build/run for the full list) |
| `tests/…Tests` | xUnit harness — **156 test cases** (+ 7 cocotb in `rtl/`; Studio has its own 33 in `tests/Gcam.Studio.Tests`): MURA properties (`MuraGeneratorTests`) + end-to-end physics invariants (`PipelineTests`) + per-theme physics classes (localization, ghost, biasing-unbiased, stopping power, dead time, sub-cell, cascade, non-proportionality, MLEM, DOI, nuclide separation, Compton stripping, …) |

### Design principle
Everything is **data-driven**: one `SimulationConfig` (JSON) fully describes a
scenario. The engine composes pluggable strategy interfaces built by
`DefaultSimulationFactory` from the config, so experiments (e.g. swapping the
decoder) mean adding an implementation + a factory switch, not editing the runner.

## Pipeline & coordinate frame

```
ISource → Photon → IMask (tungsten transmit) → IDetector (12×12 score) → IDecoder → SourceEstimate
```

Coordinate frame (optical axis = z):
- **Detector** plane at `z = 0`, centered on the axis.
- **Mask** plane at `z = D` = `Geometry.MaskDetectorDistanceMm`.
- **Source** at `z = D + S`, `S = Geometry.SourceMaskDistanceMm`; lateral `(x, y)` from
  `Source.Position[0..1]` — **this lateral offset IS the "off-axis angle"**.
  `Source.Position[2]` is currently unused (source z is derived from geometry).
- Photons propagate toward −z. Distance falloff (1/r²), near-field magnification,
  and off-axis shadow shift are all **emergent from the geometry** — none are coded
  explicitly. They only activate once ray↔plane intersection runs (mask + detector).

## Key domain concepts

- **MURA** (Modified Uniformly Redundant Array): defined for **prime rank p**. The
  rig uses **rank 7**, tiled **2×2 (mosaic)** for cyclic decoding. Signature: first
  column closed, first row (bar [0,0]) open, open fraction ≈ 0.5.
- **FCFOV** (fully-coded field of view): the region where decoding is clean. In
  source-lateral space it is **one period wide**:
  `period = rank × cellPitch × (D+S)/D`, so `FCFOV_half = period/2`.
- **Ghost**: a source **outside** the FCFOV aliases (via cyclic decoding's assumed
  periodicity) to a peak on the **opposite side**. This is the real artifact the
  original hardware exhibited. `DecoderConfig.Cyclic=false` (finite-mask decoding)
  suppresses it.
- **Decoder grid**: `DefaultSimulationFactory` sizes the reconstruction grid to one
  FCFOV period by default (auto), so an in-FCFOV source gives a single peak. Override
  via `Decoder.ReconHalfExtentMm` / `ReconStepMm`.

## Build / run

```bash
dotnet build Gcam.sln -c Release
dotnet test  Gcam.sln   # 156 engine + 33 Studio cases: MURA properties + pipeline
                                       # physics invariants + one class per theme (dead time,
                                       # sub-cell, cascade, non-prop, MLEM, DOI, …). 7 cocotb tests in rtl/.

# single scenario → prints flood map + reconstruction + estimate
dotnet run --project src/Gcam.Cli -c Release -- samples/scenario.json

# source-position sweep → FCFOV map, cyclic vs non-cyclic, writes sweep_*.csv
dotnet run --project src/Gcam.Cli -c Release -- sweep samples/scenario.json

# configuration scan (rank × cell pitch × mask-detector distance) → scan.csv
dotnet run --project src/Gcam.Cli -c Release -- scan samples/scenario.json samples/scan.csv

# noise study: localization accuracy vs detected counts (Poisson) → noise.csv
dotnet run --project src/Gcam.Cli -c Release -- noise samples/scenario.json samples/noise.csv

# tungsten thickness optimization (use a wide-FOV config to see collimation) → thickness.csv
dotnet run --project src/Gcam.Cli -c Release -- thickness samples/scenario.json samples/thickness.csv

# crystal non-uniformity + flood correction → uniformity.csv
dotnet run --project src/Gcam.Cli -c Release -- uniformity samples/scenario.json samples/uniformity.csv

# detector array (pixel pitch/count) sampling sweep → array.csv
dotnet run --project src/Gcam.Cli -c Release -- array samples/scenario.json samples/array.csv

# crystal material presets (GAGG, CeBr3, LaBr3, LYSO, BGO, NaI, GAGG:Mg) in samples/materials/
dotnet run --project src/Gcam.Cli -c Release -- samples/materials/CeBr3.json

# single-mask vs mask/antimask vs additive background → antimask.csv
dotnet run --project src/Gcam.Cli -c Release -- antimask samples/scenario.json samples/antimask.csv

# --- physical-realism gap studies (themes 36–50; see docs/AGENTS.Findings.md) ---
# thermal drift during acquisition (ambient + self-heating) → window walk / flood residual → thermal_{off,on}.csv
dotnet run --project src/Gcam.Cli -c Release -- thermal  samples/scenario.json samples/thermal
# random-coincidence pile-up SUM continuum in the spectrum → pileup.csv
dotnet run --project src/Gcam.Cli -c Release -- pileup   samples/scenario.json samples/pileup.csv
# mask fabrication tolerances vs an ideal decoder (usability threshold) → maskfab.csv
dotnet run --project src/Gcam.Cli -c Release -- maskfab  samples/scenario.json samples/maskfab.csv
# mask–detector alignment / pose error (systematic bias) → align.csv
dotnet run --project src/Gcam.Cli -c Release -- align    samples/scenario.json samples/align.csv
# bad (dead/hot) detector pixels + bad-pixel-map repair → defects.csv
dotnet run --project src/Gcam.Cli -c Release -- defects  samples/scenario.json samples/defects.csv
# mask tungsten secondaries (Compton scatter + W K-fluorescence) → masksec.csv
dotnet run --project src/Gcam.Cli -c Release -- masksec  samples/scenario.json samples/masksec.csv
# counting-system dead time (non-paralyzable / paralyzable) + live fraction → deadtime.csv
dotnet run --project src/Gcam.Cli -c Release -- deadtime samples/scenario.json samples/deadtime.csv
# sub-cell peak interpolation (tent/parabolic/gaussian vs argmax floor) vs recon step → subcell.csv (+_trace.csv)
dotnet run --project src/Gcam.Cli -c Release -- subcell  samples/scenario.json samples/subcell.csv
# true (cascade) coincidence summing (Co-60 1173+1332→2505, ∝ε²); isotope from config → cascade.csv (+_spectrum.csv)
dotnet run --project src/Gcam.Cli -c Release -- cascade  samples/scenario_co60.json samples/cascade.csv
# mask forward-scatter folded into the coded image (contamination vs gap, window recovery) → maskscatter.csv (+_spectrum.csv)
dotnet run --project src/Gcam.Cli -c Release -- maskscatter samples/scenario.json samples/maskscatter.csv
# scintillator non-proportionality → intrinsic resolution from the cascade + nP(E) → nonprop.csv (+_spectrum662.csv)
dotnet run --project src/Gcam.Cli -c Release -- nonprop  samples/scenario.json samples/nonprop.csv
# finite source size (recon blur/washout) + capsule self-attenuation (662 vs low-E) → finitesrc.csv (+_capsule.csv)
dotnet run --project src/Gcam.Cli -c Release -- finitesrc samples/scenario.json samples/finitesrc.csv
# MLEM (Poisson-likelihood) vs cross-correlation: two-source resolving power, non-negativity → mlem.csv (+_profile.csv)
dotnet run --project src/Gcam.Cli -c Release -- mlem     samples/scenario.json samples/mlem.csv
# thermal DCR/PDE readout effects (dark rate, low-E vs photopeak resolution) vs temperature → thermalro.csv
dotnet run --project src/Gcam.Cli -c Release -- thermalro samples/scenario.json samples/thermalro.csv
# depth-of-interaction (DOI) parallax: off-axis localization shift vs thickness -> doi.csv
dotnet run --project src/Gcam.Cli -c Release -- doi      samples/scenario.json samples/doi.csv

# --- multi-isotope / Compton / depth / mask-geometry / front-end studies (themes 15–34) ---
# crystal-Compton multi-isotope separation (spatial + spectral energy-window) — theme 15–17
dotnet run --project src/Gcam.Cli -c Release -- compton       samples/scenario.json
dotnet run --project src/Gcam.Cli -c Release -- compton-strip samples/scenario.json
# mixed multi-isotope field: image + localize all sources, energy-window/stripping separation — theme 26
dotnet run --project src/Gcam.Cli -c Release -- mixedfield    samples/scenario.json
dotnet run --project src/Gcam.Cli -c Release -- mixediso      samples/scenario.json
dotnet run --project src/Gcam.Cli -c Release -- mixedstrip    samples/scenario.json
# depth (z) estimation: refocusing, joint x/y/z, 3D, and the depth-from-focus design study — themes 18–24, 34
dotnet run --project src/Gcam.Cli -c Release -- depth         samples/scenario.json
dotnet run --project src/Gcam.Cli -c Release -- depth-joint   samples/scenario.json
dotnet run --project src/Gcam.Cli -c Release -- depth3d       samples/scenario.json
dotnet run --project src/Gcam.Cli -c Release -- depthdesign   samples/scenario.json
# mask channel geometry / optimal size / tapered channels — themes 20–23
dotnet run --project src/Gcam.Cli -c Release -- maskgeo       samples/scenario.json
dotnet run --project src/Gcam.Cli -c Release -- masksize      samples/scenario.json
dotnet run --project src/Gcam.Cli -c Release -- masktaper     samples/scenario.json
# ambient background + directional shield leak — theme 28; antimask over a full scene
dotnet run --project src/Gcam.Cli -c Release -- background     samples/scenario.json
dotnet run --project src/Gcam.Cli -c Release -- shield         samples/scenario.json
dotnet run --project src/Gcam.Cli -c Release -- antimask-scene samples/scenario.json
# timed MC → RTL event stream (drives the cocotb shaper) — theme 27; physical front-end folded into C# — theme 32
dotnet run --project src/Gcam.Cli -c Release -- eventstream   samples/scenario.json
dotnet run --project src/Gcam.Cli -c Release -- frontend      samples/scenario.json
```

Sample scenarios in `samples/`: `scenario.json` (centered), `scenario_offaxis.json`
(inside FCFOV), `scenario_ghost.json` (outside FCFOV → ghost). CSV/PNG outputs also
land in `samples/`. Plots are generated with a standalone Python+matplotlib script
(not part of the C# build).

## Findings so far

The full, theme-organized results log with reproduce commands and artifacts is in
**`docs/AGENTS.Findings.md`** — keep new results there, not in this file. Headlines:

- **Localization + ghost**: sub-mm inside the FCFOV; off-axis beyond it aliases to an
  opposite-side ghost. Non-cyclic decoding ≈ doubles the usable area.
- **FOV ÷ resolution = rank** — the config-optimization law. Max-FOV pick rank 23 / pitch 1 /
  D 20 mm (~7× the original FOV); collapses when the detector can't hold one shadow period.
- **Tungsten ~8–10 mm** optimum (thin leaks, thick collimates). **Array**: ≳1 detector pixel
  per mask-cell shadow (Nyquist); ~2 is the sweet spot.
- **Crystal**: efficiency ∝ density (GAGG is dense; its weakness is resolution + afterglow →
  prefer GAGG:Ce,Mg or CeBr3). Position is uniformity-robust; **energy needs per-channel
  calibration**. GAGG's afterglow collapses rate capability at ~1 Mcps (RTL).
- **Mask/antimask**: redundant with no background; helps against **diffuse/common-mode**
  background — but a calibrated background subtraction matches it (physical mask rotation is
  then unnecessary), and it does **not** remove a directional coded interferer (themes 28, and
  the mask/antimask trade note).
- **Count threshold** ~25–50 detected counts; biasing gives ~100× fewer photons, unbiased.
- **Compton multi-isotope separation (15–17, 26)**: crystal-Compton spatial + spectral
  energy-window levers strip a high-energy isotope's downscatter out of a lower line's window —
  the real rig's long-unsolved Co-60-reads-as-Cs problem, cracked in sim.
- **Depth (18–24, 34)**: z is recoverable via near-field magnification + depth-from-focus, but
  weak far (∝z²); 3D range extends by rank, not cell pitch. WPF fuses it with a rangefinder input.
- **MLEM (48)**: Poisson-likelihood reconstruction is non-negative and resolves 2–3 mm source
  pairs that cross-correlation's ±1 sidelobes merge — the main reconstruction upgrade over peak-pick.
- **Physical-realism gaps (36–50)**: thermal drift is an energy-window (not position) issue;
  pile-up + cascade summing add spectral continua/sum-peaks; mask fabrication needs σ≲40 µm;
  alignment/pose is the dominant systematic tolerance; sub-cell interpolation beats the argmax
  floor. Most effects are honestly **small for this camera** — see `docs/AGENTS.Findings.md` for magnitudes.

## RTL front-end (`rtl/`)

A separate sub-project (not part of the .NET build): the ADC peak detector as real
**SystemVerilog**, simulated by **Icarus Verilog** (`scoop install iverilog`; run via
`rtl/run.sh`). Python generates a synthetic detector waveform (Poisson-timed Cs-137
pulses) → `adc.txt` → `peak_detector.sv` (baseline + threshold/peak-hold) → `peaks.txt`
→ Python scoring. It reconstructs the 662 keV photopeak at ~7% FWHM at low rate; at high
rate pile-up merges pulses and efficiency collapses (~44%). `material_rate_study.py`
sweeps count rate per scintillator (decay time from the C# presets + GAGG afterglow):
fast crystals (CeBr3) resist pile-up, and **GAGG's afterglow collapses it at ~1 Mcps**
while GAGG:Ce,Mg recovers — the rate-domain confirmation of the material recommendation.
`pixel_uniformity_study.py` runs the RTL per crystal (each with its own gain + decay):
per-crystal gain scatter smears the aggregate photopeak (7.4%→18% at 15% gain σ) and
per-channel gain calibration restores it — so **energy needs calibration even though
position is uniformity-robust** (finding 8). See `rtl/README.md`.

**Now wired to the C# MC and cocotb** (themes 27, 31, 33): `EventStreamStudy` (`montecarlo
eventstream`) exports a Poisson-timed MC arrival stream that drives the RTL shaper, and the
`crrc_shaper.sv` (CR-RC⁴ pole-zero + 4 RC low-passes) and trapezoidal front-ends are
verified **bit-exact against the native C# `Waveform`** by a **cocotb** harness (7 tests
across `rtl/test_*.py`, python.org 3.13 + Icarus). The cusp shaper stays a Python benchmark
by choice (a digital cusp is a rare ~19-tap FIR). ModelSim/Vivado not used (Icarus + nextpnr
ECP5 as the Fmax proxy; `rtl/vivado_trap.tcl` ready for exact Artix-7 whenever installed).

## Notes & gotchas

- Emission uses **directional biasing** by default (`Source.DirectionalBiasing`):
  `DetectorBiasedSource` aims photons at the detector rectangle and weights them by
  `A·cosθ/(4π r²)`, giving an **unbiased** estimate of the 4π result with ~100× fewer
  photons (validated: efficiency matches 4π within ~1%). Set `DirectionalBiasing:false`
  for a plain 4π `IsotropicSource` check. Sensitivity is reported as geometric
  **efficiency** (`DetectedWeight/emitted`), which is invariant across both modes.
- The mask is a **finite-thickness slab** (`CodedApertureMask` ray-marches 12 sub-steps
  through it). Closed cells / edges attenuate `exp(-mu·path)` (`Mask.LinearAttenuationPerMm`
  default 0.178 ≈ 10 mm W at 662 keV, ~17% leak); at oblique angles open cells become
  channels that clip into neighbours → collimation. Set thickness=small for leak-limited,
  large for collimation-limited behaviour.
- **Cloning configs**: studies vary parameters by `baseConfig.Clone()` (a JSON deep copy)
  then mutating one or two fields. Do NOT hand-write `new SimulationConfig { ... }` clones —
  a Codex review found several that silently dropped fields (mask attenuation, crystal
  material) and reverted them to defaults. Clone + mutate instead.
- Accepted minor limitations (documented, not bugs): `PhotonsDetected` is a raw
  geometric-hit count that over-counts non-ideal crystals — use `DetectedWeight` for the
  physical count; `Sampling.Poisson` uses a Gaussian approximation for λ ≥ 30.
