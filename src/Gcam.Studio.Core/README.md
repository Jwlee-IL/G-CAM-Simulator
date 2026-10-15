# Gcam.Studio.Core — presentation layer (net9.0, no WPF)

ViewModels, models, service contracts and UI-free view geometry for GCAM Studio.
Targets plain `net9.0` on purpose: a ViewModel that reaches for a WPF type does not compile, and the tests
(`tests/Gcam.Studio.Tests`) run without a UI stack.

| Folder | Contents |
|---|---|
| `ViewModels/` | `MainViewModel` (scene, live time / speed, Start / Continue / Stop / Reset with input locks and a confirmed Reset, Start refusals for the status bar, snapshot, theme toggle; partial files for the Options summary and the physical readout: mode, Prepare, energy-window presets), `OptionsViewModel` (the Options dialog's draft, Acquisition / Detector / Environment tabs, applied on OK), `OpticsEditorViewModel`, the four workspaces, `SourceItemViewModel` (one source, clamped input), measurement tools / session / values, readout rows |
| `Services/` | `IAcquisitionService`; `IAcquisitionSession`, `AcquisitionSnapshot`, `ImagingResult`, `IThemeService`; `IConfirmationService` / `IModalService` (Reset confirmation, in-window modal content); `ISpectrumService` and spectrum settings / view / line / band records; `IImagingService`, `IWaveformService`, `IDetectorFaceService`, `IFocusSweepService` and their immutable request/result records; `IReadoutPreparationService` with the readout policy, offer record, preparation and snapshot records; option defaults and validation |
| `Imaging/` | `HeatmapViewport` — fit, whole-pixel snapping, zoom about a point, pan limits, screen ↔ image ↔ mm; `MeasurementMath` — distance, angle, ROI stats; `TickFormatter` — colour-scale labels; `IPlaneMarker` — generic overlay marker contract (Studio source markers are display-only); `OverlayLabelLayout` — deterministic chip placement; `FocusSweepMath`; `StudioMlem` — the MLEM iteration count |
| `Optics/` | `OpticsPreset` (Custom, Sharp, Baseline, Wide FOV, High-res), `OpticsPolicy` (input and allocation limits), `OpticsGeometry` (analytical resolution / field values) |
| `Detector/` | `DetectorFace` / `FaceRectangle` (exact active and gap regions), `ReadoutDetectorTab` (the Detector workspace's view tabs), `ReadoutSensorLayout` |
| `Reporting/` | `SimulationReport` (the displayed state captured for a report), `ReportWriter` (deterministic HTML with inline SVG, and the scene JSON), `WordReportWriter` (hand-written Office Open XML), `EventCsvWriter` (event data CSV and its metadata), `ReportMarkup`, `ReportFormat`, `ReportFiles`, `ReportMeasurement`; the contract `IReportExportService` is in `Services/` |
| `Rendering/` | `Colormap` — Studio's viridis table, shared by the heat map and the report |
| `Plotting/` | `PlotViewport` (linear / log mapping, zoom, pan), `MinMaxPyramid` (exact column extrema), ticks, bands, markers and series records behind `PlotView` |

See [DESIGN.Architecture](../../docs/DESIGN.Architecture.md).
