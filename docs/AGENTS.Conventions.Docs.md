# AGENTS.Conventions.Docs — how documentation is named, placed and kept in sync

Scope: every Markdown file in this repository.

## File naming: `KEYWORD.Section[.Sub].md`

A document's name says **what kind of document it is** (the upper-case keyword) and **what it covers**
(PascalCase sections, separated by `.`, most general first). Names sort into groups in any file browser.

| Keyword | Kind | Audience | Examples |
|---|---|---|---|
| `AGENTS` | how to work here: rules, conventions, logs, backlog | contributors and coding agents | `AGENTS.md`, `AGENTS.Studio.md`, `AGENTS.Conventions.Code.md`, `AGENTS.Findings.md`, `AGENTS.Todo.md` |
| `DESIGN` | how GCAM Studio is designed: architecture, UI system, components | anyone changing Studio | `DESIGN.Architecture.md`, `DESIGN.Typography.md`, `DESIGN.Controls.md` |
| `VV` | verification and validation: user / product / software requirements, the evidence behind them, design record, traceability to tests, validation scenarios, known anomalies | reviewers, anyone changing behaviour | `VV.Gcam.Overview.md`, `VV.Gcam.URS.md`, `VV.Gcam.PRS.md`, `VV.Gcam.Evidence.md`, `VV.Gcam.Limitations.md`, `VV.Gcam.Decisions.md`, `VV.Gcam.Calibration.md`, `VV.Studio.SRS.md`, `VV.Studio.SDS.md`, `VV.Studio.md` |
| `PAPER` | write-ups of the physics / results for readers | readers | `PAPER.ko.md` |
| `PLAN` | implementation plans: goal, the author's decisions with reasons, step-by-step spec and done-criteria, written before the work and handed to whoever implements it (a person or a coding agent) | implementers, reviewers | `PLAN.Physics.RigReadout.md` (open); finished ones in `docs/archive/` |

Rules:

- Keyword is UPPER CASE; every following section is **PascalCase** (`AGENTS.Findings`, not `AGENTS.findings`).
- Go one level deeper only when a section needs siblings (`AGENTS.Conventions.Code` + `AGENTS.Conventions.Docs`).
- An optional **language tag** comes last, lower-case ISO 639-1: `PAPER.ko.md`. No tag = English.
- A new keyword needs a row in the table above first. Don't invent one for a single file.

## Placement

| File | Where | Why |
|---|---|---|
| `README.md`, `AGENTS.md`, `CLAUDE.md`, `LICENSE` | repository root | names and locations fixed by GitHub / agent tooling |
| every other keyword document | `docs/` | one flat folder, grouped by name |
| `README.md` inside a source folder | next to the code (`src/Gcam.Studio/README.md`, `rtl/README.md`) | a short map of that folder that links to the keyword docs; never the main explanation |

`AGENTS.md` is the index for agents and contributors: it links every `AGENTS.*` and `DESIGN.*` document.
`DESIGN.*` currently covers GCAM Studio only (the engine's design lives in `AGENTS.md`).

## Document structure

1. **Title line** = the file's name without `.md`, an em dash, and a one-line purpose:
   `# DESIGN.Typography — fonts, type scale and text roles`.
2. **Scope line** right below it (`Scope: …`) — what the document covers and what it doesn't.
3. Body: tables for anything enumerable (keys, files, rules), prose for reasons. Say *why* a rule exists.
4. Diagrams as Mermaid code blocks (rendered by GitHub), small layouts as ASCII.
5. Links are **relative** (`[DESIGN.Color](DESIGN.Color.md#theme-switching)`), never absolute paths or URLs to
   this repo.

## Language and tone

- Public docs are in **English**; a translated copy carries a language tag.
- Plain, specific sentences. Numbers with units. Report what was measured, including when an effect turned out
  small — don't inflate.
- No machine-specific paths, personal emails, or anything from an employer.

## Keeping docs in sync

- A change that alters behaviour, structure or a name **updates the affected doc in the same commit**.
- Results go to `AGENTS.Findings` (by theme: index in `AGENTS.Findings.md`, a new theme appended to `AGENTS.Findings.Part2.md` with its index row), deferred or undecided work to `AGENTS.Backlog.md`, ready-to-start or running tasks to `AGENTS.Todo.md`, closed tasks to the index in `docs/archive/README.md`.
- **`AGENTS.Todo.md` stays a short list; a task's plan lives in a `PLAN.*` document** (one per task or task group)
  that the task's row links. A plan opens with a `Status:` line below its scope line; when the task is done the plan
  stays as the design record (status set to *done*, with the commit) and **moves with its review to `docs/archive/`
  in the closing commit** (links updated), so `docs/` lists only open plans; the closed-task index in `docs/archive/README.md` links it.
  Plans may link `AGENTS.*`; `VV.*` documents do not link plans (they are self-contained). The implementer's review of a
  plan is `PLAN.<Area>.<Name>.Review.md`; the procedure (reference plan → measured review → decisions → implementation)
  is [AGENTS.Planning](AGENTS.Planning.md).
- Counts that drift (test totals, command counts) are stated in one place and linked from elsewhere, or
  updated everywhere in the same commit.
- Renaming a document: `git mv` it, then search the repository for the old name and fix every reference.
- **`VV.*` documents are self-contained** (decision D-35). They link only to other `VV.*` documents, `DESIGN.*`
  pages, `PAPER.*`, the `README.md` files and the code, tests and outputs — never to `AGENTS.*` or `CLAUDE.md`, and
  they cite results by evidence ID (`EV-nn`), never by Findings theme number. A result a VV row rests on gets its
  entry in `VV.Gcam.Evidence.md` (result, conditions, limits, reproduce command, tests) in the same commit as the
  Findings theme; a changed result updates the entry and every row in its "Used by" column. The theme ↔ EV map
  is at the top of `AGENTS.Findings.md`.
- Every `VV.*` document opens with an **At a glance** list (≤ 6 bullets) below its scope line, and keeps change
  history out of its requirement rows (that belongs in the evidence register's model history or in the decision
  log).
