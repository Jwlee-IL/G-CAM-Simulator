# Prepared only: one worker; 32 calibration/diagnostic seeds, first eight imaging seeds per cell.
# Launch this script in a detached child process; do not rebuild/edit the frozen closure during repair.
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scratchRoot = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'gcam-todo41'))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $scratchRoot 'repair32-v5-20261010'))
if (-not $outputRoot.StartsWith($scratchRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Output must remain under the task scratch root.'
}
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'samples/evidence/manifest-studio-readout-v5.json'))) {
    throw 'V5 is not frozen: complete the declared DEV4 budget selection first.'
}
$env:PYTHONDONTWRITEBYTECODE = '1'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:GCAM_UI_TESTS = '0'
$env:GCAM_RENDER_SNAPSHOTS = '0'
$env:GCAM_EVIDENCE_TESTS = '0'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$processTemp = Join-Path $outputRoot 'tmp'
New-Item -ItemType Directory -Force -Path $processTemp | Out-Null
$env:TEMP = $processTemp
$env:TMP = $processTemp
Push-Location $repoRoot
try {
    python -B samples/evidence/run_seeds.py --manifest samples/evidence/manifest-studio-readout-v5.json `
        --probe samples/evidence/probe-v5/bin/Release/net9.0/Gcam.EvidenceProbe.dll `
        --family t41-repair-Wide-v5 t41-repair-High-v5 --jobs 1 --out $outputRoot
    if ($LASTEXITCODE -ne 0) { throw 'Incomplete v5 repair. Resume in the same root; no forced reruns.' }
    python -B samples/evidence/studio_readout_v5_select.py --runs $outputRoot `
        --manifest samples/evidence/manifest-studio-readout-v5.json --phase repair `
        --out (Join-Path $outputRoot 'repair-v5.json')
    if ($LASTEXITCODE -ne 0) { throw 'V5 aggregation refused; no repair offer table published.' }
}
finally { Pop-Location }
