## Measurements (L-2, L-3)

### Conditions

Studio defaults through `SimulationService.BuildConfig`: rank 13 MURA 2×2 mosaic, 0.7 mm cell, D = 80 mm, 10 mm W
(0.178 /mm at 662 keV), 30 × 0.6 mm GAGG 10 mm, 0.15 mm entrance absorber, 2 mm backing, 0.1 mm gap, gain σ 0.03
(seed 1), default chain (window ±1.5 FWHM = ±45.3 keV around 661.7 keV). Source: Cs-137 500 µCi (17 038 500 photons/s
over its three lines) at (z·tan a, 0, z), z = distance from the detector. Data: `ListModeSource` → `MeasurementStage`,
events up to the live time. No background, dark counts or pile-up in the data (B is still profiled).

| Set | Points | Live | Data seeds | Search | Model |
|---|---|---|---|---|---|
| 60 s grid | z 300 / 500 / 700 / 1000 mm × 0 / 30 mrad | 60 s | 12 per point: 30 000 000 + 1000 z + 37 a + 7 T + 100 003 s, s = 0 … 11 | v3 | 0.9M, stream 1 |
| 15 mrad set | 500 / 1000 mm × 15 mrad | 60 s | 8 per point: base 9 100 000, same formula | v1 + the v3 sweep candidate | 0.9M, stream 1 |
| 10 s grid | as the 60 s grid | 10 s | 12 per point: base 20 000 000 | **v2** (v3 pass running) | 0.9M, stream 1 |
| Budget doubling | the 15 mrad set's data | 60 s | 8 per point | v1 | 0.9M / 1.8M / 3.6M (nested, stream 1) and 1.8M stream 2 |
| 300 mm budget | 16 of the 24 floods of the 60 s grid at 300 mm | 60 s | as above | refine from the v3 estimate | 3.6M |

The data seeds are disjoint from the model's counter-based streams, so every validation is held out from the model. No
truth enters the estimator, and the coarse depth grid's origin is offset per data set.

Columns: bias and s.e. over the fits that are not wrong maxima; σ_Hessian = median marginal σ_z from the response
surface; coverage counts every fit, wrong maxima included; mean Λ excludes wrong maxima. Wrong maximum = Λ > 25, Λ < −10
or a bearing more than 3 mrad from the truth (a third of a resolution element; the bearing precision is 0.1–0.5 mrad).
"Surface fallback" = the response surface was not negative definite and full-model Nelder–Mead was used; those fits
have no σ_Hessian.

### 60 s, search v3 (190 of 192 fits)

| live | channel | z mm | angle mrad | seeds | mean counts | bias ± s.e. mm | median error mm | sample SD mm | median σ_Hessian mm | SD / σ | wrong max | surface fallback | 68 % cover | 95 % cover | mean Λ |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 60 s | all | 300 | 0 | 12 | 46407 | -0.13 ± 0.20 | -0.08 | 0.71 | 0.56 | 1.26 | 0 | 0 | 6/12 | 9/12 | 1.50 |
| 60 s | all | 300 | 30 | 12 | 44208 | +0.64 ± 0.20 | +0.84 | 0.71 | 0.52 | 1.35 | 0 | 0 | 2/12 | 5/12 | 3.95 |
| 60 s | all | 500 | 0 | 12 | 17044 | -1.45 ± 0.97 | -1.59 | 3.36 | 2.88 | 1.17 | 0 | 0 | 6/12 | 11/12 | 1.67 |
| 60 s | all | 500 | 30 | 12 | 15507 | -0.76 ± 0.85 | -1.10 | 2.96 | 2.78 | 1.07 | 0 | 0 | 9/12 | 10/12 | 1.09 |
| 60 s | all | 700 | 0 | 12 | 8774 | +0.17 ± 1.41 | -0.03 | 4.88 | 5.67 | 0.86 | 0 | 0 | 7/12 | 12/12 | 0.81 |
| 60 s | all | 700 | 30 | 12 | 7815 | +1.27 ± 1.46 | +1.03 | 5.05 | 6.66 | 0.76 | 0 | 0 | 10/12 | 12/12 | 0.25 |
| 60 s | all | 1000 | 0 | 12 | 4395 | +9.87 ± 4.56 | +10.54 | 15.78 | 18.72 | 0.84 | 0 | 0 | 7/12 | 12/12 | 0.92 |
| 60 s | all | 1000 | 30 | 11 | 3805 | +11.15 ± 5.94 | +16.35 | 19.70 | 21.46 | 0.92 | 0 | 0 | 6/11 | 11/11 | 1.10 |
| 60 s | win | 300 | 0 | 12 | 14262 | -0.03 ± 0.31 | +0.18 | 1.07 | 1.20 | 0.89 | 0 | 0 | 9/12 | 12/12 | 0.09 |
| 60 s | win | 300 | 30 | 12 | 13666 | -0.03 ± 0.38 | -0.04 | 1.33 | 1.08 | 1.23 | 0 | 0 | 6/12 | 10/12 | 1.91 |
| 60 s | win | 500 | 0 | 12 | 5220 | -0.13 ± 1.92 | +1.04 | 6.64 | 5.77 | 1.15 | 0 | 0 | 6/12 | 10/12 | 1.72 |
| 60 s | win | 500 | 30 | 12 | 4833 | +0.27 ± 1.70 | +0.09 | 5.89 | 5.76 | 1.02 | 0 | 0 | 6/12 | 11/12 | 1.42 |
| 60 s | win | 700 | 0 | 12 | 2682 | -7.95 ± 3.60 | -9.01 | 12.49 | 12.78 | 0.98 | 0 | 0 | 6/12 | 11/12 | 1.49 |
| 60 s | win | 700 | 30 | 12 | 2445 | -1.82 ± 3.72 | -1.01 | 12.88 | 15.27 | 0.84 | 0 | 0 | 9/12 | 12/12 | 0.35 |
| 60 s | win | 1000 | 0 | 12 | 1340 | +5.79 ± 9.73 | +10.83 | 33.72 | 42.80 | 0.79 | 0 | 0 | 9/12 | 11/12 | 0.75 |
| 60 s | win | 1000 | 30 | 11 | 1191 | -22.35 ± 14.09 | -27.85 | 46.72 | 44.17 | 1.06 | 0 | 2 | 9/11 | 10/11 | 0.66 |

Pooled: 68 % 113 / 190 = 0.595, 95 % 169 / 190 = 0.889; without 300 mm all events 0.633 / 0.934 (166 fits, mean Λ
1.02). Missing: one flood at 1000 mm / 30 mrad (seed 31 201 536), whose v3 pass did not run (the resumed pass could not
open its output file). Its v2 result: all events 1019.7 mm, Λ 1.32; window 958.7 mm, Λ −1.07.

Studio's sharpest plane on the **same** floods (81 inverse-distance planes, prominence, K = 1), mean error / seed SD in
mm, against the likelihood:

| Channel | z | Angle | Sharpest plane | Likelihood |
|---|---:|---:|---:|---:|
| all | 300 | 0 | +50.7 / 0.0 | −0.13 / 0.71 |
| all | 300 | 30 | −19.1 / 8.1 | +0.64 / 0.71 |
| all | 500 | 0 | +69.3 / 0.0 | −1.45 / 3.36 |
| all | 500 | 30 | −40.3 / 9.3 | −0.76 / 2.96 |
| all | 700 | 0 | −69.7 / 150.1 | +0.17 / 4.88 |
| all | 700 | 30 | −130.7 / 0.0 | +1.27 / 5.05 |
| all | 1000 | 0 | +59.1 / 351.1 | +9.87 / 15.78 |
| all | 1000 | 30 | +18.8 / 299.4 | +11.15 / 19.70 |
| win | 300 | 0 | +11.0 / 18.8 | −0.03 / 1.07 |
| win | 300 | 30 | −26.0 / 10.0 | −0.03 / 1.33 |
| win | 500 | 0 | +22.0 / 58.5 | −0.13 / 6.64 |
| win | 500 | 30 | −36.3 / 11.8 | +0.27 / 5.89 |
| win | 700 | 0 | −29.6 / 58.5 | −7.95 / 12.49 |
| win | 700 | 30 | −107.5 / 57.4 | −1.82 / 12.88 |
| win | 1000 | 0 | +111.6 / 317.0 | +5.79 / 33.72 |
| win | 1000 | 30 | +17.7 / 138.2 | −22.35 / 46.72 |

### 60 s, 15 mrad (v1 search + the v3 sweep candidate)

| live | channel | z mm | angle mrad | seeds | mean counts | bias ± s.e. mm | median error mm | sample SD mm | median σ_Hessian mm | SD / σ | wrong max | surface fallback | 68 % cover | 95 % cover | mean Λ |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 60 s | all | 500 | 15 | 8 | 16770 | -0.74 ± 0.88 | -0.75 | 2.48 | 2.37 | 1.04 | 0 | 0 | 5/8 | 8/8 | 1.00 |
| 60 s | all | 1000 | 15 | 8 | 4137 | -3.36 ± 6.29 | +0.28 | 17.80 | 18.21 | 0.98 | 0 | 0 | 6/8 | 7/8 | 0.73 |
| 60 s | win | 500 | 15 | 8 | 5107 | -0.26 ± 1.07 | -0.51 | 3.03 | 5.25 | 0.58 | 0 | 0 | 6/8 | 8/8 | 0.63 |
| 60 s | win | 1000 | 15 | 8 | 1275 | -13.95 ± 12.26 | -10.21 | 34.67 | 37.88 | 0.92 | 0 | 0 | 7/8 | 7/8 | 1.24 |

The v1 search alone had one wrong maximum here (500 mm window, seed 10 100 990: 148.6 mm); the sweep candidate repaired it.

### 10 s, search v2 (complete; v3 not yet applied)

@@T10@@

Pooled: 68 % 129 / 192, 95 % 177 / 192 over all fits; over the 181 that are not wrong maxima 0.685 / 0.950 (mean Λ
0.94). Wrong maxima (11): 10 at 300 mm (7 window, 3 all events) and 1 at 500 mm window. Their bearings are 4–77 mrad off,
or the truth basin is 136–1586 log-likelihood units higher than the reported maximum. These are search failures of the
kind v3 repaired at 60 s. On the 78 10 s fits that the v1 search also completed, v1 had 12 wrong maxima and v2 had 4.

### 300 mm, 60 s, refitted at 3.6M histories (from the v3 estimates)

| live | channel | z mm | angle mrad | seeds | mean counts | bias ± s.e. mm | median error mm | sample SD mm | median σ_Hessian mm | SD / σ | wrong max | surface fallback | 68 % cover | 95 % cover | mean Λ |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 60 s | all | 300 | 0 | 9 | 46425 | +0.24 ± 0.27 | +0.21 | 0.81 | 0.55 | 1.47 | 0 | 0 | 5/9 | 8/9 | 1.93 |
| 60 s | all | 300 | 30 | 7 | 44330 | +0.12 ± 0.29 | +0.27 | 0.77 | 0.55 | 1.42 | 0 | 0 | 2/7 | 6/7 | 2.38 |
| 60 s | win | 300 | 0 | 9 | 14265 | +0.67 ± 0.41 | +1.13 | 1.23 | 1.19 | 1.04 | 0 | 0 | 5/9 | 9/9 | 1.34 |
| 60 s | win | 300 | 30 | 7 | 13689 | +0.20 ± 0.38 | +0.26 | 1.00 | 1.14 | 0.87 | 0 | 0 | 5/7 | 7/7 | 0.66 |

Paired with the same floods at 0.9M, five of the six all-events fits with Λ > 4 fall (7.20 → 2.05, 8.56 → 2.35,
6.49 → 2.64, 4.11 → 0.70, 5.67 → 3.43; one 5.87 → 6.34). At 3.6M, Λ agrees with (error / σ_Hessian)², the 30 mrad
bias falls from +0.64 to +0.12 mm, and the 95 % coverage rises from 14 / 24 to 14 / 16. The excess is the model
budget, not the likelihood. It is still above nominal at 3.6M (SD / σ 1.4–1.5, 68 % coverage 7 / 16), so ≈ 46 000
counts need more than 3.6M histories or a smoother model. The window channel (≈ 14 000 counts) is nominal already at
0.9M.

### Convergence (budget doubling, L-2)

Same 8 data floods per point (500 / 1000 mm, 15 mrad, 60 s), refit with nested budgets; differences to 3.6M (stream 1);
one v1 search failure excluded from the 500 mm window pairs:

| Point | Channel | Seed SD | 0.9M − 3.6M mean / SD | 1.8M − 3.6M mean / SD | 1.8M stream 2 − 3.6M mean / SD |
|---|---|---:|---:|---:|---:|
| 500 mm | all | 2.5 | −0.40 / 0.33 | −0.10 / 0.23 | +0.04 / 0.29 |
| 500 mm | win | 3.2–4.2 | +0.48 / 1.22 | +0.24 / 0.93 | −0.46 / 1.20 |
| 1000 mm | all | 17–18 | +0.34 / 3.36 | +0.18 / 2.14 | +0.59 / 3.71 |
| 1000 mm | win | 35–37 | −2.17 / 13.66 | +2.34 / 5.15 | −3.24 / 5.60 |

(mm.) Mean shifts from 0.9M are ≤ 0.16 seed SD, so the 0.9M estimates have stopped moving beyond the seed spread at
these counts. Per-fit jitter is ≤ 0.2 SD, except 0.4 SD for the 1000 mm window (≈ 1300 counts, flat likelihood). At
300 mm / 60 s (≈ 46 000 counts) 0.9M is not converged (above).

## Cost (L-4)

| Item | Measured |
|---|---|
| Model evaluation | 0.62 µs per history single-threaded (0.9M → 0.56 s), measured on the shared machine before the long runs |
| Template build | none: the model is evaluated on demand; the CRN arrays for 0.9M histories are ≈ 28 MB |
| One flood, both channels, v2 search + intervals | 218 M (1000 mm / 60 s) … 415 M (300 mm / 10 s) transported histories |
| v3's extra candidate | 81 Studio decodes + about 40 M coarse histories, plus one full refine (≈ 100 M) when it is distinct |
| Wall time observed | 330 … 4340 s per flood single-threaded under today's load (1.5–12.8 µs / history; other sessions saturated the CPU). Unloaded: ≈ 70–130 s per channel |

A Studio worker could run it as a cancellable background job after an acquisition. The history loop parallelises
trivially (one crystal instance per thread), so on 24 logical cores it is of order 10 s per channel — an **estimate,
not measured**. It is not a live 4 Hz update; Studio's sweep is 81 decodes.

## What could not be run / limits

- **10 s grid with search v3**: running (`v3 … main10v3`), not in the numbers; the 10 s wrong-maximum rate above is v2's.
- One 60 s flood (1000 mm / 30 mrad, seed 31 201 536) lacks its v3 pass (v2 result given above).
- 15 mrad only at 500 / 1000 mm, 60 s, 8 seeds, and with the v1 search plus the sweep candidate rather than v2 plus
  it; none at 300 / 700 mm or 10 s.
- Budget doubling only at 500 / 1000 mm, 15 mrad, 60 s (8 seeds), and a 3.6M refit of 16 floods at 300 mm. No 7.2M,
  none at 700 mm or 10 s (lower counts). Model-stream swap only at 1.8M.
- 12 seeds per point: coverage per point is ±1–2 fits; the pooled coverage (166–190 fits) is the reliable figure.
- Coverage is counted from Λ at the truth (equivalent to "truth inside the LR interval" while the profile is monotone);
  interval end points were not root-found; widths are Hessian σ.
- Simulation fitted with itself. Not measured: D, mask pose, gain-map or crystal-model errors; y / diagonal bearings;
  background in the data; several sources; a real detector. The D sensitivity in the result section is derived, not
  measured.
- Unit tests not run: no repository code changed. No desktop, Studio launch or UI tests.
- Process state: two of my runs are still running (APPROVAL REQUESTS). The v1 10 s grid was lowered to Idle priority
  (reversible) so it only uses spare cycles; it is used here only for the v1 / v2 comparison.

## Reproduction

Harness: `%TEMP%\gcam-depthlik-20261002`.

| File | Contents |
|---|---|
| `Probe.csproj` | references `src/Gcam.Studio.Services` of this worktree |
| `Model.cs` | constants from `BuildConfig`, acquisition, CRN forward model, analytic window |
| `Lik.cs` | profiled Poisson likelihood |
| `Fit.cs` | coarse scan, search, response-surface refine, matched profiles, CSV row |
| `Rsm.cs`, `V3.cs`, `Refit.cs`, `Interp.cs`, `Rough.cs` | response surface; sweep-seeded candidate pass; budget refit; interpolation; roughness |
| `Program.cs` | modes |
| `v1_search_block.txt` | the v1 search, to rebuild the v1 runs |
| `tables.py`, `conv.py`, `rough.py`, `summ.py` | summaries |

| Output folder | Contents |
|---|---|
| `out4` | roughness |
| `out5` | interpolation |
| `out8` | v1 build: `fit_conv_*`, `fit_main10` |
| `out9` | v2 build: `fit_main60v2`, `fit_main10v2` |
| `out12` | current build: `fit_main60v3`, `fit_main10v3` (in progress), `fit_conv15v3`, `fit_r300_n36`, `cost.txt` |
| `out6`, `out7`, `out11` | superseded smoke runs (Nelder–Mead refine, mixed-surface Λ); no result here uses them |

```powershell
$d = Join-Path $env:TEMP 'gcam-depthlik-20261002'; cd $d
dotnet build Probe.csproj -c Release -o outR
dotnet outR\Probe.dll maskcheck                       # mask replica vs engine
dotnet outR\Probe.dll validate 1800000                # model vs long acquisitions (chi2)
dotnet outR\Probe.dll scan 180000 600                 # LL along depth at the true bearing
dotnet outR\Probe.dll interp 1800000 > interp.csv     # node-interpolation cost (noise-free)
dotnet outR\Probe.dll rough 3600000                   # roughness vs budget
# fit <N> <nCoarse> <kCoarse> <seeds> <seedBase> <z;..> <a;..> <T;..> <tag> [modelStream] [bg] [threads] [firstSeed]
dotnet outR\Probe.dll fit 900000 90000 140 12 30000000 "300;500;700;1000" "0;30" 60 main60v2 1 1 8
dotnet outR\Probe.dll v3 900000 90000 140 outR\fit_main60v2.csv main60v3 8
dotnet outR\Probe.dll fit 900000 90000 140 12 20000000 "300;500;700;1000" "0;30" 10 main10v2 1 1 8
dotnet outR\Probe.dll v3 900000 90000 140 outR\fit_main10v2.csv main10v3 10
dotnet outR\Probe.dll refit 3600000 1 outR\fit_main60v3.csv 300 r300_n36 8
dotnet outR\Probe.dll cost 900000
# budget doubling / 15 mrad: v1 build (v1_search_block.txt pasted into Fit.cs), then
#   fit {900000|1800000|3600000} 90000 140 8 9100000 "500;1000" 15 60 conv_n{09|18|36}_s1 1 1 6
#   fit 1800000 90000 140 8 9100000 "500;1000" 15 60 conv_n18_s2 2 1 6 ; v3 900000 90000 140 fit_conv_n09_s1.csv conv15v3 6
python tables.py outR\fit_main60v3.csv
```

The v2 grids were built before the `Finish` refactor and the history counter, which only reformat output. Results are
deterministic per data seed, model stream and budget. The 300 mm refit used a 16-flood snapshot of the 60 s grid.

## APPROVAL REQUESTS

None needed for the results above. Two of my processes are still running, writing only under `%TEMP%`:

| PID | Run | Suggestion |
|---|---|---|
| 63228 | `v3 … v2_main10.csv main10v3 10` — the 10 s grid with search v3 (gate item 1) | let it finish (≈ 1–2 h at today's load); or `Stop-Process -Id 63228`, which loses only unfinished rows (finished rows stay; the pass resumes from them) |
| 50696 | `fit … main10 …` — v1 10 s grid, Idle priority, 78 / 192 fits | `Stop-Process -Id 50696`: v1 is superseded; loses only unfinished v1 rows; undo = rerun the v1 build |
