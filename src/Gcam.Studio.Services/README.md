# Gcam.Studio.Services — service implementations (net9.0)

Implements the Core contracts that need the simulation engine. This is the **only** Studio layer that
references `Gcam.Simulation`.

| Type | Does |
|---|---|
| `SimulationService` | builds scene configs and starts list-mode acquisition; retains `RunAsync` for batch compatibility |
| `AcquisitionSession` | transports fresh MC histories off-thread, paces live time with an injectable clock, publishes immutable cumulative images / events at 4 Hz, and supports Stop / preset completion |

WPF-bound services (e.g. `ThemeService`) live in `Gcam.Studio/Services` instead, because they need WPF.
See [DESIGN.Architecture](../../docs/DESIGN.Architecture.md).
