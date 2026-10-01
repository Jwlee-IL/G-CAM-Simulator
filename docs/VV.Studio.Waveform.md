# VV.Studio.Waveform — scope and acquired-chain verification status

Scope: shared physical detection-chain selection and the array-wide Waveform workspace; requirement/test mapping, execution limits and headless reproduction. Four-channel position simulation and CR-RC precision redesign are excluded.

## At a glance

- Shared chain is captured in DetectorSettings at Start; retained results use acquired settings, pending edits mark outdated.
- GAGG / NaI / LYSO / BGO map explicitly to transport materials; unsupported CsI is unavailable.
- Default scope uses real acquired times, 10 µs with 20% pretrigger; rate study explicitly re-spaces retained deposits.
- Realistic response is applied once before ADC simulation; ideal removes smear/noise/rise while retaining gain/tail/filter.
- Existing integer shapers are retained; finite filter warm-up and the CR-RC low-signal limitation remain visible.
- Added tests/renders have **not been executed** after sandbox process creation failed. No new physics precision or desktop result is claimed.

## Verification inventory

| Area | Added coverage | Execution |
|---|---|---|
| Configuration and transport | Four supported-material rows; unsupported CsI rejection; no change to engine defaults | Not run |
| Shared response | Four preamps: waveform amplitudes equal the index-addressed measurement, Spectrum bins agree, Imaging uses the same response/windows; acquired identities/cache reset | Not run |
| ADC waveform | Fixed-length raster compared to legacy without second intrinsic smear; actual retained MC associations; real-time/prehistory/pixel pooling; late timestamps; empty noise windows; ideal noise removal | Not run |
| Local study/readout | Fixed-seed rate-study identity/immutability; matched trapezoid readout; suppression on overlap/saturation/partial windows; CR-RC caveat | Not run |
| Core requests/policies | Pure sample cap/conversion; prepared-pyramid identity; visible-only generation; held-event reuse; latest/next; stale/acquired policy; active-edit rejection; cancellation/latest-revision/new-run isolation; error reporting | Not run |
| Evidence | Maximum window cost/cap; retained MC pulse readouts across supported parts/energies/modes; acquired mixed-field chain calibration and independent-service comparison | Opt-in, not run |
| Offscreen WPF | Real retained MC 10 µs / rate study / ideal, shared chain selector and pending/acquired labels, shared zoom/reset, both themes and sizes | Renderer edited, PNG generation not run |

Source files: `tests/Gcam.Studio.Services.Tests/WaveformServiceTests.cs`, `tests/Gcam.Studio.Tests/WaveformWorkspaceTests.cs`, `tests/Gcam.Studio.RenderTests/WaveformRenderTests.cs`; the latter is called within the existing single-STA/Application renderer. This adds 21 default service cases + three Evidence cases and 14 Core cases by source inventory, not a verified runner count. The existing opt-in render test is extended; its test-case count does not increase. Existing engine tests and RTL/cocotb files are unchanged.

## Reproduction

Run in the repository root. No desktop opt-in is needed or authorized by these commands.

```powershell
dotnet build src/Gcam.Studio/Gcam.Studio.csproj -c Release -m:1 /nr:false /p:UseSharedCompilation=false
dotnet test tests/Gcam.Tests -c Release -m:1 /nr:false /p:UseSharedCompilation=false
dotnet test tests/Gcam.Studio.Tests -c Release -m:1 /nr:false /p:UseSharedCompilation=false
$waveSuite = [Diagnostics.Stopwatch]::StartNew()
dotnet test tests/Gcam.Studio.Services.Tests -c Release -m:1 /nr:false /p:UseSharedCompilation=false
$waveSuite.Stop()
"Services command wall time: $($waveSuite.Elapsed.TotalSeconds) s"
# Runner-reported test duration is distinct from command wall time/build overhead.
$env:GCAM_EVIDENCE_TESTS = '1'
dotnet test tests/Gcam.Studio.Services.Tests -c Release -m:1 /nr:false /p:UseSharedCompilation=false --filter FullyQualifiedName~WaveformServiceTests --logger 'console;verbosity=detailed'
Remove-Item Env:GCAM_EVIDENCE_TESTS
$env:GCAM_RENDER_SNAPSHOTS = '1'
dotnet test tests/Gcam.Studio.RenderTests -c Release -m:1 /nr:false /p:UseSharedCompilation=false --logger 'console;verbosity=detailed'
Remove-Item Env:GCAM_RENDER_SNAPSHOTS
```

Planned outputs: `docs/assets/studio-render/waveform-{real10us|rate-study|ideal}-{dark|light}-{1280x800|1440x900}.png` (12 images). These paths are targets, **not generated artifacts in this turn**. The renderer constructs/detaches the production XAML tree, measures/arranges and uses RenderTargetBitmap; it never calls Show, Studio App startup, creates an HWND or sends input. Shared chain selectors are included in each full-shell image, with pending Trapezoid and acquired original chain intentionally different. The actual MC fixture uses default scene/optics/detector, seed 12345, first 128 accepted ListModeSource events; trigger index 64. Rate study uses 1000 kcps, fixed seed 555, preserving original acquisition times in the event records.

## Known limits and execution record

The ADC simulation adds analog/ADC noise after the analytic measurement. Shaped heights therefore are not the analytic MCA spectrum; recovered trapezoid energy is a scope estimate only. CR-RC recovered energy is omitted because of the known integer low-signal limit. No new resolution/pile-up precision test uses an invented tolerance. The small real-MC regression asserts deposit/time/pixel association, and the rate-study test asserts multiple pulses under explicitly simulated arrivals, not a promised real-scene overlap fraction.

Array-wide energy grouping follows the instrument's shared readout. The four ADC position channels, centroid positioning and pile-up mispositioning are absent. Background events follow the existing acquired stream. No afterglow or hand-added continuum is introduced.

Warm-up covers amplitude-dependent pulse support, eight tail constants and filter history but resets the local integer filter; it does not reproduce an acquisition-wide baseline state. Raw integer output is displayed without BLR. A partial acquisition window includes simulated baseline beyond live time and is labelled; its trapezoid energy is suppressed when the required readout horizon is unacquired. A capped request reserves warm-up inside the 10 M total raster samples, so its displayed length is slightly below 80 ms. Plot pyramids are built on the worker; integer shaper and pyramid calls are bounded but cancellation is checked around them rather than inside every iteration. Obsolete results cannot publish.

The first default parallel `dotnet build src/Gcam.Studio -c Release --no-restore` returned exit 1 without compiler errors. A sequential Studio build was then started and completed with **0 warnings /0 errors** (2m15.50s including compiler-server connection delays). While it ran, subsequent read-only PowerShell creation failed with `CreateProcessAsUserW failed: 5 (access denied)`. Per the requested fallback, no further verification commands were launched; the previously running build was only collected. Later tests, render paths, documentation and small source fixes have no final build/test result here. Services default duration, total passed/skipped counts and visual snapshot inspection must be recorded after independent execution; no placeholder is presented as a pass.
