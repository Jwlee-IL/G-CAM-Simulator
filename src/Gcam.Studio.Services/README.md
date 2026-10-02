# Gcam.Studio.Services — service implementations (net9.0)

Implements the Core contracts that need the simulation engine. This is the **only** Studio layer that
references `Gcam.Simulation`.

| Type | Does |
|---|---|
| `SimulationService` | builds scene configs and starts list-mode acquisition |
| `AcquisitionSession` | transports fresh MC histories off-thread, paces live time with an injectable clock, publishes immutable cumulative images / events at 4 Hz, and supports Stop / preset completion and event-identical Continue; disposal on Reset releases the session |
| `ImagingService` / `FocusSweepService` | projects retained per-isotope floods / cancellable focus tracks without transport; external range is a separate view setting |
| `WaveformService` | rasterizes acquired events and runs shared-chain shapers off-thread; CR-RC uses Q12 state |
| `DetectorFaceService` | supplies seeded gain and exact active/gap geometry |
| `SpectrumService` | smears and bins the shared event deposits on a worker; incrementally handles singles / arrival-time pile-up, groups unresolved lines and computes window shares |

WPF-bound services (e.g. `ThemeService`) live in `Gcam.Studio/Services` instead, because they need WPF.
See [DESIGN.Architecture](../../docs/DESIGN.Architecture.md).
