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
| [AGENTS.UiAutomation](AGENTS.UiAutomation.md) | run or extend the UI automation of the real window (safety, selectors, pilot, evidence) |

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
4. Every interactive element has `AutomationProperties.Name` and, if a test drives it, an `AutomationId`; custom
   controls have an automation peer. Never put a fixed `Name` on a text element — it hides the text.
5. The engine is reached only through `ISimulationService`.
6. Docs that describe a change are updated in the same commit.

## Verifying a UI change

Build and test, then run the app and check the change **in both themes** (top-bar toggle) and with the
keyboard only. The automated path is `tests/Gcam.Studio.UiTests` (opt-in, `GCAM_UI_TESTS=1`): controls are found
by `AutomationId` (`RunSimulation`, `ToolDistance`, `MeasurementList`, …), the run state is the status line's
`ItemStatus`, and the measuring tools get real pointer input. How it is kept safe and what it has verified:
[AGENTS.UiAutomation](AGENTS.UiAutomation.md).

Pitfalls met so far: names are not unique (a panel title and its heatmap are both "Detector flood map"), so
select by AutomationId; a `TextBox` commits on LostFocus, so move focus before judging; capture the window's
visible frame, not its UIA rectangle (that includes borders that show what's behind); in PowerShell scripts, don't
name a helper `Select` (the `Select-Object` alias wins).

## Roadmap

| Step | Scope | State |
|---|---|---|
| 1 | Layered MVVM shell, async run with progress / cancel | done |
| 2 | `HeatmapView`: zoom, pan, mm readout, automation peer | done |
| 2.5 | Theme tokens (dark / light), metrics, typography, shared control styles; fixed-grid layout; colour bars | done |
| 3 | Measurement tools (distance, angle, ROI) as an adorner, source drag on the reconstruction, measurement panel, results table, stale-result chip | done |
| 4 | UI automation (charter, safety, measurement and pilot done; scale-out next), keyboard crosshair for measuring, high-contrast mode, colormap choice | in progress |
