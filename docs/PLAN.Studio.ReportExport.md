# PLAN.Studio.ReportExport — export a simulation report from GCAM Studio

Scope: TODO-46. GCAM Studio gains one command that writes a report of the current acquisition to a file the user
chooses: the inputs and settings the acquisition ran with, the image, the spectrum, the estimates and the user's
measurements. It is Studio's **first write surface**.

Status: planned 2026-10-14. The author asked to build it now ("커밋 한 다음에 Codex 불러와서 만들어 봐"). The first turn is a
short premise check followed by implementation in the same turn (proportionate procedure). The planner chose the scope
decisions below as defaults, and the author can override them.
Turn 1 (Codex) stopped at the premise check. The physical readout is not reproducible from the scene file, and Spectrum
has no user measurements. R1 and R2 were revised accordingly (planner, 2026-10-14).
Turn 2 stalled: the workspace-write sandbox hung `dotnet test`. Turn 3 ran with danger-full-access and implemented the
plan, and the planner re-verified it:
- the command sits beside Reset in the title bar, with Alt+E;
- the snapshot carries the configuration JSON;
- the files are an HTML report plus `.scene.json`, written with rollback;
- the build has 0 warnings and 0 errors;
- the suite has 1,065 passed and 50 skipped (baseline 1,051 / 49);
- the catalog lists 203 units.
Desktop export scenario (author's go each run): run 1 searched the desktop's children for the dialog; run 2 found it
but its Save lookup did not invoke the dialog's own button. Both were fixed (`487dbfd`, `f3798d3`). Run 3 (2026-10-15,
`f3798d3`) passed in 6 s: the dialog's Save button (IDOK) was invoked, exactly `report.html` and `report.scene.json` were
written into the owned sandbox, the HTML holds the displayed estimate and the scene file loads.
Turn 6 (substitute, 2026-10-15): the HTML embedded the full configuration (about 3.7 MB, almost all of it the ambient
spectrum tables) and duplicated the scene file; it now shows those tables' entry counts only (default scene without
figures 3,869,095 → 10,831 bytes; the scene file is unchanged). Offscreen title-bar renders show no layout defect. Docs
applied by the planner: SRS (SR-SEC-01 narrowed, SR-EXPORT-01 … 04), SDS SU-27, V&V trace and History, D-58,
AGENTS.Studio, AGENTS.UiAutomation §1, folder maps. Build 0 / 0; suite 1,066 passed / 50 skipped; catalog 203.
Run 4 (2026-10-15, `7caf9ff`, author's go) passed in 9 s: the exported HTML is 450,860 bytes with the reduced
Scene section (10 KB) and three figures; the scene file is unchanged in size (3,171,006 bytes).
Stage 2 (author, 2026-10-15): document icon button, an export popup and three more formats — PDF, Word, JSON with the
four-channel ADC values (decisions X1 … X7 below); implementation by Codex.
Codex turns 7–9 implemented it (`de86f97`; turn 9 ended when the quota ran out again, and the planner rebuilt and re-ran
the suite): build 0 / 0; suite 1,079 passed / 51 skipped; catalog 208. Samples read by the planner: PDF 7 pages with the
configuration as an appendix, Word with heading styles and PNG figures, JSON 38 conversions from a Baseline 661.7 keV
physical-readout run (3.2 MB, almost all of it the configuration's ambient tables). Desktop run 5 (2026-10-15, `13a6969`,
author's go): the theory passed for PDF (11 s), Word (8 s) and HTML (7 s) through the popup, each writing exactly the
report and its scene file into its owned sandbox. X8 (event data as CSV) implemented by a substitute implementer
(`ab34330`; suite 1,093 passed / 51 skipped); desktop run 6 (author's go) passed all four formats, CSV writing exactly its
three declared files. Stage 2 docs applied by the planner: SRS (SR-SEC-01, SR-EXPORT-01 … 04 revised, SR-EXPORT-05), SDS
SU-27 and allocation, D-58, V&V trace and History, AGENTS.Studio, AGENTS.UiAutomation §1, folder maps, catalog.

## Decisions (planner defaults, 2026-10-14)

| # | Decision | Reason |
|---|---|---|
| R1 | **Format: one self-contained HTML file.** Inline CSS and inline SVG figures. No script, no external reference, no embedded fonts. Beside it goes `<name>.scene.json`, the `SimulationConfig` the acquisition ran with (after Studio's detector, ambient and readout settings), serialised with the engine's own JSON options. The scene file **records** the configuration. It does not promise to reproduce the run: a physical-readout acquisition depends on a captured calibration bank that the config does not hold, and the ordinary CLI refuses that mode. The report states this whenever the readout is not the direct crystal assignment. | Opens in any browser and prints to PDF from there, so Studio needs no PDF library. The figures stay in one file that can be attached as is. SVG is text: deterministic, and testable without WPF. The scene file shows exactly which inputs were used. |
| R2 | **Contents:** (1) header: title, export time (local and UTC), Studio version with commit, and the notice "Simulation — not a measurement". (2) Scene: sources, optics, detector, decoder, readout and ambient settings, as the scene view model holds them. (3) Acquisition: run state, simulated time, counts, seed where Studio has one. (4) Imaging: the current image as an SVG heat map in Studio's colormap with the estimate marker, plus the estimate values. (5) Spectrum: an SVG plot with its energy windows and line markers. (6) The user's Imaging measurements as a table (Spectrum has no user measurement model). Nothing is recomputed: every value comes from the published snapshot and the view models, formatted as Studio shows it. | A report states what the screen showed, so it cannot disagree with it. |
| R3 | **Out of scope for the first cut:** Waveform and Detector workspace figures, PDF output, batch export, remembering the last folder, any setting. | Keep the first write surface small. Studio still keeps no settings. |
| R4 | **Where:** an "Export report…" command next to Studio's existing commands (verify where they live). It is enabled only when a snapshot exists. It opens the standard save dialog: default folder Documents, default name `gcam-report-<yyyyMMdd-HHmmss>.html`, and the dialog's own overwrite prompt. Studio writes only the path the user confirmed, and nothing else. The bytes are built fully in memory before the write; a failed write shows a message and leaves no partial file Studio knows of. | Writes happen only on the user's explicit choice. Nothing is written automatically. |
| R5 | **Layering:** the report model plus the HTML and SVG writer live in `Gcam.Studio.Core` (net9.0, no WPF). Its inputs are the snapshot, the scene and measurement view-model state, the version string and an injected clock, and the same inputs give identical bytes. The file dialog and the file write go behind a small view-layer service in `Gcam.Studio`, so the view model is tested with a fake. | The same rules as the rest of Studio (`AGENTS.Studio.md`, `DESIGN.Architecture.md`). |
| R6 | **Reopened UI-automation safety scope, kept proportionate:** one declared write surface, at a path only the user (or the harness, acting as the user) types. One desktop scenario exports after a short acquisition. It types a path **inside the run's owned sandbox** into the save dialog. It asserts that the file exists there and nowhere else the scenario can see, and that the HTML holds the estimate the results table shows and the scene file loads. The scenario is written and compiled but **not run**; the planner runs it on the author's go. Documentation wording is proposed to the planner, not edited. | `AGENTS.UiAutomation.md` §1: "If Studio ever gains settings or a cache, isolate them through an explicit path the harness can set, and re-run this stage." The save-dialog path is that explicit path. |
| R7 | **Manual and notices:** the manual chapter and the N-NOT-SAVED change belong to TODO-43's next edition, on that branch. Nothing here touches them. | TODO-43 is on its own unmerged branch. |

## Stage 2 — export formats (author, 2026-10-15)

The author: "버튼은 서류 아이콘 버튼으로 만들고, 팝업박스로 만들어서 다음을 추가해. 1. json (헤더로 시뮬레이션 환경 작성, 본문은
4채널 ADC Value 출력) 2. Pdf (지금 출력물 그대로) 3. Word (지금 출력물 그대로). html로 뽑은 건 맘에 들긴 하는데, 선택지를 늘릴 수
있었으면 하네." Answers the same day: PDF through PDFsharp (MIT); under the direct crystal assignment the JSON choice is
disabled with its reason. Implementation goes to Codex again (its quota is back).

| # | Decision | Reason |
|---|---|---|
| X1 | The title-bar command becomes a **document icon button** in the style of the neighbouring icon buttons, with the tooltip and accessible name "Export report…", the same access key and AutomationId, enabled only with a snapshot. | The author's request; the text button was the only text command in the title bar's right group. |
| X2 | The button opens an **in-window modal popup** (the same modal layer as Options) titled "Export report". It lists four formats, one choice at a time, each with a one-line description: **HTML** (the default), **PDF**, **Word (.docx)**, **JSON — four-channel ADC values**. Under the direct crystal assignment the JSON choice is disabled and says why, worded like the Waveform empty state ("Available with the experimental physical readout"). "Export…" opens the save dialog with that format's extension and filter; Cancel and Esc close it. | One entry point, more choices; the disabled reason follows SR-DET-06 / SR-WAVE-09. |
| X3 | HTML, PDF and Word carry the **same content** from the same report model (R2), and each still writes `<name>.scene.json` beside it. The JSON export writes one file: its header holds the configuration, so it needs no scene file. Each export writes only the paths the user confirmed and its declared sidecar, built in memory first, with rollback (R4). | "지금 출력물 그대로"; one model keeps the formats from disagreeing. |
| X4 | **PDF** through PDFsharp (MIT; MigraDoc from the same project is allowed for tables and flow). Text and tables as in the HTML; the figures drawn as vectors from the same figure data the SVG uses. Fonts resolved on Windows. | The author's choice; vectors keep the figures sharp. |
| X5 | **Word** written directly as Office Open XML (`System.IO.Compression`, no package): headings, paragraphs, tables, and the figures as PNG images rendered from the same figure data. | A .docx is a zip of XML; no dependency needed. |
| X6 | **JSON**: a header with the schema name and version, generator (Studio version and commit), export time (local and UTC), the notice "Simulation — not a measurement", the scene summary as the report shows it, the full `SimulationConfig` (the engine's JSON options, as in the scene file), the acquisition summary (run state, simulated time, counts, seed) and the readout description (channel names A–D, ADC bits, units). The body lists the acquisition's events with their four ADC values as integers, in a compact column layout named in the header. Nothing is recomputed: the values are those the acquisition produced; if the snapshot does not retain them per event, expose them from the service's retained data rather than recompute. | The author's specification; R2. |
| X7 | Tests: per format, unit tests on the bytes (structure, every section present, values equal to the model, escaping, deterministic where the format allows). The desktop export scenario goes through the popup for HTML, PDF and Word (one theory), written and compiled, run by the planner on the author's go; JSON is unit-tested (the physical readout needs a calibration capture). | Proportionate; R6 unchanged. |

### Stage 2 revision — event data as CSV (author, 2026-10-15)

The author: "JSON으로 ADC 출력을 가져가라는 이유는 다른 프로그램에서 이걸 보기 좋게 가공하기 위해 Plain한 출력도 제공해주기
위함이였어. 더 좋은 방법이 있나?" — then, on the planner's CSV proposal: "물리 판독일 때도 그냥 출력하는 게 좋겠어. 다만 헤더등에
명시해두는 게 좋겠지. 왜냐면 얻는 데이터만 필요하고 가공하는 쪽은 다른 프로그램을 쓸 수도 있으니까." The planner reads this as: export
the acquired event data under every readout, and state in the header which readout produced it.

| # | Decision | Reason |
|---|---|---|
| X8 | The JSON choice becomes **"Event data (CSV)"**, enabled whenever a snapshot exists. It writes `<name>.csv` — one row per retained event, a header row of column names, invariant culture — plus `<name>.meta.json` (a few KB: generator, export time, the notice, the readout mode, a sentence on what that readout means, each column's meaning and unit, the scene and acquisition summaries, the configuration with the ambient tables as entry counts) and `<name>.scene.json` (the full configuration, as for the other formats). Columns: **direct crystal assignment** — index, arrival_time_s, pixel_x, pixel_y, deposit_keV, measured_keV (the energy the spectrum shows), and the meta file states that this readout has no ADC stage; **physical four-output readout** — index, hold_time_s, trigger_time_s, A, B, C, D (14-bit codes), raw_x, raw_y, crystal (−1 when unassigned), energy_keV. Values are the retained ones, as Studio shows them; nothing new is computed. Replaces X6's JSON body and X2's disabled state. | Plain data for other programs; CSV opens in spreadsheets and every analysis tool; the header says what the numbers are. |

## Premises to verify first (stop and report if one is wrong)

- Studio has no file write today (`AGENTS.UiAutomation.md` §1).
- The scene can be turned into a `SimulationConfig` (`SceneConfigBuilder`), and that config serialises with the
  engine's JSON options so the CLI reproduces the run. — *turn 1:* the config is serialisable, but a physical-readout run also needs a
  captured calibration bank and the CLI refuses that mode. So R1 now promises a record of the inputs, not a reproduction.
- The snapshot and the view models hold everything R2 lists, so nothing has to be recomputed. List any item that is
  missing and propose to drop it or to expose it.
- There is a natural place for the command, and a keyboard access key that does not clash with existing ones.

## Verification

- Build: 0 warnings, 0 errors.
- Full suite with `GCAM_UI_TESTS` unset: baseline on this branch first, then new.
- New unit tests:
  - the same inputs give byte-identical HTML;
  - every R2 section is present;
  - text is HTML-escaped;
  - the SVG is well formed and holds no external reference;
  - the scene file round-trips through `ConfigLoader`;
  - the command is enabled only with a snapshot;
  - a cancelled dialog writes nothing;
  - a failed write reports and leaves no file.
- `test_records.py check-catalog --discover` and the CI checkers pass. Catalog entries are added for new test classes.
