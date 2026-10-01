# AGENTS.Conventions.Code — C#, XAML, test and commit conventions

Scope: all C# and XAML in `src/` and `tests/`. Engine-specific rules (e.g. cloning configs) stay in
[AGENTS.md](../AGENTS.md#notes--gotchas); Studio layering rules are in [DESIGN.Architecture](DESIGN.Architecture.md).

## Naming: the same dotted hierarchy everywhere

Documents (`DESIGN.Typography`), resource keys (`Brush.Bg.Canvas`, `Text.Label`) and projects
(`Gcam.Studio.Core`) all read **general → specific, separated by `.`**:

| Thing | Pattern | Examples |
|---|---|---|
| Project / assembly / namespace | `Gcam.<Area>[.<Layer>]` | `Gcam.Simulation`, `Gcam.Studio.Core`, `Gcam.Studio.Core.ViewModels` |
| XAML resource key | `<Kind>.<Group>[.<Variant>]` | `Brush.Accent.Hover`, `Color.Bg.Surface`, `Pad.Panel`, `Gap.Field`, `Size.Control`, `Text.PanelTitle`, `Button.Primary` |
| Document | `KEYWORD.Section[.Sub].md` | see [AGENTS.Conventions.Docs](AGENTS.Conventions.Docs.md) |

Resource key kinds: `Color`, `Brush`, `Space`, `Pad` (padding), `Gap` (margin), `Size`, `Radius`, `Border`,
`Font`, `FontSize`, `Text` (text roles), and the control name for keyed control styles (`Button.Ghost`).

## C#

| Topic | Convention |
|---|---|
| Files | one top-level type per file, file name = type name; **file-scoped namespaces**; nullable and implicit usings on (`Directory.Build.props`) |
| Names | PascalCase types / members / constants; `_camelCase` private fields; `I` prefix for interfaces; `Async` suffix on awaitable methods (except generated command methods) |
| Types | `sealed` by default; `record` / `record struct` for immutable data (`ImagingResult`, `Vec2`); primary constructors for small private types |
| Units | in the name when not obvious: `DistanceMm`, `ActivityUCi`, `EnergyKeV`, `ReconStepMm` |
| Members | expression bodies for one-liners; guard clauses (`ArgumentNullException.ThrowIfNull`) at public entry points |
| Async | pass `CancellationToken` through; CPU-bound work via `Task.Run` in a **service**, never in a ViewModel; report progress with `IProgress<T>` |
| Errors | throw for programmer errors; catch at the boundary that can show them (the ViewModel turns failures into `State = Failed`) |
| Comments | explain **why**, not what; XML `<summary>` on public types and non-obvious members; keep the physics reasoning next to the code it justifies |
| Literals | no magic numbers without a name or a comment (`CheckInterval = 4096 // photons between progress checks`) |

## MVVM (Studio)

- ViewModels use CommunityToolkit.Mvvm source generators: `[ObservableProperty] private T _name;`,
  `[RelayCommand]` on a private method, `[NotifyPropertyChangedFor]` / `[NotifyCanExecuteChangedFor]` for
  dependents, `partial void On<Name>Changed` for clamping.
- Commands that can be cancelled use `[RelayCommand(IncludeCancelCommand = true)]` and take a
  `CancellationToken`.
- ViewModels depend on **contracts** (`ISimulationService`, `IThemeService`) injected through the constructor;
  no `new` of services, no static singletons.
- Nothing in `Gcam.Studio.Core` may reference WPF (enforced: it targets plain `net9.0`).

## XAML

- Views: code-behind contains only `InitializeComponent()` — see [DESIGN.ViewLayer](DESIGN.ViewLayer.md#view-vs-code-behind).
- No literal colours, sizes, paddings or margins in views; colours via `DynamicResource Brush.*`, everything
  else via `StaticResource` keys from `Themes/`.
- Text through a role style (`Style="{StaticResource Text.Label}"`).
- `x:Name` only when something references the element (`ElementName` bindings, code).
- Every interactive element gets `AutomationProperties.Name`; custom surfaces also get `HelpText`.
- Attribute order: `x:Name` / `Grid.*` / `DockPanel.Dock` → content & bindings → style → layout (size, margin)
  → automation. Long elements wrap one logical group per line.

## Tests (xUnit)

| Topic | Convention |
|---|---|
| Method name | `Subject_ExpectedBehaviour[_Condition]` in PascalCase segments: `ZoomAt_KeepsAnchorPointFixed`, `Cancel_KeepsPreviousResult_ReenablesEditing` |
| Class | `<TypeUnderTest>Tests`, one file per class under test |
| Doubles | hand-written fakes for contracts (`FakeSimulation`, `FakeTheme`), no mocking library |
| Physics tests | assert invariants with tolerances that are justified in a comment, and seed every RNG |
| Layers | Studio tests reference `Gcam.Studio.Core` only — if a test needs WPF, the logic is in the wrong layer |

## Commits

- Subject: `<Area>: <imperative summary>` ≤ 72 chars — areas used so far: `Studio`, `Docs`, `WPF`, `Backlog`,
  `Polish`, or a theme/feature name.
- Body: what changed and **why**, wrapped at ~80; note bugs found along the way and how they were verified;
  end with the test totals when tests changed.
- Docs that describe the change go in the same commit ([AGENTS.Conventions.Docs](AGENTS.Conventions.Docs.md#keeping-docs-in-sync)).
- Author identity is the repository's public GitHub identity (repo-local `user.email`, a `noreply` address).
