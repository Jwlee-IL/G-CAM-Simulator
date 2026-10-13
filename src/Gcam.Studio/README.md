# Gcam.Studio — WPF shell (net9.0-windows)

Views, custom controls, converters, theme dictionaries, WPF-bound services and the DI composition root.
No business logic: views bind to ViewModels in `Gcam.Studio.Core`, and code-behind only calls
`InitializeComponent()`.

| Folder | Contents | Docs |
|---|---|---|
| `Views/` | `MainWindow` — borderless window with a themed caption, fixed grid, flexible centre, left Scene / Optics / Detector tabs, one-line status bar; Imaging, Spectrum, Waveform and Detector centre / panel templates; `OptionsView` (the Options dialog in the modal layer); `EmptyReadoutState` | [DESIGN.ViewLayer](../../docs/DESIGN.ViewLayer.md), [DESIGN.Layout](../../docs/DESIGN.Layout.md) |
| `Controls/` | `HeatmapView` (zoom / pan / mm readout, automation peer), `ColorBar`, `ImageStackPanel` (square image + legend), `MeasurementAdorner` + `MeasurementOverlay` (measurements, display-only source markers and found peaks over a heatmap), `WindowFrame` / `CaptionBar` / `CaptionButtons` (borderless window chrome), `ModalHost`, `HelpMark`, `SettingsGroup`, `FocusRequest` | [DESIGN.Controls](../../docs/DESIGN.Controls.md#custom-controls) |
| `Converters/` | `NullToCollapsedConverter`, `InverseBoolToVisibilityConverter`, `EnumMatchConverter` | [DESIGN.ViewLayer](../../docs/DESIGN.ViewLayer.md#how-a-view-gets-its-data-and-services) |
| `Rendering/` | colormap lookup tables | |
| `Services/` | `ThemeService` (token swap + DWM attribute for the remaining native frame), `ModalService` | [DESIGN.Color](../../docs/DESIGN.Color.md#theme-switching) |
| `Themes/` | `Tokens.Dark/Light`, `Metrics`, `Typography`, `Controls` | [DESIGN.Color](../../docs/DESIGN.Color.md), [DESIGN.Layout](../../docs/DESIGN.Layout.md), [DESIGN.Typography](../../docs/DESIGN.Typography.md), [DESIGN.Controls](../../docs/DESIGN.Controls.md) |
| `App.xaml(.cs)` | merged dictionaries, DI registrations, startup placement | [DESIGN.Architecture](../../docs/DESIGN.Architecture.md#dependency-injection) |
