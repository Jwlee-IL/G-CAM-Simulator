# PLAN.Physics.CascadeEmission.Review — implementer's measured review of per-decay cascade emission (TODO-14)

Scope: review of [PLAN.Physics.CascadeEmission](PLAN.Physics.CascadeEmission.md) (E-1 … E-5) by the substitute
implementer (Claude subagent, Codex out of credits), 2026-10-02, in the worktree `C:\gw\w14` at `85ff1ec`. No
repository source or test was changed; every number below comes from a throwaway probe
(`%TEMP%\gcam-cascade-probe`, referencing `Gcam.Studio.Services`) that builds the configuration with
`SimulationService.BuildConfig` and transports photons through the same `CreateMask` → `ComptonCrystalDetector`
(Argmax, pixel sink) path `ListModeSource` uses.

**Short answer.** At Studio's default geometry the true-coincidence sum fraction of detected Co-60 events is
**2.007 × 10⁻⁶ at 1000 mm** and **2.162 × 10⁻⁵ at 300 mm** — negligible at 1 m (one summed event per ~62 min at
500 µCi), ~50× smaller than random pile-up at the default 500 µCi at any distance. The physics is still worth doing
cheaply: the proposed estimator costs nothing measurable in acceptance, and it is unbiased (checked against analog
4π decays). Two findings beyond the plan: **`DefaultRandom` (seeded `System.Random`) gives a 1–2 % bias with
rejection sampling in the stream** (R-6), and **Studio's Spectrum only sums same-time events when the pile-up
toggle is on**, so the "separate hits, let pile-up sum them" form of E-1 is wrong for Studio (R-7).

## 1. "What exists" rows — checked in the code

| Plan row | Verdict | Evidence |
|---|---|---|
| Decay schemes: Cs-137, Co-60, Na-22; `Sample` returns one decay's photons | **confirmed** | `DecayScheme.cs`: `For` / `From` (other names throw), `Sample` fills a list |
| Co-60 W(θ) approximated as independent isotropic | **confirmed** | `Sample`, case `Co60`: two `rng.NextOnUnitSphere()` calls, each behind its own `NextDouble() < 0.999`. The class summary says "with … angular correlation", the bullet below says "taken as independent-isotropic", and `CascadeSummingStudy`'s summary says "correlated per-decay gammas with angular correlation". The last is wrong for Co-60 |
| "A₂ ≈ 0.10, ~10 % close-geometry correction" (code comment, Findings 44) | **corrected** | the correction is **largest far away**, not close: two photons that both reach a small detector are near-parallel, so W(0)/W̄ = 1 + A₂ + A₄ = 1.111. Measured: 1.1094 ± 0.0008 (1000 mm), 1.1104 ± 0.0008 (300 mm), 1.1092 ± 0.0013 (100 mm). It shrinks only when the detector subtends tens of degrees |
| Na-22: back-to-back 511 pair | **confirmed** | `d` and `d * -1.0`; 1275 independent isotropic (correct — no β⁺–γ correlation for an unoriented source) |
| Cascade study: sum ∝ ε², reference near geometry | **confirmed, with a caveat** | `CascadeSummingStudy` uses a coin-flip mask (`openFraction`), normal-incidence `CrystalDeposit`, and `DefaultRandom` with `DecayScheme.Sample` — the draw pattern R-6 shows to be biased at the % level for analog sampling (not re-measured for that study) |
| List-mode: one biased photon per history, weight ≤ A/(4πz²), rejection, Poisson arrivals, no decay grouping | **confirmed** | `ListModeSource`: `WeightBound = max_j A/(4π z_j²)`, accept with `w / WeightBound` on its own stream (seed+8181), gaps from the running `SourceRateCps = Σ A·Σ intensity × ΣW / N` on stream seed+4242. Each `MixedFieldSource` history picks **one line** in proportion to activity × intensity |
| Pile-up: array-wide summing within the resolving time already exists | **confirmed for Spectrum only, and only when enabled** | `SpectrumService` merges consecutive events with `Δt < τ` (paralyzable, any pixels) **only if `settings.PileUp`**; τ = 730 ns for the default chain (GAGG / S13360-3050 / CR-RC). `ImagingService` measures and windows **every event separately** (no pile-up). `WaveformService` overlays pulses at their times (sums in time by construction) |
| (new) Studio's isotope table | **new** | `Isotopes` (Scene.cs): Co-60 0.999 / 0.999; Na-22 **511 at 1.798** vs `DecayScheme` 2 × 0.903 = **1.806** (0.4 % inconsistent — two sources of truth); Cs-137 lists 662 + Ba K X-rays (32.1, 36.4) which come from internal conversion of the same transition, i.e. they are **mutually exclusive with the 662 γ, not coincident**; Co-57 122 / 136 are alternative branches of the 136 keV level; Ir-192 (Σ 2.14 γ/decay) has real cascades but no decay scheme |

## 2. E-2 — weight algebra for an unbiased decay-level estimator

Notation: one decay of source j emits a photon set of size n (energies from the branching draw), directions with
joint density p(Ω₁ … Ω_n) = Π(1/4π) × correlation; for Co-60, p(Ω₂ | Ω₁) = W̃(Ω₁·Ω₂)/4π with
W̃ = W/W̄ (mean 1 over the sphere), symmetric in the pair. `q(Ω)` is the detector-rectangle proposal
(uniform point on the face, `w(Ω) = p(Ω)/q(Ω) = A cosθ/(4π r²)`). `f` is any score of the decay (detected,
sum deposit in a window, …). Because `ComptonCrystalDetector.Score` requires entry through the front rectangle
(z = 0) and the mask is binary (no scatter), **f = 0 unless at least one photon's ray hits the rectangle** —
`H = #{photons whose ray hits it} ≥ 1` on the support.

**Options rejected.**
- *Aim photon 1, partner from W(θ) | Ω₁, weight w₁* — biased: it misses every decay where photon 1 misses and
  photon 2 hits (≈ half of all detected Co-60 decays).
- *Aim both photons, weight w₁ w₂ W̃* — unbiased only for the joint term (both hit); singles are lost. Useful as a
  measurement estimator (used below, "C"), not for emission.
- *Unbiased 4π decays* — at 1 m P(≥1 detected) = 7.2 × 10⁻⁶ per decay: ~2 × 10⁵ decays per detected event, i.e.
  ~5 orders of magnitude slower than today. Not usable for Studio.

**Proposed: force one photon chosen uniformly, balance by the hit count.** Pick k uniformly among the n emitted
photons, aim photon k with q, draw the others from their conditional density given Ω_k (W̃ for Co-60, −Ω for the
511 partner, isotropic for an uncorrelated photon), and give the decay the weight
`ω = n · w_k / H`. Proof of unbiasedness:

E[ω f] = Σ_k (1/n) ∫ q(Ω_k) p(rest | Ω_k) · (p(Ω_k)/q(Ω_k)) · (n/H) f = ∫ p(all) f · Σ_k 1[k hits]/H = ∫ p f 1[H ≥ 1] = E_p[f],

using that q covers exactly "k hits", that p(Ω_k) p(rest | Ω_k) = p(all) for every k (the pair density is
symmetric), and f = 0 when H = 0. (A "first photon that hits" decomposition is also unbiased; the 1/H form is the
balance heuristic, symmetric and simpler.)

**Allocation and the bound (keeps today's acceptance).** Allocate histories to source j with probability
∝ A_j n̄_j (n̄_j = mean photons per decay = Σ line intensities — exactly today's `_emissionRateCps` normalisation)
and use `ω = n · w_k / (n̄_j · H)`. Then rate × E[ω f] = Σ_j A_j P_j(f): the existing rate code
(`SourceRateCps = R_emit × ΣW / N`) is unchanged. The weight bound is `max_j (n_max,j / n̄_j) · A/(4π z_j²)`:

| Isotope | n_max / n̄ | Effect on acceptance (common bound, whole scene) |
|---|---|---|
| single-photon schemes (Cs-137, Co-57, Am-241) | 1 / n̄ applies only if n = 0 draws are kept as histories — see design | none if they keep the legacy path |
| Co-60 | 2 / 1.998 = 1.001 | −0.1 % |
| Na-22 | 3 / 2.805 = 1.070 | −6.5 % for every source in the scene |

Rejection then stays `accept with ω / bound`, on its own stream. Measured: `maxW / bound = 1.0000` at 100, 300,
1000 mm (the n·w_k/H form with a bound of 2A/(4πz²); never exceeded).

**Measured against analog 4π** (Studio geometry, source on axis at **100 mm** — the only distance where analog is
affordable; 4 × 10⁹ analog decays, 2 × 10⁸ balance histories, xoshiro256** RNG and fixed-draw W sampling; see R-6):

| Quantity per decay | Analog 4π | Balance estimator (B) | Both-aimed (C) | z (A vs B) |
|---|---|---|---|---|
| P(≥ 1 detected) | 6.2445e-4 ± 3.9e-7 | 6.2498e-4 ± 1.2e-7 | — | 1.27 |
| P(both detected) | 1.040e-7 ± 5.1e-9 (450 events) | 1.0808e-7 ± 1.2e-9 | 1.0834e-7 ± 9e-11 | 0.78 (B vs C 0.22) |

A second, independent seed set with `DefaultRandom` + fixed-draw W gave z = −1.06 / −0.55 (B vs C −0.68). The
analog joint term is only a 5 % check (450 events); the 0.1–1 % agreement of B with C is the tighter one.

## 3. E-3 — Co-60 angular correlation

Co-60 → Ni-60: 4⁺ (2505.7) →E2→ 2⁺ (1332.5) →E2→ 0⁺, both pure E2. Theory: A₂ = 0.1020, A₄ = 0.0091 in
W = 1 + A₂P₂ + A₄P₄, equivalently **W(θ) = 1 + (1/8) cos²θ + (1/24) cos⁴θ** (a₂ = 0.125, a₄ = 0.04167).

- Sources: D. R. Hamilton, Phys. Rev. 58, 122 (1940) (the theory); the ORTEC application note AN34, Experiment 19,
  "Gamma-ray decay scheme and angular correlation for ⁶⁰Co" (states a₂ = 1/8, a₄ = 1/24; measured
  a₂ = 0.100(2), a₄ = 0.014(2) quoted in student reports against theory 0.1020 / 0.0091). Found through a web
  search on 2026-10-02 — the primary papers themselves were not opened.
- Independent check in the probe: the Racah formula A_k = F_k(L₁L₁I_iI) F_k(L₂L₂I_fI) with 3j / 6j symbols computed
  from factorials gives **A₂ = 0.1020, A₄ = 0.0091 → 1 + 0.12500 cos² + 0.04167 cos⁴**; the same code reproduces
  the textbook 0(1)1(1)0 → 1 + cos²θ and 0(2)2(2)0 → 1 − 3cos²θ + 4cos⁴θ.
- Sampler: inverse CDF of G(c) = c + a₂c³/3 + a₄c⁵/5 (odd, increasing) by Newton, **one uniform per partner**; 10⁸
  samples, 10 bins in cos θ: χ² = 5.3 (xoshiro) / 10.9 (`DefaultRandom`), 9 dof. Rejection sampling gives the same
  histogram but must not be used with `DefaultRandom` (R-6).
- Na-22: keep 1275 isotropic and independent of the pair; the pair stays exactly anti-parallel (acollinearity
  ~0.5° is irrelevant for a one-sided detector).

## 4. E-4 — measured sum fraction at Studio's default geometry

Configuration (from `SimulationService.BuildConfig`, default `OpticsSettings` / `DetectorSettings`): rank 13 2 × 2
mosaic, 0.7 mm cells, 10 mm W (μ 0.178 /mm at 662, W μ(E) scaled), D 80 mm; 30 × 0.6 mm GAGG, 10 mm thick,
0.15 mm entrance absorber, 2 mm backing, 0.1 mm reflector gap, no crosstalk; Co-60 on axis. "Detected" = at least
one photon leaves a deposit > 0 in the crystal (what becomes a `DetectedEvent`). Branching 0.999 per line as in
`DecayScheme`/`Isotopes`, W(θ) as in §3. Estimators: single-photon ε (2 × 10⁸ biased each), P(≥1) from B
(2 × 10⁹ histories), joint terms from C (2 × 10⁸). Uncertainties are 1σ standard errors (delta method), xoshiro RNG.

| Source distance | ε₁₁₇₃ per photon | ε₁₃₃₂ per photon | P(≥1) per decay | **Sum fraction** (both deposit / detected decays) | Sum-**peak** fraction (both fully absorbed) | Measured / ε-expectation |
|---|---|---|---|---|---|---|
| 1000 mm | 3.690e-6 | 3.548e-6 | 7.229e-6 | **2.007e-6 ± 1.0e-9** | 8.21e-8 ± 2e-10 | 0.9994 |
| 300 mm | 3.976e-5 | 3.824e-5 | 7.791e-5 | **2.162e-5 ± 1.1e-8** | 8.92e-7 ± 2e-9 | 0.9993 |
| 100 mm (validation) | 3.178e-4 | 3.078e-4 | 6.248e-4 | 1.734e-4 ± 1.4e-7 | 7.26e-6 ± 3e-8 | 0.9988 |

- **ε² expectation**, derived from the measured single-photon efficiencies (nothing borrowed):
  P(both) = c b² ε₁ε₂, fraction = P(both) / (bε₁ + bε₂ − P(both)) with c = W̃(θ→0) = 1.1111 → 2.008e-6 and
  2.164e-5: measured/expected 0.9994 / 0.9993. With c = 1 (today's isotropic model) the expectation is 11 % low.
  The fraction ≈ c·ε/2 — it is ∝ ε (the sum count ∝ ε²); 300 → 1000 mm divides it by 10.8 (ε ratio 10.77).
- Summing-out of the 1332 photopeak equals c·b·ε₁₁₇₃: 4.09e-6 at 1 m, 4.41e-5 at 300 mm.
- **In Studio terms** (500 µCi = 1.85 × 10⁷ Bq): 1 m — 134 detected decays/s, **one summed event per ~62 min**, one
  2505 keV sum-peak count per ~25 h; 300 mm — 1441 /s, one summed event per ~32 s, one sum-peak count per ~13 min;
  100 mm — 11.6 k/s, 2 summed events/s, ~5 sum-peak counts/min.
- **Against random pile-up** (τ = 730 ns, `SpectrumService.ResolvingTimeS`): R·τ = 9.8e-5 at 1 m and 1.05e-3 at
  300 mm — ~50× the cascade fraction at 500 µCi. Both scale with ε, so the ratio is geometry-independent:
  fraction / (R τ) ≈ c / (4 A τ); **cascade summing exceeds random pile-up only below ≈ 10 µCi** (3.8 × 10⁵ Bq).
- **Plainly: at 1 m true-coincidence summing is negligible in Studio** (2 × 10⁻⁶ of events; invisible in any
  realistic acquisition). It becomes a visible sum peak only within ~100 mm with minutes of live time.

## 5. E-1 — which emission form matches Studio's readout

| Form | Spectrum | Imaging | Waveform | Stop / continue |
|---|---|---|---|---|
| (a) **one event per decay**: total deposit of all photons, position = arg-max site over the merged sites of the decay | summed always (as in hardware: one trigger, one charge) | sum events fall out of the windows → summing-out appears where it physically does | one pulse of the summed amplitude | unchanged: `Advance` still returns ≤ 1 event, the single `_pending` look-ahead suffices |
| (b) separate pixel hits with one arrival time | summed **only if the pile-up toggle is on** (R-7) — off, the decay counts as two full-energy pulses, which no real readout does | every hit windowed separately — wrong | two pulses at the same sample sum anyway | `_pending` holds one event; a partner at the identical time needs a queue in `ListModeSource`; ties with `ArrivalTimeS >= target` |

**Recommend (a).** It is what `ListModeSource` already does for one photon's multi-site Compton history (total
deposit, Argmax site) — a decay is the same object with more sites — and it is what an array-wide (Anger or summed)
readout records. For exactness the arg-max must be taken over the **merged site list** (two photons in one pixel
add); the current `pixelEventSink(x, y, total, weight)` loses the site deposits, so the detector needs a site-level
sink (or Begin/End-history scoping); TODO-19's centroid needs the same site list later. The argmax-pixel gain in
`MeasurementStage.Amplitude` is then applied to the whole sum — the same approximation already made for multi-site
photons; TODO-19 replaces it.

**What must change.**
- Engine: a decay-level history in `ListModeSource` (or a small `DecayEmitter` it owns): per source with a decay
  scheme, sample the photon set, force k, partners conditionally, transport each through mask + crystal, merge
  sites, one weight ω, one rejection, one gap. `DecayScheme` gains W(θ) (inverse CDF), a non-throwing lookup, and
  per-line intensities that agree with `Isotopes` (or `Isotopes` derives from it — fix Na-22 1.798 vs 1.806).
- Only isotopes with n ≥ 2 photons per decay take the new path (Co-60, Na-22). For n ≤ 1 schemes (Cs-137 662 / Ba K
  are exclusive; Co-57 122 / 136 exclusive; Am-241) today's one-line-per-history emission **is** the exact
  per-decay emission — keep it, and keep the legacy RNG draws when no scene source has a cascade (the precedent in
  `ListModeSource.Advance`: "the disabled path consumes precisely the legacy RNG draws"). Ir-192 has cascades but
  no scheme — stays independent; say so in the UI/docs or open a TODO (its summing is of the same negligible order).
- Arrival times: unchanged mechanism (one gap per accepted decay at the running rate); `DetectedWeight` accumulates ω.
- `AcquisitionSession`: no change in logic; the stop/continue equivalence test must be re-run with a Co-60 scene.
- Seeds: Co-60 / Na-22 (and mixed) scenes draw differently → their seeded outputs change (render fixtures and tests
  naming Co-60: `MainWindowRenderTests`, `AcquisitionContinuationTests`, `ImagingServiceTests`,
  `OpticsProjectionTests`, `SpectrumServiceTests`, `WaveformServiceTests`, `AcquisitionViewModelTests`,
  `ImagingWorkspaceTests`, UI tests) — check which assert exact values. `ImagingService.Calibrate` uses
  `ListModeSource` for H-only strip ratios and will pick the new path automatically (effect ~10⁻⁶).
- Optional diagnostics: a `ListModeSource` counter of decays with ≥ 2 depositing photons (for tests and provenance).

## 6. Further findings

- **R-6 — `DefaultRandom` bias with rejection sampling (measured, reproducible).** `DefaultRandom(seed)` is the
  legacy seeded `System.Random` (subtractive lagged generator). Analog decays drawing `NextDouble, NextDouble,
  NextOnUnitSphere, <rejection-sampled W partner>` hit the 18 mm detector at 100 mm **2.0 % too rarely** (2.5071e-3
  vs the analytic solid-angle fraction 2.5576e-3, 45σ); P(≥1) came out 1.0 / 1.0 / 1.2 % low on three seed bases
  (z = −10 … −13). Isolated: the bias needs the data-dependent draw count of rejection sampling; with two leading
  draws it is −1.9 %, other offsets ≈ 0; replacing the rejection by a one-draw inverse CDF removes it; xoshiro256**
  shows none (2.5568e-3). The directional-biased single-photon path agreed with analog-xoshiro to 0.05 %. Consequences
  here: sample W by inverse CDF; never use `DefaultRandom` for an analog-vs-biased reference test. Wider (not
  measured): `CascadeSummingStudy` (analog, `DecayScheme.Sample` + transport on one `DefaultRandom`) and any study
  mixing rejection sampling with `DefaultRandom` may carry % biases — a candidate TODO (a modern seeded PRNG
  behind `IRandom`, which changes every seeded result).
- **R-7 — Spectrum's pile-up toggle.** With form (b) the summing would depend on a display option; form (a) avoids
  it. Independently, Imaging never applies pile-up (single-event windows) — fine at Studio rates (R·τ ≤ 10⁻³).
- **R-8 — Findings 44 wording** ("~10 % close-geometry correction") is reversed: the W(θ) correction is 11.1 % for
  distant sources and decreases only at very close range.

## 7. Size and E-5 test plan

Size: engine ~150–250 lines (decay emitter / `ListModeSource` history, `DecayScheme` W(θ) + lookup, site sink in
`ComptonCrystalDetector`), no Studio service change beyond re-run fixtures; tests ~150–250 lines. CPU: the probe's
balance histories cost the same as single-photon ones (one extra uniform + Newton per partner; the partner is
transported only when it hits the face, 2.6 × 10⁻⁵ at 1 m); not timed inside `ListModeSource`.

| Test | Assertion (tolerance derived, not borrowed) |
|---|---|
| Per-decay photon counts / energies | Co-60 n ∈ {0,1,2} with P(2) = b², P(1) = 2b(1−b): counts within 4σ of the binomial; energies exactly the scheme's; Na-22 pairs anti-parallel to 1e-12 |
| W(θ) sampler | E[P₂(cos θ)] = A₂/5 = 0.02040 and E[P₄] = A₄/9 = 0.00101 within 4·σ/√N (σ from the sampled values); χ² over 10 bins below the 0.999 quantile (27.9, 9 dof) |
| Racah constants | 1 + A₂ + A₄ = 10/9 from the coefficients 1/8, 1/24 (exact) |
| Unbiased decay estimator | biased (B) vs analog 4π decays, P(≥1) and P(both), \|Δ\| < 4·√(s_A² + s_B²); fast version on a large-solid-angle configuration with a thin mask (both terms cheap analog), full Studio-geometry version as an opt-in Evidence test; analog reference on a non-`DefaultRandom` generator or with fixed-draw sampling only (R-6) |
| Weight bound | ω ≤ bound on every detected history (already asserted at run time) |
| One event per decay | summed deposit equals the sum of the partners' deposits (sink-level test); never two events with one decay id; arrival times strictly increasing |
| Rate | detected Co-60 rate ≈ A · P(≥1) within the existing rate test's derived tolerance |
| Stop / continue | the existing equivalence test with a Co-60 scene: events identical to the uninterrupted run |
| Legacy path | a Cs-137-only scene produces bit-identical events before and after |

## 8. Not checked

- The probe mirrors `ListModeSource`'s transport but is not `ListModeSource`; the final numbers must be re-measured
  through the implemented class.
- Off-axis sources; other scintillators; Na-22 numbers (design derived, not run).
- The primary W(θ) papers (cited from a search summary and confirmed by my own Racah calculation); ENSDF Co-60 /
  Na-22 intensities (Co-60 ≈ 99.85 / 99.98 %, Na-22 β⁺ ≈ 90.3 % from memory — not verified; 1.798 vs 1.806 needs
  the planner's choice of evaluation).
- `CascadeSummingStudy`'s absolute yields under R-6 (not re-measured).
- Wall-clock cost inside Studio.

APPROVAL REQUESTS: none (the probe lives in `%TEMP%\gcam-cascade-probe`; only build outputs under the worktree's
ignored `bin/` / `obj/` were written).
