# Calibration-only DEV4 selection; serial, resumable. This script never starts REPAIR32.
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scratchRoot = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'gcam-todo41'))
$resultRoot = [IO.Path]::GetFullPath((Join-Path $scratchRoot 'explore/v5'))
if (-not $resultRoot.StartsWith($scratchRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Development outputs must remain under the task scratch root.'
}
New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null
$processTemp = Join-Path $resultRoot 'development-tmp'
New-Item -ItemType Directory -Force -Path $processTemp | Out-Null
$env:TEMP = $processTemp
$env:TMP = $processTemp
$env:PYTHONDONTWRITEBYTECODE = '1'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $repoRoot
try {
    $developmentSeeds = @((Get-Content -Raw -LiteralPath 'samples/evidence/seeds.json' | ConvertFrom-Json).T41_DEV4)
    $probe = Join-Path $repoRoot 'samples/evidence/probe-v5-budget/bin/Release/net9.0/Gcam.ReadoutCalibrationBudget.dll'
    foreach ($geometry in @('Wide', 'High')) {
        $levels = if ($geometry -eq 'Wide') { @(8000, 32000) } else { @(64000) }
        $chosen = $false
        foreach ($budget in $levels) {
            $allSucceeded = $true
            foreach ($seed in $developmentSeeds) {
                $out = Join-Path $resultRoot "budget-$geometry-$budget-$seed.json"
                if (-not (Test-Path -LiteralPath $out)) {
                    dotnet $probe $seed $geometry $budget $out
                    if ($LASTEXITCODE -ne 0) { throw 'Calibration process failed; retain the partial development set.' }
                }
                $value = Get-Content -Raw -LiteralPath $out | ConvertFrom-Json
                $count = if ($geometry -eq 'Wide') { 34 } else { 44 }
                if ($value.Seed -ne $seed -or $value.Geometry -ne $geometry -or $value.Budget -ne $budget `
                    -or $value.EnergyKeV -ne 661.7 -or $value.Scored -ne $count*$count*$budget `
                    -or $value.Succeeded -isnot [bool] `
                    -or ($value.Succeeded -and ($value.Markers -ne $count*$count -or $null -ne $value.Failure)) `
                    -or (-not $value.Succeeded -and [string]::IsNullOrWhiteSpace($value.Failure))) {
                    throw 'Corrupt development result; no automatic replacement.'
                }
                if (-not $value.Succeeded) { $allSucceeded = $false }
            }
            if ($allSucceeded) { $chosen = $true; break }
        }
        if (-not $chosen) { throw "$geometry has no successful declared budget; stop without freezing v5." }
    }
    python -B samples/evidence/studio_readout_v5_prepare.py --development $resultRoot
    if ($LASTEXITCODE -ne 0) { throw 'V5 inputs were not frozen.' }
}
finally { Pop-Location }
