# AGENTS.Studio — working on GCAM Studio (the MVVM viewer)

Scope: entry point for anyone changing `src/Gcam.Studio*` or `tests/Gcam.Studio*`. GCAM Studio is the
MVVM rewrite of the WPF viewer; `src/Gcam.Wpf` is the original code-behind app and is left as is.

## Read first

| Document | Read it when you want to… |
|---|---|
| [DESIGN.Architecture](DESIGN.Architecture.md) | know the layers, what may reference what, where new code goes, how a run flows |
| [DESIGN.ViewLayer](DESIGN.ViewLayer.md) | add UI: XAML vs code-behind vs control vs converter vs WPF service |
| [DESIGN.Controls](DESIGN.Controls.md) | use or write a control (shared styles, `HeatmapView`, `ColorBar`) |
| [DESIGN.Color](DESIGN.Color.md) · [DESIGN.Layout](DESIGN.Layout.md) · [DESIGN.Typography](DESIGN.Typography.md) | use colours, spacing and text roles; switch themes |
| [VV.Studio.SRS](VV.Studio.SRS.md) · [VV.Studio.SDS](VV.Studio.SDS.md) · [VV.Studio](VV.Studio.md) | add or change behaviour: its requirement (SRS), the unit that meets it (SDS), its test and matrix row (VV) |
| [AGENTS.Conventions.Code](AGENTS.Conventions.Code.md) · [AGENTS.Conventions.Docs](AGENTS.Conventions.Docs.md) | naming, C# / XAML / test / commit style, how docs are named and kept in sync |
| [AGENTS.Rationale](AGENTS.Rationale.md) | know why a rule exists before changing or bending it |
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
dotnet test tests/Gcam.Studio.Tests            # ViewModels + view geometry, no WPF needed
dotnet test tests/Gcam.Studio.Services.Tests   # SimulationService against the real engine
```

Ordinary test runs and CI skip long numerical evidence tests. Set `GCAM_EVIDENCE_TESTS=1` explicitly
to run the full sampling sweep, 20-seed imaging precision measurement and fast-budget seed-spread check:

```powershell
$env:GCAM_EVIDENCE_TESTS = '1'
dotnet test tests/Gcam.Studio.Services.Tests -c Release --filter Category=Evidence --logger 'console;verbosity=detailed'
Remove-Item Env:GCAM_EVIDENCE_TESTS
```

The default run retains the small deterministic off-axis sampling regression. Evidence output and
budgets are documented in [VV.Studio.Imaging](VV.Studio.Imaging.md). The independent render opt-in
is `GCAM_RENDER_SNAPSHOTS=1`; desktop input uses `GCAM_UI_TESTS=1`. Enabling evidence does not enable either.

## Rules that are easy to break

1. Nothing in `Gcam.Studio.Core` references WPF — it targets plain `net9.0`, so it won't compile anyway.
2. View code-behind is `InitializeComponent()` only.
3. No literal colours, sizes or gaps in views; colours are `DynamicResource Brush.*`.
4. Every interactive element has an accessible name containing its visible label (an explicit
   `AutomationProperties.Name` only for glyph buttons and custom surfaces) and, if a test drives it, an
   `AutomationId`; custom controls have an automation peer. Never put a fixed `Name` on a text element — it hides
   the text.
5. Transport and spectrum processing reach the engine only through `IAcquisitionService` and `ISpectrumService`.
6. Docs that describe a change are updated in the same commit.

## Verifying a UI change

Build and test, then run the app and check the change **in both themes** (top-bar toggle) and with the
keyboard only. The automated path is `tests/Gcam.Studio.UiTests` (opt-in, `GCAM_UI_TESTS=1`): controls are found
by `AutomationId` (`StartAcquisition`, `ResetAcquisition`, `ToolDistance`, `MeasurementList`, …), the run state is the status line's
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
| 4 | UI automation (6 scenarios traced to V&V; heatmap navigation next), keyboard crosshair for measuring, high-contrast mode, colormap choice | in progress |
| Migration | One shared run, workspace templates and first-party `PlotView` (TODO-06); feature workspaces follow in TODO-07 … TODO-12 | implemented; plot CPU gate passed; existing desktop regression and polish survey blocked by desktop input / capture errors |
| Live acquisition | Fresh list-mode MC, weight rejection, shared immutable 4 Hz snapshots, Start / Stop with preset live time and speed (TODO-13) | implemented; physics / headless validation in VV.Studio; desktop validation deferred while the desktop and UI-test project are occupied |
| Spectrum | Shared live counts, physical default chain, per-energy windows with unresolved-line grouping, optional arrival-time pile-up (TODO-07) | implemented; headless evidence in VV.Studio; desktop validation deferred while the desktop and UI-test project are occupied |
| Acquisition control | Start / Stop / Continue / Reset (multichannel-analyser model): the session runs in segments and continues event for event; physical inputs locked while data exist (stale state removed); new seed per acquisition, fixable; source drag withdrawn (TODO-24, [PLAN.Studio.AcquisitionControl](PLAN.Studio.AcquisitionControl.md)) | implemented; headless + render evidence in VV.Studio.Acquisition; rewritten desktop scenarios not run (TODO-22) |

**Acquisition control in code.** `MainViewModel` derives its command table from `IsRunning`, `HasData` and whether a
continuable session is held (`CanEditInputs`, `CanEditLiveTime`, `CanEditSpeed`, `StartLabel`); every physical setter
also reverts a change while locked, so tests and other writers meet the same lock as the disabled controls. Continue
calls `IAcquisitionSession.Continue` and must not `Begin` the workspaces (the services append under one acquisition
id); Reset disposes the session and resets the workspaces. `AcquisitionSession` keeps its look-ahead event across
Stop — dropping it loses a real count (the continuation tests catch it).

## Detector and retained-flood focus

Registered workspaces are Imaging, Spectrum, Waveform and Detector (Ctrl+1…4). Reflector gap is a shared
run input edited in µm, validated against pending pixel pitch and captured in mm with acquired settings.
Detector face/readouts use pending inputs before acquisition and acquired detector/optics afterwards.
`IDetectorFaceService` supplies the engine gain pattern to pure exact rectangle geometry.

`IFocusSweepService` projects the selected retained channel flood on an independent cancellable worker;
it never transports or calibrates and is not awaited by the snapshot loop. The request captures a labelled
acquisition prefix. Setting/acquisition revisions discard obsolete responses. K is user-chosen, tracks link
by angle and raw half-max endpoints can be censored. External surface range changes focus only through
Use as focus. Numerical conditions, seed-spread evidence protocol and execution limits are recorded in
[VV.Studio.Detector](VV.Studio.Detector.md). Crosstalk/SiPM pitch are deferred to the readout model.
