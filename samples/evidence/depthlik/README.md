# TODO-25 joint depth likelihood — research probe and raw results

Raw research material for [PLAN.Physics.DepthLikelihood](../../../docs/archive/PLAN.Physics.DepthLikelihood.md) and its
review (Findings 62), moved here from a local temporary folder on 2026-10-03 so the work can continue on another
computer. **Not reviewed evidence**: the numbers here have not yet been folded into the review or Findings.

- `probe/` — the headless C# probe (`Probe.csproj`, not part of `Gcam.sln`). It was built and run against engine commit
  `85ff1ec` (a worktree), i.e. **before** the xoshiro256** generator replacement (`e0c36a1`, TODO-26); re-run on the
  current tree before quoting anything. Build: `dotnet build samples/evidence/depthlik/probe -c Release`.
- `measurement-notes.md` — the running measurement log written during the review.
- `results/` — CSV outputs of the last batch: `fit_main10v3.csv` (v3 search, 10 s live time — the run that was still
  going when the review was written; it finished afterwards and is **not yet analysed**), `fit_main60v3.csv` (60 s),
  `fit_conv15v3.csv`, `fit_r300_n36.csv`, `v1_conv15.csv`, `v2_main10.csv`, `v2_main60*.csv`, `v3_300.csv`, `cost.txt`
  and the run logs. `log_main60v3b.txt` is a duplicate run that failed on a file lock; ignore it.

Status (2026-10-08, TODO-25 closed): the 10 s v3 pass was analysed as a historical observation; the current tree was
re-measured by the `depthlik-screen` family (`manifest.json`, `seeds.json`, `recipe.py`; summary
`results/screen-v1-summary.md`, analyser `analyze.py`). Gate validation is parked in `docs/AGENTS.Backlog.md`.
