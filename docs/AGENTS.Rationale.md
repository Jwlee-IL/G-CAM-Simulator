# AGENTS.Rationale — the project's rules and why they exist

Scope: every **normative** rule stated in this repository's docs and code comments (must / never / only / always),
collected in one place with its reason, where it is stated, and what enforces it. Requirements of the product
itself live in the verification & validation documents and are not repeated here.

Use it to check a change against the rules, and to argue about a rule: change the rule's source document and this
table in the same commit. A rule with no reason is a defect — the last column says where one was missing.

Enforcement: **C** compiler / build · **T** automated test · **R** review against the docs · **H** harness code
(refuses at run time).

## Engine and physics

| Rule | Why | Stated in | Enforced |
|---|---|---|---|
| Every feature is built from real physics; never hand-add a tail, line or number. | A simulator that injects the answer can't tell you anything you didn't put in. | `CLAUDE.md` | R |
| Every physics claim is Monte-Carlo-verified; report small effects as small. | Several realism effects turned out small for this camera; inflating them would misdirect design. | `CLAUDE.md`, `AGENTS.Conventions.Docs` | R, T (per-theme tests) |
| Verify an inherited "it's done" claim against the code before repeating it. | A docs pass once trusted a summary's "CLI complete" — it was 39 commands, not 23. | `CLAUDE.md` | R |
| One `SimulationConfig` (JSON) fully describes a scenario; experiments add an implementation + factory switch, not runner edits. | Keeps every study reproducible from a file and the runner stable. | `AGENTS.md` | R |
| Clone configs with `baseConfig.Clone()` + mutate; never hand-write `new SimulationConfig { … }` copies. | A review found hand-written clones that silently dropped fields (mask attenuation, crystal material). | `AGENTS.md` | R |
| Report physical counts as `DetectedWeight`, not `PhotonsDetected`. | `PhotonsDetected` is a geometric-hit count that over-counts non-ideal crystals. | `AGENTS.md`, `SimulationService` | R |
| Directional biasing is the default emission mode. | ~100× fewer photons for the same estimate, verified unbiased against 4π within ~1 %. | `AGENTS.md` | T |
| Studio's scene builds a non-cyclic (finite-mask) decode. | Suppresses off-axis ghosts so several sources resolve separately. | `SceneConfigBuilder` | T |

## Architecture and layering (Studio)

| Rule | Why | Stated in | Enforced |
|---|---|---|---|
| `Gcam.Studio.Core` references no WPF type. | ViewModels and view geometry must be testable without a UI stack; "by discipline" erodes, a target framework doesn't. | `DESIGN.Architecture`, `AGENTS.Studio` | **C** (plain `net9.0`) |
| Only `Gcam.Studio.Services` references the simulation engine; the UI reaches it through `ISimulationService`. | Views must not be able to call the engine; tests swap the service for a fake. | `DESIGN.Architecture`, `AGENTS.Studio` | C for Core; **R only** for the shell (project references are transitive) |
| `App.xaml.cs` is the only place that knows concrete types (DI root). | ViewModels get contracts, so tests pass fakes. | `DESIGN.Architecture` | R |
| CPU-bound work runs via `Task.Run` in a **service**, never in a ViewModel. | The UI never blocks, and ViewModels stay synchronous-testable. | `AGENTS.Conventions.Code` | R |
| Build the engine config on the caller's thread, run the transport on the pool. | Bad input fails immediately instead of inside a background task. | `SimulationService` | R |
| Accept progress reports only while running and never let the bar move backwards. | `Progress<T>` posts asynchronously; a late report can land after completion. | `MainViewModel`, `DESIGN.Architecture` | T (timing-dependent, see the V&V anomalies) |
| A cancelled run keeps the previous result; any other failure becomes `State = Failed`, never an unhandled crash. | The user must never lose a good image to a cancel, or the app to an exception. | `DESIGN.Architecture` | T |
| Studio unit tests reference `Gcam.Studio.Core` only. | If a test needs WPF, the logic is in the wrong layer. | `AGENTS.Conventions.Code` | C (test project targets `net9.0`) |

## Views, XAML and controls

| Rule | Why | Stated in | Enforced |
|---|---|---|---|
| View code-behind is `InitializeComponent()` only (stateless visual glue allowed with a comment). | Everything with logic has a better, testable home (table in DESIGN.ViewLayer). | `DESIGN.ViewLayer`, `AGENTS.Studio` | R |
| Behaviour added to a control from a view uses attached properties (e.g. `MeasurementOverlay.*`). | Keeps the view declarative and the code-behind empty. | `DESIGN.ViewLayer` | R |
| A control never constructs domain objects; finished gestures go back through a command (`AddCommand`). | The ViewModel stays the single owner of state and validation. | `DESIGN.Controls` | R |
| Logic out, drawing in: UI-free geometry lives in Core and is tested; the control only translates input and draws. | The hard part (fit, zoom anchor, screen ↔ mm) gets unit tests. | `DESIGN.Controls` | T (`HeatmapViewportTests`, `MeasurementMathTests`) |
| Controls have no colours of their own; brushes are DPs set from tokens. Only image overlays use fixed white-on-black. | Theme switching works without touching controls; overlays sit on the colormap, not the theme. | `DESIGN.Controls` | R |
| Every custom control has its own focus ring and an automation peer. | A bare `FrameworkElement` is invisible to screen readers and UI tests. | `DESIGN.Controls` | T (UI tests find `HeatmapView` by ID) |
| Keyboard parity: anything the mouse can do has a key. | Accessibility. **Known exception:** creating measurements (pointer only, documented gap). | `DESIGN.Controls` | R |
| Overlay geometry is stored in mm, never in screen pixels. | Measurements survive zoom, pan and re-runs with no screen state to keep in sync. | `DESIGN.Controls`, `MeasurementViewModel` | T |
| The adorner is created on `Loaded` and removed on `Unloaded`. | It must never outlive the element or keep the ViewModels alive. | `MeasurementOverlay` | R |
| With the Pan tool the overlay is hit-test transparent except over a marker. | Pan and wheel zoom must keep reaching the heatmap underneath. | `DESIGN.Controls` | T (UI scenarios) |
| `x:Name` only when something references the element. | *Not stated in the source.* Proposed: fewer generated fields and no accidental coupling to element names. | `AGENTS.Conventions.Code` | R |
| A control never hard-codes a typeface; it uses the inherited `TextElement.FontFamily`. | The view decides fonts (Font.Mono for numbers); a hard-coded "Segoe UI" put two numeral styles on one screen. | `DESIGN.Typography` | R |
| An adorner that captures the mouse must handle losing it (`OnLostMouseCapture`). | Alt+Tab or a modal mid-drag left a marker following a released mouse. | `DESIGN.Controls` | R |
| Remove an adorner through the layer it was added to, not a fresh lookup. | On `Unloaded` the element may already be out of the tree, so a lookup can return null and leak the adorner. | `MeasurementOverlay` | R |
| Add a shared style only when two or more views need it. | *Not stated in the source.* Proposed: a shared style is an API; one-off styles stay next to their only user. | `DESIGN.Controls` | R |

## Visual design

| Rule | Why | Stated in | Enforced |
|---|---|---|---|
| No literal colours, sizes, paddings or margins in views; colours via `DynamicResource Brush.*`. Control templates inside `Themes/` may use internal literals; the window's default / minimum size is window policy. | One place per value; colours must be swappable at run time. | `AGENTS.Conventions.Code`, `DESIGN.ViewLayer` | R — met in `MainWindow.xaml` since the 2026-10-01 audit |
| Headings carry no margin; a `PanelHeader` row owns the gap and the row height. | A margin on the title pushed it 4–5 px above the buttons beside it. | `DESIGN.Layout` | R |
| An image keeps its colour bar and readout directly beneath it (`ImageStackPanel`). | A legend far from its data reads as two unrelated things. | `DESIGN.Controls` | R |
| Lists use a **max** height and grow with their rows. | A fixed height reserved blank rows. | `DESIGN.Layout` | R |
| A dictionary that uses another's keys merges it itself. | `StaticResource` can't see sibling merged dictionaries while XAML loads. | `DESIGN.ViewLayer` | load-time failure if broken |
| Theme switch replaces the token dictionary **in place**. | Later merged dictionaries win; inserting a second one leaves the old in effect. | `DESIGN.Color` | R |
| The accent (cyan) is used only where it means something: primary action, selection, focus, progress — and progress only while running. | Restraint is the "dark instrument" direction; colour that means nothing teaches users to ignore colour. A full bar at rest and an accent peak chip were cyan with no meaning. | `DESIGN.Color` | R |
| `Text.Disabled` only for disabled controls. | It is below AA as text (3.7:1 dark, 2.5:1 light). | `DESIGN.Color` | R |
| Label what a value is: flood pixels are "weighted counts". | With directional biasing a crystal's count is a fractional weight; "counts" next to 0.0016 invites distrust. | `MainWindow.xaml` | R |
| A colour scale shares one ×10ⁿ and one decimal count. | "0.00157 … 0.0161" is ragged and hard to compare. | `DESIGN.Controls`, `TickFormatter` | T |
| Image overlays never use the accent; a selected overlay is a thicker line + inverted chip. | Cyan collides with viridis' teal band. | `DESIGN.Color`, `MeasurementAdorner` | R |
| Heatmaps use viridis and always have a colour bar with numeric ticks; ticks never show `-0`. | Perceptually uniform and colour-blind safe; colours must be readable as values. | `DESIGN.Color`, `DESIGN.Controls` | R |
| Status and selection are never signalled by colour alone. | Colour-blind users, and screen readers. | `DESIGN.Color` | R |
| One primary button per screen, top right. | The main action is always in the same place. | `DESIGN.Controls`, `DESIGN.Layout` | R |
| Spacing on a 4 px grid; a container owns its inner space (`Pad.*`), the gap between siblings is the preceding sibling's margin (`Gap.*`). | Never split one gap between two elements, never stack padding and margin on one edge — that's how spacing drifts. | `DESIGN.Layout` | R |
| Fixed-width side panels, flexible centre, minimum window 1280×800 (not a fixed window). | A fixed window breaks on 1366×768 and overflows at 125 / 150 % scaling. | `DESIGN.Layout`, `App.xaml.cs` | R |
| At fit, heatmap cells span a whole number of device pixels. | Nearest-neighbour cells stay equal width at any DPI. | `DESIGN.Controls`, `HeatmapViewport` | T |
| Views pick a text role (`Text.*`), never set `FontSize` / `FontWeight` / `Foreground` directly. | *Partly stated* (roles exist so text follows the theme). Proposed: one type scale, changed in one file. | `DESIGN.Typography` | R |
| Numbers, coordinates and units use the mono font. | Values line up in columns. | `DESIGN.Typography` | R |

## Measurement semantics

| Rule | Why | Stated in | Enforced |
|---|---|---|---|
| An ROI counts whole pixels **by centre**, never fractional area. | A flood-map pixel is one crystal; a partial count is a number no detector ever read out. | `MeasurementMath` | T |
| ROI values are recomputed on every new result; lengths and angles are not. | The image under an ROI changes; geometry doesn't. | `MeasurementsViewModel` | T |
| A dragged source lands on a 0.1 mm grid, clamped to the image. | *Not stated in the source.* Proposed: the X / Y fields show one decimal; finer positions would be false precision. Clamping keeps the marker on screen; positions outside the reconstruction (e.g. to show a ghost) are typed into X / Y. | `DESIGN.Controls` | R, T (UI scenario checks the snap) |
| An ROI with no pixel centre inside has no value ("—"), not "Σ 0". | A plausible number for an invalid region is worse than none. | `MeasurementViewModel` | T |
| Editing the scene after a run marks the result "outdated" until the next run. | An old image must not be read as current. | `MainViewModel` | T |

## Accessibility and test hooks

| Rule | Why | Stated in | Enforced |
|---|---|---|---|
| Every interactive element has an accessible name that **contains its visible label**. Set `AutomationProperties.Name` only where the content can't serve (icon glyphs such as "+", custom surfaces). | A fixed name that differs from what's on screen breaks voice control (WCAG 2.5.3) — found on the theme button. | `AGENTS.Conventions.Code` | R, T (UI tests read names) |
| Never put a fixed `Name` on a text element; name list rows from the item. | A fixed name hides the text (the status live region announced only "Status"); an unnamed row is read as the ViewModel's type name. | `AGENTS.Conventions.Code`, `AGENTS.Studio` | R (found by UIA measurement) |
| Anything a UI test drives gets a unique `AutomationId`; tests select by ID, never by name. | Names are display text and not unique (a panel title and its heatmap share one). | `AGENTS.Conventions.Code` | T (selector must match exactly one) |
| The run state is published as the status line's `ItemStatus`. | Tests judge the outcome without parsing prose. | `AGENTS.UiAutomation` | T |

## Testing and UI automation

| Rule | Why | Stated in | Enforced |
|---|---|---|---|
| Test names `Subject_ExpectedBehaviour[_Condition]`; one class per type under test; hand-written fakes. | A traceability row can name one method; fakes keep tests readable without a mocking library. | `AGENTS.Conventions.Code` | R |
| Physics tests use justified tolerances and seeded RNGs. | A tolerance without a reason is a test that passes by tuning. | `AGENTS.Conventions.Code` | R |
| Desktop UI tests are opt-in (`GCAM_UI_TESTS=1`) and reported as **skipped** otherwise. | They take over the mouse and focus; a skipped run must never read as green. | `AGENTS.UiAutomation`, `DesktopFactAttribute` | H |
| UI runs are serial. | One desktop, one pointer. | `AssemblyInfo.cs` | H |
| Never attach to or kill a process the run didn't start; close only the recorded PID. | Killing "any Gcam.Studio" could destroy someone's session. | `AGENTS.UiAutomation`, `StudioProcess` | H |
| Recursive delete only for a folder under `%TEMP%` whose owner marker is still exactly ours. | A wrong path in a recursive delete is unrecoverable. | `StudioProcess` | H |
| Each scenario gets a fresh app. | No tool, selection, zoom or result leaks between scenarios. | `Scenario.cs` | H |
| Judge on product state, not on "the click didn't throw"; derive expectations independently of the app's code. | An API call succeeding doesn't prove the handler ran; an oracle that reuses product code repeats its bugs. | `AGENTS.UiAutomation` | T |
| Every verdict must fail when broken on purpose (`GCAM_UI_BREAK_VERDICT=1`). | A check that can't fail proves nothing. | `AGENTS.UiAutomation` | T (record) |
| Put oracle corners where a pixel of error can't change the answer (ROI corners on cell boundaries). | On a cell centre, half a pixel flipped the count. | `FloodOracle` | T |
| Verdicts must not all be invariant to translation / flip — at least one pins absolute positions. | Lengths, angles and counts would all pass with a shifted origin or a mirrored axis. | `AGENTS.UiAutomation` | T (readout scenario) |
| Capture only the window's visible frame (DWM extended frame bounds), and open an image before sharing it. | The UIA rectangle includes invisible borders and captured another app's text. | `AGENTS.UiAutomation`, `RunRecord` | H |
| A failing diagnostics bundle must not hide the original failure. | The first error is the one worth reading. | `RunRecord` | H |

## Documents and process

| Rule | Why | Stated in | Enforced |
|---|---|---|---|
| Docs are named `KEYWORD.Section[.Sub].md`; a new keyword needs a row in the conventions table first. | The name says what kind of document it is; no keyword for a single file. | `AGENTS.Conventions.Docs` | R |
| Docs that describe a change go in the same commit as the code. | Docs and code never drift apart. | `AGENTS.Conventions.Docs`, `AGENTS.Conventions.Code` | R |
| Say *why* a rule exists. | A rule without a reason can't be judged or safely changed — this document exists because of it. | `AGENTS.Conventions.Docs` | R |
| Links are relative; public docs in English; no machine paths, personal emails or employer material. | Docs must work on any clone and be safe to publish. | `AGENTS.Conventions.Docs` | R |
| Commits use the repository's public identity (repo-local `noreply` address). | The machine's global identity is a different account. | `AGENTS.Conventions.Code` | R |

## Findings while collecting

- **Contradiction fixed.** The XAML convention said "every interactive element gets `AutomationProperties.Name`",
  but today's accessibility fix *removed* the theme button's fixed name so the spoken name contains the visible
  label. The convention, the Studio checklist and the view-layer checklist now state the rule as above.
- **Rules without a reason in their source:** `x:Name` only when referenced; shared style only for two or more
  views; the 0.1 mm drag snap; part of the text-role rule. Reasons above are marked *proposed* — confirm or correct
  them in the source documents.
- **Rule stated but not met** (at collection time): no literal sizes in views — fixed after the audit.
- **Enforced by review only where a compiler check is possible:** the shell → engine boundary (`PrivateAssets` on
  the Services → engine reference would make it C).
