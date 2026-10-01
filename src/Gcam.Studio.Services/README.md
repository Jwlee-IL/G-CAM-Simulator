# Gcam.Studio.Services — service implementations (net9.0)

Implements the Core contracts that need the simulation engine. This is the **only** Studio layer that
references `Gcam.Simulation`.

| Type | Does |
|---|---|
| `SimulationService` | builds the config from the scene (`SceneConfigBuilder`), runs `SimulationRunner` on the thread pool with progress and cancellation, and maps the result (incl. the mm grid of both images) to `ImagingResult` |

WPF-bound services (e.g. `ThemeService`) live in `Gcam.Studio/Services` instead, because they need WPF.
See [DESIGN.Architecture](../../docs/DESIGN.Architecture.md).
