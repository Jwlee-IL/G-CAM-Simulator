# VV.Studio.SRS — software requirements specification for GCAM Studio

Scope: what the three Studio projects (`Gcam.Studio.Core`, `Gcam.Studio.Services`, `Gcam.Studio`) must do. Not
covered: the simulation engine and the original viewer `src/Gcam.Wpf` (removed in TODO-12 (2026-10-02); last present at `85b2ed1`). How the requirements are met is in
[VV.Studio.SDS](VV.Studio.SDS.md); how they are verified, and their status, is in [VV.Studio](VV.Studio.md#4-verification).

Structured after IEC 62304 §5.2 (software requirements analysis). As in [VV.Studio](VV.Studio.md#1-scope-and-intended-use),
**GCAM Studio is not a medical device and no compliance is claimed**; the structure is borrowed for its discipline.

**At a glance**
- Active requirements (plus nine withdrawn rows) cover navigation, plotting, spectrum, acquisition, scene, image view, measurement, theme (functional, §3) and environment,
  accessibility, security, architecture (§4). Each is one testable present-tense statement.
- Inputs, outputs and every status message are listed with their valid ranges (§5); risk control
  (the illustrative safety class B) maps six hazardous situations to the requirements that control them (§6);
  what Studio deliberately does not do is in §7.
- Verification status per requirement is in [VV.Studio §4](VV.Studio.md#4-verification).

## 1. Product context

| | |
|---|---|
| Purpose | A desktop viewer for Gcam, a Monte Carlo simulator of a coded-aperture gamma camera: place sources, run the simulation, inspect the detector flood map and the decoded reconstruction, measure on both in mm. Intended use and safety class: [VV.Studio §1](VV.Studio.md#1-scope-and-intended-use). |
| Users | Engineers and reviewers. They know what a flood map and a reconstruction are; they are not assumed to know the code. |
| Inputs | A scene of point sources (isotope, lateral X / Y, distance, activity), preset live time, acquisition speed and an optional fixed Monte Carlo seed. Optics have editable physical defaults (`OpticsSettings`), frozen during acquisition and while data exist until Reset; decoder focus is an Imaging view setting. |
| Outputs | Imaging flood/reconstruction with colour bars, found coordinates and measurements (distance, angle, ROI); Spectrum histogram/bands; Waveform traces/events; Detector face/gain geometry; retained-flood focus curves with a separate optional external range; acquisition status. Nothing is written to disk. |
| Upward trace | Studio is subsystem SS-4 of the product concept: it implements [PR-SW-02](VV.Gcam.PRS.md#software-and-engineering-use-pr-sw) and serves user need UN-09 (engineering inspection; [VV.Gcam.URS](VV.Gcam.URS.md)). |
| Neighbouring systems | The Gcam engine, reached through Core acquisition, spectrum, imaging, waveform, detector-face and focus-sweep service contracts ([VV.Studio.SDS §3](VV.Studio.SDS.md#3-interfaces-between-items-532-543)); Windows (WPF, DWM title bar, UI Automation). |

## 2. Conventions

- **ID** `SR-<AREA>-<nn>`. IDs are stable: add, don't renumber; a withdrawn requirement keeps its row, marked *withdrawn*.
- Each row is one testable statement in the present tense ("is clamped", "keeps") — read it as *shall*.
- Requirements were derived from the code and the `DESIGN.*` pages as of 2026-10-01 (the code existed first; this
  is a reverse-engineered baseline, not a pre-development specification — §8).
- Requirements marked **RC** are risk-control measures (§6). Verification method, evidence and status per row are
  in the [traceability matrix](VV.Studio.md#traceability-matrix); allocation to software units is in
  [VV.Studio.SDS §7](VV.Studio.SDS.md#7-requirement-allocation).

## 3. Functional and capability requirements (§5.2.2 a)

### Workspaces (`SR-NAV`)

| ID | Requirement |
|---|---|
| SR-NAV-01 | One shared scene, optics, detector inputs, background ratio, live time, speed, seed, acquisition state and cumulative snapshot feed all registered workspaces; imaging owns its measurements and peak reading. |
| SR-NAV-02 | With at least two registered workspaces, a segmented switch exposes their titles and unique `Workspace.*` AutomationIds; activating a workspace through its checked state, click or Ctrl+1…4 changes the selected workspace. Unavailable indices do nothing, and a run retains selection. A single workspace has no switch. |
| SR-NAV-03 | Physical run inputs are editable only while no acquired data exist (SR-RUN-12); workspace view settings and selection are editable in every state and only re-project retained data. |

### Plot surface (`SR-PLOT`)

| ID | Requirement |
|---|---|
| SR-PLOT-01 | A series supplies finite Y with increasing X or origin + positive step, name, Line / Area / Histogram kind and colour role; bands and labelled X markers are available. Input is capped at 10 million samples per series. |
| SR-PLOT-02 | Linear and log10 Y map between pixels and data; log counts ≤1 clamp to 1. Linear ticks use 1–2–5 steps with the decimals the step needs and grouped thousands ("0 10 20", "1,000"), log decades have 2…9 minor ticks, and labels follow the engineering notation of the image colour scales.
| SR-PLOT-03 | A pyramid built once per published dataset preserves exact per-column extrema, including short series, singleton data and boundary ranges; navigation does not rescan the whole trace. |
| SR-PLOT-04 | Plot X zoom / pan / reset have pointer and keyboard paths; read-only pointer readout, a focus ring, theme brushes and an automation peer are exposed. Resize retains X navigation. |
| SR-PLOT-05 | In a visible WPF window a 10-million-sample trace has CPU redraw ≤16 ms after a zoom and after a resize, including axes, query, geometry and drawing commands. Dispatcher / composition latency is recorded separately. |
| SR-PLOT-06 | Histogram data has N+1 finite increasing edges (or origin + positive width) and nonnegative counts. Bins at least two device pixels wide draw filled steps; narrower bins use exact column extrema. Zero bins occupy their full width at the floor. |
| SR-PLOT-07 | Same-full-range data updates preserve X zoom / pan. Changed ranges, cleared acquisitions and double-click / 0 / Home resets fit X. Log Y and resize preserve X. |
| SR-PLOT-08 | Y follows visible X with 8 percent linear headroom or the next half decade in log mode. Data updates only grow the top; navigation and log changes recompute it. |
| SR-PLOT-09 | Histogram hover highlights a half-open bin and draws its centre cursor; readout gives centre, bounds and counts with host units / formats. Default precision follows bin width. Line hover retains continuous coordinates. |
| SR-PLOT-10 | Band and marker labels sit in a label strip above the plot area, never over data: band labels centre on their bands, marker labels start right of their marker with a leader line, both clamp to the plot width and take additional collision rows. A plot may hide its marker labels (lines stay). A label wider than the plot is cut with an ellipsis on one line.
| SR-PLOT-11 | A separate offscreen STA test, opt-in with GCAM_RENDER_SNAPSHOTS=1, renders fixed-spectrum dark / light, full / 662-keV zoom PNGs without windows, focus or mouse input. |

### Spectrum (`SR-SPEC`)

| ID | Requirement |
|---|---|
| SR-SPEC-01 | Spectrum displays a live 256-bin stepped, filled histogram of the shared acquisition's measured event deposits, with measured energy in keV and acquired counts; it adds no synthetic noise floor or independent event pool. |
| SR-SPEC-02 | Measured energy applies the acquisition's fixed pixel gain to its true deposit, then smears once through the default GAGG(Ce), S13360-3050 and CSP + CR-RC chain's `FrontEndModel`. The read-only chain resolution at 662 keV excludes pixel gain spread; no resolution override is offered. |
| SR-SPEC-03 | Optional pile-up sums gained amplitudes whose actual arrival gaps are below the chain-derived resolving time, extending the interval after each pulse, then smears the summed pulse once; toggling it reprocesses retained events. |
| SR-SPEC-04 | Each emission window spans E ± N·FWHM(E); adjacent lines separated by less than FWHM at their mean merge into one labelled band spanning their windows. Resolved lines keep separate bands even if their windows overlap. |
| SR-SPEC-05 | The emission table gives isotope, every grouped line energy, window limits, counts and share selected by bin centre; the total in-window share counts each bin once across all bands. A band whose lines are all X-rays in the engine isotope table is named by their emitter from that table with the source in brackets ("Ba K X-rays (Cs-137)"; the photons come from the daughter barium). Numbers are right-aligned under right-aligned headers that share measured numeric column widths and padding, with at least 8 DIP between adjacent cells at the supported window sizes. |
| SR-SPEC-06 | Log Y, positive finite window multiplier (default 1.5) and pile-up are view settings: they reuse acquired events without starting acquisition or changing the acquisition state. |
| SR-SPEC-07 | Same events, settings and seed produce identical histogram counts regardless of snapshot partition or pile-up toggle replay; new events are processed incrementally and CPU processing runs in a service worker. |
| SR-SPEC-08 | Spectrum exposes the plot and readout, table and right-panel controls with accessible names and AutomationIds `Spectrum.Plot`, `Spectrum.Lines`, `Spectrum.LogY`, `Spectrum.Window` and `Spectrum.PileUp`. |
| SR-SPEC-09 | Counts carry explicit bin edges; the graph uses keV and counts. Selecting an emission-window row requests its range plus one window width on each side, clamped to the axis. Snapshot replacement retains selection without another zoom request. |

### Acquisition (`SR-RUN`)

| ID | Requirement |
|---|---|
| SR-RUN-01 | *Withdrawn* — batch run / Succeeded; replaced by SR-RUN-09, SR-RUN-19. |
| SR-RUN-02 | *Withdrawn* — cancel / previous run retained; replaced by SR-RUN-10. |
| SR-RUN-03 | Any other exception becomes `State = Failed` and status "Failed: <message>"; it is never rethrown to the UI. |
| SR-RUN-04 | *Withdrawn* — photon progress; replaced by SR-RUN-11. |
| SR-RUN-05 | *Withdrawn* — batch input locking; replaced by SR-RUN-12. |
| SR-RUN-06 | *Withdrawn* — batch command availability; replaced by SR-RUN-13. |
| SR-RUN-07 | *Withdrawn* — batch result staleness; replaced by SR-RUN-14. |
| SR-RUN-08 | *Withdrawn* — photon-budget config; replaced by SR-RUN-15. |
| SR-RUN-09 | Start without acquired data executes fresh list-mode MC transport and decoding off the UI thread through `IAcquisitionService`, with the acquisition's Monte Carlo seed (SR-RUN-25); the state is Acquiring until Stopped, Completed or Failed. |
| SR-RUN-10 | Stop cooperatively ends the current acquisition segment, retains all acquired data, measurements and the session, and sets Stopped; physical inputs stay locked (SR-RUN-12). |
| SR-RUN-11 | Progress equals acquired live time / current preset live time, never decreases within a segment and reaches 1 at Completed; raising the preset (SR-RUN-24) lowers it accordingly. |
| SR-RUN-12 | Physical run inputs — sources (add / remove, isotope, X, Y, distance, activity), optics, reflector gap, gain σ / seed, background ratio, detection chain and the Monte Carlo seed — are editable only in Empty or after a failure without data. Any other change is refused by the view model, not only disabled in the view. While Acquiring, live time and speed are locked too. |
| SR-RUN-13 | Without acquired data, Start is enabled when at least one source exists. With data the button reads Continue and is enabled only in Stopped or Completed when the preset exceeds the acquired live time. The command re-checks its condition when invoked; it is not the window's default button. |
| SR-RUN-14 | *Withdrawn* — outdated / stale result marking; replaced by SR-RUN-12, SR-RUN-23, SR-RUN-26. |
| SR-RUN-15 | The scene config uses nearest-prime rank, non-cyclic decoding and a reconstruction grid inside the FCFOV; non-finite or non-positive preset live time and speed are rejected by the service. |
| SR-RUN-16 | Immutable cumulative snapshots carry live time, integer counts, flood, reconstruction, estimate and the same fresh event list (pixel, true deposit in keV, Poisson arrival time in s); each event is used once. The flood adds one count at each ComptonCrystalDetector event’s Argmax pixel, so in-crystal Compton scatter mispositioning is part of the image. A decay of an isotope with correlated gammas (Co-60 with its 1173 / 1332 keV angular correlation, Na-22 with its back-to-back annihilation pair) is one event: the deposits of all its detected gammas summed, at the largest-deposit pixel over their merged interaction sites, with one arrival time (true-coincidence summing). |
| SR-RUN-17 | At 4 Hz the accumulated flood is re-decoded on its fixed grid and imaging measurements refresh without replacing their geometry. A slow consumer receives the latest cumulative snapshot. |
| SR-RUN-18 | If MC cannot supply rate × speed, live time advances only through the acquired prefix and the status appends "MC-limited ×k" with achieved live seconds per wall second. |
| SR-RUN-19 | Preset live time defaults to 60 s and speed to ×10; reaching the preset automatically sets Completed. |
| SR-RUN-20 | Studio explicitly supplies entrance 0.15 mm steel-equivalent, backing 2 mm, default reflector gap 0.1 mm, gain σ 3% and gain seed 1. Entrance and backing are read-only; gap, gain σ and seed are editable run inputs. Existing engine scene-builder defaults are unchanged. |
| SR-RUN-21 | Snapshots retain their acquisition detector inputs. True deposits are preserved; one shared deterministic measurement response applies the CrystalUniformity gain pattern before chain smearing. Gain inputs are locked while data exist, so the recorded detector response cannot change. |
| SR-RUN-22 | A finite nonnegative detected background/source ratio (default 0) adds an independent Poisson process at BSR × source rate. Each fresh background deposit comes from the existing unmasked cosine-flux crystal response at 200 keV and is placed according to the uniform detected pedestal. At zero BSR the seeded source stream is unchanged. |
| SR-RUN-23 | Continue (Start with data) resumes the same acquisition: the same list-mode source and random streams, the already-drawn look-ahead event, the events, flood, live-time clock, seed and acquisition identity. Live time does not advance while stopped. The continued event stream equals, event for event, that of an uninterrupted acquisition to the same live time, and the workspaces append to their retained processing. |
| SR-RUN-24 | In Stopped and Completed the preset live time may be raised to continue; a value below the acquired live time is rejected with a message and the previous value restored. Speed only paces the acquisition and is editable in every state but Acquiring. |
| SR-RUN-25 | Each new acquisition draws a new Monte Carlo seed unless the Seed input fixes one (blank: new; a nonnegative integer: fixed). Every random stream of the acquisition derives from it, the same seed reproduces the acquisition, and the status line shows it. |
| SR-RUN-26 | Reset — enabled only in Stopped, Completed or Failed with data, never while acquiring — discards the acquisition without a confirmation: session, snapshot, images, spectrum, scope, focus sweep and the scene frozen at Start. The state returns to Empty ("Ready") and the physical inputs unlock; measurement shapes are kept. |
| SR-RUN-27 | A failure after data keeps the last published data visible and locked and offers only Reset; a failure before any data leaves the inputs editable and Start enabled. Both show "Failed: <message>". |
| SR-RUN-28 | The status line's rate is the observed count rate, counts / live time — the definition the Detector workspace uses. |

### Scene (`SR-SCENE`)

| ID | Requirement |
|---|---|
| SR-SCENE-01 | Source inputs are clamped: distance 200–3000 mm, activity ≤ 0 → 1 µCi, unknown isotope → Cs-137; the list label follows every edit. |
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
| SR-VIEW-07 | The hovered pixel is read out as "x … mm, y … mm · value unit", the value to four significant digits with grouped thousands (integers in full); double-click, `0` or Home refits.
| SR-VIEW-08 | After a run, the reconstruction header shows the decoded peak in mm; when the selected channel has two or more found peaks it shows their number ("2 peaks found") instead of one of them.
| SR-VIEW-09 | The colour bar and readout under an image span the width the image is drawn at (after whole-device-pixel cell snapping), as do the Detector face's colour bar and legend. |

### Per-nuclide imaging (`SR-IMG`)

| ID | Requirement |
|---|---|
| SR-IMG-01 | One shell-owned positive finite window multiplier N (default 1.5) is editable in Spectrum and Imaging. Both use the same per-energy FWHM bands, including unresolved-line grouping. Changing N re-filters retained acquisition events without a new acquisition. |
| SR-IMG-02 | The worker accumulates a primary-line energy-window flood per scene isotope, using the acquisition's deterministic measured energy response, and decodes it. All retains the acquisition image of every event. |
| SR-IMG-03 | An All / isotope selector changes the displayed flood, reconstruction, colour bar, readout, peak reading and ROI statistics together. Selecting a channel does not run transport or decoding on the UI thread. |
| SR-IMG-04 | Each isotope channel reports up to its source count of separated found peaks, refined with the pipeline-default sub-cell interpolation. Found peaks are read-only diamonds labelled "Found <isotope>" and listed as coordinates; true-source rings are display-only. Full chips fit within the visible image/control intersection, with at least 4 DIP between chips and clearance from marker crosshairs; displaced labels use neutral leaders and never move marker centres. A chip that cannot fit without overlap is omitted, retaining its coordinate/measurement list entry. Layout recalculates on resize/zoom/pan. All shows the union of found peaks. Association is checked below one reconstruction-grid diagonal; localization precision is measured separately and is not implied by the marker. |
| SR-IMG-05 | A Compton strip view toggle subtracts the simultaneous raw higher-channel floods per pixel, clamped at zero. H-only calibration at acquisition start and after N changes uses at least 20,000 accepted events per contaminating isotope, with R = low-window / own-window counts. The panel lists each R and states the one-pass scalar model limitation: exact for a clean pair, approximate for 3+ overlapping contaminants. |
| SR-IMG-06 | Channel building, H-only calibration and decoding run on a worker and publish separate processing costs. The scene and optics frozen at Start determine processing for the whole acquisition, including continued segments. Cancelled or late processing cannot replace a newer view. |

### Measurement (`SR-MEAS`)

| ID | Requirement |
|---|---|
| SR-MEAS-01 | Distance is the Euclidean length in mm, shown with 0.1 mm resolution. |
| SR-MEAS-02 | Angle is measured at the vertex (second point), 0–180°; a zero-length arm gives 0. |
| SR-MEAS-03 | ROI counts whole pixels **by centre**, corners in any order, clipped to the image; sum, mean and max. |
| SR-MEAS-04 | ROI values are recomputed on every new result; with no data on that pane the value is "—". |
| SR-MEAS-05 | Measurements are numbered M1, M2, …; a new one is selected; delete selects the neighbour; clear restarts numbering; a draft with the wrong point count is rejected. |
| SR-MEAS-06 | The tool hint follows the active tool. |
| SR-MEAS-07 | Gestures: drags shorter than 4 px are ignored; Esc or right-click abandons a draft; Delete on the focused heatmap removes the selected measurement; points are clamped to the image. Geometry is stored in mm, so it follows zoom and pan. |
| SR-MEAS-08 | *Withdrawn* — source-marker drag; source positions are physical inputs (SR-RUN-12), edited in the left panel. |

### Theme (`SR-THEME`)

| ID | Requirement |
|---|---|
| SR-THEME-01 | The top-bar toggle switches dark ↔ light; its label names the theme it switches *to*. |
| SR-THEME-02 | Switching replaces the token dictionary in place, every window repaints (`DynamicResource`), and the title bar follows (DWM). |

### Editable optics and decoder focus

| ID | Requirement |
|---|---|
| SR-OPT-01 | Rank, cell pitch, mask–detector distance, pixels per side and pixel pitch are editable physical run inputs. One validated effective record feeds configuration and preset matching. The prime selector displays the effective supported rank. Physical editors are editable only while no acquired data exist (SR-RUN-12). |
| SR-OPT-02 | Sharp (default), Baseline, Wide FOV and High-res apply their historical engineering geometries atomically. The selector shows Custom after divergence. Selecting a geometry does not move scene sources or alter decoder focus; performance is conditional on scene, focus, counts and isotope channels. |
| SR-OPT-03 | Studio rejects non-finite, malformed, out-of-policy and cross-field-invalid inputs before transport or projection. Policy ranges are in §5; every scene source lies beyond the mask front face. Refocus uses the acquired physical settings. Grid allocation is bounded at 128×128. Errors remain visible with sections collapsed and Start expands error sections. |
| SR-OPT-04 | Decoder focal plane is an Imaging view setting. At Acquiring, Stopped and Completed it reprojects All and isotope/stripped reconstructions, estimates and found peaks from retained floods/events without new transport, measurement, random draws or calibration. Latest revision wins; the acquisition state is unchanged. Focus changes clear reconstruction measurements/drafts with a visible explanation while retaining flood measurements. All found markers remain the union of isotope-channel markers. |
| SR-OPT-05 | Display resolution element cF/D, nominal cyclic field ±pcF/(2D), samples per mask cell, detector coverage in periods and physical mask width. These are geometry values, not localization/usable-field guarantees; no pass/fail glyphs or depth-reach extrapolation. A one-line caption states that precision is position-dependent conditional evidence; the 1 m position-sweep figures are its tooltip and accessible help text, and are recorded in [VV.Studio.Imaging](VV.Studio.Imaging.md).
| SR-OPT-06 | Detection chain, Physical optics and Detector collapse independently in the shared panel, each with a single-line effective summary; Physical optics and Detector start collapsed, the chain expanded, so the default left panel fits 1280×800 without scrolling. The Imaging right panel holds collapsible Decoder focal plane, Imaging channel and Focus sweep sections (the sweep opens when a result arrives) and scrolls when needed; Tools and the measurement table sit under the image pair and take the height the width-limited images leave. Controls inherit theme tokens and have accessible labels, units and stable AutomationIds.

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
| SR-A11Y-05 | Opaque retained-value disabled text has contrast ≥4.5:1 against Surface, Canvas and Raised in both themes, while remaining dimmer than Secondary/Primary text on Surface. Locked inputs retain their disabled state and subdued fill/borders. Disabled action templates with separate opacity are outside this retained-value target. |

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
| SR-ARCH-02 | Only `Gcam.Studio.Services` references the simulation engine (`Gcam.Simulation`); the UI reaches acquisition, spectrum, imaging, waveform, detector-pattern and focus processing through their Core service contracts. |
| SR-ARCH-03 | View code-behind is `InitializeComponent()` only; views use theme keys, not literal colours or sizes. |
| SR-ARCH-04 | Studio tests reference `Gcam.Studio.Core` only (no WPF in tests). |

Seven withdrawn rows are retained with stable IDs; Detector, focus and Waveform requirements extend this baseline below.

## 5. Inputs, outputs, messages (§5.2.2 b–d)

### Inputs and their valid ranges

| Input | Unit | Valid range / handling | Requirement |
|---|---|---|---|
| Isotope | — | one of the engine's `Isotopes.All`; anything else → Cs-137 | SR-SCENE-01 |
| Source X, Y | mm | free in the fields while editable | SR-SCENE-01, SR-RUN-12 |
| Source distance | mm | 200–3000 (clamped) | SR-SCENE-01 |
| Activity | µCi | > 0 (≤ 0 → 1) | SR-SCENE-01 |
| Preset live time | s | finite > 0; default 60; invalid UI input → 60, invalid service input rejected; with data only ≥ the acquired live time | SR-RUN-15, SR-RUN-19, SR-RUN-24 |
| Speed | live s / wall s | finite > 0; default 10; invalid UI input → 10, invalid service input rejected | SR-RUN-15, SR-RUN-19, SR-RUN-24 |
| Seed | integer | blank (default: a new seed per acquisition) or a nonnegative integer; anything else blocks Start with a message | SR-RUN-25 |
| Gain σ / seed | % / integer | σ finite ≥ 0, default 3%; invalid UI σ → 3%; seed default 1 | SR-RUN-20, -21 |
| Reflector gap | µm editor, mm configuration | finite 0 ≤ gap < pending pixel pitch; default 100 µm; invalid text blocks Start, revalidated after pitch edits | SR-DET-01 |
| Focus sweep K | integer | 1–4, default 1; 81 planes uniform in inverse detector-referenced distance from D+30 to 3000 mm | SR-FOCUS-01, -02 |
| External surface range | mm from detector | optional, finite positive measurement; Use as focus requires valid decoder focus | SR-FOCUS-05 |
| Background BSR | detected background / source | finite ≥ 0, default 0; invalid UI input → 0; invalid service input rejected | SR-RUN-22 |
| Physical optics | mm / integer | ranks 5, 7, 11, 13, 17, 19, 23; N integer 4–64; finite D ≥1, pitches ≥0.05; pixel pitch > reflector gap; source z > D+5 (10 mm slab); invalid edits block Start | SR-OPT-01, -03 |
| Decoder focus | mm from detector | finite, > acquired D; projected grid ≤128 cells per side; invalid edits retain the last valid view | SR-OPT-03, -04 |
| Spectrum window N | × FWHM(E) | positive finite; default 1.5; invalid numeric value → 1.5 | SR-SPEC-04, -06 |
| Spectrum log Y / pile-up | boolean | defaults true / false; view settings | SR-SPEC-03, -06 |
| Measurement points | mm | clamped to the image extent; 2 points (distance, ROI) or 3 (angle) | SR-MEAS-05, SR-MEAS-07 |

### Outputs

| Output | Format | Requirement |
|---|---|---|
| Flood map, reconstruction | heatmap (viridis, linear min–max) + colour bar; row 0 at the bottom | SR-VIEW-01…06 |
| Hovered pixel | "x 1.2 mm, y −3.4 mm · 56.7" | SR-VIEW-07 |
| Decoded peak | "peak (x, y) mm", 0.1 mm | SR-VIEW-08 |
| Spectrum | 256-bin stepped histogram, measured energy (keV) / acquired counts; line bands and emission-window table | SR-SPEC-01, -04, -05 |
| Front-end chain | read-only chain name, resolution FWHM % at 662 keV and resolving time in ns | SR-SPEC-02, -03 |
| Distance / angle / ROI | "12.3 mm" · "67.0°" · "Σ 1,234" with px count, mean, max | SR-MEAS-01…04 |

### Status messages and warnings

| Situation | What the user sees | Requirement |
|---|---|---|
| Ready (Empty) | "Ready" after launch and after Reset; Start | SR-RUN-26 |
| Acquiring | "t = 12.0 s of 60 s · 1,834 counts · 153 cps · seed 81236", live-time progress, Stop button; optional "MC-limited ×k"; the rate is counts / live time | SR-RUN-11, SR-RUN-18, SR-RUN-25, SR-RUN-28 |
| Completed | "Completed · t = 60.0 s of 60 s · <counts> counts · <rate> cps · seed <n>"; Continue enabled once the preset is raised; Reset | SR-RUN-19, SR-RUN-24 |
| Stopped | "Stopped · t = <t> s of 60 s · <counts> counts · <rate> cps · seed <n>"; acquired data retained; Continue, Reset | SR-RUN-10, SR-RUN-23 |
| Failed | "Failed: <message>"; with data only Reset | SR-RUN-03, SR-RUN-27 |
| Preset below the acquired live time | "The preset cannot be below the acquired live time (<t> s)." in the status bar; previous value restored | SR-RUN-24 |
| Invalid seed | "The seed must be blank (new each acquisition) or a nonnegative integer." in the status bar | SR-RUN-25 |
| Spectrum processing failure | "Spectrum failed: <message>"; acquired data retained | SR-SPEC-01, -07 |
| ROI before any run / outside the pixels | "—" · "no image yet" / "no pixel centres inside" | SR-MEAS-04 |

Every status is carried by text, not colour alone (SR-A11Y-04).

## 6. Risk-control requirements (§5.2.3)

Hazardous situations and their analysis are in [VV.Studio §6](VV.Studio.md#risk-table-illustrative-iec-62304-7-style).
The requirements that implement the controls:

| Hazardous situation | Risk-control requirements (RC) |
|---|---|
| Wrong position read off an image | SR-VIEW-05, SR-VIEW-07 |
| Old image taken as current | SR-RUN-10, SR-RUN-12, SR-RUN-26 |
| ROI sum from the wrong image or frame | SR-MEAS-04 |
| False precision | SR-MEAS-01, SR-MEAS-03 |
| App hangs or crashes on a long or failing run | SR-RUN-09, SR-RUN-10, SR-RUN-03 |
| Status missed by colour-blind or screen-reader users | SR-A11Y-04 |

## 7. Not required (explicit exclusions)

Stated so that their absence is not read as a gap in verification:

- Saving or loading scenes, exporting images or measurements (SR-SEC-01 forbids file I/O today).
- Creating measurements from the keyboard — known gap AN-01, planned.
- High-contrast mode — known gap AN-03, planned.
- Four-channel Anger position readout, spatial SiPM pitch and optical light sharing, including crosstalk-dependent crystal identification, are excluded from the current direct-crystal readout. Correlated cascades are modelled for Co-60 and Na-22 only; Ir-192's cascades are emitted as independent lines.
- Any physics accuracy claim — the engine's own suite covers it ([VV.Studio §1](VV.Studio.md#1-scope-and-intended-use)).

## 8. Re-evaluation (§5.2.5–5.2.6)

- This baseline was written after the code; each row was checked against the code on 2026-10-01 and has at least
  one verification entry in the matrix. Rows whose evidence is *partial* are listed in
  [VV.Studio §4](VV.Studio.md#coverage-summary).
- A change that adds or alters behaviour adds or edits its row here **and** its matrix row in VV.Studio in the
  same commit.
- When the risk table in VV.Studio §6 changes, re-check §6 above.

## Waveform and shared detection chain (2026-10-02)

These requirements replace the earlier exclusion of the Waveform workspace and read-only detection-chain selection. Four-channel position reconstruction remains excluded; the scope models the summed energy channel. Implementation verification is recorded separately in [VV.Studio.Waveform](VV.Studio.Waveform.md).

| ID | Requirement | Verification |
|---|---|---|
| SR-CHAIN-01 | A shared scintillator / photosensor / preamp selection shall be frozen at Start in the acquisition's detector settings. Measurement, spectrum bands/grouping/label, isotope windows/calibration and waveform shall use that acquired chain. | WaveformServiceTests.Response_AgreesAcrossWaveformSpectrumAndImaging; MixedFieldCalibration_UsesAcquiredChainAndInvalidatesRatios (Evidence) |
| SR-CHAIN-02 | Chain editing shall be disabled during acquisition and while acquired data exist; retained data shall never be remeasured with other settings. | WaveformWorkspaceTests.Chain_LockedWithData_ScopeUsesAcquiredChain; ActiveAcquisition_DisablesPhysicalChainChanges; offscreen chain selector assertions |
| SR-CHAIN-03 | Studio shall explicitly map GAGG(Ce), NaI(Tl), LYSO, CsI(Tl) and BGO to their transport material. GAGG shall remain the default; other scintillators are what-if comparisons. CsI(Tl) shall use validated CsI host transport data with the Tl activator omitted. Unmapped scintillators shall fail before acquisition. | CsIMaterialTests; WaveformServiceTests.Chain_SetsTransportMaterialWithoutChangingEngineDefaults; Chain_RejectsUnsupportedMaterial; AcquisitionServiceTests.CsISelection_IsCapturedByTheRealAcquisition; WaveformWorkspaceTests.CsI_IsOfferedAndRequiresResetForANewAcquisition |
| SR-WAVE-01 | Waveform shall display ADC and integer-shaper traces (CR-RC Q12 output displayed as fractional codes) of the array-wide energy channel, sharing a navigable time axis, in a default 10 µs window with 20% pretrigger. Event labels appear above the ADC plot only; both plots retain their event marker lines, with full details in the event list. | WaveformServiceTests.Window_PreservesRequestedLengthAndPrecedingPulseAcrossPixels; offscreen real10us renders, shared navigation and upper-only label assertions |
| SR-WAVE-02 | A user shall select latest / next / zero-based acquired event index. Window events shall retain index, pixel, true deposit and absolute acquired time. Default scope time shall preserve recorded arrivals. | WaveformWorkspaceTests.HiddenScope_DoesNotGenerateAndHeldTriggerReusesWindow; WaveformServiceTests.RetainedMonteCarloEvents_PreserveAssociationAndTime |
| SR-WAVE-03 | Rate study shall deterministically re-space retained events in order at a labelled simulated detected rate for the energy channel; it shall not alter events, counts, live time, other workspaces or the acquisition state. | WaveformServiceTests.RateStudy_IsDeterministicAndLeavesAcquisitionUntouched; WaveformWorkspaceTests.ScopeFailure_IsVisibleAndLocalControlsKeepTheAcquisition |
| SR-WAVE-04 | Realistic stimulus amplitudes shall apply acquired pixel gain and the shared index-addressed chain response once, with no second intrinsic raster smearing. Labels shall distinguish the ADC simulation's shaped heights from the analytic MCA. | WaveformServiceTests.Response_AgreesAcrossWaveformSpectrumAndImaging; Rasterizer_MatchesLegacyWithoutSecondIntrinsicSmear |
| SR-WAVE-05 | Ideal shaper stimulus shall remove chain smearing, analog and ADC noise, and finite rise, retaining fixed pixel gain, selected tail and selected filter (Q12 fractional-state CR-RC or integer trapezoid). | WaveformServiceTests.Ideal_HasNoNoiseOrSmearAndKeepsTail; ideal offscreen renders |
| SR-WAVE-06 | Readouts shall identify acquired chain, photoelectron budget, single-channel FWHM excluding pixel gain spread, pulse time constants, actual integer filter coefficients and effective resolving interval. Trapezoidal energy shall use matched local flat-top calibration and be unavailable for overlap/saturation/partial windows. CR-RC shall report order, Q12 state/Q16 coefficients and explicit per-preset T_sum shaping time (simulation convention, separate from the DCR noise window; not peaking time or hardware evidence), and omit recovered energy until trigger/phase/overlap estimation is validated. | WaveformServiceTests.AcquiredChain_IsUsedAfterPendingSelectionChanges; Readout_SuppressesPartialOverlapAndSaturation; Crrc_PlotsFractionalCodesAndLabelsSimulationTime; CrrcContractTests; RTL contract matrix |
| SR-WAVE-07 | Scope work shall use checked origin-relative sample conversion, explicit requested length, warm-up covering preceding pulse support and filter history, and a total raster allocation cap of 10 million samples. Clipping and missing acquired future shall be labelled; an empty acquired window shall show simulated baseline noise. | ScopeWindow tests; WaveformServiceTests.TimeBase_IsOriginRelativeAtLateAcquisitionTime; Ideal_HasNoNoiseOrSmearAndKeepsTail; MaximumWindow_ReportsWorkerCostAndBoundedPlotSamples (Evidence) |
| SR-WAVE-08 | Scope processing and pyramid preparation shall run on a worker only while the workspace is active. A held covered selection shall reuse its output. Obsolete requests shall be cancelled/discarded and errors shown. Viewport navigation shall not resimulate the trace. | WaveformWorkspaceTests.HiddenScope_DoesNotGenerateAndHeldTriggerReusesWindow; LatestSelectionWinsAndNewAcquisitionCancelsOldScope; ScopeFailure_IsVisibleAndLocalControlsKeepTheAcquisition; shared-axis render assertions |

Finite warm-up resets the integer filter locally and is not a claim of acquisition-wide baseline-state equivalence. Ten million samples is an allocation ceiling, not a guaranteed 4 Hz trace-generation rate. The legacy CR-RC F=0 fixture remains bit-exact. The Q12 CR-RC contract is bounded to signed-16 input, A in [0,65536], K in [1,65536], order 1–16 and F 0–12; RTL uses signed 48-bit states/products. This arithmetic bound is not a hardware timing or trigger-efficiency claim.

## Detector and retained-flood focus analysis

| ID | Requirement |
|---|---|
| SR-DET-01 | A shared reflector-gap editor uses µm and supplies mm to acquisition. Finite nonnegative gap must be smaller than pending pixel pitch; malformed edits and pitch changes revalidate and block Start. Editing is possible only while no acquired data exist (SR-RUN-12). |
| SR-DET-02 | Detector is the fourth registered workspace. Without acquired data its static gain pattern and face use the pending detector / optics, captioned "Settings for the next acquisition"; with data they use the acquired (locked) inputs, captioned "Acquired settings · locked until Reset". The caption is neutral text, not a warning. |
| SR-DET-03 | The face uses the engine's seeded gain pattern and exact active rectangles with half-gap perimeter and full-gap interior dead regions. It reports geometric active-area fraction ((pitch−gap)/pitch)² as area, independently of acquired counts/live-time rate, material and gain σ/seed. The geometry card top-aligns to its natural content height with standard padding and scrolls when height is constrained; gain extrema remain under the face legend. |
| SR-DET-04 | Offscreen fixtures cover Detector before acquisition and with acquired (locked) settings, both themes and 1280×800/1440×900, without creating an HWND or sending desktop input. |
| SR-FOCUS-01 | A cancellable worker sweeps the selected channel's retained flood through ImagingProjection.AtFocus with non-cyclic decoding and Studio allocation validation, without transport or calibration and without joining the 4 Hz snapshot publisher. |
| SR-FOCUS-02 | Sweep planes are uniform in 1/z from acquired D+30 to 3000 mm. K is user-selected from 1–4, default 1, independent of scene truth. Across planes, candidates are assigned one-to-one by minimum squared angular displacement (x/z,y/z). |
| SR-FOCUS-03 | A plane-versus-prominence PlotView shows each complete track's curve, sharpest plane and contiguous interpolated raw half-maximum interval. Edge-reaching intervals have censored endpoints and no finite width. Boundary maxima, disconnected half-max modes and unresolved results are labelled. |
| SR-FOCUS-04 | Reset, a new acquisition or edits to channel/window/strip/K cancel and invalidate analysis (optics cannot change while data exist); obsolete responses cannot publish. Results identify the frozen acquisition prefix. Visible text describes lateral-dependent near-field bias, default-optics far-edge censoring from about 700 mm, and the difference between half-max width and uncertainty. Stripped fractional clipped floods are not independent Poisson observations. |
| SR-FOCUS-05 | Optional external surface range is separate from decoder focus and appears as a focus-curve marker. Only the explicit Use as focus action changes decoder focus. No fusion verdict, significance or automatic focus change is supplied. |

The focus tool is descriptive engineering analysis, with no calibrated depth accuracy or confidence interval. Conditions and verification limits are in [VV.Studio.Detector](VV.Studio.Detector.md).
