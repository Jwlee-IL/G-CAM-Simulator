# Gcam.Studio.Services — service implementations (net9.0)

Implements the Core contracts that need the simulation engine. This is the **only** Studio layer that
references `Gcam.Simulation`.

| Type | Does |
|---|---|
| `SimulationService` | builds scene configs, applies Studio's detector and ambient inputs, captures the prepared readout lines (refusing a physical readout with ambient or BSR), and starts list-mode acquisition |
| `AcquisitionSession` | transports fresh MC histories off-thread, paces live time with an injectable clock, publishes immutable cumulative images / events at 4 Hz, and supports Stop / preset completion and event-identical Continue; disposal on Reset releases the session |
| `ImagingService` / `FocusSweepService` | projects retained per-isotope floods / cancellable focus tracks without transport (`ImagingProjection`: cross-correlation or MLEM, with `MlemDecoderCache`; `StripProjection` / `StripCountEstimator` for stripping); external range is a separate view setting |
| `WaveformService` | rasterizes acquired events and runs shared-chain shapers off-thread; CR-RC uses Q12 state |
| `DetectorFaceService` | supplies seeded gain and exact active/gap geometry |
| `SpectrumService` | smears and bins the shared event deposits on a worker (`MeasurementStage`); incrementally handles singles / arrival-time pile-up, groups unresolved lines and computes window shares |
| `ReadoutPreparationService` | prepares the experimental four-output readout per calibration line on one cancellable worker with progress, cached for the session; the offer / cost record is embedded and hash-pinned (`ReadoutOfferRecordLoader`, `ReadoutOfferPin`) |
| `PhysicalReadoutProjection` / `PhysicalReadoutWaveform` | project the held physical-readout records through each line's own calibration into images, spectra and the four-output lanes |

WPF-bound services (e.g. `ThemeService`) live in `Gcam.Studio/Services` instead, because they need WPF.
See [DESIGN.Architecture](../../docs/DESIGN.Architecture.md).
