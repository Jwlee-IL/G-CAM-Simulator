# PLAN.Docs.StudioManual2.Review — second edition and desktop-test video review

Scope: TODO-48 review only, against `b14c6eb`, branch `todo48-manual-second-edition`, 2026-10-16.
No product or harness implementation, desktop launch, UI Automation, screen capture or UI test was performed.
V1–V4 remain author decisions; V5 is a planner proposal. The author's UI-automation procedure was consulted
read-only for process, including its current proportionality, surface-survey and observation rules.

## 1. Corrections to the premises

Paths below are repository-relative. A line reference identifies the code inspected, not a claim that a desktop
execution passed. `H/` abbreviates `tests/Gcam.Studio.UiTests/Harness/`; `U/` abbreviates its parent directory.

| Plan's “What exists” row | Evidence at this HEAD | Review / correction |
|---|---|---|
| Manual sources and generator | `docs/manual/chapters.json:8`, `:27`; `samples/manual/manual_records.py:1092`, `:1246`, `:1420`, `:1433`, `:1472`; `samples/manual/manual_formats.py:632`, `:782` | Correct: 8 chapters, 6 generated and 2 static; generate/check/self-test and PDF/Word exist. Measured committed edition size is **3,807,398 bytes**, 3.81 MB / 3.63 MiB, rather than an exact 3.9 MB. |
| Manual scenarios | `U/ManualScenarioTests.cs:30`, `:82`, `:137`, `:190`, `:262`, `:333`; `H/ManualScenario.cs:22`; `H/ManualRun.cs:55`; `H/ManualCapture.cs:40`; `H/ManualObservation.cs:17`; `H/ManualRegistry.cs:14` | Correct six scenarios and named harness components. The archived review's §3 proposes 18 chapters (`docs/archive/PLAN.Docs.StudioManual.Review.md:104`, `:125`); it is a design backlog, not the present registry. |
| Desktop tests | Attribute census below; `U/ReportExportScenarioTests.cs:20`–`:26`; `H/Scenario.cs:18`, `:23`; `H/ManualScenario.cs:37`; `U/ObservedRunTests.cs:19`; `U/PlotViewTests.cs:17` | **38 DesktopFact methods in 11 classes, plus one DesktopTheory in a twelfth class with 4 rows: 39 methods / 42 cases.** 36 facts plus that theory use the wrappers: **37 methods / 40 cases**, not 37 cases. Two fact cases bypass them. One fact is separately long-gated; the observation fact has its own probe gate. Count eligibility and executions separately. |
| Window capture | `H/RunRecord.cs:47`, `:92`; `H/ManualCapture.cs:40`, `:127`, `:141`, `:185`; `H/CaptureQuality.cs:9`, `:17` | DWM bounds and CopyFromScreen exist. **CaptureQuality has no overlap detector**: it checks supplied booleans and RGB24 pixels. The private top-level-window traversal is in **ManualCapture.Occluder**, returns only the first class-name reason, and supplies no mask rectangles. Extract/extend it rather than claiming that masking already exists. |
| Encoders on this machine | Headless sink-writer measurement in §6 | Successful RGB32→H.264 encoding establishes availability through the system APIs. A DLL path is not an encoder test; the plan's “no ffmpeg” assertion was not verified and is unnecessary. No system directories were inspected. CI has no desktop opt-in (`.github/workflows/ci.yml:39`; `U/DesktopFactAttribute.cs:17`; `U/DesktopTheoryAttribute.cs:6`). |
| Export scenario | `U/ReportExportScenarioTests.cs:20`, `:47`, `:52`, `:111`, `:154`; `H/Scenario.cs:23` | One theory, **four independent launches/sandboxes**. HTML/PDF/Word compare parsed text with the displayed estimate; CSV checks shape/meta consistency, not its row count against an independently retained population. Scene checks only seed/non-null and pixel count. Helpers are private. Crucially, diagnostics are suppressed **before** opening export and remain suppressed: no native save-dialog capture is permitted. |
| Compare scenario | `U/CompareScenarioTests.cs:9`–`:37`; `src/Gcam.Studio.Core/ViewModels/CompareWorkspaceViewModel.cs:36`, `:65`; `src/Gcam.Studio/Controls/AutomationEvidence.cs:14` | Present. It waits up to **90 s** for Baseline preparation, then checks prefix/labels/difference surface/reset. **It does not independently verify population arithmetic or either image.** “~8 s” is not a guarantee established by this scenario; no preparation timing was measured here. Numerical UIA evidence currently has Spectrum/Waveform/Imaging branches, **no Compare branch**. |
| Known local fingerprint failure | `U/ContentFingerprintTests.cs:23`, `:30`; `.github/workflows/ci.yml:21`–`:23` | Literal `python` and a 10 s wait are confirmed. The claimed Store-alias cause and full-suite-only frequency are historical diagnoses, **not reproduced or established by this code**. Do not turn that attribution into fact or assume `py -3.13` is installed. CI explicitly provisions Python 3.13. |

Other corrections: Options and Reset are **in-window ModalHost layers**, not separate windows
(`docs/AGENTS.UiAutomation.md:101`; `src/Gcam.Studio/Views/MainWindow.xaml`, ModalHost). Some dropdowns and the
native save dialog are separate windows. Screen-region capture includes only their intersection with the main
frame; it does not magically include a popup extending beyond that frame. The existing first-edition 1.2 scenario
does not exercise Ctrl+1…4 at all (`U/ManualScenarioTests.cs:31`–`:79`); the new edition should verify real key routing
if it teaches Ctrl+1…5, which are bound at `src/Gcam.Studio/Views/MainWindow.xaml:67`–`:71`.

## 2. Numbered proposals for P1 / P2: edition scope and chapter scripts

**R1. Publish 10 chapters: 8 generated, 2 static, in parts 1–5.** Keep existing numbers, add 3.7 and 4.4.
Part 4 can contain **only 4.4**: `manual_records.py:1095`–`:1108` requires unique parts, a matching chapter prefix,
and no empty part; it requires no contiguous numbering or earlier siblings. Existing 2.4 and 3.4 already have gaps.
This is readable when 4.4 supplies its own Baseline setup and explains the experimental scope. Do not add empty
4.1–4.3 entries or an omitted-chapter warning for chapters outside this edition's scope.

| Part | Included chapter numbers and titles |
|---|---|
| 1 Orientation | 1.1 Purpose, scope and prerequisites (static); 1.2 Window, workspaces and view tabs |
| 2 Configure and acquire | 2.1 Place and edit sources; 2.2 Choose optics and detector settings; 2.4 Start, Stop, Continue and Reset |
| 3 Inspect retained data | 3.1 Imaging measurements; 3.4 Spectrum; **3.7 Export reports and event data** |
| 4 Experimental readout | **4.4 Compare direct crystal and physical readout** |
| 5 Interpretation and help | 5.2 Interpretation and limitations (static) |

Reason: “event data” is not a report containing the displayed estimate (SR-EXPORT-05); “direct crystal” describes
the actual ideal-deposit baseline more precisely than a general “ideal readout.” These titles are proposed wording.

**R2. Re-run all eight generated chapters on one pinned deployment, and use the following scripts.** Retain separate
reader steps and stable check IDs. An oracle below is independent of the handler being checked; retained arrays are
input evidence, not independently validated physics. Every chapter must fail its own deliberate verdict mutation,
then pass recovery. Reuse the six existing scripts, not manually reconstructed chapter prose.

| Chapter / scenario | Reader steps in order | Checks and independent oracle | Planned captures after checks |
|---|---|---|---|
| 1.2 `manual.navigate` | Open restored shell; inspect Scene/Optics/Detector tabs; visit Spectrum, Waveform and Four outputs; Detector and SiPM; Compare in DirectCrystal mode; Choose readout; return Imaging. Demonstrate Ctrl+1…5 separately from clicking. | Empty start and Imaging surfaces; selected tabs and destination surfaces; no stale FloodView in Spectrum; documented DirectCrystal empty-state text and Choose readout's focus destination. Send actual keys and check the resulting workspace, not merely IsSelected after Select. Oracle: navigation contracts SR-NAV and SR-CMP-05, XAML bindings, empty state. Existing checks: `ManualScenarioTests.cs:36`–`:79`. | Full shell with **five** workspace controls; Optics crop; Waveform empty state; Detector empty state; readout mode choice; **Compare empty state**. Six figures. |
| 2.1 `manual.sources` | Inspect default Cs-137 at (0,0); type X=−20 and leave field; add Co-60 at X=20, activity 400; remove it; acquire; inspect locked inputs; Reset and confirm; edit again. | One default source; committed fields **and source-list description** agree; count 1→2→1 and remaining Cs state preserved; data lock Add/Remove/X; Reset unlocks. Oracle: literal script inputs and SR-SCENE / run-lock contract, not another getter of the same binding alone. Existing checks `:88`–`:134`; strengthen the field-only edit checks. | Initial Scene; two-source selection; locked Scene. Three figures. |
| 2.2 `manual.geometry` | Inspect Sharp; choose Baseline; inspect Detector Face; choose Custom retaining Baseline; enter N=3; read refusal; correct to N=12. | Preset locks; documented rank 7 / 12×12 Baseline; face N² crystals and N×pitch extent from typed/read committed geometry; next-acquisition caption; Custom unlock/preservation; documented N∈[4,64] refusal prevents acquisition; correction clears message. Oracle: documented geometry arithmetic and input rule, not preset lookup implementation. Existing `:145`–`:187`. | Sharp geometry; Baseline detector face; invalid-field/refusal state. Three figures. |
| 2.4 `manual.acquisition` | Set 600 s, speed 1; apply seed 12345 in Options; Start; Stop; Continue and Stop; Reset/Cancel; Reset/Discard. | Literal applied seed/inputs; acquisition grows; stopped count is positive and remains unchanged over a bounded observation; Continue increases it; physical fields stay locked; confirmation names the kept count; Cancel preserves; Discard clears images/counts and unlocks. Oracle: state-transition and conservation contracts, not fixed wall-clock counts. Existing `:196`–`:257`; preserve N-TIMING. | Acquiring top bar; Stopped/Continue; reset modal; Ready. Four figures. |
| 3.1 `manual.measurements` | Acquire; hover flood cell; drag distance; click three angle vertices; drag ROI; inspect table/details; focus image and Delete selected ROI; Clear. | FloodOracle's independently derived screen→mm mapping and pixel-centre inclusion; analytic Euclidean distance and angle, existing per-pixel/rounding tolerances; M1/M2/M3 kind/pane and exact remaining rows; clear empties. ROI pixel count is checked here; do not claim this script independently sums event counts. Existing `:272`–`:330`. | Flood hover/readout; three measurements with selected detail. Two figures. |
| 3.4 `manual.spectrum` | Acquire; open Spectrum; inspect Cs line and band count; set width to 2 FWHM; turn Log Y off then on. | Plot counts equal retained histogram; WorkspaceOracle independently sums bins in the published band; widening ratio follows typed width; counts remain identical across window/log view changes. Existing `:344`–`:378`. Add plot-band versus retained-band endpoints if asserting that the drawn band widens: current Width reads the retained model only. | Initial spectrum/table; widened band; final log spectrum. Three figures. |
| 3.7 `manual.export` (new) | Acquire short **completed direct** run; note retained count/scene/estimate and add one identifiable Imaging measurement; open Export popup; inspect choices; cancel once; reopen and choose HTML, PDF, Word, then Event data, each to a fresh owned subfolder; enter and read back the owned filename; Save; inspect results. Explain sidecars and overwrite confirmations without overwriting a user's file. | Before data export disabled; popup Cancel produces zero writes; compare each format's **parsed** content with values frozen before popup; exact destination set; scene parses with expected script inputs. CSV independently parse all six columns and row count against frozen retained count; finite values, indices/order, scene/meta/file sibling identity. HTML parse structure/text and inline figures, reject script/external URLs; PDF read page text/maps and vector content; DOCX read XML/relationships and PNG entries; do not call writers/renderers as oracles. Existing PDF reader `ReportDocumentTests.PdfText` (`:75`) is reusable; writer-created fixtures are not the expected output of the UI run. | Export enabled beside acquired status; in-window format popup; returned Studio after save. **Three figures; no native dialog or file-manager screenshot.** File contents are verification artifacts, not fabricated UI figures. |
| 4.4 `manual.compare` (new) | In DirectCrystal open Compare and follow Choose readout; choose Baseline then FourOutputAnger; set seed 47001, ambient 0, 10 s; Prepare and wait Ready; acquire; open Compare; read paired floods/recons, shared prefix and spectra; read populations/reference; toggle assignment disagreement; Reset/Discard and see start state. | Literal setup and ready/completed state; prefix exactly 10 s, same acquisition/settings identity; direct/Anger headings, ideal-deposit explanation and raw-count units; Same/eligible ratio recomputed with display rounding; assigned+unknown=held for the available line, same≤eligible and different=eligible−same; empty denominators unavailable; toggle changes flood row while reconstructions stay; reference labelled as benchmark and Reset removes old pair. Independent oracle: arithmetic over captured held inputs/assignments and typed settings, not Compare service output fed back to itself. Add a minimal read-only Compare UIA evidence branch (gated as existing evidence is), expose raw denominators/populations plus displayed data; use tiny headless fixtures to verify the oracle. **Existing CompareScenarioTests supplies only label/state checks**, not this oracle. | Direct-mode empty state/Choose readout; Baseline Ready preparation; full pair with prefix; scrolled populations/reference; difference map with recons retained. Five figures. |

Total planned source figures: **29**. This is a capture budget, not a guarantee or executed inventory. Figures must
pass existing label/bounds/privacy/quality gates and author inspection; no screenshot is the numerical oracle.
For Compare, avoid a huge raw-input dump on every frame: take numerical evidence only at check points. If the added
raw evidence is judged excessive, explicitly limit the chapter's claims to population labels/state transitions
and leave detailed arithmetic to separately traced headless tests; do not call the current scenario numerical validation.

**R3. Give manual export declared-write support before using it.** `ManualScenario.Run` currently always calls
`Scenario.Run`, whose expected writes are empty (`H/ManualScenario.cs:37`; `H/Scenario.cs:18`). A direct transplant of
the export body would fail cleanup. Add an explicit manual-export entry accepting only fixed, guarded owned paths,
threading an exact expected-write list through the common core. Prefer one generated chapter run with separate
format subfolders and one union of declared files; do not let four latest records for one scenario silently compete
for one chapter (`manual_records.py:1256`–`:1280`). Factor native dialog helpers without weakening their PID, path,
button-type and cleanup checks. Never reuse arbitrary extension text as a path without validation.

**R4. Update both static sources and mark purposes, then obtain wording/edition approval.** 1.1 must say five
workspaces, extend its generated-parts description to part 4, list 3.7/4.4, and describe explicit export.
5.2 should explain the ideal-deposit versus physical comparison, populations, reference cost rather than time
prediction, export's captured snapshot, scene reproduction limits, omitted calibration bank and omitted Compare.
Sources: SR-CMP-01…05 and SR-EXPORT-02…05. Add purpose records for newly marked export/Compare controls, each sourced;
the generator requires approved purposes for a formal edition (`manual_records.py:1377`). Do not edit edition 1.

## 3. P3: proposed notices

**R5. Replace N-NOT-SAVED with the following author-review draft**, allowed in 1.1 and 3.7:

> Studio keeps no settings. Scene, results and measurements live in memory and are lost when the window closes.
> To keep an export, choose a format and confirm a destination in the save dialog. Studio then writes the selected
> file and a same-name `.scene.json` sidecar; Event data (CSV) also writes a `.meta.json` sidecar. No folder is remembered.

Reason/source: SR-SEC-01 (`docs/VV.Studio.SRS.md:206`), SR-EXPORT-01/03/05 (`:388`, `:390`, `:392`);
`src/Gcam.Studio/Services/ReportExportService.cs:15`–`:32`; `ReportFileWriter.cs:13`–`:28`.
Avoid “only writes when unasked/asked” absolutes about framework bookkeeping: the application contract concerns
user data, and the charter already declares framework effects. Keep N-SIM, extending its allowed placements to
3.7 and 4.4, and N-READOUT to 4.4. Notice placement changes require corresponding BeginStep placements because
`manual_records.py:1281`–`:1290` requires every allowed placement in a printed chapter.

**R6. Add one export-scope notice rather than several overlapping warnings**, allowed in 3.7 and optionally 5.2:

> An export records the snapshot captured when Export report is opened. The scene sidecar records acquisition
> inputs; it does not promise to reproduce the run. A physical readout's calibration bank and the Compare workspace
> are not included. HTML, PDF and Word contain the report; CSV contains retained event data with its metadata.

Proposed ID: `N-EXPORT-SCOPE`. Sources: SR-EXPORT-02/03/05 and SR-CMP-05 (`SRS:389`–`:392`, `:402`),
`src/Gcam.Studio.Core/ViewModels/MainViewModel.Report.cs:20`, `src/Gcam.Studio/Views/ComparePanel.xaml:39`.
N-SIM already supplies the simulation caution; no second simulation warning is needed. The reader steps should
explain that existing files may require overwrite/sidecar confirmation (SR-EXPORT-01), without claiming that this
edition exercises replacement or rollback; those remain regression/headless obligations.

## 4. P4: does the new build need observation?

**R7. Yes. Existing records reclassified alone are insufficient for this edition. Replace the proposed idle-only
run with an observation of an applicable scenario, and reuse that run for its manual result when qualified.**

The author's UI-automation procedure, index §2.6 and safety module §2–§3, separates these cases:

- A survey that establishes a surface class is absent can close that class without inventing observation channels.
  This does not close the actual save surface: reachable SaveFileDialog and ReportFileWriter code exists.
- Reclassification without a new run applies to a mechanically established change of the background-filter table
  alone. It neither qualifies a changed product build nor proves an export path the recorded run never used.
- A product-build-only change requires an observed pilot followed by applicable observed flows. A new flow also
  needs its first execution observed. A permission expansion requires the approved change and a pilot that uses it.

Export is already declared in `H/Isolation/surface-list.json:22` and desktop runs 3–6 are reported in
`docs/VV.Studio.History.md:362`–`:380`. Those are functional runs, not a demonstrated current-build G1 basis with
channel controls, classified state changes and matching isolation fingerprint. The archived charter explicitly
reopens M03/M06 for export and calls for a new run (`docs/archive/PLAN.Docs.StudioManual.Charter.md:48`–`:51`).
The current HEAD also includes Compare and changed action policy, and the recorder will add harness files/helper
activity. Consequently this is not a background-filter-only change. Read/access/image-load events alone are not
write candidates; compare actual before/after state.

Smallest adequate sequence for implementation/desktop planning:

1. Reconcile the survey/action declarations and existing approvals against the concrete implementation commit.
   Do not reapprove an already authorised unchanged export allowance. If new targets or isolation permissions are
   added, obtain the specific approval for those changes before their pilot. The current `ProbePackage.Validate`
   unconditionally asks for three acts (`H/Isolation/ProbePackage.cs:11`); it is not a mode-aware implementation
   of every exception in the updated procedure. Do not silently bypass it.
2. Use the light before/after method with a control interval and positive controls on applicable channels. The
   first pilot must actually use the export allowance, e.g. HTML to its guarded sandbox. A fresh idle-only run
   cannot validate file-dialog/export effects. Then observe PDF/Word/CSV and new manual Compare flow; one qualified
   observed manual batch can supply both safety and chapter evidence instead of duplicate runs.
3. Record file-dialog shell effects as possible application effects, not generic OS background. Record renderer/helper
   writes as harness effects with exact ownership. Include the repository video destination in the observation
   accounting: `ManualObservation.OwnedFolders` currently knows temporary `gcam-uia-*` roots only (`:98`–`:103`).
4. Classify all state changes; use existing-record reclassification only for a genuine background-filter-only
   correction with valid controls and cleanup. It cannot rehabilitate an unsafe-effect or failed-cleanup run.
   Escalate instrumentation only for an unresolved candidate; no default kernel trace or administrator run.

No observation was attempted in this review; the permission and desktop restrictions remain intact.

## 5. P5: recorder design

**R8. Use one recording session per test case, with an outer verdict owner.** A wrapper name is not enough:
the four export rows currently pass the **same** nameof to Scenario. Use a stable filesystem-safe test ID including
class, method and canonical theory arguments, e.g. `ReportExportScenarioTests.ExportReport.Html`; append a hash
when normalisation can collide. Record a separate unique run ID. Include full arguments in identity metadata.

Lifecycle:

1. Resolve opt-out before creating video folders, threads, COM objects or a helper process. Start capture after
   owned main-window discovery, normalisation and initial-state verification (`Scenario.cs:43`–`:51`). This means
   launch/normalisation failures have no video, explicitly recorded as unavailable, never as an old-video replacement.
2. The shared Scenario core owns the session and exposes it to the outer manual adapter. `RunReportExport` uses
   that same session, not another nested recorder. Begin/End labels and privacy pauses are timestamped events.
3. Stop **pixel collection before** disposing Studio; an invalid/disappearing HWND must never become a capture of
   the desktop behind it. Encoder drain/finalisation can run after capture stops, in parallel with ordinary cleanup.
4. Compute candidate success only after disposal, pointer restoration, ledger/monitor checks, manifest/record writes
   and **all final assertions**, including exact expected writes (`Scenario.cs:83`–`:86`). Currently result becomes
   passed inside the body at `:54`; that is too early even though finally subsequently updates some issue fields.
5. For ordinary Scenario cases the outermost return is the final verdict. For manual cases defer promotion until
   `ManualRun.Finalise`, the second subject fingerprint, observation.Record and output logging have returned
   (`ManualScenario.cs:71`–`:73`; `ManualRun.cs:220`–`:242`). A failure here must retain the previous passing video.
   The optional observed batch can later revoke evidence qualification; record that separately from test pass.
6. Publish only after successful encoding and successful final test verdict. Failed/aborted/skip attempts keep
   previous latest files; any partial/failed recording remains in that run's temporary record folder.

**R9. Make coverage explicit and mechanically checked.** Inventory every DesktopFact/Theory and parameter row:
`recorded`, `disabled`, `unavailable`, or `excluded` with reason. Reflection/fixture tests should fail if a newly added
desktop method has neither a recording wrapper nor a deliberate exclusion. `ObservedRunTests` has no automation
window and must preserve the measured observation boundary; `PlotViewTests` hosts a separate timing benchmark
window in the test process (`:20`–`:47`) and recording changes what it measures. Exclude those **two**, as P5 proposes,
with author acceptance of that scope clarification to V1. Do not describe the result as literally every DesktopFact
having video. Skip requires no launch and no recorder; an opt-out does not change test skip/pass semantics.

**R10. Separate capture from encoding and contain native hangs.** Parent-side recording scope sends metadata,
step events, privacy suppression and stop over a bounded pipe to a small harness-owned recorder console helper.
The helper has a dedicated capture thread at a **100 ms target period**, Stopwatch deadlines, no UIA on the polling
path, and an MTA encoding thread. Allow at most one pending frame plus one active frame; drop/coalesce under load
and report dropped slots instead of accumulating raw frames. Reuse pixel storage. This is streaming, not a movie-sized
frame buffer. At 1440×900 one RGB32 frame is 5,184,000 bytes.

An in-test capture thread alone cannot guarantee bounded teardown if a native WriteSample/Finalize call hangs.
The helper owns MF/COM and is assigned, before work starts, to a harness-owned kill-on-close Job Object; an inherited
parent-lifetime pipe and helper watchdog bound orphan lifetime. Parent has bounded startup/stop/publish waits
(proposed 3 s startup, 5 s drain), never an unbounded Join/WaitForExit. Normal completion exits voluntarily;
on a hung encoder close only its proven-owned helper job, never Studio or another process. Register this new helper
as a harness actor/image; do not confuse it with a Studio child or weaken Studio's no-force-kill policy.
This lifecycle is part of the proposed implementation, not a process termination performed in this review.

**R11. Capture the current owned frame into a fixed even-sized canvas, with cursor and conservative privacy.**
Measure physical DWM bounds after normalisation, pad odd widths/heights to the next even value, black padding;
save source rectangle and encoded dimensions. Revalidate HWND/PID/start identity every sample; reject minimised,
offscreen, changed ownership or unknown bounds. When the window moves use its current origin. When it resizes
(Chrome maximises/restores), fit its whole current frame into the original canvas with letterboxing; transform
cursor/mask coordinates identically, record resize/scale events. Never crop a fixed screen rectangle that might now
show unrelated content. Own popups outside the main frame are clipped and logged; document this recording limit.

GetCursorInfo supplies visibility and position; copy the icon, read ICONINFO hotspot, and DrawIconEx at
`cursorPosition − frameOrigin − hotspot`, clipped to permitted pixels. Release copied icons and both ICONINFO
bitmaps. Apply privacy masks **after** cursor drawing so a cursor/icon cannot repaint a masked region.

V5 is directionally sound but **not established by V1**. Adopt its privacy intent with these corrections:

- Extract the ManualCapture traversal into a shared window-survey helper. Enumerate **all** visible, uncloaked
  top-level windows above the owned frame, including windows above an owned popup. Compare PID and actual
  ownership, get DWM bounds with GetWindowRect fallback, intersect with frame/canvas, conservatively include shadows.
  Return rectangles/reasons, not window titles or screenshots. Do not report wholly covered windows below Studio.
- Survey immediately before and after pixel acquisition; mask the union of foreign rectangles from both surveys.
  Any failed enumeration/ownership/bounds check or concurrent geometry change gives a full neutral frame.
  Mask every foreign window, including transparent/layered ones. Union rectangles and clip; never preserve their
  pixels in diagnostics, buffers sent to the encoder or debug thumbnails.
- A two-survey screen-copy method still has a race if a foreign window appears/disappears entirely between surveys.
  State that limit; z-order checks cannot prove perfect privacy. Use a blank frame whenever safe capture is uncertain.
  Strict prevention requires a stronger compositor-isolation method, outside what the current screenshot code proves.
- **Native save dialog remains private even though it belongs to Studio.** Suppress before invoking the dialog and
  acknowledge suppression before proceeding. Emit neutral timed frames/captions until it and any path-bearing
  prompt close. Existing OmitWindowDiagnosticsReason is armed before ExportReport and never cleared
  (`ReportExportScenarioTests.cs:49`–`:53`); propagate it to video now, then narrow to a well-scoped pause only when
  dialog cleanup is proven. Same-PID allowance alone leaks Documents contents and user paths.
- Reuse CaptureQuality's ownership/size/privacy reason vocabulary and fixture tests; extend pure helpers for mask
  rectangles. Do not run its current RGB24 flat-image scan plus UIA privacy traversal at 10 Hz or demand “stable”
  during motion. Neutral/masked frames are deliberately flat, not invalid video. Manual still-image quality stays strict.

**R12. Implement the sink writer with explicit formats, timestamps and resource ownership.** In the helper's MTA:
CoInitializeEx; MFStartup; MFCreateSinkWriterFromURL to a unique `.mp4` temporary path; create a video output type
(H.264, progressive, even frame size, 10/1 fps, 1/1 pixel aspect, proposed 2,000,000 bit/s); AddStream; input media type
RGB32 (GDI B,G,R,X byte order, positive width×4 stride, explicit orientation); SetInputMediaType; BeginWriting.
MF's converter accepts RGB32 here; keep any NV12 fallback a deliberate tested path, not silent channel swapping.
Keep sink-writer throttling enabled. A bounded application queue does not bound the encoder's internal buffers;
measure paced long-run memory as well as the synthetic burst, and stop recording on resource exhaustion.

For each frame allocate/reuse safely owned MF memory buffers, Lock/copy respecting stride/Unlock, SetCurrentLength,
MFCreateSample/AddBuffer, SetSampleTime and SetSampleDuration, WriteSample, release sample/buffer references exactly
once after submission. HRESULTs fail the recording, not the scenario. Native declarations must have correct stdcall,
GUID layout, 64-bit timestamps and the complete inherited COM vtable order; test x64 and avoid a handwritten partial
IMFAttributes interface with shifted slots. Interop stays in test infrastructure.

Use Stopwatch, not wall time, for media/caption timing. Nominal slots are **1,000,000 units of 100 ns**. Anchor at
the first accepted or neutral frame; use scheduled slot times. If slots are missed, retain elapsed time as a gap
or duration extension, do not compress the test into `writtenFrames/10` seconds. Hold the last pending frame to know
its duration; flush it on stop. Clamp first/last cues to actual media duration. Log captured/written/dropped counts,
actual duration, nominal fps and encoded size. Finalize once and close/release writer before hashing or promotion;
MFShutdown then CoUninitialize on the owning thread. Hardware acceleration may be an optional tested setting;
the successful default sink writer does not identify whether hardware was used.

**R13. Treat publication as a transaction, and correct the “one rename” wording.** Each finalized MP4 is staged
under the run record, hashed, then copied to a **new** staging name on the destination volume before replacement.
Never assume a rename from the temporary volume to the repository is atomic. Use a cross-process exclusive
publication lock and atomic per-file replace; close files first. VTT and index are staged too. A failed test or encoder
error never touches latest. Opt-out leaves latest intact and logs this attempt's disabled status outside latest index.

There is **no single filesystem rename that atomically replaces a flat `<test id>.mp4`, `<test id>.vtt` and
`index.json` together**. With V2's fixed flat filenames, use a publication journal plus retained prior pair/index,
replace MP4/VTT individually, commit index last, recover an interrupted transaction before serving/publishing latest,
and make consumers verify hashes under the publication lock. Each individual MP4 remains whole, but mixed
generations can exist until recovery; do not promise a three-file transaction to an unlocked reader.
If the author intends strict all-files atomic visibility, the concrete alternative is immutable per-run MP4/VTT names
and one atomically replaced index pointer; flat names become convenience copies. That changes the literal naming
decision and needs the author's resolution, not an implementer's silent reinterpretation of V2.

**R14. Define a compact index and useful captions.** Proposed latest-passing index, independent of manual schema:

```json
{
  "schema": "gcam.ui-videos", "schemaVersion": 1,
  "tests": {
    "<stable-case-id>": {
      "runId": "<run>", "passedAtUtc": "<ISO-8601>",
      "commit": "<full-sha>", "dirty": false,
      "appBinarySha256": "<sha256>", "deploymentSha256": "<sha256>",
      "video": { "path": "<case>.mp4", "sha256": "<sha256>", "bytes": 0 },
      "captions": { "path": "<case>.vtt", "sha256": "<sha256>" },
      "duration100ns": 0, "nominalFps": 10,
      "widthPx": 1440, "heightPx": 900,
      "frames": 0, "capturedFrames": 0, "droppedSlots": 0,
      "maskedFrames": 0, "privacySuppressedFrames": 0,
      "maskRanges": [ { "firstFrame": 0, "lastFrame": 0,
                         "reasons": ["foreign-window"], "rectsPx": [[0,0,20,20]] } ]
    }
  }
}
```

Use empty maskRanges when there are none. Define frame indices as zero-based **encoded** sample indices;
range-compress identical masks, keeping timestamps for long gaps. These counts refer to whole frames containing
any mask, not rectangle counts. Reject unsafe relative paths, hash mismatches, duplicate IDs, negative times,
unknown versions and out-of-range masks. No user/machine names or absolute paths in the repository index/captions.
Do not overwrite the latest passing entry with failure metadata; per-attempt `video-status.json` belongs in its run.

Write UTF-8 `.vtt` beginning `WEBVTT`, with numbered cues and `HH:MM:SS.mmm --> HH:MM:SS.mmm` intervals.
Collect structured monotonic step events: RunRecord.Step currently retains only local clock strings (`:44`), while
ManualRun.BeginStep (`:95`) retains no timestamp. Add an event callback to both, use resolved manual text after label
resolution, close each cue at the next step/end; coalesce equal millisecond starts. Sanitize path/identity/error
text, escape `&`, `<`, `>` and remove injected cue arrows/control characters. Polling diagnostics should not drown
reader step names; privacy pauses get a generic caption. Captions are an aid, never a pass/fail oracle.

**R15. Recorder faults must not change the test verdict.** Unavailable encoder, disk full, unsupported size, capture
failure, COM exception, lost pipe, timeout, VTT/index error or locked destination => attempt status with bounded
diagnostics, no latest replacement, original test failure preserved. A passing test can have `video unavailable`;
the task's video-completeness acceptance check then reports the missing video separately. Promotion errors must not
be added to Scenario's safety/cleanup issues as if Studio failed. Parent never waits indefinitely on the helper.
Do not claim full video coverage from test pass counts alone.

## 6. Headless measurements and reproduction

Scratch probe: `%TEMP%/gcam-todo48/Probe.csproj`, `Program.cs`; net9.0 console, no packages, no UI/screen APIs.
It P/Invokes MFStartup, media types/buffers/samples and MFCreateSinkWriterFromURL and calls COM vtables. Synthetic
600 frames, 1440×900 RGB32, 10 fps, 60 s media duration: RGB=(30,30,30) background and a 200×150 coloured rectangle
moving two pixels per frame horizontally; 2 Mbit/s output request. No real display pixels or external input files.
The loop builds frames, copies/submits them, and finalises; process CPU and peak working set come from Process APIs.

All heavy commands below were sequential, preceded by this memory check; measured free memory was always above
12 GiB (18,481,932 KiB before initial build; 18,505,668 before first encoding; 18,493,468 before second encoding).
Each guard command wrote a log without a pipeline and its exit code was checked. Use PowerShell syntax:

```powershell
(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory
python -B $env:TEMP/gcam-memguard/memguard.py -- dotnet build $env:TEMP/gcam-todo48/Probe.csproj -c Release --disable-build-servers > $env:TEMP/gcam-todo48/probe-build.log 2>&1
$LASTEXITCODE
python -B $env:TEMP/gcam-memguard/memguard.py -- dotnet $env:TEMP/gcam-todo48/bin/Release/net9.0/Probe.dll $env:TEMP/gcam-todo48/synthetic-01.mp4 > $env:TEMP/gcam-todo48/probe-run.log 2>&1
$LASTEXITCODE
```

Before repeating a heavy command, perform the same free-memory check and wait below 12 GiB. Never overwrite an
existing output during reproduction: choose a new synthetic suffix. The second encode used `synthetic-02.mp4`
and `probe-run-02.log`; the same encode loop was used after adding the independent readback mode.

| Measurement | Encode 1 | Encode 2 |
|---|---:|---:|
| Encoder accepted RGB32→H.264; build/run exit | yes / 0 | yes / 0 |
| Total elapsed, setup+frame generation+submission+Finalize | 1.6998513 s | 1.6201365 s |
| Total / 600 (amortised encoding-pipeline cost) | 2.8331 ms/frame | 2.7002 ms/frame |
| Frame loop / 600 | 2.5894 ms/frame | 2.5545 ms/frame |
| WriteSample call / 600 | 0.007025 ms/frame | 0.006936 ms/frame |
| Finalize | 13.3508 ms | 3.7637 ms |
| Process CPU time | 4.234375 s | 4.421875 s |
| CPU / elapsed, one logical-core equivalent | 249.10% | 272.93% |
| CPU / elapsed / 24 logical processors | 10.38% | 11.37% |
| Peak process working set | 336,683,008 bytes (321.09 MiB) | 336,588,800 bytes (321.00 MiB) |
| MP4 bytes for 60 s | 288,590 | 288,590 |

WriteSample is asynchronous; **0.007 ms is not the encoder's total cost**. At real-time pacing the synthetic CPU
budget is about 0.071–0.074 core-seconds/second (7.1–7.4% of one core), by division by 60 s, **an extrapolation**.
This excludes screen copy, cursor/mask survey, UI contention and paced behaviour. The tiny static-scene file is
0.289 MB/min; not a bound for Studio. A 2 Mbit/s bitrate request suggests an order of 15 MB/min if used fully,
but is not a hard size cap. No default hardware encoder claim is made.

Artifact verification was headless: scratch `inspect.py` independently parsed MP4 boxes and found `avc1`,
1440×900, 600 samples, media timescale 10,000 and duration 600,000 (60 s). Output SHA-256 for encode 1:
`a5deb88df3069499148e5040ae4f08129637a24ac6c552d420ba349a5a689bcf`.
The source-reader readback explicitly decoded **600 NV12 frames**, first timestamp 0, last 599,000,000 (100 ns).

```powershell
python -B $env:TEMP/gcam-todo48/inspect.py
# Recheck free memory before the following guarded readback too.
python -B $env:TEMP/gcam-memguard/memguard.py -- dotnet $env:TEMP/gcam-todo48/bin/Release/net9.0/Probe.dll read $env:TEMP/gcam-todo48/synthetic-01.mp4 > $env:TEMP/gcam-todo48/probe-read-nv12.log 2>&1
$LASTEXITCODE
```

One preliminary readback request for RGB32 without source-reader video processing failed with `0xC00D36B4`
(unsupported/inconsistent media type); `probe-read.log` retains that failed experiment. Requesting minimal NV12
decoder output fixed readback; this was not an encoder failure. The final scratch source SHA-256 is
`188b63edd91761515b22894404fb61f5dedb1456e7ff42921e02a6c7fb9291e6`;
project SHA-256 `00e8f18266a79be9f10406444e7b78cddf8b3b96f73ac7dc928b8134fedcc7ca`.
Build-readback logs are `probe-build-read.log` / `probe-build-nv12.log` (both exit 0).

Not measured: actual screen/cursor/mask cost; real Studio size/quality per minute; cold and sustained recorder memory;
interference with readout cancellation, focus sweep or redraw; privacy-race frequency; another machine's encoder;
new chapter/preparation timing. These require the authorised desktop turn, including recorder-on versus opt-out
comparisons of timing-sensitive cases. Do not claim those scenarios still pass based on the synthetic encode.

## 7. P6 / P7 / P8: replay, interpreter and documentation

**R16. Keep video provenance optional and pinned inside the retained chapter record, never a live index lookup
during replay.** Existing `check` replays edition-local `sources/manual` and `sources/runs`, then compares the entire
output set (`manual_records.py:1433`–`:1465`). It has no video dependency. Measured with **no artifacts/ui-videos
directory**: sources valid, **1 committed edition replayed**, exit 0:

```powershell
# FreePhysicalMemory was 18,539,868 KiB before this command.
python -B $env:TEMP/gcam-memguard/memguard.py -- python -B samples/manual/manual_records.py check --all > $env:TEMP/gcam-todo48/manual-check.log 2>&1
$LASTEXITCODE
```

Add optional `{runId, testId, videoSha256, captionsSha256}` provenance to a new manual-run field, not its required
file evidence list (which is copied and hashed at `:1385`–`:1390`). Pin it before/with manual finalisation from the
completed run-local encoder result, **not** from an unrelated older passing index entry. A missing recording is
explicitly absent, not an older hash. Assembly may echo the pinned values only when present; omit the field for
old records so edition 1 remains byte-identical. Latest index can later change or disappear without changing replay.
Avoid a finalisation cycle: completed encoding → manual record → final test success → promotion. The manual record
describes the run-local recording, not a promise that latest promotion succeeded.

Test old-edition replay, new-edition replay after video deletion/absence, changed optional hash detection, and an
encoder-failed manual run with valid nonvideo evidence. CI continues to run manual-editions/self-test via
`.github/workflows/ci.yml:126`–`:129`; no video files, MF startup, UI opt-in, launcher installation or desktop needed.

**R17. Fix interpreter selection without silently skipping the cross-language test.** Prefer a dedicated executable
override (proposed `GCAM_TEST_PYTHON`), otherwise resolve `python` with a short `-B -c` probe returning `sys.executable`
and version, validate execution, and use that resolved executable for stage0. If that fails, try an available Windows
launcher (`py -3`, not a required installed 3.13) and validate what it resolves. Use ProcessStartInfo.ArgumentList,
redirect/drain both streams concurrently, bounded waits, owned-process cleanup, and report attempted commands/errors.
Do not enumerate installation directories or the registry. If no interpreter works, fail with an actionable diagnostic
instead of skipping or weakening byte/path assertions. A timeout cancellation currently only cancels the wait
(`ContentFingerprintTests.cs:30`); ensure resolver probes cannot leave children behind.

Reason: a launcher can be absent while CI's setup-python executable works. Keeping working `python` first preserves
CI's selected 3.13 (`ci.yml:21`–`:23`); adding the resolver does not alter the fingerprint algorithm or fixture.
Add injected-process fixture cases for working PATH, failed alias plus launcher, explicit override, bad output,
nonzero exit and timeout. No new Python installation is required. The historical cause still needs a failure log
or a full-suite reproduction in implementation; this review did not run the full solution.

**R18. Documentation/change list for the implementation:**

- `.gitignore`: add repository-root `/artifacts/`; it is **not present now**. Document latest passing versus attempt
  records, opt-out, exclusions, privacy pauses, masking limits, worktree lifetime and destructive clean semantics in
  `docs/AGENTS.UiAutomation.md`. Record rationale for new normative recorder rules in `docs/AGENTS.Rationale.md`.
- Manual sources: `docs/manual/chapters.json`, `notices.json`, `purposes.json`, both `static/*.md` sources
  (including their source citations and draft status); new edition folder with Markdown/PDF/DOCX, figures, retained sources and generation record.
  Keep edition 1 immutable. Update relevant root/source README manual links and `AGENTS.md` index if needed.
- `docs/VV.Studio.md`: manual chapters traced to SR-EXPORT-01…05 / SR-CMP-01…05 with explicit limits; video alone is
  not new evidence of those requirements. `docs/VV.Studio.History.md`: actual desktop build/run/verdict/observed
  classification/edition review after execution. `docs/VV.Tests.md`: new recorder/oracle/resolver classes and checker
  fixtures; refresh generated catalogs/reports through their generator, not handwritten inventory counts.
- Update survey/action/isolation declarations for exact manual export paths and recorder actors/destinations;
  reconcile charter applicability and observation records with the planner. New evidence targets need static
  target-policy coverage. Do not silently broaden observation filters to hide recorder files.
- No product SRS change is necessary merely to describe existing export/Compare features. Propose SDS/trace text
  only if a test-only Compare evidence hook is added. VV documents must remain self-contained.

## 8. Implementation size and author decisions

**R19. Budget as harness infrastructure plus two chapters, not a small manual refresh.** Estimate, excluding
generated assets and optional broad raw Compare evidence:

| Group | Files new / modified (approximate) | Added/changed handwritten lines | Verification |
|---|---:|---:|---|
| Recorder helper project, MF interop, capture/masks, IPC | 5–7 / 0–1 | 850–1,250 | Synthetic encode/decode; bounded queue/timing; helper timeout and resource lifetime |
| Scope/verdict/publishing/index/caption integration | 2–3 / 5–7 | 450–700 | Final-assertion/manual-finalisation failures; per-format IDs; crash/publication recovery |
| Manual export/Compare scripts and guarded helpers/evidence | 2–3 / 4–6 | 450–700 | Two chapter mutation/recovery pairs; independent file readers and population fixtures |
| Generator provenance and Python resolver | 1 / 2–3 | 120–220 | Legacy/new replay with absent videos; resolver success/failure/timeout |
| New headless test classes | 4–6 / 2–4 | 450–700 | About **25–40 meaningful cases**, including negatives; fixture fakes for encoder/files/windows |
| Sources/docs/catalog/ignore | 0–1 / 12–18 | 180–300 | Schema/catalog/link/replay checks; author notice/static/figure review |

Overall about **14–21 new files, 25–39 modified files, 2,500–3,900 handwritten lines**, plus a generated edition.
The optional stronger Compare raw-input evidence increases the budget; a single in-process thread reduces code but
does not meet the native-hang containment goal. Desktop count would become **44 cases** with two new manual facts;
normally recordable cases **42**, still excluding the observation and benchmark facts, before opt-outs/long gates.
These are estimates, not a revised authoritative inventory.

Author decisions to resolve before implementation / edition publication:

1. Approve the 10-chapter scope, part 4 containing 4.4, proposed chapter titles and self-contained Baseline setup.
2. Approve N-NOT-SAVED / N-EXPORT-SCOPE wording, expanded notice placements, both static source revisions and new
   mark-purpose wording. Approve figures and complete edition after the desktop run.
3. Accept no native-save-dialog screenshots/video pixels, neutral-frame privacy pauses, and V5's revised masking
   design/remaining race limitation. There is no safe basis for the plan's proposed confirmed-path dialog figure.
4. Clarify V1 coverage for the observation and timing-benchmark exclusions, and accept explicit unavailable video
   status on a passing test when recorder infrastructure fails. This preserves V3 rather than faking completeness.
5. Resolve the literal V2 “one rename” versus multi-file atomicity conflict: journalled fixed names with locked
   readers/recovery, or immutable run names with an atomic index pointer. Recommended strict transaction option is
   the latter, but V2's naming cannot be changed without the author.
6. Accept the owned helper architecture/bounded shutdown, initial 2 Mbit/s and timeout defaults, then check real
   recording quality/cost. V1–V4's pass-only promotion, cursor, H.264, captions, opt-out and ignored folder stay fixed.
7. Approve concrete new isolation/target permissions only if the implementation expands them; grant the desktop
   turn on its actual build. A fresh applicable observed run is required; old-record reclassification alone is not.

Only this review is a repository change in this turn. Scratch probe/build/video/logs remain for audit; no deletion,
git mutation, installation, process stop or network upload was performed. No test/probe process is intentionally
left running. Full-solution/fingerprint and all desktop/recorder-on checks remain for implementation and the
authorised desktop turn, with the memory guard.

## APPROVAL REQUESTS

None for this review. No irreversible command is needed to retain the review and scratch evidence.
