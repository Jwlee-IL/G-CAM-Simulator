# PLAN.Studio.WpfRemoval — delete the legacy Gcam.Wpf viewer (TODO-12)

Scope: remove `src/Gcam.Wpf` now that GCAM Studio has every feature the author kept. Procedure:
[AGENTS.Planning](AGENTS.Planning.md); migration: [PLAN.Studio.Migration](PLAN.Studio.Migration.md).

Status: **done** 2026-10-02 by a substitute Claude subagent (Codex out of credits): only the solution entry and a CI comment
depended on it; `src/Gcam.Wpf` deleted (last present at `85b2ed1`); current-state docs updated, history kept. Planner
re-verified: build 0 / 0, tests 284 / 179 / 81 (+7) / 13 (+14).

**Order changed by the author (2026-10-02):** TODO-12 runs before TODO-19. Wpf's SiPM block average (`BlockifyFlood`)
was not carried into Studio (it is not a light-sharing model, Findings 35 correction) and remains in git history;
TODO-19 builds the readout model from its own plan.

## What exists (checked, 2026-10-02)

| Item | Fact |
|---|---|
| Solution | `Gcam.sln` line 24 holds `Gcam.Wpf` |
| CI | `.github/workflows/ci.yml` comment names `Gcam.Wpf` among the windows targets |
| Code references | none expected outside `src/Gcam.Wpf` — *verify* (grep, build) |
| Docs | about 25 Markdown files mention it: current-state statements must change; dated history (Findings themes, Backlog done entries, PLAN records, VV history rows) stays as history |

## Proposal

| # | Proposal | Status |
|---|---|---|
| W-1 | Delete `src/Gcam.Wpf` and its solution entry; adjust the CI comment; nothing else in code | proposed — *verify* no project / test / script references it |
| W-2 | Current-state docs (AGENTS.md layout table and text, CLAUDE.md lines about the WPF viewer, README "Where things live", AGENTS.Studio, VV.Studio / SDS where they describe the legacy viewer as present) say it was removed in TODO-12 and is in git history at the last commit that had it | proposed |
| W-3 | History stays: Findings theme 34 and other dated records keep their text; add one dated note where a reader would otherwise look for the code | proposed |
| W-4 | Verify: build, all normal tests, render opt-in, no broken relative links to `src/Gcam.Wpf` in docs | proposed |

**Not here:** TODO-19.
