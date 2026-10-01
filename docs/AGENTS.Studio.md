# AGENTS.Studio — working on GCAM Studio (the MVVM viewer)

Scope: entry point for anyone changing `src/Gcam.Studio*` or `tests/Gcam.Studio.Tests`. GCAM Studio is the
MVVM rewrite of the WPF viewer; `src/Gcam.Wpf` is the original code-behind app and is left as is.

## Read first

| Document | Read it when you want to… |
|---|---|
| [DESIGN.Architecture](DESIGN.Architecture.md) | know the layers, what may reference what, where new code goes, how a run flows |
| [DESIGN.ViewLayer](DESIGN.ViewLayer.md) | add UI: XAML vs code-behind vs control vs converter vs WPF service |
| [DESIGN.Controls](DESIGN.Controls.md) | use or write a control (shared styles, `HeatmapView`, `ColorBar`) |
| [DESIGN.Color](DESIGN.Color.md) · [DESIGN.Layout](DESIGN.Layout.md) · [DESIGN.Typography](DESIGN.Typography.md) | use colours, spacing and text roles; switch themes |
| [AGENTS.Conventions.Code](AGENTS.Conventions.Code.md) · [AGENTS.Conventions.Docs](AGENTS.Conventions.Docs.md) | naming, C# / XAML / test / commit style, how docs are named and kept in sync |

## At a glance

```
Gcam.Studio            WPF shell — views, controls, converters, themes, DI root     (net9.0-windows)
   │  references
   ├─► Gcam.Studio.Services   service implementations (the only layer touching the engine)  (net9.0)
   │        │
   └────────┴─► Gcam.Studio.Core   models, service contracts, ViewModels, view geometry  (net9.0, no WPF)
                     │
                     └─► engine: Gcam.Core, Gcam.Configuration   (Services also → Gcam.Simulation)
```

```bash
dotnet run  --project src/Gcam.Studio -c Release
dotnet test tests/Gcam.Studio.Tests        # ViewModels + view geometry, no WPF needed
```

## Rules that are easy to break

1. Nothing in `Gcam.Studio.Core` references WPF — it targets plain `net9.0`, so it won't compile anyway.
2. View code-behind is `InitializeComponent()` only.
3. No literal colours, sizes or gaps in views; colours are `DynamicResource Brush.*`.
4. Every interactive element has `AutomationProperties.Name`; custom controls have an automation peer.
5. The engine is reached only through `ISimulationService`.
6. Docs that describe a change are updated in the same commit.

## Verifying a UI change

Build and test, then run the app and check the change **in both themes** (top-bar toggle) and with the
keyboard only. UI Automation can drive it: controls are found by `AutomationProperties.Name`
(e.g. "Run simulation", "Reconstruction image"), and `HeatmapView` reports zoom and readout in `ItemStatus`.

## Roadmap

| Step | Scope | State |
|---|---|---|
| 1 | Layered MVVM shell, async run with progress / cancel | done |
| 2 | `HeatmapView`: zoom, pan, mm readout, automation peer | done |
| 2.5 | Theme tokens (dark / light), metrics, typography, shared control styles; fixed-grid layout; colour bars | done |
| 3 | Measurement tools (distance, angle, ROI) as adorners, source drag, measurement panel, results table | next |
| 4 | UI automation smoke tests, high-contrast mode, colormap choice | planned |
