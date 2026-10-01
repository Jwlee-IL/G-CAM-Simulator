# DESIGN.ViewLayer — what goes in XAML, code-behind, controls, converters and WPF services

Scope: the WPF project `src/Gcam.Studio`. Layers and dependencies: [DESIGN.Architecture](DESIGN.Architecture.md).
XAML style rules: [AGENTS.Conventions.Code](AGENTS.Conventions.Code.md#xaml).

## Folder map

```
Gcam.Studio/
  App.xaml / App.xaml.cs   merged theme dictionaries · DI composition root · startup window placement
  Views/                   screens: XAML layout + code-behind that only calls InitializeComponent()
  Controls/                elements with their own rendering / input (HeatmapView, ColorBar)
  Converters/              IValueConverter implementations (NullToCollapsedConverter)
  Rendering/               pixel-level helpers shared by controls (colormap lookup tables)
  Services/                WPF-bound implementations of Core contracts (ThemeService)
  Themes/                  Tokens.Dark / Tokens.Light, Metrics, Typography, Controls
```

## View vs code-behind

**A view's code-behind contains only `InitializeComponent()`.** `MainWindow.xaml.cs` in full:

```csharp
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
```

Everything a code-behind is usually tempted to do has a better home:

| Concern | Home | Why not code-behind |
|---|---|---|
| State, commands, validation, enable / disable | ViewModel in `Gcam.Studio.Core` | must be testable without WPF |
| Layout, styling, visual states | XAML + `Themes/` | declarative, themeable |
| Value → visual mapping (null → collapsed, state → colour) | converter, or `DataTrigger` in XAML | reusable, no per-screen glue |
| Drawing a surface, pointer / keyboard gestures on it | custom control in `Controls/` ([DESIGN.Controls](DESIGN.Controls.md#custom-controls)) | reusable, owns its automation peer |
| OS / framework calls (resource dictionaries, DWM, dialogs) | service in `Services/` behind a Core contract | ViewModels call the contract; tests fake it |
| Window placement on startup | `App.xaml.cs` | app policy, not part of a screen |

The only accepted exception is stateless visual glue with no logic (e.g. moving focus once a view has
loaded); it must carry a comment saying why it can't live elsewhere.

## How a view gets its data and services

- `App.xaml.cs` resolves `MainWindow` and sets `DataContext` to `MainViewModel` from the DI container.
- Views bind to ViewModel properties and commands only. Commands that are cancellable expose a generated
  `…CancelCommand` (`RunCancelCommand`).
- Controls expose results as read-only dependency properties, and sibling elements bind to them by
  `ElementName` (`ColorBar.Minimum` ← `HeatmapView.DataMin`), so no code connects them.
- `d:DataContext="{d:DesignInstance Type=vm:MainViewModel}"` keeps binding IntelliSense working across the
  assembly boundary (`xmlns:vm="clr-namespace:…;assembly=Gcam.Studio.Core"`).

## How resources load

`App.xaml` merges `Tokens.Dark` → `Metrics` → `Typography` → `Controls`. `StaticResource` cannot see sibling
merged dictionaries while XAML loads, so a dictionary that uses another's keys merges it itself
(`Typography` merges `Metrics`; `Controls` merges `Metrics` and `Typography`). Colours are always
`DynamicResource`, which resolves at runtime — that is what lets [theme switching](DESIGN.Color.md#theme-switching)
replace the token dictionary.

## Screen structure

The grid, panel widths and window sizing policy are in [DESIGN.Layout](DESIGN.Layout.md#screen-grid-mainwindow).

## Checklist for a new screen element

- [ ] State and commands in a ViewModel, with a test.
- [ ] XAML uses only theme keys — no literal colours, sizes or gaps.
- [ ] Code-behind still only `InitializeComponent()`.
- [ ] `AutomationProperties.Name` on every interactive element; a keyboard path exists.
- [ ] Checked in both themes (top-bar toggle).
- [ ] The relevant `DESIGN.*` page updated in the same commit.
