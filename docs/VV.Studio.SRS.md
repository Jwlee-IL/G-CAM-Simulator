# VV.Studio.SRS — software requirements specification for GCAM Studio

Scope: what the three Studio projects (`Gcam.Studio.Core`, `Gcam.Studio.Services`, `Gcam.Studio`) must do. Not
covered: the simulation engine and the original viewer `src/Gcam.Wpf`. How the requirements are met is in
[VV.Studio.SDS](VV.Studio.SDS.md); how they are verified, and their status, is in [VV.Studio](VV.Studio.md#4-verification).

Structured after IEC 62304 §5.2 (software requirements analysis). As in [VV.Studio](VV.Studio.md#1-scope-and-intended-use),
**GCAM Studio is not a medical device and no compliance is claimed**; the structure is borrowed for its discipline.

## 1. Product context

| | |
|---|---|
| Purpose | A desktop viewer for Gcam, a Monte Carlo simulator of a coded-aperture gamma camera: place sources, run the simulation, inspect the detector flood map and the decoded reconstruction, measure on both in mm. Intended use and safety class: [VV.Studio §1](VV.Studio.md#1-scope-and-intended-use). |
| Users | Engineers and reviewers. They know what a flood map and a reconstruction are; they are not assumed to know the code. |
| Inputs | A scene of point sources (isotope, lateral X / Y, distance, activity) and a photon budget. Optics are fixed defaults (`OpticsSettings`), shown read-only. |
| Outputs | Two images (flood map, reconstruction) with colour bars, the decoded peak in mm, a status line, and user measurements (distance, angle, ROI statistics). Nothing is written to disk. |
| Upward trace | Studio is subsystem SS-4 of the product concept: it implements [PR-SW-02](VV.Gcam.PRS.md#software-and-engineering-use-pr-sw) and serves user need UN-09 (engineering inspection; [VV.Gcam.URS](VV.Gcam.URS.md)). |
| Neighbouring systems | The Gcam engine, reached only through `ISimulationService` ([VV.Studio.SDS §3](VV.Studio.SDS.md#3-interfaces-between-items-532-543)); Windows (WPF, DWM title bar, UI Automation). |

## 2. Conventions

- **ID** `SR-<AREA>-<nn>`. IDs are stable: add, don't renumber; a withdrawn requirement keeps its row, marked *withdrawn*.
- Each row is one testable statement in the present tense ("is clamped", "keeps") — read it as *shall*.
- Requirements were derived from the code and the `DESIGN.*` pages as of 2026-10-01 (the code existed first; this
  is a reverse-engineered baseline, not a pre-development specification — §8).
- Requirements marked **RC** are risk-control measures (§6). Verification method, evidence and status per row are
  in the [traceability matrix](VV.Studio.md#traceability-matrix); allocation to software units is in
  [VV.Studio.SDS §7](VV.Studio.SDS.md#7-requirement-allocation).

## 3. Functional and capability requirements (§5.2.2 a)

### Run (`SR-RUN`)

| ID | Requirement |
|---|---|
| SR-RUN-01 | A run executes the simulation through `ISimulationService` off the UI thread and publishes the result, a status sentence and `State = Succeeded`. |
| SR-RUN-02 | Cancelling a run keeps the previous result, sets `State = Cancelled` with status "Cancelled — previous result kept", and re-enables editing. |
| SR-RUN-03 | Any other exception becomes `State = Failed` and status "Failed: <message>"; it is never rethrown to the UI. |
| SR-RUN-04 | Progress is accepted only while running and never moves backwards; it reads 1.0 after success. |
| SR-RUN-05 | While running, the scene is locked: add / remove source, the photon budget and source dragging are disabled. |
| SR-RUN-06 | Run is enabled only when the scene has at least one source. |
| SR-RUN-07 | Editing the scene after a run marks the shown result stale ("outdated" chip) until the next successful run. Before any run nothing is stale. |
| SR-RUN-08 | The scene → engine config is valid: MURA rank snapped to the nearest prime, non-cyclic decode, recon grid inside the fully-coded FOV, non-positive photon budget rejected. |

### Scene (`SR-SCENE`)

| ID | Requirement |
|---|---|
| SR-SCENE-01 | Source inputs are clamped: distance 200–3000 mm, activity ≤ 0 → 1 µCi, unknown isotope → Cs-137; the list label follows every edit. Photon budget < 1,000 → 1,000. |
| SR-SCENE-02 | Startup has one selected source. Adding selects the new source, offset so sources don't stack; removing selects the neighbour. |

### Image view (`SR-VIEW`)

| ID | Requirement |
|---|---|
| SR-VIEW-01 | Fit shows the whole image, aspect preserved, centred. |
| SR-VIEW-02 | At fit, each cell spans a whole number of **device** pixels (any display scaling), and the image stays centred. |
| SR-VIEW-03 | Zoom (wheel, `+` / `−`) keeps the point under the cursor fixed, is clamped to 1–32×, and full zoom-out returns to the centred fit. |
| SR-VIEW-04 | Pan (drag, arrows) cannot move the image out of view; at fit the image stays centred. |
| SR-VIEW-05 | Screen ↔ image ↔ mm mapping: row 0 at the bottom (y up), pixel *i* centred at `origin + i·step` mm, round trip exact. |
| SR-VIEW-06 | Resizing keeps the zoom; a new image size refits. |
| SR-VIEW-07 | The hovered pixel is read out as "x … mm, y … mm · value"; double-click, `0` or Home refits. |
| SR-VIEW-08 | After a run, the reconstruction header shows the decoded peak in mm. |

### Measurement and source drag (`SR-MEAS`)

| ID | Requirement |
|---|---|
| SR-MEAS-01 | Distance is the Euclidean length in mm, shown with 0.1 mm resolution. |
| SR-MEAS-02 | Angle is measured at the vertex (second point), 0–180°; a zero-length arm gives 0. |
| SR-MEAS-03 | ROI counts whole pixels **by centre**, corners in any order, clipped to the image; sum, mean and max. |
| SR-MEAS-04 | ROI values are recomputed on every new result; with no data on that pane the value is "—". |
| SR-MEAS-05 | Measurements are numbered M1, M2, …; a new one is selected; delete selects the neighbour; clear restarts numbering; a draft with the wrong point count is rejected. |
| SR-MEAS-06 | The tool hint follows the active tool. |
| SR-MEAS-07 | Gestures: drags shorter than 4 px are ignored; Esc or right-click abandons a draft; Delete on the focused heatmap removes the selected measurement; points are clamped to the image. Geometry is stored in mm, so it follows zoom and pan. |
| SR-MEAS-08 | With the Pan tool and while idle, a source marker on the reconstruction can be dragged; it lands on a 0.1 mm grid, clamped to the image extent, and marks the result stale. |

### Theme (`SR-THEME`)

| ID | Requirement |
|---|---|
| SR-THEME-01 | The top-bar toggle switches dark ↔ light; its label names the theme it switches *to*. |
| SR-THEME-02 | Switching replaces the token dictionary in place, every window repaints (`DynamicResource`), and the title bar follows (DWM). |

## 4. Non-functional requirements

### Operating environment (`SR-ENV`, §5.2.2 a, j)

| ID | Requirement |
|---|---|
| SR-ENV-01 | Studio runs on Windows 10 / 11 with the .NET 9 desktop runtime; the themed title bar is best effort (a Windows that lacks the DWM attribute ignores it without error). |
| SR-ENV-02 | The layout is designed for a 1440 × 900 work area; on a smaller work area the window starts maximised. |

### Usability and accessibility (`SR-A11Y`, §5.2.2 g)

| ID | Requirement |
|---|---|
| SR-A11Y-01 | Every interactive element has `AutomationProperties.Name`; each measurement row is named by its description ("M2 ROI on Flood: Σ 1,234"). |
| SR-A11Y-02 | `HeatmapView` has an automation peer: control type Image, class `HeatmapView`, keyboard-focusable, `ItemStatus` = zoom + readout. |
| SR-A11Y-03 | Keyboard paths: Enter runs (default button); heatmap zoom / pan / fit by key; tool picker access keys (Alt+P / D / A / R); sources movable via X / Y fields. |
| SR-A11Y-04 | The status line is a polite live region; state and selection are never signalled by colour alone. |

### Security and data (`SR-SEC`, §5.2.2 e, h)

| ID | Requirement |
|---|---|
| SR-SEC-01 | Studio opens no network connection and reads or writes no user files; scene, results and measurements live in memory only and are gone when the app closes. |

### Architecture constraints (`SR-ARCH`)

Design constraints that make the other requirements verifiable (testability without a UI stack, one engine entry
point). They are kept as requirements so that a change breaking them fails verification, not just review.

| ID | Requirement |
|---|---|
| SR-ARCH-01 | `Gcam.Studio.Core` references no WPF type. |
| SR-ARCH-02 | Only `Gcam.Studio.Services` references the simulation engine (`Gcam.Simulation`); the UI reaches it only through `ISimulationService`. |
| SR-ARCH-03 | View code-behind is `InitializeComponent()` only; views use theme keys, not literal colours or sizes. |
| SR-ARCH-04 | Studio tests reference `Gcam.Studio.Core` only (no WPF in tests). |

**39 requirements** (36 functional / usability / architecture + 3 environment and security).

## 5. Inputs, outputs, messages (§5.2.2 b–d)

### Inputs and their valid ranges

| Input | Unit | Valid range / handling | Requirement |
|---|---|---|---|
| Isotope | — | one of the engine's `Isotopes.All`; anything else → Cs-137 | SR-SCENE-01 |
| Source X, Y | mm | free in the fields; dragged markers clamped to the reconstruction extent, 0.1 mm grid | SR-SCENE-01, SR-MEAS-08 |
| Source distance | mm | 200–3000 (clamped) | SR-SCENE-01 |
| Activity | µCi | > 0 (≤ 0 → 1) | SR-SCENE-01 |
| Photon budget | photons | ≥ 1,000 (clamped in the UI); ≤ 0 rejected by the engine builder | SR-SCENE-01, SR-RUN-08 |
| Optics | mm / rank | read-only defaults in this version | — |
| Measurement points | mm | clamped to the image extent; 2 points (distance, ROI) or 3 (angle) | SR-MEAS-05, SR-MEAS-07 |

### Outputs

| Output | Format | Requirement |
|---|---|---|
| Flood map, reconstruction | heatmap (viridis, linear min–max) + colour bar; row 0 at the bottom | SR-VIEW-01…06 |
| Hovered pixel | "x 1.2 mm, y −3.4 mm · 56.7" | SR-VIEW-07 |
| Decoded peak | "peak (x, y) mm", 0.1 mm | SR-VIEW-08 |
| Distance / angle / ROI | "12.3 mm" · "67.0°" · "Σ 1,234" with px count, mean, max | SR-MEAS-01…04 |

### Status messages and warnings

| Situation | What the user sees | Requirement |
|---|---|---|
| Ready | "Ready" | — |
| Running | "Simulating N photons…", progress bar, Cancel button | SR-RUN-04 |
| Succeeded | "<counts> effective counts in <t> s · peak at (x, y) mm, ghost margin <c>" (or "no decode") | SR-RUN-01 |
| Cancelled | "Cancelled — previous result kept" | SR-RUN-02 |
| Failed | "Failed: <message>" | SR-RUN-03 |
| Images no longer match the scene | "outdated" chip on the images | SR-RUN-07 |
| ROI before any run / outside the pixels | "—" · "no image yet" / "no pixel centres inside" | SR-MEAS-04 |

Every status is carried by text, not colour alone (SR-A11Y-04).

## 6. Risk-control requirements (§5.2.3)

Hazardous situations and their analysis are in [VV.Studio §6](VV.Studio.md#risk-table-illustrative-iec-62304-7-style).
The requirements that implement the controls:

| Hazardous situation | Risk-control requirements (RC) |
|---|---|
| Wrong position read off an image | SR-VIEW-05, SR-VIEW-07 |
| Old image taken as current | SR-RUN-02, SR-RUN-07 |
| ROI sum from the wrong image or frame | SR-MEAS-04 |
| False precision | SR-MEAS-01, SR-MEAS-03, SR-MEAS-08 |
| App hangs or crashes on a long or failing run | SR-RUN-01, SR-RUN-02, SR-RUN-03 |
| Status missed by colour-blind or screen-reader users | SR-A11Y-04 |

## 7. Not required (explicit exclusions)

Stated so that their absence is not read as a gap in verification:

- Editing optics (mask rank, pitch, distances) — shown read-only in this version.
- Saving or loading scenes, exporting images or measurements (SR-SEC-01 forbids file I/O today).
- Creating measurements from the keyboard — known gap AN-01, planned.
- High-contrast mode — known gap AN-03, planned.
- Any physics accuracy claim — the engine's own suite covers it ([VV.Studio §1](VV.Studio.md#1-scope-and-intended-use)).

## 8. Re-evaluation (§5.2.5–5.2.6)

- This baseline was written after the code; each row was checked against the code on 2026-10-01 and has at least
  one verification entry in the matrix. Rows whose evidence is *partial* are listed in
  [VV.Studio §4](VV.Studio.md#coverage-summary).
- A change that adds or alters behaviour adds or edits its row here **and** its matrix row in VV.Studio in the
  same commit ([AGENTS.Conventions.Docs](AGENTS.Conventions.Docs.md#keeping-docs-in-sync)).
- When the risk table in VV.Studio §6 changes, re-check §6 above.
