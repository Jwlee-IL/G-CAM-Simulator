# PLAN.Docs.StudioManual2 — the manual's second edition and a video of every desktop test

Scope: TODO-48. Generate the second edition of the GCAM Studio manual on the current build, with new chapters for the
report export (TODO-46) and the Compare workspace (TODO-47), and record a video of every desktop UI test, keeping the latest
passing video per test. The process is the first edition's ([PLAN.Docs.StudioManual](PLAN.Docs.StudioManual.md), its
[charter](PLAN.Docs.StudioManual.Charter.md) and [review](PLAN.Docs.StudioManual.Review.md)); this plan
lists only what changes.

Status: **done 2026-10-16.** Edition `2026-10-16-1` (formal) generated from the observed manual batch
`manual-20261016-02` on `46c9ada`; every recorded desktop test has a video (record in VV.Studio.History). Turn 2 by Codex,
finished by a substitute agent; desktop turn by the planner (MD11–MD13 from it). History: reference plan 2026-10-16 by the planner, video decisions V1–V4 the author's (2026-10-15); review by
Codex 2026-10-16 ([review](PLAN.Docs.StudioManual2.Review.md), R1–R19, a measured sink-writer encode); worktree
`C:\gw\w48`, branch `todo48-manual-second-edition`.

## Why a second edition

| Change since edition `2026-10-15-1` | Effect on the manual |
|---|---|
| Report export (TODO-46, D-58): Studio's one write surface (HTML, PDF, Word, event CSV) | needs a chapter. The notice N-NOT-SAVED ("Studio saves nothing on its own") must be re-worded and re-approved: Studio still keeps no settings and saves nothing unasked, but it writes a report when the user confirms a path. The charter reopens M03 / M06 for that one surface, and the edition needs a new observed run on the new build (charter, Applicability) |
| Compare workspace (TODO-47, SR-CMP-01 … 05): a fifth workspace, Ctrl+1…5 | chapter 1.2 shows four workspaces and Ctrl+1…4; it needs a new run. Compare needs a chapter (TODO-47 C8: "TODO-48 picks Compare up") |
| New app binary | every generated chapter is re-run (one edition = one binary) |

## Decisions already made

| # | Decision | Kind |
|---|---|---|
| V1 | **Every desktop UI test records a video** of the Studio window region: about 10 frames per second, MP4 / H.264 through the Windows Media Foundation encoder (built in, nothing installed), encoded as it is captured (no frame buffer in memory). The mouse cursor is drawn into the frame. The scenario's step names go to a `.vtt` caption file beside the video. Opt-out with `GCAM_UI_VIDEO=0` | author 2026-10-15 |
| V2 | **Only a passing test replaces its video.** The recorder writes to a temporary file; after the test's final verdict (after cleanup) a pass replaces `artifacts/ui-videos/<test id>.mp4` (and `.vtt`) in one rename, so an interrupted run never leaves a broken video. `artifacts/ui-videos/index.json` lists, per test: the run id, date, commit, dirty flag, app binary SHA-256, length, frame count, file SHA-256, and the frames flagged by the overlap check (V4) | author |
| V3 | **A failed, skipped or aborted test keeps the previous passing video.** The failure video stays only in that run's record folder (`%TEMP%`, a derived file that may be deleted) | author |
| V4 | **The video folder is inside the repository and git-ignored** (`artifacts/` in `.gitignore`). Stated limits: `git clean -fdx` deletes it; a run in a worktree writes into that worktree and goes with it, so only MainRoot runs keep their videos | author |
| V5 | **Foreign windows are not recorded silently.** The capture is the screen region of the Studio window, because popups (drop-downs, Options, the export dialog) are separate windows that a window-content capture would miss. A frame whose region another process's window overlaps is masked over that window's rectangle and counted in `index.json`; reuse the occlusion test of `Harness/CaptureQuality.cs` | planner (from V1, to verify) |

## What exists (checked at `2519410`)

| Item | Where | Note |
|---|---|---|
| Manual sources and generator | `docs/manual/{chapters,notices,purposes}.json`, `docs/manual/static/`, `samples/manual/manual_records.py` (`generate`, `check --all`, `self-test`), `manual_formats.py` (PDF, Word) | 8 chapters (6 generated, 2 static); edition `2026-10-15-1` 3.9 MB |
| Manual scenarios | `tests/Gcam.Studio.UiTests/ManualScenarioTests.cs` (6), `Harness/Manual{Scenario,Run,Capture,Observation,Registry}.cs` | the review's chapter list (§3, 18 chapters) is the backlog for later editions |
| Desktop tests | 39 `[DesktopFact]` in 12 classes; 37 go through `Scenario.Run` / `RunReportExport` / `ManualScenario.Run`; `ObservedRunTests` (launch–idle–exit with no automation) and `PlotViewTests` (a timing benchmark) do not | the recorder must hook where the verdict is known |
| Window capture | `RunRecord.CaptureWindow` (DWM frame + `CopyFromScreen`), `ManualCapture`, `CaptureQuality` (owned / foreground / occluded / stable / privacy) | basis for the recorder's frame and overlap test |
| Encoders on this machine | `C:\Windows\System32\mfh264enc.dll`, `mfreadwrite.dll` (Media Foundation sink writer); no ffmpeg | the CI runners do not run desktop tests |
| Export scenario | `ReportExportScenarioTests` (one desktop scenario, four formats into owned sandboxes) | the export chapter can reuse its helpers, not its oracle as is |
| Compare scenario | `CompareScenarioTests.PhysicalPrefix_ComparisonDifferenceAndReset_KeepTheirPopulationLabels` | needs a Baseline physical preparation (~8 s) |
| Known local failure | `ContentFingerprintTests` starts `python`; on this machine that is the Microsoft Store alias, which is refused ("access denied") only in full-solution runs (2 of 3 on 2026-10-15); it passes alone and in CI | fix in this task: start a real interpreter |

## Proposed (verify / improve)

| ID | Proposal |
|---|---|
| P1 | **New chapters.** 3.7 "Export a report" (generated: open the export popup, each format into the owned sandbox, the confirmed-path dialog, sidecars named; check each written file by its own reader, not by the exporter) and 4.4 "Compare the ideal and the physical readout" (generated: prepare Baseline, acquire, Compare pair and difference view, populations, Reset; checks from `CompareScenarioTests`). Part 4 "Experimental readout" enters the registry with only 4.4 in this edition, or 4.4 waits until 4.1–4.2 exist — **verify** which reads better and what the registry allows |
| P2 | **Re-run and re-capture** chapters 1.2, 2.1, 2.2, 2.4, 3.1, 3.4 on the new build; 1.2 now shows five workspaces. Static 1.1 and 5.2: update for the export and Compare (author approves the wording) |
| P3 | **N-NOT-SAVED re-worded** (author approves): Studio keeps no settings and saves nothing unless the user confirms an export path; the export writes the chosen file and its declared sidecars only. A new notice for the export chapter if needed (e.g. the report is a simulation record, N-SIM applies) |
| P4 | **Observed run on the new build** (charter: export reopens M03 / M06): one light G1 run per the charter's MR8 method. **Verify** with the updated UI procedure guidance (lightest method that answers the question; a change that touches only the background-filter table is closed by re-classifying the existing record, without a new approval or pilot) whether an existing record re-classified is enough or a new run is needed |
| P5 | **Recorder design** (verify): a `VideoRecorder` in the UI-test harness, started by `Scenario.Run` / `ManualScenario.Run` after the main window appears and stopped in their final cleanup, where the verdict is known; a capture thread at a fixed period with the cursor drawn (`GetCursorInfo` / `DrawIconEx`); Media Foundation sink writer through COM interop (RGB32 in, H.264 out; even dimensions; a fixed size per test, the window's size at start); captions from the record's steps with times relative to the first frame. `ObservedRunTests` records nothing (its file snapshots would see the harness's own writes); `PlotViewTests` records nothing (a timing benchmark). **Measure:** capture cost per frame, CPU share, file size per minute, and whether the timing-sensitive scenarios (readout cancellation, focus sweep) still pass with the recorder on |
| P6 | **Edition traceability:** the generation record may cite each chapter run's video SHA-256 from `index.json` (not the file; videos are not committed). **Verify** that `check --all` replays committed editions without the videos present (CI has none) |
| P7 | **Fingerprint test:** start an interpreter that is not the Store alias (for example `py -3.13` when the launcher exists, else `python`); CI behaviour unchanged |
| P8 | **Docs:** `AGENTS.UiAutomation` (video, opt-out, folder, overlap masking), `.gitignore`, the manual's sources, VV.Studio trace rows for the new chapters, catalog entries for new test classes, History |

## Decisions after review (2026-10-16)

The review corrected the premises (38 facts + one 4-row theory; `CaptureQuality` has no overlap detector, the traversal is
`ManualCapture.Occluder`; Options and Reset are in-window layers; `Scenario.Run` marks a pass before its final checks;
the 1.2 scenario never presses Ctrl+1…4; the Compare scenario checks labels and states only). Its measured encode: 600
synthetic 1440×900 frames in 1.6–1.7 s, about 7 % of one core at real time, 321 MiB peak, decoded back frame for frame.

| # | Decision | Kind |
|---|---|---|
| MD1 | **Edition scope as R1:** ten chapters (eight generated, two static); new 3.7 "Export reports and event data" and 4.4 "Compare direct crystal and physical readout"; part 4 holds only 4.4, with its own Baseline setup | planner (author reviews the edition) |
| MD2 | **Chapter scripts as R2** — all eight generated chapters re-run on one deployment; 1.2 presses Ctrl+1…5 and checks the workspace that appears; each chapter fails its deliberate verdict mutation and passes recovery. **R3:** a manual export entry with an exact declared-write list, one chapter run with one subfolder per format | planner |
| MD3 | **Compare oracle (author):** read the displayed population numbers through UI Automation and recompute in the test — same / eligible, assigned + unknown = held, same ≤ eligible, different = eligible − same, empty denominators unavailable. No new evidence branch in Studio | author |
| MD4 | **Notices and static chapters as R4–R6:** N-NOT-SAVED re-worded, a new N-EXPORT-SCOPE, placements extended; static 1.1 and 5.2 revised and new mark purposes — all drafts until the author approves them with the edition | planner (author approves) |
| MD5 | **Observed run as R7:** the new build gets an applicable observed batch with the light before/after method; its first pilot uses the export (HTML into its guarded sandbox); one qualified observed manual batch serves both the observation and the chapters. The recorder's own writes are accounted as harness effects | planner |
| MD6 | **Recorder in the test process (author):** a background capture thread at a 100 ms period and an encoding thread (MTA, Media Foundation sink writer, RGB32 → H.264, 2 Mbit/s), at most one pending and one active frame. Stop waits a bounded time (5 s); on a timeout the recording is abandoned as unavailable — background threads do not keep the process alive. No helper process. Lifecycle, coverage inventory, capture, cursor, sink writer, index, captions and fault rules as R8, R9, R11, R12, R14, R15 | author |
| MD7 | **Publication (author):** fixed names; each file is copied to a staging name on the destination volume and replaced on its own — MP4, then VTT, then `index.json` last. A run interrupted between them leaves a hash mismatch the next publication repairs; no journal and no lock (desktop runs are already serialised by the desktop lease) | author |
| MD8 | **Foreign windows (author) as R11:** survey the window stack just before and after each frame, mask every other process's window over the frame, a full neutral frame when the survey fails; the native save dialog is shown as neutral frames although it belongs to Studio; the remaining race (a window that appears and vanishes between the two surveys) is stated in `AGENTS.UiAutomation` | author |
| MD9 | **Exclusions and faults:** `ObservedRunTests` and `PlotViewTests` record nothing (a stated clarification of V1); a recorder fault never changes a verdict, and a passing test may then show "video unavailable" | planner (from V1–V3) |
| MD11 | **Save-dialog history (author, 2026-10-16, from the first observed manual batch):** the Windows save dialog, in Studio's process, records the confirmed file in the per-extension recent list and the last folder per application (HKCU `ComDlg32`). Declared as the OS dialog's record (E6, RC-09) on the export surface; Studio's own claim stays "no folder or setting remembered". The draft N-NOT-SAVED says so | author |
| MD12 | **Shell display-name cache (author, 2026-10-16):** the MuiCache values written when an executable runs from a new path (a new worktree) are an OS record observing processes, recorded as a background filter (RC-10, evidenced by `probe-20261016-01`) | author |
| MD13 | **Edge pixels (author, 2026-10-16):** frames and screenshots copied the window's frame bounds, which showed one row and column of the window behind Studio. All captures now get a neutral two-pixel edge. Edition 1's committed figures keep their row (one pixel of glyph slices, unreadable); recorded in History, not regenerated | author |
| MD10 | **Provenance, interpreter, docs as R16–R18:** an optional pinned video hash in the manual run record (edition 1 stays byte-identical); the fingerprint test resolves a working interpreter (`GCAM_TEST_PYTHON`, then `python`, then `py -3`) and never skips; `.gitignore` gains `/artifacts/` | planner |

## Steps

1. **Turn 1 — review only** (Codex, no desktop): check every row above in the code; propose the chapter list for the
   edition with each scenario's steps, checks and captures; design the recorder (P5) with measured headless costs where
   possible (encoder availability, sink-writer interop on a synthetic frame sequence); list author decisions.
   Writes `docs/PLAN.Docs.StudioManual2.Review.md`.
2. **Decisions after review** (planner + author: N-NOT-SAVED wording, chapter list, static chapter text).
3. **Turn 2 — implement headless:** recorder, index, captions, opt-out, overlap masking, the fingerprint fix, registry and
   static drafts, the new manual scenarios (compiled), generator changes, unit tests on fixtures, catalog.
4. **Turn 3 — desktop (author's go each run):** the observed run if P4 needs one; the manual scenarios; the generator;
   the planner opens the edition and looks at every figure and at the videos; then the affected regression scenarios with
   the recorder on.
5. **Record and close:** the second edition committed with PDF and Word; docs; Todo row removed; plan archived. Then send the
   recorder's design and the measured results to the paired procedure session so it can make the same videos (author,
   2026-10-16).

## Done when

- Edition 2 is generated from passed runs on one binary, with the export and Compare chapters, approved notices and static
  chapters, and replays byte for byte in CI.
- Every desktop test that passed in the latest run has a video and an `index.json` entry with that run's identity; a failed
  test kept its earlier video; masked frames are counted.
- The full normal suite passes on this machine, including the fingerprint test.

## Not here

- Committing videos, or a video inside the PDF or Word edition.
- The remaining chapters of the review's list (2.3, 3.2, 3.3, 3.5, 3.6, 4.1–4.3, 5.1, 5.3) beyond what P1 decides.
- A Compare section in the report export (TODO-47 C8).
