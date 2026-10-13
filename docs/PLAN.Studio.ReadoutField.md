# PLAN.Studio.ReadoutField — physical readout with the ambient field on (TODO-42)

Scope: TODO-42. Today a physical readout (`FourOutputAnger`) starts only with ambient and BSR at zero, because the
ambient and BSR transports keep no interaction sites for the readout network (SD-3 in
[archive/PLAN.Studio.Readout](archive/PLAN.Studio.Readout.md)). This plan extends interaction recording to the
ambient transport so a scene **with a source** can run the physical readout with the terrestrial field on (stage A),
and leaves BSR to a later stage B with a decided spatial contract. No change to the DirectCrystal path, its RNG
streams or the published evidence; no new localisation-performance claim.

Status: **stage A done 2026-10-13; stage B (BSR, D2) next** — size estimate by Codex (`01a130ff…`, read-only); turn 1
review by Codex (`01a133ac…`, [review](PLAN.Studio.ReadoutField.Review.md)); decisions D1–D8 and R1–R7 below. Stage A
implemented by Codex in the review conversation; it stopped at the final verification ("model at capacity", thread lost),
and a new Codex conversation (`01a133d2…`) found the implementation complete, regenerated the catalog discovery and
verified. Planner verification: Release 0 warnings, normal suite 1,033 / 49 (1,036 after merging MainRoot), opt-in
renders 25 / 25, catalog 196 units, CI checkers pass; DirectCrystal exact-hash identities pass. Evidence: timing pilot
(Codex) and the 16-seed family `samples/evidence/results/readout-field-v1-family.json` run by the planner — zero exact-
identity violations over 160 cells, eight stochastic checks within |z| < 3.23 (family budget 0.01), assessment
`readout-field-v1-assessment.json`; a second full run is identical apart from timing fields. Desktop scenarios
`PhysicalAmbient_BothBounds_ContinuationConservesMeasuredCounts` and `RefusedLineAndMissingPreparation_ExplainWithoutZeroing`
compiled, not run (desktop unavailable). Findings and EV wording follow at closure, after stage B.

## Calibration convention (literature check, 2026-10-13)

Which calibration line serves a field event decides what stage A may promise, so the convention was checked before
deciding (sources are patents and preprints, not standards):

- **Calibrate at the energy analysed.** Gamma-camera energy, linearity and uniformity maps are made per isotope;
  linearity errors depend on the gamma energy as well as position, so correction factors from one energy do not
  carry over to another ([US5345082](https://patents.google.com/patent/US5345082),
  [US20110155900](https://patents.justia.com/patent/20110155900),
  [US4899054](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/4899054)). Nuclear medicine
  calibrates with Tc-99m 140 keV or Co-57 122 keV, PET at 511 keV; a block detector's crystal map is sometimes built
  with a higher-energy flood so every crystal is lit ([US5986266](https://patents.google.com/patent/US5986266);
  crystal-map segmentation: [arXiv 1404.6628](https://arxiv.org/pdf/1404.6628)).
- **Without a check source, the natural K-40 line is the reference.** Background-only instruments hold their gain on
  the 1461 keV K-40 photopeak or its Compton edge
  ([gain compensation on K-40](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/9435899),
  [K-40 Compton edge](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/10234577),
  [HPGe drift by K-40](https://doaj.org/article/2c2fee7defff44fba53b3eb42cef2c16)) — the same direction as the
  project's "no built-in source; peak tracking" decision.
- **Cs-137 662 keV is the convention for quoting resolution and a common check source**, not a calibration standard
  for a scene without that line.
- **This simulator agrees:** TODO-41 found the crystal LUT energy-dependent (122.06 keV refused; 661.7 and 1332.5 keV
  offered with different history budgets), so "one line calibrates every energy" would be an assumption here too.

Consequence (D3, D4): with a source, its lines calibrate and field events are positioned with that LUT. The sources
support "calibrate at the analysed energy"; they do **not** establish that a source-line LUT positions field deposits at
other energies accurately — that is a declared simulator approximation whose off-energy accuracy is unvalidated (R2).
A field-only scene is refused in stage A as a product decision based on the offered calibration set, not a physical
impossibility (R2).

## What exists (from the 2026-10-13 estimate; *verify* rows are re-checked in turn 1)

- **Ambient transport:** `AmbientPhotonProcess` builds its own transport streams and a pixel callback without an
  interaction sink (~75–85), advances one incident history to a fixed horizon including misses (~104–119), front-mask
  and all-face incidence (~137–163), and clamps the projected deposit to the incident energy (~83–85).
  `AmbientAcquisition` interleaves a pending source event with ambient arrivals (~56–74); source-free transport
  already works at this layer (~30–48).
- **Recording refusal:** `ListModeSource` refuses interaction recording with ambient / BSR / dark (~75–80) and adds a
  fixed-horizon look-ahead (~273–286). `Readout/PhysicalListModeSource` refuses ambient, BSR, dark and legacy optical
  crosstalk (~43–45), keeps 256 truth histories (~23, 75) and separately keyed response / ADC streams (~52–54).
- **BSR:** `ListModeBackground.Advance` transports a diffuse history, then `Place` draws a random pixel
  (~38–49) — the transported location is discarded (checked by the planner).
- **Detector sites:** `ComptonCrystalDetector`'s interaction sink (~63, 209–214, 263) can record sites at the scoring
  call; its buffer is reused and must be copied. `ReadoutDevice.ExpectedPhotoelectrons` (~148–159) needs crystal IDs,
  depth and deposited energy.
- **Studio:** `ReadoutPolicy` derives the required lines from source isotopes only (~79–93); `SimulationService`
  captures and validates the line bank at Start (~24–39) but refuses physical `StartAmbient` (~53) and fields / BSR in
  `BuildConfig` (~65); `MainViewModel` refuses before Start (~487–495, message in `MainViewModel.Readout.cs` ~42).
- **Latent defect:** `AcquisitionSession` publishes `SourceRateCps = _config.Ambient is null ? 0 : source!.SourceRateCps`
  (~225) — with a physical ambient session the direct `source` is null, so removing the refusal alone would throw
  (checked by the planner).
- **Geometry difference:** ambient and BSR build their detectors without reflector gaps or entrance / backing
  (`AmbientPhotonProcess` ~78–85, `ListModeBackground` ~28–31), while the physical readout's signal path uses the
  module's crystals and gaps. *verify*
- **Unchanged by TODO-41:** the four transport files are byte-identical since TODO-40 (`1ef455e`); the LUT, adjoint
  solve, line banks and calibration streaming are prerequisites already in place.

## Decisions (author 2026-10-13; *verify* = check in the code during turn 1)

| # | Decision | Kind |
|---|---|---|
| D1 | **Two stages.** Stage A: ambient with the physical readout. Stage B: BSR with the physical readout (D2). Stage A keeps refusing BSR, electronic dark arrivals and legacy optical crosstalk with explicit messages. | author: recommended |
| D2 | **BSR spatial contract (stage B): option A** — a separate physical diffuse detected-input transport whose sites and pixel come from the same transported history (Place bypassed only on the physical path); BSR stays a pre-trigger detected-input ratio. Rejected: translating sites into the legacy random pixel (synthetic history, boundary policy); a new incident-fluence background model (larger, overlaps the ambient model). | author: recommended |
| D3 | **Source plus field:** prepare only the scene's source lines; field events enter the same pulse stream and are windowed / assigned by the measured record — never dispatched by origin or true energy. Field events at other energies being positioned with the source line's LUT is the conventional behaviour and is documented as a limitation. | author (literature check above) |
| D4 | **Field-only scene: refused in stage A** with an explicit message (no analysed energy, so no conventional calibration line). Backlog: a K-40 1460.8 keV calibration line through the offer-record evidence pipeline, then field-only support. | author (literature check above) |
| D5 | **Physical-path geometry:** the physical ambient (and later BSR) transport uses the same detector construction as the physical signal path (module crystals, reflector gaps, entrance / backing); the DirectCrystal ambient path is unchanged, so EV-34 stays valid, and the two paths' ambient numbers are documented as different by construction. *verify* how the signal path builds its detector. | author: recommended |
| D6 | **DirectCrystal bit-identical:** with recording off, signal / ambient / BSR streams, Place and every RNG progression are unchanged (exact-hash tests). | planner |
| D7 | **Fix the latent defect** (`AcquisitionSession` ~225) before any refusal is removed; physical component rates come from the physical producer. | planner |
| D8 | **Evidence:** a short timing pilot (incident transport and readout separately, Baseline and one fine head, both ambient bounds), then one focused 16–32-seed family for count / rate conservation with derived tolerances. No new localisation claim; the offer record is not changed. | author: recommended |

## Decisions after review (2026-10-13, author accepted)

| # | Adopted | From |
|---|---|---|
| R1 | **D5 incoming-face passive convention (opt-in, physical path only).** The detector applies the entrance slab to every face and rejects rays with dir.Z ≥ 0 (`ComptonCrystalDetector` ~153–158), so copying the signal constructor's arguments would drop rear incidence on the all-face bound. Add a boundary-aware module mode: determine the entering face; front entry passes the entrance slab, rear entry passes the backing slab as an incoming passive proxy, no side housing; accept scattering only while the direction continues inward for that face; keep the existing outgoing backing proxy. The default `Score` stays byte-identical. Tests: deterministic face selection and unchanged-pass rays before any numerical evidence; zero-passive mode; seeded front path equal to the signal path. Documented as nominal-envelope sampling with lumped steel-equivalent slabs (no explicit passive volumes or angle-dependent path), not a housing simulation. Author: "there is no guarantee nothing enters through the back plate". | review §D5 |
| R2 | **Wording.** The literature supports calibrating at the analysed energy only; positioning off-energy field deposits with a source-line LUT is a declared approximation, unvalidated. The field-only refusal is a product decision based on the offered calibration set. Applied in the calibration section above and to every requirement / help text. | review §Assessment |
| R3 | **All-face bound on the physical path** is "unmasked all-face module approximation", no longer a bare-crystal upper bound; captured metadata and help say so. Front-only keeps its meaning (front envelope through the mask). | review §D5 |
| R4 | **Owned transport envelope** (one sealed type: event, owned read-only sites, origin) through every nested pending record — signal, ambient merge, fixed-horizon wrapper — with deterministic tie order, proved empty intervals advancing the observed pulse horizon, no future look-ahead advancing it, and bounded storage (one transient envelope per producer, 256-history truth cap). Producers clear their own last-result state before each attempt (a miss may not call the sink). | review §Stage A |
| R5 | **Every old refusal site changes together:** `ListModeSource` (~75–81), `PhysicalListModeSource` (~43–45), `SimulationService` (~53, ~65), `MainViewModel` (~478–490), `MainViewModel.Readout.cs` (~42) and `MainViewModel.Options.cs` (~16, 31–32); the field-only refusal gets its own reason ahead of the generic missing-line reason, through the TODO-45 status-bar refusal path (`StartRefusalTests`). | review §D3 / D4 |
| R6 | **Evidence recipe as reviewed:** timing pilot (Baseline and Sharp at 661.7 keV, both bounds, 0.1 and 1 µSv/h, separate timers per stage, cancellation during a low-acceptance interval); then 16 outer seeds (32 only if a precision target fixed from the pilot requires it, chosen before confirmation), exact count identities plus statistical checks whose tolerances are derived from Poisson / binomial sample sizes and a declared family error budget — no borrowed tolerance, no EV-34 efficiency as physical truth. The planner launches the long job and re-runs one seed. | review §D8 |
| R7 | **Size:** stage A ≈ 460–750 production / 570–930 test lines; stage B ≈ 150–250 / 190–310; evidence tooling 120–200; documentation 160–260. | review §Risks |

## Size (estimate, 2026-10-13)

Stage A ≈ 490–790 production lines and 450–740 test lines in ≈ 14–18 files (less the source-free policy share, now
refused by D4); stage B ≈ 135–235 production and 170–280 test lines. Evidence tooling ≈ 100–180 lines; documentation
≈ 120–220 lines. Planning ranges, not a measured diff.

## Steps

1. Turn 1 (review, read-only): check every *verify* row and the "What exists" line references; propose the site
   envelope type and its ownership through the nested pending records (signal, ambient merge, fixed-horizon wrapper);
   say how D5 is built from the signal path's detector; list the requirement rows and desktop scenarios that change;
   estimate the timing pilot.
2. Planner: "Decisions after review".
3. Stage A implementation; headless tests (recording-off exact hashes; paired sites through merge / Stop / Continue;
   empty intervals; energy conservation with a derived rounding bound; field-only and BSR refusals; the defect fix);
   Studio readiness / messages; render checks of the refusal and readiness texts.
4. Planner verification, timing pilot, seed family; desktop scenarios on the author's go (rewrite
   `RefusedLineAndField_ExplainWithoutZeroing`, add a physical-ambient continuation scenario).
5. Stage B (BSR, D2) in the same conversation after stage A is recorded.

## Done when

- A source scene runs the physical readout with the ambient field on (both bounds); Stop / Continue keep measured
  records identical; component rates are published without the null-source path.
- A field-only scene and BSR (until stage B) are refused with explicit messages; DirectCrystal is bit-identical.
- SR-RUN-34 / -29 / -22 / -13, SR-DET-05 / -11 and the input table are updated; the evidence family is recorded;
  the affected desktop scenarios pass with broken-verdict / recovery pairs.

## Not here

- Field-only physical acquisition and a K-40 calibration line (Backlog, D4); electronic dark triggers; Studio
  IndependentSipm; ReadoutStudy's experiment matrix; ambient subtraction or background-aware reconstruction; any
  change to the offer record or the DirectCrystal evidence.
