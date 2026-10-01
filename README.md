# Gcam

A Monte Carlo simulator for **coded-aperture gamma-source localization**, written in
C# / .NET 9. It re-creates, in software, a real instrument: a **Cs-137** source emits
661.7 keV photons through a **tungsten MURA coded-aperture mask**, which casts a coded
shadow onto a **12×12 pixelated scintillator flood map**; cross-correlation (or MLEM)
decoding of that image localizes the source.

Historically the source position was found by physically moving the isotope around;
this project replaces that with simulation so the geometry, decoder, and mask design
can be swept programmatically. It grew into a 50-theme experiment platform covering
FCFOV/ghost mapping, crystal-Compton multi-isotope separation, depth-of-interaction,
a full detector/front-end chain (with a SystemVerilog + cocotb RTL path and a WPF
viewer), and a physical-realism modelling pass.

## Build & run

```bash
dotnet build Gcam.sln -c Release
dotnet test  Gcam.sln            # unit + physics-invariant tests

# single scenario -> flood map + reconstruction + source estimate
dotnet run --project src/Gcam.Cli -c Release -- samples/scenario.json

# MVVM viewer (imaging workflow) — start with docs/AGENTS.Studio.md
dotnet run --project src/Gcam.Studio -c Release

# list all study sub-commands
dotnet run --project src/Gcam.Cli -c Release -- help
```

The CLI (`montecarlo`) exposes a single-scenario run plus ~39 study sub-commands
(FCFOV sweep, config scan, Compton/depth/mask-geometry/mixed-field/front-end, and the
physical-realism set). Run `montecarlo help` for the grouped list.

## Where things live

| Path | What |
|---|---|
| `src/` | Core domain, masks, detector/Compton transport, decoders, simulation studies, CLI, WPF viewer (`Gcam.Wpf`) and its MVVM rewrite (`Gcam.Studio`) |
| `tests/` | xUnit harness — MURA properties + end-to-end physics invariants; `Gcam.Studio.Tests` for the viewer's ViewModels |
| `rtl/` | SystemVerilog ADC front-end (Icarus + cocotb), verified bit-exact against the C# shaper |
| `samples/` | Example scenarios (JSON) and result CSVs / figures |
| **`AGENTS.md`** | Architecture, pipeline, coordinate frame, full command reference |
| **`docs/`** | `AGENTS.*` (findings log, backlog, conventions, Studio guide), `DESIGN.*` (Studio design), `PAPER.ko.md` — naming in `docs/AGENTS.Conventions.Docs.md` |

For anything beyond this quickstart, start with **`AGENTS.md`**.

## License

Proprietary — **All Rights Reserved**. This repository is published for personal
reference only; no permission is granted to use, copy, modify, or distribute it.
See [`LICENSE`](LICENSE).
