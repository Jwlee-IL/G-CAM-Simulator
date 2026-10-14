# PLAN.Docs.StudioManual — a GCAM Studio user manual generated from UI-automation runs

Scope: TODO-43. Produce a user manual for GCAM Studio whose operating chapters are **generated from desktop UI-automation
runs** that actually drove the app — the screen labels, the steps and the figures come from one run's record, never
from memory or hand-made screenshots — plus hand-written static chapters for what a run cannot show. The model is the
author's existing document-automation procedure from another project (the same source TODO-28 drew its test-record
process from); only its **manual process** is adopted (chapter registry, generated vs static chapters, run record,
capture and figure-mark rules, omission notices, generation record), none of its content or names. The implementer is
given the procedure's location in its prompt; it is not named in the repository.

Status: **stage 0 started 2026-10-13 (headless part)** — turn 1 review by Codex (conversation `01a134cc…`,
[review](PLAN.Docs.StudioManual.Review.md)), supplemented against the UI-automation procedure v3.0.1 and the document-
automation procedure v3.0.1; "Decisions after review" below (partial — the edition decisions wait for the author). Next:
G1 qualified by observed run 09 (2026-10-14, MR8); next the manual work (MR9: chapter registry, M15–M17) after the
open edition decisions (MR5).

## The model process (what is adopted)

```text
[runner: desktop scenario] ── run record + captures + figure marks ──> [generator] ── edition + generation record ──> [control: author's review / commit]
                                                                            ▲
                                       static sources (chapter registry, static chapters, verification catalog, risk list)
```

| Principle | What it means here |
|---|---|
| The chapter registry is the table of contents | one machine-readable registry lists the parts and every chapter: number, title, part, source type (`generated` / `static`), the scenario that generates it or the static file that holds it; scenario code, assembler and checks read numbers and titles from it, nobody types them twice |
| Generated chapters only from passed runs | a generated chapter is printed only when its scenario passed in the latest run on the same app binary; otherwise the edition carries an omission notice for it. Verdicts and logs are **not** printed in the manual — they stay in the run and generation records |
| Each generated chapter has a verification procedure | the scenario's automatic checks (screen state, values against an independent oracle) are the steps of a paired verification procedure; a chapter with no check is not generated |
| Nothing that is not on the screen | no unexecuted operation, absent feature or unmeasured performance is described; screen labels are the labels read in the run |
| Warnings and cautions come from a risk list | a scenario attaches a reference to a risk-list item; the generator takes the level and wording from that item, never from code |
| Figures are run evidence | only crops and mark overlays are derived, each recorded with source and result hashes; mark rectangles are measured by the runner at the moment of capture, never placed by the generator |
| One edition = one binary + one locale | a changed app binary means every generated chapter is re-run; earlier runs are not mixed in |
| Omissions are shown, not filled | a missing run, a failed chapter, a rejected capture each appear as a notice in the edition and as an entry in the generation record |
| The generator makes no human act | review and sign-off are the author's, outside the tool |

Not adopted (no counterpart here, to be confirmed in the review): patient-data handling, operator-performed steps
(Studio runs need no operator), organisation-specific document templates, a second locale.

## What exists (checked in the repo, 2026-10-12, at `a2c7580`)

| Item | Where | What it does / gap for a manual |
|---|---|---|
| Desktop scenarios | `tests/Gcam.Studio.UiTests/{Scenario,WorkspaceScenario,MlemScenario,ReadoutScenario}Tests.cs` (`[DesktopFact]`, `GCAM_UI_TESTS=1`) | regression flows with independent oracles (`Harness/WorkspaceOracle.cs`, `FloodOracle.cs`): Start / Stop / Continue / Reset, ROI, angle, move source, theme toggle, spectrum window, waveform events, detector lock, channel and strip, refocus / sweep, MLEM, physical readout. Written as checks, not as user procedures — step granularity and wording for a reader are missing |
| Run manifest | `Harness/RunRecord.cs` | schema 1, profile P1 "exploratory": run id, scenario, machine (OS, culture, DPI awareness), dirty-file count, free-form steps with times, result. No subject binary hash, no per-step check / capture lines, no evidence hashes. Saved before the final cleanup assertions (TODO-28 review), so it is not the verdict |
| Window capture | `RunRecord.CaptureWindow` (DWM visible frame + `CopyFromScreen`) | works; used for the failure bundle, the polish survey and the README capture (`PolishSurveyTests`, `docs/assets/studio-desktop-imaging.png`). No capture quality check (occlusion, foreground, focus), no figure-mark file |
| Element geometry | UI Automation `BoundingRectangle` / AutomationIds on controls | available for mark rectangles — verify coverage of the controls a manual would point at |
| Test records | `samples/testing/test_records.py`, `docs/VV.Tests.md`, `docs/VV.Tests.Results.md`, milestones `samples/testing/records/` (TODO-28) | run records and a generated report for test runs; the verification procedure's results could ride on the same machinery — verify |
| Risk table | `docs/VV.Studio.md` §6 "Risk table (illustrative, IEC 62304 §7 style)", six hazardous situations; RC requirements in `VV.Studio.SRS` §6 | prose table, no item IDs, no chapter mapping — candidate risk list |
| Locale | Studio has no resource localisation; labels are English | a single English edition |
| Charter | `docs/AGENTS.UiAutomation.md` §0: screenshots are not pass/fail criteria; runs move the real mouse, desktop opt-in | manual captures are evidence for the document, not verdicts — consistent with the charter, but the charter text should say so |
| Product scope | D-34: Studio is the simulator's engineering viewer, not the product UI | the manual is an engineering-viewer manual for the simulator, not the hand-held product (theme 22) |

## Proposed decisions (verify / improve)

| ID | Decision |
|---|---|
| SM-1 | **Subject and audience:** GCAM Studio as the simulator's engineering viewer (D-34); readers are users of this repository who want to drive the viewer. English edition only. The manual states on its face that Studio is a simulation viewer, not a measuring instrument |
| SM-2 | **Chapter registry** (JSON, static source, proposed `docs/manual/chapters.json`): registry id, locale, parts, chapters (number, title, part, `generated` with a scenario id, or `static` with a static-file id), the verification-procedure id. A chapter number appearing as both generated and static stops generation. **Verify** a first chapter list against the existing scenarios: e.g. getting started (static), the window and workspaces, running an acquisition (Start / Stop / Continue / Reset), placing and moving a source, the Imaging workspace (method, refocus / sweep, measurements: ROI, readout, angle), Spectrum, Waveform, Detector (incl. physical readout), themes, limitations (static), troubleshooting (static) |
| SM-3 | **Manual scenarios:** new desktop scenarios written as user procedures (one per generated chapter), reusing the existing harness and oracles; each step is a reader-meaningful action with a capture where useful, and each check belongs to one verification step. **Verify** whether existing regression scenarios can be reused as-is (likely not: their step order and fixtures serve the oracle) or should share helpers only |
| SM-4 | **Run record for manual runs:** extend `RunRecord` (new schema version) or add a separate record, carrying subject (app binary SHA-256), tool (commit, dirty, tree id), environment (OS, DPI, culture, theme, window size, screen scale), the procedure (`chapter-registry` and the paired verification procedure), cases → steps with `action` / `check` / `capture` / `error` lines, evidence by hash, and a final status written **after** cleanup. **Verify** alignment with the TODO-28 run-record format so one checker vocabulary serves both |
| SM-5 | **Captures and marks:** capture the visible frame (existing method) after a quality check (window foreground, unoccluded, expected size; otherwise the capture is `rejected` and the step lists it as missing); a figure-mark file per marked capture with integer pixel rectangles converted from UIA bounding rectangles at capture time, label = the control's visible text, target = AutomationId; the generator derives only crops and overlays (border, number, legend) and records them with hashes |
| SM-6 | **Determinism of figures:** fixed seed, window size, theme (dark default; light only where a chapter is about themes), 100 % display scale; the edition records the environment because fonts and scaling differ by machine. **Verify** which visible values change run to run with a fixed seed (timings, rates) and keep them out of the prose |
| SM-7 | **Risk list:** a machine-readable list derived from the `VV.Studio` risk table — item id, level (warning / caution), wording, chapters — kept beside the VV table (one source: either generate the VV table from it or check both agree). **Likely an author decision:** whether the illustrative risk table may serve as the manual's risk source |
| SM-8 | **Generator:** Python standard library, like `test_records.py` / `calibration_record.py`: reads registry, run records, static chapters, risk list; re-computes every hash; selects per chapter the latest run on the edition's binary; assembles Markdown (and HTML if wanted) with figures and omission notices; writes a generation record (case sources, omissions, derived figures, formal / draft and why); `--check` replays a committed edition byte for byte; `--self-test` seeds one defect at a time (missing run, failed chapter, hash mismatch, mark outside the image, warning wording edited in code, mixed binaries, locale mismatch) and requires each to refuse |
| SM-9 | **Where an edition lives (author decision):** (a) generated on demand, nothing committed; (b) a committed edition per milestone (Markdown + figures + generation record + reduced run records), `--check`-ed in CI like the test-record milestones; (c) published as a page. Recommendation to verify: (b), with the figure budget measured (PNG size per edition) |
| SM-10 | **Confidentiality and hygiene:** captures contain only the Studio window (no other apps, notifications, local paths, account names); the run checks the window title and visible text for local identifiers before saving; committed files scanned as in TODO-28 |
| SM-11 | **Docs:** the manual's own location and naming (`AGENTS.Conventions.Docs`), `AGENTS.UiAutomation` charter line (captures as document evidence, not verdicts), `AGENTS.md` / README pointer, `AGENTS.Rationale` rows — proposed by the implementer, applied by the planner |

## Decisions after review (2026-10-13, partial)

| # | Decision | Kind |
|---|---|---|
| MR1 | **Target full P4 compliance** with the author's UI-automation procedure v3.0.1 (manual result bundle: G0–G6, the closed list of human acts), after a feasibility check: no blocking item found on this machine; conditions are an elevated ETW tracing session for observed runs (Studio and the harness stay non-elevated), isolation by redirected paths (no disposable environment on this edition of Windows), and the effort (≈ 9–14 days headless, 2–3 days desktop). | author |
| MR2 | **Stage 0 inside TODO-43** brings the harness and `AGENTS.UiAutomation` to v3.0.1 for the scopes the manual uses (review §9: M01–M17, S01–S04, C01); untouched regression scopes go to a dated transition list. | author (review §9) |
| MR3 | **Probe first:** the first G1 qualification (launch–idle–exit observed run) is run as soon as the author can start the elevated tracing session; its result decides whether the rest of P4 proceeds or the scope is revisited. Until then only headless stage-0 work (no desktop, no elevation). | author |
| MR4 | **Raw traces stay local:** ETL files hold machine-wide activity and are never committed; the repository keeps only the filtered, Studio-attributed summary and hashes. | planner |
| MR6 | **Network channel: not applicable.** Studio has no network function (no client, server, socket or HTTP code); the
network channel is recorded as not applicable with that source evidence, not proven by a positive control. | author |
| MR7 | **Charter after the first runs:** the draft P4 charter (`PLAN.Docs.StudioManual.Charter.md`) is approved (HUM-04)
only after the author has seen the artefacts of the first observed runs, which will need several iterations; until
then it stays a draft and no result is promoted. | author |
| MR8 | **Proportionate observation (2026-10-14):** G1 is observed by a scoped file-system / HKCU before/after comparison with a
null window, plus the owned-process ledger, without elevation; a candidate is a state change, recorded background filters are
allowed (A3 revised). The machine-wide kernel trace becomes an optional diagnostic. Supersedes MR1's elevated-trace
condition; P4 compliance stays the target. Reason: runs 02–05 showed the exhaustive trace turns every runtime read into a
rule-and-approval cycle ("헌장을 고쳐. 지금 너무 보수적으로 행동하고 있어."). | author |
| MR9 | **Applicability first (2026-10-14):** for a viewer without write surfaces the procedure's scopes close as not applicable
with evidence instead of being built (charter, Applicability); G1 keeps one light observed run per trigger; the remaining work
is the manual itself (M15–M17, chapter registry). Reason: the stage-0 review applied all 22 items as if Studio had the
surfaces of a device-driving app (author: "표면이 없는 프로그램에 대해서는 Not Applicable하게 접근하는 게 맞지 않았을까?"). | author |
| MR10 | **Edition decisions (2026-10-14):** first edition = **8 core chapters** — 1.1 purpose and scope (static), 1.2 window and
workspaces, 2.1 sources, 2.2 optics and detector, 2.4 Start / Stop / Continue / Reset, 3.1 Imaging measurements, 3.4 Spectrum
(generated), 5.2 interpretation and limitations (static); the rest of the review §3 list follows in later editions. SM-9:
the edition is **committed** per milestone and replayed by hash in CI, within a figure budget (target ≤ 10 MiB per edition,
crops where a full window is not needed). Figures carry **rectangle marks with numbered badges** (digits from a small bitmap
glyph set, Python standard library) and a legend. SM-7: a **short Studio notice list** approved by the author is the only
source of warnings and cautions. | author |
| MR5 | Open: the P4 charter text (HUM-04, after the first manual runs, MR7). SM-7, SM-9, overlays and the first-edition scope are decided in MR10. | — |

## Steps

1. **Turn 1 — review only** (implementer, no desktop): read this plan and the model procedure; check every *verify* row in
   the code; propose the chapter list with the scenario that would generate each chapter and its checks; specify the
   run-record and figure-mark formats; measure what can be measured headless (record sizes, current capture code paths,
   UIA coverage of the controls named in the chapter list from the XAML AutomationIds); list author decisions (SM-7,
   SM-9 at least). Writes `docs/PLAN.Docs.StudioManual.Review.md`.
2. **Decisions after review** (planner + author).
3. **Turn 2 — implement headless parts:** registry, static chapters (drafts for the author), risk list, run-record and
   mark formats, generator with `--check` and `--self-test`, manual scenarios (compiled, not run), unit tests on
   fixtures; VV.Tests catalog entries.
4. **Turn 3 — desktop run (author's go):** the planner runs the manual scenarios in a detached worktree on a free
   desktop, then the generator; the planner opens the assembled edition and looks at every figure; break-verdict run
   (`GCAM_UI_BREAK_VERDICT=1`) shows that a failed check drops its chapter with a notice.
5. **Record and close:** first edition (if SM-9 commits one), docs updates, Todo row removed, plan archived.

## Done when

- The generator builds an edition from one set of manual runs on one binary, every generated chapter traceable through
  the generation record to a passed run's steps, checks and captures; static chapters and the risk list are registered
  sources.
- Self-test refusals all fire; `--check` replays a committed edition (if SM-9 commits one) byte for byte.
- The author has looked at the assembled edition, including every figure.

## Not here

- A manual for the hand-held product concept (theme 22) — design-only, no UI to drive.
- A Korean or second-locale edition (Studio is not localised).
- An STR of the verification procedure beyond what the TODO-28 test-record machinery already produces (verify whether
  it can carry it; otherwise a follow-up).
- Visual pass/fail verdicts on screenshots (charter); human review stays outside the tool.
