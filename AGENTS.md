# AGENTS.md — Genoray.MonteCarlo (GCAM)

Guidance for AI agents and contributors working on this repository.

**Companion docs:** `AGENTS.findings.md` (theme-organized results log — put new results
there), `BACKLOG.md` (done + deferred work), `rtl/README.md` (SystemVerilog front-end).

## What this is

A Monte Carlo simulator for a **coded-aperture gamma-source localization** system,
re-creating (in software) a real instrument: a **Cs-137** source emits 661.7 keV
photons through a **tungsten MURA coded-aperture mask**, which cast a coded shadow
onto a **12×12 pixelated scintillator (crystal) flood map**; cross-correlation
decoding of that image localizes the source. Historically the source position was
found by physically moving the isotope around; this project replaces that with
simulation so the geometry, decoder, and mask design can be swept programmatically.

Started 2026-07-08. It began as a hobby rebuild and grew into an experiment platform
(FCFOV mapping, ghost-artifact study, configuration optimization).

## Solution layout

`Genoray.MonteCarlo.sln`, all projects target **net9.0** (C#). Shared build settings
in `Directory.Build.props` (nullable + implicit usings enabled).

| Project | Role |
|---|---|
| `src/…Core` | Domain primitives: `Vector3`, `Ray`, `Photon`, `IRandom`+`DefaultRandom`, `DetectorImage`, result records, and the pipeline interfaces (`ISource`, `IMask`, `IDetector`, `IDecoder`) |
| `src/…Configuration` | `SimulationConfig` (+ `Source`/`Mask`/`Detector`/`Geometry`/`Decoder` sections) and `ConfigLoader` (System.Text.Json) |
| `src/…Masks` | `MaskPattern`, `MuraGenerator` (rank-p MURA, mosaic, decoding array), `CodedApertureMask` (`IMask`) |
| `src/…Detector` | `CrystalDetector` (`IDetector`) — ray→pixel scoring; `CrystalUniformity` (per-pixel gain/resolution sensitivity map) |
| `src/…Decoding` | `CrossCorrelationDecoder` (`IDecoder`) + `CodedApertureGeometry` |
| `src/…Simulation` | `SimulationRunner`, `ISimulationFactory`/`DefaultSimulationFactory`, `IsotropicSource`, `DetectorBiasedSource`, `SourceSweep`, `ParameterScan`, `NoiseStudy`, `ThicknessStudy`, `UniformityStudy`, `ArrayStudy` |
| `src/…Cli` | Console entrypoint: single run, `sweep`, `scan`, `noise`, `thickness`, `uniformity`, `array` |
| `tests/…Tests` | xUnit harness — MURA properties (`MuraGeneratorTests`) + end-to-end physics invariants (`PipelineTests`: localization, ghost, biasing-unbiased, stopping power, ghost suppression) |

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
dotnet build Genoray.MonteCarlo.sln -c Release
dotnet test  Genoray.MonteCarlo.sln   # harness: MURA properties + pipeline physics invariants
                                       # (localization, ghost, biasing-unbiased, stopping power, ghost suppression)

# single scenario → prints flood map + reconstruction + estimate
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- samples/scenario.json

# source-position sweep → FCFOV map, cyclic vs non-cyclic, writes sweep_*.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- sweep samples/scenario.json

# configuration scan (rank × cell pitch × mask-detector distance) → scan.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- scan samples/scenario.json samples/scan.csv

# noise study: localization accuracy vs detected counts (Poisson) → noise.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- noise samples/scenario.json samples/noise.csv

# tungsten thickness optimization (use a wide-FOV config to see collimation) → thickness.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- thickness samples/scenario.json samples/thickness.csv

# crystal non-uniformity + flood correction → uniformity.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- uniformity samples/scenario.json samples/uniformity.csv

# detector array (pixel pitch/count) sampling sweep → array.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- array samples/scenario.json samples/array.csv

# crystal material presets (GAGG, CeBr3, LaBr3, LYSO, BGO, NaI, GAGG:Mg) in samples/materials/
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- samples/materials/CeBr3.json

# single-mask vs mask/antimask vs additive background → antimask.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- antimask samples/scenario.json samples/antimask.csv

# --- physical-realism gap studies (themes 36–46; see AGENTS.findings.md) ---
# thermal drift during acquisition (ambient + self-heating) → window walk / flood residual → thermal_{off,on}.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- thermal  samples/scenario.json samples/thermal
# random-coincidence pile-up SUM continuum in the spectrum → pileup.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- pileup   samples/scenario.json samples/pileup.csv
# mask fabrication tolerances vs an ideal decoder (usability threshold) → maskfab.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- maskfab  samples/scenario.json samples/maskfab.csv
# mask–detector alignment / pose error (systematic bias) → align.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- align    samples/scenario.json samples/align.csv
# bad (dead/hot) detector pixels + bad-pixel-map repair → defects.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- defects  samples/scenario.json samples/defects.csv
# mask tungsten secondaries (Compton scatter + W K-fluorescence) → masksec.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- masksec  samples/scenario.json samples/masksec.csv
# counting-system dead time (non-paralyzable / paralyzable) + live fraction → deadtime.csv
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- deadtime samples/scenario.json samples/deadtime.csv
# sub-cell peak interpolation (tent/parabolic/gaussian vs argmax floor) vs recon step → subcell.csv (+_trace.csv)
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- subcell  samples/scenario.json samples/subcell.csv
# true (cascade) coincidence summing (Co-60 1173+1332→2505, ∝ε²); isotope from config → cascade.csv (+_spectrum.csv)
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- cascade  samples/scenario_co60.json samples/cascade.csv
# mask forward-scatter folded into the coded image (contamination vs gap, window recovery) → maskscatter.csv (+_spectrum.csv)
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- maskscatter samples/scenario.json samples/maskscatter.csv
# scintillator non-proportionality → intrinsic resolution from the cascade + nP(E) → nonprop.csv (+_spectrum662.csv)
dotnet run --project src/Genoray.MonteCarlo.Cli -c Release -- nonprop  samples/scenario.json samples/nonprop.csv
```

Sample scenarios in `samples/`: `scenario.json` (centered), `scenario_offaxis.json`
(inside FCFOV), `scenario_ghost.json` (outside FCFOV → ghost). CSV/PNG outputs also
land in `samples/`. Plots are generated with a standalone Python+matplotlib script
(not part of the C# build).

## Findings so far

The full, theme-organized results log with reproduce commands and artifacts is in
**`AGENTS.findings.md`** — keep new results there, not in this file. Headlines:

- **Localization + ghost**: sub-mm inside the FCFOV; off-axis beyond it aliases to an
  opposite-side ghost. Non-cyclic decoding ≈ doubles the usable area.
- **FOV ÷ resolution = rank** — the config-optimization law. Max-FOV pick rank 23 / pitch 1 /
  D 20 mm (~7× the original FOV); collapses when the detector can't hold one shadow period.
- **Tungsten ~8–10 mm** optimum (thin leaks, thick collimates). **Array**: ≳1 detector pixel
  per mask-cell shadow (Nyquist); ~2 is the sweet spot.
- **Crystal**: efficiency ∝ density (GAGG is dense; its weakness is resolution + afterglow →
  prefer GAGG:Ce,Mg or CeBr3). Position is uniformity-robust; **energy needs per-channel
  calibration**. GAGG's afterglow collapses rate capability at ~1 Mcps (RTL).
- **Mask/antimask**: redundant with no background, essential once background is significant.
- **Count threshold** ~25–50 detected counts; biasing gives ~100× fewer photons, unbiased.

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
position is uniformity-robust** (finding 8). See `rtl/README.md`. Not yet wired to the
C# MC event stream or cocotb/ModelSim.

## Notes & gotchas

- `Mask.dat` (repo root) is **base64-wrapped, encrypted/high-entropy** — NOT a
  readable 0/1 pattern. Don't try to parse it; regenerate the mask via
  `MuraGenerator` instead (rank 7 → identical pattern, deterministically).
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
