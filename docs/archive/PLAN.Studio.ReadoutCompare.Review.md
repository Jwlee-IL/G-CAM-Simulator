# PLAN.Studio.ReadoutCompare.Review — measured review of TODO-47

Scope: review of [the reference plan](PLAN.Studio.ReadoutCompare.md), against commit `5c4deda`, 2026-10-15. No product implementation. The author's fifth workspace and four content choices stand.

Status: changes to C1, C4–C7 are needed before implementation. C1 is feasible without another transport, but the required data are **not retained today**. C5's path-specific FWHM windows contradict the current physical projection. C6 describes an applicability evaluator that is absent from this revision.

## Premises and measurements

### Retention and correspondence

| Premise | Finding and code evidence |
|---|---|
| Retained direct assignment for every physical input | **No.** `ListModeSource.cs:237` constructs the direct event, and `:238` puts it with owned sites in `RecordedTransportEvent`; the decay branch does the same at `:276`. `ReadoutDevice.cs:256`'s `DirectCrystal` sums deposits per crystal before choosing the first maximum. `PhysicalListModeSource.cs:97` has `input.Event` at consumption, but `:101`–`:105` keeps only the realised charges, increments the input index, and clears the pending input. It saves sites only for the first 256 inputs (`:20`–`:26`, `:102`). |
| Acquisition snapshot can reconstruct the missing data | **No.** `AcquisitionSession.cs:176`–`:184` stores time/charges and the capped truth sample. `RealisedReadoutHit.cs:3` has only time and four charges. The physical branch never appends `_events`; that append belongs to the direct branch at `AcquisitionSession.cs:191`. The published `Events` therefore stays empty (`:218`), and the flood at `:168` is already Anger-assigned. Inverting charges cannot recover a largest-deposit crystal or a true total deposit. |
| Smallest retention change | At the consumed-input boundary, expose the direct event alongside `LastHits`, clearing it at the start of every advance, and copy its pixel/deposit into the same retained hit. Do not expose a look-ahead event or append only when a conversion occurs. Keep index identity `Hits[i] = accepted physical input i`, including source, ambient and BSR inputs. This needs no second transport, optical response or RNG draw. |
| Memory | `Unsafe.SizeOf<DetectedEvent>() = 24` bytes and `Unsafe.SizeOf<RealisedReadoutHit>() = 40` bytes, measured. Extending the latter with `int PixelX, int PixelY, double DepositKeV` yields a calculated 56-byte struct: **+16 bytes/input**, 15.26 MiB per million inputs. A separate direct-event store is simpler in isolation but costs +24 bytes/input and duplicates time. Prefer the extension. Chunked immutable publication already exists (`ImmutableRecordStore.cs:8`, `:17`); do not turn this into `_events.ToArray()` on every refresh. Array headers, chunk references and publication tails are additional. A 256-element extended tail is 14,336 payload bytes, versus 10,240 today. |
| `DominantHit` identity | The processor chooses the input with largest `hit.Sum × pulse(age)` at the sum's hold maximum (`ReadoutPulseProcessor.cs:310`–`:323`), not the largest energy deposit of the entire acquisition. Streaming adds `_firstHitIndex` to the local index (`:173`), and advances that offset when expired hits are removed (`:199`–`:204`). Thus it addresses the globally appended input history; that history's direct pixel is a separate assignment. |
| Pile-up identity | `ContributingHits` counts contributions above the configured fraction of the noiseless total (`:319`), not all nonzero contributors. `DominantShare` reports the largest fraction. Several histories may feed one conversion, and a nondominant history can still have contributed. A conversion with one significant contributor is not proof of a mathematically isolated pulse. No complete contributor membership is published. |
| Histories without conversions | Below-threshold inputs are retained as hits, and the processor has Busy/WaitRearm states (`ReadoutPulseProcessor.cs:175`, `:187`). `input count − conversion count` is not a general lost-history count. Even `input count − distinct DominantHit` means only **never named dominant**. Existing snapshots cannot determine exact unique participation, split threshold/dead-time losses, or finalize the fate of pending tails. Proposal 3 defines the additional accounting. |

Here and below source paths without a directory are under `src/Gcam.Simulation`, `src/Gcam.Detector/Readout`, or the named Studio layer. In particular, [PhysicalListModeSource](../../src/Gcam.Simulation/Readout/PhysicalListModeSource.cs), [AcquisitionSession](../../src/Gcam.Studio.Services/AcquisitionSession.cs), and [ReadoutPulseProcessor](../../src/Gcam.Detector/Readout/ReadoutPulseProcessor.cs) are the retention and indexing authorities.

### Studio preset pilot

One source at `(0, 0, 1000)` mm, standard Studio Cs-137 emissions, 500 µCi; seed **47001**; **10 s** live horizon; ambient **off**, BSR **0**. Detector defaults: 0.1 mm gap, 0.15 mm entrance absorber, 2 mm backing, default GAGG chain. Real Studio preparations, their pinned budgets/seeds and resolved policy were used. The scratch probe calls the production `PhysicalListModeSource`; it reads the private inner source's just-consumed `LastRecordedEvent.Event` by reflection, once per `LastHits` entry, before it is overwritten. This is measurement instrumentation, not a change to the source or another transport. It checks the missing-data premise directly.

The two images use raw unit counts, finite-mask cross-correlation at 1000 mm, the Studio reconstruction grid, and no flood correction. Direct energy is the **true deposit**, without `MeasurementStage`. “Window” is the explicit inclusive **600–720 keV** window: each image gates its own energy. Assignment statistics in the window rows gate the **Anger measured energy**, then compare its assigned crystal with its dominant history's direct pixel. Those are conditional diagnostics, not a truth-selected mono-line photopeak study. Cs-137's standard emission spectrum and these windows must not be relabelled “662 keV only.” Ambient-off is an intentional measurement condition, not the shell's default ambient setting.

| Preset / channel | Direct image inputs | Selected conversions | Assigned / unknown | Same / assigned single-significant-contributor | Same share | Unknown / selected |
|---|---:|---:|---:|---:|---:|---:|
| Baseline / All | 426 | 360 | 360 / 0 | 257 / 360 | **71.3889%** | **0%** |
| Baseline / Window | 125 | 125 | 125 / 0 | 85 / 125 | **68.0000%** | **0%** |
| Sharp / All | 812 | 687 | 679 / 8 | 357 / 679 | **52.5773%** | **1.1645%** |
| Sharp / Window | 269 | 269 | 269 / 0 | 142 / 269 | **52.7881%** | **0%** |

In both pilots: conversions with several significant contributors **0**; missing dominant index **0**; pending hold at the final horizon **false**. Distinct dominant histories: **360 / 687**; inputs never named dominant: **66 / 125** (Baseline / Sharp). Exact histories with no positive contribution to any recorded hold: **not observable through the present contract**, even in this low-rate pilot; the 66 / 125 must not silently be presented as that count. Unknown assignments remain part of the held conversion population. A window cannot select an unknown with unavailable energy; always retain the unconditional unknown count alongside any window-specific statistic.

| Preset / channel | Direct estimate x, y (mm) | Anger estimate x, y (mm) | Direct / Anger radial error (mm; degrees) | Separation (mm) |
|---|---|---|---|---:|
| Baseline / All and Window | 0.833333, 0.833333 | 0.833333, 0.833333 | 1.178511 / 1.178511; 0.067524° / 0.067524° | 0 |
| Sharp / All | −0.328125, 0.807112 | −0.984375, 0.398897 | 0.871261 / 1.062127; 0.049920° / 0.060855° | 0.772854 |
| Sharp / Window | −0.437500, 0.109375 | −0.729167, 0.048611 | 0.450965 / 0.730785; 0.025838° / 0.041871° | 0.297929 |

Degrees above are `atan(radial error / 1000) × 180/π` for this on-axis scene. A general scene needs angular separation between direction vectors, not this formula applied to an off-axis lateral difference. These are one-seed observations, not precision, bias or F estimates. The lab's **36% different-crystal** claim ([EV-36](../VV.Gcam.Evidence.md#ev-36--four-output-anger-readout-against-direct-crystal-assignment), `AGENTS.Findings.Part2.md:1170`) cannot substitute for any of these denominators. The pilot's window disagreement is **32.00% Baseline / 47.21% Sharp**, with the stated measured-energy conditioning.

| Work measured | Baseline | Sharp |
|---|---:|---:|
| Prepare, including validation | 7.864 s | 54.385 s |
| Transport/readout to 10 s | 0.020 s | 0.035 s |
| Two All decodes, sequential | 5.9844 ms | 81.8896 ms |
| Two Window decodes, sequential | 3.4531 ms | 53.2298 ms |

These are single elapsed observations, not p95 or a full Compare refresh benchmark. They exclude drawing, spectrum projection and assignment aggregation. Both images are decoded through `DefaultSimulationFactory` as in `ImagingProjection.cs:41`. Wide FOV and High-res were not prepared; the latter's 64 k histories/crystal preparation is explicitly outside the normal suite (`AGENTS.Studio.md`, Physical readout section). No claim about their refresh time is supported here.

### Fifth workspace and shortcut

**Fits at 1280 DIP in the detached layout pilot**, with export present. The production caption subtree was extracted from `Views/MainWindow.xaml:86` and loaded with the real theme dictionaries and control templates, with five workspace data items. No `Window`, native handle, Studio app instance, screenshot, input injection or desktop UI test was created. A bare WPF `Application` supplies resources; the caption is measured/arranged only. Bindings to the scratch read-only data context were made OneWay. The fifth item was active (the selected font weight was included).

`WorkspaceSwitch` spans x **133.50–486.33**, width **352.83** (four items: 282.67). The right control group starts at x **565.85 / 539.90 / 566.55** for Start / Continue / Stop. Thus the narrowest separation is **53.57 DIP** in Continue. Export spans x **994–1022**, width **28**. Both themes gave the same geometry. The initial unbound `DesiredSize` printed by the scratch program is not a fit oracle; these are the bounds after binding and layout. This checks 1280 DIPs at the current font environment, not 1280 physical pixels at arbitrary DPI or desktop resize behavior.

The switch uses equal cells (`MainWindow.xaml:109`), so Compare costs about one existing cell, not just its word width. `MainWindow.xaml:67`–`:70` binds only Ctrl+1…4; repository search found no Ctrl+5 binding. Add `Key="D5"`, `CommandParameter="4"` to the existing selection command; `MainViewModel.cs:94` already bounds-checks the workspace collection. Export remains Alt+E (`MainWindow.xaml:66`, `:166`).

## Numbered proposals

1. **C1 — retain the direct facts in the existing hit stream.** Extend the immutable hit value to `RealisedReadoutHit(double ArrivalTimeS, ReadoutCharges Charges, int PixelX, int PixelY, double DepositKeV)`. Obtain its new scalars from the consumed `input.Event`. Index, arrival, pixel and deposit must be identical before and after Stop/Continue. Publish with the existing chunk store, and validate every nonnegative dominant index against the same snapshot's hit count. Keep `AcquisitionSnapshot.Events` semantics unchanged on the physical path; filling it invites legacy spectrum/imaging processing of an already physical acquisition. Reason: minimum additional retained payload (+16 bytes/input), no duplicated time, and one identity for waveform and comparison.

2. **C1/C5 — name the direct reference precisely.** “Direct crystal · ideal deposit energy” means the largest summed-deposit pixel and unsmeared total deposit from the physical module's histories. Do not run `MeasurementStage`, the legacy chain or energy-only pile-up on it (`MeasurementStage.cs:45`, `PhysicalReadoutProjection.cs:9`). Do not promise equivalence to a separately started DirectCrystal acquisition under all configurations: the physical path forces GAGG (`SimulationService.cs:93`) and its ambient module convention differs from the legacy direct ambient path (Findings theme 70, `:1229`). Equality tests should project the exact captured history input, not rely on a second run with the same seed producing the same population. Reason: this preserves the author's theory/realistic comparison and the cost record's explicit ideal-direct definition (`ReadoutCostHelp.cs:25`).

3. **C4/C5 — publish accounting with explicit populations.** Use `HistoryCount`, `HeldCount`, line-specific `AssignedCount`, `UnknownCount`, `MultiContributorHeldCount`, `NoDominantHeldCount`, `DistinctDominantHistoryCount`, and `NeverDominantHistoryCount`. For assignment agreement exclude multi-significant-contributor conversions, retain them as their own displayed count, and show `DominantShare` only as a labelled diagnostic. Never assign a piled conversion to all its histories or call its dominant match a whole-history accuracy. Count each qualifying held conversion once; repeated dominant histories, if any, must not become unique-history counts.

   For the author's requested no-conversion count, add **processor-side participation accounting**, not a subtraction in the VM. Mark an active input when its noiseless pulse makes any positive contribution at a recorded hold; on support expiry finalize whether it ever participated. Publish cumulative `FinalizedHistoryCount`, `NoRecordedContributionCount`, and `UnresolvedHistoryCount` with a processing watermark. State the rule in the UI help. A Stop/preset horizon does not flush future holds or tails; unresolved inputs remain unresolved and Continue resumes their accounting. Small active-hit flags and cumulative counters suffice; no persistent all-contributor list is needed. “Below threshold” versus “dead time” is not an exclusive causal partition in the current model and must not be invented. Reason: significant-contributor count and dominant index alone cannot answer the requested history-loss question. Add synthetic overlap, dead-time, subthreshold and pending-horizon tests, including a nondominant contributor that must not be counted lost.

4. **C4 — define the difference image before coding it.** It is an assignment-disagreement map, not `AngerFlood − DirectFlood`. For the selected All population, bucket eligible assigned conversions by their dominant history's direct pixel: `eligible[p]`, `same[p]`, `different[p]`; display `different[p]/eligible[p]`. Unknowns and pile-up are separate counts, excluded from this denominator. A zero denominator is unavailable (neutral/hatch), not 0% error. Aggregate share is `Σsame/Σeligible`, not the mean of crystal percentages. If the window diagnostic is offered, label its gating explicitly; the pilot uses Anger measured-energy selection. Keep the unconditional unknown count visible. Raw flood counts, corrected flood intensities, and these ratios require different colour bars. Reason: this makes the metric independently countable and avoids treating lost inputs as crystal migration. Do not print 36% as a fixed live result.

5. **C3/C5 — use the shared physical keV window and calibrated-line policy.** Read decoder, focus, selected All/Window channel, inclusive `WindowLowKeV/WindowHighKeV` and Anger flood-correction state from Imaging; no new acquisition controls. Current physical imaging uses explicit edges (`ImagingSettings.cs:10`, `PhysicalReadoutProjection.cs:96`–`:111`), not N × each path's resolution. Reuse `ReadoutPolicy.LineForWindow` and the captured `ReadoutLineView`. Missing/ambiguous lines keep All available and mark Window unavailable with the existing reason; never fall back to another LUT. Physical stripping is already refused (`PhysicalReadoutProjection.cs:71`). For the overlay, select and label the same captured calibration line for Anger bins and its window; otherwise the current broadband reference-line spectrum and a different line's window can be silently mixed (`PhysicalReadoutProjection.cs:29`–`:64`). Use identical bins/edges, raw counts per acquisition, an explicit overflow count and line styles/legend. Do not normalize away trigger or assignment loss.

   Retain C5's in-window counts, but revise FWHM: ideal direct photopeak intrinsic broadening is zero by definition, and its histogram's apparent width is binning, not a fitted detector resolution. Show “ideal deposit energy” there. Physical spectrum currently returns NaN resolution values (`PhysicalReadoutProjection.cs:63`); a trustworthy fitted per-line FWHM is **new analysis**, not an existing snapshot fact. For the first cut show “not estimated” unless a separate specified estimator, fit range, background model, minimum-count rule and unavailable reason are adopted. Never use a noisy live fit to move either path's integration window. Reason: C5's current wording cannot be fulfilled by reusing existing data contracts alone.

6. **C7 — give Compare its own contract and worker, with one atomic result.** Core owns `CompareWorkspaceViewModel`, `IReadoutCompareService` and these proposed values (names are proposals):

   - `CompareKey(AcquisitionId, SettingsRevision, HitCount, HeldCount, LiveTimeS)` labels the captured immutable prefix.
   - `CompareSettings(ImagingSettings Imaging, CompareChannel Channel, double? CalibrationLineKeV)` captures view choices; the VM does not read engine configurations.
   - `CompareResult(Key, DirectImage, AngerImage, DirectSpectrum, AngerSpectrum, AssignmentMap, Accounting, Timings, UnavailableReasons)` publishes both paths together. Image members reuse `ImagingChannel`; spectrum members share immutable bin edges and separate counts/overflow. Assignment members carry integer numerator/denominator arrays as well as displayed ratios. Null estimate/width means unavailable, not a zero error.
   - `Accounting` carries proposal 3's counts plus the selected diagnostic population label. `Timings` separates aggregation, decoding and total projection. Truth errors are derived only for a uniquely identified source; multi-source scenes show peaks without guessing a truth association, and report scalar truth errors as unavailable.

   Services owns direct histogramming, line-aware Anger projection, decoding and engine configuration. Reuse `ImagingProjection`, `MlemDecoderCache` and the physical window/correction helpers; factor shared projection code instead of creating two Imaging VMs or a second SimulationService session. WPF owns only templates, heatmaps, markers and plots. The result key includes acquisition identity because equal prefix counts can occur across Reset; stale results cannot attach to another acquisition. Reason: compiler-enforced boundaries and synchronized populations make the comparison reviewable.

7. **C7 — budget work without extending the acquisition loop.** Acquisition currently reserves 200 ms transport and a nominal 250 ms cadence (`AcquisitionSession.cs:18`–`:19`), while the shell awaits Spectrum and Imaging updates (`MainViewModel.cs:549`). Sharp's observed pair alone costs 53–82 ms. Do not put Compare projection on the acquisition worker or add it to that awaited pair. Use one cancellable service worker, one latest pending request, acquisition/settings revisions, no unbounded queue. Run only while Compare is active; on activation/Stop/settings change process the newest retained prefix. A new snapshot replaces the pending request rather than repeatedly cancelling an almost-finished same-settings calculation. Publish both paths atomically with their prefix time and an updating indicator.

   Provisional acceptance budget: **≤100 ms p95** Compare service projection for cached cross-correlation at the tested preset, **≤16 ms** UI application, at most **4 requests/s**, no concurrent Compare projections and no whole-history copy per tick. These are proposed targets, not measured guarantees. Incrementally aggregate appended hits/conversions; rebuild histograms on window/line changes and decode once per selected path. MLEM uses its cached matrix and the same bounded scheduling; slower presets may publish below 4 Hz with an honest prefix time. Measure Baseline/Sharp plus one larger offered head at 1 k/100 k/1 M retained inputs, warm/cold decoder, allocation and cancellation before claiming those targets. The current pilot establishes only the timing floor, not a production refresh bound. Reason: nominal 4 Hz is an input cadence, not permission to block transport or queue four expensive jobs each second.

8. **C2/C3 — keep the fixed layout, make absence of data explicit.** Keep Compare as workspace five with Ctrl+5. Reuse individual `ImageStackPanel` instances for each heatmap/legend; that control sizes its **first child as a square** (`ImageStackPanel.cs:20`), so one instance is not a layout for two stacked images. Use bounded rows for flood/reconstruction and the spectrum strip, and a scrolling Comparison side panel for metrics and cost rows; no measurements or source dragging. Match image scales between paths where units are the same. Distinguish: (a) direct mode: “Available with the experimental physical readout” and the existing “Choose readout…” route; (b) physical mode without a snapshot: “Start an acquisition to compare”; (c) inputs but no held/assigned/window events: preserve the direct image and show why Anger/that channel is empty; (d) running projection: keep the last internally consistent pair with its time. Never display 0% disagreement from an empty denominator. Reason: C2's single empty state otherwise hides preparation, trigger and window states.

9. **C6 — reuse the record and wording, implement the missing applicability evaluator once.** Use the embedded pin-checked loader (`ReadoutOfferRecordLoader.cs:16`–`:43`), captured preparation `Plan`, `ReadoutOfferGeometry`, `ReadoutCostRow` and `ReadoutCostHelp.Build`. Do not read the JSON from disk at runtime or select rows by the currently editable preset label. `ReadoutPolicy.MatchModule` (`:43`) matches crystal count, pitch, gap, absorber and backing; it intentionally does **not** validate mask rank/pitch/D, focus, source or rate. `ReadoutCostHelp.Build(record, plans)` has no scene input and always says “Benchmark reference only” (`:20`). No code supplies the per-scene difference list described in Findings theme 69 (`AGENTS.Findings.Part2.md:1211`). Thus reuse the existing reference disclaimer, not an imagined existing scene evaluator.

   Add a pure Core `ReadoutCostApplicability.Evaluate(record, capturedPreparation, acquiredConfiguration, compareSettings)` returning `{Cell, ReferenceOnly = true, Differences, UnavailableReason}`. Accept a Core description of acquired scene/geometry rather than parsing JSON in the VM. Compare full profile (mask rank/cell/D, crystal module, focus), mono-line point source versus all actual emissions, source positions/distances, ambient/BSR, explicit window versus benchmark ±15%, decoder/grid/interpolation, correction on **each** path and input-rate condition. Standard isotopes are multi-emission: no standard Studio scene becomes an inherited single-run cost prediction. Unmatched Custom modules have no measured row; Custom names matching a module still need the full profile differences. Flood-corrected Anger alone does not match the benchmark's per-path correction; do not synthesize a direct calibration for this view.

   Show both 1 k and 4 k **ideal-Direct reference exposure** rows, with both rate conditions (isolated, 100,000 input hits/s); do not interpolate to the live count/rate or confuse accepted cps with input cps. Keep confidence intervals, unavailable rows/missing clusters, absolute RMS in degrees and the defined remote-peak probability. Label that definition rather than silently inventing a new gross-error threshold. Preserve F's equal-fluence MSE-ratio meaning, its below-one bias explanation and “not a time factor.” The cost table remains reference even when a geometry matches. Reason: measured current-pair effects and ensemble costs answer different questions, and the present code implements only part of C6's premise.

10. **C8 and verification — keep the stated exclusions, test the corrected contracts.** No Compare export section, independent-SiPM comparison, F estimation or second acquisition. Existing export can still operate; document that the report omits Compare, and ensure its capture does not mistake newly retained physical direct facts for legacy `Events`. Add exact retained-input projection tests including merged deposits/ties, indexing after stream compaction, pending look-ahead, immutable prefixes across Continue, ambient/BSR inputs, and proposal 3's participation populations. Count agreement independently from a small hand-built fixture, including empty pixels, unknowns, multi-contributor holds, repeated dominant ids and invalid indices. Test result revision rejection, inactive workspace behavior, coherent prefix application, line-specific overlay and every empty state. Test reference differences separately from offer eligibility. Preserve the plan's future both-theme 1280×800/1440×900 offscreen checks and planner-run desktop scenario; neither a layout pilot nor this review is a desktop validation record. Reason: the proposed same-seed second-run test misses the central retention and population failure modes.

## Reproduction, execution limits and remaining measurements

Scratch sources and logs are under `%TEMP%\gcam-todo47\`; the source listings below retain the probe logic in this review so the numbers remain reproducible after scratch cleanup. Paths in those listings are portable substitutions for the repository root. Run from the repository root. Build the acquisition probe before creating the nested layout project (otherwise the SDK's default compile glob includes its source), or use explicit compile items.

Every heavy invocation was preceded by `(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory`, with a conservative threshold **12,582,912 KiB** (12 GiB), then run serially under the supplied memguard, with output redirected to a log and the exit code inspected. No desktop/UI-test environment variable was enabled. Build servers and shared compilation were disabled.

```powershell
$scratch = Join-Path $env:TEMP 'gcam-todo47'
$guard = Join-Path $env:TEMP 'gcam-memguard/memguard.py'
# After writing the scratch projects/listings below:
# Before EACH following command, measure memory and skip it if below the threshold:
$free = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory
if ($free -lt 12582912) { throw 'Below 12 GiB; do not start' }
python -B $guard -- dotnet build "$scratch/Probe.csproj" -c Release --disable-build-servers '-m:1' '-p:UseSharedCompilation=false' > "$scratch/build2.log" 2>&1
$LASTEXITCODE # 0
# Repeat the memory check before each command below.
python -B $guard -- dotnet "$scratch/bin/Release/net9.0/Probe.dll" Baseline Sharp > "$scratch/measure.log" 2>&1
$LASTEXITCODE # 0
python -B $guard -- dotnet build "$scratch/layout/Layout.csproj" -c Release --disable-build-servers '-m:1' '-p:UseSharedCompilation=false' > "$scratch/layout-build6.log" 2>&1
$LASTEXITCODE # 0
python -B $guard -- dotnet "$scratch/layout/bin/Release/net9.0-windows/Layout.dll" > "$scratch/layout5.log" 2>&1
$LASTEXITCODE # 0
```

Probe build: **0 warnings, 0 errors**. Main measurement guard: start free 14,309 MiB, minimum free 14,273 MiB, peak working set 206 MiB, exit 0. Final layout guard: exit 0. The first build attempt failed before compilation because PowerShell split unquoted `-m:1`; quoting fixed it. Layout harness iterations failed on namespace serialization, missing local converter, and a TwoWay binding to scratch read-only data; those were scratch-harness failures, corrected before the reported run. All commands exited; no measurement/build process was left running. No deletion, git state mutation, process-stop command or installation was used.

Not measured: Wide/High preset populations, high-rate piled data in Studio geometry, exact unique hold participation (missing API), a physical live FWHM estimator, MLEM performance, long-prefix allocations/p95 refresh, Compare content rendering and desktop/DPI behavior. No full solution baseline/test suite was run for this documentation-only review; the probe and detached layout projects built the referenced production layers. No implementation acceptance or test-pass claim is made.

### Acquisition probe source

The scratch project is a `net9.0` console project with nullable/implicit usings enabled, referencing `src/Gcam.Studio.Services/Gcam.Studio.Services.csproj` by its resolved repository path. For either scratch project, a portable project file can be written from the repository root as below (use the corresponding target, WPF property and reference). Keep the two `Program.cs` listings in their corresponding directories; build the acquisition project before creating the nested layout project.

```powershell
$repo = (Get-Location).Path
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <ProjectReference Include="$repo/src/Gcam.Studio.Services/Gcam.Studio.Services.csproj" />
  </ItemGroup>
</Project>
"@
Set-Content "$scratch/Probe.csproj" $project
# Layout.csproj: net9.0-windows, add <UseWPF>true</UseWPF>,
# reference $repo/src/Gcam.Studio/Gcam.Studio.csproj instead.
```

The following is the executed acquisition C# source:

```csharp
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;
using Gcam.Studio.Services;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.Optics;

var preparation = new ReadoutPreparationService();
foreach (string name in args.Length > 0 ? args : new[]{"Baseline"}) {
var optics = OpticsPreset.All.Single(p=>p.Name==name).Settings!;
var detector = new DetectorSettings();
var watch=Stopwatch.StartNew();
var prep=await preparation.PrepareAsync(optics,detector);
Console.WriteLine($"PREP {name} success={prep.Succeeded} seconds={watch.Elapsed.TotalSeconds:F3}");
if(!prep.Succeeded) continue;
var get=typeof(ReadoutPreparationService).GetMethod("Get",BindingFlags.Instance|BindingFlags.NonPublic)!;
var artifact=get.Invoke(preparation,new object[]{prep.Id})!;
var device=(ReadoutDevice)artifact.GetType().GetProperty("Device")!.GetValue(artifact)!;
var calibration=(ReadoutCalibration)artifact.GetType().GetProperty("Calibration")!.GetValue(artifact)!;
var config=SimulationService.BuildConfig(new[]{new SceneSource()},optics,detector); config.Seed=47001;
config.Detector.Readout=ReadoutPreparationService.Policy(prep.Plan!);
using var source=new PhysicalListModeSource(config,device,calibration,47001);
var inner=(ListModeSource)typeof(PhysicalListModeSource).GetField("_source",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(source)!;
var direct=new List<DetectedEvent>(); var conversions=new List<PhysicalMeasuredEvent>();
watch.Restart();
while(source.ObservedTimeS<10) {
 conversions.AddRange(source.AdvanceUntil(10));
 if(source.LastHits.Count>0) direct.Add(inner.LastRecordedEvent!.Event);
}
Console.WriteLine($"ACQ {name} seconds={watch.Elapsed.TotalSeconds:F3} live={source.ObservedTimeS} rate={source.InputRateCps:R} hits={direct.Count} conversions={conversions.Count} pending={source.HasPendingHold}");
foreach(var mode in new[]{"All","Window"}) {
 var selected=conversions.Where(e=>mode=="All" || e.EnergyKeV>=600 && e.EnergyKeV<=720).ToArray();
 var assigned=selected.Where(e=>e.Crystal>=0).ToArray();
 var paired=assigned.Where(e=>e.Conversion.ContributingHits==1 && e.Conversion.DominantHit>=0).ToArray();
 int same=paired.Count(e=> {var d=direct[e.Conversion.DominantHit]; return e.Crystal==d.PixelY*optics.DetectorPixels+d.PixelX;});
 var df=new DetectorImage(optics.DetectorPixels,optics.DetectorPixels);var af=new DetectorImage(optics.DetectorPixels,optics.DetectorPixels);
 foreach(var d in direct.Where(d=>mode=="All" || d.DepositKeV>=600 && d.DepositKeV<=720))df.Add(d.PixelX,d.PixelY,1);
 foreach(var e in assigned)af.Add(e.Crystal%optics.DetectorPixels,e.Crystal/optics.DetectorPixels,1);
 var decoder=new DefaultSimulationFactory().CreateDecoder(config)!;
 watch.Restart(); var de=decoder.Decode(df).Estimate; var ae=decoder.Decode(af).Estimate; double ms=watch.Elapsed.TotalMilliseconds;
 Console.WriteLine(JsonSerializer.Serialize(new {name,mode,selected=selected.Length,assigned=assigned.Length,unknown=selected.Length-assigned.Length,paired=paired.Length,same,sameShare=paired.Length>0?(double)same/paired.Length:0,unknownShare=selected.Length>0?(double)(selected.Length-assigned.Length)/selected.Length:0,directCounts=df.Raw.ToArray().Sum(),directEstimate=de,angerEstimate=ae,decodePairMs=ms}));
}
var named=conversions.Where(e=>e.Conversion.DominantHit>=0).Select(e=>e.Conversion.DominantHit).Distinct().Count();
Console.WriteLine($"COUNTS piled={conversions.Count(e=>e.Conversion.ContributingHits>1)} noDominant={conversions.Count(e=>e.Conversion.DominantHit<0)} distinctDominant={named} notDominant={direct.Count-named} unknownAll={conversions.Count(e=>e.Crystal<0)}");
Console.WriteLine($"SIZE DetectedEvent={Unsafe.SizeOf<DetectedEvent>()} RealisedHit={Unsafe.SizeOf<RealisedReadoutHit>()}");
}
```

### Detached caption layout probe source

The scratch layout project is a `net9.0-windows` console project with `UseWPF=true`, nullable/implicit usings enabled, referencing `src/Gcam.Studio/Gcam.Studio.csproj`. This source uses the repository working directory in place of the original scratch harness’s resolved repository path. It creates no window. The console’s initial unbound desired width is retained for audit but is not used as evidence of fit.

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;
using System.Windows.Media;
using System.Windows.Automation;
public static class Program {
 [STAThread] public static void Main() {
 var app=new Application(); app.Resources.Add("BoolToVis",new BooleanToVisibilityConverter());
 foreach(var name in new[]{"Tokens.Dark","Metrics","Typography","Controls"}) app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri($"pack://application:,,,/Gcam.Studio;component/Themes/{name}.xaml")});
 XNamespace p="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
 var doc=XDocument.Load(System.IO.Path.Combine(Environment.CurrentDirectory, "src/Gcam.Studio/Views/MainWindow.xaml"));
 var element=new XElement(doc.Descendants(p+"CaptionBar").FirstOrDefault() ?? doc.Descendants().First(e=>e.Name.LocalName=="CaptionBar"));
 foreach(var a in doc.Root!.Attributes().Where(a=>a.IsNamespaceDeclaration))element.SetAttributeValue(a.Name,a.Value);
 foreach(var theme in new[]{"Dark","Light"}) foreach(var label in new[]{"Start","Continue","Stop"}) foreach(var n in new[]{4,5}) {
 app.Resources.MergedDictionaries[0]=new ResourceDictionary{Source=new Uri($"pack://application:,,,/Gcam.Studio;component/Themes/Tokens.{theme}.xaml")};
 var bar=(FrameworkElement)XamlReader.Parse(element.ToString().Replace("{Binding LiveTimeS,", "{Binding LiveTimeS, Mode=OneWay,").Replace("{Binding Speed,", "{Binding Speed, Mode=OneWay,").Replace("{Binding IsActive}", "{Binding IsActive, Mode=OneWay}").Replace("clr-namespace:Gcam.Studio.Controls", "clr-namespace:Gcam.Studio.Controls;assembly=Gcam.Studio"));
 bar.DataContext=new {Workspaces=new[]{"Imaging","Spectrum","Waveform","Detector","Compare"}.Take(n).Select((s,i)=>new {Title=s,IsActive=i==n-1,AutomationId="Workspace."+s}).ToArray(), HasWorkspaceSwitch=true,IsRunning=label=="Stop",IsIdle=label!="Stop", StartLabel=label,LiveTimeS=60,Speed=10,CanEditLiveTime=true,CanEditSpeed=true};
 bar.Measure(new Size(double.PositiveInfinity,48)); double desired=bar.DesiredSize.Width;
 bar.Measure(new Size(1280,48));bar.Arrange(new Rect(0,0,1280,48));bar.UpdateLayout();
 Console.WriteLine($"{theme} {label} n={n} desired={desired:F2} margin={1280-desired:F2}");
 foreach(var e in Walk(bar).OfType<FrameworkElement>().Where(e=>new[]{"WorkspaceSwitch","AcquisitionLiveTime","ExportReport"}.Contains(AutomationProperties.GetAutomationId(e)))) {
 if(AutomationProperties.GetAutomationId(e)=="AcquisitionLiveTime") { var parent=(FrameworkElement)VisualTreeHelper.GetParent(e); var start=parent.TransformToAncestor(bar).Transform(new Point());Console.WriteLine($" rightStack x={start.X:F2} width={parent.ActualWidth:F2}"); } var pos=e.TransformToAncestor(bar).Transform(new Point());Console.WriteLine($" {AutomationProperties.GetAutomationId(e)} x={pos.X:F2} width={e.ActualWidth:F2}");}
 }
 }
 static System.Collections.Generic.IEnumerable<DependencyObject> Walk(DependencyObject root) {yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Walk(VisualTreeHelper.GetChild(root,i)))yield return child;}
}
```

## APPROVAL REQUESTS

None. No implementation or irreversible command is needed to complete this review.
