# Gcam.Studio.Core — presentation layer (net9.0, no WPF)

ViewModels, models, service contracts and UI-free view geometry for GCAM Studio.
Targets plain `net9.0` on purpose: a ViewModel that reaches for a WPF type does not compile, and the tests
(`tests/Gcam.Studio.Tests`) run without a UI stack.

| Folder | Contents |
|---|---|
| `ViewModels/` | `MainViewModel` (scene, run / cancel, state, stale result, theme toggle), `SourceItemViewModel` (one source, clamped input), `MeasurementsViewModel` / `MeasurementViewModel` (tool, measurements, selection, ROI refresh) |
| `Services/` | contracts the UI depends on: `ISimulationService` + `ImagingResult`, `IThemeService` |
| `Imaging/` | `HeatmapViewport` — fit, whole-pixel snapping, zoom about a point, pan limits, screen ↔ image ↔ mm; `MeasurementMath` — distance, angle, ROI stats; `TickFormatter` — colour-scale labels; `IPlaneMarker` — what an overlay can drag |

See [DESIGN.Architecture](../../docs/DESIGN.Architecture.md).
