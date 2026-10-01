# Gcam.Studio — WPF shell (net9.0-windows)

Views, custom controls, converters, theme dictionaries, WPF-bound services and the DI composition root.
No business logic: views bind to ViewModels in `Gcam.Studio.Core`, and code-behind only calls
`InitializeComponent()`.

| Folder | Contents | Docs |
|---|---|---|
| `Views/` | `MainWindow` — fixed grid, flexible centre | [DESIGN.ViewLayer](../../docs/DESIGN.ViewLayer.md), [DESIGN.Layout](../../docs/DESIGN.Layout.md) |
| `Controls/` | `HeatmapView` (zoom / pan / mm readout, automation peer), `ColorBar` | [DESIGN.Controls](../../docs/DESIGN.Controls.md#custom-controls) |
| `Converters/` | `NullToCollapsedConverter` | |
| `Rendering/` | colormap lookup tables | |
| `Services/` | `ThemeService` (token swap + DWM title bar) | [DESIGN.Color](../../docs/DESIGN.Color.md#theme-switching) |
| `Themes/` | `Tokens.Dark/Light`, `Metrics`, `Typography`, `Controls` | [DESIGN.Color](../../docs/DESIGN.Color.md), [DESIGN.Layout](../../docs/DESIGN.Layout.md), [DESIGN.Typography](../../docs/DESIGN.Typography.md), [DESIGN.Controls](../../docs/DESIGN.Controls.md) |
| `App.xaml(.cs)` | merged dictionaries, DI registrations, startup placement | [DESIGN.Architecture](../../docs/DESIGN.Architecture.md#dependency-injection) |
