# PLAN.Studio.Spectrum — the Spectrum workspace (TODO-07)

Scope: what `Gcam.Wpf` does, the decisions S-1 … S-5 and the steps. Runs after
[PLAN.Studio.LiveAcquisition](PLAN.Studio.LiveAcquisition.md), whose events it reads.

Status: implemented (uncommitted, 2026-10-01), headless tests pass; the real-window check waits for a free desktop.

**What `Gcam.Wpf` does today** (`PrepareLive`, `LiveTimer_Tick`, `RenderSpectrum`, `UpdateFrontEnd`): a second MC
(`EventStreamStudy.Generate`, 20 000 events) gives a deposit pool; optional pile-up from the chain's pulse
(`EventStreamStudy.ApplyPileUp`, `ResolvingSamples(rise, tail)`); each deposit smeared by `FrontEndModel.Measure` of
the selected chain (`FrontEndParts`, `src/Gcam.Wpf/FrontEnd.cs` — the "Resolution %@662" box is overwritten by the
chain at start-up); 256 bins up to 1.15 × the highest line (2.15 × with pile-up), normalised and Poisson-sampled
every 250 ms of live time; a hand-shaped exp(−E/30 keV) noise wall; one ROI band per distinct line,
E·(1 ± N·FWHM₆₆₂), N default 1.5, and the share of counts inside the windows.

**Decisions (author, 2026-10-01)**

| # | Decision | Reason |
|---|---|---|
| S-1 | The Y axis is **counts acquired in live time** — the TODO-13 events, not a separate pool | independent events over time are the physical picture and the convincing one |
| S-2 | **No noise floor** | it is a hand-shaped curve; the repository rule is "never hand-add a tail / line" |
| S-3 | **Resolution derived from a front-end chain**, no hand-set % box; TODO-07 shows the default chain read-only, TODO-10 adds chain selection | same rule; it is what `Gcam.Wpf` effectively does |
| S-4 | **Live**, built as the acquisition runs (TODO-13), not one batch result | as S-1 |
| S-5 | **Windows follow the resolution at each line, and only resolvable lines get their own band.** Window = E ± N·FWHM(E), FWHM(E) from the chain's `FrontEndModel`. Lines of one scene closer than FWHM at their mean energy merge into one band spanning [E_min − N·FWHM, E_max + N·FWHM], labelled with every energy in it. Bands of resolved lines may overlap at their edges; the in-window share counts each bin once. *(Planner decision 2026-10-01 after the implementer found the plan inconsistent; the author may override.)* | `Gcam.Wpf` used one fraction N·FWHM₆₆₂ for every line, which at 32 keV is ~4× narrower than the real peak (FWHM ~27 % vs 6 %), and drew two bands for the unresolvable Ba K X-rays (32.1 / 36.4 keV, 4.3 keV apart under a ~9 keV FWHM). Merging by window overlap instead would wrongly merge the resolved 1173 / 1332 keV pair. |

**Run inputs vs view settings.** The scene, live time and speed are run inputs (TODO-13). View settings re-render
from the acquired events without a new acquisition and never mark the result stale: log Y; peak window N (in
TODO-07 it moves the ROI bands and the in-window share only — it becomes a run input in TODO-08 when it drives the
imaging windows); pile-up on / off.

**Steps**
1. **Front-end presets into the engine.** Move `ScintPreset`, `SensorPreset`, `PreampPreset`, `FrontEndParts` from
   `src/Gcam.Wpf/FrontEnd.cs` to `src/Gcam.Configuration` (they only build a `FrontEndConfig`); point `Gcam.Wpf`
   at the moved types (delete its copy — a `using` change, nothing else). Add `FrontEndParts.Default` (the chain
   `Gcam.Wpf` selects at start-up) and one tested helper for the pulse rise / tail in ADC samples as
   `UpdateFrontEnd` derives them.
2. **`ISpectrumService`** (contract in `Gcam.Studio.Core`, implementation in `Gcam.Studio.Services`): from the
   acquisition's events (deposit, arrival time) and the view settings, build a `SpectrumView` — bin centres,
   counts, one band per resolvable line group (S-5), in-window share, resolution at 662 keV. Smearing uses `FrontEndModel` of the
   chain; pile-up merges events within the chain's resolving time using their real arrival times. Deterministic
   for a seed. Incremental where cheap (new events since the last snapshot are smeared once and added); a pile-up
   toggle re-processes the kept events.
3. **`SpectrumWorkspaceViewModel`** (title "Spectrum", `Workspace.Spectrum`): view settings as observable
   properties; follows the acquisition snapshots; exposes `PlotSeries` (Area), `PlotBand`s labelled with the line
   energy, and a per-line table (isotope, line keV, window lo–hi keV, counts in window, share). Adding it makes
   the workspace switch appear.
4. **View.** Centre: `PlotView` (X "measured energy (keV)", Y "counts", log Y) with its readout `TextBlock`
   beneath, then the per-line table. Right panel: Display (log Y), Window (N × FWHM), Pile-up (checkbox with the
   derived resolving time shown), Resolution (read-only, from the chain). Stale chip as on Imaging. Accessible
   names and AutomationIds `Spectrum.Plot`, `Spectrum.LogY`, `Spectrum.Window`, `Spectrum.PileUp`,
   `Spectrum.Lines`.
5. **Tests.** Services (real engine): in a Cs-137 acquisition the maximum **within the photopeak region**
   (above the Compton edge, 478 keV, up to the axis end) is the bin holding 661.7 keV — not the global maximum,
   which is physically the Ba K X-ray peak: those ~32 keV photons are absorbed almost fully and land in a peak
   ~3× narrower than the photopeak, so its tallest bin can exceed the 662 keV one; the photopeak FWHM matches `FrontEndModel.FwhmFraction(661.7)` within a stated k·σ / bin tolerance;
   pile-up lowers the total and puts counts above 1.2 × 661.7 keV at a high rate; a Cs + Co scene gets four bands — Ba K X-rays (32.1 + 36.4 merged), 661.7, 1173.2, 1332.5 keV (with the default
   chain, 4.57 % FWHM at 662 keV, the 1173 / 1332 windows at N = 1.5 do not even touch); the merge rule is unit-tested
   with an injected FWHM so that resolved lines' windows *do* overlap and still stay separate bands;
   same seed → identical histogram. Configuration: the moved presets build the same
   `FrontEndConfig` and pulse times the old code path produced. ViewModel (fake service, virtual clock): a view
   setting re-renders without a new acquisition and without stale; counts grow with snapshots.
6. **Docs in the same change:** VV.Studio.SRS (`SR-SPEC-*`), VV.Studio.SDS, VV.Studio matrix, DESIGN.Layout
   (Spectrum centre / panel), DESIGN.Architecture (spectrum service), AGENTS.Studio roadmap row; test counts where
   quoted.

**Not in TODO-07:** chain selection (TODO-10), per-nuclide windowed imaging and Compton strip (TODO-08), desktop UI
tests until the author says the desktop is free.

**Done when** build and `dotnet test Gcam.sln` pass, the Spectrum workspace builds the Cs-137 photopeak live with
its band and the Cs + Co case shows its four bands (checked in the real window once the desktop is free), and the docs
above are updated. No commit — the author commits.
