# TODO-30 baseline — turn 7 (spectrum re-issue, AB-7 gate study, AB-9 count gates)

2026-10-04. **This turn was done by a substitute Claude implementer (Claude subagent), not by Codex.** Specification:
`docs/PLAN.Physics.AmbientBackground.md` (AB-7, AB-9, AB-10) and review §7. No docs, PLAN, Todo, VV, README, PAPER or
Findings edits; no Studio default change (AB-5); no git state change. Machine-readable: `ambient-baseline-v1-turn7.json`
(summary), `ambient-baseline-v1-turn7-gate.json` (every null configuration, every source condition, count gates,
derived ratios), `ambient-baseline-v1-turn7-sources.csv` (source conditions, one row each).

## 1. Spectrum re-issue (AB-10) and the placeholder hash

- `samples/ambient/terrestrial-unscear2000-v1.json` (+ `.sha256`, file SHA-256 `4d48bc42…35b3f4`, content hash
  `7575df5c…c0bee`), issued by the new CLI command `ambient-issue` from the committed request
  `samples/ambient/terrestrial-unscear2000-v1-issue.json`. Lines, continuum, angular table and NotIncluded are copied
  unchanged (test: identical serialisation); changed are only `Id` (no NOT-VALIDATED), `IsValidated = true`, the status
  sentence of `Reference`, and a new hashed member `Validation`: decision AB-10, kind "model comparison, not a
  statistical test", band ±0.03, the three ratios with their MC SE **read from the hash-pinned turn-6 results file, not
  typed** (K-40 1.01388 ± 0.00032, U 0.99740 ± 0.00032, Th 1.02268 ± 0.00026), and the superseded file's id, content
  hash and file hash. The NOT-VALIDATED file is kept unchanged.
- Engine: `RequireValidatedSpectrum` now also requires a self-consistent acceptance record (decision named, ratios
  present, each |ratio − 1| ≤ its band). The validated file is accepted; the NOT-VALIDATED one, a record with a ratio
  outside its band, and a stripped record are rejected (tested). Seed driver: a request that names its spectrum by file
  is checked against the pinned SHA-256 and refused unless `IsValidated` and no NOT-VALIDATED id (checked by hand: the
  validated file accepted; the NOT-VALIDATED file and a wrong pin refused).
- New: spectrum by reference — `Ambient.SpectrumFile` + `SpectrumFileSha256`, resolved and hash-checked by
  `ConfigLoader.Load`; an unresolved reference is rejected by the engine (so the placeholder default can never stand in).
- **Placeholder hash, cause and fix.** The turn-4 sidecar `3600e22b…` is the SHA-256 of the file with **CRLF** line
  ends (reproduced: LF→CRLF of the committed bytes hashes to exactly `3600e22b…`). `WritePlaceholder` serialised with
  `WriteIndented` on Windows (Environment.NewLine = CRLF), hashed those bytes, and git stored LF (`*.json text eol=lf`).
  Fix (**hash the bytes git stores**): the sidecar is now `10dccd59…8222b`; all spectrum writers go through
  `IncidentSpectrumFile` (LF only, UTF-8 without BOM, refuses CR and refuses to overwrite a pinned file); the placeholder
  regenerates byte for byte. Test `PinnedAmbientFiles_HashTheBytesGitStores`: every `samples/ambient/*.json` with a
  sidecar must contain no CR byte and match its sidecar — it fails on the old sidecar, and would fail in the writer's
  own CRLF working copy (the CR check). Turn 6's statement that a "3600e22b test still passes" was not right: no test
  checked that hash.

## 2. Gate study (AB-7) — recipe

- **Acquisition model:** fixed live time, flood map of exact Poisson counts per pixel (`Sampling.PoissonExact`, no
  Gaussian branch) with mean (activity × source map + H*(10) × ambient map) × exposure. Maps from `GateResponse`:
  ambient = the engine's `AmbientPhotonProcess` (validated spectrum required) tallied per pixel and window (5 × 10⁵
  incident photons per map per seed); source = the scenario's biased point source through its mask into the **same**
  crystal model (10⁶ photons per map per seed). Each outer seed transports its own maps.
- **Windows:** `open` (every deposit) and `cs662` (661.7 keV ± 10 %, the engine's Compton-study window; pulse height
  smeared with the scenario's `EnergyResolutionFwhm` ∝ √E: lab 0 = exact, hand-held 7 %). Acceptance is the window
  probability (Rao-Blackwellised, no smearing noise).
- **Cases:** `lab` = `scenario.json`, source plane 160 mm, centre and (8, 0) mm; `head` = `scenario_handheld.json` at
  155 mm, centre and (8, 0) mm; `head1m` / `head5m` = hand-held at D+S = 1 / 5 m, centre and 3.0° off axis. Bounds
  BareCrystalAllFaces / FrontOnlyThroughMask; fields 0.05 / 0.10 / 0.20 µSv/h; exposures 10 s and 60 s. Source levels:
  expected net counts S = 25, 50, 100, 250, 500, 1000 (activity = S / (exposure × pilot rate per Bq; pilot seed 777,
  rates in the request)) plus the default 1 MBq. Ideal (no field) runs on the same seed and source map (paired).
- **Search statistic (review §7, correlation-based alternative):** the scenario's own decoder as a matrix G (read off
  the decoder by decoding one-count images; reproduces `Decode` exactly — tested). With background shape p (an
  independent MC estimate per seed: the instrument's transported background model) and the acquisition total N as the
  only nuisance (background normalisation), a background-only acquisition is multinomial, so
  Z(θ) = (recon(θ) − N Σ G p) / √(N (Σ G² p − (Σ G p)²)) has mean 0, variance 1 at each θ (tested, k·σ). The
  statistic is max Z over the whole decoder grid (49 × 49); its location is the grid point. **Not a Gaussian
  significance** — calibrated empirically.
- **Threshold, selection seeds only:** F128[1:65] (64 seeds, disjoint from O128; 12345 skipped), 64 null acquisitions
  per seed = 4096 per configuration. Rule declared in `select_thresholds.py` before validation: T* = the (⌊0.003 n⌋+1)-th
  largest null Z (≤ 12 selection exceedances). Universal threshold = the largest T* = **4.643**. Thresholds pinned in
  `gate-thresholds-v1.json` (SHA-256 `83b931ac…78105`) before the validation runs.
- **Validation seeds:** O128[0:128] (N = 128 as family `noise`), 16 nulls per seed = **2048 per configuration** and
  300 source acquisitions per condition per seed. Trusted = Z > T*; correct = trusted and within one angular resolution
  element atan(cell / D) (lab 0.955°, hand-held 1.042°) of the truth. Pass = one-sided 95 % Clopper–Pearson upper limit
  of the false-trusted rate ≤ 1 %.

## 3. Results — false trusted locations on background-only acquisitions

96 configurations (4 cases × 2 exposures × 3 fields × 2 bounds × 2 windows), 2048 validation nulls each.

| Threshold | Pooled false-trusted | Configurations passing (upper 95 % ≤ 1 %) | Worst configuration |
|---|---:|---:|---|
| per configuration (α = 0.003) | 603 / 196 608 = 0.307 % | **93 / 96** | 13 / 2048 = 0.63 %, upper **1.007 %** (three configurations) |
| universal T* = 4.643 | max 8 / 2048 | **96 / 96** | upper 0.70 % |

- **Finding (target not met as specified for the per-configuration rule):** three configurations miss the ≤ 1 % upper
  limit by 0.007 points (13 / 2048 each): `head1m|t=60|F=0.05|Front|cs662`, `head5m|t=10|F=0.05|Front|open`,
  `head5m|t=10|F=0.1|Front|cs662` — all front-only bound with **< 1 expected background count** (0.10–0.64), where Z
  takes few discrete values and ties make the selected quantile coarse. Not tuned away; options for the planner:
  more selection nulls, a smaller α, or the universal threshold (which passes all 96 but costs power at low background).
- Calibration holds overall: pooled validation rate 0.307 % against the selection's 0.3 %. Dispersion index of per-seed
  counts 0.91–1.82 (binomial ≈ 1; 128 seeds with mean ≤ 0.1 make the index noisy — no strong clustering seen).
- Thresholds depend on the background level: T* rises from ~1.2–2 (front, cs662, < 1 count) to 4.5–4.6 (bare, open,
  10²–10³ counts); see `gate-thresholds-v1.json`.

## 4. Results — count / significance gates under background (EV-07, EV-09; N = 128 seeds, 300 acquisitions each)

Ideal reference **in this pipeline** (same crystal model and seeds, no field), lab centre, open window — compare
EV-07's legacy ideal (conventional detector): collapse below ~25 (fail 38 ± 3 % at 25), sub-mm from ~250 (0.44 mm):

| S (net counts) | 25 | 50 | 100 | 250 | 500 |
|---|---|---|---|---|---|
| ideal, RMS mm (mean ± SD over seeds) | 5.73 ± 0.22 | 3.35 ± 0.26 | 1.14 ± 0.22 | 0.42 ± 0.01 | 0.37 ± 0.01 |
| ideal, failure > 3 mm | 44 % | 14 % | 1.2 % | 0 | 0 |
| ideal, sub-mm seeds | 0 / 128 | 0 / 128 | 35 / 128 | 128 / 128 | 128 / 128 |

Lab centre, 60 s, **bare bound, open window** (B = expected ambient counts per acquisition):

| Field | B | decoder RMS mm at S = 250 / 500 / 1000 | trusted & correct at S = 250 / 500 / 1000 | S for ≥ 95 % trusted & correct |
|---|---:|---|---|---:|
| 0.05 µSv/h | 488 | 9.07 / 4.85 / 0.50 | 0.753 / 1.000 / 1.000 | 500 |
| 0.10 µSv/h | 977 | 9.13 / 9.01 / 3.21 | 0.378 / 0.997 / 1.000 | 500 |
| 0.20 µSv/h | 1954 | 9.05 / 9.02 / 8.94 | 0.097 / 0.889 / 1.000 | 1000 |

Lab centre, 60 s, **front-only bound** (B = 1.7 / 3.4 / 6.9): practically the ideal curve (0.10 µSv/h: RMS 1.22 mm at
100, 0.42 at 250; trusted & correct 0.990 at 100). **cs662 window**, both bounds: B ≤ 20 counts at 60 s;
decoder ≥ 95 % within resolution from S = 100 (as ideal), sub-mm in 128 / 128 seeds from S = 250 (as ideal);
trusted & correct ≥ 95 % from S = 100–250.

Hand-held centre (`head`, EV-09 analogue), 60 s, ideal: RMS 1.02 ± 0.24 / 0.27 ± 0.15 / 0.20 mm at S = 250 / 500 /
1000, sub-mm 58 / 128 seeds at 250 and 128 / 128 at 500 (legacy EV-09: sub-mm median at ≥ 250 — a different detector
pipeline and criterion). Bare, open, 0.10 µSv/h (B = 2011): decoder RMS ~9 mm up to S = 1000; trusted & correct
0.46 at 500, 0.998 at 1000. cs662, bare, 0.10 (B = 25.7): RMS 1.95 / 0.44 mm at 250 / 500; trusted & correct 0.980 at
250. At 1 MBq (default): centre sources stay at their ideal floor in every ambient condition (lab 0.35–0.48 mm,
hand-held 0.20–0.22 mm); edge sources stay at it except the **hand-held edge, bare, open window**: decoder RMS
1.28 / 3.37 / 9.47 mm at 0.05 / 0.10 / 0.20 µSv/h in 10 s (S = 1734, B = 168–670) and 9.33 mm at 0.20 in 60 s
(S = 10 407, B = 4022; ideal 0.70–0.85 mm) — while the search statistic is trusted and correct in 100 % of them.

**Count gates read off the declared grid** (smallest S with pooled trusted-and-correct ≥ 0.95; ideal = decoder ≥ 95 %
within one resolution element). Full table: `CountGates` in the gate JSON.

| Case / window | ideal | front-only, any field | bare 0.05 / 0.10 / 0.20, 10 s | bare 0.05 / 0.10 / 0.20, 60 s |
|---|---|---|---|---|
| lab centre, open | 100 | 100 | 250 / 250 / 500 | 500 / 500 / 1000 |
| lab centre, cs662 | 100 | 100 | 100 / 100 / 100 | 100 / 250 / 250 |
| head centre, open | 250 | 250 | 500 / 500 / 1000 | 1000 / 1000 / > 1000 |
| head centre, cs662 | 250 | 250 | 250 / 250 / 250 | 250 / 250 / 500 |
| head 1 m centre, open | 250 | 250 | 250 / 500 / 500 | 500 / 1000 / 1000 |
| head 5 m centre, open | 100 | 100 | 250 / 250 / 250 | 500 / 500 / 1000 |

**Findings.**
1. **Under background the gate is a significance gate, not a count gate:** with the bare bound in the open window the
   S needed for 95 % trusted-and-correct grows with B roughly as √B (lab: 250 → 500 → 1000 for B ≈ 160 → 490–980 →
   1950), i.e. a gate on net counts alone does not hold. In the cs662 window B stays ≤ 51 counts and the gate moves at
   most one grid level from ideal. Front-only: unchanged from ideal at every tested level.
2. **The raw decoder estimate is biased by the bare-bound background, the search statistic is not:** the bare
   all-face background is not flat (edge pixels see the side faces); the cyclic decoder's argmax drifts to ~9 mm
   (≈ 3.3°) even at S = 1000 with B = 1954, while the background-studentised search finds the source. A camera that
   reports the raw decoder answer under such a background needs background-shape subtraction.
3. **Detection alone does not assure quality:** at very low background (front-only, cs662) almost every acquisition
   with counts is "trusted" (T* ≈ 1.2–2), but correct-among-trusted is only 0.5–0.6 at S = 25 — the localisation-quality
   criterion (≥ 95 % within one element) is what sets the gate there (S = 100 lab, 250 hand-held).
4. The 1 m, 3° off-axis source does not reach 95 % even ideal at S = 1000 in cs662 (open: decoder 96.5 % at 1000),
   so that case is limited by the optics, not the background.

## 5. Absolute ambient-to-source ratios (derived from the measured rates; EV-12 / EV-02 translation)

Ambient detected rate per µSv/h (mean over 128 seeds; seed SD in the JSON): lab bare 162.8 (open) / 1.66 (cs662) cps,
front 0.572 / 0.0447 cps; hand-held bare 335.2 / 4.28 cps, front 1.283 / 0.104 cps. Source at 1 MBq: lab 80.2 / 26.0
cps, hand-held 155 mm 195.2 / 75.1, 1 m 4.76 / 1.88, 5 m 0.181 / 0.0721 cps. Ambient ÷ source at 1 MBq and 0.10 µSv/h:

| | lab 160 mm | hand-held 155 mm | 1 m | 5 m |
|---|---:|---:|---:|---:|
| bare, open | 0.203 | 0.172 | 7.04 | 185 |
| bare, cs662 | 0.0064 | 0.0057 | 0.228 | 5.93 |
| front, open | 0.0007 | 0.0007 | 0.027 | 0.71 |
| front, cs662 | 0.0002 | 0.0001 | 0.0056 | 0.145 |

(Scales linearly with the field: × 0.5 at 0.05, × 2 at 0.20 µSv/h.) EV-02's "BSR 1" pedestal at 1 m (N0 = 500 on axis)
corresponds to B / N0 = 0.67 (10 s) or 4.0 (60 s) for the bare bound, 0.003 / 0.015 for front-only, at 0.10 µSv/h, open.

## 6. EV families (AB-9)

| Family | Status this turn |
|---|---|
| EV-07 / PR-SENS-02 | **measured** (§3–4; validation family `gate_validation`, N = 128). Ideal value in this pipeline next to each ambient value; the legacy EV-07 numbers stay the best-case bound of the conventional detector |
| EV-09 | **measured** for precision vs counts (case `head`, N = 128, §4). Efficiency unchanged (source transport does not depend on an independent field); quoted source rate 195.2 cps/MBq in this crystal model |
| EV-12 | **partly**: which BSR an absolute field gives (§5, derived from measured rates, not a re-measurement); the background-sweep curves stay valid as relative stress tests. **Pending:** mask / antimask under the absolute field — needs ambient maps through the inverted mask per bound (front bound differs; bare bound nearly not) and the calibrated-subtraction comparison at equal time; estimate ~15 min of runs at N = 128 after ~1 h of code |
| EV-02 | **pending**: needs an angle sweep 0–20° at 1 / 5 m, cyclic and non-cyclic decoding, and the centroid cue re-calibrated on separate seeds — new recipe (the gate study has centre + 3° only). Translation of its BSR 1 column in §5. Estimate: 82 positions × 2 decoders per seed with the existing machinery ≈ 4 min/seed → 64 seeds ≈ 30 min wall at 10 jobs, plus ~2 h of code for the centroid cue |
| EV-15 | **pending**: stripping needs Co-60 + Cs-137 maps through this pipeline, R calibrated with background present, and a stated exposure (the cited 3-count example has none). Input measured here: ambient in the cs662 window 1.66 (lab) / 4.28 (hand-held) cps per µSv/h bare, 0.045 / 0.104 front. Estimate ~1 h code, ~20 min runs |
| EV-01 | **pending / partly unchanged**: the sweep is a noiseless mean-map recipe (10⁶ photons per point) with no exposure; an absolute field needs an activity and exposure per grid position (a recipe decision). At 1 MBq the high-count floor moves little at the lab centre (ideal 0.35–0.45 → 0.37–0.48 mm, bare) but the raw decoder at the hand-held edge in the bare bound, open window, is pulled to ~9 mm at 0.20 µSv/h (§4) |

## 7. Budget

Pilot 35 s; selection 64 seeds ≈ 7.5 min wall (10 jobs, ~65 s/seed); validation 128 seeds ≈ **54 min** wall (10 jobs,
mean 247 s/seed). Total ≈ 63 min of the ~3 h allowance. N and null counts were not reduced. Remaining plan: EV-12
antimask (~15 min runs), EV-15 (~20 min), EV-02 (~30 min), EV-01 recipe after an author decision on exposure.
The sampler of `AmbientAngularSampler` scans its 16 292-row table linearly per photon (≈ 10 µs/photon, 90 % of the
map time); a cumulative-sum binary search would cut map time ~10× but changes floating-point bin choice at boundaries
(not done; left for the planner).

## 8. Deviations and limits

1. Source and ambient both use the ambient bound's homogeneous crystal (no gaps / entrance / backing) — the legacy
   EV-07/09 used the conventional detector; hence the pipeline's own ideal reference.
2. Per-acquisition flood maps from expected maps, not event-by-event transport (the AB-1 flood route; map MC noise
   ≤ ~1 % of the acquisition's Poisson variance at the largest backgrounds, by events per pixel).
3. The search location is a grid point (no sub-cell); the decoder estimate keeps tent interpolation.
4. Activities at 5 m reach 10⁸ Bq for S = 1000 (declared S-level design, not a field claim).
5. Gross-failure (> 3 mm at the source plane) is meaningful only for lab / 155 mm; angular errors are in the JSON.
6. Studio startup scenario (rank 13, 30 × 30) not run — "Studio / hand-held" read as the hand-held head.
7. AB-3 high-energy limits apply (K-40 / Tl-208 lines in the open window; tungsten μ clamped, no pair production).

## 9. Tests and build

`dotnet build Gcam.sln -c Release`: 0 errors, 2 warnings (pre-existing xUnit2012 / xUnit2000). `dotnet test Gcam.sln
-c Release --no-build` (GCAM_UI_TESTS=0, GCAM_RENDER_SNAPSHOTS=0, GCAM_EVIDENCE_TESTS=0): engine 355 → **378**,
Studio.Core 184 → 184, services 87 + 7 skipped → same, UI 13 + 14 skipped → same; all pass. New tests (23): pinned-file
hashes / no CR, placeholder regeneration, writer refusals, spectrum-by-reference; validated spectrum = accepted file
with only status changed, evidence-grade acceptance and rejections; `PoissonExact` moments and P(0) (4 λ, k·σ);
`CountingWindow` Φ vs an independent erf series and window probabilities; ambient-map identity with the process's
detected rate; branching proportionality; matrix decode = decoder exactly (cyclic and non-cyclic); Z mean 0 / variance
1 under a multinomial background (sample-derived SE); no candidate without counts. No golden expectation changed.

## 10. Reproduce

```powershell
dotnet build Gcam.sln -c Release
dotnet src/Gcam.Cli/bin/Release/net9.0/Gcam.Cli.dll ambient-issue samples/ambient/terrestrial-unscear2000-v1-issue.json <new-folder>
python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-ambient-v1.json --out <dir> --family gate_selection --jobs 10
python samples/evidence/ambient/select_thresholds.py --runs <dir> --out <thresholds.json>     # must reproduce gate-thresholds-v1.json
python samples/evidence/run_seeds.py --manifest samples/evidence/manifest-ambient-v1.json --out <dir> --family gate_validation --jobs 10
python samples/evidence/ambient/aggregate_gate.py --runs <dir> --out <gate.json> --csv <sources.csv>
```

APPROVAL REQUESTS: none.
