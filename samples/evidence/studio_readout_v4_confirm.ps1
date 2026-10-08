# Prepared only. Calibration uses all CONFIRM32; costs use the predeclared first eight.
# Requires the v4 manifest's exact managed closure. Do not rebuild or edit during a resumed run.
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scratchRoot = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'gcam-todo41'))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $scratchRoot 'confirm32-v4-20261009'))
if (-not $outputRoot.StartsWith($scratchRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Output must remain under the task scratch root.'
}
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$processTemp = Join-Path $outputRoot 'tmp'
New-Item -ItemType Directory -Force -Path $processTemp | Out-Null
$env:TEMP = $processTemp
$env:TMP = $processTemp
$env:PYTHONDONTWRITEBYTECODE = '1'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:GCAM_UI_TESTS = '0'
$env:GCAM_RENDER_SNAPSHOTS = '0'
$env:GCAM_EVIDENCE_TESTS = '0'
Push-Location $repoRoot
try {
    python -B samples/evidence/run_seeds.py --manifest samples/evidence/manifest-studio-readout-v4.json `
        --probe samples/evidence/probe-v4/bin/Release/net9.0/Gcam.EvidenceProbe.dll `
        --family t41-confirm-Baseline-v4 t41-confirm-Sharp-v4 t41-confirm-Wide-v4 t41-confirm-High-v4 `
        --jobs 2 --out $outputRoot
    if ($LASTEXITCODE -ne 0) { throw 'Incomplete v4 ensemble. Reuse the same command to resume; no forced reruns.' }
    python -B samples/evidence/studio_readout_v4_select.py --runs $outputRoot `
        --manifest samples/evidence/manifest-studio-readout-v4.json --phase confirm `
        --out (Join-Path $outputRoot 'confirmation-v4.json')
    if ($LASTEXITCODE -ne 0) { throw 'V4 aggregation refused; no offer table published.' }
}
finally { Pop-Location }
